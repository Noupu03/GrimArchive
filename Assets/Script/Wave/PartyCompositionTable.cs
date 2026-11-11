using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Haare.Util.Logger;

namespace GrimArchive.Wave
{
	public sealed class StageRule
	{
		public int Stage;
		public int MinWave;
		public float MinDanger;
	}

	public sealed class CompositionSlot
	{
		public string UnitType;
		public int Min;
		public int Max;
		public float Weight;
	}

	public sealed class SizeRange
	{
		public int Stage;
		public int Min;
		public int Max;
	}

	public sealed class ConceptRule
	{
		public PartyConcept Concept;
		public bool Enabled;
		public float Weight = 1f;
		public int MinStage = 1;
		public int MaxStage = 99;
		// 리더 없는 초기 독립 유닛 — 인류 거점이 없을 때만, MaxWave 이전에만, Chance 확률로 등장한다.
		public bool Independent;
		public bool OnlyWithoutHumanBase;
		public int MaxWave = 99;
		public float Chance = 1f;
		public readonly List<SizeRange> Sizes = new List<SizeRange>();
		public readonly List<CompositionSlot> Slots = new List<CompositionSlot>();
	}

	public sealed class MissionRule
	{
		public PartyType Mission;
		public bool Enabled;
		public float Weight = 1f;
		public int MinWave = 1;
		public bool RequiresActivatedCore;
		public bool RequiresUnexploredRoom;
		public bool RequiresLootExists;
		public bool RequiresEnemiesExist;
		public readonly HashSet<PartyConcept> Concepts = new HashSet<PartyConcept>();
		public int Tiles;
		public int RoomBaseCap;
		public int RoomExtra;
		public int Kills;
		public int Recovers;
	}

	// 컨셉×단계 규모·슬롯, 임무 규칙, 단계 — Assets/Data/party_composition.json에서 읽는다. 문서가 후속 콘텐츠로 미룬 값이라 json의 수치는 전부 자리표시자다.
	public static class PartyCompositionTable
	{
		const string JsonPath = "Assets/Data/party_composition.json";

#pragma warning disable CS0649 // JsonUtility가 리플렉션으로 채운다
		[Serializable] class JsonStage { public int stage; public int minWave; public float minDanger; }
		[Serializable] class JsonSlot { public string unitType; public int min; public int max; public float weight; }
		[Serializable] class JsonSize { public int stage; public int min; public int max; }
		[Serializable] class JsonConcept
		{
			public string concept; public bool enabled; public float weight; public int minStage; public int maxStage;
			public bool independent; public bool onlyWithoutHumanBase; public int maxWave; public float chance;
			public JsonSize[] sizes; public JsonSlot[] slots;
		}
		[Serializable] class JsonMission
		{
			public string mission; public bool enabled; public float weight; public int minWave;
			public bool requiresActivatedCore; public bool requiresUnexploredRoom; public bool requiresLootExists; public bool requiresEnemiesExist;
			public string[] concepts; public int tiles; public int roomBaseCap; public int roomExtra; public int kills; public int recovers;
		}
		[Serializable] class JsonDatabase { public JsonStage[] stages; public JsonConcept[] concepts; public JsonMission[] missions; }
#pragma warning restore CS0649

		public static IReadOnlyList<StageRule> Stages { get { EnsureLoaded(); return _stages; } }
		public static IReadOnlyList<ConceptRule> Concepts { get { EnsureLoaded(); return _concepts; } }
		public static IReadOnlyList<MissionRule> Missions { get { EnsureLoaded(); return _missions; } }

		static List<StageRule> _stages;
		static List<ConceptRule> _concepts;
		static List<MissionRule> _missions;

		static void EnsureLoaded()
		{
			if (_concepts != null) return;
			if (!File.Exists(JsonPath))
			{
				LoadFromJson("{}");
				LogHelper.Error(LogHelper.GAME, $"[PartyCompositionTable] {JsonPath}를 찾을 수 없어 편성 계획을 만들 수 없습니다 — WaveData.parties 수동 목록으로 폴백합니다.");
				return;
			}
			LoadFromJson(File.ReadAllText(JsonPath));
		}

