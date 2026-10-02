using System.Collections.Generic;
using UnityEngine;

// 한 번의 후보 선정(ComputeInvestigateTarget 호출 하나)이 공유하는 평가 문맥 — 프론티어 걸음 수 BFS를 후보마다 다시 돌리지 않고 한 번만 계산하고,
// 점유 판정 메모에 적중하지 않은 실제 탐색 횟수를 센다(후보 평가 상한).
public sealed class RouteContext
{
	public readonly Dictionary<Vector2Int, int> FrontierSteps = new Dictionary<Vector2Int, int>();
	public bool FrontierComputed;
	public bool FrontierAvailable;
	public int NewEvaluations;

	public void Reset()
	{
		FrontierSteps.Clear();
		FrontierComputed = false;
		FrontierAvailable = false;
		NewEvaluations = 0;
	}
}

// 검증 04-08(04번 문서 9장 "전체 길 미확인과 통행 불가의 구분 / 미확인 경로의 추정 이동거리"): 유닛이 지금 아는 정보로 목적지까지의 길을 평가한다.
// OccupancySystem과 같은 성격의 부수효과 있는 호출부 — 유닛·세션 상태를 읽고, 순수 판단은 RouteMath에 있다.
//
// 판정(RouteStatus):
//  1) 낙관 A*(미확인은 통행 가능으로 계획)로 도달 + 길의 모든 칸이 개인 지도에 확인됨 → FullyKnown(실제 칸수)
//  2) 도달하지만 미확인 구간이 있음 → PartlyUnknown(추정 = 프론티어까지 실제 걸음 수 + 프론티어→목적지 체비셰프의 최솟값)
//  3) 도달 못함 → 점유를 무시한 구조 경로로는 도달 → 점유 때문인 일시 막힘(후보 유지, 04-05 대기·우회가 처리)
//  4) 구조 경로도 못함 → 함정 회피를 끈 구조 경로로는 도달 → TrapBlocked(후보 유지, 함정 대응이 처리)
//  5) 그래도 못함 → Unreachable("현재 정보로 통행 불가 확인") — 후보에서 제외한다.
// 낙관 A*는 "길이 있을 수도 있음"이면 항상 경로를 내므로 실패 = 이미 아는 정보로는 도달할 수 없다는 뜻이다. 새 정보(지형·문·함정)가 생기면 다음 평가에서 자동으로 다시 열린다.
public static class RouteAssessment
{
	// 조사는 목적지 인접 1칸(체비셰프)에 닿으면 도착이다(TacticalFSMState.MoveToInvestigateTarget의 IsAdjacent) — 목표 타일이 서 있을 수 없는 칸이어도 닿을 수 있으므로 같은 기준으로 판정한다.
	public const int ApproachRange = 1;
	private const int MemoCapacity = 64;

	// targetIsTrap: 목표가 함정 자체(해제 담당자의 도착시간 등)면 그 함정의 회피 구역을 면제하고 함정 타일 자체까지의 길이를 잰다(검증 03-13). 이 경우 판정 메모는 쓰지 않는다.
	public static RouteEstimate Assess(Unit unit, Vector3Int targetTile, bool targetIsTrap = false, RouteContext ctx = null)
	{
		Vector2Int target2D = new Vector2Int(targetTile.x, targetTile.y);
		var astar = unit.MovementAlgorithm as AStarMovement;
		if (astar == null)
			return new RouteEstimate(RouteStatus.PartlyUnknown, RouteMath.ChebyshevDistance(unit.position, target2D)); // 길찾기 알고리즘이 없으면 직선 거리 근사

		var cfg = AIConfigLoader.Behavior;
		// 개인 지도 개정으로 서명을 만들 수 있을 때만 메모한다(공용 지도 폴백 중에는 지형 변화를 서명에 담을 수 없다).
		bool memoEnabled = !targetIsTrap && (cfg?.personalMapPathingEnabled ?? true);
		RouteSignature signature = default;
		float now = Time.time;
		if (memoEnabled)
		{
			signature = SignatureOf(unit);
			if (unit.unreachableRouteMemo.TryGetValue(targetTile, out RouteMemoEntry memo)
				&& RouteMath.IsUnreachableMemoValid(memo, signature, now, cfg?.routeRecheckMinSeconds ?? 2f))
				return new RouteEstimate(RouteStatus.Unreachable, RouteMath.UnreachableDistanceTiles);
		}

		ctx ??= new RouteContext();
		ctx.NewEvaluations++;

		Vector2Int? exemptTrap = targetIsTrap ? target2D : (Vector2Int?)null;
		int range = targetIsTrap ? -1 : ApproachRange;

		if (astar.TryGetPathLength(unit, target2D, out int length, out bool fullyRevealed, exemptTrap, range))
		{
			unit.unreachableRouteMemo.Remove(targetTile);
			return fullyRevealed
				? new RouteEstimate(RouteStatus.FullyKnown, length)
				: new RouteEstimate(RouteStatus.PartlyUnknown, EstimateUnknownSegment(unit, astar, target2D, length, ctx));
		}

		// 도달 실패 — 점유 때문인 일시 막힘인가
		AStarMovement structural = OccupancySystem.GetTwin(unit, astar);
		if (structural.TryGetPathLength(unit, target2D, out int structuralLength, out bool structuralRevealed, exemptTrap, range))
		{
			unit.unreachableRouteMemo.Remove(targetTile);
			return new RouteEstimate(structuralRevealed ? RouteStatus.FullyKnown : RouteStatus.PartlyUnknown, structuralLength);
		}

		// 알려진 함정 구역 때문에만 못 가는가 — 후보로 남겨야 접근 시도에서 막힘 신호(AStarMovement.DiagnoseTrapBlock)가 나와 함정 대응이 재개된다.
		AStarMovement trapOff = GetTrapOffTwin(unit, astar);
		if (trapOff.TryGetPathLength(unit, target2D, out int trapOffLength, out _, exemptTrap, range))
		{
			unit.unreachableRouteMemo.Remove(targetTile);
			return new RouteEstimate(RouteStatus.TrapBlocked, trapOffLength);
		}

		if (memoEnabled)
		{
			if (unit.unreachableRouteMemo.Count >= MemoCapacity) unit.unreachableRouteMemo.Clear();
			unit.unreachableRouteMemo[targetTile] = new RouteMemoEntry { Signature = signature, Time = now };
		}
		return new RouteEstimate(RouteStatus.Unreachable, RouteMath.UnreachableDistanceTiles);
	}

