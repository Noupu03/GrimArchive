# Handoff Report: Monster Sprite Assets & SpriteLibrary Resolution Investigation

## 1. Observation

### 1.1 Target 3 Monster Sprite Assets
Direct inspection of `Assets/Sprite/Mon/` revealed the following files and directory structures for `Mon_DollKnight`, `Mon_GnoleA`, and `Mon_GoblinHoodA`:

1. **Mon_DollKnight (인형 기사)**:
   - **Directory**: `Assets/Sprite/Mon/Mon_DollKnight/`
   - **Texture File**: `Assets/Sprite/Mon/Mon_DollKnight/Mon_DollKnight.png`
     - Texture Meta GUID: `00f66674dfda7ee40b51c2394d4dd94d`
     - Sliced Sprites: `Mon_DollKnight_0`, `Mon_DollKnight_1`, `Mon_DollKnight_2`, `Mon_DollKnight_3`, `Mon_DollKnight_4` (32x32, pivot `{x: 0.5, y: 0}`)
   - **SpriteLibrary File**: `Assets/Sprite/Mon/Mon_DollKnight/Mon_DollKnight.spriteLib`
     - Meta File GUID: `dfc3854a7aa22ed44bc4b964c0ee8963`
     - Importer Script GUID: `db2778f6d440c47ddacff25997d7c062` (Unity 2D Animation SpriteLibrarySourceAsset)
     - Internal Category: `A` (Hash: `333029003`)
     - Entries:
       - `Down`: `{fileID: -956343663, guid: 00f66674dfda7ee40b51c2394d4dd94d, type: 3}`
       - `DownLeft`: `{fileID: 1111767792, guid: 00f66674dfda7ee40b51c2394d4dd94d, type: 3}`
       - `Left`: `{fileID: 478481280, guid: 00f66674dfda7ee40b51c2394d4dd94d, type: 3}`
       - `UpLeft`: `{fileID: -542226383, guid: 00f66674dfda7ee40b51c2394d4dd94d, type: 3}`
       - `Up`: `{fileID: -1342971888, guid: 00f66674dfda7ee40b51c2394d4dd94d, type: 3}`
   - **Exact Asset Name**: `Mon_DollKnight`

2. **Mon_GnoleA (놀)**:
   - **Directory**: `Assets/Sprite/Mon/Mon_Gnole/` (Note directory name is `Mon_Gnole`)
   - **Texture File**: `Assets/Sprite/Mon/Mon_Gnole/Mon_GnoleA.png`
     - Texture Meta GUID: `e0311bba70510e94f956a163cc3c3ac4`
   - **SpriteLibrary File**: `Assets/Sprite/Mon/Mon_Gnole/Mon_GnoleA.spriteLib`
     - Meta File GUID: `761fcd3a8823c4d44b3dca6b7133dfba`
     - Importer Script GUID: `db2778f6d440c47ddacff25997d7c062`
     - Internal Category: `A` (Hash: `333029003`)
     - Entries: `Down`, `DownLeft`, `Left`, `UpLeft`, `Up` referencing `Mon_GnoleA.png` slices
   - **Exact Asset Name**: `Mon_GnoleA`

3. **Mon_GoblinHoodA (고블린 후드)**:
   - **Directory**: `Assets/Sprite/Mon/Mon_GoblinHood/` (Note directory name is `Mon_GoblinHood`)
   - **Texture File**: `Assets/Sprite/Mon/Mon_GoblinHood/Mon_GoblinHoodA.png`
     - Texture Meta GUID: `f4c916e3f24fd434d8bb2608edf7c395`
   - **SpriteLibrary File**: `Assets/Sprite/Mon/Mon_GoblinHood/Mon_GoblinHoodA.spriteLib`
     - Meta File GUID: `0674e05a6589b4a4d99cad4a0b2c95da`
     - Importer Script GUID: `db2778f6d440c47ddacff25997d7c062`
     - Internal Category: `A` (Hash: `333029003`)
     - Entries: `Down`, `DownLeft`, `Left`, `UpLeft`, `Up` referencing `Mon_GoblinHoodA.png` slices
   - **Exact Asset Name**: `Mon_GoblinHoodA`

---

