# Victory Auditor Independent Audit Script
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$failures = 0
$passes = 0

function Assert-Check {
    param(
        [bool]$Condition,
        [string]$Name,
        [string]$Details = ""
    )
    if ($Condition) {
        $global:passes++
        Write-Host " [PASS] $Name" -ForegroundColor Green
        if ($Details -ne "") { Write-Host "        $Details" -ForegroundColor DarkGray }
    } else {
        $global:failures++
        Write-Host " [FAIL] $Name" -ForegroundColor Red
        if ($Details -ne "") { Write-Host "        $Details" -ForegroundColor Yellow }
    }
}

Write-Host "=== VICTORY AUDIT INDEPENDENT VERIFICATION ===" -ForegroundColor Cyan
Write-Host "Timestamp: $(Get-Date -Format 'yyyy-MM-ddTHH:mm:sszzz')"

$legacyTankName = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xEA, 0xB7, 0xBC, 0xEC, 0xA0, 0x91, 0x20, 0xED, 0x83, 0xB1, 0xEC, 0xBB, 0xA4)) # 근접 탱커
$dollKnightName = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xEC, 0x9D, 0xB8, 0xED, 0x98, 0x95, 0x20, 0xEA, 0xB8, 0xB0, 0xEC, 0x82, 0xAC)) # 인형 기사
$gnoleName      = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xEB, 0x86, 0x80)) # 놀
$goblinHoodName = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xEA, 0xB3, 0xA0, 0xEB, 0xB8, 0x94, 0xEB, 0xA6, 0xB0, 0x20, 0xED, 0x9B, 0x84, 0xEB, 0x93, 0x9C)) # 고블린 후드

# ----------------------------------------------------
# 1. DATA PARSING & VALIDATION (Assets/Data/units.json)
# ----------------------------------------------------
Write-Host "`n--- 1. units.json Validation ---" -ForegroundColor Yellow
$unitsJsonPath = "Assets/Data/units.json"
Assert-Check (Test-Path $unitsJsonPath) "units.json file exists"

$unitsJsonRaw = [System.IO.File]::ReadAllText($unitsJsonPath, [System.Text.Encoding]::UTF8)
$unitsObj = $unitsJsonRaw | ConvertFrom-Json
Assert-Check ($null -ne $unitsObj -and $null -ne $unitsObj.units) "units.json parses successfully as valid JSON"

$unitCount = $unitsObj.units.Count
Assert-Check ($unitCount -ge 16) "units.json contains expected unit count ($unitCount units)"

$unitMap = @{}
foreach ($u in $unitsObj.units) {
    $unitMap[$u.typeName] = $u
}

# Check MeleeTank reference entry
Assert-Check ($unitMap.ContainsKey($legacyTankName)) "Legacy '$legacyTankName' entry exists in units.json"
$meleeTank = $unitMap[$legacyTankName]

# Check skills in skills.json
$skillsJsonPath = "Assets/Data/skills.json"
Assert-Check (Test-Path $skillsJsonPath) "skills.json file exists"
$skillsRaw = [System.IO.File]::ReadAllText($skillsJsonPath, [System.Text.Encoding]::UTF8)
$skillsObj = $skillsRaw | ConvertFrom-Json
$allSkillNames = @($skillsObj.skills | ForEach-Object { $_.skillName })

$expectedSkills = @($meleeTank.skills)
foreach ($sk in $expectedSkills) {
    Assert-Check ($allSkillNames -contains $sk) "Skill '$sk' is defined in skills.json"
}

$targetMonsters = @(
    @{
        typeName = $dollKnightName
        expectedSpriteLib = "Mon_DollKnight"
        spriteLibPath = "Assets/Sprite/Mon/Mon_DollKnight/Mon_DollKnight.spriteLib"
        spriteLibMetaPath = "Assets/Sprite/Mon/Mon_DollKnight/Mon_DollKnight.spriteLib.meta"
        prefabPath = "Assets/Resources/Units/$dollKnightName.prefab"
        csharpClass = "DollKnight"
    },
    @{
        typeName = $gnoleName
        expectedSpriteLib = "Mon_GnoleA"
        spriteLibPath = "Assets/Sprite/Mon/Mon_Gnole/Mon_GnoleA.spriteLib"
        spriteLibMetaPath = "Assets/Sprite/Mon/Mon_Gnole/Mon_GnoleA.spriteLib.meta"
        prefabPath = "Assets/Resources/Units/$gnoleName.prefab"
        csharpClass = "GnoleA"
    },
    @{
        typeName = $goblinHoodName
        expectedSpriteLib = "Mon_GoblinHoodA"
        spriteLibPath = "Assets/Sprite/Mon/Mon_GoblinHood/Mon_GoblinHoodA.spriteLib"
        spriteLibMetaPath = "Assets/Sprite/Mon/Mon_GoblinHood/Mon_GoblinHoodA.spriteLib.meta"
        prefabPath = "Assets/Resources/Units/$goblinHoodName.prefab"
        csharpClass = "GoblinHoodA"
    }
)

