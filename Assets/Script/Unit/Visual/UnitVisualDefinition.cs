using System.Collections.Generic;
using UnityEngine;

// 유닛 타입별 프리팹의 루트에 붙는 컴포넌트.
// 스탯/스킬/이펙트를 인스펙터에서 직접 편집한다 (스프라이트/애니메이션은 프리팹 자식 계층의
// SpriteLibrary/SpriteResolver/UnitAnimationController 컴포넌트에서 직접 편집).
public class UnitVisualDefinition : MonoBehaviour
{
    [Header("유닛 식별")]
    public string unitTypeName;
    public Vector2 footprint = Vector2.one;
    public int engageDistance = 2;

    [Header("스탯")]
    public UnitStatsData stats = new UnitStatsData();

    [Header("스킬")]
    public List<SkillData> skills = new List<SkillData>();

    [Header("이펙트")]
    public GameObject hitSparkPrefab;
    public GameObject guardPrefab;
    public GameObject parryPrefab;
    public GameObject attackFailPrefab;

    public void ApplyStatsTo(Unit unit)
    {
        if (unit == null) return;

        unit.maxHp = stats.maxHp; unit.hp = stats.maxHp;
        unit.maxMp = stats.maxMp; unit.mp = stats.maxMp;
        unit.physicalAttack      = stats.physicalAttack;
        unit.magicalAttack       = stats.magicalAttack;
        unit.physicalDefense     = stats.physicalDefense;
        unit.magicalDefense      = stats.magicalDefense;
        unit.HPRegen             = stats.HPRegen;
        unit.attackspeed         = stats.attackspeed;
        unit.walkSpeed           = stats.walkSpeed;
        unit.reaction            = stats.reaction;
        unit.criticalChance      = stats.criticalChance;
        unit.cooltimeReduction   = stats.cooltimeReduction;
        unit.statusResistance    = stats.statusResistance;
        unit.maxMental           = stats.maxMental;
        unit.mental              = stats.mental;
        unit.spotting            = stats.spotting;
        unit.leadershipRange     = stats.leadershipRange;
        unit.charisma            = stats.charisma;
        unit.physicalAttackSpeed = stats.physicalAttackSpeed;
        unit.magicalCastSpeed    = stats.magicalCastSpeed;

        if (unit.unitType != null) unit.unitType.footprint = footprint;
    }

    public List<SkillAction> BuildSkillActions()
    {
        var list = new List<SkillAction>();
        foreach (var sd in skills)
        {
            if (sd.isProjectile)
                list.Add(new SkillAction_Projectile(sd, sd.projectilePrefab)); // 설정된 프리팹 전달
            else
                list.Add(new SkillAction_Generic(sd));
        }
        return list;
    }

#if UNITY_EDITOR
    [System.Serializable]
    private class UnitsJsonWrapper { public List<UnitJsonNode> units; }
    [System.Serializable]
    private class UnitJsonNode {
        public string typeName;
        public int[] footprint;
        public int engageDistance;
        public List<string> skills;
        public UnitStatsData stats;
    }
    [System.Serializable]
    private class SkillsJsonWrapper { public List<SkillData> skills; }

    [ContextMenu("Load Data From JSON (units.json / skills.json)")]
    public void LoadDataFromJson()
    {
        if (string.IsNullOrEmpty(unitTypeName))
        {
            Debug.LogError("Unit Type Name이 없습니다. (예: 아처형)");
            return;
        }

        string unitsPath = System.IO.Path.Combine(Application.dataPath, "Data", "units.json");
        string skillsPath = System.IO.Path.Combine(Application.dataPath, "Data", "skills.json");

        if (!System.IO.File.Exists(unitsPath) || !System.IO.File.Exists(skillsPath))
        {
            Debug.LogError("Data 폴더에 units.json 또는 skills.json이 없습니다.");
            return;
        }

        string unitsJson = System.IO.File.ReadAllText(unitsPath);
        string skillsJson = System.IO.File.ReadAllText(skillsPath);

        UnitsJsonWrapper unitsData = JsonUtility.FromJson<UnitsJsonWrapper>(unitsJson);
        SkillsJsonWrapper skillsData = JsonUtility.FromJson<SkillsJsonWrapper>(skillsJson);

        if (unitsData == null || unitsData.units == null) return;

        UnitJsonNode targetNode = null;
        foreach (var node in unitsData.units)
        {
            if (node.typeName == this.unitTypeName)
            {
                targetNode = node;
                break;
            }
        }

        if (targetNode == null)
        {
            Debug.LogError($"{unitTypeName} 데이터를 units.json에서 찾을 수 없습니다.");
            return;
        }

        // 스탯 및 기본 정보 덮어쓰기
        if (targetNode.footprint != null && targetNode.footprint.Length >= 2)
            this.footprint = new Vector2(targetNode.footprint[0], targetNode.footprint[1]);
        this.engageDistance = targetNode.engageDistance;
        this.stats = targetNode.stats;

        // 스킬 연결
        this.skills.Clear();
        if (targetNode.skills != null && skillsData != null && skillsData.skills != null)
        {
            foreach (var skillName in targetNode.skills)
            {
                foreach (var sd in skillsData.skills)
                {
                    if (sd.skillName == skillName)
                    {
                        this.skills.Add(sd);
                        break;
                    }
                }
            }
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[{unitTypeName}] 데이터 JSON 불러오기 완료!");
    }
#endif
}
