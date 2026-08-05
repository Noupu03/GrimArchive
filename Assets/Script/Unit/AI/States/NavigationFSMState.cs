using UnityEngine;

// 탐색 상태 — 전투·전술·플레이어 명령 조건이 없을 때 활성화. 항상 Priority > 0 이므로 기본 상태로
// 동작. BT: 계단이동 → 자유탐색. 플레이어 공격/이동 명령은 PlayerCommandFSMState로 분리됐다(사용자
// 요청, 2026-07-24 "플레이어 지정 명령이라는 상태를 따로 만들어서 최우선 순위로 둬").
public class NavigationFSMState : IFSMState
{
	private readonly BTNode _bt;

	public NavigationFSMState()
	{
		_bt = new BTSelector(
			// 1. 계단 이동 (원본 StairsFSMState p=140)
			new BTSequence(
				new BTCondition(HasPendingStairs),
				new BTLeaf(MoveToStairs),
				new BTLeaf(CrossStairs)
			),
			// 2. 자유탐색 (원본 ExploreFSMState p=10)
			new BTLeaf(RandomExplore)
		);
	}

	// 항상 활성 — Combat/Tactical/PlayerCommand 조건이 없을 때 실질적인 기본값이 된다.
	public float GetPriority(Unit unit)    => AIConfigLoader.Behavior?.navigationPriority ?? 10f;
	public bool  IsSticky(Unit unit)       => false;
	public bool  ShouldInterrupt(Unit unit)=> true;
	public void  OnEnter(Unit unit)        { }
	public void  OnExit(Unit unit)         { }
	public BTStatus Tick(Unit unit)        => _bt.Tick(unit);
	public string   GetLabel(Unit unit)    => GetSubLabel(unit);

	// ── 조건 ─────────────────────────────────────────────────────

	private static bool HasPendingStairs(Unit unit)
	{
		if (!(unit is Human h)) return false;

		// 기존 조건: 루팅 등으로 인해 이미 계단으로 가야 하는 상태
		if (h.pendingStairTargetFloor.HasValue && h.pendingStairTargetFloor.Value != h.currentFloor)
			return true;

		// 0층(로비)은 웨이브가 시작될 때만 1층으로 넘어가야 한다(HumanWaveManager.StartWave 참고,
		// 사용자 요청 2026-07-24 "웨이브 시작할때에만 1층으로 이동시켜줘, 0->1층으로 전이를") — 사전
		// 스폰돼 대기 중 배회하던 유닛이 계단을 눈으로 발견했다고 여기서 자동으로 건너가 버리면 안
		// 된다. 아래 "계단을 직접 발견하면 즉시 향한다" 자동화는 이미 던전에 진입한 뒤(1층 이상)의
		// 층간 이동에만 적용한다.
		if (h.currentFloor == 0) return false;

		// 유저 피드백 반영: 계단을 직접 눈으로 찾았다면(personalMap에 계단 위치가 밝혀졌다면),
		// 맵을 끝까지 밝히겠다고 역주행하지 않고 즉시 계단으로 향하도록 목표 층을 세팅합니다.
		if (h.Session?.cmap != null)
		{
			int nextFloor = h.currentFloor + 1;
			if (h.Session.cmap.TryGetStairPosition(h.currentFloor, nextFloor, out Vector2Int stairPos))
			{
				// 계단 블록(좌상단 좌표)을 한 번이라도 시야로 본 적이 있다면
				if (h.personalMap.IsTileRevealed(new Vector3Int(stairPos.x, stairPos.y, h.currentFloor)))
				{
					h.pendingStairTargetFloor = nextFloor;
					return true;
				}
			}
		}

		return false;
	}

	// ── 계단 ─────────────────────────────────────────────────────

	// GoapWorldState.DistanceToStairBlock 이식 — 2x2 블록 자체까지의 체비쇼프 거리
	private static int DistanceToStairBlock(Vector2Int pos, Vector2Int stairBlockTopLeft)
	{
		int dx = Mathf.Max(Mathf.Max(stairBlockTopLeft.x - pos.x, 0), pos.x - (stairBlockTopLeft.x + 1));
		int dy = Mathf.Max(Mathf.Max(stairBlockTopLeft.y - pos.y, 0), pos.y - (stairBlockTopLeft.y + 1));
		return Mathf.Max(dx, dy);
	}

