using UnityEngine;

// 5장: 이 유닛이 지금 진행 중인 조사의 하위 상태. Goal_Investigate가 고착 상태를 유지하는 동안
// Action_MoveToInvestigateTarget/Action_InvestigatePerform이 이 레코드를 읽고 갱신한다.
public class InvestigationState
{
	public string TargetObjectId;
	public Vector3Int TargetPosition;

	// 01번 문서 4장: 대상이 알려진 오브젝트가 아니라 "시야로 존재는 확인했지만 인지·안전 확인이
	// 끝나지 않은" 맨 타일일 때 true — TargetObjectId는 null로 남는다.
	public bool IsTileOnly;

	// 5-6/5-7장: 0~1 진행도. 중단되면 50% 손실 후 남은 값을 유지하다가 재개 시 이어서 진행한다.
	public float Progress01;

	// 5-4장: 조사 중 시야·인지·반응속도 50% 페널티가 적용 중인지 — UnitFunction.UpdateFOV가 읽는다.
	public bool PenaltyActive;

	// 이 대상이 자기 파티의 임무에 해당하는 '유지 우선 파티 목표 상호작용'이면 true(03번 1장 표). true면 TacticalFSMState.CanInvestigate가 '보호자 피격' 중단 예외를 적용해 보호자가 대응하는 동안 유지하며, 본인 피격은 이 플래그와 무관하게 항상 중단한다. 판정 기준은 Party.HasKnownRecoverableInRoom과 같은 'Loot' 태그뿐이다(다른 파티종류는 01번 3장이 스텁으로 남겨 확장하지 않는다).
	public bool IsPartyGoalTarget;
}
