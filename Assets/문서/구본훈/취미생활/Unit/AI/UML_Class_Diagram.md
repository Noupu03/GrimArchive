# Package `Unit/AI` UML Class Diagram

**소스 경로:** `Assets/Unit/AI`

```mermaid
classDiagram
    class Action_Panic {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_Panic
    class Action_MoveToPlayerTarget {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_MoveToPlayerTarget
    class Action_CompletePlayerCommand {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_CompletePlayerCommand
    class Action_MoveToStairs {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_MoveToStairs
    class Action_CrossStairs {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_CrossStairs
    class Action_RandomExplore {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_RandomExplore
    class Action_EngageEnemy {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_EngageEnemy
    class Action_TrapJoinWait {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_TrapJoinWait
    class Action_MoveToTrap {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_MoveToTrap
    class Action_TrapDisarmPerform {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_TrapDisarmPerform
    class Action_TrapBypass {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_TrapBypass
    class Action_TrapPass {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_TrapPass
    class Action_TrapDestroy {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_TrapDestroy
    class Action_MoveToInvestigateTarget {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_MoveToInvestigateTarget
    class Action_InvestigatePerform {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_InvestigatePerform
    class Action_AlertApproach {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_AlertApproach
    class Action_AlertPerimeterSearch {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_AlertPerimeterSearch
    class Action_Wait {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_Wait
    class Action_MoveToEscortSlotMelee {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_MoveToEscortSlotMelee
    class Action_MoveToEscortSlotRanged {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_MoveToEscortSlotRanged
    class Action_HoldFormation {
        -Dir randomDir
        -Vector2Int target
        -Vector3Int interactPos
        -bool isTrace
        -Vector2Int approachPos
        -Vector2Int best
        -int bestScore
        -bool occupiedByOther
        -Vector2Int diff
        -int dist
        -Action_Panic() public
        +Execute() void
        -Action_MoveToPlayerTarget() public
        -Action_CompletePlayerCommand() public
        -Action_MoveToStairs() public
        -PickFreeApproachTile() Vector2Int
        -Action_CrossStairs() public
        -Action_RandomExplore() public
        -Action_EngageEnemy() public
        -ExecuteSkillActionBased() void
    }
    GoapAction <|-- Action_HoldFormation
    class Goal_Panic {
        -return 0f
        -return 99f
        -return 140f
        -IEnumerable~Unit~ enemies
        -return 100f
        -return false
        -return 90f
        -return 94f
        -return 91f
        -Goal_Panic() public
        +GetPriority() float
        -Goal_PlayerCommand() public
        -Goal_UseStairs() public
        -Goal_DefeatEnemy() public
        -Goal_Explore() public
        -Goal_TrapResponse() public
        +ShouldInterrupt() bool
        -Goal_Alert() public
        -Goal_Investigate() public
    }
    GoapGoal <|-- Goal_Panic
    class Goal_PlayerCommand {
        -return 0f
        -return 99f
        -return 140f
        -IEnumerable~Unit~ enemies
        -return 100f
        -return false
        -return 90f
        -return 94f
        -return 91f
        -Goal_Panic() public
        +GetPriority() float
        -Goal_PlayerCommand() public
        -Goal_UseStairs() public
        -Goal_DefeatEnemy() public
        -Goal_Explore() public
        -Goal_TrapResponse() public
        +ShouldInterrupt() bool
        -Goal_Alert() public
        -Goal_Investigate() public
    }
    GoapGoal <|-- Goal_PlayerCommand
    class Goal_UseStairs {
        -return 0f
        -return 99f
        -return 140f
        -IEnumerable~Unit~ enemies
        -return 100f
        -return false
        -return 90f
        -return 94f
        -return 91f
        -Goal_Panic() public
        +GetPriority() float
        -Goal_PlayerCommand() public
        -Goal_UseStairs() public
        -Goal_DefeatEnemy() public
        -Goal_Explore() public
        -Goal_TrapResponse() public
        +ShouldInterrupt() bool
        -Goal_Alert() public
        -Goal_Investigate() public
    }
    GoapGoal <|-- Goal_UseStairs
    class Goal_DefeatEnemy {
        -return 0f
        -return 99f
        -return 140f
        -IEnumerable~Unit~ enemies
        -return 100f
        -return false
        -return 90f
        -return 94f
        -return 91f
        -Goal_Panic() public
        +GetPriority() float
        -Goal_PlayerCommand() public
        -Goal_UseStairs() public
        -Goal_DefeatEnemy() public
        -Goal_Explore() public
        -Goal_TrapResponse() public
        +ShouldInterrupt() bool
        -Goal_Alert() public
        -Goal_Investigate() public
    }
    GoapGoal <|-- Goal_DefeatEnemy
    class Goal_Explore {
        -return 0f
        -return 99f
        -return 140f
        -IEnumerable~Unit~ enemies
        -return 100f
        -return false
        -return 90f
        -return 94f
        -return 91f
        -Goal_Panic() public
        +GetPriority() float
        -Goal_PlayerCommand() public
        -Goal_UseStairs() public
        -Goal_DefeatEnemy() public
        -Goal_Explore() public
        -Goal_TrapResponse() public
        +ShouldInterrupt() bool
        -Goal_Alert() public
        -Goal_Investigate() public
    }
    GoapGoal <|-- Goal_Explore
    class Goal_TrapResponse {
        -return 0f
        -return 99f
        -return 140f
        -IEnumerable~Unit~ enemies
        -return 100f
        -return false
        -return 90f
        -return 94f
        -return 91f
        -Goal_Panic() public
        +GetPriority() float
        -Goal_PlayerCommand() public
        -Goal_UseStairs() public
        -Goal_DefeatEnemy() public
        -Goal_Explore() public
        -Goal_TrapResponse() public
        +ShouldInterrupt() bool
        -Goal_Alert() public
        -Goal_Investigate() public
    }
    GoapGoal <|-- Goal_TrapResponse
    class Goal_Alert {
        -return 0f
        -return 99f
        -return 140f
        -IEnumerable~Unit~ enemies
        -return 100f
        -return false
        -return 90f
        -return 94f
        -return 91f
        -Goal_Panic() public
        +GetPriority() float
        -Goal_PlayerCommand() public
        -Goal_UseStairs() public
        -Goal_DefeatEnemy() public
        -Goal_Explore() public
        -Goal_TrapResponse() public
        +ShouldInterrupt() bool
        -Goal_Alert() public
        -Goal_Investigate() public
    }
    GoapGoal <|-- Goal_Alert
    class Goal_Investigate {
        -return 0f
        -return 99f
        -return 140f
        -IEnumerable~Unit~ enemies
        -return 100f
        -return false
        -return 90f
        -return 94f
        -return 91f
        -Goal_Panic() public
        +GetPriority() float
        -Goal_PlayerCommand() public
        -Goal_UseStairs() public
        -Goal_DefeatEnemy() public
        -Goal_Explore() public
        -Goal_TrapResponse() public
        +ShouldInterrupt() bool
        -Goal_Alert() public
        -Goal_Investigate() public
    }
    GoapGoal <|-- Goal_Investigate
    class Goal_Wait {
        -return 0f
        -return 99f
        -return 140f
        -IEnumerable~Unit~ enemies
        -return 100f
        -return false
        -return 90f
        -return 94f
        -return 91f
        -Goal_Panic() public
        +GetPriority() float
        -Goal_PlayerCommand() public
        -Goal_UseStairs() public
        -Goal_DefeatEnemy() public
        -Goal_Explore() public
        -Goal_TrapResponse() public
        +ShouldInterrupt() bool
        -Goal_Alert() public
        -Goal_Investigate() public
    }
    GoapGoal <|-- Goal_Wait
    class Goal_ProtectiveFormation {
        -return 0f
        -return 99f
        -return 140f
        -IEnumerable~Unit~ enemies
        -return 100f
        -return false
        -return 90f
        -return 94f
        -return 91f
        -Goal_Panic() public
        +GetPriority() float
        -Goal_PlayerCommand() public
        -Goal_UseStairs() public
        -Goal_DefeatEnemy() public
        -Goal_Explore() public
        -Goal_TrapResponse() public
        +ShouldInterrupt() bool
        -Goal_Alert() public
        -Goal_Investigate() public
    }
    GoapGoal <|-- Goal_ProtectiveFormation
    class GoapState {
        +string Name
        +GoapState DesiredState
        +string ActionName
        +float Cost
        +GoapState Preconditions
        +GoapState Effects
        -IEnumerable~Unit~ enemies
        -Unit target
        -float d
        -return target
        +GetPriority() float
        +IsValid() bool
        +Execute() void
        #GetClosestEnemy() Unit
        #MoveTowardsPos() void
        #MoveAwayFromTarget() void
        #MoveToEscortSlot() void
        -GoapBrain() public
        +PlanText() string
        +JudgeState() void
    }
    GoapState --> GoapGoal
    GoapState --> GoapAction
    class GoapGoal {
        +string Name
        +GoapState DesiredState
        +string ActionName
        +float Cost
        +GoapState Preconditions
        +GoapState Effects
        -IEnumerable~Unit~ enemies
        -Unit target
        -float d
        -return target
        +GetPriority() float
        +IsValid() bool
        +Execute() void
        #GetClosestEnemy() Unit
        #MoveTowardsPos() void
        #MoveAwayFromTarget() void
        #MoveToEscortSlot() void
        -GoapBrain() public
        +PlanText() string
        +JudgeState() void
    }
    GoapGoal --> GoapState
    GoapGoal --> GoapAction
    class GoapAction {
        +string Name
        +GoapState DesiredState
        +string ActionName
        +float Cost
        +GoapState Preconditions
        +GoapState Effects
        -IEnumerable~Unit~ enemies
        -Unit target
        -float d
        -return target
        +GetPriority() float
        +IsValid() bool
        +Execute() void
        #GetClosestEnemy() Unit
        #MoveTowardsPos() void
        #MoveAwayFromTarget() void
        #MoveToEscortSlot() void
        -GoapBrain() public
        +PlanText() string
        +JudgeState() void
    }
    GoapAction --> GoapGoal
    GoapAction --> GoapState
    class GoapBrain {
        +string Name
        +GoapState DesiredState
        +string ActionName
        +float Cost
        +GoapState Preconditions
        +GoapState Effects
        -IEnumerable~Unit~ enemies
        -Unit target
        -float d
        -return target
        +GetPriority() float
        +IsValid() bool
        +Execute() void
        #GetClosestEnemy() Unit
        #MoveTowardsPos() void
        #MoveAwayFromTarget() void
        #MoveToEscortSlot() void
        -GoapBrain() public
        +PlanText() string
        +JudgeState() void
    }
    GoapBrain --> GoapGoal
    GoapBrain --> GoapState
    class GoapPlanner {
        -int MaxDepth
        +GoapState State
        +List~GoapAction~ Path
        +float Cost
        -var validActions
        -var frontier
        -var bestCost
        -int bestIdx
        -Node node
        -string nodeKey
        +Plan() List~GoapAction~
        +ApplyAll() GoapState
        -IsSatisfied() bool
        -Satisfies() bool
        -Apply() GoapState
        -Serialize() string
    }
    GoapPlanner --> GoapState
    GoapPlanner --> GoapAction
    GoapPlanner --> Node
    class Node {
        -int MaxDepth
        +GoapState State
        +List~GoapAction~ Path
        +float Cost
        -var validActions
        -var frontier
        -var bestCost
        -int bestIdx
        -Node node
        -string nodeKey
        +Plan() List~GoapAction~
        +ApplyAll() GoapState
        -IsSatisfied() bool
        -Satisfies() bool
        -Apply() GoapState
        -Serialize() string
    }
    Node --> GoapState
    Node --> GoapAction
    class GoapWorldState {
        +int StairArrivalRadius
        -int dx
        -int dy
        -var state
        -bool enemyVisible
        -var trap
        -bool investigateNeeded
        -bool atEscortSlot
        -float backDistance
        -Vector2Int slot
        +DistanceToStairBlock() int
        +Build() GoapState
    }
```

