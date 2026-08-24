using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 모든 VFX 프리팹에 ParticleLifetimeController를 일괄 부착하는 에디터 도구(2026-08-24 사용자 요청
// "모든 VFX 프리팹에 자동부착해줘. 파티클에"). JsonToUnitPrefabConverter와 동일한 관례(Tools/GrimArchive
// 메뉴, 원클릭, 재실행 가능) — 이미 붙어있으면 건너뛰므로 새 VFX 프리팹이 추가된 뒤 다시 실행해도
// 안전하다.
//
// 대상 폴더: TargetFolders에 있는 프리팹 전체. 루트 GameObject에 ParticleSystem이 있는 것만 부착
// 대상이다(ParticleLifetimeController가 [RequireComponent(typeof(ParticleSystem))]라, 없는 오브젝트에
// 억지로 붙이면 빈 ParticleSystem이 같이 생겨버린다 — 예: Torch.prefab은 파티클이 아니라 스프라이트
// 기반이라 자동으로 제외됨).
//
// 명시적 제외: VFX_BlockBreaking — 코어/문 파괴 채널링 전용 VFX로, 재생 시간이 아니라 게임플레이
// 이벤트(파괴 완료)가 끝을 결정한다(VFXManager.SpawnBlockBreakingVfx/StopBlockBreakingVfx, looping=1
// 프리팹). 여기에 유지 시간 기반 자동 정지를 붙이면 파괴가 끝나기도 전에 이펙트가 멈춰버린다.
public static class VfxParticleLifetimeAttacher
{
    private static readonly string[] TargetFolders =
    {
        "Assets/VFX/Prefab",
        "Assets/Resources/Prefabs/VFX",
    };

    // 파일명(확장자 제외) 기준 제외 목록 — 위 설명 참고.
    private static readonly HashSet<string> ExcludedPrefabNames = new HashSet<string>
    {
        "VFX_BlockBreaking",
    };

    [MenuItem("Tools/GrimArchive/VFX -> ParticleLifetimeController 자동 부착")]
    public static void AttachToAllVfxPrefabs()
    {
        var guids = TargetFolders
            .Where(AssetDatabase.IsValidFolder)
            .SelectMany(folder => AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            .Distinct();

        int attached = 0, alreadyPresent = 0, excluded = 0, noParticleSystem = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);

            if (ExcludedPrefabNames.Contains(name))
            {
                Debug.Log($"[VfxParticleLifetimeAttacher] 제외 목록 — 건너뜀: {path}");
                excluded++;
                continue;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (contents.GetComponent<ParticleSystem>() == null)
                {
                    Debug.LogWarning($"[VfxParticleLifetimeAttacher] 루트에 ParticleSystem이 없어 건너뜀(파티클 프리팹이 아닌 것으로 추정): {path}");
                    noParticleSystem++;
                    continue;
                }

                if (contents.GetComponent<ParticleLifetimeController>() != null)
                {
                    alreadyPresent++;
                    continue;
                }

                contents.AddComponent<ParticleLifetimeController>();
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                Debug.Log($"[VfxParticleLifetimeAttacher] 부착됨: {path}");
                attached++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[VfxParticleLifetimeAttacher] 완료 — 신규 부착 {attached}개 / 이미 있음 {alreadyPresent}개 / " +
                  $"제외 {excluded}개 / ParticleSystem 없어 건너뜀 {noParticleSystem}개. " +
                  "각 프리팹의 ParticleLifetimeController Emission Duration은 기본값(-1, 파티클 자체 재생 " +
                  "길이 사용)으로 부착되니, 필요하면 인스펙터에서 프리팹별로 직접 조정할 것.");
    }
}
