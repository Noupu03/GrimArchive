using System;
using System.Collections.Generic;
using UnityEngine;

public enum CorpseOriginFaction
{
	PlayerAlly,
	WildMonster,
	HumanEnemy,
}

public readonly struct CorpseRefineReward
{
	public readonly double LifeEnergy;
	public readonly double BaseSoulShard;
	public readonly double EvolutionBonusSoulShard;
	public readonly ResourceCreditSource CreditSource;

	public double TotalSoulShard => BaseSoulShard + EvolutionBonusSoulShard;

	public CorpseRefineReward(
		double lifeEnergy,
		double baseSoulShard,
		double evolutionBonusSoulShard,
		ResourceCreditSource creditSource)
	{
		LifeEnergy = lifeEnergy;
		BaseSoulShard = baseSoulShard;
		EvolutionBonusSoulShard = evolutionBonusSoulShard;
		CreditSource = creditSource;
	}
}

[Serializable]
public sealed class CorpseRecord
{
	public string CorpseId;
	public string SpeciesKey;
	public string DisplayName;
	public CorpseOriginFaction OriginFaction;

	// 생산 가능한 몬스터 종류인지 여부(14-표2)
	public bool IsProducibleSpecies;
	public double BaseProductionLifeCost;
	public double BaseProductionSoulCost;
	public bool GivesBaseSoulShard;

	// 비생산형 대상(인류·야생 전용 개체 등)의 별도 지정 정제 보상(14-표2)
	public double ConfiguredRefineLifeReward;
	public double ConfiguredRefineSoulReward;

	// 확정된 진화 상태까지의 누적 기준 진화 비용(영혼 파편, 14-1항)
	public double CumulativeBaseEvolutionSoulCost;

	public double BaseRefineDurationSeconds = 8.0;
	public double FullDecayDurationSeconds = 180.0;
	public double Decay01;
	public bool IsSpeciesRefinable = true;
	public bool IsInsideRefinery;
	public long ArrivalSequence;
	public Vector2Int Footprint = Vector2Int.one;

	// 시체 인구수는 항상 0이다(14-표1).
	public int PopulationCost => 0;

	public bool IsFullyDecayed => Decay01 >= 1.0 - 1e-9;

	public bool CanBeRefined => IsSpeciesRefinable && !IsFullyDecayed;

	// 필드 또는 운반 중일 때만 경과 게임 시간만큼 부패가 진행된다(시설 내부에서는 정지, 14-표1).
	// 반환값이 true면 이번 틱에 부패 100%에 도달한 것이다(운반 중이면 즉시 낙하·종료, 12-표2).
	public bool TickFieldOrCarryDecay(double deltaGameSeconds)
	{
		if (IsInsideRefinery || deltaGameSeconds <= 0.0 || IsFullyDecayed)
			return false;

		double duration = Math.Max(0.001, FullDecayDurationSeconds);
		Decay01 = Math.Min(1.0, Decay01 + deltaGameSeconds / duration);
		return IsFullyDecayed;
	}
}

public static class CorpseRefineMath
{
	public const double BaseRefineRate = 0.20;
	public const double EvolutionBonusRefineRate = 0.20;

