using UnityEngine;

/// <summary>
/// Resources/FSM+BT/ 아래 AI 설정 에셋을 런타임에 불러오는 정적 접근자.
/// 에셋이 없으면 null을 반환하고, 각 FSMState는 null 시 내부 기본값으로 폴백한다.
/// 비주얼 스크립팅 전환 시 BTGraphAsset 로더로 교체되고, FSMState들은 BTNode 트리를 직접 받게 된다.
/// </summary>
public static class AIConfigLoader
{
    private const string BasePath = "FSM+BT/";

    private static AIBehaviorConfig                _behavior;
    private static TacticalBehaviorPriorityConfig  _tactical;

    public static AIBehaviorConfig Behavior
        => _behavior  != null ? _behavior
           : (_behavior  = Resources.Load<AIBehaviorConfig>(BasePath + "AIBehaviorConfig"));

    public static TacticalBehaviorPriorityConfig TacticalPriority
        => _tactical  != null ? _tactical
           : (_tactical  = Resources.Load<TacticalBehaviorPriorityConfig>(BasePath + "TacticalPriority"));

    // NavigationPriority(NavigationBehaviorPriorityConfig)는 삭제됨 — NavigationFSMState의 BT 트리는
    // 생성자에 하드코딩돼 있어 이 설정을 애초에 읽지 않는 죽은 설정이었다.

    // 도메인 리로드(Play 시작) 시 캐시 초기화 — ScriptableObject 인스턴스가 재생성되므로 필요
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        _behavior   = null;
        _tactical   = null;
    }
}
