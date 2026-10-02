#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 알려진 활성 함정 회피·통과 판정. 순수 함수(ExplorationMath/CombatScoreMath/MovementMath) + 세션 없는 A*(humanFactionData.discoveredMap만 세팅) + PersonalMapKnowledge/TrapPartySystem 조율 기록을 EditMode에서 검증한다(ScriptableObject.CreateInstance<Human>() 관례). 세션이 없어 함정 피해량(DamageMax)은 0이고 '아직 존재하는가' 확인은 생략된다(TrapAvoidance.CollectKnownTraps).
// ========================================================================

public class TrapAvoidanceTests
{
	private const string TrapId = "trap";
	private static readonly Vector2Int Trap2D = new Vector2Int(6, 3);

	private FactionData _savedHumanData;

	[SetUp]
	public void SetUp() { _savedHumanData = Unit.humanFactionData; }

	[TearDown]
	public void TearDown() { Unit.humanFactionData = _savedHumanData; }

	// ── 순수 함수 ──────────────────────────────────────────────────────

	[Test]
	public void IsInTrapZone_IsChebyshevRadiusOne()
	{
		var trap = new Vector2Int(5, 5);
		Assert.IsTrue(ExplorationMath.IsInTrapZone(trap, trap));
		Assert.IsTrue(ExplorationMath.IsInTrapZone(new Vector2Int(6, 6), trap)); // 대각 인접도 구역
		Assert.IsTrue(ExplorationMath.IsInTrapZone(new Vector2Int(4, 5), trap));
		Assert.IsFalse(ExplorationMath.IsInTrapZone(new Vector2Int(7, 5), trap));
		Assert.IsFalse(ExplorationMath.IsInTrapZone(new Vector2Int(5, 3), trap));
	}

	// 전투 합류(v0.6 9-12): 피해를 아는(기록된) 함정만, 통과 후 예상 HP가 최대 HP의 50% 이상일 때만 통과 후보.
	[Test]
	public void CanPassTrapInCombat_RecordedOnly_AndHalfHpAfterHit()
	{
		Assert.IsTrue(ExplorationMath.CanPassTrapInCombat(true, 100f, 100f, 40f));
		Assert.IsTrue(ExplorationMath.CanPassTrapInCombat(true, 100f, 100f, 50f));   // 정확히 50%는 통과 후보
		Assert.IsFalse(ExplorationMath.CanPassTrapInCombat(true, 100f, 100f, 51f));
		Assert.IsFalse(ExplorationMath.CanPassTrapInCombat(false, 100f, 100f, 0f));  // 미기록 함정은 피해를 모르므로 통과 후보가 아니다
	}

	// 긴급 아군 보호(v0.6 9-13, 02번 9장): 대상 HP ≤ 30% + 함정 경로가 우회보다 빠름 + 이동자 조건(기록: 피해 후 HP ≥ 30% / 미기록: 현재 HP ≥ 60% — 피해를 모르는 함정의 별도 예외).
	[Test]
	public void CanPassTrapForProtect_RecordedTrap_RequiresAllyLowHp_ShorterPath_AndMoverHpAfterHit()
	{
		Assert.IsTrue(ExplorationMath.CanPassTrapForProtect(true, 30f, 100f, 100f, 100f, 60f, 5, 9));
		Assert.IsFalse(ExplorationMath.CanPassTrapForProtect(true, 31f, 100f, 100f, 100f, 60f, 5, 9));  // 보호 대상 HP 30% 초과
		Assert.IsFalse(ExplorationMath.CanPassTrapForProtect(true, 30f, 100f, 100f, 100f, 60f, 9, 9));  // 함정 경로가 우회보다 빠르지 않다(동률 포함)
		Assert.IsTrue(ExplorationMath.CanPassTrapForProtect(true, 30f, 100f, 100f, 100f, 60f, 5, int.MaxValue)); // 우회가 없으면 항상 빠르다
		Assert.IsTrue(ExplorationMath.CanPassTrapForProtect(true, 30f, 100f, 90f, 100f, 60f, 5, 9));    // 피해 후 30% 정확히 → 허용
		Assert.IsFalse(ExplorationMath.CanPassTrapForProtect(true, 30f, 100f, 89f, 100f, 60f, 5, 9));   // 피해 후 29%
	}

