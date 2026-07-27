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
    [Tooltip("유닛 배치 시스템(2026-07-27 신규) 5.2장: 방 인구수 점유량 - 기본 유닛 1")]
    public int populationCost = 1;

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

    [Header("시야-인지-반응 시스템 (01_시야·인지범위·가시성)")]
    [Tooltip("은신 - 최종 가시성을 낮추는 세부 스탯 (01장 10절)")]
    public float stealth = 0f;
    [Tooltip("대상 기본 가시성 - 일반 유닛 100, 은신형/특수 유닛은 낮게 설정 (01장 9절)")]
    public float baseVisibility = 100f;

    public void ApplyStatsTo(Unit unit)
    {
        if (unit == null) return;

        unit.isSpecialUnit = isSpecialUnit;
        unit.isInterestTarget = isInterestTarget;
        unit.populationCost = populationCost;
        unit.BaseStat.baseInterest = baseInterest;
        unit.BaseStat.baseDanger = baseDanger;
        unit.BaseStat.heavyHitThreshold = heavyHitThreshold;
        unit.VisionStat.stealth = stealth;
        unit.VisionStat.baseVisibility = baseVisibility;

        unit.Health.maxHp = stats.maxHp; unit.Health.hp = stats.maxHp;
        unit.Health.maxMp = stats.maxMp; unit.Health.mp = stats.maxMp;
        var combatStats = unit.CombatStat; if(combatStats != null) { combatStats.physicalAttack = stats.physicalAttack;
        combatStats.magicalAttack = stats.magicalAttack;
        combatStats.physicalDefense = stats.physicalDefense;
        combatStats.magicalDefense = stats.magicalDefense;
        unit.BaseStat.HPRegen = stats.HPRegen;
        combatStats.attackspeed = stats.attackspeed;
        unit.BaseStat.walkSpeed = stats.walkSpeed;
        unit.BaseStat.reaction = stats.reaction;
        combatStats.criticalChance = stats.criticalChance; }
        unit.BaseStat.cooltimeReduction = stats.cooltimeReduction;
        unit.BaseStat.statusResistance = stats.statusResistance;
        unit.BaseStat.maxMental = stats.maxMental;
        unit.BaseStat.mental = stats.mental;
        unit.VisionStat.spotting = stats.spotting;
        unit.BaseStat.leadershipRange = stats.leadershipRange;
        unit.BaseStat.charisma = stats.charisma;
        unit.CombatState.State.physicalAttackSpeed = stats.physicalAttackSpeed;
        unit.CombatState.State.magicalCastSpeed    = stats.magicalCastSpeed;

        if (unit.unitType != null) unit.unitType.footprint = footprint;
    }

    public List<SkillAction> BuildSkillActions()
    {
        var list = new List<SkillAction>();
        foreach (var sd in skills)
        {
            if (sd.isProjectile)
                list.Add(new SkillAction_Projectile(sd, sd.projectilePrefab)); // 설정된 프리팹 전달
            else
                list.Add(new SkillAction_Generic(sd));
        }
        return list;
    }

#if UNITY_EDITOR
    [System.Serializable]
    private class UnitsJsonWrapper { public List<UnitJsonNode> units; }
    [System.Serializable]
    private class UnitJsonNode {
        public string typeName;
        public int[] footprint;
        public int engageDistance;
        public List<string> skills;
        public UnitStatsData stats;
    }
    [System.Serializable]
    private class SkillsJsonWrapper { public List<SkillData> skills; }

    [ContextMenu("Load Data From JSON (units.json / skills.json)")]
    public void LoadDataFromJson()
    {
        if (string.IsNullOrEmpty(unitTypeName))
        {
            Debug.LogError("Unit Type Name이 없습니다. (예: 아처형)");
            return;
        }

        string unitsPath = System.IO.Path.Combine(Application.dataPath, "Data", "units.json");
        string skillsPath = System.IO.Path.Combine(Application.dataPath, "Data", "skills.json");

        if (!System.IO.File.Exists(unitsPath) || !System.IO.File.Exists(skillsPath))
        {
            Debug.LogError("Data 폴더에 units.json 또는 skills.json이 없습니다.");
            return;
        }

        string unitsJson = System.IO.File.ReadAllText(unitsPath);
        string skillsJson = System.IO.File.ReadAllText(skillsPath);

        UnitsJsonWrapper unitsData = JsonUtility.FromJson<UnitsJsonWrapper>(unitsJson);
        SkillsJsonWrapper skillsData = JsonUtility.FromJson<SkillsJsonWrapper>(skillsJson);

        if (unitsData == null || unitsData.units == null) return;

        UnitJsonNode targetNode = null;
        foreach (var node in unitsData.units)
        {
            if (node.typeName == this.unitTypeName)
            {
                targetNode = node;
                break;
            }
        }

        if (targetNode == null)
        {
            Debug.LogError($"{unitTypeName} 데이터를 units.json에서 찾을 수 없습니다.");
            return;
        }

        // 스탯 및 기본 정보 덮어쓰기
        if (targetNode.footprint != null && targetNode.footprint.Length >= 2)
            this.footprint = new Vector2(targetNode.footprint[0], targetNode.footprint[1]);
        this.engageDistance = targetNode.engageDistance;
        this.stats = targetNode.stats;

        // 스킬 연결
        this.skills.Clear();
        if (targetNode.skills != null && skillsData != null && skillsData.skills != null)
        {
            foreach (var skillName in targetNode.skills)
            {
                foreach (var sd in skillsData.skills)
                {
                    if (sd.skillName == skillName)
                    {
                        this.skills.Add(sd);
                        break;
                    }
                }
            }
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[{unitTypeName}] 데이터 JSON 불러오기 완료!");
    }
#endif
}
