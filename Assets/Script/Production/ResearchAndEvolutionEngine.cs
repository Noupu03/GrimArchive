using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ResearchDefinition
{
	public string ResearchId;
	public string DisplayName;
	public string Description;
	public double BaseLifeEnergyCost;
	public double BaseSoulShardCost;
	public double RequiredWorkSeconds = 15.0;
	public List<string> PrerequisiteIds = new List<string>();
	public int? RequiredActivatedFloor;

	// 종류별 유닛 업그레이드 효과(15-표4)
	public string TargetSpeciesTypeName;
	public List<(DetailStat stat, float flatDelta)> SpeciesFlatStatBonuses = new List<(DetailStat, float)>();

	// 인류 정보 확인 연구 여부(15-표4)
	public bool UnlocksHumanIntelView;

	public List<(ResourceType type, double rawCost)> BuildRawCosts(double multiplier = 1.0)
	{
		var list = new List<(ResourceType, double)>(2);
		double m = Math.Max(0.0, multiplier);
		if (BaseLifeEnergyCost > 0.0) list.Add((ResourceType.LifeEnergy, BaseLifeEnergyCost * m));
		if (BaseSoulShardCost > 0.0) list.Add((ResourceType.SoulShard, BaseSoulShardCost * m));
		return list;
	}
}

public enum ResearchEnqueueBlock
{
	None,
	ResearchFacilityLocked,
	FloorCoreNotActivated,
	AlreadyCompleted,
	DuplicateInProgressOrQueued,
	PrerequisiteMissing,
	SoulShardLocked,
	InsufficientResources,
}

// 세이브 데이터 단위로 전역 공유되는 연구 진척·지불 보존·완료 원장(명세서 v0.2 15항, 22항).
public sealed class GlobalResearchLedger
{
	private readonly Dictionary<string, ResearchDefinition> _catalog = new Dictionary<string, ResearchDefinition>();
	private readonly HashSet<string> _completedIds = new HashSet<string>();
	private readonly Dictionary<string, double> _progress01ById = new Dictionary<string, double>();
	private readonly Dictionary<string, Dictionary<ResourceType, int>> _preservedPaidById = new Dictionary<string, Dictionary<ResourceType, int>>();
	private readonly Dictionary<string, int> _queuedFacilityById = new Dictionary<string, int>();

	public IReadOnlyCollection<string> CompletedIds => _completedIds;
	public IReadOnlyDictionary<string, ResearchDefinition> Catalog => _catalog;

	public bool IsHumanIntelUnlocked { get; private set; }

	public void RegisterDefinition(ResearchDefinition def)
	{
		if (def == null || string.IsNullOrEmpty(def.ResearchId)) return;
		_catalog[def.ResearchId] = def;
	}

	public ResearchDefinition GetDefinition(string researchId)
		=> !string.IsNullOrEmpty(researchId) && _catalog.TryGetValue(researchId, out var d) ? d : null;

	public bool IsCompleted(string researchId) => _completedIds.Contains(researchId);

	public bool IsQueuedAnywhere(string researchId) => _queuedFacilityById.ContainsKey(researchId);

	public double GetProgress01(string researchId)
		=> _progress01ById.TryGetValue(researchId, out double p) ? p : 0.0;

	public bool HasPreservedPayment(string researchId)
		=> _preservedPaidById.ContainsKey(researchId);

	public bool AreAllPrerequisitesCompleted(ResearchDefinition def)
	{
		if (def?.PrerequisiteIds == null) return true;
		for (int i = 0; i < def.PrerequisiteIds.Count; i++)
		{
			string pre = def.PrerequisiteIds[i];
			if (!string.IsNullOrEmpty(pre) && !_completedIds.Contains(pre))
				return false;
		}
		return true;
	}

	// 선행 연구가 완료됐거나 이미 전역 어딘가에 진행·예약 중이면 후속 예약을 허용한다(15-표3).
	public bool ArePrerequisitesCompletedOrQueued(ResearchDefinition def)
	{
		if (def?.PrerequisiteIds == null) return true;
		for (int i = 0; i < def.PrerequisiteIds.Count; i++)
		{
			string pre = def.PrerequisiteIds[i];
			if (string.IsNullOrEmpty(pre)) continue;
			if (!_completedIds.Contains(pre) && !_queuedFacilityById.ContainsKey(pre))
				return false;
		}
		return true;
	}

