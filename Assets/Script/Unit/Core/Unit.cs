using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Cysharp.Threading.Tasks;

public abstract class Unit : ScriptableObject {
    public List<IUnitComponent> Components = new List<IUnitComponent>();

    public T GetComponent<T>() where T : class, IUnitComponent
    {
        foreach (var c in Components)
        {
            if (c is T tc) return tc;
        }
        return null;
    }

    // Components 선형 탐색 비용을 없애는 캐시 — OnEnable에서 null 리셋 → 첫 접근 시 ??=로 채워져 이후 O(1).
    private HealthComponent       _healthComp;
    private CombatStateComponent  _combatStateComp;
    private CombatStatComponent   _combatStatComp;
    private PerceptionComponent   _perceptionComp;
    private VisionStatComponent   _visionStatComp;
    private BaseStatComponent     _baseStatComp;
    private StatusEffectsComponent _statusEffectsComp;
    private AIStateComponent      _aiStateComp;
    private MemoryComponent       _memoryComp;
    private PartyComponent        _partyComp;
    private PropagationComponent  _propagationComp;

    public HealthComponent        Health        => _healthComp        ??= GetComponent<HealthComponent>();
    public CombatStateComponent   CombatState   => _combatStateComp   ??= GetComponent<CombatStateComponent>();
    public CombatStatComponent    CombatStat    => _combatStatComp    ??= GetComponent<CombatStatComponent>();
    public PerceptionComponent    Perception    => _perceptionComp    ??= GetComponent<PerceptionComponent>();
    public VisionStatComponent    VisionStat    => _visionStatComp    ??= GetComponent<VisionStatComponent>();
    public BaseStatComponent      BaseStat      => _baseStatComp      ??= GetComponent<BaseStatComponent>();
    public StatusEffectsComponent StatusEffects => _statusEffectsComp ??= GetComponent<StatusEffectsComponent>();
    public AIStateComponent       AIState       => _aiStateComp       ??= GetComponent<AIStateComponent>();
    public MemoryComponent        Memory        => _memoryComp        ??= GetComponent<MemoryComponent>();
    public PartyComponent         UnitParty     => _partyComp         ??= GetComponent<PartyComponent>();
    public PropagationComponent   Propagation   => _propagationComp   ??= GetComponent<PropagationComponent>();

    // 코드 전역에서 bare 이름으로 쓰이는 필드들. 실제 데이터는 컴포넌트에 있고 여기서 위임만 한다.
    public float hp               { get => Health.hp;                          set => Health.hp = value; }
    public float maxHp            { get => Health.maxHp;                       set => Health.maxHp = value; }
    public float HPRegen          { get => BaseStat.HPRegen;                   set => BaseStat.HPRegen = value; }
    public float spotting         { get => VisionStat.spotting;                set => VisionStat.spotting = value; }
    public float physicalAttack   { get => CombatStat.physicalAttack;          set => CombatStat.physicalAttack = value; }
    public float mental           { get => BaseStat.mental;                    set => BaseStat.mental = value; }
    public float maxMental        { get => BaseStat.maxMental;                 set => BaseStat.maxMental = value; }
    public float baseDanger       { get => BaseStat.baseDanger;                set => BaseStat.baseDanger = value; }
    public bool  isHitThisTurn    { get => CombatState.State.isHitThisTurn;    set => CombatState.State.isHitThisTurn = value; }
    public bool  oneTimeReactUsed { get => CombatState.State.oneTimeReactUsed; set => CombatState.State.oneTimeReactUsed = value; }
    public HashSet<Unit> personalSpottedEnemies => Perception.State.personalSpottedEnemies;
    public HashSet<Vector3Int> visionOnlyNonEmptyTiles => Perception.State.visionOnlyNonEmptyTiles;
    public ThreatTileData currentThreat { get => AIState.currentThreat; set => AIState.currentThreat = value; }

    private void OnEnable()
    {
        // 에디터 핫리로드나 재활성화 시 캐시가 이전 인스턴스를 물고 있지 않도록 초기화
        _healthComp = null; _combatStateComp = null; _combatStatComp = null;
        _perceptionComp = null; _visionStatComp = null; _baseStatComp = null;
        _statusEffectsComp = null; _aiStateComp = null; _memoryComp = null; _partyComp = null;
        _propagationComp = null;

        if (Components == null) Components = new List<IUnitComponent>();
        if (CombatStat == null) Components.Add(new CombatStatComponent(this));
        if (Health == null) Components.Add(new HealthComponent(this));
        if (VisionStat == null) Components.Add(new VisionStatComponent(this));
        if (BaseStat == null) Components.Add(new BaseStatComponent(this));
        if (Perception == null) Components.Add(new PerceptionComponent(this));
        if (Memory == null) Components.Add(new MemoryComponent(this));
        if (UnitParty == null) Components.Add(new PartyComponent(this));
        if (AIState == null) Components.Add(new AIStateComponent(this));
        if (CombatState == null) Components.Add(new CombatStateComponent(this));
        if (StatusEffects == null) Components.Add(new StatusEffectsComponent(this));
        if (Propagation == null) Components.Add(new PropagationComponent(this));
    }
	public static FactionData humanFactionData  = new FactionData();
	public static FactionData monsterFactionData = new FactionData();

