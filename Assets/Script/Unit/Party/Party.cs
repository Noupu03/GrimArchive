using System.Collections.Generic;
using UnityEngine;
using GrimArchive.Wave;
using Haare.Util.Logger;

// 파티 — 인류 유닛들이 함께 웨이브(던전)에 입장하는 단위. 생존자 전역 반영/파티 전멸/정보 오차
// 공유 등 "파티"를 전제로 하는 가중치 로직의 실체. GameSession.CreateParty()로 생성·등록된다.
public partial class Party
{
	public string Id;
	public string Name;
	// 01번 문서 7-1장: 파티 종류별 방 활동 종료·집결 기준에 쓰인다. GameSession.CreateParty가 생성
	// 시점에 4종(PartyEnums.cs 참고) 중 하나를 무작위 배정한다(편성 가능 유닛·선택 가중치는 스텁).
	public PartyType Type;
	public readonly List<Human> Members = new List<Human>();

	// 리더·명령 체계 문서가 아직 없어 임시로 추가한 최소 리더 개념 — "전투 종료 후 경계 10초 → 리더가
	// 집결 명령 생성 → 파티원이 집결"이 요구하는 최소 기능만 채운다. 생존 파티원 중 leadership 최댓값을
	// 리더로 삼고, 리더가 죽으면 GameSession이 재선정한다.
	public Human Leader;

	// 01번 7-1장: 소탕 파티는 "다음 이동 문까지 이동하며 확인한 범위에서" 적이 없을 때 문 주변 집결을 판단한다 — 리더가 그 문 앞 접근을 마친 방.
	// IsRoomActivityComplete의 MopUp 분기가 이 표식을 요구하고, 집결 시작(소비)·리더가 방을 떠남(TickAdvanceState)에서 지운다.
	public Room DoorApproachRoom { get; private set; }

	public void MarkLeaderReachedNextDoor()
	{
		if (Leader != null) DoorApproachRoom = Leader.currentRoom;
	}

	// 03번 문서 3번 항목: 리더가 아는 아직 처리 안 된(적대적인) 코어 위치. 이게 있으면 집결·다음 방
	// 이동보다 코어 처리가 우선한다 — PartyCoreReportSystem.OnCoreDiscovered가 발견 시점에 채우고,
	// TryStartRally가 매번 IsRoomCoreStillHostile로 재확인해 처리 완료를 스스로 감지·해제한다.
	public Vector3Int? LeaderKnownCorePosition;

	// 01번 문서 2장: 방 탐색 임무 요구 수량(B+E, PartyGoalMath.RequiredRoomExploreCount) 집계 — 여러
	// 유닛이 각자 다른 방을 끝내도 중복 없이 한 번만 세기 위한 dedup. 진짜 "여러 관찰자가 나눠 본
	// 시야를 합쳐 완료 판정"은 안 함 — 누구든 먼저 그 방을 개인적으로 끝내면 그걸로 집계하는 근사
	// (IsRoomActivityComplete의 "리더 개인 지도 기준 근사"와 같은 성격).
	public int CompletedRoomExploreCount { get; private set; }
	private readonly HashSet<int> _countedRoomIds = new HashSet<int>();
	// 미달성→달성으로 막 바뀐 순간에만 true — Party.ReadyToAdvance와 동일한 관례로, 소비자(01-07/
	// 01-11의 리더 판단, 아직 미착수)가 읽고 스스로 false로 되돌린다.
	public bool JustReachedRoomExploreQuota;

	// 개인의 RoomExploreState가 Complete로 바뀌는 순간 PersonalMapKnowledge.ObserveRoomTileRevealed가
	// 호출한다.
	public void OnRoomExploreCompleted(int roomId)
	{
		if (!_countedRoomIds.Add(roomId)) return; // 이미 집계된 방 — 중복 방지

		bool wasBelow = CompletedRoomExploreCount < PartyGoalMath.RequiredRoomExploreCount;
		CompletedRoomExploreCount++;
		if (wasBelow && CompletedRoomExploreCount >= PartyGoalMath.RequiredRoomExploreCount)
			JustReachedRoomExploreQuota = true;
	}

