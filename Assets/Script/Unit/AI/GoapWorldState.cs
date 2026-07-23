using UnityEngine;

// GoapPlanner가 실제로 액션을 체이닝하려면 각 목표의 DesiredState 키(trapHandled/objectInvestigated/
// alertResolved/waitConditionMet/formationHeld/playerCommandExecuted/panicResolved/explored/enemyAlive)와
// 새로 도입한 위치 기반 서브골(atTrap/atInvestigateTarget/atEscortSlot/atPlayerMoveTarget,
// trapJoinWaitElapsed)이 전부 하나의 GoapState 안에 있어야 한다(예전엔 enemyVisible/isHit 2개뿐이라
// 대부분의 Precondition이 사실상 비어있었다). Unit/Human의 기존 필드를 그대로 읽기만 하는 순수 함수 —
// 새 상태 필드는 추가하지 않는다.
public static class GoapWorldState
{
	// 계단 도착 판정 반경(체비쇼프 거리, 타일 단위) — 웨이브 유닛 여러 명이 정확히 같은 한 칸을
	// 목표로 걸으면, 먼저 도착한 유닛이 그 칸을 점유해버려 나머지가 영영 못 들어가 계단 바로 앞에서
	// 멈춰버리는 문제가 있었다(사용자 신고 "2층으로 이동 안 하는데", 2026-07-23 — AStarMovement가
	// 점유된 타일은 완전히 막기 때문). 정확히 그 칸이 아니라 계단 주변까지 넉넉히 "도착"으로 인정해서
	// 병목을 없앤다. Actions.cs의 Action_CrossStairs도 같은 상수를 참조한다.
	public const int StairArrivalRadius = 1;

	// TryGetStairPosition이 돌려주는 stairPos는 계단 2x2 블록의 좌상단 "점 하나"다. atStairs를 그
	// 점 하나까지의 거리로만 재면, TryGetStairApproachCandidates가 돌려주는 12개 후보 타일 중
	// 블록의 반대쪽 변에 붙어있는 것들은 실제로는 블록 바로 옆(거리 1)인데도 점 기준으로는 거리
	// 2로 계산돼 "반경 1 밖"으로 잘못 판정되는 불일치가 있었다(사용자 신고, 2026-07-23 "근방
	// 12타일 모두 위층으로 이동하는 대상으로 두라고 — 이동은 하는데 다음 층으로 못가"). 점이
	// 아니라 블록(2x2 영역) 자체까지의 체비쇼프 거리로 재서 12개 후보 전부가 정확히 거리 1로
	// 계산되게 한다. Actions.cs의 Action_CrossStairs도 이 헬퍼를 그대로 쓴다.
	public static int DistanceToStairBlock(Vector2Int pos, Vector2Int stairBlockTopLeft)
	{
		int dx = Mathf.Max(Mathf.Max(stairBlockTopLeft.x - pos.x, 0), pos.x - (stairBlockTopLeft.x + 1));
		int dy = Mathf.Max(Mathf.Max(stairBlockTopLeft.y - pos.y, 0), pos.y - (stairBlockTopLeft.y + 1));
		return Mathf.Max(dx, dy);
	}

	public static GoapState Build(Unit unit)
	{
		var state = new GoapState();

		bool enemyVisible = false;
		foreach (var e in unit.personalSpottedEnemies)
		{
			if (e != null && e.hp > 0 && e.currentFloor == unit.currentFloor) { enemyVisible = true; break; }
		}
		state["enemyVisible"] = enemyVisible;
		state["enemyAlive"]   = enemyVisible; // Goal_DefeatEnemy.DesiredState는 enemyAlive=false를 원함
		state["isHit"]        = unit.isHitThisTurn;

		state["panicResolved"] = !(unit is Human && unit.mental < unit.maxMental * 0.3f);

		var trap = unit.currentTrapInteraction;
		state["trapHandled"]         = trap == null;
		state["trapJoinWaitElapsed"] = trap != null && trap.JoinWaitElapsed;
		state["atTrap"] = trap != null && unit.position == new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);

		state["alertResolved"] = unit.currentAlertSearch == null;

		bool investigateNeeded = unit is Human ih && (ih.currentInvestigation != null || ih.HasReachableInvestigateTarget());
		state["objectInvestigated"] = !investigateNeeded;
		state["atInvestigateTarget"] = unit is Human aih && aih.currentInvestigation != null
			&& unit.position == new Vector2Int(aih.currentInvestigation.TargetPosition.x, aih.currentInvestigation.TargetPosition.y);

		state["waitConditionMet"] = !(unit is Human wh && wh.currentWait != null);

		state["formationHeld"] = !(unit is Human fh && fh.HasProtectiveFormationNeed());

		bool atEscortSlot = false;
		if (unit is Human afh && afh.currentFormation != null && afh.currentFormation.EscortTarget != null)
		{
			float backDistance = afh.IsRangedFormationRole() ? ExplorationMath.FormationRangedMinBackDistance : 1f;
			Vector2Int slot = afh.GetEscortSlotPosition(afh.currentFormation.EscortTarget, backDistance);
			atEscortSlot = unit.position == slot;
		}
		state["atEscortSlot"] = atEscortSlot;

		state["playerCommandExecuted"] = !unit.playerMoveTarget.HasValue;
		state["atPlayerMoveTarget"] = unit.playerMoveTarget.HasValue && unit.position == unit.playerMoveTarget.Value;

		bool atStairs = false;
		bool onTargetFloor = true;
		if (unit.pendingStairTargetFloor.HasValue)
		{
			int targetFloor = unit.pendingStairTargetFloor.Value;
			onTargetFloor = targetFloor == unit.currentFloor;
			if (!onTargetFloor && unit.Session != null && unit.Session.cmap != null &&
				unit.Session.cmap.TryGetStairPosition(unit.currentFloor, targetFloor, out Vector2Int stairPos))
			{
				atStairs = DistanceToStairBlock(unit.position, stairPos) <= StairArrivalRadius;
			}
		}
		state["atStairs"] = atStairs;
		state["onTargetFloor"] = onTargetFloor;

		state["explored"] = false; // 항상 재선택되는 최하위 폴백이라 "완료됨" 개념이 없음(GoapPlanner.cs 주석 참고)

		return state;
	}
}
