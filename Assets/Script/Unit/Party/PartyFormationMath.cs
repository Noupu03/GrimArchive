using System;
using System.Collections.Generic;
using UnityEngine;

// 파티 진형·집결 자리·진입로 판정 순수 계산(부수효과 없음, Vector2Int만 다룬다). 0층 입구 진형(DungeonEntranceSystem)과 방 문 앞 진형(PartyAdvanceSystem)이
// 같은 랭크/레인 정의를 공유한다 — 근접 0랭크 → 리더 1랭크 → 원거리 2랭크, 랭크 간격 2, 레인은 전진축 직각 방향 좌우 대칭.
public static class PartyFormationMath
{
	public const int RankSpacing = 2;
	public const int LeaderRank = 1;
	public const int MaxRank = 2;
	// 기사형(2)=근접, 아처형(7)=원거리 기준 중간값.
	public const int MeleeEngageDistanceThreshold = 3;
	public const int ArrivalRadius = 1;
	public const int DefaultSearchRadius = 12;
	// 문 앞 통과 구간 — 문 타일에서 이 체비셰프 거리 미만에는 진형 대기 자리를 두지 않는다(05번 3장 "문 바로 앞 통과 구간 2칸을 비워 두고 점유 타일 전체가 그 밖에서 기다린다").
	// AIMovementHelper.FindDoorWaitSlot의 최소 반경(2)과 같은 해석이다. 문을 부수는 유닛만 파괴 단계에서 잠시 이 구간(문 인접 1칸)으로 들어간다.
	public const int DoorClearance = 2;

	public static bool IsMeleeEngage(int engageDistance) => engageDistance <= MeleeEngageDistanceThreshold;

	public static int ResolveRank(bool isLeader, bool isMelee) => isLeader ? LeaderRank : (isMelee ? 0 : MaxRank);

	// 홀수는 중앙 포함 대칭(-1,0,1), 짝수는 중앙을 비우고 좌우 동수 대칭(-2,-1,+1,+2) — 단순 i - n/2는 짝수에서 대형 중심이 처진다.
	public static int LaneOffset(int i, int n)
	{
		if (n % 2 == 1) return i - n / 2;
		int half = n / 2;
		return i < half ? i - half : i - half + 1;
	}

	// 레인이 늘어서는 축 — forward=(±1,0)이면 (0,1), forward=(0,±1)이면 (1,0).
	public static Vector2Int LateralAxis(Vector2Int forward) => new Vector2Int(Mathf.Abs(forward.y), Mathf.Abs(forward.x));

	// 진형 자리: frontAnchor는 0랭크 줄의 레인 0 타일, rank가 클수록 forward 반대 방향(뒤)으로 RankSpacing씩 물러난다.
	public static Vector2Int SlotTarget(Vector2Int frontAnchor, Vector2Int forward, int rank, int lane)
		=> frontAnchor - forward * (rank * RankSpacing) + LateralAxis(forward) * lane;

	// 입장 자리: farAnchor는 문 먼 쪽 줄의 중앙 타일. 앞선 랭크일수록 다음 방 안쪽 깊이 들어간다(맨 뒤 랭크가 문 바로 안쪽 1칸).
	public static Vector2Int EntrySlotTarget(Vector2Int farAnchor, Vector2Int forward, int rank, int lane)
		=> farAnchor + forward * (1 + (MaxRank - rank) * RankSpacing) + LateralAxis(forward) * lane;

	public static bool IsInDoorClearance(Vector2Int tile, IList<Vector2Int> doorTiles)
	{
		for (int i = 0; i < doorTiles.Count; i++)
		{
			if (Mathf.Max(Mathf.Abs(tile.x - doorTiles[i].x), Mathf.Abs(tile.y - doorTiles[i].y)) < DoorClearance) return true;
		}
		return false;
	}

	public static bool IsAtSlot(Vector2Int position, Vector2Int slot)
		=> Mathf.Max(Mathf.Abs(position.x - slot.x), Mathf.Abs(position.y - slot.y)) <= ArrivalRadius;

	// 구역: 자리를 배정할 때 실제로 쓴 범위. 좁은 길에서는 자리가 멀리 퍼지므로 고정 반경이 아니라 "배정된 자리 중 가장 먼 것"으로 정한다(최소 1).
	public static int ZoneRadius(IEnumerable<Vector2Int> slots, Vector2Int center)
	{
		int radius = 1;
		foreach (var s in slots)
			radius = Mathf.Max(radius, Mathf.Max(Mathf.Abs(s.x - center.x), Mathf.Abs(s.y - center.y)));
		return radius;
	}

	// 구역 안인가 — 자리에 서 있던 유닛이 한 칸 비껴 서는 것을 허용하려고 반경에 1칸 여유를 둔다.
	public static bool IsWithinZone(Vector2Int position, Vector2Int center, int radius)
		=> Mathf.Max(Mathf.Abs(position.x - center.x), Mathf.Abs(position.y - center.y)) <= radius + 1;

