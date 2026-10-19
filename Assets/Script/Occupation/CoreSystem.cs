using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Haare.Util.Logger;
using GrimArchive.Wave;

// 코어 상태 전이(세션 연결부) — 코어 점령 설계명세서 v0.2 3·4·9·10장. 순수 판정은 CoreMath, 상태 값은 InteractableObject(CoreEverActivated/CoreDestroyed)에 있다.
// 코어는 층당 하나(보스방, GameSession.SpawnBossCores)이고 담당 범위는 그 층 전체다 — 분할 코어가 생기면 AreFunctionsActive/TryGetFloorCore의 조회 한 곳만 바꾸면 된다(2장).
public class CoreSystem
{
	// 수복 비용(자리표시자, 문서가 수치를 정하지 않음 — 9장 "수복 총비용·시간·수행 주체는 후속 확정"). 수복은 명령 즉시 완료된다.
	public const int RestoreStoneCost = 300;

	private static readonly Color IncompleteTint = Color.white;
	private static readonly Color ActivatedTint = new Color(0.55f, 1f, 1f, 1f);
	private static readonly Color DestroyedTint = new Color(0.28f, 0.28f, 0.3f, 1f);

	private IObjectResolver _resolver;
	private GameSession Session => _cachedSession ??= _resolver.Resolve<GameSession>();
	private GameSession _cachedSession;
	private RoomClearSystem _roomClear;

	[Inject]
	public void Construct(IObjectResolver resolver, RoomClearSystem roomClear)
	{
		_resolver = resolver;
		_roomClear = roomClear;
	}

	// ─────────────────────────── 조회 ───────────────────────────

	// 층당 코어는 하나이고 위치가 바뀌지 않아 처음 찾은 결과를 기억한다(건물 생산 틱이 매 프레임 묻는다). 기억한 오브젝트가 objectGrid에서 사라졌으면 다시 찾는다.
	private readonly Dictionary<int, (Room room, InteractableObject core)> _floorCoreCache = new Dictionary<int, (Room, InteractableObject)>();

