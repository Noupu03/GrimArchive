using UnityEngine;

// 대기 상태(2026-08-20 신규, 사용자 요청 "대기 상태를 새로 만들어줘... 인류 / 플레이어 진영 / 야생
// 진영에서 오펜스, 디펜스 중이 아닌 방의 경우 유닛은 연속 이동이 아닌, 1칸 이동 후 정지(일정 시간)를
// 반복한다") — Muster(30)와 Navigation(10) 사이에 자리해, 전투/전술 인지·소집이 없고 방이 평시(오펜스도
// 디펜스도 아님)일 때 Navigation의 "자유탐색"(매 액션 주기마다 연속으로 1칸씩 이동)을 대신한다.
//
// 범위(사용자 확인, 2026-08-20): 인류는 제외 — "인류의 대기상태는 추후 인류 로직 개선 후 진행. 일단
// 플레이어 몬스터와 야생에만 구현하자." 플레이어 진영은 "플레이어의 행동 명령이 없는 몬스터만" 해당
// (PlayerCommandFSMState가 이미 UnitFSM._states 진입 전에 최우선으로 가로채므로 이 상태까지 내려오는
// 시점엔 이미 명령이 없는 상태가 보장되지만, 의도를 코드로도 명시하기 위해 아래서 다시 확인한다).
//
// 배회 기준점(사용자 확인, 2026-08-20): summonPosition(스폰 위치)이 없으면 이 상태에 처음 진입한
// 위치를 그 자리에서 기준점으로 삼는다. 반경은 2칸(AIBehaviorConfig.idleWanderRadius, 사용자 선택
// "야생과 동일하게 anchor+2칸 반경").
public class IdleFSMState : IFSMState
{
	public float GetPriority(Unit unit)
	{
		if (unit is Human) return 0f; // 인류 대기 상태는 추후 별도 구현(사용자 명시, 지금은 제외)

		bool isWild = unit.FactionBehavior is WildMonsterBehavior;
		bool hasPlayerCommand = (unit.playerMoveTarget.HasValue && unit.isManualMoveCommand)
			|| (unit.playerAttackTarget != null && unit.playerAttackTarget.hp > 0)
			|| unit.playerAttackObjectTarget.HasValue; // 기초문서.md 피드백(2026-08-22) — 코어/문 공격 명령도 동일 취급.
		bool isIdlePlayerMonster = unit.IsPlayerMonsterFaction && !hasPlayerCommand;
		if (!isWild && !isIdlePlayerMonster) return 0f;

		if (unit.Session?.roomGrid == null) return 0f;
		if (!unit.Session.roomGrid.TryGetValue(new Vector3Int(unit.position.x, unit.position.y, unit.currentFloor), out Room room) || room == null)
			return 0f;

		if (unit.Session.OffenseProcessor != null && unit.Session.OffenseProcessor.IsRoomInOffense(room)) return 0f;
		if (unit.Session.DefenseProcessor != null && unit.Session.DefenseProcessor.IsRoomInDefense(room)) return 0f;

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
		// forceRoomConfine: true — 사용자 요청 "대기로 인한 이동 중일때는, 방 밖으로 나가면 안됨(문이
		// 있는 타일도 안됨)". 이 유닛의 MovementAlgorithm이 우연히 RoomConfinedMovement가 아니어도
		// 대기 배회만큼은 무조건 현재 방 안 + 문 타일 제외로 제한한다.
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
