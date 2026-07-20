using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Cysharp.Threading.Tasks;

public abstract class Unit : ScriptableObject
{
	public static FactionData humanFactionData  = new FactionData();
	public static FactionData monsterFactionData = new FactionData();

	// UnitGenerate가 ScriptableObject.CreateInstance 직후 IObjectResolver.Inject(this)로 채워준다.
	// GoapAction/SkillAction 등 DI 컨테이너가 직접 닿지 않는 순수 C# 로직이 이 유닛을 통해 서비스에 접근한다.
	[Inject] private UnitGenerate _unitGenerate;
	[Inject] private UIManager _uiManager;
	[Inject] private VFXManager _vfxManager;
	[Inject] private InputManager _inputManager;
	[Inject] private GameSession _gameSession;
	[Inject] private HumanKnowledgeBase _knowledgeBase;

	public UnitGenerate Generate => _unitGenerate;
	public UIManager UI => _uiManager;
	public VFXManager VFX => _vfxManager;
	public InputManager InputMgr => _inputManager;
	public GameSession Session => _gameSession;
	public HumanKnowledgeBase Knowledge => _knowledgeBase;

	// HAARE 프레임워크 (Native Routine): 유닛 소속과 행동 패턴을 결정하는 인터페이스
	public IFactionBehavior FactionBehavior { get; set; }

	// 전략 패턴: 유닛의 이동 알고리즘을 런타임에 갈아끼울 수 있는 구조
	public IMovementAlgorithm MovementAlgorithm { get; set; } = new AStarMovement();

	public UnitType unitType;

	// ─── 가중치 시스템(이해도/위험도/흥미도) 관련 — 대표 가중치 연산공식 문서 v0.7 ────────
	public bool isSpecialUnit = false;     // 7-1장: 보스/네메시스 등 종별+개별 이해도를 함께 쓰는 특수 유닛 여부
	public bool isInterestTarget = false;  // 6-2장/18장: IsInterestTarget 플래그 (이해도 상승에 따른 흥미도 감소식 미적용)
	public float baseInterest = 0f;        // 6-3장: 유닛 기본 흥미도
	public float baseDanger = 0f;          // 12-1장: 대상 기본 위험도
	public float heavyHitThreshold = 10f;  // 3장: "일정 피해량 이상" 판정 기준값 (유닛별 데이터 테이블)

	// 4장: 이 유닛(인류 관점의 관찰자)이 대상별로 들고 있는 개인 가중치 기록.
	public readonly Dictionary<string, PersonalWeightRecord> personalWeights = new Dictionary<string, PersonalWeightRecord>();

	// 가장 최근에 이 유닛에게 피해를 입힌 대상. 사망 시점(GameSession.RemoveDeadUnit)에서
	// "누가 처치했는지"를 알아야 위험도/이해도 처치 이벤트(E_MONSTER_KILL_SELF 등)를 기록할 수 있어서 둔다.
	public Unit lastAttacker;

	// ─── 레벨 및 성장 속성 ────────────────────────────────────────
	public int level = 1;                 // 현재 레벨
	public float exp = 0f;                // 현재 경험치
	public int killCount = 0;             // 적 처치 수

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

	public float spotting       = 0f;   // 감지 — 시야-인지-반응 문서(01-A)의 "감지 스탯". 시야/인지 거리·인지각·원형 인지 범위 반지름이 전부 이 값으로 결정된다(VisionMath).
	public float leadershipRange = 0f;  // 지휘범위->새로 추가됨. 로직없음
	public float charisma        = 0f;  // 카리스마->새로 추가됨. 로직없음

	// ─── 시야-인지-반응 시스템 관련 — 01_시야·인지범위·가시성 문서 v0.2 ────────────
	public float stealth = 0f;         // 은신 — 최종 가시성을 낮추는 세부 스탯(01장 10절). 거리/가림 보정 세부산식은 05-A 문서 부재로 스텁(VisionMath.FinalVisibility 참고)
	public float baseVisibility = 100f; // 대상 기본 가시성(01장 9절) — 일반 유닛은 100, 은신형/특수 유닛은 데이터로 낮게 설정
	public float attackVisibilityBoostTimer = 0f; // 공격 후 가시성 상승 지속시간 타이머(초, 01-A 9장) — SkillAction.BeginAttackCast가 공격 실행 시 세팅, UnitFunction.OnUpdate가 감소

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
	public System.Action pendingCastUpdate; // 캐스팅 중 매 프레임 업데이트
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

	// ─── 유닛 배치 시스템 롤백 완료 ───

