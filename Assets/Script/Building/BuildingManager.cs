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
	public GameObject VisualObject;
	public Sprite SpriteAsset;

	public float CurrentHp = 300f;
	public float MaxHp = 300f;
	public int UpgradeTier;
	public bool IsFormallyInstalled = true;

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

	// 정식 설치 + 손상 없음(최대 HP) + 현장 작업 없음 + 담당 코어 정상이어야 일반 가동 가능(7-표1, 10-표1, 13-1-표1, 16-표2)
	public bool IsOperational =>
		IsFormallyInstalled
		&& !IsDummy
		&& CurrentHp > 0f
		&& CurrentHp >= MaxHp - 1e-3f
		&& ActiveFieldTask == FacilityFieldTaskKind.None
		&& BuildingManager.AreCoreFunctionsActive(Position.z);
}

public enum BuildBlock
{
	None,
	CoreDestroyed,
	RoomNotSecured,
	Terrain,
	SoulUnlockRequired,
}

public class BuildingManager : NativeRoutine
{
	public static BuildingManager Instance { get; private set; }

	public const float DefaultCycleIntervalSeconds = 10f;
	public const double DefaultCycleBasePayout = 50.0;
	public const float DefaultFacilityMaxHp = 300f;
	public const double DefaultBuildWorkSeconds = 8.0;
	public const double DefaultUpgradeWorkSeconds = 12.0;
	public const double DefaultFullRepairSeconds = 10.0;
	public const double DefaultDismantleSeconds = 6.0;
	public const double DefaultReinstallSeconds = 6.0;
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

	public bool CanInstallAt(Vector3Int pos, Vector2Int footprint, bool requireOwnership = true)
		=> CheckInstallAt(pos, footprint, requireOwnership) == BuildBlock.None;

	public BuildBlock CheckInstallAt(Vector3Int pos, Vector2Int footprint, bool requireOwnership = true, FacilityKind kind = FacilityKind.UnitProduction)
	{
		if (requireOwnership && kind == FacilityKind.Research && resourceManager != null && !resourceManager.IsSoulShardSpendUnlocked)
			return BuildBlock.SoulUnlockRequired;

		foreach (Vector3Int tile in FootprintTiles(pos, footprint))
		{
			BuildBlock block = CheckInstallSingleTile(tile, requireOwnership);
			if (block != BuildBlock.None) return block;
		}
		return BuildBlock.None;
	}

	public static string DescribeBlock(BuildBlock block) => block switch
	{
		BuildBlock.CoreDestroyed => "코어가 파괴되어 건설할 수 없습니다. 먼저 코어를 수복하세요.",
		BuildBlock.RoomNotSecured => "확보하지 않은 방에서는 건설할 수 없습니다. 방의 적을 모두 처치해 확보하세요.",
		BuildBlock.Terrain => "장애물이 있거나 설치할 수 없는 지형입니다.",
		BuildBlock.SoulUnlockRequired => "최초 코어 완전 활성화 이후에 연구시설을 건설할 수 있습니다.",
		_ => string.Empty,
	};

