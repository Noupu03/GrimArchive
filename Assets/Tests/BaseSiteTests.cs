#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 거점 공사 현장 자리 고르기(BaseSiteMath) 테스트(NUnit, #if UNITY_INCLUDE_TESTS) — 청사진·건설물이 계단·건물·코어·오브젝트와 겹치지 않는 자리를 고르는지 고정한다.
// 격자는 코드로 칠한다: 방 경계 안의 막힌 칸(blocked)은 계단·건물·코어·오브젝트·문 앞 같은 "영역에 들어가면 안 되는 칸"이다.
// 시각 연출(스프라이트·입자·테두리 채움)과 세션이 필요한 칸 판정은 플레이 확인 대상이다.
// ========================================================================

public class BaseSiteTests
{
	private static Func<Vector2Int, bool> ClearExcept(RectInt bounds, params Vector2Int[] blocked)
	{
		var set = new HashSet<Vector2Int>(blocked);
		return p => bounds.Contains(p) && !set.Contains(p);
	}

	// 계단 2×2 블록 + 코어 한 칸을 가진 8×8 방(청크 하나) — 계단은 청크 가운데(3..4, 3..4)에 놓인다.
	private static readonly RectInt EightByEight = new RectInt(0, 0, 8, 8);

	private static Vector2Int[] StairBlock() => new[]
	{
		new Vector2Int(3, 3), new Vector2Int(4, 3), new Vector2Int(3, 4), new Vector2Int(4, 4),
	};

	[Test]
	public void Pick_EmptyRoom_TakesCenterWithBlueprint()
	{
		var bounds = new RectInt(0, 0, 9, 9);
		bool found = BaseSiteMath.TryPickSite(bounds, bounds.center, ClearExcept(bounds), _ => false, out SiteLayout layout);

		Assert.IsTrue(found);
		Assert.AreEqual(BaseSiteMath.BlueprintRadius, layout.Radius, "빈 방이면 3×3 청사진");
		Assert.AreEqual(new Vector2Int(4, 4), layout.Center, "9×9의 한가운데 칸");
	}

	[Test]
	public void Pick_NeverOverlapsStairBlock()
	{
		var clear = ClearExcept(EightByEight, StairBlock());
		var stairs = new HashSet<Vector2Int>(StairBlock());

		Assert.IsTrue(BaseSiteMath.TryPickSite(EightByEight, EightByEight.center, clear, p => stairs.Contains(p), out SiteLayout layout));
		AssertFootprintAvoids(layout, stairs);
	}

	[Test]
	public void Pick_PrefersMarginAwayFromStairs()
	{
		// 8×8 방 가운데 계단 → 3×3 영역이 계단에 닿지 않는 자리는 없다(남는 띠 폭이 2). 여유 0 단계로 내려가 3×3을 계단 바로 옆에 두는 쪽이
		// 여유 1인 1×1보다 먼저다. 영역은 계단과 겹치지 않아야 한다.
		var stairs = new HashSet<Vector2Int>(StairBlock());
		var clear = ClearExcept(EightByEight, StairBlock());

		Assert.IsTrue(BaseSiteMath.TryPickSite(EightByEight, EightByEight.center, clear, p => stairs.Contains(p), out SiteLayout layout));
		Assert.AreEqual(1, layout.Radius, "3×3이 들어갈 자리가 있으면 1×1로 줄이지 않는다");
		AssertFootprintAvoids(layout, stairs);
	}

	[Test]
	public void Pick_UsesMarginWhenRoomAllows()
	{
		var bounds = new RectInt(0, 0, 14, 8);
		var stairs = new HashSet<Vector2Int>(StairBlock());
		var clear = ClearExcept(bounds, StairBlock());

		Assert.IsTrue(BaseSiteMath.TryPickSite(bounds, new Vector2(4f, 4f), clear, p => stairs.Contains(p), out SiteLayout layout));
		Assert.AreEqual(1, layout.Radius);
		AssertFootprintAvoids(layout, stairs);
		// 선호 지점이 계단 한가운데여도 둘레 한 칸까지 계단에 닿지 않는 자리를 고른다.
		for (int dx = -2; dx <= 2; dx++)
			for (int dy = -2; dy <= 2; dy++)
				Assert.IsFalse(stairs.Contains(layout.Center + new Vector2Int(dx, dy)), $"둘레 여유 안에 계단이 있다: {layout.Center} + ({dx},{dy})");
	}

	[Test]
	public void Pick_TightRoomFallsBackToSingleTile()
	{
		// 3×3이 들어갈 자리가 없고(막힌 칸이 격자 무늬) 빈 칸만 군데군데 있는 방 — 1×1로 줄여서라도 겹치지 않게 놓는다.
		var bounds = new RectInt(0, 0, 5, 5);
		var open = new HashSet<Vector2Int> { new Vector2Int(2, 2), new Vector2Int(0, 4) };
		Func<Vector2Int, bool> clear = p => open.Contains(p);

		Assert.IsTrue(BaseSiteMath.TryPickSite(bounds, bounds.center, clear, _ => false, out SiteLayout layout));
		Assert.AreEqual(0, layout.Radius);
		Assert.AreEqual(new Vector2Int(2, 2), layout.Center, "방 중앙에 가까운 빈 칸");
	}

