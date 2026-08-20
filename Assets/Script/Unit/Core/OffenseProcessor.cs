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
            bool hasHuman = false, hasPlayerMonster = false;
            foreach (var u in roomUnits)
            {
                if (u.FactionBehavior is WildMonsterBehavior) return false;
                if (u.FactionBehavior is HumanFactionBehavior) hasHuman = true;
                else if (u.FactionBehavior is PlayerMonsterBehavior) hasPlayerMonster = true;
            }
            // 점령 보류 규칙(2026-07-28, 사용자 요청 "야생 유닛 막타쳐서 점령했을때, 만약 인류, 몬스터
            // 같이 방에 존재한다면, 점령되지 않고, 한 진영만 남을때까지 점령되지 않게 해줘") — 야생이
            // 전멸해도 인류·몬스터가 동시에 살아있으면 아직 결정 보류. 폴링 방식이라 다음 프레임에
            // 다시 확인한다(_activeOffenseRooms에서 제거하지 않음).
            if (hasHuman && hasPlayerMonster) return false;
            return true;
        }
        else
        {
            // 야생 거점형: 생성 거점 파괴만으로 성공 (MVP 문서 4.2 — 남은 야생 유닛 무관, 이 갈래는
            // 위 점령 보류 규칙 대상이 아니다 — 문서 그대로 유지).
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
        if (MapToRoomFaction(deadUnit.FactionBehavior) == null) return; // 방 소유권 개념이 없는 진영

        UnityEngine.Vector3Int gridPos = new UnityEngine.Vector3Int(deadUnit.position.x, deadUnit.position.y, deadUnit.currentFloor);
        if (!GameSession.Instance.roomGrid.TryGetValue(gridPos, out Room room)) return;

        TryResolveRoomOwnership(room, deadUnit.unitType?.typeName ?? "유닛");
    }

    // 점령 재계산(2026-07-28 재정정, 사용자 신고 "세 진영 유닛 동시에 존재할 때 점령이 작동 안해...
    // 야생 몬스터와 플레이어 몬스터 유닛 다 죽고 인류만 남으면 점령 처리가 되어야 하는데 안됨(문은
    // 열림)") — 예전(TryFlipRoomOwnershipOnDeath 구버전)엔 "방금 죽은 유닛의 진영이 이 방의 *현재*
    // RoomFaction과 같아야만" 점령 전환을 시도했다. 그런데 3파전 도중 야생이 먼저 죽어도 아래 점령
    // 보류 규칙 때문에 room.RoomFaction을 그대로(Wild) 뒀는데, 나중에 두 번째 진영(예: 플레이어
    // 몬스터)까지 죽어서 실제로는 인류 단독이 됐어도 "죽은 진영(Player) != 방 소유(여전히 Wild)"로
    // 게이트에 걸려 전환 자체가 시도되지 않았다 — 문은 이 게이트가 없는 GameSession.
    // RefreshRoomGateStates 기준이라 정상적으로 열렸는데 점령만 안 따라간 이유. 이제 누가 죽었는지·
    // 방의 이전 소유가 무엇이었는지와 무관하게, "지금 이 순간 방에 실제로 살아있는 유닛 구성"만 보고
    // (문 열림 판정과 동일하게 room.ContainedUnits 기준) 매번 재계산한다. public — UnitFunction.
    // SyncRoomAffiliation(유닛이 방을 "떠날 때", 죽지 않고 그냥 나가서 단일 진영이 되는 경우)도
    // 문 열림 판정과 대칭으로 이 메서드를 직접 호출한다.
    public void TryResolveRoomOwnership(Room room, string triggerLabel)
    {
        if (room == null) return;

        bool hasHuman = false, hasPlayerMonster = false, hasWild = false;
        foreach (var u in room.ContainedUnits)
        {
            if (u == null || u.hp <= 0) continue;
            if (u.FactionBehavior is HumanFactionBehavior) hasHuman = true;
            else if (u.FactionBehavior is PlayerMonsterBehavior) hasPlayerMonster = true;
            else if (u.FactionBehavior is WildMonsterBehavior) hasWild = true;
        }

        // 점령 보류 규칙(2026-07-28, 사용자 요청 "야생 유닛 막타쳐서 점령했을때, 만약 인류, 몬스터
        // 같이 방에 존재한다면, 점령되지 않고, 한 진영만 남을때까지 점령되지 않게 해줘") — 야생이
        // 남아있거나, 인류·몬스터가 동시에 있거나(hasHuman==hasPlayerMonster==true) 둘 다 없으면
        // (hasHuman==hasPlayerMonster==false, 완전히 빈 방 — TryClaimEmptyRoomOnEntry 몫이라 여기서는
        // 손대지 않음) 아직 "한 진영"이 아니므로 보류.
        if (hasWild || hasHuman == hasPlayerMonster) return;

        FactionType claimant = hasHuman ? FactionType.Human : FactionType.Player;
        if (room.RoomFaction == claimant) return; // 이미 같은 소유

        _activeOffenseRooms.Remove(room); // 진행 중이던 폴링 기반 오펜스가 있었다면 정리
        ApplyRoomOwnership(room, claimant);

        LogHelper.Log($"[점령 전환] {room.RoomName} 방(F{room.Floor}): → {claimant} (유닛 구성 재계산, 계기: {triggerLabel} 사망)");
    }

    // 점령 시스템(2026-07-28, 사용자 요청 "빈 방에 그냥 입성시, 그 방은 입성한 진영이 점령하게 해줘")
    // — 전투(TryFlipRoomOwnershipOnDeath)나 야생 전멸(OnOffenseSuccess) 없이도, 완전히 비어있던 방에
    // 유닛이 그냥 걸어 들어오기만 하면 그 진영 소유가 된다. "입성 직전엔 방이 비어 있었다"는 판정은
    // 호출부(UnitFunction.SyncRoomAffiliation)가 이 유닛을 Room.ContainedUnits에 등록하기 전에 미리
    // 해 둔다 — 그렇지 않으면 입성한 유닛 자신이 이미 점유 중인 걸로 잡혀 항상 "비어있지 않음"이 된다.
    public void TryClaimEmptyRoomOnEntry(Room room, Unit enteringUnit)
    {
        if (room == null || enteringUnit == null || GameSession.Instance == null) return;

        FactionType? faction = MapToRoomFaction(enteringUnit.FactionBehavior);
        if (faction == null) return; // 방 소유권 개념이 없는 진영(FactionBehavior가 매핑 안 됨)
        if (room.RoomFaction == faction.Value) return; // 이미 같은 소유면 할 일 없음

        ApplyRoomOwnership(room, faction.Value);

        LogHelper.Log($"[점령] {room.RoomName} 방(F{room.Floor}): 빈 방에 {enteringUnit.unitType?.typeName}({faction.Value})이 입성해 점령했습니다.");
    }

    // 구조적 이슈 수정(2026-07-28) — FactionType(RoomFaction 쪽) → OccupationState(맵 데이터 쪽)
    // 역매핑. GetRoomOwnerColor와 나란히 두되 색이 아니라 CreateMap.Chunks.occupationState 갱신용.
    private static OccupationState MapToOccupationState(FactionType faction) => faction switch
    {
        FactionType.Player => OccupationState.PlayerControlled,
        FactionType.Human => OccupationState.HumanControlled,
        _ => OccupationState.Neutral,
    };

    private static FactionType? MapToRoomFaction(IFactionBehavior behavior)
    {
        if (behavior is PlayerMonsterBehavior) return FactionType.Player;
        if (behavior is WildMonsterBehavior) return FactionType.Wild;
        if (behavior is HumanFactionBehavior) return FactionType.Human; // 2026-07-27, 사용자 요청 "인류도 소유권 있어"
        return null;
    }

    // 점령 보류 규칙(2026-07-28) 전용 — 이 방에 인류와 플레이어 소속 몬스터가 동시에 살아있는지.
    // Room.ContainedUnits는 UnitFunction.SyncRoomAffiliation(인류/몬스터)이 실시간으로 채우는 목록
    // (야생은 대상 제외라 애초에 이 판정과 무관 — GameSession.RefreshRoomGateStates와 동일 데이터).
    private static bool HasBothHumanAndPlayerMonster(Room room)
    {
        bool hasHuman = false, hasPlayerMonster = false;
        foreach (var u in room.ContainedUnits)
        {
            if (u == null || u.hp <= 0) continue;
            if (u.FactionBehavior is HumanFactionBehavior) hasHuman = true;
            else if (u.FactionBehavior is PlayerMonsterBehavior) hasPlayerMonster = true;
            if (hasHuman && hasPlayerMonster) return true;
        }
        return false;
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

    // 방 소유권 전환 4줄(RoomFaction 대입 + 색칠 + CreateMap.Chunks.occupationState 동기화 + 플레이어
    // 점령 시 안개 해금) — TryResolveRoomOwnership/TryClaimEmptyRoomOnEntry/OnOffenseSuccess 3곳에서
    // 변수명만 다르게 그대로 반복되고 있던 것을 통합했다(2026-08-20). room.RoomFaction 대입 자체는
    // 호출부가 "이미 같은 소유면 return" 가드를 각자 먼저 거치므로 이 헬퍼 안에서는 다시 확인하지 않는다.
    private void ApplyRoomOwnership(Room room, FactionType faction)
    {
        room.RoomFaction = faction;
        _colorizer?.ChangeRoomColor(room, GetRoomOwnerColor(faction));
        // 구조적 이슈 수정(2026-07-28) — CreateMap.Chunks.occupationState(맵 데이터 원본)도 같이 갱신.
        GameSession.Instance?.cmap?.SetRoomOccupationState(room.Floor, room.RoomId, MapToOccupationState(faction));
        // 안개 해금 규칙 변경(2026-07-28) — 점령된 방과 인접 방의 안개를 함께 걷는다.
        if (faction == FactionType.Player) GameSession.Instance?.RevealFogAroundCapturedRoom(room);
    }

    private void OnOffenseSuccess(Room room)
    {
        // 건축물·자원·유닛 생산 MVP(2026-07-27) — 처치 보상은 이제 즉시 지급되므로(WildMonsterBehavior.
        // OnDeath) 오펜스 성공 시점의 별도 정산이 필요 없다.

        // 2. 방 소속 변경 — 2026-07-28 이전엔 무조건 Player로 고정했으나, 점령 보류 규칙 추가로
        // CheckOffenseSuccess의 Normal 타입 방은 이제 "인류·몬스터 중 하나만 남음"을 보장하므로 실제로
        // 남아있는 진영에게 점령권을 준다(인류 혼자 정리한 방이면 인류 소유가 맞다 — "인류도 소유권
        // 있어"와 일관). Spawner 타입(거점 파괴, 인원 구성과 무관하게 성공)처럼 판정이 모호할 수 있는
        // 경우에만 기존처럼 Player를 기본값으로 유지.
        FactionType claimant = HasBothHumanAndPlayerMonster(room) ? FactionType.Player : DetermineSoleOccupant(room);
        ApplyRoomOwnership(room, claimant);

        // 3. 활성 오펜스에서 제거
        _activeOffenseRooms.Remove(room);

        // 4. 오펜스 성공 로그 (MVP 8장)
        LogHelper.Log($"[오펜스 성공] {room.RoomName} 방 점령 완료 — {claimant} 진영으로 전환");
    }

    // OnOffenseSuccess 전용 — 방에 실제로 남아있는 단일 진영을 찾는다(인류 또는 플레이어 소속 몬스터).
    // 둘 다 없으면(예: Spawner 파괴만으로 성공해 아무도 안 남은 경우) 기존 기본값 Player로 폴백.
    private static FactionType DetermineSoleOccupant(Room room)
    {
        foreach (var u in room.ContainedUnits)
        {
            if (u == null || u.hp <= 0) continue;
            if (u.FactionBehavior is HumanFactionBehavior) return FactionType.Human;
            if (u.FactionBehavior is PlayerMonsterBehavior) return FactionType.Player;
        }
        return FactionType.Player;
    }
}
