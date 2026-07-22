using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Haare.Util.Logger;

public class Action_Panic : GoapAction
{
	public Action_Panic() { ActionName = "Panic"; AddEffect("panicResolved", true); }
	public override int ActionCode => 1;

	public override bool IsValid(Unit unit) => unit is Human && unit.mental < unit.maxMental * 0.3f;

	public override void Execute(Unit unit)
	{
		if (Random.value > 0.5f)
		{
			Dir randomDir = (Dir)Random.Range(0, 8);
			unit.Move(randomDir);
			LogHelper.Log(LogHelper.GAME, $"{unit.unitType.typeName}가 공황에 빠져 무작위로 이동합니다.");
		}
		else
		{
			LogHelper.Log(LogHelper.GAME, $"{unit.unitType.typeName}가 공황에 빠져 멈춰있습니다.");
		}
	}
}

// Goal_PlayerCommand("이동 → 도착 시 대기 중인 상호작용 처리")를 두 스텝으로 나눈다 — 이동 자체와
// 도착 후 처리(루팅/조사 자동 트리거)는 서로 다른 서브골(atPlayerMoveTarget)로 체이닝된다. 웨이브
// 회수/퇴각(HumanWaveManager)도 이 경로를 그대로 타므로, 두 액션 다 사람 전용이 아니라 유닛 공통.
public class Action_MoveToPlayerTarget : GoapAction
{
	public Action_MoveToPlayerTarget() { ActionName = "MoveToPlayerTarget"; AddEffect("atPlayerMoveTarget", true); }
	public override int ActionCode => 2;

	public override bool IsValid(Unit unit) => unit.playerMoveTarget.HasValue;

	public override void Execute(Unit unit)
	{
		if (!unit.playerMoveTarget.HasValue) return;
		Vector2Int target = unit.playerMoveTarget.Value;
		if (unit.position != target) MoveTowardsPos(unit, target);
	}
}

public class Action_CompletePlayerCommand : GoapAction
{
	public Action_CompletePlayerCommand() { ActionName = "CompletePlayerCommand"; AddPrecondition("atPlayerMoveTarget", true); AddEffect("playerCommandExecuted", true); }
	public override int ActionCode => 3;

	public override bool IsValid(Unit unit) => unit.playerMoveTarget.HasValue;

	public override void Execute(Unit unit)
	{
		if (!unit.playerMoveTarget.HasValue) return;
		if (unit.position != unit.playerMoveTarget.Value) return; // Precondition이 이미 보장하지만 방어적으로 재확인

		unit.playerMoveTarget = null;
		unit.isManualMoveCommand = false;

		if (unit.playerInteractTarget.HasValue)
		{
			if (unit is Human human && unit.Session != null)
			{
				Vector3Int interactPos = unit.playerInteractTarget.Value;
				if (unit.Session.objectGrid.TryGetValue(interactPos, out InteractableObject obj) && !obj.IsCollected)
				{
					// 17장/19장: 조사→회수(루팅) 또는 확인(시체·흔적) 2단계를 도달 즉시 순서대로
					// 자동 처리한다 — 플레이어 조작은 지금처럼 한 번의 상호작용 그대로 두고,
					// 내부적으로만 "조사 완료(50%감소)" 이벤트를 먼저 찍은 뒤 "회수/확인 완료
					// (0%)"로 이어간다. Corpse/WipeoutTrace는 문서상 조사 단계 없이 확인 즉시
					// 0으로 가므로 조사 단계를 건너뛴다.
					// Tags는 "Object/Passable/Corpse"류 계층형 문자열이라 정확 일치(Contains(string))가 아니라
					// 부분 일치로 검사해야 한다(2026-07-20 발견 — 기존 정확 일치는 항상 false를 반환하는
					// 잠재 버그였다. HumanWaveManager의 "Loot" 판정은 이미 부분 일치를 쓰고 있었음).
					bool isTrace = obj.Tags.Any(t => t.Contains("Corpse")) || obj.Tags.Any(t => t.Contains("WipeoutTrace"));
					if (!isTrace && !obj.IsInvestigated)
					{
						human.personalMap.OnObjectInvestigated(obj.Id);
						obj.IsInvestigated = true;
					}

					unit.Session.CollectObject(interactPos);
					human.personalMap.OnObjectCollected(obj.Id);
					human.collectedObjects.Add(obj.Id);
				}
			}
			unit.playerInteractTarget = null;
		}
	}
}