	[Test]
	public void CanPassTrapForProtect_UnrecordedTrap_RequiresMoverCurrentHpSixtyPercent()
	{
		Assert.IsTrue(ExplorationMath.CanPassTrapForProtect(false, 30f, 100f, 60f, 100f, 0f, 5, 9));
		Assert.IsFalse(ExplorationMath.CanPassTrapForProtect(false, 30f, 100f, 59f, 100f, 0f, 5, 9));
		Assert.IsFalse(ExplorationMath.CanPassTrapForProtect(false, 50f, 100f, 100f, 100f, 0f, 5, 9)); // 보호 대상 HP 조건은 이 예외에도 그대로
	}

	// 피해 정보로 남을 HP를 추정할 수 없으면 노출 경로를 제외한다(0으로 계산하거나 빼고 허용하지 않는다, 02번 9장).
	[Test]
	public void IsExposureRouteAllowed_NotEstimable_ExcludesRoute()
	{
		Assert.IsFalse(CombatScoreMath.IsExposureRouteAllowed(false, 1.0f));
		Assert.IsFalse(CombatScoreMath.IsExposureRouteAllowed(true, 0.29f));
		Assert.IsTrue(CombatScoreMath.IsExposureRouteAllowed(true, 0.30f));
	}

	// 함정 통과 비용은 공격범위 회피 비용보다 커야 "함정을 안 밟는 우회를 먼저 본다"가 사전식으로 성립한다.
	[Test]
	public void TrapPassExtraCost_DominatesAttackRangeAvoidCost()
	{
		Assert.Greater(MovementMath.TrapPassExtraCost, MovementMath.AttackRangeAvoidExtraCost);
		Assert.Greater(MovementMath.TrapPassExtraCost, 1000 * 14);
		Assert.Greater(MovementMath.TrapZoneEscapeExtraCost, 1000 * 14);
	}

	// ── PersonalMapKnowledge: 아는 함정 목록 ────────────────────────────

	[Test]
	public void KnownTrapTiles_RegisteredByTrapTag_RemovedOnCollected()
	{
		var human = MakeHuman(new Vector2Int(0, 0));
		human.personalMap.RegisterObject("loot", new Vector3Int(1, 1, 0), 0f, 5f, new List<string> { "Object/Passable/Loot" });
		Assert.AreEqual(0, human.personalMap.KnownTrapTiles.Count);

		KnowTrap(human, Trap2D);
		Assert.AreEqual(1, human.personalMap.KnownTrapTiles.Count);
		Assert.AreEqual(new Vector3Int(Trap2D.x, Trap2D.y, 0), human.personalMap.KnownTrapTiles[TrapId]);

		human.personalMap.OnObjectCollected(TrapId); // 해제·수거를 알게 되면 구역도 사라진다
		Assert.AreEqual(0, human.personalMap.KnownTrapTiles.Count);
	}

	// ── 세션 없는 A*: 일반 모드 ─────────────────────────────────────────

	[Test]
	public void General_PathAvoidsTrapZone_InOpenMap()
	{
		SetOpenMap(13, 9);
		var human = MakeHuman(new Vector2Int(1, 3));
		KnowTrap(human, Trap2D);

		var visited = Walk(human, new AStarMovement(), new Vector2Int(11, 3));

		Assert.AreEqual(new Vector2Int(11, 3), human.position); // 돌아서 목적지에 도착한다
		foreach (var p in visited) Assert.IsFalse(ExplorationMath.IsInTrapZone(p, Trap2D), $"구역 진입: {p}");
	}

	[Test]
	public void ModeOff_GoesStraightThroughTrap()
	{
		SetOpenMap(13, 9);
		var human = MakeHuman(new Vector2Int(1, 3));
		KnowTrap(human, Trap2D);

		var visited = Walk(human, new AStarMovement { TrapModeOverride = TrapMoveMode.Off }, new Vector2Int(11, 3));

		Assert.IsTrue(visited.Contains(Trap2D)); // 회피 끔 = 예전 동작(함정을 그대로 밟는다)
	}

