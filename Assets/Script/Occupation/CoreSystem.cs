using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Haare.Util.Logger;

// 코어 상태 전이(세션 연결부) — 코어 점령 설계명세서 v0.2 3·4·9·10장 및 자원경제 설계명세서 v0.2 2·15·19·25-1항 연결.
// 코어는 층당 하나(보스방)이고 담당 범위는 그 층 전체다 — 분할 코어가 생기면 TryGetFloorCore/AreFunctionsActive 조회만 바꾸면 된다.
public class CoreSystem
{
	// 기본 수복비·기본 수복시간·수복 중 오브젝트 최대 HP(자리표시자 — 21항 콘텐츠 데이터 확정 전 검증용 초기값).
	public const int RestoreLifeEnergyCost = ResourceManager.CoreRestoreBaseLifeEnergyCost;
	public const double RestoreBaseWorkSeconds = 15.0;
	public const float RestoreObjectMaxHp = 500f;

	private static readonly Color IncompleteTint = Color.white;
	private static readonly Color ActivatedTint = new Color(0.55f, 1f, 1f, 1f);
	private static readonly Color DestroyedTint = new Color(0.28f, 0.28f, 0.3f, 1f);
	private static readonly Color RestoringTint = new Color(0.65f, 0.85f, 0.45f, 1f);

	private IObjectResolver _resolver;
	private GameSession Session => _cachedSession ??= _resolver.Resolve<GameSession>();
	private GameSession _cachedSession;
	private RoomClearSystem _roomClear;
	private CoreSalvage _salvage;

	private readonly Dictionary<int, (Room room, InteractableObject core)> _floorCoreCache = new Dictionary<int, (Room, InteractableObject)>();
	private readonly Dictionary<Vector3Int, CoreRestoreSession> _activeRestores = new Dictionary<Vector3Int, CoreRestoreSession>();
	private bool _anyCoreEverActivated;

	public CoreSalvage Salvage => _salvage ??= new CoreSalvage(Session);

	// 최초 1회라도 어느 코어든 완전 활성화된 이력이 있는지(파괴 후에도 영구 유지 — 영혼 파편 소비·연구시설 해금 기준, 2-표1, 19-표2).
	public bool HasAnyCoreEverBeenActivated
	{
		get
		{
			if (_anyCoreEverActivated) return true;
			if (Session?.allRooms == null) return false;
			foreach (var r in Session.allRooms)
			{
				if (r == null || r.CoreObjectId == null) continue;
				if (Session.objectGrid.TryGetValue(r.CorePosition, out var c) && c != null && c.CoreEverActivated)
				{
					_anyCoreEverActivated = true;
					return true;
				}
			}
			return false;
		}
	}

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

	public CoreRestoreSession GetActiveRestoreAt(Vector3Int corePos)
		=> _activeRestores.TryGetValue(corePos, out var s) && !s.IsEnded ? s : null;

