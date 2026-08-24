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

Write-Host "=== Milestone 1 Empirical Verification Suite ===" -ForegroundColor Cyan

$root = (Get-Location).Path
$unitsJsonPath = Join-Path $root "Assets/Data/units.json"
$skillsJsonPath = Join-Path $root "Assets/Data/skills.json"
$unitsDir = Join-Path $root "Assets/Resources/Units"
$monSpriteDir = Join-Path $root "Assets/Sprite/Mon"

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

function To-YamlUnicode([string]$str) {
    $sb = New-Object System.Text.StringBuilder
    foreach ($c in $str.ToCharArray()) {
        $val = [int]$c
        if ($val -gt 127) {
            [void]$sb.AppendFormat("\u{0:X4}", $val)
        } else {
            [void]$sb.Append($c)
        }
    }
    return $sb.ToString()
}

# 1. Verify units.json exists and is valid JSON
Assert-Check (Test-Path $unitsJsonPath) "units.json exists at $unitsJsonPath"

$rawJson = [System.IO.File]::ReadAllText($unitsJsonPath, [System.Text.Encoding]::UTF8)
$parsed = $null
try {
    $parsed = ConvertFrom-Json $rawJson
    Assert-Check ($parsed -ne $null -and $parsed.units -ne $null) "units.json is valid JSON with 'units' array (total: $($parsed.units.Count) units)"
} catch {
    Assert-Check $false "Failed to parse units.json: $_"
}

# 2. Find MeleeTank ('근접 탱커') and the 3 target monsters
$meleeTank = $parsed.units | Where-Object { $_.typeName -eq $NAME_MELEE_TANK }
$dollKnight = $parsed.units | Where-Object { $_.typeName -eq $NAME_DOLL_KNIGHT }
$gnoll = $parsed.units | Where-Object { $_.typeName -eq $NAME_GNOLL }
$goblinHood = $parsed.units | Where-Object { $_.typeName -eq $NAME_GOBLIN_HOOD }

Assert-Check ($meleeTank -ne $null) "'$NAME_MELEE_TANK' entry exists in units.json"
Assert-Check ($dollKnight -ne $null) "'$NAME_DOLL_KNIGHT' entry exists in units.json"
Assert-Check ($gnoll -ne $null) "'$NAME_GNOLL' entry exists in units.json"
Assert-Check ($goblinHood -ne $null) "'$NAME_GOBLIN_HOOD' entry exists in units.json"

# 3. Verify unitClass
Assert-Check ($dollKnight.unitClass -eq "Monster") "$NAME_DOLL_KNIGHT unitClass == Monster"
Assert-Check ($gnoll.unitClass -eq "Monster") "$NAME_GNOLL unitClass == Monster"
Assert-Check ($goblinHood.unitClass -eq "Monster") "$NAME_GOBLIN_HOOD unitClass == Monster"

# 4. Verify spriteLibrary names
Assert-Check ($dollKnight.visual.spriteLibrary -eq "Mon_DollKnight") "$NAME_DOLL_KNIGHT spriteLibrary == Mon_DollKnight"
Assert-Check ($gnoll.visual.spriteLibrary -eq "Mon_GnoleA") "$NAME_GNOLL spriteLibrary == Mon_GnoleA"
Assert-Check ($goblinHood.visual.spriteLibrary -eq "Mon_GoblinHoodA") "$NAME_GOBLIN_HOOD spriteLibrary == Mon_GoblinHoodA"

# 5. Verify skills exact match: ["육중한 내리찍기", "급습 할퀴기", "발톱 후려치기"]
$monstersList = @($dollKnight, $gnoll, $goblinHood)

foreach ($mon in $monstersList) {
    if ($mon -ne $null) {
        $skillsMatch = ($mon.skills.Count -eq 3) -and 
                       ($mon.skills[0] -eq $expectedSkills[0]) -and 
                       ($mon.skills[1] -eq $expectedSkills[1]) -and 
                       ($mon.skills[2] -eq $expectedSkills[2])
        $skillStr = $mon.skills -join ', '
        Assert-Check $skillsMatch "$($mon.typeName) skills exact match: [$skillStr]"
    } else {
        Assert-Check $false "Monster entry is null for skill check"
    }
}

# 6. Verify exact match of all 20 stats numeric fields with '근접 탱커'
$numericStats = @(
    "maxHp", "maxMp", "physicalAttack", "magicalAttack", "physicalDefense", "magicalDefense",
    "HPRegen", "attackspeed", "walkSpeed", "reaction", "criticalChance", "cooltimeReduction",
    "statusResistance", "maxMental", "mental", "spotting", "leadershipRange", "charisma",
    "physicalAttackSpeed", "magicalCastSpeed"
)

