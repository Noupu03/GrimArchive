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
using Haare.Util.Logger;

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

    // MapSaveModel(DataManager.GetModel<T>() 경유)처럼 이미 변환된 Map을 JSON 왕복 없이 바로 적용할 때 사용.
    public void ApplyMap(Map newMap)
    {
        map = newMap;
        if (map.floors != null && map.floors.Length > 0)
        {
            floorConfigs = new FloorConfig[map.floors.Length];
            for (int f = 0; f < map.floors.Length; f++)
                floorConfigs[f] = map.floors[f].config;
        }
        roomFloorTileCountCache.Clear();
    }

    // 20장/21장: 방 하나("roomId")의 전체 바닥 타일 수(벽 제외) — PersonalMapKnowledge.
    // ObserveRoomTileRevealed()가 "이 방을 다 둘러봤는지"(탐사완료 판정)를 계산할 때 쓰는
    // ground-truth 값이다. "인류 유닛은 방 전체 크기를 모른다"(20장)는 이 값을 UI/AI 판단에
    // 노출하지 않는다는 뜻으로 해석했고, 완료 판정 자체는 내부적으로 계산해야 하므로 여기서 제공한다.
    // 청크 단위가 아니라 실제 타일 단위로 세는 이유: 방 하나가 여러 청크로 이루어질 수 있고
    // (maxNormalRoomChunks), 청크 안에서도 벽 타일이 섞여 있어(외곽 두께) 청크 개수만으로는
    // 부정확하다.
    public int GetRoomFloorTileCount(int floorIndex, int roomId)
    {
        var key = (floorIndex, roomId);
        if (roomFloorTileCountCache.TryGetValue(key, out int cached)) return cached;
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length || roomId < 0) return 0;

        Floor floor = map.floors[floorIndex];
        if (floor.chunks == null) return 0;

        int count = 0;
        int w = floor.config.width, h = floor.config.height, cs = floor.config.chunkSize;
        for (int cx = 0; cx < w; cx++)
        {
            for (int cy = 0; cy < h; cy++)
            {
                Chunks c = floor.chunks[cx, cy];
                if (c.roomId != roomId || c.chunk == null) continue;

                for (int tx = 0; tx < cs; tx++)
                    for (int ty = 0; ty < cs; ty++)
                        if (c.chunk[tx, ty].name != "Wall") count++;
            }
        }

        roomFloorTileCountCache[key] = count;
        return count;
    }

    public void DeserializeMap(string json)
    {
        ApplyMap(MapSerializer.FromJson(json));
        LogHelper.Log(LogHelper.GAME, $"CreateMap: 맵 역직렬화 완료. Floors: {(map.floors != null ? map.floors.Length : 0)}");
    }

    public void SaveMapToFile(string filePath)
    {
        string json = SerializeMap();
        System.IO.File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
        LogHelper.Log(LogHelper.GAME, $"CreateMap: 맵 저장 완료 → {filePath} ({json.Length} bytes)");
    }

    public void LoadMapFromFile(string filePath)
    {
        if (!System.IO.File.Exists(filePath))
        {
            LogHelper.Error(LogHelper.GAME, $"CreateMap: 파일을 찾을 수 없습니다 — {filePath}");
            return;
        }

        string json = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
        DeserializeMap(json);
        LogHelper.Log(LogHelper.GAME, $"CreateMap: 맵 로드 완료 ← {filePath}");
    }

    // ── 런타임: 점령/후퇴 API ──

    public void ConquerRoom(int floorIndex, int roomId)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return;
        ref Floor floor = ref map.floors[floorIndex];

        SetChunksOccupation(ref floor, roomId, OccupationState.PlayerControlled);

        LogHelper.Log(LogHelper.GAME, $"CreateMap: F{floorIndex} roomId={roomId} 점령 완료.");
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
                    LogHelper.Warning(LogHelper.GAME, $"CreateMap: F{floorIndex} roomId={roomId} — 시작방은 후퇴할 수 없습니다.");
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
            LogHelper.Warning(LogHelper.GAME, $"CreateMap: F{floorIndex} roomId={roomId} — 점령/전초기지 상태가 아니므로 후퇴할 수 없습니다.");
            return;
        }

        SetChunksOccupation(ref floor, roomId, OccupationState.Neutral);

        LogHelper.Log(LogHelper.GAME, $"CreateMap: F{floorIndex} roomId={roomId} 후퇴 완료.");
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
                        int cs = c.chunk.GetLength(0);
                        for (int tx = 0; tx < cs; tx++)
                            for (int ty = 0; ty < cs; ty++)
                            {
                                Tile t = c.chunk[tx, ty];
                                t.understand = 100;
                                c.chunk[tx, ty] = t;
                            }
                    }

                    floor.chunks[x, y] = c;
                }
    }

    // 구조적 이슈 수정(2026-07-28, 사용자 요청 — 데모_구현현황_검증_2026-07-28.txt "발견된 사항 1") —
    // OffenseProcessor.TryFlipRoomOwnershipOnDeath/OnOffenseSuccess는 예전엔 Room.RoomFaction만 바꾸고
    // ChangeRoomColor로 색만 다시 칠했을 뿐, 이동 인접범위 판정(CanPlayerCommandRoom)/야생 몬스터
    // 배치 판정(GetRoomOccupationState)이 실제로 읽는 CreateMap.Chunks.occupationState(맵 데이터
    // 원본)는 그대로 남아있었다 — "전투로 방을 점령해도 그 방을 기준으로 인접방에 명령을 내릴 수
    // 없는" 불일치가 있었다. 외부(OffenseProcessor)에서 점령 전환이 일어날 때마다 이 메서드로 맵
    // 데이터도 함께 갱신한다. SetChunksOccupation과 동일한 부수효과(PlayerControlled 전환 시 이해도
    // 100 적용)를 그대로 받는다.
    public void SetRoomOccupationState(int floorIndex, int roomId, OccupationState state)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return;
        if (roomId < 0) return;
        ref Floor floor = ref map.floors[floorIndex];
        SetChunksOccupation(ref floor, roomId, state);
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
            LogHelper.Warning(LogHelper.GAME, $"CreateMap: F{floorIndex} roomId={roomId} — PlayerControlled 상태가 아니므로 전초기지를 건설할 수 없습니다.");
            return;
        }

        SetChunksOccupation(ref floor, roomId, OccupationState.Outpost);
        LogHelper.Log(LogHelper.GAME, $"CreateMap: F{floorIndex} roomId={roomId} 전초기지 건설 완료.");
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
        LogHelper.Log(LogHelper.GAME, $"CreateMap: F{floorIndex} roomId={roomId} 전초기지 해제 → PlayerControlled.");
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

        LogHelper.Log(LogHelper.GAME, $"CreateMap: F{floorIndex} → F{targetFloor} 계단 개방.");
    }

    // floorIndex 층에서 targetFloor로 연결되는 계단 타일의 대표 좌표를 찾는다 — 웨이브 사전 스폰
    // 유닛이 계단으로 걸어가서 다음 층으로 넘어가는 연출(HumanWaveManager, 2026-07-23 사용자 요청)에
    // 쓴다. PlaceStairTiles가 청크 내부 (3,4)x(3,4) 2x2 블록에 계단 타일을 찍으므로, 청크 좌상단
    // 기준 +3 오프셋을 대표 좌표로 쓴다(OpenStair와 동일한 청크 스캔 관례).
    public bool TryGetStairPosition(int floorIndex, int targetFloor, out Vector2Int pos)
    {
        pos = Vector2Int.zero;
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return false;

        Floor floor = map.floors[floorIndex];
        if (floor.chunks == null) return false;

        int w = floor.config.width;
        int h = floor.config.height;
        int cs = floor.config.chunkSize;
        int stairLo = cs / 2 - 1; // PlaceStairTiles(CreateMap.Stairs.cs)와 동일한 2x2 블록 좌상단 오프셋.

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (floor.chunks[x, y].stairTargetFloor == targetFloor)
                {
                    pos = new Vector2Int(x * cs + stairLo, y * cs + stairLo);
                    return true;
                }

        return false;
    }

    // 계단 2x2 블록(TryGetStairPosition이 돌려주는 좌표는 그 블록의 좌상단) 타일은 PlaceStairTiles가
    // isStructureExist=true로 찍어서 실제로는 유닛이 그 위에 설 수 없다(CanMove가 막음). 그런데
    // discoveredMap(A* 경로탐색이 쓰는 안개 낀 맵)은 "아직 못 본 타일"을 전부 통행 가능으로 취급해서
    // (UnitFunction.cs 시야 리빌 전까지는 실제 지형을 모름) A*가 안개 속에서 계단 타일 자체를 목적지로
    // 잡아버리고, 실제 이동(CanMove, 안개와 무관하게 진짜 지형을 봄)은 매번 거부해서 유닛이 영원히
    // 같은 실패를 반복하는 버그가 있었다(사용자 제보 콘솔 로그, 2026-07-23 — "CanMove 실패 추정"
    // 경고). 그래서 A*의 실제 이동 목적지는 계단 블록이 아니라 그 바로 옆의 실제로 밟을 수 있는
    // 타일로 잡는다 — GoapWorldState.StairArrivalRadius(반경 1) 안에서만 찾아서, "도착" 판정과 항상
    // 일치하게 한다.
    public bool TryGetStairApproachPosition(int floorIndex, int targetFloor, out Vector2Int pos) =>
        TryGetStairApproachPosition(floorIndex, targetFloor, null, out pos);

    // fromHint를 주면(유닛 현재 위치 등) 계단 블록을 둘러싼 여러 칸 중 그 위치에 가장 가까운 칸을
    // 고른다 — 힌트가 없으면(스폰 기준점 등 "대표 좌표 하나"면 충분한 용도) 항상 같은 첫 칸을 고르던
    // 예전 동작을 유지한다. 힌트 없이 항상 같은 한 칸만 골랐더니, 여러 유닛이 그 한 칸으로 전부
    // 몰려서(A*가 점유된 목표 칸 대신 근처로 우회는 하지만) 여전히 몇몇이 서로 길을 막아 강제 이동
    // 타임아웃에 걸리는 잔여 병목이 있었다(사용자 신고, 2026-07-23). 방향별로 다른 칸에 흩어지게 해서
    // 이 병목을 없앤다.
    public bool TryGetStairApproachPosition(int floorIndex, int targetFloor, Vector2Int? fromHint, out Vector2Int pos)
    {
        pos = Vector2Int.zero;
        if (!TryGetStairPosition(floorIndex, targetFloor, out Vector2Int stairPos)) return false;

        Vector2Int? best = null;
        int bestDist = int.MaxValue;

        // 반경 1(계단 블록을 둘러싼 테두리 한 칸)만 훑는다 — GoapWorldState.StairArrivalRadius와
        // 반드시 같은 값이어야 "여기 도착 = atStairs 만족"이 항상 성립한다.
        for (int dx = -1; dx <= 2; dx++)
        {
            for (int dy = -1; dy <= 2; dy++)
            {
                if (dx >= 0 && dx <= 1 && dy >= 0 && dy <= 1) continue; // 블록 내부(실제 계단 타일)는 제외

                Vector2Int cand = new Vector2Int(stairPos.x + dx, stairPos.y + dy);
                if (!IsStaticTileWalkable(floorIndex, cand)) continue;

                if (!fromHint.HasValue)
                {
                    pos = cand;
                    return true;
                }

                Vector2Int diff = cand - fromHint.Value;
                int dist = Mathf.Abs(diff.x) + Mathf.Abs(diff.y);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = cand;
                }
            }
        }

        if (best.HasValue)
        {
            pos = best.Value;
            return true;
        }

        pos = stairPos; // 못 찾으면(사실상 없음) 예전처럼 블록 좌표라도 반환 — 호출부가 방어적으로 처리
        return false;
    }

    // 계단 블록을 둘러싼 반경 1칸(GoapWorldState.StairArrivalRadius) 중 정적으로 밟을 수 있는(벽/
    // 구조물 아닌) 타일을 전부 반환한다 — 사용자 요청(2026-07-23) "그냥 계단 근방 1타일 모두
    // 이동하게 해줘": 특정 한 칸을 고집하지 않고, 이 후보들 중 그때그때 비어있는 칸으로 자유롭게
    // 흩어지게 하기 위함이다(실제 유닛 점유 여부는 이 메서드가 모르므로 호출부(Action_MoveToStairs)가
    // 매 틱 골라 쓴다 — 정적 지형 후보 목록 자체는 안 바뀌므로 여기선 그대로 둔다).
    public bool TryGetStairApproachCandidates(int floorIndex, int targetFloor, out List<Vector2Int> candidates)
    {
        candidates = new List<Vector2Int>();
        if (!TryGetStairPosition(floorIndex, targetFloor, out Vector2Int stairPos)) return false;

        for (int dx = -1; dx <= 2; dx++)
        {
            for (int dy = -1; dy <= 2; dy++)
            {
                if (dx >= 0 && dx <= 1 && dy >= 0 && dy <= 1) continue; // 블록 내부(실제 계단 타일)는 제외

                Vector2Int cand = new Vector2Int(stairPos.x + dx, stairPos.y + dy);
                if (IsStaticTileWalkable(floorIndex, cand)) candidates.Add(cand);
            }
        }

        return candidates.Count > 0;
    }

    // discoveredMap(안개)과 무관하게 실제 지형(Wall/isStructureExist)만으로 통행 가능 여부를 판정한다.
    // UnitFunction.CanMove와 같은 판정 기준이지만 특정 유닛(footprint/점유 유닛)에 묶이지 않은,
    // "이 타일 자체가 구조적으로 막혀있는가"만 보는 정적 버전이다. GameSession.FindNearbyFreeObjectTile
    // (시체 재배치 링 탐색)이 벽 타일을 걸러내려고 그대로 재사용하기 때문에 public으로 노출한다
    // (사용자 신고, 2026-07-23 "시체 벽에 생기는거 막아줘" — 링 탐색이 objectGrid 점유 여부만 보고
    // 벽인지는 확인하지 않아서, 좁은 통로에서 죽으면 시체가 벽 타일에 놓이는 경우가 있었다).
    public bool IsStaticTileWalkable(int floorIndex, Vector2Int p)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return false;
        if (p.x < 0 || p.y < 0) return false;

        Floor floor = map.floors[floorIndex];
        if (floor.chunks == null) return false;

        int cs = floor.config.chunkSize;
        int cx = p.x / cs, tx = p.x % cs;
        int cy = p.y / cs, ty = p.y % cs;
        if (cx >= floor.config.width || cy >= floor.config.height) return false;

        Chunks c = floor.chunks[cx, cy];
        if (c.roomId == -1 || c.chunk == null) return false;

        Tile tile = c.chunk[tx, ty];
        return tile.name != "Wall" && !tile.isStructureExist;
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

    // 점령 관련(2026-07-27 신규) — 이동 명령을 낼 좌표가 어느 방(roomId)에 속하는지 조회한다.
    // IsStaticTileWalkable과 동일한 타일→청크 변환(8칸 단위)을 재사용한다.
    public int GetRoomIdAt(int floorIndex, Vector2Int tilePos)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return -1;
        if (tilePos.x < 0 || tilePos.y < 0) return -1;

        Floor floor = map.floors[floorIndex];
        if (floor.chunks == null) return -1;

        int cs = floor.config.chunkSize;
        int cx = tilePos.x / cs, cy = tilePos.y / cs;
        if (cx >= floor.config.width || cy >= floor.config.height) return -1;

        return floor.chunks[cx, cy].roomId;
    }

    // 점령 관련(2026-07-27 신규) — "플레이어는 자신 소유 및 양옆 방까지만 이동 명령을 내릴 수 있음"
    // 요구사항. CanCommandEnemyRoom(playerRoomId 하나만 확인)과 달리, 현재 플레이어(몬스터 진영)가
    // 점령 중인 모든 방을 한 번에 스캔해 그중 하나라도 targetRoomId 자신이거나 Gate로 연결돼 있으면
    // 허용한다.
    public bool CanPlayerCommandRoom(int floorIndex, int targetRoomId)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return false;
        if (targetRoomId < 0) return false;
        Floor floor = map.floors[floorIndex];
        if (floor.chunks == null) return false;

        int w = floor.config.width;
        int h = floor.config.height;

        var ownedRoomIds = new HashSet<int>();
        bool targetIsOwned = false;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.occupationState != OccupationState.PlayerControlled) continue;
                ownedRoomIds.Add(c.roomId);
                if (c.roomId == targetRoomId) targetIsOwned = true;
            }

        if (targetIsOwned) return true;
        if (floor.gates == null) return false;

        foreach (Gate g in floor.gates)
        {
            if ((ownedRoomIds.Contains(g.roomA) && g.roomB == targetRoomId) ||
                (ownedRoomIds.Contains(g.roomB) && g.roomA == targetRoomId))
                return true;
        }
        return false;
    }

    // 위 둘을 합친 편의 메서드 — InputManager가 클릭 좌표 하나로 바로 판정할 때 사용.
    public bool CanPlayerCommandPosition(int floorIndex, Vector2Int tilePos)
        => CanPlayerCommandRoom(floorIndex, GetRoomIdAt(floorIndex, tilePos));

    // 야생 몬스터 A 배치(2026-07-27 신규) — 방 하나는 항상 단일 점령상태를 가지므로(InitOccupationAndDanger/
    // GenerateFloor0이 방 전체에 같은 값을 씀) 첫 매치만 반환해도 충분하다.
    public OccupationState GetRoomOccupationState(int floorIndex, int roomId)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return OccupationState.Neutral;
        Floor floor = map.floors[floorIndex];
        if (floor.chunks == null) return OccupationState.Neutral;

        int w = floor.config.width;
        int h = floor.config.height;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (floor.chunks[x, y].roomId == roomId)
                    return floor.chunks[x, y].occupationState;

        return OccupationState.Neutral;
    }

    // 점령 관련(2026-07-27 신규) — "플레이어는 자신 소유의 방에만 몬스터를 스폰할 수 있음". 이동
    // 명령(CanPlayerCommandRoom)과 달리 인접 방까지 허용하지 않고 정확히 점령 중인 방인지만 본다.
    public bool IsPositionPlayerOwned(int floorIndex, Vector2Int tilePos)
    {
        if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return false;
        if (tilePos.x < 0 || tilePos.y < 0) return false;

        Floor floor = map.floors[floorIndex];
        if (floor.chunks == null) return false;

        int cs = floor.config.chunkSize;
        int cx = tilePos.x / cs, cy = tilePos.y / cs;
        if (cx >= floor.config.width || cy >= floor.config.height) return false;

        return floor.chunks[cx, cy].occupationState == OccupationState.PlayerControlled;
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

    // FindPathAcrossFloors(아래)와 동일한 층간 계단 BFS를 중복 구현하고 있었다(2026-08-20 통합) —
    // 경로 자체가 필요 없는 호출부라도 그 결과의 존재 여부만 보면 되므로, 실제 층 수(BFS 큐 규모)가
    // 작아 경로 리스트 할당 비용이 무시할 만한 수준이라 그대로 위임한다.
    public bool CanReachFloor(int fromFloor, int toFloor, bool monsterCanUse = true)
        => FindPathAcrossFloors(fromFloor, toFloor, monsterCanUse).Count > 0;

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

                    LogHelper.Log(LogHelper.GAME, $"CreateMap: F{floorIndex} Gate(room{roomA}↔room{roomB}) 폭 재계산: {actualWidth}");
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