	public bool HasAvailableSalvageInCoreRoom(Room coreRoom, out InteractableObject salvagePiece)
	{
		salvagePiece = Salvage.FindInRoom(coreRoom);
		return salvagePiece != null;
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

	public CoreRestoreBlock CheckRestore(Room room, InteractableObject core, bool useSalvage = false)
	{
		double neededCost = useSalvage && Salvage.FindInRoom(room) != null
			? RestoreLifeEnergyCost * 0.5
			: RestoreLifeEnergyCost;
		bool hasEnough = ResourceManager.Instance != null
			&& ResourceManager.Instance.HasEnoughResource(ResourceType.LifeEnergy, neededCost);
		return CoreMath.CheckRestore(core.CoreDestroyed, _roomClear.IsCleared(room), hasEnough);
	}

	private bool IsBossAlive(int floor)
	{
		foreach (var u in Session.units)
			if (u != null && u.hp > 0f && u.currentFloor == floor && u.unitType is BossGolem) return true;
		return false;
	}

	// ── 전이 ──

	public CoreActivateBlock TryActivate(Vector3Int corePos, bool ignoreConditions = false)
	{
		if (!TryGetCoreAt(corePos, out Room room, out InteractableObject core)) return CoreActivateBlock.Destroyed;
		CoreActivateBlock block = ignoreConditions
			? (core.CoreEverActivated ? CoreActivateBlock.AlreadyActivated : CoreActivateBlock.None)
			: CheckActivate(room, core);
		if (block != CoreActivateBlock.None) return block;

		core.CoreEverActivated = true;
		_anyCoreEverActivated = true;
		ApplyTint(core);
		PlayActivationVfx(room, core);
		LogHelper.Log(LogHelper.GAME, $"[코어] F{room.Floor} {room.RoomName} 코어 완전 활성화 — 영혼 파편 소비·연구시설·진화 해금 열림");
		NoticeCenter.Instance?.PushMomentary($"{room.Floor}층 코어가 완전 활성화되었습니다.", NoticeCenter.QuestCompleteColor);
		OpenNextFloorStair(room);
		NotifyHumansCoreAttackable(core);
		return CoreActivateBlock.None;
	}

	private void OpenNextFloorStair(Room room)
	{
		CreateMap cmap = Session.cmap;
		int next = room.Floor + 1;
		if (cmap?.map.floors == null || next >= cmap.map.floors.Length) return;
		if (!cmap.TryGetStairPosition(room.Floor, next, out Vector2Int stairPos)) return;
		if (Session.roomGrid.TryGetValue(new Vector3Int(stairPos.x, stairPos.y, room.Floor), out Room stairRoom) && stairRoom != null && stairRoom != room) return;

		if (!cmap.OpenStair(room.Floor, next)) return;
		NoticeCenter.Instance?.PushMomentary($"{room.Floor}층 계단이 열렸습니다 — {next}층으로 내려갈 수 있습니다.", NoticeCenter.QuestCompleteColor);
	}

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

		Salvage.Spawn(core.Position);
		// 코어 파괴 임무 수량 — 조사 완료와 별개 사건이다.
		if (Session?.parties != null)
			foreach (var p in Session.parties)
				if (p != null && !p.WaveEnded && p.Type == PartyType.CoreDestroy) p.Quota.OnCoreDestroyed();
		LogHelper.Log(LogHelper.GAME, $"[코어] F{room.Floor} {room.RoomName} 코어 파괴({killer?.unitType?.typeName}) — 이 층의 건설·생산 등 작업 정지, 수복 명령 대기");
		NoticeCenter.Instance?.PushMomentary($"{room.Floor}층 코어가 파괴되었습니다. 건설·생산이 정지됩니다.", NoticeCenter.WarningColor);
	}

	// 명세서 v0.2 19항: 생명 에너지 선배정 + 선택적 코어 잔해 1개(기본 수복비 50% 충당) + 현장 작업자 수복 개시.
	public bool TryBeginRestoreOrder(Vector3Int corePos, bool useSalvageIfAvailable, out string failureMessage)
	{
		failureMessage = string.Empty;
		if (!TryGetCoreAt(corePos, out Room room, out InteractableObject core))
		{
			failureMessage = "코어를 찾을 수 없습니다.";
			return false;
		}
		if (GetActiveRestoreAt(corePos) != null)
		{
			failureMessage = "이미 코어 수복 작업이 등록되어 있습니다.";
			return false;
		}
		if (ResourceManager.Instance == null)
		{
			failureMessage = "자원 관리자가 초기화되지 않았습니다.";
			return false;
		}

		InteractableObject salvagePiece = useSalvageIfAvailable ? Salvage.FindInRoom(room) : null;
		bool bindSalvage = salvagePiece != null;

		if (!CoreRestoreSession.TryCreateOrder(
			RestoreLifeEnergyCost,
			RestoreBaseWorkSeconds,
			RestoreObjectMaxHp,
			core.CoreDestroyed,
			_roomClear.IsCleared(room),
			hasHumanByproductDevice: false,
			useOnSiteSalvage: bindSalvage,
			onSiteSalvageOrNull: salvagePiece,
			ResourceManager.Instance.LifeEnergyAccount,
			out CoreRestoreSession session,
			out failureMessage))
		{
			if (!string.IsNullOrEmpty(failureMessage))
				NoticeCenter.Instance?.PushMomentary(failureMessage, NoticeCenter.WarningColor);
			return false;
		}

		// 수복 진행 중 잔해는 해당 수복에 묶여 다른 수복이나 인류 반출에 쓰이지 않는다(19-표1).
		if (bindSalvage && salvagePiece != null)
			Session.CollectObject(salvagePiece.Position);

		_activeRestores[corePos] = session;
		ApplyTint(core);
		LogHelper.Log(LogHelper.GAME, $"[코어] F{room.Floor} {room.RoomName} 수복 등록 (잔해 사용: {bindSalvage}, 선점 비용: {session.TotalRequiredCeilCost}) — 몬스터를 우클릭 배정해 작업 진행");
		NoticeCenter.Instance?.PushMomentary($"{room.Floor}층 코어 수복이 등록되었습니다. 몬스터를 코어에 우클릭해 작업자를 배정하세요.", NoticeCenter.QuestCompleteColor);
		return true;
	}

