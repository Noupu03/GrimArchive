using System.Collections.Generic;
using UnityEngine;

public abstract class Unit : ScriptableObject
{
	public static FactionData humanFactionData  = new FactionData();
	public static FactionData monsterFactionData = new FactionData();

	public UnitType unitType;

	// ─── 전투 세부 속성 ───────────────────────────────────────────
	public float maxHp = 100f;            // 최대체력
	public float hp    = 100f;            // 현재 체력

	public float maxMp = 0f;             // 최대 마나
	public float mp    = 0f;             // 현재 마나

	public float physicalAttack  = 10f;  // 물리 공격력
	public float magicalAttack   = 0f;   // 마법 공격력

	public float physicalDefense = 0f;   // 물리 방어력
	public float magicalDefense  = 0f;   // 마법 방어력

	public float HPRegen         = 0f;   // 재생력->새로 추가됨. 로직없음
	public float attackspeed     = 0f;   // 공격 속도->새로 추가됨. 로직없음
	public float walkSpeed       = 3f;   // 이동 속도
	public float reaction        = 1f;   // 반응속도->로직없음
	public float criticalChance  = 0f;   // 치명타율->새로 추가됨. 로직없음
	public float cooltimeReduction = 0f; // 쿨타임 감소율->새로 추가됨. 로직없음
	public float statusResistance  = 0f; // 상태이상저항->구방식 작동중. 새방식으로는 로직없음

	public float maxMental = 0f;         // 최대 정신력->옛날 정신공격 연산으로만 작동중
	public float mental    = 0f;         // 현재 정신력

	public float spotting       = 0f;   // 감지
	public float leadershipRange = 0f;  // 지휘범위->새로 추가됨. 로직없음
	public float charisma        = 0f;  // 카리스마->새로 추가됨. 로직없음

	// ─── 정규화용 속성 ────────────────────────────────────────────
	public float sterngth    = 0f; // 근력. 정규화를 통해 산출해야 함
	public float Durability  = 0f; // 내구. 정규화를 통해 산출해야 함
	public float agility     = 0f; // 민첩. 정규화를 통해 산출해야 함
	public float concentration = 0f; // 집중. 정규화를 통해 산출해야 함
	public float MagicPower  = 0f; // 마력. 정규화를 통해 산출해야 함
	public float resistance  = 0f; // 저항. 정규화를 통해 산출해야 함
	public float sense       = 0f; // 감각. 정규화를 통해 산출해야 함
	public float leadership  = 0f; // 통솔. 정규화를 통해 산출해야 함

	// ─── 정규화 기준값 ────────────────────────────────────────────
	private const float BASE_PHYSICAL_ATTACK = 20f;
	private const float BASE_MAGICAL_ATTACK  = 20f;
	private const float BASE_MAX_HP          = 120f;
	private const float BASE_MAX_MP          = 50f;
	private const float BASE_PHYSICAL_DEF    = 10f;
	private const float BASE_MAGICAL_DEF     = 10f;
	private const float BASE_HP_REGEN        = 5f;
	private const float BASE_STATUS_RES      = 100f;
	private const float BASE_ATTACK_SPEED    = 100f;
	private const float BASE_REACTION        = 100f;
	private const float BASE_WALK_SPEED      = 3.0f;
	private const float BASE_SPOTTING        = 80f;
	private const float BASE_MENTAL          = 100f;
	private const float BASE_LEAD_RANGE      = 6f;
	private const float BASE_CHARISMA        = 5f;
	private const float BASE_CRIT            = 10f;
	private const float BASE_CDR             = 100f;

	// ─── 연산용 임시 스탯 ─────────────────────────────────────────
	public float physicalAttackSpeed = 10f; // 물리공격속도
	public float magicalCastSpeed    = 0f;  // 마법공격속도
	public float actionCooldown      = 0f;  // 턴 진행용 대기 시간
	public float[] skillCooldowns    = new float[4]; // 스킬 쿨다운
	public bool  isHitThisTurn       = false; // 피격 여부
	public bool  oneTimeReactUsed    = false; // 피격 리액션 등 1회성 억제용

	public HashSet<Unit> reactedAttackers = new HashSet<Unit>();

	public float         currentReactionWindow = 0f;
	public ThreatTileData reactingThreat       = null;
	public Unit          reactingAttacker       = null;

