#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 04번 4장 '역할별 이동 경로와 우회' 순수 계산 검증(NUnit, #if UNITY_INCLUDE_TESTS).
// ========================================================================

public class MovementSystemTests
{
	// '회피 가능 경로 우선 → 노출시간 최소 → 전체길이 최소' 3단계 우선순위가 가중치 하나로 성립하려면 회피 비용 상수가 기본 경로비용 차이보다 압도적으로 커야 한다 — 맵이 커져도 이 마진이 유지되는지 회귀 검증한다.
	[Test]
	public void AttackRangeAvoidExtraCost_DominatesTypicalPathCost()
	{
		// 대각선 이동(비용14) 기준 극단적으로 큰 경로(변 길이 1000칸)의 비용차보다 커야 한다.
		const int extremePathCostDifference = 1000 * 14;
		Assert.Greater(MovementMath.AttackRangeAvoidExtraCost, extremePathCostDifference);
	}

	// '보호 시작 조건 HP'(EmergencyProtectHpRatio)와 '피해 감수 시 잔여 HP 허용 하한'(ProtectApproachDamageRiskHpFloor)은 우연히 같은 수치(0.30f)지만 원문이 '조정 가능한 별도 값'이라 명시한 별개 상수다(02번 9항) — 하나로 통합되지 않았는지 검증한다.
	[Test]
	public void ProtectApproachDamageRiskHpFloor_IsIndependentConstant()
	{
		Assert.AreEqual(0.30f, CombatScoreMath.ProtectApproachDamageRiskHpFloor, 0.0001f);
		Assert.AreEqual(0.30f, CombatScoreMath.EmergencyProtectHpRatio, 0.0001f);
	}

	// 02번 5장 "새 대상을 공격할 수 있는 위치까지의 경로 길이": 경로(시작 제외, 마지막 = 대상 타일)를 따라 처음 공격 거리 안에 드는 지점까지의 걸음 수.
	private static List<Vector2Int> StraightPathTo(int targetX)
	{
		var path = new List<Vector2Int>();
		for (int x = 1; x <= targetX; x++) path.Add(new Vector2Int(x, 0));
		return path;
	}

	[Test]
	public void StepsToFirstTileWithinRange_StopsAtFirstTileInAttackReach()
	{
		var target = new Vector2Int(10, 0);
		var path = StraightPathTo(10);

		Assert.AreEqual(9, CombatScoreMath.StepsToFirstTileWithinRange(path, target, 1));  // 근접: 인접 타일(9,0)까지 9걸음
		Assert.AreEqual(6, CombatScoreMath.StepsToFirstTileWithinRange(path, target, 4));  // 원거리 4칸: (6,0)에서 이미 공격 가능
		Assert.AreEqual(1, CombatScoreMath.StepsToFirstTileWithinRange(path, target, 20)); // 첫 걸음부터 사거리 안 — 호출부가 시작 위치 사거리를 먼저 0칸으로 처리한다
	}

	// 목표가 여러 타일을 차지하면(보스 3×3) 앵커 한 점이 아니라 점유 영역까지의 거리로 잰다.
	[Test]
	public void DistanceToFootprint_MeasuresToTheNearestOccupiedTile()
	{
		var anchor = new Vector2Int(10, 3);
		var size = new Vector2Int(3, 3); // (10..12, 3..5)

		Assert.AreEqual(0, MovementMath.DistanceToFootprint(new Vector2Int(11, 4), anchor, size)); // 영역 안
		Assert.AreEqual(1, MovementMath.DistanceToFootprint(new Vector2Int(13, 3), anchor, size)); // 오른쪽 인접 — 앵커까지는 3이다
		Assert.AreEqual(1, MovementMath.DistanceToFootprint(new Vector2Int(9, 2), anchor, size));  // 대각 인접
		Assert.AreEqual(2, MovementMath.DistanceToFootprint(new Vector2Int(11, 7), anchor, size)); // 위쪽 2칸 (영역은 y 3~5)
		Assert.AreEqual(3, MovementMath.DistanceToFootprint(new Vector2Int(7, 3), anchor, size));
		Assert.AreEqual(2, MovementMath.DistanceToFootprint(new Vector2Int(8, 3), anchor, Vector2Int.one)); // 1×1이면 앵커까지의 체비셰프 거리
	}

