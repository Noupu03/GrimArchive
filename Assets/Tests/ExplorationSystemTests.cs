#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

// ========================================================================
// 03_탐색반응·경계·조사·함정대응_시스템_v0.6의 순수 함수(ExplorationMath)를 고정하는 테스트.
// VisionSystemTests.cs/PerceptionSystemTests.cs와 동일한 컨벤션.
// ========================================================================

public class ExplorationSystemTests
{
	// ── 9-2장. 함정 해제 성공률 — 문서가 공식을 안 줘서 자체 판단으로 채운 공식이라, "단조 증가"
	// 성질(집중/레벨/이해도가 높을수록 성공률이 오른다)과 0~100 클램프만 고정한다.
	[Test]
	public void TrapDisarmSuccessRate_MonotonicAndClamped()
	{
		float low = ExplorationMath.TrapDisarmSuccessRate(concentration: 0f, level: 1, understandingApplied: 0);
		float higherConcentration = ExplorationMath.TrapDisarmSuccessRate(concentration: 100f, level: 1, understandingApplied: 0);
		float higherLevel = ExplorationMath.TrapDisarmSuccessRate(concentration: 0f, level: 10, understandingApplied: 0);
		float higherUnderstanding = ExplorationMath.TrapDisarmSuccessRate(concentration: 0f, level: 1, understandingApplied: 100);
		float max = ExplorationMath.TrapDisarmSuccessRate(concentration: 200f, level: 100, understandingApplied: 100);

		Assert.AreEqual(ExplorationMath.TrapDisarmBaseRateFloor, low, 0.001f);
		Assert.Greater(higherConcentration, low);
		Assert.Greater(higherLevel, low);
		Assert.Greater(higherUnderstanding, low);
		Assert.AreEqual(100f, max, 0.001f); // 클램프 상한
		Assert.GreaterOrEqual(low, 0f);
	}

	// ── 9-4장. 기록 함정 직접 해제 기준(예상 성공률 50% 초과) — 상수 자체의 값을 고정.
	[Test]
	public void TrapRecordedDirectDisarmThreshold_IsFiftyPercent()
	{
		Assert.AreEqual(0.5f, ExplorationMath.TrapRecordedDirectDisarmThreshold, 0.001f);
	}

	// ── 9-2장. 이해도가 오를수록 예상 성공률 오차범위가 줄어든다(0=최대, 100=0).
	[Test]
	public void TrapExpectedRateErrorMargin_ShrinksWithUnderstanding()
	{
		Assert.AreEqual(ExplorationMath.TrapExpectedRateMaxErrorMargin, ExplorationMath.TrapExpectedRateErrorMargin(0), 0.001f);
		Assert.AreEqual(0f, ExplorationMath.TrapExpectedRateErrorMargin(100), 0.001f);
		Assert.Less(ExplorationMath.TrapExpectedRateErrorMargin(50), ExplorationMath.TrapExpectedRateErrorMargin(0));
	}

	// ── 14장. 조정 가능한 파라미터 표 — 숫자 자체를 고정해서 나중에 실수로 바뀌는 걸 막는다.
	[Test]
	public void Parameters_MatchDocumentTable()
	{
		Assert.AreEqual(0.75f, ExplorationMath.AlertMoveSpeedRatio, 0.001f);
		Assert.AreEqual(1.2f, ExplorationMath.AlertReactionSpeedRatio, 0.001f);
		Assert.AreEqual(10f, ExplorationMath.PostCombatAlertSeconds, 0.001f);
		Assert.AreEqual(15f, ExplorationMath.UnidentifiedAttackSearchSeconds, 0.001f);
		Assert.AreEqual(20f, ExplorationMath.SuspiciousTargetVisibilityBoostPerMove, 0.001f);
		Assert.AreEqual(0.5f, ExplorationMath.InvestigatePenaltyRatio, 0.001f);
		Assert.AreEqual(0.5f, ExplorationMath.InvestigateInterruptLossRatio, 0.001f);
		Assert.AreEqual(0.5f, ExplorationMath.TrapPenaltyRatio, 0.001f);
		Assert.AreEqual(0.5f, ExplorationMath.TrapDisarmInterruptLossRatio, 0.001f);
		// 문서 원래값은 5초인데, 사용자 요청(2026-07-22)으로 3초로 의도적으로 단축했다 — 이 테스트가
		// "문서 그대로"가 아니라 "현재 확정값"을 고정한다는 점에 유의(ExplorationMath.cs 주석 참고).
		Assert.AreEqual(3f, ExplorationMath.TrapJoinWaitSeconds, 0.001f);
		Assert.AreEqual(0.5f, ExplorationMath.TrapPassMinHpRatioAfterHit, 0.001f);
		Assert.AreEqual(0.3f, ExplorationMath.TrapAllyRescueMinHpRatioAfterHit, 0.001f);
		Assert.AreEqual(0.6f, ExplorationMath.TrapAllyRescueUnrecordedMinCurrentHpRatio, 0.001f);
		Assert.AreEqual(0.3f, ExplorationMath.AllyRescueTargetHpRatio, 0.001f);
		Assert.AreEqual(2f, ExplorationMath.FormationRangedMinBackDistance, 0.001f);
		Assert.AreEqual(0.5f, ExplorationMath.FormationNarrowSpaceBlockRatio, 0.001f);
	}
}
#endif
