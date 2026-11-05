using System;
using System.Collections.Generic;

public enum ResourceType
{
	LifeEnergy,
	SoulShard,
}

// 외부 획득(누적 획득량 포함)과 내부 정산·회수·환불(누적 획득량 미포함)을 구분한다(명세서 v0.2 1·2·14·20-1·24-2항).
public enum ResourceCreditSource
{
	InitialGrant,
	FacilityProduction,
	ExternalCorpseRefine,
	AllyCorpseRefine,
	QuestReward,
	ExternalFacilityDemolish,
	SelfFacilityDemolishRecovery,
	ManualCancelRefund,
	DebugGrant,
}

[Serializable]
public struct ResourceAccountSnapshot
{
	public ResourceType Type;
	public double CurrentBalance;
	public int ReservedAmount;
	public double CumulativeEarned;
}

public static class EconomyMath
{
	private const double FloatTolerance = 1e-9;

	public static string GetDisplayName(ResourceType type) => type switch
	{
		ResourceType.LifeEnergy => "생명 에너지",
		ResourceType.SoulShard => "영혼 파편",
		_ => type.ToString(),
	};

	public static bool CountsTowardCumulativeEarned(ResourceCreditSource source) => source switch
	{
		ResourceCreditSource.FacilityProduction => true,
		ResourceCreditSource.ExternalCorpseRefine => true,
		ResourceCreditSource.QuestReward => true,
		ResourceCreditSource.ExternalFacilityDemolish => true,
		_ => false,
	};

	// 소수 비용의 선점·지불 기준은 정수 올림한다(각주 [3], 24-표1: 7.2 -> 8).
	public static int CeilCost(double rawCost)
	{
		if (rawCost <= 0.0) return 0;
		return (int)Math.Ceiling(rawCost - FloatTolerance);
	}

	// 보유량·사용 가능량 UI 표시는 정수 버림한다(1-표2, 2-표2, 3-표2).
	public static int FloorDisplay(double amount)
	{
		if (amount <= 0.0) return 0;
		return (int)Math.Floor(amount + FloatTolerance);
	}

	public static string FormatResourceDisplay(double currentBalance, int reservedAmount)
	{
		int floorBalance = FloorDisplay(currentBalance);
		return reservedAmount > 0 ? $"{floorBalance} (-{reservedAmount})" : $"{floorBalance}";
	}

	public static string FormatInsufficientNotice(ResourceType type, int required, int available)
		=> $"{GetDisplayName(type)} 자원이 부족합니다. (필요 {required} / 사용 가능 {available})";
}

// 단일 자원 계정 — 소수 내부 잔고, 선배정 예약액, 외부 누적 획득량을 한 곳에서 관리한다(명세서 v0.2 1~3·22항).
public sealed class ResourceAccount
{
	private const double Epsilon = 1e-9;

	public ResourceType Type { get; }
	public double CurrentBalance { get; private set; }
	public int ReservedAmount { get; private set; }
	public double CumulativeEarned { get; private set; }

	public int DisplayBalance => EconomyMath.FloorDisplay(CurrentBalance);
	public double AvailableBalance => Math.Max(0.0, CurrentBalance - ReservedAmount);
	public int DisplayAvailable => EconomyMath.FloorDisplay(AvailableBalance);

	public ResourceAccount(ResourceType type, double initialAmount = 0.0)
	{
		Type = type;
		CurrentBalance = Math.Max(0.0, initialAmount);
		ReservedAmount = 0;
		CumulativeEarned = 0.0;
	}

	public bool CanAffordRaw(double rawCost)
	{
		int needed = EconomyMath.CeilCost(rawCost);
		return CanAffordInt(needed);
	}

	public bool CanAffordInt(int ceilCost)
	{
		if (ceilCost <= 0) return true;
		return CurrentBalance - ReservedAmount + Epsilon >= ceilCost;
	}

	// 등록·명령 확정 시점에 필요량을 선배정으로 묶는다(실제 잔고는 작업 시작 전까지 유지).
	public bool TryReserve(double rawCost, out int reservedInt)
	{
		reservedInt = EconomyMath.CeilCost(rawCost);
		if (reservedInt <= 0) return true;
		if (!CanAffordInt(reservedInt))
		{
			reservedInt = 0;
			return false;
		}
		ReservedAmount += reservedInt;
		return true;
	}

