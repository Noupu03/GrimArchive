# Handoff Report: JsonToUnitPrefabConverter & Prefab Architecture Investigation

## 1. Observation

### A. JsonToUnitPrefabConverter Implementation
- **File**: `Assets/Editor/JsonToUnitPrefabConverter.cs` (lines 1-318)
- **Menu Item**: `[MenuItem("Tools/GrimArchive/JSON -> 유닛 프리팹 생성 (원클릭)")] public static void ConvertJsonToPrefabs()` (lines 155-156)
- **Inputs & Output Paths**:
  - `UnitsJsonPath = "Assets/Data/units.json"` (line 23)
  - `SkillsJsonPath = "Assets/Data/skills.json"` (line 24)
  - `OutputFolder = "Assets/Resources/Units"` (line 25)
- **Conversion Flow**:
  - Reads `units.json` as `JsonUnitDatabase` and `skills.json` as `JsonSkillDatabase` (lines 169-170).
  - Iterates each unit `u` in `unitDb.units` and instantiates a root `GameObject(u.typeName)` (line 181).
  - Adds `UnitVisualDefinition` component and populates: `unitTypeName`, `footprint`, `engageDistance`, `populationCost`, `stats`, `skills` (looked up by skill name in `skillLookup`), `weight` (`isSpecialUnit`, `isInterestTarget`, `baseInterest`, `baseDanger`, `heavyHitThreshold`, `stealth`, `baseVisibility`), and visual effect prefabs (`hitSparkPrefab`, `bloodEffectPrefab`, `guardPrefab`, `parryPrefab`, `attackFailPrefab`) via `FindAssetByName<GameObject>` (lines 182-217).
  - Creates child `GameObject("Visual")` with `SpriteRenderer` (`sortingOrder = 10`) (lines 220-224).
  - If `u.visual.spriteLibrary` is present, attaches `SpriteLibrary` (`spriteLibraryAsset = FindAssetByName<SpriteLibraryAsset>(u.visual.spriteLibrary)`) and `SpriteResolver` (lines 226-232).
  - If `u.visual.weapon` is present, attaches child `GameObject("WeaponSocket")` with `SpriteRenderer` and `WeaponAttachment` (`nativeSpriteAngle`) (lines 237-248).
  - If `u.visual.animatorController` is present, adds `Animator` and `AnimationEventVfxSpawner` to the root (lines 250-255).
  - Saves prefab via `PrefabUtility.SaveAsPrefabAsset(root, $"{OutputFolder}/{SanitizeFileName(u.typeName)}.prefab")` (lines 257-258).

### B. Sprite Library Assets in `Assets/Sprite/Mon/`
- `Assets/Sprite/Mon/Mon_DollKnight/Mon_DollKnight.spriteLib` (meta GUID: `dfc3854a7aa22ed44bc4b964c0ee8963`)
- `Assets/Sprite/Mon/Mon_Gnole/Mon_GnoleA.spriteLib` (meta GUID: `761fcd3a8823c4d44b3dca6b7133dfba`)
- `Assets/Sprite/Mon/Mon_GoblinHood/Mon_GoblinHoodA.spriteLib` (meta GUID: `0674e05a6589b4a4d99cad4a0b2c95da`)

### C. Runtime Unit Loading & Prefab Consumption
- **`Assets/Script/Unit/Visual/UnitSpriteManager.cs`**:
  - `GetPrefab(string unitTypeName)` -> `Resources.Load<GameObject>("Units/" + unitTypeName)` (line 20).
  - `GetIcon(string unitTypeName)` -> finds `"Visual"` child -> `SpriteRenderer.sprite` (lines 26-39).
  - `GetSkills(string unitTypeName)` -> finds `UnitVisualDefinition` -> `BuildSkillActions()` (lines 41-51).
- **`Assets/Script/Unit/Visual/UnitGenerate.cs`**:
  - `SetupUnitVisual(Unit unit, float visualScale)` -> calls `_unitSpriteManager.GetPrefab(unit.unitType.typeName)`. If null, falls back to `BuildFallbackVisual(unit)` (lines 180-250).
