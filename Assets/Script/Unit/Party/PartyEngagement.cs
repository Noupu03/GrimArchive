using System.Collections.Generic;

// 집결·진형·돌파의 시간 제한을 멈출지 정하는 '교전·경계 중' 판정(03번 13항·05번 2장: 전투 후 10초 경계를 마친 뒤 기존 집결·이동 재개). 대기보다 우선하는 행동(전투·경계·합류 대기)이나 그 근거(인지한 적)가 있는 동안은 대기 로직이 안 돌므로 그 시간을 타이머에 세지 않는다.
public static class PartyEngagement
{
	// 멈춰 줄 수 있는 시간의 상한(한 단계·한 집결당) — 기존 개별 포기 기준(doorApproachMaxBlockedSeconds)의 4배(내부 판단, 사용자 확정).
	public static float PauseCap => 4f * (AIConfigLoader.Behavior?.doorApproachMaxBlockedSeconds ?? 30f);

	// Panic·함정 대응은 포함하지 않는다 — 개별 막힘 시계의 간격 리셋(SlotSeek.Step)과 단계 상한이 받는다.
	public static bool IsEngaged(Human m)
	{
		if (m == null || m.hp <= 0) return false;
		return m.fsm?.CurrentState is CombatFSMState
			|| m.currentAlertSearch != null
			|| m.currentJoinCombatWait != null
			|| m.personalSpottedEnemies.Count > 0;
	}

	public static bool AnyEngaged(IEnumerable<Human> members)
	{
		foreach (var m in members)
		{
			if (IsEngaged(m)) return true;
		}
		return false;
	}
}
