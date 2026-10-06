using System.Collections.Generic;
using UnityEngine;

public enum TrapAssigneeStatus
{
	EnRoute,   // 해제 위치로 이동 중 — CalcTime/RemainingSeconds가 유효
	Arrived,   // 해제 위치(함정 인접 1칸)에 도달
	Concluded, // 도달 전에 대응을 끝냄(해제·우회·통과·파괴)
}

// 담당자가 대기자에게 알리려고 조율 기록에 남기는 최신 정보 — 대기자는 전파 조건(CanPropagate)을 충족할 때만 읽고, 범위 밖이면 나중에 범위 안이 될 때 읽는다(TrapPartySystem.TickWaitingForSelectedUnit).
public class TrapAssigneeReport
{
	public string SenderName;
	public TrapAssigneeStatus Status;
	public float CalcTime;         // 이 정보를 계산한 시점(Time.time)
	public float RemainingSeconds; // 계산 시점 기준 남은 예상 이동시간(EnRoute일 때만 의미)
	public int Sequence;           // 새 보고마다 증가 — 대기자가 이미 처리한 보고를 다시 평가하지 않게 한다
	public float ArrivalTime => CalcTime + RemainingSeconds;
}

// 함정 하나를 둘러싼 파티 전체의 조율 상태(03번 9장) — 누가 발견했고 누가 해제 담당으로 선정됐는지만 담는다(Party.TrapCoordinations[함정 오브젝트 Id]). 개별 유닛의 TrapInteractionState(진행 단계/타이머)와 별개이며, 기록은 웨이브 동안 삭제되지 않는다(정보 재전파의 기준).
public class TrapPartyCoordination
{
	public string TrapObjectId;
	public Vector3Int TrapPosition;
	public string DiscovererName;
	// 발견자가 집결·공동 이동·귀환 중이라 대응(응답 대기·선정)을 시작하지 않고 기록만 남긴 상태(03번 8장) — 이후 집결·이동 중이 아닌 유닛이 그 함정을 처음 발견하면 이 기록을 이어받아 표준 절차를 시작한다(TrapPartySystem.OnTrapDiscovered).
	public bool Deferred;
	// Deferred 중에서도 "발견자가 다른 함정을 처리·대기 중이라" 보류한 경우 — 그 유닛(또는 같은 방의 다른 한가한 유닛)이 끝난 뒤 TrapPartySystem.TickAdoptBusyDeferred로 이어받는다. 집결·이동 때문에 보류한 기존 Deferred는 이어받는 틱이 없다(기존 동작·문서 유지).
	public bool DeferredBusy;
	// 알던 함정이 이동 경로를 막아 대응을 다시 연 시각 + 재시작 쿨다운 — 파괴·해제가 모두 불가능한 함정에서 대응이 끝나자마자 다시 열리는 반복을 막는다.
	public float NextBlockedRetryTime;
	public string SelectedUnitName;
	public bool SelectionLocked;
	// 기한 안에 도착하지 않아 재선정에서 제외된 유닛(미도착만으로 사망·도주를 확정하지 않는다).
	public readonly List<string> ExcludedUnitNames = new List<string>();
	public TrapAssigneeReport Report;
}
