using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.InputSystem;
using VContainer;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Haare.Util.Logger;
using GrimArchive.Wave;
using Haare.Scripts.Client.Data;

// Haare의 Processer/Routine 시스템으로 턴 처리 루프를 옮김: 평범한 Unity Update() 대신
// NativeRoutine.UpdateProcess()가 Processor의 등록된 Routine 순회를 통해 매 프레임 호출된다.
// 인스펙터 데이터가 전혀 없어서(디버그 텍스처 뷰 제거 후) 씬 GameObject일 필요가 없는 순수 C# 클래스.

public class GameSession : NativeRoutine, IOffenseQuery
{
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
    public void Construct(UnitGenerate unitGenerate, ThreatTileRenderer threatTileRenderer, IObjectResolver resolver, CreateMap injectedMap, DataManager dataManager, UnitRegistry unitRegistry, ObjectSpawner objectSpawner, PartyService partyService, CombatEventService combatEventService, DebugInputHandler debugInputHandler, BuildingManager buildingManager)
    {
        _unitGenerate = unitGenerate;
        _threatTileRenderer = threatTileRenderer;
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
    }
    public Dictionary<Vector3Int, Unit> unitGrid => _unitRegistry.unitGrid;
    public Dictionary<Vector3Int, InteractableObject> objectGrid => _objectSpawner.objectGrid;
    public Dictionary<Vector3Int, Room> roomGrid { get; private set; } = new Dictionary<Vector3Int, Room>();
    public List<Room> allRooms { get; private set; } = new List<Room>();

    // 방마다 "현재/최대 인구수" world-space 라벨(카메라 무관, 맵에 고정). ThreatTileRenderer._root와
    // 동일한 관례 — 컨테이너를 필드 초기화 시점에 딱 1번 만들고 재생성하지 않는다. 정리는 Finalize()/
    // OnApplicationQuit()에서(하단 참고), 에디터 Play 종료 후 잔재 방지는 Assets/Editor/
    // RoomPopulationLabelCleanup.cs가 맡는다.
    private readonly Transform _roomLabelRoot = new GameObject("RoomPopulationLabels").transform;
    private readonly Dictionary<Room, TextMesh> _roomPopulationLabels = new Dictionary<Room, TextMesh>();
    private readonly Dictionary<Room, string> _roomPopulationLabelText = new Dictionary<Room, string>();

    public IReadOnlyList<Unit> GetUnitsInRoom(RectInt bounds)
    {
        List<Unit> result = new List<Unit>();
        foreach (var u in units)
        {
            if (bounds.Contains(u.position))
            {
                result.Add(u);
            }
        }
        return result;
    }

    public List<Unit> units { get; private set; } = new List<Unit>();
    public List<Party> parties => _partyService.parties;
    private float updateTimer = 0f;

    public float currentGameSpeed = 1f;
    public bool isPaused = false;

    public void RegisterUnitPos(Unit u, Vector2Int pos)
    {
        _unitRegistry.RegisterUnitPos(u, pos);
    }

    public void UnregisterUnitPos(Unit u, Vector2Int pos)
    {
        _unitRegistry.UnregisterUnitPos(u, pos);
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
    private void DestroyRoomLabelRoot()
    {
        foreach (var label in _roomPopulationLabels.Values)
        {
            if (label != null) UnityEngine.Object.Destroy(label.gameObject);
        }
        _roomPopulationLabels.Clear();
        _roomPopulationLabelText.Clear();

        if (_roomLabelRoot != null) UnityEngine.Object.Destroy(_roomLabelRoot.gameObject);
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

        bool visualNeedsSync = false;

        // 턴 액션 처리 후, 씬 상주 시각적 요소들 위치 일괄 동기화
        for (int i = units.Count - 1; i >= 0; i--)
        {
            var u = units[i];
            if (u == null || u.Health.hp <= 0)
            {
                RemoveDeadUnit(i, u);
                visualNeedsSync = true;
                continue; // 사망/파괴 시 시각적 요소 제거 완료
            }

            u.OnUpdate(Time.deltaTime);

            u.CombatState.State.actionCooldown -= Time.deltaTime;
            if (u.CombatState.State.actionCooldown <= 0f)
            {
                ProcessUnitAction(u);
                visualNeedsSync = true;
            }
        }

        if (visualNeedsSync || Time.timeScale < 0.01f) // 일시정지 상태여도 외부 조작(InputManager 등)에 의한 선택 렌더링 피드백이 즉시 반영되도록 매 프레임 Sync
        {
            if (_unitGenerate != null)
            {
                _unitGenerate.SyncVisuals(units);
            }
        }

        if (_threatTileRenderer != null)
        {
            _threatTileRenderer.Render(units);
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
                _roomPopulationLabelText[room] = null;
            }

            string text = $"{room.CurrentPopulation}/{room.MaxPopulation}";
            if (_roomPopulationLabelText.TryGetValue(room, out string prevText) && prevText == text) continue;
            label.text = text;
            _roomPopulationLabelText[room] = text;
        }
    }

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
        go.transform.SetParent(_roomLabelRoot, false);