public class Action_RandomExplore : GoapAction
{
	public Action_RandomExplore() { ActionName = "RandomExplore"; AddEffect("explored", true); }
	public override int ActionCode => 19;

	public override bool IsValid(Unit unit) => true;

	public override void Execute(Unit unit)
	{
		Dir randomDir = (Dir)Random.Range(0, 8);
		Vector2Int targetPos = unit.position + unit.GetDirVector(randomDir);

		if (unit.MovementAlgorithm != null)
		{
			if (unit.MovementAlgorithm.TryGetNextStep(unit, targetPos, out Dir nextDir))
			{
				unit.Move(nextDir);
			}
		}
		else
		{
			unit.Move(randomDir);
		}
	}
}

public class Action_EngageEnemy : GoapAction
{
	public Action_EngageEnemy()
	{
		ActionName = "EngageEnemy";
		AddPrecondition("enemyVisible", true);
		AddEffect("enemyAlive", false);
	}
	public override int ActionCode => 4;

	public override bool IsValid(Unit unit) => true;

	public override void Execute(Unit unit) => ExecuteSkillActionBased(unit);

	private void ExecuteSkillActionBased(Unit unit)
	{
		if (unit.isCastingAttack) return; // 캐스팅 중에는 새로운 행동 판단을 중지

		Unit target = GetClosestEnemy(unit, out float minDist);
		if (target == null) return;

		var unitSkills = unit.Generate != null ? unit.Generate.GetSkills(unit.unitType.typeName) : new System.Collections.Generic.List<SkillAction>();
		int engageSteps = unit.Generate != null ? unit.Generate.GetEngageDistance(unit.unitType.typeName, 2) : 2;

		// [사전 수치 계산] 유닛이 보유한 최대 스킬 사거리를 계산합니다.
		int maxSkillRange = 0;
		foreach (var s in unitSkills)
		{
			if (s != null && s.HitRange > maxSkillRange) maxSkillRange = s.HitRange;
		}
		
		Vector2Int diff     = target.position - unit.position;
		int        chebDist = Mathf.Max(Mathf.Abs(diff.x), Mathf.Abs(diff.y));

		unit.currentDir         = SkillAction.GetDirection8(diff);
		unit.currentAttackAngle = ((UnitFunction)unit).CalculateAttackAngleToEnemy(target, 1);

		// 1. 사용 가능한 최우선 스킬을 '가장 먼저' 탐색합니다. (스킬 사거리에 따라 이동 로직이 달라짐)
		SkillAction bestSkill    = null;
		float       bestPriority = float.MinValue;
		foreach (SkillAction skill in unitSkills)
		{
			if (skill == null || !skill.IsAvailable(unit)) continue;
			float priority = skill.GetPriority(unit, target, minDist);
			if (priority > bestPriority) { bestPriority = priority; bestSkill = skill; }
		}

		if (bestSkill != null)
		{
			// 1. 도주(Kiting) 조건 먼저 체크 (조준 여부와 상관없이 거리가 가까우면 우선 도주)
			if (bestSkill.HitRange >= 4)
			{
				int dangerDist = bestSkill.HitRange / 2;
				if (chebDist <= dangerDist && unit.evadeCooldown <= 0f)
				{
					MoveAwayFromTarget(unit, target, dangerDist + 1);
					// 이동 방향에 맞게 시각화 업데이트 진행
					unit.currentDir = SkillAction.GetDirection8(target.position - unit.position);
					unit.Generate?.UpdateUnitSpriteForDirection(unit);
					return; // 도망가는 중에는 스킬 사용 보류
				}
			}

			// 2. 선택된 스킬의 실제 사거리로 canHit 검사
			Hitbox skillBox = bestSkill.BuildSkillHitbox(unit);
			bool   canHit   = SkillAction.GetEnemiesInHitbox(unit, skillBox).Contains(target);

			if (canHit)
			{
				// 안전하거나 근접 스킬이라면 즉시 실행!
				bestSkill.Execute(unit, target, minDist);
				return;
			}

			// 3. 사거리 밖이거나 쏘는 각도가 안맞으면 다가갑니다 (접근하여 각도 맞추기)
			if (unit.evadeCooldown <= 0f)
			{
				MoveTowardsTarget(unit, target);
			}
		}
		else
		{
			// 모든 스킬이 쿨타임일 때 (공격 불가능한 상태)
			// 원거리 유닛은 안전거리(위험거리+1) 밖으로만 도망갑니다.
			int fallbackRange = maxSkillRange >= 4 ? maxSkillRange / 2 + 1 : 1;
			if (chebDist != fallbackRange && unit.evadeCooldown <= 0f)
			{
				if (chebDist < fallbackRange)
				{
					MoveAwayFromTarget(unit, target, fallbackRange);
				}
				else
				{
					MoveTowardsTarget(unit, target);
				}
			}
		}

		// 이동 후 방향 업데이트 (계속 적을 바라보게 함, 8방향)
		unit.currentDir = SkillAction.GetDirection8(target.position - unit.position);
		unit.Generate?.UpdateUnitSpriteForDirection(unit);
	}
}

