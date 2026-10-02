#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 개인 지도 기반 길찾기 / 이동 캐시 무효화 / 경로 평가(통행 불가 판정 + 미확인 경로 추정거리). 순수 함수(RouteMath) + 세션 없는 A*(discoveredMap은 맵 크기용, 벽·미확인 판정은 유닛 개인 지도) + RouteAssessment를 EditMode에서 검증한다. 점유 때문인 막힘과 판정 메모의 시간 창은 세션·Time이 필요해 여기서 다루지 않는다.
// ========================================================================

public class RouteAssessmentTests
{
	private const string TrapId = "trap";

	private FactionData _savedHumanData;

	[SetUp]
	public void SetUp() { _savedHumanData = Unit.humanFactionData; }

	[TearDown]
	public void TearDown() { Unit.humanFactionData = _savedHumanData; }

	// ── 순수 함수(RouteMath) ─────────────────────────────────────────────

	[Test]
	public void EstimateViaFrontier_PicksSmallestSum_NotStraightLineNearest()
	{
		// 문서 9장 예: 아는 구간 4칸 + 남은 구간 최소 6칸 = 10칸. 직선으로 목적지에 가장 가까운(10,5) 프론티어는 40칸을 돌아야 해서 선택되지 않는다.
		var frontier = new Dictionary<Vector2Int, int>
		{
			{ new Vector2Int(10, 5), 40 },
			{ new Vector2Int(5, 5), 4 },
			{ new Vector2Int(2, 9), 3 },
		};
		Assert.AreEqual(10, RouteMath.EstimateViaFrontier(frontier, new Vector2Int(11, 5)));
	}

	[Test]
	public void EstimateViaFrontier_NoReachableFrontier_ReturnsNull()
	{
		Assert.IsNull(RouteMath.EstimateViaFrontier(new Dictionary<Vector2Int, int>(), new Vector2Int(1, 1)));
	}

	[Test]
	public void ShouldExcludeFromCandidates_OnlyUnreachable()
	{
		Assert.IsTrue(RouteMath.ShouldExcludeFromCandidates(RouteStatus.Unreachable));
		Assert.IsFalse(RouteMath.ShouldExcludeFromCandidates(RouteStatus.FullyKnown));
		Assert.IsFalse(RouteMath.ShouldExcludeFromCandidates(RouteStatus.PartlyUnknown));
		Assert.IsFalse(RouteMath.ShouldExcludeFromCandidates(RouteStatus.TrapBlocked)); // 접근 시도에서 03-13 막힘 신호가 나와야 하므로 후보로 남긴다
	}

	[Test]
	public void UnreachableMemo_SameSignatureAlwaysValid_ChangedSignatureOnlyInsideWindow()
	{
		var a = new RouteSignature(0, 5, 1, 2);
		var b = new RouteSignature(0, 6, 1, 2);
		var memo = new RouteMemoEntry { Signature = a, Time = 1f };
		Assert.IsTrue(RouteMath.IsUnreachableMemoValid(memo, a, 999f, 2f));
		Assert.IsTrue(RouteMath.IsUnreachableMemoValid(memo, b, 2.5f, 2f));
		Assert.IsFalse(RouteMath.IsUnreachableMemoValid(memo, b, 3.5f, 2f));
	}

	// ── 개인 지도(IKnownTerrain) ──────────────────────────────────────────

	[Test]
	public void PersonalMap_TerrainRevision_BumpsOnlyWhenKnownTerrainChanges()
	{
		var map = new PersonalMapKnowledge();
		var tile = new Vector3Int(3, 3, 0);
		int r0 = map.TerrainRevision;
		map.RevealTile(tile, false);
		int r1 = map.TerrainRevision;
		Assert.Greater(r1, r0);
		map.RevealTile(tile, false); // 같은 값으로 다시 봐도 바뀐 게 없다
		Assert.AreEqual(r1, map.TerrainRevision);
		map.RevealTile(tile, true); // 바닥 → 벽
		Assert.Greater(map.TerrainRevision, r1);
	}

	[Test]
	public void PersonalMap_FrontierTiles_AreUnrevealedOrthogonalNeighborsOfKnownFloor()
	{
		var map = new PersonalMapKnowledge();
		map.RevealTile(new Vector3Int(5, 5, 0), false);
		var frontier = new HashSet<Vector2Int>(map.GetFrontierTiles(0));
		Assert.AreEqual(4, frontier.Count);
		Assert.IsTrue(frontier.Contains(new Vector2Int(6, 5)));
		Assert.IsTrue(frontier.Contains(new Vector2Int(4, 5)));
		Assert.IsTrue(frontier.Contains(new Vector2Int(5, 6)));
		Assert.IsTrue(frontier.Contains(new Vector2Int(5, 4)));
		Assert.AreEqual(0, new List<Vector2Int>(map.GetFrontierTiles(1)).Count); // 다른 층은 비어 있다
	}

