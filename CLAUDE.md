# GrimArchive_Prototype

Unity 2D 탑다운 던전 크롤러 프로토타입. VContainer(DI) + Haare 프레임워크 기반으로 점진적 리팩토링 중.

## 대표 가중치 3종 시스템 (이해도 / 위험도 / 흥미도)

### 배경

인류 AI가 던전 내 대상/위치를 판단하는 핵심 값 3종을 구현한다. 근거 문서 2개:

- `Assets/문서/GrimArchive_대표_가중치_3종_시스템_문서_v3.docx.md` — 배경/설계 의도 (구현 대상 아님, 판단 기준 참고용)
- `Assets/문서/GrimArchive_대표가중치_연산공식_v0.7_양식정리.docx.md` — **실제 구현 대상**, 25개 장

구현 현황(장별 태그: 구현/미구현/수치상으로만 존재, 애매했던 부분의 판단 근거)은
`Assets/문서/GrimArchive_가중치_구현현황.txt`에 연산공식 문서 순서 그대로 기록되어 있다. 이 시스템을
다시 손댈 때는 그 문서를 먼저 확인할 것.

### 핵심 설계 전제

이 프로토타입에는 아직 "웨이브"/"던전 재입장"/"생존자 귀환" 같은 런(run) 단위 게임 루프가 없다
(`GameSession`은 현재 씬의 유닛 리스트만 보유, 저장은 `MapSaveModel`로 맵 타일 데이터만 됨). 연산공식
문서의 상당 부분(6장 생존자 전역 반영, 8장 총량 한도, 13장 전멸 등)은 "웨이브 종료 → 생존자 귀환 →
전역 반영" 흐름을 전제로 한다.

그래서 이번 구현은: **가중치 계산 로직 자체(공식)를 완전하고 정확하게 구현**하고, 그 계산을 실제로
트리거하는 "웨이브 종료" 지점은 아직 없는 게임 루프 대신 **명시적으로 호출 가능한 공개 API**
(`HumanKnowledgeBase.OnWaveEnd(survivors)`)로 열어 두었다. 실제 웨이브 루프가 생기면 그 지점에서
이 API를 호출하기만 하면 된다.

다른 문서에 위임된 부분(지도 기록/저장 문서, 도감 문서, 전투 연산 문서, 파티 이동 문서, 소리/전파
문서, 기억/네메시스 문서, 레벨 구조 미정 — 문서 자체가 "정확한 공식은 OOO 문서에서 작성한다"고 명시)은
그 문서들이 아직 없으므로 가장 단순하고 합리적인 기본값으로 스텁 구현했다.

### 파일 구조

```
Assets/Script/Unit/Weight/
  WeightEnums.cs          — WeightType/InfoType/MentalErrorState/EventId
  WeightEventTable.cs     — EventId → (이해도Δ, 위험도Δ), weight_events.json 로더
  PersonalWeightRecord.cs — 유닛 개인이 대상별로 들고 있는 임시 기록
  IncidentLog.cs          — 이벤트 발생 기록 (6장 중복 제거/병합의 입력)
  SpeciesWeightState.cs   — 종별/개별 누적값, 특수행동 누적 상한
  HumanKnowledgeBase.cs   — 전역 레지스트리 (순수 C#, VContainer Register<T>().AsSelf())
  WeightMath.cs           — 순수 계산 함수 모음 (부수효과 없음, 테스트 용이)
Assets/Data/weight_events.json      — 3장 이벤트 표 데이터
Assets/Tests/WeightSystemTests.cs   — 문서에 나온 숫자 예시를 그대로 고정한 단위 테스트
```

`Unit.cs`에 `personalWeights`/`isSpecialUnit`/`isInterestTarget`/`baseInterest`/`baseDanger`/
`heavyHitThreshold`/`lastAttacker` 필드를 추가했고, `GameCompositionRoot.cs`에 `HumanKnowledgeBase`를
등록했다. 실제로 자동 연결된 이벤트는 "직접 경험(SELF)" 계층뿐이다: `UnitFunction.
TakePhysicalDamage`/`TakeMagicalDamage`(피격 이벤트)와 `GameSession.RemoveDeadUnit`(처치 이벤트,
`Unit.lastAttacker`로 가해자 추적)에서 `HumanKnowledgeBase.RecordEvent(...)`를 호출한다. 목격/간접
파악 계층과 나머지 이벤트는 그 이벤트를 발생시킬 하위 시스템(FOV 기반 목격 판정, 오브젝트/함정/
버프/디버프/소환/시체/파티)이 아직 없어 API만 준비되어 있다 — 자세한 건 구현현황 문서 참고.

### 다음에 이 시스템을 확장할 때

1. `Assets/문서/GrimArchive_가중치_구현현황.txt`에서 `[미구현]`/`[수치상으로만 존재]` 태그가 붙은 항목부터 확인.
2. 웨이브 루프가 새로 생기면 `HumanKnowledgeBase.OnWaveEnd(List<Unit> survivors)`를 그 종료 지점에서 호출하도록 연결.
3. 소리/전파 시스템이 생기면 그 이벤트 발생 지점에서 `HumanKnowledgeBase.RecordEvent(..., InfoType.Indirect)`를 호출하도록 연결.