	// 01번 문서 7-2장: 파티원 전체가 나눠 본 시야를 합쳐 방 지형 확인 완료를 판단하는 집계 —
	// 방(roomId)별로 새로 밝힌 바닥 타일을 모아 그 방의 전체 바닥 타일 수에 도달하면 "파티 기준
	// 완료"로 표시한다. 개인별 RevealedFloorTiles와는 별개 집계(리더가 못 본 타일도 반영됨).
	private readonly Dictionary<int, HashSet<Vector2Int>> _partyRevealedTilesByRoom = new();
	private readonly HashSet<int> _partyCompletedRoomIds = new();

	public bool IsRoomFullyRevealedByParty(int roomId) => _partyCompletedRoomIds.Contains(roomId);

	// UnitFunction.cs/TerrainRevealHandler.cs가 개인 시야로 새 바닥 타일을 밝힐 때마다(ObserveRoomTileRevealed와
	// 같은 시점) 호출한다.
	public void OnTileRevealedInRoom(int roomId, Vector2Int tilePos, int totalFloorTilesInRoom)
	{
		if (_partyCompletedRoomIds.Contains(roomId)) return; // 이미 완료 — 더 쌓을 필요 없음

		if (!_partyRevealedTilesByRoom.TryGetValue(roomId, out var tiles))
		{
			tiles = new HashSet<Vector2Int>();
			_partyRevealedTilesByRoom[roomId] = tiles;
		}
		tiles.Add(tilePos);

		if (totalFloorTilesInRoom > 0 && tiles.Count >= totalFloorTilesInRoom)
			_partyCompletedRoomIds.Add(roomId);
	}

	// 01번 문서 7-1장: 파티 종류별 "현재 방 활동 종료" 기준. Explore 분기는 파티 전체 시야 합산
	// (IsRoomFullyRevealedByParty)을 우선 확인하고 안 되면 리더 개인 지도 근사로 보조한다(둘 중
	// 하나만 참이어도 완료). 점령 파티는 방 점령 완료 기준 — 보스공략은 범위 밖(구현현황 문서 참고).
	public bool IsRoomActivityComplete()
	{
		if (Leader == null || Leader.hp <= 0 || Leader.currentRoom == null) return false;
		Room room = Leader.currentRoom;
		switch (Type)
		{
			case PartyType.Explore:
				return IsRoomFullyRevealedByParty(room.RoomId) ||
					!Leader.personalMap.HasFrontierTileInBounds(Leader.currentFloor, room.Bounds);
			case PartyType.Recover:
				return !HasKnownRecoverableInRoom(room);
			case PartyType.Occupy:
				// 트리거 조건은 미정(PartyEnums.cs 참고) — 판정 기준만 세팅으로 남겨둔다. RoomFaction은
				// 파티 종류와 무관한 코어 파괴(OffenseProcessor.OnCoreDestroyed)로도 바뀌는 신호라,
				// 실제 점령 트리거가 생기면 교체할 것(검증문서 01-01 3번 참고).
				return room.RoomFaction == FactionType.Human;
			case PartyType.MopUp:
				// 01번 7-1장: 문까지 이동하며 확인한 뒤 현재 방에 대응할 적·교전 정보가 남지 않으면 — 이동 없이 "적을 모름"만으로는 부족하다
				// (스폰 직후부터 참이 돼 도착 즉시 집결하던 문제). 방 전체 탐색·전멸 확인은 요구하지 않는다.
				return Leader.personalSpottedEnemies.Count == 0 && DoorApproachRoom != null && DoorApproachRoom == room;
			default:
				return Leader.personalSpottedEnemies.Count == 0;
		}
	}

