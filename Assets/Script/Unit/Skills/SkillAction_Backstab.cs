using System.Collections.Generic;
using UnityEngine;

public class SkillAction_Backstab : SkillAction
{
    readonly SkillData _d;

    public SkillAction_Backstab(SkillData data)
    {
        _d = data;
        SkillName = string.IsNullOrEmpty(data.skillName) ? "기습" : data.skillName;
    }

    public override float DefaultBaseDelayMs  => _d.baseDelayMs;
    public override float DefaultBaseCooldown => _d.baseCooldown;

    public override ThreatShape HitShape => _d.hitShape == "RECT" ? ThreatShape.RECT : ThreatShape.LINE;
    public override int HitRange => _d.hitRange > 0 ? _d.hitRange : (_d.threatRange > 0 ? _d.threatRange : 1);
    public override int HitWidth => _d.hitWidth > 0 ? _d.hitWidth : (_d.threatWidth > 0 ? _d.threatWidth : 1);
    public override int HitDepth => _d.hitDepth > 0 ? _d.hitDepth : (_d.threatDepth > 0 ? _d.threatDepth : 1);

    public override bool IsAvailable(Unit unit)
    {
        if (unit == null || unit.CombatState.State.skillCooldowns == null) return false;
        if (_d.cooldownSlot < 0 || _d.cooldownSlot >= unit.CombatState.State.skillCooldowns.Length) return false;
        return unit.CombatState.State.skillCooldowns[_d.cooldownSlot] <= 0f;
    }

    public static bool IsBackstab(Unit attacker, Unit target)
    {
        if (attacker == null || target == null) return false;

        Vector2 targetForward = target.GetDirVector(target.currentDir);
        Vector2 attackerForward = attacker.GetDirVector(attacker.currentDir);
        Vector2 toTarget = target.position != attacker.position
            ? ((Vector2)(target.position - attacker.position)).normalized
            : Vector2.zero;

        float posDot = Vector2.Dot(toTarget, targetForward);

        // If attacker is directly in front of target, it can never be a backstab
        if (posDot < -0.3f) return false;

        bool isDirAligned = attacker.currentDir == target.currentDir || Vector2.Dot(attackerForward, targetForward) > 0.3f;

        // Attacker positioned behind target
        if (posDot > 0.3f)
        {
            if (isDirAligned || posDot > 0.5f) return true;
        }

        // Orientations aligned and not in front
        if (isDirAligned && posDot >= -0.1f) return true;

        return false;
    }

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        float p = _d.priorityBase;
        bool isBackstab = IsBackstab(unit, target);
        if (isBackstab) p += 40f;

        float critMult = isBackstab ? (_d.effectAmount > 0f ? _d.effectAmount : 2.0f) : 1.0f;
        float baseDmg = _d.damageMultiplier > 0 ? _d.damageMultiplier : 1.0f;
        if (target != null && target.Health != null && unit.CombatStat.physicalAttack * baseDmg * critMult * _d.priorityKillMultiplier >= target.Health.hp)
            p += _d.priorityKillBonus;
        if (minDist <= _d.priorityRangeThreshold)
            p += _d.priorityRangeBonus;
        return p;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        var threat = ThreatTileData.Create();
        threat.shape = HitShape;
        if (HitShape == ThreatShape.RECT)
        {
            threat.width = _d.threatWidth > 0 ? _d.threatWidth : 1;
            threat.depth = _d.threatDepth > 0 ? _d.threatDepth : 1;
        }
        else
        {
            threat.range = _d.threatRange > 0 ? _d.threatRange : 1;
        }

        BeginAttackCast(unit, _d.baseDelayMs, threat,
            () =>
            {
                var enemies = GetEnemiesInHitbox(unit, threat.hitbox);
                var targets = new List<Unit>(enemies);
                foreach (var t in targets)
                {
                    if (t == null || t.Health == null || t.Health.hp <= 0) continue;
                    bool backstab = IsBackstab(unit, t);
                    float critMult = backstab ? (_d.effectAmount > 0f ? _d.effectAmount : 2.0f) : 1.0f;
                    float finalMultiplier = (_d.damageMultiplier > 0 ? _d.damageMultiplier : 1.0f) * critMult;

                    t.TakePhysicalDamage(unit.CombatStat.physicalAttack * finalMultiplier, unit);
                    if (_d.hasStun) t.ApplyStun(_d.stunDuration);

                    if (backstab)
                    {
                        unit.UI?.ShowFloatingTextAt(new Vector3(t.position.x + 0.5f, t.position.y + 1f, 0f), "CRITICAL BACKSTAB!", Color.red, 0.8f);
                    }
                }
            },
            () => unit.CombatState.State.skillCooldowns[_d.cooldownSlot] = ApplyCooldown(unit, _d.baseCooldown)
        );
    }
}
