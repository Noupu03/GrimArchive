using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct WorkerContributionInput
{
	public readonly int WorkerId;
	public readonly float EffectiveSpeed;
	public readonly double OutputBonusRatio;

	public WorkerContributionInput(int workerId, float effectiveSpeed, double outputBonusRatio = 0.0)
	{
		WorkerId = workerId;
		EffectiveSpeed = effectiveSpeed;
		OutputBonusRatio = outputBonusRatio;
	}
}

[Serializable]
public struct LifeEnergyCycleSnapshot
{
	public double Progress01;
	public double WeightedBonusAccumulator;
	public double WeightedEfficiencyDelta;
}

public static class WorkSpeedMath
{
	public const int MaxWorkersPerTask = 4;
	public static readonly float[] CoopMultipliers = { 1.0f, 0.7f, 0.5f, 0.3f };

	// 개인 효과 반영 후 속도가 높은 순으로 최대 4기까지 1.0·0.7·0.5·0.3을 곱해 합산한다(5-표2, 5-표3, 각주 [5]).
	public static float CalculateTotalWorkSpeed(IReadOnlyList<float> individualEffectiveSpeeds)
	{
		if (individualEffectiveSpeeds == null || individualEffectiveSpeeds.Count == 0)
			return 0f;

		var sorted = new List<float>(individualEffectiveSpeeds.Count);
		for (int i = 0; i < individualEffectiveSpeeds.Count; i++)
		{
			float s = individualEffectiveSpeeds[i];
			if (s > 0f) sorted.Add(s);
		}
		if (sorted.Count == 0) return 0f;

		sorted.Sort((a, b) => b.CompareTo(a));
		int limit = Math.Min(sorted.Count, MaxWorkersPerTask);
		float sum = 0f;
		for (int i = 0; i < limit; i++)
			sum += sorted[i] * CoopMultipliers[i];
		return sum;
	}

	public static float CalculateUniformWorkSpeed(int workerCount, float singleWorkerSpeed = 1.0f)
	{
		if (workerCount <= 0 || singleWorkerSpeed <= 0f) return 0f;
		int limit = Math.Min(workerCount, MaxWorkersPerTask);
		float sum = 0f;
		for (int i = 0; i < limit; i++)
			sum += singleWorkerSpeed * CoopMultipliers[i];
		return sum;
	}

	// 공동 작업 계수까지 반영한 작업량 비중으로 현 구간의 가중 산출 보너스율(Σ(s_k * b_k) / Σ s_k)을 함께 계산한다(6-1항).
	public static (double totalSpeed, double weightedUnitOutputBonus) EvaluateCoopWithOutputBonus(
		IReadOnlyList<WorkerContributionInput> workers)
	{
		if (workers == null || workers.Count == 0) return (0.0, 0.0);

		var valid = new List<WorkerContributionInput>(workers.Count);
		for (int i = 0; i < workers.Count; i++)
		{
			if (workers[i].EffectiveSpeed > 0f)
				valid.Add(workers[i]);
		}
		if (valid.Count == 0) return (0.0, 0.0);

		valid.Sort((a, b) =>
		{
			int cmp = b.EffectiveSpeed.CompareTo(a.EffectiveSpeed);
			return cmp != 0 ? cmp : a.WorkerId.CompareTo(b.WorkerId);
		});

		int limit = Math.Min(valid.Count, MaxWorkersPerTask);
		double speedSum = 0.0;
		double bonusWeightedSpeedSum = 0.0;
		for (int i = 0; i < limit; i++)
		{
			double weightedSpeed = (double)valid[i].EffectiveSpeed * CoopMultipliers[i];
			speedSum += weightedSpeed;
			bonusWeightedSpeedSum += weightedSpeed * valid[i].OutputBonusRatio;
		}

		if (speedSum <= 0.0) return (0.0, 0.0);
		return (speedSum, bonusWeightedSpeedSum / speedSum);
	}

	public static bool FootprintsOverlap(Vector2Int aOrigin, Vector2Int aSize, Vector2Int bOrigin, Vector2Int bSize)
	{
		int axMax = aOrigin.x + Math.Max(1, aSize.x) - 1;
		int ayMax = aOrigin.y + Math.Max(1, aSize.y) - 1;
		int bxMax = bOrigin.x + Math.Max(1, bSize.x) - 1;
		int byMax = bOrigin.y + Math.Max(1, bSize.y) - 1;

		bool xOverlap = Math.Max(aOrigin.x, bOrigin.x) <= Math.Min(axMax, bxMax);
		bool yOverlap = Math.Max(aOrigin.y, bOrigin.y) <= Math.Min(ayMax, byMax);
		return xOverlap && yOverlap;
	}