	internal void BindFacilityQueue(string researchId, int facilityId) => _queuedFacilityById[researchId] = facilityId;
	internal void UnbindFacilityQueue(string researchId) => _queuedFacilityById.Remove(researchId);

	internal void SetProgress01(string researchId, double progress01)
		=> _progress01ById[researchId] = Math.Clamp(progress01, 0.0, 1.0);

	internal void RecordPaid(string researchId, Dictionary<ResourceType, int> paid)
		=> _preservedPaidById[researchId] = new Dictionary<ResourceType, int>(paid);

	internal bool TryConsumePreservedPaymentForRefund(string researchId, out Dictionary<ResourceType, int> paid)
	{
		if (_preservedPaidById.TryGetValue(researchId, out paid))
		{
			_preservedPaidById.Remove(researchId);
			return true;
		}
		paid = null;
		return false;
	}

	internal void MarkCompleted(ResearchDefinition def)
	{
		if (def == null) return;
		_completedIds.Add(def.ResearchId);
		_progress01ById[def.ResearchId] = 1.0;
		_preservedPaidById.Remove(def.ResearchId);
		_queuedFacilityById.Remove(def.ResearchId);
		if (def.UnlocksHumanIntelView)
			IsHumanIntelUnlocked = true;
	}

	// 완료된 종류별 업그레이드를 해당 유닛의 StatModifierLedger에 일괄 반영한다(15-표4).
	public void ApplyCompletedSpeciesUpgradesToUnit(Unit unit)
	{
		if (unit == null || unit.unitType == null) return;
		string typeName = unit.unitType.GetType().Name;
		foreach (string id in _completedIds)
		{
			if (!_catalog.TryGetValue(id, out var def)) continue;
			if (string.IsNullOrEmpty(def.TargetSpeciesTypeName) || def.TargetSpeciesTypeName != typeName) continue;
			if (def.SpeciesFlatStatBonuses != null && def.SpeciesFlatStatBonuses.Count > 0)
				unit.StatLedger.SetFlat($"Research:{def.ResearchId}", def.SpeciesFlatStatBonuses);
		}
	}

	public static List<ResearchDefinition> CreateDefaultPrototypeCatalog()
	{
		// 자리표시자 연구 카탈로그 — 첫 코어 완전 활성화 후 해금되는 기본 업그레이드·인류 확인 연구.
		return new List<ResearchDefinition>
		{
			new ResearchDefinition
			{
				ResearchId = "res_melee_tank_tier1",
				DisplayName = "근접 몬스터 외피 강화 I",
				Description = "몬스터(근접)의 물리 공격력 +4, 물리 방어력 +4",
				BaseLifeEnergyCost = 150,
				BaseSoulShardCost = 20,
				RequiredWorkSeconds = 12.0,
				TargetSpeciesTypeName = "MeleeTank",
				SpeciesFlatStatBonuses = new List<(DetailStat, float)>
				{
					(DetailStat.PhysicalAttack, 4f),
					(DetailStat.PhysicalDefense, 4f),
				},
			},
			new ResearchDefinition
			{
				ResearchId = "res_melee_tank_tier2",
				DisplayName = "근접 몬스터 외피 강화 II",
				Description = "몬스터(근접)의 물리 공격력 +6, 물리 방어력 +6 (선행: I단계)",
				BaseLifeEnergyCost = 250,
				BaseSoulShardCost = 40,
				RequiredWorkSeconds = 18.0,
				PrerequisiteIds = new List<string> { "res_melee_tank_tier1" },
				TargetSpeciesTypeName = "MeleeTank",
				SpeciesFlatStatBonuses = new List<(DetailStat, float)>
				{
					(DetailStat.PhysicalAttack, 6f),
					(DetailStat.PhysicalDefense, 6f),
				},
			},
			new ResearchDefinition
			{
				ResearchId = "res_human_intel_basic",
				DisplayName = "침공군 정찰 해석",
				Description = "인류 침공군의 수집 기록·다음 침공 정보를 확인",
				BaseLifeEnergyCost = 120,
				BaseSoulShardCost = 15,
				RequiredWorkSeconds = 10.0,
				UnlocksHumanIntelView = true,
			},
		};
	}
}

public sealed class FacilityResearchEntry
{
	public ResearchDefinition Definition;
	public Dictionary<ResourceType, int> ReservedUnstartedCosts = new Dictionary<ResourceType, int>();
	public bool UsedPreservedPayment;
}

