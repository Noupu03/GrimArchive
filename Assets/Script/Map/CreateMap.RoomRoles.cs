// ============================================================================
// CreateMap.RoomRoles.cs — 배치된 방에 역할 부여, 초과 방 가지치기, 서브목적방 확보를 담당한다
// (GenerateMap Phase 1.5, 방 배치 후·연결 전).
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

public partial class CreateMap
{
    // ③.9a RoomRole 할당: BossRoom → SubPurposeRoom → NormalRoom
    void AssignRoomRoles(ref Floor floor)
    {
        int w = floor.config.width;
        int h = floor.config.height;

        // 고유 roomId 수집 (StartRoom, BossRoom, roomId==-1 제외)
        var roomIds = new HashSet<int>();
        int startRoomId = -1;
        bool hasBossAlready = false;

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.roomId == -1) continue;
                if (c.roomRole == RoomRole.StartRoom)
                {
                    startRoomId = c.roomId;
                    continue;
                }
                if (c.roomRole == RoomRole.BossRoom)
                {
                    hasBossAlready = true;
                    continue;
                }
                roomIds.Add(c.roomId);
            }

        if (roomIds.Count == 0)
        {
            LogHelper.Warning(LogHelper.GAME, $"CreateMap: Floor {(int)floor.config.floorId} — 할당할 방이 없습니다.");
            return;
        }

        // BFS: 시작방으로부터 각 방까지 그래프 거리 계산
        var distFromStart = BfsDistances(startRoomId);

        // 거리 순으로 정렬 (먼 방 우선)
        var sortedByDist = new List<int>(roomIds);
        sortedByDist.Sort((a, b) =>
        {
            int da = distFromStart.ContainsKey(a) ? distFromStart[a] : int.MaxValue;
            int db = distFromStart.ContainsKey(b) ? distFromStart[b] : int.MaxValue;
            return db.CompareTo(da); // 내림차순
        });

        // ① 보스방: PlaceBossRoom에서 이미 배치되지 않은 경우만 여기서 할당
        int bossRoomId = -1;
        if (!hasBossAlready && sortedByDist.Count > 0)
        {
            bossRoomId = sortedByDist[0];
            SetRoomRole(ref floor, bossRoomId, RoomRole.BossRoom);
            sortedByDist.RemoveAt(0);
        }

        // ② 서브목적방: 설정 개수만큼(먼 방 우선), 반드시 1청크 방만 허용.
        var roomChunkCounts = new Dictionary<int, int>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                int rid = floor.chunks[x, y].roomId;
                if (rid >= 0)
                {
                    if (!roomChunkCounts.ContainsKey(rid))
                        roomChunkCounts[rid] = 0;
                    roomChunkCounts[rid]++;
                }
            }

        int subCount = 0;
        int subTarget = floor.config.subPurposeRoomCount;
        var subAssigned = new List<int>();
        for (int i = 0; i < sortedByDist.Count && subCount < subTarget; i++)
        {
            int candidate = sortedByDist[i];
            // 1청크 방만 서브목적방으로 할당
            if (roomChunkCounts.ContainsKey(candidate) && roomChunkCounts[candidate] == 1)
            {
                SetRoomRole(ref floor, candidate, RoomRole.SubPurposeRoom);
                subAssigned.Add(candidate);
                subCount++;
            }
        }
        foreach (int sid in subAssigned)
            sortedByDist.Remove(sid);

        // ③ 일반방: normalRoomCount 이하만 할당, 나머지는 None 유지
        int normalLimit = floor.config.normalRoomCount;
        int normalAssigned = 0;
        foreach (int id in sortedByDist)
        {
            if (normalAssigned < normalLimit)
            {
                SetRoomRole(ref floor, id, RoomRole.NormalRoom);
                normalAssigned++;
            }
        }

        string bossInfo = hasBossAlready ? "pre-placed" : bossRoomId.ToString();
        int excessCount = sortedByDist.Count - normalAssigned;
        LogHelper.Log(LogHelper.GAME, $"CreateMap: Floor {(int)floor.config.floorId} roles assigned — Boss:{bossInfo}, Sub:{subCount}, Normal:{normalAssigned}, Excess(pruned):{excessCount}");
    }

    // ── ⑪ 초과 방 제거: RoomRole.None인 방을 빈 청크로 변환 ──
    void PruneExcessRooms(ref Floor floor)
    {
        int w = floor.config.width;
        int h = floor.config.height;

        var keptIds = new HashSet<int>();
        var pruneCandidate = new HashSet<int>();
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.roomId < 0) continue;
                if (c.roomRole == RoomRole.None)
                    pruneCandidate.Add(c.roomId);
                else
                    keptIds.Add(c.roomId);
            }
        }

        if (pruneCandidate.Count == 0) return;

        var keptChunks = new Dictionary<int, List<(int x, int y)>>();
        var candidateChunks = new Dictionary<int, List<(int x, int y)>>();
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int rid = floor.chunks[x, y].roomId;
                if (rid < 0) continue;
                if (keptIds.Contains(rid))
                {
                    if (!keptChunks.ContainsKey(rid)) keptChunks[rid] = new List<(int x, int y)>();
                    keptChunks[rid].Add((x, y));
                }
                else if (pruneCandidate.Contains(rid))
                {
                    if (!candidateChunks.ContainsKey(rid)) candidateChunks[rid] = new List<(int x, int y)>();
                    candidateChunks[rid].Add((x, y));
                }
            }
        }

        int[] ddx = { 1, 0, -1, 0 };
        int[] ddy = { 0, 1, 0, -1 };

        if (keptIds.Count <= 1)
        {
            PruneRoomSet(ref floor, pruneCandidate, w, h);
            return;
        }

        var keptAdj = new Dictionary<int, HashSet<int>>();
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int ridA = floor.chunks[x, y].roomId;
                if (ridA < 0 || !keptIds.Contains(ridA)) continue;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + ddx[d];
                    int ny = y + ddy[d];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    int ridB = floor.chunks[nx, ny].roomId;
                    if (ridB < 0 || ridB == ridA || !keptIds.Contains(ridB)) continue;
                    if (!keptAdj.ContainsKey(ridA)) keptAdj[ridA] = new HashSet<int>();
                    if (!keptAdj.ContainsKey(ridB)) keptAdj[ridB] = new HashSet<int>();
                    keptAdj[ridA].Add(ridB);
                    keptAdj[ridB].Add(ridA);
                }
            }
        }

        int startKeptId = -1;
        foreach (int kid in keptIds) { startKeptId = kid; break; }

        var reachableKept = new HashSet<int> { startKeptId };
        var bfsQ = new Queue<int>();
        bfsQ.Enqueue(startKeptId);
        while (bfsQ.Count > 0)
        {
            int cur = bfsQ.Dequeue();
            if (!keptAdj.ContainsKey(cur)) continue;
            foreach (int nb in keptAdj[cur])
            {
                if (reachableKept.Add(nb))
                    bfsQ.Enqueue(nb);
            }
        }

        if (reachableKept.Count >= keptIds.Count)
        {
            PruneRoomSet(ref floor, pruneCandidate, w, h);
            return;
        }

        var disconnected = new HashSet<int>();
        foreach (int kid in keptIds)
            if (!reachableKept.Contains(kid))
                disconnected.Add(kid);

        var fullAdj = new Dictionary<int, HashSet<int>>();
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int ridA = floor.chunks[x, y].roomId;
                if (ridA < 0) continue;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + ddx[d];
                    int ny = y + ddy[d];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    int ridB = floor.chunks[nx, ny].roomId;
                    if (ridB < 0 || ridB == ridA) continue;
                    if (!fullAdj.ContainsKey(ridA)) fullAdj[ridA] = new HashSet<int>();
                    if (!fullAdj.ContainsKey(ridB)) fullAdj[ridB] = new HashSet<int>();
                    fullAdj[ridA].Add(ridB);
                    fullAdj[ridB].Add(ridA);
                }
            }
        }

        var bridgeIds = new HashSet<int>();
        foreach (int disId in disconnected)
        {
            var bfsParent = new Dictionary<int, int>();
            var bfsVisited = new HashSet<int> { disId };
            var bfsQ2 = new Queue<int>();
            bfsQ2.Enqueue(disId);
            int found = -1;

            while (bfsQ2.Count > 0)
            {
                int cur = bfsQ2.Dequeue();
                if (!fullAdj.ContainsKey(cur)) continue;
                foreach (int nb in fullAdj[cur])
                {
                    if (bfsVisited.Contains(nb)) continue;
                    bfsVisited.Add(nb);
                    bfsParent[nb] = cur;

                    if (reachableKept.Contains(nb))
                    {
                        found = nb;
                        goto foundBridge;
                    }
                    bfsQ2.Enqueue(nb);
                }
            }

        foundBridge:
            if (found < 0) continue;

            int trace = found;
            while (trace != disId && bfsParent.ContainsKey(trace))
            {
                if (pruneCandidate.Contains(trace))
                    bridgeIds.Add(trace);
                trace = bfsParent[trace];
            }

            reachableKept.Add(disId);
            foreach (int bid in bridgeIds)
                if (!keptIds.Contains(bid))
                    reachableKept.Add(bid);
        }

        foreach (int bid in bridgeIds)
        {
            pruneCandidate.Remove(bid);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (floor.chunks[x, y].roomId == bid)
                    {
                        Chunks c = floor.chunks[x, y];
                        c.roomRole = RoomRole.NormalRoom;
                        floor.chunks[x, y] = c;
                    }
                }
        }

        PruneRoomSet(ref floor, pruneCandidate, w, h);

        if (bridgeIds.Count > 0)
            LogHelper.Log(LogHelper.GAME, $"CreateMap: Floor {(int)floor.config.floorId} — {bridgeIds.Count} bridge room(s) preserved for connectivity.");
    }

    void PruneRoomSet(ref Floor floor, HashSet<int> roomIds, int w, int h)
    {
        if (roomIds.Count == 0) return;

        int prunedChunks = 0;
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.roomId >= 0 && roomIds.Contains(c.roomId))
                {
                    c.roomId = -1;
                    c.roomName = string.Empty;
                    c.roomRole = RoomRole.None;

                    if (c.chunk != null)
                    {
                        int cs = c.chunk.GetLength(0);
                        for (int tx = 0; tx < cs; tx++)
                            for (int ty = 0; ty < cs; ty++)
                                c.chunk[tx, ty] = TileFactory.Wall();
                    }

                    floor.chunks[x, y] = c;
                    prunedChunks++;
                }
            }
        }

        LogHelper.Log(LogHelper.GAME, $"CreateMap: Floor {(int)floor.config.floorId} pruned {roomIds.Count} excess rooms ({prunedChunks} chunks).");
    }

    // ── v2: 서브 목적방 고정 개수 보장(부착 생성) ──
    void EnsureSubPurposeRooms(ref Floor floor)
    {
        int w = floor.config.width;
        int h = floor.config.height;
        int target = floor.config.subPurposeRoomCount;
        if (target <= 0) return;

        var subIds = new HashSet<int>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.roomRole == RoomRole.SubPurposeRoom && c.roomId >= 0)
                    subIds.Add(c.roomId);
            }

        int need = target - subIds.Count;
        if (need <= 0) return;

        var roleMap = BuildRoomRoleMap(ref floor);

        var candidates = new List<(int x, int y, int neighborRoomId)>();
        var fallbackCandidates = new List<(int x, int y, int neighborRoomId)>();
        int[] ddx = { 1, 0, -1, 0 };
        int[] ddy = { 0, 1, 0, -1 };

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (floor.chunks[x, y].roomId != -1) continue;

                var neighborIds = new HashSet<int>();

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + ddx[d];
                    int ny = y + ddy[d];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;

                    int id = floor.chunks[nx, ny].roomId;
                    if (id < 0) continue;

                    if (roleMap.TryGetValue(id, out var nRole))
                    {
                        if (nRole == RoomRole.StartRoom || nRole == RoomRole.BossRoom) continue;
                    }

                    neighborIds.Add(id);
                }

                if (neighborIds.Count == 0) continue;

                int firstNeighbor = -1;
                foreach (int nid in neighborIds) { firstNeighbor = nid; break; }

                if (neighborIds.Count == 1)
                    candidates.Add((x, y, firstNeighbor));
                else
                    fallbackCandidates.Add((x, y, firstNeighbor));
            }
        }

        int created = 0;

        // 1차: 빈 청크에 부착 생성 (이웃 1개인 후보 우선)
        if (candidates.Count > 0)
        {
            ShuffleList(candidates);

            foreach (var (cx, cy, _) in candidates)
            {
                if (created >= need) break;
                if (floor.chunks[cx, cy].roomId != -1) continue;
                if (!HasNonSubNeighbor(ref floor, cx, cy, w, h, ddx, ddy)) continue;

                int id = RoomIdGenerator.GetNextId();
                string name = RoomIdGenerator.FormatName(id);

                Chunks c = floor.chunks[cx, cy];
                c.roomId = id;
                c.roomName = name;
                c.roomRole = RoomRole.SubPurposeRoom;
                c.allowMaxFootprint = 1;

                floor.chunks[cx, cy] = c;
                created++;
            }
        }

        // 1차-b: 이웃 여러 개인 후보 사용
        if (created < need && fallbackCandidates.Count > 0)
        {
            ShuffleList(fallbackCandidates);

            foreach (var (cx, cy, _) in fallbackCandidates)
            {
                if (created >= need) break;
                if (floor.chunks[cx, cy].roomId != -1) continue;
                if (!HasNonSubNeighbor(ref floor, cx, cy, w, h, ddx, ddy)) continue;

                int id = RoomIdGenerator.GetNextId();
                string name = RoomIdGenerator.FormatName(id);

                Chunks c = floor.chunks[cx, cy];
                c.roomId = id;
                c.roomName = name;
                c.roomRole = RoomRole.SubPurposeRoom;
                c.allowMaxFootprint = 1;

                floor.chunks[cx, cy] = c;
                created++;
            }
        }

        // 2차 fallback: leaf 청크를 떼어내 서브 목적방으로 전환
        if (created < need)
        {
            var roomChunkCount = new Dictionary<int, int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    int id = floor.chunks[x, y].roomId;
                    if (id < 0) continue;
                    if (!roomChunkCount.ContainsKey(id)) roomChunkCount[id] = 0;
                    roomChunkCount[id]++;
                }

            var leafCandidates = new List<(int x, int y, int parentRoomId)>();
            int[] ddx2 = { 1, 0, -1, 0 };
            int[] ddy2 = { 0, 1, 0, -1 };

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    int rid = c.roomId;
                    if (rid < 0) continue;

                    if (c.roomRole == RoomRole.StartRoom || c.roomRole == RoomRole.BossRoom || c.roomRole == RoomRole.SubPurposeRoom)
                        continue;

                    if (!roomChunkCount.ContainsKey(rid) || roomChunkCount[rid] < 2)
                        continue;

                    int sameNeighbor = 0;
                    bool touchesOtherRoom = false;

                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + ddx2[d];
                        int ny = y + ddy2[d];
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;

                        int nid = floor.chunks[nx, ny].roomId;
                        if (nid < 0) continue;
                        if (nid == rid) sameNeighbor++;
                        else { touchesOtherRoom = true; break; }
                    }

                    if (touchesOtherRoom) continue;
                    if (sameNeighbor != 1) continue;

                    if (roleMap.TryGetValue(rid, out var parentRole) &&
                        (parentRole == RoomRole.StartRoom || parentRole == RoomRole.BossRoom))
                        continue;

                    leafCandidates.Add((x, y, rid));
                }
            }

            if (leafCandidates.Count > 0)
            {
                ShuffleList(leafCandidates);
                foreach (var (lx, ly, parentId) in leafCandidates)
                {
                    if (created >= need) break;
                    if (floor.chunks[lx, ly].roomId != parentId) continue;
                    if (floor.chunks[lx, ly].roomRole == RoomRole.SubPurposeRoom) continue;

                    int id = RoomIdGenerator.GetNextId();
                    string name = RoomIdGenerator.FormatName(id);

                    Chunks sc = floor.chunks[lx, ly];
                    sc.roomId = id;
                    sc.roomName = name;
                    sc.roomRole = RoomRole.SubPurposeRoom;
                    sc.allowMaxFootprint = 1;

                    floor.chunks[lx, ly] = sc;
                    created++;
                }
            }
        }

        // 3차 fallback: 기존 NormalRoom 중 1청크방을 SubPurposeRoom으로 직접 전환
        if (created < need)
        {
            var normalSingleIds = new List<(int roomId, int x, int y)>();
            var normalChunkCount = new Dictionary<int, int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    int id = floor.chunks[x, y].roomId;
                    if (id < 0) continue;
                    if (!normalChunkCount.ContainsKey(id)) normalChunkCount[id] = 0;
                    normalChunkCount[id]++;
                }

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    if (c.roomId < 0 || c.roomRole != RoomRole.NormalRoom) continue;
                    if (normalChunkCount.ContainsKey(c.roomId) && normalChunkCount[c.roomId] == 1)
                        normalSingleIds.Add((c.roomId, x, y));
                }

            if (normalSingleIds.Count > 0)
            {
                ShuffleList(normalSingleIds);
                foreach (var (rid, nx, ny) in normalSingleIds)
                {
                    if (created >= need) break;
                    if (floor.chunks[nx, ny].roomRole == RoomRole.SubPurposeRoom) continue;

                    Chunks nc = floor.chunks[nx, ny];
                    nc.roomRole = RoomRole.SubPurposeRoom;
                    nc.allowMaxFootprint = 1;
                    floor.chunks[nx, ny] = nc;
                    created++;
                }
            }
        }

        if (created < need)
        {
            LogHelper.Warning(LogHelper.GAME, 
                $"CreateMap: Floor {(int)floor.config.floorId} — 서브 목적방 목표 개수를 모두 채우지 못했습니다. " +
                $"Created={created}, Need={need}, Target={target}");
        }

        LogHelper.Log(LogHelper.GAME, $"CreateMap: Floor {(int)floor.config.floorId} ensured SubPurposeRooms. Created={created}, Target={target}");
    }

    bool HasNonSubNeighbor(ref Floor floor, int cx, int cy, int w, int h, int[] ddx, int[] ddy)
    {
        for (int d = 0; d < 4; d++)
        {
            int nx = cx + ddx[d];
            int ny = cy + ddy[d];
            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
            int nid = floor.chunks[nx, ny].roomId;
            if (nid < 0) continue;
            if (floor.chunks[nx, ny].roomRole != RoomRole.SubPurposeRoom)
                return true;
        }
        return false;
    }
}
