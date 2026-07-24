using UnityEngine;

// 탐색 상태 — 전투·전술 조건이 없을 때 활성화. 항상 Priority > 0 이므로 기본 상태로 동작.
// BT: 계단이동 → 플레이어 명령 → 자유탐색
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
			// 2. 플레이어 공격 명령 (원본 PlayerCommandFSMState p=130)
			new BTSequence(
				new BTCondition(HasPlayerAttackTarget),
				new BTLeaf(ExecutePlayerAttack)
			),
			// 3. 플레이어 이동 명령 (원본 PlayerCommandFSMState p=99)
			new BTSequence(
				new BTCondition(HasPlayerMoveCommand),
				new BTLeaf(ExecutePlayerMove),
				new BTLeaf(CompletePlayerCommand)
			),
			// 4. 자유탐색 (원본 ExploreFSMState p=10)
			new BTLeaf(RandomExplore)
		);
	}

	// 항상 활성 — Combat/Tactical 조건이 없을 때 실질적인 기본값이 된다.
	public float GetPriority(Unit unit)    => AIConfigLoader.Behavior?.navigationPriority ?? 10f;
	public bool  IsSticky(Unit unit)       => false;
	public bool  ShouldInterrupt(Unit unit)=> true;
	public void  OnEnter(Unit unit)        { }
	public void  OnExit(Unit unit)         { }
	public BTStatus Tick(Unit unit)        => _bt.Tick(unit);
	public string   GetLabel(Unit unit)    => GetSubLabel(unit);

	// ── 조건 ─────────────────────────────────────────────────────

	private static bool HasPendingStairs(Unit unit)
		=> unit is Human h && h.pendingStairTargetFloor.HasValue
		&& h.pendingStairTargetFloor.Value != h.currentFloor;

	private static bool HasPlayerAttackTarget(Unit unit)
		=> unit is Human h && h.playerAttackTarget != null && h.playerAttackTarget.hp > 0;

	private static bool HasPlayerMoveCommand(Unit unit)
		=> unit is Human h && h.playerMoveTarget.HasValue && h.isManualMoveCommand;

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
			ExploreTowardsUnknown(human);
			return BTStatus.Running;
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

		if (!human.Session.cmap.TryGetStairApproachPosition(toFloor, fromFloor, out Vector2Int arrivePos))
			return BTStatus.Running;

		human.Session.UnregisterUnitPos(human, human.position);
		human.currentFloor = toFloor;
		human.position     = arrivePos;
		human.Session.RegisterUnitPos(human, human.position);
		human.pendingStairTargetFloor = null;
		return BTStatus.Success;
	}

	private static void ExploreTowardsUnknown(Human human)
	{
		Dir[] dirs = (Dir[])System.Enum.GetValues(typeof(Dir));
		foreach (Dir d in dirs)
		{
			Vector2Int next = human.position + human.GetDirVector(d);
			if (human.MovementAlgorithm != null && human.MovementAlgorithm.TryGetNextStep(human, next, out Dir step))
			{
				human.Move(step);
				return;
			}
		}
		human.Move((Dir)Random.Range(0, 8));
	}

	// ── 플레이어 명령 ─────────────────────────────────────────────

	private static BTStatus ExecutePlayerAttack(Unit unit)
	{
		if (!(unit is Human human) || human.playerAttackTarget == null) return BTStatus.Failure;
		Unit target = human.playerAttackTarget;
		if (target.hp <= 0 || target.currentFloor != human.currentFloor)
		{
			human.playerAttackTarget = null;
			human.oneTimeReactUsed   = false;
			return BTStatus.Success;
		}

		Vector2Int diff     = target.position - human.position;
		int        chebDist = Mathf.Max(Mathf.Abs(diff.x), Mathf.Abs(diff.y));

		human.currentDir = SkillAction.GetDirection8(diff);
		human.CombatState.State.currentAttackAngle = ((UnitFunction)human).CalculateAttackAngleToEnemy(target, 1);

		var  skills   = human.Generate != null ? human.Generate.GetSkills(human.unitType.typeName) : new System.Collections.Generic.List<SkillAction>();
		SkillAction best = null;
		float bestP = float.MinValue;
		foreach (var s in skills)
		{
			if (s == null || !s.IsAvailable(human)) continue;
			float p = s.GetPriority(human, target, chebDist);
			if (p > bestP) { bestP = p; best = s; }
		}

		if (best != null)
		{
			Hitbox box = best.BuildSkillHitbox(human);
			if (SkillAction.GetEnemiesInHitbox(human, box).Contains(target))
			{
				best.Execute(human, target, chebDist);
				return BTStatus.Running;
			}
		}
		AIMovementHelper.MoveTowardsTarget(human, target);
		return BTStatus.Running;
	}

	private static BTStatus ExecutePlayerMove(Unit unit)
	{
		if (!(unit is Human human) || !human.playerMoveTarget.HasValue) return BTStatus.Failure;
		Vector2Int target = human.playerMoveTarget.Value;
		if (human.position == target) return BTStatus.Success;
		AIMovementHelper.MoveTowardsPos(human, target);
		return BTStatus.Running;
	}

	private static BTStatus CompletePlayerCommand(Unit unit)
	{
		if (!(unit is Human human)) return BTStatus.Failure;
		human.playerMoveTarget      = null;
		human.isManualMoveCommand   = false;
		human.oneTimeReactUsed      = false;
		return BTStatus.Success;
	}

	// ── 자유탐색 ─────────────────────────────────────────────────

	private static BTStatus RandomExplore(Unit unit)
	{
		if (Random.value < 0.3f) { unit.Move((Dir)Random.Range(0, 8)); return BTStatus.Running; }

		FactionData data = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
		int fi = unit.currentFloor;
		if (data.discoveredMap == null || fi >= data.discoveredMap.Length || data.discoveredMap[fi] == null)
		{
			unit.Move((Dir)Random.Range(0, 8));
			return BTStatus.Running;
		}

		int mapW = data.discoveredMap[fi].GetLength(0);
		int mapH = data.discoveredMap[fi].GetLength(1);

		Vector2Int? unexplored = FindNearestUnexplored(unit, data, fi, mapW, mapH);
		if (unexplored.HasValue)
		{
			AIMovementHelper.MoveTowardsPos(unit, unexplored.Value);
		}
		else
		{
			unit.Move((Dir)Random.Range(0, 8));
		}
		return BTStatus.Running;
	}

	private static Vector2Int? FindNearestUnexplored(Unit unit, FactionData data, int fi, int mapW, int mapH)
	{
		int     r      = AIConfigLoader.Behavior?.exploreRadius ?? 10;
		float   best   = float.MaxValue;
		Vector2Int? result = null;

		for (int dx = -r; dx <= r; dx++)
		for (int dy = -r; dy <= r; dy++)
		{
			Vector2Int c = new Vector2Int(unit.position.x + dx, unit.position.y + dy);
			if (c.x < 0 || c.x >= mapW || c.y < 0 || c.y >= mapH) continue;
			if (data.discoveredMap[fi][c.x, c.y] != 0) continue;
			float d = new Vector2Int(dx, dy).sqrMagnitude;
			if (d < best) { best = d; result = c; }
		}
		return result;
	}

	// ── 라벨 ──────────────────────────────────────────────────────

	private static string GetSubLabel(Unit unit)
	{
		if (HasPendingStairs(unit))      return "탐색(계단)";
		if (HasPlayerAttackTarget(unit)) return "탐색(명령-공격)";
		if (HasPlayerMoveCommand(unit))  return "탐색(명령-이동)";
		return "탐색(탐험)";
	}
}
