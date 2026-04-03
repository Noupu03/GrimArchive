using UnityEditor;
using UnityEngine;

// ========================================================================
// CreateMap 커스텀 인스펙터
// ========================================================================
// 기능:
//   - 16×16 청크 그리드를 컬러 맵으로 시각화 (roomId 기반 색상)
//   - 청크 클릭 → 8×8 타일 상세 뷰 표시
//   - 타일 클릭 → 개별 타일 속성 표시
//   - 맵 생성 버튼 (인스펙터에서 바로 실행)
// ========================================================================

[CustomEditor(typeof(CreateMap))]
public class CreateMapInspector : Editor
{
    // 선택된 청크/타일 좌표
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

    // 색상 캐시: roomId → Color
    private readonly System.Collections.Generic.Dictionary<int, Color> roomColors
        = new System.Collections.Generic.Dictionary<int, Color>();

    // 스타일 캐시
    private GUIStyle centeredMiniLabel;
    private GUIStyle sectionHeaderStyle;
    private GUIStyle boxStyle;

    private const int ChunkCellSize = 28;
    private const int TileCellSize = 36;

    public override void OnInspectorGUI()
    {
        CreateMap cm = (CreateMap)target;
        InitStyles();

        // ── 기본 프로퍼티 ──
        DrawDefaultProperties();

        EditorGUILayout.Space(8);

        // ── 맵 생성 버튼 ──
        DrawGenerateButton(cm);

        // 맵 데이터가 없으면 여기서 종료
        if (cm.map.session == null) return;

        EditorGUILayout.Space(8);

        // ── 16×16 청크 그리드 ──
        DrawChunkGrid(cm);

        // ── 8×8 타일 그리드 (청크 선택 시) ──
        if (selectedChunkX >= 0 && selectedChunkY >= 0)
        {
            EditorGUILayout.Space(8);
            DrawTileGrid(cm);
        }

        // ── 타일 상세 정보 (타일 선택 시) ──
        if (selectedTileX >= 0 && selectedTileY >= 0
            && selectedChunkX >= 0 && selectedChunkY >= 0)
        {
            EditorGUILayout.Space(8);
            DrawTileDetail(cm);
        }
    }

    // ── 스타일 초기화 ──
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

    // ── 기본 프로퍼티 (시드 등) ──
    void DrawDefaultProperties()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("useFixedSeed"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("seed"));

