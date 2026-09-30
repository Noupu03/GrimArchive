#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 검증문서 03-15(리더의 마지막 위치가 비었을 때 보고) + 03-14 발견 2(보고 의무 영속화) 테스트.
// 순수 판정(PartyReportMath)과 개인 기록(KnownLeaderInfo)은 Unity 세션 없이 직접 검증하고, 집결 완료 판정은
// Party/Human 인스턴스만으로 확인한다. 세션(CanPropagate·지도)이 필요한 이동·전달 경로는 코드 추적으로만 점검했다.
// ========================================================================

public class CoreReportTests
{
	private static Human NewHuman(float hp = 10f)
	{
		var h = ScriptableObject.CreateInstance<Human>();
		h.hp = hp;
		return h;
	}

	// ── 목적지 우선순위: 아는 리더 위치 → 유효 집결지 → 발견한 문 → 시야 넓히기 → 없음 ──
	[TestCase(true, true, true, true, ReportDestinationKind.ToLeader)]
	[TestCase(false, true, true, true, ReportDestinationKind.ToRally)]
	[TestCase(false, false, true, true, ReportDestinationKind.ToDoor)]
	[TestCase(false, false, false, true, ReportDestinationKind.SearchFrontier)]
	[TestCase(false, false, false, false, ReportDestinationKind.None)]
	public void ResolveDestinationKind_FollowsDocumentOrder(bool leader, bool rally, bool door, bool frontier, ReportDestinationKind expected)
	{
		Assert.AreEqual(expected, PartyReportMath.ResolveDestinationKind(leader, rally, door, frontier));
	}

	// ── 리더 기록은 지금 리더에 대한 것이고 부재를 확인한 같은 위치가 아닐 때만 목적지로 쓴다 ──
	[Test]
	public void LeaderPositionUsable_RequiresCurrentLeaderAndNotEmptyConfirmedPosition()
	{
		var pos = new Vector2Int(5, 5);
		Assert.IsTrue(PartyReportMath.LeaderPositionUsable(true, pos, null));
		Assert.IsFalse(PartyReportMath.LeaderPositionUsable(false, pos, null), "리더가 교체됐으면 옛 기록은 쓸 수 없다");
		Assert.IsFalse(PartyReportMath.LeaderPositionUsable(true, pos, pos), "부재를 확인한 같은 위치를 반복 방문하지 않는다");
		Assert.IsTrue(PartyReportMath.LeaderPositionUsable(true, pos, new Vector2Int(9, 9)), "다른 위치의 부재 기록은 이 위치를 막지 않는다");
	}

	// ── 03번 11장: 도착 후 3초 대기 ──
	[Test]
	public void AbsenceWait_ElapsesAfterThreeSecondsOnly()
	{
		Assert.AreEqual(3f, ExplorationMath.LastPositionAbsenceWaitSeconds);
		Assert.IsFalse(ExplorationMath.AbsenceWaitElapsed(-1f, 100f, 3f), "시작 전(음수)에는 경과로 보지 않는다");
		Assert.IsFalse(ExplorationMath.AbsenceWaitElapsed(10f, 12.9f, 3f));
		Assert.IsTrue(ExplorationMath.AbsenceWaitElapsed(10f, 13f, 3f));
	}

	[Test]
	public void HasArrived_UsesRallyArrivalRadius()
	{
		Assert.IsTrue(PartyReportMath.HasArrived(new Vector2Int(0, 0), new Vector2Int(1, 1)));
		Assert.IsFalse(PartyReportMath.HasArrived(new Vector2Int(0, 0), new Vector2Int(2, 0)));
	}

