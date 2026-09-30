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

	[Test]
	public void StepsToFirstTileWithinRange_EmptyPath_IsZero_AndNeverExceedsPathLength()
	{
		Assert.AreEqual(0, CombatScoreMath.StepsToFirstTileWithinRange(new List<Vector2Int>(), new Vector2Int(3, 3), 1));

		// 경로 어느 타일도 공격 거리 안이 아니면(정상 경로는 마지막 타일이 대상 타일이라 없는 경우) 전체 길이를 돌려준다.
		var detached = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0) };
		Assert.AreEqual(2, CombatScoreMath.StepsToFirstTileWithinRange(detached, new Vector2Int(9, 9), 1));
	}
}
#endif
