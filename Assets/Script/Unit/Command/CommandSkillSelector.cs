using UnityEngine;

// 행동 우선 명령(강공격·제압·보호)을 받은 파티원의 기술 선택(리더 명령 9장). 명령은 기술의 "선택 순서"만 바꾼다 — 마나·쿨타임·사거리·경로·행동 취소 조건은 기존 규칙(IsAvailable/CanExecuteAgainst)을 그대로 거친다.
// 연결 기술(대응 명령·순위)은 LeaderCommandTable(leader_commands.json)이 정한다. 연결된 기술을 지금 쓸 수 없으면 false를 돌려주고 호출부는 기존 개인 선택을 계속한다(기술을 기다리며 멈추지 않는다).
public static class CommandSkillSelector
{
	// 실행 직전 선택: 적용 중인 행동 우선 명령이 있으면 연결 기술 중 사용 가능하고 순위가 가장 높은 것을 고른다. CombatFSMState.EngageTarget이 부르며 이때 시전자의 바라보는 방향은 이미 공격 대상 쪽으로 정해져 있다.
	// 정확한 사거리 판정(CanExecuteAgainst)은 바라보는 방향·공격 각도로 만든 히트박스를 쓰므로, 연결 기술이 공격 대상이 아닌 다른 적(제압 지정 적·보호 대상을 위협하는 적)을 겨누면 그 적 쪽으로 방향을 맞춰 판정한다.
	// 기술을 골랐으면 방향은 고른 대상을 향한 채로 두어 시전·피해 판정이 같은 방향을 쓰고, 못 골랐으면 호출 전 방향으로 되돌린다.
	public static bool TryPickActive(Human unit, Unit attackTarget, out SkillAction skill, out Unit resolved)
	{
		skill = null; resolved = null;
		var state = unit?.LeaderState;
		var order = state?.Action;
		if (order == null || !LeaderCommandSettings.Enabled) return false;

		if (!order.IsActive(Time.time)) { state.Action = null; return false; } // 5초가 지났다 — 우선순위 적용은 끝난다
		if (RetreatSystem.IsBoundToRetreat(unit)) { state.Action = null; return false; } // 본인의 후퇴·도주 전환은 조기 종료 조건(9-표2)

		var dirBefore = unit.currentDir;
		float angleBefore = unit.CombatState.State.currentAttackAngle;
		bool picked = TryPick(unit, order.Command, attackTarget, order.SuppressTarget, exact: true, out skill, out resolved);
		if (picked && skill.Affinity == SkillAffinity.Enemy) AimAt(unit, resolved); // 후보를 훑는 동안 바뀐 방향이 아니라 고른 대상 쪽으로 확정
		else { unit.currentDir = dirBefore; unit.CombatState.State.currentAttackAngle = angleBefore; }
		return picked;
	}

	// 바라보는 방향과 공격 각도를 대상 쪽으로 맞춘다 — CombatFSMState.EngageTarget이 공격 대상에게 하는 것과 같은 계산이다.
	internal static void AimAt(Unit unit, Unit target)
	{
		unit.currentDir = SkillAction.GetDirection8(target.position - unit.position);
		unit.CombatState.State.currentAttackAngle = ((UnitFunction)unit).CalculateAttackAngleToEnemy(target, 1);
	}

	// 발령 조건 확인용: 이 파티원이 지금 이 명령의 연결 기술을 쓸 수 있는가. 다른 파티원의 바라보는 방향을 바꾸면 안 되므로 사거리는 거리로 근사한다(exact=false).
	public static bool CanUseNow(Human unit, TacticalCommand command, Unit attackTarget, Unit commandTarget)
		=> TryPick(unit, command, attackTarget, commandTarget, exact: false, out _, out _);

	public static bool TryPick(Human unit, TacticalCommand command, Unit attackTarget, Unit commandTarget, bool exact, out SkillAction skill, out Unit resolved)
	{
		skill = null; resolved = null;
		if (unit == null || unit.hp <= 0f || unit.unitType == null || unit.Generate == null) return false;
		var skills = unit.Generate.GetSkills(unit.unitType.typeName);
		if (skills == null) return false;

		int bestRank = int.MaxValue;
		float bestPriority = float.MinValue;
		foreach (var s in skills)
		{
			if (s == null || !LeaderCommandTable.TryGetRank(s.SkillName, command, out int rank)) continue; // 미지정 기술은 이 명령의 후보가 아니다
			if (!s.IsAvailable(unit)) continue;

			Unit target = ResolveTarget(unit, command, s, attackTarget, commandTarget);
			if (target == null || target.hp <= 0f) continue;

			float dist = Vector2Int.Distance(unit.position, target.position);
			if (!InReach(unit, s, target, dist, exact)) continue;

			float priority = s.GetPriority(unit, target, dist); // 같은 순위는 기존 개인 선택 기준으로 비교한다
			if (rank < bestRank || (rank == bestRank && priority > bestPriority))
			{
				bestRank = rank; bestPriority = priority; skill = s; resolved = target;
			}
		}
		return skill != null;
	}

