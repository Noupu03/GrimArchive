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

	// AStarNode has been moved to AStarMovement.cs

	protected void MoveTowardsPos(Unit unit, Vector2Int targetPos)
	{
		if (unit.MovementAlgorithm != null)
		{
			if (unit.MovementAlgorithm.TryGetNextStep(unit, targetPos, out Dir nextDir))
			{
				unit.Move(nextDir);
			}
		}
	}

	// FallbackMove and GetHeuristic are now encapsulated in MovementAlgorithms

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
