using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Play 모드 종료 후에도 GameSession이 만든 RoomPopLabel_* 오브젝트가 씬에 남는 경우가 있어(정확한
// 원인 불명 — Reload Scene 설정과 무관하게 재현됨) 에디터 쪽에서 확실하게 청소한다. 콜백 안에서 바로
// DestroyImmediate 하면 씬이 완전히 안정되기 전이라 조용히 무시되므로, EditorApplication.delayCall로
// 한 틱 미룬다.
[InitializeOnLoad]
internal static class RoomPopulationLabelCleanup
{
    private const string RootObjectName = "RoomPopulationLabels";
    private const string LabelObjectPrefix = "RoomPopLabel_";

    static RoomPopulationLabelCleanup()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.delayCall += CleanupNow;
    }

    private static void CleanupNow()
    {
        var toDestroy = new List<GameObject>();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                CollectMatches(root.transform, toDestroy);
            }
        }

        foreach (GameObject go in toDestroy)
        {
            if (go != null) Object.DestroyImmediate(go);
        }
    }

    // 매칭된 오브젝트 자신만 수집한다 — 자식들은 어차피 함께 파괴되므로 더 내려가지 않는다.
    private static void CollectMatches(Transform t, List<GameObject> result)
    {
        if (t.name == RootObjectName || t.name.StartsWith(LabelObjectPrefix))
        {
            result.Add(t.gameObject);
            return;
        }

        for (int i = 0; i < t.childCount; i++)
        {
            CollectMatches(t.GetChild(i), result);
        }
    }
}