	// 시체 1구 정제 완료 시 지급할 보상을 계산한다(14-표2, 14-표3, 14-1-표1).
	public static CorpseRefineReward CalculateReward(CorpseRecord corpse)
	{
		if (corpse == null)
			return new CorpseRefineReward(0.0, 0.0, 0.0, ResourceCreditSource.ExternalCorpseRefine);

		double life;
		double baseSoul;

		if (corpse.IsProducibleSpecies)
		{
			life = Math.Max(0.0, corpse.BaseProductionLifeCost) * BaseRefineRate;
			if (corpse.OriginFaction == CorpseOriginFaction.PlayerAlly)
			{
				// 일반 아군 시체는 기본 영혼 파편을 지급하지 않는다(14-표2).
				baseSoul = 0.0;
			}
			else
			{
				baseSoul = corpse.GivesBaseSoulShard
					? Math.Max(0.0, corpse.BaseProductionSoulCost) * BaseRefineRate
					: 0.0;
			}
		}
		else
		{
			life = Math.Max(0.0, corpse.ConfiguredRefineLifeReward);
			baseSoul = corpse.OriginFaction == CorpseOriginFaction.PlayerAlly
				? 0.0
				: Math.Max(0.0, corpse.ConfiguredRefineSoulReward);
		}

		// 진화한 시체의 추가 영혼 파편(누적 기준 진화 비용 × 20%)은 아군 포함 모든 진영에 동일하게 적용한다(14-1-표1, 각주 [20]).
		double evoBonusSoul = Math.Max(0.0, corpse.CumulativeBaseEvolutionSoulCost) * EvolutionBonusRefineRate;

		// 아군 시체에서 나온 모든 보상(진화 추가분 포함)은 보유량에만 더하고 누적 획득량에서는 제외한다(14-1-표1, 24-2-표1).
		ResourceCreditSource source = corpse.OriginFaction == CorpseOriginFaction.PlayerAlly
			? ResourceCreditSource.AllyCorpseRefine
			: ResourceCreditSource.ExternalCorpseRefine;

		return new CorpseRefineReward(life, baseSoul, evoBonusSoul, source);
	}

	// 정제 우선순위: 총 영혼 파편(기본+진화 추가분) 내림차순 -> 생명 에너지 내림차순 -> 입고 순서 오름차순(11-표2 ④, 13-1).
	public static int ComparePriority(CorpseRecord a, CorpseRecord b)
	{
		if (ReferenceEquals(a, b)) return 0;
		if (a == null) return 1;
		if (b == null) return -1;

		CorpseRefineReward ra = CalculateReward(a);
		CorpseRefineReward rb = CalculateReward(b);

		int soulCmp = rb.TotalSoulShard.CompareTo(ra.TotalSoulShard);
		if (soulCmp != 0) return soulCmp;

		int lifeCmp = rb.LifeEnergy.CompareTo(ra.LifeEnergy);
		if (lifeCmp != 0) return lifeCmp;

		return a.ArrivalSequence.CompareTo(b.ArrivalSequence);
	}
}

// 시체 보관·정제 통합 시설의 내부 상태 엔진(명세서 v0.2 11~14항).
public sealed class CorpseRefineryState
{
	private const double Epsilon = 1e-9;

	private readonly List<CorpseRecord> _waitingCorpses = new List<CorpseRecord>();
	private readonly HashSet<string> _reservedIncomingIds = new HashSet<string>();
	private long _arrivalCounter;

	public int MaxStorageSlots { get; set; }
	public bool IsDraining { get; private set; }
	public CorpseRecord ActiveRefiningCorpse { get; private set; }
	public double ActiveRefineProgress01 { get; private set; }

	public IReadOnlyList<CorpseRecord> WaitingCorpses => _waitingCorpses;
	public IReadOnlyCollection<string> ReservedIncomingIds => _reservedIncomingIds;

	public int StoredCorpseCount => _waitingCorpses.Count + (ActiveRefiningCorpse != null ? 1 : 0);
	public int ReservedIncomingCount => _reservedIncomingIds.Count;
	public int AvailableSlots => Math.Max(0, MaxStorageSlots - StoredCorpseCount - ReservedIncomingCount);

	// 정제 중·보관 중·운반 예약까지 모두 비워져야 시설 이동·철거를 시작할 수 있다(13-2-표1).
	public bool IsCompletelyEmpty => StoredCorpseCount == 0 && ReservedIncomingCount == 0;

	public CorpseRefineryState(int maxStorageSlots = 5)
	{
		MaxStorageSlots = Math.Max(1, maxStorageSlots);
	}

	// 입고 예약 가능 여부 — 시설 손상·담당 코어 파괴 중에도 입고 예약과 저장은 허용되나, 전체 비우기 중이거나 미설치면 제외된다(12-표1, 12-표2).
	public bool CanAcceptIncoming(CorpseRecord corpse, bool isFacilityFormallyInstalled = true)
	{
		if (!isFacilityFormallyInstalled || IsDraining || corpse == null || string.IsNullOrEmpty(corpse.CorpseId))
			return false;
		if (!corpse.CanBeRefined)
			return false;
		if (_reservedIncomingIds.Contains(corpse.CorpseId))
			return true;
		return AvailableSlots >= 1;
	}

