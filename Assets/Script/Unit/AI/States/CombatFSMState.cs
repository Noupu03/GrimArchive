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
		bool isRoomConfined = AIMovementHelper.IsRoomConfined(unit);
		int myRoomId = isRoomConfined && unit.Session?.cmap != null
			? unit.Session.cmap.GetRoomIdAt(unit.currentFloor, unit.position)
			: -1;

		Human hu = unit as Human;

		foreach (var e in unit.Perception.State.personalSpottedEnemies)
		{
			if (e == null || e.hp <= 0 || e.currentFloor != unit.currentFloor) continue;
			// 2026-07-27 신규 — 야생 몬스터 A는 방을 나간 적과 전투를 유지하지 않는다(사용자 요청:
			// "상대 전투 유닛이 방 밖으로 나가면 전투 상태가 해제됨"). 시야 방향 제한(UnitFunction.
			// ClampDirectionToOwnRoom)만으로도 대체로 자연히 풀리지만, 직선 통로처럼 시야가 방 밖까지
			// 뚫리는 예외를 막는 안전망.
			if (isRoomConfined && myRoomId >= 0 && unit.Session.cmap.GetRoomIdAt(e.currentFloor, e.position) != myRoomId)
				continue;

			// 07문서 7장/07-A 9장(2026-07-31 신규): 위험도 2단계 이상 + 비근거리(>2칸)면 즉시 전투 대신
			// 합류 대기로 넘긴다 — 이미 이 적을 기준으로 대기 중이면 그대로 Tactical에 양보(대기 완료 시
			// PropagationSystem.TickJoinCombatWait이 currentJoinCombatWait를 비워 다음 틱에 정상 진입).
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

	// 03문서 4-5장(2026-08-06 수정): 경계/소리반응 중 정확 인지로 전투에 진입하면 Tactical의 Alert
	// 리프가 자기 완료 코드(TacticalFSMState.cs의 currentAlertSearch=null 처리)를 못 거치고 그대로
	// 버려진다 — 전투 중에는 IsSoundUnresponsive가 새 소리 반응 생성 자체를 막으므로, 전투 종료 시점의
	// currentAlertSearch는 항상 이 낡은 전투-진입-전 잔재이거나 null 둘 중 하나다. 여기서 미리 비워두면
	// 아래 OnExit의 "currentAlertSearch==null이면 전투 후 스윕 세팅" 분기가 매번 정상 동작한다.
	public void OnEnter(Unit unit)         { unit.currentAlertSearch = null; }

	// 전투 종료 시 전투 후 경계 세팅 (03문서 4-8장).
	// 단, 플레이어 명령이 활성화된 채 전투 상태를 벗어나는 경우엔 경계를 세팅하지 않는다 —
	// 명령 실행 직후 alertSearch가 TacticalFSMState를 불필요하게 트리거해 명령이 중단되는 걸 방지.
	public void OnExit(Unit unit)
	{
		bool playerCommandActive = (unit.playerMoveTarget.HasValue && unit.isManualMoveCommand)
			|| (unit.playerAttackTarget != null && unit.playerAttackTarget.hp > 0)
			|| unit.playerAttackObjectTarget.HasValue; // 기초문서.md 피드백(2026-08-22) — 코어/문 공격 명령도 동일 취급.
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
			if (SkillAction.GetEnemiesInHitboxContains(unit, skillBox, target))
			{
				bestSkill.Execute(unit, target, minDist);
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

	// internal — StandGroundAttackFSMState("제자리 공격", 기초문서.md 피드백 2026-08-22)가 이동 없이
	// 사거리 내 적만 공격할 때 동일한 타깃 선정 로직을 재사용한다.
	internal static Unit GetClosestEnemy(Unit unit, out float minDist)
	{
		// 2026-07-27 신규: GetPriority와 동일한 방 제한(야생 몬스터 A) — 다른 이유로 방 안 적을
		// 상대로 전투가 켜져 있어도 실제 타깃팅에서 방 밖 적을 고르지 않도록 일관되게 적용.
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