### 📋 스크립트 클래스 명세

#### `Action_Panic` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_MoveToPlayerTarget` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_CompletePlayerCommand` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_MoveToStairs` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_CrossStairs` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_RandomExplore` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_EngageEnemy` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_TrapJoinWait` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_MoveToTrap` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_TrapDisarmPerform` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_TrapBypass` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_TrapPass` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_TrapDestroy` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_MoveToInvestigateTarget` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_InvestigatePerform` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_AlertApproach` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_AlertPerimeterSearch` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_Wait` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_MoveToEscortSlotMelee` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_MoveToEscortSlotRanged` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Action_HoldFormation` (class)
- **경로:** `Script/Unit/AI/Actions.cs`
- **상속/인터페이스:** `GoapAction`
- **변수/프로퍼티:**
  - `-Dir randomDir`
  - `-Vector2Int target`
  - `-Vector3Int interactPos`
  - `-bool isTrace`
  - `-Vector2Int approachPos`
  - `-Vector2Int best`
  - `-int bestScore`
  - `-bool occupiedByOther`
  - `-Vector2Int diff`
  - `-int dist`
  - `-int score`
  - `-return best`
  - `-int fromFloor`
  - `-int toFloor`
  - `-Dir randomDir`
- **함수:**
  - `-Action_Panic() public`
  - `+Execute() void`
  - `-Action_MoveToPlayerTarget() public`
  - `+Execute() void`
  - `-Action_CompletePlayerCommand() public`
  - `+Execute() void`
  - `-Action_MoveToStairs() public`
  - `+Execute() void`
  - `-PickFreeApproachTile() Vector2Int`
  - `-Action_CrossStairs() public`
  - `+Execute() void`
  - `-Action_RandomExplore() public`
  - `+Execute() void`
  - `-Action_EngageEnemy() public`
  - `-ExecuteSkillActionBased() void`

