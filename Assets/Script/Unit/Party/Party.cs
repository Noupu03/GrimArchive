using System.Collections.Generic;
using UnityEngine;
using GrimArchive.Wave;
using Haare.Util.Logger;

// 파티 — 인류 유닛들이 함께 웨이브(던전)에 입장하는 단위. 생존자 전역 반영/파티 전멸/정보 오차
// 공유 등 "파티"를 전제로 하는 가중치 로직의 실체. GameSession.CreateParty()로 생성·등록된다.
public class Party
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

	// 집결의 최소 버전 — 전투 종료 후 경계 10초가 끝나면 리더가 자기 위치를 집결지로 지정한다. 05번
	// 문서 4장: 같은 방 파티원은 무조건, 다른 방 파티원은 일반 전파 조건(CanPropagate)을 만족해야
	// 전달된다(TryStartRally 참고). TacticalFSMState.ExecuteWait이 이 값을 읽는다.
	public Vector2Int? RallyPoint;
	// RallyPoint가 속한 층 — 좌표만으로는 층을 알 수 없어, 계단을 건넌 파티원이 다른 층의 같은 좌표로 걸어가지 않게 한다.
	public int RallyFloor;
	public bool IsRallyActive;
	// 집결 시작 시각 / 집결 구역 반경(배정된 개별 자리 중 집결지에서 가장 먼 것) — 좁은 길에서 자리를 못 잡은 유닛을 "구역 안이면 그 자리에서 집결 처리"하는 기준이다.
	public float RallyStartTime;
	public int RallyZoneRadius = 1;

	public bool IsRallyPointOnFloor(int floor) => IsRallyActive && RallyPoint.HasValue && RallyFloor == floor;
	// 05번 1장: 집결이 막 완료돼 "다음 방으로 함께 이동"을 시작해도 되는 상태 — HumanWaveManager가
	// 이 값을 보고 다음 문으로의 공동 이동 명령을 1회 발행한 뒤 false로 되돌린다(리더·명령 문서가
	// 아직 없어 웨이브 전용 단일 목표 방 전제 하의 최소 구현).
	public bool ReadyToAdvance;
	// 검증문서 03-18: 마지막으로 집결을 마친 방. 리더가 이 방을 떠나기 전까지는 "집결을 마치고 다음 방으로 이동 중"이라
	// TryStartRally가 같은 방에서 집결을 다시 시작하지 않는다 — 리더의 1초 주기 판정이 이동 중에 다시 집결을 시작하면
	// 이동 명령을 받은 파티원이 집결 대기를 못 받아 IsRallyActive가 영구히 남거나(이후 방의 집결이 막힘) 문 앞에서
	// 집결이 반복된다. 리더가 다른 방으로 나가거나 코어 처리로 집결이 해제되면 비운다.
	public Room AdvanceFromRoom { get; private set; }

	// 집결 뒤 "문 앞 진형 → 문 파괴 → 순차 입장" 진행 계획 — PartyAdvanceSystem이 만들고 매 틱 전이시키며, 끝나면 null이다. 있는 동안은 새 집결을 시작하지 않는다.
	public PartyAdvancePlan AdvancePlan;
	// 지금 방에서 진형 계획을 시작한 횟수(PartyAdvanceSystem.Begin이 올린다). 중단 뒤 재시도·예전 방식 폴백 판단에 쓰며, 리더가 이 방을 떠나면(TickAdvanceState) 0으로 돌아간다.
	public int AdvanceAttempts;

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

	// TryStartRally 전용 — 웨이브 공통 몬스터 처치 수량(HumanWaveManager.CommonMonsterKillCount) 달성을
	// 이 파티가 이미 집결 트리거로 한 번 썼는지. 파티마다 각자 들고 있어야 여러 파티가 있어도 서로
	// 간섭하지 않는다(위 TryStartRally 주석 참고).
	private bool _monsterKillQuotaConsumed;

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

	// 03번 v0.12 8장·v0.6 9-3: 웨이브 진입 전(파티 생성 시점)에 파티 내 함정 해제 성공률이 가장 높은 것으로 확인된 유닛 — 이 유닛이 직접 발견하면
	// 2초 응답 대기 없이 담당이 된다. 동률이면 전원 포함(아무도 더 높지 않아 응답을 기다릴 이유가 없다). GameSession.CreateParty가 채운다.
	public readonly HashSet<string> EntryBestDisarmerNames = new HashSet<string>();

	// 01번 문서 9장: 조사·회수 발견 정보의 지속 재전파 — DeathRecords/TrapCoordinations와 동일한
	// "파티 단위 추적, 매 틱 재확인" 패턴(PropagationSystem.TickOngoingObjectPropagation이 소비).
	// 조사 완료는 위치까지, 회수 완료는 사실만 필요해 따로 추적한다.
	public readonly Dictionary<string, Vector3Int> KnownInvestigatedObjects = new Dictionary<string, Vector3Int>();
	public readonly HashSet<string> KnownCollectedObjectIds = new HashSet<string>();

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

	// 전투 종료 후 10초 경계 스윕이 끝난 유닛이(UnitFunction.OnUpdate) 호출한다. 파티 전체가 전투/
	// 전투직후 스윕에서 완전히 벗어났을 때만 실제로 집결을 시작하고, 아직 남은 파티원이 있으면 그
	// 유닛이 끝날 때 재시도된다.
	public void TryStartRally()
	{
		if (IsRallyActive)
		{
			CheckRallyComplete(); // 도착한 파티원도 자리에서 기다리므로, 전원 도착 여부를 리더의 1초 주기로도 확인한다
			if (IsRallyActive && ShouldReportRally(RallyReport.Active, 0)) LogRally($"집결 진행 중 — {DescribeActiveRally()}");
			return;
		}

		if (AdvancePlan != null) // 문 앞 진형·돌파·입장 중 — 같은 이동 중에는 다시 집결하지 않는다
		{
			if (ShouldReportRally(RallyReport.AdvancePlanActive, (int)AdvancePlan.Phase)) LogRally($"집결 불가 — 다음 방 진입 진행 중({AdvancePlan.Phase})");
			return;
		}

		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0) continue;
			if (m.isInDungeonEntranceSequence) // 아직 던전(계단)에 다 들어오지 못한 파티원 — 웨이브 시작 전 대기 중에는 집결하지 않는다
			{
				if (ShouldReportRally(RallyReport.EntranceSequence, m.GetInstanceID())) LogRally($"집결 불가 — {m.name}이(가) 아직 던전 입구 시퀀스 중(계단 도착 전)");
				return;
			}
			if (m.personalSpottedEnemies.Count > 0) // 아직 전투 중인 파티원 있음(명시적 전투 플래그 부재로 근사)
			{
				if (ShouldReportRally(RallyReport.InCombat, m.GetInstanceID())) LogRally($"집결 불가 — {m.name}이(가) 적 {m.personalSpottedEnemies.Count}명을 인지 중(전투 중 근사)");
				return;
			}
			if (m.currentAlertSearch != null && m.currentAlertSearch.IsPostCombatSweep) // 아직 스윕 중
			{
				if (ShouldReportRally(RallyReport.PostCombatSweep, m.GetInstanceID())) LogRally($"집결 불가 — {m.name}이(가) 전투 후 경계 스윕 중(경계 {m.currentAlertSearch.ElapsedSeconds:F1}초)");
				return;
			}
		}

		AssignLeaderIfNeeded();
		if (Leader == null)
		{
			if (ShouldReportRally(RallyReport.NoLeader, 0)) LogRally("집결 불가 — 리더가 없음(생존 파티원 없음)");
			return;
		}
		if (Leader.pendingStairTargetFloor.HasValue) // 리더가 층을 건너는 중 — 도착한 층에서 판단한다
		{
			if (ShouldReportRally(RallyReport.LeaderOnStairs, Leader.pendingStairTargetFloor.Value)) LogRally($"집결 불가 — 리더 {Leader.name}이(가) {Leader.pendingStairTargetFloor.Value}층으로 이동 중");
			return;
		}

		// 집결을 마친 방을 리더가 아직 떠나지 않았다 — 공동 이동 중이므로 같은 방에서 다시 집결하지 않는다(05번 1장·2장).
		// 임무 수량 달성 신호(JustReached…)는 아래에서 소비하므로 여기서 막혀도 사라지지 않고 다음 판정으로 이어진다.
		TickAdvanceState();
		if (AdvanceFromRoom != null)
		{
			if (ShouldReportRally(RallyReport.AdvanceFromRoom, AdvanceFromRoom.RoomId))
				LogRally($"집결 불가 — 집결을 마친 방 {AdvanceFromRoom.RoomId}을(를) 리더 {Leader.name}이(가) 아직 떠나지 않음(다음 방 이동 중: ReadyToAdvance={ReadyToAdvance}, 리더 현재 방 {Leader.currentRoom?.RoomId.ToString() ?? "없음"})");
			return;
		}

		// 03번 문서 3번 항목: 리더가 미처리 코어를 이미 알고 있으면 그걸 두고 집결을 명령하지 않는다.
		// 코어가 처리(파괴→점령)돼 더 이상 적대적이지 않으면 여기서 스스로 감지해 지운다.
		if (LeaderKnownCorePosition.HasValue)
		{
			if (TacticalFSMState.IsRoomCoreStillHostile(Leader, LeaderKnownCorePosition.Value))
			{
				if (ShouldReportRally(RallyReport.HostileCore, 0)) LogRally($"집결 불가 — 리더가 미처리 코어 {LeaderKnownCorePosition.Value}를 알고 있어 코어 처리가 우선");
				return;
			}
			LeaderKnownCorePosition = null;
		}

		// 01번 8장: 임무 수량 달성도 유효한 집결 사유다 — IsRoomActivityComplete와는 별개라 둘 중
		// 하나만 참이어도 집결을 시작한다(검증문서 01-08). 몬스터 처치는 웨이브 공통 집계라, 파티별
		// 소비 여부를 _monsterKillQuotaConsumed로 따로 추적한다(공유 플래그면 먼저 확인한 파티가
		// 나머지 몫까지 꺼버림).
		bool monsterKillQuotaAvailable = !_monsterKillQuotaConsumed && HumanWaveManager.Instance != null &&
			HumanWaveManager.Instance.CommonMonsterKillCount >= PartyGoalMath.RequiredMonsterKillCount;
		bool questJustAchieved = JustReachedRoomExploreQuota || monsterKillQuotaAvailable;

		// 01번 7-1장: 전투/스윕이 끝났어도 파티 종류 기준으로 아직 이 방에서 할 일이 남았으면 집결하지
		// 않는다. 다만 위 임무 수량 달성 결과가 있다면 그것만으로도 집결 판단으로 넘어간다.
		if (!questJustAchieved && !IsRoomActivityComplete())
		{
			if (ShouldReportRally(RallyReport.RoomActivityIncomplete, Leader.currentRoom != null ? Leader.currentRoom.RoomId : -1))
				LogRally($"집결 불가 — {DescribeRoomActivityIncomplete()}");
			return;
		}

		if (questJustAchieved)
		{
			JustReachedRoomExploreQuota = false;
			if (monsterKillQuotaAvailable) _monsterKillQuotaConsumed = true;
		}

		DoorApproachRoom = null; // 문 앞 접근 표식은 이 집결이 소비한다 — 다음 방에서는 다시 접근해야 한다
		_lastRallyReport = default; // 진단 로그: 다음 막힘은 같은 사유여도 다시 한 번 남긴다
		RallyPoint = Leader.position;
		RallyFloor = Leader.currentFloor;
		IsRallyActive = true;
		RallyStartTime = Time.time;
		RallyZoneRadius = 1;
		LogHelper.Log(LogHelper.GAME, $"{LogTag} 집결 시작 — 리더 {Leader.name}, 사유: {(questJustAchieved ? "임무 수량 달성" : "방 활동 종료")}, 집결지 {RallyPoint.Value} ({RallyFloor}층)");

		// 05번 문서 4장: 같은 방 파티원에게는 전파 거리와 무관하게 무조건 전달(방 전체 전달 예외), 다른
		// 방 파티원에게는 일반 전파 조건(07문서 6장 — 비전투+같은 공간+전파 범위, PropagationSystem.
		// CanPropagate가 그 구현)을 만족해야만 전달된다(검증문서 01-02 6번).
		var recipients = new List<Human>();
		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0) continue;
			if (m.currentFloor != RallyFloor) continue; // 다른 층 파티원에게는 이 집결이 닿지 않는다
			if (m.currentRoom != Leader.currentRoom && !PropagationSystem.CanPropagate(Leader, m)) continue;
			// 명령이 실제로 전달된 파티원은 집결 위치와 그 시점의 리더 위치를 안다(검증문서 03-15). 이미 다른 대기(코어 보고
			// 이동 등) 중이라 집결 이동은 하지 않는 파티원도 정보는 받는다 — 보고가 빈 리더 위치에 닿았을 때 갈 곳이 된다.
			m.knownRallyPoint = RallyPoint;
			m.knownLeader.Update(Leader, RallyPoint.Value, Time.time);
			// 다음 문을 찾으며 리더를 따라다니던 공동 탐색 추종과, 임무 수량 달성으로 문 앞 도착 전에 집결이 시작된 리더의 문 접근은 집결로 전환한다.
			if (m.currentWait != null && m.currentWait.Reason != WaitReason.SearchingNextDoor && m.currentWait.Reason != WaitReason.ApproachingNextDoor) continue;
			// 전파받은 적 위치로 접근하던 경계도 집결 명령이 오면 접는다(03번 1장 50줄 — 개인 탐색으로 흩어지지 않음).
			if (m.currentAlertSearch != null && m.currentAlertSearch.IsIndirectEnemyApproach) m.currentAlertSearch = null;
			// 03번 0장·8장: 아직 시작하지 않은 함정 대응(응답 대기·담당자 도착 대기·해제하러 가는 이동)은 집결로 전환한다.
			TrapPartySystem.ReleaseForRally(m);
			recipients.Add(m);
		}

		// 집결지에 못 서면 근처에 서도록 파티원마다 자리를 따로 준다 — 리더 가까운 순으로 배정해 서로 길을 막지 않게 한다. 끄면 예전처럼 전원이 집결지 하나로 향한다.
		bool gatherNearby = AIConfigLoader.Behavior?.rallyGatherNearbyEnabled ?? true;
		var claimed = new HashSet<Vector2Int> { RallyPoint.Value };
		if (gatherNearby)
			recipients.Sort((a, b) => AIMovementHelper.ChebyshevDistance(a.position, RallyPoint.Value).CompareTo(AIMovementHelper.ChebyshevDistance(b.position, RallyPoint.Value)));
		var assignedSlots = new List<Vector2Int>(recipients.Count);
		foreach (var m in recipients)
		{
			Vector2Int slot = RallyPoint.Value;
			if (gatherNearby && m != Leader) slot = PickGatherSlot(m, claimed);
			assignedSlots.Add(slot);
			m.currentWait = new WaitState { Reason = WaitReason.AwaitingPartyAtRallyPoint, WaitPosition = slot, WaitFloor = RallyFloor };
			m.waitStuckTurns = 0;
		}
		RallyZoneRadius = PartyFormationMath.ZoneRadius(assignedSlots, RallyPoint.Value);
	}

	// 집결 구역 안인가(PartyFormationMath.IsWithinZone) — 자리를 못 잡은 유닛이 그 자리에서 집결 처리될 수 있는 범위.
	public bool IsInRallyZone(Vector2Int position)
		=> RallyPoint.HasValue && PartyFormationMath.IsWithinZone(position, RallyPoint.Value, RallyZoneRadius);

	// 자리가 막혔을 때의 재선택 — 집결지가 아니라 "이 유닛에서 가장 가까운" 구역 안 빈 타일을 고른다(좁은 길에서 양 끝을 오가지 않게). 후보는 지금 다른 유닛이 실제로 서 있지 않고
	// (CanMove 점유 포함 — 먼저 도착해 남의 자리에 선 유닛 포함), 같은 방이며, 이미 막혀 포기한 자리(rejected)·다른 파티원이 노리는 자리(claimed)가 아닌 타일이다. 없으면 false.
	public bool TryPickGatherFallback(Human member, HashSet<Vector2Int> claimed, ISet<Vector2Int> rejected, out Vector2Int slot)
	{
		var session = Leader?.Session;
		Room room = Leader?.currentRoom;
		int floor = RallyFloor;
		bool IsFree(Vector2Int t)
		{
			if (rejected != null && rejected.Contains(t)) return false;
			if (!PartyFormationMath.IsWithinZone(t, RallyPoint.Value, RallyZoneRadius)) return false;
			if (!member.CanMove(t)) return false; // 점유 포함 — 지금 누가 서 있는 타일은 제외
			if (session == null) return true;
			var pos3 = new Vector3Int(t.x, t.y, floor);
			if (session.IsDoorTile(pos3)) return false;
			if (room == null || session.roomGrid == null) return true;
			return session.roomGrid.TryGetValue(pos3, out var r) && r == room;
		}
		return PartyFormationMath.TryPickNearestFreeTile(member.position, IsFree, claimed, PartyFormationMath.DefaultSearchRadius, out slot);
	}

	// 집결지(리더 자리) 주변에서 반경을 넓혀 가며 리더와 같은 방 안의 빈 통행 가능 타일을 고른다 — 문 타일·다른 방(문 반대편)은 제외. 못 찾으면 집결지 그대로.
	// 재선택(TacticalFSMState의 집결 이동)도 같은 함수를 쓴다.
	public Vector2Int PickGatherSlot(Human member, HashSet<Vector2Int> claimed)
	{
		var session = Leader?.Session;
		Room room = Leader?.currentRoom;
		int floor = RallyFloor;
		bool IsFree(Vector2Int t)
		{
			if (!member.CanMove(t, ignoreUnits: true)) return false;
			if (session == null) return true;
			var pos3 = new Vector3Int(t.x, t.y, floor);
			if (session.IsDoorTile(pos3)) return false;
			if (room == null || session.roomGrid == null) return true;
			return session.roomGrid.TryGetValue(pos3, out var r) && r == room;
		}
		return PartyFormationMath.TryPickNearestFreeTile(RallyPoint.Value, IsFree, claimed, PartyFormationMath.DefaultSearchRadius, out var slot) ? slot : RallyPoint.Value;
	}

	// 집결을 마친 방을 리더가 떠났으면 그 방에서 시작한 "다음 방으로 이동" 의도와 재집결 억제를 함께 푼다. 안 풀면 다음 문을 못 찾아
	// 소비되지 않은 ReadyToAdvance가 남아, 리더가 스스로 다른 방으로 나간 뒤 새 방의 문이 알려지는 즉시 그 방의 활동을 건너뛰고
	// 이동 명령이 발행된다. HumanWaveManager가 매 프레임, TryStartRally가 판정 때 호출한다.
	public void TickAdvanceState()
	{
		// 문 앞 접근 표식은 그 방을 떠나면 의미가 없다 — 나중에 돌아왔을 때 옛 표식으로 곧바로 집결하지 않게 한다.
		if (DoorApproachRoom != null && Leader != null && Leader.currentRoom != DoorApproachRoom) DoorApproachRoom = null;
		if (AdvanceFromRoom == null || Leader == null) return;
		if (Leader.currentRoom == AdvanceFromRoom) return;
		// 이동 명령이 이미 발행돼 소비된 뒤라면(정상) 리더가 집결한 방을 떠났다는 사실만, 아직 안 발행됐다면(문을 못 찾은 채 리더가
		// 스스로 나감) 이동 의도가 소멸했다는 사실을 남긴다.
		LogHelper.Log(LogHelper.GAME, ReadyToAdvance
			? $"{LogTag} 리더 {Leader.name}이(가) 이동 명령 발행 전에 집결한 방을 떠남 — 방 이동 의도 해제"
			: $"{LogTag} 리더 {Leader.name}이(가) 집결한 방을 떠남 — 재집결 억제 해제");
		AdvanceFromRoom = null;
		ReadyToAdvance = false;
		AdvanceAttempts = 0;
	}

	private string LogTag => $"[파티] {Name}({Type.ToKorean()})";

	// ── 진단 로그: 집결이 시작되지 못하는 사유 / 진행 중인 집결의 도착 상황 ──────────────────────────────────────────
	// TryStartRally는 1초 주기로 불리고 막히면 조용히 return하므로 "왜 집결을 안 하는지"가 로그에 안 남았다. 사유(종류+구분값)가 바뀔 때, 그리고 같은 사유가 이어질 때는
	// RallyReportRepeatSeconds마다 상태를 갱신해 한 줄만 남긴다. 동작에는 영향이 없고, 문구는 사유를 만들 때만 조립한다(막힘 없는 경로엔 할당 없음).
	private enum RallyReport
	{
		None, Active, EntranceSequence, InCombat, PostCombatSweep, NoLeader, LeaderOnStairs, AdvanceFromRoom, HostileCore, RoomActivityIncomplete, AdvancePlanActive,
	}
	private const float RallyReportRepeatSeconds = 15f;
	private (RallyReport reason, int tag) _lastRallyReport;
	private float _lastRallyReportTime;

	private bool ShouldReportRally(RallyReport reason, int tag)
	{
		float now = Time.time;
		if (_lastRallyReport.reason == reason && _lastRallyReport.tag == tag && now - _lastRallyReportTime < RallyReportRepeatSeconds) return false;
		_lastRallyReport = (reason, tag);
		_lastRallyReportTime = now;
		return true;
	}

	private void LogRally(string message) => LogHelper.Log(LogHelper.GAME, $"{LogTag} {message}");

	// 진행 중인 집결: 집결지로 가는 중인(AwaitingPartyAtRallyPoint) 파티원과 그 밖의 상태를 나눠 보여 준다 — "집결이 시작은 됐는데 아무도 안 모인다"를 가려내는 용도.
	private string DescribeActiveRally()
	{
		var heading = new System.Text.StringBuilder();
		var other = new System.Text.StringBuilder();
		int headingCount = 0, otherCount = 0;
		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0) continue;
			var wait = m.currentWait;
			if (wait != null && wait.Reason == WaitReason.AwaitingPartyAtRallyPoint)
			{
				headingCount++;
				bool atSlot = wait.WaitPosition.HasValue && PartyFormationMath.IsAtSlot(m.position, wait.WaitPosition.Value);
				// 도착하지 못한 파티원은 FSM 상태·경계 종류·보이는 적·자리까지 거리를 함께 남긴다(PartyAdvanceSystem.DescribeMember) — 왜 못 서는지 가리기 위한 진단.
				if (atSlot) heading.Append(m.name).Append("[도착] ");
				else heading.Append(PartyAdvanceSystem.DescribeMember(m, wait.WaitPosition)).Append(' ');
			}
			else
			{
				otherCount++;
				other.Append(m.name).Append('[').Append(wait != null ? wait.Reason.ToString() : "대기 없음");
				if (m.currentFloor != RallyFloor) other.Append(", 다른 층 ").Append(m.currentFloor);
				other.Append("] ");
			}
		}
		return $"집결지 {(RallyPoint.HasValue ? RallyPoint.Value.ToString() : "없음")}({RallyFloor}층), 도착 대기 {headingCount}명 [{heading.ToString().TrimEnd()}], 그 밖 {otherCount}명 [{other.ToString().TrimEnd()}]";
	}

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

	// 집결 대기 유닛이 자리에 도착할 때, 그리고 리더의 1초 주기(TryStartRally)마다 호출한다.
	// 아직 자리에 못 선 파티원이 있으면 유지, 전원 도착했으면 집결을 종료한다. rallyGatherNearbyEnabled가 켜져 있으면 도착한 파티원도 대기를 유지하므로(전원이 모일 때까지 기다림)
	// 여기서 도착 여부를 위치로 매번 다시 확인하고, 완료 시점에 대기를 일괄 해제한다. 꺼져 있으면 도착한 유닛이 스스로 대기를 비우는 예전 방식이다.
	public void CheckRallyComplete()
	{
		if (!IsRallyActive) return;
		bool gatherNearby = AIConfigLoader.Behavior?.rallyGatherNearbyEnabled ?? true;
		if (gatherNearby) ParkStragglersAfterTimeLimit();
		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0) continue;
			var wait = m.currentWait;
			if (wait == null) continue;
			if (wait.Reason == WaitReason.AwaitingPartyAtRallyPoint)
			{
				if (!gatherNearby) return;
				// 다른 층으로 건넌 유닛은 이 집결에서 빠진다(ExecuteWait과 같은 기준) — 전투 중이라 대기 처리가 못 돌아도 교착되지 않게 여기서도 제외한다.
				if (wait.WaitFloor >= 0 && m.currentFloor != wait.WaitFloor) continue;
				if (wait.IsParked) continue;
				if (!wait.WaitPosition.HasValue || !PartyFormationMath.IsAtSlot(m.position, wait.WaitPosition.Value)) return;
			}
			// ReportingCoreToLeader도 "집결 미완료"로 취급 — 코어 보고 중인 유닛이 보고를 마치기 전에
			// 나머지 인원만으로 집결이 먼저 끝나 ReadyToAdvance가 앞서 발생하는 것을 막는다. 단 갈 곳이 없어
			// 정박한(IsParked) 보고자는 붙잡지 않는다 — 리더를 못 찾는 유닛 하나가 집결을 교착시키면 안 된다.
			else if (wait.Reason == WaitReason.ReportingCoreToLeader && !wait.IsParked)
				return;
		}

		if (gatherNearby)
		{
			foreach (var m in Members)
			{
				if (m == null || m.hp <= 0 || m.currentWait == null || m.currentWait.Reason != WaitReason.AwaitingPartyAtRallyPoint) continue;
				m.currentWait = null;
				m.waitStuckTurns = 0;
			}
		}
		IsRallyActive = false;
		RallyPoint = null;
		ReadyToAdvance = true; // 05번 1장: 집결 완료 → 진형 유지해 다음 방으로 이동할 차례.
		AdvanceFromRoom = Leader?.currentRoom; // 리더가 이 방을 떠날 때까지 재집결 금지(TryStartRally)
		LogHelper.Log(LogHelper.GAME, $"{LogTag} 집결 완료 — 리더 {Leader?.name}이(가) 다음 방 이동을 결정(ReadyToAdvance), 이동 명령 발행 대기");
	}

	// 집결이 시작된 지 오래(doorApproachMaxBlockedSeconds × 2, 내부 판단 — 방 이동 단계 상한과 같은 기준)인데도 자리에 못 선 파티원은 그 위치에서 집결 처리한다 —
	// 개별 재선택·구역 내 정착으로도 풀리지 않는 정체(전투가 안 끝나는 유닛 등)가 파티 전체를 영구히 붙잡지 않게 하는 마지막 안전장치다.
	private void ParkStragglersAfterTimeLimit()
	{
		float limit = 2f * (AIConfigLoader.Behavior?.doorApproachMaxBlockedSeconds ?? 30f);
		if (Time.time - RallyStartTime < limit) return;
		System.Text.StringBuilder names = null;
		foreach (var m in Members)
		{
			var wait = m?.currentWait;
			if (m == null || m.hp <= 0 || wait == null || wait.Reason != WaitReason.AwaitingPartyAtRallyPoint || wait.IsParked) continue;
			if (wait.WaitFloor >= 0 && m.currentFloor != wait.WaitFloor) continue;
			if (wait.WaitPosition.HasValue && PartyFormationMath.IsAtSlot(m.position, wait.WaitPosition.Value)) continue;
			wait.IsParked = true;
			(names ??= new System.Text.StringBuilder()).Append(PartyAdvanceSystem.DescribeMember(m, wait.WaitPosition)).Append(' ');
		}
		if (names != null) LogRally($"집결 시작 {limit:F0}초 경과 — 자리에 못 선 [{names.ToString().TrimEnd()}]을(를) 현재 위치에서 집결 처리합니다");
	}

	// 03번 문서 3번 항목: 리더가 코어를 직접 확인했거나(discoverer == Leader) 보고받았을 때
	// PartyCoreReportSystem이 호출한다. 진행 중이거나 막 완료된 집결·다음 방 이동을 즉시 해제하고
	// 코어 처리로 전환한다 — 전달 범위는 TryStartRally와 동일하게 05번 문서 4장을 따른다(같은 방은
	// 무조건, 다른 방은 CanPropagate 게이트, 검증문서 01-02 6번).
	public void OnLeaderLearnsCore(Vector3Int corePos)
	{
		if (LeaderKnownCorePosition == corePos) return; // 이미 알고 있음 — 중복 처리 방지
		LeaderKnownCorePosition = corePos;

		// 검증문서 03-03(03번 문서 3장 138줄): 위 집결 해제(같은 방은 무조건)와 달리, 코어 위치·발견
		// 내용 자체는 일반 전파 조건(CanPropagate)만으로 공유한다 — 별도 예외 없음.
		if (Leader?.Session != null && Leader.Session.objectGrid.TryGetValue(corePos, out var core))
			PropagationSystem.NotifyCoreLocationKnown(Leader, core);

		IsRallyActive = false;
		RallyPoint = null;
		ReadyToAdvance = false;
		AdvanceFromRoom = null; // 코어 처리가 끝나면 같은 방에서도 다시 집결할 수 있어야 한다(05번 2장 "코어 처리 후 집결 판단")
		AdvanceAttempts = 0;
		AdvancePlan = null; // 문 앞 진형·돌파·입장 중이었다면 폐기 — 전파가 안 닿은 파티원의 대기는 각자 계획이 없음을 보고 스스로 푼다(PartyAdvanceSystem.StepMember)
		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0) continue;
			if (m.currentWait == null ||
				(m.currentWait.Reason != WaitReason.AwaitingPartyAtRallyPoint && m.currentWait.Reason != WaitReason.AdvancingToNextRoom
				 && m.currentWait.Reason != WaitReason.ApproachingNextDoor && m.currentWait.Reason != WaitReason.FormingUpAtDoor
				 && m.currentWait.Reason != WaitReason.BreachingDoor && m.currentWait.Reason != WaitReason.EnteringNextRoom))
				continue;
			if (m.currentRoom != Leader.currentRoom && !PropagationSystem.CanPropagate(Leader, m)) continue;
			m.currentWait = null;
			// currentWait을 지우는 다른 모든 지점과 동일하게 waitStuckTurns도 항상 같이 리셋한다 —
			// 빠뜨리면 다음 대기의 잔여 스턱 카운트가 유예 턴 수를 줄인다.
			m.waitStuckTurns = 0;
		}
	}
}
