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

        // 방 이동은 오직 플레이어 명령으로만 허용 — isManualMoveCommand(PlayerCommandFSMState가 세팅)면
        // 방 경계 제한을 건너뛴다. 야생 몬스터는 이 플래그가 절대 true가 되지 않아 영향 없다.
        if (unit.isManualMoveCommand) return true;

        // 문/통로 타일은 한쪽 방의 roomGrid 소유 청크에 포함돼 같은 방 소속으로 잡히므로 아래 방
        // 경계 검사만으로는 못 거른다 — 자율 이동(플레이어 명령 아님)에서는 문 타일 자체를 walkable에서
        // 제외해 방 안쪽에서만 배회하게 한다.
        if (unit.Session != null && unit.Session.IsDoorTile(new Vector3Int(neighborPos.x, neighborPos.y, floorIdx)))
            return false;

        // 추가 검사: 유닛이 소속된 방을 벗어나는 타일은 벽으로 취급
        if (unit.Session != null && unit.Session.roomGrid != null)
        {
            // 층 구분 없이 좌표 범위로 방을 찾으면(여러 층 Room이 비슷한 로컬 좌표를 쓰므로) 엉뚱한
            // 층의 방을 집어 이동이 막히거나 새는 버그가 생긴다 — roomGrid는 Vector3Int(x,y,floor)
            // 키라 층이 자동 구분되므로 반드시 이 타일 단위 조회를 써야 한다.
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