// ════════════════════════════════════════════════════════════════════════
// 03_탐색반응·경계·조사·함정대응_시스템_v0.6 신규 Action들. 각 Action의 Cost/IsValid 설계 근거는
// 시야인지반응_03_GOAP목표우선순위표_2026-07-22.txt 8절 참고.
// ════════════════════════════════════════════════════════════════════════

// 9-4/9-8/9-9/9-10/9-11장 함정 대응 4종 — Cost 1<2<3<4 순서가 "해제→우회→통과→파괴" 문서 순서를
// 그대로 반영한다(8-1절). 4개 Action이 전부 Effects["trapHandled"]=true를 공유해도 안전한 이유는
// GoapCore.cs가 Effects를 "적용"하지 않고 "매칭"에만 쓰기 때문(8-0절).
// 9-4장 해제 경로를 3단계로 체이닝한다: (1)5초 join-wait 대기 (2)함정 위치로 이동 (3)실제 해제 판정.
// 원래 하나의 Execute() 안에 있던 "위치 안 맞으면 이동" 코드가 Action_MoveToTrap으로 분리됐고, 여전히
// 한 프레임에 한 스텝씩만 진행되므로 여러 틱에 걸친 진행(대기 타이머/이동/진행도바) 동작은 그대로다.
// Bypass/Pass/Destroy는 이 대기·이동 방식이 필요 없어(9-8/9-10/9-9장 — 각자 다른 위치·조건으로 접근)
// 분리하지 않고 예전처럼 단일 액션으로 둔다(trapHandled=true를 공유하는 건 그대로 8-0절 근거).
public class Action_TrapJoinWait : GoapAction
{
	public Action_TrapJoinWait() { ActionName = "TrapJoinWait"; AddEffect("trapJoinWaitElapsed", true); Cost = 0f; }
	public override int ActionCode => 5;

	public override bool IsValid(Unit unit) => IsDisarmWorthy(unit);

	// 9-3/9-4장: 미기록 함정은 항상 해제부터 시도, 기록 함정은 예상 성공률이 50% 초과할 때만 직접
	// 해제(그 외엔 Bypass/Pass/Destroy로 자연히 넘어간다). Action_MoveToTrap도 이 판정을 그대로
	// 재사용한다 — UnitFunction.OnUpdate가 GOAP 선택과 무관하게 배경에서 trapJoinWaitElapsed
	// 타이머를 계속 진행시키므로(기록 함정은 대기 없이 즉시 true), 이 게이트를 여기 한 곳에만 두면
	// 이미 시간이 지난 뒤엔 해제할 가치 없는 함정도 그냥 체이닝돼버리는 구멍이 생긴다.
	public static bool IsDisarmWorthy(Unit unit)
	{
		if (!(unit is Human human) || human.currentTrapInteraction == null) return false; // 13장: 해제=인류 전용
		var trap = human.currentTrapInteraction;
		if (human.Session == null || !human.Session.objectGrid.ContainsKey(trap.TrapPosition)) return false;

		if (!human.personalMap.IsTrapRecorded(trap.TrapObjectId)) return true;
		float expected = human.personalMap.GetTrapExpectedSuccessRate(trap.TrapObjectId);
		return expected > ExplorationMath.TrapRecordedDirectDisarmThreshold * 100f;
	}