	// UnitGenerate가 ScriptableObject.CreateInstance 직후 IObjectResolver.Inject(this)로 채워준다 —
	// DI 컨테이너가 직접 닿지 않는 순수 C# 로직도 Unit을 통해 서비스에 접근한다.
	[Inject] private UnitGenerate _unitGenerate;
	[Inject] private VFXManager _vfxManager;
	[Inject] private InputManager _inputManager;
	[Inject] private GameSession _gameSession;
	[Inject] private HumanKnowledgeBase _knowledgeBase;

	public UnitGenerate Generate => _unitGenerate;
	// UIManager가 Haare ICustomPanel로 전환되며 VContainer에 등록되지 않아 [Inject] 불가 — 정적 접근으로 대체.
	public UIManager UI => UIManager.Instance;
	public VFXManager VFX => _vfxManager;
	public InputManager InputMgr => _inputManager;
	public GameSession Session => _gameSession;
	public HumanKnowledgeBase Knowledge => _knowledgeBase;

	// HAARE 프레임워크(Native Routine)와 별개로: 유닛 소속과 행동 패턴을 결정하는 인터페이스
	public IFactionBehavior FactionBehavior { get; set; }

	public virtual bool IsHumanFaction => FactionBehavior is HumanFactionBehavior;
	public virtual bool IsPlayerMonsterFaction => FactionBehavior is PlayerMonsterBehavior;
	public virtual bool IsWildMonsterFaction => FactionBehavior is WildMonsterBehavior;


	// 전략 패턴: 유닛의 이동 알고리즘을 상황에 따라 갈아끼울 수 있는 구조
	public IMovementAlgorithm MovementAlgorithm { get; set; } = new AStarMovement();

	public UnitType unitType;

	// ─── 가중치 시스템(이해도/위험도/흥미도) 관련 — 대표 가중치 연산공식 문서 v0.7 ───
	public bool isSpecialUnit = false;     // 7-1장: 보스/네메시스 등 종별+개별 이해도를 함께 쌓는 특수 유닛 여부
	public bool isInterestTarget = false;  // 6-2장/18장 IsInterestTarget 플래그(이해도 상승에 따른 흥미도 감소 미적용)

	// 방 인구수 점유량 — Room.CurrentPopulation이 IsPlayerMonsterFaction만 걸러 합산하므로 인류/야생 유닛의 값은 의미가 없다.
	public int populationCost = 1;

	// "현재 소속 방" — UnitFunction.OnUpdate가 매 프레임 실제 위치 기준으로 동기화한다. 배회 몬스터는 동기화 대상이 아니라 항상 null.
	public Room currentRoom;

	// 4장: 각 유닛(인류 관측자)이 대상별로 갖고 있는 개인 가중치 기록.
	public readonly Dictionary<string, PersonalWeightRecord> personalWeights = new Dictionary<string, PersonalWeightRecord>();

	// 가장 최근에 나에게 피해를 입힌 유닛 — 사망 시점에 "누가 처치했는지"를 파악해 위험도/이해도 처치 이벤트를 기록할 수 있게 해준다.
	public Unit lastAttacker;

	// 함정 피해는 Unit이 아니라 InteractableObject가 가해자라 lastAttacker로 표현할 수 없어 별도 필드로
	// 둔다 — PartyDeathSystem이 이 필드로 "몬스터 피해 vs 함정 피해"를 구분한다.
	public InteractableObject lastTrapAttacker;

	// lastAttacker는 인류-몬스터 교차 히트 전용 가드 때문에 몬스터끼리 킬에서는 항상 null이라, 진영
	// 무관하게 "마지막 피해자"가 필요한 곳(방 소속 전환/처치 보상)은 이 필드를 쓴다.
	public Unit lastDamageDealer;

	// "수상한 타일"로 추적 중인 적(관찰자) 수 — O(N) 순회 대신 O(1) 조회를 위해 ForceRollPerception이 증감시킨다.
	protected int _suspiciousObserverCount;
	public void IncrementSuspiciousObserverCount() => _suspiciousObserverCount++;
	public void DecrementSuspiciousObserverCount() => _suspiciousObserverCount--;

	// ─── 레벨 및 성장 속성 ───
	public int level = 1;                 // ?꾩옱 ?덈꺼
	public int killCount = 0;             // ??泥섏튂 ??

	// ─── 정규화용 속성 ───
	public float concentration = 0f; // 집중. 정규화를 통해 산출해야 함
	public float MagicPower  = 0f; // 마력. 정규화를 통해 산출해야 함
	public float resistance  = 0f; // 저항. 정규화를 통해 산출해야 함
	public float leadership  = 0f; // 통솔. 정규화를 통해 산출해야 함

	// ─── 정규화 기준값 ───
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

	// ─── 유닛 배치 시스템 롤백 완료 ───

	// 웨이브 유닛이 다른 층으로 넘어가야 할 때 HumanWaveManager가 세팅 — 있고 현재 층과 다르면 최우선으로
	// 계단을 찾아 이동한다. Action_CrossStairs가 층을 넘기면 null로 되돌린다.
	public int? pendingStairTargetFloor = null;
	public Vector2Int? currentExplorationTarget = null;

	public Vector2Int? playerMoveTarget    = null;
	public bool isManualMoveCommand        = false; // 플레이어가 직접 클릭하여 내린 이동 명령인지 여부
	public Unit        playerAttackTarget   = null;

	// 플레이어가 지정한 공격 대상 오브젝트(코어/문) 위치 — playerAttackTarget과 동급이지만
	// InteractableObject는 위치 기반으로 추적한다.
	public Vector3Int? playerAttackObjectTarget = null;

