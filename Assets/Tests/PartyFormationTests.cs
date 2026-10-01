#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 집결 개선(2026-10-01 플레이 로그 분석): 집결 자리 점진 확장 / 문 앞 진형 기하 / 레인 배정 / 문 파괴 행·공격 자리·배정 / 입장 순서 — 전부 순수 함수(PartyFormationMath)라 세션 없이 EditMode에서 돈다.
// 0층 진형(DungeonEntranceSystem)이 쓰던 랭크·레인 규칙을 옮긴 것이므로 LaneOffset/SlotTarget은 0층 공식 그대로여야 한다.
// ========================================================================

public class PartyFormationTests
{
	// ── 랭크·레인(0층 진형 규칙 그대로) ───────────────────────────────────

	[Test]
	public void LaneOffset_OddCountIncludesCenter_EvenCountSkipsCenter()
	{
		Assert.AreEqual(new[] { -1, 0, 1 }, new[] { PartyFormationMath.LaneOffset(0, 3), PartyFormationMath.LaneOffset(1, 3), PartyFormationMath.LaneOffset(2, 3) });
		Assert.AreEqual(new[] { -2, -1, 1, 2 }, new[]
		{
			PartyFormationMath.LaneOffset(0, 4), PartyFormationMath.LaneOffset(1, 4), PartyFormationMath.LaneOffset(2, 4), PartyFormationMath.LaneOffset(3, 4)
		});
		Assert.AreEqual(0, PartyFormationMath.LaneOffset(0, 1));
	}

	[Test]
	public void ResolveRank_LeaderIsAlwaysRank1_MeleeFront_RangedBack()
	{
		Assert.AreEqual(1, PartyFormationMath.ResolveRank(isLeader: true, isMelee: true));
		Assert.AreEqual(1, PartyFormationMath.ResolveRank(isLeader: true, isMelee: false));
		Assert.AreEqual(0, PartyFormationMath.ResolveRank(isLeader: false, isMelee: true));
		Assert.AreEqual(2, PartyFormationMath.ResolveRank(isLeader: false, isMelee: false));
	}

	[Test]
	public void IsMeleeEngage_ThresholdIsThree()
	{
		Assert.IsTrue(PartyFormationMath.IsMeleeEngage(2));   // 기사형
		Assert.IsTrue(PartyFormationMath.IsMeleeEngage(3));
		Assert.IsFalse(PartyFormationMath.IsMeleeEngage(7));  // 아처형
	}

	[Test]
	public void SlotTarget_MatchesFloor0Formula_ForHorizontalMarch()
	{
		// DungeonEntranceSystem: new Vector2Int(frontX - Rank * 2 * dir, rowY + Lane). 동쪽(+x)으로 행진, 앞 기준 (20,10).
		var anchor = new Vector2Int(20, 10);
		var forward = Vector2Int.right;
		Assert.AreEqual(new Vector2Int(20, 11), PartyFormationMath.SlotTarget(anchor, forward, 0, 1));
		Assert.AreEqual(new Vector2Int(18, 10), PartyFormationMath.SlotTarget(anchor, forward, 1, 0));
		Assert.AreEqual(new Vector2Int(16, 9), PartyFormationMath.SlotTarget(anchor, forward, 2, -1));
		// 서쪽(-x)으로 행진하면 뒤가 +x다.
		Assert.AreEqual(new Vector2Int(22, 10), PartyFormationMath.SlotTarget(anchor, Vector2Int.left, 1, 0));
	}

	[Test]
	public void SlotTarget_VerticalMarch_LanesSpreadAlongX()
	{
		// 위(+y)로 문을 향하는 방: 0랭크는 앞(문쪽), 레인은 x축으로 퍼진다.
		var anchor = new Vector2Int(6, 46);
		Assert.AreEqual(new Vector2Int(5, 46), PartyFormationMath.SlotTarget(anchor, Vector2Int.up, 0, -1));
		Assert.AreEqual(new Vector2Int(6, 44), PartyFormationMath.SlotTarget(anchor, Vector2Int.up, 1, 0));
		Assert.AreEqual(new Vector2Int(7, 42), PartyFormationMath.SlotTarget(anchor, Vector2Int.up, 2, 1));
	}

