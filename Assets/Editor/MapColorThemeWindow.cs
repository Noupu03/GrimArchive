using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ============================================================================
// MapColorThemeWindow — 벽/바닥 스프라이트 색상 초기값 테마 관리. 런타임 조정이 아니라, 프리셋을 층마다(또는 기본값으로) 배정하면 Assets/Resources/MapColorTheme_FloorAssignments.asset(MapFloorColorThemes)에 저장되고 다음 맵 생성부터 MapRandering이 층별로 Tile.color에 적용한다(GetOrBuildFloorTileSet).
// ============================================================================
public class MapColorThemeWindow : EditorWindow
{
    private const string FloorAssignmentsAssetPath = "Assets/Resources/MapColorTheme_FloorAssignments.asset";
    private const string PresetFolder = "Assets/Resources/Themes";

    // FloorId(MapData.cs) 0~3 그대로 — 이 프로젝트는 층 4개로 고정돼 있다(Floor_0=로비 ~ Floor_3).
    private static readonly string[] FloorLabels = { "0층 (로비)", "1층", "2층", "3층" };

    private MapColorTheme[] _presets = System.Array.Empty<MapColorTheme>();
    private int _selectedIndex = -1;
    private Vector2 _scroll;
    private MapFloorColorThemes _floorAssignments;

    [MenuItem("Tools(new)/맵 타일 색상 테마")]
    public static void ShowWindow()
    {
        var window = GetWindow<MapColorThemeWindow>("맵 타일 색상 테마");
        window.RefreshPresets();
        window.LoadFloorAssignments();
    }

    private void OnEnable()
    {
        RefreshPresets();
        LoadFloorAssignments();
    }

    void LoadFloorAssignments()
    {
        _floorAssignments = AssetDatabase.LoadAssetAtPath<MapFloorColorThemes>(FloorAssignmentsAssetPath);
    }

    void RefreshPresets()
    {
        var guids = AssetDatabase.FindAssets("t:MapColorTheme");
        _presets = guids
            .Select(g => AssetDatabase.LoadAssetAtPath<MapColorTheme>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(t => t != null)
            .OrderBy(t => t.themeName)
            .ToArray();
        if (_selectedIndex >= _presets.Length) _selectedIndex = _presets.Length - 1;
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "런타임 조정 기능이 아닙니다. 프리셋을 만들고 아래에서 층마다 배정하면 다음 맵 생성부터 그 층에 적용됩니다.",
            MessageType.Info);
        EditorGUILayout.Space(6);

        DrawPresetList();
        EditorGUILayout.Space(8);
        DrawSelectedPresetEditor();
        EditorGUILayout.Space(12);
        DrawFloorAssignments();
    }

