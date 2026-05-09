using System.Collections.Generic;
using UnityEngine;

// ==========================================
// GOAP Architecture & Behaviors
// ==========================================
public class GoapState : Dictionary<string, bool> { }

public abstract class GoapGoal
{
	public string Name;
	public GoapState DesiredState = new GoapState();
	public abstract float GetPriority(Unit unit);
}

public abstract class GoapAction
{
	public string ActionName;
	public float Cost = 1f;
	public GoapState Preconditions = new GoapState();
	public GoapState Effects = new GoapState();

	public void AddPrecondition(string key, bool value) => Preconditions[key] = value;
	public void AddEffect(string key, bool value) => Effects[key] = value;

	public abstract bool IsValid(Unit unit);
	public abstract void Execute(Unit unit);

	protected Unit GetClosestEnemy(Unit unit, out float minDist)
	{
		IEnumerable<Unit> enemies = unit is Human ? Unit.humanFactionData.spottedEnemyUnits : unit.personalSpottedEnemies;
		Unit target = null;
		minDist = float.MaxValue;

		foreach (var enemy in enemies)
		{
			if (enemy == null || enemy.hp <= 0 || enemy.currentFloor != unit.currentFloor) continue;
			float d = Vector2Int.Distance(unit.position, enemy.position);
			if (d < minDist)
			{
				minDist = d;
				target = enemy;
			}
		}
		return target;
	}

	protected class AStarNode
	{
		public Vector2Int Pos;
		public AStarNode Parent;
		public int GCost;
		public int HCost;
		public int FCost => GCost + HCost;
	}

