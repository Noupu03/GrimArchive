using System.Collections.Generic;

// 07문서 3장(정보 보유 구조): "전파 정보"/"일시 간접입력"을 유닛별로 개별 보유하는 컴포넌트.
// PerceptionComponent/MemoryComponent와 동일 패턴(Unit.Components에 등록, Unit이 캐시 프로퍼티로 노출).
public class PropagationComponent : IUnitComponent
{
	private Unit _owner;

	// 전파 정보 — 대상 키(적 유닛=Unit 참조, 오브젝트=InteractableObject.Id)별 마지막 확인 위치·시각.
	public readonly Dictionary<object, PropagatedInfoRecord> PropagatedInfo = new Dictionary<object, PropagatedInfoRecord>();

	// 일시 간접입력 — 유닛당 최대 1건(07-A 7-3장)만 보류.
	public PendingSoundReaction PendingSound;

	// 07문서 10장: 이 유닛이 "진행 중" 정보를 직접 전파받은 상호작용 유닛 → 그 시점 상호작용 인스턴스
	// 토큰(PropagationSystem.GetInteractionToken, 실제로는 currentTrapInteraction/currentInvestigation/
	// currentCoreInteraction 참조 자체). 보호 포메이션 참여 자격(직접 시야 확인만으로는 참여하지 않음)에
	// 쓰인다 — 2026-08-06: Unit만 키로 쓰던 HashSet에서 인스턴스 토큰까지 저장하는 Dictionary로 교체해
	// "예전 상호작용에 대한 낡은 알림이 완전히 다른 새 상호작용에도 유효자격을 주는" 문제를 없앴다.
	public readonly Dictionary<Unit, object> NotifiedInteractionTokens = new Dictionary<Unit, object>();

	public PropagationComponent() { }

	public PropagationComponent(Unit owner)
	{
		_owner = owner;
	}

	public void OnUpdate(float deltaTime) { }
	public void OnDespawn() { }
}