	// ── 개인 지도 기반 A* ─────────────────────────────────────────────────

	[Test]
	public void AStar_UsesPersonalMap_NotSharedMap()
	{
		// 공용 지도에만 x=6 벽이 있고 이 유닛은 아무것도 모른다 → 미확인은 낙관적으로 통과한다.
		SetSharedMap(13, 9, (x, y) => x == 6 ? 2 : 1);
		var human = MakeHuman(new Vector2Int(1, 3));
		var algo = new AStarMovement();
		Assert.IsTrue(algo.TryGetPathLength(human, new Vector2Int(11, 3), out _, out bool fullyRevealed));
		Assert.IsFalse(fullyRevealed); // 미확인 타일을 지난다

		// 반대로 이 유닛이 x=6 벽을 알면 공용 지도가 열려 있어도 막힌다.
		SetSharedMap(13, 9, (x, y) => 1);
		var knowing = MakeHuman(new Vector2Int(1, 3));
		Know(knowing, 13, 9, (x, y) => x == 6 ? 2 : (x < 6 ? 1 : 0));
		Assert.IsFalse(algo.TryGetPathLength(knowing, new Vector2Int(11, 3), out _, out _));
	}

	[Test]
	public void AStar_KillSwitchOff_FallsBackToSharedMap()
	{
		var cfg = AIConfigLoader.Behavior;
		if (cfg == null) Assert.Ignore("AIBehaviorConfig 에셋을 로드하지 못했다");
		bool saved = cfg.personalMapPathingEnabled;
		try
		{
			SetSharedMap(13, 9, (x, y) => x == 6 ? 2 : 1);
			var human = MakeHuman(new Vector2Int(1, 3));
			var algo = new AStarMovement();
			cfg.personalMapPathingEnabled = false;
			Assert.IsFalse(algo.TryGetPathLength(human, new Vector2Int(11, 3), out _, out _)); // 공용 지도의 벽이 다시 막는다
		}
		finally { cfg.personalMapPathingEnabled = saved; }
	}

	[Test]
	public void AStar_PlanningThroughUnknownNeverWritesTheMap()
	{
		SetSharedMap(13, 9, (x, y) => 1);
		var human = MakeHuman(new Vector2Int(1, 3));
		new AStarMovement().TryGetNextStep(human, new Vector2Int(11, 3), out _);
		Assert.AreEqual(0, human.personalMap.GetTileTerrain(new Vector3Int(2, 3, 0))); // 계획에서만 낙관 가정, 지도엔 통행 가능으로 기록하지 않는다
	}

	// ── 이동 캐시 무효화 ──────────────────────────────────────────────────

	[Test]
	public void OnTileBecameWall_ClearsCacheOnlyWhenTheTileIsOnTheCachedPath()
	{
		SetSharedMap(13, 9, (x, y) => 1);
		var human = MakeHuman(new Vector2Int(1, 3));
		var algo = new AStarMovement();
		Assert.IsTrue(algo.TryGetNextStep(human, new Vector2Int(11, 3), out _));
		Assert.IsTrue(algo.TryGetCachedDestination(out _));

		algo.OnTileBecameWall(human, new Vector2Int(6, 7)); // 경로 밖
		Assert.IsTrue(algo.TryGetCachedDestination(out _));

		algo.OnTileBecameWall(human, new Vector2Int(5, 3)); // 경로 위
		Assert.IsFalse(algo.TryGetCachedDestination(out _));
	}

	// ── 프론티어 걸음 수 ──────────────────────────────────────────────────

	[Test]
	public void FrontierSteps_CountRealWalkOverKnownTilesOnly()
	{
		SetSharedMap(13, 9, (x, y) => 1);
		var human = MakeHuman(new Vector2Int(1, 3));
		Know(human, 13, 9, (x, y) => x <= 5 ? 1 : 0);
		var steps = new Dictionary<Vector2Int, int>();

		Assert.IsTrue(new AStarMovement().TryComputeFrontierSteps(human, human.personalMap.GetFrontierTiles(0), steps));
		Assert.AreEqual(9, steps.Count); // x=6, y=0..8
		Assert.AreEqual(5, steps[new Vector2Int(6, 3)]);
		Assert.IsFalse(steps.ContainsKey(new Vector2Int(7, 3))); // 미확인 타일 너머로는 확장하지 않는다
		Assert.AreEqual(10, RouteMath.EstimateViaFrontier(steps, new Vector2Int(11, 3))); // 5 + 5
	}

