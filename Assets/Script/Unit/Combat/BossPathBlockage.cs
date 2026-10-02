using System.Collections.Generic;
using UnityEngine;

// '실제 보스에게 이동하는 경로를 막는 적'은 보스 집중을 잠시 멈추게 하는 임시 대응 사유다(04번 9장: 막는다 = 우회할 수 없다). 보스를 공격하려면 이동해야 하는데(사거리 밖) 다른 유닛의 점유를 피해선 공격 거리 안 어디에도 닿지 못할 때, 적을 무시한 구조 경로 위에서 처음 만나는 확인된 적이 길을 막는 적이다.
public static class BossPathBlockage
{
	// 보스로 가는 길을 막고 있는 확인된 적(candidates 중 하나)을 돌려준다. 이동이 필요 없거나(이미 공격 거리 안) 우회할 수 있거나 적을 치워도 못 가면 null.
	public static Unit FindBlockingEnemy(Unit unit, Unit boss, IList<Unit> candidates, int attackReach)
	{
		Vector2Int bossSize = boss.FootprintSize;

		// 이동이 필요 없다 — 이미 보스를 공격할 수 있는 거리면 사이의 잡몹에 끌려가지 않는다(보스 집중 유지).
		if (MovementMath.DistanceToFootprint(unit.position, boss.position, bossSize) <= attackReach) return null;
		if (!(unit.MovementAlgorithm is AStarMovement astar)) return null;

		// 우회할 수 있으면 막힌 게 아니다 — 다른 유닛의 점유를 피하고도 보스의 공격 거리 안 어느 타일에든 닿는다.
		if (astar.CanReachWithinRange(unit, boss.position, bossSize, attackReach)) return null;

		// 적을 치우면 열리는 길(벽·아군·닫힌 문 때문이라면 적을 치워도 못 간다)을 따라 처음 만나는 확인된 적이 막고 있는 적이다. 공격 위치에 닿은 뒤의 구간은 이동과 무관하다.
		if (!astar.TryGetPathTiles(unit, boss.position, out List<Vector2Int> tiles, ignoreEnemyUnits: true)) return null;
		foreach (var tile in tiles)
		{
			foreach (var cand in candidates)
				if (cand != null && cand != boss && cand.hp > 0 && Occupies(cand, tile)) return cand;
			if (MovementMath.DistanceToFootprint(tile, boss.position, bossSize) <= attackReach) break;
		}
		return null;
	}

	private static bool Occupies(Unit u, Vector2Int tile)
	{
		Vector2Int size = u.FootprintSize;
		return tile.x >= u.position.x && tile.x < u.position.x + size.x
			&& tile.y >= u.position.y && tile.y < u.position.y + size.y;
	}
}
