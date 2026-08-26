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

    public override bool IsAvailable(Unit unit) => IsCooldownReady(unit, _d.cooldownSlot);

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        float p = _d.priorityBase;
        if (target != null && target.Health != null && unit.CombatStat.physicalAttack * _d.priorityKillMultiplier >= target.Health.hp) p += _d.priorityKillBonus;
        if (minDist <= _d.priorityRangeThreshold)                         p += _d.priorityRangeBonus;
        return p;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
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

        BeginAttackCast(unit, 0f, threat,
            () =>
            {
                // hitEffectPrefab이 유닛 공용 피격 스파크(UnitVisualDefinition.hitSparkPrefab)와
                // 같은 프리팹이면 TakePhysicalDamage → TriggerHitEffect가 이미 그걸 띄우므로 중복
                // 렌더링을 피하려 건너뛴다. 다르면(디자이너가 이 근접 스킬 전용 이펙트를 따로
                // 지정한 경우) 정상적으로 스폰한다 — 2026-08-24 사용자 신고 "근접 공격하는 애들
                // 스킬 이펙트 출력이 안됨" → 후속 신고 "투사체가 아닌 스킬에는 실행되지 않는 문제"
                // 수정: 예전엔 근접 계열이면 조건 없이 무조건 건너뛰어서 아예 안 떴고, 그다음엔
                // 공격자 위치에 스폰해서 히트 스파크(피격 대상 위치)와 다르게 보였다 — 이제 실제로
                // 맞은 대상 각각의 위치에 스폰한다(Projectile.ApplyHitEffect/Heal/Shield/PartyBuff와
                // 동일한 "대상 위치에 스폰" 관례).
                GameObject sharedHitSpark = unit.Generate?.GetVisualDefinition(unit)?.hitSparkPrefab;
                DamageEnemiesInHitboxWithAreaRatio(unit, threat.hitbox,
                    _d.damageMultiplier, _d.hasStun, _d.stunDuration,
                    onHit: t =>
                    {
                        if (_d.hitEffectPrefab != null && _d.hitEffectPrefab != sharedHitSpark)
                            unit.VFX?.Spawn(_d.hitEffectPrefab, t);
                    });
            },
            () => unit.CombatState.State.skillCooldowns[_d.cooldownSlot] = ApplyCooldown(unit, _d.baseCooldown)
        );
    }
}
