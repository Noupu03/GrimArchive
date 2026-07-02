using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using VContainer;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameSession : MonoBehaviour//게임 세션 관리 및 턴 처리(대부분 임시적인 테스트용 요소임
{
    public static GameSession Instance { get; private set; }
    public CreateMap cmap { get; private set; }

    private UnitGenerate _unitGenerate;
    private ThreatTileRenderer _threatTileRenderer;

    [Inject]
    public void Construct(UnitGenerate unitGenerate, ThreatTileRenderer threatTileRenderer)
    {
        _unitGenerate = unitGenerate;
        _threatTileRenderer = threatTileRenderer;
    }
    public Dictionary<Vector3Int, Unit> unitGrid { get; private set; } = new Dictionary<Vector3Int, Unit>();

    public List<Unit> units { get; private set; } = new List<Unit>();
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

    void Awake()
    {
        Instance = this;

        // InputManager/UIManager/ThreatTileRenderer는 이제 GameCompositionRoot(VContainer)가 배선한다.
        // ArtifactManager는 대부분 주석 처리된 미사용 코드라 이번 DI 전환 대상에서 제외했다.
        if (GetComponent<ArtifactManager>() == null)
        {
            gameObject.AddComponent<ArtifactManager>();
        }
    }

    void Start()
    {
        cmap = FindObjectOfType<CreateMap>();
        if (cmap != null)
        {
            Unit.humanFactionData.InitMap(cmap);
            Unit.monsterFactionData.InitMap(cmap);
        }
    }

    void Update()
    {
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
        }
    }

    private void RemoveDeadUnit(int index, Unit u)
    {
        if (_unitGenerate != null && u != null)
        {
            _unitGenerate.RemoveVisual(u);
        }
        if (u != null) UnregisterUnitPos(u, u.position);
        units.RemoveAt(index);
        if (u != null) Destroy(u);
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

            if (GameSession.Instance != null)
                GameSession.Instance.RegisterUnitPos(human, human.position);
        }
    }

    private Vector2Int GetRandomStartRoomPos(Vector2 footprint, int floorIdx)
    {
        CreateMap mapGenerator = (GameSession.Instance != null && GameSession.Instance.cmap != null)
            ? GameSession.Instance.cmap
            : FindObjectOfType<CreateMap>();

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
        if (GameSession.Instance != null) GameSession.Instance.RegisterUnitPos(monster, monster.position);
        Debug.Log($"Generated Monster: {selection.typeName} at Floor {monster.currentFloor}, {monster.position}");
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
