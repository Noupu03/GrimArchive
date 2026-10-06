using System.Collections.Generic;
using UnityEngine;

// 전투 상태 — 인지된 적이 같은 층에 있을 때 활성화.
// BT: 키팅(원거리 위험거리 이하) → 공격(사거리·스킬 준비) → 접근
public class CombatFSMState : IFSMState
{
	private readonly BTNode _bt = new BTLeaf(ExecuteCombat);

	public float GetPriority(Unit unit)
	{
		// 플레이어 수동 명령은 PlayerCommandFSMState가 전담해 명령 중엔 이 GetPriority가 호출되지 않는다.
		// 던전 입구 시퀀스 중에는 비활성화한다(TacticalFSMState와 동일 — 입구 방은 안전 구역이라 방어적 조치).
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

			// 추격 정체로 일시 배제된 대상은 후보에서 뺀다 — SelectAttackTarget의 같은 필터와 짝이 맞아야 '후보 0인데 상태 유지' 불일치가 없다.
			if (unit.combatUnreachableTarget == e && Time.time < unit.combatUnreachableUntil) continue;

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
				else
				{
					// 합류 대기가 필요 없는 즉시 전투도 전파를 병행한다(03번 6장) — 발견자는 대기 없이 전투하고 파티원에게 위치를 알린다.
					PropagationSystem.PropagateEnemySighting(hu, e);
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
		ClearBlockingTrap(unit); // 전투 중 함정 파괴는 전투와 함께 끝난다(채널링이 남아 계속 깎이지 않게)

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

		// 원거리공격/근접·원거리지원 역할은 여기서 회피형 이동으로 승급한다(04번 4장) — 이후 MoveTowardsTarget/MoveAwayFromTarget 호출은 그대로 두어도 회피 경로를 탄다.
		AIMovementHelper.EnsureAvoidanceMovementForRole(unit, CombatScoreMath.ResolveCombatRole(unit.unitType));

		// 보호 대상이 보호 스킬 사거리 밖이면 접근 이동부터 처리한다(02번 9항) — 사거리 안이면 SkillAction_Heal(emergencyBonus=1000)이 담당한다.
		if (TryEmergencyProtectApproach(unit)) return BTStatus.Running;

		// 일반 치료 대상이 사거리 밖이면 접근 이동부터 처리한다 — 위 긴급보호와
		// 동일한 패턴(사거리 안이면 개입하지 않고 false 반환, 이미 있는 일반 스킬 선택에 맡긴다).
		if (TryGeneralHealApproach(unit)) return BTStatus.Running;

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
				// 물러날 칸이 없으면(벽에 몰림) 후퇴를 포기하고 아래 공격 판단으로 이어진다 — 옆으로 새며 떠는 대신 그 자리에서 싸운다.
				if (chebDist <= dangerDist && unit.CombatState.State.evadeCooldown <= 0f && AIMovementHelper.MoveAwayFromTarget(unit, target))
				{
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
				ClearBlockingTrap(unit); // 적과 교전이 시작되면 함정 파괴는 접는다
				// 02번 5장: 공격을 실제로 실행하면(회피·방어로 무효여도) 누적 이동 칸수를 초기화한다.
				unit.CombatTargeting.ResetRetargetTracking();
				unit.combatChaseStuckTurns = 0; // 공격이 실행됐다는 건 추격 정체가 아니라는 뜻.
				unit.currentDir = SkillAction.GetDirection8(target.position - unit.position);
				unit.Generate?.UpdateUnitSpriteForDirection(unit);
				return BTStatus.Running;
			}

			ChaseTarget(unit, target);
		}
		else
		{
			// 모든 스킬 쿨다운 — 원거리 유닛은 위험 거리(HitRange/2) 이내면 한 걸음 물러나고, 그 밖~사거리 안에서는 제자리에서 쿨다운을 기다린다(RangedSpacingMath). 예전엔 거리가 정확히 HitRange/2+1칸이 아니면 접근·후퇴해, 적이 한 칸만 움직여도 쿨다운마다 앞뒤로 왕복했다(2026-10-05 아처 떨림).
			if (maxSkillRange >= rangedMin)
			{
				switch (RangedSpacingMath.Decide(chebDist, maxSkillRange / 2, maxSkillRange))
				{
					case RangedSpacing.Retreat: AIMovementHelper.MoveAwayFromTarget(unit, target); break; // 몰려 못 물러나면 그 자리에서 쿨다운을 기다린다
					case RangedSpacing.Approach: ChaseTarget(unit, target); break;
				}
			}
			else if (chebDist > 1) ChaseTarget(unit, target); // 근접 유닛 — 붙을 때까지 접근
		}

		unit.currentDir = SkillAction.GetDirection8(target.position - unit.position);
		unit.Generate?.UpdateUnitSpriteForDirection(unit);
		return BTStatus.Running;
	}

