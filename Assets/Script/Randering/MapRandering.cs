using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;
using Haare.Util.Logger;
using Haare.Client.Routine;
using VContainer;
using Cysharp.Threading.Tasks;
using System.Threading;
#if UNITY_2022_2_OR_NEWER
using UnityEngine.U2D.Animation;
#endif

public class MapRandering : NativeRoutine, IMapColorizer
{
    private CreateMap createMap;

    private Sprite wallSprite;
    private Sprite floorSprite;
    private Sprite stairSprite;

    // 아래층으로 내려가는/위층으로 올라가는 계단 표시용 오버레이 스프라이트 — 타일 자체를 바꾸지 않고
    // 그 위에 별도 SpriteRenderer로 얹는다.
    private Sprite stairDownSprite;
    private Sprite stairUpSprite;

    // 방 점령 표현 — 방 전체를 채우는 오버레이 대신 TraceContours(벽 셰도우캐스팅용 윤곽선 추적)를
    // 재사용해 방의 실제 바닥 윤곽선을 LineRenderer로 그린다(비정형 방 모양도 근사 없이 그대로).
    private readonly Dictionary<(int floor, int roomId), List<LineRenderer>> _roomOutlines
        = new Dictionary<(int, int), List<LineRenderer>>();
    // 방 하나의 윤곽선 조각(_0, _1, ...)들을 한데 묶어두는 부모 오브젝트. 방이 벽으로 갈라져 폐곡선이
    // 여러 개인 경우에도 하이어라키에서 낱개로 흩어지지 않고 "RoomOutline_F{f}_R{roomId}" 하나로 보인다.
    private readonly Dictionary<(int floor, int roomId), Transform> _roomOutlineGroups
        = new Dictionary<(int, int), Transform>();

    public Tilemap[] floorTilemaps { get; private set; }
    public Vector3Int[] floorOffsets { get; private set; }

    private UnityEngine.Tilemaps.Tile wallTile;
    private UnityEngine.Tilemaps.Tile stairTile;

    // ⚠ 임시 기능 — 바닥/벽 스프라이트 바리에이션. Resources/Tile/TileSpriteLibrary.spriteLib(Unity 2D
    // Animation SpriteLibraryAsset — Char_Knight.spriteLib와 동일한 방식, Window > 2D > Sprite Library
    // Editor로 편집)의 "Floor"/"Wall" 카테고리에 담긴 라벨별 스프라이트를 그대로 후보로 쓴다. 0번은
    // 항상 기존 wallSprite/floorSprite와 같은 스프라이트라 라이브러리에 라벨을 안 채워도 기존 룩 그대로.
    // 바리에이션을 "어떤 규칙으로" 배치할지(방 역할/바이옴/인접 타일 등)는 아직 기획이 없어서
    // PickRandomVariant가 완전 랜덤으로 하나를 고르는 자리표시자다 — 규칙이 정해지면 교체할 것.
    private UnityEngine.Tilemaps.Tile[] wallTileVariants;
    private UnityEngine.Tilemaps.Tile[] floorTileVariants;

    // TilemapRenderer 기본 머티리얼(Sprites/Default, Unlit)은 Light2D에 반응하지 않아 URP 2D Lit
    // 셰이더를 명시적으로 물려준다.
    private Material _floorLitMaterial;

    // 맵 1.5배 확장(2026-08-23 사용자 요청)으로 청크 크기가 층별 설정값(FloorConfig.chunkSize)이 됐다
    // — 전 층 공용 상수는 삭제하고 아래 메서드들이 각자 floor.config.chunkSize를 참조한다.
    private GameObject mapRoot;

    [Inject]
    public void Construct(CreateMap createMap)
    {
        this.createMap = createMap;
    }

    public override async UniTask Initialize(CancellationToken cts)
    {
        await base.Initialize(cts);
        // DoRandering() 호출은 MapManager가 맵 데이터를 준비한 뒤 명시적으로 호출하도록 제거됨
    }

    public void DoRandering(CreateMap targetMap = null) { if (targetMap != null) { this.createMap = targetMap; } BuildTileCache(); RenderAllFloors(); } private void _OldDoRanderingUnused()
    {
        BuildTileCache();
        RenderAllFloors();
    }

