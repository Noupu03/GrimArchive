using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using VContainer;
using Haare.Client.Routine;
using Haare.Util.Logger;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Haare의 Processer/Routine 시스템으로 턴 처리 루프를 옮김: 평범한 Unity Update() 대신
// MonoRoutine.UpdateProcess()가 Processor.Instance.Onupdate 구독을 통해 매 프레임 호출된다.
public class GameSession : MonoRoutine//게임 세션 관리 및 턴 처리(대부분 임시적인 테스트용 요소임
{
    public static GameSession Instance { get; private set; }
    public CreateMap cmap { get; private set; }
    public UnitGenerate unitGenerate => _unitGenerate;

    private UnitGenerate _unitGenerate;
    private ThreatTileRenderer _threatTileRenderer;
    private IObjectResolver _resolver;

    [Inject]
    public void Construct(UnitGenerate unitGenerate, ThreatTileRenderer threatTileRenderer, IObjectResolver resolver)
    {
        _unitGenerate = unitGenerate;
        _threatTileRenderer = threatTileRenderer;
        _resolver = resolver;
    }
    public Dictionary<Vector3Int, Unit> unitGrid { get; private set; } = new Dictionary<Vector3Int, Unit>();
    public Dictionary<Vector3Int, InteractableObject> objectGrid { get; private set; } = new Dictionary<Vector3Int, InteractableObject>();
    private Dictionary<InteractableObject, GameObject> objectVisuals = new Dictionary<InteractableObject, GameObject>();

    public List<Unit> units { get; private set; } = new List<Unit>();
    public List<Party> parties { get; private set; } = new List<Party>();
    private float updateTimer = 0f;
    private float textureUpdateTimer = 0f;
    private bool needTextureUpdate = false;

    public float currentGameSpeed = 1f;
    public bool isPaused = false;

    [Header("진영별 맵 시각화 텍셀 (인스펙터에서 클릭하여 확인)")]
    public Texture2D[] humanMapTextures = new Texture2D[4];
    public Texture2D[] monsterMapTextures = new Texture2D[4];

    public void RegisterUnitPos(Unit u, Vector2Int pos)
    {
        if (u == null) return;
        int w = (int)u.unitType.footprint.x;
        int h = (int)u.unitType.footprint.y;
        for (int dx = 0; dx < w; dx++)
        {
            for (int dy = 0; dy < h; dy++)
            {
                unitGrid[new Vector3Int(pos.x + dx, pos.y + dy, u.currentFloor)] = u;
            }
        }
    }

    public void UnregisterUnitPos(Unit u, Vector2Int pos)
    {
        if (u == null) return;
        int w = (int)u.unitType.footprint.x;
        int h = (int)u.unitType.footprint.y;
        for (int dx = 0; dx < w; dx++)
        {
            for (int dy = 0; dy < h; dy++)
            {
                unitGrid.Remove(new Vector3Int(pos.x + dx, pos.y + dy, u.currentFloor));
            }
        }
    }

    // MonoRoutine.Awake()는 private라 오버라이드 불가 — 대신 InitializeAsync()가 맨 처음
    // 동기적으로 호출해주는 Constructor()가 예전 Awake() 배선을 대체한다.
    protected override void Constructor()
    {
        Instance = this;

        // InputManager/UIManager/ThreatTileRenderer는 이제 GameCompositionRoot(VContainer)가 배선한다.
    }

    void Start()
    {
        // 순환 참조 방지를 위해 Start 시점에 UIManager를 지연 로드하여 강제로 띄웁니다.
        if (_resolver != null)
        {
            _resolver.Resolve<UIManager>();
        }

        cmap = FindObjectOfType<CreateMap>();
        if (cmap != null)
        {
            Unit.humanFactionData.InitMap(cmap);
            Unit.monsterFactionData.InitMap(cmap);
        }
    }

    // Processor.Instance.Onupdate 구독을 통해 매 프레임 호출됨 (예전 Update()와 동일한 역할)
    protected override void UpdateProcess()
    {
        base.UpdateProcess(); // MonoRoutine 자체의 Onupdate Subject도 계속 발행되도록 유지

        HandleDebugInput();

        bool visualNeedsSync = false;

        // 턴 액션 처리 후, 씬 상주 시각적 요소들 위치 일괄 동기화
        for (int i = units.Count - 1; i >= 0; i--)
        {
            var u = units[i];
            if (u == null || u.hp <= 0)
            {
                RemoveDeadUnit(i, u);
                visualNeedsSync = true;
                needTextureUpdate = true;
                continue; // 사망/파괴 시 시각적 요소 제거 완료
            }

            u.OnUpdate(Time.deltaTime);

            u.actionCooldown -= Time.deltaTime;
            if (u.actionCooldown <= 0f)
            {
                ProcessUnitAction(u);
                visualNeedsSync = true;
                needTextureUpdate = true;
            }
        }

        if (visualNeedsSync || Time.timeScale < 0.01f) // 일시정지 상태여도 외부 조작(InputManager 등)에 의한 선택 렌더링 피드백이 즉시 반영되도록 매 프레임 Sync
        {
            if (_unitGenerate != null)
            {
                _unitGenerate.SyncVisuals(units);
            }
        }

        // 최적화 3: 텍스처 갱신 쓰로틀링
        textureUpdateTimer += Time.deltaTime;
        if (needTextureUpdate && textureUpdateTimer >= 0.2f)
        {
            UpdateFactionTextures();
            needTextureUpdate = false;
            textureUpdateTimer = 0f;
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
        }
    }

