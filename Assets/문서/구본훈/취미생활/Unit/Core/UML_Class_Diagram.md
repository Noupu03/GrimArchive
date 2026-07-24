# Package `Unit/Core` UML Class Diagram

**소스 경로:** `Assets/Unit/Core`

```mermaid
classDiagram
    class AIStateComponent {
        -Unit _owner
        +UnitAIWeightState AIWeightState
        +HashSet~Unit~ reactedAttackers
        +float currentReactionWindow
        +ThreatTileData reactingThreat
        +Unit reactingAttacker
        +ThreatTileData currentThreat
        -AIStateComponent() public
        +OnUpdate() void
        +OnDespawn() void
    }
    IUnitComponent <|-- AIStateComponent
    AIStateComponent --> Unit
    AIStateComponent --> UnitAIWeightState
    class BaseStatComponent {
        -Unit _owner
        +float agility
        +float sense
        +float sterngth
        +float Durability
        +float walkSpeed
        +float reaction
        +float statusResistance
        +float leadershipRange
        +float charisma
        -BaseStatComponent() public
        +OnUpdate() void
        +OnDespawn() void
    }
    IUnitComponent <|-- BaseStatComponent
    BaseStatComponent --> Unit
    class CombatStatComponent {
        -Unit _owner
        +float physicalAttack
        +float magicalAttack
        +float physicalDefense
        +float magicalDefense
        +float criticalChance
        +float attackspeed
        -CombatStatComponent() public
        +OnUpdate() void
        +OnDespawn() void
    }
    IUnitComponent <|-- CombatStatComponent
    CombatStatComponent --> Unit
    class CombatStateComponent {
        -Unit _owner
        +UnitCombatState State
        -CombatStateComponent() public
        +OnUpdate() void
        +OnDespawn() void
    }
    IUnitComponent <|-- CombatStateComponent
    CombatStateComponent --> Unit
    CombatStateComponent --> UnitCombatState
    class FactionData {
        +int_Arr discoveredMap
        +List~Unit~ spottedEnemyUnits
        -int floorCount
        -int w
        -int h
        -FactionData() public
        +InitMap() void
    }
    FactionData --> Unit
    class FactionType {
        <<enum>>
    }
    class HealthComponent {
        -Unit _owner
        +float maxHp
        +float hp
        +float maxMp
        +float mp
        -HealthComponent() public
        +OnUpdate() void
        +OnDespawn() void
    }
    IUnitComponent <|-- HealthComponent
    HealthComponent --> Unit
    class HumanFactionBehavior {
        +IsEnemy() bool
        +OnUpdate() void
        +OnDeath() void
        +OnEnterRoom() void
    }
    IFactionBehavior <|-- HumanFactionBehavior
    class IFactionBehavior {
        <<interface>>
        -IsEnemy() bool
        -OnUpdate() void
        -OnDeath() void
        -OnEnterRoom() void
    }
    class IOffenseQuery {
        <<interface>>
        -GetUnitsInRoom() IReadOnlyList~Unit~
    }
    class IPerceptible {
        <<interface>>
        -Vector2Int PerceptiblePosition get_set
        -int PerceptibleFloor get_set
        -float PerceptibleStealth get_set
        -float PerceptibleSpotting get_set
        -FactionData PerceptibleFactionData get_set
        -IFactionBehavior PerceptibleFactionBehavior get_set
        -bool IsPerceptibleSpecialUnit get_set
    }
    class ITargetable {
        <<interface>>
        -Vector2Int TargetPosition get_set
        -int TargetFloor get_set
        -float TargetHp get_set
        -FactionData TargetFactionData get_set
        -TakePhysicalDamage() void
        -TakeMagicalDamage() void
        -TakeMentalDamage() void
        -ApplyDirectDamage() void
    }
    class IUnitComponent {
        <<interface>>
        -OnUpdate() void
        -OnDespawn() void
    }
    class MemoryComponent {
        -Unit _owner
        +PersonalMapKnowledge personalMap
        +List~string~ collectedObjects
        -MemoryComponent() public
        +OnUpdate() void
        +OnDespawn() void
    }
    IUnitComponent <|-- MemoryComponent
    MemoryComponent --> Unit
    class OffenseProcessor {
        -IMapColorizer _colorizer
        -bool hasPlayer
        -bool hasWild
        -var roomUnits
        +Room currentOffenseRoom get_set
        -OffenseProcessor() public
        +StartOffense() void
        +UpdateProcess() void
        -FailOffense() void
        -OnOffenseSuccess() void
    }
    class PartyComponent {
        -Unit _owner
        +Party party
        -PartyComponent() public
        +OnUpdate() void
        +OnDespawn() void
    }
    IUnitComponent <|-- PartyComponent
    PartyComponent --> Unit
    class PerceptionComponent {
        -Unit _owner
        +UnitPerceptionState State
        +bool IsAlert
        +int AlertRecordCount get_set
        -PerceptionComponent() public
        +NotifyPerceptionSuspiciousChanged() void
        +RemovePerceptionRecord() void
        +OnUpdate() void
        +OnDespawn() void
    }
    IUnitComponent <|-- PerceptionComponent
    PerceptionComponent --> Unit
    PerceptionComponent --> UnitPerceptionState
    class PlayerMonsterBehavior {
        +IsEnemy() bool
        +OnUpdate() void
        +OnDeath() void
        +OnEnterRoom() void
    }
    IFactionBehavior <|-- PlayerMonsterBehavior
    class ResourceAccumulator {
        -ResourceAccumulator _instance
        -return _instance
        -int _accumulatedResourceB
        +int AccumulatedResourceB
        +ResourceAccumulator Instance get_set
        +AccumulateResourceB() void
        +CommitResourceB() void
        +ClearResourceB() void
    }
    class RoomType {
        <<enum>>
        -int rx
        -int ry
        -List~Unit~ _containedUnits
        +IReadOnlyList~Unit~ ContainedUnits
        +string RoomName get_set
        +FactionType RoomFaction get_set
        +RoomType Type get_set
        +RectInt Bounds get_set
        +bool HasActiveSpawner get_set
        +GetRandomPosInRoom() Vector2Int
        +AddUnit() void
        +RemoveUnit() void
    }
    RoomType --> Unit
    class Room {
        -int rx
        -int ry
        -List~Unit~ _containedUnits
        +IReadOnlyList~Unit~ ContainedUnits
        +string RoomName get_set
        +FactionType RoomFaction get_set
        +RoomType Type get_set
        +RectInt Bounds get_set
        +bool HasActiveSpawner get_set
        +GetRandomPosInRoom() Vector2Int
        +AddUnit() void
        +RemoveUnit() void
    }
    Room --> Unit
    class StatusEffectsComponent {
        -Unit _owner
        +UnitStatusEffects State
        -StatusEffectsComponent() public
        +OnUpdate() void
        +OnDespawn() void
    }
    IUnitComponent <|-- StatusEffectsComponent
    StatusEffectsComponent --> Unit
    StatusEffectsComponent --> UnitStatusEffects
    class Unit {
        +List~IUnitComponent~ Components
        -return null
        -HealthComponent _healthComp
        -CombatStateComponent _combatStateComp
        -CombatStatComponent _combatStatComp
        -PerceptionComponent _perceptionComp
        -VisionStatComponent _visionStatComp
        -BaseStatComponent _baseStatComp
        -StatusEffectsComponent _statusEffectsComp
        -AIStateComponent _aiStateComp
        -OnEnable() void
        +HasPerceivedThreatCollider() bool
        -Normalize() float
        +CalculateDerivedStats() void
        +SetupStats() void
        +GetDirRotation() Quaternion
        +TakeDamage() void
        +TakePhysicalDamage() void
        +TakeMagicalDamage() void
        +TakeMentalDamage() void
    }
    Unit --> StatusEffectsComponent
    class Human {
        +List~IUnitComponent~ Components
        -return null
        -HealthComponent _healthComp
        -CombatStateComponent _combatStateComp
        -CombatStatComponent _combatStatComp
        -PerceptionComponent _perceptionComp
        -VisionStatComponent _visionStatComp
        -BaseStatComponent _baseStatComp
        -StatusEffectsComponent _statusEffectsComp
        -AIStateComponent _aiStateComp
        -OnEnable() void
        +HasPerceivedThreatCollider() bool
        -Normalize() float
        +CalculateDerivedStats() void
        +SetupStats() void
        +GetDirRotation() Quaternion
        +TakeDamage() void
        +TakePhysicalDamage() void
        +TakeMagicalDamage() void
        +TakeMentalDamage() void
    }
    UnitFunction <|-- Human
    Human --> StatusEffectsComponent
    class Monster {
        +List~IUnitComponent~ Components
        -return null
        -HealthComponent _healthComp
        -CombatStateComponent _combatStateComp
        -CombatStatComponent _combatStatComp
        -PerceptionComponent _perceptionComp
        -VisionStatComponent _visionStatComp
        -BaseStatComponent _baseStatComp
        -StatusEffectsComponent _statusEffectsComp
        -AIStateComponent _aiStateComp
        -OnEnable() void
        +HasPerceivedThreatCollider() bool
        -Normalize() float
        +CalculateDerivedStats() void
        +SetupStats() void
        +GetDirRotation() Quaternion
        +TakeDamage() void
        +TakePhysicalDamage() void
        +TakeMagicalDamage() void
        +TakeMentalDamage() void
    }
    UnitFunction <|-- Monster
    Monster --> StatusEffectsComponent
    class UnitStatusEffects {
        <<struct>>
        +float stunDuration
        +float slowDuration
        +float poisonDuration
        +float burnDuration
        +float physicalAttackSpeed
        +float magicalCastSpeed
        +float actionCooldown
        +float_Arr skillCooldowns
        +bool isHitThisTurn
        +bool oneTimeReactUsed
    }
    UnitStatusEffects --> Unit
    class UnitCombatState {
        <<struct>>
        +float stunDuration
        +float slowDuration
        +float poisonDuration
        +float burnDuration
        +float physicalAttackSpeed
        +float magicalCastSpeed
        +float actionCooldown
        +float_Arr skillCooldowns
        +bool isHitThisTurn
        +bool oneTimeReactUsed
    }
    UnitCombatState --> Unit
    class UnitPerceptionState {
        <<struct>>
        +float stunDuration
        +float slowDuration
        +float poisonDuration
        +float burnDuration
        +float physicalAttackSpeed
        +float magicalCastSpeed
        +float actionCooldown
        +float_Arr skillCooldowns
        +bool isHitThisTurn
        +bool oneTimeReactUsed
    }
    UnitPerceptionState --> Unit
    class UnitAIWeightState {
        <<struct>>
        +float stunDuration
        +float slowDuration
        +float poisonDuration
        +float burnDuration
        +float physicalAttackSpeed
        +float magicalCastSpeed
        +float actionCooldown
        +float_Arr skillCooldowns
        +bool isHitThisTurn
        +bool oneTimeReactUsed
    }
    UnitAIWeightState --> Unit
    class UnitFunction {
        -float prevHp
        -float damage
        -bool defenderIsHuman
        -bool attackerIsHuman
        -string incidentId
        -bool attackerIdentified
        -bool directionKnown
        -float dist
        -float effectiveSpotting
        -float perceptionDistance
        +TakeDamage() void
        +TakePhysicalDamage() void
        +TakeMagicalDamage() void
        +RecordHitWeightEvent() void
        -ForceReidentifyAttacker() void
        -IsFullyBlockedTowards() bool
        -IsAttackerIdentified() bool
        -IsCurrentlyIdentified() return
        -BroadcastWitnessEvent() void
        +TakeMentalDamage() void
    }
    Unit <|-- UnitFunction
    IVisionContext <|-- UnitFunction
    UnitFunction --> FactionData
    class Dir {
        <<enum>>
        +string typeName
        +Vector2 footprint
        +float BASE_REACTION_TIME_MS
        +float MIN_REACTION_TIME_MS
        +float MAX_REACTION_TIME_MS
        +float BLOCK_PREPARE_TIME_MS
        +float DODGE_PREPARE_TIME_MS
        +float BLINK_PREPARE_TIME_MS
        +float PARRY_PREPARE_TIME_MS
        +float MIN_DEFENSE_SUCCESS_RATE
        -Knight() public
        -HumanBaseType() public
        -MeleeTank() public
        -WildBaseType() public
        -Archer() public
    }
    class UnitType {
        +string typeName
        +Vector2 footprint
        +float BASE_REACTION_TIME_MS
        +float MIN_REACTION_TIME_MS
        +float MAX_REACTION_TIME_MS
        +float BLOCK_PREPARE_TIME_MS
        +float DODGE_PREPARE_TIME_MS
        +float BLINK_PREPARE_TIME_MS
        +float PARRY_PREPARE_TIME_MS
        +float MIN_DEFENSE_SUCCESS_RATE
        -Knight() public
        -HumanBaseType() public
        -MeleeTank() public
        -WildBaseType() public
        -Archer() public
    }
    class Knight {
        +string typeName
        +Vector2 footprint
        +float BASE_REACTION_TIME_MS
        +float MIN_REACTION_TIME_MS
        +float MAX_REACTION_TIME_MS
        +float BLOCK_PREPARE_TIME_MS
        +float DODGE_PREPARE_TIME_MS
        +float BLINK_PREPARE_TIME_MS
        +float PARRY_PREPARE_TIME_MS
        +float MIN_DEFENSE_SUCCESS_RATE
        -Knight() public
        -HumanBaseType() public
        -MeleeTank() public
        -WildBaseType() public
        -Archer() public
    }
    UnitType <|-- Knight
    class HumanBaseType {
        +string typeName
        +Vector2 footprint
        +float BASE_REACTION_TIME_MS
        +float MIN_REACTION_TIME_MS
        +float MAX_REACTION_TIME_MS
        +float BLOCK_PREPARE_TIME_MS
        +float DODGE_PREPARE_TIME_MS
        +float BLINK_PREPARE_TIME_MS
        +float PARRY_PREPARE_TIME_MS
        +float MIN_DEFENSE_SUCCESS_RATE
        -Knight() public
        -HumanBaseType() public
        -MeleeTank() public
        -WildBaseType() public
        -Archer() public
    }
    UnitType <|-- HumanBaseType
    class MeleeTank {
        +string typeName
        +Vector2 footprint
        +float BASE_REACTION_TIME_MS
        +float MIN_REACTION_TIME_MS
        +float MAX_REACTION_TIME_MS
        +float BLOCK_PREPARE_TIME_MS
        +float DODGE_PREPARE_TIME_MS
        +float BLINK_PREPARE_TIME_MS
        +float PARRY_PREPARE_TIME_MS
        +float MIN_DEFENSE_SUCCESS_RATE
        -Knight() public
        -HumanBaseType() public
        -MeleeTank() public
        -WildBaseType() public
        -Archer() public
    }
    UnitType <|-- MeleeTank
    class WildBaseType {
        +string typeName
        +Vector2 footprint
        +float BASE_REACTION_TIME_MS
        +float MIN_REACTION_TIME_MS
        +float MAX_REACTION_TIME_MS
        +float BLOCK_PREPARE_TIME_MS
        +float DODGE_PREPARE_TIME_MS
        +float BLINK_PREPARE_TIME_MS
        +float PARRY_PREPARE_TIME_MS
        +float MIN_DEFENSE_SUCCESS_RATE
        -Knight() public
        -HumanBaseType() public
        -MeleeTank() public
        -WildBaseType() public
        -Archer() public
    }
    UnitType <|-- WildBaseType
    class Archer {
        +string typeName
        +Vector2 footprint
        +float BASE_REACTION_TIME_MS
        +float MIN_REACTION_TIME_MS
        +float MAX_REACTION_TIME_MS
        +float BLOCK_PREPARE_TIME_MS
        +float DODGE_PREPARE_TIME_MS
        +float BLINK_PREPARE_TIME_MS
        +float PARRY_PREPARE_TIME_MS
        +float MIN_DEFENSE_SUCCESS_RATE
        -Knight() public
        -HumanBaseType() public
        -MeleeTank() public
        -WildBaseType() public
        -Archer() public
    }
    UnitType <|-- Archer
    class CombatConstants {
        +string typeName
        +Vector2 footprint
        +float BASE_REACTION_TIME_MS
        +float MIN_REACTION_TIME_MS
        +float MAX_REACTION_TIME_MS
        +float BLOCK_PREPARE_TIME_MS
        +float DODGE_PREPARE_TIME_MS
        +float BLINK_PREPARE_TIME_MS
        +float PARRY_PREPARE_TIME_MS
        +float MIN_DEFENSE_SUCCESS_RATE
        -Knight() public
        -HumanBaseType() public
        -MeleeTank() public
        -WildBaseType() public
        -Archer() public
    }
    class VisionStatComponent {
        -Unit _owner
        +float stealth
        +float spotting
        +float baseVisibility
        +float attackVisibilityBoostTimer
        -VisionStatComponent() public
        +OnUpdate() void
        +OnDespawn() void
    }
    IUnitComponent <|-- VisionStatComponent
    VisionStatComponent --> Unit
    class WildBaseSpawnerComponent {
        +GameObject debugVisual
        -Unit _owner
        -Room _targetRoom
        -float _spawnInterval
        -CancellationTokenSource _cts
        -UnitType monsterType
        -Vector2Int spawnPos
        -int attempts
        -Monster monster
        -WildBaseSpawnerComponent() public
        -SpawnLoop() UniTaskVoid
        +OnUpdate() void
        -SpawnMonster() void
        +OnDespawn() void
    }
    IUnitComponent <|-- WildBaseSpawnerComponent
    WildBaseSpawnerComponent --> Unit
    WildBaseSpawnerComponent --> Room
    WildBaseSpawnerComponent --> UnitType
    WildBaseSpawnerComponent --> Monster
    class WildMonsterBehavior {
        -int rewardAmount
        +IsEnemy() bool
        +OnUpdate() void
        +OnDeath() void
        +OnEnterRoom() void
    }
    IFactionBehavior <|-- WildMonsterBehavior
```

