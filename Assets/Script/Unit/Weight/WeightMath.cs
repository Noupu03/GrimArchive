using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 대표 가중치 3종 연산공식 문서의 순수 계산 함수 모음. 부수효과 없음(테스트 용이) —
// 저장/조회는 HumanKnowledgeBase, Unit.personalWeights가 담당하고 이 클래스는 숫자만 계산한다.
public static class WeightMath
{
	// ─────────────────────────── 1장. 범위 클램프 ───────────────────────────
	public const float UnderstandingMin = 0f, UnderstandingMax = 100f;
	public const float DangerMin = 0f, DangerMax = 999f;
	public const float InterestMin = 0f, InterestMax = 999f;

	// 던전 전체/방 위험도·흥미도는 합산값이라 999를 넘을 수 있음(1장/7장/20~22장) → 이 클램프를 쓰지 않는다.
	public static float Clamp(float value, WeightType type)
	{
		return type switch
		{
			WeightType.Understanding => Mathf.Clamp(value, UnderstandingMin, UnderstandingMax),
			WeightType.Danger        => Mathf.Clamp(value, DangerMin, DangerMax),
			WeightType.Interest      => Mathf.Clamp(value, InterestMin, InterestMax),
			_ => value,
		};
	}

	public static int AppliedValue(float stored) => Mathf.FloorToInt(stored);

	// ─────────────────────────── 4장. 개인 가중치 계산 ───────────────────────────
	public static float ApplyPersonalChange(float currentStored, float delta, WeightType type)
		=> Clamp(currentStored + delta, type);

	// ─────────────────────────── 6-1장. 정보 유형 비율값 ───────────────────────────
	public static float RatioValue(InfoType t) => t switch
	{
		InfoType.DirectExperience => 5f,
		InfoType.DirectWitness    => 3f,
		InfoType.Indirect         => 2f,
		_ => 0f,
	};

	// 전역 반영 "비율" 표기용 (6-1장 예시 1~3). 실제 반영량 계산에는 6-4장 공식을 쓰고, 이 함수는
	// 문서의 노출용 퍼센트 예시를 그대로 재현하기 위한 보조 함수다.
	// 반올림 규칙: 소수 둘째 자리까지, 셋째 자리에서 반올림(5 미만 내림) = 표준 반올림과 동일.
	public static float RoundRatio(float v) => Mathf.Round(v * 100f) / 100f;

	public static Dictionary<InfoType, float> ComputeReflectionRatios(IEnumerable<InfoType> presentTypes)
	{
		var distinct = presentTypes.Distinct().ToList();
		float sum = distinct.Sum(RatioValue);
		var result = new Dictionary<InfoType, float>();
		if (sum <= 0f) return result;
		foreach (var t in distinct) result[t] = RoundRatio(RatioValue(t) / sum);
		return result;
	}

	// ─────────────────────────── 6-2장. 동일 수치 정보 중복 제거 ───────────────────────────
	public static List<IncidentEntry> DedupExact(IEnumerable<IncidentEntry> entries)
	{
		var seen = new HashSet<string>();
		var result = new List<IncidentEntry>();
		foreach (var e in entries)
		{
			if (seen.Add(e.DedupKey)) result.Add(e);
		}
		return result;
	}

	// ─────────────────────────── 25장. 공황 단계 정보 평균 처리 ───────────────────────────
	public const float PanicWeight = 0.3f, NonPanicWeight = 0.7f;

	public static float BlendPanicValue(float panicValue, float nextPriorityValue)
		=> panicValue * PanicWeight + nextPriorityValue * NonPanicWeight;

	// 같은 InfoType 버킷 안에 공황 단계 기록과 비공황 기록이 섞여 있으면 25장 규칙으로 블렌드해서
	// "하나의 대표값"으로 접자. 공황만 있으면 그 값들의 평균을 그대로 쓴다(25장 "공황 단계 유닛만
	// 생환한 경우 해당 유닛의 기록값을 단독 확정값으로 사용한다"를 수치 병합에도 동일 적용 — 구현현황에 근거 기재).
	private static List<float> ResolvePanicBlend(List<IncidentEntry> sameInfoTypeEntries)
	{
		var panic = sameInfoTypeEntries.Where(e => e.MentalStateAtRecord == MentalErrorState.Panic).Select(e => e.ChangeValue).ToList();
		var nonPanic = sameInfoTypeEntries.Where(e => e.MentalStateAtRecord != MentalErrorState.Panic).Select(e => e.ChangeValue).ToList();

		if (panic.Count > 0 && nonPanic.Count > 0)
		{
			float panicAvg = panic.Average();
			float nonPanicAvg = nonPanic.Average();
			return new List<float> { BlendPanicValue(panicAvg, nonPanicAvg) };
		}
		if (panic.Count > 0) return panic; // 공황만 생환
		return nonPanic;
	}

