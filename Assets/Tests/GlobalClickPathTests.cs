#if UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 클릭 이동 길찾기 개선(2026-11-04) — ① 실제 지형(GlobalTerrain)으로 길을 구해 명령이 끝날 때까지 보관 ② 아군이 막으면 기다렸다 진행하는 판단.
// 세션 없는 실제 AStarMovement로 지형·캐시를 검증한다(RouteAssessmentTests와 같은 방식). unitGrid(점유)는 세션이 필요해 여기서 닫지 못한다 — 점유 대기는 순수 판단(OccupancyMath.ClickMoveBlockedDecision)까지만 테스트하고 나머지는 플레이 확인이다.
// ========================================================================

public class GlobalClickPathTests
{
	private FactionData _savedMonsterData;

	[SetUp]
	public void SetUp() { _savedMonsterData = Unit.monsterFactionData; }

	[TearDown]
	public void TearDown()
	{
		Unit.monsterFactionData = _savedMonsterData;
		GlobalTerrain.TestTerrain = null;
	}

	// 12×9 지도: x=5 세로 벽이 y=0..7을 막고 y=8에만 틈이 있다.
	private static bool WallWithGapAtTop(int x, int y) => !(x == 5 && y <= 7);

	private static Monster MakeMonster(Vector2Int position, bool playerFaction = true)
	{
		Unit.monsterFactionData = new FactionData { discoveredMap = new[] { new int[12, 9] } }; // 맵 크기용 — 벽 판정은 읽는 지형이 한다
		var m = ScriptableObject.CreateInstance<Monster>();
		m.unitType = new Knight();
		m.currentFloor = 0;
		m.position = position;
		m.FactionBehavior = playerFaction ? (IFactionBehavior)new PlayerMonsterBehavior() : new WildMonsterBehavior();
		return m;
	}

	private static void IssueClickMove(Unit u, Vector2Int target)
	{
		u.playerMoveTarget = target;
		u.isManualMoveCommand = true;
	}

	// ── GlobalTerrain ─────────────────────────────────────────────────────

	[Test]
	public void GlobalTerrain_MapsWalkableToFloorAndBlockedToWall_WithNoUnknown()
	{
		var terrain = new GlobalTerrain((floor, p) => floor == 0 && WallWithGapAtTop(p.x, p.y));
		Assert.AreEqual(1, terrain.GetTileTerrain(new Vector3Int(2, 2, 0)));
		Assert.AreEqual(2, terrain.GetTileTerrain(new Vector3Int(5, 3, 0)));
		Assert.AreEqual(1, terrain.GetTileTerrain(new Vector3Int(5, 8, 0)), "틈은 바닥");
		Assert.AreEqual(2, terrain.GetTileTerrain(new Vector3Int(2, 2, 1)), "층별로 따로 판정");
		Assert.AreEqual(0, terrain.TerrainRevision);
	}

	// ── UsesGlobalClickPathing / PathTerrain / ClickPathIgnoresAllies ─────

	[Test]
	public void UsesGlobalClickPathing_OnlyForPlayerMonsterClickMoveCommands()
	{
		var player = MakeMonster(new Vector2Int(1, 4));
		Assert.IsFalse(player.UsesGlobalClickPathing, "명령이 없으면 해당 없음");

		IssueClickMove(player, new Vector2Int(10, 4));
		Assert.IsTrue(player.UsesGlobalClickPathing);

		player.playerMoveTarget = null; // 공격·오브젝트 공격 명령은 playerMoveTarget을 비운다
		Assert.IsFalse(player.UsesGlobalClickPathing);

		var wild = MakeMonster(new Vector2Int(1, 4), playerFaction: false);
		IssueClickMove(wild, new Vector2Int(10, 4));
		Assert.IsFalse(wild.UsesGlobalClickPathing, "야생 몬스터는 제외");

		var human = ScriptableObject.CreateInstance<Human>();
		human.FactionBehavior = new HumanFactionBehavior();
		IssueClickMove(human, new Vector2Int(10, 4));
		Assert.IsFalse(human.UsesGlobalClickPathing, "인류(던전 입구 시퀀스 포함)는 제외");
	}

