using UnityEngine;

public class SkillAction_KnightFocusedStab : SkillAction
{
	public SkillAction_KnightFocusedStab() { SkillName = "집중 찌르기"; }

	public override bool IsAvailable(Unit unit) => unit.skillCooldowns[3] <= 0f;

	public override float GetPriority(Unit unit, Unit target, float minDist)
	{
		float priority = 70f;
		if (unit.physicalAttack * 1.25f >= target.hp) priority += 20f;
		if (minDist <= 2f) priority += 10f;
		return priority;
	}

	public override void Execute(Unit unit, Unit target, float minDist)
	{
		float finalDelayMs = Mathf.Max(200f, 800f * (100f / Mathf.Max(1f, unit.attackspeed)));
		Hitbox box         = BuildLineHitbox(unit, 2);

		var threat = ThreatTileData.Create();
		threat.shape = ThreatShape.LINE;
		threat.range = 2;

		BeginAttackCast(
			unit, finalDelayMs, threat,
			() =>
			{
				DamageEnemiesInHitboxWithAreaRatio(unit, box, 1.25f);
				Debug.Log($"{unit.unitType.typeName} 집중 찌르기");
			},
			() => unit.skillCooldowns[3] = ApplyCooldown(unit, 8f)
		);
	}
}

public class SkillAction_KnightShieldBash : SkillAction
{
	public SkillAction_KnightShieldBash() { SkillName = "방패 타격"; }

	public override bool IsAvailable(Unit unit) => unit.skillCooldowns[2] <= 0f;

	public override float GetPriority(Unit unit, Unit target, float minDist)
	{
		float priority = 55f;
		if (unit.physicalAttack * 0.9f >= target.hp) priority += 20f;
		if (minDist <= 1.5f) priority += 10f;
		return priority;
	}

	public override void Execute(Unit unit, Unit target, float minDist)
	{
		float finalDelayMs = Mathf.Max(200f, 650f * (100f / Mathf.Max(1f, unit.attackspeed)));
		Hitbox box         = BuildLineHitbox(unit, 1);

		var threat = ThreatTileData.Create();
		threat.shape = ThreatShape.LINE;
		threat.range = 1;

		BeginAttackCast(
			unit, finalDelayMs, threat,
			() =>
			{
				DamageEnemiesInHitboxWithAreaRatio(unit, box, 0.9f, true, 1f);
				Debug.Log($"{unit.unitType.typeName} 방패 타격");
			},
			() => unit.skillCooldowns[2] = ApplyCooldown(unit, 6f)
		);
	}
}

public class SkillAction_KnightFrontSlash : SkillAction
{
	public SkillAction_KnightFrontSlash() { SkillName = "전방 베기"; }

	public override bool IsAvailable(Unit unit) => unit.skillCooldowns[0] <= 0f;

	public override float GetPriority(Unit unit, Unit target, float minDist)
	{
		float priority = 40f;
		if (unit.physicalAttack >= target.hp) priority += 20f;
		if (minDist <= 1.5f) priority += 10f;
		return priority;
	}

	public override void Execute(Unit unit, Unit target, float minDist)
	{
		float finalDelayMs = Mathf.Max(200f, 450f * (100f / Mathf.Max(1f, unit.attackspeed)));
		Hitbox box         = BuildLineHitbox(unit, 1);

		var threat = ThreatTileData.Create();
		threat.shape = ThreatShape.LINE;
		threat.range = 1;

		BeginAttackCast(
			unit, finalDelayMs, threat,
			() =>
			{
				DamageEnemiesInHitboxWithAreaRatio(unit, box, 1f);
				Debug.Log($"{unit.unitType.typeName} 전방 베기");
			},
			() => unit.skillCooldowns[0] = ApplyCooldown(unit, 1.2f)
		);
	}
}
