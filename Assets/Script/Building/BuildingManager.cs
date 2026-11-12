using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Haare.Util.Logger;
using UnityEngine;
using VContainer;

public class BuildingData
{
	public int FacilityId;
	public FacilityKind Kind = FacilityKind.UnitProduction;
	public FacilityOriginKind OriginKind = FacilityOriginKind.PlayerBuilt;
	public string DisplayName = "시설";

	public Vector3Int Position;
	public Vector2Int Footprint = Vector2Int.one;
	public Vector2Int Origin2D => new Vector2Int(Position.x, Position.y);
	public bool ContainsTile(Vector3Int p) => p.z == Position.z && p.x >= Position.x && p.x < Position.x + Footprint.x && p.y >= Position.y && p.y < Position.y + Footprint.y;
	public GameObject VisualObject;
	public Sprite SpriteAsset;

	public float CurrentHp = 300f;
	public float MaxHp = 300f;
	public int UpgradeTier;
	public bool IsFormallyInstalled = true;

	// 시설 방향(시계 방향 90도 단위 0~3)과 이동 목적지에서 적용할 방향(건설 명세 2-6).
	public int Rotation;
	public int PendingRotation;
	// 재설치 단계 전 최대 HP(완성/건설 단계 척도). 재설치 몸체가 서 있는 동안 MaxHp는 재설치 단계 최대 HP다(건설 명세 2-7).
	public float ScaleMaxHp;
	public bool ReinstallBodyPlaced;
	// 미완성 건설을 옮기는 중인가 - 원래 건설 작업량·HP 비율·지불은 유지하고 재설치가 끝나면 건설을 잇는다(건설 명세 2-7).
	public bool MoveOfUnfinishedConstruction;

	public bool IsResourceBuilding => Kind == FacilityKind.LifeEnergy;
	public bool IsDummy => Kind == FacilityKind.Dummy;

	// 현장 작업(한 번에 한 종류만 허용 — 16항)
	public FacilityFieldTaskKind ActiveFieldTask = FacilityFieldTaskKind.None;
	public ConstructionOrUpgradeSession ActiveConstructionOrUpgrade;
	public RepairSession ActiveRepair;
	public RelocationSession ActiveRelocation;
	public double DemolishCompletedSeconds;
	public double DemolishRequiredSeconds = 8.0;

	public Dictionary<ResourceType, int> PaidBuildCosts = new Dictionary<ResourceType, int>();
	public Dictionary<ResourceType, int> PaidUpgradeCosts = new Dictionary<ResourceType, int>();

	// 시설별 엔진 상태
	public LifeEnergyCycleTracker LifeEnergyCycle = new LifeEnergyCycleTracker(10.0, 50.0);
	public List<ProductionRule> AvailableRules;
	public FacilityProductionQueue ProductionQueue = new FacilityProductionQueue();
	public CorpseRefineryState Refinery = new CorpseRefineryState(5);
	public FacilityResearchQueue ResearchQueue;

	// 이번 틱에 실제 현장 작업(수리·강화·철거·분해)이 진행 중인가 - 등록·접근·차액 승인 대기 중에는 false라 다른 조건을 충족하면 가동한다(건설 명세 2-3·2-5·2-6).
	public bool FieldWorkActive;
	// 현장 작업이 멈춘 사유(작업 현황 표시용) - 비어 있으면 진행 중이다(건설 명세 1-8).
	public string WaitReason = string.Empty;
	// 시설 안에서 일하는 내부 작업자(건설 명세 1-6) - 모습·격자·시야에서 빠져 있다.
	public readonly List<Unit> InsideWorkers = new List<Unit>();
	// 내부에서 죽어 아직 배출하지 못한 작업자 시체(건설 명세 2-8) - 부패가 계속되고 시설이 파괴되면 소멸하며, 분해·철거되면 원래 자리에 남는다.
	public readonly List<InteractableObject> PendingInsideCorpses = new List<InteractableObject>();
	// 마지막으로 시설이 피격된 게임 시간 - 내부 작업자의 '대응' 판정에 쓴다(건설 명세 1-6-표1).
	public double LastHitGameSeconds = double.NegativeInfinity;
	// 착공 부지의 아군에게 마지막으로 비켜 달라고 요청한 게임 시간(건설 명세 2-1-표2).
	public double LastMakeWayAt = double.NegativeInfinity;

	// 손상 효율 e = 0.5 + 0.5 × 남은 HP 비율(건설 명세 2-4). 시설 종류별 적용 대상(속도·산출량·피해)은 호출부가 정한다.
	public double DamageEfficiency => ConstructionRules.DamageCoefficient(CurrentHp, MaxHp);

	// 정식 설치 + 생존 + 실제 현장 작업 중이 아님 + 담당 코어 정상이면 가동한다 - 손상만으로는 멈추지 않고 효율만 줄어든다(건설 명세 2-3·2-4).
	public bool IsOperational =>
		IsFormallyInstalled
		&& !IsDummy
		&& CurrentHp > 0f
		&& !FieldWorkActive
		&& BuildingManager.AreCoreFunctionsActive(Position.z);

	// 새 생산 예약·연구 선택은 담당 코어가 파괴돼도 할 수 있다 - 전액을 선점하고 실제 가동은 수복 뒤 재검사한다(건설 명세 1-7-표1). 실제 현장 작업 중에는 막는다.
	public bool CanTakeReservations => IsFormallyInstalled && !IsDummy && CurrentHp > 0f && !FieldWorkActive;

	// 시설 가동이 내부 작업자(생명 에너지·연구)를 받는 상태인가 - 현장 작업(건설·수리·강화·철거·이동)이 없을 때만이다.
	public bool IsInternalWorkMode => ActiveFieldTask == FacilityFieldTaskKind.None && InternalWorkRules.IsInternalWorkKind(Kind);

	// 이 유닛이 이 시설의 현재 역할(내부 작업/현장 작업)로 배정돼 있는가.
	public bool IsWorkerInCurrentRole(Unit u) => u.assignedWorkBuilding == this && u.assignedWorkIsInternal == IsInternalWorkMode;
}

public enum BuildBlock
{
	None,
	CoreDestroyed,
	RoomNotSecured,
	Terrain,
	SoulUnlockRequired,
	Occupied,
	FrontZone,
}

public partial class BuildingManager : NativeRoutine
{
	public static BuildingManager Instance { get; private set; }

	public const float DefaultCycleIntervalSeconds = 10f;
	public const double DefaultCycleBasePayout = 50.0;
	public const float DefaultFacilityMaxHp = 300f;
	public const double DefaultBuildWorkSeconds = 8.0;
	public const double DefaultUpgradeWorkSeconds = 12.0;
	public const double DefaultFullRepairSeconds = 10.0;
	public const double DefaultDismantleSeconds = 5.0; // 보정 없는 1명 기준 임시 5초(건설 명세 2-6)
	public const double DefaultReinstallSeconds = 5.0; // 보정 없는 1명 기준 임시 5초(건설 명세 2-6)
	public const double DefaultDemolishRecoveryRate = 0.5;

	public static readonly Vector2Int UnitBuildingFootprint = new Vector2Int(3, 3);
	public static readonly Vector2Int ResourceBuildingFootprint = new Vector2Int(2, 2);
	public static readonly Vector2Int RefineryBuildingFootprint = new Vector2Int(2, 2);
	public static readonly Vector2Int ResearchBuildingFootprint = new Vector2Int(2, 2);
	private static readonly Vector2Int DummyBuildingFootprint = Vector2Int.one;

	private readonly Dictionary<Vector3Int, BuildingData> buildingGrid = new Dictionary<Vector3Int, BuildingData>();
	private readonly List<BuildingData> _allBuildings = new List<BuildingData>();
	private int _nextFacilityId = 1;

	private MapManager mapManager;
	private ResourceManager resourceManager;
	private CreateMap createMap;
	private UnitGenerate unitGenerate;
	private Sprite _cachedUnitBuildingSprite;
	private Sprite _cachedResourceBuildingSprite;

	[Inject]
	public void Construct(MapManager mapManager, ResourceManager resourceManager, CreateMap createMap, UnitGenerate unitGenerate)
	{
		this.mapManager = mapManager;
		this.resourceManager = resourceManager;
		this.createMap = createMap;
		this.unitGenerate = unitGenerate;
		Instance = this;
	}

	public override async UniTask Initialize(CancellationToken cts)
	{
		await base.Initialize(cts);
		Instance = this;
		_cachedUnitBuildingSprite = await GameAssets.LoadSpriteAsync(AssetKeys.UnitBuildingSprite);
		_cachedResourceBuildingSprite = await GameAssets.LoadSpriteAsync(AssetKeys.ResourceBuildingSprite);
		LogHelper.Log(LogHelper.GAME, "BuildingManager Initialized (v0.2 자원경제·생산 파이프라인)");
	}

	public override async UniTask Finalize()
	{
		buildingGrid.Clear();
		_allBuildings.Clear();
		if (Instance == this) Instance = null;
		LogHelper.Log(LogHelper.GAME, "BuildingManager Disposed");
		await base.Finalize();
	}

	public IReadOnlyList<BuildingData> AllBuildings => _allBuildings;

	public static Vector2Int GetFootprintForKind(FacilityKind kind) => kind switch
	{
		FacilityKind.UnitProduction => UnitBuildingFootprint,
		FacilityKind.LifeEnergy => ResourceBuildingFootprint,
		FacilityKind.CorpseRefinery => RefineryBuildingFootprint,
		FacilityKind.Research => ResearchBuildingFootprint,
		_ => DummyBuildingFootprint,
	};

	public static string GetDisplayNameForKind(FacilityKind kind) => kind switch
	{
		FacilityKind.LifeEnergy => "생명 에너지 시설",
		FacilityKind.UnitProduction => "유닛 생산 시설",
		FacilityKind.CorpseRefinery => "시체 보관·정제 통합시설",
		FacilityKind.Research => "연구 시설",
		_ => "더미 건물",
	};

	public static int GetBuildLifeEnergyCostForKind(FacilityKind kind) => kind switch
	{
		FacilityKind.LifeEnergy => ResourceManager.LifeEnergyBuildingCost,
		FacilityKind.UnitProduction => ResourceManager.UnitBuildingLifeEnergyCost,
		FacilityKind.CorpseRefinery => ResourceManager.CorpseRefineryBuildingCost,
		FacilityKind.Research => ResourceManager.ResearchBuildingLifeEnergyCost,
		_ => 0,
	};

	private static IEnumerable<Vector3Int> FootprintTiles(Vector3Int origin, Vector2Int footprint)
	{
		for (int dx = 0; dx < footprint.x; dx++)
			for (int dy = 0; dy < footprint.y; dy++)
				yield return new Vector3Int(origin.x + dx, origin.y + dy, origin.z);
	}

	public bool CanInstallAt(Vector3Int pos, Vector2Int footprint, bool requireOwnership = true, FacilityKind kind = FacilityKind.Dummy, int rotation = 0, BuildingData ignore = null)
		=> CheckInstallAt(pos, footprint, requireOwnership, kind, ignore, rotation) == BuildBlock.None;

	// ignore: 검사에서 자기 자신의 점유는 무시할 시설(제자리 회전·겹치는 이동 - 분해가 먼저 끝나므로 허용).
	public BuildBlock CheckInstallAt(Vector3Int pos, Vector2Int footprint, bool requireOwnership = true, FacilityKind kind = FacilityKind.UnitProduction, BuildingData ignore = null, int rotation = 0)
	{
		if (requireOwnership && kind == FacilityKind.Research && resourceManager != null && !resourceManager.IsSoulShardSpendUnlocked)
			return BuildBlock.SoulUnlockRequired;

		foreach (Vector3Int tile in FootprintTiles(pos, footprint))
		{
			BuildBlock block = CheckInstallSingleTile(tile, requireOwnership, ignore);
			if (block != BuildBlock.None) return block;
		}
		// 생산시설은 몸체와 정면 출현 구역을 함께 확인한다(건설 명세 2-1·2-10).
		if (kind == FacilityKind.UnitProduction)
		{
			RectInt zone = ConstructionRules.FrontZoneRect(new Vector2Int(pos.x, pos.y), footprint, rotation, FrontZoneDepth);
			for (int zx = zone.xMin; zx < zone.xMax; zx++)
				for (int zy = zone.yMin; zy < zone.yMax; zy++)
					if (CheckInstallSingleTile(new Vector3Int(zx, zy, pos.z), requireOwnership, ignore) != BuildBlock.None) return BuildBlock.FrontZone;
		}
		return BuildBlock.None;
	}

	public static string DescribeBlock(BuildBlock block) => block switch
	{
		BuildBlock.CoreDestroyed => "코어가 파괴되어 건설할 수 없습니다. 먼저 코어를 수복하세요.",
		BuildBlock.RoomNotSecured => "확보하지 않은 방에서는 건설할 수 없습니다. 방의 적을 모두 처치해 확보하세요.",
		BuildBlock.Terrain => "장애물이 있거나 설치할 수 없는 지형입니다.",
		BuildBlock.SoulUnlockRequired => "최초 코어 완전 활성화 이후에 연구시설을 건설할 수 있습니다.",
		BuildBlock.FrontZone => "생산시설의 정면 출현 구역(2칸)에 벽·시설·시체가 있거나 다른 생산시설 구역과 겹쳐 지정할 수 없습니다.",
		BuildBlock.Occupied => "시체나 분해된 시설이 있어 지정할 수 없습니다. 먼저 치운 뒤 다시 지정하세요.",
		_ => string.Empty,
	};

	private BuildBlock CheckInstallSingleTile(Vector3Int pos, bool requireOwnership, BuildingData ignore)
	{
		if (pos.x < 0 || pos.y < 0 || pos.z < 0) return BuildBlock.Terrain;
		if (buildingGrid.TryGetValue(pos, out BuildingData occupant) && occupant != ignore) return BuildBlock.Terrain;
		if (IsInsideOtherFrontZone(pos, ignore)) return BuildBlock.FrontZone;
		if (GameSession.Instance != null && GameSession.Instance.objectGrid.TryGetValue(pos, out InteractableObject looseObj) && IsLooseCorpseOrPackage(looseObj)) return BuildBlock.Occupied;
		if (GameSession.Instance != null && GameSession.Instance.IsRepairableDoorTile(pos)) return BuildBlock.Terrain;
		if (GameSession.Instance != null && GameSession.Instance.IsFenceTile(pos)) return BuildBlock.Terrain;
		if (requireOwnership && createMap != null && !createMap.IsPositionPlayerOwned(pos.z, new Vector2Int(pos.x, pos.y))) return BuildBlock.RoomNotSecured;
		// 코어가 파괴돼도 확보한 방에서는 건설할 수 있다(건설 명세 1-7·2-1) — 생산·자원·연구·정제만 가동이 멈춘다.

		if (createMap != null && createMap.map.floors != null && pos.z < createMap.map.floors.Length)
		{
			Floor floor = createMap.map.floors[pos.z];
			if (floor.chunks != null)
			{
				int cs = floor.config.chunkSize;
				int cx = pos.x / cs, cy = pos.y / cs;
				int tx = pos.x % cs, ty = pos.y % cs;
				if (cx >= 0 && cx < floor.config.width && cy >= 0 && cy < floor.config.height)
				{
					var chunk = floor.chunks[cx, cy];
					if (chunk.chunk != null)
					{
						var tile = chunk.chunk[tx, ty];
						// 이동·회전 검사에서 자기 자신이 세운 구조물 표시(isStructureExist)는 무시한다 - 분해가 먼저 끝난 뒤 그 자리를 다시 쓰기 때문이다.
						bool ownTile = ignore != null && ignore.ContainsTile(pos);
						if (tile.name == "Wall" || (tile.isStructureExist && !ownTile)) return BuildBlock.Terrain;
					}
				}
			}
		}
		return BuildBlock.None;
	}

