using UnityEngine;

/// <summary>
/// 인류 AI FSM+BT 행동 수치 파라미터. 각 항목의 문서 근거는 Tooltip에 명시하며, 문서가 수치를 안
/// 주는 항목은 "내부 판단"으로 구분한다. 비주얼 스크립팅 전환 시 이 파일은 BTGraphAsset 로더로 대체될 예정.
/// </summary>
[CreateAssetMenu(menuName = "GrimArchive/AI/AIBehaviorConfig", fileName = "AIBehaviorConfig")]
public class AIBehaviorConfig : ScriptableObject
{
    // ── FSM 우선순위 ────────────────────────────────────────────────────────
    [Header("FSM 상태 진입 우선순위")]
    [Tooltip("플레이어 수동 명령(공격/이동) 활성 시 우선순위 — 항상 최우선(기본 200)")]
    public float playerCommandPriority = 200f;
    [Tooltip("이동 명령 혼잡 대기 최대 턴 수 — 이 턴을 초과하면 명령 포기 + 이동 불가 피드백 (내부 판단)")]
    public int playerCommandStuckTurnLimit = 8;
    [Tooltip("코어/문 자동 파괴 접근 혼잡 대기 최대 턴 수 — 초과 시 파괴를 포기하고 경계 상태로 전환 (내부 판단)")]
    public int tacticalObjectAttackStuckTurnLimit = 4;
    [Tooltip("일반 조사 대상 접근 혼잡 대기 최대 턴 수 — 초과 시 그 대상을 포기하고 경계 상태로 전환 (검증문서 01-11 6행, 내부 판단)")]
    public int investigateStuckTurnLimit = 4;
    [Tooltip("함정 접근 이동(해제·통과·파괴 공용) 혼잡 대기 최대 턴 수 — 초과 시 함정 대응을 포기하고 경계 상태로 전환 (검증문서 03-11, 내부 판단)")]
    public int trapMoveStuckTurnLimit = 4;
    [Tooltip("자유탐색(배회) 중 다음 걸음이 아군에게 막힌 혼잡 대기 최대 턴 수 — 초과 시 탐색 목표를 포기하고 경계 상태로 전환 (플레이테스트 발견, 내부 판단)")]
    public int exploreStuckTurnLimit = 4;
    [Tooltip("집결 대기·코어 보고 이동 중 혼잡 대기 최대 턴 수 — 초과 시 대기를 포기(집결은 '도착'과 동일하게 처리) (플레이테스트 발견, 내부 판단)")]
    public int waitStuckTurnLimit = 4;
    [Tooltip("코어 보고 이동 목적지에 접근하지 못해 그 종류를 일시 제외한 뒤 다시 후보로 고려하기까지의 시간(초) — 다른 후보로 넘어가고 이 시간 뒤 재시도 (검증문서 03-15, 내부 판단)")]
    public float coreReportBlockedRetrySeconds = 5f;
    [Tooltip("다음 문을 모를 때 파티원이 아는 리더 위치로부터 유지하는 거리(체비셰프 칸) — 05번 1장 '진형을 유지하며 문을 찾는다'의 근사, 진형 자리·간격은 문서가 차후로 미룸 (내부 판단)")]
    public int doorSearchFollowRadius = 3;
    [Tooltip("공동 탐색 추종이 막히거나 리더 위치를 잃어 접은 뒤 다시 추종을 배정받기까지의 시간(초) — 배정·해제 반복 방지 (내부 판단)")]
    public float doorSearchFollowRetrySeconds = 5f;
    [Tooltip("전파받은 적 위치 정보가 이 시간(초)을 넘으면 낡은 정보로 보고 접근하지 않는다 (03번 v0.6 4-7은 유효 기간을 정하지 않음, 내부 판단)")]
    public float indirectEnemyInfoMaxAgeSeconds = 30f;
    [Tooltip("전파받은 적 위치로 접근하다 거리가 줄지 않는 혼잡 대기 최대 턴 수 — 초과 시 접근을 포기하고 그 정보를 버린다 (내부 판단)")]
    public int indirectEnemyApproachStuckTurns = 4;
    [Tooltip("전투 추격 중 대상까지 거리가 줄지 않는 혼잡 대기 최대 턴 수 — 초과 시 그 대상을 일시 배제 (검증문서 02-02, 내부 판단)")]
    public int combatChaseStuckTurnLimit = 4;
    [Tooltip("전투 추격 포기 후 같은 대상을 다시 후보로 고려하기까지의 최소 대기 시간(초) — 문 파괴 등으로 상황이 바뀔 시간을 준다 (검증문서 02-02, 내부 판단)")]
    public float combatChaseUnreachableCooldownSeconds = 6f;
    [Tooltip("적 인지 시 전투 상태 우선순위 (기본 100)")]
    public float combatPriority     = 100f;
    [Tooltip("전술 조건 충족 시 우선순위 (기본 50)")]
    public float tacticalPriority   = 50f;
    [Tooltip("항상 활성, 기본 탐색 우선순위 (기본 10)")]
    public float navigationPriority = 10f;
    [Tooltip("대기(IdleFSMState) 우선순위 — Tactical(50)과 Navigation(10) 사이. 오펜스/디펜스 중이 아닌 방에서 명령 없는 플레이어 몬스터/야생 몬스터가 이 상태로 들어간다 (내부 판단)")]
    public float idlePriority = 20f;

