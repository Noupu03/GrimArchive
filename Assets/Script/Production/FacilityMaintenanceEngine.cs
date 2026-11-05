using System;
using System.Collections.Generic;
using UnityEngine;

// 한 시설에서는 건설·업그레이드·수리·이동용 분해·철거 중 동시에 하나의 현장 작업만 수행한다(명세서 v0.2 16항).
public enum FacilityFieldTaskKind
{
	None,
	Construction,
	Upgrade,
	Repair,
	Relocation,
	Demolish,
}

public enum FacilityOriginKind
{
	PlayerBuilt,
	PrePlacedOrCapturedDungeon,
	HumanStructure,
}

// ── 건설 · 개별 시설 업그레이드 (명세서 v0.2 16항) ──

public sealed class ConstructionOrUpgradeSession
{
	private const double Epsilon = 1e-9;

	public bool IsUpgrade { get; }
	public double RequiredWorkSeconds { get; }
	public float TargetCompletedMaxHp { get; }
	public Dictionary<ResourceType, int> ReservedBeforeStart { get; private set; }
	public Dictionary<ResourceType, int> PaidCosts { get; private set; } = new Dictionary<ResourceType, int>();
	public bool HasStartedWork { get; private set; }
	public double CompletedWorkSeconds { get; private set; }
	public bool IsCompleted { get; private set; }
	public bool IsEnded { get; private set; }

	public double Progress01 => RequiredWorkSeconds > 0.0
		? Math.Clamp(CompletedWorkSeconds / RequiredWorkSeconds, 0.0, 1.0)
		: 1.0;

	private ConstructionOrUpgradeSession(
		bool isUpgrade,
		double requiredWorkSeconds,
		float targetCompletedMaxHp,
		Dictionary<ResourceType, int> reserved)
	{
		IsUpgrade = isUpgrade;
		RequiredWorkSeconds = Math.Max(0.001, requiredWorkSeconds);
		TargetCompletedMaxHp = Math.Max(1f, targetCompletedMaxHp);
		ReservedBeforeStart = reserved ?? new Dictionary<ResourceType, int>();
	}

	public static bool TryCreateOrder(
		bool isUpgrade,
		IReadOnlyList<(ResourceType type, double rawCost)> rawCosts,
		double requiredWorkSeconds,
		float targetCompletedMaxHp,
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts,
		bool isSoulShardSpendUnlocked,
		bool isCoreActive,
		out ConstructionOrUpgradeSession session,
		out string failureMessage)
	{
		session = null;
		failureMessage = string.Empty;

		if (!isCoreActive)
		{
			failureMessage = "담당 코어가 파괴되어 작업을 명령할 수 없습니다.";
			return false;
		}

		if (!ResourceTransaction.TryReserveAll(
			accounts,
			rawCosts,
			isSoulShardSpendUnlocked,
			out var reserved,
			out ResourceType failedType,
			out int req,
			out int avail,
			out bool soulLocked))
		{
			failureMessage = soulLocked
				? "최초 코어 완전 활성화 전에는 영혼 파편을 소비할 수 없습니다."
				: EconomyMath.FormatInsufficientNotice(failedType, req, avail);
			return false;
		}

		session = new ConstructionOrUpgradeSession(isUpgrade, requiredWorkSeconds, targetCompletedMaxHp, reserved);
		return true;
	}

	// 건설은 작업자 도착 + 착공 공간 확보 시점에, 업그레이드는 작업자 첫 작업 시작 시점에 선점을 차감으로 전환한다(16-표2).
	// 같은 판정 시점에 피격이 함께 들어온 경우 피해를 먼저 반영해 생존 여부를 확인한 뒤 완공 HP 비율을 승계한다(16-표1, 24-표1).
	public bool Tick(
		double deltaSeconds,
		IReadOnlyList<float> activeWorkerSpeeds,
		bool isCoreActive,
		bool isSiteClearToStart,
		IReadOnlyList<(ResourceType type, double actualRawCost)> startActualCosts,
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts,
		ref float targetCurrentHp,
		ref float targetCurrentMaxHp,
		float sameTickDamage = 0f)
	{
		if (IsEnded) return false;

		if (sameTickDamage > 0f)
			targetCurrentHp = Math.Max(0f, targetCurrentHp - sameTickDamage);

		if (targetCurrentHp <= 0f)
		{
			// 대상 파괴: 미시작 선점액이 남아 있으면 해제하고, 이미 지불한 비용은 환급 없이 소멸한다(16-표1, 24-표1).
			if (!HasStartedWork && ReservedBeforeStart.Count > 0)
			{
				ResourceTransaction.ReleaseAll(accounts, ReservedBeforeStart);
				ReservedBeforeStart.Clear();
			}
			IsEnded = true;
			return false;
		}

		// 코어 파괴 시 건설·업그레이드는 진행과 지불 상태를 보존한 채 정지한다(16-표1, 24-표1).
		if (!isCoreActive) return false;

		float coopSpeed = WorkSpeedMath.CalculateTotalWorkSpeed(activeWorkerSpeeds);
		if (coopSpeed <= 0f) return false;

		if (!HasStartedWork)
		{
			if (!isSiteClearToStart) return false;
			if (!ResourceTransaction.CommitAll(accounts, ReservedBeforeStart, startActualCosts, out var paid))
				return false;

			ReservedBeforeStart.Clear();
			PaidCosts = paid;
			HasStartedWork = true;
		}

		if (deltaSeconds <= 0.0) return false;

		CompletedWorkSeconds += deltaSeconds * coopSpeed;
		if (CompletedWorkSeconds + Epsilon >= RequiredWorkSeconds)
		{
			CompletedWorkSeconds = RequiredWorkSeconds;
			float hpRatio = targetCurrentMaxHp > 0f ? Mathf.Clamp01(targetCurrentHp / targetCurrentMaxHp) : 1f;
			targetCurrentMaxHp = TargetCompletedMaxHp;
			targetCurrentHp = TargetCompletedMaxHp * hpRatio; // 직전 HP 비율 승계(각주 [23])
			IsCompleted = true;
			IsEnded = true;
			return true;
		}

		return false;
	}

