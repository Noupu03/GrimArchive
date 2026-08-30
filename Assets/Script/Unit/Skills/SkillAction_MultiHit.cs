using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class SkillAction_MultiHit : SkillAction
{
    readonly SkillData _d;

    public SkillAction_MultiHit(SkillData data)
    {
        _d = data;
        SkillName = string.IsNullOrEmpty(data.skillName) ? "연격" : data.skillName;
    }

    public override float DefaultBaseDelayMs  => _d.baseDelayMs;
    public override float DefaultBaseCooldown => _d.baseCooldown;

    public override ThreatShape HitShape => _d.hitShape == "RECT" ? ThreatShape.RECT : ThreatShape.LINE;
    public override int HitRange => _d.hitRange > 0 ? _d.hitRange : (_d.threatRange > 0 ? _d.threatRange : 1);
    public override int HitWidth => _d.hitWidth > 0 ? _d.hitWidth : (_d.threatWidth > 0 ? _d.threatWidth : 1);
    public override int HitDepth => _d.hitDepth > 0 ? _d.hitDepth : (_d.threatDepth > 0 ? _d.threatDepth : 1);

    public override bool IsAvailable(Unit unit) => IsCooldownReady(unit, _d.cooldownSlot);

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        float p = _d.priorityBase;
        int count = _d.multiHitCount > 0 ? _d.multiHitCount : 3;
        float mult = (_d.damageMultiplier > 0 ? _d.damageMultiplier : 0.6f) * count;
        if (target != null && target.Health != null && unit.CombatStat.physicalAttack * mult * _d.priorityKillMultiplier >= target.Health.hp)
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

        int count = _d.multiHitCount > 0 ? _d.multiHitCount : 3;
        float multiplier = _d.damageMultiplier > 0 ? _d.damageMultiplier : 0.6f;

        BeginAttackCast(unit, _d.baseDelayMs, threat,
            () =>
            {
                // hitEffectPrefab을 실제로 맞은 대상 각각의 위치에 스폰한다 — SkillAction_Generic과
                // 동일하게 유닛 공용 피격 스파크와 같은 프리팹이면 중복 렌더링을 피해 건너뛴다.
                GameObject sharedHitSpark = unit.Generate?.GetVisualDefinition(unit)?.hitSparkPrefab;
                DamageEnemiesInHitboxWithAreaRatio(unit, threat.hitbox, multiplier, _d.hasStun, _d.stunDuration,
                    onHit: t =>
                    {
                        if (_d.hitEffectPrefab != null && _d.hitEffectPrefab != sharedHitSpark)
                            unit.VFX?.Spawn(_d.hitEffectPrefab, t);
                    });

                if (count > 1)
                {
                    ExecuteSubsequentHitsAsync(unit, target, count, multiplier, threat).Forget();
                }
            },
            () => unit.CombatState.State.skillCooldowns[_d.cooldownSlot] = ApplyCooldown(unit, _d.baseCooldown)
        );
    }

    private async UniTaskVoid ExecuteSubsequentHitsAsync(Unit attacker, Unit target, int count, float multiplier, ThreatTileData threat)
    {
        for (int i = 1; i < count; i++)
        {
            await UniTask.Delay(120);
            if (attacker == null || attacker.Health == null || attacker.Health.hp <= 0)
                break;

            if (target != null && target.Health != null && target.Health.hp > 0)
            {
                target.TakePhysicalDamage(attacker.CombatStat.physicalAttack * multiplier, attacker);
                if (_d.hasStun) target.ApplyStun(_d.stunDuration);
                // TakePhysicalDamage → TakeDamage가 내부적으로 이미 TriggerHitEffect(공용 히트
                // 스파크)를 호출하므로 별도로 다시 호출하면 중복 재생된다 — 대신 위 첫 타격과
                // 동일하게 스킬 전용 hitEffectPrefab만 대상 위치에 스폰한다.
                GameObject sharedHitSpark = attacker.Generate?.GetVisualDefinition(attacker)?.hitSparkPrefab;
                if (_d.hitEffectPrefab != null && _d.hitEffectPrefab != sharedHitSpark)
                    attacker.VFX?.Spawn(_d.hitEffectPrefab, target);
                PropagationSystem.EmitSound(attacker.Session, SoundType.AttackExecution, attacker.position, attacker.currentFloor, attacker);
            }
            else
            {
                GameObject sharedHitSpark = attacker.Generate?.GetVisualDefinition(attacker)?.hitSparkPrefab;
                DamageEnemiesInHitboxWithAreaRatio(attacker, threat.hitbox, multiplier, _d.hasStun, _d.stunDuration,
                    onHit: t =>
                    {
                        if (_d.hitEffectPrefab != null && _d.hitEffectPrefab != sharedHitSpark)
                            attacker.VFX?.Spawn(_d.hitEffectPrefab, t);
                    });
            }
        }
    }
}