	[Test]
	public void ClickPathIgnoresAllies_TurnsOffDuringTheDetourWindow()
	{
		var m = MakeMonster(new Vector2Int(1, 4));
		IssueClickMove(m, new Vector2Int(10, 4));
		Assert.IsTrue(m.ClickPathIgnoresAllies);

		m.clickDetourUntil = Time.time + 10f; // 대기 시간을 넘겨 우회로 전환한 구간
		Assert.IsFalse(m.ClickPathIgnoresAllies, "우회 구간에는 점유를 벽으로 본다");
		Assert.IsTrue(m.UsesGlobalClickPathing, "지형은 우회 구간에도 실제 지형");

		m.ResetClickMoveHold();
		Assert.IsTrue(m.ClickPathIgnoresAllies);
		Assert.AreEqual(-1f, m.clickWaitSince);
	}

	[Test]
	public void PathTerrain_IsGlobalOnlyWhileClickMoving()
	{
		var m = MakeMonster(new Vector2Int(1, 4));
		var global = new GlobalTerrain((f, p) => WallWithGapAtTop(p.x, p.y));
		GlobalTerrain.TestTerrain = global;

		Assert.AreNotSame(global, m.PathTerrain, "명령이 없으면 유닛이 아는 지형");
		IssueClickMove(m, new Vector2Int(10, 4));
		Assert.AreSame(global, m.PathTerrain);
		m.playerMoveTarget = null;
		Assert.AreNotSame(global, m.PathTerrain);
	}

	// ── 실제 A*: 시야 지형은 벽을 모르고 낙관적으로 직진, 전역 지형은 틈으로 간다 ──────

	[Test]
	public void KnownTerrain_WalksIntoTheWall_ButGlobalPathGoesForTheGap()
	{
		var target = new Vector2Int(10, 4);
		var m = MakeMonster(new Vector2Int(1, 4));
		IssueClickMove(m, target);

		// 전역 모드: 실제 최단 경로 — 틈 (5,8)을 지나야 한다. 처음부터 틈 쪽(북쪽)으로 올라가야 한다.
		GlobalTerrain.TestTerrain = new GlobalTerrain((f, p) => WallWithGapAtTop(p.x, p.y));
		var global = new AStarMovement();
		Assert.IsTrue(global.TryGetNextStep(m, target, out Dir globalDir));
		Assert.Greater(m.GetDirVector(globalDir).y, 0, "처음부터 틈 쪽(북쪽)으로");
		Assert.IsTrue(global.TryGetPathLength(m, target, out int globalLength, out _));
		// (1,4)→(4,8) 4칸(대각선 3 + 직선 1), 틈 (5,8)·(6,8) 2칸 — 코너 커팅 금지라 벽 옆 칸은 수평으로 지난다 — , (6,8)→(10,4) 대각선 4칸.
		Assert.AreEqual(10, globalLength, "4 + 2 + 4");

		// 비전역(명령 없음): 아는 지형이 비어 있어 미확인을 낙관적으로 통행 가능으로 보고 벽 쪽으로 곧장 걷는다(예전 동작).
		m.playerMoveTarget = null;
		m.isManualMoveCommand = false;
		var known = new AStarMovement();
		Assert.IsTrue(known.TryGetNextStep(m, target, out Dir knownDir));
		Assert.AreEqual(new Vector2Int(1, 0), m.GetDirVector(knownDir), "벽을 모르니 직진");
	}