	public float evadeCooldown  = 0f;  // 회피 후 재접근 관련
	public bool  isCastingAttack = false; // 현재 공격 선딜 진행 여부
	public float castTimer       = 0f;  // 선딜 타이머
	public System.Action pendingAttack;  // 실제 공격 실행 예약
	public System.Action pendingVFX;     // 공격 타이밍에 맞춰 재생할 VFX (가드·패링)
	public bool suppressHitVFX = false;  // true이면 TriggerHitEffect에서 HitSpark 대신 AttackFail 재생
	public ThreatTileData currentThreat; // 현재 공격 위협 타일

	// 공격 시 자유로운 각도 (라디안)
	public float currentAttackAngle = 0f;

	// ─── 상태이상 ──────────────────────────────────────────────────
	public float stunDuration   = 0f;
	public float slowDuration   = 0f;
	public float poisonDuration = 0f;
	public float burnDuration   = 0f;

	// ─── 이동 및 프레임워크 ──────────────────────────────────────────
	public float currentSpeed  = 0f;
	public float acceleration  = 10f; // 임시 기본 가속도
	public bool  isWaitState   = false; // Spotting Broadcast에 의한 대기

	public Vector2Int? playerMoveTarget    = null;
	public Unit        playerAttackTarget   = null;

	public Vector2Int position;
	public int        currentFloor = 0;        // 현재 유닛이 위치한 층 정보
	public Dir        currentDir   = Dir.DOWN;  // 현재 바라보는 방향 (시야 기준)
	public string     spriteVariation = "";     // 스프라이트 바리에이션 (라이브러리 카테고리명)
	public static float ViewRadius = 30f;       // 전역 시야 거리

	public List<Unit>          personalSpottedEnemies = new List<Unit>();
	public List<ThreatTileData> detectedThreats        = new List<ThreatTileData>();

	// ─── 정규화 함수 ─────────────────────────────────────────────────
	// 0%~200% 범위로 클램프. 100%가 기준값과 일치하도록.
	private float Normalize(float value, float baseValue)
	{
		if (baseValue <= 0f) return 0f;
		return Mathf.Clamp((value / baseValue) * 100f, 0f, 200f);
	}

	public void CalculateDerivedStats()
	{
		// 정규화
		float nAtk      = Normalize(physicalAttack,    BASE_PHYSICAL_ATTACK);
		float nMatk     = Normalize(magicalAttack,     BASE_MAGICAL_ATTACK);
		float nHp       = Normalize(maxHp,             BASE_MAX_HP);
		float nMp       = Normalize(maxMp,             BASE_MAX_MP);
		float nPDef     = Normalize(physicalDefense,   BASE_PHYSICAL_DEF);
		float nMDef     = Normalize(magicalDefense,    BASE_MAGICAL_DEF);
		float nRegen    = Normalize(HPRegen,           BASE_HP_REGEN);
		float nStatus   = Normalize(statusResistance,  BASE_STATUS_RES);
		float nAtkSpd   = Normalize(attackspeed,       BASE_ATTACK_SPEED);
		float nReact    = Normalize(reaction,          BASE_REACTION);
		float nMove     = Normalize(walkSpeed,         BASE_WALK_SPEED);
		float nSpot     = Normalize(spotting,          BASE_SPOTTING);
		float nMental   = Normalize(mental,            BASE_MENTAL);
		float nLeadRange = Normalize(leadershipRange,  BASE_LEAD_RANGE);
		float nCharisma = Normalize(charisma,          BASE_CHARISMA);
		float nCrit     = Normalize(criticalChance,    BASE_CRIT);
		float nCdr      = Normalize(cooltimeReduction, BASE_CDR);

		// 기본 능력치 계산
		// 근력 = 물리 공격력 정규화
		sterngth = nAtk;

		// 내구 = 체력 45 + 물방 45 + 재생 10
		Durability = nHp * 0.45f + nPDef * 0.45f + nRegen * 0.10f;

		// 민첩 = 공속 35 + 이동 25 + 반응 40
		agility = nAtkSpd * 0.35f + nMove * 0.25f + nReact * 0.40f;

		// 집중 = 치명 60 + 쿨감 40
		concentration = nCrit * 0.60f + nCdr * 0.40f;

		// 마력 = 마공 60 + 마나 40
		MagicPower = nMatk * 0.60f + nMp * 0.40f;

		// 저항 (몬스터 예외)
		if (this is Monster)
			resistance = nMDef * 0.5f + nStatus * 0.5f;
		else
			resistance = nMDef * 0.35f + nStatus * 0.35f + nMental * 0.30f;

		// 감각 = 감지
		sense = nSpot;

		// 통솔 = 지휘범위 50 + 카리스마 50
		leadership = nLeadRange * 0.5f + nCharisma * 0.5f;
	}

