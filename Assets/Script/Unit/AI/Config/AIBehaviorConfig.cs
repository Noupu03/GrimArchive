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
    [Tooltip("방 이동(문 앞 자리로 이동) 중 길이 막혀 더 못 다가갈 때 제자리에서 기다렸다 다시 시도하기까지의 시간(초) — 막혔다고 명령을 풀지 않는다 (검증문서 04-02, 내부 판단)")]
    public float doorApproachHoldRetrySeconds = 1f;
    [Tooltip("방 이동이 계속 막힌 채로 이 시간(초)을 넘기면 명령을 풀고 개인 행동으로 돌아간다 — 닿을 수 없는 자리에 영구히 얼어붙는 것을 막는 안전장치 (검증문서 04-02, 내부 판단)")]
    public float doorApproachMaxBlockedSeconds = 30f;
    [Tooltip("점유 충돌 판단(대기 vs 우회 예상시간 비교·비켜 주기, 검증문서 04-05/04-06) 전체 kill-switch — 끄면 예전처럼 점유 타일은 곧바로 벽으로 보고 우회한다 (내부 판단)")]
    public bool occupancyArbitrationEnabled = true;
    [Tooltip("좁은 통로(게이트 문 타일) 통과 순서·마주 막힘 양보(검증문서 04-07) kill-switch — 끄면 통과 순서 대기를 하지 않는다 (내부 판단)")]
    public bool gatePassOrderEnabled = true;
    [Tooltip("집결 중 집결지(리더 자리)에 못 서면 근처 자리를 따로 배정하고, 도착한 파티원도 전원이 모일 때까지 자리에서 기다린다. 끄면 예전처럼 전원이 리더 자리로 몰리고 4틱 정체 시 도착으로 간주한다 (내부 판단)")]
    public bool rallyGatherNearbyEnabled = true;
    [Tooltip("집결 자리 배정·도착 판정을 널널하게 한다(2026-10-03 플레이 로그: 집결지 근처에서 자리 재선택과 6초 점유 대기가 반복돼 집결이 길어졌다). 켜면 ① 자리에서 rallyArrivalRadius칸 안이면 도착으로 보고 ② 막혀 못 가는데 이미 집결 구역 안이면 자리를 바꾸지 않고 선 자리에서 바로 인정하며 ③ 이미 자리에 서 있는 같은 파티원을 기다리지 않고 ④ 처음 자리는 지금 비어 있는 칸을 먼저 고른다. 끄면 예전(도착 반경 1·재선택 우선·점유 대기) (내부 판단)")]
    public bool rallyLenientSlotEnabled = true;
    [Range(1, 4), Tooltip("rallyLenientSlotEnabled가 켜졌을 때 집결 자리 도착으로 보는 체비셰프 반경(칸). 예전 값은 1 (내부 판단)")]
    public int rallyArrivalRadius = 2;
    [Tooltip("집결 완료 뒤 문 앞 진형 → 리더 지시 문 파괴 → 랭크 순 입장으로 다음 방에 들어간다. 끄면 예전처럼 문 앞 자리로 흩어져 이동한 뒤 개인 행동으로 풀린다 (내부 판단)")]
    public bool partyAdvanceFormationEnabled = true;
    [Tooltip("집결 완료 판정이 명령을 받지 못한 생존 구성원과 리더가 모르는 사망까지 \"미집결자\"로 보고 기다린다(05번 5장 295~307줄, 검증 05-05). 시간 상한(집결 시작 후 doorApproachMaxBlockedSeconds×2)에 닿으면 포기하고 이후 집결에서 다시 기다리지 않는다. 끄면 예전처럼 대기가 있는 구성원만 확인한다")]
    public bool rallyAbsenteeWaitEnabled = true;
    [Tooltip("문 먼 쪽(다음 방) 입장 자리를 실제 지형이 아니라 리더·본인의 개인 지도로 바닥이 확인된 타일에서만 고른다(04번 0장 12~14줄, 검증 05-06 관찰 4). 끄면 예전처럼 실제 지형으로 고른다")]
    public bool entrySlotKnowledgeEnabled = true;
    [Tooltip("인류 개인의 탐색·조사 후보를 지금 있는 방 안으로 제한한다 — 개인이 임의로 다음 방에 들어가지 않는다(04번 10장 578줄·05번 1장 53줄·01번 585줄, 검증 05-01). 끄면 예전처럼 층 전체")]
    public bool roomBoundExplorationEnabled = true;
    [Tooltip("방 입장(문 파괴 뒤 Entering 단계)에서 유닛을 역할 → HP → 무작위 순(좁은 통로 통과 순서, 04번 8장)으로 이 간격(초)마다 한 명씩 출발시킨다. 앞 단계가 문을 완전히 지날 때까지 기다리는 단계 장벽이 없어 앞뒤가 겹쳐 흐른다(사용자 확정 2026-10-02 — 입장 템포가 너무 느려서). 0 이하면 예전 단계 장벽(앞 역할 단계가 전원 문을 지나야 다음 단계 출발) — 내부 판단 수치")]
    public float entryReleaseIntervalSeconds = 0.5f;
    [Tooltip("적 발견 시 전투 합류 대기(위험도 2단계 이상 + 2칸 초과 거리에서 합류 의사 응답 2초·실제 합류 최대 5초, 07-A v0.2 9장)를 쓴다. 2026-10-02부터 임시로 꺼 둠(기본 false) — 현재 문서 세트(03번 v0.12 391·419줄, 06번 설정값 표, 07번 03-08)가 이 대기를 삭제해 \"아군 응답·도착을 기다리지 않고 즉시 전투 대응\"을 요구한다. 07-A가 개정되면 코드를 지운다. 끄면 적을 정확 인지하는 즉시 전투하고 적 정보 전파는 그대로 병행된다")]
    public bool joinCombatWaitEnabled = false;
    [Tooltip("방 입장 중인 유닛이 문 통로 안·다음 방 쪽의 앞 유닛에 막히면 점유를 벽으로 본 길찾기로 길 전체를 돌아가지 않고, 구조 경로(점유 무시) 그대로 그 유닛이 지나가길 기다렸다 들어간다(사용자 확정 2026-10-02 \"앞 유닛이 완전히 들어간 후 들어가도록\"). 입장 단계 상한(doorApproachMaxBlockedSeconds×2)이 영구 대기를 끊는다. 끄면 예전 점유 판단(대기 vs 우회)")]
    public bool entryWaitForUnitAheadEnabled = true;
    [Tooltip("방 이동 때 문을 못 지난 구성원이 옛 방에 낙오되지 않게 한다(2026-10-03 플레이 로그: 주술사가 문 6칸 뒤 옛 방에서 '입장 구역 안'으로 정착하고 계획이 그대로 끝나 둘이 남았다). 켜면 ① 입장 중에는 문을 지나기 전 '구역 안' 정착을 하지 않고(30초 포기·60초 단계 상한은 유지) ② 입장 단계가 끝날 때 문을 못 지난 구성원에게 파티가 지나간 통과 지점을 마지막 확인 리더 위치로 알려 기존 합류가 문 너머로 따라가게 한다. 끄면 예전 동작 (내부 판단)")]
    public bool entryStragglerFollowEnabled = true;
    [Tooltip("리더가 다음 이동 게이트를 알려진 방 그래프(미방문 방 우선·막다른 방 되돌이·파괴된 통로 포함)로 고른다(04번 1장, 검증 05-06 관찰 2). 끄면 예전처럼 남아 있는 문 중 목표 방에 가장 가까운 것")]
    public bool leaderRoutePlannerEnabled = true;
    [Tooltip("길찾기(A*)가 진영 공용 지도 대신 유닛 개인 지도(직접 확인했거나 전달받아 아는 지형)로 경로를 계산한다 (검증문서 04-08, 04번 0장). 끄면 예전처럼 진영 공용 discoveredMap을 쓴다 (내부 판단)")]
    public bool personalMapPathingEnabled = true;
    [Tooltip("조사 후보를 고를 때 구조 경로(점유 무시)로도 닿을 수 없다고 확인된 대상을 후보에서 제외한다 (검증문서 04-08, 04번 9장 '통행 불가 확인'). 끄면 제외하지 않고 아주 먼 거리로만 계산한다 (내부 판단)")]
    public bool unreachableCandidateExclusionEnabled = true;
    [Tooltip("탐험(RandomExplore)이 길찾기로 닿지 못한 미탐색 목표를 그 타일을 벽으로 위조해 지우는 대신 '막힘 기록'으로 남기고, 아는 정보(지형·문·함정)가 바뀔 때까지 다시 고르지 않는다 (검증문서 04-08 발견 5, 04번 9장 '막힘 기록과 재시도 조건'). 닿지 못해 막힌 프론티어는 리더의 방 탐색 완료 판정에서도 제외한다. 끄면 예전처럼 그 타일을 벽으로 기록한다 (내부 판단)")]
    public bool exploreBlockedRecordEnabled = true;
    [Tooltip("통행 불가로 판정한 대상을, 아는 정보(지형·문·함정)가 바뀌었어도 판정 직후 이 시간(초) 동안은 다시 탐색하지 않는다 — 탐험 중 아는 지형이 매 틱 바뀌어 같은 대상을 반복 탐색하는 비용 폭주를 막는 성능 장치 (내부 판단)")]
    public float routeRecheckMinSeconds = 2f;
    [Tooltip("점유자가 비워질 시간을 모르고 우회도 없는 대기가 이 시간(초)을 넘으면 대기를 풀고 기존 경로(행동별 정체 인내 → 포기)로 넘긴다. 시간을 아는 대기(상호작용 남은 시간 등)는 끊지 않는다 (내부 판단)")]
    public float occupancyWaitMaxSeconds = 6f;
    [Tooltip("점유 대기 중 같은 충돌을 다시 판정하는 간격(초) — 점유자 상태가 바뀌었는지(상호작용 종료·이동 시작) 확인한다 (내부 판단)")]
    public float occupancyRecheckSeconds = 0.5f;
    [Tooltip("제자리 대기 중인 아군에게 남기는 '비켜 달라' 요청의 유효 시간(초) (내부 판단)")]
    public float yieldRequestSeconds = 2f;
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
    [Tooltip("공황 행동 사용 여부. 2026-10-02부터 임시로 꺼 둠(기본 false) — 시작 정신력 40/100에 임계값 30%라 시체 두 번이면 영구 공황이고(회복 코드 없음) 무작위 걸음이 BT 최상위라 전 행동이 막혔다. 도주·후퇴 문서(06 위기반응) 이후 켠다. 정신력 감소·인지 보정·가중치 정보 오차는 이 값과 무관하게 그대로 동작")]
    public bool panicBehaviorEnabled = false;
    [Range(0f, 1f), Tooltip("정신력이 최대 정신력의 이 비율 미만이면 공황 진입(panicBehaviorEnabled가 켜져 있을 때만)")]
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
