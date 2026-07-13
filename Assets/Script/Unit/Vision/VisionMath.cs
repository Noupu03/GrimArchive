using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 시야-인지-반응 시스템(01_시야·인지범위·가시성)의 순수 계산 함수 모음. 부수효과 없음(테스트 용이) —
// WeightMath와 동일한 컨벤션. 저장/조회는 Unit(런타임 상태)과 UnitFunction(호출부)이 담당하고
// 이 클래스는 숫자만 계산한다.
//
// 이 폴더(Assets/문서/공식문서/시야인지반응)에는 현재 00(상위구조)/01(개념)/01-A(연산공식)만 있다.
// 00번 문서가 예고하는 02~10번 문서(인지 확률, 탐색 반응, 은신 세부산식, 전투 반응, 소리 등)는
// 아직 작성되지 않았으므로, 그 문서들에 위임된 세부 공식(예: 은신/거리/가림 보정의 정확한 감쇠식,
// 인지 성공 확률 자체)은 CLAUDE.md에 명시된 기존 관례대로 "가장 단순하고 합리적인 기본값"으로만
// 스텁 처리한다. 각 스텁 지점에 주석으로 근거를 남긴다.
public static class VisionMath
{
	// ─────────────────────────── 1장. 기본 변수 ───────────────────────────
	public const float DetectionStatMax = 100f;

	public const float BaseViewAngleDeg = 120f; // 시야각(고정 — 감지 스탯 영향 없음)
	public const int BaseViewDistanceTiles = 6;
	public const int ViewDistancePerSpottingStep = 20; // 감지 스탯 20마다 +1칸

	public const int BaseAwarenessDistanceTiles = 4;
	public const int AwarenessDistancePerSpottingStep = 20; // 감지 스탯 20마다 +1칸

	public const float BaseAwarenessAngleDeg = 60f;
	public const float MaxAwarenessAngleDeg = 120f;
	public const int AwarenessAnglePerSpottingStep = 10; // 감지 스탯 10마다 +6도
	public const float AwarenessAngleStepDeg = 6f;

	public const float VisibilityMin = 0f, VisibilityMax = 100f;
	public const float WallVisibility = 0f;
	public const float NormalTileVisibility = 100f;

	public const float AttackVisibilityBoostAmount = 10f;
	public const float AttackVisibilityBoostDuration = 5f; // 초

	public const float SurpriseHighThreatMultiplier = 2f; // 기습 방향 전환 기준: 근접 공격 기대값의 2배 이상

	public const int SpecialCircularBaseRadius = 1;
	public const int SpecialCircularMaxRadius = 3;

	// 시야 범위 내 "비어있지 않은 타일"에 부여되는 임시 위험도/흥미도(7장). 저장값이 아니라 경로/탐색
	// 방향 판단에만 쓰는 일회성 조회값 — 10_목표설정·이동경로·재설정 문서(소비자)가 아직 폴더에 없어
	// 현재는 값만 계산해 둔다(Unit.visionOnlyNonEmptyTiles 참고).
	public const float NonEmptyTileTempWeight = 5f;

	// ─────────────────────────── 2장/3장. 시야/인지 거리 공식 ───────────────────────────
	public static int ViewDistance(float spotting)
	{
		float clamped = Mathf.Clamp(spotting, 0f, DetectionStatMax);
		return BaseViewDistanceTiles + Mathf.FloorToInt(clamped / ViewDistancePerSpottingStep);
	}

	public static int AwarenessDistance(float spotting)
	{
		float clamped = Mathf.Clamp(spotting, 0f, DetectionStatMax);
		return BaseAwarenessDistanceTiles + Mathf.FloorToInt(clamped / AwarenessDistancePerSpottingStep);
	}

	// 4장: 인지각 = 기본 60도 + 감지 스탯 10마다 6도, 최대 120도.
	public static float AwarenessAngle(float spotting)
	{
		float clamped = Mathf.Clamp(spotting, 0f, DetectionStatMax);
		float angle = BaseAwarenessAngleDeg + Mathf.FloorToInt(clamped / AwarenessAnglePerSpottingStep) * AwarenessAngleStepDeg;
		return Mathf.Min(angle, MaxAwarenessAngleDeg);
	}

	// 인지각이 시야각(120도)에 도달하면 시야 범위 전체가 인지 범위화된다(01장 4절).
	public static bool IsAwarenessAngleMaxed(float spotting) => AwarenessAngle(spotting) >= BaseViewAngleDeg;

	// ─────────────────────────── 7장. 시야 범위 내 임시 위험도/흥미도 ───────────────────────────
	// 시야 범위 안 + 인지 범위 밖 + 비어있지 않은 타일 → +5. 그 외(빈 타일이거나 인지 범위 진입)는 0(제거).
	public static float TempWeightForVisionOnlyTile(bool tileHasContent) => tileHasContent ? NonEmptyTileTempWeight : 0f;

	// ─────────────────────────── 9장/10장. 가시성 ───────────────────────────
	// 대상 기본 가시성: 벽=0, 일반 타일=100, 유닛/오브젝트가 있으면 그 대상 고유값이 타일 가시성을 무효화한다.
	public static float ResolveBaseVisibility(bool isWall, float? occupantBaseVisibility)
	{
		if (occupantBaseVisibility.HasValue) return Mathf.Clamp(occupantBaseVisibility.Value, VisibilityMin, VisibilityMax);
		return isWall ? WallVisibility : NormalTileVisibility;
	}

