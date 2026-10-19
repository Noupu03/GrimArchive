using UnityEngine;

// 말풍선 순수 계산 — 이름 가공·조사·문구 선택·덮어쓰기/쿨다운/페이드 판정(부수효과 없음, 테스트 용이).
public static class SpeechMath
{
	// "사제_7_5_0" → "사제". 유닛 이름은 "종류_번호…" 꼴이고, 말풍선에는 종류만 쓴다.
	public static string DisplayName(string unitName)
	{
		if (string.IsNullOrEmpty(unitName)) return unitName;
		int i = unitName.IndexOf('_');
		return i > 0 ? unitName.Substring(0, i) : unitName;
	}

	// 마지막 글자가 받침으로 끝나는 한글인가. 한글이 아니면(숫자·영문) 받침 없음으로 본다.
	public static bool HasBatchim(string s)
	{
		if (string.IsNullOrEmpty(s)) return false;
		char c = s[s.Length - 1];
		if (c < 0xAC00 || c > 0xD7A3) return false;
		return (c - 0xAC00) % 28 != 0;
	}

	// 이름 + 주격 조사("사제가", "아처형이").
	public static string WithGa(string name) => name + (HasBatchim(name) ? "이" : "가");

	// 이름 + 서술격 조사("근접 탱커다", "아처형이다").
	public static string WithIda(string name) => name + (HasBatchim(name) ? "이다" : "다");

	public static string Format(string line, string subject)
		=> line.Replace("{S가}", WithGa(subject ?? "")).Replace("{S이다}", WithIda(subject ?? "")).Replace("{S}", subject ?? "");

	// 후보 문구 중 하나를 고른다. 대상 이름이 없으면 {S…} 자리가 있는 문구는 후보에서 뺀다. random01은 [0,1). 쓸 수 있는 문구가 없으면 null.
	public static string PickLine(string[] lines, string subject, float random01)
	{
		if (lines == null || lines.Length == 0) return null;
		var usable = new System.Collections.Generic.List<string>(lines.Length);
		foreach (var l in lines)
			if (!string.IsNullOrEmpty(subject) || !l.Contains("{S")) usable.Add(l);
		if (usable.Count == 0) return null;
		int idx = Mathf.Min(usable.Count - 1, Mathf.FloorToInt(Mathf.Clamp01(random01) * usable.Count));
		return Format(usable[idx], subject);
	}

	// 긴 문구를 가운데에 가장 가까운 공백에서 두 줄로 나눈다 — 한글은 글자 폭이 넓어 한 줄이 너무 길어진다. maxChars 이하이거나 공백이 없으면 그대로.
	public static string Wrap(string text, int maxChars = 12)
	{
		if (string.IsNullOrEmpty(text) || text.Length <= maxChars) return text;
		int mid = text.Length / 2, best = -1;
		for (int i = 0; i < text.Length; i++)
			if (text[i] == ' ' && (best < 0 || Mathf.Abs(i - mid) < Mathf.Abs(best - mid))) best = i;
		return best < 0 ? text : text.Substring(0, best) + "\n" + text.Substring(best + 1);
	}

	// 이미 떠 있는 말풍선을 덮어쓸 수 있는가 — 떠 있지 않거나, 새 대사의 순위가 같거나 높을 때.
	public static bool CanReplace(bool showing, int currentPriority, int newPriority) => !showing || newPriority >= currentPriority;

	// lastTime이 없으면(한 번도 안 말함) 항상 통과.
	public static bool CooldownElapsed(float now, float? lastTime, float cooldown) => !lastTime.HasValue || now - lastTime.Value >= cooldown;

	// 끝나기 fadeSeconds 전부터 선형으로 투명해진다. 시간이 다 지났으면 0.
	public static float FadeAlpha(float elapsed, float duration, float fadeSeconds)
	{
		if (elapsed >= duration) return 0f;
		float remain = duration - elapsed;
		if (fadeSeconds <= 0f || remain >= fadeSeconds) return 1f;
		return remain / fadeSeconds;
	}
}
