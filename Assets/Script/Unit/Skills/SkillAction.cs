using System.Collections.Generic;
using UnityEngine;

public abstract class SkillAction
{
	public string SkillName { get; protected set; }

	// 에디터 / SkillTuner에서 읽는 기본값 (각 스킬에서 override)
	public virtual float DefaultBaseDelayMs  => 500f;
	public virtual float DefaultBaseCooldown => 3f;

	// 사정거리 정보 (Actions.cs의 canHit 사전 검사에 사용)
	public virtual ThreatShape HitShape => ThreatShape.LINE;
	public virtual int HitRange => 1;
	public virtual int HitWidth => 1;
	public virtual int HitDepth => 1;

	public Hitbox BuildSkillHitbox(Unit unit)
	{
		if (HitShape == ThreatShape.RECT)
			return BuildRectHitboxWithAngle(unit, HitWidth, HitDepth, unit.currentAttackAngle);
		return BuildLineHitboxWithAngle(unit, HitRange, unit.currentAttackAngle);
	}

	public abstract bool  IsAvailable(Unit unit);
	public abstract float GetPriority(Unit unit, Unit target, float minDist);
	public abstract void  Execute(Unit unit, Unit target, float minDist);

	// ─── 공격 시작 ────────────────────────────────────────────────────