	// 자기 자신을 포함해 긴급 보호 후보를 찾아 ProtectTarget에 반영한다(02번 8~9장) — 실제 보호 스킬 선택은 각 스킬의 IsAvailable이 맡는다. internal, StandGroundAttackFSMState도 호출.
	internal static void ResolveEmergencyProtectTarget(Unit unit)
	{
		Unit current = unit.CombatTargeting.ProtectTarget;
		var bestKey = default(CombatScoreMath.ProtectCandidateKey);
		bool currentStillValid = current != null && current.currentFloor == unit.currentFloor && TryEvaluateEmergency(unit, current, out bestKey);

		Unit best = currentStillValid ? current : null;

		// 그래도 동률이면 무작위 — 다만 '현재 유효 대상 유지'가 더 높은 기준이라 current가 유효하면 동률 후보를 무작위에 끼우지 않고 current를 유지한다(current가 없을 때만 추적).
		var tiedBest = currentStillValid ? null : new List<Unit>();

		void Consider(Unit candidate)
		{
			if (!TryEvaluateEmergency(unit, candidate, out var key)) return;
			if (best == null || CombatScoreMath.IsBetterProtectCandidate(key, bestKey))
			{
				best = candidate; bestKey = key;
				tiedBest?.Clear();
				tiedBest?.Add(candidate);
			}
			else if (tiedBest != null && key.Equals(bestKey))
			{
				tiedBest.Add(candidate);
			}
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

		if (tiedBest != null && tiedBest.Count > 1) best = tiedBest[UnityEngine.Random.Range(0, tiedBest.Count)];

		unit.CombatTargeting.ProtectTarget = best;
	}

	// 보호 대상에게 다가가는 접근 이동을 담당한다(02번 9항) — 사거리 안이면 개입하지 않고 false를 반환한다.
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
		// 사거리 안이어도 벽·닫힌 문이 직선을 막으면(차폐) 아직 보호할 수 있는 위치가 아니다 — 접근을 이어간다.
		if (AIMovementHelper.ChebyshevDistance(unit.position, protect.position) <= protectRange && SkillAction.HasClearLineTo(unit, protect)) return false;

		// 한 걸음도 못 다가가면(길이 완전히 막힘) 접근을 끝내고 일반 전투 판단으로 넘긴다 — 안 그러면 닿을 수 없는 아군 앞에서 전투 행동 없이 멈춘다.
		return AIMovementHelper.MoveTowardsProtectTargetWithRiskCheck(unit, protect);
	}

	// SkillAction_Heal.IsAvailable은 사거리 밖 대상을 이번 틱 후보에서만 빼고 HealTarget은 유지한다 — 여기서 유지된 대상을 다시 조회해 사거리 밖이면 접근 이동을 대신 실행한다.
	private static bool TryGeneralHealApproach(Unit unit)
	{
		SkillAction_Heal heal = FindHealSkill(unit);
		if (heal == null) return false;

		Unit ally = heal.FindLowestHpAlly(unit, 9999);
		if (ally == null || ally == unit) return false;
		// 직선 사거리 안이어도 차폐되면 아직 치료할 수 있는 위치가 아니다 — IsAvailable과 같은 기준(IsInSupportReach)으로 접근 종료를 판단한다.
		if (heal.IsInSupportReach(unit, ally)) return false;

		// 한 걸음도 못 다가가면 접근을 끝내고 일반 전투 판단으로 넘긴다(닿을 수 없는 아군 앞에서 전투 행동 없이 멈추지 않게).
		return AIMovementHelper.MoveTowardsPos(unit, ally.position);
	}

