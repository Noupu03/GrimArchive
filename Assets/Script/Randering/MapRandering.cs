using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
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

        if (stairDownSprite == null) stairDownSprite = Resources.Load<Sprite>("obj/stair_down");
        if (stairUpSprite == null) stairUpSprite = Resources.Load<Sprite>("obj/stair_up");
        if (stairDownSprite == null || stairUpSprite == null)
        {
            LogHelper.Warning(LogHelper.GAME, "MapRandering: Resources/obj 폴더에서 stair_down/stair_up 이미지를 찾지 못했습니다.");
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
                        Vector3Int pos = new Vector3Int(cx * ChunkSize + tx, cy * ChunkSize + ty, 0);
                        tilemap.SetTileFlags(pos, TileFlags.None);
                        tilemap.SetColor(pos, tint.Value);
                    }
                }
            }
        }
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

        for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
        {
            for (int y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
            {
                if (hasFloorData && IsWallTile(ref floorData, x, y)) continue;

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

