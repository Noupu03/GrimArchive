using UnityEngine;

// 5장: 이 유닛이 지금 진행 중인 조사의 하위 상태. Goal_Investigate가 고착 상태를 유지하는 동안
// Action_MoveToInvestigateTarget/Action_InvestigatePerform이 이 레코드를 읽고 갱신한다.
public class InvestigationState
{
	public string TargetObjectId;
	public Vector3Int TargetPosition;

	// 5-6/5-7장: 0~1 진행도. 중단되면 50% 손실 후 남은 값을 유지하다가 재개 시 이어서 진행한다.
	public float Progress01;

	// 5-4장: 조사 중 시야·인지·반응속도 50% 페널티가 적용 중인지 — UnitFunction.UpdateFOV가 읽는다.
	public bool PenaltyActive;
}