	// ── KnownLeaderInfo: 더 최신 정보만 받고, 부재 확인 이후의 정보만 그 기록을 푼다 ──
	[Test]
	public void KnownLeaderInfo_UpdateRejectsOlderInfoForSameLeader()
	{
		var leader = NewHuman();
		var info = new KnownLeaderInfo();

		Assert.IsTrue(info.Update(leader, new Vector2Int(1, 1), 5f));
		Assert.IsFalse(info.Update(leader, new Vector2Int(9, 9), 4f), "더 오래된 정보는 무시");
		Assert.AreEqual(new Vector2Int(1, 1), info.Position);
		Assert.IsTrue(info.Update(leader, new Vector2Int(2, 2), 6f));
		Assert.AreEqual(new Vector2Int(2, 2), info.Position);
	}

	[Test]
	public void KnownLeaderInfo_EmptyConfirmation_ClearedOnlyByNewerInfo()
	{
		var leader = NewHuman();
		var info = new KnownLeaderInfo();
		var pos = new Vector2Int(3, 3);
		info.Update(leader, pos, 1f);
		info.MarkEmpty(pos, 4f);

		// 부재 확인(4초) 이전 시점의 정보(2초)는 같은 빈 위치를 되살리지 못한다 — 위치도 다른 정보라 기록만 남는다.
		Assert.IsTrue(info.Update(leader, new Vector2Int(7, 7), 2f));
		Assert.IsTrue(info.EmptyConfirmedPosition.HasValue);

		// 부재 확인 이후(5초)에 얻은 정보는 기록을 푼다(리더가 그 자리에 다시 나타났을 수 있다).
		Assert.IsTrue(info.Update(leader, pos, 5f));
		Assert.IsFalse(info.EmptyConfirmedPosition.HasValue);
	}

	[Test]
	public void KnownLeaderInfo_NewLeaderReplacesRecordAndClearsEmptyConfirmation()
	{
		var oldLeader = NewHuman();
		var newLeader = NewHuman();
		var info = new KnownLeaderInfo();
		info.Update(oldLeader, new Vector2Int(1, 1), 10f);
		info.MarkEmpty(new Vector2Int(1, 1), 11f);

		Assert.IsTrue(info.IsFor(oldLeader));
		Assert.IsFalse(info.IsFor(newLeader));
		Assert.IsTrue(info.Update(newLeader, new Vector2Int(4, 4), 1f), "리더가 바뀌면 시점과 무관하게 교체");
		Assert.IsTrue(info.IsFor(newLeader));
		Assert.IsFalse(info.EmptyConfirmedPosition.HasValue);
	}

	[Test]
	public void KnownLeaderInfo_Clear_ForgetsEverything()
	{
		var leader = NewHuman();
		var info = new KnownLeaderInfo();
		info.Update(leader, new Vector2Int(1, 1), 3f);
		info.MarkEmpty(new Vector2Int(1, 1), 4f);

		info.Clear();

		Assert.IsFalse(info.IsFor(leader));
		Assert.IsFalse(info.EmptyConfirmedPosition.HasValue);
	}

	// ── 웨이브 종료 정리: 재사용되는 생존자가 지난 파티의 보고 의무·리더 정보를 들고 가지 않는다 ──
	[Test]
	public void ClearInteractionProgress_ResetsReportObligationAndKnownLeader()
	{
		var human = NewHuman();
		var leader = NewHuman();
		human.pendingCoreReportPos = new Vector3Int(1, 1, 1);
		human.knownRallyPoint = new Vector2Int(2, 2);
		human.knownLeader.Update(leader, new Vector2Int(3, 3), 1f);

		human.ClearInteractionProgress();

		Assert.IsFalse(human.pendingCoreReportPos.HasValue);
		Assert.IsFalse(human.knownRallyPoint.HasValue);
		Assert.IsFalse(human.knownLeader.IsFor(leader));
	}

