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

    // 시전자 앞이 아니라 대상이 서 있는 좌표에 판정을 만든다 — 전방 히트박스가 아니라 대상까지의
    // 거리로 사거리를 따진다(파이어볼도 GroundAoE 파생이라 그대로 적용된다).
    public override SkillOrigin Origin => SkillOrigin.TargetArea;

    // AI(CombatFSMState)가 원거리 사거리 내 적을 감지할 수 있도록 [너비 x 사거리] 히트박스 반환
    public override Hitbox BuildSkillHitbox(Unit unit)
    {
        int width = _d.threatWidth > 0 ? _d.threatWidth : (_d.hitWidth > 0 ? _d.hitWidth : 3);
        int range = HitRange > 0 ? HitRange : 6;
        return BuildRectHitboxWithAngle(unit, width, range, unit.CombatState.State.currentAttackAngle);
    }

    public override bool IsAvailable(Unit unit) => IsCooldownReady(unit, _d.cooldownSlot);

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

        // 2026-08-23: Debug.Log 직접 호출이었는데, 파이어볼이 실제로 이 경로를 타게 되면서 매 시전마다
        // 콘솔에 찍히게 됐다 — 프로젝트 로깅 체계(LogHelper)로 낮춘다.
        Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME,
            $"[GroundAoE/{SkillName}] 시전 시작 — 시전자: {unit.unitType?.typeName}, 착탄 좌표: {targetCenter}, " +
            $"범위: {aoeSize}x{aoeSize}, 착탄까지: {_d.baseDelayMs}ms");

        // 좌표(targetCenter)와 위협 타일은 지금 확정되고, 실제 범위 판정/피해는 baseDelayMs 뒤에 실행된다
        // — "좌표 선택 → 예고 → 낙하"라는 메테오 흐름이 여기서 성립한다(2026-08-23 사용자 확정).
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