	private static SkillAction_Heal FindHealSkill(Unit unit)
	{
		if (unit.Generate == null) return null;
		foreach (var s in unit.Generate.GetSkills(unit.unitType.typeName))
			if (s is SkillAction_Heal heal) return heal;
		return null;
	}

	// 02번 8장: 후보가 긴급 보호 조건을 하나라도 만족하면 true와 9장 비교 키를 돌려준다.
	private static bool TryEvaluateEmergency(Unit protector, Unit candidate, out CombatScoreMath.ProtectCandidateKey key)
	{
		key = default;
		if (candidate?.Health == null || candidate.Health.hp <= 0) return false;
		float ratio = candidate.Health.hp / Mathf.Max(1f, candidate.Health.maxHp);
		bool incapacitated = candidate.StatusEffects.State.stunDuration > 0f;
		// candidate 자신의 인지(HasPerceivedThreatCollider) 대신 ground truth를 쓴다 — 사각지대·기습으로 본인이 공격자를 못 봐도 실제 위협이면 보호 후보다.
		bool underThreat = candidate.HasActiveThreatGroundTruth();
		bool lethal = TryFindLethalThreat(protector, candidate, out float lethalEta);
		if (!CombatScoreMath.IsEmergencyProtectCandidate(ratio, underThreat, incapacitated, candidate.isHitThisTurn, lethal)) return false;
		key = new CombatScoreMath.ProtectCandidateKey(lethal, lethalEta, ratio, incapacitated, Vector2Int.Distance(protector.position, candidate.position));
		return true;
	}

	// 02번 8장 둘째 조건: 보호자 개인의 예상 피해량(02번 9항)과 후보와 겹치는 진행 중 공격(위 ground truth 기준)으로 한 번의 공격이 후보의 남은 HP 이상인지 본다. 피해량을 모르면(보호자가 인류가 아니거나 기록 없음) 그 공격은 빼고 false.
	// 도달 시점 = 그 공격의 남은 시전 시간, 치명적인 공격이 여럿이면 가장 빠른 것.
	private static bool TryFindLethalThreat(Unit protector, Unit candidate, out float etaSeconds)
	{
		etaSeconds = 0f;
		GameSession session = protector.Session;
		if (!(protector is Human human) || human.Knowledge == null || session == null || session.castingUnits.Count == 0) return false;

		Hitbox candidateBox = Unit.GetUnitHitbox(candidate);
		bool found = false;
		foreach (Unit caster in session.castingUnits)
		{
			if (caster == null || caster == candidate || caster.hp <= 0 || caster.currentFloor != candidate.currentFloor || caster.unitType == null) continue;
			ThreatTileData threat = caster.AIState.currentThreat;
			if (threat == null || !candidate.IsEnemy(caster) || !threat.hitbox.Overlaps(candidateBox)) continue;

			string skillName = caster.CombatState.State.lastSkillName;
			string species = caster.unitType.typeName;
			if (string.IsNullOrEmpty(skillName) || !human.Knowledge.TryGetExpectedSkillDamage(human, species, skillName, out int rawDamage)) continue;

			SkillAction skill = null;
			var skills = protector.Generate?.GetSkills(species);
			if (skills != null)
				foreach (var s in skills) if (s != null && s.SkillName == skillName) { skill = s; break; }
			float defense = SkillAction.IsMagicalDamage(skill) ? candidate.CombatStat.magicalDefense : candidate.CombatStat.physicalDefense;
			if (!CombatScoreMath.IsLethalAttack(rawDamage, defense, candidate.Health.hp)) continue;

			float eta = Mathf.Max(0f, caster.CombatState.State.castTimer);
			if (!found || eta < etaSeconds) etaSeconds = eta;
			found = true;
		}
		return found;
	}

	// 02번 5장: 대상을 "바꾸려는" 이동에만 누적한다 — 원래(안 바뀐) 대상을 계속 추격하는 이동은 제외.
	private static void TrackRetargetMovement(Unit unit)
	{
		if (unit.CombatTargeting.IsChasingRetargetedEnemy) unit.CombatTargeting.MovedTilesSinceRetarget++;
	}

