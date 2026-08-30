// ============================================================================
// CreateMap.Connection.cs — 방 연결 · 통로 생성
// ----------------------------------------------------------------------------
// 역할: 방 간 그래프 구축(BuildGraph), Prim MST 기반 연결(ConnectRooms),
//       통로 개방(OpenPassage), 루프 추가(AddLoops),
//       미연결 방 브리지(BridgeDisconnectedRoom),
//       타일 FloodFill 연결 보수(RepairGateConnectivity).
// 단계: GenerateMap Phase 2 — 방 배치 완료 후 연결 구축
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

public partial class CreateMap
{
    // ④ 방 인접 그래프 생성
    void BuildGraph(ref Floor floor)
    {
        roomAdjacency.Clear();

        int w = floor.config.width;
        int h = floor.config.height;

        int[] ddx = { 1, 0, -1, 0 };
        int[] ddy = { 0, 1, 0, -1 };

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int idA = floor.chunks[x, y].roomId;
                if (idA == -1) continue;

                if (!roomAdjacency.ContainsKey(idA))
                    roomAdjacency[idA] = new HashSet<int>();

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + ddx[d];
                    int ny = y + ddy[d];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;

                    int idB = floor.chunks[nx, ny].roomId;
                    if (idB == -1 || idB == idA) continue;

                    AddEdge(idA, idB);
                }
            }
        }

        LogHelper.Log(LogHelper.GAME, $"CreateMap: Floor {(int)floor.config.floorId} graph built. Nodes: {roomAdjacency.Count}");
    }

    void AddEdge(int a, int b)
    {
        if (!roomAdjacency.ContainsKey(a))
            roomAdjacency[a] = new HashSet<int>();
        if (!roomAdjacency.ContainsKey(b))
            roomAdjacency[b] = new HashSet<int>();

        roomAdjacency[a].Add(b);
        roomAdjacency[b].Add(a);
    }

    // ⑤ 시작방은 인접 NormalRoom만 연결 / 나머지는 랜덤 Prim MST
    void ConnectRooms(ref Floor floor)
    {
        connectedPairs.Clear();
        if (floor.gates != null) floor.gates.Clear();

        var roomRoleMap = BuildRoomRoleMap(ref floor);

        var subPurposeIds = new HashSet<int>();
        foreach (var kvp in roomRoleMap)
            if (kvp.Value == RoomRole.SubPurposeRoom)
                subPurposeIds.Add(kvp.Key);

        // 시작방 → 인접 NormalRoom만 무조건 연결
        if (roomAdjacency.ContainsKey(spawnRoomId))
        {
            foreach (int neighbor in roomAdjacency[spawnRoomId])
            {
                RoomRole nRole = roomRoleMap.ContainsKey(neighbor) ? roomRoleMap[neighbor] : RoomRole.None;
                if (nRole != RoomRole.NormalRoom) continue;

                var pair = MakePair(spawnRoomId, neighbor);
                if (connectedPairs.Add(pair))
                    OpenPassage(ref floor, spawnRoomId, neighbor);
            }
        }

        // fallback: 시작방에 연결된 방이 없으면 가장 가까운 인접 방 1개 연결
        if (connectedPairs.Count == 0 && roomAdjacency.ContainsKey(spawnRoomId))
        {
            foreach (int neighbor in roomAdjacency[spawnRoomId])
            {
                if (subPurposeIds.Contains(neighbor)) continue;
                var pair = MakePair(spawnRoomId, neighbor);
                if (connectedPairs.Add(pair))
                    OpenPassage(ref floor, spawnRoomId, neighbor);
                break;
            }
        }

        // 1단계: 서브목적방 제외한 방들로 Prim MST 구성
        var visited = new HashSet<int> { spawnRoomId };
        if (roomAdjacency.ContainsKey(spawnRoomId))
            foreach (int neighbor in roomAdjacency[spawnRoomId])
                if (connectedPairs.Contains(MakePair(spawnRoomId, neighbor)))
                    visited.Add(neighbor);

        var mstNodes = new HashSet<int>();
        foreach (int node in roomAdjacency.Keys)
            if (!subPurposeIds.Contains(node))
                mstNodes.Add(node);

        while (visited.Count < mstNodes.Count)
        {
            var frontier = new List<(int from, int to)>();

            foreach (int v in visited)
            {
                if (!roomAdjacency.ContainsKey(v)) continue;
                foreach (int adj in roomAdjacency[v])
                {
                    if (visited.Contains(adj)) continue;
                    if (subPurposeIds.Contains(adj)) continue;
                    frontier.Add((v, adj));
                }
            }

            if (frontier.Count == 0)
            {
                bool bridged = BridgeDisconnectedRoom(ref floor, visited, mstNodes, subPurposeIds);
                if (!bridged) break;
                continue;
            }

            int idx = UnityEngine.Random.Range(0, frontier.Count);
            var (from, to) = frontier[idx];

            visited.Add(to);
            var pair = MakePair(from, to);
            if (connectedPairs.Add(pair))
                OpenPassage(ref floor, from, to);
        }

        // 2단계: 서브목적방을 인접한 비-서브목적방 1개에만 연결 (단일 입구 보장)
        // MST에 연결된(visited) 이웃을 우선 선택하여 고립 방지
        foreach (int subId in subPurposeIds)
        {
            if (!roomAdjacency.ContainsKey(subId)) continue;

            // 1순위: MST에 이미 연결된 이웃
            int chosenNeighbor = -1;
            foreach (int neighbor in roomAdjacency[subId])
            {
                if (subPurposeIds.Contains(neighbor)) continue;
                if (visited.Contains(neighbor))
                {
                    chosenNeighbor = neighbor;
                    break;
                }
            }

            // 2순위: 아무 비-서브목적 이웃
            if (chosenNeighbor < 0)
            {
                foreach (int neighbor in roomAdjacency[subId])
                {
                    if (subPurposeIds.Contains(neighbor)) continue;
                    chosenNeighbor = neighbor;
                    break;
                }
            }

            if (chosenNeighbor >= 0)
            {
                var pair = MakePair(subId, chosenNeighbor);
                if (connectedPairs.Add(pair))
                    OpenPassage(ref floor, subId, chosenNeighbor);
            }
        }

        LogHelper.Log(LogHelper.GAME, $"CreateMap: Floor {(int)floor.config.floorId} rooms connected. Passages: {connectedPairs.Count}");
    }

    // MST가 끊겼을 때 빈 청크를 관통하여 미방문 방과 방문 방을 연결
    bool BridgeDisconnectedRoom(ref Floor floor, HashSet<int> visited, HashSet<int> mstNodes, HashSet<int> subPurposeIds)
    {
        int w = floor.config.width;
        int h = floor.config.height;

        var unvisitedRoomChunks = new Dictionary<int, List<(int x, int y)>>();

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int rid = floor.chunks[x, y].roomId;
                if (rid < 0 || visited.Contains(rid)) continue;
                if (!mstNodes.Contains(rid) || subPurposeIds.Contains(rid)) continue;

                if (!unvisitedRoomChunks.ContainsKey(rid))
                    unvisitedRoomChunks[rid] = new List<(int x, int y)>();
                unvisitedRoomChunks[rid].Add((x, y));
            }
        }

        if (unvisitedRoomChunks.Count == 0) return false;

        int targetRoomId = -1;
        List<(int x, int y)> targetChunks = null;
        foreach (var kvp in unvisitedRoomChunks)
        {
            targetRoomId = kvp.Key;
            targetChunks = kvp.Value;
            break;
        }

        var parent = new Dictionary<(int x, int y), (int x, int y)>();
        var bfsQueue = new Queue<(int x, int y)>();
        var bfsVisited = new HashSet<(int x, int y)>();

        foreach (var (sx, sy) in targetChunks)
        {
            bfsQueue.Enqueue((sx, sy));
            bfsVisited.Add((sx, sy));
            parent[(sx, sy)] = (-1, -1);
        }

        int[] ddx = { 1, 0, -1, 0 };
        int[] ddy = { 0, 1, 0, -1 };
        (int x, int y) endPoint = (-1, -1);

        while (bfsQueue.Count > 0)
        {
            var (cx, cy) = bfsQueue.Dequeue();

            for (int d = 0; d < 4; d++)
            {
                int nx = cx + ddx[d];
                int ny = cy + ddy[d];
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (bfsVisited.Contains((nx, ny))) continue;

                bfsVisited.Add((nx, ny));
                parent[(nx, ny)] = (cx, cy);

                int nrid = floor.chunks[nx, ny].roomId;

                if (nrid >= 0 && visited.Contains(nrid))
                {
                    endPoint = (nx, ny);
                    goto pathFound;
                }

                if (nrid == -1)
                    bfsQueue.Enqueue((nx, ny));
                else if (nrid >= 0 && !visited.Contains(nrid) && !subPurposeIds.Contains(nrid))
                    bfsQueue.Enqueue((nx, ny));
            }
        }

    pathFound:
        if (endPoint.x < 0) return false;

        var path = new List<(int x, int y)>();
        var cur = endPoint;
        while (cur.x >= 0)
        {
            path.Add(cur);
            cur = parent[cur];
        }
        path.Reverse();

        int corridorId = BuildCorridorAlongPath(ref floor, path);

        if (corridorId >= 0)
        {
            mstNodes.Add(corridorId);
            visited.Add(corridorId);
        }

        visited.Add(targetRoomId);
        // 경로 상 모든 방을 visited에 추가
        for (int i = 0; i < path.Count; i++)
        {
            int rid = floor.chunks[path[i].x, path[i].y].roomId;
            if (rid >= 0) visited.Add(rid);
        }

        LogHelper.Log(LogHelper.GAME, $"CreateMap: Floor {(int)floor.config.floorId} — bridge corridor created (roomId={corridorId}) connecting roomId {targetRoomId} to visited rooms.");
        return true;
    }

    (int, int) MakePair(int a, int b) => a < b ? (a, b) : (b, a);

    void OpenPassage(ref Floor floor, int roomA, int roomB)
    {
        int w = floor.config.width;
        int h = floor.config.height;

        var horizontal = new List<(int x, int y)>();
        var vertical   = new List<(int x, int y)>();

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int id = floor.chunks[x, y].roomId;
                if (id != roomA && id != roomB) continue;

                if (x + 1 < w)
                {
                    int nId = floor.chunks[x + 1, y].roomId;
                    if ((id == roomA && nId == roomB) || (id == roomB && nId == roomA))
                        horizontal.Add((x, y));
                }

                if (y + 1 < h)
                {
                    int nId = floor.chunks[x, y + 1].roomId;
                    if ((id == roomA && nId == roomB) || (id == roomB && nId == roomA))
                        vertical.Add((x, y));
                }
            }
        }

        int total = horizontal.Count + vertical.Count;
        if (total == 0) return;

        // 통로 폭은 항상 2로 고정한다(가변 폭은 들쭉날쭉해 부자연스러움 — 문 배치가 통로 양쪽 문턱
        // 2줄을 심으므로 2폭×2줄). 롤백 가능성 있어 기존 계산 로직은 주석 처리만 해둔다.
        // int gateWidth = ComputeGateWidth(ref floor, roomA, roomB);
        int gateWidth = 2;

        int pick = UnityEngine.Random.Range(0, total);
        if (pick < horizontal.Count)
        {
            var (hx, hy) = horizontal[pick];
            int passageWidth = OpenHorizontalPassage(ref floor, hx, hy, gateWidth);
            int leftId = floor.chunks[hx, hy].roomId;
            if (leftId == roomA)
                RegisterGate(ref floor, roomA, roomB, hx, hy, hx + 1, hy, passageWidth, true);
            else
                RegisterGate(ref floor, roomA, roomB, hx + 1, hy, hx, hy, passageWidth, true);
        }
        else
        {
            var (vx, vy) = vertical[pick - horizontal.Count];
            int passageWidth = OpenVerticalPassage(ref floor, vx, vy, gateWidth);
            int bottomId = floor.chunks[vx, vy].roomId;
            if (bottomId == roomA)
                RegisterGate(ref floor, roomA, roomB, vx, vy, vx, vy + 1, passageWidth, false);
            else
                RegisterGate(ref floor, roomA, roomB, vx, vy + 1, vx, vy, passageWidth, false);
        }
    }

    int ComputeGateWidth(ref Floor floor, int roomA, int roomB)
    {
        int w = floor.config.width;
        int h = floor.config.height;
        int maxA = 1, maxB = 1;

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                int id = floor.chunks[x, y].roomId;
                if (id == roomA) maxA = Mathf.Max(maxA, floor.chunks[x, y].allowMaxFootprint);
                if (id == roomB) maxB = Mathf.Max(maxB, floor.chunks[x, y].allowMaxFootprint);
            }

        return Mathf.Max(maxA, maxB);
    }

    void RegisterGate(ref Floor floor, int roomA, int roomB, int cxA, int cyA, int cxB, int cyB, int width, bool isHorizontal)
    {
        Gate gate = new Gate();
        gate.roomA = roomA;
        gate.roomB = roomB;
        gate.chunkAX = cxA;
        gate.chunkAY = cyA;
        gate.chunkBX = cxB;
        gate.chunkBY = cyB;
        gate.width = width;
        gate.isHorizontal = isHorizontal;

        if (floor.gates == null)
            floor.gates = new System.Collections.Generic.List<Gate>();
        floor.gates.Add(gate);
    }

    // BFS 경로의 빈 청크를 복도 방으로 변환하고 통로를 개방하여 연결
    // 반환값: 생성된 복도 roomId (-1이면 빈 청크 없음)
    int BuildCorridorAlongPath(ref Floor floor, List<(int x, int y)> path)
    {
        int w = floor.config.width;
        int h = floor.config.height;
        int[] ddx = { 1, 0, -1, 0 };
        int[] ddy = { 0, 1, 0, -1 };

        // 1. 빈 청크를 복도 방으로 변환
        int corridorId = -1;
        string corridorName = null;

        foreach (var (px, py) in path)
        {
            if (floor.chunks[px, py].roomId != -1) continue;

            if (corridorId < 0)
            {
                corridorId = RoomIdGenerator.GetNextId();
                corridorName = RoomIdGenerator.FormatName(corridorId);
            }

            Chunks c = floor.chunks[px, py];
            c.roomId = corridorId;
            c.roomName = corridorName;
            c.roomRole = RoomRole.NormalRoom;
            c.allowMaxFootprint = 1;

            int cs = floor.config.chunkSize;
            if (c.chunk == null) c.chunk = new Tile[cs, cs];
            for (int tx = 0; tx < cs; tx++)
                for (int ty = 0; ty < cs; ty++)
                    c.chunk[tx, ty] = (tx == 0 || tx == cs - 1 || ty == 0 || ty == cs - 1)
                        ? TileFactory.Wall()
                        : TileFactory.Floor();

            floor.chunks[px, py] = c;
        }

        // 2. 같은 복도 방 내 인접 청크 간 내부 벽 제거
        if (corridorId >= 0)
        {
            for (int i = 0; i < path.Count - 1; i++)
            {
                var (ax, ay) = path[i];
                var (bx, by) = path[i + 1];
                if (floor.chunks[ax, ay].roomId != corridorId || floor.chunks[bx, by].roomId != corridorId)
                    continue;

                if (bx == ax + 1 && by == ay)
                    RemoveHorizontalWall(ref floor, ax, ay, corridorId);
                else if (bx == ax - 1 && by == ay)
                    RemoveHorizontalWall(ref floor, bx, by, corridorId);
                else if (by == ay + 1 && bx == ax)
                    RemoveVerticalWall(ref floor, ax, ay, corridorId);
                else if (by == ay - 1 && bx == ax)
                    RemoveVerticalWall(ref floor, bx, by, corridorId);
            }

            // 3. 복도 인접 그래프 갱신
            if (!roomAdjacency.ContainsKey(corridorId))
                roomAdjacency[corridorId] = new HashSet<int>();

            foreach (var (px, py) in path)
            {
                if (floor.chunks[px, py].roomId != corridorId) continue;
                for (int d = 0; d < 4; d++)
                {
                    int nx = px + ddx[d];
                    int ny = py + ddy[d];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    int nrid = floor.chunks[nx, ny].roomId;
                    if (nrid >= 0 && nrid != corridorId)
                        AddEdge(corridorId, nrid);
                }
            }
        }

        // 4. 경로 상 인접한 서로 다른 방 간 통로 개방 + 그래프 갱신
        for (int i = 0; i < path.Count - 1; i++)
        {
            var (ax, ay) = path[i];
            var (bx, by) = path[i + 1];
            int ridA = floor.chunks[ax, ay].roomId;
            int ridB = floor.chunks[bx, by].roomId;

            if (ridA < 0 || ridB < 0 || ridA == ridB) continue;

            AddEdge(ridA, ridB);
            var pair = MakePair(ridA, ridB);
            if (!connectedPairs.Add(pair)) continue;

            OpenPassageBetweenChunks(ref floor, ax, ay, bx, by, ridA, ridB);
        }

        return corridorId;
    }

    // 인접 청크 (ax,ay)↔(bx,by) 사이에 방향 판별 후 통로 개방 + Gate 등록
    void OpenPassageBetweenChunks(ref Floor floor, int ax, int ay, int bx, int by, int ridA, int ridB)
    {
        // OpenPassage와 동일하게 통로 폭을 2로 고정. 롤백 가능성 있어 기존 계산 로직은 주석 처리만.
        // int gateWidth = ComputeGateWidth(ref floor, ridA, ridB);
        int gateWidth = 2;

        if (bx == ax + 1 && by == ay)
        {
            int pw = OpenHorizontalPassage(ref floor, ax, ay, gateWidth);
            if (floor.chunks[ax, ay].roomId == ridA)
                RegisterGate(ref floor, ridA, ridB, ax, ay, bx, by, pw, true);
            else
                RegisterGate(ref floor, ridA, ridB, bx, by, ax, ay, pw, true);
        }
        else if (bx == ax - 1 && by == ay)
        {
            int pw = OpenHorizontalPassage(ref floor, bx, by, gateWidth);
            if (floor.chunks[bx, by].roomId == ridA)
                RegisterGate(ref floor, ridA, ridB, bx, by, ax, ay, pw, true);
            else
                RegisterGate(ref floor, ridA, ridB, ax, ay, bx, by, pw, true);
        }
        else if (by == ay + 1 && bx == ax)
        {
            int pw = OpenVerticalPassage(ref floor, ax, ay, gateWidth);
            if (floor.chunks[ax, ay].roomId == ridA)
                RegisterGate(ref floor, ridA, ridB, ax, ay, bx, by, pw, false);
            else
                RegisterGate(ref floor, ridA, ridB, bx, by, ax, ay, pw, false);
        }
        else if (by == ay - 1 && bx == ax)
        {
            int pw = OpenVerticalPassage(ref floor, bx, by, gateWidth);
            if (floor.chunks[bx, by].roomId == ridA)
                RegisterGate(ref floor, ridA, ridB, bx, by, ax, ay, pw, false);
            else
                RegisterGate(ref floor, ridA, ridB, ax, ay, bx, by, pw, false);
        }
    }

    int OpenHorizontalPassage(ref Floor floor, int x, int y, int gateWidth)
    {
        Chunks cA = floor.chunks[x, y];
        Chunks cB = floor.chunks[x + 1, y];
        int cs = floor.config.chunkSize;

        int width = Mathf.Clamp(gateWidth, 2, 6);
        int startTy = (cs - width) / 2;
        int endTy = startTy + width - 1;

        int thkA = GetWallThickness(cA.roomId, true);
        int thkB = GetWallThickness(cB.roomId, true);

        int opened = 0;
        for (int ty = startTy; ty <= endTy; ty++)
        {
            for (int tx = cs - thkA; tx < cs; tx++)
                cA.chunk[tx, ty] = TileFactory.Floor();
            for (int tx = 0; tx < thkB; tx++)
                cB.chunk[tx, ty] = TileFactory.Floor();
            opened++;
        }

        floor.chunks[x, y] = cA;
        floor.chunks[x + 1, y] = cB;
        return opened;
    }

    int OpenVerticalPassage(ref Floor floor, int x, int y, int gateWidth)
    {
        Chunks cA = floor.chunks[x, y];
        Chunks cB = floor.chunks[x, y + 1];
        int cs = floor.config.chunkSize;

        int width = Mathf.Clamp(gateWidth, 2, 6);
        int startTx = (cs - width) / 2;
        int endTx = startTx + width - 1;

        int thkA = GetWallThickness(cA.roomId, false);
        int thkB = GetWallThickness(cB.roomId, false);

        int opened = 0;
        for (int tx = startTx; tx <= endTx; tx++)
        {
            for (int ty = cs - thkA; ty < cs; ty++)
                cA.chunk[tx, ty] = TileFactory.Floor();
            for (int ty = 0; ty < thkB; ty++)
                cB.chunk[tx, ty] = TileFactory.Floor();
            opened++;
        }

        floor.chunks[x, y] = cA;
        floor.chunks[x, y + 1] = cB;
        return opened;
    }


    void AddLoops(ref Floor floor)
    {
        var unopened = new List<(int, int)>();

        var roleMap = BuildRoomRoleMap(ref floor);

        foreach (var kvp in roomAdjacency)
        {
            int a = kvp.Key;
            foreach (int b in kvp.Value)
            {
                if (a >= b) continue;
                var pair = MakePair(a, b);
                if (connectedPairs.Contains(pair)) continue;

                RoomRole roleA = roleMap.ContainsKey(a) ? roleMap[a] : RoomRole.None;
                RoomRole roleB = roleMap.ContainsKey(b) ? roleMap[b] : RoomRole.None;
                if (roleA != RoomRole.NormalRoom || roleB != RoomRole.NormalRoom) continue;

                unopened.Add(pair);
            }
        }

        int loopCount = Mathf.RoundToInt(unopened.Count * 0.05f);
        if (loopCount <= 0 || unopened.Count == 0)
        {
            LogHelper.Log(LogHelper.GAME, $"CreateMap: Floor {(int)floor.config.floorId} loops added. Extra passages: 0");
            return;
        }

        for (int i = unopened.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var temp = unopened[i];
            unopened[i] = unopened[j];
            unopened[j] = temp;
        }

        int actualLoops = Mathf.Min(loopCount, unopened.Count);
        for (int i = 0; i < actualLoops; i++)
        {
            connectedPairs.Add(unopened[i]);
            OpenPassage(ref floor, unopened[i].Item1, unopened[i].Item2);
        }

        LogHelper.Log(LogHelper.GAME, $"CreateMap: Floor {(int)floor.config.floorId} loops added. Extra passages: {actualLoops}");
    }

    void ClosePassage(ref Floor floor, Gate gate)
    {
        int cs = floor.config.chunkSize;
        int width = Mathf.Clamp(gate.width, 2, 6);

        if (gate.isHorizontal)
        {
            int leftX = Mathf.Min(gate.chunkAX, gate.chunkBX);
            int rightX = Mathf.Max(gate.chunkAX, gate.chunkBX);
            int y = gate.chunkAY;

            Chunks cLeft = floor.chunks[leftX, y];
            Chunks cRight = floor.chunks[rightX, y];

            int thkLeft = GetWallThickness(cLeft.roomId, true);
            int thkRight = GetWallThickness(cRight.roomId, true);

            int startTy = (cs - width) / 2;
            int endTy = startTy + width - 1;
            for (int ty = startTy; ty <= endTy; ty++)
            {
                for (int tx = cs - thkLeft; tx < cs; tx++)
                    cLeft.chunk[tx, ty] = TileFactory.Wall();
                for (int tx = 0; tx < thkRight; tx++)
                    cRight.chunk[tx, ty] = TileFactory.Wall();
            }

            floor.chunks[leftX, y] = cLeft;
            floor.chunks[rightX, y] = cRight;
        }
        else
        {
            int x = gate.chunkAX;
            int bottomY = Mathf.Min(gate.chunkAY, gate.chunkBY);
            int topY = Mathf.Max(gate.chunkAY, gate.chunkBY);

            Chunks cBottom = floor.chunks[x, bottomY];
            Chunks cTop = floor.chunks[x, topY];

            int thkBottom = GetWallThickness(cBottom.roomId, false);
            int thkTop = GetWallThickness(cTop.roomId, false);

            int startTx = (cs - width) / 2;
            int endTx = startTx + width - 1;
            for (int tx = startTx; tx <= endTx; tx++)
            {
                for (int ty = cs - thkBottom; ty < cs; ty++)
                    cBottom.chunk[tx, ty] = TileFactory.Wall();
                for (int ty = 0; ty < thkTop; ty++)
                    cTop.chunk[tx, ty] = TileFactory.Wall();
            }

            floor.chunks[x, bottomY] = cBottom;
            floor.chunks[x, topY] = cTop;
        }
    }

    // 타일 레벨 FloodFill로 시작방에서 도달 불가능한 방을 찾아 통로를 개방/생성
    void RepairGateConnectivity(ref Floor floor)
    {
        int w = floor.config.width;
        int h = floor.config.height;
        int cs = floor.config.chunkSize;
        int totalW = w * cs;
        int totalH = h * cs;

        int[] ddx = { 1, 0, -1, 0 };
        int[] ddy = { 0, 1, 0, -1 };

        // 시작방의 첫 Floor 타일 찾기
        int startWx = -1, startWy = -1;
        for (int x = 0; x < w && startWx < 0; x++)
            for (int y = 0; y < h && startWx < 0; y++)
                if (floor.chunks[x, y].roomRole == RoomRole.StartRoom)
                {
                    if (floor.chunks[x, y].chunk == null) continue;
                    for (int tx = 0; tx < cs && startWx < 0; tx++)
                        for (int ty = 0; ty < cs && startWx < 0; ty++)
                            if (floor.chunks[x, y].chunk[tx, ty].name != "Wall")
                            { startWx = x * cs + tx; startWy = y * cs + ty; }
                }

        if (startWx < 0) return;

        int maxIterations = 30;
        int totalRepaired = 0;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            // FloodFill로 도달 가능한 타일 수집
            bool[,] floodVisited = new bool[totalW, totalH];
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue((startWx, startWy));
            floodVisited[startWx, startWy] = true;

            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + ddx[d];
                    int ny = cy + ddy[d];
                    if (nx < 0 || nx >= totalW || ny < 0 || ny >= totalH) continue;
                    if (floodVisited[nx, ny]) continue;

                    int chunkX = nx / cs, chunkY = ny / cs;
                    int tileX = nx % cs, tileY = ny % cs;
                    if (floor.chunks[chunkX, chunkY].chunk == null) continue;
                    if (floor.chunks[chunkX, chunkY].chunk[tileX, tileY].name != "Wall")
                    {
                        floodVisited[nx, ny] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }

            // 도달 가능 roomId 수집
            var reachable = new HashSet<int>();
            var allRoomIds = new HashSet<int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    int rid = floor.chunks[x, y].roomId;
                    if (rid < 0) continue;
                    allRoomIds.Add(rid);
                    if (floor.chunks[x, y].chunk == null) continue;
                    if (reachable.Contains(rid)) continue;
                    for (int tx = 0; tx < cs; tx++)
                        for (int ty = 0; ty < cs; ty++)
                            if (floor.chunks[x, y].chunk[tx, ty].name != "Wall" && floodVisited[x * cs + tx, y * cs + ty])
                            { reachable.Add(rid); goto nextChunk; }
                    nextChunk:;
                }

            var unreachable = new HashSet<int>();
            foreach (int id in allRoomIds)
                if (!reachable.Contains(id))
                    unreachable.Add(id);

            if (unreachable.Count == 0)
            {
                if (totalRepaired > 0)
                    LogHelper.Log(LogHelper.GAME, $"CreateMap: Floor {(int)floor.config.floorId} gate connectivity repaired. Fixed {totalRepaired} connection(s) in {iter} iteration(s).");
                return;
            }

            bool repaired = false;

            // Stage 1: 미도달 방 → 인접 그래프에서 도달 가능한 이웃과 통로 개방
            foreach (int uId in unreachable)
            {
                if (!roomAdjacency.ContainsKey(uId)) continue;
                foreach (int neighbor in roomAdjacency[uId])
                {
                    if (!reachable.Contains(neighbor)) continue;

                    var pair = MakePair(uId, neighbor);
                    connectedPairs.Add(pair);
                    OpenPassage(ref floor, uId, neighbor);
                    repaired = true;
                    totalRepaired++;
                    break;
                }
                if (repaired) break;
            }

            if (repaired) continue;

            // Stage 2: BFS로 빈 청크를 관통하여 브리지 생성
            repaired = RepairBridgeIsolatedRoom(ref floor, reachable, unreachable);
            if (repaired)
            {
                totalRepaired++;
                continue;
            }

            LogHelper.Warning(LogHelper.GAME, $"CreateMap: Floor {(int)floor.config.floorId} — {unreachable.Count} room(s) remain unreachable. Unreachable: {string.Join(",", unreachable)}");
            break;
        }

        if (totalRepaired > 0)
            LogHelper.Log(LogHelper.GAME, $"CreateMap: Floor {(int)floor.config.floorId} gate connectivity repaired. Fixed {totalRepaired} connection(s).");
    }

    // RepairGateConnectivity Stage 2: BFS로 빈 청크를 관통하여 고립된 방을 도달 가능 방에 연결
    bool RepairBridgeIsolatedRoom(ref Floor floor, HashSet<int> reachable, HashSet<int> unreachable)
    {
        int w = floor.config.width;
        int h = floor.config.height;

        int[] ddx = { 1, 0, -1, 0 };
        int[] ddy = { 0, 1, 0, -1 };

        // 미도달 방의 청크 좌표 수집
        var unreachableChunks = new List<(int x, int y)>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                int rid = floor.chunks[x, y].roomId;
                if (rid >= 0 && unreachable.Contains(rid))
                    unreachableChunks.Add((x, y));
            }

        if (unreachableChunks.Count == 0) return false;

        // BFS: 미도달 청크에서 출발하여 도달 가능한 방의 청크를 찾음
        var parent = new Dictionary<(int x, int y), (int x, int y)>();
        var bfsQueue = new Queue<(int x, int y)>();
        var bfsVisited = new HashSet<(int x, int y)>();

        foreach (var (sx, sy) in unreachableChunks)
        {
            bfsQueue.Enqueue((sx, sy));
            bfsVisited.Add((sx, sy));
            parent[(sx, sy)] = (-1, -1);
        }

        (int x, int y) endPoint = (-1, -1);

        while (bfsQueue.Count > 0)
        {
            var (cx, cy) = bfsQueue.Dequeue();

            for (int d = 0; d < 4; d++)
            {
                int nx = cx + ddx[d];
                int ny = cy + ddy[d];
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (bfsVisited.Contains((nx, ny))) continue;

                bfsVisited.Add((nx, ny));
                parent[(nx, ny)] = (cx, cy);

                int nrid = floor.chunks[nx, ny].roomId;

                if (nrid >= 0 && reachable.Contains(nrid))
                {
                    endPoint = (nx, ny);
                    goto bridgePathFound;
                }

                if (nrid == -1 || (nrid >= 0 && unreachable.Contains(nrid)))
                    bfsQueue.Enqueue((nx, ny));
            }
        }

    bridgePathFound:
        if (endPoint.x < 0) return false;

        var path = new List<(int x, int y)>();
        var cur = endPoint;
        while (cur.x >= 0)
        {
            path.Add(cur);
            cur = parent[cur];
        }
        path.Reverse();

        int corridorId = BuildCorridorAlongPath(ref floor, path);

        LogHelper.Log(LogHelper.GAME, $"CreateMap: Floor {(int)floor.config.floorId} — repair bridge created (corridor={corridorId}, path length={path.Count}).");
        return true;
    }
}