	protected void MoveTowardsPos(Unit unit, Vector2Int targetPos)
	{
		if (unit.position == targetPos) return;

		FactionData myData = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
		int floorIdx = unit.currentFloor;

		if (myData.discoveredMap == null || floorIdx >= myData.discoveredMap.Length || myData.discoveredMap[floorIdx] == null)
		{
			FallbackMove(unit, targetPos);
			return;
		}

		int mapW = myData.discoveredMap[floorIdx].GetLength(0);
		int mapH = myData.discoveredMap[floorIdx].GetLength(1);

		Vector2Int startPos = unit.position;

		List<AStarNode> openList = new List<AStarNode>();
		HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
		Dictionary<Vector2Int, AStarNode> allNodes = new Dictionary<Vector2Int, AStarNode>();

		AStarNode startNode = new AStarNode { Pos = startPos, GCost = 0, HCost = GetHeuristic(startPos, targetPos) };
		openList.Add(startNode);
		allNodes[startPos] = startNode;

		int maxIter = 5000;
		int iter = 0;
		AStarNode closestNode = startNode;

		while (openList.Count > 0 && iter < maxIter)
		{
			iter++;
			AStarNode current = openList[0];
			int currentIndex = 0;
			for (int i = 1; i < openList.Count; i++)
			{
				if (openList[i].FCost < current.FCost || (openList[i].FCost == current.FCost && openList[i].HCost < current.HCost))
				{
					current = openList[i];
					currentIndex = i;
				}
			}

			openList.RemoveAt(currentIndex);
			closedSet.Add(current.Pos);

			if (current.Pos == targetPos)
			{
				closestNode = current;
				break;
			}

			if (current.HCost < closestNode.HCost)
				closestNode = current;

			foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
			{
				Vector2Int dirVec = unit.GetDirVector(d);
				if (dirVec == Vector2Int.zero) continue;
				Vector2Int neighborPos = current.Pos + dirVec;

				if (closedSet.Contains(neighborPos)) continue;

				bool isWall = false;
				bool isOccupied = false;
				int w = (int)unit.unitType.footprint.x;
				int h = (int)unit.unitType.footprint.y;

				for (int dx = 0; dx < w; dx++)
				{
					for (int dy = 0; dy < h; dy++)
					{
						int nx = neighborPos.x + dx;
						int ny = neighborPos.y + dy;
						if (nx < 0 || nx >= mapW || ny < 0 || ny >= mapH) { isWall = true; break; }
						if (myData.discoveredMap[floorIdx][nx, ny] == 2) { isWall = true; break; }

						if (neighborPos != targetPos)
						{
							if (GameSession.Instance != null && GameSession.Instance.unitGrid.TryGetValue(new Vector3Int(nx, ny, floorIdx), out Unit u))
							{
								if (u != null && u != unit && u.hp > 0)
									isOccupied = true;
							}
						}
					}
					if (isWall) break;
				}

				if (isWall) continue; // 벽이거나 맵 밖

				if (dirVec.x != 0 && dirVec.y != 0)
				{
					bool cornerWall1 = false;
					bool cornerWall2 = false;

					for (int dx = 0; dx < w; dx++)
					{
						for (int dy = 0; dy < h; dy++)
						{
							int cx1 = current.Pos.x + dx + dirVec.x;
							int cy1 = current.Pos.y + dy;
							int cx2 = current.Pos.x + dx;
							int cy2 = current.Pos.y + dy + dirVec.y;

							if (cx1 >= 0 && cx1 < mapW && cy1 >= 0 && cy1 < mapH && myData.discoveredMap[floorIdx][cx1, cy1] == 2) cornerWall1 = true;
							if (cx2 >= 0 && cx2 < mapW && cy2 >= 0 && cy2 < mapH && myData.discoveredMap[floorIdx][cx2, cy2] == 2) cornerWall2 = true;
						}
					}
					// 양쪽 코너가 막혀있으면 대각선으로 통과 불가
					if (cornerWall1 && cornerWall2) continue;
				}

				int moveCost = (dirVec.x != 0 && dirVec.y != 0) ? 14 : 10;
				if (isOccupied) moveCost += 30; // 아군이 길을 막고 있을 때 우회

				int newGCost = current.GCost + moveCost;

				if (!allNodes.TryGetValue(neighborPos, out AStarNode neighborNode))
				{
					neighborNode = new AStarNode { Pos = neighborPos, HCost = GetHeuristic(neighborPos, targetPos) };
					allNodes[neighborPos] = neighborNode;
				}

				bool inOpen = openList.Contains(neighborNode);
				if (!inOpen || newGCost < neighborNode.GCost)
				{
					neighborNode.GCost = newGCost;
					neighborNode.Parent = current;
					if (!inOpen) openList.Add(neighborNode);
				}
			}
		}

		if (closestNode == startNode)
		{
			return; // 억지로 벽에 박지 않도록 대기
		}

		AStarNode step = closestNode;
		while (step.Parent != null && step.Parent != startNode)
			step = step.Parent;

		Vector2Int diff = step.Pos - startPos;
		foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
		{
			if (unit.GetDirVector(d) == diff)
			{
				unit.Move(d);
				return;
			}
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
			if (unit.GetDirVector(d) == new Vector2Int(dx, dy))
			{
				unit.Move(d);
				break;
			}
		}
	}

	protected void MoveTowardsTarget(Unit unit, Unit target)
	{
		MoveTowardsPos(unit, target.position);
	}
}

// Goals
public class Goal_Panic : GoapGoal
{
	public Goal_Panic() { Name = "Panic"; DesiredState["panicResolved"] = true; }
	public override float GetPriority(Unit unit)
	{
		// 인간 유닛이고 정신력이 30% 미만이면 최우선 공황 행동
		if (unit is Human && unit.mental < unit.maxMental * 0.3f)
		{
			return 150f; // 매우 높은 우선순위
		}
		return 0f;
	}
}


public class Goal_PlayerCommand : GoapGoal
{
	public Goal_PlayerCommand() { Name = "PlayerCommand"; DesiredState["playerCommandExecuted"] = true; }
	public override float GetPriority(Unit unit) => (unit.playerMoveTarget.HasValue || unit.playerAttackTarget != null) ? 100f : 0f;
}

