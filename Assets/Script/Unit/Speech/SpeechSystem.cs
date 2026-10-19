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

	static readonly Dictionary<string, float> LastSaid = new Dictionary<string, float>();
	const int PruneThreshold = 256;
	const float PruneAgeSeconds = 120f;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetCache() => LastSaid.Clear();

	// 유닛 머리 위에 대사를 띄운다. subjectUnitName은 대사 속 대상(예: "무도가가 없는데?")의 유닛 이름이고, ignoreCooldown은 debug 확인용. 실제로 띄웠으면 true.
	public static bool Say(Unit unit, SpeechId id, string subjectUnitName = null, bool ignoreCooldown = false)
	{
		if (!Enabled || unit == null || unit.hp <= 0f) return false;
		var generate = unit.Session?.unitGenerate;
		if (generate == null || !SpeechCatalog.TryGet(id, out var entry)) return false;

		string subject = SpeechMath.DisplayName(subjectUnitName);
		string text = SpeechMath.PickLine(entry.Lines, subject, Random01());
		if (text == null) return false;

		float now = Now();
		if (!TryClaim(unit.name, id, subject, entry, now, ignoreCooldown)) return false;
		if (!generate.ShowSpeech(unit, text, entry.Tone, entry.Seconds, ignoreCooldown ? int.MaxValue : entry.Priority)) return false; // debug 강제 표시는 떠 있는 말풍선도 덮어쓴다
		Record(unit.name, id, subject, now);
		return true;
	}

	// 쿨다운(유닛별 + 전체)을 통과했는지만 본다 — 기록은 실제로 띄운 뒤 Record가 한다.
	internal static bool TryClaim(string unitName, SpeechId id, string subject, in SpeechEntry entry, float now, bool ignoreCooldown)
	{
		if (ignoreCooldown) return true;
		return SpeechMath.CooldownElapsed(now, Last(UnitKey(unitName, id, subject)), entry.UnitCooldown)
			&& SpeechMath.CooldownElapsed(now, Last(GlobalKey(id)), entry.GlobalCooldown);
	}

	internal static void Record(string unitName, SpeechId id, string subject, float now)
	{
		LastSaid[UnitKey(unitName, id, subject)] = now;
		LastSaid[GlobalKey(id)] = now;
		if (LastSaid.Count > PruneThreshold) Prune(now);
	}

	internal static void ClearHistory() => LastSaid.Clear();

	static string UnitKey(string unitName, SpeechId id, string subject) => unitName + "|" + id + "|" + subject;
	static string GlobalKey(SpeechId id) => "*|" + id;
	static float? Last(string key) => LastSaid.TryGetValue(key, out float t) ? t : (float?)null;

	static void Prune(float now)
	{
		var stale = new List<string>();
		foreach (var kv in LastSaid)
			if (now - kv.Value > PruneAgeSeconds) stale.Add(kv.Key);
		foreach (var k in stale) LastSaid.Remove(k);
	}
}
