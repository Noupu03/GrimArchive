#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 메뉴: GrimArchive → AI → FSM+BT 설정 에셋 생성
/// Assets/Resources/FSM+BT/ 폴더에 세 가지 설정 에셋을 한 번에 생성한다.
/// 이미 존재하는 에셋은 덮어쓰지 않는다.
/// </summary>
public static class AIConfigAutoSetup
{
    private const string Dir = "Assets/Resources/FSM+BT";

    [MenuItem("GrimArchive/AI/FSM+BT 설정 에셋 생성")]
    public static void CreateAllConfigs()
    {
        if (!Directory.Exists(Dir))
        {
            Directory.CreateDirectory(Dir);
            AssetDatabase.Refresh();
        }

        bool created = false;
        created |= CreateIfMissing<AIBehaviorConfig>("AIBehaviorConfig");
        created |= CreateIfMissing<TacticalBehaviorPriorityConfig>("TacticalPriority");
        created |= CreateIfMissing<NavigationBehaviorPriorityConfig>("NavigationPriority");

        if (created)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[GrimArchive] FSM+BT 설정 에셋 {(created ? "생성 완료" : "이미 존재 — 건너뜀")}. 경로: {Dir}");
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>($"{Dir}/AIBehaviorConfig.asset");
    }

    private static bool CreateIfMissing<T>(string fileName) where T : ScriptableObject
    {
        string path = $"{Dir}/{fileName}.asset";
        if (File.Exists(path)) return false;
        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        Debug.Log($"[GrimArchive] 생성: {path}");
        return true;
    }
}
#endif
