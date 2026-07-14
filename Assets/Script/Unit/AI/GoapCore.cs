using System.Collections.Generic;
using UnityEngine;

// ==========================================
// GOAP Architecture
// ==========================================

public class GoapState : Dictionary<string, bool> { }

public abstract class GoapGoal
{
	public string    Name;
	public GoapState DesiredState = new GoapState();
	public abstract float GetPriority(Unit unit);
}

public abstract class GoapAction
{
	public string    ActionName;
	public float     Cost = 1f;
	public GoapState Preconditions = new GoapState();
	public GoapState Effects       = new GoapState();

	public void AddPrecondition(string key, bool value) => Preconditions[key] = value;
	public void AddEffect(string key, bool value)       => Effects[key]       = value;

	public abstract bool IsValid(Unit unit);
	public abstract void Execute(Unit unit);

	protected Unit GetClosestEnemy(Unit unit, out float minDist)
	{
		// 인간 진영도 몬스터와 동일하게 개인 시야(personalSpottedEnemies)만 사용 — 진영 공유 시야 제거.
		IEnumerable<Unit> enemies = unit.personalSpottedEnemies;

		Unit  target  = null;
		minDist = float.MaxValue;

		foreach (var enemy in enemies)
		{
			if (enemy == null || enemy.hp <= 0 || enemy.currentFloor != unit.currentFloor) continue;
			float d = Vector2Int.Distance(unit.position, enemy.position);
			if (d < minDist) { minDist = d; target = enemy; }
		}
		return target;
	}

	protected class AStarNode
	{
		public Vector2Int Pos;
		public AStarNode  Parent;
		public int        GCost;
		public int        HCost;
		public int        FCost => GCost + HCost;
	}