        serializedObject.ApplyModifiedProperties();
    }

    // ── 맵 생성 버튼 ──
    void DrawGenerateButton(CreateMap cm)
    {
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
        if (GUILayout.Button("▶ 맵 생성", GUILayout.Height(32)))
        {
            cm.GenerateMap();
            roomColors.Clear();
            selectedChunkX = selectedChunkY = -1;
            selectedTileX = selectedTileY = -1;
            Repaint();
        }
        GUI.backgroundColor = Color.white;

        GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
        if (GUILayout.Button("✕ 선택 초기화", GUILayout.Height(32), GUILayout.Width(100)))
        {
            selectedChunkX = selectedChunkY = -1;
            selectedTileX = selectedTileY = -1;
            Repaint();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    // ── 16×16 청크 그리드 ──
    void DrawChunkGrid(CreateMap cm)
    {
        showChunkGrid = EditorGUILayout.Foldout(showChunkGrid, "📦 청크 그리드 (16×16)", true, sectionHeaderStyle);
        if (!showChunkGrid) return;

        EditorGUILayout.BeginVertical(boxStyle);

        // 범례
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("클릭하여 청크 선택", EditorStyles.miniLabel);
        if (selectedChunkX >= 0)
        {
            Chunks selChunk = cm.map.session[selectedChunkX, selectedChunkY];
            EditorGUILayout.LabelField(
                $"선택: [{selectedChunkX},{selectedChunkY}] {selChunk.roomName} (id:{selChunk.roomId})",
                EditorStyles.boldLabel);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // Y축 위에서 아래로 (15 → 0) 표시하여 직관적인 좌표계
        float gridWidth = 16 * ChunkCellSize + 30;
        float gridHeight = 16 * ChunkCellSize + 10;
        chunkScrollPos = EditorGUILayout.BeginScrollView(chunkScrollPos,
            GUILayout.MaxHeight(Mathf.Min(gridHeight + 20, 500)));

        for (int y = 15; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();

            // Y축 레이블
            GUILayout.Label(y.ToString("D2"), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(20));

            for (int x = 0; x < 16; x++)
            {
                Chunks c = cm.map.session[x, y];
                Color cellColor = GetRoomColor(c.roomId);

                // 선택된 청크 강조
                bool isSelected = (x == selectedChunkX && y == selectedChunkY);
                if (isSelected)
                    cellColor = Color.Lerp(cellColor, Color.white, 0.5f);

                GUI.backgroundColor = cellColor;

                string label = c.roomId >= 0 ? c.roomId.ToString() : "·";

                if (GUILayout.Button(label, GUILayout.Width(ChunkCellSize), GUILayout.Height(ChunkCellSize)))
                {
                    selectedChunkX = x;
                    selectedChunkY = y;
                    selectedTileX = selectedTileY = -1;
                    Repaint();
                }

                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndHorizontal();
        }

        // X축 레이블
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(24);
        for (int x = 0; x < 16; x++)
        {
            GUILayout.Label(x.ToString("D2"), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(ChunkCellSize));
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ── 8×8 타일 그리드 ──
    void DrawTileGrid(CreateMap cm)
    {
        Chunks chunk = cm.map.session[selectedChunkX, selectedChunkY];

        showTileGrid = EditorGUILayout.Foldout(showTileGrid,
            $"🧱 타일 그리드 (8×8) — 청크[{selectedChunkX},{selectedChunkY}] {chunk.roomName}",
            true, sectionHeaderStyle);
        if (!showTileGrid) return;

        if (chunk.chunk == null)
        {
            EditorGUILayout.HelpBox("이 청크에 타일 데이터가 없습니다.", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginVertical(boxStyle);

        // 청크 정보 요약
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Room: {chunk.roomName}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"ID: {chunk.roomId}", GUILayout.Width(80));
        EditorGUILayout.LabelField($"Landform: {chunk.landform}", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        tileScrollPos = EditorGUILayout.BeginScrollView(tileScrollPos, GUILayout.MaxHeight(360));

        for (int ty = 7; ty >= 0; ty--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(ty.ToString(), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(16));

            for (int tx = 0; tx < 8; tx++)
            {
                Tile t = chunk.chunk[tx, ty];
                Color tileColor = GetTileColor(t.name);

                bool isSelected = (tx == selectedTileX && ty == selectedTileY);
                if (isSelected)
                    tileColor = Color.Lerp(tileColor, Color.cyan, 0.5f);

                GUI.backgroundColor = tileColor;

                string label = GetTileLabel(t.name);

                if (GUILayout.Button(label, centeredMiniLabel,
                    GUILayout.Width(TileCellSize), GUILayout.Height(TileCellSize)))
                {
                    selectedTileX = tx;
                    selectedTileY = ty;
                    Repaint();
                }

                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndHorizontal();
        }

        // X축 레이블
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(20);
        for (int tx = 0; tx < 8; tx++)
        {
            GUILayout.Label(tx.ToString(), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(TileCellSize));
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ── 타일 상세 정보 ──
    void DrawTileDetail(CreateMap cm)
    {
        Chunks chunk = cm.map.session[selectedChunkX, selectedChunkY];
        if (chunk.chunk == null) return;

        Tile tile = chunk.chunk[selectedTileX, selectedTileY];

        showTileDetail = EditorGUILayout.Foldout(showTileDetail,
            $"📋 타일 상세 — [{selectedTileX},{selectedTileY}]",
            true, sectionHeaderStyle);
        if (!showTileDetail) return;

        EditorGUILayout.BeginVertical(boxStyle);

        // 월드 좌표 표시
        int worldX = selectedChunkX * 8 + selectedTileX;
        int worldY = selectedChunkY * 8 + selectedTileY;

        EditorGUILayout.BeginHorizontal();
        Color headerColor = GetTileColor(tile.name);
        GUI.backgroundColor = headerColor;
        GUILayout.Box(GetTileLabel(tile.name), centeredMiniLabel,
            GUILayout.Width(40), GUILayout.Height(40));
        GUI.backgroundColor = Color.white;

        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField($"타일: {tile.name ?? "(null)"}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"청크[{selectedChunkX},{selectedChunkY}] 타일[{selectedTileX},{selectedTileY}] → 월드({worldX},{worldY})");
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // 속성 테이블
        DrawDetailRow("이름 (name)", tile.name ?? "(null)");
        DrawDetailRow("효과 (effect)", tile.effect.ToString());
        DrawDetailRow("오브젝트 존재", tile.isObjectExist ? "✅ Yes" : "❌ No");
        DrawDetailRow("건물 존재", tile.isStructureExist ? "✅ Yes" : "❌ No");
        DrawDetailRow("위험도 (dangerous)", tile.dangerous.ToString());
        DrawDetailRow("이해도 (understand)", tile.understand.ToString());
        DrawDetailRow("가중치 (weight)", tile.weight.ToString());

        EditorGUILayout.EndVertical();
    }

    // ── 속성 행 그리기 ──
    void DrawDetailRow(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(140));
        EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();
    }

    // ── roomId → 색상 매핑 (고유 색상 자동 생성) ──
    Color GetRoomColor(int roomId)
    {
        if (roomId < 0)
            return new Color(0.2f, 0.2f, 0.2f); // 빈 청크: 어두운 회색

        if (!roomColors.TryGetValue(roomId, out Color color))
        {
            // 골든 레이시오 기반 색상 분산 (서로 잘 구분되는 색상 생성)
            float hue = (roomId * 0.618033988749895f) % 1f;
            color = Color.HSVToRGB(hue, 0.5f, 0.85f);
            roomColors[roomId] = color;
        }

        return color;
    }

    // ── 타일 이름 → 색상 ──
    Color GetTileColor(string tileName)
    {
        switch (tileName)
        {
            case "Wall":  return new Color(0.35f, 0.35f, 0.4f);
            case "Floor": return new Color(0.6f, 0.85f, 0.5f);
            default:      return new Color(0.5f, 0.5f, 0.5f);
        }
    }

    // ── 타일 이름 → 짧은 레이블 ──
    string GetTileLabel(string tileName)
    {
        switch (tileName)
        {
            case "Wall":  return "W";
            case "Floor": return "F";
            default:      return "?";
        }
    }
}
