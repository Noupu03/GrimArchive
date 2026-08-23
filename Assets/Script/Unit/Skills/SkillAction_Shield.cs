using System;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class SkillAction_Shield : SkillAction
{
    readonly SkillData _d;

    public SkillAction_Shield(SkillData data)
    {
        _d = data;
        SkillName = string.IsNullOrEmpty(data.skillName) ? "성스러운 방패" : data.skillName;
    }

    public override float DefaultBaseDelayMs  => _d.baseDelayMs;
    public override float DefaultBaseCooldown => _d.baseCooldown;

    public override ThreatShape HitShape => ThreatShape.LINE;
    public override int HitRange => _d.hitRange > 0 ? _d.hitRange : (_d.threatRange > 0 ? _d.threatRange : 4);
    public override int HitWidth => 1;
    public override int HitDepth => 1;

    public Unit FindShieldTarget(Unit unit, int maxRange = -1)
    {
        if (unit == null || unit.Session == null || unit.Session.units == null) return unit;

        Unit bestTarget = unit;
        float lowestHpRatio = unit.Health != null ? (unit.Health.hp / Mathf.Max(1f, unit.Health.maxHp)) : 1f;
        int range = maxRange >= 0 ? maxRange : (HitRange > 0 ? HitRange : 4);

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
                bestTarget = u;
            }
        }
        return bestTarget;
    }

    public override bool IsAvailable(Unit unit)
    {
        if (unit == null || unit.CombatState.State.skillCooldowns == null) return false;
        if (_d.cooldownSlot < 0 || _d.cooldownSlot >= unit.CombatState.State.skillCooldowns.Length) return false;
        return unit.CombatState.State.skillCooldowns[_d.cooldownSlot] <= 0f;
    }

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        float p = _d.priorityBase;
        if (minDist <= _d.priorityRangeThreshold) p += _d.priorityRangeBonus;
        return p;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        Unit targetAlly = FindShieldTarget(unit) ?? unit;

        var threat = ThreatTileData.Create();
        threat.shape = ThreatShape.LINE;
        threat.range = 0;

        float boost = _d.effectAmount > 0 ? _d.effectAmount : 40f;
        float duration = _d.effectDuration > 0 ? _d.effectDuration : 5.0f;

        BeginAttackCast(unit, _d.baseDelayMs, threat,
            () =>
            {
                if (targetAlly != null && targetAlly.Health != null && targetAlly.Health.hp > 0)
                {
                    targetAlly.Health.maxHp += boost;
                    targetAlly.Health.hp += boost;
                    targetAlly.UI?.ShowFloatingTextAt(new Vector3(targetAlly.position.x + 0.5f, targetAlly.position.y + 1f, 0f), "보호막 +" + boost.ToString("F0"), Color.cyan, 1.2f);
                    ApplyShieldRollbackAsync(targetAlly, boost, duration).Forget();
                }
            },
            () => unit.CombatState.State.skillCooldowns[_d.cooldownSlot] = ApplyCooldown(unit, _d.baseCooldown)
        );
    }

    private async UniTaskVoid ApplyShieldRollbackAsync(Unit target, float boost, float duration)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(duration));
        if (target != null && target.Health != null && target.Health.hp > 0)
        {
            target.Health.maxHp = Mathf.Max(1f, target.Health.maxHp - boost);
            target.Health.hp = Mathf.Min(target.Health.hp, target.Health.maxHp);
        }
    }
}