### 📋 스크립트 클래스 명세

#### `AIStateComponent` (class)
- **경로:** `Script/Unit/Core/AIStateComponent.cs`
- **상속/인터페이스:** `IUnitComponent`
- **변수/프로퍼티:**
  - `-Unit _owner`
  - `+UnitAIWeightState AIWeightState`
  - `+HashSet~Unit~ reactedAttackers`
  - `+float currentReactionWindow`
  - `+ThreatTileData reactingThreat`
  - `+Unit reactingAttacker`
  - `+ThreatTileData currentThreat`
- **함수:**
  - `-AIStateComponent() public`
  - `-AIStateComponent() public`
  - `+OnUpdate() void`
  - `+OnDespawn() void`

#### `BaseStatComponent` (class)
- **경로:** `Script/Unit/Core/BaseStatComponent.cs`
- **상속/인터페이스:** `IUnitComponent`
- **변수/프로퍼티:**
  - `-Unit _owner`
  - `+float agility`
  - `+float sense`
  - `+float sterngth`
  - `+float Durability`
  - `+float walkSpeed`
  - `+float reaction`
  - `+float statusResistance`
  - `+float leadershipRange`
  - `+float charisma`
  - `+float exp`
  - `+float baseDanger`
  - `+float baseInterest`
  - `+float heavyHitThreshold`
  - `+float HPRegen`