	[Test]
	public void General_OneWideCorridor_StopsAtZoneEdge_AndLeavesBlockingTrapSignal()
	{
		SetCorridorMap();
		var human = MakeHuman(new Vector2Int(1, 3));
		KnowTrap(human, Trap2D);

		var visited = Walk(human, new AStarMovement(), new Vector2Int(11, 3));

		Assert.AreEqual(new Vector2Int(4, 3), human.position); // 구역(x 5~7) 바로 앞에서 멈춘다 — 유일한 통로라 돌아갈 길이 없다
		foreach (var p in visited) Assert.IsFalse(ExplorationMath.IsInTrapZone(p, Trap2D));
		Assert.AreEqual(TrapId, human.trapBlockTrapId); // 이동 계층의 진단: 함정을 무시하면 더 가까이 갈 수 있다 → 이 함정이 막고 있다
	}

	[Test]
	public void General_StartInsideZone_LeavesWithoutSteppingOnTrap()
	{
		SetOpenMap(13, 9);
		var trap = new Vector2Int(6, 4);
		var human = MakeHuman(new Vector2Int(6, 3)); // 함정 바로 아래(구역 안)
		KnowTrap(human, trap);

		var visited = Walk(human, new AStarMovement(), new Vector2Int(6, 7)); // 함정 너머로 가야 한다

		Assert.AreEqual(new Vector2Int(6, 7), human.position);
		Assert.IsFalse(visited.Contains(trap));
		for (int i = 1; i < visited.Count; i++) Assert.IsFalse(ExplorationMath.IsInTrapZone(visited[i], trap), $"구역 재진입: {visited[i]}"); // 첫 걸음에 밖으로 나온 뒤 다시 안 들어간다
	}

	[Test]
	public void General_ResponderIsExemptFromOwnTrapZone_ButNonResponderCannotReachRing()
	{
		SetOpenMap(13, 9);
		var ring = new Vector2Int(5, 3); // 함정 (6,3)에 인접한 해제 위치

		var bystander = MakeHuman(new Vector2Int(1, 3));
		KnowTrap(bystander, Trap2D);
		Walk(bystander, new AStarMovement(), ring);
		Assert.AreNotEqual(ring, bystander.position); // 일반 유닛은 구역(링) 진입 금지

		var responder = MakeHuman(new Vector2Int(1, 3));
		KnowTrap(responder, Trap2D);
		responder.currentTrapInteraction = new TrapInteractionState { TrapObjectId = TrapId, TrapPosition = new Vector3Int(Trap2D.x, Trap2D.y, 0) };
		Walk(responder, new AStarMovement(), ring);
		Assert.AreEqual(ring, responder.position); // 발견자·담당자는 그 함정의 해제 위치에 닿는다
	}

	[Test]
	public void EtaQuery_ExemptsTargetTrap_OnlyWhenAsked()
	{
		SetCorridorMap();
		var human = MakeHuman(new Vector2Int(1, 3));
		KnowTrap(human, Trap2D);
		var algo = new AStarMovement();

		Assert.IsFalse(algo.TryGetPathLength(human, Trap2D, out _, out _)); // 그냥 조회하면 함정 타일은 도달 불가
		Assert.IsTrue(algo.TryGetPathLength(human, Trap2D, out int length, out _, Trap2D)); // 담당 후보의 도착시간 조회는 그 함정 구역을 면제
		Assert.AreEqual(5, length);
	}

	// ── 세션 없는 A*: 전투 모드 ─────────────────────────────────────────

	[Test]
	public void Combat_UnrecordedTrapBlocks_RecordedTrapPassesWhenHpAfterHitIsEnough()
	{
		SetCorridorMap();
		var human = MakeHuman(new Vector2Int(1, 3));
		KnowTrap(human, Trap2D);
		var combat = new AStarMovement { TrapModeOverride = TrapMoveMode.Combat };

		Walk(human, combat, new Vector2Int(11, 3));
		Assert.AreEqual(new Vector2Int(5, 3), human.position); // 링은 자유라 함정 바로 앞까지 오지만 미기록 함정 타일은 통과 후보가 아니다

		human.personalMap.RecordTrapAttempt(TrapId, 60f); // 해제를 시도해 기록한 함정 — 피해를 안다(세션이 없어 0)
		Walk(human, combat, new Vector2Int(11, 3));
		Assert.AreEqual(new Vector2Int(11, 3), human.position); // 통과 조건(피해 후 HP ≥ 50%)을 만족해 함정 타일을 지나 도착
	}

