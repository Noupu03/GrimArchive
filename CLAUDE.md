# GrimArchive_Prototype

Unity 2D 탑다운 던전 크롤러 프로토타입. VContainer(DI) + Haare 프레임워크 기반으로 점진적 리팩토링 중.

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
`Assets/문서/구현현황/구현중/가중치_구현현황_2026-07-09.txt`(가장 최신본, 이전 버전들은 `구현현황/지난거/`에
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
발견(같은 `CastRay` 지점, `InteractableObject.Tags`로 분기)까지 자동으로 돈다. 여전히 막힌 것은
**간접 파악(소리/전파)**, **버프/디버프/소환**, **함정/방어건물**, **오브젝트 "조사" 단계** 등
"이벤트를 발생시킬 하위 게임 시스템 자체가 없는" 경우들 — 최신 구현현황 문서의 "다음 우선순위" 섹션
참고.

### 다음에 이 시스템을 확장할 때

1. `Assets/문서/구현현황/구현중/가중치_구현현황_2026-07-09.txt`(또는 그 이후 최신본) 맨 위 "다음 우선순위
   구현 후보" 섹션부터 확인 — 작업량 순으로 이미 정리돼 있다.
2. 새로 구현/연결한 게 있으면 그 문서를 직접 갱신한다(장별 태그 업데이트 + 전체 집계 수정). 날짜가
   많이 지났거나 대규모로 갱신했다면 새 날짜의 파일을 만들고 이전 파일은 `구현현황/지난거/`로 옮긴다.
3. 소리/전파 시스템이 생기면 그 이벤트 발생 지점에서 `HumanKnowledgeBase.RecordEvent(..., InfoType.Indirect)`를 호출하도록 연결.
