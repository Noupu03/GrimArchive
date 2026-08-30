using System.Collections.Generic;
using UnityEngine;

// 07_A_전파·소리·간접입력_연산공식의 순수 계산 함수 모음. VisionMath/PerceptionMath/WeightMath/
// ExplorationMath와 동일 컨벤션(부수효과 없음, 테스트 용이) — 저장/조회/게임 루프 연결은 PropagationSystem
// 담당. 미작성 문서(08/09/06, 함정, 파티 타입)에 위임된 부분(물리적 전파 이동, 합류 5초 초과 처리, 함정
// 피해 위험도 수치 등)은 CLAUDE.md 관례대로 스텁 처리한다.
public static class PropagationMath
{
	// ─────────────────────────── 1장. 전파·소리 공통 공간 판정 ───────────────────────────
	// 동일 방 또는 동일 통로 공간 = 거리 판정 진행, 그 외는 거리와 무관하게 도달 실패 — 방/통로 모두
	// CreateMap.GetRoomIdAt의 roomId로 구분되므로(복도도 고유 roomId) 일치 여부가 곧 "동일 공간"이다.
	public static bool SameSpace(int roomIdA, int roomIdB) => roomIdA >= 0 && roomIdA == roomIdB;

	// 1-2장: 장애물이 없으면 원형 N칸, 있으면 같은 공간 안에서 이동 가능한 최단 경로 거리. 8방향
	// BFS로 두 규칙을 하나로 표현한다 — 장애물이 없으면 BFS 거리가 체비셰프 원형 반경과 같아진다.
	public static bool TryGetSpaceDistance(Vector2Int from, Vector2Int to, int maxRange, System.Func<Vector2Int, bool> isWalkable, out int distance)
	{
		distance = 0;
		if (from == to) return true;
		if (maxRange <= 0 || isWalkable == null) return false;

		var visited = new HashSet<Vector2Int> { from };
		var frontier = new List<Vector2Int> { from };
		int steps = 0;

		while (frontier.Count > 0 && steps < maxRange)
		{
			steps++;
			var next = new List<Vector2Int>();
			foreach (var cur in frontier)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dy = -1; dy <= 1; dy++)
					{
						if (dx == 0 && dy == 0) continue;
						// 코너 커팅 방지 — 대각선 이동 시 양옆 직교 타일 중 하나라도 이동 불가면 블록(UnitFunction.Move()와
						// 동일 규칙). 목표 타일(to) 도달 시에도 예외 없이 적용해야 벽을 대각선으로 통과하는 버그가 안 생긴다.
						if (dx != 0 && dy != 0)
						{
							bool cornerBlocked = !isWalkable(new Vector2Int(cur.x + dx, cur.y)) || !isWalkable(new Vector2Int(cur.x, cur.y + dy));
							if (cornerBlocked) continue;
						}
						var n = new Vector2Int(cur.x + dx, cur.y + dy);
						if (visited.Contains(n)) continue;
						if (n == to) { distance = steps; return true; }
						if (!isWalkable(n)) continue;
						visited.Add(n);
						next.Add(n);
					}
				}
			}
			frontier = next;
		}
		return false;
	}

	// ─────────────────────────── 2장. 전파 범위 ───────────────────────────
	public const int BasePropagationRangeTiles = 3; // 2-1장: 인류 기본 전파 범위
	public const int PackMonsterPropagationRangeTiles = 3; // 2-2장: 전파 가능 무리형 몬스터 일반 기준값

	// 0~24:+0 / 25~49:+1 / 50~74:+2 / 75~99:+3 / 100:+4 — 카리스마(2-1장)와 소리 감지 보정(5장)이
	// 동일한 25단위 구간 표를 쓴다.
	private static int Breakpoint25Bonus(float stat)
	{
		float s = Mathf.Clamp(stat, 0f, 100f);
		if (s >= 100f) return 4;
		if (s >= 75f) return 3;
		if (s >= 50f) return 2;
		if (s >= 25f) return 1;
		return 0;
	}

	public static int CharismaPropagationBonus(float charisma) => Breakpoint25Bonus(charisma);
	public static int PropagationRange(float charisma) => BasePropagationRangeTiles + CharismaPropagationBonus(charisma);

	// ─────────────────────────── 3장. 마지막 확인 위치 갱신 ───────────────────────────
	// 수신 확인 시각이 기존 확인 시각보다 최신일 때만 갱신 — WeightMath.PriorityRank/ShouldReplace(24장)가
	// 이미 이 규칙을 InfoType 우선순위와 함께 처리하므로 여기서는 그대로 위임한다(중복 구현 없음).
	public static bool ShouldUpdateLastKnownPosition(float candidateTimestamp, float existingTimestamp)
		=> candidateTimestamp > existingTimestamp;

	// ─────────────────────────── 4장. 소리 이벤트와 기본 범위 ───────────────────────────
	public static int SoundBaseRange(SoundType type) => type switch
	{
		SoundType.Movement => 2,
		SoundType.AttackExecution => 3,
		SoundType.HitImpact => 5,
		SoundType.HitScream => 6,
		SoundType.Death => 7,
		SoundType.TrapActivation => 5,
		_ => 0,
	};

	// 4-1장: 피격 비명 발생 기준 — 방어/피해 감소 적용 후 최종 HP 감소량 ≥ 최대 HP × 10%.
	public const float HitScreamHpRatioThreshold = 0.10f;
	public static bool IsHitScreamTriggered(float finalHpLoss, float maxHp)
		=> maxHp > 0f && finalHpLoss >= maxHp * HitScreamHpRatioThreshold;

	// ─────────────────────────── 5장. 소리 감지 범위 ───────────────────────────
	public static int SoundDetectionBonus(float detectionStat) => Breakpoint25Bonus(detectionStat);

	// 5-1장: 경계 상태에서는 감지 +20을 먼저 적용한 뒤(상한 100) 범위 보정을 계산한다.
	public const float AlertSoundDetectionBonus = 20f;
	public static float AlertFinalDetection(float baseDetection) => Mathf.Min(baseDetection + AlertSoundDetectionBonus, 100f);

	// 5-2장: 일반 몬스터 청각 보정 기준값 +2칸(종별/개체 데이터가 있으면 그 값을 우선 — 이 코드베이스엔
	// 아직 종별/개체 청각 데이터가 없어 기준값만 사용).
	public const int MonsterHearingBonusTiles = 2;

	public static int SoundDetectionRange(SoundType type, float detectionStat, bool isAlert, bool isMonster, int? monsterHearingBonusOverride = null)
	{
		float effectiveDetection = isAlert ? AlertFinalDetection(detectionStat) : detectionStat;
		int bonus = SoundDetectionBonus(effectiveDetection);
		int monsterBonus = isMonster ? (monsterHearingBonusOverride ?? MonsterHearingBonusTiles) : 0;
		return SoundBaseRange(type) + bonus + monsterBonus;
	}

	// ─────────────────────────── 6장. 소리로 획득하는 방향·추정 지역 ───────────────────────────
	public static int EstimatedAreaRadius(SoundType type) => type switch
	{
		SoundType.AttackExecution => 2,
		SoundType.HitImpact => 2,
		SoundType.HitScream => 2,
		SoundType.Death => 2,
		SoundType.TrapActivation => 1,
		_ => 0, // 이동음은 방향만, 추정 지역 없음
	};

	public static bool HasEstimatedArea(SoundType type) => EstimatedAreaRadius(type) > 0;

	// ─────────────────────────── 7장. 복수 소리 선택·유효시간 ───────────────────────────
	// 숫자가 작을수록 소리 내부 우선순위가 높다(반응 대상 선택 기준일 뿐 전체 행동 우선순위가 아님).
	public static int SoundPriorityRank(SoundType type) => type switch
	{
		SoundType.Death => 1,
		SoundType.HitScream => 1,
		SoundType.HitImpact => 2,
		SoundType.TrapActivation => 3,
		SoundType.AttackExecution => 4,
		SoundType.Movement => 5,
		_ => int.MaxValue,
	};

	// true면 candidate가 현재 확인 중인 소리를 대체해야 함(우선순위가 더 높거나, 같은 순위면서 더
	// 가깝거나, 순위·거리가 모두 같으면서 시야 방향에 더 가까울 때) — 7-2장. 방향 인자 기본값 0은
	// 타이브레이크 비활성화용(호출부가 방향 정보를 안 줄 수도 있음).
	public static bool ShouldReplaceSound(int candidateRank, float candidateDist, int currentRank, float currentDist,
		int candidateDirStepDist = 0, int currentDirStepDist = 0)
	{
		if (candidateRank != currentRank) return candidateRank < currentRank;
		if (candidateDist != currentDist) return candidateDist < currentDist;
		return candidateDirStepDist < currentDirStepDist;
	}

	public const float SoundValidSeconds = 5f;           // 7-3장: 일반 소리 유효시간(확인 행동 시작 기한)
	// 7-3장 "유닛당 보류 소리 1건"은 PendingSound가 컬렉션이 아닌 단일 필드라 데이터 구조 자체로
	// 이미 강제된다(별도 개수 제한 상수 불필요).

	// ─────────────────────────── 8장. 소리 확인 판정·연출 시간 ───────────────────────────
	public const float MoveSoundHoldSeconds = 2f;         // 8-1장: 이동음 반응 후 시야 유지
	public const float EstimatedAreaHoldSeconds = 2f;     // 8-2장: 추정 지역 원인 미인지 시 시야 유지

	// ─────────────────────────── 9장. 적 인지 후 전투 시작·합류 판정 ───────────────────────────
	public const int ImmediateCombatRangeTiles = 2; // 9-1장: 근거리 즉시 전투 기준

	// 9-2장: 거리 2칸 초과일 때 위험도 단계별 처리. stage가 null이면 "미확인"(즉시 전투). Stage3
	// 이상은 문서가 "현재 2단계 규칙 임시 적용"이라고 명시해 Stage2와 동일하게 합류 대기로 묶는다.
	public static bool RequiresJoinWait(DangerStage? stage, int distanceTiles)
	{
		if (distanceTiles <= ImmediateCombatRangeTiles) return false;
		if (stage == null) return false; // 미확인 — 즉시 전투
		return stage == DangerStage.Stage2 || stage == DangerStage.Stage3 || stage == DangerStage.StageMax;
	}

	public const float JoinResponseWaitSeconds = 2f;   // 9-3장: 합류 의사 응답 대기
	public const float ActualJoinMaxWaitSeconds = 5f;  // 9-3장: 실제 합류 최대 대기(임시)

	// ─────────────────────────── 17장. 공격 방향 간접입력 ───────────────────────────
	// 방향을 특정할 수 없는 광역·지면 영역 공격만 방향 정보를 생성하지 않는다.
	public static bool AttackShapeProvidesDirection(AttackShape shape) => shape != AttackShape.AreaGround;
}
