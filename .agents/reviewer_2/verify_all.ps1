# Reviewer 2 Independent Verification Script
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "=== 1. Validating units.json ==="
$jsonText = [System.IO.File]::ReadAllText("Assets/Data/units.json", [System.Text.Encoding]::UTF8)
$json = $jsonText | ConvertFrom-Json
$unitMap = @{}
foreach ($u in $json.units) {
    $unitMap[$u.typeName] = $u
}

$legacyTankName = [System.Text.Encoding]::UTF8.GetString(@(0xEA, 0xB7, 0xBC, 0xEC, 0xA0, 0x91, 0x20, 0xED, 0x83, 0xB1, 0xEC, 0xBB, 0xA4)) # 근접 탱커
$dollKnightName = [System.Text.Encoding]::UTF8.GetString(@(0xEC, 0x9D, 0xB8, 0xED, 0x98, 0x95, 0x20, 0xEA, 0xB8, 0xB0, 0xEC, 0x82, 0xAC)) # 인형 기사
$gnoleName      = [System.Text.Encoding]::UTF8.GetString(@(0xEB, 0x86, 0x80)) # 놀
$goblinHoodName = [System.Text.Encoding]::UTF8.GetString(@(0xEA, 0xB3, 0xA0, 0xEB, 0xB8, 0x94, 0xEB, 0xA6, 0xB0, 0x20, 0xED, 0x9B, 0x84, 0xEB, 0x93, 0x9C)) # 고블린 후드

# Also verify legacy MeleeTank
if (-not $unitMap.ContainsKey($legacyTankName)) {
    throw "units.json missing backward compatible entry: '$legacyTankName'"
}
Write-Host " [PASS] units.json contains legacy '$legacyTankName'"

$requiredMonsters = @{
    $dollKnightName = "Mon_DollKnight"
    $gnoleName      = "Mon_GnoleA"
    $goblinHoodName = "Mon_GoblinHoodA"
}

foreach ($m in $requiredMonsters.Keys) {
    if (-not $unitMap.ContainsKey($m)) {
        throw "units.json missing entry: '$m'"
    }
    $u = $unitMap[$m]
    if ($u.unitClass -ne "Monster") {
        throw "Unit '$m' unitClass is '$($u.unitClass)' (expected 'Monster')"
    }
    $expectedSpriteLib = $requiredMonsters[$m]
    if ($u.visual.spriteLibrary -ne $expectedSpriteLib) {
        throw "Unit '$m' spriteLibrary is '$($u.visual.spriteLibrary)' (expected '$expectedSpriteLib')"
    }
    if ($u.skills.Count -ne 3) {
        throw "Unit '$m' skills count is $($u.skills.Count) (expected 3)"
    }
    if ($u.stats.maxHp -ne 180 -or $u.stats.physicalAttack -ne 44 -or $u.stats.physicalDefense -ne 10) {
        throw "Unit '$m' stats do not match melee tank stats"
    }
    if ($u.weight.baseDanger -ne 120 -or $u.weight.baseInterest -ne 60) {
        throw "Unit '$m' weight values do not match melee tank weights"
    }
    if ($u.visual.effects.hitSpark -ne "VFX_HitSpark" -or $u.visual.effects.bloodDrip -ne "VFX_BloodDrip" -or $u.visual.effects.guard -ne "VFX_Guard" -or $u.visual.effects.parry -ne "VFX_Parry") {
        throw "Unit '$m' visual effects mismatch"
    }
    Write-Host " [PASS] units.json entry '$m' verified with all stats, skills, weights, effects, and spriteLibrary='$expectedSpriteLib'"
}

Write-Host "`n=== 2. Validating UnitTypes.cs ==="
$unitTypesCode = [System.IO.File]::ReadAllText("Assets/Script/Unit/Core/UnitTypes.cs", [System.Text.Encoding]::UTF8)

$classesToCheck = @(
    @{ ClassName = "DollKnight"; TypeName = $dollKnightName },
    @{ ClassName = "GnoleA"; TypeName = $gnoleName },
    @{ ClassName = "GoblinHoodA"; TypeName = $goblinHoodName },
    @{ ClassName = "MeleeTank"; TypeName = $legacyTankName }
)