	[Test]
	public void GlobalPath_IsKept_WhenLearningAWallAndDroppedWhenNotClickMoving()
	{
		var target = new Vector2Int(10, 4);
		var m = MakeMonster(new Vector2Int(1, 4));
		IssueClickMove(m, target);
		GlobalTerrain.TestTerrain = new GlobalTerrain((f, p) => WallWithGapAtTop(p.x, p.y));

		var algo = new AStarMovement();
		Assert.IsTrue(algo.TryGetNextStep(m, target, out _));
		Assert.IsTrue(algo.TryGetCachedDestination(out _));

		// (2,5)는 보관 경로((1,4)→(2,5)…) 위 — 클릭 이동은 실제 지형으로 이미 알고 있으니 시야로 새로 알았다고 버리지 않는다.
		algo.OnTileBecameWall(m, new Vector2Int(2, 5));
		Assert.IsTrue(algo.TryGetCachedDestination(out _), "전역 경로는 유지");

		m.playerMoveTarget = null;
		m.isManualMoveCommand = false;
		algo.OnTileBecameWall(m, new Vector2Int(2, 5));
		Assert.IsFalse(algo.TryGetCachedDestination(out _), "명령이 아닐 땐 기존대로 캐시 폐기");
	}

	[Test]
	public void ModeChange_InvalidatesTheCachedPath()
	{
		// 같은 목적지라도 전역 ↔ 비전역 전환 시 그때 만든 캐시를 재사용하면 안 된다(AI 탐색이 클릭 이동의 경로를 물려받는 사고 방지).
		var target = new Vector2Int(10, 4);
		var m = MakeMonster(new Vector2Int(1, 4));
		IssueClickMove(m, target);
		GlobalTerrain.TestTerrain = new GlobalTerrain((f, p) => WallWithGapAtTop(p.x, p.y));

		var algo = new AStarMovement();
		Assert.IsTrue(algo.TryGetNextStep(m, target, out Dir globalDir));

		m.playerMoveTarget = null;
		m.isManualMoveCommand = false;
		Assert.IsTrue(algo.TryGetNextStep(m, target, out Dir knownDir));
		Assert.AreNotEqual(globalDir, knownDir, "비전역은 새로 계산한다(벽을 모르는 직진)");
	}

	// ── 아군에 막혔을 때의 순수 판단 ───────────────────────────────────────

	[Test]
	public void ClickMoveBlockedDecision_WaitsForAlliesThenDetours_AndLeavesTheRestToExistingRules()
	{
		const float max = 1.5f;
		Assert.AreEqual(ClickMoveHoldDecision.Proceed, OccupancyMath.ClickMoveBlockedDecision(false, false, false, 0f, max), "비었으면 바로 진행");
		Assert.AreEqual(ClickMoveHoldDecision.Wait, OccupancyMath.ClickMoveBlockedDecision(true, true, false, 0f, max));
		Assert.AreEqual(ClickMoveHoldDecision.Wait, OccupancyMath.ClickMoveBlockedDecision(true, true, false, 1.49f, max));
		Assert.AreEqual(ClickMoveHoldDecision.Detour, OccupancyMath.ClickMoveBlockedDecision(true, true, false, 1.50f, max), "1.5초가 되면 우회");
		Assert.AreEqual(ClickMoveHoldDecision.Detour, OccupancyMath.ClickMoveBlockedDecision(true, true, false, 10f, max));
		Assert.AreEqual(ClickMoveHoldDecision.Defer, OccupancyMath.ClickMoveBlockedDecision(true, false, false, 0f, max), "적이 막으면 기존 대응");
		Assert.AreEqual(ClickMoveHoldDecision.Defer, OccupancyMath.ClickMoveBlockedDecision(true, true, true, 0f, max), "목적지 근처는 대체 칸 재지정에 맡긴다");
		Assert.AreEqual(ClickMoveHoldDecision.Defer, OccupancyMath.ClickMoveBlockedDecision(true, true, true, 99f, max), "목적지 근처는 시간과 무관");
	}

	[Test]
	public void ClickMoveBlockedDecision_ZeroWaitMeansImmediateDetour()
	{
		Assert.AreEqual(ClickMoveHoldDecision.Detour, OccupancyMath.ClickMoveBlockedDecision(true, true, false, 0f, 0f), "대기 시간을 0으로 설정하면 예전처럼 곧바로 우회");
	}
}
#endif
