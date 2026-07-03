using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Haare.Util.Logger;

public class MapRandering : MonoBehaviour
{
    [Header("참조")]
    public CreateMap createMap;

    [Header("스프라이트 (Assets/Asset/Atras.png 슬라이스)")]
    public Sprite wallSprite;
    public Sprite floorSprite;
    public Sprite stairSprite;

    // 층별 Tilemap (런타임 생성)
    [HideInInspector] public Tilemap[] floorTilemaps;

    // 층별 월드 오프셋 (계단 정렬 기반, 외부 참조용)
    [HideInInspector] public Vector3Int[] floorOffsets;

    // 생성된 TileBase 캐시
    private UnityEngine.Tilemaps.Tile wallTile;
    private UnityEngine.Tilemaps.Tile floorTile;
    private UnityEngine.Tilemaps.Tile stairTile;

    private const int ChunkSize = 8;

    void Start()
    {
        //DoRandering();
    }

    public void DoRandering()
    {
        BuildTileCache();
        RenderAllFloors();
    }

    // TileBase 오브젝트를 미리 생성하여 반복 생성 비용 제거
    void BuildTileCache()
    {
        wallTile        = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        wallTile.sprite = wallSprite;

        floorTile        = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        floorTile.sprite = floorSprite;

        stairTile        = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        stairTile.sprite = stairSprite != null ? stairSprite : floorSprite;
    }

    // ── 전체 Floor(F0~F3) 렌더링 ──
    // 각 Floor마다 독립 Tilemap을 생성하고, 층간 계단이 같은 월드 좌표를 공유하도록 오프셋 적용
    public void RenderAllFloors()
    {
        if (createMap == null)
        {
            LogHelper.Error(LogHelper.GAME, "MapRandering: createMap이 연결되지 않았습니다.");
            return;
        }

        if (createMap.map.floors == null || createMap.map.floors.Length == 0)
        {
            LogHelper.Error(LogHelper.GAME, "MapRandering: map.floors가 null이거나 비어 있습니다.");
            return;
        }

        // 기존 자식 Tilemap 정리
        ClearExistingTilemaps();

        int floorCount = createMap.map.floors.Length;

        // 계단 정렬 기반 오프셋 계산
        floorOffsets = ComputeStairAlignedOffsets();

        // Grid 컴포넌트 확보 (없으면 자동 추가)
        Grid grid = GetComponent<Grid>();
        if (grid == null)
            grid = gameObject.AddComponent<Grid>();

        // 각 Floor별 Tilemap 생성 및 렌더링
        floorTilemaps = new Tilemap[floorCount];

        for (int f = 0; f < floorCount; f++)
        {
            Floor floor = createMap.map.floors[f];
            if (floor.chunks == null) continue;

            // 자식 GameObject 생성
            GameObject tilemapObj = new GameObject($"F{f}_Tilemap");
            tilemapObj.transform.SetParent(transform, false);

            // 오프셋을 로컬 위치로 적용
            tilemapObj.transform.localPosition = new Vector3(floorOffsets[f].x, floorOffsets[f].y, 0f);

            Tilemap tilemap = tilemapObj.AddComponent<Tilemap>();
            TilemapRenderer renderer = tilemapObj.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = f;

            floorTilemaps[f] = tilemap;

            // 타일 데이터 렌더링
            RenderFloor(tilemap, ref floor, f);
        }

        LogHelper.Log(LogHelper.GAME, $"MapRandering: 전체 {floorCount}개 Floor 렌더링 완료. 계단 정렬 오프셋 적용됨.");
    }