	// 9-3장: 5초 대기 — 그 자리에서 기다린다(OnUpdate가 시간을 진행시켜 trap.JoinWaitElapsed를 채움).
	public override void Execute(Unit unit) { }
}

public class Action_MoveToTrap : GoapAction
{
	public Action_MoveToTrap() { ActionName = "MoveToTrap"; AddPrecondition("trapJoinWaitElapsed", true); AddEffect("atTrap", true); Cost = 0f; }
	public override int ActionCode => 6;

	public override bool IsValid(Unit unit) => Action_TrapJoinWait.IsDisarmWorthy(unit);

	public override void Execute(Unit unit)
	{
		var trap = unit.currentTrapInteraction;
		if (trap == null) return;
		Vector2Int trapPos = new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);
		if (unit.position != trapPos) MoveTowardsPos(unit, trapPos);
	}
}

public class Action_TrapDisarmPerform : GoapAction
{
	public Action_TrapDisarmPerform() { ActionName = "TrapDisarmPerform"; AddPrecondition("atTrap", true); AddEffect("trapHandled", true); Cost = 1f; }
	public override int ActionCode => 7;

	// atTrap/trapJoinWaitElapsed는 위치·타이머 기반 실제 상태라 GOAP 바깥 경로(플레이어 수동 이동이
	// 우연히 함정 칸을 지나가는 경우 등)로도 참이 될 수 있다 — "해제할 가치가 있는 함정인가"라는
	// 진짜 게이트는 그래서 체인 중간(Action_TrapJoinWait/MoveToTrap)뿐 아니라 실제 효과를 내는 이
	// 최종 액션에도 다시 걸어둔다.
	public override bool IsValid(Unit unit) => Action_TrapJoinWait.IsDisarmWorthy(unit);

	public override void Execute(Unit unit)
	{
		var human = (Human)unit;
		var trap = human.currentTrapInteraction;
		if (trap == null || !human.Session.objectGrid.TryGetValue(trap.TrapPosition, out var obj))
		{
			if (human.currentTrapInteraction != null) human.currentTrapInteraction = null;
			return;
		}

		trap.Phase = TrapPhase.Disarming;
		trap.PenaltyActive = true; // 9-6장: 해제 중 50% 페널티

		if (trap.DisarmProgress01 < 1f) return; // OnUpdate가 진행도를 계속 채운다

		// 9-2장: 해제 성공률 판정 — "이해도 보정"은 InteractableObject가 대표 가중치 시스템의
		// 종/개체 이해도 추적 대상(Unit)이 아니라서 지금은 반영하지 않는다(집중/레벨만 사용, 자리
		// 표시자 — ExplorationMath.TrapDisarmSuccessRate 주석 참고).
		float successRate = ExplorationMath.TrapDisarmSuccessRate(human.concentration, human.level, understandingApplied: 0);
		human.personalMap.RecordTrapAttempt(trap.TrapObjectId, successRate);

		bool success = Random.value * 100f < successRate;
		if (success)
		{
			// 함정_관련_요약.txt: E_TRAP_DISARM_SELF/SEEN이 weight_events.json에 이미 정의돼 있지만
			// HumanKnowledgeBase.RecordEvent(Unit target)는 Unit만 대상으로 받을 수 있어(target.
			// isSpecialUnit 등을 바로 읽음) InteractableObject인 함정을 target으로 넘길 방법이 아직
			// 없다 — 이번 범위에서는 이 갭을 그대로 남겨두고 호출하지 않는다(RecordEvent를 Unit이
			// 아닌 대상도 받도록 확장하는 건 가중치 시스템 쪽 후속 작업).
			LogHelper.Log(LogHelper.GAME, $"{human.unitType.typeName}가 함정을 해제했습니다.");
			human.Session.CollectObject(trap.TrapPosition);
			trap.PenaltyActive = false;
			human.currentTrapInteraction = null;
		}
		else
		{
			// 12-4장: 해제 실패 → 함정 결과 적용 후 재판단(구체 분기가 문서에 없어 재시도로 단순화).
			trap.DisarmProgress01 = 0f;
		}
	}
}

