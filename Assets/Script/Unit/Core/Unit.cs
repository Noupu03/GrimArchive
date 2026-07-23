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

    private void OnEnable()
    {
        if (Components == null) Components = new List<IUnitComponent>();
        if (GetComponent<CombatStatComponent>() == null) Components.Add(new CombatStatComponent(this));
        if (GetComponent<HealthComponent>() == null) Components.Add(new HealthComponent(this));
        if (GetComponent<VisionStatComponent>() == null) Components.Add(new VisionStatComponent(this));
        if (GetComponent<BaseStatComponent>() == null) Components.Add(new BaseStatComponent(this));
        if (GetComponent<PerceptionComponent>() == null) Components.Add(new PerceptionComponent(this));
        if (GetComponent<MemoryComponent>() == null) Components.Add(new MemoryComponent(this));
        if (GetComponent<PartyComponent>() == null) Components.Add(new PartyComponent(this));
        if (GetComponent<AIStateComponent>() == null) Components.Add(new AIStateComponent(this));
        if (GetComponent<CombatStateComponent>() == null) Components.Add(new CombatStateComponent(this));
        if (GetComponent<StatusEffectsComponent>() == null) Components.Add(new StatusEffectsComponent(this));
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
	// baseInterest moved = 0f;        // 6-3?? ?좊떅 湲곕낯 ?λ???
	// baseDanger moved = 0f;          // 12-1?? ???湲곕낯 ?꾪뿕??
	// heavyHitThreshold moved = 10f;  // 3?? "?쇱젙 ?쇳빐???댁긽" ?먯젙 湲곗?媛?(?좊떅蹂??곗씠???뚯씠釉?

	// 4?? ???좊떅(?몃쪟 愿?먯쓽 愿李곗옄)????곷퀎濡??ㅺ퀬 ?덈뒗 媛쒖씤 媛以묒튂 湲곕줉.
	public readonly Dictionary<string, PersonalWeightRecord> personalWeights = new Dictionary<string, PersonalWeightRecord>();

	// 媛??理쒓렐?????좊떅?먭쾶 ?쇳빐瑜??낇엺 ??? ?щ쭩 ?쒖젏(GameSession.RemoveDeadUnit)?먯꽌
	// "?꾧? 泥섏튂?덈뒗吏"瑜??뚯븘???꾪뿕???댄빐??泥섏튂 ?대깽??E_MONSTER_KILL_SELF ??瑜?湲곕줉?????덉뼱???붾떎.
	public Unit lastAttacker;

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

	public Vector2Int? playerMoveTarget    = null;
	public bool isManualMoveCommand        = false; // ?좎?媛€ 吏곸젒 ?대┃?섏뿬 ?대┛ ?대룞 紐낅졊?몄? ?щ?
	public Unit        playerAttackTarget   = null;
	public Vector3Int? playerInteractTarget = null;
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
	public bool CanPerceive => GetComponent<StatusEffectsComponent>().State.stunDuration <= 0f;
	public float GetMentalVisibilityCorrection() => (this is Human) ? PerceptionMath.MentalCorrectionForHuman(GetComponent<BaseStatComponent>().mental, GetComponent<BaseStatComponent>().maxMental) : 0f;

	// 01??7??01-A 7?? ?쒖빞 踰붿쐞 ??+ ?몄? 踰붿쐞 諛?+ 鍮꾩뼱?덉? ?딆? ?€??紐⑸줉(?대쾲 UpdateFOV ?몄텧
	// 湲곗? ?꾩떆 ?ㅻ깄?????€?κ컪 ?꾨떂, 留?UpdateFOV留덈떎 鍮꾩슦怨??ㅼ떆 梨꾩슫??. 紐⑺몴/寃쎈줈 ?ъ꽕?뺤쓣 ?ㅻ（??
	// 10_紐⑺몴?ㅼ젙쨌?대룞寃쎈줈쨌?ъ꽕??臾몄꽌媛€ ?꾩쭅 ?대뜑???놁뼱 ??由ъ뒪?몃? ?ㅼ죣濡??뚮퉬?섎뒗 怨녹? ?녿떎 ??
	// 洹?臾몄꽌媛€ ?앷린硫?VisionMath.NonEmptyTileTempWeight?€ ?④퍡 諛붾줈 ?????덈룄濡??곗씠?곕쭔 誘몃━ 梨꾩썙?붾떎.


	// 01-A 9?? 怨듦꺽???좊떅?€ 怨좎젙 ?쒓컙(5珥? ?숈븞 媛€?쒖꽦??+10 ?곸듅?쒕떎. ?ш났寃???吏€?띿떆媛꾨쭔
	// 珥덇린?붾릺怨??곸듅?됱? ?꾩쟻?섏? ?딅뒗??臾몄꽌媛€ "吏€?띿떆媛꾩쓣 ?ㅼ떆 5珥덈줈 珥덇린???쇨퀬留?紐낆떆??肉?
	// "?곸듅?됱씠 異붽??쒕떎"怨좊뒗 ?섏? ?딆븘, ?곹븳 100 洹쒖튃怨??④퍡 媛€???⑥닚?섍쾶 ?댁꽍??寃????먮떒 洹쇨굅??
	// 援ы쁽?꾪솴 臾몄꽌??湲곗옱).
	public bool IsVisibilityBoosted => GetComponent<VisionStatComponent>().attackVisibilityBoostTimer > 0f;
	public void TriggerAttackVisibilityBoost() => GetComponent<VisionStatComponent>().attackVisibilityBoostTimer = VisionMath.AttackVisibilityBoostDuration;
	public float GetFinalVisibility() => VisionMath.FinalVisibility(GetComponent<VisionStatComponent>().baseVisibility, GetComponent<VisionStatComponent>().stealth, IsVisibilityBoosted);

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
		float nAtk      = Normalize(GetComponent<CombatStatComponent>().physicalAttack,    BASE_PHYSICAL_ATTACK);
		float nMatk     = Normalize(GetComponent<CombatStatComponent>().magicalAttack,     BASE_MAGICAL_ATTACK);
		float nHp       = Normalize(GetComponent<HealthComponent>().maxHp,             BASE_MAX_HP);
		float nMp       = Normalize(GetComponent<HealthComponent>().maxMp,             BASE_MAX_MP);
		float nPDef     = Normalize(GetComponent<CombatStatComponent>().physicalDefense,   BASE_PHYSICAL_DEF);
		float nMDef     = Normalize(GetComponent<CombatStatComponent>().magicalDefense,    BASE_MAGICAL_DEF);
		float nRegen    = Normalize(GetComponent<BaseStatComponent>().HPRegen,           BASE_HP_REGEN);
		float nStatus   = Normalize(GetComponent<BaseStatComponent>().statusResistance,  BASE_STATUS_RES);
		float nAtkSpd   = Normalize(GetComponent<CombatStatComponent>().attackspeed,       BASE_ATTACK_SPEED);
		float nReact    = Normalize(GetComponent<BaseStatComponent>().reaction,          BASE_REACTION);
		float nMove     = Normalize(GetComponent<BaseStatComponent>().walkSpeed,         BASE_WALK_SPEED);
		float nSpot     = Normalize(GetComponent<VisionStatComponent>().spotting,          BASE_SPOTTING);
		float nMental   = Normalize(GetComponent<BaseStatComponent>().mental,            BASE_MENTAL);
		float nLeadRange = Normalize(GetComponent<BaseStatComponent>().leadershipRange,  BASE_LEAD_RANGE);
		float nCharisma = Normalize(GetComponent<BaseStatComponent>().charisma,          BASE_CHARISMA);
		float nCrit     = Normalize(GetComponent<CombatStatComponent>().criticalChance,    BASE_CRIT);
		float nCdr      = Normalize(GetComponent<BaseStatComponent>().cooltimeReduction, BASE_CDR);

		// 湲곕낯 ?λ젰移?怨꾩궛
		// 洹쇰젰 = 臾쇰━ 怨듦꺽???뺢퇋??
		GetComponent<BaseStatComponent>().sterngth = nAtk;

		// ?닿뎄 = 泥대젰 45 + 臾쇰갑 45 + ?ъ깮 10
		GetComponent<BaseStatComponent>().Durability = nHp * 0.45f + nPDef * 0.45f + nRegen * 0.10f;

		// 誘쇱꺽 = 怨듭냽 35 + ?대룞 25 + 諛섏쓳 40
		GetComponent<BaseStatComponent>().agility = nAtkSpd * 0.35f + nMove * 0.25f + nReact * 0.40f;

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
		GetComponent<BaseStatComponent>().sense = nSpot;

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

	private GoapBrain _brain;
	public GoapBrain brain { get { if (_brain == null) _brain = new GoapBrain(); return _brain; } }

	public virtual void JudgeState()
	{
		if (GetComponent<StatusEffectsComponent>().State.stunDuration > 0f) return; // 스턴 중 행동 차단
		brain.JudgeState(this);
	}

	public virtual void ExecuteAction()
	{
		if (GetComponent<StatusEffectsComponent>().State.stunDuration > 0f) return;
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
    public Human()
    {
        FactionBehavior = new HumanFactionBehavior();
    }

	// 媛쒖씤 吏??????ㅻ툕?앺듃/紐ъ뒪??紐⑷꺽/諛??꾪뿕?꽷룻씎誘몃룄) ??吏?꾧????뺣━ 臾몄꽌 湲곗? "吏?꾨뒗
	// ?몃쪟留??ㅺ퀬 ?덉뼱???쒕떎"??吏?쒖뿉 ?곕씪 Human?먮쭔 ?붾떎(Monster/base Unit?먮뒗 ?놁쓬).
	// personalMap moved
	// collectedObjects moved

	// ???좊떅???랁븳 ?뚰떚(?덈떎硫? ??13???뚰떚 ?꾨㈇/6???⑥씠釉?醫낅즺 ?앹〈??諛섏쁺 ?먯젙???곗씤??
	// GameSession.CreateParty()媛 ?뚰떚 ?앹꽦 ??梨꾩썙以?? ?뚰떚 ?놁씠 ?ㅽ룿???몃쪟(?붾쾭洹??⑤룆 ?뚰솚
	// ????null濡??좎? ???뚰떚 愿???먯젙 ??곸뿉???먯뿰???쒖쇅?쒕떎.
	// party moved

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





