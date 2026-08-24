# Project: Monster Data Registration & Prefab Generation for Mon_DollKnight, Mon_GnoleA, Mon_GoblinHoodA

## Architecture
- **Data Layer**: `Assets/Data/units.json`, `Assets/Data/skills.json`
- **Asset / Sprite Layer**: `Assets/Sprite/Mon/Mon_DollKnight/Mon_DollKnight.spriteLib`, `Assets/Sprite/Mon/Mon_Gnole/Mon_GnoleA.spriteLib`, `Assets/Sprite/Mon/Mon_GoblinHood/Mon_GoblinHoodA.spriteLib`
- **Prefab / Resource Layer**: `Assets/Resources/Units/인형 기사.prefab`, `Assets/Resources/Units/놀.prefab`, `Assets/Resources/Units/고블린 후드.prefab`
- **Code / Type Layer**: `Assets/Script/Unit/Core/UnitTypes.cs` (`DollKnight`, `GnoleA`, `GoblinHoodA` deriving from `UnitType`), `Assets/Script/Wave/WaveSpawner.cs`, `Assets/Script/Unit/Visual/UnitSpriteManager.cs`
- **Editor Tooling**: `Assets/Editor/JsonToUnitPrefabConverter.cs`

## Feature Inventory
| # | Feature | Description | Milestone | Source | Status |
|---|---------|-------------|-----------|--------|:------:|
| 1 | `units.json` Registration: `Mon_DollKnight` (인형 기사) | Add unit entry with Monster class, melee tank stats & skills, spriteLibrary "Mon_DollKnight" | M1 | Survey / User Request | DONE |
| 2 | `units.json` Registration: `Mon_GnoleA` (놀) | Add unit entry with Monster class, melee tank stats & skills, spriteLibrary "Mon_GnoleA" | M1 | Survey / User Request | DONE |
| 3 | `units.json` Registration: `Mon_GoblinHoodA` (고블린 후드) | Add unit entry with Monster class, melee tank stats & skills, spriteLibrary "Mon_GoblinHoodA" | M1 | Survey / User Request | DONE |
| 4 | C# UnitType Classes | Add `DollKnight`, `GnoleA`, `GoblinHoodA` in `UnitTypes.cs` matching `typeName` for reflection resolver | M1 | Survey / Architecture | DONE |
| 5 | Prefab Creation & Asset Linking | Generate / create `인형 기사.prefab`, `놀.prefab`, `고블린 후드.prefab` in `Assets/Resources/Units/` with SpriteLibrary GUIDs & UnitVisualDefinition | M1 | Survey / User Request | DONE |
| 6 | E2E & Build Verification | Build project with `dotnet build Assembly-CSharp-Editor.csproj`, verify JSON validity, prefab resource loading, and test suite | M2 | User Request / Testing | DONE |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|:------:|
| M1 | Monster Data & Prefab Implementation | Update `units.json`, `UnitTypes.cs`, and create prefabs in `Assets/Resources/Units/` | none | DONE |
| M2 | E2E Testing & Verification | Validate JSON, compile project, verify prefab linking & runtime loadability | M1 | DONE |

## Interface Contracts
### `units.json` ↔ `JsonToUnitPrefabConverter` / `UnitVisualDefinition`
- `typeName`: string matching prefab filename and `UnitType.typeName`
- `unitClass`: `"Monster"`
- `spriteLibrary`: string matching asset name in `Assets/Sprite/Mon/`
- `skills`: `["육중한 내리찍기", "급습 할퀴기", "발톱 후려치기"]` (matching `skills.json`)
- `stats`: Complete 20-field melee tank stats
- `weight`: Complete 7-field weight dictionary
- `visual.effects`: Hit spark, blood drip, guard, parry VFX prefabs

### `Assets/Resources/Units/` ↔ `UnitSpriteManager`
- Prefab path: `Assets/Resources/Units/{typeName}.prefab`
- Root component: `UnitVisualDefinition`
- Child GameObject `Visual`: `SpriteRenderer`, `SpriteLibrary` (with `.spriteLib` GUID), `SpriteResolver`, `ShadowCaster2D`

## Code Layout
- `Assets/Data/units.json`: Unit definitions
- `Assets/Script/Unit/Core/UnitTypes.cs`: UnitType subclasses
- `Assets/Resources/Units/`: Unit prefabs and `.meta` files