	// 대각선 꼭짓점 접촉을 제외한 상하좌우 변 인접 판정(5-표1, 10-표2).
	public static bool IsOrthogonallyAdjacent(Vector2Int targetOrigin, Vector2Int targetSize, Vector2Int unitOrigin, Vector2Int unitSize)
	{
		int tw = Math.Max(1, targetSize.x);
		int th = Math.Max(1, targetSize.y);
		int uw = Math.Max(1, unitSize.x);
		int uh = Math.Max(1, unitSize.y);

		if (FootprintsOverlap(targetOrigin, new Vector2Int(tw, th), unitOrigin, new Vector2Int(uw, uh)))
			return false;

		int txMax = targetOrigin.x + tw - 1;
		int tyMax = targetOrigin.y + th - 1;
		int uxMax = unitOrigin.x + uw - 1;
		int uyMax = unitOrigin.y + uh - 1;

		bool touchHorizontal = (uxMax + 1 == targetOrigin.x || txMax + 1 == unitOrigin.x)
			&& Math.Max(targetOrigin.y, unitOrigin.y) <= Math.Min(tyMax, uyMax);
		bool touchVertical = (uyMax + 1 == targetOrigin.y || tyMax + 1 == unitOrigin.y)
			&& Math.Max(targetOrigin.x, unitOrigin.x) <= Math.Min(txMax, uxMax);

		return touchHorizontal || touchVertical;
	}

	// 대상 사각형의 상하좌우 변에 인접하게 unitSize 크기의 몸 전체를 놓을 수 있는 모든 앵커(좌하단) 후보를 반환한다(중복·모서리 대각선 제외).
	public static List<Vector2Int> GetOrthogonalAdjacentOrigins(Vector2Int targetOrigin, Vector2Int targetSize, Vector2Int unitSize)
	{
		int tw = Math.Max(1, targetSize.x);
		int th = Math.Max(1, targetSize.y);
		int uw = Math.Max(1, unitSize.x);
		int uh = Math.Max(1, unitSize.y);

		var list = new List<Vector2Int>();
		var seen = new HashSet<Vector2Int>();

		void AddCandidate(int x, int y)
		{
			var v = new Vector2Int(x, y);
			if (seen.Add(v)) list.Add(v);
		}

		// 하단 변 인접 (uy + uh == targetOrigin.y)
		for (int x = targetOrigin.x - uw + 1; x <= targetOrigin.x + tw - 1; x++)
			AddCandidate(x, targetOrigin.y - uh);

		// 상단 변 인접 (uy == targetOrigin.y + th)
		for (int x = targetOrigin.x - uw + 1; x <= targetOrigin.x + tw - 1; x++)
			AddCandidate(x, targetOrigin.y + th);

		// 좌측 변 인접 (ux + uw == targetOrigin.x)
		for (int y = targetOrigin.y - uh + 1; y <= targetOrigin.y + th - 1; y++)
			AddCandidate(targetOrigin.x - uw, y);

		// 우측 변 인접 (ux == targetOrigin.x + tw)
		for (int y = targetOrigin.y - uh + 1; y <= targetOrigin.y + th - 1; y++)
			AddCandidate(targetOrigin.x + tw, y);

		return list;
	}
}

// 생명 에너지 시설의 주기 게이지 및 구간별 기여도 기반 산출량 계산기(명세서 v0.2 6항, 6-1항).
public sealed class LifeEnergyCycleTracker
{
	private const double Epsilon = 1e-9;

	public double BaseCycleDurationSeconds { get; set; }
	public double BasePayout { get; set; }
	public double Progress01 { get; private set; }
	public double WeightedBonusAccumulator { get; private set; }
	// 손상 계수(건설 명세 2-4)로 줄어든 구간별 기여 누적: Σ 게이지 × (1+보정) × (e−1). 한 주기 산출량 = 기본 × (1 + 보정 누적 + 이 값). e=1이면 항상 0이다.
	public double WeightedEfficiencyDelta { get; private set; }

