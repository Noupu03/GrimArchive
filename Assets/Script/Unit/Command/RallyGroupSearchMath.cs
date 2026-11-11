using UnityEngine;

// 미집결자 수색을 모인 인원이 함께 하는 흐름의 순수 계산(파티 구성 명세서 8-1장 3·4). 세션 없이 테스트한다 — 상태·이동은 Party.Command.cs가 맡는다.
public static class RallyGroupSearchMath
{
	// 4방향 분담의 개수 — 전방·우측·후방·좌측.
	public const int FacingCount = 4;

	// 관찰 지점으로 쓸 수 있는 칸인가: 서 있을 수 있고, 시체가 점유한 칸이 아니며(8-1장 3번 "시체 점유 타일을 정지 목적지로 잡지 않는다"), 수색 위치가 직선으로 보인다("해당 위치를 관찰할 수 있는 유효 지점").
	public static bool IsObservationTile(bool walkable, bool occupiedByCorpse, bool lineToTargetClear)
		=> walkable && !occupiedByCorpse && lineToTargetClear;

	// 관찰 방향 분담(8-1장 4번 "전방·후방·좌우를 나눠 5초간 수색"): index 0은 수색 위치 쪽(전방), 1은 그 오른쪽, 2는 후방, 3은 왼쪽이고 인원이 많으면 순환한다.
	// heading은 관찰 자리에서 수색 위치로 가는 방향 — 성분만 −1/0/1로 접고, 같은 칸이면 아래쪽을 전방으로 본다. 반환은 −1/0/1 방향 벡터(8방향).
	public static Vector2Int FacingVector(int index, Vector2Int heading)
	{
		var h = new Vector2Int(System.Math.Sign(heading.x), System.Math.Sign(heading.y));
		if (h == Vector2Int.zero) h = new Vector2Int(0, -1);
		int turns = ((index % FacingCount) + FacingCount) % FacingCount;
		for (int i = 0; i < turns; i++) h = new Vector2Int(h.y, -h.x); // 시계 방향 90도: (x, y) → (y, −x)
		return h;
	}
}
