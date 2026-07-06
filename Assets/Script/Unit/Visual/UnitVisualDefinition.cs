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

    [Header("가중치 시스템 (이해도/위험도/흥미도)")]
    [Tooltip("보스/네메시스 등 종별+개별 이해도를 함께 쓰는 특수 유닛인지 (연산공식 문서 7-1장)")]
    public bool isSpecialUnit = false;
    [Tooltip("IsInterestTarget 플래그 - 켜져있으면 이해도 상승에 따른 흥미도 감소식을 적용하지 않음 (18장)")]
    public bool isInterestTarget = false;
    [Tooltip("유닛 기본 흥미도 (6-3장)")]
    public float baseInterest = 0f;
    [Tooltip("대상 기본 위험도 (12-1장 최종 위험도 = 기본 + 종별누적 + 개별누적)")]
    public float baseDanger = 0f;
    [Tooltip("\"일정 피해량 이상\" 판정 기준값 - 이 값 미만의 피해는 위험도/이해도 이벤트를 발생시키지 않음 (3장)")]
    public float heavyHitThreshold = 10f;

    public void ApplyStatsTo(Unit unit)
    {
        if (unit == null) return;

        unit.isSpecialUnit = isSpecialUnit;
        unit.isInterestTarget = isInterestTarget;
        unit.baseInterest = baseInterest;
        unit.baseDanger = baseDanger;
        unit.heavyHitThreshold = heavyHitThreshold;

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
            list.Add(new SkillAction_Generic(sd));
        return list;
    }
}