	protected void MoveTowardsPos(Unit unit, Vector2Int targetPos)
	{
		if (unit.position == targetPos) return;

		FactionData myData  = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
		int         floorIdx = unit.currentFloor;

		if (myData.discoveredMap == null || floorIdx >= myData.discoveredMap.Length || myData.discoveredMap[floorIdx] == null)
		{
			FallbackMove(unit, targetPos);
			return;
		}

		int mapW = myData.discoveredMap[floorIdx].GetLength(0);
		int mapH = myData.discoveredMap[floorIdx].GetLength(1);

		Vector2Int startPos = unit.position;

		List<AStarNode>             openList = new List<AStarNode>();
		HashSet<Vector2Int>         closedSet = new HashSet<Vector2Int>();
		Dictionary<Vector2Int, AStarNode> allNodes = new Dictionary<Vector2Int, AStarNode>();

		AStarNode startNode = new AStarNode { Pos = startPos, GCost = 0, HCost = GetHeuristic(startPos, targetPos) };
		openList.Add(startNode);
		allNodes[startPos] = startNode;

		int       maxIter    = 5000;
		int       iter       = 0;
		AStarNode closestNode = startNode;

		while (openList.Count > 0 && iter < maxIter)
		{
			iter++;

			// 최소 FCost 노드 선택
			AStarNode current      = openList[0];
			int       currentIndex = 0;
			for (int i = 1; i < openList.Count; i++)
			{
				if (openList[i].FCost < current.FCost ||
					(openList[i].FCost == current.FCost && openList[i].HCost < current.HCost))
				{
					current = openList[i];
					currentIndex = i;
				}
			}

			openList.RemoveAt(currentIndex);
			closedSet.Add(current.Pos);

			if (current.Pos == targetPos) { closestNode = current; break; }
			if (current.HCost < closestNode.HCost) closestNode = current;

			foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
			{
				Vector2Int dirVec      = unit.GetDirVector(d);
				if (dirVec == Vector2Int.zero) continue;
				Vector2Int neighborPos = current.Pos + dirVec;

				if (closedSet.Contains(neighborPos)) continue;

				bool isWall     = false;
				bool isOccupied = false;
				int  fw         = (int)unit.unitType.footprint.x;
				int  fh         = (int)unit.unitType.footprint.y;

				for (int dx = 0; dx < fw && !isWall; dx++)
				{
					for (int dy = 0; dy < fh && !isWall; dy++)
					{
						int nx = neighborPos.x + dx;
						int ny = neighborPos.y + dy;

						if (nx < 0 || nx >= mapW || ny < 0 || ny >= mapH) { isWall = true; break; }
						if (myData.discoveredMap[floorIdx][nx, ny] == 2)   { isWall = true; break; }



						if (neighborPos != targetPos &&
							unit.Session != null &&
							unit.Session.unitGrid.TryGetValue(new Vector3Int(nx, ny, floorIdx), out Unit u))
						{
							if (u != null && u != unit && u.hp > 0) isOccupied = true;
						}
					}
				}

				// 코너 커팅(벽 뚫기) 방지: 대각선 이동 시 양옆 직교 타일 중 하나라도 벽이면 블록
				if (!isWall && Mathf.Abs(dirVec.x) == 1 && Mathf.Abs(dirVec.y) == 1)
				{
					int ortho1X = current.Pos.x + dirVec.x, ortho1Y = current.Pos.y;
					int ortho2X = current.Pos.x, ortho2Y = current.Pos.y + dirVec.y;
					
					bool ortho1Wall = (ortho1X < 0 || ortho1X >= mapW || ortho1Y < 0 || ortho1Y >= mapH || myData.discoveredMap[floorIdx][ortho1X, ortho1Y] == 2);
					bool ortho2Wall = (ortho2X < 0 || ortho2X >= mapW || ortho2Y < 0 || ortho2Y >= mapH || myData.discoveredMap[floorIdx][ortho2X, ortho2Y] == 2);
					
					if (ortho1Wall || ortho2Wall)
					{
						isWall = true;
					}
				}

				if (isWall) continue;

				// 대각선 코너 차단
				if (dirVec.x != 0 && dirVec.y != 0)
				{
					bool cornerWall1 = false, cornerWall2 = false;
					for (int dx = 0; dx < fw && !(cornerWall1 && cornerWall2); dx++)
					{
						for (int dy = 0; dy < fh && !(cornerWall1 && cornerWall2); dy++)
						{
							int cx1 = current.Pos.x + dx + dirVec.x;
							int cy1 = current.Pos.y + dy;
							int cx2 = current.Pos.x + dx;
							int cy2 = current.Pos.y + dy + dirVec.y;

							if (cx1 >= 0 && cx1 < mapW && cy1 >= 0 && cy1 < mapH && myData.discoveredMap[floorIdx][cx1, cy1] == 2) cornerWall1 = true;
							if (cx2 >= 0 && cx2 < mapW && cy2 >= 0 && cy2 < mapH && myData.discoveredMap[floorIdx][cx2, cy2] == 2) cornerWall2 = true;
						}
					}
					if (cornerWall1 && cornerWall2) continue;
				}

				int moveCost = (dirVec.x != 0 && dirVec.y != 0) ? 14 : 10;
				if (isOccupied) moveCost += 30;

				int newGCost = current.GCost + moveCost;

				if (!allNodes.TryGetValue(neighborPos, out AStarNode neighborNode))
				{
					neighborNode = new AStarNode { Pos = neighborPos, HCost = GetHeuristic(neighborPos, targetPos) };
					allNodes[neighborPos] = neighborNode;
				}

				bool inOpen = openList.Contains(neighborNode);
				if (!inOpen || newGCost < neighborNode.GCost)
				{
					neighborNode.GCost  = newGCost;
					neighborNode.Parent = current;
					if (!inOpen) openList.Add(neighborNode);
				}
			}
		}

		if (closestNode == startNode) return;

		AStarNode step = closestNode;
		while (step.Parent != null && step.Parent != startNode)
			step = step.Parent;

		Vector2Int diff = step.Pos - startPos;
		foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
		{
			if (unit.GetDirVector(d) == diff) { unit.Move(d); return; }
		}
	}

	private int GetHeuristic(Vector2Int a, Vector2Int b)
	{
		int dx = Mathf.Abs(a.x - b.x);
		int dy = Mathf.Abs(a.y - b.y);
		return 10 * (dx + dy) - 6 * Mathf.Min(dx, dy);
	}

	protected void FallbackMove(Unit unit, Vector2Int targetPos)
	{
		Vector2Int diff = targetPos - unit.position;
		int dx = diff.x == 0 ? 0 : (diff.x > 0 ? 1 : -1);
		int dy = diff.y == 0 ? 0 : (diff.y > 0 ? 1 : -1);

		foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
		{
			if (unit.GetDirVector(d) == new Vector2Int(dx, dy)) { unit.Move(d); break; }
		}
	}