        Vector3 floorOffset = _unitGenerate != null ? _unitGenerate.GetFloorOffset(room.Floor) : Vector3.zero;
        Vector2 center = room.Bounds.center;
        go.transform.position = new Vector3(center.x, center.y, 0f) + floorOffset;

        // 세련되게(사용자 요청) — 굵고 큰 기본값 대신 은은한 반투명 회백색 + 적당한 크기로 톤을 낮춘다.
        TextMesh tm = go.AddComponent<TextMesh>();
        tm.fontSize = 48;
        tm.characterSize = 0.11f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(1f, 1f, 1f, 0.75f);

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



    private void ProcessUnitAction(Unit u)
    {
        // 4-3장: 경계 상태에서 위치/방향을 확인하며 이동할 때는 이동속도가 75%로 줄어든다.
        float speed = u.BaseStat.walkSpeed * (u.currentAlertSearch != null ? ExplorationMath.AlertMoveSpeedRatio : 1f);
        u.CombatState.State.actionCooldown = speed > 0f ? (1f / speed) : 1f;

        // 아래 TriggerTrapIfStepped가 "이번 틱 시작 시점에 이미 이 함정을 알고 대응 중이었는지"를
        // 판단할 때 쓸 스냅샷 — ExecuteAction()이 currentTrapInteraction을 바꾸기 전 상태를 기억해둔다.
        TrapInteractionState trapInteractionBefore = u.currentTrapInteraction;

        u.JudgeState();
        Vector2Int oldPos = u.position;
        u.ExecuteAction();

        if (oldPos != u.position)
        {
            UnregisterUnitPos(u, oldPos);
            RegisterUnitPos(u, u.position);
            TriggerTrapIfStepped(u, trapInteractionBefore);

            // 오펜스 자동 트리거: PlayerMonster가 야생 방에 진입하면 즉시 오펜스 시작
            if (u.IsPlayerMonsterFaction)
                TryTriggerOffenseForUnit(u);
        }

        // 01-A 11장: 이동/전투 등으로 이번 턴에 활성화된 후보 중 우선순위가 가장 높은 시야 방향을
        // 확정한다. ExecuteAction() 이후에 호출해야 Move()가 갱신한 currentDir를 "이동 중" 후보의
        // 기본값으로 넘겨줄 수 있고, UpdateFOV() 이전에 호출해야 그 방향 기준으로 시야/인지 범위를 계산한다.
        u.ResolveVisionDirection();
        u.UpdateFOV(units);
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
            
            // 시각적 부모로 Tilemap 객체를 찾기 위해 약간의 꼼수(이름 기반 검색) 유지
            GameObject childTilemap = GameObject.Find($"F{obj.Position.z}_Tilemap");
            if (childTilemap != null)
            {
                visual.transform.SetParent(childTilemap.transform);
            }
        }
        
        visual.transform.position = new Vector3(obj.Position.x + 0.5f, obj.Position.y + 0.5f, 0f) + offset;
        // 문 시스템(2026-07-27, 사용자 요청 "닫혀있는 문은 위치 고려해서 배치") — 통로 방향(수평/수직)에
        // 맞춰 스프라이트를 돌린다. 회전이 필요 없는 기존 오브젝트(트랩/시체/코어/루팅)는 기본값 0도라
        // 영향 없음.
        if (rotationZDegrees != 0f) visual.transform.rotation = Quaternion.Euler(0f, 0f, rotationZDegrees);

        // 오브젝트 스프라이트마다 원본 픽셀 크기/PPU가 제각각이라(core 32x32@32ppu, trap 30x26@32ppu,
        // colapse 24x22@32ppu, obj1 16x30@100ppu 등) 고정 스케일(0.5) 하나로는 오브젝트마다 실제
        // 렌더 크기가 다 달랐고, 특히 obj1은 타일보다 훨씬 작게 보였다(사용자 신고, 2026-07-24
        // "오브젝트들 스케일이 너무 작아. 모든 오브젝트들은 타일 크기에 맞춘 스케일로"). sprite.bounds
        // (스케일 1 기준 월드 크기)를 역산해서 타일 1칸(1x1 유닛)에 항상 꽉 차도록 스케일을 계산한다
        // — UnitGenerate.SetupUnitVisual이 풋프린트 크기에 맞춰 유닛을 스케일하는 것과 같은 원리.
        Vector2 spriteWorldSize = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        float scaleX = spriteWorldSize.x > 0f ? 1f / spriteWorldSize.x : 1f;
        float scaleY = spriteWorldSize.y > 0f ? 1f / spriteWorldSize.y : 1f;
        visual.transform.localScale = new Vector3(scaleX, scaleY, 1f);

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
    // 몬스터 스폰 위치 판정)가 호출한다 — 이동(AStarMovement 등)은 여전히 objectGrid를 보지 않으므로
    // 문을 그냥 통과할 수 있고, 이 메서드는 "배치"만 막는다.
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









