import os
import glob

def replace_in_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
        
    original = content
    
    # DebugInfoPanel.cs
    content = content.replace('u.IsPlayerFaction ? "인간" : "야생"', 'u.IsHumanFaction ? "인간" : (u.IsPlayerMonsterFaction ? "플몬" : "야생")')
    content = content.replace('u.IsPlayerFaction ? "white" : "yellow"', 'u.IsHumanFaction ? "white" : (u.IsPlayerMonsterFaction ? "green" : "yellow")')
    
    # UI Text
    content = content.replace('unit.IsPlayerFaction ? Color.green : Color.red', 'unit.IsHumanFaction ? Color.cyan : (unit.IsPlayerMonsterFaction ? Color.green : Color.red)')
    content = content.replace('u.IsPlayerFaction ? Color.green : Color.red', 'u.IsHumanFaction ? Color.cyan : (u.IsPlayerMonsterFaction ? Color.green : Color.red)')
    
    # AStarMovement.cs
    content = content.replace('unit.IsPlayerFaction ? Unit.humanFactionData : Unit.monsterFactionData', 'unit.IsHumanFaction ? Unit.humanFactionData : Unit.monsterFactionData')
    
    # GameSession.cs logic
    content = content.replace('u.FactionBehavior is PlayerUnitBehavior && OffenseProcessor.Instance != null', '(u.FactionBehavior is HumanFactionBehavior || u.FactionBehavior is PlayerMonsterBehavior) && OffenseProcessor.Instance != null')
    content = content.replace('IsPlayerFaction', 'IsHumanFaction')
    content = content.replace('IsMonsterFaction', 'IsWildMonsterFaction')
    content = content.replace('PlayerUnitBehavior', 'PlayerMonsterBehavior')
    
    if content != original:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f'Updated {filepath}')

search_dir = r'C:\Users\wish0\Kubonhun\자료\Kubonhun\Project_file\Team_Dummy\GrimArchive_Prototype\Assets\Script'
for root, dirs, files in os.walk(search_dir):
    for file in files:
        if file.endswith('.cs'):
            replace_in_file(os.path.join(root, file))
