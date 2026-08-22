using System.Collections.Generic;
using UnityEngine;

// "제자리 공격" 상태(기초문서.md 피드백, 2026-08-22 — R키 몬스터 배치 모드를 완전히 대체) — 사용자
// 결정: "배치 모드 완전 제거. 대신, 명령 항목에 하나 추가(제자리 공격). 이 명령을 넣어두면, 제자리에서
// 절대 이동하지 않고 공격만 함." HaltFSMState와 동일하게 UnitFSM.SelectState가 배열 우선순위와 무관하게
// unit.isStandGroundAttack==true인 동안 이 상태를 직접 강제 배정한다(_states 배열에는 넣지 않는다 —
// GetPriority는 그래서 의미가 없다).
//
// HaltFSMState("정지")와의 차이: 정지는 완전 무반응이지만, 이 상태는 이동만 하지 않을 뿐 사거리 내
// 적은 CombatFSMState와 동일한 스킬 우선순위/히트박스 판정으로 공격한다(원거리 유닛의 키팅·접근
// 이동만 빠짐). 해제 조건도 정지와 동일 — 새 직접 명령 또는 명령 취소만 풀 수 있다. 회피/블링크
// (DefenseSystem.EvaluateEarlyReaction)로 인한 위치 이동도 정지와 동일하게 차단된다(2026-08-22
// 사용자 신고 "제자리 공격중 회피및 점멸 여전히 존재함"으로 확정 — "제자리에서 절대 이동하지
// 않는다"는 조건에 예외가 없다는 뜻. UnitFunction.OnReactToThreat이 isHalted와 함께 확인한다).
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

		// 방향 전환(2026-08-22 재조정, 사용자 신고 "자연스러운 시선 전환이 구현 안됨. 기존에 있던
		// 로직인 소리나 피격 등으로 공격을 인지해서 그 방향을 바라봐야 함") — 한때 IdleFSMState와
		// 동일한 주기로 방향 갱신을 늦추는 타이머를 넣었었는데, 그러면 오히려 UnitFunction.
		// ResolveVisionDirection(01-A 11장 시야 방향 전환 우선순위 — 소리 감지/경계/스킬 사용 등
		// 이미 구현된 로직)이 매 틱 재계산하는 "이동 중"(최하위 폴백=currentDir 그대로 유지) 후보에
		// 얼어붙은 옛 방향을 넘겨버려서 부자연스러워졌다. CombatFSMState.ExecuteCombat과 동일하게
		// 매 틱 즉시 대상 방향으로 currentDir을 갱신하는 게 정답이다 — 이게 ResolveVisionDirection의
		// "다른 후보가 없을 때의 기본값" 역할을 하고, 소리/피격 등 더 급한 후보가 있으면
		// ResolveVisionDirection이 이 값을 자동으로 덮어써 그쪽을 본다.
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
			Hitbox skillBox = bestSkill.BuildSkillHitbox(unit);
			if (SkillAction.GetEnemiesInHitboxContains(unit, skillBox, target))
				bestSkill.Execute(unit, target, minDist);
		}

		unit.Generate?.UpdateUnitSpriteForDirection(unit);
		return BTStatus.Running;
	}
}
