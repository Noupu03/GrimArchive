using System.Collections.Generic;
using UnityEngine;

public abstract class SkillAction
{
    public string SkillName { get; protected set; }

    public abstract bool IsAvailable(Unit unit);
    public abstract float GetPriority(Unit unit, Unit target, float minDist);
    public abstract void Execute(Unit unit, Unit target, float minDist);

    public static List<Vector2Int> GetLineTiles(
        Unit unit,
        int range
    )
    {
        List<Vector2Int> tiles = new List<Vector2Int>();

        Vector2Int dir = unit.GetDirVector(unit.currentDir);

        Vector2Int current = unit.position;

        for (int i = 1; i <= range; i++)
        {
            current += dir;

            tiles.Add(current);
        }

        return tiles;
    }


	public static void BeginAttackCast(
	Unit unit,
	float castMs,
	ThreatTileData threat,
	System.Action attackAction,
	System.Action cooldownAction = null,
	System.Action effectAction = null
)
	{
		unit.isCastingAttack = true;
		unit.castTimer = castMs / 1000f;

		// 방향 고정 (여기가 중요)
		threat.dir = unit.currentDir;
		threat.forward = unit.GetDirVector(unit.currentDir);
		threat.right = GetRightVector(threat.forward);

		// hitbox 생성
		if (threat.shape == ThreatShape.LINE)
			threat.hitbox = BuildLineHitbox(unit, threat.range);

		else if (threat.shape == ThreatShape.RECT)
			threat.hitbox = BuildRectHitbox(unit, threat.width, threat.depth);

		unit.currentThreat = threat;

		unit.pendingAttack = () =>
		{
			try
			{
				effectAction?.Invoke();
				attackAction?.Invoke();
			}
			finally
			{
				cooldownAction?.Invoke();

				// ❗ 여기 중요: 반드시 완전 초기화
				unit.currentThreat = null;
				unit.isCastingAttack = false;
				unit.pendingAttack = null;
				unit.castTimer = 0f;
			}
		};
	}

	public static List<Unit> GetEnemiesInTiles(Unit attacker, List<Vector2Int> tiles)//
    {
        List<Unit> result = new List<Unit>();

        foreach (Unit u in GameSession.Instance.units)
        {
            if (u == null) continue;
            if (u == attacker) continue;
            if (u.hp <= 0) continue;

            if (u.currentFloor != attacker.currentFloor)
                continue;

            bool isEnemy =
                (attacker is Human && u is Monster) ||
                (attacker is Monster && u is Human);

            if (!isEnemy)
                continue;

            int w = (int)u.unitType.footprint.x;
            int h = (int)u.unitType.footprint.y;

            for (int dx = 0; dx < w; dx++)
            {
                for (int dy = 0; dy < h; dy++)
                {
                    Vector2Int p = new Vector2Int(u.position.x + dx, u.position.y + dy);

                    if (tiles.Contains(p))
                    {
                        result.Add(u);

                        dx = w;
                        break;
                    }
                }
            }
        }

        return result;
    }
	public static List<Unit> GetEnemiesInHitbox(Unit attacker, Hitbox box)
	{
		List<Unit> result = new();

		foreach (var u in GameSession.Instance.units)
		{
			if (u == null || u == attacker || u.hp <= 0) continue;
			if (u.currentFloor != attacker.currentFloor) continue;

			bool isEnemy =
				(attacker is Human && u is Monster) ||
				(attacker is Monster && u is Human);

			if (!isEnemy) continue;

			Hitbox targetBox = GetUnitHitbox(u);

			if (box.Overlaps(targetBox))
				result.Add(u);
		}

		return result;
	}
	public static Hitbox GetUnitHitbox(Unit u)
	{
		Vector2 size = new Vector2(
			u.unitType.footprint.x,
			u.unitType.footprint.y
		);

		Vector2 center =
			(Vector2)u.position + size * 0.5f;

		return new Hitbox
		{
			center = center,
			size = size
		};
	}
	public static void DamageEnemiesInHitbox(
	Unit attacker,
	Hitbox box,
	float multiplier,
	bool stun = false,
	float stunDuration = 0f
)
	{
		var targets = GetEnemiesInHitbox(attacker, box);

		foreach (var t in targets)
		{
			t.TakePhysicalDamage(attacker.physicalAttack * multiplier, attacker);

			if (stun)
				t.ApplyStun(stunDuration);
		}
	}
	
	public static Hitbox BuildLineHitbox(Unit unit, int range)
	{
		Vector2 dir = unit.GetDirVector(unit.currentDir);

		Vector2 center =
			(Vector2)unit.position +
			dir * (range * 0.5f + 0.5f);

		Vector2 size =
			Mathf.Abs(dir.x) > 0
			? new Vector2(range, 1)
			: new Vector2(1, range);

		return new Hitbox
		{
			center = center,
			size = size
		};
	}
	public static Hitbox BuildRectHitbox(Unit unit, int width, int depth)
	{
		Vector2 forward = unit.GetDirVector(unit.currentDir);

		Vector2 center =
			(Vector2)unit.position +
			forward * (depth * 0.5f + 0.5f);

		return new Hitbox
		{
			center = center,
			size = new Vector2(width, depth)
		};
	}

