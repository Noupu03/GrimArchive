using UnityEngine;

public static class AIMovementHelper
{
	public static void MoveTowardsPos(Unit unit, Vector2Int targetPos)
	{
		if (unit.MovementAlgorithm != null && unit.MovementAlgorithm.TryGetNextStep(unit, targetPos, out Dir nextDir))
			unit.Move(nextDir);
	}

	public static void MoveTowardsTarget(Unit unit, Unit target)
		=> MoveTowardsPos(unit, target.position);

	public static void MoveAwayFromTarget(Unit unit, Unit target, float desiredDist)
	{
		Vector2 away = (Vector2)(unit.position - target.position);
		if (away == Vector2.zero) away = Vector2.right;

		float maxComp = Mathf.Max(Mathf.Abs(away.x), Mathf.Abs(away.y));
		Vector2 scaled = away / maxComp;

		int targetDist = Mathf.RoundToInt(desiredDist);
		Vector2Int retreatPos = target.position + new Vector2Int(
			Mathf.RoundToInt(scaled.x * targetDist),
			Mathf.RoundToInt(scaled.y * targetDist)
		);
		MoveTowardsPos(unit, retreatPos);
	}

	// 03문서 6장 보호 포메이션 이동 로직 — backDistance<=1이면 근접, 더 크면 원거리 배치.
	public static void MoveToEscortSlot(Human human, float backDistance)
	{
		if (human.currentFormation == null || human.currentFormation.EscortTarget == null || !human.currentFormation.EscortTarget.IsInteracting)
		{
			Human target = human.FindDirectlyVisibleInteractingAlly();
			if (target == null) { human.currentFormation = null; return; }
			human.currentFormation = new FormationState { EscortTarget = target };
		}

		Human escortTarget = human.currentFormation.EscortTarget;
		Vector2Int slot = human.GetEscortSlotPosition(escortTarget, backDistance);
		MoveTowardsPos(human, slot);
	}
}