	// ─────────────────────────── 6-3장. 정보 유형별 대표 변화량 ───────────────────────────
	// (같은 정보 유형 안에서 서로 다른 수치가 있으면 서로 다른 값들의 평균)
	private static float RepresentativeChange(List<IncidentEntry> sameInfoTypeEntries)
	{
		var blended = ResolvePanicBlend(sameInfoTypeEntries);
		var distinctValues = blended.Distinct().ToList();
		return distinctValues.Count == 0 ? 0f : distinctValues.Average();
	}

	// ─────────────────────────── 6-4장. 전역 반영량 계산 ───────────────────────────
	// group: 이미 동일 IncidentId + TargetId + WeightType으로 묶인 항목들 (여러 생존자의 기록 포함 가능)
	public static float ComputeGlobalReflectionAmount(List<IncidentEntry> group)
	{
		if (group == null || group.Count == 0) return 0f;

		var deduped = DedupExact(group);
		var byInfoType = deduped.GroupBy(e => e.InfoType);

		float weightedSum = 0f, ratioSum = 0f;
		foreach (var bucket in byInfoType)
		{
			float representative = RepresentativeChange(bucket.ToList());
			float ratio = RatioValue(bucket.Key);
			weightedSum += representative * ratio;
			ratioSum += ratio;
		}
		return ratioSum <= 0f ? 0f : weightedSum / ratioSum;
	}

	// ─────────────────────────── 7장 / 7-1장. 이해도 증가 ───────────────────────────
	public const float SpecialUnitSpeciesUnderstandingCap = 50f;
	public const float SpecialUnitIndividualUnderstandingCap = 50f;

	// 일반 유닛 이해도 = 종별 이해도. 특수 유닛 이해도 = min(종별,50) + min(개별,50).
	public static float ComposeUnderstanding(float speciesApplied, float individualApplied, bool isSpecialUnit)
	{
		if (!isSpecialUnit) return Clamp(speciesApplied, WeightType.Understanding);
		float speciesPart = Mathf.Min(speciesApplied, SpecialUnitSpeciesUnderstandingCap);
		float individualPart = Mathf.Min(individualApplied, SpecialUnitIndividualUnderstandingCap);
		return Clamp(speciesPart + individualPart, WeightType.Understanding);
	}

	// ─────────────────────────── 8장. 이해도 총량 한도 및 감소 ───────────────────────────
	public const float ValidationUnderstandingPoolCap = 200f;   // 검증용 한도
	public const float FinalUnderstandingPoolCap = 8000f;       // 최종 목표 한도
	public const int UnderstandingDecayStaleWaves = 2;          // 2웨이브 이상 미갱신
	public const float UnderstandingDecayFloorRatio = 0.3f;     // 유지 비율 30%

	public readonly struct PoolOverflowResult
	{
		public readonly float OverflowAmount;
		public readonly float DecreaseFloor;
		public readonly float DecreaseCapacity;
		public readonly float ActualDecrease;
		public PoolOverflowResult(float overflow, float floor, float capacity, float actual)
		{
			OverflowAmount = overflow; DecreaseFloor = floor; DecreaseCapacity = capacity; ActualDecrease = actual;
		}
	}

	// 8장 예시 그대로: 초과량 계산 → 감소 하한 → 감소 가능량 → 실제 감소량(min(초과량, 감소가능량))
	public static PoolOverflowResult ComputePoolDecrease(float currentTotal, float incomingIncrease, float poolCap, float staleTargetCurrentValue)
	{
		float overflow = currentTotal + incomingIncrease - poolCap;
		if (overflow <= 0f) return new PoolOverflowResult(overflow, 0f, 0f, 0f);

		float floor = staleTargetCurrentValue * UnderstandingDecayFloorRatio;
		float capacity = Mathf.Max(0f, staleTargetCurrentValue - floor);
		float actual = Mathf.Min(overflow, capacity);
		return new PoolOverflowResult(overflow, floor, capacity, actual);
	}

	// ─────────────────────────── 9장. 미표기 내부 정보 오차 ───────────────────────────
	// 이해도 적용값 구간별 오차율 (표 그대로)
	public static float HiddenInfoNoiseRatio(int understandingApplied)
	{
		if (understandingApplied >= 100) return 0f;
		if (understandingApplied >= 80) return 0.05f;
		if (understandingApplied >= 70) return 0.06f;
		if (understandingApplied >= 60) return 0.10f;
		if (understandingApplied >= 40) return 0.20f;
		if (understandingApplied >= 20) return 0.30f;
		if (understandingApplied >= 10) return 0.40f;
		return 0.50f; // 0~9
	}