    public static Dir GetDirection8(Vector2Int diff)
    {
        if (diff == Vector2Int.zero)
            return Dir.DOWN;

        int x = diff.x;
        int y = diff.y;

        // 대각선
        if (x > 0 && y > 0)
            return Dir.UP_RIGHT;

        if (x > 0 && y < 0)
            return Dir.DOWN_RIGHT;

        if (x < 0 && y > 0)
            return Dir.UP_LEFT;

        if (x < 0 && y < 0)
            return Dir.DOWN_LEFT;

        // 직선
        if (x > 0)
            return Dir.RIGHT;

        if (x < 0)
            return Dir.LEFT;

        if (y > 0)
            return Dir.UP;

        return Dir.DOWN;
    }

    public static Vector2Int GetRightVector(Vector2Int forward)
    {
        // 직선 방향
        if (forward == Vector2Int.up)
            return Vector2Int.right;

        if (forward == Vector2Int.down)
            return Vector2Int.left;

        if (forward == Vector2Int.right)
            return Vector2Int.down;

        if (forward == Vector2Int.left)
            return Vector2Int.up;

        // 대각 방향
        if (forward == new Vector2Int(1, 1))
            return new Vector2Int(1, -1);

        if (forward == new Vector2Int(1, -1))
            return new Vector2Int(-1, -1);

        if (forward == new Vector2Int(-1, -1))
            return new Vector2Int(-1, 1);

        if (forward == new Vector2Int(-1, 1))
            return new Vector2Int(1, 1);

        return Vector2Int.right;
    }

    public static float GetCompression(Unit unit)
    {
        bool isDiagonal =
            unit.currentDir == Dir.UP_RIGHT ||
            unit.currentDir == Dir.UP_LEFT ||
            unit.currentDir == Dir.DOWN_RIGHT ||
            unit.currentDir == Dir.DOWN_LEFT;

        return isDiagonal ? 0.75f : 1f;
    }

    public static float ApplyCooldown(Unit unit, float baseCd)
    {
        float reduction = Mathf.Min(50f, unit.cooltimeReduction);
        return baseCd * (1f - reduction / 100f);
    }
}

public class SkillAction_KnightFocusedStab : SkillAction
{
	public SkillAction_KnightFocusedStab()
	{
		SkillName = "집중 찌르기";
	}

	public override bool IsAvailable(Unit unit) => unit.skillCooldowns[3] <= 0f;

	public override float GetPriority(Unit unit, Unit target, float minDist)
	{
		float expectedDamage = unit.physicalAttack * 1.25f;
		float priority = 70f;

		if (expectedDamage >= target.hp) priority += 20f;
		if (minDist <= 2f) priority += 10f;

		return priority;
	}

	public override void Execute(Unit unit, Unit target, float minDist)
	{
		float finalDelayMs =
			Mathf.Max(200f,
			800f * (100f / Mathf.Max(1f, unit.attackspeed)));

		Hitbox box = BuildLineHitbox(unit, 2);

		var threat = ThreatTileData.Create();
		threat.shape = ThreatShape.LINE;
		threat.range = 2;

		BeginAttackCast(
			unit,
			finalDelayMs,
			threat,
			() =>
			{
				DamageEnemiesInHitbox(unit, box, 1.25f);
				Debug.Log($"{unit.unitType.typeName} 집중 찌르기");
			},
			() =>
			{
				unit.skillCooldowns[3] = ApplyCooldown(unit, 8f);
			}
		);
	}
}

	public class SkillAction_KnightShieldBash : SkillAction
{
	public SkillAction_KnightShieldBash()
	{
		SkillName = "방패 타격";
	}

	public override bool IsAvailable(Unit unit) => unit.skillCooldowns[2] <= 0f;

	public override float GetPriority(Unit unit, Unit target, float minDist)
	{
		float expectedDamage = unit.physicalAttack * 0.9f;
		float priority = 55f;

		if (expectedDamage >= target.hp) priority += 20f;
		if (minDist <= 1.5f) priority += 10f;

		return priority;
	}

	public override void Execute(Unit unit, Unit target, float minDist)
	{
		float finalDelayMs =
			Mathf.Max(200f,
			650f * (100f / Mathf.Max(1f, unit.attackspeed)));

		Hitbox box = BuildLineHitbox(unit, 1);

		var threat = ThreatTileData.Create();
		threat.shape = ThreatShape.LINE;
		threat.range = 1;

		BeginAttackCast(
			unit,
			finalDelayMs,
			threat,

			() =>
			{
				DamageEnemiesInHitbox(unit, box, 0.9f, true, 1f);
				Debug.Log($"{unit.unitType.typeName} 방패 타격");
			},

			() =>
			{
				unit.skillCooldowns[2] = ApplyCooldown(unit, 6f);
			}
		);
	}
}

public class SkillAction_KnightFrontSlash : SkillAction
{
	public SkillAction_KnightFrontSlash()
	{
		SkillName = "전방 베기";
	}

	public override bool IsAvailable(Unit unit) => unit.skillCooldowns[0] <= 0f;