	// 대기 상태(IdleFSMState)에서 야생 몬스터가 주변을 배회할 기준점 — 스폰 직후 한 번 세팅 후 불변.
	public Vector2Int? summonPosition = null;

	// IdleFSMState.OnEnter가 매번 재계산하는 배회 기준점과 다음 1칸 이동이 허용되는 시각 — 상태 전환마다 새로 계산되므로 마지막 값만 보관.
	public Vector2Int? idleAnchorPosition = null;
	public float idleNextMoveTime = 0f;

	// "집결 및 정지" 명령 — 지정 위치로 이동 후 정지. 이동이 끝나는 순간 이 플래그를 보고 isHalted로 전환한다.
	public bool pendingHaltOnArrival = false;

	// "정지"(동상) 상태 — 켜지면 UnitFSM.SelectState가 무조건 HaltFSMState로 고정, 완전 무반응(공격/
	// 스킬/회피 없음). 플레이어의 새 명령이나 "명령 취소"로만 해제된다.
	public bool isHalted = false;

	// "제자리 공격" 명령 — 이동이 걸리는 순간 함께 true가 된다. 이동 완료가 이 플래그를 보고 isStandGroundAttack으로 전환한다.
	public bool pendingStandGroundOnArrival = false;

	// 켜지면 UnitFSM.SelectState가 무조건 StandGroundAttackFSMState로 고정 — 이동은 절대 하지 않지만
	// 사거리 내 적은 공격한다(정지·완전 무반응과 다름).
	public bool isStandGroundAttack = false;

	// 유닛이 영구히 고정된 개체임을 뜻하는 플래그(보스 골렘용) — 명령 취소로 풀리지 않는 스폰 시점 고정
	// 성질이다. 효과는 isStandGroundAttack과 동일하며, walkSpeed=0은 이동 가능 여부와 무관하므로 고정 유닛은 반드시 이 플래그를 써야 한다.
	public bool isImmobile = false;

	// 전방위(360도) 시야(보스 골렘용) — 시야각/인지각 제한을 해제한다. isImmobile 유닛이 자리를 못 옮겨 등 뒤 적을 영영 인지 못하는 문제를 이걸로 우회한다.
	public bool hasOmnidirectionalVision = false;

	public Vector3Int? playerInteractTarget = null;
	public int         playerCommandStuckTurns = 0;

	// 코어/문 자동 파괴 접근 중 "인접도 못 하고 대체 자리도 없는" 상태가 몇 틱째 이어지는지 — 단 1틱
	// 실패만으로 경계로 전환하면 파괴↔경계가 매 틱 뒤집히므로, 연속 일정 틱 이상 막혀야 포기한다.
	public int tacticalObjectAttackStuckTurns = 0;
	public void SetMoveCommand(Vector2Int target, bool markHalt, bool markStandGround)
	{
		MovementAlgorithm?.ClearCache();
		isHalted = false;
		isStandGroundAttack = false;
		playerMoveTarget = target;
		isManualMoveCommand = true;
		playerAttackTarget = null;
		playerAttackObjectTarget = null;
		ClearAttackObjectTarget();
		pendingHaltOnArrival = markHalt;
		pendingStandGroundOnArrival = markStandGround;
	}

	public void SetAttackCommand(Unit target)
	{
		MovementAlgorithm?.ClearCache();
		playerAttackTarget = target;
		playerMoveTarget = null;
		playerAttackObjectTarget = null;
		ClearAttackObjectTarget();
		isHalted = false;
		isStandGroundAttack = false;
		// RoomConfinedMovement의 "플레이어 명령 중이면 방 경계·문 타일 제한을 우회" 예외가 오직 이
		// 플래그만 본다 — 빠지면 우클릭 공격 명령해도 자기 진영 문 타일조차 못 밟는다.
		isManualMoveCommand = true;
	}

	public void SetObjectAttackCommand(Vector3Int target)
	{
		MovementAlgorithm?.ClearCache();
		playerAttackObjectTarget = target;
		playerAttackTarget = null;
		playerMoveTarget = null;
		isManualMoveCommand = true;
		isHalted = false;
		isStandGroundAttack = false;
	}

	public void ClearPlayerCommand()
	{
		MovementAlgorithm?.ClearCache();
		playerMoveTarget = null;
		playerAttackTarget = null;
		playerInteractTarget = null;
		isManualMoveCommand = false;
		playerAttackObjectTarget = null;
		ClearAttackObjectTarget();
		isHalted = false;
		pendingHaltOnArrival = false;
		isStandGroundAttack = false;
		pendingStandGroundOnArrival = false;
	}

	public void FinishMoveCommand()
	{
		if (pendingHaltOnArrival)
		{
			pendingHaltOnArrival = false;
			isHalted = true;
		}
		if (pendingStandGroundOnArrival)
		{
			pendingStandGroundOnArrival = false;
			isStandGroundAttack = true;
		}
		playerMoveTarget = null;
		isManualMoveCommand = false;
		oneTimeReactUsed = false;
		playerCommandStuckTurns = 0;
	}

	public void AbortMoveCommand()
	{
		MovementAlgorithm?.ClearCache();
		playerMoveTarget = null;
		isManualMoveCommand = false;
		oneTimeReactUsed = false;
		playerCommandStuckTurns = 0;
	}

	public bool HasActivePlayerCommand()
	{
		return playerMoveTarget.HasValue || playerAttackTarget != null || playerInteractTarget.HasValue
			|| isManualMoveCommand || isHalted || pendingHaltOnArrival
			|| isStandGroundAttack || pendingStandGroundOnArrival
			|| playerAttackObjectTarget.HasValue;
	}