	// 명령별 사용 대상(9-표1 "사용 대상·상황 조건"): 강공격은 기존 공격 대상, 제압 CC는 리더가 지정한 적·제압 공격 버프는 그 적을 공격 중인 아군이 있을 때, 보호는 기존 보호 대상 순서로 고른 아군(CC는 그 아군을 위협하는 적).
	private static Unit ResolveTarget(Human unit, TacticalCommand command, SkillAction s, Unit attackTarget, Unit commandTarget)
	{
		switch (command)
		{
			case TacticalCommand.Strike:
				return s.Affinity == SkillAffinity.Enemy ? attackTarget : null;

			case TacticalCommand.Suppress:
				if (commandTarget == null || commandTarget.hp <= 0f || commandTarget.currentFloor != unit.currentFloor) return null;
				if (s.Affinity == SkillAffinity.Enemy)
					return unit.personalSpottedEnemies.Contains(commandTarget) ? commandTarget : null; // 직접 확인하는 동안만
				return AnyAllyAttacking(unit, commandTarget) ? s.ResolveTarget(unit, commandTarget) : null; // 공격 버프는 그 적을 공격 중인 아군에게만

			case TacticalCommand.Protect:
				if (s.Affinity == SkillAffinity.Ally) return s.ResolveTarget(unit, attackTarget); // 치료·보호 버프는 각자의 기존 보호 대상 선정(IsAvailable·ResolveTarget)을 따른다
				return FindThreatTo(unit, unit.CombatTargeting.ProtectTarget);

			default:
				return null;
		}
	}

	private static bool InReach(Human unit, SkillAction s, Unit target, float dist, bool exact)
	{
		if (exact)
		{
			if (s.Affinity == SkillAffinity.Enemy) AimAt(unit, target); // 후보마다 그 기술이 겨눌 적 쪽 히트박스로 판정한다(앞 후보가 방향을 바꿔 놓았을 수 있다)
			return s.CanExecuteAgainst(unit, target, dist);
		}
		if (s.Affinity == SkillAffinity.Ally) return s.CanExecuteAgainst(unit, target, dist); // 아군 대상은 바라보는 방향을 쓰지 않는다
		return AIMovementHelper.ChebyshevDistance(unit.position, target.position) <= Mathf.Max(1, s.HitRange);
	}

	private static bool AnyAllyAttacking(Human unit, Unit enemy)
	{
		var party = unit.party;
		if (party == null) return unit.CombatTargeting.AttackTarget == enemy;
		foreach (var m in party.Members)
			if (m != null && m.hp > 0f && m.CombatTargeting.AttackTarget == enemy) return true;
		return false;
	}

	// 보호 대상을 지금 공격 중인 적 중 이 유닛이 직접 보고 있는 하나(가장 가까운 적).
	private static Unit FindThreatTo(Human unit, Unit protectTarget)
	{
		if (protectTarget == null) return null;
		Unit best = null;
		float bestDist = float.MaxValue;
		foreach (var e in unit.personalSpottedEnemies)
		{
			if (e == null || e.hp <= 0f || e.currentFloor != unit.currentFloor || e.CombatTargeting.AttackTarget != protectTarget) continue;
			float d = Vector2Int.Distance(unit.position, e.position);
			if (d < bestDist) { best = e; bestDist = d; }
		}
		return best;
	}

	// 발령 확인에서 쓸 기본 공격 대상 — 이 파티원의 현재 공격 대상, 없으면 집중 공격 대상, 없으면 가장 가까운 직접 확인한 적.
	public static Unit DefaultAttackTarget(Human unit, Unit focusTarget)
	{
		var current = unit.CombatTargeting.AttackTarget;
		if (current != null && current.hp > 0f && current.currentFloor == unit.currentFloor) return current;
		if (focusTarget != null && focusTarget.hp > 0f && unit.personalSpottedEnemies.Contains(focusTarget)) return focusTarget;
		Unit best = null;
		float bestDist = float.MaxValue;
		foreach (var e in unit.personalSpottedEnemies)
		{
			if (e == null || e.hp <= 0f || e.currentFloor != unit.currentFloor) continue;
			float d = Vector2Int.Distance(unit.position, e.position);
			if (d < bestDist) { best = e; bestDist = d; }
		}
		return best;
	}
}
