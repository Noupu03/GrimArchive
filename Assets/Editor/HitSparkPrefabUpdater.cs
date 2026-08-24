using System.Linq;
using UnityEditor;
using UnityEngine;

// 모든 유닛 프리팹의 UnitVisualDefinition.bloodEffectPrefab을 VFX_BloodDrip으로 일괄 설정하는
// 에디터 도구(2026-08-24 사용자 요청 "hitspark는 유지하고 추가로 BloodDrip이 출력되게 하려는거야").
// hitSparkPrefab은 건드리지 않는다 — bloodEffectPrefab은 UnitGenerate.TriggerHitEffect가
// hitSparkPrefab과 별도로 "같이" 스폰하는 추가 슬롯이다(대체가 아니라 추가).
//
// Assets/Data/units.json도 이미 전 유닛 visual.effects.bloodDrip="VFX_BloodDrip"으로 맞춰뒀지만
// (units.json이 진실의 원천이라는 JsonToUnitPrefabConverter 관례를 따름), JSON만으로는 이미 만들어진
// 프리팹에 반영되지 않는다(JsonToUnitPrefabConverter 재실행은 인스펙터에서 손댄 다른 값까지 프리팹
// 기본값으로 되돌릴 위험이 있어 피함) — 그래서 이 필드 하나만 정밀하게 건드리는 전용 도구로 대신
// 처리한다(VfxParticleLifetimeAttacher와 동일한 관례: Tools/GrimArchive 메뉴, 원클릭, 재실행해도
// 안전).
//
// [자기복구] 이 도구의 이전 버전(같은 세션, 실행 전에 정정됨)은 실수로 hitSparkPrefab을 통째로
// VFX_BloodDrip으로 교체하는 잘못된 방식이었다 — 혹시 그 버전을 이미 실행한 적이 있다면, 이번 실행이
// hitSparkPrefab이 VFX_BloodDrip을 가리키고 있는 경우에 한해 원래 값(VFX_HitSpark)으로 되돌려
// 자동으로 바로잡는다.
public static class HitSparkPrefabUpdater
{
    private const string TargetFolder = "Assets/Resources/Units";
    private const string BloodDripName = "VFX_BloodDrip";
    private const string HitSparkName = "VFX_HitSpark";

    [MenuItem("Tools/GrimArchive/VFX -> 전체 유닛에 BloodDrip 피격 이펙트 추가")]
    public static void ApplyBloodDripHitSpark()
    {
        GameObject bloodDrip = FindPrefabByExactName(BloodDripName);
        if (bloodDrip == null)
        {
            Debug.LogError($"[HitSparkPrefabUpdater] '{BloodDripName}' 프리팹을 찾을 수 없습니다.");
            return;
        }
        GameObject hitSpark = FindPrefabByExactName(HitSparkName); // 자기복구용, 없어도 치명적이지 않음

        if (!AssetDatabase.IsValidFolder(TargetFolder))
        {
            Debug.LogError($"[HitSparkPrefabUpdater] 폴더를 찾을 수 없습니다: {TargetFolder}");
            return;
        }

        int bloodAdded = 0, bloodUnchanged = 0, hitSparkRestored = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { TargetFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var def = contents.GetComponent<UnitVisualDefinition>();
                if (def == null)
                {
                    Debug.LogWarning($"[HitSparkPrefabUpdater] UnitVisualDefinition이 없어 건너뜀: {path}");
                    continue;
                }

                bool dirty = false;

                // 자기복구 — 이전(잘못된) 실행으로 hitSparkPrefab이 BloodDrip으로 바뀌어 있으면 되돌린다.
                if (hitSpark != null && def.hitSparkPrefab == bloodDrip)
                {
                    def.hitSparkPrefab = hitSpark;
                    hitSparkRestored++;
                    dirty = true;
                }

                if (def.bloodEffectPrefab != bloodDrip)
                {
                    def.bloodEffectPrefab = bloodDrip;
                    bloodAdded++;
                    dirty = true;
                }
                else
                {
                    bloodUnchanged++;
                }

                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    Debug.Log($"[HitSparkPrefabUpdater] 적용됨: {path}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[HitSparkPrefabUpdater] 완료 — bloodEffectPrefab 신규 적용 {bloodAdded}개 / 이미 적용됨 " +
                  $"{bloodUnchanged}개. hitSparkPrefab 자기복구 {hitSparkRestored}개(0이면 정상 — 되돌릴 것이 없었다는 뜻).");
    }

    private static GameObject FindPrefabByExactName(string name)
    {
        return AssetDatabase.FindAssets($"t:Prefab {name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .FirstOrDefault(go => go != null && go.name == name);
    }
}