	public bool ContainsPos(int x, int y)
	{
		if (unitType == null) return false;
		int w = (int)unitType.footprint.x;
		int h = (int)unitType.footprint.y;
		return (x >= position.x && x < position.x + w &&
				y >= position.y && y < position.y + h);
	}

	public Vector2Int position;
	public int        currentFloor = 0;        // 현재 유닛의 위치 층 정보
	public Dir        currentDir   = Dir.DOWN;  // 현재 바라보는 방향 (시야 기준)
	public string     spriteVariation = "";     // 스프라이트 배리에이션 (라이브러리 카테고리명)



	// ─── 인지·정보판정·실패처리 시스템 관련 ───
	// 대상별 지속 인지 상태는 트리거 시점에만 갱신되며, personalSpottedEnemies와 달리 UpdateFOV 호출마다 Clear하지 않는다.

	// 수상한 타일 확인 대기 중인 인류가 하나라도 있으면 경계 상태 시 감지 보정과 시야 방향 전환 우선순위의 Alert 사유가 이 값을 참조한다.

	// 정신력 보정(인류 전용, 몬스터는 항상 0) — PerceptionMath.MentalCorrectionForHuman 참고.
	public bool CanPerceive => StatusEffects.State.stunDuration <= 0f;
	public float GetMentalVisibilityCorrection() => (this is Human) ? PerceptionMath.MentalCorrectionForHuman(BaseStat.mental, BaseStat.maxMental) : 0f;

	// 시야 범위 안 + 인지 범위 밖 + 비어있지 않은 타일 목록. 매 UpdateFOV마다 비우고 다시 채우는 임시
	// 스냅샷 — 목표/경로 재설정 문서가 아직 없어 완전히 소비하는 곳은 없다.

	// 공격 시도 중인 유닛은 고정 시간 동안 가시성이 상승한다. 재공격해도 지속시간만 초기화되고 상승치는 누적되지 않는다.
	public bool IsVisibilityBoosted => VisionStat.attackVisibilityBoostTimer > 0f;
	public void TriggerAttackVisibilityBoost() => VisionStat.attackVisibilityBoostTimer = VisionMath.AttackVisibilityBoostDuration;
	// 수상한 타일 추적 중 이동 1회당 누적된 값을 그대로 더한다.
	public float GetFinalVisibility() => VisionMath.FinalVisibility(VisionStat.baseVisibility, VisionStat.stealth, IsVisibilityBoosted,
		VisionStat.suspiciousMoveBoostTimers.Count * ExplorationMath.SuspiciousTargetVisibilityBoostPerMove);

	// ─── 탐색반응·경계·조사·함정대응 시스템 관련 ────────────────────────────
	// 함정 대응/경계는 인류/몬스터 공통이라 base Unit에 둔다. 조사/대기/보호 포메이션은 인류 전용이라 Human 쪽에 둔다.
	public TrapInteractionState currentTrapInteraction; // null이면 함정 대응 중 아님
	public AlertSearchState     currentAlertSearch;      // null이면 경계 중 아님

	// 코어/문 공격 채널링 공용 필드 — 자동(코어 전용, 인류만)/플레이어 명령(코어+문) 양쪽이 인접 도착
	// 시 채우며, 값이 있는 동안 OnUpdate가 매 프레임 깎는다. 직접 대입하지 말고 항상
	// SetAttackObjectTarget/ClearAttackObjectTarget을 거칠 것 — 파괴 VFX 시작/종료가 물려 있다.
	public Vector3Int? currentAttackObjectTarget;

	// 파괴 채널링 VFX 인스턴스 — SetAttackObjectTarget/ClearAttackObjectTarget만 건드린다.
	private GameObject _attackObjectVfxInstance;

	// currentAttackObjectTarget을 설정하는 유일한 진입점 — 채널링 중 매 틱 같은 값이 재대입될 수 있어,
	// 값이 실제로 바뀔 때만 VFX를 새로 시작해 중복 재생을 막는다(looping이라 Clear 호출 시에만 멈춘다).
	public void SetAttackObjectTarget(Vector3Int pos)
	{
		if (currentAttackObjectTarget == pos) return;
		StopAttackObjectVfx();
		currentAttackObjectTarget = pos;
		_attackObjectVfxInstance = VFXManager.SpawnBlockBreakingVfx(this, pos);
	}

	// 코어/문 파괴 채널링 종료 — 파괴 VFX는 즉시 멈추지만 체력 진행 막대는 숨기지 않는다(회복이 실제로 시작되는 순간 처리된다).
	public void ClearAttackObjectTarget()
	{
		StopAttackObjectVfx();
		currentAttackObjectTarget = null;
	}

	private void StopAttackObjectVfx()
	{
		if (_attackObjectVfxInstance == null) return;
		VFXManager.StopBlockBreakingVfx(_attackObjectVfxInstance);
		_attackObjectVfxInstance = null;
	}

	// 유닛이 죽거나 소환 해제될 때 반드시 호출 — 진행 중이던 "월드 오브젝트 쪽" 임시 시각 요소는 유닛이
	// 살아서 매 프레임 OnUpdate를 돌아야만 정리되는 구조라 갑자기 사라지는 경로에서는 명시적으로 정리해야 한다.
	public void ClearTransientWorldVisuals()
	{
		ClearAttackObjectTarget();

		if (this is Human human && human.currentTrapInteraction != null)
		{
			var trap = human.currentTrapInteraction;
			if (trap.Phase == TrapPhase.Disarming)
			{
				if (trap.CachedProgressBar == null)
					trap.CachedProgressBar = Session?.GetObjectVisual(trap.TrapPosition)?.GetComponent<ObjectProgressBarVisual>();
				trap.CachedProgressBar?.SetProgress(0f, false);
			}
		}
	}