	[Test]
	public void EntrySlotTarget_FrontRankIsDeepest_RearRankJustInsideDoor()
	{
		// far 줄 중앙 (6,48), 위로 입장: 0랭크가 가장 안쪽(+5), 2랭크가 문 바로 안쪽(+1).
		var far = new Vector2Int(6, 48);
		Assert.AreEqual(new Vector2Int(6, 53), PartyFormationMath.EntrySlotTarget(far, Vector2Int.up, 0, 0));
		Assert.AreEqual(new Vector2Int(6, 51), PartyFormationMath.EntrySlotTarget(far, Vector2Int.up, 1, 0));
		Assert.AreEqual(new Vector2Int(5, 49), PartyFormationMath.EntrySlotTarget(far, Vector2Int.up, 2, -1));
	}

	// ── 레인 배정(교차 없음) ──────────────────────────────────────────────

	[Test]
	public void AssignLanes_FollowsLateralOrder_SoUnitsDoNotCross()
	{
		// 현재 가로 위치가 7, 3, 5인 세 유닛 → 3이 가장 왼쪽(-1), 5가 중앙(0), 7이 오른쪽(+1).
		Assert.AreEqual(new[] { 1, -1, 0 }, PartyFormationMath.AssignLanes(new[] { 7, 3, 5 }));
	}

	[Test]
	public void AssignLanes_TiesBrokenByInputOrder_Deterministic()
	{
		Assert.AreEqual(new[] { -1, 1 }, PartyFormationMath.AssignLanes(new[] { 4, 4 }));
		Assert.AreEqual(0, PartyFormationMath.AssignLanes(new[] { 9 })[0]);
	}

	// ── 근처 자리 점진 확장 ───────────────────────────────────────────────

	[Test]
	public void TryPickNearestFreeTile_PicksIdealItselfWhenFree_AndClaimsIt()
	{
		var claimed = new HashSet<Vector2Int>();
		Assert.IsTrue(PartyFormationMath.TryPickNearestFreeTile(new Vector2Int(5, 5), _ => true, claimed, 12, out var p));
		Assert.AreEqual(new Vector2Int(5, 5), p);
		Assert.IsTrue(claimed.Contains(p));
	}

	[Test]
	public void TryPickNearestFreeTile_ExpandsRadiusProgressively_WhenInnerRingsBlocked()
	{
		// 반경 2 안은 전부 막힘 → 반경 3 링에서 고른다(고정 반경이 아니라 점진 확장).
		var ideal = new Vector2Int(10, 10);
		bool IsFree(Vector2Int t) => Mathf.Max(Mathf.Abs(t.x - ideal.x), Mathf.Abs(t.y - ideal.y)) >= 3;
		Assert.IsTrue(PartyFormationMath.TryPickNearestFreeTile(ideal, IsFree, new HashSet<Vector2Int>(), 12, out var p));
		Assert.AreEqual(3, Mathf.Max(Mathf.Abs(p.x - ideal.x), Mathf.Abs(p.y - ideal.y)));
	}

	[Test]
	public void TryPickNearestFreeTile_PrefersEuclideanNearerTileWithinSameRing()
	{
		// 반경 1 링에서 (11,10)만 비어 있고 대각은 막힘이어도 거리가 같은 후보 중 가까운 쪽(직교)을 고른다.
		var ideal = new Vector2Int(10, 10);
		bool IsFree(Vector2Int t) => t == new Vector2Int(11, 10) || t == new Vector2Int(11, 11);
		Assert.IsTrue(PartyFormationMath.TryPickNearestFreeTile(ideal, IsFree, null, 12, out var p));
		Assert.AreEqual(new Vector2Int(11, 10), p);
	}

	[Test]
	public void TryPickNearestFreeTile_SkipsClaimed_SoTwoUnitsNeverGetSameSlot()
	{
		// 로그의 주술사·음유시인이 둘 다 (4,46)을 고르던 경합: 두 번째 호출은 이미 고른 칸을 건너뛴다.
		var claimed = new HashSet<Vector2Int>();
		var ideal = new Vector2Int(4, 46);
		Assert.IsTrue(PartyFormationMath.TryPickNearestFreeTile(ideal, _ => true, claimed, 12, out var a));
		Assert.IsTrue(PartyFormationMath.TryPickNearestFreeTile(ideal, _ => true, claimed, 12, out var b));
		Assert.AreNotEqual(a, b);
	}

