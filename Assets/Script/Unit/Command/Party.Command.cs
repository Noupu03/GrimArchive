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
	private float _rallySearchStartedAt; // 관찰 시작 시각(도착 후 5초의 기준)
	private readonly List<string> _rallySearching = new List<string>();

	// 미집결자 수색(5-2장·파티 구성 8-1장·순서도 P02): 기한 뒤 마지막 위치를 아는 미집결자 중 집결 명령을 못 받은 쪽의 우선 위치로 리더와 집결 지점에 모인 대원이 함께 나가(각자 그 위치를 볼 수 있는 서로 다른 칸, 시체 점유 칸 제외) 전원 도착 후 5초 방향을 나눠 관찰하고, 시야로 다시 보면 집결 명령을 전달해 합류시키며, 못 찾으면 미확인 상태로 제외한 뒤 모두 집결지로 돌아온다.
	// 우선 위치 = 알려진 경로 기준 도착 예상시간이 짧은 위치(동률이면 최근 확인, 8-1장 2번). 문서에 없어 정한 내부 판단: 이동 상한 20초, 명령을 받고 걸어오는 사람·다른 대기를 하는 사람은 따라나서지 않고 하던 일을 계속하며 이동 중이던 사람에겐 예전처럼 5초 창만 준다.
	// 모인 인원이 함께 가는 것은 LeaderCommandSettings.RallySearchGroupMoveEnabled로 끌 수 있다(끄면 리더만 나서는 예전 동작).
	private enum RallySearchPhase { None, Travel, Observe, Finished }
	private RallySearchPhase _searchPhase;
	private float _searchBeganAt;            // 수색을 시작한(기한이 지난) 시각 — 이 뒤에 리더가 본 기록을 발견으로 센다
	private float _searchTravelStartedAt;
	private bool _searchMoved;               // 리더가 실제로 나갔는가 — 끝난 뒤 집결지 복귀를 기다려야 한다
	private readonly HashSet<string> _foundBySearch = new HashSet<string>();
	// 리더와 함께 수색에 나선 대원(파티 구성 8-1장 3번 "모인 인원이 함께 이동") — 수색이 끝나면 집결 자리로 돌려보낸다. 리더는 포함하지 않는다.
	private readonly List<Human> _searchers = new List<Human>();

	// 테스트가 시계를 주입한다(배치 모드의 Time.time은 흐르지 않는다).
	internal static System.Func<float> RallyClock = () => Time.time;

	private void ResetRallyRules()
	{
		_rallyLatestEta = float.NegativeInfinity;
		_rallyEtaReported.Clear();
		_rallySearchStarted = false;
		_rallySearching.Clear();
		_searchPhase = RallySearchPhase.None;
		_searchMoved = false;
		_foundBySearch.Clear();
		_searchers.Clear();
	}

	// 집결 완료 확인(CheckRallyComplete)마다 부른다. 남은 미합류자를 이번 대기에서 제외하고 집결을 끝내야 하면 true(강제 완료)를 돌려준다.
	private bool ApplyRallyRules()
	{
		if (!LeaderCommandSettings.RallyRulesEnabled || Leader == null) return false;
		float now = RallyClock();

		// 도착 예정 정보: 집결 명령을 받고 걸어오는 미합류자가 처음 정보를 줄 때 한 번만 반영한다(재수신은 기한을 미루지 않는다).
		foreach (var m in Members)
		{
			var wait = m?.currentWait;
			if (m == null || m.hp <= 0f || m == Leader || wait == null || wait.Reason != WaitReason.AwaitingPartyAtRallyPoint || wait.IsParked) continue;
			if (!wait.WaitPosition.HasValue || IsRallyArrived(m, wait.WaitPosition.Value) || !_rallyEtaReported.Add(m.name)) continue;
			float eta = LeaderMath.RallyEta(now, EstimateRallyWalkSteps(m, wait.WaitPosition.Value, wait.WaitFloor), m.AppliedWalkSpeed);
			_rallyLatestEta = Mathf.Max(_rallyLatestEta, eta);
		}

		float deadline = LeaderMath.RallyDeadline(RallyStartTime, _rallyLatestEta); // RallyStartTime은 교전·경계 정지로 밀린 값이라 교전 중에는 기한이 같이 밀린다
		if (now < deadline) return false;

		if (_searchPhase == RallySearchPhase.Finished) return false; // 수색을 마쳤다 — 이후는 일반 도착 판정과 상한이 집결을 끝낸다

		if (!_rallySearchStarted)
		{
			_rallySearchStarted = true;
			_searchBeganAt = now;
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
				LogRally($"집결 기한({deadline - RallyStartTime:F0}초) 경과 — 마지막 위치를 아는 미합류자 [{string.Join(", ", _rallySearching)}]를 수색합니다(위치 도착 후 {LeaderMath.RallySearchSeconds:F0}초 관찰)");
				SpeechSystem.Say(Leader, SpeechId.AbsentMember, _rallySearching[0]);
				BeginRallySearch(now);
			}
		}

		if (_searchPhase == RallySearchPhase.None && _rallySearching.Count > 0) { _searchPhase = RallySearchPhase.Observe; _rallySearchStartedAt = now; } // 이동할 곳이 없다(걸어오는 중이거나 이미 곁) — 선 자리에서 관찰
		if (_searchPhase == RallySearchPhase.Travel || _searchPhase == RallySearchPhase.Observe) ResolveFoundBySearch();
		if (_searchPhase == RallySearchPhase.Travel && _rallySearching.Count > 0)
		{
			if (!RallySearchTravelDone(now)) return false; // 아직 가는 중 — 관찰 5초는 도착한 뒤부터 센다
			_searchPhase = RallySearchPhase.Observe;
			_rallySearchStartedAt = now;
			LogRally($"수색 위치에 도착(또는 더 못 감) — {LeaderMath.RallySearchSeconds:F0}초 관찰합니다");
		}

		bool windowOver = _rallySearching.Count == 0 || (_searchPhase == RallySearchPhase.Observe && now - _rallySearchStartedAt >= LeaderMath.RallySearchSeconds);
		if (!windowOver) return false;

		if (_rallySearching.Count > 0)
			LogRally($"수색 {LeaderMath.RallySearchSeconds:F0}초 종료 — 끝내 찾지 못한 [{string.Join(", ", _rallySearching)}]를 미확인 상태로 이번 대기에서 제외합니다");
		foreach (var m in Members) // 수색으로 찾아 집결 명령을 받은 사람은 제외하지 않는다 — 집결지로 걸어오게 둔다
			if (m != null && m.hp > 0f && m != Leader && !IsRallyMemberJoined(m) && !_foundBySearch.Contains(m.name)) ExcludeFromRally(m);
		GiveUpAbsentees(now - RallyStartTime); // 명령을 못 받은 미집결자·리더가 모르는 사망도 이번 대기에서 제외한다(단순 부재를 사망·도주로 확정하지는 않는다)
		_searchPhase = RallySearchPhase.Finished;
		ReturnLeaderFromSearch();
		// 이동한 적이 없고 찾은 사람도 없으면 예전처럼 곧바로 끝낸다(true). 나갔다 오거나 찾은 사람이 걸어오는 중이면 강제 완료하지 않고 일반 도착 판정(전원 도착·상한)에 맡긴다.
		return !(_searchMoved || _foundBySearch.Count > 0);
	}

	// 우선 위치를 정해 리더를 보낸다 — 집결 명령을 받지 못한(받았으면 이미 이쪽으로 오는 중) 미집결자의 마지막 확인 위치 중 가장 가까운 곳. 갈 곳이 없거나 이미 곁이면 이동 없이 false.
	private void BeginRallySearch(float now)
	{
		if (Leader.currentFloor != RallyFloor) return;
		var wait = Leader.currentWait;
		if (wait != null && wait.Reason != WaitReason.AwaitingPartyAtRallyPoint) return; // 다른 대기를 하는 리더는 건드리지 않는다

		var candidates = new List<LeaderMath.RallySearchCandidate>();
		foreach (string name in _rallySearching)
		{
			var m = FindMemberByName(name);
			if (m == null || _rallyRecipients.Contains(m)) continue;
			if (!Command.LeaderLastSeen.TryGetValue(name, out var seen)) continue;
			// 8-1장 2번 — 알려진 경로 기준 도착 예상시간(걸음 수)으로 비교한다. 경로를 못 구하면 EstimateRallyWalkSteps가 직선거리로 대신하고, 맵 세션이 없으면(경로 조회 재료 없음) 값을 비워 PickRallySearchTarget이 직선거리를 쓴다.
			int? eta = Leader.Session?.cmap != null ? EstimateRallyWalkSteps(Leader, seen.position, seen.floor) : (int?)null;
			candidates.Add(new LeaderMath.RallySearchCandidate { Name = name, Position = seen.position, Floor = seen.floor, SeenAt = seen.time, EtaSteps = eta });
		}
		int index = LeaderMath.PickRallySearchTarget(Leader.position, RallyFloor, candidates);
		if (index < 0) return;

		var target = candidates[index];
		if (AIMovementHelper.ChebyshevDistance(Leader.position, target.Position) <= 1) return; // 이미 곁이다 — 이동 없이 관찰

		// 관찰 지점: 수색 위치를 볼 수 있는 서로 다른 유효 칸을 리더부터 차례로 받는다(시체 점유 칸 제외 — 8-1장 3번). 리더가 가장 가까운 칸이다.
		var claimed = new HashSet<Vector2Int>();
		Vector2Int leaderSlot = PickSearchSlot(Leader, target.Position, claimed);
		Leader.currentWait = new WaitState { Reason = WaitReason.SearchingAbsentee, WaitPosition = leaderSlot, WaitFloor = RallyFloor, SearchTarget = target.Position, SearchFacingIndex = 0 };
		Leader.waitStuckTurns = 0;
		_searchers.Clear();
		int followers = LeaderCommandSettings.RallySearchGroupMoveEnabled ? SendGroupToSearch(target.Position, claimed) : 0;

		_searchPhase = RallySearchPhase.Travel;
		_searchTravelStartedAt = now;
		_searchMoved = true;
		string who = followers > 0 ? $"리더 {Leader.name}과(와) 집결 지점에 모인 {followers}명이 함께" : $"리더 {Leader.name}이(가)";
		LogRally($"{who} {target.Name}의 마지막 확인 위치 ({target.Position.x},{target.Position.y}) 쪽 관찰 지점으로 수색하러 갑니다(이동 최대 {LeaderMath.RallySearchTravelMaxSeconds:F0}초)");
	}

	// 집결 지점에 모여 선 파티원을 수색에 함께 보낸다(8-1장 3번 "모인 인원이 해당 위치를 관찰할 수 있는 유효 지점으로 함께 이동한다", 순서도 P02 "우선 위치로 공동 이동") — 각자 다른 관찰 칸과 방향 번호를 받는다.
	// 아직 집결 지점으로 걸어오는 사람·다른 대기(코어 보고·함정 대응 등)를 하는 사람·다른 층·정박한 사람은 따라나서지 않고 하던 일을 계속한다. 반환값은 따라나선 인원 수.
	private int SendGroupToSearch(Vector2Int targetPos, HashSet<Vector2Int> claimed)
	{
		int facing = 1; // 리더가 0번(전방)이다
		foreach (var m in Members)
		{
			var w = m?.currentWait;
			if (m == null || m.hp <= 0f || m == Leader || w == null || w.Reason != WaitReason.AwaitingPartyAtRallyPoint || w.IsParked) continue;
			if (w.WaitFloor >= 0 && m.currentFloor != w.WaitFloor) continue;
			if (!w.WaitPosition.HasValue || !IsRallyArrived(m, w.WaitPosition.Value)) continue;

			Vector2Int slot = PickSearchSlot(m, targetPos, claimed);
			m.currentWait = new WaitState { Reason = WaitReason.SearchingAbsentee, WaitPosition = slot, WaitFloor = RallyFloor, SearchTarget = targetPos, SearchFacingIndex = facing++, SearchFollower = true };
			m.waitStuckTurns = 0;
			_searchers.Add(m);
		}
		return _searchers.Count;
	}

	// 수색 위치 주변에서 반경을 넓혀 가며(고정 반경 금지) 관찰 칸 하나를 고른다 — 서 있을 수 있고, 시체가 점유하지 않았고, 수색 위치로 직선이 열린 칸(RallyGroupSearchMath.IsObservationTile). 이미 다른 인원이 받은 칸(claimed)은 피한다. 못 찾으면 수색 위치 그대로.
	private Vector2Int PickSearchSlot(Human member, Vector2Int target, HashSet<Vector2Int> claimed)
	{
		var session = Leader?.Session;
		int floor = RallyFloor;
		if (session?.cmap == null || session.objectGrid == null) return target; // 맵이 없으면 판정 재료가 없다(테스트) — 예전처럼 수색 위치로 간다
		bool IsObservable(Vector2Int t)
		{
			bool walkable = member.CanMove(t, ignoreUnits: true);
			bool corpse = session.objectGrid.TryGetValue(new Vector3Int(t.x, t.y, floor), out InteractableObject obj)
				&& obj != null && !obj.IsCollected && obj.Tags != null && obj.Tags.Exists(tag => tag.Contains("Corpse"));
			bool clear = MovementMath.IsLineClear(t, target, p => SkillAction.IsLineBlockingTile(session, floor, p));
			return RallyGroupSearchMath.IsObservationTile(walkable, corpse, clear);
		}
		return PartyFormationMath.TryPickNearestFreeTile(target, IsObservable, claimed, PartyFormationMath.DefaultSearchRadius, out Vector2Int slot) ? slot : target;
	}

	// 수색 위치로 가는 이동이 끝났는가 — 리더와 따라나선 대원이 모두 도착했거나 끝내 못 가는 중이거나(IsParked), 수색 대기가 없어졌거나, 이동 상한을 넘겼다. 관찰 5초는 전원이 도착한 뒤부터 센다(8-1장 4번 "접근과 재집결 이동시간은 이 5초에서 제외").
	private bool RallySearchTravelDone(float now)
	{
		var wait = Leader.currentWait;
		if (wait == null || wait.Reason != WaitReason.SearchingAbsentee) return true;
		if ((wait.AtSlot || wait.IsParked) && SearchFollowersArrived()) return true;
		if (now - _searchTravelStartedAt < LeaderMath.RallySearchTravelMaxSeconds) return false;
		LogRally($"수색 위치로 가는 이동이 {LeaderMath.RallySearchTravelMaxSeconds:F0}초를 넘겨 선 자리에서 관찰합니다");
		return true;
	}

	// 수색 중 리더가 시야로 다시 본 미집결자를 찾은 것으로 처리한다 — 곁이라 집결 명령이 닿으면 지금 전달해 집결지로 오게 하고, 닿지 않으면 이번 대기에서 제외한다(보였지만 명령할 수 없는 사람을 영원히 기다리지 않는다).
	private void ResolveFoundBySearch()
	{
		List<string> found = null;
		for (int i = _rallySearching.Count - 1; i >= 0; i--)
		{
			string name = _rallySearching[i];
			var m = FindMemberByName(name);
			if (m == null || m.hp <= 0f) { _rallySearching.RemoveAt(i); continue; }
			bool seen = Leader.visiblePartyMembers.Contains(m)
				|| (Command.LeaderLastSeen.TryGetValue(name, out var last) && last.time >= _searchBeganAt);
			if (!seen) continue;
			_rallySearching.RemoveAt(i);
			_foundBySearch.Add(name);
			(found ??= new List<string>()).Add(name);
		}
		if (found == null) return;

		LogRally($"수색 중 [{string.Join(", ", found)}]을(를) 시야로 확인했습니다(사망·도주 확정이 아니라 발견) — 집결 명령을 전달합니다");
		TickLateRallyDelivery();
		foreach (string name in found)
		{
			var m = FindMemberByName(name);
			if (m == null || _rallyRecipients.Contains(m)) continue;
			_givenUpAbsent.Add(name);
			LogRally($"{name}은(는) 보이지만 집결 명령이 닿지 않아 이번 대기에서 제외합니다");
		}
	}

	// 따라나선 대원이 모두 관찰 칸에 도착했는가(도착·정박 모두 인정). 죽었거나 수색 대기가 없어진 대원은 기다리지 않는다.
	private bool SearchFollowersArrived()
	{
		foreach (var m in _searchers)
		{
			var w = m?.currentWait;
			if (m == null || m.hp <= 0f || w == null || w.Reason != WaitReason.SearchingAbsentee || !w.SearchFollower) continue;
			if (!(w.AtSlot || w.IsParked)) return false;
		}
		return true;
	}

	// 수색을 마쳤다 — 나가 있던 리더와 따라나섰던 대원을 집결지로 돌려보낸다(다시 집결 대기로 전환). 집결 명령이 유지되므로 수색 시작 위치 주변이 아니라 집결 자리로 다시 모인다(8-3장).
	private void ReturnLeaderFromSearch()
	{
		var wait = Leader?.currentWait;
		bool leaderSearching = wait != null && wait.Reason == WaitReason.SearchingAbsentee && RallyPoint.HasValue;
		var claimed = new HashSet<Vector2Int>();
		if (leaderSearching)
		{
			claimed.Add(RallyPoint.Value);
			GiveRallySlot(Leader, claimed, gatherNearby: true); // 리더의 자리는 집결지 그대로
			LogRally($"리더 {Leader.name}이(가) 수색을 마치고 집결지 {RallyPoint.Value}로 돌아갑니다");
		}
		ReturnSearchersToRally(claimed);
	}

	// 따라나섰던 대원마다 집결 자리를 다시 받는다 — 이미 다른 대원이 서 있는 자리는 피한다. 집결이 끝났거나 이미 다른 대기로 바뀐 대원은 건드리지 않는다.
	private void ReturnSearchersToRally(HashSet<Vector2Int> claimed)
	{
		if (_searchers.Count == 0) return;
		if (RallyPoint.HasValue)
		{
			bool gatherNearby = AIConfigLoader.Behavior?.rallyGatherNearbyEnabled ?? true;
			foreach (var other in Members) // 집결지에 남아 있던 대원의 자리
			{
				var ow = other?.currentWait;
				if (other == null || other.hp <= 0f || ow == null || ow.Reason != WaitReason.AwaitingPartyAtRallyPoint || !ow.WaitPosition.HasValue) continue;
				claimed.Add(ow.WaitPosition.Value);
			}
			var newSlots = new List<Vector2Int>(_searchers.Count);
			foreach (var m in _searchers)
			{
				var w = m?.currentWait;
				if (m == null || m.hp <= 0f || w == null || w.Reason != WaitReason.SearchingAbsentee || !w.SearchFollower) continue;
				newSlots.Add(GiveRallySlot(m, claimed, gatherNearby));
			}
			if (newSlots.Count > 0) LogRally($"수색을 마치고 따라나섰던 {newSlots.Count}명이 집결지 {RallyPoint.Value}로 돌아갑니다");
		}
		_searchers.Clear();
	}

	// 교전·경계로 집결 시계가 멈춘 만큼 수색 이동·관찰 시계도 멈춘다(ApplyRallyEngagementPause가 호출).
	private void ShiftRallySearchClock(float credit)
	{
		if (_searchPhase == RallySearchPhase.Travel) _searchTravelStartedAt += credit;
		else if (_searchPhase == RallySearchPhase.Observe) _rallySearchStartedAt += credit;
	}

	private Human FindMemberByName(string name)
	{
		foreach (var m in Members)
			if (m != null && m.name == name) return m;
		return null;
	}

	// 수색 위치(관찰 칸)로 가는 리더·따라나선 대원의 한 틱(TacticalFSMState.ExecuteWait가 호출) — 도착하면 AtSlot, 오래 막혀 못 가면 IsParked로 표시하고 단계 전이는 ApplyRallyRules가 읽는다.
	// 도착(또는 정박)하면 각자 받은 방향 번호로 시야 방향을 맞춘다(8-1장 4번 "전방·후방·좌우를 나눠 수색").
	public void StepSearchAbsentee(Human who, WaitState wait)
	{
		if (!wait.WaitPosition.HasValue) return;
		if (!wait.IsParked)
		{
			if (wait.WaitFloor >= 0 && who.currentFloor != wait.WaitFloor) { wait.IsParked = true; return; } // 층을 건넜다 — 그 좌표로 걷지 않는다
			if (SlotSeek.Step(who, wait, wait.WaitPosition.Value, useHold: false, arrivalRadius: 1) == SeekStatus.GaveUp)
			{
				wait.IsParked = true;
				LogRally($"{(wait.SearchFollower ? "" : "리더 ")}{who.name}이(가) 수색 위치 ({wait.WaitPosition.Value.x},{wait.WaitPosition.Value.y})에 오래 못 가 선 자리에서 관찰합니다");
			}
		}
		if (wait.AtSlot || wait.IsParked) FaceAssignedSearchDirection(who, wait);
	}

	private static void FaceAssignedSearchDirection(Human who, WaitState wait)
	{
		if (!wait.SearchTarget.HasValue) return;
		Vector2Int facing = RallyGroupSearchMath.FacingVector(wait.SearchFacingIndex, wait.SearchTarget.Value - who.position);
		who.currentDir = SkillAction.GetDirection8(facing);
	}

	// 집결 자리까지 남은 걸음 수 — 같은 층이고 길을 알면 실제 경로 길이(아군·함정을 빼고 벽·닫힌 문·지형은 그대로 본 구조 경로, 후퇴 거점 조회와 같은 기준), 아니면 직선(체비셰프) 거리로 대신한다(R4: 우회로가 긴 대원의 기한이 일찍 잡히던 근사의 교체).
	// 다른 층이거나 경로를 못 찾으면 예전 근사 그대로라, 이 함수 때문에 집결 기한이 짧아지는 일은 없다(경로 길이 ≥ 직선거리).
	internal static int EstimateRallyWalkSteps(Human m, Vector2Int goal, int goalFloor)
	{
		int straight = AIMovementHelper.ChebyshevDistance(m.position, goal);
		if (goalFloor >= 0 && m.currentFloor != goalFloor) return straight;
		return RetreatBlockResolver.TryGetBaselineLength(m, goal, out int pathLength) ? Mathf.Max(straight, pathLength) : straight;
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
