using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class UnitStatsData
{
    public float maxHp, maxMp;
    public float physicalAttack, magicalAttack;
    public float physicalDefense, magicalDefense;
    public float HPRegen, attackspeed, walkSpeed, reaction;
    public float criticalChance, cooltimeReduction, statusResistance;
    public float maxMental, mental, spotting, leadershipRange, charisma;
    public float physicalAttackSpeed, magicalCastSpeed;
}

[System.Serializable]
public class UnitData
{
    public string       typeName;
    public string       unitClass;
    public float[]      footprint;
    public int          engageDistance;
    public string[]     skills;
    public UnitStatsData stats;
}

[System.Serializable]
public class SkillData
{
    public string skillName;
    public float  baseDelayMs;
    public float  baseCooldown;
    public int    cooldownSlot;
    // canHit 사전 검사용 히트박스
    public string hitShape;
    public int    hitRange, hitWidth, hitDepth;
    // 실제 위협 타일 히트박스
    public int    threatRange, threatWidth, threatDepth;
    public float  damageMultiplier;
    public bool   hasStun;
    public float  stunDuration;
    public float  priorityBase;
    public float  priorityKillMultiplier;
    public float  priorityKillBonus;
    public float  priorityRangeThreshold;
    public float  priorityRangeBonus;
}

[System.Serializable] class UnitDatabase  { public UnitData[]  units;  }
[System.Serializable] class SkillDatabase { public SkillData[] skills; }

public static class GameDataLoader
{
    static readonly Dictionary<string, UnitData>          _units      = new();
    static readonly Dictionary<string, SkillData>         _skills     = new();
    static readonly Dictionary<string, List<SkillAction>> _unitSkills = new();
    static bool _loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoLoad() => Load();

    public static void Load()
    {
        LoadSkills();
        LoadUnits();
        _loaded = true;
    }

    static void LoadSkills()
    {
        var asset = Resources.Load<TextAsset>("Data/skills");
        if (asset == null) { Debug.LogError("[GameDataLoader] Data/skills.json not found in Resources"); return; }
        var db = JsonUtility.FromJson<SkillDatabase>(asset.text);
        _skills.Clear();
        foreach (var s in db.skills) _skills[s.skillName] = s;
    }

    static void LoadUnits()
    {
        var asset = Resources.Load<TextAsset>("Data/units");
        if (asset == null) { Debug.LogError("[GameDataLoader] Data/units.json not found in Resources"); return; }
        var db = JsonUtility.FromJson<UnitDatabase>(asset.text);
        _units.Clear();
        _unitSkills.Clear();
        foreach (var u in db.units)
        {
            _units[u.typeName] = u;
            var list = new List<SkillAction>();
            foreach (var sName in u.skills)
            {
                if (_skills.TryGetValue(sName, out var sd))
                    list.Add(new SkillAction_Generic(sd));
                else
                    Debug.LogWarning($"[GameDataLoader] 스킬 '{sName}'을 skills.json에서 찾을 수 없습니다");
            }
            _unitSkills[u.typeName] = list;
        }
    }

    static void EnsureLoaded() { if (!_loaded) Load(); }

    public static UnitData GetUnitData(string typeName)
    {
        EnsureLoaded();
        return _units.TryGetValue(typeName, out var d) ? d : null;
    }

    public static List<SkillAction> GetSkills(string typeName)
    {
        EnsureLoaded();
        return _unitSkills.TryGetValue(typeName, out var s) ? s : new List<SkillAction>();
    }

    public static int GetEngageDistance(string typeName, int defaultDist)
    {
        EnsureLoaded();
        return _units.TryGetValue(typeName, out var d) ? d.engageDistance : defaultDist;
    }

    public static void ApplyStatsTo(Unit unit)
    {
        EnsureLoaded();
        var d = GetUnitData(unit.unitType.typeName);
        if (d == null)
        {
            Debug.LogWarning($"[GameDataLoader] 유닛 데이터 없음: {unit.unitType.typeName}");
            return;
        }

        var s = d.stats;
        unit.maxHp = s.maxHp; unit.hp = s.maxHp;
        unit.maxMp = s.maxMp; unit.mp = s.maxMp;
        unit.physicalAttack    = s.physicalAttack;
        unit.magicalAttack     = s.magicalAttack;
        unit.physicalDefense   = s.physicalDefense;
        unit.magicalDefense    = s.magicalDefense;
        unit.HPRegen           = s.HPRegen;
        unit.attackspeed       = s.attackspeed;
        unit.walkSpeed         = s.walkSpeed;
        unit.reaction          = s.reaction;
        unit.criticalChance    = s.criticalChance;
        unit.cooltimeReduction = s.cooltimeReduction;
        unit.statusResistance  = s.statusResistance;
        unit.maxMental         = s.maxMental;
        unit.mental            = s.mental;
        unit.spotting          = s.spotting;
        unit.leadershipRange   = s.leadershipRange;
        unit.charisma          = s.charisma;
        unit.physicalAttackSpeed = s.physicalAttackSpeed;
        unit.magicalCastSpeed    = s.magicalCastSpeed;

        if (d.footprint != null && d.footprint.Length >= 2)
            unit.unitType.footprint = new Vector2(d.footprint[0], d.footprint[1]);
    }
}