#### `Goal_Panic` (class)
- **경로:** `Script/Unit/AI/Goals.cs`
- **상속/인터페이스:** `GoapGoal`
- **변수/프로퍼티:**
  - `-return 0f`
  - `-return 99f`
  - `-return 0f`
  - `-return 140f`
  - `-return 0f`
  - `-IEnumerable~Unit~ enemies`
  - `-return 100f`
  - `-return 0f`
  - `-return 140f`
  - `-return false`
  - `-return 90f`
  - `-return 94f`
  - `-return false`
  - `-return 91f`
  - `-return false`
- **함수:**
  - `-Goal_Panic() public`
  - `+GetPriority() float`
  - `-Goal_PlayerCommand() public`
  - `+GetPriority() float`
  - `-Goal_UseStairs() public`
  - `+GetPriority() float`
  - `-Goal_DefeatEnemy() public`
  - `+GetPriority() float`
  - `-Goal_Explore() public`
  - `-Goal_TrapResponse() public`
  - `+GetPriority() float`
  - `+ShouldInterrupt() bool`
  - `-Goal_Alert() public`
  - `+GetPriority() float`
  - `-Goal_Investigate() public`

#### `Goal_PlayerCommand` (class)
- **경로:** `Script/Unit/AI/Goals.cs`
- **상속/인터페이스:** `GoapGoal`
- **변수/프로퍼티:**
  - `-return 0f`
  - `-return 99f`
  - `-return 0f`
  - `-return 140f`
  - `-return 0f`
  - `-IEnumerable~Unit~ enemies`
  - `-return 100f`
  - `-return 0f`
  - `-return 140f`
  - `-return false`
  - `-return 90f`
  - `-return 94f`
  - `-return false`
  - `-return 91f`
  - `-return false`
