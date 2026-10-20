using System.Collections.Generic;

// 말풍선 대사의 상황 구분 — 유닛 머리 위 텍스트 연출 전용. 대사는 연출일 뿐 전파·사망·도주 정보가 아니며(후퇴도주 명세서 4-3·7-3장), 이 모듈은 게임 상태를 읽기만 하고 바꾸지 않는다.
public enum SpeechId
{
	FleeConfirmed,        // 개인 도주 확정
	FleeWitnessed,        // 파티원의 도주를 직접 목격
	EncounterGap2,        // 조우 충격 +2
	EncounterPanic,       // 조우 충격 +3, 신규 공황
	EncounterFleeMax,     // 조우 충격 +3, 기존 공황 → −100
	PartyRetreatByHp,     // 파티 후퇴 시작(평균 HP 기준)
	PartyRetreatByMental, // 파티 후퇴 시작(평균 정신력 기준)
	PersonalRetreat,      // 리더 없는 인류의 개인 후퇴 시작
	ArriveBase,           // 거점 첫 도착
	RestCompleteResume,   // 휴식 완료, 목표 미달성 → 탐험 재개
	RestCompleteLeave,    // 휴식 완료, 목표 달성 → 출구로
	AbsentMember,         // 집결 중 미집결자 언급
	TrapFound,            // 함정 최초 발견
	AllyDeathSeen,        // 파티원 사망을 직접 확인
	CoreFound,            // 코어 발견
	RallyStart,           // 집결 명령

	// ── 파티 이동·돌파·진입 (리더 명령) ──
	AdvanceOrder,         // 집결 완료 → 다음 방으로 이동 지시
	BreachOrder,          // 문 파괴 지시
	BreachFarDoor,        // 가까운 문을 뚫고 먼 쪽 문도 부수라는 지시
	BreachRetry,          // 돌파 실패 → 대열로 물러나 재시도
	DoorOpened,           // 문 파괴 완료, 진입로 확보
	EntryOrder,           // 진입 지시(한 명씩 역할 순)
	EntryGo,              // 출발 허가를 받은 파티원
	EntryDone,            // 입장 완료
	WaveStart,            // 침공 개시(파티가 던전으로 진입)
	Resortie,             // 점령 파티 재출격
	CoreReported,         // 리더가 코어 소식을 들음

	// ── 전파·소리 반응 ──
	EnemyWarned,          // 파티원에게 적 정보를 전해 들음
	TrapWarned,           // 파티원에게 함정 정보를 전해 들음
	SoundMove,            // 이동음 감지
	SoundCombat,          // 공격 실행음·피격 발생 공격음 감지
	SoundScream,          // 피격 비명 감지
	SoundDeath,           // 사망음 감지
	SoundTrap,            // 함정 작동음 감지

	// ── 전투 ──
	EnemySpotted,         // 적을 직접 정확 인지
	AmbushHit,            // 누구인지 모르는 공격에 피격
	CombatEngage,         // 전투 진입
	HpHalf,               // HP 50% 아래로 하락
	HpLow,                // HP 25% 아래로 하락
	PanicHit,             // 공황 상태에서 피격
	KillEnemy,            // 몬스터 처치
	PostCombat,           // 전투 종료 → 주변 확인
	CastHeal, CastShield, CastBuff, CastCurse, CastFireball, CastBackstab, // 스킬 시전

	// ── 후퇴·도주·공황 ──
	RetreatOrderHeard,    // 파티 후퇴 명령을 받음
	RetreatBlockedDoor, RetreatBlockedEnemy, RetreatBlockedTrap, RetreatBlockedNoPath, // 후퇴 이동 중 길이 막힘
	FleeRunning,          // 도주 중 숨이 찬 소리
	FleeSurvived,         // 도주 판정을 버텨냄
	MentalTension, MentalFear, MentalPanic, // 정신력 단계 악화

	// ── 상황 판단 ──
	Investigate,          // 조사 대상에 도착
	LootPicked,           // 전리품 회수
	TrapDisarmStart, TrapDisarmed, TrapDisarmFailed, // 함정 해제
	TakeStairs,           // 계단 이동
	CoreAttackStart,      // 코어 공격 시작
	DoorAttackStart,      // 문 공격 시작
	CoreDestroyed,        // 코어 파괴
	SalvagePicked,        // 코어 잔해 획득(반출 시작)
	BaseBuildStart,       // 점령 파티 기초 시설 건설 시작
	BaseBuildWork,        // 기초·방어 시설 공사 중 일꾼의 작업 소리(주기 반복)
	BaseBuilt,            // 기초 시설 완공(기초 거점 형성)
	DefenseBuilt,         // 예정 방어 시설 완공(방어 거점 형성)
}

