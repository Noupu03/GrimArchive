using System.Linq;
using UnityEngine;

public class RoomConfinedMovement : AStarMovement
{
    private Room _cachedRoom;
    private int _lastCheckFloor = -1;

    protected override bool IsTileWalkable(Unit unit, Vector2Int currentPos, Vector2Int neighborPos, Vector2Int dirVec, FactionData myData, int mapW, int mapH, int floorIdx, Vector2Int targetPos, out bool isOccupied)
    {
        // 부모(AStarMovement)의 기본 벽/유닛 충돌 검사 수행
        bool baseWalkable = base.IsTileWalkable(unit, currentPos, neighborPos, dirVec, myData, mapW, mapH, floorIdx, targetPos, out isOccupied);
        
        if (!baseWalkable) return false;

        // 추가 검사: 유닛이 소속된 방을 벗어나는 타일은 벽으로 취급
        if (GameSession.Instance != null && GameSession.Instance.allRooms != null)
        {
            if (_cachedRoom == null || _lastCheckFloor != floorIdx)
            {
                // 유닛의 현재 위치를 포함하는 방 찾기
                _cachedRoom = GameSession.Instance.allRooms.FirstOrDefault(r => r.Bounds.Contains(unit.position));
                _lastCheckFloor = floorIdx;
            }

            if (_cachedRoom != null)
            {
                // 다음 타일(neighborPos)이 방의 Bounds 내부에 있는지 확인
                if (!_cachedRoom.Bounds.Contains(neighborPos))
                {
                    return false; // 방을 벗어나면 길 없음(벽) 처리
                }
            }
        }

        return true;
    }
}