- **함수:**
  - `-Goal_Panic() public`
  - `+GetPriority() float`
  - `-Goal_PlayerCommand() public`
  - `+GetPriority() float`
  - `-Goal_UseStairs() public`
  - `+GetPriority() float`
  - `-Goal_DefeatEnemy() public`
  - `+GetPriority() float`
  - `-Goal_Explore() public`
  - `-Goal_TrapResponse() public`
  - `+GetPriority() float`
  - `+ShouldInterrupt() bool`
  - `-Goal_Alert() public`
  - `+GetPriority() float`
  - `-Goal_Investigate() public`

#### `Goal_UseStairs` (class)
- **경로:** `Script/Unit/AI/Goals.cs`
- **상속/인터페이스:** `GoapGoal`
- **변수/프로퍼티:**
  - `-return 0f`
  - `-return 99f`
  - `-return 0f`
  - `-return 140f`
  - `-return 0f`
  - `-IEnumerable~Unit~ enemies`
  - `-return 100f`
  - `-return 0f`
  - `-return 140f`
  - `-return false`
  - `-return 90f`
  - `-return 94f`
  - `-return false`
  - `-return 91f`
  - `-return false`
- **함수:**
  - `-Goal_Panic() public`
  - `+GetPriority() float`
  - `-Goal_PlayerCommand() public`
  - `+GetPriority() float`
  - `-Goal_UseStairs() public`
  - `+GetPriority() float`
  - `-Goal_DefeatEnemy() public`
  - `+GetPriority() float`
  - `-Goal_Explore() public`
  - `-Goal_TrapResponse() public`
  - `+GetPriority() float`
  - `+ShouldInterrupt() bool`
  - `-Goal_Alert() public`
  - `+GetPriority() float`
  - `-Goal_Investigate() public`

