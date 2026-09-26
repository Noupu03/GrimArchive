using System.Collections.Generic;
using UnityEngine;
using GrimArchive.Wave;

// 파티 — 인류 유닛들이 함께 웨이브(던전)에 입장하는 단위. 생존자 전역 반영/파티 전멸/정보 오차
// 공유 등 "파티"를 전제로 하는 가중치 로직의 실체. GameSession.CreateParty()로 생성·등록된다.
public class Party
{
	public string Id;
	public string Name;
	// 01번 문서 7-1장: 파티 종류별 방 활동 종료·집결 기준에 쓰인다. GameSession.CreateParty가 생성
	// 시점에 4종(PartyEnums.cs 참고, 2026-09-25 회의록 반영: 점령 파티 추가) 중 하나를 무작위로
	// 배정한다(편성 가능 유닛·선택 가중치는 미작성 문서 영역이라 스텁).
	public PartyType Type;
	public readonly List<Human> Members = new List<Human>();

	// 리더·명령 체계 문서가 아직 없어 임시로 추가한 최소 리더 개념 — "전투 종료 후 경계 10초 → 리더가
	// 집결 명령 생성 → 파티원이 집결"이 요구하는 최소 기능만 채운다. 생존 파티원 중 leadership 최댓값을
	// 리더로 삼고, 리더가 죽으면 GameSession이 재선정한다.
	public Human Leader;

	// 집결의 최소 버전 — 전투 종료 후 경계 10초가 끝나면 리더가 자기 위치를 집결지로 지정한다. 05번
	// 문서 4장: 같은 방 파티원은 무조건, 다른 방 파티원은 일반 전파 조건(CanPropagate)을 만족해야
	// 전달된다(TryStartRally 참고). Goal_Wait이 이 값을 읽는다.
	public Vector2Int? RallyPoint;
	public bool IsRallyActive;
	// 05번 1장: 집결이 막 완료돼 "다음 방으로 함께 이동"을 시작해도 되는 상태 — HumanWaveManager가
	// 이 값을 보고 다음 문으로의 공동 이동 명령을 1회 발행한 뒤 false로 되돌린다(리더·명령 문서가
	// 아직 없어 웨이브 전용 단일 목표 방 전제 하의 최소 구현).
	public bool ReadyToAdvance;

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

	// 01번 문서 7-2장: "여러 유닛이 나눠 본 시야를 합쳐 방 지형 확인 완료를 판단"하는 실제 집계 —
	// 방(roomId)별로 파티원 누구든 새로 밝힌 바닥 타일 좌표를 한 집합에 모아, 합친 개수가 그 방의
	// 전체 바닥 타일 수에 도달하면 "파티 기준 완료"로 표시한다(검증문서 01-06-4 1번 참고). 개인별
	// RevealedFloorTiles(PersonalMapKnowledge)와는 별개 집계 — 리더가 직접 안 본 타일도 다른
	// 파티원이 봤다면 여기엔 반영된다.
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

	// 01번 문서 7-1장: 파티 종류별 "현재 방 활동 종료" 기준. 2026-09-27부터 Explore 분기는 위
	// 파티 전체 시야 합산(IsRoomFullyRevealedByParty)을 우선 확인하고, 그래도 안 되면 기존 리더 개인
	// 지도 근사로 보조한다(둘 중 하나만 참이어도 완료 — 어느 쪽도 기존 동작을 깎아먹지 않는 추가).
	// 2026-09-25 회의록 반영: 점령 파티는 방 점령 완료를 기준으로 추가했다 — 보스공략은 여전히 범위
	// 밖(구현현황 문서 참고).
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
				// 점령을 실제로 트리거하는 조건은 아직 미정(PartyEnums.cs 주석 참고) — 판정 기준
				// 자체("방이 인류 소유가 되면 완료")만 세팅으로 남겨둔다. 주의: RoomFaction은 파티
				// 종류와 무관한 보스방 코어 파괴(OffenseProcessor.OnCoreDestroyed)로도 바뀌므로 지금은
				// 점령 파티 전용 신호가 아니라 그 신호를 임시로 빌려 쓰는 것뿐 — 실제 점령 트리거가
				// 생기면 교체할 것(검증문서 01-01 3번 참고).
				return room.RoomFaction == FactionType.Human;
			case PartyType.MopUp:
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

