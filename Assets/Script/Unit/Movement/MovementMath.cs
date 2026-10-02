using UnityEngine;

// 04번 문서(이동경로_속도_점유충돌) 4번 항목 순수 계산 상수 모음.
public static class MovementMath
{
	// 목표가 여러 타일을 차지할 때(보스 3×3 등) 한 타일 p에서 그 점유 영역까지의 체비셰프 거리(영역 안이면 0). anchor는 좌하단(Unit.position), size는 Unit.FootprintSize — '공격 가능한 거리'를 앵커 한 점이 아니라 실제 점유 영역 기준으로 재는 데 쓴다.
	public static int DistanceToFootprint(Vector2Int p, Vector2Int anchor, Vector2Int size)
	{
		int dx = Mathf.Max(0, Mathf.Max(anchor.x - p.x, p.x - (anchor.x + size.x - 1)));
		int dy = Mathf.Max(0, Mathf.Max(anchor.y - p.y, p.y - (anchor.y + size.y - 1)));
		return Mathf.Max(dx, dy);
	}

	// 플레이어 이동 명령이 한 걸음도 못 다가간 틱의 처리 — Retarget = 목표를 옆 빈 칸으로 바꿔 계속, Retry = 잠깐 몰려 막힌 것으로 보고 재시도, Abort = '이동 불가' 표시 후 종료.
	public enum MoveFailAction { Retarget, Retry, Abort }

	// stuckTurns는 이번 실패를 포함한 연속 실패 틱 수(이동이 한 번이라도 성공하면 호출부가 0으로). 한도에 닿으면 대체 칸이 있어도 포기한다 — 대체 칸이 닿을 수 없는 곳(문 반대편 등)이면 재지정만 반복돼 포기 조건에 못 닿으므로 재지정은 카운터를 리셋하지 않는다.
	public static MoveFailAction ResolveMoveFailure(int stuckTurns, int stuckTurnLimit, bool hasFallbackTarget, bool hasStructurallyOpenAdjacentTile)
	{
		if (stuckTurns >= stuckTurnLimit) return MoveFailAction.Abort;
		if (hasFallbackTarget) return MoveFailAction.Retarget;
		return hasStructurallyOpenAdjacentTile ? MoveFailAction.Retry : MoveFailAction.Abort;
	}

	// from에서 to까지 타일 직선(브레젠험)이 차폐물에 막히지 않는가 — 양 끝 타일은 검사하지 않고, 대각선으로 넘을 때 양옆 직교 타일이 둘 다 막혀 있으면 틈으로 새지 못하게 막는다. isBlocking은 호출부가 정하는 '이 타일이 직선을 막는가'(벽·구조물·닫힌 문 등).
	public static bool IsLineClear(Vector2Int from, Vector2Int to, System.Func<Vector2Int, bool> isBlocking)
	{
		int dx = Mathf.Abs(to.x - from.x), dy = Mathf.Abs(to.y - from.y);
		int sx = from.x < to.x ? 1 : -1, sy = from.y < to.y ? 1 : -1;
		int err = dx - dy;
		Vector2Int cur = from;
		while (cur != to)
		{
			int e2 = 2 * err;
			bool stepX = e2 > -dy, stepY = e2 < dx;
			Vector2Int next = cur;
			if (stepX) { err -= dy; next.x += sx; }
			if (stepY) { err += dx; next.y += sy; }
			if (stepX && stepY && isBlocking(new Vector2Int(next.x, cur.y)) && isBlocking(new Vector2Int(cur.x, next.y))) return false;
			cur = next;
			if (cur != to && isBlocking(cur)) return false;
		}
		return true;
	}

	// '회피 가능 경로 우선 → 노출시간 최소 → 전체길이 최소' 3단계 우선순위를 회피 대상 타일 1칸당 이 상수만큼 비용을 더하는 하나의 가중치 합으로 성립시킨다 — 기본 경로비용 차이(수백~수천)보다 압도적으로 커야 사전식 순서가 깨지지 않는다.
	public const int AttackRangeAvoidExtraCost = 100000;

	// 전투 모드에서 통과가 허용된 함정 타일 1칸에 더하는 비용 — 공격범위 회피 비용보다 커서
	// "함정을 안 밟는 우회를 먼저 본다"(03번 v0.12 9장 통과 판단표)가 사전식으로 성립한다.
	public const int TrapPassExtraCost = 200000;

	// 일반 모드에서 이미 회피 구역 안에 서 있을 때 구역 타일 1칸당 비용 — 구역 타일을 가장 적게 밟고 밖으로 나오게 한다(v0.6 9-6 탈출).
	public const int TrapZoneEscapeExtraCost = 100000;
}
