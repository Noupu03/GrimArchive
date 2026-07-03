using System.Collections.Generic;

public class Goal_Panic : GoapGoal
{
	public Goal_Panic() { Name = "Panic"; DesiredState["panicResolved"] = true; }

	public override float GetPriority(Unit unit)
	{
		if (unit is Human && unit.mental < unit.maxMental * 0.3f) return 150f;
		return 0f;
	}
}

public class Goal_PlayerCommand : GoapGoal
{
	public Goal_PlayerCommand() { Name = "PlayerCommand"; DesiredState["playerCommandExecuted"] = true; }

	public override float GetPriority(Unit unit) =>
		(unit.playerMoveTarget.HasValue || unit.playerAttackTarget != null) ? 100f : 0f;
}

public class Goal_DefeatEnemy : GoapGoal
{
	public Goal_DefeatEnemy() { Name = "DefeatEnemy"; DesiredState["enemyAlive"] = false; }

	public override float GetPriority(Unit unit)
	{
		// 인간 진영도 몬스터와 동일하게 개인 시야(personalSpottedEnemies)만 사용 — 진영 공유 시야 제거.
		IEnumerable<Unit> enemies = unit.personalSpottedEnemies;

		foreach (var enemy in enemies)
		{
			if (enemy != null && enemy.hp > 0 && enemy.currentFloor == unit.currentFloor)
				return 80f;
		}
		return 0f;
	}
}

public class Goal_Explore : GoapGoal
{
	public Goal_Explore() { Name = "Explore"; DesiredState["explored"] = true; }

	public override float GetPriority(Unit unit) => 10f;
}
