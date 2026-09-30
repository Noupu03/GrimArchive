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
}
#endif
