using System.Collections.Generic;
using UnityEngine;

// 건설 설계명세서 v0.2 2-9 - 문 확장: 게이트의 문 줄 양 끝 벽을 같은 폭만큼 열어 통로를 넓힌다. 문 중심은 고정이고(1×2 → 1×4) 폭 단계·최대 폭은 문 종류 문서가 정한다(호출부가 넘기는 자리표시자).
// 런타임에 벽을 바닥으로 바꾸는 맵 데이터 경로다 - 렌더링·그림자·안개·지식 캐시 갱신은 GameSession.OnGateTilesOpened가 맡는다.
public partial class CreateMap
{
	// 게이트 폭을 widthDelta(짝수)만큼 넓힐 때 새로 열릴 벽 타일을 양 끝(end0 = 아래/왼쪽, end1 = 위/오른쪽)으로 나눠 모은다. 벽 두께만큼 깊이 전부 담는다.
	// 폭 한계를 넘거나 열 타일이 벽이 아니거나 청크 가장자리 벽을 침범하면 false이고 fail에 사유가 담긴다.
	public bool TryGetGateExpansionWalls(int floorIndex, int gateIndex, int widthDelta, int maxWidth, List<Vector2Int> end0, List<Vector2Int> end1, out int newWidth, out string fail)
	{
		newWidth = 0;
		fail = null;
		end0.Clear();
		end1.Clear();
		if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) { fail = "층 정보가 없습니다."; return false; }
		Floor floor = map.floors[floorIndex];
		if (floor.gates == null || gateIndex < 0 || gateIndex >= floor.gates.Count) { fail = "통로 정보가 없습니다."; return false; }

		Gate g = floor.gates[gateIndex];
		int cs = floor.config.chunkSize;
		newWidth = g.width + widthDelta;
		if (widthDelta <= 0 || widthDelta % 2 != 0 || newWidth > maxWidth || newWidth > 6) { fail = "더 넓힐 수 없는 문입니다."; return false; }

		int oldStart = (cs - g.width) / 2;
		int newStart = (cs - newWidth) / 2;
		if (newStart < 1 || newStart + newWidth - 1 > cs - 2) { fail = "통로 양 끝에 확장할 벽이 부족합니다."; return false; }

		bool ok = true;
		if (g.isHorizontal)
		{
			int leftX = Mathf.Min(g.chunkAX, g.chunkBX);
			int y = g.chunkAY;
			Chunks cl = floor.chunks[leftX, y];
			Chunks cr = floor.chunks[leftX + 1, y];
			if (cl.chunk == null || cr.chunk == null) { fail = "통로 청크가 없습니다."; return false; }
			int thkL = GetWallThickness(cl.roomId, true);
			int thkR = GetWallThickness(cr.roomId, true);

			void Collect(int ty, List<Vector2Int> list)
			{
				for (int tx = cs - thkL; tx < cs; tx++)
				{
					if (cl.chunk[tx, ty].name != "Wall") ok = false;
					list.Add(new Vector2Int(leftX * cs + tx, y * cs + ty));
				}
				for (int tx = 0; tx < thkR; tx++)
				{
					if (cr.chunk[tx, ty].name != "Wall") ok = false;
					list.Add(new Vector2Int((leftX + 1) * cs + tx, y * cs + ty));
				}
			}
			for (int ty = newStart; ty < oldStart; ty++) Collect(ty, end0);
			for (int ty = oldStart + g.width; ty <= newStart + newWidth - 1; ty++) Collect(ty, end1);
		}
		else
		{
			int bottomY = Mathf.Min(g.chunkAY, g.chunkBY);
			int x = g.chunkAX;
			Chunks cb = floor.chunks[x, bottomY];
			Chunks ct = floor.chunks[x, bottomY + 1];
			if (cb.chunk == null || ct.chunk == null) { fail = "통로 청크가 없습니다."; return false; }
			int thkB = GetWallThickness(cb.roomId, false);
			int thkT = GetWallThickness(ct.roomId, false);

			void Collect(int tx, List<Vector2Int> list)
			{
				for (int ty = cs - thkB; ty < cs; ty++)
				{
					if (cb.chunk[tx, ty].name != "Wall") ok = false;
					list.Add(new Vector2Int(x * cs + tx, bottomY * cs + ty));
				}
				for (int ty = 0; ty < thkT; ty++)
				{
					if (ct.chunk[tx, ty].name != "Wall") ok = false;
					list.Add(new Vector2Int(x * cs + tx, (bottomY + 1) * cs + ty));
				}
			}
			for (int tx = newStart; tx < oldStart; tx++) Collect(tx, end0);
			for (int tx = oldStart + g.width; tx <= newStart + newWidth - 1; tx++) Collect(tx, end1);
		}

		if (!ok || end0.Count == 0 || end1.Count == 0) { fail = "확장할 양 끝이 벽이 아니라 넓힐 수 없습니다."; return false; }
		return true;
	}

	// 양 끝 벽 타일을 바닥으로 바꾸고 게이트 폭을 갱신한다(문 중심은 그대로). 기존 통로 타일의 상태는 건드리지 않는다.
	public bool ApplyGateExpansionTiles(int floorIndex, int gateIndex, int newWidth, IEnumerable<Vector2Int> tiles)
	{
		if (map.floors == null || floorIndex < 0 || floorIndex >= map.floors.Length) return false;
		Floor floor = map.floors[floorIndex];
		if (floor.gates == null || gateIndex < 0 || gateIndex >= floor.gates.Count) return false;
		int cs = floor.config.chunkSize;

		foreach (Vector2Int t in tiles)
		{
			int cx = t.x / cs, cy = t.y / cs;
			if (cx < 0 || cy < 0 || cx >= floor.config.width || cy >= floor.config.height) continue;
			Chunks c = floor.chunks[cx, cy];
			if (c.chunk == null) continue;
			c.chunk[t.x - cx * cs, t.y - cy * cs] = TileFactory.Floor();
		}

		Gate g = floor.gates[gateIndex];
		g.width = newWidth;
		floor.gates[gateIndex] = g;
		return true;
	}
}
