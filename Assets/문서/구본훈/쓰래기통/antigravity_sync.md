# GrimArchive 코드베이스 동기화 문서
**최종 갱신**: 2026-07-23  
**작성 주체**: Claude (구본훈 세션)  
**대상 독자**: Gemini Antigravity Brain — 이 파일을 읽고 현재 코드 상태를 파악하세요.

---

## 이 문서의 목적

Claude와 Gemini가 같은 프로젝트를 번갈아 작업할 때 상태 충돌을 막기 위한 싱크 포인트.  
"이미 된 것 / 아직 안 된 것 / 바뀐 API"를 여기에 기록해두면 중복 작업이나 되돌림 사고를 예방할 수 있다.

---

## 현재 프로젝트 기본 구조

- **엔진**: Unity 2D 탑다운 던전 크롤러 프로토타입
- **DI**: VContainer (`GameCompositionRoot`)
- **프레임워크**: Haare (NativeRoutine/MonoRoutine 기반)
- **AI**: GOAP (`GoapBrain` → `GoapGoal` → `GoapAction` → `AStarMovement`)
- **유닛 구조**: `Unit(ScriptableObject)` → `UnitFunction` → `Human` / `Monster`
- **컴포넌트 시스템**: Unity의 MonoBehaviour 컴포넌트가 아닌 **커스텀 `IUnitComponent` 리스트** (`Unit.Components`)

---

## ⚠️ 2026-07-23 Claude 작업으로 바뀐 것 (필독)

### A. `GetComponent<T>()` API 변경 — 가장 중요

**예전 방식 (지금은 사용 금지)**:
```csharp
unit.GetComponent<HealthComponent>().hp -= 10f;
unit.GetComponent<CombatStateComponent>().State.isHitThisTurn = true;
```

**새로운 방식 (이 방식만 써야 함)**:
```csharp
unit.Health.hp -= 10f;
unit.CombatState.State.isHitThisTurn = true;
```

`Unit.cs`에 캐싱 프로퍼티 10종이 추가됐다. `GetComponent<T>()`는 내부 `List<IUnitComponent>`를 선형 탐색하는 비싼 연산이라, 프로퍼티로 O(1) 캐싱으로 바꿨다.

| 타입 | 프로퍼티 이름 |
|------|-------------|
| `HealthComponent` | `.Health` |
| `CombatStateComponent` | `.CombatState` |
| `CombatStatComponent` | `.CombatStat` |
| `PerceptionComponent` | `.Perception` |
| `VisionStatComponent` | `.VisionStat` |
| `BaseStatComponent` | `.BaseStat` |
| `StatusEffectsComponent` | `.StatusEffects` |
| `AIStateComponent` | `.AIState` |
| `MemoryComponent` | `.Memory` |
| `PartyComponent` | `.UnitParty` |

29개 파일의 기존 호출이 전부 치환됐다. **새로 코드를 추가하거나 수정할 때 `.GetComponent<X>()` 방식을 쓰면 안 된다.**

### B. `StatusInfoPanel.cs` — `Update()` 제거

`Assets/Script/UI/StatusInfoPanel.cs`에서 `Update()` 메서드(TMP 텍스트 갱신)를 제거했다.  
`OnGUI()`가 같은 데이터를 이미 그리고 있어 이중 렌더링이 발생하고 있었다.  
이제 **`OnGUI()` 하나만 남아있다.**

### C. `GoapBrain` — 초기화 위치 변경

`Assets/Script/Unit/AI/GoapCore.cs`의 `GoapBrain` 클래스.

**예전**: `JudgeState()` 내부에서 매 틱 null 체크 후 초기화  
**지금**: 생성자에서 한 번만 초기화

`JudgeState()` 호출 시점에 `availableGoals`와 `availableActions`는 반드시 이미 채워져 있다.  
이 리스트에 **동적으로 항목을 추가/제거하는 코드를 새로 짜면 생성자도 같이 수정해야 한다.**