### 1.2 Other Monster Sprite Libraries in `Assets/Sprite/Mon/`
- `Assets/Sprite/Mon/Mon_DollGothGirl/Mon_DollGothGirl.spriteLib` (GUID: `56445808682c3204486bf723aa7ae578`)
- `Assets/Sprite/Mon/Mon_Dullahan/Mon_Dullahan.spriteLib` (GUID: `8d14fffae2144cd48adb793d2a19103d`)
- `Assets/Sprite/Mon/Mon_WoodDollGolem/Mon_WoodDollGolem.spriteLib` (GUID: `7728bd5bf2d1a1c4f820ba451a3e20df`)
- `Assets/Sprite/Mon/Mon_WoodDollGolem/GolemHand.spriteLib` (GUID: `2e7a1772ca3ee464aa22f719363570fc`)

---

### 1.3 SpriteLibrary Resolution Mechanism in Code & Prefabs

1. **JSON Definition (`Assets/Data/units.json`)**:
   - `visual.spriteLibrary` is set to the asset name as a string (e.g. `"Mon_DollKnight"`, `"Mon_GnoleA"`, `"Mon_GoblinHoodA"`).
   - Monster units do **not** define `"animatorController"` or `"weapon"`.

2. **Prefab Generation (`Assets/Editor/JsonToUnitPrefabConverter.cs`)**:
   - Lines 226-232:
     ```csharp
     if (!string.IsNullOrEmpty(u.visual?.spriteLibrary))
     {
         var lib = visual.AddComponent<SpriteLibrary>();
         lib.spriteLibraryAsset = FindAssetByName<SpriteLibraryAsset>(u.visual.spriteLibrary);
         visual.AddComponent<SpriteResolver>();
     }
     ```
   - Lines 275-296 (`FindAssetByName<T>`):
     Searches `AssetDatabase.FindAssets("t:SpriteLibraryAsset " + name)`. Matches `obj.name == name` or `main.name == name`.
   - Lines 257-259:
     Saves prefab to `Assets/Resources/Units/{u.typeName}.prefab`.

3. **Runtime Loading & Direction Handling (`UnitSpriteManager.cs` & `UnitGenerate.cs`)**:
   - `UnitSpriteManager.GetPrefab(unitTypeName)` loads `Resources.Load<GameObject>($"Units/{unitTypeName}")`.
   - `UnitGenerate.SetupUnitVisual(Unit unit, float visualScale)` (lines 205-211):
     - `spriteLib = go.GetComponentInChildren<SpriteLibrary>()`
     - `unit.spriteVariation = _unitSpriteManager.PickRandomVariation(spriteLib.spriteLibraryAsset)` -> returns category `"A"`.
     - `UpdateSpriteResolver(cache.SpriteResolver, unit.currentDir, unit.spriteVariation)` -> maps `Dir` to label (`"Down"`, `"DownLeft"`, `"Left"`, `"UpLeft"`, `"Up"`) and flips X when facing right.

4. **ShadowCaster2D Consistency (`Assets/Editor/UnitShadowCasterSync.cs`)**:
   - All unit prefabs in `Assets/Resources/Units/` have `ShadowCaster2D` attached to the `Visual` child GameObject.

---

### 1.4 Reference Monster Definition: '근접 탱커'
From `Assets/Data/units.json` (lines 56-103):
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

---

## 2. Logic Chain

1. **Asset Name Matching**:
   - For `Mon_DollKnight`: The asset file is `Assets/Sprite/Mon/Mon_DollKnight/Mon_DollKnight.spriteLib`. Unity recognizes this SpriteLibraryAsset with name `"Mon_DollKnight"`. Thus in `units.json`, `"spriteLibrary": "Mon_DollKnight"` will be resolved successfully.
   - For `Mon_GnoleA`: The asset file is `Assets/Sprite/Mon/Mon_Gnole/Mon_GnoleA.spriteLib`. Unity recognizes this SpriteLibraryAsset with name `"Mon_GnoleA"`. Thus in `units.json`, `"spriteLibrary": "Mon_GnoleA"` will be resolved successfully.
   - For `Mon_GoblinHoodA`: The asset file is `Assets/Sprite/Mon/Mon_GoblinHood/Mon_GoblinHoodA.spriteLib`. Unity recognizes this SpriteLibraryAsset with name `"Mon_GoblinHoodA"`. Thus in `units.json`, `"spriteLibrary": "Mon_GoblinHoodA"` will be resolved successfully.

