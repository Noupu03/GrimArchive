using System;
using UnityEngine;

// 개인 도주 판정 상태(인류 한 명, 침공 단위) — 명세서 3-3·3-4장. 확률은 판정 차수에서 파생하고(부동소수 누적 없음) 정신력이 회복되면 초기화된다.
public sealed class FleeRollState
{
	public int FailedRolls;         // 지금까지 버틴 판정 수 — 다음 확률 = ProbabilityForRoll(FailedRolls + 1)
	public float PendingDecrease;   // 판정에 아직 쓰지 않은 공황 구간 감소량(소수점 유지)
	public int TotalRolls;          // 이번 침공의 누적 판정 수(진단용)
	public bool Confirmed;          // 도주 확정 — 이후 회복·리더 명령으로 취소하지 않는다

	public float NextProbability => FleeRollMath.ProbabilityForRoll(FailedRolls + 1);

	// 정신력이 회복됨 — 다음 확률 50%, 미사용 감소량 0. 확정된 도주는 건드리지 않는다.
	public void OnRecovered()
	{
		if (Confirmed) return;
		FailedRolls = 0;
		PendingDecrease = 0f;
	}

	// 실제 던전 밖 이탈 후 다음 침공 — 첫 판정 확률 50%, 잔여 감소량 0으로 새로 시작한다.
	public void ResetForNewInvasion()
	{
		FailedRolls = 0;
		PendingDecrease = 0f;
		TotalRolls = 0;
		Confirmed = false;
	}
}

// 개인 도주 확률 판정(순수) — 공황 구간에서 실제로 5 줄어들 때마다 1회, 확률은 50%에서 시작해 버틸 때마다 +10%p(6회째 100%).
public static class FleeRollMath
{
	public const float FirstProbability = 0.5f;
	public const float ProbabilityStep = 0.1f;
	public const float DecreasePerRoll = 5f;
	const float Epsilon = 1e-4f;

	public readonly struct Outcome
	{
		public readonly int Rolls; // 이번 호출에서 시행한 판정 수
		public readonly bool Fled; // 도주 확정 여부
		public Outcome(int rolls, bool fled) { Rolls = rolls; Fled = fled; }
	}

	// rollNumber는 1부터. 1=50%, 2=60%, ..., 6 이상=100%.
	public static float ProbabilityForRoll(int rollNumber)
		=> Mathf.Min(1f, FirstProbability + ProbabilityStep * (Mathf.Max(1, rollNumber) - 1));

	// 공황 구간 감소량을 누적하고 5마다 판정한다. random01은 [0,1) 균등 난수 — 도주는 난수 < 확률. 도주가 확정되는 순간 추가 판정을 멈춘다. 버텨도 감소한 정신력은 그대로이며 다음 확률만 오른다.
	public static Outcome Advance(FleeRollState state, float panicZoneDecrease, Func<float> random01)
	{
		if (state.Confirmed || panicZoneDecrease <= 0f) return new Outcome(0, false);

		state.PendingDecrease += panicZoneDecrease;
		int rolls = 0;
		while (state.PendingDecrease >= DecreasePerRoll - Epsilon)
		{
			state.PendingDecrease = Mathf.Max(0f, state.PendingDecrease - DecreasePerRoll);
			rolls++;
			state.TotalRolls++;
			if (random01() < ProbabilityForRoll(state.FailedRolls + 1))
			{
				state.Confirmed = true;
				return new Outcome(rolls, true);
			}
			state.FailedRolls++;
		}
		return new Outcome(rolls, false);
	}
}
