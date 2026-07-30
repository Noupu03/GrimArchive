# 성능 최적화 구현 내역 (2026-07-30)

## 1. 개요
프로파일러에서 관찰된 심각한 프레임 드랍(스크립트 연산 부하 및 GC 스파이크) 현상을 해결하기 위해 **오브젝트 풀링(Object Pooling)** 과 **GetComponent 캐싱(Caching)** 최적화를 진행했습니다.

## 2. 작업 상세 내용

### A. 오브젝트 풀링 (Object Pooling) 도입
매 프레임 무분별하게 호출되던 `Instantiate`와 `Destroy`를 제거하여 가비지 컬렉션(GC) 스파이크와 메모리 파편화를 방지했습니다. (Unity 내장 `UnityEngine.Pool.ObjectPool` 사용)

*   **`VFXManager.cs`**
    *   `Spawn` 함수를 풀링 시스템 기반으로 전면 개편.
    *   `UniTask`를 활용한 비동기 지연 반환(`ReleaseToPoolAfterTime`) 로직 구현으로 타이머 기반 Destroy 로직 대체.
*   **`AnimationEventVfxSpawner.cs`**
    *   기존 자체 `Instantiate` 로직을 제거하고, 개편된 `VFXManager.Spawn`을 호출하도록 변경하여 풀링 시스템을 공유하도록 구현.
*   **`Projectile.cs` & `SkillAction_Projectile.cs`**
    *   투사체 전용 정적 딕셔너리 기반 Object Pool 구현 (`Projectile.Spawn()`).
    *   투사체가 적에게 적중 시 발생하는 이펙트 또한 `Instantiate` 대신 `_attacker.VFX.Spawn`을 호출하여 풀링 활용.
    *   수명 초과 및 충돌 시 파괴될 때 `Destroy` 대신 `pool.Release` 호출.

### B. GetComponent 캐싱(Caching) 최적화
유닛의 상태(FSM)가 매 틱(Tick)마다 무거운 `GetComponent` 연산을 수행하여 발생하던 Scripts 오버헤드를 크게 줄였습니다.

*   **`TacticalFSMState.cs`**
    *   `ApplyTrapDisarmInterruptPenalty`, `CanContinueCore` 등 함정/코어 상호작용 관련 로직 수정.
    *   진행 막대(ProgressBar) 시각 효과를 갱신할 때 매번 `GetComponent<ObjectProgressBarVisual>()`을 찾지 않고, `UnitFunction.cs`에서 최초 1회 저장한 `CachedProgressBar` 변수를 바로 읽어오도록 수정.

## 3. 유의 사항 및 결정 사항
*   **UniTask 의존성**: VFX의 지연 비동기 소멸에 `UniTask`가 사용되었습니다. (기존 프로젝트 구성 요소 활용)
*   **초기화 주의**: 오브젝트 풀에서 객체를 꺼내어 재사용할 때, 파티클은 `ps.Play()`를 호출하여 다시 재생되도록 조치했습니다. 투사체(`Projectile.cs`) 역시 `Init()` 함수 호출 시 내부 상태가 다시 세팅되므로 큰 문제는 없으나, 추후 풀링 객체들의 상태 초기화 버그 발견 시 `actionOnGet` 콜백이나 `OnEnable` 등에서 수동 초기화가 필요할 수 있습니다.
