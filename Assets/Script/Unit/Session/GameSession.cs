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
public class GameSession : NativeRoutine, IOffenseQuery//게임 세션 관리 및 턴 처리(대부분 임시적인 테스트용 요소임
{
    public static GameSession Instance { get; private set; }
    public CreateMap cmap { get; private set; }
    public UnitGenerate unitGenerate => _unitGenerate;
    public MapManager mapManager => _mapManager;
    // MapRandering과 WaveSpawner는 하위 호환성을 위해 MapManager를 통해 노출
    public MapRandering mapRandering => _mapManager?.mapRandering;

    [Inject]
    public HumanWaveManager humanWaveManager;

    private UnitGenerate _unitGenerate;
    private ThreatTileRenderer _threatTileRenderer;
    private IObjectResolver _resolver;

    private DataManager _dataManager;
    private MapManager _mapManager;
    private OffenseProcessor _offenseProcessor;
    public OffenseProcessor OffenseProcessor => _offenseProcessor;
    private UnitRegistry _unitRegistry;
    private ObjectSpawner _objectSpawner;
    private PartyService _partyService;
    private CombatEventService _combatEventService;
    private DebugInputHandler _debugInputHandler;

    [Inject]
    public void Construct(UnitGenerate unitGenerate, ThreatTileRenderer threatTileRenderer, IObjectResolver resolver, CreateMap injectedMap, DataManager dataManager, MapManager mapManager, OffenseProcessor offenseProcessor, UnitRegistry unitRegistry, ObjectSpawner objectSpawner, PartyService partyService, CombatEventService combatEventService, DebugInputHandler debugInputHandler)
    {
        _unitGenerate = unitGenerate;
        _threatTileRenderer = threatTileRenderer;
        _resolver = resolver;
        cmap = injectedMap;
        _dataManager = dataManager;
        _mapManager = mapManager;
        _offenseProcessor = offenseProcessor;
        _unitRegistry = unitRegistry;
        _objectSpawner = objectSpawner;
        _partyService = partyService;
        _combatEventService = combatEventService;
        _debugInputHandler = debugInputHandler;
    }
    public Dictionary<Vector3Int, Unit> unitGrid => _unitRegistry.unitGrid;
    public Dictionary<Vector3Int, InteractableObject> objectGrid => _objectSpawner.objectGrid;
    public Dictionary<Vector3Int, Room> roomGrid { get; private set; } = new Dictionary<Vector3Int, Room>();
    public List<Room> allRooms { get; private set; } = new List<Room>();

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
        Instance = this;

