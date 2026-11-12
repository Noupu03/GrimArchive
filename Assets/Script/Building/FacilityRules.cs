using System;
using System.Collections.Generic;
using UnityEngine;

// 건설 설계명세서 v0.2 3단계의 순수 규칙 — 내부 작업(1-6·2-8), 문 확장(2-9), 전투 시설 정지(2-14).
// 수치는 오브젝트 종류·문 종류 문서가 정할 자리표시자이며, 맵·유닛 격자를 바꾸는 호출부는 이 파일 밖이다.

// ── 내부 작업(1-4·1-6·2-8) ──
public enum InternalCombatSetting
{
	Respond, // 대응: 현재 방의 전투 인지 또는 시설 피격 시 밖으로 나와 전투 참여
	Ignore,  // 무시: 내부 작업 계속
}

public static class InternalWorkRules
{
	// 시설별 정원 - 자리표시자(생명 에너지 4, 연구 2). 종류별 값은 오브젝트 종류 문서 소관.
	public static int GetCapacity(FacilityKind kind)
	{
		switch (kind)
		{
			case FacilityKind.LifeEnergy: return 4;
			case FacilityKind.Research: return 2;
			default: return 0;
		}
	}

	public const int MinBodyArea = 1; // 자리표시자
	public const int MaxBodyArea = 4; // 자리표시자 - 2x2까지

	public static bool IsInternalWorkKind(FacilityKind kind) => GetCapacity(kind) > 0;

	// null이면 입장·배정 가능. 코어가 파괴된 동안은 새 내부 작업자를 받을 수 없고, 실제 수리·강화 중에도 새 인원은 입장하지 못한다(1-7-표1, 1-2-표2).
	public static string GetEnterBlockReason(FacilityKind kind, int currentInside, Vector2Int bodyFootprint, bool coreActive, bool actualRepairOrUpgrade)
	{
		int cap = GetCapacity(kind);
		if (cap <= 0) return "내부 작업을 하지 않는 시설";
		if (!coreActive) return "코어가 파괴되어 새 내부 작업자를 받을 수 없음(수복 후 배정 가능)";
		if (actualRepairOrUpgrade) return "실제 수리·강화 중이라 입장할 수 없음";
		int area = bodyFootprint.x * bodyFootprint.y;
		if (area < MinBodyArea || area > MaxBodyArea) return "몸체 크기가 시설의 내부 작업 조건에 맞지 않음";
		if (currentInside >= cap) return $"내부 정원 가득({cap}명)";
		return null;
	}

	// 정원 외에 안에 있는 전원이 겹치지 않고 나올 면적도 확인한다 - 시설 정원은 전원이 겹치지 않고 나올 면적 안에서 정한다(건설 명세 2-8).
	public static string GetEnterBlockReason(FacilityKind kind, int currentInside, Vector2Int bodyFootprint, bool coreActive, bool actualRepairOrUpgrade, int insideBodyArea, int facilityArea)
	{
		string reason = GetEnterBlockReason(kind, currentInside, bodyFootprint, coreActive, actualRepairOrUpgrade);
		if (reason != null) return reason;
		if (facilityArea > 0 && insideBodyArea + bodyFootprint.x * bodyFootprint.y > facilityArea) return "시설 안에서 전원이 겹치지 않고 나올 면적이 부족함";
		return null;
	}

	// 내부 작업자가 밖으로 나와 전투에 참여해야 하는가(1-6-표1). 외부 전투 중 '무시'로 바꿔도 전투를 즉시 버리지 않는다는 규칙은 호출부가 전투 상태를 보고 처리한다.
	public static bool ShouldExitForCombat(InternalCombatSetting setting, bool combatPerceivedInRoom, bool facilityHit)
		=> setting == InternalCombatSetting.Respond && (combatPerceivedInRoom || facilityHit);

	// 실제 분해·철거·수리·강화 착수, 코어 파괴, 배정 해제는 내부 작업자를 밖으로 내보낸다. 수리·강화·코어 대기는 배정을 유지하고, 분해는 유지, 철거는 해제한다(2-5-표2).
	public enum ExitCause { None, ActualRepair, ActualUpgrade, CoreDestroyed, Dismantle, Demolish, Released, Combat }

	public static bool KeepsAssignmentAfterExit(ExitCause cause)
		=> cause == ExitCause.ActualRepair || cause == ExitCause.ActualUpgrade || cause == ExitCause.CoreDestroyed
		|| cause == ExitCause.Dismantle || cause == ExitCause.Combat;

