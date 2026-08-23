using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Haare.Util.Logger;
using UnityEngine;

public enum ResourceType
{
    Wood,
    Stone
}

public class ResourceManager : NativeRoutine
{
    public static ResourceManager Instance { get; private set; }

    // 건축물·자원·유닛 생산 MVP(2026-07-27) — P키(함정 배치) 1회당 소모 자원. InputManager와
    // UI(StatusInfoPanel의 자원 사용 안내)가 같은 값을 참조하도록 여기 한 곳에만 정의한다.
    public const int TrapPlaceStoneCost = 100;

    // 문도 방어건물화(기초문서.md 피드백, 2026-08-22) — 문 재설치 1회당 소모 자원. 자리표시자
    // (플레이 테스트 후 조정) — 함정과 동일한 급으로 우선 맞춰뒀다. 2026-08-22 사용자 요청
    // "debug에 있던 문 설치를 '설치'란에 넣고, 자원을 소모해서 설치하게 다시 바꿔줘" — 원래는 무료였다.
    public const int DoorRepairStoneCost = 100;

    // 건축물·자원·유닛 생산 MVP(2026-07-27, 사용자 확정 수치) — B/V키 건물 설치비와 유닛 생산비.
    public const int UnitProductionWoodCost = 100;
    public const int ResourceBuildingStoneCost = 300;
    public const int UnitBuildingStoneCost = 200;

    // 처치 보상(2026-07-27, 문서에 수치 미명시 — "시간기반 자동증가보다 훨씬 커야 한다"는 사용자
    // 기준만 있어 판단 근거를 남기고 상수로 뺌) — BuildingManager의 자원 건물 틱(5초당 +5)의 6배.
    public const int KillRewardWood = 30;
    public const int KillRewardStone = 30;

    private Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();

    public override async UniTask Initialize(CancellationToken cts)
    {
        await base.Initialize(cts);
        Instance = this;

        resources.Clear();
        // 초기 보유량(2026-07-27, 사용자 확정 수치 200 → 2026-07-28 사용자 요청 "초기 자원값 200씩
        // 더 늘려줘"로 Wood/Stone 각 400으로 조정. Gold는 소모처가 없는 미사용 자원이라 2026-07-28
        // 사용자 요청으로 완전히 제거(enum 자체에서 삭제, 아래 ShowKillRewardText/OffenseDebugWindow/
        // StatusInfoPanel의 관련 참조도 함께 정리). 시간 경과 자동 증가는 더 이상 "고정값" 원칙이
        // 아니라 V키 자원 생산 건물이 있을 때만 발생한다(BuildingManager 참고) — 문서 5장의 "건축물을
        // 통한 임시 자원 확보" 요구사항을 그 건물에 연결한 것.
        resources[ResourceType.Wood] = 400;
        resources[ResourceType.Stone] = 400;

        LogHelper.Log(LogHelper.GAME, "ResourceManager Initialized");
    }

    public override async UniTask Finalize()
    {
        resources.Clear();
        LogHelper.Log(LogHelper.GAME, "ResourceManager Disposed");
        await base.Finalize();
    }

    public void AddResource(ResourceType type, int amount)
    {
        if (amount <= 0) return;
        
        if (!resources.ContainsKey(type))
            resources[type] = 0;
            
        resources[type] += amount;
        LogHelper.Log(LogHelper.GAME, $"Added {amount} {type}. Total: {resources[type]}");
    }

    // 8단계: 검사와 차감을 원자적으로 처리 (동시성 방지)
    public bool TryConsumeResource(ResourceType type, int amount)
    {
        if (amount < 0) return false;
        if (amount == 0) return true;

        if (resources.TryGetValue(type, out int current) && current >= amount)
        {
            resources[type] -= amount;
            LogHelper.Log(LogHelper.GAME, $"Consumed {amount} {type}. Remaining: {resources[type]}");
            return true;
        }

        LogHelper.Warning(LogHelper.GAME, $"Not enough {type} to consume {amount}. Current: {(resources.ContainsKey(type) ? resources[type] : 0)}");
        // 자원 부족은 플레이어 행동(건물/함정 설치, 유닛 생산 등)이 실패하는 원인이라 단일 지점에서
        // notice로도 알린다(2026-08-20, 사용자 요청 "게임에 영향을 주는 실패로그들 notice로 뜨게") —
        // 호출부마다 따로 notice를 띄우면 중복되므로 여기 한 곳으로 모은다.
        NoticeCenter.Instance?.PushMomentary($"{type} 자원이 부족합니다. (필요: {amount})", NoticeCenter.WarningColor);
        return false;
    }

    public bool TryConsumeResources(List<ResourceCost> costs)
    {
        // 1. 전체 검사 (Atomic)
        foreach (var cost in costs)
        {
            if (!HasEnoughResource(cost.resourceType, cost.amount))
            {
                LogHelper.Warning(LogHelper.GAME, $"Not enough {cost.resourceType} to consume {cost.amount}.");
                NoticeCenter.Instance?.PushMomentary($"{cost.resourceType} 자원이 부족합니다. (필요: {cost.amount})", NoticeCenter.WarningColor);
                return false;
            }
        }

        // 2. 전체 차감
        foreach (var cost in costs)
        {
            resources[cost.resourceType] -= cost.amount;
            LogHelper.Log(LogHelper.GAME, $"Consumed {cost.amount} {cost.resourceType}. Remaining: {resources[cost.resourceType]}");
        }
        return true;
    }

    public bool HasEnoughResource(ResourceType type, int amount)
    {
        return resources.TryGetValue(type, out int current) && current >= amount;
    }

    public int GetResourceAmount(ResourceType type)
    {
        return resources.TryGetValue(type, out int current) ? current : 0;
    }

    // 처치 보상 MVP(2026-07-27, 사용자 요청) — 킬로 자원을 얻을 때 "돌 30개 획득!" 형태의 floating text를
    // 띄운다. UIManager.ShowFloatingTextAt(기존 함정 해제 성공/실패 문구가 쓰는 것과 동일한 메서드)을
    // 재사용한다 — 그 메서드는 floor offset을 계산하지 않으므로(unit.position 기준 화면 좌표만 반환하는
    // ShowFloatingText(Unit)와 달리 임의 월드좌표를 받음) 여기서 직접 GetFloorOffset을 더해 1층 이외의
    // 층에서도 정확한 위치에 뜨도록 한다.
    public static void ShowKillRewardText(Unit killer, ResourceType type, int amount)
    {
        if (killer == null || killer.UI == null) return;

        string label = type switch
        {
            ResourceType.Wood => "나무",
            ResourceType.Stone => "돌",
            _ => type.ToString()
        };

        Vector3 pos = new Vector3(killer.position.x + 0.5f, killer.position.y + 1.2f, 0f);
        if (killer.Generate != null) pos += killer.Generate.GetFloorOffset(killer.currentFloor);

        killer.UI?.ShowFloatingTextAt(pos, $"{label} {amount}개 획득!", Color.yellow, 0.8f);
    }
}
