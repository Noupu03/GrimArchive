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
		// TacticalFSMState와 동일한 이유로, 던전 입구 시퀀스 동안은 이 상태도 비활성화한다 — 숨은
		// 스폰 청크·입구 방은 원래 몬스터가 없는 안전 구역이라 실제로 거의 안 걸리는 방어적 조치다.
		if (unit is Human entranceHu && entranceHu.isInDungeonEntranceSequence) return 0f;
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

		// 02번 11장 "이번 전투에서"의 범위 정리 — 다음 교전은 새 표본·새 대상 선택으로 시작한다.
		unit.CombatTargeting.ResetOnCombatExit();
	}

	public BTStatus Tick(Unit unit)   => _bt.Tick(unit);
	public string   GetLabel(Unit unit) => "전투";

	private static BTStatus ExecuteCombat(Unit unit)
	{
		if (unit.CombatState.State.isCastingAttack) return BTStatus.Running;

		// 02번 0장: 긴급 아군 보호를 공격 후보 판단보다 먼저 확인한다. 실행 여부는 스킬별 IsAvailable이
		// 결정하므로(보호 가능한 스킬이 없으면 자연히 일반 공격으로 넘어감) 여기서는 상태만 갱신한다.
		ResolveEmergencyProtectTarget(unit);

		// 04번 문서 4장: 원거리공격/근접·원거리지원 역할은 이 시점부터 회피형 이동으로 승급한다 —
		// 이후 이 메서드의 기존 MoveTowardsTarget/MoveAwayFromTarget 호출은 그대로 두어도 자동으로
		// 회피 경로를 탄다(MovementAlgorithm 교체만으로 성립).
		AIMovementHelper.EnsureAvoidanceMovementForRole(unit, CombatScoreMath.ResolveCombatRole(unit.unitType));

		// 02번 9번 항목: 보호 대상이 있고 보호 스킬 사거리 밖이면 접근 이동부터 처리한다 — 사거리 안이면
		// 기존 흐름(SkillAction_Heal의 emergencyBonus=1000 우선순위)이 이미 담당하므로 여기서 건드리지 않는다.
		if (TryEmergencyProtectApproach(unit)) return BTStatus.Running;

		Unit target = SelectAttackTarget(unit, out float minDist);
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
				// 02번 5장: 공격을 실제로 실행하면(회피·방어로 무효여도) 누적 이동 칸수를 초기화한다.
				unit.CombatTargeting.ResetRetargetTracking();
				unit.currentDir = SkillAction.GetDirection8(target.position - unit.position);
				unit.Generate?.UpdateUnitSpriteForDirection(unit);
				return BTStatus.Running;
			}

			if (AIMovementHelper.MoveTowardsTarget(unit, target)) TrackRetargetMovement(unit);
		}
		else
		{
			// 모든 스킬 쿨다운 — 원거리 유닛은 안전거리 유지
			int fallbackRange = maxSkillRange >= rangedMin ? maxSkillRange / 2 + 1 : 1;
			if (chebDist != fallbackRange)
			{
				if (chebDist < fallbackRange) AIMovementHelper.MoveAwayFromTarget(unit, target, fallbackRange);
				else if (AIMovementHelper.MoveTowardsTarget(unit, target)) TrackRetargetMovement(unit);
			}
		}

		unit.currentDir = SkillAction.GetDirection8(target.position - unit.position);
		unit.Generate?.UpdateUnitSpriteForDirection(unit);
		return BTStatus.Running;
	}

	// 02번 8~9장: 자기 자신을 포함해 긴급 보호 후보를 찾아 unit.CombatTargeting.ProtectTarget에
	// 반영한다 — 실제 보호 스킬 선택은 각 스킬의 IsAvailable이 담당. internal, StandGroundAttackFSMState도 호출.
	internal static void ResolveEmergencyProtectTarget(Unit unit)
	{
		Unit current = unit.CombatTargeting.ProtectTarget;
		bool currentStillValid = current != null && current.Health != null && current.Health.hp > 0
			&& current.currentFloor == unit.currentFloor && IsEmergency(unit, current);

		Unit best = currentStillValid ? current : null;
		float bestRatio = 0f, bestDist = 0f; bool bestIncap = false;
		if (best != null)
		{
			bestRatio = best.Health.hp / Mathf.Max(1f, best.Health.maxHp);
			bestIncap = best.StatusEffects.State.stunDuration > 0f;
			bestDist = Vector2Int.Distance(unit.position, best.position);
		}

		void Consider(Unit candidate)
		{
			if (!IsEmergency(unit, candidate)) return;
			float ratio = candidate.Health.hp / Mathf.Max(1f, candidate.Health.maxHp);
			bool incap = candidate.StatusEffects.State.stunDuration > 0f;
			float dist = Vector2Int.Distance(unit.position, candidate.position);
			if (best == null || CombatScoreMath.IsBetterProtectCandidate(ratio, incap, dist, bestRatio, bestIncap, bestDist))
			{ best = candidate; bestRatio = ratio; bestIncap = incap; bestDist = dist; }
		}

		if (unit.Session?.units != null)
		{
			foreach (var u in unit.Session.units)
			{
				if (u == null || u == current || u == unit || u.currentFloor != unit.currentFloor) continue;
				if (unit.IsEnemy(u)) continue;
				Consider(u);
			}
		}
		Consider(unit); // 자기 자신도 같은 순서의 후보 — 자동으로 우선하지 않는다(비교식이 동일하게 적용).

		unit.CombatTargeting.ProtectTarget = best;
	}

	// 02번 9번 항목: ResolveEmergencyProtectTarget이 보호 대상은 정했지만 그 대상에게 다가가는 이동
	// 자체가 이전엔 없었다(사거리 밖이면 그냥 무시되고 SelectAttackTarget이 고른 적을 쫓아갔음). 이
	// 메서드가 그 "접근 이동"을 새로 담당한다 — 사거리 안이면 개입하지 않고 false를 반환한다.
	private static bool TryEmergencyProtectApproach(Unit unit)
	{
		Unit protect = unit.CombatTargeting.ProtectTarget;
		if (protect == null || protect == unit) return false;

		var skills = unit.Generate != null ? unit.Generate.GetSkills(unit.unitType.typeName) : null;
		int protectRange = 0;
		if (skills != null)
			foreach (var s in skills)
				if (s != null && s.Affinity == SkillAffinity.Ally && s.HitRange > protectRange) protectRange = s.HitRange;

		if (protectRange <= 0) return false; // 보호 수단 자체가 없음 — 개입하지 않는다.
		if (AIMovementHelper.ChebyshevDistance(unit.position, protect.position) <= protectRange) return false;

		AIMovementHelper.MoveTowardsProtectTargetWithRiskCheck(unit, protect.position);
		return true;
	}

	private static bool IsEmergency(Unit protector, Unit candidate)
	{
		if (candidate?.Health == null || candidate.Health.hp <= 0) return false;
		float ratio = candidate.Health.hp / Mathf.Max(1f, candidate.Health.maxHp);
		bool incapacitated = candidate.StatusEffects.State.stunDuration > 0f;
		bool underThreat = candidate.HasPerceivedThreatCollider();
		return CombatScoreMath.IsEmergencyProtectCandidate(ratio, underThreat, incapacitated, candidate.isHitThisTurn);
	}

	// 02번 5장: 대상을 "바꾸려는" 이동에만 누적한다 — 원래(안 바뀐) 대상을 계속 추격하는 이동은 제외.
	private static void TrackRetargetMovement(Unit unit)
	{
		if (unit.CombatTargeting.IsChasingRetargetedEnemy) unit.CombatTargeting.MovedTilesSinceRetarget++;
	}

	// internal — StandGroundAttackFSMState("제자리 공격")도 동일한 타깃 선정 로직을 재사용한다.
	// 02번 3~5장: 개인위험도(인류)/종류설정(몬스터·야생) × 역할배율 × 대상종류배율로 점수를 매기고,
	// 인류는 1.2배·몬스터/야생은 더 높을 때만 교체(5장 이동한도 확인). 보스 집중은 위협 없는 한 유지.
	internal static Unit SelectAttackTarget(Unit unit, out float minDist)
	{
		minDist = float.MaxValue;

		// GetPriority와 동일한 방 제한(야생 몬스터) — 다른 이유로 전투가 켜져 있어도 타깃팅에서
		// 방 밖 적을 고르지 않도록 일관되게 적용.
		bool isRoomConfined = AIMovementHelper.IsRoomConfined(unit);
		int myRoomId = isRoomConfined && unit.Session?.cmap != null
			? unit.Session.cmap.GetRoomIdAt(unit.currentFloor, unit.position)
			: -1;

		var allCandidates = new List<Unit>();
		var threatCandidates = new List<Unit>();
		foreach (var e in unit.Perception.State.personalSpottedEnemies)
		{
			if (e == null || e.Health.hp <= 0 || e.currentFloor != unit.currentFloor) continue;
			if (isRoomConfined && myRoomId >= 0 && unit.Session.cmap.GetRoomIdAt(e.currentFloor, e.position) != myRoomId) continue;
			allCandidates.Add(e);
			if (IsAttackingMeOrAlly(unit, e)) threatCandidates.Add(e);
		}
		if (allCandidates.Count == 0) return null;

		var pool = threatCandidates.Count > 0 ? threatCandidates : allCandidates;

		bool isHuman = unit is Human;
		CombatRole selfRole = CombatScoreMath.ResolveCombatRole(unit.unitType);

		// 4장: 보스 집중 유지 — 보스 외의 현재 위협(자신/아군을 공격 중인 다른 후보)이 없으면
		// 20% 기준 없이 그대로 복귀한다.
		Unit boss = unit.CombatTargeting.BossFocusTarget;
		bool bossStillValid = boss != null && boss.Health.hp > 0 && boss.currentFloor == unit.currentFloor && allCandidates.Contains(boss);
		if (!bossStillValid) unit.CombatTargeting.BossFocusTarget = null;
		if (bossStillValid && !threatCandidates.Exists(t => t != boss))
		{
			unit.CombatTargeting.AttackTarget = boss;
			minDist = Vector2Int.Distance(unit.position, boss.position);
			return boss;
		}

		Unit best = null;
		float bestScore = float.MinValue;
		float bestDist = float.MaxValue;
		foreach (var cand in pool)
		{
			float score = ComputeAttackScore(unit, isHuman, selfRole, cand);
			float d = Vector2Int.Distance(unit.position, cand.position);
			// 최초 선택(동점) 타이브레이크는 "첫 공격 효과까지 걸리는 시간"이 기준이나, 이동시간·선딜을
			// 전부 반영한 정밀 계산은 범위 밖이라 현재 거리로 근사한다.
			if (best == null || score > bestScore || (score == bestScore && d < bestDist))
			{ best = cand; bestScore = score; bestDist = d; }
		}

		// pool이 아니라 allCandidates로 유효성을 확인한다 — pool(위협 우선)로 확인하면 현재 대상이
		// 위협 후보가 아닐 때 다른 위협이 나타나는 것만으로 "대상 없음"과 동일 취급돼 아래 20%/이동한도
		// 게이트를 건너뛰고 즉시 교체되는 버그가 있었다(02번 5장 "이동 한도를 적용하지 않는 행동" 목록에
		// 이 경우는 없음). 후보 자체를 좁히는 것(pool)과 유지 중인 대상의 유효성(currentValid)은 별개 축.
		Unit current = unit.CombatTargeting.AttackTarget;
		bool currentValid = current != null && current.Health.hp > 0 && current.currentFloor == unit.currentFloor && allCandidates.Contains(current);

		if (!currentValid)
		{
			unit.CombatTargeting.AttackTarget = best;
			unit.CombatTargeting.ResetRetargetTracking();
			if (CombatScoreMath.ResolveTargetCategory(best) == TargetCategory.Boss) unit.CombatTargeting.BossFocusTarget = best;
			minDist = bestDist;
			return best;
		}

		if (best != current)
		{
			float currentScore = ComputeAttackScore(unit, isHuman, selfRole, current);
			if (CombatScoreMath.ShouldSwitchAttackTarget(isHuman, currentScore, bestScore))
			{
				int limit = CombatScoreMath.AttackRetargetMoveLimit(selfRole);
				// 새 대상까지 남은 이동은 실제 선택 경로 대신 체비셰프 거리로 근사한다(04번 문서의 실제
				// 경로 계산까지 연동하려면 경로탐색 결과가 필요해 범위 밖).
				int estRemaining = AIMovementHelper.ChebyshevDistance(unit.position, best.position);
				if (unit.CombatTargeting.MovedTilesSinceRetarget + estRemaining <= limit)
				{
					unit.CombatTargeting.AttackTarget = best;
					unit.CombatTargeting.IsChasingRetargetedEnemy = true; // 누적 칸수는 유지(실행 시에만 초기화)
					if (CombatScoreMath.ResolveTargetCategory(best) == TargetCategory.Boss) unit.CombatTargeting.BossFocusTarget = best;
					minDist = bestDist;
					return best;
				}
				// 한도 초과 — 이번 교체는 보류하고 기존 대상을 유지한다(출발 위치로 자동 복귀하지 않음).
			}
		}

		minDist = Vector2Int.Distance(unit.position, current.position);
		return current;
	}

	// 4장: "자신이나 아군을 공격 중인" 후보 — enemy의 현재 공격 대상이 나 자신이거나 나의 적이 아닌
	// (=아군인) 살아있는 유닛이면 참이다. 모든 진영이 CombatTargeting.AttackTarget을 공유해 쓰므로
	// 대칭적으로 성립한다.
	private static bool IsAttackingMeOrAlly(Unit observer, Unit enemy)
	{
		Unit enemyTarget = enemy.CombatTargeting.AttackTarget;
		return enemyTarget != null && enemyTarget.Health.hp > 0 && !observer.IsEnemy(enemyTarget);
	}

	// 3장: 인류는 개인위험도×역할배율×대상종류배율, 플레이어·야생은 역할배율×대상종류배율(종류별 개별
	// 설정은 후속 콘텐츠 데이터 — 지금은 역할 배율을 그대로 초기 출발값으로 사용).
	private static float ComputeAttackScore(Unit observer, bool isHuman, CombatRole selfRole, Unit target)
	{
		CombatRole targetRole = CombatScoreMath.ResolveCombatRole(target.unitType);
		TargetCategory category = CombatScoreMath.ResolveTargetCategory(target);
		if (isHuman)
		{
			float danger = observer.Knowledge != null ? observer.Knowledge.GetPersonalDanger(observer, target) : 0f;
			return CombatScoreMath.HumanAttackScore(danger, selfRole, targetRole, category);
		}
		return CombatScoreMath.MonsterAttackScore(selfRole, targetRole, category);
	}
}
