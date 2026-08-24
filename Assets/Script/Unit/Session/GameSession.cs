using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using VContainer;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Haare.Util.Logger;
using GrimArchive.Wave;
using Haare.Scripts.Client.Data;
using R3;

// Haare의 Processer/Routine 시스템으로 턴 처리 루프를 옮김: 평범한 Unity Update() 대신
// NativeRoutine.UpdateProcess()가 Processor의 등록된 Routine 순회를 통해 매 프레임 호출된다.
// 인스펙터 데이터가 전혀 없어서(디버그 텍스처 뷰 제거 후) 씬 GameObject일 필요가 없는 순수 C# 클래스.

public class GameSession : NativeRoutine, IOffenseQuery
{
    public Subject<(Unit attacker, ThreatTileData threat)> OnThreatCreated = new Subject<(Unit attacker, ThreatTileData threat)>();
    public static GameSession Instance { get; private set; }

    public CreateMap cmap { get; private set; }
    public UnitGenerate unitGenerate => _unitGenerate;
    public MapManager mapManager => _mapManager;
    // MapRandering과 WaveSpawner는 하위 호환성을 위해 MapManager를 통해 노출
    public MapRandering mapRandering => mapManager?.mapRandering;

    [Inject]
    public HumanWaveManager humanWaveManager;

    private UnitGenerate _unitGenerate;
    private ThreatTileRenderer _threatTileRenderer;
    private PropagationDebugVisualizer _propagationDebugVisualizer;
    private IObjectResolver _resolver;

    private DataManager _dataManager;
    [Inject] public MapManager _mapManager { get; set; }
    [Inject] public OffenseProcessor _offenseProcessor { get; set; }
    public OffenseProcessor OffenseProcessor => _offenseProcessor;
    [Inject] public DefenseProcessor _defenseProcessor { get; set; }
    public DefenseProcessor DefenseProcessor => _defenseProcessor;
    private UnitRegistry _unitRegistry;
    private ObjectSpawner _objectSpawner;
    private Dictionary<InteractableObject, GameObject> objectVisuals = new Dictionary<InteractableObject, GameObject>();
    private PartyService _partyService;
    private CombatEventService _combatEventService;
    private BuildingManager _buildingManager;
    private DoorSystem _doorSystem;
    private FogOfWarSystem _fogOfWarSystem;

    [Inject]
    public void Construct(UnitGenerate unitGenerate, ThreatTileRenderer threatTileRenderer, PropagationDebugVisualizer propagationDebugVisualizer, IObjectResolver resolver, CreateMap injectedMap, DataManager dataManager, UnitRegistry unitRegistry, ObjectSpawner objectSpawner, PartyService partyService, CombatEventService combatEventService, BuildingManager buildingManager, DoorSystem doorSystem, FogOfWarSystem fogOfWarSystem)
    {
        _unitGenerate = unitGenerate;
        _threatTileRenderer = threatTileRenderer;
        _propagationDebugVisualizer = propagationDebugVisualizer;
        _resolver = resolver;
        cmap = injectedMap;
        _dataManager = dataManager;
        _unitRegistry = unitRegistry;
        _objectSpawner = objectSpawner;
        _partyService = partyService;
        _combatEventService = combatEventService;
        _buildingManager = buildingManager;
        _doorSystem = doorSystem;
        _fogOfWarSystem = fogOfWarSystem;
        Instance = this;

        OnThreatCreated.Subscribe(data =>
        {
            // 시전이 있는 공격(castTimer > 0)은 "예고"다 — 범위가 뜬 시점부터 피해가 들어가는 순간까지
            // 옅어지지 않고 그대로 남아있어야 한다(holdUntilImpact). 시전 없는 즉발 공격은 표시 시점에
            // 이미 피해가 끝나 있으므로 종전대로 0.5초짜리 잔상으로 그린다.
            bool isTelegraph = data.attacker != null && data.attacker.CombatState.State.castTimer > 0f;
            float duration = isTelegraph ? data.attacker.CombatState.State.castTimer : 0.5f;
            _threatTileRenderer?.ShowThreatZone(data.attacker, data.threat, duration, isTelegraph);

            // GetEnemiesInHitbox는 static 공유 리스트를 반환하므로, OnReactToThreat 내부 콜체인이
            // 다시 GetEnemiesInHitbox를 호출해 리스트를 초기화하기 전에 복사본을 만들어 iterate한다.
            // 2026-07-31 GC 최적화 — 공격마다 new List<Unit>()를 할당하던 것을 재사용 버퍼로 교체.
            _threatSnapshotBuffer.Clear();
            _threatSnapshotBuffer.AddRange(SkillAction.GetEnemiesInHitbox(data.attacker, data.threat.hitbox));
            foreach (var u in _threatSnapshotBuffer)
            {
                u.OnReactToThreat(data.attacker, data.threat);
            }
        });
    }
    public Dictionary<Vector3Int, Unit> unitGrid => _unitRegistry?.unitGrid;
    public Dictionary<Vector3Int, InteractableObject> objectGrid => _objectSpawner.objectGrid;
    public Dictionary<Vector3Int, Room> roomGrid { get; private set; } = new Dictionary<Vector3Int, Room>();
    public List<Room> allRooms { get; private set; } = new List<Room>();

    // 방마다 "현재/최대 인구수" world-space 라벨(카메라 무관, 맵에 고정). 예전엔 독립 최상위
    // GameObject("RoomPopulationLabels")를 필드 초기화 시점에 만들어 썼는데, 사용자 요청(2026-07-28
    // "RoomPopulationLabels가 계층상, MapRoot_Grid 아래에 들어가야 할거 같아. 따로 오브젝트로 존재할
    // 이유가 없음")으로 층별 라벨 그룹(GetFloorCategoryGroup(floor, "Labels"))에 흡수됐다. 개별 라벨
    // 이름("RoomPopLabel_")은 그대로라 Assets/Editor/RoomPopulationLabelCleanup.cs의 재귀 탐색
    // (부모가 뭐든 이름만 보고 청소)에는 영향 없음.
    private readonly Dictionary<Room, TextMesh> _roomPopulationLabels = new Dictionary<Room, TextMesh>();
    // E: string 비교 대신 int 쌍 비교로 교체 — $"..." 보간 문자열을 값이 바뀔 때만 생성
    private readonly Dictionary<Room, (int pop, int max)> _roomPopulationLabelText = new Dictionary<Room, (int, int)>();

    // L: 호출마다 new List<Unit>() 할당하던 것을 static 캐시로 교체 — 반환값은 즉시 소비할 것
    private static readonly List<Unit> _unitsInRoomResult = new List<Unit>();

    // 2026-07-31 GC 최적화 — OnThreatCreated 핸들러(생성자 참고)가 공격마다 new List<Unit>()로
    // GetEnemiesInHitbox 결과를 복사하던 것을 재사용 버퍼로 교체.
    private static readonly List<Unit> _threatSnapshotBuffer = new List<Unit>();
    public IReadOnlyList<Unit> GetUnitsInRoom(RectInt bounds)
    {
        _unitsInRoomResult.Clear();
        foreach (var u in units)
        {
            if (bounds.Contains(u.position))
                _unitsInRoomResult.Add(u);
        }
        return _unitsInRoomResult;
    }

    public List<Unit> units { get; private set; } = new List<Unit>();
    // ①: isCastingAttack=true인 유닛만 모아두는 집합 — DetectThreats가 전체 units 대신 이걸 순회.
    public readonly HashSet<Unit> castingUnits = new HashSet<Unit>();

    // 뭉침 완화(2026-07-31 프로파일러 분석): actionCooldown 소진 판정에 쓰는 Time.deltaTime이 Unity
    // 기본 클램프(TimeManager Maximum Allowed Timestep=0.333초)의 영향을 받는데, 유닛 행동 주기
    // (1/walkSpeed)도 대략 0.28~0.4초로 같은 자릿수다. 그래서 로딩 직후 등 단 한 프레임만 느려져도
    // 거의 모든 유닛의 쿨다운이 그 한 프레임에 동시에 0 이하로 떨어져 한꺼번에 처리된다 — 특히
    // NavigationFSMState.FindNearestUnexploredTarget(배회 유닛의 미탐사 타겟 탐색)처럼 콜당 비용이
    // 있는 경로가 몰리면 그 프레임이 또 느려져 다음 프레임에도 뭉침이 재생산된다.
    // 전투/전술/플레이어 명령 중인 유닛은 반응성이 중요하므로 즉시 처리하고, 그 외(주로 배회/탐색)
    // 유닛만 프레임당 처리 상한을 둔 큐로 미뤄 스파이크를 여러 프레임에 걸쳐 분산시킨다.
    private readonly Queue<Unit> _throttledActionQueue = new Queue<Unit>();
    private readonly HashSet<Unit> _queuedForThrottledAction = new HashSet<Unit>();
    // 튜닝값 — 값이 클수록 뭉침 분산 효과가 줄고, 작을수록 배회 유닛의 반응(다음 목적지 결정 등)이
    // 더 늦어진다. 실측 후 조정할 것.
    private const int MaxThrottledUnitActionsPerFrame = 30;
    public List<Party> parties => _partyService.parties;

    public float currentGameSpeed = 1f;
    public bool isPaused = false;

    // 2026-08-22 사용자 신고 "난전중 겹침... 어떤 상황에서도 유닛끼리는 겹쳐지면 안돼" — 반환값
    // (bool)으로 등록 성공 여부를 알려준다(UnitRegistry.RegisterUnitPos 참고, 이미 다른 유닛이
    // 점유 중이면 false). 실패 시엔 그 유닛의 실제 위치가 바뀌지 않은 것이므로 소리 감지 인덱스도
    // 건드리지 않는다 — 호출부(ProcessUnitAction/Unit.ForceMove)가 이 반환값으로 위치 변경 자체를
    // 되돌린다.
    public bool RegisterUnitPos(Unit u, Vector2Int pos)
    {
        bool ok = _unitRegistry.RegisterUnitPos(u, pos);
        if (ok)
        {
            // 07문서 소리 스캔 최적화(2026-08-05) — PropagationSystem이 "방별 소리 감지자"만 훑을 수
            // 있도록, 유닛 그리드와 동일한 지점(스폰/이동)에서 방 인덱스도 함께 갱신한다. 2026-08-06:
            // 07문서 1장 "소리 감지: 인류/몬스터 모두 적용" 검증 중 몬스터가 이 인덱스에서 빠져 있던
            // 갭을 발견해 Human 전용에서 모든 Unit으로 확장했다(전파는 여전히 인류 전용 — 이 인덱스는
            // "소리를 들을 수 있는지"만 판단하고, 전파 가능 여부는 PropagationSystem의 별도 함수가
            // 여전히 Human으로 게이팅한다).
            PropagationSystem.UpdateListenerRoomIndex(u, u.currentFloor, cmap != null ? cmap.GetRoomIdAt(u.currentFloor, pos) : -1);
        }
        return ok;
    }

