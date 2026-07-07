using UnityEngine;
using UnityEditor;

public class ProjectileSetupTool : EditorWindow
{
    private GameObject selectedPrefab;

    [MenuItem("Tools/GrimArchive/투사체 프리팹 자동 세팅기")]
    public static void ShowWindow()
    {
        GetWindow<ProjectileSetupTool>("투사체 세팅기");
    }

    private void OnGUI()
    {
        GUILayout.Label("투사체 프리팹 자동 세팅", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("투사체로 사용할 프리팹을 등록하면 물리 컴포넌트를 제거하고 필요한 스크립트와 스프라이트 설정을 자동화합니다.", MessageType.Info);

        selectedPrefab = (GameObject)EditorGUILayout.ObjectField("투사체 프리팹", selectedPrefab, typeof(GameObject), false);

        if (GUILayout.Button("세팅 적용하기"))
        {
            if (selectedPrefab != null)
            {
                SetupProjectilePrefab(selectedPrefab);
            }
            else
            {
                EditorUtility.DisplayDialog("경고", "프리팹을 먼저 선택해주세요.", "확인");
            }
        }
    }

    private void SetupProjectilePrefab(GameObject prefab)
    {
        string assetPath = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(assetPath))
        {
            EditorUtility.DisplayDialog("오류", "프로젝트(Assets) 내의 프리팹을 선택해주세요.", "확인");
            return;
        }

        // 프리팹 에셋 로드
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

        bool modified = false;

        // 1. 물리 엔진 컴포넌트 제거 (Rigidbody2D)
        var rb = instance.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            DestroyImmediate(rb, true);
            modified = true;
            Debug.Log("[투사체 세팅] Rigidbody2D 제거됨.");
        }

        // 2. 물리 엔진 컴포넌트 제거 (Collider2D)
        var colliders = instance.GetComponents<Collider2D>();
        foreach (var col in colliders)
        {
            DestroyImmediate(col, true);
            modified = true;
            Debug.Log($"[투사체 세팅] {col.GetType().Name} 제거됨.");
        }

        // 3. Projectile 스크립트 부착
        var proj = instance.GetComponent<Projectile>();
        if (proj == null)
        {
            instance.AddComponent<Projectile>();
            modified = true;
            Debug.Log("[투사체 세팅] Projectile 스크립트 부착됨.");
        }

        // 4. SpriteRenderer 정리
        var sr = instance.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = instance.AddComponent<SpriteRenderer>();
            modified = true;
            Debug.Log("[투사체 세팅] SpriteRenderer 부착됨.");
        }
        
        if (sr.sortingOrder < 15)
        {
            sr.sortingOrder = 15;
            modified = true;
            Debug.Log("[투사체 세팅] SpriteRenderer SortingOrder 15로 변경됨.");
        }

        if (modified)
        {
            PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
            EditorUtility.DisplayDialog("성공", $"{prefab.name} 투사체 프리팹 세팅이 완료되었습니다.", "확인");
        }
        else
        {
            EditorUtility.DisplayDialog("알림", "이미 완벽하게 세팅되어 있어 변경할 내용이 없습니다.", "확인");
        }

        DestroyImmediate(instance);
    }
}
