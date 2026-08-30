using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tactical FSM 상태 안에서 BT Selector 자식 순서를 결정하는 설정. 에셋 경로: Resources/FSM+BT/TacticalPriority
///
/// 03문서 2-1장 기본 우선순위: 함정 대응 > 경계 > 조사 > 대기(공황은 항상 최우선). 포메이션은 파티
/// 보호 목적이라 문서 우선순위 외 별도 판단으로 최하위 배치.
///
/// 비주얼 스크립팅 전환 시: 이 리스트의 각 Entry → BT 그래프의 노드(TacticalBehaviorType이 타입
/// 식별자), 순서는 Selector 자식 연결 순서, enabled는 노드 활성화 여부로 대응된다.
/// </summary>
[CreateAssetMenu(menuName = "GrimArchive/AI/TacticalBehaviorPriority", fileName = "TacticalPriority")]
public class TacticalBehaviorPriorityConfig : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("행동 종류")]
        public TacticalBehaviorType behavior;
        [Tooltip("false 이면 BT에서 완전히 제외됨 (해당 행동이 절대 수행되지 않음)")]
        public bool enabled = true;
    }

    [Tooltip("위에서 아래 순서로 우선순위 높음. 03문서 2-1장 기준 기본값 적용.")]
    public List<Entry> order = new List<Entry>
    {
        new Entry { behavior = TacticalBehaviorType.Panic,          enabled = true },
        new Entry { behavior = TacticalBehaviorType.JoinCombatWait, enabled = true },
        new Entry { behavior = TacticalBehaviorType.TrapResponse, enabled = true },
        new Entry { behavior = TacticalBehaviorType.Alert,        enabled = true },
        new Entry { behavior = TacticalBehaviorType.Investigate,  enabled = true },
        new Entry { behavior = TacticalBehaviorType.Wait,         enabled = true },
        new Entry { behavior = TacticalBehaviorType.Formation,    enabled = true },
        new Entry { behavior = TacticalBehaviorType.CoreAttack,   enabled = true },
        new Entry { behavior = TacticalBehaviorType.DoorAttack,   enabled = true },
    };

    public bool IsEnabled(TacticalBehaviorType type)
    {
        foreach (var e in order)
            if (e.behavior == type) return e.enabled;
        return true;
    }
}

/// <summary>
/// Tactical BT Selector 자식 노드 종류.
/// 비주얼 스크립팅 전환 시 이 enum 값이 BTNodeSO 파생 클래스의 타입 식별자가 된다.
/// </summary>
public enum TacticalBehaviorType
{
    Panic,         // 공황 — 정신력 panicMentalRatio 이하
    // 07문서 7장/07-A 9장(2026-07-31 신규): 전투 진입 시 합류 대기 — 위험도 2단계+비근거리 정확 인지 적.
    // 소리·함정 대응보다 먼저 확인해야 "정확 인지 적에 대한 전투 합류 대기: 현재 적 대상 행동 우선"
    // (07문서 16-4장 표)이 성립한다.
    JoinCombatWait,
    TrapResponse,  // 함정 대응 (Disarm → Bypass → Pass → Destroy 내부 순서 고정)
    Alert,         // 경계 (수상한 타일 접근 / 전투 후 주변 수색)
    Investigate,   // 조사 오브젝트 상호작용
    Wait,          // 지정 위치/목적 대기
    Formation,     // 보호 포메이션 유지
    // 자기 진영 소유가 아닌 방의 코어 공격 — 최후순위, 인류 전용. ⚠️ 플레이어 몬스터는 절대 스스로
    // 코어/문을 공격하지 않는다 — 반드시 PlayerCommandFSMState.ExecutePlayerAttackObject(플레이어
    // 명령)로만 가능하다.
    CoreAttack,
    // 인류 전용 — 이미 점령한 방의 경계 문 중 아직 다른 진영 소유인 문을 부순다. CoreAttack과
    // 동급 최후순위(자리표시자).
    DoorAttack,
}
