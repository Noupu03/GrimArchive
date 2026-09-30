using UnityEngine;

// 03_탐색반응·경계·조사·함정대응_시스템_v0.6 순수 계산 함수 모음. VisionMath.cs/PerceptionMath.cs/
// WeightMath.cs와 같은 스타일(부수효과 없음, 테스트 용이) — 14장 "조정 가능한 파라미터" 표의 수치를
// 그대로 상수화했다. 문서가 수치를 안 주는 항목(함정 해제 성공률 공식 자체)만 자체 판단으로 스텁을
// 채웠다(아래 TrapDisarmSuccessRate 주석에 별도 표시).
public static class ExplorationMath
{
	// ─────────────────────────── 4장/14장. 경계 ───────────────────────────
	public const float AlertMoveSpeedRatio = 0.75f;      // 4-3장: 경계 이동속도 = 일반 이동속도의 75%
	public const float AlertReactionSpeedRatio = 1.2f;   // 4-2장: 경계 반응속도 = 기본 반응속도 × 1.2
	public const float PostCombatAlertSeconds = 10f;     // 4-8장: 전투 종료 후 경계 시간
	public const float UnidentifiedAttackSearchSeconds = 15f; // 4-12장: 미식별 공격 수색시간
	public const float SuspiciousTargetVisibilityBoostPerMove = 20f; // 4-6장: 이동 1회당 임시 가시성 +20

	// ─────────────────────────── 4-12~4-15장/14장. 파티원 사망 발견 (2026-07-27 신규) ───────────────────────────
	public const float DeathSearchSeconds = 10f; // 4-15장: 원인미상 파티원 사망 주변 수색시간
	public const float DeathDirectDiscoveryMentalLoss = 10f; // 4-12/4-13장: 최초 발견/목격 정신력 감소
	public const float DeathPropagationMentalLoss = 5f;      // 4-13장: 전파/재전파 수신 정신력 감소

	// 03번 v0.12 11장: 마지막 확인 위치에 도착해 대상 부재를 확인한 뒤의 공통 대기시간 — 리더 마지막 위치 보고 이동
	// (03-15)과 경계 수색의 마지막 위치 도착(03-16)이 함께 쓴다. 이 시간은 진행 중인 수색 시간 안에 포함된다.
	public const float LastPositionAbsenceWaitSeconds = 3f;

	// 부재 확인 대기(03번 11장 3초) 경과 여부. startTime이 음수면 아직 시작 전이다.
	public static bool AbsenceWaitElapsed(float startTime, float now, float waitSeconds)
		=> startTime >= 0f && now - startTime >= waitSeconds;

	// 03번 v0.6 4-7: 전파받은 적 위치에 접근해 그 "주위 3칸"에서도 적을 못 찾으면 확인을 마친 것으로 본다.
	public const int IndirectEnemyCheckRadius = 3;

	// 전파받은 적 정보가 아직 접근할 만큼 최신인가(내부 판단 유효 시간 안인가).
	public static bool IsIndirectInfoFresh(float now, float timestamp, float maxAgeSeconds)
		=> now - timestamp <= maxAgeSeconds;

	// 기록된 적 위치에서 이 거리 이내에 들어왔으면 그 주위 확인을 마친 것이다(적을 인지했다면 Combat이 이미 선점했다).
	public static bool IsWithinIndirectCheckRadius(float distance)
		=> distance <= IndirectEnemyCheckRadius;

	public enum AlertArrivalResult { Waiting, ContinueSearching, End }

	// 검증문서 03-16(순서도 03-16): 경계 수색이 마지막 위치에 도착해 부재를 확인한 뒤의 처리. 3초가 안 지났으면 제자리에서
	// 기다리고, 지났으면 공격 방향 수색은 남은 수색 기한 동안 정면 수색을 이어 가고 그 외(수상한 타일)는 경계를 끝낸다.
	public static AlertArrivalResult ResolveAlertArrival(float absenceStartTime, float now, bool continueSearchAfterAbsence)
	{
		if (!AbsenceWaitElapsed(absenceStartTime, now, LastPositionAbsenceWaitSeconds)) return AlertArrivalResult.Waiting;
		return continueSearchAfterAbsence ? AlertArrivalResult.ContinueSearching : AlertArrivalResult.End;
	}