	public void CancelManually(IReadOnlyDictionary<ResourceType, ResourceAccount> accounts)
	{
		if (IsEnded) return;
		if (!HasStartedWork)
		{
			ResourceTransaction.ReleaseAll(accounts, ReservedBeforeStart);
			ReservedBeforeStart.Clear();
		}
		else
		{
			ResourceTransaction.RefundAll(accounts, PaidCosts);
		}
		IsEnded = true;
	}
}

// ── 일반 수리 (명세서 v0.2 17~18항) ──

public sealed class RepairSession
{
	private const double Epsilon = 1e-9;

	public double FullRepairBaseCost { get; }
	public double FullRepairBaseSeconds { get; }
	public float TargetMaxHp { get; }

	public bool HasStartedWork { get; private set; }
	public int ReservedBeforeStart { get; private set; }
	public int PaidTotalSoFar { get; private set; }
	public int TotalRequiredCeilCost { get; private set; }
	public double TotalRequiredWorkSeconds { get; private set; }
	public double CompletedWorkSeconds { get; private set; }
	public bool IsCompleted { get; private set; }
	public bool IsDestroyed { get; private set; }
	public bool IsCancelled { get; private set; }
	public bool IsEnded => IsCompleted || IsDestroyed || IsCancelled;

	// 착수 전에는 추가 선점 필요분, 착수 후에는 추가 지불 필요분이다(18-1-표2).
	public int UnapprovedDiffAmount => HasStartedWork
		? Math.Max(0, TotalRequiredCeilCost - PaidTotalSoFar)
		: Math.Max(0, TotalRequiredCeilCost - ReservedBeforeStart);

	public bool IsPausedWaitingDiffApproval => UnapprovedDiffAmount > 0;

	public double Progress01 => TotalRequiredWorkSeconds > 0.0
		? Math.Clamp(CompletedWorkSeconds / TotalRequiredWorkSeconds, 0.0, 1.0)
		: 1.0;

	private RepairSession(
		double fullRepairBaseCost,
		double fullRepairBaseSeconds,
		float targetMaxHp,
		int initialRequiredCost,
		double initialRequiredWork)
	{
		FullRepairBaseCost = Math.Max(0.0, fullRepairBaseCost);
		FullRepairBaseSeconds = Math.Max(0.001, fullRepairBaseSeconds);
		TargetMaxHp = Math.Max(1f, targetMaxHp);
		TotalRequiredCeilCost = initialRequiredCost;
		ReservedBeforeStart = initialRequiredCost;
		TotalRequiredWorkSeconds = Math.Max(0.001, initialRequiredWork);
	}

	public static bool TryCreateOrder(
		float currentHp,
		float maxHp,
		double fullRepairBaseCost,
		double fullRepairBaseSeconds,
		ResourceAccount lifeEnergyAccount,
		bool isCoreActive,
		out RepairSession session,
		out string failureMessage)
	{
		session = null;
		failureMessage = string.Empty;

		if (!isCoreActive)
		{
			failureMessage = "담당 코어가 파괴되어 새 수리를 시작할 수 없습니다.";
			return false;
		}
		if (currentHp <= 0f || maxHp <= 0f || currentHp >= maxHp - 1e-4f)
		{
			failureMessage = "수리가 필요한 손상 상태가 아닙니다.";
			return false;
		}

		double damageRatio = Math.Clamp((maxHp - currentHp) / (double)maxHp, 0.0, 1.0);
		int initialCost = EconomyMath.CeilCost(fullRepairBaseCost * damageRatio);
		double initialWork = fullRepairBaseSeconds * damageRatio;

		if (lifeEnergyAccount == null || !lifeEnergyAccount.TryReserve(initialCost, out int reserved))
		{
			int avail = lifeEnergyAccount != null ? lifeEnergyAccount.DisplayAvailable : 0;
			failureMessage = EconomyMath.FormatInsufficientNotice(ResourceType.LifeEnergy, initialCost, avail);
			return false;
		}

		session = new RepairSession(fullRepairBaseCost, fullRepairBaseSeconds, maxHp, reserved, initialWork);
		return true;
	}