	public bool TryApproveRestoreExtraDiff(Vector3Int corePos)
	{
		CoreRestoreSession session = GetActiveRestoreAt(corePos);
		if (session == null || ResourceManager.Instance == null) return false;
		if (!session.TryApproveExtraDiffManually(ResourceManager.Instance.LifeEnergyAccount, out string failMsg))
		{
			if (!string.IsNullOrEmpty(failMsg))
				NoticeCenter.Instance?.PushMomentary(failMsg, NoticeCenter.WarningColor);
			return false;
		}
		NoticeCenter.Instance?.PushMomentary("추가 수복비를 지불했습니다. 수복을 재개할 수 있습니다.", NoticeCenter.QuestCompleteColor);
		return true;
	}

	public bool CancelRestoreManually(Vector3Int corePos)
	{
		CoreRestoreSession session = GetActiveRestoreAt(corePos);
		if (session == null) return false;

		session.CancelManually(ResourceManager.Instance?.LifeEnergyAccount, out bool returnSalvage);
		_activeRestores.Remove(corePos);

		if (returnSalvage)
			Salvage.Spawn(corePos);

		if (TryGetCoreAt(corePos, out _, out InteractableObject core))
		{
			GameObject visual = Session.GetObjectVisual(core.Position);
			visual?.GetComponent<ObjectProgressBarVisual>()?.SetProgress(0f, false);
			ApplyTint(core);
		}
		NoticeCenter.Instance?.PushMomentary("코어 수복을 취소하고 지불한 생명 에너지를 환급했습니다.", NoticeCenter.WarningColor);
		return true;
	}

	public void ApplyDamageToRestoringCore(Vector3Int corePos, float damage)
	{
		CoreRestoreSession session = GetActiveRestoreAt(corePos);
		if (session == null || damage <= 0f) return;

		session.ApplyDamageDuringRestore(damage, ResourceManager.Instance?.LifeEnergyAccount);
		if (session.IsDestroyed)
		{
			// 미완성 수복 파괴 시 지불액과 투입 잔해 모두 손실되며 새 잔해는 생성하지 않는다(19-표1, 24-표1).
			_activeRestores.Remove(corePos);
			if (TryGetCoreAt(corePos, out Room room, out InteractableObject core))
			{
				GameObject visual = Session.GetObjectVisual(core.Position);
				visual?.GetComponent<ObjectProgressBarVisual>()?.SetProgress(0f, false);
				ApplyTint(core);
				LogHelper.Warning(LogHelper.GAME, $"[코어] F{room.Floor} 수복 중 코어 오브젝트 파괴 — 지불액 및 투입 잔해 소멸(고정 위치에서 재수복 가능)");
				NoticeCenter.Instance?.PushMomentary($"{room.Floor}층 수복 중 코어가 파괴되어 수복 작업이 소멸했습니다.", NoticeCenter.WarningColor);
			}
		}
	}

	// 현장 작업자 진행 틱(BuildingManager.UpdateProcess에서 매 프레임 호출)
	public void TickActiveRestores(float deltaSeconds, IReadOnlyList<Unit> allUnits)
	{
		if (_activeRestores.Count == 0 || Session == null) return;

		var keys = new List<Vector3Int>(_activeRestores.Keys);
		var workerSpeeds = new List<float>(4);

		for (int i = 0; i < keys.Count; i++)
		{
			Vector3Int corePos = keys[i];
			if (!_activeRestores.TryGetValue(corePos, out CoreRestoreSession session) || session.IsEnded)
			{
				_activeRestores.Remove(corePos);
				continue;
			}

			if (!TryGetCoreAt(corePos, out Room room, out InteractableObject core))
			{
				_activeRestores.Remove(corePos);
				continue;
			}

			workerSpeeds.Clear();
			Vector2Int coreOrigin = new Vector2Int(corePos.x, corePos.y);
			if (allUnits != null)
			{
				for (int uIdx = 0; uIdx < allUnits.Count; uIdx++)
				{
					Unit u = allUnits[uIdx];
					if (u == null || u.hp <= 0f || !u.IsPlayerMonsterFaction || u.currentFloor != corePos.z) continue;
					if (u.assignedCoreRestorePos != corePos) continue;
					if (u.StatusEffects.State.stunDuration > 0f || u.IsInEvolutionPresentation) continue;
					if (WorkSpeedMath.IsOrthogonallyAdjacent(coreOrigin, Vector2Int.one, u.position, u.FootprintSize))
						workerSpeeds.Add(Mathf.Max(0.1f, u.workerSpeedMultiplier));
				}
			}

			bool completed = session.Tick(
				deltaSeconds,
				workerSpeeds,
				_roomClear.IsCleared(room),
				ResourceManager.Instance?.LifeEnergyAccount);

			GameObject visual = Session.GetObjectVisual(core.Position);
			visual?.GetComponent<ObjectProgressBarVisual>()?.SetProgress((float)session.Progress01, true);

			if (completed)
			{
				_activeRestores.Remove(corePos);
				CompleteCoreRestoration(room, core);
			}
		}
	}

