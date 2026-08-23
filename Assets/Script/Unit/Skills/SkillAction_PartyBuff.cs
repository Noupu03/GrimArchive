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
