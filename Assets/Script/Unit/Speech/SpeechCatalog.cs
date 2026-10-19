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
	};

	public static bool TryGet(SpeechId id, out SpeechEntry entry) => Entries.TryGetValue(id, out entry);
}
