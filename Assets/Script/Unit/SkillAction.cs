using System.Collections.Generic;
using UnityEngine;

public abstract class SkillAction
{
	public string SkillName { get; protected set; }

	public abstract bool IsAvailable(Unit unit);
	public abstract float GetPriority(Unit unit, Unit target, float minDist);
	public abstract void Execute(Unit unit, Unit target, float minDist);


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

		// hitbox 생성 - 공격 시 자유로운 각도를 사용하여 생성
		if (threat.shape == ThreatShape.LINE)
			threat.hitbox = BuildLineHitboxWithAngle(unit, threat.range, unit.currentAttackAngle);

		else if (threat.shape == ThreatShape.RECT)
			threat.hitbox = BuildRectHitboxWithAngle(unit, threat.width, threat.depth, unit.currentAttackAngle);

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
	public static void DamageEnemiesInHitbox(Unit attacker,Hitbox box,float multiplier,bool stun = false,float stunDuration = 0f)
	{
		var targets = GetEnemiesInHitbox(attacker, box);

		foreach (var t in targets)
		{
			t.TakePhysicalDamage(attacker.physicalAttack * multiplier, attacker);

			if (stun)
				t.ApplyStun(stunDuration);
		}
	}

	/// <summary>
	/// 히트박스와의 교차 면적 비율에 따라 데미지를 적용합니다.
	/// 면적이 많이 겹칠수록 더 많은 데미지를 입습니다.
	/// </summary>
	public static void DamageEnemiesInHitboxWithAreaRatio(Unit attacker, Hitbox attackBox, float multiplier, bool stun = false, float stunDuration = 0f)
	{
		var targets = GetEnemiesInHitbox(attacker, attackBox);

		foreach (var t in targets)
		{
			// 공격 히트박스와 대상의 히트박스 교차 비율 계산
			Hitbox targetBox = GetUnitHitbox(t);
			float overlapRatio = attackBox.CalculateOverlapRatio(targetBox);

			// 교차 비율에 따라 데미지 조정 (최소 0.1배)
			float adjustedMultiplier = Mathf.Max(0.1f, multiplier * overlapRatio);

			float baseDamage = attacker.physicalAttack * adjustedMultiplier;

			// 최소 데미지 1 보장
			float finalDamage = Mathf.Max(1f, baseDamage);

			t.TakePhysicalDamage(finalDamage, attacker);

			if (stun)
				t.ApplyStun(stunDuration);

			// 로깅: 개발용 (필요시 제거)
			// Debug.Log($"{attacker.unitType.typeName} → {t.unitType.typeName}: 교차비율={overlapRatio:P0}, 데미지={finalDamage:F1}");
		}
	}

	/// <summary>
	/// 마법 데미지 버전 - 히트박스 교차 면적 비율 적용
	/// </summary>
	public static void DamageMagicalEnemiesInHitboxWithAreaRatio(Unit attacker, Hitbox attackBox, float multiplier, bool stun = false, float stunDuration = 0f)
	{
		var targets = GetEnemiesInHitbox(attacker, attackBox);

		foreach (var t in targets)
		{
			// 공격 히트박스와 대상의 히트박스 교차 비율 계산
			Hitbox targetBox = GetUnitHitbox(t);
			float overlapRatio = attackBox.CalculateOverlapRatio(targetBox);

			// 교차 비율에 따라 데미지 조정 (최소 0.1배)
			float adjustedMultiplier = Mathf.Max(0.1f, multiplier * overlapRatio);

			float baseDamage = attacker.magicalAttack * adjustedMultiplier;

			// 최소 데미지 1 보장
			float finalDamage = Mathf.Max(1f, baseDamage);

			t.TakeMagicalDamage(finalDamage, attacker);

			if (stun)
				t.ApplyStun(stunDuration);
		}
	}


	public static Hitbox BuildLineHitbox(Unit unit, int range)
	{
		Vector2 dir = unit.GetDirVector(unit.currentDir);

		// 유닛 중심 계산
		Vector2 unitCenter = (Vector2)unit.position + new Vector2(unit.unitType.footprint.x, unit.unitType.footprint.y) * 0.5f;

		// 유닛 중심에서 공격 범위로 뻗어나가는 offset (정규화 필요 - 대각선은 길이가 다름)
		Vector2 offset = dir.normalized * (range * 0.5f + 0.5f);

		Vector2 center = unitCenter + offset;

		// 크기와 회전 결정
		bool isHorizontal = Mathf.Abs(dir.x) > 0 && Mathf.Abs(dir.y) == 0;
		bool isVertical = Mathf.Abs(dir.y) > 0 && Mathf.Abs(dir.x) == 0;
		bool isDiagonal = !isHorizontal && !isVertical;

		Vector2 size;
		float rotation;

		if (isHorizontal)
		{
			size = new Vector2(range, 1);
			rotation = 0f;  // 회전 불필요
		}
		else if (isVertical)
		{
			size = new Vector2(1, range);
			rotation = 0f;  // 회전 불필요 (크기로 이미 조정됨)
		}
		else  // 대각선
		{
			size = new Vector2(range, 1);
			rotation = GetRotationForDirection(unit.currentDir);  // 회전 필요
		}

		return new Hitbox
		{
			center = center,
			size = size,
			rotation = rotation
		};
	}

