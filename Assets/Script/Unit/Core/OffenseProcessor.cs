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

    // 점령 전환 MVP(2026-07-27, 사용자 요청) — "방 소유 진영의 마지막 유닛이 다른 진영 유닛에게
    // 죽으면 그 방은 죽인 진영 소속으로 전환된다." 기존 TryStartOffense/CheckOffenseSuccess(방에 들어간
    // 뒤 매 프레임 폴링, Wild→Player만 지원)와는 별개의 즉시(사망 시점) 이벤트 기반 경로다 — 이 새
    // 규칙이 Normal 타입 방의 전멸 판정을 사실상 선점하지만, Spawner 타입의 "거점 파괴로 성공" 경로는
    // 유닛 사망과 무관하므로 그대로 둔다. GameSession.RemoveDeadUnit에서 사망마다 호출된다.
    public void TryFlipRoomOwnershipOnDeath(Unit deadUnit)
    {
        if (deadUnit == null || GameSession.Instance == null) return;

        FactionType? deadFaction = MapToRoomFaction(deadUnit.FactionBehavior);
        if (deadFaction == null) return; // 방 소유권 개념이 없는 진영(FactionBehavior가 매핑 안 됨)

        UnityEngine.Vector3Int gridPos = new UnityEngine.Vector3Int(deadUnit.position.x, deadUnit.position.y, deadUnit.currentFloor);
        if (!GameSession.Instance.roomGrid.TryGetValue(gridPos, out Room room)) return;
        if (room.RoomFaction != deadFaction.Value) return; // 이 방은 애초에 죽은 유닛 진영 소유가 아님

        // 같은 진영의 다른 생존 유닛이 이 방에 남아있으면 "마지막 유닛"이 아니다. Room.Bounds는
        // 사각형 경계일 뿐이라 방 모양이 불규칙하면 인접한 다른 방과 겹쳐 오판할 수 있다(Room.cs 17-18행,
        // RoomConfinedMovement.cs와 동일한 문제) — roomGrid 타일 단위 조회로 정확히 같은 Room 인스턴스인지
        // 비교한다.
        foreach (var other in GameSession.Instance.units)
        {
            if (other == null || other == deadUnit || other.Health.hp <= 0) continue;
            if (MapToRoomFaction(other.FactionBehavior) != deadFaction.Value) continue;

            UnityEngine.Vector3Int otherGridPos = new UnityEngine.Vector3Int(other.position.x, other.position.y, other.currentFloor);
            if (GameSession.Instance.roomGrid.TryGetValue(otherGridPos, out Room otherRoom) && otherRoom == room)
                return;
        }

        Unit killer = deadUnit.lastDamageDealer;
        FactionType? killerFaction = killer != null ? MapToRoomFaction(killer.FactionBehavior) : null;
        if (killerFaction == null || killerFaction.Value == deadFaction.Value) return;

        room.RoomFaction = killerFaction.Value;
        _activeOffenseRooms.Remove(room); // 진행 중이던 폴링 기반 오펜스가 있었다면 정리
        _colorizer?.ChangeRoomColor(room, GetRoomOwnerColor(killerFaction.Value));

        LogHelper.Log($"[점령 전환] {room.RoomName} 방(F{room.Floor}): {deadFaction.Value} → {killerFaction.Value} (마지막 유닛 처치로 소속 전환, 가해자: {killer.unitType?.typeName})");
    }

    private static FactionType? MapToRoomFaction(IFactionBehavior behavior)
    {
        if (behavior is PlayerMonsterBehavior) return FactionType.Player;
        if (behavior is WildMonsterBehavior) return FactionType.Wild;
        if (behavior is HumanFactionBehavior) return FactionType.Human; // 2026-07-27, 사용자 요청 "인류도 소유권 있어"
        return null;
    }

    // 점령 색상(2026-07-27, 사용자 정정: "인류 전체가 파랑, 플레이어는 몬스터 소속이고 빨간색") —
    // MapRandering.HumanRoomTint/MonsterRoomTint(OccupationState 틴트 시스템)와 같은 색으로 맞춰서
    // 두 시스템이 같은 의미(인류=파랑/플레이어=빨강)를 일관되게 쓰도록 한다. Wild(야생 탈환)는 특별한
    // 색 없이 흰색(곱연산 항등원)으로 틴트를 해제해 원래 바닥색을 그대로 드러낸다.
    private static UnityEngine.Color GetRoomOwnerColor(FactionType faction) => faction switch
    {
        FactionType.Player => new UnityEngine.Color(1f, 0.25f, 0.25f, 1f),   // MapRandering.MonsterRoomTint와 동일
        FactionType.Human => new UnityEngine.Color(0.25f, 0.45f, 1f, 1f),   // MapRandering.HumanRoomTint와 동일
        _ => UnityEngine.Color.white,
    };

    private void OnOffenseSuccess(Room room)
    {
        // 건축물·자원·유닛 생산 MVP(2026-07-27) — 처치 보상은 이제 즉시 지급되므로(WildMonsterBehavior.
        // OnDeath) 오펜스 성공 시점의 별도 정산이 필요 없다.

        // 2. 방 소속 변경
        room.RoomFaction = FactionType.Player;
        _colorizer?.ChangeRoomColor(room, GetRoomOwnerColor(FactionType.Player));

        // 3. 활성 오펜스에서 제거
        _activeOffenseRooms.Remove(room);

        // 4. 오펜스 성공 로그 (MVP 8장)
        LogHelper.Log($"[오펜스 성공] {room.RoomName} 방 점령 완료 — 플레이어 진영으로 전환");
    }
}
