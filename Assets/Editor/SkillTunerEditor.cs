using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SkillTuner))]
public class SkillTunerEditor : Editor
{
    SerializedProperty _entries;
    readonly Dictionary<string, bool> _foldouts = new();

    // skillName → 유닛 타입 표시명 (리플렉션 캐시)
    Dictionary<string, string> _skillUnitTypeMap;

    static readonly GUILayoutOption W_Name     = GUILayout.Width(160);
    static readonly GUILayoutOption W_Delay    = GUILayout.Width(110);
    static readonly GUILayoutOption W_Cooldown = GUILayout.Width(100);
    static readonly GUILayoutOption W_Del      = GUILayout.Width(26);

    void OnEnable()
    {
        _entries          = serializedObject.FindProperty("entries");
        _skillUnitTypeMap = BuildSkillUnitTypeMap();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var tuner = (SkillTuner)target;

        // ─── 헤더 ─────────────────────────────────────────────────────────
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("스킬 밸런스 에디터", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "선딜레이: 공격속도 보정 전 기본값 (ms)  |  쿨타임: 쿨감 적용 전 기본값 (초)",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(6);

        // ─── 자동 감지 버튼 ───────────────────────────────────────────────
        if (GUILayout.Button("스킬 자동 감지 (리플렉션으로 목록 채우기)", GUILayout.Height(28)))
        {
            Undo.RecordObject(target, "Auto-detect skills");
            _skillUnitTypeMap = BuildSkillUnitTypeMap();
            AutoPopulate(tuner);
            EditorUtility.SetDirty(target);
        }

        EditorGUILayout.Space(4);

        // ─── 유닛 타입별 그룹 구성 ────────────────────────────────────────
        var groupOrder = new List<string>();
        var groupMap   = new Dictionary<string, List<int>>();

        for (int i = 0; i < _entries.arraySize; i++)
        {
            var    prop  = _entries.GetArrayElementAtIndex(i);
            string sName = prop.FindPropertyRelative("skillName").stringValue;
            string group = _skillUnitTypeMap.TryGetValue(sName, out var g) ? g : "미분류";

            if (!groupMap.ContainsKey(group))
            {
                groupMap[group] = new List<int>();
                groupOrder.Add(group);
            }
            groupMap[group].Add(i);
        }

        int deleteIndex = -1;

        foreach (string groupName in groupOrder)
        {
            var indices = groupMap[groupName];
            if (!_foldouts.ContainsKey(groupName)) _foldouts[groupName] = true;

            EditorGUILayout.Space(3);

            // ─── 그룹 헤더 ────────────────────────────────────────────────
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var headerStyle = new GUIStyle(EditorStyles.foldoutHeader)
                    { fontSize = 13, fontStyle = FontStyle.Bold };

                _foldouts[groupName] = EditorGUILayout.Foldout(
                    _foldouts[groupName],
                    $"  {groupName}  ({indices.Count}개)",
                    true,
                    headerStyle);

                if (!_foldouts[groupName]) continue;

                EditorGUILayout.Space(2);

                // 컬럼 헤더
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    var center = new GUIStyle(EditorStyles.boldLabel)
                        { alignment = TextAnchor.MiddleCenter };
                    GUILayout.Label("스킬명",        center, W_Name);
                    GUILayout.Label("선딜레이 (ms)", center, W_Delay);
                    GUILayout.Label("쿨타임 (초)",   center, W_Cooldown);
                    GUILayout.Label("",             W_Del);
                }

                // 스킬 행
                for (int j = 0; j < indices.Count; j++)
                {
                    int i         = indices[j];
                    var prop      = _entries.GetArrayElementAtIndex(i);
                    var nameProp  = prop.FindPropertyRelative("skillName");
                    var delayProp = prop.FindPropertyRelative("baseDelayMs");
                    var cdProp    = prop.FindPropertyRelative("baseCooldown");

                    var rowBg = j % 2 == 0
                        ? new GUIStyle { normal = { background = MakeTexture(new Color(0.22f, 0.22f, 0.22f, 0.3f)) } }
                        : GUIStyle.none;

                    using (new EditorGUILayout.HorizontalScope(rowBg))
                    {
                        nameProp.stringValue  = EditorGUILayout.TextField(nameProp.stringValue, W_Name);
                        delayProp.floatValue  = Mathf.Max(50f,  EditorGUILayout.FloatField(delayProp.floatValue, W_Delay));
                        cdProp.floatValue     = Mathf.Max(0.1f, EditorGUILayout.FloatField(cdProp.floatValue,   W_Cooldown));

                        if (GUILayout.Button("X", W_Del)) deleteIndex = i;
                    }
                }
            }
        }

