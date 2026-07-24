using UnityEngine;

public static class AIMovementHelper
{
	// 반환값: 실제로 한 칸이라도 다가갈 수 있었는지(true) / 더 다가갈 방법이 전혀 없어 제자리에
	// 머물렀는지(false, 목표 칸이 다른 유닛/벽으로 완전히 막혀 있고 이미 갈 수 있는 가장 가까운
	// 지점까지 도달한 상태). 호출부(PlayerCommandFSMState)가 이 신호로 "길이 막혔다"를 판단해
	// 목표를 재지정한다.
	public static bool MoveTowardsPos(Unit unit, Vector2Int targetPos)
	{
		if (unit.MovementAlgorithm != null && unit.MovementAlgorithm.TryGetNextStep(unit, targetPos, out Dir nextDir))
		{
			unit.Move(nextDir);
			return true;
		}
		return false;
	}

	public static bool MoveTowardsTarget(Unit unit, Unit target)
		=> MoveTowardsPos(unit, target.position);

	// 목표 칸이 막혀 더 다가갈 수 없을 때, 바로 옆 8칸 중 실제로 갈 수 있는 가장 가까운(유닛 현재
	// 위치 기준) 빈 칸을 대신 반환한다(사용자 요청, 2026-07-24 "길이 막혀서 플레이어의 이동, 공격
	// 명령을 수행하지 못하면, 근처 바로 옆의 빈칸으로 목표 재지정"). 갈 수 있는 칸이 하나도 없으면
	// center를 그대로 돌려준다 — 호출부가 "재지정도 불가능"으로 판단해 처리한다.
	public static Vector2Int FindNearbyOpenTile(Unit unit, Vector2Int center)
	{
		Vector2Int best = center;
		int bestDist = int.MaxValue;
		for (int dx = -1; dx <= 1; dx++)
		for (int dy = -1; dy <= 1; dy++)
		{
			if (dx == 0 && dy == 0) continue;
			Vector2Int cand = center + new Vector2Int(dx, dy);
			if (!unit.CanMove(cand)) continue;
			int dist = Mathf.Max(Mathf.Abs(cand.x - unit.position.x), Mathf.Abs(cand.y - unit.position.y));
			if (dist < bestDist) { bestDist = dist; best = cand; }
		}
		return best;
	}

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
