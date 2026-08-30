using UnityEngine;

public static class AIMovementHelper
{
	// 체비셰프(8방향 격자) 거리 — 여러 FSM 상태가 각자 중복 구현하던 것을 통합.
	public static int ChebyshevDistance(Vector2Int a, Vector2Int b)
		=> Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

	// 인접(기본 반경 1칸, 대각 포함) 판정 — 위 거리 계산의 가장 흔한 용도.
	public static bool IsAdjacent(Vector2Int a, Vector2Int b, int radius = 1)
		=> ChebyshevDistance(a, b) <= radius;

	// FactionBehavior 타입을 나열하는 대신, 실제 방 제한 여부를 결정하는 MovementAlgorithm
	// (RoomConfinedMovement)을 직접 확인해 단일 기준으로 통일한다.
	public static bool IsRoomConfined(Unit unit) => unit.MovementAlgorithm is RoomConfinedMovement;

	// 방 제한 유닛의 무작위 인접 이동 — 8방향 중 유효한 칸을 찾아 1칸 이동하며, NavigationFSMState.
	// MoveRandomlyValid와 IdleFSMState가 공유한다. anchor/radius로 배회 반경을 제한할 수 있다(기본
	// 무제한). forceRoomConfine=true면 MovementAlgorithm 종류와 무관하게 방/문 제한을 강제한다 —
	// RoomConfinedMovement가 안 붙은 유닛도 대기 배회만큼은 방을 벗어나지 않게 하려는 용도로
	// IdleFSMState가 사용한다.
	public static bool TryMoveRandomlyWithinRadius(Unit unit, Vector2Int? anchor = null, int radius = int.MaxValue, bool forceRoomConfine = false)
	{
		bool roomConfined = forceRoomConfine || IsRoomConfined(unit);
		Room myRoom = null;
		if (roomConfined && unit.Session?.roomGrid != null)
			unit.Session.roomGrid.TryGetValue(new Vector3Int(unit.position.x, unit.position.y, unit.currentFloor), out myRoom);

		int startOffset = Random.Range(0, 8);
		for (int i = 0; i < 8; i++)
		{
			Dir tryDir = (Dir)((startOffset + i) % 8);
			Vector2Int dirVec = unit.GetDirVector(tryDir);
			Vector2Int nextPos = unit.position + dirVec;
			if (!unit.CanMove(nextPos)) continue;

			if (anchor.HasValue && ChebyshevDistance(nextPos, anchor.Value) > radius) continue;

			if (roomConfined && unit.Session != null)
			{
				if (unit.Session.IsDoorTile(new Vector3Int(nextPos.x, nextPos.y, unit.currentFloor)))
					continue;
				if (myRoom != null)
				{
					if (!unit.Session.roomGrid.TryGetValue(new Vector3Int(nextPos.x, nextPos.y, unit.currentFloor), out Room nextRoom)
						|| nextRoom != myRoom)
						continue;
				}
			}

			// Move()의 대각선 코너 커팅 방지 로직과 동일하게 먼저 검사한다.
			if (Mathf.Abs(dirVec.x) == 1 && Mathf.Abs(dirVec.y) == 1)
			{
				if (!unit.CanMove(unit.position + new Vector2Int(dirVec.x, 0)) ||
					!unit.CanMove(unit.position + new Vector2Int(0, dirVec.y))) continue;
			}
			unit.Move(tryDir);
			return true;
		}
		return false;
	}

	// 계단 도착(순간이동) 지점을 점유 없는 칸으로 고른다. CanMove를 거치지 않는 순간이동성 이동
	// (NavigationFSMState.CrossStairs, HumanWaveManager 강제 이동/퇴각)이 전부 이 헬퍼를 거쳐야 한다 —
	// 대표 좌표 1칸만 쓰면 여러 유닛이 같은 계단으로 동시에 텔레포트돼 겹친다("접근" 측 MoveToStairs와
	// 동일하게 여러 후보 중 빈 칸을 고른다). 반환값 false는 후보 전부 점유(혼잡) 또는 계단 정보 없음 —
	// 호출부가 재시도 여부를 판단한다.
	public static bool TryResolveUnoccupiedStairArrival(GameSession session, int arrivalFloor, int fromFloor, out Vector2Int pos)
	{
		pos = Vector2Int.zero;
		if (session?.cmap == null) return false;
		if (!session.cmap.TryGetStairApproachCandidates(arrivalFloor, fromFloor, out var candidates) || candidates.Count == 0)
			return false;

		foreach (var c in candidates)
		{
			bool occupied = session.unitGrid.TryGetValue(new Vector3Int(c.x, c.y, arrivalFloor), out Unit occupant)
				&& occupant != null && occupant.hp > 0;
			if (!occupied) { pos = c; return true; }
		}
		return false;
	}