	/// <summary>
	/// 주어진 각도 기반으로 라인 히트박스를 생성합니다 (공격 시 자유 각도 지원)
	/// </summary>
	public static Hitbox BuildLineHitboxWithAngle(Unit unit, int range, float angleRad)
	{
		Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

		// 유닛 중심 계산
		Vector2 unitCenter = (Vector2)unit.position + new Vector2(unit.unitType.footprint.x, unit.unitType.footprint.y) * 0.5f;

		// 유닛 중심에서 공격 범위로 뻗어나가는 offset
		Vector2 offset = dir * (range * 0.5f + 0.5f);

		Vector2 center = unitCenter + offset;

		// 라인의 길이와 회전 각도
		Vector2 size = new Vector2(range, 1);
		float rotationDeg = angleRad * Mathf.Rad2Deg;

		return new Hitbox
		{
			center = center,
			size = size,
			rotation = rotationDeg
		};
	}

	public static Hitbox BuildRectHitbox(Unit unit, int width, int depth)
	{
		Vector2 forward = unit.GetDirVector(unit.currentDir);

		// 유닛 중심 계산
		Vector2 unitCenter = (Vector2)unit.position + new Vector2(unit.unitType.footprint.x, unit.unitType.footprint.y) * 0.5f;

		// 유닛 중심에서 공격 범위로 뻗어나가는 offset (정규화 필요 - 대각선은 길이가 다름)
		Vector2 offset = forward.normalized * (depth * 0.5f + 0.5f);

		Vector2 center = unitCenter + offset;

		// 크기와 회전 결정
		bool isHorizontal = Mathf.Abs(forward.x) > 0 && Mathf.Abs(forward.y) == 0;
		bool isVertical = Mathf.Abs(forward.y) > 0 && Mathf.Abs(forward.x) == 0;
		bool isDiagonal = !isHorizontal && !isVertical;

		Vector2 size;
		float rotation;

		if (isHorizontal)
		{
			size = new Vector2(depth, width);
			rotation = 0f;  // 회전 불필요
		}
		else if (isVertical)
		{
			size = new Vector2(width, depth);
			rotation = 0f;  // 회전 불필요 (크기로 이미 조정됨)
		}
		else  // 대각선
		{
			size = new Vector2(width, depth);
			rotation = GetRotationForDirection(unit.currentDir);  // 회전 필요
		}

		return new Hitbox
		{
			center = center,
			size = size,
			rotation = rotation
		};
	}

	/// <summary>
	/// 주어진 각도 기반으로 직사각형 히트박스를 생성합니다 (공격 시 자유 각도 지원)
	/// </summary>
	public static Hitbox BuildRectHitboxWithAngle(Unit unit, int width, int depth, float angleRad)
	{
		Vector2 forward = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

		// 유닛 중심 계산
		Vector2 unitCenter = (Vector2)unit.position + new Vector2(unit.unitType.footprint.x, unit.unitType.footprint.y) * 0.5f;

		// 유닛 중심에서 공격 범위로 뻗어나가는 offset
		Vector2 offset = forward * (depth * 0.5f + 0.5f);

		Vector2 center = unitCenter + offset;

		// 직사각형 크기
		Vector2 size = new Vector2(depth, width);
		float rotationDeg = angleRad * Mathf.Rad2Deg;

		return new Hitbox
		{
			center = center,
			size = size,
			rotation = rotationDeg
		};
	}


	public static float GetRotationForDirection(Dir dir)
	{
		// 방향에 따른 회전 각도 반환 (도)
		return dir switch
		{
			Dir.UP => 90f,
			Dir.UP_RIGHT => 45f,
			Dir.RIGHT => 0f,
			Dir.DOWN_RIGHT => -45f,
			Dir.DOWN => -90f,
			Dir.DOWN_LEFT => -135f,
			Dir.LEFT => 180f,
			Dir.UP_LEFT => 135f,
			_ => 0f
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
				DamageEnemiesInHitboxWithAreaRatio(unit, box, 1.25f);
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
				DamageEnemiesInHitboxWithAreaRatio(unit, box, 0.9f, true, 1f);
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
				DamageEnemiesInHitboxWithAreaRatio(unit, box, 1f);
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
				DamageEnemiesInHitboxWithAreaRatio(unit, box, 1.45f);
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
				DamageEnemiesInHitboxWithAreaRatio(unit, box, 0.65f);
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
				DamageEnemiesInHitboxWithAreaRatio(unit, box, 1f);
				Debug.Log($"{unit.unitType.typeName} 발톱 후려치기");
			},

			() =>
			{
				unit.skillCooldowns[1] = ApplyCooldown(unit, 1.4f);
			}
		);
	}
}