	// 직접 작업량·비용을 지정해 수리 세션을 여는 검증·특수 상황용 생성자(24-표1 수치 검증 지원).
	public static RepairSession CreateDirectStartedForVerification(
		int initialPaidCost,
		double initialRequiredWorkSeconds,
		ResourceAccount lifeEnergyAccount)
	{
		lifeEnergyAccount?.TrySpendImmediate(initialPaidCost, out _);
		var s = new RepairSession(initialPaidCost, initialRequiredWorkSeconds, 100f, 0, initialRequiredWorkSeconds)
		{
			HasStartedWork = true,
			ReservedBeforeStart = 0,
			PaidTotalSoFar = initialPaidCost,
			TotalRequiredCeilCost = initialPaidCost,
		};
		return s;
	}

	// 추가 피해 발생 시 총 필요 비용·작업량을 갱신하고 기존 완료 작업량은 유지한다(18-1-표1, 각주 [25]).
	public void ApplyDamageAndRecalculate(float newCurrentHp, ResourceAccount lifeEnergyAccountIfDestroyed = null)
	{
		if (IsEnded) return;
		if (newCurrentHp <= 0f)
		{
			if (!HasStartedWork && ReservedBeforeStart > 0)
			{
				lifeEnergyAccountIfDestroyed?.ReleaseReservation(ReservedBeforeStart);
				ReservedBeforeStart = 0;
			}
			IsDestroyed = true;
			return;
		}

		double damageRatio = Math.Clamp((TargetMaxHp - newCurrentHp) / (double)TargetMaxHp, 0.0, 1.0);
		int updatedCost = EconomyMath.CeilCost(FullRepairBaseCost * damageRatio);
		double updatedWork = FullRepairBaseSeconds * damageRatio;

		if (updatedCost > TotalRequiredCeilCost)
			TotalRequiredCeilCost = updatedCost;
		if (updatedWork > TotalRequiredWorkSeconds)
			TotalRequiredWorkSeconds = updatedWork;
	}

	public void ApplyDirectExtraRequirement(int newTotalRequiredCeilCost, double newTotalRequiredWorkSeconds)
	{
		if (IsEnded) return;
		if (newTotalRequiredCeilCost > TotalRequiredCeilCost)
			TotalRequiredCeilCost = newTotalRequiredCeilCost;
		if (newTotalRequiredWorkSeconds > TotalRequiredWorkSeconds)
			TotalRequiredWorkSeconds = newTotalRequiredWorkSeconds;
	}

	// 플레이어가 추가 수리비 차액을 직접 승인·지불한다(자원이 들어와도 자동 호출되지 않음, 18-1-표2, 24-표1).
	public bool TryApproveExtraDiffManually(ResourceAccount lifeEnergyAccount, out string failureMessage)
	{
		failureMessage = string.Empty;
		if (IsEnded) return false;

		int diff = UnapprovedDiffAmount;
		if (diff <= 0) return true;

		if (lifeEnergyAccount == null || !lifeEnergyAccount.CanAffordInt(diff))
		{
			int avail = lifeEnergyAccount != null ? lifeEnergyAccount.DisplayAvailable : 0;
			failureMessage = EconomyMath.FormatInsufficientNotice(ResourceType.LifeEnergy, diff, avail);
			return false;
		}

		if (!HasStartedWork)
		{
			lifeEnergyAccount.TryReserve(diff, out int addedReserve);
			ReservedBeforeStart += addedReserve;
		}
		else
		{
			lifeEnergyAccount.TrySpendImmediate(diff, out int paidDiff);
			PaidTotalSoFar += paidDiff;
		}
		return true;
	}

	// 수리 진행 — 아직 시작 전이면 코어가 유효해야 착수하나, 이미 시작한 일반 수리는 코어가 파괴되어도 계속한다(18-1항, 각주 [27], 24-표1).
	public bool Tick(
		double deltaSeconds,
		IReadOnlyList<float> activeWorkerSpeeds,
		bool isCoreActive,
		ResourceAccount lifeEnergyAccount,
		ref float currentHp,
		float sameTickExtraDamage = 0f)
	{
		if (IsEnded) return false;

		// 수리 완료와 추가 피해가 같은 판정 시점이면 피해를 먼저 반영해 총량을 재산정하고 차액 지불 전 정지한다(24-표1).
		if (sameTickExtraDamage > 0f)
		{
			currentHp = Math.Max(0f, currentHp - sameTickExtraDamage);
			ApplyDamageAndRecalculate(currentHp, lifeEnergyAccount);
			if (IsEnded) return false;
		}

		if (IsPausedWaitingDiffApproval)
			return false;

		float coopSpeed = WorkSpeedMath.CalculateTotalWorkSpeed(activeWorkerSpeeds);
		if (coopSpeed <= 0f)
			return false;

		if (!HasStartedWork)
		{
			if (!isCoreActive) return false;
			if (lifeEnergyAccount == null || !lifeEnergyAccount.CommitReservedCost(ReservedBeforeStart, TotalRequiredCeilCost, out int paid))
				return false;
			ReservedBeforeStart = 0;
			PaidTotalSoFar = paid;
			HasStartedWork = true;
		}

		if (deltaSeconds <= 0.0) return false;

		CompletedWorkSeconds += deltaSeconds * coopSpeed;
		if (CompletedWorkSeconds + Epsilon >= TotalRequiredWorkSeconds)
		{
			CompletedWorkSeconds = TotalRequiredWorkSeconds;
			currentHp = TargetMaxHp; // 완료 시 한 번에 최대 HP 복구(18항)
			IsCompleted = true;
			return true;
		}
		return false;
	}