- **함수:**
  - `-BaseStatComponent() public`
  - `-BaseStatComponent() public`
  - `+OnUpdate() void`
  - `+OnDespawn() void`

#### `CombatStatComponent` (class)
- **경로:** `Script/Unit/Core/CombatStatComponent.cs`
- **상속/인터페이스:** `IUnitComponent`
- **변수/프로퍼티:**
  - `-Unit _owner`
  - `+float physicalAttack`
  - `+float magicalAttack`
  - `+float physicalDefense`
  - `+float magicalDefense`
  - `+float criticalChance`
  - `+float attackspeed`
- **함수:**
  - `-CombatStatComponent() public`
  - `-CombatStatComponent() public`
  - `+OnUpdate() void`
  - `+OnDespawn() void`

#### `CombatStateComponent` (class)
- **경로:** `Script/Unit/Core/CombatStateComponent.cs`
- **상속/인터페이스:** `IUnitComponent`
- **변수/프로퍼티:**
  - `-Unit _owner`
  - `+UnitCombatState State`
- **함수:**
  - `-CombatStateComponent() public`
  - `-CombatStateComponent() public`
  - `+OnUpdate() void`
  - `+OnDespawn() void`

#### `FactionData` (class)
- **경로:** `Script/Unit/Core/FactionData.cs`
- **변수/프로퍼티:**
  - `+int_Arr discoveredMap`
  - `+List~Unit~ spottedEnemyUnits`
  - `-int floorCount`
  - `-int w`
  - `-int h`
