# 인류 AI — FSM + BT 설계 현황

작성일: 2026-07-24  
대상 파일: `Assets/Script/Unit/AI/`

---

## 개요

기존 GOAP(Dijkstra 탐색 기반 재계획)를 FSM + BT 구조로 교체했다.

| 역할 | 담당 | 한 줄 설명 |
|------|------|-----------|
| **FSM** | `UnitFSM` + `IFSMState` | "지금 어떤 상태인가" — 우선순위 비교로 상태 전환 |
| **BT**  | `BTNode` 서브클래스들   | "그 상태에서 뭘 할 것인가" — Sequence/Selector 트리 |

BT는 **스테이트리스**다. 매 틱 루트 노드부터 재평가하며, 진행 상태(TrapPhase, InvestigationState 등)는 Unit 데이터 필드에 저장한다.

---

## FSM 전환 흐름

```
JudgeState() 호출
    │
    ├─ Sticky 유지 조건 확인
    │     현재 상태가 IsSticky=true이고 ShouldInterrupt=false → 그대로 유지
    │
    └─ _states 배열 순서대로 GetPriority(unit) > 0 인 첫 번째 상태 선택
           ↓
       상태 변경 시: 이전.OnExit() → 다음.OnEnter()
           └─ CombatFSMState 이탈 시 OnExit가 AlertSearch 세팅

ExecuteAction() 호출
    └─ 현재 상태.Tick(unit) → BT 루트 노드부터 평가
```

---

## 3개 FSM 상태

| 상태 | 기본 우선순위 | Sticky | 진입 조건 |
|------|------------|--------|----------|
| `CombatFSMState` | 100 | X | `personalSpottedEnemies`에 같은 층 살아있는 적 존재 |
| `TacticalFSMState` | 50 | X | 공황/함정/경계/조사/대기/포메이션 조건 중 하나 이상 충족 |
| `NavigationFSMState` | 10 | X | 항상 활성 (기본 폴백 상태) |

> 우선순위 수치는 `Assets/Resources/FSM+BT/AIBehaviorConfig.asset`에서 기획 수정 가능.  
> Sticky는 현재 세 상태 모두 false — BT 내부의 Running 반환이 서브태스크 연속성을 유지한다.

---

## CombatFSMState BT

```
BTLeaf(ExecuteCombat)          ← 단일 리프, 내부에서 분기 처리
    │
    ├─ [키팅] bestSkill.HitRange >= rangedMin 이고 적이 위험거리 이내
    │      → MoveAwayFromTarget
    │
    ├─ [공격] 적이 스킬 히트박스 범위 내
    │      → skill.Execute()
    │
    └─ [접근] 위 조건 모두 불충족
           → MoveTowardsTarget
```

**OnExit**: 전투 상태를 벗어날 때 `AlertSearchState { IsPostCombatSweep = true }` 세팅.  
**GetLabel**: `"Combat"`

---

## TacticalFSMState BT

BT 자식 순서는 `Assets/Resources/FSM+BT/TacticalPriority.asset`에서 변경 가능.  
에셋이 없으면 아래 기본 순서(03문서 2-1장)로 폴백한다.

```
BTSelector  ← 03문서 기본 순서
├─ [Panic]         BTSequence
│    ├─ Condition: mental < maxMental × panicMentalRatio(0.3)
│    └─ Leaf: 50% 확률로 무작위 이동, 나머지는 정지
│
├─ [TrapResponse]  BTSelector  ← 내부 순서 고정(9장 우선순위)
│    ├─ BTSequence: Condition(CanDisarm) → TrapJoinWait → MoveToTrap → TrapDisarmPerform
│    ├─ Leaf: TrapBypass  (경로 미차단 시 우회)
│    ├─ Leaf: TrapPass    (HP 조건 충족 시 맞고 통과)
│    └─ Leaf: TrapDestroy (최후 수단)
│
├─ [Alert]         BTSequence
│    ├─ Condition: currentAlertSearch != null
│    └─ BTSelector
│         ├─ Leaf: AlertApproach      (목표 위치가 있으면 접근)
│         └─ Leaf: AlertPerimeterSearch (주변 수색)
│
├─ [Investigate]   BTSequence
│    ├─ Condition: 플레이어 명령 없고, 조사 가능한 오브젝트 있고, 피격·위협 없음
│    ├─ Leaf: MoveToInvestigateTarget
│    └─ Leaf: InvestigatePerform
│
├─ [Wait]          BTSequence
│    ├─ Condition: currentWait != null, 플레이어 명령·전투·피격 없음
│    └─ Leaf: ExecuteWait (RallyPoint 대기 또는 제자리)
│
└─ [Formation]     BTSequence
     ├─ Condition: HasProtectiveFormationNeed()
     └─ BTSequence
          ├─ BTSelector
          │    ├─ Leaf: MoveToMeleeSlot  (!IsRangedFormationRole)
          │    └─ Leaf: MoveToRangedSlot (IsRangedFormationRole, backDist 설정값)
          └─ Leaf: HoldFormation (에스코트 대상 방향 유지)
```