#### `Goal_DefeatEnemy` (class)
- **경로:** `Script/Unit/AI/Goals.cs`
- **상속/인터페이스:** `GoapGoal`
- **변수/프로퍼티:**
  - `-return 0f`
  - `-return 99f`
  - `-return 0f`
  - `-return 140f`
  - `-return 0f`
  - `-IEnumerable~Unit~ enemies`
  - `-return 100f`
  - `-return 0f`
  - `-return 140f`
  - `-return false`
  - `-return 90f`
  - `-return 94f`
  - `-return false`
  - `-return 91f`
  - `-return false`
- **함수:**
  - `-Goal_Panic() public`
  - `+GetPriority() float`
  - `-Goal_PlayerCommand() public`
  - `+GetPriority() float`
  - `-Goal_UseStairs() public`
  - `+GetPriority() float`
  - `-Goal_DefeatEnemy() public`
  - `+GetPriority() float`
  - `-Goal_Explore() public`
  - `-Goal_TrapResponse() public`
  - `+GetPriority() float`
  - `+ShouldInterrupt() bool`
  - `-Goal_Alert() public`
  - `+GetPriority() float`
  - `-Goal_Investigate() public`

#### `Goal_Explore` (class)
- **경로:** `Script/Unit/AI/Goals.cs`
- **상속/인터페이스:** `GoapGoal`
- **변수/프로퍼티:**
  - `-return 0f`
  - `-return 99f`
  - `-return 0f`
  - `-return 140f`
  - `-return 0f`
  - `-IEnumerable~Unit~ enemies`
  - `-return 100f`
  - `-return 0f`
  - `-return 140f`
  - `-return false`
  - `-return 90f`
  - `-return 94f`
  - `-return false`
  - `-return 91f`
  - `-return false`
- **함수:**
  - `-Goal_Panic() public`
  - `+GetPriority() float`
  - `-Goal_PlayerCommand() public`
  - `+GetPriority() float`
  - `-Goal_UseStairs() public`
  - `+GetPriority() float`
  - `-Goal_DefeatEnemy() public`
  - `+GetPriority() float`
  - `-Goal_Explore() public`
  - `-Goal_TrapResponse() public`
  - `+GetPriority() float`
  - `+ShouldInterrupt() bool`
  - `-Goal_Alert() public`
  - `+GetPriority() float`
  - `-Goal_Investigate() public`