	// ─────────────────────────── 5장/14장. 조사 ───────────────────────────
	public const float InvestigatePenaltyRatio = 0.5f;   // 5-4장: 조사 중 시야/인지/반응속도 50%
	public const float InvestigateInterruptLossRatio = 0.5f; // 5-6장: 중단 시 진행량의 50% 손실
	// 문서가 조사 소요시간은 안 주고 페널티 비율만 명시해, 진행도 게이지를 보여주기 위해 자체 판단으로
	// 채운 자리표시자다(밸런스 미확정).
	public const float InvestigateDurationSeconds = 8f;

	// ─────────────────────────── 9장/14장. 함정 대응 ───────────────────────────
	public const float TrapPenaltyRatio = 0.5f;          // 9-6장: 해제 중 시야/인지/반응속도 50%
	public const float TrapDisarmInterruptLossRatio = 0.5f; // 9-7장: 중단 시 해제량의 50% 손실
	// 9-3장/14장: 함정 정보 전파 후 발견 유닛 응답 대기시간 — 문서(v0.6 개정판) 공식값 그대로 적용.
	public const float TrapJoinWaitSeconds = 2f;
	// 03번 v0.12 8장: 대기 기한 = 담당자가 계산한 도착 예정 시점 + 이 여유.
	public const float TrapSelectedUnitLateGraceSeconds = 3f;
	// 담당자가 "도착 예정 시점이 달라졌다"고 대기자에게 새로 알릴 최소 변화량. 문서에 수치가 없는 내부 판단값
	// — 한 걸음(≈0.33초)보다 크고 여유 3초보다 훨씬 작다.
	public const float TrapArrivalChangeToleranceSeconds = 0.5f;
	public const float TrapArrivalEpsilon = 0.001f; // 부동소수 "같은 값" 판정용

	// 함정 인접 1칸(해제 위치)에 닿을 때까지 남은 걸음 수 — 함정 타일까지의 경로 길이에서 마지막 한 걸음을 뺀다.
	public static int StepsToDisarmPosition(int pathLengthToTrapTile) => Mathf.Max(0, pathLengthToTrapTile - 1);

	// 남은 걸음 수 ÷ 개인 적용 이동속도(칸/초). 속도가 0 이하면 도착할 수 없으므로 무한대.
	public static float RemainingTravelSeconds(int steps, float appliedWalkSpeed)
		=> appliedWalkSpeed <= 0f ? float.PositiveInfinity : Mathf.Max(0, steps) / appliedWalkSpeed;

	// 도착 예정 시점 = 계산 시점 + 남은 예상 이동시간, 대기 기한 = 도착 예정 시점 + 여유.
	public static float TrapArrivalTime(float calcTime, float remainingSeconds) => calcTime + remainingSeconds;
	public static float TrapWaitDeadline(float calcTime, float remainingSeconds, float graceSeconds)
		=> calcTime + remainingSeconds + graceSeconds;

	// 담당자 쪽(순서도 03-12): 마지막으로 알린 도착 예정 시점과 tolerance를 넘게 달라졌을 때만 새로 알린다.
	public static bool HasArrivalEstimateChanged(float lastArrivalTime, float newArrivalTime, float tolerance)
		=> Mathf.Abs(newArrivalTime - lastArrivalTime) > tolerance;

	// 대기자 쪽(순서도 03-12-2): 현재 적용 중인 정보보다 더 최근에 계산됐고 도착 예정 시점이 실제로 바뀐
	// 경우만 수락한다. 동일 정보 재수신·오래된 정보·같은 도착 예정의 재계산은 기한을 바꾸지 않는다.
	// 수신 시점은 인자로 받지 않는다 — 늦게 받아도 기한은 원래 계산 시점 기준이다.
	public static bool ShouldApplyArrivalEstimate(bool hasApplied, float appliedCalcTime, float appliedArrivalTime,
		float incomingCalcTime, float incomingArrivalTime)
	{
		if (!hasApplied) return true;
		if (incomingCalcTime <= appliedCalcTime + TrapArrivalEpsilon) return false;
		return Mathf.Abs(incomingArrivalTime - appliedArrivalTime) > TrapArrivalEpsilon;
	}

