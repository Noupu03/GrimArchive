using UnityEditor;
using UnityEngine;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

// SetupStatusInfoPanel/SetupBottomMenuBar/SetupNoticeCenter/SetupUIManager/SetupWaveGaugePanel이
// 전부 토씨 하나 안 틀리고 반복하던 "빈 프리팹 생성 + Addressable 등록" 로직을 한 곳으로 모았다
// (2026-08-20, 사용자 요청 "tools의 셋업들 좀 종류별로 통일시켜봐"). 이 프로젝트의 [PanelAttribute]
// 패널은 프리팹 껍데기(빈 GameObject + 해당 MonoRoutine 컴포넌트)만 필요하고 실제 그리기는 OnGUI가
// 담당한다는 확립된 관례를 그대로 반영한다 — 새 Haare 패널이 생기면 Setup*.cs 파일 하나에 이 헬퍼를
// 호출하는 [MenuItem] 한 줄만 추가하면 된다. HaareUISetup.cs("Tools/GrimArchive/Haare UI 셋업 생성")는
// uGUI 계층(텍스트/이미지 등)이 실제로 필요한 별개 종류의 패널들을 한 번에 배선하는 다른 흐름이라
// 이 헬퍼 대상이 아니다.
public static class EmptyHaarePanelSetup
{
    private const string FolderPath = "Assets/Resources/Prefabs";

    public static void CreateAndRegister<T>(string panelName) where T : Component
    {
        if (!System.IO.Directory.Exists(FolderPath))
        {
            System.IO.Directory.CreateDirectory(FolderPath);
            AssetDatabase.Refresh();
        }

        string prefabPath = $"{FolderPath}/{panelName}.prefab";

        if (System.IO.File.Exists(prefabPath))
            AssetDatabase.DeleteAsset(prefabPath);

        GameObject go = new GameObject(panelName);
        go.AddComponent<RectTransform>();
        go.AddComponent<CanvasRenderer>();
        go.AddComponent<T>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[Setup] AddressableAssetSettings를 찾을 수 없습니다! 어드레서블 창을 한 번 열어서 설정 파일을 생성해주세요.");
            return;
        }

        AddressableAssetGroup group = settings.DefaultGroup;
        string guid = AssetDatabase.AssetPathToGUID(prefabPath);
        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, true);

        entry.address = $"Prefabs/{panelName}";

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        AssetDatabase.SaveAssets();

        Debug.Log($"✅ [성공] {panelName} 프리팹이 자동 생성되었으며, 어드레서블(Addressables)에 완벽하게 등록되었습니다!");
    }
}
