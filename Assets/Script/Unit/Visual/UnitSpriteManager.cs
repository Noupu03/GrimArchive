using UnityEngine;
using System.Collections.Generic;
#if UNITY_2022_2_OR_NEWER
using UnityEngine.U2D.Animation;
#endif

// 유닛 타입 이름 → 프리팹 매핑 레지스트리. 스프라이트/스탯/스킬/이펙트는 전부
// 프리팹의 UnitVisualDefinition(+자식 계층)에서 오고, 여기서는 그 프리팹을 찾아주는 역할만 한다.
public class UnitSpriteManager : MonoBehaviour
{
    [System.Serializable]
    public class UnitTypePrefab
    {
        public string unitTypeName;
        public GameObject prefab;
    }

    public List<UnitTypePrefab> unitTypePrefabMap = new List<UnitTypePrefab>();

    private readonly Dictionary<string, List<SkillAction>> _skillsCache = new();

    public GameObject GetPrefab(string unitTypeName)
    {
        foreach (var entry in unitTypePrefabMap)
            if (entry.unitTypeName == unitTypeName && entry.prefab != null)
                return entry.prefab;
        return null;
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