	// 순서도 03-10 "예상 성공률이 가장 높은 1명, 동률이면 해제 위치까지 짧은 도착시간": 성공률이 더 높으면 교체, 같으면(부동소수 근사)
	// 도착시간이 더 짧을 때만 교체한다. 둘 다 같으면 기존 승자를 유지한다(발견자 우선).
	public static bool IsBetterTrapDisarmCandidate(float rate, float eta, float bestRate, float bestEta)
		=> Mathf.Approximately(rate, bestRate) ? eta < bestEta : rate > bestRate;
	public const float TrapRecordedDirectDisarmThreshold = 0.5f; // 9-4장: 예상 성공률 50% 초과 기준
	public const float TrapPassMinHpRatioAfterHit = 0.5f;        // 9-10장: 일반 통과 후 최소 HP 50%
	public const float TrapAllyRescueMinHpRatioAfterHit = 0.3f;  // 9-11장: 아군 보호 시 기록 함정 통과 후 최소 HP 30%
	public const float TrapAllyRescueUnrecordedMinCurrentHpRatio = 0.6f; // 9-11장: 미기록 함정, 이동 유닛 현재 HP 60% 이상
	public const float AllyRescueTargetHpRatio = 0.3f;   // 9-11장: 즉시 보호 대상 아군 HP 30% 이하

	// ─────────────────────────── 03번 v0.12 9장·v0.6 9-6/9-12/9-13: 활성 함정 회피 구역과 통과 판정 (검증 03-13) ───────────────────────────
	// 회피 구역 = 함정 타일 + 체비셰프 1(v0.6 9-6 "인접 1칸"). 해제 위치(9-7)도 이 구역 안이다.
	public const int TrapAvoidZoneRadius = 1;

	public static bool IsInTrapZone(Vector2Int tile, Vector2Int trapTile)
		=> Mathf.Max(Mathf.Abs(tile.x - trapTile.x), Mathf.Abs(tile.y - trapTile.y)) <= TrapAvoidZoneRadius;

	// 비율 경계("정확히 30%·60%")가 문서대로 포함되도록 float 곱셈 오차(0.3f×100 = 30.000002 등)를 흡수한다.
	private const float RatioEpsilon = 0.0001f;

	private static bool RatioAtLeast(float value, float max, float ratio) => max > 0f && value / max >= ratio - RatioEpsilon;

	// 전투 합류(v0.6 9-12): 피해를 아는(=기록된) 함정만 판단할 수 있다 — 미기록 함정은 통과 후보가 아니다(긴급 보호의 별도 예외만 가능).
	// 통과 후 예상 HP(현재 HP − 알려진 최대 예상 피해)가 최대 HP의 minHpRatio 이상이어야 한다.
	public static bool CanPassTrapInCombat(bool trapRecorded, float hp, float maxHp, float knownDamageMax, float minHpRatio = TrapPassMinHpRatioAfterHit)
		=> trapRecorded && RatioAtLeast(hp - knownDamageMax, maxHp, minHpRatio);

	// 긴급 아군 보호(v0.6 9-13, 02번 v0.12 9장): 공통 = 보호 대상 HP 30% 이하 + 함정 경로가 우회보다 빠름(우회가 없으면 int.MaxValue).
	// 기록된 함정 = 함정 최대 예상 피해 적용 후 이동자 HP가 recordedMinHpRatio(30%) 이상, 미기록 함정 = 피해를 모르므로 이동자 현재 HP가
	// unrecordedMinCurrentHpRatio(60%) 이상(별도 예외 — 일반 적 공격 노출에 확대하지 않는다).
	public static bool CanPassTrapForProtect(bool trapRecorded, float allyHp, float allyMaxHp, float moverHp, float moverMaxHp, float knownDamageMax,
		int trapPathSteps, int detourSteps,
		float recordedMinHpRatio = TrapAllyRescueMinHpRatioAfterHit, float unrecordedMinCurrentHpRatio = TrapAllyRescueUnrecordedMinCurrentHpRatio)
	{
		if (allyMaxHp <= 0f || allyHp / allyMaxHp > AllyRescueTargetHpRatio + RatioEpsilon) return false;
		if (trapPathSteps >= detourSteps) return false;
		return trapRecorded
			? RatioAtLeast(moverHp - knownDamageMax, moverMaxHp, recordedMinHpRatio)
			: RatioAtLeast(moverHp, moverMaxHp, unrecordedMinCurrentHpRatio);
	}

