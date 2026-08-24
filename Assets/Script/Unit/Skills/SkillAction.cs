using System;
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

	// ─── 타겟팅 두 축 (2026-08-23, 자세한 설명은 SkillTargeting.cs) ──────
	// 기본값은 가장 흔한 조합인 "시전자 앞 히트박스로 적을 때린다".

	/// <summary>판정을 어디에 만드는가.</summary>
	public virtual SkillOrigin Origin => SkillOrigin.SelfArea;

	/// <summary>누구를 대상으로 하는가.</summary>
	public virtual SkillAffinity Affinity => SkillAffinity.Enemy;

	/// <summary>
	/// 이 스킬이 실제로 겨눌 대상을 결정한다. AI는 "가장 가까운 적"만 알고 있으므로, 아군을 대상으로
	/// 하는 스킬은 여기서 자기 대상을 직접 찾아 돌려준다(진영 축이 소비되는 지점). 대상이 없으면 null.
	/// 반환값은 그대로 CanExecuteAgainst와 Execute로 전달되므로, 대상 탐색이 한 번만 일어난다.
	/// </summary>
	public virtual Unit ResolveTarget(Unit unit, Unit nearestEnemy) => nearestEnemy;

	/// <summary>
	/// 지금 이 스킬을 실제로 쓸 수 있는 위치인가. AI 쪽(CombatFSMState/StandGroundAttackFSMState/
	/// PlayerCommandFSMState)이 스킬을 고른 뒤 실행 직전에 부르는 공통 관문이다 — 예전에는 세 곳이
	/// 각자 "적이 시전자 앞 히트박스에 들어왔는가"만 검사해서, 아군 대상 스킬까지 그 잣대에 걸렸다.
	/// 인자로 받는 target은 ResolveTarget이 돌려준 "이미 해결된 대상"이다(적일 수도 아군일 수도 있다).
	/// </summary>
	public virtual bool CanExecuteAgainst(Unit unit, Unit target, float dist)
	{
		if (unit == null || target == null) return false;

		// 적을 시전자 기준 히트박스로 때리는 방식만 히트박스 검사를 거친다.
		// 그 외(대상 좌표 기준 / 아군 대상)는 대상까지의 거리로 판단한다.
		if (Affinity == SkillAffinity.Enemy && Origin != SkillOrigin.TargetArea)
			return GetEnemiesInHitboxContains(unit, BuildSkillHitbox(unit), target);

		int range = HitRange > 0 ? HitRange : 1;
		return AIMovementHelper.ChebyshevDistance(unit.position, target.position) <= range;
	}

	public virtual Hitbox BuildSkillHitbox(Unit unit)
	{
		if (HitShape == ThreatShape.RECT)
			return BuildRectHitboxWithAngle(unit, HitWidth, HitDepth, unit.CombatState.State.currentAttackAngle);
		return BuildLineHitboxWithAngle(unit, HitRange, unit.CombatState.State.currentAttackAngle);
	}

	public abstract bool  IsAvailable(Unit unit);
	public abstract float GetPriority(Unit unit, Unit target, float minDist);
	public abstract void  Execute(Unit unit, Unit target, float minDist);

	// 대부분의 IsAvailable override가 앞머리에 반복하던 쿨다운 게이트(2026-08-25 리팩토링 — 12개
	// 서브클래스 중복). 각 서브클래스가 SkillData(_d)를 개별 소유해(베이스 클래스엔 없음) cooldownSlot을
	// 파라미터로 받는다.
	protected static bool IsCooldownReady(Unit unit, int cooldownSlot)
	{
		if (unit == null || unit.CombatState.State.skillCooldowns == null) return false;
		if (cooldownSlot < 0 || cooldownSlot >= unit.CombatState.State.skillCooldowns.Length) return false;
		return unit.CombatState.State.skillCooldowns[cooldownSlot] <= 0f;
	}

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

		// 시전 상태를 통지보다 먼저 세팅한다 — 위협 타일이 화면에 표시되는 시간(GameSession의
		// OnThreatCreated 구독)이 공격자의 castTimer를 읽어 결정되기 때문이다. 순서가 반대면
		// 통지 시점의 castTimer가 항상 0이라 시전 시간과 무관하게 0.5초 폴백이 쓰였고, 시전이
		// 긴 스킬(파이어볼 등 지연 착탄)은 예고가 착탄 한참 전에 사라졌다(2026-08-23 수정).
		if (castMs > 0f)
		{
			unit.CombatState.State.isCastingAttack = true;
			unit.CombatState.State.castTimer       = castMs / 1000f;
			unit.Session?.castingUnits.Add(unit);
			unit.AIState.pendingCastUpdate = castUpdateAction;
		}

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
			// 시전 상태(isCastingAttack/castTimer/castingUnits/pendingCastUpdate)는 위 통지 직전에
			// 이미 세팅했다.
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
	// onHit(2026-08-24 신규) — 대상별 피해 적용 직후(스턴 적용 포함) 호출되는 선택적 콜백. 스킬별
	// hitEffectPrefab을 "실제로 맞은 대상 각각"의 위치에 스폰하려는 호출부가 쓴다(사용자 신고
	// "HitEffectPrefab에 있는 VFX가 투사체가 아닌 스킬에는 실행되지 않는 문제" — GetEnemiesInHitbox를
	// 이 메서드 밖에서 따로 다시 호출하면 그 사이 죽은 대상이 두 번째 조회에서 걸러져 정작 킬 데미지를
	// 넣은 히트에는 이펙트가 안 뜨는 문제가 생긴다. 같은 순회에서 함께 처리해야 그 문제가 없다).
	public static void DamageEnemiesInHitboxWithAreaRatio(Unit attacker, Hitbox attackBox, float multiplier, bool stun = false, float stunDuration = 0f, Action<Unit> onHit = null)
	{
		foreach (var t in GetEnemiesInHitbox(attacker, attackBox))
		{
			Hitbox targetBox    = GetUnitHitbox(t);
			float  overlapRatio = attackBox.CalculateOverlapRatio(targetBox);

			// 교차 비율에 따라 데미지 조정 (최소 0.1배)
			float finalDamage = Mathf.Max(1f, attacker.CombatStat.physicalAttack * Mathf.Max(0.1f, multiplier * overlapRatio));
			t.TakePhysicalDamage(finalDamage, attacker);
			if (stun) t.ApplyStun(stunDuration);
			onHit?.Invoke(t);

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

	// 쿨감 상한 (2026-08-24: 50 → 25). units.json의 cooltimeReduction이 거의 모든 유닛에서 90~95라
	// 예전 상한으로는 전원이 항상 최대 감소를 받아 모든 스킬 쿨다운이 절반이 됐다("공격과 공격 사이
	// 간격이 너무 짧음"). 지금은 최대 25% 감소라 실질 쿨다운이 예전의 1.5배다.
	// 더 늘리려면 이 상한을 낮추거나(전역), skills.json + 해당 프리팹의 baseCooldown을 올린다(개별).
	public const float MaxCooldownReductionPercent = 25f;

	public static float ApplyCooldown(Unit unit, float baseCd)
	{
		float reduction = Mathf.Min(MaxCooldownReductionPercent, unit.BaseStat.cooltimeReduction);
		return baseCd * (1f - reduction / 100f);
	}
}