	[Test]
	public void TryPickNearestFreeTile_ReturnsFalse_WhenNothingFreeWithinMaxRadius()
	{
		Assert.IsFalse(PartyFormationMath.TryPickNearestFreeTile(Vector2Int.zero, _ => false, new HashSet<Vector2Int>(), 4, out _));
	}

	[Test]
	public void TryPickNearestFreeTile_NineUnitsAroundLeader_AllGetDistinctSlotsWithinRadiusTwo()
	{
		// 리더 자리(집결지)는 이미 점유 — 아홉 명이 반경 2 안에서 서로 다른 자리를 받는다(집결지 하나로 몰리던 예전 동작 대체).
		var rally = new Vector2Int(6, 45);
		var claimed = new HashSet<Vector2Int> { rally };
		var slots = new HashSet<Vector2Int>();
		for (int i = 0; i < 9; i++)
		{
			Assert.IsTrue(PartyFormationMath.TryPickNearestFreeTile(rally, _ => true, claimed, 12, out var s));
			Assert.IsTrue(slots.Add(s), "자리 중복");
			int radius = Mathf.Max(Mathf.Abs(s.x - rally.x), Mathf.Abs(s.y - rally.y));
			// 반경 1 링은 8칸이라 앞의 8명은 거기 들어가고, 9번째만 반경 2로 넓어진다.
			Assert.LessOrEqual(radius, i < 8 ? 1 : 2, $"{i}번째 자리 반경");
		}
	}

	// ── 좁은 길: 구역·재선택 기억(2026-10-01 플레이 로그 2차 — 아처형이 (7,46)↔(3,46)을 오가며 집결이 끝나지 않음) ──

	[Test]
	public void ZoneRadius_IsFarthestAssignedSlot_MinOne()
	{
		var center = new Vector2Int(5, 46);
		Assert.AreEqual(3, PartyFormationMath.ZoneRadius(new[] { new Vector2Int(3, 46), new Vector2Int(7, 44), new Vector2Int(8, 46) }, center));
		Assert.AreEqual(1, PartyFormationMath.ZoneRadius(new Vector2Int[0], center));
		Assert.AreEqual(1, PartyFormationMath.ZoneRadius(new[] { new Vector2Int(5, 46) }, center));
	}

	[Test]
	public void IsWithinZone_AllowsOneTileSlack()
	{
		var center = new Vector2Int(5, 46);
		Assert.IsTrue(PartyFormationMath.IsWithinZone(new Vector2Int(9, 46), center, 3));   // 반경 3 + 여유 1
		Assert.IsFalse(PartyFormationMath.IsWithinZone(new Vector2Int(10, 46), center, 3));
		Assert.IsTrue(PartyFormationMath.IsWithinZone(new Vector2Int(5, 42), center, 3));
	}

	[Test]
	public void RepickNearestToSelf_WithRejectedMemory_NeverRevisitsASlot_InOneWideCorridor()
	{
		// 폭 1칸 복도(x 0~10, y 46)에서 막힐 때마다 "나에게 가까운 구역 안 빈 타일"을 다시 고른다. 막혀 포기한 자리를 기억하면 같은 자리를 두 번 고르지 않고
		// 구역 안 후보(x 1~9, 9칸)가 정확히 한 번씩 소진된다 — 예전엔 집결지에서 가까운 자리를 매번 새로 골라 양 끝을 무한히 오갔다.
		var self = new Vector2Int(9, 46);
		var center = new Vector2Int(5, 46);
		var rejected = new HashSet<Vector2Int>();
		var seen = new HashSet<Vector2Int>();
		int picks = 0;
		while (PartyFormationMath.TryPickNearestFreeTile(self,
				t => t.y == 46 && t.x >= 0 && t.x <= 10 && !rejected.Contains(t) && PartyFormationMath.IsWithinZone(t, center, 3),
				null, 12, out var p))
		{
			Assert.IsTrue(seen.Add(p), "같은 자리를 다시 골랐다");
			rejected.Add(p);
			Assert.LessOrEqual(++picks, 20, "후보가 소진되지 않는다");
		}
		Assert.AreEqual(9, picks);
	}