	[Test]
	public void Combat_PrefersDetourAroundAllowedTrap()
	{
		SetOpenMap(13, 9);
		var human = MakeHuman(new Vector2Int(1, 3));
		KnowTrap(human, Trap2D);
		human.personalMap.RecordTrapAttempt(TrapId, 60f);

		var visited = Walk(human, new AStarMovement { TrapModeOverride = TrapMoveMode.Combat }, new Vector2Int(11, 3));

		Assert.AreEqual(new Vector2Int(11, 3), human.position);
		Assert.IsFalse(visited.Contains(Trap2D)); // 통과가 허용돼도 안전한 우회가 있으면 우회를 먼저 본다(비용 우선순위)
	}

	// ── TrapAvoidance: 구역 안 판정 ─────────────────────────────────────

	[Test]
	public void NeedsZoneEscape_InsideZoneWithoutResponse_OnlyForNonResponders()
	{
		var human = MakeHuman(new Vector2Int(5, 3)); // 함정 (6,3)의 구역 안
		Assert.IsFalse(TrapAvoidance.NeedsZoneEscape(human)); // 아직 함정을 모른다 — 구역도 없다

		KnowTrap(human, Trap2D);
		Assert.IsTrue(TrapAvoidance.NeedsZoneEscape(human));
		Assert.IsTrue(TrapAvoidance.IsInKnownZone(human, new Vector2Int(7, 4)));
		Assert.IsFalse(TrapAvoidance.IsInKnownZone(human, new Vector2Int(8, 3)));

		human.currentTrapInteraction = new TrapInteractionState { TrapObjectId = TrapId }; // 그 함정의 응답자는 구역을 면제받는다
		Assert.IsFalse(TrapAvoidance.NeedsZoneEscape(human));

		human.currentTrapInteraction = null;
		human.position = new Vector2Int(9, 3); // 구역 밖
		Assert.IsFalse(TrapAvoidance.NeedsZoneEscape(human));

		human.position = new Vector2Int(5, 3);
		human.currentWait = new WaitState { Reason = WaitReason.Retreating }; // 귀환은 06 위기반응 문서 전까지 기존 동작(함정 무시) 유지
		Assert.IsFalse(TrapAvoidance.NeedsZoneEscape(human));
	}

	// ── 막힘 → 응답 재개(TrapPartySystem) ───────────────────────────────

	[Test]
	public void OnPathBlockedByKnownTrap_StartsStandardResponse_ThenRespectsHandlerAndCooldown()
	{
		var party = new Party("p", "p");
		var a = MakePartyHuman("a", party);
		var b = MakePartyHuman("b", party);
		var trapObj = MakeTrapObject();

		TrapPartySystem.OnPathBlockedByKnownTrap(a, trapObj);
		Assert.IsNotNull(a.currentTrapInteraction);
		Assert.AreEqual(TrapId, a.currentTrapInteraction.TrapObjectId);
		Assert.AreEqual("a", party.TrapCoordinations[TrapId].DiscovererName);

		TrapPartySystem.OnPathBlockedByKnownTrap(b, trapObj); // 이미 다른 파티원이 표준 절차를 진행 중
		Assert.IsNull(b.currentTrapInteraction);

		TrapPartySystem.EndResponse(a, TrapEndReason.Bypassed);
		TrapPartySystem.OnPathBlockedByKnownTrap(b, trapObj); // 대응이 끝났어도 조율 기록의 재시작 쿨다운 안에서는 다시 열지 않는다
		Assert.IsNull(b.currentTrapInteraction);

		party.TrapCoordinations[TrapId].NextBlockedRetryTime = Time.time - 1f; // 쿨다운이 지나면 막힌 유닛이 다시 연다
		TrapPartySystem.OnPathBlockedByKnownTrap(b, trapObj);
		Assert.IsNotNull(b.currentTrapInteraction);
		Assert.AreEqual("b", party.TrapCoordinations[TrapId].DiscovererName);
	}

