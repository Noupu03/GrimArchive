using System.Collections.Generic;
using UnityEngine;

// 리더 명령 설계명세서 v0.15 순수 계산 함수 모음 — 부수효과 없음, 테스트 용이. CombatScoreMath/PropagationMath/RetreatMath와 같은 자리·성격.
// 검증 사례(L01~L30)의 숫자 예시는 Assets/Tests/LeaderCommandTests.cs가 고정한다.
public static class LeaderMath
{
	// 부동소수 경계 허용 오차 — 100 × 1.2f = 120.00001 같은 오차로 "정확히 N배"가 거짓이 되지 않게 한다(CombatScoreMath.MeetsSwitchRatio와 같은 처리).
	private const float Epsilon = 1e-3f;

	// ── 3장: 통솔력 ────────────────────────────────────────────────
	public const float MinLeaderBaseLeadership = 10f;     // 리더 선정 자격(기본 통솔력)
	public const float BasicBuffBaseLeadership = 20f;     // 기본 버프 자격(기본 통솔력)
	public const float FocusMinLeadership = 20f;          // 집중 공격 등 단순 전술(현재 통솔력)
	public const float ActionPriorityMinLeadership = 40f; // 강공격·제압·보호
	public const float SuperiorMinLeadership = 60f;       // 상위 명령
	public const float SignatureMinLeadership = 80f;      // 고유 지휘 스킬
	public const float SignatureFinalLeadership = 100f;   // 고유 스킬 최종 강화

	public static float ClampLeadership(float value) => Mathf.Clamp(value, 0f, 100f);

	// 선정 자격: 생존·개인 도주 미확정·기본 통솔력 10 이상(2-1장). 현재 통솔력이 일시적으로 10 미만이어도 이미 선정된 리더의 자격은 유지되므로 이 함수는 '선정 시점'에만 쓴다.
	public static bool IsLeaderEligible(float baseLeadership, bool alive, bool fleeConfirmed)
		=> alive && !fleeConfirmed && baseLeadership >= MinLeaderBaseLeadership;

	// 후보 비교 — 기본 통솔력 → 유닛 등급 → 고정한 무작위 순서(2-1장). 음수면 A가 우선이다.
	public static int CompareLeaderCandidates(float baseA, float gradeA, int tieBreakA, float baseB, float gradeB, int tieBreakB)
	{
		int c = baseB.CompareTo(baseA);
		if (c != 0) return c;
		c = gradeB.CompareTo(gradeA);
		if (c != 0) return c;
		return tieBreakA.CompareTo(tieBreakB);
	}

	// 명령별 필요한 현재 통솔력(3-표1). 새 명령을 내릴 때만 확인하며 이미 받은 명령은 통솔력이 떨어져도 종료 조건까지 유지한다.
	public static float MinLeadershipFor(TacticalCommand command) => command switch
	{
		TacticalCommand.FocusAttack         => FocusMinLeadership,
		TacticalCommand.ObjectFocus         => FocusMinLeadership, // 집중 공격의 변형이라 같은 자격
		TacticalCommand.Strike              => ActionPriorityMinLeadership,
		TacticalCommand.Suppress            => ActionPriorityMinLeadership,
		TacticalCommand.Protect             => ActionPriorityMinLeadership,
		TacticalCommand.SuperiorFocusAttack => SuperiorMinLeadership,
		TacticalCommand.SignatureSkill      => SignatureMinLeadership,
		_                                   => 0f,
	};

	public static bool MeetsLeadership(TacticalCommand command, float currentLeadership)
		=> ClampLeadership(currentLeadership) >= MinLeadershipFor(command) - 1e-4f;

	// ── 13-1장: 명령·정보 전파 반경 ────────────────────────────────────
	// 기본 3칸, 현재 통솔력 25마다 1칸, 최대 7칸. PropagationMath의 25단위 구간 표가 이미 같은 규칙이라 위임한다(전파 시점의 버프·디버프가 반영된 현재 통솔력을 넘길 것).
	public static int PropagationRadius(float currentLeadership) => PropagationMath.PropagationRange(currentLeadership);

	// ── 3-1장: 리더 본인의 정신력 감소 완화 ──────────────────────────────
	public static float MitigationRate(float currentLeadership) => ClampLeadership(currentLeadership) * 0.005f;

	// 일반 보정까지 반영한 감소량(양수)에 (1 − 현재 통솔력 × 0.005)를 곱한다. 소수점 유지.
	public static float MitigatedDecrease(float decreaseAfterGeneralModifiers, float currentLeadership)
		=> decreaseAfterGeneralModifiers * (1f - MitigationRate(currentLeadership));

	// ── 14장: 기본 버프 ───────────────────────────────────────────────
	public static bool HasBasicBuff(float baseLeadership) => baseLeadership >= BasicBuffBaseLeadership;

