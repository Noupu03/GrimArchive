using System;
using UnityEngine;

// 실제 지형 전체(미탐색 구역 포함)를 길찾기에 읽히는 IKnownTerrain — 플레이어 몬스터의 클릭 이동 명령 동안만 AStarMovement.BeginSearch가 유닛이 시야로 아는 지형 대신 이걸 쓴다(2026-11-04, Unit.PathTerrain).
// 통행 판정은 UnitFunction.CanMove와 같은 CreateMap.IsStaticTileWalkable(벽·구조물 포함)이라 A*와 Move()가 어긋나지 않는다. 지형을 읽기만 하고 아는 지도에는 아무것도 기록하지 않는다(걸으며 시야로 밝히는 것은 기존대로).
// 닫힌 문·점유·알려진 함정은 지형이 아니라 AStarMovement.IsTileWalkable이 따로 판정한다.
public sealed class GlobalTerrain : IKnownTerrain
{
	// 테스트가 세션 없이 가짜 지도를 꽂는다 — 평소엔 null이고 Unit.PathTerrain이 GameSession.GlobalTerrain(실제 CreateMap)을 쓴다.
	public static GlobalTerrain TestTerrain;

	private readonly Func<int, Vector2Int, bool> _isWalkable;

	public GlobalTerrain(Func<int, Vector2Int, bool> isWalkable) => _isWalkable = isWalkable;

	public GlobalTerrain(CreateMap cmap) : this((floor, p) => cmap.IsStaticTileWalkable(floor, p)) { }

	// IKnownTerrain 규약: 0 = 미확인, 1 = 바닥, 2 = 벽. 실제 지형은 미확인이 없다.
	public int GetTileTerrain(Vector3Int pos) => _isWalkable(pos.z, new Vector2Int(pos.x, pos.y)) ? 1 : 2;

	// 정적 지형이라 개정 번호가 없다 — 경로 판정 메모(RouteAssessment)는 이 지형을 읽지 않는다.
	public int TerrainRevision => 0;
}
