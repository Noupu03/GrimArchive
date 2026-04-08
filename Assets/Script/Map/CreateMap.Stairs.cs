// ============================================================================
// CreateMap.Stairs.cs — 0층 생성 · 계단 · Footprint · 메타데이터
// ----------------------------------------------------------------------------
// 역할: 0층(로비) 생성(GenerateFloor0), 계단 배치(PlaceStairs),
//       보스방 계단 배치(PlaceBossRoomStair),
//       계단 후 Gate 폭 갱신(UpdateGateWidthsAfterStairs),
//       Footprint 할당(AssignFootprint — Phase 2에서 호출),
//       점령/위험도/시야/이해도/지형 초기화(InitOccupationAndDanger,
//       InitWeightVisibilityLandform — Phase 3에서 호출).
// 단계: Phase 2(AssignFootprint) + Phase 3(메타데이터) + Post(0층/계단)
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public partial class CreateMap
{
    // ── ⑦ Floor 0 (입구/로비) 생성 ──
    void GenerateFloor0()
    {
        ref Floor floor = ref map.floors[0];
        int w = floor.config.width;
        int h = floor.config.height;

        int lobbyId = RoomIdGenerator.GetNextId();
        string lobbyName = RoomIdGenerator.FormatName(lobbyId);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                c.roomId = lobbyId;
                c.roomName = lobbyName;
                c.roomRole = RoomRole.StartRoom;
                floor.chunks[x, y] = c;
            }
        }

        AssignTileNames(ref floor);
        OpenInternalWalls(ref floor);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.chunk == null) continue;

                for (int tx = 0; tx < 8; tx++)
                {
                    for (int ty = 0; ty < 8; ty++)
                    {
                        Tile t = c.chunk[tx, ty];
                        t.visibility = 100;
                        t.understand = 100;
                        c.chunk[tx, ty] = t;
                    }
                }

                floor.chunks[x, y] = c;
            }
        }

        Debug.Log($"CreateMap: Floor 0 lobby generated as {lobbyName} ({w}×{h}).");
    }

    // ── ⑧ 층간 계단 배치 ──
    void PlaceStairs()
    {
        ref Floor f0 = ref map.floors[0];
        int w0 = f0.config.width;
        int h0 = f0.config.height;

        int stairX = 0;
        int stairY = h0 / 2;
        if (stairX < w0 && stairY < h0)
        {
            PlaceStairTiles(ref f0, stairX, stairY, 1);
            Chunks c = f0.chunks[stairX, stairY];
            c.stairIsOpen = true;
            c.stairHumanOnly = true;
            c.allowMaxFootprint = 5;
            f0.chunks[stairX, stairY] = c;
        }

        for (int f = 1; f < map.floors.Length; f++)
        {
            ref Floor floor = ref map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (floor.chunks[x, y].roomRole == RoomRole.StartRoom)
                    {
                        PlaceStairTiles(ref floor, x, y, 0);
                        Chunks c = floor.chunks[x, y];
                        c.stairIsOpen = (f == 1);
                        c.allowMaxFootprint = 5;
                        floor.chunks[x, y] = c;
                        goto nextFloorReturn;
                    }
                }
            }
            nextFloorReturn:;
        }

        for (int f = 1; f < map.floors.Length - 1; f++)
        {
            ref Floor floor = ref map.floors[f];
            int targetFloor = f + 1;
            PlaceBossRoomStair(ref floor, targetFloor);
        }

        Debug.Log("CreateMap: Stairs placed on all floors (sequential structure).");
    }

    // 계단 배치 후 allowMaxFootprint가 변경된 방의 Gate 폭을 확장
    // Close 없이 더 넓은 폭으로 Open만 수행 (기존 열린 타일은 유지, 추가분만 확장)
    void UpdateGateWidthsAfterStairs()
    {
        for (int f = 1; f < map.floors.Length; f++)
        {
            ref Floor floor = ref map.floors[f];
            if (floor.gates == null || floor.gates.Count == 0) continue;

            // Floor별 저장된 wallThicknessCache 복원
            if (perFloorWallThicknessCache.ContainsKey(f))
                wallThicknessCache = new Dictionary<int, (int, int)>(perFloorWallThicknessCache[f]);
            else
                wallThicknessCache.Clear();

            int w = floor.config.width;
            int h = floor.config.height;

            var roomMaxFp = new Dictionary<int, int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    int rid = floor.chunks[x, y].roomId;
                    if (rid < 0) continue;
                    int fp = floor.chunks[x, y].allowMaxFootprint;
                    if (!roomMaxFp.ContainsKey(rid) || fp > roomMaxFp[rid])
                        roomMaxFp[rid] = fp;
                }

            for (int i = 0; i < floor.gates.Count; i++)
            {
                Gate g = floor.gates[i];
                int maxFpA = roomMaxFp.ContainsKey(g.roomA) ? roomMaxFp[g.roomA] : 1;
                int maxFpB = roomMaxFp.ContainsKey(g.roomB) ? roomMaxFp[g.roomB] : 1;
                int requiredWidth = Mathf.Max(maxFpA, maxFpB);

                if (g.width < requiredWidth)
                {
                    int newWidth = Mathf.Clamp(requiredWidth, 2, 6);

                    // ClosePassage 없이 더 넓은 폭으로 Open만 수행
                    // OpenPassage는 해당 범위를 Floor 타일로 덮으므로
                    // 기존 열린 타일은 그대로, 추가분만 확장됨
                    int actualWidth;
                    if (g.isHorizontal)
                    {
                        int leftX = Mathf.Min(g.chunkAX, g.chunkBX);
                        int leftY = g.chunkAY;
                        actualWidth = OpenHorizontalPassage(ref floor, leftX, leftY, newWidth);
                    }
                    else
                    {
                        int bottomX = g.chunkAX;
                        int bottomY = Mathf.Min(g.chunkAY, g.chunkBY);
                        actualWidth = OpenVerticalPassage(ref floor, bottomX, bottomY, newWidth);
                    }

                    g.width = actualWidth;
                    floor.gates[i] = g;
                }
            }
        }
    }

    void PlaceBossRoomStair(ref Floor floor, int targetFloor)
    {
        int w = floor.config.width;
        int h = floor.config.height;

        var bossChunks = new List<(int x, int y)>();
        int bossRoomId = -1;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (floor.chunks[x, y].roomRole == RoomRole.BossRoom)
                {
                    bossChunks.Add((x, y));
                    bossRoomId = floor.chunks[x, y].roomId;
                }

        if (bossChunks.Count == 0)
        {
            Debug.LogWarning($"CreateMap: Floor {(int)floor.config.floorId} — 보스방을 찾을 수 없어 다음 층 계단 배치 실패.");
            return;
        }

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var (bx, by) in bossChunks)
        {
            if (bx < minX) minX = bx;
            if (by < minY) minY = by;
            if (bx > maxX) maxX = bx;
            if (by > maxY) maxY = by;
        }

        int entranceDirX = 0, entranceDirY = 0;
        bool foundEntrance = false;
        if (floor.gates != null)
        {
            foreach (Gate g in floor.gates)
            {
                if (g.roomA == bossRoomId || g.roomB == bossRoomId)
                {
                    int gateChunkX, gateChunkY;
                    if (g.roomA == bossRoomId)
                    { gateChunkX = g.chunkAX; gateChunkY = g.chunkAY; }
                    else
                    { gateChunkX = g.chunkBX; gateChunkY = g.chunkBY; }

                    float centerX = (minX + maxX) / 2f;
                    float centerY = (minY + maxY) / 2f;
                    entranceDirX = (gateChunkX > centerX) ? 1 : (gateChunkX < centerX) ? -1 : 0;
                    entranceDirY = (gateChunkY > centerY) ? 1 : (gateChunkY < centerY) ? -1 : 0;
                    foundEntrance = true;
                    break;
                }
            }
        }

        int bestX = bossChunks[0].x, bestY = bossChunks[0].y;
        if (foundEntrance)
        {
            float bestScore = float.MinValue;
            foreach (var (bx, by) in bossChunks)
            {
                float score = bx * (-entranceDirX) + by * (-entranceDirY);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestX = bx;
                    bestY = by;
                }
            }
        }
        else
        {
            int centerX = (minX + maxX) / 2;
            int centerY = (minY + maxY) / 2;
            float bestDist = float.MaxValue;
            foreach (var (bx, by) in bossChunks)
            {
                float d = Mathf.Abs(bx - centerX) + Mathf.Abs(by - centerY);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestX = bx;
                    bestY = by;
                }
            }
        }

        PlaceStairTiles(ref floor, bestX, bestY, targetFloor);
        Chunks c = floor.chunks[bestX, bestY];
        c.stairIsOpen = false;
        floor.chunks[bestX, bestY] = c;

        Debug.Log($"CreateMap: Floor {(int)floor.config.floorId} boss room stair → F{targetFloor} at [{bestX},{bestY}] (entrance opposite).");
    }

    void PlaceStairTiles(ref Floor floor, int cx, int cy, int targetFloor)
    {
        Chunks c = floor.chunks[cx, cy];
        if (c.chunk == null) return;

        c.stairTargetFloor = targetFloor;

        for (int tx = 3; tx <= 4; tx++)
        {
            for (int ty = 3; ty <= 4; ty++)
            {
                c.chunk[tx, ty] = TileFactory.Stair();
            }
        }

        floor.chunks[cx, cy] = c;
    }

    // ── ⑨ 점령 상태 + 위험도/이해도 초기화 ──
    void InitOccupationAndDanger(ref Floor floor)
    {
        int w = floor.config.width;
        int h = floor.config.height;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                switch (c.roomRole)
                {
                    case RoomRole.StartRoom:      c.occupationState = OccupationState.PlayerControlled; break;
                    case RoomRole.BossRoom:       c.occupationState = OccupationState.Neutral; break;
                    case RoomRole.SubPurposeRoom: c.occupationState = OccupationState.Neutral; break;
                    case RoomRole.NormalRoom:     c.occupationState = OccupationState.Neutral; break;
                    default:                      c.occupationState = OccupationState.Neutral; break;
                }
                floor.chunks[x, y] = c;
            }
        }

        int startId = -1;
        for (int x = 0; x < w && startId < 0; x++)
            for (int y = 0; y < h && startId < 0; y++)
                if (floor.chunks[x, y].roomRole == RoomRole.StartRoom)
                    startId = floor.chunks[x, y].roomId;

        var dangerAdj = new Dictionary<int, HashSet<int>>();
        if (floor.gates != null)
        {
            foreach (Gate g in floor.gates)
            {
                if (!dangerAdj.ContainsKey(g.roomA)) dangerAdj[g.roomA] = new HashSet<int>();
                if (!dangerAdj.ContainsKey(g.roomB)) dangerAdj[g.roomB] = new HashSet<int>();
                dangerAdj[g.roomA].Add(g.roomB);
                dangerAdj[g.roomB].Add(g.roomA);
            }
        }

        var distFromStart = new Dictionary<int, int>();
        if (startId >= 0)
        {
            var bfsQueue = new Queue<int>();
            distFromStart[startId] = 0;
            bfsQueue.Enqueue(startId);
            while (bfsQueue.Count > 0)
            {
                int current = bfsQueue.Dequeue();
                if (!dangerAdj.ContainsKey(current)) continue;
                foreach (int neighbor in dangerAdj[current])
                {
                    if (!distFromStart.ContainsKey(neighbor))
                    {
                        distFromStart[neighbor] = distFromStart[current] + 1;
                        bfsQueue.Enqueue(neighbor);
                    }
                }
            }
        }

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.chunk == null) continue;

                int roomDist = 0;
                if (c.roomId >= 0 && distFromStart.ContainsKey(c.roomId))
                    roomDist = distFromStart[c.roomId];

                int dangerValue = roomDist;
                int understandValue = (c.roomRole == RoomRole.StartRoom) ? 100 : 0;

                for (int tx = 0; tx < 8; tx++)
                {
                    for (int ty = 0; ty < 8; ty++)
                    {
                        Tile t = c.chunk[tx, ty];
                        t.dangerous = dangerValue;
                        t.understand = understandValue;
                        c.chunk[tx, ty] = t;
                    }
                }

                floor.chunks[x, y] = c;
            }
        }

        Debug.Log($"CreateMap: Floor {(int)floor.config.floorId} occupation/danger initialized.");
    }

    // ── ⑩ 가중치 + 가시성 + 지형 초기화 ──
    void InitWeightVisibilityLandform(ref Floor floor)
    {
        int w = floor.config.width;
        int h = floor.config.height;

        var landformCache = new Dictionary<int, int>();

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.chunk == null) continue;

                if (c.roomId >= 0 && landformCache.TryGetValue(c.roomId, out int cachedLandform))
                {
                    c.landform = cachedLandform;
                }
                else
                {
                    switch (c.roomRole)
                    {
                        case RoomRole.StartRoom:      c.landform = 0; break;
                        case RoomRole.NormalRoom:      c.landform = UnityEngine.Random.Range(0, 3); break;
                        case RoomRole.SubPurposeRoom: c.landform = 3; break;
                        case RoomRole.BossRoom:       c.landform = 4; break;
                        default:                      c.landform = 0; break;
                    }
                    if (c.roomId >= 0)
                        landformCache[c.roomId] = c.landform;
                }

                bool isBoss = (c.roomRole == RoomRole.BossRoom);

                for (int tx = 0; tx < 8; tx++)
                {
                    for (int ty = 0; ty < 8; ty++)
                    {
                        Tile t = c.chunk[tx, ty];

                        if (isBoss && t.name == "Floor")
                            t.weight = 2;

                        if (isBoss && t.name != "Wall")
                            t.visibility = 50;

                        c.chunk[tx, ty] = t;
                    }
                }

                floor.chunks[x, y] = c;
            }
        }

        Debug.Log($"CreateMap: Floor {(int)floor.config.floorId} weight/visibility/landform initialized.");
    }

    // ── ⑪-b allowMaxFootprint 할당 ──
    void AssignFootprint(ref Floor floor)
    {
        int w = floor.config.width;
        int h = floor.config.height;
        int fId = (int)floor.config.floorId;

        var footprintCache = new Dictionary<int, int>();

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.roomId < 0) continue;

                if (!footprintCache.TryGetValue(c.roomId, out int fp))
                {
                    switch (c.roomRole)
                    {
                        case RoomRole.StartRoom:      fp = 1; break;
                        case RoomRole.SubPurposeRoom: fp = 1; break;
                        case RoomRole.NormalRoom:     fp = GetNormalFootprint(fId); break;
                        case RoomRole.BossRoom:       fp = GetBossFootprint(fId); break;
                        default:                      fp = 1; break;
                    }
                    footprintCache[c.roomId] = fp;
                }

                c.allowMaxFootprint = fp;
                floor.chunks[x, y] = c;
            }
        }

        Debug.Log($"CreateMap: Floor {fId} allowMaxFootprint assigned.");
    }

    int GetNormalFootprint(int floorId)
    {
        switch (floorId)
        {
            case 1: return 2;
            case 2: return UnityEngine.Random.Range(2, 4);
            case 3: return UnityEngine.Random.Range(1, 5);
            default: return 1;
        }
    }

    int GetBossFootprint(int floorId)
    {
        switch (floorId)
        {
            case 1: return 3;
            case 2: return 4;
            case 3: return 5;
            default: return 1;
        }
    }
}