	// 조사·함정 해제 중 시야/인지 범위 50% 페널티 — UnitFunction.UpdateFOV가 시야·인지 거리/인지각 계산에 곱한다.
	public bool ExplorationPenaltyActive =>
		(currentTrapInteraction != null && currentTrapInteraction.PenaltyActive) ||
		(this is Human human && human.currentInvestigation != null && human.currentInvestigation.PenaltyActive);

	// "위협 콜라이더를 인지함" 중단 조건 — 텔레그래프(currentThreat)는 경고 목적이라 확률표를 다시
	// 거치지 않고 인지 범위 안의 적이 지금 공격 예고 중인지 raw로 확인한다.
	public bool HasPerceivedThreatCollider()
	{
		float perceptionDistance = VisionMath.AwarenessDistance(spotting);
		foreach (var enemy in personalSpottedEnemies)
		{
			if (enemy == null || enemy.hp <= 0 || enemy.currentThreat == null) continue;
			if (Vector2Int.Distance(position, enemy.position) <= perceptionDistance) return true;
		}
		return false;
	}

	// ─── 정규화 함수 ───
	// 0%~200% 범위로 클램프. 100%가 기준값과 일치하도록
	private float Normalize(float value, float baseValue)
	{
		if (baseValue <= 0f) return 0f;
		return Mathf.Clamp((value / baseValue) * 100f, 0f, 200f);
	}

	public void CalculateDerivedStats()
	{
		// ?뺢퇋??
		float nAtk      = Normalize(CombatStat.physicalAttack,    BASE_PHYSICAL_ATTACK);
		float nMatk     = Normalize(CombatStat.magicalAttack,     BASE_MAGICAL_ATTACK);
		float nHp       = Normalize(Health.maxHp,             BASE_MAX_HP);
		float nMp       = Normalize(Health.maxMp,             BASE_MAX_MP);
		float nPDef     = Normalize(CombatStat.physicalDefense,   BASE_PHYSICAL_DEF);
		float nMDef     = Normalize(CombatStat.magicalDefense,    BASE_MAGICAL_DEF);
		float nRegen    = Normalize(BaseStat.HPRegen,           BASE_HP_REGEN);
		float nStatus   = Normalize(BaseStat.statusResistance,  BASE_STATUS_RES);
		float nAtkSpd   = Normalize(CombatStat.attackspeed,       BASE_ATTACK_SPEED);
		float nReact    = Normalize(BaseStat.reaction,          BASE_REACTION);
		float nMove     = Normalize(BaseStat.walkSpeed,         BASE_WALK_SPEED);
		float nSpot     = Normalize(VisionStat.spotting,          BASE_SPOTTING);
		float nMental   = Normalize(BaseStat.mental,            BASE_MENTAL);
		float nLeadRange = Normalize(BaseStat.leadershipRange,  BASE_LEAD_RANGE);
		float nCharisma = Normalize(BaseStat.charisma,          BASE_CHARISMA);
		float nCrit     = Normalize(CombatStat.criticalChance,    BASE_CRIT);
		float nCdr      = Normalize(BaseStat.cooltimeReduction, BASE_CDR);

		// 기본 능력치 계산
		// 근력 = 물리 공격력 정규화
		BaseStat.sterngth = nAtk;

		// 내구 = 체력 45 + 물방 45 + 재생 10
		BaseStat.Durability = nHp * 0.45f + nPDef * 0.45f + nRegen * 0.10f;

		// 민첩 = 공속 35 + 이동 25 + 반응 40
		BaseStat.agility = nAtkSpd * 0.35f + nMove * 0.25f + nReact * 0.40f;

		// 집중 = 치명 60 + 쿨감 40
		concentration = nCrit * 0.60f + nCdr * 0.40f;

		// 마력 = 마공 60 + 마나 40
		MagicPower = nMatk * 0.60f + nMp * 0.40f;

		// 저항(몬스터 예외)
		if (this is Monster)
			resistance = nMDef * 0.5f + nStatus * 0.5f;
		else
			resistance = nMDef * 0.35f + nStatus * 0.35f + nMental * 0.30f;

		// 감각 = 감지
		BaseStat.sense = nSpot;

		// 통솔 = 지도범위 50 + 카리스마 50
		leadership = nLeadRange * 0.5f + nCharisma * 0.5f;
	}

	// 스탯 적용은 UnitVisualDefinition.ApplyStatsTo(unit)이 SetupStats() 호출 전에 담당한다.
	// 여기서는 그 기본 스탯으로부터 파생 스탯만 계산한다.
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

	// ─── 추상 메서드 ───
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

	public bool IsEnemy(Unit other) { return FactionBehavior != null && other.FactionBehavior != null && FactionBehavior.IsEnemy(other.FactionBehavior); }

