# GrimArchive_Prototype

Unity 2D 탑다운 던전 크롤러 프로토타입. VContainer(DI) + Haare 프레임워크 기반으로 점진적 리팩토링 중.

## 대화 언어

**항상 한국어로 대답한다.** 코드/커밋 메시지/문서뿐 아니라 채팅 응답(설명, 요약, 작업 완료 보고 등)도
전부 기본값이 한국어 — "긴 기술 요약이라서", "큰 작업을 마친 뒤의 최종 요약이라서" 같은 예외를 두지
않는다. 사용자가 영어로 말을 걸면 그 턴만 맞춰 영어로 답하고, 다음 턴부터는 다시 한국어로 돌아간다.

## 작업 방식

기획 문서를 구현할 때 애매하거나 모호한 부분(수치 임계값이 명시 안 됨, 기존 코드 구조와 충돌,
구현 범위가 여러 갈래로 갈릴 수 있음 등)이 나오면 임의로 판단하고 넘어가지 말고 **반드시 사용자에게
먼저 질문한다.** 특히 다음 경우는 질문 우선:
- 문서가 수치/기준을 명시하지 않아 판단 근거를 새로 만들어야 하는 경우
- 기존 코드의 동작 방식(예: 특정 필드가 항상 즉시 갱신되는 것)을 문서 요구사항이 바꿔야 하는 경우
- 구현 범위가 "최소 스텁"과 "전체 리팩토링" 등으로 크게 갈리는 경우

## 대표 가중치 3종 시스템 (이해도 / 위험도 / 흥미도)

### 배경

인류 AI가 던전 내 대상/위치를 판단하는 핵심 값 3종을 구현한다. 근거 문서 2개(`Assets/문서/공식문서/가중치 관련/`):

- `GrimArchive_대표_가중치_3종_시스템_문서_v3.docx.md` — 배경/설계 의도 (구현 대상 아님, 판단 기준 참고용)
- `GrimArchive_대표가중치_연산공식_v0.7_양식정리.docx (1).md` — **실제 구현 대상**, 25개 장. "(1)"이
  최신 개정판(15/16/24장 세 곳만 원본과 다름).

구현 현황(장별 태그: ✅구현/🔶부분·수치만/❌미구현, 애매했던 부분의 판단 근거)은
`Assets/문서/구현현황/구현중/가중치_구현현황_2026-07-20.txt`(가장 최신본 — 같은 폴더의
`가중치_수정예정_2026-07-20.txt`도 같이 볼 것. 이전 버전들은 `구현현황/지난거/`에
이력으로만 남아있음)에 연산공식 문서 순서 그대로 기록되어 있고, 문서 맨 위에 "다음 우선순위 구현
후보" 섹션도 있다. 이 시스템을 다시 손댈 때는 그 문서를 먼저 확인할 것 — 매번 새로 조사하지 말고
이 문서를 갱신하는 방식으로 유지보수한다(날짜가 바뀌면 새 파일을 만들고 이전 파일을 `지난거/`로
옮기는 것이 지금까지의 관례).

### 핵심 설계 전제

이 프로토타입에는 아직 정식 "웨이브"/"던전 재입장" 게임 루프가 없지만, **파티 시스템**
(`Assets/Script/Unit/Party/Party.cs`, 2026-07-08)이 그 전제를 부분적으로 메운 상태다.
`WaveSpawner.SpawnWave()`가 몬스터 웨이브와 함께 인류 파티를 스폰하고, `GameSession.
CheckPartyWaveState()`가 유닛이 죽을 때마다 그 파티의 전멸/웨이브클리어 여부를 판정해서 6장(생존자
전역 반영)·13장(전멸 위험도)을 실제로 트리거한다 — "웨이브 클리어 시점 생존자"를 문서의 "생존/후퇴/
도주 성공 유닛"의 근사치로 쓴다. 다만 같은 `Party` 인스턴스가 여러 웨이브에 걸쳐 재사용되며 기억을
유지하는 흐름(5-2장)은 이 샘플 범위 밖 — `WaveSpawner`가 스폰마다 새 `Party`를 만든다.

`HumanKnowledgeBase.OnWaveEnd(survivors)`는 여전히 외부에서 명시적으로 호출 가능한 공개 API로도
남아있다 — 진짜 웨이브 루프가 생기면 그 종료 지점에서 이 API를 직접 호출해도 되고, 지금처럼
파티 전멸 판정에서 간접 호출돼도 된다.

다른 문서에 위임된 부분(지도 기록/저장 문서, 도감 문서, 전투 연산 문서, 소리/전파 문서, 기억/
네메시스 문서, 레벨 구조 미정 — 문서 자체가 "정확한 공식은 OOO 문서에서 작성한다"고 명시)은 그
문서들이 아직 없으므로 가장 단순하고 합리적인 기본값으로 스텁 구현했다.

### 파일 구조

```
Assets/Script/Unit/Weight/
  WeightEnums.cs          — WeightType/InfoType/MentalErrorState/EventId/DangerStage/InterestStage
  WeightEventTable.cs     — EventId → (이해도Δ, 위험도Δ), weight_events.json 로더
  PersonalWeightRecord.cs — 유닛 개인이 대상별로 들고 있는 임시 기록
  IncidentLog.cs          — 이벤트 발생 기록 (6장 중복 제거/병합의 입력)
  SpeciesWeightState.cs   — 종별/개별 누적값, 특수행동 누적 상한
  HumanKnowledgeBase.cs   — 전역 레지스트리 (순수 C#, VContainer Register<T>().AsSelf())
  PersonalMapKnowledge.cs — 인류 개인 지도 기록 (15~21장: 타일/오브젝트/방 위험도·흥미도, Human.personalMap)
  WeightMath.cs           — 순수 계산 함수 모음 (부수효과 없음, 테스트 용이)
Assets/Script/Unit/Party/
  Party.cs                — Id/Name/Members/WaveMonsters/WaveEnded, IsWiped/IsWaveCleared/GetSurvivors
Assets/Script/Map/
  InteractableObject.cs   — 루팅/시체/전멸흔적 오브젝트 엔티티 (Tags로 구분: Loot/Corpse/WipeoutTrace)
Assets/Data/weight_events.json      — 3장 이벤트 표 데이터
Assets/Data/units.json              — 유닛별 stats + weight(이해도/위험도 5필드) + visual 블록
Assets/Tests/WeightSystemTests.cs   — 문서에 나온 숫자 예시를 그대로 고정한 단위 테스트
```

`Unit.cs`에 `personalWeights`/`isSpecialUnit`/`isInterestTarget`/`baseInterest`/`baseDanger`/
`heavyHitThreshold`/`lastAttacker` 필드가 있고, `GameCompositionRoot.cs`에 `HumanKnowledgeBase`가
순수 C# 싱글턴으로 등록돼 있다. 자동 연결된 이벤트는 이제 "직접 경험(SELF)" 계층을 넘어 상당히
넓다 — 피격/처치(`UnitFunction`/`GameSession.RemoveDeadUnit`), 오브젝트 발견/회수(`InteractableObject`
+ `UnitFunction.CastRay`), 파티 전멸/웨이브클리어(`GameSession.CheckPartyWaveState`), 시체·전멸흔적
발견(같은 `CastRay` 지점, `InteractableObject.Tags`로 분기)까지 자동으로 돈다. **간접 파악(소리/전파)**은
2026-07-31 07_전파·소리·간접입력 시스템 구현으로 발동 지점이 생겼고, 2026-08-05에 E_HIT_HEAVY_INDIRECT
(`PropagationSystem.TryConfirmIndirectHit`, `TacticalFSMState.SoundAreaApproach`가 호출)/
E_MONSTER_KILL_INDIRECT(`PropagationSystem.OnMonsterCorpseDiscovered`, `UnitFunction.CastRay`의 몬스터
시체 발견 지점이 호출)를 실제 소리·시체 확인 결과와 연결하는 마지막 배선까지 끝났다(아래
"전파·소리·간접입력 시스템" 섹션 참고). 여전히 막힌 것은 **버프/디버프/소환**, **함정/방어건물**,
**오브젝트 "조사" 단계** 등 "이벤트를 발생시킬 하위 게임 시스템 자체가 없는" 경우들 — 최신 구현현황
문서의 "다음 우선순위" 섹션 참고.

### 다음에 이 시스템을 확장할 때

1. `Assets/문서/구현현황/구현중/가중치_구현현황_2026-07-20.txt`(또는 그 이후 최신본) 맨 위 "다음 우선순위
   구현 후보" 섹션부터 확인 — 작업량 순으로 이미 정리돼 있다.
2. 새로 구현/연결한 게 있으면 그 문서를 직접 갱신한다(장별 태그 업데이트 + 전체 집계 수정). 날짜가
   많이 지났거나 대규모로 갱신했다면 새 날짜의 파일을 만들고 이전 파일은 `구현현황/지난거/`로 옮긴다.
3. 소리/전파 이벤트 발생 지점은 이제 `Assets/Script/Unit/Propagation/PropagationSystem.cs`에 있다 —
   소리 확인 결과를 `HumanKnowledgeBase.RecordEvent(..., InfoType.Indirect)`로 잇는 마지막 연결(위
   문단 참고)만 아직 비어있다.

## 시야-인지-반응 시스템 (시야 범위 / 인지 판정 / 정보 실패 처리)

### 배경