- **`Assets/Script/Wave/WaveSpawner.cs`**:
  - `ResolveUnitType(string typeName)` uses reflection to find subclasses of `UnitType` whose `tempInstance.typeName == typeName` (lines 119-144).

### D. Existing Unit Prefab Structure (`Assets/Resources/Units/`)
- Checked prefabs: `근접 탱커.prefab`, `야생 몬스터 A.prefab`, `보스 골렘.prefab`.
- Structure:
  - **Root GameObject** (`[typeName]`):
    - `Transform` (position 0,0,0, scale 1,1,1)
    - `UnitVisualDefinition` MonoBehaviour (containing stats, skill list with data values, effect prefabs, weight parameters, engage distance, footprint, populationCost).
  - **Child GameObject `Visual`**:
    - `Transform` (position 0,0,0, scale 1,1,1)
    - `SpriteRenderer` (material guid `a97c105638bdf8b4a8650670310a4cd3`, sortingOrder 10 or 100)
    - `SpriteLibrary` (links to `SpriteLibraryAsset` guid)
    - `SpriteResolver`
    - `ShadowCaster2D` (Universal 2D pipeline shadow caster)
  - **Child GameObject `WeaponSocket`** (optional, present on weapon-wielding units):
    - `Transform`, `SpriteRenderer`, `WeaponAttachment`.

### E. Build & Compilation Commands
- Running bare `dotnet build` in root fails with MSB1011 because multiple `.csproj` files exist (`Assembly-CSharp.csproj`, `Assembly-CSharp-Editor.csproj`, `Assembly-CSharp-firstpass.csproj`, `ScratchProj/ScratchProj.csproj`).
- Running `dotnet build Assembly-CSharp-Editor.csproj` executes MSBuild cleanly on all project assemblies (`firstpass`, `Assembly-CSharp`, `Assembly-CSharp-Editor`), yielding:
  - `0 Warning(s)`, `0 Error(s)`.

---

## 2. Logic Chain

1. **Units Definition Chain**:
   - `units.json` defines all unit entries (Human, Monster, Wild).
   - Currently, `units.json` contains `"근접 탱커"` with `spriteLibrary: "Mon_GnoleA"`, `unitClass: "Monster"`, skills `["육중한 내리찍기", "급습 할퀴기", "발톱 후려치기"]`, and stats (`maxHp: 180`, `physicalAttack: 44`, `physicalDefense: 10`, etc.).
   - Adding 3 monster entries (`"인형 기사"`, `"놀"`, `"고블린 후드"`) with `unitClass: "Monster"` and their corresponding `spriteLibrary` names (`"Mon_DollKnight"`, `"Mon_GnoleA"`, `"Mon_GoblinHoodA"`) directly matches the existing schema and converter expectations.

2. **Prefab Generation & Resource Loading Chain**:
   - `JsonToUnitPrefabConverter.ConvertJsonToPrefabs()` generates prefabs under `Assets/Resources/Units/{typeName}.prefab`.
   - `UnitSpriteManager.GetPrefab(typeName)` retrieves prefabs via `Resources.Load<GameObject>("Units/" + typeName)`.
   - Therefore, the generated prefab names will be:
     - `Assets/Resources/Units/인형 기사.prefab`
     - `Assets/Resources/Units/놀.prefab`
     - `Assets/Resources/Units/고블린 후드.prefab`
   - Prefabs generated via `JsonToUnitPrefabConverter` or standalone YAML prefab files following the exact Unity YAML format can be loaded directly by `Resources.Load`.

3. **C# Code Type Resolution Chain**:
   - `WaveSpawner` / `UnitTypes.cs` resolves unit type classes at runtime.
   - Adding `DollKnight`, `GnoleA`, `GoblinHoodA` classes inheriting `UnitType` in `Assets/Script/Unit/Core/UnitTypes.cs` ensures `WaveSpawner.ResolveUnitType` and any test code can resolve `typeName` (`"인형 기사"`, `"놀"`, `"고블린 후드"`) without falling back or throwing warnings.