- **함수:**
  - `-FactionData() public`
  - `+InitMap() void`

#### `FactionType` (enum)
- **경로:** `Script/Unit/Core/FactionType.cs`

#### `HealthComponent` (class)
- **경로:** `Script/Unit/Core/HealthComponent.cs`
- **상속/인터페이스:** `IUnitComponent`
- **변수/프로퍼티:**
  - `-Unit _owner`
  - `+float maxHp`
  - `+float hp`
  - `+float maxMp`
  - `+float mp`
- **함수:**
  - `-HealthComponent() public`
  - `-HealthComponent() public`
  - `+OnUpdate() void`
  - `+OnDespawn() void`

#### `HumanFactionBehavior` (class)
- **경로:** `Script/Unit/Core/HumanFactionBehavior.cs`
- **상속/인터페이스:** `IFactionBehavior`
- **함수:**
  - `+IsEnemy() bool`
  - `+OnUpdate() void`
  - `+OnDeath() void`
  - `+OnEnterRoom() void`

#### `IFactionBehavior` (interface)
- **경로:** `Script/Unit/Core/IFactionBehavior.cs`
- **함수:**
  - `-IsEnemy() bool`
  - `-OnUpdate() void`
  - `-OnDeath() void`
  - `-OnEnterRoom() void`

#### `IOffenseQuery` (interface)
- **경로:** `Script/Unit/Core/IOffenseQuery.cs`
- **함수:**
  - `-GetUnitsInRoom() IReadOnlyList~Unit~`

#### `IPerceptible` (interface)
- **경로:** `Script/Unit/Core/IPerceptible.cs`
- **변수/프로퍼티:**
  - `-Vector2Int PerceptiblePosition get_set`
  - `-int PerceptibleFloor get_set`
  - `-float PerceptibleStealth get_set`
  - `-float PerceptibleSpotting get_set`
  - `-FactionData PerceptibleFactionData get_set`
  - `-IFactionBehavior PerceptibleFactionBehavior get_set`
  - `-bool IsPerceptibleSpecialUnit get_set`

#### `ITargetable` (interface)
- **경로:** `Script/Unit/Core/ITargetable.cs`
- **변수/프로퍼티:**
  - `-Vector2Int TargetPosition get_set`
  - `-int TargetFloor get_set`
  - `-float TargetHp get_set`
  - `-FactionData TargetFactionData get_set`
- **함수:**
  - `-TakePhysicalDamage() void`
  - `-TakeMagicalDamage() void`
  - `-TakeMentalDamage() void`
  - `-ApplyDirectDamage() void`

#### `IUnitComponent` (interface)
- **경로:** `Script/Unit/Core/IUnitComponent.cs`
- **함수:**
  - `-OnUpdate() void`
  - `-OnDespawn() void`

#### `MemoryComponent` (class)
- **경로:** `Script/Unit/Core/MemoryComponent.cs`
- **상속/인터페이스:** `IUnitComponent`
- **변수/프로퍼티:**
  - `-Unit _owner`
  - `+PersonalMapKnowledge personalMap`
  - `+List~string~ collectedObjects`
