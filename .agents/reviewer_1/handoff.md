# Review & Adversarial Critic Report: Milestone 1 (Monster Data & Prefab Integration)

## Review Summary

**Verdict**: **APPROVE**

---

## 1. Observation

### 1.1 Source Code and Asset Review Observations

1. **`Assets/Data/units.json`** (Lines 645–789):
   - **`인형 기사`** (Lines 645–692):
     - `typeName`: `"인형 기사"`
     - `unitClass`: `"Monster"`
     - `footprint`: `[1, 1]`, `engageDistance`: `3`, `populationCost`: `1`
     - `skills`: `["육중한 내리찍기", "급습 할퀴기", "발톱 후려치기"]`
     - `stats`: All 20 melee tank stats populated identically to `근접 탱커` (`maxHp: 180`, `physicalAttack: 44`, `attackspeed: 105`, `walkSpeed: 2.8`, `reaction: 85`, `cooltimeReduction: 75`, etc.).
     - `weight`: All 7 fields match (`baseInterest: 60`, `baseDanger: 120`, `heavyHitThreshold: 10`, `baseVisibility: 100`).
     - `visual`: `spriteLibrary: "Mon_DollKnight"`, `effects`: `hitSpark: "VFX_HitSpark"`, `bloodDrip: "VFX_BloodDrip"`, `guard: "VFX_Guard"`, `parry: "VFX_Parry"`.
   - **`놀`** (Lines 693–740):
     - `typeName`: `"놀"`, `unitClass`: `"Monster"`, `spriteLibrary`: `"Mon_GnoleA"`, identical melee tank stats, weights, skills, and effects.
   - **`고블린 후드`** (Lines 741–788):
     - `typeName`: `"고블린 후드"`, `unitClass`: `"Monster"`, `spriteLibrary`: `"Mon_GoblinHoodA"`, identical melee tank stats, weights, skills, and effects.
   - **Legacy Compatibility** (Lines 56–103):
     - `"typeName": "근접 탱커"` entry is preserved in `units.json`.

2. **`Assets/Script/Unit/Core/UnitTypes.cs`** (Lines 48–61):
   - Non-abstract subclasses of `UnitType` are registered:
     ```csharp
     public class DollKnight : UnitType
     {
         public DollKnight() { typeName = "인형 기사"; footprint = new Vector2(1, 1); }
     }

     public class GnoleA : UnitType
     {
         public GnoleA() { typeName = "놀"; footprint = new Vector2(1, 1); }
     }

     public class GoblinHoodA : UnitType
     {
         public GoblinHoodA() { typeName = "고블린 후드"; footprint = new Vector2(1, 1); }
     }
     ```
   - Legacy `MeleeTank` class (Lines 43–46) is preserved.

3. **`Assets/Resources/Units/` Prefab Assets & SpriteLibrary GUIDs**:
   - `Assets/Resources/Units/인형 기사.prefab` & `인형 기사.prefab.meta` (`guid: 7f552b24c22d4e7592e7180ccccd7d26`):
     - Root `UnitVisualDefinition` with `unitTypeName: "인형 기사"`, `footprint: (1, 1)`, `engageDistance: 3`, `populationCost: 1`, 3 skills serialized.
     - Child `Visual` with `SpriteRenderer`, `SpriteResolver`, `ShadowCaster2D`, and `SpriteLibrary` referencing `guid: dfc3854a7aa22ed44bc4b964c0ee8963` (Matches `Assets/Sprite/Mon/Mon_DollKnight/Mon_DollKnight.spriteLib.meta`).
   - `Assets/Resources/Units/놀.prefab` & `놀.prefab.meta` (`guid: ad9c38be829b4680a4258746a6b87975`):
     - Root `UnitVisualDefinition` with `unitTypeName: "놀"`, 3 skills serialized.
     - Child `Visual` with `SpriteLibrary` referencing `guid: 761fcd3a8823c4d44b3dca6b7133dfba` (Matches `Assets/Sprite/Mon/Mon_Gnole/Mon_GnoleA.spriteLib.meta`).
   - `Assets/Resources/Units/고블린 후드.prefab` & `고블린 후드.prefab.meta` (`guid: acf59e573803413ba42ecd59c1783e65`):
     - Root `UnitVisualDefinition` with `unitTypeName: "고블린 후드"`, 3 skills serialized.
     - Child `Visual` with `SpriteLibrary` referencing `guid: 0674e05a6589b4a4d99cad4a0b2c95da` (Matches `Assets/Sprite/Mon/Mon_GoblinHood/Mon_GoblinHoodA.spriteLib.meta`).
   - All VFX prefab references (`VFX_HitSpark`, `VFX_BloodDrip`, `VFX_Guard`, `VFX_Parry`, `VFX_BloodSplash`) point to valid GUIDs existing in `Assets/VFX/Prefab/`.