	// ── 03-14 발견 3 + 03-15: 정박한(갈 곳 없는) 보고자는 집결 완료를 붙잡지 않고, 이동 중인 보고자는 붙잡는다 ──
	[Test]
	public void CheckRallyComplete_IgnoresParkedReporterButWaitsForMovingReporter()
	{
		var party = new Party("P", "테스트");
		var reporter = NewHuman();
		party.Members.Add(reporter);
		reporter.currentWait = new WaitState { Reason = WaitReason.ReportingCoreToLeader, CorePosition = new Vector3Int(1, 1, 1) };

		party.IsRallyActive = true;
		party.CheckRallyComplete();
		Assert.IsTrue(party.IsRallyActive, "이동 중인 코어 보고자는 집결 완료를 보류시킨다");
		Assert.IsFalse(party.ReadyToAdvance);

		reporter.currentWait.IsParked = true;
		party.CheckRallyComplete();
		Assert.IsFalse(party.IsRallyActive, "정박한 보고자는 집결을 교착시키지 않는다");
		Assert.IsTrue(party.ReadyToAdvance);
	}

	// ── 갭 정리(05번 1장 73줄): 다음 문을 모를 때 공동 탐색 이동 ──
	[TestCase(false, 0, 3, PartyReportMath.FollowStep.Lost)]
	[TestCase(true, 3, 3, PartyReportMath.FollowStep.Hold)]
	[TestCase(true, 4, 3, PartyReportMath.FollowStep.Move)]
	[TestCase(true, 0, 3, PartyReportMath.FollowStep.Hold)]
	public void ResolveFollowStep_Table(bool hasKnownLeader, int distance, int radius, PartyReportMath.FollowStep expected)
	{
		Assert.AreEqual(expected, PartyReportMath.ResolveFollowStep(hasKnownLeader, distance, radius));
	}

	[Test]
	public void DoorSearch_CanAssign_RequiresNoWaitAndCooldownElapsed()
	{
		var human = NewHuman();
		Assert.IsTrue(PartyDoorSearchSystem.CanAssign(human, 10f));

		human.nextSearchFollowAllowedTime = 20f;
		Assert.IsFalse(PartyDoorSearchSystem.CanAssign(human, 10f), "추종을 접은 뒤 쿨다운 중에는 다시 배정하지 않는다");
		Assert.IsTrue(PartyDoorSearchSystem.CanAssign(human, 20f));

		human.currentWait = new WaitState { Reason = WaitReason.AwaitingPartyAtRallyPoint };
		Assert.IsFalse(PartyDoorSearchSystem.CanAssign(human, 30f), "이미 다른 대기(집결 등)가 있으면 덮어쓰지 않는다");
	}

	[Test]
	public void DoorSearch_Assign_SetsWaitAndCancelsIndirectEnemyApproach()
	{
		var human = NewHuman();
		human.currentAlertSearch = new AlertSearchState { IsIndirectEnemyApproach = true };

		PartyDoorSearchSystem.Assign(human);

		Assert.AreEqual(WaitReason.SearchingNextDoor, human.currentWait.Reason);
		Assert.IsNull(human.currentAlertSearch, "공동 이동이 시작되면 전파받은 적 위치 접근은 접는다");

		// 다른 종류의 경계(예: 공격 방향 수색)는 건드리지 않는다.
		var other = NewHuman();
		other.currentAlertSearch = new AlertSearchState { IsAttackDirectionSearch = true };
		PartyDoorSearchSystem.Assign(other);
		Assert.IsNotNull(other.currentAlertSearch);
	}

	// 집결을 마친 방을 리더가 떠나면 그 방에서 시작한 이동 의도(ReadyToAdvance)가 사라진다 — 안 지우면 새 방의 문이 알려지는 즉시
	// 그 방의 활동을 건너뛰고 이동 명령이 발행된다.
	[Test]
	public void Party_TickAdvanceState_ClearsAdvanceIntentWhenLeaderLeavesRoom()
	{
		var roomA = new Room();
		var roomB = new Room();
		var leader = NewHuman();
		leader.currentRoom = roomA;
		var party = new Party("P", "테스트") { Type = PartyType.MopUp };
		party.Members.Add(leader);
		party.Leader = leader;
		party.IsRallyActive = true;
		party.CheckRallyComplete();
		Assert.IsTrue(party.ReadyToAdvance);

		party.TickAdvanceState();
		Assert.IsTrue(party.ReadyToAdvance, "리더가 아직 그 방에 있으면 이동 의도를 유지한다");
		Assert.AreSame(roomA, party.AdvanceFromRoom);

		leader.currentRoom = roomB;
		party.TickAdvanceState();
		Assert.IsFalse(party.ReadyToAdvance);
		Assert.IsNull(party.AdvanceFromRoom);
	}

