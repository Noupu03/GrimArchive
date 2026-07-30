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
    }
	public static FactionData humanFactionData  = new FactionData();
	public static FactionData monsterFactionData = new FactionData();

	// UnitGenerate媛 ScriptableObject.CreateInstance 吏곹썑 IObjectResolver.Inject(this)濡?梨꾩썙以??
	// GoapAction/SkillAction ??DI 而⑦뀒?대꼫媛 吏곸젒 ?우? ?딅뒗 ?쒖닔 C# 濡쒖쭅?????좊떅???듯빐 ?쒕퉬?ㅼ뿉 ?묎렐?쒕떎.
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

	// HAARE ?꾨젅?꾩썙??(Native Routine): ?좊떅 ?뚯냽怨??됰룞 ?⑦꽩??寃곗젙?섎뒗 ?명꽣?섏씠??
	public IFactionBehavior FactionBehavior { get; set; }

	public virtual bool IsHumanFaction => FactionBehavior is HumanFactionBehavior;
	public virtual bool IsPlayerMonsterFaction => FactionBehavior is PlayerMonsterBehavior;
	public virtual bool IsWildMonsterFaction => FactionBehavior is WildMonsterBehavior;


	// ?꾨왂 ?⑦꽩: ?좊떅???대룞 ?뚭퀬由ъ쬁???고??꾩뿉 媛덉븘?쇱슱 ???덈뒗 援ъ“
	public IMovementAlgorithm MovementAlgorithm { get; set; } = new AStarMovement();

	public UnitType unitType;

	// ??? 媛以묒튂 ?쒖뒪???댄빐???꾪뿕???λ??? 愿???????媛以묒튂 ?곗궛怨듭떇 臾몄꽌 v0.7 ????????
	public bool isSpecialUnit = false;     // 7-1?? 蹂댁뒪/?ㅻ찓?쒖뒪 ??醫낅퀎+媛쒕퀎 ?댄빐?꾨? ?④퍡 ?곕뒗 ?뱀닔 ?좊떅 ?щ?
	public bool isInterestTarget = false;  // 6-2??18?? IsInterestTarget ?뚮옒洹?(?댄빐???곸듅???곕Ⅸ ?λ???媛먯냼??誘몄쟻??

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
	// baseInterest moved = 0f;        // 6-3?? ?좊떅 湲곕낯 ?λ???
	// baseDanger moved = 0f;          // 12-1?? ???湲곕낯 ?꾪뿕??
	// heavyHitThreshold moved = 10f;  // 3?? "?쇱젙 ?쇳빐???댁긽" ?먯젙 湲곗?媛?(?좊떅蹂??곗씠???뚯씠釉?

	// 4?? ???좊떅(?몃쪟 愿?먯쓽 愿李곗옄)????곷퀎濡??ㅺ퀬 ?덈뒗 媛쒖씤 媛以묒튂 湲곕줉.
	public readonly Dictionary<string, PersonalWeightRecord> personalWeights = new Dictionary<string, PersonalWeightRecord>();

	// 媛??理쒓렐?????좊떅?먭쾶 ?쇳빐瑜??낇엺 ??? ?щ쭩 ?쒖젏(GameSession.RemoveDeadUnit)?먯꽌
	// "?꾧? 泥섏튂?덈뒗吏"瑜??뚯븘???꾪뿕???댄빐??泥섏튂 ?대깽??E_MONSTER_KILL_SELF ??瑜?湲곕줉?????덉뼱???붾떎.
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
	public int _suspiciousObserverCount;

	// ??? ?덈꺼 諛??깆옣 ?띿꽦 ????????????????????????????????????????
	public int level = 1;                 // ?꾩옱 ?덈꺼
	// exp moved = 0f;                // ?꾩옱 寃쏀뿕移?
	public int killCount = 0;             // ??泥섏튂 ??

	// ??? ?꾪닾 ?몃? ?띿꽦 ???????????????????????????????????????????
	// maxHp moved = 100f;            // 理쒕?泥대젰
	// hp moved    = 100f;            // ?꾩옱 泥대젰

	// maxMp moved = 0f;             // 理쒕? 留덈굹
	// mp moved    = 0f;             // ?꾩옱 留덈굹

	// physicalAttack moved  = 10f;  // 臾쇰━ 怨듦꺽??
	// magicalAttack moved   = 0f;   // 留덈쾿 怨듦꺽??

	// physicalDefense moved = 0f;   // 臾쇰━ 諛⑹뼱??
	// magicalDefense moved  = 0f;   // 留덈쾿 諛⑹뼱??

	// hp movedRegen         = 0f;   // ?ъ깮??>?덈줈 異붽??? 濡쒖쭅?놁쓬
	// attackspeed moved     = 0f;   // 怨듦꺽 ?띾룄->?덈줈 異붽??? 濡쒖쭅?놁쓬
	// walkSpeed moved       = 3f;   // ?대룞 ?띾룄
	// reaction moved        = 1f;   // 諛섏쓳?띾룄->濡쒖쭅?놁쓬
	// criticalChance moved  = 0f;   // 移섎챸???>?덈줈 異붽??? 濡쒖쭅?놁쓬
	// cooltimeReduction moved = 0f; // 荑⑦???媛먯냼??>?덈줈 異붽??? 濡쒖쭅?놁쓬
	// statusResistance moved  = 0f; // ?곹깭?댁긽???>援щ갑???묐룞以? ?덈갑?앹쑝濡쒕뒗 濡쒖쭅?놁쓬

	// maxMental moved = 0f;         // 理쒕? ?뺤떊??>?쏅궇 ?뺤떊怨듦꺽 ?곗궛?쇰줈留??묐룞以?
	// mental moved    = 0f;         // ?꾩옱 ?뺤떊??

	// spotting moved       = 0f;   // 媛먯? ???쒖빞-?몄?-諛섏쓳 臾몄꽌(01-A)??"媛먯? ?ㅽ꺈". ?쒖빞/?몄? 嫄곕━쨌?몄?媛겶룹썝???몄? 踰붿쐞 諛섏?由꾩씠 ?꾨? ??媛믪쑝濡?寃곗젙?쒕떎(VisionMath).
	// leadershipRange moved = 0f;  // 吏?섎쾾??>?덈줈 異붽??? 濡쒖쭅?놁쓬
	// charisma moved        = 0f;  // 移대━?ㅻ쭏->?덈줈 異붽??? 濡쒖쭅?놁쓬

	// ??? ?쒖빞-?몄?-諛섏쓳 ?쒖뒪??愿????01_?쒖빞쨌?몄?踰붿쐞쨌媛?쒖꽦 臾몄꽌 v0.2 ????????????
	// stealth moved = 0f;         // ?????理쒖쥌 媛?쒖꽦????텛???몃? ?ㅽ꺈(01??10??. 嫄곕━/媛由?蹂댁젙 ?몃??곗떇? 05-A 臾몄꽌 遺?щ줈 ?ㅽ뀅(VisionMath.FinalVisibility 李멸퀬)
	// baseVisibility moved = 100f; // ???湲곕낯 媛?쒖꽦(01??9?? ???쇰컲 ?좊떅? 100, ??좏삎/?뱀닔 ?좊떅? ?곗씠?곕줈 ??쾶 ?ㅼ젙
	// attackVisibilityBoostTimer moved = 0f; // 怨듦꺽 ??媛?쒖꽦 ?곸듅 吏?띿떆媛???대㉧(珥? 01-A 9?? ??SkillAction.BeginAttackCast媛 怨듦꺽 ?ㅽ뻾 ???명똿, UnitFunction.OnUpdate媛 媛먯냼

	// ??? ?뺢퇋?붿슜 ?띿꽦 ????????????????????????????????????????????
	// sterngth moved    = 0f; // 洹쇰젰. ?뺢퇋?붾? ?듯빐 ?곗텧?댁빞 ??
	// Durability moved  = 0f; // ?닿뎄. ?뺢퇋?붾? ?듯빐 ?곗텧?댁빞 ??
	// agility moved     = 0f; // 誘쇱꺽. ?뺢퇋?붾? ?듯빐 ?곗텧?댁빞 ??
	public float concentration = 0f; // 吏묒쨷. ?뺢퇋?붾? ?듯빐 ?곗텧?댁빞 ??
	public float MagicPower  = 0f; // 留덈젰. ?뺢퇋?붾? ?듯빐 ?곗텧?댁빞 ??
	public float resistance  = 0f; // ??? ?뺢퇋?붾? ?듯빐 ?곗텧?댁빞 ??
	// sense moved       = 0f; // 媛먭컖. ?뺢퇋?붾? ?듯빐 ?곗텧?댁빞 ??
	public float leadership  = 0f; // ?듭넄. ?뺢퇋?붾? ?듯빐 ?곗텧?댁빞 ??

	// ??? ?뺢퇋??湲곗?媛?????????????????????????????????????????????
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

	// ?€?€?€ ?곗궛???꾩떆 ?ㅽ꺈 ?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
	// StatusEffects moved
	// CombatState moved
	/* removed PerceptionState { 
		perceptionRecords = new Dictionary<object, PerceptionRecord>(),
		visionOnlyNonEmptyTiles = new List<Vector3Int>(),
		detectedThreats = new List<ThreatTileData>(),
		} removed PerceptionState */
	// $v moved
	// $v moved

	// $v moved
	// $v moved
	// $v moved
	// $v moved
	// $v moved
	// $v moved
	// $v moved

	// ?€?€?€ ?곹깭?댁긽 ?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€


	// ?€?€?€ ?좊떅 諛곗튂 ?쒖뒪??濡ㅻ갚 ?꾨즺 ?€?€?€

	// 웨이브 유닛이 계단을 통해 다른 층으로 넘어가야 할 때 HumanWaveManager가 세팅 — Goal_UseStairs가
	// 이 값이 있고 현재 층과 다르면 최우선으로 계단을 찾아 이동/통과한다(GOAP 로직, 2026-07-23 사용자
	// 요청 "예외처리 없이 goap로직에 넣어도 되겠군"). Action_CrossStairs가 실제로 층을 넘기면 null로
	// 되돌린다.
	public int? pendingStairTargetFloor = null;
	public Vector2Int? currentExplorationTarget = null;

	public Vector2Int? playerMoveTarget    = null;
	public bool isManualMoveCommand        = false; // ?좎?媛€ 吏곸젒 ?대┃?섏뿬 ?대┛ ?대룞 紐낅졊?몄? ?щ?
	public Unit        playerAttackTarget   = null;
	public Vector3Int? playerInteractTarget = null;
	public int         playerCommandStuckTurns = 0;
	public Vector2Int position;
	public int        currentFloor = 0;        // ?꾩옱 ?좊떅???꾩튂??痢??뺣낫
	public Dir        currentDir   = Dir.DOWN;  // ?꾩옱 諛붾씪蹂대뒗 諛⑺뼢 (?쒖빞 湲곗?)
	public string     spriteVariation = "";     // ?ㅽ봽?쇱씠??諛붾━?먯씠??(?쇱씠釉뚮윭由?移댄뀒怨좊━紐?



	// ?€?€?€ ?몄?쨌?뺣낫?먯젙쨌?ㅽ뙣泥섎━ ?쒖뒪??愿€????02_?몄?쨌?뺣낫?먯젙쨌?ㅽ뙣泥섎━_?쒖뒪??v0.2 ?€?€?€?€?€?€?€?€
	// 4?? ?€?곷퀎(???좊떅=Unit 李몄“, ?ㅻ툕?앺듃=InteractableObject.Id) 吏€???몄? ?곹깭. ?몃━嫄??쒖젏?먮쭔
	// UnitFunction.CastRay/ResolveReachedTarget/ForceRollPerception??媛깆떊?쒕떎 ??personalSpottedEnemies?€ ?щ━ 留?
	// UpdateFOV ?몄텧留덈떎 Clear?섏? ?딅뒗??PerceptionRecord.cs 二쇱꽍 李멸퀬).


	// 20?? ?섏긽???€???뺤씤 ?€湲?以묒씤 ?덉퐫?쒓? ?섎굹?쇰룄 ?덉쑝硫?寃쎄퀎 ?곹깭 ??10??媛먯? 蹂댁젙(+20)怨?
	// 01-A 10??援?11?? ?쒖빞 諛⑺뼢 ?꾪솚 ?곗꽑?쒖쐞??Alert ?ъ쑀媛€ ??媛믪쓣 李몄“?쒕떎.

	// 9?? ?뺤떊??蹂댁젙(?몃쪟 ?꾩슜, 紐ъ뒪?곕뒗 ??긽 0) ??PerceptionMath.MentalCorrectionForHuman 李멸퀬.
	public bool CanPerceive => StatusEffects.State.stunDuration <= 0f;
	public float GetMentalVisibilityCorrection() => (this is Human) ? PerceptionMath.MentalCorrectionForHuman(BaseStat.mental, BaseStat.maxMental) : 0f;

	// 01??7??01-A 7?? ?쒖빞 踰붿쐞 ??+ ?몄? 踰붿쐞 諛?+ 鍮꾩뼱?덉? ?딆? ?€??紐⑸줉(?대쾲 UpdateFOV ?몄텧
	// 湲곗? ?꾩떆 ?ㅻ깄?????€?κ컪 ?꾨떂, 留?UpdateFOV留덈떎 鍮꾩슦怨??ㅼ떆 梨꾩슫??. 紐⑺몴/寃쎈줈 ?ъ꽕?뺤쓣 ?ㅻ（??
	// 10_紐⑺몴?ㅼ젙쨌?대룞寃쎈줈쨌?ъ꽕??臾몄꽌媛€ ?꾩쭅 ?대뜑???놁뼱 ??由ъ뒪?몃? ?ㅼ죣濡??뚮퉬?섎뒗 怨녹? ?녿떎 ??
	// 洹?臾몄꽌媛€ ?앷린硫?VisionMath.NonEmptyTileTempWeight?€ ?④퍡 諛붾줈 ?????덈룄濡??곗씠?곕쭔 誘몃━ 梨꾩썙?붾떎.


	// 01-A 9?? 怨듦꺽???좊떅?€ 怨좎젙 ?쒓컙(5珥? ?숈븞 媛€?쒖꽦??+10 ?곸듅?쒕떎. ?ш났寃???吏€?띿떆媛꾨쭔
	// 珥덇린?붾릺怨??곸듅?됱? ?꾩쟻?섏? ?딅뒗??臾몄꽌媛€ "吏€?띿떆媛꾩쓣 ?ㅼ떆 5珥덈줈 珥덇린???쇨퀬留?紐낆떆??肉?
	// "?곸듅?됱씠 異붽??쒕떎"怨좊뒗 ?섏? ?딆븘, ?곹븳 100 洹쒖튃怨??④퍡 媛€???⑥닚?섍쾶 ?댁꽍??寃????먮떒 洹쇨굅??
	// 援ы쁽?꾪솴 臾몄꽌??湲곗옱).
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

	// ??? ?뺢퇋???⑥닔 ?????????????????????????????????????????????????
	// 0%~200% 踰붿쐞濡??대옩?? 100%媛 湲곗?媛믨낵 ?쇱튂?섎룄濡?
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

		// 湲곕낯 ?λ젰移?怨꾩궛
		// 洹쇰젰 = 臾쇰━ 怨듦꺽???뺢퇋??
		BaseStat.sterngth = nAtk;

		// ?닿뎄 = 泥대젰 45 + 臾쇰갑 45 + ?ъ깮 10
		BaseStat.Durability = nHp * 0.45f + nPDef * 0.45f + nRegen * 0.10f;

		// 誘쇱꺽 = 怨듭냽 35 + ?대룞 25 + 諛섏쓳 40
		BaseStat.agility = nAtkSpd * 0.35f + nMove * 0.25f + nReact * 0.40f;

		// 吏묒쨷 = 移섎챸 60 + 荑④컧 40
		concentration = nCrit * 0.60f + nCdr * 0.40f;

		// 留덈젰 = 留덇났 60 + 留덈굹 40
		MagicPower = nMatk * 0.60f + nMp * 0.40f;

		// ???(紐ъ뒪???덉쇅)
		if (this is Monster)
			resistance = nMDef * 0.5f + nStatus * 0.5f;
		else
			resistance = nMDef * 0.35f + nStatus * 0.35f + nMental * 0.30f;

		// 媛먭컖 = 媛먯?
		BaseStat.sense = nSpot;

		// ?듭넄 = 吏?섎쾾??50 + 移대━?ㅻ쭏 50
		leadership = nLeadRange * 0.5f + nCharisma * 0.5f;
	}

	// ?ㅽ꺈 ?곸슜? ?댁젣 UnitGenerate媛 ?ㅽ룿???꾨━?뱀쓽 UnitVisualDefinition.ApplyStatsTo(unit)??
	// SetupStats() ?몄텧 ?꾩뿉 ?대떦?쒕떎. ?ш린?쒕뒗 洹?湲곕낯 ?ㅽ꺈?쇰줈遺???뚯깮 ?ㅽ꺈留?怨꾩궛?쒕떎.
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

	// ??? 異붿긽 硫붿꽌????????????????????????????????????????????????????
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

		if (_gameSession.unitGrid.ContainsKey(oldKey))
			_gameSession.unitGrid.Remove(oldKey);

		position = targetPos;

		Vector3Int newKey = new Vector3Int(position.x, position.y, currentFloor);
		_gameSession.unitGrid[newKey] = this;
	}

	public abstract void UpdateFOV(List<Unit> allUnits);

	// 01-A 11?? ?쒖빞 諛⑺뼢 ?꾪솚 ?곗꽑?쒖쐞 ?먯젙 ???대쾲 ?댁뿉 ?쒖꽦?붾맂 ?꾨낫??以?媛???믪? ?곗꽑?쒖쐞瑜?
	// 怨⑤씪 currentDir瑜?媛깆떊?쒕떎. GameSession.ProcessUnitAction??ExecuteAction() ?댄썑, UpdateFOV()
	// ?댁쟾???몄텧?쒕떎(洹몃옒???대룞?쇰줈 媛깆떊??currentDir瑜?"?대룞 以? ?꾨낫??湲곕낯媛믪쑝濡??쒖슜?????덈떎).
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
	public abstract void OnDirectHit(Unit attacker, ThreatTileData threat);
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

	// 媛쒖씤 吏??????ㅻ툕?앺듃/紐ъ뒪??紐⑷꺽/諛??꾪뿕?꽷룻씎誘몃룄) ??吏?꾧????뺣━ 臾몄꽌 湲곗? "吏?꾨뒗
	// ?몃쪟留??ㅺ퀬 ?덉뼱???쒕떎"??吏?쒖뿉 ?곕씪 Human?먮쭔 ?붾떎(Monster/base Unit?먮뒗 ?놁쓬).
	public PersonalMapKnowledge personalMap => Memory.personalMap;
	public List<string> collectedObjects    => Memory.collectedObjects;

	// ???좊떅???랁븳 ?뚰떚(?덈떎硫? ??13???뚰떚 ?꾨㈇/6???⑥씠釉?醫낅즺 ?앹〈??諛섏쁺 ?먯젙???곗씤??
	// GameSession.CreateParty()媛 ?뚰떚 ?앹꽦 ??梨꾩썙以?? ?뚰떚 ?놁씠 ?ㅽ룿???몃쪟(?붾쾭洹??⑤룆 ?뚰솚
	// ????null濡??좎? ???뚰떚 愿???먯젙 ??곸뿉???먯뿰???쒖쇅?쒕떎.
	public Party party { get => UnitParty.party; set => UnitParty.party = value; }

	// 03문서 5장(조사)/10장(대기)/6장(보호 포메이션) — 인류 전용(13장 표, 몬스터는 "컨셉에 따라"만
	// 명시돼 있어 실제 컨셉 시스템이 생기기 전까지는 인류만 구현). null이면 각각 진행 중 아님.
	public InvestigationState currentInvestigation;
	public WaitState          currentWait;
	public FormationState     currentFormation;
	// 7-3장(2026-07-27 신규): 리더 전용 — 이 유닛이 파티 리더일 때만 의미가 있다(CorePartySystem 참고).
	public CoreInteractionState currentCoreInteraction;

	// 6-1장 두 번째 조건("직접 시야로 상호작용 유닛을 확인한 일반 탐색 유닛") + 8-2장 판정에 쓴다 —
	// 함정 대응이나 조사 중이면(=다른 유닛이 나를 호위할 만한 상황이면) true. 함정 쪽은 "함정 위치에
	// 실제로 도달했을 때"만 true로 좁혔다(2026-07-22, 사용자 신고 — 함정이 이미 해제됐는데도 주변이
	// 경계 태세를 취함) — 9-5장 순서가 "해제 유닛이 함정 위치 도달 → 상호작용 정보 전파 및 보호
	// 포메이션 형성 → 함정 해제 시작"이라, 발견 직후 5초 합류 대기나 이동 중(아직 도착 전)에는
	// 보호 포메이션이 형성되면 안 된다. 예전엔 함정을 인지한 순간부터(도착 전 포함) true였다.
	public bool IsInteracting => IsActivelyHandlingTrap() || currentInvestigation != null || currentCoreInteraction != null;

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

			float d = Vector2Int.Distance(position, new Vector2Int(obj.Position.x, obj.Position.y));
			if (d < bestDist) { bestDist = d; best = obj; }
		}
		return best;
	}

	// 5-3장 5가지 조건 중 "비전투/비도주"만 여기서 함께 확인한다(정확 인지/선택/도달은 위
	// FindInvestigateTarget과 Action_MoveToInvestigateTarget.Execute의 이동 로직이 담당).
	public bool HasReachableInvestigateTarget()
	{
		if (personalSpottedEnemies.Count > 0) return false; // 비전투 — 적이 보이면 조사 시작 안 함(전투 우선)
		return FindInvestigateTarget() != null;
	}

	// 6-1장 두 번째 조건("자신의 시야 범위 안에서 상호작용 유닛을 직접 확인") — 07_전파 문서가 없어
	// 지금 구현 가능한 유일한 트리거. 정확 인지 확률 판정을 다시 거치지 않고(아군은 "보이면 안다"로
	// 취급, 시야인지반응_03_GOAP목표우선순위표_2026-07-22.txt 3-5절 근거) 시야 범위 안의 같은 파티
	// 인류 중 IsInteracting인 대상을 직접 찾는다.
	public Human FindDirectlyVisibleInteractingAlly()
	{
		if (party == null || Session == null) return null;

		float viewDistance = VisionMath.ViewDistance(spotting);
		Human best = null;
		float bestDist = float.MaxValue;

		foreach (var m in party.Members)
		{
			if (m == null || m == this || m.hp <= 0 || m.currentFloor != currentFloor) continue;
			if (!m.IsInteracting) continue;
			// 이미 다른 유닛을 호위 중이면 그 대상이 아닌 새 상호작용 유닛으로는 갈아타지 않는다
			// (6-2장 "기존 포메이션 유지").
			if (currentFormation != null && currentFormation.EscortTarget != null && currentFormation.EscortTarget != m) continue;

			float d = Vector2Int.Distance(position, m.position);
			if (d > viewDistance) continue;
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

	// escortTarget이 실제로 상호작용(조사 진행/함정 해제 진행/코어 조사)을 시작했는지 — 아직
	// 목적지로 "이동 중"인 단계와 구분한다(위 GetEscortSlotPosition 주석 참고).
	private bool IsEscortTargetActivelyInteracting(Human escortTarget)
	{
		if (escortTarget.currentCoreInteraction != null) return escortTarget.currentCoreInteraction.Active;
		if (escortTarget.currentInvestigation != null) return escortTarget.currentInvestigation.PenaltyActive;
		if (escortTarget.currentTrapInteraction != null) return escortTarget.currentTrapInteraction.PenaltyActive;
		return false;
	}

	// 위 GetEscortSlotPosition이 겹침 판정에 쓰는 "이 유닛이 지금 상호작용 중인 오브젝트의 위치" —
	// 조사/함정/코어(7-3장) 셋 중 진행 중인 것을 조회한다.
	private Vector2Int? GetInteractionObjectPosition(Human escortTarget)
	{
		if (escortTarget.currentCoreInteraction != null)
			return new Vector2Int(escortTarget.currentCoreInteraction.CorePosition.x, escortTarget.currentCoreInteraction.CorePosition.y);
		if (escortTarget.currentInvestigation != null)
			return new Vector2Int(escortTarget.currentInvestigation.TargetPosition.x, escortTarget.currentInvestigation.TargetPosition.y);
		if (escortTarget.currentTrapInteraction != null)
			return new Vector2Int(escortTarget.currentTrapInteraction.TrapPosition.x, escortTarget.currentTrapInteraction.TrapPosition.y);
		return null;
	}

	public override void JudgeState()
	{
		base.JudgeState();
		// ?몃쪟 ?곹깭 ?먮떒 濡쒖쭅 異붽?
	}
}

public class Monster : UnitFunction
{
    public Monster()
    {
        FactionBehavior = new PlayerMonsterBehavior();
    }

	public override void JudgeState()
	{
		base.JudgeState();
		// 紐ъ뒪???곹깭 ?먮떒 濡쒖쭅 異붽?
	}
}





