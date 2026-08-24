# E2E Test Infra: GrimArchive Monster Registration & Prefabs

## Test Philosophy
- Opaque-box, requirement-driven verification of unit data, sprite asset resolution, prefab structure, and compilation integrity.
- Methodology: Category-Partition + BVA + Resource Validation + Build Pipeline Testing.

## Feature Inventory
| # | Feature | Source (requirement) | Tier 1 (Data/Syntax) | Tier 2 (Asset Binding) | Tier 3 (Cross-Feature) | Tier 4 (Build/Runtime) |
|---|---------|---------------------|:--------------------:|:----------------------:|:----------------------:|:----------------------:|
| 1 | `Mon_DollKnight` (인형 기사) | ORIGINAL_REQUEST §R1, R2 | JSON syntax, field completeness | SpriteLib GUID, VFX bindings | Skill list, stats coherence | `dotnet build`, Prefab Load |
| 2 | `Mon_GnoleA` (놀) | ORIGINAL_REQUEST §R1, R2 | JSON syntax, field completeness | SpriteLib GUID, VFX bindings | Skill list, stats coherence | `dotnet build`, Prefab Load |
| 3 | `Mon_GoblinHoodA` (고블린 후드) | ORIGINAL_REQUEST §R1, R2 | JSON syntax, field completeness | SpriteLib GUID, VFX bindings | Skill list, stats coherence | `dotnet build`, Prefab Load |

## Test Architecture
- Test runner: Custom test validation script / `dotnet build Assembly-CSharp-Editor.csproj` / JSON schema verification / Prefab YAML parser.
- Location: E2E test verification outputs and reports under `.agents/`.
- Expected: 0 build errors, 100% valid JSON, all 3 monster prefabs exist and link to proper `.spriteLib` GUIDs.
