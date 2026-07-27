using UnityEngine;

public class RoomConfinedMovement : AStarMovement
{
    private Room _cachedRoom;
    private int _lastCheckFloor = -1;
    private Vector2Int _lastCheckPos;

    protected override bool IsTileWalkable(Unit unit, Vector2Int currentPos, Vector2Int neighborPos, Vector2Int dirVec, FactionData myData, int mapW, int mapH, int floorIdx, Vector2Int targetPos, out bool isOccupied)
    {
        // 부모(AStarMovement)의 기본 벽/유닛 충돌 검사 수행
        bool baseWalkable = base.IsTileWalkable(unit, currentPos, neighborPos, dirVec, myData, mapW, mapH, floorIdx, targetPos, out isOccupied);

        if (!baseWalkable) return false;

        // 추가 검사: 유닛이 소속된 방을 벗어나는 타일은 벽으로 취급
        if (unit.Session != null && unit.Session.roomGrid != null)
        {
            // 2026-07-27 버그 수정 — 예전엔 allRooms.FirstOrDefault(r => r.Bounds.Contains(unit.position))로
            // 층 구분 없이 찾아서, 여러 층의 Room이 비슷한 로컬 좌표 범위를 쓰는 경우(거의 항상 그렇다)
            // 엉뚱한 층의 방을 집어 이동 자체가 막히거나 다른 방으로 새는 버그가 있었다(GameSession.
            // BuildRoomGrid가 1층만 처리하던 동안은 우연히 안 드러났음). roomGrid는 Vector3Int(x,y,floor)
            // 키라 층이 자동으로 구분된다 — 정확한 타일 단위 조회로 교체.
            if (_cachedRoom == null || _lastCheckFloor != floorIdx || _lastCheckPos != unit.position)
            {
                unit.Session.roomGrid.TryGetValue(new Vector3Int(unit.position.x, unit.position.y, floorIdx), out _cachedRoom);
                _lastCheckFloor = floorIdx;
                _lastCheckPos = unit.position;
            }

            if (_cachedRoom != null)
            {
                if (!unit.Session.roomGrid.TryGetValue(new Vector3Int(neighborPos.x, neighborPos.y, floorIdx), out Room neighborRoom)
                    || neighborRoom != _cachedRoom)
                {
                    return false; // 방을 벗어나면 길 없음(벽) 처리
                }
            }
        }

        return true;
    }
}