	// 실제 내부 수치를 이해도 기반 오차 범위 안에서 정수 랜덤값으로 치환.
	public static int ApplyHiddenInfoNoise(int actualValue, int understandingApplied, System.Random rng)
	{
		float ratio = HiddenInfoNoiseRatio(understandingApplied);
		int span = Mathf.RoundToInt(actualValue * ratio);
		int min = actualValue - span, max = actualValue + span;
		return rng.Next(min, max + 1);
	}

	// ─────────────────────────── 10장. 위험도 증가 ───────────────────────────
	// 7장과 동일 함수 재사용 (ApplyPersonalChange, WeightType.Danger로 호출)

	// ─────────────────────────── 11장 / 11-1 / 11-2. 위험도 감소 ───────────────────────────
	public static float MinDangerFloor(bool isBossOrNemesis) => isBossOrNemesis ? 10f : 3f;

	public const float TotalDamageDecreaseThreshold = -1f; // 위험도 감소 판정 기준값

	// 11-1장: 판정값 = 참여 유닛이 받은 총 피해량 - 대상 기준 총 피해량. 판정값 <= -1이면 감소 조건 충족.
	public static bool TotalDamageDecreaseQualifies(float totalDamageTaken, float baselineDamage, out float judgement)
	{
		judgement = totalDamageTaken - baselineDamage;
		return judgement <= TotalDamageDecreaseThreshold;
	}

	public const float PerHitDecreaseValue = -0.01f;
	public const float PerHitDecreaseMaxAccumPerBattle = -1f;

	// 11-2장: 타격 1회 피해량 차이 = 기준 피해량 - 실제 피해량. 차이 >= 1이면 감소 조건 충족.
	public static bool PerHitDecreaseQualifies(float perHitBaseline, float actualDamage, out float diff)
	{
		diff = perHitBaseline - actualDamage;
		return diff >= 1f;
	}

	public static float ApplyDangerDecrease(float currentStored, float rawDecrease, bool isBossOrNemesis)
	{
		float floor = MinDangerFloor(isBossOrNemesis);
		float next = currentStored + rawDecrease; // rawDecrease는 음수
		return Mathf.Max(next, floor);
	}

	// ─────────────────────────── 12장 / 12-1장. 위험도 단계 / 최종 위험도 ───────────────────────────
	public static DangerStage GetDangerStage(int dangerApplied)
	{
		if (dangerApplied >= 800) return DangerStage.StageMax;
		if (dangerApplied >= 600) return DangerStage.Stage3;
		if (dangerApplied >= 300) return DangerStage.Stage2;
		if (dangerApplied >= 100) return DangerStage.Stage1;
		return DangerStage.Stage0;
	}

	public static float ComposeFinalDanger(float baseDanger, float speciesAccumulated, float individualAccumulated)
		=> Clamp(baseDanger + speciesAccumulated + individualAccumulated, WeightType.Danger);

	// 13-2장 / 19장에서 재사용하는 위험도 단계별 보정값 (전멸흔적 발견 +25 기본값, 시체/흔적 흥미도 보정)
	public static float DangerStageBonus(DangerStage stage) => stage switch
	{
		DangerStage.Stage0 => 0f,
		DangerStage.Stage1 => 5f,
		DangerStage.Stage2 => 10f,
		DangerStage.Stage3 => 15f,
		DangerStage.StageMax => 20f,
		_ => 0f,
	};

	// ─────────────────────────── 15장. 타일 위험도 안전 확인 시간 ───────────────────────────
	public static float TileSafetyCheckSeconds(DangerStage stage) => stage switch
	{
		DangerStage.Stage0 => 1f,
		DangerStage.Stage1 => 2f,
		DangerStage.Stage2 => 3f,
		DangerStage.Stage3 => 4f,
		DangerStage.StageMax => 5f,
		_ => 1f,
	};

	// ─────────────────────────── 16장. 타일 흥미도 ───────────────────────────
	public const float UnexploredTileBaseDanger = 2f;
	public const float ExploredSafeTileBaseDanger = 0f;
	public const float UnexploredTileBaseInterest = 5f;
	public const float ExploredTileBaseInterest = 0f;

	public static float ComposeTileInterest(float baseExploreInterest, float objectInterest)
		=> Mathf.Clamp(baseExploreInterest + objectInterest, InterestMin, InterestMax);

	// ─────────────────────────── 17장. 오브젝트 흥미도 감소/복구 ───────────────────────────
	public const float ObjectInvestigatedDecayRatio = 0.5f;
	public const float ObjectDroppedRestoreRatio = 0.5f;