	public void CancelManually(ResourceAccount lifeEnergyAccount)
	{
		if (IsEnded) return;
		if (!HasStartedWork)
		{
			if (ReservedBeforeStart > 0)
				lifeEnergyAccount?.ReleaseReservation(ReservedBeforeStart);
			ReservedBeforeStart = 0;
		}
		else
		{
			if (PaidTotalSoFar > 0)
				lifeEnergyAccount?.RefundPaid(PaidTotalSoFar);
		}
		IsCancelled = true;
	}
}

// ── 코어 수복과 잔해 정산 (명세서 v0.2 19항) ──

public sealed class CoreRestoreSession
{
	private const double Epsilon = 1e-9;

	public double BaseRestoreCost { get; }
	public double BaseRestoreWorkSeconds { get; }
	public float RestoreObjectMaxHp { get; }
	public float RestoreObjectCurrentHp { get; private set; }
	public bool HasBoundSalvage { get; private set; }
	public InteractableObject BoundSalvageObject { get; private set; }

	public double SalvageCredit => HasBoundSalvage ? BaseRestoreCost * 0.5 : 0.0;
	public double CumulativeExtraDamageHp { get; private set; }

	public bool HasStartedWork { get; private set; }
	public int ReservedBeforeStart { get; private set; }
	public int PaidTotalSoFar { get; private set; }
	public int TotalRequiredCeilCost { get; private set; }
	public double TotalRequiredWorkSeconds { get; private set; }
	public double CompletedWorkSeconds { get; private set; }

	public bool IsCompleted { get; private set; }
	public bool IsDestroyed { get; private set; }
	public bool IsCancelled { get; private set; }
	public bool IsEnded => IsCompleted || IsDestroyed || IsCancelled;

	public int UnapprovedDiffAmount => HasStartedWork
		? Math.Max(0, TotalRequiredCeilCost - PaidTotalSoFar)
		: Math.Max(0, TotalRequiredCeilCost - ReservedBeforeStart);

	public bool IsPausedWaitingDiffApproval => UnapprovedDiffAmount > 0;

	public double Progress01 => TotalRequiredWorkSeconds > 0.0
		? Math.Clamp(CompletedWorkSeconds / TotalRequiredWorkSeconds, 0.0, 1.0)
		: 1.0;

	private CoreRestoreSession(
		double baseCost,
		double baseWorkSeconds,
		float restoreObjMaxHp,
		bool useSalvage,
		InteractableObject salvageObj,
		int initialCost)
	{
		BaseRestoreCost = Math.Max(0.0, baseCost);
		BaseRestoreWorkSeconds = Math.Max(0.001, baseWorkSeconds);
		RestoreObjectMaxHp = Math.Max(1f, restoreObjMaxHp);
		RestoreObjectCurrentHp = RestoreObjectMaxHp;
		HasBoundSalvage = useSalvage;
		BoundSalvageObject = salvageObj;
		TotalRequiredCeilCost = initialCost;
		ReservedBeforeStart = initialCost;
		TotalRequiredWorkSeconds = BaseRestoreWorkSeconds;
	}

	public static bool TryCreateOrder(
		double baseRestoreCost,
		double baseRestoreWorkSeconds,
		float restoreObjectMaxHp,
		bool isCoreDestroyed,
		bool isCoreRoomCleared,
		bool hasHumanByproductDevice,
		bool useOnSiteSalvage,
		InteractableObject onSiteSalvageOrNull,
		ResourceAccount lifeEnergyAccount,
		out CoreRestoreSession session,
		out string failureMessage)
	{
		session = null;
		failureMessage = string.Empty;

		if (!isCoreDestroyed)
		{
			failureMessage = "파괴된 코어가 아닙니다.";
			return false;
		}
		if (!isCoreRoomCleared || hasHumanByproductDevice)
		{
			failureMessage = "코어방을 먼저 클리어해야 수복할 수 있습니다.";
			return false;
		}

		bool bindSalvage = useOnSiteSalvage && (onSiteSalvageOrNull != null || useOnSiteSalvage);
		double credit = bindSalvage ? baseRestoreCost * 0.5 : 0.0;
		int initialCeil = EconomyMath.CeilCost(Math.Max(0.0, baseRestoreCost - credit));

		if (lifeEnergyAccount == null || !lifeEnergyAccount.TryReserve(initialCeil, out int reserved))
		{
			int avail = lifeEnergyAccount != null ? lifeEnergyAccount.DisplayAvailable : 0;
			failureMessage = EconomyMath.FormatInsufficientNotice(ResourceType.LifeEnergy, initialCeil, avail);
			return false;
		}

		session = new CoreRestoreSession(baseRestoreCost, baseRestoreWorkSeconds, restoreObjectMaxHp, bindSalvage, onSiteSalvageOrNull, reserved);
		return true;
	}