Assert-Check ($numericStats.Count -eq 20) "Checked exactly 20 numeric stat fields"

foreach ($mon in $monstersList) {
    if ($mon -ne $null -and $meleeTank -ne $null) {
        $statMismatch = @()
        foreach ($stat in $numericStats) {
            $expectedVal = $meleeTank.stats.$stat
            $actualVal = $mon.stats.$stat
            if ($expectedVal -ne $actualVal) {
                $statMismatch += "$stat (expected $expectedVal, got $actualVal)"
            }
        }
        if ($statMismatch.Count -eq 0) {
            Assert-Check $true "$($mon.typeName) all 20 stats match '$NAME_MELEE_TANK' exactly"
        } else {
            $mismatchStr = $statMismatch -join ', '
            Assert-Check $false "$($mon.typeName) stats mismatch: $mismatchStr"
        }
    }
}

# 7. Verify other fields (footprint, engageDistance, populationCost, weight, visual effects)
foreach ($mon in $monstersList) {
    if ($mon -ne $null) {
        Assert-Check ($mon.footprint[0] -eq 1 -and $mon.footprint[1] -eq 1) "$($mon.typeName) footprint == [1, 1]"
        Assert-Check ($mon.engageDistance -eq 3) "$($mon.typeName) engageDistance == 3"
        Assert-Check ($mon.populationCost -eq 1) "$($mon.typeName) populationCost == 1"
        Assert-Check ($mon.visual.effects.hitSpark -eq "VFX_HitSpark") "$($mon.typeName) hitSpark == VFX_HitSpark"
        Assert-Check ($mon.visual.effects.bloodDrip -eq "VFX_BloodDrip") "$($mon.typeName) bloodDrip == VFX_BloodDrip"
        Assert-Check ($mon.visual.effects.guard -eq "VFX_Guard") "$($mon.typeName) guard == VFX_Guard"
        Assert-Check ($mon.visual.effects.parry -eq "VFX_Parry") "$($mon.typeName) parry == VFX_Parry"
    }
}

# 8. Check spriteLib files in Assets/Sprite/Mon/ and their GUIDs
$expectedSpriteLibs = @{
    "Mon_DollKnight" = @{ Folder = "Mon_DollKnight"; FileName = "Mon_DollKnight.spriteLib" }
    "Mon_GnoleA" = @{ Folder = "Mon_Gnole"; FileName = "Mon_GnoleA.spriteLib" }
    "Mon_GoblinHoodA" = @{ Folder = "Mon_GoblinHood"; FileName = "Mon_GoblinHoodA.spriteLib" }
}

$spriteLibGuids = @{}

foreach ($libName in $expectedSpriteLibs.Keys) {
    $info = $expectedSpriteLibs[$libName]
    $spriteLibPath = Join-Path $monSpriteDir (Join-Path $info.Folder $info.FileName)
    $metaPath = "$spriteLibPath.meta"
    
    Assert-Check (Test-Path $spriteLibPath) "SpriteLib asset exists: $spriteLibPath"
    Assert-Check (Test-Path $metaPath) "SpriteLib .meta exists: $metaPath"
    
    if (Test-Path $metaPath) {
        $metaContent = [System.IO.File]::ReadAllText($metaPath, [System.Text.Encoding]::UTF8)
        if ($metaContent -match "guid:\s+([0-9a-fA-F]+)") {
            $guid = $Matches[1]
            $spriteLibGuids[$libName] = $guid
            Write-Host "  -> $libName GUID: $guid" -ForegroundColor DarkGray
        }
    }
}

# 9. Verify Prefab files in Assets/Resources/Units/
$monConfigs = @(
    @{ TypeName = $NAME_DOLL_KNIGHT; SpriteLib = "Mon_DollKnight" }
    @{ TypeName = $NAME_GNOLL; SpriteLib = "Mon_GnoleA" }
    @{ TypeName = $NAME_GOBLIN_HOOD; SpriteLib = "Mon_GoblinHoodA" }
)

