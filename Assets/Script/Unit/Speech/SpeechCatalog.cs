using System.Collections.Generic;

// 말풍선 대사의 상황 구분 — 유닛 머리 위 텍스트 연출 전용. 대사는 연출일 뿐 전파·사망·도주 정보가 아니며(후퇴도주 명세서 4-3·7-3장), 이 모듈은 게임 상태를 읽기만 하고 바꾸지 않는다.
// 2026-11-04 정리: 말풍선이 너무 많다는 피드백으로 "리더의 명령"과 "개인의 변화"만 남겼다. 행동 해설(스킬 시전·함정·공사·전투 진입/종료 등)은 삭제했다 — 다시 늘리지 말 것. 예외로 사용자가 되살리라고 한 소리 반응·"적이다!" 외침·조사·줍기만 "개인 반응"으로 남아 있다.
public enum SpeechId
{
	// ── 개인의 변화: 도주·공황·정신력·HP·동료 사망 ──
	FleeConfirmed,        // 개인 도주 확정
	FleeWitnessed,        // 파티원의 도주를 직접 목격
	FleeSurvived,         // 도주 판정을 버텨냄
	EncounterGap2,        // 조우 충격 +2
	EncounterPanic,       // 조우 충격 +3, 신규 공황
	EncounterFleeMax,     // 조우 충격 +3, 기존 공황 → −100
	PersonalRetreat,      // 리더 없는 인류의 개인 후퇴 시작
	MentalPanic,          // 정신력이 공황 단계로 진입(긴장·공포 단계 대사는 삭제)
	HpLow,                // HP 25% 아래로 하락(50% 하락·공황 중 피격 대사는 삭제)
	AllyDeathSeen,        // 파티원 사망을 직접 확인
	LeaderDeathAlarm,     // 리더가 죽은 순간 가장 가까운 파티원 한 명이 알림(최종 문구는 연출 문서에서 결정 — 임시)
	CommandCollapse,      // 후임 없는 지휘 붕괴
	AbsentMember,         // 집결 중 미집결자 언급

	// ── 개인 반응 (사용자 허용 2026-11-04 "소리 반응, 적이다! 외침, 조사, 줍기는 있어도 될듯") — 리더 명령도 개인 상태 변화도 아니지만 이 넷만 예외로 남긴다 ──
	EnemySpotted,         // 적을 직접 정확 인지(리더도 같은 외침 — 리더 전용 "적이다! 공격해!"는 2026-11-04에 삭제)
	SoundMove,            // 이동음 감지
	SoundCombat,          // 공격 실행음·피격 발생 공격음 감지
	SoundScream,          // 피격 비명 감지
	SoundDeath,           // 사망음 감지
	SoundTrap,            // 함정 작동음 감지
	Investigate,          // 조사 대상에 도착
	LootPicked,           // 전리품 회수

	// ── 리더 명령 (왕관): 파티 이동·집결·돌파·후퇴 지시 ──
	RallyStart,           // 집결 명령
	AdvanceOrder,         // 집결 완료 → 다음 방으로 이동 지시
	BreachOrder,          // 문 파괴 지시
	BreachFarDoor,        // 가까운 문을 뚫고 먼 쪽 문도 부수라는 지시
	BreachRetry,          // 돌파 실패 → 대열로 물러나 재시도
	EntryOrder,           // 진입 지시(한 명씩 역할 순)
	EntryDone,            // 입장 완료 → 각자 수색
	WaveStart,            // 침공 개시(파티가 던전으로 진입)
	Resortie,             // 점령 파티 재출격
	CoreReported,         // 리더가 코어 소식을 듣고 집결을 지시
	BaseBuildStart,       // 점령 파티 기초 시설 건설 지시
	PartyRetreatByHp,     // 파티 후퇴 시작(평균 HP 기준)
	PartyRetreatByMental, // 파티 후퇴 시작(평균 정신력 기준)
	RestCompleteResume,   // 휴식 완료, 목표 미달성 → 탐험 재개 지시
	RestCompleteLeave,    // 휴식 완료, 목표 달성 → 출구로 지시
	NormalReturn,         // 정상 귀환 결정(일반 방이 더는 없고 알려진 보스방만 남음)
	SuccessorTakesCommand, // 후임 승계

