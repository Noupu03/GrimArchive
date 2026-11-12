using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Haare.Util.Logger;
using UnityEngine;

public enum FacilityKind
{
	LifeEnergy,
	UnitProduction,
	CorpseRefinery,
	Research,
	Dummy,
}

public class ResourceManager : NativeRoutine
{
	public static ResourceManager Instance { get; private set; }

	// ── 프로토타입 자리표시자 수치 (21항 유닛·시설 통합 데이터 확정 전 검증용 초기값) ──
	public const int InitialLifeEnergyAmount = 600;
	public const int InitialSoulShardAmount = 0;
	public const int InitialResourceAmount = InitialLifeEnergyAmount;

	public const int TrapPlaceLifeEnergyCost = 100;
	public const int DoorRepairLifeEnergyCost = 100;

	public const int LifeEnergyBuildingCost = 250;
	public const int UnitBuildingLifeEnergyCost = 200;
	public const int CorpseRefineryBuildingCost = 220;
	public const int ResearchBuildingLifeEnergyCost = 300;
	public const int FacilityUpgradeLifeEnergyCost = 200;
	public const int FacilityUpgradeSoulShardCost = 25;
	public const int FacilityFullRepairLifeEnergyCost = 120;
	public const int FacilityRelocateDismantleLifeEnergyCost = 0; // 시설 이동은 분해·운반·재설치 모두 무상이다(건설 명세 2-6)

	public const int UnitProductionLifeEnergyCost = 100;
	public const int CoreRestoreBaseLifeEnergyCost = 300;

	public const int DebugMaxAmount = 999999;

	private readonly Dictionary<ResourceType, ResourceAccount> _accounts = new Dictionary<ResourceType, ResourceAccount>();
	private readonly HashSet<string> _unlockedMonsterSpecies = new HashSet<string>();
	private readonly HashSet<FacilityKind> _unlockedFacilities = new HashSet<FacilityKind>();

	public GlobalResearchLedger ResearchLedger { get; } = new GlobalResearchLedger();

	// debug 전용 영혼 파편 사용 강제 해금 스위치
	public bool DebugForceUnlockSoulShard { get; set; }

	public IReadOnlyDictionary<ResourceType, ResourceAccount> Accounts => _accounts;
	public ResourceAccount LifeEnergyAccount => GetOrCreateAccount(ResourceType.LifeEnergy);
	public ResourceAccount SoulShardAccount => GetOrCreateAccount(ResourceType.SoulShard);
	public IReadOnlyCollection<string> UnlockedMonsterSpecies => _unlockedMonsterSpecies;
	public IReadOnlyCollection<FacilityKind> UnlockedFacilities => _unlockedFacilities;

	// 영혼 파편은 최초 코어 완전 활성화 전에도 획득·보유할 수 있으나 소비와 연구시설 건설은 최초 코어 완전 활성화 이후에만 가능하다(2-표1, 15-표1, 23-표1).
	public bool IsSoulShardSpendUnlocked
		=> DebugForceUnlockSoulShard
		|| (GameSession.Instance != null && GameSession.Instance.Core != null && GameSession.Instance.Core.HasAnyCoreEverBeenActivated);

	public ResourceManager()
	{
		InitializeAccountsAndDefaults();
	}

	public override async UniTask Initialize(CancellationToken cts)
	{
		await base.Initialize(cts);
		Instance = this;
		InitializeAccountsAndDefaults();
		LogHelper.Log(LogHelper.GAME, "ResourceManager Initialized (생명 에너지 / 영혼 파편 2자원 체계)");
	}

	public override async UniTask Finalize()
	{
		_accounts.Clear();
		LogHelper.Log(LogHelper.GAME, "ResourceManager Disposed");
		await base.Finalize();
	}

	private void InitializeAccountsAndDefaults()
	{
		_accounts[ResourceType.LifeEnergy] = new ResourceAccount(ResourceType.LifeEnergy, InitialLifeEnergyAmount);
		_accounts[ResourceType.SoulShard] = new ResourceAccount(ResourceType.SoulShard, InitialSoulShardAmount);

		_unlockedMonsterSpecies.Clear();
		_unlockedMonsterSpecies.Add("MeleeTank");

		_unlockedFacilities.Clear();
		_unlockedFacilities.Add(FacilityKind.LifeEnergy);
		_unlockedFacilities.Add(FacilityKind.CorpseRefinery);
		_unlockedFacilities.Add(FacilityKind.UnitProduction);
		_unlockedFacilities.Add(FacilityKind.Dummy);

		foreach (var def in GlobalResearchLedger.CreateDefaultPrototypeCatalog())
			ResearchLedger.RegisterDefinition(def);
	}

	private ResourceAccount GetOrCreateAccount(ResourceType type)
	{
		if (!_accounts.TryGetValue(type, out var acc))
		{
			acc = new ResourceAccount(type, 0.0);
			_accounts[type] = acc;
		}
		return acc;
	}

	// 플레이어 몬스터·방어건물·함정이 적 몬스터를 처치했을 때 해당 종류 생산 해금(8-표1).
	public bool UnlockMonsterSpecies(string speciesTypeName)
	{
		if (string.IsNullOrEmpty(speciesTypeName)) return false;
		bool added = _unlockedMonsterSpecies.Add(speciesTypeName);
		if (added)
			LogHelper.Log(LogHelper.GAME, $"[해금] 몬스터 종류 생산 해금: {speciesTypeName}");
		return added;
	}

	public bool IsMonsterSpeciesUnlocked(string speciesTypeName)
		=> string.IsNullOrEmpty(speciesTypeName) || _unlockedMonsterSpecies.Contains(speciesTypeName);

