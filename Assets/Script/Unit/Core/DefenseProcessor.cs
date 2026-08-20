using System.Collections.Generic;
using Haare.Util.Logger;

// OffenseProcessor의 대칭 개념(2026-08-20, 사용자 요청 "대기 상태를 새로 만들어줘... 오펜스, 디펜스
// 중이 아닌 방의 경우 유닛은... 1칸 이동 후 정지를 반복") — 플레이어 소유 방에 적대 진영(야생/인류)
// 유닛이 침입한 "디펜스 중" 상태를 추적한다. OffenseProcessor와 달리 방 소유권 전환 책임은 없다(그건
// 이미 OffenseProcessor.TryResolveRoomOwnership/GameSession.RemoveDeadUnit 경로가 담당) — 여기서는
// 오직 "지금 이 방이 침입받고 있는가"만 들고 있는다. IdleFSMState가 "이 방이 지금 전투 중이라 평시
// 배회를 하면 안 된다"는 판단에 이 값을 쓴다(사용자 확인, 2026-08-20 — OffenseProcessor와 동일하게
// 전용 추적 시스템으로 신설).
public class DefenseProcessor
{
    private readonly HashSet<Room> _activeDefenseRooms = new HashSet<Room>();

    public bool IsRoomInDefense(Room room) => room != null && _activeDefenseRooms.Contains(room);

    // 멱등(idempotent) — 이미 디펜스 중인 방이거나 플레이어 소유가 아닌 방이면 무시.
    public void TryStartDefense(Room room, Unit invader)
    {
        if (room == null || room.RoomFaction != FactionType.Player) return;
        if (_activeDefenseRooms.Contains(room)) return;

        _activeDefenseRooms.Add(room);
        LogHelper.Log(LogHelper.GAME, $"[디펜스 시작] {room.RoomName} 방에 적대 유닛({invader?.unitType?.typeName}) 진입 — 디펜스 개시");
    }

    // OffenseProcessor.UpdateProcess와 동일한 폴링 관례 — 침입자(야생/인류)가 방 안에 하나도 안 남으면
    // 디펜스 종료로 본다. 방 소유권은 건드리지 않는다(이미 다른 경로가 담당).
    public void UpdateProcess()
    {
        if (GameSession.Instance == null || _activeDefenseRooms.Count == 0) return;

        List<Room> ended = null;
        foreach (var room in _activeDefenseRooms)
        {
            if (!HasLivingInvader(room))
            {
                if (ended == null) ended = new List<Room>();
                ended.Add(room);
            }
        }

        if (ended == null) return;
        foreach (var room in ended)
        {
            _activeDefenseRooms.Remove(room);
            LogHelper.Log(LogHelper.GAME, $"[디펜스 종료] {room.RoomName} 방 — 침입 유닛 전멸/철수");
        }
    }

    // Room.ContainedUnits는 야생 몬스터를 대상에서 제외한다(SyncRoomAffiliation이 야생을 안 건드림 —
    // OffenseProcessor.CheckOffenseSuccess와 동일한 이유로 GetUnitsInRoom(방 실제 좌표 범위)을 쓴다.
    private static bool HasLivingInvader(Room room)
    {
        var roomUnits = GameSession.Instance.GetUnitsInRoom(room.Bounds);
        foreach (var u in roomUnits)
        {
            if (u == null || u.hp <= 0) continue;
            if (u.FactionBehavior is WildMonsterBehavior || u.FactionBehavior is HumanFactionBehavior) return true;
        }
        return false;
    }
}
