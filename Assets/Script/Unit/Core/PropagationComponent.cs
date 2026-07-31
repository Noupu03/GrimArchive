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

	// 17장: 공격자 미인지 + 방향만 아는 경우의 마지막 공격 방향.
	public PendingAttackDirection PendingAttackDir;

	// 07문서 10장: 이 유닛이 "진행 중" 정보를 직접 전파받은 상호작용 유닛 목록 — 보호 포메이션 참여
	// 자격(직접 시야 확인만으로는 참여하지 않음)에 쓰인다. PropagationSystem.NotifyInteractionStarted가 채운다.
	public readonly HashSet<Unit> NotifiedActiveInteractions = new HashSet<Unit>();

	public PropagationComponent() { }

	public PropagationComponent(Unit owner)
	{
		_owner = owner;
	}

	public void OnUpdate(float deltaTime) { }
	public void OnDespawn() { }
}