	// debug 즉시 수복(free: true) 및 기존 단일 호출 호환
	public CoreRestoreBlock TryRestore(Vector3Int corePos, bool free = false)
	{
		if (!TryGetCoreAt(corePos, out Room room, out InteractableObject core)) return CoreRestoreBlock.NotDestroyed;
		if (free)
		{
			if (!core.CoreDestroyed) return CoreRestoreBlock.NotDestroyed;
			if (_activeRestores.TryGetValue(corePos, out var active) && !active.IsEnded)
			{
				active.CancelManually(ResourceManager.Instance?.LifeEnergyAccount, out bool returnSalvage);
				if (returnSalvage) Salvage.Spawn(corePos);
				_activeRestores.Remove(corePos);
			}
			CompleteCoreRestoration(room, core);
			return CoreRestoreBlock.None;
		}

		CoreRestoreBlock block = CheckRestore(room, core, useSalvage: true);
		if (block != CoreRestoreBlock.None) return block;
		return TryBeginRestoreOrder(corePos, useSalvageIfAvailable: true, out _)
			? CoreRestoreBlock.None
			: CoreRestoreBlock.NotEnoughResources;
	}

	private void CompleteCoreRestoration(Room room, InteractableObject core)
	{
		core.CoreDestroyed = false;
		core.CoreHp = core.CoreMaxHp;
		core.TimeSinceLastDamaged = float.MaxValue;

		GameObject visual = Session.GetObjectVisual(core.Position);
		visual?.GetComponent<ObjectProgressBarVisual>()?.SetProgress(0f, false);
		ApplyTint(core);

		// 수복에 참여했던 작업자 배정 해제
		List<Unit> restoreWorkers = null;
		if (Session.units != null)
		{
			foreach (var u in Session.units)
			{
				if (u != null && u.assignedCoreRestorePos == core.Position)
				{
					u.assignedCoreRestorePos = null;
					(restoreWorkers ??= new List<Unit>()).Add(u);
				}
			}
		}

		// 코어 수복도 단일 작업이다 — 끝난 작업자는 최신 저장 방어 위치로 복귀를 시도한다(배치모드 설계명세서 1-6 표2).
		// 위에서 코어가 이미 가동 상태로 돌아왔으므로 코어 권한 검사를 통과한다. 이 수복만으로 취소됐던 이동이 되살아나지는 않는다.
		if (restoreWorkers != null)
			foreach (var w in restoreWorkers) DefensePlacementSystem.Instance?.OnWorkTaskEnded(w);

		LogHelper.Log(LogHelper.GAME, $"[코어] F{room.Floor} {room.RoomName} 코어 수복 완료 — 정지됐던 작업 재개(활성화 이력 {(core.CoreEverActivated ? "유지" : "없음")})");
		NoticeCenter.Instance?.PushMomentary($"{room.Floor}층 코어가 수복되었습니다. 정지된 작업이 재개됩니다.", NoticeCenter.QuestCompleteColor);
		NotifyHumansCoreAttackable(core);
	}

	public void DebugDestroy(Vector3Int corePos)
	{
		if (TryGetCoreAt(corePos, out Room room, out InteractableObject core)) OnCoreDestroyed(room, core, null);
	}

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

	private void PlayActivationVfx(Room room, InteractableObject core)
	{
		GameObject visual = Session.GetObjectVisual(core.Position);
		Vector3 floorOffset = Session.unitGenerate != null ? Session.unitGenerate.GetFloorOffset(room.Floor) : Vector3.zero;
		Vector3 center = visual != null ? visual.transform.position : new Vector3(core.Position.x + 0.5f, core.Position.y + 0.5f, 0f) + floorOffset;
		RectInt bounds = room.Bounds;
		var roomRect = new Rect(bounds.xMin + floorOffset.x, bounds.yMin + floorOffset.y, bounds.width, bounds.height);

		var sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
		CoreActivationVfx.Play(Session.GetFloorCategoryGroup(room.Floor, "Objects"), center, roomRect, ActivatedTint,
			sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 5) + 1);
	}

	private void ApplyTint(InteractableObject core)
	{
		var sr = Session.GetObjectVisual(core.Position)?.GetComponent<SpriteRenderer>();
		if (sr == null) return;
		if (GetActiveRestoreAt(core.Position) != null)
		{
			sr.color = RestoringTint;
			return;
		}
		sr.color = CoreMath.StatusOf(core.CoreEverActivated, core.CoreDestroyed) switch
		{
			CoreStatus.FullyActivated => ActivatedTint,
			CoreStatus.Destroyed => DestroyedTint,
			_ => IncompleteTint,
		};
	}
}