	[Test]
	public void RepickNearestToSelf_PicksOwnTileFirst_SoAStuckUnitInsideZoneSettlesWhereItStands()
	{
		// 자기 타일이 구역 안이고 비어 있으면 가장 먼저 뽑힌다(반경 0) — 더 못 들어가는 유닛이 그 자리에서 집결 처리되는 경로.
		var self = new Vector2Int(7, 46);
		Assert.IsTrue(PartyFormationMath.TryPickNearestFreeTile(self, t => PartyFormationMath.IsWithinZone(t, new Vector2Int(5, 46), 3), null, 12, out var p));
		Assert.AreEqual(self, p);
	}

	[Test]
	public void RepickNearestToSelf_OutsideZone_PicksNearestTileInsideZone()
	{
		// 구역 밖(x=14)에서 막힌 유닛은 구역 안에서 자기에게 가장 가까운 칸(x=9)으로 들어온다.
		var self = new Vector2Int(14, 46);
		Assert.IsTrue(PartyFormationMath.TryPickNearestFreeTile(self,
			t => t.y == 46 && t.x >= 0 && t.x <= 20 && PartyFormationMath.IsWithinZone(t, new Vector2Int(5, 46), 3), null, 12, out var p));
		Assert.AreEqual(new Vector2Int(9, 46), p);
	}

	// ── 문 앞 통과 구간(05번 3장 "문 바로 앞 통과 구간 2칸을 비워 둔다") ──────────────

	[Test]
	public void IsInDoorClearance_BlocksTilesWithinOneOfAnyDoorTile_NotTwo()
	{
		var doors = new[] { new Vector2Int(6, 47), new Vector2Int(7, 47) };
		Assert.IsTrue(PartyFormationMath.IsInDoorClearance(new Vector2Int(6, 46), doors));   // 문 바로 앞
		Assert.IsTrue(PartyFormationMath.IsInDoorClearance(new Vector2Int(8, 46), doors));   // 오른쪽 문 타일의 대각 인접
		Assert.IsFalse(PartyFormationMath.IsInDoorClearance(new Vector2Int(6, 45), doors));  // 문에서 2칸 — FindDoorWaitSlot의 최소 반경과 같다
		Assert.IsFalse(PartyFormationMath.IsInDoorClearance(new Vector2Int(9, 46), doors));
	}

	[Test]
	public void FrontRankAnchoredAtDoorClearance_StandsOutsideTheZone_YetOneStepFromAdjacency()
	{
		// 0랭크 대기 자리는 near 줄에서 DoorClearance(2)칸 뒤 — 구간 밖이고, 파괴 단계에서 한 걸음이면 문 인접(1칸)에 닿는다.
		var nearRow = new[] { new Vector2Int(6, 47), new Vector2Int(7, 47) };
		var anchor = nearRow[0] - Vector2Int.up * PartyFormationMath.DoorClearance;
		var slot = PartyFormationMath.SlotTarget(anchor, Vector2Int.up, 0, 0);
		Assert.AreEqual(new Vector2Int(6, 45), slot);
		Assert.IsFalse(PartyFormationMath.IsInDoorClearance(slot, nearRow));
		Assert.AreEqual(2, Mathf.Max(Mathf.Abs(slot.x - nearRow[0].x), Mathf.Abs(slot.y - nearRow[0].y)));
		// 1랭크는 2칸 더 뒤(간격 2)라 구간과 겹치지 않는다.
		Assert.IsFalse(PartyFormationMath.IsInDoorClearance(PartyFormationMath.SlotTarget(anchor, Vector2Int.up, 1, 0), nearRow));
	}

	[Test]
	public void IsAtSlot_WithinOneTile()
	{
		Assert.IsTrue(PartyFormationMath.IsAtSlot(new Vector2Int(5, 5), new Vector2Int(5, 5)));
		Assert.IsTrue(PartyFormationMath.IsAtSlot(new Vector2Int(5, 5), new Vector2Int(6, 6)));
		Assert.IsFalse(PartyFormationMath.IsAtSlot(new Vector2Int(5, 5), new Vector2Int(7, 5)));
	}