foreach ($mon in $monConfigs) {
    $tName = $mon.TypeName
    $prefabPath = Join-Path $unitsDir "$tName.prefab"
    $metaPath = "$prefabPath.meta"
    
    Assert-Check (Test-Path $prefabPath) "Prefab exists: $prefabPath"
    Assert-Check (Test-Path $metaPath) "Prefab .meta exists: $metaPath"
    
    if (Test-Path $prefabPath) {
        $content = [System.IO.File]::ReadAllText($prefabPath, [System.Text.Encoding]::UTF8)
        
        # Check UnitVisualDefinition component
        Assert-Check ($content -match "m_Script: \{fileID: 11500000, guid: 7691d3f2fcbe48144aceffad6e5bea89, type: 3\}") "$tName.prefab has UnitVisualDefinition script"
        Assert-Check ($content -match "SpriteLibrary") "$tName.prefab has SpriteLibrary component"
        
        # Check GUID linkage
        $expectedGuid = $spriteLibGuids[$mon.SpriteLib]
        if ($content -match "m_SpriteLibraryAsset: \{fileID: [0-9\-]+, guid: $expectedGuid, type: [23]\}") {
            Assert-Check $true "$tName.prefab SpriteLibraryAsset GUID correctly links to $($mon.SpriteLib) ($expectedGuid)"
        } else {
            Assert-Check $false "$tName.prefab SpriteLibraryAsset GUID mismatch with $($mon.SpriteLib) ($expectedGuid)"
        }
        
        # Check ShadowCaster2D
        Assert-Check ($content -match "ShadowCaster2D") "$tName.prefab contains ShadowCaster2D component"
        
        # Check Visual child GameObject
        Assert-Check ($content -match "m_Name: Visual") "$tName.prefab contains Visual child GameObject"
        
        # Check root GameObject name (supports both raw UTF-8 and YAML unicode escape)
        $yamlEscaped = To-YamlUnicode $tName
        $hasRootName = ($content -match "m_Name:\s*`"$tName`"") -or ($content -match [regex]::Escape("m_Name: `"$yamlEscaped`""))
        Assert-Check $hasRootName "$tName.prefab root object named '$tName' (or '$yamlEscaped')"
        
        # Check unitTypeName in UnitVisualDefinition
        $hasUnitTypeName = ($content -match "unitTypeName:\s*`"$tName`"") -or ($content -match [regex]::Escape("unitTypeName: `"$yamlEscaped`""))
        Assert-Check $hasUnitTypeName "$tName.prefab unitTypeName is '$tName' (or '$yamlEscaped')"
    }
}

# 10. Verify UnitTypes.cs C# class registration
$unitTypesPath = Join-Path $root "Assets/Script/Unit/Core/UnitTypes.cs"
Assert-Check (Test-Path $unitTypesPath) "UnitTypes.cs exists at $unitTypesPath"
$unitTypesCode = [System.IO.File]::ReadAllText($unitTypesPath, [System.Text.Encoding]::UTF8)

$dollMatch = ($unitTypesCode -match 'class DollKnight\s*:\s*UnitType') -and ($unitTypesCode -match [regex]::Escape($NAME_DOLL_KNIGHT))
$gnollMatch = ($unitTypesCode -match 'class GnoleA\s*:\s*UnitType') -and ($unitTypesCode -match [regex]::Escape($NAME_GNOLL))
$goblinMatch = ($unitTypesCode -match 'class GoblinHoodA\s*:\s*UnitType') -and ($unitTypesCode -match [regex]::Escape($NAME_GOBLIN_HOOD))

Assert-Check $dollMatch "UnitTypes.cs contains DollKnight with typeName '$NAME_DOLL_KNIGHT'"
Assert-Check $gnollMatch "UnitTypes.cs contains GnoleA with typeName '$NAME_GNOLL'"
Assert-Check $goblinMatch "UnitTypes.cs contains GoblinHoodA with typeName '$NAME_GOBLIN_HOOD'"

# 11. Verify skills in skills.json
Assert-Check (Test-Path $skillsJsonPath) "skills.json exists at $skillsJsonPath"
if (Test-Path $skillsJsonPath) {
    $skillsRaw = [System.IO.File]::ReadAllText($skillsJsonPath, [System.Text.Encoding]::UTF8)
    $skillsParsed = ConvertFrom-Json $skillsRaw
    $allSkillNames = $skillsParsed.skills | ForEach-Object { $_.skillName }
    foreach ($sk in $expectedSkills) {
        Assert-Check ($allSkillNames -contains $sk) "Skill '$sk' is defined in skills.json"
    }
}

Write-Host "`n=== Test Summary: $($script:passes) Passed, $($script:failures) Failed ===" -ForegroundColor $(if ($script:failures -eq 0) { "Green" } else { "Red" })
if ($script:failures -gt 0) { exit 1 } else { exit 0 }
