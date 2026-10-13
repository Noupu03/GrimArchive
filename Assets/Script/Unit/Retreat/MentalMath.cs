using UnityEngine;

// 정신력 계산(순수) — 후퇴도주 명세서 3-1·3-2장. 인류 정신력은 −100~+100 원수치로 저장하고(BaseStatComponent.mental), 파티 평균·휴식 완료 판정은 비율 (원수치+100)/2 [%]를 쓴다. 정신력과 SP는 같은 자원이다.
public static class MentalMath
{
	public const float Min = -100f;
	public const float Max = 100f;
	public const float PanicBoundary = -70f; // 이 값 "미만"이 공황 — −70 자체는 공포이고 소수점에도 같은 경계를 쓴다
	public const float StableDecreaseMultiplier = 0.95f;
	const float FloorEpsilon = 1e-4f;

	public static float Clamp(float raw) => Mathf.Clamp(raw, Min, Max);

	// 원수치 → 비율(%). −100=0, −70=15, −20=40, 0=50, +100=100.
	public static float ToRatioPercent(float raw) => (Clamp(raw) + 100f) * 0.5f;
	public static float FromRatioPercent(float percent) => Clamp(percent * 2f - 100f);

	// units.json·프리팹의 mental/maxMental은 "비율 저작 스케일(0~max)"이다(40/100 = 비율 40% = 원수치 −20) — 프리팹을 손대지 않고 런타임에 원수치로 환산한다. maxMental ≤ 0은 정신력 시스템이 없는 유닛(몬스터)이라 0.
	public static float FromAuthoringScale(float mental, float maxMental)
		=> maxMental <= 0f ? 0f : FromRatioPercent(Mathf.Clamp01(mental / maxMental) * 100f);

	public static MentalStage GetStage(float raw)
	{
		if (raw > 70f) return MentalStage.Excited;
		if (raw > 40f) return MentalStage.Calm;
		if (raw > 10f) return MentalStage.Stable;
		if (raw >= -10f) return MentalStage.Normal;
		if (raw >= -40f) return MentalStage.Tension;
		if (raw >= PanicBoundary) return MentalStage.Fear;
		return MentalStage.Panic;
	}

	public static bool IsPanic(float raw) => raw < PanicBoundary;

	// 정신력 −100(하한) 도달 — 도주 즉시 확정(명세서 3-3장 2순서).
	public static bool ReachedFloor(float raw) => raw <= Min + FloorEpsilon;

	// 단계별 전투 효과(명세서 3-2장 "현재 효과"). 전투 문서의 적용표가 정본이며 여기서는 값만 한곳에 모아 둔다 — 전투에는 아직 연결하지 않았다.
	public static MentalStageEffects GetStageEffects(MentalStage stage)
	{
		switch (stage)
		{
			case MentalStage.Excited: return new MentalStageEffects(attack: 1.10f, preDelay: 0.95f, defenseSuccessDelta: -5f);
			case MentalStage.Calm:    return new MentalStageEffects(reaction: 0.95f, defenseSuccessDelta: 5f);
			case MentalStage.Stable:  return new MentalStageEffects(decrease: StableDecreaseMultiplier);
			case MentalStage.Tension: return new MentalStageEffects(reaction: 1.05f);
			case MentalStage.Fear:    return new MentalStageEffects(attack: 0.90f, defenseSuccessDelta: -10f);
			case MentalStage.Panic:   return new MentalStageEffects(attack: 0.80f, defenseSuccessDelta: -20f, reaction: 1.15f, fleeRollTarget: true);
			default:                  return MentalStageEffects.Neutral; // 평범 — 구조체의 new T()는 선택 인자 기본값이 아니라 전부 0이 되므로 Neutral을 쓴다
		}
	}

	// 일반 정신력 감소 보정(안정 단계 ×0.95 등). 현재 값 기준 단계를 쓴다.
	public static float ApplyDecreaseModifier(float baseDecrease, float currentRaw)
		=> baseDecrease * GetStageEffects(GetStage(currentRaw)).DecreaseMultiplier;

	// 공황 구간(−70 미만)에서 실제로 줄어든 양 — 공황 밖의 감소는 제외한다. −68→−76 = 6, −70→−82 = 12, −60→−71 = 1. 감소가 아니면 0.
	public static float PanicZoneDecrease(float before, float after)
	{
		if (after >= before) return 0f;
		float depthBefore = Mathf.Max(0f, PanicBoundary - before);
		float depthAfter = Mathf.Max(0f, PanicBoundary - after);
		return depthAfter - depthBefore;
	}
}

public readonly struct MentalStageEffects
{
	public readonly float AttackMultiplier;
	public readonly float PreDelayMultiplier;
	public readonly float DefenseSuccessDelta;
	public readonly float ReactionTimeMultiplier;
	public readonly float DecreaseMultiplier; // 정신력 감소량 배율
	public readonly bool FleeRollTarget;      // 개인 도주 판정 대상(공황)

	public static readonly MentalStageEffects Neutral = new MentalStageEffects(1f, 1f, 0f, 1f, 1f, false);

	public MentalStageEffects(float attack = 1f, float preDelay = 1f, float defenseSuccessDelta = 0f, float reaction = 1f, float decrease = 1f, bool fleeRollTarget = false)
	{
		AttackMultiplier = attack;
		PreDelayMultiplier = preDelay;
		DefenseSuccessDelta = defenseSuccessDelta;
		ReactionTimeMultiplier = reaction;
		DecreaseMultiplier = decrease;
		FleeRollTarget = fleeRollTarget;
	}
}
