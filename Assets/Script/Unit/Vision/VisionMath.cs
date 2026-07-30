using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 시야-인지-반응 시스템(01_시야·인지범위·가시성)의 순수 계산 함수 모음. 부수효과 없음(테스트 용이) —
// WeightMath와 동일한 컨벤션. 저장/조회는 Unit(런타임 상태)과 UnitFunction(호출부)이 담당하고
// 이 클래스는 숫자만 계산한다.
//
// 이 폴더(Assets/문서/공식문서/시야인지반응)에는 현재 00(상위구조)/01(개념)/01-A(연산공식)/
// 02(인지·정보판정·실패처리, 2026-07-20 추가 — 실제 구현은 PerceptionMath.cs)까지 있다.
// 03~10번 문서(가중치판단 연동, 탐색 반응, 은신 세부산식, 전투 반응, 소리 등)는 아직 작성되지
// 않았으므로, 그 문서들에 위임된 세부 공식(예: 은신/거리/가림 보정의 정확한 감쇠식)은 CLAUDE.md에
// 명시된 기존 관례대로 "가장 단순하고 합리적인 기본값"으로만 스텁 처리한다. 각 스텁 지점에 주석으로
// 근거를 남긴다.
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

	// 03문서 4-6장(2026-07-27 개정): 수상한 타일 대상이 1칸 이동할 때마다 가시성이 임시로 +20 증가하며,
	// 이 증가분은 "누적된 하나의 값"이 아니라 증가 시점부터 각각 개별 5초 유지된다(예: 3초 간격으로
	// 두 번 이동하면 3초 뒤엔 +40, 8초 뒤엔 첫 증가분만 사라져 +20, 이후 다시 사라져 0). Unit.cs의
	// suspiciousMoveBoostTimers(VisionStatComponent)가 증가분별 잔여시간을 리스트로 들고 있다.
	public const float SuspiciousMoveBoostDurationSeconds = 5f;

	public const float SurpriseHighThreatMultiplier = 2f; // 기습 방향 전환 기준: 근접 공격 기대값의 2배 이상

	public const int SpecialCircularBaseRadius = 1;
	public const int SpecialCircularMaxRadius = 3;

	// 시야 범위 내 "비어있지 않은 타일"에 부여되는 임시 위험도/흥미도(7장). 저장값이 아니라 경로/탐색
	// 방향 판단에만 쓰는 일회성 조회값. "탐색 방향 판단" 절반은 2026-07-20에 실제로 연결됐다 —
	// UnitFunction.ResolveVisionDirection()이 Unit.visionOnlyNonEmptyTiles(이 값이 +5 붙는 대상 자체)
	// 존재 여부로 UnconfirmedTile(8순위) 시야 방향 후보를 만든다. 다만 그 연결은 리스트의 "존재
	// 여부"만 쓰지 이 숫자(TempWeightForVisionOnlyTile 반환값)를 직접 소비하지는 않는다 — 우선순위
	// 랭크 기반 시야 방향 시스템엔 크기 비교가 없어 방향 후보 자체는 불리언 신호만으로 충분하기
	// 때문이다. "경로 판단"(크기 비교가 실제로 필요한 절반)은 여전히 10_목표설정·이동경로·재설정
	// 문서(소비자) 부재로 미연결 — 그 문서가 생기면 이 숫자를 그대로 쓸 수 있게 남겨둔다.
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

	// 최종 가시성 = 기본 가시성 - 은신 + 공격 후 상승분(진행 중일 때만), 상한만 100으로 클램프.
	// 거리 감쇠의 정확한 산식은 05-A_은신·가시성_스테이터스연동_연산공식 문서가 다루는데 이 폴더에는
	// 아직 없어(01/01-A만 존재) "은신만 단순 차감"하는 가장 단순한 기본값으로 스텁 구현한다.
	//
	// 하한(0)은 클램프하지 않는다 — 02문서 7장이 "은신 스탯에는 최대치 제한이 없어 은신 기반 가시성은
	// 0 미만으로 내려갈 수 있다"면서 "계산식 안에서는 음수 값을 유지한다"고 명시적으로 요구한다(예시:
	// 은신 기반 가시성 -50 + 감지 보정 +40 + 공격 후 가시성 증가 +10 = 최종 계산 가시성 0). 여기서
	// 하한을 0으로 미리 클램프해버리면(2026-07-20 이전 버그) 이 함수가 반환한 값에 나중에 02문서 8장의
	// 감지/정신력 보정을 더할 때 음수 "여유분"이 이미 사라진 뒤라 문서 예시와 다른(더 높은) 값이
	// 나온다 — 최종 판정용 확률표 조회(PerceptionMath.OutcomeProbabilities)는 이미 "0 이하"를 별도
	// 구간으로 처리하므로, 하한 클램프는 이 함수가 아니라 그쪽에서만 의미상 일어나면 된다.
	//
	// 01장 11절(2026-07-13 개정 "시야 판정 불가 오브젝트")은 두 갈래로 나뉜다 — 이 함수(가시성 수치)는
	// 그중 "시야 판정 불가 오브젝트"(=미인식) 쪽에만 쓰인다:
	//   1. 완전 차단 오브젝트 — 벽처럼 시야를 물리적으로 막는다(쉐도우 캐스팅 isOpaque). 이 함수의
	//      가시성 수치와 무관한 별개 속성(InteractableObject.IsFullyBlocking)이 결정하며, 판정 지점은
	//      UnitFunction.UpdateFOV의 isOpaque 클로저다.
	//   2. 시야 판정 불가(=미인식) 오브젝트/유닛 — 구조물이 아닌 일반 대상. 시야는 막지 않되, 이 함수가
	//      계산한 값에 02문서 8장 보정을 더한 최종 계산 가시성으로 12장 확률표를 굴려 인지 결과(정확
	//      인지/수상한 타일/미인식)를 정한다(UnitFunction.ForceRollPerception). 판정 지점은 ProcessTile.
	public static float FinalVisibility(float baseVisibility, float stealth, bool attackBoosted, float suspiciousMoveBoost = 0f)
	{
		float value = baseVisibility - stealth;
		if (attackBoosted) value += AttackVisibilityBoostAmount;
		value += suspiciousMoveBoost; // 4-6장: 이동당 +20씩(개별 5초 유지) 누적된 값을 그대로 더한다.
		return Mathf.Min(value, VisibilityMax); // 하한은 클램프하지 않음 — 위 주석 참고.
	}

	// ─────────────────────────── 02문서 6장. 오브젝트 유형별 가시성 ───────────────────────────
	// 시체/전멸 흔적은 가시성 100 고정(오브젝트 자신의 BaseVisibility와 무관 — 팀 기존 관례상
	// InteractableObject.BaseVisibility 기본값이 0이라 그대로 두면 시체/전멸흔적이 영원히 미인식
	// 처리되는데, 02문서 6장이 명시적으로 이 둘을 100 고정으로 지정하므로 그 규칙을 그대로 따른다).
	// 함정/특정 건물/일반 루팅은 기존과 동일하게 오브젝트 자신의 BaseVisibility(기본 가시성)를 쓴다.
	// Tags는 "Object/Passable/Corpse"류 계층형 문자열이라 부분 일치로 검사한다(PersonalMapKnowledge.
	// RegisterObject와 동일한 2026-07-20 수정 — 기존 정확 일치는 항상 false였다).
	public static float ResolveObjectVisibility(float baseVisibility, List<string> tags)
	{
		if (tags != null && (tags.Any(t => t.Contains("Corpse")) || tags.Any(t => t.Contains("WipeoutTrace"))))
			return NormalTileVisibility;
		return Mathf.Clamp(baseVisibility, VisibilityMin, VisibilityMax);
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

	// ─────────────────────────── 대칭 쉐도우 캐스팅 (Symmetric Shadow Casting) ───────────────────────────
	// Albert Ford의 Symmetric Shadowcasting 알고리즘. 72레이 DDA + Wall Dilation 방식에 비해:
	//   • 대칭성 보장: A가 B를 보면 B도 A를 본다(DDA는 레이 방향에 따라 비대칭 발생 가능).
	//   • Wall Dilation 불필요: 인접 벽이 자연스럽게 가시 집합에 포함된다.
	//   • O(시야 내 타일 수): 시야 반경²에 비례하는 고정 복잡도(72 × 시야거리 vs. π × 반경²).
	// result에 가시 타일(Vector2Int)을 기록한다. 호출자가 미리 Clear()해야 한다.
	public static void SymmetricShadowCast(
		Vector2Int origin,
		int maxRadius,
		System.Func<Vector2Int, bool> isOpaque,
		HashSet<Vector2Int> result)
	{
		result.Add(origin);
		for (int octant = 0; octant < 8; octant++)
			ScanOctant(origin, maxRadius, octant, 1, 0f, 1f, isOpaque, result);
	}

	// 8방향 옥탄트 변환: tile = origin + depth*(dr,dc) + col*(cr,cc)
	// oct 0: 주=+X 부=+Y(0°~45°)  oct 1: 주=+Y 부=+X(45°~90°)
	// oct 2: 주=+Y 부=-X(90°~135°) oct 3: 주=-X 부=+Y(135°~180°)
	// oct 4: 주=-X 부=-Y(180°~225°) oct 5: 주=-Y 부=-X(225°~270°)
	// oct 6: 주=-Y 부=+X(270°~315°) oct 7: 주=+X 부=-Y(315°~360°)
	private static readonly (int dr, int dc, int cr, int cc)[] _octantXforms = {
		( 1,  0,  0,  1), ( 0,  1,  1,  0), ( 0,  1, -1,  0), (-1,  0,  0,  1),
		(-1,  0,  0, -1), ( 0, -1, -1,  0), ( 0, -1,  1,  0), ( 1,  0,  0, -1),
	};

	private static Vector2Int OctantTile(Vector2Int o, int depth, int col, int octant)
	{
		var (dr, dc, cr, cc) = _octantXforms[octant];
		return new Vector2Int(o.x + depth * dr + col * cr, o.y + depth * dc + col * cc);
	}

	// 한 옥탄트를 행(depth)별로 스캔한다. 스택 기반 반복으로 재귀 스택 오버플로우를 방지한다.
	private static void ScanOctant(
		Vector2Int origin, int maxRadius, int octant,
		int startDepth, float startSlope, float endSlope,
		System.Func<Vector2Int, bool> isOpaque,
		HashSet<Vector2Int> result)
	{
		var stack = new Stack<(int depth, float start, float end)>();
		stack.Push((startDepth, startSlope, endSlope));

		while (stack.Count > 0)
		{
			var (depth, start, end) = stack.Pop();
			if (depth > maxRadius) continue;

			int minCol = Mathf.Max(0, Mathf.FloorToInt(depth * start));
			int maxCol = Mathf.Min(depth, Mathf.CeilToInt(depth * end));

			float runStart = start;
			bool prevOpaque = false;
			bool hasPrev    = false;

			for (int col = minCol; col <= maxCol; col++)
			{
				bool inCircle = depth * depth + col * col <= maxRadius * maxRadius;
				Vector2Int tile = OctantTile(origin, depth, col, octant);
				bool opaque = !inCircle || isOpaque(tile);

				// 대칭 가시성 조건(Albert Ford): 불투명 타일은 항상 표시, 투명 타일은 슬로프 구간 안일 때만.
				bool symmetric = inCircle && (float)col >= depth * start && (float)col <= depth * end;
				if (inCircle && (opaque || symmetric))
					result.Add(tile);

				if (hasPrev)
				{
					if (!prevOpaque && opaque)
					{
						// 바닥→벽 전환: 벽 왼쪽 엣지까지 다음 depth로 전파
						float wallLeft = (col - 0.5f) / depth;
						if (wallLeft > runStart)
							stack.Push((depth + 1, runStart, wallLeft));
					}
					else if (prevOpaque && !opaque)
					{
						// 벽→바닥 전환: 이전 벽의 오른쪽 엣지부터 새 투명 구간 시작
						runStart = (col - 0.5f) / depth;
					}
				}

				prevOpaque = opaque;
				hasPrev    = true;
			}

			// 이 행이 투명 타일로 끝났으면 다음 depth로 계속 전파
			if (hasPrev && !prevOpaque)
				stack.Push((depth + 1, runStart, end));
		}
	}
}
