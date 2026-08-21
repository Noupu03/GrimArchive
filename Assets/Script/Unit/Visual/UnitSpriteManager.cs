using UnityEngine;
using System.Collections.Generic;
#if UNITY_2022_2_OR_NEWER
using UnityEngine.U2D.Animation;
#endif

// 유닛 타입 이름 → 프리팹 매핑 레지스트리. 스프라이트/스탯/스킬/이펙트는 전부
// 프리팹의 UnitVisualDefinition(+자식 계층)에서 오고, 여기서는 그 프리팹을 찾아주는 역할만 한다.
// ThreatTileRenderer의 attackZone.spriteLib와 동일한 컨벤션: 인스펙터 매핑 대신
// Assets/Resources/Units/{unitTypeName}.prefab 경로 컨벤션으로 Resources.Load 조회한다.
public class UnitSpriteManager
{
    private const string UnitPrefabResourceFolder = "Units";

    private readonly Dictionary<string, List<SkillAction>> _skillsCache = new();
    private readonly Dictionary<string, Sprite> _iconCache = new();

    public GameObject GetPrefab(string unitTypeName)
    {
        return Resources.Load<GameObject>($"{UnitPrefabResourceFolder}/{unitTypeName}");
    }

    // 유닛 타입의 대표 아이콘(프리팹의 "Visual" 자식 SpriteRenderer.sprite) — 웨이브 게이지 파티
    // 아이콘(WaveGaugePanel)/몬스터 배치 실루엣(InputManager)이 공유한다(2026-08-20, 두 곳에서
    // 각자 동일한 로직을 중복 구현했던 것을 여기로 통합).
    public Sprite GetIcon(string unitTypeName)
    {
        if (string.IsNullOrEmpty(unitTypeName)) return null;
        if (_iconCache.TryGetValue(unitTypeName, out var cached)) return cached;

        Sprite icon = null;
        GameObject prefab = GetPrefab(unitTypeName);
        Transform visual = prefab != null ? prefab.transform.Find("Visual") : null;
        SpriteRenderer sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (sr != null) icon = sr.sprite;

        _iconCache[unitTypeName] = icon;
        return icon;
    }

    public List<SkillAction> GetSkills(string unitTypeName)
    {
        if (_skillsCache.TryGetValue(unitTypeName, out var cached)) return cached;

        var prefab = GetPrefab(unitTypeName);
        var visualDef = prefab != null ? prefab.GetComponent<UnitVisualDefinition>() : null;
        var list = visualDef != null ? visualDef.BuildSkillActions() : new List<SkillAction>();

        _skillsCache[unitTypeName] = list;
        return list;
    }

    public int GetEngageDistance(string unitTypeName, int defaultDist)
    {
        var prefab = GetPrefab(unitTypeName);
        var visualDef = prefab != null ? prefab.GetComponent<UnitVisualDefinition>() : null;
        return visualDef != null ? visualDef.engageDistance : defaultDist;
    }

    // direction → label / flipX 반환. category(바리에이션)는 호출부에서 결정
    public static void GetSpriteLabelForDirection(Dir direction, out string label, out bool flipX)
    {
        flipX = false;
        switch (direction)
        {
            case Dir.UP:         label = "Up";       break;
            case Dir.DOWN:       label = "Down";     break;
            case Dir.LEFT:       label = "Left";     break;
            case Dir.RIGHT:      label = "Left"; flipX = true; break;
            case Dir.UP_LEFT:    label = "UpLeft";   break;
            case Dir.UP_RIGHT:   label = "UpLeft"; flipX = true; break;
            case Dir.DOWN_LEFT:  label = "DownLeft"; break;
            case Dir.DOWN_RIGHT: label = "DownLeft"; flipX = true; break;
            default:             label = "Down";     break;
        }
    }

#if UNITY_2022_2_OR_NEWER
    // SpriteResolver에 category/label을 적용하고 flipX까지 같이 세팅하는 공통 절차(2026-08-21 추출) —
    // 원래 UnitGenerate.UpdateSpriteResolver 안에 있던 걸, FogOfWarSystem의 횃불 방향별 스프라이트
    // 적용(TorchVisual.ApplyTorchVisual)이 완전히 동일한 절차를 별도로 다시 구현하고 있어서 이 한
    // 곳으로 합쳤다. resolver가 붙은 GameObject에 SpriteRenderer가 없으면 flipX는 조용히 건너뛴다.
    public static void ApplySpriteResolverLabel(SpriteResolver resolver, string category, string label, bool flipX)
    {
        if (resolver == null) return;

        resolver.SetCategoryAndLabel(category, label);

        SpriteRenderer sr = resolver.GetComponent<SpriteRenderer>();
        if (sr != null) sr.flipX = flipX;
    }

    // 스프라이트 라이브러리의 카테고리 목록을 바리에이션 후보로 반환
    public List<string> GetVariationCategories(SpriteLibraryAsset asset)
    {
        if (asset == null) return new List<string>();
        return new List<string>(asset.GetCategoryNames());
    }

    // 라이브러리에서 랜덤 바리에이션 카테고리 하나 선택
    public string PickRandomVariation(SpriteLibraryAsset asset)
    {
        var cats = GetVariationCategories(asset);
        return cats.Count > 0 ? cats[Random.Range(0, cats.Count)] : "";
    }
#endif
}