public class Action_TrapBypass : GoapAction
{
	public Action_TrapBypass() { ActionName = "TrapBypass"; AddEffect("trapHandled", true); Cost = 2f; }
	public override int ActionCode => 8;

	// 9-8장: 대체 경로 존재 여부를 실제 경로 탐색으로 검증하지는 않는다(정식 다중 경로 탐색이
	// 없음) — 해제 체인(TrapJoinWait/MoveToTrap/TrapDisarmPerform)이 유효하지 않을 때의 기본
	// 차선책으로 항상 시도 가능하게 둔다.
	// 근접 실패 시 Cost가 더 낮은 Disarm이 먼저 골라지므로 실질적으로 "해제 불가할 때만" 선택된다.
	public override bool IsValid(Unit unit) => unit is Human human && human.currentTrapInteraction != null;

	public override void Execute(Unit unit)
	{
		var human = (Human)unit;
		var trap = human.currentTrapInteraction;
		if (trap == null) return;

		// 함정 타일을 피해 옆 칸으로 한 걸음 비켜선 뒤 상태를 종료한다(정식 우회 경로 재계산은
		// 09_목표설정·이동경로·재설정 문서가 담당할 영역 — 이번 범위에서는 "밟지 않고 지나간다"는
		// 핵심 결과만 구현).
		Vector2Int trapPos = new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);
		Vector2Int away = human.position - trapPos;
		if (away == Vector2Int.zero) away = new Vector2Int(1, 0);
		Vector2Int sidePos = trapPos + new Vector2Int(System.Math.Sign(away.x) != 0 ? System.Math.Sign(away.x) : 1, System.Math.Sign(away.y));
		MoveTowardsPos(human, sidePos);

		human.currentTrapInteraction = null;
	}
}

public class Action_TrapPass : GoapAction
{
	public Action_TrapPass() { ActionName = "TrapPass"; AddEffect("trapHandled", true); Cost = 3f; }
	public override int ActionCode => 9;

	// 9-10/9-11장: 일반 통과(맞은 후 최소 HP 50%) 또는 아군 보호 완화 기준(맞은 후 30%, 미기록이면
	// 현재 HP 60% 이상) 중 하나를 만족하면 통과할 수 있다. "더 빠른 경로"/"우회 없음" 비교는 09
	// 문서(경로 재설정) 부재로 생략하고 "맞고 지나갈 수 있는가"만 확인한다.
	public override bool IsValid(Unit unit)
	{
		if (unit.currentTrapInteraction == null) return false; // 13장: 파괴/통과는 인류+몬스터 공통
		var trap = unit.currentTrapInteraction;
		if (unit.Session == null || !unit.Session.objectGrid.TryGetValue(trap.TrapPosition, out var obj)) return false;

		float hpAfter = unit.hp - obj.TrapDamageMax;
		bool normalSafe = hpAfter >= unit.maxHp * ExplorationMath.TrapPassMinHpRatioAfterHit;
		bool allyRescueSafe = false;
		if (unit is Human human)
		{
			bool recorded = human.personalMap.IsTrapRecorded(trap.TrapObjectId);
			allyRescueSafe = recorded
				? hpAfter >= unit.maxHp * ExplorationMath.TrapAllyRescueMinHpRatioAfterHit
				: unit.hp >= unit.maxHp * ExplorationMath.TrapAllyRescueUnrecordedMinCurrentHpRatio;
			// 9-11장 "즉시 보호 대상"(HP 30% 이하 아군이 근처에 있는지)까지는 확인하지 않는다 — 그
			// 대상을 찾는 정식 로직(파티 내 최근접 위기 아군 탐색)은 이번 범위 밖이라 완화 기준 자체는
			// 항상 열어두고 정상 기준(normalSafe)과 OR로만 묶는다.
		}
		return normalSafe || allyRescueSafe;
	}