	// 수복 시작 후 누적된 실제 감소 HP 비율로 추가 비용·작업량을 계산하고 총 필요 지불액은 한 번만 올림한다(19-표2, 각주 [28], 24-표1).
	public void ApplyDamageDuringRestore(float damageAmount, ResourceAccount lifeEnergyAccountIfDestroyed = null)
	{
		if (IsEnded || damageAmount <= 0f) return;

		float actualLost = Math.Min(RestoreObjectCurrentHp, damageAmount);
		RestoreObjectCurrentHp -= actualLost;

		if (RestoreObjectCurrentHp <= 0f)
		{
			if (!HasStartedWork && ReservedBeforeStart > 0)
			{
				lifeEnergyAccountIfDestroyed?.ReleaseReservation(ReservedBeforeStart);
				ReservedBeforeStart = 0;
			}
			// 대상 파괴 시 지불액과 투입 잔해 모두 손실되며 새 잔해는 생성하지 않는다(19-표1, 24-표1).
			IsDestroyed = true;
			return;
		}

		CumulativeExtraDamageHp += actualLost;
		RecalculateTotals();
	}

	public bool TryApproveExtraDiffManually(ResourceAccount lifeEnergyAccount, out string failureMessage)
	{
		failureMessage = string.Empty;
		if (IsEnded) return false;

		int diff = UnapprovedDiffAmount;
		if (diff <= 0) return true;

		if (lifeEnergyAccount == null || !lifeEnergyAccount.CanAffordInt(diff))
		{
			int avail = lifeEnergyAccount != null ? lifeEnergyAccount.DisplayAvailable : 0;
			failureMessage = EconomyMath.FormatInsufficientNotice(ResourceType.LifeEnergy, diff, avail);
			return false;
		}

		if (!HasStartedWork)
		{
			lifeEnergyAccount.TryReserve(diff, out int added);
			ReservedBeforeStart += added;
		}
		else
		{
			lifeEnergyAccount.TrySpendImmediate(diff, out int paid);
			PaidTotalSoFar += paid;
		}
		return true;
	}

	public bool Tick(
		double deltaSeconds,
		IReadOnlyList<float> activeWorkerSpeeds,
		bool isCoreRoomCleared,
		ResourceAccount lifeEnergyAccount,
		float sameTickExtraDamage = 0f)
	{
		if (IsEnded) return false;

		if (sameTickExtraDamage > 0f)
		{
			ApplyDamageDuringRestore(sameTickExtraDamage, lifeEnergyAccount);
			if (IsEnded) return false;
		}

		if (!isCoreRoomCleared || IsPausedWaitingDiffApproval)
			return false;

		float coopSpeed = WorkSpeedMath.CalculateTotalWorkSpeed(activeWorkerSpeeds);
		if (coopSpeed <= 0f)
			return false;

		if (!HasStartedWork)
		{
			if (lifeEnergyAccount == null || !lifeEnergyAccount.CommitReservedCost(ReservedBeforeStart, TotalRequiredCeilCost, out int paid))
				return false;
			ReservedBeforeStart = 0;
			PaidTotalSoFar = paid;
			HasStartedWork = true;
		}

		if (deltaSeconds <= 0.0) return false;

		CompletedWorkSeconds += deltaSeconds * coopSpeed;
		if (CompletedWorkSeconds + Epsilon >= TotalRequiredWorkSeconds)
		{
			CompletedWorkSeconds = TotalRequiredWorkSeconds;
			IsCompleted = true;
			return true;
		}
		return false;
	}

	// 수동 취소: 지불한 생명 에너지를 전액 환급하고 묶여 있던 잔해는 현장에 다시 회수 가능 상태로 둔다(19-표1).
	public void CancelManually(ResourceAccount lifeEnergyAccount, out bool returnSalvageToField)
	{
		returnSalvageToField = false;
		if (IsEnded) return;

		if (!HasStartedWork)
		{
			if (ReservedBeforeStart > 0)
				lifeEnergyAccount?.ReleaseReservation(ReservedBeforeStart);
			ReservedBeforeStart = 0;
		}
		else
		{
			if (PaidTotalSoFar > 0)
				lifeEnergyAccount?.RefundPaid(PaidTotalSoFar);
		}

		returnSalvageToField = HasBoundSalvage;
		IsCancelled = true;
	}

	private void RecalculateTotals()
	{
		double cumDamageRatio = CumulativeExtraDamageHp / RestoreObjectMaxHp;
		double extraCostRaw = BaseRestoreCost * cumDamageRatio;
		double extraWork = BaseRestoreWorkSeconds * cumDamageRatio;

		// 기본 수복비 − 잔해 충당액 + 누적 추가 비용을 합산한 뒤 한 번만 올림한다(19-표2).
		double totalRaw = Math.Max(0.0, BaseRestoreCost - SalvageCredit + extraCostRaw);
		TotalRequiredCeilCost = EconomyMath.CeilCost(totalRaw);
		TotalRequiredWorkSeconds = BaseRestoreWorkSeconds + extraWork;
	}
}

