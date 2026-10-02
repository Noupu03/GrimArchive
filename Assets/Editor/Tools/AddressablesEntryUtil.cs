#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

// 에디터 툴이 만든 에셋을 Addressables 기본 그룹에 주소(+라벨)로 등록한다(2026-10-02 Resources → Addressables
// 전환). 게임은 Resources가 아니라 Addressables 주소(AssetKeys)로만 에셋을 읽으므로, 런타임이 읽는 에셋을
// 새로 만들거나 다시 만드는 툴은 저장 직후 이걸 부른다 — 파일을 지우고 다시 만들면 GUID가 바뀌어 기존
// 등록이 끊기기 때문이다. 이미 같은 주소·라벨로 등록돼 있으면 아무것도 바꾸지 않는다.
public static class AddressablesEntryUtil
{
    public static void EnsureEntry(string assetPath, string address, string label = null)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogWarning($"[AddressablesEntryUtil] AddressableAssetSettings가 없어 '{assetPath}'를 등록하지 못했습니다.");
            return;
        }

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogWarning($"[AddressablesEntryUtil] '{assetPath}' 에셋을 찾을 수 없습니다(저장 후 AssetDatabase.Refresh가 필요할 수 있음).");
            return;
        }

        bool changed = false;
        AddressableAssetEntry entry = settings.FindAssetEntry(guid);
        if (entry == null)
        {
            entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup, readOnly: false, postEvent: false);
            changed = true;
        }
        if (entry.address != address)
        {
            entry.SetAddress(address, postEvent: false);
            changed = true;
        }
        if (!string.IsNullOrEmpty(label) && !entry.labels.Contains(label))
        {
            settings.AddLabel(label, postEvent: false);
            entry.SetLabel(label, true, force: true, postEvent: false);
            changed = true;
        }

        if (!changed) return;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, postEvent: true, settingsModified: true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AddressablesEntryUtil] '{assetPath}' → 주소 '{address}'{(string.IsNullOrEmpty(label) ? "" : $", 라벨 '{label}'")} 등록.");
    }
}
#endif
