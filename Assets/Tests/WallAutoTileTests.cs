#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

// ========================================================================
// 벽 자동 타일 연결 판정 예시를 고정하는 테스트(NUnit, #if UNITY_INCLUDE_TESTS). WallAutoTileMath는 순수 함수라 Unity 오브젝트 없이 검증한다.
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

	// 같은 N+W 연결이라도 좌상단 대각선이 채워져 있으면 "그냥 연결된 벽"(코너 아님) — 이 구현은 HorizontalTop으로 폴백한다.
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

	// ── 완전히 막힘: 4방향+대각선 전부 벽(두꺼운 벽 안쪽 깊숙한 타일) — 코너 조건에 안 걸리도록 대각선도 전부 채운다. ──
	[Test]
	public void FullySurrounded_AllEightNeighborsWalled()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: true, s: true, e: true, w: true,
			ne: true, nw: true, se: true, sw: true);
		Assert.AreEqual(WallVariant.FullySurrounded, variant);
	}

	// ── 직선: 한쪽 축만 연결됐을 때, 방향(상/하, 좌/우)은 반대편이 Floor인지로 갈린다
	// (벽 스프라이트가 상하/좌우 비대칭이라 방향을 구분한다) ──
	[Test]
	public void Straight_Horizontal_FloorBelow_IsTop()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: false, s: false, e: true, w: true,
			ne: false, nw: false, se: false, sw: false,
			isFloorN: false, isFloorS: true, isFloorE: false, isFloorW: false);
		Assert.AreEqual(WallVariant.HorizontalTop, variant); // 방이 남쪽에 있으면 이 벽은 방의 "위쪽(북측)" 경계
	}

	[Test]
	public void Straight_Horizontal_FloorAbove_IsBottom()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: false, s: false, e: true, w: true,
			ne: false, nw: false, se: false, sw: false,
			isFloorN: true, isFloorS: false, isFloorE: false, isFloorW: false);
		Assert.AreEqual(WallVariant.HorizontalBottom, variant); // 방이 북쪽에 있으면 "아래쪽(남측)" 경계
	}

	[Test]
	public void Straight_Vertical_FloorRight_IsLeft()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: true, s: true, e: false, w: false,
			ne: false, nw: false, se: false, sw: false,
			isFloorN: false, isFloorS: false, isFloorE: true, isFloorW: false);
		Assert.AreEqual(WallVariant.VerticalLeft, variant); // 방이 동쪽에 있으면 이 벽은 방의 "왼쪽(서측)" 경계
	}

	[Test]
	public void Straight_Vertical_FloorLeft_IsRight()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: true, s: true, e: false, w: false,
			ne: false, nw: false, se: false, sw: false,
			isFloorN: false, isFloorS: false, isFloorE: false, isFloorW: true);
		Assert.AreEqual(WallVariant.VerticalRight, variant); // 방이 서쪽에 있으면 "오른쪽(동측)" 경계
	}

	// ── 2칸 이상 두꺼운 벽(ApplyOuterWallThickness가 최소 2칸 보장)의 안쪽 레이어는 e/n/s가 전부 벽이고 방쪽(w)에만 Floor가 있다 — 벽-인접 조건만으로는 Vertical로 안 잡혀 Horizontal로 폴백하므로 이 케이스를 고정한다. ──
	[Test]
	public void Straight_Vertical_ThickWallInnerLayer_FloorWest_StillDetectsRight()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: true, s: true, e: true, w: false,
			ne: true, nw: false, se: true, sw: false,
			isFloorN: false, isFloorS: false, isFloorE: false, isFloorW: true);
		Assert.AreEqual(WallVariant.VerticalRight, variant); // 방이 서쪽에 있고 동쪽은 벽 두께 — 그래도 "오른쪽(동측)" 경계
	}

	[Test]
	public void Straight_Vertical_ThickWallInnerLayer_FloorEast_StillDetectsLeft()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: true, s: true, e: false, w: true,
			ne: false, nw: true, se: false, sw: true,
			isFloorN: false, isFloorS: false, isFloorE: true, isFloorW: false);
		Assert.AreEqual(WallVariant.VerticalLeft, variant); // 방이 동쪽에 있고 서쪽은 벽 두께 — 그래도 "왼쪽(서측)" 경계
	}

	[Test]
	public void Straight_Horizontal_ThickWallInnerLayer_StillDetectsTop()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: true, s: false, e: true, w: true,
			ne: true, nw: true, se: false, sw: false,
			isFloorN: false, isFloorS: true, isFloorE: false, isFloorW: false);
		Assert.AreEqual(WallVariant.HorizontalTop, variant); // 방이 남쪽에 있고 북쪽은 벽 두께 — 그래도 "위쪽(북측)" 경계
	}

	// ── 예외 상태(고립 타일, 인접 벽 없음, Floor 판정도 없음): Floor 인접 증거가 없는 자리를 방향성 있는 스프라이트로 그리면 부자연스러워 Solid로 처리한다. ──
	[Test]
	public void Isolated_FallsBackToSolid()
	{
		var variant = WallAutoTileMath.SelectVariant(
			n: false, s: false, e: false, w: false,
			ne: false, nw: false, se: false, sw: false);
		Assert.AreEqual(WallVariant.FullySurrounded, variant);
	}

	// ── '층 모서리 톱니' 재현 케이스: 코너(1·2번)는 벽-인접 기반 그대로 두고, 코너도 FullySurrounded도 아닌 나머지만 Floor 증거가 없으면 Solid로 처리한다(두꺼운 벽이 미개척 공간/층 경계와 맞닿을 때 Horizontal로 폴백해 무늬가 톱니처럼 반복되던 문제). ──
	[Test]
	public void ThickWallBorderingUnclaimedSpace_NoFloorEvidence_IsSolid()
	{
		// n/s/e 세 방향은 벽(두께 방향으로 계속 이어짐), w 방향은 "벽도 Floor도 아닌" 미개척
		// 공간(층 경계 바깥 등) — 코너 조건에도 FullySurrounded 조건에도 안 걸리는 경우.
		var variant = WallAutoTileMath.SelectVariant(
			n: true, s: true, e: true, w: false,
			ne: true, nw: false, se: true, sw: false,
			isFloorN: false, isFloorS: false, isFloorE: false, isFloorW: false);
		Assert.AreEqual(WallVariant.FullySurrounded, variant);
	}

	// ── 형태 분류(GetShape) — 12개 방향이 4개 형태로 정확히 묶이는지 ──
	[Test]
	public void GetShape_GroupsAllTwelveVariantsIntoFourShapes()
	{
		Assert.AreEqual(WallShape.Horizontal, WallAutoTileMath.GetShape(WallVariant.HorizontalTop));
		Assert.AreEqual(WallShape.Horizontal, WallAutoTileMath.GetShape(WallVariant.HorizontalBottom));
		Assert.AreEqual(WallShape.Vertical, WallAutoTileMath.GetShape(WallVariant.VerticalLeft));
		Assert.AreEqual(WallShape.Vertical, WallAutoTileMath.GetShape(WallVariant.VerticalRight));
		Assert.AreEqual(WallShape.Outer, WallAutoTileMath.GetShape(WallVariant.OuterTL));
		Assert.AreEqual(WallShape.Outer, WallAutoTileMath.GetShape(WallVariant.OuterTR));
		Assert.AreEqual(WallShape.Outer, WallAutoTileMath.GetShape(WallVariant.OuterBL));
		Assert.AreEqual(WallShape.Outer, WallAutoTileMath.GetShape(WallVariant.OuterBR));
		Assert.AreEqual(WallShape.Inner, WallAutoTileMath.GetShape(WallVariant.InnerTL));
		Assert.AreEqual(WallShape.Inner, WallAutoTileMath.GetShape(WallVariant.InnerTR));
		Assert.AreEqual(WallShape.Inner, WallAutoTileMath.GetShape(WallVariant.InnerBL));
		Assert.AreEqual(WallShape.Inner, WallAutoTileMath.GetShape(WallVariant.InnerBR));
		Assert.AreEqual(WallShape.Solid, WallAutoTileMath.GetShape(WallVariant.FullySurrounded));
	}

	// ── 회전각(GetRotationDegrees) — TL/Top/Left 기준(0°)에서 반시계 90°씩(TL→BL→BR→TR) ──
	[Test]
	public void GetRotationDegrees_MatchesReferenceOrientationMapping()
	{
		Assert.AreEqual(0f, WallAutoTileMath.GetRotationDegrees(WallVariant.HorizontalTop));
		Assert.AreEqual(180f, WallAutoTileMath.GetRotationDegrees(WallVariant.HorizontalBottom));
		Assert.AreEqual(0f, WallAutoTileMath.GetRotationDegrees(WallVariant.VerticalLeft));
		Assert.AreEqual(180f, WallAutoTileMath.GetRotationDegrees(WallVariant.VerticalRight));
		Assert.AreEqual(0f, WallAutoTileMath.GetRotationDegrees(WallVariant.OuterTL));
		Assert.AreEqual(90f, WallAutoTileMath.GetRotationDegrees(WallVariant.OuterBL));
		Assert.AreEqual(180f, WallAutoTileMath.GetRotationDegrees(WallVariant.OuterBR));
		Assert.AreEqual(270f, WallAutoTileMath.GetRotationDegrees(WallVariant.OuterTR));
		Assert.AreEqual(0f, WallAutoTileMath.GetRotationDegrees(WallVariant.InnerTL));
		Assert.AreEqual(90f, WallAutoTileMath.GetRotationDegrees(WallVariant.InnerBL));
		Assert.AreEqual(180f, WallAutoTileMath.GetRotationDegrees(WallVariant.InnerBR));
		Assert.AreEqual(270f, WallAutoTileMath.GetRotationDegrees(WallVariant.InnerTR));
		Assert.AreEqual(0f, WallAutoTileMath.GetRotationDegrees(WallVariant.FullySurrounded));
	}

	// ── 스프라이트 라이브러리 라벨 문자열 고정 — 형태별 4라벨만 실제 아트로 채우면 된다. ──
	[Test]
	public void SpriteLibraryLabels_ReducedToFourShapeLabels()
	{
		Assert.AreEqual("Wall_Horizontal", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.HorizontalTop));
		Assert.AreEqual("Wall_Horizontal", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.HorizontalBottom));
		Assert.AreEqual("Wall_Vertical", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.VerticalLeft));
		Assert.AreEqual("Wall_Vertical", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.VerticalRight));
		Assert.AreEqual("Wall_Outer_TL", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.OuterTL));
		Assert.AreEqual("Wall_Outer_TL", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.OuterTR));
		Assert.AreEqual("Wall_Outer_TL", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.OuterBL));
		Assert.AreEqual("Wall_Outer_TL", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.OuterBR));
		Assert.AreEqual("Wall_Inner_TL", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.InnerTL));
		Assert.AreEqual("Wall_Inner_TL", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.InnerTR));
		Assert.AreEqual("Wall_Inner_TL", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.InnerBL));
		Assert.AreEqual("Wall_Inner_TL", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.InnerBR));
		Assert.AreEqual("Wall_Solid", WallAutoTileMath.GetSpriteLibraryLabel(WallVariant.FullySurrounded));
	}
}
#endif