	public bool TryReserveIncomingSlot(CorpseRecord corpse, bool isFacilityFormallyInstalled = true)
	{
		if (!CanAcceptIncoming(corpse, isFacilityFormallyInstalled))
			return false;
		_reservedIncomingIds.Add(corpse.CorpseId);
		return true;
	}

	public bool CancelIncomingReservation(string corpseId)
	{
		if (string.IsNullOrEmpty(corpseId)) return false;
		return _reservedIncomingIds.Remove(corpseId);
	}

	// 운반 완료 입고: 확보했던 예약을 실제 보관 수량으로 전환한다(중복 집계 없음, 12-표1, 12-표2).
	public bool CompleteIntake(CorpseRecord corpse, bool isFacilityFormallyInstalled = true)
	{
		if (!isFacilityFormallyInstalled || IsDraining || corpse == null || !corpse.CanBeRefined)
		{
			if (corpse != null) _reservedIncomingIds.Remove(corpse.CorpseId);
			return false;
		}

		bool hadReservation = !string.IsNullOrEmpty(corpse.CorpseId) && _reservedIncomingIds.Remove(corpse.CorpseId);
		if (!hadReservation && AvailableSlots < 1)
			return false;

		corpse.IsInsideRefinery = true;
		corpse.ArrivalSequence = ++_arrivalCounter;
		_waitingCorpses.Add(corpse);
		return true;
	}

	// 대기 시체 개별 반출 — 현재 정제 중인 시체는 반출·취소할 수 없다(13-1-표1).
	public bool TryEjectWaitingCorpse(string corpseId, out CorpseRecord ejected)
	{
		ejected = null;
		if (string.IsNullOrEmpty(corpseId)) return false;

		for (int i = 0; i < _waitingCorpses.Count; i++)
		{
			if (_waitingCorpses[i].CorpseId == corpseId)
			{
				ejected = _waitingCorpses[i];
				_waitingCorpses.RemoveAt(i);
				ejected.IsInsideRefinery = false; // 기존 부패율 그대로 유지(13-2-표1, 14-표1)
				return true;
			}
		}
		return false;
	}

	// 전체 비우기 시작: 신규 반입과 다음 정제를 차단하고, 기존 반입 예약을 즉시 해제하며 대기 시체를 반출 목록으로 꺼낸다(13-2-표1, 24-표1).
	// 현재 진행 중인 정제 1구는 취소하지 않고 완료까지 진행한다.
	public void StartDrainMode(out List<string> cancelledIncomingIds, out List<CorpseRecord> ejectedWaitingCorpses)
	{
		IsDraining = true;

		cancelledIncomingIds = new List<string>(_reservedIncomingIds);
		_reservedIncomingIds.Clear();

		ejectedWaitingCorpses = new List<CorpseRecord>(_waitingCorpses.Count);
		for (int i = 0; i < _waitingCorpses.Count; i++)
		{
			CorpseRecord c = _waitingCorpses[i];
			c.IsInsideRefinery = false;
			ejectedWaitingCorpses.Add(c);
		}
		_waitingCorpses.Clear();
	}

	// 전체 비우기 해제: 신규 반입과 다음 정제를 다시 허용하되, 이미 취소·변경된 운반 명령은 되돌리지 않는다(13-2-표1, 24-표1).
	public void CancelDrainMode()
	{
		IsDraining = false;
	}