	[Test]
	public void StepsToFirstTileWithinRange_UsesTheFootprintOfALargeTarget()
	{
		var anchor = new Vector2Int(10, 0);
		var size = new Vector2Int(3, 3); // (10..12, 0..2)
		var path = StraightPathTo(10);   // (1,0)…(10,0) — 마지막 타일이 앵커

		Assert.AreEqual(9, CombatScoreMath.StepsToFirstTileWithinRange(path, anchor, size, 1)); // (9,0)이 영역에 인접
		Assert.AreEqual(9, CombatScoreMath.StepsToFirstTileWithinRange(path, anchor, Vector2Int.one, 1)); // 1×1도 같은 (9,0) — 영역이 3×3이라 다른 경로에선 달라질 수 있다
	}

	// ── 02번 3~4장: 후보 중 점수로 고르기 / 보스 집중 중 임시 위협 대응의 유지 기준(ThreatResponseMath) ──
	private class Cand
	{
		public float Score;
		public float Dist;
	}

	[Test]
	public void PickBest_HighestScore_ThenNearest_ThenChosenAmongFullTies()
	{
		var low = new Cand { Score = 1f, Dist = 1f };
		var farHigh = new Cand { Score = 2f, Dist = 9f };
		var nearHigh1 = new Cand { Score = 2f, Dist = 3f };
		var nearHigh2 = new Cand { Score = 2f, Dist = 3f }; // nearHigh1과 점수·거리까지 완전 동점
		var pool = new List<Cand> { low, farHigh, nearHigh1, nearHigh2 };

		var first = ThreatResponseMath.PickBest(pool, c => c.Score, c => c.Dist, n => 0, out float bestScore, out float bestDist);
		Assert.AreSame(nearHigh1, first);       // 점수 높은 순 → 거리 가까운 순, 완전 동점은 pickIndex가 정한다
		Assert.AreEqual(2f, bestScore, 0.0001f);
		Assert.AreEqual(3f, bestDist, 0.0001f);

		var second = ThreatResponseMath.PickBest(pool, c => c.Score, c => c.Dist, n => 1, out _, out _);
		Assert.AreSame(nearHigh2, second);

		Assert.IsNull(ThreatResponseMath.PickBest(new List<Cand>(), c => c.Score, c => c.Dist, n => 0, out _, out _));
	}

	// 02번 3장 "새 대상의 점수가 현재 대상의 1.2배 이상" — 경계(정확히 1.2배)는 교체, 동점·양쪽 0은 유지, 몬스터·야생은 더 높기만 하면 교체.
	[Test]
	public void ShouldSwitchAttackTarget_Human_SwitchesAtExactlyOnePointTwoTimes_AndKeepsOnTiesAndBothZero()
	{
		Assert.IsTrue(CombatScoreMath.ShouldSwitchAttackTarget(true, 10f, 12f));
		Assert.IsTrue(CombatScoreMath.ShouldSwitchAttackTarget(true, 100f, 120f)); // 100 × 1.2f = 120.00001 — 허용 오차 없이는 정확히 1.2배인데도 유지돼 버린다
		Assert.IsTrue(CombatScoreMath.ShouldSwitchAttackTarget(true, 50f, 60f));
		Assert.IsFalse(CombatScoreMath.ShouldSwitchAttackTarget(true, 10f, 11.9f));
		Assert.IsFalse(CombatScoreMath.ShouldSwitchAttackTarget(true, 10f, 10f));
		Assert.IsFalse(CombatScoreMath.ShouldSwitchAttackTarget(true, 0f, 0f));
		Assert.IsTrue(CombatScoreMath.ShouldSwitchAttackTarget(true, 0f, 1f));
		Assert.IsTrue(CombatScoreMath.ShouldSwitchAttackTarget(false, 10f, 10.1f));
		Assert.IsFalse(CombatScoreMath.ShouldSwitchAttackTarget(false, 10f, 10f));
	}

