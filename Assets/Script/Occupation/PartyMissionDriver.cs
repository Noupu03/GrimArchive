using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// 파티 하나의 "다음 방 이동" 구동 — 집결이 끝나면(Party.ReadyToAdvance) 목표 방으로 가는 다음 문을 골라 문 앞 진형 → 문 파괴 → 순차 입장 계획(PartyAdvanceSystem)을 시작하고, 계획을 매 프레임 진행시킨다.
// HumanWaveManager.UpdatePartyDestination의 비퇴각 부분과 같은 순서이지만 웨이브의 activeParty 하나에 묶여 있지 않아 점령 파티처럼 웨이브와 따로 상주하는 파티를 구동할 수 있다.
// (웨이브 파티 쪽은 동작을 바꾸지 않으려고 그대로 두었다 — 같은 로직이 두 곳에 있으니 한쪽을 고치면 다른 쪽도 확인할 것.) 코어 처리 경로·소탕 파티 문 앞 접근·공동 이동 폴백은 점령 파티에 필요 없어 뺐다.
public class PartyMissionDriver
{
	private const float LeaderRoomExploreGiveUpSeconds = 10f; // HumanWaveManager와 같은 값 — 방 안에서 문을 오래 못 찾으면 리더만 방 밖까지 탐색한다
	private static readonly HashSet<Human> NoExcluded = new HashSet<Human>();

	private bool _doorSearchLogged;
	private float _doorSearchSince = -1f;

	public void Tick(Party party, Room targetRoom)
	{
		if (party == null || party.Leader == null || party.Leader.hp <= 0f) return;
		if (party.Retreat.IsRetreating) return; // 후퇴 중에는 방 이동 결정·진형을 만들지 않는다 — 이동은 후퇴 시스템이 맡는다(후퇴도주 1-2장)

		party.TickAdvanceState();
		party.TickPendingRallyRelease();
		PartyAdvanceSystem.Tick(party); // 문 앞 진형 → 문 파괴 → 입장 단계 전이(계획이 없으면 즉시 반환)

		if (targetRoom == null || !party.ReadyToAdvance)
		{
			Reset(party);
			return;
		}

		if (!TryFindNextDoor(party, targetRoom, out Vector2Int doorPos, out int doorFloor))
		{
			if (!_doorSearchLogged)
			{
				_doorSearchLogged = true;
				LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] 진출 방 {targetRoom.RoomId}로 가는 문을 아직 모름 — 리더 {party.Leader.name}은(는) 탐색, 파티원은 리더 주변 유지");
			}
			if (_doorSearchSince < 0f) _doorSearchSince = Time.time;
			party.LeaderMayExploreBeyondRoom = Time.time - _doorSearchSince >= LeaderRoomExploreGiveUpSeconds;
			AssignDoorSearchFollowers(party);
			return;
		}

		Reset(party);
		if (PartyAdvanceSystem.Begin(party, doorPos, doorFloor, NoExcluded))
		{
			party.ReadyToAdvance = false; // 명령은 1회만 발행 — 이후 진행은 AdvancePlan이 맡는다
			LogHelper.Log(LogHelper.GAME, $"{PartyDiagnostics.TagOf(party)} [점령] 방 이동 시작 — 진출 방 {targetRoom.RoomId}, 문 ({doorPos.x},{doorPos.y})");
		}
	}

	private void Reset(Party party)
	{
		_doorSearchLogged = false;
		_doorSearchSince = -1f;
		if (party != null) party.LeaderMayExploreBeyondRoom = false;
	}

	// 리더가 아는 방 그래프로 먼저 고르고(LeaderRoutePlanner), 고를 게 없으면 현재 방의 아는 문 중 목표 방에 가까운 것을 고른다 — HumanWaveManager.TryFindNextDoorTowardTargetRoom과 같다.
	private static bool TryFindNextDoor(Party party, Room targetRoom, out Vector2Int doorPos, out int doorFloor)
	{
		if ((AIConfigLoader.Behavior?.leaderRoutePlannerEnabled ?? true)
			&& LeaderRoutePlanner.TryPickNextGate(party.Leader, targetRoom, out doorPos, out doorFloor))
			return true;
		return AIMovementHelper.TryFindKnownDoorInCurrentRoom(party.Leader, targetRoom.Bounds.center, out doorPos, out doorFloor);
	}

	// 다음 문을 모를 때 리더 외 적격 파티원이 리더 곁을 따라다니며 함께 문을 찾는다(05번 1장) — HumanWaveManager.AssignDoorSearchFollowers와 같다.
	private static void AssignDoorSearchFollowers(Party party)
	{
		var leader = party.Leader;
		float now = Time.time;
		foreach (var member in party.Members)
		{
			if (member == null || member == leader || member.hp <= 0 || member.currentFloor != leader.currentFloor) continue;
			if (member.isManualMoveCommand && member.playerMoveTarget.HasValue) continue;
			if (member.playerAttackTarget != null) continue;
			if (!PartyDoorSearchSystem.CanAssign(member, now)) continue;
			PartyDoorSearchSystem.Assign(member);
		}
	}
}
