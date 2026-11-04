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

	// ── 시야 유지 후퇴(2026-11-04, ㄱ자 모퉁이 "후퇴 → 시야 상실 → 전파적접근" 왕복) ──
	// 로그의 기하: 수직 복도(x=10, y 38~44)가 y=44에서 서쪽으로 꺾이는 수평 복도(y=44, x 4~10)가 된다. 아처는 수직 복도에 있고 적은 모퉁이 바로 너머 수평 복도에 있다.

	private static bool IsOpenL(int x, int y) => (x == 10 && y >= 38 && y <= 44) || (y == 44 && x >= 4 && x <= 10);

	private static bool IsLineClearL(int fromX, int fromY, int toX, int toY)
		=> MovementMath.IsLineClear(new UnityEngine.Vector2Int(fromX, fromY), new UnityEngine.Vector2Int(toX, toY), p => !IsOpenL(p.x, p.y));

	[Test]
	public void CornerCorridor_RetreatDownTheLegWouldLoseSight_IsRefused()
	{
		const int ux = 10, uy = 42, tx = 9, ty = 44; // 적은 모퉁이 서쪽 한 칸 — 현재 칸에서는 보인다
		Assert.IsTrue(IsLineClearL(ux, uy, tx, ty), "전제: 지금은 적이 보인다");
		Assert.IsFalse(IsLineClearL(ux, uy - 1, tx, ty), "전제: 한 칸 물러난 칸에서는 모퉁이에 가려 안 보인다");

		bool found = RangedSpacingMath.TryChooseRetreatStep(ux, uy, tx, ty,
			(dx, dy) => IsOpenL(ux + dx, uy + dy),
			(dx, dy) => IsLineClearL(ux + dx, uy + dy, tx, ty),
			out int sx, out int sy, out RetreatRefusal refusal);

		Assert.IsFalse(found, "시야를 잃게 되는 후퇴는 하지 않는다");
		Assert.AreEqual(RetreatRefusal.WouldLoseSight, refusal);
		Assert.AreEqual(0, sx);
		Assert.AreEqual(0, sy);
	}

	// 시야 조건이 없던 예전 동작은 같은 상황에서 물러났다 — 이게 후퇴 → 시야 상실 → 접근 왕복의 시작이었다.
	[Test]
	public void CornerCorridor_WithoutSightRule_RetreatsLikeBefore()
	{
		const int ux = 10, uy = 42, tx = 9, ty = 44;
		bool found = RangedSpacingMath.TryChooseRetreatStep(ux, uy, tx, ty, (dx, dy) => IsOpenL(ux + dx, uy + dy), null, out int sx, out int sy, out RetreatRefusal refusal);
		Assert.IsTrue(found);
		Assert.AreEqual(0, sx);
		Assert.AreEqual(-1, sy);
		Assert.AreEqual(RetreatRefusal.None, refusal);
	}

	// 적이 같은 복도 끝(모퉁이 타일, 같은 열)에 있으면 복도를 따라 물러나도 직선이 열려 있어 후퇴한다.
	[Test]
	public void StraightLeg_RetreatKeepsSight_StepsBack()
	{
		const int ux = 10, uy = 42, tx = 10, ty = 44;
		bool found = RangedSpacingMath.TryChooseRetreatStep(ux, uy, tx, ty,
			(dx, dy) => IsOpenL(ux + dx, uy + dy),
			(dx, dy) => IsLineClearL(ux + dx, uy + dy, tx, ty),
			out int sx, out int sy, out RetreatRefusal refusal);
		Assert.IsTrue(found);
		Assert.AreEqual(0, sx);
		Assert.AreEqual(-1, sy);
		Assert.AreEqual(RetreatRefusal.None, refusal);
	}

	// 시야를 유지하는 칸과 잃는 칸이 섞여 있으면, 적 반대 방향에 가장 가깝더라도 시야를 잃는 칸은 건너뛰고 유지하는 칸을 고른다.
	[Test]
	public void MixedCandidates_PicksTheSightKeepingStep()
	{
		bool found = RangedSpacingMath.TryChooseRetreatStep(10, 10, 13, 10, (dx, dy) => true, (dx, dy) => dx == -1 && dy == 1, out int sx, out int sy, out RetreatRefusal refusal);
		Assert.IsTrue(found);
		Assert.AreEqual(-1, sx);
		Assert.AreEqual(1, sy, "정면 후퇴(-1,0)는 시야를 잃으니 건너뛴다");
		Assert.AreEqual(RetreatRefusal.None, refusal);
	}

	[Test]
	public void NoStepAtAll_IsCornered_NotSightLoss()
	{
		bool found = RangedSpacingMath.TryChooseRetreatStep(10, 10, 9, 10, (dx, dy) => false, (dx, dy) => true, out _, out _, out RetreatRefusal refusal);
		Assert.IsFalse(found);
		Assert.AreEqual(RetreatRefusal.Cornered, refusal, "물러날 칸 자체가 없는 건 몰림이다(시야 때문이 아님)");

		found = RangedSpacingMath.TryChooseRetreatStep(10, 10, 9, 10, (dx, dy) => false, null, out _, out _, out refusal);
		Assert.IsFalse(found);
		Assert.AreEqual(RetreatRefusal.Cornered, refusal, "시야 조건이 없어도 몰림은 몰림");
	}
}
#endif