// 개별 연구시설의 대기열 — 전역 원장(GlobalResearchLedger)과 연동된다(명세서 v0.2 15-표1~표4).
public sealed class FacilityResearchQueue
{
	private const double Epsilon = 1e-9;

	private readonly int _facilityId;
	private readonly GlobalResearchLedger _ledger;
	private readonly List<FacilityResearchEntry> _entries = new List<FacilityResearchEntry>();

	public IReadOnlyList<FacilityResearchEntry> Entries => _entries;

	public FacilityResearchQueue(int facilityId, GlobalResearchLedger ledger)
	{
		_facilityId = facilityId;
		_ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
	}

	public ResearchEnqueueBlock TryEnqueue(
		ResearchDefinition def,
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts,
		bool isAnyCoreEverActivated,
		bool isRequiredFloorCoreEverActivated,
		double reserveCostMultiplier,
		out string failureMessage)
	{
		failureMessage = string.Empty;
		if (def == null)
		{
			failureMessage = "유효하지 않은 연구 항목입니다.";
			return ResearchEnqueueBlock.ResearchFacilityLocked;
		}
		if (!isAnyCoreEverActivated)
		{
			failureMessage = "최초 코어 완전 활성화 이후에 연구를 진행할 수 있습니다.";
			return ResearchEnqueueBlock.ResearchFacilityLocked;
		}
		if (def.RequiredActivatedFloor.HasValue && !isRequiredFloorCoreEverActivated)
		{
			failureMessage = $"{def.RequiredActivatedFloor.Value}층 코어 완전 활성화가 필요합니다.";
			return ResearchEnqueueBlock.FloorCoreNotActivated;
		}
		if (_ledger.IsCompleted(def.ResearchId))
		{
			failureMessage = "이미 완료된 연구입니다.";
			return ResearchEnqueueBlock.AlreadyCompleted;
		}
		if (_ledger.IsQueuedAnywhere(def.ResearchId))
		{
			failureMessage = "이미 진행 또는 예약 중인 연구입니다.";
			return ResearchEnqueueBlock.DuplicateInProgressOrQueued;
		}
		if (!_ledger.ArePrerequisitesCompletedOrQueued(def))
		{
			failureMessage = "선행 연구가 완료되거나 예약되어 있어야 합니다.";
			return ResearchEnqueueBlock.PrerequisiteMissing;
		}

		_ledger.RegisterDefinition(def);

		// 이전 시설 파괴·철거로 지불 상태가 보존된 연구는 추가 선점·차감 없이 등록한다(15-표2, 15-표3).
		if (_ledger.HasPreservedPayment(def.ResearchId))
		{
			_entries.Add(new FacilityResearchEntry
			{
				Definition = def,
				UsedPreservedPayment = true,
			});
			_ledger.BindFacilityQueue(def.ResearchId, _facilityId);
			return ResearchEnqueueBlock.None;
		}

		var rawCosts = def.BuildRawCosts(reserveCostMultiplier);
		if (!ResourceTransaction.TryReserveAll(
			accounts,
			rawCosts,
			isAnyCoreEverActivated,
			out var reserved,
			out ResourceType failedType,
			out int req,
			out int avail,
			out bool soulLocked))
		{
			failureMessage = soulLocked
				? "최초 코어 완전 활성화 전에는 영혼 파편을 소비할 수 없습니다."
				: EconomyMath.FormatInsufficientNotice(failedType, req, avail);
			return soulLocked ? ResearchEnqueueBlock.SoulShardLocked : ResearchEnqueueBlock.InsufficientResources;
		}

		_entries.Add(new FacilityResearchEntry
		{
			Definition = def,
			ReservedUnstartedCosts = reserved,
			UsedPreservedPayment = false,
		});
		_ledger.BindFacilityQueue(def.ResearchId, _facilityId);
		return ResearchEnqueueBlock.None;
	}