유닛이 대상을 보고 인지하고 그 결과에 따라 행동을 바꾸는 판단 구조. 근거 문서
(`Assets/문서/공식문서/시야인지반응/`): `00_...상위구조`(구현 대상 아님, 문서 패키지 전체 지도),
`01_...개념`+`01-A_...연산공식`(시야 범위/인지 범위/가시성, 실제 구현 대상), `02_인지·정보판정·
실패처리_시스템_v0.2`(인지 판정 확률/정신력 보정/수상한 타일/공격자 정체 인지 등, 실제 구현 대상 —
00장이 예고한 "02_개념"+"02-A_연산공식" 두 문서가 하나로 합쳐져 실제 전달됨). `03_탐색반응·경계·조사·
함정대응_시스템`(경계/조사/함정대응/보호포메이션 등, 실제 구현 대상 — 아래 별도 안내 참고)과
`07_전파·소리·간접입력`(전파/소리/공격방향 간접입력, 2026-07-31 구현 — 아래 "전파·소리·간접입력
시스템" 섹션 참고)도 이미 존재한다. 00장 상위구조 문서는 이 전파·소리 문서를 "08_전파·소리"로
표기하지만 실제 폴더 파일명은 "07_..."이다(리네이밍 드리프트로 추정, 가리키는 대상은 동일). 04(탐색
반응·경계 확장)/05-A(은신·가시성 세부산식)/06(전투반응·기습)/09(명령·리더)/10(목표설정·경로) 문서는
여전히 없다.

구현 현황은 `Assets/문서/구현현황/구현중/시야인지반응_구현현황_2026-08-05.txt`(01/01-A/02문서 —
가장 최신본, 이전 버전은 `구현현황/지난거/`)에 장별로 기록돼 있고, 맨 위에 "다음 우선순위 구현 후보"와
"사용자 확인이 필요했던 판단" 섹션이 있다 — 가중치 시스템과 동일한 관례로 유지보수한다(다시 손댈 때
이 문서부터 확인, 새로 구현/갱신하면 이 문서를 직접 갱신, 대규모 갱신 시 새 날짜 파일 + 이전 파일
`지난거/` 이동). 03문서(탐색반응·경계·조사·함정대응)는 별도 `시야인지반응_03_구현현황_2026-07-27.txt`
(또는 그 이후 최신본)에 장별로 기록된다 — 03과 01/01-A/02/07은 서로 다른 구현현황 파일로 관리된다.

### 파일 구조

```
Assets/Script/Unit/Vision/
  VisionEnums.cs      — VisionDirectionReason(01-A) + PerceptionOutcome/MentalTier/PerceptionReactionCandidate(02)
  VisionMath.cs        — 01/01-A 순수 계산 함수 + ResolveObjectVisibility(02 6장)
  PerceptionMath.cs    — 02문서 순수 계산 함수(감지 보정/정신력 6단계/확률표/수상한 타일·간접 인지 상수)
  PerceptionRecord.cs  — 대상별 지속 인지 상태(Outcome/WasInRange/PendingSuspiciousInvestigation)
Assets/Script/Unit/Propagation/ — 07_전파·소리·간접입력(아래 별도 섹션 참고)
Assets/Tests/
  VisionSystemTests.cs        — 01/01-A 숫자 예시 고정 테스트
  PerceptionSystemTests.cs    — 02문서 숫자 예시 고정 테스트
  PropagationSystemTests.cs   — 07/07-A 숫자 예시 고정 테스트
```

`Unit.cs`에 `spotting`/`stealth`/`baseVisibility`/`attackVisibilityBoostTimer`(01-A) +
`perceptionRecords`/`IsAlert`/`CanPerceive`/`GetMentalVisibilityCorrection`(02) 필드/프로퍼티가
있다. `UnitFunction.CastRay`/`UpdateFOV`가 매 턴 시야·인지 범위를 계산하고, 인지 판정은 트리거
시점(최초 진입/재진입/완전 차단 후 재등장/수상한 타일 2칸 재접근/피격 시)에만 확률표를 다시 굴려
`perceptionRecords`에 지속 저장한다(매 턴 반복 재판정 아님 — 02문서 4장). 공격자 정체 인지 게이팅
(`IsAttackerIdentified`/`ForceReidentifyAttacker`)은 피격 즉시 공격자를 알던 예전 동작을 대체해,
인지 판정 성공 시에만 정체 기반 가중치 이벤트를 기록한다.

### 다음에 이 시스템을 확장할 때

1. `Assets/문서/구현현황/구현중/시야인지반응_구현현황_2026-08-05.txt`(또는 그 이후 최신본) 맨 위
   "이번 반영 내역" → "이전 이력" → "다음 우선순위 구현 후보" 순으로 확인.
2. 새로 구현/연결한 게 있으면 그 문서를 직접 갱신한다(장별 태그 업데이트 + 전체 집계 수정). 대규모
   갱신이면 새 날짜 파일을 만들고 이전 파일은 `구현현황/지난거/`로 옮긴다.
3. 04(탐색반응·경계 확장)/06(전투반응·기습)/09(명령·리더)/10(목표설정·경로) 문서가 생기면 그 문서가
   담당하는 후보(반응 후보 실제 선택·실행, 수상한 타일 실제 이동, 공격 형태별 방향 정보 세부, 전투
   중 소리 기반 새 타겟 탐색, 09-A 11장 합류 5초 초과 처리)를 그 시점에 연결. 시야 방향 전환 후보와
   간접 인지(소리) 발생 지점은 07 구현으로 이미 연결됐다(아래 섹션 참고).

## 전파·소리·간접입력 시스템 (전파 / 소리 / 공격 방향 간접입력)

### 배경

유닛이 직접 인지하지 못한 정보(전파·소리·공격 방향)를 얻는 구조. 근거 문서
(`Assets/문서/공식문서/시야인지반응/`): `07_전파·소리·간접입력_개념_v0.2`(개념, 실제 구현 대상),
`07_A_...연산공식_v0.2`(연산공식, 실제 구현 대상). 2026-07-31에 구현했고, 기존 03문서(탐색반응·경계·
조사·함정대응)가 "07 문서 부재"로 근사 처리해뒀던 지점들(파티원 사망 재전파, 함정/코어 발견 공유,
보호 포메이션 참여 트리거)도 이때 실제 구현으로 교체됐다.

구현 현황은 `Assets/문서/구현현황/구현중/시야인지반응_구현현황_2026-08-05.txt`(01/01-A/02문서와 같은
파일 — 07문서 전용 섹션이 그 안에 있다)에 기록돼 있다. 다시 손댈 때는 그 문서의 "07문서 장별 구현
현황"/"03문서 근사치 교체 내역"/"다음 우선순위" 섹션부터 확인할 것.

### 파일 구조

```
Assets/Script/Unit/Propagation/
  PropagationEnums.cs      — SoundType(6종: 이동/공격실행/피격발생공격음/피격비명/사망/함정작동)
                              + AttackShape(근접/투사체/광역)
  PropagationMath.cs       — 07-A 순수 계산 함수(공간판정/거리BFS/전파범위/소리범위/감지범위/추정지역/
                              소리우선순위/유효시간/합류판정)
  PropagatedInfoRecord.cs  — PropagatedInfoRecord(전파정보)/PendingSoundReaction(일시간접입력)/
                              PendingAttackDirection(17장)
  JoinCombatWaitState.cs   — 07-A 9장 전투 합류 대기 하위 상태
  PropagationSystem.cs     — 부수효과 있는 호출부(소리 발생 시 즉시 범위 스캔+R3 통지/UniTask 유예
                              타이머/전파 조건/합류 판정/상호작용 정보 전파) — PartyDeathSystem과 동일 성격
Assets/Script/Unit/Core/PropagationComponent.cs — 유닛별 저장소(PerceptionComponent와 동일 패턴)
Assets/Tests/PropagationSystemTests.cs — 07/07-A 숫자 예시 고정 테스트 + PropagationSystem 통합 테스트
```

`Unit.cs`에 `Propagation`(컴포넌트 접근 프로퍼티) + `Human.currentJoinCombatWait` 필드가 있다. 소리
이벤트는 `UnitFunction.Move()`(이동음)/`SkillAction.BeginAttackCast`(공격실행음)/`UnitFunction.
RecordHitWeightEvent`(피격발생공격음·피격비명)/`GameSession.RemoveDeadUnit`(사망음)/`TacticalFSMState.
TrapPass`(함정작동음)에서 발생한다. **2026-08-05 재설계(사용자 피드백)**: 예전엔 `EmitSound`가 소리를
5초짜리 `_activeSounds` 목록에 넣어두고 모든 인류가 0.1초 틱마다 그 목록을 훑는 폴링 방식이었는데,
이러면 "소리 자체가 5초 동안 월드에 남아있다가 나중에 범위 안으로 걸어 들어온 유닛까지 뒤늦게
주워듣는" 문제가 있었다(07-A 7-3장의 "일반 소리 유효시간 5초"는 소리의 수명이 아니라 그 순간 감지한
인류 개인의 확인 행동 시작 유예시간이었음). 지금은 `EmitSound`가 호출된 그 자리에서 범위 스캔까지
끝내고, 그 순간 조건을 만족한 인류에게만 R3 `Subject`(`PropagationSystem.OnSoundPerceived`)로
통지한다 — 나중에 들어온 유닛은 그 사건 자체를 못 받는다. 5초 유예시간은 통지받은 인류 각자의
`PendingSound`에 대해 `UniTask.Delay` 타이머(`ExpireAfterDelay`)로 개별 적용된다(다른 시스템의 틱
호출 유무에 기대지 않는 명시적 만료). `UnitFunction.OnUpdate`의 반복 폴링(`TickSoundPerception`)은
삭제됐다. `EmitSound`의 범위 스캔은 `session.units` 전체 대신 `PropagationSystem`의
`(층, roomId)` → 인류 인덱스(`_humansByRoom`)만 훑는다 — `GameSession.RegisterUnitPos`/
`UnregisterUnitPos`(스폰/이동/사망 시 이미 호출되는 기존 유닛 그리드 관리 지점)가 이 인덱스도
함께 유지해줘서 별도 폴링이 필요 없다. 최종 거리 판정은 여전히 `Vector2Int.Distance` 좌표 수학
그대로다(공간 분할은 스캔 후보를 줄이는 역할이지 거리 계산 자체를 대체하지 않음).
반응은 기존 `AlertSearchState`를 확장해 재사용한다(`IsSoundResponse` 등 필드 추가 — 03문서가 이미
`IsDeathSearch`로 같은 방식을 썼던 전례를 따름). 전투 진입 시 위험도 2단계 이상 + 비근거리(>2칸)면
`TacticalBehaviorType.JoinCombatWait`(신규, Panic 바로 다음 우선순위)로 즉시 전투 대신 합류 대기를
거친다 — `CombatFSMState.GetPriority`가 진입점.
E_HIT_HEAVY_INDIRECT/E_MONSTER_KILL_INDIRECT 연결(2026-08-05)은 `PropagationSystem.
TryConfirmIndirectHit`(`TacticalFSMState.SoundAreaApproach`의 인지 판정 시점에서 호출)와
`PropagationSystem.OnMonsterCorpseDiscovered`(`UnitFunction.CastRay`의 몬스터 시체 발견 지점에서
호출)가 담당한다 — `HumanKnowledgeBase.RecordEventByKey`(target Unit 대신 스냅샷 키를 직접 받는
경로, `RecordEvent`가 이제 이 메서드의 얇은 래퍼)가 죽어서 Destroy된 몬스터도 안전하게 기록할 수
있게 한다. 검증을 돕는 임시 시각화(`PropagationDebugVisualizer` — 전파 범위 원 + `OnSoundPerceived`
구독 기반 소리 발생 위치 짧은 표시, `GameSession`이 `ThreatTileRenderer`와 동일하게 매 프레임 호출,
`DebugInfoPanel`의 "시야 표시" 토글과 동일한 방식으로 우측 상단 OnGUI 버튼으로 켜고 끔)도 이때 함께
추가했다 — 실제 플레이 검증이 끝나면 지워도 되는 임시 코드다.

### 다음에 이 시스템을 확장할 때

1. `시야인지반응_구현현황_2026-08-05.txt`(또는 그 이후 최신본)의 "07문서 장별 구현 현황" → "다음
   우선순위 구현 후보" 순으로 확인.
2. 08(리더·명령)/09(목표·경로)/06(전투반응·기습)/함정 시스템 문서가 생기면 그 문서가 담당하는 부분
   (물리적 전파 이동, 합류 5초 초과 처리, 전투 중 소리 기반 새 타겟 탐색, 함정 피해·흔적 가중치 수치)을
   연결 — 지금은 전부 CLAUDE.md 관례대로 스텁.

## 새 데모 — 코어/문/명령 체계 전면 개편 (기초문서.md 피드백, 2026-08-22)

### 배경

`Assets/문서/공식문서/송성현구본훈데모/기초문서.md`(플레이테스트 피드백 4개: 첫 웨이브 시간/
디펜스-오펜스 자연스럽게/모든 곳에 코어/문도 방어건물)를 반영해 게임 루프 핵심 3곳을 갈아엎었다.
구현현황은 `Assets/문서/구현현황/구현중/새데모_구현현황_2026-08-22.txt`(장별 상세 + "다음 우선순위"
섹션) — 이 시스템을 다시 손댈 때는 그 문서부터 확인할 것(기존 가중치/시야인지반응 문서와 동일 관례).

### 핵심 변경 요약

- **코어**: 예전엔 1층 보스방에 1개, HP 없이 "인류 리더가 조사해서 회수"하는 웨이브 목표물
  (방 점령과 무관)이었다. 지금은 **모든 방(야생+점령된 방 전부, 0층 제외)이 항상 코어를 보유**하고
  체력제다 — `GameSession.SpawnAllRoomCores`가 시작 시 배치, `Room.CoreObjectId`/`CorePosition`으로
  추적. 코어 체력이 0이 되면 막타친 유닛 진영으로 그 방 소유권이 즉시 전환되고(`OffenseProcessor.
  OnCoreDestroyed` — 이제 이 메서드가 방 점령의 유일한 진입점, 예전 유닛 전멸/빈방 입성 기반 로직은
  전부 제거) 코어는 사라지지 않고 반피로 회복돼 계속 뺏고 뺏기는 대상으로 남는다.

  **⚠️ 자동 오브젝트(코어/문) 공격은 인류 전용 — 플레이어 몬스터는 절대 자동으로 하지 않는다**
  (2026-08-22 하드 룰로 확정). 최초 구현 당시 `TacticalBehaviorType.CoreAttack`은 "인류/몬스터
  공통"이었는데, 사용자 신고("이동 명령중이고 앞에 막힌게 없는데도 문 앞에서 멈춤" — 2*2 통로에
  일부러 남겨둔 마지막 상대 진영 문 하나를 플레이어 몬스터가 이동 중 자기 판단으로 발견하고 파괴를
  시도하다 막힌 것으로 추정)로 사용자가 명시적으로 확정했다: "플레이어 측 몬스터는 절대, 스스로
  문이나 코어를 파괴하려 시도해서는 안돼. 반드시 플레이어의 명령으로만 시도 하게. 자동 오브젝트
  공격은 오직 인류만의 로직이야." `TacticalFSMState.FindHostileRoomCore`도 `FindHostileExitDoor`와
  동일하게 `if (!(unit is Human)) return false;`로 인류 전용이 됐다 — 플레이어 몬스터가 코어/문을
  부수려면 **반드시** `PlayerCommandFSMState.ExecutePlayerAttackObject`(플레이어 우클릭 명령)를
  거쳐야 하고, 스스로 판단해서 공격하는 경로는 존재하지 않는다. 이 시스템을 다시 손댈 때 절대
  플레이어 몬스터용 자동 코어/문 공격 조건을 추가하지 말 것 — 필요하면 반드시 사용자에게 먼저
  확인한다.

  코어 공격은 `TacticalFSMState`의 `TacticalBehaviorType.CoreAttack`(인류 전용, 위 하드 룰 참고) +
  `UnitFunction.OnUpdate`의 채널링 데미지로 이뤄진다. 채널링 데미지는 2026-08-22부터 공격자의
  physicalAttack 스탯과 무관한 **고정 초당 비율**이다(사용자 요청 "공격 시도중인 유닛 마리 수 당
  추가" — `GameSession.CoreAttackDamagePerSecond`/`DoorSystem.DoorAttackDamagePerSecond`, 둘 다
  자리표시자 20) — 채널링 중인 유닛마다 독립적으로 이 값을 적용하므로 별도의 "인원 수 세기" 없이
  동시 공격 인원수만큼 자연히 합산된다. 같은 블록에서 채널링 중엔 항상 대상(코어/문) 방향으로
  `currentDir`을 매 프레임 갱신하고 `Generate.UpdateUnitSpriteForDirection`도 짝지어 호출한다
  (사용자 요청 "코어 공격 중일때는 코어를 바라보면서" — 처음엔 `currentDir`만 갱신하고 스프라이트
  갱신 호출이 빠져있어서 "여전히 부자연스럽다"는 후속 신고가 있었다, 2026-08-22 수정 — 자동 AI/
  플레이어 명령 두 경로 모두 이 공유 블록을 거치므로 자동 적용). **인류 웨이브의 승리
  조건도 "목표 방(보스방) 코어 파괴"로 바뀌었다** — 예전 "루팅 오브젝트 운반-탈출"(`HumanWaveManager`
  의 `DummyTargetState`) 메커니즘은 완전히 제거.
