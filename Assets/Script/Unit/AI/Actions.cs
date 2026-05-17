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
	private readonly List<SkillAction> knightSkills = new List<SkillAction>
	{
		new SkillAction_KnightFocusedStab(),
		new SkillAction_KnightShieldBash(),
		new SkillAction_KnightFrontSlash()
	};

	private readonly List<SkillAction> meleeTankSkills = new List<SkillAction>
	{
		new SkillAction_MeleeTankHeavySmash(),
		new SkillAction_MeleeTankAmbushClaw(),
		new SkillAction_MeleeTankClawSwipe()
	};

	public Action_EngageEnemy()
	{
		ActionName = "EngageEnemy";
		AddPrecondition("enemyVisible", true);
		AddEffect("enemyAlive", false);
	}

	public override bool IsValid(Unit unit) => true;

	public override void Execute(Unit unit) => ExecuteSkillActionBased(unit);

	private IEnumerable<SkillAction> GetSkillActions(Unit unit)
	{
		if (unit.unitType is Knight)    return knightSkills;
		if (unit.unitType is MeleeTank) return meleeTankSkills;
		return new List<SkillAction>();
	}

	private void ExecuteSkillActionBased(Unit unit)
	{
		Unit target = GetClosestEnemy(unit, out float minDist);
		if (target == null) return;

		float      engageDist = unit.unitType is MeleeTank ? 2.5f : 1.5f;
		Vector2Int diff       = target.position - unit.position;
		unit.currentDir = SkillAction.GetDirection8(diff);

		// 공격 각도를 자동으로 계산하여 설정 (대상을 향한 정확한 각도)
		float attackRange = unit.unitType is MeleeTank ? 3 : 2;
		unit.currentAttackAngle = ((UnitFunction)unit).CalculateAttackAngleToEnemy(target, (int)attackRange);

		// 히트박스 공격 범위 검사 (타일 기반이 아님)
		int   range          = unit.unitType is MeleeTank ? 3 : 2;
		Hitbox attackCheckBox = SkillAction.BuildLineHitbox(unit, range);
		bool  canHit         = SkillAction.GetEnemiesInHitbox(unit, attackCheckBox).Contains(target);

		if (minDist <= engageDist && canHit)
		{
			SkillAction bestSkill    = null;
			float       bestPriority = float.MinValue;

			foreach (SkillAction skill in GetSkillActions(unit))
			{
				if (skill == null || !skill.IsAvailable(unit)) continue;
				float priority = skill.GetPriority(unit, target, minDist);
				if (priority > bestPriority) { bestPriority = priority; bestSkill = skill; }
			}

			if (bestSkill != null)
			{
				bestSkill.Execute(unit, target, minDist);
				return;
			}
		}

		if (unit.evadeCooldown > 0f) return;

		MoveTowardsTarget(unit, target);

		Vector2Int diff2 = target.position - unit.position;
		if (Mathf.Abs(diff2.x) > Mathf.Abs(diff2.y))
			unit.currentDir = diff2.x > 0 ? Dir.RIGHT : Dir.LEFT;
		else
			unit.currentDir = diff2.y > 0 ? Dir.UP : Dir.DOWN;
	}
}
