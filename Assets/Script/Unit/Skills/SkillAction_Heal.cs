using System.Collections.Generic;
using UnityEngine;

public class SkillAction_Heal : SkillAction
{
    readonly SkillData _d;

    public SkillAction_Heal(SkillData data)
    {
        _d = data;
        SkillName = string.IsNullOrEmpty(data.skillName) ? "치유" : data.skillName;
    }

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
    // IsAvailable → GetPriority → ResolveTarget이 한 틱에 연달아 같은 걸 물어 캐시가 없으면 Session.units
    // 전체를 세 번 훑는다. SkillAction 인스턴스는 같은 유닛 타입 전체가 공유하므로 소유자까지 키에
    // 넣어야 서로의 대상이 섞이지 않는다.
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

    // 02번 7장: 낮은 HP 비율 → 짧은 도달시간(거리로 근사) → 현재 대상 유지 순으로 비교하고, 자기 파티
    // 요구를 먼저 본다(자기 자신도 같은 순서의 후보). 이미 치료 중인 대상은 작은 체력 변화만으로
    // 계속 교체하지 않는다 — 현재 대상이 여전히 유효하고 치료 기준(클래스별 문턱) 이상 회복되지
    // 않았으면 그대로 유지한다. 결과는 unit.CombatTargeting.HealTarget에 반영된다.
    public Unit FindLowestHpAlly(Unit unit, int maxRange = -1)
    {
        if (unit == null || unit.Session == null || unit.Session.units == null) return unit;

        int range = maxRange >= 0 ? maxRange : (HitRange > 0 ? HitRange : 5);

        // 02번 0장: 긴급 아군 보호가 일반 치료보다 먼저다 — CombatFSMState.ResolveEmergencyProtectTarget이
        // 갱신해 둔 대상이 있고 아직 치료 여지가 있으면(만피가 아니면) 일반 치료 문턱과 무관하게 그 대상을 우선한다.
        Unit protectTarget = unit.CombatTargeting.ProtectTarget;
        if (protectTarget != null && protectTarget.Health != null && protectTarget.Health.hp > 0
            && protectTarget.Health.hp < protectTarget.Health.maxHp
            && Vector2.Distance(unit.position, protectTarget.position) <= range)
        {
            unit.CombatTargeting.HealTarget = protectTarget;
            return protectTarget;
        }

        CombatRole selfRole = CombatScoreMath.ResolveCombatRole(unit.unitType);
        float threshold = CombatScoreMath.GeneralHealThresholdRatio(selfRole);

        // 현재 대상 유지 — 사망/층 이동/회복 완료(문턱 이상)로 무효화되지 않았으면 그대로 쓴다.
        // [정정, 검증문서 02-07 4·5번] 사거리 이탈은 "위치 무효"에 포함하지 않는다 — 사거리 밖이라도
        // 계속 같은 대상으로 접근할 수 있어야 하므로, 여기서 범위 조건을 빼서 "지금 실행 가능한가"
        // (IsAvailable이 별도로 확인)와 "누구를 치료하려는 중인가"(이 필드)를 분리했다.
        Unit current = unit.CombatTargeting.HealTarget;
        if (current != null && current.Health != null && current.Health.hp > 0
            && current.currentFloor == unit.currentFloor
            && current.Health.hp / Mathf.Max(1f, current.Health.maxHp) < threshold)
        {
            return current;
        }

        Unit best = null;
        float bestRatio = threshold;
        float bestDist = float.MaxValue;
        // 검증문서 02-07 3번: 점수(HP비율)·거리까지 완전 동점인 후보들 — 하나만 무작위로 고른다.
        // 02-03에서 CombatFSMState.SelectAttackTarget에 적용한 것과 동일한 패턴.
        var tiedBest = new List<Unit>();

        void Consider(Unit candidate, float dist)
        {
            if (candidate.Health == null || candidate.Health.hp <= 0) return;
            float ratio = candidate.Health.hp / Mathf.Max(1f, candidate.Health.maxHp);
            if (ratio >= threshold) return; // 치료 필요 없음(해당 문턱 이상은 일반 치료 대상 아님)
            if (best == null || ratio < bestRatio || (ratio == bestRatio && dist < bestDist))
            {
                best = candidate; bestRatio = ratio; bestDist = dist;
                tiedBest.Clear();
                tiedBest.Add(candidate);
            }
            else if (ratio == bestRatio && dist == bestDist)
            {
                tiedBest.Add(candidate);
            }
        }

        // 검증문서 02-07 1번: 자기 파티 요구를 먼저 본다 — 자기 자신+파티원 안에서 후보를 찾고,
        // 아무도 없을 때만 파티 밖(다른 파티·무소속 아군)까지 넓힌다. 파티는 인류 전용 개념이라
        // (party 프로퍼티가 Unit이 아니라 Human에 있음, Unit.cs:608 Human 클래스 참고) 몬스터는
        // 이 분기를 건너뛰고, 무소속 인류(party==null)도 곧장 전체 탐색으로 넘어간다.
        if (unit is Human human && human.party != null)
        {
            Consider(unit, 0f);
            foreach (var m in human.party.Members)
            {
                if (m == null || m == unit || m.currentFloor != unit.currentFloor) continue;
                float dist = Vector2.Distance(unit.position, m.position);
                if (dist > range) continue;
                Consider(m, dist);
            }
        }

        if (best == null)
        {
            foreach (var u in unit.Session.units)
            {
                if (u == null || u == unit || u.currentFloor != unit.currentFloor) continue;
                if (unit.IsEnemy(u)) continue;
                float dist = Vector2.Distance(unit.position, u.position);
                if (dist > range) continue;
                Consider(u, dist);
            }
            Consider(unit, 0f); // 자기 자신도 같은 순서의 후보
        }

        if (tiedBest.Count > 1) best = tiedBest[Random.Range(0, tiedBest.Count)];

        unit.CombatTargeting.HealTarget = best;
        return best;
    }