	public static bool AreCoreFunctionsActive(int floor)
	{
		CoreSystem core = GameSession.Instance != null ? GameSession.Instance.Core : null;
		return core == null || core.AreFunctionsActive(floor);
	}

	public BuildingData GetBuildingAt(Vector3Int pos)
	{
		buildingGrid.TryGetValue(pos, out BuildingData data);
		return data;
	}

	public int ResourceBuildingCount
	{
		get
		{
			int count = 0;
			for (int i = 0; i < _allBuildings.Count; i++)
				if (_allBuildings[i].IsFormallyInstalled && _allBuildings[i].IsResourceBuilding) count++;
			return count;
		}
	}

	public int BuildingCount => _allBuildings.Count;

	public float CurrentResourceProductionPerSecond
		=> ResourceBuildingCount * (float)(DefaultCycleBasePayout / DefaultCycleIntervalSeconds);

	// ── 초기 배치(완공 상태) 및 플레이어 건설 예약(현장 착공) ──

	public bool InstallResourceBuilding(Vector3Int pos, Sprite buildingSprite, bool prePlaced = false)
		=> InstallCompletedFacility(FacilityKind.LifeEnergy, pos, null, buildingSprite, prePlaced ? FacilityOriginKind.PrePlacedOrCapturedDungeon : FacilityOriginKind.PlayerBuilt);

	public bool InstallProductionBuilding(Vector3Int pos, List<ProductionRule> availableRules, Sprite buildingSprite, bool prePlaced = false)
		=> InstallCompletedFacility(FacilityKind.UnitProduction, pos, availableRules, buildingSprite, prePlaced ? FacilityOriginKind.PrePlacedOrCapturedDungeon : FacilityOriginKind.PlayerBuilt);

	public bool InstallCorpseRefineryBuilding(Vector3Int pos, Sprite buildingSprite, bool prePlaced = false)
		=> InstallCompletedFacility(FacilityKind.CorpseRefinery, pos, null, buildingSprite, prePlaced ? FacilityOriginKind.PrePlacedOrCapturedDungeon : FacilityOriginKind.PlayerBuilt);

	public bool InstallResearchBuilding(Vector3Int pos, Sprite buildingSprite, bool prePlaced = false)
		=> InstallCompletedFacility(FacilityKind.Research, pos, null, buildingSprite, prePlaced ? FacilityOriginKind.PrePlacedOrCapturedDungeon : FacilityOriginKind.PlayerBuilt);

	public bool InstallDummyBuilding(Vector3Int pos, Sprite buildingSprite, string displayName)
	{
		Vector2Int fp = DummyBuildingFootprint;
		if (!CanInstallAt(pos, fp, requireOwnership: false)) return false;

		GameObject visual = CreateBuildingVisual(pos, fp, buildingSprite, displayName, Color.white);
		var b = CreateBuildingInstance(FacilityKind.Dummy, pos, fp, null, buildingSprite, displayName, FacilityOriginKind.PlayerBuilt, visual);
		RegisterBuildingToGrid(b, updateObstacle: true);
		return true;
	}

	private bool InstallCompletedFacility(
		FacilityKind kind,
		Vector3Int pos,
		List<ProductionRule> availableRules,
		Sprite sprite,
		FacilityOriginKind originKind)
	{
		Vector2Int fp = GetFootprintForKind(kind);
		// 생산시설은 정면 출현 구역이 비는 방향을 고른다. 어느 방향도 안 되면 예전 시설 둘레 출현으로 폴백한다(시작방 배치가 막히지 않게).
		int rot = 0;
		bool zoneOk = false;
		if (kind == FacilityKind.UnitProduction)
		{
			for (int r = 0; r < 4 && !zoneOk; r++)
				if (CanInstallAt(pos, fp, requireOwnership: true, kind: kind, rotation: r)) { rot = r; zoneOk = true; }
		}
		if (!zoneOk && !CanInstallAt(pos, fp, requireOwnership: true))
		{
			LogHelper.Warning(LogHelper.GAME, $"시설 설치 불가: {pos} ({kind})");
			return false;
		}

		string label = GetDisplayNameForKind(kind);
		Color tint = GetTintForKind(kind, isBlueprint: false);
		GameObject visual = CreateBuildingVisual(pos, fp, sprite, label, tint);
		BuildingData b = CreateBuildingInstance(kind, pos, fp, availableRules, sprite, label, originKind, visual);
		b.Rotation = rot;
		b.PendingRotation = rot;
		RegisterBuildingToGrid(b, updateObstacle: true);
		LogHelper.Log(LogHelper.GAME, $"[시설 설치] '{label}' 배치 완료: {pos} ({fp})");
		return true;
	}

	// 명세서 v0.2 3·16항: 건설 명령 시 비용을 선배정으로 묶고 건설 예정지를 생성한다(작업자 도착+공간 확보 시 실제 차감·착공).
	public bool TryOrderConstruction(
		FacilityKind kind,
		Vector3Int pos,
		Sprite sprite,
		List<ProductionRule> availableRules,
		out BuildingData orderedBuilding,
		out string failureMessage,
		int rotation = 0)
	{
		orderedBuilding = null;
		failureMessage = string.Empty;

		rotation = ConstructionRules.NormalizeRotation(rotation);
		Vector2Int fp = ConstructionRules.RotatedFootprint(GetFootprintForKind(kind), rotation);
		BuildBlock block = CheckInstallAt(pos, fp, requireOwnership: true, kind: kind, rotation: rotation);
		if (block != BuildBlock.None)
		{
			failureMessage = DescribeBlock(block);
			return false;
		}

		if (resourceManager == null)
		{
			failureMessage = "자원 관리자가 초기화되지 않았습니다.";
			return false;
		}

		int cost = GetBuildLifeEnergyCostForKind(kind);
		var rawCosts = new List<(ResourceType, double)> { (ResourceType.LifeEnergy, cost) };

		if (!ConstructionOrUpgradeSession.TryCreateOrder(
			isUpgrade: false,
			rawCosts: rawCosts,
			requiredWorkSeconds: DefaultBuildWorkSeconds,
			targetCompletedMaxHp: DefaultFacilityMaxHp,
			accounts: resourceManager.Accounts,
			isSoulShardSpendUnlocked: resourceManager.IsSoulShardSpendUnlocked,
			isCoreActive: true, // 확보한 방의 건설은 코어 파괴 중에도 가능(건설 명세 1-7)
			out ConstructionOrUpgradeSession session,
			out failureMessage))
		{
			return false;
		}

		string label = GetDisplayNameForKind(kind);
		Color blueprintTint = GetTintForKind(kind, isBlueprint: true);
		GameObject visual = CreateBuildingVisual(pos, fp, sprite, label + "_건설예정", blueprintTint);

		BuildingData b = CreateBuildingInstance(kind, pos, fp, availableRules, sprite, label, FacilityOriginKind.PlayerBuilt, visual);
		b.IsFormallyInstalled = false;
		b.Rotation = rotation;
		b.PendingRotation = rotation;
		b.ActiveFieldTask = FacilityFieldTaskKind.Construction;
		b.ActiveConstructionOrUpgrade = session;

		// 착공 전까지 다른 건설 예약과는 겹치지 않으나 유닛 통과는 가능하다(16-표2)
		RegisterBuildingToGrid(b, updateObstacle: false);

		orderedBuilding = b;
		OnPlanOrdered(b);
		LogHelper.Log(LogHelper.GAME, $"[건설 예약] '{label}' 등록: {pos} (선점 생명 에너지 {cost}) — 아군 몬스터를 우클릭해 건설 진행");
		NoticeCenter.Instance?.PushMomentary($"{label} 건설이 등록되었습니다. 몬스터를 우클릭해 작업자를 배정하세요.", NoticeCenter.QuestCompleteColor);
		return true;
	}

	private BuildingData CreateBuildingInstance(
		FacilityKind kind,
		Vector3Int pos,
		Vector2Int fp,
		List<ProductionRule> availableRules,
		Sprite sprite,
		string displayName,
		FacilityOriginKind origin,
		GameObject visual)
	{
		int id = _nextFacilityId++;
		var b = new BuildingData
		{
			FacilityId = id,
			Kind = kind,
			OriginKind = origin,
			DisplayName = displayName,
			Position = pos,
			Footprint = fp,
			SpriteAsset = sprite,
			VisualObject = visual,
			CurrentHp = DefaultFacilityMaxHp,
			MaxHp = DefaultFacilityMaxHp,
			IsFormallyInstalled = true,
			AvailableRules = kind == FacilityKind.UnitProduction
				? (availableRules ?? ProductionRule.CreateDefaultPlayerUnitRules())
				: null,
			LifeEnergyCycle = new LifeEnergyCycleTracker(DefaultCycleIntervalSeconds, DefaultCycleBasePayout),
			ResearchQueue = resourceManager != null ? new FacilityResearchQueue(id, resourceManager.ResearchLedger) : null,
		};
		return b;
	}

	private static Color GetTintForKind(FacilityKind kind, bool isBlueprint)
	{
		Color baseColor = kind switch
		{
			FacilityKind.LifeEnergy => new Color(0.75f, 1.0f, 0.75f, 1f),
			FacilityKind.CorpseRefinery => new Color(0.85f, 0.72f, 1.0f, 1f),
			FacilityKind.Research => new Color(0.68f, 0.9f, 1.0f, 1f),
			_ => Color.white,
		};
		if (isBlueprint)
			baseColor.a = 0.52f;
		return baseColor;
	}

	private void RegisterBuildingToGrid(BuildingData b, bool updateObstacle)
	{
		if (!_allBuildings.Contains(b))
			_allBuildings.Add(b);
		foreach (Vector3Int tile in FootprintTiles(b.Position, b.Footprint))
			buildingGrid[tile] = b;
		if (updateObstacle)
			UpdateMapDataObstacle(b.Position, b.Footprint, true);
	}

	private void UnregisterBuildingFromGrid(BuildingData b, bool clearObstacle)
	{
		foreach (Vector3Int tile in FootprintTiles(b.Position, b.Footprint))
		{
			if (buildingGrid.TryGetValue(tile, out var cur) && cur == b)
				buildingGrid.Remove(tile);
		}
		if (clearObstacle)
			UpdateMapDataObstacle(b.Position, b.Footprint, false);
	}

	// ── 시설 현장 작업 명령 (수리 · 업그레이드 · 철거 · 이동용 분해) ──

	public bool TryStartFacilityRepair(BuildingData b)
	{
		if (b == null || !b.IsFormallyInstalled || b.ActiveFieldTask != FacilityFieldTaskKind.None || resourceManager == null)
			return false;

		if (!RepairSession.TryCreateOrder(
			b.CurrentHp,
			b.MaxHp,
			ResourceManager.FacilityFullRepairLifeEnergyCost,
			DefaultFullRepairSeconds,
			resourceManager.LifeEnergyAccount,
			true, // 수리는 코어 파괴 중에도 가능(건설 명세 1-7)
			out RepairSession repair,
			out string failMsg))
		{
			if (!string.IsNullOrEmpty(failMsg))
				NoticeCenter.Instance?.PushMomentary(failMsg, NoticeCenter.WarningColor);
			return false;
		}

		b.ActiveFieldTask = FacilityFieldTaskKind.Repair;
		b.ActiveRepair = repair;
		NoticeCenter.Instance?.PushMomentary($"{b.DisplayName} 수리가 등록되었습니다. 작업자를 우클릭 배정하세요.", NoticeCenter.QuestCompleteColor);
		return true;
	}

	public bool TryApproveFacilityRepairDiff(BuildingData b)
	{
		if (b?.ActiveRepair == null || resourceManager == null) return false;
		if (!b.ActiveRepair.TryApproveExtraDiffManually(resourceManager.LifeEnergyAccount, out string failMsg))
		{
			if (!string.IsNullOrEmpty(failMsg))
				NoticeCenter.Instance?.PushMomentary(failMsg, NoticeCenter.WarningColor);
			return false;
		}
		NoticeCenter.Instance?.PushMomentary("추가 수리비를 지불했습니다.", NoticeCenter.QuestCompleteColor);
		return true;
	}

	public void CancelFacilityRepair(BuildingData b)
	{
		if (b?.ActiveRepair == null) return;
		b.ActiveRepair.CancelManually(resourceManager?.LifeEnergyAccount);
		b.ActiveRepair = null;
		b.ActiveFieldTask = FacilityFieldTaskKind.None;
		NoticeCenter.Instance?.PushMomentary("수리를 취소하고 지불한 수리비를 환급했습니다.", NoticeCenter.WarningColor);
	}

	public bool TryStartFacilityUpgrade(BuildingData b)
	{
		if (b == null || !b.IsFormallyInstalled || resourceManager == null)
			return false;
		if (b.ActiveFieldTask == FacilityFieldTaskKind.Repair && b.ActiveRepair != null)
		{
			// 수리에서 강화로 바꿀 때는 수리 환급분을 포함해 새 비용을 검사한다. 성공하면 수리를 취소 정산하고 교체, 부족하면 기존 수리를 유지한다(건설 명세 1-2-표2).
			double refund = b.ActiveRepair.PaidTotalSoFar + b.ActiveRepair.ReservedBeforeStart;
			bool lifeOk = resourceManager.LifeEnergyAccount.AvailableBalance + refund >= ResourceManager.FacilityUpgradeLifeEnergyCost;
			bool soulOk = !resourceManager.Accounts.TryGetValue(ResourceType.SoulShard, out ResourceAccount soulAcc) || soulAcc.AvailableBalance >= ResourceManager.FacilityUpgradeSoulShardCost;
			if (!resourceManager.IsSoulShardSpendUnlocked || !lifeOk || !soulOk)
			{
				NoticeCenter.Instance?.PushMomentary("강화 비용이 부족해 기존 수리를 유지합니다.", NoticeCenter.WarningColor);
				return false;
			}
			CancelFacilityRepair(b);
		}
		if (b.ActiveFieldTask != FacilityFieldTaskKind.None)
			return false;

		if (!resourceManager.IsSoulShardSpendUnlocked)
		{
			NoticeCenter.Instance?.PushMomentary("최초 코어 완전 활성화 이후에 시설을 업그레이드할 수 있습니다.", NoticeCenter.WarningColor);
			return false;
		}

		var rawCosts = new List<(ResourceType, double)>
		{
			(ResourceType.LifeEnergy, ResourceManager.FacilityUpgradeLifeEnergyCost),
			(ResourceType.SoulShard, ResourceManager.FacilityUpgradeSoulShardCost),
		};

		if (!ConstructionOrUpgradeSession.TryCreateOrder(
			isUpgrade: true,
			rawCosts: rawCosts,
			requiredWorkSeconds: DefaultUpgradeWorkSeconds,
			targetCompletedMaxHp: b.MaxHp + 150f,
			accounts: resourceManager.Accounts,
			isSoulShardSpendUnlocked: resourceManager.IsSoulShardSpendUnlocked,
			isCoreActive: true, // 강화도 코어 파괴 중 가능(건설 명세 1-7)
			out ConstructionOrUpgradeSession upg,
			out string failMsg))
		{
			if (!string.IsNullOrEmpty(failMsg))
				NoticeCenter.Instance?.PushMomentary(failMsg, NoticeCenter.WarningColor);
			return false;
		}

		b.ActiveFieldTask = FacilityFieldTaskKind.Upgrade;
		b.ActiveConstructionOrUpgrade = upg;
		NoticeCenter.Instance?.PushMomentary($"{b.DisplayName} 업그레이드가 등록되었습니다. 작업자를 배정하세요.", NoticeCenter.QuestCompleteColor);
		return true;
	}

