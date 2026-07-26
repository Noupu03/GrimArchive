using UnityEngine;

// 03문서 9장 개편(2026-07-27): 함정 하나를 둘러싼 파티 전체의 조율 상태 — 누가 발견했고, 누가 실제
// 해제 담당으로 선정됐는지. Party.TrapCoordinations[함정 오브젝트 Id]에 저장된다. 개별 유닛의
// TrapInteractionState(진행 단계/타이머)와 별개로, "파티 안에서 이 함정을 누가 맡는지"라는 한 번만
// 결정되면 되는 사실만 담는다.
public class TrapPartyCoordination
{
	public string TrapObjectId;
	public Vector3Int TrapPosition;
	public string DiscovererName;
	public string SelectedUnitName;
	public bool SelectionLocked;
}
