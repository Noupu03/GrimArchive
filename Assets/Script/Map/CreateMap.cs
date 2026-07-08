// ============================================================================
// CreateMap.cs — 메인 파일 (partial class 루트)
// ----------------------------------------------------------------------------
// 역할: CreateMap MonoBehaviour의 진입점.
//       Inspector 노출 필드, Awake/GenerateMap 호출 흐름, InitMap,
//       공용 유틸리티(ShuffleList, BfsDistances, SetRoomRole, BuildRoomRoleMap)
//       를 포함한다.
// 호출 흐름: Awake → GenerateMap → (각 partial 파일의 메서드 순차 호출)
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

public partial class CreateMap : MonoBehaviour
{
    public Map map;
    public FloorConfig[] floorConfigs;

    // true면 고정 시드, false면 매번 랜덤
    public bool useFixedSeed = true;
    public int seed = 12345;

    // 현재 생성 중인 Floor 인덱스 (Inspector/외부 참조용)
    public int currentFloorIndex = 1;

    // 검증: 최대 재시도 횟수
    public int maxRetryCount = 5;

    // 검증 결과 (Inspector/외부 참조용)
    [System.NonSerialized] public List<string> lastValidationErrors = new List<string>();
    [System.NonSerialized] public bool lastValidationPassed = false;
    [System.NonSerialized] public int lastRetryCount = 0;

    // 디버그 Gizmo 설정
    public bool showGizmos = true;
    public bool gizmoShowRoomBounds = true;
    public bool gizmoShowPassages = true;
    public bool gizmoShowStairs = true;
    public bool gizmoShowRoomLabels = true;

    private int spawnRoomId = -1;
    private Dictionary<int, HashSet<int>> roomAdjacency = new Dictionary<int, HashSet<int>>();
    private HashSet<(int, int)> connectedPairs = new HashSet<(int, int)>();
    private Dictionary<int, (int thicknessX, int thicknessY)> wallThicknessCache = new Dictionary<int, (int, int)>();
    private Dictionary<int, Dictionary<int, (int thicknessX, int thicknessY)>> perFloorWallThicknessCache = new Dictionary<int, Dictionary<int, (int, int)>>();

    // 20장/21장(방 위험도·흥미도) "탐사완료" 판정용 — 방 하나의 전체 바닥 타일 수는 맵이 재생성/
    // 재적용되기 전까지 바뀌지 않으므로, 한 번 세면 계속 재사용한다. GetRoomFloorTileCount()
    // (CreateMap.RuntimeAPI.cs)가 채우고, GenerateMap()/ApplyMap()이 무효화한다.
    private readonly Dictionary<(int floorIndex, int roomId), int> roomFloorTileCountCache = new();

    void Awake() => GenerateMap();

    public void GenerateMap()
    {
        lastRetryCount = 0;
        lastValidationErrors.Clear();
        lastValidationPassed = false;
        roomFloorTileCountCache.Clear();

        for (int attempt = 0; attempt <= maxRetryCount; attempt++)
        {
            lastRetryCount = attempt;

            try
            {
                // 시드 설정: 첫 시도는 원래 시드, 재시도는 시드 변형
                if (useFixedSeed)
                    UnityEngine.Random.InitState(seed + attempt);
                else
                    UnityEngine.Random.InitState(System.Environment.TickCount + attempt);

                RoomIdGenerator.Reset();
                spawnRoomId = -1;
                roomAdjacency.Clear();
                connectedPairs.Clear();

                // 층별 설정 초기화
                floorConfigs = FloorConfigFactory.CreateDefault();

                // ① Floor별 독립 배열 초기화
                InitMap();

                // ② ~ ⑥ 각 Floor(1~3)에 대해 방 배치 → 타일 → 연결
                for (int f = 1; f < map.floors.Length; f++)
                {
                    currentFloorIndex = f;
                    roomAdjacency.Clear();
                    connectedPairs.Clear();
                    wallThicknessCache.Clear();

                    ref Floor floor = ref map.floors[f];

                    // ── Phase 1: 방 배치 확정 (타일 작업 없이 roomId/roomRole만 결정) ──
                    PlaceBossRoom(ref floor);
                    PlaceRooms(ref floor);
                    AssignStartRoom(ref floor);
                    BuildGraph(ref floor);              // 인접 그래프 (AssignRoomRoles의 BFS에 필요)
                    AssignRoomRoles(ref floor);
                    PruneExcessRooms(ref floor);
                    EnsureSubPurposeRooms(ref floor);   // v2: Floor별 고정 개수(1/2/3) 보장

                    // ── Phase 2: 타일·벽·연결 (방 배치가 완전히 확정된 후) ──
                    BuildGraph(ref floor);              // Prune/EnsureSub 이후 최종 그래프
                    AssignTileNames(ref floor);          // 가장자리=Wall, 내부=Floor (Prune된 방은 이미 roomId==-1)
                    OpenInternalWalls(ref floor);        // 같은 roomId 청크 간 내부 벽 허물기
                    AssignFootprint(ref floor);          // Gate 폭 계산에 필요하므로 ConnectRooms 이전
                    ConnectRooms(ref floor);
                    AddLoops(ref floor);
                    RepairGateConnectivity(ref floor);   // 타일 레벨 연결 보장

                    // wallThicknessCache를 Floor별로 보관 (UpdateGateWidthsAfterStairs에서 복원)
                    perFloorWallThicknessCache[f] = new Dictionary<int, (int, int)>(wallThicknessCache);

                    // ── Phase 3: 메타데이터 초기화 ──
                    InitOccupationAndDanger(ref floor);
                    InitWeightVisibilityLandform(ref floor);
                }

                // ⑦ Floor 0 (입구/로비) 생성
                GenerateFloor0();

                // ⑧ 층간 계단 배치
                PlaceStairs();

                // ⑧-b 계단 배치 후 allowMaxFootprint가 변경된 방의 Gate 폭 갱신
                UpdateGateWidthsAfterStairs();

                // ⑨ 검증: 모든 Floor 무결성 확인
                var errors = ValidateMap();
                lastValidationErrors = errors;

                if (errors.Count == 0)
                {
                    lastValidationPassed = true;
                    LogHelper.Log(LogHelper.GAME, $"CreateMap: All floors generated. 검증 통과 (시도 {attempt + 1}회).");
                    return;
                }

                // 검증 실패 → 재시도
                LogHelper.Warning(LogHelper.GAME, $"CreateMap: 검증 실패 (시도 {attempt + 1}/{maxRetryCount + 1}). 오류 {errors.Count}건:");
                foreach (string err in errors)
                    LogHelper.Warning(LogHelper.GAME, $"  ● {err}");
            }
            catch (System.Exception ex)
            {
                // 예상치 못한 예외 발생 시 재시도로 복구
                LogHelper.Warning(LogHelper.GAME, $"CreateMap: 시도 {attempt + 1}에서 예외 발생, 재시도합니다. {ex.GetType().Name}: {ex.Message}");
                lastValidationErrors = new List<string> { $"Exception: {ex.Message}" };
            }
        }

        // 최대 재시도 초과: 마지막 결과 유지
        lastValidationPassed = false;
        LogHelper.Error(LogHelper.GAME, $"CreateMap: {maxRetryCount + 1}회 시도 후에도 검증 실패. 오류 {lastValidationErrors.Count}건 남음.");
    }