    // 개별 Floor를 Tilemap에 렌더링
    void RenderFloor(Tilemap tilemap, ref Floor floor, int floorIdx)
    {
        int chunkCountX = floor.config.width;
        int chunkCountY = floor.config.height;

        int totalTiles = chunkCountX * ChunkSize * chunkCountY * ChunkSize;
        var positions  = new Vector3Int[totalTiles];
        var tiles      = new UnityEngine.Tilemaps.TileBase[totalTiles];
        int idx        = 0;

        for (int cx = 0; cx < chunkCountX; cx++)
        {
            for (int cy = 0; cy < chunkCountY; cy++)
            {
                Chunks chunk = floor.chunks[cx, cy];

                if (chunk.chunk == null)
                    continue;

                for (int tx = 0; tx < ChunkSize; tx++)
                {
                    for (int ty = 0; ty < ChunkSize; ty++)
                    {
                        int localX = cx * ChunkSize + tx;
                        int localY = cy * ChunkSize + ty;

                        string tileName = chunk.chunk[tx, ty].name;
                        UnityEngine.Tilemaps.TileBase tileBase;
                        if (tileName == "Wall")
                            tileBase = wallTile;
                        else if (tileName == "Stair")
                            tileBase = stairTile;
                        else
                            tileBase = floorTile;

                        positions[idx] = new Vector3Int(localX, localY, 0);
                        tiles[idx]     = tileBase;
                        idx++;
                    }
                }
            }
        }

        // 실제 배치된 타일 수만 처리
        if (idx < totalTiles)
        {
            var usedPositions = new Vector3Int[idx];
            var usedTiles     = new UnityEngine.Tilemaps.TileBase[idx];
            System.Array.Copy(positions, usedPositions, idx);
            System.Array.Copy(tiles,     usedTiles,     idx);
            tilemap.SetTiles(usedPositions, usedTiles);
        }
        else
        {
            tilemap.SetTiles(positions, tiles);
        }

        LogHelper.Log(LogHelper.GAME, $"MapRandering: F{floorIdx} — {idx}개 타일 렌더링 ({floor.config.width}×{floor.config.height} chunks), 오프셋={floorOffsets[floorIdx]}");
    }

    // ── 계단 정렬 오프셋 계산 ──
    // F0을 기준(0,0)으로, 연결되는 계단의 타일 좌표가 일치하도록 각 Floor의 오프셋을 체인 방식으로 계산
    // F0→F1: F0의 stairTargetFloor==1 계단 위치와 F1의 stairTargetFloor==0 계단 위치가 같은 월드 좌표
    // F1→F2: F1의 보스방 stairTargetFloor==2 위치와 F2의 stairTargetFloor==1(또는 0) 시작방 계단 위치
    // F2→F3: F2의 보스방 stairTargetFloor==3 위치와 F3의 stairTargetFloor==2(또는 0) 시작방 계단 위치
    Vector3Int[] ComputeStairAlignedOffsets()
    {
        int floorCount = createMap.map.floors.Length;
        var offsets = new Vector3Int[floorCount];

        // F0 기준: 오프셋 (0, 0)
        offsets[0] = Vector3Int.zero;

        // F0 → F1 정렬
        if (floorCount > 1)
        {
            Vector2Int stairInF0 = FindStairTileCenter(ref createMap.map.floors[0], 1);
            Vector2Int stairInF1 = FindStairTileCenter(ref createMap.map.floors[1], 0);

            // F1 오프셋 = F0 오프셋 + (F0에서의 계단 위치) - (F1에서의 계단 위치)
            offsets[1] = new Vector3Int(
                offsets[0].x + stairInF0.x - stairInF1.x,
                offsets[0].y + stairInF0.y - stairInF1.y,
                0
            );

            LogHelper.Log(LogHelper.GAME, $"MapRandering: F0→F1 정렬 — F0 계단({stairInF0}), F1 계단({stairInF1}), F1 오프셋={offsets[1]}");
        }

        // F1 → F2 정렬 (F1 보스방 계단 → F2 시작방 귀환 계단)
        if (floorCount > 2)
        {
            Vector2Int stairInF1 = FindStairTileCenter(ref createMap.map.floors[1], 2);
            Vector2Int stairInF2 = FindReturnStairTileCenter(ref createMap.map.floors[2], 1);

            offsets[2] = new Vector3Int(
                offsets[1].x + stairInF1.x - stairInF2.x,
                offsets[1].y + stairInF1.y - stairInF2.y,
                0
            );

            LogHelper.Log(LogHelper.GAME, $"MapRandering: F1→F2 정렬 — F1 계단({stairInF1}), F2 계단({stairInF2}), F2 오프셋={offsets[2]}");
        }

        // F2 → F3 정렬 (F2 보스방 계단 → F3 시작방 귀환 계단)
        if (floorCount > 3)
        {
            Vector2Int stairInF2 = FindStairTileCenter(ref createMap.map.floors[2], 3);
            Vector2Int stairInF3 = FindReturnStairTileCenter(ref createMap.map.floors[3], 2);

            offsets[3] = new Vector3Int(
                offsets[2].x + stairInF2.x - stairInF3.x,
                offsets[2].y + stairInF2.y - stairInF3.y,
                0
            );

            LogHelper.Log(LogHelper.GAME, $"MapRandering: F2→F3 정렬 — F2 계단({stairInF2}), F3 계단({stairInF3}), F3 오프셋={offsets[3]}");
        }

        return offsets;
    }

