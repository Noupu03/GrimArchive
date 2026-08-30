// ============================================================================
// CreateMap.Validation.cs — 맵 무결성 검증
// ----------------------------------------------------------------------------
// ValidateMap → ValidateFloor0 + ValidateFloor(F1~F3) + ValidateStairs. 시작방/보스방/방 개수,
// 고아 청크, 연결성(FloodFill), Gate 정합성, Footprint 범위, 계단 양방향 정합성 등 13개 항목 검증.
// GenerateMap 최종 단계 — 맵 완성 후 품질 보증.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public partial class CreateMap
{
    public List<string> ValidateMap()
    {
        var errors = new List<string>();

        if (map.floors == null || map.floors.Length == 0)
        {
            errors.Add("맵 데이터가 초기화되지 않았습니다.");
            return errors;
        }

        errors.AddRange(ValidateFloor0(ref map.floors[0]));

        for (int f = 1; f < map.floors.Length; f++)
        {
            errors.AddRange(ValidateFloor(ref map.floors[f]));
        }

        errors.AddRange(ValidateStairs());

        return errors;
    }

    List<string> ValidateFloor(ref Floor floor)
    {
        var errors = new List<string>();
        int fId = (int)floor.config.floorId;
        int w = floor.config.width;
        int h = floor.config.height;

        // ① 시작방 존재 확인
        int startCount = 0;
        var startIds = new HashSet<int>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (floor.chunks[x, y].roomRole == RoomRole.StartRoom)
                { startCount++; startIds.Add(floor.chunks[x, y].roomId); }

        if (startIds.Count == 0)
            errors.Add($"F{fId}: 시작방이 존재하지 않습니다.");
        else if (startIds.Count > 1)
            errors.Add($"F{fId}: 시작방이 {startIds.Count}개 존재합니다. (1개여야 함)");

        // ② 보스방 존재 확인
        var bossIds = new HashSet<int>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (floor.chunks[x, y].roomRole == RoomRole.BossRoom)
                    bossIds.Add(floor.chunks[x, y].roomId);

        if (bossIds.Count == 0)
            errors.Add($"F{fId}: 보스방이 존재하지 않습니다.");
        else if (bossIds.Count > 1)
            errors.Add($"F{fId}: 보스방이 {bossIds.Count}개 존재합니다. (1개여야 함)");

        // ③ 서브목적방 개수 확인
        var subIds = new HashSet<int>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (floor.chunks[x, y].roomRole == RoomRole.SubPurposeRoom)
                    subIds.Add(floor.chunks[x, y].roomId);

        if (subIds.Count != floor.config.subPurposeRoomCount)
            errors.Add($"F{fId}: 서브목적방 수({subIds.Count})가 설정값({floor.config.subPurposeRoomCount})과 다릅니다.");

        // ④ 일반방 개수 확인
        var normalIds = new HashSet<int>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (floor.chunks[x, y].roomRole == RoomRole.NormalRoom)
                    normalIds.Add(floor.chunks[x, y].roomId);

        int normalMinRequired = Mathf.Max(1, floor.config.normalRoomCount - floor.config.subPurposeRoomCount);
        if (normalIds.Count < normalMinRequired)
            errors.Add($"F{fId}: 일반방 수({normalIds.Count})가 최소 기준({normalMinRequired})보다 부족합니다.");

        // ④-b 일반방 청크 수 상한 확인
        int maxChunks = floor.config.maxNormalRoomChunks;
        if (maxChunks > 0)
        {
            var normalRoomChunkCount = new Dictionary<int, int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    if (c.roomId >= 0 && c.roomRole == RoomRole.NormalRoom)
                    {
                        if (!normalRoomChunkCount.ContainsKey(c.roomId))
                            normalRoomChunkCount[c.roomId] = 0;
                        normalRoomChunkCount[c.roomId]++;
                    }
                }

            foreach (var kvp in normalRoomChunkCount)
            {
                if (kvp.Value > maxChunks)
                    errors.Add($"F{fId}: 일반방 roomId={kvp.Key}의 청크 수({kvp.Value})가 상한({maxChunks})을 초과합니다.");
            }
        }

        // ④-c 서브목적방 1×1 강제 검증
        if (subIds.Count > 0)
        {
            var subChunkCount = new Dictionary<int, int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    if (c.roomId >= 0 && c.roomRole == RoomRole.SubPurposeRoom)
                    {
                        if (!subChunkCount.ContainsKey(c.roomId))
                            subChunkCount[c.roomId] = 0;
                        subChunkCount[c.roomId]++;
                    }
                }

            foreach (var kvp in subChunkCount)
            {
                if (kvp.Value > 1)
                    errors.Add($"F{fId}: 서브목적방 roomId={kvp.Key}의 청크 수({kvp.Value})가 1을 초과합니다. (1×1이어야 함)");
            }
        }

        // ④-d 총 방 수 합산 검증 (서브 목적방은 별도 카운트이므로 제외)
        if (floor.config.totalRoomCount > 0)
        {
            int actualTotal = startIds.Count + normalIds.Count + bossIds.Count;
            int minRequired = floor.config.totalRoomCount;
            if (actualTotal < minRequired)
                errors.Add($"F{fId}: 총 방 수({actualTotal})가 설정값({minRequired})보다 부족합니다. " +
                           $"(시작={startIds.Count}, 일반={normalIds.Count}, 보스={bossIds.Count}) [서브목적방({subIds.Count})은 별도]");
        }

        // ⑤ 고아 청크 검사
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.roomRole == RoomRole.None && c.roomId >= 0)
                    errors.Add($"F{fId} chunk[{x},{y}]: 고아 청크 (roomId={c.roomId}, role=None)");
                if (c.roomRole != RoomRole.None && c.roomId < 0)
                    errors.Add($"F{fId} chunk[{x},{y}]: 역할 불일치 (roomId=-1, role={c.roomRole})");
            }

        // ⑥ 연결성: 타일 기반 flood fill
        var validRoomIds = new HashSet<int>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (floor.chunks[x, y].roomId >= 0)
                    validRoomIds.Add(floor.chunks[x, y].roomId);

        if (validRoomIds.Count > 1)
        {
            int startWx = -1, startWy = -1;
            for (int x = 0; x < w && startWx < 0; x++)
                for (int y = 0; y < h && startWx < 0; y++)
                    if (floor.chunks[x, y].roomRole == RoomRole.StartRoom)
                    {
                        var startTile = FindFirstFloorTile(floor.chunks[x, y], x, y, floor.config.chunkSize);
                        startWx = startTile.wx;
                        startWy = startTile.wy;
                    }

            if (startWx >= 0)
            {
                var reachable = FloodFillReachableRooms(ref floor, startWx, startWy);
                foreach (int id in validRoomIds)
                {
                    if (!reachable.Contains(id))
                        errors.Add($"F{fId}: roomId {id}에 시작방에서 도달할 수 없습니다.");
                }
            }
        }

        // ⑦ Gate 정합성
        if (floor.gates != null)
        {
            for (int i = 0; i < floor.gates.Count; i++)
            {
                Gate g = floor.gates[i];
                if (!validRoomIds.Contains(g.roomA))
                    errors.Add($"F{fId} Gate[{i}]: roomA={g.roomA}가 존재하지 않습니다.");
                if (!validRoomIds.Contains(g.roomB))
                    errors.Add($"F{fId} Gate[{i}]: roomB={g.roomB}가 존재하지 않습니다.");
                if (g.chunkAX < 0 || g.chunkAX >= w || g.chunkAY < 0 || g.chunkAY >= h)
                    errors.Add($"F{fId} Gate[{i}]: chunkA[{g.chunkAX},{g.chunkAY}]가 범위를 벗어났습니다.");
                if (g.chunkBX < 0 || g.chunkBX >= w || g.chunkBY < 0 || g.chunkBY >= h)
                    errors.Add($"F{fId} Gate[{i}]: chunkB[{g.chunkBX},{g.chunkBY}]가 범위를 벗어났습니다.");
                if (g.width <= 0)
                    errors.Add($"F{fId} Gate[{i}]: width={g.width}가 유효하지 않습니다.");
            }
        }

        // ⑧ allowMaxFootprint 범위 검증
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.roomId >= 0)
                {
                    if (c.allowMaxFootprint < 1 || c.allowMaxFootprint > 5)
                        errors.Add($"F{fId} chunk[{x},{y}]: allowMaxFootprint={c.allowMaxFootprint}가 범위(1~5)를 벗어났습니다.");
                }
            }

        // ⑨ SubPurposeRoom 단일 입구 검증
        if (floor.gates != null && subIds.Count > 0)
        {
            var subGateCount = new Dictionary<int, int>();
            foreach (int sid in subIds)
                subGateCount[sid] = 0;

            foreach (Gate g in floor.gates)
            {
                if (subGateCount.ContainsKey(g.roomA))
                    subGateCount[g.roomA]++;
                if (subGateCount.ContainsKey(g.roomB))
                    subGateCount[g.roomB]++;
            }

            foreach (var kvp in subGateCount)
            {
                if (kvp.Value > 1)
                    errors.Add($"F{fId}: 서브목적방 roomId={kvp.Key}의 Gate 수({kvp.Value})가 1개를 초과합니다.");
            }
        }

        // ⑩ Gate 폭 vs allowMaxFootprint 일관성 검증 — 통로가 항상 2*2로 고정돼 방 footprint를 더 이상
        // 따라가지 않으므로 비활성화(살리면 매번 실패해 재시도만 낭비). 롤백 가능성 있어 주석 처리만.
        // if (floor.gates != null)
        // {
        //     foreach (Gate g in floor.gates)
        //     {
        //         int maxFpA = 1, maxFpB = 1;
        //         for (int x = 0; x < w; x++)
        //             for (int y = 0; y < h; y++)
        //             {
        //                 int id = floor.chunks[x, y].roomId;
        //                 if (id == g.roomA) maxFpA = Mathf.Max(maxFpA, floor.chunks[x, y].allowMaxFootprint);
        //                 if (id == g.roomB) maxFpB = Mathf.Max(maxFpB, floor.chunks[x, y].allowMaxFootprint);
        //             }
        //
        //         int expected = Mathf.Max(maxFpA, maxFpB);
        //         if (g.width < expected)
        //             errors.Add($"F{fId} Gate(room{g.roomA}↔room{g.roomB}): width={g.width}가 allowMaxFootprint 기대값({expected})보다 작습니다.");
        //     }
        // }

        // ⑪ 시작방 위치 검증
        {
            int expectedSX = 0;
            int expectedSY = h / 2;
            bool startPosCorrect = false;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (floor.chunks[x, y].roomRole == RoomRole.StartRoom && x == expectedSX && y == expectedSY)
                        startPosCorrect = true;

            if (startIds.Count > 0 && !startPosCorrect)
                errors.Add($"F{fId}: 시작방 위치가 [{expectedSX},{expectedSY}]이 아닙니다.");
        }

        // ⑫ 보스방 청크 수 검증
        if (bossIds.Count == 1)
        {
            string fmt = floor.config.bossRoomFormat;
            int expectedBossChunks = ParseExpectedBossChunkCount(fmt);
            if (expectedBossChunks > 0)
            {
                int bossRoomId = -1;
                foreach (int bid in bossIds) { bossRoomId = bid; break; }

                int actualBossChunks = 0;
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                        if (floor.chunks[x, y].roomId == bossRoomId)
                            actualBossChunks++;

                if (actualBossChunks != expectedBossChunks)
                    errors.Add($"F{fId}: 보스방 청크 수({actualBossChunks})가 bossRoomFormat '{fmt}' 기대값({expectedBossChunks})과 다릅니다.");
            }
        }

        // ⑬ 시작방 직접 연결 방은 NormalRoom만 허용
        if (floor.gates != null && startIds.Count > 0)
        {
            int startRmId = -1;
            foreach (int sid in startIds) { startRmId = sid; break; }

            var startNeighborRoles = new Dictionary<int, RoomRole>();
            foreach (Gate g in floor.gates)
            {
                int neighborId = -1;
                if (g.roomA == startRmId) neighborId = g.roomB;
                else if (g.roomB == startRmId) neighborId = g.roomA;
                if (neighborId < 0) continue;

                if (!startNeighborRoles.ContainsKey(neighborId))
                {
                    for (int x = 0; x < w; x++)
                        for (int y = 0; y < h; y++)
                            if (floor.chunks[x, y].roomId == neighborId && !startNeighborRoles.ContainsKey(neighborId))
                                startNeighborRoles[neighborId] = floor.chunks[x, y].roomRole;
                }
            }

            foreach (var kvp in startNeighborRoles)
            {
                if (kvp.Value != RoomRole.NormalRoom)
                    errors.Add($"F{fId}: 시작방에 직접 연결된 roomId={kvp.Key}의 역할이 {kvp.Value}입니다. (NormalRoom이어야 함)");
            }
        }

        return errors;
    }

    int ParseExpectedBossChunkCount(string fmt)
    {
        if (string.IsNullOrEmpty(fmt)) return 0;

        if (fmt.Contains("x"))
        {
            var parts = fmt.Split('x');
            if (parts.Length == 2 && int.TryParse(parts[0], out int bw) && int.TryParse(parts[1], out int bh))
                return bw * bh;
        }

        if (fmt.StartsWith("ㄷ") && int.TryParse(fmt.Substring(1), out int chunkCount))
            return chunkCount;

        return 0;
    }

    List<string> ValidateFloor0(ref Floor floor)
    {
        var errors = new List<string>();
        int w = floor.config.width;
        int h = floor.config.height;

        var roomIds = new HashSet<int>();
        bool allStart = true;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.roomId >= 0) roomIds.Add(c.roomId);
                if (c.roomRole != RoomRole.StartRoom) allStart = false;
            }

        if (roomIds.Count != 1)
            errors.Add($"F0: 로비가 단일 방이 아닙니다. (roomId 수: {roomIds.Count})");
        if (!allStart)
            errors.Add("F0: 모든 청크가 StartRoom이 아닙니다.");

        return errors;
    }

    List<string> ValidateStairs()
    {
        var errors = new List<string>();

        ref Floor f0 = ref map.floors[0];
        int w0 = f0.config.width;
        int h0 = f0.config.height;
        bool hasF0ToF1 = false;
        for (int x = 0; x < w0; x++)
            for (int y = 0; y < h0; y++)
                if (f0.chunks[x, y].stairTargetFloor == 1)
                    hasF0ToF1 = true;

        if (!hasF0ToF1)
            errors.Add("F0 → F1 계단이 존재하지 않습니다.");

        for (int x = 0; x < w0; x++)
            for (int y = 0; y < h0; y++)
            {
                Chunks c = f0.chunks[x, y];
                if (c.stairTargetFloor == 1 && !c.stairHumanOnly)
                    errors.Add($"F0 chunk[{x},{y}] → F1: stairHumanOnly=false (인류 전용이어야 함)");
            }

        for (int x = 0; x < w0; x++)
            for (int y = 0; y < h0; y++)
            {
                Chunks c = f0.chunks[x, y];
                if (c.stairTargetFloor >= 2)
                    errors.Add($"F0 chunk[{x},{y}] → F{c.stairTargetFloor}: F0에서 F2 이상으로의 직접 계단은 허용되지 않습니다.");
            }

        for (int f = 1; f < map.floors.Length; f++)
        {
            ref Floor floor = ref map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;
            bool hasReturn = false;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (floor.chunks[x, y].stairTargetFloor == 0)
                        hasReturn = true;

            if (!hasReturn)
                errors.Add($"F{f} → F0 귀환 계단이 존재하지 않습니다.");
        }

        for (int f = 1; f < map.floors.Length - 1; f++)
        {
            ref Floor floor = ref map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;
            int targetFloor = f + 1;
            bool hasBossStair = false;

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (floor.chunks[x, y].roomRole == RoomRole.BossRoom && floor.chunks[x, y].stairTargetFloor == targetFloor)
                        hasBossStair = true;

            if (!hasBossStair)
                errors.Add($"F{f} 보스방 → F{targetFloor} 계단이 존재하지 않습니다.");
        }

        for (int x = 0; x < w0; x++)
        {
            for (int y = 0; y < h0; y++)
            {
                Chunks c = f0.chunks[x, y];
                if (c.stairTargetFloor >= 0)
                {
                    bool expectedOpen = (c.stairTargetFloor == 1);
                    if (c.stairIsOpen != expectedOpen)
                        errors.Add($"F0 chunk[{x},{y}] → F{c.stairTargetFloor}: stairIsOpen={c.stairIsOpen} (기대값: {expectedOpen})");
                }
            }
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
                    Chunks c = floor.chunks[x, y];
                    if (c.stairTargetFloor == 0)
                    {
                        bool expectedOpen = (f == 1);
                        if (c.stairIsOpen != expectedOpen)
                            errors.Add($"F{f} chunk[{x},{y}] → F0: stairIsOpen={c.stairIsOpen} (기대값: {expectedOpen})");
                    }
                }
            }
        }

        return errors;
    }

    HashSet<int> FloodFillReachableRooms(ref Floor floor, int startWx, int startWy)
    {
        int w = floor.config.width;
        int h = floor.config.height;
        int cs = floor.config.chunkSize;
        int totalW = w * cs;
        int totalH = h * cs;

        bool[,] visited = new bool[totalW, totalH];
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((startWx, startWy));
        visited[startWx, startWy] = true;

        int[] ddx = { 1, -1, 0, 0 };
        int[] ddy = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                int nx = cx + ddx[d];
                int ny = cy + ddy[d];
                if (nx < 0 || nx >= totalW || ny < 0 || ny >= totalH) continue;
                if (visited[nx, ny]) continue;

                int chunkX = nx / cs, chunkY = ny / cs;
                int tileX = nx % cs, tileY = ny % cs;
                Tile t = floor.chunks[chunkX, chunkY].chunk[tileX, tileY];
                if (t.name != "Wall")
                {
                    visited[nx, ny] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }

        var reachable = new HashSet<int>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (floor.chunks[x, y].roomId < 0) continue;
                if (IsChunkReachable(floor.chunks[x, y], x, y, visited, cs))
                    reachable.Add(floor.chunks[x, y].roomId);
            }

        return reachable;
    }

    bool IsChunkReachable(Chunks chunk, int cx, int cy, bool[,] visited, int chunkSize)
    {
        if (chunk.chunk == null) return false;
        for (int tx = 0; tx < chunkSize; tx++)
            for (int ty = 0; ty < chunkSize; ty++)
            {
                if (chunk.chunk[tx, ty].name != "Wall" && visited[cx * chunkSize + tx, cy * chunkSize + ty])
                    return true;
            }
        return false;
    }

    (int wx, int wy) FindFirstFloorTile(Chunks chunk, int cx, int cy, int chunkSize)
    {
        if (chunk.chunk != null)
        {
            for (int tx = 0; tx < chunkSize; tx++)
                for (int ty = 0; ty < chunkSize; ty++)
                    if (chunk.chunk[tx, ty].name != "Wall")
                        return (cx * chunkSize + tx, cy * chunkSize + ty);
        }
        return (cx * chunkSize + chunkSize / 2, cy * chunkSize + chunkSize / 2);
    }
}
