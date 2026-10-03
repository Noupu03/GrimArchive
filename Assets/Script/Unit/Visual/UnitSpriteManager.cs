using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
#if UNITY_2022_2_OR_NEWER
using UnityEngine.U2D.Animation;
#endif

// 유닛 타입 이름 → 프리팹 매핑 레지스트리. 스프라이트/스탯/스킬/이펙트는 전부 프리팹의
// UnitVisualDefinition(+자식 계층)에서 오고 여기서는 그 프리팹을 찾아주는 역할만 한다 —
// 프리팹은 Assets/Prefabs/Units/{unitTypeName}.prefab, Addressables 주소 "Units/{unitTypeName}"
// (전부 "Units" 라벨)이고 LoadAsync가 라벨로 한 번에 불러 둔다.
public class UnitSpriteManager
{
    private readonly Dictionary<string, GameObject> _prefabs = new();
    private readonly Dictionary<string, List<SkillAction>> _skillsCache = new();
    private readonly Dictionary<string, Sprite> _iconCache = new();
    private bool _loaded;

    // 유닛 생성과 매 틱 AI 판단(GetSkills 등)이 프리팹을 동기로 읽으므로 GameSession.Initialize가 유닛을
    // 스폰하기 전에 await한다. 여러 번 불려도 GameAssets가 같은 로드를 공유한다. 프리팹 루트 이름 = 파일
    // 이름 = 유닛 타입 이름이다(JsonToUnitPrefabConverter가 그렇게 만든다).
    public async UniTask LoadAsync()
    {
        IList<GameObject> prefabs = await GameAssets.LoadByLabelAsync<GameObject>(AssetKeys.UnitsLabel);
        if (prefabs == null) return;
        foreach (var prefab in prefabs)
            if (prefab != null) _prefabs[prefab.name] = prefab;
        _loaded = true;
    }

    // LoadAsync 전이거나 등록 안 된 타입이면 null — 호출부는 원래부터 null 폴백(도형 비주얼 등)을 갖고 있다.
    public GameObject GetPrefab(string unitTypeName)
    {
        if (string.IsNullOrEmpty(unitTypeName)) return null;
        return _prefabs.TryGetValue(unitTypeName, out var prefab) ? prefab : null;
    }

    // 유닛 타입의 대표 아이콘(프리팹의 "Visual" 자식 SpriteRenderer.sprite) — 웨이브 게이지 파티
    // 아이콘/몬스터 배치 실루엣이 공유한다.
    public Sprite GetIcon(string unitTypeName)
    {
        if (string.IsNullOrEmpty(unitTypeName)) return null;
        if (_iconCache.TryGetValue(unitTypeName, out var cached)) return cached;

        GameObject prefab = GetPrefab(unitTypeName);
        // 아직 LoadAsync 전이면 "아이콘 없음"을 캐시하지 않는다 — 로드가 끝난 뒤 다시 물으면 채워진다.
        if (prefab == null && !_loaded) return null;

        Sprite icon = null;
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
        // GetIcon과 같은 이유로 로드 전의 빈 결과는 캐시하지 않는다.
        if (prefab == null && !_loaded) return new List<SkillAction>();
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