#### `Goal_TrapResponse` (class)
- **경로:** `Script/Unit/AI/Goals.cs`
- **상속/인터페이스:** `GoapGoal`
- **변수/프로퍼티:**
  - `-return 0f`
  - `-return 99f`
  - `-return 0f`
  - `-return 140f`
  - `-return 0f`
  - `-IEnumerable~Unit~ enemies`
  - `-return 100f`
  - `-return 0f`
  - `-return 140f`
  - `-return false`
  - `-return 90f`
  - `-return 94f`
  - `-return false`
  - `-return 91f`
  - `-return false`
- **함수:**
  - `-Goal_Panic() public`
  - `+GetPriority() float`
  - `-Goal_PlayerCommand() public`
  - `+GetPriority() float`
  - `-Goal_UseStairs() public`
  - `+GetPriority() float`
  - `-Goal_DefeatEnemy() public`
  - `+GetPriority() float`
  - `-Goal_Explore() public`
  - `-Goal_TrapResponse() public`
  - `+GetPriority() float`
  - `+ShouldInterrupt() bool`
  - `-Goal_Alert() public`
  - `+GetPriority() float`
  - `-Goal_Investigate() public`

#### `Goal_Alert` (class)
- **경로:** `Script/Unit/AI/Goals.cs`
- **상속/인터페이스:** `GoapGoal`
- **변수/프로퍼티:**
  - `-return 0f`
  - `-return 99f`
  - `-return 0f`
  - `-return 140f`
  - `-return 0f`
  - `-IEnumerable~Unit~ enemies`
  - `-return 100f`
  - `-return 0f`
  - `-return 140f`
  - `-return false`
  - `-return 90f`
  - `-return 94f`
  - `-return false`
  - `-return 91f`
  - `-return false`
- **함수:**
  - `-Goal_Panic() public`
  - `+GetPriority() float`
  - `-Goal_PlayerCommand() public`
  - `+GetPriority() float`
  - `-Goal_UseStairs() public`
  - `+GetPriority() float`
  - `-Goal_DefeatEnemy() public`
  - `+GetPriority() float`
  - `-Goal_Explore() public`
  - `-Goal_TrapResponse() public`
  - `+GetPriority() float`
  - `+ShouldInterrupt() bool`
  - `-Goal_Alert() public`
  - `+GetPriority() float`
  - `-Goal_Investigate() public`

#### `Goal_Investigate` (class)
- **경로:** `Script/Unit/AI/Goals.cs`
- **상속/인터페이스:** `GoapGoal`
- **변수/프로퍼티:**
  - `-return 0f`
  - `-return 99f`
  - `-return 0f`
  - `-return 140f`
  - `-return 0f`
  - `-IEnumerable~Unit~ enemies`
  - `-return 100f`
  - `-return 0f`
  - `-return 140f`
  - `-return false`
  - `-return 90f`
  - `-return 94f`
  - `-return false`
  - `-return 91f`
  - `-return false`
- **함수:**
  - `-Goal_Panic() public`
  - `+GetPriority() float`
  - `-Goal_PlayerCommand() public`
  - `+GetPriority() float`
  - `-Goal_UseStairs() public`
  - `+GetPriority() float`
  - `-Goal_DefeatEnemy() public`
  - `+GetPriority() float`
  - `-Goal_Explore() public`
  - `-Goal_TrapResponse() public`
  - `+GetPriority() float`
  - `+ShouldInterrupt() bool`
  - `-Goal_Alert() public`
  - `+GetPriority() float`
  - `-Goal_Investigate() public`

#### `Goal_Wait` (class)
- **경로:** `Script/Unit/AI/Goals.cs`
- **상속/인터페이스:** `GoapGoal`
- **변수/프로퍼티:**
  - `-return 0f`
  - `-return 99f`
  - `-return 0f`
  - `-return 140f`
  - `-return 0f`
  - `-IEnumerable~Unit~ enemies`
  - `-return 100f`
  - `-return 0f`
  - `-return 140f`
  - `-return false`
  - `-return 90f`
  - `-return 94f`
  - `-return false`
  - `-return 91f`
  - `-return false`