- **함수:**
  - `-MemoryComponent() public`
  - `-MemoryComponent() public`
  - `+OnUpdate() void`
  - `+OnDespawn() void`

#### `OffenseProcessor` (class)
- **경로:** `Script/Unit/Core/OffenseProcessor.cs`
- **변수/프로퍼티:**
  - `-IMapColorizer _colorizer`
  - `-bool hasPlayer`
  - `-bool hasWild`
  - `-var roomUnits`
  - `+Room currentOffenseRoom get_set`
- **함수:**
  - `-OffenseProcessor() public`
  - `+StartOffense() void`
  - `+UpdateProcess() void`
  - `-FailOffense() void`
  - `-OnOffenseSuccess() void`

#### `PartyComponent` (class)
- **경로:** `Script/Unit/Core/PartyComponent.cs`
- **상속/인터페이스:** `IUnitComponent`
- **변수/프로퍼티:**
  - `-Unit _owner`
  - `+Party party`
- **함수:**
  - `-PartyComponent() public`
  - `-PartyComponent() public`
  - `+OnUpdate() void`
  - `+OnDespawn() void`

#### `PerceptionComponent` (class)
- **경로:** `Script/Unit/Core/PerceptionComponent.cs`
- **상속/인터페이스:** `IUnitComponent`
- **변수/프로퍼티:**
  - `-Unit _owner`
  - `+UnitPerceptionState State`
  - `+bool IsAlert`
  - `+int AlertRecordCount get_set`
- **함수:**
  - `-PerceptionComponent() public`
  - `-PerceptionComponent() public`
  - `+NotifyPerceptionSuspiciousChanged() void`
  - `+RemovePerceptionRecord() void`
  - `+OnUpdate() void`
  - `+OnDespawn() void`

#### `PlayerMonsterBehavior` (class)
- **경로:** `Script/Unit/Core/PlayerMonsterBehavior.cs`
- **상속/인터페이스:** `IFactionBehavior`
- **함수:**
  - `+IsEnemy() bool`
  - `+OnUpdate() void`
  - `+OnDeath() void`
  - `+OnEnterRoom() void`

#### `ResourceAccumulator` (class)
- **경로:** `Script/Unit/Core/ResourceAccumulator.cs`
- **변수/프로퍼티:**
  - `-ResourceAccumulator _instance`
  - `-return _instance`
  - `-int _accumulatedResourceB`
  - `+int AccumulatedResourceB`
  - `+ResourceAccumulator Instance get_set`
- **함수:**
  - `+AccumulateResourceB() void`
  - `+CommitResourceB() void`
  - `+ClearResourceB() void`

#### `RoomType` (enum)
- **경로:** `Script/Unit/Core/Room.cs`
- **변수/프로퍼티:**
  - `-int rx`
  - `-int ry`
  - `-List~Unit~ _containedUnits`
  - `+IReadOnlyList~Unit~ ContainedUnits`
  - `+string RoomName get_set`
  - `+FactionType RoomFaction get_set`
  - `+RoomType Type get_set`
  - `+RectInt Bounds get_set`
  - `+bool HasActiveSpawner get_set`
- **함수:**
  - `+GetRandomPosInRoom() Vector2Int`
  - `+AddUnit() void`
  - `+RemoveUnit() void`

#### `Room` (class)
- **경로:** `Script/Unit/Core/Room.cs`
- **변수/프로퍼티:**
  - `-int rx`
  - `-int ry`
  - `-List~Unit~ _containedUnits`
  - `+IReadOnlyList~Unit~ ContainedUnits`
  - `+string RoomName get_set`
  - `+FactionType RoomFaction get_set`
  - `+RoomType Type get_set`
  - `+RectInt Bounds get_set`
  - `+bool HasActiveSpawner get_set`
- **함수:**
  - `+GetRandomPosInRoom() Vector2Int`
  - `+AddUnit() void`
  - `+RemoveUnit() void`

#### `StatusEffectsComponent` (class)
- **경로:** `Script/Unit/Core/StatusEffectsComponent.cs`
- **상속/인터페이스:** `IUnitComponent`
- **변수/프로퍼티:**
  - `-Unit _owner`
  - `+UnitStatusEffects State`
- **함수:**
  - `-StatusEffectsComponent() public`
  - `-StatusEffectsComponent() public`
  - `+OnUpdate() void`
  - `+OnDespawn() void`

#### `Unit` (class)
- **경로:** `Script/Unit/Core/Unit.cs`
- **상속/인터페이스:** `ScriptableObject`
- **변수/프로퍼티:**
  - `+List~IUnitComponent~ Components`
  - `-return null`
  - `-HealthComponent _healthComp`
  - `-CombatStateComponent _combatStateComp`
  - `-CombatStatComponent _combatStatComp`
  - `-PerceptionComponent _perceptionComp`
  - `-VisionStatComponent _visionStatComp`
  - `-BaseStatComponent _baseStatComp`
  - `-StatusEffectsComponent _statusEffectsComp`
  - `-AIStateComponent _aiStateComp`
  - `-MemoryComponent _memoryComp`
  - `-PartyComponent _partyComp`
  - `+HealthComponent Health`
  - `+CombatStateComponent CombatState`
  - `+CombatStatComponent CombatStat`
- **함수:**
  - `-OnEnable() void`
  - `+HasPerceivedThreatCollider() bool`
  - `-Normalize() float`
  - `+CalculateDerivedStats() void`
  - `+SetupStats() void`
  - `+GetDirRotation() Quaternion`
  - `+TakeDamage() void`
  - `+TakePhysicalDamage() void`
  - `+TakeMagicalDamage() void`
  - `+TakeMentalDamage() void`
  - `+ApplyStun() void`
  - `+ApplySlow() void`
  - `+ApplyPoison() void`
  - `+ApplyBurn() void`
  - `+GetDirVector() Vector2Int`