	[Test]
	public void Pick_NoClearTile_ReturnsFalse()
	{
		var bounds = new RectInt(0, 0, 4, 4);
		Assert.IsFalse(BaseSiteMath.TryPickSite(bounds, bounds.center, _ => false, _ => false, out _));
	}

	[Test]
	public void Pick_IsDeterministic_TieBreaksByLowerYThenX()
	{
		// 선호 지점이 두 후보의 한가운데면 y가 작은 쪽, 같으면 x가 작은 쪽을 고른다.
		var bounds = new RectInt(0, 0, 4, 4);
		var onlyTwo = new HashSet<Vector2Int> { new Vector2Int(1, 1), new Vector2Int(1, 2) };
		Assert.IsTrue(BaseSiteMath.TryPickSite(bounds, new Vector2(1.5f, 2f), p => onlyTwo.Contains(p), _ => false, out SiteLayout vertical));
		Assert.AreEqual(new Vector2Int(1, 1), vertical.Center);

		var onlySideBySide = new HashSet<Vector2Int> { new Vector2Int(1, 1), new Vector2Int(2, 1) };
		Assert.IsTrue(BaseSiteMath.TryPickSite(bounds, new Vector2(2f, 1.5f), p => onlySideBySide.Contains(p), _ => false, out SiteLayout horizontal));
		Assert.AreEqual(new Vector2Int(1, 1), horizontal.Center);
	}

	[Test]
	public void Footprint_BecomesBlockedWhenSomethingIsBuiltOnIt()
	{
		var bounds = new RectInt(0, 0, 9, 9);
		var layout = new SiteLayout { Center = new Vector2Int(4, 4), Radius = 1 };
		Assert.IsTrue(BaseSiteMath.IsFootprintClear(layout, ClearExcept(bounds)));

		// 플레이어가 영역 모서리에 건물을 지었다 — 현장은 그 자리가 더 이상 비어 있지 않다고 판단해야 옮길 수 있다.
		Assert.IsFalse(BaseSiteMath.IsFootprintClear(layout, ClearExcept(bounds, new Vector2Int(5, 5))));
		// 영역 바로 바깥은 영향을 주지 않는다.
		Assert.IsTrue(BaseSiteMath.IsFootprintClear(layout, ClearExcept(bounds, new Vector2Int(6, 6))));
	}

	[Test]
	public void Relocation_AfterBlock_PicksADifferentClearSite()
	{
		var bounds = new RectInt(0, 0, 9, 9);
		var blocked = new[] { new Vector2Int(4, 4) };
		var clear = ClearExcept(bounds, blocked);

		Assert.IsTrue(BaseSiteMath.TryPickSite(bounds, bounds.center, clear, _ => false, out SiteLayout moved));
		Assert.AreNotEqual(new Vector2Int(4, 4), moved.Center);
		Assert.IsTrue(BaseSiteMath.IsFootprintClear(moved, clear));
	}

	// ── 완공 상징물(건설물 한 채)을 옮길 때는 1×1 자리만 고른다 ──

	[Test]
	public void Pick_MaxRadiusZero_NeverReturnsBlueprint()
	{
		var bounds = new RectInt(0, 0, 9, 9);
		Assert.IsTrue(BaseSiteMath.TryPickSite(bounds, bounds.center, ClearExcept(bounds), _ => false, out SiteLayout layout, maxRadius: 0));
		Assert.AreEqual(0, layout.Radius, "넓은 빈 방이어도 상징물은 한 칸만 차지한다");
	}

	[Test]
	public void Relocate_Emblem_TakesNearestFreeTileToOldOne()
	{
		// 상징물 칸(4,4)에 건물이 섰다 — 옛 칸에서 가장 가까운 빈 칸(여유 있는 단계에서 y→x가 작은 쪽)으로 옮긴다.
		var bounds = new RectInt(0, 0, 9, 9);
		var clear = ClearExcept(bounds, new Vector2Int(4, 4));
		Assert.IsTrue(BaseSiteMath.TryPickSite(bounds, new Vector2(4.5f, 4.5f), clear, _ => false, out SiteLayout moved, maxRadius: 0));

		Assert.AreNotEqual(new Vector2Int(4, 4), moved.Center);
		Assert.LessOrEqual(Mathf.Max(Mathf.Abs(moved.Center.x - 4), Mathf.Abs(moved.Center.y - 4)), 1, "바로 옆 칸으로 옮긴다");
	}

