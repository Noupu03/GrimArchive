using System.Collections.Generic;

public class FactionData
{
	// 동적 크기 맵 (0: 미탐색, 1: 바닥, 2: 벽) 타일맵 정보 공유
	public int[][,] discoveredMap;
	// 시야 내 발견된 적 유닛 데이터 공유
	public List<Unit> spottedEnemyUnits = new List<Unit>();

	// 재사용 가능한 BFS 탐색 전용 캐시 (캐릭터 팩션 내부 지도 데이터 활용)
	public int[,] bfsVisitedGrid;
	public int bfsVisitToken = 0;
	public Queue<UnityEngine.Vector2Int> bfsQueue = new Queue<UnityEngine.Vector2Int>(3000);

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
		int maxW = 0;
		int maxH = 0;
		for (int i = 0; i < floorCount; i++)
		{
			int w = cmap.map.floors[i].config.width  * 8;
			int h = cmap.map.floors[i].config.height * 8;
			discoveredMap[i] = new int[w, h];
			if (w > maxW) maxW = w;
			if (h > maxH) maxH = h;
		}
		
		// 가장 큰 층을 기준으로 BFS 탐색용 캐시 그리드 초기화
		bfsVisitedGrid = new int[maxW + 20, maxH + 20];
	}

	public void ClearSpottedUnits() => spottedEnemyUnits.Clear();
}
