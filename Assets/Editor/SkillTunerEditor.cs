using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SkillTuner))]
public class SkillTunerEditor : Editor
{
    SerializedProperty _entries;

    static readonly GUILayoutOption W_Name    = GUILayout.Width(160);
    static readonly GUILayoutOption W_Delay   = GUILayout.Width(110);
    static readonly GUILayoutOption W_Cooldown = GUILayout.Width(100);
    static readonly GUILayoutOption W_Del     = GUILayout.Width(26);

    void OnEnable() => _entries = serializedObject.FindProperty("entries");

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
            AutoPopulate(tuner);
            EditorUtility.SetDirty(target);
        }

        EditorGUILayout.Space(4);

        // ─── 컬럼 헤더 ───────────────────────────────────────────────────
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            var center = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
            GUILayout.Label("스킬명",       center, W_Name);
            GUILayout.Label("선딜레이 (ms)", center, W_Delay);
            GUILayout.Label("쿨타임 (초)",  center, W_Cooldown);
            GUILayout.Label("",            W_Del);
        }

        // ─── 스킬 목록 ───────────────────────────────────────────────────
        int deleteIndex = -1;
        for (int i = 0; i < _entries.arraySize; i++)
        {
            var prop      = _entries.GetArrayElementAtIndex(i);
            var nameProp  = prop.FindPropertyRelative("skillName");
            var delayProp = prop.FindPropertyRelative("baseDelayMs");
            var cdProp    = prop.FindPropertyRelative("baseCooldown");

            var rowStyle = i % 2 == 0
                ? new GUIStyle { normal = { background = MakeTexture(new Color(0.22f, 0.22f, 0.22f, 0.3f)) } }
                : GUIStyle.none;

            using (new EditorGUILayout.HorizontalScope(rowStyle))
            {
                nameProp.stringValue   = EditorGUILayout.TextField(nameProp.stringValue, W_Name);
                delayProp.floatValue   = Mathf.Max(50f,  EditorGUILayout.FloatField(delayProp.floatValue, W_Delay));
                cdProp.floatValue      = Mathf.Max(0.1f, EditorGUILayout.FloatField(cdProp.floatValue,    W_Cooldown));

                if (GUILayout.Button("X", W_Del)) deleteIndex = i;
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
                var newElem = _entries.GetArrayElementAtIndex(idx);
                newElem.FindPropertyRelative("skillName").stringValue  = "새 스킬";
                newElem.FindPropertyRelative("baseDelayMs").floatValue  = 500f;
                newElem.FindPropertyRelative("baseCooldown").floatValue = 3f;
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

    // ─── 리플렉션으로 모든 SkillAction 서브클래스 탐색 ──────────────────
    void AutoPopulate(SkillTuner tuner)
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
