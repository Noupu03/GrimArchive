using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// 모든 유닛 프리팹의 "Visual" 자식에 ShadowCaster2D를 갖추도록 맞추는 에디터 도구(2026-08-24 사용자
// 요청 "모든 유닛에 넣고, 횃불 바로 위에 있으면 빛 그냥 투과로 예외처리 하자"). 런타임 예외 처리
// (횃불 위에서 컴포넌트를 임시로 끄는 것)는 UnitGenerate.SyncVisual이 담당 — 이 도구는 그 전제가
// 되는 "모든 유닛이 애초에 ShadowCaster2D를 갖고 있어야 한다"는 부분만 채운다.
//
// [2026-08-24 수정 — 최초 버전의 InvalidCastException 버그] 처음엔 이미 올바르게 설정된 3개 프리팹
// (근접 탱커/기사형/아처형) 중 하나를 ComponentUtility.CopyComponent/PasteComponentAsNew로 복제했는데,
// 유닛을 스폰할 때마다 ShadowCaster2D.OnEnable → ShadowShape2DProvider_SpriteRenderer.Enable에서
// InvalidCastException이 터졌다(errors.txt). 원인: ShadowCaster2D의 내부 필드 m_ShadowShape2DComponent
// (그림자 모양을 제공하는 소스로 삼을 컴포넌트, 보통 자기 자신의 SpriteRenderer)가 "원본 프리팹 안의
// 특정 컴포넌트"를 가리키는 직렬화 참조인데, 이 상태 그대로 복사-붙여넣기하면 대상 프리팹에서는 엉뚱한
// 참조로 남는다 — 게다가 이 필드가 이미 채워진 채로 있으면(m_ShadowCastingSource가 -1이 아니게 되면)
// Awake()의 자동 감지 로직(ShapeProviderUtility.TryGetDefaultShadowShapeProviderSource — 에디터 전용,
// GameObject 자신의 Renderer를 스스로 찾아 올바르게 다시 연결해주는 코드)이 건너뛰어져 잘못된 참조가
// 그대로 굳어버린다.
//
// 올바른 해법은 복사가 아니라 "새로 추가"다 — AddComponent<ShadowCaster2D>()로 만든 새 컴포넌트는
// m_ShadowCastingSource가 기본값 -1이라 Awake()가 자동으로 "이 GameObject 자신의" Renderer를 찾아
// 올바르게 연결한다. 기본 castingOption도 이미 CastShadow(자기 자신은 안 가리되 그림자는 드리움 —
// 원래 3개 프리팹의 설정과 동일)라 별도 설정도 필요 없다. 이 도구는 그래서 (1) 이미 붙어있는
// ShadowCaster2D는 일단 전부 지우고(복사로 생겨 망가진 것 포함, 원래 정상이던 3개도 포함 —
// 재생성해도 결과는 동일하므로 구분할 필요가 없다) (2) AddComponent로 깨끗하게 다시 만드는 방식으로
// 항상 "새로 추가"만 하도록 통일했다.
public static class UnitShadowCasterSync
{
    private const string TargetFolder = "Assets/Resources/Units";
    private const string VisualChildName = "Visual";

    [MenuItem("Tools/GrimArchive/유닛 프리팹에 ShadowCaster2D 통일")]
    public static void SyncAllUnitPrefabs()
    {
        if (!AssetDatabase.IsValidFolder(TargetFolder))
        {
            Debug.LogError($"[UnitShadowCasterSync] 폴더를 찾을 수 없습니다: {TargetFolder}");
            return;
        }

        int recreated = 0, skippedNoVisual = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { TargetFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform visual = contents.transform.Find(VisualChildName);
                if (visual == null)
                {
                    Debug.LogWarning($"[UnitShadowCasterSync] '{VisualChildName}' 자식이 없어 건너뜀: {path}");
                    skippedNoVisual++;
                    continue;
                }

                // 기존 것(복사로 망가졌을 수 있는 것 포함)은 지우고 항상 새로 추가한다 — AddComponent가
                // Awake() 자동 감지를 타게 만드는 것 자체가 이 수정의 핵심이라, 복사로 생긴 것이든
                // 원래 있던 것이든 구분하지 않고 전부 재생성한다.
                var existing = visual.GetComponent<ShadowCaster2D>();
                if (existing != null)
                    Object.DestroyImmediate(existing, true);

                visual.gameObject.AddComponent<ShadowCaster2D>();

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                Debug.Log($"[UnitShadowCasterSync] 재생성됨: {path}");
                recreated++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[UnitShadowCasterSync] 완료 — {recreated}개 프리팹 재생성 / '{VisualChildName}' 없어 " +
                  $"건너뜀 {skippedNoVisual}개. 재실행해도 안전(매번 새로 만드므로 항상 정상 상태로 수렴).");
    }
}
