using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class UnitProductionSpec
{
	public string RuleId;
	public string DisplayName;
	public string TargetUnitTypeName;
	public double BaseLifeEnergyCost;
	public double BaseSoulShardCost;
	public double BaseProductionSeconds = 1.0;
	public int PopulationCost = 1;
	public Vector2Int Footprint = Vector2Int.one;
	public int? DedicatedFloor;
	public bool RequiresSpeciesUnlock;
	public bool IsNamed;

	public List<(ResourceType type, double rawCost)> BuildCostList(double multiplier = 1.0, int quantity = 1)
	{
		var list = new List<(ResourceType, double)>(2);
		double m = Math.Max(0.0, multiplier);
		int q = Math.Max(0, quantity);
		if (BaseLifeEnergyCost > 0.0 && q > 0)
			list.Add((ResourceType.LifeEnergy, EconomyMath.CeilCost(BaseLifeEnergyCost * m) * (double)q));
		if (BaseSoulShardCost > 0.0 && q > 0)
			list.Add((ResourceType.SoulShard, EconomyMath.CeilCost(BaseSoulShardCost * m) * (double)q));
		return list;
	}

	public List<(ResourceType type, double rawCost)> BuildSingleUnitRawCosts(double multiplier = 1.0)
	{
		var list = new List<(ResourceType, double)>(2);
		double m = Math.Max(0.0, multiplier);
		if (BaseLifeEnergyCost > 0.0)
			list.Add((ResourceType.LifeEnergy, BaseLifeEnergyCost * m));
		if (BaseSoulShardCost > 0.0)
			list.Add((ResourceType.SoulShard, BaseSoulShardCost * m));
		return list;
	}
}

public enum ProductionEnqueueBlock
{
	None,
	InvalidQuantity,
	SpeciesLocked,
	RoomNotSecured,
	CoreInactive,
	WrongDedicatedFloor,
	SoulShardLocked,
	InsufficientResources,
}

public sealed class ProductionQueueEntry
{
	public int EntryId;
	public long RegistrationOrder;
	public UnitProductionSpec Spec;
	public int WaitingCount;
	public Dictionary<ResourceType, int> ReservedPerWaitingUnit = new Dictionary<ResourceType, int>();

	public bool HasActiveUnit;
	public double ActiveProgress01;
	public Dictionary<ResourceType, int> ActivePaidCosts = new Dictionary<ResourceType, int>();
	public bool Reached100WaitingSpawn;
	public bool IsBlockedWaitingSpawnUI;
	public double FirstReached100Time;

	public int TotalRemainingUnits => WaitingCount + (HasActiveUnit ? 1 : 0);
}

public sealed class FacilityProductionQueue
{
	public const int MinBatchQuantity = 1;
	public const int MaxBatchQuantity = 99;
	public const string WaitingSpawnStatusText = "생산 완료 · 출현 대기";
	public const string WaitingSpawnReasonText = "방 정원 또는 출현 공간이 부족해 유닛이 출현하지 않습니다.";

	private const double Epsilon = 1e-9;
	private static long _globalRegistrationCounter;

	private readonly List<ProductionQueueEntry> _entries = new List<ProductionQueueEntry>();
	private int _nextEntryId = 1;

	public IReadOnlyList<ProductionQueueEntry> Entries => _entries;

	public ProductionQueueEntry HeadEntry => _entries.Count > 0 ? _entries[0] : null;

	public bool HasWaitingSpawnCandidate
		=> _entries.Count > 0 && _entries[0].HasActiveUnit && _entries[0].Reached100WaitingSpawn;

	public static void ResetGlobalRegistrationOrderForTests() => _globalRegistrationCounter = 0;