	// 비켜주기(1-6-표2) - 막힌 쪽이 무엇을 하고 있는가에 따른 처리.
	public enum BlockerState { WaitingAlly, ExitWaitingAlly, WorkerOnlyEscape, BusyOrFighting, NoFreeSpace, NaturalRecoveryOnly }
	public enum YieldAction { StepAside, StepAsideAlsoNeighbors, WorkerStepsBackThenResume, WaitWithoutForcing, KeepExitWait }

	// 출구를 막은 유닛의 상태를 분류한다(1-6-표2) - 작업·전투 중이면 강제하지 않고, 작업자가 유일한 탈출 경로를 막으면 잠시 물러난다.
	public static BlockerState ClassifyBlocker(bool busyOrFighting, bool workerOccupiesOnlyEscape, bool exitingWaiting)
	{
		if (busyOrFighting) return BlockerState.BusyOrFighting;
		if (workerOccupiesOnlyEscape) return BlockerState.WorkerOnlyEscape;
		return exitingWaiting ? BlockerState.ExitWaitingAlly : BlockerState.WaitingAlly;
	}

	public static YieldAction ResolveYield(BlockerState s)
	{
		switch (s)
		{
			case BlockerState.WaitingAlly: return YieldAction.StepAside;
			case BlockerState.ExitWaitingAlly: return YieldAction.StepAsideAlsoNeighbors;
			case BlockerState.WorkerOnlyEscape: return YieldAction.WorkerStepsBackThenResume;
			case BlockerState.BusyOrFighting: return YieldAction.WaitWithoutForcing;
			case BlockerState.NoFreeSpace: return YieldAction.KeepExitWait; // 순간이동·자리 교환 없음
			default: return YieldAction.StepAside; // 자연 회복만 받으며 대기하는 유닛은 별도 작업이 아니라 비켜줄 수 있다
		}
	}
}

// ── 바닥에 겹쳐 놓인 시체의 선택 순서(2-8-표2) ──
// 영혼 파편 총가치(기본 + 진화 추가분) → 생명 에너지 → 먼저 놓인 순. 겹친 시체도 각 개체의 크기·종류·보상·부패·입고 배정을 유지하는 독립 대상이다.
public static class FloorCorpseOrder
{
	public static double SoulValue(CorpseRecord c)
	{
		if (c == null) return 0.0;
		CorpseRefineReward r = CorpseRefineMath.CalculateReward(c);
		return r.BaseSoulShard + r.EvolutionBonusSoulShard;
	}

	public static double LifeValue(CorpseRecord c) => c == null ? 0.0 : CorpseRefineMath.CalculateReward(c).LifeEnergy;

	// 0 이상 후보 중 가장 먼저 골라야 할 인덱스(후보는 놓인 순서대로 들어온다). 후보가 없으면 -1이다.
	public static int PickBestIndex(IList<CorpseRecord> candidates)
	{
		int best = -1;
		double bestSoul = double.MinValue, bestLife = double.MinValue;
		for (int i = 0; i < candidates.Count; i++)
		{
			if (candidates[i] == null) continue;
			double soul = SoulValue(candidates[i]);
			double life = LifeValue(candidates[i]);
			bool better = best < 0 || soul > bestSoul + 1e-9 || (Math.Abs(soul - bestSoul) <= 1e-9 && life > bestLife + 1e-9);
			if (!better) continue; // 동률은 먼저 놓인 쪽(앞선 인덱스)이 이긴다
			best = i;
			bestSoul = soul;
			bestLife = life;
		}
		return best;
	}
}

// ── 문 확장(2-9) ──
// 문 중심은 고정하고 양 끝 벽을 같은 폭만큼 넓힌다(1x2 → 1x4). 양 끝은 각각 작업량 50%이며 한 명이 한쪽을 마치고 반대쪽으로 가도 완료된다.
// 공사 중엔 기존 문 HP·폭을 쓰고 완료 시 폭과 최대 HP를 바꾸며 HP 비율을 유지한다. 폭 단계·최대 폭·배율·비용은 자리표시자다.
public sealed class DoorExpansionSession
{
	public int CurrentWidth { get; }
	public int TargetWidth { get; }
	public float MaxHpMultiplier { get; }
	public int Cost { get; }
	public double TotalWorkSeconds { get; }
	public double[] EndWork { get; } = new double[2];
	public bool HasStartedWork { get; private set; }
	public int PaidCost { get; private set; }
	public bool IsCompleted { get; private set; }
	public bool IsEnded { get; private set; }