	public static float ObjectInterestAfterInvestigate(float currentInterest) => currentInterest * (1f - ObjectInvestigatedDecayRatio);
	public static float ObjectInterestAfterDrop(float baseInterest) => baseInterest * ObjectDroppedRestoreRatio;

	// ─────────────────────────── 18장. 유닛 흥미도-이해도 관계 ───────────────────────────
	// 일반 유닛 흥미도 = 유닛 기본 흥미도 × (100 - 이해도 적용값)%. IsInterestTarget이면 이 공식을 쓰지 않는다(호출부에서 분기).
	public static float UnitInterestFromUnderstanding(float baseInterest, int understandingApplied)
	{
		float remainRatio = Mathf.Clamp(100 - understandingApplied, 0, 100) / 100f;
		return Mathf.Clamp(baseInterest * remainRatio, InterestMin, InterestMax);
	}

	// ─────────────────────────── 19장. 시체 / 흔적 흥미도 ───────────────────────────
	public const float CorpseTraceBaseInterest = 10f;
	public const float WipeoutTraceBaseInterest = 25f;

	public static float CorpseTraceInterest(DangerStage causerStage) => CorpseTraceBaseInterest + DangerStageBonus(causerStage);

	// ─────────────────────────── 13-2장. 전멸 흔적 발견 전역 반영값 ───────────────────────────
	public const float WipeoutTraceGlobalBase = 25f;
	public static float WipeoutTraceGlobalReflection(DangerStage causerStage) => WipeoutTraceGlobalBase + DangerStageBonus(causerStage);

	// ─────────────────────────── 13-1장. 파티 전멸 ───────────────────────────
	public const float PartyWipeoutDungeonDangerIncrease = 30f;
	public const int WipeoutTraceLifespanWaves = 2;

	// ─────────────────────────── 20장. 방 위험도 기본값 ───────────────────────────
	public const float UnexploredNormalRoomBaseDanger = 20f;
	public const float UnexploredBossRoomBaseDanger = 70f;
	public const float ExploringNormalRoomFixedDanger = 10f;
	public const float ExploringBossRoomFixedDanger = 35f;

	// ─────────────────────────── 21장. 방 흥미도 기본값 ───────────────────────────
	public const float UnexploredNormalRoomBaseInterest = 30f;
	public const float UnexploredBossRoomBaseInterest = 50f;
	public const float ExploringNormalRoomFixedInterest = 15f;
	public const float ExploringBossRoomFixedInterest = 25f;

	// ─────────────────────────── 23장. 정보 오차 (위치/수치) ───────────────────────────
	public static (int tileError, float valueErrorRatio) InfoError(InfoType info, MentalErrorState mental)
	{
		// 정보 유형 기준 오차
		(int tile, float value) baseErr = info switch
		{
			InfoType.DirectExperience => (0, 0f),
			InfoType.DirectWitness    => (0, 0.05f),
			InfoType.Indirect         => (1, 0.10f),
			_ => (0, 0f),
		};
		// 정신력 상태 오차 (더 큰 쪽 채택 — 문서 23장 마지막 규칙)
		(int tile, float value) mentalErr = mental switch
		{
			MentalErrorState.Fear  => (1, 0.15f),
			MentalErrorState.Panic => (2, 0.25f),
			_ => (0, 0f),
		};
		int tileError = Mathf.Max(baseErr.tile, mentalErr.tile);
		float valueErrorRatio = Mathf.Max(baseErr.value, mentalErr.value);
		return (tileError, valueErrorRatio);
	}

	public static int ApplyValueNoise(int actualValue, float errorRatio, System.Random rng)
	{
		int span = Mathf.RoundToInt(actualValue * errorRatio);
		return rng.Next(actualValue - span, actualValue + span + 1);
	}

	// ─────────────────────────── 24장. 정보 우선순위 ───────────────────────────
	// 숫자가 작을수록 우선순위 높음 (1=최신 직접 경험 ... 5=오래된 간접 파악)
	public static int PriorityRank(InfoType info, bool isLatest)
	{
		if (info == InfoType.DirectExperience) return isLatest ? 1 : 2;
		if (info == InfoType.DirectWitness) return 3;
		return isLatest ? 4 : 5; // Indirect
	}

	// true면 candidate가 existing을 대체해야 함 (우선순위가 더 높거나, 같은 우선순위면서 더 최신)
	public static bool ShouldReplace(int candidateRank, int existingRank, bool candidateIsNewer)
	{
		if (candidateRank < existingRank) return true;
		if (candidateRank == existingRank) return candidateIsNewer;
		return false;
	}
}
