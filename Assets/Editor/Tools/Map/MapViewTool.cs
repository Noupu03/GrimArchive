using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

// ========================================================================
// Map View 에디터 툴 (맵 데이터 읽기 전용)
// 플레이 중: MapManager가 브로드캐스트한 런타임 맵만 보여 준다.
// 플레이 전: GameSession이 시작할 때 읽는 Data/map.json(초기 상태)을 자동으로 읽고,
//           Map Generator 결과나 임의의 JSON 파일로 바꿔 볼 수 있다.
// ========================================================================
public class MapViewTool : EditorWindow
{
    // 지금 보고 있는 맵 데이터의 출처
    private enum MapSource { None, Runtime, DefaultFile, Generator, CustomFile }

    // GameSession.Initialize가 Addressables 주소 "Data/map"(AssetKeys.Map)으로 읽는 파일과 같아야 한다
    private const string DefaultMapAssetPath = "Assets/Data/map.json";

    private CreateMap cm;
    private MapSource source = MapSource.None;
    private string sourcePath = string.Empty;
    private string loadError = string.Empty;

    [MenuItem("Tools(new)/맵 뷰")]
    public static void ShowWindow()
    {
        GetWindow<MapViewTool>("Map View");
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        MapManager.OnRuntimeMapDataUpdated += OnMapUpdated;
        MapGeneratorTool.OnEditorMapChanged += OnGeneratorMapChanged;

        // 플레이 전에도 플레이를 누르면 쓰일 맵을 바로 보여 준다
        if (cm == null && !EditorApplication.isPlayingOrWillChangePlaymode)
            LoadDefaultMap();
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        MapManager.OnRuntimeMapDataUpdated -= OnMapUpdated;
        MapGeneratorTool.OnEditorMapChanged -= OnGeneratorMapChanged;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            // 플레이 중에는 런타임 데이터만 보여 준다 — 파일/생성기 데이터를 비우고 브로드캐스트를 기다린다.
            // EnteredPlayMode가 아니라 ExitingEditMode에서 비우는 이유: 런타임 브로드캐스트보다 항상 먼저 오기 때문.
            case PlayModeStateChange.ExitingEditMode:
            case PlayModeStateChange.ExitingPlayMode:
                SetMap(null, MapSource.None, string.Empty);
                break;
            case PlayModeStateChange.EnteredEditMode:
                LoadDefaultMap();
                break;
        }
    }

    private void OnMapUpdated(CreateMap runtimeMap)
    {
        SetMap(runtimeMap, MapSource.Runtime, string.Empty);
    }

    private void OnGeneratorMapChanged(CreateMap generatedMap)
    {
        if (EditorApplication.isPlaying) return;
        SetMap(generatedMap, MapSource.Generator, string.Empty);
    }

    // ── 맵 데이터 불러오기 ──

    void SetMap(CreateMap map, MapSource src, string path)
    {
        cm = map;
        source = map != null ? src : MapSource.None;
        sourcePath = path;
        loadError = string.Empty;
        roomColors.Clear();
        ClampSelection();
        Repaint();
    }

    void LoadDefaultMap()
    {
        TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultMapAssetPath);
        if (asset == null || string.IsNullOrEmpty(asset.text))
        {
            loadError = $"{DefaultMapAssetPath}이 없습니다. Map Generator에서 '기본 맵 저장'을 먼저 하세요.";
            Repaint();
            return;
        }
        TryLoadJson(asset.text, MapSource.DefaultFile, AssetDatabase.GetAssetPath(asset));
    }

    void OpenCustomFile()
    {
        string dir = Application.dataPath + "/Data";
        string path = EditorUtility.OpenFilePanel("맵 JSON 열기", Directory.Exists(dir) ? dir : Application.dataPath, "json");
        if (string.IsNullOrEmpty(path)) return;

        string json;
        try
        {
            json = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (System.Exception ex)
        {
            loadError = $"파일을 읽지 못했습니다 — {path}\n{ex.Message}";
            Repaint();
            return;
        }

        string projectRelative = FileUtil.GetProjectRelativePath(path);
        TryLoadJson(json, MapSource.CustomFile, string.IsNullOrEmpty(projectRelative) ? path : projectRelative);
    }

    // 실패하면 지금 보던 맵은 그대로 두고 오류만 표시한다
    void TryLoadJson(string json, MapSource src, string path)
    {
        try
        {
            var loaded = new CreateMap();
            loaded.DeserializeMap(json);
            if (loaded.map.floors == null || loaded.map.floors.Length == 0)
                throw new System.Exception("층 데이터가 비어 있습니다.");
            SetMap(loaded, src, path);
        }
        catch (System.Exception ex)
        {
            loadError = $"맵 JSON을 읽지 못했습니다 — {path}\n{ex.Message}";
            Repaint();
        }
    }

    // 다른 크기의 맵으로 바뀌어도 이전 선택 좌표로 범위 밖을 읽지 않게 한다
    void ClampSelection()
    {
        if (cm == null || cm.map.floors == null || cm.map.floors.Length == 0) return;

        if (selectedFloor < 0 || selectedFloor >= cm.map.floors.Length)
        {
            selectedFloor = Mathf.Clamp(selectedFloor, 0, cm.map.floors.Length - 1);
            selectedChunkX = selectedChunkY = -1;
            selectedTileX = selectedTileY = -1;
            return;
        }

        FloorConfig cfg = cm.map.floors[selectedFloor].config;
        if (selectedChunkX >= cfg.width || selectedChunkY >= cfg.height)
        {
            selectedChunkX = selectedChunkY = -1;
            selectedTileX = selectedTileY = -1;
        }
        if (selectedTileX >= cfg.chunkSize || selectedTileY >= cfg.chunkSize)
            selectedTileX = selectedTileY = -1;
    }

    // 선택된 Floor / 청크 / 타일 좌표
    private int selectedFloor = 1;
    private int selectedChunkX = -1;
    private int selectedChunkY = -1;
    private int selectedTileX = -1;
    private int selectedTileY = -1;

    // 스크롤 위치
    private Vector2 chunkScrollPos;
    private Vector2 tileScrollPos;

    // 접힘 상태
    private bool showChunkGrid = true;
    private bool showTileGrid = true;
    private bool showTileDetail = true;

    // 색상 캐시
    private readonly Dictionary<int, Color> roomColors = new Dictionary<int, Color>();

    // 스타일 캐시
    private GUIStyle centeredMiniLabel;
    private GUIStyle sectionHeaderStyle;
    private GUIStyle boxStyle;

    private const int ChunkCellSize = 28;
    private const int TileCellSize = 36;

    private void OnGUI()
    {
        InitStyles();

        EditorGUILayout.LabelField("🗺️ 맵 뷰어", sectionHeaderStyle);
        EditorGUILayout.Space(4);
        DrawSourceBar();
        EditorGUILayout.Space(8);

        if (cm == null || cm.map.floors == null || cm.map.floors.Length == 0)
        {
            string msg = EditorApplication.isPlaying
                ? "런타임 맵 데이터를 기다리는 중입니다.\n(맵이 세팅된 뒤에 이 창을 열었다면 플레이를 다시 시작해야 합니다.)"
                : "표시할 맵 데이터가 없습니다.\n위 버튼으로 기본 맵 파일, Map Generator 결과, JSON 파일 중 하나를 불러오세요.";
            EditorGUILayout.HelpBox(msg, MessageType.Info);
            return;
        }

        DrawFloorSelector(cm);

        if (selectedFloor < 0 || selectedFloor >= cm.map.floors.Length) return;
        Floor floor = cm.map.floors[selectedFloor];
        if (floor.chunks == null) return;

        EditorGUILayout.Space(8);
        DrawChunkGrid(cm, ref floor);

        if (selectedChunkX >= 0 && selectedChunkY >= 0)
        {
            EditorGUILayout.Space(8);
            DrawTileGrid(cm, ref floor);
        }

        if (selectedTileX >= 0 && selectedTileY >= 0 && selectedChunkX >= 0 && selectedChunkY >= 0)
        {
            EditorGUILayout.Space(8);
            DrawTileDetail(cm, ref floor);
        }
    }

    void InitStyles()
    {
        if (centeredMiniLabel == null)
        {
            centeredMiniLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 9,
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Bold
            };
        }
        if (sectionHeaderStyle == null)
        {
            sectionHeaderStyle = new GUIStyle(EditorStyles.foldoutHeader)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
        }
        if (boxStyle == null)
        {
            boxStyle = new GUIStyle("HelpBox")
            {
                padding = new RectOffset(8, 8, 8, 8)
            };
        }
    }

    void DrawSourceBar()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("데이터 출처", GetSourceLabel(), EditorStyles.boldLabel);

        // 플레이 중에는 런타임 데이터만 보여 준다
        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("📄 기본 맵 (Data/map)", "플레이를 누르면 GameSession이 읽는 맵"), GUILayout.Height(22)))
            LoadDefaultMap();

        CreateMap generated = MapGeneratorTool.LatestMap;
        EditorGUI.BeginDisabledGroup(generated == null || generated.map.floors == null || generated.map.floors.Length == 0);
        if (GUILayout.Button(new GUIContent("🛠 Map Generator 결과", "Map Generator 창에서 맵을 생성하거나 로드하면 활성화됩니다"), GUILayout.Height(22)))
            SetMap(generated, MapSource.Generator, string.Empty);
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("📂 JSON 열기...", GUILayout.Height(22)))
            OpenCustomFile();
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();

        if (source != MapSource.None && source != MapSource.Runtime)
            EditorGUILayout.HelpBox("플레이 전 초기 상태입니다. 플레이 중 건물 설치·점령 등으로 바뀌는 값(건물 존재, 이해도 등)은 반영되지 않습니다.", MessageType.None);
        if (!string.IsNullOrEmpty(loadError))
            EditorGUILayout.HelpBox(loadError, MessageType.Error);
        EditorGUILayout.EndVertical();
    }

    string GetSourceLabel()
    {
        switch (source)
        {
            case MapSource.Runtime: return "런타임 (플레이 중 실제 맵)";
            case MapSource.DefaultFile: return $"기본 맵 파일 — {sourcePath}";
            case MapSource.Generator: return "Map Generator 결과 (저장 전일 수 있음)";
            case MapSource.CustomFile: return $"JSON 파일 — {sourcePath}";
            default: return "없음";
        }
    }

    void DrawFloorSelector(CreateMap cm)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Floor 선택:", EditorStyles.boldLabel, GUILayout.Width(80));
        for (int f = 0; f < cm.map.floors.Length; f++)
        {
            bool isSelected = (f == selectedFloor);
            GUI.backgroundColor = isSelected ? Color.cyan : Color.white;
            FloorConfig cfg = cm.map.floors[f].config;
            string label = $"F{f} ({cfg.width}×{cfg.height})";

            if (GUILayout.Button(label, GUILayout.Height(24)))
            {
                selectedFloor = f;
                selectedChunkX = selectedChunkY = -1;
                selectedTileX = selectedTileY = -1;
                roomColors.Clear();
                Repaint();
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    void DrawChunkGrid(CreateMap cm, ref Floor floor)
    {
        int fw = floor.config.width;
        int fh = floor.config.height;

        showChunkGrid = EditorGUILayout.Foldout(showChunkGrid, $"📦 청크 그리드 ({fw}×{fh}) — Floor {selectedFloor}", true, sectionHeaderStyle);
        if (!showChunkGrid) return;

        EditorGUILayout.BeginVertical(boxStyle);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("클릭하여 청크 선택", EditorStyles.miniLabel);
        if (selectedChunkX >= 0 && selectedChunkX < fw && selectedChunkY >= 0 && selectedChunkY < fh)
        {
            Chunks selChunk = floor.chunks[selectedChunkX, selectedChunkY];
            EditorGUILayout.LabelField($"선택: [{selectedChunkX},{selectedChunkY}] {selChunk.roomName} (id:{selChunk.roomId}) [{selChunk.roomRole}]", EditorStyles.boldLabel);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DrawLegendBox("S:시작", new Color(0.3f, 0.9f, 0.3f));
        DrawLegendBox("B:보스", new Color(0.9f, 0.2f, 0.2f));
        DrawLegendBox("P:서브", new Color(0.9f, 0.7f, 0.2f));
        DrawLegendBox("N:일반", new Color(0.7f, 0.7f, 0.85f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DrawLegendBox("중립", new Color(0.6f, 0.6f, 0.6f));
        DrawLegendBox("아군", new Color(0.2f, 0.8f, 1.0f));
        DrawLegendBox("적군", new Color(1.0f, 0.3f, 0.3f));
        DrawLegendBox("전초", new Color(0.9f, 0.5f, 0.1f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        float gridHeight = fh * ChunkCellSize + 10;
        chunkScrollPos = EditorGUILayout.BeginScrollView(chunkScrollPos, GUILayout.MaxHeight(Mathf.Min(gridHeight + 20, 500)));

        for (int y = fh - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(y.ToString("D2"), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(20));

            for (int x = 0; x < fw; x++)
            {
                Chunks c = floor.chunks[x, y];
                Color cellColor = GetRoomColor(c.roomId, c.roomRole);
                if (x == selectedChunkX && y == selectedChunkY) cellColor = Color.Lerp(cellColor, Color.white, 0.5f);
                
                GUI.backgroundColor = cellColor;
                string label = c.roomId >= 0 ? c.roomId.ToString() : "·";
                string rolePrefix = GetRolePrefix(c.roomRole);
                if (rolePrefix.Length > 0) label = rolePrefix;
                if (c.stairTargetFloor >= 0) label = ((c.stairTargetFloor > selectedFloor) ? "↑" : "↓") + c.stairTargetFloor;

                if (GUILayout.Button(label, GUILayout.Width(ChunkCellSize), GUILayout.Height(ChunkCellSize)))
                {
                    selectedChunkX = x; selectedChunkY = y;
                    selectedTileX = selectedTileY = -1;
                    Repaint();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(24);
        for (int x = 0; x < fw; x++) GUILayout.Label(x.ToString("D2"), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(ChunkCellSize));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void DrawTileGrid(CreateMap cm, ref Floor floor)
    {
        Chunks chunk = floor.chunks[selectedChunkX, selectedChunkY];
        int cs = chunk.chunk != null ? chunk.chunk.GetLength(0) : floor.config.chunkSize;
        showTileGrid = EditorGUILayout.Foldout(showTileGrid, $"🧱 타일 그리드 ({cs}×{cs}) — 청크[{selectedChunkX},{selectedChunkY}] {chunk.roomName}", true, sectionHeaderStyle);
        if (!showTileGrid) return;

        if (chunk.chunk == null)
        {
            EditorGUILayout.HelpBox("이 청크에 타일 데이터가 없습니다.", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Room: {chunk.roomName}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"ID: {chunk.roomId}");
        EditorGUILayout.EndHorizontal();

        tileScrollPos = EditorGUILayout.BeginScrollView(tileScrollPos, GUILayout.MaxHeight(360));
        for (int ty = cs - 1; ty >= 0; ty--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(ty.ToString(), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(16));
            for (int tx = 0; tx < cs; tx++)
            {
                Tile t = chunk.chunk[tx, ty];
                Color tileColor = GetTileColor(t.name);
                if (tx == selectedTileX && ty == selectedTileY) tileColor = Color.Lerp(tileColor, Color.cyan, 0.5f);
                GUI.backgroundColor = tileColor;
                if (GUILayout.Button(GetTileLabel(t.name), centeredMiniLabel, GUILayout.Width(TileCellSize), GUILayout.Height(TileCellSize)))
                {
                    selectedTileX = tx; selectedTileY = ty;
                    Repaint();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void DrawTileDetail(CreateMap cm, ref Floor floor)
    {
        Chunks chunk = floor.chunks[selectedChunkX, selectedChunkY];
        if (chunk.chunk == null) return;
        Tile tile = chunk.chunk[selectedTileX, selectedTileY];

        showTileDetail = EditorGUILayout.Foldout(showTileDetail, $"📋 타일 상세 — [{selectedTileX},{selectedTileY}]", true, sectionHeaderStyle);
        if (!showTileDetail) return;

        EditorGUILayout.BeginVertical(boxStyle);
        DrawDetailRow("이름 (name)", tile.name ?? "(null)");
        DrawDetailRow("오브젝트 존재", tile.isObjectExist ? "✅ Yes" : "❌ No");
        DrawDetailRow("건물 존재", tile.isStructureExist ? "✅ Yes" : "❌ No");
        DrawDetailRow("위험도 (dangerous)", tile.dangerous.ToString());
        DrawDetailRow("이해도 (understand)", tile.understand.ToString());
        EditorGUILayout.EndVertical();
    }

    void DrawDetailRow(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(140));
        EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();
    }

    Color GetRoomColor(int roomId, RoomRole role)
    {
        if (roomId < 0) return new Color(0.2f, 0.2f, 0.2f);
        switch (role) {
            case RoomRole.StartRoom: return new Color(0.3f, 0.9f, 0.3f);
            case RoomRole.BossRoom: return new Color(0.9f, 0.2f, 0.2f);
            case RoomRole.SubPurposeRoom: return new Color(0.9f, 0.7f, 0.2f);
        }
        if (!roomColors.TryGetValue(roomId, out Color color)) {
            float hue = (roomId * 0.618033988749895f) % 1f;
            color = Color.HSVToRGB(hue, 0.5f, 0.85f);
            roomColors[roomId] = color;
        }
        return color;
    }

    void DrawLegendBox(string label, Color color)
    {
        GUI.backgroundColor = color;
        GUILayout.Box(label, centeredMiniLabel, GUILayout.Width(52), GUILayout.Height(16));
        GUI.backgroundColor = Color.white;
    }

    string GetRolePrefix(RoomRole role)
    {
        switch (role) {
            case RoomRole.StartRoom: return "S";
            case RoomRole.BossRoom: return "B";
            case RoomRole.SubPurposeRoom: return "P";
            case RoomRole.NormalRoom: return "N";
            default: return "";
        }
    }

    Color GetTileColor(string tileName)
    {
        switch (tileName) {
            case "Wall": return new Color(0.35f, 0.35f, 0.4f);
            case "Floor": return new Color(0.6f, 0.85f, 0.5f);
            case "Stair": return new Color(0.4f, 0.6f, 0.95f);
            default: return new Color(0.5f, 0.5f, 0.5f);
        }
    }

    string GetTileLabel(string tileName)
    {
        switch (tileName) {
            case "Wall": return "W";
            case "Floor": return "F";
            case "Stair": return "↑";
            default: return "?";
        }
    }
}
