import sys

with open(r'C:\Users\wish0\Kubonhun\자료\Kubonhun\Project_file\Team_Dummy\GrimArchive_Prototype\Assets\Script\UI\DebugInfoPanel.cs', 'r', encoding='utf-8', errors='ignore') as f:
    lines = f.readlines()

new_lines = []
skip = False
for i, line in enumerate(lines):
    if "selectedUnitInfoText.SetupText(sb.ToString());" in line:
        new_lines.append(line)
        new_lines.append("    }\n")
        skip = True
    elif "private string BuildMultiSelectListText(List<Unit> units)" in line:
        skip = False
        new_lines.append(line)
    elif not skip:
        if "string faction = u.IsHumanFaction" in line:
            new_lines.append('            string factionName = u.IsHumanFaction ? "인간" : (u.IsPlayerMonsterFaction ? "플몬" : "야생");\n')
        elif "sb.AppendLine($\"<color={color}>{u.unitType.typeName}" in line:
            new_lines.append('            sb.AppendLine($"<color={color}>{u.unitType.typeName} ({factionName}) HP {u.hp:F0}/{u.maxHp:F0}</color>");\n')
        else:
            new_lines.append(line)

with open(r'C:\Users\wish0\Kubonhun\자료\Kubonhun\Project_file\Team_Dummy\GrimArchive_Prototype\Assets\Script\UI\DebugInfoPanel.cs', 'w', encoding='utf-8') as f:
    f.writelines(new_lines)
