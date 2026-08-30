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

// Haare의 Processer/Routine 시스템으로 턴 처리 루프를 옮김 — NativeRoutine.UpdateProcess()가 매
// 프레임 호출된다. 인스펙터 데이터가 없어 씬 GameObject일 필요가 없는 순수 C# 클래스.

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
            // 시전이 있는 공격(castTimer > 0)은 피해가 들어가는 순간까지 옅어지지 않는 "예고"로,
            // 시전 없는 즉발 공격은 0.5초짜리 잔상으로 그린다.
            bool isTelegraph = data.attacker != null && data.attacker.CombatState.State.castTimer > 0f;
            float duration = isTelegraph ? data.attacker.CombatState.State.castTimer : 0.5f;
            _threatTileRenderer?.ShowThreatZone(data.attacker, data.threat, duration, isTelegraph);

            // GetEnemiesInHitbox는 static 공유 리스트를 반환하므로, OnReactToThreat 콜체인이 다시
            // 호출해 리스트를 초기화하기 전에 재사용 버퍼로 복사본을 만들어 순회한다.
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

    // 방마다 "현재/최대 인구수" world-space 라벨(카메라 무관, 맵에 고정). 개별 라벨 이름
    // ("RoomPopLabel_")은 Assets/Editor/RoomPopulationLabelCleanup.cs가 이름만으로 탐색/청소한다.
    private readonly Dictionary<Room, TextMesh> _roomPopulationLabels = new Dictionary<Room, TextMesh>();
    // E: string 비교 대신 int 쌍 비교로 교체 — $"..." 보간 문자열을 값이 바뀔 때만 생성
    private readonly Dictionary<Room, (int pop, int max)> _roomPopulationLabelText = new Dictionary<Room, (int, int)>();

    // L: 호출마다 new List<Unit>() 할당하던 것을 static 캐시로 교체 — 반환값은 즉시 소비할 것
    private static readonly List<Unit> _unitsInRoomResult = new List<Unit>();

    // OnThreatCreated 핸들러(생성자 참고)가 공격마다 new List<Unit>()로 복사하던 것을 재사용 버퍼로 교체.
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

    // 뭉침 완화 — 프레임 드랍 시 대부분의 actionCooldown이 동시에 0 이하로 떨어져 몰아서 처리되면
    // (특히 비용 큰 FindNearestUnexploredTarget) 다음 프레임에도 뭉침이 재생산된다. 전투/전술/
    // 플레이어 명령 중인 유닛은 즉시 처리하고, 그 외(배회/탐색) 유닛만 프레임당 상한을 둔 큐로 분산시킨다.
    private readonly Queue<Unit> _throttledActionQueue = new Queue<Unit>();
    private readonly HashSet<Unit> _queuedForThrottledAction = new HashSet<Unit>();
    // 튜닝값 — 값이 클수록 뭉침 분산 효과가 줄고, 작을수록 배회 유닛의 반응(다음 목적지 결정 등)이
    // 더 늦어진다. 실측 후 조정할 것.
    private const int MaxThrottledUnitActionsPerFrame = 30;
    public List<Party> parties => _partyService.parties;

    public float currentGameSpeed = 1f;
    public bool isPaused = false;

    // 등록 성공 여부를 bool로 알려준다(이미 점유 중이면 false) — 호출부(ProcessUnitAction/
    // Unit.ForceMove)가 실패 시 위치 변경을 되돌린다.
    public bool RegisterUnitPos(Unit u, Vector2Int pos)
    {
        bool ok = _unitRegistry.RegisterUnitPos(u, pos);
        if (ok)
        {
            // PropagationSystem의 방별 소리 감지 인덱스도 함께 갱신한다 — 소리 감지는 전 Unit
            // 대상이지만 전파(정보 확산) 자체는 인류 전용 게이팅이 따로 있다.
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
            // UIManager는 Haare ICustomPanel로 편입되어 VContainer에 등록되지 않는다
            // (GameUIPresenter.BootSequence가 로드) — 여기서 Resolve<UIManager>()하면 예외가 난다.

            TextAsset mapTextAsset = Resources.Load<TextAsset>("Data/map");
            if (mapTextAsset != null && !string.IsNullOrEmpty(mapTextAsset.text))
            {
                cmap.DeserializeMap(mapTextAsset.text);
                Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, "GameSession: Map deserialized from Data/map.");
                // 기존 저장된 map.json엔 예전 로직("2층 이상 시작방=PlayerControlled")이 남아있을 수
                // 있어(신규 생성만 Neutral로 만듦) 여기서 강제로 바로잡는다.
                EnforceFloor2And3StartRoomsAreWild();
                _mapManager.SetupAndVisualizeMap(cmap);
                Unit.humanFactionData.InitMap(cmap);
                Unit.monsterFactionData.InitMap(cmap);
                BuildRoomGrid();
                // 모든 방과 방 사이 통로에 문을 깔아둔다 — 다른 오브젝트보다 먼저 실행해야 SpawnObject의
                // "이미 오브젝트 있으면 스킵" 동작을 이용해 문 자리에 다른 오브젝트가 안 생기게 된다.
                _doorSystem.SpawnDoors();
                // 모든 야생 방에 야생 몬스터를 필수 배치.
                SpawnWildRoomGuards();
                // 1층 보스방에 보스 골렘 고정 소환, 야생 소속.
                SpawnBossGolem();
                // 모든 방에 코어를 하나씩 자동 생성한다. HumanWaveManager는 WaveData.targetRoomRole/
                // targetRoomId로 지정된 방의 Room.CorePosition을 목표로 삼는다.
                // 안개 시스템 초기화 — 코어/건물이 횃불 자리를 뺏지 않도록 먼저 스폰.
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

    // 종료 시퀀스 안전장치 — Dispose 순서가 엄격히 보장되지 않아 라벨 Destroy 후에도 UpdateProcess()가
    // 한두 프레임 더 돌 수 있어, 파괴 중인 그룹에 새 라벨을 만들다 고아 GameObject가 생길 수 있다.
    // 이 플래그로 종료 시작 시 UpdateProcess() 전체를 끊는다.
    private bool _isShuttingDown;

    // NativeRoutine은 OnDestroy가 없어서 Dispose()→Finalize() 시점(ResourceManager/BuildingManager와
    // 동일 관례)에 런타임 생성 GameObject를 직접 정리해야 한다.
    public override async UniTask Finalize()
    {
        _isShuttingDown = true;
        ClearRoomPopulationLabels();
        await base.Finalize();
    }

    // 2층 이상 시작방은 항상 야생(Neutral)이어야 한다 — 신규 생성은 InitOccupationAndDanger가
    // 처리하지만, 기존 저장 맵엔 옛 로직이 남아있을 수 있어 역직렬화 직후 다시 강제 보정한다.
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

    // 층별·종류별 하위 그룹 GameObject(Units/Labels/Doors/Objects/Fog/Torches/Buildings)를
    // F{n}_Tilemap 아래에 모은다. (floorIdx, category) 조합마다 한 번만 만들고 캐시해 GameObject.Find
    // 반복 호출을 피한다.
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

    // 5.1장 — 방 최대 인구수 = 청크 수 × 이 값(문서에 수치가 없어 "방 크기 비례" 공식으로 채택).
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

        // 모든 층을 순회해 야생 몬스터를 모든 층의 야생 방에 배치할 수 있게 한다. RoomIdGenerator가
        // 전역 카운터라 roomId는 층을 넘나들어도 고유하므로 이 딕셔너리들을 층 사이에 공유해도 안전하다.
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
                                // 기본값(Wild)으로 두면 0층/1층 시작방도 생성 직후엔 야생으로
                                // 취급되므로, 맵 원본 occupationState를 그대로 반영해 초기값부터
                                // 일치시킨다.
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

    // BuildRoomGrid 전용 — CreateMap.Chunks.occupationState → Room.RoomFaction 매핑. Outpost는
    // "PlayerControlled 이후 거점화한 상태"라 Player로, 실사용 안 되는 Occupied는 Wild로 근사한다.
    private static FactionType MapOccupationStateToFaction(OccupationState state) => state switch
    {
        OccupationState.PlayerControlled => FactionType.Player,
        OccupationState.Outpost => FactionType.Player,
        OccupationState.HumanControlled => FactionType.Human,
        _ => FactionType.Wild,
    };

    // 안개 스폰/해제/문 통합 셰도우 재계산은 FogOfWarSystem(횃불도 안개 해제 타이밍과 강하게 결합돼
    // 같이 있음)에 있다 — 아래는 GameSession.Instance.X() 형태의 기존 진입점을 유지하는 얇은 위임.
    public void RevealRoomFog(Room room) => _fogOfWarSystem.RevealRoomFog(room);
    public void RevealFogAroundCapturedRoom(Room room) => _fogOfWarSystem.RevealFogAroundCapturedRoom(room);

    // 횃불 바로 위의 유닛은 빛을 그대로 투과시킨다 — UnitGenerate.SyncVisual이 매 위치 갱신 시 이 자리에
    // 횃불이 있는지 확인해 그 유닛의 ShadowCaster2D를 임시로 끈다.
    public bool IsTorchAt(Vector3Int pos) => _fogOfWarSystem != null && _fogOfWarSystem.ActiveTorchPositions.Contains(pos);


    // 모든 야생 방에 몬스터 3~5마리를 랜덤 배치하고 방 밖으로 못 나가게 한다. 야생 여부는
    // CreateMap.Chunks.occupationState(Neutral)로 판정 — Room.RoomFaction(오펜스 시스템)과는 별개
    // 개념. 자리를 못 찾은 개체는 자연히 빠지므로 실제 배치 수가 뽑힌 값보다 적을 수 있다.
    private const int WildRoomGuardCountMin = 3;
    private const int WildRoomGuardCountMax = 5;

    // 한 마리씩 독립적으로 균등 추첨한다. 주술사만 인류 직업이지만 생성 클래스는 Monster로 통일한다
    // — Human으로 만들면 파티/코어·문 자동 공격 등 인류 전용 AI가 붙어 "방 안에만 머무는 야생 가드"가
    // 아니게 된다(CLAUDE.md 하드 룰과 충돌).
    private static readonly System.Func<UnitType>[] WildRoomGuardTypeFactories =
    {
        () => new WildMonsterA(),
        () => new Shaman(),
        () => new GoblinHoodA(), // typeName "고블린 후드"
        () => new GnoleA(),      // typeName "놀"
    };

    private static UnitType PickRandomWildGuardType()
        => WildRoomGuardTypeFactories[UnityEngine.Random.Range(0, WildRoomGuardTypeFactories.Length)]();

    public void SpawnWildRoomGuards()
    {
        if (cmap == null || _unitGenerate == null || allRooms == null) return;

        foreach (var room in allRooms)
        {
            if (room.RoomId < 0 || room.Floor < 0) continue;
            if (room.Floor == 0) continue; // 0층(인류 소유 로비)에는 생성 금지.
            if (cmap.GetRoomOccupationState(room.Floor, room.RoomId) != OccupationState.Neutral) continue;

            int guardCount = UnityEngine.Random.Range(WildRoomGuardCountMin, WildRoomGuardCountMax + 1);
            for (int i = 0; i < guardCount; i++)
            {
                UnitType monsterType = PickRandomWildGuardType();
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

    // 보스 골렘 — SpawnWildRoomGuards처럼 방마다 반복 배치되는 게 아니라, 게임 전체에 1층 보스방 안
    // 고정된 한 마리(야생 소속)만 배치한다.
    private const int BossGolemFloor = 1;

    public void SpawnBossGolem()
    {
        if (cmap == null || _unitGenerate == null) return;

        UnitType golemType = new BossGolem();
        Room bossRoom = FindBossRoom(BossGolemFloor);
        if (bossRoom == null)
        {
            LogHelper.Warning(LogHelper.GAME, $"SpawnBossGolem: {BossGolemFloor}층에 보스방이 없습니다 — 보스 골렘을 배치하지 않습니다.");
            return;
        }

        if (!TryFindRoomCenterSpawnPos(bossRoom, golemType.footprint, out Vector2Int spawnPos))
        {
            LogHelper.Warning(LogHelper.GAME,
                $"SpawnBossGolem: {bossRoom.RoomName} 방({bossRoom.Bounds}) 안에서 3x3이 들어갈 빈자리를 찾지 못했습니다 — " +
                "보스 골렘을 배치하지 않습니다(복도를 막지 않도록 임의 위치 폴백은 하지 않는다).");
            return;
        }

        Monster golem = _unitGenerate.GenerateUnitAtPos<Monster>(golemType, spawnPos, BossGolemFloor);
        golem.FactionBehavior = new WildMonsterBehavior();
        golem.MovementAlgorithm = new RoomConfinedMovement(); // 실제로는 isImmobile이 모든 이동을 막지만 다른 몬스터와 동일 관례 유지
        golem.summonPosition = spawnPos;
        // units.json의 walkSpeed=0만으로는 이동이 막히지 않으므로(행동 주기만 결정) 고정은 이 플래그가 담당.
        golem.isImmobile = true;
        // 고정 유닛이라 시야각 제한이 있으면 등 뒤 적을 영영 못 보므로 전방위 시야로 예외 처리한다.
        golem.hasOmnidirectionalVision = true;

        units.Add(golem);
        RegisterUnitPos(golem, golem.position);
        bossRoom.AddUnit(golem);

        LogHelper.Log(LogHelper.GAME,
            $"SpawnBossGolem: {BossGolemFloor}층 보스방(roomId={bossRoom.RoomId}) 중앙 {spawnPos}에 보스 골렘 배치 완료 " +
            $"(방 범위={bossRoom.Bounds}, 점유타일={spawnPos}~{spawnPos + new Vector2Int(2, 2)}).");
    }

    // 해당 층에서 RoomRole.BossRoom인 청크의 roomId를 찾아 그에 대응하는 Room을 돌려준다.
    private Room FindBossRoom(int floorIdx)
    {
        if (cmap?.map.floors == null || floorIdx < 0 || floorIdx >= cmap.map.floors.Length || allRooms == null) return null;

        Floor floor = cmap.map.floors[floorIdx];
        if (floor.chunks == null) return null;

        for (int cx = 0; cx < floor.config.width; cx++)
        {
            for (int cy = 0; cy < floor.config.height; cy++)
            {
                Chunks c = floor.chunks[cx, cy];
                if (c.roomRole != RoomRole.BossRoom || c.roomId < 0) continue;

                foreach (var room in allRooms)
                    if (room.Floor == floorIdx && room.RoomId == c.roomId) return room;
            }
        }
        return null;
    }

    // 방 중앙에서 바깥쪽 링으로 넓혀가며 footprint가 들어갈 첫 빈자리를 찾는다 — 기존 방식(청크
    // 로컬 좌표 순차 탐색)은 여러 청크로 이뤄진 방에서 중앙이 아닌 구석을 잡고, 실패 시 층 전체
    // 랜덤 폴백이 복도를 막을 위험이 있었다.
    private bool TryFindRoomCenterSpawnPos(Room room, Vector2 footprint, out Vector2Int result)
    {
        int fw = Mathf.Max(1, (int)footprint.x);
        int fh = Mathf.Max(1, (int)footprint.y);

        // footprint의 좌하단 원점 기준으로 방 정중앙에 오도록 보정한다.
        Vector2Int center = new Vector2Int(
            room.Bounds.xMin + (room.Bounds.width  - fw) / 2,
            room.Bounds.yMin + (room.Bounds.height - fh) / 2);

        int maxRing = Mathf.Max(room.Bounds.width, room.Bounds.height);
        for (int ring = 0; ring <= maxRing; ring++)
        {
            for (int dx = -ring; dx <= ring; dx++)
            {
                for (int dy = -ring; dy <= ring; dy++)
                {
                    // 링 테두리만 검사(안쪽은 이전 ring에서 이미 확인함)
                    if (ring > 0 && Mathf.Abs(dx) != ring && Mathf.Abs(dy) != ring) continue;

                    Vector2Int cand = new Vector2Int(center.x + dx, center.y + dy);
                    if (cand.x < room.Bounds.xMin || cand.y < room.Bounds.yMin) continue;
                    if (cand.x + fw > room.Bounds.xMax || cand.y + fh > room.Bounds.yMax) continue;
                    if (!_unitGenerate.IsAreaClear(cand, footprint, room.Floor)) continue;
                    if (!IsGoodForInitialSpawn(new Vector3Int(cand.x, cand.y, room.Floor), footprint)) continue;

                    result = cand;
                    return true;
                }
            }
        }

        result = default;
        return false;
    }

    // 빌드에서는 Application.Quit() 시 OnDestroy가 보장되지 않아 Finalize()만으로 부족할 수 있어,
    // 실제 Unity 콜백으로 한 번 더 정리한다(ClearRoomPopulationLabels는 중복 호출해도 안전).
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
        // 문 개폐 — 기본 닫힘 + 보유 진영 근접 시에만 시각적으로 열림.
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

            // ProcessUnitAction 경유 SyncVisual은 상태가 바뀐 유닛에게만 호출되므로, 선택 표시(발밑
            // 링)는 상태 변화와 무관하게 모든 살아있는 유닛에 매 프레임 갱신한다(비용은 낮음). 카메라가
            // 현재 층 밖을 못 비추므로 다른 층 시각 갱신은 건너뛴다.
            if (CameraController.Instance == null || u.currentFloor == CameraController.Instance.CurrentFloor)
            {
                _unitGenerate?.RefreshSelectionVisual(u);
            }

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
        TickCoreRegen();
    }

    // 코어 자동 회복(DoorSystem.UpdateProcess의 문 회복 로직과 대칭). 공격 중인 코어는
    // UnitFunction.OnUpdate가 매 프레임 TimeSinceLastDamaged를 리셋하므로 회복 지연시간을 넘기지 못한다.
    private void TickCoreRegen()
    {
        if (allRooms == null) return;

        foreach (var room in allRooms)
        {
            if (room == null || room.CoreObjectId == null) continue;
            if (!objectGrid.TryGetValue(room.CorePosition, out InteractableObject core)) continue;
            if (core.CoreHp >= core.CoreMaxHp) continue;

            bool wasBeforeDelay = core.TimeSinceLastDamaged < CoreRegenDelaySeconds;
            core.TimeSinceLastDamaged += Time.deltaTime;
            if (core.TimeSinceLastDamaged < CoreRegenDelaySeconds) continue;

            core.CoreHp = Mathf.Min(core.CoreMaxHp, core.CoreHp + CoreRegenPerSecond * Time.deltaTime);
            // 문턱을 막 넘는 그 프레임에만 막대를 숨겨 불필요한 반복 호출을 피한다 — 이후 다시
            // 공격받으면 UnitFunction.OnUpdate의 SetProgress(_, true)가 다시 걸려 자연히 재표시된다.
            if (wasBeforeDelay)
                GetObjectVisual(room.CorePosition)?.GetComponent<ObjectProgressBarVisual>()?.SetProgress(0f, false);
        }
    }

    // 방의 현재/최대 인구수를 N/M 형식으로 표시(world-space 고정). Room.CurrentPopulation은 이미
    // 인류/야생을 제외해 플레이어 몬스터 기준이 되고, 0층(인류 로비)은 표시하지 않는다. TextMesh.text는
    // 재할당마다 메시를 재생성하므로 값이 바뀔 때만 갱신한다.
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

            // 인구수가 꽉 차면 라벨을 빨간색으로 표시 — 텍스트가 바뀌는 시점(=인구수가 바뀐 시점)에만
            // 같이 재계산하면 충분하다(CurrentPopulation이 안 바뀌면 가득 참 여부도 안 바뀜).
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
            // lastAttacker가 아니라 lastDamageDealer를 넘긴다 — lastAttacker는 인류-몬스터 교차 히트
            // 에서만 갱신되는 가중치 시스템 전용 필드라 몬스터끼리 킬은 항상 null이 된다.
            u.FactionBehavior?.OnDeath(u, u.lastDamageDealer);

            // 방 소유권 전환은 코어 체력제(OffenseProcessor.OnCoreDestroyed)로만 일어난다 — 유닛
            // 사망 자체는 점령 전환을 트리거하지 않고, 문 개폐도 매 프레임 판정이라 사망 시점 재확인이
            // 불필요하다.

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
            
            // SpawnObject는 이미 오브젝트가 있는 타일이면 무시한다 — 함정에 맞아 죽으면 사망 위치가
            // 함정 타일이라 시체가 안 생기므로, 자리가 차있으면 옆 빈 타일을 찾아 대신 놓는다.
            if (objectGrid.ContainsKey(gridPos))
                gridPos = FindNearbyFreeObjectTile(gridPos);

            // 07문서 14장: 사망 시 사망 위치에서 사망음 발생(Destroy 전, 위치가 아직 유효한 지금 시점).
            PropagationSystem.EmitSound(this, SoundType.Death, u.position, u.currentFloor, u);

            bool isMonsterCorpse = u is Monster;
            List<string> tags = new List<string> { "Object/Passable/Corpse", isMonsterCorpse ? "Monster" : "Human" };
            // InteractableObject.BaseVisibility 기본값이 0이라 여기서 따로 넘길 필요 없음.
            InteractableObject corpse = new InteractableObject(objId, gridPos, WeightMath.CorpseTraceBaseInterest, 0f, tags, causerStage);
            // 인간 시체(짙은 붉은색)와 몬스터 시체(붉은 갈색)를 미묘하게 다른 색으로 구분.
            Color corpseColor = isMonsterCorpse ? new Color(0.45f, 0.2f, 0.05f) : new Color(0.5f, 0f, 0f);
            // u가 아직 Destroy되기 전인 지금 UnitVisualDefinition.corpseSprite를 스냅샷한다 — 없으면
            // null로 두고 SpawnObject가 공용 스프라이트로 폴백.
            corpse.CorpseSpriteOverride = u.Generate?.GetVisualDefinition(u)?.corpseSprite;
            // GameSession.humanWaveManager 필드는 아무도 채워주지 않으므로 HumanWaveManager.Instance
            // 정적 싱글턴을 직접 참조한다(WaveSpawner도 동일 관례).
            corpse.SpawnWaveNumber = HumanWaveManager.Instance != null ? HumanWaveManager.Instance.WaveNumber : 0;

            // 인류 시체는 사망 사건 추적의 시작점이라 Destroy 전인 지금 위치/방향/lastAttacker를
            // 스냅샷해야 한다. corpse.OwnerPartyId는 나중에 스폰돼도 미리 채운 값 그대로 붙는다.
            if (!isMonsterCorpse && u is Human deadHuman && deadHuman.party != null)
            {
                corpse.OwnerPartyId = deadHuman.party.Id;
                PartyDeathSystem.OnPartyMemberDied(deadHuman, objId);
            }
            else if (isMonsterCorpse && u.lastAttacker is Human)
            {
                // E_MONSTER_KILL_INDIRECT 연결용 — 인류에게 죽은 몬스터만 Destroy 전(unitType/name
                // 접근이 안전한 지금)에 스냅샷해두면, PropagationSystem.OnMonsterCorpseDiscovered가
                // 나중에 RecordEventByKey를 호출한다.
                corpse.MonsterKilledByHuman = true;
                corpse.MonsterIsSpecialUnit = u.isSpecialUnit;
                corpse.MonsterSpeciesKey = u.unitType != null ? u.unitType.typeName : null;
                corpse.MonsterIndividualKey = u.isSpecialUnit ? u.name : null;
            }

            // 사망 판정 즉시 Corpse 스프라이트로 전환하고 Death VFX를 발동한다 — 킬 이벤트/파티 사망
            // 기록/컴포넌트 정리는 위에서 이미 끝났으므로 지연 없이 처리한다.
            _unitGenerate?.PlayDeathVisual(u);
            SpawnObject(corpse, corpseColor);
            // 시체 소멸은 시간이 아니라 웨이브 카운트 단위다(corpse.SpawnWaveNumber로 스냅샷) —
            // HumanWaveManager.StartWave 시마다 DespawnCorpsesForNewWave가 정리한다.
            if (_unitGenerate != null) _unitGenerate.RemoveVisual(u);
            UnityEngine.Object.Destroy(u);
        }

        if (u != null) CheckPartyWaveState(u);
        // 코어/문 파괴 VFX·함정 해제 진행바 등 "오브젝트 쪽에 붙는" 임시 비주얼 정리 — 죽은 유닛이
        // 더 채널링할 일은 없으므로 즉시 처리한다.
        if (u != null) u.ClearTransientWorldVisuals();
        if (_threatTileRenderer != null && u != null)
        {
            _threatTileRenderer.RemoveThreatZone(u);
        }
        if (u != null) UnregisterUnitPos(u, u.position);
        if (u != null) ClearPerceptionRecordsFor(u);
        if (u != null) castingUnits.Remove(u);
        units.RemoveAt(index);
    }

    // 죽은 자리에 이미 오브젝트가 있으면 바로 옆부터 링 모양으로 넓혀가며 빈 타일을 찾는다. 벽/구조물
    // 타일도 걸러내고(안 그러면 시체가 벽에 놓일 수 있다), 못 찾으면 원래 위치를 반환한다(SpawnObject가 무시).
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

    // Unit.perceptionRecords는 관찰자 쪽에 Unit 참조를 키로 들고 있어, 유닛이 죽어도 destroyed
    // 참조가 남아 웨이브를 거듭할수록 UpdateFOV 비용이 커진다 — 사망 시점에 전 유닛을 순회해 지운다
    // (드문 이벤트라 O(units) 비용 감당 가능).
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
        // 코어/문 파괴 VFX·함정 해제 진행바 등 "오브젝트 쪽에 붙는" 임시 비주얼 정리 — RemoveDeadUnit과
        // 동일한 이유(이 유닛도 갑자기 사라지는 경로).
        u.ClearTransientWorldVisuals();
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

            // 5-1장: 파티 합류(=웨이브 입장) 시점이 "신규 유닛 개인 지도 정보 = 최신 전역 지도 정보"가
            // 적용되는 순간이다. 이미 개인 기억이 있는 유닛은 InitializeNewUnitPersonalInfo가 덮어쓰지
            // 않으므로(5-2장) 재사용해도 안전하다.
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

            // 13-2장: 전멸 흔적 — RegisterWipeoutTrace가 발급한 traceId를 흔적 오브젝트에 실어두면,
            // 생환한 다른 파티가 CastRay로 발견하는 시점에 OnWipeoutTraceReflected가 동일 ID당 1회만
            // 던전 위험도에 반영한다.
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
            // break하지 않고 끝까지 순회한다 — 같은 웨이브의 여러 Party가 동일한 WaveMonsters 리스트를
            // 공유할 수 있어, 몬스터 한 마리의 죽음이 여러 파티의 웨이브 클리어를 동시에 트리거할 수 있다.
            foreach (var party in parties)
            {
                if (party.WaveEnded || !party.WaveMonsters.Contains(deadMonster) || !party.IsWaveCleared) continue;

                party.WaveEnded = true;
                var survivors = party.GetSurvivors();
                knowledge.OnWaveEnd(survivors);
            }
        }
    }



    // 전투/전술/플레이어 명령 상태는 즉시 처리(뭉침 완화 큐를 건너뜀) — 판정 기준이 "이번 프레임
    // JudgeState 이전의 마지막 확정 상태"라 최대 1행동주기 지연될 수 있지만, 공격받는 쪽 반응
    // (OnReactToThreat/DefenseSystem)은 이 유닛의 턴과 무관한 별도 경로라 안전하다.
    private static bool IsHighPriorityFsmState(Unit unit)
    {
        IFSMState state = unit.fsm.CurrentState;
        return state is CombatFSMState || state is TacticalFSMState || state is PlayerCommandFSMState;
    }

    private void ProcessUnitAction(Unit u)
    {
        // 4-3장: 경계 상태 이동은 75% 감속. 07문서 16-3장: 단, 피격/사망음 확인 접근은 긴급 소리라
        // 감속 없이 정상 속도를 유지한다(AlertSearchState.IsUrgentSoundApproach).
        bool alertMoveSlowdown = u.currentAlertSearch != null && !u.currentAlertSearch.IsUrgentSoundApproach;
        float speed = u.BaseStat.walkSpeed * (alertMoveSlowdown ? ExplorationMath.AlertMoveSpeedRatio : 1f);
        u.CombatState.State.actionCooldown = speed > 0f ? (1f / speed) : 1f;

        // 아래 TriggerTrapIfStepped가 "이번 틱 시작 시점에 이미 이 함정을 알고 대응 중이었는지"를
        // 판단할 때 쓸 스냅샷 — ExecuteAction()이 currentTrapInteraction을 바꾸기 전 상태를 기억해둔다.
        TrapInteractionState trapInteractionBefore = u.currentTrapInteraction;

        // 라벨 스냅샷은 반드시 JudgeState() 이전에 찍어야 한다 — 이후에 찍으면 전환 직후 라벨이
        // old/new 둘 다로 잡혀, 위치·방향이 안 바뀌는 전환(예: HaltFSMState 진입)의 stateChanged가
        // 감지되지 않는다.
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
                // 목적지가 이미 점유돼 있다 — 이 프레임 이동을 되돌린다. Move()가 이동 시점에 점유를
                // 확인하지만, 그 확인과 이 grid 동기화 사이 다른 경로가 먼저 차지했을 수 있는 최종
                // 안전망이다.
                u.position = oldPos;
                RegisterUnitPos(u, oldPos);
            }
        }

        // 01-A 11장: 이번 턴 후보 중 우선순위가 가장 높은 시야 방향을 확정한다. ExecuteAction() 이후
        // (Move()가 갱신한 currentDir를 폴백으로 쓰기 위해) + UpdateFOV() 이전(그 방향으로 시야를
        // 계산하기 위해) 호출해야 한다.
        u.ResolveVisionDirection();
        u.UpdateFOV(units);

        if (stateChanged && _unitGenerate != null)
        {
            _unitGenerate.SyncVisual(u);
        }
    }

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

    // 9-9/9-10장의 의도적 통과/파괴 선택과 별개로, 함정을 인지 못 하고 밟으면 자동으로 피해를
    // 입는다. trapInteractionBefore로 이미 대응 중이었는지 확인해 의도적 대응(Action_TrapPass 등)과의
    // 중복 피해를 막는다. 함정은 해제/파괴 전까지 소모되지 않아 다시 밟으면 또 맞고, 몬스터는
    // currentTrapInteraction이 세팅되지 않아 매번 그대로 맞는다.
    private void TriggerTrapIfStepped(Unit unit, TrapInteractionState trapInteractionBefore)
    {
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

        // 태그별 아트 스프라이트 배정(시체=colapse, 함정=trap, 코어=core, 그 외=obj1) —
        // Resources.Load 실패 시에만 도형 폴백(함정=삼각형, 그 외=단색 사각형).
        bool isTrap = obj.Tags != null && obj.Tags.Exists(t => t.Contains("Trap"));
        bool isCorpse = obj.Tags != null && obj.Tags.Exists(t => t.Contains("Corpse"));
        bool isCoreOnly = obj.Tags != null && obj.Tags.Contains(CoreTag);
        bool isLoot = obj.Tags != null && obj.Tags.Exists(t => t.Contains("Loot"));
        // 여기서는 기본 열림(door_open) 스프라이트로 그리지만 DoorSystem.SpawnDoorAt/RebuildDoorAt이
        // 곧바로 닫힘으로 교체한다 — 이후 개폐는 DoorSystem.UpdateProcess가 매 프레임 관리한다.
        bool isDoor = obj.Tags != null && obj.Tags.Contains(DoorSystem.DoorTag);

        Sprite sprite = null;
        if (isTrap) sprite = Resources.Load<Sprite>("obj/trap");
        // 캐릭터별 시체 스프라이트 — RemoveDeadUnit이 스냅샷한 CorpseSpriteOverride가 있으면 쓰고,
        // 없으면 공용 시체 스프라이트로 폴백한다.
        else if (isCorpse) sprite = obj.CorpseSpriteOverride != null ? obj.CorpseSpriteOverride : Resources.Load<Sprite>("obj/colapse");
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

        // 함정 해제·코어/문 파괴 진행 막대를 붙일 자리 — 그 외 오브젝트는 불필요한 컴포넌트 낭비를 막기 위해 생략.
        if (isTrap || isCoreOnly || isDoor) visual.AddComponent<ObjectProgressBarVisual>();

        // 빛이 문도 막도록 벽과 동일한 ShadowCaster2D 기법을 쓴다 — 문 열림/닫힘으로 sr.sprite가
        // 바뀌면 SpriteRenderer 프로바이더가 콜백으로 셰이프를 자동으로 따라 바꾼다.
        if (isDoor) visual.AddComponent<ShadowCaster2D>();

        Vector3 offset = Vector3.zero;
        if (mapRandering != null)
        {
            // mapRandering이 mapRoot 계층 접근을 제공하지 않아 floorOffsets 배열에서 오프셋만 가져온다.
            if (mapRandering.floorOffsets != null && obj.Position.z >= 0 && obj.Position.z < mapRandering.floorOffsets.Length)
            {
                offset = mapRandering.floorOffsets[obj.Position.z];
            }

            // 문은 "Doors", 그 외(트랩/시체/코어/루팅)는 "Objects" 하위 그룹으로 나눠 담는다.
            Transform group = GetFloorCategoryGroup(obj.Position.z, isDoor ? "Doors" : "Objects");
            if (group != null)
            {
                visual.transform.SetParent(group);
            }
        }
        
        visual.transform.position = new Vector3(obj.Position.x + 0.5f, obj.Position.y + 0.5f, 0f) + offset;
        // 문은 통로 방향(수평/수직)에 맞춰 스프라이트를 돌린다. 회전이 필요 없는 오브젝트는 기본값
        // 0도라 영향 없음.
        if (rotationZDegrees != 0f) visual.transform.rotation = Quaternion.Euler(0f, 0f, rotationZDegrees);

        // 오브젝트는 타일 크기에 맞추지 않고 원본 스프라이트 크기(스케일 1)로 스폰한다 — 스프라이트마다
        // 실제 렌더 크기가 제각각이어도 그대로 둔다.
        visual.transform.localScale = Vector3.one;

        objectVisuals[obj] = visual;
    }

    // "Object/Passable/Core" 태그: Human.ComputeInvestigateTarget이 일반 조사 후보에서 제외하고,
    // UnitFunction.CastRay가 PerceptionTargetKind.Core로 분류해 개인 지도에 등록한다. 방 소유권
    // 판정은 태그가 아니라 Room.CoreObjectId/CorePosition을 직접 참조한다.
    public const string CoreTag = "Object/Passable/Core";
    // 자리표시자 — 물리공격력 40 기준 파괴 배율을 그대로 쓰면 너무 빨리 파괴돼 5배로 올렸다(약 50초).
    private const float RoomCoreMaxHp = 1000f;
    // 코어 공격 데미지는 유닛 스탯과 무관한 고정 초당 비율이다 — UnitFunction.OnUpdate가 채널링
    // 유닛마다 독립 적용하므로 별도 인원 수 세기 없이 동시 공격 인원수만큼 자연히 합산된다.
    public const float CoreAttackDamagePerSecond = 20f;
    // 마지막 피해로부터 이 시간(초)이 지나면 코어 회복이 시작된다(DoorSystem.DoorRegenDelaySeconds와
    // 동일 값). 회복 속도는 파괴 속도의 절반으로 잡았다.
    public const float CoreRegenDelaySeconds = 5f;
    public const float CoreRegenPerSecond = 10f;
    // 문 배치/개폐/시야 차단 판정은 DoorSystem으로 뺐다(GameSession 비대화 방지) — 아래는 기존
    // 진입점을 유지하는 얇은 위임일 뿐 실제 구현은 DoorSystem에 있다.
    public bool IsDoorTile(Vector3Int pos) => _doorSystem.IsDoorTile(pos);
    public static List<Vector2Int>[] GetGateDoorTiles(Gate gate, int chunkSize) => DoorSystem.GetGateDoorTiles(gate, chunkSize);
    // 문은 보유 진영의 유닛만 지나갈 수 있고, 아니면 공격해서 파괴해야 한다 — UnitFunction.CanMove/
    // AStarMovement.IsTileWalkable이 이동 판정에 직접 사용.
    public bool IsBlockedByClosedDoor(Vector3Int pos, Unit unit) => _doorSystem.IsBlockedByClosedDoor(pos, unit);
    // 문 개폐 시각 트리거 — UnitFunction.Move가 인접 칸에서 문 타일로 넘어가려는 시도가 있을 때마다
    // 호출한다. 통행 가능 여부(IsBlockedByClosedDoor)와는 완전히 별개 판정(순수 시각 연출용).
    public void NotifyDoorApproachAttempt(Vector3Int pos, Unit unit) => _doorSystem.NotifyApproachAttempt(pos, unit);
    public void RemoveDoor(Vector3Int pos) => _doorSystem.RemoveDoor(pos);
    public void RebuildDoorAt(Vector3Int pos) => _doorSystem.RebuildDoorAt(pos);
    public bool IsRepairableDoorTile(Vector3Int pos) => _doorSystem.IsRepairableDoorTile(pos);
    // 점령 여부와 무관하게 통행 가능한 문(파괴됐거나 자기 진영 소유)만 거쳐 도달 가능한 방인지 판정
    // (InputManager.IssueMoveCommand가 사용).
    public bool CanFactionReachRoom(FactionType faction, int floorIndex, int fromRoomId, int targetRoomId)
        => _doorSystem.CanFactionReachRoom(faction, floorIndex, fromRoomId, targetRoomId);

    // 게임 시작 시 모든 방(야생 포함, 0층 제외)에 코어를 하나씩 자동 생성 — SpawnWildRoomGuards와
    // 동일한 재시도 패턴을 재사용한다.
    // 초기 생성 시 문이나 횃불 바로 앞을 막지 않도록 판별하는 메서드
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

    // 코어도 벽과 동일하게 통행 불가로 판정한다(BuildingManager.UpdateMapDataObstacle과 동일 패턴 —
    // 맵 타일 + 양 진영 discoveredMap 동기화). 코어는 파괴 시 반피로 회복될 뿐 철거되지 않으므로
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

    // 던전 1층 시작방에 자원 생산 건물(V키)과 유닛 생산 건물(B키)을 각각 무상으로 1개씩 배치 —
    // 동일한 Install*Building 경로를 재사용하되 자원 소모만 건너뛴다.
    private void SpawnInitialBuildings()
    {
        if (_buildingManager == null || _unitGenerate == null) return;

        int floorIdx = 1;
        // 자원 생산 건물은 GothicClocktower(2x2), 유닛 생산 건물은 GothicDollhouse(3x3). 예전
        // 스프라이트(obj/building, obj/resource_building)는 디버그 더미 건물용으로 남겨뒀다.
        Sprite resourceBuildingSprite = Resources.Load<Sprite>("obj/GothicClocktower");
        Sprite productionBuildingSprite = Resources.Load<Sprite>("obj/GothicDollhouse");

        // 각자 실제 footprint로 자리를 찾아야 설치 시 CanInstallAt(footprint)이 다시 실패하지 않는다.
        Vector3Int? resourcePos = FindInstallableStartRoomPos(floorIdx, (Vector2)BuildingManager.ResourceBuildingFootprint);
        if (resourcePos.HasValue)
        {
            _buildingManager.InstallResourceBuilding(resourcePos.Value, resourceBuildingSprite);
        }
        else
        {
            LogHelper.Warning(LogHelper.GAME, "SpawnInitialBuildings: 시작방에 자원 생산 건물을 놓을 자리를 찾지 못했습니다.");
        }

        Vector3Int? productionPos = FindInstallableStartRoomPos(floorIdx, (Vector2)BuildingManager.UnitBuildingFootprint);
        if (productionPos.HasValue)
        {
            _buildingManager.InstallProductionBuilding(productionPos.Value, ProductionRule.CreateDefaultPlayerUnitRules(), productionBuildingSprite);
        }
        else
        {
            LogHelper.Warning(LogHelper.GAME, "SpawnInitialBuildings: 시작방에 유닛 생산 건물을 놓을 자리를 찾지 못했습니다.");
        }
    }


    // 시작방 안에서 건물을 놓을 랜덤 위치를 찾는다(SpawnWildRoomGuards와 동일한 재시도 패턴).
    // footprint는 실제 설치될 건물 크기와 일치해야 한다 — 안 그러면 CanInstallAt이 다시 실패할 수 있다.
    private Vector3Int? FindInstallableStartRoomPos(int floorIdx, Vector2 footprint)
    {
        const int maxAttempts = 20;
        Vector2Int footprintInt = new Vector2Int((int)footprint.x, (int)footprint.y);
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2Int pos = GetRandomStartRoomPos(footprint, floorIdx);
            Vector3Int gridPos = new Vector3Int(pos.x, pos.y, floorIdx);
            if (!_buildingManager.CanInstallAt(gridPos, footprintInt)) continue;
            if (!IsGoodForInitialSpawn(gridPos, footprint)) continue;

            // 좁은 시작방에서는 건물 하나가 방을 완전히 갈라놓을(길을 막을) 수 있으므로, 후보 자리를
            // 확정하기 전에 방의 나머지 통행 가능 영역이 하나로 연결돼 있는지 검사해 걸러낸다.
            if (roomGrid.TryGetValue(gridPos, out Room room) && WouldFootprintBlockRoomPath(room, gridPos, footprintInt))
                continue;

            return gridPos;
        }
        return null;
    }

    // footprint가 room의 나머지 통행 가능 타일들을 서로 갈라놓는지 floodfill로 검사한다
    // (CreateMap.RepairGateConnectivity와 같은 원리를 방 내부 타일 단위로 재사용) — 하나라도
    // 고립되면 true.
    private bool WouldFootprintBlockRoomPath(Room room, Vector3Int footprintOrigin, Vector2Int footprint)
    {
        if (room == null || cmap == null) return false;

        var footprintSet = new HashSet<Vector2Int>();
        for (int dx = 0; dx < footprint.x; dx++)
            for (int dy = 0; dy < footprint.y; dy++)
                footprintSet.Add(new Vector2Int(footprintOrigin.x + dx, footprintOrigin.y + dy));

        var floorTiles = new List<Vector2Int>();
        for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
        {
            for (int y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
            {
                Vector2Int p = new Vector2Int(x, y);
                if (footprintSet.Contains(p)) continue;
                if (cmap.GetRoomIdAt(room.Floor, p) != room.RoomId) continue;
                if (!cmap.IsStaticTileWalkable(room.Floor, p)) continue;
                floorTiles.Add(p);
            }
        }

        if (floorTiles.Count <= 1) return false;

        var floorSet = new HashSet<Vector2Int>(floorTiles);
        var visited = new HashSet<Vector2Int> { floorTiles[0] };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(floorTiles[0]);
        Vector2Int[] dirs = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            foreach (var d in dirs)
            {
                Vector2Int n = cur + d;
                if (!floorSet.Contains(n) || visited.Contains(n)) continue;
                visited.Add(n);
                queue.Enqueue(n);
            }
        }

        return visited.Count < floorTiles.Count;
    }

    // O키(루팅 오브젝트)/P키(함정) — B키(빌드 모드)처럼 InputManager가 고스트 배치 모드로 위치를
    // 직접 고르게 하고, 위치가 정해지면 이 메서드들을 호출한다.
    public void SpawnLootObjectAt(Vector3Int gridPos)
    {
        if (cmap == null || cmap.map.floors == null) return;
        if (objectGrid.ContainsKey(gridPos)) return;

        string objId = "InteractableObj_" + System.Guid.NewGuid().ToString().Substring(0, 4);
        InteractableObject obj = new InteractableObject(objId, gridPos, 120f, 0f, new List<string> { "Object/Passable/Loot" });
        SpawnObject(obj, Color.magenta);
    }

    // 03문서 9장 함정 대응 테스트용 — 정식 배치 시스템(레벨 구조 문서 부재) 대신 수동 스폰 훅만
    // 둔다. BaseDanger>0으로 스폰해야 Goal_TrapResponse가 반응한다(다른 오브젝트는 BaseDanger=0).
    public void SpawnTrapAt(Vector3Int gridPos)
    {
        if (cmap == null || cmap.map.floors == null) return;
        if (objectGrid.ContainsKey(gridPos)) return;

        string objId = "Trap_" + System.Guid.NewGuid().ToString().Substring(0, 4);
        InteractableObject obj = new InteractableObject(
            // 오브젝트→건축물→지나갈 수 있는 건축물 계층 — Loot/Corpse/WipeoutTrace 같은 단순
            // 오브젝트와 달리 함정은 "지나갈 수 있는 건축물"로 취급한다.
            objId, gridPos, baseInterest: 0f, baseDanger: 30f,
            tags: new List<string> { "Object/Building/Passable/Trap" },
            // 자동 트리거(TriggerTrapIfStepped)로 자주 발동하므로 데미지를 30~60으로 크게 잡았다.
            // 체력도 DoorSystem.DoorMaxHp(300)와 맞춰 실제 저지력을 갖도록 했다 — 낮으면 1초 안에 파괴된다.
            baseVisibility: 40f, trapHp: 300f, trapDamageMin: 30f, trapDamageMax: 60f);
        SpawnObject(obj, Color.red);
    }

    // debug 전용 — 즉석에서 벽 타일을 추가한다. isStructureExist가 아니라 tile.name을 "Wall"로
    // 바꿔야 BuildWallMask 기반 빛 차단이 실제 벽과 동일하게 적용된다.
    public bool IsFloorTileConvertibleToWall(Vector3Int gridPos)
    {
        return TryGetFloorTile(gridPos, out _, out _, out _, out _, out Tile tile) && tile.name != "Wall";
    }

    public bool DebugConvertFloorTileToWall(Vector3Int gridPos)
    {
        if (!TryGetFloorTile(gridPos, out Floor floor, out Chunks chunk, out int cx, out int cy, out Tile _)) return false;
        int cs = floor.config.chunkSize;
        int tx = gridPos.x - cx * cs;
        int ty = gridPos.y - cy * cs;
        if (chunk.chunk[tx, ty].name == "Wall") return false;

        chunk.chunk[tx, ty].name = "Wall";

        const int WallMapValue = 2;
        if (Unit.humanFactionData?.discoveredMap != null && gridPos.z < Unit.humanFactionData.discoveredMap.Length)
            Unit.humanFactionData.discoveredMap[gridPos.z][gridPos.x, gridPos.y] = WallMapValue;
        if (Unit.monsterFactionData?.discoveredMap != null && gridPos.z < Unit.monsterFactionData.discoveredMap.Length)
            Unit.monsterFactionData.discoveredMap[gridPos.z][gridPos.x, gridPos.y] = WallMapValue;

        mapRandering?.SetTileToWall(gridPos.z, new Vector3Int(gridPos.x, gridPos.y, 0));
        _fogOfWarSystem?.NotifyFloorGeometryChanged(gridPos.z);

        return true;
    }

    // 위 두 메서드가 공유하는 층/청크/타일 조회 — BuildingManager.UpdateMapDataObstacle과 동일한
    // 좌표 변환(gridPos.x/y를 chunkSize로 나눠 청크·타일 인덱스로 분해).
    private bool TryGetFloorTile(Vector3Int gridPos, out Floor floor, out Chunks chunk, out int cx, out int cy, out Tile tile)
    {
        floor = default; chunk = default; cx = 0; cy = 0; tile = default;
        if (cmap == null || cmap.map.floors == null) return false;
        if (gridPos.z < 0 || gridPos.z >= cmap.map.floors.Length) return false;
        if (gridPos.x < 0 || gridPos.y < 0) return false;

        floor = cmap.map.floors[gridPos.z];
        if (floor.chunks == null) return false;

        int cs = floor.config.chunkSize;
        cx = gridPos.x / cs; cy = gridPos.y / cs;
        if (cx >= floor.config.width || cy >= floor.config.height) return false;

        chunk = floor.chunks[cx, cy];
        if (chunk.chunk == null) return false;

        int tx = gridPos.x - cx * cs, ty = gridPos.y - cy * cs;
        tile = chunk.chunk[tx, ty];
        return true;
    }

    // 9-7/9-8장: 함정 해제 진행 막대/결과 문구가 함정 위치의 실제 비주얼 GameObject를 찾아야 해서
    // 추가한 조회용 공개 메서드.
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

    // 시체 자동 소멸은 시간이 아니라 웨이브 카운트 기준 — 스폰된 웨이브 포함 2웨이브 동안
    // 존재한다(웨이브 3에 생긴 시체는 3·4에 남고 5 시작 시 정리). HumanWaveManager.StartWave가
    // 새 웨이브 시작마다 호출한다.
    public const int CorpseDespawnAfterWaves = 2;

    public void DespawnCorpsesForNewWave(int currentWaveNumber)
    {
        List<Vector3Int> toRemove = null;
        foreach (var kv in objectGrid)
        {
            InteractableObject obj = kv.Value;
            if (obj.Tags == null || !obj.Tags.Exists(t => t.Contains("Corpse"))) continue;
            if (obj.SpawnWaveNumber + CorpseDespawnAfterWaves > currentWaveNumber) continue;

            (toRemove ??= new List<Vector3Int>()).Add(kv.Key);
        }

        if (toRemove == null) return;
        foreach (var pos in toRemove)
            CollectObject(pos);
    }

}









