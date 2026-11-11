// 플레이어 이동 명령 플래그 조합의 순수 판정 — Unit 프로퍼티와 FSM·이동 판정이 같은 규칙을 읽게 한다.
// 자동 방어 복귀(배치모드 1-6)는 수동 이동 명령(isManualMoveCommand)을 그대로 쓰되 isDefenseAutoReturn 표식으로 "전투·경계에 밀리는 이동"임을 구분한다.
public static class UnitCommandMath
{
	// 전투·경계·대기가 끼어들 수 없는 플레이어 이동(직접 이동 적용·우클릭 이동). 자동 방어 복귀는 제외한다.
	public static bool HasForcedMove(bool hasMoveTarget, bool isManualMove, bool isAutoDefenseReturn)
		=> hasMoveTarget && isManualMove && !isAutoDefenseReturn;

	// 방 이탈 제한(RoomConfinedMovement) 우회 여부 — 수동 이동이면 열리지만, 자동 복귀는 복귀 상태가 실제로 걷는 동안만이다.
	// 전투·경계 상태에서는 닫혀 있어야 전투 추격·소리 접근이 수동 플래그 때문에 방을 벗어나지 않는다.
	public static bool MayLeaveRoom(bool isManualMove, bool isAutoDefenseReturn, bool inDefenseReturnState)
		=> isManualMove && (!isAutoDefenseReturn || inDefenseReturnState);
}
