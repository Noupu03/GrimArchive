using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using Haare.Util.Logger;

// 임시 진단(2026-10-05) — 인류 유닛이 좁은 타일 몇 개를 계속 왕복하면 "그때 어떤 상태·대기·목표였는지"를 로그 한 줄로 남긴다. 동작은 바꾸지 않는다. 사용자가 "아처가 비전투에서도 앞뒤·좌우로 빠르게 왔다갔다"를 신고했지만 상태·상황이 제각각이라 코드만으로 원인을 못 좁혀서 넣었다. 원인이 확정돼 고쳐지면 지워도 되는 임시 코드다(PropagationDebugVisualizer와 같은 관례).
public static class OscillationDiagnostics
{
	private const int MaxRecords = 16;
	private const float LogCooldownSeconds = 10f; // 같은 유닛은 이 간격마다 한 줄만

	private sealed class Track
	{
		public readonly List<float> Times = new List<float>(MaxRecords + 1);
		public readonly List<int> Xs = new List<int>(MaxRecords + 1);
		public readonly List<int> Ys = new List<int>(MaxRecords + 1);
		public float LastLogTime = -999f;
	}

	// 유닛이 사라지면 기록도 같이 수거된다.
	private static readonly ConditionalWeakTable<Unit, Track> _tracks = new ConditionalWeakTable<Unit, Track>();

	// GameSession.ProcessUnitAction이 유닛 위치가 실제로 바뀐 틱마다 부른다.
	public static void OnMoved(Unit unit)
	{
		if (!(unit is Human human) || !(AIConfigLoader.Behavior?.oscillationDiagnosticsEnabled ?? true)) return;

		Track track = _tracks.GetOrCreateValue(human);
		float now = Time.time;
		track.Times.Add(now);
		track.Xs.Add(human.position.x);
		track.Ys.Add(human.position.y);
		if (track.Times.Count > MaxRecords)
		{
			track.Times.RemoveAt(0);
			track.Xs.RemoveAt(0);
			track.Ys.RemoveAt(0);
		}

		if (track.Times.Count < OscillationMath.MinMoves || now - track.LastLogTime < LogCooldownSeconds) return;
		if (!OscillationMath.IsOscillating(track.Times, track.Xs, track.Ys, now, OscillationMath.WindowSeconds, OscillationMath.MinMoves, OscillationMath.MaxDistinctTiles, out int moves, out int distinct)) return;

		track.LastLogTime = now;
		LogHelper.Log(LogHelper.GAME, Describe(human, track, moves, distinct));
	}

	private static string Describe(Human h, Track track, int moves, int distinct)
	{
		var sb = new StringBuilder(220);
		sb.Append("[진동진단] ").Append(h.name).Append(": ").Append(OscillationMath.WindowSeconds.ToString("F0")).Append("초간 ").Append(moves)
			.Append("회 이동(서로 다른 타일 ").Append(distinct).Append("개) 타일 ");
		var seen = new List<long>(4);
		for (int i = track.Times.Count - 1; i >= 0 && seen.Count < 4; i--)
		{
			long key = ((long)track.Xs[i] << 32) ^ (uint)track.Ys[i];
			if (seen.Contains(key)) continue;
			seen.Add(key);
			sb.Append('(').Append(track.Xs[i]).Append(',').Append(track.Ys[i]).Append(')');
		}

		sb.Append(", 라벨=").Append(h.fsm != null ? h.fsm.GetLabel(h) : "?");

		var wait = h.currentWait;
		sb.Append(", 대기=");
		if (wait == null) sb.Append("없음");
		else
		{
			sb.Append(wait.Reason);
			if (wait.WaitPosition.HasValue) sb.Append(wait.WaitPosition.Value);
			if (wait.IsParked) sb.Append("·정박");
		}
		if (h.idleWait != null && Time.time - h.idleLastStepTime <= 3f) sb.Append(", 합류 중");

		var alert = h.currentAlertSearch;
		sb.Append(", 경계=");
		if (alert == null) sb.Append("없음");
		else
		{
			if (alert.IsPostCombatSweep) sb.Append("전투후스윕 ");
			if (alert.IsSoundResponse) sb.Append("소리반응 ");
			if (alert.IsDeathSearch) sb.Append("사망수색 ");
			if (alert.IsIndirectEnemyApproach) sb.Append("전파적접근 ");
			if (alert.IsAttackDirectionSearch) sb.Append("공격방향수색 ");
			if (alert.TargetPosition.HasValue) sb.Append(alert.TargetPosition.Value);
		}

		sb.Append(", 함정=").Append(h.currentTrapInteraction != null ? h.currentTrapInteraction.Phase.ToString() : "없음");
		sb.Append(", 조사=").Append(h.currentInvestigation != null ? h.currentInvestigation.TargetPosition.ToString() : "없음");
		sb.Append(", 탐험목표=").Append(h.currentExplorationTarget.HasValue ? h.currentExplorationTarget.Value.ToString() : "없음");

		Unit enemy = h.CombatTargeting.AttackTarget;
		sb.Append(", 전투대상=");
		if (enemy == null || enemy.hp <= 0) sb.Append("없음");
		else sb.Append(enemy.name).Append("(거리 ").Append(AIMovementHelper.ChebyshevDistance(h.position, enemy.position)).Append(')');
		return sb.ToString();
	}
}
