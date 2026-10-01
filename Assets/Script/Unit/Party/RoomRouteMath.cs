using System;
using System.Collections.Generic;

// 알려진 게이트로 이뤄진 방 그래프의 한 간선 — 두 방 사이의 게이트(문 오브젝트가 있든 파괴돼 열렸든 같은 간선이다).
public struct RouteEdge
{
	public int GateId;
	public int RoomA;
	public int RoomB;

	public RouteEdge(int gateId, int roomA, int roomB)
	{
		GateId = gateId;
		RoomA = roomA;
		RoomB = roomB;
	}

	public bool Touches(int room) => RoomA == room || RoomB == room;
	public int Other(int room) => room == RoomA ? RoomB : RoomA;
}

// 리더의 방 경로 결정(04번 1장 30~35·47줄: 인류 방 경로 = 리더가 "현재 방 이후 어느 알려진 문·방으로 이동할지" 정하고, 다른 방 경로가 필요하면 리더가 새로 결정한다) — 부수효과 없는 순수 계산.
// 호출부(LeaderRoutePlanner)가 리더가 아는 게이트만 간선으로 넘기고 "방문한 방"은 구성원이 들어가 본 방이다. 방 선택의 세부 기준은 문서가 리더 문서에 맡겼으므로 여기서는 문서가 명시한 틀
// (알려진 정보만 사용, 목표 방이 있으면 그쪽, 막히면 다른 방 경로로 새 결정)에 필요한 최소 규칙만 둔다.
public static class RoomRouteMath
{
	// fromRoom에서 다음에 지날 게이트를 고른다. 우선순위:
	//  1) fromRoom에 붙은 게이트 중 목표 방(targetRoom)으로 곧장 가는 것
	//  2) 아직 방문하지 않은 방으로 가는 것
	//  3) 둘 다 없으면(막다른 방·모두 방문) 방문한 방들을 거쳐 "목표 방이나 미방문 방으로 이어지는 가장 가까운 방"까지의 경로의 첫 게이트(되돌이)
	// 같은 순위 안에서는 tieScore가 작은 쪽(예: 목표 방 중심에 가까운 게이트), 되돌이는 홉 수가 적은 쪽이 먼저다. 고를 게 없으면 false.
	public static bool TryPickGate(int fromRoom, IList<RouteEdge> edges, ISet<int> visited, int targetRoom, Func<RouteEdge, float> tieScore, out RouteEdge picked)
	{
		picked = default;
		if (edges == null || edges.Count == 0) return false;
		tieScore ??= (_ => 0f);

		bool haveTarget = false, haveUnvisited = false;
		RouteEdge bestTarget = default, bestUnvisited = default;
		float bestTargetScore = float.MaxValue, bestUnvisitedScore = float.MaxValue;
		for (int i = 0; i < edges.Count; i++)
		{
			var e = edges[i];
			if (!e.Touches(fromRoom)) continue;
			int neighbor = e.Other(fromRoom);
			if (neighbor == fromRoom) continue;
			float score = tieScore(e);
			if (neighbor == targetRoom)
			{
				if (!haveTarget || score < bestTargetScore) { haveTarget = true; bestTarget = e; bestTargetScore = score; }
			}
			else if (!visited.Contains(neighbor))
			{
				if (!haveUnvisited || score < bestUnvisitedScore) { haveUnvisited = true; bestUnvisited = e; bestUnvisitedScore = score; }
			}
		}
		if (haveTarget) { picked = bestTarget; return true; }
		if (haveUnvisited) { picked = bestUnvisited; return true; }

		return TryPickBacktrackGate(fromRoom, edges, visited, targetRoom, tieScore, out picked);
	}

	// 방문한 방들만 거쳐 BFS — "목표 방이나 미방문 방으로 이어지는 간선을 가진 방"(frontier 방) 중 홉 수가 가장 적은 곳으로 가는 경로의 첫 간선을 고른다.
	// 미방문 방 안으로는 들어가 탐색하지 않는다(그곳으로 가는 결정은 frontier 방에 도착한 뒤 1)·2)가 한다).
	private static bool TryPickBacktrackGate(int fromRoom, IList<RouteEdge> edges, ISet<int> visited, int targetRoom, Func<RouteEdge, float> tieScore, out RouteEdge picked)
	{
		picked = default;
		var dist = new Dictionary<int, int> { { fromRoom, 0 } };
		var first = new Dictionary<int, RouteEdge>();
		var queue = new Queue<int>();
		queue.Enqueue(fromRoom);

		int bestDist = int.MaxValue;
		float bestScore = float.MaxValue;
		bool found = false;

		while (queue.Count > 0)
		{
			int x = queue.Dequeue();
			if (dist[x] > bestDist) break; // 더 먼 방은 볼 필요 없다(같은 홉 수까지만 비교)

			for (int i = 0; i < edges.Count; i++)
			{
				var e = edges[i];
				if (!e.Touches(x)) continue;
				int y = e.Other(x);
				if (y == x) continue;

				// x가 frontier 방이다(fromRoom 자신은 위 TryPickGate가 이미 처리했다).
				if (x != fromRoom && (y == targetRoom || (y != fromRoom && !visited.Contains(y))))
				{
					RouteEdge firstEdge = first[x];
					float score = tieScore(firstEdge);
					if (!found || dist[x] < bestDist || (dist[x] == bestDist && score < bestScore))
					{
						found = true;
						bestDist = dist[x];
						bestScore = score;
						picked = firstEdge;
					}
				}

				if (visited.Contains(y) && y != targetRoom && !dist.ContainsKey(y))
				{
					dist[y] = dist[x] + 1;
					first[y] = x == fromRoom ? e : first[x];
					queue.Enqueue(y);
				}
			}
		}
		return found;
	}
}