	public LifeEnergyCycleTracker(double baseCycleDurationSeconds, double basePayout)
	{
		BaseCycleDurationSeconds = Math.Max(0.001, baseCycleDurationSeconds);
		BasePayout = Math.Max(0.0, basePayout);
	}

	// 가동 조건을 만족한 시간(dt)만큼 진행하고, 완료된 주기가 있으면 총 지급할 생명 에너지(소수 포함)를 반환한다.
	public double Advance(
		double deltaSeconds,
		IReadOnlyList<WorkerContributionInput> activeWorkers,
		bool isFacilityOperational,
		double facilityOutputBonusRatio = 0.0,
		double damageCoefficient = 1.0)
	{
		if (!isFacilityOperational || deltaSeconds <= 0.0) return 0.0;

		var (totalSpeed, weightedWorkerBonus) = WorkSpeedMath.EvaluateCoopWithOutputBonus(activeWorkers);
		if (totalSpeed <= 0.0) return 0.0;

		double e = Math.Clamp(damageCoefficient, 0.0, 1.0);
		if (e <= 0.0) return 0.0;
		double segmentBonusRatio = weightedWorkerBonus + facilityOutputBonusRatio;
		double effLoss = (1.0 + segmentBonusRatio) * (e - 1.0);
		double remainingDeltaGauge = (totalSpeed * e * deltaSeconds) / BaseCycleDurationSeconds;
		double totalPayoutEarned = 0.0;

		while (remainingDeltaGauge > Epsilon)
		{
			double neededToComplete = 1.0 - Progress01;
			if (remainingDeltaGauge + Epsilon >= neededToComplete)
			{
				double applied = Math.Max(0.0, neededToComplete);
				WeightedBonusAccumulator += applied * segmentBonusRatio;
				WeightedEfficiencyDelta += applied * effLoss;
				remainingDeltaGauge -= applied;

				double cycleMultiplier = Math.Max(0.0, 1.0 + WeightedBonusAccumulator + WeightedEfficiencyDelta);
				totalPayoutEarned += BasePayout * cycleMultiplier;

				Progress01 = 0.0;
				WeightedBonusAccumulator = 0.0;
				WeightedEfficiencyDelta = 0.0;
			}
			else
			{
				Progress01 += remainingDeltaGauge;
				WeightedBonusAccumulator += remainingDeltaGauge * segmentBonusRatio;
				WeightedEfficiencyDelta += remainingDeltaGauge * effLoss;
				remainingDeltaGauge = 0.0;
			}
		}

		return totalPayoutEarned;
	}

	// 단일 작업자/단일 속도 구간을 직접 주입하는 보조 메서드(단위 테스트 및 단순 시뮬레이션용).
	public double AdvanceSegmentByGaugeRatio(double gaugeFraction, double bonusRatio)
	{
		if (gaugeFraction <= 0.0) return 0.0;
		double totalEarned = 0.0;
		double rem = gaugeFraction;
		while (rem > Epsilon)
		{
			double need = 1.0 - Progress01;
			if (rem + Epsilon >= need)
			{
				double applied = Math.Max(0.0, need);
				WeightedBonusAccumulator += applied * bonusRatio;
				rem -= applied;
				totalEarned += BasePayout * Math.Max(0.0, 1.0 + WeightedBonusAccumulator);
				Progress01 = 0.0;
				WeightedBonusAccumulator = 0.0;
			}
			else
			{
				Progress01 += rem;
				WeightedBonusAccumulator += rem * bonusRatio;
				rem = 0.0;
			}
		}
		return totalEarned;
	}

	public LifeEnergyCycleSnapshot CaptureSnapshot() => new LifeEnergyCycleSnapshot
	{
		Progress01 = Progress01,
		WeightedBonusAccumulator = WeightedBonusAccumulator,
		WeightedEfficiencyDelta = WeightedEfficiencyDelta,
	};

	public void RestoreSnapshot(LifeEnergyCycleSnapshot snapshot)
	{
		Progress01 = Math.Clamp(snapshot.Progress01, 0.0, 1.0);
		WeightedBonusAccumulator = snapshot.WeightedBonusAccumulator;
		WeightedEfficiencyDelta = snapshot.WeightedEfficiencyDelta;
	}
}
