using System;
using System.Collections.Generic;
using UnityEngine;

// 문 묶음 순수 계산(부수효과 없음). 게이트의 문 타일을 "어느 방에 붙어 있는가"로 묶어 한 줄(폭 2 → 1×2)을 문 오브젝트 하나로 다루기 위한 기하 — DoorSystem이 방 조회(roomGrid)만 연결한다.
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

	// rows: 게이트의 두 줄(DoorSystem.GetGateDoorTiles). roomOf: 타일이 실제 속한 방 id(모르면 -1) — 줄 배열 순서는 기하학적 규칙일 뿐 방과 무관해서 방 조회로 묶는다.
	// 같은 방이어도 줄이 다르면 분리(한 방이 두 줄을 다 차지하는 비정상 데이터 방어), 한 줄이 여러 방에 걸치면 방별로 분리한다.
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

	// 문 묶음의 짝(칸) 중 좌우 반전해 그릴 것 — 열림 스프라이트는 한쪽 가장자리(왼쪽 7열)에만 문짝이 있어서, 앞쪽 절반은 그대로 뒤쪽 절반은 반전해야 두 짝이 바깥쪽 양 끝으로 갈라져 젖혀진다(양쪽 여닫이).
	// 타일은 (x, y) 오름차순이라 수직 게이트는 왼→오른쪽, 수평 게이트(90도 회전)는 아래→위쪽 순서로 같은 규칙이 성립한다. 한 칸짜리 문은 반전하지 않는다.
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