	private BuildBlock CheckInstallSingleTile(Vector3Int pos, bool requireOwnership)
	{
		if (pos.x < 0 || pos.y < 0 || pos.z < 0) return BuildBlock.Terrain;
		if (buildingGrid.ContainsKey(pos)) return BuildBlock.Terrain;
		if (GameSession.Instance != null && GameSession.Instance.IsRepairableDoorTile(pos)) return BuildBlock.Terrain;
		if (GameSession.Instance != null && GameSession.Instance.IsFenceTile(pos)) return BuildBlock.Terrain;
		if (requireOwnership && createMap != null && !createMap.IsPositionPlayerOwned(pos.z, new Vector2Int(pos.x, pos.y))) return BuildBlock.RoomNotSecured;
		if (requireOwnership && !AreCoreFunctionsActive(pos.z)) return BuildBlock.CoreDestroyed;

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
						if (tile.name == "Wall" || tile.isStructureExist) return BuildBlock.Terrain;
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
		if (!CanInstallAt(pos, fp, requireOwnership: true))
		{
			LogHelper.Warning(LogHelper.GAME, $"시설 설치 불가: {pos} ({kind})");
			return false;
		}

		string label = GetDisplayNameForKind(kind);
		Color tint = GetTintForKind(kind, isBlueprint: false);
		GameObject visual = CreateBuildingVisual(pos, fp, sprite, label, tint);
		BuildingData b = CreateBuildingInstance(kind, pos, fp, availableRules, sprite, label, originKind, visual);
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
		out string failureMessage)
	{
		orderedBuilding = null;
		failureMessage = string.Empty;

		Vector2Int fp = GetFootprintForKind(kind);
		BuildBlock block = CheckInstallAt(pos, fp, requireOwnership: true, kind: kind);
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
			isCoreActive: AreCoreFunctionsActive(pos.z),
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
		b.ActiveFieldTask = FacilityFieldTaskKind.Construction;
		b.ActiveConstructionOrUpgrade = session;

		// 착공 전까지 다른 건설 예약과는 겹치지 않으나 유닛 통과는 가능하다(16-표2)
		RegisterBuildingToGrid(b, updateObstacle: false);

		orderedBuilding = b;
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
			AreCoreFunctionsActive(b.Position.z),
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
		if (b == null || !b.IsFormallyInstalled || b.ActiveFieldTask != FacilityFieldTaskKind.None || resourceManager == null)
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
			isCoreActive: AreCoreFunctionsActive(b.Position.z),
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
		if (!AreCoreFunctionsActive(b.Position.z))
		{
			NoticeCenter.Instance?.PushMomentary("담당 코어가 파괴되어 철거를 시작할 수 없습니다.", NoticeCenter.WarningColor);
			return false;
		}
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

	public bool TryStartFacilityRelocation(BuildingData b, Vector3Int destination)
	{
		if (b == null || !b.IsFormallyInstalled || b.ActiveFieldTask != FacilityFieldTaskKind.None || resourceManager == null)
			return false;

		bool refineryEmpty = b.Kind != FacilityKind.CorpseRefinery || b.Refinery.IsCompletelyEmpty;
		bool destValid = CanInstallAt(destination, b.Footprint, requireOwnership: true);

		if (!RelocationSession.TryCreateOrder(
			ResourceManager.FacilityRelocateDismantleLifeEnergyCost,
			DefaultDismantleSeconds,
			DefaultReinstallSeconds,
			b.Footprint,
			destination,
			AreCoreFunctionsActive(b.Position.z),
			refineryEmpty,
			destValid,
			resourceManager.LifeEnergyAccount,
			out RelocationSession reloc,
			out string failMsg))
		{
			if (!string.IsNullOrEmpty(failMsg))
				NoticeCenter.Instance?.PushMomentary(failMsg, NoticeCenter.WarningColor);
			return false;
		}

		b.ActiveFieldTask = FacilityFieldTaskKind.Relocation;
		b.ActiveRelocation = reloc;
		NoticeCenter.Instance?.PushMomentary($"{b.DisplayName} 이동용 분해가 등록되었습니다. 작업자를 배정하세요.", NoticeCenter.QuestCompleteColor);
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
		if (!AreCoreFunctionsActive(pkgObj.Position.z))
		{
			NoticeCenter.Instance?.PushMomentary("담당 코어가 파괴되어 분해된 시설을 운반·재설치할 수 없습니다.", NoticeCenter.WarningColor);
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
		bool survived = b.CurrentHp > 0f;

		// 이동용 분해 완료 전 피격: 생존 시 진행 삭제·전액 환급·원위치 유지, 파괴 시 환급 없음(20-2-표1)
		if (b.ActiveFieldTask == FacilityFieldTaskKind.Relocation && b.ActiveRelocation != null && !b.ActiveRelocation.IsDismantleFinished)
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
		RemoveBuildingCompletely(b);
		FinishSingleTaskWorkers(null, demolishWorkers);
	}

	// ── 단일 작업 종료와 자동 방어 복귀(배치모드 1-6 표2) ──

	private List<Unit> CollectAssignedWorkers(BuildingData b)
	{
		var workers = new List<Unit>();
		if (b == null || GameSession.Instance?.units == null) return workers;
		foreach (var u in GameSession.Instance.units)
			if (u != null && u.hp > 0f && u.assignedWorkBuilding == b) workers.Add(u);
		return workers;
	}

	// 단일 작업이 끝나 일손이 풀렸다 — 배정을 놓고 저장된 방어 위치로 복귀를 시도한다.
	private void FinishSingleTaskWorkers(BuildingData b, List<Unit> workers)
	{
		foreach (var w in workers)
		{
			if (b != null && w.assignedWorkBuilding == b) w.assignedWorkBuilding = null;
			DefensePlacementSystem.Instance?.OnWorkTaskEnded(w);
		}
	}

	private void RemoveBuildingCompletely(BuildingData b)
	{
		UnregisterBuildingFromGrid(b, clearObstacle: b.IsFormallyInstalled || (b.ActiveConstructionOrUpgrade != null && b.ActiveConstructionOrUpgrade.HasStartedWork));
		_allBuildings.Remove(b);

		if (GameSession.Instance?.units != null)
		{
			foreach (var u in GameSession.Instance.units)
			{
				if (u != null && u.assignedWorkBuilding == b)
					u.assignedWorkBuilding = null;
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
			if (u != null && u.hp > 0f && u.assignedWorkBuilding == b) count++;
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

		if (worker.assignedWorkBuilding != b && CountAssignedWorkers(b) >= WorkSpeedMath.MaxWorkersPerTask)
		{
			NoticeCenter.Instance?.PushMomentary("해당 작업의 최대 투입 인원(4기)이 모두 찼습니다.", NoticeCenter.WarningColor);
			return false;
		}

		NotifyWorkerCommandChanged(worker);
		worker.assignedWorkBuilding = b;
		if (b.ActiveFieldTask == FacilityFieldTaskKind.Relocation && b.ActiveRelocation != null)
			b.ActiveRelocation.AssignedWorkerUnitId = worker.GetInstanceID();

		Vector2Int bOrigin = new Vector2Int(b.Position.x, b.Position.y);
		if (!WorkSpeedMath.IsOrthogonallyAdjacent(bOrigin, b.Footprint, worker.position, worker.FootprintSize))
		{
			if (TryPickNearestAdjacentWorkTile(bOrigin, b.Footprint, worker, out Vector2Int slot))
				IssueInternalEconomyMove(worker, slot);
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

	private BuildingData FindNearestValidRefinery(int floor, Vector2Int fromPos, CorpseRecord corpse, BuildingData exclude = null)
	{
		BuildingData best = null;
		float bestDist = float.MaxValue;
		for (int i = 0; i < _allBuildings.Count; i++)
		{
			BuildingData b = _allBuildings[i];
			if (b == null || b == exclude || b.Kind != FacilityKind.CorpseRefinery || b.Position.z != floor)
				continue;
			if (!b.Refinery.CanAcceptIncoming(corpse, b.IsFormallyInstalled))
				continue;
			float d = Vector2Int.Distance(fromPos, new Vector2Int(b.Position.x, b.Position.y));
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
			if (b.ActiveFieldTask == FacilityFieldTaskKind.Relocation && b.ActiveRelocation != null)
			{
				// [이동 적용]으로 분해 작업자 이탈 시 분해 취소·환급 없이 진행·지불 보존(20-2-표1)
				b.ActiveRelocation.OnWorkerMovedAwayDuringDismantle();
			}
			u.assignedWorkBuilding = null;
		}

		u.assignedCoreRestorePos = null;

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
	}

	// 작업자 또는 운반자가 피격됐을 때의 처리(5-표4, 12-표2, 20-2-표1, 20-3항)
	public void NotifyUnitHit(Unit u)
	{
		if (u == null || !u.IsPlayerMonsterFaction) return;

		if (u.assignedWorkBuilding != null)
		{
			BuildingData b = u.assignedWorkBuilding;
			u.assignedWorkBuilding = null;

			// 이동용 분해 중 작업자 피격은 일반 작업과 달리 분해 자체를 취소하고 전액 환급한다(20-2-표1, 24-표1).
			if (b.ActiveFieldTask == FacilityFieldTaskKind.Relocation && b.ActiveRelocation != null && !b.ActiveRelocation.IsDismantleFinished)
			{
				b.ActiveRelocation.OnHitBeforeDismantleComplete(b.CurrentHp > 0f, resourceManager?.LifeEnergyAccount);
				b.ActiveRelocation = null;
				b.ActiveFieldTask = FacilityFieldTaskKind.None;
			}
		}

		u.assignedCoreRestorePos = null;

		// 운반자 피격: 명령 취소·시체 내려놓기·전투 후 자동 재개 없음(12-표2, 24-표1)
		if (u.pendingCorpsePickupTarget != null || u.carriedCorpse != null)
		{
			if (u.pendingCorpsePickupTarget != null)
			{
				string cId = u.pendingCorpsePickupTarget.CorpseEconomyData?.CorpseId;
				u.targetRefineryForCarry?.Refinery.CancelIncomingReservation(cId);
				u.pendingCorpsePickupTarget = null;
			}
			if (u.carriedCorpse != null)
			{
				u.targetRefineryForCarry?.Refinery.CancelIncomingReservation(u.carriedCorpse.CorpseId);
				if (u.hp > 0f)
					DropCorpseToField(u.carriedCorpse, new Vector3Int(u.position.x, u.position.y, u.currentFloor), livingCarrier: u, carrierCorpseOnSameTile: null);
				u.carriedCorpse = null;
			}
			u.targetRefineryForCarry = null;
			u.suppressEconomyCommandHook = true;
			u.ClearPlayerCommand();
			u.suppressEconomyCommandHook = false;
		}

		if (u.carriedDismantledBuilding != null && u.hp > 0f)
		{
			DropDismantledBuildingPackage(u.carriedDismantledBuilding, new Vector3Int(u.position.x, u.position.y, u.currentFloor), carrierCorpseOnSameTile: null);
			u.carriedDismantledBuilding = null;
			u.suppressEconomyCommandHook = true;
			u.ClearPlayerCommand();
			u.suppressEconomyCommandHook = false;
		}
	}

	// 운반자 사망 시: 인접 빈 공간이 없으면 운반자의 시체와 운반 중이던 시체/1타일 분해 시설을 별도 대상으로 함께 보존한다(12-표2, 20-3-표1, 24-표1).
	public void NotifyUnitDied(Unit deadUnit, InteractableObject carrierCorpseObj)
	{
		if (deadUnit == null) return;

		deadUnit.assignedWorkBuilding = null;
		deadUnit.assignedCoreRestorePos = null;

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

	public void DropCorpseToField(CorpseRecord corpse, Vector3Int nearPos, Unit livingCarrier, InteractableObject carrierCorpseOnSameTile)
	{
		if (corpse == null || GameSession.Instance == null) return;
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
			return;
		}

		if (session.objectGrid.TryGetValue(dropPos, out var existing))
		{
			existing.CoexistingObjects ??= new List<InteractableObject>();
			existing.CoexistingObjects.Add(droppedObj);
			return;
		}

		session.SpawnObject(droppedObj, Color.gray);
	}

	public void DropDismantledBuildingPackage(BuildingData b, Vector3Int nearPos, InteractableObject carrierCorpseOnSameTile)
	{
		if (b == null || GameSession.Instance == null) return;
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
			return;
		}

		if (session.objectGrid.TryGetValue(dropPos, out var existing))
		{
			existing.CoexistingObjects ??= new List<InteractableObject>();
			existing.CoexistingObjects.Add(pkgObj);
			return;
		}

		session.SpawnObject(pkgObj, new Color(0.95f, 0.8f, 0.35f, 1f));
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

	public override void UpdateProcess()
	{
		float dt = Time.deltaTime;
		// InputManager 일시정지는 Time.timeScale = 0.0001f를 쓰므로 timeScale/isPaused를 함께 확인해 일시정지 중 진행을 0으로 차단한다(4-표2, 5-표5, 23-표1).
		if (dt <= 0f || Time.timeScale < 0.01f || (GameSession.Instance != null && GameSession.Instance.isPaused))
			return;

		GameSession session = GameSession.Instance;
		double now = session != null ? session.ElapsedGameSeconds : Time.time;

		// 1. 시체 및 분해 시설 운반자 진행
		if (session?.units != null)
			TickCorpseCarriers(session, dt);

		// 2. 코어 수복 작업 진행
		session?.Core?.TickActiveRestores(dt, session.units);

		// 3. 개별 시설 틱 (건설·업그레이드·수리·철거·분해·생명 에너지·자동 정제·연구·유닛 생산 진행)
		var buildingsSnapshot = new List<BuildingData>(_allBuildings);
		var workerSpeedBuffer = new List<float>(4);
		var lifeWorkerBuffer = new List<WorkerContributionInput>(4);
		var roomsWithReadyProduction = new HashSet<Room>();

		for (int i = 0; i < buildingsSnapshot.Count; i++)
		{
			BuildingData b = buildingsSnapshot[i];
			if (b == null || !_allBuildings.Contains(b) || b.IsDummy) continue;

			GatherActiveAdjacentWorkers(b, session?.units, workerSpeedBuffer, lifeWorkerBuffer);

			if (b.ActiveFieldTask != FacilityFieldTaskKind.None)
			{
				TickBuildingFieldTask(b, dt, workerSpeedBuffer);
				continue;
			}

			bool operational = b.IsOperational;

			if (b.Kind == FacilityKind.LifeEnergy && resourceManager != null)
			{
				double earned = b.LifeEnergyCycle.Advance(dt, lifeWorkerBuffer, operational);
				if (earned > 0.0)
					resourceManager.AddResource(ResourceType.LifeEnergy, earned, ResourceCreditSource.FacilityProduction);
			}
			else if (b.Kind == FacilityKind.CorpseRefinery && resourceManager != null)
			{
				b.Refinery.TickAutoRefine(dt, operational, 1.0, resourceManager.Accounts);
			}
			else if (b.Kind == FacilityKind.Research && resourceManager != null && b.ResearchQueue != null)
			{
				bool researchCompleted = b.ResearchQueue.TickResearch(
					dt,
					workerSpeedBuffer,
					operational,
					1.0,
					1.0,
					resourceManager.Accounts,
					facilityDestroyedOnSameTick: false,
					onCompleted: def => ApplyCompletedResearchToExistingUnits(def));
				// 연구 대기열 소진도 단일 작업 종료다.
				if (researchCompleted && b.ResearchQueue.Entries.Count == 0) FinishSingleTaskWorkers(b, CollectAssignedWorkers(b));
			}
			else if (b.Kind == FacilityKind.UnitProduction && resourceManager != null)
			{
				b.ProductionQueue.TickProduction(dt, now, operational, 1.0, 1.0, resourceManager.Accounts);
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
		bool coreActive = AreCoreFunctionsActive(b.Position.z);

		switch (b.ActiveFieldTask)
		{
			case FacilityFieldTaskKind.Construction:
			case FacilityFieldTaskKind.Upgrade:
			{
				var session = b.ActiveConstructionOrUpgrade;
				if (session == null) { b.ActiveFieldTask = FacilityFieldTaskKind.None; break; }

				bool siteClear = session.IsUpgrade || session.HasStartedWork || IsConstructionFootprintClearOfUnits(b);
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

				if (!prevStarted && session.HasStartedWork && !session.IsUpgrade)
				{
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
					if (!DoesBuildingAcceptWorkers(b)) FinishSingleTaskWorkers(b, CollectAssignedWorkers(b)); // 반복 작업 시설(생명 에너지·연구)의 작업자는 계속 일한다
				}
				break;
			}
			case FacilityFieldTaskKind.Repair:
			{
				if (b.ActiveRepair == null) { b.ActiveFieldTask = FacilityFieldTaskKind.None; break; }
				float curHp = b.CurrentHp;
				bool done = b.ActiveRepair.Tick(dt, workerSpeeds, coreActive, resourceManager?.LifeEnergyAccount, ref curHp);
				b.CurrentHp = curHp;
				if (done)
				{
					b.ActiveRepair = null;
					b.ActiveFieldTask = FacilityFieldTaskKind.None;
					NoticeCenter.Instance?.PushMomentary($"{b.DisplayName} 수리가 완료되었습니다.", NoticeCenter.QuestCompleteColor);
					if (!DoesBuildingAcceptWorkers(b)) FinishSingleTaskWorkers(b, CollectAssignedWorkers(b));
				}
				break;
			}
			case FacilityFieldTaskKind.Demolish:
			{
				float coopSpeed = WorkSpeedMath.CalculateTotalWorkSpeed(workerSpeeds);
				if (coopSpeed <= 0f) break;
				b.DemolishCompletedSeconds += dt * coopSpeed;
				if (b.DemolishCompletedSeconds >= b.DemolishRequiredSeconds)
					CompleteBuildingDemolish(b);
				break;
			}
			case FacilityFieldTaskKind.Relocation:
			{
				var reloc = b.ActiveRelocation;
				if (reloc == null) { b.ActiveFieldTask = FacilityFieldTaskKind.None; break; }
				bool dismantled = reloc.TickDismantle(dt, workerSpeeds, coreActive, 1.0, resourceManager?.LifeEnergyAccount);
				if (dismantled)
				{
					// 분해 완료: 기존 자리 점유 해제 후 1타일 분해 오브젝트로 전환(20-2·20-3항)
					UnregisterBuildingFromGrid(b, clearObstacle: true);
					_allBuildings.Remove(b);
					b.IsFormallyInstalled = false;
					if (b.VisualObject != null) b.VisualObject.SetActive(false);
					DropDismantledBuildingPackage(b, b.Position, carrierCorpseOnSameTile: null);
				}
				break;
			}
		}
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
				FacilityOrigin = new Vector2Int(b.Position.x, b.Position.y),
				FacilityFootprint = b.Footprint,
				IsFacilityOperational = b.IsOperational,
				Queue = b.ProductionQueue,
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
					SpawnUnitAtExactOrigin(fac, spawned.Spec, spawned.SpawnOrigin);
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

	private void SpawnUnitAtExactOrigin(BuildingData b, UnitProductionSpec spec, Vector2Int spawnOrigin)
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
			// 3. 1타일 분해 시설 회수 위치로 이동 중(20-3항)
			else if (u.pendingDismantledPackagePickup != null)
			{
				InteractableObject pkgObj = u.pendingDismantledPackagePickup;
				BuildingData payload = pkgObj.DismantledBuildingPayload;
				if (pkgObj.IsCollected || payload?.ActiveRelocation == null || !AreCoreFunctionsActive(u.currentFloor))
				{
					u.pendingDismantledPackagePickup = null;
					continue;
				}

				if (pkgObj.IsAdjacentTo(u.position, 1) || (u.position.x == pkgObj.Position.x && u.position.y == pkgObj.Position.y))
				{
					ConsumeSingleObjectOrCoexisting(session, pkgObj);
					u.pendingDismantledPackagePickup = null;

					Vector3Int dest = payload.ActiveRelocation.TargetDestination;
					bool destOk = CanInstallAt(dest, payload.Footprint, requireOwnership: true);
					if (!payload.ActiveRelocation.PickUpDroppedPackage(u.GetInstanceID(), dest, destOk))
					{
						DropDismantledBuildingPackage(payload, new Vector3Int(u.position.x, u.position.y, u.currentFloor), carrierCorpseOnSameTile: null);
						NoticeCenter.Instance?.PushMomentary("재설치 목적지를 사용할 수 없어 분해된 시설을 내려놓았습니다.", NoticeCenter.WarningColor);
						continue;
					}

					u.carriedDismantledBuilding = payload;
					if (TryPickNearestAdjacentWorkTile(new Vector2Int(dest.x, dest.y), payload.Footprint, u, out Vector2Int destSlot))
						IssueInternalEconomyMove(u, destSlot);
				}
			}
			// 4. 분해 시설을 목적지로 운반·재설치(0 비용) 중(20-3항)
			else if (u.carriedDismantledBuilding != null)
			{
				BuildingData payload = u.carriedDismantledBuilding;
				RelocationSession reloc = payload.ActiveRelocation;
				if (reloc == null || !AreCoreFunctionsActive(u.currentFloor))
					continue;

				Vector3Int dest = reloc.TargetDestination;
				Vector2Int destOrigin = new Vector2Int(dest.x, dest.y);
				if (!WorkSpeedMath.IsOrthogonallyAdjacent(destOrigin, payload.Footprint, u.position, u.FootprintSize))
					continue;

				bool destValid = CanInstallAt(dest, payload.Footprint, requireOwnership: true);
				if (!destValid)
				{
					reloc.OnCurrentDestinationBecameInvalid();
					DropDismantledBuildingPackage(payload, new Vector3Int(u.position.x, u.position.y, u.currentFloor), carrierCorpseOnSameTile: null);
					u.carriedDismantledBuilding = null;
					NoticeCenter.Instance?.PushMomentary("재설치 목적지를 사용할 수 없어 분해된 시설을 내려놓았습니다.", NoticeCenter.WarningColor);
					continue;
				}

				singleWorkerBuf.Clear();
				singleWorkerBuf.Add(Mathf.Max(0.1f, u.workerSpeedMultiplier));
				if (reloc.TickReinstallAtDestination(dt, singleWorkerBuf, destValid))
				{
					u.carriedDismantledBuilding = null;
					payload.Position = dest;
					payload.IsFormallyInstalled = true;
					payload.ActiveFieldTask = FacilityFieldTaskKind.None;
					payload.ActiveRelocation = null;
					if (payload.VisualObject != null)
					{
						Vector3 floorOffset = unitGenerate != null ? unitGenerate.GetFloorOffset(dest.z) : Vector3.zero;
						payload.VisualObject.transform.position = new Vector3(dest.x + payload.Footprint.x / 2f, dest.y + payload.Footprint.y / 2f, 0f) + floorOffset;
						payload.VisualObject.SetActive(true);
					}
					RegisterBuildingToGrid(payload, updateObstacle: true);
					NoticeCenter.Instance?.PushMomentary($"{payload.DisplayName} 재설치가 완료되었습니다.", NoticeCenter.QuestCompleteColor);
					DefensePlacementSystem.Instance?.OnWorkTaskEnded(u); // 분해 이동(운반·재설치)도 단일 작업 — 저장 방어 위치로 복귀(배치모드 1-6 표2)
				}
			}
		}
	}

	private static void ConsumeSingleObjectOrCoexisting(GameSession session, InteractableObject targetObj)
	{
		if (session == null || targetObj == null) return;
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

	private void GatherActiveAdjacentWorkers(
		BuildingData b,
		List<Unit> allUnits,
		List<float> outSpeeds,
		List<WorkerContributionInput> outLifeWorkers)
	{
		outSpeeds.Clear();
		outLifeWorkers.Clear();
		if (allUnits == null) return;

		Vector2Int bOrigin = new Vector2Int(b.Position.x, b.Position.y);
		for (int i = 0; i < allUnits.Count; i++)
		{
			Unit u = allUnits[i];
			if (u == null || u.hp <= 0f || !u.IsPlayerMonsterFaction || u.currentFloor != b.Position.z) continue;
			if (u.assignedWorkBuilding != b) continue;
			if (u.StatusEffects.State.stunDuration > 0f || u.IsInEvolutionPresentation) continue;
			if (!WorkSpeedMath.IsOrthogonallyAdjacent(bOrigin, b.Footprint, u.position, u.FootprintSize)) continue;

			float spd = Mathf.Max(0.1f, u.workerSpeedMultiplier);
			outSpeeds.Add(spd);
			outLifeWorkers.Add(new WorkerContributionInput(u.GetInstanceID(), spd, u.lifeEnergyOutputBonusRatio));
		}
	}

	private bool IsConstructionFootprintClearOfUnits(BuildingData b)
	{
		GameSession session = GameSession.Instance;
		if (session == null) return true;
		foreach (Vector3Int tile in FootprintTiles(b.Position, b.Footprint))
			if (session.unitGrid.ContainsKey(tile)) return false;
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

	private static void IssueInternalEconomyMove(Unit worker, Vector2Int dest)
	{
		worker.suppressEconomyCommandHook = true;
		try
		{
			worker.SetMoveCommand(dest, markHalt: false, markStandGround: false);
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
