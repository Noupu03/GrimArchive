# BRIEFING — 2026-08-24T20:36:20+09:00

## Mission
Investigate sprite assets, file names, extensions, meta files, directory structure, and SpriteLibrary resolution logic for Mon_DollKnight, Mon_GnoleA, and Mon_GoblinHoodA.

## 🔒 My Identity
- Archetype: explorer
- Roles: investigation, synthesis
- Working directory: c:/Users/wish0/Kubonhun/자료/Kubonhun/Project_file/Team_Dummy/GrimArchive_Prototype/.agents/explorer_sprites/
- Original parent: 8e4c2b58-3b19-47ce-b928-0cb3dddc6cc5
- Milestone: sprite_asset_investigation

## 🔒 Key Constraints
- Read-only investigation — do NOT implement
- Verify exact file names, extensions, meta GUIDs, directories, SpriteLibrary references in code/prefabs

## Current Parent
- Conversation ID: 8e4c2b58-3b19-47ce-b928-0cb3dddc6cc5
- Updated: 2026-08-24T20:36:20+09:00

## Investigation State
- **Explored paths**:
  - `Assets/Sprite/Mon/` (`Mon_DollKnight/`, `Mon_Gnole/`, `Mon_GoblinHood/`, `Mon_DollGothGirl/`, `Mon_Dullahan/`, `Mon_WoodDollGolem/`)
  - `Assets/Data/units.json`, `Assets/Data/skills.json`
  - `Assets/Resources/Units/` (`근접 탱커.prefab`, `야생 몬스터 A.prefab`, etc.)
  - `Assets/Editor/JsonToUnitPrefabConverter.cs`, `UnitShadowCasterSync.cs`, `UnitVisualEditor.cs`
  - `Assets/Script/Unit/Visual/UnitSpriteManager.cs`, `UnitGenerate.cs`
- **Key findings**:
  - `Mon_DollKnight.spriteLib` located at `Assets/Sprite/Mon/Mon_DollKnight/Mon_DollKnight.spriteLib` (GUID: `dfc3854a7aa22ed44bc4b964c0ee8963`).
  - `Mon_GnoleA.spriteLib` located at `Assets/Sprite/Mon/Mon_Gnole/Mon_GnoleA.spriteLib` (GUID: `761fcd3a8823c4d44b3dca6b7133dfba`). Note folder is `Mon_Gnole`, file is `Mon_GnoleA.spriteLib`.
  - `Mon_GoblinHoodA.spriteLib` located at `Assets/Sprite/Mon/Mon_GoblinHood/Mon_GoblinHoodA.spriteLib` (GUID: `0674e05a6589b4a4d99cad4a0b2c95da`). Note folder is `Mon_GoblinHood`, file is `Mon_GoblinHoodA.spriteLib`.
  - All 3 `.spriteLib` files contain Category `A` with entries `Down`, `DownLeft`, `Left`, `UpLeft`, `Up`.
  - `JsonToUnitPrefabConverter` uses `FindAssetByName<SpriteLibraryAsset>(u.visual.spriteLibrary)` to link `.spriteLib` to the prefab's `Visual` GameObject `SpriteLibrary` component.
  - `UnitSpriteManager.GetPrefab(typeName)` retrieves `Assets/Resources/Units/{typeName}.prefab`.
  - Monster units have no `animatorController` in `units.json` and don't need Animator component.
- **Unexplored areas**: None (investigation complete).

## Key Decisions Made
- All asset paths, GUIDs, and resolution mechanisms documented in `handoff.md`.

## Artifact Index
- `handoff.md` — Complete sprite assets and architecture investigation report.