		// 테스트와 로드가 함께 쓴다. 파싱에 실패한 항목은 건너뛰고 경고만 남긴다.
		public static void LoadFromJson(string json)
		{
			_stages = new List<StageRule>();
			_concepts = new List<ConceptRule>();
			_missions = new List<MissionRule>();
			var db = JsonUtility.FromJson<JsonDatabase>(json);
			if (db == null) return;

			if (db.stages != null)
				foreach (var s in db.stages)
					if (s != null) _stages.Add(new StageRule { Stage = Mathf.Max(1, s.stage), MinWave = s.minWave, MinDanger = s.minDanger });
			_stages.Sort((a, b) => a.Stage.CompareTo(b.Stage));

			if (db.concepts != null)
				foreach (var c in db.concepts)
					if (c != null && TryParseConcept(c, out ConceptRule rule)) _concepts.Add(rule);

			if (db.missions != null)
				foreach (var m in db.missions)
					if (m != null && TryParseMission(m, out MissionRule rule)) _missions.Add(rule);
		}

		static bool TryParseConcept(JsonConcept c, out ConceptRule rule)
		{
			rule = null;
			if (!Enum.TryParse(c.concept, out PartyConcept parsed))
			{
				LogHelper.Warning(LogHelper.GAME, $"[PartyCompositionTable] 알 수 없는 컨셉 '{c.concept}'");
				return false;
			}
			rule = new ConceptRule
			{
				Concept = parsed, Enabled = c.enabled, Weight = c.weight > 0f ? c.weight : 1f,
				MinStage = c.minStage > 0 ? c.minStage : 1, MaxStage = c.maxStage > 0 ? c.maxStage : 99,
				Independent = c.independent, OnlyWithoutHumanBase = c.onlyWithoutHumanBase,
				MaxWave = c.maxWave > 0 ? c.maxWave : 99, Chance = c.chance > 0f ? Mathf.Clamp01(c.chance) : 1f,
			};
			if (c.sizes != null)
				foreach (var s in c.sizes)
					if (s != null) rule.Sizes.Add(new SizeRange { Stage = Mathf.Max(1, s.stage), Min = Mathf.Max(1, s.min), Max = Mathf.Max(Mathf.Max(1, s.min), s.max) });
			rule.Sizes.Sort((a, b) => a.Stage.CompareTo(b.Stage));
			if (c.slots != null)
				foreach (var s in c.slots)
					if (s != null && !string.IsNullOrEmpty(s.unitType))
						rule.Slots.Add(new CompositionSlot { UnitType = s.unitType, Min = Mathf.Max(0, s.min), Max = Mathf.Max(Mathf.Max(0, s.min), s.max), Weight = s.weight > 0f ? s.weight : 1f });
			return true;
		}

		static bool TryParseMission(JsonMission m, out MissionRule rule)
		{
			rule = null;
			if (!Enum.TryParse(m.mission, out PartyType parsed))
			{
				LogHelper.Warning(LogHelper.GAME, $"[PartyCompositionTable] 알 수 없는 임무 '{m.mission}'");
				return false;
			}
			rule = new MissionRule
			{
				Mission = parsed, Enabled = m.enabled, Weight = m.weight > 0f ? m.weight : 1f, MinWave = Mathf.Max(1, m.minWave),
				RequiresActivatedCore = m.requiresActivatedCore, RequiresUnexploredRoom = m.requiresUnexploredRoom,
				RequiresLootExists = m.requiresLootExists, RequiresEnemiesExist = m.requiresEnemiesExist,
				Tiles = m.tiles, RoomBaseCap = m.roomBaseCap, RoomExtra = m.roomExtra, Kills = m.kills, Recovers = m.recovers,
			};
			if (m.concepts == null) return true;
			foreach (var name in m.concepts)
			{
				if (Enum.TryParse(name, out PartyConcept c)) rule.Concepts.Add(c);
				else LogHelper.Warning(LogHelper.GAME, $"[PartyCompositionTable] 임무 '{m.mission}'의 알 수 없는 컨셉 '{name}'");
			}
			return true;
		}

		public static void ResetForTests() { _stages = null; _concepts = null; _missions = null; }
	}
}
