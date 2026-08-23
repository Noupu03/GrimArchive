using UnityEngine;

public class SkillAction_Heal : SkillAction
{
    readonly SkillData _d;

    public SkillAction_Heal(SkillData data)
    {
        _d = data;
        SkillName = string.IsNullOrEmpty(data.skillName) ? "치유" : data.skillName;
    }

    public override float DefaultBaseDelayMs  => _d.baseDelayMs;
    public override float DefaultBaseCooldown => _d.baseCooldown;

    public override ThreatShape HitShape => ThreatShape.LINE;
    public override int HitRange => _d.hitRange > 0 ? _d.hitRange : (_d.threatRange > 0 ? _d.threatRange : 5);
    public override int HitWidth => 1;
    public override int HitDepth => 1;

    public Unit FindLowestHpAlly(Unit unit, int maxRange = -1)
    {
        if (unit == null || unit.Session == null || unit.Session.units == null) return unit;

        Unit lowestAlly = null;
        float lowestHpRatio = 1.0f;
        int range = maxRange >= 0 ? maxRange : (HitRange > 0 ? HitRange : 5);

        foreach (var u in unit.Session.units)
        {
            if (u == null || u.Health == null || u.Health.hp <= 0 || u.currentFloor != unit.currentFloor) continue;
            if (unit.IsEnemy(u)) continue;

            float dist = Vector2.Distance(unit.position, u.position);
            if (dist > range) continue;

            float hpRatio = u.Health.hp / Mathf.Max(1f, u.Health.maxHp);
            if (hpRatio < lowestHpRatio)
            {
                lowestHpRatio = hpRatio;
                lowestAlly = u;
            }
        }

        if (unit.Health != null)
        {
            float selfRatio = unit.Health.hp / Mathf.Max(1f, unit.Health.maxHp);
            if (selfRatio < lowestHpRatio)
            {
                lowestAlly = unit;
            }
        }

        return lowestAlly;
    }

    public override bool IsAvailable(Unit unit)
    {
        if (unit == null || unit.CombatState.State.skillCooldowns == null) return false;
        if (_d.cooldownSlot < 0 || _d.cooldownSlot >= unit.CombatState.State.skillCooldowns.Length) return false;
        if (unit.CombatState.State.skillCooldowns[_d.cooldownSlot] > 0f) return false;

        Unit lowest = FindLowestHpAlly(unit);
        return lowest != null && lowest.Health != null && lowest.Health.hp < lowest.Health.maxHp;
    }

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        Unit ally = FindLowestHpAlly(unit);
        if (ally == null || ally.Health == null || ally.Health.hp >= ally.Health.maxHp) return 0f;

        float hpRatio = ally.Health.hp / Mathf.Max(1f, ally.Health.maxHp);
        return _d.priorityBase + (1f - hpRatio) * 80f;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        Unit targetAlly = FindLowestHpAlly(unit) ?? unit;

        var threat = ThreatTileData.Create();
        threat.shape = ThreatShape.LINE;
        threat.range = 0;

        BeginAttackCast(unit, _d.baseDelayMs, threat,
            () =>
            {
                if (targetAlly != null && targetAlly.Health != null && targetAlly.Health.hp > 0)
                {
                    float healAmount = _d.effectAmount > 0 ? _d.effectAmount : (unit.CombatStat.magicalAttack * (_d.damageMultiplier > 0 ? _d.damageMultiplier : 1.5f));
                    targetAlly.Health.hp = Mathf.Min(targetAlly.Health.maxHp, targetAlly.Health.hp + healAmount);
                    targetAlly.UI?.ShowFloatingTextAt(new Vector3(targetAlly.position.x + 0.5f, targetAlly.position.y + 1f, 0f), "+" + healAmount.ToString("F0"), Color.green, 1.2f);
                }
            },
            () => unit.CombatState.State.skillCooldowns[_d.cooldownSlot] = ApplyCooldown(unit, _d.baseCooldown)
        );
    }
}