	public void SetupStats()
	{
		GameDataLoader.ApplyStatsTo(this);
		CalculateDerivedStats();
	}

	public Quaternion GetDirRotation(Dir dir)
	{
		float angle = dir switch
		{
			Dir.UP        => 90f,
			Dir.UP_RIGHT  => 45f,
			Dir.RIGHT     => 0f,
			Dir.DOWN_RIGHT => -45f,
			Dir.DOWN      => -90f,
			Dir.DOWN_LEFT => -135f,
			Dir.LEFT      => 180f,
			Dir.UP_LEFT   => 135f,
			_             => 0f
		};
		return Quaternion.Euler(0f, 0f, angle);
	}

	// ─── 추상 메서드 ──────────────────────────────────────────────────
	public abstract void TakeDamage(float damage);
	public abstract void TakePhysicalDamage(float rawDamage, Unit attacker);
	public abstract void TakeMagicalDamage(float rawDamage, Unit attacker);
	public abstract void TakeMentalDamage(float rawDamage, Unit attacker);
	public abstract void ApplyStun(float duration);
	public abstract void ApplySlow(float duration);
	public abstract void ApplyPoison(float duration);
	public abstract void ApplyBurn(float duration);
	public abstract Vector2Int GetDirVector(Dir dir);
	public abstract bool CanMove(Vector2Int pos);
	public abstract void Move(Dir dir);

	public virtual void ForceMove(Vector2Int targetPos)
	{
		if (GameSession.Instance == null) return;

		Vector3Int oldKey = new Vector3Int(position.x, position.y, currentFloor);
		if (GameSession.Instance.unitGrid.ContainsKey(oldKey))
			GameSession.Instance.unitGrid.Remove(oldKey);

		position = targetPos;

		Vector3Int newKey = new Vector3Int(position.x, position.y, currentFloor);
		GameSession.Instance.unitGrid[newKey] = this;
	}

	public abstract void UpdateFOV(List<Unit> allUnits);

	private GoapBrain _brain;
	public GoapBrain brain { get { if (_brain == null) _brain = new GoapBrain(); return _brain; } }

	public virtual void JudgeState()
	{
		if (stunDuration > 0f) return; // 기절 시 행동 불가
		brain.JudgeState(this);
	}

	public virtual void ExecuteAction()
	{
		if (stunDuration > 0f) return;
		brain.ExecuteAction(this);
	}

	public static Hitbox GetUnitHitbox(Unit u)
	{
		return new Hitbox
		{
			center = (Vector2)u.position + new Vector2(u.unitType.footprint.x, u.unitType.footprint.y) * 0.5f,
			size   = new Vector2(u.unitType.footprint.x, u.unitType.footprint.y),
			rotation = 0f
		};
	}

	public abstract void OnUpdate(float deltaTime);
	public abstract void OnThreatDetected(List<ThreatTileData> threats);
	public abstract void OnReactToThreat(Unit attacker, ThreatTileData threat);
	public abstract void OnDirectHit(Unit attacker, ThreatTileData threat);
	public abstract void ApplyDirectDamage(Unit attacker, float multiplier = 1f);
	public abstract List<ThreatTileData> DetectThreats();
	public abstract bool IsInThreat(Vector2Int pos, ThreatTileData threat);
	public abstract bool RollCritical(bool canCritical);
	public abstract float ApplyCriticalDamage(float rawDamage);
}

public class Human : UnitFunction
{
	public override void JudgeState()
	{
		base.JudgeState();
		// 인류 상태 판단 로직 추가
	}
}

public class Monster : UnitFunction
{
	public override void JudgeState()
	{
		base.JudgeState();
		// 몬스터 상태 판단 로직 추가
	}
}
