# FSM + BT 제어 변수 목록

작성일: 2026-07-24  
FSM+BT가 읽거나 쓰는 변수를 역할별로 분류.

---

## 1. FSM 전환 차단 변수

`UnitFSM.SelectState` / `RunCurrentState`가 **가장 먼저** 확인하는 변수들.

| 변수 | 위치 | 역할 |
|------|------|------|
| `StatusEffects.State.stunDuration` | Unit → StatusEffectsComponent | > 0 이면 JudgeState·ExecuteAction 모두 즉시 반환 |
| `CombatState.State.isCastingAttack` | Unit → CombatStateComponent | true 이면 RunCurrentState가 Tick 호출을 건너뜀 |

---

## 2. 각 FSMState.GetPriority 판정 변수

### CombatFSMState (우선순위 100)

| 변수 | 위치 | 판정 내용 |
|------|------|----------|
| `Perception.State.personalSpottedEnemies` | Unit → PerceptionComponent | 같은 층에 hp > 0 인 적이 하나라도 있으면 활성 |
| `unit.currentFloor` | Unit | 위 목록의 적과 층 비교 |

### TacticalFSMState (우선순위 50)

| 변수 | 위치 | 판정 내용 |
|------|------|----------|
| `BaseStat.mental` | Unit → BaseStatComponent | < maxMental × panicMentalRatio(기본 0.3) → Panic 활성 |
| `BaseStat.maxMental` | Unit → BaseStatComponent | 위와 함께 사용 |
| `currentTrapInteraction` | Unit | null 아니면 TrapResponse 활성 |
| `currentAlertSearch` | Unit | null 아니면 Alert 활성 |
| `currentInvestigation` | Human | null 아니면 Investigate 활성 |
| `FindInvestigateTarget()` | Human | 도달 가능한 조사 대상이 있으면 Investigate 활성 |
| `currentWait` | Human | null 아니면 Wait 활성 |
| `HasProtectiveFormationNeed()` | Human | true 면 Formation 활성 |

### NavigationFSMState (우선순위 10, 항상 활성)

별도 판정 없음 — GetPriority가 항상 navigationPriority(기본 10) 반환.

---

## 3. BT 분기 조건 변수 (BTCondition이 읽는 값)

### Combat BT (ExecuteCombat Leaf 내부)

| 변수 | 역할 |
|------|------|
| `personalSpottedEnemies` | 가장 가까운 적 탐색 |
| `unit.currentFloor` | 적과 같은 층인지 확인 |
| `CombatState.State.evadeCooldown` | > 0 이면 이동 행동 건너뜀 |
| `skill.HitRange` | >= rangedMin(기본 4) 이면 원거리 → 키팅 분기 |
| `chebDist` | 현재 거리 (키팅 임계값 HitRange/2 비교) |
| `skill.IsAvailable(unit)` | 스킬 쿨다운·조건 확인 |

### Tactical BT

| 조건 메서드 | 읽는 변수 |
|------------|---------|
| `IsPanic` | `BaseStat.mental`, `BaseStat.maxMental` |
| `CanDisarm` | `currentTrapInteraction`, `isHitThisTurn`, `HasPerceivedThreatCollider()` |
| `HasAlert` | `currentAlertSearch` |
| `CanInvestigate` | `playerAttackTarget`, `playerMoveTarget`, `isManualMoveCommand`, `currentInvestigation`, `isHitThisTurn`, `HasPerceivedThreatCollider()` |
| `HasWait` | `currentWait`, `playerAttackTarget`, `playerMoveTarget`, `isManualMoveCommand`, `isHitThisTurn`, `personalSpottedEnemies.Count` |
| `HasFormationNeed` | `HasProtectiveFormationNeed()` → `currentFormation`, `FindDirectlyVisibleInteractingAlly()` |

#### HasPerceivedThreatCollider 내부에서 사용하는 변수

| 변수 | 위치 |
|------|------|
| `VisionStat.spotting` | VisionStatComponent (인지 거리 계산) |
| `personalSpottedEnemies` | PerceptionComponent |
| `enemy.currentThreat` | AIStateComponent |
| `unit.position` / `enemy.position` | Unit |

### Navigation BT

| 조건 메서드 | 읽는 변수 |
|------------|---------|
| `HasPendingStairs` | `pendingStairTargetFloor` (HasValue && Value != currentFloor) |
| `HasPlayerAttackTarget` | `playerAttackTarget` (null 아니고 hp > 0) |
| `HasPlayerMoveCommand` | `playerMoveTarget`, `isManualMoveCommand` |

---

## 4. BT Leaf가 읽고 쓰는 서브태스크 진행 상태

### TrapInteractionState (`currentTrapInteraction`)

Unit 공통 필드. null 이면 대응 중 아님.

| 필드 | 타입 | 역할 |
|------|------|------|
| `Phase` | `TrapPhase` | AwaitingJoin / Disarming / Destroying — Leaf가 진행 단계 제어 |
| `TrapObjectId` | string | objectGrid 조회용 ID |
| `TrapPosition` | Vector3Int | 함정 위치 (이동 목표) |
| `JoinWaitElapsed` | bool | false 이면 TrapJoinWait Leaf가 Running 반환 |
| `JoinWaitTimer` | float | OnUpdate가 증가시킴 |
| `HigherRateJoiner` | Human | 더 높은 성공률 합류자 — 있으면 이 유닛은 기다림 |
| `PenaltyActive` | bool | 해제/파괴 중 시야·인지 50% 페널티 여부 |
| `DisarmProgress01` | float | 0→1 해제 진행도. 중단 시 50% 손실 |
| `DestroyProgressDamage` | float | 파괴 누적 피해 |
| `IsBlockingPath` | bool? | null이면 미계산. BFS로 한 번 계산 후 캐시 |