	// ── 경로 평가 ─────────────────────────────────────────────────────────

	[Test]
	public void Assess_PartlyUnknown_UsesFrontierEstimate()
	{
		SetSharedMap(13, 9, (x, y) => 1);
		var human = MakeHuman(new Vector2Int(1, 3));
		Know(human, 13, 9, (x, y) => x <= 5 ? 1 : 0);

		RouteEstimate r = RouteAssessment.Assess(human, new Vector3Int(11, 3, 0));
		Assert.AreEqual(RouteStatus.PartlyUnknown, r.Status);
		Assert.AreEqual(10, r.Tiles);
	}

	[Test]
	public void Assess_FullyKnown_AdjacentCountsAsArrived()
	{
		SetSharedMap(13, 9, (x, y) => 1);
		var human = MakeHuman(new Vector2Int(1, 3));
		Know(human, 13, 9, (x, y) => 1);

		RouteEstimate r = RouteAssessment.Assess(human, new Vector3Int(4, 3, 0));
		Assert.AreEqual(RouteStatus.FullyKnown, r.Status);
		Assert.AreEqual(2, r.Tiles); // (3,3)에 닿으면 인접 1칸 — 조사 접근과 같은 도달 기준
	}

	[Test]
	public void Assess_TargetTileThatCannotBeStoodOn_IsStillReachableByAdjacency()
	{
		Func<int, int, int> tile = (x, y) => (x == 8 && y == 3) ? 2 : 1;
		SetSharedMap(13, 9, tile);
		var human = MakeHuman(new Vector2Int(1, 3));
		Know(human, 13, 9, tile);

		RouteEstimate r = RouteAssessment.Assess(human, new Vector3Int(8, 3, 0));
		Assert.AreEqual(RouteStatus.FullyKnown, r.Status);
		Assert.AreEqual(6, r.Tiles);
	}

	[Test]
	public void Assess_WalledInTarget_IsUnreachable_AndSecondAssessmentHitsTheMemo()
	{
		Func<int, int, int> ring = (x, y) => (Mathf.Abs(x - 11) <= 1 && Mathf.Abs(y - 3) <= 1 && !(x == 11 && y == 3)) ? 2 : 1;
		SetSharedMap(13, 9, ring);
		var human = MakeHuman(new Vector2Int(1, 3));
		Know(human, 13, 9, ring);
		var ctx = new RouteContext();
		var target = new Vector3Int(11, 3, 0);

		RouteEstimate first = RouteAssessment.Assess(human, target, false, ctx);
		Assert.AreEqual(RouteStatus.Unreachable, first.Status);
		Assert.AreEqual(RouteMath.UnreachableDistanceTiles, first.Tiles);
		Assert.AreEqual(1, ctx.NewEvaluations);

		RouteEstimate second = RouteAssessment.Assess(human, target, false, ctx);
		Assert.AreEqual(RouteStatus.Unreachable, second.Status);
		Assert.AreEqual(1, ctx.NewEvaluations); // 같은 정보라 다시 탐색하지 않았다
	}

	[Test]
	public void Assess_CorridorBlockedOnlyByKnownTrapZone_IsTrapBlocked_NotUnreachable()
	{
		Func<int, int, int> corridor = (x, y) => (x >= 4 && x <= 8 && y != 3) ? 2 : 1;
		SetSharedMap(13, 7, corridor);
		var human = MakeHuman(new Vector2Int(1, 3));
		Know(human, 13, 7, corridor);
		human.personalMap.RegisterObject(TrapId, new Vector3Int(6, 3, 0), 0f, 0f, new List<string> { "Object/Building/Passable/Trap" }); // 구역(x 5~7)이 1칸 통로를 막는다

		RouteEstimate r = RouteAssessment.Assess(human, new Vector3Int(11, 3, 0));
		Assert.AreEqual(RouteStatus.TrapBlocked, r.Status); // 후보로 남아야 접근 시도에서 03-13 막힘 신호가 나온다
	}