    private void RemoveDeadUnit(int index, Unit u)
    {
        if (u != null) RecordKillWeightEvent(u);
        
        if (u != null && u.hp <= 0)
        {
            string objId = "Corpse_" + System.Guid.NewGuid().ToString().Substring(0, 4);
            Vector3Int gridPos = new Vector3Int(u.position.x, u.position.y, u.currentFloor);
            DangerStage causerStage = DangerStage.Stage0;
            
            if (u.lastAttacker != null && u.Knowledge != null)
            {
                causerStage = u.Knowledge.GetDangerStage(u.lastAttacker.unitType.typeName, u.lastAttacker.isSpecialUnit ? u.lastAttacker.name : null, u.lastAttacker.baseDanger);
            }
            
            List<string> tags = new List<string> { "Corpse", u is Monster ? "Monster" : "Human" };
            InteractableObject corpse = new InteractableObject(objId, gridPos, WeightMath.CorpseTraceBaseInterest, 0f, tags, causerStage);
            SpawnObject(corpse, new Color(0.5f, 0f, 0f)); // 어두운 붉은색
        }

        if (u != null) CheckPartyWaveState(u);
        if (_unitGenerate != null && u != null)
        {
            _unitGenerate.RemoveVisual(u);
        }
        if (u != null) UnregisterUnitPos(u, u.position);
        units.RemoveAt(index);
        if (u != null) Destroy(u);
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
            if (party.WaveEnded || !party.IsWiped) return;

            party.WaveEnded = true;
            knowledge.OnPartyWipeout();

            // 13-2장: 전멸 흔적 — 원인 대상(이 파티원을 마지막으로 공격한 대상)의 위험도 단계로
            // 보정치를 계산해 등록한다. 실제 "생환 파티가 흔적을 발견"하는 흐름(시체/흔적 엔티티가
            // 아직 없음)은 이번 샘플 범위 밖이라 OnWipeoutTraceReflected는 호출하지 않는다.
            Unit causer = deadHuman.lastAttacker;
            DangerStage causerStage = DangerStage.Stage0;
            if (causer != null)
                causerStage = knowledge.GetDangerStage(causer.unitType.typeName, causer.isSpecialUnit ? causer.name : null, causer.baseDanger);
            string traceId = knowledge.RegisterWipeoutTrace(causerStage);

            // 전멸 흔적 오브젝트 생성 추가
            string objId = "Wipeout_" + System.Guid.NewGuid().ToString().Substring(0, 4);
            Vector3Int gridPos = new Vector3Int(deadHuman.position.x, deadHuman.position.y, deadHuman.currentFloor);
            List<string> tags = new List<string> { "WipeoutTrace" };
            InteractableObject wipeoutObj = new InteractableObject(objId, gridPos, WeightMath.WipeoutTraceBaseInterest, 0f, tags, causerStage);
            SpawnObject(wipeoutObj, Color.black);

            LogHelper.Log($"<b><color=red>[EventId:E_PARTY_WIPEOUT]</color></b>",
                $"파티={party.Name} 전멸. 던전 위험도 +{WeightMath.PartyWipeoutDungeonDangerIncrease}, 전멸흔적={traceId}(단계={causerStage})");
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

                LogHelper.Log($"<b><color=cyan>[EventId:E_WAVE_CLEAR]</color></b>",
                    $"파티={party.Name} 웨이브 클리어. 생존자 {survivors.Count}명 정보를 6장 규칙으로 전역 반영(OnWaveEnd).");
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
        CreateMap mapGenerator = cmap != null ? cmap : FindObjectOfType<CreateMap>();

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

        units.Add(monster);
        RegisterUnitPos(monster, monster.position);
        LogHelper.Log(LogHelper.GAME, $"Generated Monster: {selection.typeName} at Floor {monster.currentFloor}, {monster.position}");
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
            units.Add(human);

            RegisterUnitPos(human, human.position);
            LogHelper.Log(LogHelper.GAME, $"Generated Archer at Floor {human.currentFloor}, {human.position}");
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
        var mr = UnityEngine.Object.FindObjectOfType<MapRandering>();
        if (mr != null)
        {
            Transform childTilemap = mr.transform.Find($"F{obj.Position.z}_Tilemap");
            if (childTilemap != null)
            {
                offset = childTilemap.position;
                visual.transform.SetParent(childTilemap);
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
        Vector2Int spawnPos = GetRandomStartRoomPos(Vector2.one, floorIdx);
        if (spawnPos == Vector2Int.zero) return;

        string objId = "InteractableObj_" + System.Guid.NewGuid().ToString().Substring(0, 4);
        Vector3Int gridPos = new Vector3Int(spawnPos.x, spawnPos.y, floorIdx);
        
        if (!objectGrid.ContainsKey(gridPos))
        {
            InteractableObject obj = new InteractableObject(objId, gridPos, 120f, 0f, new List<string> { "Loot" });
            SpawnObject(obj, Color.magenta);
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

    private void UpdateFactionTextures()
    {
        if (cmap == null || cmap.map.floors == null) return;
        int floorCount = cmap.map.floors.Length;
        if (humanMapTextures.Length != floorCount) humanMapTextures = new Texture2D[floorCount];
        if (monsterMapTextures.Length != floorCount) monsterMapTextures = new Texture2D[floorCount];

        for (int f = 0; f < floorCount; f++)
        {
            int mapW = Unit.humanFactionData.discoveredMap[f].GetLength(0);
            int mapH = Unit.humanFactionData.discoveredMap[f].GetLength(1);

            if (humanMapTextures[f] == null || humanMapTextures[f].width != mapW || humanMapTextures[f].height != mapH) { humanMapTextures[f] = new Texture2D(mapW, mapH); humanMapTextures[f].filterMode = FilterMode.Point; }
            if (monsterMapTextures[f] == null || monsterMapTextures[f].width != mapW || monsterMapTextures[f].height != mapH) { monsterMapTextures[f] = new Texture2D(mapW, mapH); monsterMapTextures[f].filterMode = FilterMode.Point; }

            Color[] hPixels = new Color[mapW * mapH];
            Color[] mPixels = new Color[mapW * mapH];

            for (int y = 0; y < mapH; y++)
            {
                for (int x = 0; x < mapW; x++)
                {
                    int hVal = Unit.humanFactionData.discoveredMap[f][x, y];
                    hPixels[y * mapW + x] = hVal == 1 ? Color.white : (hVal == 2 ? Color.gray : Color.black);

                    int mVal = Unit.monsterFactionData.discoveredMap[f][x, y];
                    mPixels[y * mapW + x] = mVal == 1 ? Color.white : (mVal == 2 ? Color.gray : Color.black);
                }
            }

            foreach (var u in units)
            {
                if (u == null || u.currentFloor != f) continue;
                int idx = u.position.y * mapW + u.position.x;
                if (u.position.x >= 0 && u.position.x < mapW && u.position.y >= 0 && u.position.y < mapH)
                {
                    if (u is Human)
                    {
                        hPixels[idx] = Color.green;
                        if (Unit.humanFactionData.spottedEnemyUnits.Contains(u)) hPixels[idx] = Color.red;
                    }
                    else if (u is Monster)
                    {
                        mPixels[idx] = Color.yellow;
                        if (Unit.monsterFactionData.spottedEnemyUnits.Contains(u)) mPixels[idx] = Color.blue;
                    }
                }
            }

            foreach (var enemy in Unit.humanFactionData.spottedEnemyUnits)
            {
                if (enemy == null || enemy.currentFloor != f) continue;
                int idx = enemy.position.y * mapW + enemy.position.x;
                if (enemy.position.x >= 0 && enemy.position.x < mapW && enemy.position.y >= 0 && enemy.position.y < mapH) hPixels[idx] = Color.red;
            }

            foreach (var enemy in Unit.monsterFactionData.spottedEnemyUnits)
            {
                if (enemy == null || enemy.currentFloor != f) continue;
                int idx = enemy.position.y * mapW + enemy.position.x;
                if (enemy.position.x >= 0 && enemy.position.x < mapW && enemy.position.y >= 0 && enemy.position.y < mapH) mPixels[idx] = Color.blue;
            }

            // 오브젝트 시각화 (자홍색)
            foreach (var kvp in objectGrid)
            {
                if (kvp.Value.Position.z == f)
                {
                    int idx = kvp.Key.y * mapW + kvp.Key.x;
                    if (kvp.Key.x >= 0 && kvp.Key.x < mapW && kvp.Key.y >= 0 && kvp.Key.y < mapH)
                    {
                        hPixels[idx] = Color.magenta;
                        mPixels[idx] = Color.magenta;
                    }
                }
            }

            humanMapTextures[f].SetPixels(hPixels);
            humanMapTextures[f].Apply();

            monsterMapTextures[f].SetPixels(mPixels);
            monsterMapTextures[f].Apply();
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(GameSession))]
public class GameSessionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        GameSession gs = (GameSession)target;

        int maxFloor = gs.humanMapTextures != null ? gs.humanMapTextures.Length : 0;
        for (int f = 0; f < maxFloor; f++)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"[{f}층] 인류 / 몬스터 맵", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (gs.humanMapTextures != null && gs.humanMapTextures.Length > f && gs.humanMapTextures[f] != null)
            {
                Rect rect1 = GUILayoutUtility.GetRect(128, 128);
                GUI.DrawTexture(rect1, gs.humanMapTextures[f], ScaleMode.ScaleToFit);
            }
            if (gs.monsterMapTextures != null && gs.monsterMapTextures.Length > f && gs.monsterMapTextures[f] != null)
            {
                Rect rect2 = GUILayoutUtility.GetRect(128, 128);
                GUI.DrawTexture(rect2, gs.monsterMapTextures[f], ScaleMode.ScaleToFit);
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
