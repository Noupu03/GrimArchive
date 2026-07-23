using System.Collections.Generic;

public class Goal_Panic : GoapGoal
{
	public Goal_Panic() { Name = "Panic"; DesiredState["panicResolved"] = true; }

	public override float GetPriority(Unit unit)
	{
		if (unit is Human && unit.mental < unit.maxMental * 0.3f) return 150f;
		return 0f;
	}
}

public class Goal_PlayerCommand : GoapGoal
{
	public Goal_PlayerCommand() { Name = "PlayerCommand"; DesiredState["playerCommandExecuted"] = true; }

	public override float GetPriority(Unit unit)
	{
		if (unit.playerAttackTarget != null) return 130f; // 수동 공격 명령은 최우선 (패닉 제외)
		if (unit.playerMoveTarget.HasValue)
		{
			if (unit.isManualMoveCommand) return 130f; // 수동 이동 명령도 최우선
			// 자동 이동 명령(웨이브 회수/퇴각, HumanWaveManager 전용 경로 — InputManager는 항상
			// isManualMoveCommand=true로 세팅) — 전투(100f) 바로 아래로 둬서 전투만 이걸 가로챌 수
			// 있게 한다. 원래 90f였는데 Alert(97)/Investigate(94, 고착)보다 낮아서 웨이브 회수/퇴각
			// 중에도 유닛이 알던 미조사 오브젝트가 하나라도 남아있으면 조사로 새버리고 다시는 돌아오지
			// 않는 버그가 있었다(2026-07-22, 사용자 신고 "오브젝트 수집 후 멈춤"/"퇴각 로직 고장" —
			// 시야인지반응_구현현황_2026-07-22.txt 참고). 문서 §7 표에 실제로 90으로 명시돼 있었지만,
			// 이건 이전 세션이 웨이브 회수/퇴각이 이 경로를 탄다는 걸 모르고 정한 값이라 사용자 확인
			// 후 99로 올림.
			return 99f;
		}
		return 0f;
	}
}

// 웨이브 유닛이 계단을 통해 다른 층으로 넘어가야 할 때(Unit.pendingStairTargetFloor) 최우선으로
// 계단을 찾아 이동/통과시킨다(사용자 요청, 2026-07-23 "0층에서 웨이브 시작후, 인류들의 최우선 목표는
// 계단을 통해 1층으로 이동하는거야. 예외처리 없이 goap로직에 넣어도 되겠군"). Panic(150) 다음으로
// 높게 잡아서 전투/탐색/자동이동 명령보다 우선한다 — 패닉 상태만 계단 이동을 가로챌 수 있다.
public class Goal_UseStairs : GoapGoal
{
	public Goal_UseStairs() { Name = "UseStairs"; DesiredState["onTargetFloor"] = true; }

	public override float GetPriority(Unit unit)
	{
		if (unit.pendingStairTargetFloor.HasValue && unit.pendingStairTargetFloor.Value != unit.currentFloor)
			return 140f;
		return 0f;
	}
}

public class Goal_DefeatEnemy : GoapGoal
{
	public Goal_DefeatEnemy() { Name = "DefeatEnemy"; DesiredState["enemyAlive"] = false; }

	public override float GetPriority(Unit unit)
	{
		// 인간 진영도 몬스터와 동일하게 개인 시야(personalSpottedEnemies)만 사용 — 진영 공유 시야 제거.
		IEnumerable<Unit> enemies = unit.personalSpottedEnemies;

		foreach (var enemy in enemies)
		{
			if (enemy != null && enemy.hp > 0 && enemy.currentFloor == unit.currentFloor)
				return 100f; // 자동 이동 명령(99f)보다 우선순위가 높아 이동 중 전투 발생
		}
		return 0f;
	}
}

public class Goal_Explore : GoapGoal
{
	public Goal_Explore() { Name = "Explore"; DesiredState["explored"] = true; }

	public override float GetPriority(Unit unit) => 10f;
}

// ════════════════════════════════════════════════════════════════════════
// 03_탐색반응·경계·조사·함정대응_시스템_v0.6 신규 목표 5종. 우선순위 숫자/고착 여부의 전체 근거는
// 시야인지반응_03_GOAP목표우선순위표_2026-07-22.txt(설계) 참고 — 이 파일은 그 설계를 그대로 옮긴다.
// ════════════════════════════════════════════════════════════════════════

public class Goal_TrapResponse : GoapGoal
{
	public Goal_TrapResponse() { Name = "TrapResponse"; DesiredState["trapHandled"] = true; }
	public override bool IsSticky(Unit unit) => true;

	public override float GetPriority(Unit unit)
	{
		// 원래 13장 주석대로 "파괴/통과는 인류+몬스터 공통"이었는데, 몬스터는 Bypass/Disarm이 애초에
		// Human 전용이라 TrapResponse가 걸리면 선택지가 Pass/Destroy(둘 다 함정을 소모/제거)밖에 없다
		// — 그 결과 몬스터가 함정을 인지하는 즉시 최우선(140, 고착)으로 달려가 부수거나 맞고 지나가며
		// 함정을 없애버리는 게 사실상 강제됐다. "함정이 이유 없이 계속 사라진다"는 사용자 신고
		// (2026-07-22)의 원인 — 몬스터 전용 함정 대응(회피/역이용 등)은 아직 컨셉조차 없으므로
		// (CLAUDE.md "13장 표, 몬스터는 컨셉에 따라만 명시") 이번엔 인류 전용으로 좁혔다. 몬스터가
		// 우연히 함정을 밟는 경우는 여전히 GameSession.TriggerTrapIfStepped 자동 트리거로 피해를 입고
		// 소모되지만, 일부러 찾아가 없애는 일은 더 이상 없다.
		if (!(unit is Human)) return 0f;
		if (unit.currentTrapInteraction == null) return 0f;
		// 9-7장: 적을 정확 인지하면 함정 대응은 즉시 양보하고 DefeatEnemy(100)에 자연히 밀린다.
		if (unit.personalSpottedEnemies.Count > 0) return 0f;
		return 140f;
	}

