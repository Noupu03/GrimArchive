using System.Text;
using UnityEngine;

// 집결·진형·돌파 로그에 쓰는 설명 문자열 — 동작에는 영향이 없다. 대기 중인 유닛이 왜 자리에 못 서는지 가리는 것이 핵심이라(플레이 로그 2026-10-01 3차 분석),
// FSM 상태 라벨(TacticalFSMState.GetSubLabel)이 대기를 경계보다 먼저 판정해 경계 중에도 "전술(대기)"로 보이는 한계를 경계 종류·보이는 적·합류 대기를 따로 적어 보완한다.
public static class PartyDiagnostics
{
	public static string TagOf(Party party) => $"[파티] {party.Name}({party.Type.ToKorean()})";

	// "이름[상태, 경계=종류 N초, 보이는 적 N명, 전투 합류 대기, 조사 중, 포기, 출발 허가 대기, 자리까지 N칸]"
	public static string DescribeMember(Human m, Vector2Int? slot)
	{
		var sb = new StringBuilder(m.name).Append('[');
		sb.Append(m.fsm?.CurrentState?.GetType().Name.Replace("FSMState", "") ?? "상태 없음");
		var alert = m.currentAlertSearch;
		if (alert != null) sb.Append(", 경계=").Append(AlertKind(alert)).Append(' ').Append(alert.ElapsedSeconds.ToString("F0")).Append('초');
		if (m.personalSpottedEnemies.Count > 0) sb.Append(", 보이는 적 ").Append(m.personalSpottedEnemies.Count).Append('명');
		if (m.currentJoinCombatWait != null) sb.Append(", 전투 합류 대기");
		if (m.currentInvestigation != null) sb.Append(", 조사 중");
		var wait = m.currentWait;
		if (wait != null && wait.IsParked) sb.Append(", 포기");
		if (wait != null && wait.Reason == WaitReason.EnteringNextRoom && !wait.Released) sb.Append(", 출발 허가 대기");
		if (slot.HasValue) sb.Append(", 자리까지 ").Append(AIMovementHelper.ChebyshevDistance(m.position, slot.Value)).Append('칸');
		return sb.Append(']').ToString();
	}

	private static string AlertKind(AlertSearchState a)
	{
		if (a.IsDeathSearch) return "사망 수색";
		if (a.IsSoundResponse) return "소리 반응";
		if (a.IsUnidentifiedAttackSearch) return "미식별 공격 수색";
		if (a.IsPostCombatSweep) return "전투 후 스윕";
		if (a.IsIndirectEnemyApproach) return "전파 적 접근";
		if (a.IsAttackDirectionSearch) return "공격 방향 수색";
		return "일반 경계";
	}

	// 이 단계에서 아직 할 일을 끝내지 못한 파티원들의 설명(없으면 null).
	public static string DescribeNotDone(PartyAdvancePlan plan)
	{
		StringBuilder sb = null;
		foreach (var m in plan.Ranks.Keys)
		{
			if (PartyAdvanceSystem.IsMemberDone(m, plan)) continue;
			(sb ??= new StringBuilder()).Append(DescribeMember(m, PartyAdvanceSystem.SlotOf(m, plan))).Append(' ');
		}
		return sb?.ToString().TrimEnd();
	}

	public static string RankSummary(PartyAdvancePlan plan)
	{
		int[] counts = new int[PartyFormationMath.MaxRank + 1];
		foreach (var rank in plan.Ranks.Values) counts[rank]++;
		return $"근접 {counts[0]}명·리더 {counts[PartyFormationMath.LeaderRank]}명·원거리 {counts[PartyFormationMath.MaxRank]}명";
	}
}
