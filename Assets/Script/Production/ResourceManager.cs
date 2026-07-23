using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Haare.Util.Logger;

public enum ResourceType
{
    Wood,
    Stone,
    Gold,
    DefenseReward, // 기획문서 상 자원 A (디펜스 보상)
    OffenseReward  // 기획문서 상 자원 B (오펜스 보상)
}

public class ResourceManager : NativeRoutine
{
    public static ResourceManager Instance { get; private set; }

    // M키(몬스터 배치)/P키(함정 배치) 1회당 소모 자원 — InputManager와 UI(StatusInfoPanel의 자원 사용
    // 안내)가 같은 값을 참조하도록 여기 한 곳에만 정의한다(고정값, 2026-07-23 사용자 요청 "적절히
    // 분배해줘" — 시작 보유량 100/100 기준으로 초반에 여러 번 쓸 수 있게 잡음).
    public const int MonsterPlaceWoodCost = 15;
    public const int TrapPlaceStoneCost = 10;

    private Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();

    public override async UniTask Initialize(CancellationToken cts)
    {
        await base.Initialize(cts);
        Instance = this;

        resources.Clear();
        // 초기 자원 세팅 — 시간이 지나도 자동으로 늘어나지 않는 고정값(사용자 요청, 2026-07-23:
        // "나무와 돌은 시간이 지날수록 생성되는게 아니라 일단 고정값으로 둬줘"). 예전엔 5초마다
        // 나무+10/돌+5가 자동으로 채굴되는 더미 채굴기가 있었으나 제거했다 — M/P키 소모(TryConsumeResource)
        // 로만 줄어들고, 늘어나는 경로는 지금 없다.
        // 나무는 늘리고 돌은 줄임(사용자 요청, 2026-07-23) — 나무 150이면 몬스터(M, 15개/회) 10회,
        // 돌 60이면 함정(P, 10개/회) 6회 배치 가능.
        resources[ResourceType.Wood] = 150;
        resources[ResourceType.Stone] = 60;
        resources[ResourceType.Gold] = 50;

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
}
