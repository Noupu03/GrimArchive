using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 시야-인지-반응 시스템(01장)의 순수 계산 함수 모음(부수효과 없음, WeightMath와 동일 컨벤션) —
// 저장/조회는 Unit/UnitFunction 담당. 세부 공식이 위임된 문서(03~10번)가 아직 없는 지점은 단순 기본값 스텁.
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

	// 03문서 4-6장: 수상한 타일 이동 시 가시성 +20은 단일 누적값이 아니라 증가분마다 개별 5초씩 유지된다
	// (Unit.suspiciousMoveBoostTimers가 증가분별 잔여시간을 리스트로 관리).
	public const float SuspiciousMoveBoostDurationSeconds = 5f;

	public const float SurpriseHighThreatMultiplier = 2f; // 기습 방향 전환 기준: 근접 공격 기대값의 2배 이상

	public const int SpecialCircularBaseRadius = 1;
	public const int SpecialCircularMaxRadius = 3;

	// 시야 범위 내 비어있지 않은 타일에 부여되는 임시 위험도/흥미도(7장, 일회성 조회값) — 탐색 방향
	// 판단에는 이미 쓰이지만, 경로 판단(크기 비교)은 10_목표설정 문서 부재로 미연결, 숫자만 미리 채워둠.
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

	// 최종 가시성 = 기본 가시성 - 은신 + 공격 후 상승분, 상한 100만 클램프(거리 감쇠는 미작성 05-A
	// 문서 몫이라 미반영). 하한은 클램프하지 않는다 — 8장 감지/정신력 보정이 음수 여유분을 그대로
	// 써야 하기 때문(하한 클램프는 PerceptionMath 확률표 조회 쪽 몫).
	public static float FinalVisibility(float baseVisibility, float stealth, bool attackBoosted, float suspiciousMoveBoost = 0f)
	{
		float value = baseVisibility - stealth;
		if (attackBoosted) value += AttackVisibilityBoostAmount;
		value += suspiciousMoveBoost; // 4-6장: 이동당 +20씩(개별 5초 유지) 누적된 값을 그대로 더한다.
		return Mathf.Min(value, VisibilityMax); // 하한은 클램프하지 않음 — 위 주석 참고.
	}

	// ─────────────────────────── 02문서 6장. 오브젝트 유형별 가시성 ───────────────────────────
	// 시체/전멸 흔적은 가시성 100 고정(기본값 0이면 영구 미인식되는 문제 방지), 나머지는 오브젝트의
	// BaseVisibility 사용. Tags는 계층형 문자열이라 부분 일치로 검사한다.
	public static float ResolveObjectVisibility(float baseVisibility, List<string> tags)
	{
		if (tags != null && (tags.Any(t => t.Contains("Corpse")) || tags.Any(t => t.Contains("WipeoutTrace"))))
			return NormalTileVisibility;
		return Mathf.Clamp(baseVisibility, VisibilityMin, VisibilityMax);
	}

	// ─────────────────────────── 13장. 특수 원형 인지 범위 ───────────────────────────
	// 엘리트/네메시스/보스 전용(01장 15절) — 전용 유닛 타입이 없어 가중치 문서 7-1장의
	// Unit.isSpecialUnit 플래그를 재사용한다.
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

	// candidates는 호출부가 적용 가능한 후보만 걸러서 넘긴다. 매 틱 도는 핫패스라 LINQ 대신 GC 할당
	// 없는 수동 2-패스 루프 사용(최소 랭크 → 동률이면 currentDir에 가장 가까운 방향 순).
	public static Dir ResolveVisionDirection(IReadOnlyList<VisionDirectionCandidate> candidates, Dir currentDir)
	{
		if (candidates == null || candidates.Count == 0) return currentDir;

		int bestRank = int.MaxValue;
		for (int i = 0; i < candidates.Count; i++)
		{
			int rank = PriorityRank(candidates[i].Reason);
			if (rank < bestRank) bestRank = rank;
		}

		Dir bestDir = currentDir;
		int bestDist = int.MaxValue;
		for (int i = 0; i < candidates.Count; i++)
		{
			if (PriorityRank(candidates[i].Reason) != bestRank) continue;
			int dist = DirStepDistance(candidates[i].Direction, currentDir);
			if (dist < bestDist)
			{
				bestDist = dist;
				bestDir = candidates[i].Direction;
			}
		}
		return bestDir;
	}

	// ─────────────────────────── 대칭 쉐도우 캐스팅 (Symmetric Shadow Casting) ───────────────────────────
	// Albert Ford의 Symmetric Shadowcasting — 기존 72레이 DDA와 달리 대칭성(A가 B를 보면 B도 A를 봄)이
	// 보장되고 Wall Dilation 없이 인접 벽이 가시 집합에 자연히 포함된다. result에 가시 타일을 기록하며,
	// 호출자가 미리 Clear()해야 한다.
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

	// 옥탄트 스캔용 재사용 스크래치 스택(매 호출 new Stack<>() 방지) — "순수 계산" 원칙을 지키기 위해
	// 일반 static 대신 [ThreadStatic]으로 스레드 간 부수효과를 차단한다.
	[System.ThreadStatic]
	private static Stack<(int depth, float start, float end)> _octantScanStack;

	// 한 옥탄트를 행(depth)별로 스캔한다. 스택 기반 반복으로 재귀 스택 오버플로우를 방지한다.
	private static void ScanOctant(
		Vector2Int origin, int maxRadius, int octant,
		int startDepth, float startSlope, float endSlope,
		System.Func<Vector2Int, bool> isOpaque,
		HashSet<Vector2Int> result)
	{
		var stack = _octantScanStack ??= new Stack<(int depth, float start, float end)>();
		stack.Clear();
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
