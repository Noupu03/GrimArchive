using UnityEngine;
using Haare.Util.Logger;

// 던전 밖으로 나가는 단계(후퇴도주 문서 5-1·10장). 출구 = 현재 층에서 0층(던전 밖)으로 가는 계단이며, 계단 접근·통과는 NavigationFSMState의 계단 이동 로직을 그대로 쓴다. 귀환은 던전 밖으로 돌아가는 것이고 거점 도착은 귀환에 포함하지 않는다.
public static class RetreatExit
{
	// 현재 층의 출구 접근 타일(비어 있는 가장 가까운 후보). 이 층에 출구 계단이 없으면 false.
	public static bool TryResolveExitTile(Human h, out Vector2Int tile)
		=> NavigationFSMState.TryResolveStairApproach(h, 0, out tile);

	// 계단 블록 곁(stairArrivalRadius 이내)에 있는가.
	public static bool IsAtStairs(Human h)
	{
		if (h.Session?.cmap == null || !h.Session.cmap.TryGetStairPosition(h.currentFloor, 0, out Vector2Int stairPos)) return false;
		return NavigationFSMState.DistanceToStairBlock(h.position, stairPos) <= (AIConfigLoader.Behavior?.stairArrivalRadius ?? 1);
	}

	// 계단을 건너 던전 밖으로 나간다. 건넜으면 Success(이탈 마무리까지 끝냄), 점유 등으로 아직이면 Running.
	public static BTStatus StepCross(Human h)
	{
		h.pendingStairTargetFloor = 0;
		BTStatus status = NavigationFSMState.CrossStairs(h);
		if (status == BTStatus.Success)
		{
			Complete(h);
			return BTStatus.Success;
		}
		h.pendingStairTargetFloor = null; // 못 건넌 채 남기면 이 유닛이 Navigation 상태로 돌아갔을 때 계단 이동으로 오해된다
		return BTStatus.Running;
	}

	// 던전 밖 이탈의 공통 마무리 — 후퇴 출구 이동과 웨이브 승리 퇴각(HumanWaveManager.RetreatMemberToFloor0)이 같이 쓴다. 기록 전달(명세서 10장: 생환자가 던전 밖으로 전달한 기록만 전역에 반영), 파티에서 제외, 침공 단위 상태 초기화.
	public static void Complete(Human member)
	{
		if (member == null) return;
		if (RetreatSystem.GetEffectiveMode(member) != RetreatMode.None) // 후퇴·도주로 나가는 경우만 알린다(웨이브 승리 퇴각은 제외)
			RetreatSystem.NotifyPlayer(member, $"인류 {member.name}이(가) 던전 밖으로 빠져나갔습니다 — 가진 기록이 전달됩니다");
		member.ClearInteractionProgress(); // v0.6 9-9장: 웨이브를 떠나는 시점에 조사·해제 진행도 제거(생존자는 재사용된다)
		member.Knowledge?.DeliverRecords(member);
		member.Session?.Core?.Salvage.OnExported(member); // 코어 잔해를 들고 나갔다면 반출 성공(코어 점령 v0.2 7·8장) — 후퇴·도주로 나간 경우도 같다
		member.party?.OnMemberExited(member);
		FleeSystem.ResetForNewInvasion(member);
		LogHelper.Log(LogHelper.GAME, $"[후퇴] {member.name}이(가) 던전 밖으로 나갔습니다(기록 전달 완료)");
		GrimArchive.Wave.HumanWaveManager.Instance?.OnMemberLeftDungeon(member);
	}
}
