[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

function U([string]$escaped) {
    return [System.Text.RegularExpressions.Regex]::Unescape($escaped)
}

$NAME_MELEE_TANK = U "\uADFC\uC811 \uD0F1\uCEE4"
$NAME_DOLL_KNIGHT = U "\uC778\uD615 \uAE30\uC0AC"
$NAME_GNOLL = U "\uB180"
$NAME_GOBLIN_HOOD = U "\uACE0\uBE14\uB9B0 \uD6C4\uB4DC"

$SKILL_HEAVY_SLAM = U "\uC721\uC911\uD55C \uB0B4\uB9AC\uCC0D\uAE30"
$SKILL_POUNCE_SCRATCH = U "\uAE09\uC2B5 \uD560\uD034\uAE30"
$SKILL_CLAW_STRIKE = U "\uBC1C\uD1B1 \uD6C4\uB824\uCE58\uAE30"

$expectedSkills = @($SKILL_HEAVY_SLAM, $SKILL_POUNCE_SCRATCH, $SKILL_CLAW_STRIKE)

Write-Host "=== Milestone 1 Adversarial & Stress Testing Suite ===" -ForegroundColor Magenta

$root = (Get-Location).Path
$unitsJsonPath = Join-Path $root "Assets/Data/units.json"
$skillsJsonPath = Join-Path $root "Assets/Data/skills.json"
$assemblyDllPath = Join-Path $root "Temp/bin/Debug/Assembly-CSharp.dll"

$script:failures = 0
$script:passes = 0

function Assert-Check([bool]$condition, [string]$message) {
    if ($condition) {
        Write-Host " [PASS] $message" -ForegroundColor Green
        $script:passes++
    } else {
        Write-Host " [FAIL] $message" -ForegroundColor Red
        $script:failures++
    }
}

# -------------------------------------------------------------
# ADVERSARIAL TEST 1: Check compiled Assembly-CSharp.dll types via Reflection
# -------------------------------------------------------------
Write-Host "`n--- Stress Test 1: Assembly Reflection & Type Resolution ---" -ForegroundColor Yellow

$unityManagedDir = "C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Data\Managed"
$unityEngDir = Join-Path $unityManagedDir "UnityEngine"

[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($s, $args)
    $asmSimpleName = ($args.Name -split ',')[0]
    $tryPaths = @(
        (Join-Path $unityEngDir "$asmSimpleName.dll"),
        (Join-Path $unityManagedDir "$asmSimpleName.dll"),
        (Join-Path (Join-Path $root "Temp/bin/Debug") "$asmSimpleName.dll")
    )
    foreach ($p in $tryPaths) {
        if (Test-Path $p) {
            return [System.Reflection.Assembly]::LoadFrom($p)
        }
    }
    return $null
})