	protected void MoveTowardsTarget(Unit unit, Unit target) => MoveTowardsPos(unit, target.position);

	protected void MoveAwayFromTarget(Unit unit, Unit target, float desiredDist)
	{
		Vector2 away = (Vector2)(unit.position - target.position);
		if (away == Vector2.zero) away = Vector2.right;

		// 체비쇼프 스케일: max(|x|,|y|)=1로 정규화 → 대각선 방향도 정확한 타일 거리로 이동
		float maxComp = Mathf.Max(Mathf.Abs(away.x), Mathf.Abs(away.y));
		Vector2 scaled = away / maxComp;

		int targetDist = Mathf.RoundToInt(desiredDist);
		Vector2Int retreatPos = target.position + new Vector2Int(
			Mathf.RoundToInt(scaled.x * targetDist),
			Mathf.RoundToInt(scaled.y * targetDist)
		);
		MoveTowardsPos(unit, retreatPos);
	}
}

// ==========================================
// GOAP Brain (Agent)
// ==========================================
public class GoapBrain
{
	protected List<GoapGoal>   availableGoals;
	protected List<GoapAction> availableActions;
	protected GoapAction       currentPlannedAction;

	public void JudgeState(Unit unit)
	{
		if (availableGoals == null)
			availableGoals = new List<GoapGoal>
			{
				new Goal_Panic(), new Goal_PlayerCommand(), new Goal_DefeatEnemy(), new Goal_Explore()
			};

		if (availableActions == null)
			availableActions = new List<GoapAction>
			{
				new Action_Panic(), new Action_PlayerCommandExecute(), new Action_RandomExplore(), new Action_EngageEnemy()
			};

		// 최우선 목표 선택
		GoapGoal bestGoal        = null;
		float    highestPriority = -1f;

		foreach (var goal in availableGoals)
		{
			float priority = goal.GetPriority(unit);
			if (priority > highestPriority) { highestPriority = priority; bestGoal = goal; }
		}

		// 월드 스테이트 구성
		GoapState         worldState  = new GoapState();
		IEnumerable<Unit> enemies     = unit.personalSpottedEnemies;
		bool              enemyVisible = false;

		foreach (var e in enemies)
		{
			if (e != null && e.hp > 0 && e.currentFloor == unit.currentFloor) { enemyVisible = true; break; }
		}

		worldState["enemyVisible"] = enemyVisible;
		worldState["isHit"]        = unit.isHitThisTurn;

		// 목표를 충족하는 최저 비용 액션 선택
		currentPlannedAction = null;
		float lowestCost     = float.MaxValue;

		if (bestGoal != null)
		{
			foreach (var action in availableActions)
			{
				if (!action.IsValid(unit)) continue;

				bool fulfillsGoal = false;
				foreach (var eff in action.Effects)
				{
					if (bestGoal.DesiredState.ContainsKey(eff.Key) && bestGoal.DesiredState[eff.Key] == eff.Value)
					{
						fulfillsGoal = true;
						break;
					}
				}
				if (!fulfillsGoal) continue;
				if (action.Cost >= lowestCost) continue;

				bool meetsPreconditions = true;
				foreach (var pre in action.Preconditions)
				{
					if (!worldState.ContainsKey(pre.Key) || worldState[pre.Key] != pre.Value)
					{
						meetsPreconditions = false;
						break;
					}
				}

				if (meetsPreconditions)
				{
					currentPlannedAction = action;
					lowestCost           = action.Cost;
				}
			}
		}

		if (!enemyVisible) unit.oneTimeReactUsed = false;
	}

	public void ExecuteAction(Unit unit)
	{
		// 캐스팅 중일 때는 UnitFunction.OnUpdate가 타이머/펜딩공격을 처리하므로 여기서는 그냥 대기
		if (unit.isCastingAttack) return;

		if (currentPlannedAction != null)
			currentPlannedAction.Execute(unit);
		else
		{
			Dir randomDir = (Dir)Random.Range(0, 8);
			unit.Move(randomDir);
		}

		unit.isHitThisTurn = false;
	}
}
