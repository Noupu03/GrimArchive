using System.Collections.Generic;
using UnityEngine;

// 임무 수량 목표 — 발급 시 고정하고 진행 중 새 방·새 대상을 발견해도 늘리지 않는다. 0/false는 이 임무의 요구가 아니다.
public struct QuotaTargets
{
	public int Tiles;          // 이번 진입 이후 새로 본 지형 타일(파티 전체 중복 금지)
	public int Rooms;          // 방 탐색 B+E
	public int Kills;          // 종류 배율을 곱한 처치 합
	public int Recovers;       // 회수 완료(조사 완료와 구분)
	public bool CoreDestroyed;
	public bool Occupied;
	public bool BossDone;
}

public struct QuotaProgress
{
	public int Tiles;
	public int Rooms;
	public float Kills;
	public int Recovers;
	public bool CoreDestroyed;
	public bool Occupied;
	public bool BossDone;
}

public static class MissionQuotaMath
{
	// 임무가 요구하는 수량 종류: 탐색=타일+방, 회수=회수, 소탕=처치, 코어 파괴/점령/보스=완료 사건.
	public static QuotaTargets ForMission(PartyType mission, int tiles, int rooms, int kills, int recovers)
	{
		var t = new QuotaTargets();
		switch (mission)
		{
			case PartyType.Explore: t.Tiles = tiles; t.Rooms = rooms; break;
			case PartyType.Recover: t.Recovers = recovers; break;
			case PartyType.MopUp: t.Kills = kills; break;
			case PartyType.CoreDestroy: t.CoreDestroyed = true; break;
			case PartyType.Occupy: t.Occupied = true; break;
			case PartyType.Boss: t.BossDone = true; break;
		}
		return t;
	}

	// B = 발급 시점 미완료 일반 방 수(알려진 보스방 제외)를 상한으로 자른 값, E = 추가 방.
	public static int RoomRequirement(int unexploredNormalRooms, int baseCap, int extra)
		=> Mathf.Min(Mathf.Max(0, unexploredNormalRooms), Mathf.Max(0, baseCap)) + Mathf.Max(0, extra);

	public static bool HasAnyTarget(in QuotaTargets t)
		=> t.Tiles > 0 || t.Rooms > 0 || t.Kills > 0 || t.Recovers > 0 || t.CoreDestroyed || t.Occupied || t.BossDone;

	// 요구된 수량 전부 달성. anyOf는 계획 없는 파티의 옛 동작("방 또는 처치 중 하나")용이다. 요구가 없으면 달성할 수 없다.
	public static bool IsAchieved(in QuotaTargets t, in QuotaProgress p, bool anyOf)
	{
		int required = 0, met = 0;
		Tally(t.Tiles > 0, p.Tiles >= t.Tiles, ref required, ref met);
		Tally(t.Rooms > 0, p.Rooms >= t.Rooms, ref required, ref met);
		Tally(t.Kills > 0, p.Kills >= t.Kills, ref required, ref met);
		Tally(t.Recovers > 0, p.Recovers >= t.Recovers, ref required, ref met);
		Tally(t.CoreDestroyed, p.CoreDestroyed, ref required, ref met);
		Tally(t.Occupied, p.Occupied, ref required, ref met);
		Tally(t.BossDone, p.BossDone, ref required, ref met);

		if (required == 0) return false;
		return anyOf ? met > 0 : met == required;
	}

	static void Tally(bool isRequired, bool isMet, ref int required, ref int met)
	{
		if (!isRequired) return;
		required++;
		if (isMet) met++;
	}
}

// 파티 한 개의 임무 수량 집계. 처치는 웨이브 공통 집계(HumanWaveManager.CommonMonsterKillCount)를 읽고 목표만 파티별로 둔다.
public sealed class PartyMissionQuota
{
	public PartyType Mission { get; private set; } = PartyType.Explore;
	public QuotaTargets Targets { get; private set; }
	public bool AnyOf { get; private set; }

	readonly HashSet<Vector3Int> _tiles = new HashSet<Vector3Int>();
	readonly HashSet<string> _recoveredIds = new HashSet<string>();
	public int TilesRevealed => _tiles.Count;
	public int RoomsCompleted { get; private set; }
	public int Recovered => _recoveredIds.Count;
	public bool CoreDestroyed { get; private set; }
	public bool Occupied { get; private set; }
	public bool BossDone { get; private set; }

	// 처음 달성한 순간만 집결 판단에 반영한다 — 이후 수량이 늘어도 같은 집결을 새로 발령하지 않는다.
	bool _achieved;
	bool _consumed;

	// 계획 없이 만든 파티(디버그 스폰·수동 WaveData)는 옛 동작을 유지한다.
	public static PartyMissionQuota Legacy() => new PartyMissionQuota
	{
		Mission = PartyType.Explore,
		AnyOf = true,
		Targets = new QuotaTargets { Rooms = PartyGoalMath.RequiredRoomExploreCount, Kills = PartyGoalMath.RequiredMonsterKillCount },
	};

	public static PartyMissionQuota Create(PartyType mission, QuotaTargets targets)
		=> new PartyMissionQuota { Mission = mission, Targets = targets };

	// 목표만큼만 모은다 — 달성한 뒤에는 집합을 키우지 않는다.
	public bool CountTile(Vector3Int tile)
		=> tile.z >= 1 && _tiles.Count < Targets.Tiles && _tiles.Add(tile);

	public void OnRoomCompleted() => RoomsCompleted++;
	public bool OnRecovered(string objectId) => !string.IsNullOrEmpty(objectId) && _recoveredIds.Add(objectId);
	public void OnCoreDestroyed() => CoreDestroyed = true;
	public void OnOccupied() => Occupied = true;
	public void OnBossDone() => BossDone = true;

	QuotaProgress Snapshot(float commonKills) => new QuotaProgress
	{
		Tiles = _tiles.Count, Rooms = RoomsCompleted, Kills = commonKills, Recovers = _recoveredIds.Count,
		CoreDestroyed = CoreDestroyed, Occupied = Occupied, BossDone = BossDone,
	};

	// 한 번 달성하면 유지한다.
	public bool IsAchieved(float commonKills)
	{
		if (!_achieved && MissionQuotaMath.IsAchieved(Targets, Snapshot(commonKills), AnyOf)) _achieved = true;
		return _achieved;
	}

	public bool HasPendingAchievement(float commonKills) => IsAchieved(commonKills) && !_consumed;
	public void ConsumeAchievement() => _consumed = true;

	internal void ForceAchieved() { _achieved = true; _consumed = false; }
}
