using UnityEngine;

// 04번 문서(이동경로_속도_점유충돌) 4번 항목 순수 계산 상수 모음.
public static class MovementMath
{
	// 목표가 여러 타일을 차지할 때(보스 3×3 등) 한 타일 p에서 그 점유 영역까지의 체비셰프 거리 — 영역 안이면 0. anchor는 영역의 좌하단(Unit.position),
	// size는 점유 크기(Unit.FootprintSize). "공격 가능한 거리"를 앵커 한 점이 아니라 실제 점유 영역 기준으로 재는 데 쓴다.
	public static int DistanceToFootprint(Vector2Int p, Vector2Int anchor, Vector2Int size)
	{
		int dx = Mathf.Max(0, Mathf.Max(anchor.x - p.x, p.x - (anchor.x + size.x - 1)));
		int dy = Mathf.Max(0, Mathf.Max(anchor.y - p.y, p.y - (anchor.y + size.y - 1)));
		return Mathf.Max(dx, dy);
	}

	// "회피 가능 경로 우선 → 노출시간 최소 → 전체길이 최소"라는 3단계 우선순위를, 회피 대상 타일 1칸당
	// 이 상수만큼 이동비용을 더하는 방식으로 하나의 가중치 합으로 성립시킨다. 이 프로토타입 맵 규모에서
	// 나올 수 있는 기본 경로비용 차이(수백~수천 수준)보다 압도적으로 커야 사전식(lexicographic) 순서가
	// 깨지지 않는다.
	public const int AttackRangeAvoidExtraCost = 100000;

	// 검증 03-13(활성 함정): 전투 모드에서 통과가 허용된 함정 타일 1칸에 더하는 비용 — 공격범위 회피 비용보다 커서
	// "함정을 안 밟는 우회를 먼저 본다"(03번 v0.12 9장 통과 판단표)가 사전식으로 성립한다.
	public const int TrapPassExtraCost = 200000;

	// 일반 모드에서 이미 회피 구역 안에 서 있을 때 구역 타일 1칸당 비용 — 구역 타일을 가장 적게 밟고 밖으로 나오게 한다(v0.6 9-6 탈출).
	public const int TrapZoneEscapeExtraCost = 100000;
}
