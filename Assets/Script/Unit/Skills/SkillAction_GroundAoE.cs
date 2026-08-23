using System.Collections.Generic;
using UnityEngine;

public class SkillAction_GroundAoE : SkillAction
{
    protected readonly SkillData _d;

    public SkillAction_GroundAoE(SkillData data)
    {
        _d = data;
        SkillName = string.IsNullOrEmpty(data.skillName) ? "GroundAoE" : data.skillName;
    }

    public override float DefaultBaseDelayMs  => _d.baseDelayMs;
    public override float DefaultBaseCooldown => _d.baseCooldown;

    public override ThreatShape HitShape => ThreatShape.RECT;
    public override int HitRange => _d.threatRange > 0 ? _d.threatRange : (_d.hitRange > 0 ? _d.hitRange : 6);
    public override int HitWidth => _d.threatWidth > 0 ? _d.threatWidth : (_d.hitWidth > 0 ? _d.hitWidth : 3);
    public override int HitDepth => _d.threatDepth > 0 ? _d.threatDepth : (_d.hitDepth > 0 ? _d.hitDepth : 3);

    // AI(CombatFSMState)가 원거리 사거리 내 적을 감지할 수 있도록 [너비 x 사거리] 히트박스 반환
    public override Hitbox BuildSkillHitbox(Unit unit)
    {
        int width = _d.threatWidth > 0 ? _d.threatWidth : (_d.hitWidth > 0 ? _d.hitWidth : 3);
        int range = HitRange > 0 ? HitRange : 6;
        return BuildRectHitboxWithAngle(unit, width, range, unit.CombatState.State.currentAttackAngle);
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
        float dmgMult = _d.damageMultiplier > 0 ? _d.damageMultiplier : 1.5f;
        if (target != null && target.Health != null && unit.CombatStat.magicalAttack * dmgMult * _d.priorityKillMultiplier >= target.Health.hp)
            p += _d.priorityKillBonus;
        if (minDist <= _d.priorityRangeThreshold)
            p += _d.priorityRangeBonus;
        return p;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        float radius = _d.explosionRadius > 0 ? _d.explosionRadius : 1.5f;
        int aoeSize = Mathf.Max(1, Mathf.RoundToInt(radius * 2));

        Vector2 targetCenter;
        if (target != null)
        {
            Vector2 footprint = target.unitType != null ? target.unitType.footprint : Vector2.one;
            targetCenter = (Vector2)target.position + footprint * 0.5f;
        }
        else
        {
            Vector2 forward = unit.GetDirVector(unit.currentDir);
            targetCenter = (Vector2)unit.position + forward * (_d.threatRange > 0 ? _d.threatRange : 3f) + Vector2.one * 0.5f;
        }

        var threat = ThreatTileData.Create();
        threat.shape = ThreatShape.RECT;
        threat.width = aoeSize;
        threat.depth = aoeSize;
        threat.range = _d.threatRange > 0 ? _d.threatRange : 6;
        threat.hitbox = new Hitbox
        {
            center = targetCenter,
            size = new Vector2(aoeSize, aoeSize),
            rotation = 0f
        };

        // 1번 디버깅 및 상태 확인용 로그 (콘솔에서 확인 가능)
        Debug.Log($"[GroundAoE/{SkillName}] 시전 시작! 시전자: {unit.unitType?.typeName}, 대상 위치: {targetCenter}, 반경: {aoeSize}x{aoeSize}, SessionNull: {unit.Session == null}");

        BeginAttackCast(unit, _d.baseDelayMs, threat,
            () =>
            {
                DamageMagicalEnemiesInHitboxWithAreaRatio(unit, threat.hitbox, _d.damageMultiplier > 0 ? _d.damageMultiplier : 1.5f, _d.hasStun, _d.stunDuration);
                unit.UI?.ShowFloatingTextAt(new Vector3(targetCenter.x, targetCenter.y + 0.5f, 0f), "BOOM!", Color.red, 0.8f);

                // 폭발 파티클 (VFX_Fire / VFX_Burst 등) 스폰
                if (_d.hitEffectPrefab != null)
                {
                    Vector3 floorOffset = unit.Generate != null ? unit.Generate.GetFloorOffset(unit.currentFloor) : Vector3.zero;
                    VFXManager.Spawn(_d.hitEffectPrefab, new Vector3(targetCenter.x, targetCenter.y, 0f) + floorOffset, Quaternion.identity);
                }
            },
            () => unit.CombatState.State.skillCooldowns[_d.cooldownSlot] = ApplyCooldown(unit, _d.baseCooldown),
            shape: AttackShape.AreaGround
        );
    }
}
