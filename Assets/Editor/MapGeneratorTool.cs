using UnityEditor;
using UnityEngine;

// ========================================================================
// CreateMap 커스텀 인스펙터 (순수 생성 및 데이터 관리 전용)
// ========================================================================
public class MapGeneratorTool : EditorWindow
{
    private CreateMap cm;

    [MenuItem("Tools/GrimArchive/Map Generator")]
    public static void ShowWindow()
    {
        GetWindow<MapGeneratorTool>("Map Generator");
    }

    private void OnEnable()
    {
        if (cm == null)
            cm = new CreateMap();
    }

    private bool showGizmoSettings = false;
    private GUIStyle sectionHeaderStyle;
    private GUIStyle boxStyle;

    private void OnGUI()
    {
        if (cm == null) cm = new CreateMap();
        
        InitStyles();

        // ── 기본 프로퍼티 ──
        DrawDefaultProperties(cm);
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
        
        // ── Floor 선택 (Gizmo 확인용) ──
        if (cm.map.floors != null && cm.map.floors.Length > 0)
        {
            EditorGUILayout.Space(8);
            DrawGizmoFloorSelector(cm);
        }
    }

    void InitStyles()
    {
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

    void DrawDefaultProperties(CreateMap cm)
    {
        cm.useFixedSeed = EditorGUILayout.Toggle("Use Fixed Seed", cm.useFixedSeed);
        cm.seed = EditorGUILayout.IntField("Seed", cm.seed);
    }

    void DrawGenerateButton(CreateMap cm)
    {
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
        if (GUILayout.Button("▶ 맵 생성", GUILayout.Height(32)))
        {
            cm.GenerateMap();
            Repaint();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    void DrawValidationResult(CreateMap cm)
    {
        EditorGUILayout.Space(4);

        if (cm.lastValidationPassed)
        {
            EditorGUILayout.HelpBox($"✅ 검증 통과 (시도 {cm.lastRetryCount + 1}회)", MessageType.Info);
        }
        else if (cm.lastValidationErrors != null && cm.lastValidationErrors.Count > 0)
        {
            string errMsg = $"❌ 검증 실패 (시도 {cm.lastRetryCount + 1}회) — 오류 {cm.lastValidationErrors.Count}건:\n";
            foreach (string err in cm.lastValidationErrors)
                errMsg += $"  • {err}\n";
            EditorGUILayout.HelpBox(errMsg, MessageType.Error);
        }
    }

    void DrawSaveLoadButtons(CreateMap cm)
    {
        bool hasMap = cm.map.floors != null && cm.map.floors.Length > 0;

        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginDisabledGroup(!hasMap);
        GUI.backgroundColor = new Color(0.4f, 0.7f, 0.95f);
        if (GUILayout.Button("💾 기본 맵 저장 (Resources/Data)", GUILayout.Height(26)))
        {
            string dirPath = Application.dataPath + "/Resources/Data";
            if (!System.IO.Directory.Exists(dirPath))
                System.IO.Directory.CreateDirectory(dirPath);
            
            string path = dirPath + "/map.json";
            cm.SaveMapToFile(path);
            AssetDatabase.Refresh();
        }
        
        if (GUILayout.Button("💾 다른 이름으로 저장 (JSON)", GUILayout.Height(26)))
        {
            string defaultName = $"map_seed{cm.seed}.json";
            string path = EditorUtility.SaveFilePanel("맵 JSON 저장", Application.dataPath, defaultName, "json");
            if (!string.IsNullOrEmpty(path))
            {
                cm.SaveMapToFile(path);
                AssetDatabase.Refresh();
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
                Repaint();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    void DrawGizmoSettings(CreateMap cm)
    {
        showGizmoSettings = EditorGUILayout.Foldout(showGizmoSettings, "🔍 Scene Gizmo 설정", true, sectionHeaderStyle);
        if (!showGizmoSettings) return;

        EditorGUILayout.BeginVertical(boxStyle);
        cm.showGizmos = EditorGUILayout.Toggle("Gizmo 표시", cm.showGizmos);

        EditorGUI.BeginDisabledGroup(!cm.showGizmos);
        EditorGUI.indentLevel++;
        cm.gizmoShowRoomBounds = EditorGUILayout.Toggle("방 경계 + 역할 색상", cm.gizmoShowRoomBounds);
        cm.gizmoShowPassages = EditorGUILayout.Toggle("통로 위치", cm.gizmoShowPassages);
        cm.gizmoShowStairs = EditorGUILayout.Toggle("계단 위치", cm.gizmoShowStairs);
        cm.gizmoShowRoomLabels = EditorGUILayout.Toggle("방 라벨 (역할+ID)", cm.gizmoShowRoomLabels);
        EditorGUI.indentLevel--;
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.LabelField($"현재 Gizmo 대상: Floor {cm.currentFloorIndex}", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.EndVertical();
    }
    
    void DrawGizmoFloorSelector(CreateMap cm)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Gizmo Floor 선택:", EditorStyles.boldLabel, GUILayout.Width(120));

        for (int f = 0; f < cm.map.floors.Length; f++)
        {
            bool isSelected = (f == cm.currentFloorIndex);
            GUI.backgroundColor = isSelected ? Color.cyan : Color.white;
            string label = $"F{f}";

            if (GUILayout.Button(label, GUILayout.Height(24)))
            {
                cm.currentFloorIndex = f;
                SceneView.RepaintAll();
                Repaint();
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }
}