	// ── 리더 명령 (왕관): 전술 명령 — 리더 명령 설계명세서 5·12·16장 ──
	CmdFocusAttack,       // 집중 공격 지정(신규·교체)
	CmdObjectFocus,       // 차단물 공동 파괴 지정
	CmdStrike,            // 강공격
	CmdSuppress,          // 제압
	CmdProtect,           // 보호 — 위급한 아군 이름을 밝힌다
	CmdProtectSelf,       // 보호 — 위급한 사람이 리더 자신일 때
	CmdSuperiorFocus,     // 상위 공동 공격
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
	public readonly bool LeaderOrder;    // 리더의 명령 — 말하는 사람이 실제 지휘자일 때 말풍선 위에 왕관을 붙인다

	public SpeechEntry(string[] lines, SpeechTone tone, float seconds, int priority, float unitCooldown, float globalCooldown = 0f, bool leaderOrder = false)
	{
		Lines = lines; Tone = tone; Seconds = seconds; Priority = priority; UnitCooldown = unitCooldown; GlobalCooldown = globalCooldown; LeaderOrder = leaderOrder;
	}
}

// 상황별 대사 목록. 명세서가 직접 적은 문구(4-3·7-3·19장)는 그대로 쓰고, 나머지는 행동의 이유를 캐릭터 말투로 설명하는 연출 문구다.
public static class SpeechCatalog
{
	// 리더 명령 순위: 이동·집결 지시 60 < 전술 명령 70 < 후퇴·승계 80 — 개인 도주(90)·공황(85)보다는 낮다.
	const int OrderPriority = 60, TacticalPriority = 70;

	static SpeechEntry Order(string[] lines, SpeechTone tone, float seconds, int priority, float unitCooldown, float globalCooldown = 0f)
		=> new SpeechEntry(lines, tone, seconds, priority, unitCooldown, globalCooldown, leaderOrder: true);

