using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

// Haare 원본 저장소의 Demo 프리팹/씬을 Addressables에 등록하는 1회성 에디터 도구.
// Assets/Editor/HaareUISetup.cs와 같은 스타일 — 몇 번을 다시 실행해도 안전(멱등).
public static class HaareDemoSetup
{
    private const string PrefabFolder = "Assets/Haare/Demo/Asset/Resources_moved/Prefabs";
    private const string SceneFolder = "Assets/Haare/Demo/Scene";

    // Haare.Util.Prefab.PrefabPath / PanelAttribute가 기대하는 주소와 정확히 일치해야 한다.
    private static readonly (string fileName, string address)[] Prefabs =
    {
        ("CoreCanvas", "Prefabs/CoreCanvas"),
        ("HaareDebugPannel", "Prefabs/HaareDebugPannel"),
        ("Demo_TitlePanel", "Prefabs/Demo_TitlePanel"),
        ("Demo_LoadingPanel", "Prefabs/Demo_LoadingPanel"),
        ("Demo_LoadingFadePanel", "Prefabs/Demo_LoadingFadePanel"),
        ("Demo_NoticePanel", "Prefabs/Demo_NoticePanel"),
        ("Demo_PanelBackground", "Prefabs/Demo_PanelBackground"),
    };

    // Haare.Client.Routine.Service.SceneService.SceneName enum 값과 이름이 같아야 한다.
    private static readonly string[] Scenes =
    {
        "DemoTitleScene",
        "DemoLoadScene",
        "DemoLobbyScene",
    };

    [MenuItem("Tools/GrimArchive/Haare 데모 Addressable 등록")]
    public static void SetupHaareDemoAddressables()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[HaareDemoSetup] AddressableAssetSettings를 찾을 수 없습니다.");
            return;
        }

        var group = settings.FindGroup("Default Local Group") ?? settings.DefaultGroup;

        foreach (var (fileName, address) in Prefabs)
        {
            RegisterEntry($"{PrefabFolder}/{fileName}.prefab", address, settings, group);
        }

        // SceneService가 Assets/Haare/Demo/Scene/<이름>.unity 경로 자체를 주소로 사용하므로
        // 기본 주소(에셋 경로)를 그대로 유지한다.
        foreach (var sceneName in Scenes)
        {
            string scenePath = $"{SceneFolder}/{sceneName}.unity";
            RegisterEntry(scenePath, scenePath, settings, group);
        }

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        Debug.Log("[HaareDemoSetup] 완료: 데모 프리팹 7개 + 씬 3개 Addressable 등록.");
    }

    private static void RegisterEntry(string assetPath, string address, AddressableAssetSettings settings, AddressableAssetGroup group)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogError($"[HaareDemoSetup] 에셋을 찾을 수 없습니다: {assetPath}");
            return;
        }

        var entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = address;
    }
}