---

## 현재 아키텍처 메모 (Gemini 기록과의 대조)

Gemini의 `architecture_and_optimizations_log.md` (`c:/Users/wish0/.gemini/antigravity/brain/...`)에 기록된 내용과 현재 상태:

| Gemini 기록 | 실제 반영 여부 |
|------------|--------------|
| 1.1 컴포넌트화 분리 | ✅ 완료 (파일 모두 존재) |
| 1.2 VContainer DI 도입 | ✅ 완료 (단, `GameSession.Instance` 싱글톤은 하위 호환성으로 남아있음) |
| 2.1 FOV 0.2초 스로틀링 (`_lastFovUpdateTime`) | ❌ 별도 타이머 없음 — `UpdateFOV()`가 `actionCooldown` 기반으로만 호출되어 실질적으로 동일한 효과. **별도 타이머 추가 불필요.** |
| 2.2 GC 폭발 제거 (`_cachedThreats`, `_safetyTickTimer`) | ✅ 완료 |
| 2.3 A* `maxIter` 300 제한 | ✅ 완료 |
| 2.4 ThreatTileRenderer GC-Free | ✅ 완료 |

---

## 주요 파일 위치 빠른 참조

```
Assets/Script/Unit/Core/
  Unit.cs                  — 유닛 최상위 추상 클래스. 캐싱 프로퍼티 10종 여기에 있음.
  UnitFunction.cs          — 실질 구현. TakeDamage/OnUpdate 등
  Unit.cs (하단)           — Human(395줄~), Monster(579줄~) 클래스 정의
  UnitComponents.cs        — UnitCombatState, UnitPerceptionState 등 struct 정의
  HealthComponent.cs 등    — IUnitComponent 구현체들

Assets/Script/Unit/AI/
  GoapCore.cs              — GoapBrain, GoapGoal, GoapAction 기반 클래스
  Actions.cs               — 21개 구체 액션 (ActionCode 1~21)
  Goals.cs                 — 10개 구체 목표
  GoapPlanner.cs           — A*식 계획 수립

Assets/Script/Unit/Session/
  GameSession.cs           — 메인 게임 루프. UpdateProcess() 진입점.
  GameCompositionRoot.cs   — VContainer DI 설정
  UnitRegistry.cs          — 그리드 상 유닛 위치 관리 (O(1) 해시맵)

Assets/Script/Unit/Weight/  — 가중치 3종 시스템 (이해도/위험도/흥미도)
Assets/Script/Unit/Vision/  — 시야-인지-반응 시스템
```

---

## 다음 작업 후보 (우선순위 순)

이 항목들은 아직 아무도 손대지 않은 것들이다.

1. **소리/전파 시스템 연결** — `HumanKnowledgeBase.RecordEvent(..., InfoType.Indirect)` 호출 지점이 없음. 소리 시스템이 생기면 여기 연결.

2. **`GameSession.Instance` 싱글톤 제거** — VContainer DI가 있음에도 `GameSession.Instance`가 여전히 남아있다. 이를 참조하는 곳을 `[Inject]`로 전환하면 DI 구조가 완전해진다.

3. **가중치 구현현황 문서 확인** — `Assets/문서/구현현황/구현중/가중치_구현현황_2026-07-20.txt` 맨 위 "다음 우선순위" 섹션 참조.

4. **시야인지반응 구현현황 문서 확인** — `Assets/문서/구현현황/구현중/시야인지반응_구현현황_2026-07-22.txt` 맨 위 참조.

---

## 협업 규칙

- **이 파일을 갱신할 때**: 날짜와 작업 주체(Claude/Gemini)를 표시하고, "바뀐 것" 섹션을 업데이트할 것.
- **GetComponent<T>() 방식 코드를 새로 작성하지 말 것** — 위 프로퍼티 사용.
- **`JudgeState()`에 초기화 코드를 다시 넣지 말 것** — 생성자에 있음.
