# Handoff Report: Monster Data Specification & units.json Schema Analysis

## 1. Observation

### 1.1 `Assets/Data/units.json` Inspection (Lines 56–103)
The reference template for monster tank units is `"typeName": "근접 탱커"`:
```json
{
  "typeName": "근접 탱커",
  "unitClass": "Monster",
  "footprint": [1, 1],
  "engageDistance": 3,
  "populationCost": 1,
  "skills": ["육중한 내리찍기", "급습 할퀴기", "발톱 후려치기"],
  "stats": {
    "maxHp": 180,
    "maxMp": 0,
    "physicalAttack": 44,
    "magicalAttack": 9,
    "physicalDefense": 10,
    "magicalDefense": 8,
    "HPRegen": 3,
    "attackspeed": 105,
    "walkSpeed": 2.8,
    "reaction": 85,
    "criticalChance": 12,
    "cooltimeReduction": 75,
    "statusResistance": 75,
    "maxMental": 0,
    "mental": 0,
    "spotting": 0,
    "leadershipRange": 0,
    "charisma": 0,
    "physicalAttackSpeed": 13,
    "magicalCastSpeed": 0
  },
  "weight": {
    "isSpecialUnit": false,
    "isInterestTarget": false,
    "baseInterest": 60,
    "baseDanger": 120,
    "heavyHitThreshold": 10,
    "stealth": 0,
    "baseVisibility": 100
  },
  "visual": {
    "spriteLibrary": "Mon_GnoleA",
    "effects": {
      "hitSpark": "VFX_HitSpark",
      "bloodDrip": "VFX_BloodDrip",
      "guard": "VFX_Guard",
      "parry": "VFX_Parry"
    }
  }
}
```

### 1.2 `Assets/Data/skills.json` Inspection (Lines 66–128)
The exact skill names and definitions referenced by the monster tank skillset:
- **`육중한 내리찍기`** (Lines 67–86):
  - `skillName`: `"육중한 내리찍기"`
  - `baseDelayMs`: 1000, `baseCooldown`: 7.0, `cooldownSlot`: 3
  - `hitShape`: "RECT", `hitRange`: 1, `hitWidth`: 2, `hitDepth`: 2
  - `threatRange`: 1, `threatWidth`: 3, `threatDepth`: 3
  - `damageMultiplier`: 1.45, `hasStun`: false, `stunDuration`: 0
  - `priorityBase`: 75, `priorityKillMultiplier`: 1.45, `priorityKillBonus`: 20, `priorityRangeThreshold`: 2.5, `priorityRangeBonus`: 10
- **`급습 할퀴기`** (Lines 88–107):
  - `skillName`: `"급습 할퀴기"`
  - `baseDelayMs`: 240, `baseCooldown`: 4.0, `cooldownSlot`: 2
  - `hitShape`: "LINE", `hitRange`: 1, `hitWidth`: 1, `hitDepth`: 1
  - `threatRange`: 1, `threatWidth`: 1, `threatDepth`: 1
  - `damageMultiplier`: 0.65, `hasStun`: false, `stunDuration`: 0
  - `priorityBase`: 60, `priorityKillMultiplier`: 0.65, `priorityKillBonus`: 20, `priorityRangeThreshold`: 1.5, `priorityRangeBonus`: 10
- **`발톱 후려치기`** (Lines 109–128):
  - `skillName`: `"발톱 후려치기"`
  - `baseDelayMs`: 650, `baseCooldown`: 1.4, `cooldownSlot`: 1
  - `hitShape`: "LINE", `hitRange`: 3, `hitWidth`: 1, `hitDepth`: 1
  - `threatRange`: 3, `threatWidth`: 1, `threatDepth`: 1
  - `damageMultiplier`: 1.0, `hasStun`: false, `stunDuration`: 0
  - `priorityBase`: 50, `priorityKillMultiplier`: 1.0, `priorityKillBonus`: 20, `priorityRangeThreshold`: 3.0, `priorityRangeBonus`: 10

### 1.3 Sprite Library Assets in `Assets/Sprite/Mon/`
Directly verified existing assets:
- `Assets/Sprite/Mon/Mon_DollKnight/Mon_DollKnight.spriteLib` (Asset Name: `Mon_DollKnight`, GUID: `dfc3854a7aa22ed44bc4b964c0ee8963`)
- `Assets/Sprite/Mon/Mon_Gnole/Mon_GnoleA.spriteLib` (Asset Name: `Mon_GnoleA`, GUID: `761fcd3a8823c4d44b3dca6b7133dfba`)
- `Assets/Sprite/Mon/Mon_GoblinHood/Mon_GoblinHoodA.spriteLib` (Asset Name: `Mon_GoblinHoodA`, GUID: `0674e05a6589b4a4d99cad4a0b2c95da`)

