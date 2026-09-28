using UnityEngine;

// 대기 상태 — Navigation(10)보다 우선순위가 높아, 전투/전술 인지가 없을 때 자유탐색 대신 1칸 이동
// 후 정지를 반복한다(인류는 제외, 추후 별도 구현; 플레이어 진영은 명령 없는 몬스터만). 배회 기준점은
// summonPosition(없으면 최초 진입 위치), 반경 2칸(AIBehaviorConfig.idleWanderRadius).
//
// 검증문서 03-06(03번 문서 5번 항목 "상태·진영" 표): 오펜스는 "현재 위치에서 기다리며", 디펜스는
// "지정 방어 타일로 복귀"(그 기반이었던 R키 배치모드 defenseStartPosition은 2026-08-22 삭제 —
// CLAUDE.md 참고, 이제 근사할 수 있는 가장 가까운 대체가 이 Idle의 제자리 배회다). 둘 다 "대기"가
// 핵심이라, 오펜스·디펜스 중인 방에서 오히려 이 상태를 꺼서 Navigation(자유탐색으로 그 방 전체를
// 돌아다님)으로 넘기던 이전 로직은 원문과 정반대였다(2026-09-29 발견+수정, 사용자 확인) — 원래
// 있던 OffenseProcessor/DefenseProcessor 게이트를 제거해 오펜스·디펜스 여부와 무관하게 평시와
// 동일하게 Idle을 유지한다. "소리·적 자동 반응을 끄는 의미가 아니다"는 요구는 Idle의 우선순위(20)가
// Combat(100)·Tactical(50)보다 낮아 그쪽 조건이 걸리면 자동으로 밀리는 기존 구조로 이미 충족된다.
public class IdleFSMState : IFSMState
{
	public float GetPriority(Unit unit)
	{
		if (unit is Human) return 0f; // 인류 대기 상태는 추후 별도 구현(지금은 제외)

		bool isWild = unit.FactionBehavior is WildMonsterBehavior;
		bool hasPlayerCommand = (unit.playerMoveTarget.HasValue && unit.isManualMoveCommand)
			|| (unit.playerAttackTarget != null && unit.playerAttackTarget.hp > 0)
			|| unit.playerAttackObjectTarget.HasValue; // 코어/문 공격 명령도 동일 취급.
		bool isIdlePlayerMonster = unit.IsPlayerMonsterFaction && !hasPlayerCommand;
		if (!isWild && !isIdlePlayerMonster) return 0f;

		return AIConfigLoader.Behavior?.idlePriority ?? 20f;
	}

	public bool IsSticky(Unit unit)        => false;
	public bool ShouldInterrupt(Unit unit) => true;

	public void OnEnter(Unit unit)
	{
		unit.idleAnchorPosition = ResolveAnchor(unit);
		unit.idleNextMoveTime = 0f; // 진입 직후 첫 이동은 곧바로 허용
	}

	public void OnExit(Unit unit)
	{
		unit.idleAnchorPosition = null;
	}

	public BTStatus Tick(Unit unit)
	{
		if (Time.time < unit.idleNextMoveTime) return BTStatus.Running;

		Vector2Int anchor = unit.idleAnchorPosition ?? unit.position;
		int radius = AIConfigLoader.Behavior?.idleWanderRadius ?? 2;
		// forceRoomConfine: true — 대기 배회는 MovementAlgorithm이 RoomConfinedMovement가 아니어도
		// 무조건 현재 방 안 + 문 타일 제외로 제한한다(방 밖으로 나가면 안 됨).
		AIMovementHelper.TryMoveRandomlyWithinRadius(unit, anchor, radius, forceRoomConfine: true);

		float min = AIConfigLoader.Behavior?.idlePauseMinSeconds ?? 2f;
		float max = AIConfigLoader.Behavior?.idlePauseMaxSeconds ?? 4f;
		if (max < min) max = min;
		unit.idleNextMoveTime = Time.time + Random.Range(min, max);
		return BTStatus.Running;
	}

	public string GetLabel(Unit unit) => "대기";

	private static Vector2Int ResolveAnchor(Unit unit)
	{
		if (unit.summonPosition.HasValue) return unit.summonPosition.Value;
		return unit.position;
	}
}