	public Vector2Int? playerMoveTarget    = null;
	public bool isManualMoveCommand        = false; // 유저가 직접 클릭하여 내린 이동 명령인지 여부
	public Unit        playerAttackTarget   = null;
	public Vector3Int? playerInteractTarget = null;
	public Vector2Int position;
	public int        currentFloor = 0;        // 현재 유닛이 위치한 층 정보
	public Dir        currentDir   = Dir.DOWN;  // 현재 바라보는 방향 (시야 기준)
	public string     spriteVariation = "";     // 스프라이트 바리에이션 (라이브러리 카테고리명)

	public List<Unit>          personalSpottedEnemies = new List<Unit>();
	public List<ThreatTileData> detectedThreats        = new List<ThreatTileData>();

	// ─── 인지·정보판정·실패처리 시스템 관련 — 02_인지·정보판정·실패처리_시스템_v0.2 ────────
	// 4장: 대상별(적 유닛=Unit 참조, 오브젝트=InteractableObject.Id) 지속 인지 상태. 트리거 시점에만
	// UnitFunction.CastRay/ResolveReachedTarget/ForceRollPerception이 갱신한다 — personalSpottedEnemies와 달리 매
	// UpdateFOV 호출마다 Clear되지 않는다(PerceptionRecord.cs 주석 참고).
	public readonly Dictionary<object, PerceptionRecord> perceptionRecords = new Dictionary<object, PerceptionRecord>();

	// 20장: 수상한 타일 확인 대기 중인 레코드가 하나라도 있으면 경계 상태 — 10장 감지 보정(+20)과
	// 01-A 10장(구 11장) 시야 방향 전환 우선순위의 Alert 사유가 이 값을 참조한다.
	// 2026-07-20 성능 수정: 원래 perceptionRecords 전체를 매번 순회(O(n))했는데, 이 프로퍼티가
	// UnitFunction.ForceRollPerception(피격마다 강제 호출되는 IsAttackerIdentified 경로 포함) 안에서
	// 읽혀 전투 중 매 타격마다 O(n)이 반복됐다 — perceptionRecords가 세션 내내 정리되지 않고 쌓이는
	// 구조(아래 RemovePerceptionRecord 참고 전까지는 그랬음)와 겹쳐 유닛 수·전투 시간이 늘수록 급격히
	// 무거워졌다. PendingSuspiciousInvestigation이 바뀌는 유일한 지점(ForceRollPerception)에서
	// _alertRecordCount만 갱신하는 O(1) 카운터로 교체.
	private int _alertRecordCount = 0;
	public bool IsAlert => _alertRecordCount > 0;

	// PendingSuspiciousInvestigation을 바꾸는 모든 지점(ForceRollPerception, 레코드 제거)이 반드시
	// 이 메서드를 통해서만 카운터를 갱신한다 — 직접 필드를 대입하면 카운터가 어긋난다.
	public void NotifyPerceptionSuspiciousChanged(bool wasSuspicious, bool nowSuspicious)
	{
		if (wasSuspicious == nowSuspicious) return;
		_alertRecordCount += nowSuspicious ? 1 : -1;
		if (_alertRecordCount < 0) _alertRecordCount = 0; // 방어적 처리 — 정상 흐름에서는 발생하지 않아야 함
	}

	// 2026-07-20: 유닛 사망/오브젝트 회수·파괴 시 이 관찰자가 들고 있던 해당 대상 기록을 정리한다.
	// perceptionRecords는 "한 번 본 대상은 세션 내내 안 지워지는" 구조였는데(원래 스코프였던 관찰자
	// 개인당 소수 항목 가정과 달리, 웨이브가 반복되며 죽은 몬스터 참조가 계속 쌓이는 실사용 환경에서
	// 예상보다 훨씬 크게 자라 — 프레임 드롭의 실제 원인이었다), IsAlert 카운터 O(1)화와 별개로 이
	// 정리가 없으면 순회 비용(UpdateFOV 끝의 sweep 등) 자체가 계속 커진다.
	public void RemovePerceptionRecord(object key)
	{
		if (perceptionRecords.TryGetValue(key, out var record))
		{
			NotifyPerceptionSuspiciousChanged(record.PendingSuspiciousInvestigation, false);
			perceptionRecords.Remove(key);
		}
	}

	// 5장: 인지 판정 자체가 불가능한 상태. 문서는 기절/수면/마비/행동불능 4종을 들지만, 이 코드베이스엔
	// 아직 스턴(stunDuration) 외의 상태이상 시스템이 없다(수면/마비/행동불능은 담당 상태이상 문서
	// 부재로 미구현) — 그 상태들이 생기면 이 프로퍼티에 조건만 추가하면 된다.
	public bool CanPerceive => stunDuration <= 0f;

