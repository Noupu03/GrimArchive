using System;
using System.Collections.Generic;
using UnityEngine;

// 배치모드 순수 계산 — 세션·유닛 상태를 읽는 부분은 DefensePlacementSystem이 한다.
public static class DefensePlacementMath
{
	public static readonly Dir[] AllDirs =
	{
		Dir.UP, Dir.UP_RIGHT, Dir.RIGHT, Dir.DOWN_RIGHT, Dir.DOWN, Dir.DOWN_LEFT, Dir.LEFT, Dir.UP_LEFT,
	};

	public static Vector2Int DirVector(Dir d)
	{
		switch (d)
		{
			case Dir.UP: return new Vector2Int(0, 1);
			case Dir.UP_RIGHT: return new Vector2Int(1, 1);
			case Dir.RIGHT: return new Vector2Int(1, 0);
			case Dir.DOWN_RIGHT: return new Vector2Int(1, -1);
			case Dir.DOWN: return new Vector2Int(0, -1);
			case Dir.DOWN_LEFT: return new Vector2Int(-1, -1);
			case Dir.LEFT: return new Vector2Int(-1, 0);
			case Dir.UP_LEFT: return new Vector2Int(-1, 1);
			default: return Vector2Int.zero;
		}
	}

	// 순차 배치 순서(1-9 표1): 드래그 시작점에서 가까운 순 → 위쪽 → 왼쪽. 월드 y가 클수록 위쪽이다.
	public static void SortForSequentialPlacement<T>(List<T> items, Func<T, Vector2> centerOf, Vector2 anchor)
	{
		items.Sort((a, b) =>
		{
			Vector2 ca = centerOf(a), cb = centerOf(b);
			int byDist = (ca - anchor).sqrMagnitude.CompareTo((cb - anchor).sqrMagnitude);
			if (byDist != 0) return byDist;
			int byUp = cb.y.CompareTo(ca.y);
			return byUp != 0 ? byUp : ca.x.CompareTo(cb.x);
		});
	}

	// 같은 방에서 자기 위치만 바꿀 때는 기존 몫을 빼고 센다(1-5).
	public static int PlannedOthers(int plannedTotal, bool sameRoomAsOld, int ownOldCost)
		=> Mathf.Max(0, plannedTotal - (sameRoomAsOld ? Mathf.Max(0, ownOldCost) : 0));

	public static bool CanSaveWithinCapacity(int plannedOthers, int ownCost, int maxPopulation)
		=> plannedOthers + Mathf.Max(0, ownCost) <= maxPopulation;

	// 경유·같은 방 안 재배치·저장만 한 유닛은 유입이 아니다.
	public static bool CountsAsIncoming(int currentRoomId, int destRoomId) => destRoomId >= 0 && currentRoomId != destRoomId;

	public static int RemainingCapacity(int maxPopulation, int livingPopulation, int confirmedIncoming)
		=> maxPopulation - livingPopulation - confirmedIncoming;

	public readonly struct Candidate
	{
		public readonly int Id;
		public readonly int Cost;
		public readonly float EtaSeconds;
		public Candidate(int id, int cost, float etaSeconds) { Id = id; Cost = cost; EtaSeconds = etaSeconds; }
	}

	// 남은 정원보다 후보가 많으면 예상 이동 시간이 짧은 순으로 들어가는 후보만 수용한다(1-5 표1). 동률은 입력 순서.
	public static List<int> AcceptWithinRemaining(IReadOnlyList<Candidate> candidates, int remaining)
	{
		var indexed = new List<(Candidate c, int i)>(candidates.Count);
		for (int i = 0; i < candidates.Count; i++) indexed.Add((candidates[i], i));
		indexed.Sort((a, b) =>
		{
			int byEta = a.c.EtaSeconds.CompareTo(b.c.EtaSeconds);
			return byEta != 0 ? byEta : a.i.CompareTo(b.i);
		});

		var accepted = new List<int>();
		foreach (var (c, _) in indexed)
		{
			if (c.Cost > remaining) continue;
			remaining -= c.Cost;
			accepted.Add(c.Id);
		}
		return accepted;
	}

	// 진화 연출 중인 유닛이 시작 순간 확보해 둔 추가 인구 — 완료 전까지 방 정원에서 비워 둬야 한다(명세 1-5 "진화 추가 확보분", 1-8).
	// 유닛의 인구수는 완료 때 바뀌므로(UnitFunction) 연출 중에는 진화 후 인구수와 현재 인구수의 차이가 곧 확보분이다. 줄어드는 진화는 0.
	public static int EvolutionExtraPopulation(int postEvolutionCost, int currentCost) => Mathf.Max(0, postEvolutionCost - currentCost);

	// 진화 연출 진행률(0~1)과 남은 초 — 인원 목록이 "진화 연출 남은 시간"을 보여 줄 때 쓴다(명세 1-7 표1).
	public static float EvolutionProgress01(double elapsedSeconds, int durationSeconds)
		=> durationSeconds <= 0 ? 1f : Mathf.Clamp01((float)(elapsedSeconds / durationSeconds));

	public static int EvolutionRemainingSeconds(double elapsedSeconds, int durationSeconds)
		=> Mathf.Max(0, Mathf.CeilToInt((float)(durationSeconds - elapsedSeconds)));

	public static void BodyTiles(Vector2Int origin, Vector2Int footprint, List<Vector2Int> into)
	{
		into.Clear();
		int w = Mathf.Max(1, footprint.x), h = Mathf.Max(1, footprint.y);
		for (int dx = 0; dx < w; dx++)
			for (int dy = 0; dy < h; dy++)
				into.Add(new Vector2Int(origin.x + dx, origin.y + dy));
	}

