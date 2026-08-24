[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "=== TEST 1: units.json Verification ==="
$jsonPath = "Assets/Data/units.json"
if (-not (Test-Path $jsonPath)) { throw "units.json not found" }

$rawJson = Get-Content -Raw -Encoding UTF8 $jsonPath
$db = $rawJson | ConvertFrom-Json
Write-Host "Successfully parsed units.json. Total units: $($db.units.Count)"

$targetMonsters = @("인형 기사", "놀", "고블린 후드")
$expectedSpriteLibs = @{
    "인형 기사" = "Mon_DollKnight"
    "놀" = "Mon_GnoleA"
    "고블린 후드" = "Mon_GoblinHoodA"
}

$tank = $db.units | Where-Object { $_.typeName -eq "근접 탱커" }
if (-not $tank) { throw "근접 탱커 not found in units.json" }
Write-Host "[PASS] 근접 탱커 is preserved."

foreach ($monName in $targetMonsters) {
    $mon = $db.units | Where-Object { $_.typeName -eq $monName }
    if (-not $mon) { throw "Monster $monName not found in units.json" }
    
    if ($mon.unitClass -ne "Monster") { throw "Monster $monName unitClass is not Monster (got $($mon.unitClass))" }
    if ($mon.visual.spriteLibrary -ne $expectedSpriteLibs[$monName]) { 
        throw "Monster $monName spriteLibrary mismatch: expected $($expectedSpriteLibs[$monName]), got $($mon.visual.spriteLibrary)" 
    }
    
    if ($mon.skills.Count -ne 3 -or $mon.skills[0] -ne "육중한 내리찍기" -or $mon.skills[1] -ne "급습 할퀴기" -or $mon.skills[2] -ne "발톱 후려치기") {
        throw "Monster $monName skills mismatch: $($mon.skills -join ', ')"
    }
    
    # Check stats match melee tank
    if ($mon.stats.maxHp -ne $tank.stats.maxHp -or $mon.stats.physicalAttack -ne $tank.stats.physicalAttack) {
        throw "Monster $monName stats mismatch against MeleeTank"
    }
    
    Write-Host "[PASS] Monster $monName in units.json is valid (unitClass=Monster, spriteLib=$($mon.visual.spriteLibrary), skills=3, stats match MeleeTank)."
}

Write-Host "`n=== TEST 2: Prefab & Asset Linkages ==="
$prefabChecks = @(
    @{ Name="인형 기사"; SpriteLibPath="Assets/Sprite/Mon/Mon_DollKnight/Mon_DollKnight.spriteLib"; PngPath="Assets/Sprite/Mon/Mon_DollKnight/Mon_DollKnight.png" },
    @{ Name="놀"; SpriteLibPath="Assets/Sprite/Mon/Mon_Gnole/Mon_GnoleA.spriteLib"; PngPath="Assets/Sprite/Mon/Mon_Gnole/Mon_GnoleA.png" },
    @{ Name="고블린 후드"; SpriteLibPath="Assets/Sprite/Mon/Mon_GoblinHood/Mon_GoblinHoodA.spriteLib"; PngPath="Assets/Sprite/Mon/Mon_GoblinHood/Mon_GoblinHoodA.png" }
)

foreach ($item in $prefabChecks) {
    $prefabPath = "Assets/Resources/Units/$($item.Name).prefab"
    $metaPath = "$prefabPath.meta"
    if (-not (Test-Path $prefabPath)) { throw "Prefab $prefabPath missing" }
    if (-not (Test-Path $metaPath)) { throw "Prefab meta $metaPath missing" }
    
    $spriteLibMeta = Get-Content -Raw -Encoding UTF8 "$($item.SpriteLibPath).meta"
    if ($spriteLibMeta -notmatch 'guid:\s*([0-9a-fA-F]+)') { throw "Could not extract GUID from $($item.SpriteLibPath).meta" }
    $expectedSpriteLibGuid = $matches[1]
    
    $pngMeta = Get-Content -Raw -Encoding UTF8 "$($item.PngPath).meta"
    if ($pngMeta -notmatch 'guid:\s*([0-9a-fA-F]+)') { throw "Could not extract GUID from $($item.PngPath).meta" }
    $expectedPngGuid = $matches[1]
    
    $prefabContent = Get-Content -Raw -Encoding UTF8 $prefabPath
    if (-not $prefabContent.Contains($expectedSpriteLibGuid)) {
        throw "Prefab $($item.Name) does NOT contain expected SpriteLibrary GUID $expectedSpriteLibGuid"
    }
    if (-not $prefabContent.Contains($expectedPngGuid)) {
        throw "Prefab $($item.Name) does NOT contain expected Texture GUID $expectedPngGuid"
    }
    if (-not $prefabContent.Contains("UnitVisualDefinition")) {
        throw "Prefab $($item.Name) missing UnitVisualDefinition component"
    }
    if (-not $prefabContent.Contains("ShadowCaster2D")) {
        throw "Prefab $($item.Name) missing ShadowCaster2D component"
    }
    Write-Host "[PASS] Prefab for $($item.Name) correctly references spriteLib GUID ($expectedSpriteLibGuid) and png GUID ($expectedPngGuid), with UnitVisualDefinition and ShadowCaster2D."
}

Write-Host "`n=== TEST 3: C# Assembly & Reflection Verification ==="
$asmPath = "Temp/bin/Debug/Assembly-CSharp.dll"
$asm = [System.Reflection.Assembly]::LoadFrom((Resolve-Path $asmPath))

$typeMap = @{
    "인형 기사" = "DollKnight"
    "놀" = "GnoleA"
    "고블린 후드" = "GoblinHoodA"
    "근접 탱커" = "MeleeTank"
}

$allUnitSubtypes = $asm.GetTypes() | Where-Object { $_.BaseType.Name -eq "UnitType" -and -not $_.IsAbstract }

foreach ($entry in $typeMap.GetEnumerator()) {
    $targetTypeName = $entry.Key
    $expectedClassName = $entry.Value
    
    $t = $asm.GetType($expectedClassName)
    if (-not $t) { throw "Class $expectedClassName not found in Assembly-CSharp" }
    
    $inst = [System.Activator]::CreateInstance($t)
    $typeNameVal = $t.GetField("typeName").GetValue($inst)
    $footprintVal = $t.GetField("footprint").GetValue($inst)
    
    if ($typeNameVal -ne $targetTypeName) {
        throw "Class $expectedClassName typeName field is '$typeNameVal', expected '$targetTypeName'"
    }
    
    # Simulate WaveSpawner reflection lookup:
    $resolved = $null
    foreach ($sub in $allUnitSubtypes) {
        try {
            $temp = [System.Activator]::CreateInstance($sub)
            if ($sub.GetField("typeName").GetValue($temp) -eq $targetTypeName) {
                $resolved = $sub
                break
            }
        } catch {}
    }
    
    if ($resolved -ne $t) {
        throw "Reflection resolution failed for '$targetTypeName': expected $expectedClassName, resolved $($resolved.Name)"
    }
    
    Write-Host "[PASS] Class $expectedClassName successfully instantiated, typeName='$typeNameVal', footprint=($($footprintVal.x), $($footprintVal.y)), reflection resolved cleanly."
}

Write-Host "`nALL EMPIRICAL INTEGRITY TESTS PASSED WITH ZERO ERRORS!"
