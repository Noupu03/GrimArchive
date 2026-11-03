using System.Text;
using UnityEngine;
using Haare.Util.Logger;

// 리더 명령 플레이 확인용 debug 도구(임시) — debug 메뉴 "리더 명령 확인"과 선택 유닛 정보 패널이 쓴다. 값만 바꾸고 판정은 실제 경로(승계 10초·사망 충격·정신력 완화·명령 발령)를 그대로 탄다. 확인이 끝나면 지워도 되는 코드다.
public static class LeaderDebug
{
	private const string DebugModifierKey = "debug";

	// 선택 유닛을 즉시 사망 처리한다 — 리더면 10초 승계 흐름(대사 포함)을 확인할 수 있다.
	public static void Kill(Unit unit)
	{
		if (!(unit is Human human)) return;
		LogHelper.Log(LogHelper.GAME, $"[지휘] (debug) {human.name} 사망 처리");
		human.hp = 0f;
	}

	public static void ForceFlee(Unit unit)
	{
		if (!(unit is Human human)) return;
		LogHelper.Log(LogHelper.GAME, $"[지휘] (debug) {human.name} 개인 도주 확정");
		FleeSystem.ConfirmFlee(human, FleeReason.MentalFloor);
	}

	public static void DesignateSuperior(Unit unit)
	{
		if (unit is Human human) ExpeditionCommandSystem.Designate(human);
	}

	// 현재 통솔력 일시 보정(+/−) — 새 명령 자격·전파 반경·버프 강도·정신력 완화가 즉시 달라지는 것을 확인한다. 기본 통솔력은 바뀌지 않는다.
	public static void AdjustLeadership(Unit unit, float delta)
	{
		if (!(unit is Human human) || human.LeaderState == null) return;
		float next = human.LeaderState.GetLeadershipModifier(DebugModifierKey) + delta;
		human.LeaderState.SetLeadershipModifier(DebugModifierKey, next);
		LogHelper.Log(LogHelper.GAME, $"[지휘] (debug) {human.name} 현재 통솔력 {human.LeaderState.CurrentLeadership:F0}(기본 {human.BaseStat.leadership:F0}, 보정 {next:+0;-0;0})");
	}

	// 선택 유닛 정보 패널용 요약 — 리더/상위 지휘관 여부, 파티 컨셉, 쿨타임, 받은 명령.
	public static string Describe(Human human)
	{
		if (human == null || human.LeaderState == null) return "";
		var sb = new StringBuilder();
		var state = human.LeaderState;
		var party = human.party;
		float now = Time.time;

		sb.AppendLine($"현재 통솔력: {state.CurrentLeadership:F0} (기본 {human.BaseStat.leadership:F0}), 전파 반경 {LeaderMath.PropagationRadius(state.CurrentLeadership)}칸");
		if (party != null)
		{
			bool isLeader = party.Leader == human;
			sb.AppendLine($"파티 컨셉: {party.Concept.ToKorean()} / 리더: {(isLeader ? "본인" : party.Leader != null ? party.Leader.name : party.Command.SuccessionPending ? "승계 대기" : "없음")}");
			if (isLeader)
			{
				float cd = Mathf.Max(0f, party.Command.CooldownUntil - now);
				sb.AppendLine($"리더 명령 쿨타임: {cd:F1}초 / 지정: {(party.Command.HasFocus ? DescribeFocus(party.Command) : "없음")}");
			}
			else if (party.Command.SuccessionPending)
			{
				sb.AppendLine(party.Command.SuccessionClockStarted
					? $"후임 선정까지 {Mathf.Max(0f, LeaderMath.SuccessionDelaySeconds - (now - party.Command.SuccessionStartedAt)):F1}초"
					: "도주 사실을 아직 아무도 확인하지 못해 승계 시계 정지");
			}
		}
		if (state.IsSuperiorCommander) sb.AppendLine($"상위 지휘관 — 쿨타임 {Mathf.Max(0f, state.SuperiorCooldownUntil - now):F1}초");
		if (state.EffectiveFocus != null)
		{
			var f = state.EffectiveFocus;
			sb.AppendLine($"받은 {f.Command.ToKorean()}: {(f.IsObject ? "차단물" : f.Target != null ? f.Target.name : "-")}{(f.Suspended ? " (중단)" : "")}{(f.LostAt >= 0f ? $", 추적 남은 {LeaderMath.PursuitRemaining(f.PursuitStart, now):F1}초" : "")}");
		}
		if (state.Action != null && state.Action.IsActive(now))
			sb.AppendLine($"적용 중 {state.Action.Command.ToKorean()}: {Mathf.Max(0f, state.Action.ExpiresAt - now):F1}초 남음");
		if (state.BuffRate > 0f) sb.AppendLine($"기본 버프: {state.BuffRate * 100f:F1}% ({state.BuffSource})");
		if (state.DeathShockUntil > 0f) sb.AppendLine($"사망 충격: {Mathf.Max(0f, state.DeathShockUntil - now):F1}초 남음 (세부 스테이터스 −20%)");
		return sb.ToString();
	}

	private static string DescribeFocus(PartyCommandState st)
		=> st.FocusObject.HasValue ? $"차단물 {st.FocusObject.Value}" : st.FocusTarget != null ? $"{st.FocusTarget.name}(점수 {st.FocusScore:F0})" : "대상 소멸";
}
