using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Navigation FSM 상태 안에서 BT Selector 자식 순서를 결정하는 설정.
/// 에셋 경로: Resources/FSM+BT/NavigationPriority
///
/// 비주얼 스크립팅 전환 시:
///   이 리스트의 각 Entry → BT 그래프의 노드로 대응된다.
/// </summary>
[CreateAssetMenu(menuName = "GrimArchive/AI/NavigationBehaviorPriority", fileName = "NavigationPriority")]
public class NavigationBehaviorPriorityConfig : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public NavigationBehaviorType behavior;
        [Tooltip("false 이면 BT에서 완전히 제외됨")]
        public bool enabled = true;
    }

    [Tooltip("위에서 아래 순서로 우선순위 높음.")]
    public List<Entry> order = new List<Entry>
    {
        new Entry { behavior = NavigationBehaviorType.Stairs,       enabled = true },
        new Entry { behavior = NavigationBehaviorType.PlayerAttack, enabled = true },
        new Entry { behavior = NavigationBehaviorType.PlayerMove,   enabled = true },
        new Entry { behavior = NavigationBehaviorType.Explore,      enabled = true },
    };

    public bool IsEnabled(NavigationBehaviorType type)
    {
        foreach (var e in order)
            if (e.behavior == type) return e.enabled;
        return true;
    }
}

/// <summary>
/// Navigation BT Selector 자식 노드 종류.
/// </summary>
public enum NavigationBehaviorType
{
    Stairs,        // 계단 이동 (pendingStairTargetFloor 세팅 시)
    PlayerAttack,  // 플레이어 공격 명령
    PlayerMove,    // 플레이어 이동 명령
    Explore,       // 자유 탐색 (미탐색 타일 접근 / 무작위 이동)
}
