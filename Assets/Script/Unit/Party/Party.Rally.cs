using System.Collections.Generic;
using UnityEngine;
using GrimArchive.Wave;
using Haare.Util.Logger;

// 파티의 집결·방 이동 상태 — 집결 시작/완료(TryStartRally·CheckRallyComplete), 집결 뒤 진형 계획(PartyAdvancePlan)과의 접점, 막힘 진단 로그. 방 활동 종료 판정·리더·집계는 Party.cs.
public partial class Party
{
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
	// 교전·경계 중 집결 시간 제한 정지(PartyEngagement) — 마지막 시계 갱신 시각과 이번 집결에서 멈춰 준 누적 시간, 정지 시작 로그를 남겼는지.
	private float _lastRallyClockTime;
	private float _rallyPausedSeconds;
	private bool _rallyPauseLogged;

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
	// 지금 방에서 방 이동 계획을 시작한 횟수(PartyAdvanceSystem.Begin이 올린다) — 비정상 중단의 무한 재시작을 막는 안전한도(PartyAdvanceSystem.MaxAttempts)에 쓴다. 리더가 이 방을 떠나면(TickAdvanceState) 0으로 돌아간다.
	public int AdvanceAttempts;
	// 방 이동 시도를 한도까지 소진해 포기한 뒤 재집결을 미루는 시각(Time.time 기준) — GiveUpAdvance가 정하고 TryStartRally가 읽는다.
	public float RallyBlockedUntil;

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

		if (Time.time < RallyBlockedUntil) // 방 이동을 한도까지 시도하고 포기한 직후 — 쿨다운이 지나야 처음부터 다시 집결한다
		{
			if (ShouldReportRally(RallyReport.AdvanceCooldown, 0)) LogRally($"집결 불가 — 문 파괴 실패 후 쿨다운({RallyBlockedUntil - Time.time:F0}초 남음)");
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
		_lastRallyClockTime = Time.time;
		_rallyPausedSeconds = 0f;
		_rallyPauseLogged = false;
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

	// 방 이동을 시도 한도(PartyAdvanceSystem.MaxAttempts)까지 해도 문을 못 뚫어 포기한다 — 집결을 마친 방의 재집결 잠금(AdvanceFromRoom)은 리더가 방을 떠나야만 풀려
	// 그냥 두면 영구 정지하므로 잠금·시도 횟수를 풀고, 쿨다운 뒤 집결부터 처음부터 다시 시도하게 한다(PartyAdvanceSystem.Abort가 호출).
	public void GiveUpAdvance(float cooldownSeconds)
	{
		AdvanceFromRoom = null;
		ReadyToAdvance = false;
		AdvanceAttempts = 0;
		RallyBlockedUntil = Time.time + cooldownSeconds;
	}

	// ── 진단 로그: 집결이 시작되지 못하는 사유 / 진행 중인 집결의 도착 상황 ──────────────────────────────────────────
	// TryStartRally는 1초 주기로 불리고 막히면 조용히 return하므로 "왜 집결을 안 하는지"가 로그에 안 남았다. 사유(종류+구분값)가 바뀔 때, 그리고 같은 사유가 이어질 때는
	// RallyReportRepeatSeconds마다 상태를 갱신해 한 줄만 남긴다. 동작에는 영향이 없고, 문구는 사유를 만들 때만 조립한다(막힘 없는 경로엔 할당 없음).
	private enum RallyReport
	{
		None, Active, EntranceSequence, InCombat, PostCombatSweep, NoLeader, LeaderOnStairs, AdvanceFromRoom, HostileCore, RoomActivityIncomplete, AdvancePlanActive, AdvanceCooldown,
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
				// 도착하지 못한 파티원은 FSM 상태·경계 종류·보이는 적·자리까지 거리를 함께 남긴다(PartyDiagnostics.DescribeMember) — 왜 못 서는지 가리기 위한 진단.
				if (atSlot) heading.Append(m.name).Append("[도착] ");
				else heading.Append(PartyDiagnostics.DescribeMember(m, wait.WaitPosition)).Append(' ');
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
		ApplyRallyEngagementPause();
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
			(names ??= new System.Text.StringBuilder()).Append(PartyDiagnostics.DescribeMember(m, wait.WaitPosition)).Append(' ');
		}
		if (names != null) LogRally($"집결 시작 {limit:F0}초 경과 — 자리에 못 선 [{names.ToString().TrimEnd()}]을(를) 현재 위치에서 집결 처리합니다");
	}

	// 집결 중 교전·경계가 있으면 그 경과 시간만큼 집결 시작 시각을 뒤로 밀어 60초 상한에 세지 않는다(03번 13항: 전투 후 10초 경계를 마친 뒤 기존 집결을 재개) — 집결 대기 중인 파티원 중 한 명이라도 교전·경계일 때.
	// 상한은 PartyEngagement.PauseCap — 풀리지 않는 교전이 집결을 영구히 붙잡지 않게 한다. CheckRallyComplete가 1초 주기와 도착 시점에 불러 주므로 dt는 마지막 호출 이후의 경과다.
	private void ApplyRallyEngagementPause()
	{
		float now = Time.time;
		float dt = now - _lastRallyClockTime;
		_lastRallyClockTime = now;
		if (dt <= 0f) return;

		bool engaged = false;
		foreach (var m in Members)
		{
			var wait = m?.currentWait;
			if (m == null || m.hp <= 0 || wait == null || wait.Reason != WaitReason.AwaitingPartyAtRallyPoint) continue;
			if (PartyEngagement.IsEngaged(m)) { engaged = true; break; }
		}
		if (!engaged) return;

		float cap = PartyEngagement.PauseCap;
		float credit = PartyFormationMath.PauseCredit(dt, _rallyPausedSeconds, cap);
		if (credit <= 0f)
		{
			if (_rallyPauseLogged)
			{
				_rallyPauseLogged = false;
				LogRally($"교전·경계 정지 시간이 상한({cap:F0}초)에 닿아 집결 시간 제한을 다시 셉니다");
			}
			return;
		}

		bool first = _rallyPausedSeconds <= 0f;
		_rallyPausedSeconds += credit;
		RallyStartTime += credit;
		if (first)
		{
			_rallyPauseLogged = true;
			LogRally($"집결 중 교전·경계가 있어 시간 제한을 멈춥니다(상한 {cap:F0}초)");
		}
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
		AdvancePlan = null; // 문 앞 진형·돌파·입장 중이었다면 폐기 — 전파가 안 닿은 파티원의 대기는 각자 계획이 없음을 보고 스스로 푼다(PartyAdvanceSteps.StepMember)
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
