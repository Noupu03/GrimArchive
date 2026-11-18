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
		// 0보다 크면 웨이브 번호와 무관하게 던전 위험도가 이 값 이상일 때도 이 단계에 오른다 — "인류가 던전을 위험하다고 배워 더 강하게 온다"(체험판 난이도 곡선 C안, 2026-11-18).
		public float DangerAlone;
	}

	// 플레이어 병력 규모에 비례한 파티 추가 인원(체험판 난이도 곡선 C안, 2026-11-18). 독립 유닛(부랑자)에는 적용하지 않는다.
	public sealed class ArmyScaleRule
	{
		public bool Enabled;
		public int FreeAllies;   // 이 수까지의 아군은 추가 인원을 만들지 않는다
		public float PerAlly;    // 그 초과분 1기당 추가 인원
		public int MaxBonus;     // 추가 인원 상한
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
		[Serializable] class JsonStage { public int stage; public int minWave; public float minDanger; public float dangerAlone; }
		[Serializable] class JsonArmyScale { public bool enabled; public int freeAllies; public float perAlly; public int maxBonus; }
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
		[Serializable] class JsonDatabase { public JsonStage[] stages; public JsonConcept[] concepts; public JsonMission[] missions; public JsonArmyScale armyScale; }
#pragma warning restore CS0649

		public static IReadOnlyList<StageRule> Stages { get { EnsureLoaded(); return _stages; } }
		public static IReadOnlyList<ConceptRule> Concepts { get { EnsureLoaded(); return _concepts; } }
		public static IReadOnlyList<MissionRule> Missions { get { EnsureLoaded(); return _missions; } }
		public static ArmyScaleRule ArmyScale { get { EnsureLoaded(); return _armyScale; } }

		static ArmyScaleRule _armyScale = new ArmyScaleRule();
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
					if (s != null) _stages.Add(new StageRule { Stage = Mathf.Max(1, s.stage), MinWave = s.minWave, MinDanger = s.minDanger, DangerAlone = s.dangerAlone });
			_stages.Sort((a, b) => a.Stage.CompareTo(b.Stage));

			_armyScale = db.armyScale == null ? new ArmyScaleRule() : new ArmyScaleRule
			{
				Enabled = db.armyScale.enabled,
				FreeAllies = Mathf.Max(0, db.armyScale.freeAllies),
				PerAlly = Mathf.Max(0f, db.armyScale.perAlly),
				MaxBonus = Mathf.Max(0, db.armyScale.maxBonus),
			};

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

		public static void ResetForTests() { _stages = null; _concepts = null; _missions = null; _armyScale = new ArmyScaleRule(); }
	}
}
