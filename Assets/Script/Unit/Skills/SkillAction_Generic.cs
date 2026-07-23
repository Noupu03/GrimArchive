using UnityEngine;

public class SkillAction_Generic : SkillAction
{
    readonly SkillData _d;

    public SkillAction_Generic(SkillData data)
    {
        _d        = data;
        SkillName = data.skillName;
    }

    public override float DefaultBaseDelayMs  => _d.baseDelayMs;
    public override float DefaultBaseCooldown => _d.baseCooldown;

    public override ThreatShape HitShape => _d.hitShape == "RECT" ? ThreatShape.RECT : ThreatShape.LINE;
    public override int HitRange => _d.hitRange;
    public override int HitWidth => _d.hitWidth;
    public override int HitDepth => _d.hitDepth;

    public override bool IsAvailable(Unit unit) => unit.GetComponent<CombatStateComponent>().State.skillCooldowns[_d.cooldownSlot] <= 0f;

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        float p = _d.priorityBase;
        if (unit.GetComponent<CombatStatComponent>().physicalAttack * _d.priorityKillMultiplier >= target.GetComponent<HealthComponent>().hp) p += _d.priorityKillBonus;
        if (minDist <= _d.priorityRangeThreshold)                         p += _d.priorityRangeBonus;
        return p;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        float finalDelayMs = Mathf.Max(200f, _d.baseDelayMs * (100f / Mathf.Max(1f, unit.GetComponent<CombatStatComponent>().attackspeed)));

        var threat = ThreatTileData.Create();
        threat.shape = HitShape;
        if (HitShape == ThreatShape.RECT)
        {
            threat.width = _d.threatWidth;
            threat.depth = _d.threatDepth;
        }
        else
        {
            threat.range = _d.threatRange;
        }

        BeginAttackCast(unit, finalDelayMs, threat,
            () =>
            {
                DamageEnemiesInHitboxWithAreaRatio(unit, threat.hitbox,
                    _d.damageMultiplier, _d.hasStun, _d.stunDuration);
            },
            () => unit.GetComponent<CombatStateComponent>().State.skillCooldowns[_d.cooldownSlot] = ApplyCooldown(unit, _d.baseCooldown)
        );
    }
}