	// 같은 시설 내 순서 변경: 선행 연구가 후속 연구 뒤로 가는 순서는 금지하며, 진행 중 연구를 옮겨도 진척·지불 상태를 보존한다(15-표3).
	public bool TryMoveEntry(string researchId, int targetIndex, out string failureMessage)
	{
		failureMessage = string.Empty;
		int oldIdx = FindIndex(researchId);
		if (oldIdx < 0 || _entries.Count <= 1) return false;
		targetIndex = Math.Clamp(targetIndex, 0, _entries.Count - 1);
		if (oldIdx == targetIndex) return false;

		var simulated = new List<FacilityResearchEntry>(_entries);
		FacilityResearchEntry moving = simulated[oldIdx];
		simulated.RemoveAt(oldIdx);
		simulated.Insert(targetIndex, moving);

		for (int i = 0; i < simulated.Count; i++)
		{
			var laterDef = simulated[i].Definition;
			if (laterDef.PrerequisiteIds == null) continue;
			for (int j = i + 1; j < simulated.Count; j++)
			{
				if (laterDef.PrerequisiteIds.Contains(simulated[j].Definition.ResearchId))
				{
					failureMessage = "선행 연구를 후속 연구 뒤로 옮길 수 없습니다.";
					return false;
				}
			}
		}

		_entries.Clear();
		_entries.AddRange(simulated);
		return true;
	}

	// 플레이어 직접 취소: 진척은 보존하고 실제 지불액(또는 미시작 선점액)을 환급·해제한다(15-표2, 15-표3).
	// 완료와 같은 판정 시점에 취소된 경우에는 완료 효과를 적용하고 환급하지 않는다(15-표3, 23-표1).
	public bool CancelManually(
		string researchId,
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts,
		bool completedOnSameTick = false,
		Action<ResearchDefinition> onCompletedIfSameTick = null)
	{
		int idx = FindIndex(researchId);
		if (idx < 0) return false;

		FacilityResearchEntry entry = _entries[idx];
		if (completedOnSameTick)
		{
			_entries.RemoveAt(idx);
			_ledger.MarkCompleted(entry.Definition);
			onCompletedIfSameTick?.Invoke(entry.Definition);
			return true;
		}

		if (entry.ReservedUnstartedCosts != null && entry.ReservedUnstartedCosts.Count > 0)
		{
			ResourceTransaction.ReleaseAll(accounts, entry.ReservedUnstartedCosts);
			entry.ReservedUnstartedCosts.Clear();
		}

		if (_ledger.TryConsumePreservedPaymentForRefund(researchId, out var paid))
			ResourceTransaction.RefundAll(accounts, paid);

		_ledger.UnbindFacilityQueue(researchId);
		_entries.RemoveAt(idx);
		return true;
	}

	// 작업자 기반 연구 진행 — 시설 파괴가 같은 판정 시점에 발생했으면 피해·파괴 우선으로 완료를 적용하지 않고 진척·지불을 보존한다(23-표1).
	public bool TickResearch(
		double deltaSeconds,
		IReadOnlyList<float> activeWorkerSpeeds,
		bool isFacilityOperational,
		double facilityResearchSpeedMultiplier,
		double startCostMultiplier,
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts,
		bool facilityDestroyedOnSameTick = false,
		Action<ResearchDefinition> onCompleted = null)
	{
		if (facilityDestroyedOnSameTick)
		{
			OnFacilityLost(accounts);
			return false;
		}

		if (!isFacilityOperational || _entries.Count == 0)
			return false;

		float coopSpeed = WorkSpeedMath.CalculateTotalWorkSpeed(activeWorkerSpeeds);
		if (coopSpeed <= 0f)
			return false;

		FacilityResearchEntry head = _entries[0];
		ResearchDefinition def = head.Definition;

		// 앞 연구의 선행 조건이 아직 미완료면 뒤 연구가 추월하지 않고 대기한다(15-표1, 15-표3).
		if (!_ledger.AreAllPrerequisitesCompleted(def))
			return false;

		// 아직 지불되지 않은 연구면 실제 첫 시작 시점에 1회 차감하고 지불 보존 원장에 기록한다(15-표3).
		if (!_ledger.HasPreservedPayment(def.ResearchId))
		{
			var actualCosts = def.BuildRawCosts(startCostMultiplier);
			if (!ResourceTransaction.CommitAll(accounts, head.ReservedUnstartedCosts, actualCosts, out var paid))
				return false;
			head.ReservedUnstartedCosts.Clear();
			head.UsedPreservedPayment = true;
			_ledger.RecordPaid(def.ResearchId, paid);
		}

		if (deltaSeconds <= 0.0 || facilityResearchSpeedMultiplier <= 0.0)
			return false;

		double duration = Math.Max(0.001, def.RequiredWorkSeconds);
		double prevProgress = _ledger.GetProgress01(def.ResearchId);
		double nextProgress = prevProgress + (deltaSeconds * coopSpeed * facilityResearchSpeedMultiplier) / duration;

		if (nextProgress + Epsilon >= 1.0)
		{
			_entries.RemoveAt(0);
			_ledger.MarkCompleted(def);
			onCompleted?.Invoke(def);
			return true;
		}

		_ledger.SetProgress01(def.ResearchId, nextProgress);
		return false;
	}