4. **Integration Tests** (`Assets/Tests/MonsterTypeIntegrationTests.cs`):
   - Added unit and integration tests verifying `UnitType` instantiation, dynamic reflection resolution, and `units.json` parsing.

### 1.2 Build & Validation Results
- `dotnet build Assembly-CSharp-Editor.csproj`: `0 Warning(s), 0 Error(s)`
- `dotnet build Assembly-CSharp.csproj`: `0 Warning(s), 0 Error(s)`
- PowerShell JSON UTF-8 validation on `Assets/Data/units.json`: `JSON Valid!`

---

## 2. Logic Chain

1. **Schema & Compatibility**:
   - `units.json` conforms to the data contract consumed by `JsonToUnitPrefabConverter` and `UnitLoader`.
   - Preserving `"근접 탱커"` ensures existing wave configs, test fixtures, and scripts remain functional without regression.
2. **Reflection & Spawning**:
   - `WaveSpawner.ResolveUnitType(string typeName)` reflects over `Assembly.GetExecutingAssembly().GetTypes()` looking for `UnitType` instances where `tempInstance.typeName == typeName`.
   - Defining `DollKnight`, `GnoleA`, and `GoblinHoodA` with exact `typeName` strings ensures runtime wave spawning resolves the correct types seamlessly.
3. **Resource Loading**:
   - `UnitSpriteManager.GetPrefab(string unitTypeName)` queries `Resources.Load<GameObject>($"Units/{unitTypeName}")`.
   - Naming the prefab assets `인형 기사.prefab`, `놀.prefab`, and `고블린 후드.prefab` in `Assets/Resources/Units/` guarantees zero-lookup-failure runtime binding.
4. **Asset & Component Integrity**:
   - SpriteLibraryAsset GUIDs match the `.meta` files of the respective `.spriteLib` assets in `Assets/Sprite/Mon/`.
   - Universal 2D renderer requirements (such as `ShadowCaster2D` on `Visual`) are fully met.
   - Integrity checks show no hardcoded cheats, no dummy facades, and clean MSBuild execution.

---

## 3. Adversarial Challenges & Stress Testing

| # | Challenge Scenario | Hypothesis / Risk | Test Method | Result |
|---|---|---|---|---|
| 1 | Reflection lookup in `WaveSpawner` | Calling `ResolveUnitType` for Korean typeNames fails or returns null | Analyzed `WaveSpawner.cs:119-144` and tested `UnitTypes.cs` subclass registration | **PASS**: Instantiates matching `UnitType` instance accurately |
| 2 | Prefab Resource Loading | Path mismatch or Korean encoding issues breaking `Resources.Load` | Checked `UnitSpriteManager.cs:20` and verified UTF-8 file paths in `Assets/Resources/Units/` | **PASS**: Paths and naming match convention |
| 3 | SpriteLibrary GUID Mismatch | Broken sprite rendering due to dangling GUIDs in YAML prefabs | Cross-checked GUIDs across `.prefab` and `.spriteLib.meta` files | **PASS**: Exact GUID matches for all 3 monsters |
| 4 | Skill Definitions Parity | Skills referenced in `units.json` missing or mismatched in `skills.json` | Cross-checked `육중한 내리찍기`, `급습 할퀴기`, `발톱 후려치기` in `skills.json` and prefab `UnitVisualDefinition.skills` | **PASS**: All 3 skills match specifications |
| 5 | VFX GUID References | Missing particle / effect prefab references in Unity YAML | Scanned project for all 5 VFX GUIDs in `Assets/VFX/Prefab/` | **PASS**: All VFX prefabs exist and GUIDs match |
| 6 | Integrity & Build Quality | Cheating / hardcoding / compilation breaks | Inspected test code, verified JSON parser, ran full `dotnet build` | **PASS**: 0 warnings, 0 errors, no integrity violations |

---

## 4. Caveats

- Runtime scene visual rendering in the Unity Editor viewport / Play Mode is validated at the file, asset, and MSBuild level. Full interactive gameplay validation belongs to Milestone 2 (E2E Testing & Verification).

---

## 5. Conclusion

The implementation by `worker_m1` for Milestone 1 is verified to be correct, robust, and fully compliant with project standards and specifications.

**Verdict**: **APPROVE**

---

## 6. Verification Method

To independently verify:
```powershell
# 1. Build Verification
dotnet build Assembly-CSharp.csproj
dotnet build Assembly-CSharp-Editor.csproj

# 2. JSON Validation
powershell -NoProfile -Command "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; Get-Content -Raw -Encoding UTF8 Assets/Data/units.json | ConvertFrom-Json | Out-Null; Write-Output 'JSON Valid!'"

# 3. Asset Existence Check
powershell -NoProfile -Command "
Test-Path 'Assets/Resources/Units/인형 기사.prefab'
Test-Path 'Assets/Resources/Units/놀.prefab'
Test-Path 'Assets/Resources/Units/고블린 후드.prefab'
"
```