	public virtual void ForceMove(Vector2Int targetPos)
	{
		if (_gameSession == null) return;

		Vector3Int oldKey = new Vector3Int(position.x, position.y, currentFloor);

		// Dodge/Blink는 A*를 거치지 않고 이 메서드로 직접 위치를 옮겨 RoomConfinedMovement의 방 경계
		// 검사를 우회하므로, 여기서 같은 규칙을 다시 적용한다. 문 타일은 한쪽 방 소속으로 잡혀
		// 경계 검사만으로 안 걸러지므로 IsDoorTile로 추가 차단한다.
		if (MovementAlgorithm is RoomConfinedMovement && !isManualMoveCommand
			&& _gameSession.roomGrid.TryGetValue(oldKey, out Room myRoom))
		{
			Vector3Int targetKey = new Vector3Int(targetPos.x, targetPos.y, currentFloor);
			if (!_gameSession.roomGrid.TryGetValue(targetKey, out Room targetRoom) || targetRoom != myRoom)
				return;
			if (_gameSession.IsDoorTile(targetKey))
				return;
		}

		// ForceMove는 A*/CanMove를 거치지 않는 예외 이동(회피/점멸)이라 점유 검사를 RegisterUnitPos에서
		// 다시 거치고, 실패하면 이동을 취소해 원래 자리에 남는다.
		_gameSession.UnregisterUnitPos(this, position);
		Vector2Int oldPos = position;
		position = targetPos;
		if (!_gameSession.RegisterUnitPos(this, targetPos))
		{
			position = oldPos;
			_gameSession.RegisterUnitPos(this, oldPos);
		}
	}

	public abstract void UpdateFOV(List<Unit> allUnits);

	// 시야 방향 전환 우선순위 결정 — 이번 틱 후보들 중 가장 급한 방향으로 currentDir를 갱신한다.
	// ExecuteAction() 이후, UpdateFOV() 이전에 호출해야 이동 갱신값을 기본값으로 쓸 수 있다.
	public abstract void ResolveVisionDirection();

	private UnitFSM _fsm;
	public UnitFSM fsm { get { if (_fsm == null) _fsm = new UnitFSM(); return _fsm; } }

	public virtual void JudgeState()
	{
		if (StatusEffects.State.stunDuration > 0f) return; // 스턴 중 행동 차단
		fsm.SelectState(this);
	}

	public virtual void ExecuteAction()
	{
		if (StatusEffects.State.stunDuration > 0f) return;
		fsm.RunCurrentState(this);
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
	public abstract void ApplyDirectDamage(Unit attacker, float multiplier = 1f);
	public abstract List<ThreatTileData> DetectThreats();
	public abstract bool IsInThreat(Vector2Int pos, ThreatTileData threat);
	public abstract bool RollCritical(bool canCritical);
	public abstract float ApplyCriticalDamage(float rawDamage);
}

public class Human : UnitFunction
{
    public Human()
    {
        FactionBehavior = new HumanFactionBehavior();
    }

	// 개인 지도(오브젝트/몬스터 목격/방 위험도·흥미도) — "지도는 인류만 갖고 있어야 한다"는 지침에 따라 Human에만 둔다.
	public PersonalMapKnowledge personalMap => Memory.personalMap;
	public List<string> collectedObjects    => Memory.collectedObjects;

	// 이 유닛에 한정된 파티(있다면) — GameSession.CreateParty()가 채워준다. 파티 없이 스폰된 인류는 null로 남아 파티 관련 산정에서 자연히 제외된다.
	public Party party { get => UnitParty.party; set => UnitParty.party = value; }

	// 조사/대기/보호 포메이션 — 인류 전용(몬스터는 "컨셉에 따라"만 명시돼 있어 컨셉 시스템이 생기기
	// 전까지는 인류만 구현). null이면 각각 진행 중 아님.
	public InvestigationState currentInvestigation;
	public WaitState          currentWait;
	public FormationState     currentFormation;
	// 전투 진입 시 합류 대기 — null이면 대기 중 아님(즉시 전투).
	public JoinCombatWaitState currentJoinCombatWait;
	// 9-3장: 합류 의사 응답을 못 받고 2초 타임아웃된 적 — 이후로는 같은 적 인스턴스에 한해 합류 대기를
	// 다시 시도하지 않고 곧장 전투로 들어간다(PropagationSystem.ShouldDeferForJoinWait 참고). 이게 없으면
	// 거리·위험도 조건이 그대로인 한 타임아웃 직후 같은 조건으로 StartJoinCombatWait이 재호출돼 2초 대기가
	// 무한 반복된다(적이 실제로 접근하기 전까지 전투가 시작되지 않음).
	public Unit joinWaitGiveUpTarget;

	// 던전 입구 구조 — DungeonEntranceSystem이 0층 숨은 스폰 청크→1x3 입구→계단까지 파티 진형을 직접
	// 제어하는 동안 켜진다. 켜진 동안 NavigationFSMState의 자유탐색이 끼어들지 않는다.
	public bool isInDungeonEntranceSequence = false;

	// 보호 포메이션 형성 판정에 쓴다 — 함정 대응이나 조사 중이면(=호위할 만한 상황이면) true.
	// 함정 쪽은 "함정 위치에 실제로 도달했을 때"만 true다 — 이동 중(도착 전)엔 형성되면 안 되기 때문.
	public bool IsInteracting => IsActivelyHandlingTrap() || currentInvestigation != null;

	private bool IsActivelyHandlingTrap()
	{
		var trap = currentTrapInteraction;
		if (trap == null) return false;
		// 도달 판정 반경은 TacticalFSMState.MoveToTrap과 정확히 일치해야 한다 — 어긋나면 "이동은
		// 끝났는데 아직 상호작용 중이 아닌" 프레임이 생긴다.
		Vector2Int trapPos = new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);
		return Mathf.Max(Mathf.Abs(position.x - trapPos.x), Mathf.Abs(position.y - trapPos.y)) <= 1;
	}

