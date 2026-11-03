using System.Collections.Generic;
using UnityEngine;

// 파티의 지휘 상태(리더 명령 설계명세서 v0.15) — 승계 대기 시계, 지휘 상실 이력. 명령·쿨타임 상태는 이후 단계에서 이 클래스에 더한다. 전이는 LeaderSuccessionSystem이 맡는다.
public sealed class PartyCommandState
{
	// ── 승계(17·17-1장) ──
	// 일반 리더를 잃고 후임 선정을 기다리는 중이다. 이 동안 Party.AssignLeaderIfNeeded는 아무것도 고르지 않는다(10초 뒤 LeaderSuccessionSystem이 고른다).
	public bool SuccessionPending;
	public LeaderLossReason PendingReason;
	public string LostLeaderName;
	// 10초 시계가 시작됐는가 — 사망은 실제 사망 시각에 바로 시작하고, 도주는 소속 생존 파티원이 도주 사실을 최초 확인한 시각에 시작한다(그 전엔 시작 안 함). 이후 다른 파티원의 뒤늦은 확인은 시계를 다시 시작하지 않는다.
	public bool SuccessionClockStarted;
	public float SuccessionStartedAt;

	// 개인 도주를 확정한 리더 이름 — 이 도주를 처음 안 파티원의 정신력 감소를 일반 −5가 아니라 리더 −10으로 정하는 데 쓴다(17-1-표1).
	public readonly HashSet<string> FledLeaderNames = new HashSet<string>();

	// 10초가 지나도 후임이 없어 리더 없이 행동하는 중(소속·임무는 유지).
	public bool NoSuccessor;
	public int SuccessionCount; // 이 파티가 지금까지 후임을 세운 횟수(진단)

	// ── 명령 상태(7·8·11장) ──
	// 이 파티 지휘자(일반 리더 역할)의 리더 명령 쿨타임이 끝나는 시각(Time.time). 리더가 사망·도주해도 흘러가고 후임은 남은 시간만 이어받는다(7-1장) — 리더 개인이 아니라 파티 쪽에 둔 이유다.
	public float CooldownUntil;
	public float NextEvaluateTime;
	public float NextBlockerCheckTime; // 차단물 공동 파괴 지정(12-3장) 확인 스로틀 — 경로 조회가 드는 판단이라 1초마다

	// 지금 유효한 집중 공격 지정(없으면 FocusCommandId가 null). 점수 변화만으로 끝내지 않으며, 대상 사망·교체·지휘 상실로 끝난다.
	public string FocusCommandId;
	public TacticalCommand FocusKind;
	public Unit FocusTarget;
	public Vector3Int? FocusObject;
	public float FocusScore;
	public bool HasFocus => !string.IsNullOrEmpty(FocusCommandId);

	// 교전 단위 기록: 전투 중인가(마지막으로 적을 본 시각에서 5초 유예 — 적이 시야에서 깜빡이는 것만으로 '새 교전'을 반복하지 않는다), 직전 제압 대상, 동점 무작위 순서.
	public bool InEngagement;
	public float LastCombatSeenAt;
	public Unit LastSuppressTarget;
	public readonly Dictionary<Unit, int> TieOrder = new Dictionary<Unit, int>();
	public readonly StrikeTriggerTracker Strike = new StrikeTriggerTracker();

	// 리더가 마지막으로 직접 확인한 파티원 위치 — 집결 기한 뒤 "마지막 위치를 아는 미집결자"만 수색하고 위치 정보가 없으면 제외하는 판단(5-2장)에 쓴다. 리더가 바뀌면 후임의 시야 기준으로 비운다.
	public readonly Dictionary<string, (Vector2Int position, int floor, float time)> LeaderLastSeen = new Dictionary<string, (Vector2Int, int, float)>();

	// 진단: 마지막으로 낸 명령.
	public TacticalCommand LastIssued;
	public float LastIssuedAt;
	public int IssuedCount;

	// 같은 비교 동안 유지되는 무작위 순서 — 적마다 처음 필요할 때 한 번 정한다(11장 "무작위로 순서를 한 번 정하고 같은 비교 동안 유지").
	public int TieRankOf(Unit enemy)
	{
		if (enemy == null) return int.MaxValue;
		if (!TieOrder.TryGetValue(enemy, out int rank)) { rank = UnityEngine.Random.Range(0, int.MaxValue); TieOrder[enemy] = rank; }
		return rank;
	}

