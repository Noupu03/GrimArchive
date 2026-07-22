using UnityEngine;

public class ResourceAccumulator
{
    private static ResourceAccumulator _instance;
    public static ResourceAccumulator Instance
    {
        get
        {
            if (_instance == null) _instance = new ResourceAccumulator();
            return _instance;
        }
    }

    private int _accumulatedResourceB = 0;
    public int AccumulatedResourceB => _accumulatedResourceB;

    public void AccumulateResourceB(int amount)
    {
        _accumulatedResourceB += amount;
        Debug.Log($"[Resource] 자원 B 임시 누적: +{amount} (총 누적: {_accumulatedResourceB})");
    }

    // 오펜스 성공 시 호출되어 누적된 자원 B를 실제 플레이어 자원에 더함
    public void CommitResourceB()
    {
        if (_accumulatedResourceB > 0)
        {
            Debug.Log($"[Resource] 자원 B 일괄 지급: {_accumulatedResourceB}");
            
            // 실제 자원 매니저(ResourceManager)에 추가
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.AddResource(ResourceType.OffenseReward, _accumulatedResourceB);
            }
            
            _accumulatedResourceB = 0;
        }
    }

    public void ClearResourceB()
    {
        _accumulatedResourceB = 0;
        Debug.Log("[Resource] 자원 B 임시 누적분이 초기화되었습니다 (패배).");
    }
}