	// 대상까지 거리가 줄지 않는 상태가 계속되면(닫힌 상대 진영 문 등 도달 불가) 이 대상을 일정 시간 배제해 GetPriority/SelectAttackTarget이 건너뛰게 한다. Combat이 Tactical보다 우선이라 GetPriority 단계 배제까지 걸어야 Tactical(DoorAttack 등)로 넘어간다.
	private static void ChaseTarget(Unit unit, Unit target)
	{
		// 이미 파괴하기로 한 막힌 함정이 있으면 추격보다 그 파괴를 이어간다(교전이 시작되면 ExecuteCombat이 비운다).
		if (TryContinueTrapDestroy(unit)) return;

		int distBefore = AIMovementHelper.ChebyshevDistance(unit.position, target.position);
		bool moved = AIMovementHelper.MoveTowardsTarget(unit, target);
		if (AIMovementHelper.ChebyshevDistance(unit.position, target.position) < distBefore)
		{
			unit.combatChaseStuckTurns = 0;
		}
		else if (TryBeginTrapDestroy(unit))
		{
			// 함정이 유일한 통로를 막고 파괴할 수 있다 — 대상을 포기하지 않고 함정부터 부순다(03번 v0.12 9장 566줄).
			unit.combatChaseStuckTurns = 0;
		}
		else if (++unit.combatChaseStuckTurns >= (AIConfigLoader.Behavior?.combatChaseStuckTurnLimit ?? 4))
		{
			unit.combatChaseStuckTurns = 0;
			unit.combatUnreachableTarget = target;
			unit.combatUnreachableUntil = Time.time + (AIConfigLoader.Behavior?.combatChaseUnreachableCooldownSeconds ?? 6f);
			unit.CombatTargeting.AttackTarget = null;
		}
		if (moved) TrackRetargetMovement(unit);
	}

	// ── 전투 중 막힌 경로의 함정 파괴 ──
	// 전투 중 이동 경로를 막고 대체 경로가 없는 함정은 파괴 가능하면 파괴 대상이다(03번 9장). 추격이 진전 없이 끝났고 A*(DiagnoseTrapBlock)가 '함정 때문'이라고 남긴 신호가 있으면 그 함정이 후보이며, 실제 데미지는 UnitFunction.OnUpdate의 오브젝트 채널링이 적용한다.
	private static bool TryBeginTrapDestroy(Unit unit)
	{
		if (!(unit is Human human) || string.IsNullOrEmpty(human.trapBlockTrapId)) return false;
		string trapId = human.trapBlockTrapId;
		human.trapBlockTrapId = null; // 신호는 한 번만 소비한다
		if (Time.time - human.trapBlockSignalTime > 1f || human.Session == null) return false;

		if (!human.personalMap.KnownTrapTiles.TryGetValue(trapId, out Vector3Int tile)) return false;
		if (!human.Session.objectGrid.TryGetValue(tile, out InteractableObject trapObj) || trapObj.Id != trapId || trapObj.IsCollected) return false;
		// 파괴 가능 여부는 함정 속성에 따른다 — 모든 오브젝트를 파괴 가능한 것으로 취급하지 않는다(03번 v0.12 9장 584~586줄).
		if (trapObj.TrapMaxHp <= 0f || human.physicalAttack <= 0f) return false;

		human.CombatTargeting.BlockingTrapTile = tile;
		human.CombatTargeting.TrapDestroyStuckTurns = 0;
		return true;
	}

