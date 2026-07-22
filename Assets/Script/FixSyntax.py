import sys

def fix_lines(filepath, replacements):
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()
        
    for line_num, new_text in replacements.items():
        if line_num - 1 < len(lines):
            lines[line_num - 1] = new_text + '\n'
            
    with open(filepath, 'w', encoding='utf-8') as f:
        f.writelines(lines)

fix_lines(r'C:\Users\wish0\Kubonhun\자료\Kubonhun\Project_file\Team_Dummy\GrimArchive_Prototype\Assets\Script\Unit\Session\InputManager.cs', {
    400: '		if (selectedUnits.Count > 0) LogHelper.Log(LogHelper.GAME, $\"Drag Select: {selectedUnits.Count}\");',
    438: '		LogHelper.Log(LogHelper.GAME, $\"Double Click Select: {origin.unitType.typeName} nearby {selectedUnits.Count}\");'
})

fix_lines(r'C:\Users\wish0\Kubonhun\자료\Kubonhun\Project_file\Team_Dummy\GrimArchive_Prototype\Assets\Script\UI\DebugInfoPanel.cs', {
    230: '        sb.AppendLine($\"<b>진영:</b> {(u.IsHumanFaction ? \\"인간\\" : (u.IsPlayerMonsterFaction ? \\"플몬\\" : \\"야생\\"))}\");',
    294: '            string faction = u.IsHumanFaction ? \"인간\" : (u.IsPlayerMonsterFaction ? \"플몬\" : \"야생\");'
})