    // ── 대기 (IdleFSMState) ────────────────────────────────────────────────
    [Header("대기 (IdleFSMState, 내부 판단 — 문서 미명시)")]
    [Tooltip("1칸 이동 후 정지하는 최소 시간(초)")]
    public float idlePauseMinSeconds = 2f;
    [Tooltip("1칸 이동 후 정지하는 최대 시간(초)")]
    public float idlePauseMaxSeconds = 4f;
    [Tooltip("배회 기준점(플레이어 몬스터: 디펜스 시작 위치, 야생: 소환 위치)으로부터 벗어날 수 있는 최대 반경(칸, 체비쇼프 거리)")]
    public int idleWanderRadius = 2;

    // ── 공황 ────────────────────────────────────────────────────────────────
    [Header("공황 (내부 판단 — 문서 미명시)")]
    [Range(0f, 1f), Tooltip("정신력이 최대 정신력의 이 비율 이하면 공황 진입")]
    public float panicMentalRatio = 0.3f;

    // ── 전투 ────────────────────────────────────────────────────────────────
    [Header("전투 (CombatFSMState)")]
    [Tooltip("HitRange가 이 값 이상이면 원거리 유닛으로 판정 → 키팅 활성화 (내부 판단)")]
    public int rangedSkillMinRange = 4;

    // ── 경계 ────────────────────────────────────────────────────────────────
    [Header("경계 (03문서 4장)")]
    [Tooltip("4-3장: 경계 이동속도 = 일반 이동속도 × 이 값")]
    public float alertMoveSpeedRatio           = 0.75f;
    [Tooltip("4-2장: 경계 반응속도 = 기본 반응속도 × 이 값")]
    public float alertReactionSpeedRatio        = 1.2f;
    [Tooltip("4-8장: 전투 종료 후 경계 지속 시간 (초)")]
    public float postCombatAlertSeconds         = 10f;
    [Tooltip("4-12장: 미식별 공격 수색 지속 시간 (초)")]
    public float unidentifiedAttackSearchSeconds = 15f;
    [Tooltip("4-6장: 수상한 타일 대상 이동 1회당 임시 가시성 증가량")]
    public float suspiciousVisibilityBoostPerMove = 20f;

    // ── 조사 ────────────────────────────────────────────────────────────────
    [Header("조사 (03문서 5장)")]
    [Tooltip("5-4장: 조사 중 시야·인지·반응속도 감소 비율")]
    public float investigatePenaltyRatio       = 0.5f;
    [Tooltip("5-6장: 조사 중단 시 진행량 손실 비율")]
    public float investigateInterruptLossRatio = 0.5f;
    [Tooltip("조사 기준 소요시간 (초) — 문서 미명시, 내부 판단값")]
    public float investigateDurationSeconds    = 8f;

    // ── 함정 대응 ───────────────────────────────────────────────────────────
    [Header("함정 대응 (03문서 9장)")]
    [Tooltip("9-3장: 함정 정보 전파 후 발견 유닛 응답 대기 시간 (초) — 개정 문서 공식값")]
    public float trapJoinWaitSeconds            = 2f;
    [Tooltip("03번 v0.12 8장: 담당자 도착 대기 기한 = 도착 예정 시점 + 이 여유 (초)")]
    public float trapSelectedUnitLateGraceSeconds = 3f;
    [Tooltip("담당자가 도착 예정 시점이 달라졌다고 새로 알릴 최소 변화량 (초) — 문서 미명시, 내부 판단값")]
    public float trapArrivalChangeToleranceSeconds = 0.5f;
    [Tooltip("9-4장: 기록된 함정의 예상 성공률이 이 값 초과면 직접 해제 시도")]
    [Range(0f, 1f)]
    public float trapRecordedDisarmThreshold   = 0.5f;
    [Tooltip("함정 해제 기준 소요시간 (초) — 문서 미명시, 내부 판단값")]
    public float trapDisarmDurationSeconds     = 5f;
    [Tooltip("9-7장: 해제 중단 시 진행량 손실 비율")]
    public float trapDisarmInterruptLossRatio  = 0.5f;
    [Tooltip("9-10장: 함정 통과 후 남아야 할 최소 HP 비율 (일반)")]
    [Range(0f, 1f)]
    public float trapPassMinHpRatioAfterHit    = 0.5f;
    [Tooltip("9-11장: 아군 보호 시 기록 함정 통과 후 최소 HP 비율")]
    [Range(0f, 1f)]
    public float trapRescueMinHpRatioRecorded  = 0.3f;
    [Tooltip("9-11장: 미기록 함정 보호 진입 조건 — 이동 유닛 현재 HP 비율 이상")]
    [Range(0f, 1f)]
    public float trapRescueMinHpRatioUnrecorded = 0.6f;
    [Tooltip("검증 03-13: 알려진 활성 함정 회피(일반 이동 인접 1칸 진입 금지·전투 통과 판정·긴급 보호 통과)를 켠다. 끄면 예전처럼 함정을 무시하고 이동한다")]
    public bool trapAvoidanceEnabled = true;

    // ── 포메이션 ────────────────────────────────────────────────────────────
    [Header("보호 포메이션 (03문서 6장)")]
    [Tooltip("HitRange가 이 값 이상이면 후방 원거리 배치")]
    public int   formationRangedHitRangeThreshold = 4;
    [Tooltip("6-5장: 원거리 유닛 후방 최소 거리 (칸)")]
    public float formationRangedMinBackDistance   = 2f;

    // ── 탐색 ────────────────────────────────────────────────────────────────
    [Header("탐색 (NavigationFSMState)")]
    [Tooltip("계단 도달 판정 체비쇼프 거리 반경 (내부 판단)")]
    public int stairArrivalRadius = 1;
    [Tooltip("미탐색 타일 탐색 반경 (내부 판단)")]
    public int exploreRadius      = 10;
}