	public static bool BodiesOverlap(Vector2Int originA, Vector2Int footprintA, Vector2Int originB, Vector2Int footprintB)
	{
		int aw = Mathf.Max(1, footprintA.x), ah = Mathf.Max(1, footprintA.y);
		int bw = Mathf.Max(1, footprintB.x), bh = Mathf.Max(1, footprintB.y);
		return originA.x < originB.x + bw && originA.x + aw > originB.x
			&& originA.y < originB.y + bh && originA.y + ah > originB.y;
	}

	public static Vector2 BodyCenter(Vector2Int origin, Vector2Int footprint)
		=> new Vector2(origin.x + Mathf.Max(1, footprint.x) / 2f, origin.y + Mathf.Max(1, footprint.y) / 2f);

	static readonly HashSet<Vector2Int> _shadowScratch = new HashSet<Vector2Int>(); // 메인 스레드 전용

	// 예상 시야 — 실제 시야(UnitFunction.UpdateFOV)와 같은 대칭 쉐도우캐스트 + 120도 콘. 표시 전용이다.
	public static void ExpectedVisionTiles(Vector2Int origin, int viewDistance, Dir facing, Func<Vector2Int, bool> isOpaque, HashSet<Vector2Int> result)
	{
		_shadowScratch.Clear();
		VisionMath.SymmetricShadowCast(origin, Mathf.Max(0, viewDistance), isOpaque, _shadowScratch);
		Vector2Int f = DirVector(facing);
		float centerAngle = Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg;
		float half = VisionMath.BaseViewAngleDeg * 0.5f;
		foreach (var t in _shadowScratch)
		{
			if (t == origin) { result.Add(t); continue; }
			float angle = Mathf.Atan2(t.y - origin.y, t.x - origin.x) * Mathf.Rad2Deg;
			if (Mathf.Abs(Mathf.DeltaAngle(centerAngle, angle)) <= half) result.Add(t);
		}
	}

	// 예상 시야 윤곽선용 — 광선(origin에서 단위 벡터 dir)이 처음 만나는 불투명 타일(벽 종류·닫힌 문)의 경계까지의 거리, 없으면 maxRadius.
	// 격자 순회(DDA)라 지나는 타일만 확인한다. 출발 타일(유닛이 서 있는 칸)은 검사하지 않는다. 표시 전용이다.
	public static float RayClipDistance(Vector2 origin, Vector2 dir, float maxRadius, Func<Vector2Int, bool> isOpaque)
	{
		if (maxRadius <= 0f || dir.sqrMagnitude < 1e-12f) return Mathf.Max(0f, maxRadius);

		int x = Mathf.FloorToInt(origin.x), y = Mathf.FloorToInt(origin.y);
		int stepX = dir.x > 0f ? 1 : -1, stepY = dir.y > 0f ? 1 : -1;
		float tDeltaX = Mathf.Abs(dir.x) > 1e-6f ? 1f / Mathf.Abs(dir.x) : float.PositiveInfinity;
		float tDeltaY = Mathf.Abs(dir.y) > 1e-6f ? 1f / Mathf.Abs(dir.y) : float.PositiveInfinity;
		float tMaxX = float.IsPositiveInfinity(tDeltaX) ? float.PositiveInfinity : (dir.x > 0f ? x + 1 - origin.x : origin.x - x) * tDeltaX;
		float tMaxY = float.IsPositiveInfinity(tDeltaY) ? float.PositiveInfinity : (dir.y > 0f ? y + 1 - origin.y : origin.y - y) * tDeltaY;

		while (true)
		{
			float t;
			if (tMaxX < tMaxY) { t = tMaxX; x += stepX; tMaxX += tDeltaX; }
			else { t = tMaxY; y += stepY; tMaxY += tDeltaY; }
			if (t >= maxRadius) return maxRadius;
			if (isOpaque(new Vector2Int(x, y))) return t;
		}
	}

	// 기본 공격 범위 — 체비셰프 range 안에서 공격선이 불투명 타일에 막히지 않는 타일.
	public static void ExpectedAttackTiles(Vector2Int origin, int range, Func<Vector2Int, bool> isOpaque, HashSet<Vector2Int> result)
	{
		range = Mathf.Max(0, range);
		for (int dx = -range; dx <= range; dx++)
			for (int dy = -range; dy <= range; dy++)
			{
				var t = new Vector2Int(origin.x + dx, origin.y + dy);
				if (t == origin || HasClearLine(origin, t, isOpaque)) result.Add(t);
			}
	}

	// 브레젠험 직선 — 시작·끝을 뺀 중간 타일에 불투명이 없으면 열려 있다.
	public static bool HasClearLine(Vector2Int a, Vector2Int b, Func<Vector2Int, bool> isOpaque)
	{
		int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
		int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
		int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
		int err = dx - dy;
		while (true)
		{
			if ((x0 != a.x || y0 != a.y) && (x0 != x1 || y0 != y1) && isOpaque(new Vector2Int(x0, y0))) return false;
			if (x0 == x1 && y0 == y1) return true;
			int e2 = 2 * err;
			if (e2 > -dy) { err -= dy; x0 += sx; }
			if (e2 < dx) { err += dx; y0 += sy; }
		}
	}

	public static bool SwapOrder<T>(List<T> order, int a, int b)
	{
		if (a == b || a < 0 || b < 0 || a >= order.Count || b >= order.Count) return false;
		(order[a], order[b]) = (order[b], order[a]);
		return true;
	}
}