	// "비목표 상호작용 중 보호 유닛 피격 → 포메이션 해제 후 전투 또는 경계". 같은 파티에서 이 유닛을
	// 호위 중인 멤버 중 이번 턴 피격당한 사람이 있는지 확인 — 호위하는 쪽만 참조를 들고 있어 역방향으로 순회한다.
	public bool AnyEscortHitThisTurn()
	{
		if (party == null) return false;
		foreach (var m in party.Members)
		{
			if (m == null || m == this) continue;
			if (m.currentFormation != null && m.currentFormation.EscortTarget == this && m.isHitThisTurn) return true;
		}
		return false;
	}

	// Goal_Investigate.GetPriority와 Action_MoveToInvestigateTarget이 공유하는 헬퍼 — 함정이 아니고
	// 이미 자동 확인 완료된 시체/전멸흔적도 아닌, 아직 조사되지 않은 가장 가까운 오브젝트를 찾는다.
	// 여러 호출부가 각자 전체를 훑어 프레임 드랍의 원인이었으므로 프레임 단위로 캐시한다.
	private int _investigateTargetCacheFrame = -1;
	private InteractableObject _investigateTargetCache;

	public InteractableObject FindInvestigateTarget()
	{
		if (_investigateTargetCacheFrame == Time.frameCount) return _investigateTargetCache;
		_investigateTargetCacheFrame = Time.frameCount;
		_investigateTargetCache = ComputeInvestigateTarget();
		return _investigateTargetCache;
	}

	private InteractableObject ComputeInvestigateTarget()
	{
		if (Session == null) return null;

		InteractableObject best = null;
		float bestDist = float.MaxValue;

		foreach (var obj in Session.objectGrid.Values)
		{
			if (obj == null || obj.IsCollected || obj.IsInvestigated) continue;
			if (obj.Position.z != currentFloor) continue;
			if (!personalMap.IsObjectKnown(obj.Id)) continue;

			bool isTrap = false;
			bool isCorpse = false;
			bool isHumanTag = false;
			bool isTrace = false;
			bool isCore = false;
			bool isDoor = false;
			foreach (var tag in obj.Tags)
			{
				if (tag.Contains("Trap")) isTrap = true;
				if (tag.Contains("Corpse")) isCorpse = true;
				if (tag == "Human") isHumanTag = true;
				if (tag.Contains("WipeoutTrace")) isTrace = true;
				if (tag == "Object/Passable/Core") isCore = true;
				if (tag.Contains("Door")) isDoor = true;
			}
			// 파티원 시체는 조사 대상 유지, 몬스터 시체/전멸 흔적은 제외(CastRay가 인지 즉시 확인
			// 완료). 코어는 리더 전용 조사 대상이라 제외, 문은 통행용 배경 오브젝트라 제외.
			bool isExcludedTrace = isTrace || (isCorpse && !isHumanTag);
			if (isTrap || isExcludedTrace || isCore || isDoor) continue;
			// 재전파 수신자는 같은 대상에 중복 조사·해제 목표를 선택하지 않는다 — 다른 파티원이 이미
			// 이 오브젝트를 currentInvestigation으로 잡고 있으면 후보에서 뺀다.
			if (IsInvestigationClaimedByPartyMember(obj.Id)) continue;

			float d = Vector2Int.Distance(position, new Vector2Int(obj.Position.x, obj.Position.y));
			if (d < bestDist) { bestDist = d; best = obj; }
		}
		return best;
	}

	private bool IsInvestigationClaimedByPartyMember(string objectId)
	{
		if (party == null) return false;
		foreach (var m in party.Members)
		{
			if (m == null || m == this || m.hp <= 0) continue;
			if (m.currentInvestigation != null && m.currentInvestigation.TargetObjectId == objectId) return true;
		}
		return false;
	}

	// 조사 개시 조건 중 "비전투/비도주"만 여기서 함께 확인한다(정확 인지/선택/도달은 위
	// FindInvestigateTarget과 Action_MoveToInvestigateTarget.Execute의 이동 로직이 담당).
	public bool HasReachableInvestigateTarget()
	{
		if (personalSpottedEnemies.Count > 0) return false; // 비전투 — 적이 보이면 조사 시작 안 함(전투 우선)
		return FindInvestigateTarget() != null;
	}

	// 상호작용 유닛을 시야에서 직접 확인한 것만으로는 참여하지 않고, PropagationSystem.
	// NotifyInteractionStarted가 전파한 정보를 실제로 받은 파티원만 후보가 된다.
	public Human FindDirectlyVisibleInteractingAlly()
	{
		if (party == null || Session == null) return null;

		Human best = null;
		float bestDist = float.MaxValue;

		foreach (var m in party.Members)
		{
			if (m == null || m == this || m.hp <= 0 || m.currentFloor != currentFloor) continue;
			if (!m.IsInteracting) continue;
			if (!PropagationSystem.HasReceivedInteractionNotice(this, m)) continue;
			// 이미 다른 유닛을 호위 중이면 그 대상이 아닌 새 상호작용 유닛으로는 갈아타지 않는다("기존 포메이션 유지").
			if (currentFormation != null && currentFormation.EscortTarget != null && currentFormation.EscortTarget != m) continue;

			float d = Vector2Int.Distance(position, m.position);
			if (d < bestDist) { bestDist = d; best = m; }
		}
		return best;
	}

