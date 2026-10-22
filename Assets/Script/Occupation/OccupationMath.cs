using System.Collections.Generic;

// 인류 점령 파티의 거점·건설·진출 판정(순수) — 코어 점령 설계명세서 v0.2 12~14장. 거점 = 기초 시설을 완공한 방, 예정 방어 시설까지 완공하면 방어 거점이다.
public enum BaseStage
{
	None,    // 거점 아님(기초 시설 미완공 — 점령 작업 진행 중이거나 손대지 않은 방)
	Basic,   // 기초 거점 — 후퇴·휴식 가능
	Defense, // 방어 거점 — 예정 방어 시설까지 완공, 후퇴 목적지로 우선 검토
}

// 진출 후보 방 하나의 요약 — OccupationSystem이 개인 지도·방 그래프에서 채운다.
public struct AdvanceCandidate
{
	public int RoomId;
	public bool IsHumanBase;       // 이미 인류 거점이면 "진출"이 아니다
	public bool DangerKnown;       // 리더의 개인 지도에 위험도 기록이 있는가
	public float Danger;           // DangerKnown일 때만 의미가 있다
	public int HopsFromExit;       // 출구 방에서의 방 그래프 거리 — 클수록 전방
}

public static class OccupationMath
{
	// 초기 주둔 거점 상한 — 던전 전체에서 모든 인류 집단이 공유한다(14장, 회의록 10.19 "현재 2층까지는 2개"). 후반 확대 시점·최대치는 문서에 없다.
	public const int GarrisonBaseCap = 2;

	// 건설 작업량(자리표시자 — 문서가 시간·비용을 정하지 않음, 12장 "건설 설계에서 정한다"). 건설 인원 1명이 초당 1씩 채운다: 5명이면 30초.
	public const float BasicWorkUnits = 150f;
	public const float DefenseWorkUnits = 150f;

	// 1명이 1초에 채우는 작업량 × 지금 현장에서 일하는 인원 × 경과 시간.
	public static float WorkDelta(int workers, float deltaSeconds) => workers <= 0 || deltaSeconds <= 0f ? 0f : workers * deltaSeconds;

	public static float Progress01(float done, float total) => total <= 0f ? 1f : (done <= 0f ? 0f : done >= total ? 1f : done / total);

	public static bool IsWorkComplete(float done, float total) => done >= total;

	// 피격 직후 공사를 멈춰 두는 시간(자리표시자 — 문서가 정하지 않음). 피격은 한 행동 틱만 켜지는 표식이라 공사 틱이 놓칠 수 있어, 맞은 시각을 기억해 이 시간 동안 멈춘다. 대개 그 사이 전투·경계 상태가 이어받는다.
	public const float HitPauseSeconds = 2f;

	// 거점 공사를 멈춰야 하는가 — 파티원 누구든 전투·경계·합류 대기·적 인지 중이거나(anyEngaged) 방금 맞았으면 멈춘다(진행도는 남는다). 한 번도 안 맞았으면 secondsSinceLastHit은 +무한대.
	public static bool ShouldPauseConstruction(bool anyEngaged, float secondsSinceLastHit) => anyEngaged || secondsSinceLastHit < HitPauseSeconds;

	// 점령 파티의 방 활동 종료 기준(01번 7-1장 Occupy) — 현재 방이 기초 거점이면 끝이다. 방어 시설은 방어 거점으로 정한 방에서 후퇴·휴식 뒤에 짓는 것이라(13장) 전진을 막지 않는다.
	public static bool IsRoomActivityComplete(BaseStage stage) => stage != BaseStage.None;

	// 주둔 거점을 하나 더 둘 수 있는가 — 새로 건설을 시작하는 때부터 수에 들어가므로 countedBases는 건설 중인 것까지 센 값이다(14-3장).
	public static bool CanAddGarrisonBase(int countedBases, int cap = GarrisonBaseCap) => countedBases < cap;

