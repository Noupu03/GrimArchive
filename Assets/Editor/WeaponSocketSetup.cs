using UnityEditor;
using UnityEngine;

// 유닛 프리팹의 Visual 자식 밑에 무기 소켓(SpriteRenderer + WeaponAttachment)을 추가하는 범용 도구.
// 프리팹/스프라이트를 매번 다르게 지정할 수 있어 어떤 유닛 프리팹에도 재사용 가능하다.
// 사용법: Tools > GrimArchive > 무기 소켓 추가
public class WeaponSocketSetup : EditorWindow
{
    private GameObject unitPrefab;
    private Sprite weaponSprite;
    private string socketName = "WeaponSocket";

    [MenuItem("Tools/GrimArchive/무기 소켓 추가")]
    public static void ShowWindow()
    {
        GetWindow<WeaponSocketSetup>("무기 소켓 추가");
    }

    private void OnGUI()
    {
        GUILayout.Label("유닛 프리팹에 무기 소켓 추가", EditorStyles.boldLabel);
        GUILayout.Space(10);

        unitPrefab   = (GameObject)EditorGUILayout.ObjectField("대상 유닛 프리팹", unitPrefab, typeof(GameObject), false);
        weaponSprite = (Sprite)EditorGUILayout.ObjectField("무기 스프라이트", weaponSprite, typeof(Sprite), false);
        socketName   = EditorGUILayout.TextField("소켓 이름", socketName);

        GUILayout.Space(10);

        using (new EditorGUI.DisabledScope(unitPrefab == null || weaponSprite == null || string.IsNullOrEmpty(socketName)))
        {
            if (GUILayout.Button("소켓 추가", GUILayout.Height(30)))
                AddWeaponSocket();
        }

        EditorGUILayout.HelpBox(
            "대상 프리팹의 'Visual' 자식 밑에 소켓 GameObject를 만들고 SpriteRenderer + WeaponAttachment를 붙인 뒤\n" +
            "지정한 스프라이트를 배정합니다. WeaponAttachment의 방향별 위치/회전/정렬순서는 이후 인스펙터에서\n" +
            "직접 튜닝해야 합니다 (기본값은 대략치).",
            MessageType.Info);
    }

    private void AddWeaponSocket()
    {
        string path = AssetDatabase.GetAssetPath(unitPrefab);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("[WeaponSocketSetup] 유효한 프리팹 에셋이 아닙니다.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform visual = root.transform.Find("Visual");
            if (visual == null)
            {
                Debug.LogError($"[WeaponSocketSetup] '{root.name}' 프리팹에서 'Visual' 자식을 찾을 수 없습니다.");
                return;
            }

            Transform existing = visual.Find(socketName);
            GameObject socketGo = existing != null ? existing.gameObject : new GameObject(socketName);
            if (existing == null)
            {
                socketGo.transform.SetParent(visual, false);
                socketGo.transform.localPosition = Vector3.zero;
            }

            SpriteRenderer sr = socketGo.GetComponent<SpriteRenderer>();
            if (sr == null) sr = socketGo.AddComponent<SpriteRenderer>();
            sr.sprite = weaponSprite;

            if (socketGo.GetComponent<WeaponAttachment>() == null)
                socketGo.AddComponent<WeaponAttachment>();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[WeaponSocketSetup] '{path}'에 무기 소켓 '{socketName}' 추가/갱신 완료. WeaponAttachment의 poses를 인스펙터에서 튜닝하세요.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
