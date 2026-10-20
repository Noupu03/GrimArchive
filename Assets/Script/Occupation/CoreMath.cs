// 코어 상태 판정(순수) — 코어 점령 설계명세서 v0.2 3·4·9·10·19장. 상태 값은 InteractableObject(CoreEverActivated/CoreDestroyed)에 있고, 표시(UI)와 실제 기능이 어긋나지 않도록 모두 같은 함수로 판정한다.

public enum CoreActivateBlock
{
	None,
	AlreadyActivated,
	Destroyed,        // 파괴된 코어는 먼저 수복해야 한다
	BossAlive,
	RoomNotCleared,
}

public enum CoreRestoreBlock
{
	None,
	NotDestroyed,
	RoomNotCleared,
	NotEnoughResources,
}

public enum CoreStatus
{
	Incomplete,      // 불완전 활성화 — 모든 층의 초기 상태
	FullyActivated,
	Destroyed,
}

public static class CoreMath
{
	// 인류가 조사·공격할 수 있는 코어: 최초 완전 활성화를 마쳤고 현재 파괴되지 않았다(10장). 방 소유와는 무관하다.
	public static bool IsAttackable(bool everActivated, bool destroyed) => everActivated && !destroyed;

	// 건설·생산·문 제어를 쓸 수 있는가 — 담당 코어가 파괴되지 않았을 때(4장).
	public static bool FunctionsActive(bool destroyed) => !destroyed;

	// 완전 활성화 = 보스 처치 + 코어 방 클리어 후 플레이어가 실행(3장).
	public static CoreActivateBlock CheckActivate(bool everActivated, bool destroyed, bool bossAlive, bool coreRoomCleared)
	{
		if (destroyed) return CoreActivateBlock.Destroyed;
		if (everActivated) return CoreActivateBlock.AlreadyActivated;
		if (bossAlive) return CoreActivateBlock.BossAlive;
		return coreRoomCleared ? CoreActivateBlock.None : CoreActivateBlock.RoomNotCleared;
	}

	// 수복 = 코어 방 클리어 + 수복 자원(9장; 부산물 장치 조건은 장치 결합이 생길 때 추가한다).
	public static CoreRestoreBlock CheckRestore(bool destroyed, bool coreRoomCleared, bool hasResources)
	{
		if (!destroyed) return CoreRestoreBlock.NotDestroyed;
		if (!coreRoomCleared) return CoreRestoreBlock.RoomNotCleared;
		return hasResources ? CoreRestoreBlock.None : CoreRestoreBlock.NotEnoughResources;
	}

	public static CoreStatus StatusOf(bool everActivated, bool destroyed)
		=> destroyed ? CoreStatus.Destroyed : everActivated ? CoreStatus.FullyActivated : CoreStatus.Incomplete;

	public static string Describe(CoreStatus status) => status switch
	{
		CoreStatus.Incomplete => "불완전 활성화",
		CoreStatus.FullyActivated => "완전 활성화",
		_ => "파괴됨",
	};

	public static string Describe(CoreActivateBlock block) => block switch
	{
		CoreActivateBlock.None => "활성화할 수 있습니다",
		CoreActivateBlock.AlreadyActivated => "이미 완전 활성화한 코어입니다",
		CoreActivateBlock.Destroyed => "파괴된 코어입니다 — 먼저 수복해야 합니다",
		CoreActivateBlock.BossAlive => "해당 층의 보스가 아직 살아 있습니다",
		_ => "코어 방에 적이 남아 있습니다(은신 포함)",
	};

	public static string Describe(CoreRestoreBlock block) => block switch
	{
		CoreRestoreBlock.None => "수복할 수 있습니다",
		CoreRestoreBlock.NotDestroyed => "파괴되지 않은 코어입니다",
		CoreRestoreBlock.RoomNotCleared => "코어 방에 적이 남아 있어 수복할 수 없습니다",
		_ => "수복 자원이 부족합니다",
	};
}
