#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

// ========================================================================
// 원거리 간격 유지 판정 고정 테스트(NUnit, #if UNITY_INCLUDE_TESTS) — RangedSpacingMath는 순수 함수라 Unity 오브젝트 없이 검증한다.
// 2026-10-05 아처 떨림: 후퇴/접근 사이에 '제자리' 구간이 없어 적이 한 칸만 움직여도 앞뒤로 왕복했고, 벽 좌표를 향한 후퇴가 옆으로 샜다.
// ========================================================================

public class RangedSpacingTests
{
	// 아처(정밀 사격 HitRange 7): 위험 거리 3, 사거리 7.
	private const int Danger = 3;
	private const int Range = 7;

	[TestCase(1)]
	[TestCase(2)]
	[TestCase(3)]
	public void WithinDangerDistance_Retreats(int dist)
	{
		Assert.AreEqual(RangedSpacing.Retreat, RangedSpacingMath.Decide(dist, Danger, Range));
	}

	[TestCase(4)]
	[TestCase(5)]
	[TestCase(6)]
	[TestCase(7)]
	public void BetweenDangerAndRange_Holds(int dist)
	{
		Assert.AreEqual(RangedSpacing.Hold, RangedSpacingMath.Decide(dist, Danger, Range));
	}

	[TestCase(8)]
	[TestCase(12)]
	public void BeyondRange_Approaches(int dist)
	{
		Assert.AreEqual(RangedSpacing.Approach, RangedSpacingMath.Decide(dist, Danger, Range));
	}

	// 예전 on/off 제어는 거리가 4칸에서 한 칸만 벗어나도 접근·후퇴했다 — 적이 4~7칸 사이에서 움직이는 동안은 전부 제자리여야 한다.
	[Test]
	public void EnemyJitteringInsideBand_NeverMoves()
	{
		int[] enemyDistances = { 4, 5, 4, 6, 7, 5, 4, 6, 5 };
		foreach (int d in enemyDistances)
			Assert.AreEqual(RangedSpacing.Hold, RangedSpacingMath.Decide(d, Danger, Range), "dist " + d);
	}

	[Test]
	public void NoDangerZone_NeverRetreats()
	{
		Assert.AreEqual(RangedSpacing.Hold, RangedSpacingMath.Decide(1, 0, Range));
	}

	// ── 후퇴 한 칸 선택 ──

	[Test]
	public void EnemyOnLeft_OpenGround_StepsStraightAway()
	{
		bool found = RangedSpacingMath.TryChooseRetreatStep(10, 10, 7, 10, (dx, dy) => true, out int sx, out int sy);
		Assert.IsTrue(found);
		Assert.AreEqual(1, sx);
		Assert.AreEqual(0, sy); // 대각선으로 새지 않고 정반대로
	}

	[Test]
	public void EnemyDiagonal_StepsDiagonallyAway()
	{
		bool found = RangedSpacingMath.TryChooseRetreatStep(10, 10, 8, 8, (dx, dy) => true, out int sx, out int sy);
		Assert.IsTrue(found);
		Assert.AreEqual(1, sx);
		Assert.AreEqual(1, sy);
	}

	// 정반대 칸이 막히면 그다음으로 반대에 가까운 대각선을 쓴다.
	[Test]
	public void StraightAwayBlocked_UsesNextBestAwayStep()
	{
		bool found = RangedSpacingMath.TryChooseRetreatStep(10, 10, 7, 10, (dx, dy) => !(dx == 1 && dy == 0), out int sx, out int sy);
		Assert.IsTrue(found);
		Assert.AreEqual(1, sx);
		Assert.AreNotEqual(0, sy);
	}

	// 벽에 몰려 반대쪽이 전부 막힌 경우 옆걸음으로 샌 채 떨지 않고 후퇴를 포기한다(그 자리에서 싸운다).
	[Test]
	public void WallBehind_OnlySidewaysOpen_GivesUpInsteadOfSidestepping()
	{
		bool found = RangedSpacingMath.TryChooseRetreatStep(10, 10, 7, 10, (dx, dy) => dx == 0, out int sx, out int sy);
		Assert.IsFalse(found);
		Assert.AreEqual(0, sx);
		Assert.AreEqual(0, sy);
	}

	[Test]
	public void FullyCornered_GivesUp()
	{
		Assert.IsFalse(RangedSpacingMath.TryChooseRetreatStep(10, 10, 9, 10, (dx, dy) => false, out _, out _));
	}

	// 적과 같은 칸이면 어느 쪽이든 한 칸 벌어지는 방향을 고른다.
	[Test]
	public void SameTile_PicksSomeStepAway()
	{
		Assert.IsTrue(RangedSpacingMath.TryChooseRetreatStep(10, 10, 10, 10, (dx, dy) => true, out int sx, out int sy));
		Assert.IsTrue(sx != 0 || sy != 0);
	}
}
#endif
