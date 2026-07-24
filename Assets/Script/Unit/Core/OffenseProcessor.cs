using System.Collections.Generic;
using Haare.Util.Logger;
using VContainer;

public class OffenseProcessor
{
    // 순환 의존성 방지: UpdateProcess 내 유닛 조회는 GameSession.Instance 직접 참조
    private readonly IMapColorizer _colorizer;
    private readonly HashSet<Room> _activeOffenseRooms = new HashSet<Room>();

    [Inject]
    public OffenseProcessor(IMapColorizer colorizer)
    {
        _colorizer = colorizer;
    }

    // 하위 호환성 — 첫 번째 활성 방 반환 (디버그 창 등)
    public Room currentOffenseRoom
    {
        get { foreach (var r in _activeOffenseRooms) return r; return null; }
    }

    public bool IsRoomInOffense(Room room) => _activeOffenseRooms.Contains(room);

    // 멱등(idempotent): 이미 진행 중인 방이면 무시, 야생 방이 아니면 무시
    public void TryStartOffense(Room room, Unit playerUnit)
    {
        if (room == null || room.RoomFaction != FactionType.Wild) return;
        if (_activeOffenseRooms.Contains(room)) return;

        _activeOffenseRooms.Add(room);
        LogHelper.Log($"[오펜스 시작] {room.RoomName} 방에 플레이어 유닛 진입 — 오펜스 개시");
    }

    // 하위 호환성 (디버그 창 호출용)
    public void StartOffense(Room room, Unit playerUnit) => TryStartOffense(room, playerUnit);

    public void UpdateProcess()
    {
        if (GameSession.Instance == null || _activeOffenseRooms.Count == 0) return;

        List<Room> succeeded = null;

        foreach (var room in _activeOffenseRooms)
        {
            if (CheckOffenseSuccess(room))
            {
                if (succeeded == null) succeeded = new List<Room>();
                succeeded.Add(room);
            }
        }

        if (succeeded != null)
        {
            foreach (var room in succeeded)
                OnOffenseSuccess(room);
        }
    }

    private bool CheckOffenseSuccess(Room room)
    {
        if (room.Type == RoomType.Normal)
        {
            // 야생 무리형: 방 안 야생 유닛 전멸
            var roomUnits = GameSession.Instance.GetUnitsInRoom(room.Bounds);
            foreach (var u in roomUnits)
                if (u.FactionBehavior is WildMonsterBehavior) return false;
            return true;
        }
        else
        {
            // 야생 거점형: 생성 거점 파괴만으로 성공 (MVP 문서 4.2 — 남은 야생 유닛 무관)
            return !room.HasActiveSpawner;
        }
    }

    private void OnOffenseSuccess(Room room)
    {
        // 1. 자원 B 정산
        ResourceAccumulator.Instance?.CommitResourceB();

        // 2. 방 소속 변경
        room.RoomFaction = FactionType.Player;
        _colorizer?.ChangeRoomColor(room, new UnityEngine.Color(0.4f, 0.6f, 1f, 1f));

        // 3. 활성 오펜스에서 제거
        _activeOffenseRooms.Remove(room);

        // 4. 오펜스 성공 로그 (MVP 8장)
        LogHelper.Log($"[오펜스 성공] {room.RoomName} 방 점령 완료 — 플레이어 진영으로 전환, 자원 B 지급");
    }
}
