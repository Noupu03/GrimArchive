using UnityEngine;

// 03_탐색반응·경계·조사·함정대응_시스템_v0.6 순수 계산 함수 모음. VisionMath.cs/PerceptionMath.cs/
// WeightMath.cs와 같은 스타일(부수효과 없음, 테스트 용이) — 14장 "조정 가능한 파라미터" 표의 수치를
// 그대로 상수화했다. 문서가 수치를 안 주는 항목(함정 해제 성공률 공식 자체)만 이번 구현의 자체
// 판단으로 스텁을 채웠다 — 그 부분은 아래 TrapDisarmSuccessRate 주석에 별도로 표시.
public static class ExplorationMath
{
	// ─────────────────────────── 4장/14장. 경계 ───────────────────────────
	public const float AlertMoveSpeedRatio = 0.75f;      // 4-3장: 경계 이동속도 = 일반 이동속도의 75%
	public const float AlertReactionSpeedRatio = 1.2f;   // 4-2장: 경계 반응속도 = 기본 반응속도 × 1.2
	public const float PostCombatAlertSeconds = 10f;     // 4-8장: 전투 종료 후 경계 시간
	public const float UnidentifiedAttackSearchSeconds = 15f; // 4-12장: 미식별 공격 수색시간
	public const float SuspiciousTargetVisibilityBoostPerMove = 20f; // 4-6장: 이동 1회당 임시 가시성 +20

	// ─────────────────────────── 5장/14장. 조사 ───────────────────────────
	public const float InvestigatePenaltyRatio = 0.5f;   // 5-4장: 조사 중 시야/인지/반응속도 50%
	public const float InvestigateInterruptLossRatio = 0.5f; // 5-6장: 중단 시 진행량의 50% 손실
	// 문서가 "조사에 총 몇 초가 걸리는지"는 안 주고 페널티 비율만 준다 — 진행도 게이지가 실제로
	// 움직이는 걸 보여주려면 기준 소요시간이 필요해 자체 판단으로 채운 자리표시자(밸런스 미확정).
	// 원래 3초였는데 너무 빨리 끝난다는 사용자 피드백(2026-07-22)에 따라 8초로 늘림.
	public const float InvestigateDurationSeconds = 8f;

	// ─────────────────────────── 9장/14장. 함정 대응 ───────────────────────────
	public const float TrapPenaltyRatio = 0.5f;          // 9-6장: 해제 중 시야/인지/반응속도 50%
	public const float TrapDisarmInterruptLossRatio = 0.5f; // 9-7장: 중단 시 해제량의 50% 손실
	// 9-3장/14장 파라미터표: 문서 원래값은 5초인데, 사용자 요청(2026-07-22)으로 3초로 단축 — 문서
	// 명시값에서 의도적으로 벗어난 값이라는 점을 기억해둘 것.
	public const float TrapJoinWaitSeconds = 3f;
	public const float TrapRecordedDirectDisarmThreshold = 0.5f; // 9-4장: 예상 성공률 50% 초과 기준
	public const float TrapPassMinHpRatioAfterHit = 0.5f;        // 9-10장: 일반 통과 후 최소 HP 50%
	public const float TrapAllyRescueMinHpRatioAfterHit = 0.3f;  // 9-11장: 아군 보호 시 기록 함정 통과 후 최소 HP 30%
	public const float TrapAllyRescueUnrecordedMinCurrentHpRatio = 0.6f; // 9-11장: 미기록 함정, 이동 유닛 현재 HP 60% 이상
	public const float AllyRescueTargetHpRatio = 0.3f;   // 9-11장: 즉시 보호 대상 아군 HP 30% 이하

	// 9-2장은 "클래스별 기본 함정 해제 성공률 + 유닛 레벨 + 해당 함정 이해도에 따른 보정"이라고만
	// 서술하고 실제 공식/기본값을 주지 않는다 — "클래스"(역할군) 개념 자체도 코드에 없어(UnitType은
	// Archer 등 스폰 타입일 뿐 함정 해제 숙련도와 연결된 값이 아님) 대체 지표가 필요했다. 기존
	// 파생스탯 중 "집중(concentration, 치명타60%+쿨감40%)"이 가장 "정교한 손기술" 성격에 가깝다고
	// 판단해 기본 성공률의 대체 지표로 썼다 — 밸런스 확정값이 아니라 구조를 채우기 위한 자리표시자다
	// (구현현황 문서에도 동일하게 표시). 이해도 보정은 WeightMath.AppliedValue(0~100 정수)를 그대로
	// 0~100% 스케일에 얹는다(9-2장 "이해도가 증가하면... 실제 해제 성공률이 증가한다"만 만족하면
	// 충분하다고 보고 별도 곡선은 만들지 않음).
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

	// 조사와 같은 이유의 자리표시자 — 해제 자체의 기준 소요시간(문서 미명시, §14 파라미터표에도 없음
	// — 문서는 "함정 합류 의사 대기시간"(TrapJoinWaitSeconds)만 명시하고 해제 진행 자체의 소요시간은
	// 안 준다). 4초→10초→15초로 늘려오다 10초로 확정했었는데(2026-07-22), 사용자가 다시 5초로
	// 단축 요청(2026-07-23 "함정 해제 시간은 5초로 줄이자").
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
}
