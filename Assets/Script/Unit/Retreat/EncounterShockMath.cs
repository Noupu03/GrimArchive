using System;
using System.Collections.Generic;

public enum EncounterShockKind
{
	None,
	MentalDamage,  // 격차 +2 — 기본 정신력 감소(일반 감소 보정 적용), 기절·공황 판정 없음
	StunAndPanic,  // 격차 +3 이상·직전 공황 아님 — 기절 1초 + 정신력 −71
	StunAndMax,    // 격차 +3 이상·직전부터 공황 — 기절 1초 + 정신력 −100(도주 즉시 확정, 이동은 기절 뒤)
}

public readonly struct EncounterShock
{
	public readonly EncounterShockKind Kind;
	public readonly int Tier;           // 0 없음, 2 = 격차 +2, 3 = 격차 +3 이상
	public readonly float MentalDamage; // MentalDamage일 때 기본 감소량(보정 전)
	public readonly float SetMentalTo;  // Stun 계열일 때 강제 설정할 정신력(감소 보정으로 완화하지 않음)
	public readonly float StunSeconds;

	public EncounterShock(EncounterShockKind kind, int tier, float mentalDamage, float setMentalTo, float stunSeconds)
	{
		Kind = kind; Tier = tier; MentalDamage = mentalDamage; SetMentalTo = setMentalTo; StunSeconds = stunSeconds;
	}

	public static readonly EncounterShock None = new EncounterShock(EncounterShockKind.None, 0, 0f, 0f, 0f);
}

// 몬스터 직접 조우 충격 판정(순수) — 후퇴도주 명세서 4장. 인류가 몬스터를 "직접 시야·인지로 확인"했을 때만 적용한다(소리·보고·함정 확인·관찰자는 제외 — 호출부 책임).
public static class EncounterShockMath
{
	public const float DefaultGap2MentalDamage = 20f; // 조정 가능한 설정값
	public const float StunSeconds = 1f;
	public const float PanicEntryMental = -71f;
	public const int MaxOrdinal = 5; // MAX 단계 순번

	// 위험도 → 비교 순번. 0~99=0, 100~299=1, 300~499=2, 500~699=3, 700~899=4, 900 이상(999 초과 포함)=5(MAX).
	// 가중치 시스템의 DangerStage(5단계: 300~599, 600~799, 800+)와 경계가 달라 별도 함수로 둔다 — 명세서 4-1장이 정한 6단계는 이 조우 충격 전용이다.
	public static int DangerOrdinal(float danger)
	{
		if (danger < 100f) return 0;
		if (danger < 300f) return 1;
		if (danger < 500f) return 2;
		if (danger < 700f) return 3;
		if (danger < 900f) return 4;
		return MaxOrdinal;
	}

	// 인류 Lv → 대응 단계 환산표는 성장 설계에서 확정 예정이다(명세서 4-1장 "미확정 입력"). 표가 없으면 격차를 임의로 계산하지 않고, 원수치 Lv를 위험도나 단계에서 직접 빼지도 않는다. 성장 문서가 확정되면 이 제공자를 채운다.
	public static Func<int, int?> LevelToResponseStage;

	public static bool TryComputeGap(float monsterDanger, int humanLevel, out int gap)
	{
		gap = 0;
		int? responseStage = LevelToResponseStage?.Invoke(humanLevel);
		if (!responseStage.HasValue) return false;
		gap = DangerOrdinal(monsterDanger) - responseStage.Value;
		return true;
	}

	// 격차 → 효과 구간. +3·+4·+5는 모두 같은 "+3 이상" 구간이다. +1 이하는 일반 조우 감소가 미정이라 이번 특수 규칙을 적용하지 않는다.
	public static int TierOfGap(int gap) => gap >= 3 ? 3 : (gap == 2 ? 2 : 0);

	// 구간별 확정 효과. 직전 공황 여부는 사건 직전 값으로 한 번만 분기한다(−71로 만든 뒤 다시 공황으로 보고 −100으로 내리지 않음).
	public static EncounterShock Resolve(int tier, float mentalBefore, float gap2Damage = DefaultGap2MentalDamage)
	{
		if (tier == 2) return new EncounterShock(EncounterShockKind.MentalDamage, 2, gap2Damage, 0f, 0f);
		if (tier >= 3)
		{
			return MentalMath.IsPanic(mentalBefore)
				? new EncounterShock(EncounterShockKind.StunAndMax, 3, 0f, MentalMath.Min, StunSeconds)
				: new EncounterShock(EncounterShockKind.StunAndPanic, 3, 0f, PanicEntryMental, StunSeconds);
		}
		return EncounterShock.None;
	}
}

// 인류 한 명의 조우 충격 이력(침공 단위) — 같은 인류가 같은 몬스터 개체에게 받는 조우 충격은 침공당 1회, 한 조우 묶음에서는 가장 높은 구간 하나만 적용한다(4-3·4-4장).
public sealed class EncounterShockTracker
{
	// 이번 침공에서 이 인류의 조우 충격 판정이 끝난 몬스터 개체 — 시야 이탈·재교전·방 이동·리더 교체·정신력 회복으로 초기화하지 않는다.
	public readonly HashSet<string> HandledMonsters = new HashSet<string>();
	// 지금 조우 묶음에서 이미 적용한 최고 구간(0 없음, 2, 3).
	public int AppliedTier { get; private set; }

	// 전투·추적이 끝나 조우 묶음이 종료됨 — 개체 처리 이력은 유지한다.
	public void EndEncounterBundle() => AppliedTier = 0;

	public void ResetForNewInvasion()
	{
		HandledMonsters.Clear();
		AppliedTier = 0;
	}

	// 새로 직접 확인한 몬스터들(개체 키, 위험도 격차)로 이번에 적용할 효과를 정한다. 판정이 끝난 개체는 제외하고, 함께 판단한 다른 몬스터도 이력에 기록한다. 남은 몬스터 중 가장 높은 구간이 이미 적용한 구간보다 높을 때만 효과가 나온다(+2 뒤 새 +3 이상은 상위 적용, 동급 이하는 없음).
	public EncounterShock Evaluate(IEnumerable<KeyValuePair<string, int>> seenMonsterGaps, float mentalBefore, float gap2Damage = EncounterShockMath.DefaultGap2MentalDamage)
	{
		int best = 0;
		foreach (var seen in seenMonsterGaps)
		{
			if (string.IsNullOrEmpty(seen.Key) || !HandledMonsters.Add(seen.Key)) continue;
			best = Math.Max(best, EncounterShockMath.TierOfGap(seen.Value));
		}
		if (best == 0 || best <= AppliedTier) return EncounterShock.None;

		AppliedTier = best;
		return EncounterShockMath.Resolve(best, mentalBefore, gap2Damage);
	}
}
