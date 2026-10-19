// 코어 상태 판정(순수, 부수효과 없음) — 코어 점령 설계명세서 v0.2 3·4·9·10·19장. 상태 자체는 InteractableObject(CoreEverActivated/CoreDestroyed/CoreDeviceAttached)에 있고
// 여기서는 그 값과 주변 조건(보스 생존·방 클리어·자원)으로 "무엇이 가능한가"만 판정한다. 호출부(CoreSystem·AI·UI)가 같은 함수를 써야 표시와 실제 기능이 어긋나지 않는다(19장).

// 코어 활성화 불가 사유 — None이면 활성화할 수 있다(보스 처치 + 코어 방 클리어 후 플레이어가 실행, 3장).
public enum CoreActivateBlock
{
	None,
	AlreadyActivated, // 이미 완전 활성화한 코어 — 보스 처치·활성화는 반복하지 않는다
	Destroyed,        // 파괴된 코어는 먼저 수복해야 한다
	BossAlive,        // 해당 층 보스가 살아 있음
	RoomNotCleared,   // 코어 방에 적대 생존 유닛이 남음
}

// 코어 수복 불가 사유 — None이면 수복 명령을 낼 수 있다(9장: 부산물 장치 없음 + 코어 방 클리어 + 수복 자원).
public enum CoreRestoreBlock
{
	None,
	NotDestroyed,
	DeviceAttached,     // 코어 부산물 장치가 남아 있으면 수복 불가
	RoomNotCleared,
	NotEnoughResources,
}

public enum CoreStatus
{
	Incomplete,      // 불완전 활성화 — 모든 층의 초기 상태
	FullyActivated,  // 완전 활성화
	Destroyed,       // 파괴 — 고정 위치에 파괴 스프라이트로 남는다
	DeviceAttached,  // 파괴된 코어에 부산물 장치가 결합됨(7장 — 지금은 결합 경로가 없어 표시용 구조만)
}

public static class CoreMath
{
	// 인류가 조사·공격할 수 있는 코어: 최초 완전 활성화를 마쳤고 현재 파괴되지 않았다(10장). 방 소유 진영과는 무관하다.
	public static bool IsAttackable(bool everActivated, bool destroyed) => everActivated && !destroyed;

	// 건설·생산·배치 모드·문 제어·업그레이드·시체 정제·자원 생산을 쓸 수 있는가 — 담당 코어가 파괴되지 않았을 때(4장). 방 확보 여부는 별개로 호출부가 본다.
	public static bool FunctionsActive(bool destroyed) => !destroyed;

	public static CoreActivateBlock CheckActivate(bool everActivated, bool destroyed, bool bossAlive, bool coreRoomCleared)
	{
		if (destroyed) return CoreActivateBlock.Destroyed;
		if (everActivated) return CoreActivateBlock.AlreadyActivated;
		if (bossAlive) return CoreActivateBlock.BossAlive;
		if (!coreRoomCleared) return CoreActivateBlock.RoomNotCleared;
		return CoreActivateBlock.None;
	}

	public static CoreRestoreBlock CheckRestore(bool destroyed, bool deviceAttached, bool coreRoomCleared, bool hasResources)
	{
		if (!destroyed) return CoreRestoreBlock.NotDestroyed;
		if (deviceAttached) return CoreRestoreBlock.DeviceAttached;
		if (!coreRoomCleared) return CoreRestoreBlock.RoomNotCleared;
		if (!hasResources) return CoreRestoreBlock.NotEnoughResources;
		return CoreRestoreBlock.None;
	}

	public static CoreStatus StatusOf(bool everActivated, bool destroyed, bool deviceAttached)
	{
		if (destroyed) return deviceAttached ? CoreStatus.DeviceAttached : CoreStatus.Destroyed;
		return everActivated ? CoreStatus.FullyActivated : CoreStatus.Incomplete;
	}

	public static string Describe(CoreStatus status) => status switch
	{
		CoreStatus.Incomplete => "불완전 활성화",
		CoreStatus.FullyActivated => "완전 활성화",
		CoreStatus.Destroyed => "파괴됨",
		CoreStatus.DeviceAttached => "파괴됨(부산물 장치 결합)",
		_ => status.ToString(),
	};

	public static string Describe(CoreActivateBlock block) => block switch
	{
		CoreActivateBlock.None => "활성화할 수 있습니다",
		CoreActivateBlock.AlreadyActivated => "이미 완전 활성화한 코어입니다",
		CoreActivateBlock.Destroyed => "파괴된 코어입니다 — 먼저 수복해야 합니다",
		CoreActivateBlock.BossAlive => "해당 층의 보스가 아직 살아 있습니다",
		CoreActivateBlock.RoomNotCleared => "코어 방에 적이 남아 있습니다(은신 포함)",
		_ => block.ToString(),
	};

	public static string Describe(CoreRestoreBlock block) => block switch
	{
		CoreRestoreBlock.None => "수복할 수 있습니다",
		CoreRestoreBlock.NotDestroyed => "파괴되지 않은 코어입니다",
		CoreRestoreBlock.DeviceAttached => "코어 부산물 장치가 남아 있어 수복할 수 없습니다",
		CoreRestoreBlock.RoomNotCleared => "코어 방에 적이 남아 있어 수복할 수 없습니다",
		CoreRestoreBlock.NotEnoughResources => "수복 자원이 부족합니다",
		_ => block.ToString(),
	};
}