	public void ClearFocus()
	{
		FocusCommandId = null;
		FocusKind = TacticalCommand.None;
		FocusTarget = null;
		FocusObject = null;
		FocusScore = 0f;
	}

	// 지휘 상실 — 그 지휘자의 지정·계기·직전 제압 기억을 끝낸다. 쿨타임(CooldownUntil)은 건드리지 않는다(후임이 남은 시간을 이어받는다).
	public void ClearOnLeaderLoss()
	{
		ClearFocus();
		Strike.CancelPending();
		LastSuppressTarget = null;
	}

	public void StartSuccession(LeaderLossReason reason, string lostLeaderName)
	{
		SuccessionPending = true;
		PendingReason = reason;
		LostLeaderName = lostLeaderName;
		SuccessionClockStarted = false;
		SuccessionStartedAt = 0f;
		NoSuccessor = false;
	}

	public void StartClock(float now)
	{
		if (SuccessionClockStarted) return; // 뒤늦은 확인은 시계를 다시 시작하지 않는다
		SuccessionClockStarted = true;
		SuccessionStartedAt = now;
	}

	public void FinishSuccession()
	{
		SuccessionPending = false;
		SuccessionClockStarted = false;
		LostLeaderName = null;
	}
}

public partial class Party
{
	// 인류 파티 컨셉(모험가·도적 등) — 임무(PartyType)와 다른 분류다. 기본 버프 종류·위협 배율·행동 우선 명령 순서가 이것으로 정해지며 후임 리더에게도 유지된다(8·14장). GameSession.CreateParty가 정한다(파티 구성 문서 대기 — 지금은 모험가·도적만 무작위).
	public PartyConcept Concept;

	public readonly PartyCommandState Command = new PartyCommandState();

	// ─────────────────────────── 집결 기한 규칙 (리더 명령 5-2장) ───────────────────────────
	// 기본 대기 기한은 집결 명령 후 8초다. 아직 합류하지 않은 대원의 도착 예정 정보를 받으면 max(기본 기한, 가장 늦은 도착 예정+3초)까지 기다리고, 같은 정보를 다시 받아도 기한을 새로 시작하지 않는다.
	// 전원이 먼저 합류하면 즉시 완료한다. 기한 뒤 마지막 위치를 아는 미집결자는 5초 수색하고, 위치 정보가 없으면 미확인 상태로 이번 대기에서 제외한다. 단순 부재를 사망·도주로 확정하지 않는다.
	private float _rallyLatestEta = float.NegativeInfinity;
	private readonly HashSet<string> _rallyEtaReported = new HashSet<string>();
	private bool _rallySearchStarted;
	private float _rallySearchStartedAt;
	private readonly List<string> _rallySearching = new List<string>();

	private void ResetRallyRules()
	{
		_rallyLatestEta = float.NegativeInfinity;
		_rallyEtaReported.Clear();
		_rallySearchStarted = false;
		_rallySearching.Clear();
	}