    // 특정 Floor에서 stairTargetFloor==targetFloor인 계단의 타일 중앙 좌표 반환
    // (청크좌표 × ChunkSize + 타일 중앙 오프셋)
    Vector2Int FindStairTileCenter(ref Floor floor, int targetFloor)
    {
        int w = floor.config.width;
        int h = floor.config.height;

        for (int cx = 0; cx < w; cx++)
        {
            for (int cy = 0; cy < h; cy++)
            {
                if (floor.chunks[cx, cy].stairTargetFloor == targetFloor)
                {
                    // 계단 타일은 청크 중앙 2×2 (tx=3~4, ty=3~4)
                    // 중앙점: tx=3.5, ty=3.5 → 정수로 3 사용
                    return new Vector2Int(cx * ChunkSize + 3, cy * ChunkSize + 3);
                }
            }
        }

        // fallback: (0, 0)
        LogHelper.Warning(LogHelper.GAME, $"MapRandering: stairTargetFloor={targetFloor} 계단을 찾을 수 없습니다.");
        return Vector2Int.zero;
    }

    // 특정 Floor에서 이전 층으로의 귀환 계단 위치 반환
    // 우선순위: stairTargetFloor==fromFloor → stairTargetFloor==0 (F0 귀환) 순
    Vector2Int FindReturnStairTileCenter(ref Floor floor, int fromFloor)
    {
        int w = floor.config.width;
        int h = floor.config.height;

        // 1차: stairTargetFloor == fromFloor 직접 연결 계단
        for (int cx = 0; cx < w; cx++)
            for (int cy = 0; cy < h; cy++)
                if (floor.chunks[cx, cy].stairTargetFloor == fromFloor)
                    return new Vector2Int(cx * ChunkSize + 3, cy * ChunkSize + 3);

        // 2차: stairTargetFloor == 0 (F0 귀환 계단, 시작방에 위치)
        for (int cx = 0; cx < w; cx++)
            for (int cy = 0; cy < h; cy++)
                if (floor.chunks[cx, cy].stairTargetFloor == 0)
                    return new Vector2Int(cx * ChunkSize + 3, cy * ChunkSize + 3);

        LogHelper.Warning(LogHelper.GAME, $"MapRandering: Floor에서 fromFloor={fromFloor} 또는 F0 귀환 계단을 찾을 수 없습니다.");
        return Vector2Int.zero;
    }

    // 기존 자식 Tilemap 오브젝트 모두 제거
    void ClearExistingTilemaps()
    {
        // 기존 배열 참조 해제
        floorTilemaps = null;

        // 자식 중 Tilemap을 가진 오브젝트 수집 후 제거
        var toDestroy = new List<GameObject>();
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.GetComponent<Tilemap>() != null)
                toDestroy.Add(child.gameObject);
        }

        foreach (var obj in toDestroy)
        {
            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }
    }

    // ── 단일 Floor 렌더링 (하위 호환 / Inspector 연동용) ──
    // currentFloorIndex에 해당하는 Floor만 표시하고 나머지는 숨김
    public void ShowFloor(int floorIndex)
    {
        if (floorTilemaps == null) return;

        for (int f = 0; f < floorTilemaps.Length; f++)
        {
            if (floorTilemaps[f] != null)
                floorTilemaps[f].gameObject.SetActive(f == floorIndex);
        }
    }

    // 모든 Floor 표시
    public void ShowAllFloors()
    {
        if (floorTilemaps == null) return;

        for (int f = 0; f < floorTilemaps.Length; f++)
        {
            if (floorTilemaps[f] != null)
                floorTilemaps[f].gameObject.SetActive(true);
        }
    }
}
