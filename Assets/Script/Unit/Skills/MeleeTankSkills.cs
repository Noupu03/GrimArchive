using UnityEngine;

public class SkillAction_MeleeTankHeavySmash : SkillAction
{
	public SkillAction_MeleeTankHeavySmash() { SkillName = "육중한 내리찍기"; }

	public override bool IsAvailable(Unit unit) => unit.skillCooldowns[3] <= 0f;

	public override float GetPriority(Unit unit, Unit target, float minDist)
	{
		float priority = 75f;
		if (unit.physicalAttack * 1.45f >= target.hp) priority += 20f;
		if (minDist <= 2.5f) priority += 10f;
		return priority;
	}

	public override void Execute(Unit unit, Unit target, float minDist)
	{
		float finalDelayMs = Mathf.Max(200f, 1000f * (100f / Mathf.Max(1f, unit.attackspeed)));
		Hitbox box         = BuildRectHitbox(unit, 2, 2);

		var threat = ThreatTileData.Create();
		threat.shape = ThreatShape.RECT;
		threat.width = 2;
		threat.depth = 2;

		BeginAttackCast(
			unit, finalDelayMs, threat,
			() =>
			{
				DamageEnemiesInHitboxWithAreaRatio(unit, box, 1.45f);
				Debug.Log($"{unit.unitType.typeName} 육중한 내리찍기");
			},
			() => unit.skillCooldowns[3] = ApplyCooldown(unit, 7f)
		);
	}
}

public class SkillAction_MeleeTankAmbushClaw : SkillAction
{
	public SkillAction_MeleeTankAmbushClaw() { SkillName = "급습 할퀴기"; }

	public override bool IsAvailable(Unit unit) => unit.skillCooldowns[2] <= 0f;

	public override float GetPriority(Unit unit, Unit target, float minDist)
	{
		float priority = 60f;
		if (unit.physicalAttack * 0.65f >= target.hp) priority += 20f;
		if (minDist <= 1.5f) priority += 10f;
		return priority;
	}

	public override void Execute(Unit unit, Unit target, float minDist)
	{
		float finalDelayMs = Mathf.Max(200f, 240f * (100f / Mathf.Max(1f, unit.attackspeed)));
		Hitbox box         = BuildLineHitbox(unit, 1);

		var threat = ThreatTileData.Create();
		threat.shape = ThreatShape.LINE;
		threat.range = 1;

		BeginAttackCast(
			unit, finalDelayMs, threat,
			() =>
			{
				DamageEnemiesInHitboxWithAreaRatio(unit, box, 0.65f);
				Debug.Log($"{unit.unitType.typeName} 급습 할퀴기");
			},
			() => unit.skillCooldowns[2] = ApplyCooldown(unit, 4f)
		);
	}
}

public class SkillAction_MeleeTankClawSwipe : SkillAction
{
	public SkillAction_MeleeTankClawSwipe() { SkillName = "발톱 후려치기"; }

	public override bool IsAvailable(Unit unit) => unit.skillCooldowns[1] <= 0f;

	public override float GetPriority(Unit unit, Unit target, float minDist)
	{
		float priority = 50f;
		if (unit.physicalAttack >= target.hp) priority += 20f;
		if (minDist <= 3f) priority += 10f;
		return priority;
	}

	public override void Execute(Unit unit, Unit target, float minDist)
	{
		float finalDelayMs = Mathf.Max(200f, 650f * (100f / Mathf.Max(1f, unit.attackspeed)));
		Hitbox box         = BuildLineHitbox(unit, 3);

		var threat = ThreatTileData.Create();
		threat.shape = ThreatShape.LINE;
		threat.range = 3;

		BeginAttackCast(
			unit, finalDelayMs, threat,
			() =>
			{
				DamageEnemiesInHitboxWithAreaRatio(unit, box, 1f);
				Debug.Log($"{unit.unitType.typeName} 발톱 후려치기");
			},
			() => unit.skillCooldowns[1] = ApplyCooldown(unit, 1.4f)
		);
	}
}
