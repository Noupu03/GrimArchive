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

        // 플레이어 진영 몬스터 방 제한 MVP(2026-07-27, 사용자 요청) — "오직 플레이어의 명령에 의해서만
        // 다른 방으로 이동 가능". 플레이어가 직접 내린 이동 명령(우클릭, PlayerCommandFSMState가 이
        // 플래그로 판단)이면 방 경계 제한을 건너뛴다. 야생 몬스터는 isManualMoveCommand가 절대 true가
        // 되지 않으므로(플레이어가 조종하지 않음) 이 분기는 기존 야생 몬스터 동작에 영향 없다.
        if (unit.isManualMoveCommand) return true;

        // 배회 금지(2026-07-28, 사용자 요청 "배회하는 야생 몬스터가, 문이 있는 공간을 돌아다니지
        // 못하게") — 문/통로 타일은 그 문을 낀 두 방 중 한쪽의 roomGrid 소유 청크에 포함돼 있어(같은
        // 방 소속으로 잡힘) 아래 방 경계 검사만으로는 걸러지지 않는다. GameSession.IsDoorTile은 원래
        // "배치"만 막는 용도였지만(주석 참고) 여기서는 자율 이동(플레이어 명령이 아닌 경우, 위에서
        // 이미 걸러짐)에도 문 타일 자체를 walkable에서 제외해 방 안쪽에서만 배회하게 한다.
        if (unit.Session != null && unit.Session.IsDoorTile(new Vector3Int(neighborPos.x, neighborPos.y, floorIdx)))
            return false;

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
