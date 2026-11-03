using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Haare.Util.Logger;

// 리더 명령 콘텐츠 데이터 — 유닛 종류별 기본 통솔력·보유 명령, 스킬별 명령 연결(명령 + 순위). Assets/Data/leader_commands.json에서 로드한다(WeightEventTable과 같은 File.ReadAllText 방식).
// 문서(21·22장)가 이 값들을 후속 콘텐츠로 미뤘기 때문에 json의 모든 값은 자리표시자다 — 코드는 값을 모르고 이 표만 읽는다. 프리팹이 아니라 json에 둔 이유: 프리팹은 Unity 에디터 없이 못 고치고 컨버터 재실행이 보스 골렘 손 오브젝트를 지운다.
public static class LeaderCommandTable
{
	private const string JsonPath = "Assets/Data/leader_commands.json";

	public readonly struct SkillLink
	{
		public readonly TacticalCommand Command;
		public readonly int Rank; // 숫자가 작을수록 우선(9-표1)
		public SkillLink(TacticalCommand command, int rank) { Command = command; Rank = rank; }
	}

	private sealed class UnitEntry
	{
		public float Leadership;
		public float Grade;
		public HashSet<TacticalCommand> Commands = new HashSet<TacticalCommand>();
	}

#pragma warning disable CS0649 // JsonUtility가 리플렉션으로 채우는 DTO라 코드에서 할당하지 않는다
	[Serializable] private class JsonUnit { public string typeName; public float leadership; public float grade; public string[] commands; }
	[Serializable] private class JsonLink { public string command; public int rank; }
	[Serializable] private class JsonSkill { public string skillName; public JsonLink[] links; }
	[Serializable] private class JsonDatabase { public JsonUnit[] units; public JsonSkill[] skills; }
#pragma warning restore CS0649

	private static Dictionary<string, UnitEntry> _units;
	private static Dictionary<string, List<SkillLink>> _skills;

	private static void EnsureLoaded()
	{
		if (_units != null) return;
		if (!File.Exists(JsonPath))
		{
			_units = new Dictionary<string, UnitEntry>();
			_skills = new Dictionary<string, List<SkillLink>>();
			LogHelper.Error(LogHelper.GAME, $"[LeaderCommandTable] {JsonPath} 파일을 찾을 수 없습니다 — 모든 유닛의 기본 통솔력이 0이라 리더 명령이 동작하지 않습니다.");
			return;
		}
		LoadFromJson(File.ReadAllText(JsonPath));
	}

	// 테스트와 로드가 함께 쓴다. 파싱에 실패한 항목은 건너뛰고 경고만 남긴다.
	public static void LoadFromJson(string json)
	{
		_units = new Dictionary<string, UnitEntry>();
		_skills = new Dictionary<string, List<SkillLink>>();
		var db = JsonUtility.FromJson<JsonDatabase>(json);
		if (db == null) return;

		if (db.units != null)
		{
			foreach (var u in db.units)
			{
				if (u == null || string.IsNullOrEmpty(u.typeName)) continue;
				var entry = new UnitEntry { Leadership = Mathf.Clamp(u.leadership, 0f, 100f), Grade = u.grade };
				if (u.commands != null)
				{
					foreach (var name in u.commands)
					{
						if (Enum.TryParse(name, out TacticalCommand parsed)) entry.Commands.Add(parsed);
						else LogHelper.Warning(LogHelper.GAME, $"[LeaderCommandTable] '{u.typeName}'의 알 수 없는 명령 '{name}' — TacticalCommand에 없음.");
					}
				}
				_units[u.typeName] = entry;
			}
		}

		if (db.skills != null)
		{
			foreach (var s in db.skills)
			{
				if (s == null || string.IsNullOrEmpty(s.skillName) || s.links == null) continue;
				var list = new List<SkillLink>();
				foreach (var l in s.links)
				{
					if (l == null) continue;
					if (!Enum.TryParse(l.command, out TacticalCommand parsed) || !parsed.IsActionPriority())
					{
						LogHelper.Warning(LogHelper.GAME, $"[LeaderCommandTable] 스킬 '{s.skillName}'의 연결 명령 '{l.command}'은 강공격·제압·보호가 아니라 무시합니다.");
						continue;
					}
					list.Add(new SkillLink(parsed, Mathf.Max(1, l.rank)));
				}
				_skills[s.skillName] = list;
			}
		}
	}

	public static void ResetForTests() { _units = null; _skills = null; }

	// 유닛 종류별 기본 통솔력(0~100) — 표에 없으면 0(리더 자격 없음).
	public static float BaseLeadership(string typeName)
	{
		EnsureLoaded();
		return typeName != null && _units.TryGetValue(typeName, out var e) ? e.Leadership : 0f;
	}

	// 유닛 등급 — 리더 선정 동률 비교용(높을수록 우선). 등급 체계가 아직 없어 표의 값(자리표시자 0)을 그대로 쓴다.
	public static float Grade(string typeName)
	{
		EnsureLoaded();
		return typeName != null && _units.TryGetValue(typeName, out var e) ? e.Grade : 0f;
	}

	// 이 종류의 유닛이 리더일 때 보유한 명령인가(통솔력 조건과 별개 — 3-표1은 "명령을 실제로 보유하고 다른 조건도 충족해야 한다").
	public static bool Holds(string typeName, TacticalCommand command)
	{
		EnsureLoaded();
		return typeName != null && _units.TryGetValue(typeName, out var e) && e.Commands.Contains(command);
	}

	// 스킬이 해당 행동 우선 명령에 연결돼 있으면 그 순위를 돌려준다(9-표1 "대응 명령"·"명령 중 우선순위"). 미지정 후보는 그 명령의 후보에 포함하지 않는다.
	public static bool TryGetRank(string skillName, TacticalCommand command, out int rank)
	{
		EnsureLoaded();
		rank = 0;
		if (skillName == null || !_skills.TryGetValue(skillName, out var links)) return false;
		foreach (var l in links)
		{
			if (l.Command != command) continue;
			rank = l.Rank;
			return true;
		}
		return false;
	}
}