	// ── 문 파괴: 행 선택·공격 자리·최근접 배정 ────────────────────────────

	[Test]
	public void NextBreachRow_NearFirst_ThenFar_ThenDone()
	{
		Assert.AreEqual(0, PartyFormationMath.NextBreachRow(true, true));
		Assert.AreEqual(0, PartyFormationMath.NextBreachRow(true, false));
		Assert.AreEqual(1, PartyFormationMath.NextBreachRow(false, true)); // near는 이미 부서졌고 far만 남음
		Assert.AreEqual(-1, PartyFormationMath.NextBreachRow(false, false));
	}

	// 수직 게이트: near 줄 y=47(x 6,7), far 줄 y=48. 방은 y<=46, 벽은 x<=5와 x>=8의 문 줄(y 47·48).
	private static bool StandableInRoomFrontRow(Vector2Int t) => t.y <= 46 && t.x >= 3 && t.x <= 10;

	[Test]
	public void AttackSlotsAround_NearDoor_CollectsTheFrontRow_ExcludingDoorTilesAndWalls()
	{
		var near = new[] { new Vector2Int(6, 47), new Vector2Int(7, 47) };
		// 문 줄 옆(5,47)·(8,47)은 벽이라 서 있을 수 없다.
		bool Standable(Vector2Int t) => StandableInRoomFrontRow(t);

		var slots = PartyFormationMath.AttackSlotsAround(near, Standable);

		// 앞줄 y=46의 x=5..8 네 칸 — 문 칸·벽·문 너머는 빠진다.
		Assert.AreEqual(new[] { new Vector2Int(5, 46), new Vector2Int(6, 46), new Vector2Int(7, 46), new Vector2Int(8, 46) }, slots.ToArray());
	}

	[Test]
	public void AttackSlotsAround_FarDoorAfterNearIsOpen_OnlyTheTwoOpenedTiles()
	{
		var far = new[] { new Vector2Int(6, 48), new Vector2Int(7, 48) };
		// near 줄 두 칸이 열렸다 — 그 줄의 양옆(5,47)·(8,47)은 벽. far 문 너머(y=49)는 아직 닿을 수 없다.
		bool Standable(Vector2Int t) => (t.y == 47 && (t.x == 6 || t.x == 7));

		var slots = PartyFormationMath.AttackSlotsAround(far, Standable);

		Assert.AreEqual(new[] { new Vector2Int(6, 47), new Vector2Int(7, 47) }, slots.ToArray());
	}

	[Test]
	public void AttackSlotsAround_NothingStandable_ReturnsEmpty_SoNobodyIsSentToTheDoor()
	{
		Assert.AreEqual(0, PartyFormationMath.AttackSlotsAround(new[] { new Vector2Int(6, 47) }, _ => false).Count);
	}

	[Test]
	public void AssignNearest_EveryoneGetsAnAdjacentSlot_WhenSlotsRemain()
	{
		var slots = new[] { new Vector2Int(5, 46), new Vector2Int(6, 46), new Vector2Int(7, 46), new Vector2Int(8, 46) };
		var positions = new[] { new Vector2Int(5, 40), new Vector2Int(8, 40), new Vector2Int(6, 41) };
		var priorities = new[] { 0, 0, 0 };

		int[] pick = PartyFormationMath.AssignNearest(positions, priorities, slots);

		Assert.AreEqual(new[] { 0, 3, 1 }, pick);   // 각자 자기 바로 위 칸
	}