- **함수:**
  - `-Goal_Panic() public`
  - `+GetPriority() float`
  - `-Goal_PlayerCommand() public`
  - `+GetPriority() float`
  - `-Goal_UseStairs() public`
  - `+GetPriority() float`
  - `-Goal_DefeatEnemy() public`
  - `+GetPriority() float`
  - `-Goal_Explore() public`
  - `-Goal_TrapResponse() public`
  - `+GetPriority() float`
  - `+ShouldInterrupt() bool`
  - `-Goal_Alert() public`
  - `+GetPriority() float`
  - `-Goal_Investigate() public`

#### `Goal_ProtectiveFormation` (class)
- **경로:** `Script/Unit/AI/Goals.cs`
- **상속/인터페이스:** `GoapGoal`
- **변수/프로퍼티:**
  - `-return 0f`
  - `-return 99f`
  - `-return 0f`
  - `-return 140f`
  - `-return 0f`
  - `-IEnumerable~Unit~ enemies`
  - `-return 100f`
  - `-return 0f`
  - `-return 140f`
  - `-return false`
  - `-return 90f`
  - `-return 94f`
  - `-return false`
  - `-return 91f`
  - `-return false`
- **함수:**
  - `-Goal_Panic() public`
  - `+GetPriority() float`
  - `-Goal_PlayerCommand() public`
  - `+GetPriority() float`
  - `-Goal_UseStairs() public`
  - `+GetPriority() float`
  - `-Goal_DefeatEnemy() public`
  - `+GetPriority() float`
  - `-Goal_Explore() public`
  - `-Goal_TrapResponse() public`
  - `+GetPriority() float`
  - `+ShouldInterrupt() bool`
  - `-Goal_Alert() public`
  - `+GetPriority() float`
  - `-Goal_Investigate() public`

#### `GoapState` (class)
- **경로:** `Script/Unit/AI/GoapCore.cs`
- **상속/인터페이스:** `Dictionary, bool>`
- **변수/프로퍼티:**
  - `+string Name`
  - `+GoapState DesiredState`
  - `+string ActionName`
  - `+float Cost`
  - `+GoapState Preconditions`
  - `+GoapState Effects`
  - `-IEnumerable~Unit~ enemies`
  - `-Unit target`
  - `-float d`
  - `-return target`
  - `-Vector2 away`
  - `-float maxComp`
  - `-Vector2 scaled`
  - `-int targetDist`
  - `-Vector2Int retreatPos`
- **함수:**
  - `+GetPriority() float`
  - `+IsValid() bool`
  - `+Execute() void`
  - `#GetClosestEnemy() Unit`
  - `#MoveTowardsPos() void`
  - `#MoveAwayFromTarget() void`
  - `#MoveToEscortSlot() void`
  - `-GoapBrain() public`
  - `+PlanText() string`
  - `+JudgeState() void`
  - `+ExecuteAction() void`

#### `GoapGoal` (class)
- **경로:** `Script/Unit/AI/GoapCore.cs`
- **변수/프로퍼티:**
  - `+string Name`
  - `+GoapState DesiredState`
  - `+string ActionName`
  - `+float Cost`
  - `+GoapState Preconditions`
  - `+GoapState Effects`
  - `-IEnumerable~Unit~ enemies`
  - `-Unit target`
  - `-float d`
  - `-return target`
  - `-Vector2 away`
  - `-float maxComp`
  - `-Vector2 scaled`
  - `-int targetDist`
  - `-Vector2Int retreatPos`
- **함수:**
  - `+GetPriority() float`
  - `+IsValid() bool`
  - `+Execute() void`
  - `#GetClosestEnemy() Unit`
  - `#MoveTowardsPos() void`
  - `#MoveAwayFromTarget() void`
  - `#MoveToEscortSlot() void`
  - `-GoapBrain() public`
  - `+PlanText() string`
  - `+JudgeState() void`
  - `+ExecuteAction() void`

