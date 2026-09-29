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

	// ── 검증문서 03-11: 해제 중단(v0.6 9-9장)과 웨이브 종료 시 진행도 제거 ──
	[Test]
	public void ApplyDisarmInterruptPenalty_HalvesOnce_AndKeepsResponseForResume()
	{
		var human = MakeHuman("h", new Party("p", "p"));
		var trap = new TrapInteractionState { TrapObjectId = "t", Phase = TrapPhase.Disarming, DisarmProgress01 = 0.6f, PenaltyActive = true };
		human.currentTrapInteraction = trap;

		TrapPartySystem.ApplyDisarmInterruptPenalty(human);
		TrapPartySystem.ApplyDisarmInterruptPenalty(human); // 같은 중단이 이어지는 동안 다시 깎지 않는다(PenaltyActive 가드)

		Assert.AreEqual(0.3f, trap.DisarmProgress01, 0.001f);
		Assert.IsFalse(trap.PenaltyActive);
		Assert.AreSame(trap, human.currentTrapInteraction); // 대응 상태·남은 진행도 유지 — 원인이 사라지면 재개
	}

	[Test]
	public void InterruptDisarmersOf_PenalizesOnlyUnitsOnThatTrap()
	{
		var party = new Party("p", "p");
		var onTrap = MakeHuman("on", party);
		var alsoOnTrap = MakeHuman("also", party);
		var otherTrap = MakeHuman("other", party);
		var idle = MakeHuman("idle", party);
		onTrap.currentTrapInteraction = new TrapInteractionState { TrapObjectId = "t", Phase = TrapPhase.Disarming, DisarmProgress01 = 0.8f, PenaltyActive = true };
		alsoOnTrap.currentTrapInteraction = new TrapInteractionState { TrapObjectId = "t", Phase = TrapPhase.Disarming, DisarmProgress01 = 0.4f, PenaltyActive = true };
		otherTrap.currentTrapInteraction = new TrapInteractionState { TrapObjectId = "t2", Phase = TrapPhase.Disarming, DisarmProgress01 = 0.8f, PenaltyActive = true };

		// "함정이 작동함"(9-9장) — 그 함정을 해제 중인 유닛만 중단된다.
		TrapPartySystem.InterruptDisarmersOf("t", new Unit[] { onTrap, alsoOnTrap, otherTrap, idle, null });

		Assert.AreEqual(0.4f, onTrap.currentTrapInteraction.DisarmProgress01, 0.001f);
		Assert.AreEqual(0.2f, alsoOnTrap.currentTrapInteraction.DisarmProgress01, 0.001f);
		Assert.AreEqual(0.8f, otherTrap.currentTrapInteraction.DisarmProgress01, 0.001f);
		Assert.IsTrue(otherTrap.currentTrapInteraction.PenaltyActive);
	}

	[Test]
	public void ClearInteractionProgress_DropsTrapAndInvestigation_AndReleasesAssignment()
	{
		var party = new Party("p", "p");
		MakeHuman("discoverer", party);
		var assignee = MakeHuman("assignee", party);
		var coord = new TrapPartyCoordination { TrapObjectId = "t", DiscovererName = "discoverer", SelectedUnitName = "assignee", SelectionLocked = true };
		party.TrapCoordinations["t"] = coord;
		assignee.currentTrapInteraction = new TrapInteractionState { TrapObjectId = "t", Phase = TrapPhase.Disarming, JoinWaitElapsed = true, IsSelectedDisarmer = true, SelectedUnitName = "assignee", DisarmProgress01 = 0.7f };
		assignee.currentInvestigation = new InvestigationState { TargetObjectId = "o", Progress01 = 0.5f };

		assignee.ClearInteractionProgress();

		Assert.IsNull(assignee.currentTrapInteraction);
		Assert.IsNull(assignee.currentInvestigation);
		Assert.IsTrue(party.TrapCoordinations.ContainsKey("t")); // 함정 발견 기록은 파티가 이어지는 한 남는다
		Assert.IsNull(coord.SelectedUnitName);                    // 담당 배정만 풀려 다른 파티원이 재선정할 수 있다
		Assert.IsNull(coord.Report);                              // 정상 종료가 아니므로 Concluded 보고도 남기지 않는다
	}

	// ── 검증문서 03-12: 해제 담당 선정(성공률 우선·입장 전 최고 성공률·후보 필터·집결 중 제외) ──
	// 함정은 (10,10)에 있고 유닛 기본 이동속도(3칸/초)라 도착시간은 체비셰프 거리로만 갈린다(세션이 없어 A*·프론티어 추정은 폴백).
	// 성공률(TrapDisarmSuccessRate) = 20 + 집중×0.2 + (레벨−1)×1.5 — 집중 100/레벨 1이면 40, 집중 0/레벨 1이면 20.
	private const string TrapId = "t";
	private static readonly Vector3Int TrapPos = new Vector3Int(10, 10, 0);

	private static Human MakeDisarmer(string unitName, Party party, float concentration, int x, int y, bool knowsTrap = true)
	{
		var human = MakeHuman(unitName, party);
		human.concentration = concentration;
		human.level = 1;
		human.position = new Vector2Int(x, y);
		if (knowsTrap) human.personalMap.RegisterObject(TrapId, TrapPos, 0f, 0f);
		return human;
	}

	private static InteractableObject MakeTrapObject()
		=> new InteractableObject(TrapId, TrapPos, 0f, tags: new System.Collections.Generic.List<string> { "Object/Building/Passable/Trap" },
			trapHp: 300f, trapDamageMin: 30f, trapDamageMax: 60f);

	private static TrapPartyCoordination MakeCoord(string discovererName)
		=> new TrapPartyCoordination { TrapObjectId = TrapId, TrapPosition = TrapPos, DiscovererName = discovererName };

	private static WaitState RallyWait() => new WaitState { Reason = WaitReason.AwaitingPartyAtRallyPoint, WaitPosition = new Vector2Int(0, 0) };

	[Test]
	public void IsBetterTrapDisarmCandidate_RateFirst_ThenEta_ThenKeepsIncumbent()
	{
		Assert.IsTrue(ExplorationMath.IsBetterTrapDisarmCandidate(60f, 9f, 50f, 1f));  // 성공률이 높으면 도착이 늦어도 우선
		Assert.IsFalse(ExplorationMath.IsBetterTrapDisarmCandidate(40f, 1f, 50f, 9f)); // 성공률이 낮으면 도착이 빨라도 아님
		Assert.IsTrue(ExplorationMath.IsBetterTrapDisarmCandidate(50f, 2f, 50f, 3f));  // 동률이면 해제 위치에 더 빨리 도착하는 쪽
		Assert.IsFalse(ExplorationMath.IsBetterTrapDisarmCandidate(50f, 3f, 50f, 3f)); // 완전 동률은 기존 승자(발견자) 유지
	}

	[Test]
	public void WouldAttemptDisarm_UnrecordedAlways_RecordedOnlyAboveFiftyPercent()
	{
		var human = MakeHuman("h", new Party("p", "p"));
		Assert.IsTrue(TrapPartySystem.WouldAttemptDisarm(human, TrapId)); // 미기록 함정은 최초 1회 시도

		human.personalMap.RecordTrapAttempt(TrapId, 60f);
		Assert.IsTrue(TrapPartySystem.WouldAttemptDisarm(human, TrapId));
		human.personalMap.RecordTrapAttempt(TrapId, 50f);
		Assert.IsFalse(TrapPartySystem.WouldAttemptDisarm(human, TrapId)); // 정확히 50%는 우회·파괴 판단
		human.personalMap.RecordTrapAttempt(TrapId, 40f);
		Assert.IsFalse(TrapPartySystem.WouldAttemptDisarm(human, TrapId));
	}

	[Test]
	public void SnapshotEntryBestDisarmers_SingleBest_AndTiesAllIncluded()
	{
		var party = new Party("p", "p");
		MakeDisarmer("rogue", party, 100f, 0, 0);
		MakeDisarmer("mage", party, 0f, 0, 0);
		CollectionAssert.AreEquivalent(new[] { "rogue" }, TrapPartySystem.SnapshotEntryBestDisarmers(party.Members));

		MakeDisarmer("rogue2", party, 100f, 0, 0); // 동률 — 아무도 더 높지 않으므로 둘 다 "최고"
		CollectionAssert.AreEquivalent(new[] { "rogue", "rogue2" }, TrapPartySystem.SnapshotEntryBestDisarmers(party.Members));
	}

	[Test]
	public void ChooseDisarmer_HighestRateWins_EvenIfFarther()
	{
		var party = new Party("p", "p");
		var discoverer = MakeDisarmer("d", party, 0f, 9, 10);  // 성공률 20, 함정 바로 옆
		var expert = MakeDisarmer("far", party, 100f, 0, 10);  // 성공률 40, 멀리 있음
		MakeDisarmer("near", party, 50f, 8, 10);               // 성공률 30, 가까움

		var chosen = TrapPartySystem.ChooseDisarmer(discoverer, party.Members, MakeCoord("d"), TrapId, TrapPos, _ => true);

		Assert.AreSame(expert, chosen); // 예전 "가장 가까운 1명" 선정이라면 discoverer/near가 뽑혔다
	}

	[Test]
	public void ChooseDisarmer_EqualRate_NearestWins_AndDiscovererKeepsFullTies()
	{
		var party = new Party("p", "p");
		var discoverer = MakeDisarmer("d", party, 0f, 0, 10);
		MakeDisarmer("mid", party, 0f, 5, 10);
		var nearest = MakeDisarmer("nearest", party, 0f, 9, 10);
		Assert.AreSame(nearest, TrapPartySystem.ChooseDisarmer(discoverer, party.Members, MakeCoord("d"), TrapId, TrapPos, _ => true));

		// 성공률·도착시간이 모두 같으면 발견자가 유지된다.
		var party2 = new Party("p2", "p2");
		var d2 = MakeDisarmer("d2", party2, 0f, 5, 10);
		MakeDisarmer("same", party2, 0f, 5, 10);
		Assert.AreSame(d2, TrapPartySystem.ChooseDisarmer(d2, party2.Members, MakeCoord("d2"), TrapId, TrapPos, _ => true));
	}

	[Test]
	public void ChooseDisarmer_SkipsUnwillingRecordedUnits_AndFallsBackToDiscoverer()
	{
		var party = new Party("p", "p");
		var discoverer = MakeDisarmer("d", party, 0f, 9, 10);
		var other = MakeDisarmer("o", party, 0f, 8, 10);
		discoverer.personalMap.RecordTrapAttempt(TrapId, 40f); // 기록·성공률 40% — 해제하지 않을 유닛
		other.personalMap.RecordTrapAttempt(TrapId, 45f);

		// 아무도 해제하지 않으면 발견자가 그대로 맡아 우회·파괴·다른 행동을 판단한다.
		Assert.AreSame(discoverer, TrapPartySystem.ChooseDisarmer(discoverer, party.Members, MakeCoord("d"), TrapId, TrapPos, _ => true));

		// 해제할 유닛(미기록)이 있으면, 성공률이 낮아도 그 유닛이 뽑힌다 — 발견자는 해제하지 않는다.
		var willing = MakeDisarmer("w", party, 0f, 0, 10);
		Assert.AreSame(willing, TrapPartySystem.ChooseDisarmer(discoverer, party.Members, MakeCoord("d"), TrapId, TrapPos, _ => true));
	}

	[Test]
	public void ChooseDisarmer_ExcludesCandidatesThatMustKeepTheirCurrentAction()
	{
		// 각 경우마다 성공률이 더 높은 후보가 하나뿐이고, 그 후보가 제외되면 발견자가 남는다.
		var spoilers = new System.Collections.Generic.Dictionary<string, System.Action<Human, TrapPartyCoordination>>
		{
			["집결 중"] = (h, c) => h.currentWait = RallyWait(),
			["공동 이동 중"] = (h, c) => h.currentWait = new WaitState { Reason = WaitReason.AdvancingToNextRoom, WaitPosition = new Vector2Int(0, 0) },
			["귀환 중"] = (h, c) => h.currentWait = new WaitState { Reason = WaitReason.Retreating, WaitPosition = new Vector2Int(0, 0) },
			["코어 보고 중"] = (h, c) => h.currentWait = new WaitState { Reason = WaitReason.ReportingCoreToLeader, CorePosition = new Vector3Int(1, 1, 0) },
			["전투 합류 대기"] = (h, c) => h.currentJoinCombatWait = new JoinCombatWaitState(),
			["조사 중"] = (h, c) => h.currentInvestigation = new InvestigationState { TargetObjectId = "o" },
			["다른 함정 대응 중"] = (h, c) => h.currentTrapInteraction = new TrapInteractionState { TrapObjectId = "other" },
			["재선정 제외"] = (h, c) => c.ExcludedUnitNames.Add(h.name),
		};

		foreach (var spoiler in spoilers)
		{
			var party = new Party("p", "p");
			var discoverer = MakeDisarmer("d", party, 0f, 9, 10);
			var expert = MakeDisarmer("expert", party, 100f, 8, 10);
			var coord = MakeCoord("d");
			spoiler.Value(expert, coord);

			Assert.AreSame(discoverer, TrapPartySystem.ChooseDisarmer(discoverer, party.Members, coord, TrapId, TrapPos, _ => true), spoiler.Key);
		}
	}

	[Test]
	public void ChooseDisarmer_ExcludesCandidateThatNeverReceivedTheTrapInfo()
	{
		var party = new Party("p", "p");
		var discoverer = MakeDisarmer("d", party, 0f, 9, 10);
		MakeDisarmer("uninformed", party, 100f, 8, 10, knowsTrap: false); // 선정 시점 범위 안이어도 함정 정보를 못 받았으면 후보가 아니다

		Assert.AreSame(discoverer, TrapPartySystem.ChooseDisarmer(discoverer, party.Members, MakeCoord("d"), TrapId, TrapPos, _ => true));
	}

	[Test]
	public void IsCommittedToPartyMovement_WaitReasons_AndRallyPointWaiters()
	{
		var party = new Party("p", "p");
		var human = MakeDisarmer("h", party, 0f, 5, 5);
		Assert.IsFalse(TrapPartySystem.IsCommittedToPartyMovement(human));

		foreach (var reason in new[] { WaitReason.AwaitingPartyAtRallyPoint, WaitReason.AdvancingToNextRoom, WaitReason.ReportingCoreToLeader, WaitReason.Retreating })
		{
			human.currentWait = new WaitState { Reason = reason };
			Assert.IsTrue(TrapPartySystem.IsCommittedToPartyMovement(human), reason.ToString());
		}
		human.currentWait = new WaitState { Reason = WaitReason.AwaitingJoinBeforeApproach }; // 전투 관련 대기라 이 규칙의 대상이 아님
		Assert.IsFalse(TrapPartySystem.IsCommittedToPartyMovement(human));

		// 집결지에 도착하면 currentWait이 비워지므로 집결 진행 중(IsRallyActive) + 집결지 반경으로 본다 — 명령을 못 받은 먼 파티원은 아님.
		human.currentWait = null;
		party.IsRallyActive = true;
		party.RallyPoint = new Vector2Int(5, 5);
		Assert.IsTrue(TrapPartySystem.IsCommittedToPartyMovement(human));
		human.position = new Vector2Int(20, 20);
		Assert.IsFalse(TrapPartySystem.IsCommittedToPartyMovement(human));
	}

	[Test]
	public void OnTrapDiscovered_EntryBestDiscoverer_ConfirmsWithoutWaiting_OthersWait()
	{
		var party = new Party("p", "p");
		var best = MakeDisarmer("best", party, 100f, 9, 10, knowsTrap: false);
		var other = MakeDisarmer("other", party, 0f, 8, 10, knowsTrap: false);
		party.EntryBestDisarmerNames.Add("best");

		TrapPartySystem.OnTrapDiscovered(best, MakeTrapObject());
		Assert.IsTrue(best.currentTrapInteraction.AutoConfirmed);
		Assert.IsTrue(best.currentTrapInteraction.JoinWaitElapsed);
		Assert.IsTrue(best.currentTrapInteraction.IsSelectedDisarmer);
		Assert.AreEqual("best", party.TrapCoordinations[TrapId].SelectedUnitName);

		// 입장 전 최고 유닛이 아닌 발견자는 (가장 가까워도) 2초 응답 대기부터 시작한다.
		var party2 = new Party("p2", "p2");
		var nearButNotBest = MakeDisarmer("near", party2, 0f, 9, 10, knowsTrap: false);
		MakeDisarmer("expert", party2, 100f, 0, 10, knowsTrap: false);
		party2.EntryBestDisarmerNames.Add("expert");
		TrapPartySystem.OnTrapDiscovered(nearButNotBest, MakeTrapObject());
		Assert.IsFalse(nearButNotBest.currentTrapInteraction.AutoConfirmed);
		Assert.IsFalse(nearButNotBest.currentTrapInteraction.JoinWaitElapsed);
		Assert.IsNull(party2.TrapCoordinations[TrapId].SelectedUnitName);
		Assert.IsNull(other.currentTrapInteraction);
	}

	[Test]
	public void OnTrapDiscovered_EntryBestButWouldNotDisarm_StillWaits()
	{
		var party = new Party("p", "p");
		var best = MakeDisarmer("best", party, 100f, 9, 10, knowsTrap: false);
		party.EntryBestDisarmerNames.Add("best");
		best.personalMap.RecordTrapAttempt(TrapId, 40f); // 기록·성공률 40% — 해제하지 않을 유닛이라 즉시 확정하지 않는다

		TrapPartySystem.OnTrapDiscovered(best, MakeTrapObject());

		Assert.IsFalse(best.currentTrapInteraction.AutoConfirmed);
		Assert.IsFalse(best.currentTrapInteraction.JoinWaitElapsed);
	}

	[Test]
	public void OnTrapDiscovered_CommittedDiscoverer_Defers_AndLaterFreeDiscovererAdopts()
	{
		var party = new Party("p", "p");
		var rallying = MakeDisarmer("rallying", party, 100f, 9, 10, knowsTrap: false);
		var free = MakeDisarmer("free", party, 0f, 8, 10, knowsTrap: false);
		var rallying2 = MakeDisarmer("rallying2", party, 0f, 7, 10, knowsTrap: false);
		rallying.currentWait = RallyWait();
		rallying2.currentWait = RallyWait();
		party.EntryBestDisarmerNames.Add("rallying");

		// 집결 중인 발견자는 정보만 남기고 대응(응답 대기·선정)을 시작하지 않는다.
		TrapPartySystem.OnTrapDiscovered(rallying, MakeTrapObject());
		Assert.IsNull(rallying.currentTrapInteraction);
		Assert.IsTrue(party.TrapCoordinations[TrapId].Deferred);
		Assert.IsTrue(rallying.personalMap.IsObjectKnown(TrapId)); // 정보 자체는 기록된다

		// 보류된 함정을 또 집결 중인 유닛이 보면 여전히 보류다.
		TrapPartySystem.OnTrapDiscovered(rallying2, MakeTrapObject());
		Assert.IsNull(rallying2.currentTrapInteraction);
		Assert.AreEqual("rallying", party.TrapCoordinations[TrapId].DiscovererName);

		// 집결·이동 중이 아닌 유닛이 처음 발견하면 그 기록을 이어받아 표준 절차를 시작한다.
		TrapPartySystem.OnTrapDiscovered(free, MakeTrapObject());
		Assert.IsFalse(party.TrapCoordinations[TrapId].Deferred);
		Assert.AreEqual("free", party.TrapCoordinations[TrapId].DiscovererName);
		Assert.IsNotNull(free.currentTrapInteraction);
	}

	[Test]
	public void OnTrapDiscovered_AlreadyHandledTrap_IsIgnoredForLaterDiscoverers()
	{
		var party = new Party("p", "p");
		var first = MakeDisarmer("first", party, 0f, 9, 10, knowsTrap: false);
		var later = MakeDisarmer("later", party, 0f, 8, 10, knowsTrap: false);

		TrapPartySystem.OnTrapDiscovered(first, MakeTrapObject());
		TrapPartySystem.OnTrapDiscovered(later, MakeTrapObject());

		Assert.IsNotNull(first.currentTrapInteraction);
		Assert.IsNull(later.currentTrapInteraction); // 이미 다른 파티원이 처리 중
		Assert.AreEqual("first", party.TrapCoordinations[TrapId].DiscovererName);
	}

	[Test]
	public void ResolveSelection_WithNobodyInRange_KeepsDiscovererAsAssignee()
	{
		var party = new Party("p", "p");
		var discoverer = MakeDisarmer("d", party, 0f, 9, 10);
		MakeDisarmer("far", party, 100f, 0, 10); // 세션이 없어 전파 범위 조건(CanPropagate)이 성립하지 않는다
		var coord = MakeCoord("d");
		party.TrapCoordinations[TrapId] = coord;
		var trap = new TrapInteractionState { TrapObjectId = TrapId, TrapPosition = TrapPos, JoinWaitElapsed = true };
		discoverer.currentTrapInteraction = trap;

		TrapPartySystem.ResolveSelection(discoverer, trap);

		Assert.AreEqual("d", coord.SelectedUnitName);
		Assert.IsTrue(trap.IsSelectedDisarmer);
		Assert.IsTrue(coord.SelectionLocked);
	}
}
#endif
