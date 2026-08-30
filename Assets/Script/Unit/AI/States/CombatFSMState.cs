using System.Collections.Generic;
using UnityEngine;

// 전투 상태 — 인지된 적이 같은 층에 있을 때 활성화.
// BT: 키팅(원거리 위험거리 이하) → 공격(사거리·스킬 준비) → 접근
public class CombatFSMState : IFSMState
{
	private readonly BTNode _bt = new BTLeaf(ExecuteCombat);

	public float GetPriority(Unit unit)
	{
		// 플레이어 수동 명령은 PlayerCommandFSMState(배열 맨 앞, 최우선)가 전담한다 — 명령이 활성
		// 상태면 이 GetPriority는 호출되지도 않으므로 따로 예외 처리할 필요가 없다.
		bool isRoomConfined = AIMovementHelper.IsRoomConfined(unit);
		int myRoomId = isRoomConfined && unit.Session?.cmap != null
			? unit.Session.cmap.GetRoomIdAt(unit.currentFloor, unit.position)
			: -1;

		Human hu = unit as Human;

		foreach (var e in unit.Perception.State.personalSpottedEnemies)
		{
			if (e == null || e.hp <= 0 || e.currentFloor != unit.currentFloor) continue;
			// 야생 몬스터(방 제한)는 방을 나간 적과 전투를 유지하지 않는다. 시야 방향 제한
			// (ClampDirectionToOwnRoom)으로도 대체로 자연히 풀리지만, 직선 통로처럼 시야가 방 밖까지
			// 뚫리는 예외를 막는 안전망.
			if (isRoomConfined && myRoomId >= 0 && unit.Session.cmap.GetRoomIdAt(e.currentFloor, e.position) != myRoomId)
				continue;

			// 07문서 7장/07-A 9장: 위험도 2단계 이상 + 비근거리(>2칸)면 즉시 전투 대신 합류 대기로
			// 넘긴다 — 이미 대기 중이면 그대로 Tactical에 양보(완료 시 TickJoinCombatWait이 비워줌).
			if (hu != null)
			{
				if (hu.currentJoinCombatWait != null)
				{
					if (hu.currentJoinCombatWait.TargetEnemy == e) continue;
				}
				else if (PropagationSystem.ShouldDeferForJoinWait(hu, e))
				{
					PropagationSystem.StartJoinCombatWait(hu, e);
					continue;
				}
			}

			return AIConfigLoader.Behavior?.combatPriority ?? 100f;
		}
		return 0f;
	}

	public bool IsSticky(Unit unit)        => false;
	public bool ShouldInterrupt(Unit unit) => true;

	// 03문서 4-5장: 경계/소리반응 중 정확 인지로 전투에 진입하면 Tactical의 Alert 리프가 자기 완료
	// 코드를 못 거치고 버려진다 — 여기서 미리 비워두면 OnExit의 "전투 후 스윕 세팅" 분기가 정상 동작한다.
	public void OnEnter(Unit unit)         { unit.currentAlertSearch = null; }

	// 전투 종료 시 전투 후 경계 세팅 (03문서 4-8장).
	// 단, 플레이어 명령이 활성화된 채 전투 상태를 벗어나는 경우엔 경계를 세팅하지 않는다 —
	// 명령 실행 직후 alertSearch가 TacticalFSMState를 불필요하게 트리거해 명령이 중단되는 걸 방지.
	public void OnExit(Unit unit)
	{
		bool playerCommandActive = (unit.playerMoveTarget.HasValue && unit.isManualMoveCommand)
			|| (unit.playerAttackTarget != null && unit.playerAttackTarget.hp > 0)
			|| unit.playerAttackObjectTarget.HasValue; // 코어/문 공격 명령도 동일 취급.
		if (playerCommandActive) return;

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
		int        chebDist = AIMovementHelper.ChebyshevDistance(target.position, unit.position);

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
			// 키팅: 원거리 스킬이고 적이 위험거리 이하 — 단, 아군 대상 스킬(치유/보호막/파티버프)이
			// 최우선으로 뽑혔다는 건 아군이 위독하다는 뜻이므로 물러나지 말고 그 자리에서 바로 쓴다.
			if (bestSkill.Affinity != SkillAffinity.Ally && bestSkill.HitRange >= rangedMin)
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

			// 스킬이 실제로 겨눌 대상을 먼저 결정한다 — 아군 대상 스킬(치유/보호막/버프)은 여기서
			// 자기 대상을 직접 찾아 돌려주므로, 아래 사거리 판정과 실행이 그 대상을 그대로 쓴다.
			Unit resolved = bestSkill.ResolveTarget(unit, target);
			if (bestSkill.CanExecuteAgainst(unit, resolved, minDist))
			{
				bestSkill.Execute(unit, resolved, minDist);
				unit.currentDir = SkillAction.GetDirection8(target.position - unit.position);
				unit.Generate?.UpdateUnitSpriteForDirection(unit);
				return BTStatus.Running;
			}

			AIMovementHelper.MoveTowardsTarget(unit, target);
		}
		else
		{
			// 모든 스킬 쿨다운 — 원거리 유닛은 안전거리 유지
			int fallbackRange = maxSkillRange >= rangedMin ? maxSkillRange / 2 + 1 : 1;
			if (chebDist != fallbackRange)
			{
				if (chebDist < fallbackRange) AIMovementHelper.MoveAwayFromTarget(unit, target, fallbackRange);
				else                          AIMovementHelper.MoveTowardsTarget(unit, target);
			}
		}

		unit.currentDir = SkillAction.GetDirection8(target.position - unit.position);
		unit.Generate?.UpdateUnitSpriteForDirection(unit);
		return BTStatus.Running;
	}

	// internal — StandGroundAttackFSMState("제자리 공격")가 이동 없이 사거리 내 적만 공격할 때
	// 동일한 타깃 선정 로직을 재사용한다.
	internal static Unit GetClosestEnemy(Unit unit, out float minDist)
	{
		// GetPriority와 동일한 방 제한(야생 몬스터) — 다른 이유로 전투가 켜져 있어도 타깃팅에서
		// 방 밖 적을 고르지 않도록 일관되게 적용.
		bool isRoomConfined = AIMovementHelper.IsRoomConfined(unit);
		int myRoomId = isRoomConfined && unit.Session?.cmap != null
			? unit.Session.cmap.GetRoomIdAt(unit.currentFloor, unit.position)
			: -1;

		Unit  best    = null;
		minDist = float.MaxValue;
		foreach (var e in unit.Perception.State.personalSpottedEnemies)
		{
			if (e == null || e.Health.hp <= 0 || e.currentFloor != unit.currentFloor) continue;
			if (isRoomConfined && myRoomId >= 0 && unit.Session.cmap.GetRoomIdAt(e.currentFloor, e.position) != myRoomId) continue;
			float d = Vector2Int.Distance(unit.position, e.position);
			if (d < minDist) { minDist = d; best = e; }
		}
		return best;
	}

}
