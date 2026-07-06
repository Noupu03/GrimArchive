using System.Collections.Generic;
using UnityEngine;

// 유닛 타입별 프리팹의 루트에 붙는 컴포넌트.
// 스탯/스킬/이펙트를 인스펙터에서 직접 편집한다 (스프라이트/애니메이션은 프리팹 자식 계층의
// SpriteLibrary/SpriteResolver/UnitAnimationController 컴포넌트에서 직접 편집).
public class UnitVisualDefinition : MonoBehaviour
{
    [Header("유닛 식별")]
    public string unitTypeName;
    public Vector2 footprint = Vector2.one;
    public int engageDistance = 2;

    [Header("스탯")]
    public UnitStatsData stats = new UnitStatsData();

    [Header("스킬")]
    public List<SkillData> skills = new List<SkillData>();

    [Header("이펙트")]
    public GameObject hitSparkPrefab;
    public GameObject guardPrefab;
    public GameObject parryPrefab;
    public GameObject attackFailPrefab;

    public void ApplyStatsTo(Unit unit)
    {
        if (unit == null) return;

        unit.maxHp = stats.maxHp; unit.hp = stats.maxHp;
        unit.maxMp = stats.maxMp; unit.mp = stats.maxMp;
        unit.physicalAttack      = stats.physicalAttack;
        unit.magicalAttack       = stats.magicalAttack;
        unit.physicalDefense     = stats.physicalDefense;
        unit.magicalDefense      = stats.magicalDefense;
        unit.HPRegen             = stats.HPRegen;
        unit.attackspeed         = stats.attackspeed;
        unit.walkSpeed           = stats.walkSpeed;
        unit.reaction            = stats.reaction;
        unit.criticalChance      = stats.criticalChance;
        unit.cooltimeReduction   = stats.cooltimeReduction;
        unit.statusResistance    = stats.statusResistance;
        unit.maxMental           = stats.maxMental;
        unit.mental              = stats.mental;
        unit.spotting            = stats.spotting;
        unit.leadershipRange     = stats.leadershipRange;
        unit.charisma            = stats.charisma;
        unit.physicalAttackSpeed = stats.physicalAttackSpeed;
        unit.magicalCastSpeed    = stats.magicalCastSpeed;

        if (unit.unitType != null) unit.unitType.footprint = footprint;
    }

    public List<SkillAction> BuildSkillActions()
    {
        var list = new List<SkillAction>();
        foreach (var sd in skills)
        {
            if (sd.isProjectile)
                list.Add(new SkillAction_Projectile(sd, null)); // 프리팹은 null로 전달 (Fallback 비주얼 사용)
            else
                list.Add(new SkillAction_Generic(sd));
        }
        return list;
    }
}
