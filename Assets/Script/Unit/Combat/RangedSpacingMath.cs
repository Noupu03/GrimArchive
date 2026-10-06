using System;

public enum RangedSpacing { Retreat, Hold, Approach }

// 원거리 유닛의 간격 유지 판정(순수) — 예전 CombatFSMState는 적이 위험 거리 이내면 후퇴, 쿨다운 중엔 거리가 정확히 HitRange/2+1칸이 아니면 접근·후퇴하는 on/off 제어라 적이 한 칸만 움직여도 매 행동 틱마다 앞뒤로 왕복했다(2026-10-05 아처 떨림). 후퇴와 접근 사이에 '제자리' 구간을 둔다.
public static class RangedSpacingMath
{
	// 위험 거리 이내면 후퇴, 사거리 안이면 제자리(쿨다운 중에도 그 자리에서 기다렸다 쏜다), 사거리 밖이면 접근. dangerDist <= 0이면 후퇴 구간이 없다.
	public static RangedSpacing Decide(int chebDist, int dangerDist, int maxRange)
	{
		if (dangerDist > 0 && chebDist <= dangerDist) return RangedSpacing.Retreat;
		return chebDist <= maxRange ? RangedSpacing.Hold : RangedSpacing.Approach;
	}

	// 적(tx,ty)에게서 한 걸음 물러날 칸 — 8방향 이웃 중 canStep(dx,dy)가 참이고 실제로 거리가 늘며 '적 반대 방향'에 가장 가까운 칸. 반대 방향 성분이 없는 옆걸음은 고르지 않는다(벽에 몰렸을 때 옆으로 새며 떠는 것을 막는다). 갈 칸이 없으면 false(몰림). canStep은 벽·점유·방 제한·코너 커팅 판정을 호출부가 맡는다.
	public static bool TryChooseRetreatStep(int ux, int uy, int tx, int ty, Func<int, int, bool> canStep, out int stepX, out int stepY)
	{
		stepX = 0;
		stepY = 0;
		int awayX = ux - tx, awayY = uy - ty;
		if (awayX == 0 && awayY == 0) awayX = 1; // 같은 칸이면 임의 방향
		int curSq = awayX * awayX + awayY * awayY;

		float bestScore = 0f;
		int bestSq = curSq;
		bool found = false;
		for (int dx = -1; dx <= 1; dx++)
		{
			for (int dy = -1; dy <= 1; dy++)
			{
				if (dx == 0 && dy == 0) continue;
				int nx = awayX + dx, ny = awayY + dy;
				int sq = nx * nx + ny * ny;
				if (sq <= curSq) continue; // 거리가 안 늘면 후퇴가 아니다

				float stepLen = (float)Math.Sqrt(dx * dx + dy * dy);
				float score = (dx * awayX + dy * awayY) / stepLen; // 적 반대 방향 정렬도(양수여야 한다)
				if (score <= 0.0001f) continue;
				if (score < bestScore - 0.0001f || (Math.Abs(score - bestScore) <= 0.0001f && sq <= bestSq)) continue;
				if (!canStep(dx, dy)) continue;

				bestScore = score;
				bestSq = sq;
				stepX = dx;
				stepY = dy;
				found = true;
			}
		}
		return found;
	}
}
