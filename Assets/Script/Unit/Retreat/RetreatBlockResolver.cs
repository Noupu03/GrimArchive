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

	// 닿을 수 있는가·가까운 순 조회 전용 구조 경로 스크래치 — 함정뿐 아니라 모든 유닛 점유를 무시한다(벽·닫힌 문·지형은 그대로). 같은 방에 몰린 아군이나 잠깐 선 유닛을 장애물로 세면 거점이 "닿을 수 없음"으로 뒤집혀 한 번의 False로 거점 상실이 확정됐다(검증 D3).
	static readonly AStarMovement _baselineStructural = new AStarMovement { TrapModeOverride = TrapMoveMode.Off, IgnoreAllUnits = true };

	// 실제 경로 조회 전용 스크래치 — 유닛의 이동 알고리즘이 아니라 평범한 A*를 쓴다. 원거리·지원 역할은 교전을 겪으면 AttackRangeAvoidingMovement로 승급되는데, 그쪽은 알려진 적 사거리 타일마다 비용 100000을 물려 노출을 피하려고 아주 긴 우회도 고른다. 그 우회 길이를 "길이 막혀 돌아간 것"으로 세면 막히지도 않았는데 6칸 한도를 넘어 적과 교전하게 된다. 함정 모드는 유닛 상태(RetreatFSMState → Retreat)에서 정해지고 점유·닫힌 문·방 이탈 금지도 같은 기준으로 반영된다.
	static readonly AStarMovement _actual = new AStarMovement();

	const int ScanLimitTiles = 80;

	// 기준 경로 길이(모든 유닛 점유·함정을 빼고 벽·닫힌 문·지형은 그대로 — 구조 경로, 검증 D3) — 거점 후보의 "닿을 수 있는가"와 가까운 순 비교에 쓴다. 적이 일시적으로 길을 막았다는 이유로 거점을 이용 불가로 보지 않기 위해 실제 경로가 아니라 이 기준 경로를 쓴다(명세서 5-2장).
	public static bool TryGetBaselineLength(Human h, Vector2Int target, out int length)
	{
		length = 0;
		// 후퇴 대상(다른 방의 거점·출구)을 묻는 조회라 방 이탈 금지를 조회 동안만 푼다 — 안 풀면 지금 방 안에 갇힌 경로만 나와 모든 거점이 닿지 못하는 것으로 보인다.
		h.SuspendRoomConfinement();
		try
		{
			if (!_baselineStructural.TryGetPathTiles(h, target, out List<Vector2Int> tiles)) return false;
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

	// 문 경로 조회 전용 스크래치 — 닫힌 상대 진영 문을 통과 가능으로 보고 점유·함정은 무시한다. 이 경로를 따라 처음 만나는 닫힌 문이 먼저 부술 문이다(검증 D2).
	static readonly AStarMovement _doorPassable = new AStarMovement { TrapModeOverride = TrapMoveMode.Off, IgnoreAllUnits = true, IgnoreClosedDoors = true };

	// 닫힌 상대 진영 문 때문에 못 가는 경우 어느 문을 먼저 부술지 정한다 — ① 문을 통과 가능으로 본 경로를 따라 처음 만나는 닫힌 문(두 줄 게이트면 유닛 쪽 줄, 벽을 알게 된 뒤 문이 3칸 이상 떨어져 있어도 찾는다) ② 경로가 안 나오면 목적지에 가장 가까이 갈 수 있는 지점(TraceTowards) 이웃의 문.
	static Verdict IdentifyStructural(Human h, Vector2Int target)
	{
		var session = h.Session;
		if (session == null) return new Verdict(true, RetreatBlockKind.Unreachable);

		if (_doorPassable.TryGetPathTiles(h, target, out List<Vector2Int> doorPath) && TryFindFirstClosedDoor(h, doorPath, out Vector3Int firstDoor))
			return new Verdict(true, RetreatBlockKind.Door, firstDoor);

		Vector2Int end = h.position;
		if (_baseline.TraceTowards(h, target, out List<Vector2Int> traced, out _) && traced.Count > 0) end = traced[traced.Count - 1];

		_doorKeys.Clear();
		_doorTiles.Clear();
		for (int dx = -2; dx <= 2; dx++)
		{
			for (int dy = -2; dy <= 2; dy++)
			{
				var key = new Vector3Int(end.x + dx, end.y + dy, h.currentFloor);
				if (!session.objectGrid.TryGetValue(key, out InteractableObject obj) || obj.Tags == null || !obj.Tags.Contains(DoorSystem.DoorTag)) continue;
				if (obj.DoorHp <= 0f || !session.IsBlockedByClosedDoor(key, h)) continue;
				_doorKeys.Add(key);
				_doorTiles.Add(new Vector2Int(key.x, key.y));
			}
		}
		// 목적지에 가까운 먼 줄 문이 아니라 유닛이 실제로 먼저 닿는 문을 고른다(두 줄 게이트).
		int pick = RetreatPathMath.ChooseBlockingDoor(end, target, _doorTiles);
		return pick >= 0 ? new Verdict(true, RetreatBlockKind.Door, _doorKeys[pick]) : new Verdict(true, RetreatBlockKind.Unreachable);
	}

	// 경로(앵커 좌표 순서)를 앞에서부터 훑어 발자국이 처음 닿는 닫힌 상대 진영 문 타일.
	static bool TryFindFirstClosedDoor(Human h, List<Vector2Int> path, out Vector3Int doorKey)
	{
		doorKey = default;
		var session = h.Session;
		int fw = h.unitType != null ? Mathf.Max(1, (int)h.unitType.footprint.x) : 1;
		int fh = h.unitType != null ? Mathf.Max(1, (int)h.unitType.footprint.y) : 1;
		int n = Mathf.Min(path.Count, ScanLimitTiles * 2);
		for (int i = 0; i < n; i++)
		{
			for (int dx = 0; dx < fw; dx++)
			{
				for (int dy = 0; dy < fh; dy++)
				{
					var key = new Vector3Int(path[i].x + dx, path[i].y + dy, h.currentFloor);
					if (!session.objectGrid.TryGetValue(key, out InteractableObject obj) || obj.Tags == null || !obj.Tags.Contains(DoorSystem.DoorTag)) continue;
					if (obj.DoorHp <= 0f || !session.IsBlockedByClosedDoor(key, h)) continue;
					doorKey = key;
					return true;
				}
			}
		}
		return false;
	}

	static readonly List<Vector3Int> _doorKeys = new List<Vector3Int>();
	static readonly List<Vector2Int> _doorTiles = new List<Vector2Int>();
}