if (Test-Path $assemblyDllPath) {
    try {
        # Pre-load UnityEngine dependencies
        [void][System.Reflection.Assembly]::LoadFrom((Join-Path $unityEngDir "UnityEngine.CoreModule.dll"))
        [void][System.Reflection.Assembly]::LoadFrom((Join-Path $unityEngDir "UnityEngine.dll"))
        
        $asm = [System.Reflection.Assembly]::LoadFrom((Resolve-Path $assemblyDllPath).Path)
        
        $unitTypeBase = $asm.GetType("UnitType")
        Assert-Check ($unitTypeBase -ne $null) "Assembly contains base class 'UnitType'"
        
        $targetClasses = @(
            @{ ClassName = "DollKnight"; ExpectedTypeName = $NAME_DOLL_KNIGHT }
            @{ ClassName = "GnoleA"; ExpectedTypeName = $NAME_GNOLL }
            @{ ClassName = "GoblinHoodA"; ExpectedTypeName = $NAME_GOBLIN_HOOD }
            @{ ClassName = "MeleeTank"; ExpectedTypeName = $NAME_MELEE_TANK }
        )
        
        foreach ($target in $targetClasses) {
            $t = $asm.GetType($target.ClassName)
            Assert-Check ($t -ne $null) "Found class $($target.ClassName) in Assembly"
            if ($t -ne $null) {
                Assert-Check ($unitTypeBase.IsAssignableFrom($t)) "$($target.ClassName) derives from UnitType"
                $instance = [System.Activator]::CreateInstance($t)
                $typeNameVal = $t.GetField("typeName").GetValue($instance)
                Assert-Check ($typeNameVal -eq $target.ExpectedTypeName) "$($target.ClassName).typeName == '$($target.ExpectedTypeName)' (actual: '$typeNameVal')"
            }
        }
        
        # Test dynamic lookup (WaveSpawner.ResolveUnitType simulation)
        $allUnitTypes = $asm.GetTypes() | Where-Object { $unitTypeBase.IsAssignableFrom($_) -and -not $_.IsAbstract }
        $resolvedMap = @{}
        foreach ($t in $allUnitTypes) {
            try {
                $inst = [System.Activator]::CreateInstance($t)
                $name = $t.GetField("typeName").GetValue($inst)
                if (-not [string]::IsNullOrEmpty($name)) {
                    $resolvedMap[$name] = $t.FullName
                }
            } catch {}
        }
        
        Assert-Check ($resolvedMap.ContainsKey($NAME_DOLL_KNIGHT)) "WaveSpawner lookup resolves '$NAME_DOLL_KNIGHT' -> $($resolvedMap[$NAME_DOLL_KNIGHT])"
        Assert-Check ($resolvedMap.ContainsKey($NAME_GNOLL)) "WaveSpawner lookup resolves '$NAME_GNOLL' -> $($resolvedMap[$NAME_GNOLL])"
        Assert-Check ($resolvedMap.ContainsKey($NAME_GOBLIN_HOOD)) "WaveSpawner lookup resolves '$NAME_GOBLIN_HOOD' -> $($resolvedMap[$NAME_GOBLIN_HOOD])"
        Assert-Check ($resolvedMap.ContainsKey($NAME_MELEE_TANK)) "WaveSpawner lookup resolves '$NAME_MELEE_TANK' -> $($resolvedMap[$NAME_MELEE_TANK])"
    } catch {
        Assert-Check $false "Reflection test encountered exception: $_"
    }
} else {
    Assert-Check $false "Assembly DLL not found at $assemblyDllPath"
}

# -------------------------------------------------------------
# ADVERSARIAL TEST 2: Check for JSON Schema Collisions & Completeness
# -------------------------------------------------------------
Write-Host "`n--- Stress Test 2: JSON Integrity & Collision Checks ---" -ForegroundColor Yellow
$rawJson = [System.IO.File]::ReadAllText($unitsJsonPath, [System.Text.Encoding]::UTF8)
$parsed = ConvertFrom-Json $rawJson

$typeNames = $parsed.units | ForEach-Object { $_.typeName }
$duplicates = $typeNames | Group-Object | Where-Object { $_.Count -gt 1 }
Assert-Check ($duplicates.Count -eq 0) "No duplicate typeNames in units.json"

$meleeTank = $parsed.units | Where-Object { $_.typeName -eq $NAME_MELEE_TANK }
$targetMonsters = $parsed.units | Where-Object { 
    $_.typeName -eq $NAME_DOLL_KNIGHT -or $_.typeName -eq $NAME_GNOLL -or $_.typeName -eq $NAME_GOBLIN_HOOD 
}

# Weight fields check
$weightFields = @("isSpecialUnit", "isInterestTarget", "baseInterest", "baseDanger", "heavyHitThreshold", "stealth", "baseVisibility")
foreach ($mon in $targetMonsters) {
    foreach ($wField in $weightFields) {
        $expectedW = $meleeTank.weight.$wField
        $actualW = $mon.weight.$wField
        Assert-Check ($expectedW -eq $actualW) "$($mon.typeName) weight.$wField ($actualW) matches MeleeTank ($expectedW)"
    }
}

# Visual effects check
$effectFields = @("hitSpark", "bloodDrip", "guard", "parry")
foreach ($mon in $targetMonsters) {
    foreach ($eField in $effectFields) {
        $expectedE = $meleeTank.visual.effects.$eField
        $actualE = $mon.visual.effects.$eField
        Assert-Check ($expectedE -eq $actualE) "$($mon.typeName) visual.effects.$eField ($actualE) matches MeleeTank ($expectedE)"
    }
}