    void DrawPresetList()
    {
        EditorGUILayout.LabelField("프리셋 목록", EditorStyles.boldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(Mathf.Min(160, 24 * Mathf.Max(1, _presets.Length))));
        for (int i = 0; i < _presets.Length; i++)
        {
            var preset = _presets[i];
            if (preset == null) continue;

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = (i == _selectedIndex) ? Color.cyan : Color.white;
            if (GUILayout.Button(preset.themeName, GUILayout.Height(22)))
                _selectedIndex = i;
            GUI.backgroundColor = Color.white;

            EditorGUI.DrawRect(GUILayoutUtility.GetRect(24, 20), preset.wallColor);
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(24, 20), preset.floorColor);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("+ 새 프리셋 만들기", GUILayout.Height(26)))
            CreateNewPreset();
    }

    void CreateNewPreset()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(PresetFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "Themes");

        string path = AssetDatabase.GenerateUniqueAssetPath($"{PresetFolder}/MapColorTheme_New.asset");
        var theme = ScriptableObject.CreateInstance<MapColorTheme>();
        theme.themeName = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(theme, path);
        AssetDatabase.SaveAssets();

        RefreshPresets();
        _selectedIndex = System.Array.IndexOf(_presets, theme);
    }

    void DrawSelectedPresetEditor()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _presets.Length)
        {
            EditorGUILayout.HelpBox("편집할 프리셋을 목록에서 선택하거나 새로 만드세요.", MessageType.None);
            return;
        }

        var preset = _presets[_selectedIndex];
        EditorGUILayout.LabelField("선택한 프리셋 편집", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        string newName = EditorGUILayout.TextField("이름", preset.themeName);
        Color newWall = EditorGUILayout.ColorField("벽 색상", preset.wallColor);
        Color newFloor = EditorGUILayout.ColorField("바닥 색상", preset.floorColor);
        if (EditorGUI.EndChangeCheck())
        {
            bool nameChanged = newName != preset.themeName;

            Undo.RecordObject(preset, "Edit Map Color Theme");
            preset.themeName = newName;
            preset.wallColor = newWall;
            preset.floorColor = newFloor;
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();

            if (nameChanged && !string.IsNullOrWhiteSpace(newName))
            {
                RenamePresetAsset(preset, newName);
                RefreshPresets();
                _selectedIndex = System.Array.IndexOf(_presets, preset);
            }
        }

        EditorGUILayout.Space(4);
        GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
        if (GUILayout.Button("이 프리셋 삭제", GUILayout.Height(24)))
            DeletePreset(preset);
        GUI.backgroundColor = Color.white;
    }

    // 프리셋 이름(themeName)을 바꾸면 에셋 파일 이름도 맞춘다 — AssetDatabase.RenameAsset을 거쳐야 .meta/GUID가 유지된다(파일 시스템 직접 rename 금지).
    static void RenamePresetAsset(MapColorTheme preset, string newName)
    {
        string path = AssetDatabase.GetAssetPath(preset);
        if (string.IsNullOrEmpty(path)) return;

        string sanitized = SanitizeFileName(newName);
        if (string.IsNullOrEmpty(sanitized)) return;

        string error = AssetDatabase.RenameAsset(path, sanitized);
        if (!string.IsNullOrEmpty(error))
            Debug.LogWarning($"[MapColorThemeWindow] 프리셋 파일 이름 변경 실패: {error}");
    }

    static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }

    void DeletePreset(MapColorTheme preset)
    {
        string path = AssetDatabase.GetAssetPath(preset);
        if (string.IsNullOrEmpty(path)) return;

        bool usedByFloor = _floorAssignments != null &&
            (_floorAssignments.defaultTheme == preset ||
             _floorAssignments.floorThemes.Any(e => e != null && e.theme == preset));
        string warning = usedByFloor
            ? "\n\n⚠ 이 프리셋은 현재 층 배정에 쓰이고 있습니다 — 삭제하면 그 층은 배정 없음으로 바뀝니다."
            : "";

        bool confirmed = EditorUtility.DisplayDialog("프리셋 삭제",
            $"'{preset.themeName}' 프리셋을 삭제할까요?\n(휴지통으로 이동 — 복구 가능){warning}", "삭제", "취소");
        if (!confirmed) return;

        AssetDatabase.MoveAssetToTrash(path);
        AssetDatabase.SaveAssets();

        _selectedIndex = -1;
        RefreshPresets();
        LoadFloorAssignments();
    }

    void DrawFloorAssignments()
    {
        EditorGUILayout.LabelField("층별 테마 배정", EditorStyles.boldLabel);

        if (_floorAssignments == null)
            EditorGUILayout.HelpBox("아직 배정표가 없습니다 — 아래에서 지정하면 자동으로 만들어집니다.", MessageType.None);

        MapColorTheme currentDefault = _floorAssignments != null ? _floorAssignments.defaultTheme : null;
        EditorGUI.BeginChangeCheck();
        MapColorTheme newDefault = (MapColorTheme)EditorGUILayout.ObjectField(
            "기본값 (미지정 층)", currentDefault, typeof(MapColorTheme), false);
        bool changed = EditorGUI.EndChangeCheck();

        var perFloor = new MapColorTheme[FloorLabels.Length];
        for (int i = 0; i < FloorLabels.Length; i++)
        {
            MapColorTheme current = _floorAssignments != null ? _floorAssignments.GetExplicitThemeForFloor(i) : null;
            EditorGUI.BeginChangeCheck();
            perFloor[i] = (MapColorTheme)EditorGUILayout.ObjectField(FloorLabels[i], current, typeof(MapColorTheme), false);
            changed |= EditorGUI.EndChangeCheck();
        }

        if (changed)
            SaveFloorAssignments(newDefault, perFloor);
    }

    void SaveFloorAssignments(MapColorTheme defaultTheme, MapColorTheme[] perFloor)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        if (_floorAssignments == null)
        {
            _floorAssignments = ScriptableObject.CreateInstance<MapFloorColorThemes>();
            AssetDatabase.CreateAsset(_floorAssignments, FloorAssignmentsAssetPath);
        }

        Undo.RecordObject(_floorAssignments, "Edit Map Floor Color Themes");
        _floorAssignments.defaultTheme = defaultTheme;
        _floorAssignments.floorThemes.Clear();
        for (int i = 0; i < perFloor.Length; i++)
        {
            if (perFloor[i] == null) continue;
            _floorAssignments.floorThemes.Add(new MapFloorColorThemes.FloorEntry { floorIndex = i, theme = perFloor[i] });
        }

        EditorUtility.SetDirty(_floorAssignments);
        AssetDatabase.SaveAssets();
    }
}
