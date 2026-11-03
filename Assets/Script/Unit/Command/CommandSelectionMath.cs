using System;
using System.Collections.Generic;

// 8장(명령 선택)·5-1장(제압 대상·보호 긴급 조건)의 순수 판정 — 부수효과 없음. 게임 상태를 읽어 후보를 모으는 일은 CommandIssueSystem이 한다.
public static class CommandSelectionMath
{
	// 8-표2: 파티 컨셉별 강공격·제압·보호 선택 순서(먼저 → 나중). 리더 직업·임무가 아니라 파티 컨셉을 따르며 후임 리더도 유지한다.
	public static TacticalCommand[] ActionPriorityOrder(PartyConcept concept)
	{
		switch (concept)
		{
			case PartyConcept.Adventurer: return new[] { TacticalCommand.Protect, TacticalCommand.Suppress, TacticalCommand.Strike };
			case PartyConcept.Rogue:      return new[] { TacticalCommand.Strike, TacticalCommand.Suppress, TacticalCommand.Protect };
			case PartyConcept.Mercenary:  return new[] { TacticalCommand.Strike, TacticalCommand.Protect, TacticalCommand.Suppress };
			case PartyConcept.Army:       return new[] { TacticalCommand.Suppress, TacticalCommand.Protect, TacticalCommand.Strike };
			case PartyConcept.Hero:       return new[] { TacticalCommand.Protect, TacticalCommand.Strike, TacticalCommand.Suppress };
			default:                      return new[] { TacticalCommand.Protect, TacticalCommand.Suppress, TacticalCommand.Strike };
		}
	}

	// 앞순위가 지금 불가능하면 다음 후보를 본다 — 셋 다 불가능하면 None(발령하지 않는다). 사용할 수 없는 앞순위 명령을 기다리지 않는다(L09).
	public static TacticalCommand PickActionPriority(PartyConcept concept, Func<TacticalCommand, bool> isReady)
	{
		foreach (var c in ActionPriorityOrder(concept))
			if (isReady(c)) return c;
		return TacticalCommand.None;
	}

	// 8-표1의 층위 순서(고유 스킬 → 상위 공동 공격 → 집중 공격 → 행동 우선 명령)는 별도 판정 함수가 아니라 호출 순서로 구현돼 있다 — UnitFunction.OnUpdate가 ExpeditionCommandSystem.Tick(상위)을 CommandIssueSystem.Tick(일반: 차단물 → 집중 → 행동 우선)보다 먼저 부르고, 상위 발령이 같은 지휘자의 일반 쿨타임도 시작한다.

	// 5-1-표5: 보호 명령을 판단하는 긴급 조건 — HP 30% 이하이면서 공격 위협을 받음 / 행동 불능이면서 공격받음. 사망 예측은 기본 긴급 보호 조건으로 쓰지 않는다(L17).
	public static bool IsProtectEmergency(float hpRatio, bool underAttackThreat, bool incapacitated, bool beingAttacked)
		=> (LeaderMath.IsLowHp(hpRatio) && underAttackThreat) || (incapacitated && beingAttacked);

	// 5-1-표4: 제압 대상 선정. pool은 caller가 5-1-표3 분류로 만든 "먼저 비교할 분류(후위 공격 + CC 사용자 있음)가 있으면 그 적들, 없으면 아군 근접 교전 적들"이다.
	// 직전 대상이 pool에 있으면(생존·직접 확인·교전·기술 조건 충족) 다시 지정하고, 아니면 지휘 목표 점수가 가장 높은 적, 동점이면 고정한 무작위 순서의 첫 적이다.
	public static T PickSuppressTarget<T>(IReadOnlyList<T> pool, T previous, Func<T, float> score, Func<T, int> tieRank) where T : class
	{
		if (pool == null || pool.Count == 0) return null;
		if (previous != null)
			for (int i = 0; i < pool.Count; i++)
				if (ReferenceEquals(pool[i], previous)) return previous;

		T best = null;
		float bestScore = float.NegativeInfinity;
		int bestTie = int.MaxValue;
		for (int i = 0; i < pool.Count; i++)
		{
			float s = score(pool[i]);
			int tie = tieRank(pool[i]);
			if (s > bestScore || (s == bestScore && tie < bestTie)) { best = pool[i]; bestScore = s; bestTie = tie; }
		}
		return best;
	}

	// 11-표1: 새 집중 공격 대상 선정. 점수가 가장 높은 적, 같으면 리더에게 가까운 적, 거리도 같으면 고정한 무작위 순서(11장). 점수보다 거리를 먼저 보지 않는다(L20).
	public static T PickTopScored<T>(IReadOnlyList<T> candidates, Func<T, float> score, Func<T, float> distanceToLeader, Func<T, int> tieRank, out float topScore, out float secondScore) where T : class
	{
		topScore = 0f;
		secondScore = 0f;
		if (candidates == null || candidates.Count == 0) return null;

		T best = null;
		float bestScore = float.NegativeInfinity, bestDist = float.PositiveInfinity;
		int bestTie = int.MaxValue;
		float secondBest = float.NegativeInfinity;
		for (int i = 0; i < candidates.Count; i++)
		{
			float s = score(candidates[i]);
			float d = distanceToLeader(candidates[i]);
			int tie = tieRank(candidates[i]);
			bool better = s > bestScore
				|| (s == bestScore && d < bestDist)
				|| (s == bestScore && d == bestDist && tie < bestTie);
			if (better)
			{
				if (best != null) secondBest = Math.Max(secondBest, bestScore);
				best = candidates[i]; bestScore = s; bestDist = d; bestTie = tie;
			}
			else
			{
				secondBest = Math.Max(secondBest, s);
			}
		}
		topScore = bestScore;
		secondScore = float.IsNegativeInfinity(secondBest) ? 0f : secondBest;
		return best;
	}
}
