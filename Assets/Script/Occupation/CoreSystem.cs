using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Haare.Util.Logger;

// 코어 상태 전이(세션 연결부) — 코어 점령 설계명세서 v0.2 3·4·9·10장. 판정은 CoreMath, 상태 값은 InteractableObject(CoreEverActivated/CoreDestroyed), 잔해는 CoreSalvage.
// 코어는 층당 하나(보스방)이고 담당 범위는 그 층 전체다 — 분할 코어가 생기면 TryGetFloorCore/AreFunctionsActive 조회만 바꾸면 된다.
public class CoreSystem
{
	// 수복 비용(자리표시자 — 9장은 비용·시간·수행 주체를 후속 확정으로 남김). 수복은 명령 즉시 완료된다.
	public const int RestoreStoneCost = 300;

	private static readonly Color IncompleteTint = Color.white;
	private static readonly Color ActivatedTint = new Color(0.55f, 1f, 1f, 1f);
	private static readonly Color DestroyedTint = new Color(0.28f, 0.28f, 0.3f, 1f);

	private IObjectResolver _resolver;
	private GameSession Session => _cachedSession ??= _resolver.Resolve<GameSession>();
	private GameSession _cachedSession;
	private RoomClearSystem _roomClear;
	private CoreSalvage _salvage;
	// 층당 코어는 하나이고 위치가 바뀌지 않아 처음 찾은 결과를 기억한다(건물 생산 틱이 매 프레임 묻는다).
	private readonly Dictionary<int, (Room room, InteractableObject core)> _floorCoreCache = new Dictionary<int, (Room, InteractableObject)>();

	public CoreSalvage Salvage => _salvage ??= new CoreSalvage(Session);

	[Inject]
	public void Construct(IObjectResolver resolver, RoomClearSystem roomClear)
	{
		_resolver = resolver;
		_roomClear = roomClear;
	}

	// ── 조회 ──

	public bool TryGetFloorCore(int floor, out Room room, out InteractableObject core)
	{
		room = null;
		core = null;
		if (Session?.allRooms == null) return false;

		if (_floorCoreCache.TryGetValue(floor, out var cached)
			&& Session.objectGrid.TryGetValue(cached.room.CorePosition, out InteractableObject live) && live == cached.core)
		{
			room = cached.room;
			core = cached.core;
			return true;
		}

		foreach (var r in Session.allRooms)
		{
			if (r == null || r.Floor != floor || r.CoreObjectId == null) continue;
			if (!Session.objectGrid.TryGetValue(r.CorePosition, out core) || core == null) continue;
			room = r;
			_floorCoreCache[floor] = (r, core);
			return true;
		}
		core = null;
		return false;
	}

	public bool TryGetCoreAt(Vector3Int pos, out Room room, out InteractableObject core)
	{
		room = null;
		if (Session == null || !Session.objectGrid.TryGetValue(pos, out core) || core.Tags == null || !core.Tags.Contains(GameSession.CoreTag))
		{
			core = null;
			return false;
		}
		return Session.roomGrid.TryGetValue(pos, out room) && room != null;
	}

	// 건설·생산·문 제어·자원 생산을 쓸 수 있는 층인가 — 담당 코어가 파괴됐으면 false. 코어가 없는 층(0층 등)은 제한이 없다.
	public bool AreFunctionsActive(int floor)
		=> !TryGetFloorCore(floor, out _, out InteractableObject core) || CoreMath.FunctionsActive(core.CoreDestroyed);

	public bool IsFloorCoreEverActivated(int floor)
		=> TryGetFloorCore(floor, out _, out InteractableObject core) && core.CoreEverActivated;

	public static bool IsAttackable(InteractableObject core)
		=> core != null && CoreMath.IsAttackable(core.CoreEverActivated, core.CoreDestroyed);

	public CoreActivateBlock CheckActivate(Room room, InteractableObject core)
		=> CoreMath.CheckActivate(core.CoreEverActivated, core.CoreDestroyed, IsBossAlive(room.Floor), _roomClear.IsCleared(room));

	public CoreRestoreBlock CheckRestore(Room room, InteractableObject core)
		=> CoreMath.CheckRestore(core.CoreDestroyed, _roomClear.IsCleared(room),
			ResourceManager.Instance != null && ResourceManager.Instance.HasEnoughResource(ResourceType.Stone, RestoreStoneCost));

	private bool IsBossAlive(int floor)
	{
		foreach (var u in Session.units)
			if (u != null && u.hp > 0f && u.currentFloor == floor && u.unitType is BossGolem) return true;
		return false;
	}

	// ── 전이 ──

	// 플레이어가 코어를 선택해 실행하는 완전 활성화. 해금되는 진화·업그레이드는 연결할 시스템이 없어 이력만 남긴다. ignoreConditions는 debug 전용.
	public CoreActivateBlock TryActivate(Vector3Int corePos, bool ignoreConditions = false)
	{
		if (!TryGetCoreAt(corePos, out Room room, out InteractableObject core)) return CoreActivateBlock.Destroyed;
		CoreActivateBlock block = ignoreConditions
			? (core.CoreEverActivated ? CoreActivateBlock.AlreadyActivated : CoreActivateBlock.None)
			: CheckActivate(room, core);
		if (block != CoreActivateBlock.None) return block;

		core.CoreEverActivated = true;
		ApplyTint(core);
		LogHelper.Log(LogHelper.GAME, $"[코어] F{room.Floor} {room.RoomName} 코어 완전 활성화 — 인류가 이 코어를 공격할 수 있게 됨(진화·업그레이드 해금은 실행 시스템이 아직 없다)");
		NoticeCenter.Instance?.PushMomentary($"{room.Floor}층 코어가 완전 활성화되었습니다.", NoticeCenter.QuestCompleteColor);
		NotifyHumansCoreAttackable(core);
		return CoreActivateBlock.None;
	}