	public override void Execute(Unit unit)
	{
		var trap = unit.currentTrapInteraction;
		if (trap == null || unit.Session == null || !unit.Session.objectGrid.TryGetValue(trap.TrapPosition, out var obj))
		{
			if (unit.currentTrapInteraction != null) unit.currentTrapInteraction = null;
			return;
		}

		Vector2Int trapPos = new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);
		if (unit.position != trapPos)
		{
			MoveTowardsPos(unit, trapPos);
			return;
		}

		// 9-10장: 예상 피해 오차 범위 중 최대값을 적용해 통과.
		unit.TakeDamage(obj.TrapDamageMax);
		LogHelper.Log(LogHelper.GAME, $"{unit.unitType.typeName}가 함정을 맞고 통과했습니다({obj.TrapDamageMax} 피해).");
		unit.currentTrapInteraction = null;
	}
}

public class Action_TrapDestroy : GoapAction
{
	public Action_TrapDestroy() { ActionName = "TrapDestroy"; AddEffect("trapHandled", true); Cost = 4f; }
	public override int ActionCode => 10;

	// 13장: 파괴는 인류+몬스터 공통. Pass(Cost 3)가 유효하면 항상 먼저 선택되므로(더 낮은 Cost),
	// Destroy는 통과도 위험할 만큼 피해가 큰 함정에서만 실질적으로 선택된다.
	public override bool IsValid(Unit unit) => unit.currentTrapInteraction != null;

	public override void Execute(Unit unit)
	{
		var trap = unit.currentTrapInteraction;
		if (trap == null || unit.Session == null || !unit.Session.objectGrid.TryGetValue(trap.TrapPosition, out var obj))
		{
			if (unit.currentTrapInteraction != null) unit.currentTrapInteraction = null;
			return;
		}

		Vector2Int trapPos = new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);
		if (Vector2Int.Distance(unit.position, trapPos) > 1.5f)
		{
			MoveTowardsPos(unit, trapPos);
			return;
		}

		trap.Phase = TrapPhase.Destroying; // OnUpdate가 매 프레임 obj.TrapHp를 깎는다(8-7절 간이 구현)

		if (obj.TrapHp > 0f) return;

		LogHelper.Log(LogHelper.GAME, $"{unit.unitType.typeName}가 함정을 파괴했습니다.");
		unit.Session.CollectObject(trap.TrapPosition);
		unit.currentTrapInteraction = null;
	}
}

// 5장 조사 — 5-3장 5조건에 갈림길이 없어 "대상 확보/이동" + "조사 수행" 2단계로 충분(각각
// Action_RandomExplore와 동일하게 IsValid는 항상 유효, 내부 상태 머신으로 진행). 대상 선택(없으면
// FindInvestigateTarget으로 새로 고름)은 이동 스텝에서 하고, 도착 후 실제 조사 판정/전파/회수는
// 별도 스텝에서 한다.
public class Action_MoveToInvestigateTarget : GoapAction
{
	public Action_MoveToInvestigateTarget() { ActionName = "MoveToInvestigateTarget"; AddEffect("atInvestigateTarget", true); Cost = 1f; }
	public override int ActionCode => 11;

	public override bool IsValid(Unit unit) => unit is Human;

	public override void Execute(Unit unit)
	{
		var human = (Human)unit;

		if (human.currentInvestigation == null)
		{
			var target = human.FindInvestigateTarget();
			if (target == null) return;
			human.currentInvestigation = new InvestigationState { TargetObjectId = target.Id, TargetPosition = target.Position };
		}

		var inv = human.currentInvestigation;
		if (!human.Session.objectGrid.TryGetValue(inv.TargetPosition, out var obj) || obj.IsCollected)
		{
			human.currentInvestigation = null; // 도중에 다른 유닛이 이미 처리(회수/파괴)함
			return;
		}

		Vector2Int targetPos = new Vector2Int(inv.TargetPosition.x, inv.TargetPosition.y);
		if (human.position != targetPos) MoveTowardsPos(human, targetPos);
	}
}

public class Action_InvestigatePerform : GoapAction
{
	public Action_InvestigatePerform() { ActionName = "InvestigatePerform"; AddPrecondition("atInvestigateTarget", true); AddEffect("objectInvestigated", true); Cost = 0f; }
	public override int ActionCode => 12;