	// 리더 전용 주기 체크 — 전투 없이도(스윕 트리거 없이도) 방 활동이 끝나면 집결을 시작할 수 있어야
	// 하므로(01번 7-1장 "전투가 없었던 방에도 같은 기준 사용"), UnitFunction.OnUpdate가 매 프레임 대신
	// 이 타이머로 던진다.
	private float _nextRoomActivityCheckTime;
	public void TickRoomActivityCheck(float currentTime)
	{
		if (currentTime < _nextRoomActivityCheckTime) return;
		_nextRoomActivityCheckTime = currentTime + 1f;
		TryStartRally();
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

	// 01번 문서 9장/검증문서 01-09 2번: 일반 오브젝트(조사·회수) 발견 정보의 지속 재전파 —
	// DeathRecords/TrapCoordinations와 동일한 "파티 단위로 추적해 매 틱 재확인" 패턴. 완료 시점
	// 일회성 전파(TacticalFSMState.InvestigatePerform/PickUpObject)가 그 순간 범위 밖이었던 파티원을
	// 놓쳐도, 여기 등록해두면 PropagationSystem.TickOngoingObjectPropagation이 나중에 채워준다.
	// 조사 완료(위치까지 필요, objectGrid 재조회용)와 회수 완료(위치 불필요, 사실 자체만)는 파티원에게
	// 적용할 정보가 달라 따로 추적한다.
	public readonly Dictionary<string, Vector3Int> KnownInvestigatedObjects = new Dictionary<string, Vector3Int>();
	public readonly HashSet<string> KnownCollectedObjectIds = new HashSet<string>();

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

	// 전투 종료 후 10초 경계 스윕이 끝난 유닛이(UnitFunction.OnUpdate) 호출한다. 파티 전체가 전투/
	// 전투직후 스윕에서 완전히 벗어났을 때만 실제로 집결을 시작하고, 아직 남은 파티원이 있으면 그
	// 유닛이 끝날 때 재시도된다.
	public void TryStartRally()
	{
		if (IsRallyActive) return;

		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0) continue;
			if (m.personalSpottedEnemies.Count > 0) return; // 아직 전투 중인 파티원 있음(명시적 전투 플래그 부재로 근사)
			if (m.currentAlertSearch != null && m.currentAlertSearch.IsPostCombatSweep) return; // 아직 스윕 중
		}

		AssignLeaderIfNeeded();
		if (Leader == null) return;

		// 03번 문서 3번 항목: 리더가 미처리 코어를 이미 알고 있으면 그걸 두고 집결을 명령하지 않는다.
		// 코어가 처리(파괴→점령)돼 더 이상 적대적이지 않으면 여기서 스스로 감지해 지운다.
		if (LeaderKnownCorePosition.HasValue)
		{
			if (TacticalFSMState.IsRoomCoreStillHostile(Leader, LeaderKnownCorePosition.Value)) return;
			LeaderKnownCorePosition = null;
		}

		// 01번 8장: 임무 요구 수량이 막 달성됐다는 결과 자체도 유효한 집결 판단 근거다 — 파티종류별
		// "현재 방" 기준(IsRoomActivityComplete)과는 별개 사유라, 어느 한쪽만 참이어도 집결을 시작한다
		// (검증문서 01-08 참고). 소비 즉시 리셋해 이후 다른 방에서 다시 트리거되지 않게 한다.
		bool questJustAchieved = JustReachedRoomExploreQuota ||
			(HumanWaveManager.Instance != null && HumanWaveManager.Instance.JustReachedMonsterKillQuota);