**GetLabel**: `"Panic"` / `"TrapResponse"` / `"Alert"` / `"Investigate"` / `"Wait"` / `"Formation"` / `"Tactical"`

---

## NavigationFSMState BT

BT 자식 순서는 생성자에 하드코딩(설정 에셋 없음 — 2026-08-24 FSM 리팩토링 감사로
`NavigationBehaviorPriorityConfig`/`NavigationPriority.asset`가 죽은 설정임이 확인돼 삭제됨. 실제
에셋 인스턴스가 생성된 적도 없었다). 플레이어 공격/이동 명령은 2026-07-24에
`PlayerCommandFSMState`로 완전히 분리됐다 — 아래 트리엔 더 이상 없다.

```
BTSelector
├─ [Stairs]       BTSequence
│    ├─ Condition: pendingStairTargetFloor.HasValue && Value != currentFloor
│    ├─ Leaf: MoveToStairs   (매 틱 최적 접근 후보 타일로 이동)
│    └─ Leaf: CrossStairs    (2×2 블록 반경 stairArrivalRadius 이내 → 층 이동 실행)
│
├─ [DungeonEntranceWait] BTSequence (2026-08-20 신규)
│    ├─ Condition: IsInDungeonEntranceSequence (DungeonEntranceSystem이 0층 진형을 제어 중)
│    └─ Leaf: HoldPosition
│
└─ [Explore]      Leaf: RandomExplore
      30% 확률 무작위 이동,
      나머지: exploreRadius 내 미탐색 타일(discoveredMap==0) 중 가장 가까운 곳으로 이동
```

**GetLabel**: `"탐색(계단)"` / `"탐색(탐험)"`(2026-08-24 문서 정정 — 예전 영문 라벨 표기는 이미 오래
전에 한국어 라벨로 교체돼 실제 코드와 안 맞았다. 플레이어 명령 중엔 PlayerCommandFSMState.
GetLabel이 별도로 `"명령(공격)"`/`"명령(이동)"`을 반환 — NavigationFSMState는 그 라벨을 관여하지
않는다)

---

## BT 노드 타입 요약

| 클래스 | 동작 |
|--------|------|
| `BTSequence` | 자식을 순서대로 실행. 하나라도 Failure/Running이면 그 값 반환. 전부 Success → Success |
| `BTSelector` | 자식을 순서대로 실행. 하나라도 Success/Running이면 그 값 반환. 전부 Failure → Failure |
| `BTCondition` | predicate(unit) → true면 Success, false면 Failure |
| `BTLeaf` | action(unit) → BTStatus 직접 반환 |

---

## 설정 파일 (기획 수정 가능)

| 에셋 경로 | 클래스 | 역할 |
|----------|-------|------|
| `Resources/FSM+BT/AIBehaviorConfig.asset` | `AIBehaviorConfig` | 수치 파라미터 전체 (우선순위, 비율, 초 단위 값 등) |
| `Resources/FSM+BT/TacticalPriority.asset` | `TacticalBehaviorPriorityConfig` | Tactical BT 자식 순서 + 활성화 여부 |

(`NavigationPriority.asset`/`NavigationBehaviorPriorityConfig`는 2026-08-24 삭제 — Navigation BT는
설정 에셋 없이 생성자에 하드코딩된 순서로만 동작한다. 위 NavigationFSMState BT 섹션 참고.)

에셋 생성 메뉴: **GrimArchive → AI → FSM+BT 설정 에셋 생성**  
에셋이 없으면 각 값은 `?? 하드코딩 기본값`으로 폴백한다.

---

## 진입점 (Unit.cs)

```
JudgeState()   → fsm.SelectState(this)
ExecuteAction() → fsm.RunCurrentState(this)
GetLabel()     → fsm.GetLabel(this)   // UI/디버그 표시용
```

---

## 비주얼 스크립팅 전환 시 교체 지점

| 현재 | 전환 후 |
|------|--------|
| `AIConfigLoader`의 `Resources.Load<T>()` | `BTGraphAsset` 로더로 교체 |
| `TacticalBehaviorType` enum | BTNodeSO 파생 클래스의 타입 식별자 |
| `BuildBTNodes()` 딕셔너리 | BTNodeSO 에셋에서 직접 트리 구성 |
| `TacticalPriority.asset` | 그래프 에셋으로 통합 |