2. **Prefab Generation**:
   - `JsonToUnitPrefabConverter` iterates over `units.json` and creates `Assets/Resources/Units/{typeName}.prefab`.
   - When given `typeName: "인형 기사"`, `typeName: "놀"`, `typeName: "고블린 후드"`, it will create:
     - `Assets/Resources/Units/인형 기사.prefab` (linked to `Mon_DollKnight.spriteLib` GUID `dfc3854a7aa22ed44bc4b964c0ee8963`)
     - `Assets/Resources/Units/놀.prefab` (linked to `Mon_GnoleA.spriteLib` GUID `761fcd3a8823c4d44b3dca6b7133dfba`)
     - `Assets/Resources/Units/고블린 후드.prefab` (linked to `Mon_GoblinHoodA.spriteLib` GUID `0674e05a6589b4a4d99cad4a0b2c95da`)

3. **Skills & Stats**:
   - The 3 skills (`"육중한 내리찍기"`, `"급습 할퀴기"`, `"발톱 후려치기"`) are fully defined in `Assets/Data/skills.json` (lines 67-128) and match `JsonToUnitPrefabConverter`'s skill resolution.

4. **Runtime Resolution**:
   - `UnitSpriteManager.GetPrefab(typeName)` retrieves `Resources.Load<GameObject>($"Units/{typeName}")`. Since `typeName` matches the prefab file name in `Assets/Resources/Units/`, it loads seamlessly without needing extra registries.

---

## 3. Caveats

1. **Folder Name vs Asset Name**:
   - `Mon_GnoleA` is in folder `Assets/Sprite/Mon/Mon_Gnole/`, but the `.spriteLib` and `.png` files are named `Mon_GnoleA`.
   - `Mon_GoblinHoodA` is in folder `Assets/Sprite/Mon/Mon_GoblinHood/`, but the `.spriteLib` and `.png` files are named `Mon_GoblinHoodA`.
   - The JSON `spriteLibrary` field matches the **asset file name** (`Mon_GnoleA`, `Mon_GoblinHoodA`), not the folder name.
2. **Existing '근접 탱커' entry**:
   - The existing entry `"근접 탱커"` in `units.json` uses `spriteLibrary: "Mon_GnoleA"`. According to `ORIGINAL_REQUEST.md`, `"근접 탱커"` can be retained for compatibility while adding `"놀"`.
3. **ShadowCaster2D on Prefabs**:
   - After generating prefabs via `JsonToUnitPrefabConverter`, `UnitShadowCasterSync.SyncAllUnitPrefabs()` should be run (or prefabs should ensure `ShadowCaster2D` is attached to `Visual`) to maintain consistency across all unit prefabs.

---

## 4. Conclusion

All 3 target monster sprite library assets and textures exist in `Assets/Sprite/Mon/`, are properly formatted as Unity `.spriteLib` assets with standard 5-directional entries (`Down`, `DownLeft`, `Left`, `UpLeft`, `Up`) under Category `A`, and are 100% compatible with `JsonToUnitPrefabConverter` and runtime systems (`UnitSpriteManager`, `UnitGenerate`).

### Proposed `units.json` entries:
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

## 5. Verification Method

1. **Verify C# Build Status**:
   - `dotnet build Assembly-CSharp.csproj`
   - `dotnet build Assembly-CSharp-Editor.csproj`
   - Both build cleanly with 0 errors and 0 warnings.
2. **Verify Asset Files & GUIDs**:
   - Check presence of `Assets/Sprite/Mon/Mon_DollKnight/Mon_DollKnight.spriteLib` (GUID: `dfc3854a7aa22ed44bc4b964c0ee8963`)
   - Check presence of `Assets/Sprite/Mon/Mon_Gnole/Mon_GnoleA.spriteLib` (GUID: `761fcd3a8823c4d44b3dca6b7133dfba`)
   - Check presence of `Assets/Sprite/Mon/Mon_GoblinHood/Mon_GoblinHoodA.spriteLib` (GUID: `0674e05a6589b4a4d99cad4a0b2c95da`)
3. **Verify Skill References**:
   - Confirm skills `"육중한 내리찍기"`, `"급습 할퀴기"`, `"발톱 후려치기"` in `Assets/Data/skills.json`.