	// 반환값: 실제로 한 칸이라도 다가갔으면 true, 목표 칸이 완전히 막혀 제자리에 머물렀으면 false —
	// 호출부(PlayerCommandFSMState)가 이 신호로 "길이 막혔다"를 판단해 목표를 재지정한다.
	public static bool MoveTowardsPos(Unit unit, Vector2Int targetPos)
	{
		if (unit.MovementAlgorithm != null && unit.MovementAlgorithm.TryGetNextStep(unit, targetPos, out Dir nextDir))
		{
			// "방향을 받았다"가 아니라 "실제로 움직였다"를 반환한다 — A*와 Move()의 판정이 어긋나면
			// (코너 커팅 등) Move()가 조용히 실패할 수 있는데, true를 그대로 반환하면 호출부가
			// stuckTurns를 리셋해 영원히 얼어붙는 오판을 한다.
			Vector2Int before = unit.position;
			unit.Move(nextDir);
			return unit.position != before;
		}
		return false;
	}

	public static bool MoveTowardsTarget(Unit unit, Unit target)
		=> MoveTowardsPos(unit, target.position);

	// 목표 칸이 막혀 더 다가갈 수 없을 때, 바로 옆 8칸 중 실제로 갈 수 있는 가장 가까운(유닛 현재
	// 위치 기준) 빈 칸을 대신 반환한다. 갈 수 있는 칸이 하나도 없으면 center를 그대로 돌려준다 —
	// 호출부가 "재지정도 불가능"으로 판단해 처리한다.
	public static Vector2Int FindNearbyOpenTile(Unit unit, Vector2Int center)
	{
		Vector2Int best = center;
		int bestDist = int.MaxValue;
		for (int dx = -1; dx <= 1; dx++)
		for (int dy = -1; dy <= 1; dy++)
		{
			if (dx == 0 && dy == 0) continue;
			Vector2Int cand = center + new Vector2Int(dx, dy);
			if (!unit.CanMove(cand)) continue;
			int dist = ChebyshevDistance(cand, unit.position);
			if (dist < bestDist) { bestDist = dist; best = cand; }
		}
		return best;
	}

	// 명령 포기 오판 방지 — 벽/닫힌 문 때문인지, 다른 유닛이 잠깐 몰려(점유) 막힌 것뿐인지 구분해야
	// 한다. 후자를 완전히 막힘으로 오판하면 곧 풀릴 상황에서도 명령이 영구히 취소되므로, CanMove
	// (ignoreUnits: true)로 점유를 무시하고 지형만 기준으로 재확인한다.
	public static bool HasAnyStructurallyOpenNeighbor(Unit unit, Vector2Int center)
	{
		if (unit.CanMove(center, ignoreUnits: true)) return true;
		for (int dx = -1; dx <= 1; dx++)
		for (int dy = -1; dy <= 1; dy++)
		{
			if (dx == 0 && dy == 0) continue;
			if (unit.CanMove(center + new Vector2Int(dx, dy), ignoreUnits: true)) return true;
		}
		return false;
	}

	// 유닛 자기 위치 기준 혼잡 판정 전용 — HasAnyStructurallyOpenNeighbor(unit, unit.position)를 그대로
	// 쓰면 안 된다(center 자신이 항상 열려있어 검사가 무의미해짐). 여기서는 자기 자신은 제외하고 인접
	// 8칸만(점유 무시) 확인해 하나라도 갈 수 있으면 혼잡(인내), 전부 막히면 완전히 막힘(포기)으로
	// 판정한다.
	public static bool HasAnyStructurallyOpenAdjacentTile(Unit unit)
	{
		Vector2Int center = unit.position;
		for (int dx = -1; dx <= 1; dx++)
		for (int dy = -1; dy <= 1; dy++)
		{
			if (dx == 0 && dy == 0) continue;
			if (unit.CanMove(center + new Vector2Int(dx, dy), ignoreUnits: true)) return true;
		}
		return false;
	}

	public static void MoveAwayFromTarget(Unit unit, Unit target, float desiredDist)
	{
		Vector2 away = (Vector2)(unit.position - target.position);
		if (away == Vector2.zero) away = Vector2.right;

		float maxComp = Mathf.Max(Mathf.Abs(away.x), Mathf.Abs(away.y));
		Vector2 scaled = away / maxComp;

		int targetDist = Mathf.RoundToInt(desiredDist);
		Vector2Int retreatPos = target.position + new Vector2Int(
			Mathf.RoundToInt(scaled.x * targetDist),
			Mathf.RoundToInt(scaled.y * targetDist)
		);
		MoveTowardsPos(unit, retreatPos);
	}

	// 03문서 6장 보호 포메이션 이동 로직 — backDistance<=1이면 근접, 더 크면 원거리 배치.
	public static void MoveToEscortSlot(Human human, float backDistance)
	{
		if (human.currentFormation == null || human.currentFormation.EscortTarget == null || !human.currentFormation.EscortTarget.IsInteracting)
		{
			Human target = human.FindDirectlyVisibleInteractingAlly();
			if (target == null) { human.currentFormation = null; return; }
			human.currentFormation = new FormationState { EscortTarget = target };
		}

		Human escortTarget = human.currentFormation.EscortTarget;
		Vector2Int slot = human.GetEscortSlotPosition(escortTarget, backDistance);
		MoveTowardsPos(human, slot);
	}
}