	private static BTStatus MoveToStairs(Unit unit)
	{
		if (!(unit is Human human) || !human.pendingStairTargetFloor.HasValue) return BTStatus.Failure;
		if (human.Session?.cmap == null) return BTStatus.Failure;
		int toFloor = human.pendingStairTargetFloor.Value;

		if (!human.Session.cmap.TryGetStairApproachCandidates(human.currentFloor, toFloor, out var candidates) || candidates.Count == 0)
		{
			return RandomExplore(human);
		}

		// 원본과 동일: 매 틱 비어있는 가장 가까운 후보 타일로 이동
		Vector2Int best = candidates[0];
		int bestScore = int.MaxValue;
		foreach (var c in candidates)
		{
			bool occupied = human.Session.unitGrid.TryGetValue(new Vector3Int(c.x, c.y, human.currentFloor), out Unit u)
				&& u != null && u != human && u.hp > 0;
			Vector2Int d = c - human.position;
			int score = (occupied ? 1000 : 0) + Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y));
			if (score < bestScore) { bestScore = score; best = c; }
		}

		if (human.position == best) return BTStatus.Success;
		AIMovementHelper.MoveTowardsPos(human, best);
		return BTStatus.Running;
	}

	private static BTStatus CrossStairs(Unit unit)
	{
		if (!(unit is Human human) || !human.pendingStairTargetFloor.HasValue) return BTStatus.Failure;
		if (human.Session?.cmap == null) return BTStatus.Failure;
		int fromFloor = human.currentFloor;
		int toFloor   = human.pendingStairTargetFloor.Value;

		if (!human.Session.cmap.TryGetStairPosition(fromFloor, toFloor, out Vector2Int stairPos))
			return BTStatus.Running;
		if (DistanceToStairBlock(human.position, stairPos) > (AIConfigLoader.Behavior?.stairArrivalRadius ?? 1))
			return BTStatus.Running;

		// 2026-08-05 사용자 신고 "유닛끼리 겹친다" 수정 — 힌트 없는 TryGetStairApproachPosition(항상
		// 같은 대표 좌표 1칸)로 점유 확인 없이 텔레포트하던 걸, MoveToStairs(접근 측)와 동일하게
		// 여러 후보 중 점유 안 된 칸을 고르는 방식으로 교체했다. 후보가 전부 점유돼 있으면(극단적
		// 혼잡) 실패로 보고 다음 틱에 재시도한다 — 절대 겹치는 칸으로는 텔레포트하지 않는다.
		if (!AIMovementHelper.TryResolveUnoccupiedStairArrival(human.Session, toFloor, fromFloor, out Vector2Int arrivePos))
			return BTStatus.Running;

		human.Session.UnregisterUnitPos(human, human.position);
		human.currentFloor = toFloor;
		human.position     = arrivePos;
		human.Session.RegisterUnitPos(human, human.position);
		human.pendingStairTargetFloor  = null;
		human.currentExplorationTarget = null; // 층 이동 후 이전 층 BFS 타깃을 초기화 — 새 층에서 처음부터 탐색
		return BTStatus.Success;
	}



	// ── 자유탐색 ─────────────────────────────────────────────────

	private static BTStatus RandomExplore(Unit unit)
	{
		FactionData data = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
		int fi = unit.currentFloor;
		if (data.discoveredMap == null || fi >= data.discoveredMap.Length || data.discoveredMap[fi] == null)
		{
			MoveRandomlyValid(unit);
			return BTStatus.Running;
		}

		int mapW = data.discoveredMap[fi].GetLength(0);
		int mapH = data.discoveredMap[fi].GetLength(1);

		// --- BFS 캐싱 로직 ---
		Human h = unit as Human;
		bool needsNewTarget = true;
		if (unit.currentExplorationTarget.HasValue)
		{
			Vector2Int cTarget = unit.currentExplorationTarget.Value;
			int targetTerrain = h != null ? h.personalMap.GetTileTerrain(new Vector3Int(cTarget.x, cTarget.y, fi)) : data.discoveredMap[fi][cTarget.x, cTarget.y];
			if (targetTerrain == 0) // 아직 미탐색 상태라면 기존 타겟 유지
			{
				needsNewTarget = false;
			}
		}

		Vector2Int? target = unit.currentExplorationTarget;
		if (needsNewTarget)
		{
			target = FindNearestUnexploredTarget(unit, data, fi, mapW, mapH);
			unit.currentExplorationTarget = target;
			// G: BFS(최대 3만 노드)와 A*(최대 5만 회)를 같은 틱에 돌리면 스파이크가 발생한다.
			// 타깃을 찾았으면 이번 틱은 BFS로 끝내고, A*는 다음 틱에 처리한다.
			if (target.HasValue) return BTStatus.Running;
		}
		// ---------------------

		if (target.HasValue)
		{
			if (unit.MovementAlgorithm != null && unit.MovementAlgorithm.TryGetNextStep(unit, target.Value, out Dir nextDir))
			{
				Vector2Int nextPos = unit.position + unit.GetDirVector(nextDir);
				if (!unit.CanMove(nextPos, ignoreUnits: true))
				{
					// 물리적인 벽인데 시야가 놓쳤다면, 뇌(discoveredMap)와 개인 지도에 벽(2)으로 강제 각인!
					if (nextPos.x >= 0 && nextPos.x < mapW && nextPos.y >= 0 && nextPos.y < mapH)
					{
						data.discoveredMap[fi][nextPos.x, nextPos.y] = 2;
						if (h != null) h.personalMap.RevealTile(new Vector3Int(nextPos.x, nextPos.y, fi), true);
					}
					unit.currentExplorationTarget = null; // 타겟 초기화
				}
				else
				{
					Vector2Int oldPos = unit.position;
					unit.Move(nextDir);
					
					if (unit.position == oldPos)
					{
						MoveRandomlyValid(unit);
					}
				}
			}
			else
			{
				// A* 경로 탐색 실패 (도달 불가능한 타겟)
				// 타겟이 대각선 코너 등에 가려진 닿을 수 없는 0(미탐색)일 수 있으므로 벽으로 치부하고 무시합니다.
				data.discoveredMap[fi][target.Value.x, target.Value.y] = 2;
				if (h != null) h.personalMap.RevealTile(new Vector3Int(target.Value.x, target.Value.y, fi), true);
				
				unit.currentExplorationTarget = null; // 영원히 A* 5만번 도는 것을 방지
				MoveRandomlyValid(unit);
			}
		}
		else
		{
			MoveRandomlyValid(unit);
		}
		return BTStatus.Running;
	}

	private static void MoveRandomlyValid(Unit unit)
	{
		// E: 1칸 인접 이동에 A*(TryGetNextStep)를 쓰면 _cacheTarget을 인접 좌표로 덮어써서
		// 다음 틱에 진짜 탐색 A*가 반드시 캐시 미스를 낸다. CanMove로 직접 검사해서 A*를 완전히 우회한다.
		// 단 방 제한 유닛(RoomConfinedMovement)은 CanMove가 방 경계를 확인하지 않으므로 별도로 검사한다.
		bool roomConfined = unit.MovementAlgorithm is RoomConfinedMovement;
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

			// 방 제한 유닛: RoomConfinedMovement.IsTileWalkable와 동일 기준(문 타일 + roomGrid) 재적용.
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
			return;
		}
	}

	private static Vector2Int? FindNearestUnexploredTarget(Unit unit, FactionData data, int fi, int mapW, int mapH)
	{
		// 2026-07-31 최적화 — 인류 유닛(방 제한 없이 던전 전체를 탐사)은 personalMap이 RevealTile마다
		// 유지하는 프론티어(미탐사 경계) 집합에서 바로 최근접 후보를 찾는다. 예전 BFS는 이미 탐색된
		// 영역 전체를 매번 다시 훑어야 해서 탐사가 진행될수록 호출 1번의 비용이 계속 늘어났다
		// (프로파일러 확인: 84회 호출에 134ms). 방 제한 유닛(몬스터, RoomConfinedMovement)은 애초에
		// 탐색 범위가 자기 방으로 좁아 BFS 비용이 낮으므로 아래 기존 방식을 그대로 둔다.
		if (unit is Human explorer)
		{
			return explorer.personalMap.TryGetNearestFrontierTile(fi, unit.position, out Vector2Int frontierTarget)
				? frontierTarget
				: (Vector2Int?)null;
		}

		if (data.bfsVisitedGrid == null || data.bfsVisitedGrid.GetLength(0) < mapW || data.bfsVisitedGrid.GetLength(1) < mapH)
		{
			data.bfsVisitedGrid = new int[mapW + 20, mapH + 20];
		}

		data.bfsVisitToken++;
		if (data.bfsVisitToken == 0) data.bfsVisitToken = 1;

		// 방 제한 유닛은 자기 방 안에서만 탐색한다 — 방 밖 타일을 BFS 목표로 잡으면 A* 실패 →
		// MoveRandomlyValid 반복 호출 사이클이 발생한다.
		bool roomConfined = unit.MovementAlgorithm is RoomConfinedMovement;
		Room myRoom = null;
		if (roomConfined && unit.Session?.roomGrid != null)
			unit.Session.roomGrid.TryGetValue(new Vector3Int(unit.position.x, unit.position.y, fi), out myRoom);

		var q = data.bfsQueue;
		q.Clear();
		q.Enqueue(unit.position);
		data.bfsVisitedGrid[unit.position.x, unit.position.y] = data.bfsVisitToken;

		int maxSearchNodes = 30000; // 맵 횡단을 위해 탐색 범위를 크게 확장
		int iter = 0;

		Human h = unit as Human;

		while (q.Count > 0 && iter < maxSearchNodes)
		{
			iter++;
			Vector2Int cur = q.Dequeue();

			// FactionData(공유 지도)가 아닌 각 유닛의 개인 지도를 기준으로 안 가본 곳을 판별합니다.
			// 공유 지도를 쓰면 남이 밝힌 곳을 자기도 가본 줄 알고 구석에서 영원히 방황하게 됩니다.
			int currentTerrain = h != null ? h.personalMap.GetTileTerrain(new Vector3Int(cur.x, cur.y, fi)) : data.discoveredMap[fi][cur.x, cur.y];
			if (currentTerrain == 0)
			{
				return cur; // 어둠(미탐색) 발견 시 최종 목적지(Target) 좌표 반환
			}

			for (int i = 0; i < 8; i++)
			{
				Dir d = (Dir)i;
				Vector2Int next = cur + unit.GetDirVector(d);

				if (next.x < 0 || next.x >= mapW || next.y < 0 || next.y >= mapH) continue;
				if (data.bfsVisitedGrid[next.x, next.y] == data.bfsVisitToken) continue;

				// 방 제한 유닛: 방 밖 타일은 BFS에서 완전히 제외한다.
				if (roomConfined && myRoom != null && unit.Session?.roomGrid != null)
				{
					if (!unit.Session.roomGrid.TryGetValue(new Vector3Int(next.x, next.y, fi), out Room nextRoom) || nextRoom != myRoom)
					{
						data.bfsVisitedGrid[next.x, next.y] = data.bfsVisitToken; // 재방문 방지
						continue;
					}
				}

				int nextTerrain = h != null ? h.personalMap.GetTileTerrain(new Vector3Int(next.x, next.y, fi)) : data.discoveredMap[fi][next.x, next.y];
				if (nextTerrain == 2) continue; // 벽 패스

				data.bfsVisitedGrid[next.x, next.y] = data.bfsVisitToken;
				q.Enqueue(next);
			}
		}

		return null;
	}

	// ── 라벨 ──────────────────────────────────────────────────────

	private static string GetSubLabel(Unit unit)
	{
		if (HasPendingStairs(unit)) return "탐색(계단)";
		return "탐색(탐험)";
	}
}