    void BuildTileCache()
    {
        if (wallSprite == null || floorSprite == null)
        {
            // 사용자가 지정한 각각의 텍스처 로드 (Resources 폴더 기준)
            wallSprite = Resources.Load<Sprite>("Tile_StoneWall");
            floorSprite = Resources.Load<Sprite>("FloorTexture");

            if (wallSprite == null || floorSprite == null)
            {
                LogHelper.Warning(LogHelper.GAME, "MapRandering: Resources 폴더에서 타일 이미지를 찾지 못해 임시 단색 이미지를 생성합니다.");
                
                if (wallSprite == null) wallSprite = CreateColorSprite(Color.gray);
                if (floorSprite == null) floorSprite = CreateColorSprite(Color.white);
            }
        }

        // 파일명(stair_down2/stair_up2)과 실제 그림이 반대로 그려져 있어 로드 시점에 바꿔 배정한다
        // (RenderStairOverlays의 goesDown 판정 로직 자체는 정상).
        SpriteCache.GetOrLoad(ref stairDownSprite, "obj/stair_up2");
        SpriteCache.GetOrLoad(ref stairUpSprite, "obj/stair_down2");
        if (stairDownSprite == null || stairUpSprite == null)
        {
            LogHelper.Warning(LogHelper.GAME, "MapRandering: Resources/obj 폴더에서 stair_down2/stair_up2 이미지를 찾지 못했습니다.");
        }

        if (floorTileVariants == null || wallTileVariants == null)
            BuildTileVariants();

        // 기존 필드 — SetTileToWall(디버그 단일 셀 갱신)이 계속 참조하므로 0번 변형(원본 스프라이트)으로 유지.
        wallTile = wallTileVariants[0];

        // 계단 타일은 현재 바닥 타일과 동일하게 렌더링 — 아이콘은 RenderStairOverlays가 오버레이로 처리
        // 별도 계단 스프라이트가 필요해지면 stairSprite를 Resources.Load로 로드하고 여기서 할당할 것
        stairTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        stairTile.sprite = floorSprite;
    }

    // ⚠ 임시 기능 — TileSpriteLibrary.spriteLib의 "Floor"/"Wall" 카테고리 라벨을 읽어 바리에이션
    // Tile 배열을 만든다. 라이브러리가 없거나 카테고리가 비어있으면 원본 스프라이트 1개짜리 배열로
    // 폴백(기존 룩 그대로).
    void BuildTileVariants()
    {
        Sprite[] floorLabelSprites = null;
        Sprite[] wallLabelSprites = null;

#if UNITY_2022_2_OR_NEWER
        var library = Resources.Load<SpriteLibraryAsset>("Tile/TileSpriteLibrary");
        if (library != null)
        {
            floorLabelSprites = LoadCategorySprites(library, "Floor");
            wallLabelSprites = LoadCategorySprites(library, "Wall");
        }
#endif

        floorTileVariants = BuildVariantTiles(floorSprite, floorLabelSprites);
        wallTileVariants = BuildVariantTiles(wallSprite, wallLabelSprites);
    }

#if UNITY_2022_2_OR_NEWER
    static Sprite[] LoadCategorySprites(SpriteLibraryAsset library, string category)
    {
        var sprites = new List<Sprite>();
        foreach (string label in library.GetCategoryLabelNames(category))
        {
            Sprite sprite = library.GetSprite(category, label);
            if (sprite != null) sprites.Add(sprite);
        }
        return sprites.ToArray();
    }
#endif

    // baseSprite(항상 0번)에 라이브러리 라벨 스프라이트를 이어붙인다 — 중복(라이브러리 라벨이 base와
    // 같은 스프라이트를 가리키는 경우, 지금 기본 상태가 그렇다)은 제외한다.
    UnityEngine.Tilemaps.Tile[] BuildVariantTiles(Sprite baseSprite, Sprite[] extraVariants)
    {
        var sprites = new List<Sprite> { baseSprite };
        if (extraVariants != null)
            foreach (var s in extraVariants)
                if (s != null && s != baseSprite) sprites.Add(s);

        var tiles = new UnityEngine.Tilemaps.Tile[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            var t = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            t.sprite = sprites[i];
            tiles[i] = t;
        }
        return tiles;
    }