	[Test]
	public void OnPathBlockedByKnownTrap_AdoptsDeferredCoordination()
	{
		var party = new Party("p", "p");
		var a = MakePartyHuman("a", party);
		party.TrapCoordinations[TrapId] = new TrapPartyCoordination { TrapObjectId = TrapId, TrapPosition = new Vector3Int(Trap2D.x, Trap2D.y, 0), DiscovererName = "x", Deferred = true };

		TrapPartySystem.OnPathBlockedByKnownTrap(a, MakeTrapObject());

		Assert.IsNotNull(a.currentTrapInteraction);
		Assert.IsFalse(party.TrapCoordinations[TrapId].Deferred); // 집결 중이라 보류돼 있던 함정 — 막힌 유닛이 이어받는다
	}

	// ── 헬퍼 ────────────────────────────────────────────────────────────

	private static Human MakeHuman(Vector2Int position)
	{
		var human = ScriptableObject.CreateInstance<Human>();
		human.unitType = new Knight();
		human.currentFloor = 0;
		human.position = position;
		SeedPersonalMapFromSharedMap(human);
		return human;
	}

	// A*가 개인 지도로 경로를 계산하므로, 이 파일의 지도 헬퍼(SetOpenMap/SetCorridorMap)가 세팅한 공용 지도 내용을 개인 지도에도 같은 값으로 알린다(0 미확인은 건너뜀).
	private static void SeedPersonalMapFromSharedMap(Human human)
	{
		var data = Unit.humanFactionData;
		if (data?.discoveredMap == null || data.discoveredMap.Length == 0 || data.discoveredMap[0] == null) return;
		int[,] map = data.discoveredMap[0];
		for (int x = 0; x < map.GetLength(0); x++)
			for (int y = 0; y < map.GetLength(1); y++)
				if (map[x, y] != 0) human.personalMap.RevealTile(new Vector3Int(x, y, 0), map[x, y] == 2);
	}

	private static Human MakePartyHuman(string unitName, Party party)
	{
		var human = MakeHuman(new Vector2Int(0, 0));
		human.name = unitName;
		human.party = party;
		party.Members.Add(human);
		return human;
	}

	private static void KnowTrap(Human human, Vector2Int tile)
		=> human.personalMap.RegisterObject(TrapId, new Vector3Int(tile.x, tile.y, 0), 0f, 0f, new List<string> { "Object/Building/Passable/Trap" });

	private static InteractableObject MakeTrapObject()
		=> new InteractableObject(TrapId, new Vector3Int(Trap2D.x, Trap2D.y, 0), 0f, tags: new List<string> { "Object/Building/Passable/Trap" },
			trapHp: 300f, trapDamageMin: 30f, trapDamageMax: 60f);

	// discoveredMap: 0 미탐색(통행 가능으로 취급), 1 바닥, 2 벽.
	private static void SetOpenMap(int width, int height)
	{
		var map = new int[width, height];
		for (int x = 0; x < width; x++)
			for (int y = 0; y < height; y++) map[x, y] = 1;
		Unit.humanFactionData = new FactionData { discoveredMap = new[] { map } };
	}

	// 폭 13×높이 7, y=3 한 줄만 뚫린 x 4~8 구간 통로 — 함정 (6,3)이 그 한가운데라 구역(x 5~7)이 통로를 완전히 막는다.
	private static void SetCorridorMap()
	{
		var map = new int[13, 7];
		for (int x = 0; x < 13; x++)
			for (int y = 0; y < 7; y++)
				map[x, y] = (x >= 4 && x <= 8 && y != 3) ? 2 : 1;
		Unit.humanFactionData = new FactionData { discoveredMap = new[] { map } };
	}

	// A*가 주는 걸음을 실제로 따라가며 방문 타일을 모은다(세션이 없어 Move() 대신 위치를 직접 갱신). 더 못 가면 멈춘다.
	private static List<Vector2Int> Walk(Human human, IMovementAlgorithm algo, Vector2Int target, int maxSteps = 60)
	{
		var visited = new List<Vector2Int> { human.position };
		for (int i = 0; i < maxSteps && human.position != target; i++)
		{
			if (!algo.TryGetNextStep(human, target, out Dir dir)) break;
			human.position += human.GetDirVector(dir);
			visited.Add(human.position);
		}
		return visited;
	}
}
#endif