	// 리더가 새 방에서 집결을 시작하면 다음 문을 찾으며 따라오던 파티원과 전파받은 적 위치로 접근하던 파티원도 집결로 전환된다.
	[Test]
	public void Party_TryStartRally_ConvertsSearchFollowAndIndirectApproachToRally()
	{
		var room = new Room();
		var leader = NewHuman();
		var follower = NewHuman();
		var approacher = NewHuman();
		leader.currentRoom = follower.currentRoom = approacher.currentRoom = room;
		follower.currentWait = new WaitState { Reason = WaitReason.SearchingNextDoor };
		approacher.currentAlertSearch = new AlertSearchState { IsIndirectEnemyApproach = true };
		var party = new Party("P", "테스트") { Type = PartyType.MopUp };
		party.Members.Add(leader);
		party.Members.Add(follower);
		party.Members.Add(approacher);
		party.Leader = leader;
		party.MarkLeaderReachedNextDoor(); // 소탕 파티는 문 앞 접근을 마쳐야 집결을 판단한다

		party.TryStartRally();

		Assert.IsTrue(party.IsRallyActive);
		Assert.AreEqual(WaitReason.AwaitingPartyAtRallyPoint, follower.currentWait.Reason);
		Assert.AreEqual(WaitReason.AwaitingPartyAtRallyPoint, approacher.currentWait.Reason);
		Assert.IsNull(approacher.currentAlertSearch);
	}

	private static Party NewMopUpParty(Human leader, params Human[] others)
	{
		var party = new Party("P", "테스트") { Type = PartyType.MopUp };
		party.Members.Add(leader);
		foreach (var o in others) party.Members.Add(o);
		party.Leader = leader;
		party.MarkLeaderReachedNextDoor(); // 소탕 파티는 문 앞 접근을 마쳐야 집결을 판단한다(리더 현재 방 기준 — 방 지정 뒤에 호출된다)
		return party;
	}

	// 웨이브 시작 전(던전 입구 시퀀스 중)에는 집결 판정을 하지 않는다 — 소탕 파티는 "인지한 적 없음"이 스폰 직후부터 참이라,
	// 막지 않으면 0층 스폰 자리에서 집결이 시작돼 계단을 건너면 끝나지 않는다(디버그 로그 사고).
	[Test]
	public void Party_TryStartRally_BlockedWhileAnyMemberInEntranceSequence()
	{
		var room = new Room();
		var leader = NewHuman();
		var member = NewHuman();
		leader.currentRoom = member.currentRoom = room;
		member.isInDungeonEntranceSequence = true;
		var party = NewMopUpParty(leader, member);

		party.TryStartRally();
		Assert.IsFalse(party.IsRallyActive, "입구 시퀀스 중인 파티원이 있으면 집결하지 않는다");

		member.isInDungeonEntranceSequence = false;
		party.TryStartRally();
		Assert.IsTrue(party.IsRallyActive, "전원이 들어온 뒤에는 정상적으로 집결한다");
	}

	[Test]
	public void Party_TryStartRally_BlockedWhileLeaderCrossingStairs()
	{
		var leader = NewHuman();
		leader.currentRoom = new Room();
		leader.pendingStairTargetFloor = 1;
		var party = NewMopUpParty(leader);

		party.TryStartRally();
		Assert.IsFalse(party.IsRallyActive);

		leader.pendingStairTargetFloor = null;
		party.TryStartRally();
		Assert.IsTrue(party.IsRallyActive);
	}

