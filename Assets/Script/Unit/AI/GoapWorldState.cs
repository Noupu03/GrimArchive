using UnityEngine;

// GoapPlanner가 실제로 액션을 체이닝하려면 각 목표의 DesiredState 키(trapHandled/objectInvestigated/
// alertResolved/waitConditionMet/formationHeld/playerCommandExecuted/panicResolved/explored/enemyAlive)와
// 새로 도입한 위치 기반 서브골(atTrap/atInvestigateTarget/atEscortSlot/atPlayerMoveTarget,
// trapJoinWaitElapsed)이 전부 하나의 GoapState 안에 있어야 한다(예전엔 enemyVisible/isHit 2개뿐이라
// 대부분의 Precondition이 사실상 비어있었다). Unit/Human의 기존 필드를 그대로 읽기만 하는 순수 함수 —
// 새 상태 필드는 추가하지 않는다.
public static class GoapWorldState
{
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

		state["explored"] = false; // 항상 재선택되는 최하위 폴백이라 "완료됨" 개념이 없음(GoapPlanner.cs 주석 참고)

		return state;
	}
}
