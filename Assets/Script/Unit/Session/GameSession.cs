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
    private UnitRegistry _unitRegistry;
    private ObjectSpawner _objectSpawner;
    private Dictionary<InteractableObject, GameObject> objectVisuals = new Dictionary<InteractableObject, GameObject>();
    private PartyService _partyService;
    private CombatEventService _combatEventService;
    private DebugInputHandler _debugInputHandler;
    private BuildingManager _buildingManager;

    [Inject]
    public void Construct(UnitGenerate unitGenerate, ThreatTileRenderer threatTileRenderer, PropagationDebugVisualizer propagationDebugVisualizer, IObjectResolver resolver, CreateMap injectedMap, DataManager dataManager, UnitRegistry unitRegistry, ObjectSpawner objectSpawner, PartyService partyService, CombatEventService combatEventService, DebugInputHandler debugInputHandler, BuildingManager buildingManager)
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
        _debugInputHandler = debugInputHandler;
        _buildingManager = buildingManager;
        Instance = this;

        OnThreatCreated.Subscribe(data =>
        {
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
    public Dictionary<Vector3Int, Unit> unitGrid => _unitRegistry.unitGrid;
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
    private float updateTimer = 0f;

    public float currentGameSpeed = 1f;
    public bool isPaused = false;

    public void RegisterUnitPos(Unit u, Vector2Int pos)
    {
        _unitRegistry.RegisterUnitPos(u, pos);
        // 07문서 소리 스캔 최적화(2026-08-05) — PropagationSystem이 "방별 인류 후보"만 훑을 수
        // 있도록, 유닛 그리드와 동일한 지점(스폰/이동)에서 방 인덱스도 함께 갱신한다.
        if (u is Human human)
            PropagationSystem.UpdateHumanRoomIndex(human, u.currentFloor, cmap != null ? cmap.GetRoomIdAt(u.currentFloor, pos) : -1);
    }

    public void UnregisterUnitPos(Unit u, Vector2Int pos)
    {
        _unitRegistry.UnregisterUnitPos(u, pos);
        if (u is Human human)
            PropagationSystem.RemoveFromRoomIndex(human);
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
            if (_resolver != null)
            {
                _resolver.Resolve<UIManager>();
                Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, "GameSession: UIManager Resolved");
            }

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
                SpawnDoors();
                // 점령 관련(2026-07-27 신규): 모든 야생 방에 야생 몬스터 A 2마리씩 필수 배치.
                SpawnWildRoomGuards();
                // 게임을 시작하자마자 보스방에 던전 코어를 자동 생성한다(사용자 요청, 2026-07-23 최초 도입
                // → 2026-07-24 전용 오브젝트로 분리) — HumanWaveManager.MonitorWave()가 이 오브젝트를
                // "Loot" 태그(DungeonCoreTag 참고)로 발견해서 첫 웨이브부터 곧바로 목표로 추적하므로,
                // 유저가 매번 수동으로 O키를 눌러줄 필요가 없어진다.
                SpawnInitialDungeonCore();
                // 건축물·자원·유닛 생산 MVP(2026-07-27, 사용자 요청): 던전 1층 시작방(플레이어=몬스터
                // 진영 거점)에 자원 생산 건물(V키)과 유닛 생산 건물(B키)을 무상으로 하나씩 미리 깔아둔다.
                SpawnInitialBuildings();
                // 안개 시스템(2026-07-28, 사용자 요청): 위에서 스폰된 모든 초기 콘텐츠(야생 몬스터/
                // 던전 코어/건물)를 가리도록 깐다 — 각 방의 Room.FogRevealed 최종 상태를 여기서 먼저
                // 확정해야, 바로 다음의 SpawnTorches가 "안개 안 걷힌 방은 지금 스폰하지 않고 대기"를
                // 정확히 판단할 수 있다(사용자 요청, 2026-07-28 "안개가 있는 방에 토치 미리 생성하지
                // 말고, 안개 걷히고 나서 토치 생성하게 해줘" — 순서를 InitializeFogOfWar → SpawnTorches
                // 로 바꾼 이유).
                InitializeFogOfWar();
                // 횃불 배치(2026-07-28, 사용자 요청): 시작방을 제외한 모든 방(0층 포함, 모든 층 동일
                // 규칙)의 각 청크마다 하나씩(Prefabs/Torch.prefab, Light2D 포함). 안개가 안 걷힌 방은
                // 즉시 스폰하지 않고 대기열에 넣는다.
                SpawnTorches();
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

    // NativeRoutine 생명주기: Processor에서 UnRegister될 때(Dispose()→Finalize(), VContainer 컨테이너
    // 파괴 시) 한 번 호출됨 — ResourceManager/BuildingManager와 동일 관례. NativeRoutine은 OnDestroy가
    // 없어서 런타임에 만든 GameObject는 여기서 직접 정리해야 한다.
    public override async UniTask Finalize()
    {
        DestroyRoomLabelRoot();
        await base.Finalize();
    }

    // 부모-자식 관계에 기대지 않고 각 라벨을 직접 들고 있는 참조(_roomPopulationLabels)로 파괴한다.
    // 라벨은 이제 독립 루트가 아니라 층별 타일맵의 "Labels" 하위 그룹 자식이라(위 필드 주석 참고)
    // 별도 루트를 따로 파괴할 필요가 없다 — MapRoot_Grid/타일맵이 정리될 때 자연히 함께 정리됨.
    private void DestroyRoomLabelRoot()
    {
        foreach (var label in _roomPopulationLabels.Values)
        {
            if (label != null) UnityEngine.Object.Destroy(label.gameObject);
        }
        _roomPopulationLabels.Clear();
        _roomPopulationLabelText.Clear();
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

                        int startX = cx * 8;
                        int startY = cy * 8;

                        var min = roomMin[c.roomId];
                        var max = roomMax[c.roomId];
                        min.x = Mathf.Min(min.x, startX);
                        min.y = Mathf.Min(min.y, startY);
                        max.x = Mathf.Max(max.x, startX + 8);
                        max.y = Mathf.Max(max.y, startY + 8);
                        roomMin[c.roomId] = min;
                        roomMax[c.roomId] = max;

                        for (int tx = 0; tx < 8; tx++)
                        {
                            for (int ty = 0; ty < 8; ty++)
                            {
                                Vector3Int pos = new Vector3Int(cx * 8 + tx, cy * 8 + ty, currentFloor);
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

    // ══════════════════════════════════════════════════════════════════════
    // 안개 시스템(2026-07-28, 사용자 요청 "0층, 시작방과 양옆 방을 제외하고는 안개가 생겨. 이 안개는,
    // 인접 방으로 플레이어 진영 몬스터가 진입한 경험이 있어야지만 사라져.") — 순수 시각 오버레이라
    // 이동/시야/AI 판정 등 어떤 게임플레이 로직도 건드리지 않는다(Room.FogRevealed는 UI/시각 목적
    // 전용 플래그). 0층은 안개 개념 자체가 없고(인류 로비), 1층 이상은 그 층의 시작방(BuildRoomGrid
    // 직후 시점 RoomFaction==Player로 식별 — 아직 전투/점령 변화가 전혀 없는 순수 생성값)과 그 방과
    // Gate로 직접 연결된 인접 방만 처음부터 안개 없이 시작한다. 문 타일은 ApplyOccupationTint/
    // ChangeRoomColor와 동일한 선례(문이 있는 바닥은 점령색 칠도 안 함)를 따라 안개도 씌우지 않는다
    // (문은 이미 별도 스프라이트로 열림/닫힘을 표현).
    // ══════════════════════════════════════════════════════════════════════
    private readonly Dictionary<Room, List<GameObject>> _roomFogVisuals = new Dictionary<Room, List<GameObject>>();
    // 문/통로 안개(2026-07-28, 사용자 요청 "문이 있는 공간(복도)도 옆방중에 하나라도 안개가 있다면 다
    // 안개로 가리고") — 게이트 하나는 두 방을 잇는 통로라 어느 한 쪽 Room에도 배타적으로 속하지 않는다.
    // (floorIndex, min(roomA,roomB), max(roomA,roomB))로 게이트를 식별해 별도로 추적한다.
    private readonly Dictionary<(int floor, int roomA, int roomB), List<GameObject>> _gateFogVisuals = new Dictionary<(int, int, int), List<GameObject>>();
    private Sprite _fogSprite;
    private Sprite _fogBackingSprite;
    // "스윽 사라지게"(사용자 요청, 구체적 초 수 지정 없음) — 자리표시자, 나중에 조정 요청 오면 이
    // 상수만 바꾸면 됨.
    private const float FogFadeOutSeconds = 0.6f;
    // 후속 신고(2026-07-28, "안개를 통해 몬스터의 상태 보인다") — 위협타일(ThreatTileRenderer,
    // 999)이 안개(기존 900)보다 위라 공격 예고 셀이 뚫고 보였다. 이 값보다 확실히 위여야 유닛/
    // 오브젝트/라벨/위협타일 등 안개 아래 모든 것이 실제로 안 보인다. 안개 자체가 배경(Backing)+
    // 무늬(Pattern) 2겹이라 배경=이 값, 무늬=+1을 쓴다.
    private const int FogSortingOrder = 1000;
    // 후속 신고(2026-07-28, "안개 좀더 선명하게. 약간 투명해서 안에 다 비쳐보여") — obj/fog.png
    // 단독으로는 줄무늬 사이 틈으로 안이 비쳐서, 그 뒤에 불투명에 가까운 단색 배경 한 겹을 깔고
    // 그 위에 무늬를 얹는 2겹 구조로 바꿨다. 배경색 자체(RGB)는 임의 선택 — 조정 요청 오면 이
    // 상수만 바꾸면 됨.
    private static readonly Color FogBackingColor = new Color(0.05f, 0.05f, 0.08f, 0.95f);

    private void InitializeFogOfWar()
    {
        if (allRooms == null || cmap == null || cmap.map.floors == null) return;

        foreach (var room in allRooms)
        {
            if (room != null) room.FogRevealed = room.Floor == 0;
        }

        for (int floorIndex = 1; floorIndex < cmap.map.floors.Length; floorIndex++)
        {
            Room startRoom = null;
            foreach (var room in allRooms)
            {
                if (room != null && room.Floor == floorIndex && room.RoomFaction == FactionType.Player)
                {
                    startRoom = room;
                    break;
                }
            }
            if (startRoom == null) continue;
            startRoom.FogRevealed = true;

            Floor floor = cmap.map.floors[floorIndex];
            if (floor.gates == null) continue;
            foreach (Gate g in floor.gates)
            {
                int neighborId = -1;
                if (g.roomA == startRoom.RoomId) neighborId = g.roomB;
                else if (g.roomB == startRoom.RoomId) neighborId = g.roomA;
                if (neighborId < 0) continue;

                Room neighbor = FindRoomByFloorAndId(floorIndex, neighborId);
                if (neighbor != null) neighbor.FogRevealed = true;
            }
        }

        foreach (var room in allRooms)
        {
            if (room != null && !room.FogRevealed) SpawnFogForRoom(room);
        }

        // 문/통로 안개 + 방이 생성되지 않은 청크(2026-07-28, 사용자 요청 "문이 있는 공간(복도)도
        // 옆방중에 하나라도 안개가 있다면 다 안개로 가리고, 벽만 있는, 방이 생성되지 않은 청크도
        // 안개 씌워줘") — 룸 기준 루프가 끝나 모든 Room.FogRevealed가 최종 확정된 뒤에 실행해야
        // "양옆 다 걷혔는지" 판정이 정확하다.
        for (int floorIndex = 1; floorIndex < cmap.map.floors.Length; floorIndex++)
        {
            SpawnFogForEmptyChunks(floorIndex);

            Floor floor = cmap.map.floors[floorIndex];
            if (floor.gates == null) continue;
            foreach (Gate g in floor.gates)
            {
                Room roomA = FindRoomByFloorAndId(floorIndex, g.roomA);
                Room roomB = FindRoomByFloorAndId(floorIndex, g.roomB);
                bool aRevealed = roomA == null || roomA.FogRevealed;
                bool bRevealed = roomB == null || roomB.FogRevealed;
                if (!aRevealed || !bRevealed) SpawnFogForGate(floorIndex, g);
            }

            RebuildFloorFogShadowCasters(floorIndex);
        }
    }

    private Room FindRoomByFloorAndId(int floorIndex, int roomId)
    {
        if (allRooms == null || roomId < 0) return null;
        foreach (var r in allRooms)
            if (r != null && r.Floor == floorIndex && r.RoomId == roomId) return r;
        return null;
    }

    private static (int floor, int roomA, int roomB) GateKey(int floorIndex, Gate g)
    {
        int a = Mathf.Min(g.roomA, g.roomB);
        int b = Mathf.Max(g.roomA, g.roomB);
        return (floorIndex, a, b);
    }

    private HashSet<Vector2Int> CollectDoorTilesForFloor(int floorIndex)
    {
        var doorTiles = new HashSet<Vector2Int>();
        if (cmap == null || cmap.map.floors == null || floorIndex < 0 || floorIndex >= cmap.map.floors.Length) return doorTiles;

        Floor floor = cmap.map.floors[floorIndex];
        if (floor.gates == null) return doorTiles;
        foreach (var gate in floor.gates)
            foreach (var row in GetGateDoorTiles(gate))
                foreach (var tile in row)
                    doorTiles.Add(tile);
        return doorTiles;
    }

    // SpawnObject의 단색 폴백 텍스처 생성과 동일한 방식(사용자 요청 "안개 좀더 선명하게, 안 비쳐
    // 보이게") — 불투명에 가까운 배경 한 장을 미리 만들어두고 모든 안개 타일이 공유해서 쓴다.
    private Sprite EnsureFogBackingSprite()
    {
        if (_fogBackingSprite != null) return _fogBackingSprite;

        Texture2D tex = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        _fogBackingSprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        return _fogBackingSprite;
    }

    // 안개 스프라이트/배경 로드 실패 시 false. 성공하면 backingSprite와 스케일 4종을 채워준다 —
    // SpawnFogForRoom/SpawnFogForGate/SpawnFogForEmptyChunks가 모두 재사용.
    private bool TryPrepareFogAssets(out Sprite backingSprite, out float patScaleX, out float patScaleY, out float backScaleX, out float backScaleY)
    {
        backingSprite = null; patScaleX = patScaleY = backScaleX = backScaleY = 1f;
        if (_fogSprite == null) _fogSprite = Resources.Load<Sprite>("obj/fog");
        if (_fogSprite == null)
        {
            LogHelper.Warning(LogHelper.GAME, "TryPrepareFogAssets: Resources.Load<Sprite>(\"obj/fog\")가 null입니다 — Import 설정(Sprite Mode) 확인 필요.");
            return false;
        }
        backingSprite = EnsureFogBackingSprite();

        Vector2 patternWorldSize = _fogSprite.bounds.size;
        patScaleX = patternWorldSize.x > 0f ? 1f / patternWorldSize.x : 1f;
        patScaleY = patternWorldSize.y > 0f ? 1f / patternWorldSize.y : 1f;
        Vector2 backingWorldSize = backingSprite.bounds.size;
        backScaleX = backingWorldSize.x > 0f ? 1f / backingWorldSize.x : 1f;
        backScaleY = backingWorldSize.y > 0f ? 1f / backingWorldSize.y : 1f;
        return true;
    }

    // 안개 타일 하나(배경+무늬 2겹) 생성 — SpawnFogForRoom/SpawnFogForGate/SpawnFogForEmptyChunks 공용.
    // 셰도우 캐스트는 여기서 타일 단위로 안 붙인다(사용자 요청, 2026-07-28) — 문 안개는
    // SpawnFogForGate가 타일들을 다 모은 뒤 윤곽선 하나짜리 캐스터를 별도로 만든다(벽과 동일한
    // MapRandering.TraceContours 기법 — 타일마다 독립된 캐스터를 붙이면 그 이음새에서 빛이 샌다).
    private GameObject SpawnFogTile(int x, int y, Vector3 offset, Transform parentGroup, Sprite backingSprite,
        float patScaleX, float patScaleY, float backScaleX, float backScaleY, string namePrefix)
    {
        GameObject go = new GameObject($"Fog_{namePrefix}_{x}_{y}");
        if (parentGroup != null) go.transform.SetParent(parentGroup);
        go.transform.position = new Vector3(x + 0.5f, y + 0.5f, 0f) + offset;

        // 배경(불투명에 가까움, 아래) + 무늬(위) 2겹 — 무늬 텍스처의 줄무늬 틈으로 안이 비쳐 보이지
        // 않도록 배경이 항상 먼저 완전히 가린다.
        GameObject backingGo = new GameObject("Backing");
        backingGo.transform.SetParent(go.transform, false);
        SpriteRenderer backingSr = backingGo.AddComponent<SpriteRenderer>();
        backingSr.sprite = backingSprite;
        backingSr.color = FogBackingColor;
        backingSr.sortingOrder = FogSortingOrder;
        backingGo.transform.localScale = new Vector3(backScaleX, backScaleY, 1f);

        GameObject patternGo = new GameObject("Pattern");
        patternGo.transform.SetParent(go.transform, false);
        SpriteRenderer patternSr = patternGo.AddComponent<SpriteRenderer>();
        patternSr.sprite = _fogSprite;
        patternSr.sortingOrder = FogSortingOrder + 1;
        patternGo.transform.localScale = new Vector3(patScaleX, patScaleY, 1f);

        return go;
    }

    private void SpawnFogForRoom(Room room)
    {
        if (room == null || room.Floor < 0) return;
        if (!TryPrepareFogAssets(out Sprite backingSprite, out float patScaleX, out float patScaleY, out float backScaleX, out float backScaleY)) return;

        HashSet<Vector2Int> doorTiles = CollectDoorTilesForFloor(room.Floor);
        Vector3 offset = (mapRandering != null && mapRandering.floorOffsets != null && room.Floor < mapRandering.floorOffsets.Length)
            ? mapRandering.floorOffsets[room.Floor] : Vector3.zero;
        Transform fogGroup = GetFloorCategoryGroup(room.Floor, "Fog");

        var tiles = new List<GameObject>();
        for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
        {
            for (int y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
            {
                // 청크 전체를 가린다(사용자 요청 "바닥만 가리는게 아니라, 청크 전체 가려야됨") — 벽
                // 타일도 더 이상 건너뛰지 않는다. 문/통로 타일만 예외 — SpawnFogForGate가 두 방의
                // 안개 상태를 함께 보고 따로 처리한다(아래 참고).
                var p2 = new Vector2Int(x, y);
                if (doorTiles.Contains(p2)) continue;
                // Bounds는 사각 경계값이라 방이 L자 등 비직사각형이면 다른 방 타일까지 포함할 수 있다
                // — roomGrid로 실제 소유 방을 재확인해 그 방 타일만 안개로 덮는다.
                if (!roomGrid.TryGetValue(new Vector3Int(x, y, room.Floor), out Room owner) || owner != room) continue;

                tiles.Add(SpawnFogTile(x, y, offset, fogGroup, backingSprite, patScaleX, patScaleY, backScaleX, backScaleY, room.RoomName));
            }
        }

        if (tiles.Count > 0) _roomFogVisuals[room] = tiles;
    }

    // 문/통로 안개(2026-07-28, 사용자 요청 "문이 있는 공간(복도)도 옆방중에 하나라도 안개가 있다면 다
    // 안개로 가리고") — 게이트가 잇는 두 방 중 하나라도 아직 FogRevealed==false면 그 게이트의 문
    // 타일들(GetGateDoorTiles) 전체를 안개로 덮는다. 방이 없는 쪽(neighborId가 실제 Room을 못 찾는
    // 경우, 이론상 발생 안 함)은 "이미 걷힌 것"으로 간주해 반대쪽 방 상태만으로 판단한다.
    private void SpawnFogForGate(int floorIndex, Gate g)
    {
        if (!TryPrepareFogAssets(out Sprite backingSprite, out float patScaleX, out float patScaleY, out float backScaleX, out float backScaleY)) return;

        Vector3 offset = (mapRandering != null && mapRandering.floorOffsets != null && floorIndex < mapRandering.floorOffsets.Length)
            ? mapRandering.floorOffsets[floorIndex] : Vector3.zero;
        Transform fogGroup = GetFloorCategoryGroup(floorIndex, "Fog");

        var tiles = new List<GameObject>();
        foreach (var row in GetGateDoorTiles(g))
            foreach (var pos in row)
                tiles.Add(SpawnFogTile(pos.x, pos.y, offset, fogGroup, backingSprite, patScaleX, patScaleY, backScaleX, backScaleY, "Gate"));

        if (tiles.Count > 0) _gateFogVisuals[GateKey(floorIndex, g)] = tiles;
    }

    // 방이 생성되지 않은 청크(2026-07-28, 사용자 요청 "벽만 있는, 방이 생성되지 않은 청크도 안개
    // 씌워줘") — CreateMap.Chunks.roomId < 0인 청크는 BuildRoomGrid가 아예 roomGrid에 등록하지
    // 않아(어떤 Room에도 안 속함) SpawnFogForRoom 루프에 걸리지 않는다. 이런 청크는 유닛이 물리적으로
    // 들어갈 방법이 없어(벽뿐이거나 방 자체가 없음) 해제 트리거가 있을 수 없으므로 영구 안개로 둔다
    // (별도 딕셔너리 추적 없이 스폰만 하고 끝 — 다른 타일 오브젝트들처럼 씬 파괴 시 자연 정리됨).
    private void SpawnFogForEmptyChunks(int floorIndex)
    {
        if (cmap == null || cmap.map.floors == null || floorIndex <= 0 || floorIndex >= cmap.map.floors.Length) return;
        Floor floor = cmap.map.floors[floorIndex];
        if (floor.chunks == null) return;
        if (!TryPrepareFogAssets(out Sprite backingSprite, out float patScaleX, out float patScaleY, out float backScaleX, out float backScaleY)) return;

        Vector3 offset = (mapRandering != null && mapRandering.floorOffsets != null && floorIndex < mapRandering.floorOffsets.Length)
            ? mapRandering.floorOffsets[floorIndex] : Vector3.zero;
        Transform fogGroup = GetFloorCategoryGroup(floorIndex, "Fog");

        int w = floor.config.width, h = floor.config.height;
        for (int cx = 0; cx < w; cx++)
        {
            for (int cy = 0; cy < h; cy++)
            {
                if (floor.chunks[cx, cy].roomId >= 0) continue;
                for (int tx = 0; tx < 8; tx++)
                    for (int ty = 0; ty < 8; ty++)
                        SpawnFogTile(cx * 8 + tx, cy * 8 + ty, offset, fogGroup, backingSprite, patScaleX, patScaleY, backScaleX, backScaleY, "Void");
            }
        }
    }

    // 벽 + "아직 안 걷힌 안개" 전체를 하나의 격자로 합쳐서 한 번에 윤곽선을 뽑는다(사용자 요청,
    // 2026-07-28 "지금 문에잇는 안개만 섀도캐스팅 박혀있어. 문+ 벽 섀도우캐스팅과 겹치는 안개 모두
    // 한번에 해서 구워줘"). 안개(방/문/빈 청크)를 벽과 따로따로 셰도우 캐스팅하면 방 경계에서 벽의
    // 자체 셰도우와 거의 겹치는 별개의 선이 하나 더 생길 뿐이라 눈에 띄는 차이가 없었다 — 벽이 이미
    // 막고 있는 경계를 안개가 다시 막아봤자 티가 안 남. 대신 벽 마스크와 "안 걷힌 안개" 마스크를
    // OR로 합친 뒤 그 결과를 통째로 외곽선 추적하면, 문(벽이 없는 구간)처럼 벽만으로는 안 막히는
    // 지점까지 안개가 자연스럽게 이어 붙어 하나의 연속된 경계가 된다(이음새 없음).
    //
    // 안개는 방이 걷힐 때마다 바뀌는 동적 상태라, 벽처럼 한 번만 굽고 끝낼 수 없다 — 그 방이 있는
    // 층 전체를 RevealRoomFog/InitializeFogOfWar에서 다시 구워(재계산) 갈아 끼운다. 잦은 일이
    // 아니라(웨이브 진행에 따라 방 하나 걷힐 때만) 층 전체를 매번 다시 훑어도 성능 문제는 없다.
    private readonly Dictionary<int, List<GameObject>> _floorFogShadowCasters = new Dictionary<int, List<GameObject>>();

    private bool[,] BuildStillFoggedMask(int floorIndex, int worldW, int worldH)
    {
        var mask = new bool[worldW, worldH];
        Floor floor = cmap.map.floors[floorIndex];

        for (int x = 0; x < worldW; x++)
        {
            for (int y = 0; y < worldH; y++)
            {
                if (roomGrid.TryGetValue(new Vector3Int(x, y, floorIndex), out Room owner) && owner != null)
                    mask[x, y] = !owner.FogRevealed;
                else
                    mask[x, y] = true; // 방이 없는 칸(빈 청크) — 영구 안개
            }
        }

        // 게이트(문) 타일은 두 방 중 하나라도 안 걷혔으면 안개 — 각 타일이 속한 청크의 개별 방
        // 판정과 별개로 덮어쓴다(SpawnFogForGate와 동일한 규칙).
        if (floor.gates != null)
        {
            foreach (var g in floor.gates)
            {
                Room roomA = FindRoomByFloorAndId(floorIndex, g.roomA);
                Room roomB = FindRoomByFloorAndId(floorIndex, g.roomB);
                bool aRevealed = roomA == null || roomA.FogRevealed;
                bool bRevealed = roomB == null || roomB.FogRevealed;
                bool stillFogged = !aRevealed || !bRevealed;

                foreach (var row in GetGateDoorTiles(g))
                    foreach (var pos in row)
                        if (pos.x >= 0 && pos.x < worldW && pos.y >= 0 && pos.y < worldH)
                            mask[pos.x, pos.y] = stillFogged;
            }
        }

        return mask;
    }

    // 재구성 시 빛이 한 프레임 새는 문제 수정(사용자 확인, 2026-07-28 "새로 구울때 한번 번쩍거리면서
    // 빛이 새는데") — 원인은 순서: 기존 캐스터를 먼저 Destroy()하면 실제 파괴/등록 해제는 그 프레임
    // 렌더링 전에 일어나는데, 새로 만든 ShadowCaster2D는 자기 Update()가 최소 한 번 돌아야 셰도우
    // 그룹에 실제로 등록된다(ShadowCaster2D.Update() 내부에서 등록 — Awake 시점엔 아직 미등록). 즉
    // "새 걸 등록하기 전에 기존 걸 지우는" 순간 사이에 이 층 전체가 무방비 상태인 프레임이 한 번
    // 생겨서 그 프레임에 빛이 새어 보였다. 그래서 새 캐스터를 먼저 만들어 등록될 시간을 확실히 준
    // 뒤에(2프레임 대기) 기존 걸 지우는 순서로 바꿨다 — 겹치는 몇 프레임 동안 신/구 캐스터가 같이
    // 있어도 중복으로 막아줄 뿐 문제 없다.
    private void RebuildFloorFogShadowCasters(int floorIndex)
    {
        if (cmap == null || cmap.map.floors == null || floorIndex < 0 || floorIndex >= cmap.map.floors.Length) return;
        Floor floor = cmap.map.floors[floorIndex];
        if (floor.chunks == null) return;

        bool[,] isWall = MapRandering.BuildWallMask(ref floor, out int worldW, out int worldH);
        bool[,] stillFogged = BuildStillFoggedMask(floorIndex, worldW, worldH);

        bool[,] combined = new bool[worldW, worldH];
        for (int x = 0; x < worldW; x++)
            for (int y = 0; y < worldH; y++)
                combined[x, y] = isWall[x, y] || stillFogged[x, y];

        Transform fogGroup = GetFloorCategoryGroup(floorIndex, "Fog");
        var loops = MapRandering.TraceContours(combined, worldW, worldH);
        var created = new List<GameObject>();
        MapRandering.CreateEdgeShadowCasters(fogGroup, loops, "FloorShadowCaster", created);

        List<GameObject> oldCasters = _floorFogShadowCasters.TryGetValue(floorIndex, out var prev) ? prev : null;
        _floorFogShadowCasters[floorIndex] = created;

        if (oldCasters != null && oldCasters.Count > 0)
            DestroyAfterFramesAsync(oldCasters).Forget();
    }

    private async UniTaskVoid DestroyAfterFramesAsync(List<GameObject> toDestroy)
    {
        await UniTask.DelayFrame(2);
        foreach (var go in toDestroy)
            if (go != null) UnityEngine.Object.Destroy(go);
    }

    // 안개 해제(2026-07-28, 사용자 요청 "인접 방으로 플레이어 진영 몬스터가 진입한 경험이 있어야지만
    // 사라져") — UnitFunction.SyncRoomAffiliation이 플레이어 진영 몬스터의 방 최초 입장을 감지하면
    // 호출한다. Room.FogRevealed는 한번 true가 되면 다시 false로 안 돌아간다(영구 해제).
    public void RevealRoomFog(Room room)
    {
        if (room == null || room.FogRevealed) return;
        room.FogRevealed = true;

        if (_roomFogVisuals.TryGetValue(room, out List<GameObject> tiles) && tiles != null && tiles.Count > 0)
        {
            _roomFogVisuals.Remove(room);
            FadeOutAndDestroyFogAsync(tiles).Forget();
        }

        // 횃불 지연 스폰(2026-07-28, 사용자 요청 "안개가 있는 방에 토치 미리 생성하지 말고, 안개
        // 걷히고 나서 토치 생성하게 해줘") — 이 방을 위해 대기 중이던 횃불이 있으면 지금 배치한다.
        SpawnPendingTorchesForRoom(room);

        TryRevealAdjacentGates(room);

        // 이 방(과 인접 게이트)의 FogRevealed가 방금 바뀌었으니 벽+안개 통합 셰도우도 다시 굽는다
        // (RebuildFloorFogShadowCasters 주석 참고).
        RebuildFloorFogShadowCasters(room.Floor);
    }

    // 안개 해금 규칙 변경(2026-07-28, 사용자 요청 "안개 해금이 잘 안돼. 규칙을 바꾸자. 플레이어
    // 유닛이 해당 방을 점령한 적이 있으면 인접 방의 안개가 사라지도록 처리하자") — 기존 "유닛이 방에
    // 물리적으로 들어오면 그 방 자체의 안개가 사라짐"(RevealRoomFog, UnitFunction.SyncRoomAffiliation
    // 트리거)은 신뢰도가 낮았다고 판단해(RoomConfinedMovement로 방 밖 자율 이동이 막혀 있고, 플레이어
    // 명령도 이미 소유/인접 방으로만 제한돼 있어 "새 방에 처음 들어가는" 순간 자체가 드물게만 발생)
    // 그대로 안전장치로 남겨두고, 훨씬 확실한 이벤트인 "점령"(OffenseProcessor의
    // TryResolveRoomOwnership/TryClaimEmptyRoomOnEntry/OnOffenseSuccess 세 경로가 Room.RoomFaction을
    // Player로 확정하는 순간)을 새 트리거로 추가한다. 점령된 방 자기 자신과, 그 방과 Gate로 직접
    // 연결된 인접 방들의 안개를 함께 걷는다(2칸 이상 떨어진 방까지 한꺼번에 열리진 않음 — 정확히
    // "인접 방"까지만).
    public void RevealFogAroundCapturedRoom(Room room)
    {
        if (room == null) return;
        RevealRoomFog(room);

        if (cmap == null || cmap.map.floors == null) return;
        if (room.Floor < 0 || room.Floor >= cmap.map.floors.Length) return;
        Floor floor = cmap.map.floors[room.Floor];
        if (floor.gates == null) return;

        foreach (Gate g in floor.gates)
        {
            if (g.roomA != room.RoomId && g.roomB != room.RoomId) continue;
            int neighborId = g.roomA == room.RoomId ? g.roomB : g.roomA;
            Room neighbor = FindRoomByFloorAndId(room.Floor, neighborId);
            if (neighbor != null) RevealRoomFog(neighbor);
        }
    }

    // 문/통로 안개 해제 — room이 막 걷혔을 때, 그 room과 맞닿은 게이트들 중 반대쪽 방도 이미 걷혀
    // 있으면(즉 양쪽 다 FogRevealed) 그 게이트의 안개도 같이 걷는다. 아직 반대쪽이 안 걷혔으면
    // 그대로 둔다("옆방중에 하나라도 안개가 있다면 다 안개로 가리고").
    private void TryRevealAdjacentGates(Room room)
    {
        if (room == null || cmap == null || cmap.map.floors == null) return;
        if (room.Floor < 0 || room.Floor >= cmap.map.floors.Length) return;
        Floor floor = cmap.map.floors[room.Floor];
        if (floor.gates == null) return;

        foreach (Gate g in floor.gates)
        {
            if (g.roomA != room.RoomId && g.roomB != room.RoomId) continue;
            int neighborId = g.roomA == room.RoomId ? g.roomB : g.roomA;
            Room neighbor = FindRoomByFloorAndId(room.Floor, neighborId);
            bool neighborRevealed = neighbor == null || neighbor.FogRevealed;
            if (!neighborRevealed) continue;

            var key = GateKey(room.Floor, g);
            if (_gateFogVisuals.TryGetValue(key, out List<GameObject> tiles) && tiles != null && tiles.Count > 0)
            {
                _gateFogVisuals.Remove(key);
                FadeOutAndDestroyFogAsync(tiles).Forget();
            }
        }
    }

    // "스윽 사라지게"(사용자 요청) — SceneTransitionFade.FadeAsync와 동일한 UniTask 알파 페이드 관례.
    // 안개 타일 하나가 배경/무늬 2겹(서로 다른 시작 알파값)이라, 각 렌더러의 "시작 알파"를 미리
    // 찍어두고 거기서부터 0으로 보간한다(전부 1→0으로 고정하면 배경(0.95)이 페이드 시작 순간 잠깐
    // 더 진해지는 튐이 생김).
    private async UniTaskVoid FadeOutAndDestroyFogAsync(List<GameObject> tiles)
    {
        var renderers = new List<SpriteRenderer>();
        var startAlphas = new List<float>();
        foreach (var go in tiles)
        {
            if (go == null) continue;
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>())
            {
                renderers.Add(sr);
                startAlphas.Add(sr.color.a);
            }
        }

        float t = 0f;
        while (t < FogFadeOutSeconds)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / FogFadeOutSeconds);
            for (int i = 0; i < renderers.Count; i++)
            {
                var sr = renderers[i];
                if (sr == null) continue;
                Color c = sr.color;
                c.a = Mathf.Lerp(startAlphas[i], 0f, progress);
                sr.color = c;
            }
            await UniTask.Yield();
        }

        foreach (var go in tiles)
            if (go != null) UnityEngine.Object.Destroy(go);
    }

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
    // 있다 — Processor.OnApplicationQuit()(실제 Unity 콜백)로 한 번 더 정리한다. DestroyRoomLabelRoot()는
    // 중복 호출해도 안전(Unity 파괴된 오브젝트 == null 오버로드).
    public override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
        DestroyRoomLabelRoot();
    }

    // Processor가 등록된 Routine들을 순회하며 매 프레임 호출함(기존 Update()와 동일한 역할)
    public override void UpdateProcess()
    {
        HandleDebugInput();
        
        _offenseProcessor?.UpdateProcess();

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

    private void HandleDebugInput()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.hKey.wasPressedThisFrame) OnKeyDown_H();
            if (Keyboard.current.kKey.wasPressedThisFrame) OnKeyDown_K();
            // O(루팅 오브젝트)/P(함정)은 InputManager의 배치 고스트 모드가 담당한다(원하는 위치를 직접
            // 골라서 놓기 위함, 2026-07-22/23) — GameSession.SpawnLootObjectAt/SpawnTrapAt 참고. M(몬스터
            // 즉시 배치)은 건축물·자원·유닛 생산 MVP(2026-07-27)로 완전히 대체되어 삭제됨 — B/V키 참고.
        }
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

            // 점령 전환 MVP(2026-07-27, 사용자 요청): 방 소유 진영의 마지막 유닛이 다른 진영에게 죽으면
            // 그 방은 죽인 진영 소속으로 전환된다. OnDeath 직후(포지션이 아직 유효한 시점)에 확인한다.
            _offenseProcessor?.TryFlipRoomOwnershipOnDeath(u);

            // 문 닫힘 시스템(2026-07-28, 사용자 요청) — 이 유닛의 죽음으로 방이 "정리된 상태"가 됐을
            // 수 있으니 인접 문 개방 조건을 다시 확인한다. u.currentRoom(유닛 배치 시스템 전용 필드)은
            // 야생 몬스터에서 항상 null이라(SyncRoomAffiliation이 야생을 대상에서 제외) 쓸 수 없어,
            // OffenseProcessor와 동일하게 roomGrid를 위치로 직접 조회한다.
            if (roomGrid.TryGetValue(new Vector3Int(u.position.x, u.position.y, u.currentFloor), out Room deadUnitRoom))
                RefreshRoomGateStates(deadUnitRoom);

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
        }

        if (u != null) CheckPartyWaveState(u);
        if (_unitGenerate != null && u != null)
        {
            _unitGenerate.RemoveVisual(u);
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

        u.JudgeState();
        Vector2Int oldPos = u.position;
        string oldLabel = u.fsm.GetLabel(u);
        Dir oldDir = u.currentDir;
        u.ExecuteAction();
        string newLabel = u.fsm.GetLabel(u);

        bool stateChanged = oldPos != u.position || oldLabel != newLabel || oldDir != u.currentDir;

        if (oldPos != u.position)
        {
            UnregisterUnitPos(u, oldPos);
            RegisterUnitPos(u, u.position);
            TriggerTrapIfStepped(u, trapInteractionBefore);

            // 오펜스 자동 트리거: PlayerMonster가 야생 방에 진입하면 즉시 오펜스 시작
            if (u.IsPlayerMonsterFaction)
                TryTriggerOffenseForUnit(u);
                
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

    public void OnKeyDown_H()
    {
        if (_unitGenerate == null) return;

        UnitType[] types = { new Knight() };
        Vector2Int[] offsets = { new Vector2Int(0, 0) };

        int floorIdx = 1;
        UnitType type = types[0];

        Vector2Int spawnPos = GetRandomStartRoomPos(type.footprint, floorIdx);

        for (int i = 0; i < types.Length; i++)
        {
            Vector2Int pos = spawnPos + offsets[i];

            if (!_unitGenerate.IsAreaClear(pos, types[i].footprint, floorIdx))
                pos = _unitGenerate.GetRandomFloorPos(types[i].footprint, floorIdx);

            Human human = _unitGenerate.GenerateUnitAtPos<Human>(types[i], pos, floorIdx);
            units.Add(human);
            human.CombatState.State.actionCooldown = human.BaseStat.walkSpeed > 0f
                ? UnityEngine.Random.Range(0f, 1f / human.BaseStat.walkSpeed) : UnityEngine.Random.Range(0f, 1f);

            RegisterUnitPos(human, human.position);
        }
    }

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

                for (int tx = 0; tx < 8; tx++)
                {
                    for (int ty = 0; ty < 8; ty++)
                    {
                        Vector2Int pos = new Vector2Int(cx * 8 + tx, cy * 8 + ty);

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

    public void OnKeyDown_K()
    {
        if (_unitGenerate == null) return;

        UnitType[] types = { new Archer() };
        Vector2Int[] offsets = { new Vector2Int(0, 0) };

        int floorIdx = 1;
        UnitType type = types[0];

        Vector2Int spawnPos = GetRandomStartRoomPos(type.footprint, floorIdx);

        for (int i = 0; i < types.Length; i++)
        {
            Vector2Int pos = spawnPos + offsets[i];

            if (!_unitGenerate.IsAreaClear(pos, types[i].footprint, floorIdx))
                pos = _unitGenerate.GetRandomFloorPos(types[i].footprint, floorIdx);

            Human human = _unitGenerate.GenerateUnitAtPos<Human>(types[i], pos, floorIdx);
            human.FactionBehavior = new HumanFactionBehavior();
            units.Add(human);
            human.CombatState.State.actionCooldown = human.BaseStat.walkSpeed > 0f
                ? UnityEngine.Random.Range(0f, 1f / human.BaseStat.walkSpeed) : UnityEngine.Random.Range(0f, 1f);

            RegisterUnitPos(human, human.position);
            LogHelper.Log(LogHelper.GAME, $"Generated Archer (Human Faction) at Floor {human.currentFloor}, {human.position}");
        }
    }

    public void SpawnObject(InteractableObject obj, Color color, float rotationZDegrees = 0f)
    {
        if (objectGrid.ContainsKey(obj.Position)) return;

        objectGrid[obj.Position] = obj;
        LogHelper.Log(LogHelper.GAME, $"Generated {obj.Id} at Floor {obj.Position.z}, {new Vector2Int(obj.Position.x, obj.Position.y)} with Tags: [{string.Join(", ", obj.Tags)}]");

        GameObject visual = new GameObject(obj.Id);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();

        // 태그별 실제 아트 스프라이트 배정(사용자 요청, 2026-07-23) — 시체는 colapse.png, 함정은
        // trap.png. Loot 계열은 던전 코어(DungeonCore, 보스방 자동 배치 전용)만 core.png를 그대로
        // 쓰고, 그 외 일반 루팅 오브젝트(O키로 수동 생성)는 obj1.png로 바꿨다(사용자 요청,
        // 2026-07-24) — DungeonCoreTag가 "Loot"를 포함하는 하위 태그라 DungeonCore 여부를 먼저
        // 확인해야 한다. Resources.Load 실패(아직 없는 태그 등) 시에만 기존 도형 폴백(함정=삼각형,
        // 그 외=단색 사각형)으로 되돌아간다.
        bool isTrap = obj.Tags != null && obj.Tags.Exists(t => t.Contains("Trap"));
        bool isCorpse = obj.Tags != null && obj.Tags.Exists(t => t.Contains("Corpse"));
        bool isDungeonCore = obj.Tags != null && obj.Tags.Exists(t => t.Contains("DungeonCore"));
        // 03문서 7-3장(2026-07-27) — 리더 전용 조사용 "코어"도 기존 던전 코어와 같은 아트를 그대로
        // 쓴다(사용자 요청 "스프라이트도 기존에 쓰던 던전코어 스프라이트 이용").
        bool isCoreOnly = obj.Tags != null && obj.Tags.Contains(CoreTag);
        bool isLoot = obj.Tags != null && obj.Tags.Exists(t => t.Contains("Loot"));
        // 문 시스템(2026-07-27) — 기본적으로 열린 상태(door_open) 스프라이트로 그린다. 닫힌 상태
        // (obj/door_closed)는 아직 여닫는 기능이 없어 안 쓰지만 에셋은 이미 있음(추후 확장용).
        bool isDoor = obj.Tags != null && obj.Tags.Contains(DoorTag);

        Sprite sprite = null;
        if (isTrap) sprite = Resources.Load<Sprite>("obj/trap");
        else if (isCorpse) sprite = Resources.Load<Sprite>("obj/colapse");
        else if (isDungeonCore || isCoreOnly) sprite = Resources.Load<Sprite>("obj/core");
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
        // sr.sprite가 바뀌는데(ApplyGateDoorVisual), ShadowCaster2D의 SpriteRenderer 프로바이더가
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

    // 던전 코어 태그 — 일반 루팅 오브젝트("Object/Passable/Loot")의 하위 태그로 둬서, 기존 Loot
    // 판정(HumanWaveManager 목표 탐지/GameSession 스프라이트 선택 — 전부 Tags.Contains("Loot")로
    // 검사한다)을 코드 변경 없이 그대로 재사용한다. O키/InputManager 고스트 배치 등 수동 생성 경로는
    // 이 태그를 만들지 않는다 — 오직 SpawnInitialDungeonCore()로만, 보스방에 자동으로 하나 배치된다
    // (사용자 요청, 2026-07-24 "따로 생성할 수 없고 보스방에만 자동 배치").
    private const string DungeonCoreTag = "Object/Passable/Loot/DungeonCore";

    // 03문서 7-3장(2026-07-27, 사용자 요청 "코어에 대해서, 통합하자") — 보스방 자동배치·웨이브 목표라는
    // 기존 던전 코어의 정체성은 그대로 두고, 그 위에 7-3장 "코어" 정확 일치 태그를 추가로 얹었다.
    // "Object/Passable/Core"가 붙으면: (1) Human.ComputeInvestigateTarget이 이 오브젝트를 일반 조사
    // 후보에서 제외하고(더 이상 아무 인류나 즉시 조사·회수하지 않음), (2) UnitFunction.CastRay가
    // PerceptionTargetKind.Core로 분류해 CorePartySystem.OnCoreDiscovered(파티 전파 → 리더 전용
    // 발견~조사 흐름, TacticalFSMState.CanContinueCore 등)를 태운다. HumanWaveManager의 웨이브 목표
    // 추적·운반·탈출(Carried/Secured) 메커니즘은 여전히 Loot 하위 태그만 보고 동작하므로 손대지 않고
    // 그대로 둔다(사용자 요청 "다른 코어의 기능은 그대로 둔채") — 즉 리더가 먼저 조사를 마치든 말든
    // 파티는 기존과 동일하게 이 오브젝트를 웨이브 목표로 들고 나갈 수 있고, 리더의 조사는 그 위에
    // 병행되는 별개 절차로 결합된다.
    private const string CoreTag = "Object/Passable/Core";

    // 문 시스템(2026-07-27, 사용자 요청) — 지나갈 수 있는 오브젝트. 이동 판정(UnitFunction.CanMove/
    // AStarMovement.IsTileWalkable)은 objectGrid를 아예 안 보고 Tile.isStructureExist/Wall만 확인하므로
    // (SpawnObject가 이 필드를 건드리지 않음), 다른 오브젝트들과 마찬가지로 별도 처리 없이 이미
    // 통행 가능하다.
    private const string DoorTag = "Object/Passable/Door";

    // 사용자 정정(2026-07-28, "문이 있는 자리가 가장 최우선이며, 문이 있는 자리에는 오브젝트 배치
    // 불가능. 몬스터 배치도 불가능(이동만 가능)") — 문은 SpawnDoors()가 다른 오브젝트/유닛보다 먼저
    // 깔아 objectGrid를 선점하므로, 그 뒤에 오는 모든 "이 타일에 뭔가 놓아도 되는지" 판정이 이 메서드로
    // 문 타일을 걸러내야 한다. BuildingManager.CanInstallAt(건물 배치)과 UnitGenerate.IsAreaClear(유닛/
    // 몬스터 스폰 위치 판정)가 호출한다 — 대부분의 이동(AStarMovement 등)은 여전히 objectGrid를 보지
    // 않으므로 문을 그냥 통과할 수 있고, 이 메서드는 원래 "배치"만 막는 용도였다. 다만 2026-07-28
    // 사용자 요청("배회하는 야생 몬스터가 문이 있는 공간을 돌아다니지 못하게")으로 RoomConfinedMovement
    // (야생/일부 플레이어 몬스터의 자율 이동)도 이 메서드를 호출해 문 타일 자체를 walkable에서 제외한다.
    public bool IsDoorTile(Vector3Int pos)
    {
        return objectGrid.TryGetValue(pos, out InteractableObject obj) &&
               obj.Tags != null && obj.Tags.Contains(DoorTag);
    }

    // 문 시스템(2026-07-27, 사용자 요청 "모든 방과 방 사이 통로에 문이 일렬로 설치") — CreateMap이
    // 생성 단계에서 이미 기록해 둔 Floor.gates(방 연결 통로: 청크 좌표+폭+수평/수직)를 그대로 재사용해
    // 실제 통로 타일 좌표를 되짚는다. 새로 통로를 탐색하는 로직을 만들 필요가 없다 — 모든 층을 순회.
    //
    // 사용자 정정(2026-07-28, "문이 한쪽 공간에 몰려서... 문을 양쪽에 달자, 2*1로 존재하던 문을 2*2로")
    // — 기존에는 통로 양 끝(청크 A/B 경계) 중 B쪽 문턱 한 줄에만 문을 심어서, 방마다 문이 한쪽에만
    // 몰려 보였다. GetGateDoorTiles가 이제 A쪽/B쪽 문턱 두 줄을 모두 돌려주므로(폭 W일 때 W*2개),
    // 각 방 입구마다 독립적으로 문이 생긴다. 회전은 두 줄 각각 기존과 같은 0/180 교대 패턴을 적용
    // — 두 문턱 사이의 힌지 방향을 서로 맞물리게 할지는 실제로 봐야 판단 가능해 일단 독립 적용.
    private void SpawnDoors()
    {
        if (cmap == null || cmap.map.floors == null) return;

        int doorCount = 0;

        for (int floorIdx = 0; floorIdx < cmap.map.floors.Length; floorIdx++)
        {
            Floor floor = cmap.map.floors[floorIdx];
            if (floor.gates == null) continue;

            foreach (var gate in floor.gates)
            {
                // door_open.png/door_closed.png를 직접 보고 판단(사용자 요청) — door_closed는 가로로
                // 넓은 막대 모양(위아래로 인접한 방 사이, 즉 수직 통로를 가로막는 문을 위에서 내려다본
                // 모습)이고, door_open은 그 문짝이 옆벽에 딱 붙어 접힌 듯 세로로 얇게 보인다(같은 문이
                // 열린 상태). 즉 기본(회전 0도) 그림은 수직 통로(isHorizontal=false) 기준이라, 수평
                // 통로(좌우로 인접한 방)에서는 90도 돌려야 벽 방향과 맞는다. 실제로 봤을 때 반대로
                // 보이면 이 한 줄(0f ↔ 90f)만 바꾸면 된다.
                float baseRotation = gate.isHorizontal ? 90f : 0f;

                // 사용자 정정(2026-07-27, "한쪽 스프라이트가 180도 돌아가야지... 정상,180도,정상,180도
                // 이런식으로... 열어젖힌 형태") — door_open 그림 한 장은 문짝이 한쪽 벽에만 붙어 접힌
                // 모습이라, 통로 폭 전체에 같은 회전만 반복하면 모든 문짝이 같은 벽 쪽으로만 열린 것처럼
                // 보인다. 통로를 가로지르며 타일 순서대로 0도/180도를 번갈아 적용해 절반은 한쪽 벽에,
                // 나머지 절반은 반대쪽 벽에 붙어 열린 것처럼 — 즉 통로 양쪽으로 열어젖힌 이중문 형태로
                // 보이게 한다.
                List<Vector2Int>[] gateTileRows = GetGateDoorTiles(gate);
                foreach (List<Vector2Int> gateTiles in gateTileRows)
                {
                    for (int tileIndex = 0; tileIndex < gateTiles.Count; tileIndex++)
                    {
                        Vector2Int tilePos = gateTiles[tileIndex];
                        Vector3Int gridPos = new Vector3Int(tilePos.x, tilePos.y, floorIdx);
                        if (objectGrid.ContainsKey(gridPos)) continue;

                        float doorRotation = baseRotation + (tileIndex % 2 == 1 ? 180f : 0f);
                        string objId = $"Door_{floorIdx}_{tilePos.x}_{tilePos.y}";
                        InteractableObject door = new InteractableObject(objId, gridPos, 0f, 0f, new List<string> { DoorTag });
                        SpawnObject(door, Color.white, doorRotation);
                        doorCount++;
                    }
                }
            }
        }

        LogHelper.Log(LogHelper.GAME, $"SpawnDoors: 전체 {cmap.map.floors.Length}개 층에 문 {doorCount}개 배치 완료.");
    }

    // Gate(청크 경계 + 폭 + 방향)로부터 실제 문이 놓일 타일 좌표 목록을 계산한다. CreateMap.Connection.cs의
    // OpenHorizontalPassage/OpenVerticalPassage가 통로를 깎을 때 쓴 것과 똑같은 공식(중앙 정렬,
    // (8-width)/2부터 width칸)을 재사용해 정확히 같은 타일들을 되짚는다 — chunkAX/BX(또는 AY/BY) 중
    // 어느 쪽이 A/B로 기록됐는지는 방향(왼쪽/오른쪽, 아래/위)에 따라 뒤바뀔 수 있어 Min으로 왼쪽·아래
    // 청크를 먼저 찾는다.
    //
    // 사용자 정정(2026-07-28, "문을 양쪽에 달자, 2*1로 존재하던 문을 2*2로") — 통로 양 끝(A쪽 청크의
    // 마지막 칸 / B쪽 청크의 첫 칸) 두 줄을 모두 반환한다. 반환값은 [A쪽 문턱 줄, B쪽 문턱 줄] 순서의
    // 배열. MapRandering.ApplyOccupationTint/ChangeRoomColor가 "문이 있는 바닥은 점령 색칠 제외"
    // (사용자 요청 2026-07-28)를 위해 그대로 재사용하므로 public static.
    public static List<Vector2Int>[] GetGateDoorTiles(Gate gate)
    {
        var tilesA = new List<Vector2Int>();
        var tilesB = new List<Vector2Int>();
        int start = (8 - gate.width) / 2;

        if (gate.isHorizontal)
        {
            int leftChunkX = Mathf.Min(gate.chunkAX, gate.chunkBX);
            int doorWorldXA = leftChunkX * 8 + 7; // 왼쪽 청크의 마지막 칸
            int doorWorldXB = (leftChunkX + 1) * 8; // 오른쪽(문턱 너머) 청크의 첫 칸
            int chunkY = gate.chunkAY; // 수평 게이트는 두 청크가 같은 행(chunkY == chunkBY)
            for (int i = 0; i < gate.width; i++)
            {
                tilesA.Add(new Vector2Int(doorWorldXA, chunkY * 8 + start + i));
                tilesB.Add(new Vector2Int(doorWorldXB, chunkY * 8 + start + i));
            }
        }
        else
        {
            int bottomChunkY = Mathf.Min(gate.chunkAY, gate.chunkBY);
            int doorWorldYA = bottomChunkY * 8 + 7; // 아래쪽 청크의 마지막 칸
            int doorWorldYB = (bottomChunkY + 1) * 8; // 위쪽 청크의 첫 칸
            int chunkX = gate.chunkAX; // 수직 게이트는 두 청크가 같은 열(chunkX == chunkBX)
            for (int i = 0; i < gate.width; i++)
            {
                tilesA.Add(new Vector2Int(chunkX * 8 + start + i, doorWorldYA));
                tilesB.Add(new Vector2Int(chunkX * 8 + start + i, doorWorldYB));
            }
        }

        return new[] { tilesA, tilesB };
    }

    // 문 닫힘 시스템(2026-07-28, 사용자 요청) — "웨이브가 시작되면 모든 문이 닫히며 벽과 같은 판정이
    // 된다(시야 막힘, 이동 불가)." HumanWaveManager.StartWave()가 웨이브 시작 시점에 이 메서드를
    // 호출한다. 이후 "비어있는 방의 인접 문은 다 열어버리는거로 처리해" 요구사항에 따라, 잠그자마자
    // 이미 비어있는 방들은 그 자리에서 곧바로 다시 연다 — 이 "완전히 비어있으면 연다" 예외는 웨이브
    // 시작 시점에만 적용되는 1회성 부트스트랩이다(사용자 정정 2026-07-28 "문도 한 진영이 남을때까지
    // 열리지 않는거로 하자" — RefreshRoomGateStates 쪽 일반 규칙에서는 아래처럼 이 예외를 뺐다). 만약
    // 이 부트스트랩이 없으면, 애초에 아무도 없던 방은 영원히 "한 진영"이 될 기회 자체가 없어 문이
    // 평생 안 열리는 도달 불가 구역이 생긴다 — 그래서 웨이브 시작 순간에 한해서만 별도로 열어준다.
    public void CloseAllDoorsForWaveStart()
    {
        if (cmap == null || cmap.map.floors == null) return;

        for (int floorIdx = 0; floorIdx < cmap.map.floors.Length; floorIdx++)
        {
            Floor floor = cmap.map.floors[floorIdx];
            if (floor.gates == null) continue;
            for (int i = 0; i < floor.gates.Count; i++)
                SetGateClosed(floorIdx, i, true);
        }

        foreach (var room in allRooms)
        {
            RefreshRoomGateStates(room);
            if (IsRoomEmpty(room)) OpenAllGatesForRoom(room);
        }

        // 문 끼임 방지(2026-07-28, 사용자 요청 "웨이브가 시작하며 문이 닫혔을때, 문에 있는 유닛들이
        // 끼이지 않도록 밀어줘") — 위 재개방까지 끝나 최종적으로 "닫힘"으로 남은 문들만 대상으로,
        // 마침 그 문 타일 위에 서 있던 유닛을 인접한 빈 칸으로 밀어낸다.
        PushUnitsOffClosedDoorTiles();

        LogHelper.Log(LogHelper.GAME, "CloseAllDoorsForWaveStart: 모든 문을 잠그고, 이미 비어있는 방의 인접 문은 다시 열었습니다.");
    }

    // CloseAllDoorsForWaveStart 전용 — isStructureExist가 켜져도 "이미 그 칸에 있던" 유닛은 저절로
    // 튕겨나가지 않는다(새로 들어오는 이동만 막음, UnitFunction.CanMove 참고) — 그대로 두면 문이 닫힌
    // 칸 위에 유닛이 갇힌 것처럼 보인다. AIMovementHelper.FindNearbyOpenTile과 동일한 "8방향 중 가장
    // 가까운 갈 수 있는 칸" 탐색으로 밀어내고, 위치만 직접 갱신한다(HumanWaveManager의 강제 층 이동과
    // 동일한 텔레포트 관례 — 경로탐색 없이 Unregister→position 대입→Register).
    private void PushUnitsOffClosedDoorTiles()
    {
        if (cmap == null || cmap.map.floors == null) return;

        for (int floorIdx = 0; floorIdx < cmap.map.floors.Length; floorIdx++)
        {
            Floor floor = cmap.map.floors[floorIdx];
            if (floor.gates == null) continue;

            foreach (Gate g in floor.gates)
            {
                if (!g.isDoorClosed) continue;

                foreach (var row in GetGateDoorTiles(g))
                {
                    foreach (var doorPos in row)
                    {
                        Vector3Int gridPos = new Vector3Int(doorPos.x, doorPos.y, floorIdx);
                        if (!unitGrid.TryGetValue(gridPos, out Unit u) || u == null || u.hp <= 0) continue;

                        Vector2Int pushTo = AIMovementHelper.FindNearbyOpenTile(u, doorPos);
                        if (pushTo == doorPos) continue; // 밀어낼 빈 칸을 못 찾음(드묾) — 그대로 둠

                        UnregisterUnitPos(u, u.position);
                        u.position = pushTo;
                        RegisterUnitPos(u, u.position);
                        LogHelper.Log(LogHelper.GAME, $"PushUnitsOffClosedDoorTiles: {u.unitType?.typeName}({u.name})를 닫힌 문 {doorPos} → {pushTo}로 밀어냈습니다.");
                    }
                }
            }
        }
    }

    // 문 닫힘 시스템(2026-07-28, 사용자 정정 "문도 한 진영이 남을때까지 열리지 않는거로 하자") — 방
    // 하나의 현재 유닛 구성을 보고 "야생이 아닌 단일 진영만 남음"이면 그 방과 연결된 모든 문을 연다.
    // 사용자 지시 그대로:
    //   - 인간만 남음(야생 전멸, 몬스터 없음) → 열림 / 몬스터만 남음(야생 전멸, 인간 없음) → 열림
    //   - 야생만 남음(인간·몬스터 모두 없음) → 그래도 닫힘 유지
    //   - 인간+몬스터+야생 중 둘 이상이 동시에 살아있음 → 닫힘 유지("야생을 제외한 한 진영이 남을 때까지")
    //   - 완전히 비어있음(셋 다 없음) → 이 메서드만으로는 열리지 않는다(위 정정) — 웨이브 시작 시점의
    //     1회성 예외(CloseAllDoorsForWaveStart)에서만 별도로 처리한다. 웨이브 도중에 방이 나중에
    //     비게 되는 경우(전멸/모두 이탈)까지 자동으로 열어주지는 않는다 — "한 진영"이 아니기 때문.
    // 한번 연 문은 다시 잠그지 않는다(단방향) — 열린 뒤 다른 진영이 흘러들어와 다시 섞여도 그 순간
    // 문을 잠그면 마침 통로에 있던 유닛이 방 사이에 갇히는 부작용이 생긴다. 전체 재잠금은 다음 웨이브
    // 시작 시 CloseAllDoorsForWaveStart가 한 번에 처리한다. GameSession.RemoveDeadUnit(사망 시)과
    // UnitFunction.SyncRoomAffiliation(유닛이 방을 떠날 때)이 관련 방이 바뀔 때마다 이 메서드를 호출한다.
    public void RefreshRoomGateStates(Room room)
    {
        if (room == null || cmap == null || cmap.map.floors == null) return;
        if (room.RoomId < 0 || room.Floor < 0 || room.Floor >= cmap.map.floors.Length) return;

        bool hasHuman = false, hasPlayerMonster = false, hasWild = false;
        foreach (var u in room.ContainedUnits)
        {
            if (u == null || u.hp <= 0) continue;
            if (u.FactionBehavior is HumanFactionBehavior) hasHuman = true;
            else if (u.FactionBehavior is PlayerMonsterBehavior) hasPlayerMonster = true;
            else if (u.FactionBehavior is WildMonsterBehavior) hasWild = true;
        }

        bool isSingleNonWildFaction = !hasWild && (hasHuman ^ hasPlayerMonster);
        if (!isSingleNonWildFaction) return;

        OpenAllGatesForRoom(room);
    }

    // RefreshRoomGateStates/CloseAllDoorsForWaveStart 공용 — 이 방과 연결된 모든 게이트를 연다.
    private void OpenAllGatesForRoom(Room room)
    {
        if (cmap == null || cmap.map.floors == null) return;
        if (room.RoomId < 0 || room.Floor < 0 || room.Floor >= cmap.map.floors.Length) return;

        Floor floor = cmap.map.floors[room.Floor];
        if (floor.gates == null) return;

        for (int i = 0; i < floor.gates.Count; i++)
        {
            Gate g = floor.gates[i];
            if (g.roomA == room.RoomId || g.roomB == room.RoomId)
                SetGateClosed(room.Floor, i, false);
        }
    }

    // CloseAllDoorsForWaveStart 전용 — 살아있는 유닛이 하나도 없으면 "빈 방"(UnitFunction.
    // IsRoomEffectivelyEmpty와 동일 기준).
    private static bool IsRoomEmpty(Room room)
    {
        foreach (var u in room.ContainedUnits)
            if (u != null && u.hp > 0) return false;
        return true;
    }

    // 문 닫힘 시스템(2026-07-28) — 게이트 하나를 열거나 잠근다: (1) Gate.isDoorClosed 갱신,
    // (2) 그 게이트의 문 타일(GetGateDoorTiles)에 Tile.isStructureExist를 씌우거나 벗겨 실제 벽처럼
    // 시야·이동을 막고(UnitFunction.CanMove/CastRay가 이미 Wall과 함께 이 필드를 확인), (3) 문
    // 스프라이트를 door_closed/door_open으로 교체한다. 이미 같은 상태면 아무 것도 하지 않는다(중복
    // 호출·재계산 방지).
    public void SetGateClosed(int floorIndex, int gateIndex, bool closed)
    {
        if (cmap == null || cmap.map.floors == null) return;
        if (floorIndex < 0 || floorIndex >= cmap.map.floors.Length) return;
        Floor floor = cmap.map.floors[floorIndex];
        if (floor.gates == null || gateIndex < 0 || gateIndex >= floor.gates.Count) return;

        Gate gate = floor.gates[gateIndex];
        if (gate.isDoorClosed == closed) return;
        gate.isDoorClosed = closed;
        floor.gates[gateIndex] = gate;

        ApplyGateTileBlocking(floor, gate, closed);
        ApplyGateDoorVisual(floorIndex, gate, closed);

        // 실제 상태 전환이 일어난 경우에만 남는다(위 short-circuit 덕분에 스팸 아님) — 문 개폐 이력을
        // 추적할 수 있는 이벤트 로그로 계속 유지.
        LogHelper.Log(LogHelper.GAME, $"SetGateClosed: F{floorIndex} Gate[{gateIndex}](room{gate.roomA}<->room{gate.roomB}) → {(closed ? "닫힘" : "열림")}");
    }

    // SetGateClosed 전용 — 문이 놓인 타일들의 Tile.isStructureExist를 토글한다. tile.name은 "Floor"
    // 그대로 둔다(실제 Wall로 바꾸지 않아도 UnitFunction의 모든 차단 판정이 "name==Wall || isStructureExist"
    // OR 조건이라 이것만으로 충분 — CanMove/CastRay/UpdateFOV 전부 동일 패턴 사용).
    private void ApplyGateTileBlocking(Floor floor, Gate gate, bool closed)
    {
        if (floor.chunks == null) return;

        foreach (var row in GetGateDoorTiles(gate))
        {
            foreach (var tilePos in row)
            {
                int cx = tilePos.x / 8, cy = tilePos.y / 8;
                int tx = tilePos.x % 8, ty = tilePos.y % 8;
                if (cx < 0 || cx >= floor.config.width || cy < 0 || cy >= floor.config.height) continue;

                Chunks c = floor.chunks[cx, cy];
                if (c.chunk == null) continue;
                Tile t = c.chunk[tx, ty];
                t.isStructureExist = closed;
                c.chunk[tx, ty] = t;
                floor.chunks[cx, cy] = c;
            }
        }
    }

    // SetGateClosed 전용 — 이미 SpawnDoors가 심어둔 문 오브젝트의 스프라이트/회전을 닫힘·열림에 맞게
    // 바꾼다(GetObjectVisual로 기존 GameObject를 그대로 재사용, 새로 생성하지 않음). 닫힘 상태는 통로
    // 전체를 막는 막대 모양이라 타일마다 다른 회전을 줄 필요가 없다 — SpawnDoors의 0/180 교대 패턴은
    // 열림 상태(문짝이 한쪽 벽에 접혀 붙은 모습)에만 의미가 있다.
    //
    // 시야 차단(2026-07-28, 사용자 요청 "문이 닫혀버리면, 벽과 같은 가시성을 가지게 해줘") — 여기서
    // 문 InteractableObject.IsFullyBlocking도 함께 토글한다. UnitFunction.CastRay의 레이 중단 조건이
    // "tile.name==Wall / tile.visibility / IsFullyBlocking"만 보고 Tile.isStructureExist는 안 보는
    // 별개 로직이라(데모_구현현황_검증_2026-07-28.txt "알려진 한계" 참고), isStructureExist만 세워서는
    // 타일 각인(discoveredMap)은 정확해도 실제 레이가 문에서 멈추지 않았다 — IsFullyBlocking을 같이
    // 세워야 진짜로 "벽과 같은 가시성"이 된다.
    private void ApplyGateDoorVisual(int floorIndex, Gate gate, bool closed)
    {
        string spritePath = closed ? "obj/door_closed" : "obj/door_open";
        Sprite sprite = Resources.Load<Sprite>(spritePath);
        // Resources.Load 실패(Import 설정 등)를 조용히 넘기지 않고 경고로 남긴다 — 그 외에는 스프라이트
        // 교체를 그냥 건너뛴다(아래 sprite != null 체크).
        if (sprite == null)
            LogHelper.Warning(LogHelper.GAME, $"ApplyGateDoorVisual: Resources.Load<Sprite>(\"{spritePath}\")가 null을 반환했습니다 — Import 설정(Sprite Mode) 확인 필요.");

        float baseRotation = gate.isHorizontal ? 90f : 0f;

        foreach (var row in GetGateDoorTiles(gate))
        {
            for (int tileIndex = 0; tileIndex < row.Count; tileIndex++)
            {
                Vector3Int gridPos = new Vector3Int(row[tileIndex].x, row[tileIndex].y, floorIndex);

                if (objectGrid.TryGetValue(gridPos, out InteractableObject doorObj))
                    doorObj.IsFullyBlocking = closed;

                GameObject visual = GetObjectVisual(gridPos);
                if (visual == null) continue;

                SpriteRenderer sr = visual.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                if (sprite != null) sr.sprite = sprite;

                float rotation = closed ? baseRotation : baseRotation + (tileIndex % 2 == 1 ? 180f : 0f);
                visual.transform.rotation = Quaternion.Euler(0f, 0f, rotation);

                // SpawnObject와 동일한 관례(2026-07-28, 사용자 요청으로 타일 맞춤 스케일 폐지) —
                // door_open/door_closed 스프라이트를 원본 크기(스케일 1) 그대로 사용.
                visual.transform.localScale = Vector3.one;
            }
        }
    }

    // 게임 시작 시 보스방에 던전 코어를 1회 자동 생성 (GameSession.Initialize 참고).
    private void SpawnInitialDungeonCore()
    {
        if (_unitGenerate == null) return;

        int floorIdx = 1;
        Vector2Int pos = _unitGenerate.GetBossRoomPos(Vector2.one, floorIdx);
        Vector3Int gridPos = new Vector3Int(pos.x, pos.y, floorIdx);

        if (objectGrid.ContainsKey(gridPos)) return;

        string objId = "DungeonCore_" + System.Guid.NewGuid().ToString().Substring(0, 4);
        InteractableObject obj = new InteractableObject(objId, gridPos, 120f, 0f, new List<string> { DungeonCoreTag, CoreTag });
        SpawnObject(obj, Color.magenta);
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

    // ══════════════════════════════════════════════════════════════════════
    // 횃불 배치(2026-07-28, 사용자 요청 "spot light2d 이용해서 토치 프리팹 생성하도록 해봐. 생성
    // 로직은 동일함. 프리팹은 너가 직접 생성해서 실제 파일로 존재해야 해") — 이전엔 SpriteRenderer만
    // 코드로 조립했지만, 이번엔 Assets/Resources/Prefabs/Torch.prefab(직접 작성한 실제 .prefab 에셋
    // — SpriteRenderer(obj/torch.png) + Light2D(Point/원형, 따뜻한 색, 반경 4)를 가진 GameObject)을
    // Resources.Load로 불러와 Instantiate한다. 배치 로직(시작방 제외, 청크 정중앙, 계단 회피)은
    // 이전과 동일하게 유지. 0층 전용 "층 전체를 덮는 대형 횃불 하나" 예외는 한때 있었지만 사용자 요청
    // ("0층 예외 지우고, 0층 청크도 기존 규칙에 따라 토치 깔아줘")으로 폐지 — 0층도 다른 층과 완전히
    // 동일한 청크 단위 배치를 받는다.
    // ══════════════════════════════════════════════════════════════════════
    private GameObject _torchPrefab;
    // 횃불 지연 스폰(2026-07-28, 사용자 요청 "안개가 있는 방에 토치 미리 생성하지 말고, 안개 걷히고
    // 나서 토치 생성하게 해줘") — 아직 안개가 안 걷힌 방의 횃불 배치 좌표는 바로 스폰하지 않고 방
    // 단위로 모아뒀다가, RevealRoomFog가 그 방을 걷는 순간 SpawnPendingTorchesForRoom이 실제로 꺼내
    // 스폰한다.
    private readonly Dictionary<Room, List<Vector2Int>> _pendingTorchTiles = new Dictionary<Room, List<Vector2Int>>();
    // "토치 범위 더 넓혀줘"(사용자 요청) — Torch.prefab의 Light2D.pointLightOuterRadius 자체를
    // 4→6으로 늘렸다(청크 폭 8의 절반이던 "딱 그 청크만큼만"에서 이웃 청크 가장자리까지 살짝 넘치도록).

    private void SpawnTorches()
    {
        if (cmap == null || cmap.map.floors == null) return;
        if (_torchPrefab == null) _torchPrefab = Resources.Load<GameObject>("Prefabs/Torch");
        if (_torchPrefab == null)
        {
            LogHelper.Warning(LogHelper.GAME, "SpawnTorches: Resources.Load<GameObject>(\"Prefabs/Torch\")가 null입니다.");
            return;
        }

        for (int floorIdx = 0; floorIdx < cmap.map.floors.Length; floorIdx++)
        {
            Floor floor = cmap.map.floors[floorIdx];
            if (floor.chunks == null) continue;

            // 0층 전용 예외 폐지(2026-07-28, 사용자 요청 "0층 예외 지우고, 0층 청크도 기존 규칙에
            // 따라 토치 깔아줘") — 층 전체를 덮는 단일 대형 횃불 대신, 0층도 아래 청크 단위 배치를
            // 다른 층과 완전히 동일하게 그대로 받는다. 0층은 안개가 없는 층이라(FogRevealed 항상
            // true) 지연 스폰 없이 전부 즉시 스폰된다.

            int w = floor.config.width, h = floor.config.height;
            for (int cx = 0; cx < w; cx++)
            {
                for (int cy = 0; cy < h; cy++)
                {
                    Chunks c = floor.chunks[cx, cy];
                    if (c.roomId < 0 || c.chunk == null) continue;
                    // 시작방 제외 규칙 폐지(2026-07-28, 사용자 요청 "1층 시작방에 토치 깔려야 해" +
                    // "물론 횃불로직에도 포함되어야겠지" — 2/3층 시작방을 야생으로 되돌린 것과 짝을
                    // 맞춰) — 1층 진짜 시작방(플레이어 거점)도, 이제 야생으로 남는 2/3층 시작방도,
                    // 0층도 전부 일반 방과 동일하게 청크마다 배치한다.

                    if (!TryFindTorchTilePos(floorIdx, cx, cy, c, out Vector2Int tilePos)) continue;

                    Room room = FindRoomByFloorAndId(floorIdx, c.roomId);
                    if (room != null && !room.FogRevealed)
                    {
                        if (!_pendingTorchTiles.TryGetValue(room, out List<Vector2Int> pending))
                        {
                            pending = new List<Vector2Int>();
                            _pendingTorchTiles[room] = pending;
                        }
                        pending.Add(tilePos);
                        continue;
                    }

                    SpawnTorchAt(floorIdx, tilePos);
                }
            }
        }

        LogHelper.Log(LogHelper.GAME, "SpawnTorches: 횃불 배치 완료(안개가 안 걷힌 방은 대기열로 보류).");
    }

    // 횃불 지연 스폰 전용 — RevealRoomFog가 room을 막 걷었을 때 호출된다. 그 방을 위해 쌓여있던
    // 대기 좌표가 있으면 지금 실제로 Instantiate한다.
    private void SpawnPendingTorchesForRoom(Room room)
    {
        if (room == null) return;
        if (!_pendingTorchTiles.TryGetValue(room, out List<Vector2Int> pending)) return;
        _pendingTorchTiles.Remove(room);

        if (_torchPrefab == null) _torchPrefab = Resources.Load<GameObject>("Prefabs/Torch");
        if (_torchPrefab == null) return;

        foreach (var tilePos in pending)
            SpawnTorchAt(room.Floor, tilePos);
    }

    // 청크 정중앙(로컬 (4,4) — 계단 2x2 블록((3,3)~(4,4), PlaceStairTiles와 동일 좌표 공식)과 안 겹치는
    // 나머지 중앙 타일)을 1순위 후보로, 계단이 있는 청크는 그 블록 바로 옆(우→좌→아래→위 순서로 시도)
    // 타일을 대신 쓴다. 벽 타일이거나 이미 다른 오브젝트(문 등)가 있으면 건너뛴다.
    private bool TryFindTorchTilePos(int floorIdx, int cx, int cy, Chunks c, out Vector2Int tilePos)
    {
        tilePos = default;
        bool hasStairs = c.stairTargetFloor >= 0;

        Vector2Int[] candidates = hasStairs
            ? new[] { new Vector2Int(5, 4), new Vector2Int(2, 3), new Vector2Int(4, 5), new Vector2Int(3, 2) }
            : new[] { new Vector2Int(4, 4) };

        foreach (var local in candidates)
        {
            if (local.x < 0 || local.x > 7 || local.y < 0 || local.y > 7) continue;
            if (c.chunk[local.x, local.y].name == "Wall") continue;

            Vector2Int cand = new Vector2Int(cx * 8 + local.x, cy * 8 + local.y);
            if (objectGrid.ContainsKey(new Vector3Int(cand.x, cand.y, floorIdx))) continue;

            tilePos = cand;
            return true;
        }

        return false;
    }

    private GameObject SpawnTorchAt(int floorIdx, Vector2Int tilePos)
    {
        Vector3 offset = (mapRandering != null && mapRandering.floorOffsets != null && floorIdx < mapRandering.floorOffsets.Length)
            ? mapRandering.floorOffsets[floorIdx] : Vector3.zero;
        Transform torchGroup = GetFloorCategoryGroup(floorIdx, "Torches");
        Vector3 worldPos = new Vector3(tilePos.x + 0.5f, tilePos.y + 0.5f, 0f) + offset;

        GameObject go = UnityEngine.Object.Instantiate(_torchPrefab, worldPos, Quaternion.identity);
        go.name = $"Torch_{tilePos.x}_{tilePos.y}";
        if (torchGroup != null) go.transform.SetParent(torchGroup, true);
        return go;
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
            if (_buildingManager.CanInstallAt(gridPos)) return gridPos;
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
            baseVisibility: 40f, trapHp: 20f, trapDamageMin: 30f, trapDamageMax: 60f);
        SpawnObject(obj, Color.red);
    }

    // 03문서 7-3장 테스트용 — 정식 배치 시스템(파티 종류·목표·포메이션 문서 부재) 대신 함정과 동일한
    // 관례로 수동 스폰 훅만 만들어둔다. 웨이브 목표가 아닌 "리더 조사 흐름"만 단독으로 검증하고 싶을
    // 때 쓴다 — 보스방 던전 코어(DungeonCoreTag+CoreTag 둘 다 붙음, 2026-07-27부터 통합)와 달리 이
    // 오브젝트는 CoreTag만 붙어 웨이브 목표로는 추적되지 않는다(HumanWaveManager는 Loot 하위 태그만
    // 봄). 오직 수동 생성 경로에서만 CoreTag 단독으로 스폰되며, 자동 배치(SpawnInitialDungeonCore)는
    // 항상 DungeonCoreTag를 겸해서 붙인다는 원칙(2026-07-24)은 그대로 유지한다.
    public void SpawnCoreAt(Vector3Int gridPos)
    {
        if (cmap == null || cmap.map.floors == null) return;
        if (objectGrid.ContainsKey(gridPos)) return;

        string objId = "Core_" + System.Guid.NewGuid().ToString().Substring(0, 4);
        InteractableObject obj = new InteractableObject(objId, gridPos, baseInterest: 120f, baseDanger: 0f,
            tags: new List<string> { CoreTag }, baseVisibility: 60f);
        SpawnObject(obj, Color.magenta);
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

}









