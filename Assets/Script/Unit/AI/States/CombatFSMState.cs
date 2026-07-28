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
		bool isRoomConfined = IsRoomConfined(unit);
		int myRoomId = isRoomConfined && unit.Session?.cmap != null
			? unit.Session.cmap.GetRoomIdAt(unit.currentFloor, unit.position)
			: -1;

		foreach (var e in unit.Perception.State.personalSpottedEnemies)
		{
			if (e == null || e.hp <= 0 || e.currentFloor != unit.currentFloor) continue;
			// 2026-07-27 신규 — 야생 몬스터 A는 방을 나간 적과 전투를 유지하지 않는다(사용자 요청:
			// "상대 전투 유닛이 방 밖으로 나가면 전투 상태가 해제됨"). 시야 방향 제한(UnitFunction.
			// ClampDirectionToOwnRoom)만으로도 대체로 자연히 풀리지만, 직선 통로처럼 시야가 방 밖까지
			// 뚫리는 예외를 막는 안전망.
			if (isRoomConfined && myRoomId >= 0 && unit.Session.cmap.GetRoomIdAt(e.currentFloor, e.position) != myRoomId)
				continue;
			return AIConfigLoader.Behavior?.combatPriority ?? 100f;
		}
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
		// 2026-07-27 신규: GetPriority와 동일한 방 제한(야생 몬스터 A) — 다른 이유로 방 안 적을
		// 상대로 전투가 켜져 있어도 실제 타깃팅에서 방 밖 적을 고르지 않도록 일관되게 적용.
		bool isRoomConfined = IsRoomConfined(unit);
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

	// 플레이어 진영 몬스터 방 제한 MVP(2026-07-27, 사용자 요청 "몬스터는 방과 방 사이 못봄" +
	// 사용자 신고 "자꾸 전투 상태가 됨") — 기존엔 야생 몬스터(WildMonsterBehavior)만 방 제한 대상으로
	// 하드코딩돼 있어서, 나중에 방 제한이 추가된 플레이어 몬스터(BuildingManager.SpawnUnitFromBuilding)
	// 에는 이 안전망이 안 걸려 방 밖 적과도 계속 전투 상태가 됐다. FactionBehavior 타입을 나열하는 대신
	// 실제 방 제한 여부를 결정하는 MovementAlgorithm(RoomConfinedMovement)을 직접 확인해 단일 기준으로
	// 통일한다 — 앞으로 다른 유닛이 방 제한을 받게 되어도 이 체크가 자동으로 따라간다.
	private static bool IsRoomConfined(Unit unit) => unit.MovementAlgorithm is RoomConfinedMovement;
}
