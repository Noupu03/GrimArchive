using System;
using System.Collections.Generic;

namespace GrimArchive.Wave
{
	// 편성 입력 스냅샷. PartyPlanContextBuilder가 세션에서 모은다.
	public struct PlannerContext
	{
		public int NextWaveNumber;
		public float DungeonDanger;
		public bool HumanBaseExists;
		public bool CoreActivated;
		public int UnexploredNormalRooms; // 알려진 보스방 제외
		public bool LootExists;
		public bool EnemiesExist;
		public int GarrisonShortfall;
	}

	// 순수 계산 — 난수는 [0,1)을 돌려주는 함수로 주입한다.
	public static class PartyCompositionMath
	{
		public static int ResolveStage(IReadOnlyList<StageRule> stages, int waveNumber, float danger)
		{
			int stage = 1;
			if (stages == null) return stage;
			foreach (var s in stages)
				if (waveNumber >= s.MinWave && danger >= s.MinDanger && s.Stage > stage) stage = s.Stage;
			return stage;
		}

		public static bool IsMissionEligible(MissionRule rule, in PlannerContext ctx)
		{
			if (rule == null || !rule.Enabled || ctx.NextWaveNumber < rule.MinWave) return false;
			if (rule.RequiresActivatedCore && !ctx.CoreActivated) return false;
			if (rule.RequiresUnexploredRoom && ctx.UnexploredNormalRooms <= 0) return false;
			if (rule.RequiresLootExists && !ctx.LootExists) return false;
			if (rule.RequiresEnemiesExist && !ctx.EnemiesExist) return false;
			return true;
		}

		public static bool IndependentAllowed(ConceptRule c, int stage, in PlannerContext ctx)
			=> c != null && c.Enabled && c.Independent
				&& stage >= c.MinStage && stage <= c.MaxStage
				&& ctx.NextWaveNumber <= c.MaxWave
				&& (!c.OnlyWithoutHumanBase || !ctx.HumanBaseExists);

		// 가중치가 전부 0 이하면 균등 추첨. 후보가 없으면 default.
		public static T PickWeighted<T>(IReadOnlyList<T> items, Func<T, float> weightOf, Func<float> rand)
		{
			if (items == null || items.Count == 0) return default;
			float total = 0f;
			foreach (var it in items) total += Math.Max(0f, weightOf(it));
			if (total <= 0f) return items[Math.Min(items.Count - 1, (int)(rand() * items.Count))];

			float r = rand() * total;
			foreach (var it in items)
			{
				r -= Math.Max(0f, weightOf(it));
				if (r < 0f) return it;
			}
			return items[items.Count - 1];
		}

		// 해당 단계 이하 중 가장 높은 규모 항목에서 정수를 굴린다. 정의가 없으면 0.
		public static int RollSize(ConceptRule concept, int stage, Func<float> rand)
		{
			if (concept == null || concept.Sizes.Count == 0) return 0;
			SizeRange pick = concept.Sizes[0];
			foreach (var s in concept.Sizes)
				if (s.Stage <= stage) pick = s;
			int span = pick.Max - pick.Min + 1;
			return pick.Min + Math.Min(span - 1, (int)(rand() * span));
		}

		// 슬롯 최소 수를 먼저 채우고 남는 자리는 가중치 추첨(슬롯 상한 안에서). 필수 역할은 없다.
		public static List<UnitCount> BuildMembers(ConceptRule concept, int size, Func<float> rand)
		{
			var result = new List<UnitCount>();
			if (concept == null || concept.Slots.Count == 0 || size <= 0) return result;

			int n = concept.Slots.Count;
			var counts = new int[n];
			int remaining = size;
			for (int i = 0; i < n && remaining > 0; i++)
			{
				counts[i] = Math.Min(concept.Slots[i].Min, remaining);
				remaining -= counts[i];
			}

			var open = new List<int>(n);
			while (remaining > 0)
			{
				open.Clear();
				for (int i = 0; i < n; i++)
					if (counts[i] < concept.Slots[i].Max) open.Add(i);
				if (open.Count == 0) break; // 슬롯 상한 합이 규모보다 작으면 규모가 줄어든다
				counts[PickWeighted(open, idx => concept.Slots[idx].Weight, rand)]++;
				remaining--;
			}

			for (int i = 0; i < n; i++)
				if (counts[i] > 0) result.Add(new UnitCount(concept.Slots[i].UnitType, counts[i]));
			return result;
		}

		// 생존자가 같은 종류 슬롯부터 채우고, 계획에 없던 종류도 가장 많이 남은 슬롯 하나씩을 줄여 규모를 넘지 않게 한다.
		// 반환값은 새로 스폰할 인원, surplusSurvivors는 규모를 넘겨 합류하는 생존자 수(버리지 않는다).
		public static List<UnitCount> ApplySurvivors(IReadOnlyList<UnitCount> planned, IReadOnlyList<string> survivorTypes, out int surplusSurvivors)
		{
			var types = new List<string>(planned.Count);
			var need = new List<int>(planned.Count);
			foreach (var m in planned) { types.Add(m.UnitType); need.Add(m.Count); }

			int unmatched = 0;
			if (survivorTypes != null)
			{
				foreach (var t in survivorTypes)
				{
					int idx = types.IndexOf(t);
					if (idx >= 0 && need[idx] > 0) need[idx]--;
					else unmatched++;
				}
			}

			while (unmatched > 0)
			{
				int best = -1;
				for (int i = 0; i < need.Count; i++)
					if (need[i] > 0 && (best < 0 || need[i] > need[best])) best = i;
				if (best < 0) break;
				need[best]--;
				unmatched--;
			}
			surplusSurvivors = unmatched;

			var spawn = new List<UnitCount>();
			for (int i = 0; i < types.Count; i++)
				if (need[i] > 0) spawn.Add(new UnitCount(types[i], need[i]));
			return spawn;
		}

		public static int Total(IReadOnlyList<UnitCount> list)
		{
			int n = 0;
			foreach (var m in list) n += m.Count;
			return n;
		}
	}
}