	// 코어 체력이 0이 된 순간(UnitFunction.OnUpdate의 채널링 직후)의 단일 진입점. 방 소유권은 바꾸지 않고 코어는 파괴 상태로 남는다.
	public void OnCoreDestroyed(Room room, InteractableObject core, Unit killer)
	{
		if (room == null || core == null || core.CoreDestroyed) return;

		core.CoreHp = 0f;
		core.CoreDestroyed = true;
		ApplyTint(core);
		GameObject visual = Session.GetObjectVisual(core.Position);
		visual?.GetComponent<ObjectProgressBarVisual>()?.SetProgress(0f, false);

		FactionType? claimant = killer != null ? OffenseProcessor.MapToRoomFaction(killer.FactionBehavior) : null;
		if (claimant.HasValue)
			VFXManager.SpawnCoreCaptureVfx(claimant.Value, visual != null ? visual.transform.position : new Vector3(core.Position.x + 0.5f, core.Position.y + 0.5f, 0f));
		if (killer != null) SpeechSystem.Say(killer, SpeechId.CoreDestroyed);

		Salvage.Spawn(core.Position);
		LogHelper.Log(LogHelper.GAME, $"[코어] F{room.Floor} {room.RoomName} 코어 파괴({killer?.unitType?.typeName}) — 이 층의 건설·생산 등 작업 정지, 수복 명령 대기");
		NoticeCenter.Instance?.PushMomentary($"{room.Floor}층 코어가 파괴되었습니다. 건설·생산이 정지됩니다.", NoticeCenter.WarningColor);
	}

	// 플레이어의 수복 명령 — 비용을 쓰고 즉시 완료한다. 활성화 이력은 유지된다. free는 방 클리어·자원 조건을 건너뛰는 debug 전용.
	public CoreRestoreBlock TryRestore(Vector3Int corePos, bool free = false)
	{
		if (!TryGetCoreAt(corePos, out Room room, out InteractableObject core)) return CoreRestoreBlock.NotDestroyed;
		CoreRestoreBlock block = free
			? (core.CoreDestroyed ? CoreRestoreBlock.None : CoreRestoreBlock.NotDestroyed)
			: CheckRestore(room, core);
		if (block != CoreRestoreBlock.None) return block;
		if (!free && (ResourceManager.Instance == null || !ResourceManager.Instance.TryConsumeResource(ResourceType.Stone, RestoreStoneCost))) return CoreRestoreBlock.NotEnoughResources;

		core.CoreDestroyed = false;
		core.CoreHp = core.CoreMaxHp;
		core.TimeSinceLastDamaged = float.MaxValue;
		ApplyTint(core);
		LogHelper.Log(LogHelper.GAME, $"[코어] F{room.Floor} {room.RoomName} 코어 수복 완료 — 정지됐던 작업 재개(활성화 이력 {(core.CoreEverActivated ? "유지" : "없음")})");
		NoticeCenter.Instance?.PushMomentary($"{room.Floor}층 코어가 수복되었습니다. 정지된 작업이 재개됩니다.", NoticeCenter.QuestCompleteColor);
		NotifyHumansCoreAttackable(core);
		return CoreRestoreBlock.None;
	}

	public void DebugDestroy(Vector3Int corePos)
	{
		if (TryGetCoreAt(corePos, out Room room, out InteractableObject core)) OnCoreDestroyed(room, core, null);
	}

	// 코어가 공격 가능해진 순간(활성화·수복) 그 코어를 이미 아는 인류에게 미처리 코어로 알린다 — 발견 이벤트는 처음 본 순간 한 번뿐이라 활성화 전에 본 코어는 이렇게 알리지 않으면 영영 보고되지 않는다
	// (활성화·수복이 세계에 드러나는 사건이라는 해석 — 문서가 인류의 인지 경로를 정하지 않음). 파티당 한 명만 보고를 시작한다.
	private void NotifyHumansCoreAttackable(InteractableObject core)
	{
		var notified = new HashSet<Party>();
		foreach (var u in Session.units)
		{
			if (!(u is Human h) || h.hp <= 0f || h.party == null || notified.Contains(h.party) || !h.personalMap.IsObjectKnown(core.Id)) continue;
			notified.Add(h.party);
			PartyCoreReportSystem.OnCoreDiscovered(h, core);
		}
	}

	private void ApplyTint(InteractableObject core)
	{
		var sr = Session.GetObjectVisual(core.Position)?.GetComponent<SpriteRenderer>();
		if (sr == null) return;
		sr.color = CoreMath.StatusOf(core.CoreEverActivated, core.CoreDestroyed) switch
		{
			CoreStatus.FullyActivated => ActivatedTint,
			CoreStatus.Destroyed => DestroyedTint,
			_ => IncompleteTint,
		};
	}
}
