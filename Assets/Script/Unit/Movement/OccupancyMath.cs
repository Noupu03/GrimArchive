using UnityEngine;

// 점유 충돌 순수 계산(검증 04-05~04-07, 04번 문서 5~8장). 부수효과 없음 — 호출부(OccupancySystem)가 유닛·세션에서 값을 읽어 넘긴다.

// 다음 걸음 자리를 차지한 같은 진영 유닛의 상태(04번 5장 표 "점유 유닛 상태"). 교전·시전·앞 유닛도 막힘 등 "비워질 시간을 알 수 없는" 경우는 Busy/IdleInPlace로 모은다.
public enum OccupantKind
{
	Moving,      // 이미 이동 중인 아군 — 비켜 달라는 새 명령 없이 실제 이동을 기다리거나 우회
	Interacting, // 상호작용(조사·함정 해제·파괴)을 수행 중인 아군 — 남은 시간을 대기 예상에 반영
	Busy,        // 전투·시전·다른 유닛을 기다리는 대기 등 — 통로 때문에 취소시키지 않고, 끝나는 시간은 모른다
	IdleInPlace, // 전투·상호작용·이동 없이 제자리에서 대기 중 — 비켜 주기 판단 대상
}

// 대기와 우회 중 지금 선택한 방식(04번 6장 "동률이면 현재 선택 유지, 아직 선택 전이면 원래 경로에서 대기").
public enum OccupancyChoice { None, Wait, Detour }

// 좁은 통로 통과 순서 비교 키(04번 8장): 역할 순위 → 현재 HP 비율(높은 쪽 먼저) → 유닛당 한 번 뽑아 유지하는 무작위 값. Id는 위 셋이 모두 같을 때 양쪽이 같은 결론을 내게 하는 최후 고정 키다.
public struct PassKey
{
	public int Rank;
	public float HpRatio;
	public int TieBreak;
	public int Id;

	public PassKey(int rank, float hpRatio, int tieBreak, int id)
	{
		Rank = rank; HpRatio = hpRatio; TieBreak = tieBreak; Id = id;
	}
}

public static class OccupancyMath
{
	// 유닛 한 걸음에 걸리는 시간(초). 속도가 0 이하면 매우 느린 값으로 고정해 0 나눗셈을 막는다.
	public const float MinSpeed = 0.1f;
	public static float StepSeconds(float speed) => 1f / Mathf.Max(MinSpeed, speed);

	// 조사·함정 해제의 남은 시간 = (1 − 진행도) × 지속시간. 진행도는 0~1로 고정한다.
	public static float InteractionRemainingSeconds(float progress01, float durationSeconds)
		=> Mathf.Max(0f, (1f - Mathf.Clamp01(progress01)) * durationSeconds);

	// 대기 예상시간(초) — 04번 6장. null = 알 수 없음(교전 중·시전 중·제자리 대기·앞 유닛도 막힘·파괴 중 등, 6장 "타일이 비워질 시간을 알 수 없는 경우").
	// Moving은 점유자가 다음 걸음에 비켜난다고 보고 한 걸음 시간, Interacting은 남은 시간 + 한 걸음(상호작용이 끝나도 유닛이 실제로 자리를 떠나야 열린다 — 7장).
	public static float? EstimateWaitSeconds(OccupantKind kind, float occupantStepSeconds, float? interactionRemainingSeconds)
	{
		switch (kind)
		{
			case OccupantKind.Moving:
				return occupantStepSeconds;
			case OccupantKind.Interacting:
				return interactionRemainingSeconds.HasValue ? interactionRemainingSeconds.Value + occupantStepSeconds : (float?)null;
			default:
				return null;
		}
	}

	private const float TieEpsilonSeconds = 0.001f;