	public ProductionEnqueueBlock TryEnqueue(
		UnitProductionSpec spec,
		int quantity,
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts,
		bool isSoulShardSpendUnlocked,
		bool isSpeciesUnlocked,
		bool isRoomSecured,
		bool isCoreValid,
		int facilityFloor,
		double reserveCostMultiplier,
		out ProductionQueueEntry createdEntry,
		out string failureMessage)
	{
		createdEntry = null;
		failureMessage = string.Empty;

		if (spec == null || quantity < MinBatchQuantity || quantity > MaxBatchQuantity)
		{
			failureMessage = $"예약 수량은 {MinBatchQuantity}~{MaxBatchQuantity} 사이여야 합니다.";
			return ProductionEnqueueBlock.InvalidQuantity;
		}
		if (spec.RequiresSpeciesUnlock && !isSpeciesUnlocked)
		{
			failureMessage = $"{spec.DisplayName} 종류가 아직 해금되지 않았습니다.";
			return ProductionEnqueueBlock.SpeciesLocked;
		}
		if (!isRoomSecured)
		{
			failureMessage = "확보하지 않은 방에서는 유닛 생산을 예약할 수 없습니다.";
			return ProductionEnqueueBlock.RoomNotSecured;
		}
		if (!isCoreValid)
		{
			failureMessage = "담당 코어가 파괴되어 유닛 생산을 예약할 수 없습니다.";
			return ProductionEnqueueBlock.CoreInactive;
		}
		if (spec.DedicatedFloor.HasValue && spec.DedicatedFloor.Value != facilityFloor)
		{
			failureMessage = $"전용 생산시설은 {spec.DedicatedFloor.Value}층에서만 가동할 수 있습니다.";
			return ProductionEnqueueBlock.WrongDedicatedFloor;
		}

		var perUnitRaw = spec.BuildSingleUnitRawCosts(reserveCostMultiplier);
		var perUnitCeil = ResourceTransaction.AggregateCeilCosts(perUnitRaw);

		var totalBatchCosts = new List<(ResourceType type, double rawCost)>(perUnitCeil.Count);
		foreach (var kv in perUnitCeil)
			totalBatchCosts.Add((kv.Key, (double)kv.Value * quantity));

		if (!ResourceTransaction.TryReserveAll(
			accounts,
			totalBatchCosts,
			isSoulShardSpendUnlocked,
			out _,
			out ResourceType failedType,
			out int req,
			out int avail,
			out bool soulLocked))
		{
			if (soulLocked)
			{
				failureMessage = "최초 코어 완전 활성화 전에는 영혼 파편을 소비할 수 없습니다.";
				return ProductionEnqueueBlock.SoulShardLocked;
			}
			failureMessage = EconomyMath.FormatInsufficientNotice(failedType, req, avail);
			return ProductionEnqueueBlock.InsufficientResources;
		}

		createdEntry = new ProductionQueueEntry
		{
			EntryId = _nextEntryId++,
			RegistrationOrder = ++_globalRegistrationCounter,
			Spec = spec,
			WaitingCount = quantity,
			ReservedPerWaitingUnit = perUnitCeil,
		};
		_entries.Add(createdEntry);
		return ProductionEnqueueBlock.None;
	}

	// 대기(미시작) 수량만 줄인다 — 현재 진행 중 한 마리는 유지하고 줄인 수량의 선점액만 해제한다(9-표1).
	public bool TryReduceWaitingCount(
		int entryId,
		int reduceBy,
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts)
	{
		int idx = FindIndexById(entryId);
		if (idx < 0 || reduceBy <= 0) return false;

		ProductionQueueEntry entry = _entries[idx];
		int actualReduce = Math.Min(reduceBy, entry.WaitingCount);
		if (actualReduce <= 0) return false;

		ReleaseWaitingUnits(entry, actualReduce, accounts);
		entry.WaitingCount -= actualReduce;

		if (entry.WaitingCount <= 0 && !entry.HasActiveUnit)
			_entries.RemoveAt(idx);

		return true;
	}

	// 예약 항목 전체 취소 — 같은 틱에 출현 완료가 확정된 1마리가 있으면 그 1마리와 비용은 유지하고 나머지만 취소한다(9-표1, 각주 [12]).
	public bool CancelEntry(
		int entryId,
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts,
		bool firstUnitSpawnedOnSameTick = false)
	{
		int idx = FindIndexById(entryId);
		if (idx < 0) return false;

		ProductionQueueEntry entry = _entries[idx];
		if (entry.WaitingCount > 0)
		{
			ReleaseWaitingUnits(entry, entry.WaitingCount, accounts);
			entry.WaitingCount = 0;
		}

		if (entry.HasActiveUnit && !firstUnitSpawnedOnSameTick)
		{
			ResourceTransaction.RefundAll(accounts, entry.ActivePaidCosts);
			entry.ActivePaidCosts.Clear();
			entry.HasActiveUnit = false;
			entry.ActiveProgress01 = 0.0;
			entry.Reached100WaitingSpawn = false;
			entry.IsBlockedWaitingSpawnUI = false;
		}
		else if (entry.HasActiveUnit && firstUnitSpawnedOnSameTick)
		{
			// 같은 판정 시점에 유효하게 출현한 1마리는 유지한다.
			entry.ActivePaidCosts.Clear();
			entry.HasActiveUnit = false;
			entry.ActiveProgress01 = 0.0;
			entry.Reached100WaitingSpawn = false;
			entry.IsBlockedWaitingSpawnUI = false;
		}

		_entries.RemoveAt(idx);
		return true;
	}