		// 01번 7-1장: 전투/스윕이 끝났어도 파티 종류 기준으로 아직 이 방에서 할 일이 남았으면 집결하지
		// 않는다. 다만 위 임무 수량 달성 결과가 있다면 그것만으로도 집결 판단으로 넘어간다.
		if (!questJustAchieved && !IsRoomActivityComplete()) return;

		if (questJustAchieved)
		{
			JustReachedRoomExploreQuota = false;
			if (HumanWaveManager.Instance != null) HumanWaveManager.Instance.JustReachedMonsterKillQuota = false;
		}

		RallyPoint = Leader.position;
		IsRallyActive = true;

		// 05번 문서 4장: 같은 방 파티원에게는 전파 거리와 무관하게 무조건 전달(방 전체 전달 예외), 다른
		// 방 파티원에게는 일반 전파 조건(07문서 6장 — 비전투+같은 공간+전파 범위, PropagationSystem.
		// CanPropagate가 그 구현)을 만족해야만 전달된다(검증문서 01-02 6번).
		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0 || m.currentWait != null) continue;
			if (m.currentRoom != Leader.currentRoom && !PropagationSystem.CanPropagate(Leader, m)) continue;
			m.currentWait = new WaitState { Reason = WaitReason.AwaitingPartyAtRallyPoint, WaitPosition = RallyPoint };
		}
	}

	// TacticalFSMState.ExecuteWait이 유닛 하나가 집결지에 도착해 currentWait을 비울 때마다 호출한다.
	// 아직 집결 대기 중인 파티원이 남아있으면 유지, 전원 도착했으면 집결을 종료한다(별도 마칭
	// 포메이션 시스템이 없어 "다음 목표 수행"으로 자연히 넘어가는 것을 대체로 본다).
	public void CheckRallyComplete()
	{
		if (!IsRallyActive) return;
		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0) continue;
			// ReportingCoreToLeader도 "아직 집결 미완료"로 취급 — 코어 보고 중인 유닛이
			// PartyCoreReportSystem에 의해 AwaitingPartyAtRallyPoint에서 이쪽으로 전환됐을 때, 그
			// 유닛이 보고를 마치기(→ OnLeaderLearnsCore가 이 집결 자체를 해제) 전에 나머지 인원만으로
			// 집결이 먼저 "완료" 처리돼 ReadyToAdvance가 앞서 발생하는 것을 막는다(2026-09-26 발견).
			if (m.currentWait != null &&
				(m.currentWait.Reason == WaitReason.AwaitingPartyAtRallyPoint || m.currentWait.Reason == WaitReason.ReportingCoreToLeader))
				return;
		}
		IsRallyActive = false;
		RallyPoint = null;
		ReadyToAdvance = true; // 05번 1장: 집결 완료 → 진형 유지해 다음 방으로 이동할 차례.
	}

	// 03번 문서 3번 항목: 리더가 코어를 직접 확인했거나(discoverer == Leader) 보고받았을 때
	// PartyCoreReportSystem이 호출한다. 진행 중이거나 막 완료된 집결·다음 방 이동을 즉시 해제하고
	// 코어 처리로 전환한다 — 전달 범위는 TryStartRally와 동일하게 05번 문서 4장을 따른다(같은 방은
	// 무조건, 다른 방은 CanPropagate 게이트, 검증문서 01-02 6번).
	public void OnLeaderLearnsCore(Vector3Int corePos)
	{
		if (LeaderKnownCorePosition == corePos) return; // 이미 알고 있음 — 중복 처리 방지
		LeaderKnownCorePosition = corePos;

		IsRallyActive = false;
		RallyPoint = null;
		ReadyToAdvance = false;
		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0) continue;
			if (m.currentWait == null ||
				(m.currentWait.Reason != WaitReason.AwaitingPartyAtRallyPoint && m.currentWait.Reason != WaitReason.AdvancingToNextRoom))
				continue;
			if (m.currentRoom != Leader.currentRoom && !PropagationSystem.CanPropagate(Leader, m)) continue;
			m.currentWait = null;
		}
	}
}
