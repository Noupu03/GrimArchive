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
    // PulseRoomOutline 중복 호출 시 이전 펄스를 조용히 중단시키는 세대 카운터 — 값이 바뀌면 이전
    // 루프는 스스로 종료한다(같은 LineRenderer를 두 루프가 동시에 써서 깜빡이는 것을 방지).
    private readonly Dictionary<(int floor, int roomId), int> _outlinePulseGeneration
        = new Dictionary<(int, int), int>();

    public Tilemap[] floorTilemaps { get; private set; }
    public Vector3Int[] floorOffsets { get; private set; }

    // ⚠ 임시 기능 — 바닥/벽 스프라이트 바리에이션 소스. Resources/Tile/TileSpriteLibrary.spriteLib
    // (Unity 2D Animation SpriteLibraryAsset — Char_Knight.spriteLib와 동일한 방식, Window > 2D >
    // Sprite Library Editor로 편집)의 "Floor"/"Wall" 카테고리 라벨을 한 번만 읽어 캐시해둔다. 라이브러리가
    // 없거나 카테고리가 비어있으면 null 그대로(BuildVariantTiles가 원본 스프라이트로 폴백).
    private SpriteLibraryAsset _spriteLibrary;
    private Sprite[] _floorLabelSprites;
    private Sprite[] _wallLabelSprites;
    private bool _tileLibraryLoaded;

    // 층별 색상 테마 배정표(2026-09-27, 단일 활성 테마에서 층별 배정으로 확장 — 사용자 요청). 배정
    // 내용 자체는 Tools(new)/맵/타일 색상 테마 창이 Resources/MapColorTheme_FloorAssignments 에셋에 써넣는다.
    private const string FloorColorThemesResourcePath = "MapColorTheme_FloorAssignments";
    private MapFloorColorThemes _floorColorThemes;

    // 벽 자동 타일 연결(회의록 2026-09-27) — 층마다 색 테마가 다를 수 있어(위 배정표) Wall/Floor Tile을
    // 층별로 따로 굽는다. WallVariant 10종 라벨(WallAutoTileMath.GetSpriteLibraryLabel)이 비어있으면
    // (=정림이 아직 실제 아트를 안 채운 상태) wallSprite로 폴백해 형태는 지금 룩 그대로 유지된다.
    private class FloorTileSet
    {
        public UnityEngine.Tilemaps.Tile[] wallVariants;
        public UnityEngine.Tilemaps.Tile[] floorVariants;
        public UnityEngine.Tilemaps.Tile[] wallShapeTiles;
        public UnityEngine.Tilemaps.Tile stairTile;
    }
    private readonly Dictionary<int, FloorTileSet> _floorTileSets = new Dictionary<int, FloorTileSet>();

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

        if (!_tileLibraryLoaded)
            LoadTileLibrary();

        if (_floorColorThemes == null)
            _floorColorThemes = Resources.Load<MapFloorColorThemes>(FloorColorThemesResourcePath);

        // 맵을 다시 생성할 때마다 배정표를 새로 반영하도록 층별 캐시를 비운다 — 이전 세션 값이 아니라
        // 지금 배정표 내용 그대로 다시 굽는다(2026-09-27, 재생성 시 테마가 안 바뀌어 보이는 문제 방지).
        _floorTileSets.Clear();
    }

    // TileSpriteLibrary.spriteLib의 "Floor"/"Wall" 카테고리 라벨을 한 번만 읽어 캐시한다(테마별로
    // 다시 읽을 필요 없음 — 라벨/스프라이트 자체는 테마와 무관, 색만 GetOrBuildFloorTileSet에서 입힌다).
    void LoadTileLibrary()
    {
        _tileLibraryLoaded = true;
#if UNITY_2022_2_OR_NEWER
        _spriteLibrary = Resources.Load<SpriteLibraryAsset>("Tile/TileSpriteLibrary");
        if (_spriteLibrary != null)
        {
            _floorLabelSprites = LoadCategorySprites(_spriteLibrary, "Floor");
            _wallLabelSprites = LoadCategorySprites(_spriteLibrary, "Wall");
        }
#endif
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

    // 층 하나의 벽/바닥 Tile 세트를 그 층에 배정된 테마 색으로 구워 캐시한다(2026-09-27, 층별 배정
    // 지원 — 층마다 색이 다를 수 있어 더 이상 전 층 공유 배열을 쓸 수 없다).
    FloorTileSet GetOrBuildFloorTileSet(int floorIndex)
    {
        if (_floorTileSets.TryGetValue(floorIndex, out var cached)) return cached;

        MapColorTheme theme = _floorColorThemes != null ? _floorColorThemes.GetThemeForFloor(floorIndex) : null;
        Color wallTint = theme != null ? theme.wallColor : Color.white;
        Color floorTint = theme != null ? theme.floorColor : Color.white;

        // 계단 타일은 바닥 타일과 같은 스프라이트를 쓴다(아이콘은 RenderStairOverlays가 오버레이로
        // 처리) — floorTint를 똑같이 입혀야 아이콘이 못 덮는 가장자리가 주변 바닥과 이어져 보인다
        // (2026-09-27 사용자 신고 "계단 스프라이트 뒤쪽부분 바닥 색 안바뀌는 문제" 수정).
        var stairTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        stairTile.sprite = floorSprite;
        stairTile.color = floorTint;

        var set = new FloorTileSet
        {
            wallVariants = BuildVariantTiles(wallSprite, _wallLabelSprites, wallTint),
            floorVariants = BuildVariantTiles(floorSprite, _floorLabelSprites, floorTint, preferLibraryOnly: true),
            wallShapeTiles = BuildWallShapeTiles(wallTint),
            stairTile = stairTile,
        };
        _floorTileSets[floorIndex] = set;
        return set;
    }

    // WallVariant 12종 각각의 Tile을 만든다 — TileSpriteLibrary "Wall" 카테고리에서 GetSpriteLibraryLabel
    // 라벨(형태별 4개만 존재, 2026-09-27 축소)로 스프라이트를 찾고, 없으면(라이브러리 자체가 없거나
    // 그 라벨만 비어있어도) wallSprite로 폴백한다. 같은 형태를 공유하는 variant끼리는 스프라이트가
    // 같고 회전(tile.transform)만 다르다.
    UnityEngine.Tilemaps.Tile[] BuildWallShapeTiles(Color wallTint)
    {
        var variantValues = (WallVariant[])System.Enum.GetValues(typeof(WallVariant));
        var tiles = new UnityEngine.Tilemaps.Tile[variantValues.Length];
        foreach (WallVariant variant in variantValues)
        {
            Sprite sprite = null;
#if UNITY_2022_2_OR_NEWER
            if (_spriteLibrary != null)
                sprite = _spriteLibrary.GetSprite("Wall", WallAutoTileMath.GetSpriteLibraryLabel(variant));
#endif
            if (sprite == null) sprite = wallSprite;

            var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            tile.sprite = sprite;
            tile.color = wallTint;
            // 2026-09-27 회의록 후속: 형태별 기준 스프라이트 1장(GetSpriteLibraryLabel이 이제 12종을
            // 4라벨로 묶어 반환)을 방향마다 회전시켜 재사용한다 — 정확한 회전각은
            // WallAutoTileMath.GetRotationDegrees 참고(실물 아트로 아직 시각 검증 안 됨, 틀렸으면 그
            // 표만 뒤집으면 됨). RuleTile 자체 관례를 따라 LockTransform도 같이 설정.
            tile.transform = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, WallAutoTileMath.GetRotationDegrees(variant)));
            tile.flags = UnityEngine.Tilemaps.TileFlags.LockTransform;
            tiles[(int)variant] = tile;
        }
        return tiles;
    }

    // baseSprite(항상 0번)에 라이브러리 라벨 스프라이트를 이어붙인다 — 중복(라이브러리 라벨이 base와
    // 같은 스프라이트를 가리키는 경우, 지금 기본 상태가 그렇다)은 제외한다.
    // preferLibraryOnly=true면 라이브러리에 실제로 채워진 변형이 있는 한 baseSprite를 완전히 배제한다
    // (라이브러리가 비어있을 때만 baseSprite로 폴백). 바닥 전용 — 회의록 2026-09-27 버그 신고("라이브러리
    // 바닥 스프라이트를 바꿨는데 기존에 쓰던 잔디 스프라이트가 섞여서 배치됨") 수정: 예전엔 라이브러리
    // 유무와 무관하게 항상 baseSprite를 포함해서, 라이브러리를 완전히 다른 스프라이트로 바꿔도 옛
    // 하드코딩 스프라이트가 무작위 풀에 계속 섞여 있었다. 벽 쪽 호출(wallVariants, SetTileToWall 디버그
    // 전용이 [0]만 읽음)은 기존 동작 그대로 유지해야 하므로 기본값은 false로 둔다.
    UnityEngine.Tilemaps.Tile[] BuildVariantTiles(Sprite baseSprite, Sprite[] extraVariants, Color tint, bool preferLibraryOnly = false)
    {
        var sprites = new List<Sprite>();
        if (preferLibraryOnly)
        {
            if (extraVariants != null)
                foreach (var s in extraVariants) if (s != null) sprites.Add(s);
            if (sprites.Count == 0) sprites.Add(baseSprite);
        }
        else
        {
            sprites.Add(baseSprite);
            if (extraVariants != null)
                foreach (var s in extraVariants)
                    if (s != null && s != baseSprite) sprites.Add(s);
        }

        var tiles = new UnityEngine.Tilemaps.Tile[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            var t = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            t.sprite = sprites[i];
            t.color = tint;
            tiles[i] = t;
        }
        return tiles;
    }

    // ⚠ 임시 기능 — 배치 규칙 미정이라 완전 랜덤. 바닥 타일 전용(벽은 아래 GetWallShapeTile이 대체).
    private static UnityEngine.Tilemaps.Tile PickRandomVariant(UnityEngine.Tilemaps.Tile[] variants)
    {
        if (variants == null || variants.Length == 0) return null;
        if (variants.Length == 1) return variants[0];
        return variants[UnityEngine.Random.Range(0, variants.Length)];
    }

    // 벽 자동 타일 연결(회의록 2026-09-27) — (wx,wy) 벽 타일의 8방향 인접 상태를 isWall 격자에서 읽어
    // WallAutoTileMath로 10종 중 하나를 판정하고, 그 WallVariant에 해당하는 Tile을 반환한다.
    private static UnityEngine.Tilemaps.TileBase GetWallShapeTile(bool[,] isWall, bool[,] isFloor, int worldW, int worldH, int wx, int wy, UnityEngine.Tilemaps.Tile[] wallShapeTiles)
    {
        bool n = IsSetAt(isWall, worldW, worldH, wx, wy + 1);
        bool s = IsSetAt(isWall, worldW, worldH, wx, wy - 1);
        bool e = IsSetAt(isWall, worldW, worldH, wx + 1, wy);
        bool w = IsSetAt(isWall, worldW, worldH, wx - 1, wy);
        bool ne = IsSetAt(isWall, worldW, worldH, wx + 1, wy + 1);
        bool nw = IsSetAt(isWall, worldW, worldH, wx - 1, wy + 1);
        bool se = IsSetAt(isWall, worldW, worldH, wx + 1, wy - 1);
        bool sw = IsSetAt(isWall, worldW, worldH, wx - 1, wy - 1);

        bool isFloorN = IsSetAt(isFloor, worldW, worldH, wx, wy + 1);
        bool isFloorS = IsSetAt(isFloor, worldW, worldH, wx, wy - 1);
        bool isFloorE = IsSetAt(isFloor, worldW, worldH, wx + 1, wy);
        bool isFloorW = IsSetAt(isFloor, worldW, worldH, wx - 1, wy);

        WallVariant variant = WallAutoTileMath.SelectVariant(n, s, e, w, ne, nw, se, sw, isFloorN, isFloorS, isFloorE, isFloorW);
        return wallShapeTiles[(int)variant];
    }

    private static bool IsSetAt(bool[,] grid, int worldW, int worldH, int x, int y)
        => x >= 0 && x < worldW && y >= 0 && y < worldH && grid[x, y];

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

        // 벽 자동 타일 연결용 — 청크 경계를 넘나드는 8방향 인접 판정을 위해 층 전체를 미리 평탄화한다
        // (BuildWallMask와 동일한 산출물, 셰도우캐스터 쪽과 별개로 렌더링 시점에 한 번 더 계산).
        bool[,] isWall = BuildWallMask(ref floor, out int worldW, out int worldH);
        bool[,] isFloor = BuildFloorMask(ref floor, worldW, worldH);
        FloorTileSet tileSet = GetOrBuildFloorTileSet(floorIdx);

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
                        int wx = cx * chunkSize + tx;
                        int wy = cy * chunkSize + ty;

                        UnityEngine.Tilemaps.TileBase tileBase;
                        if (tileName == "Wall") tileBase = GetWallShapeTile(isWall, isFloor, worldW, worldH, wx, wy, tileSet.wallShapeTiles);
                        else if (tileName == "Stair") tileBase = tileSet.stairTile;
                        else tileBase = PickRandomVariant(tileSet.floorVariants);

                        positions[idx] = new Vector3Int(wx, wy, 0);
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

    // 벽 방향(상/하, 좌/우) 판정 전용 — BuildWallMask의 "Wall" 격자와 대칭되는 "Floor" 전용 격자
    // (2026-09-27 회의록 후속 요청 — Vertical/Horizontal이 선택되는 조건 자체가 "동서(또는 남북) 둘
    // 다 벽 아님"이라, 벽 아님 여부만으로는 어느 쪽이 방 내부인지 못 가른다. Stair/미개척 영역은
    // 둘 다 false로 나와 애매하면 WallAutoTileMath가 기본값(Top/Left)으로 폴백한다.
    static bool[,] BuildFloorMask(ref Floor floor, int worldW, int worldH)
    {
        bool[,] isFloor = new bool[worldW, worldH];
        int chunkCountX = floor.config.width;
        int chunkCountY = floor.config.height;
        int chunkSize = floor.config.chunkSize;

        for (int cx = 0; cx < chunkCountX; cx++)
        {
            for (int cy = 0; cy < chunkCountY; cy++)
            {
                Chunks chunk = floor.chunks[cx, cy];
                if (chunk.chunk == null) continue;

                for (int tx = 0; tx < chunkSize; tx++)
                    for (int ty = 0; ty < chunkSize; ty++)
                        if (chunk.chunk[tx, ty].name == "Floor")
                            isFloor[cx * chunkSize + tx, cy * chunkSize + ty] = true;
            }
        }

        return isFloor;
    }

    // 2026-08-24 debug 전용(GameSession.DebugConvertFloorTileToWall) — 이미 렌더링된 타일맵의 셀 하나만
    // 벽 스프라이트로 바꾼다. RenderFloor 전체를 다시 돌리지 않고 그 칸만 갱신.
    public void SetTileToWall(int floorIndex, Vector3Int localPos)
    {
        if (floorTilemaps == null || floorIndex < 0 || floorIndex >= floorTilemaps.Length) return;
        Tilemap tilemap = floorTilemaps[floorIndex];
        if (tilemap == null) return;

        var tileSet = GetOrBuildFloorTileSet(floorIndex);
        if (tileSet.wallVariants == null || tileSet.wallVariants.Length == 0) return;
        tilemap.SetTile(localPos, tileSet.wallVariants[0]);
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

    // ── 방 테두리 펄스("웅웅거리는" 점령 연출, 2026-09-25 + 후속 "퍼지는 느낌" 재요청) ──────────
    // 테두리 전체가 같은 위상으로 균일하게 깜빡이던 1차안 대신, waveCount개의 봉우리가 테두리를 따라
    // travelSpeed로 계속 돌면서(=위치별 위상이 다름 → "퍼지는" 느낌) decay로 서서히 잦아든다.
    // LineRenderer.widthCurve/colorGradient(모두 라인 길이 0~1 정규화 위치 기준)를 매 프레임 다시
    // 만들어 위치별로 다른 값을 준다 — Gradient는 색상 키가 최대 8개라는 Unity 제약이 있어 색은 8개,
    // 폭은 AnimationCurve라 제약이 없어 24개로 더 곱게 샘플링한다.
    private const float RoomOutlinePulseDurationSeconds = 1.5f;
    private const float RoomOutlinePulseWaveCount = 3f;      // 테두리 한 바퀴에 동시에 보이는 파동 개수
    private const float RoomOutlinePulseTravelSpeed = 1.5f;  // 파동이 테두리를 도는 속도(초당 바퀴 수)
    private const float RoomOutlinePulseWidthAmplitude = 0.15f; // 기본 굵기(0.12) 대비 파동 정점 추가 굵기
    private const float RoomOutlinePulseBrightenRatio = 0.6f;   // 파동 정점에서 흰색과 섞는 비율
    private const int RoomOutlinePulseWidthKeyCount = 24;
    private const int RoomOutlinePulseColorKeyCount = 8; // Unity Gradient 색상 키 최대치

    public void PulseRoomOutline(Room room)
    {
        if (room == null) return;
        var key = (room.Floor, room.RoomId);
        if (!_roomOutlines.TryGetValue(key, out var renderers) || renderers.Count == 0) return;

        int generation = (_outlinePulseGeneration.TryGetValue(key, out int g) ? g : 0) + 1;
        _outlinePulseGeneration[key] = generation;

        // 매 프레임 "이전 프레임 색"에서 다시 섞으면 하얗게 누적되므로, 지금(=ChangeRoomColor로 이미
        // 소유 진영 색이 반영된 상태)의 색을 기준값으로 한 번만 캡처해 그 값에서 매번 다시 섞는다.
        var baseColors = new List<Color>(renderers.Count);
        foreach (var lr in renderers) baseColors.Add(lr != null ? lr.startColor : Color.white);

        PulseRoomOutlineAsync(key, renderers, baseColors, generation).Forget();
    }

    // t(0~1, 테두리 위 정규화 위치)와 elapsed(경과시간)로 그 지점의 파동 세기를 계산한다.
    private static float RoomOutlineWaveIntensity(float t, float elapsed, float decay)
    {
        float phase = (t * RoomOutlinePulseWaveCount - elapsed * RoomOutlinePulseTravelSpeed) * Mathf.PI * 2f;
        return Mathf.Max(0f, Mathf.Sin(phase)) * decay;
    }

    private async UniTaskVoid PulseRoomOutlineAsync(
        (int floor, int roomId) key, List<LineRenderer> renderers, List<Color> baseColors, int generation)
    {
        var widthKeys = new Keyframe[RoomOutlinePulseWidthKeyCount];
        var alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };

        float elapsed = 0f;
        while (elapsed < RoomOutlinePulseDurationSeconds)
        {
            // 같은 방이 다시 펄스되면(예: 짧은 시간 내 재점령) generation이 바뀌어 이전 루프는 조용히 멈춘다.
            if (!_outlinePulseGeneration.TryGetValue(key, out int current) || current != generation) return;

            float decay = 1f - elapsed / RoomOutlinePulseDurationSeconds;

            for (int k = 0; k < RoomOutlinePulseWidthKeyCount; k++)
            {
                float t = k / (float)(RoomOutlinePulseWidthKeyCount - 1);
                float wave = RoomOutlineWaveIntensity(t, elapsed, decay);
                widthKeys[k] = new Keyframe(t, RoomOutlineWidth + RoomOutlinePulseWidthAmplitude * wave);
            }
            var widthCurve = new AnimationCurve(widthKeys);

            for (int i = 0; i < renderers.Count; i++)
            {
                LineRenderer lr = renderers[i];
                if (lr == null || !lr.gameObject.activeSelf) continue;

                lr.widthCurve = widthCurve;

                var colorKeys = new GradientColorKey[RoomOutlinePulseColorKeyCount];
                for (int k = 0; k < RoomOutlinePulseColorKeyCount; k++)
                {
                    float t = k / (float)(RoomOutlinePulseColorKeyCount - 1);
                    float wave = RoomOutlineWaveIntensity(t, elapsed, decay);
                    colorKeys[k] = new GradientColorKey(Color.Lerp(baseColors[i], Color.white, wave * RoomOutlinePulseBrightenRatio), t);
                }
                var gradient = new Gradient();
                gradient.SetKeys(colorKeys, alphaKeys);
                lr.colorGradient = gradient;
            }

            elapsed += Time.deltaTime;
            await UniTask.Yield();
        }

        // 다른 펄스가 이미 이어받지 않았을 때만 기준값으로 되돌린다(이어받았으면 그쪽이 알아서 마무리).
        if (_outlinePulseGeneration.TryGetValue(key, out int finalGen) && finalGen == generation)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                LineRenderer lr = renderers[i];
                if (lr == null) continue;
                ResetOutlineRendererToFlat(lr, baseColors[i]);
            }
        }
    }

    // widthCurve/colorGradient를 균일한 값으로 되돌린다 — 펄스가 자연 종료될 때뿐 아니라
    // SetRoomOutline이 소유권 갱신으로 다시 그릴 때도 항상 호출해, 중단된 펄스가 남긴 굴곡진
    // widthCurve/colorGradient가 이후의 갱신을 계속 가리는 일이 없게 한다(둘 다 startWidth/
    // startColor보다 우선 적용되는 값이라 이렇게 명시적으로 맞춰줘야 한다).
    private static void ResetOutlineRendererToFlat(LineRenderer lr, Color color)
    {
        lr.widthCurve = AnimationCurve.Constant(0f, 1f, RoomOutlineWidth);
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        lr.colorGradient = gradient;
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
            // 진행 중이던 방 테두리 펄스(PulseRoomOutline)가 widthCurve/colorGradient를 굴곡지게
            // 남겨뒀을 수 있다 — 소유권이 바뀌어 다시 그리는 지금은 항상 균일한 값으로 되돌린다.
            ResetOutlineRendererToFlat(lr, color);
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

