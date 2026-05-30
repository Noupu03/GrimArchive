using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 인스펙터에서 유닛 타입별 대치 거리를 조정하는 컴포넌트.
/// 빈 GameObject에 붙이고 에디터의 "유닛 타입 자동 감지" 버튼으로 목록을 채운다.
/// </summary>
public class EngageDistanceTuner : MonoBehaviour
{
    [System.Serializable]
    public class EngageDistanceEntry
    {
        public string unitTypeName;
        [Tooltip("적과 유지할 대치 거리 (타일 수, 체비쇼프 거리)")]
        [Min(1)] public int engageDistance = 2;
    }

    public List<EngageDistanceEntry> entries = new();

    void Awake() => Apply();

    public void Apply()
    {
        EngageDistanceOverride.Clear();
        foreach (var e in entries)
            EngageDistanceOverride.Register(e.unitTypeName, e.engageDistance);
    }
}
