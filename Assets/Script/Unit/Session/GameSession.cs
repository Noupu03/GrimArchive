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
public class GameSession : NativeRoutine//게임 세션 관리 및 턴 처리(대부분 임시적인 테스트용 요소임
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

    [Inject]
    public void Construct(UnitGenerate unitGenerate, ThreatTileRenderer threatTileRenderer, IObjectResolver resolver, CreateMap injectedMap, DataManager dataManager, MapManager mapManager)
    {
        _unitGenerate = unitGenerate;
        _threatTileRenderer = threatTileRenderer;
        _resolver = resolver;
        cmap = injectedMap;
        _dataManager = dataManager;
        _mapManager = mapManager;
    }
    public Dictionary<Vector3Int, Unit> unitGrid { get; private set; } = new Dictionary<Vector3Int, Unit>();
    public Dictionary<Vector3Int, InteractableObject> objectGrid { get; private set; } = new Dictionary<Vector3Int, InteractableObject>();
    public Dictionary<Vector3Int, Room> roomGrid { get; private set; } = new Dictionary<Vector3Int, Room>();
    public List<Room> allRooms { get; private set; } = new List<Room>();
    private Dictionary<InteractableObject, GameObject> objectVisuals = new Dictionary<InteractableObject, GameObject>();

    public List<Unit> units { get; private set; } = new List<Unit>();
    public List<Party> parties { get; private set; } = new List<Party>();
    private float updateTimer = 0f;

    public float currentGameSpeed = 1f;
    public bool isPaused = false;

    public void RegisterUnitPos(Unit u, Vector2Int pos)
    {
        if (u == null) return;
        
        // unitType이 없는 더미/스포너 유닛은 기본 1x1 크기로 간주
        int w = u.unitType != null ? (int)u.unitType.footprint.x : 1;
        int h = u.unitType != null ? (int)u.unitType.footprint.y : 1;
        
        for (int dx = 0; dx < w; dx++)
        {
            for (int dy = 0; dy < h; dy++)
            {
                unitGrid[new Vector3Int(pos.x + dx, pos.y + dy, u.currentFloor)] = u;
            }
        }
        
        // HAARE 프레임워크: 오펜스 자동 진입 판정 (Trigger Hooking)
        if ((u.FactionBehavior is HumanFactionBehavior || u.FactionBehavior is PlayerMonsterBehavior) && OffenseProcessor.Instance != null && OffenseProcessor.Instance.currentOffenseRoom == null)
        {
            foreach (var room in allRooms)
            {
                if (room.RoomFaction == FactionType.Wild && room.Bounds.Contains(pos))
                {
                    Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, $"[오펜스 트리거] 플레이어가 야생 방({room.RoomName})에 물리적으로 진입했습니다.");
                    OffenseProcessor.Instance.StartOffense(room, u);
                    break;
                }
            }
        }
    }

    public void UnregisterUnitPos(Unit u, Vector2Int pos)
    {
        if (u == null) return;
        int w = u.unitType != null ? (int)u.unitType.footprint.x : 1;
        int h = u.unitType != null ? (int)u.unitType.footprint.y : 1;
        for (int dx = 0; dx < w; dx++)
        {
            for (int dy = 0; dy < h; dy++)
            {
                unitGrid.Remove(new Vector3Int(pos.x + dx, pos.y + dy, u.currentFloor));
            }
        }
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
        
        OffenseProcessor.Instance.UpdateProcess();

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

            u.actionCooldown -= Time.deltaTime;
            if (u.actionCooldown <= 0f)
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
        if (Keyboard.current != null)
        {
            if (Keyboard.current.hKey.wasPressedThisFrame) OnKeyDown_H();
            if (Keyboard.current.mKey.wasPressedThisFrame) OnKeyDown_M();
            if (Keyboard.current.kKey.wasPressedThisFrame) OnKeyDown_K();
            if (Keyboard.current.oKey.wasPressedThisFrame) OnKeyDown_O();
            if (Keyboard.current.pKey.wasPressedThisFrame) OnKeyDown_P();
        }
    }

    private void RemoveDeadUnit(int index, Unit u)
    {
        if (u != null) RecordKillWeightEvent(u);
        
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
            other.RemovePerceptionRecord(dead);
        }
        dead.perceptionRecords.Clear();
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
    // 인류 유닛들을 하나의 파티로 묶는다 — 연산공식 문서 6장(생존자 전역 반영)/13장(파티 전멸)/
    // 23장(파티 입장 시 정보 오차 공유)이 전제하는 "파티" 단위의 실체. WaveSpawner가 웨이브
    // 몬스터와 함께 인류 파티를 스폰할 때 호출한다.
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

    // 유닛이 하나 죽을 때마다(이 유닛이 파티원이면 그 파티가 전멸했는지, 몬스터면 어느 파티의
    // 웨이브가 클리어됐는지) 확인한다. 13-1장 파티 전멸과 6장 웨이브 종료 생존자 반영은 서로
    // 배타적인 두 종료 방식이라 한 파티당 한쪽만, 그것도 딱 한 번만 트리거되어야 한다
    // (Party.WaveEnded 플래그로 방지).
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

    // 대표 가중치 3종 연산공식 문서 3장: 처치 이벤트를 이해도/위험도에 반영.
    // 인류가 몬스터를 처치한 경우는 "직접 경험(SELF)"으로 바로 연결한다.
    // 몬스터가 인류를 처치한 경우, 죽은 본인은 정보를 남길 수 없으므로 그 순간 생존해 있는
    // 다른 인류 전원이 "직접 목격"한 것으로 근사 처리한다 — 실제 FOV 기반 목격 판정(그 인류가
    // 정말 그 자리를 보고 있었는지)은 아직 없어서 근사임을 구현현황 문서에 남긴다.
    private void RecordKillWeightEvent(Unit victim)
    {
        Unit attacker = victim.lastAttacker;
        if (attacker == null) return;

        var knowledge = attacker.Knowledge;
        if (knowledge == null) return;

        bool victimIsHuman = victim is Human;
        bool attackerIsHuman = attacker is Human;
        if (victimIsHuman == attackerIsHuman) return;

        string incidentId = System.Guid.NewGuid().ToString();

        if (!victimIsHuman)
        {
            knowledge.RecordEvent(EventId.E_MONSTER_KILL_SELF, attacker, victim, InfoType.DirectExperience, incidentId);

            // 처치한 본인 외에 그 순간 생존해 있는 다른 인류도 "직접 목격"한 것으로 근사(위와 동일한 근사).
            foreach (var witness in units)
            {
                if (witness == null || witness == attacker || !(witness is Human) || witness.hp <= 0) continue;
                knowledge.RecordEvent(EventId.E_MONSTER_KILL_SEEN, witness, victim, InfoType.DirectWitness, incidentId);
            }
        }
        else
        {
            foreach (var witness in units)
            {
                if (witness == null || witness == victim || !(witness is Human) || witness.hp <= 0) continue;
                knowledge.RecordEvent(EventId.E_HUMAN_KILL_SEEN, witness, attacker, InfoType.DirectWitness, incidentId);
            }
        }
    }

    private void ProcessUnitAction(Unit u)
    {
        float speed = u.walkSpeed;
        u.actionCooldown = speed > 0f ? (1f / speed) : 1f;

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

    public void OnKeyDown_M()
    {
        if (_unitGenerate == null) return;

        UnitType[] types = new UnitType[] { new MeleeTank() };

        UnitType selection = types[Random.Range(0, types.Length)];

        Monster monster = _unitGenerate.GenerateUnitAtRandomFloor<Monster>(selection, 1);
        monster.FactionBehavior = new PlayerMonsterBehavior();

        units.Add(monster);
        RegisterUnitPos(monster, monster.position);
        LogHelper.Log(LogHelper.GAME, $"Generated Monster (Player Faction): {selection.typeName} at Floor {monster.currentFloor}, {monster.position}");
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

    public void SpawnObject(InteractableObject obj, Color color)
    {
        if (objectGrid.ContainsKey(obj.Position)) return;
        
        objectGrid[obj.Position] = obj;
        LogHelper.Log(LogHelper.GAME, $"Generated {obj.Id} at Floor {obj.Position.z}, {new Vector2Int(obj.Position.x, obj.Position.y)} with Tags: [{string.Join(", ", obj.Tags)}]");

        GameObject visual = new GameObject(obj.Id);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        
        Texture2D tex = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        sr.sprite = sprite;
        sr.sortingOrder = 5;
        
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
        visual.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        
        objectVisuals[obj] = visual;
    }

    public void OnKeyDown_O()
    {
        if (cmap == null || cmap.map.floors == null) return;
        
        int floorIdx = 1;
        Vector2Int spawnPos = _unitGenerate.GetRandomFloorPos(Vector2.one, floorIdx);
        if (spawnPos == Vector2Int.zero) return;

        string objId = "InteractableObj_" + System.Guid.NewGuid().ToString().Substring(0, 4);
        Vector3Int gridPos = new Vector3Int(spawnPos.x, spawnPos.y, floorIdx);
        
        if (!objectGrid.ContainsKey(gridPos))
        {
            InteractableObject obj = new InteractableObject(objId, gridPos, 120f, 0f, new List<string> { "Object/Passable/Loot" });
            SpawnObject(obj, Color.magenta);
        }
    }

    // 03문서 9장 함정 대응 테스트용 — 정식 배치 시스템(레벨 구조 문서 부재) 대신 O키(루팅)와 동일한
    // 관례로 수동 스폰 훅만 만들어둔다. BaseDanger>0으로 스폰해야 Goal_TrapResponse가 실제로 반응한다
    // (기존 오브젝트들은 전부 BaseDanger=0 — InteractableObject.cs 주석 참고).
    public void OnKeyDown_P()
    {
        if (cmap == null || cmap.map.floors == null) return;

        int floorIdx = 1;
        Vector2Int spawnPos = _unitGenerate.GetRandomFloorPos(Vector2.one, floorIdx);
        if (spawnPos == Vector2Int.zero) return;

        string objId = "Trap_" + System.Guid.NewGuid().ToString().Substring(0, 4);
        Vector3Int gridPos = new Vector3Int(spawnPos.x, spawnPos.y, floorIdx);

        if (!objectGrid.ContainsKey(gridPos))
        {
            InteractableObject obj = new InteractableObject(
                objId, gridPos, baseInterest: 0f, baseDanger: 30f,
                tags: new List<string> { "Object/Passable/Trap" },
                baseVisibility: 40f, trapHp: 20f, trapDamageMin: 10f, trapDamageMax: 25f);
            SpawnObject(obj, Color.red);
        }
    }

    public void CollectObject(Vector3Int pos)
    {
        if (objectGrid.ContainsKey(pos))
        {
            var obj = objectGrid[pos];
            obj.IsCollected = true;
            objectGrid.Remove(pos);

            if (objectVisuals.TryGetValue(obj, out GameObject visual))
            {
                if (visual != null) UnityEngine.Object.Destroy(visual);
                objectVisuals.Remove(obj);
            }
        }
    }

}

