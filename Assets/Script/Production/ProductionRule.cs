using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ResourceCost
{
	public ResourceType resourceType;
	public int amount;
}

public class ProductionRule : ScriptableObject
{
	[Header("Rule Info")]
	public string ruleId;
	public string displayName;

	[Header("Cost")]
	public List<ResourceCost> costs = new List<ResourceCost>();

	[Header("Production Time (Seconds)")]
	public float productionTime = 5f;

	[Header("Target Result")]
	[Tooltip("생산될 유닛의 TypeName (예: MeleeTank 등)")]
	public string targetUnitTypeName;

	[Header("Economy Spec v0.2 Metadata")]
	public int populationCost = 1;
	public Vector2Int footprint = Vector2Int.one;
	public int dedicatedFloor = -1; // -1이면 모든 층 허용
	public bool requiresSpeciesUnlock;
	public bool isNamed;

	public UnitProductionSpec ToSpec()
	{
		double lifeCost = 0.0;
		double soulCost = 0.0;
		if (costs != null)
		{
			for (int i = 0; i < costs.Count; i++)
			{
				if (costs[i].resourceType == ResourceType.LifeEnergy)
					lifeCost += costs[i].amount;
				else if (costs[i].resourceType == ResourceType.SoulShard)
					soulCost += costs[i].amount;
			}
		}

		return new UnitProductionSpec
		{
			RuleId = string.IsNullOrEmpty(ruleId) ? targetUnitTypeName : ruleId,
			DisplayName = string.IsNullOrEmpty(displayName) ? targetUnitTypeName : displayName,
			TargetUnitTypeName = targetUnitTypeName,
			BaseLifeEnergyCost = lifeCost,
			BaseSoulShardCost = soulCost,
			BaseProductionSeconds = Mathf.Max(0.1f, productionTime),
			PopulationCost = Mathf.Max(1, populationCost),
			Footprint = new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y)),
			DedicatedFloor = dedicatedFloor >= 1 ? dedicatedFloor : (int?)null,
			RequiresSpeciesUnlock = requiresSpeciesUnlock,
			IsNamed = isNamed,
		};
	}

	// 플레이어(몬스터 진영) 생산시설이 제공하는 기본 생산 규칙 목록(자리표시자 — 후속 유닛 통합 엑셀 연동 전 기본값).
	public static List<ProductionRule> CreateDefaultPlayerUnitRules()
	{
		var meleeTankRule = CreateInstance<ProductionRule>();
		meleeTankRule.ruleId = "unit_melee_tank";
		meleeTankRule.displayName = "몬스터(근접)";
		meleeTankRule.costs.Add(new ResourceCost
		{
			resourceType = ResourceType.LifeEnergy,
			amount = ResourceManager.UnitProductionLifeEnergyCost,
		});
		meleeTankRule.productionTime = 5f;
		meleeTankRule.targetUnitTypeName = "MeleeTank";
		meleeTankRule.populationCost = 1;
		meleeTankRule.footprint = Vector2Int.one;
		meleeTankRule.requiresSpeciesUnlock = false;

		// 야생에서 처치해 해금하는 종(체험판 난이도 곡선 D안, 2026-11-18 — 비용·시간은 자리표시자).
		// 해금 키는 WildMonsterBehavior가 기록하는 UnitType 클래스 이름과 같아야 한다.
		var goblinRule = CreateUnlockableRule("unit_goblin_hood", "고블린 후드(원거리)", "GoblinHoodA", 120, 6f);
		var gnoleRule = CreateUnlockableRule("unit_gnole", "놀(돌격)", "GnoleA", 110, 5f);

		return new List<ProductionRule> { meleeTankRule, goblinRule, gnoleRule };
	}

	private static ProductionRule CreateUnlockableRule(string ruleId, string displayName, string unitTypeName, int lifeCost, float seconds)
	{
		var rule = CreateInstance<ProductionRule>();
		rule.ruleId = ruleId;
		rule.displayName = displayName;
		rule.costs.Add(new ResourceCost { resourceType = ResourceType.LifeEnergy, amount = lifeCost });
		rule.productionTime = seconds;
		rule.targetUnitTypeName = unitTypeName;
		rule.populationCost = 1;
		rule.footprint = Vector2Int.one;
		rule.requiresSpeciesUnlock = true;
		return rule;
	}

	// 생산 규칙의 대상 이름 → 유닛 종류. 모르는 이름이면 null.
	public static UnitType CreateUnitType(string targetUnitTypeName)
	{
		switch (targetUnitTypeName)
		{
			case "MeleeTank": return new MeleeTank();
			case "GoblinHoodA": return new GoblinHoodA();
			case "GnoleA": return new GnoleA();
			default: return null;
		}
	}
}
