using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;
using Haare.Util.Logger;
using Haare.Client.Routine;
using VContainer;
using Cysharp.Threading.Tasks;
using System.Threading;

public class MapRandering : NativeRoutine, IMapColorizer
{
    private CreateMap createMap;

    private Sprite wallSprite;
    private Sprite floorSprite;
    private Sprite stairSprite;

    // 아래층(더 깊은 층)으로 내려가는 계단/위층(입구 쪽)으로 올라가는 계단 표시용 — 기존 바닥·계단
    // 타일 위에 겹쳐서 그리는 오버레이 스프라이트(사용자 요청, 2026-07-23). 타일 자체를 바꾸는 게
    // 아니라 그 위에 별도 SpriteRenderer로 얹는다.
    private Sprite stairDownSprite;
    private Sprite stairUpSprite;

    public Tilemap[] floorTilemaps { get; private set; }
    public Vector3Int[] floorOffsets { get; private set; }

    private UnityEngine.Tilemaps.Tile wallTile;
    private UnityEngine.Tilemaps.Tile floorTile;
    private UnityEngine.Tilemaps.Tile stairTile;

    private const int ChunkSize = 8;
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

        // 계단 스프라이트 교체(2026-07-28, 사용자 요청) — stair_down2/stair_up2로 변경.
        // 2026-07-28 추가 정정(사용자 요청 "올라가는 계단과 내려가는 계단 스프라이트를 스왑해줘.
        // 기획자가 의도와 다르대") — 파일명(stair_down2/stair_up2)과 실제 그림이 반대로 그려져 있어
        // 로드 시점에 바꿔 배정한다(RenderStairOverlays의 goesDown 판정 로직 자체는 정상이라 그쪽은
        // 안 건드림).
        if (stairDownSprite == null) stairDownSprite = Resources.Load<Sprite>("obj/stair_up2");
        if (stairUpSprite == null) stairUpSprite = Resources.Load<Sprite>("obj/stair_down2");
        if (stairDownSprite == null || stairUpSprite == null)
        {
            LogHelper.Warning(LogHelper.GAME, "MapRandering: Resources/obj 폴더에서 stair_down2/stair_up2 이미지를 찾지 못했습니다.");
        }

        wallTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        wallTile.sprite = wallSprite;

        floorTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        floorTile.sprite = floorSprite;

        // 계단 타일은 현재 바닥 타일과 동일하게 렌더링 — 아이콘은 RenderStairOverlays가 오버레이로 처리
        // 별도 계단 스프라이트가 필요해지면 stairSprite를 Resources.Load로 로드하고 여기서 할당할 것
        stairTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        stairTile.sprite = floorSprite;
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