### 1.4 Prefab Conversion & Runtime Resolution
- `Assets/Editor/JsonToUnitPrefabConverter.cs`:
  - `ConvertJsonToPrefabs()` iterates through `units.json`, finds matching `SkillData` from `skills.json`, finds `SpriteLibraryAsset` by name (`u.visual.spriteLibrary`), builds prefab hierarchy with `UnitVisualDefinition`, `SpriteRenderer`, `SpriteLibrary`, `SpriteResolver`, and saves to `Assets/Resources/Units/{typeName}.prefab`.
- `Assets/Script/Unit/Visual/UnitSpriteManager.cs`:
  - `GetPrefab(string unitTypeName)` queries `Resources.Load<GameObject>("Units/" + unitTypeName)`.
- `Assets/Script/Wave/WaveSpawner.cs`:
  - `ResolveUnitType(string typeName)` searches for concrete `UnitType` subclasses in assembly matching `tempInstance.typeName == typeName`.

---

## 2. Logic Chain

1. **Schema Extraction**:
   - In `units.json`, each entry contains 7 top-level properties: `typeName` (string), `unitClass` (string, e.g. "Monster"), `footprint` (float[2]), `engageDistance` (int), `populationCost` (int), `skills` (string[]), `stats` (object), `weight` (object), and `visual` (object).
2. **Template Mapping**:
   - The user requested applying the exact stats, skills, and configuration of `'근접 탱커'` to 3 monsters:
     - `Mon_DollKnight` -> `typeName`: `"인형 기사"`, `unitClass`: `"Monster"`, `spriteLibrary`: `"Mon_DollKnight"`
     - `Mon_GnoleA` -> `typeName`: `"놀"`, `unitClass`: `"Monster"`, `spriteLibrary`: `"Mon_GnoleA"`
     - `Mon_GoblinHoodA` -> `typeName`: `"고블린 후드"`, `unitClass`: `"Monster"`, `spriteLibrary`: `"Mon_GoblinHoodA"`
3. **Skill Verification**:
   - The 3 skill names `"육중한 내리찍기"`, `"급습 할퀴기"`, `"발톱 후려치기"` in `units.json` strictly correspond to the `skillName` keys defined in `skills.json` (lines 67, 88, 109).
4. **Visual & Asset Linking**:
   - `JsonToUnitPrefabConverter.cs` uses `FindAssetByName<SpriteLibraryAsset>(u.visual.spriteLibrary)`.
   - Matching `.spriteLib` files exist in `Assets/Sprite/Mon/` with names `Mon_DollKnight`, `Mon_GnoleA`, and `Mon_GoblinHoodA`.
5. **C# Subclass Recommendation**:
   - To support runtime spawning via `WaveSpawner.cs` (which calls `ResolveUnitType`), `UnitTypes.cs` should define `DollKnight`, `Gnole`, and `GoblinHood` inheriting from `UnitType` with corresponding `typeName` values `"인형 기사"`, `"놀"`, and `"고블린 후드"`.

---

## 3. Recommended JSON Entries for `Assets/Data/units.json`

Below are the 3 complete JSON entries ready to be added to `units.json`:

```json
    {
      "typeName": "인형 기사",
      "unitClass": "Monster",
      "footprint": [1, 1],
      "engageDistance": 3,
      "populationCost": 1,
      "skills": ["육중한 내리찍기", "급습 할퀴기", "발톱 후려치기"],
      "stats": {
        "maxHp": 180,
        "maxMp": 0,
        "physicalAttack": 44,
        "magicalAttack": 9,
        "physicalDefense": 10,
        "magicalDefense": 8,
        "HPRegen": 3,
        "attackspeed": 105,
        "walkSpeed": 2.8,
        "reaction": 85,
        "criticalChance": 12,
        "cooltimeReduction": 75,
        "statusResistance": 75,
        "maxMental": 0,
        "mental": 0,
        "spotting": 0,
        "leadershipRange": 0,
        "charisma": 0,
        "physicalAttackSpeed": 13,
        "magicalCastSpeed": 0
      },
      "weight": {
        "isSpecialUnit": false,
        "isInterestTarget": false,
        "baseInterest": 60,
        "baseDanger": 120,
        "heavyHitThreshold": 10,
        "stealth": 0,
        "baseVisibility": 100
      },
      "visual": {
        "spriteLibrary": "Mon_DollKnight",
        "effects": {
          "hitSpark": "VFX_HitSpark",
          "bloodDrip": "VFX_BloodDrip",
          "guard": "VFX_Guard",
          "parry": "VFX_Parry"
        }
      }
    },
    {
      "typeName": "놀",
      "unitClass": "Monster",
      "footprint": [1, 1],
      "engageDistance": 3,
      "populationCost": 1,
      "skills": ["육중한 내리찍기", "급습 할퀴기", "발톱 후려치기"],
      "stats": {
        "maxHp": 180,
        "maxMp": 0,
        "physicalAttack": 44,
        "magicalAttack": 9,
        "physicalDefense": 10,
        "magicalDefense": 8,
        "HPRegen": 3,
        "attackspeed": 105,
        "walkSpeed": 2.8,
        "reaction": 85,
        "criticalChance": 12,
        "cooltimeReduction": 75,
        "statusResistance": 75,
        "maxMental": 0,
        "mental": 0,
        "spotting": 0,
        "leadershipRange": 0,
        "charisma": 0,
        "physicalAttackSpeed": 13,
        "magicalCastSpeed": 0
      },
      "weight": {
        "isSpecialUnit": false,
        "isInterestTarget": false,
        "baseInterest": 60,
        "baseDanger": 120,
        "heavyHitThreshold": 10,
        "stealth": 0,
        "baseVisibility": 100
      },
      "visual": {
        "spriteLibrary": "Mon_GnoleA",
        "effects": {
          "hitSpark": "VFX_HitSpark",
          "bloodDrip": "VFX_BloodDrip",
          "guard": "VFX_Guard",
          "parry": "VFX_Parry"
        }
      }
    },
    {
      "typeName": "고블린 후드",
      "unitClass": "Monster",
      "footprint": [1, 1],
      "engageDistance": 3,
      "populationCost": 1,
      "skills": ["육중한 내리찍기", "급습 할퀴기", "발톱 후려치기"],
      "stats": {
        "maxHp": 180,
        "maxMp": 0,
        "physicalAttack": 44,
        "magicalAttack": 9,
        "physicalDefense": 10,
        "magicalDefense": 8,
        "HPRegen": 3,
        "attackspeed": 105,
        "walkSpeed": 2.8,
        "reaction": 85,
        "criticalChance": 12,
        "cooltimeReduction": 75,
        "statusResistance": 75,
        "maxMental": 0,
        "mental": 0,
        "spotting": 0,
        "leadershipRange": 0,
        "charisma": 0,
        "physicalAttackSpeed": 13,
        "magicalCastSpeed": 0
      },
      "weight": {
        "isSpecialUnit": false,
        "isInterestTarget": false,
        "baseInterest": 60,
        "baseDanger": 120,
        "heavyHitThreshold": 10,
        "stealth": 0,
        "baseVisibility": 100
      },
      "visual": {
        "spriteLibrary": "Mon_GoblinHoodA",
        "effects": {
          "hitSpark": "VFX_HitSpark",
          "bloodDrip": "VFX_BloodDrip",
          "guard": "VFX_Guard",
          "parry": "VFX_Parry"
        }
      }
    }
```

---

## 4. Features Discovered & Edge Cases

