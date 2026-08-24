using UnityEditor;
using UnityEngine;

// "보스 골렘" 프리팹(먼저 Tools/GrimArchive/JSON -> 유닛 프리팹 생성으로 만들어야 함)에 왼손/오른손
// 자식 오브젝트와 BossGolemHandController를 자동으로 붙여주는 후처리 도구(2026-08-24).
// JsonToUnitPrefabConverter는 범용 유닛 생성기라 보스 전용 손 오브젝트까지는 모른다 — 이 도구가 그
// 마지막 수동 배선(자식 오브젝트 생성 + 컴포넌트 연결)을 대신한다.
//
// 이 도구가 못 채워주는 것: 손 SpriteRenderer.sprite(실제 일러스트)는 에셋이 아직 없으므로 직접
// 지정해야 한다 — 기획 확정(2026-08-24): "손의 일러스트는 일반적인 유닛 스프라이트와 크기가 같다"이므로
// 별도 스케일 조정 없이 일반 유닛 스프라이트를 그대로 꽂으면 된다(BossGolemHandController.Awake가
// 부모의 3배 스케일을 자동 상쇄하므로).
public static class BossGolemHandSetupTool
{
    private const string PrefabPath = "Assets/Resources/Units/보스 골렘.prefab";

    [MenuItem("Tools/GrimArchive/보스 골렘 손 오브젝트 배선")]
    public static void SetupHands()
    {
        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabAsset == null)
        {
            Debug.LogError($"[BossGolemHandSetupTool] {PrefabPath}를 찾을 수 없습니다. 먼저 " +
                            "Tools/GrimArchive/JSON -> 유닛 프리팹 생성 (원클릭)으로 '보스 골렘' 프리팹을 만드세요.");
            return;
        }

        string path = AssetDatabase.GetAssetPath(prefabAsset);
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        Transform visual = root.transform.Find("Visual");
        if (visual == null)
        {
            Debug.LogError("[BossGolemHandSetupTool] 프리팹에 Visual 자식이 없습니다 — JSON 컨버터로 다시 생성했는지 확인하세요.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        // 본체(footprint 3x3, 부모 Visual/루트가 런타임에 3배로 스케일됨) 기준 로컬 좌표 자리표시자 —
        // 월드 기준 대략 좌우 (±1.2, 0.3) 위치에 놓이도록 로컬 좌표는 3으로 나눈 값을 쓴다.
        Transform leftHand  = EnsureHandChild(visual, "LeftHand",  new Vector3(-0.4f, 0.1f, 0f), mirrored: false);
        Transform rightHand = EnsureHandChild(visual, "RightHand", new Vector3( 0.4f, 0.1f, 0f), mirrored: true);

        var hands = root.GetComponent<BossGolemHandController>();
        if (hands == null) hands = root.AddComponent<BossGolemHandController>();

        var so = new SerializedObject(hands);
        so.FindProperty("leftHand").objectReferenceValue  = leftHand;
        so.FindProperty("rightHand").objectReferenceValue = rightHand;
        so.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[BossGolemHandSetupTool] LeftHand/RightHand 오브젝트와 BossGolemHandController 배선 완료. " +
                  "남은 작업: 두 SpriteRenderer의 sprite를 일반 유닛 스프라이트로 직접 지정 " +
                  "(BossGolemHandController.Awake가 부모의 3배 스케일을 자동 상쇄하므로 크기 보정은 불필요).");
    }

    // mirrored: 손 스프라이트는 방향별로 한 벌뿐이라 반대쪽 손은 localScale.x = -1로 좌우 반전해서 쓴다
    // (2026-08-24 — 이 부호를 빠뜨리면 양손이 같은 방향을 본다. BossGolemHandController.
    // ApplyNormalSpriteScale이 런타임에 크기만 보정하고 이 부호는 그대로 살려준다).
    private static Transform EnsureHandChild(Transform parent, string name, Vector3 restLocalPos, bool mirrored)
    {
        Transform hand = parent.Find(name);
        if (hand == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.AddComponent<SpriteRenderer>().sortingOrder = 11; // 몸통(10)보다 위에 그려지도록
            hand = go.transform;
        }
        hand.localPosition = restLocalPos;
        hand.localScale = new Vector3(mirrored ? -1f : 1f, 1f, 1f);
        return hand;
    }
}