	[Test]
	public void SelectWithHysteresis_CurrentInsideTheSet_IsKeptUnlessTheSwitchCriterionIsMet()
	{
		var current = new Cand { Score = 10f, Dist = 1f };
		var rival = new Cand { Score = 11f, Dist = 1f };
		var set = new List<Cand> { current, rival };

		// 인류는 현재 점수의 1.2배 이상일 때만 교체 — 11 < 12라 유지한다. 몬스터는 더 높기만 하면 교체.
		Assert.AreSame(current, ThreatResponseMath.SelectWithHysteresis(set, current, true, c => c.Score, c => c.Dist, n => 0, CombatScoreMath.ShouldSwitchAttackTarget));
		Assert.AreSame(rival, ThreatResponseMath.SelectWithHysteresis(set, current, false, c => c.Score, c => c.Dist, n => 0, CombatScoreMath.ShouldSwitchAttackTarget));

		rival.Score = 12.5f; // 1.2배 이상
		Assert.AreSame(rival, ThreatResponseMath.SelectWithHysteresis(set, current, true, c => c.Score, c => c.Dist, n => 0, CombatScoreMath.ShouldSwitchAttackTarget));
	}

	// 보스 집중 중 임시 대응: 지금 공격하던 보스는 위협 집합 밖이라 20% 게이트 없이 가장 점수 높은 직접 위협으로 바로 갈아탄다(그래서 보스 점수가 훨씬 높아도 대응한다).
	[Test]
	public void SelectWithHysteresis_CurrentOutsideTheSet_HasNoGate()
	{
		var boss = new Cand { Score = 100f, Dist = 5f };
		var mob = new Cand { Score = 1f, Dist = 1f };
		var directThreats = new List<Cand> { mob };

		Assert.AreSame(mob, ThreatResponseMath.SelectWithHysteresis(directThreats, boss, true, c => c.Score, c => c.Dist, n => 0, CombatScoreMath.ShouldSwitchAttackTarget));
		Assert.AreSame(mob, ThreatResponseMath.SelectWithHysteresis(directThreats, null, true, c => c.Score, c => c.Dist, n => 0, CombatScoreMath.ShouldSwitchAttackTarget));
		Assert.IsNull(ThreatResponseMath.SelectWithHysteresis(new List<Cand>(), boss, true, c => c.Score, c => c.Dist, n => 0, CombatScoreMath.ShouldSwitchAttackTarget));
	}