	public void ReleaseReservation(int reservedInt)
	{
		if (reservedInt <= 0) return;
		ReservedAmount = Math.Max(0, ReservedAmount - reservedInt);
	}

	// 실제 작업 시작 시점에 선점액을 풀고 그 시점의 유효 비용(할인 등 반영)만 1회 차감한다(3-표1).
	public bool CommitReservedCost(int reservedInt, double actualRawCostAtStart, out int actualPaid)
	{
		actualPaid = EconomyMath.CeilCost(actualRawCostAtStart);
		int prevReserved = ReservedAmount;
		ReservedAmount = Math.Max(0, ReservedAmount - Math.Max(0, reservedInt));

		if (actualPaid > 0 && CurrentBalance - ReservedAmount + Epsilon < actualPaid)
		{
			ReservedAmount = prevReserved;
			actualPaid = 0;
			return false;
		}

		if (actualPaid > 0)
			CurrentBalance = Math.Max(0.0, CurrentBalance - actualPaid);
		return true;
	}

	// 선배정 없이 클릭·시작 즉시 차감하는 경우(개별 진화, 수리·수복 추가 차액 지불 등).
	public bool TrySpendImmediate(double rawCost, out int actualPaid)
	{
		actualPaid = EconomyMath.CeilCost(rawCost);
		if (actualPaid <= 0) return true;
		if (!CanAffordInt(actualPaid))
		{
			actualPaid = 0;
			return false;
		}
		CurrentBalance = Math.Max(0.0, CurrentBalance - actualPaid);
		return true;
	}

	public void Credit(double amount, ResourceCreditSource source)
	{
		if (amount <= 0.0) return;
		CurrentBalance += amount;
		if (EconomyMath.CountsTowardCumulativeEarned(source))
			CumulativeEarned += amount;
	}

	public void RefundPaid(int paidAmount)
	{
		if (paidAmount <= 0) return;
		Credit(paidAmount, ResourceCreditSource.ManualCancelRefund);
	}

	public void DebugSetBalance(double nextBalance)
	{
		CurrentBalance = Math.Max(0.0, nextBalance);
		if (ReservedAmount > EconomyMath.FloorDisplay(CurrentBalance))
			ReservedAmount = EconomyMath.FloorDisplay(CurrentBalance);
	}

	public void ResetForDebug(double initialBalance)
	{
		CurrentBalance = Math.Max(0.0, initialBalance);
		ReservedAmount = 0;
		CumulativeEarned = 0.0;
	}

	public string FormatDisplay() => EconomyMath.FormatResourceDisplay(CurrentBalance, ReservedAmount);

	public ResourceAccountSnapshot CaptureSnapshot() => new ResourceAccountSnapshot
	{
		Type = Type,
		CurrentBalance = CurrentBalance,
		ReservedAmount = ReservedAmount,
		CumulativeEarned = CumulativeEarned,
	};

	public void RestoreSnapshot(ResourceAccountSnapshot snapshot)
	{
		CurrentBalance = Math.Max(0.0, snapshot.CurrentBalance);
		ReservedAmount = Math.Max(0, snapshot.ReservedAmount);
		CumulativeEarned = Math.Max(0.0, snapshot.CumulativeEarned);
	}
}

// 다중 자원 비용 묶음의 원자적 검사·선점·차감·환불 도우미.
public static class ResourceTransaction
{
	public static bool CanReserveAll(
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts,
		IReadOnlyList<(ResourceType type, double rawCost)> costs,
		bool isSoulShardSpendUnlocked,
		out ResourceType failedType,
		out int requiredAmount,
		out int availableAmount,
		out bool blockedBySoulLock)
	{
		failedType = ResourceType.LifeEnergy;
		requiredAmount = 0;
		availableAmount = 0;
		blockedBySoulLock = false;

		if (costs == null || costs.Count == 0) return true;

		var aggregated = AggregateCeilCosts(costs);
		foreach (var kv in aggregated)
		{
			if (kv.Value <= 0) continue;
			if (kv.Key == ResourceType.SoulShard && !isSoulShardSpendUnlocked)
			{
				failedType = ResourceType.SoulShard;
				requiredAmount = kv.Value;
				availableAmount = accounts.TryGetValue(kv.Key, out var acc0) ? acc0.DisplayAvailable : 0;
				blockedBySoulLock = true;
				return false;
			}
			if (!accounts.TryGetValue(kv.Key, out var account) || !account.CanAffordInt(kv.Value))
			{
				failedType = kv.Key;
				requiredAmount = kv.Value;
				availableAmount = account != null ? account.DisplayAvailable : 0;
				return false;
			}
		}
		return true;
	}

