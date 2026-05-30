using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(EngageDistanceTuner))]
public class EngageDistanceTunerEditor : Editor
{
    SerializedProperty _entries;

    static readonly GUILayoutOption W_Name     = GUILayout.Width(160);
    static readonly GUILayoutOption W_Distance = GUILayout.Width(140);
    static readonly GUILayoutOption W_Del      = GUILayout.Width(26);

    void OnEnable() => _entries = serializedObject.FindProperty("entries");

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var tuner = (EngageDistanceTuner)target;

        // ─── 헤더 ─────────────────────────────────────────────────────────
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("대치 거리 에디터", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "유닛 타입별 대치 거리 설정 (타일 단위, 1 = 1칸)",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(6);

        // ─── 자동 감지 버튼 ───────────────────────────────────────────────
        if (GUILayout.Button("유닛 타입 자동 감지 (리플렉션으로 목록 채우기)", GUILayout.Height(28)))
        {
            Undo.RecordObject(target, "Auto-detect unit types");
            AutoPopulate(tuner);
            EditorUtility.SetDirty(target);
        }

        EditorGUILayout.Space(4);

        // ─── 컬럼 헤더 ───────────────────────────────────────────────────
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            var center = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
            GUILayout.Label("유닛 타입명", center, W_Name);
            GUILayout.Label("대치 거리 (타일)", center, W_Distance);
            GUILayout.Label("", W_Del);
        }

        // ─── 유닛 타입 목록 ───────────────────────────────────────────────
        int deleteIndex = -1;
        for (int i = 0; i < _entries.arraySize; i++)
        {
            var prop     = _entries.GetArrayElementAtIndex(i);
            var nameProp = prop.FindPropertyRelative("unitTypeName");
            var distProp = prop.FindPropertyRelative("engageDistance");

            var rowStyle = i % 2 == 0
                ? new GUIStyle { normal = { background = MakeTexture(new Color(0.22f, 0.22f, 0.22f, 0.3f)) } }
                : GUIStyle.none;

            using (new EditorGUILayout.HorizontalScope(rowStyle))
            {
                nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue, W_Name);
                distProp.intValue    = Mathf.Max(1, EditorGUILayout.IntField(distProp.intValue, W_Distance));
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
                newElem.FindPropertyRelative("unitTypeName").stringValue = "새 유닛 타입";
                newElem.FindPropertyRelative("engageDistance").intValue  = 2;
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

    // ─── 리플렉션으로 모든 UnitType 서브클래스 탐색 ─────────────────────
    void AutoPopulate(EngageDistanceTuner tuner)
    {
        var unitTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(UnitType)));

        bool changed = false;
        foreach (var type in unitTypes)
        {
            UnitType ut;
            try { ut = (UnitType)Activator.CreateInstance(type); }
            catch { continue; }

            if (tuner.entries.Exists(e => e.unitTypeName == ut.typeName)) continue;

            int defaultDist = type == typeof(MeleeTank) ? 3 : 2;

            tuner.entries.Add(new EngageDistanceTuner.EngageDistanceEntry
            {
                unitTypeName   = ut.typeName,
                engageDistance = defaultDist
            });
            changed = true;
        }

        if (!changed)
            Debug.Log("[EngageDistanceTuner] 새로 감지된 유닛 타입 없음 (이미 모두 등록됨)");
        else
            Debug.Log("[EngageDistanceTuner] 유닛 타입 목록 갱신 완료");
    }

    static Texture2D MakeTexture(Color c)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, c);
        tex.Apply();
        return tex;
    }
}
