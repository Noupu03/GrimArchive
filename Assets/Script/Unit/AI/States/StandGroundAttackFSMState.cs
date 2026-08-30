using System.Collections.Generic;
using UnityEngine;

// "제자리 공격" 명령 상태 — 제자리에서 절대 이동하지 않고 사거리 내 적만 CombatFSMState와 동일하게
// 공격한다(키팅·접근 이동만 빠짐). "절대 이동하지 않는다"는 조건에 예외가 없어 회피/블링크도
// 차단된다(UnitFunction.OnReactToThreat이 isHalted와 함께 확인). HaltFSMState와 동일하게
// UnitFSM.SelectState가 배열 우선순위와 무관하게 강제 배정한다(GetPriority는 무의미).
public class StandGroundAttackFSMState : IFSMState
{
	private readonly BTNode _bt = new BTLeaf(ExecuteStandGroundAttack);

	public float GetPriority(Unit unit)    => 0f; // SelectState가 직접 강제 배정 — 배열 비교 대상 아님.
	public bool  IsSticky(Unit unit)       => true;
	public bool  ShouldInterrupt(Unit unit)=> false;
	public void  OnEnter(Unit unit)        { }
	public void  OnExit(Unit unit)         { }
	public BTStatus Tick(Unit unit)        => _bt.Tick(unit);
	public string   GetLabel(Unit unit)    => "제자리 공격";

	private static BTStatus ExecuteStandGroundAttack(Unit unit)
	{
		if (unit.CombatState.State.isCastingAttack) return BTStatus.Running;

		Unit target = CombatFSMState.GetClosestEnemy(unit, out float minDist);
		if (target == null) return BTStatus.Running; // 대상 없음 — 제자리에서 계속 대기

		var unitSkills = unit.Generate != null
			? unit.Generate.GetSkills(unit.unitType.typeName)
			: new List<SkillAction>();

		// 방향 전환은 CombatFSMState.ExecuteCombat과 동일하게 매 틱 즉시 갱신한다 — 늦추면
		// ResolveVisionDirection(더 급한 후보가 있을 때 자동 덮어쓰는 시스템)의 폴백값이 얼어붙는다.
		unit.currentDir = SkillAction.GetDirection8(target.position - unit.position);
		unit.CombatState.State.currentAttackAngle = ((UnitFunction)unit).CalculateAttackAngleToEnemy(target, 1);

		SkillAction bestSkill    = null;
		float       bestPriority = float.MinValue;
		foreach (var skill in unitSkills)
		{
			if (skill == null || !skill.IsAvailable(unit)) continue;
			float p = skill.GetPriority(unit, target, minDist);
			if (p > bestPriority) { bestPriority = p; bestSkill = skill; }
		}

		// 사거리 밖이면 그냥 대기 — "절대 이동하지 않는다"는 명시된 조건이라 키팅/접근 이동을 전혀 하지 않는다.
		if (bestSkill != null)
		{
			// 스킬이 겨눌 대상 결정(아군 대상 스킬은 여기서 자기 대상을 찾는다) → 사거리 판정 → 실행.
			Unit resolved = bestSkill.ResolveTarget(unit, target);
			if (bestSkill.CanExecuteAgainst(unit, resolved, minDist))
				bestSkill.Execute(unit, resolved, minDist);
		}

		unit.Generate?.UpdateUnitSpriteForDirection(unit);
		return BTStatus.Running;
	}
}
