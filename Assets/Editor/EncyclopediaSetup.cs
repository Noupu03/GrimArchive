#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

public class EncyclopediaSetup
{
    [MenuItem("Tools/GrimArchive/어드레서블 자동 연결 (Addressables Fix)")]
    public static void FixAddressables()
    {
        string prefabPath = "Assets/Prefabs/UI/EncyclopediaPanel.prefab";
        string addressableKey = "Prefabs/UI/EncyclopediaPanel";

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[어드레서블 자동 연결] 실패: {prefabPath} 경로에 프리팹이 없습니다. 이름을 EncyclopediaPanel 로 바꿨는지 확인해주세요.");
            return;
        }

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[어드레서블 자동 연결] AddressableAssetSettings를 찾을 수 없습니다.");
            return;
        }

        AddressableAssetGroup group = settings.DefaultGroup;
        string guid = AssetDatabase.AssetPathToGUID(prefabPath);
        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
        
        if (entry != null)
        {
            entry.address = addressableKey;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>🎉 어드레서블 등록 완료! 키: {addressableKey}</color>\n이제 게임을 실행하고 탭(Tab) 키를 눌러보세요!");
        }
        else
        {
            Debug.LogError("[어드레서블 자동 연결] 그룹 엔트리 생성에 실패했습니다.");
        }
    }

    [MenuItem("Tools/GrimArchive/어드레서블 연결 해제 (Addressables Remove)")]
    public static void RemoveAddressables()
    {
        string addressableKey = "Prefabs/UI/EncyclopediaPanel";
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

        if (settings == null)
        {
            Debug.LogError("[어드레서블 연결 해제] AddressableAssetSettings를 찾을 수 없습니다.");
            return;
        }

        bool found = false;
        foreach (var group in settings.groups)
        {
            if (group == null) continue;
            
            var entries = new System.Collections.Generic.List<AddressableAssetEntry>(group.entries);
            foreach (var entry in entries)
            {
                if (entry.address == addressableKey)
                {
                    group.RemoveAssetEntry(entry);
                    found = true;
                }
            }
        }

        if (found)
        {
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, null, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=yellow>🗑️ 어드레서블 등록 해제 완료! 키: {addressableKey}</color>");
        }
        else
        {
            Debug.LogWarning($"[어드레서블 연결 해제] '{addressableKey}' 주소로 등록된 항목을 찾을 수 없어 해제할 내용이 없습니다.");
        }
    }
}
#endif