	// 01번 7-1장: 소탕 파티는 "다음 문까지 이동하며 확인한 뒤"에야 문 주변 집결을 판단한다 — 적을 모른다는 것만으로는 스폰 직후에도 참이라
	// 도착 즉시 집결하던 문제. 문 앞 접근 표식이 있어야 하고, 집결이 시작되면 소비되며, 리더가 방을 떠나면 사라진다.
	[Test]
	public void Party_MopUp_RequiresDoorApproachBeforeRally()
	{
		var roomA = new Room();
		var leader = NewHuman();
		leader.currentRoom = roomA;
		var party = new Party("P", "테스트") { Type = PartyType.MopUp };
		party.Members.Add(leader);
		party.Leader = leader;

		Assert.IsFalse(party.IsRoomActivityComplete(), "문 앞 접근 전에는 적을 몰라도 방 활동이 끝난 게 아니다");
		party.TryStartRally();
		Assert.IsFalse(party.IsRallyActive);

		party.MarkLeaderReachedNextDoor();
		Assert.IsTrue(party.IsRoomActivityComplete());

		// 문 앞에 도착했어도 대응할 적(인지한 적)이 남아 있으면 아직 아니다.
		leader.personalSpottedEnemies.Add(NewHuman());
		Assert.IsFalse(party.IsRoomActivityComplete());
	}

	[Test]
	public void Party_MopUp_DoorApproachMarkIsConsumedByRallyAndClearedOnLeavingRoom()
	{
		var roomA = new Room();
		var roomB = new Room();
		var leader = NewHuman();
		leader.currentRoom = roomA;
		var party = new Party("P", "테스트") { Type = PartyType.MopUp };
		party.Members.Add(leader);
		party.Leader = leader;

		party.MarkLeaderReachedNextDoor();
		party.TryStartRally();
		Assert.IsTrue(party.IsRallyActive);
		Assert.IsNull(party.DoorApproachRoom, "집결이 시작되면 표식은 소비된다");

		// 표식만 있고 집결 전에 리더가 방을 떠나면(TickAdvanceState) 옛 표식은 사라진다.
		var other = new Party("Q", "테스트") { Type = PartyType.MopUp };
		other.Members.Add(leader);
		other.Leader = leader;
		other.MarkLeaderReachedNextDoor();
		leader.currentRoom = roomB;
		other.TickAdvanceState();
		Assert.IsNull(other.DoorApproachRoom);
		Assert.IsFalse(other.IsRoomActivityComplete(), "새 방에서는 다시 문 앞까지 가야 한다");
	}

	// 다른 종류(탐색 등)의 방 활동 종료 기준은 문 앞 접근 표식과 무관하다.
	[Test]
	public void Party_NonMopUp_UnaffectedByDoorApproachMark()
	{
		var leader = NewHuman();
		leader.currentRoom = new Room();
		var party = new Party("P", "테스트") { Type = PartyType.Occupy };
		party.Members.Add(leader);
		party.Leader = leader;
		bool before = party.IsRoomActivityComplete();
		party.MarkLeaderReachedNextDoor();
		Assert.AreEqual(before, party.IsRoomActivityComplete());
	}

	// 임무 수량 달성으로 문 앞 도착 전에 집결이 시작되면 리더의 문 접근 대기도 집결 대기로 전환된다.
	[Test]
	public void Party_TryStartRally_ConvertsLeaderDoorApproachToRally()
	{
		var room = new Room();
		var leader = NewHuman();
		leader.currentRoom = room;
		leader.currentWait = new WaitState { Reason = WaitReason.ApproachingNextDoor, WaitPosition = new Vector2Int(3, 3) };
		var party = new Party("P", "테스트") { Type = PartyType.Explore };
		party.Members.Add(leader);
		party.Leader = leader;
		party.JustReachedRoomExploreQuota = true; // 임무 수량 달성은 방 활동 종료와 별개의 집결 사유

		party.TryStartRally();

		Assert.IsTrue(party.IsRallyActive);
		Assert.AreEqual(WaitReason.AwaitingPartyAtRallyPoint, leader.currentWait.Reason);
	}