	public bool TryGetFloorCore(int floor, out Room room, out InteractableObject core)
	{
		room = null;
		core = null;
		var session = Session;
		if (session?.allRooms == null) return false;

		if (_floorCoreCache.TryGetValue(floor, out var cached)
			&& session.objectGrid.TryGetValue(cached.room.CorePosition, out InteractableObject live) && live == cached.core)
		{
			room = cached.room;
			core = cached.core;
			return true;
		}

		foreach (var r in session.allRooms)
		{
			if (r == null || r.Floor != floor || r.CoreObjectId == null) continue;
			if (!session.objectGrid.TryGetValue(r.CorePosition, out core) || core == null) continue;
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
		core = null;
		var session = Session;
		if (session == null || !session.objectGrid.TryGetValue(pos, out core) || core?.Tags == null || !core.Tags.Contains(GameSession.CoreTag)) { core = null; return false; }
		return session.roomGrid.TryGetValue(pos, out room) && room != null;
	}

	// 건설·생산·배치 모드·문 제어·자원 생산 등을 쓸 수 있는 층인가 — 담당 코어가 파괴됐으면 false. 코어가 없는 층(0층 등)은 제한이 없다.
	public bool AreFunctionsActive(int floor)
		=> !TryGetFloorCore(floor, out _, out InteractableObject core) || CoreMath.FunctionsActive(core.CoreDestroyed);

	public bool IsFloorCoreEverActivated(int floor)
		=> TryGetFloorCore(floor, out _, out InteractableObject core) && core.CoreEverActivated;

	// 인류가 조사·공격할 수 있는 코어인가 — 최초 완전 활성화를 마쳤고 파괴되지 않았다.
	public static bool IsAttackable(InteractableObject core)
		=> core != null && CoreMath.IsAttackable(core.CoreEverActivated, core.CoreDestroyed);

	public bool IsBossAlive(int floor)
	{
		var session = Session;
		if (session == null) return false;
		foreach (var u in session.units)
			if (u != null && u.hp > 0f && u.currentFloor == floor && u.unitType is BossGolem) return true;
		return false;
	}

	public CoreActivateBlock CheckActivate(Room room, InteractableObject core)
		=> CoreMath.CheckActivate(core.CoreEverActivated, core.CoreDestroyed, IsBossAlive(room.Floor), _roomClear.IsCleared(room));

	public CoreRestoreBlock CheckRestore(Room room, InteractableObject core)
		=> CoreMath.CheckRestore(core.CoreDestroyed, core.CoreDeviceAttached, _roomClear.IsCleared(room),
			ResourceManager.Instance != null && ResourceManager.Instance.HasEnoughResource(ResourceType.Stone, RestoreStoneCost));

	// ─────────────────────────── 전이 ───────────────────────────

	// 플레이어가 코어를 선택해 실행하는 완전 활성화(3장). 해금되는 진화·업그레이드는 아직 연결할 시스템이 없어 이력만 남긴다.
	public CoreActivateBlock TryActivate(Vector3Int corePos, bool ignoreConditions = false)
	{
		if (!TryGetCoreAt(corePos, out Room room, out InteractableObject core)) return CoreActivateBlock.Destroyed;
		CoreActivateBlock block = ignoreConditions
			? (core.CoreEverActivated ? CoreActivateBlock.AlreadyActivated : CoreActivateBlock.None)
			: CheckActivate(room, core);
		if (block != CoreActivateBlock.None) return block;

		core.CoreEverActivated = true;
		ApplyTint(core);
		LogHelper.Log(LogHelper.GAME, $"[코어] F{room.Floor} {room.RoomName} 코어 완전 활성화 — 이 층 몬스터 진화·건물 업그레이드 해금(실행 시스템은 아직 없음), 인류가 이 코어를 공격할 수 있게 됨");
		NoticeCenter.Instance?.PushMomentary($"{room.Floor}층 코어가 완전 활성화되었습니다.", NoticeCenter.QuestCompleteColor);
		NotifyHumansCoreAttackable(core);
		return CoreActivateBlock.None;
	}

	// 코어 체력이 0이 된 순간(UnitFunction.OnUpdate의 채널링 데미지 적용 직후)의 단일 진입점. 방 소유권은 바꾸지 않고 코어는 파괴 상태로 남는다.
	public void OnCoreDestroyed(Room room, InteractableObject core, Unit killer)
	{
		if (room == null || core == null || core.CoreDestroyed) return;

		core.CoreHp = 0f;
		core.CoreDestroyed = true;
		ApplyTint(core);
		Session.GetObjectVisual(core.Position)?.GetComponent<ObjectProgressBarVisual>()?.SetProgress(0f, false);

		FactionType? claimant = killer != null ? OffenseProcessor.MapToRoomFaction(killer.FactionBehavior) : null;
		if (claimant.HasValue)
		{
			GameObject coreVisual = Session.GetObjectVisual(core.Position);
			Vector3 vfxPos = coreVisual != null ? coreVisual.transform.position : new Vector3(core.Position.x + 0.5f, core.Position.y + 0.5f, 0f);
			VFXManager.SpawnCoreCaptureVfx(claimant.Value, vfxPos);
		}
		if (killer != null) SpeechSystem.Say(killer, SpeechId.CoreDestroyed);

		LogHelper.Log(LogHelper.GAME, $"[코어] F{room.Floor} {room.RoomName} 코어 파괴({killer?.unitType?.typeName}) — 이 층의 건설·생산·문 제어 등 작업 정지, 수복 명령 대기");
		SpawnSalvage(core.Position);
		NoticeCenter.Instance?.PushMomentary($"{room.Floor}층 코어가 파괴되었습니다. 건설·생산이 정지됩니다.", NoticeCenter.WarningColor);
	}

	// 플레이어의 수복 명령(9장) — 비용을 쓰고 즉시 완료한다. 파괴 전 활성화 이력(CoreEverActivated)은 그대로 유지된다. free는 debug 전용.
	public CoreRestoreBlock TryRestore(Vector3Int corePos, bool free = false)
	{
		if (!TryGetCoreAt(corePos, out Room room, out InteractableObject core)) return CoreRestoreBlock.NotDestroyed;
		// free(debug 전용)는 방 클리어·자원 조건을 건너뛴다 — 부산물 장치 결합만은 그대로 막는다.
		CoreRestoreBlock block = free
			? (!core.CoreDestroyed ? CoreRestoreBlock.NotDestroyed : core.CoreDeviceAttached ? CoreRestoreBlock.DeviceAttached : CoreRestoreBlock.None)
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

	// 코어가 공격 가능해진 순간(완전 활성화·수복) 그 코어를 이미 아는 인류에게 "미처리 코어"로 알린다 — 발견 이벤트는 처음 본 순간에만 나서(UnitFunction.CastRay의 firstTouch) 활성화 전에 본 코어는 따로 알리지 않으면 영영 보고되지 않는다.
	// 활성화·수복이 세계에 드러나는 사건이라는 해석이다(문서가 인류의 인지 경로를 정하지 않음). 파티당 한 명만 보고를 시작하게 한다.
	private void NotifyHumansCoreAttackable(InteractableObject core)
	{
		var session = Session;
		if (session == null) return;
		var notified = new HashSet<Party>();
		foreach (var u in session.units)
		{
			if (!(u is Human h) || h.hp <= 0f || h.party == null || notified.Contains(h.party)) continue;
			if (!h.personalMap.IsObjectKnown(core.Id)) continue;
			notified.Add(h.party);
			PartyCoreReportSystem.OnCoreDiscovered(h, core);
		}
	}

	// ─────────────────────────── 코어 잔해 (8장) ───────────────────────────

	// 코어가 파괴되면 고정 코어와 별개로 회수·운반하는 잔해 오브젝트가 생긴다. 인류가 주워 던전 밖으로 반출하면 부산물 장치 연구가 시작되는 기준(7장)이 되지만, 연구·반입·결합은 아직 없어 반출한 웨이브 번호만 기록한다.
	public const string SalvageTag = "Object/Passable/CoreSalvage";

	private readonly List<InteractableObject> _salvages = new List<InteractableObject>();
	private readonly List<int> _salvageExportWaves = new List<int>();
	public IReadOnlyList<int> SalvageExportWaves => _salvageExportWaves;

	// 아직 줍지 않은 잔해가 하나라도 있는가 — 인류 AI가 매 틱 묻기 전에 싸게 걸러 내는 용도.
	public bool HasAnySalvage => _salvages.Count > 0;

	// room 안에서 아직 아무도 줍지 않은 잔해 — 줍힌 잔해는 objectGrid에서 사라지므로 목록에서도 정리한다.
	public InteractableObject FindSalvageInRoom(Room room)
	{
		var session = Session;
		if (session == null || room == null) return null;
		for (int i = _salvages.Count - 1; i >= 0; i--)
		{
			InteractableObject s = _salvages[i];
			if (s == null || s.IsCollected || !session.objectGrid.TryGetValue(s.Position, out InteractableObject live) || live != s)
			{
				_salvages.RemoveAt(i);
				continue;
			}
			if (session.roomGrid.TryGetValue(s.Position, out Room r) && r == room) return s;
		}
		return null;
	}

	public static bool PartyHasCarrier(Party party)
	{
		if (party == null) return false;
		foreach (var m in party.Members)
			if (m != null && m.hp > 0f && m.carriesCoreSalvage) return true;
		return false;
	}

	private void SpawnSalvage(Vector3Int near)
	{
		var session = Session;
		Vector3Int pos = session.objectGrid.ContainsKey(near) ? session.FindNearbyFreeObjectTile(near) : near;
		var salvage = new InteractableObject("CoreSalvage_" + System.Guid.NewGuid().ToString().Substring(0, 4), pos, 120f, 0f, new List<string> { SalvageTag });
		session.SpawnObject(salvage, Color.cyan);
		if (!session.objectGrid.TryGetValue(pos, out InteractableObject placed) || placed != salvage) return; // 자리가 없어 놓지 못했다
		_salvages.Add(salvage);
	}

	// 인류가 잔해를 줍는다 — 오브젝트는 사라지고 줍은 인류가 들고 이동한다.
	public void OnSalvagePickedUp(Human carrier, InteractableObject salvage)
	{
		if (carrier == null || salvage == null || salvage.IsCollected) return;
		Session.CollectObject(salvage.Position);
		carrier.carriesCoreSalvage = true;
		SpeechSystem.Say(carrier, SpeechId.SalvagePicked);
		LogHelper.Log(LogHelper.GAME, $"[코어] {carrier.name}이(가) 코어 잔해를 획득 — 파티가 던전 밖으로 반출한다");
	}

	// 운반자가 죽으면 잔해가 사망 위치 근처에 다시 놓인다(운반 중 공격받으면 떨어질 수 있다는 8장 규칙의 가장 단순한 형태 — 확률 낙하는 문서에 수치가 없어 만들지 않았다).
	public void OnCarrierDied(Human carrier)
	{
		if (carrier == null || !carrier.carriesCoreSalvage) return;
		carrier.carriesCoreSalvage = false;
		SpawnSalvage(new Vector3Int(carrier.position.x, carrier.position.y, carrier.currentFloor));
		LogHelper.Log(LogHelper.GAME, $"[코어] 운반자 {carrier.name} 사망 — 잔해가 사망 위치에 다시 놓임");
	}

	// 잔해를 든 인류가 실제로 던전 밖으로 나갔다(RetreatExit.Complete) — 반출 성공. 후퇴·도주로 나간 경우도 같다.
	public void OnSalvageExported(Human carrier)
	{
		if (carrier == null || !carrier.carriesCoreSalvage) return;
		carrier.carriesCoreSalvage = false;
		int wave = HumanWaveManager.Instance != null ? HumanWaveManager.Instance.WaveNumber : 0;
		_salvageExportWaves.Add(wave);
		LogHelper.Log(LogHelper.GAME, $"[코어] 코어 잔해 반출 성공(웨이브 {wave}) — 부산물 장치 연구는 반출 뒤 정규 2웨이브, 반입은 3번째 웨이브부터(연구·반입·결합은 아직 구현되지 않음)");
		NoticeCenter.Instance?.PushMomentary("인류가 코어 잔해를 던전 밖으로 반출했습니다.", NoticeCenter.WarningColor);
	}

	// debug 전용 — 인류의 공격 없이 코어를 파괴 상태로 만든다.
	public void DebugDestroy(Vector3Int corePos)
	{
		if (TryGetCoreAt(corePos, out Room room, out InteractableObject core)) OnCoreDestroyed(room, core, null);
	}

	public void ApplyTint(InteractableObject core)
	{
		var sr = Session.GetObjectVisual(core.Position)?.GetComponent<SpriteRenderer>();
		if (sr == null) return;
		sr.color = CoreMath.StatusOf(core.CoreEverActivated, core.CoreDestroyed, core.CoreDeviceAttached) switch
		{
			CoreStatus.FullyActivated => ActivatedTint,
			CoreStatus.Destroyed => DestroyedTint,
			CoreStatus.DeviceAttached => DestroyedTint,
			_ => IncompleteTint,
		};
	}
}