#### `GoapAction` (class)
- **경로:** `Script/Unit/AI/GoapCore.cs`
- **변수/프로퍼티:**
  - `+string Name`
  - `+GoapState DesiredState`
  - `+string ActionName`
  - `+float Cost`
  - `+GoapState Preconditions`
  - `+GoapState Effects`
  - `-IEnumerable~Unit~ enemies`
  - `-Unit target`
  - `-float d`
  - `-return target`
  - `-Vector2 away`
  - `-float maxComp`
  - `-Vector2 scaled`
  - `-int targetDist`
  - `-Vector2Int retreatPos`
- **함수:**
  - `+GetPriority() float`
  - `+IsValid() bool`
  - `+Execute() void`
  - `#GetClosestEnemy() Unit`
  - `#MoveTowardsPos() void`
  - `#MoveAwayFromTarget() void`
  - `#MoveToEscortSlot() void`
  - `-GoapBrain() public`
  - `+PlanText() string`
  - `+JudgeState() void`
  - `+ExecuteAction() void`

#### `GoapBrain` (class)
- **경로:** `Script/Unit/AI/GoapCore.cs`
- **변수/프로퍼티:**
  - `+string Name`
  - `+GoapState DesiredState`
  - `+string ActionName`
  - `+float Cost`
  - `+GoapState Preconditions`
  - `+GoapState Effects`
  - `-IEnumerable~Unit~ enemies`
  - `-Unit target`
  - `-float d`
  - `-return target`
  - `-Vector2 away`
  - `-float maxComp`
  - `-Vector2 scaled`
  - `-int targetDist`
  - `-Vector2Int retreatPos`
- **함수:**
  - `+GetPriority() float`
  - `+IsValid() bool`
  - `+Execute() void`
  - `#GetClosestEnemy() Unit`
  - `#MoveTowardsPos() void`
  - `#MoveAwayFromTarget() void`
  - `#MoveToEscortSlot() void`
  - `-GoapBrain() public`
  - `+PlanText() string`
  - `+JudgeState() void`
  - `+ExecuteAction() void`

#### `GoapPlanner` (class)
- **경로:** `Script/Unit/AI/GoapPlanner.cs`
- **변수/프로퍼티:**
  - `-int MaxDepth`
  - `+GoapState State`
  - `+List~GoapAction~ Path`
  - `+float Cost`
  - `-var validActions`
  - `-var frontier`
  - `-var bestCost`
  - `-int bestIdx`
  - `-Node node`
  - `-string nodeKey`
  - `-GoapState nextState`
  - `-float nextCost`
  - `-string key`
  - `-var nextPath`
  - `-return null`
- **함수:**
  - `+Plan() List~GoapAction~`
  - `+ApplyAll() GoapState`
  - `-IsSatisfied() bool`
  - `-Satisfies() bool`
  - `-Apply() GoapState`
  - `-Serialize() string`

#### `Node` (class)
- **경로:** `Script/Unit/AI/GoapPlanner.cs`
- **변수/프로퍼티:**
  - `-int MaxDepth`
  - `+GoapState State`
  - `+List~GoapAction~ Path`
  - `+float Cost`
  - `-var validActions`
  - `-var frontier`
  - `-var bestCost`
  - `-int bestIdx`
  - `-Node node`
  - `-string nodeKey`
  - `-GoapState nextState`
  - `-float nextCost`
  - `-string key`
  - `-var nextPath`
  - `-return null`
- **함수:**
  - `+Plan() List~GoapAction~`
  - `+ApplyAll() GoapState`
  - `-IsSatisfied() bool`
  - `-Satisfies() bool`
  - `-Apply() GoapState`
  - `-Serialize() string`

#### `GoapWorldState` (class)
- **경로:** `Script/Unit/AI/GoapWorldState.cs`
- **변수/프로퍼티:**
  - `+int StairArrivalRadius`
  - `-int dx`
  - `-int dy`
  - `-var state`
  - `-bool enemyVisible`
  - `-var trap`
  - `-bool investigateNeeded`
  - `-bool atEscortSlot`
  - `-float backDistance`
  - `-Vector2Int slot`
  - `-bool atStairs`
  - `-bool onTargetFloor`
  - `-int targetFloor`
  - `-return state`
- **함수:**
  - `+DistanceToStairBlock() int`
  - `+Build() GoapState`