	// 근접·원거리 배치 분기 — Action_EngageEnemy.ExecuteSkillActionBased가 이미 쓰는 "최대 스킬 사거리" 판정을 그대로 재사용한다.
	public bool IsRangedFormationRole()
	{
		var skills = Generate != null ? Generate.GetSkills(unitType.typeName) : null;
		if (skills == null) return false;
		int maxRange = 0;
		foreach (var s in skills)
		{
			if (s != null && s.HitRange > maxRange) maxRange = s.HitRange;
		}
		return maxRange >= ExplorationMath.FormationRangedHitRangeThreshold;
	}

	// "기존 포메이션 유지" + "직접 시야 확인" 트리거 — Goal_ProtectiveFormation.GetPriority와
	// GoapWorldState.Build가 같은 판정을 따로 구현하지 않도록 공유한다.
	public bool HasProtectiveFormationNeed()
	{
		if (currentFormation != null && currentFormation.EscortTarget != null && currentFormation.EscortTarget.IsInteracting) return true;
		return FindDirectlyVisibleInteractingAlly() != null;
	}

	// GoapAction.MoveToEscortSlot과 동일한 배치 공식 — atEscortSlot 판정이 실제 이동 목표와 어긋나지
	// 않도록 공유한다. 근접="전방"/원거리="후방"이라 부호가 반대다. facing.x/y는 GetDirVector가 이미
	// -1/0/1로 정규화해서 주므로 그대로 캐스트한다 — Mathf.Sign(0)이 1을 반환해 Sign()을 쓰면 수직/수평 정면에서 밀리는 버그가 있다.
	public Vector2Int GetEscortSlotPosition(Human escortTarget, float backDistance)
	{
		Vector2 facing = GetDirVector(escortTarget.currentDir);
		if (facing == Vector2.zero) facing = Vector2.down;

		// 상호작용 유닛이 아직 이동 중일 때 근접 호위를 "전방"에 두면 좁은 통로에서 서로 길을 막는
		// 교착이 생긴다 — 이동 중엔 후방(따라가기)으로 배치하고, 도착 후 실제 상호작용이 시작돼야 전방/후방 배치를 적용한다.
		bool activelyInteracting = IsEscortTargetActivelyInteracting(escortTarget);

		Vector2Int offset;
		if (!activelyInteracting)
		{
			offset = new Vector2Int(Mathf.RoundToInt(-facing.x), Mathf.RoundToInt(-facing.y)); // 이동 중 — 후방에서 뒤따름
		}
		else
		{
			offset = backDistance <= 1f
				? new Vector2Int((int)facing.x, (int)facing.y)
				: new Vector2Int(
					Mathf.RoundToInt(-facing.x * backDistance),
					Mathf.RoundToInt(-facing.y * backDistance));
		}

		Vector2Int slot = escortTarget.position + offset;

		// "전방(근접 배치)" 슬롯이 상호작용 오브젝트 자신의 타일과 겹칠 수 있어(Passable이라 밟을 수
		// 있음), 폴백 순서(전방→좌우)대로 겹치면 한 칸 더 물러나고 그래도 겹치면 옆으로 민다.
		Vector2Int? interactionPos = GetInteractionObjectPosition(escortTarget);
		if (interactionPos.HasValue && slot == interactionPos.Value)
		{
			Vector2Int farther = escortTarget.position + new Vector2Int((int)facing.x * 2, (int)facing.y * 2);
			slot = farther != interactionPos.Value ? farther : slot + new Vector2Int(-(int)facing.y, (int)facing.x);
		}

		return slot;
	}

	// escortTarget이 실제로 상호작용(조사 진행/함정 해제 진행)을 시작했는지 — 아직 목적지로 "이동
	// 중"인 단계와 구분한다(위 GetEscortSlotPosition 주석 참고).
	private bool IsEscortTargetActivelyInteracting(Human escortTarget)
	{
		if (escortTarget.currentInvestigation != null) return escortTarget.currentInvestigation.PenaltyActive;
		if (escortTarget.currentTrapInteraction != null) return escortTarget.currentTrapInteraction.PenaltyActive;
		return false;
	}

	// 위 GetEscortSlotPosition이 겹침 판정에 쓰는 "이 유닛이 지금 상호작용 중인 오브젝트의 위치".
	private Vector2Int? GetInteractionObjectPosition(Human escortTarget)
	{
		if (escortTarget.currentInvestigation != null)
			return new Vector2Int(escortTarget.currentInvestigation.TargetPosition.x, escortTarget.currentInvestigation.TargetPosition.y);
		if (escortTarget.currentTrapInteraction != null)
			return new Vector2Int(escortTarget.currentTrapInteraction.TrapPosition.x, escortTarget.currentTrapInteraction.TrapPosition.y);
		return null;
	}

	public override void JudgeState()
	{
		base.JudgeState();
		// 인류 상태 판단 로직 추가
	}
}

public class Monster : UnitFunction
{
    public Monster()
    {
        FactionBehavior = new PlayerMonsterBehavior();
    }

	// 몬스터용 개인 지도 — Human.personalMap과 동일한 노출 패턴, 다만 담는 내용은 지형 밝히기 + 함정
	// 위치뿐(가중치 없음). UnitFunction.CastRay가 채운다.
	public MonsterMapKnowledge monsterMap => Memory.monsterMap;

	public override void JudgeState()
	{
		base.JudgeState();
		// 몬스터 상태 판단 로직 추가
	}
}





