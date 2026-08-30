using UnityEngine;

// 인지·정보판정·실패처리 시스템(02_인지·정보판정·실패처리_시스템_v0.2)의 순수 계산 함수 모음 —
// VisionMath(01/01-A)와 동일 컨벤션(부수효과 없음, 저장/조회는 Unit/UnitFunction 담당). 문서가
// 위임한 부분(정신력 단계 임계값, 경계 상태 실제 이동 행동, 간접 인지 발생 지점 등)은 CLAUDE.md
// 관례대로 스텁 처리한다.
public static class PerceptionMath
{
	// ─────────────────────────── 10장. 경계 상태 보정 ───────────────────────────
	public const float AlertDetectionBonus = 20f; // 경계 중 감지 보정 = 감지 스탯 + 20

	// 관찰자의 "경계 반영 감지 보정" — 기본은 감지 스탯 그대로, 경계 상태면 +20.
	public static float DetectionCorrection(float observerSpotting, bool isAlert)
		=> observerSpotting + (isAlert ? AlertDetectionBonus : 0f);

	// ─────────────────────────── 9장. 정신력 보정 (인류 전용) ───────────────────────────
	// 문서는 6단계 보정값만 명시하고 mental/maxMental 비율 구간 경계는 밝히지 않는다 — 기존 가중치
	// 시스템의 MentalErrorState(공포<50%/공황<25%)와는 별개로, 0~100%를 6등분(17/33/50/67/83)해서
	// 매핑한다. ratio가 높을수록(정신력이 가득 찰수록) 상위 단계(흥분 쪽)로 간다.
	public const float MentalTierBoundary1 = 17f; // 공황 | 공포
	public const float MentalTierBoundary2 = 33f; // 공포 | 긴장
	public const float MentalTierBoundary3 = 50f; // 긴장 | 안정
	public const float MentalTierBoundary4 = 67f; // 안정 | 냉정
	public const float MentalTierBoundary5 = 83f; // 냉정 | 흥분

	public const float MentalCorrectionExcited = 10f;  // 흥분 +2단계
	public const float MentalCorrectionCalm    = 5f;   // 냉정 +1단계
	public const float MentalCorrectionStable  = 0f;   // 안정  0단계
	public const float MentalCorrectionTension = -5f;  // 긴장 -1단계
	public const float MentalCorrectionFear    = -15f; // 공포 -2단계
	public const float MentalCorrectionPanic   = -30f; // 공황 -3단계

	// mentalRatioPercent: 0~100 (mental/maxMental*100). 100을 넘거나 0 미만이어도 클램프해서 처리한다.
	public static MentalTier GetMentalTier(float mentalRatioPercent)
	{
		float r = Mathf.Clamp(mentalRatioPercent, 0f, 100f);
		if (r < MentalTierBoundary1) return MentalTier.Panic;
		if (r < MentalTierBoundary2) return MentalTier.Fear;
		if (r < MentalTierBoundary3) return MentalTier.Tension;
		if (r < MentalTierBoundary4) return MentalTier.Stable;
		if (r < MentalTierBoundary5) return MentalTier.Calm;
		return MentalTier.Excited;
	}

	public static float MentalVisibilityCorrection(MentalTier tier) => tier switch
	{
		MentalTier.Excited => MentalCorrectionExcited,
		MentalTier.Calm     => MentalCorrectionCalm,
		MentalTier.Stable   => MentalCorrectionStable,
		MentalTier.Tension  => MentalCorrectionTension,
		MentalTier.Fear     => MentalCorrectionFear,
		MentalTier.Panic    => MentalCorrectionPanic,
		_ => 0f,
	};

	// 인류 전용(9장 — 몬스터는 사용하지 않음). maxMental이 0 이하면 "정신력 보정 대상 데이터 없음"으로
	// 보고 안정(0) 취급한다 — 안 그러면 0으로 나눠 항상 100%가 되어 최고 단계 보너스가 공짜로 붙는다.
	public static float MentalCorrectionForHuman(float mental, float maxMental)
	{
		if (maxMental <= 0f) return MentalCorrectionStable;
		float ratioPercent = Mathf.Clamp01(mental / maxMental) * 100f;
		return MentalVisibilityCorrection(GetMentalTier(ratioPercent));
	}

	// ─────────────────────────── 8장. 최종 계산 가시성 ───────────────────────────
	// 최종 계산 가시성 = 대상 가시성(VisionMath.FinalVisibility — 기본/은신 기반 가시성 + 공격 후
	// 가시성 증가까지 이미 반영된 값) + 경계 반영 감지 보정 + 정신력 보정(인류 관찰자만 0이 아님).
	public static float TotalPerceptionVisibility(float targetVisibility, float detectionCorrection, float mentalCorrection)
		=> targetVisibility + detectionCorrection + mentalCorrection;

