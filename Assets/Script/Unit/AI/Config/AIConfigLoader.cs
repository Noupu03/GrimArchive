using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// AI 설정 에셋(Addressables 주소 "FSM+BT/...", 파일은 Data/AI/)에 대한 정적 접근자.
/// 매 틱 AI 판단이 동기로 읽으므로 GameSession.Initialize가 유닛을 스폰하기 전에 LoadAsync를 await한다.
/// 에셋이 없으면(또는 로드 전이면) null을 반환하고, 각 FSMState는 null 시 내부 기본값으로 폴백한다.
/// 비주얼 스크립팅 전환 시 BTGraphAsset 로더로 교체되고, FSMState들은 BTNode 트리를 직접 받게 된다.
/// </summary>
public static class AIConfigLoader
{
    private static AIBehaviorConfig                _behavior;
    private static TacticalBehaviorPriorityConfig  _tactical;

    public static AIBehaviorConfig Behavior => _behavior;

    public static TacticalBehaviorPriorityConfig TacticalPriority => _tactical;

    // NavigationPriority(NavigationBehaviorPriorityConfig)는 삭제됨 — NavigationFSMState의 BT 트리는
    // 생성자에 하드코딩돼 있어 이 설정을 애초에 읽지 않는 죽은 설정이었다.

    // 설정 에셋은 선택 사항이라(Tools(new)/유닛/FSM+BT 설정 에셋 생성을 안 돌렸으면 없음) 없어도 경고하지 않는다.
    public static async UniTask LoadAsync()
    {
        (_behavior, _tactical) = await UniTask.WhenAll(
            GameAssets.LoadAsync<AIBehaviorConfig>(AssetKeys.AIBehaviorConfig, warnIfMissing: false),
            GameAssets.LoadAsync<TacticalBehaviorPriorityConfig>(AssetKeys.TacticalPriority, warnIfMissing: false));
    }

    // 도메인 리로드(Play 시작) 시 캐시 초기화 — ScriptableObject 인스턴스가 재생성되므로 필요
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        _behavior   = null;
        _tactical   = null;
    }
}
