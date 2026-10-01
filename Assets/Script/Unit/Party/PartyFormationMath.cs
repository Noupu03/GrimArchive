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
	// forward(near → far 통과 방향)를 주면 문 줄 너머(통과 방향 쪽) 칸은 뺀다 — isStandable은 그 칸 하나만 보므로 아직 닫힌 far 문 너머 칸도 "설 수 있는 칸"으로 나오는데, 닿을 수 없는 자리를 받은 인원이 돌아가는 길로 헤매기 때문이다.
	public static List<Vector2Int> AttackSlotsAround(IList<Vector2Int> doorTiles, Func<Vector2Int, bool> isStandable, Vector2Int? forward = null)
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
				if (forward.HasValue && dx * forward.Value.x + dy * forward.Value.y > 0) continue; // 문 줄 너머(다음 방 쪽)
				if (isStandable(t)) slots.Add(t);
			}
		}
		slots.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
		return slots;
	}

	// 우선순위 단계(작을수록 먼저)별로, 같은 단계 안에서는 "전체에서 가장 가까운 (멤버, 자리) 쌍"부터 차지하는 그리디 배정 + 자리를 못 받은 멤버를 늘리는 보강. 반환은 멤버별 자리 번호(자리가 모자라면 -1).
	// 멤버를 입력 순서대로 하나씩 처리하면 자리 정면에 선 멤버가 아니라 순서가 앞선 옆·대각선 멤버가 그 자리를 받는다 — 좁은 문 앞은 모서리 규칙으로 대각선 진입이 막혀, 정면 칸에 남은 멤버가 길을 막고 둘 다 정체한다(플레이 로그 2026-10-02 far 문).
	// 가까운 쌍부터 고르면 isAllowed가 막은 자리 때문에 "모두가 자리를 받을 수 있는데 한 명이 남는" 배정이 나올 수 있어, 그리디 뒤에 남은 멤버마다 이미 배정된 멤버를 다른 자리로 옮겨서라도 자리를 얻는 보강(증가 경로)을 한다 — 이미 자리를 받은 멤버는 보강으로 자리를 잃지 않는다.
	// 거리가 같은 쌍은 앞선 멤버 번호·작은 자리 번호가 먼저다. isAllowed(멤버 번호, 자리 번호)가 있으면 그 멤버가 못 쓰는 자리(이미 막혀 포기한 자리 등)를 건너뛴다. 앞선 단계가 차지한 자리는 뒤 단계가 건드리지 않는다.
	public static int[] AssignNearest(IList<Vector2Int> positions, IList<int> priorities, IList<Vector2Int> slots, Func<int, int, bool> isAllowed = null)
	{
		int n = positions.Count;
		var result = new int[n];
		for (int i = 0; i < n; i++) result[i] = -1;
		var owner = new int[slots.Count];
		for (int s = 0; s < owner.Length; s++) owner[s] = -1;

		var tiers = new SortedSet<int>();
		for (int i = 0; i < n; i++) tiers.Add(priorities[i]);

		foreach (int tier in tiers)
		{
			while (true)
			{
				int bestMember = -1, bestSlot = -1;
				long bestKey = long.MaxValue;
				for (int i = 0; i < n; i++)
				{
					if (result[i] >= 0 || priorities[i] != tier) continue;
					for (int s = 0; s < slots.Count; s++)
					{
						if (owner[s] >= 0 || (isAllowed != null && !isAllowed(i, s))) continue;
						int dx = positions[i].x - slots[s].x, dy = positions[i].y - slots[s].y;
						// 체비셰프 거리가 1순위, 같으면 직선 거리(제곱)가 짧은 쪽 — 격자에서 체비셰프 동률이 흔해 곧장 앞 칸을 고르게 한다. 그래도 같으면 먼저 본 쌍(strict <).
						long key = (long)Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 1000000L + (dx * dx + dy * dy);
						if (key < bestKey) { bestKey = key; bestMember = i; bestSlot = s; }
					}
				}
				if (bestMember < 0) break;
				result[bestMember] = bestSlot;
				owner[bestSlot] = bestMember;
			}

			for (int i = 0; i < n; i++)
			{
				if (result[i] >= 0 || priorities[i] != tier) continue;
				TryAugment(i, tier, slots.Count, priorities, isAllowed, result, owner, new bool[slots.Count]);
			}
		}
		return result;
	}

	// 증가 경로 — 멤버 i가 자리를 얻도록, 필요하면 같은 단계의 다른 멤버를 다른 자리로 옮긴다. 성공하면 true(그때만 배정이 바뀐다).
	private static bool TryAugment(int i, int tier, int slotCount, IList<int> priorities, Func<int, int, bool> isAllowed, int[] result, int[] owner, bool[] visited)
	{
		for (int s = 0; s < slotCount; s++)
		{
			if (visited[s] || (isAllowed != null && !isAllowed(i, s))) continue;
			int holder = owner[s];
			if (holder >= 0 && priorities[holder] != tier) continue; // 앞선 단계가 차지한 자리
			visited[s] = true;
			if (holder < 0 || TryAugment(holder, tier, slotCount, priorities, isAllowed, result, owner, visited))
			{
				result[i] = s;
				owner[s] = i;
				return true;
			}
		}
		return false;
	}

	// ── 입장 ────────────────────────────────────────────────────────────────────────────────

	// 문 먼 쪽 줄(farTile)을 전진 방향으로 완전히 지났는가(far 줄 다음 칸부터).
	public static bool HasPassedGate(Vector2Int position, Vector2Int farTile, Vector2Int forward)
		=> (position.x - farTile.x) * forward.x + (position.y - farTile.y) * forward.y >= 1;

	// 조사가 "상호작용을 시작했는가" — 진행 중(PenaltyActive)이거나 진행도가 남아 있으면 시작한 것이다(중단돼 진행도가 절반으로 남은 조사 포함). 둘 다 아니면 대상을 정해 이동 중인 미착수 상태라
	// 집결 명령·공동 이동 배정 때 접는다(05번 4장 245줄, 03번 1장 181줄). currentInvestigation은 대상 "선택" 순간부터 채워지므로 그 유무만으로는 시작 여부를 알 수 없다(검증 05-04).
	public static bool IsInvestigationStarted(bool penaltyActive, float progress01)
		=> penaltyActive || progress01 > 0f;

	// 리더의 명령(집결 명령·집결 해제·방 이동 지시)이 "같은 방 전체" 예외를 받는 같은 방인가 — 방이 있고 같은 방이어야 한다. 방이 없는(통로·문 위치 등) 두 유닛은 서로 다른 곳이어도 둘 다 null이라
	// 참조 비교만으로는 같은 방으로 잘못 보이므로 따로 거른다(검증 05-03 관찰 4).
	public static bool IsSameRoomForCommand(object leaderRoom, object memberRoom)
		=> leaderRoom != null && ReferenceEquals(leaderRoom, memberRoom);

	// tiles 중 사각형(xMin ≤ x < xMax, yMin ≤ y < yMax — RectInt.Contains와 같은 반열린 구간) 안에서 from과 직선거리가 가장 가까운 타일. 개인 탐색을 현재 방 안으로 제한할 때 프론티어 후보를 고르는 데 쓴다(검증 05-01).
	public static bool TryNearestInRect(IEnumerable<Vector2Int> tiles, Vector2Int from, int xMin, int yMin, int xMax, int yMax, out Vector2Int nearest)
	{
		nearest = default;
		bool found = false;
		float bestDistSq = float.MaxValue;
		foreach (var tile in tiles)
		{
			if (tile.x < xMin || tile.x >= xMax || tile.y < yMin || tile.y >= yMax) continue;
			float dx = tile.x - from.x, dy = tile.y - from.y;
			float distSq = dx * dx + dy * dy;
			if (distSq < bestDistSq) { bestDistSq = distSq; nearest = tile; found = true; }
		}
		return found;
	}

	// 교전·경계로 시간 제한을 멈춰 줄 때 이번에 인정할 시간 — 아직 쓰지 않은 상한(cap − 이미 멈춘 시간) 안에서 경과 dt만큼. 상한에 닿으면 0이라 시간이 다시 흐른다(풀리지 않는 교전이 계획을 영구히 얼리지 않게).
	public static float PauseCredit(float dt, float alreadyPaused, float cap)
		=> System.Math.Max(0f, System.Math.Min(dt, cap - alreadyPaused));

	// 랭크 k는 더 앞선(작은) 랭크의 모든 구성원이 문을 지난 뒤에 출발한다. 입장에서는 4단계 역할 순서(OccupancyMath.EntryRank)를 이 "랭크"로 넘긴다.
	public static bool CanReleaseRank(int rank, IList<int> memberRanks, IList<bool> passed)
	{
		for (int i = 0; i < memberRanks.Count; i++)
		{
			if (memberRanks[i] < rank && !passed[i]) return false;
		}
		return true;
	}
}