	// 9-2장은 "클래스별 기본 성공률+레벨+이해도 보정"이라고만 서술하지만 이 코드베이스엔 "클래스" 개념이
	// 없어(UnitType은 스폰 타입일 뿐) 파생스탯 "집중(concentration)"을 "정교한 손기술" 대체 지표로 쓴
	// 밸런스 미확정 자리표시자다. 이해도 보정은 WeightMath.AppliedValue(0~100 정수)를 그대로 얹는다.
	public const float TrapDisarmBaseRateFloor = 20f;    // 최소 기본 성공률(%) — concentration=0이어도 완전히 0%는 아니게
	public const float TrapDisarmConcentrationWeight = 0.4f; // concentration(0~200 정규화값) 반영 비율
	public const float TrapDisarmLevelBonusPerLevel = 1.5f;  // 레벨 1당 보너스(%)
	public const float TrapDisarmUnderstandingWeight = 0.3f; // 이해도(0~100 AppliedValue) 반영 비율

	public static float TrapDisarmSuccessRate(float concentration, int level, int understandingApplied)
	{
		float rate = TrapDisarmBaseRateFloor
			+ concentration * TrapDisarmConcentrationWeight * 0.5f // concentration은 0~200 스케일이라 %로 반쯤 눌러줌
			+ Mathf.Max(0, level - 1) * TrapDisarmLevelBonusPerLevel
			+ Mathf.Clamp(understandingApplied, 0, 100) * TrapDisarmUnderstandingWeight;
		return Mathf.Clamp(rate, 0f, 100f);
	}

	// 9-2장: "이해도가 증가하면 예상 성공률의 오차 범위가 감소한다" — 오차범위를 이해도에 반비례해
	// 줄어드는 값으로 표현한다(이해도 0=오차 최대, 100=오차 0).
	public const float TrapExpectedRateMaxErrorMargin = 25f; // 이해도 0일 때 오차범위(%p)
	public static float TrapExpectedRateErrorMargin(int understandingApplied)
		=> TrapExpectedRateMaxErrorMargin * (1f - Mathf.Clamp(understandingApplied, 0, 100) / 100f);

	// 조사와 같은 이유의 자리표시자 — 문서가 해제 자체의 기준 소요시간은 안 주고(§14 파라미터표에도
	// TrapJoinWaitSeconds만 명시) 밸런스 미확정 값이다.
	public const float TrapDisarmDurationSeconds = 5f;
	// 9-9장: 파괴 중 매초 함정 Hp를 얼마나 깎는지(정식 Hitbox 경유가 아닌 간이 구현, physicalAttack에
	// 비례 — 시야인지반응_03_GOAP목표우선순위표_2026-07-22.txt 8-7절에 이미 명시된 한계).
	public const float TrapDestroyDamagePerSecondPerAttack = 0.5f;

	// ─────────────────────────── 6장/14장. 보호 포메이션 ───────────────────────────
	public const int FormationRangedHitRangeThreshold = 4; // Actions.cs의 기존 HitRange>=4 판정 재사용
	public const float FormationRangedMinBackDistance = 2f; // 6-5장: 후방 2칸 이상
	public const float FormationNarrowSpaceBlockRatio = 0.5f; // 6-7장: 정면 시야선 50% 이상 차단 시 재배정

	// ─────────────────────────── 8장. 공격 방향 경계 포메이션 ───────────────────────────
	public const float FormationAttackDirectionSearchSeconds = 15f; // 8-3장: 공격 방향 확인 후 15초 수색

	// 파티 목표 합류 지연(옛 7-2장 "1초") 상수는 삭제 — 문서 개정으로 규칙이 "즉시 전파"로 바뀌어
	// 지연시간 개념 자체가 없어졌다.
}
