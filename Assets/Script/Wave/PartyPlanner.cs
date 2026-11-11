using System;
using System.Collections.Generic;
using UnityEngine;

namespace GrimArchive.Wave
{
	// 웨이브 편성 계획: 임무 후보 구성 → 가중치 추첨(사건이 지정하면 그 결과) → 임무를 수행할 수 있는 컨셉·규모·수량.
	// 표와 컨텍스트만 읽는 순수 계산이다(세션 조회는 PartyPlanContextBuilder).
	public static class PartyPlanner
	{
		// 계획을 만들 수 없으면(표가 비었거나 컨셉이 없음) null — 호출부가 WaveData.parties 수동 목록으로 폴백한다.
		public static WavePlan BuildPlan(in PlannerContext ctx, PartyType? forcedMission, Func<float> rand)
		{
			var plan = new WavePlan { WaveNumber = ctx.NextWaveNumber };
			plan.Stage = PartyCompositionMath.ResolveStage(PartyCompositionTable.Stages, ctx.NextWaveNumber, ctx.DungeonDanger);

			PartyPlan party = forcedMission.HasValue ? null : TryBuildIndependent(ctx, plan.Stage, rand);
			party ??= BuildMissionParty(ctx, plan.Stage, forcedMission, rand);
			if (party == null) return null;

			party.ReinforcementCount = Mathf.Max(0, ctx.GarrisonShortfall);
			plan.Parties.Add(party);
			return plan;
		}

		static PartyPlan TryBuildIndependent(in PlannerContext ctx, int stage, Func<float> rand)
		{
			foreach (var c in PartyCompositionTable.Concepts)
			{
				if (!PartyCompositionMath.IndependentAllowed(c, stage, ctx) || rand() >= c.Chance) continue;
				var party = BuildMembers(c, stage, rand);
				if (party == null) continue;

				party.IsIndependent = true;
				party.Mission = PartyType.Explore; // 임무 수량을 쓰지 않는 기본값
				party.Name = $"{c.Concept.ToKorean()} 무리";
				return party;
			}
			return null;
		}

		static PartyPlan BuildMissionParty(in PlannerContext ctx, int stage, PartyType? forcedMission, Func<float> rand)
		{
			MissionRule mission = forcedMission.HasValue ? FindOrDefaultRule(forcedMission.Value) : PickMission(ctx, rand);
			if (mission == null) return null;

			var concepts = new List<ConceptRule>();
			foreach (var c in PartyCompositionTable.Concepts)
			{
				if (!c.Enabled || c.Independent || c.Slots.Count == 0) continue;
				if (stage < c.MinStage || stage > c.MaxStage) continue;
				if (mission.Concepts.Count > 0 && !mission.Concepts.Contains(c.Concept)) continue;
				concepts.Add(c);
			}
			var concept = PartyCompositionMath.PickWeighted(concepts, c => c.Weight, rand);
			if (concept == null) return null;

			var party = BuildMembers(concept, stage, rand);
			if (party == null) return null;

			int rooms = MissionQuotaMath.RoomRequirement(ctx.UnexploredNormalRooms, mission.RoomBaseCap, mission.RoomExtra);
			party.Mission = mission.Mission;
			party.Name = $"{concept.Concept.ToKorean()} {mission.Mission.ToKorean()} 파티";
			party.Quota = MissionQuotaMath.ForMission(mission.Mission, mission.Tiles, rooms, mission.Kills, mission.Recovers);
			return party;
		}

		// 사건이 임무를 지정하면 후보 자격과 무관하게 그 결과를 쓴다. 표에 없는 임무는 기본값 규칙으로 만든다.
		static MissionRule FindOrDefaultRule(PartyType type)
		{
			foreach (var m in PartyCompositionTable.Missions)
				if (m.Mission == type) return m;
			return new MissionRule { Mission = type, Enabled = true };
		}

		static MissionRule PickMission(in PlannerContext ctx, Func<float> rand)
		{
			var candidates = new List<MissionRule>();
			foreach (var m in PartyCompositionTable.Missions)
				if (PartyCompositionMath.IsMissionEligible(m, ctx)) candidates.Add(m);
			return PartyCompositionMath.PickWeighted(candidates, m => m.Weight, rand);
		}

		static PartyPlan BuildMembers(ConceptRule concept, int stage, Func<float> rand)
		{
			var members = PartyCompositionMath.BuildMembers(concept, PartyCompositionMath.RollSize(concept, stage, rand), rand);
			if (members.Count == 0) return null;

			var party = new PartyPlan { Concept = concept.Concept, Stage = stage };
			party.Members.AddRange(members);
			party.TargetSize = party.MemberTotal;
			return party;
		}
	}

	// 세션에서 계획 입력을 모은다 — 조회만 하고 상태를 바꾸지 않는다.
	public static class PartyPlanContextBuilder
	{
		public static PlannerContext Build(GameSession session, int nextWaveNumber, int targetFloor, float dungeonDanger, IReadOnlyList<Human> survivors)
		{
			var ctx = new PlannerContext { NextWaveNumber = nextWaveNumber, DungeonDanger = dungeonDanger };
			if (session == null) return ctx;

			ctx.CoreActivated = session.Core != null && session.Core.IsFloorCoreEverActivated(targetFloor);
			CountRooms(session, targetFloor, survivors, ref ctx);
			ctx.LootExists = AnyUncollectedLoot(session, targetFloor);
			ctx.EnemiesExist = AnyLivingNonHuman(session, targetFloor);
			ctx.GarrisonShortfall = session.Occupation != null ? session.Occupation.GarrisonShortfall : 0;
			return ctx;
		}

		static void CountRooms(GameSession session, int floor, IReadOnlyList<Human> survivors, ref PlannerContext ctx)
		{
			if (session.allRooms == null) return;
			foreach (var room in session.allRooms)
			{
				if (room == null) continue;
				if (room.BaseStage != BaseStage.None) ctx.HumanBaseExists = true;
				if (room.Floor != floor || room.CoreObjectId != null) continue; // 알려진 보스방은 B에 넣지 않는다
				if (!IsExploredBySurvivor(room.RoomId, survivors)) ctx.UnexploredNormalRooms++;
			}
		}

		static bool AnyUncollectedLoot(GameSession session, int floor)
		{
			if (session.objectGrid == null) return false;
			foreach (var obj in session.objectGrid.Values)
				if (obj != null && !obj.IsCollected && obj.Position.z == floor && obj.Tags != null && obj.Tags.Exists(t => t.Contains("Loot"))) return true;
			return false;
		}

		static bool AnyLivingNonHuman(GameSession session, int floor)
		{
			if (session.units == null) return false;
			foreach (var u in session.units)
				if (u != null && !(u is Human) && u.hp > 0f && u.currentFloor == floor) return true;
			return false;
		}

		static bool IsExploredBySurvivor(int roomId, IReadOnlyList<Human> survivors)
		{
			if (survivors == null) return false;
			foreach (var s in survivors)
				if (s != null && s.hp > 0f && s.personalMap != null && s.personalMap.IsRoomExploreComplete(roomId)) return true;
			return false;
		}
	}
}
