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
}