	public void CancelFacilityConstructionOrUpgrade(BuildingData b)
	{
		if (b?.ActiveConstructionOrUpgrade == null) return;
		if (b.ActiveRelocation != null)
		{
			// 이동 중인 미완성 건설: 분해물이 바닥에 있으면 재설치가 끝난 뒤 취소한다. 그 밖에는 연결된 이동을 끝내고 취소한다(건설 명세 2-7).
			if (b.ActiveRelocation.IsDismantleFinished && !b.ReinstallBodyPlaced)
			{
				NoticeCenter.Instance?.PushMomentary("분해된 미완성 건설은 재설치가 끝난 뒤에 취소할 수 있습니다.", NoticeCenter.WarningColor);
				return;
			}
			b.ActiveRelocation.CancelOrInterrupt(resourceManager?.LifeEnergyAccount);
			b.ActiveRelocation = null;
			b.ReinstallBodyPlaced = false;
			b.MoveOfUnfinishedConstruction = false;
		}
		bool wasConstruction = !b.ActiveConstructionOrUpgrade.IsUpgrade;
		b.ActiveConstructionOrUpgrade.CancelManually(resourceManager?.Accounts);
		b.ActiveConstructionOrUpgrade = null;
		b.ActiveFieldTask = FacilityFieldTaskKind.None;

		if (wasConstruction)
		{
			RemoveBuildingCompletely(b);
			NoticeCenter.Instance?.PushMomentary("건설을 취소하고 비용을 환급했습니다.", NoticeCenter.WarningColor);
		}
		else
		{
			NoticeCenter.Instance?.PushMomentary("업그레이드를 취소하고 비용을 환급했습니다.", NoticeCenter.WarningColor);
		}
	}

	public bool TryStartFacilityDemolish(BuildingData b)
	{
		if (b == null || !b.IsFormallyInstalled || b.ActiveFieldTask != FacilityFieldTaskKind.None)
			return false;
		if (b.Kind == FacilityKind.CorpseRefinery && !b.Refinery.IsCompletelyEmpty)
		{
			NoticeCenter.Instance?.PushMomentary("시체 보관·정제 시설은 내부 시체를 모두 비운 뒤 철거할 수 있습니다.", NoticeCenter.WarningColor);
			return false;
		}

		b.ActiveFieldTask = FacilityFieldTaskKind.Demolish;
		b.DemolishCompletedSeconds = 0.0;
		NoticeCenter.Instance?.PushMomentary($"{b.DisplayName} 철거가 등록되었습니다. 작업자를 우클릭 배정하세요.", NoticeCenter.QuestCompleteColor);
		return true;
	}

	public void CancelFacilityDemolish(BuildingData b)
	{
		if (b == null || b.ActiveFieldTask != FacilityFieldTaskKind.Demolish) return;
		b.DemolishCompletedSeconds = 0.0;
		b.ActiveFieldTask = FacilityFieldTaskKind.None;
		NoticeCenter.Instance?.PushMomentary("철거 작업을 취소했습니다.", NoticeCenter.WarningColor);
	}

	// 이동(위치 변경·제자리 회전 포함)은 분해·운반·재설치를 한 번에 지시하며 모두 무상이다(건설 명세 2-6). 미완성 건설도 옮길 수 있다(건설 진행·HP 비율·지불 유지).
	public bool TryStartFacilityRelocation(BuildingData b, Vector3Int destination, int? newRotation = null)
	{
		if (b == null || resourceManager == null || b.ActiveRelocation != null)
			return false;
		bool unfinished = !b.IsFormallyInstalled
			&& b.ActiveFieldTask == FacilityFieldTaskKind.Construction
			&& b.ActiveConstructionOrUpgrade != null
			&& b.ActiveConstructionOrUpgrade.HasStartedWork;
		if (!unfinished && (!b.IsFormallyInstalled || b.ActiveFieldTask != FacilityFieldTaskKind.None))
			return false;

		int rot = ConstructionRules.NormalizeRotation(newRotation ?? b.Rotation);
		if (destination == b.Position && rot == b.Rotation)
		{
			NoticeCenter.Instance?.PushMomentary("같은 위치·같은 방향으로는 이동할 수 없습니다.", NoticeCenter.WarningColor);
			return false;
		}

		Vector2Int destFootprint = ConstructionRules.RotatedFootprint(b.Footprint, rot - b.Rotation);
		BuildBlock block = CheckInstallAt(destination, destFootprint, requireOwnership: true, kind: b.Kind, ignore: b, rotation: rot);
		bool destValid = block == BuildBlock.None;

		if (!RelocationSession.TryCreateOrder(
			ResourceManager.FacilityRelocateDismantleLifeEnergyCost,
			DefaultDismantleSeconds,
			DefaultReinstallSeconds,
			destFootprint,
			destination,
			true, // 이동용 분해·운반·재설치는 코어 파괴 중에도 가능(건설 명세 1-7)
			true, // 저장/정제 시설은 비우지 않고 옮긴다 - 안의 시체는 부패 정지 상태로 함께 이동한다(건설 명세 2-8)
			destValid,
			resourceManager.LifeEnergyAccount,
			out RelocationSession reloc,
			out string failMsg))
		{
			string msg = !destValid && block != BuildBlock.None ? DescribeBlock(block) : failMsg;
			if (!string.IsNullOrEmpty(msg))
				NoticeCenter.Instance?.PushMomentary(msg, NoticeCenter.WarningColor);
			return false;
		}

		b.PendingRotation = rot;
		b.MoveOfUnfinishedConstruction = unfinished;
		b.ActiveFieldTask = FacilityFieldTaskKind.Relocation;
		b.ActiveRelocation = reloc;
		NoticeCenter.Instance?.PushMomentary($"{b.DisplayName} 이동이 등록되었습니다. 작업자를 배정하세요.", NoticeCenter.QuestCompleteColor);
		return true;
	}

	// 설치 목적지(와 방향) 변경(건설 명세 2-7-표2) - 새 위치 검사에 성공해야 바꾸고 실패하면 기존을 유지한다.
	// 분해 중 변경은 작업량·지불 보존, 운반 중 변경은 같은 유닛이 새 목적지로, 재설치 중 변경은 설치 작업량만 초기화하고 분해 상태로 운반한다.
	public bool TryChangeRelocationDestination(BuildingData b, Vector3Int newDestination, int? newRotation = null)
	{
		RelocationSession reloc = b?.ActiveRelocation;
		if (reloc == null) return false;

		int rot = ConstructionRules.NormalizeRotation(newRotation ?? b.PendingRotation);
		Vector2Int fp = ConstructionRules.RotatedFootprint(b.Footprint, rot - b.Rotation);
		BuildBlock block = CheckInstallAt(newDestination, fp, requireOwnership: true, kind: b.Kind, ignore: b, rotation: rot);
		if (block != BuildBlock.None)
		{
			NoticeCenter.Instance?.PushMomentary(DescribeBlock(block), NoticeCenter.WarningColor);
			return false;
		}

		bool wasInstalling = b.ReinstallBodyPlaced;
		if (!reloc.TryChangeDestination(newDestination, true)) return false;
		b.PendingRotation = rot;

		if (wasInstalling)
		{
			LeaveReinstallBody(b);
			List<Unit> helpers = CollectAssignedWorkers(b);
			Unit carrier = PickNearestUnit(helpers, b.Origin2D);
			if (carrier != null)
			{
				carrier.carriedDismantledBuilding = b;
				if (TryPickNearestAdjacentWorkTile(new Vector2Int(newDestination.x, newDestination.y), fp, carrier, out Vector2Int slot))
					IssueInternalEconomyMove(carrier, slot);
			}
		}
		else
		{
			for (int i = 0; i < GameSession.Instance.units.Count; i++)
			{
				Unit u = GameSession.Instance.units[i];
				if (u != null && u.hp > 0f && u.carriedDismantledBuilding == b
					&& TryPickNearestAdjacentWorkTile(new Vector2Int(newDestination.x, newDestination.y), fp, u, out Vector2Int slot2))
					IssueInternalEconomyMove(u, slot2);
			}
		}
		return true;
	}

	private static Unit PickNearestUnit(List<Unit> units, Vector2Int from)
	{
		Unit best = null;
		float bestScore = float.MaxValue;
		for (int i = 0; i < units.Count; i++)
		{
			Unit u = units[i];
			if (u == null || u.hp <= 0f) continue;
			float eta = Vector2Int.Distance(u.position, from) / Mathf.Max(0.1f, u.workerSpeedMultiplier); // 도착 예상 시간의 근사
			if (eta < bestScore || (Mathf.Approximately(eta, bestScore) && best != null && u.GetInstanceID() < best.GetInstanceID()))
			{
				bestScore = eta;
				best = u;
			}
		}
		return best;
	}

	// 재설치 몸체를 목적지에 세운다 - 시설 전체 점유를 가진 건설 중 오브젝트가 되고, 이동 전 HP 비율 × 재설치 단계 최대 HP로 시작한다(건설 명세 2-7-표1).
	private void PlaceReinstallBody(BuildingData b, Vector3Int dest)
	{
		RelocationSession reloc = b.ActiveRelocation;
		if (b.PendingRotation != b.Rotation)
		{
			b.Footprint = ConstructionRules.RotatedFootprint(b.Footprint, b.PendingRotation - b.Rotation);
			b.Rotation = b.PendingRotation;
		}
		b.Position = dest;
		b.ScaleMaxHp = b.MaxHp;
		float stageMax = ConstructionRules.ConstructionStageMaxHp(b.ScaleMaxHp);
		b.CurrentHp = ConstructionRules.ReinstallStartHp(b.CurrentHp, b.MaxHp, stageMax);
		b.MaxHp = stageMax;
		b.ReinstallBodyPlaced = true;
		b.IsFormallyInstalled = false;
		if (b.VisualObject != null)
		{
			Vector3 floorOffset = unitGenerate != null ? unitGenerate.GetFloorOffset(dest.z) : Vector3.zero;
			b.VisualObject.transform.position = new Vector3(dest.x + b.Footprint.x / 2f, dest.y + b.Footprint.y / 2f, 0f) + floorOffset;
			b.VisualObject.SetActive(true);
			var sr = b.VisualObject.GetComponent<SpriteRenderer>();
			if (sr != null) sr.color = GetTintForKind(b.Kind, isBlueprint: true);
		}
		RegisterBuildingToGrid(b, updateObstacle: true);
		reloc?.BeginInstall();
	}

	// 재설치 몸체를 거두고 분해 상태(HP는 완성 척도 비율로 환원)로 돌린다 - 같은 피해는 한 번만 반영된다(건설 명세 2-7-표1).
	private void LeaveReinstallBody(BuildingData b)
	{
		if (!b.ReinstallBodyPlaced) return;
		float ratio = b.MaxHp > 0f ? Mathf.Clamp01(b.CurrentHp / b.MaxHp) : 1f;
		b.MaxHp = b.ScaleMaxHp;
		b.CurrentHp = ratio * b.ScaleMaxHp;
		b.ReinstallBodyPlaced = false;
		UnregisterBuildingFromGrid(b, clearObstacle: true);
		_allBuildings.Remove(b);
		if (b.VisualObject != null) b.VisualObject.SetActive(false);
	}

	// 재설치 완료: 재설치 남은 HP 비율 × 완성 시설 최대 HP. 미완성 건설이었다면 건설을 잇는다(건설 명세 2-7).
	private void CompleteReinstall(BuildingData b)
	{
		float stageMax = b.MaxHp;
		b.MaxHp = b.ScaleMaxHp;
		b.CurrentHp = ConstructionRules.ReinstallCompleteHp(b.CurrentHp, stageMax, b.ScaleMaxHp);
		b.ReinstallBodyPlaced = false;
		b.ActiveRelocation = null;

		if (b.MoveOfUnfinishedConstruction)
		{
			b.MoveOfUnfinishedConstruction = false;
			b.ActiveFieldTask = FacilityFieldTaskKind.Construction;
			NoticeCenter.Instance?.PushMomentary($"{b.DisplayName} 재설치가 끝났습니다. 남은 건설을 이어갑니다.", NoticeCenter.QuestCompleteColor);
			return;
		}

		b.IsFormallyInstalled = true;
		b.ActiveFieldTask = FacilityFieldTaskKind.None;
		b.FieldWorkActive = false;
		if (b.VisualObject != null)
		{
			var sr = b.VisualObject.GetComponent<SpriteRenderer>();
			if (sr != null) sr.color = GetTintForKind(b.Kind, isBlueprint: false);
		}
		NoticeCenter.Instance?.PushMomentary($"{b.DisplayName} 재설치가 완료되었습니다.", NoticeCenter.QuestCompleteColor);
		FinishSingleTaskWorkers(b, CollectAssignedWorkers(b));
	}

	private bool IsFootprintClearOfUnits(Vector3Int pos, Vector2Int footprint)
	{
		GameSession session = GameSession.Instance;
		if (session == null) return true;
		foreach (Vector3Int tile in FootprintTiles(pos, footprint))
			if (session.unitGrid.ContainsKey(tile)) return false;
		return true;
	}

