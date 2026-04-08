using UnityEditor;
using UnityEngine;

// ========================================================================
// CreateMap 커스텀 인스펙터
// ========================================================================
// 기능:
//   - Floor 선택 탭 (F0~F3)
//   - 선택된 Floor의 청크 그리드를 컬러 맵으로 시각화 (roomId 기반 색상)
//   - 청크 클릭 → 8×8 타일 상세 뷰 표시
//   - 타일 클릭 → 개별 타일 속성 표시
//   - 맵 생성 버튼 (인스펙터에서 바로 실행)
// ========================================================================

[CustomEditor(typeof(CreateMap))]
public class CreateMapInspector : Editor
{
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
    private bool showGizmoSettings = false;

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

        // ── 검증 결과 ──
        DrawValidationResult(cm);

        EditorGUILayout.Space(8);

        // ── 맵 저장/로드 ──
        DrawSaveLoadButtons(cm);

        EditorGUILayout.Space(8);

        // ── Gizmo 설정 ──
        DrawGizmoSettings(cm);

        // 맵 데이터가 없으면 여기서 종료
        if (cm.map.floors == null || cm.map.floors.Length == 0) return;

        EditorGUILayout.Space(8);

        // ── Floor 선택 탭 ──
        DrawFloorSelector(cm);

        // 선택된 Floor가 유효한지 확인
        if (selectedFloor < 0 || selectedFloor >= cm.map.floors.Length) return;
        Floor floor = cm.map.floors[selectedFloor];
        if (floor.chunks == null) return;

        EditorGUILayout.Space(8);

        // ── 청크 그리드 ──
        DrawChunkGrid(cm, ref floor);

        // ── 8×8 타일 그리드 (청크 선택 시) ──
        if (selectedChunkX >= 0 && selectedChunkY >= 0)
        {
            EditorGUILayout.Space(8);
            DrawTileGrid(cm, ref floor);
        }

        // ── 타일 상세 정보 (타일 선택 시) ──
        if (selectedTileX >= 0 && selectedTileY >= 0
            && selectedChunkX >= 0 && selectedChunkY >= 0)
        {
            EditorGUILayout.Space(8);
            DrawTileDetail(cm, ref floor);
        }

        EditorGUILayout.Space(8);

        // ── Gate 정보 ──
        DrawGateInfo(ref floor);

        EditorGUILayout.Space(8);

        // ── 런타임 점령/후퇴/계단 버튼 ──
        DrawRuntimeActions(cm, ref floor);
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

            // 씬에 MapRandering이 있으면 타일맵도 자동 업데이트
            var mr = Object.FindObjectOfType<MapRandering>();
            if (mr != null)
                mr.DoRandering();

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

