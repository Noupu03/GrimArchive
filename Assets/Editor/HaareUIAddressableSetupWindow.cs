#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

public class HaareUIAddressableSetupWindow : EditorWindow
{
    private string _prefabName = "";

    [MenuItem("Tools/GrimArchive/UI 어드레서블 범용 연결 도구")]
    public static void ShowWindow()
    {
        var window = GetWindow<HaareUIAddressableSetupWindow>("UI 어드레서블 세팅");
        window.minSize = new Vector2(400, 150);
        window.maxSize = new Vector2(400, 150);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("하레 프레임워크 UI 범용 어드레서블 등록 도구", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "Assets/Prefabs/UI/ 폴더 내에 있는 프리팹 이름을 확장자 없이 입력하세요.\n" +
            "예: EncyclopediaPanel (입력 시 Prefabs/UI/EncyclopediaPanel 키로 자동 등록)", 
            MessageType.Info);

        GUILayout.Space(10);

        _prefabName = EditorGUILayout.TextField("프리팹 이름:", _prefabName);

        GUILayout.Space(20);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("등록 (Register)", GUILayout.Height(30)))
        {
            RegisterAddressable(_prefabName);
        }

        if (GUILayout.Button("해제 (Remove)", GUILayout.Height(30)))
        {
            RemoveAddressable(_prefabName);
        }
        GUILayout.EndHorizontal();
    }

    private void RegisterAddressable(string prefabName)
    {
        if (string.IsNullOrWhiteSpace(prefabName))
        {
            EditorUtility.DisplayDialog("경고", "프리팹 이름을 입력해주세요.", "확인");
            return;
        }

        string prefabPath = $"Assets/Prefabs/UI/{prefabName}.prefab";
        string addressableKey = $"Prefabs/UI/{prefabName}";

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("오류", $"해당 경로에 프리팹이 존재하지 않습니다.\n{prefabPath}", "확인");
            Debug.LogError($"[UI 어드레서블 등록] 프리팹 없음: {prefabPath}");
            return;
        }

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[UI 어드레서블 등록] AddressableAssetSettings를 찾을 수 없습니다.");
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
            EditorUtility.DisplayDialog("성공", $"어드레서블 등록이 완료되었습니다.\n키: {addressableKey}", "확인");
            Debug.Log($"<color=green>🎉 UI 어드레서블 등록 완료! 키: {addressableKey}</color>");
        }
        else
        {
            Debug.LogError("[UI 어드레서블 등록] 그룹 엔트리 생성에 실패했습니다.");
        }
    }

    private void RemoveAddressable(string prefabName)
    {
        if (string.IsNullOrWhiteSpace(prefabName))
        {
            EditorUtility.DisplayDialog("경고", "프리팹 이름을 입력해주세요.", "확인");
            return;
        }

        string addressableKey = $"Prefabs/UI/{prefabName}";
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

        if (settings == null)
        {
            Debug.LogError("[UI 어드레서블 해제] AddressableAssetSettings를 찾을 수 없습니다.");
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
            EditorUtility.DisplayDialog("성공", $"어드레서블 등록이 해제되었습니다.\n키: {addressableKey}", "확인");
            Debug.Log($"<color=yellow>🗑️ UI 어드레서블 등록 해제 완료! 키: {addressableKey}</color>");
        }
        else
        {
            EditorUtility.DisplayDialog("알림", $"해제할 항목을 찾을 수 없습니다.\n키: {addressableKey}", "확인");
            Debug.LogWarning($"[UI 어드레서블 해제] '{addressableKey}' 주소로 등록된 항목을 찾을 수 없습니다.");
        }
    }
}
#endif
