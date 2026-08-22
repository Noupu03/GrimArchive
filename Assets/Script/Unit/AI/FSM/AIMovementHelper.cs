using UnityEngine;

public static class AIMovementHelper
{
	// 체비셰프(8방향 격자) 거리 — CombatFSMState/PlayerCommandFSMState/TacticalFSMState(함정 접근/조사
	// 목표/코어 접근)가 각자 Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) 형태로 동일하게 중복 구현하고
	// 있던 것을 통합했다(2026-08-20).
	public static int ChebyshevDistance(Vector2Int a, Vector2Int b)
		=> Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

	// 인접(기본 반경 1칸, 대각 포함) 판정 — 위 거리 계산의 가장 흔한 용도.
	public static bool IsAdjacent(Vector2Int a, Vector2Int b, int radius = 1)
		=> ChebyshevDistance(a, b) <= radius;

	// 플레이어 진영 몬스터 방 제한 MVP(2026-07-27) — FactionBehavior 타입을 나열하는 대신 실제 방 제한
	// 여부를 결정하는 MovementAlgorithm(RoomConfinedMovement)을 직접 확인해 단일 기준으로 통일한다.
	// CombatFSMState/NavigationFSMState가 각자 동일한 `is RoomConfinedMovement` 검사를 중복하고
	// 있던 것을 통합했다(2026-08-20).
	public static bool IsRoomConfined(Unit unit) => unit.MovementAlgorithm is RoomConfinedMovement;

	// 방 제한 유닛의 무작위 인접 이동 — 8방향 중 유효한 칸(문/방 경계/벽/유닛 점유/대각선 코너 커팅 확인)을
	// 찾아 1칸 이동한다. NavigationFSMState.MoveRandomlyValid(자유탐색 폴백)와 IdleFSMState(대기 상태
	// 배회, 2026-08-20 신규)가 공유한다 — anchor/radius를 주면(체비쇼프 거리) 그 범위를 벗어나는 칸은
	// 후보에서 제외해 "배회 기준점 주변 N칸"으로 반경을 제한할 수 있다(기본값은 무제한이라 기존
	// MoveRandomlyValid 동작과 동일).
	// forceRoomConfine(2026-08-20, 사용자 요청 "대기로 인한 이동 중일때는, 방 밖으로 나가면 안됨(문이
	// 있는 타일도 안됨)") — 기본값(false)은 지금까지처럼 유닛의 MovementAlgorithm이 RoomConfinedMovement
	// 일 때만 방/문 제한을 건다(NavigationFSMState의 자유탐색은 인류처럼 방 제한이 없는 유닛도 호출하므로
	// 이 동작을 유지해야 한다). true면 MovementAlgorithm 종류와 무관하게 무조건 "현재 방 밖 금지 + 문
	// 타일 금지"를 강제한다 — IdleFSMState가 이 값으로 호출해서, 스폰 경로에 따라 우연히
	// RoomConfinedMovement가 안 붙은 플레이어 몬스터가 있더라도 대기 배회만큼은 절대 방을 벗어나지
	// 않도록 보장한다.
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
	// (NavigationFSMState.CrossStairs, HumanWaveManager의 강제 이동/퇴각)이 전부 이 헬퍼를 거쳐야
	// 한다 — 2026-08-05 사용자 신고 "유닛끼리 겹친다"의 원인이 바로 이 지점들이었다: 전부
	// CreateMap.TryGetStairApproachPosition의 힌트 없는 오버로드(항상 같은 대표 좌표 1칸만 반환)를
	// 점유 확인 없이 그대로 썼다. 계단 "접근" 측(NavigationFSMState.MoveToStairs)은 이미
	// TryGetStairApproachCandidates + unitGrid 점유 확인으로 여러 후보 중 빈 칸을 고르고 있었는데
	// (2026-07-23 병목 수정), "도착" 측만 그 수정이 안 돼 있었다 — 여러 인류가 같은 계단으로 동시에
	// 넘어가면 전부 같은 한 칸에 텔레포트돼 겹쳤다.
	// 반환값: 빈 후보를 찾았으면 true(pos에 담김) / 후보 전부 점유(극단적 혼잡) 또는 계단 정보 자체를
	// 못 찾으면 false — 호출부가 "이번엔 실패, 나중에 재시도"로 처리할지 판단한다.
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

