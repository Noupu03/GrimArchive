using System.Collections.Generic;
using UnityEngine;

// 후퇴·도주 이동이 막혔는지, 무엇이 막는지 판정한다(후퇴도주 명세서 5-2·5-3장). 기준 경로 = 같은 목적지까지 적 점유와 함정을 빼고 계산한 경로, 실제 경로 = 유닛의 이동 알고리즘이 적 점유·알려진 함정·닫힌 문을 반영해 계산한 경로. 실제 − 기준이 추가 우회 한도(기본 6칸)를 넘거나 실제 경로가 없으면 "막힘"이고, 막는 요소(문/적/함정)를 식별한다.
// 부수효과는 없다(경로 조회 전용 — 유닛의 이동 캐시 불변). 대응 실행은 RetreatMovement가 한다.
public static class RetreatBlockResolver
{
	public readonly struct Verdict
	{
		public readonly bool Blocked;
		public readonly RetreatBlockKind Kind;
		public readonly Vector3Int ObjectPos; // Door/Trap일 때 대상 오브젝트 위치
		public readonly Unit Enemy;           // Enemy일 때 가장 먼저 만나는 적
		public Verdict(bool blocked, RetreatBlockKind kind, Vector3Int objectPos = default, Unit enemy = null)
		{ Blocked = blocked; Kind = kind; ObjectPos = objectPos; Enemy = enemy; }

		public static readonly Verdict Clear = new Verdict(false, RetreatBlockKind.None);
	}

	// 기준 경로 조회 전용 스크래치 — 함정을 무시(TrapMoveMode.Off)하고 적 점유를 장애물로 보지 않는다. 유닛의 이동 인스턴스와 별개라 이동 캐시를 오염시키지 않는다.
	static readonly AStarMovement _baseline = new AStarMovement { TrapModeOverride = TrapMoveMode.Off };

	// 실제 경로 조회 전용 스크래치 — 유닛의 이동 알고리즘이 아니라 평범한 A*를 쓴다. 원거리·지원 역할은 교전을 겪으면 AttackRangeAvoidingMovement로 승급되는데, 그쪽은 알려진 적 사거리 타일마다 비용 100000을 물려 노출을 피하려고 아주 긴 우회도 고른다. 그 우회 길이를 "길이 막혀 돌아간 것"으로 세면 막히지도 않았는데 6칸 한도를 넘어 적과 교전하게 된다. 함정 모드는 유닛 상태(RetreatFSMState → Retreat)에서 정해지고 점유·닫힌 문·방 이탈 금지도 같은 기준으로 반영된다.
	static readonly AStarMovement _actual = new AStarMovement();

	const int ScanLimitTiles = 80;

	// 기준 경로 길이(적 점유·함정을 빼고 벽·닫힌 문·지형은 그대로) — 거점 후보의 "닿을 수 있는가"와 가까운 순 비교에 쓴다. 적이 일시적으로 길을 막았다는 이유로 거점을 이용 불가로 보지 않기 위해 실제 경로가 아니라 이 기준 경로를 쓴다(명세서 5-2장).
	public static bool TryGetBaselineLength(Human h, Vector2Int target, out int length)
	{
		length = 0;
		// 후퇴 대상(다른 방의 거점·출구)을 묻는 조회라 방 이탈 금지를 조회 동안만 푼다 — 안 풀면 지금 방 안에 갇힌 경로만 나와 모든 거점이 닿지 못하는 것으로 보인다.
		h.SuspendRoomConfinement();
		try
		{
			if (!_baseline.TryGetPathTiles(h, target, out List<Vector2Int> tiles, ignoreEnemyUnits: true)) return false;
			length = tiles.Count;
			return true;
		}
		finally { h.ResumeRoomConfinement(); }
	}

