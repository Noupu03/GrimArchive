# GrimArchive Prototype - 역할/패키지 그룹화 및 의존도 반영 단일 UML 클래스 다이어그램

본 문서는 프로젝트의 모든 클래스(총 342개)를 **역할/패키지(`namespace`)별로 그룹화**하고, **의존도가 낮은 패키지(Left) → 의존도가 높은 패키지(Right)** 순서로 배치한 단일 통합 Mermaid UML 다이어그램입니다.

```mermaid
classDiagram
    direction LR

    namespace Camera {
        class CameraController {
            +float panSpeed
            +float zoomSpeed
            +float minZoom
            +float maxZoom
            -AutoAttach() void
            -Update() void
        }
    }

    namespace Haare_Editor {
        class FrameworkMenuItems {
            -var go
            -var slider
            -CreateCustomImage() void
            -CreateCustomText() void
            -CreateCustomButton() void
            -CreateCustomSlider() void
        }
    }

    namespace Haare_Editor_UI {
        class CustomButtonEditor {
            -string_Arr propertiesToExclude
            -SerializedProperty hoverImageProp
            -SerializedProperty hoverColorProp
            -SerializedProperty animationProp
            +OnInspectorGUI() void
        }
        class CustomImageEditor {
            -string_Arr propertiesToExclude
            -SerializedProperty hoverImageProp
            -SerializedProperty hoverColorProp
            -SerializedProperty animationProp
            +OnInspectorGUI() void
        }
    }

    namespace Haare_Scripts_Client_Core_Singleton {
        class Singleton {
            +bool isCreated
            -T instance
            -return instance
            -var i
        }
        class SingletonMonoBehaviour {
            +bool isCreated
            -bool QuittingProgram
            -T instance
            -return null
            +Initialize() UniTask
            -OnDestroy() void
            -OnApplicationQuit() void
        }
    }

    namespace Haare_Scripts_Client_Data_Attribute {
        class DataModelAttribute {
            +Type dataType
            +string AddressableJsonDataPath
            +string JsonDataPath
            -DataModelAttribute() public
        }
    }

    namespace Haare_Scripts_Client_Data_interface {
        class IData {
            <<interface>>
        }
        class IDataInstance {
            <<interface>>
            +int Hash
            -Save() void
        }
        class IDataModel {
            <<interface>>
        }
    }

    namespace Haare_Scripts_Client_Routine_Service_SceneService_interface {
        class ISceneWasLoaded {
            <<interface>>
            -OnSceneWasLoaded() void
        }
    }

    namespace Haare_Scripts_Client_UI_Animator {
        class UIAnimator {
            -Transform targetTransform
            -Tween currentHoverTween
            -Vector3 originalScale
            +RectTransform panelRectTransform
            -UIAnimator() public
            +TriggerHoverEnter() void
            +TriggerHoverExit() void
            +TriggerClickAsync() UniTask
        }
    }

    namespace Haare_Scripts_Client_UI_Panel_Attribute {
        class PanelAttribute {
            +string AddressablePath
            -PanelAttribute() public
        }
    }

    namespace Haare_Scripts_Client_UI_Panel_interface {
        class ICustomPanel {
            <<interface>>
            -Func~UniTask~ setData
            +SceneUIManager uiManager
            +GameObject panel
            +OpenPanel() void
            +ClosePanel() void
            +ReloadPanel() void
            -setData() await
        }
        class IPanelData {
            <<interface>>
        }
    }

    namespace Haare_Scripts_Util_AssetLoader {
        class AddressableLoader {
            +GameObject downMessage
            +Slider downSlider
            +Text sizeInfoText
            +Text downValText
            -Start() void
            -InitAddressable() IEnumerator
            -GetFileSize() string
            +Button_Down() void
        }
        class AssetLoader {
            -ReactiveProperty~float~ _dlProgress
            +ReadOnlyReactiveProperty~float~ DownloadProgress
            +Subject~bool~ AssetDownloadTaskFinished
            -CancellationToken cts
            +SaveJson() UniTask
            +Exists() bool
            +LoadJsonAsync() UniTask~TextAsset~
        }
        class AssetPath {
            +string SCENE_PATH
            +string SCENE_EXT
        }
        class JsonUtil {
            -var method
            -var genericMethod
            +FromJson() object
        }
        class JsonUtilityGeneric {
            -var method
            -var genericMethod
            +FromJson() object
        }
    }

    namespace Haare_Scripts_Util_HashGenerator {
        class HashGenerator {
            -string UniqueId
            -return 0
            +GetUniqueHashCode() int
        }
    }

    namespace Haare_Scripts_Util_LogHelper {
        class LogHelper {
            -string message
            -StringBuilder headerBuilder
            -StringBuilder sb
            +string DEMO
            +Log() void
            +LogTask() void
            +Warning() void
            +Error() void
        }
    }

    namespace Haare_Scripts_Util_Prefab {
        class PrefabPath {
            +string CORE_CANVAS
            -string DEBUG_PANEL
            -string DEMO_TITLE_PANEL
            -string DEMO_LOADING_PANEL
        }
        class PrefabUtil {
            -AsyncOperationHandle~GameObject~ handle
            -GameObject instance
            -return component
            -return null
            -PrefabParam() public
        }
        class PrefabParam {
            -AsyncOperationHandle~GameObject~ handle
            -GameObject instance
            -return component
            -return null
            -PrefabParam() public
        }
    }

    namespace Plugins_Demigiant_DOTween_Modules {
        class DOTweenModuleAudio {
            -return t
            -float currVal
            -return currVal
            +DOComplete() int
            +DOKill() int
            +DOFlip() int
            +DOGoto() int
        }
        class DOTweenModulePhysics {
            -return t
            -float startPosY
            -float offsetY
            -bool offsetYSet
            +DOJump() Sequence
        }
        class DOTweenModulePhysics2D {
            -return t
            -float startPosY
            -float offsetY
            -bool offsetYSet
            +DOJump() Sequence
        }
        class DOTweenModuleSprite {
            -return t
            -Sequence s
            -GradientColorKey_Arr colors
            -int len
            +DOGradientColor() Sequence
            +DOBlendableColor() Tweener
        }
        class DOTweenModuleUI {
            -return t
            -Sequence s
            -GradientColorKey_Arr colors
            -int len
            +DOGradientColor() Sequence
            +DOPunchAnchorPos() Tweener
            +DOShakeAnchorPos() Tweener
            +DOJumpAnchorPos() Sequence
        }
        class Utils {
            -return t
            -Sequence s
            -GradientColorKey_Arr colors
            -int len
            +DOGradientColor() Sequence
            +DOPunchAnchorPos() Tweener
            +DOShakeAnchorPos() Tweener
            +DOJumpAnchorPos() Sequence
        }
        class DOTweenModuleUnityVersion {
            -Sequence s
            -GradientColorKey_Arr colors
            -int len
            -GradientColorKey c
            +DOGradientColor() Sequence
            +WaitForCompletion() CustomYieldInstruction
            +WaitForRewind() CustomYieldInstruction
            +WaitForKill() CustomYieldInstruction
        }
        class DOTweenCYInstruction {
            -Sequence s
            -GradientColorKey_Arr colors
            -int len
            -GradientColorKey c
            +DOGradientColor() Sequence
            +WaitForCompletion() CustomYieldInstruction
            +WaitForRewind() CustomYieldInstruction
            +WaitForKill() CustomYieldInstruction
        }
        class WaitForCompletion {
            -Sequence s
            -GradientColorKey_Arr colors
            -int len
            -GradientColorKey c
            +DOGradientColor() Sequence
            +WaitForCompletion() CustomYieldInstruction
            +WaitForRewind() CustomYieldInstruction
            +WaitForKill() CustomYieldInstruction
        }
        class WaitForRewind {
            -Sequence s
            -GradientColorKey_Arr colors
            -int len
            -GradientColorKey c
            +DOGradientColor() Sequence
            +WaitForCompletion() CustomYieldInstruction
            +WaitForRewind() CustomYieldInstruction
            +WaitForKill() CustomYieldInstruction
        }
        class WaitForKill {
            -Sequence s
            -GradientColorKey_Arr colors
            -int len
            -GradientColorKey c
            +DOGradientColor() Sequence
            +WaitForCompletion() CustomYieldInstruction
            +WaitForRewind() CustomYieldInstruction
            +WaitForKill() CustomYieldInstruction
        }
        class WaitForElapsedLoops {
            -Sequence s
            -GradientColorKey_Arr colors
            -int len
            -GradientColorKey c
            +DOGradientColor() Sequence
            +WaitForCompletion() CustomYieldInstruction
            +WaitForRewind() CustomYieldInstruction
            +WaitForKill() CustomYieldInstruction
        }
        class WaitForPosition {
            -Sequence s
            -GradientColorKey_Arr colors
            -int len
            -GradientColorKey c
            +DOGradientColor() Sequence
            +WaitForCompletion() CustomYieldInstruction
            +WaitForRewind() CustomYieldInstruction
            +WaitForKill() CustomYieldInstruction
        }
        class WaitForStart {
            -Sequence s
            -GradientColorKey_Arr colors
            -int len
            -GradientColorKey c
            +DOGradientColor() Sequence
            +WaitForCompletion() CustomYieldInstruction
            +WaitForRewind() CustomYieldInstruction
            +WaitForKill() CustomYieldInstruction
        }
        class DOTweenModuleUtils {
            -bool _initialized
            -Assembly_Arr loadedAssemblies
            -MethodInfo mi
            -return false
            +Init() void
            -Preserver() void
            +SetOrientationOnPath() void
            +HasRigidbody2D() bool
        }
        class Physics {
            -bool _initialized
            -Assembly_Arr loadedAssemblies
            -MethodInfo mi
            -return false
            +Init() void
            -Preserver() void
            +SetOrientationOnPath() void
            +HasRigidbody2D() bool
        }
    }

    namespace Unit_Data {
        class UnitStatsData {
            +string skillName
            +float baseDelayMs
            +float baseCooldown
            +int cooldownSlot
        }
        class SkillData {
            +string skillName
            +float baseDelayMs
            +float baseCooldown
            +int cooldownSlot
        }
    }

    namespace Encyclopedia {
        class EncyclopediaEntryData {
            +string Id
            +string Category
            +string Name
            +string Description
        }
        class EncyclopediaManager {
            -HashSet~string~ _unlockedIds
            -return true
            -return false
            -return entry
            -Awake() void
            -InitializeDatabase() void
            +UnlockEntry() bool
            +GetEntry() IEncyclopediaEntry
        }
        class EncyclopediaTester {
            +string_Arr entryIdsToUnlock
            -bool success
            -Update() void
        }
        class IEncyclopediaEntry {
            <<interface>>
            -string Id
            -string Category
            -string Name
            -string Description
        }
        class IEncyclopediaSystem {
            <<interface>>
            -UnlockEntry() bool
            -GetEntry() IEncyclopediaEntry
            -GetAllEntries() List~IEncyclopediaEntry~
            -GetUnlockedEntries() List~IEncyclopediaEntry~
        }
    }

    namespace Unit_Vision {
        class IVisionContext {
            <<interface>>
            -GameSession Session
            -ResolveReachedTarget() PerceptionOutcome
            -HasReachedPerceptionThisPass() bool
            -HasVisionOnlyNonEmptyTile() bool
            -AddVisionOnlyNonEmptyTile() void
        }
        class IVisionTileHandler {
            <<interface>>
            -Handle() void
        }
        class ObjectPerceptionHandler {
            -float objVisibility
            -PerceptionOutcome outcome
            -bool isBossRoom
            +Handle() void
        }
        class PerceptionMath {
            +float AlertDetectionBonus
            +float MentalTierBoundary1
            +float MentalTierBoundary2
            +float MentalTierBoundary3
            +GetMentalTier() MentalTier
            +MentalCorrectionForHuman() float
            +RollOutcome() PerceptionOutcome
        }
        class PerceptionTargetKind {
            <<enum>>
            +float AlertDetectionBonus
            +float MentalTierBoundary1
            +float MentalTierBoundary2
            +float MentalTierBoundary3
            +GetMentalTier() MentalTier
            +MentalCorrectionForHuman() float
            +RollOutcome() PerceptionOutcome
        }
        class PerceptionRecord {
            +PerceptionOutcome Outcome
            +bool WasInRange
            +bool PendingSuspiciousInvestigation
            +Vector3Int LastKnownTile
        }
        class TerrainRevealHandler {
            -bool tileIsWall
            -bool isFirstReveal
            -bool isBossRoom
            -int totalFloorTiles
            +Handle() void
        }
        class UnitPerceptionHandler {
            -bool isEnemy
            -PerceptionOutcome outcome
            -float danger
            -float interest
            +Handle() void
        }
        class PerceptionOutcome {
            <<enum>>
        }
        class MentalTier {
            <<enum>>
        }
        class PerceptionReactionCandidate {
            <<enum>>
        }
        class VisionDirectionReason {
            <<enum>>
        }
        class VisionMath {
            +float DetectionStatMax
            +float BaseViewAngleDeg
            +int BaseViewDistanceTiles
            +int ViewDistancePerSpottingStep
            +ViewDistance() int
            +AwarenessDistance() int
            +AwarenessAngle() float
            +ResolveBaseVisibility() float
        }
        class VisionDirectionCandidate {
            <<struct>>
            +float DetectionStatMax
            +float BaseViewAngleDeg
            +int BaseViewDistanceTiles
            +int ViewDistancePerSpottingStep
            +ViewDistance() int
            +AwarenessDistance() int
            +AwarenessAngle() float
            +ResolveBaseVisibility() float
        }
    }

    namespace Haare_Scripts_Client_Core {
        class HaareClient {
            -var eventSystemObj
            -var audioObj
            -Main() void
            -InitializePlugin() UniTask
            -RegisterProcesses() UniTask
        }
        class Processor {
            +ReadOnlyReactiveProperty~bool~ PROCESSING
            -ReactiveProperty~bool~ processing
            -List~IRoutine~ Routines
            -List~IRoutine~ deleteRoutines
            -OnValidate() void
            +Constructor() UniTask
            -initializePlugin() await
            -RegisterEvents() await
        }
    }

    namespace Haare_Scripts_Client_Routine_interface {
        class INativeRoutine {
            <<interface>>
            -UpdateProcess() void
            -OnApplicationQuit() void
            -OnApplicationPause() void
        }
        class IRoutine {
            <<interface>>
            -CancellationTokenSource _cts
            -bool isRegistered
            -bool isInSceneOnly
            -bool isInitialized
            -Initialize() UniTask
            -Finalize() UniTask
        }
    }

    namespace Unit_Weight {
        class HumanKnowledgeBase {
            -List~IncidentEntry~ _pendingIncidents
            -return s
            -string targetId
            -bool isIndividualTarget
            -GetOrCreateSpecies() SpeciesWeightState
            -GetOrCreateIndividual() IndividualWeightState
            +RecordEvent() void
            -RecordEventForWeight() void
        }
        class WipeoutTraceRecord {
            -List~IncidentEntry~ _pendingIncidents
            -return s
            -string targetId
            -bool isIndividualTarget
            -GetOrCreateSpecies() SpeciesWeightState
            -GetOrCreateIndividual() IndividualWeightState
            +RecordEvent() void
            -RecordEventForWeight() void
        }
        class IncidentEntry {
            +string IncidentId
            +EventId EventId
            +string TargetId
            +WeightType WeightType
            -IncidentEntry() public
        }
        class PersonalMapKnowledge {
            +IEnumerable~Vector3Int~ KnownInterestTiles
            -string objId
            -bool newInterestPresent
            -float elapsed
            +TickTileInterestConfirm() void
            +GetTileDanger() float
            +SetTileDangerFromUnit() void
            +TickTileSafety() void
        }
        class MonsterSighting {
            +IEnumerable~Vector3Int~ KnownInterestTiles
            -string objId
            -bool newInterestPresent
            -float elapsed
            +TickTileInterestConfirm() void
            +GetTileDanger() float
            +SetTileDangerFromUnit() void
            +TickTileSafety() void
        }
        class RoomExploreState {
            <<enum>>
            +IEnumerable~Vector3Int~ KnownInterestTiles
            -string objId
            -bool newInterestPresent
            -float elapsed
            +TickTileInterestConfirm() void
            +GetTileDanger() float
            +SetTileDangerFromUnit() void
            +TickTileSafety() void
        }
        class RoomKnowledge {
            +IEnumerable~Vector3Int~ KnownInterestTiles
            -string objId
            -bool newInterestPresent
            -float elapsed
            +TickTileInterestConfirm() void
            +GetTileDanger() float
            +SetTileDangerFromUnit() void
            +TickTileSafety() void
        }
        class PersonalWeightRecord {
            +string TargetId
            +WeightType Type
            +float StoredValue
            +int AppliedValue
            -PersonalWeightRecord() public
        }
        class SpeciesWeightState {
            +string SpeciesKey
            +float UnderstandingStored
            +int LastUnderstandingUpdateWave
            +float DangerAccumulatedStored
            -SpeciesWeightState() public
            -IndividualWeightState() public
        }
        class IndividualWeightState {
            +string SpeciesKey
            +float UnderstandingStored
            +int LastUnderstandingUpdateWave
            +float DangerAccumulatedStored
            -SpeciesWeightState() public
            -IndividualWeightState() public
        }
        class WeightType {
            <<enum>>
        }
        class InfoType {
            <<enum>>
        }
        class MentalErrorState {
            <<enum>>
        }
        class DangerStage {
            <<enum>>
        }
        class InterestStage {
            <<enum>>
        }
        class EventId {
            <<enum>>
        }
        class WeightEventTable {
            -string JsonPath
            +float Understanding
            +float Danger
            +string id
            -Delta() public
            -Load() void
            +Get() Delta
        }
        class Delta {
            <<struct>>
            -string JsonPath
            +float Understanding
            +float Danger
            +string id
            -Delta() public
            -Load() void
            +Get() Delta
        }
        class JsonEvent {
            -string JsonPath
            +float Understanding
            +float Danger
            +string id
            -Delta() public
            -Load() void
            +Get() Delta
        }
        class JsonEventDatabase {
            -string JsonPath
            +float Understanding
            +float Danger
            +string id
            -Delta() public
            -Load() void
            +Get() Delta
        }
        class WeightMath {
            +float UnderstandingMin
            +float DangerMin
            +float InterestMin
            -var distinct
            +Clamp() float
            +DedupExact() List~IncidentEntry~
            -ResolvePanicBlend() List~float~
            -RepresentativeChange() float
        }
        class PoolOverflowResult {
            <<struct>>
            +float UnderstandingMin
            +float DangerMin
            +float InterestMin
            -var distinct
            +Clamp() float
            +DedupExact() List~IncidentEntry~
            -ResolvePanicBlend() List~float~
            -RepresentativeChange() float
        }
    }

    namespace Unit_Exploration {
        class AlertSearchState {
            +Vector2Int_Opt TargetPosition
            +bool IsPostCombatSweep
            +float ElapsedSeconds
        }
        class ExplorationMath {
            +float AlertMoveSpeedRatio
            +float AlertReactionSpeedRatio
            +float PostCombatAlertSeconds
            +float UnidentifiedAttackSearchSeconds
            +TrapDisarmSuccessRate() float
        }
        class FormationState {
            +Human EscortTarget
        }
        class InvestigationState {
            +string TargetObjectId
            +Vector3Int TargetPosition
            +float Progress01
            +bool PenaltyActive
        }
        class TrapPhase {
            <<enum>>
            +TrapPhase Phase
            +string TrapObjectId
            +Vector3Int TrapPosition
            +bool JoinWaitElapsed
        }
        class TrapInteractionState {
            +TrapPhase Phase
            +string TrapObjectId
            +Vector3Int TrapPosition
            +bool JoinWaitElapsed
        }
        class WaitReason {
            <<enum>>
            +WaitReason Reason
            +Vector2Int_Opt WaitPosition
        }
        class WaitState {
            +WaitReason Reason
            +Vector2Int_Opt WaitPosition
        }
    }

    namespace Encyclopedia_UI {
        class UI_Encyclopedia {
            -IEncyclopediaSystem _encyclopediaSystem
            -List~UI_EncyclopediaSlot~ _spawnedSlots
            -string keyPath
            -List~IEncyclopediaEntry~ entriesToShow
            #Constructor() void
            -TogglePanel() void
            -OpenPanel() else
            -OnEnable() void
        }
        class UI_EncyclopediaDetail {
            +ShowDetails() void
            +Clear() void
        }
        class UI_EncyclopediaSlot {
            -IEncyclopediaEntry _currentEntry
            -UI_Encyclopedia _parentUI
            -Awake() void
            +SetData() void
            -OnClick() void
        }
    }

    namespace Haare_Scripts_Client_Routine {
        class MonoRoutine {
            +CompositeDisposable disposables
            -bool _isFinalized
            +CancellationTokenSource _cts
            +bool isInSceneOnly
            -Awake() void
            #InitializeAsync() UniTask
            #Constructor() void
            +Initialize() UniTask
        }
        class NativeRoutine {
            +CancellationTokenSource _cts
            +bool isInSceneOnly
            +bool isInitialized
            +Func~UniTask~ Oninitialize
            -NativeRoutine() protected
            -Constructor() UniTask
            +Initialize() UniTask
            -Oninitialize() await
        }
    }

    namespace Production {
        class ResourceCost {
            +ResourceType resourceType
            +int amount
            +string ruleId
            +string displayName
        }
        class ProductionRule {
            +ResourceType resourceType
            +int amount
            +string ruleId
            +string displayName
        }
        class ResourceType {
            <<enum>>
            +int MonsterPlaceWoodCost
            +int TrapPlaceStoneCost
            -return true
            -return false
            +Initialize() UniTask
            +Finalize() UniTask
            +AddResource() void
            +TryConsumeResource() bool
        }
        class ResourceManager {
            +int MonsterPlaceWoodCost
            +int TrapPlaceStoneCost
            -return true
            -return false
            +Initialize() UniTask
            +Finalize() UniTask
            +AddResource() void
            +TryConsumeResource() bool
        }
    }

    namespace UI_Title {
        class GameTitlePanel {
            +SceneUIManager uiManager
            +GameObject panel
            +OpenPanel() void
            +ClosePanel() void
            +BindEvent() void
        }
        class TitlePresenter {
            -string TitleSceneName
            -string GameSceneName
            +CompositeDisposable disposables
            +Dispose() void
            +PostInitialize() void
            -BindPanel() void
            -StartGame() void
        }
        class TitleScope {
            #Configure() void
        }
        class TitleUIManager {
            -int titlePanelID
            -var panel
            +Initialize() UniTask
        }
    }

    namespace VFX_Shader_Editor {
        class FXFlatLabGUI {
            -bool showRender
            -bool showMain
            -bool showMotion
            -bool showNoise
            +OnGUI() void
            -P() MaterialProperty
            -FindProperty() return
            -DrawProp() void
        }
        class FXMaterialDesignerWindow {
            -TargetType targetType
            -StylePreset stylePreset
            -string materialName
            -Color tintColor
            +Open() void
            -OnGUI() void
            -ApplyPreset() void
            -CreateMaterial() void
        }
        class TargetType {
            <<enum>>
            -TargetType targetType
            -StylePreset stylePreset
            -string materialName
            -Color tintColor
            +Open() void
            -OnGUI() void
            -ApplyPreset() void
            -CreateMaterial() void
        }
        class StylePreset {
            <<enum>>
            -TargetType targetType
            -StylePreset stylePreset
            -string materialName
            -Color tintColor
            +Open() void
            -OnGUI() void
            -ApplyPreset() void
            -CreateMaterial() void
        }
    }

    namespace Haare_Scripts_Client_UI_UiManager {
        class CoreUIManager {
            +Initialize() UniTask
        }
        class SceneUIManager {
            -Stack~PanelType~ TypePanelStack
            +bool ILoadedScene
            -Rect safeArea
            -Vector2 anchorMin
            +Initialize() UniTask
            -OnValidate() void
            -ApplySafeArea() void
            +PeekPanel() ICustomPanel
        }
        class PanelType {
            -Stack~PanelType~ TypePanelStack
            +bool ILoadedScene
            -Rect safeArea
            -Vector2 anchorMin
            +Initialize() UniTask
            -OnValidate() void
            -ApplySafeArea() void
            +PeekPanel() ICustomPanel
        }
    }

    namespace Haare_Demo_Script_UI {
        class DebugPanel {
            -ICustomPanel _customPanelImplementation
            +SceneUIManager uiManager
            +GameObject panel
            +BindEvent() void
            +SetData() void
            +OpenPanel() void
            +ClosePanel() void
        }
        class LoadingFadePanel {
            -ICustomPanel _customPanelImplementation
            +SceneUIManager uiManager
            +GameObject panel
            +Initialize() UniTask
            +FadeIn() UniTask
            +FadeOut() UniTask
            +BindEvent() void
        }
        class LoadingPanel {
            +SceneUIManager uiManager
            +GameObject panel
            +BindEvent() void
            +SetData() void
            +OpenPanel() void
            +ClosePanel() void
        }
        class TitlePanel {
            -ICustomPanel _customPanelImplementation
            +SceneUIManager uiManager
            +GameObject panel
            +BindEvent() void
            +SetData() void
            +OpenPanel() void
            +ClosePanel() void
        }
    }

    namespace Haare_Scripts_Client_UI_Button {
        class CustomButton {
            +bool INTERACTIABLE
            +bool OPTION_HOVERIMAGE
            +bool OPTION_HOVERALPHA
            +bool OPTION_ANIMATION
            +Initialize() UniTask
            +SetInteractable() void
            +Finalize() UniTask
            +OnPointerClick() void
        }
    }

    namespace Haare_Scripts_Client_UI_Image {
        class CustomImage {
            +Image _image
            -Sprite CommonSprite
            -Sprite HoveredSprite
            -Sprite ClickedSprite
            #Constructor() void
            +Initialize() UniTask
            +ClearSlidePosition() void
            +SlideOpenPanel() void
        }
    }

    namespace Haare_Scripts_Client_UI_Slider {
        class CustomSlider {
            -Slider _slider
            +float Value
            #Constructor() void
            +Initialize() UniTask
            +Setup() void
            +SetValue() void
        }
    }

    namespace Haare_Scripts_Client_UI_Text {
        class CustomText {
            -TMP_Text _text
            +TMP_Text Text
            #Constructor() void
            +SetupText() void
            +SetupTextColor() void
        }
    }

    namespace Unit_Movement {
        class AStarMovement {
            +Vector2Int Pos
            +AStarNode Parent
            +int GCost
            +int HCost
            +TryGetNextStep() bool
            -TryFallbackMove() return
            #IsTileWalkable() bool
            -IsCoordBlocked() bool
        }
        class AStarNode {
            +Vector2Int Pos
            +AStarNode Parent
            +int GCost
            +int HCost
            +TryGetNextStep() bool
            -TryFallbackMove() return
            #IsTileWalkable() bool
            -IsCoordBlocked() bool
        }
        class IMovementAlgorithm {
            <<interface>>
            -TryGetNextStep() bool
        }
        class RoomConfinedMovement {
            -Room _cachedRoom
            -int _lastCheckFloor
            -bool baseWalkable
            -return false
            #IsTileWalkable() bool
        }
    }

    namespace Haare_Scripts_Client_DI_Presenter {
        class GamePresenter {
            -SceneService _sceneService
            +Dispose() void
            +PostInitialize() void
        }
        class IPresenter {
            <<interface>>
            +CompositeDisposable disposables
        }
        class UIPresenter {
            -var fadepanel
            +CompositeDisposable disposables
            +bool isInitialized
            +Dispose() void
            +PostInitialize() void
            #PostInitializeAsync() UniTask
            #BindPanelEvents() void
        }
    }

    namespace Haare_Demo_Script {
        class DemoNative {
            +Initialize() UniTask
            +UpdateProcess() void
            +Finalize() UniTask
        }
    }

    namespace Haare_Demo_Script_LoadScene {
        class DemoLoadMono {
            +Initialize() UniTask
        }
        class DemoLoadScope {
            #Configure() void
        }
        class DemoLoadUIManager {
            -int loadingPanelID
            +Initialize() UniTask
        }
        class DemoLoadUIPresenter {
            -CoreUIManager _coreUIManager
            -SceneUIManager _sceneUiManager
            -IObjectResolver _resolver
            +SceneService sceneService
            +Dispose() void
            +PostInitialize() void
            -BindIPanel() void
            -LoadStartSequence() UniTask
        }
    }

    namespace Haare_Demo_Script_LobbyScene {
        class DemoCubeRotator {
            +float rotationSpeed
            +Initialize() UniTask
            #UpdateProcess() void
        }
        class DemoLobbyScope {
            #Configure() void
        }
        class DemoLobbyUIManager {
            +Initialize() UniTask
            -BindIPanel() void
        }
        class DemoLobbyUIPresenter {
            -CoreUIManager _coreUIManager
            -SceneUIManager _sceneUiManager
            -IObjectResolver _resolver
            +SceneService sceneService
            +Dispose() void
            +PostInitialize() void
            -BindIPanel() void
            -StartSequence() UniTask
        }
    }

    namespace Haare_Demo_Script_TitleScene {
        class DemoTitleMono {
            +Initialize() UniTask
        }
        class DemoTitleScope {
            #Configure() void
        }
        class DemoTitleUIManager {
            -int debugPanelID
            -int titlePanelID
            -var _titlepanel
            +Initialize() UniTask
            -Reset() void
            -BindIPanel() void
        }
        class DemoTitleUIPresenter {
            -CoreUIManager _coreUIManager
            -SceneUIManager _sceneUiManager
            -IObjectResolver _resolver
            +SceneService sceneService
            +Dispose() void
            +PostInitialize() void
            -StartGameSequence() UniTask
            -OnFinishedFadePanel() void
        }
    }

    namespace Haare_Scripts_Client_Data {
        class DataManager {
            -var modelType
            -var sourceAttribute
            -return default
            -Type targetDataType
        }
    }

    namespace Haare_Scripts_Util_FPSLogger {
        class FPSLogger {
            +CustomText fpsText
            -float deltaTime
            -float fps
            +Initialize() UniTask
            #UpdateProcess() void
        }
    }

    namespace Haare_Scripts_Client_Routine_Service_SceneService {
        class SceneLoadRequest {
            +SceneName Scene
            +LoadSceneMode Mode
            +object Argument
            -SceneLoadRequest() public
        }
        class SceneName {
            <<enum>>
            -CoreUIManager _coreUIManager
            -SceneName sceneToUnload
            -ReactiveProperty~SceneLoadPhase~ currentPhaseReactive
            +ReactiveProperty~SceneLoadPhase~ CurrentPhase
            +Initialize() UniTask
            +LoadSceneWithLoad() UniTask
            -LoadSceneInternal() await
            +LoadScene() UniTask
        }
        class SceneLoadPhase {
            <<enum>>
            -CoreUIManager _coreUIManager
            -SceneName sceneToUnload
            -ReactiveProperty~SceneLoadPhase~ currentPhaseReactive
            +ReactiveProperty~SceneLoadPhase~ CurrentPhase
            +Initialize() UniTask
            +LoadSceneWithLoad() UniTask
            -LoadSceneInternal() await
            +LoadScene() UniTask
        }
        class SceneService {
            -CoreUIManager _coreUIManager
            -SceneName sceneToUnload
            -ReactiveProperty~SceneLoadPhase~ currentPhaseReactive
            +ReactiveProperty~SceneLoadPhase~ CurrentPhase
            +Initialize() UniTask
            +LoadSceneWithLoad() UniTask
            -LoadSceneInternal() await
            +LoadScene() UniTask
        }
    }

    namespace Haare_Scripts_Client_DI_Container {
        class CoreLifetimeScope {
            #CoreUIManager _coreUIManagerPrefab
            -bool isLocalMode
            #Awake() void
            #Configure() void
        }
    }

    namespace Editor {
        class EncyclopediaSetup {
            -string prefabPath
            -string addressableKey
            -GameObject prefab
            -AddressableAssetSettings settings
            +FixAddressables() void
            +RemoveAddressables() void
        }
        class HaareDemoSetup {
            -string PrefabFolder
            -string SceneFolder
            -string_Arr Scenes
            -var settings
            +SetupHaareDemoAddressables() void
            +RemoveHaareDemoAddressables() void
            -RegisterEntry() void
        }
        class HaareUIAddressableSetupWindow {
            -string _prefabName
            -var window
            -string prefabPath
            -string addressableKey
            +ShowWindow() void
            -OnGUI() void
            -RegisterAddressable() void
            -RemoveAddressable() void
        }
        class HaareUISetup {
            -string OutputFolder
            -string CoreCanvasAddress
            -string DebugPanelAddress
            -string LoadingFadePanelAddress
            +SetupHaareUI() void
            -CreateCoreCanvasPrefab() GameObject
            -CreateDebugInfoPanelPrefab() GameObject
            -CreateLoadingFadePanelPrefab() GameObject
        }
        class JsonToUnitPrefabConverter {
            -string UnitsJsonPath
            -string SkillsJsonPath
            -string OutputFolder
            +bool isSpecialUnit
            +ConvertJsonToPrefabs() void
            -CreateFolderRecursive() void
            -SanitizeFileName() string
        }
        class JsonWeightData {
            -string UnitsJsonPath
            -string SkillsJsonPath
            -string OutputFolder
            +bool isSpecialUnit
            +ConvertJsonToPrefabs() void
            -CreateFolderRecursive() void
            -SanitizeFileName() string
        }
        class JsonWeaponData {
            -string UnitsJsonPath
            -string SkillsJsonPath
            -string OutputFolder
            +bool isSpecialUnit
            +ConvertJsonToPrefabs() void
            -CreateFolderRecursive() void
            -SanitizeFileName() string
        }
        class JsonEffectsData {
            -string UnitsJsonPath
            -string SkillsJsonPath
            -string OutputFolder
            +bool isSpecialUnit
            +ConvertJsonToPrefabs() void
            -CreateFolderRecursive() void
            -SanitizeFileName() string
        }
        class JsonVisualData {
            -string UnitsJsonPath
            -string SkillsJsonPath
            -string OutputFolder
            +bool isSpecialUnit
            +ConvertJsonToPrefabs() void
            -CreateFolderRecursive() void
            -SanitizeFileName() string
        }
        class JsonUnitData {
            -string UnitsJsonPath
            -string SkillsJsonPath
            -string OutputFolder
            +bool isSpecialUnit
            +ConvertJsonToPrefabs() void
            -CreateFolderRecursive() void
            -SanitizeFileName() string
        }
        class JsonUnitDatabase {
            -string UnitsJsonPath
            -string SkillsJsonPath
            -string OutputFolder
            +bool isSpecialUnit
            +ConvertJsonToPrefabs() void
            -CreateFolderRecursive() void
            -SanitizeFileName() string
        }
        class JsonSkillData {
            -string UnitsJsonPath
            -string SkillsJsonPath
            -string OutputFolder
            +bool isSpecialUnit
            +ConvertJsonToPrefabs() void
            -CreateFolderRecursive() void
            -SanitizeFileName() string
        }
        class JsonSkillDatabase {
            -string UnitsJsonPath
            -string SkillsJsonPath
            -string OutputFolder
            +bool isSpecialUnit
            +ConvertJsonToPrefabs() void
            -CreateFolderRecursive() void
            -SanitizeFileName() string
        }
        class MapGeneratorTool {
            -CreateMap cm
            -bool showGizmoSettings
            -GUIStyle sectionHeaderStyle
            -GUIStyle boxStyle
            +ShowWindow() void
            -OnEnable() void
            -OnGUI() void
            -InitStyles() void
        }
        class MapViewTool {
            -CreateMap cm
            -int selectedFloor
            -int selectedChunkX
            -int selectedChunkY
            +ShowWindow() void
            -OnEnable() void
            -OnDisable() void
            -OnPlayModeStateChanged() void
        }
        class ProjectileSetupTool {
            -GameObject selectedPrefab
            -string assetPath
            -GameObject instance
            -bool modified
            +ShowWindow() void
            -OnGUI() void
            -SetupProjectilePrefab() void
        }
        class TestMapGen {
            -CreateMap cm
            +Run() void
        }
        class TitleSceneSetup {
            -string PrefabFolder
            -string TitlePanelPrefabPath
            -string TitlePanelAddress
            -string TitleScenePath
            +SetupTitleScene() void
            -CreateTitlePanelPrefab() GameObject
            -CreateTitleButton() GameObject
            -BuildAndSaveTitleScene() void
        }
        class UnitVisualEditor {
            -var visual
            -var tex
            -Rect rect
            +OnInspectorGUI() void
        }
        class WeaponSocketSetup {
            -GameObject unitPrefab
            -Sprite weaponSprite
            -string socketName
            -string path
            +ShowWindow() void
            -OnGUI() void
            -AddWeaponSocket() void
        }
        class AddressablesPlayModeSetup {
            -var settings
            -int targetIndex
            -AddressablesPlayModeSetup() static
            +SetPlayModeToAssetDatabase() void
        }
        class OffenseDebugWindow {
            -GameSession _gameSession
            -var root
            -return _gameSession
            -float _customWaveCooldown
            +ShowWindow() void
            -OnGUI() void
            -GetRandomRealRoom() Room
        }
        class SetupStatusInfoPanel {
            -string folderPath
            -string prefabPath
            -GameObject go
            -GameObject prefab
            +Setup() void
        }
    }

    namespace Unit_Core {
        class AIStateComponent {
            -Unit _owner
            +UnitAIWeightState AIWeightState
            +HashSet~Unit~ reactedAttackers
            +float currentReactionWindow
            -AIStateComponent() public
            +OnUpdate() void
            +OnDespawn() void
        }
        class BaseStatComponent {
            -Unit _owner
            +float agility
            +float sense
            +float sterngth
            -BaseStatComponent() public
            +OnUpdate() void
            +OnDespawn() void
        }
        class CombatStatComponent {
            -Unit _owner
            +float physicalAttack
            +float magicalAttack
            +float physicalDefense
            -CombatStatComponent() public
            +OnUpdate() void
            +OnDespawn() void
        }
        class CombatStateComponent {
            -Unit _owner
            +UnitCombatState State
            -CombatStateComponent() public
            +OnUpdate() void
            +OnDespawn() void
        }
        class FactionData {
            +int_Arr discoveredMap
            +List~Unit~ spottedEnemyUnits
            -int floorCount
            -int w
            -FactionData() public
            +InitMap() void
        }
        class FactionType {
            <<enum>>
        }
        class HealthComponent {
            -Unit _owner
            +float maxHp
            +float hp
            +float maxMp
            -HealthComponent() public
            +OnUpdate() void
            +OnDespawn() void
        }
        class HumanFactionBehavior {
            +IsEnemy() bool
            +OnUpdate() void
            +OnDeath() void
            +OnEnterRoom() void
        }
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
            -Vector2Int PerceptiblePosition
            -int PerceptibleFloor
            -float PerceptibleStealth
            -float PerceptibleSpotting
        }
        class ITargetable {
            <<interface>>
            -Vector2Int TargetPosition
            -int TargetFloor
            -float TargetHp
            -FactionData TargetFactionData
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
        class OffenseProcessor {
            -IMapColorizer _colorizer
            -bool hasPlayer
            -bool hasWild
            -var roomUnits
            -OffenseProcessor() public
            +StartOffense() void
            +UpdateProcess() void
            -FailOffense() void
        }
        class PartyComponent {
            -Unit _owner
            +Party party
            -PartyComponent() public
            +OnUpdate() void
            +OnDespawn() void
        }
        class PerceptionComponent {
            -Unit _owner
            +UnitPerceptionState State
            +bool IsAlert
            +int AlertRecordCount
            -PerceptionComponent() public
            +NotifyPerceptionSuspiciousChanged() void
            +RemovePerceptionRecord() void
            +OnUpdate() void
        }
        class PlayerMonsterBehavior {
            +IsEnemy() bool
            +OnUpdate() void
            +OnDeath() void
            +OnEnterRoom() void
        }
        class ResourceAccumulator {
            -ResourceAccumulator _instance
            -return _instance
            -int _accumulatedResourceB
            +int AccumulatedResourceB
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
            +GetRandomPosInRoom() Vector2Int
            +AddUnit() void
            +RemoveUnit() void
        }
        class Room {
            -int rx
            -int ry
            -List~Unit~ _containedUnits
            +IReadOnlyList~Unit~ ContainedUnits
            +GetRandomPosInRoom() Vector2Int
            +AddUnit() void
            +RemoveUnit() void
        }
        class StatusEffectsComponent {
            -Unit _owner
            +UnitStatusEffects State
            -StatusEffectsComponent() public
            +OnUpdate() void
            +OnDespawn() void
        }
        class Unit {
            +List~IUnitComponent~ Components
            -return null
            -HealthComponent _healthComp
            -CombatStateComponent _combatStateComp
            -OnEnable() void
            +HasPerceivedThreatCollider() bool
            -Normalize() float
            +CalculateDerivedStats() void
        }
        class Human {
            +List~IUnitComponent~ Components
            -return null
            -HealthComponent _healthComp
            -CombatStateComponent _combatStateComp
            -OnEnable() void
            +HasPerceivedThreatCollider() bool
            -Normalize() float
            +CalculateDerivedStats() void
        }
        class Monster {
            +List~IUnitComponent~ Components
            -return null
            -HealthComponent _healthComp
            -CombatStateComponent _combatStateComp
            -OnEnable() void
            +HasPerceivedThreatCollider() bool
            -Normalize() float
            +CalculateDerivedStats() void
        }
        class UnitStatusEffects {
            <<struct>>
            +float stunDuration
            +float slowDuration
            +float poisonDuration
            +float burnDuration
        }
        class UnitCombatState {
            <<struct>>
            +float stunDuration
            +float slowDuration
            +float poisonDuration
            +float burnDuration
        }
        class UnitPerceptionState {
            <<struct>>
            +float stunDuration
            +float slowDuration
            +float poisonDuration
            +float burnDuration
        }
        class UnitAIWeightState {
            <<struct>>
            +float stunDuration
            +float slowDuration
            +float poisonDuration
            +float burnDuration
        }
        class UnitFunction {
            -float prevHp
            -float damage
            -bool defenderIsHuman
            -bool attackerIsHuman
            +TakeDamage() void
            +TakePhysicalDamage() void
            +TakeMagicalDamage() void
            +RecordHitWeightEvent() void
        }
        class Dir {
            <<enum>>
            +string typeName
            +Vector2 footprint
            +float BASE_REACTION_TIME_MS
            +float MIN_REACTION_TIME_MS
            -Knight() public
            -HumanBaseType() public
            -MeleeTank() public
            -WildBaseType() public
        }
        class UnitType {
            +string typeName
            +Vector2 footprint
            +float BASE_REACTION_TIME_MS
            +float MIN_REACTION_TIME_MS
            -Knight() public
            -HumanBaseType() public
            -MeleeTank() public
            -WildBaseType() public
        }
        class Knight {
            +string typeName
            +Vector2 footprint
            +float BASE_REACTION_TIME_MS
            +float MIN_REACTION_TIME_MS
            -Knight() public
            -HumanBaseType() public
            -MeleeTank() public
            -WildBaseType() public
        }
        class HumanBaseType {
            +string typeName
            +Vector2 footprint
            +float BASE_REACTION_TIME_MS
            +float MIN_REACTION_TIME_MS
            -Knight() public
            -HumanBaseType() public
            -MeleeTank() public
            -WildBaseType() public
        }
        class MeleeTank {
            +string typeName
            +Vector2 footprint
            +float BASE_REACTION_TIME_MS
            +float MIN_REACTION_TIME_MS
            -Knight() public
            -HumanBaseType() public
            -MeleeTank() public
            -WildBaseType() public
        }
        class WildBaseType {
            +string typeName
            +Vector2 footprint
            +float BASE_REACTION_TIME_MS
            +float MIN_REACTION_TIME_MS
            -Knight() public
            -HumanBaseType() public
            -MeleeTank() public
            -WildBaseType() public
        }
        class Archer {
            +string typeName
            +Vector2 footprint
            +float BASE_REACTION_TIME_MS
            +float MIN_REACTION_TIME_MS
            -Knight() public
            -HumanBaseType() public
            -MeleeTank() public
            -WildBaseType() public
        }
        class CombatConstants {
            +string typeName
            +Vector2 footprint
            +float BASE_REACTION_TIME_MS
            +float MIN_REACTION_TIME_MS
            -Knight() public
            -HumanBaseType() public
            -MeleeTank() public
            -WildBaseType() public
        }
        class VisionStatComponent {
            -Unit _owner
            +float stealth
            +float spotting
            +float baseVisibility
            -VisionStatComponent() public
            +OnUpdate() void
            +OnDespawn() void
        }
        class WildBaseSpawnerComponent {
            +GameObject debugVisual
            -Unit _owner
            -Room _targetRoom
            -float _spawnInterval
            -WildBaseSpawnerComponent() public
            -SpawnLoop() UniTaskVoid
            +OnUpdate() void
            -SpawnMonster() void
        }
        class WildMonsterBehavior {
            -int rewardAmount
            +IsEnemy() bool
            +OnUpdate() void
            +OnDeath() void
            +OnEnterRoom() void
        }
    }

    namespace Unit_Combat {
        class DefenseType {
            <<enum>>
            +DefenseType type
            +float score
            +float weight
            -List~DefenseCandidate~ candidates
            -DefenseCandidate() public
            +EvaluateEarlyReaction() void
            +EvaluateImpactDefense() float
            -ExecuteImpactDefense() return
        }
        class DefenseCandidate {
            +DefenseType type
            +float score
            +float weight
            -List~DefenseCandidate~ candidates
            -DefenseCandidate() public
            +EvaluateEarlyReaction() void
            +EvaluateImpactDefense() float
            -ExecuteImpactDefense() return
        }
        class DefenseSystem {
            +DefenseType type
            +float score
            +float weight
            -List~DefenseCandidate~ candidates
            -DefenseCandidate() public
            +EvaluateEarlyReaction() void
            +EvaluateImpactDefense() float
            -ExecuteImpactDefense() return
        }
        class Hitbox {
            <<struct>>
            +Vector2 center
            +Vector2 size
            +float rotation
            -Vector2_Arr axes
            +ToRect() Rect
            +Overlaps() bool
            -GetCorners() Vector2_Arr
            -OverlapOnAxis() bool
        }
        class Projectile {
            -Unit _attacker
            -SkillData _skillData
            -Hitbox _logicalCollider
            -Vector2 _moveDir
            +Init() void
            -Update() void
            -ApplyHitEffect() void
            -DestroyProjectile() void
        }
        class ThreatShape {
            <<enum>>
            +ThreatShape shape
            +int range
            +int width
            +int depth
            +Create() ThreatTileData
        }
        class ThreatTileData {
            +ThreatShape shape
            +int range
            +int width
            +int depth
            +Create() ThreatTileData
        }
    }

    namespace Randering {
        class IMapColorizer {
            <<interface>>
            -ChangeRoomColor() void
        }
        class MapRandering {
            -CreateMap createMap
            -Sprite wallSprite
            -Sprite floorSprite
            -Sprite stairSprite
            +Construct() void
            +Initialize() UniTask
            +DoRandering() void
            -BuildTileCache() void
        }
    }

    namespace Map {
        class CreateMap {
            -int w
            -int h
            -int_Arr ddx
            -int_Arr ddy
            -BuildGraph() void
            -AddEdge() void
            -ConnectRooms() void
            -BridgeDisconnectedRoom() bool
        }
        class InteractableObject {
            +string Id
            +Vector3Int Position
            +float BaseInterest
            +float BaseDanger
            -InteractableObject() public
        }
        class FloorId {
            <<enum>>
            +int roomA
            +int roomB
            +int chunkAX
            +int chunkAY
            +Wall() Tile
            +Floor() Tile
            +Stair() Tile
            +CreateDefault() FloorConfig_Arr
        }
        class RoomRole {
            <<enum>>
            +int roomA
            +int roomB
            +int chunkAX
            +int chunkAY
            +Wall() Tile
            +Floor() Tile
            +Stair() Tile
            +CreateDefault() FloorConfig_Arr
        }
        class OccupationState {
            <<enum>>
            +int roomA
            +int roomB
            +int chunkAX
            +int chunkAY
            +Wall() Tile
            +Floor() Tile
            +Stair() Tile
            +CreateDefault() FloorConfig_Arr
        }
        class TileEffect {
            <<enum>>
            +int roomA
            +int roomB
            +int chunkAX
            +int chunkAY
            +Wall() Tile
            +Floor() Tile
            +Stair() Tile
            +CreateDefault() FloorConfig_Arr
        }
        class Footprint {
            <<enum>>
            +int roomA
            +int roomB
            +int chunkAX
            +int chunkAY
            +Wall() Tile
            +Floor() Tile
            +Stair() Tile
            +CreateDefault() FloorConfig_Arr
        }
        class Gate {
            <<struct>>
            +int roomA
            +int roomB
            +int chunkAX
            +int chunkAY
            +Wall() Tile
            +Floor() Tile
            +Stair() Tile
            +CreateDefault() FloorConfig_Arr
        }
        class Tile {
            <<struct>>
            +int roomA
            +int roomB
            +int chunkAX
            +int chunkAY
            +Wall() Tile
            +Floor() Tile
            +Stair() Tile
            +CreateDefault() FloorConfig_Arr
        }
        class Chunks {
            <<struct>>
            +int roomA
            +int roomB
            +int chunkAX
            +int chunkAY
            +Wall() Tile
            +Floor() Tile
            +Stair() Tile
            +CreateDefault() FloorConfig_Arr
        }
        class Floor {
            <<struct>>
            +int roomA
            +int roomB
            +int chunkAX
            +int chunkAY
            +Wall() Tile
            +Floor() Tile
            +Stair() Tile
            +CreateDefault() FloorConfig_Arr
        }
        class Map {
            <<struct>>
            +int roomA
            +int roomB
            +int chunkAX
            +int chunkAY
            +Wall() Tile
            +Floor() Tile
            +Stair() Tile
            +CreateDefault() FloorConfig_Arr
        }
        class FloorConfig {
            <<struct>>
            +int roomA
            +int roomB
            +int chunkAX
            +int chunkAY
            +Wall() Tile
            +Floor() Tile
            +Stair() Tile
            +CreateDefault() FloorConfig_Arr
        }
        class MapData {
            <<struct>>
            +int roomA
            +int roomB
            +int chunkAX
            +int chunkAY
            +Wall() Tile
            +Floor() Tile
            +Stair() Tile
            +CreateDefault() FloorConfig_Arr
        }
        class TileFactory {
            +int roomA
            +int roomB
            +int chunkAX
            +int chunkAY
            +Wall() Tile
            +Floor() Tile
            +Stair() Tile
            +CreateDefault() FloorConfig_Arr
        }
        class RoomIdGenerator {
            +int roomA
            +int roomB
            +int chunkAX
            +int chunkAY
            +Wall() Tile
            +Floor() Tile
            +Stair() Tile
            +CreateDefault() FloorConfig_Arr
        }
        class FloorConfigFactory {
            +int roomA
            +int roomB
            +int chunkAX
            +int chunkAY
            +Wall() Tile
            +Floor() Tile
            +Stair() Tile
            +CreateDefault() FloorConfig_Arr
        }
        class MapManager {
            -CreateMap _createMap
            -MapRandering _mapRandering
            -WaveSpawner _waveSpawner
            +MapRandering mapRandering
            +Construct() void
            +Initialize() UniTask
            +SetupAndVisualizeMap() void
        }
        class MapSaveModel {
            +Map Map
            -MapSaveModel() public
        }
        class MapSerializer {
            -var dto
            -return dto
            -Floor floor
            -var floorDto
            +ToJson() string
            +FromJson() Map
            -DtoToMap() return
            +MapToDto() MapDto
        }
        class MapDto {
            -var dto
            -return dto
            -Floor floor
            -var floorDto
            +ToJson() string
            +FromJson() Map
            -DtoToMap() return
            +MapToDto() MapDto
        }
        class FloorDto {
            -var dto
            -return dto
            -Floor floor
            -var floorDto
            +ToJson() string
            +FromJson() Map
            -DtoToMap() return
            +MapToDto() MapDto
        }
        class ChunksDto {
            -var dto
            -return dto
            -Floor floor
            -var floorDto
            +ToJson() string
            +FromJson() Map
            -DtoToMap() return
            +MapToDto() MapDto
        }
    }

    namespace Unit_Visual {
        class AnimationEventVfxSpawner {
            -VfxEventDefinition def
            -Transform reference
            -Vector3 pos
            -Quaternion rot
            +OnVfxEvent() void
        }
        class ThreatTileRenderer {
            -string AttackZoneLibraryResourcePath
            -bool_Arr_Arr LabelPatterns
            -SpriteLibraryAsset _attackZoneLibrary
            -string _attackZoneCategory
            +Construct() void
            -ThreatTileRenderer() public
            -GetLabelSprite() Sprite
            -GetOrCreateCellSprite() SpriteRenderer
        }
        class ThreatVisual {
            -string AttackZoneLibraryResourcePath
            -bool_Arr_Arr LabelPatterns
            -SpriteLibraryAsset _attackZoneLibrary
            -string _attackZoneCategory
            +Construct() void
            -ThreatTileRenderer() public
            -GetLabelSprite() Sprite
            -GetOrCreateCellSprite() SpriteRenderer
        }
        class UnitAnimationController {
            -float MOVE_THRESHOLD
            -int IDLE_DEBOUNCE
            -Animator animator
            -SpriteRenderer sr
            -Awake() void
            -BuildGraph() void
            -Update() void
            -TransitionTo() void
        }
        class AnimState {
            <<enum>>
            -float MOVE_THRESHOLD
            -int IDLE_DEBOUNCE
            -Animator animator
            -SpriteRenderer sr
            -Awake() void
            -BuildGraph() void
            -Update() void
            -TransitionTo() void
        }
        class UnitGenerate {
            +bool ShowAllVisionRanges
            -float SelectionRingDiameterRatio
            -float SelectionRingFlatten
            -float SelectionRingFootOffset
            -CreateRingSprite() Sprite
            -GetCache() VisualCache
            -GetMapRandering() MapRandering
            +Construct() void
        }
        class VisualCache {
            +bool ShowAllVisionRanges
            -float SelectionRingDiameterRatio
            -float SelectionRingFlatten
            -float SelectionRingFootOffset
            -CreateRingSprite() Sprite
            -GetCache() VisualCache
            -GetMapRandering() MapRandering
            +Construct() void
        }
        class UnitSpriteManager {
            -string UnitPrefabResourceFolder
            -var prefab
            -var visualDef
            -var list
            +GetPrefab() GameObject
            +GetSkills() List~SkillAction~
            +GetEngageDistance() int
            +GetSpriteLabelForDirection() void
        }
        class UnitVisual {
            +Unit boundUnit
            -LineRenderer _visionRangeLine
            -LineRenderer _perceptionRangeLine
            -LineRenderer _circularPerceptionLine
            +Setup() void
            +UpdateStatusLabel() void
            -EnsureStatusLabel() void
            +UpdateBelowLabel() void
        }
        class UnitVisualDefinition {
            +string unitTypeName
            +Vector2 footprint
            +int engageDistance
            +UnitStatsData stats
            +ApplyStatsTo() void
            +BuildSkillActions() List~SkillAction~
            +LoadDataFromJson() void
        }
        class UnitsJsonWrapper {
            +string unitTypeName
            +Vector2 footprint
            +int engageDistance
            +UnitStatsData stats
            +ApplyStatsTo() void
            +BuildSkillActions() List~SkillAction~
            +LoadDataFromJson() void
        }
        class UnitJsonNode {
            +string unitTypeName
            +Vector2 footprint
            +int engageDistance
            +UnitStatsData stats
            +ApplyStatsTo() void
            +BuildSkillActions() List~SkillAction~
            +LoadDataFromJson() void
        }
        class SkillsJsonWrapper {
            +string unitTypeName
            +Vector2 footprint
            +int engageDistance
            +UnitStatsData stats
            +ApplyStatsTo() void
            +BuildSkillActions() List~SkillAction~
            +LoadDataFromJson() void
        }
        class VfxEventDefinition {
            +string eventKey
            +GameObject effectPrefab
            +Transform referenceTransform
            +Vector3 positionOffset
        }
        class VFXManager {
            -Transform parent
            -Vector3 worldPos
            -var go
            -var ps
            +Spawn() void
            -GetWorldPos() Vector3
        }
        class WeaponAttachment {
            +Dir direction
            +Vector2 offset
            +int sortingOrder
            -DirectionalPose_Arr poses
            -Awake() void
            +UpdatePose() void
        }
        class DirectionalPose {
            +Dir direction
            +Vector2 offset
            +int sortingOrder
            -DirectionalPose_Arr poses
            -Awake() void
            +UpdatePose() void
        }
    }

    namespace Tests {
        class CreateMapPlayTests {
            -var cm
            -return cm
            -var mr
            -return mr
            -SetupCreateMap() CreateMap
            -SetupRenderer() MapRandering
            -Cleanup() void
            +InitMap_CreatesFloorArrays() void
        }
        class ExplorationSystemTests {
            -float low
            -float higherConcentration
            -float higherLevel
            -float higherUnderstanding
            +TrapDisarmSuccessRate_MonotonicAndClamped() void
            +TrapRecordedDirectDisarmThreshold_IsFiftyPercent() void
            +TrapExpectedRateErrorMargin_ShrinksWithUnderstanding() void
            +Parameters_MatchDocumentTable() void
        }
        class GoapPlannerTests {
            -Human _dummyUnit
            -var moveToDoor
            -var openDoor
            -var start
            -FakeAction() public
            +Execute() void
            +SetUp() void
            +TearDown() void
        }
        class FakeAction {
            -Human _dummyUnit
            -var moveToDoor
            -var openDoor
            -var start
            -FakeAction() public
            +Execute() void
            +SetUp() void
            +TearDown() void
        }
        class PerceptionSystemTests {
            -var record
            +DetectionCorrection_AlertAddsTwenty() void
            +GetMentalTier_Boundaries() void
            +MentalVisibilityCorrection_Table() void
            +MentalCorrectionForHuman_UnsetMaxMentalIsStable() void
        }
        class VisionSystemTests {
            -var candidates
            +ViewDistance_Table() void
            +AwarenessDistance_Table() void
            +AwarenessAngle_Table() void
            +AwarenessAngle_MaxedOut_MeansFullVisionConeIsPerceptionCone() void
        }
        class WeightSystemTests {
            -float result
            -var ratios
            -var entries
            -var deduped
            +PersonalWeightChange_Example() void
            +ReflectionRatio_Example1_WitnessAndIndirect() void
            +ReflectionRatio_Example2_ExperienceAndIndirect() void
            +ReflectionRatio_Example3_WitnessOnly() void
        }
    }

    namespace Unit_Session {
        class CombatEventService {
            -Unit attacker
            -var knowledge
            -bool victimIsHuman
            -bool attackerIsHuman
            +RecordKillWeightEvent() void
        }
        class DebugInputHandler {
            -IObjectResolver _resolver
            -GameSession _gameSession
            -GameSession _cachedGameSession
            -UnitGenerate _unitGenerate
            +Construct() void
            +HandleDebugInput() void
            +OnKeyDown_H() void
            -GetRandomStartRoomPos() Vector2Int
        }
        class GameBootstrapper {
            -IObjectResolver _resolver
            -var mapRandering
            -var waveSpawner
            -var humanWaveMgr
            -GameBootstrapper() public
            +StartAsync() UniTask
        }
        class GameCompositionRoot {
            #Awake() void
            #Configure() void
        }
        class GameSession {
            +UnitGenerate unitGenerate
            +MapManager mapManager
            +MapRandering mapRandering
            +HumanWaveManager humanWaveManager
            +Construct() void
            +GetUnitsInRoom() IReadOnlyList~Unit~
            +RegisterUnitPos() void
            +UnregisterUnitPos() void
        }
        class InputManager {
            +List~Unit~ selectedUnits
            -float DragThresholdPixels
            -bool _isMouseDown
            -bool _dragBoxActive
            +Construct() void
            -IsPointInFootprint() bool
            -FindUnitAtGridPos() Unit
            -ScreenToWorldPoint() Vector3
        }
        class ObjectSpawner {
            -MapRandering _mapRandering
            -GameObject visual
            -SpriteRenderer sr
            -Texture2D tex
            +Construct() void
            +SpawnObject() void
            +CollectObject() void
        }
        class UIManager {
            -GameSession _gameSession
            -float WaveStartWarningSeconds
            -HumanWaveManager wm
            -GUIStyle style
            +Construct() void
            -OnGUI() void
            -DrawWaveStartBanner() void
            -DrawTopLeftUI() void
        }
        class UnitRegistry {
            -OffenseProcessor _offenseProcessor
            -IObjectResolver _resolver
            -GameSession _gameSession
            -GameSession _cachedGameSession
            +Construct() void
            +RegisterUnitPos() void
            +UnregisterUnitPos() void
        }
    }

    namespace Wave_Editor {
        class WaveDataEditor {
            -SerializedProperty waveCooldownProp
            -SerializedProperty spawnModeProp
            -SerializedProperty targetFloorProp
            -SerializedProperty spawnCenterProp
            -OnEnable() void
            +OnInspectorGUI() void
        }
        class WaveSpawnerEditor {
            -SerializedProperty waveDataProp
            -WaveData newData
            -string path
            -OnEnable() void
            -OnDisable() void
            +OnInspectorGUI() void
        }
    }

    namespace UI {
        class DebugInfoPanel {
            -float ScrollZoomSpeed
            -InputManager _inputManager
            -DataManager _dataManager
            -GameSession _gameSession
            +Construct() void
            +OpenPanel() void
            +ClosePanel() void
            +BindEvent() void
        }
        class GameUIPresenter {
            -int debugPanelId
            -int statusPanelId
            +PostInitialize() void
            -BootSequence() UniTask
            -FadeIn() await
            -FadeOut() await
        }
        class SceneTransitionFade {
            -SceneTransitionFade _instance
            -CanvasGroup _canvasGroup
            -float RevealSeconds
            -float PostLoadSettleSeconds
            +EnsureInstance() SceneTransitionFade
            -BuildOverlay() void
            +LoadSceneWithCoverAsync() UniTask
            -FadeAsync() await
        }
        class StatusInfoPanel {
            -GameSession _gameSession
            -GameSession Session
            -int PanelWidth
            -int PanelY
            +OpenPanel() void
            +ClosePanel() void
            +BindEvent() void
            -OnGUI() void
        }
    }

    namespace Wave {
        class WaveState {
            <<enum>>
            +float waveCooldown
            +WaveSpawner targetSpawner
            +WaveState currentState
            +float cooldownTimer
            +Initialize() UniTask
            -WaveLoop() UniTaskVoid
            -PreSpawnWaveUnits() void
            -ResolveStairPositions() bool
        }
        class DummyTargetState {
            <<enum>>
            +float waveCooldown
            +WaveSpawner targetSpawner
            +WaveState currentState
            +float cooldownTimer
            +Initialize() UniTask
            -WaveLoop() UniTaskVoid
            -PreSpawnWaveUnits() void
            -ResolveStairPositions() bool
        }
        class HumanWaveManager {
            +float waveCooldown
            +WaveSpawner targetSpawner
            +WaveState currentState
            +float cooldownTimer
            +Initialize() UniTask
            -WaveLoop() UniTaskVoid
            -PreSpawnWaveUnits() void
            -ResolveStairPositions() bool
        }
        class WaveUnitGroup {
            +string unitTypeName
            +int count
            +string partyName
            +PartyFaction faction
        }
        class PartyFaction {
            <<enum>>
            +string unitTypeName
            +int count
            +string partyName
            +PartyFaction faction
        }
        class WavePartyConfig {
            +string unitTypeName
            +int count
            +string partyName
            +PartyFaction faction
        }
        class WaveData {
            +string unitTypeName
            +int count
            +string partyName
            +PartyFaction faction
        }
        class SpawnMode {
            <<enum>>
            +WaveData waveData
            -List~Monster~ spawnedMonsters
            -List~Party~ spawnedParties
            -Monster monster
            +SpawnWave() void
            -ResolveUnitType() Type
            -CreateUnitTypeInstance() UnitType
            -InstantiateMonster() Monster
        }
        class WaveSpawner {
            +WaveData waveData
            -List~Monster~ spawnedMonsters
            -List~Party~ spawnedParties
            -Monster monster
            +SpawnWave() void
            -ResolveUnitType() Type
            -CreateUnitTypeInstance() UnitType
            -InstantiateMonster() Monster
        }
    }

    namespace Unit_Party {
        class Party {
            +string Id
            +string Name
            +List~Human~ Members
            +Human Leader
            +AssignLeaderIfNeeded() void
            -Party() public
            +GetSurvivors() List~Unit~
        }
        class PartyService {
            -ObjectSpawner _objectSpawner
            -var party
            -return party
            -var knowledge
            +Construct() void
            +CreateParty() Party
            +CheckPartyWaveState() void
        }
    }

    namespace Building {
        class BuildingData {
            +Vector3Int Position
            +ProductionRule Rule
            +float ProductionProgress
            +bool IsProducing
            +Construct() void
            +Initialize() UniTask
            +Finalize() UniTask
            +CanInstallAt() bool
        }
        class BuildingManager {
            +Vector3Int Position
            +ProductionRule Rule
            +float ProductionProgress
            +bool IsProducing
            +Construct() void
            +Initialize() UniTask
            +Finalize() UniTask
            +CanInstallAt() bool
        }
    }

    namespace Unit_AI {
        class Action_Panic {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_MoveToPlayerTarget {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_CompletePlayerCommand {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_MoveToStairs {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_CrossStairs {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_RandomExplore {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_EngageEnemy {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_TrapJoinWait {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_MoveToTrap {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_TrapDisarmPerform {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_TrapBypass {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_TrapPass {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_TrapDestroy {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_MoveToInvestigateTarget {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_InvestigatePerform {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_AlertApproach {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_AlertPerimeterSearch {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_Wait {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_MoveToEscortSlotMelee {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_MoveToEscortSlotRanged {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Action_HoldFormation {
            -Dir randomDir
            -Vector2Int target
            -Vector3Int interactPos
            -bool isTrace
            -Action_Panic() public
            +Execute() void
            -Action_MoveToPlayerTarget() public
            -Action_CompletePlayerCommand() public
        }
        class Goal_Panic {
            -return 0f
            -return 99f
            -return 140f
            -IEnumerable~Unit~ enemies
            -Goal_Panic() public
            +GetPriority() float
            -Goal_PlayerCommand() public
            -Goal_UseStairs() public
        }
        class Goal_PlayerCommand {
            -return 0f
            -return 99f
            -return 140f
            -IEnumerable~Unit~ enemies
            -Goal_Panic() public
            +GetPriority() float
            -Goal_PlayerCommand() public
            -Goal_UseStairs() public
        }
        class Goal_UseStairs {
            -return 0f
            -return 99f
            -return 140f
            -IEnumerable~Unit~ enemies
            -Goal_Panic() public
            +GetPriority() float
            -Goal_PlayerCommand() public
            -Goal_UseStairs() public
        }
        class Goal_DefeatEnemy {
            -return 0f
            -return 99f
            -return 140f
            -IEnumerable~Unit~ enemies
            -Goal_Panic() public
            +GetPriority() float
            -Goal_PlayerCommand() public
            -Goal_UseStairs() public
        }
        class Goal_Explore {
            -return 0f
            -return 99f
            -return 140f
            -IEnumerable~Unit~ enemies
            -Goal_Panic() public
            +GetPriority() float
            -Goal_PlayerCommand() public
            -Goal_UseStairs() public
        }
        class Goal_TrapResponse {
            -return 0f
            -return 99f
            -return 140f
            -IEnumerable~Unit~ enemies
            -Goal_Panic() public
            +GetPriority() float
            -Goal_PlayerCommand() public
            -Goal_UseStairs() public
        }
        class Goal_Alert {
            -return 0f
            -return 99f
            -return 140f
            -IEnumerable~Unit~ enemies
            -Goal_Panic() public
            +GetPriority() float
            -Goal_PlayerCommand() public
            -Goal_UseStairs() public
        }
        class Goal_Investigate {
            -return 0f
            -return 99f
            -return 140f
            -IEnumerable~Unit~ enemies
            -Goal_Panic() public
            +GetPriority() float
            -Goal_PlayerCommand() public
            -Goal_UseStairs() public
        }
        class Goal_Wait {
            -return 0f
            -return 99f
            -return 140f
            -IEnumerable~Unit~ enemies
            -Goal_Panic() public
            +GetPriority() float
            -Goal_PlayerCommand() public
            -Goal_UseStairs() public
        }
        class Goal_ProtectiveFormation {
            -return 0f
            -return 99f
            -return 140f
            -IEnumerable~Unit~ enemies
            -Goal_Panic() public
            +GetPriority() float
            -Goal_PlayerCommand() public
            -Goal_UseStairs() public
        }
        class GoapState {
            +string Name
            +GoapState DesiredState
            +string ActionName
            +float Cost
            +GetPriority() float
            +IsValid() bool
            +Execute() void
            #GetClosestEnemy() Unit
        }
        class GoapGoal {
            +string Name
            +GoapState DesiredState
            +string ActionName
            +float Cost
            +GetPriority() float
            +IsValid() bool
            +Execute() void
            #GetClosestEnemy() Unit
        }
        class GoapAction {
            +string Name
            +GoapState DesiredState
            +string ActionName
            +float Cost
            +GetPriority() float
            +IsValid() bool
            +Execute() void
            #GetClosestEnemy() Unit
        }
        class GoapBrain {
            +string Name
            +GoapState DesiredState
            +string ActionName
            +float Cost
            +GetPriority() float
            +IsValid() bool
            +Execute() void
            #GetClosestEnemy() Unit
        }
        class GoapPlanner {
            -int MaxDepth
            +GoapState State
            +List~GoapAction~ Path
            +float Cost
            +Plan() List~GoapAction~
            +ApplyAll() GoapState
            -IsSatisfied() bool
            -Satisfies() bool
        }
        class Node {
            -int MaxDepth
            +GoapState State
            +List~GoapAction~ Path
            +float Cost
            +Plan() List~GoapAction~
            +ApplyAll() GoapState
            -IsSatisfied() bool
            -Satisfies() bool
        }
        class GoapWorldState {
            +int StairArrivalRadius
            -int dx
            -int dy
            -var state
            +DistanceToStairBlock() int
            +Build() GoapState
        }
    }

    namespace Unit_Debug {
        class AreaBasedDamageValidator {
            -GameSession _gameSession
            -GameSession Session
            -float testInterval
            -float testTimer
            -Update() void
            -ValidateAreaBasedDamage() void
            +TestOverlapCalculation() void
            +TestPartialVsFullHit() void
        }
        class AttackAngleTestValidator {
            -GameSession _gameSession
            -GameSession Session
            -float testInterval
            -float testTimer
            -Update() void
            -ValidateAttackAngleMechanics() void
            -ValidateMovementDirections() void
            +ValidateAttackRecognition() void
        }
    }

    namespace Unit_Skills {
        class SkillAction {
            -List~Unit~ result
            -bool isEnemy
            -return result
            -Vector2 size
            +BuildSkillHitbox() Hitbox
            -BuildRectHitboxWithAngle() return
            -BuildLineHitboxWithAngle() return
            +IsAvailable() bool
        }
        class SkillAction_Generic {
            -SkillData _d
            -float p
            -return p
            -float finalDelayMs
            -SkillAction_Generic() public
            +GetPriority() float
            +Execute() void
        }
        class SkillAction_Projectile {
            -SkillData _d
            -GameObject _projectilePrefab
            -float p
            -return p
            -SkillAction_Projectile() public
            +GetPriority() float
            +Execute() void
            -FireProjectile() void
        }
    }

    %% ==================== RELATIONSHIPS ====================
    JsonWeaponData <-- JsonToUnitPrefabConverter
    JsonEffectsData <-- JsonToUnitPrefabConverter
    JsonWeaponData <-- JsonWeightData
    JsonVisualData <-- JsonWeightData
    JsonVisualData <-- JsonWeaponData
    JsonEffectsData <-- JsonWeaponData
    JsonWeaponData <-- JsonEffectsData
    JsonVisualData <-- JsonEffectsData
    JsonWeaponData <-- JsonVisualData
    JsonEffectsData <-- JsonVisualData
    JsonWeaponData <-- JsonUnitData
    JsonEffectsData <-- JsonUnitData
    JsonWeaponData <-- JsonUnitDatabase
    JsonEffectsData <-- JsonUnitDatabase
    JsonWeaponData <-- JsonSkillData
    JsonEffectsData <-- JsonSkillData
    JsonWeaponData <-- JsonSkillDatabase
    JsonEffectsData <-- JsonSkillDatabase
    CreateMap <-- MapGeneratorTool
    Tile <-- MapViewTool
    FloorConfig <-- MapViewTool
    CreateMap <-- TestMapGen
    NativeRoutine <|-- DemoNative
    MonoRoutine <|-- DemoLoadMono
    SceneUIManager <|-- DemoLoadUIManager
    IPresenter <|-- DemoLoadUIPresenter
    SceneService <-- DemoLoadUIPresenter
    DemoLoadMono <-- DemoLoadUIPresenter
    MonoRoutine <|-- DemoCubeRotator
    SceneUIManager <|-- DemoLobbyUIManager
    IPresenter <|-- DemoLobbyUIPresenter
    SceneService <-- DemoLobbyUIPresenter
    CoreUIManager <-- DemoLobbyUIPresenter
    MonoRoutine <|-- DemoTitleMono
    SceneUIManager <|-- DemoTitleUIManager
    IPresenter <|-- DemoTitleUIPresenter
    SceneService <-- DemoTitleUIPresenter
    CoreUIManager <-- DemoTitleUIPresenter
    MonoRoutine <|-- DebugPanel
    ICustomPanel <|-- DebugPanel
    ICustomPanel <-- DebugPanel
    MonoRoutine <|-- LoadingFadePanel
    ICustomPanel <|-- LoadingFadePanel
    ICustomPanel <-- LoadingFadePanel
    MonoRoutine <|-- LoadingPanel
    ICustomPanel <|-- LoadingPanel
    MonoRoutine <|-- TitlePanel
    ICustomPanel <|-- TitlePanel
    ICustomPanel <-- TitlePanel
    SingletonMonoBehaviour <|-- Processor
    IRoutine <-- Processor
    NativeRoutine <|-- DataManager
    CoreUIManager <-- CoreLifetimeScope
    SceneService <-- GamePresenter
    IPresenter <|-- UIPresenter
    IRoutine <|-- MonoRoutine
    INativeRoutine <|-- NativeRoutine
    IRoutine <|-- INativeRoutine
    SceneLoadRequest <-- SceneName
    SceneLoadPhase <-- SceneName
    SceneName <-- SceneLoadPhase
    SceneLoadRequest <-- SceneLoadPhase
    NativeRoutine <|-- SceneService
    SceneName <-- SceneService
    SceneLoadPhase <-- SceneService
    MonoRoutine <|-- CustomButton
    UIAnimator <-- CustomButton
    MonoRoutine <|-- CustomImage
    UIAnimator <-- CustomImage
    MonoRoutine <|-- CustomSlider
    MonoRoutine <|-- CustomText
    SceneUIManager <|-- CoreUIManager
    MonoRoutine <|-- SceneUIManager
    ISceneWasLoaded <|-- SceneUIManager
    PanelType <-- SceneUIManager
    MonoRoutine <|-- FPSLogger
    CustomText <-- FPSLogger
    ResourceManager <-- BuildingData
    UnitGenerate <-- BuildingData
    NativeRoutine <|-- BuildingManager
    BuildingData <-- BuildingManager
    ResourceManager <-- BuildingManager
    ResourceManager <-- OffenseDebugWindow
    WildBaseSpawnerComponent <-- OffenseDebugWindow
    IEncyclopediaEntry <|-- EncyclopediaEntryData
    IEncyclopediaSystem <|-- EncyclopediaManager
    MonoRoutine <|-- UI_Encyclopedia
    ICustomPanel <|-- UI_Encyclopedia
    IEncyclopediaEntry <-- UI_Encyclopedia
    UI_EncyclopediaSlot <-- UI_Encyclopedia
    IEncyclopediaEntry <-- UI_EncyclopediaSlot
    UI_Encyclopedia <-- UI_EncyclopediaSlot
    RoomRole <-- CreateMap
    Gate <-- CreateMap
    DangerStage <-- InteractableObject
    Tile <-- FloorId
    TileEffect <-- FloorId
    Tile <-- RoomRole
    TileEffect <-- RoomRole
    Tile <-- OccupationState
    TileEffect <-- OccupationState
    Tile <-- TileEffect
    RoomRole <-- TileEffect
    Tile <-- Footprint
    TileEffect <-- Footprint
    Tile <-- Gate
    TileEffect <-- Gate
    TileEffect <-- Tile
    RoomRole <-- Tile
    Tile <-- Chunks
    TileEffect <-- Chunks
    Tile <-- Floor
    TileEffect <-- Floor
    Tile <-- Map
    TileEffect <-- Map
    Tile <-- FloorConfig
    TileEffect <-- FloorConfig
    Tile <-- MapData
    TileEffect <-- MapData
    Tile <-- TileFactory
    TileEffect <-- TileFactory
    Tile <-- RoomIdGenerator
    TileEffect <-- RoomIdGenerator
    Tile <-- FloorConfigFactory
    TileEffect <-- FloorConfigFactory
    NativeRoutine <|-- MapManager
    MapRandering <-- MapManager
    CreateMap <-- MapManager
    IDataModel <|-- MapSaveModel
    Tile <-- MapSerializer
    OccupationState <-- MapSerializer
    IData <|-- MapDto
    Tile <-- MapDto
    OccupationState <-- MapDto
    Tile <-- FloorDto
    OccupationState <-- FloorDto
    Tile <-- ChunksDto
    OccupationState <-- ChunksDto
    ResourceType <-- ResourceCost
    ResourceCost <-- ProductionRule
    ResourceType <-- ProductionRule
    NativeRoutine <|-- ResourceManager
    NativeRoutine <|-- MapRandering
    IMapColorizer <|-- MapRandering
    Chunks <-- MapRandering
    CreateMap <-- MapRandering
    MonoRoutine <|-- DebugInfoPanel
    ICustomPanel <|-- DebugInfoPanel
    DataManager <-- DebugInfoPanel
    InputManager <-- DebugInfoPanel
    UIPresenter <|-- GameUIPresenter
    MonoRoutine <|-- StatusInfoPanel
    ICustomPanel <|-- StatusInfoPanel
    GameSession <-- StatusInfoPanel
    MonoRoutine <|-- GameTitlePanel
    ICustomPanel <|-- GameTitlePanel
    IPresenter <|-- TitlePresenter
    SceneUIManager <|-- TitleUIManager
    GoapAction <|-- Action_Panic
    Dir <-- Action_Panic
    Unit <-- Action_Panic
    GoapAction <|-- Action_MoveToPlayerTarget
    Dir <-- Action_MoveToPlayerTarget
    Unit <-- Action_MoveToPlayerTarget
    GoapAction <|-- Action_CompletePlayerCommand
    Dir <-- Action_CompletePlayerCommand
    Unit <-- Action_CompletePlayerCommand
    GoapAction <|-- Action_MoveToStairs
    Dir <-- Action_MoveToStairs
    Unit <-- Action_MoveToStairs
    GoapAction <|-- Action_CrossStairs
    Dir <-- Action_CrossStairs
    Unit <-- Action_CrossStairs
    GoapAction <|-- Action_RandomExplore
    Dir <-- Action_RandomExplore
    Unit <-- Action_RandomExplore
    GoapAction <|-- Action_EngageEnemy
    Dir <-- Action_EngageEnemy
    Unit <-- Action_EngageEnemy
    GoapAction <|-- Action_TrapJoinWait
    Dir <-- Action_TrapJoinWait
    Unit <-- Action_TrapJoinWait
    GoapAction <|-- Action_MoveToTrap
    Dir <-- Action_MoveToTrap
    Unit <-- Action_MoveToTrap
    GoapAction <|-- Action_TrapDisarmPerform
    Dir <-- Action_TrapDisarmPerform
    Unit <-- Action_TrapDisarmPerform
    GoapAction <|-- Action_TrapBypass
    Dir <-- Action_TrapBypass
    Unit <-- Action_TrapBypass
    GoapAction <|-- Action_TrapPass
    Dir <-- Action_TrapPass
    Unit <-- Action_TrapPass
    GoapAction <|-- Action_TrapDestroy
    Dir <-- Action_TrapDestroy
    Unit <-- Action_TrapDestroy
    GoapAction <|-- Action_MoveToInvestigateTarget
    Dir <-- Action_MoveToInvestigateTarget
    Unit <-- Action_MoveToInvestigateTarget
    GoapAction <|-- Action_InvestigatePerform
    Dir <-- Action_InvestigatePerform
    Unit <-- Action_InvestigatePerform
    GoapAction <|-- Action_AlertApproach
    Dir <-- Action_AlertApproach
    Unit <-- Action_AlertApproach
    GoapAction <|-- Action_AlertPerimeterSearch
    Dir <-- Action_AlertPerimeterSearch
    Unit <-- Action_AlertPerimeterSearch
    GoapAction <|-- Action_Wait
    Dir <-- Action_Wait
    Unit <-- Action_Wait
    GoapAction <|-- Action_MoveToEscortSlotMelee
    Dir <-- Action_MoveToEscortSlotMelee
    Unit <-- Action_MoveToEscortSlotMelee
    GoapAction <|-- Action_MoveToEscortSlotRanged
    Dir <-- Action_MoveToEscortSlotRanged
    Unit <-- Action_MoveToEscortSlotRanged
    GoapAction <|-- Action_HoldFormation
    Dir <-- Action_HoldFormation
    Unit <-- Action_HoldFormation
    GoapGoal <|-- Goal_Panic
    Unit <-- Goal_Panic
    GoapGoal <|-- Goal_PlayerCommand
    Unit <-- Goal_PlayerCommand
    GoapGoal <|-- Goal_UseStairs
    Unit <-- Goal_UseStairs
    GoapGoal <|-- Goal_DefeatEnemy
    Unit <-- Goal_DefeatEnemy
    GoapGoal <|-- Goal_Explore
    Unit <-- Goal_Explore
    GoapGoal <|-- Goal_TrapResponse
    Unit <-- Goal_TrapResponse
    GoapGoal <|-- Goal_Alert
    Unit <-- Goal_Alert
    GoapGoal <|-- Goal_Investigate
    Unit <-- Goal_Investigate
    GoapGoal <|-- Goal_Wait
    Unit <-- Goal_Wait
    GoapGoal <|-- Goal_ProtectiveFormation
    Unit <-- Goal_ProtectiveFormation
    Dir <-- GoapState
    Human <-- GoapState
    Dir <-- GoapGoal
    Human <-- GoapGoal
    Dir <-- GoapAction
    Human <-- GoapAction
    Dir <-- GoapBrain
    Human <-- GoapBrain
    GoapAction <-- GoapPlanner
    Node <-- GoapPlanner
    GoapAction <-- Node
    GoapState <-- Node
    Hitbox <-- DefenseType
    DefenseCandidate <-- DefenseType
    Hitbox <-- DefenseCandidate
    DefenseType <-- DefenseCandidate
    Hitbox <-- DefenseSystem
    DefenseCandidate <-- DefenseSystem
    Hitbox <-- Projectile
    SkillData <-- Projectile
    Hitbox <-- ThreatShape
    Hitbox <-- ThreatTileData
    ThreatShape <-- ThreatTileData
    IUnitComponent <|-- AIStateComponent
    UnitAIWeightState <-- AIStateComponent
    ThreatTileData <-- AIStateComponent
    IUnitComponent <|-- BaseStatComponent
    Unit <-- BaseStatComponent
    IUnitComponent <|-- CombatStatComponent
    Unit <-- CombatStatComponent
    IUnitComponent <|-- CombatStateComponent
    UnitCombatState <-- CombatStateComponent
    Unit <-- CombatStateComponent
    Unit <-- FactionData
    IUnitComponent <|-- HealthComponent
    Unit <-- HealthComponent
    IFactionBehavior <|-- HumanFactionBehavior
    IUnitComponent <|-- MemoryComponent
    PersonalMapKnowledge <-- MemoryComponent
    Unit <-- MemoryComponent
    IMapColorizer <-- OffenseProcessor
    IUnitComponent <|-- PartyComponent
    Party <-- PartyComponent
    Unit <-- PartyComponent
    IUnitComponent <|-- PerceptionComponent
    UnitPerceptionState <-- PerceptionComponent
    Unit <-- PerceptionComponent
    IFactionBehavior <|-- PlayerMonsterBehavior
    Unit <-- RoomType
    Unit <-- Room
    IUnitComponent <|-- StatusEffectsComponent
    UnitStatusEffects <-- StatusEffectsComponent
    Unit <-- StatusEffectsComponent
    HumanKnowledgeBase <-- Unit
    HealthComponent <-- Unit
    UnitFunction <|-- Human
    HumanKnowledgeBase <-- Human
    HealthComponent <-- Human
    UnitFunction <|-- Monster
    HumanKnowledgeBase <-- Monster
    HealthComponent <-- Monster
    ThreatTileData <-- UnitStatusEffects
    Unit <-- UnitStatusEffects
    ThreatTileData <-- UnitCombatState
    Unit <-- UnitCombatState
    ThreatTileData <-- UnitPerceptionState
    Unit <-- UnitPerceptionState
    ThreatTileData <-- UnitAIWeightState
    Unit <-- UnitAIWeightState
    Unit <|-- UnitFunction
    IVisionContext <|-- UnitFunction
    Tile <-- UnitFunction
    Dir <-- UnitFunction
    UnitType <|-- Knight
    UnitType <|-- HumanBaseType
    UnitType <|-- MeleeTank
    UnitType <|-- WildBaseType
    UnitType <|-- Archer
    IUnitComponent <|-- VisionStatComponent
    Unit <-- VisionStatComponent
    IUnitComponent <|-- WildBaseSpawnerComponent
    Room <-- WildBaseSpawnerComponent
    UnitType <-- WildBaseSpawnerComponent
    IFactionBehavior <|-- WildMonsterBehavior
    Hitbox <-- AreaBasedDamageValidator
    GameSession <-- AreaBasedDamageValidator
    Hitbox <-- AttackAngleTestValidator
    GameSession <-- AttackAngleTestValidator
    Human <-- FormationState
    Human <-- TrapPhase
    TrapPhase <-- TrapInteractionState
    Human <-- TrapInteractionState
    WaitReason <-- WaitState
    IMovementAlgorithm <|-- AStarMovement
    FactionData <-- AStarMovement
    AStarNode <-- AStarMovement
    FactionData <-- AStarNode
    AStarMovement <|-- RoomConfinedMovement
    Room <-- RoomConfinedMovement
    Monster <-- Party
    Human <-- Party
    Unit <-- PartyService
    DangerStage <-- PartyService
    Unit <-- CombatEventService
    Human <-- DebugInputHandler
    UnitGenerate <-- DebugInputHandler
    CoreLifetimeScope <|-- GameCompositionRoot
    NativeRoutine <|-- GameSession
    IOffenseQuery <|-- GameSession
    Chunks <-- GameSession
    Floor <-- GameSession
    BuildingData <-- InputManager
    ResourceManager <-- InputManager
    MapRandering <-- ObjectSpawner
    GameSession <-- UIManager
    HumanWaveManager <-- UIManager
    OffenseProcessor <-- UnitRegistry
    GameSession <-- UnitRegistry
    Hitbox <-- SkillAction
    Unit <-- SkillAction
    SkillAction <|-- SkillAction_Generic
    SkillData <-- SkillAction_Generic
    SkillAction <|-- SkillAction_Projectile
    Hitbox <-- SkillAction_Projectile
    Projectile <-- SkillAction_Projectile
    IVisionTileHandler <|-- ObjectPerceptionHandler
    PerceptionOutcome <-- ObjectPerceptionHandler
    PerceptionTargetKind <-- PerceptionRecord
    PerceptionReactionCandidate <-- PerceptionRecord
    IVisionTileHandler <|-- TerrainRevealHandler
    IVisionTileHandler <|-- UnitPerceptionHandler
    PerceptionOutcome <-- UnitPerceptionHandler
    VisionDirectionReason <-- VisionMath
    Dir <-- VisionMath
    VisionDirectionReason <-- VisionDirectionCandidate
    Dir <-- VisionDirectionCandidate
    VfxEventDefinition <-- AnimationEventVfxSpawner
    Hitbox <-- ThreatTileRenderer
    Unit <-- ThreatTileRenderer
    Hitbox <-- ThreatVisual
    Unit <-- ThreatVisual
    AnimState <-- UnitAnimationController
    Unit <-- UnitGenerate
    CreateMap <-- UnitGenerate
    Unit <-- VisualCache
    CreateMap <-- VisualCache
    Unit <-- UnitVisual
    SkillsJsonWrapper <-- UnitVisualDefinition
    SkillData <-- UnitVisualDefinition
    SkillsJsonWrapper <-- UnitsJsonWrapper
    SkillData <-- UnitsJsonWrapper
    UnitsJsonWrapper <-- UnitJsonNode
    SkillData <-- UnitJsonNode
    UnitsJsonWrapper <-- SkillsJsonWrapper
    SkillData <-- SkillsJsonWrapper
    DirectionalPose <-- WeaponAttachment
    Dir <-- WeaponAttachment
    Dir <-- DirectionalPose
    IncidentEntry <-- HumanKnowledgeBase
    DangerStage <-- HumanKnowledgeBase
    IncidentEntry <-- WipeoutTraceRecord
    DangerStage <-- WipeoutTraceRecord
    EventId <-- IncidentEntry
    InfoType <-- IncidentEntry
    RoomExploreState <-- PersonalMapKnowledge
    InfoType <-- PersonalMapKnowledge
    RoomExploreState <-- MonsterSighting
    InfoType <-- MonsterSighting
    InfoType <-- RoomExploreState
    RoomExploreState <-- RoomKnowledge
    InfoType <-- RoomKnowledge
    InfoType <-- PersonalWeightRecord
    WeightType <-- PersonalWeightRecord
    Human <-- WaveState
    Party <-- WaveState
    Human <-- DummyTargetState
    Party <-- DummyTargetState
    NativeRoutine <|-- HumanWaveManager
    Human <-- HumanWaveManager
    Party <-- HumanWaveManager
    RoomRole <-- WaveUnitGroup
    PartyFaction <-- WaveUnitGroup
    RoomRole <-- PartyFaction
    WaveUnitGroup <-- PartyFaction
    RoomRole <-- WavePartyConfig
    PartyFaction <-- WavePartyConfig
    PartyFaction <-- WaveData
    SpawnMode <-- WaveData
    Human <-- SpawnMode
    UnitGenerate <-- SpawnMode
    Human <-- WaveSpawner
    UnitGenerate <-- WaveSpawner
    SpawnMode <-- WaveDataEditor
    WaveData <-- WaveSpawnerEditor
    Tile <-- CreateMapPlayTests
    RoomRole <-- CreateMapPlayTests
    GoapAction <-- GoapPlannerTests
    Human <-- GoapPlannerTests
    GoapAction <|-- FakeAction
    GoapAction <-- FakeAction
    Human <-- FakeAction
    TargetType <-- FXMaterialDesignerWindow
    StylePreset <-- FXMaterialDesignerWindow
    StylePreset <-- TargetType
    TargetType <-- StylePreset
```
