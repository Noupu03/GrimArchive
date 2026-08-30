using UnityEngine;

// 보스 골렘 공격2: 대상 A부터 대상 B인 적까지 손을 쭉 이동하며 스치는 적들에게 피해를 입힌다. A는
// CombatFSMState가 넘겨주는 "가장 가까운 적"을 그대로 쓰고, B는 A를 기준으로 사거리(HitRange) 내에서
// 가장 먼 적을 자동으로 고른다 — 기획 문서가 선정 기준을 명시하지 않아 잡은 자리표시자 기본값이다
// (적이 하나뿐이면 그 적을 지나 반대 방향으로 더 뻗은 지점을 B로 삼는다). 바꾸려면 FindFarthestEnemyFrom만 고치면 된다.
public class SkillAction_GolemSweep : SkillAction
{
    private readonly SkillData _d;

    public SkillAction_GolemSweep(SkillData data)
    {
        _d = data;
        SkillName = string.IsNullOrEmpty(data.skillName) ? "골렘 손쓸기" : data.skillName;
    }

    public override float DefaultBaseDelayMs  => _d.baseDelayMs;
    public override float DefaultBaseCooldown => _d.baseCooldown;

    public override ThreatShape HitShape => ThreatShape.LINE;
    public override int HitRange => _d.threatRange > 0 ? _d.threatRange : 8;
    public override int HitWidth => _d.threatWidth > 0 ? _d.threatWidth : 1;

    // A~B는 시전자 전방 히트박스가 아니라 두 대상 좌표 자체로 판정을 만든다 — TargetArea로 두면
    // CanExecuteAgainst가 "대상까지의 거리"로 사거리를 판단해준다(대상 A까지의 거리).
    public override SkillOrigin Origin => SkillOrigin.TargetArea;

    public override bool IsAvailable(Unit unit) => IsCooldownReady(unit, _d.cooldownSlot);

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        float p = _d.priorityBase;
        if (CountEnemiesWithinRange(unit, HitRange) >= 2) p += _d.priorityRangeBonus; // "여러 명 걸림" 보너스 재사용
        return p;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        Unit pointA = target;
        Unit pointB = FindFarthestEnemyFrom(unit, pointA);

        Vector2 aCenter = CenterOf(pointA, unit);
        Vector2 bCenter = pointB != null
            ? CenterOf(pointB, unit)
            : aCenter + (aCenter - (Vector2)unit.position).normalized * HitRange; // 적 한 명뿐이면 그 방향으로 더 뻗는다

        var threat = ThreatTileData.Create();
        threat.shape = ThreatShape.LINE;
        threat.range = HitRange;
        threat.hitbox = BuildTwoPointLineHitbox(aCenter, bCenter, HitWidth);

        float multiplier = _d.damageMultiplier > 0 ? _d.damageMultiplier : 1.2f;

        // hitEffectPrefab을 실제로 스친 대상 각각의 위치에 스폰한다.
        GameObject sharedHitSpark = unit.Generate?.GetVisualDefinition(unit)?.hitSparkPrefab;
        BeginAttackCast(unit, _d.baseDelayMs, threat,
            attackAction: () => DamageEnemiesInHitboxWithAreaRatio(unit, threat.hitbox, multiplier, _d.hasStun, _d.stunDuration,
                onHit: t =>
                {
                    if (_d.hitEffectPrefab != null && _d.hitEffectPrefab != sharedHitSpark)
                        unit.VFX?.Spawn(_d.hitEffectPrefab, t);
                }),
            cooldownAction: () => unit.CombatState.State.skillCooldowns[_d.cooldownSlot] = ApplyCooldown(unit, _d.baseCooldown)
        );

        Vector3 floorOffset = unit.Generate != null ? unit.Generate.GetFloorOffset(unit.currentFloor) : Vector3.zero;
        BossGolemHandController.Get(unit)?.PlaySweep(
            new Vector3(aCenter.x, aCenter.y, 0f) + floorOffset,
            new Vector3(bCenter.x, bCenter.y, 0f) + floorOffset);
    }

    private static Vector2 CenterOf(Unit u, Unit fallback)
    {
        if (u == null) return fallback.position;
        Vector2 footprint = u.unitType != null ? u.unitType.footprint : Vector2.one;
        return (Vector2)u.position + footprint * 0.5f;
    }

    private static Hitbox BuildTwoPointLineHitbox(Vector2 a, Vector2 b, int width)
    {
        Vector2 diff = b - a;
        float length = Mathf.Max(1f, diff.magnitude);
        float angleDeg = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        return new Hitbox { center = (a + b) * 0.5f, size = new Vector2(length, width), rotation = angleDeg };
    }

    private static Unit FindFarthestEnemyFrom(Unit unit, Unit from)
    {
        if (unit == null || from == null) return null;

        Unit best = null;
        float bestDist = -1f;
        foreach (var e in unit.Perception.State.personalSpottedEnemies)
        {
            if (e == null || e == from || e.Health.hp <= 0 || e.currentFloor != unit.currentFloor) continue;
            float d = Vector2Int.Distance(from.position, e.position);
            if (d > bestDist) { bestDist = d; best = e; }
        }
        return best;
    }

    private static int CountEnemiesWithinRange(Unit unit, int range)
    {
        int count = 0;
        foreach (var e in unit.Perception.State.personalSpottedEnemies)
        {
            if (e == null || e.Health.hp <= 0 || e.currentFloor != unit.currentFloor) continue;
            if (Vector2Int.Distance(unit.position, e.position) <= range) count++;
        }
        return count;
    }
}