    public void UnregisterUnitPos(Unit u, Vector2Int pos)
    {
        _unitRegistry.UnregisterUnitPos(u, pos);
        PropagationSystem.RemoveFromRoomIndex(u);
    }

    public GameSession()
    {
        

        // InputManager/UIManager/ThreatTileRenderer는 이제 GameCompositionRoot(VContainer)가 배선한다.
    }

    // NativeRoutine 생명주기: Processor 등록 완료 후 한 번 호출됨 (예전 Start()와 동일한 역할)
        public override async UniTask Initialize(CancellationToken cts)
    {
        Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, "GameSession: Initialize START");
        try {
            // UI 리팩토링(2026-08-20) — UIManager가 Haare ICustomPanel로 편입되면서 VContainer에
            // 더는 등록되지 않는다(GameUIPresenter.BootSequence가 로드를 담당). 여기서 강제로
            // Resolve<UIManager>()하던 코드는 이제 미등록 타입이라 예외만 던지므로 제거.

            TextAsset mapTextAsset = Resources.Load<TextAsset>("Data/map");
            if (mapTextAsset != null && !string.IsNullOrEmpty(mapTextAsset.text))
            {
                cmap.DeserializeMap(mapTextAsset.text);
                Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, "GameSession: Map deserialized from Data/map.");
                // 기존 맵 데이터 보정(2026-07-28, 사용자 요청 "초기 점령 방 중 2층, 3층은 시작방...
                // 야생으로 남겨주고") — CreateMap.Stairs.cs의 InitOccupationAndDanger는 "새로 생성할
                // 때"만 2층 이상 시작방을 Neutral로 만든다. 이미 저장된 Resources/Data/map.json(맵을
                // 다시 생성하지 않고 그대로 불러 쓰는 기존 스냅샷)에는 예전 로직(전 층 시작방=
                // PlayerControlled)이 그대로 박혀있을 수 있어, 시각화/방 그리드 구성 전에 여기서 다시
                // 한번 강제로 바로잡는다 — 맵을 재생성하지 않아도 항상 올바른 상태가 되도록.
                EnforceFloor2And3StartRoomsAreWild();
                _mapManager.SetupAndVisualizeMap(cmap);
                Unit.humanFactionData.InitMap(cmap);
                Unit.monsterFactionData.InitMap(cmap);
                BuildRoomGrid();
                // 문 시스템(2026-07-27, 사용자 요청): 모든 방과 방 사이 통로에 문을 깔아둔다. 다른
                // 오브젝트보다 먼저 실행해야 "문이 있는 곳엔 다른 오브젝트가 안 생기게"가 성립한다
                // (SpawnObject가 objectGrid에 이미 오브젝트가 있으면 조용히 스킵하는 걸 그대로 이용).
                _doorSystem.SpawnDoors();
                // 점령 관련(2026-07-27 신규): 모든 야생 방에 야생 몬스터 A 2마리씩 필수 배치.
                SpawnWildRoomGuards();
                // 코어 전면 개편(기초문서.md 피드백, 2026-08-22) — 모든 방에 코어를 하나씩 자동
                // 생성한다(이전엔 보스방 1개 한정). HumanWaveManager는 WaveData.targetRoomRole/
                // targetRoomId로 지정된 방의 Room.CorePosition을 직접 목표로 삼는다.
                // 안개 시스템 초기화 (2026-08-22 코어/건물이 횃불 자리 뺏지 않게 먼저 스폰)
                _fogOfWarSystem.Initialize();
                _fogOfWarSystem.SpawnTorches();

                SpawnAllRoomCores();
                // 자원/유닛 생산 건물(MVP, 2026-07-27)
                SpawnInitialBuildings();
                Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, "GameSession: 맵 데이터 로드 성공.");
            }
            else
            {
                Haare.Util.Logger.LogHelper.Error(Haare.Util.Logger.LogHelper.GAME, "GameSession: 맵 데이터 로드 실패 (Resources/Data/map.json 파일이 없습니다). Tools -> Map Generator에서 먼저 맵을 생성해주세요.");
            }
        }
        catch (System.Exception e)
        {
            Haare.Util.Logger.LogHelper.Error(Haare.Util.Logger.LogHelper.GAME, $"GameSession: Initialize 중 예외: {e}");
        }

        await base.Initialize(cts);
        Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, "GameSession: Initialize END");
    }

    // 종료 시퀀스 안전장치(2026-08-20, 사용자 신고 "게임 실행 종료시 안전 destroy 검사... roomlabel
    // 파괴가 꼬이는것 같이 보임") — Finalize()/OnApplicationQuit() 중 하나가 먼저 라벨을 Destroy해서
    // _roomPopulationLabels를 비워도, UpdateProcess()는 Processor 등록 해제 타이밍과 별개로 그 뒤에도
    // 한두 프레임 더 돌 수 있다(Dispose 순서가 엄격히 보장되지 않음). 그러면 RefreshRoomPopulationLabels
    // 가 "라벨이 없네" 하고 CreateRoomPopulationLabel로 새로 만들어버리는데, 그 시점엔 부모로 쓸 층별
    // 타일맵/그룹(GetFloorCategoryGroup)이 이미 같이 파괴되고 있는 중일 수 있어 고아 GameObject가
    // 생기거나 파괴 순서가 뒤엉킨 것처럼 보인다. 종료가 시작되면 이 플래그로 UpdateProcess() 전체를
    // 끊어서 더 이상 아무것도 새로 만들지 않게 한다.
    private bool _isShuttingDown;

    // NativeRoutine 생명주기: Processor에서 UnRegister될 때(Dispose()→Finalize(), VContainer 컨테이너
    // 파괴 시) 한 번 호출됨 — ResourceManager/BuildingManager와 동일 관례. NativeRoutine은 OnDestroy가
    // 없어서 런타임에 만든 GameObject는 여기서 직접 정리해야 한다.
    public override async UniTask Finalize()
    {
        _isShuttingDown = true;
        ClearRoomPopulationLabels();
        await base.Finalize();
    }

    // 기존 맵 데이터 보정(2026-07-28, 사용자 요청 "초기 점령 방 중 2층, 3층은 시작방 플레이어 몬스터
    // 진영에게 점령되는게 아닌, 야생으로 남겨주고") — CreateMap.Stairs.cs.InitOccupationAndDanger는
    // "새로 생성할 때"만 2층 이상 시작방을 Neutral로 만든다. 이미 저장된 Resources/Data/map.json(맵을
    // 다시 생성하지 않고 그대로 불러 쓰는 기존 스냅샷)에는 예전 로직(전 층 시작방=PlayerControlled)이
    // 그대로 박혀있을 수 있어, GameSession.Initialize()가 역직렬화 직후·시각화/방 그리드 구성 전에
    // 호출해 다시 한번 강제로 바로잡는다 — 맵을 재생성하지 않아도 항상 올바른 상태가 되도록.
    private void EnforceFloor2And3StartRoomsAreWild()
    {
        if (cmap == null || cmap.map.floors == null) return;

        for (int floorIdx = 2; floorIdx < cmap.map.floors.Length; floorIdx++)
        {
            Floor floor = cmap.map.floors[floorIdx];
            if (floor.chunks == null) continue;

            int w = floor.config.width, h = floor.config.height;
            for (int cx = 0; cx < w; cx++)
            {
                for (int cy = 0; cy < h; cy++)
                {
                    Chunks c = floor.chunks[cx, cy];
                    if (c.roomRole != RoomRole.StartRoom) continue;
                    if (c.occupationState != OccupationState.PlayerControlled) continue;

                    c.occupationState = OccupationState.Neutral;
                    floor.chunks[cx, cy] = c;
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // 계층 정리(2026-07-28, 사용자 요청 "RoomPopulationLabels가 계층상, MapRoot_Grid 아래에 들어가야
    // 할거 같아. 따로 오브젝트로 존재할 이유가 없음. 그리고 각 층에 자식으로 할당된 오브젝트들을 좀
    // 유닛이면 유닛, 라벨이면 라벨, 문이면 문, 안개면 안개끼리 묶어서 나타나게 해줘") — 그동안 문/
    // 트랩·시체·루팅·코어/안개/횃불/건물/유닛이 전부 F{n}_Tilemap 바로 아래 뒤섞여 flat하게 매달려
    // 있었다. 층별·종류별 하위 그룹 GameObject(Units/Labels/Doors/Objects/Fog/Torches/Buildings)를
    // 만들어 그 아래로 모은다. (floorIdx, category) 조합마다 한 번만 만들고 캐시해서 재사용 —
    // GameObject.Find/Transform.Find 반복 호출을 피한다.
    // ══════════════════════════════════════════════════════════════════════
    private readonly Dictionary<(int floor, string category), Transform> _floorCategoryGroups = new Dictionary<(int, string), Transform>();

    public Transform GetFloorCategoryGroup(int floorIdx, string category)
    {
        var key = (floorIdx, category);
        if (_floorCategoryGroups.TryGetValue(key, out Transform cached) && cached != null) return cached;

        Transform floorRoot = null;
        if (mapRandering != null && mapRandering.floorTilemaps != null && floorIdx >= 0 && floorIdx < mapRandering.floorTilemaps.Length)
        {
            var tilemap = mapRandering.floorTilemaps[floorIdx];
            if (tilemap != null) floorRoot = tilemap.transform;
        }
        if (floorRoot == null) return null;

        Transform existing = floorRoot.Find(category);
        if (existing != null)
        {
            _floorCategoryGroups[key] = existing;
            return existing;
        }

        GameObject groupGo = new GameObject(category);
        groupGo.transform.SetParent(floorRoot, false);
        _floorCategoryGroups[key] = groupGo.transform;
        return groupGo.transform;
    }

    // 유닛 배치 시스템(2026-07-27 신규) 5.1장 — 방 최대 인구수 = 청크 수 × 이 값(문서에 수치가 없어
    // 사용자 확인대로 "방 크기 비례" 공식 채택, 2026-07-27 사용자 요청으로 6→4 조정).
    private const int PopulationPerChunk = 4;

    public void BuildRoomGrid()
    {
        if (cmap == null || cmap.map.floors == null) return;
        roomGrid.Clear();
        allRooms.Clear();
        ClearRoomPopulationLabels();
        Dictionary<int, Room> generatedRooms = new Dictionary<int, Room>();

        // 룸의 경계(Bounds)를 계산하기 위한 변수
        Dictionary<int, Vector2Int> roomMin = new Dictionary<int, Vector2Int>();
        Dictionary<int, Vector2Int> roomMax = new Dictionary<int, Vector2Int>();
        Dictionary<int, int> roomChunkCount = new Dictionary<int, int>();

        // 2026-07-27 확장 — 기존엔 "int currentFloor = 1" 고정이라 오펜스/야생몬스터 관련 Room이
        // 1층에서만 만들어졌다. 야생 몬스터 A를 모든 층의 야생 방에 배치해야 해서 전체 층을 순회하도록
        // 확장한다. RoomIdGenerator.GetNextId()가 전역 카운터라(층마다 리셋 안 됨) roomId는 항상
        // 층을 넘나들어도 고유하므로 이 딕셔너리들을 층 사이에 공유해도 충돌하지 않는다.
        for (int currentFloor = 0; currentFloor < cmap.map.floors.Length; currentFloor++)
        {
            Floor floor = cmap.map.floors[currentFloor];
            if (floor.chunks == null) continue;

            int chunkW = floor.config.width;
            int chunkH = floor.config.height;

            for (int cx = 0; cx < chunkW; cx++)
            {
                for (int cy = 0; cy < chunkH; cy++)
                {
                    Chunks c = floor.chunks[cx, cy];
                    if (c.roomId >= 0)
                    {
                        if (!generatedRooms.TryGetValue(c.roomId, out Room room))
                        {
                            room = new Room
                            {
                                RoomName = string.IsNullOrEmpty(c.roomName) ? $"Room {c.roomId}" : c.roomName,
                                RoomId = c.roomId,
                                Floor = currentFloor,
                                // 구조적 이슈 수정(2026-07-28, 사용자 요청 — 데모_구현현황_검증_2026-07-28.txt
                                // "발견된 사항 2") — 예전엔 Room.RoomFaction이 항상 클래스 기본값(Wild)으로
                                // 시작해서, 0층(HumanControlled)/1층 시작방(PlayerControlled)도 Room 객체
                                // 기준으로는 생성 직후 "야생"으로 취급됐다. CreateMap.Chunks.occupationState
                                // (맵 데이터 원본, 방 하나는 항상 단일 값)를 그대로 반영해 초기값부터 일치시킨다.
                                RoomFaction = MapOccupationStateToFaction(c.occupationState),
                            };
                            generatedRooms[c.roomId] = room;
                            allRooms.Add(room);

                            roomMin[c.roomId] = new Vector2Int(int.MaxValue, int.MaxValue);
                            roomMax[c.roomId] = new Vector2Int(int.MinValue, int.MinValue);
                            roomChunkCount[c.roomId] = 0;
                        }
                        roomChunkCount[c.roomId]++;

                        int cs = floor.config.chunkSize;
                        int startX = cx * cs;
                        int startY = cy * cs;

                        var min = roomMin[c.roomId];
                        var max = roomMax[c.roomId];
                        min.x = Mathf.Min(min.x, startX);
                        min.y = Mathf.Min(min.y, startY);
                        max.x = Mathf.Max(max.x, startX + cs);
                        max.y = Mathf.Max(max.y, startY + cs);
                        roomMin[c.roomId] = min;
                        roomMax[c.roomId] = max;

                        for (int tx = 0; tx < cs; tx++)
                        {
                            for (int ty = 0; ty < cs; ty++)
                            {
                                Vector3Int pos = new Vector3Int(cx * cs + tx, cy * cs + ty, currentFloor);
                                roomGrid[pos] = room;
                            }
                        }
                    }
                }
            }
        }

        // 최종적으로 각 룸에 Bounds/MaxPopulation 할당
        foreach (var kvp in generatedRooms)
        {
            int rid = kvp.Key;
            Room r = kvp.Value;
            var min = roomMin[rid];
            var max = roomMax[rid];
            r.Bounds = new RectInt(min.x, min.y, max.x - min.x, max.y - min.y);
            r.MaxPopulation = roomChunkCount[rid] * PopulationPerChunk;
        }

        LogHelper.Log(LogHelper.GAME, $"BuildRoomGrid: 전체 {cmap.map.floors.Length}개 층에서 방 {generatedRooms.Count}개 생성됨.");
    }

    // BuildRoomGrid 전용(2026-07-28) — CreateMap.Chunks.occupationState → Room.RoomFaction 매핑.
    // Outpost/Occupied는 OffenseProcessor.MapToRoomFaction 쪽 FactionType 값과 1:1 대응이 없어(Outpost는
    // "PlayerControlled 이후 인류가 거점화한 상태"이므로 Player로, 실사용 안 되는 Occupied는 Wild로 폴백)
    // 안전한 값으로 근사한다.
    private static FactionType MapOccupationStateToFaction(OccupationState state) => state switch
    {
        OccupationState.PlayerControlled => FactionType.Player,
        OccupationState.Outpost => FactionType.Player,
        OccupationState.HumanControlled => FactionType.Human,
        _ => FactionType.Wild,
    };

    // 안개 시스템(2026-07-28 구현, 2026-08-20 분리) — 안개 스폰/해제/문 통합 셰도우 재계산 전체를
    // DoorSystem과 동일한 이유·같은 패턴으로 FogOfWarSystem(Assets/Script/Unit/Session/)으로 뺐다
    // (횃불도 안개 해제 타이밍에 강하게 결합돼 있어 같은 클래스로 함께 옮김 — 아래 SpawnTorches 위치
    // 참고). 아래는 외부에서 GameSession.Instance.X() 형태로 호출하던 기존 진입점을 유지하기 위한
    // 얇은 위임이다.
    public void RevealRoomFog(Room room) => _fogOfWarSystem.RevealRoomFog(room);
    public void RevealFogAroundCapturedRoom(Room room) => _fogOfWarSystem.RevealFogAroundCapturedRoom(room);


    // 2026-07-27 신규 — "모든 야생 진영 방에 야생 몬스터 A 2마리씩 필수 배치(위치는 랜덤), 방 밖으로
    // 나갈 수 없음" 요구사항. BuildRoomGrid() 직후(Initialize 참고) 한 번 호출한다. 야생 여부는
    // CreateMap.Chunks.occupationState(Neutral=야생)로 판정한다 — Room.RoomFaction(오펜스 시스템)과는
    // 별개의 개념이라 건드리지 않는다. WildBaseSpawnerComponent.SpawnMonster와 동일한 스폰 패턴
    // (랜덤 위치 + IsAreaClear 재시도 + WildMonsterBehavior/RoomConfinedMovement 부여)을 재사용한다.
    private const int WildRoomGuardCount = 2;

    public void SpawnWildRoomGuards()
    {
        if (cmap == null || _unitGenerate == null || allRooms == null) return;

        foreach (var room in allRooms)
        {
            if (room.RoomId < 0 || room.Floor < 0) continue;
            if (room.Floor == 0) continue; // 사용자 요청(2026-07-27): 0층(인류 소유 로비)에는 생성 금지.
            if (cmap.GetRoomOccupationState(room.Floor, room.RoomId) != OccupationState.Neutral) continue;

            for (int i = 0; i < WildRoomGuardCount; i++)
            {
                UnitType monsterType = new WildMonsterA();
                Vector2Int spawnPos = room.GetRandomPosInRoom();
                int attempts = 0;
                while (!_unitGenerate.IsAreaClear(spawnPos, monsterType.footprint, room.Floor) && attempts < 20)
                {
                    spawnPos = room.GetRandomPosInRoom();
                    attempts++;
                }
                if (attempts >= 20) continue; // 자리를 못 찾으면 이번 개체는 포기(방이 너무 좁거나 이미 붐빔)

                Monster monster = _unitGenerate.GenerateUnitAtPos<Monster>(monsterType, spawnPos, room.Floor);
                monster.FactionBehavior = new WildMonsterBehavior();
                monster.MovementAlgorithm = new RoomConfinedMovement();
                monster.summonPosition = spawnPos; // IdleFSMState 배회 기준점(소환 위치)

                units.Add(monster);
                // 2번: 동시 스폰된 유닛들이 같은 프레임에 actionCooldown이 만료돼 버스트가 일어나는
                // 것을 막는다. 초기값을 0~1액션주기 범위에서 랜덤 지터로 흩뿌린다.
                monster.CombatState.State.actionCooldown = monster.BaseStat.walkSpeed > 0f
                    ? UnityEngine.Random.Range(0f, 1f / monster.BaseStat.walkSpeed) : UnityEngine.Random.Range(0f, 1f);
                RegisterUnitPos(monster, monster.position);
                room.AddUnit(monster);
            }
        }

        LogHelper.Log(LogHelper.GAME, "SpawnWildRoomGuards: 야생 방 배치 완료.");
    }

    // 빌드에서는 Application.Quit() 시 OnDestroy 호출이 보장되지 않아 Finalize()만으로는 부족할 수
    // 있다 — Processor.OnApplicationQuit()(실제 Unity 콜백)로 한 번 더 정리한다. ClearRoomPopulationLabels()는
    // 중복 호출해도 안전(Unity 파괴된 오브젝트 == null 오버로드).
    public override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
        _isShuttingDown = true;
        ClearRoomPopulationLabels();
    }

    // Processor가 등록된 Routine들을 순회하며 매 프레임 호출함(기존 Update()와 동일한 역할)
    public override void UpdateProcess()
    {
        // 종료 시퀀스 중엔 아무 것도 새로 만들거나 건드리지 않는다(위 _isShuttingDown 주석 참고).
        if (_isShuttingDown) return;

        _offenseProcessor?.UpdateProcess();
        _defenseProcessor?.UpdateProcess();
        // 문 개폐(기초문서.md 피드백, 2026-08-22) — 기본 닫힘 + 보유 진영 근접 시에만 시각적으로 열림.
        _doorSystem?.UpdateProcess();

        // 턴 액션 처리 후, 씬 상주 시각적 요소들 위치 일괄 동기화
        for (int i = units.Count - 1; i >= 0; i--)
        {
            var u = units[i];
            if (u == null || u.Health.hp <= 0)
            {
                RemoveDeadUnit(i, u);
                continue; // 사망/파괴 시 시각적 요소 제거 완료
            }

            u.OnUpdate(Time.deltaTime);

            // 선택 표시(발밑 링)/단일 선택 시야 범위(2026-08-22 사용자 신고 "선택 담당 시각화들이
            // 꼬임" — 발밑 링이 몇 개는 생기고 몇 개는 안 생기거나, 시야 범위 표시가 계속 남거나,
            // 다른 유닛을 선택해도 안 사라지는 문제) — 아래 ProcessUnitAction 경유 SyncVisual은
            // "이번 틱에 위치/라벨/방향이 실제로 바뀐 유닛"에게만 호출되므로, 가만히 서 있는 유닛은
            // 선택 상태가 바뀌어도 시각이 갱신되지 않았다. 상태 변화 여부와 무관하게 모든 살아있는
            // 유닛에 대해 매 프레임 무조건 갱신한다(SetActive/불리언 비교뿐이라 비용이 낮다).
            _unitGenerate?.RefreshSelectionVisual(u);

            u.CombatState.State.actionCooldown -= Time.deltaTime;
            if (u.CombatState.State.actionCooldown <= 0f)
            {
                if (IsHighPriorityFsmState(u))
                {
                    ProcessUnitAction(u);
                }
                else if (_queuedForThrottledAction.Add(u))
                {
                    _throttledActionQueue.Enqueue(u);
                }
            }
        }

        // 위 루프에서 큐로 미룬 배회/탐색 유닛을 프레임당 상한만큼만 꺼내 처리 — 나머지는 다음
        // 프레임(들)로 자연스럽게 넘어간다.
        int throttledProcessed = 0;
        while (throttledProcessed < MaxThrottledUnitActionsPerFrame && _throttledActionQueue.Count > 0)
        {
            Unit u = _throttledActionQueue.Dequeue();
            _queuedForThrottledAction.Remove(u);
            if (u == null || u.Health.hp <= 0) continue; // 대기 중 사망 — 예산 소모 없이 건너뜀
            ProcessUnitAction(u);
            throttledProcessed++;
        }

        if (Time.timeScale < 0.01f) // 일시정지 상태여도 외부 조작(InputManager 등)에 의한 선택 렌더링 피드백이 즉시 반영되도록 매 프레임 Sync
        {
            if (_unitGenerate != null)
            {
                foreach(var u in units) _unitGenerate.SyncVisual(u);
            }
        }

        if (_threatTileRenderer != null)
        {
            _threatTileRenderer.Render(units);
        }

        if (_propagationDebugVisualizer != null)
        {
            _propagationDebugVisualizer.Render(units);
        }

        RefreshRoomPopulationLabels();
    }

    // 유닛 배치 시스템(2026-07-27 신규) — "방의 최대 인원수를 맵에 표시, 카메라를 따라다니지 않게,
    // N/M 형식"(사용자 요청). 인구수는 몬스터 전용이라(Room.CurrentPopulation이 이미 인류/야생 제외)
    // 값 자체가 자동으로 플레이어 몬스터 기준으로 나온다. 0층은 인구수 개념이 적용 안 되는 인류 로비라
    // 표시하지 않는다(사용자 요청). 방 개수가 많지 않아 매 프레임 확인해도 비용이 낮지만, 실제로 값이
    // 바뀔 때만 TextMesh.text를 갱신한다(text setter가 매번 메시를 재생성하므로 불필요한 대입을
    // 피하는 게 프레임드랍 방지에 중요 — 사용자 요청으로 점검·수정).
    private void RefreshRoomPopulationLabels()
    {
        if (allRooms == null) return;

        foreach (var room in allRooms)
        {
            if (room == null || room.Floor == 0 || room.MaxPopulation <= 0) continue;

            if (!_roomPopulationLabels.TryGetValue(room, out TextMesh label) || label == null)
            {
                label = CreateRoomPopulationLabel(room);
                _roomPopulationLabels[room] = label;
                _roomPopulationLabelText[room] = (-1, -1);
            }

            // E: int 비교로 변화 감지 → 바뀐 경우에만 $"" 문자열 생성
            if (_roomPopulationLabelText.TryGetValue(room, out var prev)
                && prev.pop == room.CurrentPopulation && prev.max == room.MaxPopulation) continue;
            _roomPopulationLabelText[room] = (room.CurrentPopulation, room.MaxPopulation);
            label.text = $"{room.CurrentPopulation}/{room.MaxPopulation}";

            // 인구수 초과 시각 피드백(2026-07-28, 사용자 요청 "방 인원수가 꽉차면 방 라벨 빨간색으로
            // 바꾸고, 아니면 다시 원래대로 돌려") — 텍스트가 바뀌는 시점(=인구수가 바뀐 시점)에만
            // 같이 재계산하면 충분하다(CurrentPopulation이 바뀌지 않으면 가득 참 여부도 안 바뀜).
            label.color = room.CurrentPopulation >= room.MaxPopulation ? Color.red : DefaultRoomPopulationLabelColor;
        }
    }

    private static readonly Color DefaultRoomPopulationLabelColor = new Color(1f, 1f, 1f, 0.75f);

    // BuildRoomGrid가 다시 호출될 때(맵 재생성 등) 컨테이너는 그대로 두고 라벨만 갈아 끼운다.
    private void ClearRoomPopulationLabels()
    {
        foreach (var label in _roomPopulationLabels.Values)
        {
            if (label != null) UnityEngine.Object.Destroy(label.gameObject);
        }
        _roomPopulationLabels.Clear();
        _roomPopulationLabelText.Clear();
    }

    private TextMesh CreateRoomPopulationLabel(Room room)
    {
        GameObject go = new GameObject($"RoomPopLabel_{room.RoomName}");
        Transform labelGroup = GetFloorCategoryGroup(room.Floor, "Labels");
        if (labelGroup != null) go.transform.SetParent(labelGroup, false);

        Vector3 floorOffset = _unitGenerate != null ? _unitGenerate.GetFloorOffset(room.Floor) : Vector3.zero;
        Vector2 center = room.Bounds.center;
        go.transform.position = new Vector3(center.x, center.y, 0f) + floorOffset;

        // 세련되게(사용자 요청) — 굵고 큰 기본값 대신 은은한 반투명 회백색 + 적당한 크기로 톤을 낮춘다.
        TextMesh tm = go.AddComponent<TextMesh>();
        tm.fontSize = 48;
        tm.characterSize = 0.11f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = DefaultRoomPopulationLabelColor;

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sortingOrder = 50; // 바닥/오브젝트(5)보다 위, 위협타일 셀(999)보다는 아래

        return tm;
    }

    private void RemoveDeadUnit(int index, Unit u)
    {
        if (u != null) _combatEventService?.RecordKillWeightEvent(u, units);
        
        if (u != null && u.Health.hp <= 0)
        {
            // 세력별 사망 이벤트(예: 처치 보상) 처리 — lastAttacker가 아니라 lastDamageDealer를
            // 넘긴다(2026-07-27, 점령 전환/처치 보상 MVP): lastAttacker는 인류-몬스터 교차 히트에서만
            // 갱신되는 가중치 시스템 전용 필드라 몬스터끼리(예: 플레이어 몬스터의 야생 몬스터 처치) 킬은
            // 항상 null이 된다 — Unit.lastDamageDealer 필드 주석 참고.
            u.FactionBehavior?.OnDeath(u, u.lastDamageDealer);

            // 방 소유권 전환은 이제 코어 체력제(OffenseProcessor.OnCoreDestroyed)로만 일어난다
            // (기초문서.md 피드백, 2026-08-22) — 유닛 사망 자체는 더 이상 점령 전환을 트리거하지 않는다.
            // 문 개폐도 이제 방 유닛 구성이 아니라 매 프레임 진영·근접 여부로 직접 판정하므로
            // (DoorSystem.UpdateProcess) 사망 시점에 따로 재확인할 필요가 없다.

            // 컴포넌트 정리 — WildBaseSpawnerComponent.OnDespawn이 HasActiveSpawner = false로
            // 바꿔야 거점형 오펜스 성공 판정이 작동한다. 유닛 사망 시점마다 호출.
            foreach (var comp in u.Components)
                comp.OnDespawn();

            string objId = "Corpse_" + System.Guid.NewGuid().ToString().Substring(0, 4);
            Vector3Int gridPos = new Vector3Int(u.position.x, u.position.y, u.currentFloor);
            DangerStage causerStage = DangerStage.Stage0;
            
            if (u.lastAttacker != null && u.Knowledge != null)
            {
                causerStage = u.Knowledge.GetDangerStage(u.lastAttacker.unitType.typeName, u.lastAttacker.isSpecialUnit ? u.lastAttacker.name : null, u.lastAttacker.BaseStat.baseDanger);
            }
            
            // SpawnObject는 objectGrid에 이미 오브젝트가 있는 타일이면 조용히 아무것도 안 하고
            // 리턴한다 — 함정에 맞아 죽으면 사망 위치가 곧 그 함정 타일이라 항상 이 케이스에 걸려서
            // 시체가 전혀 안 생기고 있었다(사용자 신고 "시체 생성이 안 되는데 확인해줘", 2026-07-23).
            // 죽은 자리가 이미 차있으면 바로 옆 빈 타일을 찾아 대신 놓는다.
            if (objectGrid.ContainsKey(gridPos))
                gridPos = FindNearbyFreeObjectTile(gridPos);

            // 07문서 14장: 사망 시 사망 위치에서 사망음 발생(Destroy 전, 위치가 아직 유효한 지금 시점).
            PropagationSystem.EmitSound(this, SoundType.Death, u.position, u.currentFloor, u);

            bool isMonsterCorpse = u is Monster;
            List<string> tags = new List<string> { "Object/Passable/Corpse", isMonsterCorpse ? "Monster" : "Human" };
            // InteractableObject.BaseVisibility 기본값 자체가 0(사용자 요청) — 여기서 따로 넘길 필요 없음.
            InteractableObject corpse = new InteractableObject(objId, gridPos, WeightMath.CorpseTraceBaseInterest, 0f, tags, causerStage);
            // 인간 시체(짙은 붉은색)와 몬스터 시체(붉은 갈색)를 미묘하게 다른 색으로 구분.
            Color corpseColor = isMonsterCorpse ? new Color(0.45f, 0.2f, 0.05f) : new Color(0.5f, 0f, 0f);

            // 03문서 4-12~4-15장(2026-07-27 신규): 인류 시체는 사망 사건 추적(정신력 감소/사망 원인
            // 확인/원인미상 수색)의 시작점이다 — Destroy 전인 지금(u는 Human) 위치/방향/lastAttacker를
            // 스냅샷으로 남겨야 한다.
            if (!isMonsterCorpse && u is Human deadHuman && deadHuman.party != null)
            {
                corpse.OwnerPartyId = deadHuman.party.Id;
                SpawnObject(corpse, corpseColor);
                PartyDeathSystem.OnPartyMemberDied(deadHuman, objId);
            }
            else
            {
                // E_MONSTER_KILL_INDIRECT 연결용(2026-08-05) — 인류에게 죽은 몬스터만 스냅샷한다(u는
                // 아직 Destroy 전이라 unitType/name 접근이 안전한 지금 시점). PropagationSystem.
                // OnMonsterCorpseDiscovered가 나중에 이 값으로 RecordEventByKey를 호출한다.
                if (isMonsterCorpse && u.lastAttacker is Human)
                {
                    corpse.MonsterKilledByHuman = true;
                    corpse.MonsterIsSpecialUnit = u.isSpecialUnit;
                    corpse.MonsterSpeciesKey = u.unitType != null ? u.unitType.typeName : null;
                    corpse.MonsterIndividualKey = u.isSpecialUnit ? u.name : null;
                }
                SpawnObject(corpse, corpseColor);
            }

            // 사용자 요청(2026-08-23): 시체는 1분 뒤 자동으로 사라진다. 그 사이 조사/파티 사망 추적
            // 등 다른 경로가 이미 CollectObject로 치웠거나 같은 타일에 다른 시체가 새로 자리잡았을
            // 수 있으니, 타이머가 끝나는 시점에 objectGrid[gridPos]가 여전히 이 corpse 인스턴스인지
            // 확인한 뒤에만 제거한다.
            DespawnCorpseAfterDelay(gridPos, corpse).Forget();
        }

        if (u != null) CheckPartyWaveState(u);
        if (_unitGenerate != null && u != null)
        {
            _unitGenerate.RemoveVisual(u);
        }
        if (_threatTileRenderer != null && u != null)
        {
            _threatTileRenderer.RemoveThreatZone(u);
        }
        if (u != null) UnregisterUnitPos(u, u.position);
        if (u != null) ClearPerceptionRecordsFor(u);
        if (u != null) castingUnits.Remove(u);
        units.RemoveAt(index);
        if (u != null) UnityEngine.Object.Destroy(u);
    }

    // RemoveDeadUnit의 시체 배치용 — 죽은 자리에 이미 오브젝트가 있으면(대표적으로 함정 위에서 죽은
    // 경우, objectGrid에 함정 자신이 이미 그 타일을 차지하고 있음) 바로 옆부터 정사각형 링 모양으로
    // 넓혀가며 비어있는 첫 타일을 찾는다. objectGrid 점유 여부만 보고 벽인지는 확인하지 않아서, 좁은
    // 통로에서 죽으면 시체가 벽 타일에 놓이는 경우가 있었다(사용자 신고, 2026-07-23 "시체 벽에
    // 생기는거 막아줘") — CreateMap.IsStaticTileWalkable로 벽/구조물 타일도 함께 걸러낸다. 반경 안에
    // 빈 자리가 전혀 없으면(사실상 거의 없음) 원래 위치를 그대로 반환한다 — 그러면 SpawnObject가
    // 조용히 무시하고 넘어간다.
    private Vector3Int FindNearbyFreeObjectTile(Vector3Int center)
    {
        const int maxRadius = 5;
        for (int radius = 1; radius <= maxRadius; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    // 이전 반경에서 이미 검사한 안쪽 칸은 건너뛰어 링(테두리)만 순회한다.
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius) continue;

                    Vector3Int candidate = new Vector3Int(center.x + dx, center.y + dy, center.z);
                    if (objectGrid.ContainsKey(candidate)) continue;
                    if (cmap != null && !cmap.IsStaticTileWalkable(center.z, new Vector2Int(candidate.x, candidate.y))) continue;

                    return candidate;
                }
            }
        }
        return center;
    }

    // 2026-07-20: 02문서(인지·정보판정) 구현으로 생긴 Unit.perceptionRecords는 "누가 이 유닛을 봤는지"를
    // 그 관찰자 쪽에 Unit 참조를 키로 들고 있는 구조라, 유닛이 죽어도 다른 유닛들의 딕셔너리에는 destroyed
    // 참조가 그대로 남는다 — 웨이브가 반복될수록 죽은 몬스터 참조가 계속 쌓여 UpdateFOV 끝의 sweep(전체
    // perceptionRecords 순회) 비용이 웨이브를 거듭할수록 계속 커지는 게 실제 프레임 드롭의 원인이었다.
    // 유닛이 죽는 시점에 전 유닛을 한 번 순회해 그 유닛에 대한 기록을 지운다(사망은 매 프레임 일어나는
    // 일이 아니므로 O(units) 비용을 여기서 감당하는 게 맞다).
    private void ClearPerceptionRecordsFor(Unit dead)
    {
        foreach (var other in units)
        {
            if (other == null || other == dead) continue;
            other.Perception.RemovePerceptionRecord(dead);
        }
        dead.Perception.State.perceptionRecords.Clear();
    }

    public void DespawnUnit(Unit u)
    {
        if (u == null) return;
        if (_unitGenerate != null)
        {
            _unitGenerate.RemoveVisual(u);
        }
        UnregisterUnitPos(u, u.position);
        ClearPerceptionRecordsFor(u);
        units.Remove(u);
        UnityEngine.Object.Destroy(u);
    }

    // ─────────────────────────── 파티 시스템 ───────────────────────────
    public Party CreateParty(string name, List<Human> members)
    {
        var party = new Party(System.Guid.NewGuid().ToString(), name);
        foreach (var m in members)
        {
            if (m == null) continue;
            party.Members.Add(m);
            m.party = party;

            // 5-1장: "신규 유닛 개인 지도 정보 = 최신 전역 지도 정보" — 파티에 합류하는(=웨이브에
            // 입장하는) 시점이 정확히 문서가 말하는 "신규 진입" 순간이다. 이미 개인 기억
            // (personalWeights)이 있는 유닛은 InitializeNewUnitPersonalInfo 내부에서 덮어쓰지
            // 않으므로(5-2장) 재사용 유닛을 넣어도 안전하다.
            m.Knowledge?.InitializeNewUnitPersonalInfo(m);
        }
        party.AssignLeaderIfNeeded(); // 09_명령·리더 문서 부재 임시 대체 — Party.cs 주석 참고
        parties.Add(party);
        return party;
    }

    private void CheckPartyWaveState(Unit deadUnit)
    {
        var knowledge = deadUnit.Knowledge;
        if (knowledge == null) return;

        if (deadUnit is Human deadHuman && deadHuman.party != null)
        {
            var party = deadHuman.party;
            party.AssignLeaderIfNeeded(); // 죽은 유닛이 리더였으면 여기서 재선정(파티 전멸 여부와 무관하게 항상 확인)
            if (party.WaveEnded || !party.IsWiped) return;

            party.WaveEnded = true;
            knowledge.OnPartyWipeout();

            // 13-2장: 전멸 흔적 — 원인 대상(이 파티원을 마지막으로 공격한 대상)의 위험도 단계로
            // 보정치를 계산해 등록한다. RegisterWipeoutTrace가 발급한 traceId를 흔적 오브젝트에
            // 실어 스폰하면, 생환한 다른 파티가 CastRay로 이 오브젝트를 발견하는 시점에
            // UnitFunction.CastRay가 OnWipeoutTraceReflected(traceId)를 호출해 동일 ID당 1회만
            // 던전 위험도에 반영한다(2026-07-09: 시체/흔적 엔티티가 생기면서 실제로 연결됨).
            Unit causer = deadHuman.lastAttacker;
            DangerStage causerStage = DangerStage.Stage0;
            if (causer != null)
                causerStage = knowledge.GetDangerStage(causer.unitType.typeName, causer.isSpecialUnit ? causer.name : null, causer.baseDanger);
            string traceId = knowledge.RegisterWipeoutTrace(causerStage);

            // 전멸 흔적 오브젝트 생성
            string objId = "Wipeout_" + System.Guid.NewGuid().ToString().Substring(0, 4);
            Vector3Int gridPos = new Vector3Int(deadHuman.position.x, deadHuman.position.y, deadHuman.currentFloor);
            List<string> tags = new List<string> { "Object/Passable/WipeoutTrace" };
            InteractableObject wipeoutObj = new InteractableObject(objId, gridPos, WeightMath.WipeoutTraceBaseInterest, 0f, tags, causerStage, traceId);
            SpawnObject(wipeoutObj, Color.black);
        }
        else if (deadUnit is Monster deadMonster)
        {
            // break하지 않고 끝까지 순회한다 — WaveSpawner가 같은 웨이브에 여러 파티를 스폰하면
            // 여러 Party가 동일한 WaveMonsters 리스트(참조)를 공유하므로, 몬스터 한 마리의 죽음이
            // 동시에 여러 파티의 웨이브 클리어를 트리거할 수 있다(2026-07-08: 다중 파티 지원 추가
            // 당시 이 break를 지우지 않아서 첫 번째로 매칭된 파티만 OnWaveEnd를 받던 버그 수정).
            foreach (var party in parties)
            {
                if (party.WaveEnded || !party.WaveMonsters.Contains(deadMonster) || !party.IsWaveCleared) continue;

                party.WaveEnded = true;
                var survivors = party.GetSurvivors();
                knowledge.OnWaveEnd(survivors);
            }
        }
    }



    // 전투/전술/플레이어 명령 상태는 즉시 처리 대상(위 뭉침 완화 큐를 건너뜀) — 반응성이 중요한
    // 상태만 골라낸다. 판정 기준은 "이번 프레임 JudgeState 이전, 마지막으로 확정된 상태"라 최대
    // 1행동주기(≈0.3초)만큼 지연될 수 있지만(예: 지금 막 적을 발견해 이번 프레임에 Combat으로
    // 전환될 유닛), 그 경우도 공격을 받는 쪽 반응(OnReactToThreat/DefenseSystem)은 이 유닛의 턴과
    // 무관하게 별도 경로로 처리되므로 안전하다.
    private static bool IsHighPriorityFsmState(Unit unit)
    {
        IFSMState state = unit.fsm.CurrentState;
        return state is CombatFSMState || state is TacticalFSMState || state is PlayerCommandFSMState;
    }

    private void ProcessUnitAction(Unit u)
    {
        // 4-3장: 경계 상태에서 위치/방향을 확인하며 이동할 때는 이동속도가 75%로 줄어든다.
        // 07문서 16-3장: 단, 피격 발생 공격음/피격 비명/사망음 확인 접근은 긴급 소리라 감속 없이 정상
        // 이동속도를 유지한다(AlertSearchState.IsUrgentSoundApproach).
        bool alertMoveSlowdown = u.currentAlertSearch != null && !u.currentAlertSearch.IsUrgentSoundApproach;
        float speed = u.BaseStat.walkSpeed * (alertMoveSlowdown ? ExplorationMath.AlertMoveSpeedRatio : 1f);
        u.CombatState.State.actionCooldown = speed > 0f ? (1f / speed) : 1f;

        // 아래 TriggerTrapIfStepped가 "이번 틱 시작 시점에 이미 이 함정을 알고 대응 중이었는지"를
        // 판단할 때 쓸 스냅샷 — ExecuteAction()이 currentTrapInteraction을 바꾸기 전 상태를 기억해둔다.
        TrapInteractionState trapInteractionBefore = u.currentTrapInteraction;

        // 라벨 스냅샷은 JudgeState()보다 먼저 찍어야 한다(2026-08-20 버그 수정, 사용자 신고 "머리 위에
        // (정지)라는 상태가 안 떠") — JudgeState()가 FSM _current를 실제로 전환시키는 지점인데, 예전엔
        // 이 스냅샷을 JudgeState() 다음에 찍어서 "전환 직후"의 라벨을 old/new 둘 다로 잡아버렸다(그
        // 틱엔 위치·방향도 안 바뀌는 전환이면 stateChanged가 전혀 감지되지 않음 — HaltFSMState처럼
        // 진입 즉시 아무것도 안 하는 상태에서 특히 두드러진다). 소집("소집" 라벨)은 UnitFSM.GetLabel이
        // isMustered를 매번 새로 확인하는 별도 오버라이드라 이 문제를 우연히 피해갔을 뿐, 근본적으로는
        // 모든 FSM 상태 전환 라벨에 해당하는 일반적인 결함이었다.
        string oldLabel = u.fsm.GetLabel(u);
        u.JudgeState();
        Vector2Int oldPos = u.position;
        Dir oldDir = u.currentDir;
        u.ExecuteAction();
        string newLabel = u.fsm.GetLabel(u);

        bool stateChanged = oldPos != u.position || oldLabel != newLabel || oldDir != u.currentDir;

        if (oldPos != u.position)
        {
            UnregisterUnitPos(u, oldPos);
            if (RegisterUnitPos(u, u.position))
            {
                TriggerTrapIfStepped(u, trapInteractionBefore);

                // 오펜스 자동 트리거: PlayerMonster가 야생 방에 진입하면 즉시 오펜스 시작
                if (u.IsPlayerMonsterFaction)
                    TryTriggerOffenseForUnit(u);

                // 디펜스 자동 트리거(2026-08-20, OffenseProcessor와 대칭): 야생/인류가 플레이어 방에
                // 진입하면 즉시 디펜스 시작 — IdleFSMState가 "평시 배회 금지" 판단에 쓴다.
                if (u.FactionBehavior is WildMonsterBehavior || u.FactionBehavior is HumanFactionBehavior)
                    TryTriggerDefenseForUnit(u);


                // 능동적 위협 감지: 유닛이 이동하여 활성화된 공격 범위로 직접 들어간 경우
                foreach (var caster in castingUnits)
                {
                    if (caster == u || caster.AIState.currentThreat == null) continue;
                    if (caster.AIState.currentThreat.hitbox.Overlaps(SkillAction.GetUnitHitbox(u)))
                    {
                        u.OnReactToThreat(caster, caster.AIState.currentThreat);
                    }
                }
            }
            else
            {
                // 목적지가 이미 다른 유닛에 점유돼 있다(2026-08-22 사용자 신고 "난전중 겹침... 어떤
                // 상황에서도 유닛끼리는 겹쳐지면 안돼") — 이 프레임의 이동 자체를 되돌린다. Move()/
                // ForceMove가 이동 시점엔 이미 자체적으로 점유를 확인하지만, 그 확인과 이 grid 동기화
                // 사이에 다른 경로(스폰/텔레포트 등)가 같은 칸을 먼저 차지했을 수 있는 최종 안전망이다.
                u.position = oldPos;
                RegisterUnitPos(u, oldPos);
            }
        }

        // 01-A 11장: 이동/전투 등으로 이번 턴에 활성화된 후보 중 우선순위가 가장 높은 시야 방향을
        // 확정한다. ExecuteAction() 이후에 호출해야 Move()가 갱신한 currentDir를 "이동 중" 후보의
        // 기본값으로 넘겨줄 수 있고, UpdateFOV() 이전에 호출해야 그 방향 기준으로 시야/인지 범위를 계산한다.
        u.ResolveVisionDirection();
        u.UpdateFOV(units);

        if (stateChanged && _unitGenerate != null)
        {
            _unitGenerate.SyncVisual(u);
        }
    }

    // 9-9/9-10장의 "의도적으로 통과/파괴를 선택했을 때"와 별개로, 함정을 인지하지 못했거나(또는 다른
    // 함정에 정신 팔려) 그냥 밟고 지나가면 GOAP의 선택과 무관하게 자동으로 피해를 입는다 — 해제/우회가
    // 거의 항상 먼저 성공해서 Action_TrapPass가 실전에서 거의 발동하지 않는다는 사용자 피드백
    // (2026-07-22)에 따라 추가. trapInteractionBefore로 "이번 틱 시작 시점에 이미 이 함정을 알고
    // 대응 중이었는지"를 확인해서, 그런 경우(해제 접근/통과/파괴가 이미 진행 중이던 것)엔 제외한다 —
    // 안 그러면 의도적 대응(Action_TrapPass 등)과 중복으로 두 번 맞는다. 함정은 일회성이 아니다
    // (사용자 요청, 2026-07-22) — 해제/파괴로 실제 없앴을 때만 사라지고, 그냥 밟은 것만으로는
    // 소모되지 않는다 — 같은 자리를 다시 밟으면(이 유닛이든 다른 유닛이든) 또 맞는다.
    // 인류 전용(사용자 요청, 2026-07-22) — Goal_TrapResponse를 인류 전용으로 좁힌 것과 맞춰, 몬스터는
    // 이 자동 트리거로도 함정에 전혀 영향받지 않는다(우연히 밟아도 무해 — Passable 태그 그대로 그냥
    // 지나간다).
    private void TryTriggerOffenseForUnit(Unit unit)
    {
        Vector3Int gridPos = new Vector3Int(unit.position.x, unit.position.y, unit.currentFloor);
        if (roomGrid.TryGetValue(gridPos, out Room room) && room.RoomFaction == FactionType.Wild)
            _offenseProcessor?.TryStartOffense(room, unit);
    }

    // TryTriggerOffenseForUnit의 대칭 — 야생/인류 유닛이 플레이어 소유 방으로 걸어 들어오면 디펜스 시작.
    private void TryTriggerDefenseForUnit(Unit unit)
    {
        Vector3Int gridPos = new Vector3Int(unit.position.x, unit.position.y, unit.currentFloor);
        if (roomGrid.TryGetValue(gridPos, out Room room) && room.RoomFaction == FactionType.Player)
            _defenseProcessor?.TryStartDefense(room, unit);
    }

    private void TriggerTrapIfStepped(Unit unit, TrapInteractionState trapInteractionBefore)
    {
        if (!(unit is Human)) return;

        Vector3Int gridPos = new Vector3Int(unit.position.x, unit.position.y, unit.currentFloor);
        if (!objectGrid.TryGetValue(gridPos, out InteractableObject obj)) return;
        if (obj.Tags == null || !obj.Tags.Exists(t => t.Contains("Trap"))) return;

        bool alreadyHandling = trapInteractionBefore != null && trapInteractionBefore.TrapPosition == gridPos;
        if (alreadyHandling) return;

        unit.TakeDamage(obj.TrapDamageMax);
        LogHelper.Log(LogHelper.GAME, $"{unit.unitType.typeName}가 함정을 인지하지 못한 채 밟아 {obj.TrapDamageMax} 피해를 입었습니다.");
    }

    // 시작방 안의 랜덤 배치 가능 위치를 찾는다 — FindInstallableStartRoomPos(생산 건물 자동 배치)가 쓴다.
    private Vector2Int GetRandomStartRoomPos(Vector2 footprint, int floorIdx)
    {
        CreateMap mapGenerator = cmap;

        if (mapGenerator == null || mapGenerator.map.floors == null || floorIdx < 0 || floorIdx >= mapGenerator.map.floors.Length)
            return Vector2Int.zero;

        Floor floor = mapGenerator.map.floors[floorIdx];
        if (floor.chunks == null) return Vector2Int.zero;

        List<Vector2Int> candidates = new List<Vector2Int>();

        int chunkW = floor.config.width;
        int chunkH = floor.config.height;

        for (int cx = 0; cx < chunkW; cx++)
        {
            for (int cy = 0; cy < chunkH; cy++)
            {
                Chunks c = floor.chunks[cx, cy];
                if (c.roomRole != RoomRole.StartRoom || c.chunk == null) continue;

                int cs = floor.config.chunkSize;
                for (int tx = 0; tx < cs; tx++)
                {
                    for (int ty = 0; ty < cs; ty++)
                    {
                        Vector2Int pos = new Vector2Int(cx * cs + tx, cy * cs + ty);

                        if (_unitGenerate.IsAreaClear(pos, footprint, floorIdx))
                            candidates.Add(pos);
                    }
                }
            }
        }

        if (candidates.Count == 0)
            return Vector2Int.zero;

        return candidates[Random.Range(0, candidates.Count)];
    }

    public void SpawnObject(InteractableObject obj, Color color, float rotationZDegrees = 0f)
    {
        if (objectGrid.ContainsKey(obj.Position)) return;

        objectGrid[obj.Position] = obj;
        LogHelper.Log(LogHelper.GAME, $"Generated {obj.Id} at Floor {obj.Position.z}, {new Vector2Int(obj.Position.x, obj.Position.y)} with Tags: [{string.Join(", ", obj.Tags)}]");

        GameObject visual = new GameObject(obj.Id);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();

        // 태그별 실제 아트 스프라이트 배정(사용자 요청, 2026-07-23) — 시체는 colapse.png, 함정은
        // trap.png, 코어는 core.png, 그 외 일반 루팅 오브젝트(O키로 수동 생성)는 obj1.png.
        // Resources.Load 실패(아직 없는 태그 등) 시에만 기존 도형 폴백(함정=삼각형, 그 외=단색
        // 사각형)으로 되돌아간다.
        bool isTrap = obj.Tags != null && obj.Tags.Exists(t => t.Contains("Trap"));
        bool isCorpse = obj.Tags != null && obj.Tags.Exists(t => t.Contains("Corpse"));
        bool isCoreOnly = obj.Tags != null && obj.Tags.Contains(CoreTag);
        bool isLoot = obj.Tags != null && obj.Tags.Exists(t => t.Contains("Loot"));
        // 문 시스템(2026-07-27, 2026-08-22 기본 닫힘으로 전면 개편) — 여기서는 기본 열림(door_open)
        // 스프라이트로 그리지만, DoorSystem.SpawnDoorAt/RebuildDoorAt이 바로 이어서 기본 닫힘
        // (door_closed)으로 교체한다. 이후 개폐는 DoorSystem.UpdateProcess가 매 프레임 진영·근접
        // 여부로 직접 관리한다.
        bool isDoor = obj.Tags != null && obj.Tags.Contains(DoorSystem.DoorTag);

        Sprite sprite = null;
        if (isTrap) sprite = Resources.Load<Sprite>("obj/trap");
        else if (isCorpse) sprite = Resources.Load<Sprite>("obj/colapse");
        else if (isCoreOnly) sprite = Resources.Load<Sprite>("obj/core");
        else if (isDoor) sprite = Resources.Load<Sprite>("obj/door_open");
        else if (isLoot) sprite = Resources.Load<Sprite>("obj/obj1");

        if (sprite == null)
        {
            if (isTrap && _unitGenerate != null)
            {
                sprite = _unitGenerate.CreateTriangleSprite(color);
            }
            else
            {
                Texture2D tex = new Texture2D(32, 32);
                Color[] pixels = new Color[32 * 32];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
                tex.SetPixels(pixels);
                tex.Apply();
                sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
            }
        }
        sr.sprite = sprite;
        sr.sortingOrder = 5;

        // 9-7/9-8장/7-3장(2026-07-27 추가) — 함정 해제·코어 조사 진행 막대를 붙일 자리. 그 외
        // 오브젝트에는 붙이지 않는다(불필요한 컴포넌트/자식 GameObject 낭비 방지).
        if (isTrap || isCoreOnly) visual.AddComponent<ObjectProgressBarVisual>();

        // 빛(Light2D)이 문도 막게(사용자 요청, 2026-07-28) — MapRandering의 벽 셰도우 캐스터와 동일한
        // 기법(유닛 프리팹과 같은 SpriteRenderer 실루엣 기반 ShadowCaster2D). 문은 열림/닫힘에 따라
        // sr.sprite가 바뀌는데(DoorSystem.UpdateProcess), ShadowCaster2D의 SpriteRenderer 프로바이더가
        // 스프라이트 변경 콜백을 등록해두므로 별도 갱신 코드 없이 셰이프가 따라 바뀐다.
        if (isDoor) visual.AddComponent<ShadowCaster2D>();

        Vector3 offset = Vector3.zero;
        if (mapRandering != null)
        {
            // mapRandering의 mapRoot와 같은 계층 접근 특성이 없으므로 임시로 오프셋(offset)을 사용하고,
            // floorTilemaps[obj.Position.z]를 참조해주는 유도도 해야 합니다.
            // 여기서는 floorOffsets 배열을 참조하여 오프셋만 가져옵니다.
            if (mapRandering.floorOffsets != null && obj.Position.z >= 0 && obj.Position.z < mapRandering.floorOffsets.Length)
            {
                offset = mapRandering.floorOffsets[obj.Position.z];
            }

            // 계층 정리(2026-07-28, 사용자 요청) — 문은 "Doors", 그 외(트랩/시체/코어/루팅)는
            // "Objects" 하위 그룹으로 나눠 담는다.
            Transform group = GetFloorCategoryGroup(obj.Position.z, isDoor ? "Doors" : "Objects");
            if (group != null)
            {
                visual.transform.SetParent(group);
            }
        }
        
        visual.transform.position = new Vector3(obj.Position.x + 0.5f, obj.Position.y + 0.5f, 0f) + offset;
        // 문 시스템(2026-07-27, 사용자 요청 "닫혀있는 문은 위치 고려해서 배치") — 통로 방향(수평/수직)에
        // 맞춰 스프라이트를 돌린다. 회전이 필요 없는 기존 오브젝트(트랩/시체/코어/루팅)는 기본값 0도라
        // 영향 없음.
        if (rotationZDegrees != 0f) visual.transform.rotation = Quaternion.Euler(0f, 0f, rotationZDegrees);

        // 사용자 요청(2026-07-28) "오브젝트를 타일 크기에 맞춰 스폰하지 말고 원본 스프라이트 크기에
        // 맞춰 스폰" — 2026-07-24에 도입했던 "sprite.bounds 역산해서 타일 1칸에 꽉 차도록" 스케일
        // 보정을 되돌린다. 이제 스케일 1(원본 픽셀 크기/PPU 그대로)로 스폰 — 스프라이트마다 실제
        // 렌더 크기가 제각각이어도 그대로 둔다.
        visual.transform.localScale = Vector3.one;

        objectVisuals[obj] = visual;
    }

    // 코어 전면 개편(기초문서.md 피드백, 2026-08-22) — "Object/Passable/Core" 태그가 붙으면:
    // (1) Human.ComputeInvestigateTarget이 이 오브젝트를 일반 조사 후보에서 제외하고, (2)
    // UnitFunction.CastRay가 PerceptionTargetKind.Core로 분류해 개인 지도에 등록한다. 방 소유권
    // 판정 자체는 태그가 아니라 Room.CoreObjectId/CorePosition을 직접 참조한다(TacticalFSMState.
    // FindHostileRoomCore). 이제 모든 방이 항상 코어를 하나씩 갖는다 — 보스방 1개 한정이던 예전
    // 던전 코어(DungeonCoreTag)는 폐기.
    public const string CoreTag = "Object/Passable/Core";
    // 모든 방에 공통 적용하는 자리표시자 값(플레이 테스트 후 조정) — 물리공격력 40 기준 코어 파괴
    // 배율(TrapDestroyDamagePerSecondPerAttack=0.5)을 그대로 적용하면 약 10초 만에 파괴된다.
    // 2026-08-22 사용자 요청 "코어의 체력을 5배로 늘려줘" — 200 → 1000(약 50초).
    private const float RoomCoreMaxHp = 1000f;
    // 코어 공격 데미지 고정 초당 비율(2026-08-22 사용자 요청 "코어 공격을, 공격 시도 중일때만 체력이
    // 초당으로 다는 형식으로 바꿔줘. 공격 시도중인 유닛 마리 수 당 추가") — 예전엔 각 유닛의
    // physicalAttack 스탯(물리공격력 40 기준 0.5배 = 초당 20)에 비례해서 깎였는데, 이제는 유닛 스탯과
    // 무관하게 채널링 중인 유닛 1명당 항상 이 고정값만큼만 초당 깎인다. UnitFunction.OnUpdate가 채널링
    // 중인 유닛마다 독립적으로 이 값을 적용하므로, 같은 코어를 여러 명이 동시에 공격하면 그 인원수만큼
    // 자연히 합산된다(별도의 "공격 인원 수 세기" 로직 없이 인원수 비례가 성립). 자리표시자 20은 기존
    // 물리공격력 40 기준 수치와 동일하게 맞춰 1명이 공격할 때의 파괴 시간(약 50초)이 그대로 유지되게
    // 했다 — 플레이 테스트 후 조정.
    public const float CoreAttackDamagePerSecond = 20f;
    // 문/게이트 시스템(2026-07-27~28 구현, 2026-08-20 분리) — 문 배치/웨이브 시작 시 전체 잠금/단일
    // 진영만 남으면 재개방/문 타일 이동·시야 차단 판정을 UnitRegistry와 동일한 지연 조회 패턴을 쓰는
    // DoorSystem(Assets/Script/Unit/Session/)으로 뺐다. GameSession이 지나치게 커지는 것을 막기
    // 위함(MonsterDefensePlacementSystem 분리와 동일한 이유). 아래는 외부에서 GameSession.Instance.X()
    // 형태로 호출하던 기존 진입점을 유지하기 위한 얇은 위임이다 — 실제 구현은 전부 DoorSystem에 있다.
    public bool IsDoorTile(Vector3Int pos) => _doorSystem.IsDoorTile(pos);
    public static List<Vector2Int>[] GetGateDoorTiles(Gate gate, int chunkSize) => DoorSystem.GetGateDoorTiles(gate, chunkSize);
    // 문 진영 판정(기초문서.md 피드백, 2026-08-22 "문은 보유 진영의 유닛만 지나갈 수 있고... 그게
    // 아니라면 공격해서 파괴해야 해") — UnitFunction.CanMove/AStarMovement.IsTileWalkable이 이동 판정에
    // 직접 사용(GameSession.Instance 없이도 static으로 호출 가능하도록 DoorSystem에 그대로 위임).
    public bool IsBlockedByClosedDoor(Vector3Int pos, Unit unit) => _doorSystem.IsBlockedByClosedDoor(pos, unit);
    // 문 개폐 시각 트리거(2026-08-22 재조정, 사용자 요청 "문 인접 칸에서 문에 접근 시도시 열리는
    // 방식으로") — UnitFunction.Move가 인접 칸에서 문 타일로 넘어가려는 시도가 있을 때마다 호출한다.
    // 통행 가능 여부(IsBlockedByClosedDoor)와는 완전히 별개 판정(순수 시각 연출용).
    public void NotifyDoorApproachAttempt(Vector3Int pos, Unit unit) => _doorSystem.NotifyApproachAttempt(pos, unit);
    // 문도 방어건물화(기초문서.md 피드백, 2026-08-22) — DoorSystem에 얇게 위임(위 세 메서드와 동일 관례).
    public void RemoveDoor(Vector3Int pos) => _doorSystem.RemoveDoor(pos);
    public void RebuildDoorAt(Vector3Int pos) => _doorSystem.RebuildDoorAt(pos);
    public bool IsRepairableDoorTile(Vector3Int pos) => _doorSystem.IsRepairableDoorTile(pos);

    // 코어 전면 개편(기초문서.md 피드백, 2026-08-22) — 게임 시작 시 모든 방(야생 포함, 0층 제외)에
    // 코어를 하나씩 자동 생성한다. 이전엔 보스방 1개뿐이었다(SpawnInitialDungeonCore, 폐기).
    // SpawnWildRoomGuards와 동일한 "방 안 랜덤 위치 + IsAreaClear 재시도" 패턴을 재사용한다.
    // 2026-08-22 초기 생성 시 문이나 횃불 바로 앞을 막지 않도록 판별하는 메서드
    private bool IsGoodForInitialSpawn(Vector3Int gridPos, Vector2 footprint)
    {
        int fw = (int)footprint.x;
        int fh = (int)footprint.y;

        for (int dx = -1; dx <= fw; dx++)
        {
            for (int dy = -1; dy <= fh; dy++)
            {
                Vector3Int checkPos = new Vector3Int(gridPos.x + dx, gridPos.y + dy, gridPos.z);
                
                // 횃불이 있는 위치인지 확인
                if (_fogOfWarSystem != null && _fogOfWarSystem.ActiveTorchPositions.Contains(checkPos))
                    return false;
                
                // 문/게이트 바로 앞인지 확인 (통로 차단 방지)
                if (_doorSystem != null && _doorSystem.IsDoorTile(checkPos))
                    return false;
            }
        }
        return true;
    }

    private void SpawnAllRoomCores()
    {
        if (_unitGenerate == null || allRooms == null) return;

        foreach (var room in allRooms)
        {
            if (room.RoomId < 0 || room.Floor < 0) continue;
            if (room.Floor == 0) continue; // 0층(인류 소유 로비)은 방 점령 개념이 없음

            Vector2Int spawnPos = room.GetRandomPosInRoom();
            int attempts = 0;
            while ((objectGrid.ContainsKey(new Vector3Int(spawnPos.x, spawnPos.y, room.Floor))
                    || !_unitGenerate.IsAreaClear(spawnPos, Vector2.one, room.Floor) || !IsGoodForInitialSpawn(new Vector3Int(spawnPos.x, spawnPos.y, room.Floor), Vector2.one)) && attempts < 20)
            {
                spawnPos = room.GetRandomPosInRoom();
                attempts++;
            }
            if (attempts >= 20)
            {
                LogHelper.Warning(LogHelper.GAME, $"SpawnAllRoomCores: {room.RoomName} 방(F{room.Floor})에 코어를 놓을 자리를 찾지 못했습니다.");
                continue;
            }

            Vector3Int gridPos = new Vector3Int(spawnPos.x, spawnPos.y, room.Floor);
            string objId = "Core_" + System.Guid.NewGuid().ToString().Substring(0, 4);
            InteractableObject obj = new InteractableObject(objId, gridPos, 120f, 0f, new List<string> { CoreTag }, coreHp: RoomCoreMaxHp);
            SpawnObject(obj, Color.magenta);
            MarkTileObstacle(gridPos, true);

            room.CoreObjectId = objId;
            room.CorePosition = gridPos;
        }

        LogHelper.Log(LogHelper.GAME, "SpawnAllRoomCores: 모든 방에 코어 배치 완료.");
    }

    // 코어도 건물처럼 벽과 동일한 판정으로 취급되게 해달라는 요청(2026-08-22) — BuildingManager.
    // UpdateMapDataObstacle과 동일한 패턴(맵 타일 데이터 + 양 진영 discoveredMap 동기화). 코어는
    // 건물과 달리 철거(Uninstall)되지 않고 파괴 시 방 소유권만 바뀐 채 반피로 회복되므로, 여기엔
    // 해제(false) 경로가 없다.
    private void MarkTileObstacle(Vector3Int pos, bool isObstacle)
    {
        if (pos.x < 0 || pos.y < 0 || pos.z < 0) return;
        if (cmap == null || cmap.map.floors == null) return;
        if (pos.z >= cmap.map.floors.Length) return;

        Floor floor = cmap.map.floors[pos.z];
        int cs = floor.config.chunkSize;
        int cx = pos.x / cs;
        int cy = pos.y / cs;
        int tx = pos.x % cs;
        int ty = pos.y % cs;

        if (cx >= 0 && cx < floor.config.width && cy >= 0 && cy < floor.config.height)
        {
            var chunk = floor.chunks[cx, cy];
            if (chunk.chunk != null)
            {
                chunk.chunk[tx, ty].isStructureExist = isObstacle;
            }
        }

        int mapValue = isObstacle ? 2 : 1;

        if (Unit.humanFactionData != null && Unit.humanFactionData.discoveredMap != null
            && pos.z >= 0 && pos.z < Unit.humanFactionData.discoveredMap.Length)
        {
            Unit.humanFactionData.discoveredMap[pos.z][pos.x, pos.y] = mapValue;
        }

        if (Unit.monsterFactionData != null && Unit.monsterFactionData.discoveredMap != null
            && pos.z >= 0 && pos.z < Unit.monsterFactionData.discoveredMap.Length)
        {
            Unit.monsterFactionData.discoveredMap[pos.z][pos.x, pos.y] = mapValue;
        }
    }

    // 건축물·자원·유닛 생산 MVP(2026-07-27, 사용자 요청) — 게임 시작 시 던전 1층 시작방(RoomRole.
    // StartRoom, 플레이어=몬스터 진영 거점)에 자원 생산 건물(V키)과 유닛 생산 건물(B키)을 각각 무상으로
    // 1개씩, 서로 다른 랜덤 위치에 배치한다. B/V키로 직접 짓는 것과 동일한 Install*Building 경로를
    // 그대로 재사용 — 자원 소모만 건너뛴다.
    private void SpawnInitialBuildings()
    {
        if (_buildingManager == null || _unitGenerate == null) return;

        int floorIdx = 1;
        // 사용자 요청(2026-07-27) — V키(자원 생산 건물)는 별도 스프라이트(obj/resource_building)를 쓴다.
        Sprite resourceBuildingSprite = Resources.Load<Sprite>("obj/resource_building");
        Sprite productionBuildingSprite = Resources.Load<Sprite>("obj/building");

        Vector3Int? resourcePos = FindInstallableStartRoomPos(floorIdx);
        if (resourcePos.HasValue)
        {
            _buildingManager.InstallResourceBuilding(resourcePos.Value, resourceBuildingSprite);
        }
        else
        {
            LogHelper.Warning(LogHelper.GAME, "SpawnInitialBuildings: 시작방에 자원 생산 건물을 놓을 자리를 찾지 못했습니다.");
        }

        Vector3Int? productionPos = FindInstallableStartRoomPos(floorIdx);
        if (productionPos.HasValue)
        {
            _buildingManager.InstallProductionBuilding(productionPos.Value, ProductionRule.CreateDefaultPlayerUnitRules(), productionBuildingSprite);
        }
        else
        {
            LogHelper.Warning(LogHelper.GAME, "SpawnInitialBuildings: 시작방에 유닛 생산 건물을 놓을 자리를 찾지 못했습니다.");
        }
    }


    // 시작방 안에서 건물을 놓을 수 있는 랜덤 위치를 찾는다 — 자원/유닛 생산 건물 두 개가 같은 자리를
    // 뽑아 겹치지 않도록(CanInstallAt이 이미 건물이 있는 타일은 거부) 여러 번 재시도한다
    // (SpawnWildRoomGuards의 자리 재시도 패턴과 동일).
    private Vector3Int? FindInstallableStartRoomPos(int floorIdx)
    {
        const int maxAttempts = 20;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2Int pos = GetRandomStartRoomPos(Vector2.one, floorIdx);
            Vector3Int gridPos = new Vector3Int(pos.x, pos.y, floorIdx);
            if (_buildingManager.CanInstallAt(gridPos) && IsGoodForInitialSpawn(gridPos, Vector2.one)) return gridPos;
        }
        return null;
    }

    // O키(루팅 오브젝트)/P키(함정) — 예전엔 눌렀을 때 즉시 무작위 위치에 스폰했지만, B키(빌드 모드)
    // 처럼 원하는 위치를 직접 골라서 놓을 수 있게 해달라는 요청(2026-07-22)에 따라 InputManager가
    // 고스트 배치 모드를 관리하고 실제 위치가 정해지면 이 메서드들을 호출하는 방식으로 바뀌었다.
    public void SpawnLootObjectAt(Vector3Int gridPos)
    {
        if (cmap == null || cmap.map.floors == null) return;
        if (objectGrid.ContainsKey(gridPos)) return;

        string objId = "InteractableObj_" + System.Guid.NewGuid().ToString().Substring(0, 4);
        InteractableObject obj = new InteractableObject(objId, gridPos, 120f, 0f, new List<string> { "Object/Passable/Loot" });
        SpawnObject(obj, Color.magenta);
    }

    // 03문서 9장 함정 대응 테스트용 — 정식 배치 시스템(레벨 구조 문서 부재) 대신 루팅 오브젝트와 동일한
    // 관례로 수동 스폰 훅만 만들어둔다. BaseDanger>0으로 스폰해야 Goal_TrapResponse가 실제로 반응한다
    // (기존 오브젝트들은 전부 BaseDanger=0 — InteractableObject.cs 주석 참고).
    public void SpawnTrapAt(Vector3Int gridPos)
    {
        if (cmap == null || cmap.map.floors == null) return;
        if (objectGrid.ContainsKey(gridPos)) return;

        string objId = "Trap_" + System.Guid.NewGuid().ToString().Substring(0, 4);
        InteractableObject obj = new InteractableObject(
            // 오브젝트→건축물→지나갈 수 있는 건축물 계층(2026-07-22, 사용자 지정) — Loot/Corpse/
            // WipeoutTrace 같은 단순 오브젝트와 달리 함정은 "지나갈 수 있는 건축물"로 취급한다.
            objId, gridPos, baseInterest: 0f, baseDanger: 30f,
            tags: new List<string> { "Object/Building/Passable/Trap" },
            // 데미지 상향(2026-07-22, 사용자 요청 "실제로 데미지 들어가게, 꽤 크게") — 기존 10~25에서
            // 30~60으로. 자동 트리거(GameSession.TriggerTrapIfStepped)까지 추가돼 실제로 자주
            // 발동하니 체감 위협도를 맞추려고 크게 올렸다.
            // 체력 상향(2026-08-23, 사용자 요청) — 기존 20은 물리공격력 비례 파괴 데미지(0.5×physicalAttack
            // /초) 앞에서 실제 유닛 스탯(26~55)이면 1초 안팎에 파괴돼 사실상 "툭 치면 끝"이었다.
            // DoorSystem.DoorMaxHp(300)와 동일한 값으로 맞춤 — 코어/문처럼 실제 저지력을 갖도록.
            baseVisibility: 40f, trapHp: 300f, trapDamageMin: 30f, trapDamageMax: 60f);
        SpawnObject(obj, Color.red);
    }

    // 9-7/9-8장(2026-07-27 추가) — 함정 해제 진행 막대/결과 문구가 함정 위치의 실제 비주얼
    // GameObject(트랜스폼 위치·자식 컴포넌트 포함)를 찾아야 해서 추가한 조회용 공개 메서드.
    public GameObject GetObjectVisual(Vector3Int pos)
    {
        if (objectGrid.TryGetValue(pos, out var obj) && objectVisuals.TryGetValue(obj, out var visual))
            return visual;
        return null;
    }

    public void CollectObject(Vector3Int pos)
    {
        // GameSession.SpawnObject가 생성한 비주얼은 GameSession.objectVisuals에 있다.
        // ObjectSpawner.CollectObject는 자신의 objectVisuals(비어있음)만 보므로 직접 파괴한다.
        if (objectGrid.TryGetValue(pos, out var objToRemove) && objectVisuals.TryGetValue(objToRemove, out GameObject visual))
        {
            UnityEngine.Object.Destroy(visual);
            objectVisuals.Remove(objToRemove);
        }
        _objectSpawner.CollectObject(pos);
    }

    // 시체 자동 소멸(사용자 요청, 2026-08-23) — 1분.
    public const float CorpseDespawnSeconds = 60f;

    private async UniTaskVoid DespawnCorpseAfterDelay(Vector3Int gridPos, InteractableObject corpse)
    {
        await UniTask.Delay(System.TimeSpan.FromSeconds(CorpseDespawnSeconds));
        // 그 사이 조사/PartyDeathSystem 등 다른 경로로 이미 치워졌거나, 같은 타일에 다른 오브젝트가
        // 새로 자리잡았을 수 있으므로 여전히 이 corpse 인스턴스가 그 자리에 있을 때만 제거한다.
        if (objectGrid.TryGetValue(gridPos, out var current) && current == corpse)
            CollectObject(gridPos);
    }

}