// 색·글자 크기를 정하는 어조.
public enum SpeechTone { Calm, Alert, Fear, Panic }

public readonly struct SpeechEntry
{
	public readonly string[] Lines;      // 후보 문구 — {S}는 대상 이름, {S가}는 조사 포함 이름
	public readonly SpeechTone Tone;
	public readonly float Seconds;       // 표시 시간(게임 시간)
	public readonly int Priority;        // 이미 떠 있는 말풍선을 덮어쓸 수 있는 순위(같거나 높을 때만)
	public readonly float UnitCooldown;  // 같은 유닛이 같은 대사(같은 대상)를 다시 말하기까지
	public readonly float GlobalCooldown; // 누가 말했든 같은 대사를 다시 띄우기까지 — 여러 명이 동시에 외치는 합창 방지

	public SpeechEntry(string[] lines, SpeechTone tone, float seconds, int priority, float unitCooldown, float globalCooldown = 0f)
	{
		Lines = lines; Tone = tone; Seconds = seconds; Priority = priority; UnitCooldown = unitCooldown; GlobalCooldown = globalCooldown;
	}
}

// 상황별 대사 목록. 명세서가 직접 적은 문구(4-3·7-3장)는 그대로 쓰고, 나머지는 행동의 이유를 캐릭터 말투로 설명하는 연출 문구다.
public static class SpeechCatalog
{
	static readonly Dictionary<SpeechId, SpeechEntry> Entries = new Dictionary<SpeechId, SpeechEntry>
	{
		// ── 명세서 문구 ──
		{ SpeechId.FleeConfirmed,    new SpeechEntry(new[] { "난 살아남을거야!!!!", "흐아아악", "더는 못 싸워!!" }, SpeechTone.Panic, 2.6f, 90, 30f) },
		{ SpeechId.FleeWitnessed,    new SpeechEntry(new[] { "어디가!!!", "이봐! 돌아와!" }, SpeechTone.Alert, 2.0f, 60, 15f, 2f) },
		{ SpeechId.EncounterGap2,    new SpeechEntry(new[] { "상대하기 버겁다…" }, SpeechTone.Fear, 2.4f, 70, 20f) },
		{ SpeechId.EncounterPanic,   new SpeechEntry(new[] { "몸이 움직이지 않아…" }, SpeechTone.Panic, 2.4f, 85, 20f) },
		{ SpeechId.EncounterFleeMax, new SpeechEntry(new[] { "도망쳐야 해!" }, SpeechTone.Panic, 2.4f, 95, 20f) },
		{ SpeechId.AbsentMember,     new SpeechEntry(new[] { "{S가} 없는데?" }, SpeechTone.Alert, 2.4f, 30, 30f, 4f) },

		// ── 후퇴·상황판단 연출 문구 ──
		{ SpeechId.PartyRetreatByHp,     new SpeechEntry(new[] { "더는 버틸 수 없다… 후퇴한다!", "전원 물러서! 일단 살아야 해!" }, SpeechTone.Alert, 3.2f, 80, 20f) },
		{ SpeechId.PartyRetreatByMental, new SpeechEntry(new[] { "다들 겁에 질렸어… 물러난다!", "이대로는 안 된다. 철수한다!" }, SpeechTone.Alert, 3.2f, 80, 20f) },
		{ SpeechId.PersonalRetreat,      new SpeechEntry(new[] { "혼자서는 무리야… 빠져나가자.", "더 버티면 죽는다. 여길 벗어나야 해." }, SpeechTone.Fear, 3.0f, 75, 20f) },
		{ SpeechId.ArriveBase,           new SpeechEntry(new[] { "여기서 숨 좀 돌리자.", "일단 여기라면 안전하겠지…" }, SpeechTone.Calm, 2.6f, 30, 20f, 3f) },
		{ SpeechId.RestCompleteResume,   new SpeechEntry(new[] { "충분히 쉬었다. 다시 간다!", "정비 끝. 임무를 이어가자." }, SpeechTone.Calm, 2.8f, 30, 20f) },
		{ SpeechId.RestCompleteLeave,    new SpeechEntry(new[] { "임무는 끝났다. 돌아가자.", "할 일은 다 했어. 철수한다." }, SpeechTone.Calm, 2.8f, 30, 20f) },
		{ SpeechId.TrapFound,            new SpeechEntry(new[] { "함정이다! 조심해!", "잠깐, 저기 함정이 있어." }, SpeechTone.Alert, 2.4f, 40, 12f, 3f) },
		{ SpeechId.AllyDeathSeen,        new SpeechEntry(new[] { "이럴 수가…!", "안 돼… 이게 무슨…" }, SpeechTone.Fear, 2.4f, 55, 10f, 3f) },
		{ SpeechId.CoreFound,            new SpeechEntry(new[] { "코어다! 여기가 목표야.", "찾았다, 코어야!" }, SpeechTone.Alert, 2.6f, 40, 20f, 4f) },
		{ SpeechId.RallyStart,           new SpeechEntry(new[] { "모여! 정비한다.", "전원 집결! 대열을 갖춰라." }, SpeechTone.Calm, 2.4f, 20, 20f) },

		// ── 파티 이동·돌파·진입: 리더가 명령하고 파티원이 따른다 ──
		{ SpeechId.AdvanceOrder,  new SpeechEntry(new[] { "다음 방으로 간다. 문 앞에 대열을 갖춰라!", "이동한다! 전원 문 앞에 모여라!" }, SpeechTone.Alert, 3.0f, 45, 20f, 3f) },
		{ SpeechId.BreachOrder,   new SpeechEntry(new[] { "문을 부숴라!", "돌파한다! 문부터 열어!", "막힌 문이다. 전원 공격!" }, SpeechTone.Alert, 2.6f, 50, 15f, 3f) },
		{ SpeechId.BreachFarDoor, new SpeechEntry(new[] { "한 겹 더 남았다! 먼 쪽 문도 부숴라!" }, SpeechTone.Alert, 2.8f, 50, 15f) },
		{ SpeechId.BreachRetry,   new SpeechEntry(new[] { "안 되겠다. 대열로 물러났다가 다시 친다!" }, SpeechTone.Alert, 3.0f, 50, 20f) },
		{ SpeechId.DoorOpened,    new SpeechEntry(new[] { "길이 열렸다!", "좋아, 문이 뚫렸다!" }, SpeechTone.Calm, 2.2f, 50, 10f, 3f) },
		{ SpeechId.EntryOrder,    new SpeechEntry(new[] { "한 명씩 들어간다! 대열을 유지해라!", "진입한다! 앞에서부터 차례로!" }, SpeechTone.Alert, 3.0f, 50, 15f, 3f) },
		{ SpeechId.EntryGo,       new SpeechEntry(new[] { "간다!", "들어갑니다!", "먼저 가겠습니다!" }, SpeechTone.Calm, 1.8f, 25, 30f, 2.2f) },
		{ SpeechId.EntryDone,     new SpeechEntry(new[] { "전원 들어왔다. 각자 방을 살펴라!", "진입 완료. 흩어져서 수색한다!" }, SpeechTone.Calm, 2.8f, 40, 20f) },
		{ SpeechId.WaveStart,     new SpeechEntry(new[] { "침공을 개시한다! 던전으로 진입!", "가자! 이 던전을 정복한다!" }, SpeechTone.Alert, 3.2f, 45, 60f) },
		{ SpeechId.Resortie,      new SpeechEntry(new[] { "정비는 끝났다. 다시 출격한다!" }, SpeechTone.Alert, 3.0f, 50, 20f) },
		{ SpeechId.CoreReported,  new SpeechEntry(new[] { "코어를 찾았다고? 전원 집결해라!", "보고 받았다. 코어부터 처리한다!" }, SpeechTone.Alert, 3.0f, 45, 20f, 3f) },

		// ── 전파·소리 반응: 직접 듣거나 전해 들은 사람만 반응한다 ──
		{ SpeechId.EnemyWarned, new SpeechEntry(new[] { "적이 나타났다고?!", "적이 있다고? 전투 준비!" }, SpeechTone.Alert, 2.2f, 50, 12f, 3f) },
		{ SpeechId.TrapWarned,  new SpeechEntry(new[] { "함정이 있다고? 조심하자.", "알았다, 함정 구역은 피해 가자." }, SpeechTone.Calm, 2.4f, 35, 12f, 3f) },
		{ SpeechId.SoundMove,   new SpeechEntry(new[] { "…발소리가 들려.", "누군가 움직이고 있어." }, SpeechTone.Calm, 2.2f, 20, 15f, 3f) },
		{ SpeechId.SoundCombat, new SpeechEntry(new[] { "싸우는 소리다!", "저쪽에서 전투가 벌어졌어!" }, SpeechTone.Alert, 2.2f, 35, 10f, 2f) },
		{ SpeechId.SoundScream, new SpeechEntry(new[] { "방금 비명 소리 들었어?", "비명이다! 무슨 일이지?" }, SpeechTone.Fear, 2.4f, 40, 10f, 2f) },
		{ SpeechId.SoundDeath,  new SpeechEntry(new[] { "…뭔가 쓰러지는 소리가 났어." }, SpeechTone.Fear, 2.4f, 40, 10f, 3f) },
		{ SpeechId.SoundTrap,   new SpeechEntry(new[] { "함정이 작동했다!" }, SpeechTone.Alert, 2.2f, 40, 10f, 3f) },

		// ── 전투 ──
		{ SpeechId.EnemySpotted, new SpeechEntry(new[] { "적이다!!!", "적 발견! 전투 준비!", "{S이다}! 조심해!" }, SpeechTone.Alert, 2.2f, 50, 20f, 3f) },
		{ SpeechId.AmbushHit,    new SpeechEntry(new[] { "윽! 어디서 공격이냐!", "누, 누구냐!" }, SpeechTone.Fear, 2.2f, 55, 15f) },
		{ SpeechId.CombatEngage, new SpeechEntry(new[] { "간다!", "여기서 막는다!", "공격한다!" }, SpeechTone.Alert, 1.8f, 35, 25f, 1.5f) },
		{ SpeechId.HpHalf,       new SpeechEntry(new[] { "큭… 꽤 아프군.", "아직 버틸 만해… 아직은." }, SpeechTone.Fear, 2.0f, 50, 20f) },
		{ SpeechId.HpLow,        new SpeechEntry(new[] { "이대로는… 위험해!", "헉… 더는 못 버텨!" }, SpeechTone.Fear, 2.4f, 65, 20f) },
		{ SpeechId.PanicHit,     new SpeechEntry(new[] { "으아아악!", "싫어, 아파!!", "살려줘!" }, SpeechTone.Panic, 1.8f, 70, 8f) },
		{ SpeechId.KillEnemy,    new SpeechEntry(new[] { "해치웠다!", "하나 처치!", "다음은 누구냐!" }, SpeechTone.Alert, 1.8f, 30, 12f, 1.5f) },
		{ SpeechId.PostCombat,   new SpeechEntry(new[] { "끝난 건가…? 주변을 살펴보자." }, SpeechTone.Calm, 2.4f, 20, 25f, 3f) },
		{ SpeechId.CastHeal,     new SpeechEntry(new[] { "치료한다! 버텨!", "상처를 봐 줄게!" }, SpeechTone.Calm, 2.0f, 30, 12f, 1.5f) },
		{ SpeechId.CastShield,   new SpeechEntry(new[] { "방어막이다! 내 뒤로!", "버텨라, 보호막을 건다!" }, SpeechTone.Calm, 2.0f, 30, 12f, 1.5f) },
		{ SpeechId.CastBuff,     new SpeechEntry(new[] { "힘내라! 사기를 올려 주마!", "싸울 힘을 줄게!" }, SpeechTone.Calm, 2.2f, 30, 20f, 2f) },
		{ SpeechId.CastCurse,    new SpeechEntry(new[] { "저주를 받아라!", "어둠이 너를 삼키리라…" }, SpeechTone.Alert, 1.8f, 30, 12f) },
		{ SpeechId.CastFireball, new SpeechEntry(new[] { "불꽃이여, 타올라라!", "전부 태워 주마!" }, SpeechTone.Alert, 2.0f, 30, 10f, 1.5f) },
		{ SpeechId.CastBackstab, new SpeechEntry(new[] { "등 뒤가 비었군…!" }, SpeechTone.Alert, 1.8f, 30, 12f) },

		// ── 후퇴·도주·공황 ──
		{ SpeechId.RetreatOrderHeard,    new SpeechEntry(new[] { "알겠습니다! 후퇴합니다!", "철수한다! 서둘러!", "물러서자!" }, SpeechTone.Fear, 2.2f, 60, 20f, 2.5f) },
		{ SpeechId.RetreatBlockedDoor,   new SpeechEntry(new[] { "문이 막혔어! 부수고 나간다!" }, SpeechTone.Fear, 2.4f, 70, 10f, 2f) },
		{ SpeechId.RetreatBlockedEnemy,  new SpeechEntry(new[] { "적이 길을 막았어! 뚫고 간다!" }, SpeechTone.Fear, 2.4f, 70, 10f, 2f) },
		{ SpeechId.RetreatBlockedTrap,   new SpeechEntry(new[] { "함정이다! 돌아가야 해!" }, SpeechTone.Fear, 2.4f, 70, 10f, 2f) },
		{ SpeechId.RetreatBlockedNoPath, new SpeechEntry(new[] { "길이 없어…! 어떡하지!" }, SpeechTone.Panic, 2.4f, 70, 10f, 2f) },
		{ SpeechId.FleeRunning,          new SpeechEntry(new[] { "헉헉…", "살려줘…!", "제발… 제발…" }, SpeechTone.Panic, 1.8f, 40, 10f) },
		{ SpeechId.FleeSurvived,         new SpeechEntry(new[] { "아직… 쓰러질 순 없어…!" }, SpeechTone.Fear, 2.2f, 65, 15f) },
		{ SpeechId.MentalTension,        new SpeechEntry(new[] { "왠지 불안해…", "뭔가 불길한데…" }, SpeechTone.Fear, 2.0f, 45, 40f) },
		{ SpeechId.MentalFear,           new SpeechEntry(new[] { "이건… 너무 위험해…", "손이 떨려…" }, SpeechTone.Fear, 2.4f, 60, 40f) },
		{ SpeechId.MentalPanic,          new SpeechEntry(new[] { "으아아아악!", "더는 안 돼…!" }, SpeechTone.Panic, 2.6f, 85, 30f) },

		// ── 상황 판단 ──
		{ SpeechId.Investigate,      new SpeechEntry(new[] { "저건 뭐지? 조사해 보자.", "수상한 물건이 있다." }, SpeechTone.Calm, 2.2f, 25, 15f, 2f) },
		{ SpeechId.LootPicked,       new SpeechEntry(new[] { "이건 챙겨가야지.", "쓸 만한 물건이군!" }, SpeechTone.Calm, 2.0f, 25, 12f, 2f) },
		{ SpeechId.TrapDisarmStart,  new SpeechEntry(new[] { "내가 해제할게. 조용히 해.", "건드리지 마. 해제해 볼게." }, SpeechTone.Calm, 2.4f, 40, 15f) },
		{ SpeechId.TrapDisarmed,     new SpeechEntry(new[] { "해제 완료!", "됐다. 함정은 처리했어." }, SpeechTone.Calm, 2.0f, 40, 8f) },
		{ SpeechId.TrapDisarmFailed, new SpeechEntry(new[] { "젠장, 실패다!", "윽, 손이 미끄러졌다!" }, SpeechTone.Fear, 2.0f, 40, 5f) },
		{ SpeechId.TakeStairs,       new SpeechEntry(new[] { "계단으로 간다.", "다음 층으로 이동한다." }, SpeechTone.Calm, 2.4f, 20, 30f, 6f) },
		{ SpeechId.CoreAttackStart,  new SpeechEntry(new[] { "코어를 부숴라!", "코어를 노려! 전력으로 공격!" }, SpeechTone.Alert, 2.4f, 40, 20f, 3f) },
		{ SpeechId.DoorAttackStart,  new SpeechEntry(new[] { "저 문을 부수고 나아간다!", "문이 막고 있군. 부숴라!" }, SpeechTone.Alert, 2.2f, 40, 20f, 3f) },
		{ SpeechId.CoreDestroyed,    new SpeechEntry(new[] { "코어 파괴! 잔해를 챙겨라!", "해냈다! 코어가 부서졌다!" }, SpeechTone.Alert, 3.0f, 55, 10f) },
		{ SpeechId.SalvagePicked,    new SpeechEntry(new[] { "잔해는 내가 챙긴다. 철수하자!", "이 잔해를 밖으로 가져가야 해." }, SpeechTone.Calm, 2.6f, 50, 20f, 4f) },
		{ SpeechId.BaseBuildStart,   new SpeechEntry(new[] { "여기에 거점을 세운다. 서둘러라!", "시설을 세운다. 경계를 늦추지 마라." }, SpeechTone.Calm, 2.6f, 35, 30f, 8f) },
		{ SpeechId.BaseBuildWork,    new SpeechEntry(new[] { "영차!", "여기 좀 잡아줘!", "조금만 더 버티자!", "단단히 고정해!", "망치 이리 줘!" }, SpeechTone.Calm, 1.8f, 15, 12f, 2.5f) },
		{ SpeechId.BaseBuilt,        new SpeechEntry(new[] { "기초 시설 완공! 이곳이 우리 거점이다.", "문을 세웠다. 여기까지는 우리 땅이야." }, SpeechTone.Calm, 3.0f, 45, 20f, 4f) },
		{ SpeechId.DefenseBuilt,     new SpeechEntry(new[] { "방어 시설 완공! 이제 버틸 수 있다.", "방어 거점이 완성됐다." }, SpeechTone.Calm, 3.0f, 45, 20f, 4f) },
	};

	public static bool TryGet(SpeechId id, out SpeechEntry entry) => Entries.TryGetValue(id, out entry);
}