	// 04번 9장 추정 이동거리: 프론티어까지 실제 걸음 수 + 프론티어→목적지 최소 칸수의 최솟값. 인류가 아니거나 도달 가능한 프론티어가 없으면 낙관 A* 길이(아는 구간 + 미확인을 가로지르는 구간)로 근사한다.
	private static int EstimateUnknownSegment(Unit unit, AStarMovement astar, Vector2Int target, int optimisticLength, RouteContext ctx)
	{
		if (unit is Human human)
		{
			if (!ctx.FrontierComputed)
			{
				ctx.FrontierComputed = true;
				ctx.FrontierAvailable = astar.TryComputeFrontierSteps(unit, human.personalMap.GetFrontierTiles(unit.currentFloor), ctx.FrontierSteps);
			}
			if (ctx.FrontierAvailable)
			{
				int? estimate = RouteMath.EstimateViaFrontier(ctx.FrontierSteps, target);
				if (estimate.HasValue) return estimate.Value;
			}
		}
		return optimisticLength;
	}

	private static RouteSignature SignatureOf(Unit unit)
	{
		IKnownTerrain known = unit.KnownTerrain;
		int traps = unit is Human human ? human.personalMap.KnownTrapTiles.Count : 0;
		int doors = unit.Session != null ? unit.Session.DoorStateVersion : 0;
		return new RouteSignature(unit.currentFloor, known != null ? known.TerrainRevision : 0, traps, doors);
	}

	// ── 탐험 목표 막힘 기록 (검증 04-08 발견 5, 04번 9장 '막힘 기록과 재시도 조건') ─────────────
	// 탐험이 길찾기로 닿지 못한 미탐색 목표를 (서명, 시각)으로 기록해 두고, 같은 서명이면 다시 고르지 않는다 — 서명이 바뀌어도 기록 직후 routeRecheckMinSeconds 안에는 그대로다
	// (조사 후보의 통행 불가 메모와 같은 규칙, RouteMath.IsUnreachableMemoValid). 점유는 서명에 없어 점유만 원인이면 아는 정보가 바뀔 때까지 건너뛸 수 있다 — 탐험 중엔 새 타일을 밝히며 곧 바뀐다.
	public static void MarkExploreBlocked(Unit unit, Vector2Int tile)
	{
		if (unit.exploreBlockedTargets.Count >= MemoCapacity) unit.exploreBlockedTargets.Clear();
		unit.exploreBlockedTargets[new Vector3Int(tile.x, tile.y, unit.currentFloor)] = new RouteMemoEntry { Signature = SignatureOf(unit), Time = Time.time };
	}

	// 지금 유효한 막힘 기록으로 제외할 타일을 가려내는 필터(프론티어 선택·방 탐색 완료 판정용). 끄거나(exploreBlockedRecordEnabled) 기록이 없으면 null — 호출부는 null이면 필터 없이 훑는다.
	public static System.Predicate<Vector2Int> CreateExploreBlockFilter(Unit unit)
	{
		var cfg = AIConfigLoader.Behavior;
		if (!(cfg?.exploreBlockedRecordEnabled ?? true) || unit == null || unit.exploreBlockedTargets.Count == 0) return null;

		RouteSignature signature = SignatureOf(unit);
		float now = Time.time;
		float recheck = cfg?.routeRecheckMinSeconds ?? 2f;
		int floor = unit.currentFloor;
		var memos = unit.exploreBlockedTargets;
		return tile => memos.TryGetValue(new Vector3Int(tile.x, tile.y, floor), out RouteMemoEntry memo)
			&& RouteMath.IsUnreachableMemoValid(memo, signature, now, recheck);
	}

	// 구조 경로 쌍둥이(점유 무시)에서 함정 회피까지 끈 것 — 유닛마다 한 번 만들어 캐시한다(이동 알고리즘이 바뀌면 다시 만든다).
	private static AStarMovement GetTrapOffTwin(Unit unit, AStarMovement astar)
	{
		if (unit.routeTrapOffTwin == null || !ReferenceEquals(unit.routeTrapOffTwinSource, astar))
		{
			AStarMovement twin = astar.CreateStructuralTwin();
			twin.TrapModeOverride = TrapMoveMode.Off;
			unit.routeTrapOffTwin = twin;
			unit.routeTrapOffTwinSource = astar;
		}
		return unit.routeTrapOffTwin;
	}
}
