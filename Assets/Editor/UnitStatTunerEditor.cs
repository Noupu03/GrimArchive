using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(UnitStatTuner))]
public class UnitStatTunerEditor : Editor
{
    SerializedProperty _entries;
    int _selectedIndex = 0;

    void OnEnable() => _entries = serializedObject.FindProperty("entries");

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var tuner = (UnitStatTuner)target;

        // ─── 헤더 ─────────────────────────────────────────────────────────
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("유닛 스탯 에디터", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "유닛 타입별 세부 능력치 설정 | 파생 스탯(근력·내구 등)은 자동 재계산됨",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(6);

        // ─── 자동 감지 버튼 ───────────────────────────────────────────────
        if (GUILayout.Button("유닛 타입 자동 감지 (기본값으로 목록 채우기)", GUILayout.Height(28)))
        {
            Undo.RecordObject(target, "Auto-detect unit stats");
            AutoPopulate(tuner);
            EditorUtility.SetDirty(target);
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _entries.arraySize - 1));
        }

        EditorGUILayout.Space(6);

        int count = _entries.arraySize;

        // ─── 셀렉터 바 ───────────────────────────────────────────────────
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUILayout.LabelField("유닛 선택", GUILayout.Width(60));

            if (count == 0)
            {
                EditorGUILayout.LabelField("(항목 없음)", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }
            else
            {
                // 드롭다운 팝업
                string[] names = new string[count];
                for (int i = 0; i < count; i++)
                {
                    var n = _entries.GetArrayElementAtIndex(i)
                                    .FindPropertyRelative("unitTypeName").stringValue;
                    names[i] = string.IsNullOrEmpty(n) ? $"(항목 {i + 1})" : n;
                }

                _selectedIndex = Mathf.Clamp(_selectedIndex, 0, count - 1);
                _selectedIndex = EditorGUILayout.Popup(_selectedIndex, names,
                    EditorStyles.toolbarPopup, GUILayout.MinWidth(140));

                GUILayout.FlexibleSpace();
            }

            // 추가 / 삭제 버튼
            if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(26)))
            {
                Undo.RecordObject(target, "Add stat entry");
                int idx = _entries.arraySize;
                _entries.InsertArrayElementAtIndex(idx);
                _entries.GetArrayElementAtIndex(idx)
                        .FindPropertyRelative("unitTypeName").stringValue = "새 유닛 타입";
                serializedObject.ApplyModifiedProperties();
                _selectedIndex = idx;
            }

            using (new EditorGUI.DisabledScope(count == 0))
            {
                if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(26)))
                {
                    Undo.RecordObject(target, "Remove stat entry");
                    _entries.DeleteArrayElementAtIndex(_selectedIndex);
                    serializedObject.ApplyModifiedProperties();
                    _selectedIndex = Mathf.Clamp(_selectedIndex, 0,
                        Mathf.Max(0, _entries.arraySize - 1));
                }
            }
        }

        // ─── 선택된 항목 편집 패널 ────────────────────────────────────────
        if (_entries.arraySize == 0)
        {
            EditorGUILayout.HelpBox("항목이 없습니다. '+' 버튼이나 자동 감지로 추가하세요.", MessageType.Info);
        }
        else
        {
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _entries.arraySize - 1);
            var entry    = _entries.GetArrayElementAtIndex(_selectedIndex);
            var nameProp = entry.FindPropertyRelative("unitTypeName");

            EditorGUILayout.Space(4);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                nameProp.stringValue = EditorGUILayout.TextField("유닛 타입명", nameProp.stringValue);

                EditorGUILayout.Space(6);

                DrawStatGroup(entry, "기본 전투",
                    ("maxHp",           "최대 HP"),
                    ("maxMp",           "최대 MP"),
                    ("physicalAttack",  "물리 공격력"),
                    ("magicalAttack",   "마법 공격력"),
                    ("physicalDefense", "물리 방어력"),
                    ("magicalDefense",  "마법 방어력"));

                DrawStatGroup(entry, "전투 스탯",
                    ("HPRegen",           "HP 재생"),
                    ("attackspeed",       "공격 속도"),
                    ("walkSpeed",         "이동 속도"),
                    ("reaction",          "반응 속도"),
                    ("criticalChance",    "치명타율"),
                    ("cooltimeReduction", "쿨감"),
                    ("statusResistance",  "상태이상 저항"));

                DrawStatGroup(entry, "정신 / 감지",
                    ("maxMental",       "최대 정신력"),
                    ("mental",          "현재 정신력"),
                    ("spotting",        "감지"),
                    ("leadershipRange", "지휘 범위"),
                    ("charisma",        "카리스마"));

                DrawStatGroup(entry, "공격 속도",
                    ("physicalAttackSpeed", "물리 공격 속도"),
                    ("magicalCastSpeed",    "마법 시전 속도"));
            }
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(8);

        // ─── 런타임 즉시 적용 ─────────────────────────────────────────────
        if (Application.isPlaying)
        {
            if (GUILayout.Button("런타임 즉시 적용", GUILayout.Height(30)))
                tuner.Apply();
        }
        else
        {
            EditorGUILayout.HelpBox(
                "플레이 시작(Awake)에 자동 적용됩니다.\n" +
                "플레이 중에는 '런타임 즉시 적용' 버튼이 나타납니다.\n" +
                "런타임 적용 시 파생 스탯(근력·내구·민첩 등)도 자동 재계산됩니다.",
                MessageType.Info);
        }
    }

    // ─── 스탯 그룹 ────────────────────────────────────────────────────────
    void DrawStatGroup(SerializedProperty entry, string label,
        params (string prop, string display)[] fields)
    {
        var headerStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { background = MakeTexture(new Color(0.25f, 0.25f, 0.25f, 0.4f)) }
        };
        EditorGUILayout.LabelField($"  {label}", headerStyle);

        float savedLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 110f;

        const int cols = 3;
        int rows = Mathf.CeilToInt(fields.Length / (float)cols);

        for (int r = 0; r < rows; r++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int c = 0; c < cols; c++)
                {
                    int idx = r * cols + c;
                    if (idx >= fields.Length) { GUILayout.FlexibleSpace(); continue; }

                    var (propName, displayName) = fields[idx];
                    var prop = entry.FindPropertyRelative(propName);
                    if (prop != null)
                        prop.floatValue = EditorGUILayout.FloatField(
                            displayName, prop.floatValue, GUILayout.MinWidth(150));
                }
            }
        }

        EditorGUIUtility.labelWidth = savedLabelWidth;
        EditorGUILayout.Space(4);
    }

    // ─── 자동 감지 ────────────────────────────────────────────────────────
    void AutoPopulate(UnitStatTuner tuner)
    {
        var unitTypeClasses = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(UnitType)));

        bool changed = false;
        foreach (var typeClass in unitTypeClasses)
        {
            UnitType ut;
            try { ut = (UnitType)Activator.CreateInstance(typeClass); }
            catch { continue; }

            if (tuner.entries.Exists(e => e.unitTypeName == ut.typeName)) continue;

            var tempUnit = ScriptableObject.CreateInstance<Human>();
            tempUnit.unitType = ut;
            tempUnit.SetupStats();

            tuner.entries.Add(new UnitStatTuner.UnitStatEntry
            {
                unitTypeName        = ut.typeName,
                maxHp               = tempUnit.maxHp,
                maxMp               = tempUnit.maxMp,
                physicalAttack      = tempUnit.physicalAttack,
                magicalAttack       = tempUnit.magicalAttack,
                physicalDefense     = tempUnit.physicalDefense,
                magicalDefense      = tempUnit.magicalDefense,
                HPRegen             = tempUnit.HPRegen,
                attackspeed         = tempUnit.attackspeed,
                walkSpeed           = tempUnit.walkSpeed,
                reaction            = tempUnit.reaction,
                criticalChance      = tempUnit.criticalChance,
                cooltimeReduction   = tempUnit.cooltimeReduction,
                statusResistance    = tempUnit.statusResistance,
                maxMental           = tempUnit.maxMental,
                mental              = tempUnit.mental,
                spotting            = tempUnit.spotting,
                leadershipRange     = tempUnit.leadershipRange,
                charisma            = tempUnit.charisma,
                physicalAttackSpeed = tempUnit.physicalAttackSpeed,
                magicalCastSpeed    = tempUnit.magicalCastSpeed,
            });

            ScriptableObject.DestroyImmediate(tempUnit);
            changed = true;
        }

        if (!changed) Debug.Log("[UnitStatTuner] 새로 감지된 유닛 타입 없음");
        else          Debug.Log("[UnitStatTuner] 유닛 타입 목록 갱신 완료");
    }

    static Texture2D MakeTexture(Color c)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, c);
        tex.Apply();
        return tex;
    }
}
