using UnityEngine;
using System.Collections.Generic;
#if UNITY_2022_2_OR_NEWER
using UnityEngine.U2D.Animation;
#endif

// 유닛 타입 이름 → 프리팹 매핑 레지스트리. 스프라이트/스탯/스킬/이펙트는 전부 프리팹의
// UnitVisualDefinition(+자식 계층)에서 오고 여기서는 그 프리팹을 찾아주는 역할만 한다 —
// 인스펙터 매핑 대신 Assets/Resources/Units/{unitTypeName}.prefab 경로 컨벤션으로 조회한다.
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
    // 아이콘/몬스터 배치 실루엣이 공유한다.
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

    // 02번 문서 9번 항목: 예상 피해량 근사 계산용 — 살아있는 실시간 인스턴스가 아니라 "그 종의 기본
    // (버프 없는) 스탯"이 필요할 때 쓴다(버프/디버프가 Unit.CombatStat을 직접 변경하는 시스템이 실제로
    // 있어 CombatStat을 그대로 읽으면 안 됨 — SkillAction_PartyBuff.cs/SkillAction_Curse.cs 참고).
    public bool TryGetBaseCombatStats(string unitTypeName, out float physicalAttack, out float magicalAttack)
    {
        var prefab = GetPrefab(unitTypeName);
        var visualDef = prefab != null ? prefab.GetComponent<UnitVisualDefinition>() : null;
        physicalAttack = visualDef != null ? visualDef.stats.physicalAttack : 0f;
        magicalAttack = visualDef != null ? visualDef.stats.magicalAttack : 0f;
        return visualDef != null;
    }

    // GetSkills(SkillAction 리스트)와 별개 — damageMultiplier 등 raw 수치가 필요한 예상 피해량 계산은
    // SkillAction이 캡슐화해 감추고 있는 SkillData 자체를 봐야 한다.
    public List<SkillData> GetSkillDataList(string unitTypeName)
    {
        var prefab = GetPrefab(unitTypeName);
        var visualDef = prefab != null ? prefab.GetComponent<UnitVisualDefinition>() : null;
        return visualDef != null ? visualDef.skills : new List<SkillData>();
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
    // SpriteResolver에 category/label과 flipX를 적용하는 공통 절차 — UnitGenerate/TorchVisual의
    // 중복 로직을 이 한 곳으로 합쳤다(SpriteRenderer가 없으면 flipX는 조용히 건너뜀).
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