	public override bool IsValid(Unit unit) => unit is Human human && human.currentInvestigation != null;

	public override void Execute(Unit unit)
	{
		var human = (Human)unit;
		var inv = human.currentInvestigation;
		if (inv == null) return;

		if (!human.Session.objectGrid.TryGetValue(inv.TargetPosition, out var obj) || obj.IsCollected)
		{
			human.currentInvestigation = null; // 도중에 다른 유닛이 이미 처리(회수/파괴)함
			return;
		}

		inv.PenaltyActive = true; // 5-4장: 조사 중 50% 페널티 — OnUpdate가 이 플래그를 보고 진행도를 채운다

		if (inv.Progress01 < 1f) return;

		// 5-5장: 조사 완료 → 정보 기록 + 파티 전파(07 문서 부재로 BroadcastWitnessEvent와 동일한
		// "그 순간 생존한 파티원 전원에게 직접 반영" 근사).
		obj.IsInvestigated = true;
		human.personalMap.OnObjectInvestigated(obj.Id);
		if (human.party != null)
		{
			foreach (var member in human.party.Members)
			{
				if (member == null || member == human || member.hp <= 0) continue;
				if (!member.personalMap.IsObjectKnown(obj.Id))
					member.personalMap.RegisterObject(obj.Id, obj.Position, obj.BaseDanger, obj.BaseInterest, obj.Tags, obj.CauserStage);
				member.personalMap.OnObjectInvestigated(obj.Id);
			}
		}

		// 5-1장 표: 회수 전용 오브젝트는 이 시점에 바로 회수까지 진행한다(Action_CompletePlayerCommand의
		// 기존 수동 처리와 동일한 관례 — Loot 태그는 회수 전용으로 취급).
		bool isLoot = false;
		foreach (var tag in obj.Tags) if (tag.Contains("Loot")) isLoot = true;
		if (isLoot)
		{
			human.Session.CollectObject(inv.TargetPosition);
			human.collectedObjects.Add(obj.Id);
		}

		inv.PenaltyActive = false;
		human.currentInvestigation = null;
	}
}

// 4장 경계 — 구체적 위치/방향이 있으면 접근(4-4/4-10장), 없으면 주변 수색(4-8/4-11장). Cost 동률
// (1)이라 IsValid의 상호 배타성만으로 정확히 하나가 선택된다(8-3절).
public class Action_AlertApproach : GoapAction
{
	public Action_AlertApproach() { ActionName = "AlertApproach"; AddEffect("alertResolved", true); Cost = 1f; }
	public override int ActionCode => 13;

	public override bool IsValid(Unit unit) => unit.currentAlertSearch != null && unit.currentAlertSearch.TargetPosition.HasValue;

	public override void Execute(Unit unit)
	{
		var alert = unit.currentAlertSearch;
		if (alert == null || !alert.TargetPosition.HasValue) return;

		Vector2Int target = alert.TargetPosition.Value;
		unit.currentDir = SkillAction.GetDirection8(target - unit.position); // "그 방향을 바라보고" 접근

		if (Vector2Int.Distance(unit.position, target) <= 1.5f)
		{
			// 4-5장: 2칸 이내 재인지는 기존 02문서 suspiciousReapproach 트리거가 이미 자동으로
			// 처리한다(ResolveReachedTarget) — 여기서는 도착으로 보고 경계를 종료한다. 이후 인지
			// 결과가 실제로 뭐였는지에 따라 다음 틱에 DefeatEnemy/TrapResponse/Investigate 중 맞는
			// 목표가 자연히 선택된다.
			unit.currentAlertSearch = null;
			return;
		}

		MoveTowardsPos(unit, target); // 4-3장 경계 이동속도(75%)는 이번 범위에서 속도 자체는 조정하지 않음(간이 구현)
	}
}

public class Action_AlertPerimeterSearch : GoapAction
{
	public Action_AlertPerimeterSearch() { ActionName = "AlertPerimeterSearch"; AddEffect("alertResolved", true); Cost = 1f; }
	public override int ActionCode => 14;

	public override bool IsValid(Unit unit) => unit.currentAlertSearch != null && !unit.currentAlertSearch.TargetPosition.HasValue;