	// 순서 변경(9-표1, 9-1-표1): 진행 중 선두 항목이 뒤로 밀리면 현재 한 마리 진행률을 삭제·전액 환급하고,
	// 그 한 마리를 다시 대기 수량에 합쳐 선점액을 유지한다. 대기 항목끼리만 순서를 바꾸면 현재 생산은 그대로 유지한다.
	public bool TryMoveEntry(
		int entryId,
		int targetIndex,
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts,
		bool isSoulShardSpendUnlocked = true)
	{
		int oldIndex = FindIndexById(entryId);
		if (oldIndex < 0 || _entries.Count <= 1) return false;
		targetIndex = Math.Clamp(targetIndex, 0, _entries.Count - 1);
		if (oldIndex == targetIndex) return false;

		ProductionQueueEntry prevHead = _entries[0];

		ProductionQueueEntry moving = _entries[oldIndex];
		_entries.RemoveAt(oldIndex);
		_entries.Insert(targetIndex, moving);

		ProductionQueueEntry newHead = _entries[0];
		if (prevHead != newHead && prevHead.HasActiveUnit)
		{
			// 진행 중이던 선두가 뒤로 밀림 -> 지불액 환급 후 대기 수량 +1 & 선점 복원
			ResourceTransaction.RefundAll(accounts, prevHead.ActivePaidCosts);

			var reReserveCosts = new List<(ResourceType type, double rawCost)>(prevHead.ReservedPerWaitingUnit.Count);
			foreach (var kv in prevHead.ReservedPerWaitingUnit)
				reReserveCosts.Add((kv.Key, kv.Value));

			if (!ResourceTransaction.TryReserveAll(
				accounts,
				reReserveCosts,
				isSoulShardSpendUnlocked,
				out _,
				out _,
				out _,
				out _,
				out _))
			{
				// 환급 직후라 일반적으로 항상 성공하지만, 실패 시 원위치 복구
				_entries.RemoveAt(targetIndex);
				_entries.Insert(oldIndex, moving);
				return false;
			}

			prevHead.ActivePaidCosts.Clear();
			prevHead.HasActiveUnit = false;
			prevHead.ActiveProgress01 = 0.0;
			prevHead.Reached100WaitingSpawn = false;
			prevHead.IsBlockedWaitingSpawnUI = false;
			prevHead.FirstReached100Time = 0.0;
			prevHead.WaitingCount += 1;
		}

		return true;
	}

	// 한 마리 시작과 생산 시간 진행 — 출현은 SpawnArbitrator가 담당한다(7-표1, 8-표2, 10-표1).
	public void TickProduction(
		double deltaSeconds,
		double currentGameTime,
		bool isFacilityOperational,
		double productionSpeedMultiplier,
		double startCostMultiplier,
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts)
	{
		CleanupLeadingEmptyEntries();
		if (!isFacilityOperational || _entries.Count == 0) return;

		ProductionQueueEntry head = _entries[0];

		if (!head.HasActiveUnit)
		{
			if (head.WaitingCount <= 0)
			{
				_entries.RemoveAt(0);
				return;
			}

			var actualStartCosts = head.Spec.BuildSingleUnitRawCosts(startCostMultiplier);
			if (!ResourceTransaction.CommitAll(accounts, head.ReservedPerWaitingUnit, actualStartCosts, out var paid))
				return;

			head.WaitingCount -= 1;
			head.HasActiveUnit = true;
			head.ActiveProgress01 = 0.0;
			head.ActivePaidCosts = paid;
			head.Reached100WaitingSpawn = false;
			head.IsBlockedWaitingSpawnUI = false;
			head.FirstReached100Time = 0.0;
		}

		if (head.Reached100WaitingSpawn)
			return;

		if (deltaSeconds <= 0.0 || productionSpeedMultiplier <= 0.0)
			return;

		double baseDuration = Math.Max(0.001, head.Spec.BaseProductionSeconds);
		double prevProgress = head.ActiveProgress01;
		double deltaProgress = (deltaSeconds * productionSpeedMultiplier) / baseDuration;
		double nextProgress = prevProgress + deltaProgress;

		if (nextProgress + Epsilon >= 1.0)
		{
			double neededProgress = Math.Max(0.0, 1.0 - prevProgress);
			double timeUsed = deltaProgress > Epsilon ? deltaSeconds * (neededProgress / deltaProgress) : 0.0;
			double startOfTickTime = currentGameTime - deltaSeconds;

			head.ActiveProgress01 = 1.0;
			head.Reached100WaitingSpawn = true;
			head.FirstReached100Time = startOfTickTime + timeUsed;
		}
		else
		{
			head.ActiveProgress01 = nextProgress;
		}
	}