	// 증가율(0.1 = 10%) = 현재 통솔력 × 0.5%. 자격(기본 통솔력)이 없으면 0.
	public static float BasicBuffRate(float baseLeadership, float currentLeadership)
		=> HasBasicBuff(baseLeadership) ? ClampLeadership(currentLeadership) * 0.005f : 0f;

	// ── 7장·9-표2: 쿨타임과 적용시간 ──────────────────────────────────
	public const float GeneralCooldownSeconds = 10f;   // 일반 전술 명령(집중 공격·강공격·제압·보호) 뒤
	public const float SuperiorCooldownSeconds = 15f;  // 상위 전술 명령 뒤
	public const float ActionPriorityApplySeconds = 5f; // 강공격·제압·보호의 기술 우선순위 적용시간

	// 쿨타임을 시작하는 명령의 길이. 고유 지휘 스킬은 스킬별 설정이라 여기서 정하지 않는다(0).
	public static float CooldownSecondsOf(TacticalCommand command) => command switch
	{
		TacticalCommand.SuperiorFocusAttack => SuperiorCooldownSeconds,
		TacticalCommand.FocusAttack         => GeneralCooldownSeconds,
		TacticalCommand.ObjectFocus         => GeneralCooldownSeconds,
		TacticalCommand.Strike              => GeneralCooldownSeconds,
		TacticalCommand.Suppress            => GeneralCooldownSeconds,
		TacticalCommand.Protect             => GeneralCooldownSeconds,
		_                                   => 0f,
	};

	// ── 10장·11장: 지휘 목표 점수 ──────────────────────────────────────
	public const float NewFocusRatio = 1.5f;

	// 인류 파티 컨셉별 적 전투 역할 위협 배율(10-1-표1) — 초반 모험가는 적 원거리 지원, 초반 도적은 적 원거리 공격 1.30. 그 밖은 1.00(용병·군대·용사는 문서 미정).
	public static float ThreatRoleMultiplier(PartyConcept concept, CombatRole enemyRole)
	{
		if (concept == PartyConcept.Adventurer && enemyRole == CombatRole.RangedSupport) return 1.30f;
		if (concept == PartyConcept.Rogue && enemyRole == CombatRole.RangedDps) return 1.30f;
		return 1.00f;
	}

	// 지휘 목표 점수 = 리더가 기록한 위험도 × 적 종류 위협 배율 × 대상 종류 배율. 방어건물은 전투 역할이 없어 위협 배율 1.00이고, 보스·건물 배율은 한 대상에 하나만 적용한다(10-2장). 개인 공격 역할 배율은 넣지 않는다.
	public static float CommandTargetScore(float leaderRecordedDanger, PartyConcept concept, CombatRole enemyRole, TargetCategory category)
	{
		float roleMultiplier = category == TargetCategory.AttackBuilding ? 1.00f : ThreatRoleMultiplier(concept, enemyRole);
		return leaderRecordedDanger * roleMultiplier * CombatScoreMath.TargetCategoryMultiplier(category);
	}

	// 신규 지정: 후보 2명 이상이고 최고 점수 ≥ 두 번째 점수 × 1.5(11-표1·11-1장). 점수가 0이면 비교 의미가 없어 지정하지 않는다.
	public static bool MeetsNewFocusRatio(float topScore, float secondScore)
		=> topScore > 0f && topScore >= secondScore * NewFocusRatio - Epsilon;

	// 교체: 다른 적의 점수 ≥ 현재 지정한 적의 점수 × 1.2. 개인 공격 대상 교체와 같은 규칙이라 CombatScoreMath를 재사용한다(동점·양쪽 0은 유지).
	public static bool MeetsReplaceRatio(float currentScore, float otherScore)
		=> CombatScoreMath.ShouldSwitchAttackTarget(true, currentScore, otherScore);

	// ── 5-1-표2: 적 HP 30% 이하 ────────────────────────────────────────
	public const float LowHpRatio = 0.30f;
	public static bool IsLowHp(float hpRatio) => hpRatio <= LowHpRatio + 1e-4f; // 30%·29.9%는 계기, 30.1%는 아님(L15)

	// ── 12-2장: 대상 추적 ──────────────────────────────────────────────
	public const float PursuitMaxSeconds = 5f;
	public const float PursuitConfirmWaitMaxSeconds = 3f;

	public static float PursuitRemaining(float startTime, float now) => Mathf.Max(0f, PursuitMaxSeconds - (now - startTime));

	// 마지막 위치에 도착한 뒤 직접 확인을 기다리는 시간 — 최대 3초이고 전체 추적 5초 안에 포함된다(L23: 2초에 도착하면 3초, 4초에 도착하면 1초).
	public static float ConfirmWaitSeconds(float startTime, float arrivalTime)
		=> Mathf.Clamp(startTime + PursuitMaxSeconds - arrivalTime, 0f, PursuitConfirmWaitMaxSeconds);

