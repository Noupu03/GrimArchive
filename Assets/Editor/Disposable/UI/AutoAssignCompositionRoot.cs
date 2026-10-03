#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Haare.Client.UI;
using Haare.Client.Core.DI;

[InitializeOnLoad]
public class AutoAssignCompositionRoot
{
    static AutoAssignCompositionRoot()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            FixMissingPrefab();
        }
    }

    [MenuItem("Disposable/UI/Fix GameCompositionRoot Prefab")]
    public static void FixMissingPrefab()
    {
        GameCompositionRoot root = Object.FindFirstObjectByType<GameCompositionRoot>();
        if (root != null)
        {
            SerializedObject so = new SerializedObject(root);
            SerializedProperty prop = so.FindProperty("_coreUIManagerPrefab");

            if (prop != null && prop.objectReferenceValue == null)
            {
                string[] guids = AssetDatabase.FindAssets("CoreCanvas t:Prefab");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    CoreUIManager prefab = AssetDatabase.LoadAssetAtPath<CoreUIManager>(path);
                    if (prefab != null)
                    {
                        prop.objectReferenceValue = prefab;
                        so.ApplyModifiedProperties();
                        Debug.Log("<b><color=green>[AUTO-FIX]</color></b> GameCompositionRoot의 _coreUIManagerPrefab 빈칸에 CoreCanvas 프리팹을 자동 할당했습니다!");
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                    }
                }
            }
        }
    }
}
#endif
