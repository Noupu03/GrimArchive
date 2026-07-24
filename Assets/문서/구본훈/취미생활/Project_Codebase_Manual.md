# 📖 GrimArchive Prototype - 코드베이스 종합 시스템 설명서

본 문서는 개발자 및 기획자가 코드베이스 전체 구조와 각 모듈/클래스의 역할, 변수, 메서드, 종속성을 시각적 차트 대신 **명확한 설명과 체계적인 텍스트**로 손쉽게 이해할 수 있도록 구성된 종합 명세서입니다.

## 1. 🏗️ 프로젝트 아키텍처 개요

- **핵심 프레임워크 패턴**: VContainer 기반 의존성 주입(DI), R3 반응형 이벤트 스트림(Reactive Messaging), MonoRoutine 프레임워크.
- **주요 도메인 구성**: `Unit` (유닛 생명주기/AI/시야), `Map` (타일 맵 생성 및 그리드), `Building` (건설 및 생산), `UI` (HUD 및 도감 시스템).
- **총 분석 패키지 수**: 59개 모듈
- **총 분석 스크립트/클래스 수**: 350개

---

## 2. 🧩 시스템 패키지별 상세 스크립트 명세

### 📦 Package: `Building`

**모듈 내 포함 스크립트 수**: 2개

#### 📄 클래스: `BuildingData` (class)
- **경로**: `Script/Building/BuildingManager.cs`
- **클래스 목적 및 의도**: `BuildingData` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] Position` (Vector3Int): BuildingData의 내부 데이터 또는 의존 참조 변수
  - `[public] Rule` (ProductionRule): BuildingData의 내부 데이터 또는 의존 참조 변수
  - `[public] ProductionProgress` (float): BuildingData의 내부 데이터 또는 의존 참조 변수
  - `[public] IsProducing` (bool): BuildingData의 내부 데이터 또는 의존 참조 변수
  - `[private] mapManager` (MapManager): BuildingData의 내부 데이터 또는 의존 참조 변수
  - `[private] resourceManager` (ResourceManager): BuildingData의 내부 데이터 또는 의존 참조 변수
  - `[private] mapRandering` (MapRandering): BuildingData의 내부 데이터 또는 의존 참조 변수
  - `[private] createMap` (CreateMap): BuildingData의 내부 데이터 또는 의존 참조 변수
  - `[private] unitGenerate` (UnitGenerate): BuildingData의 내부 데이터 또는 의존 참조 변수
  - `[private] gameSession` (GameSession): BuildingData의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Construct(MapManager mapManager, ResourceManager resourceManager, MapRandering mapRandering, CreateMap createMap, UnitGenerate unitGenerate, GameSession gameSession)` ➔ `void`: Construct 관련 로직 수행 함수
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[public] Finalize()` ➔ `UniTask`: Finalize 관련 로직 수행 함수
  - `[public] CanInstallAt(Vector3Int pos)` ➔ `bool`: CanInstallAt 관련 로직 수행 함수
  - `[public] GetBuildingAt(Vector3Int pos)` ➔ `BuildingData`: GetBuildingAt 관련 로직 수행 함수
  - `[public] UpdateProcess()` ➔ `void`: UpdateProcess 관련 로직 수행 함수
  - `[private] SpawnUnitFromBuilding(BuildingData b)` ➔ `void`: SpawnUnitFromBuilding 관련 로직 수행 함수
  - `[public] InstallBuilding(Vector3Int pos, ProductionRule rule, Sprite buildingSprite)` ➔ `bool`: InstallBuilding 관련 로직 수행 함수

#### 📄 클래스: `BuildingManager` (class)
- **경로**: `Script/Building/BuildingManager.cs`
- **클래스 목적 및 의도**: 게임 내 `Building` 관련 전역 상태 및 루프 시스템을 총괄 관리하는 매니저 클래스입니다.
- **상속 및 구현 관계**: `NativeRoutine` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] Position` (Vector3Int): BuildingManager의 내부 데이터 또는 의존 참조 변수
  - `[public] Rule` (ProductionRule): BuildingManager의 내부 데이터 또는 의존 참조 변수
  - `[public] ProductionProgress` (float): BuildingManager의 내부 데이터 또는 의존 참조 변수
  - `[public] IsProducing` (bool): BuildingManager의 내부 데이터 또는 의존 참조 변수
  - `[private] mapManager` (MapManager): BuildingManager의 내부 데이터 또는 의존 참조 변수
  - `[private] resourceManager` (ResourceManager): BuildingManager의 내부 데이터 또는 의존 참조 변수
  - `[private] mapRandering` (MapRandering): BuildingManager의 내부 데이터 또는 의존 참조 변수
  - `[private] createMap` (CreateMap): BuildingManager의 내부 데이터 또는 의존 참조 변수
  - `[private] unitGenerate` (UnitGenerate): BuildingManager의 내부 데이터 또는 의존 참조 변수
  - `[private] gameSession` (GameSession): BuildingManager의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Construct(MapManager mapManager, ResourceManager resourceManager, MapRandering mapRandering, CreateMap createMap, UnitGenerate unitGenerate, GameSession gameSession)` ➔ `void`: Construct 관련 로직 수행 함수
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[public] Finalize()` ➔ `UniTask`: Finalize 관련 로직 수행 함수
  - `[public] CanInstallAt(Vector3Int pos)` ➔ `bool`: CanInstallAt 관련 로직 수행 함수
  - `[public] GetBuildingAt(Vector3Int pos)` ➔ `BuildingData`: GetBuildingAt 관련 로직 수행 함수
  - `[public] UpdateProcess()` ➔ `void`: UpdateProcess 관련 로직 수행 함수
  - `[private] SpawnUnitFromBuilding(BuildingData b)` ➔ `void`: SpawnUnitFromBuilding 관련 로직 수행 함수
  - `[public] InstallBuilding(Vector3Int pos, ProductionRule rule, Sprite buildingSprite)` ➔ `bool`: InstallBuilding 관련 로직 수행 함수

---

### 📦 Package: `Camera`

**모듈 내 포함 스크립트 수**: 1개

#### 📄 클래스: `CameraController` (class)
- **경로**: `Script/Camera/CameraController.cs`
- **클래스 목적 및 의도**: `CameraController`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoBehaviour` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] panSpeed` (float): CameraController의 내부 데이터 또는 의존 참조 변수
  - `[public] zoomSpeed` (float): CameraController의 내부 데이터 또는 의존 참조 변수
  - `[public] minZoom` (float): CameraController의 내부 데이터 또는 의존 참조 변수
  - `[public] maxZoom` (float): CameraController의 내부 데이터 또는 의존 참조 변수
  - `[private] eventSystem` (var): CameraController의 내부 데이터 또는 의존 참조 변수
  - `[private] standalone` (var): CameraController의 내부 데이터 또는 의존 참조 변수
  - `[private] pos` (Vector3): CameraController의 내부 데이터 또는 의존 참조 변수
  - `[private] move` (float): CameraController의 내부 데이터 또는 의존 참조 변수
  - `[private] scroll` (float): CameraController의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] AutoAttach()` ➔ `void`: AutoAttach 관련 로직 수행 함수
  - `[private] Update()` ➔ `void`: Update 관련 로직 수행 함수

---

### 📦 Package: `Editor`

**모듈 내 포함 스크립트 수**: 23개

#### 📄 클래스: `EncyclopediaSetup` (class)
- **경로**: `Editor/EncyclopediaSetup.cs`
- **클래스 목적 및 의도**: `EncyclopediaSetup`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] prefabPath` (string): EncyclopediaSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] addressableKey` (string): EncyclopediaSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] prefab` (GameObject): EncyclopediaSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] settings` (AddressableAssetSettings): EncyclopediaSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] group` (AddressableAssetGroup): EncyclopediaSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] guid` (string): EncyclopediaSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] entry` (AddressableAssetEntry): EncyclopediaSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] addressableKey` (string): EncyclopediaSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] settings` (AddressableAssetSettings): EncyclopediaSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] found` (bool): EncyclopediaSetup의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] FixAddressables()` ➔ `void`: FixAddressables 관련 로직 수행 함수
  - `[public] RemoveAddressables()` ➔ `void`: RemoveAddressables 관련 로직 수행 함수

#### 📄 클래스: `HaareDemoSetup` (class)
- **경로**: `Editor/HaareDemoSetup.cs`
- **클래스 목적 및 의도**: `HaareDemoSetup`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] PrefabFolder` (string): HaareDemoSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] SceneFolder` (string): HaareDemoSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] Scenes` (string[]): HaareDemoSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] settings` (var): HaareDemoSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] group` (var): HaareDemoSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] scenePath` (string): HaareDemoSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] settings` (var): HaareDemoSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] removedCount` (int): HaareDemoSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] entries` (var): HaareDemoSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] shouldRemove` (bool): HaareDemoSetup의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] SetupHaareDemoAddressables()` ➔ `void`: SetupHaareDemoAddressables 관련 로직 수행 함수
  - `[public] RemoveHaareDemoAddressables()` ➔ `void`: RemoveHaareDemoAddressables 관련 로직 수행 함수
  - `[private] RegisterEntry(string assetPath, string address, AddressableAssetSettings settings, AddressableAssetGroup group)` ➔ `void`: RegisterEntry 관련 로직 수행 함수

#### 📄 클래스: `HaareUIAddressableSetupWindow` (class)
- **경로**: `Editor/HaareUIAddressableSetupWindow.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `HaareUIAddressableSetupWindow` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `EditorWindow` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _prefabName` (string): HaareUIAddressableSetupWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] window` (var): HaareUIAddressableSetupWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] prefabPath` (string): HaareUIAddressableSetupWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] addressableKey` (string): HaareUIAddressableSetupWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] prefab` (GameObject): HaareUIAddressableSetupWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] settings` (AddressableAssetSettings): HaareUIAddressableSetupWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] group` (AddressableAssetGroup): HaareUIAddressableSetupWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] guid` (string): HaareUIAddressableSetupWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] entry` (AddressableAssetEntry): HaareUIAddressableSetupWindow의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ShowWindow()` ➔ `void`: ShowWindow 관련 로직 수행 함수
  - `[private] OnGUI()` ➔ `void`: OnGUI 관련 로직 수행 함수
  - `[private] RegisterAddressable(string prefabName)` ➔ `void`: RegisterAddressable 관련 로직 수행 함수
  - `[private] RemoveAddressable(string prefabName)` ➔ `void`: RemoveAddressable 관련 로직 수행 함수

#### 📄 클래스: `HaareUISetup` (class)
- **경로**: `Editor/HaareUISetup.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `HaareUISetup` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] OutputFolder` (string): HaareUISetup의 내부 데이터 또는 의존 참조 변수
  - `[private] CoreCanvasAddress` (string): HaareUISetup의 내부 데이터 또는 의존 참조 변수
  - `[private] DebugPanelAddress` (string): HaareUISetup의 내부 데이터 또는 의존 참조 변수
  - `[private] LoadingFadePanelAddress` (string): HaareUISetup의 내부 데이터 또는 의존 참조 변수
  - `[private] ScenePath` (string): HaareUISetup의 내부 데이터 또는 의존 참조 변수
  - `[private] KoreanFontPath` (string): HaareUISetup의 내부 데이터 또는 의존 참조 변수
  - `[private] canvasPrefab` (GameObject): HaareUISetup의 내부 데이터 또는 의존 참조 변수
  - `[private] panelPrefab` (GameObject): HaareUISetup의 내부 데이터 또는 의존 참조 변수
  - `[private] fadePanelPrefab` (GameObject): HaareUISetup의 내부 데이터 또는 의존 참조 변수
  - `[private] root` (var): HaareUISetup의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] SetupHaareUI()` ➔ `void`: SetupHaareUI 관련 로직 수행 함수
  - `[private] CreateCoreCanvasPrefab()` ➔ `GameObject`: CreateCoreCanvasPrefab 관련 로직 수행 함수
  - `[private] CreateDebugInfoPanelPrefab()` ➔ `GameObject`: CreateDebugInfoPanelPrefab 관련 로직 수행 함수

#### 📄 클래스: `JsonToUnitPrefabConverter` (class)
- **경로**: `Editor/JsonToUnitPrefabConverter.cs`
- **클래스 목적 및 의도**: `JsonToUnitPrefabConverter`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] UnitsJsonPath` (string): JsonToUnitPrefabConverter의 내부 데이터 또는 의존 참조 변수
  - `[private] SkillsJsonPath` (string): JsonToUnitPrefabConverter의 내부 데이터 또는 의존 참조 변수
  - `[private] OutputFolder` (string): JsonToUnitPrefabConverter의 내부 데이터 또는 의존 참조 변수
  - `[public] isSpecialUnit` (bool): JsonToUnitPrefabConverter의 내부 데이터 또는 의존 참조 변수
  - `[public] isInterestTarget` (bool): JsonToUnitPrefabConverter의 내부 데이터 또는 의존 참조 변수
  - `[public] baseInterest` (float): JsonToUnitPrefabConverter의 내부 데이터 또는 의존 참조 변수
  - `[public] baseDanger` (float): JsonToUnitPrefabConverter의 내부 데이터 또는 의존 참조 변수
  - `[public] heavyHitThreshold` (float): JsonToUnitPrefabConverter의 내부 데이터 또는 의존 참조 변수
  - `[public] stealth` (float): JsonToUnitPrefabConverter의 내부 데이터 또는 의존 참조 변수
  - `[public] baseVisibility` (float): JsonToUnitPrefabConverter의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ConvertJsonToPrefabs()` ➔ `void`: ConvertJsonToPrefabs 관련 로직 수행 함수

#### 📄 클래스: `JsonWeightData` (class)
- **경로**: `Editor/JsonToUnitPrefabConverter.cs`
- **클래스 목적 및 의도**: `JsonWeightData` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] UnitsJsonPath` (string): JsonWeightData의 내부 데이터 또는 의존 참조 변수
  - `[private] SkillsJsonPath` (string): JsonWeightData의 내부 데이터 또는 의존 참조 변수
  - `[private] OutputFolder` (string): JsonWeightData의 내부 데이터 또는 의존 참조 변수
  - `[public] isSpecialUnit` (bool): JsonWeightData의 내부 데이터 또는 의존 참조 변수
  - `[public] isInterestTarget` (bool): JsonWeightData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseInterest` (float): JsonWeightData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseDanger` (float): JsonWeightData의 내부 데이터 또는 의존 참조 변수
  - `[public] heavyHitThreshold` (float): JsonWeightData의 내부 데이터 또는 의존 참조 변수
  - `[public] stealth` (float): JsonWeightData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseVisibility` (float): JsonWeightData의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ConvertJsonToPrefabs()` ➔ `void`: ConvertJsonToPrefabs 관련 로직 수행 함수

#### 📄 클래스: `JsonWeaponData` (class)
- **경로**: `Editor/JsonToUnitPrefabConverter.cs`
- **클래스 목적 및 의도**: `JsonWeaponData` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] UnitsJsonPath` (string): JsonWeaponData의 내부 데이터 또는 의존 참조 변수
  - `[private] SkillsJsonPath` (string): JsonWeaponData의 내부 데이터 또는 의존 참조 변수
  - `[private] OutputFolder` (string): JsonWeaponData의 내부 데이터 또는 의존 참조 변수
  - `[public] isSpecialUnit` (bool): JsonWeaponData의 내부 데이터 또는 의존 참조 변수
  - `[public] isInterestTarget` (bool): JsonWeaponData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseInterest` (float): JsonWeaponData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseDanger` (float): JsonWeaponData의 내부 데이터 또는 의존 참조 변수
  - `[public] heavyHitThreshold` (float): JsonWeaponData의 내부 데이터 또는 의존 참조 변수
  - `[public] stealth` (float): JsonWeaponData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseVisibility` (float): JsonWeaponData의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ConvertJsonToPrefabs()` ➔ `void`: ConvertJsonToPrefabs 관련 로직 수행 함수

#### 📄 클래스: `JsonEffectsData` (class)
- **경로**: `Editor/JsonToUnitPrefabConverter.cs`
- **클래스 목적 및 의도**: `JsonEffectsData` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] UnitsJsonPath` (string): JsonEffectsData의 내부 데이터 또는 의존 참조 변수
  - `[private] SkillsJsonPath` (string): JsonEffectsData의 내부 데이터 또는 의존 참조 변수
  - `[private] OutputFolder` (string): JsonEffectsData의 내부 데이터 또는 의존 참조 변수
  - `[public] isSpecialUnit` (bool): JsonEffectsData의 내부 데이터 또는 의존 참조 변수
  - `[public] isInterestTarget` (bool): JsonEffectsData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseInterest` (float): JsonEffectsData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseDanger` (float): JsonEffectsData의 내부 데이터 또는 의존 참조 변수
  - `[public] heavyHitThreshold` (float): JsonEffectsData의 내부 데이터 또는 의존 참조 변수
  - `[public] stealth` (float): JsonEffectsData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseVisibility` (float): JsonEffectsData의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ConvertJsonToPrefabs()` ➔ `void`: ConvertJsonToPrefabs 관련 로직 수행 함수

#### 📄 클래스: `JsonVisualData` (class)
- **경로**: `Editor/JsonToUnitPrefabConverter.cs`
- **클래스 목적 및 의도**: `JsonVisualData` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] UnitsJsonPath` (string): JsonVisualData의 내부 데이터 또는 의존 참조 변수
  - `[private] SkillsJsonPath` (string): JsonVisualData의 내부 데이터 또는 의존 참조 변수
  - `[private] OutputFolder` (string): JsonVisualData의 내부 데이터 또는 의존 참조 변수
  - `[public] isSpecialUnit` (bool): JsonVisualData의 내부 데이터 또는 의존 참조 변수
  - `[public] isInterestTarget` (bool): JsonVisualData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseInterest` (float): JsonVisualData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseDanger` (float): JsonVisualData의 내부 데이터 또는 의존 참조 변수
  - `[public] heavyHitThreshold` (float): JsonVisualData의 내부 데이터 또는 의존 참조 변수
  - `[public] stealth` (float): JsonVisualData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseVisibility` (float): JsonVisualData의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ConvertJsonToPrefabs()` ➔ `void`: ConvertJsonToPrefabs 관련 로직 수행 함수

#### 📄 클래스: `JsonUnitData` (class)
- **경로**: `Editor/JsonToUnitPrefabConverter.cs`
- **클래스 목적 및 의도**: `JsonUnitData` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] UnitsJsonPath` (string): JsonUnitData의 내부 데이터 또는 의존 참조 변수
  - `[private] SkillsJsonPath` (string): JsonUnitData의 내부 데이터 또는 의존 참조 변수
  - `[private] OutputFolder` (string): JsonUnitData의 내부 데이터 또는 의존 참조 변수
  - `[public] isSpecialUnit` (bool): JsonUnitData의 내부 데이터 또는 의존 참조 변수
  - `[public] isInterestTarget` (bool): JsonUnitData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseInterest` (float): JsonUnitData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseDanger` (float): JsonUnitData의 내부 데이터 또는 의존 참조 변수
  - `[public] heavyHitThreshold` (float): JsonUnitData의 내부 데이터 또는 의존 참조 변수
  - `[public] stealth` (float): JsonUnitData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseVisibility` (float): JsonUnitData의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ConvertJsonToPrefabs()` ➔ `void`: ConvertJsonToPrefabs 관련 로직 수행 함수

#### 📄 클래스: `JsonUnitDatabase` (class)
- **경로**: `Editor/JsonToUnitPrefabConverter.cs`
- **클래스 목적 및 의도**: `JsonUnitDatabase` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] UnitsJsonPath` (string): JsonUnitDatabase의 내부 데이터 또는 의존 참조 변수
  - `[private] SkillsJsonPath` (string): JsonUnitDatabase의 내부 데이터 또는 의존 참조 변수
  - `[private] OutputFolder` (string): JsonUnitDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] isSpecialUnit` (bool): JsonUnitDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] isInterestTarget` (bool): JsonUnitDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] baseInterest` (float): JsonUnitDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] baseDanger` (float): JsonUnitDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] heavyHitThreshold` (float): JsonUnitDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] stealth` (float): JsonUnitDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] baseVisibility` (float): JsonUnitDatabase의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ConvertJsonToPrefabs()` ➔ `void`: ConvertJsonToPrefabs 관련 로직 수행 함수

#### 📄 클래스: `JsonSkillData` (class)
- **경로**: `Editor/JsonToUnitPrefabConverter.cs`
- **클래스 목적 및 의도**: `JsonSkillData` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] UnitsJsonPath` (string): JsonSkillData의 내부 데이터 또는 의존 참조 변수
  - `[private] SkillsJsonPath` (string): JsonSkillData의 내부 데이터 또는 의존 참조 변수
  - `[private] OutputFolder` (string): JsonSkillData의 내부 데이터 또는 의존 참조 변수
  - `[public] isSpecialUnit` (bool): JsonSkillData의 내부 데이터 또는 의존 참조 변수
  - `[public] isInterestTarget` (bool): JsonSkillData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseInterest` (float): JsonSkillData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseDanger` (float): JsonSkillData의 내부 데이터 또는 의존 참조 변수
  - `[public] heavyHitThreshold` (float): JsonSkillData의 내부 데이터 또는 의존 참조 변수
  - `[public] stealth` (float): JsonSkillData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseVisibility` (float): JsonSkillData의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ConvertJsonToPrefabs()` ➔ `void`: ConvertJsonToPrefabs 관련 로직 수행 함수

#### 📄 클래스: `JsonSkillDatabase` (class)
- **경로**: `Editor/JsonToUnitPrefabConverter.cs`
- **클래스 목적 및 의도**: `JsonSkillDatabase` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] UnitsJsonPath` (string): JsonSkillDatabase의 내부 데이터 또는 의존 참조 변수
  - `[private] SkillsJsonPath` (string): JsonSkillDatabase의 내부 데이터 또는 의존 참조 변수
  - `[private] OutputFolder` (string): JsonSkillDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] isSpecialUnit` (bool): JsonSkillDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] isInterestTarget` (bool): JsonSkillDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] baseInterest` (float): JsonSkillDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] baseDanger` (float): JsonSkillDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] heavyHitThreshold` (float): JsonSkillDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] stealth` (float): JsonSkillDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] baseVisibility` (float): JsonSkillDatabase의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ConvertJsonToPrefabs()` ➔ `void`: ConvertJsonToPrefabs 관련 로직 수행 함수

#### 📄 클래스: `MapGeneratorTool` (class)
- **경로**: `Editor/MapGeneratorTool.cs`
- **클래스 목적 및 의도**: `MapGeneratorTool`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `EditorWindow` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] cm` (CreateMap): MapGeneratorTool의 내부 데이터 또는 의존 참조 변수
  - `[private] showGizmoSettings` (bool): MapGeneratorTool의 내부 데이터 또는 의존 참조 변수
  - `[private] sectionHeaderStyle` (GUIStyle): MapGeneratorTool의 내부 데이터 또는 의존 참조 변수
  - `[private] boxStyle` (GUIStyle): MapGeneratorTool의 내부 데이터 또는 의존 참조 변수
  - `[private] errMsg` (string): MapGeneratorTool의 내부 데이터 또는 의존 참조 변수
  - `[private] hasMap` (bool): MapGeneratorTool의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ShowWindow()` ➔ `void`: ShowWindow 관련 로직 수행 함수
  - `[private] OnEnable()` ➔ `void`: OnEnable 관련 로직 수행 함수
  - `[private] OnGUI()` ➔ `void`: OnGUI 관련 로직 수행 함수
  - `[private] InitStyles()` ➔ `void`: InitStyles 관련 로직 수행 함수
  - `[private] DrawDefaultProperties(CreateMap cm)` ➔ `void`: DrawDefaultProperties 관련 로직 수행 함수
  - `[private] DrawGenerateButton(CreateMap cm)` ➔ `void`: DrawGenerateButton 관련 로직 수행 함수
  - `[private] DrawValidationResult(CreateMap cm)` ➔ `void`: DrawValidationResult 관련 로직 수행 함수
  - `[private] DrawSaveLoadButtons(CreateMap cm)` ➔ `void`: DrawSaveLoadButtons 관련 로직 수행 함수
  - `[private] DrawGizmoSettings(CreateMap cm)` ➔ `void`: DrawGizmoSettings 관련 로직 수행 함수

#### 📄 클래스: `MapViewTool` (class)
- **경로**: `Editor/MapViewTool.cs`
- **클래스 목적 및 의도**: `MapViewTool`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `EditorWindow` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] cm` (CreateMap): MapViewTool의 내부 데이터 또는 의존 참조 변수
  - `[private] selectedFloor` (int): MapViewTool의 내부 데이터 또는 의존 참조 변수
  - `[private] selectedChunkX` (int): MapViewTool의 내부 데이터 또는 의존 참조 변수
  - `[private] selectedChunkY` (int): MapViewTool의 내부 데이터 또는 의존 참조 변수
  - `[private] selectedTileX` (int): MapViewTool의 내부 데이터 또는 의존 참조 변수
  - `[private] selectedTileY` (int): MapViewTool의 내부 데이터 또는 의존 참조 변수
  - `[private] chunkScrollPos` (Vector2): MapViewTool의 내부 데이터 또는 의존 참조 변수
  - `[private] tileScrollPos` (Vector2): MapViewTool의 내부 데이터 또는 의존 참조 변수
  - `[private] showChunkGrid` (bool): MapViewTool의 내부 데이터 또는 의존 참조 변수
  - `[private] showTileGrid` (bool): MapViewTool의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ShowWindow()` ➔ `void`: ShowWindow 관련 로직 수행 함수
  - `[private] OnEnable()` ➔ `void`: OnEnable 관련 로직 수행 함수
  - `[private] OnDisable()` ➔ `void`: OnDisable 관련 로직 수행 함수
  - `[private] OnPlayModeStateChanged(PlayModeStateChange state)` ➔ `void`: OnPlayModeStateChanged 관련 로직 수행 함수
  - `[private] OnMapUpdated(CreateMap runtimeMap)` ➔ `void`: OnMapUpdated 관련 로직 수행 함수
  - `[private] OnGUI()` ➔ `void`: OnGUI 관련 로직 수행 함수
  - `[private] InitStyles()` ➔ `void`: InitStyles 관련 로직 수행 함수
  - `[private] DrawFloorSelector(CreateMap cm)` ➔ `void`: DrawFloorSelector 관련 로직 수행 함수
  - `[private] DrawChunkGrid(CreateMap cm, ref Floor floor)` ➔ `void`: DrawChunkGrid 관련 로직 수행 함수

#### 📄 클래스: `ProjectileSetupTool` (class)
- **경로**: `Editor/ProjectileSetupTool.cs`
- **클래스 목적 및 의도**: `ProjectileSetupTool`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `EditorWindow` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] selectedPrefab` (GameObject): ProjectileSetupTool의 내부 데이터 또는 의존 참조 변수
  - `[private] assetPath` (string): ProjectileSetupTool의 내부 데이터 또는 의존 참조 변수
  - `[private] instance` (GameObject): ProjectileSetupTool의 내부 데이터 또는 의존 참조 변수
  - `[private] modified` (bool): ProjectileSetupTool의 내부 데이터 또는 의존 참조 변수
  - `[private] rb` (var): ProjectileSetupTool의 내부 데이터 또는 의존 참조 변수
  - `[private] colliders` (var): ProjectileSetupTool의 내부 데이터 또는 의존 참조 변수
  - `[private] proj` (var): ProjectileSetupTool의 내부 데이터 또는 의존 참조 변수
  - `[private] sr` (var): ProjectileSetupTool의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ShowWindow()` ➔ `void`: ShowWindow 관련 로직 수행 함수
  - `[private] OnGUI()` ➔ `void`: OnGUI 관련 로직 수행 함수
  - `[private] SetupProjectilePrefab(GameObject prefab)` ➔ `void`: SetupProjectilePrefab 관련 로직 수행 함수

#### 📄 클래스: `TestMapGen` (class)
- **경로**: `Editor/TestMapGen.cs`
- **클래스 목적 및 의도**: `TestMapGen` 로직의 동작 검증을 위한 플레이/단위 테스트 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] cm` (CreateMap): TestMapGen의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Run()` ➔ `void`: Run 관련 로직 수행 함수

#### 📄 클래스: `TitleSceneSetup` (class)
- **경로**: `Editor/TitleSceneSetup.cs`
- **클래스 목적 및 의도**: `TitleSceneSetup`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] PrefabFolder` (string): TitleSceneSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] TitlePanelPrefabPath` (string): TitleSceneSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] TitlePanelAddress` (string): TitleSceneSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] TitleScenePath` (string): TitleSceneSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] SshScenePath` (string): TitleSceneSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] KoreanFontPath` (string): TitleSceneSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] BackgroundColor` (Color): TitleSceneSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] TitleColor` (Color): TitleSceneSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] ButtonLabelColor` (Color): TitleSceneSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] panelPrefab` (GameObject): TitleSceneSetup의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] SetupTitleScene()` ➔ `void`: SetupTitleScene 관련 로직 수행 함수
  - `[private] CreateTitlePanelPrefab()` ➔ `GameObject`: CreateTitlePanelPrefab 관련 로직 수행 함수

#### 📄 클래스: `UnitVisualEditor` (class)
- **경로**: `Editor/UnitVisualEditor.cs`
- **클래스 목적 및 의도**: `UnitVisualEditor`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `Editor` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] visual` (var): UnitVisualEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] tex` (var): UnitVisualEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] rect` (Rect): UnitVisualEditor의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] OnInspectorGUI()` ➔ `void`: OnInspectorGUI 관련 로직 수행 함수

#### 📄 클래스: `WeaponSocketSetup` (class)
- **경로**: `Editor/WeaponSocketSetup.cs`
- **클래스 목적 및 의도**: `WeaponSocketSetup`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `EditorWindow` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] unitPrefab` (GameObject): WeaponSocketSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] weaponSprite` (Sprite): WeaponSocketSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] socketName` (string): WeaponSocketSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] path` (string): WeaponSocketSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] root` (GameObject): WeaponSocketSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] visual` (Transform): WeaponSocketSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] existing` (Transform): WeaponSocketSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] socketGo` (GameObject): WeaponSocketSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] sr` (SpriteRenderer): WeaponSocketSetup의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ShowWindow()` ➔ `void`: ShowWindow 관련 로직 수행 함수
  - `[private] OnGUI()` ➔ `void`: OnGUI 관련 로직 수행 함수
  - `[private] AddWeaponSocket()` ➔ `void`: AddWeaponSocket 관련 로직 수행 함수

#### 📄 클래스: `AddressablesPlayModeSetup` (class)
- **경로**: `Script/Editor/AddressablesPlayModeSetup.cs`
- **클래스 목적 및 의도**: `AddressablesPlayModeSetup`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] settings` (var): AddressablesPlayModeSetup의 내부 데이터 또는 의존 참조 변수
  - `[private] targetIndex` (int): AddressablesPlayModeSetup의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] AddressablesPlayModeSetup()` ➔ `static`: AddressablesPlayModeSetup 관련 로직 수행 함수
  - `[public] SetPlayModeToAssetDatabase()` ➔ `void`: SetPlayModeToAssetDatabase 관련 로직 수행 함수

#### 📄 클래스: `OffenseDebugWindow` (class)
- **경로**: `Script/Editor/OffenseDebugWindow.cs`
- **클래스 목적 및 의도**: `OffenseDebugWindow`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `EditorWindow` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _gameSession` (GameSession): OffenseDebugWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] root` (var): OffenseDebugWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] _gameSession` (return): OffenseDebugWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] _customWaveCooldown` (float): OffenseDebugWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] _addResourceAmount` (int): OffenseDebugWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] _dummyRoom` (Room): OffenseDebugWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] randomIndex` (int): OffenseDebugWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] null` (return): OffenseDebugWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] targetRoom` (Room): OffenseDebugWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] spawnPos` (Vector2Int): OffenseDebugWindow의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ShowWindow()` ➔ `void`: ShowWindow 관련 로직 수행 함수
  - `[private] OnGUI()` ➔ `void`: OnGUI 관련 로직 수행 함수
  - `[private] GetRandomRealRoom()` ➔ `Room`: GetRandomRealRoom 관련 로직 수행 함수

#### 📄 클래스: `SetupStatusInfoPanel` (class)
- **경로**: `Script/Editor/SetupStatusInfoPanel.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `SetupStatusInfoPanel` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] folderPath` (string): SetupStatusInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] prefabPath` (string): SetupStatusInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] go` (GameObject): SetupStatusInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] prefab` (GameObject): SetupStatusInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] settings` (AddressableAssetSettings): SetupStatusInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] group` (AddressableAssetGroup): SetupStatusInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] guid` (string): SetupStatusInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] entry` (AddressableAssetEntry): SetupStatusInfoPanel의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Setup()` ➔ `void`: Setup 관련 로직 수행 함수

---

### 📦 Package: `Encyclopedia`

**모듈 내 포함 스크립트 수**: 5개

#### 📄 클래스: `EncyclopediaEntryData` (class)
- **경로**: `Script/Encyclopedia/EncyclopediaEntryData.cs`
- **클래스 목적 및 의도**: `EncyclopediaEntryData` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: `ScriptableObject, IEncyclopediaEntry` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] Id` (string): EncyclopediaEntryData의 내부 데이터 또는 의존 참조 변수
  - `[public] Category` (string): EncyclopediaEntryData의 내부 데이터 또는 의존 참조 변수
  - `[public] Name` (string): EncyclopediaEntryData의 내부 데이터 또는 의존 참조 변수
  - `[public] Description` (string): EncyclopediaEntryData의 내부 데이터 또는 의존 참조 변수
  - `[public] Icon` (Sprite): EncyclopediaEntryData의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `EncyclopediaManager` (class)
- **경로**: `Script/Encyclopedia/EncyclopediaManager.cs`
- **클래스 목적 및 의도**: 게임 내 `Encyclopedia` 관련 전역 상태 및 루프 시스템을 총괄 관리하는 매니저 클래스입니다.
- **상속 및 구현 관계**: `MonoBehaviour, IEncyclopediaSystem` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _unlockedIds` (HashSet<string>): EncyclopediaManager의 내부 데이터 또는 의존 참조 변수
  - `[private] true` (return): EncyclopediaManager의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): EncyclopediaManager의 내부 데이터 또는 의존 참조 변수
  - `[private] entry` (return): EncyclopediaManager의 내부 데이터 또는 의존 참조 변수
  - `[private] null` (return): EncyclopediaManager의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Awake()` ➔ `void`: Awake 관련 로직 수행 함수
  - `[private] InitializeDatabase()` ➔ `void`: InitializeDatabase 관련 로직 수행 함수
  - `[public] UnlockEntry(string id)` ➔ `bool`: UnlockEntry 관련 로직 수행 함수
  - `[public] GetEntry(string id)` ➔ `IEncyclopediaEntry`: GetEntry 관련 로직 수행 함수
  - `[public] GetAllEntries()` ➔ `List<IEncyclopediaEntry>`: GetAllEntries 관련 로직 수행 함수
  - `[public] GetUnlockedEntries()` ➔ `List<IEncyclopediaEntry>`: GetUnlockedEntries 관련 로직 수행 함수
  - `[public] GetEntriesByCategory(string category)` ➔ `List<IEncyclopediaEntry>`: GetEntriesByCategory 관련 로직 수행 함수
  - `[public] IsUnlocked(string id)` ➔ `bool`: IsUnlocked 관련 로직 수행 함수
  - `[private] SaveData()` ➔ `void`: SaveData 관련 로직 수행 함수
  - `[private] LoadData()` ➔ `void`: LoadData 관련 로직 수행 함수

#### 📄 클래스: `EncyclopediaTester` (class)
- **경로**: `Script/Encyclopedia/EncyclopediaTester.cs`
- **클래스 목적 및 의도**: `EncyclopediaTester` 로직의 동작 검증을 위한 플레이/단위 테스트 스크립트입니다.
- **상속 및 구현 관계**: `MonoBehaviour` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] entryIdsToUnlock` (string[]): EncyclopediaTester의 내부 데이터 또는 의존 참조 변수
  - `[private] success` (bool): EncyclopediaTester의 내부 데이터 또는 의존 참조 변수
  - `[private] success` (bool): EncyclopediaTester의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Update()` ➔ `void`: Update 관련 로직 수행 함수

#### 📄 클래스: `IEncyclopediaEntry` (interface)
- **경로**: `Script/Encyclopedia/IEncyclopediaEntry.cs`
- **클래스 목적 및 의도**: `IEncyclopediaEntry`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `IEncyclopediaSystem` (interface)
- **경로**: `Script/Encyclopedia/IEncyclopediaSystem.cs`
- **클래스 목적 및 의도**: `IEncyclopedia` 도메인의 핵심 비즈니스 로직 연산을 수행하는 핵심 시스템입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] UnlockEntry(string id)` ➔ `bool`: UnlockEntry 관련 로직 수행 함수
  - `[private] GetEntry(string id)` ➔ `IEncyclopediaEntry`: GetEntry 관련 로직 수행 함수
  - `[private] GetAllEntries()` ➔ `List<IEncyclopediaEntry>`: GetAllEntries 관련 로직 수행 함수
  - `[private] GetUnlockedEntries()` ➔ `List<IEncyclopediaEntry>`: GetUnlockedEntries 관련 로직 수행 함수
  - `[private] GetEntriesByCategory(string category)` ➔ `List<IEncyclopediaEntry>`: GetEntriesByCategory 관련 로직 수행 함수
  - `[private] IsUnlocked(string id)` ➔ `bool`: IsUnlocked 관련 로직 수행 함수

---

### 📦 Package: `Encyclopedia/UI`

**모듈 내 포함 스크립트 수**: 3개

#### 📄 클래스: `UI_Encyclopedia` (class)
- **경로**: `Script/Encyclopedia/UI/UI_Encyclopedia.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `UI_Encyclopedia` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `MonoRoutine, ICustomPanel` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _encyclopediaSystem` (IEncyclopediaSystem): UI_Encyclopedia의 내부 데이터 또는 의존 참조 변수
  - `[private] _spawnedSlots` (List<UI_EncyclopediaSlot>): UI_Encyclopedia의 내부 데이터 또는 의존 참조 변수
  - `[private] keyPath` (string): UI_Encyclopedia의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[protected] Constructor()` ➔ `void`: Constructor 관련 로직 수행 함수
  - `[private] TogglePanel()` ➔ `void`: TogglePanel 관련 로직 수행 함수
  - `[private] OpenPanel()` ➔ `else`: OpenPanel 관련 로직 수행 함수
  - `[private] OnEnable()` ➔ `void`: OnEnable 관련 로직 수행 함수
  - `[public] BindEvent()` ➔ `void`: BindEvent 관련 로직 수행 함수
  - `[public] OpenPanel()` ➔ `void`: OpenPanel 관련 로직 수행 함수
  - `[public] ClosePanel()` ➔ `void`: ClosePanel 관련 로직 수행 함수
  - `[private] HandleEntryUnlocked(string id)` ➔ `void`: HandleEntryUnlocked 관련 로직 수행 함수
  - `[public] RefreshUI()` ➔ `void`: RefreshUI 관련 로직 수행 함수
  - `[public] OnSlotClicked(IEncyclopediaEntry entry)` ➔ `void`: OnSlotClicked 관련 로직 수행 함수

#### 📄 클래스: `UI_EncyclopediaDetail` (class)
- **경로**: `Script/Encyclopedia/UI/UI_EncyclopediaDetail.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `UI_EncyclopediaDetail` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `MonoBehaviour` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ShowDetails(IEncyclopediaEntry entry)` ➔ `void`: ShowDetails 관련 로직 수행 함수
  - `[public] Clear()` ➔ `void`: Clear 관련 로직 수행 함수

#### 📄 클래스: `UI_EncyclopediaSlot` (class)
- **경로**: `Script/Encyclopedia/UI/UI_EncyclopediaSlot.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `UI_EncyclopediaSlot` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `MonoBehaviour` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _currentEntry` (IEncyclopediaEntry): UI_EncyclopediaSlot의 내부 데이터 또는 의존 참조 변수
  - `[private] _parentUI` (UI_Encyclopedia): UI_EncyclopediaSlot의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Awake()` ➔ `void`: Awake 관련 로직 수행 함수
  - `[public] SetData(IEncyclopediaEntry entry, UI_Encyclopedia parentUI)` ➔ `void`: SetData 관련 로직 수행 함수
  - `[private] OnClick()` ➔ `void`: OnClick 관련 로직 수행 함수

---

### 📦 Package: `Haare/Demo/Script`

**모듈 내 포함 스크립트 수**: 1개

#### 📄 클래스: `DemoNative` (class)
- **경로**: `Haare/Demo/Script/DemoNative.cs`
- **클래스 목적 및 의도**: `DemoNative`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `NativeRoutine` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[public] UpdateProcess()` ➔ `void`: UpdateProcess 관련 로직 수행 함수
  - `[public] Finalize()` ➔ `UniTask`: Finalize 관련 로직 수행 함수

---

### 📦 Package: `Haare/Demo/Script/LoadScene`

**모듈 내 포함 스크립트 수**: 4개

#### 📄 클래스: `DemoLoadMono` (class)
- **경로**: `Haare/Demo/Script/LoadScene/DemoLoadMono.cs`
- **클래스 목적 및 의도**: `DemoLoadMono`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoRoutine` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수

#### 📄 클래스: `DemoLoadScope` (class)
- **경로**: `Haare/Demo/Script/LoadScene/DemoLoadScope.cs`
- **클래스 목적 및 의도**: `DemoLoadScope`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `LifetimeScope` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[protected] Configure(IContainerBuilder builder)` ➔ `void`: Configure 관련 로직 수행 함수

#### 📄 클래스: `DemoLoadUIManager` (class)
- **경로**: `Haare/Demo/Script/LoadScene/DemoLoadUIManager.cs`
- **클래스 목적 및 의도**: 게임 내 `DemoLoadUI` 관련 전역 상태 및 루프 시스템을 총괄 관리하는 매니저 클래스입니다.
- **상속 및 구현 관계**: `SceneUIManager` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] loadingPanelID` (int): DemoLoadUIManager의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수

#### 📄 클래스: `DemoLoadUIPresenter` (class)
- **경로**: `Haare/Demo/Script/LoadScene/DemoLoadUIPresenter.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `DemoLoadUIPresenter` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `IPresenter` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _coreUIManager` (CoreUIManager): DemoLoadUIPresenter의 내부 데이터 또는 의존 참조 변수
  - `[private] _sceneUiManager` (SceneUIManager): DemoLoadUIPresenter의 내부 데이터 또는 의존 참조 변수
  - `[private] _resolver` (IObjectResolver): DemoLoadUIPresenter의 내부 데이터 또는 의존 참조 변수
  - `[public] sceneService` (SceneService): DemoLoadUIPresenter의 내부 데이터 또는 의존 참조 변수
  - `[private] _loadMono` (DemoLoadMono): DemoLoadUIPresenter의 내부 데이터 또는 의존 참조 변수
  - `[private] panel` (var): DemoLoadUIPresenter의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Dispose()` ➔ `void`: Dispose 관련 로직 수행 함수
  - `[public] PostInitialize()` ➔ `void`: PostInitialize 관련 로직 수행 함수
  - `[private] BindIPanel(ICustomPanel panel)` ➔ `void`: BindIPanel 관련 로직 수행 함수
  - `[private] LoadStartSequence()` ➔ `UniTask`: LoadStartSequence 관련 로직 수행 함수

---

### 📦 Package: `Haare/Demo/Script/LobbyScene`

**모듈 내 포함 스크립트 수**: 4개

#### 📄 클래스: `DemoCubeRotator` (class)
- **경로**: `Haare/Demo/Script/LobbyScene/DemoCubeRotator.cs`
- **클래스 목적 및 의도**: `DemoCubeRotator`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoRoutine` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] rotationSpeed` (float): DemoCubeRotator의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[protected] UpdateProcess()` ➔ `void`: UpdateProcess 관련 로직 수행 함수

#### 📄 클래스: `DemoLobbyScope` (class)
- **경로**: `Haare/Demo/Script/LobbyScene/DemoLobbyScope.cs`
- **클래스 목적 및 의도**: `DemoLobbyScope`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `LifetimeScope` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[protected] Configure(IContainerBuilder builder)` ➔ `void`: Configure 관련 로직 수행 함수

#### 📄 클래스: `DemoLobbyUIManager` (class)
- **경로**: `Haare/Demo/Script/LobbyScene/DemoLobbyUIManager.cs`
- **클래스 목적 및 의도**: 게임 내 `DemoLobbyUI` 관련 전역 상태 및 루프 시스템을 총괄 관리하는 매니저 클래스입니다.
- **상속 및 구현 관계**: `SceneUIManager` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[private] BindIPanel(ICustomPanel panel)` ➔ `void`: BindIPanel 관련 로직 수행 함수

#### 📄 클래스: `DemoLobbyUIPresenter` (class)
- **경로**: `Haare/Demo/Script/LobbyScene/DemoLobbyUIPresenter.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `DemoLobbyUIPresenter` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `IPresenter` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _coreUIManager` (CoreUIManager): DemoLobbyUIPresenter의 내부 데이터 또는 의존 참조 변수
  - `[private] _sceneUiManager` (SceneUIManager): DemoLobbyUIPresenter의 내부 데이터 또는 의존 참조 변수
  - `[private] _resolver` (IObjectResolver): DemoLobbyUIPresenter의 내부 데이터 또는 의존 참조 변수
  - `[public] sceneService` (SceneService): DemoLobbyUIPresenter의 내부 데이터 또는 의존 참조 변수
  - `[private] fadepanelID` (var): DemoLobbyUIPresenter의 내부 데이터 또는 의존 참조 변수
  - `[private] panel` (var): DemoLobbyUIPresenter의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Dispose()` ➔ `void`: Dispose 관련 로직 수행 함수
  - `[public] PostInitialize()` ➔ `void`: PostInitialize 관련 로직 수행 함수
  - `[private] BindIPanel(ICustomPanel panel)` ➔ `void`: BindIPanel 관련 로직 수행 함수
  - `[private] StartSequence()` ➔ `UniTask`: StartSequence 관련 로직 수행 함수

---

### 📦 Package: `Haare/Demo/Script/TitleScene`

**모듈 내 포함 스크립트 수**: 4개

#### 📄 클래스: `DemoTitleMono` (class)
- **경로**: `Haare/Demo/Script/TitleScene/DemoTitleMono.cs`
- **클래스 목적 및 의도**: `DemoTitleMono`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoRoutine` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수

#### 📄 클래스: `DemoTitleScope` (class)
- **경로**: `Haare/Demo/Script/TitleScene/DemoTitleScope.cs`
- **클래스 목적 및 의도**: `DemoTitleScope`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `LifetimeScope` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[protected] Configure(IContainerBuilder builder)` ➔ `void`: Configure 관련 로직 수행 함수

#### 📄 클래스: `DemoTitleUIManager` (class)
- **경로**: `Haare/Demo/Script/TitleScene/DemoTitleUIManager.cs`
- **클래스 목적 및 의도**: 게임 내 `DemoTitleUI` 관련 전역 상태 및 루프 시스템을 총괄 관리하는 매니저 클래스입니다.
- **상속 및 구현 관계**: `SceneUIManager` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] debugPanelID` (int): DemoTitleUIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] titlePanelID` (int): DemoTitleUIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] _titlepanel` (var): DemoTitleUIManager의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[private] Reset()` ➔ `void`: Reset 관련 로직 수행 함수
  - `[private] BindIPanel(ICustomPanel panel)` ➔ `void`: BindIPanel 관련 로직 수행 함수

#### 📄 클래스: `DemoTitleUIPresenter` (class)
- **경로**: `Haare/Demo/Script/TitleScene/DemoTitleUIPresenter.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `DemoTitleUIPresenter` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `IPresenter` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _coreUIManager` (CoreUIManager): DemoTitleUIPresenter의 내부 데이터 또는 의존 참조 변수
  - `[private] _sceneUiManager` (SceneUIManager): DemoTitleUIPresenter의 내부 데이터 또는 의존 참조 변수
  - `[private] _resolver` (IObjectResolver): DemoTitleUIPresenter의 내부 데이터 또는 의존 참조 변수
  - `[public] sceneService` (SceneService): DemoTitleUIPresenter의 내부 데이터 또는 의존 참조 변수
  - `[private] loadingPanelID` (var): DemoTitleUIPresenter의 내부 데이터 또는 의존 참조 변수
  - `[private] loadingPanel` (var): DemoTitleUIPresenter의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Dispose()` ➔ `void`: Dispose 관련 로직 수행 함수
  - `[public] PostInitialize()` ➔ `void`: PostInitialize 관련 로직 수행 함수
  - `[private] StartGameSequence()` ➔ `UniTask`: StartGameSequence 관련 로직 수행 함수
  - `[private] OnFinishedFadePanel()` ➔ `void`: OnFinishedFadePanel 관련 로직 수행 함수
  - `[private] BindIPanel(ICustomPanel panel)` ➔ `void`: BindIPanel 관련 로직 수행 함수

---

### 📦 Package: `Haare/Demo/Script/UI`

**모듈 내 포함 스크립트 수**: 4개

#### 📄 클래스: `DebugPanel` (class)
- **경로**: `Haare/Demo/Script/UI/DebugPanel.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `DebugPanel` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `MonoRoutine, ICustomPanel` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _customPanelImplementation` (ICustomPanel): DebugPanel의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] BindEvent()` ➔ `void`: BindEvent 관련 로직 수행 함수
  - `[public] BindEvent(IDataInstance data)` ➔ `void`: BindEvent 관련 로직 수행 함수
  - `[public] SetData(IDataInstance data)` ➔ `void`: SetData 관련 로직 수행 함수
  - `[public] OpenPanel()` ➔ `void`: OpenPanel 관련 로직 수행 함수
  - `[public] ClosePanel()` ➔ `void`: ClosePanel 관련 로직 수행 함수

#### 📄 클래스: `LoadingFadePanel` (class)
- **경로**: `Haare/Demo/Script/UI/LoadingFadePanel.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `LoadingFadePanel` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `MonoRoutine, ICustomPanel` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _customPanelImplementation` (ICustomPanel): LoadingFadePanel의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[public] FadeIn(float duration = 0.4f)` ➔ `UniTask`: FadeIn 관련 로직 수행 함수
  - `[public] FadeOut(float duration = 0.4f)` ➔ `UniTask`: FadeOut 관련 로직 수행 함수
  - `[public] BindEvent()` ➔ `void`: BindEvent 관련 로직 수행 함수
  - `[public] BindEvent(IDataInstance data)` ➔ `void`: BindEvent 관련 로직 수행 함수
  - `[public] SetData(IDataInstance data)` ➔ `void`: SetData 관련 로직 수행 함수
  - `[public] OpenPanel()` ➔ `void`: OpenPanel 관련 로직 수행 함수
  - `[public] ClosePanel()` ➔ `void`: ClosePanel 관련 로직 수행 함수

#### 📄 클래스: `LoadingPanel` (class)
- **경로**: `Haare/Demo/Script/UI/LoadingPanel.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `LoadingPanel` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `MonoRoutine, ICustomPanel` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[public] BindEvent()` ➔ `void`: BindEvent 관련 로직 수행 함수
  - `[public] SetData(IDataInstance data)` ➔ `void`: SetData 관련 로직 수행 함수
  - `[public] OpenPanel()` ➔ `void`: OpenPanel 관련 로직 수행 함수
  - `[public] ClosePanel()` ➔ `void`: ClosePanel 관련 로직 수행 함수

#### 📄 클래스: `TitlePanel` (class)
- **경로**: `Haare/Demo/Script/UI/TitlePanel.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `TitlePanel` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `MonoRoutine, ICustomPanel` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _customPanelImplementation` (ICustomPanel): TitlePanel의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] BindEvent()` ➔ `void`: BindEvent 관련 로직 수행 함수
  - `[public] BindEvent(IDataInstance data)` ➔ `void`: BindEvent 관련 로직 수행 함수
  - `[public] SetData(IDataInstance data)` ➔ `void`: SetData 관련 로직 수행 함수
  - `[public] OpenPanel()` ➔ `void`: OpenPanel 관련 로직 수행 함수
  - `[public] ClosePanel()` ➔ `void`: ClosePanel 관련 로직 수행 함수

---

### 📦 Package: `Haare/Editor`

**모듈 내 포함 스크립트 수**: 1개

#### 📄 클래스: `FrameworkMenuItems` (class)
- **경로**: `Haare/Editor/HaareUI.cs`
- **클래스 목적 및 의도**: `FrameworkMenuItems`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] go` (var): FrameworkMenuItems의 내부 데이터 또는 의존 참조 변수
  - `[private] go` (var): FrameworkMenuItems의 내부 데이터 또는 의존 참조 변수
  - `[private] go` (var): FrameworkMenuItems의 내부 데이터 또는 의존 참조 변수
  - `[private] go` (var): FrameworkMenuItems의 내부 데이터 또는 의존 참조 변수
  - `[private] slider` (var): FrameworkMenuItems의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] CreateCustomImage(MenuCommand menuCommand)` ➔ `void`: CreateCustomImage 관련 로직 수행 함수
  - `[private] CreateCustomText(MenuCommand menuCommand)` ➔ `void`: CreateCustomText 관련 로직 수행 함수
  - `[private] CreateCustomButton(MenuCommand menuCommand)` ➔ `void`: CreateCustomButton 관련 로직 수행 함수
  - `[private] CreateCustomSlider(MenuCommand menuCommand)` ➔ `void`: CreateCustomSlider 관련 로직 수행 함수
  - `[private] ValidateUIElementCreation()` ➔ `bool`: ValidateUIElementCreation 관련 로직 수행 함수
  - `[private] SetupAndRegister(GameObject go, MenuCommand menuCommand)` ➔ `void`: SetupAndRegister 관련 로직 수행 함수

---

### 📦 Package: `Haare/Editor/UI`

**모듈 내 포함 스크립트 수**: 2개

#### 📄 클래스: `CustomButtonEditor` (class)
- **경로**: `Haare/Editor/UI/AnimationUIEditor.cs`
- **클래스 목적 및 의도**: `CustomButtonEditor`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `UnityEditor.Editor` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] propertiesToExclude` (string[]): CustomButtonEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] hoverImageProp` (SerializedProperty): CustomButtonEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] hoverColorProp` (SerializedProperty): CustomButtonEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] animationProp` (SerializedProperty): CustomButtonEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] clickAnimationFlagProp` (SerializedProperty): CustomButtonEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] clickDurationProp` (SerializedProperty): CustomButtonEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] clickPunchScaleProp` (SerializedProperty): CustomButtonEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] hoverAnimationFlagProp` (SerializedProperty): CustomButtonEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] hoverScaleProp` (SerializedProperty): CustomButtonEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] hoverDurationProp` (SerializedProperty): CustomButtonEditor의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] OnInspectorGUI()` ➔ `void`: OnInspectorGUI 관련 로직 수행 함수
  - `[public] OnInspectorGUI()` ➔ `void`: OnInspectorGUI 관련 로직 수행 함수

#### 📄 클래스: `CustomImageEditor` (class)
- **경로**: `Haare/Editor/UI/AnimationUIEditor.cs`
- **클래스 목적 및 의도**: `CustomImageEditor`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `UnityEditor.Editor` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] propertiesToExclude` (string[]): CustomImageEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] hoverImageProp` (SerializedProperty): CustomImageEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] hoverColorProp` (SerializedProperty): CustomImageEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] animationProp` (SerializedProperty): CustomImageEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] clickAnimationFlagProp` (SerializedProperty): CustomImageEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] clickDurationProp` (SerializedProperty): CustomImageEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] clickPunchScaleProp` (SerializedProperty): CustomImageEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] hoverAnimationFlagProp` (SerializedProperty): CustomImageEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] hoverScaleProp` (SerializedProperty): CustomImageEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] hoverDurationProp` (SerializedProperty): CustomImageEditor의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] OnInspectorGUI()` ➔ `void`: OnInspectorGUI 관련 로직 수행 함수
  - `[public] OnInspectorGUI()` ➔ `void`: OnInspectorGUI 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Client/Core`

**모듈 내 포함 스크립트 수**: 2개

#### 📄 클래스: `HaareClient` (class)
- **경로**: `Haare/Scripts/Client/Core/HaareClient.cs`
- **클래스 목적 및 의도**: `HaareClient`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] eventSystemObj` (var): HaareClient의 내부 데이터 또는 의존 참조 변수
  - `[private] audioObj` (var): HaareClient의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Main()` ➔ `void`: Main 관련 로직 수행 함수
  - `[private] InitializePlugin()` ➔ `UniTask`: InitializePlugin 관련 로직 수행 함수
  - `[private] RegisterProcesses()` ➔ `UniTask`: RegisterProcesses 관련 로직 수행 함수

#### 📄 클래스: `Processor` (class)
- **경로**: `Haare/Scripts/Client/Core/Processor.cs`
- **클래스 목적 및 의도**: `Processor`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `SingletonMonoBehaviour<Processor>` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] PROCESSING` (ReadOnlyReactiveProperty<bool>): Processor의 내부 데이터 또는 의존 참조 변수
  - `[private] processing` (ReactiveProperty<bool>): Processor의 내부 데이터 또는 의존 참조 변수
  - `[private] Routines` (List<IRoutine>): Processor의 내부 데이터 또는 의존 참조 변수
  - `[private] deleteRoutines` (List<IRoutine>): Processor의 내부 데이터 또는 의존 참조 변수
  - `[private] DeleteProcessing` (bool): Processor의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] OnValidate()` ➔ `void`: OnValidate 관련 로직 수행 함수
  - `[public] Constructor( 
            Func<UniTask> initializePlugin, 
            Func<UniTask> registerProcesses 
            )` ➔ `UniTask`: Constructor 관련 로직 수행 함수
  - `[private] initializePlugin()` ➔ `await`: initializePlugin 관련 로직 수행 함수
  - `[private] RegisterEvents( registerProcesses )` ➔ `await`: RegisterEvents 관련 로직 수행 함수
  - `[private] Initialize(_cts.Token)` ➔ `await`: Initialize 관련 로직 수행 함수
  - `[private] RegisterEvents( Func<UniTask> registerProcesses )` ➔ `UniTask`: RegisterEvents 관련 로직 수행 함수
  - `[private] registerProcesses()` ➔ `await`: registerProcesses 관련 로직 수행 함수
  - `[private] CheckDeleteProcesses()` ➔ `await`: CheckDeleteProcesses 관련 로직 수행 함수
  - `[public] CheckDeleteProcessesForScene()` ➔ `UniTask`: CheckDeleteProcessesForScene 관련 로직 수행 함수
  - `[public] Register( IRoutine process ,CancellationToken cts)` ➔ `UniTask`: Register 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Client/Core/Singleton`

**모듈 내 포함 스크립트 수**: 2개

#### 📄 클래스: `Singleton` (class)
- **경로**: `Haare/Scripts/Client/Core/Singleton/Singleton.cs`
- **클래스 목적 및 의도**: `Singleton`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] isCreated` (bool): Singleton의 내부 데이터 또는 의존 참조 변수
  - `[private] instance` (T): Singleton의 내부 데이터 또는 의존 참조 변수
  - `[private] instance` (return): Singleton의 내부 데이터 또는 의존 참조 변수
  - `[private] i` (var): Singleton의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `SingletonMonoBehaviour` (class)
- **경로**: `Haare/Scripts/Client/Core/Singleton/SingletonMono.cs`
- **클래스 목적 및 의도**: `SingletonMonoBehaviour`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] isCreated` (bool): SingletonMonoBehaviour의 내부 데이터 또는 의존 참조 변수
  - `[private] QuittingProgram` (bool): SingletonMonoBehaviour의 내부 데이터 또는 의존 참조 변수
  - `[private] instance` (T): SingletonMonoBehaviour의 내부 데이터 또는 의존 참조 변수
  - `[private] null` (return): SingletonMonoBehaviour의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (Type): SingletonMonoBehaviour의 내부 데이터 또는 의존 참조 변수
  - `[private] instance` (return): SingletonMonoBehaviour의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (Type): SingletonMonoBehaviour의 내부 데이터 또는 의존 참조 변수
  - `[private] Obj` (GameObject): SingletonMonoBehaviour의 내부 데이터 또는 의존 참조 변수
  - `[private] i` (var): SingletonMonoBehaviour의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[private] OnDestroy()` ➔ `void`: OnDestroy 관련 로직 수행 함수
  - `[private] OnApplicationQuit()` ➔ `void`: OnApplicationQuit 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Client/DI/Container`

**모듈 내 포함 스크립트 수**: 1개

#### 📄 클래스: `CoreLifetimeScope` (class)
- **경로**: `Haare/Scripts/Client/DI/Container/CoreLifetimeScope.cs`
- **클래스 목적 및 의도**: `CoreLifetimeScope`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `LifetimeScope` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[protected] _coreUIManagerPrefab` (CoreUIManager): CoreLifetimeScope의 내부 데이터 또는 의존 참조 변수
  - `[private] isLocalMode` (bool): CoreLifetimeScope의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[protected] Awake()` ➔ `void`: Awake 관련 로직 수행 함수
  - `[protected] Configure(IContainerBuilder builder)` ➔ `void`: Configure 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Client/DI/Presenter`

**모듈 내 포함 스크립트 수**: 3개

#### 📄 클래스: `GamePresenter` (class)
- **경로**: `Haare/Scripts/Client/DI/Presenter/GamePresenter.cs`
- **클래스 목적 및 의도**: `GamePresenter`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IPostInitializable, IDisposable` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _sceneService` (SceneService): GamePresenter의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Dispose()` ➔ `void`: Dispose 관련 로직 수행 함수
  - `[public] PostInitialize()` ➔ `void`: PostInitialize 관련 로직 수행 함수

#### 📄 클래스: `IPresenter` (interface)
- **경로**: `Haare/Scripts/Client/DI/Presenter/IPresenter.cs`
- **클래스 목적 및 의도**: `IPresenter`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IPostInitializable, IDisposable` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `UIPresenter` (class)
- **경로**: `Haare/Scripts/Client/DI/Presenter/UIPresenter.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `UIPresenter` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `IPresenter` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] fadepanel` (var): UIPresenter의 내부 데이터 또는 의존 참조 변수
  - `[private] fadepanel` (var): UIPresenter의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Dispose()` ➔ `void`: Dispose 관련 로직 수행 함수
  - `[public] PostInitialize()` ➔ `void`: PostInitialize 관련 로직 수행 함수
  - `[protected] PostInitializeAsync()` ➔ `UniTask`: PostInitializeAsync 관련 로직 수행 함수
  - `[protected] BindPanelEvents()` ➔ `void`: BindPanelEvents 관련 로직 수행 함수
  - `[private] FadeIn()` ➔ `await`: FadeIn 관련 로직 수행 함수
  - `[private] FadeOut()` ➔ `await`: FadeOut 관련 로직 수행 함수
  - `[private] FadeIn()` ➔ `await`: FadeIn 관련 로직 수행 함수
  - `[private] FadeOut()` ➔ `await`: FadeOut 관련 로직 수행 함수
  - `[protected] StartSequence()` ➔ `UniTask`: StartSequence 관련 로직 수행 함수
  - `[private] FadeOut()` ➔ `await`: FadeOut 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Client/Data`

**모듈 내 포함 스크립트 수**: 1개

#### 📄 클래스: `DataManager` (class)
- **경로**: `Haare/Scripts/Client/Data/DataManager.cs`
- **클래스 목적 및 의도**: 게임 내 `Data` 관련 전역 상태 및 루프 시스템을 총괄 관리하는 매니저 클래스입니다.
- **상속 및 구현 관계**: `NativeRoutine` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] modelType` (var): DataManager의 내부 데이터 또는 의존 참조 변수
  - `[private] sourceAttribute` (var): DataManager의 내부 데이터 또는 의존 참조 변수
  - `[private] default` (return): DataManager의 내부 데이터 또는 의존 참조 변수
  - `[private] targetDataType` (Type): DataManager의 내부 데이터 또는 의존 참조 변수
  - `[private] address` (string): DataManager의 내부 데이터 또는 의존 참조 변수
  - `[private] loadedAsset` (TextAsset): DataManager의 내부 데이터 또는 의존 참조 변수
  - `[private] loadedLocalJsonAsset` (var): DataManager의 내부 데이터 또는 의존 참조 변수
  - `[private] loadedTemplateJsonAsset` (var): DataManager의 내부 데이터 또는 의존 참조 변수
  - `[private] default` (return): DataManager의 내부 데이터 또는 의존 참조 변수
  - `[private] deserializedObject` (var): DataManager의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

---

### 📦 Package: `Haare/Scripts/Client/Data/Attribute`

**모듈 내 포함 스크립트 수**: 1개

#### 📄 클래스: `DataModelAttribute` (class)
- **경로**: `Haare/Scripts/Client/Data/Attribute/DataModelAttribute.cs`
- **클래스 목적 및 의도**: `DataModelAttribute` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: `Attribute` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] DataModelAttribute(Type templateType, string addressableJsonDataPath, string jsonDataPath)` ➔ `public`: DataModelAttribute 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Client/Data/interface`

**모듈 내 포함 스크립트 수**: 3개

#### 📄 클래스: `IData` (interface)
- **경로**: `Haare/Scripts/Client/Data/interface/IData.cs`
- **클래스 목적 및 의도**: `IData` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `IDataInstance` (interface)
- **경로**: `Haare/Scripts/Client/Data/interface/IDataInstance.cs`
- **클래스 목적 및 의도**: `IDataInstance` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Save()` ➔ `void`: Save 관련 로직 수행 함수

#### 📄 클래스: `IDataModel` (interface)
- **경로**: `Haare/Scripts/Client/Data/interface/IDataModel.cs`
- **클래스 목적 및 의도**: `IDataModel` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

---

### 📦 Package: `Haare/Scripts/Client/Routine`

**모듈 내 포함 스크립트 수**: 2개

#### 📄 클래스: `MonoRoutine` (class)
- **경로**: `Haare/Scripts/Client/Routine/MonoRoutine.cs`
- **클래스 목적 및 의도**: `MonoRoutine`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoBehaviour, IRoutine` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] disposables` (CompositeDisposable): MonoRoutine의 내부 데이터 또는 의존 참조 변수
  - `[private] _isFinalized` (bool): MonoRoutine의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Awake()` ➔ `void`: Awake 관련 로직 수행 함수
  - `[protected] InitializeAsync(CancellationToken cts)` ➔ `UniTask`: InitializeAsync 관련 로직 수행 함수
  - `[protected] Constructor()` ➔ `void`: Constructor 관련 로직 수행 함수
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[private] Oninitialize(cts)` ➔ `await`: Oninitialize 관련 로직 수행 함수
  - `[protected] OnStopProcess()` ➔ `void`: OnStopProcess 관련 로직 수행 함수
  - `[protected] OnRestartProcess()` ➔ `void`: OnRestartProcess 관련 로직 수행 함수
  - `[protected] UpdateProcess()` ➔ `void`: UpdateProcess 관련 로직 수행 함수
  - `[protected] LateUpdateProcess()` ➔ `void`: LateUpdateProcess 관련 로직 수행 함수
  - `[protected] FixedUpdateProcess()` ➔ `void`: FixedUpdateProcess 관련 로직 수행 함수

#### 📄 클래스: `NativeRoutine` (class)
- **경로**: `Haare/Scripts/Client/Routine/NativeRoutine.cs`
- **클래스 목적 및 의도**: `NativeRoutine`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `INativeRoutine, IDisposable` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] NativeRoutine()` ➔ `protected`: NativeRoutine 관련 로직 수행 함수
  - `[private] Constructor(CancellationToken cts)` ➔ `UniTask`: Constructor 관련 로직 수행 함수
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[private] Oninitialize()` ➔ `await`: Oninitialize 관련 로직 수행 함수
  - `[public] UpdateProcess()` ➔ `void`: UpdateProcess 관련 로직 수행 함수
  - `[public] OnApplicationQuit()` ➔ `void`: OnApplicationQuit 관련 로직 수행 함수
  - `[public] OnApplicationPause(bool pauseStatus)` ➔ `void`: OnApplicationPause 관련 로직 수행 함수
  - `[public] Dispose()` ➔ `void`: Dispose 관련 로직 수행 함수
  - `[public] Finalize()` ➔ `UniTask`: Finalize 관련 로직 수행 함수
  - `[private] Onfinalize()` ➔ `await`: Onfinalize 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Client/Routine/Service/SceneService`

**모듈 내 포함 스크립트 수**: 4개

#### 📄 클래스: `SceneLoadRequest` (class)
- **경로**: `Haare/Scripts/Client/Routine/Service/SceneService/SceneLoadRequest.cs`
- **클래스 목적 및 의도**: `SceneLoadRequest`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] SceneLoadRequest(SceneName scene, LoadSceneMode mode, object argument = null)` ➔ `public`: SceneLoadRequest 관련 로직 수행 함수

#### 📄 클래스: `SceneName` (enum)
- **경로**: `Haare/Scripts/Client/Routine/Service/SceneService/SceneService.cs`
- **클래스 목적 및 의도**: `SceneName`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] _coreUIManager` (CoreUIManager): SceneName의 내부 데이터 또는 의존 참조 변수
  - `[private] sceneToUnload` (SceneName): SceneName의 내부 데이터 또는 의존 참조 변수
  - `[private] currentPhaseReactive` (ReactiveProperty<SceneLoadPhase>): SceneName의 내부 데이터 또는 의존 참조 변수
  - `[public] CurrentPhase` (ReactiveProperty<SceneLoadPhase>): SceneName의 내부 데이터 또는 의존 참조 변수
  - `[private] _loadProgress` (ReactiveProperty<float>): SceneName의 내부 데이터 또는 의존 참조 변수
  - `[public] LoadProgress` (ReadOnlyReactiveProperty<float>): SceneName의 내부 데이터 또는 의존 참조 변수
  - `[public] LoadSceneRequest` (SceneLoadRequest): SceneName의 내부 데이터 또는 의존 참조 변수
  - `[private] req` (SceneLoadRequest): SceneName의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[public] LoadSceneWithLoad(SceneName scene, LoadSceneMode mode = LoadSceneMode.Additive)` ➔ `UniTask`: LoadSceneWithLoad 관련 로직 수행 함수
  - `[private] LoadSceneInternal(req,false)` ➔ `await`: LoadSceneInternal 관련 로직 수행 함수
  - `[public] LoadScene()` ➔ `UniTask`: LoadScene 관련 로직 수행 함수
  - `[private] LoadSceneInternal(LoadSceneRequest,true)` ➔ `await`: LoadSceneInternal 관련 로직 수행 함수
  - `[public] LoadScene(SceneName scene, LoadSceneMode mode = LoadSceneMode.Additive)` ➔ `UniTask`: LoadScene 관련 로직 수행 함수
  - `[private] LoadSceneInternal(LoadSceneRequest,false)` ➔ `await`: LoadSceneInternal 관련 로직 수행 함수
  - `[private] LoadSceneInternal(SceneLoadRequest request,bool withLoad)` ➔ `UniTask`: LoadSceneInternal 관련 로직 수행 함수
  - `[private] ExitLoadingSceneTask()` ➔ `await`: ExitLoadingSceneTask 관련 로직 수행 함수
  - `[private] LoadSceneProgressTask(loadOperation)` ➔ `await`: LoadSceneProgressTask 관련 로직 수행 함수

#### 📄 클래스: `SceneLoadPhase` (enum)
- **경로**: `Haare/Scripts/Client/Routine/Service/SceneService/SceneService.cs`
- **클래스 목적 및 의도**: `SceneLoadPhase`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] _coreUIManager` (CoreUIManager): SceneLoadPhase의 내부 데이터 또는 의존 참조 변수
  - `[private] sceneToUnload` (SceneName): SceneLoadPhase의 내부 데이터 또는 의존 참조 변수
  - `[private] currentPhaseReactive` (ReactiveProperty<SceneLoadPhase>): SceneLoadPhase의 내부 데이터 또는 의존 참조 변수
  - `[public] CurrentPhase` (ReactiveProperty<SceneLoadPhase>): SceneLoadPhase의 내부 데이터 또는 의존 참조 변수
  - `[private] _loadProgress` (ReactiveProperty<float>): SceneLoadPhase의 내부 데이터 또는 의존 참조 변수
  - `[public] LoadProgress` (ReadOnlyReactiveProperty<float>): SceneLoadPhase의 내부 데이터 또는 의존 참조 변수
  - `[public] LoadSceneRequest` (SceneLoadRequest): SceneLoadPhase의 내부 데이터 또는 의존 참조 변수
  - `[private] req` (SceneLoadRequest): SceneLoadPhase의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[public] LoadSceneWithLoad(SceneName scene, LoadSceneMode mode = LoadSceneMode.Additive)` ➔ `UniTask`: LoadSceneWithLoad 관련 로직 수행 함수
  - `[private] LoadSceneInternal(req,false)` ➔ `await`: LoadSceneInternal 관련 로직 수행 함수
  - `[public] LoadScene()` ➔ `UniTask`: LoadScene 관련 로직 수행 함수
  - `[private] LoadSceneInternal(LoadSceneRequest,true)` ➔ `await`: LoadSceneInternal 관련 로직 수행 함수
  - `[public] LoadScene(SceneName scene, LoadSceneMode mode = LoadSceneMode.Additive)` ➔ `UniTask`: LoadScene 관련 로직 수행 함수
  - `[private] LoadSceneInternal(LoadSceneRequest,false)` ➔ `await`: LoadSceneInternal 관련 로직 수행 함수
  - `[private] LoadSceneInternal(SceneLoadRequest request,bool withLoad)` ➔ `UniTask`: LoadSceneInternal 관련 로직 수행 함수
  - `[private] ExitLoadingSceneTask()` ➔ `await`: ExitLoadingSceneTask 관련 로직 수행 함수
  - `[private] LoadSceneProgressTask(loadOperation)` ➔ `await`: LoadSceneProgressTask 관련 로직 수행 함수

#### 📄 클래스: `SceneService` (class)
- **경로**: `Haare/Scripts/Client/Routine/Service/SceneService/SceneService.cs`
- **클래스 목적 및 의도**: `SceneService`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `NativeRoutine` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _coreUIManager` (CoreUIManager): SceneService의 내부 데이터 또는 의존 참조 변수
  - `[private] sceneToUnload` (SceneName): SceneService의 내부 데이터 또는 의존 참조 변수
  - `[private] currentPhaseReactive` (ReactiveProperty<SceneLoadPhase>): SceneService의 내부 데이터 또는 의존 참조 변수
  - `[public] CurrentPhase` (ReactiveProperty<SceneLoadPhase>): SceneService의 내부 데이터 또는 의존 참조 변수
  - `[private] _loadProgress` (ReactiveProperty<float>): SceneService의 내부 데이터 또는 의존 참조 변수
  - `[public] LoadProgress` (ReadOnlyReactiveProperty<float>): SceneService의 내부 데이터 또는 의존 참조 변수
  - `[public] LoadSceneRequest` (SceneLoadRequest): SceneService의 내부 데이터 또는 의존 참조 변수
  - `[private] req` (SceneLoadRequest): SceneService의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[public] LoadSceneWithLoad(SceneName scene, LoadSceneMode mode = LoadSceneMode.Additive)` ➔ `UniTask`: LoadSceneWithLoad 관련 로직 수행 함수
  - `[private] LoadSceneInternal(req,false)` ➔ `await`: LoadSceneInternal 관련 로직 수행 함수
  - `[public] LoadScene()` ➔ `UniTask`: LoadScene 관련 로직 수행 함수
  - `[private] LoadSceneInternal(LoadSceneRequest,true)` ➔ `await`: LoadSceneInternal 관련 로직 수행 함수
  - `[public] LoadScene(SceneName scene, LoadSceneMode mode = LoadSceneMode.Additive)` ➔ `UniTask`: LoadScene 관련 로직 수행 함수
  - `[private] LoadSceneInternal(LoadSceneRequest,false)` ➔ `await`: LoadSceneInternal 관련 로직 수행 함수
  - `[private] LoadSceneInternal(SceneLoadRequest request,bool withLoad)` ➔ `UniTask`: LoadSceneInternal 관련 로직 수행 함수
  - `[private] ExitLoadingSceneTask()` ➔ `await`: ExitLoadingSceneTask 관련 로직 수행 함수
  - `[private] LoadSceneProgressTask(loadOperation)` ➔ `await`: LoadSceneProgressTask 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Client/Routine/Service/SceneService/interface`

**모듈 내 포함 스크립트 수**: 1개

#### 📄 클래스: `ISceneWasLoaded` (interface)
- **경로**: `Haare/Scripts/Client/Routine/Service/SceneService/interface/ISceneWasLoaded.cs`
- **클래스 목적 및 의도**: `ISceneWasLoaded`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IEventSystemHandler` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] OnSceneWasLoaded(object argument)` ➔ `void`: OnSceneWasLoaded 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Client/Routine/interface`

**모듈 내 포함 스크립트 수**: 2개

#### 📄 클래스: `INativeRoutine` (interface)
- **경로**: `Haare/Scripts/Client/Routine/interface/INativeRoutine.cs`
- **클래스 목적 및 의도**: `INativeRoutine`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IRoutine` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] UpdateProcess()` ➔ `void`: UpdateProcess 관련 로직 수행 함수
  - `[private] OnApplicationQuit()` ➔ `void`: OnApplicationQuit 관련 로직 수행 함수
  - `[private] OnApplicationPause(bool pauseStatus)` ➔ `void`: OnApplicationPause 관련 로직 수행 함수

#### 📄 클래스: `IRoutine` (interface)
- **경로**: `Haare/Scripts/Client/Routine/interface/IRoutine.cs`
- **클래스 목적 및 의도**: `IRoutine`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[private] Finalize()` ➔ `UniTask`: Finalize 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Client/UI/Animator`

**모듈 내 포함 스크립트 수**: 1개

#### 📄 클래스: `UIAnimator` (class)
- **경로**: `Haare/Scripts/Client/UI/Animator/TweenUI.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `UIAnimator` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] targetTransform` (Transform): UIAnimator의 내부 데이터 또는 의존 참조 변수
  - `[private] currentHoverTween` (Tween): UIAnimator의 내부 데이터 또는 의존 참조 변수
  - `[private] originalScale` (Vector3): UIAnimator의 내부 데이터 또는 의존 참조 변수
  - `[public] panelRectTransform` (RectTransform): UIAnimator의 내부 데이터 또는 의존 참조 변수
  - `[private] sequence` (Sequence): UIAnimator의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] UIAnimator(
            GameObject target)` ➔ `public`: UIAnimator 관련 로직 수행 함수
  - `[public] TriggerHoverEnter(float scaleMultiplier, float duration)` ➔ `void`: TriggerHoverEnter 관련 로직 수행 함수
  - `[public] TriggerHoverExit(float duration)` ➔ `void`: TriggerHoverExit 관련 로직 수행 함수
  - `[public] TriggerClickAsync(float punchScale, float duration)` ➔ `UniTask`: TriggerClickAsync 관련 로직 수행 함수
  - `[public] TriggerClick(float punchScale, float duration)` ➔ `void`: TriggerClick 관련 로직 수행 함수
  - `[public] clearSlidePostion(RectTransform offScreenPosition)` ➔ `void`: clearSlidePostion 관련 로직 수행 함수
  - `[public] SlideOpenPanel(float slideDuration,RectTransform onScreenPosition,Ease easeType)` ➔ `void`: SlideOpenPanel 관련 로직 수행 함수
  - `[public] SlideClosePanel(float slideDuration,RectTransform offScreenPosition,Ease easeType)` ➔ `void`: SlideClosePanel 관련 로직 수행 함수
  - `[public] OpenPopup(float duration,Ease easeType)` ➔ `void`: OpenPopup 관련 로직 수행 함수
  - `[public] ClosePopup(float duration,Ease easeType)` ➔ `void`: ClosePopup 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Client/UI/Button`

**모듈 내 포함 스크립트 수**: 1개

#### 📄 클래스: `CustomButton` (class)
- **경로**: `Haare/Scripts/Client/UI/Button/CustomButton.cs`
- **클래스 목적 및 의도**: `CustomButton`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoRoutine` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] INTERACTIABLE` (bool): CustomButton의 내부 데이터 또는 의존 참조 변수
  - `[public] OPTION_HOVERIMAGE` (bool): CustomButton의 내부 데이터 또는 의존 참조 변수
  - `[public] OPTION_HOVERALPHA` (bool): CustomButton의 내부 데이터 또는 의존 참조 변수
  - `[public] OPTION_ANIMATION` (bool): CustomButton의 내부 데이터 또는 의존 참조 변수
  - `[private] _isLocked` (bool): CustomButton의 내부 데이터 또는 의존 참조 변수
  - `[private] _animator` (UIAnimator): CustomButton의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[public] SetInteractable(bool isInteractable)` ➔ `void`: SetInteractable 관련 로직 수행 함수
  - `[public] Finalize()` ➔ `UniTask`: Finalize 관련 로직 수행 함수
  - `[public] OnPointerClick(PointerEventData eventData)` ➔ `void`: OnPointerClick 관련 로직 수행 함수
  - `[public] OnPointerDown(PointerEventData eventData)` ➔ `void`: OnPointerDown 관련 로직 수행 함수
  - `[public] OnPointerExit(PointerEventData eventData)` ➔ `void`: OnPointerExit 관련 로직 수행 함수
  - `[public] OnPointerEnter(PointerEventData eventData)` ➔ `void`: OnPointerEnter 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Client/UI/Image`

**모듈 내 포함 스크립트 수**: 1개

#### 📄 클래스: `CustomImage` (class)
- **경로**: `Haare/Scripts/Client/UI/Image/CustomImage.cs`
- **클래스 목적 및 의도**: `CustomImage`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoRoutine` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] _image` (Image): CustomImage의 내부 데이터 또는 의존 참조 변수
  - `[private] CommonSprite` (Sprite): CustomImage의 내부 데이터 또는 의존 참조 변수
  - `[private] HoveredSprite` (Sprite): CustomImage의 내부 데이터 또는 의존 참조 변수
  - `[private] ClickedSprite` (Sprite): CustomImage의 내부 데이터 또는 의존 참조 변수
  - `[public] OPTION_ANIMATION` (bool): CustomImage의 내부 데이터 또는 의존 참조 변수
  - `[public] ANIMATION_SLIDE` (bool): CustomImage의 내부 데이터 또는 의존 참조 변수
  - `[public] ANIMATION_POPUP` (bool): CustomImage의 내부 데이터 또는 의존 참조 변수
  - `[private] _originalColor` (Color): CustomImage의 내부 데이터 또는 의존 참조 변수
  - `[private] _animator` (UIAnimator): CustomImage의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[protected] Constructor()` ➔ `void`: Constructor 관련 로직 수행 함수
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[public] ClearSlidePosition()` ➔ `void`: ClearSlidePosition 관련 로직 수행 함수
  - `[public] SlideOpenPanel()` ➔ `void`: SlideOpenPanel 관련 로직 수행 함수
  - `[public] PopupOpenPanel()` ➔ `void`: PopupOpenPanel 관련 로직 수행 함수
  - `[public] PopupclosePanel()` ➔ `void`: PopupclosePanel 관련 로직 수행 함수
  - `[private] SetupImage()` ➔ `void`: SetupImage 관련 로직 수행 함수
  - `[public] ChangeColor(Color b)` ➔ `void`: ChangeColor 관련 로직 수행 함수
  - `[public] Fade(float startAlpha,float targetAlpha, float duration)` ➔ `UniTask`: Fade 관련 로직 수행 함수
  - `[public] ChangeHoverColor()` ➔ `void`: ChangeHoverColor 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Client/UI/Panel/Attribute`

**모듈 내 포함 스크립트 수**: 1개

#### 📄 클래스: `PanelAttribute` (class)
- **경로**: `Haare/Scripts/Client/UI/Panel/Attribute/PanelAttribute.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `PanelAttribute` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `Attribute` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] PanelAttribute(string addressablePath)` ➔ `public`: PanelAttribute 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Client/UI/Panel/interface`

**모듈 내 포함 스크립트 수**: 2개

#### 📄 클래스: `ICustomPanel` (interface)
- **경로**: `Haare/Scripts/Client/UI/Panel/interface/ICustomPanel.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `ICustomPanel` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] setData` (Func<UniTask>): ICustomPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] setData` (Func<UniTask>): ICustomPanel의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] OpenPanel()` ➔ `void`: OpenPanel 관련 로직 수행 함수
  - `[public] ClosePanel()` ➔ `void`: ClosePanel 관련 로직 수행 함수
  - `[public] ReloadPanel()` ➔ `void`: ReloadPanel 관련 로직 수행 함수
  - `[private] setData()` ➔ `await`: setData 관련 로직 수행 함수
  - `[private] setData()` ➔ `await`: setData 관련 로직 수행 함수
  - `[private] setDataWithData()` ➔ `await`: setDataWithData 관련 로직 수행 함수
  - `[private] bindEventWithData()` ➔ `await`: bindEventWithData 관련 로직 수행 함수
  - `[public] BindEvent()` ➔ `void`: BindEvent 관련 로직 수행 함수
  - `[public] BindEvent(IPanelData data)` ➔ `UniTask`: BindEvent 관련 로직 수행 함수
  - `[public] SetData(IPanelData data)` ➔ `UniTask`: SetData 관련 로직 수행 함수

#### 📄 클래스: `IPanelData` (interface)
- **경로**: `Haare/Scripts/Client/UI/Panel/interface/IPanelData.cs`
- **클래스 목적 및 의도**: `IPanelData` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

---

### 📦 Package: `Haare/Scripts/Client/UI/Slider`

**모듈 내 포함 스크립트 수**: 1개

#### 📄 클래스: `CustomSlider` (class)
- **경로**: `Haare/Scripts/Client/UI/Slider/CustomSlider.cs`
- **클래스 목적 및 의도**: `CustomSlider`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoRoutine` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _slider` (Slider): CustomSlider의 내부 데이터 또는 의존 참조 변수
  - `[public] Value` (float): CustomSlider의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[protected] Constructor()` ➔ `void`: Constructor 관련 로직 수행 함수
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[public] Setup(float minValue, float maxValue, float currentValue)` ➔ `void`: Setup 관련 로직 수행 함수
  - `[public] SetValue(float value)` ➔ `void`: SetValue 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Client/UI/Text`

**모듈 내 포함 스크립트 수**: 1개

#### 📄 클래스: `CustomText` (class)
- **경로**: `Haare/Scripts/Client/UI/Text/CustomText.cs`
- **클래스 목적 및 의도**: `CustomText`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoRoutine` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _text` (TMP_Text): CustomText의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[protected] Constructor()` ➔ `void`: Constructor 관련 로직 수행 함수
  - `[public] SetupText(string value)` ➔ `void`: SetupText 관련 로직 수행 함수
  - `[public] SetupTextColor(Color32 color)` ➔ `void`: SetupTextColor 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Client/UI/UiManager`

**모듈 내 포함 스크립트 수**: 3개

#### 📄 클래스: `CoreUIManager` (class)
- **경로**: `Haare/Scripts/Client/UI/UiManager/CoreUIManager.cs`
- **클래스 목적 및 의도**: 게임 내 `CoreUI` 관련 전역 상태 및 루프 시스템을 총괄 관리하는 매니저 클래스입니다.
- **상속 및 구현 관계**: `SceneUIManager` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수

#### 📄 클래스: `SceneUIManager` (class)
- **경로**: `Haare/Scripts/Client/UI/UiManager/SceneUiManager.cs`
- **클래스 목적 및 의도**: 게임 내 `SceneUI` 관련 전역 상태 및 루프 시스템을 총괄 관리하는 매니저 클래스입니다.
- **상속 및 구현 관계**: `MonoRoutine, ISceneWasLoaded` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] TypePanelStack` (Stack<PanelType>): SceneUIManager의 내부 데이터 또는 의존 참조 변수
  - `[public] ILoadedScene` (bool): SceneUIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] safeArea` (Rect): SceneUIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] anchorMin` (Vector2): SceneUIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] anchorMax` (Vector2): SceneUIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] key` (PanelType): SceneUIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] null` (return): SceneUIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] findRentPanel` (var): SceneUIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] pageTypeToRegister` (var): SceneUIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] panel` (var): SceneUIManager의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[private] OnValidate()` ➔ `void`: OnValidate 관련 로직 수행 함수
  - `[private] ApplySafeArea()` ➔ `void`: ApplySafeArea 관련 로직 수행 함수
  - `[public] PeekPanel()` ➔ `ICustomPanel`: PeekPanel 관련 로직 수행 함수
  - `[private] onCompletedTask()` ➔ `await`: onCompletedTask 관련 로직 수행 함수
  - `[private] onCompletedTask()` ➔ `await`: onCompletedTask 관련 로직 수행 함수

#### 📄 클래스: `PanelType` (class)
- **경로**: `Haare/Scripts/Client/UI/UiManager/SceneUiManager.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `PanelType` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] TypePanelStack` (Stack<PanelType>): PanelType의 내부 데이터 또는 의존 참조 변수
  - `[public] ILoadedScene` (bool): PanelType의 내부 데이터 또는 의존 참조 변수
  - `[private] safeArea` (Rect): PanelType의 내부 데이터 또는 의존 참조 변수
  - `[private] anchorMin` (Vector2): PanelType의 내부 데이터 또는 의존 참조 변수
  - `[private] anchorMax` (Vector2): PanelType의 내부 데이터 또는 의존 참조 변수
  - `[private] key` (PanelType): PanelType의 내부 데이터 또는 의존 참조 변수
  - `[private] null` (return): PanelType의 내부 데이터 또는 의존 참조 변수
  - `[private] findRentPanel` (var): PanelType의 내부 데이터 또는 의존 참조 변수
  - `[private] pageTypeToRegister` (var): PanelType의 내부 데이터 또는 의존 참조 변수
  - `[private] panel` (var): PanelType의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[private] OnValidate()` ➔ `void`: OnValidate 관련 로직 수행 함수
  - `[private] ApplySafeArea()` ➔ `void`: ApplySafeArea 관련 로직 수행 함수
  - `[public] PeekPanel()` ➔ `ICustomPanel`: PeekPanel 관련 로직 수행 함수
  - `[private] onCompletedTask()` ➔ `await`: onCompletedTask 관련 로직 수행 함수
  - `[private] onCompletedTask()` ➔ `await`: onCompletedTask 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Util/AssetLoader`

**모듈 내 포함 스크립트 수**: 5개

#### 📄 클래스: `AddressableLoader` (class)
- **경로**: `Haare/Scripts/Util/AssetLoader/AddressableLoader.cs`
- **클래스 목적 및 의도**: `AddressableLoader`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoBehaviour` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] downMessage` (GameObject): AddressableLoader의 내부 데이터 또는 의존 참조 변수
  - `[public] downSlider` (Slider): AddressableLoader의 내부 데이터 또는 의존 참조 변수
  - `[public] sizeInfoText` (Text): AddressableLoader의 내부 데이터 또는 의존 참조 변수
  - `[public] downValText` (Text): AddressableLoader의 내부 데이터 또는 의존 참조 변수
  - `[private] patchSize` (long): AddressableLoader의 내부 데이터 또는 의존 참조 변수
  - `[private] init` (var): AddressableLoader의 내부 데이터 또는 의존 참조 변수
  - `[private] size` (string): AddressableLoader의 내부 데이터 또는 의존 참조 변수
  - `[private] size` (return): AddressableLoader의 내부 데이터 또는 의존 참조 변수
  - `[private] labels` (var): AddressableLoader의 내부 데이터 또는 의존 참조 변수
  - `[private] handle` (var): AddressableLoader의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Start()` ➔ `void`: Start 관련 로직 수행 함수
  - `[private] InitAddressable()` ➔ `IEnumerator`: InitAddressable 관련 로직 수행 함수
  - `[private] GetFileSize(long byteCnt)` ➔ `string`: GetFileSize 관련 로직 수행 함수
  - `[public] Button_Down()` ➔ `void`: Button_Down 관련 로직 수행 함수
  - `[private] CheckUpdateFiles()` ➔ `IEnumerator`: CheckUpdateFiles 관련 로직 수행 함수
  - `[private] PatchFiles()` ➔ `IEnumerator`: PatchFiles 관련 로직 수행 함수
  - `[private] DownLoadLabel(string label)` ➔ `IEnumerator`: DownLoadLabel 관련 로직 수행 함수
  - `[private] CheckDownLoad()` ➔ `IEnumerator`: CheckDownLoad 관련 로직 수행 함수

#### 📄 클래스: `AssetLoader` (class)
- **경로**: `Haare/Scripts/Util/AssetLoader/AssetLoader.cs`
- **클래스 목적 및 의도**: `AssetLoader`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] _dlProgress` (ReactiveProperty<float>): AssetLoader의 내부 데이터 또는 의존 참조 변수
  - `[public] DownloadProgress` (ReadOnlyReactiveProperty<float>): AssetLoader의 내부 데이터 또는 의존 참조 변수
  - `[public] AssetDownloadTaskFinished` (Subject<bool>): AssetLoader의 내부 데이터 또는 의존 참조 변수
  - `[private] cts` (CancellationToken): AssetLoader의 내부 데이터 또는 의존 참조 변수
  - `[private] instance` (GameObject): AssetLoader의 내부 데이터 또는 의존 참조 변수
  - `[private] component` (return): AssetLoader의 내부 데이터 또는 의존 참조 변수
  - `[private] null` (return): AssetLoader의 내부 데이터 또는 의존 참조 변수
  - `[private] null` (return): AssetLoader의 내부 데이터 또는 의존 참조 변수
  - `[private] handle` (AsyncOperationHandle<T>): AssetLoader의 내부 데이터 또는 의존 참조 변수
  - `[private] asset` (T): AssetLoader의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] SaveJson(string filePath, object data)` ➔ `UniTask`: SaveJson 관련 로직 수행 함수
  - `[public] Exists(string fileName)` ➔ `bool`: Exists 관련 로직 수행 함수
  - `[public] LoadJsonAsync(string fileName)` ➔ `UniTask<TextAsset>`: LoadJsonAsync 관련 로직 수행 함수

#### 📄 클래스: `AssetPath` (class)
- **경로**: `Haare/Scripts/Util/AssetLoader/AssetPath.cs`
- **클래스 목적 및 의도**: `AssetPath`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] SCENE_PATH` (string): AssetPath의 내부 데이터 또는 의존 참조 변수
  - `[public] SCENE_EXT` (string): AssetPath의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `JsonUtil` (class)
- **경로**: `Haare/Scripts/Util/AssetLoader/JsonUtil.cs`
- **클래스 목적 및 의도**: `JsonUtil`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] method` (var): JsonUtil의 내부 데이터 또는 의존 참조 변수
  - `[private] genericMethod` (var): JsonUtil의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] FromJson(string json, Type targetType)` ➔ `object`: FromJson 관련 로직 수행 함수

#### 📄 클래스: `JsonUtilityGeneric` (class)
- **경로**: `Haare/Scripts/Util/AssetLoader/JsonUtil.cs`
- **클래스 목적 및 의도**: `JsonUtilityGeneric`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] method` (var): JsonUtilityGeneric의 내부 데이터 또는 의존 참조 변수
  - `[private] genericMethod` (var): JsonUtilityGeneric의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] FromJson(string json, Type targetType)` ➔ `object`: FromJson 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Util/FPSLogger`

**모듈 내 포함 스크립트 수**: 1개

#### 📄 클래스: `FPSLogger` (class)
- **경로**: `Haare/Scripts/Util/FPSLogger/FPSLogger.cs`
- **클래스 목적 및 의도**: `FPSLogger`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoRoutine` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] fpsText` (CustomText): FPSLogger의 내부 데이터 또는 의존 참조 변수
  - `[private] deltaTime` (float): FPSLogger의 내부 데이터 또는 의존 참조 변수
  - `[private] fps` (float): FPSLogger의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[protected] UpdateProcess()` ➔ `void`: UpdateProcess 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Util/HashGenerator`

**모듈 내 포함 스크립트 수**: 1개

#### 📄 클래스: `HashGenerator` (class)
- **경로**: `Haare/Scripts/Util/HashGenerator/HashGenerator.cs`
- **클래스 목적 및 의도**: `HashGenerator`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] UniqueId` (string): HashGenerator의 내부 데이터 또는 의존 참조 변수
  - `[private] 0` (return): HashGenerator의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] GetUniqueHashCode()` ➔ `int`: GetUniqueHashCode 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Util/LogHelper`

**모듈 내 포함 스크립트 수**: 1개

#### 📄 클래스: `LogHelper` (class)
- **경로**: `Haare/Scripts/Util/LogHelper/LogHelper.cs`
- **클래스 목적 및 의도**: `LogHelper`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] message` (string): LogHelper의 내부 데이터 또는 의존 참조 변수
  - `[private] headerBuilder` (StringBuilder): LogHelper의 내부 데이터 또는 의존 참조 변수
  - `[private] message` (string): LogHelper의 내부 데이터 또는 의존 참조 변수
  - `[private] headerBuilder` (StringBuilder): LogHelper의 내부 데이터 또는 의존 참조 변수
  - `[private] message` (string): LogHelper의 내부 데이터 또는 의존 참조 변수
  - `[private] sb` (StringBuilder): LogHelper의 내부 데이터 또는 의존 참조 변수
  - `[public] DEMO` (string): LogHelper의 내부 데이터 또는 의존 참조 변수
  - `[public] CANCELLED` (string): LogHelper의 내부 데이터 또는 의존 참조 변수
  - `[public] TASK` (string): LogHelper의 내부 데이터 또는 의존 참조 변수
  - `[public] SERVER` (string): LogHelper의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Log(params string[] contents)` ➔ `void`: Log 관련 로직 수행 함수
  - `[public] LogTask(params string[] contents)` ➔ `void`: LogTask 관련 로직 수행 함수
  - `[public] Warning(params string[] contents)` ➔ `void`: Warning 관련 로직 수행 함수
  - `[public] Error(params string[] contents)` ➔ `void`: Error 관련 로직 수행 함수

---

### 📦 Package: `Haare/Scripts/Util/Prefab`

**모듈 내 포함 스크립트 수**: 3개

#### 📄 클래스: `PrefabPath` (class)
- **경로**: `Haare/Scripts/Util/Prefab/PrefabPath.cs`
- **클래스 목적 및 의도**: `PrefabPath`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] CORE_CANVAS` (string): PrefabPath의 내부 데이터 또는 의존 참조 변수
  - `[private] DEBUG_PANEL` (string): PrefabPath의 내부 데이터 또는 의존 참조 변수
  - `[private] DEMO_TITLE_PANEL` (string): PrefabPath의 내부 데이터 또는 의존 참조 변수
  - `[private] DEMO_LOADING_PANEL` (string): PrefabPath의 내부 데이터 또는 의존 참조 변수
  - `[private] DEMO_LOADINGFADE_PANEL` (string): PrefabPath의 내부 데이터 또는 의존 참조 변수
  - `[private] LOBBY_BASE_PANEL` (string): PrefabPath의 내부 데이터 또는 의존 참조 변수
  - `[private] LOBBY_PANEL` (string): PrefabPath의 내부 데이터 또는 의존 참조 변수
  - `[private] ITEM_PANEL` (string): PrefabPath의 내부 데이터 또는 의존 참조 변수
  - `[public] SCENE_PATH` (string): PrefabPath의 내부 데이터 또는 의존 참조 변수
  - `[public] SCENE_EXT` (string): PrefabPath의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `PrefabUtil` (class)
- **경로**: `Haare/Scripts/Util/Prefab/PrefabUtil.cs`
- **클래스 목적 및 의도**: `PrefabUtil`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] handle` (AsyncOperationHandle<GameObject>): PrefabUtil의 내부 데이터 또는 의존 참조 변수
  - `[private] instance` (GameObject): PrefabUtil의 내부 데이터 또는 의존 참조 변수
  - `[private] component` (return): PrefabUtil의 내부 데이터 또는 의존 참조 변수
  - `[private] null` (return): PrefabUtil의 내부 데이터 또는 의존 참조 변수
  - `[private] null` (return): PrefabUtil의 내부 데이터 또는 의존 참조 변수
  - `[public] PrefabPath` (string): PrefabUtil의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] PrefabParam(string prefabPath)` ➔ `public`: PrefabParam 관련 로직 수행 함수

#### 📄 클래스: `PrefabParam` (class)
- **경로**: `Haare/Scripts/Util/Prefab/PrefabUtil.cs`
- **클래스 목적 및 의도**: `PrefabParam`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] handle` (AsyncOperationHandle<GameObject>): PrefabParam의 내부 데이터 또는 의존 참조 변수
  - `[private] instance` (GameObject): PrefabParam의 내부 데이터 또는 의존 참조 변수
  - `[private] component` (return): PrefabParam의 내부 데이터 또는 의존 참조 변수
  - `[private] null` (return): PrefabParam의 내부 데이터 또는 의존 참조 변수
  - `[private] null` (return): PrefabParam의 내부 데이터 또는 의존 참조 변수
  - `[public] PrefabPath` (string): PrefabParam의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] PrefabParam(string prefabPath)` ➔ `public`: PrefabParam 관련 로직 수행 함수

---

### 📦 Package: `Map`

**모듈 내 포함 스크립트 수**: 31개

#### 📄 클래스: `CreateMap` (class)
- **경로**: `Script/Map/CreateMap.Connection.cs`
- **클래스 목적 및 의도**: `CreateMap`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] w` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] h` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] ddx` (int[]): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] ddy` (int[]): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] idA` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] nx` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] ny` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] idB` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] roomRoleMap` (var): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] subPurposeIds` (var): CreateMap의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] BuildGraph(ref Floor floor)` ➔ `void`: BuildGraph 관련 로직 수행 함수
  - `[private] AddEdge(int a, int b)` ➔ `void`: AddEdge 관련 로직 수행 함수
  - `[private] ConnectRooms(ref Floor floor)` ➔ `void`: ConnectRooms 관련 로직 수행 함수

#### 📄 클래스: `CreateMap` (class)
- **경로**: `Script/Map/CreateMap.cs`
- **클래스 목적 및 의도**: `CreateMap`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] map` (Map): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[public] floorConfigs` (FloorConfig[]): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[public] useFixedSeed` (bool): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[public] seed` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[public] currentFloorIndex` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[public] maxRetryCount` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[public] showGizmos` (bool): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[public] gizmoShowRoomBounds` (bool): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[public] gizmoShowPassages` (bool): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[public] gizmoShowStairs` (bool): CreateMap의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] GenerateMap()` ➔ `void`: GenerateMap 관련 로직 수행 함수
  - `[private] InitMap()` ➔ `void`: InitMap 관련 로직 수행 함수

#### 📄 클래스: `CreateMap` (class)
- **경로**: `Script/Map/CreateMap.Gizmos.cs`
- **클래스 목적 및 의도**: `CreateMap`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `CreateMap` (class)
- **경로**: `Script/Map/CreateMap.RoomPlacement.cs`
- **클래스 목적 및 의도**: `CreateMap`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] bossOccupied` (bool[,]): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] w` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] h` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] fmt` (string): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] shapes` (var): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] corners` (var): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] sx` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] da` (float): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] db` (float): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] placed` (bool): CreateMap의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] PlaceBossRoom(ref Floor floor)` ➔ `void`: PlaceBossRoom 관련 로직 수행 함수

#### 📄 클래스: `CreateMap` (class)
- **경로**: `Script/Map/CreateMap.RoomRoles.cs`
- **클래스 목적 및 의도**: `CreateMap`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] w` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] h` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] roomIds` (var): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] startRoomId` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] hasBossAlready` (bool): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (Chunks): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] distFromStart` (var): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] sortedByDist` (var): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] da` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] db` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] AssignRoomRoles(ref Floor floor)` ➔ `void`: AssignRoomRoles 관련 로직 수행 함수
  - `[private] PruneExcessRooms(ref Floor floor)` ➔ `void`: PruneExcessRooms 관련 로직 수행 함수

#### 📄 클래스: `CreateMap` (class)
- **경로**: `Script/Map/CreateMap.RuntimeAPI.cs`
- **클래스 목적 및 의도**: `CreateMap`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] default` (return): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] key` (var): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] floor` (Floor): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] count` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] w` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (Chunks): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] count` (return): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] json` (string): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] json` (string): CreateMap의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] GetCurrentFloor()` ➔ `Floor`: GetCurrentFloor 관련 로직 수행 함수
  - `[public] SerializeMap()` ➔ `string`: SerializeMap 관련 로직 수행 함수
  - `[public] ApplyMap(Map newMap)` ➔ `void`: ApplyMap 관련 로직 수행 함수
  - `[public] GetRoomFloorTileCount(int floorIndex, int roomId)` ➔ `int`: GetRoomFloorTileCount 관련 로직 수행 함수
  - `[public] DeserializeMap(string json)` ➔ `void`: DeserializeMap 관련 로직 수행 함수
  - `[public] SaveMapToFile(string filePath)` ➔ `void`: SaveMapToFile 관련 로직 수행 함수
  - `[public] LoadMapFromFile(string filePath)` ➔ `void`: LoadMapFromFile 관련 로직 수행 함수
  - `[public] ConquerRoom(int floorIndex, int roomId)` ➔ `void`: ConquerRoom 관련 로직 수행 함수
  - `[public] RetreatFromRoom(int floorIndex, int roomId)` ➔ `void`: RetreatFromRoom 관련 로직 수행 함수
  - `[private] SetChunksOccupation(ref Floor floor, int roomId, OccupationState state)` ➔ `void`: SetChunksOccupation 관련 로직 수행 함수

#### 📄 클래스: `CreateMap` (class)
- **경로**: `Script/Map/CreateMap.Stairs.cs`
- **클래스 목적 및 의도**: `CreateMap`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] w` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] h` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] lobbyId` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] lobbyName` (string): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (Chunks): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (Chunks): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (Tile): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] w0` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] h0` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] stairX` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] GenerateFloor0()` ➔ `void`: GenerateFloor0 관련 로직 수행 함수
  - `[private] PlaceStairs()` ➔ `void`: PlaceStairs 관련 로직 수행 함수
  - `[private] UpdateGateWidthsAfterStairs()` ➔ `void`: UpdateGateWidthsAfterStairs 관련 로직 수행 함수

#### 📄 클래스: `CreateMap` (class)
- **경로**: `Script/Map/CreateMap.TileWall.cs`
- **클래스 목적 및 의도**: `CreateMap`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] w` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] h` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (Chunks): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] w` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] h` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] thkMin` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] thkMax` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] roomBounds` (var): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (Chunks): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] isLeft` (bool): CreateMap의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] AssignTileNames(ref Floor floor)` ➔ `void`: AssignTileNames 관련 로직 수행 함수
  - `[private] ApplyOuterWallThickness(ref Floor floor)` ➔ `void`: ApplyOuterWallThickness 관련 로직 수행 함수

#### 📄 클래스: `CreateMap` (class)
- **경로**: `Script/Map/CreateMap.Validation.cs`
- **클래스 목적 및 의도**: `CreateMap`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] errors` (var): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] errors` (return): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] errors` (return): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] errors` (var): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] fId` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] w` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] h` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] startCount` (int): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] startIds` (var): CreateMap의 내부 데이터 또는 의존 참조 변수
  - `[private] bossIds` (var): CreateMap의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ValidateMap()` ➔ `List<string>`: ValidateMap 관련 로직 수행 함수
  - `[private] ValidateFloor(ref Floor floor)` ➔ `List<string>`: ValidateFloor 관련 로직 수행 함수

#### 📄 클래스: `InteractableObject` (class)
- **경로**: `Script/Map/InteractableObject.cs`
- **클래스 목적 및 의도**: `InteractableObject`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] Id` (string): InteractableObject의 내부 데이터 또는 의존 참조 변수
  - `[public] Position` (Vector3Int): InteractableObject의 내부 데이터 또는 의존 참조 변수
  - `[public] BaseInterest` (float): InteractableObject의 내부 데이터 또는 의존 참조 변수
  - `[public] BaseDanger` (float): InteractableObject의 내부 데이터 또는 의존 참조 변수
  - `[public] BaseVisibility` (float): InteractableObject의 내부 데이터 또는 의존 참조 변수
  - `[public] IsFullyBlocking` (bool): InteractableObject의 내부 데이터 또는 의존 참조 변수
  - `[public] IsCollected` (bool): InteractableObject의 내부 데이터 또는 의존 참조 변수
  - `[public] IsInvestigated` (bool): InteractableObject의 내부 데이터 또는 의존 참조 변수
  - `[public] Tags` (List<string>): InteractableObject의 내부 데이터 또는 의존 참조 변수
  - `[public] CauserStage` (DangerStage): InteractableObject의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] InteractableObject(string id, Vector3Int position, float baseInterest, float baseDanger = 0f, List<string> tags = null, DangerStage causerStage = DangerStage.Stage0, string traceId = null, float baseVisibility = 0f, bool isFullyBlocking = false, float trapHp = 0f, float trapDamageMin = 0f, float trapDamageMax = 0f)` ➔ `public`: InteractableObject 관련 로직 수행 함수

#### 📄 클래스: `FloorId` (enum)
- **경로**: `Script/Map/MapData.cs`
- **클래스 목적 및 의도**: `FloorId`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] roomA` (int): FloorId의 내부 데이터 또는 의존 참조 변수
  - `[public] roomB` (int): FloorId의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAX` (int): FloorId의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAY` (int): FloorId의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBX` (int): FloorId의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBY` (int): FloorId의 내부 데이터 또는 의존 참조 변수
  - `[public] width` (int): FloorId의 내부 데이터 또는 의존 참조 변수
  - `[public] isHorizontal` (bool): FloorId의 내부 데이터 또는 의존 참조 변수
  - `[public] name` (string): FloorId의 내부 데이터 또는 의존 참조 변수
  - `[public] effect` (TileEffect): FloorId의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Wall()` ➔ `Tile`: Wall 관련 로직 수행 함수
  - `[public] Floor()` ➔ `Tile`: Floor 관련 로직 수행 함수
  - `[public] Stair()` ➔ `Tile`: Stair 관련 로직 수행 함수
  - `[public] CreateDefault()` ➔ `FloorConfig[]`: CreateDefault 관련 로직 수행 함수

#### 📄 클래스: `RoomRole` (enum)
- **경로**: `Script/Map/MapData.cs`
- **클래스 목적 및 의도**: `RoomRole`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] roomA` (int): RoomRole의 내부 데이터 또는 의존 참조 변수
  - `[public] roomB` (int): RoomRole의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAX` (int): RoomRole의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAY` (int): RoomRole의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBX` (int): RoomRole의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBY` (int): RoomRole의 내부 데이터 또는 의존 참조 변수
  - `[public] width` (int): RoomRole의 내부 데이터 또는 의존 참조 변수
  - `[public] isHorizontal` (bool): RoomRole의 내부 데이터 또는 의존 참조 변수
  - `[public] name` (string): RoomRole의 내부 데이터 또는 의존 참조 변수
  - `[public] effect` (TileEffect): RoomRole의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Wall()` ➔ `Tile`: Wall 관련 로직 수행 함수
  - `[public] Floor()` ➔ `Tile`: Floor 관련 로직 수행 함수
  - `[public] Stair()` ➔ `Tile`: Stair 관련 로직 수행 함수
  - `[public] CreateDefault()` ➔ `FloorConfig[]`: CreateDefault 관련 로직 수행 함수

#### 📄 클래스: `OccupationState` (enum)
- **경로**: `Script/Map/MapData.cs`
- **클래스 목적 및 의도**: `OccupationState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] roomA` (int): OccupationState의 내부 데이터 또는 의존 참조 변수
  - `[public] roomB` (int): OccupationState의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAX` (int): OccupationState의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAY` (int): OccupationState의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBX` (int): OccupationState의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBY` (int): OccupationState의 내부 데이터 또는 의존 참조 변수
  - `[public] width` (int): OccupationState의 내부 데이터 또는 의존 참조 변수
  - `[public] isHorizontal` (bool): OccupationState의 내부 데이터 또는 의존 참조 변수
  - `[public] name` (string): OccupationState의 내부 데이터 또는 의존 참조 변수
  - `[public] effect` (TileEffect): OccupationState의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Wall()` ➔ `Tile`: Wall 관련 로직 수행 함수
  - `[public] Floor()` ➔ `Tile`: Floor 관련 로직 수행 함수
  - `[public] Stair()` ➔ `Tile`: Stair 관련 로직 수행 함수
  - `[public] CreateDefault()` ➔ `FloorConfig[]`: CreateDefault 관련 로직 수행 함수

#### 📄 클래스: `TileEffect` (enum)
- **경로**: `Script/Map/MapData.cs`
- **클래스 목적 및 의도**: `TileEffect`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] roomA` (int): TileEffect의 내부 데이터 또는 의존 참조 변수
  - `[public] roomB` (int): TileEffect의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAX` (int): TileEffect의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAY` (int): TileEffect의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBX` (int): TileEffect의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBY` (int): TileEffect의 내부 데이터 또는 의존 참조 변수
  - `[public] width` (int): TileEffect의 내부 데이터 또는 의존 참조 변수
  - `[public] isHorizontal` (bool): TileEffect의 내부 데이터 또는 의존 참조 변수
  - `[public] name` (string): TileEffect의 내부 데이터 또는 의존 참조 변수
  - `[public] effect` (TileEffect): TileEffect의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Wall()` ➔ `Tile`: Wall 관련 로직 수행 함수
  - `[public] Floor()` ➔ `Tile`: Floor 관련 로직 수행 함수
  - `[public] Stair()` ➔ `Tile`: Stair 관련 로직 수행 함수
  - `[public] CreateDefault()` ➔ `FloorConfig[]`: CreateDefault 관련 로직 수행 함수

#### 📄 클래스: `Footprint` (enum)
- **경로**: `Script/Map/MapData.cs`
- **클래스 목적 및 의도**: `Footprint`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] roomA` (int): Footprint의 내부 데이터 또는 의존 참조 변수
  - `[public] roomB` (int): Footprint의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAX` (int): Footprint의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAY` (int): Footprint의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBX` (int): Footprint의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBY` (int): Footprint의 내부 데이터 또는 의존 참조 변수
  - `[public] width` (int): Footprint의 내부 데이터 또는 의존 참조 변수
  - `[public] isHorizontal` (bool): Footprint의 내부 데이터 또는 의존 참조 변수
  - `[public] name` (string): Footprint의 내부 데이터 또는 의존 참조 변수
  - `[public] effect` (TileEffect): Footprint의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Wall()` ➔ `Tile`: Wall 관련 로직 수행 함수
  - `[public] Floor()` ➔ `Tile`: Floor 관련 로직 수행 함수
  - `[public] Stair()` ➔ `Tile`: Stair 관련 로직 수행 함수
  - `[public] CreateDefault()` ➔ `FloorConfig[]`: CreateDefault 관련 로직 수행 함수

#### 📄 클래스: `Gate` (struct)
- **경로**: `Script/Map/MapData.cs`
- **클래스 목적 및 의도**: `Gate`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] roomA` (int): Gate의 내부 데이터 또는 의존 참조 변수
  - `[public] roomB` (int): Gate의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAX` (int): Gate의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAY` (int): Gate의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBX` (int): Gate의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBY` (int): Gate의 내부 데이터 또는 의존 참조 변수
  - `[public] width` (int): Gate의 내부 데이터 또는 의존 참조 변수
  - `[public] isHorizontal` (bool): Gate의 내부 데이터 또는 의존 참조 변수
  - `[public] name` (string): Gate의 내부 데이터 또는 의존 참조 변수
  - `[public] effect` (TileEffect): Gate의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Wall()` ➔ `Tile`: Wall 관련 로직 수행 함수
  - `[public] Floor()` ➔ `Tile`: Floor 관련 로직 수행 함수
  - `[public] Stair()` ➔ `Tile`: Stair 관련 로직 수행 함수
  - `[public] CreateDefault()` ➔ `FloorConfig[]`: CreateDefault 관련 로직 수행 함수

#### 📄 클래스: `Tile` (struct)
- **경로**: `Script/Map/MapData.cs`
- **클래스 목적 및 의도**: `Tile`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] roomA` (int): Tile의 내부 데이터 또는 의존 참조 변수
  - `[public] roomB` (int): Tile의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAX` (int): Tile의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAY` (int): Tile의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBX` (int): Tile의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBY` (int): Tile의 내부 데이터 또는 의존 참조 변수
  - `[public] width` (int): Tile의 내부 데이터 또는 의존 참조 변수
  - `[public] isHorizontal` (bool): Tile의 내부 데이터 또는 의존 참조 변수
  - `[public] name` (string): Tile의 내부 데이터 또는 의존 참조 변수
  - `[public] effect` (TileEffect): Tile의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Wall()` ➔ `Tile`: Wall 관련 로직 수행 함수
  - `[public] Floor()` ➔ `Tile`: Floor 관련 로직 수행 함수
  - `[public] Stair()` ➔ `Tile`: Stair 관련 로직 수행 함수
  - `[public] CreateDefault()` ➔ `FloorConfig[]`: CreateDefault 관련 로직 수행 함수

#### 📄 클래스: `Chunks` (struct)
- **경로**: `Script/Map/MapData.cs`
- **클래스 목적 및 의도**: `Chunks`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] roomA` (int): Chunks의 내부 데이터 또는 의존 참조 변수
  - `[public] roomB` (int): Chunks의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAX` (int): Chunks의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAY` (int): Chunks의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBX` (int): Chunks의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBY` (int): Chunks의 내부 데이터 또는 의존 참조 변수
  - `[public] width` (int): Chunks의 내부 데이터 또는 의존 참조 변수
  - `[public] isHorizontal` (bool): Chunks의 내부 데이터 또는 의존 참조 변수
  - `[public] name` (string): Chunks의 내부 데이터 또는 의존 참조 변수
  - `[public] effect` (TileEffect): Chunks의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Wall()` ➔ `Tile`: Wall 관련 로직 수행 함수
  - `[public] Floor()` ➔ `Tile`: Floor 관련 로직 수행 함수
  - `[public] Stair()` ➔ `Tile`: Stair 관련 로직 수행 함수
  - `[public] CreateDefault()` ➔ `FloorConfig[]`: CreateDefault 관련 로직 수행 함수

#### 📄 클래스: `Floor` (struct)
- **경로**: `Script/Map/MapData.cs`
- **클래스 목적 및 의도**: `Floor`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] roomA` (int): Floor의 내부 데이터 또는 의존 참조 변수
  - `[public] roomB` (int): Floor의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAX` (int): Floor의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAY` (int): Floor의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBX` (int): Floor의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBY` (int): Floor의 내부 데이터 또는 의존 참조 변수
  - `[public] width` (int): Floor의 내부 데이터 또는 의존 참조 변수
  - `[public] isHorizontal` (bool): Floor의 내부 데이터 또는 의존 참조 변수
  - `[public] name` (string): Floor의 내부 데이터 또는 의존 참조 변수
  - `[public] effect` (TileEffect): Floor의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Wall()` ➔ `Tile`: Wall 관련 로직 수행 함수
  - `[public] Floor()` ➔ `Tile`: Floor 관련 로직 수행 함수
  - `[public] Stair()` ➔ `Tile`: Stair 관련 로직 수행 함수
  - `[public] CreateDefault()` ➔ `FloorConfig[]`: CreateDefault 관련 로직 수행 함수

#### 📄 클래스: `Map` (struct)
- **경로**: `Script/Map/MapData.cs`
- **클래스 목적 및 의도**: `Map`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] roomA` (int): Map의 내부 데이터 또는 의존 참조 변수
  - `[public] roomB` (int): Map의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAX` (int): Map의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAY` (int): Map의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBX` (int): Map의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBY` (int): Map의 내부 데이터 또는 의존 참조 변수
  - `[public] width` (int): Map의 내부 데이터 또는 의존 참조 변수
  - `[public] isHorizontal` (bool): Map의 내부 데이터 또는 의존 참조 변수
  - `[public] name` (string): Map의 내부 데이터 또는 의존 참조 변수
  - `[public] effect` (TileEffect): Map의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Wall()` ➔ `Tile`: Wall 관련 로직 수행 함수
  - `[public] Floor()` ➔ `Tile`: Floor 관련 로직 수행 함수
  - `[public] Stair()` ➔ `Tile`: Stair 관련 로직 수행 함수
  - `[public] CreateDefault()` ➔ `FloorConfig[]`: CreateDefault 관련 로직 수행 함수

#### 📄 클래스: `FloorConfig` (struct)
- **경로**: `Script/Map/MapData.cs`
- **클래스 목적 및 의도**: `FloorConfig` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] roomA` (int): FloorConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] roomB` (int): FloorConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAX` (int): FloorConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAY` (int): FloorConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBX` (int): FloorConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBY` (int): FloorConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] width` (int): FloorConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] isHorizontal` (bool): FloorConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] name` (string): FloorConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] effect` (TileEffect): FloorConfig의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Wall()` ➔ `Tile`: Wall 관련 로직 수행 함수
  - `[public] Floor()` ➔ `Tile`: Floor 관련 로직 수행 함수
  - `[public] Stair()` ➔ `Tile`: Stair 관련 로직 수행 함수
  - `[public] CreateDefault()` ➔ `FloorConfig[]`: CreateDefault 관련 로직 수행 함수

#### 📄 클래스: `MapData` (struct)
- **경로**: `Script/Map/MapData.cs`
- **클래스 목적 및 의도**: `MapData` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] roomA` (int): MapData의 내부 데이터 또는 의존 참조 변수
  - `[public] roomB` (int): MapData의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAX` (int): MapData의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAY` (int): MapData의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBX` (int): MapData의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBY` (int): MapData의 내부 데이터 또는 의존 참조 변수
  - `[public] width` (int): MapData의 내부 데이터 또는 의존 참조 변수
  - `[public] isHorizontal` (bool): MapData의 내부 데이터 또는 의존 참조 변수
  - `[public] name` (string): MapData의 내부 데이터 또는 의존 참조 변수
  - `[public] effect` (TileEffect): MapData의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Wall()` ➔ `Tile`: Wall 관련 로직 수행 함수
  - `[public] Floor()` ➔ `Tile`: Floor 관련 로직 수행 함수
  - `[public] Stair()` ➔ `Tile`: Stair 관련 로직 수행 함수
  - `[public] CreateDefault()` ➔ `FloorConfig[]`: CreateDefault 관련 로직 수행 함수

#### 📄 클래스: `TileFactory` (class)
- **경로**: `Script/Map/MapData.cs`
- **클래스 목적 및 의도**: `TileFactory`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] roomA` (int): TileFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] roomB` (int): TileFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAX` (int): TileFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAY` (int): TileFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBX` (int): TileFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBY` (int): TileFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] width` (int): TileFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] isHorizontal` (bool): TileFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] name` (string): TileFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] effect` (TileEffect): TileFactory의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Wall()` ➔ `Tile`: Wall 관련 로직 수행 함수
  - `[public] Floor()` ➔ `Tile`: Floor 관련 로직 수행 함수
  - `[public] Stair()` ➔ `Tile`: Stair 관련 로직 수행 함수
  - `[public] CreateDefault()` ➔ `FloorConfig[]`: CreateDefault 관련 로직 수행 함수

#### 📄 클래스: `RoomIdGenerator` (class)
- **경로**: `Script/Map/MapData.cs`
- **클래스 목적 및 의도**: `RoomIdGenerator`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] roomA` (int): RoomIdGenerator의 내부 데이터 또는 의존 참조 변수
  - `[public] roomB` (int): RoomIdGenerator의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAX` (int): RoomIdGenerator의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAY` (int): RoomIdGenerator의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBX` (int): RoomIdGenerator의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBY` (int): RoomIdGenerator의 내부 데이터 또는 의존 참조 변수
  - `[public] width` (int): RoomIdGenerator의 내부 데이터 또는 의존 참조 변수
  - `[public] isHorizontal` (bool): RoomIdGenerator의 내부 데이터 또는 의존 참조 변수
  - `[public] name` (string): RoomIdGenerator의 내부 데이터 또는 의존 참조 변수
  - `[public] effect` (TileEffect): RoomIdGenerator의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Wall()` ➔ `Tile`: Wall 관련 로직 수행 함수
  - `[public] Floor()` ➔ `Tile`: Floor 관련 로직 수행 함수
  - `[public] Stair()` ➔ `Tile`: Stair 관련 로직 수행 함수
  - `[public] CreateDefault()` ➔ `FloorConfig[]`: CreateDefault 관련 로직 수행 함수

#### 📄 클래스: `FloorConfigFactory` (class)
- **경로**: `Script/Map/MapData.cs`
- **클래스 목적 및 의도**: `FloorConfigFactory` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] roomA` (int): FloorConfigFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] roomB` (int): FloorConfigFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAX` (int): FloorConfigFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkAY` (int): FloorConfigFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBX` (int): FloorConfigFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] chunkBY` (int): FloorConfigFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] width` (int): FloorConfigFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] isHorizontal` (bool): FloorConfigFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] name` (string): FloorConfigFactory의 내부 데이터 또는 의존 참조 변수
  - `[public] effect` (TileEffect): FloorConfigFactory의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Wall()` ➔ `Tile`: Wall 관련 로직 수행 함수
  - `[public] Floor()` ➔ `Tile`: Floor 관련 로직 수행 함수
  - `[public] Stair()` ➔ `Tile`: Stair 관련 로직 수행 함수
  - `[public] CreateDefault()` ➔ `FloorConfig[]`: CreateDefault 관련 로직 수행 함수

#### 📄 클래스: `MapManager` (class)
- **경로**: `Script/Map/MapManager.cs`
- **클래스 목적 및 의도**: 게임 내 `Map` 관련 전역 상태 및 루프 시스템을 총괄 관리하는 매니저 클래스입니다.
- **상속 및 구현 관계**: `NativeRoutine` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _createMap` (CreateMap): MapManager의 내부 데이터 또는 의존 참조 변수
  - `[private] _mapRandering` (MapRandering): MapManager의 내부 데이터 또는 의존 참조 변수
  - `[private] _waveSpawner` (WaveSpawner): MapManager의 내부 데이터 또는 의존 참조 변수
  - `[public] mapRandering` (MapRandering): MapManager의 내부 데이터 또는 의존 참조 변수
  - `[public] waveSpawner` (WaveSpawner): MapManager의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Construct(CreateMap createMap, MapRandering mapRandering, WaveSpawner waveSpawner)` ➔ `void`: Construct 관련 로직 수행 함수
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[public] SetupAndVisualizeMap(CreateMap cmap)` ➔ `void`: SetupAndVisualizeMap 관련 로직 수행 함수

#### 📄 클래스: `MapSaveModel` (class)
- **경로**: `Script/Map/MapSaveModel.cs`
- **클래스 목적 및 의도**: `MapSaveModel`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IDataModel` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] MapSaveModel(MapSerializer.MapDto dto)` ➔ `public`: MapSaveModel 관련 로직 수행 함수

#### 📄 클래스: `MapSerializer` (class)
- **경로**: `Script/Map/MapSerializer.cs`
- **클래스 목적 및 의도**: `MapSerializer`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] dto` (var): MapSerializer의 내부 데이터 또는 의존 참조 변수
  - `[private] dto` (var): MapSerializer의 내부 데이터 또는 의존 참조 변수
  - `[private] dto` (var): MapSerializer의 내부 데이터 또는 의존 참조 변수
  - `[private] dto` (return): MapSerializer의 내부 데이터 또는 의존 참조 변수
  - `[private] floor` (Floor): MapSerializer의 내부 데이터 또는 의존 참조 변수
  - `[private] floorDto` (var): MapSerializer의 내부 데이터 또는 의존 참조 변수
  - `[private] w` (int): MapSerializer의 내부 데이터 또는 의존 참조 변수
  - `[private] h` (int): MapSerializer의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (Chunks): MapSerializer의 내부 데이터 또는 의존 참조 변수
  - `[private] cDto` (var): MapSerializer의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ToJson(Map map, bool prettyPrint = false)` ➔ `string`: ToJson 관련 로직 수행 함수
  - `[public] FromJson(string json)` ➔ `Map`: FromJson 관련 로직 수행 함수
  - `[private] DtoToMap(dto)` ➔ `return`: DtoToMap 관련 로직 수행 함수
  - `[public] MapToDto(Map map)` ➔ `MapDto`: MapToDto 관련 로직 수행 함수
  - `[public] DtoToMap(MapDto dto)` ➔ `Map`: DtoToMap 관련 로직 수행 함수

#### 📄 클래스: `MapDto` (class)
- **경로**: `Script/Map/MapSerializer.cs`
- **클래스 목적 및 의도**: `MapDto`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IData` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] dto` (var): MapDto의 내부 데이터 또는 의존 참조 변수
  - `[private] dto` (var): MapDto의 내부 데이터 또는 의존 참조 변수
  - `[private] dto` (var): MapDto의 내부 데이터 또는 의존 참조 변수
  - `[private] dto` (return): MapDto의 내부 데이터 또는 의존 참조 변수
  - `[private] floor` (Floor): MapDto의 내부 데이터 또는 의존 참조 변수
  - `[private] floorDto` (var): MapDto의 내부 데이터 또는 의존 참조 변수
  - `[private] w` (int): MapDto의 내부 데이터 또는 의존 참조 변수
  - `[private] h` (int): MapDto의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (Chunks): MapDto의 내부 데이터 또는 의존 참조 변수
  - `[private] cDto` (var): MapDto의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ToJson(Map map, bool prettyPrint = false)` ➔ `string`: ToJson 관련 로직 수행 함수
  - `[public] FromJson(string json)` ➔ `Map`: FromJson 관련 로직 수행 함수
  - `[private] DtoToMap(dto)` ➔ `return`: DtoToMap 관련 로직 수행 함수
  - `[public] MapToDto(Map map)` ➔ `MapDto`: MapToDto 관련 로직 수행 함수
  - `[public] DtoToMap(MapDto dto)` ➔ `Map`: DtoToMap 관련 로직 수행 함수

#### 📄 클래스: `FloorDto` (class)
- **경로**: `Script/Map/MapSerializer.cs`
- **클래스 목적 및 의도**: `FloorDto`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] dto` (var): FloorDto의 내부 데이터 또는 의존 참조 변수
  - `[private] dto` (var): FloorDto의 내부 데이터 또는 의존 참조 변수
  - `[private] dto` (var): FloorDto의 내부 데이터 또는 의존 참조 변수
  - `[private] dto` (return): FloorDto의 내부 데이터 또는 의존 참조 변수
  - `[private] floor` (Floor): FloorDto의 내부 데이터 또는 의존 참조 변수
  - `[private] floorDto` (var): FloorDto의 내부 데이터 또는 의존 참조 변수
  - `[private] w` (int): FloorDto의 내부 데이터 또는 의존 참조 변수
  - `[private] h` (int): FloorDto의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (Chunks): FloorDto의 내부 데이터 또는 의존 참조 변수
  - `[private] cDto` (var): FloorDto의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ToJson(Map map, bool prettyPrint = false)` ➔ `string`: ToJson 관련 로직 수행 함수
  - `[public] FromJson(string json)` ➔ `Map`: FromJson 관련 로직 수행 함수
  - `[private] DtoToMap(dto)` ➔ `return`: DtoToMap 관련 로직 수행 함수
  - `[public] MapToDto(Map map)` ➔ `MapDto`: MapToDto 관련 로직 수행 함수
  - `[public] DtoToMap(MapDto dto)` ➔ `Map`: DtoToMap 관련 로직 수행 함수

#### 📄 클래스: `ChunksDto` (class)
- **경로**: `Script/Map/MapSerializer.cs`
- **클래스 목적 및 의도**: `ChunksDto`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] dto` (var): ChunksDto의 내부 데이터 또는 의존 참조 변수
  - `[private] dto` (var): ChunksDto의 내부 데이터 또는 의존 참조 변수
  - `[private] dto` (var): ChunksDto의 내부 데이터 또는 의존 참조 변수
  - `[private] dto` (return): ChunksDto의 내부 데이터 또는 의존 참조 변수
  - `[private] floor` (Floor): ChunksDto의 내부 데이터 또는 의존 참조 변수
  - `[private] floorDto` (var): ChunksDto의 내부 데이터 또는 의존 참조 변수
  - `[private] w` (int): ChunksDto의 내부 데이터 또는 의존 참조 변수
  - `[private] h` (int): ChunksDto의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (Chunks): ChunksDto의 내부 데이터 또는 의존 참조 변수
  - `[private] cDto` (var): ChunksDto의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ToJson(Map map, bool prettyPrint = false)` ➔ `string`: ToJson 관련 로직 수행 함수
  - `[public] FromJson(string json)` ➔ `Map`: FromJson 관련 로직 수행 함수
  - `[private] DtoToMap(dto)` ➔ `return`: DtoToMap 관련 로직 수행 함수
  - `[public] MapToDto(Map map)` ➔ `MapDto`: MapToDto 관련 로직 수행 함수
  - `[public] DtoToMap(MapDto dto)` ➔ `Map`: DtoToMap 관련 로직 수행 함수

---

### 📦 Package: `Plugins/Demigiant/DOTween/Modules`

**모듈 내 포함 스크립트 수**: 16개

#### 📄 클래스: `DOTweenModuleAudio` (class)
- **경로**: `Plugins/Demigiant/DOTween/Modules/DOTweenModuleAudio.cs`
- **클래스 목적 및 의도**: `DOTweenModuleAudio`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] t` (return): DOTweenModuleAudio의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): DOTweenModuleAudio의 내부 데이터 또는 의존 참조 변수
  - `[private] currVal` (float): DOTweenModuleAudio의 내부 데이터 또는 의존 참조 변수
  - `[private] currVal` (return): DOTweenModuleAudio의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): DOTweenModuleAudio의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] DOComplete(this AudioMixer target, bool withCallbacks = false)` ➔ `int`: DOComplete 관련 로직 수행 함수
  - `[public] DOKill(this AudioMixer target, bool complete = false)` ➔ `int`: DOKill 관련 로직 수행 함수
  - `[public] DOFlip(this AudioMixer target)` ➔ `int`: DOFlip 관련 로직 수행 함수
  - `[public] DOGoto(this AudioMixer target, float to, bool andPlay = false)` ➔ `int`: DOGoto 관련 로직 수행 함수
  - `[public] DOPause(this AudioMixer target)` ➔ `int`: DOPause 관련 로직 수행 함수
  - `[public] DOPlay(this AudioMixer target)` ➔ `int`: DOPlay 관련 로직 수행 함수
  - `[public] DOPlayBackwards(this AudioMixer target)` ➔ `int`: DOPlayBackwards 관련 로직 수행 함수
  - `[public] DOPlayForward(this AudioMixer target)` ➔ `int`: DOPlayForward 관련 로직 수행 함수
  - `[public] DORestart(this AudioMixer target)` ➔ `int`: DORestart 관련 로직 수행 함수
  - `[public] DORewind(this AudioMixer target)` ➔ `int`: DORewind 관련 로직 수행 함수

#### 📄 클래스: `DOTweenModulePhysics` (class)
- **경로**: `Plugins/Demigiant/DOTween/Modules/DOTweenModulePhysics.cs`
- **클래스 목적 및 의도**: `DOTweenModulePhysics`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] t` (return): DOTweenModulePhysics의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): DOTweenModulePhysics의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): DOTweenModulePhysics의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): DOTweenModulePhysics의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): DOTweenModulePhysics의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] DOJump(this Rigidbody target, Vector3 endValue, float jumpPower, int numJumps, float duration, bool snapping = false)` ➔ `Sequence`: DOJump 관련 로직 수행 함수

#### 📄 클래스: `DOTweenModulePhysics2D` (class)
- **경로**: `Plugins/Demigiant/DOTween/Modules/DOTweenModulePhysics2D.cs`
- **클래스 목적 및 의도**: `DOTweenModulePhysics2D`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] t` (return): DOTweenModulePhysics2D의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): DOTweenModulePhysics2D의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): DOTweenModulePhysics2D의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): DOTweenModulePhysics2D의 내부 데이터 또는 의존 참조 변수
  - `[private] startPosY` (float): DOTweenModulePhysics2D의 내부 데이터 또는 의존 참조 변수
  - `[private] offsetY` (float): DOTweenModulePhysics2D의 내부 데이터 또는 의존 참조 변수
  - `[private] offsetYSet` (bool): DOTweenModulePhysics2D의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (Sequence): DOTweenModulePhysics2D의 내부 데이터 또는 의존 참조 변수
  - `[private] yTween` (Tween): DOTweenModulePhysics2D의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] DOJump(this Rigidbody2D target, Vector2 endValue, float jumpPower, int numJumps, float duration, bool snapping = false)` ➔ `Sequence`: DOJump 관련 로직 수행 함수

#### 📄 클래스: `DOTweenModuleSprite` (class)
- **경로**: `Plugins/Demigiant/DOTween/Modules/DOTweenModuleSprite.cs`
- **클래스 목적 및 의도**: `DOTweenModuleSprite`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] t` (return): DOTweenModuleSprite의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): DOTweenModuleSprite의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (Sequence): DOTweenModuleSprite의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): DOTweenModuleSprite의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): DOTweenModuleSprite의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): DOTweenModuleSprite의 내부 데이터 또는 의존 참조 변수
  - `[private] colorDuration` (float): DOTweenModuleSprite의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (return): DOTweenModuleSprite의 내부 데이터 또는 의존 참조 변수
  - `[private] to` (Color): DOTweenModuleSprite의 내부 데이터 또는 의존 참조 변수
  - `[private] diff` (Color): DOTweenModuleSprite의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] DOGradientColor(this SpriteRenderer target, Gradient gradient, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수
  - `[public] DOBlendableColor(this SpriteRenderer target, Color endValue, float duration)` ➔ `Tweener`: DOBlendableColor 관련 로직 수행 함수

#### 📄 클래스: `DOTweenModuleUI` (class)
- **경로**: `Plugins/Demigiant/DOTween/Modules/DOTweenModuleUI.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `DOTweenModuleUI` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] t` (return): DOTweenModuleUI의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): DOTweenModuleUI의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): DOTweenModuleUI의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): DOTweenModuleUI의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): DOTweenModuleUI의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): DOTweenModuleUI의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (Sequence): DOTweenModuleUI의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): DOTweenModuleUI의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): DOTweenModuleUI의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): DOTweenModuleUI의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] DOGradientColor(this Image target, Gradient gradient, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수

#### 📄 클래스: `Utils` (class)
- **경로**: `Plugins/Demigiant/DOTween/Modules/DOTweenModuleUI.cs`
- **클래스 목적 및 의도**: `Utils`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] t` (return): Utils의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): Utils의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): Utils의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): Utils의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): Utils의 내부 데이터 또는 의존 참조 변수
  - `[private] t` (return): Utils의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (Sequence): Utils의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): Utils의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): Utils의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): Utils의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] DOGradientColor(this Image target, Gradient gradient, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수

#### 📄 클래스: `DOTweenModuleUnityVersion` (class)
- **경로**: `Plugins/Demigiant/DOTween/Modules/DOTweenModuleUnityVersion.cs`
- **클래스 목적 및 의도**: `DOTweenModuleUnityVersion`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] s` (Sequence): DOTweenModuleUnityVersion의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): DOTweenModuleUnityVersion의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): DOTweenModuleUnityVersion의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): DOTweenModuleUnityVersion의 내부 데이터 또는 의존 참조 변수
  - `[private] colorDuration` (float): DOTweenModuleUnityVersion의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (return): DOTweenModuleUnityVersion의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (Sequence): DOTweenModuleUnityVersion의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): DOTweenModuleUnityVersion의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): DOTweenModuleUnityVersion의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): DOTweenModuleUnityVersion의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] DOGradientColor(this Material target, Gradient gradient, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수
  - `[public] DOGradientColor(this Material target, Gradient gradient, string property, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수
  - `[public] WaitForCompletion(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForCompletion 관련 로직 수행 함수
  - `[public] WaitForRewind(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForRewind 관련 로직 수행 함수
  - `[public] WaitForKill(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForKill 관련 로직 수행 함수
  - `[public] WaitForElapsedLoops(this Tween t, int elapsedLoops, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForElapsedLoops 관련 로직 수행 함수
  - `[public] WaitForPosition(this Tween t, float position, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForPosition 관련 로직 수행 함수
  - `[public] WaitForStart(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForStart 관련 로직 수행 함수

#### 📄 클래스: `DOTweenCYInstruction` (class)
- **경로**: `Plugins/Demigiant/DOTween/Modules/DOTweenModuleUnityVersion.cs`
- **클래스 목적 및 의도**: `DOTweenCYInstruction`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] s` (Sequence): DOTweenCYInstruction의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): DOTweenCYInstruction의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): DOTweenCYInstruction의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): DOTweenCYInstruction의 내부 데이터 또는 의존 참조 변수
  - `[private] colorDuration` (float): DOTweenCYInstruction의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (return): DOTweenCYInstruction의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (Sequence): DOTweenCYInstruction의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): DOTweenCYInstruction의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): DOTweenCYInstruction의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): DOTweenCYInstruction의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] DOGradientColor(this Material target, Gradient gradient, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수
  - `[public] DOGradientColor(this Material target, Gradient gradient, string property, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수
  - `[public] WaitForCompletion(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForCompletion 관련 로직 수행 함수
  - `[public] WaitForRewind(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForRewind 관련 로직 수행 함수
  - `[public] WaitForKill(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForKill 관련 로직 수행 함수
  - `[public] WaitForElapsedLoops(this Tween t, int elapsedLoops, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForElapsedLoops 관련 로직 수행 함수
  - `[public] WaitForPosition(this Tween t, float position, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForPosition 관련 로직 수행 함수
  - `[public] WaitForStart(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForStart 관련 로직 수행 함수

#### 📄 클래스: `WaitForCompletion` (class)
- **경로**: `Plugins/Demigiant/DOTween/Modules/DOTweenModuleUnityVersion.cs`
- **클래스 목적 및 의도**: `WaitForCompletion`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `CustomYieldInstruction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] s` (Sequence): WaitForCompletion의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): WaitForCompletion의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): WaitForCompletion의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): WaitForCompletion의 내부 데이터 또는 의존 참조 변수
  - `[private] colorDuration` (float): WaitForCompletion의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (return): WaitForCompletion의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (Sequence): WaitForCompletion의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): WaitForCompletion의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): WaitForCompletion의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): WaitForCompletion의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] DOGradientColor(this Material target, Gradient gradient, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수
  - `[public] DOGradientColor(this Material target, Gradient gradient, string property, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수
  - `[public] WaitForCompletion(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForCompletion 관련 로직 수행 함수
  - `[public] WaitForRewind(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForRewind 관련 로직 수행 함수
  - `[public] WaitForKill(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForKill 관련 로직 수행 함수
  - `[public] WaitForElapsedLoops(this Tween t, int elapsedLoops, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForElapsedLoops 관련 로직 수행 함수
  - `[public] WaitForPosition(this Tween t, float position, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForPosition 관련 로직 수행 함수
  - `[public] WaitForStart(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForStart 관련 로직 수행 함수

#### 📄 클래스: `WaitForRewind` (class)
- **경로**: `Plugins/Demigiant/DOTween/Modules/DOTweenModuleUnityVersion.cs`
- **클래스 목적 및 의도**: `WaitForRewind`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `CustomYieldInstruction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] s` (Sequence): WaitForRewind의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): WaitForRewind의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): WaitForRewind의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): WaitForRewind의 내부 데이터 또는 의존 참조 변수
  - `[private] colorDuration` (float): WaitForRewind의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (return): WaitForRewind의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (Sequence): WaitForRewind의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): WaitForRewind의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): WaitForRewind의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): WaitForRewind의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] DOGradientColor(this Material target, Gradient gradient, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수
  - `[public] DOGradientColor(this Material target, Gradient gradient, string property, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수
  - `[public] WaitForCompletion(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForCompletion 관련 로직 수행 함수
  - `[public] WaitForRewind(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForRewind 관련 로직 수행 함수
  - `[public] WaitForKill(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForKill 관련 로직 수행 함수
  - `[public] WaitForElapsedLoops(this Tween t, int elapsedLoops, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForElapsedLoops 관련 로직 수행 함수
  - `[public] WaitForPosition(this Tween t, float position, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForPosition 관련 로직 수행 함수
  - `[public] WaitForStart(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForStart 관련 로직 수행 함수

#### 📄 클래스: `WaitForKill` (class)
- **경로**: `Plugins/Demigiant/DOTween/Modules/DOTweenModuleUnityVersion.cs`
- **클래스 목적 및 의도**: `WaitForKill`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `CustomYieldInstruction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] s` (Sequence): WaitForKill의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): WaitForKill의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): WaitForKill의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): WaitForKill의 내부 데이터 또는 의존 참조 변수
  - `[private] colorDuration` (float): WaitForKill의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (return): WaitForKill의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (Sequence): WaitForKill의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): WaitForKill의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): WaitForKill의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): WaitForKill의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] DOGradientColor(this Material target, Gradient gradient, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수
  - `[public] DOGradientColor(this Material target, Gradient gradient, string property, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수
  - `[public] WaitForCompletion(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForCompletion 관련 로직 수행 함수
  - `[public] WaitForRewind(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForRewind 관련 로직 수행 함수
  - `[public] WaitForKill(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForKill 관련 로직 수행 함수
  - `[public] WaitForElapsedLoops(this Tween t, int elapsedLoops, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForElapsedLoops 관련 로직 수행 함수
  - `[public] WaitForPosition(this Tween t, float position, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForPosition 관련 로직 수행 함수
  - `[public] WaitForStart(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForStart 관련 로직 수행 함수

#### 📄 클래스: `WaitForElapsedLoops` (class)
- **경로**: `Plugins/Demigiant/DOTween/Modules/DOTweenModuleUnityVersion.cs`
- **클래스 목적 및 의도**: `WaitForElapsedLoops`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `CustomYieldInstruction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] s` (Sequence): WaitForElapsedLoops의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): WaitForElapsedLoops의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): WaitForElapsedLoops의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): WaitForElapsedLoops의 내부 데이터 또는 의존 참조 변수
  - `[private] colorDuration` (float): WaitForElapsedLoops의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (return): WaitForElapsedLoops의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (Sequence): WaitForElapsedLoops의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): WaitForElapsedLoops의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): WaitForElapsedLoops의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): WaitForElapsedLoops의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] DOGradientColor(this Material target, Gradient gradient, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수
  - `[public] DOGradientColor(this Material target, Gradient gradient, string property, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수
  - `[public] WaitForCompletion(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForCompletion 관련 로직 수행 함수
  - `[public] WaitForRewind(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForRewind 관련 로직 수행 함수
  - `[public] WaitForKill(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForKill 관련 로직 수행 함수
  - `[public] WaitForElapsedLoops(this Tween t, int elapsedLoops, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForElapsedLoops 관련 로직 수행 함수
  - `[public] WaitForPosition(this Tween t, float position, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForPosition 관련 로직 수행 함수
  - `[public] WaitForStart(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForStart 관련 로직 수행 함수

#### 📄 클래스: `WaitForPosition` (class)
- **경로**: `Plugins/Demigiant/DOTween/Modules/DOTweenModuleUnityVersion.cs`
- **클래스 목적 및 의도**: `WaitForPosition`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `CustomYieldInstruction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] s` (Sequence): WaitForPosition의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): WaitForPosition의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): WaitForPosition의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): WaitForPosition의 내부 데이터 또는 의존 참조 변수
  - `[private] colorDuration` (float): WaitForPosition의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (return): WaitForPosition의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (Sequence): WaitForPosition의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): WaitForPosition의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): WaitForPosition의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): WaitForPosition의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] DOGradientColor(this Material target, Gradient gradient, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수
  - `[public] DOGradientColor(this Material target, Gradient gradient, string property, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수
  - `[public] WaitForCompletion(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForCompletion 관련 로직 수행 함수
  - `[public] WaitForRewind(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForRewind 관련 로직 수행 함수
  - `[public] WaitForKill(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForKill 관련 로직 수행 함수
  - `[public] WaitForElapsedLoops(this Tween t, int elapsedLoops, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForElapsedLoops 관련 로직 수행 함수
  - `[public] WaitForPosition(this Tween t, float position, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForPosition 관련 로직 수행 함수
  - `[public] WaitForStart(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForStart 관련 로직 수행 함수

#### 📄 클래스: `WaitForStart` (class)
- **경로**: `Plugins/Demigiant/DOTween/Modules/DOTweenModuleUnityVersion.cs`
- **클래스 목적 및 의도**: `WaitForStart`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `CustomYieldInstruction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] s` (Sequence): WaitForStart의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): WaitForStart의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): WaitForStart의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): WaitForStart의 내부 데이터 또는 의존 참조 변수
  - `[private] colorDuration` (float): WaitForStart의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (return): WaitForStart의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (Sequence): WaitForStart의 내부 데이터 또는 의존 참조 변수
  - `[private] colors` (GradientColorKey[]): WaitForStart의 내부 데이터 또는 의존 참조 변수
  - `[private] len` (int): WaitForStart의 내부 데이터 또는 의존 참조 변수
  - `[private] c` (GradientColorKey): WaitForStart의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] DOGradientColor(this Material target, Gradient gradient, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수
  - `[public] DOGradientColor(this Material target, Gradient gradient, string property, float duration)` ➔ `Sequence`: DOGradientColor 관련 로직 수행 함수
  - `[public] WaitForCompletion(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForCompletion 관련 로직 수행 함수
  - `[public] WaitForRewind(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForRewind 관련 로직 수행 함수
  - `[public] WaitForKill(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForKill 관련 로직 수행 함수
  - `[public] WaitForElapsedLoops(this Tween t, int elapsedLoops, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForElapsedLoops 관련 로직 수행 함수
  - `[public] WaitForPosition(this Tween t, float position, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForPosition 관련 로직 수행 함수
  - `[public] WaitForStart(this Tween t, bool returnCustomYieldInstruction)` ➔ `CustomYieldInstruction`: WaitForStart 관련 로직 수행 함수

#### 📄 클래스: `DOTweenModuleUtils` (class)
- **경로**: `Plugins/Demigiant/DOTween/Modules/DOTweenModuleUtils.cs`
- **클래스 목적 및 의도**: `DOTweenModuleUtils`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] _initialized` (bool): DOTweenModuleUtils의 내부 데이터 또는 의존 참조 변수
  - `[private] loadedAssemblies` (Assembly[]): DOTweenModuleUtils의 내부 데이터 또는 의존 참조 변수
  - `[private] mi` (MethodInfo): DOTweenModuleUtils의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): DOTweenModuleUtils의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): DOTweenModuleUtils의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Init()` ➔ `void`: Init 관련 로직 수행 함수
  - `[private] Preserver()` ➔ `void`: Preserver 관련 로직 수행 함수
  - `[public] SetOrientationOnPath(PathOptions options, Tween t, Quaternion newRot, Transform trans)` ➔ `void`: SetOrientationOnPath 관련 로직 수행 함수
  - `[public] HasRigidbody2D(Component target)` ➔ `bool`: HasRigidbody2D 관련 로직 수행 함수
  - `[public] HasRigidbody(Component target)` ➔ `bool`: HasRigidbody 관련 로직 수행 함수

#### 📄 클래스: `Physics` (class)
- **경로**: `Plugins/Demigiant/DOTween/Modules/DOTweenModuleUtils.cs`
- **클래스 목적 및 의도**: `Physics`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] _initialized` (bool): Physics의 내부 데이터 또는 의존 참조 변수
  - `[private] loadedAssemblies` (Assembly[]): Physics의 내부 데이터 또는 의존 참조 변수
  - `[private] mi` (MethodInfo): Physics의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): Physics의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): Physics의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Init()` ➔ `void`: Init 관련 로직 수행 함수
  - `[private] Preserver()` ➔ `void`: Preserver 관련 로직 수행 함수
  - `[public] SetOrientationOnPath(PathOptions options, Tween t, Quaternion newRot, Transform trans)` ➔ `void`: SetOrientationOnPath 관련 로직 수행 함수
  - `[public] HasRigidbody2D(Component target)` ➔ `bool`: HasRigidbody2D 관련 로직 수행 함수
  - `[public] HasRigidbody(Component target)` ➔ `bool`: HasRigidbody 관련 로직 수행 함수

---

### 📦 Package: `Production`

**모듈 내 포함 스크립트 수**: 4개

#### 📄 클래스: `ResourceCost` (class)
- **경로**: `Script/Production/ProductionRule.cs`
- **클래스 목적 및 의도**: `ResourceCost`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] resourceType` (ResourceType): ResourceCost의 내부 데이터 또는 의존 참조 변수
  - `[public] amount` (int): ResourceCost의 내부 데이터 또는 의존 참조 변수
  - `[public] ruleId` (string): ResourceCost의 내부 데이터 또는 의존 참조 변수
  - `[public] displayName` (string): ResourceCost의 내부 데이터 또는 의존 참조 변수
  - `[public] costs` (List<ResourceCost>): ResourceCost의 내부 데이터 또는 의존 참조 변수
  - `[public] productionTime` (float): ResourceCost의 내부 데이터 또는 의존 참조 변수
  - `[public] targetUnitTypeName` (string): ResourceCost의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `ProductionRule` (class)
- **경로**: `Script/Production/ProductionRule.cs`
- **클래스 목적 및 의도**: `ProductionRule`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `ScriptableObject` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] resourceType` (ResourceType): ProductionRule의 내부 데이터 또는 의존 참조 변수
  - `[public] amount` (int): ProductionRule의 내부 데이터 또는 의존 참조 변수
  - `[public] ruleId` (string): ProductionRule의 내부 데이터 또는 의존 참조 변수
  - `[public] displayName` (string): ProductionRule의 내부 데이터 또는 의존 참조 변수
  - `[public] costs` (List<ResourceCost>): ProductionRule의 내부 데이터 또는 의존 참조 변수
  - `[public] productionTime` (float): ProductionRule의 내부 데이터 또는 의존 참조 변수
  - `[public] targetUnitTypeName` (string): ProductionRule의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `ResourceType` (enum)
- **경로**: `Script/Production/ResourceManager.cs`
- **클래스 목적 및 의도**: `ResourceType`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] MonsterPlaceWoodCost` (int): ResourceType의 내부 데이터 또는 의존 참조 변수
  - `[public] TrapPlaceStoneCost` (int): ResourceType의 내부 데이터 또는 의존 참조 변수
  - `[private] true` (return): ResourceType의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): ResourceType의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): ResourceType의 내부 데이터 또는 의존 참조 변수
  - `[private] true` (return): ResourceType의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[public] Finalize()` ➔ `UniTask`: Finalize 관련 로직 수행 함수
  - `[public] AddResource(ResourceType type, int amount)` ➔ `void`: AddResource 관련 로직 수행 함수
  - `[public] TryConsumeResource(ResourceType type, int amount)` ➔ `bool`: TryConsumeResource 관련 로직 수행 함수
  - `[public] TryConsumeResources(List<ResourceCost> costs)` ➔ `bool`: TryConsumeResources 관련 로직 수행 함수
  - `[public] HasEnoughResource(ResourceType type, int amount)` ➔ `bool`: HasEnoughResource 관련 로직 수행 함수
  - `[public] GetResourceAmount(ResourceType type)` ➔ `int`: GetResourceAmount 관련 로직 수행 함수

#### 📄 클래스: `ResourceManager` (class)
- **경로**: `Script/Production/ResourceManager.cs`
- **클래스 목적 및 의도**: 게임 내 `Resource` 관련 전역 상태 및 루프 시스템을 총괄 관리하는 매니저 클래스입니다.
- **상속 및 구현 관계**: `NativeRoutine` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] MonsterPlaceWoodCost` (int): ResourceManager의 내부 데이터 또는 의존 참조 변수
  - `[public] TrapPlaceStoneCost` (int): ResourceManager의 내부 데이터 또는 의존 참조 변수
  - `[private] true` (return): ResourceManager의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): ResourceManager의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): ResourceManager의 내부 데이터 또는 의존 참조 변수
  - `[private] true` (return): ResourceManager의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[public] Finalize()` ➔ `UniTask`: Finalize 관련 로직 수행 함수
  - `[public] AddResource(ResourceType type, int amount)` ➔ `void`: AddResource 관련 로직 수행 함수
  - `[public] TryConsumeResource(ResourceType type, int amount)` ➔ `bool`: TryConsumeResource 관련 로직 수행 함수
  - `[public] TryConsumeResources(List<ResourceCost> costs)` ➔ `bool`: TryConsumeResources 관련 로직 수행 함수
  - `[public] HasEnoughResource(ResourceType type, int amount)` ➔ `bool`: HasEnoughResource 관련 로직 수행 함수
  - `[public] GetResourceAmount(ResourceType type)` ➔ `int`: GetResourceAmount 관련 로직 수행 함수

---

### 📦 Package: `Randering`

**모듈 내 포함 스크립트 수**: 2개

#### 📄 클래스: `IMapColorizer` (interface)
- **경로**: `Script/Randering/IMapColorizer.cs`
- **클래스 목적 및 의도**: `IMapColorizer`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] ChangeRoomColor(Room room, Color color)` ➔ `void`: ChangeRoomColor 관련 로직 수행 함수

#### 📄 클래스: `MapRandering` (class)
- **경로**: `Script/Randering/MapRandering.cs`
- **클래스 목적 및 의도**: `MapRandering`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `NativeRoutine, IMapColorizer` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] createMap` (CreateMap): MapRandering의 내부 데이터 또는 의존 참조 변수
  - `[private] wallSprite` (Sprite): MapRandering의 내부 데이터 또는 의존 참조 변수
  - `[private] floorSprite` (Sprite): MapRandering의 내부 데이터 또는 의존 참조 변수
  - `[private] stairSprite` (Sprite): MapRandering의 내부 데이터 또는 의존 참조 변수
  - `[private] stairDownSprite` (Sprite): MapRandering의 내부 데이터 또는 의존 참조 변수
  - `[private] stairUpSprite` (Sprite): MapRandering의 내부 데이터 또는 의존 참조 변수
  - `[private] ChunkSize` (int): MapRandering의 내부 데이터 또는 의존 참조 변수
  - `[private] mapRoot` (GameObject): MapRandering의 내부 데이터 또는 의존 참조 변수
  - `[private] tex` (Texture2D): MapRandering의 내부 데이터 또는 의존 참조 변수
  - `[private] pixels` (Color[]): MapRandering의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Construct(CreateMap createMap)` ➔ `void`: Construct 관련 로직 수행 함수
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[public] DoRandering(CreateMap targetMap = null)` ➔ `void`: DoRandering 관련 로직 수행 함수
  - `[private] BuildTileCache()` ➔ `void`: BuildTileCache 관련 로직 수행 함수
  - `[private] CreateColorSprite(Color color)` ➔ `Sprite`: CreateColorSprite 관련 로직 수행 함수
  - `[public] RenderAllFloors()` ➔ `void`: RenderAllFloors 관련 로직 수행 함수
  - `[private] RenderFloor(Tilemap tilemap, ref Floor floor, int floorIdx)` ➔ `void`: RenderFloor 관련 로직 수행 함수

---

### 📦 Package: `Tests`

**모듈 내 포함 스크립트 수**: 7개

#### 📄 클래스: `CreateMapPlayTests` (class)
- **경로**: `Tests/CreateMapPlayTests.cs`
- **클래스 목적 및 의도**: `CreateMapPlayTests` 로직의 동작 검증을 위한 플레이/단위 테스트 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] cm` (var): CreateMapPlayTests의 내부 데이터 또는 의존 참조 변수
  - `[private] cm` (return): CreateMapPlayTests의 내부 데이터 또는 의존 참조 변수
  - `[private] mr` (var): CreateMapPlayTests의 내부 데이터 또는 의존 참조 변수
  - `[private] mr` (return): CreateMapPlayTests의 내부 데이터 또는 의존 참조 변수
  - `[private] mapRoot` (var): CreateMapPlayTests의 내부 데이터 또는 의존 참조 변수
  - `[private] cm` (var): CreateMapPlayTests의 내부 데이터 또는 의존 참조 변수
  - `[private] cm` (var): CreateMapPlayTests의 내부 데이터 또는 의존 참조 변수
  - `[private] floor` (Floor): CreateMapPlayTests의 내부 데이터 또는 의존 참조 변수
  - `[private] w` (int): CreateMapPlayTests의 내부 데이터 또는 의존 참조 변수
  - `[private] h` (int): CreateMapPlayTests의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] SetupCreateMap(int seed = 14056)` ➔ `CreateMap`: SetupCreateMap 관련 로직 수행 함수
  - `[private] SetupRenderer(CreateMap cm)` ➔ `MapRandering`: SetupRenderer 관련 로직 수행 함수
  - `[private] Cleanup()` ➔ `void`: Cleanup 관련 로직 수행 함수
  - `[public] InitMap_CreatesFloorArrays()` ➔ `void`: InitMap_CreatesFloorArrays 관련 로직 수행 함수
  - `[public] AllChunks_Have8x8TileArray()` ➔ `void`: AllChunks_Have8x8TileArray 관련 로직 수행 함수
  - `[public] StartRoom_ExistsOnEachFloor()` ➔ `void`: StartRoom_ExistsOnEachFloor 관련 로직 수행 함수
  - `[public] TileNames_EdgeIsWall_InteriorIsFloor()` ➔ `void`: TileNames_EdgeIsWall_InteriorIsFloor 관련 로직 수행 함수
  - `[public] InternalWalls_OpenedBetweenSameRoomChunks()` ➔ `void`: InternalWalls_OpenedBetweenSameRoomChunks 관련 로직 수행 함수

#### 📄 클래스: `ExplorationSystemTests` (class)
- **경로**: `Tests/ExplorationSystemTests.cs`
- **클래스 목적 및 의도**: `ExplorationTests` 도메인의 핵심 비즈니스 로직 연산을 수행하는 핵심 시스템입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] low` (float): ExplorationSystemTests의 내부 데이터 또는 의존 참조 변수
  - `[private] higherConcentration` (float): ExplorationSystemTests의 내부 데이터 또는 의존 참조 변수
  - `[private] higherLevel` (float): ExplorationSystemTests의 내부 데이터 또는 의존 참조 변수
  - `[private] higherUnderstanding` (float): ExplorationSystemTests의 내부 데이터 또는 의존 참조 변수
  - `[private] max` (float): ExplorationSystemTests의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] TrapDisarmSuccessRate_MonotonicAndClamped()` ➔ `void`: TrapDisarmSuccessRate_MonotonicAndClamped 관련 로직 수행 함수
  - `[public] TrapRecordedDirectDisarmThreshold_IsFiftyPercent()` ➔ `void`: TrapRecordedDirectDisarmThreshold_IsFiftyPercent 관련 로직 수행 함수
  - `[public] TrapExpectedRateErrorMargin_ShrinksWithUnderstanding()` ➔ `void`: TrapExpectedRateErrorMargin_ShrinksWithUnderstanding 관련 로직 수행 함수
  - `[public] Parameters_MatchDocumentTable()` ➔ `void`: Parameters_MatchDocumentTable 관련 로직 수행 함수

#### 📄 클래스: `GoapPlannerTests` (class)
- **경로**: `Tests/GoapPlannerTests.cs`
- **클래스 목적 및 의도**: `GoapPlannerTests` 로직의 동작 검증을 위한 플레이/단위 테스트 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] _dummyUnit` (Human): GoapPlannerTests의 내부 데이터 또는 의존 참조 변수
  - `[private] moveToDoor` (var): GoapPlannerTests의 내부 데이터 또는 의존 참조 변수
  - `[private] openDoor` (var): GoapPlannerTests의 내부 데이터 또는 의존 참조 변수
  - `[private] start` (var): GoapPlannerTests의 내부 데이터 또는 의존 참조 변수
  - `[private] desired` (var): GoapPlannerTests의 내부 데이터 또는 의존 참조 변수
  - `[private] plan` (List<GoapAction>): GoapPlannerTests의 내부 데이터 또는 의존 참조 변수
  - `[private] openDoor` (var): GoapPlannerTests의 내부 데이터 또는 의존 참조 변수
  - `[private] start` (var): GoapPlannerTests의 내부 데이터 또는 의존 참조 변수
  - `[private] desired` (var): GoapPlannerTests의 내부 데이터 또는 의존 참조 변수
  - `[private] plan` (List<GoapAction>): GoapPlannerTests의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] FakeAction(string name, float cost)` ➔ `public`: FakeAction 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[public] SetUp()` ➔ `void`: SetUp 관련 로직 수행 함수
  - `[public] TearDown()` ➔ `void`: TearDown 관련 로직 수행 함수
  - `[public] Plan_ChainsTwoActionsWhenFirstEffectUnlocksSecondPrecondition()` ➔ `void`: Plan_ChainsTwoActionsWhenFirstEffectUnlocksSecondPrecondition 관련 로직 수행 함수
  - `[public] Plan_ReturnsNullWhenGoalUnreachable()` ➔ `void`: Plan_ReturnsNullWhenGoalUnreachable 관련 로직 수행 함수
  - `[public] Plan_PicksCheapestPathWhenMultipleSingleStepOptionsExist()` ➔ `void`: Plan_PicksCheapestPathWhenMultipleSingleStepOptionsExist 관련 로직 수행 함수
  - `[public] Plan_ReturnsEmptyWhenAlreadySatisfied()` ➔ `void`: Plan_ReturnsEmptyWhenAlreadySatisfied 관련 로직 수행 함수

#### 📄 클래스: `FakeAction` (class)
- **경로**: `Tests/GoapPlannerTests.cs`
- **클래스 목적 및 의도**: `FakeAction`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _dummyUnit` (Human): FakeAction의 내부 데이터 또는 의존 참조 변수
  - `[private] moveToDoor` (var): FakeAction의 내부 데이터 또는 의존 참조 변수
  - `[private] openDoor` (var): FakeAction의 내부 데이터 또는 의존 참조 변수
  - `[private] start` (var): FakeAction의 내부 데이터 또는 의존 참조 변수
  - `[private] desired` (var): FakeAction의 내부 데이터 또는 의존 참조 변수
  - `[private] plan` (List<GoapAction>): FakeAction의 내부 데이터 또는 의존 참조 변수
  - `[private] openDoor` (var): FakeAction의 내부 데이터 또는 의존 참조 변수
  - `[private] start` (var): FakeAction의 내부 데이터 또는 의존 참조 변수
  - `[private] desired` (var): FakeAction의 내부 데이터 또는 의존 참조 변수
  - `[private] plan` (List<GoapAction>): FakeAction의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] FakeAction(string name, float cost)` ➔ `public`: FakeAction 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[public] SetUp()` ➔ `void`: SetUp 관련 로직 수행 함수
  - `[public] TearDown()` ➔ `void`: TearDown 관련 로직 수행 함수
  - `[public] Plan_ChainsTwoActionsWhenFirstEffectUnlocksSecondPrecondition()` ➔ `void`: Plan_ChainsTwoActionsWhenFirstEffectUnlocksSecondPrecondition 관련 로직 수행 함수
  - `[public] Plan_ReturnsNullWhenGoalUnreachable()` ➔ `void`: Plan_ReturnsNullWhenGoalUnreachable 관련 로직 수행 함수
  - `[public] Plan_PicksCheapestPathWhenMultipleSingleStepOptionsExist()` ➔ `void`: Plan_PicksCheapestPathWhenMultipleSingleStepOptionsExist 관련 로직 수행 함수
  - `[public] Plan_ReturnsEmptyWhenAlreadySatisfied()` ➔ `void`: Plan_ReturnsEmptyWhenAlreadySatisfied 관련 로직 수행 함수

#### 📄 클래스: `PerceptionSystemTests` (class)
- **경로**: `Tests/PerceptionSystemTests.cs`
- **클래스 목적 및 의도**: `PerceptionTests` 도메인의 핵심 비즈니스 로직 연산을 수행하는 핵심 시스템입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[public] DetectionCorrection_AlertAddsTwenty()` ➔ `void`: DetectionCorrection_AlertAddsTwenty 관련 로직 수행 함수
  - `[public] GetMentalTier_Boundaries()` ➔ `void`: GetMentalTier_Boundaries 관련 로직 수행 함수
  - `[public] MentalVisibilityCorrection_Table()` ➔ `void`: MentalVisibilityCorrection_Table 관련 로직 수행 함수
  - `[public] MentalCorrectionForHuman_UnsetMaxMentalIsStable()` ➔ `void`: MentalCorrectionForHuman_UnsetMaxMentalIsStable 관련 로직 수행 함수
  - `[public] TotalPerceptionVisibility_ExampleFromDoc()` ➔ `void`: TotalPerceptionVisibility_ExampleFromDoc 관련 로직 수행 함수
  - `[private] AssertProbabilities(float totalVisibility, float expectedAccurate, float expectedSuspicious, float expectedUnrecognized)` ➔ `void`: AssertProbabilities 관련 로직 수행 함수
  - `[public] OutcomeProbabilities_Table()` ➔ `void`: OutcomeProbabilities_Table 관련 로직 수행 함수
  - `[public] RollOutcome_SplitsByRollValue()` ➔ `void`: RollOutcome_SplitsByRollValue 관련 로직 수행 함수
  - `[public] SuspiciousTileTempWeights_AreFive()` ➔ `void`: SuspiciousTileTempWeights_AreFive 관련 로직 수행 함수
  - `[public] ResolveObjectVisibility_CorpseAndWipeoutAreFixedAt100()` ➔ `void`: ResolveObjectVisibility_CorpseAndWipeoutAreFixedAt100 관련 로직 수행 함수

#### 📄 클래스: `VisionSystemTests` (class)
- **경로**: `Tests/VisionSystemTests.cs`
- **클래스 목적 및 의도**: `VisionTests` 도메인의 핵심 비즈니스 로직 연산을 수행하는 핵심 시스템입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ViewDistance_Table()` ➔ `void`: ViewDistance_Table 관련 로직 수행 함수
  - `[public] AwarenessDistance_Table()` ➔ `void`: AwarenessDistance_Table 관련 로직 수행 함수
  - `[public] AwarenessAngle_Table()` ➔ `void`: AwarenessAngle_Table 관련 로직 수행 함수
  - `[public] AwarenessAngle_MaxedOut_MeansFullVisionConeIsPerceptionCone()` ➔ `void`: AwarenessAngle_MaxedOut_MeansFullVisionConeIsPerceptionCone 관련 로직 수행 함수
  - `[public] TempWeightForVisionOnlyTile_NonEmptyIsFive()` ➔ `void`: TempWeightForVisionOnlyTile_NonEmptyIsFive 관련 로직 수행 함수
  - `[public] ResolveBaseVisibility_WallFloorAndOccupant()` ➔ `void`: ResolveBaseVisibility_WallFloorAndOccupant 관련 로직 수행 함수
  - `[public] FinalVisibility_StealthAndAttackBoost()` ➔ `void`: FinalVisibility_StealthAndAttackBoost 관련 로직 수행 함수
  - `[public] FinalVisibility_StealthTable_AllowsNegativeAndKeepsItUnclamped()` ➔ `void`: FinalVisibility_StealthTable_AllowsNegativeAndKeepsItUnclamped 관련 로직 수행 함수
  - `[public] CircularPerceptionRadius_Table()` ➔ `void`: CircularPerceptionRadius_Table 관련 로직 수행 함수
  - `[public] IsHighThreatSurprise_TwiceMeleeExpectedDamage()` ➔ `void`: IsHighThreatSurprise_TwiceMeleeExpectedDamage 관련 로직 수행 함수

#### 📄 클래스: `WeightSystemTests` (class)
- **경로**: `Tests/WeightSystemTests.cs`
- **클래스 목적 및 의도**: `WeightTests` 도메인의 핵심 비즈니스 로직 연산을 수행하는 핵심 시스템입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] result` (float): WeightSystemTests의 내부 데이터 또는 의존 참조 변수
  - `[private] ratios` (var): WeightSystemTests의 내부 데이터 또는 의존 참조 변수
  - `[private] ratios` (var): WeightSystemTests의 내부 데이터 또는 의존 참조 변수
  - `[private] ratios` (var): WeightSystemTests의 내부 데이터 또는 의존 참조 변수
  - `[private] entries` (var): WeightSystemTests의 내부 데이터 또는 의존 참조 변수
  - `[private] deduped` (var): WeightSystemTests의 내부 데이터 또는 의존 참조 변수
  - `[private] group` (var): WeightSystemTests의 내부 데이터 또는 의존 참조 변수
  - `[private] amount` (float): WeightSystemTests의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] PersonalWeightChange_Example()` ➔ `void`: PersonalWeightChange_Example 관련 로직 수행 함수
  - `[public] ReflectionRatio_Example1_WitnessAndIndirect()` ➔ `void`: ReflectionRatio_Example1_WitnessAndIndirect 관련 로직 수행 함수
  - `[public] ReflectionRatio_Example2_ExperienceAndIndirect()` ➔ `void`: ReflectionRatio_Example2_ExperienceAndIndirect 관련 로직 수행 함수
  - `[public] ReflectionRatio_Example3_WitnessOnly()` ➔ `void`: ReflectionRatio_Example3_WitnessOnly 관련 로직 수행 함수
  - `[public] Dedup_IdenticalEntries_CollapseToOne()` ➔ `void`: Dedup_IdenticalEntries_CollapseToOne 관련 로직 수행 함수
  - `[public] GlobalReflection_PanicNoiseAveraging_Example()` ➔ `void`: GlobalReflection_PanicNoiseAveraging_Example 관련 로직 수행 함수
  - `[public] GlobalReflection_Example1_AllThreeTypes()` ➔ `void`: GlobalReflection_Example1_AllThreeTypes 관련 로직 수행 함수
  - `[public] GlobalReflection_Example2_WitnessAndIndirectWithDedup()` ➔ `void`: GlobalReflection_Example2_WitnessAndIndirectWithDedup 관련 로직 수행 함수
  - `[public] UnderstandingIncrease_Example()` ➔ `void`: UnderstandingIncrease_Example 관련 로직 수행 함수
  - `[public] SpecialUnitUnderstanding_CapsAt50Each()` ➔ `void`: SpecialUnitUnderstanding_CapsAt50Each 관련 로직 수행 함수

---

### 📦 Package: `UI`

**모듈 내 포함 스크립트 수**: 4개

#### 📄 클래스: `DebugInfoPanel` (class)
- **경로**: `Script/UI/DebugInfoPanel.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `DebugInfoPanel` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `MonoRoutine, ICustomPanel` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] ScrollZoomSpeed` (float): DebugInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] _inputManager` (InputManager): DebugInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] _dataManager` (DataManager): DebugInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] _gameSession` (GameSession): DebugInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] cmap` (var): DebugInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] dto` (var): DebugInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] cmap` (var): DebugInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] model` (var): DebugInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] scroll` (float): DebugInfoPanel의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Construct(InputManager inputManager, DataManager dataManager, GameSession gameSession)` ➔ `void`: Construct 관련 로직 수행 함수
  - `[public] OpenPanel()` ➔ `void`: OpenPanel 관련 로직 수행 함수
  - `[public] ClosePanel()` ➔ `void`: ClosePanel 관련 로직 수행 함수
  - `[public] BindEvent()` ➔ `void`: BindEvent 관련 로직 수행 함수
  - `[private] Zoom(float delta)` ➔ `void`: Zoom 관련 로직 수행 함수
  - `[private] SaveMapAsync()` ➔ `UniTaskVoid`: SaveMapAsync 관련 로직 수행 함수
  - `[private] LoadMapAsync()` ➔ `UniTaskVoid`: LoadMapAsync 관련 로직 수행 함수
  - `[protected] UpdateProcess()` ➔ `void`: UpdateProcess 관련 로직 수행 함수
  - `[private] OnGUI()` ➔ `void`: OnGUI 관련 로직 수행 함수
  - `[private] DrawVisionToggle()` ➔ `void`: DrawVisionToggle 관련 로직 수행 함수

#### 📄 클래스: `GameUIPresenter` (class)
- **경로**: `Script/UI/GameUIPresenter.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `GameUIPresenter` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `UIPresenter` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] debugPanelId` (int): GameUIPresenter의 내부 데이터 또는 의존 참조 변수
  - `[private] statusPanelId` (int): GameUIPresenter의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] PostInitialize()` ➔ `void`: PostInitialize 관련 로직 수행 함수
  - `[private] BootSequence()` ➔ `UniTask`: BootSequence 관련 로직 수행 함수
  - `[private] FadeIn()` ➔ `await`: FadeIn 관련 로직 수행 함수
  - `[private] FadeOut()` ➔ `await`: FadeOut 관련 로직 수행 함수
  - `[private] FadeOut()` ➔ `await`: FadeOut 관련 로직 수행 함수

#### 📄 클래스: `SceneTransitionFade` (class)
- **경로**: `Script/UI/SceneTransitionFade.cs`
- **클래스 목적 및 의도**: `SceneTransitionFade`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoBehaviour` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _instance` (SceneTransitionFade): SceneTransitionFade의 내부 데이터 또는 의존 참조 변수
  - `[private] _canvasGroup` (CanvasGroup): SceneTransitionFade의 내부 데이터 또는 의존 참조 변수
  - `[private] RevealSeconds` (float): SceneTransitionFade의 내부 데이터 또는 의존 참조 변수
  - `[private] PostLoadSettleSeconds` (float): SceneTransitionFade의 내부 데이터 또는 의존 참조 변수
  - `[private] go` (var): SceneTransitionFade의 내부 데이터 또는 의존 참조 변수
  - `[private] _instance` (return): SceneTransitionFade의 내부 데이터 또는 의존 참조 변수
  - `[private] canvas` (var): SceneTransitionFade의 내부 데이터 또는 의존 참조 변수
  - `[private] imageGo` (var): SceneTransitionFade의 내부 데이터 또는 의존 참조 변수
  - `[private] image` (var): SceneTransitionFade의 내부 데이터 또는 의존 참조 변수
  - `[private] rect` (var): SceneTransitionFade의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] EnsureInstance()` ➔ `SceneTransitionFade`: EnsureInstance 관련 로직 수행 함수
  - `[private] BuildOverlay()` ➔ `void`: BuildOverlay 관련 로직 수행 함수
  - `[public] LoadSceneWithCoverAsync(string sceneToLoad, string sceneToUnload)` ➔ `UniTask`: LoadSceneWithCoverAsync 관련 로직 수행 함수
  - `[private] FadeAsync(1f, 0f, RevealSeconds)` ➔ `await`: FadeAsync 관련 로직 수행 함수
  - `[private] FadeAsync(float from, float to, float duration)` ➔ `UniTask`: FadeAsync 관련 로직 수행 함수

#### 📄 클래스: `StatusInfoPanel` (class)
- **경로**: `Script/UI/StatusInfoPanel.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `StatusInfoPanel` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `MonoRoutine, ICustomPanel` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _gameSession` (GameSession): StatusInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] Session` (GameSession): StatusInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] PanelWidth` (int): StatusInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] PanelY` (int): StatusInfoPanel의 내부 데이터 또는 의존 참조 변수
  - `[private] PanelHeight` (int): StatusInfoPanel의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] OpenPanel()` ➔ `void`: OpenPanel 관련 로직 수행 함수
  - `[public] ClosePanel()` ➔ `void`: ClosePanel 관련 로직 수행 함수
  - `[public] BindEvent()` ➔ `void`: BindEvent 관련 로직 수행 함수
  - `[private] OnGUI()` ➔ `void`: OnGUI 관련 로직 수행 함수

---

### 📦 Package: `UI/Title`

**모듈 내 포함 스크립트 수**: 4개

#### 📄 클래스: `GameTitlePanel` (class)
- **경로**: `Script/UI/Title/GameTitlePanel.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `GameTitlePanel` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `MonoRoutine, ICustomPanel` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[public] OpenPanel()` ➔ `void`: OpenPanel 관련 로직 수행 함수
  - `[public] ClosePanel()` ➔ `void`: ClosePanel 관련 로직 수행 함수
  - `[public] BindEvent()` ➔ `void`: BindEvent 관련 로직 수행 함수

#### 📄 클래스: `TitlePresenter` (class)
- **경로**: `Script/UI/Title/TitlePresenter.cs`
- **클래스 목적 및 의도**: `TitlePresenter`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IPresenter` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] TitleSceneName` (string): TitlePresenter의 내부 데이터 또는 의존 참조 변수
  - `[private] GameSceneName` (string): TitlePresenter의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Dispose()` ➔ `void`: Dispose 관련 로직 수행 함수
  - `[public] PostInitialize()` ➔ `void`: PostInitialize 관련 로직 수행 함수
  - `[private] BindPanel(GameTitlePanel panel)` ➔ `void`: BindPanel 관련 로직 수행 함수
  - `[private] StartGame()` ➔ `void`: StartGame 관련 로직 수행 함수
  - `[private] OpenSettings()` ➔ `void`: OpenSettings 관련 로직 수행 함수
  - `[private] QuitGame()` ➔ `void`: QuitGame 관련 로직 수행 함수

#### 📄 클래스: `TitleScope` (class)
- **경로**: `Script/UI/Title/TitleScope.cs`
- **클래스 목적 및 의도**: `TitleScope`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `LifetimeScope` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[protected] Configure(IContainerBuilder builder)` ➔ `void`: Configure 관련 로직 수행 함수

#### 📄 클래스: `TitleUIManager` (class)
- **경로**: `Script/UI/Title/TitleUIManager.cs`
- **클래스 목적 및 의도**: 게임 내 `TitleUI` 관련 전역 상태 및 루프 시스템을 총괄 관리하는 매니저 클래스입니다.
- **상속 및 구현 관계**: `SceneUIManager` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] titlePanelID` (int): TitleUIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] panel` (var): TitleUIManager의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수

---

### 📦 Package: `Unit/AI`

**모듈 내 포함 스크립트 수**: 38개

#### 📄 클래스: `Action_Panic` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_Panic`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_Panic의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_Panic의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_Panic의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_Panic의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_MoveToPlayerTarget` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_MoveToPlayerTarget`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_MoveToPlayerTarget의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_MoveToPlayerTarget의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_MoveToPlayerTarget의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_MoveToPlayerTarget의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_CompletePlayerCommand` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_CompletePlayerCommand`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_CompletePlayerCommand의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_CompletePlayerCommand의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_CompletePlayerCommand의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_CompletePlayerCommand의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_MoveToStairs` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_MoveToStairs`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_MoveToStairs의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_MoveToStairs의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_MoveToStairs의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_MoveToStairs의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_CrossStairs` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_CrossStairs`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_CrossStairs의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_CrossStairs의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_CrossStairs의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_CrossStairs의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_RandomExplore` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_RandomExplore`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_RandomExplore의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_RandomExplore의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_RandomExplore의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_RandomExplore의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_EngageEnemy` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_EngageEnemy`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_EngageEnemy의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_EngageEnemy의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_EngageEnemy의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_EngageEnemy의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_TrapJoinWait` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_TrapJoinWait`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_TrapJoinWait의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_TrapJoinWait의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_TrapJoinWait의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_TrapJoinWait의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_MoveToTrap` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_MoveToTrap`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_MoveToTrap의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_MoveToTrap의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_MoveToTrap의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_MoveToTrap의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_TrapDisarmPerform` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_TrapDisarmPerform`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_TrapDisarmPerform의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_TrapDisarmPerform의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_TrapDisarmPerform의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_TrapDisarmPerform의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_TrapBypass` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_TrapBypass`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_TrapBypass의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_TrapBypass의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_TrapBypass의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_TrapBypass의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_TrapPass` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_TrapPass`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_TrapPass의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_TrapPass의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_TrapPass의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_TrapPass의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_TrapDestroy` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_TrapDestroy`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_TrapDestroy의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_TrapDestroy의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_TrapDestroy의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_TrapDestroy의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_MoveToInvestigateTarget` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_MoveToInvestigateTarget`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_MoveToInvestigateTarget의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_MoveToInvestigateTarget의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_MoveToInvestigateTarget의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_MoveToInvestigateTarget의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_InvestigatePerform` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_InvestigatePerform`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_InvestigatePerform의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_InvestigatePerform의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_InvestigatePerform의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_InvestigatePerform의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_AlertApproach` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_AlertApproach`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_AlertApproach의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_AlertApproach의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_AlertApproach의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_AlertApproach의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_AlertPerimeterSearch` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_AlertPerimeterSearch`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_AlertPerimeterSearch의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_AlertPerimeterSearch의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_AlertPerimeterSearch의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_AlertPerimeterSearch의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_Wait` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_Wait`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_Wait의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_Wait의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_Wait의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_Wait의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_MoveToEscortSlotMelee` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_MoveToEscortSlotMelee`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_MoveToEscortSlotMelee의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_MoveToEscortSlotMelee의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_MoveToEscortSlotMelee의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_MoveToEscortSlotMelee의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_MoveToEscortSlotRanged` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_MoveToEscortSlotRanged`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_MoveToEscortSlotRanged의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_MoveToEscortSlotRanged의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_MoveToEscortSlotRanged의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_MoveToEscortSlotRanged의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Action_HoldFormation` (class)
- **경로**: `Script/Unit/AI/Actions.cs`
- **클래스 목적 및 의도**: `Action_HoldFormation`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] randomDir` (Dir): Action_HoldFormation의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Vector2Int): Action_HoldFormation의 내부 데이터 또는 의존 참조 변수
  - `[private] interactPos` (Vector3Int): Action_HoldFormation의 내부 데이터 또는 의존 참조 변수
  - `[private] isTrace` (bool): Action_HoldFormation의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Action_Panic()` ➔ `public`: Action_Panic 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToPlayerTarget()` ➔ `public`: Action_MoveToPlayerTarget 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_CompletePlayerCommand()` ➔ `public`: Action_CompletePlayerCommand 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] Action_MoveToStairs()` ➔ `public`: Action_MoveToStairs 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] PickFreeApproachTile(Unit unit, List<Vector2Int> candidates)` ➔ `Vector2Int`: PickFreeApproachTile 관련 로직 수행 함수
  - `[private] Action_CrossStairs()` ➔ `public`: Action_CrossStairs 관련 로직 수행 함수

#### 📄 클래스: `Goal_Panic` (class)
- **경로**: `Script/Unit/AI/Goals.cs`
- **클래스 목적 및 의도**: `Goal_Panic`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapGoal` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] 0f` (return): Goal_Panic의 내부 데이터 또는 의존 참조 변수
  - `[private] 99f` (return): Goal_Panic의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_Panic의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_Panic의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_Panic의 내부 데이터 또는 의존 참조 변수
  - `[private] enemies` (IEnumerable<Unit>): Goal_Panic의 내부 데이터 또는 의존 참조 변수
  - `[private] 100f` (return): Goal_Panic의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_Panic의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_Panic의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): Goal_Panic의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Goal_Panic()` ➔ `public`: Goal_Panic 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_PlayerCommand()` ➔ `public`: Goal_PlayerCommand 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_UseStairs()` ➔ `public`: Goal_UseStairs 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_DefeatEnemy()` ➔ `public`: Goal_DefeatEnemy 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_Explore()` ➔ `public`: Goal_Explore 관련 로직 수행 함수
  - `[private] Goal_TrapResponse()` ➔ `public`: Goal_TrapResponse 관련 로직 수행 함수

#### 📄 클래스: `Goal_PlayerCommand` (class)
- **경로**: `Script/Unit/AI/Goals.cs`
- **클래스 목적 및 의도**: `Goal_PlayerCommand`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapGoal` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] 0f` (return): Goal_PlayerCommand의 내부 데이터 또는 의존 참조 변수
  - `[private] 99f` (return): Goal_PlayerCommand의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_PlayerCommand의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_PlayerCommand의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_PlayerCommand의 내부 데이터 또는 의존 참조 변수
  - `[private] enemies` (IEnumerable<Unit>): Goal_PlayerCommand의 내부 데이터 또는 의존 참조 변수
  - `[private] 100f` (return): Goal_PlayerCommand의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_PlayerCommand의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_PlayerCommand의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): Goal_PlayerCommand의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Goal_Panic()` ➔ `public`: Goal_Panic 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_PlayerCommand()` ➔ `public`: Goal_PlayerCommand 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_UseStairs()` ➔ `public`: Goal_UseStairs 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_DefeatEnemy()` ➔ `public`: Goal_DefeatEnemy 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_Explore()` ➔ `public`: Goal_Explore 관련 로직 수행 함수
  - `[private] Goal_TrapResponse()` ➔ `public`: Goal_TrapResponse 관련 로직 수행 함수

#### 📄 클래스: `Goal_UseStairs` (class)
- **경로**: `Script/Unit/AI/Goals.cs`
- **클래스 목적 및 의도**: `Goal_UseStairs`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapGoal` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] 0f` (return): Goal_UseStairs의 내부 데이터 또는 의존 참조 변수
  - `[private] 99f` (return): Goal_UseStairs의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_UseStairs의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_UseStairs의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_UseStairs의 내부 데이터 또는 의존 참조 변수
  - `[private] enemies` (IEnumerable<Unit>): Goal_UseStairs의 내부 데이터 또는 의존 참조 변수
  - `[private] 100f` (return): Goal_UseStairs의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_UseStairs의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_UseStairs의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): Goal_UseStairs의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Goal_Panic()` ➔ `public`: Goal_Panic 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_PlayerCommand()` ➔ `public`: Goal_PlayerCommand 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_UseStairs()` ➔ `public`: Goal_UseStairs 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_DefeatEnemy()` ➔ `public`: Goal_DefeatEnemy 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_Explore()` ➔ `public`: Goal_Explore 관련 로직 수행 함수
  - `[private] Goal_TrapResponse()` ➔ `public`: Goal_TrapResponse 관련 로직 수행 함수

#### 📄 클래스: `Goal_DefeatEnemy` (class)
- **경로**: `Script/Unit/AI/Goals.cs`
- **클래스 목적 및 의도**: `Goal_DefeatEnemy`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapGoal` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] 0f` (return): Goal_DefeatEnemy의 내부 데이터 또는 의존 참조 변수
  - `[private] 99f` (return): Goal_DefeatEnemy의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_DefeatEnemy의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_DefeatEnemy의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_DefeatEnemy의 내부 데이터 또는 의존 참조 변수
  - `[private] enemies` (IEnumerable<Unit>): Goal_DefeatEnemy의 내부 데이터 또는 의존 참조 변수
  - `[private] 100f` (return): Goal_DefeatEnemy의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_DefeatEnemy의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_DefeatEnemy의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): Goal_DefeatEnemy의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Goal_Panic()` ➔ `public`: Goal_Panic 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_PlayerCommand()` ➔ `public`: Goal_PlayerCommand 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_UseStairs()` ➔ `public`: Goal_UseStairs 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_DefeatEnemy()` ➔ `public`: Goal_DefeatEnemy 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_Explore()` ➔ `public`: Goal_Explore 관련 로직 수행 함수
  - `[private] Goal_TrapResponse()` ➔ `public`: Goal_TrapResponse 관련 로직 수행 함수

#### 📄 클래스: `Goal_Explore` (class)
- **경로**: `Script/Unit/AI/Goals.cs`
- **클래스 목적 및 의도**: `Goal_Explore`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapGoal` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] 0f` (return): Goal_Explore의 내부 데이터 또는 의존 참조 변수
  - `[private] 99f` (return): Goal_Explore의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_Explore의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_Explore의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_Explore의 내부 데이터 또는 의존 참조 변수
  - `[private] enemies` (IEnumerable<Unit>): Goal_Explore의 내부 데이터 또는 의존 참조 변수
  - `[private] 100f` (return): Goal_Explore의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_Explore의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_Explore의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): Goal_Explore의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Goal_Panic()` ➔ `public`: Goal_Panic 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_PlayerCommand()` ➔ `public`: Goal_PlayerCommand 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_UseStairs()` ➔ `public`: Goal_UseStairs 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_DefeatEnemy()` ➔ `public`: Goal_DefeatEnemy 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_Explore()` ➔ `public`: Goal_Explore 관련 로직 수행 함수
  - `[private] Goal_TrapResponse()` ➔ `public`: Goal_TrapResponse 관련 로직 수행 함수

#### 📄 클래스: `Goal_TrapResponse` (class)
- **경로**: `Script/Unit/AI/Goals.cs`
- **클래스 목적 및 의도**: `Goal_TrapResponse`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapGoal` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] 0f` (return): Goal_TrapResponse의 내부 데이터 또는 의존 참조 변수
  - `[private] 99f` (return): Goal_TrapResponse의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_TrapResponse의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_TrapResponse의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_TrapResponse의 내부 데이터 또는 의존 참조 변수
  - `[private] enemies` (IEnumerable<Unit>): Goal_TrapResponse의 내부 데이터 또는 의존 참조 변수
  - `[private] 100f` (return): Goal_TrapResponse의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_TrapResponse의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_TrapResponse의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): Goal_TrapResponse의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Goal_Panic()` ➔ `public`: Goal_Panic 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_PlayerCommand()` ➔ `public`: Goal_PlayerCommand 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_UseStairs()` ➔ `public`: Goal_UseStairs 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_DefeatEnemy()` ➔ `public`: Goal_DefeatEnemy 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_Explore()` ➔ `public`: Goal_Explore 관련 로직 수행 함수
  - `[private] Goal_TrapResponse()` ➔ `public`: Goal_TrapResponse 관련 로직 수행 함수

#### 📄 클래스: `Goal_Alert` (class)
- **경로**: `Script/Unit/AI/Goals.cs`
- **클래스 목적 및 의도**: `Goal_Alert`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapGoal` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] 0f` (return): Goal_Alert의 내부 데이터 또는 의존 참조 변수
  - `[private] 99f` (return): Goal_Alert의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_Alert의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_Alert의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_Alert의 내부 데이터 또는 의존 참조 변수
  - `[private] enemies` (IEnumerable<Unit>): Goal_Alert의 내부 데이터 또는 의존 참조 변수
  - `[private] 100f` (return): Goal_Alert의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_Alert의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_Alert의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): Goal_Alert의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Goal_Panic()` ➔ `public`: Goal_Panic 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_PlayerCommand()` ➔ `public`: Goal_PlayerCommand 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_UseStairs()` ➔ `public`: Goal_UseStairs 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_DefeatEnemy()` ➔ `public`: Goal_DefeatEnemy 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_Explore()` ➔ `public`: Goal_Explore 관련 로직 수행 함수
  - `[private] Goal_TrapResponse()` ➔ `public`: Goal_TrapResponse 관련 로직 수행 함수

#### 📄 클래스: `Goal_Investigate` (class)
- **경로**: `Script/Unit/AI/Goals.cs`
- **클래스 목적 및 의도**: `Goal_Investigate`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapGoal` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] 0f` (return): Goal_Investigate의 내부 데이터 또는 의존 참조 변수
  - `[private] 99f` (return): Goal_Investigate의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_Investigate의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_Investigate의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_Investigate의 내부 데이터 또는 의존 참조 변수
  - `[private] enemies` (IEnumerable<Unit>): Goal_Investigate의 내부 데이터 또는 의존 참조 변수
  - `[private] 100f` (return): Goal_Investigate의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_Investigate의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_Investigate의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): Goal_Investigate의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Goal_Panic()` ➔ `public`: Goal_Panic 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_PlayerCommand()` ➔ `public`: Goal_PlayerCommand 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_UseStairs()` ➔ `public`: Goal_UseStairs 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_DefeatEnemy()` ➔ `public`: Goal_DefeatEnemy 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_Explore()` ➔ `public`: Goal_Explore 관련 로직 수행 함수
  - `[private] Goal_TrapResponse()` ➔ `public`: Goal_TrapResponse 관련 로직 수행 함수

#### 📄 클래스: `Goal_Wait` (class)
- **경로**: `Script/Unit/AI/Goals.cs`
- **클래스 목적 및 의도**: `Goal_Wait`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapGoal` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] 0f` (return): Goal_Wait의 내부 데이터 또는 의존 참조 변수
  - `[private] 99f` (return): Goal_Wait의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_Wait의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_Wait의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_Wait의 내부 데이터 또는 의존 참조 변수
  - `[private] enemies` (IEnumerable<Unit>): Goal_Wait의 내부 데이터 또는 의존 참조 변수
  - `[private] 100f` (return): Goal_Wait의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_Wait의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_Wait의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): Goal_Wait의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Goal_Panic()` ➔ `public`: Goal_Panic 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_PlayerCommand()` ➔ `public`: Goal_PlayerCommand 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_UseStairs()` ➔ `public`: Goal_UseStairs 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_DefeatEnemy()` ➔ `public`: Goal_DefeatEnemy 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_Explore()` ➔ `public`: Goal_Explore 관련 로직 수행 함수
  - `[private] Goal_TrapResponse()` ➔ `public`: Goal_TrapResponse 관련 로직 수행 함수

#### 📄 클래스: `Goal_ProtectiveFormation` (class)
- **경로**: `Script/Unit/AI/Goals.cs`
- **클래스 목적 및 의도**: `Goal_ProtectiveFormation`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `GoapGoal` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] 0f` (return): Goal_ProtectiveFormation의 내부 데이터 또는 의존 참조 변수
  - `[private] 99f` (return): Goal_ProtectiveFormation의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_ProtectiveFormation의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_ProtectiveFormation의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_ProtectiveFormation의 내부 데이터 또는 의존 참조 변수
  - `[private] enemies` (IEnumerable<Unit>): Goal_ProtectiveFormation의 내부 데이터 또는 의존 참조 변수
  - `[private] 100f` (return): Goal_ProtectiveFormation의 내부 데이터 또는 의존 참조 변수
  - `[private] 0f` (return): Goal_ProtectiveFormation의 내부 데이터 또는 의존 참조 변수
  - `[private] 140f` (return): Goal_ProtectiveFormation의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): Goal_ProtectiveFormation의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Goal_Panic()` ➔ `public`: Goal_Panic 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_PlayerCommand()` ➔ `public`: Goal_PlayerCommand 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_UseStairs()` ➔ `public`: Goal_UseStairs 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_DefeatEnemy()` ➔ `public`: Goal_DefeatEnemy 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[private] Goal_Explore()` ➔ `public`: Goal_Explore 관련 로직 수행 함수
  - `[private] Goal_TrapResponse()` ➔ `public`: Goal_TrapResponse 관련 로직 수행 함수

#### 📄 클래스: `GoapState` (class)
- **경로**: `Script/Unit/AI/GoapCore.cs`
- **클래스 목적 및 의도**: `GoapState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `Dictionary<string, bool>` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] Name` (string): GoapState의 내부 데이터 또는 의존 참조 변수
  - `[public] DesiredState` (GoapState): GoapState의 내부 데이터 또는 의존 참조 변수
  - `[public] ActionName` (string): GoapState의 내부 데이터 또는 의존 참조 변수
  - `[public] Cost` (float): GoapState의 내부 데이터 또는 의존 참조 변수
  - `[public] Preconditions` (GoapState): GoapState의 내부 데이터 또는 의존 참조 변수
  - `[public] Effects` (GoapState): GoapState의 내부 데이터 또는 의존 참조 변수
  - `[private] enemies` (IEnumerable<Unit>): GoapState의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Unit): GoapState의 내부 데이터 또는 의존 참조 변수
  - `[private] d` (float): GoapState의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (return): GoapState의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[public] IsValid(Unit unit)` ➔ `bool`: IsValid 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[protected] GetClosestEnemy(Unit unit, out float minDist)` ➔ `Unit`: GetClosestEnemy 관련 로직 수행 함수
  - `[protected] MoveTowardsPos(Unit unit, Vector2Int targetPos)` ➔ `void`: MoveTowardsPos 관련 로직 수행 함수
  - `[protected] MoveAwayFromTarget(Unit unit, Unit target, float desiredDist)` ➔ `void`: MoveAwayFromTarget 관련 로직 수행 함수
  - `[protected] MoveToEscortSlot(Human human, float backDistance)` ➔ `void`: MoveToEscortSlot 관련 로직 수행 함수
  - `[private] GoapBrain()` ➔ `public`: GoapBrain 관련 로직 수행 함수
  - `[public] PlanText(Unit unit)` ➔ `string`: PlanText 관련 로직 수행 함수

#### 📄 클래스: `GoapGoal` (class)
- **경로**: `Script/Unit/AI/GoapCore.cs`
- **클래스 목적 및 의도**: `GoapGoal`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] Name` (string): GoapGoal의 내부 데이터 또는 의존 참조 변수
  - `[public] DesiredState` (GoapState): GoapGoal의 내부 데이터 또는 의존 참조 변수
  - `[public] ActionName` (string): GoapGoal의 내부 데이터 또는 의존 참조 변수
  - `[public] Cost` (float): GoapGoal의 내부 데이터 또는 의존 참조 변수
  - `[public] Preconditions` (GoapState): GoapGoal의 내부 데이터 또는 의존 참조 변수
  - `[public] Effects` (GoapState): GoapGoal의 내부 데이터 또는 의존 참조 변수
  - `[private] enemies` (IEnumerable<Unit>): GoapGoal의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Unit): GoapGoal의 내부 데이터 또는 의존 참조 변수
  - `[private] d` (float): GoapGoal의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (return): GoapGoal의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[public] IsValid(Unit unit)` ➔ `bool`: IsValid 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[protected] GetClosestEnemy(Unit unit, out float minDist)` ➔ `Unit`: GetClosestEnemy 관련 로직 수행 함수
  - `[protected] MoveTowardsPos(Unit unit, Vector2Int targetPos)` ➔ `void`: MoveTowardsPos 관련 로직 수행 함수
  - `[protected] MoveAwayFromTarget(Unit unit, Unit target, float desiredDist)` ➔ `void`: MoveAwayFromTarget 관련 로직 수행 함수
  - `[protected] MoveToEscortSlot(Human human, float backDistance)` ➔ `void`: MoveToEscortSlot 관련 로직 수행 함수
  - `[private] GoapBrain()` ➔ `public`: GoapBrain 관련 로직 수행 함수
  - `[public] PlanText(Unit unit)` ➔ `string`: PlanText 관련 로직 수행 함수

#### 📄 클래스: `GoapAction` (class)
- **경로**: `Script/Unit/AI/GoapCore.cs`
- **클래스 목적 및 의도**: `GoapAction`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] Name` (string): GoapAction의 내부 데이터 또는 의존 참조 변수
  - `[public] DesiredState` (GoapState): GoapAction의 내부 데이터 또는 의존 참조 변수
  - `[public] ActionName` (string): GoapAction의 내부 데이터 또는 의존 참조 변수
  - `[public] Cost` (float): GoapAction의 내부 데이터 또는 의존 참조 변수
  - `[public] Preconditions` (GoapState): GoapAction의 내부 데이터 또는 의존 참조 변수
  - `[public] Effects` (GoapState): GoapAction의 내부 데이터 또는 의존 참조 변수
  - `[private] enemies` (IEnumerable<Unit>): GoapAction의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Unit): GoapAction의 내부 데이터 또는 의존 참조 변수
  - `[private] d` (float): GoapAction의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (return): GoapAction의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[public] IsValid(Unit unit)` ➔ `bool`: IsValid 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[protected] GetClosestEnemy(Unit unit, out float minDist)` ➔ `Unit`: GetClosestEnemy 관련 로직 수행 함수
  - `[protected] MoveTowardsPos(Unit unit, Vector2Int targetPos)` ➔ `void`: MoveTowardsPos 관련 로직 수행 함수
  - `[protected] MoveAwayFromTarget(Unit unit, Unit target, float desiredDist)` ➔ `void`: MoveAwayFromTarget 관련 로직 수행 함수
  - `[protected] MoveToEscortSlot(Human human, float backDistance)` ➔ `void`: MoveToEscortSlot 관련 로직 수행 함수
  - `[private] GoapBrain()` ➔ `public`: GoapBrain 관련 로직 수행 함수
  - `[public] PlanText(Unit unit)` ➔ `string`: PlanText 관련 로직 수행 함수

#### 📄 클래스: `GoapBrain` (class)
- **경로**: `Script/Unit/AI/GoapCore.cs`
- **클래스 목적 및 의도**: `GoapBrain`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] Name` (string): GoapBrain의 내부 데이터 또는 의존 참조 변수
  - `[public] DesiredState` (GoapState): GoapBrain의 내부 데이터 또는 의존 참조 변수
  - `[public] ActionName` (string): GoapBrain의 내부 데이터 또는 의존 참조 변수
  - `[public] Cost` (float): GoapBrain의 내부 데이터 또는 의존 참조 변수
  - `[public] Preconditions` (GoapState): GoapBrain의 내부 데이터 또는 의존 참조 변수
  - `[public] Effects` (GoapState): GoapBrain의 내부 데이터 또는 의존 참조 변수
  - `[private] enemies` (IEnumerable<Unit>): GoapBrain의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (Unit): GoapBrain의 내부 데이터 또는 의존 참조 변수
  - `[private] d` (float): GoapBrain의 내부 데이터 또는 의존 참조 변수
  - `[private] target` (return): GoapBrain의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] GetPriority(Unit unit)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[public] IsValid(Unit unit)` ➔ `bool`: IsValid 관련 로직 수행 함수
  - `[public] Execute(Unit unit)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[protected] GetClosestEnemy(Unit unit, out float minDist)` ➔ `Unit`: GetClosestEnemy 관련 로직 수행 함수
  - `[protected] MoveTowardsPos(Unit unit, Vector2Int targetPos)` ➔ `void`: MoveTowardsPos 관련 로직 수행 함수
  - `[protected] MoveAwayFromTarget(Unit unit, Unit target, float desiredDist)` ➔ `void`: MoveAwayFromTarget 관련 로직 수행 함수
  - `[protected] MoveToEscortSlot(Human human, float backDistance)` ➔ `void`: MoveToEscortSlot 관련 로직 수행 함수
  - `[private] GoapBrain()` ➔ `public`: GoapBrain 관련 로직 수행 함수
  - `[public] PlanText(Unit unit)` ➔ `string`: PlanText 관련 로직 수행 함수

#### 📄 클래스: `GoapPlanner` (class)
- **경로**: `Script/Unit/AI/GoapPlanner.cs`
- **클래스 목적 및 의도**: `GoapPlanner`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] MaxDepth` (int): GoapPlanner의 내부 데이터 또는 의존 참조 변수
  - `[public] State` (GoapState): GoapPlanner의 내부 데이터 또는 의존 참조 변수
  - `[public] Path` (List<GoapAction>): GoapPlanner의 내부 데이터 또는 의존 참조 변수
  - `[public] Cost` (float): GoapPlanner의 내부 데이터 또는 의존 참조 변수
  - `[private] validActions` (var): GoapPlanner의 내부 데이터 또는 의존 참조 변수
  - `[private] frontier` (var): GoapPlanner의 내부 데이터 또는 의존 참조 변수
  - `[private] bestCost` (var): GoapPlanner의 내부 데이터 또는 의존 참조 변수
  - `[private] bestIdx` (int): GoapPlanner의 내부 데이터 또는 의존 참조 변수
  - `[private] node` (Node): GoapPlanner의 내부 데이터 또는 의존 참조 변수
  - `[private] nodeKey` (string): GoapPlanner의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Plan(Unit unit, GoapState start, GoapState desired, List<GoapAction> availableActions)` ➔ `List<GoapAction>`: Plan 관련 로직 수행 함수
  - `[public] ApplyAll(GoapState start, List<GoapAction> plan)` ➔ `GoapState`: ApplyAll 관련 로직 수행 함수
  - `[private] IsSatisfied(GoapState state, GoapState desired)` ➔ `bool`: IsSatisfied 관련 로직 수행 함수
  - `[private] Satisfies(GoapState state, GoapState preconditions)` ➔ `bool`: Satisfies 관련 로직 수행 함수
  - `[private] Apply(GoapState state, GoapState effects)` ➔ `GoapState`: Apply 관련 로직 수행 함수
  - `[private] Serialize(GoapState state)` ➔ `string`: Serialize 관련 로직 수행 함수

#### 📄 클래스: `Node` (class)
- **경로**: `Script/Unit/AI/GoapPlanner.cs`
- **클래스 목적 및 의도**: `Node`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] MaxDepth` (int): Node의 내부 데이터 또는 의존 참조 변수
  - `[public] State` (GoapState): Node의 내부 데이터 또는 의존 참조 변수
  - `[public] Path` (List<GoapAction>): Node의 내부 데이터 또는 의존 참조 변수
  - `[public] Cost` (float): Node의 내부 데이터 또는 의존 참조 변수
  - `[private] validActions` (var): Node의 내부 데이터 또는 의존 참조 변수
  - `[private] frontier` (var): Node의 내부 데이터 또는 의존 참조 변수
  - `[private] bestCost` (var): Node의 내부 데이터 또는 의존 참조 변수
  - `[private] bestIdx` (int): Node의 내부 데이터 또는 의존 참조 변수
  - `[private] node` (Node): Node의 내부 데이터 또는 의존 참조 변수
  - `[private] nodeKey` (string): Node의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Plan(Unit unit, GoapState start, GoapState desired, List<GoapAction> availableActions)` ➔ `List<GoapAction>`: Plan 관련 로직 수행 함수
  - `[public] ApplyAll(GoapState start, List<GoapAction> plan)` ➔ `GoapState`: ApplyAll 관련 로직 수행 함수
  - `[private] IsSatisfied(GoapState state, GoapState desired)` ➔ `bool`: IsSatisfied 관련 로직 수행 함수
  - `[private] Satisfies(GoapState state, GoapState preconditions)` ➔ `bool`: Satisfies 관련 로직 수행 함수
  - `[private] Apply(GoapState state, GoapState effects)` ➔ `GoapState`: Apply 관련 로직 수행 함수
  - `[private] Serialize(GoapState state)` ➔ `string`: Serialize 관련 로직 수행 함수

#### 📄 클래스: `GoapWorldState` (class)
- **경로**: `Script/Unit/AI/GoapWorldState.cs`
- **클래스 목적 및 의도**: `GoapWorldState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] StairArrivalRadius` (int): GoapWorldState의 내부 데이터 또는 의존 참조 변수
  - `[private] dx` (int): GoapWorldState의 내부 데이터 또는 의존 참조 변수
  - `[private] dy` (int): GoapWorldState의 내부 데이터 또는 의존 참조 변수
  - `[private] state` (var): GoapWorldState의 내부 데이터 또는 의존 참조 변수
  - `[private] enemyVisible` (bool): GoapWorldState의 내부 데이터 또는 의존 참조 변수
  - `[private] trap` (var): GoapWorldState의 내부 데이터 또는 의존 참조 변수
  - `[private] investigateNeeded` (bool): GoapWorldState의 내부 데이터 또는 의존 참조 변수
  - `[private] atEscortSlot` (bool): GoapWorldState의 내부 데이터 또는 의존 참조 변수
  - `[private] backDistance` (float): GoapWorldState의 내부 데이터 또는 의존 참조 변수
  - `[private] slot` (Vector2Int): GoapWorldState의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] DistanceToStairBlock(Vector2Int pos, Vector2Int stairBlockTopLeft)` ➔ `int`: DistanceToStairBlock 관련 로직 수행 함수
  - `[public] Build(Unit unit)` ➔ `GoapState`: Build 관련 로직 수행 함수

---

### 📦 Package: `Unit/Combat`

**모듈 내 포함 스크립트 수**: 7개

#### 📄 클래스: `DefenseType` (enum)
- **경로**: `Script/Unit/Combat/DefenseSystem.cs`
- **클래스 목적 및 의도**: `DefenseType`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] type` (DefenseType): DefenseType의 내부 데이터 또는 의존 참조 변수
  - `[public] score` (float): DefenseType의 내부 데이터 또는 의존 참조 변수
  - `[public] weight` (float): DefenseType의 내부 데이터 또는 의존 참조 변수
  - `[private] candidates` (List<DefenseCandidate>): DefenseType의 내부 데이터 또는 의존 참조 변수
  - `[private] selected` (DefenseCandidate): DefenseType의 내부 데이터 또는 의존 참조 변수
  - `[private] candidates` (List<DefenseCandidate>): DefenseType의 내부 데이터 또는 의존 참조 변수
  - `[private] selected` (DefenseCandidate): DefenseType의 내부 데이터 또는 의존 참조 변수
  - `[private] result` (List<DefenseCandidate>): DefenseType의 내부 데이터 또는 의존 참조 변수
  - `[private] durability` (float): DefenseType의 내부 데이터 또는 의존 참조 변수
  - `[private] resistance` (float): DefenseType의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] DefenseCandidate(DefenseType type, float score, float weight)` ➔ `public`: DefenseCandidate 관련 로직 수행 함수
  - `[public] EvaluateEarlyReaction(Unit defender, Unit attacker, ThreatTileData threat)` ➔ `void`: EvaluateEarlyReaction 관련 로직 수행 함수
  - `[public] EvaluateImpactDefense(Unit defender, Unit attacker, float rawDamage)` ➔ `float`: EvaluateImpactDefense 관련 로직 수행 함수
  - `[private] ExecuteImpactDefense(defender, attacker, selected, rawDamage)` ➔ `return`: ExecuteImpactDefense 관련 로직 수행 함수
  - `[private] BuildDefenseCandidates(Unit defender, Unit attacker, ThreatTileData threat, bool isEarlyReaction)` ➔ `List<DefenseCandidate>`: BuildDefenseCandidates 관련 로직 수행 함수
  - `[private] SelectDefense(List<DefenseCandidate> list)` ➔ `DefenseCandidate`: SelectDefense 관련 로직 수행 함수
  - `[private] HasPathWithoutWalls(Unit defender, Vector2Int start, Vector2Int target, int maxRange)` ➔ `bool`: HasPathWithoutWalls 관련 로직 수행 함수
  - `[private] ExecuteEarlyReaction(Unit defender, Unit attacker, DefenseCandidate selected)` ➔ `void`: ExecuteEarlyReaction 관련 로직 수행 함수

#### 📄 클래스: `DefenseCandidate` (class)
- **경로**: `Script/Unit/Combat/DefenseSystem.cs`
- **클래스 목적 및 의도**: `DefenseCandidate`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] type` (DefenseType): DefenseCandidate의 내부 데이터 또는 의존 참조 변수
  - `[public] score` (float): DefenseCandidate의 내부 데이터 또는 의존 참조 변수
  - `[public] weight` (float): DefenseCandidate의 내부 데이터 또는 의존 참조 변수
  - `[private] candidates` (List<DefenseCandidate>): DefenseCandidate의 내부 데이터 또는 의존 참조 변수
  - `[private] selected` (DefenseCandidate): DefenseCandidate의 내부 데이터 또는 의존 참조 변수
  - `[private] candidates` (List<DefenseCandidate>): DefenseCandidate의 내부 데이터 또는 의존 참조 변수
  - `[private] selected` (DefenseCandidate): DefenseCandidate의 내부 데이터 또는 의존 참조 변수
  - `[private] result` (List<DefenseCandidate>): DefenseCandidate의 내부 데이터 또는 의존 참조 변수
  - `[private] durability` (float): DefenseCandidate의 내부 데이터 또는 의존 참조 변수
  - `[private] resistance` (float): DefenseCandidate의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] DefenseCandidate(DefenseType type, float score, float weight)` ➔ `public`: DefenseCandidate 관련 로직 수행 함수
  - `[public] EvaluateEarlyReaction(Unit defender, Unit attacker, ThreatTileData threat)` ➔ `void`: EvaluateEarlyReaction 관련 로직 수행 함수
  - `[public] EvaluateImpactDefense(Unit defender, Unit attacker, float rawDamage)` ➔ `float`: EvaluateImpactDefense 관련 로직 수행 함수
  - `[private] ExecuteImpactDefense(defender, attacker, selected, rawDamage)` ➔ `return`: ExecuteImpactDefense 관련 로직 수행 함수
  - `[private] BuildDefenseCandidates(Unit defender, Unit attacker, ThreatTileData threat, bool isEarlyReaction)` ➔ `List<DefenseCandidate>`: BuildDefenseCandidates 관련 로직 수행 함수
  - `[private] SelectDefense(List<DefenseCandidate> list)` ➔ `DefenseCandidate`: SelectDefense 관련 로직 수행 함수
  - `[private] HasPathWithoutWalls(Unit defender, Vector2Int start, Vector2Int target, int maxRange)` ➔ `bool`: HasPathWithoutWalls 관련 로직 수행 함수
  - `[private] ExecuteEarlyReaction(Unit defender, Unit attacker, DefenseCandidate selected)` ➔ `void`: ExecuteEarlyReaction 관련 로직 수행 함수

#### 📄 클래스: `DefenseSystem` (class)
- **경로**: `Script/Unit/Combat/DefenseSystem.cs`
- **클래스 목적 및 의도**: `Defense` 도메인의 핵심 비즈니스 로직 연산을 수행하는 핵심 시스템입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] type` (DefenseType): DefenseSystem의 내부 데이터 또는 의존 참조 변수
  - `[public] score` (float): DefenseSystem의 내부 데이터 또는 의존 참조 변수
  - `[public] weight` (float): DefenseSystem의 내부 데이터 또는 의존 참조 변수
  - `[private] candidates` (List<DefenseCandidate>): DefenseSystem의 내부 데이터 또는 의존 참조 변수
  - `[private] selected` (DefenseCandidate): DefenseSystem의 내부 데이터 또는 의존 참조 변수
  - `[private] candidates` (List<DefenseCandidate>): DefenseSystem의 내부 데이터 또는 의존 참조 변수
  - `[private] selected` (DefenseCandidate): DefenseSystem의 내부 데이터 또는 의존 참조 변수
  - `[private] result` (List<DefenseCandidate>): DefenseSystem의 내부 데이터 또는 의존 참조 변수
  - `[private] durability` (float): DefenseSystem의 내부 데이터 또는 의존 참조 변수
  - `[private] resistance` (float): DefenseSystem의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] DefenseCandidate(DefenseType type, float score, float weight)` ➔ `public`: DefenseCandidate 관련 로직 수행 함수
  - `[public] EvaluateEarlyReaction(Unit defender, Unit attacker, ThreatTileData threat)` ➔ `void`: EvaluateEarlyReaction 관련 로직 수행 함수
  - `[public] EvaluateImpactDefense(Unit defender, Unit attacker, float rawDamage)` ➔ `float`: EvaluateImpactDefense 관련 로직 수행 함수
  - `[private] ExecuteImpactDefense(defender, attacker, selected, rawDamage)` ➔ `return`: ExecuteImpactDefense 관련 로직 수행 함수
  - `[private] BuildDefenseCandidates(Unit defender, Unit attacker, ThreatTileData threat, bool isEarlyReaction)` ➔ `List<DefenseCandidate>`: BuildDefenseCandidates 관련 로직 수행 함수
  - `[private] SelectDefense(List<DefenseCandidate> list)` ➔ `DefenseCandidate`: SelectDefense 관련 로직 수행 함수
  - `[private] HasPathWithoutWalls(Unit defender, Vector2Int start, Vector2Int target, int maxRange)` ➔ `bool`: HasPathWithoutWalls 관련 로직 수행 함수
  - `[private] ExecuteEarlyReaction(Unit defender, Unit attacker, DefenseCandidate selected)` ➔ `void`: ExecuteEarlyReaction 관련 로직 수행 함수

#### 📄 클래스: `Hitbox` (struct)
- **경로**: `Script/Unit/Combat/Hitbox.cs`
- **클래스 목적 및 의도**: `Hitbox`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] center` (Vector2): Hitbox의 내부 데이터 또는 의존 참조 변수
  - `[public] size` (Vector2): Hitbox의 내부 데이터 또는 의존 참조 변수
  - `[public] rotation` (float): Hitbox의 내부 데이터 또는 의존 참조 변수
  - `[private] axes` (Vector2[]): Hitbox의 내부 데이터 또는 의존 참조 변수
  - `[private] angle1` (float): Hitbox의 내부 데이터 또는 의존 참조 변수
  - `[private] angle2` (float): Hitbox의 내부 데이터 또는 의존 참조 변수
  - `[private] corners1` (Vector2[]): Hitbox의 내부 데이터 또는 의존 참조 변수
  - `[private] corners2` (Vector2[]): Hitbox의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): Hitbox의 내부 데이터 또는 의존 참조 변수
  - `[private] true` (return): Hitbox의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ToRect()` ➔ `Rect`: ToRect 관련 로직 수행 함수
  - `[public] Overlaps(Hitbox other)` ➔ `bool`: Overlaps 관련 로직 수행 함수
  - `[private] GetCorners(Hitbox box, float angleRad)` ➔ `Vector2[]`: GetCorners 관련 로직 수행 함수
  - `[private] OverlapOnAxis(Vector2[] corners1, Vector2[] corners2, Vector2 axis)` ➔ `bool`: OverlapOnAxis 관련 로직 수행 함수
  - `[public] CalculateOverlapArea(Hitbox other)` ➔ `float`: CalculateOverlapArea 관련 로직 수행 함수
  - `[public] CalculateOverlapRatio(Hitbox other)` ➔ `float`: CalculateOverlapRatio 관련 로직 수행 함수
  - `[public] IsInside(Vector2Int pos, Hitbox box)` ➔ `bool`: IsInside 관련 로직 수행 함수

#### 📄 클래스: `Projectile` (class)
- **경로**: `Script/Unit/Combat/Projectile.cs`
- **클래스 목적 및 의도**: `Projectile`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoBehaviour` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _attacker` (Unit): Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] _skillData` (SkillData): Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] _logicalCollider` (Hitbox): Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] _moveDir` (Vector2): Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] _startPos` (Vector2): Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] _maxDistance` (float): Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] _isInitialized` (bool): Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] _hitTargets` (HashSet<Unit>): Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] angle` (float): Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] currentSpeed` (float): Projectile의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Init(Unit attacker, SkillData skillData, Hitbox initialHitbox, Vector2 moveDir, float maxDistance)` ➔ `void`: Init 관련 로직 수행 함수
  - `[private] Update()` ➔ `void`: Update 관련 로직 수행 함수
  - `[private] ApplyHitEffect(Unit enemy, float overlapRatio)` ➔ `void`: ApplyHitEffect 관련 로직 수행 함수
  - `[private] DestroyProjectile()` ➔ `void`: DestroyProjectile 관련 로직 수행 함수
  - `[private] GetVisualPosition(Vector2 logicalPos)` ➔ `Vector3`: GetVisualPosition 관련 로직 수행 함수
  - `[private] OnDrawGizmos()` ➔ `void`: OnDrawGizmos 관련 로직 수행 함수

#### 📄 클래스: `ThreatShape` (enum)
- **경로**: `Script/Unit/Combat/ThreatTileData.cs`
- **클래스 목적 및 의도**: `ThreatShape`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] shape` (ThreatShape): ThreatShape의 내부 데이터 또는 의존 참조 변수
  - `[public] range` (int): ThreatShape의 내부 데이터 또는 의존 참조 변수
  - `[public] width` (int): ThreatShape의 내부 데이터 또는 의존 참조 변수
  - `[public] depth` (int): ThreatShape의 내부 데이터 또는 의존 참조 변수
  - `[public] color` (Color): ThreatShape의 내부 데이터 또는 의존 참조 변수
  - `[public] hitbox` (Hitbox): ThreatShape의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Create()` ➔ `ThreatTileData`: Create 관련 로직 수행 함수

#### 📄 클래스: `ThreatTileData` (class)
- **경로**: `Script/Unit/Combat/ThreatTileData.cs`
- **클래스 목적 및 의도**: `ThreatTileData` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] shape` (ThreatShape): ThreatTileData의 내부 데이터 또는 의존 참조 변수
  - `[public] range` (int): ThreatTileData의 내부 데이터 또는 의존 참조 변수
  - `[public] width` (int): ThreatTileData의 내부 데이터 또는 의존 참조 변수
  - `[public] depth` (int): ThreatTileData의 내부 데이터 또는 의존 참조 변수
  - `[public] color` (Color): ThreatTileData의 내부 데이터 또는 의존 참조 변수
  - `[public] hitbox` (Hitbox): ThreatTileData의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Create()` ➔ `ThreatTileData`: Create 관련 로직 수행 함수

---

### 📦 Package: `Unit/Core`

**모듈 내 포함 스크립트 수**: 41개

#### 📄 클래스: `AIStateComponent` (class)
- **경로**: `Script/Unit/Core/AIStateComponent.cs`
- **클래스 목적 및 의도**: `AIStateComponent`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IUnitComponent` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _owner` (Unit): AIStateComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] AIWeightState` (UnitAIWeightState): AIStateComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] reactedAttackers` (HashSet<Unit>): AIStateComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] currentReactionWindow` (float): AIStateComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] reactingThreat` (ThreatTileData): AIStateComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] reactingAttacker` (Unit): AIStateComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] currentThreat` (ThreatTileData): AIStateComponent의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] AIStateComponent()` ➔ `public`: AIStateComponent 관련 로직 수행 함수
  - `[private] AIStateComponent(Unit owner)` ➔ `public`: AIStateComponent 관련 로직 수행 함수
  - `[public] OnUpdate(float deltaTime)` ➔ `void`: OnUpdate 관련 로직 수행 함수
  - `[public] OnDespawn()` ➔ `void`: OnDespawn 관련 로직 수행 함수

#### 📄 클래스: `BaseStatComponent` (class)
- **경로**: `Script/Unit/Core/BaseStatComponent.cs`
- **클래스 목적 및 의도**: `BaseStatComponent`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IUnitComponent` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _owner` (Unit): BaseStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] agility` (float): BaseStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] sense` (float): BaseStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] sterngth` (float): BaseStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] Durability` (float): BaseStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] walkSpeed` (float): BaseStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] reaction` (float): BaseStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] statusResistance` (float): BaseStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] leadershipRange` (float): BaseStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] charisma` (float): BaseStatComponent의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] BaseStatComponent()` ➔ `public`: BaseStatComponent 관련 로직 수행 함수
  - `[private] BaseStatComponent(Unit owner)` ➔ `public`: BaseStatComponent 관련 로직 수행 함수
  - `[public] OnUpdate(float deltaTime)` ➔ `void`: OnUpdate 관련 로직 수행 함수
  - `[public] OnDespawn()` ➔ `void`: OnDespawn 관련 로직 수행 함수

#### 📄 클래스: `CombatStatComponent` (class)
- **경로**: `Script/Unit/Core/CombatStatComponent.cs`
- **클래스 목적 및 의도**: `CombatStatComponent`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IUnitComponent` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _owner` (Unit): CombatStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] physicalAttack` (float): CombatStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] magicalAttack` (float): CombatStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] physicalDefense` (float): CombatStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] magicalDefense` (float): CombatStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] criticalChance` (float): CombatStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] attackspeed` (float): CombatStatComponent의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] CombatStatComponent()` ➔ `public`: CombatStatComponent 관련 로직 수행 함수
  - `[private] CombatStatComponent(Unit owner)` ➔ `public`: CombatStatComponent 관련 로직 수행 함수
  - `[public] OnUpdate(float deltaTime)` ➔ `void`: OnUpdate 관련 로직 수행 함수
  - `[public] OnDespawn()` ➔ `void`: OnDespawn 관련 로직 수행 함수

#### 📄 클래스: `CombatStateComponent` (class)
- **경로**: `Script/Unit/Core/CombatStateComponent.cs`
- **클래스 목적 및 의도**: `CombatStateComponent`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IUnitComponent` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _owner` (Unit): CombatStateComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] State` (UnitCombatState): CombatStateComponent의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] CombatStateComponent()` ➔ `public`: CombatStateComponent 관련 로직 수행 함수
  - `[private] CombatStateComponent(Unit owner)` ➔ `public`: CombatStateComponent 관련 로직 수행 함수
  - `[public] OnUpdate(float deltaTime)` ➔ `void`: OnUpdate 관련 로직 수행 함수
  - `[public] OnDespawn()` ➔ `void`: OnDespawn 관련 로직 수행 함수

#### 📄 클래스: `FactionData` (class)
- **경로**: `Script/Unit/Core/FactionData.cs`
- **클래스 목적 및 의도**: `FactionData` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] discoveredMap` (int[][,]): FactionData의 내부 데이터 또는 의존 참조 변수
  - `[public] spottedEnemyUnits` (List<Unit>): FactionData의 내부 데이터 또는 의존 참조 변수
  - `[private] floorCount` (int): FactionData의 내부 데이터 또는 의존 참조 변수
  - `[private] w` (int): FactionData의 내부 데이터 또는 의존 참조 변수
  - `[private] h` (int): FactionData의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] FactionData()` ➔ `public`: FactionData 관련 로직 수행 함수
  - `[public] InitMap(CreateMap cmap)` ➔ `void`: InitMap 관련 로직 수행 함수

#### 📄 클래스: `FactionType` (enum)
- **경로**: `Script/Unit/Core/FactionType.cs`
- **클래스 목적 및 의도**: `FactionType`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `HealthComponent` (class)
- **경로**: `Script/Unit/Core/HealthComponent.cs`
- **클래스 목적 및 의도**: `HealthComponent`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IUnitComponent` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _owner` (Unit): HealthComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] maxHp` (float): HealthComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] hp` (float): HealthComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] maxMp` (float): HealthComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] mp` (float): HealthComponent의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] HealthComponent()` ➔ `public`: HealthComponent 관련 로직 수행 함수
  - `[private] HealthComponent(Unit owner)` ➔ `public`: HealthComponent 관련 로직 수행 함수
  - `[public] OnUpdate(float deltaTime)` ➔ `void`: OnUpdate 관련 로직 수행 함수
  - `[public] OnDespawn()` ➔ `void`: OnDespawn 관련 로직 수행 함수

#### 📄 클래스: `HumanFactionBehavior` (class)
- **경로**: `Script/Unit/Core/HumanFactionBehavior.cs`
- **클래스 목적 및 의도**: `HumanFactionBehavior`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IFactionBehavior` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[public] IsEnemy(IFactionBehavior other)` ➔ `bool`: IsEnemy 관련 로직 수행 함수
  - `[public] OnUpdate(Unit unit)` ➔ `void`: OnUpdate 관련 로직 수행 함수
  - `[public] OnDeath(Unit unit, Unit killer)` ➔ `void`: OnDeath 관련 로직 수행 함수
  - `[public] OnEnterRoom(Unit unit, Room room)` ➔ `void`: OnEnterRoom 관련 로직 수행 함수

#### 📄 클래스: `IFactionBehavior` (interface)
- **경로**: `Script/Unit/Core/IFactionBehavior.cs`
- **클래스 목적 및 의도**: `IFactionBehavior`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] IsEnemy(IFactionBehavior other)` ➔ `bool`: IsEnemy 관련 로직 수행 함수
  - `[private] OnUpdate(Unit unit)` ➔ `void`: OnUpdate 관련 로직 수행 함수
  - `[private] OnDeath(Unit unit, Unit killer)` ➔ `void`: OnDeath 관련 로직 수행 함수
  - `[private] OnEnterRoom(Unit unit, Room room)` ➔ `void`: OnEnterRoom 관련 로직 수행 함수

#### 📄 클래스: `IOffenseQuery` (interface)
- **경로**: `Script/Unit/Core/IOffenseQuery.cs`
- **클래스 목적 및 의도**: `IOffenseQuery`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] GetUnitsInRoom(RectInt bounds)` ➔ `IReadOnlyList<Unit>`: GetUnitsInRoom 관련 로직 수행 함수

#### 📄 클래스: `IPerceptible` (interface)
- **경로**: `Script/Unit/Core/IPerceptible.cs`
- **클래스 목적 및 의도**: `IPerceptible`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `ITargetable` (interface)
- **경로**: `Script/Unit/Core/ITargetable.cs`
- **클래스 목적 및 의도**: `ITargetable`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] TakePhysicalDamage(float rawDamage, ITargetable attacker)` ➔ `void`: TakePhysicalDamage 관련 로직 수행 함수
  - `[private] TakeMagicalDamage(float rawDamage, ITargetable attacker)` ➔ `void`: TakeMagicalDamage 관련 로직 수행 함수
  - `[private] TakeMentalDamage(float rawDamage, ITargetable attacker)` ➔ `void`: TakeMentalDamage 관련 로직 수행 함수
  - `[private] ApplyDirectDamage(ITargetable attacker, float multiplier = 1f)` ➔ `void`: ApplyDirectDamage 관련 로직 수행 함수

#### 📄 클래스: `IUnitComponent` (interface)
- **경로**: `Script/Unit/Core/IUnitComponent.cs`
- **클래스 목적 및 의도**: `IUnitComponent`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] OnUpdate(float deltaTime)` ➔ `void`: OnUpdate 관련 로직 수행 함수
  - `[private] OnDespawn()` ➔ `void`: OnDespawn 관련 로직 수행 함수

#### 📄 클래스: `MemoryComponent` (class)
- **경로**: `Script/Unit/Core/MemoryComponent.cs`
- **클래스 목적 및 의도**: `MemoryComponent`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IUnitComponent` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _owner` (Unit): MemoryComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] personalMap` (PersonalMapKnowledge): MemoryComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] collectedObjects` (List<string>): MemoryComponent의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] MemoryComponent()` ➔ `public`: MemoryComponent 관련 로직 수행 함수
  - `[private] MemoryComponent(Unit owner)` ➔ `public`: MemoryComponent 관련 로직 수행 함수
  - `[public] OnUpdate(float deltaTime)` ➔ `void`: OnUpdate 관련 로직 수행 함수
  - `[public] OnDespawn()` ➔ `void`: OnDespawn 관련 로직 수행 함수

#### 📄 클래스: `OffenseProcessor` (class)
- **경로**: `Script/Unit/Core/OffenseProcessor.cs`
- **클래스 목적 및 의도**: `OffenseProcessor`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] _colorizer` (IMapColorizer): OffenseProcessor의 내부 데이터 또는 의존 참조 변수
  - `[private] hasPlayer` (bool): OffenseProcessor의 내부 데이터 또는 의존 참조 변수
  - `[private] hasWild` (bool): OffenseProcessor의 내부 데이터 또는 의존 참조 변수
  - `[private] roomUnits` (var): OffenseProcessor의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] OffenseProcessor(IMapColorizer colorizer)` ➔ `public`: OffenseProcessor 관련 로직 수행 함수
  - `[public] StartOffense(Room room, Unit playerUnit)` ➔ `void`: StartOffense 관련 로직 수행 함수
  - `[public] UpdateProcess()` ➔ `void`: UpdateProcess 관련 로직 수행 함수
  - `[private] FailOffense()` ➔ `void`: FailOffense 관련 로직 수행 함수
  - `[private] OnOffenseSuccess(Room room)` ➔ `void`: OnOffenseSuccess 관련 로직 수행 함수

#### 📄 클래스: `PartyComponent` (class)
- **경로**: `Script/Unit/Core/PartyComponent.cs`
- **클래스 목적 및 의도**: `PartyComponent`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IUnitComponent` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _owner` (Unit): PartyComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] party` (Party): PartyComponent의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] PartyComponent()` ➔ `public`: PartyComponent 관련 로직 수행 함수
  - `[private] PartyComponent(Unit owner)` ➔ `public`: PartyComponent 관련 로직 수행 함수
  - `[public] OnUpdate(float deltaTime)` ➔ `void`: OnUpdate 관련 로직 수행 함수
  - `[public] OnDespawn()` ➔ `void`: OnDespawn 관련 로직 수행 함수

#### 📄 클래스: `PerceptionComponent` (class)
- **경로**: `Script/Unit/Core/PerceptionComponent.cs`
- **클래스 목적 및 의도**: `PerceptionComponent`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IUnitComponent` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _owner` (Unit): PerceptionComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] State` (UnitPerceptionState): PerceptionComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] IsAlert` (bool): PerceptionComponent의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] PerceptionComponent()` ➔ `public`: PerceptionComponent 관련 로직 수행 함수
  - `[private] PerceptionComponent(Unit owner)` ➔ `public`: PerceptionComponent 관련 로직 수행 함수
  - `[public] NotifyPerceptionSuspiciousChanged(bool wasSuspicious, bool nowSuspicious)` ➔ `void`: NotifyPerceptionSuspiciousChanged 관련 로직 수행 함수
  - `[public] RemovePerceptionRecord(object key)` ➔ `void`: RemovePerceptionRecord 관련 로직 수행 함수
  - `[public] OnUpdate(float deltaTime)` ➔ `void`: OnUpdate 관련 로직 수행 함수
  - `[public] OnDespawn()` ➔ `void`: OnDespawn 관련 로직 수행 함수

#### 📄 클래스: `PlayerMonsterBehavior` (class)
- **경로**: `Script/Unit/Core/PlayerMonsterBehavior.cs`
- **클래스 목적 및 의도**: `PlayerMonsterBehavior`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IFactionBehavior` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[public] IsEnemy(IFactionBehavior other)` ➔ `bool`: IsEnemy 관련 로직 수행 함수
  - `[public] OnUpdate(Unit unit)` ➔ `void`: OnUpdate 관련 로직 수행 함수
  - `[public] OnDeath(Unit unit, Unit killer)` ➔ `void`: OnDeath 관련 로직 수행 함수
  - `[public] OnEnterRoom(Unit unit, Room room)` ➔ `void`: OnEnterRoom 관련 로직 수행 함수

#### 📄 클래스: `ResourceAccumulator` (class)
- **경로**: `Script/Unit/Core/ResourceAccumulator.cs`
- **클래스 목적 및 의도**: `ResourceAccumulator`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] _instance` (ResourceAccumulator): ResourceAccumulator의 내부 데이터 또는 의존 참조 변수
  - `[private] _instance` (return): ResourceAccumulator의 내부 데이터 또는 의존 참조 변수
  - `[private] _accumulatedResourceB` (int): ResourceAccumulator의 내부 데이터 또는 의존 참조 변수
  - `[public] AccumulatedResourceB` (int): ResourceAccumulator의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] AccumulateResourceB(int amount)` ➔ `void`: AccumulateResourceB 관련 로직 수행 함수
  - `[public] CommitResourceB()` ➔ `void`: CommitResourceB 관련 로직 수행 함수
  - `[public] ClearResourceB()` ➔ `void`: ClearResourceB 관련 로직 수행 함수

#### 📄 클래스: `RoomType` (enum)
- **경로**: `Script/Unit/Core/Room.cs`
- **클래스 목적 및 의도**: `RoomType`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] rx` (int): RoomType의 내부 데이터 또는 의존 참조 변수
  - `[private] ry` (int): RoomType의 내부 데이터 또는 의존 참조 변수
  - `[private] _containedUnits` (List<Unit>): RoomType의 내부 데이터 또는 의존 참조 변수
  - `[public] ContainedUnits` (IReadOnlyList<Unit>): RoomType의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] GetRandomPosInRoom()` ➔ `Vector2Int`: GetRandomPosInRoom 관련 로직 수행 함수
  - `[public] AddUnit(Unit unit)` ➔ `void`: AddUnit 관련 로직 수행 함수
  - `[public] RemoveUnit(Unit unit)` ➔ `void`: RemoveUnit 관련 로직 수행 함수

#### 📄 클래스: `Room` (class)
- **경로**: `Script/Unit/Core/Room.cs`
- **클래스 목적 및 의도**: `Room`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] rx` (int): Room의 내부 데이터 또는 의존 참조 변수
  - `[private] ry` (int): Room의 내부 데이터 또는 의존 참조 변수
  - `[private] _containedUnits` (List<Unit>): Room의 내부 데이터 또는 의존 참조 변수
  - `[public] ContainedUnits` (IReadOnlyList<Unit>): Room의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] GetRandomPosInRoom()` ➔ `Vector2Int`: GetRandomPosInRoom 관련 로직 수행 함수
  - `[public] AddUnit(Unit unit)` ➔ `void`: AddUnit 관련 로직 수행 함수
  - `[public] RemoveUnit(Unit unit)` ➔ `void`: RemoveUnit 관련 로직 수행 함수

#### 📄 클래스: `StatusEffectsComponent` (class)
- **경로**: `Script/Unit/Core/StatusEffectsComponent.cs`
- **클래스 목적 및 의도**: `StatusEffectsComponent`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IUnitComponent` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _owner` (Unit): StatusEffectsComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] State` (UnitStatusEffects): StatusEffectsComponent의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] StatusEffectsComponent()` ➔ `public`: StatusEffectsComponent 관련 로직 수행 함수
  - `[private] StatusEffectsComponent(Unit owner)` ➔ `public`: StatusEffectsComponent 관련 로직 수행 함수
  - `[public] OnUpdate(float deltaTime)` ➔ `void`: OnUpdate 관련 로직 수행 함수
  - `[public] OnDespawn()` ➔ `void`: OnDespawn 관련 로직 수행 함수

#### 📄 클래스: `Unit` (class)
- **경로**: `Script/Unit/Core/Unit.cs`
- **클래스 목적 및 의도**: `Unit`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `ScriptableObject` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] Components` (List<IUnitComponent>): Unit의 내부 데이터 또는 의존 참조 변수
  - `[private] null` (return): Unit의 내부 데이터 또는 의존 참조 변수
  - `[private] _healthComp` (HealthComponent): Unit의 내부 데이터 또는 의존 참조 변수
  - `[private] _combatStateComp` (CombatStateComponent): Unit의 내부 데이터 또는 의존 참조 변수
  - `[private] _combatStatComp` (CombatStatComponent): Unit의 내부 데이터 또는 의존 참조 변수
  - `[private] _perceptionComp` (PerceptionComponent): Unit의 내부 데이터 또는 의존 참조 변수
  - `[private] _visionStatComp` (VisionStatComponent): Unit의 내부 데이터 또는 의존 참조 변수
  - `[private] _baseStatComp` (BaseStatComponent): Unit의 내부 데이터 또는 의존 참조 변수
  - `[private] _statusEffectsComp` (StatusEffectsComponent): Unit의 내부 데이터 또는 의존 참조 변수
  - `[private] _aiStateComp` (AIStateComponent): Unit의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] OnEnable()` ➔ `void`: OnEnable 관련 로직 수행 함수

#### 📄 클래스: `Human` (class)
- **경로**: `Script/Unit/Core/Unit.cs`
- **클래스 목적 및 의도**: `Human`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `UnitFunction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] Components` (List<IUnitComponent>): Human의 내부 데이터 또는 의존 참조 변수
  - `[private] null` (return): Human의 내부 데이터 또는 의존 참조 변수
  - `[private] _healthComp` (HealthComponent): Human의 내부 데이터 또는 의존 참조 변수
  - `[private] _combatStateComp` (CombatStateComponent): Human의 내부 데이터 또는 의존 참조 변수
  - `[private] _combatStatComp` (CombatStatComponent): Human의 내부 데이터 또는 의존 참조 변수
  - `[private] _perceptionComp` (PerceptionComponent): Human의 내부 데이터 또는 의존 참조 변수
  - `[private] _visionStatComp` (VisionStatComponent): Human의 내부 데이터 또는 의존 참조 변수
  - `[private] _baseStatComp` (BaseStatComponent): Human의 내부 데이터 또는 의존 참조 변수
  - `[private] _statusEffectsComp` (StatusEffectsComponent): Human의 내부 데이터 또는 의존 참조 변수
  - `[private] _aiStateComp` (AIStateComponent): Human의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] OnEnable()` ➔ `void`: OnEnable 관련 로직 수행 함수

#### 📄 클래스: `Monster` (class)
- **경로**: `Script/Unit/Core/Unit.cs`
- **클래스 목적 및 의도**: `Monster`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `UnitFunction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] Components` (List<IUnitComponent>): Monster의 내부 데이터 또는 의존 참조 변수
  - `[private] null` (return): Monster의 내부 데이터 또는 의존 참조 변수
  - `[private] _healthComp` (HealthComponent): Monster의 내부 데이터 또는 의존 참조 변수
  - `[private] _combatStateComp` (CombatStateComponent): Monster의 내부 데이터 또는 의존 참조 변수
  - `[private] _combatStatComp` (CombatStatComponent): Monster의 내부 데이터 또는 의존 참조 변수
  - `[private] _perceptionComp` (PerceptionComponent): Monster의 내부 데이터 또는 의존 참조 변수
  - `[private] _visionStatComp` (VisionStatComponent): Monster의 내부 데이터 또는 의존 참조 변수
  - `[private] _baseStatComp` (BaseStatComponent): Monster의 내부 데이터 또는 의존 참조 변수
  - `[private] _statusEffectsComp` (StatusEffectsComponent): Monster의 내부 데이터 또는 의존 참조 변수
  - `[private] _aiStateComp` (AIStateComponent): Monster의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] OnEnable()` ➔ `void`: OnEnable 관련 로직 수행 함수

#### 📄 클래스: `UnitStatusEffects` (struct)
- **경로**: `Script/Unit/Core/UnitComponents.cs`
- **클래스 목적 및 의도**: `UnitStatusEffects`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] stunDuration` (float): UnitStatusEffects의 내부 데이터 또는 의존 참조 변수
  - `[public] slowDuration` (float): UnitStatusEffects의 내부 데이터 또는 의존 참조 변수
  - `[public] poisonDuration` (float): UnitStatusEffects의 내부 데이터 또는 의존 참조 변수
  - `[public] burnDuration` (float): UnitStatusEffects의 내부 데이터 또는 의존 참조 변수
  - `[public] physicalAttackSpeed` (float): UnitStatusEffects의 내부 데이터 또는 의존 참조 변수
  - `[public] magicalCastSpeed` (float): UnitStatusEffects의 내부 데이터 또는 의존 참조 변수
  - `[public] actionCooldown` (float): UnitStatusEffects의 내부 데이터 또는 의존 참조 변수
  - `[public] skillCooldowns` (float[]): UnitStatusEffects의 내부 데이터 또는 의존 참조 변수
  - `[public] isHitThisTurn` (bool): UnitStatusEffects의 내부 데이터 또는 의존 참조 변수
  - `[public] oneTimeReactUsed` (bool): UnitStatusEffects의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `UnitCombatState` (struct)
- **경로**: `Script/Unit/Core/UnitComponents.cs`
- **클래스 목적 및 의도**: `UnitCombatState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] stunDuration` (float): UnitCombatState의 내부 데이터 또는 의존 참조 변수
  - `[public] slowDuration` (float): UnitCombatState의 내부 데이터 또는 의존 참조 변수
  - `[public] poisonDuration` (float): UnitCombatState의 내부 데이터 또는 의존 참조 변수
  - `[public] burnDuration` (float): UnitCombatState의 내부 데이터 또는 의존 참조 변수
  - `[public] physicalAttackSpeed` (float): UnitCombatState의 내부 데이터 또는 의존 참조 변수
  - `[public] magicalCastSpeed` (float): UnitCombatState의 내부 데이터 또는 의존 참조 변수
  - `[public] actionCooldown` (float): UnitCombatState의 내부 데이터 또는 의존 참조 변수
  - `[public] skillCooldowns` (float[]): UnitCombatState의 내부 데이터 또는 의존 참조 변수
  - `[public] isHitThisTurn` (bool): UnitCombatState의 내부 데이터 또는 의존 참조 변수
  - `[public] oneTimeReactUsed` (bool): UnitCombatState의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `UnitPerceptionState` (struct)
- **경로**: `Script/Unit/Core/UnitComponents.cs`
- **클래스 목적 및 의도**: `UnitPerceptionState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] stunDuration` (float): UnitPerceptionState의 내부 데이터 또는 의존 참조 변수
  - `[public] slowDuration` (float): UnitPerceptionState의 내부 데이터 또는 의존 참조 변수
  - `[public] poisonDuration` (float): UnitPerceptionState의 내부 데이터 또는 의존 참조 변수
  - `[public] burnDuration` (float): UnitPerceptionState의 내부 데이터 또는 의존 참조 변수
  - `[public] physicalAttackSpeed` (float): UnitPerceptionState의 내부 데이터 또는 의존 참조 변수
  - `[public] magicalCastSpeed` (float): UnitPerceptionState의 내부 데이터 또는 의존 참조 변수
  - `[public] actionCooldown` (float): UnitPerceptionState의 내부 데이터 또는 의존 참조 변수
  - `[public] skillCooldowns` (float[]): UnitPerceptionState의 내부 데이터 또는 의존 참조 변수
  - `[public] isHitThisTurn` (bool): UnitPerceptionState의 내부 데이터 또는 의존 참조 변수
  - `[public] oneTimeReactUsed` (bool): UnitPerceptionState의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `UnitAIWeightState` (struct)
- **경로**: `Script/Unit/Core/UnitComponents.cs`
- **클래스 목적 및 의도**: `UnitAIWeightState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] stunDuration` (float): UnitAIWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] slowDuration` (float): UnitAIWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] poisonDuration` (float): UnitAIWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] burnDuration` (float): UnitAIWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] physicalAttackSpeed` (float): UnitAIWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] magicalCastSpeed` (float): UnitAIWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] actionCooldown` (float): UnitAIWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] skillCooldowns` (float[]): UnitAIWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] isHitThisTurn` (bool): UnitAIWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] oneTimeReactUsed` (bool): UnitAIWeightState의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `UnitFunction` (class)
- **경로**: `Script/Unit/Core/UnitFunction.cs`
- **클래스 목적 및 의도**: `UnitFunction`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `Unit, IVisionContext` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] prevHp` (float): UnitFunction의 내부 데이터 또는 의존 참조 변수
  - `[private] damage` (float): UnitFunction의 내부 데이터 또는 의존 참조 변수
  - `[private] damage` (float): UnitFunction의 내부 데이터 또는 의존 참조 변수
  - `[private] defenderIsHuman` (bool): UnitFunction의 내부 데이터 또는 의존 참조 변수
  - `[private] attackerIsHuman` (bool): UnitFunction의 내부 데이터 또는 의존 참조 변수
  - `[private] incidentId` (string): UnitFunction의 내부 데이터 또는 의존 참조 변수
  - `[private] attackerIdentified` (bool): UnitFunction의 내부 데이터 또는 의존 참조 변수
  - `[private] directionKnown` (bool): UnitFunction의 내부 데이터 또는 의존 참조 변수
  - `[private] dist` (float): UnitFunction의 내부 데이터 또는 의존 참조 변수
  - `[private] effectiveSpotting` (float): UnitFunction의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] TakeDamage(float damage)` ➔ `void`: TakeDamage 관련 로직 수행 함수
  - `[public] TakePhysicalDamage(float rawDamage, Unit attacker)` ➔ `void`: TakePhysicalDamage 관련 로직 수행 함수
  - `[public] TakeMagicalDamage(float rawDamage, Unit attacker)` ➔ `void`: TakeMagicalDamage 관련 로직 수행 함수
  - `[public] RecordHitWeightEvent(float appliedDamage, Unit attacker, float rawDamage)` ➔ `void`: RecordHitWeightEvent 관련 로직 수행 함수
  - `[private] ForceReidentifyAttacker(Unit attacker)` ➔ `void`: ForceReidentifyAttacker 관련 로직 수행 함수
  - `[private] IsFullyBlockedTowards(float angleRad, float maxDistance)` ➔ `bool`: IsFullyBlockedTowards 관련 로직 수행 함수

#### 📄 클래스: `Dir` (enum)
- **경로**: `Script/Unit/Core/UnitTypes.cs`
- **클래스 목적 및 의도**: `Dir`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] typeName` (string): Dir의 내부 데이터 또는 의존 참조 변수
  - `[public] footprint` (Vector2): Dir의 내부 데이터 또는 의존 참조 변수
  - `[public] BASE_REACTION_TIME_MS` (float): Dir의 내부 데이터 또는 의존 참조 변수
  - `[public] MIN_REACTION_TIME_MS` (float): Dir의 내부 데이터 또는 의존 참조 변수
  - `[public] MAX_REACTION_TIME_MS` (float): Dir의 내부 데이터 또는 의존 참조 변수
  - `[public] BLOCK_PREPARE_TIME_MS` (float): Dir의 내부 데이터 또는 의존 참조 변수
  - `[public] DODGE_PREPARE_TIME_MS` (float): Dir의 내부 데이터 또는 의존 참조 변수
  - `[public] BLINK_PREPARE_TIME_MS` (float): Dir의 내부 데이터 또는 의존 참조 변수
  - `[public] PARRY_PREPARE_TIME_MS` (float): Dir의 내부 데이터 또는 의존 참조 변수
  - `[public] MIN_DEFENSE_SUCCESS_RATE` (float): Dir의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Knight()` ➔ `public`: Knight 관련 로직 수행 함수
  - `[private] HumanBaseType()` ➔ `public`: HumanBaseType 관련 로직 수행 함수
  - `[private] MeleeTank()` ➔ `public`: MeleeTank 관련 로직 수행 함수
  - `[private] WildBaseType()` ➔ `public`: WildBaseType 관련 로직 수행 함수
  - `[private] Archer()` ➔ `public`: Archer 관련 로직 수행 함수

#### 📄 클래스: `UnitType` (class)
- **경로**: `Script/Unit/Core/UnitTypes.cs`
- **클래스 목적 및 의도**: `UnitType`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] typeName` (string): UnitType의 내부 데이터 또는 의존 참조 변수
  - `[public] footprint` (Vector2): UnitType의 내부 데이터 또는 의존 참조 변수
  - `[public] BASE_REACTION_TIME_MS` (float): UnitType의 내부 데이터 또는 의존 참조 변수
  - `[public] MIN_REACTION_TIME_MS` (float): UnitType의 내부 데이터 또는 의존 참조 변수
  - `[public] MAX_REACTION_TIME_MS` (float): UnitType의 내부 데이터 또는 의존 참조 변수
  - `[public] BLOCK_PREPARE_TIME_MS` (float): UnitType의 내부 데이터 또는 의존 참조 변수
  - `[public] DODGE_PREPARE_TIME_MS` (float): UnitType의 내부 데이터 또는 의존 참조 변수
  - `[public] BLINK_PREPARE_TIME_MS` (float): UnitType의 내부 데이터 또는 의존 참조 변수
  - `[public] PARRY_PREPARE_TIME_MS` (float): UnitType의 내부 데이터 또는 의존 참조 변수
  - `[public] MIN_DEFENSE_SUCCESS_RATE` (float): UnitType의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Knight()` ➔ `public`: Knight 관련 로직 수행 함수
  - `[private] HumanBaseType()` ➔ `public`: HumanBaseType 관련 로직 수행 함수
  - `[private] MeleeTank()` ➔ `public`: MeleeTank 관련 로직 수행 함수
  - `[private] WildBaseType()` ➔ `public`: WildBaseType 관련 로직 수행 함수
  - `[private] Archer()` ➔ `public`: Archer 관련 로직 수행 함수

#### 📄 클래스: `Knight` (class)
- **경로**: `Script/Unit/Core/UnitTypes.cs`
- **클래스 목적 및 의도**: `Knight`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `UnitType` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] typeName` (string): Knight의 내부 데이터 또는 의존 참조 변수
  - `[public] footprint` (Vector2): Knight의 내부 데이터 또는 의존 참조 변수
  - `[public] BASE_REACTION_TIME_MS` (float): Knight의 내부 데이터 또는 의존 참조 변수
  - `[public] MIN_REACTION_TIME_MS` (float): Knight의 내부 데이터 또는 의존 참조 변수
  - `[public] MAX_REACTION_TIME_MS` (float): Knight의 내부 데이터 또는 의존 참조 변수
  - `[public] BLOCK_PREPARE_TIME_MS` (float): Knight의 내부 데이터 또는 의존 참조 변수
  - `[public] DODGE_PREPARE_TIME_MS` (float): Knight의 내부 데이터 또는 의존 참조 변수
  - `[public] BLINK_PREPARE_TIME_MS` (float): Knight의 내부 데이터 또는 의존 참조 변수
  - `[public] PARRY_PREPARE_TIME_MS` (float): Knight의 내부 데이터 또는 의존 참조 변수
  - `[public] MIN_DEFENSE_SUCCESS_RATE` (float): Knight의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Knight()` ➔ `public`: Knight 관련 로직 수행 함수
  - `[private] HumanBaseType()` ➔ `public`: HumanBaseType 관련 로직 수행 함수
  - `[private] MeleeTank()` ➔ `public`: MeleeTank 관련 로직 수행 함수
  - `[private] WildBaseType()` ➔ `public`: WildBaseType 관련 로직 수행 함수
  - `[private] Archer()` ➔ `public`: Archer 관련 로직 수행 함수

#### 📄 클래스: `HumanBaseType` (class)
- **경로**: `Script/Unit/Core/UnitTypes.cs`
- **클래스 목적 및 의도**: `HumanBaseType`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `UnitType` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] typeName` (string): HumanBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] footprint` (Vector2): HumanBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] BASE_REACTION_TIME_MS` (float): HumanBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] MIN_REACTION_TIME_MS` (float): HumanBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] MAX_REACTION_TIME_MS` (float): HumanBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] BLOCK_PREPARE_TIME_MS` (float): HumanBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] DODGE_PREPARE_TIME_MS` (float): HumanBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] BLINK_PREPARE_TIME_MS` (float): HumanBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] PARRY_PREPARE_TIME_MS` (float): HumanBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] MIN_DEFENSE_SUCCESS_RATE` (float): HumanBaseType의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Knight()` ➔ `public`: Knight 관련 로직 수행 함수
  - `[private] HumanBaseType()` ➔ `public`: HumanBaseType 관련 로직 수행 함수
  - `[private] MeleeTank()` ➔ `public`: MeleeTank 관련 로직 수행 함수
  - `[private] WildBaseType()` ➔ `public`: WildBaseType 관련 로직 수행 함수
  - `[private] Archer()` ➔ `public`: Archer 관련 로직 수행 함수

#### 📄 클래스: `MeleeTank` (class)
- **경로**: `Script/Unit/Core/UnitTypes.cs`
- **클래스 목적 및 의도**: `MeleeTank`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `UnitType` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] typeName` (string): MeleeTank의 내부 데이터 또는 의존 참조 변수
  - `[public] footprint` (Vector2): MeleeTank의 내부 데이터 또는 의존 참조 변수
  - `[public] BASE_REACTION_TIME_MS` (float): MeleeTank의 내부 데이터 또는 의존 참조 변수
  - `[public] MIN_REACTION_TIME_MS` (float): MeleeTank의 내부 데이터 또는 의존 참조 변수
  - `[public] MAX_REACTION_TIME_MS` (float): MeleeTank의 내부 데이터 또는 의존 참조 변수
  - `[public] BLOCK_PREPARE_TIME_MS` (float): MeleeTank의 내부 데이터 또는 의존 참조 변수
  - `[public] DODGE_PREPARE_TIME_MS` (float): MeleeTank의 내부 데이터 또는 의존 참조 변수
  - `[public] BLINK_PREPARE_TIME_MS` (float): MeleeTank의 내부 데이터 또는 의존 참조 변수
  - `[public] PARRY_PREPARE_TIME_MS` (float): MeleeTank의 내부 데이터 또는 의존 참조 변수
  - `[public] MIN_DEFENSE_SUCCESS_RATE` (float): MeleeTank의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Knight()` ➔ `public`: Knight 관련 로직 수행 함수
  - `[private] HumanBaseType()` ➔ `public`: HumanBaseType 관련 로직 수행 함수
  - `[private] MeleeTank()` ➔ `public`: MeleeTank 관련 로직 수행 함수
  - `[private] WildBaseType()` ➔ `public`: WildBaseType 관련 로직 수행 함수
  - `[private] Archer()` ➔ `public`: Archer 관련 로직 수행 함수

#### 📄 클래스: `WildBaseType` (class)
- **경로**: `Script/Unit/Core/UnitTypes.cs`
- **클래스 목적 및 의도**: `WildBaseType`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `UnitType` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] typeName` (string): WildBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] footprint` (Vector2): WildBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] BASE_REACTION_TIME_MS` (float): WildBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] MIN_REACTION_TIME_MS` (float): WildBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] MAX_REACTION_TIME_MS` (float): WildBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] BLOCK_PREPARE_TIME_MS` (float): WildBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] DODGE_PREPARE_TIME_MS` (float): WildBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] BLINK_PREPARE_TIME_MS` (float): WildBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] PARRY_PREPARE_TIME_MS` (float): WildBaseType의 내부 데이터 또는 의존 참조 변수
  - `[public] MIN_DEFENSE_SUCCESS_RATE` (float): WildBaseType의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Knight()` ➔ `public`: Knight 관련 로직 수행 함수
  - `[private] HumanBaseType()` ➔ `public`: HumanBaseType 관련 로직 수행 함수
  - `[private] MeleeTank()` ➔ `public`: MeleeTank 관련 로직 수행 함수
  - `[private] WildBaseType()` ➔ `public`: WildBaseType 관련 로직 수행 함수
  - `[private] Archer()` ➔ `public`: Archer 관련 로직 수행 함수

#### 📄 클래스: `Archer` (class)
- **경로**: `Script/Unit/Core/UnitTypes.cs`
- **클래스 목적 및 의도**: `Archer`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `UnitType` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] typeName` (string): Archer의 내부 데이터 또는 의존 참조 변수
  - `[public] footprint` (Vector2): Archer의 내부 데이터 또는 의존 참조 변수
  - `[public] BASE_REACTION_TIME_MS` (float): Archer의 내부 데이터 또는 의존 참조 변수
  - `[public] MIN_REACTION_TIME_MS` (float): Archer의 내부 데이터 또는 의존 참조 변수
  - `[public] MAX_REACTION_TIME_MS` (float): Archer의 내부 데이터 또는 의존 참조 변수
  - `[public] BLOCK_PREPARE_TIME_MS` (float): Archer의 내부 데이터 또는 의존 참조 변수
  - `[public] DODGE_PREPARE_TIME_MS` (float): Archer의 내부 데이터 또는 의존 참조 변수
  - `[public] BLINK_PREPARE_TIME_MS` (float): Archer의 내부 데이터 또는 의존 참조 변수
  - `[public] PARRY_PREPARE_TIME_MS` (float): Archer의 내부 데이터 또는 의존 참조 변수
  - `[public] MIN_DEFENSE_SUCCESS_RATE` (float): Archer의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Knight()` ➔ `public`: Knight 관련 로직 수행 함수
  - `[private] HumanBaseType()` ➔ `public`: HumanBaseType 관련 로직 수행 함수
  - `[private] MeleeTank()` ➔ `public`: MeleeTank 관련 로직 수행 함수
  - `[private] WildBaseType()` ➔ `public`: WildBaseType 관련 로직 수행 함수
  - `[private] Archer()` ➔ `public`: Archer 관련 로직 수행 함수

#### 📄 클래스: `CombatConstants` (class)
- **경로**: `Script/Unit/Core/UnitTypes.cs`
- **클래스 목적 및 의도**: `CombatConstants`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] typeName` (string): CombatConstants의 내부 데이터 또는 의존 참조 변수
  - `[public] footprint` (Vector2): CombatConstants의 내부 데이터 또는 의존 참조 변수
  - `[public] BASE_REACTION_TIME_MS` (float): CombatConstants의 내부 데이터 또는 의존 참조 변수
  - `[public] MIN_REACTION_TIME_MS` (float): CombatConstants의 내부 데이터 또는 의존 참조 변수
  - `[public] MAX_REACTION_TIME_MS` (float): CombatConstants의 내부 데이터 또는 의존 참조 변수
  - `[public] BLOCK_PREPARE_TIME_MS` (float): CombatConstants의 내부 데이터 또는 의존 참조 변수
  - `[public] DODGE_PREPARE_TIME_MS` (float): CombatConstants의 내부 데이터 또는 의존 참조 변수
  - `[public] BLINK_PREPARE_TIME_MS` (float): CombatConstants의 내부 데이터 또는 의존 참조 변수
  - `[public] PARRY_PREPARE_TIME_MS` (float): CombatConstants의 내부 데이터 또는 의존 참조 변수
  - `[public] MIN_DEFENSE_SUCCESS_RATE` (float): CombatConstants의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Knight()` ➔ `public`: Knight 관련 로직 수행 함수
  - `[private] HumanBaseType()` ➔ `public`: HumanBaseType 관련 로직 수행 함수
  - `[private] MeleeTank()` ➔ `public`: MeleeTank 관련 로직 수행 함수
  - `[private] WildBaseType()` ➔ `public`: WildBaseType 관련 로직 수행 함수
  - `[private] Archer()` ➔ `public`: Archer 관련 로직 수행 함수

#### 📄 클래스: `VisionStatComponent` (class)
- **경로**: `Script/Unit/Core/VisionStatComponent.cs`
- **클래스 목적 및 의도**: `VisionStatComponent`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IUnitComponent` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _owner` (Unit): VisionStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] stealth` (float): VisionStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] spotting` (float): VisionStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] baseVisibility` (float): VisionStatComponent의 내부 데이터 또는 의존 참조 변수
  - `[public] attackVisibilityBoostTimer` (float): VisionStatComponent의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] VisionStatComponent()` ➔ `public`: VisionStatComponent 관련 로직 수행 함수
  - `[private] VisionStatComponent(Unit owner)` ➔ `public`: VisionStatComponent 관련 로직 수행 함수
  - `[public] OnUpdate(float deltaTime)` ➔ `void`: OnUpdate 관련 로직 수행 함수
  - `[public] OnDespawn()` ➔ `void`: OnDespawn 관련 로직 수행 함수

#### 📄 클래스: `WildBaseSpawnerComponent` (class)
- **경로**: `Script/Unit/Core/WildBaseSpawnerComponent.cs`
- **클래스 목적 및 의도**: `WildBaseSpawnerComponent`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IUnitComponent` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] debugVisual` (GameObject): WildBaseSpawnerComponent의 내부 데이터 또는 의존 참조 변수
  - `[private] _owner` (Unit): WildBaseSpawnerComponent의 내부 데이터 또는 의존 참조 변수
  - `[private] _targetRoom` (Room): WildBaseSpawnerComponent의 내부 데이터 또는 의존 참조 변수
  - `[private] _spawnInterval` (float): WildBaseSpawnerComponent의 내부 데이터 또는 의존 참조 변수
  - `[private] _cts` (CancellationTokenSource): WildBaseSpawnerComponent의 내부 데이터 또는 의존 참조 변수
  - `[private] monsterType` (UnitType): WildBaseSpawnerComponent의 내부 데이터 또는 의존 참조 변수
  - `[private] spawnPos` (Vector2Int): WildBaseSpawnerComponent의 내부 데이터 또는 의존 참조 변수
  - `[private] attempts` (int): WildBaseSpawnerComponent의 내부 데이터 또는 의존 참조 변수
  - `[private] monster` (Monster): WildBaseSpawnerComponent의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] WildBaseSpawnerComponent()` ➔ `public`: WildBaseSpawnerComponent 관련 로직 수행 함수
  - `[private] WildBaseSpawnerComponent(Unit owner, Room room)` ➔ `public`: WildBaseSpawnerComponent 관련 로직 수행 함수
  - `[private] SpawnLoop(CancellationToken token)` ➔ `UniTaskVoid`: SpawnLoop 관련 로직 수행 함수
  - `[public] OnUpdate(float deltaTime)` ➔ `void`: OnUpdate 관련 로직 수행 함수
  - `[private] SpawnMonster()` ➔ `void`: SpawnMonster 관련 로직 수행 함수
  - `[public] OnDespawn()` ➔ `void`: OnDespawn 관련 로직 수행 함수

#### 📄 클래스: `WildMonsterBehavior` (class)
- **경로**: `Script/Unit/Core/WildMonsterBehavior.cs`
- **클래스 목적 및 의도**: `WildMonsterBehavior`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IFactionBehavior` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] rewardAmount` (int): WildMonsterBehavior의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] IsEnemy(IFactionBehavior other)` ➔ `bool`: IsEnemy 관련 로직 수행 함수
  - `[public] OnUpdate(Unit unit)` ➔ `void`: OnUpdate 관련 로직 수행 함수
  - `[public] OnDeath(Unit unit, Unit killer)` ➔ `void`: OnDeath 관련 로직 수행 함수
  - `[public] OnEnterRoom(Unit unit, Room room)` ➔ `void`: OnEnterRoom 관련 로직 수행 함수

---

### 📦 Package: `Unit/Data`

**모듈 내 포함 스크립트 수**: 2개

#### 📄 클래스: `UnitStatsData` (class)
- **경로**: `Script/Unit/Data/UnitStatsData.cs`
- **클래스 목적 및 의도**: `UnitStatsData` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] skillName` (string): UnitStatsData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseDelayMs` (float): UnitStatsData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseCooldown` (float): UnitStatsData의 내부 데이터 또는 의존 참조 변수
  - `[public] cooldownSlot` (int): UnitStatsData의 내부 데이터 또는 의존 참조 변수
  - `[public] isProjectile` (bool): UnitStatsData의 내부 데이터 또는 의존 참조 변수
  - `[public] projectilePrefab` (GameObject): UnitStatsData의 내부 데이터 또는 의존 참조 변수
  - `[public] projectileSpeed` (float): UnitStatsData의 내부 데이터 또는 의존 참조 변수
  - `[public] isPiercing` (bool): UnitStatsData의 내부 데이터 또는 의존 참조 변수
  - `[public] hitEffectPrefab` (GameObject): UnitStatsData의 내부 데이터 또는 의존 참조 변수
  - `[public] hitShape` (string): UnitStatsData의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `SkillData` (class)
- **경로**: `Script/Unit/Data/UnitStatsData.cs`
- **클래스 목적 및 의도**: `SkillData` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] skillName` (string): SkillData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseDelayMs` (float): SkillData의 내부 데이터 또는 의존 참조 변수
  - `[public] baseCooldown` (float): SkillData의 내부 데이터 또는 의존 참조 변수
  - `[public] cooldownSlot` (int): SkillData의 내부 데이터 또는 의존 참조 변수
  - `[public] isProjectile` (bool): SkillData의 내부 데이터 또는 의존 참조 변수
  - `[public] projectilePrefab` (GameObject): SkillData의 내부 데이터 또는 의존 참조 변수
  - `[public] projectileSpeed` (float): SkillData의 내부 데이터 또는 의존 참조 변수
  - `[public] isPiercing` (bool): SkillData의 내부 데이터 또는 의존 참조 변수
  - `[public] hitEffectPrefab` (GameObject): SkillData의 내부 데이터 또는 의존 참조 변수
  - `[public] hitShape` (string): SkillData의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

---

### 📦 Package: `Unit/Debug`

**모듈 내 포함 스크립트 수**: 2개

#### 📄 클래스: `AreaBasedDamageValidator` (class)
- **경로**: `Script/Unit/Debug/AreaBasedDamageValidator.cs`
- **클래스 목적 및 의도**: `AreaBasedDamageValidator`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoBehaviour` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _gameSession` (GameSession): AreaBasedDamageValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] Session` (GameSession): AreaBasedDamageValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] testInterval` (float): AreaBasedDamageValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] testTimer` (float): AreaBasedDamageValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] showLogging` (bool): AreaBasedDamageValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] attackHitbox` (Hitbox): AreaBasedDamageValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] attackArea` (float): AreaBasedDamageValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] enemyHitbox` (Hitbox): AreaBasedDamageValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] enemyArea` (float): AreaBasedDamageValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] overlapArea` (float): AreaBasedDamageValidator의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Update()` ➔ `void`: Update 관련 로직 수행 함수
  - `[private] ValidateAreaBasedDamage()` ➔ `void`: ValidateAreaBasedDamage 관련 로직 수행 함수
  - `[public] TestOverlapCalculation()` ➔ `void`: TestOverlapCalculation 관련 로직 수행 함수
  - `[public] TestPartialVsFullHit()` ➔ `void`: TestPartialVsFullHit 관련 로직 수행 함수

#### 📄 클래스: `AttackAngleTestValidator` (class)
- **경로**: `Script/Unit/Debug/AttackAngleTestValidator.cs`
- **클래스 목적 및 의도**: `AttackAngleTestValidator` 로직의 동작 검증을 위한 플레이/단위 테스트 스크립트입니다.
- **상속 및 구현 관계**: `MonoBehaviour` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _gameSession` (GameSession): AttackAngleTestValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] Session` (GameSession): AttackAngleTestValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] testInterval` (float): AttackAngleTestValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] testTimer` (float): AttackAngleTestValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] enemies` (IEnumerable<Unit>): AttackAngleTestValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] dirToTarget` (Vector2): AttackAngleTestValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] expectedAngle` (float): AttackAngleTestValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] distance` (float): AttackAngleTestValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] attackRange` (float): AttackAngleTestValidator의 내부 데이터 또는 의존 참조 변수
  - `[private] angleDiff` (float): AttackAngleTestValidator의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Update()` ➔ `void`: Update 관련 로직 수행 함수
  - `[private] ValidateAttackAngleMechanics()` ➔ `void`: ValidateAttackAngleMechanics 관련 로직 수행 함수
  - `[private] ValidateMovementDirections()` ➔ `void`: ValidateMovementDirections 관련 로직 수행 함수
  - `[public] ValidateAttackRecognition()` ➔ `void`: ValidateAttackRecognition 관련 로직 수행 함수

---

### 📦 Package: `Unit/Exploration`

**모듈 내 포함 스크립트 수**: 8개

#### 📄 클래스: `AlertSearchState` (class)
- **경로**: `Script/Unit/Exploration/AlertSearchState.cs`
- **클래스 목적 및 의도**: `AlertSearchState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] TargetPosition` (Vector2Int?): AlertSearchState의 내부 데이터 또는 의존 참조 변수
  - `[public] IsPostCombatSweep` (bool): AlertSearchState의 내부 데이터 또는 의존 참조 변수
  - `[public] ElapsedSeconds` (float): AlertSearchState의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `ExplorationMath` (class)
- **경로**: `Script/Unit/Exploration/ExplorationMath.cs`
- **클래스 목적 및 의도**: `ExplorationMath`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] AlertMoveSpeedRatio` (float): ExplorationMath의 내부 데이터 또는 의존 참조 변수
  - `[public] AlertReactionSpeedRatio` (float): ExplorationMath의 내부 데이터 또는 의존 참조 변수
  - `[public] PostCombatAlertSeconds` (float): ExplorationMath의 내부 데이터 또는 의존 참조 변수
  - `[public] UnidentifiedAttackSearchSeconds` (float): ExplorationMath의 내부 데이터 또는 의존 참조 변수
  - `[public] SuspiciousTargetVisibilityBoostPerMove` (float): ExplorationMath의 내부 데이터 또는 의존 참조 변수
  - `[public] InvestigatePenaltyRatio` (float): ExplorationMath의 내부 데이터 또는 의존 참조 변수
  - `[public] InvestigateInterruptLossRatio` (float): ExplorationMath의 내부 데이터 또는 의존 참조 변수
  - `[public] InvestigateDurationSeconds` (float): ExplorationMath의 내부 데이터 또는 의존 참조 변수
  - `[public] TrapPenaltyRatio` (float): ExplorationMath의 내부 데이터 또는 의존 참조 변수
  - `[public] TrapDisarmInterruptLossRatio` (float): ExplorationMath의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] TrapDisarmSuccessRate(float concentration, int level, int understandingApplied)` ➔ `float`: TrapDisarmSuccessRate 관련 로직 수행 함수

#### 📄 클래스: `FormationState` (class)
- **경로**: `Script/Unit/Exploration/FormationState.cs`
- **클래스 목적 및 의도**: `FormationState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] EscortTarget` (Human): FormationState의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `InvestigationState` (class)
- **경로**: `Script/Unit/Exploration/InvestigationState.cs`
- **클래스 목적 및 의도**: `InvestigationState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] TargetObjectId` (string): InvestigationState의 내부 데이터 또는 의존 참조 변수
  - `[public] TargetPosition` (Vector3Int): InvestigationState의 내부 데이터 또는 의존 참조 변수
  - `[public] Progress01` (float): InvestigationState의 내부 데이터 또는 의존 참조 변수
  - `[public] PenaltyActive` (bool): InvestigationState의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `TrapPhase` (enum)
- **경로**: `Script/Unit/Exploration/TrapInteractionState.cs`
- **클래스 목적 및 의도**: `TrapPhase`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] Phase` (TrapPhase): TrapPhase의 내부 데이터 또는 의존 참조 변수
  - `[public] TrapObjectId` (string): TrapPhase의 내부 데이터 또는 의존 참조 변수
  - `[public] TrapPosition` (Vector3Int): TrapPhase의 내부 데이터 또는 의존 참조 변수
  - `[public] JoinWaitElapsed` (bool): TrapPhase의 내부 데이터 또는 의존 참조 변수
  - `[public] JoinWaitTimer` (float): TrapPhase의 내부 데이터 또는 의존 참조 변수
  - `[public] HigherRateJoiner` (Human): TrapPhase의 내부 데이터 또는 의존 참조 변수
  - `[public] DestroyProgressDamage` (float): TrapPhase의 내부 데이터 또는 의존 참조 변수
  - `[public] PenaltyActive` (bool): TrapPhase의 내부 데이터 또는 의존 참조 변수
  - `[public] DisarmProgress01` (float): TrapPhase의 내부 데이터 또는 의존 참조 변수
  - `[public] IsBlockingPath` (bool?): TrapPhase의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `TrapInteractionState` (class)
- **경로**: `Script/Unit/Exploration/TrapInteractionState.cs`
- **클래스 목적 및 의도**: `TrapInteractionState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] Phase` (TrapPhase): TrapInteractionState의 내부 데이터 또는 의존 참조 변수
  - `[public] TrapObjectId` (string): TrapInteractionState의 내부 데이터 또는 의존 참조 변수
  - `[public] TrapPosition` (Vector3Int): TrapInteractionState의 내부 데이터 또는 의존 참조 변수
  - `[public] JoinWaitElapsed` (bool): TrapInteractionState의 내부 데이터 또는 의존 참조 변수
  - `[public] JoinWaitTimer` (float): TrapInteractionState의 내부 데이터 또는 의존 참조 변수
  - `[public] HigherRateJoiner` (Human): TrapInteractionState의 내부 데이터 또는 의존 참조 변수
  - `[public] DestroyProgressDamage` (float): TrapInteractionState의 내부 데이터 또는 의존 참조 변수
  - `[public] PenaltyActive` (bool): TrapInteractionState의 내부 데이터 또는 의존 참조 변수
  - `[public] DisarmProgress01` (float): TrapInteractionState의 내부 데이터 또는 의존 참조 변수
  - `[public] IsBlockingPath` (bool?): TrapInteractionState의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `WaitReason` (enum)
- **경로**: `Script/Unit/Exploration/WaitState.cs`
- **클래스 목적 및 의도**: `WaitReason`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] Reason` (WaitReason): WaitReason의 내부 데이터 또는 의존 참조 변수
  - `[public] WaitPosition` (Vector2Int?): WaitReason의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `WaitState` (class)
- **경로**: `Script/Unit/Exploration/WaitState.cs`
- **클래스 목적 및 의도**: `WaitState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] Reason` (WaitReason): WaitState의 내부 데이터 또는 의존 참조 변수
  - `[public] WaitPosition` (Vector2Int?): WaitState의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

---

### 📦 Package: `Unit/Movement`

**모듈 내 포함 스크립트 수**: 4개

#### 📄 클래스: `AStarMovement` (class)
- **경로**: `Script/Unit/Movement/AStarMovement.cs`
- **클래스 목적 및 의도**: `AStarMovement`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IMovementAlgorithm` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] Pos` (Vector2Int): AStarMovement의 내부 데이터 또는 의존 참조 변수
  - `[public] Parent` (AStarNode): AStarMovement의 내부 데이터 또는 의존 참조 변수
  - `[public] GCost` (int): AStarMovement의 내부 데이터 또는 의존 참조 변수
  - `[public] HCost` (int): AStarMovement의 내부 데이터 또는 의존 참조 변수
  - `[public] FCost` (int): AStarMovement의 내부 데이터 또는 의존 참조 변수
  - `[private] myData` (FactionData): AStarMovement의 내부 데이터 또는 의존 참조 변수
  - `[private] floorIdx` (int): AStarMovement의 내부 데이터 또는 의존 참조 변수
  - `[private] mapW` (int): AStarMovement의 내부 데이터 또는 의존 참조 변수
  - `[private] mapH` (int): AStarMovement의 내부 데이터 또는 의존 참조 변수
  - `[private] startPos` (Vector2Int): AStarMovement의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] TryGetNextStep(Unit unit, Vector2Int targetPos, out Dir nextDir)` ➔ `bool`: TryGetNextStep 관련 로직 수행 함수
  - `[private] TryFallbackMove(unit, targetPos, out nextDir)` ➔ `return`: TryFallbackMove 관련 로직 수행 함수
  - `[protected] IsTileWalkable(Unit unit, Vector2Int currentPos, Vector2Int neighborPos, Vector2Int dirVec, FactionData myData, int mapW, int mapH, int floorIdx, Vector2Int targetPos, out bool isOccupied)` ➔ `bool`: IsTileWalkable 관련 로직 수행 함수

#### 📄 클래스: `AStarNode` (class)
- **경로**: `Script/Unit/Movement/AStarMovement.cs`
- **클래스 목적 및 의도**: `AStarNode`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] Pos` (Vector2Int): AStarNode의 내부 데이터 또는 의존 참조 변수
  - `[public] Parent` (AStarNode): AStarNode의 내부 데이터 또는 의존 참조 변수
  - `[public] GCost` (int): AStarNode의 내부 데이터 또는 의존 참조 변수
  - `[public] HCost` (int): AStarNode의 내부 데이터 또는 의존 참조 변수
  - `[public] FCost` (int): AStarNode의 내부 데이터 또는 의존 참조 변수
  - `[private] myData` (FactionData): AStarNode의 내부 데이터 또는 의존 참조 변수
  - `[private] floorIdx` (int): AStarNode의 내부 데이터 또는 의존 참조 변수
  - `[private] mapW` (int): AStarNode의 내부 데이터 또는 의존 참조 변수
  - `[private] mapH` (int): AStarNode의 내부 데이터 또는 의존 참조 변수
  - `[private] startPos` (Vector2Int): AStarNode의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] TryGetNextStep(Unit unit, Vector2Int targetPos, out Dir nextDir)` ➔ `bool`: TryGetNextStep 관련 로직 수행 함수
  - `[private] TryFallbackMove(unit, targetPos, out nextDir)` ➔ `return`: TryFallbackMove 관련 로직 수행 함수
  - `[protected] IsTileWalkable(Unit unit, Vector2Int currentPos, Vector2Int neighborPos, Vector2Int dirVec, FactionData myData, int mapW, int mapH, int floorIdx, Vector2Int targetPos, out bool isOccupied)` ➔ `bool`: IsTileWalkable 관련 로직 수행 함수

#### 📄 클래스: `IMovementAlgorithm` (interface)
- **경로**: `Script/Unit/Movement/IMovementAlgorithm.cs`
- **클래스 목적 및 의도**: `IMovementAlgorithm`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] TryGetNextStep(Unit unit, Vector2Int targetPos, out Dir nextDir)` ➔ `bool`: TryGetNextStep 관련 로직 수행 함수

#### 📄 클래스: `RoomConfinedMovement` (class)
- **경로**: `Script/Unit/Movement/RoomConfinedMovement.cs`
- **클래스 목적 및 의도**: `RoomConfinedMovement`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `AStarMovement` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _cachedRoom` (Room): RoomConfinedMovement의 내부 데이터 또는 의존 참조 변수
  - `[private] _lastCheckFloor` (int): RoomConfinedMovement의 내부 데이터 또는 의존 참조 변수
  - `[private] baseWalkable` (bool): RoomConfinedMovement의 내부 데이터 또는 의존 참조 변수
  - `[private] false` (return): RoomConfinedMovement의 내부 데이터 또는 의존 참조 변수
  - `[private] true` (return): RoomConfinedMovement의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[protected] IsTileWalkable(Unit unit, Vector2Int currentPos, Vector2Int neighborPos, Vector2Int dirVec, FactionData myData, int mapW, int mapH, int floorIdx, Vector2Int targetPos, out bool isOccupied)` ➔ `bool`: IsTileWalkable 관련 로직 수행 함수

---

### 📦 Package: `Unit/Party`

**모듈 내 포함 스크립트 수**: 2개

#### 📄 클래스: `Party` (class)
- **경로**: `Script/Unit/Party/Party.cs`
- **클래스 목적 및 의도**: `Party`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] Id` (string): Party의 내부 데이터 또는 의존 참조 변수
  - `[public] Name` (string): Party의 내부 데이터 또는 의존 참조 변수
  - `[public] Members` (List<Human>): Party의 내부 데이터 또는 의존 참조 변수
  - `[public] Leader` (Human): Party의 내부 데이터 또는 의존 참조 변수
  - `[public] RallyPoint` (Vector2Int?): Party의 내부 데이터 또는 의존 참조 변수
  - `[public] IsRallyActive` (bool): Party의 내부 데이터 또는 의존 참조 변수
  - `[private] best` (Human): Party의 내부 데이터 또는 의존 참조 변수
  - `[private] bestLeadership` (float): Party의 내부 데이터 또는 의존 참조 변수
  - `[public] WaveMonsters` (List<Monster>): Party의 내부 데이터 또는 의존 참조 변수
  - `[public] WaveEnded` (bool): Party의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] AssignLeaderIfNeeded()` ➔ `void`: AssignLeaderIfNeeded 관련 로직 수행 함수
  - `[private] Party(string id, string name)` ➔ `public`: Party 관련 로직 수행 함수
  - `[public] GetSurvivors()` ➔ `List<Unit>`: GetSurvivors 관련 로직 수행 함수

#### 📄 클래스: `PartyService` (class)
- **경로**: `Script/Unit/Party/PartyService.cs`
- **클래스 목적 및 의도**: `PartyService`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] _objectSpawner` (ObjectSpawner): PartyService의 내부 데이터 또는 의존 참조 변수
  - `[private] party` (var): PartyService의 내부 데이터 또는 의존 참조 변수
  - `[private] party` (return): PartyService의 내부 데이터 또는 의존 참조 변수
  - `[private] knowledge` (var): PartyService의 내부 데이터 또는 의존 참조 변수
  - `[private] party` (var): PartyService의 내부 데이터 또는 의존 참조 변수
  - `[private] causer` (Unit): PartyService의 내부 데이터 또는 의존 참조 변수
  - `[private] causerStage` (DangerStage): PartyService의 내부 데이터 또는 의존 참조 변수
  - `[private] traceId` (string): PartyService의 내부 데이터 또는 의존 참조 변수
  - `[private] objId` (string): PartyService의 내부 데이터 또는 의존 참조 변수
  - `[private] gridPos` (Vector3Int): PartyService의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Construct(ObjectSpawner objectSpawner)` ➔ `void`: Construct 관련 로직 수행 함수
  - `[public] CreateParty(string name, List<Human> members)` ➔ `Party`: CreateParty 관련 로직 수행 함수
  - `[public] CheckPartyWaveState(Unit deadUnit)` ➔ `void`: CheckPartyWaveState 관련 로직 수행 함수

---

### 📦 Package: `Unit/Session`

**모듈 내 포함 스크립트 수**: 9개

#### 📄 클래스: `CombatEventService` (class)
- **경로**: `Script/Unit/Session/CombatEventService.cs`
- **클래스 목적 및 의도**: `CombatEventService`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] attacker` (Unit): CombatEventService의 내부 데이터 또는 의존 참조 변수
  - `[private] knowledge` (var): CombatEventService의 내부 데이터 또는 의존 참조 변수
  - `[private] victimIsHuman` (bool): CombatEventService의 내부 데이터 또는 의존 참조 변수
  - `[private] attackerIsHuman` (bool): CombatEventService의 내부 데이터 또는 의존 참조 변수
  - `[private] incidentId` (string): CombatEventService의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] RecordKillWeightEvent(Unit victim, List<Unit> units)` ➔ `void`: RecordKillWeightEvent 관련 로직 수행 함수

#### 📄 클래스: `DebugInputHandler` (class)
- **경로**: `Script/Unit/Session/DebugInputHandler.cs`
- **클래스 목적 및 의도**: `DebugInputHandler`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] _resolver` (IObjectResolver): DebugInputHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] _gameSession` (GameSession): DebugInputHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] _cachedGameSession` (GameSession): DebugInputHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] _unitGenerate` (UnitGenerate): DebugInputHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] types` (UnitType[]): DebugInputHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] offsets` (Vector2Int[]): DebugInputHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] floorIdx` (int): DebugInputHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] type` (UnitType): DebugInputHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] spawnPos` (Vector2Int): DebugInputHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] pos` (Vector2Int): DebugInputHandler의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Construct(IObjectResolver resolver, UnitGenerate unitGenerate)` ➔ `void`: Construct 관련 로직 수행 함수
  - `[public] HandleDebugInput()` ➔ `void`: HandleDebugInput 관련 로직 수행 함수
  - `[public] OnKeyDown_H()` ➔ `void`: OnKeyDown_H 관련 로직 수행 함수
  - `[private] GetRandomStartRoomPos(Vector2 footprint, int floorIdx)` ➔ `Vector2Int`: GetRandomStartRoomPos 관련 로직 수행 함수
  - `[public] OnKeyDown_M()` ➔ `void`: OnKeyDown_M 관련 로직 수행 함수
  - `[public] OnKeyDown_K()` ➔ `void`: OnKeyDown_K 관련 로직 수행 함수
  - `[public] OnKeyDown_O()` ➔ `void`: OnKeyDown_O 관련 로직 수행 함수

#### 📄 클래스: `GameBootstrapper` (class)
- **경로**: `Script/Unit/Session/GameBootstrapper.cs`
- **클래스 목적 및 의도**: `GameBootstrapper`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IAsyncStartable` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _resolver` (IObjectResolver): GameBootstrapper의 내부 데이터 또는 의존 참조 변수
  - `[private] mapRandering` (var): GameBootstrapper의 내부 데이터 또는 의존 참조 변수
  - `[private] waveSpawner` (var): GameBootstrapper의 내부 데이터 또는 의존 참조 변수
  - `[private] humanWaveMgr` (var): GameBootstrapper의 내부 데이터 또는 의존 참조 변수
  - `[private] mapManager` (var): GameBootstrapper의 내부 데이터 또는 의존 참조 변수
  - `[private] inputManager` (var): GameBootstrapper의 내부 데이터 또는 의존 참조 변수
  - `[private] gameSession` (var): GameBootstrapper의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] GameBootstrapper(IObjectResolver resolver)` ➔ `public`: GameBootstrapper 관련 로직 수행 함수
  - `[public] StartAsync(CancellationToken cancellation)` ➔ `UniTask`: StartAsync 관련 로직 수행 함수

#### 📄 클래스: `GameCompositionRoot` (class)
- **경로**: `Script/Unit/Session/GameCompositionRoot.cs`
- **클래스 목적 및 의도**: `GameCompositionRoot`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `CoreLifetimeScope` 상속/구현
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[protected] Awake()` ➔ `void`: Awake 관련 로직 수행 함수
  - `[protected] Configure(IContainerBuilder builder)` ➔ `void`: Configure 관련 로직 수행 함수

#### 📄 클래스: `GameSession` (class)
- **경로**: `Script/Unit/Session/GameSession.cs`
- **클래스 목적 및 의도**: `GameSession`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `NativeRoutine, IOffenseQuery` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] unitGenerate` (UnitGenerate): GameSession의 내부 데이터 또는 의존 참조 변수
  - `[public] mapManager` (MapManager): GameSession의 내부 데이터 또는 의존 참조 변수
  - `[public] mapRandering` (MapRandering): GameSession의 내부 데이터 또는 의존 참조 변수
  - `[public] humanWaveManager` (HumanWaveManager): GameSession의 내부 데이터 또는 의존 참조 변수
  - `[private] _unitGenerate` (UnitGenerate): GameSession의 내부 데이터 또는 의존 참조 변수
  - `[private] _threatTileRenderer` (ThreatTileRenderer): GameSession의 내부 데이터 또는 의존 참조 변수
  - `[private] _resolver` (IObjectResolver): GameSession의 내부 데이터 또는 의존 참조 변수
  - `[private] _dataManager` (DataManager): GameSession의 내부 데이터 또는 의존 참조 변수
  - `[public] OffenseProcessor` (OffenseProcessor): GameSession의 내부 데이터 또는 의존 참조 변수
  - `[private] _unitRegistry` (UnitRegistry): GameSession의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Construct(UnitGenerate unitGenerate, ThreatTileRenderer threatTileRenderer, IObjectResolver resolver, CreateMap injectedMap, DataManager dataManager, UnitRegistry unitRegistry, ObjectSpawner objectSpawner, PartyService partyService, CombatEventService combatEventService, DebugInputHandler debugInputHandler)` ➔ `void`: Construct 관련 로직 수행 함수
  - `[public] GetUnitsInRoom(RectInt bounds)` ➔ `IReadOnlyList<Unit>`: GetUnitsInRoom 관련 로직 수행 함수
  - `[public] RegisterUnitPos(Unit u, Vector2Int pos)` ➔ `void`: RegisterUnitPos 관련 로직 수행 함수
  - `[public] UnregisterUnitPos(Unit u, Vector2Int pos)` ➔ `void`: UnregisterUnitPos 관련 로직 수행 함수
  - `[private] GameSession()` ➔ `public`: GameSession 관련 로직 수행 함수
  - `[public] Initialize(CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[public] BuildRoomGrid()` ➔ `void`: BuildRoomGrid 관련 로직 수행 함수

#### 📄 클래스: `InputManager` (class)
- **경로**: `Script/Unit/Session/InputManager.cs`
- **클래스 목적 및 의도**: 게임 내 `Input` 관련 전역 상태 및 루프 시스템을 총괄 관리하는 매니저 클래스입니다.
- **상속 및 구현 관계**: `MonoBehaviour` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] selectedUnits` (List<Unit>): InputManager의 내부 데이터 또는 의존 참조 변수
  - `[private] DragThresholdPixels` (float): InputManager의 내부 데이터 또는 의존 참조 변수
  - `[private] _isMouseDown` (bool): InputManager의 내부 데이터 또는 의존 참조 변수
  - `[private] _dragBoxActive` (bool): InputManager의 내부 데이터 또는 의존 참조 변수
  - `[private] _dragStartScreenPos` (Vector2): InputManager의 내부 데이터 또는 의존 참조 변수
  - `[private] _dragCurrentScreenPos` (Vector2): InputManager의 내부 데이터 또는 의존 참조 변수
  - `[private] DoubleClickTimeThreshold` (float): InputManager의 내부 데이터 또는 의존 참조 변수
  - `[private] SameTypeNearbyRadius` (float): InputManager의 내부 데이터 또는 의존 참조 변수
  - `[private] _lastClickedUnit` (Unit): InputManager의 내부 데이터 또는 의존 참조 변수
  - `[private] _lastClickTime` (float): InputManager의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Construct(UnitGenerate unitGenerate, GameSession gameSession, BuildingManager buildingManager, ResourceManager resourceManager)` ➔ `void`: Construct 관련 로직 수행 함수
  - `[private] IsPointInFootprint(Vector3Int pos, Unit u)` ➔ `bool`: IsPointInFootprint 관련 로직 수행 함수
  - `[private] FindUnitAtGridPos(Vector3Int gridPos, int currentFloor)` ➔ `Unit`: FindUnitAtGridPos 관련 로직 수행 함수
  - `[private] ScreenToWorldPoint(Vector2 screenPos)` ➔ `Vector3`: ScreenToWorldPoint 관련 로직 수행 함수
  - `[private] ScreenToGridPos(Vector2 screenPos, Vector3 floorOffset, int currentFloor)` ➔ `Vector3Int`: ScreenToGridPos 관련 로직 수행 함수
  - `[private] Update()` ➔ `void`: Update 관련 로직 수행 함수

#### 📄 클래스: `ObjectSpawner` (class)
- **경로**: `Script/Unit/Session/ObjectSpawner.cs`
- **클래스 목적 및 의도**: `ObjectSpawner`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] _mapRandering` (MapRandering): ObjectSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] visual` (GameObject): ObjectSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] sr` (SpriteRenderer): ObjectSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] tex` (Texture2D): ObjectSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] pixels` (Color[]): ObjectSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] sprite` (Sprite): ObjectSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] offset` (Vector3): ObjectSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] childTilemap` (GameObject): ObjectSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] obj` (var): ObjectSpawner의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Construct(MapRandering mapRandering)` ➔ `void`: Construct 관련 로직 수행 함수
  - `[public] SpawnObject(InteractableObject obj, Color color)` ➔ `void`: SpawnObject 관련 로직 수행 함수
  - `[public] CollectObject(Vector3Int pos)` ➔ `void`: CollectObject 관련 로직 수행 함수

#### 📄 클래스: `UIManager` (class)
- **경로**: `Script/Unit/Session/UIManager.cs`
- **클래스 목적 및 의도**: 게임 내 `UI` 관련 전역 상태 및 루프 시스템을 총괄 관리하는 매니저 클래스입니다.
- **상속 및 구현 관계**: `MonoBehaviour` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _gameSession` (GameSession): UIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] WaveStartWarningSeconds` (float): UIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] wm` (HumanWaveManager): UIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] style` (GUIStyle): UIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] w` (float): UIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] rect` (Rect): UIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] prevColor` (Color): UIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] y` (int): UIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] go` (GameObject): UIManager의 내부 데이터 또는 의존 참조 변수
  - `[private] pos` (Vector3): UIManager의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Construct(GameSession gameSession)` ➔ `void`: Construct 관련 로직 수행 함수
  - `[private] OnGUI()` ➔ `void`: OnGUI 관련 로직 수행 함수
  - `[private] DrawWaveStartBanner()` ➔ `void`: DrawWaveStartBanner 관련 로직 수행 함수
  - `[private] DrawTopLeftUI()` ➔ `void`: DrawTopLeftUI 관련 로직 수행 함수
  - `[private] DrawUnitLabels()` ➔ `void`: DrawUnitLabels 관련 로직 수행 함수
  - `[public] ShowFloatingText(Unit unit, string message)` ➔ `void`: ShowFloatingText 관련 로직 수행 함수
  - `[private] GetWorldTextPosition(Unit unit)` ➔ `Vector3`: GetWorldTextPosition 관련 로직 수행 함수

#### 📄 클래스: `UnitRegistry` (class)
- **경로**: `Script/Unit/Session/UnitRegistry.cs`
- **클래스 목적 및 의도**: `UnitRegistry`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] _offenseProcessor` (OffenseProcessor): UnitRegistry의 내부 데이터 또는 의존 참조 변수
  - `[private] _resolver` (IObjectResolver): UnitRegistry의 내부 데이터 또는 의존 참조 변수
  - `[private] _gameSession` (GameSession): UnitRegistry의 내부 데이터 또는 의존 참조 변수
  - `[private] _cachedGameSession` (GameSession): UnitRegistry의 내부 데이터 또는 의존 참조 변수
  - `[private] w` (int): UnitRegistry의 내부 데이터 또는 의존 참조 변수
  - `[private] h` (int): UnitRegistry의 내부 데이터 또는 의존 참조 변수
  - `[private] w` (int): UnitRegistry의 내부 데이터 또는 의존 참조 변수
  - `[private] h` (int): UnitRegistry의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Construct(OffenseProcessor offenseProcessor, IObjectResolver resolver)` ➔ `void`: Construct 관련 로직 수행 함수
  - `[public] RegisterUnitPos(Unit u, Vector2Int pos)` ➔ `void`: RegisterUnitPos 관련 로직 수행 함수
  - `[public] UnregisterUnitPos(Unit u, Vector2Int pos)` ➔ `void`: UnregisterUnitPos 관련 로직 수행 함수

---

### 📦 Package: `Unit/Skills`

**모듈 내 포함 스크립트 수**: 3개

#### 📄 클래스: `SkillAction` (class)
- **경로**: `Script/Unit/Skills/SkillAction.cs`
- **클래스 목적 및 의도**: `SkillAction`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] result` (List<Unit>): SkillAction의 내부 데이터 또는 의존 참조 변수
  - `[private] isEnemy` (bool): SkillAction의 내부 데이터 또는 의존 참조 변수
  - `[private] result` (return): SkillAction의 내부 데이터 또는 의존 참조 변수
  - `[private] size` (Vector2): SkillAction의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] BuildSkillHitbox(Unit unit)` ➔ `Hitbox`: BuildSkillHitbox 관련 로직 수행 함수
  - `[private] BuildRectHitboxWithAngle(unit, HitWidth, HitDepth, unit.CombatState.State.currentAttackAngle)` ➔ `return`: BuildRectHitboxWithAngle 관련 로직 수행 함수
  - `[private] BuildLineHitboxWithAngle(unit, HitRange, unit.CombatState.State.currentAttackAngle)` ➔ `return`: BuildLineHitboxWithAngle 관련 로직 수행 함수
  - `[public] IsAvailable(Unit unit)` ➔ `bool`: IsAvailable 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit, Unit target, float minDist)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[public] Execute(Unit unit, Unit target, float minDist)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[public] BeginAttackCast(
		Unit         unit,
		float        castMs,
		ThreatTileData threat,
		System.Action attackAction,
		System.Action cooldownAction = null,
		System.Action effectAction   = null,
		System.Action castUpdateAction = null)` ➔ `void`: BeginAttackCast 관련 로직 수행 함수
  - `[public] GetEnemiesInHitbox(Unit attacker, Hitbox box)` ➔ `List<Unit>`: GetEnemiesInHitbox 관련 로직 수행 함수
  - `[public] GetUnitHitbox(Unit u)` ➔ `Hitbox`: GetUnitHitbox 관련 로직 수행 함수
  - `[public] DamageEnemiesInHitbox(Unit attacker, Hitbox box, float multiplier, bool stun = false, float stunDuration = 0f)` ➔ `void`: DamageEnemiesInHitbox 관련 로직 수행 함수

#### 📄 클래스: `SkillAction_Generic` (class)
- **경로**: `Script/Unit/Skills/SkillAction_Generic.cs`
- **클래스 목적 및 의도**: `SkillAction_Generic`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `SkillAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _d` (SkillData): SkillAction_Generic의 내부 데이터 또는 의존 참조 변수
  - `[private] p` (float): SkillAction_Generic의 내부 데이터 또는 의존 참조 변수
  - `[private] p` (return): SkillAction_Generic의 내부 데이터 또는 의존 참조 변수
  - `[private] finalDelayMs` (float): SkillAction_Generic의 내부 데이터 또는 의존 참조 변수
  - `[private] threat` (var): SkillAction_Generic의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] SkillAction_Generic(SkillData data)` ➔ `public`: SkillAction_Generic 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit, Unit target, float minDist)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[public] Execute(Unit unit, Unit target, float minDist)` ➔ `void`: Execute 관련 로직 수행 함수

#### 📄 클래스: `SkillAction_Projectile` (class)
- **경로**: `Script/Unit/Skills/SkillAction_Projectile.cs`
- **클래스 목적 및 의도**: `SkillAction_Projectile`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `SkillAction` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] _d` (SkillData): SkillAction_Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] _projectilePrefab` (GameObject): SkillAction_Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] p` (float): SkillAction_Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] p` (return): SkillAction_Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] finalDelayMs` (float): SkillAction_Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] threat` (var): SkillAction_Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] finalWidth` (float): SkillAction_Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] sr` (var): SkillAction_Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] maxRange` (int): SkillAction_Projectile의 내부 데이터 또는 의존 참조 변수
  - `[private] maxHitbox` (Hitbox): SkillAction_Projectile의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] SkillAction_Projectile(SkillData data, GameObject projectilePrefab = null)` ➔ `public`: SkillAction_Projectile 관련 로직 수행 함수
  - `[public] GetPriority(Unit unit, Unit target, float minDist)` ➔ `float`: GetPriority 관련 로직 수행 함수
  - `[public] Execute(Unit unit, Unit target, float minDist)` ➔ `void`: Execute 관련 로직 수행 함수
  - `[private] FireProjectile(Unit attacker, int maxDistance)` ➔ `void`: FireProjectile 관련 로직 수행 함수

---

### 📦 Package: `Unit/Vision`

**모듈 내 포함 스크립트 수**: 14개

#### 📄 클래스: `IVisionContext` (interface)
- **경로**: `Script/Unit/Vision/IVisionContext.cs`
- **클래스 목적 및 의도**: `IVisionContext`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] ResolveReachedTarget(object key, float targetVisibility, Vector3Int tile, float currentDist, out bool firstTouch)` ➔ `PerceptionOutcome`: ResolveReachedTarget 관련 로직 수행 함수
  - `[private] HasReachedPerceptionThisPass(object key)` ➔ `bool`: HasReachedPerceptionThisPass 관련 로직 수행 함수
  - `[private] HasVisionOnlyNonEmptyTile(Vector3Int tile)` ➔ `bool`: HasVisionOnlyNonEmptyTile 관련 로직 수행 함수
  - `[private] AddVisionOnlyNonEmptyTile(Vector3Int tile)` ➔ `void`: AddVisionOnlyNonEmptyTile 관련 로직 수행 함수
  - `[private] AddPersonalSpottedEnemy(Unit unit)` ➔ `void`: AddPersonalSpottedEnemy 관련 로직 수행 함수

#### 📄 클래스: `IVisionTileHandler` (interface)
- **경로**: `Script/Unit/Vision/IVisionTileHandler.cs`
- **클래스 목적 및 의도**: `IVisionTileHandler`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Handle(Unit observer, IVisionContext context, Vector3Int tile, bool inPerceptionRange, float dist, Chunks chunk, Tile tileData)` ➔ `void`: Handle 관련 로직 수행 함수

#### 📄 클래스: `ObjectPerceptionHandler` (class)
- **경로**: `Script/Unit/Vision/ObjectPerceptionHandler.cs`
- **클래스 목적 및 의도**: `ObjectPerceptionHandler`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IVisionTileHandler` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] objVisibility` (float): ObjectPerceptionHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] outcome` (PerceptionOutcome): ObjectPerceptionHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] isBossRoom` (bool): ObjectPerceptionHandler의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Handle(Unit observer, IVisionContext context, Vector3Int tile, bool inPerceptionRange, float dist, Chunks chunk, Tile tileData)` ➔ `void`: Handle 관련 로직 수행 함수

#### 📄 클래스: `PerceptionMath` (class)
- **경로**: `Script/Unit/Vision/PerceptionMath.cs`
- **클래스 목적 및 의도**: `PerceptionMath`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] AlertDetectionBonus` (float): PerceptionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalTierBoundary1` (float): PerceptionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalTierBoundary2` (float): PerceptionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalTierBoundary3` (float): PerceptionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalTierBoundary4` (float): PerceptionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalTierBoundary5` (float): PerceptionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalCorrectionExcited` (float): PerceptionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalCorrectionCalm` (float): PerceptionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalCorrectionStable` (float): PerceptionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalCorrectionTension` (float): PerceptionMath의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] GetMentalTier(float mentalRatioPercent)` ➔ `MentalTier`: GetMentalTier 관련 로직 수행 함수
  - `[public] MentalCorrectionForHuman(float mental, float maxMental)` ➔ `float`: MentalCorrectionForHuman 관련 로직 수행 함수
  - `[public] RollOutcome(float totalVisibility, float roll01)` ➔ `PerceptionOutcome`: RollOutcome 관련 로직 수행 함수

#### 📄 클래스: `PerceptionTargetKind` (enum)
- **경로**: `Script/Unit/Vision/PerceptionMath.cs`
- **클래스 목적 및 의도**: `PerceptionTargetKind`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] AlertDetectionBonus` (float): PerceptionTargetKind의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalTierBoundary1` (float): PerceptionTargetKind의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalTierBoundary2` (float): PerceptionTargetKind의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalTierBoundary3` (float): PerceptionTargetKind의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalTierBoundary4` (float): PerceptionTargetKind의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalTierBoundary5` (float): PerceptionTargetKind의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalCorrectionExcited` (float): PerceptionTargetKind의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalCorrectionCalm` (float): PerceptionTargetKind의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalCorrectionStable` (float): PerceptionTargetKind의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalCorrectionTension` (float): PerceptionTargetKind의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] GetMentalTier(float mentalRatioPercent)` ➔ `MentalTier`: GetMentalTier 관련 로직 수행 함수
  - `[public] MentalCorrectionForHuman(float mental, float maxMental)` ➔ `float`: MentalCorrectionForHuman 관련 로직 수행 함수
  - `[public] RollOutcome(float totalVisibility, float roll01)` ➔ `PerceptionOutcome`: RollOutcome 관련 로직 수행 함수

#### 📄 클래스: `PerceptionRecord` (class)
- **경로**: `Script/Unit/Vision/PerceptionRecord.cs`
- **클래스 목적 및 의도**: `PerceptionRecord`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] Outcome` (PerceptionOutcome): PerceptionRecord의 내부 데이터 또는 의존 참조 변수
  - `[public] WasInRange` (bool): PerceptionRecord의 내부 데이터 또는 의존 참조 변수
  - `[public] PendingSuspiciousInvestigation` (bool): PerceptionRecord의 내부 데이터 또는 의존 참조 변수
  - `[public] LastKnownTile` (Vector3Int): PerceptionRecord의 내부 데이터 또는 의존 참조 변수
  - `[public] IsSuspicious` (bool): PerceptionRecord의 내부 데이터 또는 의존 참조 변수
  - `[public] TempDanger` (float): PerceptionRecord의 내부 데이터 또는 의존 참조 변수
  - `[public] TempInterest` (float): PerceptionRecord의 내부 데이터 또는 의존 참조 변수
  - `[public] TargetKind` (PerceptionTargetKind): PerceptionRecord의 내부 데이터 또는 의존 참조 변수
  - `[public] ReactionCandidates` (PerceptionReactionCandidate[]): PerceptionRecord의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `TerrainRevealHandler` (class)
- **경로**: `Script/Unit/Vision/TerrainRevealHandler.cs`
- **클래스 목적 및 의도**: `TerrainRevealHandler`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IVisionTileHandler` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] tileIsWall` (bool): TerrainRevealHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] isFirstReveal` (bool): TerrainRevealHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] isBossRoom` (bool): TerrainRevealHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] totalFloorTiles` (int): TerrainRevealHandler의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Handle(Unit observer, IVisionContext context, Vector3Int tile, bool inPerceptionRange, float dist, Chunks chunk, Tile tileData)` ➔ `void`: Handle 관련 로직 수행 함수

#### 📄 클래스: `UnitPerceptionHandler` (class)
- **경로**: `Script/Unit/Vision/UnitPerceptionHandler.cs`
- **클래스 목적 및 의도**: `UnitPerceptionHandler`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `IVisionTileHandler` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] isEnemy` (bool): UnitPerceptionHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] outcome` (PerceptionOutcome): UnitPerceptionHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] danger` (float): UnitPerceptionHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] interest` (float): UnitPerceptionHandler의 내부 데이터 또는 의존 참조 변수
  - `[private] isBossRoom` (bool): UnitPerceptionHandler의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Handle(Unit observer, IVisionContext context, Vector3Int tile, bool inPerceptionRange, float dist, Chunks chunk, Tile tileData)` ➔ `void`: Handle 관련 로직 수행 함수

#### 📄 클래스: `PerceptionOutcome` (enum)
- **경로**: `Script/Unit/Vision/VisionEnums.cs`
- **클래스 목적 및 의도**: `PerceptionOutcome`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `MentalTier` (enum)
- **경로**: `Script/Unit/Vision/VisionEnums.cs`
- **클래스 목적 및 의도**: `MentalTier`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `PerceptionReactionCandidate` (enum)
- **경로**: `Script/Unit/Vision/VisionEnums.cs`
- **클래스 목적 및 의도**: `PerceptionReactionCandidate`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `VisionDirectionReason` (enum)
- **경로**: `Script/Unit/Vision/VisionEnums.cs`
- **클래스 목적 및 의도**: `VisionDirectionReason`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `VisionMath` (class)
- **경로**: `Script/Unit/Vision/VisionMath.cs`
- **클래스 목적 및 의도**: `VisionMath`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] DetectionStatMax` (float): VisionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] BaseViewAngleDeg` (float): VisionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] BaseViewDistanceTiles` (int): VisionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] ViewDistancePerSpottingStep` (int): VisionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] BaseAwarenessDistanceTiles` (int): VisionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] AwarenessDistancePerSpottingStep` (int): VisionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] BaseAwarenessAngleDeg` (float): VisionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] MaxAwarenessAngleDeg` (float): VisionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] AwarenessAnglePerSpottingStep` (int): VisionMath의 내부 데이터 또는 의존 참조 변수
  - `[public] AwarenessAngleStepDeg` (float): VisionMath의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ViewDistance(float spotting)` ➔ `int`: ViewDistance 관련 로직 수행 함수
  - `[public] AwarenessDistance(float spotting)` ➔ `int`: AwarenessDistance 관련 로직 수행 함수
  - `[public] AwarenessAngle(float spotting)` ➔ `float`: AwarenessAngle 관련 로직 수행 함수
  - `[public] ResolveBaseVisibility(bool isWall, float? occupantBaseVisibility)` ➔ `float`: ResolveBaseVisibility 관련 로직 수행 함수
  - `[public] FinalVisibility(float baseVisibility, float stealth, bool attackBoosted)` ➔ `float`: FinalVisibility 관련 로직 수행 함수
  - `[public] ResolveObjectVisibility(float baseVisibility, List<string> tags)` ➔ `float`: ResolveObjectVisibility 관련 로직 수행 함수
  - `[public] CircularPerceptionRadius(float spotting)` ➔ `int`: CircularPerceptionRadius 관련 로직 수행 함수
  - `[public] DirStepDistance(Dir a, Dir b)` ➔ `int`: DirStepDistance 관련 로직 수행 함수
  - `[private] VisionDirectionCandidate(VisionDirectionReason reason, Dir direction)` ➔ `public`: VisionDirectionCandidate 관련 로직 수행 함수
  - `[public] ResolveVisionDirection(IReadOnlyList<VisionDirectionCandidate> candidates, Dir currentDir)` ➔ `Dir`: ResolveVisionDirection 관련 로직 수행 함수

#### 📄 클래스: `VisionDirectionCandidate` (struct)
- **경로**: `Script/Unit/Vision/VisionMath.cs`
- **클래스 목적 및 의도**: `VisionDirectionCandidate`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] DetectionStatMax` (float): VisionDirectionCandidate의 내부 데이터 또는 의존 참조 변수
  - `[public] BaseViewAngleDeg` (float): VisionDirectionCandidate의 내부 데이터 또는 의존 참조 변수
  - `[public] BaseViewDistanceTiles` (int): VisionDirectionCandidate의 내부 데이터 또는 의존 참조 변수
  - `[public] ViewDistancePerSpottingStep` (int): VisionDirectionCandidate의 내부 데이터 또는 의존 참조 변수
  - `[public] BaseAwarenessDistanceTiles` (int): VisionDirectionCandidate의 내부 데이터 또는 의존 참조 변수
  - `[public] AwarenessDistancePerSpottingStep` (int): VisionDirectionCandidate의 내부 데이터 또는 의존 참조 변수
  - `[public] BaseAwarenessAngleDeg` (float): VisionDirectionCandidate의 내부 데이터 또는 의존 참조 변수
  - `[public] MaxAwarenessAngleDeg` (float): VisionDirectionCandidate의 내부 데이터 또는 의존 참조 변수
  - `[public] AwarenessAnglePerSpottingStep` (int): VisionDirectionCandidate의 내부 데이터 또는 의존 참조 변수
  - `[public] AwarenessAngleStepDeg` (float): VisionDirectionCandidate의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ViewDistance(float spotting)` ➔ `int`: ViewDistance 관련 로직 수행 함수
  - `[public] AwarenessDistance(float spotting)` ➔ `int`: AwarenessDistance 관련 로직 수행 함수
  - `[public] AwarenessAngle(float spotting)` ➔ `float`: AwarenessAngle 관련 로직 수행 함수
  - `[public] ResolveBaseVisibility(bool isWall, float? occupantBaseVisibility)` ➔ `float`: ResolveBaseVisibility 관련 로직 수행 함수
  - `[public] FinalVisibility(float baseVisibility, float stealth, bool attackBoosted)` ➔ `float`: FinalVisibility 관련 로직 수행 함수
  - `[public] ResolveObjectVisibility(float baseVisibility, List<string> tags)` ➔ `float`: ResolveObjectVisibility 관련 로직 수행 함수
  - `[public] CircularPerceptionRadius(float spotting)` ➔ `int`: CircularPerceptionRadius 관련 로직 수행 함수
  - `[public] DirStepDistance(Dir a, Dir b)` ➔ `int`: DirStepDistance 관련 로직 수행 함수
  - `[private] VisionDirectionCandidate(VisionDirectionReason reason, Dir direction)` ➔ `public`: VisionDirectionCandidate 관련 로직 수행 함수
  - `[public] ResolveVisionDirection(IReadOnlyList<VisionDirectionCandidate> candidates, Dir currentDir)` ➔ `Dir`: ResolveVisionDirection 관련 로직 수행 함수

---

### 📦 Package: `Unit/Visual`

**모듈 내 포함 스크립트 수**: 17개

#### 📄 클래스: `AnimationEventVfxSpawner` (class)
- **경로**: `Script/Unit/Visual/AnimationEventVfxSpawner.cs`
- **클래스 목적 및 의도**: `AnimationEventVfxSpawner`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoBehaviour` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] def` (VfxEventDefinition): AnimationEventVfxSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] reference` (Transform): AnimationEventVfxSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] pos` (Vector3): AnimationEventVfxSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] rot` (Quaternion): AnimationEventVfxSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] instance` (GameObject): AnimationEventVfxSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] lifetime` (float): AnimationEventVfxSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] ps` (ParticleSystem): AnimationEventVfxSpawner의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] OnVfxEvent(string eventKey)` ➔ `void`: OnVfxEvent 관련 로직 수행 함수

#### 📄 클래스: `ThreatTileRenderer` (class)
- **경로**: `Script/Unit/Visual/ThreatTileRenderer.cs`
- **클래스 목적 및 의도**: `ThreatTileRenderer`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] AttackZoneLibraryResourcePath` (string): ThreatTileRenderer의 내부 데이터 또는 의존 참조 변수
  - `[private] LabelPatterns` (bool[][]): ThreatTileRenderer의 내부 데이터 또는 의존 참조 변수
  - `[private] _attackZoneLibrary` (SpriteLibraryAsset): ThreatTileRenderer의 내부 데이터 또는 의존 참조 변수
  - `[private] _attackZoneCategory` (string): ThreatTileRenderer의 내부 데이터 또는 의존 참조 변수
  - `[public] root` (Transform): ThreatTileRenderer의 내부 데이터 또는 의존 참조 변수
  - `[public] cellSprites` (List<SpriteRenderer>): ThreatTileRenderer의 내부 데이터 또는 의존 참조 변수
  - `[private] _root` (Transform): ThreatTileRenderer의 내부 데이터 또는 의존 참조 변수
  - `[private] _unitGenerate` (UnitGenerate): ThreatTileRenderer의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (Sprite): ThreatTileRenderer의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (return): ThreatTileRenderer의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Construct(UnitGenerate unitGenerate)` ➔ `void`: Construct 관련 로직 수행 함수
  - `[private] ThreatTileRenderer()` ➔ `public`: ThreatTileRenderer 관련 로직 수행 함수
  - `[private] GetLabelSprite(int label)` ➔ `Sprite`: GetLabelSprite 관련 로직 수행 함수
  - `[private] GetOrCreateCellSprite(ThreatVisual tv, int index)` ➔ `SpriteRenderer`: GetOrCreateCellSprite 관련 로직 수행 함수
  - `[public] Render(List<Unit> units)` ➔ `void`: Render 관련 로직 수행 함수

#### 📄 클래스: `ThreatVisual` (class)
- **경로**: `Script/Unit/Visual/ThreatTileRenderer.cs`
- **클래스 목적 및 의도**: `ThreatVisual`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] AttackZoneLibraryResourcePath` (string): ThreatVisual의 내부 데이터 또는 의존 참조 변수
  - `[private] LabelPatterns` (bool[][]): ThreatVisual의 내부 데이터 또는 의존 참조 변수
  - `[private] _attackZoneLibrary` (SpriteLibraryAsset): ThreatVisual의 내부 데이터 또는 의존 참조 변수
  - `[private] _attackZoneCategory` (string): ThreatVisual의 내부 데이터 또는 의존 참조 변수
  - `[public] root` (Transform): ThreatVisual의 내부 데이터 또는 의존 참조 변수
  - `[public] cellSprites` (List<SpriteRenderer>): ThreatVisual의 내부 데이터 또는 의존 참조 변수
  - `[private] _root` (Transform): ThreatVisual의 내부 데이터 또는 의존 참조 변수
  - `[private] _unitGenerate` (UnitGenerate): ThreatVisual의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (Sprite): ThreatVisual의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (return): ThreatVisual의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Construct(UnitGenerate unitGenerate)` ➔ `void`: Construct 관련 로직 수행 함수
  - `[private] ThreatTileRenderer()` ➔ `public`: ThreatTileRenderer 관련 로직 수행 함수
  - `[private] GetLabelSprite(int label)` ➔ `Sprite`: GetLabelSprite 관련 로직 수행 함수
  - `[private] GetOrCreateCellSprite(ThreatVisual tv, int index)` ➔ `SpriteRenderer`: GetOrCreateCellSprite 관련 로직 수행 함수
  - `[public] Render(List<Unit> units)` ➔ `void`: Render 관련 로직 수행 함수

#### 📄 클래스: `UnitAnimationController` (class)
- **경로**: `Script/Unit/Visual/UnitAnimationController.cs`
- **클래스 목적 및 의도**: `UnitAnimationController`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoBehaviour` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] MOVE_THRESHOLD` (float): UnitAnimationController의 내부 데이터 또는 의존 참조 변수
  - `[private] IDLE_DEBOUNCE` (int): UnitAnimationController의 내부 데이터 또는 의존 참조 변수
  - `[private] animator` (Animator): UnitAnimationController의 내부 데이터 또는 의존 참조 변수
  - `[private] sr` (SpriteRenderer): UnitAnimationController의 내부 데이터 또는 의존 참조 변수
  - `[private] graph` (PlayableGraph): UnitAnimationController의 내부 데이터 또는 의존 참조 변수
  - `[private] mixer` (AnimationMixerPlayable): UnitAnimationController의 내부 데이터 또는 의존 참조 변수
  - `[private] movementRoot` (Transform): UnitAnimationController의 내부 데이터 또는 의존 참조 변수
  - `[private] state` (AnimState): UnitAnimationController의 내부 데이터 또는 의존 참조 변수
  - `[private] lastRootPos` (Vector3): UnitAnimationController의 내부 데이터 또는 의존 참조 변수
  - `[private] stationaryFrames` (int): UnitAnimationController의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Awake()` ➔ `void`: Awake 관련 로직 수행 함수
  - `[private] BuildGraph(AnimationClip idle, AnimationClip walk)` ➔ `void`: BuildGraph 관련 로직 수행 함수
  - `[private] Update()` ➔ `void`: Update 관련 로직 수행 함수
  - `[private] TransitionTo(AnimState next)` ➔ `void`: TransitionTo 관련 로직 수행 함수
  - `[private] SetWeights(AnimState s)` ➔ `void`: SetWeights 관련 로직 수행 함수
  - `[private] OnDestroy()` ➔ `void`: OnDestroy 관련 로직 수행 함수

#### 📄 클래스: `AnimState` (enum)
- **경로**: `Script/Unit/Visual/UnitAnimationController.cs`
- **클래스 목적 및 의도**: `AnimState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] MOVE_THRESHOLD` (float): AnimState의 내부 데이터 또는 의존 참조 변수
  - `[private] IDLE_DEBOUNCE` (int): AnimState의 내부 데이터 또는 의존 참조 변수
  - `[private] animator` (Animator): AnimState의 내부 데이터 또는 의존 참조 변수
  - `[private] sr` (SpriteRenderer): AnimState의 내부 데이터 또는 의존 참조 변수
  - `[private] graph` (PlayableGraph): AnimState의 내부 데이터 또는 의존 참조 변수
  - `[private] mixer` (AnimationMixerPlayable): AnimState의 내부 데이터 또는 의존 참조 변수
  - `[private] movementRoot` (Transform): AnimState의 내부 데이터 또는 의존 참조 변수
  - `[private] state` (AnimState): AnimState의 내부 데이터 또는 의존 참조 변수
  - `[private] lastRootPos` (Vector3): AnimState의 내부 데이터 또는 의존 참조 변수
  - `[private] stationaryFrames` (int): AnimState의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Awake()` ➔ `void`: Awake 관련 로직 수행 함수
  - `[private] BuildGraph(AnimationClip idle, AnimationClip walk)` ➔ `void`: BuildGraph 관련 로직 수행 함수
  - `[private] Update()` ➔ `void`: Update 관련 로직 수행 함수
  - `[private] TransitionTo(AnimState next)` ➔ `void`: TransitionTo 관련 로직 수행 함수
  - `[private] SetWeights(AnimState s)` ➔ `void`: SetWeights 관련 로직 수행 함수
  - `[private] OnDestroy()` ➔ `void`: OnDestroy 관련 로직 수행 함수

#### 📄 클래스: `UnitGenerate` (class)
- **경로**: `Script/Unit/Visual/UnitGenerate.cs`
- **클래스 목적 및 의도**: `UnitGenerate`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] ShowAllVisionRanges` (bool): UnitGenerate의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionRingDiameterRatio` (float): UnitGenerate의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionRingFlatten` (float): UnitGenerate의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionRingFootOffset` (float): UnitGenerate의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionMarkerSortingOrder` (int): UnitGenerate의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionRingTextureSize` (int): UnitGenerate의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionRingInnerRatio` (float): UnitGenerate의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionRingOuterRatio` (float): UnitGenerate의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionRingColorHuman` (Color): UnitGenerate의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionRingColorMonster` (Color): UnitGenerate의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] CreateRingSprite(int size, float innerRatio, float outerRatio)` ➔ `Sprite`: CreateRingSprite 관련 로직 수행 함수
  - `[private] GetCache(GameObject go)` ➔ `VisualCache`: GetCache 관련 로직 수행 함수
  - `[private] GetMapRandering()` ➔ `MapRandering`: GetMapRandering 관련 로직 수행 함수
  - `[public] Construct(UnitSpriteManager unitSpriteManager, IObjectResolver resolver)` ➔ `void`: Construct 관련 로직 수행 함수
  - `[private] SetupUnitVisual(Unit unit, float visualScale)` ➔ `void`: SetupUnitVisual 관련 로직 수행 함수

#### 📄 클래스: `VisualCache` (class)
- **경로**: `Script/Unit/Visual/UnitGenerate.cs`
- **클래스 목적 및 의도**: `VisualCache`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] ShowAllVisionRanges` (bool): VisualCache의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionRingDiameterRatio` (float): VisualCache의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionRingFlatten` (float): VisualCache의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionRingFootOffset` (float): VisualCache의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionMarkerSortingOrder` (int): VisualCache의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionRingTextureSize` (int): VisualCache의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionRingInnerRatio` (float): VisualCache의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionRingOuterRatio` (float): VisualCache의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionRingColorHuman` (Color): VisualCache의 내부 데이터 또는 의존 참조 변수
  - `[private] SelectionRingColorMonster` (Color): VisualCache의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] CreateRingSprite(int size, float innerRatio, float outerRatio)` ➔ `Sprite`: CreateRingSprite 관련 로직 수행 함수
  - `[private] GetCache(GameObject go)` ➔ `VisualCache`: GetCache 관련 로직 수행 함수
  - `[private] GetMapRandering()` ➔ `MapRandering`: GetMapRandering 관련 로직 수행 함수
  - `[public] Construct(UnitSpriteManager unitSpriteManager, IObjectResolver resolver)` ➔ `void`: Construct 관련 로직 수행 함수
  - `[private] SetupUnitVisual(Unit unit, float visualScale)` ➔ `void`: SetupUnitVisual 관련 로직 수행 함수

#### 📄 클래스: `UnitSpriteManager` (class)
- **경로**: `Script/Unit/Visual/UnitSpriteManager.cs`
- **클래스 목적 및 의도**: 게임 내 `UnitSprite` 관련 전역 상태 및 루프 시스템을 총괄 관리하는 매니저 클래스입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] UnitPrefabResourceFolder` (string): UnitSpriteManager의 내부 데이터 또는 의존 참조 변수
  - `[private] prefab` (var): UnitSpriteManager의 내부 데이터 또는 의존 참조 변수
  - `[private] visualDef` (var): UnitSpriteManager의 내부 데이터 또는 의존 참조 변수
  - `[private] list` (var): UnitSpriteManager의 내부 데이터 또는 의존 참조 변수
  - `[private] list` (return): UnitSpriteManager의 내부 데이터 또는 의존 참조 변수
  - `[private] prefab` (var): UnitSpriteManager의 내부 데이터 또는 의존 참조 변수
  - `[private] visualDef` (var): UnitSpriteManager의 내부 데이터 또는 의존 참조 변수
  - `[private] cats` (var): UnitSpriteManager의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] GetPrefab(string unitTypeName)` ➔ `GameObject`: GetPrefab 관련 로직 수행 함수
  - `[public] GetSkills(string unitTypeName)` ➔ `List<SkillAction>`: GetSkills 관련 로직 수행 함수
  - `[public] GetEngageDistance(string unitTypeName, int defaultDist)` ➔ `int`: GetEngageDistance 관련 로직 수행 함수
  - `[public] GetSpriteLabelForDirection(Dir direction, out string label, out bool flipX)` ➔ `void`: GetSpriteLabelForDirection 관련 로직 수행 함수
  - `[public] GetVariationCategories(SpriteLibraryAsset asset)` ➔ `List<string>`: GetVariationCategories 관련 로직 수행 함수
  - `[public] PickRandomVariation(SpriteLibraryAsset asset)` ➔ `string`: PickRandomVariation 관련 로직 수행 함수

#### 📄 클래스: `UnitVisual` (class)
- **경로**: `Script/Unit/Visual/UnitVisual.cs`
- **클래스 목적 및 의도**: `UnitVisual`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoBehaviour` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] boundUnit` (Unit): UnitVisual의 내부 데이터 또는 의존 참조 변수
  - `[private] _visionRangeLine` (LineRenderer): UnitVisual의 내부 데이터 또는 의존 참조 변수
  - `[private] _perceptionRangeLine` (LineRenderer): UnitVisual의 내부 데이터 또는 의존 참조 변수
  - `[private] _circularPerceptionLine` (LineRenderer): UnitVisual의 내부 데이터 또는 의존 참조 변수
  - `[private] HumanVisionColor` (Color): UnitVisual의 내부 데이터 또는 의존 참조 변수
  - `[private] HumanPerceptionColor` (Color): UnitVisual의 내부 데이터 또는 의존 참조 변수
  - `[private] HumanCircularColor` (Color): UnitVisual의 내부 데이터 또는 의존 참조 변수
  - `[private] MonsterVisionColor` (Color): UnitVisual의 내부 데이터 또는 의존 참조 변수
  - `[private] MonsterPerceptionColor` (Color): UnitVisual의 내부 데이터 또는 의존 참조 변수
  - `[private] MonsterCircularColor` (Color): UnitVisual의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Setup(bool isHuman)` ➔ `void`: Setup 관련 로직 수행 함수
  - `[public] UpdateStatusLabel(string planText, bool isHuman)` ➔ `void`: UpdateStatusLabel 관련 로직 수행 함수
  - `[private] EnsureStatusLabel()` ➔ `void`: EnsureStatusLabel 관련 로직 수행 함수
  - `[public] UpdateBelowLabel(string text)` ➔ `void`: UpdateBelowLabel 관련 로직 수행 함수
  - `[private] EnsureBelowLabel()` ➔ `void`: EnsureBelowLabel 관련 로직 수행 함수
  - `[private] CreateRangeLine(string name, Color color, float width, int sortingOrder)` ➔ `LineRenderer`: CreateRangeLine 관련 로직 수행 함수
  - `[public] SetVisionRangesVisible(bool visible)` ➔ `void`: SetVisionRangesVisible 관련 로직 수행 함수

#### 📄 클래스: `UnitVisualDefinition` (class)
- **경로**: `Script/Unit/Visual/UnitVisualDefinition.cs`
- **클래스 목적 및 의도**: `UnitVisualDefinition`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoBehaviour` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] unitTypeName` (string): UnitVisualDefinition의 내부 데이터 또는 의존 참조 변수
  - `[public] footprint` (Vector2): UnitVisualDefinition의 내부 데이터 또는 의존 참조 변수
  - `[public] engageDistance` (int): UnitVisualDefinition의 내부 데이터 또는 의존 참조 변수
  - `[public] stats` (UnitStatsData): UnitVisualDefinition의 내부 데이터 또는 의존 참조 변수
  - `[public] skills` (List<SkillData>): UnitVisualDefinition의 내부 데이터 또는 의존 참조 변수
  - `[public] hitSparkPrefab` (GameObject): UnitVisualDefinition의 내부 데이터 또는 의존 참조 변수
  - `[public] guardPrefab` (GameObject): UnitVisualDefinition의 내부 데이터 또는 의존 참조 변수
  - `[public] parryPrefab` (GameObject): UnitVisualDefinition의 내부 데이터 또는 의존 참조 변수
  - `[public] attackFailPrefab` (GameObject): UnitVisualDefinition의 내부 데이터 또는 의존 참조 변수
  - `[public] isSpecialUnit` (bool): UnitVisualDefinition의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ApplyStatsTo(Unit unit)` ➔ `void`: ApplyStatsTo 관련 로직 수행 함수
  - `[public] BuildSkillActions()` ➔ `List<SkillAction>`: BuildSkillActions 관련 로직 수행 함수
  - `[public] LoadDataFromJson()` ➔ `void`: LoadDataFromJson 관련 로직 수행 함수

#### 📄 클래스: `UnitsJsonWrapper` (class)
- **경로**: `Script/Unit/Visual/UnitVisualDefinition.cs`
- **클래스 목적 및 의도**: `UnitsJsonWrapper`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] unitTypeName` (string): UnitsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] footprint` (Vector2): UnitsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] engageDistance` (int): UnitsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] stats` (UnitStatsData): UnitsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] skills` (List<SkillData>): UnitsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] hitSparkPrefab` (GameObject): UnitsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] guardPrefab` (GameObject): UnitsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] parryPrefab` (GameObject): UnitsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] attackFailPrefab` (GameObject): UnitsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] isSpecialUnit` (bool): UnitsJsonWrapper의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ApplyStatsTo(Unit unit)` ➔ `void`: ApplyStatsTo 관련 로직 수행 함수
  - `[public] BuildSkillActions()` ➔ `List<SkillAction>`: BuildSkillActions 관련 로직 수행 함수
  - `[public] LoadDataFromJson()` ➔ `void`: LoadDataFromJson 관련 로직 수행 함수

#### 📄 클래스: `UnitJsonNode` (class)
- **경로**: `Script/Unit/Visual/UnitVisualDefinition.cs`
- **클래스 목적 및 의도**: `UnitJsonNode`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] unitTypeName` (string): UnitJsonNode의 내부 데이터 또는 의존 참조 변수
  - `[public] footprint` (Vector2): UnitJsonNode의 내부 데이터 또는 의존 참조 변수
  - `[public] engageDistance` (int): UnitJsonNode의 내부 데이터 또는 의존 참조 변수
  - `[public] stats` (UnitStatsData): UnitJsonNode의 내부 데이터 또는 의존 참조 변수
  - `[public] skills` (List<SkillData>): UnitJsonNode의 내부 데이터 또는 의존 참조 변수
  - `[public] hitSparkPrefab` (GameObject): UnitJsonNode의 내부 데이터 또는 의존 참조 변수
  - `[public] guardPrefab` (GameObject): UnitJsonNode의 내부 데이터 또는 의존 참조 변수
  - `[public] parryPrefab` (GameObject): UnitJsonNode의 내부 데이터 또는 의존 참조 변수
  - `[public] attackFailPrefab` (GameObject): UnitJsonNode의 내부 데이터 또는 의존 참조 변수
  - `[public] isSpecialUnit` (bool): UnitJsonNode의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ApplyStatsTo(Unit unit)` ➔ `void`: ApplyStatsTo 관련 로직 수행 함수
  - `[public] BuildSkillActions()` ➔ `List<SkillAction>`: BuildSkillActions 관련 로직 수행 함수
  - `[public] LoadDataFromJson()` ➔ `void`: LoadDataFromJson 관련 로직 수행 함수

#### 📄 클래스: `SkillsJsonWrapper` (class)
- **경로**: `Script/Unit/Visual/UnitVisualDefinition.cs`
- **클래스 목적 및 의도**: `SkillsJsonWrapper`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] unitTypeName` (string): SkillsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] footprint` (Vector2): SkillsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] engageDistance` (int): SkillsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] stats` (UnitStatsData): SkillsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] skills` (List<SkillData>): SkillsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] hitSparkPrefab` (GameObject): SkillsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] guardPrefab` (GameObject): SkillsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] parryPrefab` (GameObject): SkillsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] attackFailPrefab` (GameObject): SkillsJsonWrapper의 내부 데이터 또는 의존 참조 변수
  - `[public] isSpecialUnit` (bool): SkillsJsonWrapper의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] ApplyStatsTo(Unit unit)` ➔ `void`: ApplyStatsTo 관련 로직 수행 함수
  - `[public] BuildSkillActions()` ➔ `List<SkillAction>`: BuildSkillActions 관련 로직 수행 함수
  - `[public] LoadDataFromJson()` ➔ `void`: LoadDataFromJson 관련 로직 수행 함수

#### 📄 클래스: `VfxEventDefinition` (class)
- **경로**: `Script/Unit/Visual/VfxEventDefinition.cs`
- **클래스 목적 및 의도**: `VfxEventDefinition`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] eventKey` (string): VfxEventDefinition의 내부 데이터 또는 의존 참조 변수
  - `[public] effectPrefab` (GameObject): VfxEventDefinition의 내부 데이터 또는 의존 참조 변수
  - `[public] referenceTransform` (Transform): VfxEventDefinition의 내부 데이터 또는 의존 참조 변수
  - `[public] positionOffset` (Vector3): VfxEventDefinition의 내부 데이터 또는 의존 참조 변수
  - `[public] rotationOffset` (Vector3): VfxEventDefinition의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `VFXManager` (class)
- **경로**: `Script/Unit/Visual/VFXManager.cs`
- **클래스 목적 및 의도**: 게임 내 `VFX` 관련 전역 상태 및 루프 시스템을 총괄 관리하는 매니저 클래스입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] parent` (Transform): VFXManager의 내부 데이터 또는 의존 참조 변수
  - `[private] worldPos` (Vector3): VFXManager의 내부 데이터 또는 의존 참조 변수
  - `[private] go` (var): VFXManager의 내부 데이터 또는 의존 참조 변수
  - `[private] ps` (var): VFXManager의 내부 데이터 또는 의존 참조 변수
  - `[private] lifetime` (float): VFXManager의 내부 데이터 또는 의존 참조 변수
  - `[private] offset` (Vector3): VFXManager의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Spawn(GameObject prefab, Unit unit)` ➔ `void`: Spawn 관련 로직 수행 함수
  - `[private] GetWorldPos(Unit unit)` ➔ `Vector3`: GetWorldPos 관련 로직 수행 함수

#### 📄 클래스: `WeaponAttachment` (class)
- **경로**: `Script/Unit/Visual/WeaponAttachment.cs`
- **클래스 목적 및 의도**: `WeaponAttachment`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `MonoBehaviour` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] direction` (Dir): WeaponAttachment의 내부 데이터 또는 의존 참조 변수
  - `[public] offset` (Vector2): WeaponAttachment의 내부 데이터 또는 의존 참조 변수
  - `[public] sortingOrder` (int): WeaponAttachment의 내부 데이터 또는 의존 참조 변수
  - `[private] poses` (DirectionalPose[]): WeaponAttachment의 내부 데이터 또는 의존 참조 변수
  - `[private] _sr` (SpriteRenderer): WeaponAttachment의 내부 데이터 또는 의존 참조 변수
  - `[private] _lastDir` (Dir): WeaponAttachment의 내부 데이터 또는 의존 참조 변수
  - `[private] mirror` (bool): WeaponAttachment의 내부 데이터 또는 의존 참조 변수
  - `[private] lookup` (Dir): WeaponAttachment의 내부 데이터 또는 의존 참조 변수
  - `[private] pose` (DirectionalPose): WeaponAttachment의 내부 데이터 또는 의존 참조 변수
  - `[private] targetAngle` (float): WeaponAttachment의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Awake()` ➔ `void`: Awake 관련 로직 수행 함수
  - `[public] UpdatePose(Dir direction)` ➔ `void`: UpdatePose 관련 로직 수행 함수

#### 📄 클래스: `DirectionalPose` (class)
- **경로**: `Script/Unit/Visual/WeaponAttachment.cs`
- **클래스 목적 및 의도**: `DirectionalPose`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] direction` (Dir): DirectionalPose의 내부 데이터 또는 의존 참조 변수
  - `[public] offset` (Vector2): DirectionalPose의 내부 데이터 또는 의존 참조 변수
  - `[public] sortingOrder` (int): DirectionalPose의 내부 데이터 또는 의존 참조 변수
  - `[private] poses` (DirectionalPose[]): DirectionalPose의 내부 데이터 또는 의존 참조 변수
  - `[private] _sr` (SpriteRenderer): DirectionalPose의 내부 데이터 또는 의존 참조 변수
  - `[private] _lastDir` (Dir): DirectionalPose의 내부 데이터 또는 의존 참조 변수
  - `[private] mirror` (bool): DirectionalPose의 내부 데이터 또는 의존 참조 변수
  - `[private] lookup` (Dir): DirectionalPose의 내부 데이터 또는 의존 참조 변수
  - `[private] pose` (DirectionalPose): DirectionalPose의 내부 데이터 또는 의존 참조 변수
  - `[private] targetAngle` (float): DirectionalPose의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Awake()` ➔ `void`: Awake 관련 로직 수행 함수
  - `[public] UpdatePose(Dir direction)` ➔ `void`: UpdatePose 관련 로직 수행 함수

---

### 📦 Package: `Unit/Weight`

**모듈 내 포함 스크립트 수**: 22개

#### 📄 클래스: `HumanKnowledgeBase` (class)
- **경로**: `Script/Unit/Weight/HumanKnowledgeBase.cs`
- **클래스 목적 및 의도**: `HumanKnowledgeBase`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] _pendingIncidents` (List<IncidentEntry>): HumanKnowledgeBase의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (return): HumanKnowledgeBase의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (return): HumanKnowledgeBase의 내부 데이터 또는 의존 참조 변수
  - `[private] targetId` (string): HumanKnowledgeBase의 내부 데이터 또는 의존 참조 변수
  - `[private] isIndividualTarget` (bool): HumanKnowledgeBase의 내부 데이터 또는 의존 참조 변수
  - `[private] mentalState` (MentalErrorState): HumanKnowledgeBase의 내부 데이터 또는 의존 참조 변수
  - `[private] key` (string): HumanKnowledgeBase의 내부 데이터 또는 의존 참조 변수
  - `[private] ratio` (float): HumanKnowledgeBase의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] GetOrCreateSpecies(string speciesKey)` ➔ `SpeciesWeightState`: GetOrCreateSpecies 관련 로직 수행 함수
  - `[private] GetOrCreateIndividual(string unitId)` ➔ `IndividualWeightState`: GetOrCreateIndividual 관련 로직 수행 함수
  - `[public] RecordEvent(EventId id, Unit observer, Unit target, InfoType infoType, string incidentId)` ➔ `void`: RecordEvent 관련 로직 수행 함수
  - `[private] RecordEventForWeight(EventId id, Unit observer, string targetId, bool isIndividualTarget, WeightType type,
		float changeValue, InfoType infoType, MentalErrorState mentalState, string incidentId)` ➔ `void`: RecordEventForWeight 관련 로직 수행 함수
  - `[public] GetMentalState(Unit u)` ➔ `MentalErrorState`: GetMentalState 관련 로직 수행 함수
  - `[public] InitializeNewUnitPersonalInfo(Unit unit)` ➔ `void`: InitializeNewUnitPersonalInfo 관련 로직 수행 함수
  - `[private] SetPersonalInitial(Unit unit, string targetId, WeightType type, float value)` ➔ `void`: SetPersonalInitial 관련 로직 수행 함수
  - `[public] OnWaveEnd(List<Unit> survivors)` ➔ `void`: OnWaveEnd 관련 로직 수행 함수
  - `[private] ApplyUnderstandingGlobal(string targetId, bool isIndividual, float delta)` ➔ `void`: ApplyUnderstandingGlobal 관련 로직 수행 함수

#### 📄 클래스: `WipeoutTraceRecord` (class)
- **경로**: `Script/Unit/Weight/HumanKnowledgeBase.cs`
- **클래스 목적 및 의도**: `WipeoutTraceRecord`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] _pendingIncidents` (List<IncidentEntry>): WipeoutTraceRecord의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (return): WipeoutTraceRecord의 내부 데이터 또는 의존 참조 변수
  - `[private] s` (return): WipeoutTraceRecord의 내부 데이터 또는 의존 참조 변수
  - `[private] targetId` (string): WipeoutTraceRecord의 내부 데이터 또는 의존 참조 변수
  - `[private] isIndividualTarget` (bool): WipeoutTraceRecord의 내부 데이터 또는 의존 참조 변수
  - `[private] mentalState` (MentalErrorState): WipeoutTraceRecord의 내부 데이터 또는 의존 참조 변수
  - `[private] key` (string): WipeoutTraceRecord의 내부 데이터 또는 의존 참조 변수
  - `[private] ratio` (float): WipeoutTraceRecord의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] GetOrCreateSpecies(string speciesKey)` ➔ `SpeciesWeightState`: GetOrCreateSpecies 관련 로직 수행 함수
  - `[private] GetOrCreateIndividual(string unitId)` ➔ `IndividualWeightState`: GetOrCreateIndividual 관련 로직 수행 함수
  - `[public] RecordEvent(EventId id, Unit observer, Unit target, InfoType infoType, string incidentId)` ➔ `void`: RecordEvent 관련 로직 수행 함수
  - `[private] RecordEventForWeight(EventId id, Unit observer, string targetId, bool isIndividualTarget, WeightType type,
		float changeValue, InfoType infoType, MentalErrorState mentalState, string incidentId)` ➔ `void`: RecordEventForWeight 관련 로직 수행 함수
  - `[public] GetMentalState(Unit u)` ➔ `MentalErrorState`: GetMentalState 관련 로직 수행 함수
  - `[public] InitializeNewUnitPersonalInfo(Unit unit)` ➔ `void`: InitializeNewUnitPersonalInfo 관련 로직 수행 함수
  - `[private] SetPersonalInitial(Unit unit, string targetId, WeightType type, float value)` ➔ `void`: SetPersonalInitial 관련 로직 수행 함수
  - `[public] OnWaveEnd(List<Unit> survivors)` ➔ `void`: OnWaveEnd 관련 로직 수행 함수
  - `[private] ApplyUnderstandingGlobal(string targetId, bool isIndividual, float delta)` ➔ `void`: ApplyUnderstandingGlobal 관련 로직 수행 함수

#### 📄 클래스: `IncidentEntry` (class)
- **경로**: `Script/Unit/Weight/IncidentLog.cs`
- **클래스 목적 및 의도**: `IncidentEntry`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] IncidentId` (string): IncidentEntry의 내부 데이터 또는 의존 참조 변수
  - `[public] EventId` (EventId): IncidentEntry의 내부 데이터 또는 의존 참조 변수
  - `[public] TargetId` (string): IncidentEntry의 내부 데이터 또는 의존 참조 변수
  - `[public] WeightType` (WeightType): IncidentEntry의 내부 데이터 또는 의존 참조 변수
  - `[public] InfoType` (InfoType): IncidentEntry의 내부 데이터 또는 의존 참조 변수
  - `[public] ChangeValue` (float): IncidentEntry의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalStateAtRecord` (MentalErrorState): IncidentEntry의 내부 데이터 또는 의존 참조 변수
  - `[public] ObserverUnitName` (string): IncidentEntry의 내부 데이터 또는 의존 참조 변수
  - `[public] IsIndividualTarget` (bool): IncidentEntry의 내부 데이터 또는 의존 참조 변수
  - `[public] DedupKey` (string): IncidentEntry의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] IncidentEntry(string incidentId, EventId eventId, string targetId, WeightType weightType,
		InfoType infoType, float changeValue, MentalErrorState mentalState, string observerUnitName, bool isIndividualTarget = false)` ➔ `public`: IncidentEntry 관련 로직 수행 함수

#### 📄 클래스: `PersonalMapKnowledge` (class)
- **경로**: `Script/Unit/Weight/PersonalMapKnowledge.cs`
- **클래스 목적 및 의도**: `PersonalMapKnowledge`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] KnownInterestTiles` (IEnumerable<Vector3Int>): PersonalMapKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[private] objId` (string): PersonalMapKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[private] newInterestPresent` (bool): PersonalMapKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[private] elapsed` (float): PersonalMapKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[private] stage` (var): PersonalMapKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[private] baseTileDanger` (float): PersonalMapKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[private] objId` (string): PersonalMapKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[private] objectDanger` (float): PersonalMapKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[public] KnownDangerTiles` (IEnumerable<Vector3Int>): PersonalMapKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[private] elapsed` (float): PersonalMapKnowledge의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] TickTileInterestConfirm(Vector3Int pos, float deltaTime)` ➔ `void`: TickTileInterestConfirm 관련 로직 수행 함수
  - `[public] GetTileDanger(Vector3Int pos, bool explored)` ➔ `float`: GetTileDanger 관련 로직 수행 함수
  - `[public] SetTileDangerFromUnit(Vector3Int pos, float unitFinalDanger)` ➔ `void`: SetTileDangerFromUnit 관련 로직 수행 함수
  - `[public] TickTileSafety(Vector3Int pos, bool threatPresent, float deltaTime)` ➔ `void`: TickTileSafety 관련 로직 수행 함수
  - `[public] RevealTile(Vector3Int pos, bool isWall)` ➔ `bool`: RevealTile 관련 로직 수행 함수
  - `[public] GetTerrainTexture(int floor)` ➔ `Texture2D`: GetTerrainTexture 관련 로직 수행 함수

#### 📄 클래스: `MonsterSighting` (class)
- **경로**: `Script/Unit/Weight/PersonalMapKnowledge.cs`
- **클래스 목적 및 의도**: `MonsterSighting`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] KnownInterestTiles` (IEnumerable<Vector3Int>): MonsterSighting의 내부 데이터 또는 의존 참조 변수
  - `[private] objId` (string): MonsterSighting의 내부 데이터 또는 의존 참조 변수
  - `[private] newInterestPresent` (bool): MonsterSighting의 내부 데이터 또는 의존 참조 변수
  - `[private] elapsed` (float): MonsterSighting의 내부 데이터 또는 의존 참조 변수
  - `[private] stage` (var): MonsterSighting의 내부 데이터 또는 의존 참조 변수
  - `[private] baseTileDanger` (float): MonsterSighting의 내부 데이터 또는 의존 참조 변수
  - `[private] objId` (string): MonsterSighting의 내부 데이터 또는 의존 참조 변수
  - `[private] objectDanger` (float): MonsterSighting의 내부 데이터 또는 의존 참조 변수
  - `[public] KnownDangerTiles` (IEnumerable<Vector3Int>): MonsterSighting의 내부 데이터 또는 의존 참조 변수
  - `[private] elapsed` (float): MonsterSighting의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] TickTileInterestConfirm(Vector3Int pos, float deltaTime)` ➔ `void`: TickTileInterestConfirm 관련 로직 수행 함수
  - `[public] GetTileDanger(Vector3Int pos, bool explored)` ➔ `float`: GetTileDanger 관련 로직 수행 함수
  - `[public] SetTileDangerFromUnit(Vector3Int pos, float unitFinalDanger)` ➔ `void`: SetTileDangerFromUnit 관련 로직 수행 함수
  - `[public] TickTileSafety(Vector3Int pos, bool threatPresent, float deltaTime)` ➔ `void`: TickTileSafety 관련 로직 수행 함수
  - `[public] RevealTile(Vector3Int pos, bool isWall)` ➔ `bool`: RevealTile 관련 로직 수행 함수
  - `[public] GetTerrainTexture(int floor)` ➔ `Texture2D`: GetTerrainTexture 관련 로직 수행 함수

#### 📄 클래스: `RoomExploreState` (enum)
- **경로**: `Script/Unit/Weight/PersonalMapKnowledge.cs`
- **클래스 목적 및 의도**: `RoomExploreState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] KnownInterestTiles` (IEnumerable<Vector3Int>): RoomExploreState의 내부 데이터 또는 의존 참조 변수
  - `[private] objId` (string): RoomExploreState의 내부 데이터 또는 의존 참조 변수
  - `[private] newInterestPresent` (bool): RoomExploreState의 내부 데이터 또는 의존 참조 변수
  - `[private] elapsed` (float): RoomExploreState의 내부 데이터 또는 의존 참조 변수
  - `[private] stage` (var): RoomExploreState의 내부 데이터 또는 의존 참조 변수
  - `[private] baseTileDanger` (float): RoomExploreState의 내부 데이터 또는 의존 참조 변수
  - `[private] objId` (string): RoomExploreState의 내부 데이터 또는 의존 참조 변수
  - `[private] objectDanger` (float): RoomExploreState의 내부 데이터 또는 의존 참조 변수
  - `[public] KnownDangerTiles` (IEnumerable<Vector3Int>): RoomExploreState의 내부 데이터 또는 의존 참조 변수
  - `[private] elapsed` (float): RoomExploreState의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] TickTileInterestConfirm(Vector3Int pos, float deltaTime)` ➔ `void`: TickTileInterestConfirm 관련 로직 수행 함수
  - `[public] GetTileDanger(Vector3Int pos, bool explored)` ➔ `float`: GetTileDanger 관련 로직 수행 함수
  - `[public] SetTileDangerFromUnit(Vector3Int pos, float unitFinalDanger)` ➔ `void`: SetTileDangerFromUnit 관련 로직 수행 함수
  - `[public] TickTileSafety(Vector3Int pos, bool threatPresent, float deltaTime)` ➔ `void`: TickTileSafety 관련 로직 수행 함수
  - `[public] RevealTile(Vector3Int pos, bool isWall)` ➔ `bool`: RevealTile 관련 로직 수행 함수
  - `[public] GetTerrainTexture(int floor)` ➔ `Texture2D`: GetTerrainTexture 관련 로직 수행 함수

#### 📄 클래스: `RoomKnowledge` (class)
- **경로**: `Script/Unit/Weight/PersonalMapKnowledge.cs`
- **클래스 목적 및 의도**: `RoomKnowledge`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] KnownInterestTiles` (IEnumerable<Vector3Int>): RoomKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[private] objId` (string): RoomKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[private] newInterestPresent` (bool): RoomKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[private] elapsed` (float): RoomKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[private] stage` (var): RoomKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[private] baseTileDanger` (float): RoomKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[private] objId` (string): RoomKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[private] objectDanger` (float): RoomKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[public] KnownDangerTiles` (IEnumerable<Vector3Int>): RoomKnowledge의 내부 데이터 또는 의존 참조 변수
  - `[private] elapsed` (float): RoomKnowledge의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] TickTileInterestConfirm(Vector3Int pos, float deltaTime)` ➔ `void`: TickTileInterestConfirm 관련 로직 수행 함수
  - `[public] GetTileDanger(Vector3Int pos, bool explored)` ➔ `float`: GetTileDanger 관련 로직 수행 함수
  - `[public] SetTileDangerFromUnit(Vector3Int pos, float unitFinalDanger)` ➔ `void`: SetTileDangerFromUnit 관련 로직 수행 함수
  - `[public] TickTileSafety(Vector3Int pos, bool threatPresent, float deltaTime)` ➔ `void`: TickTileSafety 관련 로직 수행 함수
  - `[public] RevealTile(Vector3Int pos, bool isWall)` ➔ `bool`: RevealTile 관련 로직 수행 함수
  - `[public] GetTerrainTexture(int floor)` ➔ `Texture2D`: GetTerrainTexture 관련 로직 수행 함수

#### 📄 클래스: `PersonalWeightRecord` (class)
- **경로**: `Script/Unit/Weight/PersonalWeightRecord.cs`
- **클래스 목적 및 의도**: `PersonalWeightRecord`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] TargetId` (string): PersonalWeightRecord의 내부 데이터 또는 의존 참조 변수
  - `[public] Type` (WeightType): PersonalWeightRecord의 내부 데이터 또는 의존 참조 변수
  - `[public] StoredValue` (float): PersonalWeightRecord의 내부 데이터 또는 의존 참조 변수
  - `[public] AppliedValue` (int): PersonalWeightRecord의 내부 데이터 또는 의존 참조 변수
  - `[public] LastInfoType` (InfoType): PersonalWeightRecord의 내부 데이터 또는 의존 참조 변수
  - `[public] MentalStateAtRecord` (MentalErrorState): PersonalWeightRecord의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] PersonalWeightRecord(string targetId, WeightType type)` ➔ `public`: PersonalWeightRecord 관련 로직 수행 함수

#### 📄 클래스: `SpeciesWeightState` (class)
- **경로**: `Script/Unit/Weight/SpeciesWeightState.cs`
- **클래스 목적 및 의도**: `SpeciesWeightState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] SpeciesKey` (string): SpeciesWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] UnderstandingStored` (float): SpeciesWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] LastUnderstandingUpdateWave` (int): SpeciesWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] DangerAccumulatedStored` (float): SpeciesWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] SpecialActionCap` (float): SpeciesWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] SummonAccum` (float): SpeciesWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] BuffAccum` (float): SpeciesWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] DebuffAccum` (float): SpeciesWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] UnitId` (string): SpeciesWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] UnderstandingStored` (float): SpeciesWeightState의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] SpeciesWeightState(string speciesKey)` ➔ `public`: SpeciesWeightState 관련 로직 수행 함수
  - `[private] IndividualWeightState(string unitId)` ➔ `public`: IndividualWeightState 관련 로직 수행 함수

#### 📄 클래스: `IndividualWeightState` (class)
- **경로**: `Script/Unit/Weight/SpeciesWeightState.cs`
- **클래스 목적 및 의도**: `IndividualWeightState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] SpeciesKey` (string): IndividualWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] UnderstandingStored` (float): IndividualWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] LastUnderstandingUpdateWave` (int): IndividualWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] DangerAccumulatedStored` (float): IndividualWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] SpecialActionCap` (float): IndividualWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] SummonAccum` (float): IndividualWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] BuffAccum` (float): IndividualWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] DebuffAccum` (float): IndividualWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] UnitId` (string): IndividualWeightState의 내부 데이터 또는 의존 참조 변수
  - `[public] UnderstandingStored` (float): IndividualWeightState의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] SpeciesWeightState(string speciesKey)` ➔ `public`: SpeciesWeightState 관련 로직 수행 함수
  - `[private] IndividualWeightState(string unitId)` ➔ `public`: IndividualWeightState 관련 로직 수행 함수

#### 📄 클래스: `WeightType` (enum)
- **경로**: `Script/Unit/Weight/WeightEnums.cs`
- **클래스 목적 및 의도**: `WeightType`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `InfoType` (enum)
- **경로**: `Script/Unit/Weight/WeightEnums.cs`
- **클래스 목적 및 의도**: `InfoType`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `MentalErrorState` (enum)
- **경로**: `Script/Unit/Weight/WeightEnums.cs`
- **클래스 목적 및 의도**: `MentalErrorState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `DangerStage` (enum)
- **경로**: `Script/Unit/Weight/WeightEnums.cs`
- **클래스 목적 및 의도**: `DangerStage`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `InterestStage` (enum)
- **경로**: `Script/Unit/Weight/WeightEnums.cs`
- **클래스 목적 및 의도**: `InterestStage`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `EventId` (enum)
- **경로**: `Script/Unit/Weight/WeightEnums.cs`
- **클래스 목적 및 의도**: `EventId`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수**: 외부 노출 변수 없음
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `WeightEventTable` (class)
- **경로**: `Script/Unit/Weight/WeightEventTable.cs`
- **클래스 목적 및 의도**: `WeightEventTable`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] JsonPath` (string): WeightEventTable의 내부 데이터 또는 의존 참조 변수
  - `[public] Understanding` (float): WeightEventTable의 내부 데이터 또는 의존 참조 변수
  - `[public] Danger` (float): WeightEventTable의 내부 데이터 또는 의존 참조 변수
  - `[public] id` (string): WeightEventTable의 내부 데이터 또는 의존 참조 변수
  - `[public] understanding` (float): WeightEventTable의 내부 데이터 또는 의존 참조 변수
  - `[public] danger` (float): WeightEventTable의 내부 데이터 또는 의존 참조 변수
  - `[private] _table` (return): WeightEventTable의 내부 데이터 또는 의존 참조 변수
  - `[private] db` (var): WeightEventTable의 내부 데이터 또는 의존 참조 변수
  - `[private] default` (return): WeightEventTable의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Delta(float understanding, float danger)` ➔ `public`: Delta 관련 로직 수행 함수
  - `[private] Load()` ➔ `void`: Load 관련 로직 수행 함수
  - `[public] Get(EventId id)` ➔ `Delta`: Get 관련 로직 수행 함수

#### 📄 클래스: `Delta` (struct)
- **경로**: `Script/Unit/Weight/WeightEventTable.cs`
- **클래스 목적 및 의도**: `Delta`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] JsonPath` (string): Delta의 내부 데이터 또는 의존 참조 변수
  - `[public] Understanding` (float): Delta의 내부 데이터 또는 의존 참조 변수
  - `[public] Danger` (float): Delta의 내부 데이터 또는 의존 참조 변수
  - `[public] id` (string): Delta의 내부 데이터 또는 의존 참조 변수
  - `[public] understanding` (float): Delta의 내부 데이터 또는 의존 참조 변수
  - `[public] danger` (float): Delta의 내부 데이터 또는 의존 참조 변수
  - `[private] _table` (return): Delta의 내부 데이터 또는 의존 참조 변수
  - `[private] db` (var): Delta의 내부 데이터 또는 의존 참조 변수
  - `[private] default` (return): Delta의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Delta(float understanding, float danger)` ➔ `public`: Delta 관련 로직 수행 함수
  - `[private] Load()` ➔ `void`: Load 관련 로직 수행 함수
  - `[public] Get(EventId id)` ➔ `Delta`: Get 관련 로직 수행 함수

#### 📄 클래스: `JsonEvent` (class)
- **경로**: `Script/Unit/Weight/WeightEventTable.cs`
- **클래스 목적 및 의도**: `JsonEvent`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] JsonPath` (string): JsonEvent의 내부 데이터 또는 의존 참조 변수
  - `[public] Understanding` (float): JsonEvent의 내부 데이터 또는 의존 참조 변수
  - `[public] Danger` (float): JsonEvent의 내부 데이터 또는 의존 참조 변수
  - `[public] id` (string): JsonEvent의 내부 데이터 또는 의존 참조 변수
  - `[public] understanding` (float): JsonEvent의 내부 데이터 또는 의존 참조 변수
  - `[public] danger` (float): JsonEvent의 내부 데이터 또는 의존 참조 변수
  - `[private] _table` (return): JsonEvent의 내부 데이터 또는 의존 참조 변수
  - `[private] db` (var): JsonEvent의 내부 데이터 또는 의존 참조 변수
  - `[private] default` (return): JsonEvent의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Delta(float understanding, float danger)` ➔ `public`: Delta 관련 로직 수행 함수
  - `[private] Load()` ➔ `void`: Load 관련 로직 수행 함수
  - `[public] Get(EventId id)` ➔ `Delta`: Get 관련 로직 수행 함수

#### 📄 클래스: `JsonEventDatabase` (class)
- **경로**: `Script/Unit/Weight/WeightEventTable.cs`
- **클래스 목적 및 의도**: `JsonEventDatabase` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] JsonPath` (string): JsonEventDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] Understanding` (float): JsonEventDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] Danger` (float): JsonEventDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] id` (string): JsonEventDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] understanding` (float): JsonEventDatabase의 내부 데이터 또는 의존 참조 변수
  - `[public] danger` (float): JsonEventDatabase의 내부 데이터 또는 의존 참조 변수
  - `[private] _table` (return): JsonEventDatabase의 내부 데이터 또는 의존 참조 변수
  - `[private] db` (var): JsonEventDatabase의 내부 데이터 또는 의존 참조 변수
  - `[private] default` (return): JsonEventDatabase의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] Delta(float understanding, float danger)` ➔ `public`: Delta 관련 로직 수행 함수
  - `[private] Load()` ➔ `void`: Load 관련 로직 수행 함수
  - `[public] Get(EventId id)` ➔ `Delta`: Get 관련 로직 수행 함수

#### 📄 클래스: `WeightMath` (class)
- **경로**: `Script/Unit/Weight/WeightMath.cs`
- **클래스 목적 및 의도**: `WeightMath`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] UnderstandingMin` (float): WeightMath의 내부 데이터 또는 의존 참조 변수
  - `[public] DangerMin` (float): WeightMath의 내부 데이터 또는 의존 참조 변수
  - `[public] InterestMin` (float): WeightMath의 내부 데이터 또는 의존 참조 변수
  - `[private] distinct` (var): WeightMath의 내부 데이터 또는 의존 참조 변수
  - `[private] sum` (float): WeightMath의 내부 데이터 또는 의존 참조 변수
  - `[private] result` (var): WeightMath의 내부 데이터 또는 의존 참조 변수
  - `[private] result` (return): WeightMath의 내부 데이터 또는 의존 참조 변수
  - `[private] seen` (var): WeightMath의 내부 데이터 또는 의존 참조 변수
  - `[private] result` (var): WeightMath의 내부 데이터 또는 의존 참조 변수
  - `[private] result` (return): WeightMath의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Clamp(float value, WeightType type)` ➔ `float`: Clamp 관련 로직 수행 함수
  - `[public] DedupExact(IEnumerable<IncidentEntry> entries)` ➔ `List<IncidentEntry>`: DedupExact 관련 로직 수행 함수
  - `[private] ResolvePanicBlend(List<IncidentEntry> sameInfoTypeEntries)` ➔ `List<float>`: ResolvePanicBlend 관련 로직 수행 함수
  - `[private] RepresentativeChange(List<IncidentEntry> sameInfoTypeEntries)` ➔ `float`: RepresentativeChange 관련 로직 수행 함수
  - `[public] ComputeGlobalReflectionAmount(List<IncidentEntry> group)` ➔ `float`: ComputeGlobalReflectionAmount 관련 로직 수행 함수
  - `[public] ComposeUnderstanding(float speciesApplied, float individualApplied, bool isSpecialUnit)` ➔ `float`: ComposeUnderstanding 관련 로직 수행 함수
  - `[private] Clamp(speciesPart + individualPart, WeightType.Understanding)` ➔ `return`: Clamp 관련 로직 수행 함수
  - `[private] PoolOverflowResult(float overflow, float floor, float capacity, float actual)` ➔ `public`: PoolOverflowResult 관련 로직 수행 함수
  - `[public] ComputePoolDecrease(float currentTotal, float incomingIncrease, float poolCap, float staleTargetCurrentValue)` ➔ `PoolOverflowResult`: ComputePoolDecrease 관련 로직 수행 함수

#### 📄 클래스: `PoolOverflowResult` (struct)
- **경로**: `Script/Unit/Weight/WeightMath.cs`
- **클래스 목적 및 의도**: `PoolOverflowResult`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] UnderstandingMin` (float): PoolOverflowResult의 내부 데이터 또는 의존 참조 변수
  - `[public] DangerMin` (float): PoolOverflowResult의 내부 데이터 또는 의존 참조 변수
  - `[public] InterestMin` (float): PoolOverflowResult의 내부 데이터 또는 의존 참조 변수
  - `[private] distinct` (var): PoolOverflowResult의 내부 데이터 또는 의존 참조 변수
  - `[private] sum` (float): PoolOverflowResult의 내부 데이터 또는 의존 참조 변수
  - `[private] result` (var): PoolOverflowResult의 내부 데이터 또는 의존 참조 변수
  - `[private] result` (return): PoolOverflowResult의 내부 데이터 또는 의존 참조 변수
  - `[private] seen` (var): PoolOverflowResult의 내부 데이터 또는 의존 참조 변수
  - `[private] result` (var): PoolOverflowResult의 내부 데이터 또는 의존 참조 변수
  - `[private] result` (return): PoolOverflowResult의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Clamp(float value, WeightType type)` ➔ `float`: Clamp 관련 로직 수행 함수
  - `[public] DedupExact(IEnumerable<IncidentEntry> entries)` ➔ `List<IncidentEntry>`: DedupExact 관련 로직 수행 함수
  - `[private] ResolvePanicBlend(List<IncidentEntry> sameInfoTypeEntries)` ➔ `List<float>`: ResolvePanicBlend 관련 로직 수행 함수
  - `[private] RepresentativeChange(List<IncidentEntry> sameInfoTypeEntries)` ➔ `float`: RepresentativeChange 관련 로직 수행 함수
  - `[public] ComputeGlobalReflectionAmount(List<IncidentEntry> group)` ➔ `float`: ComputeGlobalReflectionAmount 관련 로직 수행 함수
  - `[public] ComposeUnderstanding(float speciesApplied, float individualApplied, bool isSpecialUnit)` ➔ `float`: ComposeUnderstanding 관련 로직 수행 함수
  - `[private] Clamp(speciesPart + individualPart, WeightType.Understanding)` ➔ `return`: Clamp 관련 로직 수행 함수
  - `[private] PoolOverflowResult(float overflow, float floor, float capacity, float actual)` ➔ `public`: PoolOverflowResult 관련 로직 수행 함수
  - `[public] ComputePoolDecrease(float currentTotal, float incomingIncrease, float poolCap, float staleTargetCurrentValue)` ➔ `PoolOverflowResult`: ComputePoolDecrease 관련 로직 수행 함수

---

### 📦 Package: `VFX/Shader/Editor`

**모듈 내 포함 스크립트 수**: 4개

#### 📄 클래스: `FXFlatLabGUI` (class)
- **경로**: `VFX/Shader/Editor/FXAllInOneLabGUI.cs`
- **클래스 목적 및 의도**: 사용자 화면의 `FXFlatLabGUI` 요소 바인딩, 입력 이벤트 및 뷰 업데이트를 담당하는 UI 스크립트입니다.
- **상속 및 구현 관계**: `ShaderGUI` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] showRender` (bool): FXFlatLabGUI의 내부 데이터 또는 의존 참조 변수
  - `[private] showMain` (bool): FXFlatLabGUI의 내부 데이터 또는 의존 참조 변수
  - `[private] showMotion` (bool): FXFlatLabGUI의 내부 데이터 또는 의존 참조 변수
  - `[private] showNoise` (bool): FXFlatLabGUI의 내부 데이터 또는 의존 참조 변수
  - `[private] showMask` (bool): FXFlatLabGUI의 내부 데이터 또는 의존 참조 변수
  - `[private] showDissolve` (bool): FXFlatLabGUI의 내부 데이터 또는 의존 참조 변수
  - `[private] showGradient` (bool): FXFlatLabGUI의 내부 데이터 또는 의존 참조 변수
  - `[private] showDistortion` (bool): FXFlatLabGUI의 내부 데이터 또는 의존 참조 변수
  - `[private] showStyle` (bool): FXFlatLabGUI의 내부 데이터 또는 의존 참조 변수
  - `[private] p` (MaterialProperty): FXFlatLabGUI의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)` ➔ `void`: OnGUI 관련 로직 수행 함수
  - `[private] P(MaterialProperty[] props, string name)` ➔ `MaterialProperty`: P 관련 로직 수행 함수
  - `[private] FindProperty(name, props, false)` ➔ `return`: FindProperty 관련 로직 수행 함수
  - `[private] DrawProp(MaterialEditor editor, MaterialProperty[] props, string name)` ➔ `void`: DrawProp 관련 로직 수행 함수
  - `[private] DrawTex(MaterialEditor editor, MaterialProperty[] props, string texName, string colorName = null)` ➔ `void`: DrawTex 관련 로직 수행 함수
  - `[private] DrawPresetButtons(MaterialEditor editor)` ➔ `void`: DrawPresetButtons 관련 로직 수행 함수
  - `[private] SetFloat(MaterialEditor editor, string prop, float value)` ➔ `void`: SetFloat 관련 로직 수행 함수
  - `[private] DrawRender(MaterialEditor editor, MaterialProperty[] props)` ➔ `void`: DrawRender 관련 로직 수행 함수

#### 📄 클래스: `FXMaterialDesignerWindow` (class)
- **경로**: `VFX/Shader/Editor/FXMaterialDesignerWindow.cs`
- **클래스 목적 및 의도**: `FXMaterialDesignerWindow`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `EditorWindow` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] targetType` (TargetType): FXMaterialDesignerWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] stylePreset` (StylePreset): FXMaterialDesignerWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] materialName` (string): FXMaterialDesignerWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] tintColor` (Color): FXMaterialDesignerWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] emissionColor` (Color): FXMaterialDesignerWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] alpha` (float): FXMaterialDesignerWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] emissionPower` (float): FXMaterialDesignerWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] noiseStrength` (float): FXMaterialDesignerWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] noiseScale` (float): FXMaterialDesignerWindow의 내부 데이터 또는 의존 참조 변수
  - `[private] scrollX` (float): FXMaterialDesignerWindow의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Open()` ➔ `void`: Open 관련 로직 수행 함수
  - `[private] OnGUI()` ➔ `void`: OnGUI 관련 로직 수행 함수
  - `[private] ApplyPreset()` ➔ `void`: ApplyPreset 관련 로직 수행 함수

#### 📄 클래스: `TargetType` (enum)
- **경로**: `VFX/Shader/Editor/FXMaterialDesignerWindow.cs`
- **클래스 목적 및 의도**: `TargetType`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] targetType` (TargetType): TargetType의 내부 데이터 또는 의존 참조 변수
  - `[private] stylePreset` (StylePreset): TargetType의 내부 데이터 또는 의존 참조 변수
  - `[private] materialName` (string): TargetType의 내부 데이터 또는 의존 참조 변수
  - `[private] tintColor` (Color): TargetType의 내부 데이터 또는 의존 참조 변수
  - `[private] emissionColor` (Color): TargetType의 내부 데이터 또는 의존 참조 변수
  - `[private] alpha` (float): TargetType의 내부 데이터 또는 의존 참조 변수
  - `[private] emissionPower` (float): TargetType의 내부 데이터 또는 의존 참조 변수
  - `[private] noiseStrength` (float): TargetType의 내부 데이터 또는 의존 참조 변수
  - `[private] noiseScale` (float): TargetType의 내부 데이터 또는 의존 참조 변수
  - `[private] scrollX` (float): TargetType의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Open()` ➔ `void`: Open 관련 로직 수행 함수
  - `[private] OnGUI()` ➔ `void`: OnGUI 관련 로직 수행 함수
  - `[private] ApplyPreset()` ➔ `void`: ApplyPreset 관련 로직 수행 함수

#### 📄 클래스: `StylePreset` (enum)
- **경로**: `VFX/Shader/Editor/FXMaterialDesignerWindow.cs`
- **클래스 목적 및 의도**: `StylePreset`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[private] targetType` (TargetType): StylePreset의 내부 데이터 또는 의존 참조 변수
  - `[private] stylePreset` (StylePreset): StylePreset의 내부 데이터 또는 의존 참조 변수
  - `[private] materialName` (string): StylePreset의 내부 데이터 또는 의존 참조 변수
  - `[private] tintColor` (Color): StylePreset의 내부 데이터 또는 의존 참조 변수
  - `[private] emissionColor` (Color): StylePreset의 내부 데이터 또는 의존 참조 변수
  - `[private] alpha` (float): StylePreset의 내부 데이터 또는 의존 참조 변수
  - `[private] emissionPower` (float): StylePreset의 내부 데이터 또는 의존 참조 변수
  - `[private] noiseStrength` (float): StylePreset의 내부 데이터 또는 의존 참조 변수
  - `[private] noiseScale` (float): StylePreset의 내부 데이터 또는 의존 참조 변수
  - `[private] scrollX` (float): StylePreset의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Open()` ➔ `void`: Open 관련 로직 수행 함수
  - `[private] OnGUI()` ➔ `void`: OnGUI 관련 로직 수행 함수
  - `[private] ApplyPreset()` ➔ `void`: ApplyPreset 관련 로직 수행 함수

---

### 📦 Package: `Wave`

**모듈 내 포함 스크립트 수**: 9개

#### 📄 클래스: `WaveState` (enum)
- **경로**: `Script/Wave/HumanWaveManager.cs`
- **클래스 목적 및 의도**: `WaveState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] waveCooldown` (float): WaveState의 내부 데이터 또는 의존 참조 변수
  - `[public] targetSpawner` (WaveSpawner): WaveState의 내부 데이터 또는 의존 참조 변수
  - `[public] currentState` (WaveState): WaveState의 내부 데이터 또는 의존 참조 변수
  - `[public] cooldownTimer` (float): WaveState의 내부 데이터 또는 의존 참조 변수
  - `[public] activeParty` (Party): WaveState의 내부 데이터 또는 의존 참조 변수
  - `[public] dummyTarget` (InteractableObject): WaveState의 내부 데이터 또는 의존 참조 변수
  - `[public] targetState` (DummyTargetState): WaveState의 내부 데이터 또는 의존 참조 변수
  - `[public] targetCarrier` (Human): WaveState의 내부 데이터 또는 의존 참조 변수
  - `[public] exitAreaPos` (Vector2Int): WaveState의 내부 데이터 또는 의존 참조 변수
  - `[private] PreSpawnLeadSeconds` (float): WaveState의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(System.Threading.CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[private] WaveLoop(System.Threading.CancellationToken cts)` ➔ `UniTaskVoid`: WaveLoop 관련 로직 수행 함수
  - `[private] PreSpawnWaveUnits()` ➔ `void`: PreSpawnWaveUnits 관련 로직 수행 함수
  - `[private] ResolveStairPositions()` ➔ `bool`: ResolveStairPositions 관련 로직 수행 함수

#### 📄 클래스: `DummyTargetState` (enum)
- **경로**: `Script/Wave/HumanWaveManager.cs`
- **클래스 목적 및 의도**: `DummyTargetState`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] waveCooldown` (float): DummyTargetState의 내부 데이터 또는 의존 참조 변수
  - `[public] targetSpawner` (WaveSpawner): DummyTargetState의 내부 데이터 또는 의존 참조 변수
  - `[public] currentState` (WaveState): DummyTargetState의 내부 데이터 또는 의존 참조 변수
  - `[public] cooldownTimer` (float): DummyTargetState의 내부 데이터 또는 의존 참조 변수
  - `[public] activeParty` (Party): DummyTargetState의 내부 데이터 또는 의존 참조 변수
  - `[public] dummyTarget` (InteractableObject): DummyTargetState의 내부 데이터 또는 의존 참조 변수
  - `[public] targetState` (DummyTargetState): DummyTargetState의 내부 데이터 또는 의존 참조 변수
  - `[public] targetCarrier` (Human): DummyTargetState의 내부 데이터 또는 의존 참조 변수
  - `[public] exitAreaPos` (Vector2Int): DummyTargetState의 내부 데이터 또는 의존 참조 변수
  - `[private] PreSpawnLeadSeconds` (float): DummyTargetState의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(System.Threading.CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[private] WaveLoop(System.Threading.CancellationToken cts)` ➔ `UniTaskVoid`: WaveLoop 관련 로직 수행 함수
  - `[private] PreSpawnWaveUnits()` ➔ `void`: PreSpawnWaveUnits 관련 로직 수행 함수
  - `[private] ResolveStairPositions()` ➔ `bool`: ResolveStairPositions 관련 로직 수행 함수

#### 📄 클래스: `HumanWaveManager` (class)
- **경로**: `Script/Wave/HumanWaveManager.cs`
- **클래스 목적 및 의도**: 게임 내 `HumanWave` 관련 전역 상태 및 루프 시스템을 총괄 관리하는 매니저 클래스입니다.
- **상속 및 구현 관계**: `NativeRoutine` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] waveCooldown` (float): HumanWaveManager의 내부 데이터 또는 의존 참조 변수
  - `[public] targetSpawner` (WaveSpawner): HumanWaveManager의 내부 데이터 또는 의존 참조 변수
  - `[public] currentState` (WaveState): HumanWaveManager의 내부 데이터 또는 의존 참조 변수
  - `[public] cooldownTimer` (float): HumanWaveManager의 내부 데이터 또는 의존 참조 변수
  - `[public] activeParty` (Party): HumanWaveManager의 내부 데이터 또는 의존 참조 변수
  - `[public] dummyTarget` (InteractableObject): HumanWaveManager의 내부 데이터 또는 의존 참조 변수
  - `[public] targetState` (DummyTargetState): HumanWaveManager의 내부 데이터 또는 의존 참조 변수
  - `[public] targetCarrier` (Human): HumanWaveManager의 내부 데이터 또는 의존 참조 변수
  - `[public] exitAreaPos` (Vector2Int): HumanWaveManager의 내부 데이터 또는 의존 참조 변수
  - `[private] PreSpawnLeadSeconds` (float): HumanWaveManager의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] Initialize(System.Threading.CancellationToken cts)` ➔ `UniTask`: Initialize 관련 로직 수행 함수
  - `[private] WaveLoop(System.Threading.CancellationToken cts)` ➔ `UniTaskVoid`: WaveLoop 관련 로직 수행 함수
  - `[private] PreSpawnWaveUnits()` ➔ `void`: PreSpawnWaveUnits 관련 로직 수행 함수
  - `[private] ResolveStairPositions()` ➔ `bool`: ResolveStairPositions 관련 로직 수행 함수

#### 📄 클래스: `WaveUnitGroup` (class)
- **경로**: `Script/Wave/WaveData.cs`
- **클래스 목적 및 의도**: `WaveUnitGroup`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] unitTypeName` (string): WaveUnitGroup의 내부 데이터 또는 의존 참조 변수
  - `[public] count` (int): WaveUnitGroup의 내부 데이터 또는 의존 참조 변수
  - `[public] partyName` (string): WaveUnitGroup의 내부 데이터 또는 의존 참조 변수
  - `[public] faction` (PartyFaction): WaveUnitGroup의 내부 데이터 또는 의존 참조 변수
  - `[public] units` (List<WaveUnitGroup>): WaveUnitGroup의 내부 데이터 또는 의존 참조 변수
  - `[public] waveCooldown` (float): WaveUnitGroup의 내부 데이터 또는 의존 참조 변수
  - `[public] spawnMode` (SpawnMode): WaveUnitGroup의 내부 데이터 또는 의존 참조 변수
  - `[public] targetFloor` (int): WaveUnitGroup의 내부 데이터 또는 의존 참조 변수
  - `[public] spawnCenter` (Vector3): WaveUnitGroup의 내부 데이터 또는 의존 참조 변수
  - `[public] spawnTileRadius` (int): WaveUnitGroup의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `PartyFaction` (enum)
- **경로**: `Script/Wave/WaveData.cs`
- **클래스 목적 및 의도**: `PartyFaction`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] unitTypeName` (string): PartyFaction의 내부 데이터 또는 의존 참조 변수
  - `[public] count` (int): PartyFaction의 내부 데이터 또는 의존 참조 변수
  - `[public] partyName` (string): PartyFaction의 내부 데이터 또는 의존 참조 변수
  - `[public] faction` (PartyFaction): PartyFaction의 내부 데이터 또는 의존 참조 변수
  - `[public] units` (List<WaveUnitGroup>): PartyFaction의 내부 데이터 또는 의존 참조 변수
  - `[public] waveCooldown` (float): PartyFaction의 내부 데이터 또는 의존 참조 변수
  - `[public] spawnMode` (SpawnMode): PartyFaction의 내부 데이터 또는 의존 참조 변수
  - `[public] targetFloor` (int): PartyFaction의 내부 데이터 또는 의존 참조 변수
  - `[public] spawnCenter` (Vector3): PartyFaction의 내부 데이터 또는 의존 참조 변수
  - `[public] spawnTileRadius` (int): PartyFaction의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `WavePartyConfig` (class)
- **경로**: `Script/Wave/WaveData.cs`
- **클래스 목적 및 의도**: `WavePartyConfig` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] unitTypeName` (string): WavePartyConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] count` (int): WavePartyConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] partyName` (string): WavePartyConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] faction` (PartyFaction): WavePartyConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] units` (List<WaveUnitGroup>): WavePartyConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] waveCooldown` (float): WavePartyConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] spawnMode` (SpawnMode): WavePartyConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] targetFloor` (int): WavePartyConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] spawnCenter` (Vector3): WavePartyConfig의 내부 데이터 또는 의존 참조 변수
  - `[public] spawnTileRadius` (int): WavePartyConfig의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `WaveData` (class)
- **경로**: `Script/Wave/WaveData.cs`
- **클래스 목적 및 의도**: `WaveData` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: `ScriptableObject` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] unitTypeName` (string): WaveData의 내부 데이터 또는 의존 참조 변수
  - `[public] count` (int): WaveData의 내부 데이터 또는 의존 참조 변수
  - `[public] partyName` (string): WaveData의 내부 데이터 또는 의존 참조 변수
  - `[public] faction` (PartyFaction): WaveData의 내부 데이터 또는 의존 참조 변수
  - `[public] units` (List<WaveUnitGroup>): WaveData의 내부 데이터 또는 의존 참조 변수
  - `[public] waveCooldown` (float): WaveData의 내부 데이터 또는 의존 참조 변수
  - `[public] spawnMode` (SpawnMode): WaveData의 내부 데이터 또는 의존 참조 변수
  - `[public] targetFloor` (int): WaveData의 내부 데이터 또는 의존 참조 변수
  - `[public] spawnCenter` (Vector3): WaveData의 내부 데이터 또는 의존 참조 변수
  - `[public] spawnTileRadius` (int): WaveData의 내부 데이터 또는 의존 참조 변수
- **주요 함수**: 정의된 커스텀 주요 함수 없음

#### 📄 클래스: `SpawnMode` (enum)
- **경로**: `Script/Wave/WaveSpawner.cs`
- **클래스 목적 및 의도**: `SpawnMode`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: 독립 구조 클래스
- **주요 변수 (State & Dependencies)**:
  - `[public] waveData` (WaveData): SpawnMode의 내부 데이터 또는 의존 참조 변수
  - `[private] spawnedMonsters` (List<Monster>): SpawnMode의 내부 데이터 또는 의존 참조 변수
  - `[private] spawnedParties` (List<Party>): SpawnMode의 내부 데이터 또는 의존 참조 변수
  - `[private] monster` (Monster): SpawnMode의 내부 데이터 또는 의존 참조 변수
  - `[private] members` (List<Human>): SpawnMode의 내부 데이터 또는 의존 참조 변수
  - `[private] human` (Human): SpawnMode의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] SpawnWave()` ➔ `void`: SpawnWave 관련 로직 수행 함수
  - `[private] ResolveUnitType(string typeName)` ➔ `Type`: ResolveUnitType 관련 로직 수행 함수
  - `[private] CreateUnitTypeInstance(string typeName)` ➔ `UnitType`: CreateUnitTypeInstance 관련 로직 수행 함수

#### 📄 클래스: `WaveSpawner` (class)
- **경로**: `Script/Wave/WaveSpawner.cs`
- **클래스 목적 및 의도**: `WaveSpawner`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `Haare.Client.Routine.NativeRoutine` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[public] waveData` (WaveData): WaveSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] spawnedMonsters` (List<Monster>): WaveSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] spawnedParties` (List<Party>): WaveSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] monster` (Monster): WaveSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] members` (List<Human>): WaveSpawner의 내부 데이터 또는 의존 참조 변수
  - `[private] human` (Human): WaveSpawner의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[public] SpawnWave()` ➔ `void`: SpawnWave 관련 로직 수행 함수
  - `[private] ResolveUnitType(string typeName)` ➔ `Type`: ResolveUnitType 관련 로직 수행 함수
  - `[private] CreateUnitTypeInstance(string typeName)` ➔ `UnitType`: CreateUnitTypeInstance 관련 로직 수행 함수

---

### 📦 Package: `Wave/Editor`

**모듈 내 포함 스크립트 수**: 2개

#### 📄 클래스: `WaveDataEditor` (class)
- **경로**: `Script/Wave/Editor/WaveDataEditor.cs`
- **클래스 목적 및 의도**: `WaveDataEditor` 관련 설정값, 파라미터 및 런타임 데이터를 유지 및 제공하는 데이터 구조체/에셋입니다.
- **상속 및 구현 관계**: `UnityEditor.Editor` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] waveCooldownProp` (SerializedProperty): WaveDataEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] spawnModeProp` (SerializedProperty): WaveDataEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] targetFloorProp` (SerializedProperty): WaveDataEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] spawnCenterProp` (SerializedProperty): WaveDataEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] spawnTileRadiusProp` (SerializedProperty): WaveDataEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] targetRoomRoleProp` (SerializedProperty): WaveDataEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] targetRoomIdProp` (SerializedProperty): WaveDataEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] partiesProp` (SerializedProperty): WaveDataEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] mode` (SpawnMode): WaveDataEditor의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] OnEnable()` ➔ `void`: OnEnable 관련 로직 수행 함수
  - `[public] OnInspectorGUI()` ➔ `void`: OnInspectorGUI 관련 로직 수행 함수

#### 📄 클래스: `WaveSpawnerEditor` (class)
- **경로**: `Script/Wave/Editor/WaveSpawnerEditor.cs`
- **클래스 목적 및 의도**: `WaveSpawnerEditor`의 기능과 상태를 정의하는 스크립트입니다.
- **상속 및 구현 관계**: `UnityEditor.Editor` 상속/구현
- **주요 변수 (State & Dependencies)**:
  - `[private] waveDataProp` (SerializedProperty): WaveSpawnerEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] newData` (WaveData): WaveSpawnerEditor의 내부 데이터 또는 의존 참조 변수
  - `[private] path` (string): WaveSpawnerEditor의 내부 데이터 또는 의존 참조 변수
- **주요 함수 (Methods & Behaviors)**:
  - `[private] OnEnable()` ➔ `void`: OnEnable 관련 로직 수행 함수
  - `[private] OnDisable()` ➔ `void`: OnDisable 관련 로직 수행 함수
  - `[public] OnInspectorGUI()` ➔ `void`: OnInspectorGUI 관련 로직 수행 함수

---

## 3. 🔄 주요 런타임 데이터 흐름 (Runtime Flow)

1. **게임 세션 초기화 (`GameSession`)**:
   - `CreateMap`을 통해 그리드 타일맵 및 Floor 정보 동적 생성.
   - `FactionData.InitMap(cmap)`을 통해 세션의 시야/탐색 배열 생성 및 할당.
   - `UnitGenerate`를 통해 초기 아군/적 유닛 턴 스케줄러 등록.

2. **유닛 행동 및 시야 갱신 루프 (`UnitFunction`)**:
   - `GameSession.UpdateProcess` 실행 시 유닛별 `UpdateFOV(allUnits)` 호출.
   - `CastRay` 함수가 시야각(120도) 및 인지 거리에 맞춰 타일/오브젝트 감지.
   - 감지된 정보는 유닛 개인 지도(`PersonalMapKnowledge`) 및 진영 공유 지도(`discoveredMap`)에 실시간 누적.

3. **AI 판단 및 이동 (`AStarMovement` & GOAP)**:
   - 감지된 타일 위험도/흥미도 가중치를 기반으로 A* 알고리즘 경로 산출.
   - 이동 및 공격 수행 후 UI 및 VFX 이벤트 발행.