	// ─────────────────────────── 12장. 인지 결과 확률표 ───────────────────────────
	// 현재 확률표는 문서가 명시한 임시 기준 그대로다. "100 이상" 구간(100%/0%/0%)은 "80 이상" 구간과
	// 분리된 별도 행 — 하한(0 이하)과 대칭되는 상한 처리가 명시적으로 존재한다.
	public static (float accurate, float suspicious, float unrecognized) OutcomeProbabilities(float totalVisibility)
	{
		if (totalVisibility <= 0f)  return (0.00f, 0.05f, 0.95f);
		if (totalVisibility < 20f)  return (0.10f, 0.20f, 0.70f);
		if (totalVisibility < 40f)  return (0.25f, 0.35f, 0.40f);
		if (totalVisibility < 60f)  return (0.50f, 0.35f, 0.15f);
		if (totalVisibility < 80f)  return (0.75f, 0.20f, 0.05f);
		if (totalVisibility < 100f) return (0.90f, 0.08f, 0.02f);
		return (1.00f, 0.00f, 0.00f); // 100 이상
	}

	// roll01: 0~1 균등 난수는 호출부(UnitFunction)가 UnityEngine.Random.value 등으로 넘겨준다 —
	// 이 함수 자체는 순수 함수로 유지해 테스트에서 정확한 경계값을 고정 검증할 수 있게 한다.
	public static PerceptionOutcome RollOutcome(float totalVisibility, float roll01)
	{
		var (accurate, suspicious, _) = OutcomeProbabilities(totalVisibility);
		if (roll01 < accurate) return PerceptionOutcome.AccuratePerception;
		if (roll01 < accurate + suspicious) return PerceptionOutcome.SuspiciousTile;
		return PerceptionOutcome.Unrecognized;
	}

	// ─────────────────────────── 21장. 수상한 타일 임시 위험도/흥미도 ───────────────────────────
	public const float SuspiciousTileTempDanger = 5f;
	public const float SuspiciousTileTempInterest = 5f;

	// ─────────────────────────── 22장. 2칸 이내 재접근 재인지 판정 ───────────────────────────
	public const int SuspiciousTileReapproachDistanceTiles = 2;

	// ─────────────────────────── 25장. 간접 인지 임시 위험도/흥미도 ───────────────────────────
	// 08_전파·소리·간접입력 문서/시스템이 아직 없어 실제 발생 지점이 없다 — 값만 미리 계산해 둔다
	// (VisionMath.NonEmptyTileTempWeight와 동일한 "소비자 없는 스텁" 전례).
	public const float IndirectCombatPositionTempDanger = 10f;   // 전투 위치 간접 인지
	public const float IndirectNonHostileTempInterest = 3f;      // 시체/전멸흔적/건물 전파 간접 인지

	// ─────────────────────────── 15장. 정확 인지 후속 반응 후보 ───────────────────────────
	// 대상 종류별로 어떤 반응 후보가 "생성 가능한 후보"인지만 정리한 조회표다. 실제로 이 중 하나를
	// "선택"해서 행동으로 옮기는 건 04/06/08 문서(부재)의 몫 — 여기서는 후보 목록만 돌려준다.
	public static PerceptionReactionCandidate[] ReactionCandidatesFor(PerceptionTargetKind kind) => kind switch
	{
		PerceptionTargetKind.EnemyUnit => new[]
		{
			PerceptionReactionCandidate.CombatResponse, PerceptionReactionCandidate.TargetCandidate,
			PerceptionReactionCandidate.Bypass, PerceptionReactionCandidate.Propagate,
		},
		PerceptionTargetKind.Trap => new[]
		{
			PerceptionReactionCandidate.Alert, PerceptionReactionCandidate.DisarmTrap,
			PerceptionReactionCandidate.Bypass, PerceptionReactionCandidate.Destroy,
			PerceptionReactionCandidate.Propagate,
		},
		PerceptionTargetKind.Building => new[]
		{
			PerceptionReactionCandidate.Investigate, PerceptionReactionCandidate.Destroy,
			PerceptionReactionCandidate.Bypass, PerceptionReactionCandidate.Propagate,
		},
		PerceptionTargetKind.Corpse => new[]
		{
			PerceptionReactionCandidate.Investigate, PerceptionReactionCandidate.Alert,
			PerceptionReactionCandidate.Propagate,
		},
		PerceptionTargetKind.WipeoutTrace => new[]
		{
			PerceptionReactionCandidate.Investigate, PerceptionReactionCandidate.Alert,
			PerceptionReactionCandidate.Propagate,
		},
		// 03문서 7-3장(2026-07-27 신규): 코어 — 일반 유닛은 직접 조사하지 않고 전파만 한다.
		PerceptionTargetKind.Core => new[]
		{
			PerceptionReactionCandidate.Propagate, PerceptionReactionCandidate.Investigate,
		},
		_ => System.Array.Empty<PerceptionReactionCandidate>(),
	};
}

// 02문서 2장/14장/15장의 정확 인지 대상 분류. 함정/건물은 아직 전용 엔티티가 없어(구현현황 문서
// 참고) 실질적으로 EnemyUnit/Corpse/WipeoutTrace만 실제로 발생한다 — 나머지는 대응 엔티티가
// 생기면 그대로 쓸 수 있도록 미리 분류만 남겨둔다.
public enum PerceptionTargetKind
{
	None, // 15장 표에 없는 대상(예: 일반 루팅) — ReactionCandidatesFor의 기본 분기(빈 배열)로 처리됨.
	EnemyUnit,
	Trap,
	Building,
	Corpse,
	WipeoutTrace,
	Core, // 03문서 7-3장(2026-07-27 신규): 리더 전용 조사 오브젝트.
}
