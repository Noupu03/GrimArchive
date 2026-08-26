using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class SkillAction_PartyBuff : SkillAction
{
    readonly SkillData _d;

    public SkillAction_PartyBuff(SkillData data)
    {
        _d = data;
        SkillName = string.IsNullOrEmpty(data.skillName) ? "전투의 노래" : data.skillName;
    }

    public override float DefaultBaseDelayMs  => _d.baseDelayMs;
    public override float DefaultBaseCooldown => _d.baseCooldown;

    public override ThreatShape HitShape => ThreatShape.LINE;
    public override int HitRange => 0;
    public override int HitWidth => 1;
    public override int HitDepth => 1;

    // 아군 전체 대상 — 적이 사거리 안에 있는지와 무관하게 발동한다.
    public override SkillAffinity Affinity => SkillAffinity.Ally;

    // 특정 한 명이 아니라 같은 층 아군 전체가 대상이라, 사거리 판정의 기준점으로 시전자 자신을 돌려준다
    // (거리 0이므로 항상 통과한다). 실제 대상 순회는 Execute가 한다.
    public override Unit ResolveTarget(Unit unit, Unit nearestEnemy) => unit;

    public override bool IsAvailable(Unit unit) => IsCooldownReady(unit, _d.cooldownSlot);

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        float p = _d.priorityBase;
        if (minDist <= _d.priorityRangeThreshold) p += _d.priorityRangeBonus;
        return p;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        var threat = ThreatTileData.Create();
        threat.shape = ThreatShape.LINE;
        threat.range = 0;

        float duration = _d.effectDuration > 0 ? _d.effectDuration : 8.0f;

        BeginAttackCast(unit, _d.baseDelayMs, threat,
            () =>
            {
                var applied = new Dictionary<Unit, (float pAtk, float mAtk)>();
                if (unit.Session != null && unit.Session.units != null)
                {
                    foreach (var u in unit.Session.units)
                    {
                        if (u == null || u.Health == null || u.Health.hp <= 0 || u.currentFloor != unit.currentFloor || u.CombatStat == null) continue;
                        if (unit.IsEnemy(u)) continue;

                        float pBonus = _d.effectAmount > 1.0f ? _d.effectAmount : (_d.effectAmount > 0f ? u.CombatStat.physicalAttack * _d.effectAmount : 10f);
                        float mBonus = _d.effectAmount > 1.0f ? _d.effectAmount : (_d.effectAmount > 0f ? u.CombatStat.magicalAttack * _d.effectAmount : 10f);

                        u.CombatStat.physicalAttack += pBonus;
                        u.CombatStat.magicalAttack += mBonus;

                        applied[u] = (pBonus, mBonus);
                        u.UI?.ShowFloatingTextAt(new Vector3(u.position.x + 0.5f, u.position.y + 1f, 0f), "공격력 증가!", Color.cyan, 1.2f);

                        // 버프 이펙트 — 버프받은 아군 각자의 위치에 터진다(2026-08-23 추가).
                        if (_d.hitEffectPrefab != null)
                            unit.VFX?.Spawn(_d.hitEffectPrefab, u);
                    }
                }
                else
                {
                    if (unit.CombatStat != null)
                    {
                        float pBonus = _d.effectAmount > 1.0f ? _d.effectAmount : (_d.effectAmount > 0f ? unit.CombatStat.physicalAttack * _d.effectAmount : 10f);
                        float mBonus = _d.effectAmount > 1.0f ? _d.effectAmount : (_d.effectAmount > 0f ? unit.CombatStat.magicalAttack * _d.effectAmount : 10f);

                        unit.CombatStat.physicalAttack += pBonus;
                        unit.CombatStat.magicalAttack += mBonus;
                        applied[unit] = (pBonus, mBonus);
                        unit.UI?.ShowFloatingTextAt(new Vector3(unit.position.x + 0.5f, unit.position.y + 1f, 0f), "공격력 증가!", Color.cyan, 1.2f);
                    }
                }

                RollbackPartyBuffAsync(applied, duration).Forget();
            },
            () => unit.CombatState.State.skillCooldowns[_d.cooldownSlot] = ApplyCooldown(unit, _d.baseCooldown)
        );
    }

    private async UniTaskVoid RollbackPartyBuffAsync(Dictionary<Unit, (float pAtk, float mAtk)> applied, float duration)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(duration));
        foreach (var kvp in applied)
        {
            var ally = kvp.Key;
            if (ally != null && ally.Health != null && ally.Health.hp > 0 && ally.CombatStat != null)
            {
                ally.CombatStat.physicalAttack = Mathf.Max(0f, ally.CombatStat.physicalAttack - kvp.Value.pAtk);
                ally.CombatStat.magicalAttack = Mathf.Max(0f, ally.CombatStat.magicalAttack - kvp.Value.mAtk);
            }
        }
    }
}
