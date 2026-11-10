using UnityEngine;

// 사망 클립이 끝난 유닛 비주얼을 그대로 시체로 남길 때의 순수 계산 (회의록 2026-11-10).
// 예전엔 클립이 끝나면 유닛 비주얼을 지우고 시체 오브젝트가 별도 corpseSprite를 타일 중앙 기준으로 그렸는데,
// 유닛 비주얼은 발 기준이라 두 위치가 어긋나 시체가 위로 떠 보였다. 이제 클립의 마지막 프레임을 시체 오브젝트가 이어받는다.
public static class CorpseVisualMath
{
	// 시체 오브젝트 스프라이트의 정렬 순서와 같다(GameSession.SpawnObject) — 산 유닛(몸 100~, 실측)보다 아래에 깔린다.
	public const int CorpseSortingBase = 5;

	// 보이는 부위들의 상대 순서는 그대로 두고 가장 낮은 부위가 CorpseSortingBase가 되도록 옮긴다.
	public static int RemapSortingOrder(int order, int lowestVisibleOrder)
		=> CorpseSortingBase + (order - lowestVisibleOrder);

	// 시체 오브젝트의 논리 타일이 사망 위치와 다를 때(함정 칸이라 옆 빈 타일로 밀린 경우) 비주얼을 같은 만큼 옮긴다.
	public static Vector3 TileShift(Vector2Int deathTile, Vector3Int corpseTile)
		=> new Vector3(corpseTile.x - deathTile.x, corpseTile.y - deathTile.y, 0f);
}