	// 9장: 정신력 보정(인류 전용, 몬스터는 항상 0) — PerceptionMath.MentalCorrectionForHuman 참고.
	public float GetMentalVisibilityCorrection() => (this is Human) ? PerceptionMath.MentalCorrectionForHuman(mental, maxMental) : 0f;

	// 01장 7절/01-A 7장: 시야 범위 안 + 인지 범위 밖 + 비어있지 않은 타일 목록(이번 UpdateFOV 호출
	// 기준 임시 스냅샷 — 저장값 아님, 매 UpdateFOV마다 비우고 다시 채운다). 목표/경로 재설정을 다루는
	// 10_목표설정·이동경로·재설정 문서가 아직 폴더에 없어 이 리스트를 실제로 소비하는 곳은 없다 —
	// 그 문서가 생기면 VisionMath.NonEmptyTileTempWeight와 함께 바로 쓸 수 있도록 데이터만 미리 채워둔다.
	public List<Vector3Int> visionOnlyNonEmptyTiles = new List<Vector3Int>();

	// 01-A 9장: 공격한 유닛은 고정 시간(5초) 동안 가시성이 +10 상승한다. 재공격 시 지속시간만
	// 초기화되고 상승량은 누적되지 않는다(문서가 "지속시간을 다시 5초로 초기화"라고만 명시할 뿐
	// "상승량이 추가된다"고는 하지 않아, 상한 100 규칙과 함께 가장 단순하게 해석한 것 — 판단 근거는
	// 구현현황 문서에 기재).
	public bool IsVisibilityBoosted => attackVisibilityBoostTimer > 0f;
	public void TriggerAttackVisibilityBoost() => attackVisibilityBoostTimer = VisionMath.AttackVisibilityBoostDuration;
	public float GetFinalVisibility() => VisionMath.FinalVisibility(baseVisibility, stealth, IsVisibilityBoosted);

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

	// 스탯 적용은 이제 UnitGenerate가 스폰한 프리팹의 UnitVisualDefinition.ApplyStatsTo(unit)이
	// SetupStats() 호출 전에 담당한다. 여기서는 그 기본 스탯으로부터 파생 스탯만 계산한다.
	public void SetupStats()
	{
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
	public abstract bool CanMove(Vector2Int pos, bool ignoreUnits = false);
	public abstract void Move(Dir dir);

	public bool IsEnemy(Unit other)
	{
		if (this.FactionBehavior != null && other.FactionBehavior != null)
		{
			if (this.FactionBehavior is PlayerUnitBehavior && other.FactionBehavior is WildMonsterBehavior) return true;
			if (this.FactionBehavior is WildMonsterBehavior && other.FactionBehavior is PlayerUnitBehavior) return true;
		}
		
		// 기존 레거시 체크 (안전망)
		return (this is Human && other is Monster) || (this is Monster && other is Human);
	}

	public virtual void ForceMove(Vector2Int targetPos)
	{
		if (_gameSession == null) return;

		Vector3Int oldKey = new Vector3Int(position.x, position.y, currentFloor);
		if (_gameSession.unitGrid.ContainsKey(oldKey))
			_gameSession.unitGrid.Remove(oldKey);

		position = targetPos;

		Vector3Int newKey = new Vector3Int(position.x, position.y, currentFloor);
		_gameSession.unitGrid[newKey] = this;
	}

	public abstract void UpdateFOV(List<Unit> allUnits);

	// 01-A 11장: 시야 방향 전환 우선순위 판정 — 이번 턴에 활성화된 후보들 중 가장 높은 우선순위를
	// 골라 currentDir를 갱신한다. GameSession.ProcessUnitAction이 ExecuteAction() 이후, UpdateFOV()
	// 이전에 호출한다(그래야 이동으로 갱신된 currentDir를 "이동 중" 후보의 기본값으로 활용할 수 있다).
	public abstract void ResolveVisionDirection();

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
	// 개인 지도(타일/오브젝트/몬스터 목격/방 위험도·흥미도) — 지도관련_정리 문서 기준 "지도는
	// 인류만 들고 있어야 한다"는 지시에 따라 Human에만 둔다(Monster/base Unit에는 없음).
	public PersonalMapKnowledge personalMap = new PersonalMapKnowledge();
	public System.Collections.Generic.List<string> collectedObjects = new System.Collections.Generic.List<string>();

	// 이 유닛이 속한 파티(있다면) — 13장 파티 전멸/6장 웨이브 종료 생존자 반영 판정에 쓰인다.
	// GameSession.CreateParty()가 파티 생성 시 채워준다. 파티 없이 스폰된 인류(디버그 단독 소환
	// 등)는 null로 유지 — 파티 관련 판정 대상에서 자연히 제외된다.
	public Party party;

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
