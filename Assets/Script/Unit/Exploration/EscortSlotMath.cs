using System;
using System.Collections.Generic;
using UnityEngine;

// 보호 포메이션 자리 규칙(03번 6-4·6-5장) — 순수 함수. 보호 포메이션은 꺼져 있어(Formation.enabled=false) 휴면 로직이다.
// 근접: 1 상호작용 유닛 전방 → 2 좌우 → 3 가장 가까운 이동 가능 타일. 원거리: 1 후방 minBack칸 이상 → 2 좌우 후방 → 3 가장 가까운 비점유 타일(가능하면 minBack칸 유지).
// 호출부가 정한 순서대로 차례로 배정해(앞 유닛이 차지한 자리는 뒤 유닛이 못 씀) 여럿이 한 자리로 몰리지 않고, 한 유닛의 자리는 다른 호위 유닛 위치와 무관해 매 틱 다시 계산해도 흔들리지 않는다.
public static class EscortSlotMath
{
	// 단계 3(가장 가까운 타일) 탐색 반경(체비셰프).
	public const int NearestSearchRadius = 6;
	// 원거리 단계 1·2에서 후방으로 더 물러나 보는 칸 수(minBack부터 minBack + 이 값 - 1칸까지).
	public const int RangedBackDepthSteps = 3;

	// 전방 방향의 좌우 단위 벡터(전방이 대각선이면 좌우도 대각선).
	public static Vector2Int Lateral(Vector2Int facing) => new Vector2Int(-facing.y, facing.x);

	// 근접 단계 1~2 후보(전방 → 좌 → 우). 단계 3은 PickNearest가 맡는다.
	public static List<Vector2Int[]> MeleeTiers(Vector2Int escort, Vector2Int facing)
	{
		Vector2Int lat = Lateral(facing);
		return new List<Vector2Int[]>
		{
			new[] { escort + facing },
			new[] { escort + lat, escort - lat },
		};
	}

	// 원거리 단계 1~2 후보 — 1: 정후방 minBack, minBack+1, ... / 2: 좌우 후방(같은 깊이에서 좌 → 우). 단계 3은 PickNearest가 맡는다.
	public static List<Vector2Int[]> RangedTiers(Vector2Int escort, Vector2Int facing, int minBack)
	{
		Vector2Int lat = Lateral(facing);
		var straight = new List<Vector2Int>();
		var diagonal = new List<Vector2Int>();
		for (int k = minBack; k < minBack + RangedBackDepthSteps; k++)
		{
			Vector2Int back = escort - facing * k;
			straight.Add(back);
			diagonal.Add(back + lat);
			diagonal.Add(back - lat);
		}
		return new List<Vector2Int[]> { straight.ToArray(), diagonal.ToArray() };
	}

	// 단계 3 — 상호작용 유닛에서 가장 가까운(체비셰프 → 직선 거리 → x → y) free 타일. preferMinDistance > 0이면 그 거리 이상을 먼저 찾고 없을 때만 더 가까운 타일을 쓴다(원거리의 '가능하면 최소 2칸').
	public static Vector2Int? PickNearest(Vector2Int escort, int preferMinDistance, Func<Vector2Int, bool> isFree, ICollection<Vector2Int> claimed)
	{
		if (preferMinDistance > 1)
		{
			var far = NearestInRings(escort, preferMinDistance, NearestSearchRadius, isFree, claimed);
			if (far.HasValue) return far;
			return NearestInRings(escort, 1, preferMinDistance - 1, isFree, claimed);
		}
		return NearestInRings(escort, 1, NearestSearchRadius, isFree, claimed);
	}

	private static Vector2Int? NearestInRings(Vector2Int escort, int fromRing, int toRing, Func<Vector2Int, bool> isFree, ICollection<Vector2Int> claimed)
	{
		var ring = new List<Vector2Int>();
		for (int r = fromRing; r <= toRing; r++)
		{
			ring.Clear();
			for (int dx = -r; dx <= r; dx++)
			{
				for (int dy = -r; dy <= r; dy++)
				{
					if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
					ring.Add(new Vector2Int(escort.x + dx, escort.y + dy));
				}
			}
			ring.Sort((a, b) =>
			{
				int da = (a.x - escort.x) * (a.x - escort.x) + (a.y - escort.y) * (a.y - escort.y);
				int db = (b.x - escort.x) * (b.x - escort.x) + (b.y - escort.y) * (b.y - escort.y);
				if (da != db) return da.CompareTo(db);
				return a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y);
			});
			foreach (var t in ring)
			{
				if (!claimed.Contains(t) && isFree(t)) return t;
			}
		}
		return null;
	}

	// 한 호위 유닛의 자리 — 단계별 후보를 차례로 보고 첫 free·미배정 타일, 없으면 단계 3. claimed는 앞 유닛이 이미 차지한 자리. 자리가 전혀 없으면 null.
	public static Vector2Int? PickSlot(Vector2Int escort, Vector2Int facing, bool ranged, int minBack, Func<Vector2Int, bool> isFree, ICollection<Vector2Int> claimed)
	{
		List<Vector2Int[]> tiers = ranged ? RangedTiers(escort, facing, minBack) : MeleeTiers(escort, facing);
		foreach (var tier in tiers)
		{
			foreach (var t in tier)
			{
				if (!claimed.Contains(t) && isFree(t)) return t;
			}
		}
		return PickNearest(escort, ranged ? minBack : 0, isFree, claimed);
	}

	// 같은 대상을 호위하는 유닛 전원의 자리를 목록 순서대로 배정한다(호출부가 근접 먼저·같은 역할은 고정 번호순으로 정렬해 넘김). isFree(호위 번호, 타일)는 그 유닛이 설 수 있는지(점유 크기·벽·오브젝트·남의 점유 포함).
	public static Vector2Int?[] AssignSlots(Vector2Int escort, Vector2Int facing, IList<bool> ranged, int minBack, Func<int, Vector2Int, bool> isFree)
	{
		var result = new Vector2Int?[ranged.Count];
		var claimed = new HashSet<Vector2Int>();
		for (int i = 0; i < ranged.Count; i++)
		{
			int index = i;
			result[i] = PickSlot(escort, facing, ranged[i], minBack, t => isFree(index, t), claimed);
			if (result[i].HasValue) claimed.Add(result[i].Value);
		}
		return result;
	}
}
