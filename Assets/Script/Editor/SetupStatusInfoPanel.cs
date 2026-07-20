using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

public class SetupStatusInfoPanel
{
    [MenuItem("Tools/Setup StatusInfoPanel")]
    public static void Setup()
    {
        // 1. 프리팹 폴더 확인 및 생성
        string folderPath = "Assets/Resources/Prefabs";
        if (!System.IO.Directory.Exists(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }
        
        string prefabPath = folderPath + "/StatusInfoPanel.prefab";

        // 이미 프리팹이 있다면 삭제 (덮어쓰기 위해)
        if (System.IO.File.Exists(prefabPath))
        {
            AssetDatabase.DeleteAsset(prefabPath);
        }

        // 2. 임시 게임 오브젝트 생성 및 UI 컴포넌트 세팅
        GameObject go = new GameObject("StatusInfoPanel");
        go.AddComponent<RectTransform>();
        go.AddComponent<CanvasRenderer>();
        go.AddComponent<StatusInfoPanel>();
        
        // 3. 프리팹으로 저장
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        
        // 4. 어드레서블(Addressables) 설정에 등록
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[Setup] AddressableAssetSettings를 찾을 수 없습니다! 어드레서블 창을 한 번 열어서 설정 파일을 생성해주세요.");
            return;
        }
        
        AddressableAssetGroup group = settings.DefaultGroup;
        string guid = AssetDatabase.AssetPathToGUID(prefabPath);
        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, true);
        
        // 어드레스(Key)를 코드로 덮어씌움
        entry.address = "Prefabs/StatusInfoPanel";
        
        // 설정 저장
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        AssetDatabase.SaveAssets();
        
        Debug.Log("✅ [성공] StatusInfoPanel 프리팹이 자동 생성되었으며, 어드레서블(Addressables)에 완벽하게 등록되었습니다!");
    }
}
