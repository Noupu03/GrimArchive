## 2026-08-24T11:37:18Z

You are a teamwork_preview_worker.
Your working directory is: c:/Users/wish0/Kubonhun/자료/Kubonhun/Project_file/Team_Dummy/GrimArchive_Prototype/.agents/worker_m1/
Original User Request Path: c:/Users/wish0/Kubonhun/자료/Kubonhun/Project_file/Team_Dummy/GrimArchive_Prototype/.agents/ORIGINAL_REQUEST.md
Project Scope Path: c:/Users/wish0/Kubonhun/자료/Kubonhun/Project_file/Team_Dummy/GrimArchive_Prototype/PROJECT.md

Survey Handoff Reports for Reference:
- Spec Miner: c:/Users/wish0/Kubonhun/자료/Kubonhun/Project_file/Team_Dummy/GrimArchive_Prototype/.agents/spec_miner_survey/handoff.md
- Sprite Explorer: c:/Users/wish0/Kubonhun/자료/Kubonhun/Project_file/Team_Dummy/GrimArchive_Prototype/.agents/explorer_sprites/handoff.md
- Converter Explorer: c:/Users/wish0/Kubonhun/자료/Kubonhun/Project_file/Team_Dummy/GrimArchive_Prototype/.agents/explorer_converter/handoff.md

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Tasks to Implement:
1. Update `Assets/Data/units.json`:
   - Add the 3 monster definitions:
     - `인형 기사`: `typeName: "인형 기사"`, `unitClass: "Monster"`, `spriteLibrary: "Mon_DollKnight"`, melee tank stats & skills (`["육중한 내리찍기", "급습 할퀴기", "발톱 후려치기"]`), effects (`hitSpark: "VFX_HitSpark"`, `bloodDrip: "VFX_BloodDrip"`, `guard: "VFX_Guard"`, `parry: "VFX_Parry"`).
     - `놀`: `typeName: "놀"`, `unitClass: "Monster"`, `spriteLibrary: "Mon_GnoleA"`, melee tank stats & skills, effects. (Preserve the existing "근접 탱커" entry for backward compatibility).
     - `고블린 후드`: `typeName: "고블린 후드"`, `unitClass: "Monster"`, `spriteLibrary: "Mon_GoblinHoodA"`, melee tank stats & skills, effects.
2. Update `Assets/Script/Unit/Core/UnitTypes.cs`:
   - Add `DollKnight`, `GnoleA`, and `GoblinHoodA` subclasses deriving from `UnitType` with matching `typeName` strings and `footprint = new Vector2(1, 1)` to support runtime reflection in `WaveSpawner.cs`.
3. Create Prefab Files in `Assets/Resources/Units/`:
   - `Assets/Resources/Units/인형 기사.prefab` (and `.meta`)
   - `Assets/Resources/Units/놀.prefab` (and `.meta`)
   - `Assets/Resources/Units/고블린 후드.prefab` (and `.meta`)
   - Ensure the YAML structure matches existing monster prefabs (e.g. `근접 탱커.prefab` / `야생 몬스터 A.prefab`):
     - Root GameObject with `UnitVisualDefinition` populated with melee tank stats, skill references, weight, engageDistance, footprint, populationCost.
     - Child `Visual` GameObject with `SpriteRenderer`, `SpriteLibrary` (linking to the correct `.spriteLib` GUID: `dfc3854a7aa22ed44bc4b964c0ee8963` for DollKnight, `761fcd3a8823c4d44b3dca6b7133dfba` for GnoleA, `0674e05a6589b4a4d99cad4a0b2c95da` for GoblinHoodA), `SpriteResolver`, and `ShadowCaster2D`.
4. Verification:
   - Run `dotnet build Assembly-CSharp-Editor.csproj` and confirm 0 errors and 0 warnings.
   - Verify JSON format in `Assets/Data/units.json`.
   - Verify files exist and are valid.
5. Documentation:
   - Write your complete handoff report to `c:/Users/wish0/Kubonhun/자료/Kubonhun/Project_file/Team_Dummy/GrimArchive_Prototype/.agents/worker_m1/handoff.md`.
6. Send a message to parent when done.