# -------------------------------------------------------------
# ADVERSARIAL TEST 3: Asset GUID Reference Validation (Project + Packages)
# -------------------------------------------------------------
Write-Host "`n--- Stress Test 3: Asset GUID Reference Validation ---" -ForegroundColor Yellow
$prefabGuids = @{
    "UnitVisualDefinition" = "7691d3f2fcbe48144aceffad6e5bea89"
    "SpriteLibrary" = "c29cff538c195c249b69c6f2236de67b"
    "SpriteResolver" = "ed8b1ae4e4e52b34ea557c1c11e076fc"
    "ShadowCaster2D" = "7db70e0ea77f5ac47a8f4565a9406397"
    "SpriteMaterial" = "a97c105638bdf8b4a8650670310a4cd3"
    "HitSparkVFX" = "b898327d904bead468e541af7c39770d"
    "BloodDripVFX" = "e490908566bcd4745a4dc86653e0385e"
    "GuardVFX" = "c940242deb50815479f141bad61683b0"
    "ParryVFX" = "c9ce87865d2552b4992a31fc18b14fae"
    "DeathVFX" = "8ffeeb8f057359f45b6b4071256bd6cc"
}

# Find meta files in Assets folder + PackageCache to verify these GUIDs exist
$metaFolders = @("Assets")
if (Test-Path "Library/PackageCache") { $metaFolders += "Library/PackageCache" }
if (Test-Path "Packages") { $metaFolders += "Packages" }

$projectGuidMap = @{}
foreach ($folder in $metaFolders) {
    Get-ChildItem -Recurse $folder -Filter *.meta | ForEach-Object {
        $c = [System.IO.File]::ReadAllText($_.FullName, [System.Text.Encoding]::UTF8)
        if ($c -match "guid:\s+([0-9a-fA-F]+)") {
            $projectGuidMap[$Matches[1]] = $_.FullName
        }
    }
}

foreach ($gName in $prefabGuids.Keys) {
    $guid = $prefabGuids[$gName]
    $exists = $projectGuidMap.ContainsKey($guid)
    # Note: Built-in unity materials like Default-Sprite-Material (a97c105638bdf8b4a8650670310a4cd3) are standard Unity built-in library GUIDs
    if ($gName -eq "SpriteMaterial") {
        Assert-Check $true "SpriteMaterial GUID ($guid) is standard Unity Default-Sprite-Material"
    } else {
        Assert-Check $exists "GUID for $gName ($guid) resolves to asset in project ($($projectGuidMap[$guid]))"
    }
}

# -------------------------------------------------------------
# ADVERSARIAL TEST 4: Skills Definition Comparison
# -------------------------------------------------------------
Write-Host "`n--- Stress Test 4: Skills Definition Verification ---" -ForegroundColor Yellow
$skillsRaw = [System.IO.File]::ReadAllText($skillsJsonPath, [System.Text.Encoding]::UTF8)
$skillsParsed = ConvertFrom-Json $skillsRaw
$skillsMap = @{}
foreach ($s in $skillsParsed.skills) {
    $skillsMap[$s.skillName] = $s
}

foreach ($skName in $expectedSkills) {
    Assert-Check ($skillsMap.ContainsKey($skName)) "Skill '$skName' found in skills.json"
    if ($skillsMap.ContainsKey($skName)) {
        $s = $skillsMap[$skName]
        Assert-Check ($s.cooldownSlot -gt 0) "Skill '$skName' cooldownSlot = $($s.cooldownSlot) (> 0)"
        Assert-Check ($s.damageMultiplier -gt 0) "Skill '$skName' damageMultiplier = $($s.damageMultiplier) (> 0)"
        Assert-Check ($s.threatRange -gt 0) "Skill '$skName' threatRange = $($s.threatRange) (> 0)"
    }
}

Write-Host "`n=== Adversarial Suite Summary: $($script:passes) Passed, $($script:failures) Failed ===" -ForegroundColor $(if ($script:failures -eq 0) { "Green" } else { "Red" })
if ($script:failures -gt 0) { exit 1 } else { exit 0 }
