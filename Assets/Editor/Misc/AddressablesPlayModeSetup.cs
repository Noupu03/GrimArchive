#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// 에디터 시작 시 Addressables Play Mode를 "Use Asset Database (fastest)"로 자동 설정합니다.
/// Addressables 빌드 없이 Play 버튼만으로 게임을 실행할 수 있습니다.
/// </summary>
[InitializeOnLoad]
public static class AddressablesPlayModeSetup
{
    static AddressablesPlayModeSetup()
    {
        EditorApplication.delayCall += SetPlayModeToAssetDatabase;
    }

    [MenuItem("Tools/Haare/Addressables: Use Asset Database (Dev Mode)")]
    public static void SetPlayModeToAssetDatabase()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogWarning("[AddressablesPlayModeSetup] AddressableAssetSettings not found.");
            return;
        }

        // 0 = Use Asset Database (fastest) — 빌드 불필요
        // 1 = Simulate Groups (Advanced)
        // 2 = Use Existing Build (requires build)
        int targetIndex = 0;

        if (settings.ActivePlayModeDataBuilderIndex != targetIndex)
        {
            settings.ActivePlayModeDataBuilderIndex = targetIndex;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[AddressablesPlayModeSetup] Play Mode set to: Use Asset Database (fastest). Addressables build is NOT required.");
        }
    }
}
#endif