$statKeys = @(
    "maxHp", "maxMp", "physicalAttack", "magicalAttack", "physicalDefense",
    "magicalDefense", "HPRegen", "attackspeed", "walkSpeed", "reaction",
    "criticalChance", "cooltimeReduction", "statusResistance", "maxMental",
    "mental", "spotting", "leadershipRange", "charisma", "physicalAttackSpeed",
    "magicalCastSpeed"
)

foreach ($mon in $targetMonsters) {
    $name = $mon.typeName
    Write-Host "`n  [Monster: $name]" -ForegroundColor Cyan
    Assert-Check ($unitMap.ContainsKey($name)) "Entry for '$name' found in units.json"
    if ($unitMap.ContainsKey($name)) {
        $entry = $unitMap[$name]
        Assert-Check ($entry.unitClass -eq "Monster") "'$name' unitClass == 'Monster' (actual: '$($entry.unitClass)')"
        Assert-Check ($entry.visual.spriteLibrary -eq $mon.expectedSpriteLib) "'$name' spriteLibrary == '$($mon.expectedSpriteLib)' (actual: '$($entry.visual.spriteLibrary)')"
        
        $monSkills = @($entry.skills)
        $skillsMatch = ($monSkills.Count -eq 3) -and 
                       ($monSkills[0] -eq $expectedSkills[0]) -and 
                       ($monSkills[1] -eq $expectedSkills[1]) -and 
                       ($monSkills[2] -eq $expectedSkills[2])
        Assert-Check $skillsMatch "'$name' skills match exactly Melee Tank skills" "Actual: $($monSkills -join ', ')"

        $allStatsMatch = $true
        $statMismatchDetails = @()
        foreach ($k in $statKeys) {
            $expectedVal = $meleeTank.stats.$k
            $actualVal = $entry.stats.$k
            if ($expectedVal -ne $actualVal) {
                $allStatsMatch = $false
                $statMismatchDetails += "$k expected $expectedVal, got $actualVal"
            }
        }
        Assert-Check $allStatsMatch "'$name' all 20 stats match Melee Tank exactly" ($statMismatchDetails -join "; ")

        Assert-Check ($entry.footprint[0] -eq 1 -and $entry.footprint[1] -eq 1) "'$name' footprint == [1, 1]"
        Assert-Check ($entry.engageDistance -eq 3) "'$name' engageDistance == 3"
        Assert-Check ($entry.populationCost -eq 1) "'$name' populationCost == 1"

        Assert-Check ($entry.visual.effects.hitSpark -eq "VFX_HitSpark") "'$name' VFX hitSpark == 'VFX_HitSpark'"
        Assert-Check ($entry.visual.effects.bloodDrip -eq "VFX_BloodDrip") "'$name' VFX bloodDrip == 'VFX_BloodDrip'"
        Assert-Check ($entry.visual.effects.guard -eq "VFX_Guard") "'$name' VFX guard == 'VFX_Guard'"
        Assert-Check ($entry.visual.effects.parry -eq "VFX_Parry") "'$name' VFX parry == 'VFX_Parry'"
    }
}

# ----------------------------------------------------
# 2. SPRITE ASSET & PREFAB GUID VERIFICATION
# ----------------------------------------------------
Write-Host "`n--- 2. Sprite Assets & Prefabs Verification ---" -ForegroundColor Yellow

foreach ($mon in $targetMonsters) {
    $name = $mon.typeName
    Write-Host "`n  [Asset & Prefab: $name]" -ForegroundColor Cyan

    Assert-Check (Test-Path $mon.spriteLibPath) "SpriteLib asset exists at $($mon.spriteLibPath)"
    Assert-Check (Test-Path $mon.spriteLibMetaPath) "SpriteLib .meta exists at $($mon.spriteLibMetaPath)"
    
    $spriteLibMetaRaw = [System.IO.File]::ReadAllText($mon.spriteLibMetaPath, [System.Text.Encoding]::UTF8)
    $spriteLibGuid = ""
    if ($spriteLibMetaRaw -match "guid:\s*([a-f0-9]+)") {
        $spriteLibGuid = $Matches[1]
    }
    Assert-Check ($spriteLibGuid -ne "") "SpriteLib GUID extracted: $spriteLibGuid"

    Assert-Check (Test-Path $mon.prefabPath) "Prefab exists at $($mon.prefabPath)"
    $prefabMetaPath = "$($mon.prefabPath).meta"
    Assert-Check (Test-Path $prefabMetaPath) "Prefab .meta exists at $prefabMetaPath"

    if (Test-Path $mon.prefabPath) {
        $prefabRaw = [System.IO.File]::ReadAllText($mon.prefabPath, [System.Text.Encoding]::UTF8)
        
        $hasSpriteLibGuid = $prefabRaw -match "guid:\s*$spriteLibGuid"
        Assert-Check $hasSpriteLibGuid "Prefab SpriteLibrary references correct SpriteLib GUID ($spriteLibGuid)"

        $hasVisualDef = $prefabRaw -match "UnitVisualDefinition" -or $prefabRaw -match "guid: 7691d3f2fcbe48144aceffad6e5bea89"
        Assert-Check $hasVisualDef "Prefab contains UnitVisualDefinition component"

        $hasShadowCaster = $prefabRaw -match "ShadowCaster2D" -or $prefabRaw -match "guid: 7db70e0ea77f5ac47a8f4565a9406397"
        Assert-Check $hasShadowCaster "Prefab contains ShadowCaster2D component"

        $hasSpriteResolver = $prefabRaw -match "SpriteResolver" -or $prefabRaw -match "guid: ed8b1ae4e4e52b34ea557c1c11e076fc"
        Assert-Check $hasSpriteResolver "Prefab contains SpriteResolver component"

        $hasSpriteRenderer = $prefabRaw -match "SpriteRenderer:"
        Assert-Check $hasSpriteRenderer "Prefab contains SpriteRenderer component"
    }
}

