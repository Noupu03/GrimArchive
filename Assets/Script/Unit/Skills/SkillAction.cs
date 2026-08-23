using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

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

	public virtual Hitbox BuildSkillHitbox(Unit unit)
	{
		if (HitShape == ThreatShape.RECT)
			return BuildRectHitboxWithAngle(unit, HitWidth, HitDepth, unit.CombatState.State.currentAttackAngle);
		return BuildLineHitboxWithAngle(unit, HitRange, unit.CombatState.State.currentAttackAngle);
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
		System.Action effectAction   = null,
		System.Action castUpdateAction = null,
		AttackShape  shape = AttackShape.Melee)
	{
		unit.CombatState.State.lastAttackShape = shape; // 07문서 17장: 방향 간접입력 판정용

		// hitbox 생성 - 공격 시 자유로운 각도를 사용하여 생성
		if (threat.hitbox.size == Vector2.zero)
		{
			if (threat.shape == ThreatShape.LINE)
				threat.hitbox = BuildLineHitboxWithAngle(unit, threat.range, unit.CombatState.State.currentAttackAngle);
			else if (threat.shape == ThreatShape.RECT)
				threat.hitbox = BuildRectHitboxWithAngle(unit, threat.width, threat.depth, unit.CombatState.State.currentAttackAngle);
		}

		unit.AIState.currentThreat = threat;
		unit.Session?.OnThreatCreated.OnNext((unit, threat));

		if (castMs <= 0f)
		{
			// 즉시 공격 실행: 시전 대기 없이 바로 공격 및 방어/피해 연산
			try
			{
				effectAction?.Invoke();
				attackAction?.Invoke();
				unit.TriggerAttackVisibilityBoost(); // 01-A 9장: 공격 후 가시성 상승(+10, 5초, 재공격 시 지속시간 초기화)
				PropagationSystem.EmitSound(unit.Session, SoundType.AttackExecution, unit.position, unit.currentFloor, unit);
			}
			finally
			{
				cooldownAction?.Invoke();
				unit.AIState.currentThreat             = null;
				unit.CombatState.State.isCastingAttack = false;
				unit.CombatState.State.castTimer       = 0f;
				unit.AIState.pendingAttack             = null;
				unit.AIState.pendingCastUpdate         = null;
				unit.Session?.castingUnits.Remove(unit);
			}
		}
		else
		{
			unit.CombatState.State.isCastingAttack = true;
			unit.CombatState.State.castTimer       = castMs / 1000f;
			unit.Session?.castingUnits.Add(unit);
			unit.AIState.pendingCastUpdate = castUpdateAction;

			unit.AIState.pendingAttack = () =>
			{
				try
				{
					effectAction?.Invoke();
					attackAction?.Invoke();
					unit.TriggerAttackVisibilityBoost();
					PropagationSystem.EmitSound(unit.Session, SoundType.AttackExecution, unit.position, unit.currentFloor, unit);
				}
				finally
				{
					cooldownAction?.Invoke();

					unit.AIState.currentThreat             = null;
					unit.CombatState.State.isCastingAttack = false;
					unit.AIState.pendingAttack             = null;
					unit.AIState.pendingCastUpdate         = null;
					unit.CombatState.State.castTimer       = 0f;
				}
			};
		}
	}

	// ─── 히트박스 검색 / 데미지 ──────────────────────────────────────

	// ⑤: 공격마다 new List<Unit>()를 할당하던 것을 static 캐시로 교체 — 호출자는 반환값을 즉시 소비해야 함.
	// 2026-07-31 프로파일러 분석(Object.CompareBaseObjects 39,397회/IsNativeObjectAlive 28,565회) —
	// Unit이 ScriptableObject(UnityEngine.Object)라서 List<Unit>.Contains()가 원소마다 네이티브
	// 유효성 검사가 딸린 Equals를 호출한다. Footprint 중복 셀 제거용 Contains 검사를 HashSet으로
	// 병행 관리해 해시코드(인스턴스ID, 네이티브 호출 없음) 기반 O(1) 조회로 대체한다.
	private static readonly List<Unit> _hitboxQueryResult = new List<Unit>();
	private static readonly HashSet<Unit> _hitboxQueryResultSet = new HashSet<Unit>();

	public static List<Unit> GetEnemiesInHitbox(Unit attacker, Hitbox box)
	{
		_hitboxQueryResult.Clear();
		_hitboxQueryResultSet.Clear();

		var unitGrid = attacker.Session?.unitGrid;
		if (unitGrid != null)
		{
			float rad      = box.rotation * Mathf.Deg2Rad;
			float cosA     = Mathf.Abs(Mathf.Cos(rad));
			float sinA     = Mathf.Abs(Mathf.Sin(rad));
			float halfW    = box.size.x * 0.5f;
			float halfH    = box.size.y * 0.5f;
			int minX = Mathf.FloorToInt(box.center.x - (halfW * cosA + halfH * sinA));
			int maxX = Mathf.FloorToInt(box.center.x + (halfW * cosA + halfH * sinA));
			int minY = Mathf.FloorToInt(box.center.y - (halfW * sinA + halfH * cosA));
			int maxY = Mathf.FloorToInt(box.center.y + (halfW * sinA + halfH * cosA));

			int floor = attacker.currentFloor;
			for (int cx = minX; cx <= maxX; cx++)
			{
				for (int cy = minY; cy <= maxY; cy++)
				{
					if (!unitGrid.TryGetValue(new Vector3Int(cx, cy, floor), out Unit u)) continue;
					if (u == null || u == attacker || u.Health.hp <= 0) continue;
					if (!attacker.IsEnemy(u)) continue;
					if (_hitboxQueryResultSet.Contains(u)) continue; // Footprint 중복 셀 무시

					if (box.Overlaps(GetUnitHitbox(u)))
					{
						_hitboxQueryResult.Add(u);
						_hitboxQueryResultSet.Add(u);
					}
				}
			}
		}
		else if (attacker.Session != null)
		{
			foreach (var u in attacker.Session.units)
			{
				if (u == null || u == attacker || u.Health.hp <= 0) continue;
				if (u.currentFloor != attacker.currentFloor) continue;
				if (!attacker.IsEnemy(u)) continue;

				if (box.Overlaps(GetUnitHitbox(u)))
				{
					_hitboxQueryResult.Add(u);
					_hitboxQueryResultSet.Add(u);
				}
			}
		}

		return _hitboxQueryResult;
	}

	// 2026-07-31 — CombatFSMState/PlayerCommandFSMState가 GetEnemiesInHitbox(...).Contains(target)로
	// 특정 대상 포함 여부만 확인하던 자리를 위한 전용 진입점. 위 _hitboxQueryResultSet을 그대로 재사용해
	// List.Contains()의 선형 Equals 호출을 피한다. GetEnemiesInHitbox와 동일하게 호출 직후 즉시 소비할 것.
	public static bool GetEnemiesInHitboxContains(Unit attacker, Hitbox box, Unit target)
	{
		GetEnemiesInHitbox(attacker, box);
		return _hitboxQueryResultSet.Contains(target);
	}

	public static Hitbox GetUnitHitbox(Unit u)
	{
		Vector2 size = u.unitType != null ? new Vector2(u.unitType.footprint.x, u.unitType.footprint.y) : new Vector2(1, 1);
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
			t.TakePhysicalDamage(attacker.CombatStat.physicalAttack * multiplier, attacker);
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
			float finalDamage = Mathf.Max(1f, attacker.CombatStat.physicalAttack * Mathf.Max(0.1f, multiplier * overlapRatio));
			t.TakePhysicalDamage(finalDamage, attacker);
			if (stun) t.ApplyStun(stunDuration);

			// 로깅: 개발용 (필요시 제거)
			// LogHelper.Log(LogHelper.GAME, $"{attacker.unitType.typeName} → {t.unitType.typeName}: 교차비율={overlapRatio:P0}, 데미지={finalDamage:F1}");
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
			float finalDamage = Mathf.Max(1f, attacker.CombatStat.magicalAttack * Mathf.Max(0.1f, multiplier * overlapRatio));
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
		float reduction = Mathf.Min(50f, unit.BaseStat.cooltimeReduction);
		return baseCd * (1f - reduction / 100f);
	}
}