	public override float GetPriority(Unit unit, Unit target, float minDist)
	{
		float expectedDamage = unit.physicalAttack;
		float priority = 40f;

		if (expectedDamage >= target.hp) priority += 20f;
		if (minDist <= 1.5f) priority += 10f;

		return priority;
	}

	public override void Execute(Unit unit, Unit target, float minDist)
	{
		float finalDelayMs =
			Mathf.Max(200f,
			450f * (100f / Mathf.Max(1f, unit.attackspeed)));

		Hitbox box = BuildLineHitbox(unit, 1);

		var threat = ThreatTileData.Create();
		threat.shape = ThreatShape.LINE;
		threat.range = 1;

		BeginAttackCast(
			unit,
			finalDelayMs,
			threat,

			() =>
			{
				DamageEnemiesInHitbox(unit, box, 1f);
				Debug.Log($"{unit.unitType.typeName} 전방 베기");
			},

			() =>
			{
				unit.skillCooldowns[0] = ApplyCooldown(unit, 1.2f);
			}
		);
	}
}

public class SkillAction_MeleeTankHeavySmash : SkillAction
{
	public SkillAction_MeleeTankHeavySmash()
	{
		SkillName = "육중한 내리찍기";
	}

	public override bool IsAvailable(Unit unit) => unit.skillCooldowns[3] <= 0f;

	public override float GetPriority(Unit unit, Unit target, float minDist)
	{
		float expectedDamage = unit.physicalAttack * 1.45f;
		float priority = 75f;

		if (expectedDamage >= target.hp) priority += 20f;
		if (minDist <= 2.5f) priority += 10f;

		return priority;
	}

	public override void Execute(Unit unit, Unit target, float minDist)
	{
		float finalDelayMs =
			Mathf.Max(200f,
			1000f * (100f / Mathf.Max(1f, unit.attackspeed)));

		Hitbox box = BuildRectHitbox(unit, 2, 2);

		var threat = ThreatTileData.Create();
		threat.shape = ThreatShape.RECT;
		threat.width = 2;
		threat.depth = 2;

		BeginAttackCast(
			unit,
			finalDelayMs,
			threat,

			() =>
			{
				DamageEnemiesInHitbox(unit, box, 1.45f);
				Debug.Log($"{unit.unitType.typeName} 육중한 내리찍기");
			},

			() =>
			{
				unit.skillCooldowns[3] = ApplyCooldown(unit, 7f);
			}
		);
	}
}

public class SkillAction_MeleeTankAmbushClaw : SkillAction
{
	public SkillAction_MeleeTankAmbushClaw()
	{
		SkillName = "급습 할퀴기";
	}

	public override bool IsAvailable(Unit unit) => unit.skillCooldowns[2] <= 0f;

	public override float GetPriority(Unit unit, Unit target, float minDist)
	{
		float expectedDamage = unit.physicalAttack * 0.65f;
		float priority = 60f;

		if (expectedDamage >= target.hp) priority += 20f;
		if (minDist <= 1.5f) priority += 10f;

		return priority;
	}

	public override void Execute(Unit unit, Unit target, float minDist)
	{
		float finalDelayMs =
			Mathf.Max(200f,
			240f * (100f / Mathf.Max(1f, unit.attackspeed)));

		Hitbox box = BuildLineHitbox(unit, 1);

		var threat = ThreatTileData.Create();
		threat.shape = ThreatShape.LINE;
		threat.range = 1;

		BeginAttackCast(
			unit,
			finalDelayMs,
			threat,

			() =>
			{
				DamageEnemiesInHitbox(unit, box, 0.65f);
				Debug.Log($"{unit.unitType.typeName} 급습 할퀴기");
			},

			() =>
			{
				unit.skillCooldowns[2] = ApplyCooldown(unit, 4f);
			}
		);
	}
}

public class SkillAction_MeleeTankClawSwipe : SkillAction
{
	public SkillAction_MeleeTankClawSwipe()
	{
		SkillName = "발톱 후려치기";
	}

	public override bool IsAvailable(Unit unit) => unit.skillCooldowns[1] <= 0f;

	public override float GetPriority(Unit unit, Unit target, float minDist)
	{
		float expectedDamage = unit.physicalAttack;
		float priority = 50f;

		if (expectedDamage >= target.hp) priority += 20f;
		if (minDist <= 3f) priority += 10f;

		return priority;
	}

	public override void Execute(Unit unit, Unit target, float minDist)
	{
		float finalDelayMs =
			Mathf.Max(200f,
			650f * (100f / Mathf.Max(1f, unit.attackspeed)));

		Hitbox box = BuildLineHitbox(unit, 3);

		var threat = ThreatTileData.Create();
		threat.shape = ThreatShape.LINE;
		threat.range = 3;

		BeginAttackCast(
			unit,
			finalDelayMs,
			threat,

			() =>
			{
				DamageEnemiesInHitbox(unit, box, 1f);
				Debug.Log($"{unit.unitType.typeName} 발톱 후려치기");
			},

			() =>
			{
				unit.skillCooldowns[1] = ApplyCooldown(unit, 1.4f);
			}
		);
	}
}
