# 📦 GrimArchive Prototype - 기능 그룹별 통합 아키텍처 다이어그램 (Grouped Architecture)

본 문서는 방대한 전체 스크립트와 함수들을 논리적인 **기능 덩어리(네임스페이스/패키지)**로 묶고, 여러 시스템에 걸쳐 사용되는 **교집합(Cross-Cutting Hub)** 역할을 하는 핵심 클래스들을 중앙에 배치하여 전체 시스템의 조감도(Bird's-eye view)를 한눈에 파악할 수 있도록 작성된 다이어그램입니다.

```mermaid
classDiagram
    direction TB

    %% ==========================================
    %% 🌟 교집합 / 중앙 허브 (Cross-Cutting Hubs)
    %% 여러 시스템(UI, 전투, AI, 맵)에서 공통으로 접근하는 브릿지 클래스들
    %% ==========================================
    namespace Cross_Cutting_Hub {
        class GameCompositionRoot {
            <<DI Container>>
            +Configure() 모든 매니저 주입
        }
        class UnitRegistry {
            <<Grid Tracker>>
            +RegisterUnitPos()
            +GetUnitsInRoom()
        }
        class Processor {
            <<Event Pipeline>>
            +Routines, processing
        }
        class InputManager {
            <<Player Input>>
            +FindUnitAtGridPos()
        }
        class FactionData {
            <<Shared Memory>>
            +discoveredMap
            +spottedEnemyUnits
        }
        class ObjectSpawner {
            <<Factory>>
            +Spawn()
        }
    }

    %% 1. 코어 세션 및 제어 (Core Session & Control)
    namespace Core_Session {
        class GameSession {
            +UpdateProcess()
            +ProcessUnitAction()
        }
        class MonoRoutine {
            +Initialize()
        }
    }

    %% 2. 유닛 및 데이터 컴포넌트 (Unit ECS)
    namespace Unit_ECS {
        class Unit {
            +JudgeState()
            +ExecuteAction()
        }
        class UnitFunction {
            +TakeDamage()
            +OnUpdate()
        }
        class HealthComponent {
            +hp, maxHp
        }
        class PerceptionComponent {
            +personalSpottedEnemies
        }
        class CombatStateComponent {
            +State
        }
    }

    %% 3. 인공지능 (AI FSM + BT)
    namespace AI_FSM_BT {
        class UnitFSM {
            +SelectState()
            +RunCurrentState()
        }
        class IFSMState {
            <<interface>>
            +GetPriority()
            +Tick()
        }
        class CombatFSMState { +Tick() }
        class TacticalFSMState { +Tick() }
        class NavigationFSMState { +Tick() }
        class BTNode { <<abstract>> +Tick() }
        class BTSequence { +Tick() }
        class BTSelector { +Tick() }
    }

    %% 4. 이동 및 길찾기 (Movement & Pathfinding)
    namespace Movement_Pathfinding {
        class AStarMovement {
            +TryGetNextStep()
        }
        class RoomMoveManager {
            +MoveUnit()
        }
    }

    %% 5. 전투 및 스킬 (Combat & Skills)
    namespace Combat_Skills {
        class SkillAction {
            +BuildSkillHitbox()
        }
        class SkillData {
            +baseDelayMs
            +baseCooldown
        }
        class Projectile {
            +Fire()
        }
        class DefenseSystem {
            +CalculateDamage()
        }
    }

    %% 6. 시야 및 인지 (Vision & Perception)
    namespace Vision_Perception {
        class VisionMath {
            +ViewDistance()
        }
        class PersonalMapKnowledge {
            +TickTileSafety()
        }
        class UnitPerceptionHandler {
            +Handle()
        }
    }

    %% 7. 맵 생성 및 그리드 (Map Generation)
    namespace Map_Generation {
        class CreateMap {
            +GenerateMap()
        }
        class MapManager {
            +SetupAndVisualizeMap()
        }
        class Chunks { +Floor(), Wall() }
        class Tile { +chunkAX, chunkAY }
    }

    %% 8. 시각화 및 VFX (Visuals & Rendering)
    namespace Visual_Rendering {
        class ThreatTileRenderer {
            +Render()
        }
        class UnitGenerate {
            +SyncVisuals()
        }
        class UnitAnimationController {
            +PlayAttack()
        }
        class VFXManager {
            +PlayVFX()
        }
    }

    %% 9. 건설 및 경제 (Building & Economy)
    namespace Economy_Building {
        class BuildingManager {
            +CanInstallAt()
            +Construct()
        }
        class ResourceManager {
            +AddResource()
            +TryConsumeResource()
        }
    }

    %% 10. 적 스폰 및 웨이브 (Wave & Offense)
    namespace Wave_Offense {
        class WaveSpawner {
            +SpawnWave()
        }
        class HumanWaveManager {
            +UpdateWaveTimer()
        }
        class OffenseProcessor {
            +StartOffense()
        }
    }

    %% 11. UI 및 메타 시스템 (UI & Meta)
    namespace UI_Encyclopedia {
        class UI_Encyclopedia {
            +TogglePanel()
        }
        class EncyclopediaManager {
            +UnlockEntry()
        }
        class DebugInfoPanel {
            +Refresh()
        }
        class UIManager {
            +ShowPanel()
        }
    }

    %% ==========================================
    %% 덩어리(그룹) 간의 의존성 관계선 (Dependencies)
    %% ==========================================
    
    %% 중앙 허브로의 연결 (교집합)
    GameCompositionRoot ..> Core_Session : DI 주입
    InputManager ..> Core_Session : 단축키/클릭 명령
    InputManager ..> UI_Encyclopedia : 패널 토글
    Unit_ECS --> UnitRegistry : 위치 등록 (Cross-cutting)
    Vision_Perception --> FactionData : 전장 시야 공유 (Cross-cutting)
    Movement_Pathfinding --> FactionData : 발견된 맵 기반 경로 탐색
    Processor --> MonoRoutine : R3 이벤트 파이프라인
    Core_Session --> ObjectSpawner : 유닛/이펙트 동적 생성

    %% 주요 비즈니스 로직 흐름
    Core_Session --> Map_Generation : [초기화 1회] 맵 생성 및 데이터 세팅
    Core_Session --> Unit_ECS : 매 틱 UpdateProcess
    
    Unit_ECS --> AI_FSM_BT : 매 틱 상태 판단 및 실행 위임
    Unit_ECS --> Vision_Perception : 이동 시 레이캐스트 시야 갱신
    Unit_ECS --> Movement_Pathfinding : A* 이동 명령
    Unit_ECS --> Combat_Skills : 스킬 발동 및 데미지 판정
    Unit_ECS --> Visual_Rendering : 상태 변경 시 스프라이트/VFX 동기화

    AI_FSM_BT --> Movement_Pathfinding : BT 이동 노드
    AI_FSM_BT --> Combat_Skills : BT 공격 노드

    Economy_Building --> UnitRegistry : 건물 겹침 체크
    Economy_Building --> UI_Encyclopedia : 자원 갱신 UI
    Wave_Offense --> UnitRegistry : 스폰 위치 탐색
    Wave_Offense --> Core_Session : 적 침공 이벤트 발생
```

### 💡 다이어그램 구성 요약
- **교집합 허브 (Cross-Cutting Hubs)**: DI 컨테이너(`GameCompositionRoot`), 물리 그리드 캐시(`UnitRegistry`), 유저 입력(`InputManager`), 공유 시야 데이터(`FactionData`) 등 **여러 시스템이 공통으로 참조하는 런타임 데이터 스크립트**들을 `Cross_Cutting_Hub` 네임스페이스로 분리하여 거미줄처럼 얽히는 의존성의 중심축을 명확히 했습니다.
- **맵 생성은 일회성 (Initialization Only)**: 사용자님의 예리한 지적대로, `Map_Generation`은 게임 시작 시 맵 데이터를 뽑아내고 역할을 종료하는 **빌더(Builder)**에 불과합니다. 따라서 이동/시야 덩어리에서 `Map_Generation`을 실시간으로 참조하던 잘못된 런타임 의존성을 끊고, 런타임 시에는 중앙 허브에 있는 `FactionData(discoveredMap)`를 조회하도록 올바르게 교정했습니다.
- **주요 기능 덩어리**: 코어 세션, 유닛 ECS, AI_FSM_BT, 전투/스킬, 건설/경제, 웨이브/오펜스, UI/도감
- **흐름 방향**: DI/Input 같은 외곽 제어가 중앙의 Core로 들어오고, Core가 Unit을 조종하며, Unit이 AI와 Combat을 거쳐 공유 데이터 허브(FactionData, Registry)에 접근하는 계층적(Hierarchical) 형태로 의존성을 정리했습니다.