	// 시설 파괴 또는 철거: 미시작 예약은 선점 해제하고, 이미 시작해 지불된 연구는 진척과 지불 상태를 모두 보존한다(15-표2, 15-표3, 20-1-표1).
	public void OnFacilityLost(IReadOnlyDictionary<ResourceType, ResourceAccount> accounts)
	{
		for (int i = 0; i < _entries.Count; i++)
		{
			FacilityResearchEntry entry = _entries[i];
			if (entry.ReservedUnstartedCosts != null && entry.ReservedUnstartedCosts.Count > 0)
			{
				ResourceTransaction.ReleaseAll(accounts, entry.ReservedUnstartedCosts);
				entry.ReservedUnstartedCosts.Clear();
			}
			_ledger.UnbindFacilityQueue(entry.Definition.ResearchId);
		}
		_entries.Clear();
	}

	private int FindIndex(string researchId)
	{
		for (int i = 0; i < _entries.Count; i++)
			if (_entries[i].Definition.ResearchId == researchId) return i;
		return -1;
	}
}

// ── 개체 진화 (명세서 v0.2 15-1항, 15-2항, 24-2항) ──

[Serializable]
public sealed class EvolutionDefinition
{
	public string EvolutionId;
	public string DisplayName;
	public string ResultSpeciesKey;
	public double BaseSoulShardCost = 100.0;
	public int PresentationDurationSeconds = 2;
	public float TotalMaxHpIncrease = 100f;
	public List<(DetailStat stat, float totalDelta)> TotalFlatStatIncreases = new List<(DetailStat, float)>();
	public Vector2Int PostEvolutionFootprint = Vector2Int.one;
	public int PostEvolutionPopulationCost = 1;
}

public enum EvolutionStartBlock
{
	None,
	AlreadyEvolving,
	NotQualifiedOrUnlocked,
	SoulShardLocked,
	InsufficientSoulShard,
	InsufficientSpace,
	InsufficientRoomPopulation,
}

[Serializable]
public struct SavedUnitCommandSnapshot
{
	public bool HadManualMove;
	public Vector2Int MoveTarget;
	public Unit AttackUnitTarget;
	public Vector3Int? AttackObjectTarget;
	public bool WasHalted;
	public bool WasStandGroundAttack;
	// 보관한 이동 명령이 자동 방어 복귀였는가 — 복원 뒤에도 전투·경계에 밀리는 이동으로 남겨 직접 이동 적용과 구분한다(배치모드 1-6).
	public bool WasDefenseAutoReturn;
}

[Serializable]
public struct EvolutionSessionSnapshot
{
	public string EvolutionId;
	public int PresentationDurationSeconds;
	public double ElapsedSeconds;
	public int AppliedStepCount;
	public float TotalMaxHpIncrease;
	public float AppliedHpSoFar;
	public bool IsCompleted;
	public bool WasKilledMidPresentation;
}

public sealed class EvolutionSession
{
	public const string SpaceInsufficientMessage = "공간이 부족합니다.";
	public const string RoomPopInsufficientMessage = "방 정원이 부족합니다.";

	private const double Epsilon = 1e-9;

	public EvolutionDefinition Definition { get; }
	public SavedUnitCommandSnapshot SavedCommand { get; }
	public int PaidSoulShardAmount { get; }

	public double ElapsedSeconds { get; private set; }
	public int AppliedStepCount { get; private set; }
	public float AppliedHpSoFar { get; private set; }
	public bool IsCompleted { get; private set; }
	public bool WasKilledMidPresentation { get; private set; }
	public bool IsActivePresentation => !IsCompleted && !WasKilledMidPresentation;

	private readonly float[] _appliedStatTotals;

