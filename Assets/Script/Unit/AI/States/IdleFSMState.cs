using UnityEngine;

// 대기 상태 — Navigation(10)보다 우선순위가 높아, 전투/전술 인지가 없고 방이 평시(오펜스/디펜스 아님)
// 일 때 자유탐색 대신 1칸 이동 후 정지를 반복한다(인류는 제외, 추후 별도 구현; 플레이어 진영은 명령
// 없는 몬스터만). 배회 기준점은 summonPosition(없으면 최초 진입 위치), 반경 2칸(AIBehaviorConfig.idleWanderRadius).
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