	// 대기 경로 예상시간 = 통로가 열릴 때까지의 대기시간 + 개방 뒤 남은 이동시간(구조 경로), 우회 경로 예상시간 = 허용 우회로 목적지까지 이동하는 시간(04번 6장).
	// 대기시간을 알면 짧은 쪽, 동률이면 현재 선택 유지(선택 전이면 원래 경로에서 대기). 모르면 우회가 있을 때 우회, 없으면 대기한다.
	public static OccupancyChoice Decide(float? waitSeconds, float structuralPathSeconds, float? detourSeconds, OccupancyChoice current)
	{
		if (!waitSeconds.HasValue) return detourSeconds.HasValue ? OccupancyChoice.Detour : OccupancyChoice.Wait;
		if (!detourSeconds.HasValue) return OccupancyChoice.Wait;

		float waitPath = waitSeconds.Value + structuralPathSeconds;
		float detourPath = detourSeconds.Value;
		if (Mathf.Abs(waitPath - detourPath) < TieEpsilonSeconds)
			return current == OccupancyChoice.None ? OccupancyChoice.Wait : current;
		return waitPath < detourPath ? OccupancyChoice.Wait : OccupancyChoice.Detour;
	}

	// 같은 점유자·같은 충돌이 이어지는 동안은 마지막 선택을 재판정 시각까지 유지한다(매 틱 뒤집히지 않게).
	public static bool ShouldReevaluate(float now, float reevaluateAt) => now >= reevaluateAt;

	// 해결되지 않는 미확인 대기의 상한 — 대기 시간을 모르고 우회도 없는 채로 maxSeconds를 넘기면 대기를 풀어 기존 경로(행동별 정체 인내 → 9번 후속 행동)로 넘긴다.
	// 대기시간을 아는 경우(상호작용 남은 시간 등)는 끊지 않는다.
	public static bool UnknownWaitExpired(float now, float startedAt, float maxSeconds, bool waitKnown)
		=> !waitKnown && now - startedAt >= maxSeconds;

	// ── 04번 8장 좁은 통로 통과 순서 ──

	// 진입 역할 순위(작을수록 먼저): 전방 근접 전사·탱커 → 근접 지원 → 원거리 공격 → 원거리 지원. CombatRole은 CombatScoreMath.ResolveCombatRole이 정한다.
	public static int EntryRank(CombatRole role)
	{
		switch (role)
		{
			case CombatRole.MeleeTank:
			case CombatRole.MeleeDps:
				return 0;
			case CombatRole.MeleeSupport:
				return 1;
			case CombatRole.RangedDps:
				return 2;
			default: // RangedSupport
				return 3;
		}
	}

	private const float HpTieEpsilon = 0.0001f;

	// 음수 = a가 먼저, 양수 = b가 먼저, 0 = 완전 동일(같은 유닛). 같은 역할 순위 안에서는 HP 비율이 높은 유닛이 먼저, HP 비율도 같으면 유닛당 한 번 뽑은 무작위 값이 작은 쪽이 먼저
	// (매 판단 재추첨하지 않아 순서가 흔들리지 않는다). 리더도 자기 역할 순서를 따르므로 리더 예외는 없다.
	public static int ComparePassPriority(PassKey a, PassKey b)
	{
		if (a.Rank != b.Rank) return a.Rank < b.Rank ? -1 : 1;
		if (Mathf.Abs(a.HpRatio - b.HpRatio) > HpTieEpsilon) return a.HpRatio > b.HpRatio ? -1 : 1;
		if (a.TieBreak != b.TieBreak) return a.TieBreak < b.TieBreak ? -1 : 1;
		if (a.Id != b.Id) return a.Id < b.Id ? -1 : 1;
		return 0;
	}

	// 마주 막혔을 때 "내가 물러나야 하는가"(04번 8장 "통로 이탈과 마주 막힘"). 상대가 이미 통로 안에서 이쪽으로 빠져나오는 중이면 그 이탈이 먼저라 내가 양보하고,
	// 그 외에는 규칙상 허용되는 빈 타일로 물러날 수 있는 쪽이 양보한다. 둘 다 물러날 수 있으면 통과 우선순위가 낮은 쪽이, 둘 다 못 물러나면 아무도 물러나지 않는다(호출부가 대기·다른 경로를 판단).
	public static bool ShouldIYield(bool otherExitingTowardsMe, bool iCanRetreat, bool otherCanRetreat, PassKey me, PassKey other)
	{
		if (otherExitingTowardsMe) return true;
		if (iCanRetreat && !otherCanRetreat) return true;
		if (!iCanRetreat && otherCanRetreat) return false;
		if (!iCanRetreat) return false; // 둘 다 못 물러남
		return ComparePassPriority(me, other) > 0; // 둘 다 가능 — 우선순위 낮은 쪽이 양보
	}
}