public class Goal_DefeatEnemy : GoapGoal
{
	public Goal_DefeatEnemy() { Name = "DefeatEnemy"; DesiredState["enemyAlive"] = false; }
	public override float GetPriority(Unit unit)
	{
		IEnumerable<Unit> enemies = unit is Human ? Unit.humanFactionData.spottedEnemyUnits : unit.personalSpottedEnemies;
		foreach (var enemy in enemies)
		{
			if (enemy != null && enemy.hp > 0 && enemy.currentFloor == unit.currentFloor)
			{
				/*=======파티 관련 참조 주석처리========
				if (unit is Human h && PartyController.Instance != null)
				{
					Party p = PartyController.Instance.GetPartyOf(h);
					if (p != null)
					{
						if (p.partyGoal == PartyGoal.Sweep) return 90f; // 소탕 파티: 적 처치 최우선
						if (p.partyGoal == PartyGoal.Exploration) return 30f; // 탐사 파티: 유물이나 생존보다 후순위
					}
				}
				*/
				return 80f;
			}
		}
		return 0f;
	}
}

public class Goal_Explore : GoapGoal
{
	public Goal_Explore() { Name = "Explore"; DesiredState["explored"] = true; }
	public override float GetPriority(Unit unit)
	{
		/*=======파티 관련 참조 주석처리========
		if (unit is Human h && PartyController.Instance != null)
		{
			Party p = PartyController.Instance.GetPartyOf(h);
			if (p != null)
			{
				if (p.partyGoal == PartyGoal.Exploration) return 90f; // 탐사 파티: 평상시 탐사 최우선
				if (p.partyGoal == PartyGoal.Sweep) return 10f; 
			}
		}
		*/
		return 10f; // 기본 목표 우선순위 가중치
	}
}

// Actions
public class Action_Panic : GoapAction
{
	public Action_Panic()
	{
		ActionName = "Panic";
		AddEffect("panicResolved", true); // 임시 달성을 통해 계속 패닉 상태 액션이 실행되도록 함 (실제 수치 회복 전까지 발동)
	}

	public override bool IsValid(Unit unit) => unit is Human && unit.mental < unit.maxMental * 0.3f;

	public override void Execute(Unit unit)
	{
		// 공황 상태의 유닛은 제자리에서 무작위 이동(방황)하며 아무것도 하지 않음.
		// 임시 구현: 매 턴 랜덤 방향으로 도망치거나 아무것도 하지 않음.
		if (Random.value > 0.5f)
		{
			Dir randomDir = (Dir)Random.Range(0, 8);
			unit.Move(randomDir);
			Debug.Log($"{unit.unitType.typeName}가 공황에 빠져 허둥지둥 이동합니다.");
		}
		else
		{
			Debug.Log($"{unit.unitType.typeName}가 공황에 빠져 움직이지 못합니다.");
		}
	}
}




public class Action_PlayerCommandExecute : GoapAction
{
	public Action_PlayerCommandExecute()
	{
		ActionName = "PlayerCommandExecute";
		AddEffect("playerCommandExecuted", true);
	}
	public override bool IsValid(Unit unit) => unit.playerMoveTarget.HasValue || unit.playerAttackTarget != null;
	public override void Execute(Unit unit)
	{
		if (unit.playerAttackTarget != null)
		{

			if (unit.playerAttackTarget.hp <= 0)
			{
				unit.playerAttackTarget = null; // 타겟 사망
				return;
			}
			float dist = Vector2Int.Distance(unit.position, unit.playerAttackTarget.position);
			float engageDist = unit.unitType is MeleeTank ? 2.5f : 1.5f;

			if (dist <= engageDist)
			{
				if (unit.attackCooldown <= 0f)
				{
					unit.attackCooldown = Mathf.Max(0.45f, 1.2f - (unit.physicalAttackSpeed * 0.02f));

					unit.playerAttackTarget.TakePhysicalDamage(
						unit.physicalAttack > 0 ? unit.physicalAttack : unit.magicalAttack,
						unit
					);

					Debug.Log($"*수동* {unit.unitType.typeName}가 {unit.playerAttackTarget.unitType.typeName}을(를) 공격!");
				}
			}
			else
			{
				MoveTowardsTarget(unit, unit.playerAttackTarget);
			}
		}
		else if (unit.playerMoveTarget.HasValue)
		{
			Vector2Int target = unit.playerMoveTarget.Value;
			if (unit.position == target)
			{
				unit.playerMoveTarget = null;
			}
			else
			{
				MoveTowardsPos(unit, target);
			}
		}
	}
}