	// 최종 가시성 = 기본 가시성 - 은신 + 공격 후 상승분(진행 중일 때만), 0~100 클램프.
	// 거리 감쇠의 정확한 산식은 05-A_은신·가시성_스테이터스연동_연산공식 문서가 다루는데 이 폴더에는
	// 아직 없어(01/01-A만 존재) "은신만 단순 차감"하는 가장 단순한 기본값으로 스텁 구현한다.
	//
	// 01장 11절(2026-07-13 개정 "시야 판정 불가 오브젝트")은 두 갈래로 나뉜다 — 이 함수(가시성 수치)는
	// 그중 "시야 판정 불가 오브젝트"(=미인식) 쪽에만 쓰인다:
	//   1. 완전 차단 오브젝트 — 벽처럼 레이 자체를 물리적으로 막는다(쉐도우 캐스팅). 이 함수의 가시성
	//      수치와 무관한 별개 속성(InteractableObject.IsFullyBlocking)이 결정하며, 판정 지점은
	//      UnitFunction.CastRay다.
	//   2. 시야 판정 불가(=미인식) 오브젝트/유닛 — 구조물이 아닌 일반 대상. 레이는 막지 않되, 이
	//      함수가 계산한 최종 가시성이 0 이하면 그 대상 자신만 인지 실패("미인식")로 처리한다
	//      (01장 5절 마지막 규칙). 판정 지점도 동일하게 UnitFunction.CastRay.
	public static float FinalVisibility(float baseVisibility, float stealth, bool attackBoosted)
	{
		float value = baseVisibility - stealth;
		if (attackBoosted) value += AttackVisibilityBoostAmount;
		return Mathf.Clamp(value, VisibilityMin, VisibilityMax);
	}

	// ─────────────────────────── 13장. 특수 원형 인지 범위 ───────────────────────────
	// 엘리트/네메시스/보스 전용(01장 15절) — 이 코드베이스엔 전용 유닛 타입이 없어 Unit.isSpecialUnit
	// 플래그(가중치 문서 7-1장, 보스/네메시스 등 특수 유닛 판정에 이미 쓰이는 값)를 그대로 재사용한다.
	public static int CircularPerceptionRadius(float spotting)
	{
		if (spotting >= 60f) return SpecialCircularMaxRadius;
		if (spotting >= 30f) return 2;
		return SpecialCircularBaseRadius;
	}

	// ─────────────────────────── 11장. 시야 방향 전환 우선순위 ───────────────────────────
	// 숫자가 작을수록 우선순위 높음. 0=플레이어 명령(항상 최우선, 표 밖 특례), 1~10은 표 그대로.
	public static int PriorityRank(VisionDirectionReason reason) => reason switch
	{
		VisionDirectionReason.PlayerCommand => 0,
		VisionDirectionReason.SkillUse => 1,
		VisionDirectionReason.HighThreatSurprise => 2,
		VisionDirectionReason.AdjacentMeleeTarget => 3,
		VisionDirectionReason.CurrentAttackTarget => 4,
		VisionDirectionReason.LeaderCommand => 5,
		VisionDirectionReason.NormalSurprise => 6,
		VisionDirectionReason.SoundDetected => 7,
		VisionDirectionReason.UnconfirmedTile => 8,
		VisionDirectionReason.Alert => 9,
		VisionDirectionReason.Moving => 10,
		_ => int.MaxValue,
	};

	// 고위험 기습 판정: 기습 피해 기대값(또는 실제 피해)이 현재 근접 유닛 공격 기대값의 2배 이상인지.
	public static bool IsHighThreatSurprise(float surpriseDamage, float currentMeleeExpectedDamage)
		=> surpriseDamage >= currentMeleeExpectedDamage * SurpriseHighThreatMultiplier;

	// Dir 8방향 사이의 최소 회전 단계 수(0~4). 우선순위가 동률인 후보가 여럿일 때 "기존 시야 방향에
	// 가장 가까운 후보"를 고르는 데 쓴다(11장 마지막 규칙).
	public static int DirStepDistance(Dir a, Dir b)
	{
		int diff = Mathf.Abs((int)a - (int)b);
		return Mathf.Min(diff, 8 - diff);
	}

	public readonly struct VisionDirectionCandidate
	{
		public readonly VisionDirectionReason Reason;
		public readonly Dir Direction;
		public VisionDirectionCandidate(VisionDirectionReason reason, Dir direction)
		{
			Reason = reason;
			Direction = direction;
		}
	}

	// candidates: 이번 판정 시점에 "적용 가능한" 후보만 호출부가 미리 걸러서 넘긴다 — "방향 정보가
	// 없거나, 대상이 사라졌거나, 현재 행동 때문에 적용할 수 없는 후보는 우선순위 비교에서 제외한다"
	// (11장)는 규칙은 이 함수가 아니라 후보 목록을 구성하는 호출부의 책임이다.
	// currentDir: 동일 우선순위 후보가 여럿일 때 더 가까운 쪽을 고르기 위한 기존 시야 방향.
	public static Dir ResolveVisionDirection(IReadOnlyList<VisionDirectionCandidate> candidates, Dir currentDir)
	{
		if (candidates == null || candidates.Count == 0) return currentDir;

		int bestRank = candidates.Min(c => PriorityRank(c.Reason));
		var top = candidates.Where(c => PriorityRank(c.Reason) == bestRank).ToList();
		if (top.Count == 1) return top[0].Direction;

		return top.OrderBy(c => DirStepDistance(c.Direction, currentDir)).First().Direction;
	}
}