	// 무인 자동 정제 틱 — 작업자 없이 1구씩 처리하며, 가동 가능(최대 HP·코어 정상·정식 설치)일 때만 진행한다(11-표2, 13-1-표1, 13-2-표1).
	public int TickAutoRefine(
		double deltaSeconds,
		bool isFacilityOperational,
		double refineSpeedMultiplier,
		IReadOnlyDictionary<ResourceType, ResourceAccount> accounts,
		Action<CorpseRecord, CorpseRefineReward> onCorpseRefined = null)
	{
		if (!isFacilityOperational)
			return 0;

		int completedCount = 0;
		double remainingTime = Math.Max(0.0, deltaSeconds);

		// 정제 시작 자체는 deltaSeconds == 0 호출에서도 판정할 수 있도록 먼저 검사
		if (ActiveRefiningCorpse == null && !IsDraining && _waitingCorpses.Count > 0)
			SelectNextWaitingCorpse();

		if (remainingTime <= 0.0 || refineSpeedMultiplier <= 0.0)
			return 0;

		while (remainingTime > Epsilon)
		{
			if (ActiveRefiningCorpse == null)
			{
				if (IsDraining || _waitingCorpses.Count == 0)
					break;
				SelectNextWaitingCorpse();
				if (ActiveRefiningCorpse == null)
					break;
			}

			double duration = Math.Max(0.001, ActiveRefiningCorpse.BaseRefineDurationSeconds);
			double neededProgress = Math.Max(0.0, 1.0 - ActiveRefineProgress01);
			double progressPerSec = refineSpeedMultiplier / duration;
			double timeToFinish = neededProgress / progressPerSec;

			if (remainingTime + Epsilon >= timeToFinish)
			{
				remainingTime = Math.Max(0.0, remainingTime - timeToFinish);
				CorpseRecord finished = ActiveRefiningCorpse;
				finished.IsInsideRefinery = false;
				ActiveRefiningCorpse = null;
				ActiveRefineProgress01 = 0.0;

				CorpseRefineReward reward = CorpseRefineMath.CalculateReward(finished);
				if (accounts != null)
				{
					if (reward.LifeEnergy > 0.0 && accounts.TryGetValue(ResourceType.LifeEnergy, out var lifeAcc))
						lifeAcc.Credit(reward.LifeEnergy, reward.CreditSource);
					if (reward.TotalSoulShard > 0.0 && accounts.TryGetValue(ResourceType.SoulShard, out var soulAcc))
						soulAcc.Credit(reward.TotalSoulShard, reward.CreditSource);
				}

				completedCount++;
				onCorpseRefined?.Invoke(finished, reward);

				if (IsDraining)
					break;
			}
			else
			{
				ActiveRefineProgress01 += remainingTime * progressPerSec;
				remainingTime = 0.0;
			}
		}

		return completedCount;
	}

	// 시설 파괴: 대기·정제 중 시체 모두 소멸하며 미완료 보상은 지급하지 않는다(13-1-표1).
	public void OnFacilityDestroyed(out List<string> cancelledIncomingIds)
	{
		cancelledIncomingIds = new List<string>(_reservedIncomingIds);
		_reservedIncomingIds.Clear();
		_waitingCorpses.Clear();
		ActiveRefiningCorpse = null;
		ActiveRefineProgress01 = 0.0;
	}

	// 인류의 시설 정리: 대기 시체는 먼저 밖으로 꺼내고 정제 중 시체는 소멸한다(13-2-표1).
	public void OnClearedByHumans(out List<string> cancelledIncomingIds, out List<CorpseRecord> ejectedWaitingCorpses)
	{
		cancelledIncomingIds = new List<string>(_reservedIncomingIds);
		_reservedIncomingIds.Clear();

		ejectedWaitingCorpses = new List<CorpseRecord>(_waitingCorpses.Count);
		for (int i = 0; i < _waitingCorpses.Count; i++)
		{
			CorpseRecord c = _waitingCorpses[i];
			c.IsInsideRefinery = false;
			ejectedWaitingCorpses.Add(c);
		}
		_waitingCorpses.Clear();

		ActiveRefiningCorpse = null;
		ActiveRefineProgress01 = 0.0;
	}

	private void SelectNextWaitingCorpse()
	{
		if (_waitingCorpses.Count == 0) return;

		int bestIdx = 0;
		for (int i = 1; i < _waitingCorpses.Count; i++)
		{
			if (CorpseRefineMath.ComparePriority(_waitingCorpses[i], _waitingCorpses[bestIdx]) < 0)
				bestIdx = i;
		}

		ActiveRefiningCorpse = _waitingCorpses[bestIdx];
		_waitingCorpses.RemoveAt(bestIdx);
		ActiveRefineProgress01 = 0.0;
	}
}