	[Test]
	public void AssignNearest_MeleeFirst_RangedNext_LeaderLast_WhenSlotsAreScarce()
	{
		var slots = new[] { new Vector2Int(6, 46), new Vector2Int(7, 46) };
		// 입력 순서: 리더(우선 2), 원거리(우선 1), 근접(우선 0), 근접(우선 0) — 자리는 둘뿐이라 근접 둘이 차지하고 나머지는 -1.
		var positions = new[] { new Vector2Int(6, 45), new Vector2Int(6, 44), new Vector2Int(7, 43), new Vector2Int(6, 43) };
		var priorities = new[] { 2, 1, 0, 0 };

		int[] pick = PartyFormationMath.AssignNearest(positions, priorities, slots);

		Assert.AreEqual(-1, pick[0]);
		Assert.AreEqual(-1, pick[1]);
		Assert.AreEqual(1, pick[2]);   // (7,43) → (7,46)
		Assert.AreEqual(0, pick[3]);   // (6,43) → (6,46)
	}

	[Test]
	public void AssignNearest_TiesGoToTheLowerSlotNumber_AndTheLowerMemberIndexGoesFirst()
	{
		var slots = new[] { new Vector2Int(5, 46), new Vector2Int(7, 46) };
		var positions = new[] { new Vector2Int(6, 40), new Vector2Int(6, 40) };   // 두 슬롯까지 거리가 같다

		int[] pick = PartyFormationMath.AssignNearest(positions, new[] { 0, 0 }, slots);

		Assert.AreEqual(new[] { 0, 1 }, pick);
	}

	[Test]
	public void AssignNearest_HonorsPerMemberExclusions_SoARejectedSlotGoesToSomeoneElse()
	{
		var slots = new[] { new Vector2Int(6, 46), new Vector2Int(7, 46) };
		var positions = new[] { new Vector2Int(6, 44), new Vector2Int(7, 44) };

		// 0번 멤버는 자리 0을 이미 막혀 포기했다 — 가장 가까운 자리를 못 쓰고 자리 1로 간다.
		int[] pick = PartyFormationMath.AssignNearest(positions, new[] { 0, 0 }, slots, (m, s) => !(m == 0 && s == 0));

		Assert.AreEqual(new[] { 1, 0 }, pick);
	}

	// ── 입장 순서 ─────────────────────────────────────────────────────────

	[Test]
	public void HasPassedGate_RequiresBeingBeyondFarRow()
	{
		var far = new Vector2Int(6, 48);
		Assert.IsFalse(PartyFormationMath.HasPassedGate(new Vector2Int(6, 47), far, Vector2Int.up)); // near 줄
		Assert.IsFalse(PartyFormationMath.HasPassedGate(new Vector2Int(6, 48), far, Vector2Int.up)); // far 줄 위
		Assert.IsTrue(PartyFormationMath.HasPassedGate(new Vector2Int(6, 49), far, Vector2Int.up));
		// 아래(-y)로 가는 문이면 부호가 반대다.
		Assert.IsTrue(PartyFormationMath.HasPassedGate(new Vector2Int(6, 47), far, Vector2Int.down));
	}

	[Test]
	public void CanReleaseRank_WaitsForAllLowerRanksToPass()
	{
		var ranks = new[] { 0, 0, 1, 2 };
		// 0랭크 하나가 아직 못 지남 → 1·2랭크는 출발 불가, 0랭크는 항상 가능.
		var passed = new[] { true, false, false, false };
		Assert.IsTrue(PartyFormationMath.CanReleaseRank(0, ranks, passed));
		Assert.IsFalse(PartyFormationMath.CanReleaseRank(1, ranks, passed));
		Assert.IsFalse(PartyFormationMath.CanReleaseRank(2, ranks, passed));
		// 0랭크 전원 통과 → 1랭크 출발, 2랭크는 1랭크가 지나야.
		passed = new[] { true, true, false, false };
		Assert.IsTrue(PartyFormationMath.CanReleaseRank(1, ranks, passed));
		Assert.IsFalse(PartyFormationMath.CanReleaseRank(2, ranks, passed));
		passed = new[] { true, true, true, false };
		Assert.IsTrue(PartyFormationMath.CanReleaseRank(2, ranks, passed));
	}

	[Test]
	public void CanReleaseRank_EmptyLowerRanks_DoNotBlock()
	{
		// 근접이 없는 파티(0랭크 없음)는 리더(1랭크)가 바로 출발한다.
		Assert.IsTrue(PartyFormationMath.CanReleaseRank(1, new[] { 1, 2 }, new[] { false, false }));
	}
}
#endif