#### `Human` (class)
- **경로:** `Script/Unit/Core/Unit.cs`
- **상속/인터페이스:** `UnitFunction`
- **변수/프로퍼티:**
  - `+List~IUnitComponent~ Components`
  - `-return null`
  - `-HealthComponent _healthComp`
  - `-CombatStateComponent _combatStateComp`
  - `-CombatStatComponent _combatStatComp`
  - `-PerceptionComponent _perceptionComp`
  - `-VisionStatComponent _visionStatComp`
  - `-BaseStatComponent _baseStatComp`
  - `-StatusEffectsComponent _statusEffectsComp`
  - `-AIStateComponent _aiStateComp`
  - `-MemoryComponent _memoryComp`
  - `-PartyComponent _partyComp`
  - `+HealthComponent Health`
  - `+CombatStateComponent CombatState`
  - `+CombatStatComponent CombatStat`
- **함수:**
  - `-OnEnable() void`
  - `+HasPerceivedThreatCollider() bool`
  - `-Normalize() float`
  - `+CalculateDerivedStats() void`
  - `+SetupStats() void`
  - `+GetDirRotation() Quaternion`
  - `+TakeDamage() void`
  - `+TakePhysicalDamage() void`
  - `+TakeMagicalDamage() void`
  - `+TakeMentalDamage() void`
  - `+ApplyStun() void`
  - `+ApplySlow() void`
  - `+ApplyPoison() void`
  - `+ApplyBurn() void`
  - `+GetDirVector() Vector2Int`

#### `Monster` (class)
- **경로:** `Script/Unit/Core/Unit.cs`
- **상속/인터페이스:** `UnitFunction`
- **변수/프로퍼티:**
  - `+List~IUnitComponent~ Components`
  - `-return null`
  - `-HealthComponent _healthComp`
  - `-CombatStateComponent _combatStateComp`
  - `-CombatStatComponent _combatStatComp`
  - `-PerceptionComponent _perceptionComp`
  - `-VisionStatComponent _visionStatComp`
  - `-BaseStatComponent _baseStatComp`
  - `-StatusEffectsComponent _statusEffectsComp`
  - `-AIStateComponent _aiStateComp`
  - `-MemoryComponent _memoryComp`
  - `-PartyComponent _partyComp`
  - `+HealthComponent Health`
  - `+CombatStateComponent CombatState`
  - `+CombatStatComponent CombatStat`
- **함수:**
  - `-OnEnable() void`
  - `+HasPerceivedThreatCollider() bool`
  - `-Normalize() float`
  - `+CalculateDerivedStats() void`
  - `+SetupStats() void`
  - `+GetDirRotation() Quaternion`
  - `+TakeDamage() void`
  - `+TakePhysicalDamage() void`
  - `+TakeMagicalDamage() void`
  - `+TakeMentalDamage() void`
  - `+ApplyStun() void`
  - `+ApplySlow() void`
  - `+ApplyPoison() void`
  - `+ApplyBurn() void`
  - `+GetDirVector() Vector2Int`

#### `UnitStatusEffects` (struct)
- **경로:** `Script/Unit/Core/UnitComponents.cs`
- **변수/프로퍼티:**
  - `+float stunDuration`
  - `+float slowDuration`
  - `+float poisonDuration`
  - `+float burnDuration`
  - `+float physicalAttackSpeed`
  - `+float magicalCastSpeed`
  - `+float actionCooldown`
  - `+float_Arr skillCooldowns`
  - `+bool isHitThisTurn`
  - `+bool oneTimeReactUsed`
  - `+float currentReactionWindow`
  - `+float evadeCooldown`
  - `+bool isCastingAttack`
  - `+float castTimer`
  - `+bool suppressHitVFX`

#### `UnitCombatState` (struct)
- **경로:** `Script/Unit/Core/UnitComponents.cs`
- **변수/프로퍼티:**
  - `+float stunDuration`
  - `+float slowDuration`
  - `+float poisonDuration`
  - `+float burnDuration`
  - `+float physicalAttackSpeed`
  - `+float magicalCastSpeed`
  - `+float actionCooldown`
  - `+float_Arr skillCooldowns`
  - `+bool isHitThisTurn`
  - `+bool oneTimeReactUsed`
  - `+float currentReactionWindow`
  - `+float evadeCooldown`
  - `+bool isCastingAttack`
  - `+float castTimer`
  - `+bool suppressHitVFX`

#### `UnitPerceptionState` (struct)
- **경로:** `Script/Unit/Core/UnitComponents.cs`
- **변수/프로퍼티:**
  - `+float stunDuration`
  - `+float slowDuration`
  - `+float poisonDuration`
  - `+float burnDuration`
  - `+float physicalAttackSpeed`
  - `+float magicalCastSpeed`
  - `+float actionCooldown`
  - `+float_Arr skillCooldowns`
  - `+bool isHitThisTurn`
  - `+bool oneTimeReactUsed`
  - `+float currentReactionWindow`
  - `+float evadeCooldown`
  - `+bool isCastingAttack`
  - `+float castTimer`
  - `+bool suppressHitVFX`

#### `UnitAIWeightState` (struct)
- **경로:** `Script/Unit/Core/UnitComponents.cs`
- **변수/프로퍼티:**
  - `+float stunDuration`
  - `+float slowDuration`
  - `+float poisonDuration`
  - `+float burnDuration`
  - `+float physicalAttackSpeed`
  - `+float magicalCastSpeed`
  - `+float actionCooldown`
  - `+float_Arr skillCooldowns`
  - `+bool isHitThisTurn`
  - `+bool oneTimeReactUsed`
  - `+float currentReactionWindow`
  - `+float evadeCooldown`
  - `+bool isCastingAttack`
  - `+float castTimer`
  - `+bool suppressHitVFX`

