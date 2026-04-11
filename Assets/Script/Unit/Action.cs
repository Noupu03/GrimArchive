using UnityEngine;
using System.Collections.Generic;

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
		FactionData myData = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
		Unit target = null;
		minDist = float.MaxValue;

		foreach (var enemy in myData.spottedEnemyUnits)
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

	protected void MoveTowardsTarget(Unit unit, Unit target)
	{
		Vector2Int diff = target.position - unit.position;
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
}

// Goals
public class Goal_DefeatEnemy : GoapGoal
{
	public Goal_DefeatEnemy() { Name = "DefeatEnemy"; DesiredState["enemyAlive"] = false; }
	public override float GetPriority(Unit unit)
	{
		FactionData myData = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
		foreach (var enemy in myData.spottedEnemyUnits)
		{
			if (enemy != null && enemy.hp > 0 && enemy.currentFloor == unit.currentFloor) return 80f;
		}
		return 0f;
	}
}

public class Goal_Explore : GoapGoal
{
	public Goal_Explore() { Name = "Explore"; DesiredState["explored"] = true; }
	public override float GetPriority(Unit unit) => 10f; // 기본 목표 우선순위 가중치 10임 낮음.
}

// Actions
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
		if (unit.unitType is Archer) ENGAGE_Archer(unit);
		else if (unit.unitType is Wolf) ENGAGE_Wolf(unit);
		else ENGAGE_Default(unit);
	}

	private void ENGAGE_Archer(Unit unit)
	{
		if (unit.isHitThisTurn)
		{
			Dir runDir = (Dir)Mathf.Repeat((int)unit.currentDir + 4 + Random.Range(-1, 2), 8);
			unit.Move(runDir);
			return;
		}

		Unit target = GetClosestEnemy(unit, out float minDist);
		if (target == null) return;

		if (minDist <= 5.5f)
		{
			target.TakeDamage(unit.attackPower);
			Debug.Log($"{unit.unitType.typeName}가 {target.unitType.typeName}을 공격해 {unit.attackPower} 피해를 입힘");
		}
		else
		{
			MoveTowardsTarget(unit, target);
		}
	}

	private void ENGAGE_Wolf(Unit unit)
	{
		if (unit.isHitThisTurn && !unit.oneTimeReactUsed)
		{
			unit.oneTimeReactUsed = true;
			unit.Move((Dir)Random.Range(0, 8)); // 임시 전진
			return;
		}

		Unit target = GetClosestEnemy(unit, out float minDist);
		if (target == null) return;

		if (minDist <= 1.5f)
		{
			target.TakeDamage(unit.attackPower);
			Debug.Log($"{unit.unitType.typeName}가 {target.unitType.typeName}을 공격해 {unit.attackPower} 피해를 입힘");
		}
		else
		{
			MoveTowardsTarget(unit, target);
		}
	}

	private void ENGAGE_Default(Unit unit)
	{
		Unit target = GetClosestEnemy(unit, out float minDist);
		if (target == null) return;

		if (minDist <= 1.5f)
		{
			target.TakeDamage(unit.attackPower);
			Debug.Log($"{unit.unitType.typeName}가 {target.unitType.typeName}을 공격해 {unit.attackPower} 피해를 입힘");
		}
		else
		{
			MoveTowardsTarget(unit, target);
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
			availableGoals = new List<GoapGoal> { new Goal_DefeatEnemy(), new Goal_Explore() };
		if (availableActions == null)
			availableActions = new List<GoapAction> { new Action_RandomExplore(), new Action_EngageEnemy() };

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
		FactionData myData = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
		bool enemyVisible = myData.spottedEnemyUnits.Exists(e => e != null && e.hp > 0 && e.currentFloor == unit.currentFloor);
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