	public EvolutionSession(
		EvolutionDefinition definition,
		int paidSoulShardAmount,
		SavedUnitCommandSnapshot savedCommand)
	{
		Definition = definition ?? throw new ArgumentNullException(nameof(definition));
		PaidSoulShardAmount = paidSoulShardAmount;
		SavedCommand = savedCommand;
		int statCount = Definition.TotalFlatStatIncreases != null ? Definition.TotalFlatStatIncreases.Count : 0;
		_appliedStatTotals = new float[statCount];
	}

	// 진화 버튼 클릭 시 자격·사용 가능 영혼 파편·진화 후 공간·진화 후 방 정원을 모두 검사하고 1회 차감한다(15-1-표1, 15-1-표2).
	public static EvolutionStartBlock TryBeginEvolution(
		EvolutionDefinition def,
		bool isAlreadyEvolving,
		bool isQualifiedAndUnlocked,
		bool isSoulShardSpendUnlocked,
		double costMultiplier,
		ResourceAccount soulShardAccount,
		bool hasPostEvolutionFootprintSpace,
		int currentRoomLivingPop,
		int currentUnitPopCost,
		int roomMaxPop,
		SavedUnitCommandSnapshot commandSnapshotToSave,
		ref double unitCumulativeBaseEvolutionSoulCost,
		out EvolutionSession startedSession,
		out string failureMessage)
	{
		startedSession = null;
		failureMessage = string.Empty;

		if (isAlreadyEvolving)
			return EvolutionStartBlock.AlreadyEvolving;

		if (def == null || !isQualifiedAndUnlocked)
		{
			failureMessage = "진화 자격을 충족하지 않았습니다.";
			return EvolutionStartBlock.NotQualifiedOrUnlocked;
		}

		if (!isSoulShardSpendUnlocked)
		{
			failureMessage = "최초 코어 완전 활성화 전에는 진화할 수 없습니다.";
			return EvolutionStartBlock.SoulShardLocked;
		}

		double discountedCost = Math.Max(0.0, def.BaseSoulShardCost * Math.Max(0.0, costMultiplier));
		int neededSoul = EconomyMath.CeilCost(discountedCost);
		if (soulShardAccount == null || !soulShardAccount.CanAffordInt(neededSoul))
		{
			int avail = soulShardAccount != null ? soulShardAccount.DisplayAvailable : 0;
			failureMessage = EconomyMath.FormatInsufficientNotice(ResourceType.SoulShard, neededSoul, avail);
			return EvolutionStartBlock.InsufficientSoulShard;
		}

		if (!hasPostEvolutionFootprintSpace)
		{
			failureMessage = SpaceInsufficientMessage;
			return EvolutionStartBlock.InsufficientSpace;
		}

		int popAfter = currentRoomLivingPop - currentUnitPopCost + def.PostEvolutionPopulationCost;
		if (popAfter > roomMaxPop)
		{
			failureMessage = RoomPopInsufficientMessage;
			return EvolutionStartBlock.InsufficientRoomPopulation;
		}

		if (!soulShardAccount.TrySpendImmediate(discountedCost, out int actualPaid))
		{
			failureMessage = EconomyMath.FormatInsufficientNotice(ResourceType.SoulShard, neededSoul, soulShardAccount.DisplayAvailable);
			return EvolutionStartBlock.InsufficientSoulShard;
		}

		// 시작 즉시 진화 상태와 누적 기준 진화 비용을 확정한다(연출 중 사망해도 14-1항 보상에 그대로 반영).
		unitCumulativeBaseEvolutionSoulCost += Math.Max(0.0, def.BaseSoulShardCost);
		startedSession = new EvolutionSession(def, actualPaid, commandSnapshotToSave);
		return EvolutionStartBlock.None;
	}

