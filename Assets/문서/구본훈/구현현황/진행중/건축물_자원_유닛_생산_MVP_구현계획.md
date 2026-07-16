# 건축물·자원·유닛 생산 및 중앙 통제 아키텍처 MVP (최종 10단계 통합 기획안)

초기 기획부터 논의된 모든 변경점(타일맵 렌더링, 중앙 통제 BuildingManager, 태그 계층화, 1타일 1오브젝트 원칙, 철거, 동시성 보장 등)을 **개발 순서에 맞게 하나의 직관적인 10단계 파이프라인으로 재구성**했습니다.

---
### [Phase 1: 데이터 및 기반 시스템 정비]

#### 1단계: `InteractableObject` 태그 계층화 및 마이그레이션
- `Object/Passable/Loot`, `Object/Impassable/Building` 등 통행 여부를 포함한 계층적 태그 구조 도입.
- 기존 루팅/시체 오브젝트들이 이 규칙을 따르도록 팩토리 병합.

#### 2단계: 공용 자원 저장소 (`ResourceManager`) 구현
- 자원 Enum 정의 및 저장소 구현.
- 전역 좀비 데이터 방지를 위한 HAARE Lifecycle (`Init` / `Dispose`) 연동.

#### 3단계: 임시 자원 채굴기 구현 (UniTask)
- **반드시 HAARE Native Routine (UniTask)**을 사용하여 일정 시간마다 더미 자원을 자동으로 채워주는 비동기 로직 가동.

#### 4단계: 생산 비용 규칙 (`ProductionRule`) 데이터 정의
- 유닛 종류별 소모 자원량과 대상 프리팹을 정의하는 ScriptableObject 작성.

---
### [Phase 2: 중앙 통제형 건축 시스템 구축]

#### 5단계: `BuildingManager` 데이터 구조 설계 (1타일 1오브젝트)
- 맵 전체의 건물을 통제할 `BuildingManager` 생성.
- `Dictionary<Vector3Int, BuildingData>`를 선언하되, 추후 다중 리스트 확장을 대비해 좌표 질의(Query) 로직을 철저히 캡슐화. 중복 설치 원천 차단.

#### 6단계: 설치(`Install`) 및 철거(`Uninstall`) 로직 구현
- **설치:** `MapData` 장애물 속성 갱신 -> `BuildingManager` 등록 -> `MapRandering`으로 타일맵 그리기.
- **철거:** 위 프로세스의 완벽한 역순(Rollback) 처리 및 타일맵 지우기.

#### 7단계: 마우스 레이캐스트 설치 테스트 UI
- 마우스 커서를 따라다니는 반투명 고스트 프리팹 구현.
- 좌클릭 시 해당 그리드 좌표를 쏴서 6단계의 `Install(Vector3Int)` 호출.

---
### [Phase 3: 생산 연계 및 통합 검증]

#### 8단계: 생산 명령 클릭 감지 및 원자적 자원 차감
- 유저가 그려진 건물 타일을 마우스로 클릭하면 `BuildingManager`가 캐치.
- 동시성 버그(마이너스 자원)를 막기 위해 `TryConsumeResource()`로 자원 검사와 지불을 원자적(Atomic)으로 처리.

#### 9단계: 기존 `GameSession` 파이프라인 연계
- 자원 차감이 완료되면, 중앙 매니저가 `GameSession`에 이벤트/메세지를 날려 "선택한 타일 좌표에 유닛 스폰"을 위임 (강결합 완전 분리).

#### 10단계: 아키텍처 풀-사이클 통합 테스트
- 1) 고스트 마우스로 건물 설치 (MapData 및 타일맵 갱신 확인)
- 2) UniTask 자원 채굴
- 3) 건물 클릭 시 자원 차감
- 4) 유닛 스폰 완료
- 5) 피격/철거 로직(Uninstall) 발동 시 데이터 롤백 및 타일맵 삭제 검증.