            floorTilemaps[f] = tilemap;
            RenderFloor(tilemap, ref floor, f);
            RenderStairOverlays(tilemapObj.transform, ref floor, f);
            ApplyOccupationTint(tilemap, ref floor);
            SetupWallShadowCasters(tilemapObj.transform, ref floor);
        }

        LogHelper.Log(LogHelper.GAME, $"MapRandering: 전체 {floorCount}개 Floor 렌더링 완료.");
    }

    void RenderFloor(Tilemap tilemap, ref Floor floor, int floorIdx)
    {
        int chunkCountX = floor.config.width;
        int chunkCountY = floor.config.height;
        int totalTiles = chunkCountX * ChunkSize * chunkCountY * ChunkSize;
        var positions = new Vector3Int[totalTiles];
        var tiles = new UnityEngine.Tilemaps.TileBase[totalTiles];
        int idx = 0;

        for (int cx = 0; cx < chunkCountX; cx++)
        {
            for (int cy = 0; cy < chunkCountY; cy++)
            {
                Chunks chunk = floor.chunks[cx, cy];
                if (chunk.chunk == null) continue;

                for (int tx = 0; tx < ChunkSize; tx++)
                {
                    for (int ty = 0; ty < ChunkSize; ty++)
                    {
                        string tileName = chunk.chunk[tx, ty].name;
                        UnityEngine.Tilemaps.TileBase tileBase = floorTile;
                        if (tileName == "Wall") tileBase = wallTile;
                        else if (tileName == "Stair") tileBase = stairTile;

                        positions[idx] = new Vector3Int(cx * ChunkSize + tx, cy * ChunkSize + ty, 0);
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

    // PlaceStairTiles(CreateMap.Stairs.cs)가 청크 내부 (3,4)x(3,4) 2x2 블록에 계단 타일을 찍으므로,
    // 그 블록 전체를 덮도록 방향 아이콘(stairDown/stairUp)을 기존 타일 위에 겹쳐 그린다(사용자 요청,
    // 2026-07-23). stairTargetFloor가 현재 층보다 크면(더 깊은 층) 내려가는 계단, 작으면(입구 쪽)
    // 올라가는 계단으로 판단한다.
    private const int StairBlockSize = 2; // PlaceStairTiles와 동일한 블록 크기
    private const float StairOverlayWorldSize = 2f; // 2x2 타일 블록 전체를 덮는 크기(비율 유지, 큰 쪽 기준)
    private const int StairOverlaySortingOrder = 5; // GameSession.SpawnObject의 오브젝트 오버레이와 동일한 관례

    void RenderStairOverlays(Transform parent, ref Floor floor, int floorIdx)
    {
        if (stairDownSprite == null && stairUpSprite == null) return;
        if (floor.chunks == null) return;

        int chunkCountX = floor.config.width;
        int chunkCountY = floor.config.height;

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

                // 계단 블록(2x2) 중심 좌표 — 블록은 (cx*8+3, cy*8+3)~(cx*8+5, cy*8+5) 구간을 차지한다.
                float centerX = cx * ChunkSize + 3 + StairBlockSize / 2f;
                float centerY = cy * ChunkSize + 3 + StairBlockSize / 2f;
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

    // 층 사이에 두는 간격(타일 단위, 고정값) — 사용자 요청(2026-07-23) "계단 위치끼리 맞물리지 말고
    // 층별로 스프라이트 간격 떨어트려줘". 모든 층 Tilemap이 항상 동시에 활성화된 채로 렌더링되므로
    // (ShowFloor로 한 층만 보이게 하는 기능은 아직 어디서도 안 쓰임 — RenderAllFloors 참고), 예전의
    // "계단 위치를 맞춰서 겹쳐 쌓기" 오프셋은 층들이 화면에서 서로 거의 같은 자리에 겹쳐 보이는
    // 문제가 있었다. 계단 정렬 대신 층마다 가로로 나란히 떨어뜨려 배치한다.
    private const int FloorGapTiles = 10;

    Vector3Int[] ComputeSpacedOffsets()
    {
        int floorCount = createMap.map.floors.Length;
        var offsets = new Vector3Int[floorCount];

        int cursorX = 0;
        for (int f = 0; f < floorCount; f++)
        {
            offsets[f] = new Vector3Int(cursorX, 0, 0);

            int widthTiles = createMap.map.floors[f].config.width * ChunkSize;
            cursorX += widthTiles + FloorGapTiles;
        }

        return offsets;
    }

    // 빛(Light2D)이 벽을 통과하지 않게(사용자 요청). 시행착오 요약(2026-07-28):
    // 1) Collider2D 기반(TilemapCollider2D+CompositeCollider2D) — ShadowCaster2D의 자동 소스 판정이
    //    같은 오브젝트의 두 Collider2D 중 어느 쪽을 잡을지 불확실해서 실패(빛이 벽을 그냥 통과).
    // 2) SpriteRenderer 기반 사각형(1개 또는 여러 개로 병합) 캐스터 — 세로 벽은 세로로 조각이 쌓여
    //    이음새가 생기고, 벽 전체 두께를 다 쓰면 방마다 두께가 달라 안쪽 경계가 들쭉날쭉, 표면 한
    //    겹만 쓰면 이번엔 대각선 꼭짓점이 옆 직선 사각형과 합쳐지며 실제 타일 모양과 다른 사각형이
    //    되어 그 이음새에서 계속 빛이 샜다. 근본 원인은 "여러 개의 독립된 사각형 오브젝트"로 실제
    //    타일 모양(방마다 두께가 다르고 L/T/ㄷ/S자 등 비정형이라 절대 사각형이 아님, CreateMap.
    //    RoomPlacement.cs의 shapeTemplates + 방마다 랜덤인 벽 두께)을 흉내 내려 한 것 자체였다.
    // 3) 그래서 흉내 내지 않고 실제 벽 타일 격자를 그대로 외곽선으로 추적(TraceContours)해 그
    //    윤곽선 그대로를 PolygonCollider2D 경로(외곽/구멍)로 넣어봤으나, Unity 문서대로 방향(외곽=
    //    반시계, 구멍=시계)을 맞춰도 안/밖 차단이 계속 거꾸로 나왔다(사용자 확인, 2026-07-28
    //    "래이캐스팅 부여가 반대로 됐다" → 방향을 뒤집어도 "아직 반대로 됨"). PolygonCollider2D는
    //    "채워진 도형"이라 안/밖(구멍) 판정이 꼭 필요한데 그 판정 자체가 우리 기대와 다르게 동작한
    //    것으로 보여 폐기.
    // 4) 그래서 도형을 "채우지" 않고 그냥 "선"으로만 준다 — 폐곡선마다 EdgeCollider2D(닫힌 선) +
    //    ShadowCaster2D를 하나씩 만든다(CreateEdgeShadowCasters). 선은 안/밖 개념이 없어 방향과
    //    무관하게 항상 올바르게 막는다. 콜라이더 하나당 경로 하나뿐이라 폐곡선(=대략 방 개수)만큼
    //    오브젝트가 생기지만 타일 개수보다 훨씬 적어 성능 문제는 없다.
    void SetupWallShadowCasters(Transform parent, ref Floor floor)
    {
        bool[,] isWall = BuildWallMask(ref floor, out int worldW, out int worldH);
        List<List<Vector2>> loops = TraceContours(isWall, worldW, worldH);
        CreateEdgeShadowCasters(parent, loops, "WallShadowCaster");
    }

    // 벽 타일 격자(bool[worldW,worldH], true=Wall) 생성 — SetupWallShadowCasters 및 GameSession의
    // 통합 벽+안개 셰도우 재계산(RebuildFloorFogShadowCasters, 사용자 요청 "문+벽 섀도우캐스팅과
    // 겹치는 안개 모두 한번에 해서 구워줘")이 공용으로 쓴다.
    public static bool[,] BuildWallMask(ref Floor floor, out int worldW, out int worldH)
    {
        int chunkCountX = floor.config.width;
        int chunkCountY = floor.config.height;
        worldW = chunkCountX * ChunkSize;
        worldH = chunkCountY * ChunkSize;

        bool[,] isWall = new bool[worldW, worldH];
        for (int cx = 0; cx < chunkCountX; cx++)
        {
            for (int cy = 0; cy < chunkCountY; cy++)
            {
                Chunks chunk = floor.chunks[cx, cy];
                if (chunk.chunk == null) continue;

                for (int tx = 0; tx < ChunkSize; tx++)
                    for (int ty = 0; ty < ChunkSize; ty++)
                        if (chunk.chunk[tx, ty].name == "Wall")
                            isWall[cx * ChunkSize + tx, cy * ChunkSize + ty] = true;
            }
        }

        return isWall;
    }

    // 격자(mask) 위에서 solid(true) 영역의 외곽선을 그대로 추적해 폐곡선 목록으로 뽑아낸다 — 벽/방
    // 뭉치가 사각형이 아니라 방마다 두께가 다르고 L/T/ㄷ/S자 등 비정형이어도(CreateMap.
    // RoomPlacement.cs shapeTemplates) 근사 없이 실제 타일 모양 그대로 나온다. solid 뭉치 하나가
    // 구멍(방)을 여러 개 가지면 바깥 윤곽선 1개 + 구멍마다 안쪽 윤곽선 1개, 총 여러 개의 폐곡선이
    // 나올 수 있다 — 전부 반환해서 호출부가 EdgeCollider2D 하나씩으로 만든다(CreateEdgeShadowCasters
    // 참고 — 폴리곤 채우기(구멍/외곽 판정)를 아예 안 쓰므로 각 변의 진행 방향은 결과에 영향 없음).
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

    // TraceContours가 뽑아낸 폐곡선마다 EdgeCollider2D(닫힌 선) + ShadowCaster2D 오브젝트를 하나씩
    // 만든다. 처음엔 PolygonCollider2D 하나에 외곽선/구멍(방)을 전부 경로로 몰아넣었는데(바깥은
    // 반시계, 구멍은 시계 — Unity PolygonCollider2D의 문서화된 구멍 규약), 방향을 맞게 뒤집어도
    // 안/밖 차단이 계속 거꾸로였다(사용자 확인, 2026-07-28 "래이캐스팅 부여가 반대로 됐다" →
    // "아직 반대로 됨"). PolygonCollider2D는 "채워진 도형"이라 안/밖(구멍) 판정이 꼭 필요하지만,
    // EdgeCollider2D는 그냥 "선"이라 안/밖 개념 자체가 없다 — 빛은 그 선을 넘어가지 못할 뿐이니
    // 폐곡선 하나하나를 선으로만 넘기면 방향과 무관하게 항상 올바르게 막는다. 대신 콜라이더 하나당
    // 경로 하나만 담을 수 있어 폐곡선 개수만큼(방 개수 정도) 오브젝트가 생기지만, 방 개수는 타일
    // 개수보다 훨씬 적어 성능 문제는 없다.
    public static void CreateEdgeShadowCasters(Transform parent, List<List<Vector2>> loops, string namePrefix, List<GameObject> createdOut = null)
    {
        if (loops == null) return;

        for (int i = 0; i < loops.Count; i++)
        {
            var loop = loops[i];
            if (loop.Count < 3) continue;

            var go = new GameObject($"{namePrefix}_{i}");
            go.transform.SetParent(parent, false);

            // EdgeCollider2D는 닫힌 도형 표현이 따로 없어 시작점을 끝에 한 번 더 넣어 닫아준다.
            var points = new Vector2[loop.Count + 1];
            for (int j = 0; j < loop.Count; j++) points[j] = loop[j];
            points[loop.Count] = loop[0];

            var edgeCollider = go.AddComponent<EdgeCollider2D>();
            edgeCollider.isTrigger = true;
            edgeCollider.points = points;

            go.AddComponent<ShadowCaster2D>();
            createdOut?.Add(go);
        }
    }

    void ClearExistingTilemaps()
    {
        floorTilemaps = null;
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

    // 점령 관련(2026-07-27 신규) — 방 점령 상태별로 바닥 타일에 옅은 색을 입힌다. 벽 타일은 제외한다
    // (요청: "바닥 타일 희미하게"). RenderFloor와 같은 타일 좌표 변환(cx*8+tx, cy*8+ty)을 그대로
    // 재사용해 같은 Tilemap 위에 SetColor만 덧씌운다. 야생(Neutral)/Occupied/Outpost는 착색하지
    // 않는다(사용자 요청, 2026-07-27: "야생 지역은 회색 말고 그냥 원래 색으로") — 기본 바닥 스프라이트
    // 색 그대로 노출된다.
    private static readonly Color HumanRoomTint = new Color(0.25f, 0.45f, 1f, 1f);  // 인류 소유 — 파랑(2026-07-27 사용자 요청으로 더 진하게)
    private static readonly Color MonsterRoomTint = new Color(1f, 0.25f, 0.25f, 1f); // 몬스터(플레이어) 점령 — 빨강(위와 동일 조정)

    void ApplyOccupationTint(Tilemap tilemap, ref Floor floor)
    {
        int chunkCountX = floor.config.width;
        int chunkCountY = floor.config.height;

        // 사용자 요청(2026-07-28, "문이 있는 바닥 공간은 점령으로 인한 바닥 색 변화가 없게") — 문은
        // 항상 특정 타일(Floor.gates → GameSession.GetGateDoorTiles와 동일 공식으로 역산)에 놓이므로,
        // 점령 색칠 전에 미리 그 타일 집합을 구해 건너뛴다.
        HashSet<Vector2Int> doorTiles = CollectDoorTiles(ref floor);

        for (int cx = 0; cx < chunkCountX; cx++)
        {
            for (int cy = 0; cy < chunkCountY; cy++)
            {
                Chunks chunk = floor.chunks[cx, cy];
                if (chunk.chunk == null) continue;

                Color? tint = chunk.occupationState switch
                {
                    OccupationState.HumanControlled => HumanRoomTint,
                    OccupationState.PlayerControlled => MonsterRoomTint,
                    _ => (Color?)null,
                };
                if (tint == null) continue;

                for (int tx = 0; tx < ChunkSize; tx++)
                {
                    for (int ty = 0; ty < ChunkSize; ty++)
                    {
                        if (chunk.chunk[tx, ty].name == "Wall") continue;
                        Vector2Int tilePos2D = new Vector2Int(cx * ChunkSize + tx, cy * ChunkSize + ty);
                        if (doorTiles.Contains(tilePos2D)) continue;
                        Vector3Int pos = new Vector3Int(tilePos2D.x, tilePos2D.y, 0);
                        tilemap.SetTileFlags(pos, TileFlags.None);
                        tilemap.SetColor(pos, tint.Value);
                    }
                }
            }
        }
    }

    // 사용자 요청(2026-07-28) — Floor.gates에 이미 기록된 통로 정보로부터 문이 놓일 모든 타일 좌표를
    // 모은다. GameSession.SpawnDoors가 실제 문 오브젝트를 심을 때 쓰는 것과 동일한
    // GameSession.GetGateDoorTiles 공식을 그대로 재사용해 좌표가 항상 일치하게 한다.
    private static HashSet<Vector2Int> CollectDoorTiles(ref Floor floor)
    {
        var doorTiles = new HashSet<Vector2Int>();
        if (floor.gates == null) return doorTiles;

        foreach (var gate in floor.gates)
        {
            foreach (var row in GameSession.GetGateDoorTiles(gate))
                foreach (var tile in row)
                    doorTiles.Add(tile);
        }

        return doorTiles;
    }

    public void ChangeRoomColor(Room room, Color color)
    {
        if (floorTilemaps == null || floorTilemaps.Length == 0) return;
        // 예전엔 "MVP: 0층 기준"으로 floorTilemaps[0]에 고정 — 야생 몬스터 방은 전부 1층 이상이라
        // (SpawnWildRoomGuards가 0층을 명시적으로 제외) 점령 색칠이 항상 엉뚱한 층(0층 로비)에
        // 적용되고 있었다(사용자 신고 2026-07-27 "점령 처리해도 바닥 색깔이 안 바뀜"). room.Floor를
        // 그대로 써서 실제 방이 있는 층에 칠하도록 수정.
        if (room.Floor < 0 || room.Floor >= floorTilemaps.Length) return;
        Tilemap tm = floorTilemaps[room.Floor];

        // 벽 타일은 칠하지 않는다(사용자 요청 "벽은 색깔 바꾸지 마, 바닥만") — ApplyOccupationTint와
        // 동일한 청크/타일 조회 방식으로 벽 여부를 확인한다.
        Floor floorData = default;
        bool hasFloorData = createMap != null && createMap.map.floors != null
            && room.Floor < createMap.map.floors.Length;
        if (hasFloorData) floorData = createMap.map.floors[room.Floor];

        // 사용자 요청(2026-07-28, "문이 있는 바닥 공간은 점령으로 인한 바닥 색 변화가 없게") —
        // ApplyOccupationTint와 동일하게 문 타일 집합을 구해 건너뛴다.
        HashSet<Vector2Int> doorTiles = hasFloorData ? CollectDoorTiles(ref floorData) : new HashSet<Vector2Int>();

        for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
        {
            for (int y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
            {
                if (hasFloorData && IsWallTile(ref floorData, x, y)) continue;
                if (doorTiles.Contains(new Vector2Int(x, y))) continue;

                Vector3Int pos = new Vector3Int(x, y, 0);
                tm.SetTileFlags(pos, TileFlags.None);
                tm.SetColor(pos, color);
            }
        }
    }

    // ChangeRoomColor 전용 — ApplyOccupationTint와 동일한 청크 좌표 변환(cx*8+tx)으로 벽 타일인지 확인.
    private bool IsWallTile(ref Floor floor, int x, int y)
    {
        if (x < 0 || y < 0 || floor.chunks == null) return false;

        int cx = x / ChunkSize;
        int cy = y / ChunkSize;
        int tx = x % ChunkSize;
        int ty = y % ChunkSize;
        if (cx < 0 || cx >= floor.config.width || cy < 0 || cy >= floor.config.height) return false;

        Chunks chunk = floor.chunks[cx, cy];
        if (chunk.chunk == null) return false;

        return chunk.chunk[tx, ty].name == "Wall";
    }
}

