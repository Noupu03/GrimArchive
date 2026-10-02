using System.Collections.Generic;
using UnityEngine;
using GrimArchive.Wave;
using Haare.Util.Logger;

// 파티의 집결·방 이동 상태 — 집결 시작/완료, 진형 계획(PartyAdvancePlan)과의 접점, 막힘 진단 로그. 방 활동 종료 판정·리더·집계는 Party.cs.
public partial class Party
{
	// 집결의 최소 버전 — 전투 종료 후 경계 10초가 끝나면 리더가 자기 위치를 집결지로 지정한다. 전달 범위는 05번 4장(TryStartRally 참고), TacticalFSMState.ExecuteWait이 읽는다.
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
	// 집결이 막 완료돼 다음 방으로 함께 이동해도 되는 상태 — HumanWaveManager가 보고 공동 이동 명령을 1회 발행한 뒤 false로 되돌린다(05번 1장).
	public bool ReadyToAdvance;
	// 마지막으로 집결을 마친 방 — 리더가 이 방을 떠나기 전엔 TryStartRally가 같은 방에서 다시 집결하지 않는다(이동 중 재집결하면 IsRallyActive가 남거나 문 앞 집결이 반복된다). 리더가 나가거나 코어 처리로 해제되면 비운다.
	public Room AdvanceFromRoom { get; private set; }

	// 집결 뒤 진형 → 문 파괴 → 순차 입장 계획 — PartyAdvanceSystem이 만들고 전이시키며 끝나면 null. 있는 동안은 새 집결을 시작하지 않는다.
	public PartyAdvancePlan AdvancePlan;
	// 지금 방에서 방 이동 계획을 시작한 횟수(PartyAdvanceSystem.Begin) — 비정상 중단의 무한 재시작을 막는 안전한도(MaxAttempts)에 쓰며 리더가 방을 떠나면 0으로 돌아간다.
	public int AdvanceAttempts;
	// 방 이동 시도를 한도까지 소진해 포기한 뒤 재집결을 미루는 시각(Time.time 기준) — GiveUpAdvance가 정하고 TryStartRally가 읽는다.
	public float RallyBlockedUntil;

	// 이 파티가 웨이브 공통 몬스터 처치 수량 달성을 집결 트리거로 이미 썼는지 — 파티별로 들고 있어야 서로 간섭하지 않는다.
	private bool _monsterKillQuotaConsumed;

	// 코어 때문에 진행 중이던 집결·방 이동을 해제했다 — 코어 처리 뒤 활동 종료 기준 없이 다시 집결한다(05번 2장). OnLeaderLearnsCore가 켜고 TryStartRally가 끈다.
	private bool _resumeRallyAfterCore;
	public bool ResumeRallyAfterCore => _resumeRallyAfterCore;

	// 집결 대상은 생존 파티원 전원 — 명령을 못 받은 생존자와 리더가 모르는 사망(PartyDeathRecord.InfoKnownUnits)은 미집결자로 기다린다(05번 5장).
	// 수색·포기 뒤 판정은 후속 문서 몫이라 상한은 기존 집결 60초 근사(ParkStragglersAfterTimeLimit)이고, 포기한 미집결자(_givenUpAbsent)는 다시 기다리지 않는다.
	private readonly HashSet<Human> _rallyRecipients = new HashSet<Human>();
	private readonly HashSet<string> _givenUpAbsent = new HashSet<string>();