public class Action_RandomExplore : GoapAction
{
	public Action_RandomExplore()
	{
		ActionName = "RandomExplore";
		AddEffect("explored", true);
	}
	public override bool IsValid(Unit unit) => true;
	public override void Execute(Unit unit)
	{
		Dir randomDir = (Dir)Random.Range(0, 8);
		unit.Move(randomDir);
	}
}

public class Action_EngageEnemy : GoapAction
{
	public Action_EngageEnemy()
	{
		ActionName = "EngageEnemy";
		AddPrecondition("enemyVisible", true);
		AddEffect("enemyAlive", false);
	}
	public override bool IsValid(Unit unit) => true;
	public override void Execute(Unit unit)
	{
		ENGAGE_Default(unit);
	}

	private void ENGAGE_Default(Unit unit)
	{
		Unit target = GetClosestEnemy(unit, out float minDist);
		if (target == null) return;

		float engageDist = unit.unitType is MeleeTank ? 2.5f : 1.5f;

		if (minDist <= engageDist)
		{
			// 공격 가능 상태인지 확인
			if (unit.attackCooldown <= 0f)
			{
				// =========================================================
				// 기사형(Knight) 스킬 로직
				// =========================================================
				if (unit.unitType is Knight)
				{
					// -----------------------------------------------------
					// 집중 찌르기
					// 기본 선딜 : 800ms
					// 최종 선딜 : max(200, 800 * (100 / 공격속도))
					// 계수 : 1.25
					// 쿨타임 : 8초
					// -----------------------------------------------------
					if (unit.skillCooldowns[3] <= 0f)
					{
						float finalDelayMs =Mathf.Max(200f, 800f * (100f / Mathf.Max(1f, unit.attackspeed)));

						unit.attackCooldown = finalDelayMs / 1000f;
						unit.skillCooldowns[3] = 8f;

						List<Vector2Int> tiles = GetLineTiles(unit, 2);

						BeginAttackCast(unit,finalDelayMs,tiles,() =>
							{
								target.TakePhysicalDamage(unit.physicalAttack * 1.25f,unit);

								Debug.Log($"{unit.unitType.typeName} 집중 찌르기 사용");
							}
						);

						return;
					}

					// -----------------------------------------------------
					// 방패 타격
					// 기본 선딜 : 650ms
					// 최종 선딜 : max(200, 650 * (100 / 공격속도))
					// 계수 : 0.9
					// 쿨타임 : 6초
					// -----------------------------------------------------
					if (unit.skillCooldowns[2] <= 0f)
					{
						float finalDelayMs = Mathf.Max(200f, 650f * (100f / Mathf.Max(1f, unit.attackspeed)));

						unit.attackCooldown = finalDelayMs / 1000f;
						unit.skillCooldowns[2] = 6f;

						List<Vector2Int> tiles = GetLineTiles(unit, 1);

						BeginAttackCast(unit,finalDelayMs,tiles,() =>
							{
								target.TakePhysicalDamage(unit.physicalAttack * 0.9f,unit);

								target.ApplyStun(1f);

								Debug.Log($"{unit.unitType.typeName} 방패 타격 적중!");
							}
						);

						return;
					}

					// -----------------------------------------------------
					// 전방 베기 (기본 공격)
					// 기본 선딜 : 450ms
					// 최종 선딜 : max(200, 450 * (100 / 공격속도))
					// 계수 : 1.0
					// 쿨타임 : 1.2초
					// -----------------------------------------------------
					{
						float finalDelayMs =
							Mathf.Max(200f, 450f * (100f / Mathf.Max(1f, unit.attackspeed)));

						unit.attackCooldown =Mathf.Max(1.2f, finalDelayMs / 1000f);

						List<Vector2Int> tiles =GetLineTiles(unit, 1);

						BeginAttackCast(unit,finalDelayMs,tiles,() =>
							{
								target.TakePhysicalDamage(unit.physicalAttack,unit);

								Debug.Log($"{unit.unitType.typeName} 전방 베기");
							}
						);

						return;
					}
				}
				// =========================================================
				// MeleeTank 스킬 로직
				// =========================================================
				if (unit.unitType is MeleeTank)
				{
					// -----------------------------------------------------
					// 육중한 내리찍기
					// skillCooldowns[3]
					// -----------------------------------------------------
					if (unit.skillCooldowns[3] <= 0f)
					{
						float finalDelayMs =Mathf.Max(200f, 1000f * (100f / Mathf.Max(1f, unit.attackspeed)));

						unit.attackCooldown = finalDelayMs / 1000f;
						unit.skillCooldowns[3] = 7f;
						List<Vector2Int> tiles =GetFrontAreaTiles(unit, 2, 2);

						BeginAttackCast(unit,finalDelayMs,tiles,() =>
							{
								target.TakePhysicalDamage(unit.physicalAttack * 1.45f,unit);

								Debug.Log($"{unit.unitType.typeName} 육중한 내리찍기");
							}
						);

						return;
					}

					// -----------------------------------------------------
					// 급습 할퀴기
					// skillCooldowns[2]
					// -----------------------------------------------------
					if (unit.skillCooldowns[2] <= 0f)
					{
						float finalDelayMs =
							Mathf.Max(200f, 240f * (100f / Mathf.Max(1f, unit.attackspeed)));

						unit.attackCooldown = finalDelayMs / 1000f;
						unit.skillCooldowns[2] = 4f;

						List<Vector2Int> tiles =GetLineTiles(unit, 1);

						BeginAttackCast(unit,finalDelayMs,tiles,() =>
							{
								target.TakePhysicalDamage(unit.physicalAttack * 0.65f,unit);

								Debug.Log($"{unit.unitType.typeName} 급습 할퀴기"
								);
							}
						);

						return;
					}

					// -----------------------------------------------------
					// 발톱 후려치기
					// skillCooldowns[1]
					// -----------------------------------------------------
					if (unit.skillCooldowns[1] <= 0f)
					{
						float finalDelayMs =Mathf.Max(200f, 650f * (100f / Mathf.Max(1f, unit.attackspeed)));

						unit.attackCooldown =Mathf.Max(1.4f, finalDelayMs / 1000f);

						unit.skillCooldowns[1] = 1.4f;

						List<Vector2Int> tiles = GetLineTiles(unit, 3);

						BeginAttackCast(unit,finalDelayMs,tiles,() =>
							{
								target.TakePhysicalDamage(unit.physicalAttack,unit);

								Debug.Log($"{unit.unitType.typeName} 발톱 후려치기");
							}
						);

						return;
					}
				}
				// =========================================================
				// 일반 유닛 기본 공격
				// =========================================================
				float defaultDelayMs = Mathf.Max(200f, 500f * (100f / Mathf.Max(1f, unit.attackspeed)));

				unit.attackCooldown = defaultDelayMs / 1000f;

				target.TakePhysicalDamage(unit.physicalAttack, unit);

				Debug.Log($"{unit.unitType.typeName} 기본 공격 " +$"(선딜 {defaultDelayMs:F0}ms)");
			}
		}
		else
		{
			MoveTowardsTarget(unit, target);
		}
	}
	// ================================
	// 공격 범위 계산
	// ================================

