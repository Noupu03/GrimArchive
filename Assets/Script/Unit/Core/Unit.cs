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

    // Components 리스트를 매번 선형 탐색하는 GetComponent<T>() 호출 비용을 없애기 위한 캐시.
    // OnEnable에서 null 리셋 → 첫 접근 시 ??= 로 채워지며 이후 O(1)로 반환.
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

	// UnitGenerate가 ScriptableObject.CreateInstance 직후 IObjectResolver.Inject(this)로 채워준다.
	// GoapAction/SkillAction 등 DI 컨테이너가 직접 닿지 않는 순수 C# 로직도 Unit을 통해 서비스에 접근한다.
	[Inject] private UnitGenerate _unitGenerate;
	[Inject] private VFXManager _vfxManager;
	[Inject] private InputManager _inputManager;
	[Inject] private GameSession _gameSession;
	[Inject] private HumanKnowledgeBase _knowledgeBase;

	public UnitGenerate Generate => _unitGenerate;
	// UI 리팩토링(2026-08-20, "모든 UI Haare 프레임워크에 편입") — UIManager가 Haare ICustomPanel로
	// 바뀌면서 VContainer 컨테이너에 더는 등록되지 않아 [Inject]로 주입받을 수 없다. BuildingControlPanel.
	// Instance와 동일한 정적 접근으로 교체(사용자 확인).
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

	// 유닛 배치 시스템(2026-07-27 신규) — 이 유닛이 방 인구수에서 차지하는 점유량(5.2장 "기본 유닛
	// 인구수는 1을 기준으로 한다", 2026-07-27 사용자 요청으로 2→1 조정). UnitVisualDefinition.
	// ApplyStatsTo가 units.json 값을 채운다. 필드 자체는 Unit 공통이지만 실제로 읽는 곳(Room.
	// CurrentPopulation)이 IsPlayerMonsterFaction만 걸러 합산하므로, 사용자 정정(2026-07-28 "플레이어
	// 몬스터 진영만 코스트가 필요해, 나머지 유닛은 코스트 아예 필요없어")대로 인류/야생 유닛의 값은
	// 의미가 없다 — units.json/프리팹에도 그 두 진영은 populationCost 0으로 맞춰 뒀다(현재 유일한
	// 플레이어 몬스터 유닛인 MeleeTank만 1).
	public int populationCost = 1;

	// 유닛 배치 시스템(2026-07-27 신규) 2.1/3장 "현재 소속 방" — UnitFunction.OnUpdate가 매 프레임
	// 실제 위치 기준으로 동기화한다(SyncRoomAffiliation 참고). 배회 몬스터(WildMonsterBehavior)는
	// 이 시스템 대상이 아니라(9장 보류 항목) 동기화하지 않고 항상 null로 남는다.
	public Room currentRoom;

	// 4장: 각 유닛(인류 관측자)이 대상별로 갖고 있는 개인 가중치 기록.
	public readonly Dictionary<string, PersonalWeightRecord> personalWeights = new Dictionary<string, PersonalWeightRecord>();

	// 가장 최근에 나에게 피해를 입힌 유닛 — 사망 시점(GameSession.RemoveDeadUnit)에서
	// "누가 처치했는지"를 파악해 위험도/이해도 처치 이벤트(E_MONSTER_KILL_SELF 등)를 기록할 수 있게 해준다.
	public Unit lastAttacker;

	// 4-14장: 함정 피해는 Unit이 아니라 InteractableObject가 가해자라 lastAttacker로 표현할 수 없다 —
	// TrapPass 등 함정이 실제로 TakeDamage를 호출하는 지점에서 이 필드를 채우고 lastAttacker는 비워서,
	// PartyDeathSystem이 "가장 최근 피해가 몬스터인지 함정인지"를 구분할 수 있게 한다.
	public InteractableObject lastTrapAttacker;

	// 점령 전환/처치 보상 MVP(2026-07-27, 사용자 요청) — lastAttacker는 RecordHitWeightEvent의 인류-몬스터
	// 교차 히트 전용 가드(defenderIsHuman == attackerIsHuman이면 갱신 안 됨) 때문에 몬스터끼리(예:
	// 플레이어 몬스터가 야생 몬스터를 처치) 킬에서는 항상 null로 남는다 — 방 소속 전환/처치 보상처럼
	// "누가 실제로 마지막 피해를 입혔는가"가 진영 조합과 무관하게 필요한 곳에서는 이 필드를 대신 쓴다.
	// TakePhysicalDamage/TakeMagicalDamage/ApplyDirectDamage에서 attacker가 있을 때마다 갱신된다.
	public Unit lastDamageDealer;

	// J: "수상한 타일"로 추적 중인 적(관찰자) 수 — IsTrackedAsSuspiciousByAnyEnemy가 O(N) 순회 대신 O(1)로
	// 조회할 수 있도록 ForceRollPerception에서 PendingSuspiciousInvestigation 변경 시마다 증감한다.
	// protected + 전용 증감 메서드(2026-08-20) — 다른 유닛 인스턴스가 이 값을 직접 ++/--하던 걸(캡슐화
	// 위반) 막는다. 실제 증감은 UnitFunction.ForceRollPerception이 "관찰 대상 유닛"의 이 메서드를
	// 호출(protected라 UnitFunction 자신의 읽기(IsTrackedAsSuspiciousByAnyEnemy)는 그대로 가능).
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

	// 웨이브 유닛이 계단을 통해 다른 층으로 넘어가야 할 때 HumanWaveManager가 세팅 — Goal_UseStairs가
	// 이 값이 있고 현재 층과 다르면 최우선으로 계단을 찾아 이동/통과한다(GOAP 로직, 2026-07-23 사용자
	// 요청 "예외처리 없이 goap로직에 넣어도 되겠군"). Action_CrossStairs가 실제로 층을 넘기면 null로
	// 되돌린다.
	public int? pendingStairTargetFloor = null;
	public Vector2Int? currentExplorationTarget = null;

	public Vector2Int? playerMoveTarget    = null;
	public bool isManualMoveCommand        = false; // 플레이어가 직접 클릭하여 내린 이동 명령인지 여부
	public Unit        playerAttackTarget   = null;

	// 기초문서.md 피드백(2026-08-22, "코어와 문을 명령으로 인한 파괴 대상으로 지정할 수 있게 해줘") —
	// 플레이어가 좌클릭으로 지정한 공격 대상 오브젝트(코어/문) 위치. playerAttackTarget(Unit 대상)과
	// 동급이지만 InteractableObject는 오브젝트라 위치 기반으로 추적한다. PlayerCommandFSMState.
	// ExecutePlayerAttackObject가 소비 — 파괴/소유권 전환 등으로 더 이상 유효한 대상이 아니게 되면
	// 스스로 null로 비운다.
	public Vector3Int? playerAttackObjectTarget = null;

	// 대기 상태(IdleFSMState, 2026-08-20 신규, 사용자 요청 "대기 상태를 새로 만들어줘... 야생의 경우...
	// 소환 위치(야생) 주변 배회") — 야생 몬스터가 스폰된 좌표. GameSession.SpawnWildRoomGuards/
	// WildBaseSpawnerComponent.SpawnMonster가 생성 직후 한 번 세팅하고 그 뒤로는 안 바뀐다.
	public Vector2Int? summonPosition = null;

	// IdleFSMState.OnEnter가 매번 다시 계산해 세팅하는 배회 기준점(위 summonPosition, 없으면 진입
	// 시점 위치)과 다음 1칸 이동이 허용되는 시각(Time.time 기준, 정지
	// 시간이 지날 때마다 갱신) — 상태를 넘나들 때마다 새로 계산하므로 여기서는 그냥 마지막 값을 들고
	// 있기만 한다.
	public Vector2Int? idleAnchorPosition = null;
	public float idleNextMoveTime = 0f;

	// "집결 및 정지" 명령(2026-08-20, 사용자 요청 "명령 메뉴에 '집결 및 정지' 모드를 넣어줘. 선택한
	// 유닛들을 우클릭을 통해 장소를 지정하면 해당 위치로 이동하고, 이동 후에는 '정지' 상태가 됨") —
	// 이동이 끝나는 순간(PlayerCommandFSMState.CompletePlayerCommand) 이 플래그를 보고 isHalted로
	// 전환한다.
	public bool pendingHaltOnArrival = false;

	// "정지"(동상) 상태 — 켜지면 UnitFSM.SelectState가 무조건 HaltFSMState로 고정해 그 무엇으로도
	// 풀리지 않는다. 사용자 확인(2026-08-20): 완전 무반응 — 공격/스킬/회피 일체 하지 않는다(적이 옆에
	// 있어도 자동 반격 없음). 오직 플레이어의 새 직접 명령이나 "명령 취소"로만 해제된다
	// (InputManager.IssueMoveCommand/CancelSelectedUnitsCommands 참고).
	public bool isHalted = false;

	// "제자리 공격" 명령(기초문서.md 피드백, 2026-08-22 — R키 몬스터 배치 모드를 대체) — 이동이 걸리는
	// 순간 함께 true가 된다. 이동 완료(PlayerCommandFSMState.CompletePlayerCommand)가 이 플래그를 보고
	// isStandGroundAttack으로 전환한다.
	public bool pendingStandGroundOnArrival = false;

	// 켜지면 UnitFSM.SelectState가 무조건 StandGroundAttackFSMState로 고정한다 — 이동은 절대 하지
	// 않지만 사거리 내 적은 공격한다(정지·완전 무반응과 다름). 오직 플레이어의 새 직접 명령이나
	// "명령 취소"로만 해제된다(InputManager.IssueMoveCommand/CancelSelectedUnitsCommands 참고).
	public bool isStandGroundAttack = false;

	// 유닛 자체가 영구히 고정된 개체임을 뜻하는 플래그(2026-08-24, 보스 골렘용) — 위 isHalted/
	// isStandGroundAttack이 "플레이어 명령으로 켜고 끄는 일시 상태"인 것과 달리 이건 스폰 시점에
	// 한 번 켜지고 절대 꺼지지 않는 유닛의 성질이다. 그래서 HasActivePlayerCommand()/
	// ClearPlayerCommand() 같은 명령 계열 로직에는 일부러 포함하지 않는다(명령 취소로 풀리면 안 됨).
	//
	// 효과는 isStandGroundAttack과 동일하다 — UnitFSM.SelectState가 StandGroundAttackFSMState를
	// 강제 배정해 "이동은 절대 안 하지만 사거리 내 적은 공격"하게 만들고, UnitFunction.
	// OnReactToThreat이 회피/점멸로 인한 위치 이동까지 막고, UnitFunction.Move가 최종 안전망으로
	// 모든 이동 시도를 무시한다.
	//
	// 주의(2026-08-24 사용자 신고 "보스가 움직임"): units.json의 walkSpeed=0으로는 이동이 막히지
	// 않는다 — walkSpeed는 GameSession.ProcessUnitAction에서 "행동 주기(actionCooldown)"만 결정할
	// 뿐 이동 가능 여부와 무관하다. 고정 유닛은 반드시 이 플래그를 써야 한다.
	public bool isImmobile = false;

	// 전방위(360도) 시야(2026-08-24, 보스 골렘 — 사용자 요청 "보스 시야 360도로 해줄래? 제자리에
	// 있는데 시야각때문에 공격범위가 이상하게 됨"). 켜지면 UnitFunction.UpdateFOV의 시야각(기본 120도)과
	// 인지각(감지 스탯에 따라 60~120도)이 둘 다 360도가 되고, 피격 시 공격자 인지 판정(ForceRollPerception)의
	// 인지각도 함께 360도가 된다 — 거리/차폐(벽·방 경계) 판정은 그대로라 "각도 제한만" 사라진다.
	//
	// 고정 유닛에 필요한 이유: isImmobile 유닛은 자리를 못 옮기므로 등 뒤로 돌아간 적을 영영 인지하지
	// 못해 한쪽만 바라본 채 굳어버린다. 각도 제한을 풀면 어느 방향의 적이든 대상으로 잡고, 그때마다
	// StandGroundAttackFSMState가 currentDir을 그쪽으로 돌려 공격 히트박스도 정상 방향으로 생성된다.
	public bool hasOmnidirectionalVision = false;

	public Vector3Int? playerInteractTarget = null;
	public int         playerCommandStuckTurns = 0;

	// Encapsulated command setters (2026-08-22 refactoring)
	public void SetMoveCommand(Vector2Int target, bool markHalt, bool markStandGround)
	{
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
		playerAttackTarget = target;
		playerMoveTarget = null;
		playerAttackObjectTarget = null;
		ClearAttackObjectTarget();
		isHalted = false;
		isStandGroundAttack = false;
	}

	public void SetObjectAttackCommand(Vector3Int target)
	{
		playerAttackObjectTarget = target;
		playerAttackTarget = null;
		playerMoveTarget = null;
		isManualMoveCommand = true;
		isHalted = false;
		isStandGroundAttack = false;
	}

	public void ClearPlayerCommand()
	{
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



	// ─── 인지·정보판정·실패처리 시스템 관련 (02_인지·정보판정·실패처리_시스템_v0.2) ───
	// 4장: 대상별(내 유닛=Unit 참조, 오브젝트=InteractableObject.Id) 지속 인지 상태. 트리거 시점에만
	// UnitFunction.CastRay/ResolveReachedTarget/ForceRollPerception이 갱신한다 — personalSpottedEnemies와 달리
	// UpdateFOV 호출마다 Clear하지 않는다(PerceptionRecord.cs 주석 참고).


	// 20장: 수상한 타일 확인 대기 중인 인류가 하나라도 있으면 경계 상태 시 10% 감지 보정(+20)과
	// 01-A 10장/11장 시야 방향 전환 우선순위의 Alert 사유가 이 값을 참조한다.

	// 9장: 정신력 보정(인류 전용, 몬스터는 항상 0) — PerceptionMath.MentalCorrectionForHuman 참고.
	public bool CanPerceive => StatusEffects.State.stunDuration <= 0f;
	public float GetMentalVisibilityCorrection() => (this is Human) ? PerceptionMath.MentalCorrectionForHuman(BaseStat.mental, BaseStat.maxMental) : 0f;

	// 01장/7장, 01-A 7장: 시야 범위 안 + 인지 범위 밖 + 비어있지 않은 타일 목록(이번 UpdateFOV 호출
	// 기준 임시 스냅샷 값 전달, 매 UpdateFOV마다 비우고 다시 채운다. 목표/경로 재설정을 다루는
	// 10_목표설정·이동경로·재설정 문서가 아직 없어서 이 리스트를 완전하게 소비하는 곳은 없다 —
	// 그 문서가 생기면 VisionMath.NonEmptyTileTempWeight와 함께 바로 쓸 수 있도록 데이터만 미리 채워둔다.


	// 01-A 9장: 공격 시도 중인 유닛은 고정 시간(5초) 동안 가시성이 +10 상승한다. 재공격해도 지속시간만
	// 초기화되고 상승치는 누적되지 않는다 — 문서가 "지속시간을 다시 5초로 초기화"라고만 명시했지
	// "상승치가 추가된다"고는 하지 않아, 상한 100 규칙과 함께 가장 단순하게 해석한 것(불확실, 판단 근거는
	// 구현현황 문서에 기재).
	public bool IsVisibilityBoosted => VisionStat.attackVisibilityBoostTimer > 0f;
	public void TriggerAttackVisibilityBoost() => VisionStat.attackVisibilityBoostTimer = VisionMath.AttackVisibilityBoostDuration;
	// 4-6장: 수상한 타일 추적 중 이동 1회당 +20씩(개별 5초 유지) 누적된 값을 그대로 더한다.
	public float GetFinalVisibility() => VisionMath.FinalVisibility(VisionStat.baseVisibility, VisionStat.stealth, IsVisibilityBoosted,
		VisionStat.suspiciousMoveBoostTimers.Count * ExplorationMath.SuspiciousTargetVisibilityBoostPerMove);

	// ─── 03_탐색반응·경계·조사·함정대응_시스템 관련 ────────────────────────────
	// 함정 대응(9장)과 경계(4장)는 13장 표에 따라 인류/몬스터 공통이라 base Unit에 둔다. 조사(5장)/
	// 대기(10장)/보호 포메이션(6장)은 인류 전용(또는 몬스터는 "컨셉에 따라"라 아직 구현 안 함)이라
	// Human 쪽에 둔다(아래 Human 클래스 참고).
	public TrapInteractionState currentTrapInteraction; // null이면 함정 대응 중 아님
	public AlertSearchState     currentAlertSearch;      // null이면 경계 중 아님

	// 기초문서.md 피드백(2026-08-22) — 코어/문 공격 채널링 공용 필드. 자동(TacticalFSMState.
	// CoreAttackPerform, 코어 전용, 방 소유권 없는 진영 제외) + 플레이어 명령(PlayerCommandFSMState.
	// ExecutePlayerAttackObject, 코어+문 둘 다) 양쪽이 인접 도착 시 채운다. 값이 있는 동안만
	// UnitFunction.OnUpdate가 매 프레임 CoreHp/DoorHp를 깎는다(TrapInteractionState의 Destroying
	// 단계와 동일한 채널링 패턴). 직접 대입하지 말고 항상 SetAttackObjectTarget/
	// ClearAttackObjectTarget을 거칠 것 — 파괴 VFX 시작/종료가 그 두 메서드에 물려 있다.
	public Vector3Int? currentAttackObjectTarget;

	// 파괴 채널링 VFX 인스턴스(2026-08-24 신규, VFX_BlockBreaking.prefab) — SetAttackObjectTarget/
	// ClearAttackObjectTarget만 건드린다.
	private GameObject _attackObjectVfxInstance;

	// currentAttackObjectTarget을 설정하는 유일한 진입점(2026-08-24 사용자 요청 "문/코어 파괴 VFX,
	// 재생시간 끝나도 파괴 행동 끝날 때까지 계속") — BT가 채널링 중에도 매 틱 같은 값을 다시 대입할
	// 수 있어(예: TacticalFSMState.MoveToCoreAttack이 인접할 때마다 재확인), 값이 실제로 바뀔 때만
	// VFX를 새로 시작해 중복 재생을 막는다. VFX_BlockBreaking 자체가 looping이라 재생 시간과 무관하게
	// 계속 돌고, ClearAttackObjectTarget이 호출되는 순간(파괴 완료/중단 무관) 즉시 멈춘다.
	public void SetAttackObjectTarget(Vector3Int pos)
	{
		if (currentAttackObjectTarget == pos) return;
		StopAttackObjectVfx();
		currentAttackObjectTarget = pos;
		_attackObjectVfxInstance = VFXManager.SpawnBlockBreakingVfx(this, pos);
	}

	// 코어/문 파괴 채널링 종료(2026-08-24 재조정, 사용자 요청 "파괴가 진행된지 5초 지난 시점부터
	// 서서히 회복 + 동일 시점에 진행바 숨김, 다시 파괴 시도하면 다시 표시") — 채널링이 어떤 이유로든
	// (완료/파괴/명령 취소/대상 이탈) 끝날 때 파괴 VFX는 항상 즉시 멈추지만, 진행 막대는 더 이상
	// 여기서 즉시 숨기지 않는다 — 공격이 끊긴 뒤에도 회복 지연시간(CoreRegenDelaySeconds/
	// DoorRegenDelaySeconds) 동안은 마지막 체력 그대로 막대를 보여줘야 하기 때문이다. 막대 숨김은
	// GameSession.TickCoreRegen/DoorSystem.UpdateProcess가 그 지연시간이 지나 회복이 실제로 시작되는
	// 순간에 처리한다(InteractableObject.TimeSinceLastDamaged 참고).
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

	// 5장/9-6장: 조사·함정 해제 중 시야/인지 범위 50% 페널티(각 문서 동일 비율) — UnitFunction.
	// UpdateFOV가 시야·인지 거리/인지각 계산에 곱한다.
	public bool ExplorationPenaltyActive =>
		(currentTrapInteraction != null && currentTrapInteraction.PenaltyActive) ||
		(this is Human human && human.currentInvestigation != null && human.currentInvestigation.PenaltyActive);

	// 9-7장/5-6장 "위협 콜라이더를 인지함" 중단 조건 — 텔레그래프(currentThreat)는 경고 목적이라
	// 02문서 확률표를 다시 거치지 않고 인지 범위 안의 적이 지금 공격 예고 중인지 raw로 확인한다
	// (Goal_TrapResponse/Goal_Investigate의 ShouldInterrupt가 공유 호출, 시야인지반응_03_GOAP목표
	// 우선순위표_2026-07-22.txt 8-1절 근거).
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

	// 스탯 적용은 이제 UnitGenerate가 스폰될 프리팹의 UnitVisualDefinition.ApplyStatsTo(unit)이
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

		// 플레이어 진영 몬스터 방 제한 MVP(2026-07-27, 사용자 요청 "회피나 점멸 등 행동으로도 방 밖으로
		// 나갈 수 없게") — Dodge/Blink(DefenseSystem.cs)는 A* 경로탐색을 거치지 않고 이 메서드로 직접
		// 위치를 옮겨서 RoomConfinedMovement.IsTileWalkable의 방 경계 검사를 우회한다. 여기서 같은
		// 규칙을 한 번 더 적용한다 — RoomConfinedMovement를 쓰는 유닛이 플레이어 명령 중이 아닌데
		// 목적지가 현재 방을 벗어나면 이동을 취소(제자리 유지)한다. Dodge/Blink 둘 다 "후보가 없으면
		// 그 자리에 남는다"는 기존 동작과 자연스럽게 일치한다.
		//
		// 문 타일 회피/점멸 금지(2026-07-28, 사용자 요청 "문이 있는 공간도 사용자 명령이 있지 않으면
		// 갈 수 없는거로 해. 회피 점멸 등을 통한 이동도 포함해서 막아야") — 문 타일은 그 문을 낀 두
		// 방 중 한쪽의 roomGrid 소유 청크에 포함돼 있어(같은 방 소속으로 잡힘) 위 방 경계 검사만으로는
		// 걸러지지 않는다(자기 방의 문으로는 순간이동 가능한 구멍이었음). RoomConfinedMovement.
		// IsTileWalkable(일반 이동)과 동일하게 IsDoorTile로 한 번 더 막는다.
		if (MovementAlgorithm is RoomConfinedMovement && !isManualMoveCommand
			&& _gameSession.roomGrid.TryGetValue(oldKey, out Room myRoom))
		{
			Vector3Int targetKey = new Vector3Int(targetPos.x, targetPos.y, currentFloor);
			if (!_gameSession.roomGrid.TryGetValue(targetKey, out Room targetRoom) || targetRoom != myRoom)
				return;
			if (_gameSession.IsDoorTile(targetKey))
				return;
		}

		// 최종 안전장치(2026-08-22 사용자 신고 "난전 중 유닛끼리 겹쳐진다. 어떤 상황에서도 유닛끼리는
		// 겹쳐지면 안돼") — ForceMove는 A*/CanMove 정상 경로를 거치지 않는 예외 이동(회피/점멸)이라,
		// 호출부가 후보를 고를 때 점유 검사를 빠뜨리면(실제로 DefenseSystem의 점멸 후보 탐색이
		// ignoreUnits:true를 써서 이 문제가 있었다 — 그쪽은 이미 착지 칸 재검증으로 고쳤다) 바로 겹침
		// 사고로 이어진다. GameSession.RegisterUnitPos(발자국 크기까지 고려해 점유를 확인, 이미 다른
		// 유닛이 있으면 false 반환)를 거쳐 등록하고, 실패하면 이동 자체를 취소하고 원래 자리에 남는다
		// (후보가 없으면 제자리 유지라는 기존 회피/점멸 관례와 동일). 예전엔 이 메서드가 unitGrid를
		// 직접 건드리면서 발자국(footprint)을 전혀 고려하지 않아 다중 타일 유닛에서 등록이 어긋날 수
		// 있었는데, 공용 헬퍼로 옮기며 그 문제도 함께 해결됐다.
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

	// 01-A 11장 시야 방향 전환 우선순위 결정 — 이번 틱에 활성화된 후보들 중 가장 높은 우선순위를
	// 골라 currentDir를 갱신한다. GameSession.ProcessUnitAction이 ExecuteAction() 이후, UpdateFOV()
	// 이전에 호출한다(그래야 이동으로 갱신된 currentDir를 "이동 중 방향 기본값으로 사용할 수 있다).
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

	// 개인 지도(오브젝트/몬스터 목격/방 위험도·흥미도) — 지도 기록 정리 문서 기준 "지도는
	// 인류만 갖고 있어야 한다"는 지침에 따라 Human에만 둔다(Monster/base Unit에는 없음).
	public PersonalMapKnowledge personalMap => Memory.personalMap;
	public List<string> collectedObjects    => Memory.collectedObjects;

	// 이 유닛에 한정된 파티(있다면, 6장 파티 전멸/6장 웨이브클리어 상태 반영 사정과 연관)
	// GameSession.CreateParty()가 파티 생성 시 채워준다. 파티 없이 스폰된 인류(디버그/단독 소환
	// 등)는 null로 남아 파티 관련 사정 산정에서 자연히 제외된다.
	public Party party { get => UnitParty.party; set => UnitParty.party = value; }

	// 03문서 5장(조사)/10장(대기)/6장(보호 포메이션) — 인류 전용(13장 표, 몬스터는 "컨셉에 따라"만
	// 명시돼 있어 실제 컨셉 시스템이 생기기 전까지는 인류만 구현). null이면 각각 진행 중 아님.
	public InvestigationState currentInvestigation;
	public WaitState          currentWait;
	public FormationState     currentFormation;
	// 07문서 7장/07-A 9장(2026-07-31 신규): 전투 진입 시 합류 대기 — null이면 대기 중 아님(즉시 전투).
	public JoinCombatWaitState currentJoinCombatWait;

	// 던전 입구 구조(2026-08-20, "던전 입구 구조 프로그래머 지시서") — DungeonEntranceSystem이 0층
	// 숨은 스폰 청크→1x3 입구→계단까지 파티 진형을 직접(GOAP 우회) 제어하는 동안 켜진다.
	// NavigationFSMState의 자유탐색(Goal_Explore)이 이 값이 켜진 동안 끼어들지 않도록 막는 용도 —
	// isMustered(몬스터 소집)와 동일한 성격이지만, 인류 전용 별개 개념이라 필드도 따로 둔다.
	public bool isInDungeonEntranceSequence = false;

	// 6-1장 두 번째 조건("직접 시야로 상호작용 유닛을 확인한 일반 탐색 유닛") + 8-2장 판정에 쓴다 —
	// 함정 대응이나 조사 중이면(=다른 유닛이 나를 호위할 만한 상황이면) true. 함정 쪽은 "함정 위치에
	// 실제로 도달했을 때"만 true로 좁혔다(2026-07-22, 사용자 신고 — 함정이 이미 해제됐는데도 주변이
	// 경계 태세를 취함) — 9-5장 순서가 "해제 유닛이 함정 위치 도달 → 상호작용 정보 전파 및 보호
	// 포메이션 형성 → 함정 해제 시작"이라, 발견 직후 5초 합류 대기나 이동 중(아직 도착 전)에는
	// 보호 포메이션이 형성되면 안 된다. 예전엔 함정을 인지한 순간부터(도착 전 포함) true였다.
	public bool IsInteracting => IsActivelyHandlingTrap() || currentInvestigation != null;

	private bool IsActivelyHandlingTrap()
	{
		var trap = currentTrapInteraction;
		if (trap == null) return false;
		// "함정 위치에 실제로 도달했을 때"의 도달 판정 반경이 TacticalFSMState.MoveToTrap과 함께
		// 정확 일치 → Chebyshev ≤ 1(바로 옆 1칸)로 넓어졌다(사용자 요청, 2026-07-25 "함정 바로
		// 위가 아니라 인근 1칸에서 해제 상호작용 가능하게") — 두 판정이 어긋나면 "이동은 끝났는데
		// 아직 상호작용 중이 아닌 것으로 보이는" 프레임이 생긴다.
		Vector2Int trapPos = new Vector2Int(trap.TrapPosition.x, trap.TrapPosition.y);
		return Mathf.Max(Mathf.Abs(position.x - trapPos.x), Mathf.Abs(position.y - trapPos.y)) <= 1;
	}

	// 8-2장: "비목표 상호작용 중 보호 유닛 피격 → 포메이션 해제 후 전투 또는 경계"(파티 목표 개념이
	// 없어 예외 없이 항상 적용, 시야인지반응_03_GOAP목표우선순위표_2026-07-22.txt 3-1/3-3절 참고).
	// 같은 파티 안에서 이 유닛을 호위 중(FormationState.EscortTarget==this)인 멤버 중 이번 턴에
	// 피격당한 사람이 있는지 확인한다 — 호위하는 쪽(FormationState)만 참조를 들고 있어 역방향으로
	// 파티원을 순회한다(양방향 리스트 관리를 피하기 위한 설계, FormationState.cs 주석 참고).
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

	// Goal_Investigate.GetPriority와 Action_MoveToInvestigateTarget이 공유하는 헬퍼(같은 판정을 두 곳에
	// 다시 구현하지 않기 위함, 시야인지반응_03_GOAP목표우선순위표_2026-07-22.txt 0-1절 "공유 원칙").
	// 5-2장 대상(비전투 오브젝트) 중 함정이 아니고, 시체/전멸흔적처럼 이미 단일 단계로 자동 확인
	// 완료되는 대상도 아니고(02문서 6장/17장 — CastRay가 정확 인지 즉시 RegisterObject까지 이미
	// 끝냄), 아직 조사되지 않은, 이 유닛이 이미 아는(personalMap.IsObjectKnown) 가장 가까운
	// 오브젝트를 찾는다.
	//
	// GoapWorldState.Build가 매 틱(JudgeState/ExecuteAction 각각) 이걸 호출하고 Goal_Investigate.
	// GetPriority/Action_MoveToInvestigateTarget.Execute도 각자 또 호출해서, 한 유닛의 ProcessUnitAction
	// 한 번에 오브젝트 전체를 3~4번씩 훑는 게 프레임 드랍의 주된 원인이었다(2026-07-22, 사용자 신고).
	// 같은 프레임 안에서는 결과가 바뀔 일이 없으므로 프레임 단위로 캐시한다.
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
			// 03문서 5-2장(2026-07-27 개정): 파티원 시체는 조사 대상으로 유지한다(사망 원인·전투 흔적
			// 등 추가 정보 획득) — 몬스터 시체/전멸 흔적은 여전히 제외(CastRay가 인지 즉시 단일 단계로
			// 확인 완료하는 대상이라 별도 조사 단계가 없음, 17장 참고). 코어(7-3장)는 리더 전용 조사
			// 대상이라 이 일반 조사 후보 풀에서 완전히 제외한다(TacticalFSMState.CanContinueCore 참고).
			// 문(2026-07-28, 사용자 신고 "계속 전술(조사) 상태로 들어가는데 이유가 뭐지?") — 문은 이
			// 목록이 만들어질 당시(문 시스템 도입 전)엔 존재하지 않던 오브젝트 종류라 제외 목록에서
			// 빠져 있었다. 맵 전체에 176개나 깔려 있는 통행용 배경 오브젝트일 뿐 조사할 대상이 아닌데
			// 인류가 알게 될 때마다(거의 항상 — 통로마다 있으므로) 조사 후보로 잡혀 탐색을 계속
			// 가로막고 있었다 — 명백한 누락이라 제외 목록에 추가한다.
			bool isExcludedTrace = isTrace || (isCorpse && !isHumanTag);
			if (isTrap || isExcludedTrace || isCore || isDoor) continue;
			// 07문서 10장 마지막 문단: "재전파 수신자는... 같은 대상에 대한 중복 조사·해제 목표를
			// 선택하지 않는다" — 다른 파티원이 이미 이 오브젝트를 currentInvestigation으로 잡고 있으면
			// 후보에서 뺀다. TrapPartySystem이 함정 쪽에서 쓰는 것과 동일하게 별도 코디네이션 자료구조
			// 없이 파티 순회로 즉석 확인한다(2026-08-06, 재검증에서 발견한 중복 조사 버그 수정).
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

	// 5-3장 5가지 조건 중 "비전투/비도주"만 여기서 함께 확인한다(정확 인지/선택/도달은 위
	// FindInvestigateTarget과 Action_MoveToInvestigateTarget.Execute의 이동 로직이 담당).
	public bool HasReachableInvestigateTarget()
	{
		if (personalSpottedEnemies.Count > 0) return false; // 비전투 — 적이 보이면 조사 시작 안 함(전투 우선)
		return FindInvestigateTarget() != null;
	}

	// 6-1장 두 번째 조건("상호작용 유닛 본인의 최초 전파를 직접 받은 일반 탐색 유닛") — 2026-07-31:
	// 07문서 10장 구현으로 시야 기반 근사(예전엔 "시야 범위 안에서 직접 확인"이면 참여)를 대체했다.
	// 07문서는 "상호작용 유닛을 시야에서 직접 확인한 것만으로는 참여하지 않는다"고 명시하므로, 이제는
	// PropagationSystem.NotifyInteractionStarted가 상호작용 시작 시점에 전파한 정보를 실제로 받은
	// 파티원만 후보가 된다(TacticalFSMState의 InvestigatePerform/TrapDisarmPerform/CoreInvestigatePerform
	// 참고).
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
			// 이미 다른 유닛을 호위 중이면 그 대상이 아닌 새 상호작용 유닛으로는 갈아타지 않는다
			// (6-2장 "기존 포메이션 유지").
			if (currentFormation != null && currentFormation.EscortTarget != null && currentFormation.EscortTarget != m) continue;

			float d = Vector2Int.Distance(position, m.position);
			if (d < bestDist) { bestDist = d; best = m; }
		}
		return best;
	}

	// 6-4/6-5장 근접·원거리 배치 분기 — Actions.cs의 Action_EngageEnemy.ExecuteSkillActionBased가
	// 이미 쓰는 "최대 스킬 사거리" 판정(HitRange>=4 기준)을 그대로 재사용한다.
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

	// 6-2장 "기존 포메이션 유지" + 6-1장 "직접 시야 확인" 트리거 — Goal_ProtectiveFormation.GetPriority와
	// GoapWorldState.Build가 같은 판정을 따로 구현하지 않도록 공유한다(0-1절 "공유 원칙").
	public bool HasProtectiveFormationNeed()
	{
		if (currentFormation != null && currentFormation.EscortTarget != null && currentFormation.EscortTarget.IsInteracting) return true;
		return FindDirectlyVisibleInteractingAlly() != null;
	}

	// GoapAction.MoveToEscortSlot(GoapCore.cs)과 동일한 배치 공식 — GoapWorldState.Build의 atEscortSlot
	// 판정이 실제 이동 목표와 어긋나지 않도록 공유한다.
	// 03문서 6-4장: 근접 유닛은 상호작용 유닛 "전방"에 배치(+facing 방향) — 6-5장 원거리는 "후방"
	// 2칸 이상(-facing 방향)이라 부호가 반대다. 예전엔 근접도 -facing을 써서 후방에 서게 돼 있었다
	// (검증 발견 버그, 사용자 확인 2026-07-24 "버그다, 문서대로 전방으로 고쳐줘"). facing.x/y는
	// GetDirVector(8방향)가 이미 -1/0/1로 정규화해서 주므로 그대로 캐스트한다 — Mathf.Sign(0)이 0이
	// 아니라 1을 반환하는 Unity 특성 때문에 Sign()을 쓰면 정면이 수직/수평(UP/DOWN/LEFT/RIGHT)일 때
	// 옆으로 밀리는 버그가 있었다(실제 테스트로 검증 발견, 2026-07-25).
	public Vector2Int GetEscortSlotPosition(Human escortTarget, float backDistance)
	{
		Vector2 facing = GetDirVector(escortTarget.currentDir);
		if (facing == Vector2.zero) facing = Vector2.down;

		// 2026-07-27 사용자 신고("보호 포메이션 동안 다른 유닛에게 길이 막혀서 리더가 코어에 영구히
		// 도착 못함") — 상호작용 유닛이 아직 목적지로 걸어가는 중일 때, 근접 호위의 "전방"(+facing)
		// 배치는 상호작용 유닛이 이동 중인 바로 그 방향과 대개 일치한다(Move()가 이동 방향으로
		// currentDir을 갱신하므로). 그래서 호위가 상호작용 유닛보다 먼저 그 앞자리를 차지해버리면
		// 좁은 통로에서 진행 경로 자체를 막아 서로 오도 가도 못하는 교착이 생겼다. 아직 실제
		// 상호작용을 시작하지 않고 "이동 중"일 때는 전방 대신 후방(따라가기)으로 배치하고, 실제로
		// 도착해 상호작용이 시작된 뒤에야(예: 조사/함정 페널티 활성화, 코어 Active) 문서대로의
		// 전방/후방 배치를 적용한다.
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

		// 2026-07-27 사용자 신고("코어 포메이션에서 유닛이 코어를 밟고 서있어") — 상호작용 유닛이
		// 오브젝트 바로 옆(1칸)에서 그 방향을 보고 있으면, "전방(근접 배치)" 슬롯 계산이 오브젝트
		// 자신의 타일과 우연히 겹친다(오브젝트가 조사/함정/코어처럼 Passable이라 실제로 밟고 설 수
		// 있어서 눈에 띔). 6-4장 폴백 순서(전방→좌우)와 같은 정신으로, 겹치면 한 칸 더 물러난 자리를
		// 쓰고 그래도 겹치면 옆으로 민다.
		Vector2Int? interactionPos = GetInteractionObjectPosition(escortTarget);
		if (interactionPos.HasValue && slot == interactionPos.Value)
		{
			Vector2Int farther = escortTarget.position + new Vector2Int((int)facing.x * 2, (int)facing.y * 2);
			slot = farther != interactionPos.Value ? farther : slot + new Vector2Int(-(int)facing.y, (int)facing.x);
		}

		return slot;
	}

	// escortTarget이 실제로 상호작용(조사 진행/함정 해제 진행)을 시작했는지 — 아직
	// 목적지로 "이동 중"인 단계와 구분한다(위 GetEscortSlotPosition 주석 참고).
	private bool IsEscortTargetActivelyInteracting(Human escortTarget)
	{
		if (escortTarget.currentInvestigation != null) return escortTarget.currentInvestigation.PenaltyActive;
		if (escortTarget.currentTrapInteraction != null) return escortTarget.currentTrapInteraction.PenaltyActive;
		return false;
	}

	// 위 GetEscortSlotPosition이 겹침 판정에 쓰는 "이 유닛이 지금 상호작용 중인 오브젝트의 위치" —
	// 조사/함정 중 진행 중인 것을 조회한다.
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

	// 몬스터용 개인 지도(2026-08-20, 사용자 요청) — Human.personalMap과 동일한 노출 패턴, 다만
	// 담는 내용은 지형 밝히기 + 함정 위치뿐(가중치 없음). UnitFunction.CastRay가 채운다.
	public MonsterMapKnowledge monsterMap => Memory.monsterMap;

	public override void JudgeState()
	{
		base.JudgeState();
		// 몬스터 상태 판단 로직 추가
	}
}