	public double EndRequired => TotalWorkSeconds * 0.5; // 양 끝이 각각 50%
	public bool IsEndDone(int end) => EndWork[end] + 1e-9 >= EndRequired;
	public double Progress01 => Math.Min(1.0, (EndWork[0] + EndWork[1]) / TotalWorkSeconds);

	// 실제 확장 작업이 시작되면 문이 열리고(2-12), 중단·완료 뒤에도 열린 채 남는다. 접근만으로는 열리지 않는다.
	public bool OpensDoor => HasStartedWork;

	public DoorExpansionSession(int currentWidth, int targetWidth, float maxHpMultiplier, int cost, double totalWorkSeconds)
	{
		CurrentWidth = currentWidth;
		TargetWidth = Math.Max(currentWidth, targetWidth);
		MaxHpMultiplier = Math.Max(1f, maxHpMultiplier);
		Cost = Math.Max(0, cost);
		TotalWorkSeconds = Math.Max(0.001, totalWorkSeconds);
	}

	// 한 쪽 끝에서 일하는 작업량. 그 끝이 이미 끝났으면 더 쌓이지 않는다(작업자는 반대쪽으로 옮겨 가야 한다). 비용은 첫 실제 작업 시 한 번 지불한다.
	public void Tick(int end, double workAmount, ref int availableResource)
	{
		if (IsEnded || end < 0 || end > 1 || workAmount <= 0.0) return;
		if (!HasStartedWork)
		{
			if (availableResource < Cost) return;
			availableResource -= Cost;
			PaidCost = Cost;
			HasStartedWork = true;
		}
		if (IsEndDone(end)) return;
		EndWork[end] = Math.Min(EndRequired, EndWork[end] + workAmount);
		if (IsEndDone(0) && IsEndDone(1))
		{
			IsCompleted = true;
			IsEnded = true;
		}
	}

	// 완료 시 폭과 최대 HP가 바뀌고 남은 HP 비율을 유지한다(120/200, x1.5 -> 180/300).
	public (float newMaxHp, float newHp) ComputeCompletedHp(float hp, float maxHp)
	{
		float newMax = maxHp * MaxHpMultiplier;
		return (newMax, ConstructionRules.HpAfterMaxChange(hp, maxHp, newMax));
	}

	// 직접 취소하면 확장 비용을 돌려주고 현재 폭·HP를 보존한다.
	public int Cancel()
	{
		if (IsEnded) return 0;
		IsEnded = true;
		int refund = PaidCost;
		PaidCost = 0;
		return refund;
	}

	// 공사 중 문이 파괴되면 환급이나 미완료 폭 확장은 없다. 이미 확장한 개구부는 문이 파괴돼도 남는다.
	public void OnDoorDestroyed()
	{
		IsEnded = true;
	}
}

// ── 전투 시설의 정지(2-14) ──
// 전투 시설은 실제 수리·강화, 분해·철거 착수, 담당 코어 파괴로 정지한다. 정지 전 공격 상태는 종류별로 다르게 처리한다.
public enum CombatAttackPhase { Preparing, ProjectileInFlight, AppliedDot, FacilityDependentSustained, Idle }

public static class CombatFacilityStopRules
{
	// 정지 순간 그 공격 상태가 어떻게 되는가.
	public static bool CancelsOnStop(CombatAttackPhase phase)
		=> phase == CombatAttackPhase.Preparing || phase == CombatAttackPhase.FacilityDependentSustained;

	// 이미 발사한 투사체와 이미 적용한 지속 피해는 시설 정지와 무관하게 끝까지 처리된다.
	public static bool ContinuesAfterStop(CombatAttackPhase phase)
		=> phase == CombatAttackPhase.ProjectileInFlight || phase == CombatAttackPhase.AppliedDot;

	// 남은 공격 재사용 시간은 정지 동안 줄지 않고 보존되며 운반·재설치로 초기화되지 않는다.
	public static float AdvanceCooldown(float remainingCooldown, float dt, bool stopped)
		=> stopped ? remainingCooldown : Math.Max(0f, remainingCooldown - dt);
}
