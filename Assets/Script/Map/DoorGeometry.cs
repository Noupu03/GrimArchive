using System;
using System.Collections.Generic;
using UnityEngine;

// 문 묶음 순수 계산(부수효과 없음) — 게이트의 문 타일을 '어느 방에 붙어 있는가'로 묶어 한 줄(폭 2 → 1×2)을 문 오브젝트 하나로 다루는 기하이며 DoorSystem이 방 조회(roomGrid)만 연결한다.
public static class DoorGeometry
{
	public struct Group
	{
		public int RoomId;
		// (x, y) 오름차순 — 첫 타일이 대표 타일(InteractableObject.Position)이다.
		public List<Vector2Int> Tiles;
		// 타일 좌표계 중심(+0.5 포함, 층 오프셋 전). 문 비주얼·고스트의 위치.
		public Vector2 Center;
		public int Length => Tiles.Count;
	}

	// rows: 게이트의 두 줄(DoorSystem.GetGateDoorTiles). roomOf: 타일이 실제 속한 방 id(모르면 -1) — 줄 배열 순서는 방과 무관해 방 조회로 묶는다. 같은 방이어도 줄이 다르면 분리(한 방이 두 줄을 다 차지하는 비정상 데이터 방어), 한 줄이 여러 방에 걸치면 방별로 분리한다.
	public static List<Group> GroupByRoom(IList<List<Vector2Int>> rows, Func<Vector2Int, int> roomOf)
	{
		var groups = new List<Group>();
		var index = new Dictionary<(int room, int row), int>();
		for (int r = 0; r < rows.Count; r++)
		{
			foreach (var tile in rows[r])
			{
				var key = (roomOf(tile), r);
				if (!index.TryGetValue(key, out int gi))
				{
					gi = groups.Count;
					index[key] = gi;
					groups.Add(new Group { RoomId = key.Item1, Tiles = new List<Vector2Int>() });
				}
				groups[gi].Tiles.Add(tile);
			}
		}

		for (int i = 0; i < groups.Count; i++)
		{
			var g = groups[i];
			g.Tiles.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
			g.Center = CenterOf(g.Tiles);
			groups[i] = g;
		}
		return groups;
	}

	public static Vector2 CenterOf(IReadOnlyList<Vector2Int> tiles)
	{
		int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
		for (int i = 0; i < tiles.Count; i++)
		{
			minX = Mathf.Min(minX, tiles[i].x); maxX = Mathf.Max(maxX, tiles[i].x);
			minY = Mathf.Min(minY, tiles[i].y); maxY = Mathf.Max(maxY, tiles[i].y);
		}
		return new Vector2((minX + maxX) / 2f + 0.5f, (minY + maxY) / 2f + 0.5f);
	}

	public static Vector2 CenterOf(IReadOnlyList<Vector3Int> tiles)
	{
		int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
		for (int i = 0; i < tiles.Count; i++)
		{
			minX = Mathf.Min(minX, tiles[i].x); maxX = Mathf.Max(maxX, tiles[i].x);
			minY = Mathf.Min(minY, tiles[i].y); maxY = Mathf.Max(maxY, tiles[i].y);
		}
		return new Vector2((minX + maxX) / 2f + 0.5f, (minY + maxY) / 2f + 0.5f);
	}

	// 문 묶음의 "방 안쪽" 방향 — 수평 게이트(문 타일이 한 열에 늘어섬)는 ±x, 수직 게이트는 ±y 중 문 타일의 이웃이 같은 방인 쪽이다(반대쪽 이웃은 게이트 건너편 방의 문 줄).
	// 양쪽이 모두 같은 방이거나 어느 쪽도 아니면 안쪽을 정할 수 없어 false다. 목책은 문 줄을 이 방향으로 한 칸 민 자리에 선다.
	public static bool TryGetInward(bool horizontalGate, Vector2Int sample, Func<Vector2Int, bool> isInRoom, out Vector2Int inward)
	{
		Vector2Int a = horizontalGate ? Vector2Int.left : Vector2Int.down;
		Vector2Int b = -a;
		bool inA = isInRoom(sample + a);
		bool inB = isInRoom(sample + b);
		inward = default;
		if (inA == inB) return false;
		inward = inA ? a : b;
		return true;
	}

	// 타일 목록을 offset만큼 민다(입력 순서 유지 — (x, y) 오름차순이면 결과도 오름차순이다).
	public static List<Vector2Int> Shifted(IReadOnlyList<Vector2Int> tiles, Vector2Int offset)
	{
		var result = new List<Vector2Int>(tiles.Count);
		for (int i = 0; i < tiles.Count; i++) result.Add(tiles[i] + offset);
		return result;
	}

	// 문 묶음의 짝(칸) 중 좌우 반전해 그릴 것 — 열림 스프라이트는 왼쪽 7열에만 문짝이 있어 앞쪽 절반은 그대로, 뒤쪽 절반은 반전해야 두 짝이 바깥 양 끝으로 갈라진다(양쪽 여닫이). 타일이 (x, y) 오름차순이라 수직 게이트는 왼→오른쪽, 수평 게이트(90도 회전)는 아래→위쪽 순서로 같은 규칙이며 한 칸짜리 문은 반전하지 않는다.
	public static bool IsMirroredLeaf(int index, int count)
		=> count > 1 && index >= (count + 1) / 2;