foreach ($c in $classesToCheck) {
    $pattern = "public\s+class\s+" + $c.ClassName + "\s*:\s*UnitType"
    if (-not ($unitTypesCode -match $pattern)) {
        throw "UnitTypes.cs does not declare 'public class $($c.ClassName) : UnitType'"
    }
    if (-not ($unitTypesCode -match ($c.ClassName + "\(\)\s*\{\s*typeName\s*=\s*`"" + [regex]::Escape($c.TypeName) + "`""))) {
        throw "UnitTypes.cs does not set typeName = '$($c.TypeName)' in $($c.ClassName) constructor"
    }
    Write-Host " [PASS] UnitTypes.cs declares class $($c.ClassName) with typeName='$($c.TypeName)'"
}

Write-Host "`n=== 3. Validating SpriteLibrary Assets and GUIDs ==="
$spriteLibChecks = @(
    @{ Path = "Assets/Sprite/Mon/Mon_DollKnight/Mon_DollKnight.spriteLib"; Meta = "Assets/Sprite/Mon/Mon_DollKnight/Mon_DollKnight.spriteLib.meta"; ExpectedGuid = "dfc3854a7aa22ed44bc4b964c0ee8963" },
    @{ Path = "Assets/Sprite/Mon/Mon_Gnole/Mon_GnoleA.spriteLib"; Meta = "Assets/Sprite/Mon/Mon_Gnole/Mon_GnoleA.spriteLib.meta"; ExpectedGuid = "761fcd3a8823c4d44b3dca6b7133dfba" },
    @{ Path = "Assets/Sprite/Mon/Mon_GoblinHood/Mon_GoblinHoodA.spriteLib"; Meta = "Assets/Sprite/Mon/Mon_GoblinHood/Mon_GoblinHoodA.spriteLib.meta"; ExpectedGuid = "0674e05a6589b4a4d99cad4a0b2c95da" }
)

foreach ($sl in $spriteLibChecks) {
    if (-not (Test-Path $sl.Path)) { throw "Missing sprite library asset: $($sl.Path)" }
    if (-not (Test-Path $sl.Meta)) { throw "Missing sprite library meta: $($sl.Meta)" }
    $metaContent = [System.IO.File]::ReadAllText($sl.Meta, [System.Text.Encoding]::UTF8)
    if (-not ($metaContent -match ("guid:\s*" + $sl.ExpectedGuid))) {
        throw "Sprite library meta $($sl.Meta) GUID mismatch: expected $($sl.ExpectedGuid)"
    }
    Write-Host " [PASS] Sprite library '$($sl.Path)' verified with GUID $($sl.ExpectedGuid)"
}

Write-Host "`n=== 4. Validating Prefabs and Components ==="
$prefabChecks = @(
    @{ Name = $dollKnightName; Path = "Assets/Resources/Units/$dollKnightName.prefab"; SpriteLibGuid = "dfc3854a7aa22ed44bc4b964c0ee8963" },
    @{ Name = $gnoleName; Path = "Assets/Resources/Units/$gnoleName.prefab"; SpriteLibGuid = "761fcd3a8823c4d44b3dca6b7133dfba" },
    @{ Name = $goblinHoodName; Path = "Assets/Resources/Units/$goblinHoodName.prefab"; SpriteLibGuid = "0674e05a6589b4a4d99cad4a0b2c95da" }
)

foreach ($chk in $prefabChecks) {
    if (-not (Test-Path $chk.Path)) { throw "Missing prefab file: $($chk.Path)" }
    if (-not (Test-Path "$($chk.Path).meta")) { throw "Missing prefab meta file: $($chk.Path).meta" }
    $content = [System.IO.File]::ReadAllText($chk.Path, [System.Text.Encoding]::UTF8)
    
    if (-not $content.Contains($chk.SpriteLibGuid)) {
        throw "Prefab $($chk.Path) does not reference SpriteLibrary GUID $($chk.SpriteLibGuid)"
    }
    if (-not $content.Contains("Assembly-CSharp::UnitVisualDefinition")) {
        throw "Prefab $($chk.Path) does not contain UnitVisualDefinition component"
    }
    if (-not $content.Contains("Unity.2D.Animation.Runtime::UnityEngine.U2D.Animation.SpriteLibrary")) {
        throw "Prefab $($chk.Path) does not contain SpriteLibrary component"
    }
    if (-not $content.Contains("Unity.2D.Animation.Runtime::UnityEngine.U2D.Animation.SpriteResolver")) {
        throw "Prefab $($chk.Path) does not contain SpriteResolver component"
    }
    if (-not $content.Contains("Unity.RenderPipelines.Universal.2D.Runtime::UnityEngine.Rendering.Universal.ShadowCaster2D")) {
        throw "Prefab $($chk.Path) does not contain ShadowCaster2D component"
    }
    Write-Host " [PASS] Prefab '$($chk.Name)' verified (UnitVisualDefinition, SpriteLibrary, SpriteResolver, ShadowCaster2D, GUID $($chk.SpriteLibGuid))"
}

Write-Host "`n=== 5. Validating Integration Tests ==="
if (-not (Test-Path "Assets/Tests/MonsterTypeIntegrationTests.cs")) {
    throw "Missing integration test file: Assets/Tests/MonsterTypeIntegrationTests.cs"
}
if (-not (Test-Path "Assets/Tests/MonsterTypeIntegrationTests.cs.meta")) {
    throw "Missing integration test meta file: Assets/Tests/MonsterTypeIntegrationTests.cs.meta"
}
$testCode = [System.IO.File]::ReadAllText("Assets/Tests/MonsterTypeIntegrationTests.cs", [System.Text.Encoding]::UTF8)
if (-not ($testCode.Contains("DollKnight") -and $testCode.Contains("GnoleA") -and $testCode.Contains("GoblinHoodA"))) {
    throw "MonsterTypeIntegrationTests.cs missing test coverage for target monsters"
}
Write-Host " [PASS] MonsterTypeIntegrationTests.cs verified"

Write-Host "`n=== ALL VERIFICATION CHECKS PASSED SUCCESSFULLY ==="