	[Test]
	public void Assess_TrapTarget_IsExactPath_AndNeverMemoized()
	{
		SetSharedMap(13, 9, (x, y) => 1);
		var human = MakeHuman(new Vector2Int(1, 3));
		Know(human, 13, 9, (x, y) => 1);
		human.personalMap.RegisterObject(TrapId, new Vector3Int(6, 3, 0), 0f, 0f, new List<string> { "Object/Building/Passable/Trap" });

		RouteEstimate r = RouteAssessment.Assess(human, new Vector3Int(6, 3, 0), targetIsTrap: true);
		Assert.AreEqual(RouteStatus.FullyKnown, r.Status);
		Assert.AreEqual(5, r.Tiles); // 함정 타일 자체까지(인접 허용 없음)
		Assert.AreEqual(0, human.unreachableRouteMemo.Count);
	}

	// ── 탐험 막힘 기록 ────────────────────────────────

	[Test]
	public void ExploreBlocked_NoRecords_MeansNoFilter()
	{
		SetSharedMap(13, 9, (x, y) => 1);
		var human = MakeHuman(new Vector2Int(1, 3));
		Assert.IsNull(RouteAssessment.CreateExploreBlockFilter(human)); // 호출부는 필터 없이 훑는다
	}

	[Test]
	public void ExploreBlocked_MarkedTileIsExcluded_SameSignatureStaysBlocked_OtherTilesAndFloorsAreNot()
	{
		SetSharedMap(13, 9, (x, y) => 1);
		var human = MakeHuman(new Vector2Int(1, 3));
		Know(human, 13, 9, (x, y) => x <= 5 ? 1 : 0);

		RouteAssessment.MarkExploreBlocked(human, new Vector2Int(6, 3));
		var blocked = RouteAssessment.CreateExploreBlockFilter(human);

		Assert.IsNotNull(blocked);
		Assert.IsTrue(blocked(new Vector2Int(6, 3)));
		Assert.IsFalse(blocked(new Vector2Int(6, 4)));
		human.currentFloor = 1; // 기록은 층별이다
		Assert.IsFalse(RouteAssessment.CreateExploreBlockFilter(human)(new Vector2Int(6, 3)));
	}

	[Test]
	public void ExploreBlocked_FeedsFrontierSelection_WithoutWritingFakeWalls()
	{
		SetSharedMap(13, 9, (x, y) => 1);
		var human = MakeHuman(new Vector2Int(1, 3));
		Know(human, 13, 9, (x, y) => x <= 5 ? 1 : 0);
		var nearest = new Vector2Int(6, 3);
		RouteAssessment.MarkExploreBlocked(human, nearest);
		var blocked = RouteAssessment.CreateExploreBlockFilter(human);

		Assert.IsTrue(human.personalMap.TryGetNearestFrontierTile(0, new Vector2Int(5, 3), out var picked, blocked));
		Assert.AreNotEqual(nearest, picked); // 막힘 기록된 프론티어는 건너뛰고 다음 후보를 고른다
		Assert.AreEqual(0, human.personalMap.GetTileTerrain(new Vector3Int(nearest.x, nearest.y, 0))); // 지형을 벽으로 위조하지 않는다 — 그 타일은 여전히 미탐색

		Assert.IsTrue(human.personalMap.HasFrontierTileInBounds(0, new RectInt(0, 0, 13, 9)));                  // 필터 없이는 프론티어가 남아 있다
		Assert.AreEqual(human.personalMap.CountFrontierTilesInBounds(0, new RectInt(0, 0, 13, 9), out _) - 1,
			human.personalMap.CountFrontierTilesInBounds(0, new RectInt(0, 0, 13, 9), out _, blocked));          // 방 탐색 완료 판정에서 막힌 것만 빠진다
	}

	// ── 헬퍼 ────────────────────────────────────────────────────────────

	private static Human MakeHuman(Vector2Int position)
	{
		var human = ScriptableObject.CreateInstance<Human>();
		human.unitType = new Knight();
		human.currentFloor = 0;
		human.position = position;
		return human;
	}

	// discoveredMap: 길찾기가 맵 크기를 읽는 용도(벽·미확인 판정은 유닛 개인 지도). 0 미탐색, 1 바닥, 2 벽.
	private static void SetSharedMap(int width, int height, Func<int, int, int> tile)
	{
		var map = new int[width, height];
		for (int x = 0; x < width; x++)
			for (int y = 0; y < height; y++) map[x, y] = tile(x, y);
		Unit.humanFactionData = new FactionData { discoveredMap = new[] { map } };
	}

	// 유닛 개인 지도에 tile(x, y)이 0이 아닌 칸만 알린다.
	private static void Know(Human human, int width, int height, Func<int, int, int> tile)
	{
		for (int x = 0; x < width; x++)
			for (int y = 0; y < height; y++)
			{
				int t = tile(x, y);
				if (t != 0) human.personalMap.RevealTile(new Vector3Int(x, y, 0), t == 2);
			}
	}
}
#endif