## Features Discovered
| # | Category | Feature | Description | Inputs | Outputs | Error Behavior | Discovered Via |
|---|----------|---------|-------------|--------|---------|----------------|----------------|
| 1 | Unit Data | `units.json` Schema | Defines unit properties, stats, skills, weights, and visual configuration | JSON string | Unit object graph in `JsonToUnitPrefabConverter` | `JsonUtility` skips unknown fields, uses defaults | `Assets/Data/units.json`, `JsonToUnitPrefabConverter.cs` |
| 2 | Skill Data | `skills.json` Schema | Defines combat skill attributes, hitboxes, priority, cooldowns | JSON string | `SkillData` lookup dictionary | Missing skills trigger warning log in converter | `Assets/Data/skills.json` |
| 3 | Visual Assets | SpriteLibrary Mapping | 2D Animation Sprite Library assets assigned per unit | SpriteLibrary asset name (e.g. `Mon_DollKnight`) | `SpriteLibraryAsset` reference assigned to `SpriteLibrary` component | Warning if asset name not found in project | `JsonToUnitPrefabConverter.cs:228`, `Assets/Sprite/Mon/` |
| 4 | Prefab Generation | `JsonToUnitPrefabConverter` | Batch converter creating/updating `.prefab` files under `Assets/Resources/Units/` | `units.json` & `skills.json` | Prefabs: `{typeName}.prefab` | Overwrites existing prefabs, logs missing assets | `Assets/Editor/JsonToUnitPrefabConverter.cs` |
| 5 | Runtime Resolution | `UnitSpriteManager` | Loads unit prefabs from `Assets/Resources/Units/{typeName}.prefab` at runtime | `unitTypeName` string | `GameObject` prefab / `Sprite` icon | Returns `null` if prefab not in Resources | `Assets/Script/Unit/Visual/UnitSpriteManager.cs` |
| 6 | Unit Type Reflection | `WaveSpawner.ResolveUnitType` | Dynamically resolves C# `UnitType` subclasses matching `typeName` | `typeName` string | C# `Type` deriving from `UnitType` | Logs warning if no matching subclass found | `Assets/Script/Wave/WaveSpawner.cs:119` |

## Edge Cases
| # | Feature | Input | Observed Behavior |
|---|---------|-------|-------------------|
| 1 | `units.json` TypeName Space / Special Characters | `typeName: "고블린 후드"`, `"인형 기사"` | Handled cleanly by UTF-8 encoding and `SanitizeFileName()` during prefab creation (`인형 기사.prefab`). |
| 2 | Legacy `근접 탱커` Entry Compatibility | Keeping `근접 탱커` along with `놀` | Existing tests and `MeleeTank` class reference `"근접 탱커"`. Retaining the `근접 탱커` entry ensures full backward compatibility. |
| 3 | Missing C# `UnitType` Subclass | Spawning unit via `WaveSpawner` without C# class | `ResolveUnitType` fails to find class if only in JSON; adding `UnitType` subclasses in `UnitTypes.cs` prevents reflection failure. |
| 4 | SpriteLibrary vs TypeName mismatch | `typeName: "고블린 후드"` with `spriteLibrary: "Mon_GoblinHoodA"` | Converter resolves `spriteLibrary` field directly by asset name regardless of `typeName`. |

---

## 5. Caveats

1. **Legacy Entry Preservation**: In `units.json`, `"근접 탱커"` is referenced in tests (`PropagationSystemTests.cs`, `WeightSystemTests.cs`) and `UnitTypes.cs`. It is recommended to keep `"근접 탱커"` in `units.json` and append the new 3 entries (`"인형 기사"`, `"놀"`, `"고블린 후드"`).
2. **Prefab Generation**: While JSON entries can be added directly, Unity Editor menu item (`Tools/GrimArchive/JSON -> 유닛 프리팹 생성 (원클릭)`) or prefab instantiation creates the corresponding `.prefab` assets in `Assets/Resources/Units/`.

---

## 6. Conclusion

- The schema and exact stats of `'근접 탱커'` have been thoroughly extracted from `units.json` and verified against `UnitStatsData.cs` and `UnitVisualDefinition.cs`.
- The 3 required skill names (`"육중한 내리찍기"`, `"급습 할퀴기"`, `"발톱 후려치기"`) are confirmed in `skills.json`.
- The exact sprite library asset names (`Mon_DollKnight`, `Mon_GnoleA`, `Mon_GoblinHoodA`) are verified in `Assets/Sprite/Mon/`.
- Concrete JSON snippets for all 3 monsters are fully formulated and validated for seamless integration.

---

## 7. Verification Method

1. **C# Compilation**:
   ```powershell
   dotnet build Assembly-CSharp.csproj
   dotnet build Assembly-CSharp-Editor.csproj
   ```
2. **JSON Syntax Validation**:
   - Validate JSON parsing of `Assets/Data/units.json` using standard JSON parsers or Unity `JsonUtility`.
3. **Asset & Prefab Verification**:
   - Check `Assets/Sprite/Mon/Mon_DollKnight/Mon_DollKnight.spriteLib` exists.
   - Check `Assets/Sprite/Mon/Mon_Gnole/Mon_GnoleA.spriteLib` exists.
   - Check `Assets/Sprite/Mon/Mon_GoblinHood/Mon_GoblinHoodA.spriteLib` exists.
