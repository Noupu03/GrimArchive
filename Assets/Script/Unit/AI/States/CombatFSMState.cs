using System.Collections.Generic;
using UnityEngine;

// 전투 상태 — 인지된 적이 같은 층에 있을 때 활성화.
// BT: 키팅(원거리 위험거리 이하) → 공격(사거리·스킬 준비) → 접근
public class CombatFSMState : IFSMState
{
	private readonly BTNode _bt = new BTLeaf(ExecuteCombat);

	public float GetPriority(Unit unit)
	{
		// 플레이어 수동 명령(공격/이동)은 PlayerCommandFSMState(UnitFSM._states 배열 맨 앞, 최우선)가
		// 전담한다 — 명령이 활성 상태면 이 GetPriority는 아예 호출되지도 않으므로 여기서 따로 예외
		// 처리할 필요가 없다.
		foreach (var e in unit.Perception.State.personalSpottedEnemies)
			if (e != null && e.hp > 0 && e.currentFloor == unit.currentFloor)
				return AIConfigLoader.Behavior?.combatPriority ?? 100f;
		return 0f;
	}

	public bool IsSticky(Unit unit)        => false;
	public bool ShouldInterrupt(Unit unit) => true;
	public void OnEnter(Unit unit)         { }

	// 전투 종료 시 전투 후 경계 세팅 (03문서 4-8장)
	public void OnExit(Unit unit)
	{
		if (unit.currentAlertSearch == null)
			unit.currentAlertSearch = new AlertSearchState { IsPostCombatSweep = true };
	}

	public BTStatus Tick(Unit unit)   => _bt.Tick(unit);
	public string   GetLabel(Unit unit) => "전투";

	private static BTStatus ExecuteCombat(Unit unit)
	{
		if (unit.CombatState.State.isCastingAttack) return BTStatus.Running;

		Unit target = GetClosestEnemy(unit, out float minDist);
		if (target == null) return BTStatus.Failure;

		int rangedMin = AIConfigLoader.Behavior?.rangedSkillMinRange ?? 4;

		var unitSkills = unit.Generate != null
			? unit.Generate.GetSkills(unit.unitType.typeName)
			: new List<SkillAction>();

		int maxSkillRange = 0;
		foreach (var s in unitSkills) if (s != null && s.HitRange > maxSkillRange) maxSkillRange = s.HitRange;

		Vector2Int diff     = target.position - unit.position;
		int        chebDist = Mathf.Max(Mathf.Abs(diff.x), Mathf.Abs(diff.y));

		unit.currentDir = SkillAction.GetDirection8(diff);
		unit.CombatState.State.currentAttackAngle = ((UnitFunction)unit).CalculateAttackAngleToEnemy(target, 1);

		SkillAction bestSkill    = null;
		float       bestPriority = float.MinValue;
		foreach (var skill in unitSkills)
		{
			if (skill == null || !skill.IsAvailable(unit)) continue;
			float p = skill.GetPriority(unit, target, minDist);
			if (p > bestPriority) { bestPriority = p; bestSkill = skill; }
		}

		if (bestSkill != null)
		{
			// 키팅: 원거리 스킬이고 적이 위험거리 이하
			if (bestSkill.HitRange >= rangedMin)
			{
				int dangerDist = bestSkill.HitRange / 2;
				if (chebDist <= dangerDist && unit.CombatState.State.evadeCooldown <= 0f)
				{
					AIMovementHelper.MoveAwayFromTarget(unit, target, dangerDist + 1);
					unit.currentDir = SkillAction.GetDirection8(target.position - unit.position);
					unit.Generate?.UpdateUnitSpriteForDirection(unit);
					return BTStatus.Running;
				}
			}

			Hitbox skillBox = bestSkill.BuildSkillHitbox(unit);
			if (SkillAction.GetEnemiesInHitbox(unit, skillBox).Contains(target))
			{
				bestSkill.Execute(unit, target, minDist);
				unit.currentDir = SkillAction.GetDirection8(target.position - unit.position);
				unit.Generate?.UpdateUnitSpriteForDirection(unit);
				return BTStatus.Running;
			}

			if (unit.CombatState.State.evadeCooldown <= 0f)
				AIMovementHelper.MoveTowardsTarget(unit, target);
		}
		else
		{
			// 모든 스킬 쿨다운 — 원거리 유닛은 안전거리 유지
			int fallbackRange = maxSkillRange >= rangedMin ? maxSkillRange / 2 + 1 : 1;
			if (chebDist != fallbackRange && unit.CombatState.State.evadeCooldown <= 0f)
			{
				if (chebDist < fallbackRange) AIMovementHelper.MoveAwayFromTarget(unit, target, fallbackRange);
				else                          AIMovementHelper.MoveTowardsTarget(unit, target);
			}
		}

		unit.currentDir = SkillAction.GetDirection8(target.position - unit.position);
		unit.Generate?.UpdateUnitSpriteForDirection(unit);
		return BTStatus.Running;
	}

	private static Unit GetClosestEnemy(Unit unit, out float minDist)
	{
		Unit  best    = null;
		minDist = float.MaxValue;
		foreach (var e in unit.Perception.State.personalSpottedEnemies)
		{
			if (e == null || e.Health.hp <= 0 || e.currentFloor != unit.currentFloor) continue;
			float d = Vector2Int.Distance(unit.position, e.position);
			if (d < minDist) { minDist = d; best = e; }
		}
		return best;
	}
}