	// 출현 중재자가 정원·인접 공간을 확인하고 실제 유닛을 출현시킨 직후 호출한다.
	public UnitProductionSpec CompleteActiveSpawn()
	{
		if (_entries.Count == 0) return null;
		ProductionQueueEntry head = _entries[0];
		if (!head.HasActiveUnit || !head.Reached100WaitingSpawn) return null;

		UnitProductionSpec spawnedSpec = head.Spec;
		head.ActivePaidCosts.Clear();
		head.HasActiveUnit = false;
		head.ActiveProgress01 = 0.0;
		head.Reached100WaitingSpawn = false;
		head.IsBlockedWaitingSpawnUI = false;
		head.FirstReached100Time = 0.0;

		if (head.WaitingCount <= 0)
			_entries.RemoveAt(0);

		return spawnedSpec;
	}

	// 시설 파괴(HP 0): 진행 중(100% 출현 대기 포함) 미출현 유닛과 지불 비용은 손실, 미시작 예약은 선점 해제(9-표1, 23-표1).
	public void OnFacilityDestroyed(IReadOnlyDictionary<ResourceType, ResourceAccount> accounts)
	{
		for (int i = 0; i < _entries.Count; i++)
		{
			ProductionQueueEntry entry = _entries[i];
			if (entry.WaitingCount > 0)
				ReleaseWaitingUnits(entry, entry.WaitingCount, accounts);
		}
		_entries.Clear();
	}

	// 플레이어 직접 철거 완료: 미출현 현재 1마리의 지불액도 전액 환급하고 미시작 선점액도 해제한다(20-1-표1).
	public void OnFacilityDemolished(IReadOnlyDictionary<ResourceType, ResourceAccount> accounts)
	{
		for (int i = 0; i < _entries.Count; i++)
		{
			ProductionQueueEntry entry = _entries[i];
			if (entry.WaitingCount > 0)
				ReleaseWaitingUnits(entry, entry.WaitingCount, accounts);
			if (entry.HasActiveUnit)
				ResourceTransaction.RefundAll(accounts, entry.ActivePaidCosts);
		}
		_entries.Clear();
	}

	private void CleanupLeadingEmptyEntries()
	{
		while (_entries.Count > 0 && !_entries[0].HasActiveUnit && _entries[0].WaitingCount <= 0)
			_entries.RemoveAt(0);
	}

	private int FindIndexById(int entryId)
	{
		for (int i = 0; i < _entries.Count; i++)
			if (_entries[i].EntryId == entryId) return i;
		return -1;
	}

	private static void ReleaseWaitingUnits(
		ProductionQueueEntry entry,
		int count,
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts)
	{
		if (count <= 0 || entry.ReservedPerWaitingUnit == null) return;
		foreach (var kv in entry.ReservedPerWaitingUnit)
		{
			if (kv.Value > 0 && accounts.TryGetValue(kv.Key, out var acc))
				acc.ReleaseReservation(kv.Value * count);
		}
	}
}

public sealed class RoomSpawnFacilityHandle
{
	public int FacilityId;
	public Vector2Int FacilityOrigin;
	public Vector2Int FacilityFootprint;
	public bool IsFacilityOperational;
	public FacilityProductionQueue Queue;
}

public sealed class RoomPopulationContext
{
	public int LivingPlayerPopulation;
	public int ConfirmedIncomingMovePopulation;
	public int MaxPopulation;

	public bool CanFitPopulation(int additionalPop)
		=> LivingPlayerPopulation + ConfirmedIncomingMovePopulation + additionalPop <= MaxPopulation;
}