# ----------------------------------------------------
# 3. C# SOURCE CODE & REFLECTION TYPE VERIFICATION
# ----------------------------------------------------
Write-Host "`n--- 3. C# UnitTypes & Reflection Verification ---" -ForegroundColor Yellow
$unitTypesPath = "Assets/Script/Unit/Core/UnitTypes.cs"
Assert-Check (Test-Path $unitTypesPath) "UnitTypes.cs exists"

$unitTypesContent = [System.IO.File]::ReadAllText($unitTypesPath, [System.Text.Encoding]::UTF8)
foreach ($mon in $targetMonsters) {
    $className = $mon.csharpClass
    $typeName = $mon.typeName
    $classRegex = "public\s+class\s+$className\s*:\s*UnitType"
    $hasSubclass = $unitTypesContent -match $classRegex
    Assert-Check $hasSubclass "UnitTypes.cs defines class $className : UnitType"

    $hasTypeNameAssign = $unitTypesContent -match "typeName\s*=\s*`"$typeName`""
    Assert-Check $hasTypeNameAssign "Class $className constructor sets typeName = `"$typeName`""
}

# ----------------------------------------------------
# 4. COMPILATION VERIFICATION
# ----------------------------------------------------
Write-Host "`n--- 4. Independent MSBuild Compilation ---" -ForegroundColor Yellow

$build1 = & dotnet build Assembly-CSharp.csproj --nologo -v q 2>&1
$build1Exit = $LASTEXITCODE
Assert-Check ($build1Exit -eq 0) "dotnet build Assembly-CSharp.csproj exits with code 0"

$build2 = & dotnet build Assembly-CSharp-Editor.csproj --nologo -v q 2>&1
$build2Exit = $LASTEXITCODE
Assert-Check ($build2Exit -eq 0) "dotnet build Assembly-CSharp-Editor.csproj exits with code 0"

# ----------------------------------------------------
# 5. FORENSIC INTEGRITY & SHORTCUT / CHEAT DETECTION
# ----------------------------------------------------
Write-Host "`n--- 5. Anti-Cheating & Integrity Forensics ---" -ForegroundColor Yellow

$hasNotImplemented = $unitTypesContent -match "throw new NotImplementedException"
Assert-Check (-not $hasNotImplemented) "UnitTypes.cs contains no NotImplementedException placeholders"

$hasDummyReturn = $unitTypesContent -match "return null;"
Assert-Check (-not $hasDummyReturn) "UnitTypes.cs contains no dummy return stubs"

$allTypeNames = @($unitsObj.units | ForEach-Object { $_.typeName })
$uniqueTypeNames = @($allTypeNames | Select-Object -Unique)
Assert-Check ($allTypeNames.Count -eq $uniqueTypeNames.Count) "units.json has no duplicate typeName entries ($($allTypeNames.Count) entries total)"

# ----------------------------------------------------
# SUMMARY
# ----------------------------------------------------
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "VICTORY AUDIT RESULT SUMMARY:" -ForegroundColor Cyan
Write-Host "  Total Passes  : $passes" -ForegroundColor Green
Write-Host "  Total Failures: $failures" -ForegroundColor $(if ($failures -eq 0) { "Green" } else { "Red" })
Write-Host "========================================" -ForegroundColor Cyan

if ($failures -eq 0) {
    Write-Host "`n>>> INDEPENDENT VERDICT: ALL AUDIT CHECKS PASSED (VICTORY CONFIRMED) <<<" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n>>> INDEPENDENT VERDICT: AUDIT CHECKS FAILED (VICTORY REJECTED) <<<" -ForegroundColor Red
    exit 1
}