	// ── 11-1장: 다른 근접 적과의 접근 제한 거리 ──────────────────────────
	// 경로의 한 점유 위치에서 내 점유 타일 중심들과 다른 근접 적 점유 타일 중심들 사이 직선거리의 최솟값. 적이 없으면 +무한대.
	public static float MinCenterDistance(IList<Vector2> myTileCenters, IList<Vector2> otherTileCenters)
	{
		float min = float.PositiveInfinity;
		if (myTileCenters == null || otherTileCenters == null) return min;
		for (int i = 0; i < myTileCenters.Count; i++)
			for (int j = 0; j < otherTileCenters.Count; j++)
				min = Mathf.Min(min, Vector2.Distance(myTileCenters[i], otherTileCenters[j]));
		return min;
	}

	// 설정값과 같으면 통과, 작으면 그 경로를 제외한다. 설정값 0은 이 추가 제한을 쓰지 않는다는 뜻이다.
	public static bool PassesApproachLimit(float minDistanceToOtherMelee, float limit)
		=> limit <= 0f || minDistanceToOtherMelee >= limit - 1e-4f;

	// ── 17·18장: 지휘 상실·사망 충격 ────────────────────────────────────
	public const float SuccessionDelaySeconds = 10f;
	public const float DeathShockFraction = 0.20f;
	public const float DeathShockSeconds = 10f;
	public const float LeaderFleeMentalLoss = 10f;

	// 리더(일반·상위) 사망을 처음 안 방식별 기본 감소량 — 직접 확인 −20 / 전달받음 −10. 일반 파티원 사망 감소(−10/−5)를 추가하지 않고 대체한다.
	public static float LeaderDeathMentalLoss(bool directlyWitnessed) => directlyWitnessed ? 20f : 10f;

	public static bool IsSuccessionDue(float startedAt, float now) => now - startedAt >= SuccessionDelaySeconds - 1e-4f;

	// ── 5-2장: 집결 대기 ───────────────────────────────────────────────
	public const float RallyBaseWaitSeconds = 8f;
	public const float RallyEtaMarginSeconds = 3f;
	public const float RallySearchSeconds = 5f;

	// 수색 위치로 가는 이동의 상한(초) — 문서에 값이 없어 정한 내부 판단이다. 넘으면 선 자리에서 관찰을 시작한다.
	public const float RallySearchTravelMaxSeconds = 20f;

	// 수색 후보 — 집결 명령을 받지 못한 미집결자의 마지막 확인 위치(리더가 직접 본 기록).
	public struct RallySearchCandidate
	{
		public string Name;
		public Vector2Int Position;
		public int Floor;
		public float SeenAt;
		// 리더가 그 위치까지 걷는 예상 걸음 수(알려진 경로 기준) — 파티 구성 8-1장 2번 "알려진 경로 기준 도착 예상시간이 짧은 위치". null이면 직선(체비셰프) 거리로 대신한다.
		public int? EtaSteps;
	}

	// 우선 위치 선정(파티 구성 8-1장 2번·순서도 P02): 리더와 같은 층의 후보 중 도착 예상시간(걸음 수)이 짧은 위치, 동률이면 실제 확인 시각이 더 최근인 쪽, 그래도 같으면 앞선 후보. 경로 길이를 모르면(EtaSteps 없음) 직선(체비셰프) 거리로 대신한다. 후보가 없으면 −1.
	public static int PickRallySearchTarget(Vector2Int leaderPosition, int leaderFloor, IReadOnlyList<RallySearchCandidate> candidates)
	{
		int best = -1, bestDist = int.MaxValue;
		float bestSeen = float.NegativeInfinity;
		if (candidates == null) return -1;
		for (int i = 0; i < candidates.Count; i++)
		{
			var c = candidates[i];
			if (c.Floor != leaderFloor) continue;
			int dist = c.EtaSteps ?? Mathf.Max(Mathf.Abs(c.Position.x - leaderPosition.x), Mathf.Abs(c.Position.y - leaderPosition.y));
			if (dist < bestDist || (dist == bestDist && c.SeenAt > bestSeen)) { best = i; bestDist = dist; bestSeen = c.SeenAt; }
		}
		return best;
	}

	// 미합류자의 도착 예정 시각 = 지금 + 남은 걸음 수 ÷ 걷기 속도. 걸음 수는 직선거리가 아니라 실제 경로 길이를 넣어야 우회로가 긴 대원의 기한이 일찍 잡히지 않는다(R4).
	public static float RallyEta(float now, int walkSteps, float walkSpeed) => now + Mathf.Max(0, walkSteps) / Mathf.Max(0.1f, walkSpeed);

	// 집결 기한 = max(명령 시각 + 8초, 가장 늦은 도착 예정 시각 + 3초). 도착 예정 정보가 없으면 기본 기한뿐이다.
	public static float RallyDeadline(float commandTime, float latestEta)
	{
		float baseDeadline = commandTime + RallyBaseWaitSeconds;
		return float.IsNegativeInfinity(latestEta) || float.IsNaN(latestEta) ? baseDeadline : Mathf.Max(baseDeadline, latestEta + RallyEtaMarginSeconds);
	}
}
