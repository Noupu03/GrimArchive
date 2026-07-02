using System.Collections.Generic;
using UnityEngine;

public class Action_Panic : GoapAction
{
	public Action_Panic() { ActionName = "Panic"; AddEffect("panicResolved", true); }

	public override bool IsValid(Unit unit) => unit is Human && unit.mental < unit.maxMental * 0.3f;

	public override void Execute(Unit unit)
	{
		if (Random.value > 0.5f)
		{
			Dir randomDir = (Dir)Random.Range(0, 8);
			unit.Move(randomDir);
			Debug.Log($"{unit.unitType.typeName}가 공황에 빠져 무작위로 이동합니다.");
		}
		else
		{
			Debug.Log($"{unit.unitType.typeName}가 공황에 빠져 멈춰있습니다.");
		}
	}
}

public class Action_PlayerCommandExecute : GoapAction
{
	public Action_PlayerCommandExecute() { ActionName = "PlayerCommandExecute"; AddEffect("playerCommandExecuted", true); }

	public override bool IsValid(Unit unit) => unit.playerMoveTarget.HasValue;

	public override void Execute(Unit unit)
	{
		if (!unit.playerMoveTarget.HasValue) return;

		Vector2Int target = unit.playerMoveTarget.Value;
		if (unit.position == target)
			unit.playerMoveTarget = null;
		else
			MoveTowardsPos(unit, target);
	}
}

public class Action_RandomExplore : GoapAction
{
	public Action_RandomExplore() { ActionName = "RandomExplore"; AddEffect("explored", true); }

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

	public override void Execute(Unit unit) => ExecuteSkillActionBased(unit);

	private void ExecuteSkillActionBased(Unit unit)
	{
		Unit target = GetClosestEnemy(unit, out float minDist);
		if (target == null) return;

		int engageSteps = unit.Generate != null ? unit.Generate.GetEngageDistance(unit.unitType.typeName, 2) : 2;

		Vector2Int diff     = target.position - unit.position;
		int        chebDist = Mathf.Max(Mathf.Abs(diff.x), Mathf.Abs(diff.y));

		unit.currentDir         = SkillAction.GetDirection8(diff);
		unit.currentAttackAngle = ((UnitFunction)unit).CalculateAttackAngleToEnemy(target, 1);

		// 사용 가능한 최우선 스킬 탐색
		SkillAction bestSkill    = null;
		float       bestPriority = float.MinValue;
		var unitSkills = unit.Generate != null ? unit.Generate.GetSkills(unit.unitType.typeName) : new System.Collections.Generic.List<SkillAction>();
		foreach (SkillAction skill in unitSkills)
		{
			if (skill == null || !skill.IsAvailable(unit)) continue;
			float priority = skill.GetPriority(unit, target, minDist);
			if (priority > bestPriority) { bestPriority = priority; bestSkill = skill; }
		}

		if (bestSkill != null)
		{
			// 선택된 스킬의 실제 사정거리로 canHit 검사
			Hitbox skillBox = bestSkill.BuildSkillHitbox(unit);
			bool   canHit   = SkillAction.GetEnemiesInHitbox(unit, skillBox).Contains(target);

			if (canHit)
			{
				bestSkill.Execute(unit, target, minDist);
				return;
			}

			// 사정거리 밖: 적에게 접근
			if (unit.evadeCooldown > 0f) return;
			MoveTowardsTarget(unit, target);
		}
		else
		{
			// 스킬 쿨다운 중: 체비쇼프 거리 기준으로 대치 거리 유지 (대각선 포함)
			if (unit.evadeCooldown > 0f) return;

			if (chebDist != engageSteps)
				MoveAwayFromTarget(unit, target, engageSteps);
		}

		// 이동 후 방향 업데이트 (항상 적을 바라봄, 대각 포함 8방향)
		unit.currentDir = SkillAction.GetDirection8(target.position - unit.position);
		unit.Generate?.UpdateUnitSpriteForDirection(unit);
	}
}
