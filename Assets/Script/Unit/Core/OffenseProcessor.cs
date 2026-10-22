using System.Collections.Generic;
using Haare.Util.Logger;
using VContainer;
using UnityEngine;

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

        // 다음 방 미리 밝히기 — 이 방 자체는 SyncRoomAffiliation(UnitFunction.cs)이 진입 시점에 이미
        // RevealRoomFog로 걷지만, Gate로 직접 연결된 다음 방까지는 걷어주지 않는다. 점령 시 쓰는
        // RevealFogAroundCapturedRoom(자기 자신 + 인접 방까지 해제)을 그대로 재사용 — idempotent라
        // 이미 걷힌 방에 걸어도 안전하다.
        GameSession.Instance?.RevealFogAroundCapturedRoom(room);
    }

    // 하위 호환성 (디버그 창 호출용)
    public void StartOffense(Room room, Unit playerUnit) => TryStartOffense(room, playerUnit);

    // 오펜스(교전 중) 추적 전용 — 방 소유권 전환은 ApplyRoomOwnership(RoomClearSystem)이 맡는다. 여기서는
    // IdleFSMState가 "이 방이 지금 교전 중이라 평시 배회를 하면 안 된다"를 판단하는 상태만 유지한다
    // (DefenseProcessor.UpdateProcess와 동일한 "침입자 전멸 시 종료" 관례) — 야생이 전멸하면 교전 종료.
    public void UpdateProcess()
    {
        if (GameSession.Instance == null || _activeOffenseRooms.Count == 0) return;

        List<Room> ended = null;
        foreach (var room in _activeOffenseRooms)
        {
            if (!HasLivingWild(room))
            {
                if (ended == null) ended = new List<Room>();
                ended.Add(room);
            }
        }

        if (ended == null) return;
        foreach (var room in ended)
        {
            _activeOffenseRooms.Remove(room);
            LogHelper.Log($"[오펜스 종료] {room.RoomName} 방(F{room.Floor}) — 야생 전멸/철수.");
        }
    }

    private static bool HasLivingWild(Room room)
    {
        var roomUnits = GameSession.Instance.GetUnitsInRoom(room.Bounds);
        foreach (var u in roomUnits)
        {
            if (u == null || u.hp <= 0) continue;
            if (u.FactionBehavior is WildMonsterBehavior) return true;
        }
        return false;
    }

    // RoomFaction → 맵 데이터(CreateMap.Chunks.occupationState). 건설 권한(CreateMap.IsPositionPlayerOwned)이 이 값을 읽는다.
    private static OccupationState MapToOccupationState(FactionType faction)
        => faction == FactionType.Player ? OccupationState.PlayerControlled : OccupationState.Neutral;

    // TacticalFSMState.HasCoreAttackTarget/FindHostileRoomCore도 사용(코어 공격 대상이 자기 진영
    // 소유 방인지 판정) — Room.RoomFaction/OccupationState와 대응되는 FactionType이 없는 진영은 null.
    public static FactionType? MapToRoomFaction(IFactionBehavior behavior)
    {
        if (behavior is PlayerMonsterBehavior) return FactionType.Player;
        if (behavior is WildMonsterBehavior) return FactionType.Wild;
        if (behavior is HumanFactionBehavior) return FactionType.Human;
        return null;
    }

    // 점령 색상 — 야생은 검은색, 인류는 파란색, 플레이어 몬스터는 빨간색 바닥 윤곽선.
    // MapRandering.*RoomOutlineColor를 직접 참조해 두 경로가 항상 같은 팔레트를 쓴다.
    public static UnityEngine.Color GetRoomOwnerColor(FactionType faction) => faction switch
    {
        FactionType.Player => MapRandering.MonsterRoomOutlineColor,
        FactionType.Human => MapRandering.HumanRoomOutlineColor,
        _ => MapRandering.WildRoomOutlineColor,
    };

    // 플레이어 방 확보(RoomFaction)의 단일 진입점 — 방 클리어(RoomClearSystem)만 부른다. 코어 파괴도, 인류 거점 건설도 방 소유를 바꾸지 않는다(코어 점령 v0.2 1-1·12-2장: 인류 시설 완공이 플레이어의 확보를 제거하지 않는다).
    // 방 테두리(색 + 펄스)는 이 소유 변경만 표현한다 — 인류 거점(BaseStage)은 테두리를 쓰지 않아 확보와 겹치지 않는다(2026-10-22).
    // 이미 같은 소유면 아무것도 하지 않고 false를 돌려준다.
    public bool ApplyRoomOwnership(Room room, FactionType faction)
    {
        if (room == null || room.RoomFaction == faction) return false;
        _activeOffenseRooms.Remove(room);
        room.RoomFaction = faction;
        GameSession.Instance?.cmap?.SetRoomOccupationState(room.Floor, room.RoomId, MapToOccupationState(faction));
        if (faction == FactionType.Player) GameSession.Instance?.RevealFogAroundCapturedRoom(room);
        _colorizer?.ChangeRoomColor(room, GetRoomOwnerColor(faction));
        _colorizer?.PulseRoomOutline(room);
        return true;
    }
}