### AlertSearchState (`currentAlertSearch`)

Unit 공통 필드. null 이면 경계 중 아님.

| 필드 | 타입 | 역할 |
|------|------|------|
| `TargetPosition` | Vector2Int? | 접근할 수상한 위치. null이면 주변 수색 |
| `IsPostCombatSweep` | bool | true 이면 전투 후 10초 스윕 |
| `ElapsedSeconds` | float | 경계 경과 시간 (OnUpdate 증가) |

**세팅 시점**: `CombatFSMState.OnExit`가 `IsPostCombatSweep=true`로 자동 생성.  
수상한 타일/미식별 공격 인지 시 외부에서 `TargetPosition`을 세팅.

### InvestigationState (`currentInvestigation`, Human 전용)

| 필드 | 타입 | 역할 |
|------|------|------|
| `TargetObjectId` | string | 조사 대상 오브젝트 ID |
| `TargetPosition` | Vector3Int | 이동 목표 위치 |
| `Progress01` | float | 0→1 진행도. 중단 시 50% 손실 |
| `PenaltyActive` | bool | 조사 중 시야·인지 50% 페널티 여부 |

### WaitState (`currentWait`, Human 전용)

| 필드 | 타입 | 역할 |
|------|------|------|
| `Reason` | WaitReason | AwaitingJoinBeforeApproach / AwaitingPartyAtRallyPoint |
| `WaitPosition` | Vector2Int? | 집결 위치 (있으면 거기로 이동 후 대기) |

### FormationState (`currentFormation`, Human 전용)

| 필드 | 타입 | 역할 |
|------|------|------|
| `EscortTarget` | Human | 호위 대상. null이면 포메이션 해제 상태 |

---

## 5. 플레이어 명령 변수 (InputManager가 세팅)

Unit 공통 필드. NavigationFSMState와 TacticalFSMState의 조건에서 확인.

| 변수 | 타입 | 역할 |
|------|------|------|
| `playerMoveTarget` | Vector2Int? | 이동 명령 목적지 |
| `isManualMoveCommand` | bool | true 이면 플레이어가 직접 내린 이동 명령 |
| `playerAttackTarget` | Unit | 공격 명령 대상 |
| `playerInteractTarget` | Vector3Int? | 상호작용 명령 위치 (미사용 — 향후 확장용) |
| `pendingStairTargetFloor` | int? | HumanWaveManager가 세팅. 계단 이동 목적 층 |

---

## 6. 전투 제어 변수 (CombatStateComponent)

| 변수 | 타입 | 역할 |
|------|------|------|
| `isHitThisTurn` | bool | 이번 턴 피격 여부 — Tactical 조건에서 중단 트리거로 사용. Tick 말미에 false 리셋 |
| `oneTimeReactUsed` | bool | 일회성 반응 사용 여부. Navigation 이탈 시 UnitFSM이 false 리셋 |
| `isCastingAttack` | bool | 공격 시전 중 — RunCurrentState 실행 차단 |
| `evadeCooldown` | float | 회피 쿨다운 — > 0 이면 키팅·이동 건너뜀 |
| `currentAttackAngle` | float | 공격 각도 — SkillAction.BuildSkillHitbox가 읽음 |

---

## 7. 인지·시야 관련 변수

| 변수 | 타입 | 역할 |
|------|------|------|
| `Perception.State.personalSpottedEnemies` | List\<Unit\> | 현재 인지 중인 적 목록. Combat 진입 조건 |
| `VisionStat.spotting` | float | 시야 수치. HasPerceivedThreatCollider 인지 거리 계산 |
| `AIState.currentThreat` | ThreatTileData | 위협 텔레그래프. HasPerceivedThreatCollider가 읽음 |

---

## 8. 변수 쓰기 책임 요약

| 변수 | 세팅하는 쪽 | 클리어하는 쪽 |
|------|------------|-------------|
| `currentTrapInteraction` | UnitPerceptionHandler (인지 시) | TrapDisarmPerform / TrapBypass / TrapPass / TrapDestroy Leaf |
| `currentAlertSearch` | CombatFSMState.OnExit (전투 후 스윕), UnitPerceptionHandler (수상한 타일·미식별 공격) | AlertApproach Leaf (목표 도달 시), OnUpdate (시간 초과 시) |
| `currentInvestigation` | MoveToInvestigateTarget Leaf | InvestigatePerform Leaf (완료/실패 시) |
| `currentWait` | 외부 (파티 시스템 등) | ExecuteWait Leaf |
| `currentFormation` | MoveToMeleeSlot / MoveToRangedSlot Leaf 내 암묵적 세팅 | 포메이션 조건이 false가 될 때 |
| `pendingStairTargetFloor` | HumanWaveManager | CrossStairs Leaf (층 이동 완료 시) |
| `playerMoveTarget` | InputManager | CompletePlayerCommand Leaf |
| `isManualMoveCommand` | InputManager | CompletePlayerCommand Leaf |
| `playerAttackTarget` | InputManager | ExecutePlayerAttack Leaf (적 사망·층 이탈 시) |
| `isHitThisTurn` | DefenseSystem (피격 처리) | UnitFSM.RunCurrentState 말미에 false 리셋 |
| `oneTimeReactUsed` | Combat 외 상태에서 UnitFSM.SelectState가 false 리셋 | — |
