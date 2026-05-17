using System.Collections.Generic;

public class FactionData
{
	// 동적 크기 맵 (0: 미탐색, 1: 바닥, 2: 벽) 타일맵 정보 공유
	public int[][,] discoveredMap;
	// 시야 내 발견된 적 유닛 데이터 공유
	public List<Unit> spottedEnemyUnits = new List<Unit>();

	public FactionData()
	{
		// 기본적으로 빈 배열로 두거나, InitMap에서 초기화
		discoveredMap = new int[0][,];
	}

	public void InitMap(CreateMap cmap)
	{
		if (cmap == null || cmap.map.floors == null) return;
		int floorCount = cmap.map.floors.Length;
		discoveredMap = new int[floorCount][,];
		for (int i = 0; i < floorCount; i++)
		{
			int w = cmap.map.floors[i].config.width  * 8;
			int h = cmap.map.floors[i].config.height * 8;
			discoveredMap[i] = new int[w, h];
		}
	}

	public void ClearSpottedUnits() => spottedEnemyUnits.Clear();
}
