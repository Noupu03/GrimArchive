using System.Collections.Generic;

public class Goal_Panic : GoapGoal
{
	public Goal_Panic() { Name = "Panic"; DesiredState["panicResolved"] = true; }

	public override float GetPriority(Unit unit)
	{
		if (unit is Human && unit.GetComponent<BaseStatComponent>().mental < unit.GetComponent<BaseStatComponent>().maxMental * 0.3f) return 150f;
		return 0f;
	}
}

public class Goal_PlayerCommand : GoapGoal
{
	public Goal_PlayerCommand() { Name = "PlayerCommand"; DesiredState["playerCommandExecuted"] = true; }

	public override float GetPriority(Unit unit)
	{
		if (unit.playerAttackTarget != null) return 130f; // 수동 공격 명령은 최우선 (패닉 제외)
		if (unit.playerMoveTarget.HasValue) 
		{
			if (unit.isManualMoveCommand) return 130f; // 수동 이동 명령도 최우선
			return 90f;   // 자동 이동 명령(웨이브)은 자동 전투(100f)보다 낮아 이동 중 적 발견 시 전투에 돌입함
		}
		return 0f;
	}
}

public class Goal_DefeatEnemy : GoapGoal
{
	public Goal_DefeatEnemy() { Name = "DefeatEnemy"; DesiredState["enemyAlive"] = false; }

	public override float GetPriority(Unit unit)
	{
		// 인간 진영도 몬스터와 동일하게 개인 시야(PerceptionState.personalSpottedEnemies)만 사용 — 진영 공유 시야 제거.
		IEnumerable<Unit> enemies = unit.GetComponent<PerceptionComponent>().State.personalSpottedEnemies;

		foreach (var enemy in enemies)
		{
			if (enemy != null && enemy.GetComponent<HealthComponent>().hp > 0 && enemy.currentFloor == unit.currentFloor)
				return 100f; // 이동 명령(90f)보다 우선순위가 높아 이동 중 전투 발생
		}
		return 0f;
	}
}

public class Goal_Explore : GoapGoal
{
	public Goal_Explore() { Name = "Explore"; DesiredState["explored"] = true; }

	public override float GetPriority(Unit unit) => 10f;
}