	public bool TryFindNearbyRelocationDestination(BuildingData b, out Vector3Int destination)
	{
		destination = b != null ? b.Position : Vector3Int.zero;
		if (b == null || GameSession.Instance == null) return false;

		GameSession session = GameSession.Instance;
		session.roomGrid.TryGetValue(b.Position, out Room curRoom);

		for (int radius = b.Footprint.x; radius <= 8; radius++)
		{
			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius) continue;
					Vector3Int cand = new Vector3Int(b.Position.x + dx, b.Position.y + dy, b.Position.z);
					if (curRoom != null && (!session.roomGrid.TryGetValue(cand, out Room cr) || cr != curRoom))
						continue;
					if (CanInstallAt(cand, b.Footprint, requireOwnership: true))
					{
						destination = cand;
						return true;
					}
				}
			}
		}
		return false;
	}

	public bool TryCommandDismantledPackagePickup(Unit carrier, InteractableObject pkgObj)
	{
		if (carrier == null || pkgObj?.DismantledBuildingPayload == null || carrier.hp <= 0f || !carrier.IsPlayerMonsterFaction || carrier.IsInEvolutionPresentation)
			return false;
		if (carrier.currentFloor != pkgObj.Position.z)
		{
			NoticeCenter.Instance?.PushMomentary("같은 층의 아군 몬스터에게만 분해 시설 운반을 명령할 수 있습니다.", NoticeCenter.WarningColor);
			return false;
		}

		NotifyWorkerCommandChanged(carrier);
		carrier.pendingDismantledPackagePickup = pkgObj;
		IssueInternalEconomyMove(carrier, new Vector2Int(pkgObj.Position.x, pkgObj.Position.y));
		NoticeCenter.Instance?.PushMomentary("분해된 시설 회수·재설치 명령을 내렸습니다.", NoticeCenter.QuestCompleteColor);
		return true;
	}

	public void CancelFacilityRelocation(BuildingData b)
	{
		if (b?.ActiveRelocation == null) return;
		b.ActiveRelocation.CancelOrInterrupt(resourceManager?.LifeEnergyAccount);
		if (!b.ActiveRelocation.IsDismantleFinished)
		{
			b.ActiveRelocation = null;
			b.ActiveFieldTask = FacilityFieldTaskKind.None;
			NoticeCenter.Instance?.PushMomentary("분해를 취소하고 비용을 환급했습니다.", NoticeCenter.WarningColor);
		}
	}

	// 시설 피격 처리(건설·수리·분해·파괴 규칙 통합 반영)
	public void ApplyDamageToBuilding(BuildingData b, float damage)
	{
		if (b == null || damage <= 0f) return;

		b.CurrentHp = Mathf.Max(0f, b.CurrentHp - damage);
		if (GameSession.Instance != null) b.LastHitGameSeconds = GameSession.Instance.ElapsedGameSeconds; // 내부 작업자의 '대응' 판정(건설 명세 1-6-표1)
		bool survived = b.CurrentHp > 0f;

		// 분해 도중 시설이 피격돼도 생존하면 원위치에 유지하고 작업/지불을 보존한다(건설 명세 2-6-표2). 파괴되면 지불액을 환급 없이 잃는다.
		if (!survived && b.ActiveFieldTask == FacilityFieldTaskKind.Relocation && b.ActiveRelocation != null && !b.ActiveRelocation.IsDismantleFinished)
		{
			b.ActiveRelocation.OnHitBeforeDismantleComplete(survived, resourceManager?.LifeEnergyAccount);
			b.ActiveRelocation = null;
			b.ActiveFieldTask = FacilityFieldTaskKind.None;
		}

		if (b.ActiveFieldTask == FacilityFieldTaskKind.Repair && b.ActiveRepair != null)
		{
			b.ActiveRepair.ApplyDamageAndRecalculate(b.CurrentHp, resourceManager?.LifeEnergyAccount);
			if (b.ActiveRepair.IsDestroyed)
			{
				b.ActiveRepair = null;
				b.ActiveFieldTask = FacilityFieldTaskKind.None;
			}
		}

		if (!survived)
			HandleBuildingDestroyedByDamage(b);
	}

	private void HandleBuildingDestroyedByDamage(BuildingData b)
	{
		if (b.ActiveConstructionOrUpgrade != null && !b.ActiveConstructionOrUpgrade.HasStartedWork && resourceManager != null)
			ResourceTransaction.ReleaseAll(resourceManager.Accounts, b.ActiveConstructionOrUpgrade.ReservedBeforeStart);

		if (resourceManager != null)
		{
			b.ProductionQueue?.OnFacilityDestroyed(resourceManager.Accounts);
			b.ResearchQueue?.OnFacilityLost(resourceManager.Accounts);
		}

		if (b.Kind == FacilityKind.CorpseRefinery)
		{
			b.Refinery.OnFacilityDestroyed(out var cancelledCorpseIds);
			HandleCancelledRefineryReservations(b, cancelledCorpseIds);
		}

		RemoveBuildingCompletely(b);
	}

	private void CompleteBuildingDemolish(BuildingData b)
	{
		if (resourceManager != null)
		{
			var baseExternal = new Dictionary<ResourceType, double>();
			if (b.OriginKind == FacilityOriginKind.PrePlacedOrCapturedDungeon)
				baseExternal[ResourceType.LifeEnergy] = GetBuildLifeEnergyCostForKind(b.Kind) * DefaultDemolishRecoveryRate;
			else if (b.OriginKind == FacilityOriginKind.HumanStructure)
				baseExternal[ResourceType.LifeEnergy] = 80.0;

			DemolitionMath.CreditDemolitionPayout(
				b.OriginKind,
				b.PaidBuildCosts,
				b.PaidUpgradeCosts,
				DefaultDemolishRecoveryRate,
				baseExternal,
				resourceManager.Accounts);

			b.ProductionQueue?.OnFacilityDemolished(resourceManager.Accounts);
			b.ResearchQueue?.OnFacilityLost(resourceManager.Accounts);
		}

		NoticeCenter.Instance?.PushMomentary($"{b.DisplayName} 철거가 완료되었습니다.", NoticeCenter.QuestCompleteColor);
		List<Unit> demolishWorkers = CollectAssignedWorkers(b); // 철거로 배정이 지워지기 전에 모은다
		ReleasePendingInsideCorpsesAtFootprint(b, GameSession.Instance); // 미배출 작업자 시체는 원래 시설 자리에 남는다(건설 명세 2-8-표1)
		RemoveBuildingCompletely(b);
		FinishSingleTaskWorkers(null, demolishWorkers);
	}

	// ── 단일 작업 종료와 자동 방어 복귀(배치모드 1-6 표2) ──

	private List<Unit> CollectAssignedWorkers(BuildingData b)
	{
		var workers = new List<Unit>();
		if (b == null || GameSession.Instance?.units == null) return workers;
		foreach (var u in GameSession.Instance.units)
			if (u != null && u.hp > 0f && u.assignedWorkBuilding == b && !u.assignedWorkIsInternal) workers.Add(u); // 외부 현장 작업자만 - 내부 작업자는 CollectInternalWorkers
		return workers;
	}

	// 단일 작업이 끝나 일손이 풀렸다 — 배정을 놓고 저장된 방어 위치로 복귀를 시도한다.
	private void FinishSingleTaskWorkers(BuildingData b, List<Unit> workers)
	{
		foreach (var w in workers)
		{
			if (b != null && w.assignedWorkBuilding == b) { w.assignedWorkBuilding = null; w.assignedWorkIsInternal = false; }
			ReleaseInsideUnit(w);
			ReleaseWorkPriority(w);
			DefensePlacementSystem.Instance?.OnWorkTaskEnded(w);
		}
	}

	private void RemoveBuildingCompletely(BuildingData b)
	{
		UnregisterBuildingFromGrid(b, clearObstacle: b.IsFormallyInstalled || (b.ActiveConstructionOrUpgrade != null && b.ActiveConstructionOrUpgrade.HasStartedWork));
		_allBuildings.Remove(b);
		// 안에 남은 생존 내부 작업자는 해제된 시설 영역에 겹치지 않게 나오고, 미배출·정식 보관 시체는 시설과 함께 소멸한다(건설 명세 2-8).
		EjectInsideWorkersAtFootprint(b, GameSession.Instance);
		b.PendingInsideCorpses.Clear();

		if (GameSession.Instance?.units != null)
		{
			foreach (var u in GameSession.Instance.units)
			{
				if (u == null) continue;
				u.workQueue?.Remove(b);
				if (u.assignedWorkBuilding == b)
				{
					bool wasInternal = u.assignedWorkIsInternal;
					AdvanceWorkQueue(u, b, notifyEnd: false);
					if (wasInternal) DefensePlacementSystem.Instance?.OnWorkTaskEnded(u); // 내부 작업이 끝났다 - 방어 위치로 복귀
				} // 현재 대상 취소/파괴 - 그 항목만 순서에서 빼고 다음 남은 건설을 확인한다(건설 명세 2-11)
			}
		}

		if (b.VisualObject != null)
			UnityEngine.Object.Destroy(b.VisualObject);
	}

	// ── 작업자 배정 · 시체 운반 · 피격/명령 변경 훅 ──

	public int CountAssignedWorkers(BuildingData b)
	{
		if (b == null || GameSession.Instance?.units == null) return 0;
		int count = 0;
		foreach (var u in GameSession.Instance.units)
			if (u != null && u.hp > 0f && b.IsWorkerInCurrentRole(u)) count++; // 현재 모드(내부 작업/현장 작업)의 인원만 센다
		return count;
	}

	public bool DoesBuildingAcceptWorkers(BuildingData b)
	{
		if (b == null || b.IsDummy) return false;
		if (b.ActiveFieldTask != FacilityFieldTaskKind.None) return true;
		return b.Kind == FacilityKind.LifeEnergy || b.Kind == FacilityKind.Research;
	}

	public bool TryAssignWorkerToBuilding(Unit worker, BuildingData b)
	{
		if (worker == null || b == null || worker.hp <= 0f || !worker.IsPlayerMonsterFaction || worker.IsInEvolutionPresentation)
			return false;
		if (worker.currentFloor != b.Position.z || !DoesBuildingAcceptWorkers(b))
			return false;

		bool internalRole = b.IsInternalWorkMode; // 시설 가동은 내부 작업, 그 밖의 건설·수리·강화·철거·이동은 외부 현장 작업이다
		bool sameRole = b.IsWorkerInCurrentRole(worker);
		if (!sameRole && !CheckInternalEntryAllowed(worker, b)) return false;
		if (!sameRole && CountAssignedWorkers(b) >= GetWorkerCap(b))
		{
			NoticeCenter.Instance?.PushMomentary($"해당 작업의 최대 투입 인원({GetWorkerCap(b)}기)이 모두 찼습니다.", NoticeCenter.WarningColor);
			return false;
		}

		// 같은 작업을 같은 유닛에게 다시 지시하면 작업 우선 - 그 일이 끝날 때까지 자동 공격/추격/능동 방어를 멈춘다(건설 명세 1-5). 처음 배정은 전투에 참여할 수 있는 일반 작업이다.
		bool reinstruct = sameRole;
		if (!reinstruct) NotifyWorkerCommandChanged(worker);
		worker.assignedWorkBuilding = b;
		worker.assignedWorkIsInternal = internalRole;
		worker.workPriorityActive = reinstruct;
		if (b.ActiveFieldTask == FacilityFieldTaskKind.Relocation && b.ActiveRelocation != null)
			b.ActiveRelocation.AssignedWorkerUnitId = worker.GetInstanceID();

		Vector2Int bOrigin = b.Origin2D;
		if (!WorkSpeedMath.IsOrthogonallyAdjacent(bOrigin, b.Footprint, worker.position, worker.FootprintSize))
		{
			if (TryPickNearestAdjacentWorkTile(bOrigin, b.Footprint, worker, out Vector2Int slot))
				IssueInternalEconomyMove(worker, slot, reinstruct);
		}
		else if (reinstruct)
		{
			worker.isHalted = true;
		}
		return true;
	}

	public bool TryAssignWorkerToCoreRestore(Unit worker, Vector3Int corePos)
	{
		if (worker == null || worker.hp <= 0f || !worker.IsPlayerMonsterFaction || worker.IsInEvolutionPresentation)
			return false;
		if (worker.currentFloor != corePos.z)
			return false;
		if (GameSession.Instance?.Core?.GetActiveRestoreAt(corePos) == null)
			return false;

		int existing = 0;
		if (GameSession.Instance.units != null)
		{
			foreach (var u in GameSession.Instance.units)
				if (u != null && u != worker && u.hp > 0f && u.assignedCoreRestorePos == corePos) existing++;
		}
		if (existing >= WorkSpeedMath.MaxWorkersPerTask)
		{
			NoticeCenter.Instance?.PushMomentary("코어 수복 최대 투입 인원(4기)이 모두 찼습니다.", NoticeCenter.WarningColor);
			return false;
		}

		NotifyWorkerCommandChanged(worker);
		worker.assignedCoreRestorePos = corePos;

		Vector2Int cOrigin = new Vector2Int(corePos.x, corePos.y);
		if (!WorkSpeedMath.IsOrthogonallyAdjacent(cOrigin, Vector2Int.one, worker.position, worker.FootprintSize))
		{
			if (TryPickNearestAdjacentWorkTile(cOrigin, Vector2Int.one, worker, out Vector2Int slot))
				IssueInternalEconomyMove(worker, slot);
		}
		return true;
	}

	// 명세서 v0.2 11~12항: 같은 층 아군 몬스터에게 시체 회수·운반 명령(출발 전 목적지 저장 자리 예약)
	public bool TryCommandCorpseRetrieve(Unit carrier, InteractableObject corpseObj, BuildingData explicitRefinery = null)
	{
		if (carrier == null || corpseObj?.CorpseEconomyData == null || carrier.hp <= 0f || !carrier.IsPlayerMonsterFaction || carrier.IsInEvolutionPresentation)
			return false;

		// 다른 층 몬스터에게는 회수 명령 불가(11-표2, 24-표1)
		if (carrier.currentFloor != corpseObj.Position.z)
		{
			NoticeCenter.Instance?.PushMomentary("같은 층에 있는 아군 몬스터에게만 시체 회수를 명령할 수 있습니다.", NoticeCenter.WarningColor);
			return false;
		}

		CorpseRecord corpse = corpseObj.CorpseEconomyData;
		if (!corpse.CanBeRefined)
		{
			NoticeCenter.Instance?.PushMomentary("부패가 100% 진행되어 정제할 수 없는 시체입니다.", NoticeCenter.WarningColor);
			return false;
		}

		if (explicitRefinery != null && IsRefineryBeingDismantled(explicitRefinery))
		{
			NoticeCenter.Instance?.PushMomentary("분해 중인 시체 보관·정제 통합시설에는 새 시체를 반입할 수 없습니다.", NoticeCenter.WarningColor);
			return false;
		}

		BuildingData targetRefinery = explicitRefinery ?? FindNearestValidRefinery(carrier.currentFloor, new Vector2Int(corpseObj.Position.x, corpseObj.Position.y), corpse);
		if (targetRefinery == null || !targetRefinery.Refinery.TryReserveIncomingSlot(corpse, targetRefinery.IsFormallyInstalled))
		{
			NoticeCenter.Instance?.PushMomentary("입고 가능한 시체 보관·정제 통합시설의 빈자리가 없습니다.", NoticeCenter.WarningColor);
			return false;
		}

		NotifyWorkerCommandChanged(carrier);
		carrier.pendingCorpsePickupTarget = corpseObj;
		carrier.targetRefineryForCarry = targetRefinery;
		IssueInternalEconomyMove(carrier, new Vector2Int(corpseObj.Position.x, corpseObj.Position.y));
		return true;
	}

	private static bool IsRefineryBeingDismantled(BuildingData b)
		=> b != null && b.Kind == FacilityKind.CorpseRefinery && b.ActiveFieldTask == FacilityFieldTaskKind.Relocation && b.FieldWorkActive;

	private BuildingData FindNearestValidRefinery(int floor, Vector2Int fromPos, CorpseRecord corpse, BuildingData exclude = null)
	{
		BuildingData best = null;
		float bestDist = float.MaxValue;
		for (int i = 0; i < _allBuildings.Count; i++)
		{
			BuildingData b = _allBuildings[i];
			if (b == null || b == exclude || b.Kind != FacilityKind.CorpseRefinery || b.Position.z != floor)
				continue;
			if (IsRefineryBeingDismantled(b)) continue; // 분해 착수부터 신규 반입을 멈춘다(건설 명세 2-8)
			if (!b.Refinery.CanAcceptIncoming(corpse, b.IsFormallyInstalled))
				continue;
			float d = Vector2Int.Distance(fromPos, b.Origin2D);
			if (d < bestDist)
			{
				bestDist = d;
				best = b;
			}
		}
		return best;
	}

	// 플레이어가 작업자/운반자에게 다른 이동·공격 명령([이동 적용] 포함)을 내렸을 때의 상태 정리(5-표5, 12-표2, 20-2-표1, 20-3항)
	public void NotifyWorkerCommandChanged(Unit u)
	{
		if (u == null) return;
		DefensePlacementSystem.Instance?.OnPlayerCommandReplaced(u); // 발행했던 방어 이동·대기 자리를 해제한다

		if (u.assignedWorkBuilding != null)
		{
			BuildingData b = u.assignedWorkBuilding;
			if (b.ActiveFieldTask == FacilityFieldTaskKind.Relocation && b.ActiveRelocation != null && !u.assignedWorkIsInternal)
			{
				// [이동 적용]으로 분해 작업자 이탈 시 분해 취소·환급 없이 진행·지불 보존(20-2-표1)
				b.ActiveRelocation.OnWorkerMovedAwayDuringDismantle();
			}
			u.assignedWorkBuilding = null;
			u.assignedWorkIsInternal = false;
		}
		ReleaseInsideUnit(u); // 시설 안에서 일하던 유닛이면 새 명령을 위해 나온다(건설 명세 1-6)

		u.assignedCoreRestorePos = null;
		u.assignedDoorExpansion = null;
		ReleaseWorkPriority(u);
		u.workQueue = null; // 직접 이동/다른 작업/직접 전투 명령은 현재와 이후 건설 배정을 함께 해제한다(건설 명세 2-11)

		if (u.pendingCorpsePickupTarget != null)
		{
			string cId = u.pendingCorpsePickupTarget.CorpseEconomyData?.CorpseId;
			u.targetRefineryForCarry?.Refinery.CancelIncomingReservation(cId);
			u.pendingCorpsePickupTarget = null;
			u.targetRefineryForCarry = null;
		}

		if (u.carriedCorpse != null)
		{
			u.targetRefineryForCarry?.Refinery.CancelIncomingReservation(u.carriedCorpse.CorpseId);
			DropCorpseToField(u.carriedCorpse, new Vector3Int(u.position.x, u.position.y, u.currentFloor), livingCarrier: u, carrierCorpseOnSameTile: null);
			u.carriedCorpse = null;
			u.targetRefineryForCarry = null;
		}

		if (u.carriedDismantledBuilding != null)
		{
			DropDismantledBuildingPackage(u.carriedDismantledBuilding, new Vector3Int(u.position.x, u.position.y, u.currentFloor), carrierCorpseOnSameTile: null);
			u.carriedDismantledBuilding = null;
		}
		u.pendingDismantledPackagePickup = null;
		u.pickupHoldStartedAt = null;
	}

	// 작업자·운반자 피격 처리(건설 명세 1-5, 2-6-표2, 2-8-표2) - 작업자는 배정·진행·지불·목적지를 보존하며 전투 참여와 10초 경계 뒤 재개는 FSM과 TickWorkerResume이 맡는다.
	public void NotifyUnitHit(Unit u)
	{
		if (u == null || !u.IsPlayerMonsterFaction) return;
		// 작업 우선(재지시) 중이면 전투로 전환하지 않고 들고 있는 것도 그대로 운반한다(2-8-표2).
		if (u.hp <= 0f || u.workPriorityActive) return;

		// 시체/분해물 운반 중 직접 피격: 내려놓고 전투에 참여한다. 배정/목적지/입고 예약 1구는 유지하고 전투/경계 뒤 다시 회수한다.
		if (u.carriedCorpse != null)
		{
			InteractableObject dropped = DropCorpseToField(u.carriedCorpse, new Vector3Int(u.position.x, u.position.y, u.currentFloor), livingCarrier: u, carrierCorpseOnSameTile: null);
			u.carriedCorpse = null;
			if (dropped != null && u.targetRefineryForCarry != null)
			{
				u.pendingCorpsePickupTarget = dropped;
				IssueInternalEconomyMove(u, new Vector2Int(dropped.Position.x, dropped.Position.y));
			}
			else
			{
				u.targetRefineryForCarry = null;
			}
		}
		else if (u.carriedDismantledBuilding != null)
		{
			InteractableObject dropped = DropDismantledBuildingPackage(u.carriedDismantledBuilding, new Vector3Int(u.position.x, u.position.y, u.currentFloor), carrierCorpseOnSameTile: null);
			u.carriedDismantledBuilding = null;
			if (dropped != null)
			{
				u.pendingDismantledPackagePickup = dropped;
				IssueInternalEconomyMove(u, new Vector2Int(dropped.Position.x, dropped.Position.y));
			}
		}
	}

	private double _nextWorkerResumeAt;

	// 전투/경계(전투 후 10초)가 끝났는데 작업 자리에 없는 배정 작업자를 작업 자리로 돌려보낸다(건설 명세 1-5, 2-11). 이동은 전투/경계에 밀리는 자동 이동이라 재발행이 겹쳐도 안전하다.
	private void TickWorkerResume(GameSession session)
	{
		double now = session.ElapsedGameSeconds;
		if (now < _nextWorkerResumeAt) return;
		_nextWorkerResumeAt = now + 0.5;

		for (int i = 0; i < session.units.Count; i++)
		{
			Unit u = session.units[i];
			if (u == null) continue;
			if (u.assignedCoreRestorePos.HasValue) ResumeCoreRestoreWorker(u, session);
			else if (u.assignedDoorExpansion != null) ResumeDoorWorker(u);
			else ResumeBuildingWorker(u, now);
		}
	}

	// 작업자가 작업 자리에 없고 전투·경계·다른 명령 중이 아니면 돌아가야 한다.
	private static bool ShouldReturnToWorkSpot(Unit u, bool atWorkSpot)
		=> ConstructionRules.ShouldResumeAssignedWork(true, atWorkSpot, u.IsInCombatOrAlertState, u.playerMoveTarget.HasValue || u.isHalted || u.IsHaulingCorpseOrPackage);

	private void ResumeCoreRestoreWorker(Unit u, GameSession session)
	{
		Vector3Int cp = u.assignedCoreRestorePos.Value;
		if (u.hp <= 0f || !u.IsPlayerMonsterFaction || u.IsStunnedOrEvolving || u.currentFloor != cp.z || session.Core?.GetActiveRestoreAt(cp) == null) return;
		var coreOrigin = new Vector2Int(cp.x, cp.y);
		bool atCore = WorkSpeedMath.IsOrthogonallyAdjacent(coreOrigin, Vector2Int.one, u.position, u.FootprintSize);
		if (ShouldReturnToWorkSpot(u, atCore) && TryPickNearestAdjacentWorkTile(coreOrigin, Vector2Int.one, u, out Vector2Int slot))
			IssueInternalEconomyMove(u, slot);
	}

	private void ResumeBuildingWorker(Unit u, double now)
	{
		BuildingData b = u.assignedWorkBuilding;
		if (b == null || u.hp <= 0f || !u.IsPlayerMonsterFaction || u.IsStunnedOrEvolving) return;
		if (u.IsInsideFacility || now < u.workResumeHoldUntil) return; // 안에서 일하는 중이거나 비켜 준 직후다
		if (!_allBuildings.Contains(b) || u.currentFloor != b.Position.z) return;

		Vector2Int origin = b.Origin2D;
		bool atSpot = WorkSpeedMath.IsOrthogonallyAdjacent(origin, b.Footprint, u.position, u.FootprintSize);
		if (ShouldReturnToWorkSpot(u, atSpot) && TryPickNearestAdjacentWorkTile(origin, b.Footprint, u, out Vector2Int slot))
			IssueInternalEconomyMove(u, slot);
	}

	// 작업 우선(재지시) 상태를 끝낸다 - 그 상태가 만든 정지만 푼다(건설 명세 1-5).
	private static void ReleaseWorkPriority(Unit u)
	{
		if (u == null || !u.workPriorityActive) return;
		u.workPriorityActive = false;
		u.isHalted = false;
	}

	// 운반자 사망 시: 인접 빈 공간이 없으면 운반자의 시체와 운반 중이던 시체/1타일 분해 시설을 별도 대상으로 함께 보존한다(12-표2, 20-3-표1, 24-표1).
	public void NotifyUnitDied(Unit deadUnit, InteractableObject carrierCorpseObj)
	{
		if (deadUnit == null) return;

		deadUnit.assignedWorkBuilding = null;
		deadUnit.assignedWorkIsInternal = false;
		deadUnit.assignedCoreRestorePos = null;
		deadUnit.assignedDoorExpansion = null;

		if (deadUnit.pendingCorpsePickupTarget != null)
		{
			string cId = deadUnit.pendingCorpsePickupTarget.CorpseEconomyData?.CorpseId;
			deadUnit.targetRefineryForCarry?.Refinery.CancelIncomingReservation(cId);
			deadUnit.pendingCorpsePickupTarget = null;
		}

		Vector3Int deathTile = new Vector3Int(deadUnit.position.x, deadUnit.position.y, deadUnit.currentFloor);

		if (deadUnit.carriedCorpse != null)
		{
			deadUnit.targetRefineryForCarry?.Refinery.CancelIncomingReservation(deadUnit.carriedCorpse.CorpseId);
			DropCorpseToField(deadUnit.carriedCorpse, deathTile, livingCarrier: null, carrierCorpseOnSameTile: carrierCorpseObj);
			deadUnit.carriedCorpse = null;
		}
		deadUnit.targetRefineryForCarry = null;

		if (deadUnit.carriedDismantledBuilding != null)
		{
			DropDismantledBuildingPackage(deadUnit.carriedDismantledBuilding, deathTile, carrierCorpseOnSameTile: carrierCorpseObj);
			deadUnit.carriedDismantledBuilding = null;
		}
	}

	public InteractableObject DropCorpseToField(CorpseRecord corpse, Vector3Int nearPos, Unit livingCarrier, InteractableObject carrierCorpseOnSameTile)
	{
		if (corpse == null || GameSession.Instance == null) return null;
		corpse.IsInsideRefinery = false;

		GameSession session = GameSession.Instance;
		Vector3Int dropPos = FindFreeAdjacentDropTile(nearPos, out bool foundAdjacent) ? FindFreeAdjacentDropTileValue(nearPos) : nearPos;

		var droppedObj = new InteractableObject(
			corpse.CorpseId,
			dropPos,
			80f,
			20f,
			new List<string> { "Object/Passable/Corpse", corpse.OriginFaction == CorpseOriginFaction.HumanEnemy ? "Human" : "Monster" })
		{
			CorpseEconomyData = corpse,
		};

		if (!foundAdjacent && carrierCorpseOnSameTile != null && dropPos == carrierCorpseOnSameTile.Position)
		{
			// 인접 공간 없이 운반자가 사망한 경우 두 시체를 별도 오브젝트로 같은 타일에 함께 보존한다(12-표2, 24-표1).
			carrierCorpseOnSameTile.CoexistingObjects ??= new List<InteractableObject>();
			carrierCorpseOnSameTile.CoexistingObjects.Add(droppedObj);
			return droppedObj;
		}

		if (session.objectGrid.TryGetValue(dropPos, out var existing))
		{
			existing.CoexistingObjects ??= new List<InteractableObject>();
			existing.CoexistingObjects.Add(droppedObj);
			return droppedObj;
		}

		session.SpawnObject(droppedObj, Color.gray);
		return droppedObj;
	}

	public InteractableObject DropDismantledBuildingPackage(BuildingData b, Vector3Int nearPos, InteractableObject carrierCorpseOnSameTile)
	{
		if (b == null || GameSession.Instance == null) return null;
		b.ActiveRelocation?.DropPackageToField();

		GameSession session = GameSession.Instance;
		Vector3Int dropPos = FindFreeAdjacentDropTile(nearPos, out bool foundAdj) ? FindFreeAdjacentDropTileValue(nearPos) : nearPos;

		var pkgObj = new InteractableObject(
			$"Dismantled_{b.FacilityId}_{Guid.NewGuid().ToString().Substring(0, 4)}",
			dropPos,
			40f,
			0f,
			new List<string> { InteractableObject.DismantledFacilityTag })
		{
			DismantledBuildingPayload = b,
		};

		if (!foundAdj && carrierCorpseOnSameTile != null && dropPos == carrierCorpseOnSameTile.Position)
		{
			carrierCorpseOnSameTile.CoexistingObjects ??= new List<InteractableObject>();
			carrierCorpseOnSameTile.CoexistingObjects.Add(pkgObj);
			return pkgObj;
		}

		if (session.objectGrid.TryGetValue(dropPos, out var existing))
		{
			existing.CoexistingObjects ??= new List<InteractableObject>();
			existing.CoexistingObjects.Add(pkgObj);
			return pkgObj;
		}

		session.SpawnObject(pkgObj, new Color(0.95f, 0.8f, 0.35f, 1f));
		return pkgObj;
	}

	private bool FindFreeAdjacentDropTile(Vector3Int center, out bool found)
	{
		found = TryGetFreeAdjacentDropTile(center, out _);
		return found;
	}

	private Vector3Int FindFreeAdjacentDropTileValue(Vector3Int center)
		=> TryGetFreeAdjacentDropTile(center, out var pos) ? pos : center;

	private bool TryGetFreeAdjacentDropTile(Vector3Int center, out Vector3Int result)
	{
		result = center;
		GameSession session = GameSession.Instance;
		if (session == null) return false;

		Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
		for (int i = 0; i < dirs.Length; i++)
		{
			Vector3Int cand = new Vector3Int(center.x + dirs[i].x, center.y + dirs[i].y, center.z);
			if (buildingGrid.ContainsKey(cand) || session.objectGrid.ContainsKey(cand) || session.unitGrid.ContainsKey(cand))
				continue;
			if (createMap != null && !createMap.IsStaticTileWalkable(cand.z, new Vector2Int(cand.x, cand.y)))
				continue;
			result = cand;
			return true;
		}
		return false;
	}

	// 정제시설 전체 비우기(13-2항) UI 호출 진입점
	public void StartRefineryDrain(BuildingData refineryBuilding)
	{
		if (refineryBuilding == null || refineryBuilding.Kind != FacilityKind.CorpseRefinery) return;

		refineryBuilding.Refinery.StartDrainMode(out var cancelledIds, out var ejectedWaiting);
		HandleCancelledRefineryReservations(refineryBuilding, cancelledIds);

		for (int i = 0; i < ejectedWaiting.Count; i++)
			DropCorpseToField(ejectedWaiting[i], refineryBuilding.Position, livingCarrier: null, carrierCorpseOnSameTile: null);

		NoticeCenter.Instance?.PushMomentary("시체 보관·정제 시설 전체 비우기를 시작했습니다.", NoticeCenter.WarningColor);
	}

	public void CancelRefineryDrain(BuildingData refineryBuilding)
	{
		if (refineryBuilding == null || refineryBuilding.Kind != FacilityKind.CorpseRefinery) return;
		refineryBuilding.Refinery.CancelDrainMode();
		NoticeCenter.Instance?.PushMomentary("전체 비우기를 해제했습니다.", NoticeCenter.QuestCompleteColor);
	}

	public void EjectSingleWaitingCorpse(BuildingData refineryBuilding, string corpseId)
	{
		if (refineryBuilding == null || refineryBuilding.Kind != FacilityKind.CorpseRefinery) return;
		if (refineryBuilding.Refinery.TryEjectWaitingCorpse(corpseId, out var ejected))
			DropCorpseToField(ejected, refineryBuilding.Position, livingCarrier: null, carrierCorpseOnSameTile: null);
	}

	private void HandleCancelledRefineryReservations(BuildingData lostOrDrainingRefinery, List<string> cancelledIds)
	{
		if (cancelledIds == null || cancelledIds.Count == 0 || GameSession.Instance?.units == null) return;
		var idSet = new HashSet<string>(cancelledIds);

		foreach (var u in GameSession.Instance.units)
		{
			if (u == null || u.targetRefineryForCarry != lostOrDrainingRefinery) continue;

			// 아직 시체를 회수하기 전이면 시체를 현장에 두고 명령 종료(12-표2, 13-2-표1)
			if (u.pendingCorpsePickupTarget != null)
			{
				u.pendingCorpsePickupTarget = null;
				u.targetRefineryForCarry = null;
				u.suppressEconomyCommandHook = true;
				u.ClearPlayerCommand();
				u.suppressEconomyCommandHook = false;
				continue;
			}

			// 이미 운반 중이면 다른 유효 시설을 재탐색하고 없으면 내려놓는다(12-표2, 13-2-표1, 24-표1)
			if (u.carriedCorpse != null && idSet.Contains(u.carriedCorpse.CorpseId))
			{
				BuildingData alt = FindNearestValidRefinery(u.currentFloor, u.position, u.carriedCorpse, exclude: lostOrDrainingRefinery);
				if (alt != null && alt.Refinery.TryReserveIncomingSlot(u.carriedCorpse, alt.IsFormallyInstalled))
				{
					u.targetRefineryForCarry = alt;
					if (TryPickNearestAdjacentWorkTile(new Vector2Int(alt.Position.x, alt.Position.y), alt.Footprint, u, out Vector2Int slot))
						IssueInternalEconomyMove(u, slot);
				}
				else
				{
					DropCorpseToField(u.carriedCorpse, new Vector3Int(u.position.x, u.position.y, u.currentFloor), livingCarrier: u, carrierCorpseOnSameTile: null);
					u.carriedCorpse = null;
					u.targetRefineryForCarry = null;
					u.suppressEconomyCommandHook = true;
					u.ClearPlayerCommand();
					u.suppressEconomyCommandHook = false;
				}
			}
		}
	}

	// 방 클리어 시 야생 생성 거점을 플레이어 소속 생산시설로 전환하고 시설 해금을 기록한다(8-1-표1, 23-표1).
	public void ConvertWildFacilitiesOnRoomSecured(Room securedRoom)
	{
		if (securedRoom == null || GameSession.Instance == null) return;
		GameSession session = GameSession.Instance;

		var wildSpawners = new List<Unit>();
		foreach (var u in session.units)
		{
			if (u == null || u.hp <= 0f || u.currentFloor != securedRoom.Floor) continue;
			if (!(u.unitType is WildBaseType)) continue;
			if (session.roomGrid.TryGetValue(new Vector3Int(u.position.x, u.position.y, u.currentFloor), out Room r) && r == securedRoom)
				wildSpawners.Add(u);
		}

		for (int i = 0; i < wildSpawners.Count; i++)
		{
			Unit spawner = wildSpawners[i];
			Vector3Int spawnerPos = new Vector3Int(spawner.position.x, spawner.position.y, spawner.currentFloor);

			// 야생 자동 생성을 중단하고 해당 위치(또는 인접 가능 위치)를 플레이어 생산시설로 전환한다.
			spawner.Health.hp = 0f;
			session.UnregisterUnitPos(spawner, spawner.position);
			session.units.Remove(spawner);
			unitGenerate?.RemoveVisual(spawner, 0f);
			UnityEngine.Object.Destroy(spawner);

			resourceManager?.UnlockFacilityKind(FacilityKind.UnitProduction);
			Sprite prodSprite = _cachedUnitBuildingSprite;
			Vector2Int fp = UnitBuildingFootprint;
			if (CanInstallAt(spawnerPos, fp, requireOwnership: true))
				InstallCompletedFacility(FacilityKind.UnitProduction, spawnerPos, ProductionRule.CreateDefaultPlayerUnitRules(), prodSprite, FacilityOriginKind.PrePlacedOrCapturedDungeon);
		}
	}

	// 개체 진화 버튼 실행(15-1항, 15-2항)
	public bool TryTriggerUnitEvolution(Unit unit)
	{
		if (unit == null || unit.hp <= 0f || !unit.IsPlayerMonsterFaction || resourceManager == null || GameSession.Instance == null)
			return false;

		var def = new EvolutionDefinition
		{
			EvolutionId = $"evo_{unit.unitType?.typeName ?? "monster"}_t{unit.currentEvolutionTier + 1}",
			DisplayName = $"{unit.unitType?.typeName ?? "몬스터"} {unit.currentEvolutionTier + 1}차 진화",
			ResultSpeciesKey = unit.unitType?.GetType().Name ?? "MeleeTank",
			BaseSoulShardCost = 50.0 * (unit.currentEvolutionTier + 1),
			PresentationDurationSeconds = 2,
			TotalMaxHpIncrease = 80f,
			TotalFlatStatIncreases = new List<(DetailStat, float)>
			{
				(DetailStat.PhysicalAttack, 6f),
				(DetailStat.PhysicalDefense, 5f),
			},
			PostEvolutionFootprint = unit.FootprintSize,
			PostEvolutionPopulationCost = unit.populationCost,
		};

		GameSession session = GameSession.Instance;
		Room room = null;
		session.roomGrid.TryGetValue(new Vector3Int(unit.position.x, unit.position.y, unit.currentFloor), out room);

		int roomLivingPop = room != null ? room.CurrentPopulation : unit.populationCost;
		int roomMaxPop = room != null ? room.MaxPopulation : 999;
		bool spaceOk = IsEvolutionFootprintClearForUnit(unit, def.PostEvolutionFootprint);

		SavedUnitCommandSnapshot snap = unit.CaptureCommandSnapshot();
		EvolutionStartBlock block = EvolutionSession.TryBeginEvolution(
			def,
			unit.IsInEvolutionPresentation,
			isQualifiedAndUnlocked: unit.currentEvolutionTier < 2,
			isSoulShardSpendUnlocked: resourceManager.IsSoulShardSpendUnlocked,
			costMultiplier: 1.0,
			soulShardAccount: resourceManager.SoulShardAccount,
			hasPostEvolutionFootprintSpace: spaceOk,
			currentRoomLivingPop: roomLivingPop,
			currentUnitPopCost: unit.populationCost,
			roomMaxPop: roomMaxPop,
			commandSnapshotToSave: snap,
			ref unit.cumulativeBaseEvolutionSoulCost,
			out EvolutionSession started,
			out string failMsg);

		if (block != EvolutionStartBlock.None)
		{
			if (!string.IsNullOrEmpty(failMsg))
				NoticeCenter.Instance?.PushMomentary(failMsg, NoticeCenter.WarningColor);
			return false;
		}

		unit.suppressEconomyCommandHook = true;
		unit.ClearPlayerCommand();
		unit.suppressEconomyCommandHook = false;
		unit.activeEvolutionSession = started;
		NoticeCenter.Instance?.PushMomentary($"{def.DisplayName} 연출을 시작합니다.", NoticeCenter.QuestCompleteColor);
		return true;
	}

	private bool IsEvolutionFootprintClearForUnit(Unit evolvingUnit, Vector2Int postFootprint)
	{
		GameSession session = GameSession.Instance;
		if (session == null) return true;

		for (int dx = 0; dx < postFootprint.x; dx++)
		{
			for (int dy = 0; dy < postFootprint.y; dy++)
			{
				Vector3Int tile = new Vector3Int(evolvingUnit.position.x + dx, evolvingUnit.position.y + dy, evolvingUnit.currentFloor);
				if (buildingGrid.ContainsKey(tile)) return false;
				if (createMap != null && !createMap.IsStaticTileWalkable(tile.z, new Vector2Int(tile.x, tile.y))) return false;
				if (session.unitGrid.TryGetValue(tile, out Unit occ) && occ != null && occ != evolvingUnit && occ.hp > 0f)
					return false;
			}
		}
		return true;
	}

	// ── 메인 루프 (UpdateProcess) ──

	// UpdateProcess가 프레임마다 재사용하는 버퍼(재진입 없음) - 매 프레임 새 컬렉션을 만들지 않는다.
	private readonly List<BuildingData> _tickBuildingSnapshot = new List<BuildingData>();
	private readonly List<float> _fieldWorkerSpeeds = new List<float>(4);
	private readonly List<float> _insideWorkerSpeeds = new List<float>(4);
	private readonly List<WorkerContributionInput> _insideLifeWorkers = new List<WorkerContributionInput>(4);
	private readonly HashSet<Room> _roomsWithReadyProduction = new HashSet<Room>();

	public override void UpdateProcess()
	{
		UpdateFrontZoneViews(); // 일시정지 중에도 계획의 정면 구역을 보여 준다
		UpdateProgressBars(); // 외부 작업 진행 바(건설 명세 2-13)
		float dt = Time.deltaTime;
		// InputManager 일시정지는 Time.timeScale = 0.0001f를 쓰므로 timeScale/isPaused를 함께 확인해 일시정지 중 진행을 0으로 차단한다(4-표2, 5-표5, 23-표1).
		if (dt <= 0f || Time.timeScale < 0.01f || (GameSession.Instance != null && GameSession.Instance.isPaused))
			return;

		GameSession session = GameSession.Instance;
		double now = session != null ? session.ElapsedGameSeconds : Time.time;

		// 1. 시체 및 분해 시설 운반자 진행
		if (session?.units != null)
		{
			TickCorpseCarriers(session, dt);
			TickWorkerResume(session);
			TickInternalWork(session); // 내부 작업자의 입장·퇴장·비켜주기(건설 명세 1-6)
			TickDoorExpansions(session, dt); // 문 확장 작업(건설 명세 2-9)
		}

		// 2. 코어 수복 작업 진행
		session?.Core?.TickActiveRestores(dt, session.units);

		// 3. 개별 시설 틱 (건설·업그레이드·수리·철거·분해·생명 에너지·자동 정제·연구·유닛 생산 진행)
		var buildingsSnapshot = _tickBuildingSnapshot;
		buildingsSnapshot.Clear();
		buildingsSnapshot.AddRange(_allBuildings);
		var workerSpeedBuffer = _fieldWorkerSpeeds;
		var lifeWorkerBuffer = _insideLifeWorkers;
		var internalSpeedBuffer = _insideWorkerSpeeds;
		var roomsWithReadyProduction = _roomsWithReadyProduction;
		roomsWithReadyProduction.Clear();

		for (int i = 0; i < buildingsSnapshot.Count; i++)
		{
			BuildingData b = buildingsSnapshot[i];
			if (b == null || !_allBuildings.Contains(b) || b.IsDummy) continue;

			// 현장 작업자(시설 둘레)와 내부 작업자(시설 안)는 역할이 달라 입력 버퍼가 따로다 - 내부 작업자는 수리·강화가 실제로 진행되지 않는 동안 계속 가동에 기여한다(건설 명세 1-2-1·1-6).
			GatherActiveAdjacentWorkers(b, session?.units, workerSpeedBuffer);
			GatherInsideWorkers(b, internalSpeedBuffer, lifeWorkerBuffer);

			if (b.ActiveFieldTask != FacilityFieldTaskKind.None)
			{
				TickBuildingFieldTask(b, dt, workerSpeedBuffer);
				b.WaitReason = ComputeWaitReason(b, workerSpeedBuffer.Count);
				if (b.ActiveFieldTask == FacilityFieldTaskKind.None) b.FieldWorkActive = false;
				// 현장 작업 중인 시설도 가동 조건(FieldWorkActive)이 허용하면 아래 가동 틱을 이어서 돈다(건설 명세 2-3). 철거/분해로 사라졌거나 미완공이면 건너뛴다.
				if (!_allBuildings.Contains(b) || !b.IsFormallyInstalled) continue;
			}
			else
			{
				b.FieldWorkActive = false;
				b.WaitReason = b.IsFormallyInstalled && !BuildingManager.AreCoreFunctionsActive(b.Position.z) ? "코어 수복 대기" : string.Empty;
			}

			bool operational = b.IsOperational;
			double dmgE = b.DamageEfficiency; // 손상 효율(건설 명세 2-4)

			if (b.Kind == FacilityKind.LifeEnergy && resourceManager != null)
			{
				double earned = b.LifeEnergyCycle.Advance(dt, lifeWorkerBuffer, operational, 0.0, dmgE);
				if (earned > 0.0)
					resourceManager.AddResource(ResourceType.LifeEnergy, earned, ResourceCreditSource.FacilityProduction);
			}
			else if (b.Kind == FacilityKind.CorpseRefinery && resourceManager != null)
			{
				b.Refinery.TickAutoRefine(dt, operational, dmgE, resourceManager.Accounts, null, dmgE);
			}
			else if (b.Kind == FacilityKind.Research && resourceManager != null && b.ResearchQueue != null)
			{
				bool researchCompleted = b.ResearchQueue.TickResearch(
					dt,
					internalSpeedBuffer,
					operational,
					dmgE,
					1.0,
					resourceManager.Accounts,
					facilityDestroyedOnSameTick: false,
					onCompleted: def => ApplyCompletedResearchToExistingUnits(def));
				// 연구 대기열 소진도 단일 작업 종료다.
				if (researchCompleted && b.ResearchQueue.Entries.Count == 0) FinishSingleTaskWorkers(b, CollectInternalWorkers(b));
			}
			else if (b.Kind == FacilityKind.UnitProduction && resourceManager != null)
			{
				b.ProductionQueue.TickProduction(dt, now, operational, dmgE, 1.0, resourceManager.Accounts);
				if (b.ProductionQueue.HasWaitingSpawnCandidate && session != null && session.roomGrid.TryGetValue(b.Position, out Room prodRoom) && prodRoom != null)
					roomsWithReadyProduction.Add(prodRoom);
			}
		}

		// 4. 방 단위 다중 생산시설 100% 출현 중재(10-표3)
		if (session != null && roomsWithReadyProduction.Count > 0)
		{
			foreach (Room room in roomsWithReadyProduction)
				ArbitrateRoomUnitSpawns(room, session);
		}
	}

	private void TickBuildingFieldTask(BuildingData b, float dt, List<float> workerSpeeds)
	{
		bool coreActive = true; // 건설/수리/강화/분해 현장 작업은 확보한 방에서 코어 파괴 중에도 진행한다(건설 명세 1-7)

		switch (b.ActiveFieldTask)
		{
			case FacilityFieldTaskKind.Construction:
			case FacilityFieldTaskKind.Upgrade:
			{
				var session = b.ActiveConstructionOrUpgrade;
				if (session == null) { b.ActiveFieldTask = FacilityFieldTaskKind.None; break; }

				bool siteClear = session.IsUpgrade || session.HasStartedWork || IsConstructionFootprintClearOfUnits(b);
				if (!siteClear && GameSession.Instance != null && CountAssignedWorkers(b) > 0)
					RequestAlliesToMakeWay(b, GameSession.Instance); // 착공할 때 대기 아군은 비켜 준다(건설 명세 2-1-표2)
				var actualCosts = new List<(ResourceType, double)>();
				if (!session.IsUpgrade)
					actualCosts.Add((ResourceType.LifeEnergy, GetBuildLifeEnergyCostForKind(b.Kind)));
				else
				{
					actualCosts.Add((ResourceType.LifeEnergy, ResourceManager.FacilityUpgradeLifeEnergyCost));
					actualCosts.Add((ResourceType.SoulShard, ResourceManager.FacilityUpgradeSoulShardCost));
				}

				bool prevStarted = session.HasStartedWork;
				float curHp = b.CurrentHp;
				float curMax = b.MaxHp;
				bool completed = session.Tick(
					dt,
					workerSpeeds,
					coreActive,
					siteClear,
					actualCosts,
					resourceManager?.Accounts,
					ref curHp,
					ref curMax);
				b.CurrentHp = curHp;
				b.MaxHp = curMax;
				b.FieldWorkActive = session.IsUpgrade && session.HasStartedWork && !session.IsEnded && workerSpeeds.Count > 0;

				if (!prevStarted && session.HasStartedWork && !session.IsUpgrade)
				{
					// 착공 시 시공체는 건설 단계의 최대 HP로 생성된다(건설 명세 2-2). 완료되면 완성 시설의 최대 HP로 전부 회복한다.
					if (!completed)
					{
						b.MaxHp = ConstructionRules.ConstructionStageMaxHp(session.TargetCompletedMaxHp);
						b.CurrentHp = b.MaxHp;
					}
					// 첫 작업 시작 시 실제 건설 오브젝트 점유 활성화 및 투자액 기록(16-표2)
					UpdateMapDataObstacle(b.Position, b.Footprint, true);
					AccumulateCosts(b.PaidBuildCosts, session.PaidCosts);
				}

				if (completed)
				{
					if (session.IsUpgrade)
					{
						b.UpgradeTier++;
						AccumulateCosts(b.PaidUpgradeCosts, session.PaidCosts);
					}
					else
					{
						b.IsFormallyInstalled = true;
						if (b.VisualObject != null)
						{
							var sr = b.VisualObject.GetComponent<SpriteRenderer>();
							if (sr != null) sr.color = GetTintForKind(b.Kind, isBlueprint: false);
						}
					}
					b.ActiveConstructionOrUpgrade = null;
					b.ActiveFieldTask = FacilityFieldTaskKind.None;
					NoticeCenter.Instance?.PushMomentary($"{b.DisplayName} 작업이 완료되었습니다.", NoticeCenter.QuestCompleteColor);
					// 건설 작업자는 내부 작업자가 아니다 - 순서에 남은 건설이 있으면 다음 현장으로, 없으면 배정을 풀고 방어 위치로 복귀한다(건설 명세 1-2-1, 2-11).
					foreach (Unit doneWorker in CollectAssignedWorkers(b)) AdvanceWorkQueue(doneWorker, b, notifyEnd: true);
				}
				break;
			}
			case FacilityFieldTaskKind.Repair:
			{
				if (b.ActiveRepair == null) { b.ActiveFieldTask = FacilityFieldTaskKind.None; break; }
				float curHp = b.CurrentHp;
				bool done = b.ActiveRepair.Tick(dt, workerSpeeds, coreActive, resourceManager?.LifeEnergyAccount, ref curHp);
				b.CurrentHp = curHp;
				b.FieldWorkActive = !done && ConstructionRules.IsFieldWorkStoppingFacility(b.ActiveRepair.HasStartedWork, workerSpeeds.Count, b.ActiveRepair.IsPausedWaitingDiffApproval);
				if (done)
				{
					b.ActiveRepair = null;
					b.ActiveFieldTask = FacilityFieldTaskKind.None;
					NoticeCenter.Instance?.PushMomentary($"{b.DisplayName} 수리가 완료되었습니다.", NoticeCenter.QuestCompleteColor);
					FinishSingleTaskWorkers(b, CollectAssignedWorkers(b)); // 수리 작업자는 내부 작업자가 아니다 - 배정을 풀고 방어 위치로 복귀한다
				}
				break;
			}
			case FacilityFieldTaskKind.Demolish:
			{
				// 살아 있는 내부 작업자는 실제 철거 전에 나온다 - 나오지 못하면 착수하지 않고 퇴장 대기를 유지한다(건설 명세 2-5).
				if (workerSpeeds.Count > 0 && BlockUntilInsideWorkersExit(b, InternalWorkRules.ExitCause.Demolish)) break;
				float coopSpeed = WorkSpeedMath.CalculateTotalWorkSpeed(workerSpeeds);
				b.FieldWorkActive = coopSpeed > 0f; // 철거 착수부터 가동/새 생산 예약을 멈춘다(건설 명세 2-5-표2)
				if (coopSpeed <= 0f) break;
				b.DemolishCompletedSeconds += dt * coopSpeed;
				if (b.DemolishCompletedSeconds >= b.DemolishRequiredSeconds)
					CompleteBuildingDemolish(b);
				break;
			}
			case FacilityFieldTaskKind.Relocation:
			{
				var reloc = b.ActiveRelocation;
				if (reloc == null) { b.ActiveFieldTask = FacilityFieldTaskKind.None; b.FieldWorkActive = false; break; }

				if (b.ReinstallBodyPlaced)
				{
					// 재설치 중: 시설 몸체가 현장을 점유하고 피해를 받는다. 작업자가 모두 떠나도 현장·HP·진행·지불은 남는다(건설 명세 2-7).
					b.FieldWorkActive = false;
					if (reloc.TickReinstallAtDestination(dt, workerSpeeds, true))
						CompleteReinstall(b);
					break;
				}

				// 분해도 살아 있는 내부 작업자가 먼저 나온다 - 배정은 유지해 원래 자리 밖에서 기다린다(건설 명세 2-5·2-6).
				if (workerSpeeds.Count > 0 && BlockUntilInsideWorkersExit(b, InternalWorkRules.ExitCause.Dismantle)) break;
				bool wasActive = b.FieldWorkActive;
				bool dismantled = reloc.TickDismantle(dt, workerSpeeds, coreActive, 1.0, resourceManager?.LifeEnergyAccount);
				b.FieldWorkActive = !dismantled && workerSpeeds.Count > 0; // 실제 분해 착수부터 가동 중지(건설 명세 2-6-표1)
				if (b.FieldWorkActive && !wasActive) OnDismantleStarted(b);
				if (dismantled) OnDismantleFinished(b);
				break;
			}
		}
	}

	// 분해 착수: 정제시설은 신규 반입을 멈추고 입고 예약을 푼다 - 줍기 전 명령은 종료하고 이미 든 시체는 다른 저장시설을 찾는다(건설 명세 2-8).
	private void OnDismantleStarted(BuildingData b)
	{
		if (b.Kind != FacilityKind.CorpseRefinery) return;
		List<string> released = b.Refinery.ReleaseAllIncomingReservations();
		HandleCancelledRefineryReservations(b, released);
	}

	// 분해 완료: 기존 자리 점유를 풀고 1타일 분해 오브젝트로 내려놓은 뒤, 기존 담당자 중 도착 예상이 가장 빠른 유닛이 든다(건설 명세 2-6).
	private void OnDismantleFinished(BuildingData b)
	{
		List<Unit> helpers = CollectAssignedWorkers(b);
		UnregisterBuildingFromGrid(b, clearObstacle: true);
		_allBuildings.Remove(b);
		b.IsFormallyInstalled = false;
		b.FieldWorkActive = false;
		if (b.VisualObject != null) b.VisualObject.SetActive(false);
		EjectInsideWorkersAtFootprint(b, GameSession.Instance); // 착수 전에 모두 나왔어야 하지만 놓친 경우의 안전망
		ReleasePendingInsideCorpsesAtFootprint(b, GameSession.Instance); // 미배출 작업자 시체는 원래 시설 자리에 남는다(건설 명세 2-8-표1)
		InteractableObject pkg = DropDismantledBuildingPackage(b, b.Position, carrierCorpseOnSameTile: null);

		Unit carrier = pkg != null ? PickNearestUnit(helpers, new Vector2Int(pkg.Position.x, pkg.Position.y)) : null;
		if (carrier == null) return;
		b.ActiveRelocation.AssignedWorkerUnitId = carrier.GetInstanceID();
		carrier.pendingDismantledPackagePickup = pkg;
		IssueInternalEconomyMove(carrier, new Vector2Int(pkg.Position.x, pkg.Position.y));
	}

	private void ArbitrateRoomUnitSpawns(Room room, GameSession session)
	{
		var handles = new List<RoomSpawnFacilityHandle>();
		var byId = new Dictionary<int, BuildingData>();

		for (int i = 0; i < _allBuildings.Count; i++)
		{
			BuildingData b = _allBuildings[i];
			if (b == null || b.Kind != FacilityKind.UnitProduction || b.Position.z != room.Floor) continue;
			if (!session.roomGrid.TryGetValue(b.Position, out Room br) || br != room) continue;

			handles.Add(new RoomSpawnFacilityHandle
			{
				FacilityId = b.FacilityId,
				FacilityOrigin = b.Origin2D,
				FacilityFootprint = b.Footprint,
				IsFacilityOperational = b.IsOperational,
				Queue = b.ProductionQueue,
				UseFrontZone = IsFrontZoneUsable(b),
				FrontZone = GetFrontZone(b),
			});
			byId[b.FacilityId] = b;
		}

		if (handles.Count == 0) return;

		int incomingPop = CountConfirmedIncomingPlayerPop(room, session);
		var popCtx = new RoomPopulationContext
		{
			LivingPlayerPopulation = room.CurrentPopulation,
			ConfirmedIncomingMovePopulation = incomingPop,
			MaxPopulation = room.MaxPopulation,
		};

		SpawnArbitrator.ResolveRoomSpawns(
			handles,
			popCtx,
			(candOrigin, unitFp) => IsSpawnAreaStrictlyClear(candOrigin, unitFp, room.Floor, session),
			spawned =>
			{
				if (byId.TryGetValue(spawned.FacilityId, out BuildingData fac))
					SpawnUnitAtExactOrigin(fac, spawned.Spec, spawned.SpawnOrigin, IsFrontZoneUsable(fac) ? (RectInt?)GetFrontZone(fac) : null);
			});
	}

	private int CountConfirmedIncomingPlayerPop(Room targetRoom, GameSession session)
	{
		int incoming = 0;
		foreach (var u in session.units)
		{
			if (u == null || u.hp <= 0f || !u.IsPlayerMonsterFaction || u.currentFloor != targetRoom.Floor) continue;
			if (u.currentRoom == targetRoom) continue;
			if (!u.isManualMoveCommand || !u.playerMoveTarget.HasValue) continue;
			Vector3Int destKey = new Vector3Int(u.playerMoveTarget.Value.x, u.playerMoveTarget.Value.y, u.currentFloor);
			if (session.roomGrid.TryGetValue(destKey, out Room destRoom) && destRoom == targetRoom)
				incoming += u.populationCost;
		}
		return incoming;
	}

	// 명세서 v0.2 10-표3 ②: 시설 외곽 상하좌우 인접 공간에 벽·시설·시체·다른 유닛과 겹침 없이 몸 전체가 들어가야 한다.
	private bool IsSpawnAreaStrictlyClear(Vector2Int origin, Vector2Int footprint, int floor, GameSession session)
	{
		if (unitGenerate == null || !unitGenerate.IsAreaClear(origin, new Vector2(footprint.x, footprint.y), floor))
			return false;

		for (int dx = 0; dx < footprint.x; dx++)
		{
			for (int dy = 0; dy < footprint.y; dy++)
			{
				Vector3Int tile = new Vector3Int(origin.x + dx, origin.y + dy, floor);
				if (buildingGrid.ContainsKey(tile)) return false;
				if (session.unitGrid.ContainsKey(tile)) return false;
				if (session.objectGrid.TryGetValue(tile, out InteractableObject obj) && obj != null && !obj.IsCollected)
				{
					// 시체·분해 오브젝트·코어·문·목책 등 점유 대상과 겹칠 수 없음(10-표3)
					return false;
				}
			}
		}
		return true;
	}

	private void SpawnUnitAtExactOrigin(BuildingData b, UnitProductionSpec spec, Vector2Int spawnOrigin, RectInt? leaveZone = null)
	{
		GameSession gameSession = GameSession.Instance;
		if (unitGenerate == null || gameSession == null || spec == null) return;

		UnitType t = null;
		if (spec.TargetUnitTypeName == "MeleeTank") t = new MeleeTank();
		if (t == null)
		{
			LogHelper.Warning(LogHelper.GAME, $"알 수 없는 유닛 타입: {spec.TargetUnitTypeName}");
			return;
		}

		Monster m = unitGenerate.GenerateUnitAtPos<Monster>(t, spawnOrigin, b.Position.z);
		m.populationCost = spec.PopulationCost;
		m.MovementAlgorithm = new RoomConfinedMovement();

		// 완료된 종류별 업그레이드 연구 즉시 반영(10-1-표1, 15-표4)
		resourceManager?.ResearchLedger.ApplyCompletedSpeciesUpgradesToUnit(m);

		gameSession.units.Add(m);
		gameSession.RegisterUnitPos(m, m.position);
		if (leaveZone.HasValue) LeaveFrontZoneAfterSpawn(m, leaveZone.Value); // 출현 직후 정면 구역 밖으로 걸어 나간다(건설 명세 2-10-표1)
		LogHelper.Log(LogHelper.GAME, $"[유닛 출현] {spec.DisplayName} 출현 완료: ({spawnOrigin.x},{spawnOrigin.y},F{b.Position.z})");
	}

	private void ApplyCompletedResearchToExistingUnits(ResearchDefinition completedDef)
	{
		if (completedDef == null || GameSession.Instance?.units == null || resourceManager == null) return;
		foreach (var u in GameSession.Instance.units)
		{
			if (u != null && u.hp > 0f && u.IsPlayerMonsterFaction)
				resourceManager.ResearchLedger.ApplyCompletedSpeciesUpgradesToUnit(u);
		}
		NoticeCenter.Instance?.PushMomentary($"연구 완료: {completedDef.DisplayName}", NoticeCenter.QuestCompleteColor);
	}

	private void TickCorpseCarriers(GameSession session, float dt)
	{
		var singleWorkerBuf = new List<float>(1);
		for (int i = 0; i < session.units.Count; i++)
		{
			Unit u = session.units[i];
			if (u == null || u.hp <= 0f || !u.IsPlayerMonsterFaction) continue;

			// 1. 시체 회수 위치로 이동 중
			if (u.pendingCorpsePickupTarget != null)
			{
				InteractableObject targetObj = u.pendingCorpsePickupTarget;
				CorpseRecord corpse = targetObj.CorpseEconomyData;

				if (targetObj.IsCollected || corpse == null || !corpse.CanBeRefined || u.targetRefineryForCarry == null || !_allBuildings.Contains(u.targetRefineryForCarry))
				{
					if (corpse != null) u.targetRefineryForCarry?.Refinery.CancelIncomingReservation(corpse.CorpseId);
					u.pendingCorpsePickupTarget = null;
					u.targetRefineryForCarry = null;
					continue;
				}

				if (targetObj.IsAdjacentTo(u.position, 1) || (u.position.x == targetObj.Position.x && u.position.y == targetObj.Position.y))
				{
					ConsumeSingleObjectOrCoexisting(session, targetObj);
					u.pendingCorpsePickupTarget = null;
					u.carriedCorpse = corpse;

					BuildingData refB = u.targetRefineryForCarry;
					if (TryPickNearestAdjacentWorkTile(new Vector2Int(refB.Position.x, refB.Position.y), refB.Footprint, u, out Vector2Int refSlot))
						IssueInternalEconomyMove(u, refSlot);
				}
			}
			// 2. 시체를 들고 통합 정제시설로 운반 중
			else if (u.carriedCorpse != null)
			{
				BuildingData refB = u.targetRefineryForCarry;
				if (refB == null || !_allBuildings.Contains(refB) || refB.Refinery.IsDraining)
				{
					BuildingData alt = FindNearestValidRefinery(u.currentFloor, u.position, u.carriedCorpse, exclude: refB);
					if (alt != null && alt.Refinery.TryReserveIncomingSlot(u.carriedCorpse, alt.IsFormallyInstalled))
					{
						u.targetRefineryForCarry = alt;
						if (TryPickNearestAdjacentWorkTile(new Vector2Int(alt.Position.x, alt.Position.y), alt.Footprint, u, out Vector2Int altSlot))
							IssueInternalEconomyMove(u, altSlot);
					}
					else
					{
						DropCorpseToField(u.carriedCorpse, new Vector3Int(u.position.x, u.position.y, u.currentFloor), livingCarrier: u, carrierCorpseOnSameTile: null);
						u.carriedCorpse = null;
						u.targetRefineryForCarry = null;
					}
					continue;
				}

				Vector2Int refOrigin = new Vector2Int(refB.Position.x, refB.Position.y);
				if (WorkSpeedMath.IsOrthogonallyAdjacent(refOrigin, refB.Footprint, u.position, u.FootprintSize))
				{
					if (refB.Refinery.CompleteIntake(u.carriedCorpse, refB.IsFormallyInstalled))
					{
						u.carriedCorpse = null;
						u.targetRefineryForCarry = null;
						u.suppressEconomyCommandHook = true;
						u.ClearPlayerCommand();
						u.suppressEconomyCommandHook = false;
						DefensePlacementSystem.Instance?.OnWorkTaskEnded(u); // 시체 정제 운반이 끝났다 — 저장 방어 위치로 복귀(배치모드 1-6 표2)
					}
				}
			}
			// 3. 1타일 분해 시설 회수 위치로 이동 중(20-3항, 건설 명세 2-6 - 바닥에서 다시 줍기는 접근 뒤 임시 1초)
			else if (u.pendingDismantledPackagePickup != null)
			{
				InteractableObject pkgObj = u.pendingDismantledPackagePickup;
				BuildingData payload = pkgObj.DismantledBuildingPayload;
				if (pkgObj.IsCollected || payload?.ActiveRelocation == null)
				{
					u.pendingDismantledPackagePickup = null;
					u.pickupHoldStartedAt = null;
					continue;
				}

				if (pkgObj.IsAdjacentTo(u.position, 1) || (u.position.x == pkgObj.Position.x && u.position.y == pkgObj.Position.y))
				{
					double nowT = session.ElapsedGameSeconds;
					if (!u.pickupHoldStartedAt.HasValue)
					{
						u.pickupHoldStartedAt = nowT;
						continue;
					}
					if (nowT - u.pickupHoldStartedAt.Value < PackagePickupHoldSeconds) continue;
					u.pickupHoldStartedAt = null;

					ConsumeSingleObjectOrCoexisting(session, pkgObj);
					u.pendingDismantledPackagePickup = null;

					Vector3Int dest = payload.ActiveRelocation.TargetDestination;
					Vector2Int destFp = ConstructionRules.RotatedFootprint(payload.Footprint, payload.PendingRotation - payload.Rotation);
					bool destOk = CanInstallAt(dest, destFp, requireOwnership: true, kind: payload.Kind, rotation: payload.PendingRotation);
					if (!payload.ActiveRelocation.PickUpDroppedPackage(u.GetInstanceID(), dest, destOk))
					{
						DropDismantledBuildingPackage(payload, new Vector3Int(u.position.x, u.position.y, u.currentFloor), carrierCorpseOnSameTile: null);
						NoticeCenter.Instance?.PushMomentary("재설치 목적지를 사용할 수 없어 분해된 시설을 내려놓았습니다.", NoticeCenter.WarningColor);
						continue;
					}

					u.carriedDismantledBuilding = payload;
					if (TryPickNearestAdjacentWorkTile(new Vector2Int(dest.x, dest.y), destFp, u, out Vector2Int destSlot))
						IssueInternalEconomyMove(u, destSlot);
				}
			}
			// 4. 분해 시설을 목적지로 운반해 재설치 몸체를 세운다(20-3항, 건설 명세 2-7). 이후 재설치는 배정된 작업자 전원(최대 4명)이 TickBuildingFieldTask에서 진행한다.
			else if (u.carriedDismantledBuilding != null)
			{
				BuildingData payload = u.carriedDismantledBuilding;
				RelocationSession reloc = payload.ActiveRelocation;
				if (reloc == null)
					continue;

				Vector3Int dest = reloc.TargetDestination;
				Vector2Int destFp = ConstructionRules.RotatedFootprint(payload.Footprint, payload.PendingRotation - payload.Rotation);
				Vector2Int destOrigin = new Vector2Int(dest.x, dest.y);
				if (!WorkSpeedMath.IsOrthogonallyAdjacent(destOrigin, destFp, u.position, u.FootprintSize))
					continue;

				bool destValid = CanInstallAt(dest, destFp, requireOwnership: true, kind: payload.Kind, rotation: payload.PendingRotation);
				if (!destValid)
				{
					reloc.OnCurrentDestinationBecameInvalid();
					DropDismantledBuildingPackage(payload, new Vector3Int(u.position.x, u.position.y, u.currentFloor), carrierCorpseOnSameTile: null);
					u.carriedDismantledBuilding = null;
					NoticeCenter.Instance?.PushMomentary("재설치 목적지를 사용할 수 없어 분해된 시설을 내려놓았습니다.", NoticeCenter.WarningColor);
					continue;
				}

				// 일시적인 아군 점유는 실패가 아니라 대기다(건설 명세 2-7-표2).
				if (!IsFootprintClearOfUnits(dest, destFp))
					continue;

				PlaceReinstallBody(payload, dest);
				u.carriedDismantledBuilding = null;
				u.assignedWorkBuilding = payload; // 운반자도 이제 재설치 작업자다
				u.assignedWorkIsInternal = false;
			}
		}
	}

	private const double PackagePickupHoldSeconds = 1.0; // 보정 없는 1명 기준 바닥 회수 접근 뒤 임시 1초(건설 명세 2-6)

	private static void ConsumeSingleObjectOrCoexisting(GameSession session, InteractableObject targetObj)
	{
		if (session == null || targetObj == null) return;
		// 겹쳐 놓인 대상(대표가 아닌 쪽)을 회수한다 - 대표 오브젝트는 그대로 두고 겹침 목록에서만 뺀다(건설 명세 2-8-표2).
		if (session.objectGrid.TryGetValue(targetObj.Position, out InteractableObject head) && head != targetObj
			&& head.CoexistingObjects != null && head.CoexistingObjects.Remove(targetObj))
		{
			targetObj.IsCollected = true;
			if (head.CoexistingObjects.Count == 0) head.CoexistingObjects = null;
			return;
		}
		if (session.objectGrid.TryGetValue(targetObj.Position, out InteractableObject primary) && primary == targetObj)
		{
			if (targetObj.CoexistingObjects != null && targetObj.CoexistingObjects.Count > 0)
			{
				// 같은 타일에 보존된 다른 시체/분해 오브젝트가 있으면 한 구 회수 후에도 나머지가 점유를 이어받는다(12-표2, 24-표1).
				InteractableObject nextPrimary = targetObj.CoexistingObjects[0];
				targetObj.CoexistingObjects.RemoveAt(0);
				nextPrimary.CoexistingObjects = targetObj.CoexistingObjects;
				targetObj.CoexistingObjects = null;
				session.CollectObject(targetObj.Position);
				session.SpawnObject(nextPrimary, Color.gray);
			}
			else
			{
				session.CollectObject(targetObj.Position);
			}
		}
	}

	// 외부 현장 작업자(건설·수리·강화·철거·이동) - 시설 상하좌우에 붙은 인원이다. 내부 작업자는 GatherInsideWorkers가 모은다.
	private void GatherActiveAdjacentWorkers(BuildingData b, List<Unit> allUnits, List<float> outSpeeds)
	{
		outSpeeds.Clear();
		if (allUnits == null) return;

		Vector2Int bOrigin = b.Origin2D;
		for (int i = 0; i < allUnits.Count; i++)
		{
			Unit u = allUnits[i];
			if (u == null || u.hp <= 0f || !u.IsPlayerMonsterFaction || u.currentFloor != b.Position.z) continue;
			if (u.assignedWorkBuilding != b || u.assignedWorkIsInternal || u.IsInsideFacility) continue;
			if (u.StatusEffects.State.stunDuration > 0f || u.IsInEvolutionPresentation) continue;
			if (!WorkSpeedMath.IsOrthogonallyAdjacent(bOrigin, b.Footprint, u.position, u.FootprintSize)) continue;

			outSpeeds.Add(Mathf.Max(0.1f, u.workerSpeedMultiplier));
		}
	}

	// 시설 안에서 일하는 내부 작업자의 가동 기여 - 안에 들어가 있는 동안만 생명 에너지·연구 속도에 더해진다(건설 명세 1-6).
	private void GatherInsideWorkers(BuildingData b, List<float> outSpeeds, List<WorkerContributionInput> outLifeWorkers)
	{
		outSpeeds.Clear();
		outLifeWorkers.Clear();
		if (b.InsideWorkers.Count == 0) return;
		foreach (Unit u in b.InsideWorkers)
		{
			if (u == null || u.hp <= 0f || u.insideFacility != b || u.insideExitRequested) continue; // 나가는 중인 인원은 더 일하지 않는다
			if (u.StatusEffects.State.stunDuration > 0f) continue;
			float spd = Mathf.Max(0.1f, u.workerSpeedMultiplier);
			outSpeeds.Add(spd);
			outLifeWorkers.Add(new WorkerContributionInput(u.GetInstanceID(), spd, u.lifeEnergyOutputBonusRatio));
		}
	}

	// 착공 조건(건설 명세 2-1-표2): 설치 몸체 안에 유닛과 떨어진 시체·분해물이 없어야 한다 - 대기 아군은 비켜 주고 시체·분해물은 가까운 빈 곳으로 반출한다.
	private bool IsConstructionFootprintClearOfUnits(BuildingData b)
	{
		GameSession session = GameSession.Instance;
		if (session == null) return true;
		foreach (Vector3Int tile in FootprintTiles(b.Position, b.Footprint))
		{
			if (session.unitGrid.ContainsKey(tile)) return false;
			if (session.objectGrid.TryGetValue(tile, out InteractableObject o) && IsLooseCorpseOrPackage(o)) return false;
		}
		return true;
	}

	private bool TryPickNearestAdjacentWorkTile(Vector2Int targetOrigin, Vector2Int targetSize, Unit worker, out Vector2Int bestSlot)
	{
		bestSlot = worker.position;
		GameSession session = GameSession.Instance;
		var candidates = WorkSpeedMath.GetOrthogonalAdjacentOrigins(targetOrigin, targetSize, worker.FootprintSize);
		float bestDist = float.MaxValue;
		bool found = false;

		for (int i = 0; i < candidates.Count; i++)
		{
			Vector2Int c = candidates[i];
			bool clear = true;
			for (int dx = 0; dx < worker.FootprintSize.x && clear; dx++)
			{
				for (int dy = 0; dy < worker.FootprintSize.y && clear; dy++)
				{
					Vector3Int tile = new Vector3Int(c.x + dx, c.y + dy, worker.currentFloor);
					if (buildingGrid.ContainsKey(tile)) { clear = false; break; }
					if (createMap != null && !createMap.IsStaticTileWalkable(tile.z, new Vector2Int(tile.x, tile.y))) { clear = false; break; }
					if (session != null && session.unitGrid.TryGetValue(tile, out Unit occ) && occ != null && occ != worker && occ.hp > 0f)
					{
						clear = false;
						break;
					}
				}
			}
			if (!clear) continue;

			float d = Vector2Int.Distance(worker.position, c);
			if (d < bestDist)
			{
				bestDist = d;
				bestSlot = c;
				found = true;
			}
		}
		return found;
	}

	// workPriority=true(재지시)면 전투/경계가 끼어들 수 없는 고착 이동 + 도착 시 정지, 아니면 전투/경계에 밀렸다가 끝난 뒤 이어지는 자동 이동이다(건설 명세 1-5).
	private static void IssueInternalEconomyMove(Unit worker, Vector2Int dest, bool workPriority = false)
	{
		worker.suppressEconomyCommandHook = true;
		try
		{
			worker.SetMoveCommand(dest, markHalt: workPriority, markStandGround: false);
			worker.isDefenseAutoReturn = !workPriority && (AIConfigLoader.Behavior?.defenseReturnYieldsToCombatEnabled ?? true);
		}
		finally
		{
			worker.suppressEconomyCommandHook = false;
		}
	}

	private static void AccumulateCosts(Dictionary<ResourceType, int> target, Dictionary<ResourceType, int> delta)
	{
		if (target == null || delta == null) return;
		foreach (var kv in delta)
			target[kv.Key] = target.TryGetValue(kv.Key, out int prev) ? prev + kv.Value : kv.Value;
	}

	private GameObject CreateBuildingVisual(Vector3Int pos, Vector2Int footprint, Sprite buildingSprite, string objectName, Color tint)
	{
		GameObject visual = new GameObject($"Building_{objectName}_{pos.x}_{pos.y}_{pos.z}");
		SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
		sr.sprite = buildingSprite;
		sr.color = tint;
		sr.sortingOrder = 6;

		Vector3 floorOffset = unitGenerate != null ? unitGenerate.GetFloorOffset(pos.z) : Vector3.zero;
		visual.transform.position = new Vector3(pos.x + footprint.x / 2f, pos.y + footprint.y / 2f, 0f) + floorOffset;

		Vector2 spriteWorldSize = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
		float scaleX = spriteWorldSize.x > 0f ? footprint.x / spriteWorldSize.x : footprint.x;
		float scaleY = spriteWorldSize.y > 0f ? footprint.y / spriteWorldSize.y : footprint.y;
		visual.transform.localScale = new Vector3(scaleX, scaleY, 1f);

		Transform buildingGroup = GameSession.Instance?.GetFloorCategoryGroup(pos.z, "Buildings");
		if (buildingGroup != null) visual.transform.SetParent(buildingGroup, true);

		return visual;
	}

	private void UpdateMapDataObstacle(Vector3Int pos, Vector2Int footprint, bool isObstacle)
	{
		foreach (Vector3Int tile in FootprintTiles(pos, footprint))
			UpdateMapDataObstacleSingleTile(tile, isObstacle);
	}

	private void UpdateMapDataObstacleSingleTile(Vector3Int pos, bool isObstacle)
	{
		if (pos.x < 0 || pos.y < 0 || pos.z < 0) return;
		if (createMap == null || createMap.map.floors == null || pos.z >= createMap.map.floors.Length) return;

		Floor floor = createMap.map.floors[pos.z];
		int cs = floor.config.chunkSize;
		int cx = pos.x / cs, cy = pos.y / cs;
		int tx = pos.x % cs, ty = pos.y % cs;

		if (cx >= 0 && cx < floor.config.width && cy >= 0 && cy < floor.config.height)
		{
			var chunk = floor.chunks[cx, cy];
			if (chunk.chunk != null)
				chunk.chunk[tx, ty].isStructureExist = isObstacle;
		}

		int mapValue = isObstacle ? 2 : 1;
		if (Unit.humanFactionData?.discoveredMap != null && pos.z < Unit.humanFactionData.discoveredMap.Length)
		{
			var floorMap = Unit.humanFactionData.discoveredMap[pos.z];
			if (floorMap != null && pos.x < floorMap.GetLength(0) && pos.y < floorMap.GetLength(1))
				floorMap[pos.x, pos.y] = mapValue;
		}
		if (Unit.monsterFactionData?.discoveredMap != null && pos.z < Unit.monsterFactionData.discoveredMap.Length)
		{
			var floorMap = Unit.monsterFactionData.discoveredMap[pos.z];
			if (floorMap != null && pos.x < floorMap.GetLength(0) && pos.y < floorMap.GetLength(1))
				floorMap[pos.x, pos.y] = mapValue;
		}
	}
}
