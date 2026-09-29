#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

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
		// 03번 v0.12 8장: 참여 응답 기본 대기 2초, 담당자 도착 대기 기한의 여유 3초.
		Assert.AreEqual(2f, ExplorationMath.TrapJoinWaitSeconds, 0.001f);
		Assert.AreEqual(3f, ExplorationMath.TrapSelectedUnitLateGraceSeconds, 0.001f);
		// 문서에 수치가 없는 내부 판단값 — 바뀌면 의도한 변경인지 확인하게 고정한다.
		Assert.AreEqual(0.5f, ExplorationMath.TrapArrivalChangeToleranceSeconds, 0.001f);
		Assert.AreEqual(0.5f, ExplorationMath.TrapPassMinHpRatioAfterHit, 0.001f);
		Assert.AreEqual(0.3f, ExplorationMath.TrapAllyRescueMinHpRatioAfterHit, 0.001f);
		Assert.AreEqual(0.6f, ExplorationMath.TrapAllyRescueUnrecordedMinCurrentHpRatio, 0.001f);
		Assert.AreEqual(0.3f, ExplorationMath.AllyRescueTargetHpRatio, 0.001f);
		Assert.AreEqual(2f, ExplorationMath.FormationRangedMinBackDistance, 0.001f);
		Assert.AreEqual(0.5f, ExplorationMath.FormationNarrowSpaceBlockRatio, 0.001f);
	}

	// ── 03번 v0.12 8장 / 순서도 03-12·03-12-2. 함정 해제 담당자 도착 예정과 대기 기한 ──
	[Test]
	public void TrapWaitDeadline_IsCalcTimePlusRemainingPlusGrace()
	{
		// 0초에 남은 예상 이동시간 8초 → 대기 기한 11초.
		Assert.AreEqual(11f, ExplorationMath.TrapWaitDeadline(0f, 8f, ExplorationMath.TrapSelectedUnitLateGraceSeconds), 0.001f);
	}

	[Test]
	public void TrapArrivalEstimate_SameArrivalRecalculated_KeepsDeadline()
	{
		// 2초에 남은 6초로 다시 계산해도 도착 예정(8초)이 같으므로 수락하지 않고 기한 11초를 유지한다.
		float appliedArrival = ExplorationMath.TrapArrivalTime(0f, 8f);
		float incomingArrival = ExplorationMath.TrapArrivalTime(2f, 6f);
		Assert.IsFalse(ExplorationMath.ShouldApplyArrivalEstimate(true, 0f, appliedArrival, 2f, incomingArrival));
	}

	[Test]
	public void TrapArrivalEstimate_DuplicateOrStaleInfo_IsIgnored()
	{
		float appliedArrival = ExplorationMath.TrapArrivalTime(5f, 9f); // 도착 예정 14초
		// 같은 정보 재수신(계산 시점 동일).
		Assert.IsFalse(ExplorationMath.ShouldApplyArrivalEstimate(true, 5f, appliedArrival, 5f, appliedArrival));
		// 더 오래전에 계산된 정보가 늦게 도착해도(도착 예정이 달라도) 적용하지 않는다.
		Assert.IsFalse(ExplorationMath.ShouldApplyArrivalEstimate(true, 5f, appliedArrival, 2f, ExplorationMath.TrapArrivalTime(2f, 9f)));
	}

	[Test]
	public void TrapArrivalEstimate_ValidChange_UsesItsOwnCalcTime()
	{
		// 2초에 남은 9초(도착 예정 11초)로 바뀐 정보를 전달받으면 새 계산 시점 기준 기한 2+9+3=14초로 갱신한다.
		// 수신 시점은 인자에 없으므로 몇 초에 받든 같다(늦게 받아도 원래 계산 시점 기준).
		float appliedArrival = ExplorationMath.TrapArrivalTime(0f, 8f);
		float incomingArrival = ExplorationMath.TrapArrivalTime(2f, 9f);
		Assert.IsTrue(ExplorationMath.ShouldApplyArrivalEstimate(true, 0f, appliedArrival, 2f, incomingArrival));
		Assert.AreEqual(14f, ExplorationMath.TrapWaitDeadline(2f, 9f, 3f), 0.001f);
	}

	[Test]
	public void TrapArrivalEstimate_RepeatedValidUpdates_HaveNoCap()
	{
		// 유효 갱신이 반복되는 동안에는 총 대기시간·갱신 횟수 상한이 없다.
		float calc = 0f;
		float arrival = ExplorationMath.TrapArrivalTime(0f, 8f);
		for (int i = 1; i <= 500; i++)
		{
			float newArrival = arrival + 1f; // 매번 더 최근에 계산됐고 도착 예정이 1초씩 늦어짐
			Assert.IsTrue(ExplorationMath.ShouldApplyArrivalEstimate(true, calc, arrival, i, newArrival), $"{i}번째 유효 갱신이 거부됨");
			calc = i;
			arrival = newArrival;
		}
	}

	[Test]
	public void TrapArrivalEstimate_ChangeTolerance_Boundary()
	{
		float tol = ExplorationMath.TrapArrivalChangeToleranceSeconds;
		Assert.IsFalse(ExplorationMath.HasArrivalEstimateChanged(10f, 10.5f, tol)); // 허용오차 이하는 변경이 아니다
		Assert.IsTrue(ExplorationMath.HasArrivalEstimateChanged(10f, 10.6f, tol));
		Assert.IsTrue(ExplorationMath.HasArrivalEstimateChanged(10f, 9.4f, tol));   // 빨라진 경우도 변경
	}

	[Test]
	public void TrapRemainingTravel_StepsAndSpeed()
	{
		Assert.AreEqual(0, ExplorationMath.StepsToDisarmPosition(0));
		Assert.AreEqual(0, ExplorationMath.StepsToDisarmPosition(1)); // 이미 함정 인접 1칸
		Assert.AreEqual(9, ExplorationMath.StepsToDisarmPosition(10));
		Assert.AreEqual(3f, ExplorationMath.RemainingTravelSeconds(9, 3f), 0.001f);
		Assert.AreEqual(0f, ExplorationMath.RemainingTravelSeconds(0, 3f), 0.001f);
		Assert.IsTrue(float.IsPositiveInfinity(ExplorationMath.RemainingTravelSeconds(5, 0f)));
	}

	[Test]
	public void TrapArrivalTime_IsConstantWhileWalkingSteadily()
	{
		// 행동 틱마다 한 칸씩 이동하며 그 시점에 다시 계산하면 계산 시점+남은 시간(도착 예정)이 일정하다 —
		// 정상 이동 중에는 갱신이 발생하지 않는다.
		float speed = 3f;
		int pathLength = 10;
		float arrivalAtStart = ExplorationMath.TrapArrivalTime(0f,
			ExplorationMath.RemainingTravelSeconds(ExplorationMath.StepsToDisarmPosition(pathLength), speed));
		for (int step = 1; step <= 9; step++)
		{
			float t = step / speed;
			float remaining = ExplorationMath.RemainingTravelSeconds(ExplorationMath.StepsToDisarmPosition(pathLength - step), speed);
			Assert.AreEqual(arrivalAtStart, ExplorationMath.TrapArrivalTime(t, remaining), 0.001f);
		}
	}

	// ── 순서도 05-03: 집결 명령을 받으면 아직 시작하지 않은 함정 대응은 집결로 넘기고, 이미 시작한
	// 해제는 완료 후 합류한다. 조율 기록은 남고 담당 배정만 풀린다(EditMode — ScriptableObject.CreateInstance<Human>()
	// 관례). ──
	private static Human MakeHuman(string unitName, Party party)
	{
		var human = ScriptableObject.CreateInstance<Human>();
		human.name = unitName;
		human.party = party;
		party.Members.Add(human);
		return human;
	}

	[Test]
	public void ReleaseForRally_ReleasesUnstartedResponses_ButKeepsStartedDisarm()
	{
		var party = new Party("p", "p");
		var waiter = MakeHuman("waiter", party);
		var walker = MakeHuman("walker", party);
		var disarmer = MakeHuman("disarmer", party);
		waiter.currentTrapInteraction = new TrapInteractionState { TrapObjectId = "t", SelectedUnitName = "walker" };            // 담당자 도착 대기
		walker.currentTrapInteraction = new TrapInteractionState { TrapObjectId = "t", JoinWaitElapsed = true, IsSelectedDisarmer = true }; // 해제하러 가는 이동
		disarmer.currentTrapInteraction = new TrapInteractionState { TrapObjectId = "t2", Phase = TrapPhase.Disarming };         // 이미 시작한 해제

		TrapPartySystem.ReleaseForRally(waiter);
		TrapPartySystem.ReleaseForRally(walker);
		TrapPartySystem.ReleaseForRally(disarmer);

		Assert.IsNull(waiter.currentTrapInteraction);
		Assert.IsNull(walker.currentTrapInteraction);
		Assert.IsNotNull(disarmer.currentTrapInteraction);
	}

	[Test]
	public void ReleaseForRally_Assignee_KeepsCoordinationButClearsAssignment()
	{
		var party = new Party("p", "p");
		MakeHuman("discoverer", party);
		var assignee = MakeHuman("assignee", party);
		var coord = new TrapPartyCoordination { TrapObjectId = "t", DiscovererName = "discoverer", SelectedUnitName = "assignee", SelectionLocked = true };
		party.TrapCoordinations["t"] = coord;
		assignee.currentTrapInteraction = new TrapInteractionState { TrapObjectId = "t", JoinWaitElapsed = true, IsSelectedDisarmer = true, SelectedUnitName = "assignee" };

		TrapPartySystem.ReleaseForRally(assignee);

		Assert.IsNull(assignee.currentTrapInteraction);
		Assert.IsTrue(party.TrapCoordinations.ContainsKey("t")); // 기록은 유지(정보 재전파의 기준)
		Assert.IsNull(coord.SelectedUnitName);                    // 담당 배정만 해제
		Assert.IsFalse(coord.SelectionLocked);
	}

	[Test]
	public void EndResponse_Assignee_PostsConcluded_ButWaiterDoesNotTouchCoordination()
	{
		var party = new Party("p", "p");
		var discoverer = MakeHuman("discoverer", party);
		var assignee = MakeHuman("assignee", party);
		var coord = new TrapPartyCoordination { TrapObjectId = "t", DiscovererName = "discoverer", SelectedUnitName = "assignee", SelectionLocked = true };
		party.TrapCoordinations["t"] = coord;
		discoverer.currentTrapInteraction = new TrapInteractionState { TrapObjectId = "t", SelectedUnitName = "assignee" };
		assignee.currentTrapInteraction = new TrapInteractionState { TrapObjectId = "t", JoinWaitElapsed = true, IsSelectedDisarmer = true, SelectedUnitName = "assignee" };

		TrapPartySystem.EndResponse(discoverer, TrapEndReason.WaitEnded);
		Assert.IsNull(coord.Report); // 대기자가 대기를 끝내도 조율 기록은 그대로

		TrapPartySystem.EndResponse(assignee, TrapEndReason.Bypassed);
		Assert.IsNull(assignee.currentTrapInteraction);
		Assert.AreEqual(TrapAssigneeStatus.Concluded, coord.Report.Status);
		Assert.AreEqual("assignee", coord.Report.SenderName);
	}

	[Test]
	public void IsStillAssigned_FalseAfterReselection()
	{
		var party = new Party("p", "p");
		var oldAssignee = MakeHuman("old", party);
		MakeHuman("new", party);
		var coord = new TrapPartyCoordination { TrapObjectId = "t", DiscovererName = "discoverer", SelectedUnitName = "new", SelectionLocked = true };
		party.TrapCoordinations["t"] = coord;
		var state = new TrapInteractionState { TrapObjectId = "t", JoinWaitElapsed = true, IsSelectedDisarmer = true };

		Assert.IsFalse(TrapPartySystem.IsStillAssigned(oldAssignee, state)); // 재선정으로 밀린 이전 담당자는 양보한다
	}

	[Test]
	public void TickWaitingForSelectedUnit_AfterDeadline_ExcludesAssigneeAndRestartsSelection()
	{
		var party = new Party("p", "p");
		var waiter = MakeHuman("discoverer", party);
		MakeHuman("assignee", party);
		var coord = new TrapPartyCoordination { TrapObjectId = "t", DiscovererName = "discoverer", SelectedUnitName = "assignee", SelectionLocked = true };
		party.TrapCoordinations["t"] = coord;
		var trap = new TrapInteractionState
		{
			TrapObjectId = "t", JoinWaitElapsed = true, SelectedUnitName = "assignee",
			HasAppliedEstimate = true, WaitDeadline = float.NegativeInfinity, // 이미 지난 기한
		};
		waiter.currentTrapInteraction = trap;

		TrapPartySystem.TickWaitingForSelectedUnit(waiter, trap);

		CollectionAssert.Contains(coord.ExcludedUnitNames, "assignee"); // 미도착 담당자는 재선정 후보에서만 제외(사망·도주 확정 아님)
		Assert.IsNull(coord.SelectedUnitName);
		Assert.IsFalse(coord.SelectionLocked);
		Assert.IsNull(trap.SelectedUnitName);
		Assert.IsFalse(trap.JoinWaitElapsed); // 표준 선정 절차(2초 응답 대기)부터 다시
		Assert.IsFalse(trap.HasAppliedEstimate);
		Assert.IsTrue(float.IsPositiveInfinity(trap.WaitDeadline));
		Assert.IsNotNull(waiter.currentTrapInteraction); // 대응 자체를 접는 게 아니라 다시 판단한다
	}

	[Test]
	public void TickWaitingForSelectedUnit_SeesAssigneeAtTrap_EndsWaiting_EvenDiagonally()
	{
		// 전파 범위 밖이어도 담당자가 시야에 보이고 함정 인접 1칸(체비셰프, 대각선 포함)에 있으면 직접 확인한다.
		var party = new Party("p", "p");
		var waiter = MakeHuman("discoverer", party);
		var assignee = MakeHuman("assignee", party);
		party.TrapCoordinations["t"] = new TrapPartyCoordination { TrapObjectId = "t", DiscovererName = "discoverer", SelectedUnitName = "assignee", SelectionLocked = true };
		var trap = new TrapInteractionState
		{
			TrapObjectId = "t", TrapPosition = new Vector3Int(6, 6, 0), JoinWaitElapsed = true, SelectedUnitName = "assignee",
			HasAppliedEstimate = true, WaitDeadline = float.PositiveInfinity,
		};
		waiter.currentTrapInteraction = trap;
		assignee.position = new Vector2Int(5, 5); // 대각선 인접
		waiter.visiblePartyMembers.Add(assignee);

		TrapPartySystem.TickWaitingForSelectedUnit(waiter, trap);

		Assert.IsTrue(trap.AssigneeDone);
	}

	[Test]
	public void TickWaitingForSelectedUnit_DoesNotConfirmArrival_WhenNotSeenOrNotAdjacent()
	{
		var party = new Party("p", "p");
		var waiter = MakeHuman("discoverer", party);
		var assignee = MakeHuman("assignee", party);
		party.TrapCoordinations["t"] = new TrapPartyCoordination { TrapObjectId = "t", DiscovererName = "discoverer", SelectedUnitName = "assignee", SelectionLocked = true };
		var trap = new TrapInteractionState
		{
			TrapObjectId = "t", TrapPosition = new Vector3Int(6, 6, 0), JoinWaitElapsed = true, SelectedUnitName = "assignee",
			HasAppliedEstimate = true, WaitDeadline = float.PositiveInfinity,
		};
		waiter.currentTrapInteraction = trap;

		assignee.position = new Vector2Int(5, 5); // 인접하지만 시야에 없음 → 직접 확인 불가
		TrapPartySystem.TickWaitingForSelectedUnit(waiter, trap);
		Assert.IsFalse(trap.AssigneeDone);

		waiter.visiblePartyMembers.Add(assignee);
		assignee.position = new Vector2Int(4, 4); // 보이지만 아직 2칸 거리 → 도착 아님
		TrapPartySystem.TickWaitingForSelectedUnit(waiter, trap);
		Assert.IsFalse(trap.AssigneeDone);
	}

	[Test]
	public void TickWaitingForSelectedUnit_BeforeDeadline_KeepsWaiting()
	{
		var party = new Party("p", "p");
		var waiter = MakeHuman("discoverer", party);
		MakeHuman("assignee", party);
		var coord = new TrapPartyCoordination { TrapObjectId = "t", DiscovererName = "discoverer", SelectedUnitName = "assignee", SelectionLocked = true };
		party.TrapCoordinations["t"] = coord;
		var trap = new TrapInteractionState
		{
			TrapObjectId = "t", JoinWaitElapsed = true, SelectedUnitName = "assignee",
			HasAppliedEstimate = true, WaitDeadline = float.PositiveInfinity,
		};
		waiter.currentTrapInteraction = trap;

		TrapPartySystem.TickWaitingForSelectedUnit(waiter, trap);

		Assert.AreEqual("assignee", trap.SelectedUnitName);
		Assert.AreEqual(0, coord.ExcludedUnitNames.Count);
		Assert.IsTrue(coord.SelectionLocked);
	}
}
#endif
