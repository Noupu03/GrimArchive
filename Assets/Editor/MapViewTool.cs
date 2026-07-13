using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

// ========================================================================
// Map View 에디터 툴 (런타임 데이터 읽기 전용)
// ========================================================================
public class MapViewTool : EditorWindow
{
    private CreateMap cm;

    [MenuItem("Tools/GrimArchive/Map View")]
    public static void ShowWindow()
    {
        GetWindow<MapViewTool>("Map View");
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        MapManager.OnRuntimeMapDataUpdated += OnMapUpdated;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        MapManager.OnRuntimeMapDataUpdated -= OnMapUpdated;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            cm = null;
            Repaint();
        }
    }

    private void OnMapUpdated(CreateMap runtimeMap)
    {
        cm = runtimeMap;
        Repaint();
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

        EditorGUILayout.LabelField("🗺️ 런타임 맵 뷰어", sectionHeaderStyle);
        EditorGUILayout.Space(8);

        if (cm == null || cm.map.floors == null || cm.map.floors.Length == 0)
        {
            EditorGUILayout.HelpBox("현재 런타임 맵 데이터가 없습니다.\n게임을 플레이하여 맵 데이터를 로드하거나 런타임 상태여야 합니다.", MessageType.Info);
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
        showTileGrid = EditorGUILayout.Foldout(showTileGrid, $"🧱 타일 그리드 (8×8) — 청크[{selectedChunkX},{selectedChunkY}] {chunk.roomName}", true, sectionHeaderStyle);
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
        for (int ty = 7; ty >= 0; ty--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(ty.ToString(), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(16));
            for (int tx = 0; tx < 8; tx++)
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