	// 집결 완료를 막는 미집결자 수(목록을 주면 이름도 채운다) — 명령을 받지 못한 생존 구성원(alive) + 리더가 아직 모르는 사망(dead), 포기한 것은 제외.
	private int CollectUnaccountedAbsentees(List<string> alive = null, List<string> dead = null)
	{
		if (!(AIConfigLoader.Behavior?.rallyAbsenteeWaitEnabled ?? true) || Leader == null) return 0;
		int count = 0;
		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0 || m == Leader) continue;
			if (_rallyRecipients.Contains(m) || _givenUpAbsent.Contains(m.name)) continue;
			count++;
			alive?.Add(m.name);
		}
		foreach (var record in DeathRecords.Values)
		{
			if (record.InfoKnownUnits.Contains(Leader.name) || _givenUpAbsent.Contains(record.DeadUnitName)) continue;
			count++;
			dead?.Add(record.DeadUnitName);
		}
		return count;
	}

	private static string FormatAbsentees(List<string> alive, List<string> dead)
	{
		var names = new List<string>(alive);
		foreach (var n in dead) names.Add(n + "(사망 미확인)");
		return string.Join(", ", names);
	}

	// 기다림의 상한에 닿았다 — 남은 미집결자를 포기한다(수색은 후속 문서 몫이라 기다림만 끝내고, 진행·후퇴 판정은 하지 않는다).
	private void GiveUpAbsentees(float limit)
	{
		var alive = new List<string>();
		var dead = new List<string>();
		if (CollectUnaccountedAbsentees(alive, dead) == 0) return;
		_givenUpAbsent.UnionWith(alive);
		_givenUpAbsent.UnionWith(dead);
		LogRally($"집결 시작 {limit:F0}초 경과 — 미집결자 [{FormatAbsentees(alive, dead)}]를 더는 기다리지 않습니다(명령을 받게 되거나 사망을 알게 되기 전까지 다음 집결에서도 제외)");
	}

	// 리더 전용 주기 체크 — 전투 없이도 방 활동이 끝나면 집결할 수 있어야 하므로(01번 7-1장) 매 프레임 대신 이 타이머로 던진다.
	private float _nextRoomActivityCheckTime;
	public void TickRoomActivityCheck(float currentTime)
	{
		if (currentTime < _nextRoomActivityCheckTime) return;
		_nextRoomActivityCheckTime = currentTime + 1f;
		// 입구 시퀀스·리더의 층 이동 중엔 방 활동이 없어 TryStartRally가 어차피 거절하므로 호출을 건너뛴다. 진행 중 집결의 완료 확인은 막지 않는다.
		if (!IsRallyActive && (AnyMemberInEntranceSequence() || (Leader != null && Leader.pendingStairTargetFloor.HasValue))) return;
		TryStartRally();
	}

	// 아직 던전(계단)에 다 들어오지 못한 생존 파티원이 있는가 — 웨이브 시작 전 대기 중에는 집결하지 않는다.
	private bool AnyMemberInEntranceSequence()
	{
		foreach (var m in Members)
		{
			if (m != null && m.hp > 0 && m.isInDungeonEntranceSequence) return true;
		}
		return false;
	}

	// 전투 후 경계 스윕이 끝난 유닛이 호출한다 — 파티 전체가 전투에서 벗어났을 때만 집결을 시작하고, 남은 파티원이 있으면 그 유닛이 끝날 때 재시도된다.
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
			if (m.isInDungeonEntranceSequence) // 아직 던전(계단)에 다 못 들어온 파티원은 집결하지 않는다.
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

		// 집결을 마친 방을 리더가 아직 안 떠났다 — 공동 이동 중이라 같은 방에서 다시 집결하지 않는다(05번 1·2장). 임무 수량 달성 신호는 아래에서 소비하므로 여기서 막혀도 이어진다.
		TickAdvanceState();
		if (AdvanceFromRoom != null)
		{
			if (ShouldReportRally(RallyReport.AdvanceFromRoom, AdvanceFromRoom.RoomId))
				LogRally($"집결 불가 — 집결을 마친 방 {AdvanceFromRoom.RoomId}을(를) 리더 {Leader.name}이(가) 아직 떠나지 않음(다음 방 이동 중: ReadyToAdvance={ReadyToAdvance}, 리더 현재 방 {Leader.currentRoom?.RoomId.ToString() ?? "없음"})");
			return;
		}

		// 리더가 미처리 코어를 알면 집결을 명령하지 않는다(03번 3항). 코어가 처리돼 더는 적대적이지 않으면 여기서 감지해 지운다.
		if (LeaderKnownCorePosition.HasValue)
		{
			if (TacticalFSMState.IsRoomCoreStillHostile(Leader, LeaderKnownCorePosition.Value))
			{
				if (ShouldReportRally(RallyReport.HostileCore, 0)) LogRally($"집결 불가 — 리더가 미처리 코어 {LeaderKnownCorePosition.Value}를 알고 있어 코어 처리가 우선");
				return;
			}
			LeaderKnownCorePosition = null;
		}

		// 임무 수량 달성도 유효한 집결 사유다(01번 8장) — IsRoomActivityComplete와 별개라 둘 중 하나만 참이어도 집결한다. 몬스터 처치는 웨이브 공통 집계라 파티별 소비 여부를 _monsterKillQuotaConsumed로 따로 추적한다.
		bool monsterKillQuotaAvailable = !_monsterKillQuotaConsumed && HumanWaveManager.Instance != null &&
			HumanWaveManager.Instance.CommonMonsterKillCount >= PartyGoalMath.RequiredMonsterKillCount;
		bool questJustAchieved = JustReachedRoomExploreQuota || monsterKillQuotaAvailable;
		bool resumeAfterCore = _resumeRallyAfterCore; // 코어 때문에 해제했던 집결 — 코어 처리가 끝났으니 방 활동 종료 기준 없이 다시 집결한다

		// 전투/스윕이 끝나도 파티 종류 기준으로 이 방에서 할 일이 남았으면 집결하지 않는다(01번 7-1장). 임무 수량 달성 결과가 있으면 그것만으로 집결 판단으로 넘어간다.
		if (!questJustAchieved && !resumeAfterCore && !IsRoomActivityComplete())
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
		_resumeRallyAfterCore = false;

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
		LogHelper.Log(LogHelper.GAME, $"{LogTag} 집결 시작 — 리더 {Leader.name}, 사유: {(questJustAchieved ? "임무 수량 달성" : resumeAfterCore ? "코어 처리 후 재집결" : "방 활동 종료")}, 집결지 {RallyPoint.Value} ({RallyFloor}층)");

		// 전달 범위: 같은 방은 거리와 무관하게, 다른 방은 일반 전파 조건(PropagationSystem.CanPropagate)을 만족해야 한다(05번 4장).
		TickPendingRallyRelease(force: true); // 옛 집결의 해제를 못 받아 대기가 남은 유닛 중 지금 닿는 유닛은 먼저 푼다(아래 순회는 대기가 있는 유닛을 건너뛴다)
		var recipients = new List<Human>();
		_rallyRecipients.Clear();
		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0) continue;
			if (m.currentFloor != RallyFloor) continue; // 다른 층 파티원에게는 이 집결이 닿지 않는다
			if (!IsReachedByLeaderCommand(Leader, m)) continue;
			_rallyRecipients.Add(m); // 명령을 받았다 — 다른 대기(코어 보고 등) 중이라 집결 이동은 안 해도 미집결자가 아니다
			_givenUpAbsent.Remove(m.name);
			m.currentFormation = null; // 보호 포메이션 중 집결 명령을 받으면 종료하고 집결로 전환한다(안 지우면 대기가 풀린 뒤 호위가 재개된다, 05번 9장).
			// 명령이 전달된 파티원은 집결 위치와 리더 위치를 안다 — 다른 대기(코어 보고 이동 등) 중이라 집결 이동을 안 해도 정보는 받아 보고가 빈 리더 위치에 닿을 때 갈 곳이 된다.
			m.knownRallyPoint = RallyPoint;
			m.knownLeader.Update(Leader, RallyPoint.Value, Time.time);
			// 다음 문을 찾으며 리더를 따라다니던 공동 탐색 추종과, 임무 수량 달성으로 문 앞 도착 전에 집결이 시작된 리더의 문 접근은 집결로 전환한다.
			if (m.currentWait != null && m.currentWait.Reason != WaitReason.SearchingNextDoor && m.currentWait.Reason != WaitReason.ApproachingNextDoor) continue;
			// 전파받은 적 위치로 접근하던 경계도 집결 명령이 오면 접는다(03번 1장 50줄 — 개인 탐색으로 흩어지지 않음).
			if (m.currentAlertSearch != null && m.currentAlertSearch.IsIndirectEnemyApproach) m.currentAlertSearch = null;
			// 03번 0장·8장: 아직 시작하지 않은 함정 대응(응답 대기·담당자 도착 대기·해제하러 가는 이동)은 집결로 전환한다.
			TrapPartySystem.ReleaseForRally(m);
			// 시작 전 임무 대상으로 이동 중이던 조사도 집결로 전환한다(대상은 개인 지도에 남음). 이미 시작한 조사는 기존 조건으로 마친 뒤 합류한다(05번 4장).
			if (m.ReleaseUnstartedInvestigation()) LogRally($"{m.name}이(가) 집결 명령으로 대상을 향하던 조사를 접고 집결합니다(대상은 개인 지도에 남음)");
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

	// 집결 자리로 쓸 수 있는 구역인가 — 문 타일·문 앞 통과 구간 2칸(05번 3장)과 리더와 다른 방(문 반대편)은 제외한다.
	private static bool IsGatherArea(GameSession session, Room room, int floor, Vector2Int t)
	{
		if (session == null) return true;
		if (AIMovementHelper.IsWithinDoorClearance(session, floor, t)) return false;
		if (room == null || session.roomGrid == null) return true;
		return session.roomGrid.TryGetValue(new Vector3Int(t.x, t.y, floor), out var r) && r == room;
	}

	// 자리가 막혔을 때 재선택 — 집결지가 아니라 이 유닛에서 가장 가까운 구역 안 빈 타일을 고른다(좁은 길에서 양 끝을 오가지 않게). 후보: 다른 유닛이 서 있지 않고, 같은 방이며, 막혀 포기한 자리(rejected)·다른 파티원이 노리는 자리(claimed)가 아닌 타일. 없으면 false.
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
			return IsGatherArea(session, room, floor, t);
		}
		return PartyFormationMath.TryPickNearestFreeTile(member.position, IsFree, claimed, PartyFormationMath.DefaultSearchRadius, out slot);
	}

	// 집결지 주변에서 반경을 넓혀 가며 리더와 같은 방의 빈 통행 가능 타일을 고른다 — 문 타일·문 앞 통과 구간·다른 방은 제외, 못 찾으면 집결지 그대로. 재선택도 같은 함수를 쓴다.
	public Vector2Int PickGatherSlot(Human member, HashSet<Vector2Int> claimed)
	{
		var session = Leader?.Session;
		Room room = Leader?.currentRoom;
		int floor = RallyFloor;
		bool IsFree(Vector2Int t)
		{
			return member.CanMove(t, ignoreUnits: true) && IsGatherArea(session, room, floor, t);
		}
		return PartyFormationMath.TryPickNearestFreeTile(RallyPoint.Value, IsFree, claimed, PartyFormationMath.DefaultSearchRadius, out var slot) ? slot : RallyPoint.Value;
	}

	// 리더 명령(집결·해제·방 이동 지시)의 전달 범위(05번 4장) — 같은 방이면 거리와 무관하게, 다른 방이면 CanPropagate. 방 없는 유닛끼리는 같은 방이 아니다. 방 이동 지시는 규정이 없어 집결 명령과 같은 규칙을 쓴다.
	public static bool IsReachedByLeaderCommand(Human leader, Human m)
	{
		if (leader == null || m == leader) return true;
		if (PartyFormationMath.IsSameRoomForCommand(leader.currentRoom, m.currentRoom)) return true;
		return PropagationSystem.CanPropagate(leader, m);
	}

	// 집결 해제를 못 받은 구성원 — 해제 순간 다른 방이고 전파가 안 닿았으면 전달받기 전까지 기존 집결을 따르고(05번), 나중에 같은 방이 되거나 범위에 들어오면 전달한다. 그 사이 스스로 대기를 접고 새 집결 대기를 받은 유닛은 건드리지 않도록 대기 객체까지 기억한다.
	private readonly List<(Human member, WaitState wait)> _pendingRallyRelease = new List<(Human member, WaitState wait)>();
	private float _nextPendingReleaseCheck;

	// HumanWaveManager가 매 프레임 부른다(1초 자체 스로틀). 새 집결을 시작할 때는 force로 먼저 불러 도달 가능해진 유닛을 풀어 준다.
	public void TickPendingRallyRelease(bool force = false)
	{
		if (_pendingRallyRelease.Count == 0) return;
		float now = Time.time;
		if (!force && now < _nextPendingReleaseCheck) return;
		_nextPendingReleaseCheck = now + 1f;
		if (Leader == null || Leader.hp <= 0) return; // 리더가 재선정되면 그때 다시 판단한다

		for (int i = _pendingRallyRelease.Count - 1; i >= 0; i--)
		{
			var (m, wait) = _pendingRallyRelease[i];
			if (m == null || m.hp <= 0 || m.currentWait != wait) { _pendingRallyRelease.RemoveAt(i); continue; } // 이미 풀렸거나 다른 대기로 바뀜
			if (!IsReachedByLeaderCommand(Leader, m)) continue;
			m.currentWait = null;
			m.waitStuckTurns = 0;
			_pendingRallyRelease.RemoveAt(i);
			LogRally($"{m.name}이(가) 전파 범위에 들어와 집결 해제를 전달받아 기존 집결·이동을 접습니다");
		}
	}

	// 코어 처리로 해제되는 집결·방 이동 계열 대기인가.
	private static bool IsRallyOrAdvanceWait(WaitState w)
		=> w != null && (w.Reason == WaitReason.AwaitingPartyAtRallyPoint || w.Reason == WaitReason.AdvancingToNextRoom
			|| w.Reason == WaitReason.ApproachingNextDoor || w.Reason == WaitReason.FormingUpAtDoor
			|| w.Reason == WaitReason.BreachingDoor || w.Reason == WaitReason.EnteringNextRoom);

	// 집결을 마친 방을 리더가 떠나면 그 방의 '다음 방 이동' 의도와 재집결 억제를 함께 푼다 — 안 풀면 소비되지 않은 ReadyToAdvance가 남아 새 방의 문이 알려지는 즉시 활동을 건너뛰고 이동 명령이 나간다.
	public void TickAdvanceState()
	{
		// 문 앞 접근 표식은 그 방을 떠나면 의미가 없다 — 나중에 돌아왔을 때 옛 표식으로 곧바로 집결하지 않게 한다.
		if (DoorApproachRoom != null && Leader != null && Leader.currentRoom != DoorApproachRoom) DoorApproachRoom = null;
		if (AdvanceFromRoom == null || Leader == null) return;
		if (Leader.currentRoom == AdvanceFromRoom) return;
		// 이동 명령이 이미 소비됐으면 리더가 집결한 방을 떠났다는 사실만, 아직 안 발행됐으면 이동 의도가 소멸했다는 사실을 남긴다.
		LogHelper.Log(LogHelper.GAME, ReadyToAdvance
			? $"{LogTag} 리더 {Leader.name}이(가) 이동 명령 발행 전에 집결한 방을 떠남 — 방 이동 의도 해제"
			: $"{LogTag} 리더 {Leader.name}이(가) 집결한 방을 떠남 — 재집결 억제 해제");
		AdvanceFromRoom = null;
		ReadyToAdvance = false;
		AdvanceAttempts = 0;
	}

	// 방 이동이 시도 한도(MaxAttempts)까지 문을 못 뚫어 포기한다 — 재집결 잠금(AdvanceFromRoom)은 리더가 방을 떠나야 풀려 영구 정지하므로 잠금·시도 횟수를 풀고 쿨다운 뒤 집결부터 다시 시도한다.
	public void GiveUpAdvance(float cooldownSeconds)
	{
		AdvanceFromRoom = null;
		ReadyToAdvance = false;
		AdvanceAttempts = 0;
		RallyBlockedUntil = Time.time + cooldownSeconds;
	}

	// ── 진단 로그: 집결이 시작되지 못하는 사유 / 진행 중인 집결의 도착 상황 ──────────────────────────────────────────
	// TryStartRally는 막히면 조용히 return하므로 사유가 바뀔 때와 같은 사유가 이어질 때 RallyReportRepeatSeconds마다 한 줄만 남긴다(동작 무관, 문구는 사유를 만들 때만 조립).
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

	// 진행 중인 집결: 집결지로 가는 중인(AwaitingPartyAtRallyPoint) 파티원과 그 밖을 나눠 보여 준다 — '시작은 됐는데 아무도 안 모인다'를 가리는 용도.
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
				// 도착하지 못한 파티원은 FSM 상태·경계 종류·보이는 적·자리까지 거리를 함께 남긴다(PartyDiagnostics.DescribeMember).
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
		var absentAlive = new List<string>();
		var absentDead = new List<string>();
		int absentCount = CollectUnaccountedAbsentees(absentAlive, absentDead);
		string absent = absentCount > 0 ? $", 미집결자 {absentCount}명 [{FormatAbsentees(absentAlive, absentDead)}]" : "";
		return $"집결지 {(RallyPoint.HasValue ? RallyPoint.Value.ToString() : "없음")}({RallyFloor}층), 도착 대기 {headingCount}명 [{heading.ToString().TrimEnd()}], 그 밖 {otherCount}명 [{other.ToString().TrimEnd()}]{absent}";
	}

	// 집결 대기 유닛이 자리에 도착할 때와 리더의 1초 주기마다 호출한다 — 전원 도착이면 집결을 종료한다. rallyGatherNearbyEnabled가 켜져 있으면 도착해도 대기를 유지하므로 위치로 매번 도착을 확인하고 완료 시 대기를 일괄 해제한다(꺼져 있으면 도착한 유닛이 스스로 비우는 예전 방식).
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
			// ReportingCoreToLeader도 집결 미완료로 본다 — 코어 보고 중인 유닛을 두고 집결이 끝나 ReadyToAdvance가 앞서 발생하는 것을 막는다. 단 갈 곳이 없어 정박한(IsParked) 보고자는 붙잡지 않는다.
			else if (wait.Reason == WaitReason.ReportingCoreToLeader && !wait.IsParked)
				return;
		}

		if (gatherNearby && CollectUnaccountedAbsentees() > 0) return; // 05번 5장: 못 받은 생존 구성원·모르는 사망은 단순 부재만으로 제외하지 않고 기다린다(상한까지)

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

	// 집결 시작 후 오래(doorApproachMaxBlockedSeconds × 2) 자리에 못 선 파티원은 그 위치에서 집결 처리한다 — 개별 재선택으로도 안 풀리는 정체가 파티 전체를 영구히 붙잡지 않게 하는 마지막 안전장치.
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
		GiveUpAbsentees(limit);
	}

	// 집결 중 교전·경계가 있으면 그 경과만큼 시작 시각을 뒤로 밀어 60초 상한에 세지 않는다(03번 13항) — 대기 중 파티원 한 명이라도 교전·경계일 때. 상한은 PartyEngagement.PauseCap이며 dt는 마지막 호출 이후 경과다.
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

	// 리더가 코어를 직접 확인했거나 보고받으면 PartyCoreReportSystem이 호출한다 — 진행 중이거나 막 완료된 집결·다음 방 이동을 즉시 해제하고 코어 처리로 전환한다(03번 3항). 전달 범위는 TryStartRally와 같다(05번 4장).
	public void OnLeaderLearnsCore(Vector3Int corePos)
	{
		if (LeaderKnownCorePosition == corePos) return; // 이미 알고 있음 — 중복 처리 방지
		LeaderKnownCorePosition = corePos;

		// 코어 위치·발견 내용은 위 집결 해제(같은 방은 무조건)와 달리 일반 전파 조건(CanPropagate)만으로 공유한다(03번 3장).
		if (Leader?.Session != null && Leader.Session.objectGrid.TryGetValue(corePos, out var core))
			PropagationSystem.NotifyCoreLocationKnown(Leader, core);

		// 진행 중이던(집결·이동 대기·진형·돌파·입장) 집결을 코어 때문에 해제하면 처리 뒤 다시 집결하도록 기억한다. 집결이 없었다면 탐색 중 코어 처리라 기존 활동 종료 기준을 따른다.
		if (IsRallyActive || ReadyToAdvance || AdvanceFromRoom != null || AdvancePlan != null) _resumeRallyAfterCore = true;
		IsRallyActive = false;
		RallyPoint = null;
		ReadyToAdvance = false;
		AdvanceFromRoom = null; // 코어 처리가 끝나면 같은 방에서도 다시 집결할 수 있어야 한다(05번 2장 "코어 처리 후 집결 판단")
		AdvanceAttempts = 0;
		AdvancePlan = null; // 진행 중이던 진형·돌파·입장 계획 폐기 — 전파가 안 닿은 파티원은 각자 계획이 없음을 보고 스스로 푼다(PartyAdvanceSteps.StepMember)
		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0) continue;
			if (!IsRallyOrAdvanceWait(m.currentWait)) continue;
			if (!IsReachedByLeaderCommand(Leader, m))
			{
				// 해제가 닿지 않은 유닛은 기존 집결을 따르다가, 나중에 닿을 때 TickPendingRallyRelease가 해제를 전달한다.
				_pendingRallyRelease.RemoveAll(p => p.member == m);
				_pendingRallyRelease.Add((m, m.currentWait));
				continue;
			}
			m.currentWait = null;
			// currentWait을 지우는 다른 모든 지점과 동일하게 waitStuckTurns도 항상 같이 리셋한다 —
			// 빠뜨리면 다음 대기의 잔여 스턱 카운트가 유예 턴 수를 줄인다.
			m.waitStuckTurns = 0;
		}
	}
}