	public static bool TryReserveAll(
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts,
		IReadOnlyList<(ResourceType type, double rawCost)> costs,
		bool isSoulShardSpendUnlocked,
		out Dictionary<ResourceType, int> reservedByType,
		out ResourceType failedType,
		out int requiredAmount,
		out int availableAmount,
		out bool blockedBySoulLock)
	{
		reservedByType = null;
		if (!CanReserveAll(accounts, costs, isSoulShardSpendUnlocked, out failedType, out requiredAmount, out availableAmount, out blockedBySoulLock))
			return false;

		reservedByType = AggregateCeilCosts(costs);
		foreach (var kv in reservedByType)
		{
			if (kv.Value <= 0) continue;
			accounts[kv.Key].TryReserve(kv.Value, out _);
		}
		return true;
	}

	public static void ReleaseAll(
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts,
		IReadOnlyDictionary<ResourceType, int> reservedByType)
	{
		if (reservedByType == null) return;
		foreach (var kv in reservedByType)
		{
			if (kv.Value > 0 && accounts.TryGetValue(kv.Key, out var acc))
				acc.ReleaseReservation(kv.Value);
		}
	}

	public static bool CommitAll(
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts,
		IReadOnlyDictionary<ResourceType, int> reservedByType,
		IReadOnlyList<(ResourceType type, double actualRawCost)> actualCosts,
		out Dictionary<ResourceType, int> paidByType)
	{
		paidByType = new Dictionary<ResourceType, int>();
		var actualCeils = AggregateCeilCosts(actualCosts);

		var allTypes = new HashSet<ResourceType>();
		if (reservedByType != null) foreach (var k in reservedByType.Keys) allTypes.Add(k);
		foreach (var k in actualCeils.Keys) allTypes.Add(k);

		foreach (var t in allTypes)
		{
			int res = reservedByType != null && reservedByType.TryGetValue(t, out int r) ? r : 0;
			int act = actualCeils.TryGetValue(t, out int a) ? a : 0;
			if (!accounts.TryGetValue(t, out var acc)) return false;
			if (act > res && !acc.CanAffordInt(act - res)) return false;
		}

		foreach (var t in allTypes)
		{
			int res = reservedByType != null && reservedByType.TryGetValue(t, out int r) ? r : 0;
			int act = actualCeils.TryGetValue(t, out int a) ? a : 0;
			accounts[t].CommitReservedCost(res, act, out int paid);
			if (paid > 0) paidByType[t] = paid;
		}
		return true;
	}

	public static void RefundAll(
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts,
		IReadOnlyDictionary<ResourceType, int> paidByType)
	{
		if (paidByType == null) return;
		foreach (var kv in paidByType)
		{
			if (kv.Value > 0 && accounts.TryGetValue(kv.Key, out var acc))
				acc.RefundPaid(kv.Value);
		}
	}

	public static Dictionary<ResourceType, int> AggregateCeilCosts(IReadOnlyList<(ResourceType type, double rawCost)> costs)
	{
		var rawSums = new Dictionary<ResourceType, double>();
		if (costs != null)
		{
			for (int i = 0; i < costs.Count; i++)
			{
				var (type, raw) = costs[i];
				if (raw <= 0.0) continue;
				rawSums[type] = rawSums.TryGetValue(type, out double prev) ? prev + raw : raw;
			}
		}
		var result = new Dictionary<ResourceType, int>();
		foreach (var kv in rawSums)
		{
			int ceil = EconomyMath.CeilCost(kv.Value);
			if (ceil > 0) result[kv.Key] = ceil;
		}
		return result;
	}
}
