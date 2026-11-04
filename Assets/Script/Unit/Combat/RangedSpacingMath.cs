using System;

public enum RangedSpacing { Retreat, Hold, Approach }

// 한 걸음 후퇴가 거절된 이유(진단·로그용) — None: 후퇴 가능, Cornered: 물러날 칸이 없다(벽·점유), WouldLoseSight: 물러날 칸은 있지만 모두 적을 시야에서 놓치게 한다(ㄱ자 모퉁이 등).
public enum RetreatRefusal { None, Cornered, WouldLoseSight }

// 원거리 유닛의 간격 유지 판정(순수) — 예전 CombatFSMState는 적이 위험 거리 이내면 후퇴, 쿨다운 중엔 거리가 정확히 HitRange/2+1칸이 아니면 접근·후퇴하는 on/off 제어라 적이 한 칸만 움직여도 매 행동 틱마다 앞뒤로 왕복했다(2026-10-05 아처 떨림). 후퇴와 접근 사이에 '제자리' 구간을 둔다.
public static class RangedSpacingMath
{
	// 위험 거리 이내면 후퇴, 사거리 안이면 제자리(쿨다운 중에도 그 자리에서 기다렸다 쏜다), 사거리 밖이면 접근. dangerDist <= 0이면 후퇴 구간이 없다.
	public static RangedSpacing Decide(int chebDist, int dangerDist, int maxRange)
	{
		if (dangerDist > 0 && chebDist <= dangerDist) return RangedSpacing.Retreat;
		return chebDist <= maxRange ? RangedSpacing.Hold : RangedSpacing.Approach;
	}

	// 시야 유지 조건이 붙은 후퇴 한 칸(2026-11-04, ㄱ자 모퉁이 왕복) — 후퇴 칸이 적을 시야에서 놓치게 하면 FSM이 전투에서 내려가고(적이 안 보이면 Combat 우선순위 0) 파티원이 전해 준 적 위치로 '전파적접근'이 시작돼 "후퇴 → 시야 상실 → 접근"이 반복됐다.
	// keepsSight(dx,dy)는 그 걸음 뒤에도 적을 볼 수 있는가(호출부가 직선 판정으로 정한다). null이면 조건이 없어 예전과 같다. 시야를 유지하는 칸이 없으면 후퇴하지 않는다 — 시야 조건 없이는 갈 칸이 있었다면 WouldLoseSight, 그것도 없으면 Cornered.
	public static bool TryChooseRetreatStep(int ux, int uy, int tx, int ty, Func<int, int, bool> canStep, Func<int, int, bool> keepsSight, out int stepX, out int stepY, out RetreatRefusal refusal)
	{
		refusal = RetreatRefusal.None;
		if (keepsSight == null) return TryChooseRetreatStep(ux, uy, tx, ty, canStep, out stepX, out stepY) || Refuse(RetreatRefusal.Cornered, out refusal);

		if (TryChooseRetreatStep(ux, uy, tx, ty, (dx, dy) => canStep(dx, dy) && keepsSight(dx, dy), out stepX, out stepY)) return true;

		stepX = 0;
		stepY = 0;
		bool couldRetreatBlindly = TryChooseRetreatStep(ux, uy, tx, ty, canStep, out _, out _);
		refusal = couldRetreatBlindly ? RetreatRefusal.WouldLoseSight : RetreatRefusal.Cornered;
		return false;
	}

	private static bool Refuse(RetreatRefusal reason, out RetreatRefusal refusal)
	{
		refusal = reason;
		return false;
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
