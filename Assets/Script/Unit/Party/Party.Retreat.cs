using System.Collections.Generic;

// 파티 후퇴 상태(후퇴도주 문서 2026-10-13). WaveSpawner가 웨이브마다 새 Party를 만들므로 침공 단위 이력(휴식 완료 등)은 별도 정리 없이 웨이브 경계에서 초기화된다. 전이는 PartyRetreatSystem이 맡는다.
public sealed class PartyRetreatState
{
	public RetreatMode Mode;                 // None 또는 PartyRetreat — 한 번 시작하면 리더 사망·회복으로 취소하지 않는다(명세서 2-2·7-1장). 휴식을 마치고 재탐험/재출격할 때만 None으로 돌아간다
	public PartyRetreatPhase Phase;
	public RetreatDestination Destination;   // 지금 단계의 목적지 종류(거점/출구)
	public BaseKind PreferredBaseKind;
	public Room BaseRoom;                    // Destination이 거점일 때의 방
	public float StartedAt;
	public string StartReason;

	public float RestStartedAt = -1f;        // 첫 도착자가 생긴 시각(-1 = 아직 없음)
	public readonly HashSet<UnityEngine.Vector2Int> ClaimedRestSlots = new HashSet<UnityEngine.Vector2Int>();

	// 일반 파티: 이번 침공에서 거점 휴식을 완료했는가(휴식 1회 제한, 명세서 2-2장). 점령 파티는 제한 없음.
	public bool NormalRestCompleted;
	// 휴식을 마치고 목표 미달성으로 탐험을 재개한 상태 — 목표를 달성해도 즉시 귀환하지 않고, 후퇴 기준을 다시 채우면 재휴식 없이 출구로 간다(명세서 2-2장).
	public bool Reexploring;
	// 점령 파티 N0: 처음 출격한 인원(고정) — 재출격 생존 인원 80% 기준(명세서 2-3장). GameSession.CreateParty가 채운다.
	public int InitialSortieCount;
	public int RetreatCount;                 // 이번 침공 후퇴 시작 횟수(진단)
	public int ResortieCount;                // 점령 파티 재출격 횟수(진단)
	public float NextResortieLogTime;        // 재출격 대기 로그 스로틀
	public float NextReachCheckTime;         // 거점 도달성 재확인 스로틀(PartyRetreatSystem.ValidateBase)

	public bool IsRetreating => Mode != RetreatMode.None;
}

public partial class Party
{
	public readonly PartyRetreatState Retreat = new PartyRetreatState();

	// 던전 밖으로 이탈한 파티원 수 — 이탈자는 Members에서 빠지므로(OnMemberExited) 남은 인원이 전부 죽어도 "전멸"로 세지 않기 위한 기록이다(명세서 10장: 전멸 = 파티원 전원 사망).
	public int ExitedCount { get; private set; }

	// 후퇴 상태 전이를 구동하는 구성원: 리더가 살아 있으면 리더, 아니면 던전 안(1층 이상) 생존자 중 첫 번째. 리더가 죽어도 이미 시작한 후퇴가 계속 진행되게 한다(R57).
	public Human RetreatDriver
	{
		get
		{
			if (Leader != null && Leader.hp > 0f && Leader.currentFloor >= 1) return Leader;
			foreach (var m in Members)
				if (m != null && m.hp > 0f && m.currentFloor >= 1) return m;
			return null;
		}
	}

	// 임무 수량 달성(방 탐색 B+E 또는 몬스터 처치 수량, 01번 2장) — 달성만으로는 귀환하지 않고 휴식 종료 시점의 분기(귀환/재탐험)에만 쓴다(명세서 2-2장).
	public bool IsMissionGoalAchieved
		=> CompletedRoomExploreCount >= PartyGoalMath.RequiredRoomExploreCount
			|| (GrimArchive.Wave.HumanWaveManager.Instance != null && GrimArchive.Wave.HumanWaveManager.Instance.CommonMonsterKillCount >= PartyGoalMath.RequiredMonsterKillCount);

	// 후퇴 평균 계산 대상(명세서 1-2장): 집결했거나 함께 행동 중인 생존 파티원. 사망자·개인 도주 확정자(확정 즉시 제외, 실제 이탈까지 기다리지 않음)·미집결자는 뺀다. 전투 진형이 잠시 흩어진 것만으로는 빼지 않는다.
	public void CollectRetreatAverageMembers(List<Human> into)
	{
		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0f) continue;
			if (m.Retreat != null && m.Retreat.IsFleeing) continue;
			if (IsUnaccountedForRetreatAverage(m)) continue;
			into.Add(m);
		}
	}

	// 미집결자 근사: 집결이 진행 중인데 명령을 받지 못한 구성원, 또는 기다림을 포기한 구성원. 집결·수색 문서(6장)의 "도착 예정·마지막 위치" 판단은 후속이라 집결 시스템의 기존 분류(_rallyRecipients/_givenUpAbsent)를 그대로 쓴다. 단순 부재·무응답은 사망·도주로 확정하지 않는다.
	private bool IsUnaccountedForRetreatAverage(Human m)
	{
		if (m == Leader) return false;
		if (_givenUpAbsent.Contains(m.name)) return true;
		return IsRallyActive && !_rallyRecipients.Contains(m);
	}

	public PartyRetreatMath.Averages ComputeRetreatAverages(List<Human> scratch = null)
	{
		var members = scratch ?? new List<Human>();
		members.Clear();
		CollectRetreatAverageMembers(members);
		return AveragesOf(members);
	}

	public static PartyRetreatMath.Averages AveragesOf(List<Human> members)
	{
		var vitals = new List<PartyRetreatMath.MemberVitals>(members.Count);
		foreach (var m in members) vitals.Add(PartyRetreatMath.MemberVitals.From(m.hp, m.maxHp, m.BaseStat.mental));
		return PartyRetreatMath.ComputeAverages(vitals);
	}

	// 후퇴가 시작되면 진행 중이던 집결(미집결자 수색 포함)·방 이동 계획을 중단하고 새로 시작하지 않는다(명세서 1-2·6장: 후퇴 기준을 충족한 파티는 미집결자를 기다리지 않는다). 구성원 개인의 대기·조사 등은 PartyRetreatSystem이 명령을 전달할 때 접는다.
	public void AbortActivitiesForRetreat()
	{
		IsRallyActive = false;
		RallyPoint = null;
		_rallyRecipients.Clear();
		_pendingRallyRelease.Clear();
		ReadyToAdvance = false;
		AdvanceFromRoom = null;
		AdvanceAttempts = 0;
		LeaderKnownCorePosition = null;
		PartyAdvanceSystem.Abort(this, "후퇴 시작", retry: false);
		AdvancePlan = null;
	}

	// 던전 밖으로 나간 파티원을 파티에서 뺀다. 리더가 나갔으면 남은 인원 중에서 다시 선정한다.
	public void OnMemberExited(Human member)
	{
		if (member == null || !Members.Remove(member)) return;
		ExitedCount++;
		if (Leader == member)
		{
			Leader = null;
			AssignLeaderIfNeeded();
		}
	}
}
