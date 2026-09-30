#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 04번 문서(이동경로_속도_점유충돌) 4장 "역할별 이동 경로와 우회" 순수 계산 검증.
// WeightSystemTests.cs와 동일한 컨벤션(NUnit, #if UNITY_INCLUDE_TESTS).
// ========================================================================

public class MovementSystemTests
{
	// "회피 가능 경로 우선 → 노출시간 최소 → 전체길이 최소"라는 3단계 우선순위가 가중치 하나로
	// 성립하려면, 회피 비용 상수가 이 프로토타입 맵에서 나올 수 있는 임의의 기본 경로비용 차이보다
	// 압도적으로 커야 한다(사전식 순서가 깨지지 않으려면). 맵 크기가 커져도 이 마진이 유지되는지
	// 회귀 검증한다.
	[Test]
	public void AttackRangeAvoidExtraCost_DominatesTypicalPathCost()
	{
		// 대각선 이동(비용14) 기준 극단적으로 큰 경로(변 길이 1000칸)의 비용차보다 커야 한다.
		const int extremePathCostDifference = 1000 * 14;
		Assert.Greater(MovementMath.AttackRangeAvoidExtraCost, extremePathCostDifference);
	}

	// 02번 문서 9번 항목: "보호 시작 조건 HP"(EmergencyProtectHpRatio)와 "피해 감수 시 잔여 HP 허용
	// 하한"(ProtectApproachDamageRiskHpFloor)은 우연히 같은 수치(0.30f)를 쓰지만 원문이 "조정 가능한
	// 별도 값"이라 명시한 별개 상수다 — 하나로 통합되지 않았는지 회귀 검증한다.
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

	// ── 검증 04-03: 플레이어 이동 명령이 막혔을 때의 처리 ──
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

	// ── 검증 04-01: 치료 등 아군 대상 스킬의 차폐(직선이 벽·닫힌 문에 막히는가) ──
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
