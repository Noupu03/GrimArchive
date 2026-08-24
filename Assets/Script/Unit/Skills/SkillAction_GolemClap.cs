using UnityEngine;

// 보스 골렘 공격3(2026-08-24, 기본 구조): 단일 대상을 양손으로 박수치듯 협공한다. 구조는 GolemSlam과
// 거의 같지만(대상 좌표 기준 지연 착탄), 데미지 배율이 더 높고(양손) 연출이 좌우 협공이라는 점만 다르다.
public class SkillAction_GolemClap : SkillAction
{
    private readonly SkillData _d;

    public SkillAction_GolemClap(SkillData data)
    {
        _d = data;
        SkillName = string.IsNullOrEmpty(data.skillName) ? "골렘 박수" : data.skillName;
    }

    public override float DefaultBaseDelayMs  => _d.baseDelayMs;
    public override float DefaultBaseCooldown => _d.baseCooldown;

    public override ThreatShape HitShape => ThreatShape.RECT;
    public override int HitRange => _d.threatRange > 0 ? _d.threatRange : 2;
    public override int HitWidth => _d.explosionRadius > 0 ? Mathf.Max(1, Mathf.RoundToInt(_d.explosionRadius * 2)) : 1;
    public override int HitDepth => HitWidth;

    public override SkillOrigin Origin => SkillOrigin.TargetArea;

    public override Hitbox BuildSkillHitbox(Unit unit)
    {
        return BuildRectHitboxWithAngle(unit, HitWidth, HitRange, unit.CombatState.State.currentAttackAngle);
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
        float dmgMult = _d.damageMultiplier > 0 ? _d.damageMultiplier : 2.5f;
        // 단일 대상 처치기 성격 — 마무리 가능하면 우선순위를 확 올린다.
        if (target != null && target.Health != null && unit.CombatStat.physicalAttack * dmgMult * _d.priorityKillMultiplier >= target.Health.hp)
            p += _d.priorityKillBonus;
        return p;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        Vector2 targetCenter;
        if (target != null)
        {
            Vector2 footprint = target.unitType != null ? target.unitType.footprint : Vector2.one;
            targetCenter = (Vector2)target.position + footprint * 0.5f;
        }
        else
        {
            Vector2 forward = unit.GetDirVector(unit.currentDir);
            targetCenter = (Vector2)unit.position + forward * HitRange + Vector2.one * 0.5f;
        }

        int aoeSize = HitWidth;
        var threat = ThreatTileData.Create();
        threat.shape = ThreatShape.RECT;
        threat.width = aoeSize;
        threat.depth = aoeSize;
        threat.range = HitRange;
        threat.hitbox = new Hitbox { center = targetCenter, size = new Vector2(aoeSize, aoeSize), rotation = 0f };

        float multiplier = _d.damageMultiplier > 0 ? _d.damageMultiplier : 2.5f; // 양손이므로 GolemSlam보다 높은 기본 배율

        BeginAttackCast(unit, _d.baseDelayMs, threat,
            attackAction: () =>
            {
                DamageEnemiesInHitboxWithAreaRatio(unit, threat.hitbox, multiplier, _d.hasStun, _d.stunDuration);
                if (_d.hitEffectPrefab != null)
                {
                    Vector3 floorOffset = unit.Generate != null ? unit.Generate.GetFloorOffset(unit.currentFloor) : Vector3.zero;
                    VFXManager.Spawn(_d.hitEffectPrefab, new Vector3(targetCenter.x, targetCenter.y, 0f) + floorOffset, Quaternion.identity);
                }
            },
            cooldownAction: () => unit.CombatState.State.skillCooldowns[_d.cooldownSlot] = ApplyCooldown(unit, _d.baseCooldown),
            shape: AttackShape.AreaGround
        );

        Vector3 handTargetWorld = new Vector3(targetCenter.x, targetCenter.y, 0f)
            + (unit.Generate != null ? unit.Generate.GetFloorOffset(unit.currentFloor) : Vector3.zero);
        BossGolemHandController.Get(unit)?.PlayClap(handTargetWorld);
    }
}