	private bool HasKnownRecoverableInRoom(Room room)
	{
		if (Leader?.Session == null) return false;
		foreach (var obj in Leader.Session.objectGrid.Values)
		{
			if (obj == null || obj.IsCollected || obj.Position.z != Leader.currentFloor) continue;
			if (!room.Bounds.Contains(new Vector2Int(obj.Position.x, obj.Position.y))) continue;
			if (!Leader.personalMap.IsObjectKnown(obj.Id)) continue;
			foreach (var tag in obj.Tags)
			{
				if (tag.Contains("Loot")) return true;
			}
		}
		return false;
	}


	// 생존 파티원 중 leadership 최댓값을 리더로 재선정한다 — 리더가 없거나 죽었을 때 GameSession이 호출한다.
	public void AssignLeaderIfNeeded()
	{
		if (Leader != null && Leader.hp > 0) return;

		Human best = null;
		float bestLeadership = float.MinValue;
		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0) continue;
			if (m.leadership > bestLeadership) { bestLeadership = m.leadership; best = m; }
		}
		Leader = best;
	}

	// 이번 웨이브에 이 파티와 함께 소환된 몬스터 목록 — 전부 죽으면 "웨이브 클리어"로 보고 생존자
	// 전역 반영(HumanKnowledgeBase.OnWaveEnd)을 트리거한다. 여러 Party가 같은 리스트 참조를 공유하는
	// 설계라 통짜 재대입은 스폰 시점 한 곳에서만 허용한다.
	public List<Monster> WaveMonsters { get; private set; } = new List<Monster>();
	public void SetWaveMonsters(List<Monster> monsters) => WaveMonsters = monsters;

	// 이 파티의 이번 웨이브가 이미 종료 처리(전멸/클리어)됐는지 — 유닛 사망마다 재확인하므로 중복 트리거를 막아야 한다.
	public bool WaveEnded;

	// 파티원 사망 발견: 시체 오브젝트 Id → 그 사망 사건의 진행 상태. PartyDeathSystem이 읽고 쓴다.
	// WaveSpawner가 웨이브마다 새 Party를 만들므로 별도 정리 없이 웨이브 경계에서 자연히 초기화된다.
	public readonly Dictionary<string, PartyDeathRecord> DeathRecords = new Dictionary<string, PartyDeathRecord>();

	// 함정 오브젝트 Id → 그 함정의 발견자/선정 해제 유닛 조율 상태. TrapPartySystem이 읽고 쓴다.
	public readonly Dictionary<string, TrapPartyCoordination> TrapCoordinations = new Dictionary<string, TrapPartyCoordination>();

	// 03번 v0.12 8장·v0.6 9-3: 웨이브 진입 전(파티 생성 시점)에 파티 내 함정 해제 성공률이 가장 높은 것으로 확인된 유닛 — 이 유닛이 직접 발견하면
	// 2초 응답 대기 없이 담당이 된다. 동률이면 전원 포함(아무도 더 높지 않아 응답을 기다릴 이유가 없다). GameSession.CreateParty가 채운다.
	public readonly HashSet<string> EntryBestDisarmerNames = new HashSet<string>();

	// 01번 문서 9장: 조사·회수 발견 정보의 지속 재전파 — DeathRecords/TrapCoordinations와 동일한
	// "파티 단위 추적, 매 틱 재확인" 패턴(PropagationSystem.TickOngoingObjectPropagation이 소비).
	// 조사 완료는 위치까지, 회수 완료는 사실만 필요해 따로 추적한다.
	public readonly Dictionary<string, Vector3Int> KnownInvestigatedObjects = new Dictionary<string, Vector3Int>();
	public readonly HashSet<string> KnownCollectedObjectIds = new HashSet<string>();

	// 05번 7장 408·454~456줄(검증 05-06): 발견한 문은 개인 지도 기록이자 "전달 가능한 정보"다 — 이 파티원 중 누군가 시야로 확인한 문 오브젝트 Id → 대표 위치.
	// 같은 지속 재전파 패턴(PropagationSystem.TickOngoingObjectPropagation)이 아직 모르는 파티원에게 전파 조건이 성립할 때 전달한다. 문이 파괴되면 전파 순회 중 정리된다.
	public readonly Dictionary<string, Vector3Int> KnownDoorObjects = new Dictionary<string, Vector3Int>();

	// 리더가 방 경로를 정할 때 이미 지나온 방을 피하도록, 이 파티 구성원이 들어가 본 방 (층, 방 Id) — UnitFunction.SyncRoomAffiliation이 방이 바뀔 때 채운다(04번 1장, 검증 05-06 관찰 2).
	private readonly HashSet<(int floor, int roomId)> _visitedRooms = new HashSet<(int floor, int roomId)>();
	public void OnMemberEnteredRoom(int floor, int roomId) => _visitedRooms.Add((floor, roomId));
	public bool HasVisitedRoom(int floor, int roomId) => _visitedRooms.Contains((floor, roomId));

	// 리더가 방 안에서 다음 이동 문을 못 찾은 채 오래 지났다 — 개인 탐색의 방 제한을 리더에 한해 풀어 방 밖까지 탐색해 문을 찾게 하는 안전 해치(영구 정지 방지). HumanWaveManager가 켜고 끈다.
	public bool LeaderMayExploreBeyondRoom;

	// 검증문서 03-02: 파티 목표 상호작용이 "시작"되는 순간(완료 아님)의 합류 정보 — 위
	// KnownInvestigatedObjects와 동일한 지속 재전파 패턴(PropagationSystem.TickOngoingObjectPropagation)
	// 이지만 소비 방식이 다르다. 등록만 해주면 자유로운 파티원은 기존 FindInvestigateTarget이 자연히
	// 그 대상을 후보로 찾아 합류 이동을 시작하고, 전투·도주·다른 상호작용·집결 중인 파티원은
	// CanInvestigate의 기존 우선순위 게이트에 막혀 그대로 현재 행동을 유지한다(03번 문서 3번 항목의
	// "유지해야 하는 수신자" 처리를 새 강제 상태 없이 기존 FSM 우선순위만으로 재현).
	public readonly Dictionary<string, Vector3Int> PartyGoalJoinTargets = new Dictionary<string, Vector3Int>();

	public Party(string id, string name)
	{
		Id = id;
		Name = name;
	}

	// 13-1장: 파티 전체가 사망하면 파티 전멸.
	public bool IsWiped
	{
		get
		{
			if (Members.Count == 0) return false;
			foreach (var m in Members)
				if (m != null && m.Health.hp > 0) return false;
			return true;
		}
	}

	// 6장: 이 파티와 함께 들어온 몬스터가 전부 죽으면 웨이브 클리어로 본다.
	public bool IsWaveCleared
	{
		get
		{
			if (WaveMonsters.Count == 0) return false;
			foreach (var m in WaveMonsters)
				if (m != null && m.Health.hp > 0) return false;
			return true;
		}
	}

	// 생존자 전역 반영(OnWaveEnd)에 넘길 생존자 목록. "생존/후퇴/도주 성공 유닛"을 이 샘플 구현에서는
	// "웨이브 종료 시점에 살아있는 파티원"으로 근사한다 — 후퇴/도주 판정 시스템이 아직 없다.
	public List<Unit> GetSurvivors()
	{
		var survivors = new List<Unit>();
		foreach (var m in Members)
			if (m != null && m.Health.hp > 0) survivors.Add(m);
		return survivors;
	}

	private string LogTag => PartyDiagnostics.TagOf(this);

	// 방 활동이 아직 안 끝난 사유 — IsRoomActivityComplete가 false일 때만 부른다.
	private string DescribeRoomActivityIncomplete()
	{
		Room room = Leader.currentRoom;
		if (room == null) return $"방 활동 미종료 — 리더 {Leader.name}이(가) 어느 방에도 속하지 않음(통로·문 위치 등)";

		string head = $"방 활동 미종료({Type.ToKorean()}) — 방 {room.RoomId}";
		switch (Type)
		{
			case PartyType.Explore:
			{
				int revealed = _partyRevealedTilesByRoom.TryGetValue(room.RoomId, out var tiles) ? tiles.Count : 0;
				int total = Leader.Session?.cmap != null ? Leader.Session.cmap.GetRoomFloorTileCount(Leader.currentFloor, room.RoomId) : -1;
				int frontier = Leader.personalMap.CountFrontierTilesInBounds(Leader.currentFloor, room.Bounds, out Vector2Int sample);
				return $"{head}: 파티 시야 합산 {revealed}/{(total >= 0 ? total.ToString() : "?")}칸으로 미완이고, 리더 개인 지도에 방 안 프론티어 {frontier}개가 남음" + (frontier > 0 ? $"(예: {sample})" : "");
			}
			case PartyType.Recover:
				return $"{head}: 리더가 아는 회수물이 방 안에 남음";
			case PartyType.Occupy:
				return $"{head}: 방 소유 진영이 인류가 아님(현재 {room.RoomFaction})";
			case PartyType.MopUp:
				return $"{head}: 리더가 인지한 적 {Leader.personalSpottedEnemies.Count}명, 다음 문 앞 접근 {(DoorApproachRoom != null && DoorApproachRoom == room ? "완료" : "미완료")}";
			default:
				return $"{head}: 리더가 인지한 적 {Leader.personalSpottedEnemies.Count}명";
		}
	}

	// 검증 04-04: 이 구성원의 이동 기본 속도(경계 감속을 곱하기 전, Human.MovementBaseSpeed가 호출한다). 공동 이동(방 이동·문 찾기 추종 대기) 중이고 리더 기준 합류 반경
	// (doorSearchFollowRadius, 3칸) 안이면 "같은 층의 살아 있는 이동 가능한 구성원 중 가장 느린 이동 능력치"(상호작용으로 잠깐 멈춘 구성원도 포함)를 쓰고, 아니면 자기 능력치다 —
	// 뒤처진 구성원은 자기 속도로 복귀하다 합류하면 공동 속도로 바뀐다. 이동 불가(isImmobile·속도 0)·사망·다른 층 구성원은 계산에서 뺀다. 모든 기준은 사용자 확정(2026-10-01)이며
	// 진형 문서가 생기면 "합류"를 실제 진형 자리 도착으로 바꿀 자리다.
	public float ResolveMoveBaseSpeed(Human member)
	{
		float individual = member.BaseStat.walkSpeed;
		var wait = member.currentWait;
		bool inCoMovement = wait != null && (wait.Reason == WaitReason.AdvancingToNextRoom || wait.Reason == WaitReason.SearchingNextDoor
			|| wait.Reason == WaitReason.FormingUpAtDoor || wait.Reason == WaitReason.EnteringNextRoom);
		if (!inCoMovement) return individual; // 개인 탐색·보고·전투 등은 개인 속도 — 구성원 순회 없이 바로 반환

		bool hasLeader = Leader != null && Leader.hp > 0 && Leader.currentFloor == member.currentFloor;
		int distanceToLeader = hasLeader ? AIMovementHelper.ChebyshevDistance(member.position, Leader.position) : 0;
		bool joined = PartyReportMath.IsJoinedToLeader(hasLeader, distanceToLeader, AIConfigLoader.Behavior?.doorSearchFollowRadius ?? 3);
		if (!joined) return individual;

		float slowest = individual;
		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0 || m.currentFloor != member.currentFloor || m.isImmobile) continue;
			float speed = m.BaseStat.walkSpeed;
			if (speed <= 0f) continue; // 이동 능력이 없는 구성원은 공동 속도를 0으로 끌어내리지 않는다
			if (speed < slowest) slowest = speed;
		}
		return PartyReportMath.ResolveMoveBaseSpeed(true, true, individual, slowest);
	}
}