// ── 철거 보상 계산 및 철거 작업 (명세서 v0.2 20-1항) ──

public static class DemolitionMath
{
	// 철거 완료 시 외부 기본 보상(누적 획득량 포함)과 자기 투자 회수분(누적 획득량 제외)을 분리 지급한다(20-1-표1, 20-1-표2).
	// 수리비와 전역 연구비는 회수 대상에 포함하지 않으며 남은 HP도 반영하지 않는다.
	public static void CreditDemolitionPayout(
		FacilityOriginKind origin,
		IReadOnlyDictionary<ResourceType, int> actualPaidBuildCosts,
		IReadOnlyDictionary<ResourceType, int> actualPaidUpgradeCosts,
		double recoveryRate,
		IReadOnlyDictionary<ResourceType, double> externalBaseDemolishReward,
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts)
	{
		if (accounts == null) return;
		double rate = Math.Clamp(recoveryRate, 0.0, 1.0);

		if (origin == FacilityOriginKind.PrePlacedOrCapturedDungeon || origin == FacilityOriginKind.HumanStructure)
		{
			if (externalBaseDemolishReward != null)
			{
				foreach (var kv in externalBaseDemolishReward)
				{
					if (kv.Value > 0.0 && accounts.TryGetValue(kv.Key, out var acc))
						acc.Credit(kv.Value, ResourceCreditSource.ExternalFacilityDemolish);
				}
			}
		}

		if (origin == FacilityOriginKind.PlayerBuilt && actualPaidBuildCosts != null)
		{
			foreach (var kv in actualPaidBuildCosts)
			{
				double refund = kv.Value * rate;
				if (refund > 0.0 && accounts.TryGetValue(kv.Key, out var acc))
					acc.Credit(refund, ResourceCreditSource.SelfFacilityDemolishRecovery);
			}
		}

		// 직접 건설한 시설과 점령 후 직접 업그레이드한 시설 모두 자기 업그레이드 투자 회수분을 돌려받는다(20-1-표1).
		if (origin != FacilityOriginKind.HumanStructure && actualPaidUpgradeCosts != null)
		{
			foreach (var kv in actualPaidUpgradeCosts)
			{
				double refund = kv.Value * rate;
				if (refund > 0.0 && accounts.TryGetValue(kv.Key, out var acc))
					acc.Credit(refund, ResourceCreditSource.SelfFacilityDemolishRecovery);
			}
		}
	}
}

// ── 이동용 분해 · 1타일 낙하 · 재설치 (명세서 v0.2 20-2항, 20-3항) ──

public enum RelocationStage
{
	WaitingDismantleStart,
	Dismantling,
	CarryingPackage,
	DroppedAs1x1Package,
	InstallingAtDestination,
	CompletedReinstall,
	AbortedBeforeDismantle,
	Destroyed,
}

public sealed class RelocationSession
{
	private const double Epsilon = 1e-9;

	public double DismantleBaseCost { get; }
	public double DismantleRequiredSeconds { get; }
	public double InstallRequiredSeconds { get; }
	public Vector2Int FacilityFullFootprint { get; }
	public Vector2Int PackageFootprint => Vector2Int.one; // 분해 완료 후 1타일 오브젝트(20-3항)
	public int? DedicatedFloorRestriction { get; }

	public RelocationStage Stage { get; private set; }
	public Vector3Int TargetDestination { get; private set; }
	public Vector3Int? LastInstallProgressDestination { get; private set; }

	public int ReservedDismantleCost { get; private set; }
	public int PaidDismantleCost { get; private set; }
	public bool HasPaidDismantle { get; private set; }

	public double DismantleCompletedSeconds { get; private set; }
	public double InstallCompletedSeconds { get; private set; }
	public int AssignedWorkerUnitId { get; set; } = -1;

	public bool IsDismantleFinished => Stage == RelocationStage.CarryingPackage
		|| Stage == RelocationStage.DroppedAs1x1Package
		|| Stage == RelocationStage.InstallingAtDestination
		|| Stage == RelocationStage.CompletedReinstall;

	public RelocationSession(
		double dismantleCost,
		double dismantleSeconds,
		double installSeconds,
		Vector2Int fullFootprint,
		Vector3Int initialDestination,
		int reservedDismantleCost,
		int? dedicatedFloor = null)
	{
		DismantleBaseCost = Math.Max(0.0, dismantleCost);
		DismantleRequiredSeconds = Math.Max(0.001, dismantleSeconds);
		InstallRequiredSeconds = Math.Max(0.001, installSeconds);
		FacilityFullFootprint = fullFootprint;
		TargetDestination = initialDestination;
		ReservedDismantleCost = reservedDismantleCost;
		DedicatedFloorRestriction = dedicatedFloor;
		Stage = RelocationStage.WaitingDismantleStart;
	}

