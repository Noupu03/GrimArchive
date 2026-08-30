using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class InteractableObject
{
    public string Id;
    public Vector3Int Position;
    public float BaseInterest;
    // 타일 최종 위험도 = 기본 탐사 위험도 + 오브젝트 위험도. 순수 루팅 대상은 기본값 0.
    public float BaseDanger;
    // 대상 기본 가시성 — 오브젝트가 타일의 가시성(일반 100/벽 0)을 무효화하고 대신 갖는 자기 값. 기본값 0.
    public float BaseVisibility = 0f;
    // "완전 차단 오브젝트" — 벽과 동일하게 시야(레이)를 물리적으로 막는 구조물 성격인지.
    // BaseVisibility(그 대상 자신이 인지되는지)와는 완전히 별개 속성이다 — 가시성이 낮아도 완전
    // 차단이 아니면 시야를 막지 않고, 반대로 가시성이 높아도 완전 차단이면 막는다.
    public bool IsFullyBlocking = false;
    public bool IsCollected;
    // 조사(investigate) 완료 여부 — Loot 오브젝트에만 의미가 있다(조사 50%감소 → 회수 0%감소 2단계).
    // Corpse/WipeoutTrace는 확인 즉시 흥미도 0으로 가는 단일 단계라 이 필드를 안 쓴다.
    public bool IsInvestigated;

    // 시체/전리품 등을 구분하기 위한 태그
    public List<string> Tags = new List<string>();
    // 시체(흔적)인 경우, 원인 제공자의 위험도 단계
    public DangerStage CauserStage;
    // WipeoutTrace 태그일 때만 사용 — HumanKnowledgeBase.RegisterWipeoutTrace()가 발급한 흔적 ID.
    // 생환 파티가 발견하면 OnWipeoutTraceReflected(TraceId)로 동일 ID당 1회만 던전 위험도에 반영한다.
    public string TraceId;

    // 인류 시체(Tags에 "Human" 포함)일 때만 사용 — 이 시체를 남긴 파티의 Party.Id. 어느 유닛이든
    // 이 시체를 처음 정확 인지하면 PartyDeathSystem이 이 값으로 원본 파티를 찾아 갱신한다.
    public string OwnerPartyId;

    // E_MONSTER_KILL_INDIRECT 연결용 — 몬스터 시체(Tags에 "Monster" 포함)일 때만 사용. 몬스터는
    // Destroy된 뒤라 Unit 참조로 종/개체 키를 다시 조회할 수 없으므로 RemoveDeadUnit이 Destroy 전에
    // 스냅샷해 둔다. MonsterKilledByHuman이 false면(야생 개체끼리 등) 간접 확인 대상이 아니다.
    public bool MonsterKilledByHuman;
    public string MonsterSpeciesKey;
    public bool MonsterIsSpecialUnit;
    public string MonsterIndividualKey;

    // 캐릭터별 시체 스프라이트 — Corpse 태그일 때만 의미가 있다. 죽은 유닛의 corpseSprite를 Destroy
    // 전에 스냅샷해 둔다. null이면 GameSession.SpawnObject가 공용 시체 스프라이트로 폴백한다.
    public Sprite CorpseSpriteOverride;

    // 시체는 스폰 웨이브 + 2가 되는 시점에 정리된다(시간이 아닌 웨이브 카운트 기준, 현재 웨이브
    // 포함) — 스폰 시점의 WaveNumber를 스냅샷해두면 DespawnCorpsesForNewWave가 새 웨이브마다 정리한다.
    public int SpawnWaveNumber;

    // Trap 태그일 때만 의미가 있다(그 외 태그는 전부 0) — Action_TrapDestroy가 매 틱 TrapHp를 깎는다.
    public float TrapHp;
    public float TrapMaxHp;
    // "예상 피해 오차 범위" — 통과 판정은 항상 TrapDamageMax(최대 예상 피해)를 쓴다.
    public float TrapDamageMin;
    public float TrapDamageMax;

    // 코어 전면 개편: RoomCoreTag가 붙은 오브젝트에만 의미가 있다. 체력이 0이 되면 막타친 유닛의
    // 진영으로 방 소유권이 전환되고(OffenseProcessor.OnCoreDestroyed), 코어 자체는 사라지지 않고
    // 즉시 CoreMaxHp의 절반으로 회복된다.
    public float CoreHp;
    public float CoreMaxHp;

    // 문도 방어건물화: DoorTag가 붙은 오브젝트에만 의미가 있다. 체력이 0이 되면 DoorSystem.RemoveDoor가
    // 오브젝트를 제거해 통로가 뚫리고, 이후 배치 모드로만(원래 게이트 타일 자리에 한해) 재설치할 수 있다.
    public float DoorHp;
    public float DoorMaxHp;

    // 코어/문 자동 회복 — 마지막으로 채널링 데미지를 받은 뒤 흐른 시간. 데미지 적용 시 0으로
    // 리셋되고, 공격이 없는 동안 누적되다 회복 지연시간을 넘기면 회복을 시작한다. 기본값
    // float.MaxValue = "데미지 이력 없음"(회복 대상 아님).
    public float TimeSinceLastDamaged = float.MaxValue;

    // DoorSystem.UpdateProcess가 매 프레임 계산해 캐싱하는 현재 시각적 개폐 상태(스프라이트/
    // IsFullyBlocking 중복 재적용 방지용) — 실제 통행 가능 여부(진영 일치)와는 별개로,
    // 그쪽은 DoorSystem.IsBlockedByClosedDoor가 상시 판정하므로 이 값에 의존하지 않는다.
    public bool DoorIsOpenVisual;

    // 문 소유 진영은 방 소유권과 분리되어 생성/재설치되는 순간에만 고정되고 이후 방 점령과 무관하게
    // 유지된다. 최초 스폰 시엔 그 시점 방 소유 진영을 스냅샷(SpawnDoors), 파괴 후 재설치 시엔
    // 재설치한 플레이어 진영으로 고정(RebuildDoorAt).
    public FactionType DoorOwnerFaction = FactionType.Wild;

    public InteractableObject(string id, Vector3Int position, float baseInterest, float baseDanger = 0f, List<string> tags = null, DangerStage causerStage = DangerStage.Stage0, string traceId = null, float baseVisibility = 0f, bool isFullyBlocking = false, float trapHp = 0f, float trapDamageMin = 0f, float trapDamageMax = 0f, float coreHp = 0f, float doorHp = 0f)
    {
        Id = id;
        Position = position;
        BaseInterest = baseInterest;
        BaseDanger = baseDanger;
        BaseVisibility = baseVisibility;
        IsFullyBlocking = isFullyBlocking;
        IsCollected = false;
        TrapHp = trapHp;
        TrapMaxHp = trapHp;
        TrapDamageMin = trapDamageMin;
        TrapDamageMax = trapDamageMax;
        CoreHp = coreHp;
        CoreMaxHp = coreHp;
        DoorHp = doorHp;
        DoorMaxHp = doorHp;

        if (tags != null)
        {
            Tags = new List<string>(tags);
        }
        else
        {
            // 기본값은 지나갈 수 있는 전리품("Object/Passable/Loot")으로 처리
            Tags = new List<string> { "Object/Passable/Loot" };
        }

        CauserStage = causerStage;
        TraceId = traceId;
    }
}
