using UnityEditor;
using UnityEngine;

// 보스 골렘 프리팹의 UnitVisualDefinition.visualScaleIgnoresFootprint를 켜는 일회성 도구(2026-08-24
// 사용자 신고 "골렘 이미지 자체를 3배 크기로 그려서... 이미지 자체가 3배 스케일링, 3배해서 9배가
// 되어버림"). 원본 스프라이트 아트가 이미 footprint(3배) 배율로 그려져 있는데, UnitGenerate.
// SetupUnitVisual이 footprint를 루트 오브젝트 크기에 한 번 더 곱해서 실질적으로 9배가 되고 있었다.
// footprint 값 자체(인구수/충돌/타일 점유 등 게임플레이 판정)는 그대로 두고, 시각적 확대만 건너뛰게
// UnitVisualDefinition.cs에 새로 추가한 플래그를 이 프리팹에 켠다.
public static class BossGolemVisualScaleFix
{
    private const string TargetPath = "Assets/Resources/Units/보스 골렘.prefab";

    [MenuItem("Tools/GrimArchive/보스 골렘 시각 스케일 9배 버그 수정")]
    public static void FixVisualScale()
    {
        if (!System.IO.File.Exists(TargetPath))
        {
            Debug.LogError($"[BossGolemVisualScaleFix] 프리팹을 찾을 수 없습니다: {TargetPath}");
            return;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(TargetPath);
        try
        {
            var def = contents.GetComponent<UnitVisualDefinition>();
            if (def == null)
            {
                Debug.LogError($"[BossGolemVisualScaleFix] UnitVisualDefinition을 찾을 수 없습니다: {TargetPath}");
                return;
            }

            if (def.visualScaleIgnoresFootprint)
            {
                Debug.Log("[BossGolemVisualScaleFix] 이미 켜져 있어 변경 사항 없음.");
                return;
            }

            def.visualScaleIgnoresFootprint = true;
            PrefabUtility.SaveAsPrefabAsset(contents, TargetPath);
            Debug.Log("[BossGolemVisualScaleFix] visualScaleIgnoresFootprint를 켰습니다 — " +
                      "이제 footprint([3,3])는 게임플레이 판정에만 쓰이고 시각적 크기는 스프라이트 " +
                      "원본 크기 그대로(추가 확대 없음) 적용됩니다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.SaveAssets();
    }
}
