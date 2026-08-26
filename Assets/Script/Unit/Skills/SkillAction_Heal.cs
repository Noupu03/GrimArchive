using UnityEngine;

public class SkillAction_Heal : SkillAction
{
    readonly SkillData _d;

    public SkillAction_Heal(SkillData data)
    {
        _d = data;
        SkillName = string.IsNullOrEmpty(data.skillName) ? "치유" : data.skillName;
    }

    public override float DefaultBaseDelayMs  => _d.baseDelayMs;
    public override float DefaultBaseCooldown => _d.baseCooldown;

    public override ThreatShape HitShape => ThreatShape.LINE;
    public override int HitRange => _d.hitRange > 0 ? _d.hitRange : (_d.threatRange > 0 ? _d.threatRange : 5);
    public override int HitWidth => 1;
    public override int HitDepth => 1;

    // 아군 대상 — 적이 사거리 안에 있는지와 무관하게 발동한다. (Origin은 기본값 SelfArea:
    // 시전자 기준 사거리 안에서 대상을 찾는다.)
    public override SkillAffinity Affinity => SkillAffinity.Ally;

    public override Unit ResolveTarget(Unit unit, Unit nearestEnemy) => GetHealTarget(unit);

    // ─── 대상 탐색 1틱 캐시 ────────────────────────────────────────────
    // IsAvailable → GetPriority → ResolveTarget이 한 틱에 연달아 같은 걸 묻기 때문에, 캐시가 없으면
    // Session.units 전체를 세 번 훑는다. 주의: SkillAction 인스턴스는 같은 유닛 타입 전체가 공유하므로
    // (사제가 여럿이면 이 객체 하나를 나눠 쓴다) 소유자까지 키에 넣어야 서로의 대상이 섞이지 않는다.
    private Unit _cacheOwner;
    private int  _cacheFrame = -1;
    private Unit _cachedAlly;

    private Unit GetHealTarget(Unit unit)
    {
        if (unit == null) return null;
        if (_cacheOwner == unit && _cacheFrame == Time.frameCount) return _cachedAlly;

        _cachedAlly = FindLowestHpAlly(unit);
        _cacheOwner = unit;
        _cacheFrame = Time.frameCount;
        return _cachedAlly;
    }

    public Unit FindLowestHpAlly(Unit unit, int maxRange = -1)
    {
        if (unit == null || unit.Session == null || unit.Session.units == null) return unit;

        Unit lowestAlly = null;
        float lowestHpRatio = 1.0f;
        int range = maxRange >= 0 ? maxRange : (HitRange > 0 ? HitRange : 5);

        foreach (var u in unit.Session.units)
        {
            if (u == null || u.Health == null || u.Health.hp <= 0 || u.currentFloor != unit.currentFloor) continue;
            if (unit.IsEnemy(u)) continue;

            float dist = Vector2.Distance(unit.position, u.position);
            if (dist > range) continue;

            float hpRatio = u.Health.hp / Mathf.Max(1f, u.Health.maxHp);
            if (hpRatio < lowestHpRatio)
            {
                lowestHpRatio = hpRatio;
                lowestAlly = u;
            }
        }

        if (unit.Health != null)
        {
            float selfRatio = unit.Health.hp / Mathf.Max(1f, unit.Health.maxHp);
            if (selfRatio < lowestHpRatio)
            {
                lowestAlly = unit;
            }
        }

        return lowestAlly;
    }

    public override bool IsAvailable(Unit unit)
    {
        if (!IsCooldownReady(unit, _d.cooldownSlot)) return false;

        Unit lowest = GetHealTarget(unit);
        return lowest != null && lowest.Health != null && lowest.Health.hp < lowest.Health.maxHp;
    }

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        Unit ally = GetHealTarget(unit);
        if (ally == null || ally.Health == null || ally.Health.hp >= ally.Health.maxHp) return 0f;

        float hpRatio = ally.Health.hp / Mathf.Max(1f, ally.Health.maxHp);
        return _d.priorityBase + (1f - hpRatio) * 80f;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        // AI 경로에서는 ResolveTarget이 찾아 넘겨준 아군이 그대로 들어온다(탐색 재실행 없음).
        // 테스트처럼 직접 호출해 적을 넘기는 경우가 있으므로, 아군이 아니면 스스로 다시 찾는다.
        Unit targetAlly = (target != null && target.Health != null && target.Health.hp > 0 && !unit.IsEnemy(target))
            ? target
            : (GetHealTarget(unit) ?? unit);

        var threat = ThreatTileData.Create();
        threat.shape = ThreatShape.LINE;
        threat.range = 0;

        BeginAttackCast(unit, _d.baseDelayMs, threat,
            () =>
            {
                if (targetAlly != null && targetAlly.Health != null && targetAlly.Health.hp > 0)
                {
                    float healAmount = _d.effectAmount > 0 ? _d.effectAmount : (unit.CombatStat.magicalAttack * (_d.damageMultiplier > 0 ? _d.damageMultiplier : 1.5f));
                    targetAlly.Health.hp = Mathf.Min(targetAlly.Health.maxHp, targetAlly.Health.hp + healAmount);
                    targetAlly.UI?.ShowFloatingTextAt(new Vector3(targetAlly.position.x + 0.5f, targetAlly.position.y + 1f, 0f), "+" + healAmount.ToString("F0"), Color.green, 1.2f);

                    // 회복 이펙트 — 치유받은 아군 위치에 터진다(2026-08-23 추가). 그전까지는 데이터에
                    // hitEffectPrefab이 지정돼 있어도 아무 데서도 쓰지 않아 초록 숫자만 떴다.
                    if (_d.hitEffectPrefab != null)
                        unit.VFX?.Spawn(_d.hitEffectPrefab, targetAlly);
                }
            },
            () => unit.CombatState.State.skillCooldowns[_d.cooldownSlot] = ApplyCooldown(unit, _d.baseCooldown)
        );
    }
}
