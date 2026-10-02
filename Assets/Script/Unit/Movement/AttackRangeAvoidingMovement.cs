using System.Collections.Generic;
using UnityEngine;

// 원거리공격/근접·원거리지원 역할이 '알려진 적 공격 범위'를 회피하며 이동하게 하는 AStarMovement 변형(04번 4장) — RoomConfinedMovement가 IsTileWalkable을 오버라이드하는 전례를 GetExtraTileCost에 적용했다. 몬스터(RoomConfinedMovement 전용)에는 배정하지 않는다.
public class AttackRangeAvoidingMovement : AStarMovement
{
	private Unit _cacheUnit;
	private int _cacheFrame = -1;
	private readonly HashSet<Vector2Int> _avoidTiles = new HashSet<Vector2Int>();

	public override AStarMovement CreateStructuralTwin() => new AttackRangeAvoidingMovement { IgnoreAllUnits = true };

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
		// 알려진 함정 회피 비용(전투 모드 통과 허용 타일·구역 탈출)도 함께 더한다.
		return (_avoidTiles.Contains(tilePos) ? MovementMath.AttackRangeAvoidExtraCost : 0) + base.GetExtraTileCost(unit, tilePos);
	}
}