	public static bool TryCreateOrder(
		double dismantleLifeCost,
		double dismantleSeconds,
		double installSeconds,
		Vector2Int fullFootprint,
		Vector3Int initialDestination,
		bool isCoreActive,
		bool isRefineryCompletelyEmpty,
		bool isDestinationValidForFullFootprint,
		ResourceAccount lifeEnergyAccount,
		out RelocationSession session,
		out string failureMessage,
		int? dedicatedFloor = null)
	{
		session = null;
		failureMessage = string.Empty;

		if (!isCoreActive)
		{
			failureMessage = "담당 코어가 파괴되어 이동용 분해를 시작할 수 없습니다.";
			return false;
		}
		if (!isRefineryCompletelyEmpty)
		{
			failureMessage = "내부 시체를 모두 비운 뒤에 시설을 이동할 수 있습니다.";
			return false;
		}
		if (dedicatedFloor.HasValue && dedicatedFloor.Value != initialDestination.z)
		{
			failureMessage = $"전용 생산시설은 {dedicatedFloor.Value}층에만 설치할 수 있습니다.";
			return false;
		}
		if (!isDestinationValidForFullFootprint)
		{
			failureMessage = "설치 목적지에 시설 전체 크기의 공간이 부족합니다.";
			return false;
		}

		int ceilCost = EconomyMath.CeilCost(dismantleLifeCost);
		if (lifeEnergyAccount == null || !lifeEnergyAccount.TryReserve(ceilCost, out int reserved))
		{
			int avail = lifeEnergyAccount != null ? lifeEnergyAccount.DisplayAvailable : 0;
			failureMessage = EconomyMath.FormatInsufficientNotice(ResourceType.LifeEnergy, ceilCost, avail);
			return false;
		}

		session = new RelocationSession(dismantleLifeCost, dismantleSeconds, installSeconds, fullFootprint, initialDestination, reserved, dedicatedFloor);
		return true;
	}

	// 분해 진행 — 실제 분해 시작 시 1회 차감하며, 이동 적용으로 멈춘 뒤 재개할 때는 추가 차감하지 않는다(20-2-표1).
	public bool TickDismantle(
		double deltaSeconds,
		IReadOnlyList<float> activeWorkerSpeeds,
		bool isCoreActive,
		double startCostMultiplier,
		ResourceAccount lifeEnergyAccount)
	{
		if (Stage != RelocationStage.WaitingDismantleStart && Stage != RelocationStage.Dismantling)
			return false;

		float coopSpeed = WorkSpeedMath.CalculateTotalWorkSpeed(activeWorkerSpeeds);
		if (coopSpeed <= 0f) return false;

		if (!HasPaidDismantle)
		{
			if (!isCoreActive) return false;
			if (lifeEnergyAccount == null || !lifeEnergyAccount.CommitReservedCost(ReservedDismantleCost, DismantleBaseCost * startCostMultiplier, out int paid))
				return false;
			ReservedDismantleCost = 0;
			PaidDismantleCost = paid;
			HasPaidDismantle = true;
		}

		Stage = RelocationStage.Dismantling;
		if (deltaSeconds <= 0.0) return false;

		DismantleCompletedSeconds += deltaSeconds * coopSpeed;
		if (DismantleCompletedSeconds + Epsilon >= DismantleRequiredSeconds)
		{
			DismantleCompletedSeconds = DismantleRequiredSeconds;
			Stage = RelocationStage.CarryingPackage;
			return true;
		}
		return false;
	}

	// 분해 완료 전 작업자 또는 대상 피격: 대상 생존 시 진행 삭제·전액 환급·원위치 유지, 대상 파괴 시 환급 없음(20-2-표1, 각주 [30], 24-표1).
	public void OnHitBeforeDismantleComplete(bool isTargetFacilityAlive, ResourceAccount lifeEnergyAccount)
	{
		if (Stage != RelocationStage.WaitingDismantleStart && Stage != RelocationStage.Dismantling)
			return;

		if (!HasPaidDismantle && ReservedDismantleCost > 0)
		{
			lifeEnergyAccount?.ReleaseReservation(ReservedDismantleCost);
			ReservedDismantleCost = 0;
		}
		else if (HasPaidDismantle && isTargetFacilityAlive && PaidDismantleCost > 0)
		{
			lifeEnergyAccount?.RefundPaid(PaidDismantleCost);
		}

		DismantleCompletedSeconds = 0.0;
		AssignedWorkerUnitId = -1;
		Stage = isTargetFacilityAlive ? RelocationStage.AbortedBeforeDismantle : RelocationStage.Destroyed;
	}

	// [이동 적용]으로 분해 작업자가 이탈한 경우: 취소·환급 없이 진행과 지불 상태를 그대로 보존한다(20-2-표1, 각주 [31], 24-표1).
	public void OnWorkerMovedAwayDuringDismantle()
	{
		if (Stage == RelocationStage.Dismantling || Stage == RelocationStage.WaitingDismantleStart)
			AssignedWorkerUnitId = -1;
	}