	[Test]
	public void Relocate_Emblem_NoFreeTile_KeepsReturningFalse()
	{
		var bounds = new RectInt(0, 0, 3, 3);
		Assert.IsFalse(BaseSiteMath.TryPickSite(bounds, bounds.center, _ => false, _ => false, out _, maxRadius: 0));
	}

	// ── 방 인원 라벨("N/M")은 방 중심에 놓이고 오브젝트 위에 그려진다 — 그 아래에 건설물을 세우지 않는다 ──

	[TestCase(4.5f, 4.5f, 3, 4, true)]   // 홀수 방: 라벨이 칸 한가운데 — 좌우로 한 칸씩 더 덮는다
	[TestCase(4.5f, 4.5f, 5, 4, true)]
	[TestCase(4.5f, 4.5f, 6, 4, false)]
	[TestCase(4.5f, 4.5f, 4, 3, false)]  // 세로로는 한 줄만
	[TestCase(4.5f, 4.5f, 4, 5, false)]
	[TestCase(4f, 4f, 3, 3, true)]       // 짝수 방: 라벨이 칸 경계에 걸려 두 줄·두 칸을 덮는다
	[TestCase(4f, 4f, 4, 4, true)]
	[TestCase(4f, 4f, 2, 4, false)]
	[TestCase(4f, 4f, 4, 2, false)]
	public void RoomLabel_CoversTilesItOverlaps(float cx, float cy, int tx, int ty, bool expected)
	{
		Assert.AreEqual(expected, BaseSiteMath.IsUnderRoomLabel(new Vector2(cx, cy), new Vector2Int(tx, ty)));
	}

	[Test]
	public void Pick_StaysOffTheRoomLabel()
	{
		var bounds = new RectInt(0, 0, 9, 9);
		var label = bounds.center;
		Func<Vector2Int, bool> clear = p => bounds.Contains(p) && !BaseSiteMath.IsUnderRoomLabel(label, p);

		Assert.IsTrue(BaseSiteMath.TryPickSite(bounds, label, clear, _ => false, out SiteLayout layout));
		for (int dx = -layout.Radius; dx <= layout.Radius; dx++)
			for (int dy = -layout.Radius; dy <= layout.Radius; dy++)
				Assert.IsFalse(BaseSiteMath.IsUnderRoomLabel(label, layout.Center + new Vector2Int(dx, dy)), $"영역이 라벨 아래다: {layout.Center}");
		Assert.AreEqual(1, layout.Radius, "라벨을 피해도 3×3이 들어갈 자리가 있으면 줄이지 않는다");
	}

	// ── 연출 수치 ──

	[TestCase(0f, 0)]
	[TestCase(0.24f, 0)]
	[TestCase(0.25f, 1)]
	[TestCase(0.74f, 2)]
	[TestCase(0.75f, 3)]
	[TestCase(1f, 3)]
	[TestCase(-1f, 0)]
	[TestCase(5f, 3)]
	public void StructureStage_RisesEveryQuarter(float progress, int expected)
	{
		Assert.AreEqual(expected, BaseSiteMath.StructureStage(progress));
	}

	[Test]
	public void StructureAlpha_StartsTranslucentAndEndsOpaque()
	{
		Assert.AreEqual(0.5f, BaseSiteMath.StructureAlpha(0f), 0.0001f);
		Assert.AreEqual(1f, BaseSiteMath.StructureAlpha(1f), 0.0001f);
		Assert.Less(BaseSiteMath.StructureAlpha(0.3f), BaseSiteMath.StructureAlpha(0.6f));
	}

	[Test]
	public void WorkFxInterval_ShrinksWithWorkersButIsBounded()
	{
		Assert.IsTrue(float.IsPositiveInfinity(BaseSiteMath.WorkFxInterval(0)));
		Assert.AreEqual(0.8f, BaseSiteMath.WorkFxInterval(1), 0.0001f);
		Assert.Less(BaseSiteMath.WorkFxInterval(4), BaseSiteMath.WorkFxInterval(1));
		Assert.AreEqual(0.2f, BaseSiteMath.WorkFxInterval(100), 0.0001f, "일꾼이 아무리 많아도 0.2초보다 잦지 않다");
	}

	[Test]
	public void PopScale_ReturnsToOneAtBothEnds()
	{
		Assert.AreEqual(1f, BaseSiteMath.PopScale(0f), 0.0001f);
		Assert.AreEqual(1.18f, BaseSiteMath.PopScale(0.5f), 0.0001f);
		Assert.AreEqual(1f, BaseSiteMath.PopScale(1f), 0.0001f);
	}

	private static void AssertFootprintAvoids(SiteLayout layout, HashSet<Vector2Int> forbidden)
	{
		for (int dx = -layout.Radius; dx <= layout.Radius; dx++)
			for (int dy = -layout.Radius; dy <= layout.Radius; dy++)
				Assert.IsFalse(forbidden.Contains(layout.Center + new Vector2Int(dx, dy)), $"영역이 금지 칸과 겹친다: {layout.Center} 반경 {layout.Radius}");
	}
}
#endif