	protected Vector2Int GetForwardTile(Unit unit)
	{
		return unit.position + unit.GetDirVector(unit.currentDir);
	}

	protected List<Vector2Int> GetLineTiles(
		Unit unit,
		int range
	)
	{
		List<Vector2Int> tiles =new List<Vector2Int>();

		Vector2Int dir =unit.GetDirVector(unit.currentDir);

		Vector2Int current =unit.position;

		for (int i = 1; i <= range; i++)
		{
			current += dir;

			tiles.Add(current);
		}

		return tiles;
	}

	protected List<Vector2Int> GetFrontAreaTiles(Unit unit,int width,int depth)
	{
		List<Vector2Int> result =new List<Vector2Int>();

		Vector2Int forward =unit.GetDirVector(unit.currentDir);

		Vector2Int right =new Vector2Int(forward.y, -forward.x);

		for (int d = 1; d <= depth; d++)
		{
			Vector2Int center =unit.position + forward * d;

			for (int w = -width / 2; w <= width / 2; w++)
			{
				result.Add(center + right * w);
			}
		}

		return result;
	}

	protected void BeginAttackCast(
		Unit unit,
		float castMs,
		List<Vector2Int> tiles,
		System.Action attackAction
	)
	{
		unit.isCastingAttack = true;

		unit.castTimer =castMs / 1000f;

		unit.pendingAttack =attackAction;

		unit.threatTiles.Clear();

		foreach (Vector2Int tile in tiles)
		{
			unit.threatTiles.Add(tile);
		}
	}
}

	// ==========================================
	// GOAP Brain (Agent)
	// ==========================================
	public class GoapBrain
{
	protected List<GoapGoal> availableGoals;
	protected List<GoapAction> availableActions;
	protected GoapAction currentPlannedAction;

