#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

// ========================================================================
// 벽 자동 타일 연결(회의록 2026-09-27, 정림 전달안)의 8방향 판정 예시를 고정하는 테스트.
// PropagationSystemTests.cs와 동일한 컨벤션(NUnit, #if UNITY_INCLUDE_TESTS). WallAutoTileMath는
// 순수 함수라 Unity 오브젝트 없이 직접 검증 가능하다.
// ========================================================================

public class WallAutoTileTests
{
	// ── 회의록 예시 그대로: 위/왼쪽에 벽이 있어도 좌상단 대각선이 비어있으면 안쪽 모서리 ──
	[Test]
	public void InnerCorner_TopLeftDiagonalEmpty_MatchesDocExample()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: true, s: false, e: false, w: true,
			ne: false, nw: false, se: false, sw: false);

		Assert.AreEqual(WallVariant.InnerTL, variant);
	}

	// 같은 N+W 연결이라도 좌상단 대각선이 채워져 있으면 "그냥 연결된 벽"(코너 아님) — 이 구현은 Horizontal로 폴백한다.
	[Test]
	public void NotInnerCorner_TopLeftDiagonalFilled_FallsBackToStraight()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: true, s: false, e: false, w: true,
			ne: false, nw: true, se: false, sw: false);

		Assert.AreNotEqual(WallVariant.InnerTL, variant);
	}

	[Test]
	public void InnerCorner_TopRight()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: true, s: false, e: true, w: false,
			ne: false, nw: false, se: false, sw: false);
		Assert.AreEqual(WallVariant.InnerTR, variant);
	}

	[Test]
	public void InnerCorner_BottomLeft()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: false, s: true, e: false, w: true,
			ne: false, nw: false, se: false, sw: false);
		Assert.AreEqual(WallVariant.InnerBL, variant);
	}

	[Test]
	public void InnerCorner_BottomRight()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: false, s: true, e: true, w: false,
			ne: false, nw: false, se: false, sw: false);
		Assert.AreEqual(WallVariant.InnerBR, variant);
	}

	// ── 우선순위: 4방향이 전부 막혀 있어도(=일반적으로는 내부 벽) 대각선 한 곳이 비어있으면 안쪽 모서리가 이긴다 ──
	[Test]
	public void InnerCorner_TakesPriorityOverFullySurrounded()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: true, s: true, e: true, w: true,
			ne: true, nw: false, se: true, sw: true);

		Assert.AreEqual(WallVariant.InnerTL, variant);
	}

	// ── 바깥쪽 모서리: 인접한 두 직교 방향만 막혀 있고 나머지는 완전히 비어있는 전형적인 L자 코너 ──
	[Test]
	public void OuterCorner_TopLeft()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: false, s: true, e: true, w: false,
			ne: false, nw: false, se: false, sw: false);
		Assert.AreEqual(WallVariant.OuterTL, variant);
	}

	[Test]
	public void OuterCorner_TopRight()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: false, s: true, e: false, w: true,
			ne: false, nw: false, se: false, sw: false);
		Assert.AreEqual(WallVariant.OuterTR, variant);
	}

	[Test]
	public void OuterCorner_BottomLeft()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: true, s: false, e: true, w: false,
			ne: false, nw: false, se: false, sw: false);
		Assert.AreEqual(WallVariant.OuterBL, variant);
	}

	[Test]
	public void OuterCorner_BottomRight()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: true, s: false, e: false, w: true,
			ne: false, nw: false, se: false, sw: false);
		Assert.AreEqual(WallVariant.OuterBR, variant);
	}

	// ── 직선: 한쪽 축만 연결됐을 때 ──
	[Test]
	public void Straight_Horizontal()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: false, s: false, e: true, w: true,
			ne: false, nw: false, se: false, sw: false);
		Assert.AreEqual(WallVariant.Horizontal, variant);
	}

	[Test]
	public void Straight_Vertical()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: true, s: true, e: false, w: false,
			ne: false, nw: false, se: false, sw: false);
		Assert.AreEqual(WallVariant.Vertical, variant);
	}

	// ── 예외 상태(고립 타일, 인접 벽 없음) — 문서의 "예외 상태 처리"는 Horizontal로 폴백한다 ──
	[Test]
	public void Isolated_FallsBackToHorizontal()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: false, s: false, e: false, w: false,
			ne: false, nw: false, se: false, sw: false);
		Assert.AreEqual(WallVariant.Horizontal, variant);
	}

	// ── 스프라이트 라이브러리 라벨 문자열 고정 — 회의록에 명시된 이름과 정확히 일치해야 정림이 채워
	// 넣을 실제 아트가 즉시 연결된다.
	[Test]
	public void SpriteLibraryLabels_MatchDocNaming()
	{
		Assert.AreEqual("Wall_Horizontal", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.Horizontal));
		Assert.AreEqual("Wall_Vertical", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.Vertical));
		Assert.AreEqual("Wall_Outer_TL", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.OuterTL));
		Assert.AreEqual("Wall_Outer_TR", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.OuterTR));
		Assert.AreEqual("Wall_Outer_BL", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.OuterBL));
		Assert.AreEqual("Wall_Outer_BR", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.OuterBR));
		Assert.AreEqual("Wall_Inner_TL", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.InnerTL));
		Assert.AreEqual("Wall_Inner_TR", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.InnerTR));
		Assert.AreEqual("Wall_Inner_BL", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.InnerBL));
		Assert.AreEqual("Wall_Inner_BR", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.InnerBR));
	}
}
#endif