    // ⚠ 임시 기능 — 배치 규칙 미정이라 완전 랜덤. 규칙이 정해지면 이 메서드를 그 규칙으로 교체할 것.
    private static UnityEngine.Tilemaps.Tile PickRandomVariant(UnityEngine.Tilemaps.Tile[] variants)
    {
        if (variants == null || variants.Length == 0) return null;
        if (variants.Length == 1) return variants[0];
        return variants[UnityEngine.Random.Range(0, variants.Length)];
    }

    private Sprite CreateColorSprite(Color color)
    {
        Texture2D tex = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }

    public void RenderAllFloors()
    {
        if (createMap == null || createMap.map.floors == null || createMap.map.floors.Length == 0)
        {
            LogHelper.Error(LogHelper.GAME, "MapRandering: map 데이터가 없습니다.");
            return;
        }

        ClearExistingTilemaps();

        int floorCount = createMap.map.floors.Length;
        floorOffsets = ComputeSpacedOffsets();

        if (mapRoot == null)
        {
            mapRoot = new GameObject("MapRoot_Grid");
            mapRoot.AddComponent<Grid>();
        }

        floorTilemaps = new Tilemap[floorCount];

        if (_floorLitMaterial == null)
            _floorLitMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));

        for (int f = 0; f < floorCount; f++)
        {
            Floor floor = createMap.map.floors[f];
            if (floor.chunks == null) continue;

            GameObject tilemapObj = new GameObject($"F{f}_Tilemap");
            tilemapObj.transform.SetParent(mapRoot.transform, false);
            tilemapObj.transform.localPosition = new Vector3(floorOffsets[f].x, floorOffsets[f].y, 0f);

            Tilemap tilemap = tilemapObj.AddComponent<Tilemap>();
            TilemapRenderer renderer = tilemapObj.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = f;
            renderer.material = _floorLitMaterial;

            floorTilemaps[f] = tilemap;
            RenderFloor(tilemap, ref floor, f);
            RenderStairOverlays(tilemapObj.transform, ref floor, f);
            ApplyOccupationTint(tilemap, ref floor, f);
            // 1층 이상은 RebuildFloorFogShadowCasters가 벽+안개 통합 캐스터를 구성하므로
            // 벽 전용 캐스터는 0층(로비, 안개 없음)에만 생성한다.
            if (f == 0) SetupWallShadowCasters(tilemapObj.transform, ref floor);
        }

        LogHelper.Log(LogHelper.GAME, $"MapRandering: 전체 {floorCount}개 Floor 렌더링 완료.");
    }

    void RenderFloor(Tilemap tilemap, ref Floor floor, int floorIdx)
    {
        int chunkCountX = floor.config.width;
        int chunkCountY = floor.config.height;
        int chunkSize = floor.config.chunkSize;
        int totalTiles = chunkCountX * chunkSize * chunkCountY * chunkSize;
        var positions = new Vector3Int[totalTiles];
        var tiles = new UnityEngine.Tilemaps.TileBase[totalTiles];
        int idx = 0;

        for (int cx = 0; cx < chunkCountX; cx++)
        {
            for (int cy = 0; cy < chunkCountY; cy++)
            {
                Chunks chunk = floor.chunks[cx, cy];
                if (chunk.chunk == null) continue;

                for (int tx = 0; tx < chunkSize; tx++)
                {
                    for (int ty = 0; ty < chunkSize; ty++)
                    {
                        string tileName = chunk.chunk[tx, ty].name;
                        UnityEngine.Tilemaps.TileBase tileBase;
                        if (tileName == "Wall") tileBase = PickRandomVariant(wallTileVariants);
                        else if (tileName == "Stair") tileBase = stairTile;
                        else tileBase = PickRandomVariant(floorTileVariants);

                        positions[idx] = new Vector3Int(cx * chunkSize + tx, cy * chunkSize + ty, 0);
                        tiles[idx] = tileBase;
                        idx++;
                    }
                }
            }
        }

        if (idx < totalTiles)
        {
            var usedPositions = new Vector3Int[idx];
            var usedTiles = new UnityEngine.Tilemaps.TileBase[idx];
            System.Array.Copy(positions, usedPositions, idx);
            System.Array.Copy(tiles, usedTiles, idx);
            tilemap.SetTiles(usedPositions, usedTiles);
            LogHelper.Log(LogHelper.GAME, $"MapRandering: F{floorIdx}에 총 {idx}개의 타일을 배치했습니다. (일부 청크 비어있음)");
        }
        else
        {
            tilemap.SetTiles(positions, tiles);
            LogHelper.Log(LogHelper.GAME, $"MapRandering: F{floorIdx}에 총 {idx}개의 타일을 모두 꽉 채워 배치했습니다.");
        }
    }

    // PlaceStairTiles(CreateMap.Stairs.cs)가 찍은 2x2 계단 블록 전체를 덮도록 방향 아이콘을 기존
    // 타일 위에 겹쳐 그린다. stairTargetFloor가 현재 층보다 크면 내려가는 계단, 작으면 올라가는 계단.
    private const int StairBlockSize = 2; // PlaceStairTiles와 동일한 블록 크기
    private const float StairOverlayWorldSize = 2f; // 2x2 타일 블록 전체를 덮는 크기(비율 유지, 큰 쪽 기준)
    private const int StairOverlaySortingOrder = 5; // GameSession.SpawnObject의 오브젝트 오버레이와 동일한 관례

    void RenderStairOverlays(Transform parent, ref Floor floor, int floorIdx)
    {
        if (stairDownSprite == null && stairUpSprite == null) return;
        if (floor.chunks == null) return;

        int chunkCountX = floor.config.width;
        int chunkCountY = floor.config.height;
        int chunkSize = floor.config.chunkSize;
        // PlaceStairTiles(CreateMap.Stairs.cs)와 동일한 2x2 블록 좌상단 오프셋 — chunkSize=8이면 3(원래 값).
        int stairLo = chunkSize / 2 - 1;

        for (int cx = 0; cx < chunkCountX; cx++)
        {
            for (int cy = 0; cy < chunkCountY; cy++)
            {
                Chunks chunk = floor.chunks[cx, cy];
                if (chunk.chunk == null || chunk.stairTargetFloor < 0) continue;

                bool goesDown = chunk.stairTargetFloor > floorIdx;
                Sprite sprite = goesDown ? stairDownSprite : stairUpSprite;
                if (sprite == null) continue;

                var go = new GameObject(goesDown ? "StairDownIcon" : "StairUpIcon");
                go.transform.SetParent(parent, false);

                // 계단 블록(2x2) 중심 좌표 — 블록은 (cx*cs+stairLo, cy*cs+stairLo)~(+1,+1) 구간을 차지한다.
                float centerX = cx * chunkSize + stairLo + StairBlockSize / 2f;
                float centerY = cy * chunkSize + stairLo + StairBlockSize / 2f;
                go.transform.localPosition = new Vector3(centerX, centerY, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = StairOverlaySortingOrder;

                float maxDim = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
                if (maxDim > 0f)
                {
                    float scale = StairOverlayWorldSize / maxDim;
                    go.transform.localScale = new Vector3(scale, scale, 1f);
                }
            }
        }
    }

    // 층 사이 간격(타일 단위) — 모든 층 Tilemap이 항상 동시에 활성화돼 렌더링되므로 층마다 가로로
    // 나란히 배치한다. CameraController 클램프가 카메라 중심만 묶고 줌아웃 시 보이는 폭은 못 줄이므로
    // 최대 줌아웃에서도 옆 층이 겹쳐 보이지 않도록 여유를 크게 뒀다.
    private const int FloorGapTiles = 120;

    Vector3Int[] ComputeSpacedOffsets()
    {
        int floorCount = createMap.map.floors.Length;
        var offsets = new Vector3Int[floorCount];

        int cursorX = 0;
        for (int f = 0; f < floorCount; f++)
        {
            offsets[f] = new Vector3Int(cursorX, 0, 0);

            int widthTiles = createMap.map.floors[f].config.width * createMap.map.floors[f].config.chunkSize;
            cursorX += widthTiles + FloorGapTiles;
        }

        return offsets;
    }

    // floorIndex 층이 월드 좌표에서 차지하는 전체 사각 범위 — CameraController의 층별 클램프가
    // 필요로 해서 추가. 카메라 쪽에서 청크 크기를 중복 정의하지 않도록 여기서 한 번만 계산해 공개한다.
    public bool TryGetFloorWorldBounds(int floorIndex, out Rect bounds)
    {
        bounds = default;
        if (createMap?.map.floors == null || floorOffsets == null) return false;
        if (floorIndex < 0 || floorIndex >= createMap.map.floors.Length || floorIndex >= floorOffsets.Length) return false;

        Floor floor = createMap.map.floors[floorIndex];
        Vector3Int origin = floorOffsets[floorIndex];

        bounds = new Rect(origin.x, origin.y, floor.config.width * floor.config.chunkSize, floor.config.height * floor.config.chunkSize);
        return true;
    }

    // 빛(Light2D)이 벽을 통과하지 않게 한다. Collider2D 자동 소스/사각형 캐스터 근사/PolygonCollider2D
    // 안팎 판정 등 여러 방식이 문제가 있어 전부 폐기하고, 벽 타일 격자를 TraceContours로 외곽선만
    // 추적해 폐곡선마다 EdgeCollider2D+ShadowCaster2D를 만든다 — 방향 무관하게 항상 올바르고 콜라이더
    // 개수도 방 개수 수준이라 가볍다.
    void SetupWallShadowCasters(Transform parent, ref Floor floor)
    {
        bool[,] isWall = BuildWallMask(ref floor, out int worldW, out int worldH);
        List<List<Vector2>> loops = TraceContours(isWall, worldW, worldH);
        CreateEdgeShadowCasters(parent, loops, "WallShadowCaster");
    }

    // 벽 타일 격자(bool[worldW,worldH], true=Wall) 생성 — SetupWallShadowCasters 및 GameSession의
    // 통합 벽+안개 셰도우 재계산(RebuildFloorFogShadowCasters)이 공용으로 쓴다.
    public static bool[,] BuildWallMask(ref Floor floor, out int worldW, out int worldH)
    {
        int chunkCountX = floor.config.width;
        int chunkCountY = floor.config.height;
        int chunkSize = floor.config.chunkSize;
        worldW = chunkCountX * chunkSize;
        worldH = chunkCountY * chunkSize;

        bool[,] isWall = new bool[worldW, worldH];
        for (int cx = 0; cx < chunkCountX; cx++)
        {
            for (int cy = 0; cy < chunkCountY; cy++)
            {
                Chunks chunk = floor.chunks[cx, cy];
                if (chunk.chunk == null) continue;

                for (int tx = 0; tx < chunkSize; tx++)
                    for (int ty = 0; ty < chunkSize; ty++)
                        if (chunk.chunk[tx, ty].name == "Wall")
                            isWall[cx * chunkSize + tx, cy * chunkSize + ty] = true;
            }
        }

        return isWall;
    }

    // 2026-08-24 debug 전용(GameSession.DebugConvertFloorTileToWall) — 이미 렌더링된 타일맵의 셀 하나만
    // 벽 스프라이트로 바꾼다. RenderFloor 전체를 다시 돌리지 않고 그 칸만 갱신.
    public void SetTileToWall(int floorIndex, Vector3Int localPos)
    {
        if (floorTilemaps == null || floorIndex < 0 || floorIndex >= floorTilemaps.Length) return;
        Tilemap tilemap = floorTilemaps[floorIndex];
        if (tilemap == null || wallTile == null) return;
        tilemap.SetTile(localPos, wallTile);
    }

    // 격자(mask) 위에서 solid(true) 영역의 외곽선을 그대로 추적해 폐곡선 목록으로 뽑아낸다 — 비정형
    // 모양이어도 근사 없이 실제 타일 모양 그대로 나온다. solid 뭉치가 구멍을 여러 개 가지면 바깥
    // 윤곽선 1개 + 구멍마다 안쪽 윤곽선 1개가 나올 수 있어 전부 반환한다(폴리곤 채우기를 안 쓰므로
    // 각 변의 진행 방향은 결과에 영향 없음).
    public static List<List<Vector2>> TraceContours(bool[,] mask, int w, int h)
    {
        bool Solid(int x, int y) => x >= 0 && x < w && y >= 0 && y < h && mask[x, y];

        var edgesFrom = new Dictionary<Vector2Int, List<Vector2Int>>();
        void AddEdge(Vector2Int a, Vector2Int b)
        {
            if (!edgesFrom.TryGetValue(a, out var list)) { list = new List<Vector2Int>(); edgesFrom[a] = list; }
            list.Add(b);
        }

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (!mask[x, y]) continue;

                if (!Solid(x - 1, y)) AddEdge(new Vector2Int(x, y), new Vector2Int(x, y + 1));         // 왼쪽 변
                if (!Solid(x + 1, y)) AddEdge(new Vector2Int(x + 1, y + 1), new Vector2Int(x + 1, y)); // 오른쪽 변
                if (!Solid(x, y - 1)) AddEdge(new Vector2Int(x + 1, y), new Vector2Int(x, y));         // 아래쪽 변
                if (!Solid(x, y + 1)) AddEdge(new Vector2Int(x, y + 1), new Vector2Int(x + 1, y + 1)); // 위쪽 변
            }
        }

        var visited = new HashSet<(Vector2Int from, Vector2Int to)>();
        var loops = new List<List<Vector2>>();

        foreach (var kvp in edgesFrom)
        {
            foreach (var firstTo in kvp.Value)
            {
                Vector2Int loopStart = kvp.Key;
                if (visited.Contains((loopStart, firstTo))) continue;

                var loop = new List<Vector2>();
                Vector2Int cur = loopStart;
                Vector2Int next = firstTo;
                while (true)
                {
                    visited.Add((cur, next));
                    loop.Add(new Vector2(cur.x, cur.y));
                    cur = next;
                    if (cur == loopStart) break;

                    if (!edgesFrom.TryGetValue(cur, out var options)) break; // 기형 격자 — 더 못 이어감

                    Vector2Int? pick = null;
                    foreach (var opt in options)
                    {
                        if (!visited.Contains((cur, opt))) { pick = opt; break; }
                    }
                    if (pick == null) break;
                    next = pick.Value;
                }

                if (loop.Count >= 3) loops.Add(loop);
            }
        }

        return loops;
    }

    // TraceContours가 뽑아낸 폐곡선마다 ShadowCaster2D 오브젝트를 하나씩 만든다. EdgeCollider2D 매개
    // 경로는 에디터 전용 API 의존으로 빌드에서 1x1 기본 박스로 대체되는 버그가 있어, m_ShapePath에
    // 직접 써서 에디터/빌드 동일 동작 + 브로드페이즈 부하도 없앤다.
    private static readonly FieldInfo s_FieldShapePath =
        typeof(ShadowCaster2D).GetField("m_ShapePath", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo s_FieldShapePathHash =
        typeof(ShadowCaster2D).GetField("m_ShapePathHash", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo s_FieldForceRebuild =
        typeof(ShadowCaster2D).GetField("m_ForceShadowMeshRebuild", BindingFlags.NonPublic | BindingFlags.Instance);

    public static void CreateEdgeShadowCasters(Transform parent, List<List<Vector2>> loops, string namePrefix, List<GameObject> createdOut = null)
    {
        if (loops == null) return;

        for (int i = 0; i < loops.Count; i++)
        {
            var loop = loops[i];
            if (loop.Count < 3) continue;

            var go = new GameObject($"{namePrefix}_{i}");
            go.transform.SetParent(parent, false);

            var shapePath = new Vector3[loop.Count];
            for (int j = 0; j < loop.Count; j++)
                shapePath[j] = new Vector3(loop[j].x, loop[j].y, 0f);

            var caster = go.AddComponent<ShadowCaster2D>();
            // Awake()가 즉시 실행돼 기본 박스를 세팅하므로, 실제 윤곽선으로 덮어쓴다.
            s_FieldShapePath.SetValue(caster, shapePath);
            s_FieldShapePathHash.SetValue(caster, shapePath.GetHashCode());
            s_FieldForceRebuild.SetValue(caster, true);

            createdOut?.Add(go);
        }
    }

    void ClearExistingTilemaps()
    {
        floorTilemaps = null;
        // 아래 mapRoot 자식 파괴가 outline GameObject도 함께 정리하므로(_roomOverlays.Clear()와
        // 동일한 관례) 여기서는 딕셔너리만 비운다.
        _roomOutlines.Clear();
        _roomOutlineGroups.Clear();
        if (mapRoot != null)
        {
            for (int i = mapRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = mapRoot.transform.GetChild(i);
                if (Application.isPlaying) Object.Destroy(child.gameObject);
                else Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    public void ShowFloor(int floorIndex)
    {
        if (floorTilemaps == null) return;
        for (int f = 0; f < floorTilemaps.Length; f++)
            if (floorTilemaps[f] != null) floorTilemaps[f].gameObject.SetActive(f == floorIndex);
    }

    public void ShowAllFloors()
    {
        if (floorTilemaps == null) return;
        for (int f = 0; f < floorTilemaps.Length; f++)
            if (floorTilemaps[f] != null) floorTilemaps[f].gameObject.SetActive(true);
    }

    // 점령 표현 — 야생(Wild)/인류/몬스터 진영별로 검은색/파란색/빨간색 윤곽선을 그린다. 윤곽선은
    // 얇은 선이라 완전 불투명. public으로 열어서 OffenseProcessor.GetRoomOwnerColor가 직접 참조한다.
    public static readonly Color WildRoomOutlineColor     = Color.black;
    public static readonly Color HumanRoomOutlineColor    = new Color(0.25f, 0.55f, 1f,   1f); // 인류 소유 — 파랑
    public static readonly Color MonsterRoomOutlineColor  = new Color(1f,    0.25f, 0.25f, 1f); // 몬스터 점령 — 빨강

    void ApplyOccupationTint(Tilemap tilemap, ref Floor floor, int floorIdx)
    {
        int chunkCountX = floor.config.width;
        int chunkCountY = floor.config.height;

        // 청크를 roomId별로 대표 색 하나로 묶는다(한 방의 모든 청크는 항상 같은 occupationState).
        var roomColors = new Dictionary<int, Color>();
        for (int cx = 0; cx < chunkCountX; cx++)
        {
            for (int cy = 0; cy < chunkCountY; cy++)
            {
                Chunks chunk = floor.chunks[cx, cy];
                if (chunk.chunk == null || chunk.roomId < 0) continue;

                Color color = chunk.occupationState switch
                {
                    OccupationState.HumanControlled  => HumanRoomOutlineColor,
                    OccupationState.PlayerControlled => MonsterRoomOutlineColor,
                    _ => WildRoomOutlineColor,
                };
                roomColors[chunk.roomId] = color;
            }
        }

        foreach (var kvp in roomColors)
            SetRoomOutline(floorIdx, kvp.Key, ref floor, kvp.Value, tilemap.transform);
    }

    public void ChangeRoomColor(Room room, Color color)
    {
        if (createMap?.map.floors == null) return;
        if (room.Floor < 0 || room.Floor >= createMap.map.floors.Length) return;
        if (floorTilemaps == null || room.Floor >= floorTilemaps.Length || floorTilemaps[room.Floor] == null) return;

        Floor floor = createMap.map.floors[room.Floor];
        SetRoomOutline(room.Floor, room.RoomId, ref floor, color, floorTilemaps[room.Floor].transform);
    }

    // roomId 소속이면서 벽이 아닌 타일들의 격자 마스크를 만든다 — BuildWallMask와 동일한 월드 크기라
    // TraceContours 윤곽선 좌표가 이 층 타일맵의 로컬 좌표와 그대로 일치한다. 문이 있는 경계의 이 방
    // 쪽 문턱 타일은 마스크에서 제외해 윤곽선이 그 자리를 돌아가며 문/복도 폭만큼 빈틈을 만든다 —
    // 인접 방도 자기 쪽 문턱을 똑같이 제외하므로 두 틈이 합쳐져 복도가 선 없이 뚫려 보인다.
    private static bool[,] BuildRoomFloorMask(ref Floor floor, int roomId, out int worldW, out int worldH)
    {
        int chunkCountX = floor.config.width;
        int chunkCountY = floor.config.height;
        int chunkSize = floor.config.chunkSize;
        worldW = chunkCountX * chunkSize;
        worldH = chunkCountY * chunkSize;

        bool[,] isFloor = new bool[worldW, worldH];
        for (int cx = 0; cx < chunkCountX; cx++)
        {
            for (int cy = 0; cy < chunkCountY; cy++)
            {
                Chunks chunk = floor.chunks[cx, cy];
                if (chunk.chunk == null || chunk.roomId != roomId) continue;

                for (int tx = 0; tx < chunkSize; tx++)
                    for (int ty = 0; ty < chunkSize; ty++)
                        if (chunk.chunk[tx, ty].name != "Wall")
                            isFloor[cx * chunkSize + tx, cy * chunkSize + ty] = true;
            }
        }

        ExcludeOwnGateDoorTiles(ref floor, roomId, isFloor, worldW, worldH);
        return isFloor;
    }

    // floor.gates 중 이 방과 접한 게이트마다 "이 방 쪽" 문턱 타일 행을 마스크에서 false로 되돌린다.
    // DoorSystem.GetGateDoorTiles의 [tilesA, tilesB] 순서는 순전히 기하학적 규칙일 뿐 gate.roomA/
    // roomB와 무관하므로(FindHostileExitDoor가 겪었던 것과 동일한 함정), 각 행의 대표 타일이 실제로
    // 속한 청크의 roomId를 직접 조회해서 골라낸다.
    private static void ExcludeOwnGateDoorTiles(ref Floor floor, int roomId, bool[,] isFloor, int worldW, int worldH)
    {
        if (floor.gates == null) return;
        int chunkSize = floor.config.chunkSize;

        foreach (var gate in floor.gates)
        {
            if (gate.roomA != roomId && gate.roomB != roomId) continue;

            var tileRows = DoorSystem.GetGateDoorTiles(gate, chunkSize);
            foreach (var row in tileRows)
            {
                if (row.Count == 0) continue;

                Vector2Int sample = row[0];
                int cx = sample.x / chunkSize, cy = sample.y / chunkSize;
                if (cx < 0 || cx >= floor.chunks.GetLength(0) || cy < 0 || cy >= floor.chunks.GetLength(1)) continue;
                if (floor.chunks[cx, cy].roomId != roomId) continue; // 인접 방 쪽 행 — 이 방 트레이스와 무관

                foreach (var tile in row)
                    if (tile.x >= 0 && tile.x < worldW && tile.y >= 0 && tile.y < worldH)
                        isFloor[tile.x, tile.y] = false;
            }
        }
    }

    // 방 하나의 바닥 윤곽선을 LineRenderer(들)로 그리거나 갱신한다. 폐곡선이 여러 개면 그만큼 여러 개
    // 쓰고, 같은 (층, roomId) 키에 이미 만든 게 있으면 재사용, 이번에 불필요한 나머지는 비활성화만
    // 한다(다음 소유권 전환 때 재사용).
    private const int RoomOutlineSortingOrder = 1; // 타일맵(0) 위, 계단 아이콘(5) 아래
    private const float RoomOutlineWidth = 0.12f;

    private void SetRoomOutline(int floorIdx, int roomId, ref Floor floor, Color color, Transform parent)
    {
        bool[,] mask = BuildRoomFloorMask(ref floor, roomId, out int worldW, out int worldH);
        List<List<Vector2>> loops = TraceContours(mask, worldW, worldH);

        var key = (floorIdx, roomId);
        if (!_roomOutlines.TryGetValue(key, out var renderers))
        {
            renderers = new List<LineRenderer>();
            _roomOutlines[key] = renderers;
        }

        if (!_roomOutlineGroups.TryGetValue(key, out var group) || group == null)
        {
            var groupGo = new GameObject($"RoomOutline_F{floorIdx}_R{roomId}");
            groupGo.transform.SetParent(parent, false);
            group = groupGo.transform;
            _roomOutlineGroups[key] = group;
        }

        for (int i = 0; i < loops.Count; i++)
        {
            var loop = loops[i];
            if (loop.Count < 3) continue;

            LineRenderer lr;
            if (i < renderers.Count && renderers[i] != null)
            {
                lr = renderers[i];
                lr.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject($"Outline_{i}");
                go.transform.SetParent(group, false);
                lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.loop = true;
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.sortingOrder = RoomOutlineSortingOrder;
                lr.numCapVertices = 2;
                lr.numCornerVertices = 2;
                if (i < renderers.Count) renderers[i] = lr;
                else renderers.Add(lr);
            }

            lr.startWidth = RoomOutlineWidth;
            lr.endWidth = RoomOutlineWidth;
            lr.startColor = color;
            lr.endColor = color;
            lr.positionCount = loop.Count;
            for (int p = 0; p < loop.Count; p++)
                lr.SetPosition(p, new Vector3(loop[p].x, loop[p].y, 0f));
        }

        // 이전엔 있었지만 이번엔 안 쓰는 여분 LineRenderer는 꺼둔다(방 모양이 바뀌어 폐곡선 개수가
        // 줄어든 경우 — 예: 파괴됐던 벽이 재설치돼 두 덩어리가 다시 하나로 합쳐지는 등).
        for (int i = loops.Count; i < renderers.Count; i++)
            if (renderers[i] != null) renderers[i].gameObject.SetActive(false);
    }
}