        // InputManager/UIManager/ThreatTileRenderer는 이제 GameCompositionRoot(VContainer)가 배선한다.
    }

    // NativeRoutine 생명주기: Processor 등록 완료 후 한 번 호출됨 (예전 Start()와 동일한 역할)
    public override async UniTask Initialize(CancellationToken cts)
    {
        // 순환 참조 방지를 위해 이 시점에 UIManager를 지연 로드하여 강제로 띄웁니다.
        if (_resolver != null)
        {
            _resolver.Resolve<UIManager>();
        }

        // 맵 데이터를 Resources에서 직접 로드 (MapGeneratorTool이 생성한 정적 템플릿 데이터)
        TextAsset mapTextAsset = Resources.Load<TextAsset>("Data/map");
        if (mapTextAsset != null)
        {
            cmap.DeserializeMap(mapTextAsset.text);
            Unit.humanFactionData.InitMap(cmap);
            Unit.monsterFactionData.InitMap(cmap);
            // MapManager에게 맵 시각화(렌더링) 지시 및 브로드캐스트
            // 렌더링이 완료되어야 층별 실제 오프셋(floorOffset)이 계산됩니다.
            if (_mapManager != null)
            {
                _mapManager.SetupAndVisualizeMap(cmap);
            }

            // 렌더링 후 계산된 오프셋을 바탕으로 방 좌표(TopLeftWorldPos) 설정
            BuildRoomGrid();

            Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, "GameSession: 맵 데이터 로드 성공.");
        }
        else
        {
            Haare.Util.Logger.LogHelper.Error(Haare.Util.Logger.LogHelper.GAME, "GameSession: 맵 데이터 로드 실패 (Resources/Data/map.json 파일이 없습니다). Tools -> Map Generator에서 먼저 맵을 생성해주세요.");
        }

        await base.Initialize(cts);
    }

    public void BuildRoomGrid()
    {
        if (cmap == null || cmap.map.floors == null) return;
        roomGrid.Clear();
        allRooms.Clear();
        Dictionary<int, Room> generatedRooms = new Dictionary<int, Room>();
        
        // 룸의 경계(Bounds)를 계산하기 위한 변수
        Dictionary<int, Vector2Int> roomMin = new Dictionary<int, Vector2Int>();
        Dictionary<int, Vector2Int> roomMax = new Dictionary<int, Vector2Int>();
        
        int currentFloor = 1;
        if (currentFloor >= cmap.map.floors.Length) return;
        
        Floor floor = cmap.map.floors[currentFloor];
        if (floor.chunks == null) return;

        int chunkW = floor.config.width;
        int chunkH = floor.config.height;
        
        Vector3 floorOffset = _unitGenerate != null ? _unitGenerate.GetFloorOffset(currentFloor) : Vector3.zero;

        for (int cx = 0; cx < chunkW; cx++)
        {
            for (int cy = 0; cy < chunkH; cy++)
            {
                Chunks c = floor.chunks[cx, cy];
                if (c.roomId >= 0)
                {
                    if (!generatedRooms.TryGetValue(c.roomId, out Room room))
                    {
                        room = new Room { RoomName = string.IsNullOrEmpty(c.roomName) ? $"Room {c.roomId}" : c.roomName };
                        generatedRooms[c.roomId] = room;
                        allRooms.Add(room);
                        
                        roomMin[c.roomId] = new Vector2Int(int.MaxValue, int.MaxValue);
                        roomMax[c.roomId] = new Vector2Int(int.MinValue, int.MinValue);
                    }
                    
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
        
        // 최종적으로 각 룸에 Bounds 할당
        foreach (var kvp in generatedRooms)
        {
            int rid = kvp.Key;
            Room r = kvp.Value;
            var min = roomMin[rid];
            var max = roomMax[rid];
            r.Bounds = new RectInt(min.x, min.y, max.x - min.x, max.y - min.y);
        }
        
        LogHelper.Log(LogHelper.GAME, $"BuildRoomGrid: 층 {currentFloor}에서 방 {generatedRooms.Count}개 생성됨.");
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
            if (u == null || u.hp <= 0)
            {
                RemoveDeadUnit(i, u);
                visualNeedsSync = true;
                continue; // 사망/파괴 시 시각적 요소 제거 완료
            }

            u.OnUpdate(Time.deltaTime);

            u.CombatState.actionCooldown -= Time.deltaTime;
            if (u.CombatState.actionCooldown <= 0f)
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
    }

    private void HandleDebugInput()
    {
        _debugInputHandler?.HandleDebugInput();
    }

    private void RemoveDeadUnit(int index, Unit u)
    {
        if (u != null) _combatEventService?.RecordKillWeightEvent(u, units);
        
        if (u != null && u.hp <= 0)
        {
            // 세력별 사망 이벤트(예: 오펜스 보상 누적 등) 처리
            u.FactionBehavior?.OnDeath(u, u.lastAttacker);

            string objId = "Corpse_" + System.Guid.NewGuid().ToString().Substring(0, 4);
            Vector3Int gridPos = new Vector3Int(u.position.x, u.position.y, u.currentFloor);
            DangerStage causerStage = DangerStage.Stage0;
            
            if (u.lastAttacker != null && u.Knowledge != null)
            {
                causerStage = u.Knowledge.GetDangerStage(u.lastAttacker.unitType.typeName, u.lastAttacker.isSpecialUnit ? u.lastAttacker.name : null, u.lastAttacker.baseDanger);
            }
            
            bool isMonsterCorpse = u is Monster;
            List<string> tags = new List<string> { "Object/Passable/Corpse", isMonsterCorpse ? "Monster" : "Human" };
            // InteractableObject.BaseVisibility 기본값 자체가 0(사용자 요청) — 여기서 따로 넘길 필요 없음.
            InteractableObject corpse = new InteractableObject(objId, gridPos, WeightMath.CorpseTraceBaseInterest, 0f, tags, causerStage);
            // 인간 시체(짙은 붉은색)와 몬스터 시체(붉은 갈색)를 미묘하게 다른 색으로 구분.
            Color corpseColor = isMonsterCorpse ? new Color(0.45f, 0.2f, 0.05f) : new Color(0.5f, 0f, 0f);
            SpawnObject(corpse, corpseColor);
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

    // 2026-07-20: 02문서(인지·정보판정) 구현으로 생긴 Unit.PerceptionState.perceptionRecords는 "누가 이 유닛을 봤는지"를
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
            other.RemovePerceptionRecord(dead);
        }
        dead.PerceptionState.perceptionRecords.Clear();
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
        return _partyService.CreateParty(name, members);
    }

    private void CheckPartyWaveState(Unit deadUnit)
    {
        _partyService.CheckPartyWaveState(deadUnit);
    }



    private void ProcessUnitAction(Unit u)
    {
        float speed = u.walkSpeed;
        u.CombatState.actionCooldown = speed > 0f ? (1f / speed) : 1f;

        u.JudgeState();
        Vector2Int oldPos = u.position;
        u.ExecuteAction();

        if (oldPos != u.position)
        {
            UnregisterUnitPos(u, oldPos);
            RegisterUnitPos(u, u.position);
        }

        // 01-A 11장: 이동/전투 등으로 이번 턴에 활성화된 후보 중 우선순위가 가장 높은 시야 방향을
        // 확정한다. ExecuteAction() 이후에 호출해야 Move()가 갱신한 currentDir를 "이동 중" 후보의
        // 기본값으로 넘겨줄 수 있고, UpdateFOV() 이전에 호출해야 그 방향 기준으로 시야/인지 범위를 계산한다.
        u.ResolveVisionDirection();
        u.UpdateFOV(units);
    }

    public void SpawnObject(InteractableObject obj, Color color)
    {
        _objectSpawner.SpawnObject(obj, color);
    }

    public void CollectObject(Vector3Int pos)
    {
        _objectSpawner.CollectObject(pos);
    }

}