- **문**: 자동 배치(모든 방, 야생 포함)는 그대로지만 2026-08-22에 두 차례(체력/파괴 → 진영 기반
  개폐) 전면 개편됐다. 문은 이제 자신이 물리적으로 속한 방의 `Room.RoomFaction`을 "보유 진영"으로
  삼고(`DoorSystem.GetDoorOwnerFaction`, 코어 파괴로 방 소유권이 바뀌면 즉시 반영), **기본은 항상
  닫힘**이다. 원안은 진영별 색상 틴트(`OffenseProcessor.GetRoomOwnerColor`)도 있었지만, 방 바닥과
  같은 색으로 칠해져 문이 안 보이는 문제로 2026-08-22 후속 피드백("문색깔과 방 진영색이 동일해서
  아예 안 보여")에서 제거했다 — 문은 항상 기본 색(흰색)이고, 소유 진영은 클릭 시 뜨는 정보 패널
  (`BuildingControlPanel.ShowForObject`, 아래 참고)로 확인한다. 개폐 트리거는 2026-08-22에
  한 번 더 재조정됐다(사용자 요청 "1칸 접근시 열리는 형식이 아닌, 문 인접 칸에서 문에 접근 시도시
  열리는 방식으로") — 처음엔 문 타일 반경 1(3x3) 안에 보유 진영 유닛이 "존재"하면 열렸지만, 지금은
  `UnitFunction.Move`가 인접 칸에서 문 타일로 "넘어가려는 시도"를 하는 그 순간(`DoorSystem.
  NotifyApproachAttempt`, 이동 성공 여부 무관)에만 열리고 시도가 끊기면 다음 프레임에 다시 닫힌다
  (`DoorSystem.UpdateProcess`가 매 프레임 그 표시를 비움). 이건 순수 코스메틱이고, **실제 통행 가능
  여부는 `DoorSystem.IsBlockedByClosedDoor`가 항상 "문의 소유 진영 == 이동하려는 유닛의 진영"으로만
  판정**한다
  (`UnitFunction.CanMove`/`AStarMovement.IsTileWalkable`이 직접 호출 — 문 타일은 더 이상
  `Tile.isStructureExist`를 건드리지 않는다). 다른 진영은 파괴(`InteractableObject.DoorHp`)해야만
  지나갈 수 있고, 파괴되면 재설치 전까지 아무나 통과 가능하며 재설치는
  `ObjectPlacementController.EnterDoorRepairMode`(원래 게이트 자리에만 가능)로만 할 수 있다. 옛
  "방 유닛 구성이 야생 제외 한 진영이 되면 영구히 열림" 규칙(`CloseAllDoorsForWaveStart`/
  `RefreshRoomGateStates`)은 완전히 폐기. **문 소유권은 방 점령과 분리**돼 있다(2026-08-22 후속
  피드백, 사용자 요청 "점령으로 인해서 문의 소유권을 바뀌지 않아. 부수고 다시 재설치하는게
  원칙임") — `InteractableObject.DoorOwnerFaction`(생성/재설치 시점에 고정)이 진실의 원천이고,
  `DoorSystem.GetDoorOwnerFaction`은 더 이상 `Room.RoomFaction`을 실시간 조회하지 않는다. 최초
  스폰(`SpawnDoors`)은 그 순간 방 소유 진영을 스냅샷하고, 파괴 후 재설치(`RebuildDoorAt`)는 항상
  `FactionType.Player`로 고정된다(재설치는 `ObjectPlacementController`를 통해 플레이어만
  실행하므로). 코어는 이 규칙의 예외다 — "코어 파괴 = 방 점령" 설계 자체가 방 소유권과 코어
  소유권을 하나로 묶어야 성립하므로 그대로 `Room.RoomFaction`을 따른다. 문 재설치는 원래 debug
  메뉴에 있었다가 "설치" 메뉴(유닛/자원 생산 건물·함정과 같은 줄)로 옮겨졌고, `ResourceManager.
  DoorRepairStoneCost`(자리표시자 100)를 소모하는 유료 설치로 바뀌었다(원래는 무료였다).
- **명령 체계**: R키 몬스터 배치 프리셋 모드(방 단위 배치, 진입 시 `Time.timeScale`을 거의 0으로
  낮춰 게임을 멈추던 것)를 통째로 제거했다(`MonsterPlacementController`/
  `MonsterDefensePlacementSystem`/`MusterFSMState` 파일 자체 삭제, `Unit.isMustered`/
  `defenseStartPosition` 필드 제거). 대신 기존 "명령 취소"/"집결 및 정지"와 동급인 **"제자리 공격"**
  토글이 추가됐다 — 이동은 절대 하지 않지만 사거리 내 적은 공격한다(`StandGroundAttackFSMState`,
  `HaltFSMState`와 동일하게 `UnitFSM.SelectState`가 강제 배정하는 고착 상태). "정지"와 동일하게
  위협 반응(회피/점멸)도 차단된다 — `UnitFunction.OnReactToThreat`이 `isHalted`와 함께
  `isStandGroundAttack`도 확인한다(2026-08-22, 처음엔 안 막았다가 사용자 신고 "제자리 공격중
  회피및 점멸 여전히 존재함"으로 "제자리에서 절대 이동하지 않는다"는 조건에 예외가 없다는 뜻임을
  확정) — 제자리 공격 중에는 회피/점멸로도 전혀 움직이지 않는다. 바라보는 방향은
  `CombatFSMState.ExecuteCombat`과 동일하게 매 틱 즉시 대상 방향으로 `currentDir`을 갱신한다 —
  한때 `IdleFSMState`와 동일한 2~4초 주기로 갱신을 늦추는 타이머(`Unit.standGroundNextFaceTime`)를
  뒀었지만(사용자 요청 "대기 상태와 동일한 바라보는 방향 전환 시간을 가져야 해"), 그러면 이미 있는
  `UnitFunction.ResolveVisionDirection`(01-A 11장 시야 방향 전환 우선순위 — 스킬 사용/인접 대상/
  소리 감지/경계 등 여러 후보 중 가장 급한 방향으로 매 틱 currentDir을 자동 갱신하고, 후보가 없으면
  "그 틱에 이미 세팅된 currentDir"을 최하위 폴백으로 유지하는 시스템, `GameSession.ProcessUnitAction`
  이 모든 유닛에 매 틱 자동 적용)의 폴백 값이 얼어붙어 오히려 부자연스러워졌다(사용자 재신고
  "자연스러운 시선 전환이 구현 안됨... 소리나 피격 등으로 인지해서 그 방향을 바라봐야 함") — 타이머를
  완전히 제거하고 매 틱 즉시 갱신으로 되돌렸다. 그 값이 `ResolveVisionDirection`의 폴백 후보가 되어
  "평소엔 공격 대상을 보고, 소리/피격 등 더 급한 일이 생기면 그쪽을 본다"가 별도 코드 없이 자동으로
  성립한다(2026-08-22). 추가로 **오브젝트(코어/
  문) 공격 명령**(2026-08-22 추가) — 유닛을 선택하고 코어/문을 우클릭하면(`Unit.
  playerAttackObjectTarget`) `PlayerCommandFSMState.ExecutePlayerAttackObject`가 접근~채널링을
  담당한다. 코어 자동 공격(`TacticalFSMState.CoreAttack`)과 이 채널링 필드(`Unit.
  currentAttackObjectTarget`, 코어 전용에서 코어+문 공용으로 일반화)를 공유한다 — 자동 AI(코어든
  문이든)는 인류 전용이고(위 "⚠️ 자동 오브젝트 공격은 인류 전용" 하드 룰 참고), 플레이어 몬스터는
  이 우클릭 명령으로만 코어/문을 공격할 수 있다. **인류 전용 "전술(문 공격)"**(2026-08-22
  추가, 사용자 요청 "인간쪽에만 적용되는 fsm인데, 방을 점령하고 난 다음, 다른 방으로 향하는 다른
  진영 문이 발견되었으면 공격하고, 탐험을 이어나가는 로직으로 바꿔줘") — `TacticalBehaviorType.
  DoorAttack`(CoreAttack과 정반대 방향: 자기 진영이 이미 점령한 방에 있을 때, 그 방 경계 게이트 중
  아직 다른 진영 소유인 문을 찾아 부순다, `TacticalFSMState.FindHostileExitDoor`). 문이 파괴되거나
  이미 아군 소유가 되면 조건이 자연히 꺼져 다음 우선순위(조사 등 기존 탐험 로직)로 넘어가므로
  "탐험 재개"용 별도 코드는 없다. 플레이어 몬스터/야생에는 적용되지 않는다. **2*2 통로처럼 문
  타일이 문턱 줄 전체를 차지하는 좁은 통로**에서는 아직 재설치 전인 "내 편" 경계 문이 체비셰프
  거리 1 이내 모든 칸을 막아버려 목표 문에 정확히 거리 1로는 영원히 못 붙는 경우가 있다(사용자
  신고 "접근은 하는데 채널링이 안 걸림", 2026-08-22) — `MoveToDoorAttack`이 `MoveTowardsPos`가
  더 가까워질 수 없다고 판단했을 때 반경 2 이내면 그 자리에서 채널링을 시작하는 폴백을 한때 추가했다.
  **2026-08-24에 이 반경 2 폴백을 완전히 제거했다** — 데미지 적용부(`UnitFunction.OnUpdate`)가 매
  프레임 반경 1만 인정하도록 바뀌면서(사용자 요청 "원거리에서 문이나 코어 파괴 안되도록... 반드시
  인접 1칸"), 반경 2에서 시작한 채널링이 데미지가 붙기도 전에 곧장 취소되고 매 틱 반경 2 조건이 다시
  성립해 시작→취소가 끝없이 반복되는 버그가 생겼다(사용자 신고 "마법사 같은 유닛들이 계속 코어나 문
  파괴를 원거리로 한다" — 회피/점멸과 무관하게 인류끼리만 있어도 재현). 채널링 시작 조건도 반경 1로
  통일했고, 반경 1까지 정말 못 붙는 좁은 통로 케이스는 `Unit.tacticalObjectAttackStuckTurns`(연속
  4틱 실패 시 경계 상태로 전환, `AIBehaviorConfig.tacticalObjectAttackStuckTurnLimit`)가 대신
  처리한다 — 얼어붙는 대신 경계로 전환했다가 알림 만료 후 재시도한다. 이 조사 과정에서 더 근본적인
  버그도
  드러났다(사용자 신고 "2*2 문을 부술때 멀리 있는 문부터 부수려고 시도하는데... 가까운 것부터
  부숴야 해") — `FindHostileExitDoor`가 게이트 문턱 두 줄(가까운 쪽/먼 쪽) 중 `GetGateDoorTiles`가
  반환하는 배열 순서상 "먼저 찾은 것"을 그냥 목표로 삼았는데, 그 순서는 기하학적("왼쪽/아래 청크가
  A") 규칙일 뿐 실제로 어느 쪽이 지금 방과 인접한 "가까운 쪽"인지와 무관했다. 이제는 모든 후보를
  모아 지금 위치에서 체비셰프 거리가 가장 가까운 것 하나만 고른다 — 가까운 문이 남아있는 한 항상
  먼저 선택되고, 그게 파괴돼 더 다가갈 수 있게 되면 그때 자연히 먼 쪽 문으로 넘어간다. 오브젝트
  공격 명령은 발행 시
  `Unit.isManualMoveCommand`를 `true`로 켠다(`RoomConfinedMovement`가 "플레이어 명령 중"으로만
  방 경계·문 타일 제한을 우회시켜주기 때문 — 2026-08-22 버그 수정: 이 플래그가 꺼져 있으면 플레이어
  몬스터가 자기 진영 문 타일조차 못 밟고 그 앞에서 멈췄다). `ExecutePlayerAttackObject`가 명령
  종료(대상 무효화) 시 다시 `false`로 되돌린다. 자기 진영 문은 공격 대상에서 제외된다
  (`PlayerCommandFSMState.IsPendingObjectAttackValid`가 문이 속한 방의 소유 진영과 유닛 진영이
  같으면 false — 2026-08-22 재조정, 사용자 요청 "자기 진영의 문은 우클릭시 공격 대상이 되면 안돼")
  — 이 경우 `InputManager.ExecuteRightClickCommand`가 자동으로 일반 이동 명령으로 넘어가 그 문
  위치로 이동해서 서는 동작이 된다(자기 진영 문은 항상 통과 가능하므로). **클릭 관례 전면 개편**(2026-08-22 추가,
  사용자 요청 "어떤 기능이든 좌클릭은 선택, 우클릭은 실행으로 두자. 설치나 명령 전반 모두 포함") —
  원래 좌클릭=선택+공격/우클릭=이동으로 나뉘어 있던 것("우클릭으로는 문을 공격할 수 없다"는 구조적
  불일치가 있었음)을 **좌클릭=오직 선택, 우클릭=모든 실행**(이동/유닛 공격/오브젝트 공격/배치 확정)
  으로 통일했다. `InputManager.DoClickSelect`는 이제 선택 전용이고, 신규 `ExecuteRightClickCommand`
  가 우클릭 시 적 유닛 공격 → 코어/문 공격(`PlayerCommandFSMState.IsPendingObjectAttackValid`, 자기
  진영 문은 대상에서 제외 — 이 경우 자동으로 일반 이동으로 넘어가 그 문 위치에 가서 선다) → 일반
  이동 순으로 판정한다. `BuildPlacementController`/`ObjectPlacementController`의 설치 확정 입력도
  `GameInputScheme.PrimaryDown`에서 `SecondaryDown`으로 옮겼다. 일반 이동 명령이 목표 지점 자체는
  안 붐비는데 가는 길 중간의 상대 진영 문에 막히면, 기존 "혼잡 판정" 휴리스틱(`FindNearbyOpenTile`/
  `HasAnyStructurallyOpenNeighbor`, 목표 지점 주변만 확인)이 "곧 풀릴 혼잡"으로 오판해 8틱 동안 문
  앞에서 얼어붙어 보이다 뒤늦게 포기했다(사용자 신고 2026-08-22 "이동 명령중이고 앞에 막힌게
  없는데도 문 앞에서 멈춤") — 이 증상에 대해 한때 이 절이 `PlayerCommandFSMState.
  IsBlockedByHostileDoorNearby`(인접 8칸 문 즉시 포기)를 적어 뒀지만 그 함수는 코드에 들어간 적이 없었다
  (검증 04-03, 2026-10-01 — git 이력상 CLAUDE.md 문구로만 존재). 실제 원인은 목표가 문 바로 뒤처럼
  유닛에서 2칸 이내일 때 `FindNearbyOpenTile`(정적 통행 가능성만 봄)이 닿을 수 없는 대체 칸을 계속 뽑고
  그때마다 막힘 카운터가 0으로 리셋돼 포기 조건에 영영 못 닿던 것이다. 지금은 `ExecutePlayerMove`가
  이동이 실패한 틱마다 카운터를 올리고(대체 칸 재지정은 리셋하지 않고 다음 틱 이동이 성공해야 0),
  `playerCommandStuckTurnLimit`(8틱)에 닿으면 대체 칸이 있어도 "이동 불가" 표시와 함께 포기한다
  (판정은 순수 함수 `MovementMath.ResolveMoveFailure`). 구조적으로 완전히 막힌 경우의 종료에도 같은
  표시가 붙는다. 이동 명령으로 상대 진영 문을 억지로 통과하는 기능 자체는 없다 — 지나가려면 오브젝트 공격
  명령으로 파괴해야 한다. **오브젝트 정보 조회**(2026-08-22
  추가, 사용자 요청 "모든 오브젝튼 이제 클릭을 통해 정보를 볼 수 있어(건물처럼)") — 코어/문/함정/
  전리품/시체/전멸흔적 등 모든 `InteractableObject`를 좌클릭하면 건물 클릭과 동일하게
  `BuildingControlPanel.ShowForObject`가 정보 패널(체력이 있으면 체력, 진영 소유 가능하면 소유
  진영, 기본 정보)을 띄운다 — 새 프리팹 없이 기존 `BuildingControlPanel`의 `_current`(건물)/
  `_currentObject`(오브젝트) 두 상태를 상호 배타로 관리해 겸용시켰다(에디터 실행 불가 환경이라 새
  프리팹 GUID를 안전하게 발급할 수 없어서 — `DoorSystem.GetDoorOwnerFaction`과 동일한 방식으로
  오브젝트가 속한 방의 `Room.RoomFaction`을 조회).

### 다음에 이 시스템을 확장할 때

1. `새데모_구현현황_2026-08-22.txt`(또는 그 이후 최신본) 맨 위 "다음 우선순위 구현 후보" 섹션부터
   확인 — 수치(코어/문 최대체력 등 전부 자리표시자) 튜닝, 웨이브 목표 방을 보스방 고정에서 WaveData
   설정 가능하게 확장하는 순서로 정리돼 있다. 자동 AI가 문도 스스로 공격하게 하려면
   `TacticalFSMState.CoreAttack`과 대칭인 `DoorAttack` 행동을 추가하면 된다(패턴은 이미 검증됨).
2. 문 공격 트리거가 정해지면 `TacticalFSMState`의 `HasCoreAttackTarget`/`MoveToCoreAttack`/
   `CoreAttackPerform` 3종 + `UnitFunction.OnUpdate`의 코어 채널링 블록을 그대로 본떠 `DoorAttack`
   계열로 만들면 된다(트랩 파괴 → 코어 공격에 이어 이미 두 번 검증된 채널링 패턴).
3. 새로 구현/연결한 게 있으면 구현현황 문서를 직접 갱신한다(가중치/시야인지반응 문서와 동일 관례 —
   대규모 갱신 시 새 날짜 파일 생성 + 이전 파일은 `구현현황/지난거/`로 이동).

## 스킬/공격 시스템 (아키타입 라우팅 / 투사체 / 광역 판정, 2026-08-23)

### 배경

"마법사 파이어볼이 근접공격처럼 나간다"는 신고에서 출발해 스킬 데이터 파이프라인과 공격 판정
기반을 손봤다. 구현현황은 `Assets/문서/구현현황/구현중/스킬시스템_구현현황_2026-08-23.txt`
(장별 상세 + "다음 우선순위" 섹션) — 이 시스템을 다시 손댈 때는 그 문서부터 확인할 것.

### 핵심 규칙

- **런타임 스킬의 진실의 원천은 skills.json이 아니라 프리팹이다**
  (`UnitSpriteManager.GetSkills` → `UnitVisualDefinition.BuildSkillActions`). JSON만 고치면 게임에
  전혀 반영되지 않는다 — 반드시 `Assets/Resources/Units/*.prefab`의 `skills` 블록까지 같이 고칠 것.
- **프리팹을 다시 뽑을 때는 `Tools/GrimArchive/JSON -> 유닛 프리팹 생성`
  (`JsonToUnitPrefabConverter`)만 쓴다.** 프리팹 컨텍스트 메뉴의
  `UnitVisualDefinition.LoadDataFromJson`은 `JsonUtility`로 `SkillData`를 직접 파싱하는데
  `projectilePrefab`/`hitEffectPrefab`이 `GameObject` 타입이라 JSON의 이름 문자열을 해석하지 못하고
  참조가 날아간다(컨버터 쪽은 `FindAssetByName`으로 정상 해석).
- `skillArchetype`이 비어있으면 `SkillAction_Generic`으로 폴백한다 — 몬스터 기본 스킬은 이게
  정상이지만, 값이 있는데 `switch`의 case에 없으면 `Debug.LogWarning`이 뜬다. 2026-08-23에 클래스
  프리팹 8종 전부가 이 필드를 잃어버린 채 전원 Generic으로 돌던 사고가 있었다.
- **스킬 타겟팅은 서로 독립적인 두 축이다**(`SkillTargeting.cs`, 2026-08-23 신설, 사용자 제안).
  `SkillOrigin`(판정을 어디에 만드는가 — `SelfArea`/`Projectile`/`TargetArea`)과
  `SkillAffinity`(누구를 대상으로 하는가 — `Enemy`/`Ally`)를 **한 enum으로 합치지 말 것.** 처음엔
  4분류 하나로 만들었다가 `Support`만 대상 진영이라 축이 섞여, "아군 좌표 기준 광역 힐" 같은 조합을
  표현할 수 없어 같은 날 다시 나눴다. 효과의 성격(피해/회복/버프/디버프)은 또 다른 축이므로 필요해
  지면 세 번째 enum을 나란히 두면 된다 — 기존 두 축에 끼워넣지 말 것.
- **새 스킬을 만들면 두 축을 반드시 확인할 것** — 기본값이 `SelfArea` + `Enemy`라, 아군 대상 스킬에서
  `Affinity`를 빠뜨리면 "적이 사거리에 있어야만 발동"하는 옛 버그가 그대로 재현된다.
- 대상 선정과 사거리 판정은 `SkillAction.ResolveTarget`(진영 축) → `CanExecuteAgainst`(판정 축)
  두 단계로 통합돼 있고, `CombatFSMState`/`StandGroundAttackFSMState`/`PlayerCommandFSMState` 세 곳이
  모두 이 순서로만 호출한다 — 게이트 로직을 세 곳에 복붙하던 예전 방식으로 되돌리지 말 것.
  `Execute`가 받는 `target`은 이제 "`ResolveTarget`이 돌려준 대상"이라 아군일 수도 있다.
- **`SkillAction` 인스턴스는 같은 유닛 타입 전체가 공유한다** — 스킬 안에 캐시나 상태를 두려면
  반드시 소유자(`unit`)를 키에 포함할 것(`SkillAction_Heal`의 1틱 대상 캐시가 그 예). 개인 상태는
  원칙적으로 유닛 쪽(`unit.CombatState.State` 등)에 저장한다.
- 파이어볼은 **투사체가 아니라 지정 좌표에 지연 착탄하는 메테오**다(2026-08-23 사용자 확정 —
  "유닛에게서 발사되는 형식이 아님 / 그 지점에 생성되는 방식임 / 좌표 선택 후 일정 시간이 지난 뒤에
  떨어지는 느낌이지, 공격하자마자 범위 판정과 공격이 동시에 일어나면 안 됨"). 흐름은
  `SkillAction_GroundAoE`가 담당한다 — 시전 순간 대상 중심 좌표를 클로저에 캡처해 굳히고(대상이
  이동해도 따라가지 않는다, 즉 회피 가능) 그 자리에 위협 타일을 띄운 뒤, `baseDelayMs`(파이어볼
  1500ms)가 지나야 범위 판정과 피해가 실행된다. `SkillAction_Fireball`은 그 위의 얇은 상속이다.
  한때 투사체 방식으로 구현했다가 위 정정으로 전부 되돌렸으니, 다시 투사체로 바꾸지 말 것.
- **시전 상태(`castTimer` 등)는 `OnThreatCreated` 통지보다 먼저 세팅해야 한다**
  (`SkillAction.BeginAttackCast`). 위협 타일이 화면에 떠 있는 시간을 `GameSession`의 구독부가
  공격자의 `castTimer`에서 읽어가기 때문이다 — 순서가 반대면 항상 0.5초 폴백이 쓰여, 시전이 긴
  스킬은 예고가 착탄 한참 전에 사라진다(2026-08-23 수정).
- **위협 타일은 "예고"와 "잔상" 두 종류다**(`ThreatTileRenderer`의 `holdUntilImpact`, 2026-08-23
  사용자 요청 "공격범위가 파티클 생성타이밍부터 피해순간까지로 남아있게"). 시전이 있는 공격
  (`castTimer > 0`)은 예고라서 색이 옅어지지 않고 그대로 있다가 **피해가 들어가는 순간 사라지고**,
  시전 없는 즉발 공격은 표시 시점에 이미 피해가 끝나 있으므로 종전대로 0.5초에 걸쳐 페이드된다.
  구분은 `GameSession`의 `OnThreatCreated` 구독부가 자동으로 하므로 스킬마다 지정할 필요가 없다.
- `Hitbox.CalculateOverlapRatio`의 분모는 **두 히트박스 중 작은 쪽 면적**이다(2026-08-23 수정).
  예전처럼 공격 히트박스 면적으로 나누면 범위가 넓을수록 데미지가 깎이고(3x3이면 1/9), 대상 면적
  으로만 나누면 대형 유닛이 작은 공격에 덜 맞는 정반대 문제가 생긴다. 이 값을 다시 손대려면 두
  경우를 모두 확인할 것.

### 다음에 이 시스템을 확장할 때

1. `스킬시스템_구현현황_2026-08-23.txt`(또는 그 이후 최신본) 맨 위 "다음 우선순위 구현 후보"부터
   확인 — 파이어볼 착탄 지연(1500ms 자리표시자) 튜닝, 낙하 오브젝트 연출, 광역기 데미지 재튜닝,
   `SkillData.damageType` 필드 신설(지금은 물리/마법이 아키타입별 하드코딩) 순서로 정리돼 있다.
2. 새로 구현/연결한 게 있으면 그 문서를 직접 갱신한다(다른 구현현황 문서와 동일 관례).

## 점유 충돌 시스템 — 대기 vs 우회 / 비켜 주기 / 좁은 통로 통과 순서 (검증 04-05~04-07, 2026-10-01)

### 배경
행동경로·목표결정 04번 문서 5~8장을 구현했다(검증 기록: `Assets/문서/구현현황/구현중/행동경로목표결정_검증_2026-09-25.txt`의 04-05~04-07, 계획 승인 후 구현). 예전엔 점유 타일이 A*에서 벽이라 다음 걸음이
점유되면 우회가 아무리 길어도 곧바로 우회했고, 우회가 없으면 행동마다 따로 있는 정체 인내(4~8틱)가 포기로 끌고 갔다.

### 구조 (이걸 다시 손댈 때 먼저 볼 것)
- **진입점은 두 곳뿐이다.** ① `AIMovementHelper.MoveTowardsPos` 맨 앞의 `OccupancySystem.TryHold(unit, dest)` — true면 "이번 주기는 대기(또는 비켜서기)로 썼다"(이동 안 함)로 true 반환, false면 기존 동작. ②
  `GameSession.ProcessUnitAction`의 `JudgeState()` 뒤·`ExecuteAction()` 앞 `OccupancySystem.TryHonorYield(u)` → `ShouldHold(u)` — 대기 중이면 **이번 행동 주기의 ExecuteAction만 건너뛴다**(FSM 전환은 그대로). 새 이동 호출부를 만들 때
  TryHold를 따로 부를 필요는 없다(MoveTowardsPos를 거치면 자동).
- **대기 = 행동 틱 스킵.** 대기 중엔 각 행동의 리프가 호출되지 않아 정체 카운터가 돌지 않고, 대기 시작 때 `Unit.ResetNonCombatStuckCounters()`가 비전투 정체 카운터(조사·탐색·함정 접근·코어/문 공격 접근·플레이어 이동 명령·경계 접근·Human 대기)를 0으로
  되돌린다 — **새 "정체 카운터"를 추가하면 이 메서드(Unit/Human override)에도 넣을 것**, 안 그러면 정당한 대기가 포기로 이어진다.
- **판단 로직은 `OccupancyMath`(순수, 하니스로 실행 가능)에, 세션·유닛 상태를 읽는 부분은 `OccupancySystem`에 있다.** 점유자 분류(이동 중·상호작용 중(남은 시간)·교전/대기 중·제자리 대기) → 대기 예상시간 → 구조 경로(점유 무시, `AStarMovement.IgnoreAllUnits`
  쌍둥이 — `CreateStructuralTwin`이 종류별로 복제, 방 제한/공격 범위 회피 알고리즘 포함) 길이와 실제 우회 길이 비교. **`IgnoreAllUnits`는 구조 경로 조회용 쌍둥이에만 켠다 — 실제 이동 인스턴스에 켜면 Move()가 점유 타일을 거부해 유닛이 얼어붙는다.**
- **적용 범위는 비전투 이동뿐**(사용자 확정). 적 인지·피격·시전·Combat 상태·던전 입구 시퀀스 중에는 TryHold가 즉시 false라 예전 "점유=벽" 즉시 우회 그대로다. 전투 추격 쪽에 이 시스템을 넓히려면 `combatChaseStuckTurns` 등 자체 정체 처리와의 상호작용부터 검증할 것.
- **kill-switch**: `AIBehaviorConfig.occupancyArbitrationEnabled`(false면 전체가 예전 동작), `gatePassOrderEnabled`(통과 순서·마주 막힘만). 이 시스템이 원인으로 의심되는 이동 이상은 먼저 이 스위치를 꺼서 확인한다.
- **좁은 통로 = 게이트 문턱 타일**(`DoorSystem.TryGetGateKeyAt`, `Floor.gates` 기반이라 문이 파괴돼도 성립). 통과 우선순위는 역할(전방 근접 → 근접 지원 → 원거리 공격 → 원거리 지원) → HP 비율 → `Unit.PassTieBreak`(유닛당 한 번 뽑아 유지 — 매번 재추첨하면 순서가 흔들린다) → Id.
- 6초(`occupancyWaitMaxSeconds`) 넘게 시간을 모르고 우회도 없는 대기는 포기하고 기존 정체 인내로 넘긴다(교착 방지) — 시간을 아는 대기(상호작용 남은 시간)는 끊지 않는다.

### 알려진 한계 / 미검증
Unity 실제 플레이 검증을 아직 하지 않았다(순수 판정만 하니스로 실행). `currentWait`가 있는 Human·플레이어 명령 중 유닛은 비켜 주기를 하지 않으며, 게이트 경쟁 판정은 상대의 캐시된 경로 앞 3~4칸에 의존하는 근사다. 플레이 확인 체크리스트는 검증문서 04-05 항목에 있다.

## 경로 지식 = 개인 지도 / 경로 평가 — 전체 길 미확인·통행 불가 구분 (검증 04-08, 2026-10-01)

### 구조 (이걸 다시 손댈 때 먼저 볼 것)
- **A*는 진영 공용 `discoveredMap`이 아니라 유닛이 아는 지형을 읽는다**(`IKnownTerrain` — 인류 `personalMap`, 몬스터 `monsterMap`; `AStarMovement.IsKnownWall`, 모든 탐색 진입점이 `BeginSearch`로 지식 소스를 정함). 공용 `discoveredMap`은 맵 크기·null 폴백과
  kill-switch(`AIBehaviorConfig.personalMapPathingEnabled=false`) 폴백용으로만 남았고 **쓰기는 전부 그대로**다(건물/코어 동기화 포함). 미확인(0)은 **계획에서만 낙관적으로 통행 가능**으로 보고 지도에는 기록하지 않는다 — 실제 이동은 걸음마다 `CanMove`가 판정한다.
- **조사 후보 선정은 `RouteAssessment.Assess`로 "닿을 수 있는가 + 거리"를 판정한다**(`RouteMath` 순수 + `RouteContext`): FullyKnown / PartlyUnknown(문서 9장 공식 = 프론티어까지 실제 걸음 수 + 프론티어→목적지 체비셰프의 최솟값, `AStarMovement.TryComputeFrontierSteps` BFS) /
  TrapBlocked(**후보 유지** — 제외하면 접근 시도에서 03-13 막힘 신호가 안 나와 함정 대응 재개가 끊긴다) / Unreachable(점유 무시 구조 쌍둥이·함정 끈 쌍둥이로도 길 없음 → 후보 제외). 도달 기준은 목적지 **인접 1칸**(`acceptRange`, 조사의 실제 도착 조건 —
  목표 타일이 서 있을 수 없는 칸이어도 닿는다). 함정 목표(`targetIsTrap`)는 정확한 타일까지이고, 통행 불가여도 직선거리로 근사한다(큰 값이면 `TrapPartySystem.BeginWaitingFor`의 대기 기한이 비정상적으로 길어짐).
- **통행 불가 판정 메모**(`Unit.unreachableRouteMemo`)는 성능용 캐시일 뿐 행동 상태가 아니다 — 서명(층·`TerrainRevision`·아는 함정 수·`DoorSystem.StateVersion`)이 같으면 유효, 바뀌어도 판정 직후 `routeRecheckMinSeconds`(2초) 안엔 유효. **새 "길에 영향을 주는 정보"가 생기면 서명에 넣을 것.**
- 새 벽을 알게 되면 `IMovementAlgorithm.OnTileBecameWall`(← `UnitFunction.ProcessTile`)이 그 타일이 캐시 경로 위일 때만 이동 캐시를 지운다 — 전역 지도 개정으로 무효화하면 탐험 중 매 프레임 지워져 A* 폭주 방지 캐시가 무의미해진다.
- **탐험이 닿지 못한 미탐색 목표는 지형을 벽으로 위조하지 않고 "막힘 기록"으로 남긴다**(2026-10-02, 검증 04-08 발견 5, 04번 9장 "막힘 기록과 재시도 조건"): `NavigationFSMState.RandomExplore`가 A*로 목표에 한 걸음도 못 다가가면 `RouteAssessment.MarkExploreBlocked`가 `Unit.exploreBlockedTargets`에 (서명, 시각)을 남기고, `RouteAssessment.CreateExploreBlockFilter`가 만든 필터를 프론티어 선택(`PersonalMapKnowledge.TryGetNearestFrontierTile[InBounds]`의 `exclude`)과 몬스터 BFS가 쓴다. 서명·유효 규칙은 조사 후보의 통행 불가 메모와 같다(`RouteMath.IsUnreachableMemoValid`). **리더의 막힘 기록 필터는 `Party.IsRoomActivityComplete`의 "방 안 프론티어 없음" 근사에도 넘어간다** — 예전엔 벽 위조가 그 프론티어를 지도에서 지워 같은 효과였고, 위조만 빼면 닿을 수 없는 프론티어가 Explore 파티의 방 탐색 완료를 영구히 붙들기 때문이다(문서 규정이 아니라 예전 동작 보존용 근사). 이 필터를 빼고 프론티어를 세는 새 코드를 만들지 말 것. 점유는 서명에 없어 점유만 원인이면 아는 정보가 바뀔 때까지 건너뛴다.
- kill-switch: `personalMapPathingEnabled`, `unreachableCandidateExclusionEnabled`, `exploreBlockedRecordEnabled`(끄면 탐험이 예전처럼 닿지 못한 타일을 벽으로 기록). 이동 이상이 의심되면 먼저 이것들로 확인한다.

### 알려진 한계 / 미검증
Unity 실제 플레이 검증 안 함(실제 A*·RouteAssessment 코드를 스크래치패드 하니스로 44개 단정 실행, `Assets/Tests/RouteAssessmentTests.cs` 17건은 Test Runner 미실행). 문 상태·점유는 여전히 실시간 실제값을 읽는다(개인 지식화 미착수). "통행 불가 제외"는 **조사 후보 선정에만** 적용 —
탐험·문/코어 접근·함정 접근의 포기 후 같은 대상 재선택은 그대로다(탐험의 닿을 수 없는 프론티어는 위 "탐험 막힘 기록"이 처리). 몬스터도 개인 지도 기반이 돼서, 방 제한 몬스터가 플레이어 명령으로 안 가본 방을 지날 때 미확인을 낙관 가정으로 걷는다.

## 집결 → 문 앞 진형 → 문 파괴 → 순차 입장 (플레이 로그 분석, 2026-10-01)

### 구조 (이걸 다시 손댈 때 먼저 볼 것)
- **집결은 "전원이 자기 자리에 설 때까지 대기"다.** 파티원마다 집결지(리더 자리) 근처 **개별 자리**(`Party.PickGatherSlot` — 같은 방·점진 확장)를 받고, 도착해도 대기를 유지한다. 완료는 도착한 유닛이 아니라 `Party.CheckRallyComplete`가 **위치로 전원 도착을 매번 재확인**해 일괄 해제한다(전투로 밀려난 유닛 대응). 예전의 "4틱 정체 = 도착 간주"는 폐기(kill-switch `rallyGatherNearbyEnabled`).
- **집결 완료 뒤 방 이동은 `Party.AdvancePlan`(FormingUp → Breaching → Entering)이 소유한다.** 파일 분담(`Assets/Script/Unit/Party/`): `PartyAdvancePlan`(데이터) / `PartyAdvanceSystem`(공개 API `Begin`·`Tick`·`Abort`, 단계 전이, 진형·입장 자리 배정, 종료) / `PartyBreachCommand`(돌파 단계의 리더 파괴 지시) / `PartyAdvanceSteps`(유닛 한 틱 + 공통 `SlotSeek`) / `PartyDiagnostics`(로그 문자열) / `PartyFormationMath`(순수 계산) / `Party.Rally.cs`(집결 시작·완료·쿨다운·진단 — `Party`는 partial). `HumanWaveManager.UpdatePartyDestination`이 `Begin`/`Tick`을 부르고 유닛은 `WaitReason.FormingUpAtDoor/BreachingDoor/EnteringNextRoom`으로 `TacticalFSMState.ExecuteWait`→`PartyAdvanceSteps.StepMember`를 탄다. 코어 처리 경로(`hasPendingCore`)·`Begin`이 false(게이트·파티원 없음)·`partyAdvanceFormationEnabled=false`만 예전 `AdvancingToNextRoom`(`IssueCoMovementAdvance`).
- **진형 규칙은 0층 입구 진형(`DungeonEntranceSystem`)과 한 정의를 공유한다**(`PartyFormationMath` — 근접 0랭크·리더 1랭크·원거리 2랭크, 랭크 간격 2). 한쪽만 바꾸지 말 것.
- **문 파괴는 리더 지시로만 시작하며 기존 `DoorAttack`(인류가 점령한 방 안에서만 발동)과 별개다**(사용자 확정). 현재 방 소유 진영과 무관하게 0랭크가 인접 1칸에서 채널링한다. 단 진형 **대기** 자리는 문서(05번 3장)대로 문 앞 통과 구간(문 타일에서 체비셰프 거리 2 미만, `PartyFormationMath.DoorClearance`)을 비우고, 파괴 단계(`StepBreach`)에서만 문 인접 1칸으로 접근한다(2026-10-01 사용자 결정). 진입로 확보 = **near·far 두 행(방 쪽 문 오브젝트 줄)이 모두 `DoorSystem.IsBlockedByClosedDoor`로 통행 가능**(`PartyBreachCommand.ResolveRow`/`IsPassable`, 레인 개념은 문이 1×2 묶음이 되며 폐기). 기존 `DoorAttack`/`FindHostileExitDoor`를 이 흐름에 맞춰 고치려는 제안이 나오면 사용자 확정 사항이니 먼저 확인할 것.
- **돌파는 "자리가 남는 만큼 최대한 많은 인원"이 리더 지시로 달라붙는다**(사용자 확정 2026-10-01). `PartyBreachCommand.Assign`이 목표 행 문 타일에 인접 1칸인 서 있을 수 있는 칸을 전부 공격 자리로 모으고(`PartyFormationMath.AttackSlotsAround`) 우선순위(근접 → 원거리 → 리더) 단계별로 같은 단계 안에서는 "전체에서 가장 가까운 (멤버, 자리) 쌍"부터 자리를 준다(`AssignNearest` — 2026-10-02: 멤버를 입력 순서로 하나씩 처리하면 자리 정면에 선 멤버가 아니라 옆·대각선 멤버가 자리를 받아 좁은 문 앞에서 모서리 규칙으로 대각선 진입이 막히고, 정면에 남은 멤버가 길을 막아 far 문을 한 명만 치던 정체가 났다. 그리디 뒤에 자리를 못 받은 멤버를 늘리는 증가 경로 보강이 있어 `isAllowed`로 막힌 자리 때문에 "모두 받을 수 있는데 한 명이 남는" 배정은 생기지 않고, 앞 단계가 차지한 자리는 건드리지 않는다). 받은 자리가 유효하면 유지하고, 재지시는 행 변경(near → far)·자리 포기(`WaitState.RejectedSlots`, 3초)·3초 주기에 일어난다. 자리를 못 받은 인원은 진형 자리에서 대기한다. 채널링 데미지는 인원 × 고정 초당 값이라 모일수록 빨라진다. **공격 자리 수가 곧 동시 투입 상한**이라 좁은 통로에서는 나머지가 대기한다(의도). **자리 후보는 문 줄 "자기 쪽"만이다**(`AttackSlotsAround`의 `forward` — `plan.Forward` 방향 너머 칸은 뺀다, 2026-10-02): `CanMove`는 칸 하나만 봐서 닫힌 far 문 너머 칸도 "설 수 있다"고 답하므로, 빼지 않으면 far 단계 자리 6개 중 4개가 닿을 수 없는 문 너머였고 그 자리를 받은 인원(우선순위가 낮은 리더 포함)이 3초마다 포기·재배정을 반복하며 낙관적 길찾기로 돌아가는 길로 헤맸다.
- 안전장치(전부 기존 `doorApproachMaxBlockedSeconds` 기반, 내부 판단): 개별 30s 정체 → 그 유닛만 포기(`IsParked`), 문 파괴 외 단계 60s 초과 → 못 선 인원을 현재 위치에서 인정하고 진행, 목표 행 문 체력 30s 무진행 → **진형 복귀 후 재돌파**(아래 중단 정책). 코어 확인·퇴각 전환 시 계획 폐기.
- **교전·경계 중에는 위 시간 제한이 멈춘다**(2026-10-01 문서 일치화, 03번 13항 804줄·05번 2장 161줄: 전투 후 10초 경계를 마친 뒤 기존 집결·이동 재개). `PartyEngagement.IsEngaged`(전투 상태·경계·전투 합류 대기·인지한 적)인 계획 구성원이 한 명이라도 있으면 `PartyAdvanceSystem.ApplyEngagementPause`가 경과 시간만큼 `PhaseStartTime`·`LastProgressTime`·`RetryNotBefore`를 뒤로 밀고, 집결은 `Party.ApplyRallyEngagementPause`가 `RallyStartTime`을 민다. 멈춰 주는 시간 상한은 단계(집결)마다 `PartyEngagement.PauseCap`(= 4 × `doorApproachMaxBlockedSeconds` = 120초, 사용자 확정)이고 닿으면 시간이 다시 흐른다. 유닛 개별 30s 시계는 `SlotSeek.Step`이 이전 Step과의 간격(`WaitState.LastStepTime`)이 `max(3초, 3 × 걸음 시간)`을 넘으면 새로 시작한다(교전·경계·함정 대응·점유 대기로 대기 로직이 안 돈 시간을 세지 않음). Panic·함정 대응은 정지 신호에 넣지 않았다(개별 간격 리셋과 단계 상한이 받음).
- **자리를 못 잡는 유닛은 "구역 안이면 선 자리에서 인정"한다**(2026-10-01 좁은 길 정체 수정). 집결·진형·입장 자리 재선택은 ① 막혀 포기한 자리를 `WaitState.RejectedSlots`에 기억해 다시 고르지 않고 ② 집결지가 아니라 **이 유닛에서 가장 가까운** 구역 안 빈 타일(지금 다른 유닛이 서 있는 타일 제외)을 고르며 ③ 자기 타일이 뽑히면 그 자리에서 대기/집결 처리한다. 구역 = 자리 배정에 실제로 쓴 반경(`Party.RallyZoneRadius`, `PartyAdvancePlan.FormRadius/EntryRadius`, `PartyFormationMath.ZoneRadius/IsWithinZone`). 예전엔 "집결지에서 가까운 빈 자리"를 매번 새로 골라 좁은 길 양 끝을 무한히 오갔고, 자리 교체가 막힘 시계를 리셋해 안전장치도 못 걸렸다. 집결은 시작 60초(`doorApproachMaxBlockedSeconds`×2) 뒤 남은 파티원을 현재 위치에서 인정한다. **이 "구역 안이면 선 자리에서 인정"과 60초는 공식문서에 없는 근사다**(문서는 미집결자 → 대기·수색 → 포기 시 대기 대상 제외이고 시간은 후속 "정신력·도주·후퇴" 문서가 정한다 — 사용자가 2026-10-01에 현재 방식 유지를 확정). 문서 규정처럼 설명하지 말 것.
- **입장은 "문을 지난 유닛부터 즉시 개인 행동"이다**(사용자 확정 2026-10-01 "다음 방 진입 판정 후 바로 개인 행동"). `Entering`에서 **04번 8장 4단계 역할 순**(전방 근접 0 → 근접 지원 1 → 원거리 공격 2 → 원거리 지원 3, `plan.EntryTiers` = `OccupancyMath.EntryRank`, 리더도 자기 역할 단계 — 진형·입장 *자리 기하*(`plan.Ranks` 3랭크)와는 따로다)으로 출발하되 각 유닛은 문 먼 쪽 줄을 지난 순간(`PartyFormationMath.HasPassedGate`) `PartyAdvanceSystem.ReleaseIfEntered`가 그 유닛의 대기를 풀고, 전원이 풀리거나 포기하면 계획이 끝난다. **출발은 유닛별 간격이다**(2026-10-02, 사용자 확정 "한 유닛씩 순서대로는 좋은데 템포가 너무 느리다" → "유닛별 간격 출발"): `PartyAdvanceSystem.ReleaseNextInOrder`가 아직 출발하지 않은 유닛 중 통과 우선순위(`OccupancySystem.PassKeyOf` — 문턱 중재가 쓰는 같은 키: 역할 → HP 비율 → 유지되는 무작위 → Id)가 가장 높은 한 명을 `AIBehaviorConfig.entryReleaseIntervalSeconds`(기본 0.5초)마다 출발시킨다. 예전 단계 장벽(앞 역할 단계 전원이 문을 *완전히* 지나야 다음 단계 출발, 단계마다 뒤쪽 진형 자리에서 걸어와 빈 시간이 쌓임)은 04번 8장이 요구한 적 없는 이중 강제였고(문서는 좁은 통로의 통과 순서만 정하며 실시간 순서는 `OccupancySystem.TryGateOrderHold`가 이미 같은 키로 중재), `entryReleaseIntervalSeconds ≤ 0`이면 `ReleaseByRoleTier`로 그대로 남아 있다. 앞 유닛이 막혀도 시계가 흘러 뒤 유닛이 영구히 붙들리지 않는다. **입장 중인 유닛은 앞 유닛에 막혀도 돌아가지 않고 기다린다**(2026-10-02, 사용자 요청 "대기하다 입장할 때 유닛에 막혀서 뱅 돌아가는 현상 막아줘 — 앞 유닛이 완전히 들어간 후 들어가도록"): 실제 A*는 점유를 벽으로 보므로 문 통로를 앞 유닛이 막으면 다른 문·방을 거치는 긴 우회를 골랐다(`TryHold`는 구조 경로의 *바로 다음 걸음*만 보므로 앞 유닛이 아직 멀면 우회를 막지 못했다). `OccupancySystem.TryEntryStep`(`TryHold` 맨 앞, `PartyAdvanceSystem.EnteringPlanOf`가 대상 판별 = 출발 허가를 받고 문 먼 쪽 줄을 아직 못 지난 유닛)이 구조 경로(점유 무시)의 다음 걸음을 직접 내딛고, 그 걸음이 같은 진영 유닛에 막혔을 때 막은 유닛이 문 가까운 줄 이후(`PartyFormationMath.IsAtOrBeyondRow` — 통로 안·다음 방 쪽)에 있으면 우회 없이 점유 대기 hold로 기다린다(제자리 아군이면 비켜 달라고 요청, 문턱 통과 순서 `TryGateOrderHold`는 먼저 확인). 막은 유닛이 진형 쪽(문 앞)이면 짧은 비켜 가기로 충분해 기존 판단(대기 vs 우회)에 맡긴다. 6초 미확인 대기 포기가 없고 영구 대기는 입장 단계 상한(60초, `ParkStragglersAtLimit`)이 끊는다. 04번 6장의 "예상 시간 비교 후 대기/우회 선택"을 입장 중에는 "항상 대기"로 바꾼 사용자 결정이다. kill-switch `entryWaitForUnitAheadEnabled`. 다음 방 안쪽 자리에 전원이 모일 때까지 붙들지 않는다(입장 자리는 문을 지나는 방향을 정하는 목적지일 뿐). **입장 이동의 도착 반경은 0이고(`StepGoToSlot`의 `entering`), 입장 단계의 `IsMemberDone`은 대기가 남은 유닛을 항상 "미완료"로 본다**(2026-10-02 플레이 로그) — 입장 자리가 문 먼 쪽 줄 바로 다음 칸(맨 뒤 랭크, 아는 타일이 문 근처뿐일 때)이면 반경 1에서 문 위에 서서 "도착"하고, 통과 전이라 뒤 역할 단계가 영구히 막혔으며 단계 상한·진단 로그도 그 유닛을 "끝낸 것"으로 건너뛰었다.
- **진형·입장 단계는 상한(60초)에서 계획을 중단하지 않고 못 선 인원을 현재 위치에서 인정해 진행한다**(집결과 같은 규칙, 2026-10-01 3차 로그). **돌파 실패(목표 행 문 체력 30초 무진행)는 계획을 중단하지 않는다**(2026-10-01 문서 일치화, 사용자 확정 "진형 복귀 후 재돌파" — 05번 1장 71·73줄: 집결 후에는 흩어지지 않고 진형을 유지) — `PartyAdvanceSystem.RegroupAfterStall`이 같은 계획을 `FormingUp`으로 되돌려 전원이 진형 자리로 물러나게 하고, `RetryNotBefore`(30초 쿨다운)가 지난 뒤 `TickFormingUp`이 다시 `Breaching`을 시작해 리더가 공격 자리를 새로 지시한다(`plan.BreachCycles`가 횟수, 매 사이클 로그). 못 나아갈 때의 계속/후퇴 판단은 후속 리더·후퇴 문서 몫이라 그 전까지는 진형을 유지한 채 재시도를 반복한다. 예전의 "2회째 진형 없이 직행 돌파"(`plan.Direct`)와 "한도 소진 시 개인 행동 복귀 + 쿨다운 재집결"은 문서와 어긋나 폐기했다. `Abort(retry=true)`는 **비정상 중단**(진행할 파티원 없음 등)만 맡는다 — 시도(`Party.AdvanceAttempts`)가 `PartyAdvanceSystem.MaxAttempts`(2) 미만이면 `ReadyToAdvance`를 되살려 다시 지시하고, 한도에 닿으면 `Party.GiveUpAdvance`로 재집결 잠금(`AdvanceFromRoom`)·시도 횟수를 풀고 쿨다운(`RallyBlockedUntil`, 30초) 뒤 집결부터 다시 한다(Begin ↔ Abort 무한 재시작 방지용 안전밸브 — 중단 뒤 `ReadyToAdvance`가 소비된 채 `AdvanceFromRoom`만 남아 파티가 영구 정지하던 문제, 로그 101~107줄). 예전 `AdvancingToNextRoom` 폴백은 문이 적 소유 방에서 `DoorAttack`이 발동하지 않아 문이 안 부서져 쓰지 않는다. 리더 없음·퇴각은 `retry=false`.
- **리더 명령의 전달 범위는 한 규칙이다**(05번 4장, 검증 05-03): `Party.IsReachedByLeaderCommand(리더, 구성원)` — 같은 방이면 거리와 무관하게(방이 있고 같은 방이어야 함, 방 없는 유닛끼리는 같은 방이 아님 — `PartyFormationMath.IsSameRoomForCommand`), 다른 방이면 `PropagationSystem.CanPropagate`를 통과해야 한다. 집결 명령·코어 처리 집결 해제·방 이동 지시(`PartyAdvanceSystem.CollectMembers`와 예전 공동 이동 `HumanWaveManager.IssueCoMovementAdvance`)가 모두 이 헬퍼를 쓴다 — 방 이동 지시는 문서 4장 표가 범위를 명시하지 않아 집결 명령의 연장으로 해석했다. 못 받은 구성원은 개인 행동으로 남고 코어 처리 경로는 매 틱 재발행돼 범위에 들어오면 받는다. 집결 해제를 못 받은 구성원은 `Party.TickPendingRallyRelease`가 같은 방이 되거나 전파 범위에 들어올 때 해제를 전달한다(대기 객체를 기억해 그 사이 바뀐 대기는 건드리지 않음).
- **집결 명령·방 이동 계획 편입·공동 이동 대기 배정을 받으면 상호작용을 시작하기 전 대상으로 이동 중이던 조사는 접는다**(05번 4장 245줄·03번 1장 181줄, 검증 05-04): `Human.ReleaseUnstartedInvestigation()` — `currentInvestigation`은 대상 "선택" 순간부터 채워져 그 유무로는 시작 여부를 알 수 없으므로 `PartyFormationMath.IsInvestigationStarted(PenaltyActive, Progress01)`로 가른다(진행 중이거나 진행도가 남은 중단된 조사는 시작한 것 — 기존 유지·중단 조건대로 끝내고 합류). 접힌 대상은 개인 지도에 남아 나중에 다시 후보가 된다. 집결 자리(`PickGatherSlot`)도 문 앞 통과 구간(`AIMovementHelper.IsWithinDoorClearance`)을 비운다.
- **발견한 문은 파티가 전파하는 정보다**(05번 7장 408·454~456줄, 검증 05-06): `UnitFunction.CastRay`가 문 타일을 시야로 확인하면 `Party.KnownDoorObjects`(문 Id → 위치)에 올리고, `PropagationSystem.TickOngoingObjectPropagation`의 `PropagateKnownDoors`가 코어·함정과 같은 지속 재전파로 아직 모르는 파티원에게 `CanPropagate`가 성립할 때 개인 지도에 등록한다. 리더의 다음 이동 문 후보(`AIMovementHelper.TryFindKnownDoorInCurrentRoom`)는 `PropagationSystem.KnowsDoor`(오브젝트로 등록했거나 문 타일을 직접 확인)로 "아는 문"을 판정하므로 구성원이 본 문을 리더가 쓸 수 있다. 알게 되는 것은 개인 지도 등록뿐이고 방 이동 명령은 리더 결정이다. 파괴된 문은 `objectGrid`에서 사라지므로 다음 이동 후보에서 빠진다(관찰 — 방 경로 선정은 후속 리더·명령 문서 몫).
- **코어 때문에 집결을 해제한 파티는 코어 처리 뒤 곧바로 다시 집결한다**(05번 2장 165~172줄 표 첫 행, 검증 05-02): `Party.OnLeaderLearnsCore`가 진행 중이던 집결·이동 계획을 해제할 때 `ResumeRallyAfterCore`를 켜고, `TryStartRally`가 코어 처리 뒤 방 활동 종료 기준(`IsRoomActivityComplete`) 없이 재집결한다(다른 집결 불가 조건은 그대로). 집결이 없던 상태에서 탐색 중 코어를 처리하면 켜지 않아 기존 파티 종류별 종료 기준을 따른다(표 둘째 행).
- **집결 대상은 생존 파티원 전원이다**(05번 5장 295~307줄, 검증 05-05): `Party.CollectUnaccountedAbsentees`가 명령을 받지 못한 생존 구성원과 리더가 모르는 사망(`PartyDeathRecord.InfoKnownUnits`에 리더 이름 없음)을 미집결자로 보고 집결 완료를 막는다. 기다림의 상한은 기존 집결 60초 근사(교전·경계 중 정지)이고 포기한 이름은 `_givenUpAbsent`로 기억해 다음 집결에서 다시 기다리지 않는다(수색·포기 뒤 진행/후퇴는 후속 문서 몫). kill-switch `rallyAbsenteeWaitEnabled`.
- **입장 자리는 아는 타일에서만 고른다**(04번 0장 12~14줄, 검증 05-06 관찰 4): `PartyAdvanceSystem.IsSlotFree(..., knowledge)` — 입장 자리는 리더(`BeginEntering`)·본인(`RetargetPlanSlot`)의 개인 지도로 바닥이 확인된 타일만. 없으면 방향 목적지/문 먼 쪽 줄 다음 칸. kill-switch `entrySlotKnowledgeEnabled`.
- **개인은 임의로 다음 방에 들어가지 않는다**(04번 10장 578줄·05번 1장 53줄·01번 585줄, 검증 05-01): `Human.TryGetExplorationBounds`가 `lastKnownRoom`(`UnitFunction.SyncRoomAffiliation`) 사각형을 돌려주고, 인류의 프론티어 탐색(`NavigationFSMState`)·무작위 배회·조사 후보(`Unit.ComputeInvestigateTarget`)가 그 방 안으로 제한된다. 방 안에 할 일이 없으면 `HumanIdleSystem`(05번 8장)이 합류한다 — 목적지 해석(아는 리더 → 유효한 집결 위치 → 현재 방의 아는 문 주변 대기 자리(통과 구간 밖) → 시야 넓히기 → 허용 위치 대기)·부재 확인·막힘 기억은 코어 보고 이동과 같은 `PartyCoreReportSystem.StepTowardDestination`을 쓰고(상태는 `Human.idleWait`, `currentWait`가 아님), 리더를 최근 확인했고 곁이면 유지한다. **합류할 리더가 없는 인류(파티 없음·리더 없음·리더 사망)는 방 제한을 받지 않는다**(`Human.TryGetExplorationBounds`가 false — 안 그러면 방 탐색 뒤 영구 정지, 검증 05-07). 예외: 계단 자동 이동, 리더 지시 공동 이동·진형 입장, 코어 처리, 전투 추격(전투 검증 영역). 개인 `DoorAttack`은 유지(문만 부수고, 부순 뒤 이동은 집결 → 리더 결정 → 입장). 안전 해치 `Party.LeaderMayExploreBeyondRoom`: 리더가 `ReadyToAdvance`인데 문을 10초 넘게 못 찾으면 리더만 방 밖까지 탐색. kill-switch `roomBoundExplorationEnabled`.
- **방 경로는 리더가 아는 방 그래프로 정한다**(04번 1장 30~35·47줄, 검증 05-06 관찰 2): `LeaderRoutePlanner`(+순수 `RoomRouteMath`)가 리더가 아는 게이트(파괴된 통로 포함)로 ① 목표 방 직행 ② 구성원이 아직 들어가 보지 않은 방(`Party.HasVisitedRoom`) ③ 막다른 방이면 방문한 방들을 BFS한 되돌이 게이트를 고르고, 고를 게 없으면 예전 `TryFindKnownDoorInCurrentRoom`으로 폴백한다. 이미 열린 게이트는 계획이 돌파 없이 입장한다. kill-switch `leaderRoutePlannerEnabled`. 방 선택의 세부 기준은 문서가 리더 문서에 맡겼으므로 같은 순위 안의 판단은 목표 방 중심 거리 휴리스틱을 그대로 쓴다.
- **보호 포메이션(`Formation`)은 꺼져 있다**(사용자 결정 2026-09-04 "더 자세히 논의해야 해" — `TacticalBehaviorPriorityConfig` `Formation.enabled=false`, 폴백 BT도 주석): 참가 조건(상호작용 시작 순간 `NotifyInteractionStarted` 토큰을 받은 파티원만)·시야 방향은 03번 6장대로 휴면 상태로 남아 있고, 검증 05-08(2026-10-02)에서 두 가지를 고쳤다 — ① 집결 명령·방 이동 계획 편입·공동 이동·문 찾기 추종 배정 시 `Human.currentFormation`을 비운다(05번 9장 526·572줄, 안 비우면 대기가 풀린 뒤 새 모집 확인 없이 호위 재개) ② 호위 자리는 `EscortSlotMath`(03번 6-4·6-5장: 근접 전방 → 좌우 → 가장 가까운 이동 가능 타일, 원거리 후방 2칸 이상 → 좌우 후방 → 가장 가까운 비점유 타일)로 같은 대상 호위 전원을 차례로 배정한다. 둘 다 플레이 미확인 — 다시 켜기 전에 확인할 것.
- **리더의 입장 순서는 문서가 정한 그대로다**(2026-10-02 조사): 04번 8장 337·361줄 "리더도 자신의 역할 순서를 따르며 무조건 맨 앞에 세우지 않는다" — 근접 리더는 근접 단계라 먼저 문을 지나고 지난 즉시 개인 행동으로 풀린다(05번 1장 "방 진입 후에는 개인 탐색·임무 판단으로 전환"). 문서에 "리더는 마지막에 입장"·"리더가 입구에서 파티를 기다림" 같은 규칙이 없어 추가하지 않았다(사용자 지시: 문서에 없으면 넣지 말 것) — 같은 요청이 다시 나오면 문서 근거부터 확인한다.
- **리더의 1초 집결 확인은 입구 시퀀스·층 이동 중에는 건너뛴다**(`Party.TickRoomActivityCheck`, 2026-10-02): 집결 "시도"는 의도가 아니라 리더 `OnUpdate`의 1초 폴링이고, 사전 스폰으로 파티가 0층에 생길 때부터 돌아 `TryStartRally`의 첫 게이트(입구 시퀀스)에서 매번 거절돼 "집결 불가" 로그만 반복됐다. 구성원이 입구 시퀀스 중이거나 리더가 계단 이동 중이면(진행 중 집결이 아닐 때) 호출 자체를 하지 않는다. 전투 후 스윕이 끝난 유닛이 `TryStartRally`를 직접 부르는 경로는 그대로다.
- **대기·집결은 경계에 밀린다**(BT 순서 `Alert` → `Wait`, 문서 03 시스템 v0.6 4-15도 사망 수색이 대기·집결을 중단시킨다고 규정). 유닛이 자리를 못 지키면 먼저 `PartyDiagnostics.DescribeMember` 진단 로그(집결 진행 중 줄·진형 15초 로그)의 경계 종류·보이는 적을 본다 — `GetSubLabel`은 대기를 경계보다 먼저 판정해 경계 중에도 "전술(대기)"로 보인다.
- `AIMovementHelper.FindDoorWaitSlot`은 문이 속한 방 안에서만 자리를 고른다 — 반경 안이라는 이유만으로 문 반대편(닫힌 적 문 너머)을 뽑던 버그(검증문서 "플레이 로그 분석 → 집결 개선" ④).

### 알려진 한계 / 미검증
Unity 플레이 검증을 하지 못했다(컴파일 + `PartyFormationMath` 순수 함수 하니스 38건까지 — 리더 지시 배정·교전 중 시간 정지·진형 복귀 후 재돌파·4단계 입장 흐름은 플레이로만 확인된다). 전투 중인 유닛이 있으면 집결·FormingUp이 그 전투가 끝날 때까지 기다린다. 입구 문을 다음 문으로 잘못 고르는 문제(`TryFindKnownDoorInCurrentRoom`)는 그대로다. 플레이 확인 체크리스트는 검증문서 같은 항목에 있다.

## 문 구조 — 방 쪽 줄(1×2) 묶음이 문 오브젝트 하나 (2026-10-01)

### 구조 (이걸 다시 손댈 때 먼저 볼 것)
- **문 오브젝트 = 게이트의 한 줄(폭 2 → 1×2).** 게이트당 두 개(A방 쪽·B방 쪽)이고, 묶는 기준은 `GetGateDoorTiles`의 줄 순서가 아니라 **각 타일이 실제 속한 방**(`DoorGeometry.GroupByRoom` + `DoorSystem.GetGateDoorGroups`가 `roomGrid`를 연결 — `MapRandering.ExcludeOwnGateDoorTiles`와 같은 이유로 순서는 기하학적 규칙일 뿐 방과 무관). 한 방이 두 줄을 다 차지하는 비정상 데이터는 줄 단위로 분리한다.
- **체력·개폐·소유 진영·파괴·재설치가 묶음 단위로 한 번에 처리된다.** 체력은 묶음당 `DoorMaxHp`(300 — 한 레인을 뚫는 데 필요한 near+far 600은 예전과 같다), 재설치 비용 `DoorRepairStoneCost`(100)도 묶음당이다. 묶음의 어느 칸이든 접근 시도·보유 진영 유닛이 있으면 문 전체가 열린다. `RemoveDoor(pos)`는 pos가 어느 칸이든 묶음 전체를 제거한다. 소유 진영은 **그 묶음이 붙은 방**의 최초 방 소유 스냅샷(파괴+재설치로만 바뀜 — 기존 규칙 그대로).
- **멀티 타일 오브젝트는 별칭으로 등록한다**: `InteractableObject.OccupiedTiles`의 모든 타일이 `objectGrid`에서 같은 오브젝트를 가리키고(`GameSession.SpawnObject`), 제거도 별칭 전부(`ObjectSpawner.CollectObject`). 그래서 타일 키로 조회하는 코드(`IsBlockedByClosedDoor`, 시야 차단, 클릭 선택, `GetObjectVisual`, `IsDoorTile`)는 수정 없이 어느 칸에서나 동작한다. `Position`은 대표 타일(가장 작은 좌표). **`objectGrid.Values`를 순회하는 코드는 문이 두 번 나온다**(현재 `TryFindKnownDoorInCurrentRoom`·`ComputeInvestigateTarget`(문 제외)·`HasKnownRecoverableInRoom`(전리품만)은 무해 — 새 순회를 추가할 때 확인).
- **인접 판정은 "오브젝트의 어느 타일에든"이다**(`InteractableObject.IsAdjacentTo`/`NearestTileTo`): `UnitFunction.OnUpdate` 채널링 블록, 플레이어 우클릭 공격(`ExecutePlayerAttackObject`, 클릭한 칸이 아니라 가장 가까운 칸으로 접근), 진형 돌파(`StepBreach`). `FindHostileExitDoor`는 원래 가장 가까운 타일을 고르므로 그 칸 인접 = 문 전체 인접이라 그대로다. 코어/함정은 한 칸이라 기존과 같다.
- **비주얼은 로직과 달리 두 장이다**(사용자 확정 2026-10-01 "기존처럼 양쪽으로 젖혀지는듯이, 2개의 스프라이트가 존재하게 — 2개를 묶어서 1개처럼 동작"): 문 오브젝트(체력·개폐·파괴·소유 진영·통행·채널링 판정)는 하나이고, 비주얼 루트 하나(중심, 회전 없음 — 진행 막대와 파괴 이펙트가 두 짝 중앙에 붙음) 아래에 **칸마다 자식 스프라이트 한 장 + `ShadowCaster2D`**(`GameSession.CreateDoorLeaves`, `DoorLeafVisual` 표식)를 둔다. 각 짝은 예전 한 칸 문과 똑같이 자기 타일 중심에서 수평 게이트 90도 회전으로 그려지고, `DoorSystem.UpdateProcess`가 열림/닫힘 스프라이트를 **짝 전부에 같이** 바꾼다. 늘려 그리는 방식(`StretchScale`)은 폐기. 재설치 고스트도 `PlacementGhost.UpdateTiles`로 칸마다 한 장씩 보여 준다. **뒤쪽 짝은 좌우 반전한다**(`DoorGeometry.IsMirroredLeaf` — 앞쪽 절반 그대로·뒤쪽 절반 반전): `door_open` 아트는 왼쪽 7열에만 문짝이 있어서 같은 스프라이트를 그대로 두면 두 짝이 같은 방향으로 젖혀진다. 반전하면 두 문짝이 바깥 양 끝으로 갈라지는 양쪽 여닫이가 된다(수직 게이트는 왼→오른쪽, 수평 게이트는 90도 회전이라 아래→위쪽 순서로 같은 규칙). 반전은 `flipX`가 아니라 로컬 스케일 x=-1로 해서 `ShadowCaster2D`도 같이 뒤집히게 한다. 재설치 고스트(`PlacementGhost.UpdateTiles`)도 같은 규칙을 쓴다.
- **방 도달성(`IsGatePassableForFaction`)은 두 방 쪽 문이 모두 통행 가능해야 한다**(예전엔 한 줄의 첫 타일만 대표로 봐 반대쪽 문이 막혀 있어도 "도달 가능"으로 판정할 수 있었다).

### 알려진 한계 / 미검증
Unity 플레이로 눈 검증을 못 했다(컴파일 + `DoorGeometry`/`InteractableObject` 헬퍼 하니스 14건 — 늘린 스프라이트·그림자·진행 막대 위치·재설치 고스트·한 칸만 접근해도 두 칸이 같이 열리는지는 플레이로만 확인). 한 줄이 한 번에 부서지므로 예전의 "2*2 통로에 한 칸만 뚫린 상태"는 더 이상 생기지 않는다.

## 유니티 상단 Tools 메뉴 구조 (Tools / Tools(new), 2026-09-27)

**핵심 사실 — "Tools"는 이름을 바꿀 수 있는 별도 객체가 아니다.** Unity는 `[MenuItem("Tools/...")]`
처럼 문자열이 "Tools/"로 시작하는 게 하나라도 있으면 그 이름의 최상위 메뉴를 자동으로 만든다.
"Tools를 다른 이름으로 바꾼다"는 건 결국 그 문자열들을 전부 바꾸는 것과 동일하다.

같은 날 안에서 두 번 뒤집힌 결정이라 순서를 남겨둔다:
1. 처음엔 기존 `Tools/*`·`GrimArchive/*` 항목 전부(24개 파일, 29개 `[MenuItem]`)를
   `Tools(old)/...`로 옮겨 `Tools`와 분리했다.
2. `Assets/Plugins/Demigiant/DOTween/Editor/DOTweenEditor.dll`에 미리 컴파일된
   `Tools/Demigiant/DOTween Utility Panel`은 소스가 아니라 바이너리 안의 문자열이라 옮길 방법이
   없다(`Menu.RemoveMenuItem` 비공개 API + 리플렉션 우회안은 있지만, DOTween 업데이트 시 깨질 수
   있어 사용자가 기각) — 즉 `Tools`라는 최상위 메뉴 자체는 DOTween 때문에 절대 완전히 없어지지
   않는다.
3. 그럴 거면 굳이 `Tools(old)`로 나눠둘 이유가 없다고 판단해(사용자: "으악. 그럼 tools old에
   있는 내용들을 전부 tools에 넣자") **다시 전부 `Tools/...`로 되돌렸다** — 최종 상태는 기존
   그대로: GrimArchive 관련 항목은 `Tools/GrimArchive/...`, 나머지는 `Tools/...` 그대로이고
   `Tools/Demigiant/...`(DOTween)와 그냥 공존한다.

**새 에디터 툴의 `[MenuItem]`은 여전히 `Tools(new)/...` 아래에 만들 것** — 이 관례만 유지된다.
다만 "실행 한 번으로 끝나는 일회성 에셋 준비" 성격이 강해 사용자가 굳이 메뉴가 필요 없다고 판단한
경우(예: 벽 자동 타일 스프라이트 라벨 슬롯 — 아래 섹션 참고)는 GUI 없이 코드만 추가하고 끝내기도
한다. 다시 "old/new로 나누자"는 제안이 나오면 위 1~3번 히스토리부터 공유할 것(같은 시행착오를
반복하지 말 것).

## 벽 자동 타일 연결 + 색상 테마 (회의록 2026-09-27)

### 배경

정림(아트)·송성현(사용자) 회의록(`Assets/문서/공식문서/회의록 09.27.md`) 기반 2가지 작업.
구현현황은 `Assets/문서/구현현황/구현중/벽자동타일_색상테마_구현현황_2026-09-27.txt`(또는 그 이후
최신본) — 이 시스템을 다시 손댈 때는 그 문서부터 확인할 것(다른 구현현황 문서와 동일 관례).

1. **벽 자동 타일 연결**: 벽 타일을 배치하면 주변 8방향 연결 상태를 보고 10종(직선 2 + 외곽 모서리
   4 + 안쪽 모서리 4) 중 적절한 스프라이트를 자동 선택한다. 순수 판정 함수는
   `Assets/Script/Randering/WallAutoTileMath.cs`(`WallVariant` enum + `SelectVariant`, 우선순위:
   안쪽 모서리 > 바깥쪽 모서리 > 직선 > 예외 Horizontal 폴백), 단위테스트는
   `Assets/Tests/WallAutoTileTests.cs`. `MapRandering.RenderFloor`가 Wall 타일마다 `BuildWallMask`
   (기존 셰도우캐스터용 헬퍼 재사용)로 만든 층 전체 bool 격자에서 8방향을 샘플링해 판정하고,
   `wallShapeTiles[10]`(각 변형의 실제 Tile)에서 골라 배치한다 — 기존 `PickRandomVariant`(완전
   랜덤)를 대체했다(바닥 타일은 그대로 랜덤 유지, 이번 변경과 무관). 실제 스프라이트는
   `Assets/Resources/Tile/TileSpriteLibrary.spriteLib`의 "Wall" 카테고리에서
   `WallAutoTileMath.GetSpriteLibraryLabel`이 반환하는 라벨(`Wall_Horizontal`/`Wall_Outer_TL` 등
   회의록에 명시된 이름 그대로)로 조회한다. 사용자가 명시적으로 "tools에 넣지 말고"라고 확정해서
   별도 EditorWindow/메뉴 없이 코드+슬롯 준비까지만 했다 — **10개 라벨 자체는 2026-09-27에 이미
   `TileSpriteLibrary.spriteLib`를 직접 편집해 만들어뒀다**(기존 "Base" 라벨 1개를 10개로 교체,
   전부 당장은 기존 wallSprite(Tile_StoneWall)를 임시로 가리킴 — 라벨을 새로 만들 필요는 없고, 이제
   정림이 Sprite Library Editor(Window > 2D > Sprite Library Editor)에서 그 10개 라벨 각각의
   스프라이트만 실제 아트로 교체하면 코드 변경 없이 바로 반영된다). `.spriteLib`는 YAML이지만
   `SpriteLibrarySourceAssetImporter.OnImportAsset`이 라벨 `m_Hash`를 신뢰하지 않고 `m_Name`에서
   항상 새로 계산하므로(`Library/PackageCache/com.unity.2d.animation@.../Editor/SpriteLib/
   SpriteLibrarySourceAsset/SpriteLibrarySourceAssetImporter.cs` 확인) 손으로 편집해도 해시를 맞출
   필요가 없다 — 다만 이건 이 자산의 구조를 직접 확인하고 내린 예외적 판단이고, 일반적으로는 여전히
   "Unity Editor 환경" 메모의 원칙(프리팹 등은 손으로 YAML 편집 금지, 메뉴 툴을 거칠 것)을 따른다.
2. **색상 초기값 테마**: 런타임 조정이 아니라 "같은 스프라이트로 다른 테마 표현"용 생성 시점 틴트
   (정림 확정: "RGB 초기값만... 런타임 변경이 당장 필수가 아녀서"). `MapColorTheme`
   (`Assets/Script/Randering/MapColorTheme.cs`, `[CreateAssetMenu]` ScriptableObject —
   themeName/wallColor/floorColor)를 여러 개 만들어 보관한다. **층별로 다른 프리셋을 배정할 수
   있다**(2026-09-27 같은 날 후속 요청 "층별 색깔 프리셋 지정 가능하게 바꾸자" — 처음엔 "활성 테마"
   1개만 전 층 공통 적용이었다가 교체) — `MapFloorColorThemes`
   (`Assets/Script/Randering/MapFloorColorThemes.cs`, defaultTheme + 층별 리스트)가
   `Assets/Resources/MapColorTheme_FloorAssignments.asset` 단일 인스턴스로 저장되고,
   `Tools(new)/맵 타일 색상 테마`(`Assets/Editor/MapColorThemeWindow.cs`)의 "층별 테마 배정" 섹션
   (이 프로젝트는 `FloorId` 0~3 고정 4개 층이라 "기본값"+"0~3층" 5개 `ObjectField`)이 이 에셋을
   편집한다. `MapRandering`은 이제 벽/바닥 Tile을 전 층 공유가 아니라 층마다
   `GetOrBuildFloorTileSet(floorIndex)`로 따로 구워 캐시한다 — 그 층에 명시 배정이 없으면
   `defaultTheme`, 그것도 없으면 흰색(원본 그대로) — 다음 맵 생성부터 반영, 실시간 반영 아님. 이
   층별 캐시(`_floorTileSets`)는 `BuildTileCache`가 매 맵 생성(재생성 포함)마다 비우므로, 배정을
   바꾼 뒤 맵을 다시 생성하면 바로 반영된다(세션을 새로 시작할 필요 없음 — 처음엔 세션당 한 번만
   굽고 재사용해 재생성해도 안 바뀌는 문제가 있었는데, 이 확장 작업 중 같이 고쳤다).

### 다음에 이 시스템을 확장할 때

1. `벽자동타일_색상테마_구현현황_2026-09-27.txt`(또는 그 이후 최신본) 맨 위 "다음 우선순위 구현
   후보" 확인 — 실제 벽 아트 10종은 아직 없고, 색상 테마도 층별 배정을 다시 해줘야 하는 상태다
   (기존 "테스트" 프리셋 자체는 안 사라짐, 재연결만 필요)라고 적혀 있다.
2. T자/십자/벽 끝부분(`Wall_End_*`/`Wall_T_*`/`Wall_Cross`)이 필요해지면
   `WallAutoTileMath.SelectVariant`의 예외 폴백 분기부터 확장할 것 — 지금은 전부 Horizontal로
   뭉뚱그려진다.
3. 새로 구현/연결한 게 있으면 구현현황 문서를 직접 갱신한다(다른 구현현황 문서와 동일 관례).

## 공황(Panic) 행동 임시 비활성 (2026-10-02)

**공황 행동은 지금 꺼져 있다** — `AIBehaviorConfig.panicBehaviorEnabled`(기본 `false`). 판정의 단일 출처는 `TacticalFSMState.IsPanic`이고 `PlayerCommandFSMState.IsPanicMode`가 그걸 부른다(스위치도 여기서만 본다). 플레이 테스트에서 공황이 너무 쉽게 걸려 대부분의 행동이 막혀서(사용자 요청 "공황 잠시 꺼줘... 나중에 도주·후퇴 관련 더 나오면 키자") 껐다.

- **왜 그렇게 쉬웠나**: 인류 10종 시작 정신력이 40/100(`units.json`·프리팹), 임계값 `panicMentalRatio` 30% 미만, 03번 4-12장 사망 정보로 −10(직접 발견)/−5(전파 수신) — 사망 사건 두세 번이면 공황이고, **정신력을 회복시키는 코드가 없어** 영구다. 공황 동작은 BT 최상위의 무작위 걸음(50%/틱)이라 경계·조사·집결 대기·코어/문 공격·함정 대응이 전부 가려지고 집결도 60초 상한까지 끌린다.
- **문서 근거는 없다**: 행동경로·목표결정 00~07번 문서에 "공황"이 없고, 03번 v0.6이 "공황·도주 판정"을 아직 없는 06 위기반응·도주·후퇴 시스템으로, 00/05번이 후퇴·도주의 체력·정신력 기준을 "정신력·도주·후퇴 [차후 작성 예정]"으로 미뤘다. 임계값·무작위 걸음·영구 지속은 전부 내부 임시 값이다(기준도 코드 세 곳이 다르다 — 행동 30%, 가중치 정보 오차 25%, 인지 보정 17%).
- **꺼도 그대로인 것**: 정신력 감소(03번 4-12장 −10/−5), 정신력 단계별 인지 보정(02번 9장), 가중치 정보 오차(공황 단계 ±2타일·±25%) — 이 셋은 `IsPanic`과 무관하게 정신력 비율로 따로 계산된다.
- **다시 켤 때**: 도주·후퇴 문서가 나오면 ① `panicBehaviorEnabled = true` ② 시작 정신력 40·임계값 30%·영구 지속·무작위 걸음을 그 문서 기준으로 교체(회복 규칙 포함) ③ 세 곳의 공황 기준을 한 값으로 정리 ④ 03번 9-14장 "공황 도주 중 함정 통과 기준"(현재 공황 무작위 이동은 함정을 그대로 밟음)을 같이 연결한다. 그 전에 그냥 켜지 말 것 — 이전 증상이 그대로 재현된다.

## 적 발견 합류 대기 임시 비활성 (2026-10-02, 검증 06-01)

**전투 합류 대기(`TacticalBehaviorType.JoinCombatWait` — 위험도 2단계 이상 + 2칸 초과 거리에서 합류 의사 응답 2초·실제 합류 최대 5초, 07-A v0.2 9장)는 꺼져 있다** — `AIBehaviorConfig.joinCombatWaitEnabled`(기본 `false`), 단일 진입점 `PropagationSystem.ShouldDeferForJoinWait`가 꺼져 있으면 항상 false라 적을 정확 인지하는 즉시 전투하고 적 정보 전파(`PropagateEnemySighting`)는 병행된다. 현재 문서 세트(03번 v0.12 391·419줄 "별도 합류 대기를 두지 않는다", 06번 설정값 표가 "삭제된 적 발견 응답·합류·연장 대기"로 명시, 07번 03-08 "2초 응답 대기와 기본·추가 5초 도착 대기를 만들지 않는다")가 이 대기를 삭제했기 때문이다. 시야인지반응 폴더의 07-A v0.2는 아직 이 규칙을 정의하고 있어(문서 간 충돌) 코드는 지우지 않고 휴면으로 남겼다 — 07-A가 개정되면 합류 대기 코드·상수·테스트를 삭제한다. 다시 켜기 전에 문서 세트 기준부터 확인할 것(03-07·03-08 검증이 옛 07-A 기준으로 ✅ 처리했다가 정정된 전례). 같은 검증에서 인류 공격 대상 교체를 "1.2배 이상"으로 맞추고(`CombatScoreMath.ShouldSwitchAttackTarget` — 동점·양쪽 0은 유지, 부동소수 오차용 `CombatScoreMath.MeetsSwitchRatio`(허용 오차 포함), 비전투 `PartyGoalMath`에도 동일 적용), 어디서도 읽지 않던 설정 필드 4개(`postCombatAlertSeconds`·`unidentifiedAttackSearchSeconds`·`investigatePenaltyRatio`·`investigateDurationSeconds`)를 `config ?? ExplorationMath 상수`로 연결했다.