4. **Build Verification Chain**:
   - `dotnet build Assembly-CSharp-Editor.csproj` compiles both runtime and editor scripts.
   - Any modifications to `UnitTypes.cs` or editor scripts can be verified with `dotnet build Assembly-CSharp-Editor.csproj`.

---

## 3. Caveats

- `JsonToUnitPrefabConverter` relies on Unity Editor APIs (`UnityEditor.AssetDatabase`, `UnityEditor.PrefabUtility`, `UnityEditor.SerializedObject`). It can only execute within the Unity Editor environment (via Editor GUI menu or Unity batchmode CLI `Unity.exe -batchmode -quit -executeMethod JsonToUnitPrefabConverter.ConvertJsonToPrefabs`).
- If Unity Editor is not running in batchmode, prefabs can also be created or maintained as standard Unity YAML `.prefab` + `.prefab.meta` files matching `Assets/Resources/Units/` convention, ensuring `Resources.Load` works immediately even outside the Unity Editor GUI.
- `skills.json` already contains `"육중한 내리찍기"`, `"급습 할퀴기"`, `"발톱 후려치기"`, so no new skills need to be registered in `skills.json`.

---

## 4. Conclusion

- **Actionable Steps for Implementation**:
  1. **Update `Assets/Data/units.json`**:
     - Add `인형 기사` (`typeName: "인형 기사"`, `unitClass: "Monster"`, `spriteLibrary: "Mon_DollKnight"`).
     - Add `놀` (`typeName: "놀"`, `unitClass: "Monster"`, `spriteLibrary: "Mon_GnoleA"`).
     - Add `고블린 후드` (`typeName: "고블린 후드"`, `unitClass: "Monster"`, `spriteLibrary: "Mon_GoblinHoodA"`).
     - Retain identical stats, weight, skills (`["육중한 내리찍기", "급습 할퀴기", "발톱 후려치기"]`), and visual effects (`hitSpark: "VFX_HitSpark"`, `bloodDrip: "VFX_BloodDrip"`, `guard: "VFX_Guard"`, `parry: "VFX_Parry"`) as `근접 탱커`.
  2. **Add UnitType Subclasses in `Assets/Script/Unit/Core/UnitTypes.cs`**:
     - Define `DollKnight`, `GnoleA` (or `Gnole`), `GoblinHoodA` (or `GoblinHood`) with their respective `typeName` strings and `footprint = new Vector2(1, 1)`.
  3. **Create / Generate Prefabs in `Assets/Resources/Units/`**:
     - `인형 기사.prefab` + `인형 기사.prefab.meta` (SpriteLibrary GUID: `dfc3854a7aa22ed44bc4b964c0ee8963`)
     - `놀.prefab` + `놀.prefab.meta` (SpriteLibrary GUID: `761fcd3a8823c4d44b3dca6b7133dfba`)
     - `고블린 후드.prefab` + `고블린 후드.prefab.meta` (SpriteLibrary GUID: `0674e05a6589b4a4d99cad4a0b2c95da`)
  4. **Verify Compilation**:
     - Run `dotnet build Assembly-CSharp-Editor.csproj` to confirm 0 compilation errors.

---

## 5. Verification Method

1. **Verify JSON Integrity**:
   - Inspect `Assets/Data/units.json` using valid JSON parser / validator to ensure no syntax errors.
2. **Verify C# Build**:
   - Execute `dotnet build Assembly-CSharp-Editor.csproj` in `c:/Users/wish0/Kubonhun/자료/Kubonhun/Project_file/Team_Dummy/GrimArchive_Prototype`.
   - Verify `0 Warning(s), 0 Error(s)`.
3. **Verify Prefab Assets**:
   - Check that `Assets/Resources/Units/인형 기사.prefab`, `Assets/Resources/Units/놀.prefab`, `Assets/Resources/Units/고블린 후드.prefab` exist.
   - Inspect YAML contents to ensure `SpriteLibraryAsset` GUID matches each monster's `.spriteLib.meta` GUID.
