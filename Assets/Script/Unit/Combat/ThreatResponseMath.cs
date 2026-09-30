using System;
using System.Collections.Generic;

// 02번 문서 3~4장의 "후보 중 점수로 고르기"를 게임 객체(Unit)와 무관한 순수 로직으로 분리한 것 — CombatFSMState.SelectAttackTarget이 일반 후보 비교와 보스 집중 중의 임시 위협 대응에
// 같은 규칙(점수 높은 순 → 거리 가까운 순 → 완전 동점은 무작위)을 쓰게 하고, 단위 테스트가 세션 없이 규칙을 검증할 수 있게 한다.
public static class ThreatResponseMath
{
	// 점수가 가장 높은 후보, 같으면 거리가 더 가까운 후보, 점수·거리까지 완전히 같으면 그중 하나를 pickIndex(count)로 무작위 선택한다(02번 3장 "동점"). 후보가 없으면 null.
	public static T PickBest<T>(IList<T> pool, Func<T, float> scoreOf, Func<T, float> distanceOf, Func<int, int> pickIndex, out float bestScore, out float bestDistance) where T : class
	{
		T best = null;
		bestScore = float.MinValue;
		bestDistance = float.MaxValue;
		var tied = new List<T>();
		foreach (var cand in pool)
		{
			float score = scoreOf(cand);
			float dist = distanceOf(cand);
			if (best == null || score > bestScore || (score == bestScore && dist < bestDistance))
			{
				best = cand; bestScore = score; bestDistance = dist;
				tied.Clear();
				tied.Add(cand);
			}
			else if (score == bestScore && dist == bestDistance)
			{
				tied.Add(cand);
			}
		}
		if (tied.Count > 1) best = tied[pickIndex(tied.Count)];
		return best;
	}

	// 같은 우선순위 집합(candidates) 안에서 대상을 고른다 — 이미 그 집합 안의 대상(current)을 상대 중이면 같은 우선순위 안의 교체 기준(shouldSwitch: 인류는 현재 점수의 1.2배 이상,
	// 몬스터는 더 높을 때만)을 그대로 적용해 후보 사이를 매 틱 오가지 않는다. current가 집합 밖(예: 임시 대응 전에 공격하던 보스)이거나 없으면 게이트 없이 최고 점수 후보를 고른다.
	public static T SelectWithHysteresis<T>(IList<T> candidates, T current, bool isHuman, Func<T, float> scoreOf, Func<T, float> distanceOf, Func<int, int> pickIndex,
		Func<bool, float, float, bool> shouldSwitch) where T : class
	{
		T best = PickBest(candidates, scoreOf, distanceOf, pickIndex, out float bestScore, out _);
		if (best == null) return null;

		if (current != null && !ReferenceEquals(current, best) && candidates.Contains(current) && !shouldSwitch(isHuman, scoreOf(current), bestScore))
			return current;
		return best;
	}
}