    // ── Floor 선택 탭 ──
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
                cm.currentFloorIndex = f;
                selectedChunkX = selectedChunkY = -1;
                selectedTileX = selectedTileY = -1;
                roomColors.Clear();
                SceneView.RepaintAll();
                Repaint();
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    // ── 청크 그리드 (Floor별 동적 크기) ──
    void DrawChunkGrid(CreateMap cm, ref Floor floor)
    {
        int fw = floor.config.width;
        int fh = floor.config.height;

        showChunkGrid = EditorGUILayout.Foldout(showChunkGrid,
            $"📦 청크 그리드 ({fw}×{fh}) — Floor {selectedFloor}", true, sectionHeaderStyle);
        if (!showChunkGrid) return;

        EditorGUILayout.BeginVertical(boxStyle);

        // 범례
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("클릭하여 청크 선택", EditorStyles.miniLabel);
        if (selectedChunkX >= 0 && selectedChunkX < fw && selectedChunkY >= 0 && selectedChunkY < fh)
        {
            Chunks selChunk = floor.chunks[selectedChunkX, selectedChunkY];
            EditorGUILayout.LabelField(
                $"선택: [{selectedChunkX},{selectedChunkY}] {selChunk.roomName} (id:{selChunk.roomId}) [{selChunk.roomRole}]",
                EditorStyles.boldLabel);
        }
        EditorGUILayout.EndHorizontal();

        // RoomRole 색상 범례
        EditorGUILayout.BeginHorizontal();
        DrawLegendBox("S:시작", new Color(0.3f, 0.9f, 0.3f));
        DrawLegendBox("B:보스", new Color(0.9f, 0.2f, 0.2f));
        DrawLegendBox("P:서브", new Color(0.9f, 0.7f, 0.2f));
        DrawLegendBox("N:일반", new Color(0.7f, 0.7f, 0.85f));
        EditorGUILayout.EndHorizontal();

        // OccupationState 범례
        EditorGUILayout.BeginHorizontal();
        DrawLegendBox("중립", new Color(0.6f, 0.6f, 0.6f));
        DrawLegendBox("아군", new Color(0.2f, 0.8f, 1.0f));
        DrawLegendBox("적군", new Color(1.0f, 0.3f, 0.3f));
        DrawLegendBox("전초", new Color(0.9f, 0.5f, 0.1f));
        EditorGUILayout.EndHorizontal();

        // 방 개수 통계
        DrawRoomCountStats(ref floor);

        EditorGUILayout.Space(4);

        float gridHeight = fh * ChunkCellSize + 10;
        chunkScrollPos = EditorGUILayout.BeginScrollView(chunkScrollPos,
            GUILayout.MaxHeight(Mathf.Min(gridHeight + 20, 500)));

        for (int y = fh - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();

            // Y축 레이블
            GUILayout.Label(y.ToString("D2"), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(20));

            for (int x = 0; x < fw; x++)
            {
                Chunks c = floor.chunks[x, y];
                Color cellColor = GetRoomColor(c.roomId, c.roomRole);

                // 선택된 청크 강조
                bool isSelected = (x == selectedChunkX && y == selectedChunkY);
                if (isSelected)
                    cellColor = Color.Lerp(cellColor, Color.white, 0.5f);

                GUI.backgroundColor = cellColor;

                string label = c.roomId >= 0 ? c.roomId.ToString() : "·";
                string rolePrefix = GetRolePrefix(c.roomRole);
                if (rolePrefix.Length > 0) label = rolePrefix;
                if (c.stairTargetFloor >= 0)
                {
                    string arrow = (c.stairTargetFloor > selectedFloor) ? "↑" : "↓";
                    label = arrow + c.stairTargetFloor;
                }

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
        for (int x = 0; x < fw; x++)
        {
            GUILayout.Label(x.ToString("D2"), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(ChunkCellSize));
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ── 8×8 타일 그리드 ──
    void DrawTileGrid(CreateMap cm, ref Floor floor)
    {
        Chunks chunk = floor.chunks[selectedChunkX, selectedChunkY];

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
        EditorGUILayout.LabelField($"ID: {chunk.roomId}");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Landform: {GetLandformLabel(chunk.landform)}");
        EditorGUILayout.LabelField($"Occupied: {chunk.occupationState}");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Role: {chunk.roomRole}");
        EditorGUILayout.LabelField($"Floor: {chunk.floorId}");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"MaxFootprint: {chunk.allowMaxFootprint}");
        if (chunk.stairTargetFloor >= 0)
            EditorGUILayout.LabelField($"StairOpen: {chunk.stairIsOpen}");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        Color occColor = GetOccupationColor(chunk.occupationState);
        GUI.backgroundColor = occColor;
        GUILayout.Box(chunk.occupationState.ToString(), centeredMiniLabel, GUILayout.Width(80), GUILayout.Height(16));
        GUI.backgroundColor = Color.white;
        // 대표 타일(2,2)의 dangerous/understand 표시
        if (chunk.chunk != null)
        {
            Tile sampleTile = chunk.chunk[2, 2];
            EditorGUILayout.LabelField($"위험도: {sampleTile.dangerous}  이해도: {sampleTile.understand}");
        }
        EditorGUILayout.EndHorizontal();

        if (chunk.stairTargetFloor >= 0)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"↑ 계단 → Floor {chunk.stairTargetFloor}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }

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

                bool isSel = (tx == selectedTileX && ty == selectedTileY);
                if (isSel)
                    tileColor = Color.Lerp(tileColor, Color.cyan, 0.5f);

                GUI.backgroundColor = tileColor;

                string tileLabel = GetTileLabel(t.name);

                if (GUILayout.Button(tileLabel, centeredMiniLabel,
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
    void DrawTileDetail(CreateMap cm, ref Floor floor)
    {
        Chunks chunk = floor.chunks[selectedChunkX, selectedChunkY];
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
        DrawDetailRow("가시성 (visibility)", tile.visibility.ToString());

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
        return GetRoomColor(roomId, RoomRole.None);
    }

    Color GetRoomColor(int roomId, RoomRole role)
    {
        if (roomId < 0)
            return new Color(0.2f, 0.2f, 0.2f); // 빈 청크: 어두운 회색

        // RoomRole 기반 고정 색상
        switch (role)
        {
            case RoomRole.StartRoom:      return new Color(0.3f, 0.9f, 0.3f);  // 녹색
            case RoomRole.BossRoom:       return new Color(0.9f, 0.2f, 0.2f);  // 빨간색
            case RoomRole.SubPurposeRoom: return new Color(0.9f, 0.7f, 0.2f);  // 주황색
        }

        if (!roomColors.TryGetValue(roomId, out Color color))
        {
            // 골든 레이시오 기반 색상 분산 (서로 잘 구분되는 색상 생성)
            float hue = (roomId * 0.618033988749895f) % 1f;
            color = Color.HSVToRGB(hue, 0.5f, 0.85f);
            roomColors[roomId] = color;
        }

        return color;
    }

    // ── 범례 색상 박스 ──
    void DrawLegendBox(string label, Color color)
    {
        GUI.backgroundColor = color;
        GUILayout.Box(label, centeredMiniLabel, GUILayout.Width(52), GUILayout.Height(16));
        GUI.backgroundColor = Color.white;
    }

    // ── RoomRole → 짧은 프리픽스 ──
    string GetRolePrefix(RoomRole role)
    {
        switch (role)
        {
            case RoomRole.StartRoom:      return "S";
            case RoomRole.BossRoom:       return "B";
            case RoomRole.SubPurposeRoom: return "P";
            case RoomRole.NormalRoom:     return "N";
            default:                      return "";
        }
    }

    // ── OccupationState → 색상 ──
    Color GetOccupationColor(OccupationState state)
    {
        switch (state)
        {
            case OccupationState.PlayerControlled: return new Color(0.2f, 0.8f, 1.0f);
            case OccupationState.Occupied:         return new Color(1.0f, 0.3f, 0.3f);
            case OccupationState.Outpost:          return new Color(0.9f, 0.5f, 0.1f);
            default:                               return new Color(0.6f, 0.6f, 0.6f);
        }
    }

    // ── landform → 레이블 ──
    string GetLandformLabel(int landform)
    {
        switch (landform)
        {
            case 0: return "0 (기본)";
            case 1: return "1 (변형A)";
            case 2: return "2 (변형B)";
            case 3: return "3 (서브목적)";
            case 4: return "4 (보스)";
            default: return landform.ToString();
        }
    }

    // ── 타일 이름 → 색상 ──
    Color GetTileColor(string tileName)
    {
        switch (tileName)
        {
            case "Wall":  return new Color(0.35f, 0.35f, 0.4f);
            case "Floor": return new Color(0.6f, 0.85f, 0.5f);
            case "Stair": return new Color(0.4f, 0.6f, 0.95f);
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
            case "Stair": return "↑";
            default:      return "?";
        }
    }

            // ── 방 개수 통계 (Floor별) ──
        void DrawRoomCountStats(ref Floor floor)
        {
            int fw = floor.config.width;
            int fh = floor.config.height;

            var normalIds = new System.Collections.Generic.HashSet<int>();
            var subIds = new System.Collections.Generic.HashSet<int>();
            var bossIds = new System.Collections.Generic.HashSet<int>();
            var startIds = new System.Collections.Generic.HashSet<int>();
            int emptyCount = 0;

            for (int x = 0; x < fw; x++)
            {
                for (int y = 0; y < fh; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    switch (c.roomRole)
                    {
                        case RoomRole.NormalRoom:      normalIds.Add(c.roomId); break;
                        case RoomRole.SubPurposeRoom: subIds.Add(c.roomId); break;
                        case RoomRole.BossRoom:       bossIds.Add(c.roomId); break;
                        case RoomRole.StartRoom:      startIds.Add(c.roomId); break;
                        default:
                            if (c.roomId == -1) emptyCount++;
                            break;
                    }
                }
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"방 수: S:{startIds.Count} B:{bossIds.Count} P:{subIds.Count}/{floor.config.subPurposeRoomCount} N:{normalIds.Count}/{floor.config.normalRoomCount} 빈칸:{emptyCount}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        // ── 맵 저장/로드 버튼 ──
        void DrawSaveLoadButtons(CreateMap cm)
        {
            bool hasMap = cm.map.floors != null && cm.map.floors.Length > 0;

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(!hasMap);
            GUI.backgroundColor = new Color(0.4f, 0.7f, 0.95f);
            if (GUILayout.Button("💾 맵 저장 (JSON)", GUILayout.Height(26)))
            {
                string defaultName = $"map_seed{cm.seed}.json";
                string path = EditorUtility.SaveFilePanel("맵 JSON 저장", Application.dataPath, defaultName, "json");
                if (!string.IsNullOrEmpty(path))
                {
                    cm.SaveMapToFile(path);
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.EndDisabledGroup();

            GUI.backgroundColor = new Color(0.95f, 0.8f, 0.4f);
            if (GUILayout.Button("📂 맵 로드 (JSON)", GUILayout.Height(26)))
            {
                string path = EditorUtility.OpenFilePanel("맵 JSON 로드", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(path))
                {
                    cm.LoadMapFromFile(path);

                    var mr = Object.FindObjectOfType<MapRandering>();
                    if (mr != null)
                        mr.DoRandering();

                    roomColors.Clear();
                    selectedChunkX = selectedChunkY = -1;
                    selectedTileX = selectedTileY = -1;
                    Repaint();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        // ── Gizmo 설정 ──
        void DrawGizmoSettings(CreateMap cm)
        {
            showGizmoSettings = EditorGUILayout.Foldout(showGizmoSettings,
                "🔍 Scene Gizmo 설정", true, sectionHeaderStyle);
            if (!showGizmoSettings) return;

            EditorGUILayout.BeginVertical(boxStyle);

            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("showGizmos"),
                new GUIContent("Gizmo 표시"));

            EditorGUI.BeginDisabledGroup(!cm.showGizmos);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("gizmoShowRoomBounds"),
                new GUIContent("방 경계 + 역할 색상"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gizmoShowPassages"),
                new GUIContent("통로 위치"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gizmoShowStairs"),
                new GUIContent("계단 위치"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gizmoShowRoomLabels"),
                new GUIContent("방 라벨 (역할+ID)"));

            EditorGUI.indentLevel--;
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();

            // 현재 Floor 안내
            EditorGUILayout.LabelField(
                $"현재 Gizmo 대상: Floor {cm.currentFloorIndex}",
                EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndVertical();
        }

        // ── 검증 결과 표시 ──
        void DrawValidationResult(CreateMap cm)
        {
            EditorGUILayout.Space(4);

            if (cm.lastValidationPassed)
            {
                EditorGUILayout.HelpBox(
                    $"✅ 검증 통과 (시도 {cm.lastRetryCount + 1}회)",
                    MessageType.Info);
            }
            else if (cm.lastValidationErrors != null && cm.lastValidationErrors.Count > 0)
            {
                string errMsg = $"❌ 검증 실패 (시도 {cm.lastRetryCount + 1}회) — 오류 {cm.lastValidationErrors.Count}건:\n";
                foreach (string err in cm.lastValidationErrors)
                    errMsg += $"  • {err}\n";
                EditorGUILayout.HelpBox(errMsg, MessageType.Error);
            }
        }

        // ── Gate 정보 표시 ──
        private bool showGateInfo = false;
        void DrawGateInfo(ref Floor floor)
        {
            if (floor.gates == null || floor.gates.Count == 0) return;

            showGateInfo = EditorGUILayout.Foldout(showGateInfo,
                $"🚪 Gate 목록 ({floor.gates.Count}개) — Floor {selectedFloor}", true, sectionHeaderStyle);
            if (!showGateInfo) return;

            EditorGUILayout.BeginVertical(boxStyle);
            for (int i = 0; i < floor.gates.Count && i < 30; i++)
            {
                Gate g = floor.gates[i];
                string dir = g.isHorizontal ? "H" : "V";
                EditorGUILayout.LabelField(
                    $"  [{i}] {dir} Room{g.roomA}↔Room{g.roomB}  Chunk({g.chunkAX},{g.chunkAY})↔({g.chunkBX},{g.chunkBY})  W={g.width}",
                    EditorStyles.miniLabel);
            }
            if (floor.gates.Count > 30)
                EditorGUILayout.LabelField($"  ... 외 {floor.gates.Count - 30}개", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        // ── 런타임 점령/후퇴/계단 버튼 ──
        private int runtimeTargetRoomId = 0;
        private int runtimeTargetStairFloor = 1;
        void DrawRuntimeActions(CreateMap cm, ref Floor floor)
        {
            EditorGUILayout.LabelField("⚔ 런타임 액션", sectionHeaderStyle);
            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Room ID:", GUILayout.Width(60));
            runtimeTargetRoomId = EditorGUILayout.IntField(runtimeTargetRoomId, GUILayout.Width(60));

            GUI.backgroundColor = new Color(0.2f, 0.8f, 1.0f);
            if (GUILayout.Button("점령", GUILayout.Width(50)))
            {
                cm.ConquerRoom(selectedFloor, runtimeTargetRoomId);
                Repaint();
            }
            GUI.backgroundColor = new Color(1.0f, 0.5f, 0.3f);
            if (GUILayout.Button("후퇴", GUILayout.Width(50)))
            {
                cm.RetreatFromRoom(selectedFloor, runtimeTargetRoomId);
                Repaint();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("계단 → F:", GUILayout.Width(60));
            runtimeTargetStairFloor = EditorGUILayout.IntField(runtimeTargetStairFloor, GUILayout.Width(60));

            GUI.backgroundColor = new Color(0.3f, 0.5f, 1.0f);
            if (GUILayout.Button("계단 개방", GUILayout.Width(80)))
            {
                cm.OpenStair(selectedFloor, runtimeTargetStairFloor);
                Repaint();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
    }