	// 방 클리어 시 그 방에 있었던 던전 시설 종류(파괴된 종류 포함, 인류 시설 제외)를 해금한다(8-표1, 8-1-표1).
	public bool UnlockFacilityKind(FacilityKind kind)
	{
		bool added = _unlockedFacilities.Add(kind);
		if (added)
			LogHelper.Log(LogHelper.GAME, $"[해금] 던전 시설 종류 해금: {kind}");
		return added;
	}

	public bool IsFacilityKindUnlocked(FacilityKind kind)
	{
		if (kind == FacilityKind.Research)
			return IsSoulShardSpendUnlocked;
		return _unlockedFacilities.Contains(kind);
	}

	public void AddResource(ResourceType type, double amount, ResourceCreditSource source = ResourceCreditSource.FacilityProduction)
	{
		if (amount <= 0.0) return;
		GetOrCreateAccount(type).Credit(amount, source);
	}

	public void AddResource(ResourceType type, int amount)
		=> AddResource(type, (double)amount, ResourceCreditSource.FacilityProduction);

	public bool TryConsumeResource(ResourceType type, double rawAmount, bool notifyOnFailure = true)
	{
		if (rawAmount < 0.0) return false;
		int needed = EconomyMath.CeilCost(rawAmount);
		if (needed == 0) return true;

		if (type == ResourceType.SoulShard && !IsSoulShardSpendUnlocked)
		{
			if (notifyOnFailure)
				NoticeCenter.Instance?.PushMomentary("최초 코어 완전 활성화 전에는 영혼 파편을 사용할 수 없습니다.", NoticeCenter.WarningColor);
			return false;
		}

		ResourceAccount acc = GetOrCreateAccount(type);
		if (acc.TrySpendImmediate(rawAmount, out int paid))
		{
			LogHelper.Log(LogHelper.GAME, $"[자원] {EconomyMath.GetDisplayName(type)} {paid} 차감 (잔여 표시: {acc.FormatDisplay()})");
			return true;
		}

		string msg = EconomyMath.FormatInsufficientNotice(type, needed, acc.DisplayAvailable);
		LogHelper.Warning(LogHelper.GAME, msg);
		if (notifyOnFailure)
			NoticeCenter.Instance?.PushMomentary(msg, NoticeCenter.WarningColor);
		return false;
	}

	public bool TryConsumeResource(ResourceType type, int amount)
		=> TryConsumeResource(type, (double)amount, notifyOnFailure: true);

	public bool TryConsumeResources(List<ResourceCost> costs)
	{
		if (costs == null || costs.Count == 0) return true;

		var rawTuples = new List<(ResourceType type, double rawCost)>(costs.Count);
		for (int i = 0; i < costs.Count; i++)
			rawTuples.Add((costs[i].resourceType, costs[i].amount));

		if (!ResourceTransaction.CanReserveAll(
			_accounts,
			rawTuples,
			IsSoulShardSpendUnlocked,
			out ResourceType failedType,
			out int req,
			out int avail,
			out bool soulLocked))
		{
			string msg = soulLocked
				? "최초 코어 완전 활성화 전에는 영혼 파편을 사용할 수 없습니다."
				: EconomyMath.FormatInsufficientNotice(failedType, req, avail);
			LogHelper.Warning(LogHelper.GAME, msg);
			NoticeCenter.Instance?.PushMomentary(msg, NoticeCenter.WarningColor);
			return false;
		}

		var aggregated = ResourceTransaction.AggregateCeilCosts(rawTuples);
		foreach (var kv in aggregated)
			GetOrCreateAccount(kv.Key).TrySpendImmediate(kv.Value, out _);
		return true;
	}

	public bool HasEnoughResource(ResourceType type, double rawAmount)
	{
		int needed = EconomyMath.CeilCost(rawAmount);
		if (needed <= 0) return true;
		if (type == ResourceType.SoulShard && !IsSoulShardSpendUnlocked) return false;
		return GetOrCreateAccount(type).CanAffordInt(needed);
	}

	public bool HasEnoughResource(ResourceType type, int amount)
		=> HasEnoughResource(type, (double)amount);

	// 정수 버림된 총 보유량 표시값(기존 호출부 호환)
	public int GetResourceAmount(ResourceType type) => GetOrCreateAccount(type).DisplayBalance;

	// 선점액을 제외한 실제 사용 가능 정수량
	public int GetAvailableAmount(ResourceType type) => GetOrCreateAccount(type).DisplayAvailable;

	public int GetReservedAmount(ResourceType type) => GetOrCreateAccount(type).ReservedAmount;

	public double GetCumulativeEarned(ResourceType type) => GetOrCreateAccount(type).CumulativeEarned;

	public string FormatResourceDisplay(ResourceType type) => GetOrCreateAccount(type).FormatDisplay();

	public void DebugSetResource(ResourceType type, int amount)
	{
		int next = Mathf.Clamp(amount, 0, DebugMaxAmount);
		ResourceAccount acc = GetOrCreateAccount(type);
		acc.DebugSetBalance(next);
		LogHelper.Log(LogHelper.GAME, $"[debug] {EconomyMath.GetDisplayName(type)} → {next}");
	}

	public void DebugAdjustResource(ResourceType type, int delta)
		=> DebugSetResource(type, (int)Math.Clamp((long)GetResourceAmount(type) + delta, 0L, DebugMaxAmount));

	public void DebugResetResources()
	{
		GetOrCreateAccount(ResourceType.LifeEnergy).ResetForDebug(InitialLifeEnergyAmount);
		GetOrCreateAccount(ResourceType.SoulShard).ResetForDebug(InitialSoulShardAmount);
		LogHelper.Log(LogHelper.GAME, $"[debug] 자원을 초기값(생명 에너지 {InitialLifeEnergyAmount}, 영혼 파편 {InitialSoulShardAmount})으로 되돌림");
	}
}