	[Test]
	public void StepsToFirstTileWithinRange_EmptyPath_IsZero_AndNeverExceedsPathLength()
	{
		Assert.AreEqual(0, CombatScoreMath.StepsToFirstTileWithinRange(new List<Vector2Int>(), new Vector2Int(3, 3), 1));

		// 경로 어느 타일도 공격 거리 안이 아니면(정상 경로는 마지막 타일이 대상 타일이라 없는 경우) 전체 길이를 돌려준다.
		var detached = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0) };
		Assert.AreEqual(2, CombatScoreMath.StepsToFirstTileWithinRange(detached, new Vector2Int(9, 9), 1));
	}

	// ── 점유자 상태별 대기 예상시간 + 대기 vs 우회 선택 ──
	[Test]
	public void OccupancyMath_EstimateWaitSeconds_ByOccupantKind()
	{
		Assert.AreEqual(0.4f, OccupancyMath.EstimateWaitSeconds(OccupantKind.Moving, 0.4f, null).Value, 0.0001f, "이동 중인 아군은 다음 걸음에 비킨다고 본다");
		Assert.AreEqual(5.4f, OccupancyMath.EstimateWaitSeconds(OccupantKind.Interacting, 0.4f, 5f).Value, 0.0001f, "상호작용은 남은 시간 + 자리를 떠나는 한 걸음");
		Assert.IsNull(OccupancyMath.EstimateWaitSeconds(OccupantKind.Interacting, 0.4f, null), "파괴 중처럼 남은 시간을 모르면 미확인");
		Assert.IsNull(OccupancyMath.EstimateWaitSeconds(OccupantKind.Busy, 0.4f, null), "교전·시전·다른 것을 기다리는 중은 미확인");
		Assert.IsNull(OccupancyMath.EstimateWaitSeconds(OccupantKind.IdleInPlace, 0.4f, null), "제자리 대기는 미확인(비켜 주기 대상)");
	}

	[Test]
	public void OccupancyMath_InteractionRemainingSeconds_ClampsProgress()
	{
		Assert.AreEqual(8f, OccupancyMath.InteractionRemainingSeconds(0f, 8f), 0.0001f);
		Assert.AreEqual(2f, OccupancyMath.InteractionRemainingSeconds(0.75f, 8f), 0.0001f);
		Assert.AreEqual(0f, OccupancyMath.InteractionRemainingSeconds(1.5f, 8f), 0.0001f, "진행도는 1로 고정");
		Assert.AreEqual(8f, OccupancyMath.InteractionRemainingSeconds(-1f, 8f), 0.0001f, "진행도는 0으로 고정");
	}

	[Test]
	public void OccupancyMath_Decide_ShorterWins_TieKeepsCurrent_UnknownPrefersDetour()
	{
		// 대기 0.4+구조 3s=3.4 vs 우회 10s → 대기. 상호작용 8.4+3=11.4 vs 우회 4s → 우회.
		Assert.AreEqual(OccupancyChoice.Wait, OccupancyMath.Decide(0.4f, 3f, 10f, OccupancyChoice.None));
		Assert.AreEqual(OccupancyChoice.Detour, OccupancyMath.Decide(8.4f, 3f, 4f, OccupancyChoice.None));

		// 동률이면 현재 선택 유지, 아직 선택 전이면 원래 경로에서 기다린다.
		Assert.AreEqual(OccupancyChoice.Detour, OccupancyMath.Decide(2f, 3f, 5f, OccupancyChoice.Detour), "동률 + 우회를 고르던 중 → 우회 유지");
		Assert.AreEqual(OccupancyChoice.Wait, OccupancyMath.Decide(2f, 3f, 5f, OccupancyChoice.Wait), "동률 + 대기를 고르던 중 → 대기 유지");
		Assert.AreEqual(OccupancyChoice.Wait, OccupancyMath.Decide(2f, 3f, 5f, OccupancyChoice.None), "동률 + 선택 전 → 원래 경로에서 대기");

		// 비워질 시간을 모르면 허용 우회가 있으면 우회, 없으면 대기.
		Assert.AreEqual(OccupancyChoice.Detour, OccupancyMath.Decide(null, 3f, 6f, OccupancyChoice.Wait));
		Assert.AreEqual(OccupancyChoice.Wait, OccupancyMath.Decide(null, 3f, null, OccupancyChoice.None));
		// 시간을 알아도 우회가 없으면 기다린다.
		Assert.AreEqual(OccupancyChoice.Wait, OccupancyMath.Decide(8.4f, 3f, null, OccupancyChoice.None));
	}

	[Test]
	public void OccupancyMath_UnknownWaitExpired_OnlyForUnknownWaits()
	{
		Assert.IsFalse(OccupancyMath.UnknownWaitExpired(105f, 100f, 6f, false));
		Assert.IsTrue(OccupancyMath.UnknownWaitExpired(106f, 100f, 6f, false));
		Assert.IsFalse(OccupancyMath.UnknownWaitExpired(200f, 100f, 6f, true), "시간을 아는 대기(상호작용 등)는 끊지 않는다");
	}

	// ── 좁은 통로 통과 순서·마주 막힘 양보 ──
	[Test]
	public void OccupancyMath_EntryRank_FollowsDocumentOrder()
	{
		Assert.AreEqual(0, OccupancyMath.EntryRank(CombatRole.MeleeTank));
		Assert.AreEqual(0, OccupancyMath.EntryRank(CombatRole.MeleeDps));
		Assert.AreEqual(1, OccupancyMath.EntryRank(CombatRole.MeleeSupport));
		Assert.AreEqual(2, OccupancyMath.EntryRank(CombatRole.RangedDps));
		Assert.AreEqual(3, OccupancyMath.EntryRank(CombatRole.RangedSupport));
	}

	[Test]
	public void OccupancyMath_ComparePassPriority_RoleThenHpThenKeptRandomThenId_NoLeaderException()
	{
		var tank = new PassKey(0, 0.2f, 50, 1);
		var priest = new PassKey(3, 1.0f, 1, 2);
		Assert.Less(OccupancyMath.ComparePassPriority(tank, priest), 0, "역할 순위가 HP 비율·무작위보다 먼저(리더가 사제여도 전방 근접이 먼저)");
		Assert.Greater(OccupancyMath.ComparePassPriority(priest, tank), 0);

		var healthy = new PassKey(2, 0.9f, 99, 3);
		var hurt = new PassKey(2, 0.4f, 1, 4);
		Assert.Less(OccupancyMath.ComparePassPriority(healthy, hurt), 0, "같은 순위는 HP 비율이 높은 쪽이 먼저");

		var a = new PassKey(2, 0.5f, 10, 5);
		var b = new PassKey(2, 0.5f, 20, 6);
		Assert.Less(OccupancyMath.ComparePassPriority(a, b), 0, "HP도 같으면 한 번 정한 무작위 값(작은 쪽)이 먼저");
		Assert.Greater(OccupancyMath.ComparePassPriority(b, a), 0, "같은 비교는 항상 같은 결과(재추첨 없음)");

		var c = new PassKey(2, 0.5f, 10, 7);
		Assert.Less(OccupancyMath.ComparePassPriority(a, c), 0, "무작위 값까지 같으면 Id로 고정해 양쪽이 같은 결론");
		Assert.AreEqual(0, OccupancyMath.ComparePassPriority(a, a));
	}

	[Test]
	public void OccupancyMath_ShouldIYield_RetreatAbilityThenPriority()
	{
		var high = new PassKey(0, 1f, 1, 1);
		var low = new PassKey(3, 1f, 1, 2);
		Assert.IsTrue(OccupancyMath.ShouldIYield(true, false, true, high, low), "상대가 통로를 빠져나오는 중이면 이탈이 먼저라 내가 양보");
		Assert.IsTrue(OccupancyMath.ShouldIYield(false, true, false, high, low), "나만 물러날 수 있으면 우선순위가 높아도 내가 양보");
		Assert.IsFalse(OccupancyMath.ShouldIYield(false, false, true, low, high), "상대만 물러날 수 있으면 내가 양보하지 않는다");
		Assert.IsFalse(OccupancyMath.ShouldIYield(false, false, false, low, high), "둘 다 못 물러나면 아무도 물러나지 않는다(호출부가 대기·다른 경로 판단)");
		Assert.IsTrue(OccupancyMath.ShouldIYield(false, true, true, low, high), "둘 다 가능하면 우선순위 낮은 쪽이 양보");
		Assert.IsFalse(OccupancyMath.ShouldIYield(false, true, true, high, low), "둘 다 가능하면 우선순위 높은 쪽은 양보하지 않는다");
	}

	// ── 플레이어 이동 명령이 막혔을 때의 처리 ──
	[Test]
	public void ResolveMoveFailure_FallbackRetargetsButNeverOutlivesTheLimit()
	{
		// 대체 칸이 있으면 재지정하되, 연속 실패가 한도에 닿으면 대체 칸이 있어도 포기한다(문 뒤처럼 닿을 수 없는 대체 칸이 계속 뽑히는 고리 방지).
		Assert.AreEqual(MovementMath.MoveFailAction.Retarget, MovementMath.ResolveMoveFailure(1, 8, true, false));
		Assert.AreEqual(MovementMath.MoveFailAction.Retarget, MovementMath.ResolveMoveFailure(7, 8, true, true));
		Assert.AreEqual(MovementMath.MoveFailAction.Abort, MovementMath.ResolveMoveFailure(8, 8, true, true));
		Assert.AreEqual(MovementMath.MoveFailAction.Abort, MovementMath.ResolveMoveFailure(9, 8, true, false));
	}

	[Test]
	public void ResolveMoveFailure_NoFallback_RetriesWhileOpenNeighborsExist_ElseAbortsImmediately()
	{
		Assert.AreEqual(MovementMath.MoveFailAction.Retry, MovementMath.ResolveMoveFailure(1, 8, false, true), "잠깐 몰려 막힌 것으로 보고 재시도");
		Assert.AreEqual(MovementMath.MoveFailAction.Abort, MovementMath.ResolveMoveFailure(8, 8, false, true), "한도에 닿으면 포기");
		Assert.AreEqual(MovementMath.MoveFailAction.Abort, MovementMath.ResolveMoveFailure(1, 8, false, false), "주변이 지형으로 완전히 막혔으면 즉시 포기(표시는 호출부가 붙인다)");
	}

	// 연속 실패를 시뮬레이션해 재지정 고리가 정확히 한도 틱에서 끝나는지 확인한다(호출부 카운터 규칙: 실패마다 +1, 이동 성공 시에만 0).
	[Test]
	public void ResolveMoveFailure_RetargetLoopEndsAtLimit_AndSuccessResetsTheCount()
	{
		int stuck = 0, limit = 8, ticks = 0;
		MovementMath.MoveFailAction action;
		do
		{
			stuck++; ticks++;
			action = MovementMath.ResolveMoveFailure(stuck, limit, true, false); // 대체 칸이 매번 뽑히지만 매번 이동 실패
		} while (action == MovementMath.MoveFailAction.Retarget && ticks < 100);
		Assert.AreEqual(MovementMath.MoveFailAction.Abort, action);
		Assert.AreEqual(limit, ticks, "대체 칸이 계속 뽑혀도 한도 틱에서 포기한다");

		// 정상 혼잡: 한 번 실패해 재지정한 뒤 다음 틱 이동이 성공하면 카운터가 리셋돼 포기에 가까워지지 않는다.
		stuck = 0;
		for (int round = 0; round < 50; round++)
		{
			stuck++; // 실패
			Assert.AreEqual(MovementMath.MoveFailAction.Retarget, MovementMath.ResolveMoveFailure(stuck, limit, true, false));
			stuck = 0; // 재지정한 칸으로 이동 성공
		}
	}

	// ── 치료 등 아군 대상 스킬의 차폐(직선이 벽·닫힌 문에 막히는가) ──
	[Test]
	public void IsLineClear_OpenFloor_IsClear_AndEndpointsAreNotChecked()
	{
		var blocked = new HashSet<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(5, 0) }; // 양 끝 타일이 막혀 있어도(문 위에 서 있는 경우 등) 직선 판정에는 영향 없다.
		Assert.IsTrue(MovementMath.IsLineClear(new Vector2Int(0, 0), new Vector2Int(5, 0), p => blocked.Contains(p)));
		Assert.IsTrue(MovementMath.IsLineClear(new Vector2Int(2, 2), new Vector2Int(2, 2), p => true), "같은 타일은 항상 열려 있다");
		Assert.IsTrue(MovementMath.IsLineClear(new Vector2Int(0, 0), new Vector2Int(1, 0), p => true), "인접 타일은 사이에 검사할 타일이 없다");
	}

	[Test]
	public void IsLineClear_WallBetween_Blocks_InBothDirections()
	{
		var wall = new HashSet<Vector2Int> { new Vector2Int(3, 0) };
		Assert.IsFalse(MovementMath.IsLineClear(new Vector2Int(0, 0), new Vector2Int(6, 0), p => wall.Contains(p)));
		Assert.IsFalse(MovementMath.IsLineClear(new Vector2Int(6, 0), new Vector2Int(0, 0), p => wall.Contains(p)));

		// 벽이 직선 밖이면 통과한다.
		Assert.IsTrue(MovementMath.IsLineClear(new Vector2Int(0, 1), new Vector2Int(6, 1), p => wall.Contains(p)));
	}

	[Test]
	public void IsLineClear_DiagonalSqueezeBetweenTwoWallCorners_IsBlocked_ButOneCornerIsNot()
	{
		// (0,0)→(1,1) 대각선: 양옆 (1,0)·(0,1)이 둘 다 벽이면 틈으로 새지 못하고, 하나만 벽이면 통과한다.
		var both = new HashSet<Vector2Int> { new Vector2Int(1, 0), new Vector2Int(0, 1) };
		var one = new HashSet<Vector2Int> { new Vector2Int(1, 0) };
		Assert.IsFalse(MovementMath.IsLineClear(new Vector2Int(0, 0), new Vector2Int(1, 1), p => both.Contains(p)));
		Assert.IsTrue(MovementMath.IsLineClear(new Vector2Int(0, 0), new Vector2Int(1, 1), p => one.Contains(p)));
	}
}
#endif