        if (deleteIndex >= 0)
            _entries.DeleteArrayElementAtIndex(deleteIndex);

        EditorGUILayout.Space(4);

        // ─── 추가 버튼 ───────────────────────────────────────────────────
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ 항목 추가"))
            {
                int idx = _entries.arraySize;
                _entries.InsertArrayElementAtIndex(idx);
                var e = _entries.GetArrayElementAtIndex(idx);
                e.FindPropertyRelative("skillName").stringValue   = "새 스킬";
                e.FindPropertyRelative("baseDelayMs").floatValue  = 500f;
                e.FindPropertyRelative("baseCooldown").floatValue = 3f;
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
                "플레이 시작(Awake)에 자동으로 적용됩니다.\n" +
                "플레이 중에는 '런타임 즉시 적용' 버튼이 나타납니다.",
                MessageType.Info);
        }
    }

    // ─── skillName → 유닛 타입 표시명 맵 빌드 ───────────────────────────
    // SkillAction_KnightFocusedStab → strip "SkillAction_" → "KnightFocusedStab"
    // → UnitType 클래스명 "Knight" 매칭 → typeName "기사형"
    static Dictionary<string, string> BuildSkillUnitTypeMap()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        // UnitType 서브클래스를 (C#클래스명, 표시명) 목록으로 수집
        // 이름 길이 내림차순: "MeleeTank" 가 "Melee" 보다 먼저 매칭되게
        var unitTypes = assemblies
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(UnitType)))
            .OrderByDescending(t => t.Name.Length)
            .Select(t =>
            {
                try { return (t.Name, ((UnitType)Activator.CreateInstance(t)).typeName); }
                catch { return (t.Name, t.Name); }
            })
            .ToList();

        var map = new Dictionary<string, string>();

        var skillTypes = assemblies
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(SkillAction)));

        foreach (var type in skillTypes)
        {
            SkillAction skill;
            try { skill = (SkillAction)Activator.CreateInstance(type); }
            catch { continue; }

            string unitDisplay = ResolveUnitType(type.Name, unitTypes);
            map[skill.SkillName] = unitDisplay;
        }

        return map;
    }

    static string ResolveUnitType(string className, List<(string cn, string dn)> unitTypes)
    {
        const string prefix = "SkillAction_";
        if (!className.StartsWith(prefix)) return "미분류";
        string rest = className.Substring(prefix.Length);

        foreach (var (cn, dn) in unitTypes)
            if (rest.StartsWith(cn)) return dn;

        return "미분류";
    }

    // ─── 리플렉션으로 모든 SkillAction 서브클래스 탐색 ──────────────────
    static void AutoPopulate(SkillTuner tuner)
    {
        var skillTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(SkillAction)));

        bool changed = false;
        foreach (var type in skillTypes)
        {
            SkillAction skill;
            try { skill = (SkillAction)Activator.CreateInstance(type); }
            catch { continue; }

            if (tuner.entries.Exists(e => e.skillName == skill.SkillName)) continue;

            tuner.entries.Add(new SkillTuner.SkillTuningEntry
            {
                skillName    = skill.SkillName,
                baseDelayMs  = skill.DefaultBaseDelayMs,
                baseCooldown = skill.DefaultBaseCooldown
            });
            changed = true;
        }

        if (!changed)
            Debug.Log("[SkillTuner] 새로 감지된 스킬 없음 (이미 모두 등록됨)");
        else
            Debug.Log("[SkillTuner] 스킬 목록 갱신 완료");
    }

    static Texture2D MakeTexture(Color c)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, c);
        tex.Apply();
        return tex;
    }
}
