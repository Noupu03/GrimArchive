using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
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

    private Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();
    private CancellationTokenSource minerCts;

    public override async UniTask Initialize(CancellationToken cts)
    {
        await base.Initialize(cts);
        Instance = this;
        
        resources.Clear();
        // 초기 자원 세팅 (더미)
        resources[ResourceType.Wood] = 100;
        resources[ResourceType.Stone] = 100;
        resources[ResourceType.Gold] = 50;

        LogHelper.Log(LogHelper.GAME, "ResourceManager Initialized");

        // 3단계: 임시 자원 채굴기 가동 (UniTask 기반)
        minerCts = CancellationTokenSource.CreateLinkedTokenSource(cts);
        StartDummyMining(minerCts.Token).Forget();
    }

    public override async UniTask Finalize()
    {
        if (minerCts != null)
        {
            minerCts.Cancel();
            minerCts.Dispose();
            minerCts = null;
        }
        resources.Clear();
        LogHelper.Log(LogHelper.GAME, "ResourceManager Disposed");
        await base.Finalize();
    }

    private async UniTaskVoid StartDummyMining(CancellationToken ct)
    {
        LogHelper.Log(LogHelper.GAME, "Dummy Mining Started");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(5000, cancellationToken: ct); // 5초마다
                AddResource(ResourceType.Wood, 10);
                AddResource(ResourceType.Stone, 5);
            }
        }
        catch (System.OperationCanceledException)
        {
            LogHelper.Log(LogHelper.GAME, "Dummy Mining Stopped");
        }
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