    public override bool IsAvailable(Unit unit)
    {
        if (!IsCooldownReady(unit, _d.cooldownSlot)) return false;

        Unit lowest = GetHealTarget(unit);
        if (lowest == null || lowest.Health == null || lowest.Health.hp >= lowest.Health.maxHp) return false;
        // 검증문서 02-07 4번: 지금 실행 가능한 스킬 후보는 사거리 안일 때만이다 — 사거리 밖이면
        // HealTarget은 그대로 유지한 채(위 FindLowestHpAlly 참고) 여기서만 이번 틱 후보에서 빠지고,
        // CombatFSMState.TryGeneralHealApproach가 별도로 접근 이동을 담당한다.
        // 검증 04-01: 지원 범위에 더해 차폐(벽·구조물·닫힌 문)도 없어야 지금 쓸 수 있다 — 막혀 있으면 접근 이동이 차폐가 풀리는 자리까지 이어간다.
        return IsInSupportReach(unit, lowest);
    }

    // 치료를 지금 실행할 수 있는 위치인가 — 직선 사거리(유클리드) 안이고 직선이 차폐물에 막히지 않는다. CombatFSMState.TryGeneralHealApproach도 같은 기준으로 접근 종료를 판단한다.
    public bool IsInSupportReach(Unit unit, Unit ally)
        => Vector2.Distance(unit.position, ally.position) <= HitRange && HasClearLineTo(unit, ally);

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        Unit ally = GetHealTarget(unit);
        if (ally == null || ally.Health == null || ally.Health.hp >= ally.Health.maxHp) return 0f;

        float hpRatio = ally.Health.hp / Mathf.Max(1f, ally.Health.maxHp);
        // 02번 0/8장: 긴급 아군 보호 대상이면 일반 치료·공격 스킬을 모두 확실히 앞서도록 큰 값을 더한다.
        float emergencyBonus = (unit.CombatTargeting.ProtectTarget != null && ally == unit.CombatTargeting.ProtectTarget) ? 1000f : 0f;
        return emergencyBonus + _d.priorityBase + (1f - hpRatio) * 80f;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        // AI 경로에서는 ResolveTarget이 찾아 넘겨준 아군이 그대로 들어오지만, 테스트처럼 직접 호출해
        // 적을 넘기는 경우가 있으므로 아군이 아니면 스스로 다시 찾는다.
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

                    // 회복 이펙트 — 치유받은 아군 위치에 스폰한다.
                    if (_d.hitEffectPrefab != null)
                        unit.VFX?.Spawn(_d.hitEffectPrefab, targetAlly);
                }
            },
            () => unit.CombatState.State.skillCooldowns[_d.cooldownSlot] = ApplyCooldown(unit, _d.baseCooldown),
            skillName: SkillName
        );
    }
}