public readonly struct SpawnResolutionResult
{
	public readonly int FacilityId;
	public readonly UnitProductionSpec Spec;
	public readonly Vector2Int SpawnOrigin;

	public SpawnResolutionResult(int facilityId, UnitProductionSpec spec, Vector2Int spawnOrigin)
	{
		FacilityId = facilityId;
		Spec = spec;
		SpawnOrigin = spawnOrigin;
	}
}

// 같은 방 여러 생산시설의 100% 도달 후보를 중재하여 출현 가능한 순서대로 한 마리씩 배출한다(명세서 v0.2 10-표3).
public static class SpawnArbitrator
{
	private struct ReadyCandidate
	{
		public RoomSpawnFacilityHandle Handle;
		public ProductionQueueEntry Entry;
		public Vector2Int SpawnPos;
	}

	public static List<SpawnResolutionResult> ResolveRoomSpawns(
		IReadOnlyList<RoomSpawnFacilityHandle> facilitiesInRoom,
		RoomPopulationContext roomPop,
		Func<Vector2Int, Vector2Int, bool> isAdjacentFootprintClear,
		Action<SpawnResolutionResult> onSingleSpawnCommitted = null)
	{
		var results = new List<SpawnResolutionResult>();
		if (facilitiesInRoom == null || facilitiesInRoom.Count == 0 || roomPop == null || isAdjacentFootprintClear == null)
			return results;

		while (true)
		{
			var spawnable = new List<ReadyCandidate>();

			for (int i = 0; i < facilitiesInRoom.Count; i++)
			{
				RoomSpawnFacilityHandle fh = facilitiesInRoom[i];
				if (fh?.Queue == null || !fh.Queue.HasWaitingSpawnCandidate)
					continue;

				ProductionQueueEntry head = fh.Queue.HeadEntry;
				if (!fh.IsFacilityOperational)
				{
					// 시설 손상·코어 파괴 중에는 가동 정지 사유이므로 출현 불가.
					continue;
				}

				bool popOk = roomPop.CanFitPopulation(head.Spec.PopulationCost);
				bool spaceOk = TryFindOrthogonalSpawnOrigin(
					fh.FacilityOrigin,
					fh.FacilityFootprint,
					head.Spec.Footprint,
					isAdjacentFootprintClear,
					out Vector2Int chosenPos);

				if (!popOk || !spaceOk)
				{
					head.IsBlockedWaitingSpawnUI = true;
					continue;
				}

				spawnable.Add(new ReadyCandidate
				{
					Handle = fh,
					Entry = head,
					SpawnPos = chosenPos,
				});
			}

			if (spawnable.Count == 0)
				break;

			// 100% 최초 도달 시점 오름차순 -> 동률이면 예약 등록 순서 오름차순(10-표3 ③)
			spawnable.Sort((a, b) =>
			{
				int cmp = a.Entry.FirstReached100Time.CompareTo(b.Entry.FirstReached100Time);
				if (cmp != 0) return cmp;
				return a.Entry.RegistrationOrder.CompareTo(b.Entry.RegistrationOrder);
			});

			ReadyCandidate winner = spawnable[0];
			UnitProductionSpec spawnedSpec = winner.Handle.Queue.CompleteActiveSpawn();
			if (spawnedSpec == null)
				break;

			roomPop.LivingPlayerPopulation += spawnedSpec.PopulationCost;
			var res = new SpawnResolutionResult(winner.Handle.FacilityId, spawnedSpec, winner.SpawnPos);
			results.Add(res);
			onSingleSpawnCommitted?.Invoke(res);
		}

		return results;
	}

	public static bool TryFindOrthogonalSpawnOrigin(
		Vector2Int facilityOrigin,
		Vector2Int facilityFootprint,
		Vector2Int unitFootprint,
		Func<Vector2Int, Vector2Int, bool> isFootprintClear,
		out Vector2Int spawnOrigin)
	{
		var candidates = WorkSpeedMath.GetOrthogonalAdjacentOrigins(facilityOrigin, facilityFootprint, unitFootprint);
		for (int i = 0; i < candidates.Count; i++)
		{
			if (isFootprintClear(candidates[i], unitFootprint))
			{
				spawnOrigin = candidates[i];
				return true;
			}
		}
		spawnOrigin = default;
		return false;
	}
}
