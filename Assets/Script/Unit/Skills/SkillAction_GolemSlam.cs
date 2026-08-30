using UnityEngine;

// 보스 골렘 공격1 — 대상 좌표로 일반 공격. GroundAoE와 같은 "좌표에 지연 착탄" 흐름을 그대로 쓰되,
// 파티클 대신 BossGolemHandController가 손을 들어올렸다 내려찍는 연출로 대체한다. 손 애니메이션은
// Execute 시점에 곧바로 시작해 시전(castMs) 동안 진행되고, 실제 피해는 기존 BeginAttackCast 흐름대로
// castMs 경과 시점에 별도 계산된다(자세한 이유는 BossGolemHandController.cs 상단 주석 참고).
public class SkillAction_GolemSlam : SkillAction
{
    private readonly SkillData _d;

    public SkillAction_GolemSlam(SkillData data)
    {
        _d = data;
        SkillName = string.IsNullOrEmpty(data.skillName) ? "골렘 내려찍기" : data.skillName;
    }

    public override float DefaultBaseDelayMs  => _d.baseDelayMs;
    public override float DefaultBaseCooldown => _d.baseCooldown;

    public override ThreatShape HitShape => ThreatShape.RECT;
    public override int HitRange => _d.threatRange > 0 ? _d.threatRange : 6;
    public override int HitWidth => _d.explosionRadius > 0 ? Mathf.Max(1, Mathf.RoundToInt(_d.explosionRadius * 2)) : 2;
    public override int HitDepth => HitWidth;

    // 시전자 앞이 아니라 대상이 서 있는 좌표 기준(GroundAoE/Fireball과 동일).
    public override SkillOrigin Origin => SkillOrigin.TargetArea;

    public override Hitbox BuildSkillHitbox(Unit unit)
    {
        return BuildRectHitboxWithAngle(unit, HitWidth, HitRange, unit.CombatState.State.currentAttackAngle);
    }

    public override bool IsAvailable(Unit unit) => IsCooldownReady(unit, _d.cooldownSlot);

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        return _d.priorityBase;
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

        float multiplier = _d.damageMultiplier > 0 ? _d.damageMultiplier : 2f;

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
        BossGolemHandController.Get(unit)?.PlaySlam(handTargetWorld);
    }
}