#### `UnitFunction` (class)
- **경로:** `Script/Unit/Core/UnitFunction.cs`
- **상속/인터페이스:** `Unit, IVisionContext`
- **변수/프로퍼티:**
  - `-float prevHp`
  - `-float damage`
  - `-float damage`
  - `-bool defenderIsHuman`
  - `-bool attackerIsHuman`
  - `-string incidentId`
  - `-bool attackerIdentified`
  - `-bool directionKnown`
  - `-float dist`
  - `-float effectiveSpotting`
  - `-float perceptionDistance`
  - `-float perceptionAngle`
  - `-Vector2 forward`
  - `-float centerAngle`
  - `-float angleToAttackerDeg`
- **함수:**
  - `+TakeDamage() void`
  - `+TakePhysicalDamage() void`
  - `+TakeMagicalDamage() void`
  - `+RecordHitWeightEvent() void`
  - `-ForceReidentifyAttacker() void`
  - `-IsFullyBlockedTowards() bool`
  - `-IsAttackerIdentified() bool`
  - `-IsCurrentlyIdentified() return`
  - `-BroadcastWitnessEvent() void`
  - `+TakeMentalDamage() void`
  - `+ApplyStun() void`
  - `+ApplySlow() void`
  - `+ApplyPoison() void`
  - `+ApplyBurn() void`
  - `-RecordStatusWeightEvent() void`

#### `Dir` (enum)
- **경로:** `Script/Unit/Core/UnitTypes.cs`
- **변수/프로퍼티:**
  - `+string typeName`
  - `+Vector2 footprint`
  - `+float BASE_REACTION_TIME_MS`
  - `+float MIN_REACTION_TIME_MS`
  - `+float MAX_REACTION_TIME_MS`
  - `+float BLOCK_PREPARE_TIME_MS`
  - `+float DODGE_PREPARE_TIME_MS`
  - `+float BLINK_PREPARE_TIME_MS`
  - `+float PARRY_PREPARE_TIME_MS`
  - `+float MIN_DEFENSE_SUCCESS_RATE`
  - `+float MAX_DEFENSE_SUCCESS_RATE`
  - `+float BLINK_MP_COST_RATIO`
  - `+float MIN_BLOCK_DAMAGE_REDUCTION`
  - `+float MAX_BLOCK_DAMAGE_REDUCTION`
- **함수:**
  - `-Knight() public`
  - `-HumanBaseType() public`
  - `-MeleeTank() public`
  - `-WildBaseType() public`
  - `-Archer() public`

#### `UnitType` (class)
- **경로:** `Script/Unit/Core/UnitTypes.cs`
- **변수/프로퍼티:**
  - `+string typeName`
  - `+Vector2 footprint`
  - `+float BASE_REACTION_TIME_MS`
  - `+float MIN_REACTION_TIME_MS`
  - `+float MAX_REACTION_TIME_MS`
  - `+float BLOCK_PREPARE_TIME_MS`
  - `+float DODGE_PREPARE_TIME_MS`
  - `+float BLINK_PREPARE_TIME_MS`
  - `+float PARRY_PREPARE_TIME_MS`
  - `+float MIN_DEFENSE_SUCCESS_RATE`
  - `+float MAX_DEFENSE_SUCCESS_RATE`
  - `+float BLINK_MP_COST_RATIO`
  - `+float MIN_BLOCK_DAMAGE_REDUCTION`
  - `+float MAX_BLOCK_DAMAGE_REDUCTION`
- **함수:**
  - `-Knight() public`
  - `-HumanBaseType() public`
  - `-MeleeTank() public`
  - `-WildBaseType() public`
  - `-Archer() public`

#### `Knight` (class)
- **경로:** `Script/Unit/Core/UnitTypes.cs`
- **상속/인터페이스:** `UnitType`
- **변수/프로퍼티:**
  - `+string typeName`
  - `+Vector2 footprint`
  - `+float BASE_REACTION_TIME_MS`
  - `+float MIN_REACTION_TIME_MS`
  - `+float MAX_REACTION_TIME_MS`
  - `+float BLOCK_PREPARE_TIME_MS`
  - `+float DODGE_PREPARE_TIME_MS`
  - `+float BLINK_PREPARE_TIME_MS`
  - `+float PARRY_PREPARE_TIME_MS`
  - `+float MIN_DEFENSE_SUCCESS_RATE`
  - `+float MAX_DEFENSE_SUCCESS_RATE`
  - `+float BLINK_MP_COST_RATIO`
  - `+float MIN_BLOCK_DAMAGE_REDUCTION`
  - `+float MAX_BLOCK_DAMAGE_REDUCTION`
- **함수:**
  - `-Knight() public`
  - `-HumanBaseType() public`
  - `-MeleeTank() public`
  - `-WildBaseType() public`
  - `-Archer() public`

#### `HumanBaseType` (class)
- **경로:** `Script/Unit/Core/UnitTypes.cs`
- **상속/인터페이스:** `UnitType`
- **변수/프로퍼티:**
  - `+string typeName`
  - `+Vector2 footprint`
  - `+float BASE_REACTION_TIME_MS`
  - `+float MIN_REACTION_TIME_MS`
  - `+float MAX_REACTION_TIME_MS`
  - `+float BLOCK_PREPARE_TIME_MS`
  - `+float DODGE_PREPARE_TIME_MS`
  - `+float BLINK_PREPARE_TIME_MS`
  - `+float PARRY_PREPARE_TIME_MS`
  - `+float MIN_DEFENSE_SUCCESS_RATE`
  - `+float MAX_DEFENSE_SUCCESS_RATE`
  - `+float BLINK_MP_COST_RATIO`
  - `+float MIN_BLOCK_DAMAGE_REDUCTION`
  - `+float MAX_BLOCK_DAMAGE_REDUCTION`
- **함수:**
  - `-Knight() public`
  - `-HumanBaseType() public`
  - `-MeleeTank() public`
  - `-WildBaseType() public`
  - `-Archer() public`