	// 분해 완료 전 수동 취소: 대상 생존 중이므로 지불액 전액 환급(또는 선점 해제) 후 원위치 유지.
	// 분해 완료 후 취소·중단: 완료한 분해 비용은 환급하지 않고 1타일 분해 오브젝트로 내려놓는다(20-2-표1).
	public void CancelOrInterrupt(ResourceAccount lifeEnergyAccount)
	{
		if (Stage == RelocationStage.WaitingDismantleStart || Stage == RelocationStage.Dismantling)
		{
			OnHitBeforeDismantleComplete(isTargetFacilityAlive: true, lifeEnergyAccount);
			return;
		}
		if (Stage == RelocationStage.CarryingPackage || Stage == RelocationStage.InstallingAtDestination)
		{
			AssignedWorkerUnitId = -1;
			Stage = RelocationStage.DroppedAs1x1Package;
		}
	}

	// 운반·설치 중 피격 또는 [이동 적용] 또는 운반자 사망: 1타일 분해 오브젝트로 낙하하며 분해비 환급은 없다(20-3항, 24-표1).
	public void DropPackageToField()
	{
		if (Stage == RelocationStage.CarryingPackage || Stage == RelocationStage.InstallingAtDestination)
		{
			AssignedWorkerUnitId = -1;
			Stage = RelocationStage.DroppedAs1x1Package;
		}
	}

	// 낙하한 1타일 분해 오브젝트를 다른 유닛(또는 같은 유닛)이 회수해 재개한다 — 분해비 재부과·설치 추가 비용 없음(20-2-표1, 24-표1).
	public bool PickUpDroppedPackage(int newWorkerUnitId, Vector3Int destination, bool isDestinationValid)
	{
		if (Stage != RelocationStage.DroppedAs1x1Package) return false;
		if (!TrySetOrValidateDestination(destination, isDestinationValid, isCurrentlyInstalling: true))
			return false;

		AssignedWorkerUnitId = newWorkerUnitId;
		Stage = RelocationStage.CarryingPackage;
		return true;
	}

	// 설치 목적지 변경(20-3-표1, 각주 [34], 24-표1):
	// - 새 목적지가 무효면 변경을 거절하고 기존 목적지·작업을 유지한다.
	// - 같은 목적지면 설치 작업량을 보존한다.
	// - 다른 유효 목적지면 분해·운반 중에는 작업량 손실 없이 목적지만 바꾸고, 설치 중이었다면 설치 작업량만 초기화한다.
	public bool TryChangeDestination(Vector3Int newDestination, bool isNewDestinationValidAndReachable)
	{
		bool wasInstalling = Stage == RelocationStage.InstallingAtDestination || LastInstallProgressDestination.HasValue;
		if (!TrySetOrValidateDestination(newDestination, isNewDestinationValidAndReachable, wasInstalling))
			return false;

		if (Stage == RelocationStage.InstallingAtDestination && TargetDestination != LastInstallProgressDestination)
			Stage = RelocationStage.CarryingPackage;

		return true;
	}

	// 기존 목적지가 확실히 설치 불가·경로 없음으로 판명된 경우: 다른 위치를 자동 선택하지 않고 내려놓아 종료한다(20-3-표1, 각주 [35], 24-표1).
	public void OnCurrentDestinationBecameInvalid()
	{
		if (Stage == RelocationStage.CarryingPackage || Stage == RelocationStage.InstallingAtDestination)
		{
			AssignedWorkerUnitId = -1;
			Stage = RelocationStage.DroppedAs1x1Package;
		}
	}

	// 목적지 도착 후 추가 비용 0으로 재설치 작업 시간을 진행한다(20-2-표1, 24-표1).
	public bool TickReinstallAtDestination(
		double deltaSeconds,
		IReadOnlyList<float> activeWorkerSpeeds,
		bool isDestinationStillValid)
	{
		if (Stage != RelocationStage.CarryingPackage && Stage != RelocationStage.InstallingAtDestination)
			return false;

		if (!isDestinationStillValid)
		{
			OnCurrentDestinationBecameInvalid();
			return false;
		}

		float coopSpeed = WorkSpeedMath.CalculateTotalWorkSpeed(activeWorkerSpeeds);
		if (coopSpeed <= 0f) return false;

		Stage = RelocationStage.InstallingAtDestination;
		LastInstallProgressDestination = TargetDestination;

		if (deltaSeconds <= 0.0) return false;

		InstallCompletedSeconds += deltaSeconds * coopSpeed;
		if (InstallCompletedSeconds + Epsilon >= InstallRequiredSeconds)
		{
			InstallCompletedSeconds = InstallRequiredSeconds;
			Stage = RelocationStage.CompletedReinstall;
			return true;
		}
		return false;
	}

	private bool TrySetOrValidateDestination(Vector3Int candidate, bool isValid, bool isCurrentlyInstalling)
	{
		if (!isValid) return false;
		if (DedicatedFloorRestriction.HasValue && DedicatedFloorRestriction.Value != candidate.z)
			return false;

		if (LastInstallProgressDestination.HasValue && LastInstallProgressDestination.Value != candidate && isCurrentlyInstalling)
		{
			InstallCompletedSeconds = 0.0;
			LastInstallProgressDestination = null;
		}

		TargetDestination = candidate;
		return true;
	}
}
