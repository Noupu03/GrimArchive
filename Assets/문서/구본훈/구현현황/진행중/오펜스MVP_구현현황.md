# 오펜스 MVP 최소 단위 구현 현황 (진행중)

## 개요
- **목표**: 야생 세력이 점유한 방을 대상으로 진행되는 오펜스의 최소 기능 단위 구현
- **구조 철학**: HAARE Native Routine 원칙에 기반하여 알고리즘 교체(Strategy Pattern)로 로직 분리

## 1. 진행 및 구현 내역
### 1.1 소속/행동 분리 알고리즘 설계
- 일반 몬스터와 야생 세력의 동작 알고리즘을 분리하여 할당 가능한 구조 확립 (완료)
  - `IFactionBehavior`, `WildMonsterBehavior`, `PlayerUnitBehavior` 등 전략 패턴 구현

### 1.2 방(Room) 구조체 및 오펜스 프로세서
- 야생 방에 플레이어 진입 시 오펜스 자동 시작 로직 구성 (완료)
  - `OffenseProcessor`, `Room` (물리적 좌표 Bounds 포함)
- 야생 무리형 및 거점형 성공 조건 로직 연결 (완료)
  - `WildSpawner`를 통한 거점형 스폰과 무리형 일반 소환 분리

### 1.3 전투 자원 정산기 (ResourceAccumulator)
- 오펜스 중 자원 B 누적 기능 구성 (완료)
- 승리 시 누적 자원 일괄 지급 및 진영 전환 연결 (완료)

## 2. 특이사항 및 논의 내역
- 알고리즘 교체 구조를 도입하여 향후 타 진영 유닛도 쉽게 확장이 가능하도록 유연한 코드로 작성 중.
- 디버그 전용 UI(`OffenseDebugWindow`)를 구축하여 에디터 플레이 모드에서 전체 사이클 시뮬레이션 가능.


### 1.4 오펜스 보상 시스템 재설계 및 고도화
- ResourceType에 OffenseReward (자원 B) 추가 및 분리
- 오펜스 종료 시 Wood 대신 OffenseReward 지급으로 수정
- WildMonsterBehavior에서 몬스터의 maxHp에 비례한 동적 보상(가중치) 누적 로직 구현
- StatusInfoPanel UI에 오펜스 전용 보상 현황 반영

### 2026-07-21 ��Ű��ó ���� ��Ȳ
- **Phase 4**: Unit Ŭ���� �� �Ŵ��� �ʵ� ������ ������ ���Һ� ����ü(UnitStatusEffects, UnitCombatState, UnitPerceptionState)�� �и� �Ϸ�.
- **Phase 5**: UnitFunction.CastRay ������ ����� �þ�/���� ������ �и��ϱ� ���� IVisionTileHandler, IVisionContext �������̽� ���� ��, TerrainRevealHandler, ObjectPerceptionHandler, UnitPerceptionHandler Ŭ������ �� ������ ���������� ����(OCP ���� �ؼ�).
- **Phase 6**: ���� �и� �� ���� �� ����. GameSession�� 372�ٷ� ũ�� ����.