	public void JudgeState(Unit unit)
	{
		if (availableGoals == null)
			availableGoals = new List<GoapGoal> { new Goal_Panic(), new Goal_PlayerCommand(), new Goal_DefeatEnemy(), new Goal_Explore() };
		if (availableActions == null)
			availableActions = new List<GoapAction> { new Action_Panic(), new Action_PlayerCommandExecute(), new Action_RandomExplore(), new Action_EngageEnemy() };

		// 1. 최고 우선순위 목표 선정
		GoapGoal bestGoal = null;
		float highestPriority = -1f;

		foreach (var goal in availableGoals)
		{
			float priority = goal.GetPriority(unit);
			if (priority > highestPriority)
			{
				highestPriority = priority;
				bestGoal = goal;
			}
		}

		// 2. 현재 월드 상태(WorldState) 수집
		GoapState worldState = new GoapState();
		IEnumerable<Unit> enemies = unit is Human ? Unit.humanFactionData.spottedEnemyUnits : unit.personalSpottedEnemies;
		bool enemyVisible = false;
		foreach (var e in enemies)
		{
			if (e != null && e.hp > 0 && e.currentFloor == unit.currentFloor) { enemyVisible = true; break; }
		}
		worldState["enemyVisible"] = enemyVisible;
		worldState["isHit"] = unit.isHitThisTurn;

		// 3. 플래닝 (가장 단순한 1-step 매칭)
		currentPlannedAction = null;
		float lowestCost = float.MaxValue;

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

				if (fulfillsGoal && action.Cost < lowestCost)
				{
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
						lowestCost = action.Cost;
					}
				}
			}
		}

		if (!enemyVisible) unit.oneTimeReactUsed = false;
	}

	public void ExecuteAction(Unit unit)
	{
		if (currentPlannedAction != null)
		{
			currentPlannedAction.Execute(unit);
		}
		else
		{
			// 계획 실패 시 기본 행동
			Dir randomDir = (Dir)Random.Range(0, 8);
			unit.Move(randomDir);
		}

		unit.isHitThisTurn = false; // 턴 시작/종료시 피격 플래그 리셋
	}
}