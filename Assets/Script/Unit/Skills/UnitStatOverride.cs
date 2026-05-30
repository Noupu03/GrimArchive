using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 런타임 유닛 스탯 오버라이드 레지스트리.
/// UnitStatTuner가 씬 시작 시 채워주며, Unit.SetupStats() 내에서 ApplyTo()를 호출한다.
/// </summary>
public static class UnitStatOverride
{
    public class StatEntry
    {
        public float maxHp;
        public float maxMp;
        public float physicalAttack;
        public float magicalAttack;
        public float physicalDefense;
        public float magicalDefense;
        public float HPRegen;
        public float attackspeed;
        public float walkSpeed;
        public float reaction;
        public float criticalChance;
        public float cooltimeReduction;
        public float statusResistance;
        public float maxMental;
        public float mental;
        public float spotting;
        public float leadershipRange;
        public float charisma;
        public float physicalAttackSpeed;
        public float magicalCastSpeed;
    }

    static readonly Dictionary<string, StatEntry> _overrides = new();

    public static void Register(string unitTypeName, StatEntry entry)
        => _overrides[unitTypeName] = entry;

    public static void Clear() => _overrides.Clear();

    public static bool Has(string unitTypeName)
        => _overrides.ContainsKey(unitTypeName);

    /// <summary>
    /// SetupStats() 내에서 호출. 오버라이드 값을 유닛에 전부 적용한다.
    /// hp/mp/mental은 max 기준으로 초기화된다.
    /// </summary>
    public static void ApplyTo(Unit unit)
    {
        if (!_overrides.TryGetValue(unit.unitType.typeName, out var e)) return;

        unit.maxHp              = e.maxHp;
        unit.hp                 = e.maxHp;
        unit.maxMp              = e.maxMp;
        unit.mp                 = e.maxMp;
        unit.physicalAttack     = e.physicalAttack;
        unit.magicalAttack      = e.magicalAttack;
        unit.physicalDefense    = e.physicalDefense;
        unit.magicalDefense     = e.magicalDefense;
        unit.HPRegen            = e.HPRegen;
        unit.attackspeed        = e.attackspeed;
        unit.walkSpeed          = e.walkSpeed;
        unit.reaction           = e.reaction;
        unit.criticalChance     = e.criticalChance;
        unit.cooltimeReduction  = e.cooltimeReduction;
        unit.statusResistance   = e.statusResistance;
        unit.maxMental          = e.maxMental;
        unit.mental             = e.mental;
        unit.spotting           = e.spotting;
        unit.leadershipRange    = e.leadershipRange;
        unit.charisma           = e.charisma;
        unit.physicalAttackSpeed = e.physicalAttackSpeed;
        unit.magicalCastSpeed   = e.magicalCastSpeed;
    }

    /// <summary>
    /// 런타임 즉시 적용용. hp/mp는 새 최대치를 초과하지 않도록 보정하고 파생 스탯을 재계산한다.
    /// </summary>
    public static void ApplyRuntime(Unit unit)
    {
        if (!_overrides.TryGetValue(unit.unitType.typeName, out var e)) return;

        unit.maxHp = e.maxHp;
        unit.hp    = Mathf.Min(unit.hp, unit.maxHp);
        unit.maxMp = e.maxMp;
        unit.mp    = Mathf.Min(unit.mp, unit.maxMp);

        unit.physicalAttack     = e.physicalAttack;
        unit.magicalAttack      = e.magicalAttack;
        unit.physicalDefense    = e.physicalDefense;
        unit.magicalDefense     = e.magicalDefense;
        unit.HPRegen            = e.HPRegen;
        unit.attackspeed        = e.attackspeed;
        unit.walkSpeed          = e.walkSpeed;
        unit.reaction           = e.reaction;
        unit.criticalChance     = e.criticalChance;
        unit.cooltimeReduction  = e.cooltimeReduction;
        unit.statusResistance   = e.statusResistance;
        unit.maxMental          = e.maxMental;
        unit.mental             = Mathf.Min(unit.mental, unit.maxMental);
        unit.spotting           = e.spotting;
        unit.leadershipRange    = e.leadershipRange;
        unit.charisma           = e.charisma;
        unit.physicalAttackSpeed = e.physicalAttackSpeed;
        unit.magicalCastSpeed   = e.magicalCastSpeed;

        unit.CalculateDerivedStats();
    }
}