	private static bool TryContinueTrapDestroy(Unit unit)
	{
		Vector3Int? tile = unit.CombatTargeting.BlockingTrapTile;
		if (!tile.HasValue) return false;

		if (unit.Session == null || !unit.Session.objectGrid.TryGetValue(tile.Value, out InteractableObject trapObj) || trapObj.IsCollected || trapObj.TrapHp <= 0f)
		{
			ClearBlockingTrap(unit); // 이미 부서졌거나 사라짐 — 추격을 이어간다
			return false;
		}

		Vector2Int trapPos = new Vector2Int(tile.Value.x, tile.Value.y);
		if (AIMovementHelper.IsAdjacent(unit.position, trapPos))
		{
			unit.CombatTargeting.TrapDestroyStuckTurns = 0;
			unit.SetAttackObjectTarget(tile.Value); // 채널링 시작·유지 — 같은 값 재대입은 무해하다
			unit.currentDir = SkillAction.GetDirection8(trapPos - unit.position);
			unit.Generate?.UpdateUnitSpriteForDirection(unit);
			return true;
		}

		unit.ClearAttackObjectTarget();
		int distBefore = AIMovementHelper.ChebyshevDistance(unit.position, trapPos);
		AIMovementHelper.MoveTowardsPos(unit, trapPos);
		if (AIMovementHelper.ChebyshevDistance(unit.position, trapPos) < distBefore)
		{
			unit.CombatTargeting.TrapDestroyStuckTurns = 0;
		}
		else if (++unit.CombatTargeting.TrapDestroyStuckTurns >= (AIConfigLoader.Behavior?.combatChaseStuckTurnLimit ?? 4))
		{
			ClearBlockingTrap(unit); // 함정에 다가갈 수도 없다 — 포기하고 기존 도달 불가 처리로 넘긴다
			return false;
		}
		return true;
	}

	private static void ClearBlockingTrap(Unit unit)
	{
		var targeting = unit.CombatTargeting;
		if (!targeting.BlockingTrapTile.HasValue) return;
		if (unit.currentAttackObjectTarget == targeting.BlockingTrapTile) unit.ClearAttackObjectTarget();
		targeting.BlockingTrapTile = null;
		targeting.TrapDestroyStuckTurns = 0;
	}

