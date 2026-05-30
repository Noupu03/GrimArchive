using UnityEngine;

public class SkillAction_MeleeTankHeavySmash : SkillAction
{
	public SkillAction_MeleeTankHeavySmash() { SkillName = "육중한 내리찍기"; }

	public override float       DefaultBaseDelayMs  => 1000f;
	public override float       DefaultBaseCooldown => 7f;
	public override ThreatShape HitShape            => ThreatShape.RECT;
	public override int         HitWidth            => 2;
	public override int         HitDepth            => 2;

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
		float baseMs       = SkillTuningOverride.GetDelayMs(SkillName, DefaultBaseDelayMs);
		float finalDelayMs = Mathf.Max(200f, baseMs * (100f / Mathf.Max(1f, unit.attackspeed)));

		var threat = ThreatTileData.Create();
		threat.shape = ThreatShape.RECT;
		threat.width = 3;
		threat.depth = 3;

		BeginAttackCast(
			unit, finalDelayMs, threat,
			() =>
			{
				DamageEnemiesInHitboxWithAreaRatio(unit, threat.hitbox, 1.45f);
				Debug.Log($"{unit.unitType.typeName} 육중한 내리찍기");
			},
			() => unit.skillCooldowns[3] = ApplyCooldown(unit, SkillTuningOverride.GetCooldown(SkillName, DefaultBaseCooldown))
		);
	}
}

public class SkillAction_MeleeTankAmbushClaw : SkillAction
{
	public SkillAction_MeleeTankAmbushClaw() { SkillName = "급습 할퀴기"; }

	public override float DefaultBaseDelayMs  => 240f;
	public override float DefaultBaseCooldown => 4f;

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
		float baseMs       = SkillTuningOverride.GetDelayMs(SkillName, DefaultBaseDelayMs);
		float finalDelayMs = Mathf.Max(200f, baseMs * (100f / Mathf.Max(1f, unit.attackspeed)));

		var threat = ThreatTileData.Create();
		threat.shape = ThreatShape.LINE;
		threat.range = 1;

		BeginAttackCast(
			unit, finalDelayMs, threat,
			() =>
			{
				DamageEnemiesInHitboxWithAreaRatio(unit, threat.hitbox, 0.65f);
				Debug.Log($"{unit.unitType.typeName} 급습 할퀴기");
			},
			() => unit.skillCooldowns[2] = ApplyCooldown(unit, SkillTuningOverride.GetCooldown(SkillName, DefaultBaseCooldown))
		);
	}
}

public class SkillAction_MeleeTankClawSwipe : SkillAction
{
	public SkillAction_MeleeTankClawSwipe() { SkillName = "발톱 후려치기"; }

	public override float DefaultBaseDelayMs  => 650f;
	public override float DefaultBaseCooldown => 1.4f;
	public override int   HitRange            => 3;

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
		float baseMs       = SkillTuningOverride.GetDelayMs(SkillName, DefaultBaseDelayMs);
		float finalDelayMs = Mathf.Max(200f, baseMs * (100f / Mathf.Max(1f, unit.attackspeed)));

		var threat = ThreatTileData.Create();
		threat.shape = ThreatShape.LINE;
		threat.range = 3;

		BeginAttackCast(
			unit, finalDelayMs, threat,
			() =>
			{
				DamageEnemiesInHitboxWithAreaRatio(unit, threat.hitbox, 1f);
				Debug.Log($"{unit.unitType.typeName} 발톱 후려치기");
			},
			() => unit.skillCooldowns[1] = ApplyCooldown(unit, SkillTuningOverride.GetCooldown(SkillName, DefaultBaseCooldown))
		);
	}
}