	public static void BeginAttackCast(
		Unit         unit,
		float        castMs,
		ThreatTileData threat,
		System.Action attackAction,
		System.Action cooldownAction = null,
		System.Action effectAction   = null)
	{
		unit.isCastingAttack = true;
		unit.castTimer       = castMs / 1000f;

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
				unit.currentThreat   = null;
				unit.isCastingAttack = false;
				unit.pendingAttack   = null;
				unit.castTimer       = 0f;
			}
		};
	}

	// ─── 히트박스 검색 / 데미지 ──────────────────────────────────────

	public static List<Unit> GetEnemiesInHitbox(Unit attacker, Hitbox box)
	{
		List<Unit> result = new List<Unit>();

		foreach (var u in GameSession.Instance.units)
		{
			if (u == null || u == attacker || u.hp <= 0) continue;
			if (u.currentFloor != attacker.currentFloor) continue;

			bool isEnemy = (attacker is Human && u is Monster) ||
			               (attacker is Monster && u is Human);
			if (!isEnemy) continue;

			if (box.Overlaps(GetUnitHitbox(u))) result.Add(u);
		}
		return result;
	}

	public static Hitbox GetUnitHitbox(Unit u)
	{
		Vector2 size = new Vector2(u.unitType.footprint.x, u.unitType.footprint.y);
		return new Hitbox
		{
			center = (Vector2)u.position + size * 0.5f,
			size   = size
		};
	}

	public static void DamageEnemiesInHitbox(Unit attacker, Hitbox box, float multiplier, bool stun = false, float stunDuration = 0f)
	{
		foreach (var t in GetEnemiesInHitbox(attacker, box))
		{
			t.TakePhysicalDamage(attacker.physicalAttack * multiplier, attacker);
			if (stun) t.ApplyStun(stunDuration);
		}
	}

	/// <summary>
	/// 히트박스와의 교차 면적 비율에 따라 데미지를 적용합니다.
	/// 면적이 많이 겹칠수록 더 많은 데미지를 입습니다.
	/// </summary>
	public static void DamageEnemiesInHitboxWithAreaRatio(Unit attacker, Hitbox attackBox, float multiplier, bool stun = false, float stunDuration = 0f)
	{
		foreach (var t in GetEnemiesInHitbox(attacker, attackBox))
		{
			Hitbox targetBox    = GetUnitHitbox(t);
			float  overlapRatio = attackBox.CalculateOverlapRatio(targetBox);

			// 교차 비율에 따라 데미지 조정 (최소 0.1배)
			float finalDamage = Mathf.Max(1f, attacker.physicalAttack * Mathf.Max(0.1f, multiplier * overlapRatio));
			t.TakePhysicalDamage(finalDamage, attacker);
			if (stun) t.ApplyStun(stunDuration);

			// 로깅: 개발용 (필요시 제거)
			// Debug.Log($"{attacker.unitType.typeName} → {t.unitType.typeName}: 교차비율={overlapRatio:P0}, 데미지={finalDamage:F1}");
		}
	}

	/// <summary>
	/// 마법 데미지 버전 - 히트박스 교차 면적 비율 적용
	/// </summary>
	public static void DamageMagicalEnemiesInHitboxWithAreaRatio(Unit attacker, Hitbox attackBox, float multiplier, bool stun = false, float stunDuration = 0f)
	{
		foreach (var t in GetEnemiesInHitbox(attacker, attackBox))
		{
			Hitbox targetBox    = GetUnitHitbox(t);
			float  overlapRatio = attackBox.CalculateOverlapRatio(targetBox);

			// 교차 비율에 따라 데미지 조정 (최소 0.1배)
			float finalDamage = Mathf.Max(1f, attacker.magicalAttack * Mathf.Max(0.1f, multiplier * overlapRatio));
			t.TakeMagicalDamage(finalDamage, attacker);
			if (stun) t.ApplyStun(stunDuration);
		}
	}

	// ─── 히트박스 빌더 ─────────────────────────────────────────────────

	public static Hitbox BuildLineHitbox(Unit unit, int range)
	{
		Vector2 dir = unit.GetDirVector(unit.currentDir);
		Vector2 unitCenter = (Vector2)unit.position + new Vector2(unit.unitType.footprint.x, unit.unitType.footprint.y) * 0.5f;
		Vector2 offset     = dir.normalized * (range * 0.5f + 0.5f);
		Vector2 center     = unitCenter + offset;

		bool isHorizontal = Mathf.Abs(dir.x) > 0 && Mathf.Abs(dir.y) == 0;
		bool isVertical   = Mathf.Abs(dir.y) > 0 && Mathf.Abs(dir.x) == 0;

		Vector2 size;
		float   rotation;

		if (isHorizontal)      { size = new Vector2(range, 1); rotation = 0f; }
		else if (isVertical)   { size = new Vector2(1, range); rotation = 0f; }
		else                   { size = new Vector2(range, 1); rotation = GetRotationForDirection(unit.currentDir); } // 대각선

		return new Hitbox { center = center, size = size, rotation = rotation };
	}

	/// <summary>
	/// 주어진 각도 기반으로 라인 히트박스를 생성합니다 (공격 시 자유 각도 지원)
	/// </summary>
	public static Hitbox BuildLineHitboxWithAngle(Unit unit, int range, float angleRad)
	{
		Vector2 dir        = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
		Vector2 unitCenter = (Vector2)unit.position + new Vector2(unit.unitType.footprint.x, unit.unitType.footprint.y) * 0.5f;
		Vector2 center     = unitCenter + dir * (range * 0.5f + 0.5f);

		return new Hitbox
		{
			center   = center,
			size     = new Vector2(range, 1),
			rotation = angleRad * Mathf.Rad2Deg
		};
	}

	public static Hitbox BuildRectHitbox(Unit unit, int width, int depth)
	{
		Vector2 forward    = unit.GetDirVector(unit.currentDir);
		Vector2 unitCenter = (Vector2)unit.position + new Vector2(unit.unitType.footprint.x, unit.unitType.footprint.y) * 0.5f;
		Vector2 offset     = forward.normalized * (depth * 0.5f + 0.5f);
		Vector2 center     = unitCenter + offset;

		bool isHorizontal = Mathf.Abs(forward.x) > 0 && Mathf.Abs(forward.y) == 0;
		bool isVertical   = Mathf.Abs(forward.y) > 0 && Mathf.Abs(forward.x) == 0;

		Vector2 size;
		float   rotation;

		if (isHorizontal)    { size = new Vector2(depth, width); rotation = 0f; }
		else if (isVertical) { size = new Vector2(width, depth); rotation = 0f; }
		else                 { size = new Vector2(width, depth); rotation = GetRotationForDirection(unit.currentDir); } // 대각선

		return new Hitbox { center = center, size = size, rotation = rotation };
	}

	/// <summary>
	/// 주어진 각도 기반으로 직사각형 히트박스를 생성합니다 (공격 시 자유 각도 지원)
	/// </summary>
	public static Hitbox BuildRectHitboxWithAngle(Unit unit, int width, int depth, float angleRad)
	{
		Vector2 forward    = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
		Vector2 unitCenter = (Vector2)unit.position + new Vector2(unit.unitType.footprint.x, unit.unitType.footprint.y) * 0.5f;
		Vector2 center     = unitCenter + forward * (depth * 0.5f + 0.5f);

		return new Hitbox
		{
			center   = center,
			size     = new Vector2(depth, width),
			rotation = angleRad * Mathf.Rad2Deg
		};
	}

	// ─── 유틸리티 ──────────────────────────────────────────────────────

	public static float GetRotationForDirection(Dir dir)
	{
		return dir switch
		{
			Dir.UP         => 90f,
			Dir.UP_RIGHT   => 45f,
			Dir.RIGHT      => 0f,
			Dir.DOWN_RIGHT => -45f,
			Dir.DOWN       => -90f,
			Dir.DOWN_LEFT  => -135f,
			Dir.LEFT       => 180f,
			Dir.UP_LEFT    => 135f,
			_              => 0f
		};
	}

	public static Dir GetDirection8(Vector2Int diff)
	{
		if (diff == Vector2Int.zero) return Dir.DOWN;

		int x = diff.x;
		int y = diff.y;

		if (x > 0 && y > 0) return Dir.UP_RIGHT;
		if (x > 0 && y < 0) return Dir.DOWN_RIGHT;
		if (x < 0 && y > 0) return Dir.UP_LEFT;
		if (x < 0 && y < 0) return Dir.DOWN_LEFT;
		if (x > 0) return Dir.RIGHT;
		if (x < 0) return Dir.LEFT;
		if (y > 0) return Dir.UP;
		return Dir.DOWN;
	}

	public static float GetCompression(Unit unit)
	{
		bool isDiagonal =
			unit.currentDir == Dir.UP_RIGHT  ||
			unit.currentDir == Dir.UP_LEFT    ||
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