	// ideal(반경 0)부터 maxRadius까지 링을 넓혀 가며 isFree를 만족하는 가장 가까운 타일 — 고정 반경이 아니라 점진 확장이다. 같은 링에서는 ideal과 거리가 가까운 쪽,
	// 동률은 순회 순서(결정적)로 고른다. 고른 타일은 claimed에 넣는다.
	public static bool TryPickNearestFreeTile(Vector2Int ideal, Func<Vector2Int, bool> isFree, ISet<Vector2Int> claimed, int maxRadius, out Vector2Int picked)
	{
		picked = default;
		for (int radius = 0; radius <= maxRadius; radius++)
		{
			bool found = false;
			float bestSqr = float.MaxValue;
			for (int dx = -radius; dx <= radius; dx++)
			for (int dy = -radius; dy <= radius; dy++)
			{
				if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius) continue; // 이번 반경의 테두리만
				var cand = new Vector2Int(ideal.x + dx, ideal.y + dy);
				if (claimed != null && claimed.Contains(cand)) continue;
				if (!isFree(cand)) continue;
				float sqr = dx * dx + dy * dy;
				if (sqr < bestSqr) { bestSqr = sqr; picked = cand; found = true; }
			}
			if (found)
			{
				claimed?.Add(picked);
				return true;
			}
		}
		return false;
	}

	// 같은 랭크 안에서 현재 가로(lateral) 좌표 순으로 레인을 나눈다 — 서로 교차해 비비지 않는다. 반환은 입력과 같은 순서의 레인 오프셋.
	public static int[] AssignLanes(IList<int> lateralPositions)
	{
		int n = lateralPositions.Count;
		var order = new int[n];
		for (int i = 0; i < n; i++) order[i] = i;
		Array.Sort(order, (a, b) =>
		{
			int c = lateralPositions[a].CompareTo(lateralPositions[b]);
			return c != 0 ? c : a.CompareTo(b);
		});
		var lanes = new int[n];
		for (int k = 0; k < n; k++) lanes[order[k]] = LaneOffset(k, n);
		return lanes;
	}

	// ── 문 파괴 ────────────────────────────────────────────────────────────────────────────

	// 문은 방 쪽 줄(1×2 묶음)마다 오브젝트 하나라 파괴 대상은 두 행뿐이다. 다음에 부술 행: 0 = near(리더 방 쪽), 1 = far, -1 = 둘 다 열림(진입로 확보). far는 near가 열린 뒤에만 닿으므로 near를 먼저 본다.
	public static int NextBreachRow(bool nearBlocked, bool farBlocked)
		=> nearBlocked ? 0 : (farBlocked ? 1 : -1);

	// 문 파괴 자리 — 목표 문 타일 중 하나에 체비셰프 거리 1이고, 문 타일이 아니며, isStandable인 칸을 전부 모은다(자리가 남는 만큼만 투입하기 위한 후보). (x, y) 오름차순이라 결정적이다.
	public static List<Vector2Int> AttackSlotsAround(IList<Vector2Int> doorTiles, Func<Vector2Int, bool> isStandable)
	{
		var doorSet = new HashSet<Vector2Int>(doorTiles);
		var seen = new HashSet<Vector2Int>();
		var slots = new List<Vector2Int>();
		foreach (var door in doorTiles)
		{
			for (int dx = -1; dx <= 1; dx++)
			for (int dy = -1; dy <= 1; dy++)
			{
				if (dx == 0 && dy == 0) continue;
				var t = new Vector2Int(door.x + dx, door.y + dy);
				if (doorSet.Contains(t) || !seen.Add(t)) continue;
				if (isStandable(t)) slots.Add(t);
			}
		}
		slots.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
		return slots;
	}

	// 우선순위(작을수록 먼저, 동률은 입력 순서)대로 각자 "가장 가까운 빈 자리"를 차지하는 그리디 배정. 반환은 멤버별 자리 번호(자리가 모자라면 -1).
	// isAllowed(멤버 번호, 자리 번호)가 있으면 그 멤버가 못 쓰는 자리(이미 막혀 포기한 자리 등)를 건너뛴다.
	public static int[] AssignNearest(IList<Vector2Int> positions, IList<int> priorities, IList<Vector2Int> slots, Func<int, int, bool> isAllowed = null)
	{
		int n = positions.Count;
		var result = new int[n];
		for (int i = 0; i < n; i++) result[i] = -1;

		var order = new int[n];
		for (int i = 0; i < n; i++) order[i] = i;
		Array.Sort(order, (a, b) => priorities[a] != priorities[b] ? priorities[a].CompareTo(priorities[b]) : a.CompareTo(b));

		var used = new bool[slots.Count];
		foreach (int i in order)
		{
			int best = -1;
			long bestKey = long.MaxValue;
			for (int s = 0; s < slots.Count; s++)
			{
				if (used[s] || (isAllowed != null && !isAllowed(i, s))) continue;
				int dx = positions[i].x - slots[s].x, dy = positions[i].y - slots[s].y;
				// 체비셰프 거리가 1순위, 같으면 직선 거리(제곱)가 짧은 쪽 — 격자에서 체비셰프 동률이 흔해 곧장 앞 칸을 고르게 한다. 그래도 같으면 작은 자리 번호(strict <).
				long key = (long)Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 1000000L + (dx * dx + dy * dy);
				if (key < bestKey) { bestKey = key; best = s; }
			}
			if (best < 0) continue;
			used[best] = true;
			result[i] = best;
		}
		return result;
	}

	// ── 입장 ────────────────────────────────────────────────────────────────────────────────

	// 문 먼 쪽 줄(farTile)을 전진 방향으로 완전히 지났는가(far 줄 다음 칸부터).
	public static bool HasPassedGate(Vector2Int position, Vector2Int farTile, Vector2Int forward)
		=> (position.x - farTile.x) * forward.x + (position.y - farTile.y) * forward.y >= 1;

	// 랭크 k는 더 앞선(작은) 랭크의 모든 구성원이 문을 지난 뒤에 출발한다.
	public static bool CanReleaseRank(int rank, IList<int> memberRanks, IList<bool> passed)
	{
		for (int i = 0; i < memberRanks.Count; i++)
		{
			if (memberRanks[i] < rank && !passed[i]) return false;
		}
		return true;
	}
}