	// 문 대상(코어·문·함정 파괴)은 인접 1칸에서만 공격할 수 있다 — 멀티 타일 오브젝트는 어느 타일에든 인접하면 된다. z는 비교하지 않는다(호출부가 같은 층만 넘긴다).
	public static bool IsAdjacentToAny(Vector2Int pos, IReadOnlyList<Vector3Int> tiles, int radius = 1)
	{
		for (int i = 0; i < tiles.Count; i++)
		{
			if (Mathf.Max(Mathf.Abs(pos.x - tiles[i].x), Mathf.Abs(pos.y - tiles[i].y)) <= radius) return true;
		}
		return false;
	}

	// 문 앞·뒤 비움 구역(코어 점령 v0.2 6장 "문 앞·뒤 각각 2칸") — 문 줄과 같은 폭으로 통로 방향(수평 게이트는 x, 수직 게이트는 y) 앞뒤 depth칸씩과 문 줄 자신이다. 문 줄이 통로 방향 기준 한 좌표에 놓이므로 첫 타일로 그 좌표를 잡는다.
	// 문 앞·뒤 타일 형태·대형 유닛 점유는 문서가 후속 확정으로 남겼다(21장) — 여기서는 문 폭 그대로 직선 구역으로 정했다.
	public static void ClearanceTiles(IReadOnlyList<Vector3Int> doorTiles, bool horizontalGate, int depth, List<Vector2Int> result)
	{
		result.Clear();
		if (doorTiles == null || doorTiles.Count == 0) return;

		int axisCoord = horizontalGate ? doorTiles[0].x : doorTiles[0].y;
		var lateral = new List<int>(doorTiles.Count);
		for (int i = 0; i < doorTiles.Count; i++)
		{
			int l = horizontalGate ? doorTiles[i].y : doorTiles[i].x;
			if (!lateral.Contains(l)) lateral.Add(l);
		}
		for (int a = axisCoord - depth; a <= axisCoord + depth; a++)
			foreach (int l in lateral)
				result.Add(horizontalGate ? new Vector2Int(a, l) : new Vector2Int(l, a));
	}

	// from에서 체비셰프 거리가 가장 가까운 타일(동률은 앞선 타일). tiles가 비어 있으면 from을 그대로 돌려준다.
	public static Vector2Int NearestTile(Vector2Int from, IReadOnlyList<Vector3Int> tiles)
	{
		Vector2Int best = from;
		int bestDist = int.MaxValue;
		for (int i = 0; i < tiles.Count; i++)
		{
			int d = Mathf.Max(Mathf.Abs(from.x - tiles[i].x), Mathf.Abs(from.y - tiles[i].y));
			if (d < bestDist) { bestDist = d; best = new Vector2Int(tiles[i].x, tiles[i].y); }
		}
		return best;
	}
}

// 플레이어의 문 열기·닫기 명령 판정(순수) — 코어 점령 v0.2 4·6·19장. 인류 전용 문은 플레이어가 제어할 수 없고, 담당 코어가 파괴되면 문 제어도 멈춘다. 닫기는 문 앞·뒤 비움 구역에 어느 진영의 유닛이든(은신 포함) 없을 때만 된다.
public enum DoorControlBlock
{
	None,
	NotPlayerDoor,   // 플레이어 소유 문이 아니다(인류 전용 문·야생 문)
	CoreDestroyed,   // 담당 코어가 파괴돼 문 제어 불가
	AlreadyInState,  // 이미 그 상태
	UnitsNearby,     // 닫기 — 문 앞·뒤에 유닛이 있다
}

public static class DoorControlRules
{
	public const int ClearanceDepth = 2;

	public static DoorControlBlock Check(FactionType owner, FactionType actor, bool coreFunctionsActive, bool wantOpen, bool currentlyOpen, bool anyUnitInClearance)
	{
		if (owner != actor) return DoorControlBlock.NotPlayerDoor;
		if (!coreFunctionsActive) return DoorControlBlock.CoreDestroyed;
		if (wantOpen == currentlyOpen) return DoorControlBlock.AlreadyInState;
		if (!wantOpen && anyUnitInClearance) return DoorControlBlock.UnitsNearby;
		return DoorControlBlock.None;
	}

	public static string Describe(DoorControlBlock block) => block switch
	{
		DoorControlBlock.None => "",
		DoorControlBlock.NotPlayerDoor => "플레이어가 제어할 수 없는 문입니다.",
		DoorControlBlock.CoreDestroyed => "코어가 파괴되어 문을 제어할 수 없습니다.",
		DoorControlBlock.AlreadyInState => "",
		_ => "문 근처에 유닛이 있어 현재 문을 닫을 수 없습니다.",
	};
}
