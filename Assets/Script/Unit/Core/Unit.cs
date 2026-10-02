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

    // 길찾기(AStarMovement)가 읽는 "이 유닛이 직접 확인했거나 전달받아 아는 지형"(검증 04-08) — 인류는 개인 지도, 몬스터는 몬스터 지도. 그 밖이면 null(진영 공용 지도로 폴백).
    public IKnownTerrain KnownTerrain => this is Human ? (IKnownTerrain)Memory?.personalMap : this is Monster ? Memory?.monsterMap : null;
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

	// 점유 크기(정수 타일) — position이 좌하단이다(AStarMovement.IsTileWalkable의 풋프린트 순회와 같은 관례). unitType이 없으면 1×1.
	public Vector2Int FootprintSize => unitType == null
		? Vector2Int.one
		: new Vector2Int(Mathf.Max(1, (int)unitType.footprint.x), Mathf.Max(1, (int)unitType.footprint.y));

	// ─── 가중치 시스템(이해도/위험도/흥미도) 관련 — 대표 가중치 연산공식 문서 v0.7 ───
	public bool isSpecialUnit = false;     // 7-1장: 보스/네메시스 등 종별+개별 이해도를 함께 쌓는 특수 유닛 여부
	public bool isInterestTarget = false;  // 6-2장/18장 IsInterestTarget 플래그(이해도 상승에 따른 흥미도 감소 미적용)

	// 방 인구수 점유량 — Room.CurrentPopulation이 IsPlayerMonsterFaction만 걸러 합산하므로 인류/야생 유닛의 값은 의미가 없다.
	public int populationCost = 1;

	// "현재 소속 방" — UnitFunction.OnUpdate가 매 프레임 실제 위치 기준으로 동기화한다. 배회 몬스터는 동기화 대상이 아니라 항상 null.
	public Room currentRoom;

	// 4장: 각 유닛(인류 관측자)이 대상별로 갖고 있는 개인 가중치 기록.
	public readonly Dictionary<string, PersonalWeightRecord> personalWeights = new Dictionary<string, PersonalWeightRecord>();

	// 02번 문서 9번 항목: 종·스킬별 예상 공격 피해량(입장 기록 근사치 또는 직접 경험 확정치).
	public readonly Dictionary<string, PersonalSkillDamageRecord> personalSkillDamage = new Dictionary<string, PersonalSkillDamageRecord>();

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
	// 검증문서 01-11 6행: 일반 조사 대상 접근이 몇 틱째 막혀 있는지 — 위와 동일한 이유로 도입,
	// 대상이 진짜 도달 불가능하면(고립 구역/상대 진영 문 뒤 등) 포기하고 다른 후보로 넘어간다.
	public int investigateStuckTurns = 0;
	// 검증문서 03-11: 함정 접근 이동(MoveToTrap/TrapDestroy 공용)이 몇 틱째 막혀 있는지 — 위와 같은
	// 패턴. 함정이 도달 불가능하면(상대 진영 문 뒤 등) 포기하고 대응을 접는다.
	public int trapMoveStuckTurns = 0;
	// 검증문서 03-13: 이동 계층(AStarMovement)이 "알려진 함정 구역 때문에만 도달하지 못한다"고 진단한 함정과 그 시각 — TrapPartySystem.
	// TickBlockedPathResponse(비전투)와 CombatFSMState.ChaseTarget(전투)이 소비한다. nextTrapBlockDiagTime은 진단 탐색 스로틀.
	public string trapBlockTrapId;
	public float  trapBlockSignalTime = float.NegativeInfinity;
	public float  nextTrapBlockDiagTime;
	// 검증문서 03-13(v0.6 9-6): 구역 탈출(TacticalFSMState.ZoneEscape)이 막혀 못 나갈 때 잠시 시도를 끄는 시각과 정체 카운터 — 안 끄면
	// TrapAvoidance.NeedsZoneEscape가 계속 참이라 Tactical을 붙든 채 얼어붙는다.
	public float  trapZoneEscapeSuppressUntil;
	public int    trapEscapeStuckTurns;
	// 검증문서 03-13: 막힘 신호로 함정 대응을 다시 연 뒤 이 유닛이 또 열 수 있게 되는 시각(TrapPartySystem.TickBlockedPathResponse).
	public float  trapBlockResponseCooldownUntil;
	// 자유탐색(NavigationFSMState.RandomExplore) 중 다음 걸음이 아군에게 막히는 경우를 위 두 필드와
	// 동일한 패턴으로 처리 — 안 하면 좁은 통로 코너에서 여러 유닛이 서로를 영구히 막을 수 있다.
	public int exploreStuckTurns = 0;
	// 검증문서 02-02: 전투 추격(SelectAttackTarget이 고른 AttackTarget으로 접근) 중 체비셰프 거리가
	// 줄지 않는 턴 수 — 위 세 필드와 동일한 패턴. 상대 진영 문 뒤의 적처럼 실제 도달 불가능한 대상을
	// 무한정 추격하는 걸 막는다.
	public int combatChaseStuckTurns = 0;
	// 위 카운터가 한도를 넘으면 이 대상을 combatUnreachableUntil까지 공격 후보에서 제외한다
	// (CombatFSMState.GetPriority/SelectAttackTarget 둘 다 확인) — Combat은 Tactical보다 우선순위가
	// 높아 대상을 그냥 놓아주기만 하면 다음 틱에 똑같이 재선택되므로, 이 배제가 있어야 Tactical
	// (DoorAttack 등)로 실제로 넘어간다.
	public Unit  combatUnreachableTarget;
	public float combatUnreachableUntil;

	// ─── 점유 충돌 판단(검증문서 04-05~04-07, OccupancySystem) ───
	// Move()가 마지막으로 성공한 시각 — 이 유닛이 점유자일 때 "이미 이동 중인 아군"인지 가르는 근거.
	public float lastMoveTime = float.NegativeInfinity;
	// 진행 중인 대기 결정(null = 대기 중 아님). GameSession.ProcessUnitAction이 OccupancySystem.ShouldHold로 읽어, 대기 중이면 이번 행동 주기의 ExecuteAction을 건너뛴다.
	public OccupancyHold occupancyHold;
	// 대기시간을 모르고 우회도 없는 채로 상한을 넘겨 대기를 포기한 점유자 — 그 점유자에 대해선 그 시각까지 다시 대기하지 않는다(기존 정체 인내로 넘어감).
	public Unit  occupancyGiveUpBlocker;
	public float occupancyGiveUpUntil;
	// 제자리 대기 중인 이 유닛에게 온 "비켜 달라" 요청(요청자·만료 시각) — OccupancySystem.TryHonorYield가 소비한다.
	public Unit  yieldRequester;
	public float yieldRequestUntil;
	// 좁은 통로 통과 순서의 동률용 무작위 값 — 유닛당 처음 필요할 때 한 번만 뽑아 계속 쓴다(매 판단 재추첨하면 순서가 흔들린다, 04번 8장).
	private int _passTieBreak;
	public int PassTieBreak
	{
		get
		{
			if (_passTieBreak == 0) _passTieBreak = UnityEngine.Random.Range(1, int.MaxValue);
			return _passTieBreak;
		}
	}
	// 다른 유닛 점유를 무시하는 "구조 경로" 전용 A*(이 유닛 이동 알고리즘의 쌍둥이) — OccupancySystem이 만들고 재사용한다.
	public AStarMovement occupancyTwin;
	public IMovementAlgorithm occupancyTwinSource;
	// 검증 04-08(RouteAssessment): 함정 회피를 끈 구조 경로 쌍둥이(함정 구역 때문에만 막힌 대상을 가려낸다)와, 구조 경로로도 닿을 수 없다고 판정한 대상의 판정 메모.
	// 메모 값은 판정 당시 서명(층, 아는 지형 개정, 아는 함정 수, 문 상태 개정)과 시각 — 서명이 같을 때만(또는 판정 직후 잠깐) 재탐색을 생략한다.
	public AStarMovement routeTrapOffTwin;
	public IMovementAlgorithm routeTrapOffTwinSource;
	public readonly Dictionary<Vector3Int, RouteMemoEntry> unreachableRouteMemo = new();
	// 검증 04-08 발견 5: 탐험(RandomExplore)이 길찾기로 닿지 못한 미탐색 목표의 막힘 기록 — 지형을 벽으로 위조하지 않고 같은 서명 규칙으로 다시 고르지 않는다(RouteAssessment.MarkExploreBlocked).
	public readonly Dictionary<Vector3Int, RouteMemoEntry> exploreBlockedTargets = new();
	// 이 프레임에 대기·비켜서기로 행동을 이미 썼는가 — 한 행동 주기에 이동 호출을 두 번 하는 리프가 방금의 물러나기를 되돌리지 않게 한다.
	public int occupancyActedFrame = -1;

	// 정당한 점유 대기가 행동별 정체 인내(조사·탐색·함정·코어 문 공격·플레이어 이동 명령·경계 접근)로 포기되지 않게, 대기를 시작·유지할 때 비전투 정체 카운터를 전부 0으로 되돌린다.
	// 각 행동의 리프가 대기 동안은 호출되지 않지만(ProcessUnitAction이 ExecuteAction을 건너뜀) 대기 시작 틱에 이미 한 번 호출됐을 수 있어 그 1틱도 지운다.
	public virtual void ResetNonCombatStuckCounters()
	{
		tacticalObjectAttackStuckTurns = 0;
		investigateStuckTurns = 0;
		trapMoveStuckTurns = 0;
		exploreStuckTurns = 0;
		playerCommandStuckTurns = 0;
		if (currentAlertSearch != null) currentAlertSearch.ApproachStuckTurns = 0;
	}

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

	// 개인 적용 이동속도(칸/초) — 경계 이동은 75% 감속(4-3장), 피격·비명·사망음 확인 접근은 긴급이라 정상 속도
	// (07문서 16-3장). 행동 주기(1/속도, GameSession.ProcessUnitAction)와 도착시간 추정이 같은 값을 쓴다.
	public float AppliedWalkSpeed
	{
		get
		{
			bool alertSlowdown = currentAlertSearch != null && !currentAlertSearch.IsExemptFromAlertSlowdown;
			return MovementBaseSpeed * (alertSlowdown ? ExplorationMath.AlertMoveSpeedRatio : 1f);
		}
	}

	// 경계 감속을 곱하기 전의 이동 기본 속도 — 기본은 개인 능력치. Human은 파티 공동 이동 중에 공동 속도로 바꾼다(검증 04-04, Party.ResolveMoveBaseSpeed).
	protected virtual float MovementBaseSpeed => BaseStat.walkSpeed;

	// 02번 문서(전투 목표와 아군 보호 및 지원) 관련 지속 상태 — 인류/플레이어몬스터/야생 공통이라
	// base Unit에 둔다. 필드 9개를 CombatTargetingState 하나로 묶어(캡슐화) 리셋도 그 안의
	// ResetRetargetTracking/ResetOnCombatExit로 일관되게 처리한다.
	public readonly CombatTargetingState CombatTargeting = new CombatTargetingState();

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

	// 검증문서 02-08 4번: 긴급 아군 보호 전용 — 이 유닛 "자신"의 인지 여부와 무관하게, 실제로 활성
	// 위협(공격 예고 중인 적의 히트박스)이 이 유닛과 겹치는지 ground truth로 확인한다. 위
	// HasPerceivedThreatCollider(자기 자신의 인지 기반 중단 조건, TacticalFSMState.CanDisarm 등)와는
	// 의도적으로 분리 — "내가 느끼는 위협"과 "보호가 필요한 아군이 실제로 처한 위협"은 다른 질문이다
	// (사각지대·기습 공격도 후자는 놓치면 안 된다). GameSession의 전역 위협 반응 판정(캐스터 히트박스
	// vs 대상 히트박스 Overlaps, GameSession.cs:1013-1016)과 동일한 기준 — 그쪽은 회피 등 반응
	// 트리거용, 이쪽은 부수효과 없는 순수 조회용이라 따로 둔다.
	public bool HasActiveThreatGroundTruth()
	{
		if (Session?.units == null) return false;
		Hitbox myHitbox = GetUnitHitbox(this);
		foreach (var u in Session.units)
		{
			if (u == null || u == this || u.hp <= 0 || u.currentFloor != currentFloor) continue;
			if (!IsEnemy(u) || u.currentThreat == null) continue;
			if (u.currentThreat.hitbox.Overlaps(myHitbox)) return true;
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
	// FSM을 아직 만들지 않았으면(스폰 직후·세션 없는 테스트) 새로 만들지 않고 null — 이동 모드 판정처럼 상태만 들여다보는 호출용.
	public IFSMState CurrentFsmStateIfCreated => _fsm?.CurrentState;

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

	// 파티 공동 이동(방 이동·문 찾기 추종) 중에는 합류한 구성원이 "가장 느린 구성원" 속도를 쓴다 — 자세한 기준은 Party.ResolveMoveBaseSpeed.
	protected override float MovementBaseSpeed => party != null ? party.ResolveMoveBaseSpeed(this) : base.MovementBaseSpeed;

	public override void ResetNonCombatStuckCounters()
	{
		base.ResetNonCombatStuckCounters();
		waitStuckTurns = 0;
	}

	// 이번 시야 패스에서 직접 본 같은 파티 아군 — UnitFunction.UpdateFOV가 매 패스 비우고 다시 채운다. 함정
	// 담당자의 도착처럼 "시야로 직접 확인한" 사실의 근거이며 전파 범위와 무관하다.
	public readonly HashSet<Human> visiblePartyMembers = new HashSet<Human>();

	// 조사/대기/보호 포메이션 — 인류 전용(몬스터는 "컨셉에 따라"만 명시돼 있어 컨셉 시스템이 생기기
	// 전까지는 인류만 구현). null이면 각각 진행 중 아님.
	public InvestigationState currentInvestigation;
	public WaitState          currentWait;
	// TacticalFSMState.ExecuteWait의 AwaitingPartyAtRallyPoint/ReportingCoreToLeader 분기에서
	// exploreStuckTurns와 동일한 패턴으로 쓴다 — currentWait의 Reason은 매번 하나뿐이라 필드를 공유한다.
	public int waitStuckTurns = 0;
	// 03번 10장·05번 8장(검증문서 03-15): 이 유닛이 "유효하게 아는" 리더 위치·집결 위치와 아직 리더에게 전하지 못한
	// 코어 발견 보고 의무 — 실제 리더 위치를 읽지 않고 보고 이동의 목적지를 정하는 근거다.
	public readonly KnownLeaderInfo knownLeader = new KnownLeaderInfo();
	public Vector2Int? knownRallyPoint;
	public Vector3Int? pendingCoreReportPos;
	// 다음 문을 모를 때 공동 탐색 추종(WaitReason.SearchingNextDoor)을 접은 뒤 다시 배정받을 수 있는 시각 — 배정·해제 반복 방지.
	public float nextSearchFollowAllowedTime;
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

	// v0.6 5-6·9-9·12-4장: 웨이브가 끝나면 유지 중이던 조사·해제 진행도를 제거한다 — 생존자는 다음 웨이브에 재사용되므로
	// 낡은 대응 상태(이미 없는 함정·조사 대상)를 들고 넘어가지 않게 한다. 진행 막대가 남지 않게 시각 요소를 먼저 정리한다.
	public void ClearInteractionProgress()
	{
		ClearTransientWorldVisuals();
		TrapPartySystem.EndResponse(this, TrapEndReason.WaveEnded); // 조율 기록의 담당 배정도 함께 풀린다(개별 탈출 시 파티가 이어지므로)
		currentInvestigation = null;
		// 웨이브를 넘어 재사용되는 생존자가 지난 파티의 리더·집결 정보나 미전달 보고를 들고 가지 않게 한다.
		pendingCoreReportPos = null;
		knownRallyPoint = null;
		knownLeader.Clear();
	}

	private bool IsActivelyHandlingTrap()
	{
		var trap = currentTrapInteraction;
		if (trap == null) return false;
		// 도달 판정 반경은 TacticalFSMState.MoveToTrap과 정확히 일치해야 한다 — 어긋나면 "이동은
		// 끝났는데 아직 상호작용 중이 아닌" 프레임이 생긴다.
		Vector2Int trapPos = new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);
		return Mathf.Max(Mathf.Abs(position.x - trapPos.x), Mathf.Abs(position.y - trapPos.y)) <= 1;
	}

	// 개인 탐색·조사가 머무를 방 — 지금 있는 방(통로·문 위치처럼 방이 없으면 직전 방). 개인이 임의로 다음 방에 들어가지 않는다(04번 10장 578줄·05번 1장 53줄·01번 585줄, 검증 05-01).
	// UnitFunction.SyncRoomAffiliation이 방이 바뀔 때 갱신한다.
	public Room lastKnownRoom;
	// HumanIdleSystem(할 일 없는 개인의 합류)의 목적지 해석 상태 — currentWait이 아니라 따로 둬서 BT 우선순위(조사·경계·대기)에 끼어들지 않는다. Step이 3초 넘게 안 불리거나 방이 바뀌면 새로 만든다.
	public WaitState idleWait;
	public float idleLastStepTime;
	public Room idleWaitRoom;

	// 개인 탐색·조사 후보를 가둘 방 범위 — 켜져 있고 방을 알면 그 방의 사각형(true), 아니면 제한 없음(false). 합류할 리더가 없는 인류(파티·리더 없음·리더 사망)와 문을 오래 못 찾은 리더(Party.LeaderMayExploreBeyondRoom)는 제한하지 않는다(05번 8장, 검증 05-07).
	public bool TryGetExplorationBounds(out RectInt bounds)
	{
		bounds = default;
		if (!(AIConfigLoader.Behavior?.roomBoundExplorationEnabled ?? true) || lastKnownRoom == null) return false;
		if (party == null || party.Leader == null || party.Leader.hp <= 0) return false;
		if (party.Leader == this && party.LeaderMayExploreBeyondRoom) return false;
		bounds = lastKnownRoom.Bounds;
		return true;
	}

	// 집결 명령·공동 이동 배정 때 부른다 — 시작 전 대상으로 이동 중인 조사는 접는다(05번 4장 245줄, 03번 1장 181줄). 대상은 개인 지도에 남고, 이미 시작한 조사(진행 중이거나 진행도가 남은 것)는 기존 유지·중단 조건대로 둔다. 접었으면 true(검증 05-04).
	public bool ReleaseUnstartedInvestigation()
	{
		var inv = currentInvestigation;
		if (inv == null || PartyFormationMath.IsInvestigationStarted(inv.PenaltyActive, inv.Progress01)) return false;
		currentInvestigation = null;
		investigateStuckTurns = 0;
		return true;
	}

	// 공동 이동(방 이동 계획·예전 공동 이동)에 편입될 때 정리할 개인 상태 — 미착수 조사 접기(05번 1장 31줄, 검증 05-04), 비전투 보호 포메이션 종료(05번 9장, 검증 05-08), 전파받은 적 위치 접근 경계 접기(03번 1장 50줄).
	public void BeginPartyMovement()
	{
		ReleaseUnstartedInvestigation();
		currentFormation = null;
		if (currentAlertSearch != null && currentAlertSearch.IsIndirectEnemyApproach) currentAlertSearch = null;
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

	// TacticalFSMState.CanInvestigate/MoveToInvestigateTarget이 공유하는 헬퍼 — 01번 문서 4~5장:
	// 알려진 오브젝트(회수물/시체/함정 등)와 시야로만 존재를 확인한 미확인 타일을 한데 모아
	// 흥미도×가중치÷거리 점수(PartyGoalMath.NonCombatGoalScore)로 가장 좋은 후보를 고른다.
	// 여러 호출부가 각자 전체를 훑어 프레임 드랍의 원인이었으므로 프레임 단위로 캐시한다.
	private int _investigateTargetCacheFrame = -1;
	private InvestigationState _investigateTargetCache;

	// 성능 튜닝값(밸런스 아님) — 후보가 많을 때 직선거리로 1차 정렬한 뒤, 통행 불가가 아닌 후보 이만큼만 실제 경로(RouteAssessment)로 정밀 채점한다.
	// 한 번의 후보 선정에서 통행 불가 메모에 적중하지 않은 실제 경로 탐색은 InvestigateMaxNewEvaluations회로 제한한다.
	private const int InvestigateShortlistSize = 5;
	private const int InvestigateMaxNewEvaluations = 8;
	private readonly RouteContext _routeContext = new RouteContext();
	private readonly List<(InteractableObject obj, Vector3Int tilePos, bool isTile)> _investigateCandidateBuffer = new();

	public InvestigationState FindInvestigateTarget()
	{
		if (_investigateTargetCacheFrame == Time.frameCount) return _investigateTargetCache;
		_investigateTargetCacheFrame = Time.frameCount;
		_investigateTargetCache = ComputeInvestigateTarget();
		return _investigateTargetCache;
	}

	private InvestigationState ComputeInvestigateTarget()
	{
		if (Session == null) return null;

		_investigateCandidateBuffer.Clear();

		// 개인이 임의로 다음 방에 들어가지 않는다(검증 05-01) — 조사 후보는 지금 있는 방 안의 오브젝트·타일뿐이다. 다른 방 대상은 리더의 방 이동 결정 뒤 그 방에 들어가서 후보가 된다.
		bool roomBound = TryGetExplorationBounds(out RectInt roomBounds);

		foreach (var obj in Session.objectGrid.Values)
		{
			if (obj == null || obj.IsCollected || obj.IsInvestigated) continue;
			if (obj.Position.z != currentFloor) continue;
			if (roomBound && !roomBounds.Contains(new Vector2Int(obj.Position.x, obj.Position.y))) continue;
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
			// 이 오브젝트를 currentInvestigation으로 잡고 있으면 후보에서 뺀다. 검증문서 03-02: 파티
			// 목표 오브젝트(IsPartyGoalObject)는 이 규칙의 예외다 — 03번 문서 3번 항목이 "합류"(여러
			// 파티원이 같은 대상으로 모이는 것)를 명시적으로 요구하므로, 일반 조사 대상에만 중복 방지를 적용한다.
			if (!IsPartyGoalObject(obj) && IsInvestigationClaimedByPartyMember(obj.Id)) continue;
			// 검증문서 03-13: 알려진 활성 함정의 인접 1칸 안에 있는 대상은 일반 이동으로 갈 수 없다 — 후보로 잡았다 접근 실패로 포기하기를 반복하지 않는다.
			if (TrapAvoidance.IsInKnownZone(this, new Vector2Int(obj.Position.x, obj.Position.y))) continue;

			_investigateCandidateBuffer.Add((obj, obj.Position, false));
		}

		// 01번 문서 4장: 시야로 존재만 확인했고 아직 안전 확인이 안 끝난 타일도 후보다. 그 위치에
		// 이미 오브젝트가 있으면 위 루프가 이미 대표하므로 중복 후보로 넣지 않는다(4장 "중복 가산
		// 방지").
		foreach (var tilePos in personalMap.KnownInterestTiles)
		{
			if (tilePos.z != currentFloor) continue;
			if (roomBound && !roomBounds.Contains(new Vector2Int(tilePos.x, tilePos.y))) continue;
			if (personalMap.GetObjectIdAtTile(tilePos) != null) continue;
			if (IsTileInvestigationClaimedByPartyMember(tilePos)) continue;
			if (TrapAvoidance.IsInKnownZone(this, new Vector2Int(tilePos.x, tilePos.y))) continue; // 위 오브젝트 후보와 같은 이유(03-13)

			_investigateCandidateBuffer.Add((null, tilePos, true));
		}

		if (_investigateCandidateBuffer.Count == 0) return null;

		// 1차: 직선거리 기준 정렬(비용 0) 후 앞에서부터 경로를 평가한다. 검증 04-08(04번 9장): 현재 정보로 통행 불가가 확인된 대상은 후보에서 빼고(RouteAssessment) 그 뒤의
		// 후보로 계속 내려가되, 평가가 끝난 후보가 InvestigateShortlistSize개가 되거나 이번 호출의 실제 탐색이 InvestigateMaxNewEvaluations회에 닿으면 멈춘다 — 매 틱 전체 후보로
		// 비용이 확대되지 않게 한다. 통행 불가 판정은 아는 정보가 그대로인 동안 유닛 메모가 기억해 다시 탐색하지 않는다.
		Vector2Int selfPos = position;
		_investigateCandidateBuffer.Sort((a, b) =>
			Vector2Int.Distance(selfPos, new Vector2Int(a.tilePos.x, a.tilePos.y))
				.CompareTo(Vector2Int.Distance(selfPos, new Vector2Int(b.tilePos.x, b.tilePos.y))));

		bool excludeUnreachable = AIConfigLoader.Behavior?.unreachableCandidateExclusionEnabled ?? true;
		_routeContext.Reset();
		InvestigationState best = null;
		float bestScore = 0f;
		int evaluated = 0;
		for (int i = 0; i < _investigateCandidateBuffer.Count
			&& evaluated < InvestigateShortlistSize
			&& _routeContext.NewEvaluations < InvestigateMaxNewEvaluations; i++)
		{
			var candidate = _investigateCandidateBuffer[i];
			RouteEstimate route = RouteAssessment.Assess(this, candidate.tilePos, false, _routeContext);
			if (excludeUnreachable && RouteMath.ShouldExcludeFromCandidates(route.Status)) continue;
			evaluated++;

			string objId = candidate.obj?.Id;
			float interest = personalMap.GetTileInterest(candidate.tilePos, objId);
			int distance = route.Tiles;
			float score = PartyGoalMath.NonCombatGoalScore(interest, PartyGoalMath.UniformPartyTypeWeightPlaceholder, distance);

			if (best == null || score > bestScore)
			{
				bestScore = score;
				best = new InvestigationState
				{
					TargetObjectId = objId,
					TargetPosition = candidate.tilePos,
					IsTileOnly = candidate.isTile,
					IsPartyGoalTarget = IsPartyGoalObject(candidate.obj),
				};
			}
		}

		// ShouldSwitchNonCombatGoal의 "0→양수" 분기를 실제로 통과시켜 배선을 완료해 둔다(1.2배 실시간
		// 재비교는 이번 범위 밖 — 이미 커밋된 목표가 있으면 이 함수 자체가 재호출되지 않는다).
		if (best != null && !PartyGoalMath.ShouldSwitchNonCombatGoal(0f, bestScore)) return null;
		return best;
	}

	// 01번 문서 4장/04번 문서 9번 항목: 목표까지 전체 길을 알면 실제 경로 길이, 일부만 알면 "프론티어까지 실제 걸음 수 + 프론티어→목표 최소 칸수"의 최솟값, 현재 정보로
	// 통행 불가가 확인되면 RouteMath.UnreachableDistanceTiles(아주 먼 거리 — 닿을 수 없는 대상이 가까운 것으로 계산되던 문제, 검증 04-08). 판정은 RouteAssessment.
	// targetIsTrap: 목표가 함정 자체(담당 후보의 도착시간 등)면 그 함정의 회피 구역을 면제하고 함정 타일까지의 길이를 잰다(검증 03-13). 이 경로의 통행 불가는 큰 거리로 바꾸지 않고
	// 직선거리로 근사한다 — 도착 예정시간이 대기 기한(TrapPartySystem.BeginWaitingFor)이 되므로 비정상적으로 길어지면 "기한 안에 못 오면 재선정"이 막힌다(함정 흐름은 04-08 수정 범위 밖).
	public int EstimateDistanceTilesTo(Vector3Int targetTile, bool targetIsTrap = false)
	{
		RouteEstimate route = RouteAssessment.Assess(this, targetTile, targetIsTrap);
		if (targetIsTrap && route.Status == RouteStatus.Unreachable)
			return RouteMath.ChebyshevDistance(position, new Vector2Int(targetTile.x, targetTile.y));
		return route.Tiles;
	}

	// 03번 문서 8장: 목표 오브젝트(함정)에 상호작용할 수 있는 인접 1칸까지 남은 예상 이동시간(초) — 위
	// 추정 거리(전체 길을 알면 실제 경로)를 개인 적용 이동속도로 나눈다.
	public float EstimateRemainingSecondsToInteract(Vector3Int targetTile)
		=> ExplorationMath.RemainingTravelSeconds(
			ExplorationMath.StepsToDisarmPosition(EstimateDistanceTilesTo(targetTile, targetIsTrap: true)), AppliedWalkSpeed);

	// 검증문서 03-01 4번/03-02: 지금 유일하게 명확한 "파티종류-오브젝트" 매칭인 회수 파티+Loot 태그만
	// "파티 목표 오브젝트"로 판정한다(Party.HasKnownRecoverableInRoom과 동일 기준) — 다른 파티종류는
	// 01번 문서 3장이 이미 스텁으로 남긴 영역이라 여기서 임의로 확장하지 않는다. 후보 필터링(중복
	// 클레임 예외)과 최종 IsPartyGoalTarget 플래그가 같은 기준을 쓰도록 한 곳으로 모은다.
	private bool IsPartyGoalObject(InteractableObject obj)
		=> party != null && party.Type == PartyType.Recover && obj != null
			&& obj.Tags != null && obj.Tags.Exists(t => t.Contains("Loot"));

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

	// IsInvestigationClaimedByPartyMember와 대칭 — 파티원이 이미 같은 맨 타일을 목표로 잡고 있으면 제외.
	private bool IsTileInvestigationClaimedByPartyMember(Vector3Int tilePos)
	{
		if (party == null) return false;
		foreach (var m in party.Members)
		{
			if (m == null || m == this || m.hp <= 0) continue;
			if (m.currentInvestigation != null && m.currentInvestigation.IsTileOnly && m.currentInvestigation.TargetPosition == tilePos) return true;
		}
		return false;
	}

	// 조사 개시 조건 중 "비전투/비도주"만 여기서 함께 확인한다(정확 인지/선택은 위 FindInvestigateTarget이, 이동은 MoveToInvestigateTarget이 담당).
	// 검증 04-08: FindInvestigateTarget이 현재 정보로 통행 불가가 확인된 대상을 이미 후보에서 빼므로, 이 값이 true면 "닿을 수 있는(또는 닿을 수 있는지 아직 모르는) 후보가 있다"는 뜻이다.
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

	// "기존 포메이션 유지" + "직접 시야 확인" 트리거 — TacticalFSMState.HasFormationNeed가 이 판정을
	// 그대로 재사용한다.
	public bool HasProtectiveFormationNeed()
	{
		if (currentFormation != null && currentFormation.EscortTarget != null && currentFormation.EscortTarget.IsInteracting) return true;
		return FindDirectlyVisibleInteractingAlly() != null;
	}

	// 보호 포메이션 호위 자리 계산 버퍼 — GetEscortSlotPosition이 매 틱 호출되므로 재사용한다(메인 스레드 전용).
	private static readonly List<(Human unit, bool ranged)> _escortBuffer = new List<(Human unit, bool ranged)>();
	private static readonly List<bool> _escortRangedBuffer = new List<bool>();

	// AIMovementHelper.MoveToEscortSlot과 같은 배치 공식 — 실제 이동 목표와 어긋나지 않도록 공유한다. backDistance <= 1이면 근접, 더 크면 원거리(후방 최소 거리).
	// 03번 6-4·6-5장(05번 9장 534~536줄, 검증 05-08 관찰 2): 근접은 전방 → 좌우 → 가장 가까운 이동 가능 타일, 원거리는 후방 2칸 이상 → 좌우 후방 → 가장 가까운 비점유 타일이며
	// 같은 대상을 호위하는 유닛 전원의 자리를 근접 먼저·같은 역할은 InstanceID 순으로 차례로 배정해 한 자리로 몰리지 않는다(EscortSlotMath — 순수). 점유 크기는 CanMove가 반영한다.
	// facing.x/y는 GetDirVector가 이미 -1/0/1로 정규화해서 주므로 그대로 캐스트한다 — Mathf.Sign(0)이 1을 반환해 Sign()을 쓰면 수직/수평 정면에서 밀리는 버그가 있다.
	public Vector2Int GetEscortSlotPosition(Human escortTarget, float backDistance)
	{
		Vector2 facing = GetDirVector(escortTarget.currentDir);
		if (facing == Vector2.zero) facing = Vector2.down;
		var facingI = new Vector2Int((int)facing.x, (int)facing.y);

		// 상호작용 유닛이 아직 이동 중일 때 근접 호위를 "전방"에 두면 좁은 통로에서 서로 길을 막는 교착이 생긴다 — 이동 중엔 후방(따라가기)으로 배치하고,
		// 도착 후 실제 상호작용이 시작돼야 전방/후방 배치를 적용한다.
		if (!IsEscortTargetActivelyInteracting(escortTarget)) return escortTarget.position - facingI;

		// 같은 대상을 호위하는 유닛 전원(자신 포함) — 근접 먼저, 같은 역할은 고정 번호순. 역할 판정(IsRangedFormationRole)은 스킬 목록을 훑으므로 유닛당 한 번만 한다.
		_escortBuffer.Clear();
		bool selfListed = false;
		if (party != null)
		{
			foreach (var m in party.Members)
			{
				if (m == null || m.hp <= 0 || m == escortTarget || m.currentFormation == null || m.currentFormation.EscortTarget != escortTarget) continue;
				_escortBuffer.Add((m, m.IsRangedFormationRole()));
				if (m == this) selfListed = true;
			}
		}
		if (!selfListed) _escortBuffer.Add((this, IsRangedFormationRole()));
		_escortBuffer.Sort((a, b) => a.ranged != b.ranged ? a.ranged.CompareTo(b.ranged) : a.unit.GetInstanceID().CompareTo(b.unit.GetInstanceID()));
		_escortRangedBuffer.Clear();
		int selfIndex = 0;
		for (int i = 0; i < _escortBuffer.Count; i++)
		{
			_escortRangedBuffer.Add(_escortBuffer[i].ranged);
			if (_escortBuffer[i].unit == this) selfIndex = i;
		}

		// 서 있을 수 있는 타일 — 그 유닛의 점유 크기·벽·문, 상호작용 유닛 자신과 상호작용 오브젝트 타일(함정이면 밟으면 안 된다) 제외, 이 대상의 호위가 아닌 다른 유닛이 서 있는 타일 제외.
		Vector2Int? interactionPos = GetInteractionObjectPosition(escortTarget);
		Vector2Int targetPos = escortTarget.position;
		int floor = currentFloor;
		int minBack = Mathf.Max(2, Mathf.RoundToInt(backDistance));
		var slots = EscortSlotMath.AssignSlots(targetPos, facingI, _escortRangedBuffer, minBack, (index, tile) =>
		{
			if (tile == targetPos || (interactionPos.HasValue && tile == interactionPos.Value)) return false;
			if (!_escortBuffer[index].unit.CanMove(tile, ignoreUnits: true)) return false;
			return !(Session != null && Session.unitGrid.TryGetValue(new Vector3Int(tile.x, tile.y, floor), out Unit u)
				&& u != null && u.hp > 0 && u != escortTarget && !_escortBuffer.Exists(e => e.unit == u));
		});
		return slots[selfIndex] ?? position; // 자리가 전혀 없으면 제자리
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