    // ① Floor별 독립 Chunks 배열 초기화
    void InitMap()
    {
        map.floors = new Floor[floorConfigs.Length];

        for (int f = 0; f < floorConfigs.Length; f++)
        {
            FloorConfig cfg = floorConfigs[f];
            int w = cfg.width;
            int h = cfg.height;

            Floor floor = new Floor();
            floor.config = cfg;
            floor.chunks = new Chunks[w, h];
            floor.gates = new System.Collections.Generic.List<Gate>();

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks c = new Chunks();
                    c.chunk = new Tile[8, 8];
                    c.landform = 0;
                    c.roomId = -1;
                    c.roomName = string.Empty;
                    c.roomRole = RoomRole.None;
                    c.floorId = (int)cfg.floorId;
                    c.occupationState = OccupationState.Neutral;
                    c.stairTargetFloor = -1;
                    c.allowMaxFootprint = 1;
                    c.stairIsOpen = false;
                    c.stairHumanOnly = false;
                    floor.chunks[x, y] = c;
                }
            }

            map.floors[f] = floor;
        }

        LogHelper.Log(LogHelper.GAME, $"CreateMap: Floors initialized — F0({floorConfigs[0].width}×{floorConfigs[0].height}), " +
                  $"F1({floorConfigs[1].width}×{floorConfigs[1].height}), " +
                  $"F2({floorConfigs[2].width}×{floorConfigs[2].height}), " +
                  $"F3({floorConfigs[3].width}×{floorConfigs[3].height})");
    }

    // ── 공용 유틸리티 ──

    // Fisher-Yates 셔플
    void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    // BFS 거리 계산 (roomAdjacency 그래프 기반)
    Dictionary<int, int> BfsDistances(int startId)
    {
        var dist = new Dictionary<int, int>();
        if (startId < 0 || !roomAdjacency.ContainsKey(startId))
            return dist;

        var queue = new Queue<int>();
        dist[startId] = 0;
        queue.Enqueue(startId);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (!roomAdjacency.ContainsKey(current)) continue;

            foreach (int neighbor in roomAdjacency[current])
            {
                if (!dist.ContainsKey(neighbor))
                {
                    dist[neighbor] = dist[current] + 1;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return dist;
    }

    // 특정 roomId의 모든 청크에 RoomRole 설정
    void SetRoomRole(ref Floor floor, int roomId, RoomRole role)
    {
        int w = floor.config.width;
        int h = floor.config.height;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (floor.chunks[x, y].roomId == roomId)
                {
                    Chunks c = floor.chunks[x, y];
                    c.roomRole = role;
                    floor.chunks[x, y] = c;
                }
            }
        }
    }

    // roomId → RoomRole 매핑 구축
    Dictionary<int, RoomRole> BuildRoomRoleMap(ref Floor floor)
    {
        int w = floor.config.width;
        int h = floor.config.height;
        var roleMap = new Dictionary<int, RoomRole>();

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                int id = floor.chunks[x, y].roomId;
                if (id >= 0 && !roleMap.ContainsKey(id))
                    roleMap[id] = floor.chunks[x, y].roomRole;
            }

        return roleMap;
    }
}