	// 9-7장 "함정 해제 중단과 진행도" 목록 — 하나라도 참이면 고착을 풀고 전체 재평가로 넘어간다.
	public override bool ShouldInterrupt(Unit unit)
	{
		if (GetPriority(unit) <= 0f) return true;               // 함정 사라짐/적 발견 등 자체 소멸
		if (unit.isHitThisTurn) return true;                     // "해제 유닛이 공격받음"
		if (unit.HasPerceivedThreatCollider()) return true;      // "위협 콜라이더를 인지함"
		if (unit is Human human && human.AnyEscortHitThisTurn()) return true; // 8-2장: 비목표 보호 유닛 피격
		return false;
	}
}

public class Goal_Alert : GoapGoal
{
	public Goal_Alert() { Name = "Alert"; DesiredState["alertResolved"] = true; }
	// 고착 아님(기본값 false 그대로) — 03문서가 명시적으로 고착을 요구하는 목표는 TrapResponse/
	// Investigate/Wait 셋뿐이다(시야인지반응_03_GOAP목표우선순위표_2026-07-22.txt 0-1절 마지막 항목).

	public override float GetPriority(Unit unit)
	{
		if (unit.currentAlertSearch == null) return 0f;
		// 원래 97(Investigate 94/Wait 91보다 위)이었는데, 전투가 끝나자마자 "정면 주시" 경계 스윕이
		// 곧바로 끼어들어서 부자연스럽게 멈춰버린다는 사용자 피드백(2026-07-22)에 따라 90으로 내렸다
		// — Investigate/Wait처럼 더 확실한 목표가 있으면 그쪽을 먼저 계속하고, 경계는 정말 아무 할 일도
		// 없을 때만(Explore 10보다는 위) 나선다.
		return 90f;
	}
}

public class Goal_Investigate : GoapGoal
{
	public Goal_Investigate() { Name = "Investigate"; DesiredState["objectInvestigated"] = true; }
	public override bool IsSticky(Unit unit) => true;

	public override float GetPriority(Unit unit)
	{
		if (!(unit is Human human)) return 0f;
		if (human.playerAttackTarget != null || (human.playerMoveTarget.HasValue && human.isManualMoveCommand)) return 0f; // 수동 명령 존중
		if (human.currentInvestigation == null && !human.HasReachableInvestigateTarget()) return 0f;
		return 94f;
	}

	// 5-6장 "조사 중단과 진행도" 목록 + 8-2장(비목표 보호 유닛 피격).
	public override bool ShouldInterrupt(Unit unit)
	{
		if (!(unit is Human human)) return true;
		if (unit.isHitThisTurn) return true;                     // "상호작용 유닛이 공격받음"
		if (unit.personalSpottedEnemies.Count > 0) return true;  // "상호작용 유닛이 적을 정확 인지함"
		if (unit.HasPerceivedThreatCollider()) return true;      // "위협 콜라이더를 인지함"
		if (human.AnyEscortHitThisTurn()) return true;           // 8-2장: 비목표 보호 유닛 피격
		return false;
	}
}

public class Goal_Wait : GoapGoal
{
	public Goal_Wait() { Name = "Wait"; DesiredState["waitConditionMet"] = true; }
	public override bool IsSticky(Unit unit) => true;

	public override float GetPriority(Unit unit)
	{
		if (!(unit is Human human)) return 0f;
		if (human.playerAttackTarget != null || (human.playerMoveTarget.HasValue && human.isManualMoveCommand)) return 0f;
		if (human.currentWait == null) return 0f;
		return 91f;
	}

	// 10장은 "수상한 타일 확인해도 대기 유지"만 명시한다 — 적 정확 인지/피격까지 무시하라는 문장은
	// 없지만, 다른 고착 목표와 동일한 최소 자기 보호 조건을 기본값으로 둔다(가정 — 시야인지반응_03_
	// GOAP목표우선순위표_2026-07-22.txt 3-4절에 명시된 지점, 실 구현 시 재확인 권장).
	public override bool ShouldInterrupt(Unit unit)
	{
		if (!(unit is Human human) || human.currentWait == null) return true;
		if (unit.isHitThisTurn) return true;
		if (unit.personalSpottedEnemies.Count > 0) return true;
		return false;
	}
}

public class Goal_ProtectiveFormation : GoapGoal
{
	public Goal_ProtectiveFormation() { Name = "ProtectiveFormation"; DesiredState["formationHeld"] = true; }
	// 6-2장의 "기존 포메이션 유지"는 GetPriority 자체가 currentFormation.EscortTarget을 우선
	// 확인하는 방식으로 처리한다(고착 플래그 불필요, 시야인지반응_03_GOAP목표우선순위표_2026-07-22.txt
	// 3-5절 근거) — 20점은 어차피 다른 모든 목표보다 낮아 자연히 밀리므로 별도 보호가 필요 없다.

	public override float GetPriority(Unit unit)
	{
		// GoapWorldState.Build의 formationHeld 판정과 같은 헬퍼를 공유한다(0-1절 "공유 원칙").
		if (!(unit is Human human)) return 0f;
		return human.HasProtectiveFormationNeed() ? 20f : 0f;
	}
}