	// ── 검증 04-04: 공동 이동 속도(가장 느린 구성원) ──
	private static Human SpeedHuman(float walkSpeed, Vector2Int pos)
	{
		var h = NewHuman();
		h.BaseStat.walkSpeed = walkSpeed;
		h.position = pos;
		return h;
	}

	[Test]
	public void ResolveMoveBaseSpeed_Pure_CommonSpeedOnlyWhenCoMovingAndJoined_NeverFasterThanIndividual()
	{
		Assert.AreEqual(4f, PartyReportMath.ResolveMoveBaseSpeed(false, true, 4f, 3f), "공동 이동이 아니면 개인 속도");
		Assert.AreEqual(4f, PartyReportMath.ResolveMoveBaseSpeed(true, false, 4f, 3f), "합류 전(뒤처진 구성원)은 개인 속도");
		Assert.AreEqual(3f, PartyReportMath.ResolveMoveBaseSpeed(true, true, 4f, 3f), "합류한 공동 이동은 가장 느린 구성원 속도");
		Assert.AreEqual(3f, PartyReportMath.ResolveMoveBaseSpeed(true, true, 3f, 4f), "공동 속도가 개인 속도보다 빠르게 나오지 않는다");
		Assert.IsTrue(PartyReportMath.IsJoinedToLeader(true, 3, 3));
		Assert.IsFalse(PartyReportMath.IsJoinedToLeader(true, 4, 3));
		Assert.IsFalse(PartyReportMath.IsJoinedToLeader(false, 0, 3), "리더가 없거나 다른 층이면 합류가 아니다");
	}

	[Test]
	public void Party_ResolveMoveBaseSpeed_CoMovingJoinedMemberUsesSlowestMember_EvenIfThatMemberIsStopped()
	{
		var leader = SpeedHuman(4.0f, new Vector2Int(10, 10));
		var fast = SpeedHuman(3.6f, new Vector2Int(11, 10));
		var slowStopped = SpeedHuman(3.0f, new Vector2Int(9, 10)); // 상호작용으로 멈춰 있어도 공동 속도 계산에는 포함된다
		slowStopped.currentWait = null;
		var party = NewMopUpParty(leader, fast, slowStopped);
		leader.party = fast.party = slowStopped.party = party;
		fast.currentWait = new WaitState { Reason = WaitReason.AdvancingToNextRoom };
		leader.currentWait = new WaitState { Reason = WaitReason.AdvancingToNextRoom };

		Assert.AreEqual(3.0f, party.ResolveMoveBaseSpeed(fast), 0.001f);
		Assert.AreEqual(3.0f, party.ResolveMoveBaseSpeed(leader), 0.001f, "리더도 공동 이동 중이면 같은 공동 속도");
		Assert.AreEqual(3.0f, fast.AppliedWalkSpeed, 0.001f, "Human.AppliedWalkSpeed가 공동 속도를 쓴다");
	}

	[Test]
	public void Party_ResolveMoveBaseSpeed_IndividualBehaviorsAndStragglersKeepIndividualSpeed()
	{
		var leader = SpeedHuman(3.0f, new Vector2Int(10, 10));
		var fastNearby = SpeedHuman(4.0f, new Vector2Int(11, 10));
		var fastStraggler = SpeedHuman(4.0f, new Vector2Int(30, 10)); // 리더에서 멀리 떨어진 뒤처진 구성원
		var party = NewMopUpParty(leader, fastNearby, fastStraggler);
		leader.party = fastNearby.party = fastStraggler.party = party;

		Assert.AreEqual(4.0f, party.ResolveMoveBaseSpeed(fastNearby), 0.001f, "공동 이동이 아니면(대기 없음) 개인 속도");

		fastNearby.currentWait = new WaitState { Reason = WaitReason.ReportingCoreToLeader };
		Assert.AreEqual(4.0f, party.ResolveMoveBaseSpeed(fastNearby), 0.001f, "코어 보고 이동은 개인 속도");

		fastNearby.currentWait = new WaitState { Reason = WaitReason.SearchingNextDoor };
		fastStraggler.currentWait = new WaitState { Reason = WaitReason.SearchingNextDoor };
		Assert.AreEqual(3.0f, party.ResolveMoveBaseSpeed(fastNearby), 0.001f, "합류한 추종은 공동 속도");
		Assert.AreEqual(4.0f, party.ResolveMoveBaseSpeed(fastStraggler), 0.001f, "뒤처진 구성원은 합류 전까지 개인 속도로 복귀");
	}