	// 거점 후퇴 뒤 재출격 전에 방어 시설을 지어야 하는가 — 방어 거점으로 정한 방이 아직 방어 시설을 완공하지 못했다(13장 "추가 건설").
	public static bool NeedsDefenseBuild(BaseStage stage, bool defenseDesignated) => defenseDesignated && stage == BaseStage.Basic;

	// 정원이 가득 찼고 아직 방어 거점으로 정한 방이 없으면, 거점 상한에 여유가 있을 때 가장 가까운 유효한 기초 거점을 다음 방어 거점으로 정한다(14장 표 둘째 행).
	// 정할 후보가 이미 주둔 거점으로 세어지고 있으면(candidateAlreadyCounted) 새 거점을 더하는 게 아니라 상한을 쓰지 않는다.
	public static bool ShouldDesignateDefenseBase(int garrison, int capacity, int countedBases, bool anyDesignated, bool candidateAlreadyCounted, int cap = GarrisonBaseCap)
		=> capacity > 0 && garrison >= capacity && !anyDesignated && (candidateAlreadyCounted || CanAddGarrisonBase(countedBases, cap));

	// 진출할 방을 고른다(12-1장). 이미 인류 거점인 방은 후보가 아니고, 위험도가 알려진 방은 안전 상한 이하일 때만 후보다.
	// 위험도를 모르는 방은 allowUnknown일 때만 후보로 보되 알려진 안전한 방보다 뒤에 둔다. 순위: 알려진 위험도 낮은 순 → 전방(출구에서 먼) 순 → RoomId. 고를 곳이 없으면 -1.
	public static int PickAdvance(IList<AdvanceCandidate> candidates, float maxKnownDanger, bool allowUnknown)
	{
		int best = -1;
		for (int i = 0; i < candidates.Count; i++)
		{
			var c = candidates[i];
			if (c.IsHumanBase) continue;
			if (c.DangerKnown ? c.Danger > maxKnownDanger : !allowUnknown) continue;
			if (best < 0 || Better(c, candidates[best])) best = i;
		}
		return best;
	}

	private static bool Better(AdvanceCandidate a, AdvanceCandidate b)
	{
		if (a.DangerKnown != b.DangerKnown) return a.DangerKnown; // 알려진 안전한 방이 먼저
		if (a.DangerKnown && a.Danger != b.Danger) return a.Danger < b.Danger;
		if (a.HopsFromExit != b.HopsFromExit) return a.HopsFromExit > b.HopsFromExit; // 전방으로
		return a.RoomId < b.RoomId;
	}

	// 거점들을 출구까지의 거리로 후방/전방으로 가른다 — 출구에 더 가까운 쪽이 후방, 먼 쪽이 전방이다(회의록 10.19). 거점이 하나뿐이면 그 거점이 전방이자 후방이다(-1 반환 없음: rear == front).
	public static void ClassifyRearFront(IList<int> hopsFromExit, out int rearIndex, out int frontIndex)
	{
		rearIndex = -1;
		frontIndex = -1;
		for (int i = 0; i < hopsFromExit.Count; i++)
		{
			if (rearIndex < 0 || hopsFromExit[i] < hopsFromExit[rearIndex]) rearIndex = i;
			if (frontIndex < 0 || hopsFromExit[i] > hopsFromExit[frontIndex]) frontIndex = i;
		}
	}

	// 방 그래프(간선 = 게이트로 이어진 방 쌍)에서 출구 방으로부터의 방 단위 거리. 닿지 못하는 방은 포함하지 않는다.
	public static Dictionary<int, int> HopsFrom(int startRoomId, IList<(int a, int b)> edges)
	{
		var dist = new Dictionary<int, int> { [startRoomId] = 0 };
		var queue = new Queue<int>();
		queue.Enqueue(startRoomId);
		while (queue.Count > 0)
		{
			int cur = queue.Dequeue();
			foreach (var (a, b) in edges)
			{
				int next = a == cur ? b : b == cur ? a : -1;
				if (next < 0 || dist.ContainsKey(next)) continue;
				dist[next] = dist[cur] + 1;
				queue.Enqueue(next);
			}
		}
		return dist;
	}
}
