#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;

// ========================================================================
// 리더 방 경로 결정(04번 1장, 검증 05-06 관찰 2) — 알려진 게이트 그래프에서 다음 게이트를 고르는 순수 계산(RoomRouteMath).
// 방 그래프는 Prim MST + 5% 루프라 막다른 방이 흔하므로 미방문 방 우선·막다른 방 되돌이를 고정한다.
// ========================================================================

public class RoomRouteTests
{
	private static RouteEdge E(int gate, int a, int b) => new RouteEdge(gate, a, b);

	private static HashSet<int> Visited(params int[] rooms) => new HashSet<int>(rooms);

	[Test]
	public void PicksTheGateToAnUnvisitedRoom_OverOneBackToAVisitedRoom()
	{
		var edges = new List<RouteEdge> { E(1, 0, 1), E(2, 0, 2) };
		bool ok = RoomRouteMath.TryPickGate(0, edges, Visited(0, 1), targetRoom: -1, null, out var picked);

		Assert.IsTrue(ok);
		Assert.AreEqual(2, picked.GateId); // 이미 지나온 방(1)이 아니라 미방문 방(2)으로 간다
	}

	[Test]
	public void GoesStraightToTheTargetRoom_WhenAGateLeadsThere()
	{
		var edges = new List<RouteEdge> { E(1, 0, 1), E(2, 0, 9), E(3, 0, 2) };
		bool ok = RoomRouteMath.TryPickGate(0, edges, Visited(0), targetRoom: 9, null, out var picked);

		Assert.IsTrue(ok);
		Assert.AreEqual(2, picked.GateId);
	}

	[Test]
	public void AmongUnvisitedRooms_TheLowerTieScoreWins()
	{
		var edges = new List<RouteEdge> { E(1, 0, 1), E(2, 0, 2) };
		float Score(RouteEdge e) => e.GateId == 1 ? 8f : 3f; // 게이트 2가 목표 방 중심에 더 가깝다

		bool ok = RoomRouteMath.TryPickGate(0, edges, Visited(0), targetRoom: -1, Score, out var picked);

		Assert.IsTrue(ok);
		Assert.AreEqual(2, picked.GateId);
	}

	[Test]
	public void FromADeadEndRoom_BacktracksThroughTheEntranceGate()
	{
		// 방 0(막다른 방) — 방 1(방문) — 방 2(미방문): 0에는 1로 가는 게이트뿐이라 되돌아간다.
		var edges = new List<RouteEdge> { E(1, 0, 1), E(2, 1, 2) };
		bool ok = RoomRouteMath.TryPickGate(0, edges, Visited(0, 1), targetRoom: -1, null, out var picked);

		Assert.IsTrue(ok);
		Assert.AreEqual(1, picked.GateId); // 미방문 방(2)으로 이어지는 방(1)으로 되돌아가는 첫 게이트
	}

	[Test]
	public void Backtracking_FollowsTheShortestChainOfVisitedRooms()
	{
		// 0 — 1 — 2 — 3(미방문), 0 — 4 — 5 — 6 — 7(미방문): 사슬이 짧은 쪽(0→1→2)으로 되돌아간다.
		var edges = new List<RouteEdge> { E(1, 0, 1), E(2, 1, 2), E(3, 2, 3), E(4, 0, 4), E(5, 4, 5), E(6, 5, 6), E(7, 6, 7) };
		bool ok = RoomRouteMath.TryPickGate(0, edges, Visited(0, 1, 2, 4, 5, 6), targetRoom: -1, null, out var picked);

		Assert.IsTrue(ok);
		Assert.AreEqual(1, picked.GateId);
	}

	[Test]
	public void Backtracking_TiesOnHopCount_AreBrokenByTheTieScoreOfTheFirstGate()
	{
		// 0 — 1 — 3(미방문), 0 — 2 — 4(미방문): 홉 수가 같아 첫 게이트의 점수가 낮은 쪽을 고른다.
		var edges = new List<RouteEdge> { E(1, 0, 1), E(2, 1, 3), E(3, 0, 2), E(4, 2, 4) };
		float Score(RouteEdge e) => e.GateId == 3 ? 1f : 5f;

		bool ok = RoomRouteMath.TryPickGate(0, edges, Visited(0, 1, 2), targetRoom: -1, Score, out var picked);

		Assert.IsTrue(ok);
		Assert.AreEqual(3, picked.GateId);
	}

	[Test]
	public void WhenEveryKnownRoomIsVisited_ThereIsNothingToPick()
	{
		var edges = new List<RouteEdge> { E(1, 0, 1), E(2, 1, 2) };
		bool ok = RoomRouteMath.TryPickGate(0, edges, Visited(0, 1, 2), targetRoom: -1, null, out _);

		Assert.IsFalse(ok, "알려진 게이트가 모두 방문한 방뿐이면 고르지 않는다(호출부가 기존 방식으로 폴백)");
	}

	[Test]
	public void NoKnownGates_MeansNoDecision()
	{
		Assert.IsFalse(RoomRouteMath.TryPickGate(0, new List<RouteEdge>(), Visited(0), targetRoom: 5, null, out _));
		Assert.IsFalse(RoomRouteMath.TryPickGate(0, null, Visited(0), targetRoom: 5, null, out _));
	}

	[Test]
	public void Backtracking_DoesNotSeeGatesBeyondUnvisitedRooms()
	{
		// 미방문 방(2)은 들어가 보기 전엔 그 너머(3)의 간선도 모른다 — 호출부가 아는 간선만 넘기므로 3으로 이어지는 간선이 없으면 되돌이 대상이 없다.
		var edges = new List<RouteEdge> { E(1, 0, 1) };
		bool ok = RoomRouteMath.TryPickGate(0, edges, Visited(0, 1), targetRoom: -1, null, out _);

		Assert.IsFalse(ok);
	}

	[Test]
	public void TheTargetRoomCountsAsAFrontier_WhenBacktracking()
	{
		// 0(막다른 방) — 1(방문) — 9(목표 방, 아직 방문 전): 목표 방으로 이어지는 1까지 되돌아간다.
		var edges = new List<RouteEdge> { E(1, 0, 1), E(2, 1, 9) };
		bool ok = RoomRouteMath.TryPickGate(0, edges, Visited(0, 1), targetRoom: 9, null, out var picked);

		Assert.IsTrue(ok);
		Assert.AreEqual(1, picked.GateId);
	}
}
#endif
