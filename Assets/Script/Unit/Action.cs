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
public class Goal_RetrieveArtifact : GoapGoal
{
	public Goal_RetrieveArtifact() { Name = "RetrieveArtifact"; DesiredState["hasArtifact"] = true; }
	public override float GetPriority(Unit unit)
	{
		if (unit.hasArtifact) return 0f;
		FactionData myData = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
		if (myData.spottedArtifacts.Exists(a => !a.isPickedUp && a.floor == unit.currentFloor)) return 50f;
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
public class Action_RetrieveArtifact : GoapAction
{
	public Action_RetrieveArtifact()
	{
		ActionName = "RetrieveArtifact";
		AddEffect("hasArtifact", true);
	}
	public override bool IsValid(Unit unit) => !unit.hasArtifact;
	public override void Execute(Unit unit)
	{
		FactionData myData = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
		ArtifactItem target = myData.spottedArtifacts.Find(a => !a.isPickedUp && a.floor == unit.currentFloor);
		if (target == null) return;

		float dist = Vector2Int.Distance(unit.position, target.position);
		if (dist <= 1.5f)
		{
			unit.interactionTimer += unit.walkSpeed > 0f ? (1f / unit.walkSpeed) : 0f;
			if (unit.interactionTimer >= 3f)
			{
				if (ArtifactManager.Instance != null) ArtifactManager.Instance.PickupArtifact(target, unit);
			}
			else
			{
				Debug.Log($"{unit.unitType.typeName} 유물 상호작용 중... ({unit.interactionTimer:F1}/3.0s)");
			}
		}
		else
		{
			unit.interactionTimer = 0f;
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
			if (unit.hasArtifact) return; // 운반 중 공격 불가

			if (unit.playerAttackTarget.hp <= 0)
			{
				unit.playerAttackTarget = null; // 타겟 사망
				return;
			}
			float dist = Vector2Int.Distance(unit.position, unit.playerAttackTarget.position);
			float engageDist = unit.unitType is MeleeTank ? 2.5f : (unit.unitType is Knight || unit.unitType is MeleeDealer ? 1.5f : 5.5f);

			if (dist <= engageDist)
			{
				if (unit.attackCooldown <= 0f)
				{
					unit.attackCooldown = Mathf.Max(0.45f, 1.2f - (unit.physicalAttackSpeed * 0.02f));
					bool hit = Random.Range(0, 100) <= Mathf.Clamp(unit.accuracy - unit.playerAttackTarget.GetEvasion(), 5f, 95f);
					if (hit)
					{
						unit.playerAttackTarget.TakePhysicalDamage(unit.physicalAttack > 0 ? unit.physicalAttack : unit.magicalAttack, unit);
						Debug.Log($"*수동* {unit.unitType.typeName}가 {unit.playerAttackTarget.unitType.typeName}을(를) 공격!");
					}
					else
					{
						Debug.Log($"*수동* {unit.unitType.typeName}의 공격 빗나감");
					}
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
				Vector2Int diff = target - unit.position;
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
	public override bool IsValid(Unit unit) => !unit.hasArtifact; // 운반 중 공격 불가
	public override void Execute(Unit unit)
	{
		if (unit.unitType is ArcherType) ENGAGE_Archer(unit);
		else if (unit.unitType is Boss) ENGAGE_Boss(unit);
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
			if (unit.attackCooldown <= 0f)
			{
				unit.attackCooldown = Mathf.Max(0.45f, 1.0f - (unit.physicalAttackSpeed * 0.02f)); // 궁수형 공속 공식 임시
				bool hit = Random.Range(0, 100) <= Mathf.Clamp(unit.accuracy - target.GetEvasion(), 5f, 95f);
				if (hit)
				{
					if (unit.skillCooldown <= 0f)
					{
						unit.skillCooldown = 15f; // 상태이상 화살 쿨다운
						target.TakePhysicalDamage(unit.physicalAttack, unit); // 같은 데미지 + 독 혹은 화상
						if (Random.value > 0.5f) target.ApplyPoison(5f); else target.ApplyBurn(3f);
						Debug.Log($"{unit.unitType.typeName}가 상태이상 스킬 화살 적중! -> {target.unitType.typeName}");
					}
					else
					{
						target.TakePhysicalDamage(unit.physicalAttack, unit);
						Debug.Log($"{unit.unitType.typeName}가 {target.unitType.typeName}을 공격함");
					}
				}
				else
				{
					Debug.Log($"{unit.unitType.typeName}의 공격 빗나감 (회피됨)");
				}
			}
		}
		else
		{
			MoveTowardsTarget(unit, target);
		}
	}

	private void ENGAGE_Boss(Unit unit)
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
			if (unit.attackCooldown <= 0f)
			{
				unit.attackCooldown = 1.4f;
				bool hit = Random.Range(0, 100) <= Mathf.Clamp(unit.accuracy - target.GetEvasion(), 5f, 95f);
				if (hit)
				{
					target.TakePhysicalDamage(unit.physicalAttack, unit);
					target.TakeMentalDamage(10f, unit); // 보스 정신 공격(임시)
					Debug.Log($"{unit.unitType.typeName}가 {target.unitType.typeName}을 물리 및 정신 공격함");
				}
			}
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

		float engageDist = unit.unitType is MeleeTank ? 2.5f : 1.5f;

		if (minDist <= engageDist)
		{
			if (unit.attackCooldown <= 0f)
			{
				unit.attackCooldown = Mathf.Max(0.45f, 1.2f - (unit.physicalAttackSpeed * 0.02f)); // 기본 공속(기사형 기준)
				bool hit = Random.Range(0, 100) <= Mathf.Clamp(unit.accuracy - target.GetEvasion(), 5f, 95f);
				if (hit)
				{
					if (unit.unitType is Knight && unit.skillCooldown <= 0f)
					{
						unit.skillCooldown = 15f; // 방패강타
						target.TakePhysicalDamage(unit.physicalAttack * 0.8f, unit);
						target.ApplyStun(1f);
						Debug.Log($"{unit.unitType.typeName}가 {target.unitType.typeName}에게 방패 강타 적중! (기절)");
					}
					else if (unit.unitType is Priest)
					{
						target.TakeMagicalDamage(unit.magicalAttack, unit);
						Debug.Log($"{unit.unitType.typeName}가 {target.unitType.typeName}을 마법 공격함");
					}
					else if (unit.unitType is RangedSlow)
					{
						target.TakePhysicalDamage(unit.physicalAttack, unit);
						target.ApplySlow(3f);
						Debug.Log($"{unit.unitType.typeName}의 추가 둔화 공격 적중!");
					}
					else if (unit.unitType is RangedMental)
					{
						target.TakeMentalDamage(12f, unit);
						Debug.Log($"{unit.unitType.typeName}가 {target.unitType.typeName}의 정신력을 강타!");
					}
					else
					{
						target.TakePhysicalDamage(unit.physicalAttack, unit);
						Debug.Log($"{unit.unitType.typeName}가 {target.unitType.typeName}을 공격함");
					}
				}
				else
				{
					Debug.Log($"{unit.unitType.typeName}의 공격 빗나감");
				}
			}
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
			availableGoals = new List<GoapGoal> { new Goal_PlayerCommand(), new Goal_RetrieveArtifact(), new Goal_DefeatEnemy(), new Goal_Explore() };
		if (availableActions == null)
			availableActions = new List<GoapAction> { new Action_PlayerCommandExecute(), new Action_RetrieveArtifact(), new Action_RandomExplore(), new Action_EngageEnemy() };

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
		worldState["hasArtifact"] = unit.hasArtifact;

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