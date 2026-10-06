using System.Collections.Generic;

// 제자리 왕복(A-B-A-B 떨림) 감지 판정(순수) — OscillationDiagnostics가 쓴다. 최근 windowSeconds 안에 minMoves회 이상 움직였는데 서로 다른 타일이 maxDistinctTiles개 이하면 왕복 중으로 본다(직선 이동은 타일이 계속 늘고, 제자리 대기는 이동 자체가 없다).
public static class OscillationMath
{
	public const float WindowSeconds = 6f;
	public const int MinMoves = 8;
	public const int MaxDistinctTiles = 3;

	// times/xs/ys는 오래된 순으로 나란히 쌓인 이동 기록(이동 후 위치)이다.
	public static bool IsOscillating(IList<float> times, IList<int> xs, IList<int> ys, float now, float windowSeconds, int minMoves, int maxDistinctTiles, out int moves, out int distinctTiles)
	{
		moves = 0;
		distinctTiles = 0;
		for (int i = times.Count - 1; i >= 0; i--)
		{
			if (now - times[i] > windowSeconds) break;
			moves++;
		}
		if (moves < minMoves) return false;

		var tiles = new List<long>(maxDistinctTiles + 1);
		for (int i = times.Count - 1; i >= times.Count - moves; i--)
		{
			long key = ((long)xs[i] << 32) ^ (uint)ys[i];
			if (!tiles.Contains(key)) tiles.Add(key);
			if (tiles.Count > maxDistinctTiles) break; // 이미 왕복이 아니다
		}
		distinctTiles = tiles.Count;
		return distinctTiles <= maxDistinctTiles;
	}
}
