using System;
using System.Collections.Generic;
using UnityEngine;

// 유닛 머리 위 대사 연출의 단일 진입점 — 행동 시스템은 SpeechSystem.Say(유닛, 상황)만 부른다. 문구·표시 시간·순위·쿨다운은 SpeechCatalog, 표시는 UnitVisual → UnitSpeechBubble이 맡는다.
// 연출 전용이다: 게임 상태를 바꾸지 않고(전파·사망·도주 정보가 아님, 후퇴도주 명세서 4-3·7-3장), 비주얼이 없거나 세션이 없으면(테스트 등) 조용히 false를 돌려준다.
public static class SpeechSystem
{
	// debug 메뉴 토글 — 끄면 모든 말풍선을 띄우지 않는다.
	public static bool Enabled = true;

	// [0,1) 난수·현재 시각 — 테스트에서 교체한다.
	public static Func<float> Random01 = () => UnityEngine.Random.value;
	public static Func<float> Now = () => Time.time;

	// 키는 (유닛 이름, 대사, 대상 이름) — 전체 쿨다운은 유닛·대상이 null인 키. 튜플이라 틱마다 불려도 문자열 할당이 없다.
	static readonly Dictionary<(string unit, SpeechId id, string subject), float> LastSaid = new Dictionary<(string, SpeechId, string), float>();
	const int PruneThreshold = 256;
	const float PruneAgeSeconds = 120f;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetCache() => LastSaid.Clear();

	// 유닛 머리 위에 대사를 띄운다. subjectUnitName은 대사 속 대상(예: "무도가가 없는데?")의 유닛 이름이고, ignoreCooldown은 debug 확인용. 실제로 띄웠으면 true.
	public static bool Say(Unit unit, SpeechId id, string subjectUnitName = null, bool ignoreCooldown = false)
	{
		if (!Enabled || !(unit is Human) || unit.hp <= 0f) return false; // 몬스터 대사는 아직 쓰지 않았다 — 필요해지면 SpeechEntry에 대상 진영을 추가한다
		var generate = unit.Session?.unitGenerate;
		if (generate == null || !SpeechCatalog.TryGet(id, out var entry)) return false;

		// 틱마다 불리는 호출부가 있어(도주 중 숨소리·적 인지 갱신) 쿨다운을 먼저 본다 — 문구 선택·말풍선 갱신은 실제로 말할 때만 한다.
		string subject = SpeechMath.DisplayName(subjectUnitName);
		float now = Now();
		if (!TryClaim(unit.name, id, subject, entry, now, ignoreCooldown)) return false;
		string text = SpeechMath.PickLine(entry.Lines, subject, Random01());
		if (text == null) return false;
		if (!generate.ShowSpeech(unit, text, entry.Tone, entry.Seconds, ignoreCooldown ? int.MaxValue : entry.Priority)) // debug 강제 표시는 떠 있는 말풍선도 덮어쓴다
		{
			// 더 높은 순위의 말풍선이 떠 있어 못 띄웠다 — 이 사건은 놓친 것으로 본다. 소비하지 않으면 틱마다 부르는 호출부가 말풍선이 빌 때까지 재시도하다 한참 뒤 낡은 대사를 뒤늦게 띄운다.
			if (!ignoreCooldown) Suppress(unit.name, id, subject, now);
			return false;
		}
		Record(unit.name, id, subject, now);
		return true;
	}

	// 쿨다운(유닛별 + 전체)을 통과했는지 본다 — 말한 기록은 실제로 띄운 뒤 Record가 한다. 유닛 쿨다운은 통과했는데 전체 쿨다운에 막혔다면 이 사건은 놓친 것으로 보고 유닛 쿨다운을 소비한다(Suppress):
	// 막힌 유닛이 틱마다 재시도하다가 전체 쿨다운이 풀리는 순간 차례로 말하면, 합창을 막는 쿨다운이 오히려 합창을 시간차로 줄 세워 "말풍선이 밀려서 뜨는" 연쇄가 된다.
	internal static bool TryClaim(string unitName, SpeechId id, string subject, in SpeechEntry entry, float now, bool ignoreCooldown)
	{
		if (ignoreCooldown) return true;
		if (!SpeechMath.CooldownElapsed(now, Last(UnitKey(unitName, id, subject)), entry.UnitCooldown)) return false;
		if (!SpeechMath.CooldownElapsed(now, Last(GlobalKey(id)), entry.GlobalCooldown))
		{
			Suppress(unitName, id, subject, now);
			return false;
		}
		return true;
	}

	// 이번 사건에서 말할 기회를 놓친 유닛의 기록 — 유닛 쿨다운만 소비하고 전체 쿨다운은 건드리지 않는다.
	internal static void Suppress(string unitName, SpeechId id, string subject, float now)
	{
		LastSaid[UnitKey(unitName, id, subject)] = now;
		if (LastSaid.Count > PruneThreshold) Prune(now);
	}

	internal static void Record(string unitName, SpeechId id, string subject, float now)
	{
		LastSaid[UnitKey(unitName, id, subject)] = now;
		LastSaid[GlobalKey(id)] = now;
		if (LastSaid.Count > PruneThreshold) Prune(now);
	}

	internal static void ClearHistory() => LastSaid.Clear();

	static (string, SpeechId, string) UnitKey(string unitName, SpeechId id, string subject) => (unitName, id, subject);
	static (string, SpeechId, string) GlobalKey(SpeechId id) => (null, id, null);
	static float? Last((string, SpeechId, string) key) => LastSaid.TryGetValue(key, out float t) ? t : (float?)null;

	static void Prune(float now)
	{
		var stale = new List<(string unit, SpeechId id, string subject)>();
		foreach (var kv in LastSaid)
			if (now - kv.Value > PruneAgeSeconds) stale.Add(kv.Key);
		foreach (var k in stale) LastSaid.Remove(k);
	}
}
