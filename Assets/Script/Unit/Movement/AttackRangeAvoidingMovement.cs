using System.Collections.Generic;
using UnityEngine;

// 04번 문서 4장: 원거리공격/근접·원거리지원 역할이 "알려진 적 공격 범위"를 회피하며 이동하게 만드는
// AStarMovement 변형 — RoomConfinedMovement가 IsTileWalkable만 오버라이드하는 것과 동일한 서브클래싱
// 전례를 GetExtraTileCost에 적용한 것. 몬스터(RoomConfinedMovement 전용, 04번 4장 표의 "몬스터" 행 —
// 종류·역할 설정과 방·활동 범위로 별도 관리됨)에는 이 클래스를 배정하지 않는다.
public class AttackRangeAvoidingMovement : AStarMovement
{
	private Unit _cacheUnit;
	private int _cacheFrame = -1;
	private readonly HashSet<Vector2Int> _avoidTiles = new HashSet<Vector2Int>();

	protected override int GetExtraTileCost(Unit unit, Vector2Int tilePos)
	{
		// 회피 집합 계산은 프레임당 1회로 제한 — 캐시 없으면 탐색 1회 중 최대 수만 번 호출될 수 있다.
		if (_cacheUnit != unit || _cacheFrame != Time.frameCount)
		{
			_avoidTiles.Clear();
			AIMovementHelper.ComputeKnownAttackRangeAvoidTiles(unit, _avoidTiles);
			_cacheUnit = unit;
			_cacheFrame = Time.frameCount;
		}
		return _avoidTiles.Contains(tilePos) ? MovementMath.AttackRangeAvoidExtraCost : 0;
	}
}