	[Test]
	public void Party_ResolveMoveBaseSpeed_ExcludesDeadOtherFloorImmobileAndZeroSpeedMembers()
	{
		var leader = SpeedHuman(4.0f, new Vector2Int(10, 10));
		var dead = SpeedHuman(1.0f, new Vector2Int(10, 11));
		dead.hp = 0f;
		var otherFloor = SpeedHuman(1.0f, new Vector2Int(10, 12));
		otherFloor.currentFloor = 1;
		var immobile = SpeedHuman(1.0f, new Vector2Int(10, 13));
		immobile.isImmobile = true;
		var zeroSpeed = SpeedHuman(0f, new Vector2Int(10, 14));
		var party = NewMopUpParty(leader, dead, otherFloor, immobile, zeroSpeed);
		foreach (var m in party.Members) m.party = party;
		leader.currentWait = new WaitState { Reason = WaitReason.AdvancingToNextRoom };

		Assert.AreEqual(4.0f, party.ResolveMoveBaseSpeed(leader), 0.001f, "사망·다른 층·이동 불가·속도 0 구성원은 공동 속도를 끌어내리지 않는다");
	}

	[Test]
	public void Human_AppliedWalkSpeed_AlertSlowdownIsAppliedOnTopOfTheCommonSpeed()
	{
		var leader = SpeedHuman(3.0f, new Vector2Int(10, 10));
		var fast = SpeedHuman(4.0f, new Vector2Int(11, 10));
		var party = NewMopUpParty(leader, fast);
		leader.party = fast.party = party;
		fast.currentWait = new WaitState { Reason = WaitReason.AdvancingToNextRoom };
		fast.currentAlertSearch = new AlertSearchState(); // 경계 중인 개인은 공동 속도 위에 자기 감속을 곱한다

		Assert.AreEqual(3.0f * ExplorationMath.AlertMoveSpeedRatio, fast.AppliedWalkSpeed, 0.001f);
	}

	// 집결지에는 층이 기록되고, 다른 층 파티원에게는 집결 대기가 배정되지 않는다.
	[Test]
	public void Party_TryStartRally_RecordsFloorAndSkipsMembersOnOtherFloors()
	{
		var room = new Room();
		var leader = NewHuman();
		var sameFloor = NewHuman();
		var otherFloor = NewHuman();
		leader.currentRoom = sameFloor.currentRoom = otherFloor.currentRoom = room;
		leader.currentFloor = sameFloor.currentFloor = 1;
		otherFloor.currentFloor = 0;
		var party = NewMopUpParty(leader, sameFloor, otherFloor);

		party.TryStartRally();

		Assert.IsTrue(party.IsRallyActive);
		Assert.AreEqual(1, party.RallyFloor);
		Assert.IsTrue(party.IsRallyPointOnFloor(1));
		Assert.IsFalse(party.IsRallyPointOnFloor(0));
		Assert.AreEqual(WaitReason.AwaitingPartyAtRallyPoint, sameFloor.currentWait.Reason);
		Assert.AreEqual(1, sameFloor.currentWait.WaitFloor);
		Assert.IsNull(otherFloor.currentWait, "다른 층 파티원은 집결 대기를 받지 않는다");
		Assert.IsNull(otherFloor.knownRallyPoint);
	}
}
#endif