	// 초 단위(1.0s 간격) 성장 적용 — 현재 HP와 최대 HP를 같은 양만큼 올리고 마지막 단계에서 잔여분을 정확히 맞춘다(15-2-표1, 15-2-표2).
	public void Tick(
		double deltaGameSeconds,
		ref float currentHp,
		ref float maxHp,
		Action<IReadOnlyList<(DetailStat stat, float cumulativeAppliedDelta)>> onStatsUpdated = null)
	{
		if (!IsActivePresentation) return;

		if (currentHp <= 0f)
		{
			WasKilledMidPresentation = true;
			return;
		}

		if (deltaGameSeconds <= 0.0) return;

		int totalSteps = Math.Max(1, Definition.PresentationDurationSeconds);
		ElapsedSeconds += deltaGameSeconds;

		bool anyStepApplied = false;
		while (AppliedStepCount < totalSteps && ElapsedSeconds + Epsilon >= (AppliedStepCount + 1) * 1.0)
		{
			if (currentHp <= 0f)
			{
				WasKilledMidPresentation = true;
				return;
			}

			AppliedStepCount++;
			bool isFinalStep = AppliedStepCount >= totalSteps;

			float targetCumHp = ComputeStepTarget(Definition.TotalMaxHpIncrease, AppliedStepCount, totalSteps, isFinalStep);
			float hpDelta = targetCumHp - AppliedHpSoFar;
			AppliedHpSoFar = targetCumHp;

			maxHp += hpDelta;
			currentHp += hpDelta;

			if (Definition.TotalFlatStatIncreases != null)
			{
				for (int i = 0; i < Definition.TotalFlatStatIncreases.Count; i++)
				{
					float totalStat = Definition.TotalFlatStatIncreases[i].totalDelta;
					_appliedStatTotals[i] = ComputeStepTarget(totalStat, AppliedStepCount, totalSteps, isFinalStep);
				}
			}

			anyStepApplied = true;
			if (isFinalStep)
			{
				IsCompleted = true;
				break;
			}
		}

		if (anyStepApplied && onStatsUpdated != null && Definition.TotalFlatStatIncreases != null && Definition.TotalFlatStatIncreases.Count > 0)
		{
			var cumulativeList = new List<(DetailStat, float)>(Definition.TotalFlatStatIncreases.Count);
			for (int i = 0; i < Definition.TotalFlatStatIncreases.Count; i++)
				cumulativeList.Add((Definition.TotalFlatStatIncreases[i].stat, _appliedStatTotals[i]));
			onStatsUpdated(cumulativeList);
		}
	}

	public void NotifyKilled()
	{
		if (!IsCompleted)
			WasKilledMidPresentation = true;
	}

	public EvolutionSessionSnapshot CaptureSnapshot() => new EvolutionSessionSnapshot
	{
		EvolutionId = Definition.EvolutionId,
		PresentationDurationSeconds = Definition.PresentationDurationSeconds,
		ElapsedSeconds = ElapsedSeconds,
		AppliedStepCount = AppliedStepCount,
		TotalMaxHpIncrease = Definition.TotalMaxHpIncrease,
		AppliedHpSoFar = AppliedHpSoFar,
		IsCompleted = IsCompleted,
		WasKilledMidPresentation = WasKilledMidPresentation,
	};

	// 불러오기 복원 — 이미 적용된 단계·HP를 기록대로 복구해 비용·능력치 중복 적용이나 피해 초기화를 막는다(22-표1, 24-2-표1).
	public void RestoreSnapshot(EvolutionSessionSnapshot snapshot)
	{
		ElapsedSeconds = Math.Max(0.0, snapshot.ElapsedSeconds);
		AppliedStepCount = Math.Max(0, snapshot.AppliedStepCount);
		AppliedHpSoFar = snapshot.AppliedHpSoFar;
		IsCompleted = snapshot.IsCompleted;
		WasKilledMidPresentation = snapshot.WasKilledMidPresentation;

		int totalSteps = Math.Max(1, Definition.PresentationDurationSeconds);
		if (Definition.TotalFlatStatIncreases != null)
		{
			bool isFinal = AppliedStepCount >= totalSteps;
			for (int i = 0; i < Definition.TotalFlatStatIncreases.Count; i++)
			{
				_appliedStatTotals[i] = AppliedStepCount <= 0
					? 0f
					: ComputeStepTarget(Definition.TotalFlatStatIncreases[i].totalDelta, Math.Min(AppliedStepCount, totalSteps), totalSteps, isFinal);
			}
		}
	}

	private static float ComputeStepTarget(float totalIncrease, int step, int totalSteps, bool isFinalStep)
	{
		if (isFinalStep || totalSteps <= 1)
			return totalIncrease;

		bool isWholeNumber = Math.Abs(totalIncrease - MathF.Round(totalIncrease)) < 1e-4f;
		if (isWholeNumber && Math.Abs(totalIncrease) >= totalSteps)
		{
			float perTick = MathF.Truncate(totalIncrease / totalSteps);
			return perTick * step;
		}
		return (totalIncrease * step) / totalSteps;
	}
}