	static readonly Dictionary<SpeechId, SpeechEntry> Entries = new Dictionary<SpeechId, SpeechEntry>
	{
		// ── 개인의 변화 — 명세서 문구 ──
		{ SpeechId.FleeConfirmed,    new SpeechEntry(new[] { "난 살아남을거야!!!!", "흐아아악", "더는 못 싸워!!" }, SpeechTone.Panic, 2.6f, 90, 30f) },
		{ SpeechId.FleeWitnessed,    new SpeechEntry(new[] { "어디가!!!", "이봐! 돌아와!" }, SpeechTone.Alert, 2.0f, 60, 15f, 2f) },
		{ SpeechId.EncounterGap2,    new SpeechEntry(new[] { "상대하기 버겁다…" }, SpeechTone.Fear, 2.4f, 70, 20f) },
		{ SpeechId.EncounterPanic,   new SpeechEntry(new[] { "몸이 움직이지 않아…" }, SpeechTone.Panic, 2.4f, 85, 20f) },
		{ SpeechId.EncounterFleeMax, new SpeechEntry(new[] { "도망쳐야 해!" }, SpeechTone.Panic, 2.4f, 95, 20f) },
		{ SpeechId.AbsentMember,     new SpeechEntry(new[] { "{S가} 없는데?" }, SpeechTone.Alert, 2.4f, 30, 30f, 4f) },

		// ── 개인의 변화 — 연출 문구 ──
		{ SpeechId.PersonalRetreat, new SpeechEntry(new[] { "혼자서는 무리야… 빠져나가자.", "더 버티면 죽는다. 여길 벗어나야 해." }, SpeechTone.Fear, 3.0f, 75, 20f) },
		{ SpeechId.FleeSurvived,    new SpeechEntry(new[] { "도망치지 않는다!" }, SpeechTone.Fear, 2.2f, 65, 15f) }, // 직관적인 문구(2026-11-04 사용자 요청)
		{ SpeechId.MentalPanic,     new SpeechEntry(new[] { "으아아아악!", "더는 안 돼…!" }, SpeechTone.Panic, 2.6f, 85, 30f) },
		{ SpeechId.HpLow,           new SpeechEntry(new[] { "체력이 너무 낮아졌어!", "체력이 바닥이야! 위험해!" }, SpeechTone.Fear, 2.4f, 65, 20f) }, // 직관적인 문구(2026-11-04 사용자 요청)
		{ SpeechId.AllyDeathSeen,   new SpeechEntry(new[] { "동료가 죽었어..", "동료가 죽었어! 안 돼…" }, SpeechTone.Fear, 2.4f, 55, 10f, 3f) }, // 직관적인 문구(2026-11-04 사용자 요청)
		{ SpeechId.LeaderDeathAlarm, new SpeechEntry(new[] { "리더가 죽었다!", "리더가 쓰러졌어!" }, SpeechTone.Panic, 2.8f, 82, 10f, 3f) },
		{ SpeechId.CommandCollapse, new SpeechEntry(new[] { "이번 원정은 망했어.", "이제 어떻게 하지?" }, SpeechTone.Fear, 3.0f, 80, 5f, 4f) },

		// ── 개인 반응 — 순위가 낮아(20~50) 리더 명령(60~)이 떠 있는 유닛에서는 밀린다 ──
		{ SpeechId.EnemySpotted, new SpeechEntry(new[] { "적이다!!!" }, SpeechTone.Alert, 2.2f, 50, 20f, 3f) }, // 적 발견 외침은 이 한 마디로 통일(2026-11-04) — 호출부가 넘기는 적 이름은 쿨다운 키로만 쓰이고 문구에는 안 들어간다
		{ SpeechId.SoundMove,    new SpeechEntry(new[] { "…발소리가 들려.", "누군가 움직이고 있어." }, SpeechTone.Calm, 2.2f, 20, 15f, 3f) },
		{ SpeechId.SoundCombat,  new SpeechEntry(new[] { "싸우는 소리다!", "저쪽에서 전투가 벌어졌어!" }, SpeechTone.Alert, 2.2f, 35, 10f, 2f) },
		{ SpeechId.SoundScream,  new SpeechEntry(new[] { "방금 비명 소리 들었어?", "비명이다! 무슨 일이지?" }, SpeechTone.Fear, 2.4f, 40, 10f, 2f) },
		{ SpeechId.SoundDeath,   new SpeechEntry(new[] { "…뭔가 쓰러지는 소리가 났어." }, SpeechTone.Fear, 2.4f, 40, 10f, 3f) },
		{ SpeechId.SoundTrap,    new SpeechEntry(new[] { "함정이 작동했다!" }, SpeechTone.Alert, 2.2f, 40, 10f, 3f) },
		{ SpeechId.Investigate,  new SpeechEntry(new[] { "저건 뭐지? 조사해 보자.", "수상한 물건이 있다." }, SpeechTone.Calm, 2.2f, 25, 15f, 2f) },
		{ SpeechId.LootPicked,   new SpeechEntry(new[] { "이건 챙겨가야지.", "쓸 만한 물건이군!" }, SpeechTone.Calm, 2.0f, 25, 12f, 2f) },

		// ── 리더 명령 (왕관): 집결·이동·돌파·진입·후퇴 지시 ──
		{ SpeechId.RallyStart,    Order(new[] { "모여! 정비한다.", "전원 집결! 대열을 갖춰라." }, SpeechTone.Calm, 2.4f, OrderPriority, 20f) },
		{ SpeechId.AdvanceOrder,  Order(new[] { "다음 방으로 간다. 문 앞에 대열을 갖춰라!", "이동한다! 전원 문 앞에 모여라!" }, SpeechTone.Alert, 3.0f, OrderPriority, 20f, 3f) },
		{ SpeechId.BreachOrder,   Order(new[] { "문을 부숴라!", "돌파한다! 문부터 열어!", "막힌 문이다. 전원 공격!" }, SpeechTone.Alert, 2.6f, OrderPriority, 15f, 3f) },
		{ SpeechId.BreachFarDoor, Order(new[] { "한 겹 더 남았다! 먼 쪽 문도 부숴라!" }, SpeechTone.Alert, 2.8f, OrderPriority, 15f) },
		{ SpeechId.BreachRetry,   Order(new[] { "안 되겠다. 대열로 물러났다가 다시 친다!" }, SpeechTone.Alert, 3.0f, OrderPriority, 20f) },
		{ SpeechId.EntryOrder,    Order(new[] { "한 명씩 들어간다! 대열을 유지해라!", "진입한다! 앞에서부터 차례로!" }, SpeechTone.Alert, 3.0f, OrderPriority, 15f, 3f) },
		{ SpeechId.EntryDone,     Order(new[] { "전원 들어왔다. 각자 방을 살펴라!", "진입 완료. 흩어져서 수색한다!" }, SpeechTone.Calm, 2.8f, OrderPriority, 20f) },
		{ SpeechId.WaveStart,     Order(new[] { "침공을 개시한다! 던전으로 진입!", "가자! 이 던전을 정복한다!" }, SpeechTone.Alert, 3.2f, OrderPriority, 60f) },
		{ SpeechId.Resortie,      Order(new[] { "정비는 끝났다. 다시 출격한다!" }, SpeechTone.Alert, 3.0f, OrderPriority, 20f) },
		{ SpeechId.CoreReported,  Order(new[] { "코어를 찾았다고? 전원 집결해라!", "보고 받았다. 코어부터 처리한다!" }, SpeechTone.Alert, 3.0f, OrderPriority, 20f, 3f) },
		{ SpeechId.BaseBuildStart, Order(new[] { "여기에 거점을 세운다. 서둘러라!", "시설을 세운다. 경계를 늦추지 마라." }, SpeechTone.Calm, 2.6f, OrderPriority, 30f, 8f) },
		{ SpeechId.PartyRetreatByHp,     Order(new[] { "더는 버틸 수 없다… 후퇴한다!", "전원 물러서! 일단 살아야 해!" }, SpeechTone.Alert, 3.2f, 80, 20f) },
		{ SpeechId.PartyRetreatByMental, Order(new[] { "다들 겁에 질렸어… 물러난다!", "이대로는 안 된다. 철수한다!" }, SpeechTone.Alert, 3.2f, 80, 20f) },
		{ SpeechId.RestCompleteResume,   Order(new[] { "충분히 쉬었다. 다시 간다!", "정비 끝. 임무를 이어가자." }, SpeechTone.Calm, 2.8f, OrderPriority, 20f) },
		{ SpeechId.RestCompleteLeave,    Order(new[] { "임무는 끝났다. 돌아가자.", "할 일은 다 했어. 철수한다." }, SpeechTone.Calm, 2.8f, OrderPriority, 20f) },
		{ SpeechId.NormalReturn,         Order(new[] { "더 갈 곳이 없군. 일단 돌아간다.", "보스방 말고는 다 봤다. 철수하자." }, SpeechTone.Calm, 3.0f, OrderPriority, 20f) },

		// ── 리더 명령 (왕관): 리더 명령 설계명세서 19장 — 명세서가 적은 문구는 그대로(단 "적이다! 공격해!"는 사용자 요청으로 뺐다) ──
		{ SpeechId.SuccessorTakesCommand, Order(new[] { "내가 지휘한다!" }, SpeechTone.Alert, 2.6f, 80, 5f) },

		// ── 리더 명령 (왕관): 전술 명령 — 명세서가 문구를 정하지 않아 연출 문구다. 명령 대상(공격할 적·제압할 적·지킬 아군)을 대사에 밝힌다(2026-11-04 사용자 요청): 대상 이름이 있으면 {S…}가 든 문구만 쓰이고(SpeechMath.PickLine), 대상 없는 문구는 대상을 끝내 못 구했을 때의 폴백이다 ──
		{ SpeechId.CmdFocusAttack,   Order(new[] { "{S를} 집중 공격하라!", "전원 {S를} 노려라!", "{S} 하나만 노려라! 집중 공격!", "한 놈에게 집중 공격!" }, SpeechTone.Alert, 2.4f, TacticalPriority, 5f, 1.5f) },
		{ SpeechId.CmdObjectFocus,   Order(new[] { "길을 막은 문을 부숴라!", "저 문부터 뚫는다! 전원 공격!" }, SpeechTone.Alert, 2.4f, TacticalPriority, 5f, 1.5f) }, // 대상이 유닛이 아니라 문이라 이름 토큰 없이 문을 가리킨다
		{ SpeechId.CmdStrike,        Order(new[] { "{S}에게 강공격!", "{S를} 일격에 쳐라!", "한 방에 끝내자! {S}에게 강공격!", "지금이다! 강하게 쳐라!" }, SpeechTone.Alert, 2.2f, TacticalPriority, 5f, 1.5f) },
		{ SpeechId.CmdSuppress,      Order(new[] { "{S를} 제압하라!", "{S}의 발을 묶어라!", "{S}부터 묶어라! 제압!", "놈을 묶어라! 제압해!" }, SpeechTone.Alert, 2.2f, TacticalPriority, 5f, 1.5f) },
		{ SpeechId.CmdProtect,       Order(new[] { "{S를} 보호하라!", "{S가} 위험하다! 지켜라!", "{S} 곁을 지켜라!", "동료가 위험하다! 지켜라!" }, SpeechTone.Alert, 2.2f, TacticalPriority, 5f, 1.5f) },
		{ SpeechId.CmdProtectSelf,   Order(new[] { "내가 위험하다! 나를 지켜라!", "나부터 지켜라! 위험하다!" }, SpeechTone.Alert, 2.2f, TacticalPriority, 5f, 1.5f) },
		{ SpeechId.CmdSuperiorFocus, Order(new[] { "전 부대, {S를} 집중 공격!", "상위 명령이다! 목표는 {S}!", "전 부대 일제 공격! {S}에게 집중!", "전 부대, 일제히 공격!" }, SpeechTone.Alert, 2.8f, TacticalPriority + 5, 5f, 1.5f) },
	};

	public static bool TryGet(SpeechId id, out SpeechEntry entry) => Entries.TryGetValue(id, out entry);
}
