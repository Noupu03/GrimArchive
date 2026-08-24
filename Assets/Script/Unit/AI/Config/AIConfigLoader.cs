using UnityEngine;

/// <summary>
/// Resources/FSM+BT/ 아래 AI 설정 에셋을 런타임에 불러오는 정적 접근자.
/// 에셋이 없으면 null을 반환하고, 각 FSMState는 null 시 내부 기본값으로 폴백한다.
///
/// 비주얼 스크립팅 전환 시:
///   이 클래스가 BTGraphAsset 로더로 교체된다.
///   FSMState들이 LoadedGraph(CombatFSMState) 등으로 BTNode 트리를 직접 받는 방식으로 전환.
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

    // NavigationPriority(NavigationBehaviorPriorityConfig) 삭제됨(2026-08-24 FSM 리팩토링 감사) —
    // NavigationFSMState의 BT 트리는 생성자에 하드코딩돼 있어 이 설정을 애초에 읽지 않았고, 이 설정의
    // NavigationBehaviorType엔 2026-07-24에 이미 PlayerCommandFSMState로 분리되며 제거된 PlayerAttack/
    // PlayerMove 잔재까지 남아있어 죽은 설정이었다. 실제 에셋 인스턴스도 생성된 적이 없었다(사용자
    // 확인 후 삭제).

    // 도메인 리로드(Play 시작) 시 캐시 초기화 — ScriptableObject 인스턴스가 재생성되므로 필요
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        _behavior   = null;
        _tactical   = null;
    }
}