	// 반환값: 실제로 한 칸이라도 다가갈 수 있었는지(true) / 더 다가갈 방법이 전혀 없어 제자리에
	// 머물렀는지(false, 목표 칸이 다른 유닛/벽으로 완전히 막혀 있고 이미 갈 수 있는 가장 가까운
	// 지점까지 도달한 상태). 호출부(PlayerCommandFSMState)가 이 신호로 "길이 막혔다"를 판단해
	// 목표를 재지정한다.
	public static bool MoveTowardsPos(Unit unit, Vector2Int targetPos)
	{
		if (unit.MovementAlgorithm != null && unit.MovementAlgorithm.TryGetNextStep(unit, targetPos, out Dir nextDir))
		{
			// "방향을 받았다"가 아니라 "실제로 움직였다"를 반환한다(2026-08-22, 사용자 신고 "2*2문에서
			// 1개 문만 남겨두고 이동할때 중간에 멈춤, 명령해도 안먹음") — A*와 Move()의 판정이 어긋나면
			// (코너 커팅/같은 프레임 내 점유 변화 등) Move()가 조용히 실패하는데, 예전엔 그래도 true를
			// 반환해 호출부(PlayerCommandFSMState 등)가 stuckTurns를 리셋하며 "정상 이동 중"으로 오판해
			// 아무 피드백 없이 영원히 얼어붙었다. 실패를 false로 드러내면 기존 혼잡 인내/포기 로직이
			// 그대로 안전망이 된다.
			Vector2Int before = unit.position;
			unit.Move(nextDir);
			return unit.position != before;
		}
		return false;
	}

	public static bool MoveTowardsTarget(Unit unit, Unit target)
		=> MoveTowardsPos(unit, target.position);

	// 목표 칸이 막혀 더 다가갈 수 없을 때, 바로 옆 8칸 중 실제로 갈 수 있는 가장 가까운(유닛 현재
	// 위치 기준) 빈 칸을 대신 반환한다(사용자 요청, 2026-07-24 "길이 막혀서 플레이어의 이동, 공격
	// 명령을 수행하지 못하면, 근처 바로 옆의 빈칸으로 목표 재지정"). 갈 수 있는 칸이 하나도 없으면
	// center를 그대로 돌려준다 — 호출부가 "재지정도 불가능"으로 판단해 처리한다.
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

	// 명령 포기 오판 방지(2026-07-28, 사용자 신고 "자꾸 전투중에 한번씩 플레이어 명령 무시해") —
	// FindNearbyOpenTile이 "갈 수 있는 칸이 하나도 없다"고 판단해도, 그게 진짜 벽/닫힌 문 때문인지
	// 아니면 전투 중 다른 유닛들이 그 순간 잠깐 몰려서(점유) 막힌 것뿐인지를 구분하지 못했다.
	// PlayerCommandFSMState.ExecutePlayerMove가 후자까지 "완전히 막힘"으로 오판해 명령을 그 자리에서
	// 영구히 취소해 버렸다 — 혼잡한 전투에서 한 틱만 지나면 풀릴 상황인데도 명령이 사라지는 원인.
	// 여기서는 CanMove(ignoreUnits: true)로 유닛 점유를 무시하고 "지형만" 기준으로 재확인한다.
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

	// 유닛 "자기 자신의 현재 위치" 기준 혼잡 판정 전용(2026-08-23, PlayerCommandFSMState 좁은 병목
	// 간헐적 정지 수정) — 위 HasAnyStructurallyOpenNeighbor(unit, center)를 center=unit.position으로
	// 그대로 호출하면 안 된다: 그 함수의 첫 줄이 "center 자신이 열려있는지"부터 확인하는데, center가
	// 유닛이 이미 서 있는 칸이면 당연히 항상 열려있어(트루) 검사 자체가 무의미해진다. 여기서는 자기
	// 자신은 제외하고 인접 8칸만(점유 무시) 확인 — 그중 하나라도 갈 수 있으면 "지금은 다른 유닛이
	// 막고 있을 뿐 구조적으로는 갈 곳이 있다"(혼잡, 인내 대기), 8칸 전부 벽/닫힌 문이면 "진짜 완전히
	// 막힘"(포기)으로 판정한다.
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
