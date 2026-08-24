using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class SkillAction_Curse : SkillAction
{
    readonly SkillData _d;

    public SkillAction_Curse(SkillData data)
    {
        _d = data;
        SkillName = string.IsNullOrEmpty(data.skillName) ? "저주" : data.skillName;
    }

    public override float DefaultBaseDelayMs  => _d.baseDelayMs;
    public override float DefaultBaseCooldown => _d.baseCooldown;

    public override ThreatShape HitShape => _d.hitShape == "RECT" ? ThreatShape.RECT : ThreatShape.LINE;
    public override int HitRange => _d.hitRange > 0 ? _d.hitRange : (_d.threatRange > 0 ? _d.threatRange : 4);
    public override int HitWidth => _d.hitWidth > 0 ? _d.hitWidth : (_d.threatWidth > 0 ? _d.threatWidth : 1);
    public override int HitDepth => _d.hitDepth > 0 ? _d.hitDepth : (_d.threatDepth > 0 ? _d.threatDepth : 1);

    // 이름은 디버프지만 대상은 적이고(Affinity=Enemy), 판정도 시전자 기준 전방 히트박스다
    // (Origin=SelfArea, Execute의 GetEnemiesInHitbox) — 즉 일반 공격과 완전히 같은 조합이라
    // 기본값 그대로면 된다. 효과가 피해가 아니라 스탯 감소라는 것은 타겟팅과는 다른 축의 이야기다.

    public override bool IsAvailable(Unit unit)
    {
        if (unit == null || unit.CombatState.State.skillCooldowns == null) return false;
        if (_d.cooldownSlot < 0 || _d.cooldownSlot >= unit.CombatState.State.skillCooldowns.Length) return false;
        return unit.CombatState.State.skillCooldowns[_d.cooldownSlot] <= 0f;
    }

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        float p = _d.priorityBase;
        if (target != null && target.CombatStat != null && target.CombatStat.physicalAttack >= 15f)
            p += 20f;
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
            threat.range = _d.threatRange > 0 ? _d.threatRange : 4;
        }

        float duration = _d.effectDuration > 0 ? _d.effectDuration : 5.0f;

        GameObject sharedHitSpark = unit.Generate?.GetVisualDefinition(unit)?.hitSparkPrefab;
        BeginAttackCast(unit, _d.baseDelayMs, threat,
            () =>
            {
                var enemies = GetEnemiesInHitbox(unit, threat.hitbox);
                var targets = new List<Unit>(enemies);
                foreach (var enemy in targets)
                {
                    if (enemy == null || enemy.Health == null || enemy.Health.hp <= 0 || enemy.CombatStat == null) continue;

                    float debuffAtk = _d.effectAmount > 1.0f
                        ? Mathf.Min(enemy.CombatStat.physicalAttack, _d.effectAmount)
                        : (_d.effectAmount > 0f ? enemy.CombatStat.physicalAttack * _d.effectAmount : Mathf.Min(enemy.CombatStat.physicalAttack, 10f));

                    float debuffDef = _d.effectAmount > 1.0f
                        ? Mathf.Min(enemy.CombatStat.physicalDefense, _d.effectAmount)
                        : (_d.effectAmount > 0f ? enemy.CombatStat.physicalDefense * _d.effectAmount : Mathf.Min(enemy.CombatStat.physicalDefense, 10f));

                    enemy.CombatStat.physicalAttack = Mathf.Max(0f, enemy.CombatStat.physicalAttack - debuffAtk);
                    enemy.CombatStat.physicalDefense = Mathf.Max(0f, enemy.CombatStat.physicalDefense - debuffDef);

                    float magicDmg = unit != null && unit.CombatStat != null ? unit.CombatStat.magicalAttack * (_d.damageMultiplier > 0 ? _d.damageMultiplier : 1.0f) : 0f;
                    enemy.TakeMagicalDamage(magicDmg, unit);
                    if (_d.hasStun) enemy.ApplyStun(_d.stunDuration);

                    // hitEffectPrefab을 대상 위치에 스폰(2026-08-24 사용자 신고 "HitEffectPrefab에 있는
                    // VFX가 투사체가 아닌 스킬에는 실행되지 않는 문제" — 저주는 이 필드를 전혀 쓰지
                    // 않고 있었다). 유닛 공용 피격 스파크와 같은 프리팹이면 중복 렌더링을 피한다.
                    if (_d.hitEffectPrefab != null && _d.hitEffectPrefab != sharedHitSpark)
                        unit.VFX?.Spawn(_d.hitEffectPrefab, enemy);

                    enemy.UI?.ShowFloatingTextAt(new Vector3(enemy.position.x + 0.5f, enemy.position.y + 1f, 0f), "저주!", Color.magenta, 1.2f);
                    RollbackCurseAsync(enemy, debuffAtk, debuffDef, duration).Forget();
                }
            },
            () => unit.CombatState.State.skillCooldowns[_d.cooldownSlot] = ApplyCooldown(unit, _d.baseCooldown)
        );
    }

    private async UniTaskVoid RollbackCurseAsync(Unit target, float debuffAtk, float debuffDef, float duration)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(duration));
        if (target != null && target.Health != null && target.Health.hp > 0 && target.CombatStat != null)
        {
            target.CombatStat.physicalAttack += debuffAtk;
            target.CombatStat.physicalDefense += debuffDef;
        }
    }
}
