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

    // 오펜스(교전 중) 추적 전용 — 방 소유권 전환은 OnCoreDestroyed가 전담한다. 여기서는
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

    // 방 소유권 전환의 유일한 진입점 — 코어 체력이 0이 된 순간(UnitFunction.OnUpdate의 채널링
    // 데미지 적용 직후) 호출된다. 옛 "유닛 전멸/빈방 입성" 기반 경로를 완전히 대체 — 코어는 사라지지
    // 않고 반피로 즉시 회복돼 계속 뺏고 뺏기는 대상으로 남는다.
    public void OnCoreDestroyed(Room room, InteractableObject core, Unit killer)
    {
        if (room == null || core == null || killer == null) return;

        FactionType? claimant = MapToRoomFaction(killer.FactionBehavior);
        if (claimant == null) return; // 야생은 방 점령 개념이 없음(코어 공격 자체를 안 하지만 방어적으로 재확인)
        if (room.RoomFaction == claimant.Value)
        {
            // 이미 같은 진영 소유인데 코어가 파괴된 극단적 경우(동시 다중 공격 등) — 소유권은 안
            // 바꾸되 회복은 그대로 적용한다.
            core.CoreHp = core.CoreMaxHp * 0.5f;
            return;
        }

        _activeOffenseRooms.Remove(room);
        ApplyRoomOwnership(room, claimant.Value);
        core.CoreHp = core.CoreMaxHp * 0.5f;

        // 점령 VFX — 점령한 진영에 따라 다른 폭발 이펙트(VFX_CoreBoomHuman/Monster)를 코어 위치에 1회 재생.
        GameObject coreVisual = GameSession.Instance?.GetObjectVisual(core.Position);
        Vector3 vfxWorldPos = coreVisual != null
            ? coreVisual.transform.position
            : new Vector3(core.Position.x + 0.5f, core.Position.y + 0.5f, 0f);
        VFXManager.SpawnCoreCaptureVfx(claimant.Value, vfxWorldPos);

        LogHelper.Log($"[코어 파괴] {room.RoomName} 방(F{room.Floor}): {killer.unitType?.typeName}({claimant.Value})이 코어를 파괴해 점령 — 코어 체력 절반 회복.");
    }

    // FactionType(RoomFaction 쪽) → OccupationState(맵 데이터 쪽) 역매핑. GetRoomOwnerColor와
    // 나란히 두되 색이 아니라 CreateMap.Chunks.occupationState 갱신용.
    private static OccupationState MapToOccupationState(FactionType faction) => faction switch
    {
        FactionType.Player => OccupationState.PlayerControlled,
        FactionType.Human => OccupationState.HumanControlled,
        _ => OccupationState.Neutral,
    };

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

    // 방 소유권 전환 4줄(RoomFaction 대입 + 색칠 + CreateMap.Chunks.occupationState 동기화 + 플레이어
    // 점령 시 안개 해금) — OnCoreDestroyed가 사용한다. room.RoomFaction 대입 자체는 호출부가 "이미
    // 같은 소유면 return" 가드를 먼저 거치므로 여기서 다시 확인하지 않는다.
    private void ApplyRoomOwnership(Room room, FactionType faction)
    {
        room.RoomFaction = faction;
        _colorizer?.ChangeRoomColor(room, GetRoomOwnerColor(faction));
        GameSession.Instance?.cmap?.SetRoomOccupationState(room.Floor, room.RoomId, MapToOccupationState(faction));
        // 점령된 방과 인접 방의 안개를 함께 걷는다.
        if (faction == FactionType.Player) GameSession.Instance?.RevealFogAroundCapturedRoom(room);
    }
}