	public override void Execute(Unit unit)
	{
		// 4-9장: "바라보는 정면 방향으로 이동하며 시야·인지 판정 반복" — 방향은 그대로 유지하고
		// 그 방향으로 한 걸음씩 이동한다(4-11장 포메이션 위치별 분산 방향 배정은 Goal_
		// ProtectiveFormation 연동이 더 갖춰진 뒤 확장할 영역, 8-3/8-4절 참고).
		Vector2Int forward = unit.GetDirVector(unit.currentDir);
		MoveTowardsPos(unit, unit.position + forward);
	}
}

// 10장 대기 — 분기 없이 항상 유효, 제자리에서 조건 해소를 기다린다.
public class Action_Wait : GoapAction
{
	public Action_Wait() { ActionName = "Wait"; AddEffect("waitConditionMet", true); Cost = 1f; }
	public override int ActionCode => 15;

	public override bool IsValid(Unit unit) => unit is Human human && human.currentWait != null;

	public override void Execute(Unit unit)
	{
		var human = (Human)unit;
		var wait = human.currentWait;
		if (wait == null) return;

		if (wait.Reason == WaitReason.AwaitingPartyAtRallyPoint && wait.WaitPosition.HasValue)
		{
			if (Vector2Int.Distance(human.position, wait.WaitPosition.Value) <= 1.5f)
			{
				human.currentWait = null; // 11장: 이 유닛 기준 집결 완료(전 파티원 동시 완료 판정은 간이 구현 — 8-4절 유사 단순화)
				return;
			}
			MoveTowardsPos(human, wait.WaitPosition.Value);
			return;
		}
		// AwaitingJoinBeforeApproach: 간접 인지 기반 적 위치 접근(4-7장)이 아직 없어(07/08 문서 부재)
		// 실제로 이 케이스를 만드는 트리거가 없다 — 그 문서가 생기면 이 분기에 이동 로직을 채운다.
	}
}

// 6장 보호 포메이션 — 근접/원거리 배치 분기(6-4/6-5장)로 이동한 뒤, 도착하면 상호작용 유닛 쪽을
// 바라본다(6-6장 간이화, 시야 방향 세부 배정은 8-5-1절이 예고한 FormationMath 모듈로 나중에 대체할
// 자리). 이동 목적지가 매 틱 escort 대상 위치에 따라 바뀌므로 "도착 유지"가 오래 지속되진 않지만,
// 그때그때 재계획되며 계속 쫓아가는 동작 자체는 예전과 동일하다.
public class Action_MoveToEscortSlotMelee : GoapAction
{
	public Action_MoveToEscortSlotMelee() { ActionName = "MoveToEscortSlotMelee"; AddEffect("atEscortSlot", true); Cost = 0f; }
	public override int ActionCode => 16;
	public override bool IsValid(Unit unit) => unit is Human human && !human.IsRangedFormationRole();
	public override void Execute(Unit unit) => MoveToEscortSlot((Human)unit, backDistance: 1f);
}

public class Action_MoveToEscortSlotRanged : GoapAction
{
	public Action_MoveToEscortSlotRanged() { ActionName = "MoveToEscortSlotRanged"; AddEffect("atEscortSlot", true); Cost = 0f; }
	public override int ActionCode => 17;
	public override bool IsValid(Unit unit) => unit is Human human && human.IsRangedFormationRole();
	public override void Execute(Unit unit) => MoveToEscortSlot((Human)unit, backDistance: ExplorationMath.FormationRangedMinBackDistance);
}

public class Action_HoldFormation : GoapAction
{
	public Action_HoldFormation() { ActionName = "HoldFormation"; AddPrecondition("atEscortSlot", true); AddEffect("formationHeld", true); Cost = 1f; }
	public override int ActionCode => 18;
	public override bool IsValid(Unit unit) => unit is Human human && human.currentFormation != null && human.currentFormation.EscortTarget != null;

	public override void Execute(Unit unit)
	{
		var human = (Human)unit;
		var escortTarget = human.currentFormation?.EscortTarget;
		if (escortTarget == null) return;
		human.currentDir = SkillAction.GetDirection8(escortTarget.position - human.position); // 6-6장 간이화
	}
}
