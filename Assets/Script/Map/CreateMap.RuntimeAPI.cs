// ============================================================================
// CreateMap.RuntimeAPI.cs — 런타임 공개 API
// ----------------------------------------------------------------------------
// 역할: 게임 플레이 중 맵 상태를 변경·조회하는 공개 메서드 모음.
//       점령(ConquerRoom), 후퇴(RetreatFromRoom), 후퇴 목표 탐색(FindRetreatTarget),
//       전초기지 건설/해체(BuildOutpost, DemolishOutpost),
//       계단 개방(OpenStair), 통행 가능 판정(CanEntityPassGate, CanEntityEnterRoom),
//       층 점령 확인(IsFloorOccupied), 도달/재진입 가능(CanReachFloor, CanReenterFloor),
//       Gate 폭 재계산(RecalculateGateWidth), 층간 경로 탐색(FindPathAcrossFloors),
//       직렬화/역직렬화(SerializeMap, DeserializeMap).
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public partial class CreateMap
{
    // ── 편의 접근: 현재 보고 있는 Floor 반환 ──
    public Floor GetCurrentFloor()
    {
        if (map.floors == null || currentFloorIndex < 0 || currentFloorIndex >= map.floors.Length)
            return default;
        return map.floors[currentFloorIndex];
    }

    // ── 맵 직렬화 (저장/로드) ──

    public string SerializeMap()
    {
        return MapSerializer.ToJson(map, true);
    }

    public void DeserializeMap(string json)
    {
        map = MapSerializer.FromJson(json);
        if (map.floors != null && map.floors.Length > 0)
        {
            floorConfigs = new FloorConfig[map.floors.Length];
            for (int f = 0; f < map.floors.Length; f++)
                floorConfigs[f] = map.floors[f].config;
        }
        Debug.Log($"CreateMap: 맵 역직렬화 완료. Floors: {(map.floors != null ? map.floors.Length : 0)}");
    }

    public void SaveMapToFile(string filePath)
    {
        string json = SerializeMap();
        System.IO.File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
        Debug.Log($"CreateMap: 맵 저장 완료 → {filePath} ({json.Length} bytes)");
    }

    public void LoadMapFromFile(string filePath)
    {
        if (!System.IO.File.Exists(filePath))
        {
            Debug.LogError($"CreateMap: 파일을 찾을 수 없습니다 — {filePath}");
            return;
        }

        string json = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
        DeserializeMap(json);
        Debug.Log($"CreateMap: 맵 로드 완료 ← {filePath}");
    }

    // ── 런타임: 점령/후퇴 API ──

    public void ConquerRoom(int floorIndex, int roomId)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return;
        ref Floor floor = ref map.floors[floorIndex];

        SetChunksOccupation(ref floor, roomId, OccupationState.PlayerControlled);

        Debug.Log($"CreateMap: F{floorIndex} roomId={roomId} 점령 완료.");
    }

    public void RetreatFromRoom(int floorIndex, int roomId)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return;
        ref Floor floor = ref map.floors[floorIndex];

        int w = floor.config.width;
        int h = floor.config.height;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (floor.chunks[x, y].roomId == roomId && floor.chunks[x, y].roomRole == RoomRole.StartRoom)
                {
                    Debug.LogWarning($"CreateMap: F{floorIndex} roomId={roomId} — 시작방은 후퇴할 수 없습니다.");
                    return;
                }

        bool isControlled = false;
        for (int x = 0; x < w && !isControlled; x++)
            for (int y = 0; y < h && !isControlled; y++)
                if (floor.chunks[x, y].roomId == roomId &&
                    (floor.chunks[x, y].occupationState == OccupationState.PlayerControlled ||
                     floor.chunks[x, y].occupationState == OccupationState.Outpost))
                    isControlled = true;

        if (!isControlled)
        {
            Debug.LogWarning($"CreateMap: F{floorIndex} roomId={roomId} — 점령/전초기지 상태가 아니므로 후퇴할 수 없습니다.");
            return;
        }

        SetChunksOccupation(ref floor, roomId, OccupationState.Neutral);

        Debug.Log($"CreateMap: F{floorIndex} roomId={roomId} 후퇴 완료.");
    }

    void SetChunksOccupation(ref Floor floor, int roomId, OccupationState state)
    {
        int w = floor.config.width;
        int h = floor.config.height;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (floor.chunks[x, y].roomId == roomId)
                {
                    Chunks c = floor.chunks[x, y];
                    c.occupationState = state;

                    if (state == OccupationState.PlayerControlled && c.chunk != null)
                    {
                        for (int tx = 0; tx < 8; tx++)
                            for (int ty = 0; ty < 8; ty++)
                            {
                                Tile t = c.chunk[tx, ty];
                                t.understand = 100;
                                c.chunk[tx, ty] = t;
                            }
                    }

                    floor.chunks[x, y] = c;
                }
    }

    // 전초기지는 인류가 명시적으로 점령방을 거점화한 상태 (자동 확산 아님)
    public void BuildOutpost(int floorIndex, int roomId)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return;
        ref Floor floor = ref map.floors[floorIndex];

        int w = floor.config.width;
        int h = floor.config.height;

        // 해당 방이 PlayerControlled 상태인지 확인
        bool isControlled = false;
        for (int x = 0; x < w && !isControlled; x++)
            for (int y = 0; y < h && !isControlled; y++)
                if (floor.chunks[x, y].roomId == roomId && floor.chunks[x, y].occupationState == OccupationState.PlayerControlled)
                    isControlled = true;

        if (!isControlled)
        {
            Debug.LogWarning($"CreateMap: F{floorIndex} roomId={roomId} — PlayerControlled 상태가 아니므로 전초기지를 건설할 수 없습니다.");
            return;
        }

        SetChunksOccupation(ref floor, roomId, OccupationState.Outpost);
        Debug.Log($"CreateMap: F{floorIndex} roomId={roomId} 전초기지 건설 완료.");
    }

    public void DemolishOutpost(int floorIndex, int roomId)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return;
        ref Floor floor = ref map.floors[floorIndex];

        int w = floor.config.width;
        int h = floor.config.height;

        bool isOutpost = false;
        for (int x = 0; x < w && !isOutpost; x++)
            for (int y = 0; y < h && !isOutpost; y++)
                if (floor.chunks[x, y].roomId == roomId && floor.chunks[x, y].occupationState == OccupationState.Outpost)
                    isOutpost = true;

        if (!isOutpost) return;

        SetChunksOccupation(ref floor, roomId, OccupationState.PlayerControlled);
        Debug.Log($"CreateMap: F{floorIndex} roomId={roomId} 전초기지 해제 → PlayerControlled.");
    }

    public void OpenStair(int floorIndex, int targetFloor)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return;
        ref Floor floor = ref map.floors[floorIndex];
        int w = floor.config.width;
        int h = floor.config.height;

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (floor.chunks[x, y].stairTargetFloor == targetFloor)
                {
                    Chunks c = floor.chunks[x, y];
                    c.stairIsOpen = true;
                    floor.chunks[x, y] = c;
                }

        Debug.Log($"CreateMap: F{floorIndex} → F{targetFloor} 계단 개방.");
    }

    // ── Footprint 기반 통행 판정 API ──

    public bool CanEntityPassGate(int footprintSize, Gate gate)
    {
        return footprintSize <= gate.width;
    }

    public bool CanEntityEnterRoom(int footprintSize, int floorIndex, int roomId)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return false;
        Floor floor = map.floors[floorIndex];
        if (floor.gates == null || floor.gates.Count == 0) return false;

        foreach (Gate g in floor.gates)
        {
            if ((g.roomA == roomId || g.roomB == roomId) && footprintSize <= g.width)
                return true;
        }

        return false;
    }

    public int GetRoomAllowMaxFootprint(int floorIndex, int roomId)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return 0;
        Floor floor = map.floors[floorIndex];
        int w = floor.config.width;
        int h = floor.config.height;
        int maxFp = 0;

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (floor.chunks[x, y].roomId == roomId)
                    maxFp = Mathf.Max(maxFp, floor.chunks[x, y].allowMaxFootprint);

        return maxFp;
    }

    public static bool IsValidFootprint(int value)
    {
        return value >= 1 && value <= 5;
    }

    public int FindRetreatTarget(int floorIndex, int currentRoomId)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return -1;

        // 현재 층에서 가장 가까운 점령 거점(PlayerControlled 또는 Outpost) 탐색
        int sameFloorTarget = FindRetreatTargetInFloor(floorIndex, currentRoomId);
        if (sameFloorTarget >= 0) return sameFloorTarget;

        // 점령 거점이 없으면 던전 입구(0층) 방향으로 후퇴
        // 현재 층 시작방을 중간 목표로 반환 (시작방 → 계단 → 0층 경로의 첫 단계)
        Floor floor = map.floors[floorIndex];
        int w = floor.config.width;
        int h = floor.config.height;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (floor.chunks[x, y].roomRole == RoomRole.StartRoom && floor.chunks[x, y].roomId >= 0)
                    return floor.chunks[x, y].roomId;

        return -1;
    }

    int FindRetreatTargetInFloor(int floorIndex, int currentRoomId)
    {
        Floor floor = map.floors[floorIndex];
        if (floor.gates == null || floor.gates.Count == 0) return -1;

        var adj = new Dictionary<int, HashSet<int>>();
        foreach (Gate g in floor.gates)
        {
            if (!adj.ContainsKey(g.roomA)) adj[g.roomA] = new HashSet<int>();
            if (!adj.ContainsKey(g.roomB)) adj[g.roomB] = new HashSet<int>();
            adj[g.roomA].Add(g.roomB);
            adj[g.roomB].Add(g.roomA);
        }

        int w = floor.config.width;
        int h = floor.config.height;
        var roomOcc = new Dictionary<int, OccupationState>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                int id = floor.chunks[x, y].roomId;
                if (id >= 0 && !roomOcc.ContainsKey(id))
                    roomOcc[id] = floor.chunks[x, y].occupationState;
            }

        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(currentRoomId);
        visited.Add(currentRoomId);

        while (queue.Count > 0)
        {
            int room = queue.Dequeue();

            if (room != currentRoomId)
            {
                // 점령 거점: PlayerControlled 또는 Outpost
                if (roomOcc.ContainsKey(room) &&
                    (roomOcc[room] == OccupationState.PlayerControlled || roomOcc[room] == OccupationState.Outpost))
                    return room;
            }

            if (!adj.ContainsKey(room)) continue;
            foreach (int neighbor in adj[room])
            {
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        return -1;
    }

    public bool CanCommandEnemyRoom(int floorIndex, int playerRoomId, int targetRoomId)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return false;
        Floor floor = map.floors[floorIndex];
        if (floor.gates == null || floor.gates.Count == 0) return false;

        int w = floor.config.width;
        int h = floor.config.height;
        bool isPlayerControlled = false;
        for (int x = 0; x < w && !isPlayerControlled; x++)
            for (int y = 0; y < h && !isPlayerControlled; y++)
                if (floor.chunks[x, y].roomId == playerRoomId && floor.chunks[x, y].occupationState == OccupationState.PlayerControlled)
                    isPlayerControlled = true;

        if (!isPlayerControlled) return false;

        foreach (Gate g in floor.gates)
        {
            if ((g.roomA == playerRoomId && g.roomB == targetRoomId) ||
                (g.roomB == playerRoomId && g.roomA == targetRoomId))
                return true;
        }

        return false;
    }

    public bool IsFloorOccupied(int floorIndex)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return false;
        Floor floor = map.floors[floorIndex];
        int w = floor.config.width;
        int h = floor.config.height;

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.roomRole == RoomRole.BossRoom && c.occupationState == OccupationState.PlayerControlled)
                    return true;
            }

        return false;
    }

    public bool CanReachFloor(int fromFloor, int toFloor, bool monsterCanUse = true)
    {
        if (map.floors == null) return false;
        if (fromFloor < 0 || fromFloor >= map.floors.Length) return false;
        if (toFloor < 0 || toFloor >= map.floors.Length) return false;
        if (fromFloor == toFloor) return true;

        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(fromFloor);
        visited.Add(fromFloor);

        while (queue.Count > 0)
        {
            int currentFloor = queue.Dequeue();
            if (currentFloor == toFloor) return true;

            Floor floor = map.floors[currentFloor];
            int w = floor.config.width;
            int h = floor.config.height;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    if (c.stairTargetFloor >= 0 && c.stairIsOpen)
                    {
                        if (!monsterCanUse && c.stairHumanOnly) continue;

                        int target = c.stairTargetFloor;
                        if (target >= 0 && target < map.floors.Length && !visited.Contains(target))
                        {
                            visited.Add(target);
                            queue.Enqueue(target);
                        }
                    }
                }
            }
        }

        return false;
    }

    public bool CanReenterFloor(int floorIndex)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return false;
        if (floorIndex == 0) return true;

        Floor floor = map.floors[floorIndex];
        int w = floor.config.width;
        int h = floor.config.height;

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.roomRole == RoomRole.StartRoom && c.occupationState == OccupationState.PlayerControlled)
                    return true;
            }

        return false;
    }

    public int RecalculateGateWidth(int floorIndex, int roomA, int roomB, int actualMaxFootprint)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return -1;
        ref Floor floor = ref map.floors[floorIndex];
        if (floor.gates == null || floor.gates.Count == 0) return -1;

        int newWidth = Mathf.Clamp(actualMaxFootprint, 2, 6);

        for (int i = 0; i < floor.gates.Count; i++)
        {
            Gate g = floor.gates[i];
            if ((g.roomA == roomA && g.roomB == roomB) ||
                (g.roomB == roomA && g.roomA == roomB))
            {
                if (newWidth > g.width)
                {
                    ClosePassage(ref floor, g);

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

                    Debug.Log($"CreateMap: F{floorIndex} Gate(room{roomA}↔room{roomB}) 폭 재계산: {actualWidth}");
                    return actualWidth;
                }

                return g.width;
            }
        }

        return -1;
    }

    public List<int> FindPathAcrossFloors(int fromFloor, int toFloor, bool monsterCanUse = true)
    {
        if (map.floors == null) return new List<int>();
        if (fromFloor < 0 || fromFloor >= map.floors.Length) return new List<int>();
        if (toFloor < 0 || toFloor >= map.floors.Length) return new List<int>();
        if (fromFloor == toFloor) return new List<int> { fromFloor };

        var visited = new HashSet<int>();
        var parent = new Dictionary<int, int>();
        var queue = new Queue<int>();
        queue.Enqueue(fromFloor);
        visited.Add(fromFloor);
        parent[fromFloor] = -1;

        while (queue.Count > 0)
        {
            int currentFloor = queue.Dequeue();
            if (currentFloor == toFloor)
            {
                var path = new List<int>();
                int cur = toFloor;
                while (cur >= 0)
                {
                    path.Add(cur);
                    cur = parent[cur];
                }
                path.Reverse();
                return path;
            }

            Floor floor = map.floors[currentFloor];
            int w = floor.config.width;
            int h = floor.config.height;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    if (c.stairTargetFloor >= 0 && c.stairIsOpen)
                    {
                        if (!monsterCanUse && c.stairHumanOnly) continue;

                        int target = c.stairTargetFloor;
                        if (target >= 0 && target < map.floors.Length && !visited.Contains(target))
                        {
                            visited.Add(target);
                            parent[target] = currentFloor;
                            queue.Enqueue(target);
                        }
                    }
                }
            }
        }

        return new List<int>();
    }
}
