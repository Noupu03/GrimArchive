using UnityEngine;

// 유닛 개인이 "직접 확인했거나 전달받아 아는" 지형 — 길찾기(AStarMovement)가 진영 공용 discoveredMap 대신 읽는다(검증 04-08, 04번 0장 "개인이 아는 지형"으로만 계산).
// 값 관례는 discoveredMap과 같다: 0=미확인, 1=바닥, 2=벽. TerrainRevision은 아는 지형이 실제로 바뀔 때마다 오르며 경로 판정 메모(RouteAssessment)의 서명으로 쓴다.
public interface IKnownTerrain
{
	int GetTileTerrain(Vector3Int pos);
	int TerrainRevision { get; }
}