	// 집결 완료 확인(CheckRallyComplete)마다 부른다. 남은 미합류자를 이번 대기에서 제외하고 집결을 끝내야 하면 true(강제 완료)를 돌려준다.
	private bool ApplyRallyRules()
	{
		if (!LeaderCommandSettings.RallyRulesEnabled || Leader == null) return false;
		float now = Time.time;

		// 도착 예정 정보: 집결 명령을 받고 걸어오는 미합류자가 처음 정보를 줄 때 한 번만 반영한다(재수신은 기한을 미루지 않는다).
		foreach (var m in Members)
		{
			var wait = m?.currentWait;
			if (m == null || m.hp <= 0f || m == Leader || wait == null || wait.Reason != WaitReason.AwaitingPartyAtRallyPoint || wait.IsParked) continue;
			if (!wait.WaitPosition.HasValue || IsRallyArrived(m, wait.WaitPosition.Value) || !_rallyEtaReported.Add(m.name)) continue;
			float speed = Mathf.Max(0.1f, m.AppliedWalkSpeed);
			float eta = now + AIMovementHelper.ChebyshevDistance(m.position, wait.WaitPosition.Value) / speed;
			_rallyLatestEta = Mathf.Max(_rallyLatestEta, eta);
		}

		float deadline = LeaderMath.RallyDeadline(RallyStartTime, _rallyLatestEta); // RallyStartTime은 교전·경계 정지로 밀린 값이라 교전 중에는 기한이 같이 밀린다
		if (now < deadline) return false;

		if (!_rallySearchStarted)
		{
			_rallySearchStarted = true;
			_rallySearchStartedAt = now;
			_rallySearching.Clear();
			var excluded = new List<string>();
			foreach (var m in Members)
			{
				if (m == null || m.hp <= 0f || m == Leader || IsRallyMemberJoined(m)) continue;
				if (Command.LeaderLastSeen.ContainsKey(m.name)) _rallySearching.Add(m.name);
				else { excluded.Add(m.name); ExcludeFromRally(m); } // 위치 정보가 없으면 미확인 상태로 이번 대기에서 제외(사망·도주로 확정하지 않는다)
			}
			if (excluded.Count > 0) LogRally($"집결 기한({deadline - RallyStartTime:F0}초) 경과 — 마지막 위치 정보가 없는 미합류자 [{string.Join(", ", excluded)}]는 미확인 상태로 이번 대기에서 제외합니다");
			if (_rallySearching.Count > 0)
			{
				LogRally($"집결 기한({deadline - RallyStartTime:F0}초) 경과 — 마지막 위치를 아는 미합류자 [{string.Join(", ", _rallySearching)}]를 {LeaderMath.RallySearchSeconds:F0}초 수색합니다");
				SpeechSystem.Say(Leader, SpeechId.AbsentMember, _rallySearching[0]);
			}
		}

		if (_rallySearching.Count == 0 || now - _rallySearchStartedAt >= LeaderMath.RallySearchSeconds)
		{
			if (_rallySearching.Count > 0)
				LogRally($"수색 {LeaderMath.RallySearchSeconds:F0}초 종료 — 끝내 합류하지 못한 [{string.Join(", ", _rallySearching)}]를 미확인 상태로 이번 대기에서 제외합니다");
			foreach (var m in Members)
				if (m != null && m.hp > 0f && m != Leader && !IsRallyMemberJoined(m)) ExcludeFromRally(m);
			GiveUpAbsentees(now - RallyStartTime); // 명령을 못 받은 미집결자·리더가 모르는 사망도 이번 대기에서 제외한다(단순 부재를 사망·도주로 확정하지는 않는다)
			return true;
		}
		return false;
	}

	// 합류했는가: 집결 자리에 도착했거나 다른 층·정박 등으로 이미 이번 집결에서 빠졌다.
	private bool IsRallyMemberJoined(Human m)
	{
		var wait = m.currentWait;
		if (wait == null || wait.Reason != WaitReason.AwaitingPartyAtRallyPoint)
			return _rallyRecipients.Contains(m) || _givenUpAbsent.Contains(m.name) || RetreatSystem.IsFleeKnownToLeader(m, Leader); // 대기가 없는 수신자(다른 대기 중)는 명령을 받은 것이다
		if (wait.IsParked) return true;
		if (wait.WaitFloor >= 0 && m.currentFloor != wait.WaitFloor) return true;
		return wait.WaitPosition.HasValue && IsRallyArrived(m, wait.WaitPosition.Value);
	}

	private void ExcludeFromRally(Human m)
	{
		var wait = m.currentWait;
		if (wait != null && wait.Reason == WaitReason.AwaitingPartyAtRallyPoint) wait.IsParked = true; // 현재 위치에서 인정
		else _givenUpAbsent.Add(m.name);
	}

	// 지휘 상실(사망·도주 확정) 즉시 끝나는 리더 주도 활동 — 집결·방 이동 계획·진형 대기·문 앞 접근(17-표1). 후퇴 종료는 PartyRetreatSystem이 따로 한다.
	// 소속·임무와 개인의 정보 교환·문 개방·파괴·이동·전투는 유지하므로 개인 대기만 접고 계획은 폐기한다.
	public void EndLeaderDrivenActivities()
	{
		IsRallyActive = false;
		RallyPoint = null;
		_rallyRecipients.Clear();
		_pendingRallyRelease.Clear();
		ReadyToAdvance = false;
		AdvanceFromRoom = null;
		AdvanceAttempts = 0;
		PartyAdvanceSystem.Abort(this, "지휘 상실", retry: false);
		AdvancePlan = null;
		DoorApproachRoom = null;

		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0) continue;
			var wait = m.currentWait;
			if (wait != null && (IsRallyOrAdvanceWait(wait) || wait.Reason == WaitReason.SearchingNextDoor))
			{
				m.currentWait = null;
				m.waitStuckTurns = 0;
			}
			m.currentFormation = null; // 자동 보호 포메이션도 리더 주도라 함께 끝난다(10-표1)
		}
	}
}