	// internal — StandGroundAttackFSMState도 같은 타깃 선정을 재사용한다. 02번 3~5장: 개인위험도(인류)/종류설정(몬스터·야생) × 역할배율 × 대상종류배율로 점수를 매기고, 인류는 1.2배·몬스터/야생은 더 높을 때만 교체한다. 보스 집중은 위협이 없는 한 유지.
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
			if (unit.combatUnreachableTarget == e && Time.time < unit.combatUnreachableUntil) continue;
			allCandidates.Add(e);
			if (IsAttackingMeOrAlly(unit, e)) threatCandidates.Add(e);
		}
		if (allCandidates.Count == 0) return null;

		// 보스 집중 유지(4장) — 임시 대응 사유가 없고 보스 외 현재 위협도 없으면 20% 기준 없이 그대로 복귀한다.
		Unit boss = unit.CombatTargeting.BossFocusTarget;
		bool bossStillValid = boss != null && boss.Health.hp > 0 && boss.currentFloor == unit.currentFloor && allCandidates.Contains(boss);
		if (!bossStillValid) unit.CombatTargeting.BossFocusTarget = null;

		bool isHuman = unit is Human;
		CombatRole selfRole = CombatScoreMath.ResolveCombatRole(unit.unitType);

		if (bossStillValid)
		{
			// 임시 대응(02번 4장): 보스 집중(BossFocusTarget)은 든 채 그 적부터 상대한다. 일반 대상 변경이 아니라 20% 점수·이동 한도 게이트를 거치지 않는다(거치면 보스 점수 배율 1.5 때문에 교체가 안 된다). 사유가 사라지면 아래 보스 복귀 분기로 돌아간다.
			Unit temporary = SelectTemporaryResponseTarget(unit, boss, allCandidates, threatCandidates, isHuman, selfRole);
			if (temporary != null)
			{
				unit.CombatTargeting.AttackTarget = temporary;
				minDist = Vector2Int.Distance(unit.position, temporary.position);
				return temporary;
			}
		}

		bool hasOtherThreat = threatCandidates.Exists(t => t != boss);
		var pool = threatCandidates.Count > 0 ? threatCandidates : allCandidates;

		if (bossStillValid && !hasOtherThreat)
		{
			unit.CombatTargeting.AttackTarget = boss;
			minDist = Vector2Int.Distance(unit.position, boss.position);
			return boss;
		}

		// 점수·거리까지 완전 동점이면 하나만 무작위로 고른다(ThreatResponseMath.PickBest) — 이후 currentValid가 선택을 붙잡아 별도 기억 상태가 필요 없다. 최초 타이브레이크 기준인 '첫 공격 효과까지 시간'은 정밀 계산이 범위 밖이라 현재 거리로 근사한다.
		Unit best = ThreatResponseMath.PickBest(pool,
			cand => ComputeAttackScore(unit, isHuman, selfRole, cand),
			cand => Vector2Int.Distance(unit.position, cand.position),
			n => UnityEngine.Random.Range(0, n),
			out float bestScore, out float bestDist);

		// 유효성은 pool이 아니라 allCandidates로 확인한다 — pool(위협 우선)로 확인하면 현재 대상이 위협 후보가 아닐 때 다른 위협이 생기는 것만으로 '대상 없음'이 돼 20%/이동한도 게이트를 건너뛰고 교체되는 버그가 난다.
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
				// 남은 이동은 '새 대상을 공격할 수 있는 위치까지' 실제 경로 길이로 판단한다(이미 사거리 안이면 0칸, 02번 5장). 인류는 개인 지도로 경로가 드러났을 때만(fullyRevealed) 한도 충족을 확정하고, 몬스터·야생은 경로를 찾았는지만 확인한다. 미확인이면 교체를 보류한다(아래 폴백).
				// 한 걸음에 체비셰프 거리는 최대 1 줄어 (거리 − 공격 거리)가 필요한 걸음의 하한이므로, 이미 한도를 넘으면 A* 없이 보류한다(먼 후보에 매 틱 A*를 돌지 않게).
				int reach = MaxAttackReach(unit);
				int lowerBound = Mathf.Max(0, MovementMath.DistanceToFootprint(unit.position, best.position, best.FootprintSize) - reach);
				int pathRemaining = 0;
				bool fullyRevealed = false;
				bool pathConfirmed = unit.CombatTargeting.MovedTilesSinceRetarget + lowerBound <= limit
					&& TryEstimateStepsToAttackPosition(unit, best, reach, out pathRemaining, out fullyRevealed)
					&& (!isHuman || fullyRevealed);
				if (pathConfirmed && unit.CombatTargeting.MovedTilesSinceRetarget + pathRemaining <= limit)
				{
					unit.CombatTargeting.AttackTarget = best;
					unit.CombatTargeting.IsChasingRetargetedEnemy = true; // 누적 칸수는 유지(실행 시에만 초기화)
					if (CombatScoreMath.ResolveTargetCategory(best) == TargetCategory.Boss) unit.CombatTargeting.BossFocusTarget = best;
					minDist = bestDist;
					return best;
				}
				// 경로 미확인 또는 한도 초과 — 이번 교체는 보류하고 기존 대상을 유지한다(출발 위치로 자동 복귀하지 않음).
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

	// 4장 "자신을 직접 위협하는" — enemy의 현재 공격 대상이 바로 나다(아군을 공격 중인 적은 해당 안 됨: 문서는 다른 아군이 대응 중이면 보스 공격을 유지한다).
	private static bool IsAttackingMe(Unit observer, Unit enemy) => enemy.CombatTargeting.AttackTarget == observer;

	// 4장 임시 대응 대상: ① 자신을 직접 위협하는 잡몹(나를 공격 중인 보스 외 후보 중 점수 최고, 이미 그 집합 안의 대상을 상대 중이면 유지) ② 없으면 보스로 가는 길을 막는 적(BossPathBlockage). 긴급 보호는 ExecuteCombat이 별도로 먼저 처리한다. 아군만 공격받는 상황·단순 소환·먼 잡몹의 높은 점수는 사유가 아니며, null이면 호출부가 보스 집중을 유지한다.
	private static Unit SelectTemporaryResponseTarget(Unit unit, Unit boss, List<Unit> allCandidates, List<Unit> threatCandidates, bool isHuman, CombatRole selfRole)
	{
		var directThreats = new List<Unit>();
		foreach (var t in threatCandidates)
			if (t != boss && IsAttackingMe(unit, t)) directThreats.Add(t);

		if (directThreats.Count > 0)
			return ThreatResponseMath.SelectWithHysteresis(directThreats, unit.CombatTargeting.AttackTarget, isHuman,
				t => ComputeAttackScore(unit, isHuman, selfRole, t),
				t => Vector2Int.Distance(unit.position, t.position),
				n => UnityEngine.Random.Range(0, n),
				CombatScoreMath.ShouldSwitchAttackTarget);

		return FindPathBlockingEnemy(unit, boss, allCandidates);
	}

	// 이 유닛이 쓸 수 있는 적 대상 스킬 중 가장 먼 공격 거리(체비셰프, 최소 1) — "공격할 수 있는 위치"를 시야·각도·쿨다운 없이 거리만으로 근사한다.
	internal static int MaxAttackReach(Unit unit)
	{
		int reach = 1;
		var skills = unit.Generate?.GetSkills(unit.unitType.typeName);
		if (skills != null)
			foreach (var s in skills)
				if (s != null && s.Affinity == SkillAffinity.Enemy && s.HitRange > reach) reach = s.HitRange;
		return reach;
	}

	// 현재 위치에서 새 대상을 공격할 수 있는 위치까지의 실제 경로 걸음 수(02번 5장) — 이미 사거리 안이면 0. 대상 타일까지 A* 경로(다른 유닛 점유는 장애물, 대상 타일만 예외)를 따라가다 처음 사거리 안에 드는 지점에서 끊는다(CombatScoreMath.StepsToFirstTileWithinRange). fullyRevealed는 그 경로가 개인 지도에 모두 드러났는지(인류만), 경로를 못 찾으면 false.
	private static bool TryEstimateStepsToAttackPosition(Unit unit, Unit target, int reach, out int steps, out bool fullyRevealed)
	{
		steps = 0;
		fullyRevealed = false;

		// 공격 가능한 거리는 목표의 점유 영역 기준으로 잰다(보스 3×3 등 — 앵커 한 점 기준이면 영역 반대편에서 이미 공격 중인 유닛이 "이동 필요"로 오판된다).
		Vector2Int targetSize = target.FootprintSize;
		if (MovementMath.DistanceToFootprint(unit.position, target.position, targetSize) <= reach)
		{
			fullyRevealed = true; // 이동이 필요 없으니 확인할 경로도 없다
			return true;
		}

		if (!(unit.MovementAlgorithm is AStarMovement astar) || !astar.TryGetPathTiles(unit, target.position, out List<Vector2Int> tiles)) return false;

		steps = CombatScoreMath.StepsToFirstTileWithinRange(tiles, target.position, targetSize, reach);
		var human = unit as Human;
		fullyRevealed = human != null;
		for (int i = 0; i < steps && fullyRevealed; i++)
			if (!human.personalMap.IsTileRevealed(new Vector3Int(tiles[i].x, tiles[i].y, unit.currentFloor))) fullyRevealed = false;
		return true;
	}

	// 경로 차단 재확인 간격(초) — 판정이 A* 조회 2~3번이라 매 틱 돌리지 않고 이 간격마다만 다시 계산한다.
	private const float PathBlockCheckIntervalSeconds = 0.5f;

	// 보스로 가는 길을 막아 임시 대응해야 하는 확인된 적(02번 4장) — 판정은 BossPathBlockage(이동이 필요하고 우회할 수 없을 때 구조 경로 위에서 처음 만나는 적). 재계산 사이엔 마지막 결과를 재사용하되 그 적이 죽거나 후보에서 빠지면 버린다. 보스 집중 중이고 다른 위협이 없을 때만 호출된다.
	private static Unit FindPathBlockingEnemy(Unit unit, Unit boss, List<Unit> allCandidates)
	{
		var targeting = unit.CombatTargeting;
		if (Time.time < targeting.NextPathBlockCheckTime)
		{
			Unit cached = targeting.PathBlocker;
			return cached != null && cached.hp > 0 && allCandidates.Contains(cached) ? cached : null;
		}

		targeting.NextPathBlockCheckTime = Time.time + PathBlockCheckIntervalSeconds;
		targeting.PathBlocker = BossPathBlockage.FindBlockingEnemy(unit, boss, allCandidates, MaxAttackReach(unit));
		return targeting.PathBlocker;
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
