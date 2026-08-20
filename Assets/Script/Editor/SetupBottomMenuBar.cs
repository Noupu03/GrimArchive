using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

// SetupStatusInfoPanel.cs/SetupWaveGaugePanel.cs와 동일한 관례 — 이 프로젝트의 [PanelAttribute] 패널은
// 프리팹 껍데기(빈 GameObject + 해당 MonoRoutine 컴포넌트)만 필요하고 실제 그리기는 OnGUI가 담당한다.
// 이 메뉴를 한 번 실행해서 Assets/Resources/Prefabs/BottomMenuBar.prefab을 만들고 Addressables에
// 등록해야 GameUIPresenter.LoadPanel<BottomMenuBar>가 정상 동작한다.
public class SetupBottomMenuBar
{
    [MenuItem("Tools/Setup BottomMenuBar")]
    public static void Setup()
    {
        string folderPath = "Assets/Resources/Prefabs";
        if (!System.IO.Directory.Exists(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }

        string prefabPath = folderPath + "/BottomMenuBar.prefab";

        if (System.IO.File.Exists(prefabPath))
        {
            AssetDatabase.DeleteAsset(prefabPath);
        }

        GameObject go = new GameObject("BottomMenuBar");
        go.AddComponent<RectTransform>();
        go.AddComponent<CanvasRenderer>();
        go.AddComponent<BottomMenuBar>();

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

        entry.address = "Prefabs/BottomMenuBar";

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        AssetDatabase.SaveAssets();

        Debug.Log("✅ [성공] BottomMenuBar 프리팹이 자동 생성되었으며, 어드레서블(Addressables)에 완벽하게 등록되었습니다!");
    }
}