#### `MeleeTank` (class)
- **경로:** `Script/Unit/Core/UnitTypes.cs`
- **상속/인터페이스:** `UnitType`
- **변수/프로퍼티:**
  - `+string typeName`
  - `+Vector2 footprint`
  - `+float BASE_REACTION_TIME_MS`
  - `+float MIN_REACTION_TIME_MS`
  - `+float MAX_REACTION_TIME_MS`
  - `+float BLOCK_PREPARE_TIME_MS`
  - `+float DODGE_PREPARE_TIME_MS`
  - `+float BLINK_PREPARE_TIME_MS`
  - `+float PARRY_PREPARE_TIME_MS`
  - `+float MIN_DEFENSE_SUCCESS_RATE`
  - `+float MAX_DEFENSE_SUCCESS_RATE`
  - `+float BLINK_MP_COST_RATIO`
  - `+float MIN_BLOCK_DAMAGE_REDUCTION`
  - `+float MAX_BLOCK_DAMAGE_REDUCTION`
- **함수:**
  - `-Knight() public`
  - `-HumanBaseType() public`
  - `-MeleeTank() public`
  - `-WildBaseType() public`
  - `-Archer() public`

#### `WildBaseType` (class)
- **경로:** `Script/Unit/Core/UnitTypes.cs`
- **상속/인터페이스:** `UnitType`
- **변수/프로퍼티:**
  - `+string typeName`
  - `+Vector2 footprint`
  - `+float BASE_REACTION_TIME_MS`
  - `+float MIN_REACTION_TIME_MS`
  - `+float MAX_REACTION_TIME_MS`
  - `+float BLOCK_PREPARE_TIME_MS`
  - `+float DODGE_PREPARE_TIME_MS`
  - `+float BLINK_PREPARE_TIME_MS`
  - `+float PARRY_PREPARE_TIME_MS`
  - `+float MIN_DEFENSE_SUCCESS_RATE`
  - `+float MAX_DEFENSE_SUCCESS_RATE`
  - `+float BLINK_MP_COST_RATIO`
  - `+float MIN_BLOCK_DAMAGE_REDUCTION`
  - `+float MAX_BLOCK_DAMAGE_REDUCTION`
- **함수:**
  - `-Knight() public`
  - `-HumanBaseType() public`
  - `-MeleeTank() public`
  - `-WildBaseType() public`
  - `-Archer() public`

#### `Archer` (class)
- **경로:** `Script/Unit/Core/UnitTypes.cs`
- **상속/인터페이스:** `UnitType`
- **변수/프로퍼티:**
  - `+string typeName`
  - `+Vector2 footprint`
  - `+float BASE_REACTION_TIME_MS`
  - `+float MIN_REACTION_TIME_MS`
  - `+float MAX_REACTION_TIME_MS`
  - `+float BLOCK_PREPARE_TIME_MS`
  - `+float DODGE_PREPARE_TIME_MS`
  - `+float BLINK_PREPARE_TIME_MS`
  - `+float PARRY_PREPARE_TIME_MS`
  - `+float MIN_DEFENSE_SUCCESS_RATE`
  - `+float MAX_DEFENSE_SUCCESS_RATE`
  - `+float BLINK_MP_COST_RATIO`
  - `+float MIN_BLOCK_DAMAGE_REDUCTION`
  - `+float MAX_BLOCK_DAMAGE_REDUCTION`
- **함수:**
  - `-Knight() public`
  - `-HumanBaseType() public`
  - `-MeleeTank() public`
  - `-WildBaseType() public`
  - `-Archer() public`

#### `CombatConstants` (class)
- **경로:** `Script/Unit/Core/UnitTypes.cs`
- **변수/프로퍼티:**
  - `+string typeName`
  - `+Vector2 footprint`
  - `+float BASE_REACTION_TIME_MS`
  - `+float MIN_REACTION_TIME_MS`
  - `+float MAX_REACTION_TIME_MS`
  - `+float BLOCK_PREPARE_TIME_MS`
  - `+float DODGE_PREPARE_TIME_MS`
  - `+float BLINK_PREPARE_TIME_MS`
  - `+float PARRY_PREPARE_TIME_MS`
  - `+float MIN_DEFENSE_SUCCESS_RATE`
  - `+float MAX_DEFENSE_SUCCESS_RATE`
  - `+float BLINK_MP_COST_RATIO`
  - `+float MIN_BLOCK_DAMAGE_REDUCTION`
  - `+float MAX_BLOCK_DAMAGE_REDUCTION`
- **함수:**
  - `-Knight() public`
  - `-HumanBaseType() public`
  - `-MeleeTank() public`
  - `-WildBaseType() public`
  - `-Archer() public`

#### `VisionStatComponent` (class)
- **경로:** `Script/Unit/Core/VisionStatComponent.cs`
- **상속/인터페이스:** `IUnitComponent`
- **변수/프로퍼티:**
  - `-Unit _owner`
  - `+float stealth`
  - `+float spotting`
  - `+float baseVisibility`
  - `+float attackVisibilityBoostTimer`
- **함수:**
  - `-VisionStatComponent() public`
  - `-VisionStatComponent() public`
  - `+OnUpdate() void`
  - `+OnDespawn() void`

#### `WildBaseSpawnerComponent` (class)
- **경로:** `Script/Unit/Core/WildBaseSpawnerComponent.cs`
- **상속/인터페이스:** `IUnitComponent`
- **변수/프로퍼티:**
  - `+GameObject debugVisual`
  - `-Unit _owner`
  - `-Room _targetRoom`
  - `-float _spawnInterval`
  - `-CancellationTokenSource _cts`
  - `-UnitType monsterType`
  - `-Vector2Int spawnPos`
  - `-int attempts`
  - `-Monster monster`
- **함수:**
  - `-WildBaseSpawnerComponent() public`
  - `-WildBaseSpawnerComponent() public`
  - `-SpawnLoop() UniTaskVoid`
  - `+OnUpdate() void`
  - `-SpawnMonster() void`
  - `+OnDespawn() void`

#### `WildMonsterBehavior` (class)
- **경로:** `Script/Unit/Core/WildMonsterBehavior.cs`
- **상속/인터페이스:** `IFactionBehavior`
- **변수/프로퍼티:**
  - `-int rewardAmount`
- **함수:**
  - `+IsEnemy() bool`
  - `+OnUpdate() void`
  - `+OnDeath() void`
  - `+OnEnterRoom() void`