	public static Verdict Evaluate(Human h, Vector2Int target, RetreatMoveState move)
	{
		if (h.position == target) { move.ResetSegment(); return Verdict.Clear; }
		bool actualOk = _actual.TryGetPathLength(h, target, out int actualLen, out _);
		bool baselineOk = _baseline.TryGetPathTiles(h, target, out List<Vector2Int> baselineTiles, ignoreEnemyUnits: true);
		int limit = RetreatSystem.DetourExtraTiles;

		if (actualOk && baselineOk)
		{
			int baselineLen = baselineTiles.Count;
			if (actualLen <= baselineLen) { move.ResetSegment(); return Verdict.Clear; }

			// 길이 막혀 돌아가는 구간의 시작 — 이때 잰 기준 길이를 구간이 끝날 때까지 유지한다(재탐색으로 한도를 초기화하지 않음).
			if (move.SegmentBaseline < 0) { move.SegmentBaseline = baselineLen; move.SegmentWalked = 0; }
			int total = RetreatPathMath.TotalDetourLength(move.SegmentWalked, actualLen);
			if (RetreatPathMath.IsDetourAllowed(move.SegmentBaseline, total, limit)) return Verdict.Clear;
			return IdentifyOnPath(h, baselineTiles);
		}
		if (baselineOk) return IdentifyOnPath(h, baselineTiles); // 적·함정을 치우면 닿는다 — 돌아갈 길이 전혀 없음
		return IdentifyStructural(h, target);                    // 적·함정을 빼도 못 간다 — 닫힌 상대 진영 문 등 구조적 차단
	}

	// 기준 경로를 앞에서부터 훑어 처음 만나는 방해물: 알고 있는 함정 타일 → 점유한 적 순.
	static Verdict IdentifyOnPath(Human h, List<Vector2Int> path)
	{
		var session = h.Session;
		int n = Mathf.Min(path.Count, ScanLimitTiles);
		for (int i = 0; i < n; i++)
		{
			var key = new Vector3Int(path[i].x, path[i].y, h.currentFloor);
			if (IsKnownActiveTrap(h, key)) return new Verdict(true, RetreatBlockKind.Trap, key);
			if (session != null && session.unitGrid.TryGetValue(key, out Unit u) && u != null && u != h && u.hp > 0f && h.IsEnemy(u))
				return new Verdict(true, RetreatBlockKind.Enemy, default, u);
		}
		return new Verdict(true, RetreatBlockKind.Unreachable);
	}

	static bool IsKnownActiveTrap(Human h, Vector3Int tile)
	{
		if (h.Session == null) return false;
		bool known = false;
		foreach (var knownTile in h.personalMap.KnownTrapTiles.Values)
			if (knownTile == tile) { known = true; break; }
		if (!known) return false;
		return h.Session.objectGrid.TryGetValue(tile, out InteractableObject obj) && !obj.IsCollected && obj.Tags != null && obj.Tags.Exists(t => t.Contains("Trap"));
	}

	// 목적지에 가장 가까이 갈 수 있는 지점(TraceTowards)의 이웃에서 닫힌 상대 진영 문을 찾는다.
	static Verdict IdentifyStructural(Human h, Vector2Int target)
	{
		var session = h.Session;
		if (session == null) return new Verdict(true, RetreatBlockKind.Unreachable);

		Vector2Int end = h.position;
		if (_baseline.TraceTowards(h, target, out List<Vector2Int> traced, out _) && traced.Count > 0) end = traced[traced.Count - 1];

		bool found = false;
		Vector3Int bestPos = default;
		int bestDist = int.MaxValue;
		for (int dx = -2; dx <= 2; dx++)
		{
			for (int dy = -2; dy <= 2; dy++)
			{
				var key = new Vector3Int(end.x + dx, end.y + dy, h.currentFloor);
				if (!session.objectGrid.TryGetValue(key, out InteractableObject obj) || obj.Tags == null || !obj.Tags.Contains(DoorSystem.DoorTag)) continue;
				if (obj.DoorHp <= 0f || !session.IsBlockedByClosedDoor(key, h)) continue;
				int dist = AIMovementHelper.ChebyshevDistance(new Vector2Int(key.x, key.y), target);
				if (dist < bestDist) { bestDist = dist; bestPos = key; found = true; }
			}
		}
		return found ? new Verdict(true, RetreatBlockKind.Door, bestPos) : new Verdict(true, RetreatBlockKind.Unreachable);
	}
}
