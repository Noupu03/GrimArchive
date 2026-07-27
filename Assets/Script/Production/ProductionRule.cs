using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ResourceCost
{
    public ResourceType resourceType;
    public int amount;
}

[CreateAssetMenu(fileName = "NewProductionRule", menuName = "GrimArchive/Production Rule")]
public class ProductionRule : ScriptableObject
{
    [Header("Rule Info")]
    public string ruleId;
    public string displayName;

    [Header("Cost")]
    public List<ResourceCost> costs = new List<ResourceCost>();

    [Header("Production Time (Seconds)")]
    public float productionTime = 1f;

    [Header("Target Result")]
    [Tooltip("생산될 유닛의 TypeName (예: Knight, Archer, MeleeTank 등)")]
    public string targetUnitTypeName;

    // 건축물·자원·유닛 생산 MVP(2026-07-27) — 플레이어(몬스터 진영) 생산 건물이 제공하는 기본 생산
    // 규칙 목록. B키 배치(InputManager)와 게임 시작 시 자동 배치(GameSession) 둘 다 이 팩토리를 써서
    // 두 경로의 값이 어긋나지 않게 한다. Knight/Archer는 Human 기본 진영(HumanFactionBehavior=적)이라
    // 플레이어 생산 건물에서 뽑으면 안 되고, 지금 코드베이스에 있는 플레이어용 Monster UnitType은
    // MeleeTank뿐이라 우선 이것만 등록한다(BuildingManager.SpawnUnitFromBuilding과 동일 판단).
    public static List<ProductionRule> CreateDefaultPlayerUnitRules()
    {
        var meleeTankRule = CreateInstance<ProductionRule>();
        meleeTankRule.ruleId = "unit_melee_tank";
        meleeTankRule.displayName = "몬스터(근접)";
        meleeTankRule.costs.Add(new ResourceCost { resourceType = ResourceType.Wood, amount = ResourceManager.UnitProductionWoodCost });
        meleeTankRule.productionTime = 1f;
        meleeTankRule.targetUnitTypeName = "MeleeTank";

        return new List<ProductionRule> { meleeTankRule };
    }
}
