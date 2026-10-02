using System.Collections.Generic;
using UnityEngine;

// 02번 문서(전투 목표와 아군 보호 및 지원) 순수 계산 함수 모음 — 부수효과 없음, 테스트 용이.
// WeightMath.cs/VisionMath.cs/PropagationMath.cs와 동일한 자리·성격.
public static class CombatScoreMath
{
	// ── 1장: 역할 분류 ──────────────────────────────────────────────────
	// 인류 8클래스 매핑 근거는 구현현황 문서 "핵심 설계 결정" 6번 참고(실제 보유 스킬 기준).
	public static CombatRole ResolveCombatRole(UnitType type)
	{
		switch (type)
		{
			case Paladin _:  return CombatRole.MeleeTank;
			case Warrior _:  return CombatRole.MeleeDps;
			case Rogue _:    return CombatRole.MeleeDps;
			case Mage _:     return CombatRole.RangedDps;
			case Archer _:   return CombatRole.RangedDps;
			case Priest _:   return CombatRole.RangedSupport;
			case Shaman _:   return CombatRole.RangedSupport;
			case Bard _:     return CombatRole.RangedSupport;
			case Monk _:     return CombatRole.MeleeSupport;
			// 나머지 몬스터/야생: 전부 근접 공용 스킷(육중한 내리찍기류)이라 타입명 그대로 근접 탱커로
			// 통일(아처형은 위에서 이미 원거리공격으로 예외 처리됨).
			default:         return CombatRole.MeleeTank;
		}
	}

	// ── 3장: 대상 종류 배율 ──────────────────────────────────────────────
	public static TargetCategory ResolveTargetCategory(Unit target)
	{
		if (target?.unitType is BossGolem) return TargetCategory.Boss;
		// 공격형 방어 건물 대응 유닛 타입이 이 프로토타입엔 아직 없다 — 생기면 여기서 분기 추가.
		return TargetCategory.Normal;
	}

	public static float TargetCategoryMultiplier(TargetCategory category) => category switch
	{
		TargetCategory.AttackBuilding => 1.15f,
		TargetCategory.Boss           => 1.50f,
		_                              => 1.00f,
	};

	// 3장 4x5 표(초기 조정값). 근접 지원 행은 공격 행동을 선택했을 때만 쓴다. 원거리 지원 행은 적 대상 스킬(Curse)을 가진 주술사가 MeleeTank 행으로 조용히 폴백되던 문제 때문에 추가했고, 튜닝값이 없어 전부 중립(1.00)이다 — 공격을 안 하는 RangedSupport(사제·음유시인)는 이 행이 호출되지 않는다.
	private static readonly float[,] RoleMultiplierTable =
	{
		//                    적MeleeTank  적MeleeDps  적RangedDps  적MeleeSupport  적RangedSupport
		/* MeleeTank    */  { 1.10f,      1.20f,      1.00f,       1.00f,          1.00f },
		/* MeleeDps     */  { 0.90f,      1.10f,      1.15f,       1.10f,          1.20f },
		/* RangedDps    */  { 0.90f,      1.00f,      1.20f,       1.15f,          1.30f },
		/* MeleeSupport */  { 1.00f,      1.15f,      1.00f,       1.05f,          1.10f },
		/* RangedSupport*/  { 1.00f,      1.00f,      1.00f,       1.00f,          1.00f },
	};

	public static float RoleMultiplier(CombatRole attackerRole, CombatRole defenderRole)
	{
		int row = (int)attackerRole;
		if (row < 0 || row >= RoleMultiplierTable.GetLength(0)) row = (int)CombatRole.MeleeTank; // 정의되지 않은 값 방어적 폴백(현재 CombatRole 5종은 전부 행이 있어 도달 안 함).
		int col = (int)defenderRole;
		return RoleMultiplierTable[row, col];
	}

	// ── 3장: 공격 목표 점수 ─────────────────────────────────────────────
	// 인류: 개인위험도 × 적 역할 배율 × 대상 종류 배율. 플레이어·야생: 종류 설정(현재는 역할 배율을
	// 그대로 초기 출발값으로 사용, 종별 개별 설정은 후속 데이터) × 적 역할 배율 × 대상 종류 배율.
	public static float HumanAttackScore(float personalDanger, CombatRole selfRole, CombatRole targetRole, TargetCategory targetCategory)
		=> personalDanger * RoleMultiplier(selfRole, targetRole) * TargetCategoryMultiplier(targetCategory);

	public static float MonsterAttackScore(CombatRole selfRole, CombatRole targetRole, TargetCategory targetCategory)
		=> RoleMultiplier(selfRole, targetRole) * TargetCategoryMultiplier(targetCategory);

	// ── 3장: 인류 20% 교체 기준 / 몬스터·야생은 더 높으면 교체 ──────────────
	// 새 점수가 현재의 1.2배 '이상'인가 — 비전투 목표(PartyGoalMath)와 공격 대상 교체가 같은 규칙이며, 100 × 1.2f = 120.00001 같은 부동소수 오차로 정확히 1.2배가 떨어지지 않게 허용 오차를 둔다.
	public static bool MeetsSwitchRatio(float currentScore, float newScore) => newScore >= currentScore * 1.2f - 1e-3f;

	// 인류는 1.2배 이상일 때만 교체한다(양쪽 0처럼 새 점수가 더 높지 않으면 유지 — 동점 유지), 몬스터·야생은 더 높기만 하면 교체.
	public static bool ShouldSwitchAttackTarget(bool isHuman, float currentScore, float newScore)
		=> isHuman ? (MeetsSwitchRatio(currentScore, newScore) && newScore > currentScore) : newScore > currentScore;

	// ── 8장: 긴급 아군 보호 후보 조건 ───────────────────────────────────
	// 둘째 조건(치명적 공격 예상)은 호출부(CombatFSMState.TryFindLethalThreat)가 보호자 개인의 예상 피해량과 진행 중인 공격으로 판정해 넘긴다 — 피해량을 모르면 false라 그 조건만 빠지고 나머지로 판단한다.
	public const float EmergencyProtectHpRatio = 0.30f;

	// 9장: 노출 경로 접근 시 '첫 보호효과 시점 이동자 자기 HP' 잔여율 허용 하한. EmergencyProtectHpRatio(보호 시작 조건)와 우연히 같은 수치지만 별개 값이다(원문: 조정 가능한 별도 값).
	public const float ProtectApproachDamageRiskHpFloor = 0.30f;

	// 피해를 감수하는 노출 경로는 확보한 피해 정보로 남을 HP를 추정할 수 있고 예상 HP가 하한 이상일 때만 후보에 든다(9장). 추정할 수 없으면 0 피해로 계산하지도 허용하지도 않고 그 노출 경로를 제외한다.
	public static bool IsExposureRouteAllowed(bool damageEstimable, float projectedHpRatio)
		=> damageEstimable && projectedHpRatio >= ProtectApproachDamageRiskHpFloor;

	public static bool IsEmergencyProtectCandidate(float hpRatio, bool underAttackThreat, bool isIncapacitated, bool isHitThisTurn, bool lethalAttackExpected)
	{
		if (hpRatio <= EmergencyProtectHpRatio && underAttackThreat) return true;
		if (lethalAttackExpected) return true;
		if (isIncapacitated && isHitThisTurn) return true;
		return false;
	}

	// 방어 적용 후 피해 — 실제 피해 파이프라인(TakePhysicalDamage/TakeMagicalDamage)과 같은 규칙: 방어력을 빼고 1 아래로 내려가지 않는다.
	public static float DamageAfterDefense(float rawDamage, float defense) => Mathf.Max(1f, rawDamage - defense);

	// 8장 둘째 조건: 알려진 한 번의 공격 피해가 보호 대상의 남은 HP 이상이면 치명적이다(여러 공격을 합산하지 않는다 — 문서가 "해당 공격"으로 적는다).
	public static bool IsLethalAttack(float rawDamage, float defense, float targetHp) => DamageAfterDefense(rawDamage, defense) >= targetHp;

	// ── 9장: 여러 긴급 후보의 비교 ──────────────────────────────────────
	// 비교 순서: 1 치명적 공격 예상 → 2 그 공격의 도달 시점이 빠름 → 3 HP 비율 낮음 → 4 행동불능 → 5 자신의 보호 효과 도달(거리로 근사). 6(현재 대상 유지·동률 무작위)은 호출부가 맡고, LethalEtaSeconds는 치명적일 때만 의미가 있어 아니면 0으로 정규화한다.
	public readonly struct ProtectCandidateKey : System.IEquatable<ProtectCandidateKey>
	{
		public readonly bool Lethal;
		public readonly float LethalEtaSeconds;
		public readonly float HpRatio;
		public readonly bool Incapacitated;
		public readonly float Distance;

		public ProtectCandidateKey(bool lethal, float lethalEtaSeconds, float hpRatio, bool incapacitated, float distance)
		{
			Lethal = lethal;
			LethalEtaSeconds = lethal ? lethalEtaSeconds : 0f;
			HpRatio = hpRatio;
			Incapacitated = incapacitated;
			Distance = distance;
		}

		public bool Equals(ProtectCandidateKey o)
			=> Lethal == o.Lethal && LethalEtaSeconds == o.LethalEtaSeconds && HpRatio == o.HpRatio && Incapacitated == o.Incapacitated && Distance == o.Distance;
		public override bool Equals(object obj) => obj is ProtectCandidateKey o && Equals(o);
		public override int GetHashCode() => (Lethal, LethalEtaSeconds, HpRatio, Incapacitated, Distance).GetHashCode();
	}

	// true면 candidate가 incumbent보다 우선한다(완전 동률이면 false).
	public static bool IsBetterProtectCandidate(in ProtectCandidateKey candidate, in ProtectCandidateKey incumbent)
	{
		if (candidate.Lethal != incumbent.Lethal) return candidate.Lethal;
		if (candidate.Lethal && candidate.LethalEtaSeconds != incumbent.LethalEtaSeconds) return candidate.LethalEtaSeconds < incumbent.LethalEtaSeconds;
		if (candidate.HpRatio != incumbent.HpRatio) return candidate.HpRatio < incumbent.HpRatio;
		if (candidate.Incapacitated != incumbent.Incapacitated) return candidate.Incapacitated;
		return candidate.Distance < incumbent.Distance;
	}

	// ── 7장: 일반 치료 시작 기준 (HP 비율 미만) ─────────────────────────
	// 문서는 몽크(근접지원)·사제(원거리지원) 두 예시만 준다 — 나머지 원거리지원(주술사/음유시인)도
	// 같은 80%를 쓰지만, 실제로 치유 스킬을 보유한 건 지금 데이터상 사제뿐이다.
	public static float GeneralHealThresholdRatio(CombatRole healerRole)
		=> healerRole == CombatRole.MeleeSupport ? 0.60f : 0.80f;

	// ── 5장: 공격 대상 변경 시 이동 한도(칸) ────────────────────────────
	// 원거리 지원도 다른 역할과 같이 SelectAttackTarget/GetPriority를 그대로 거치므로 default 케이스가 나머지 역할과 동일하게 4를 받는다 — 별도 분기가 필요 없어 명시 케이스를 추가하지 않았다.
	public static int AttackRetargetMoveLimit(CombatRole role) => role switch
	{
		CombatRole.MeleeTank    => 4,
		CombatRole.MeleeDps     => 6,
		CombatRole.RangedDps    => 4,
		CombatRole.MeleeSupport => 4, // "근접 지원이 공격 중인 경우" — 치료 이동에는 적용하지 않음(호출부 책임)
		_                       => 4,
	};

	// 5장 '새 대상을 공격할 수 있는 위치까지의 경로 길이': 경로 타일(시작 제외, 마지막이 대상 타일)을 따라 처음 공격 거리(체비셰프) 안에 들어오는 지점까지의 걸음 수. 이미 거리 안이면 0칸이라 호출부가 먼저 처리하며, 거리 안에 드는 타일이 없으면 전체 길이다.
	public static int StepsToFirstTileWithinRange(IList<Vector2Int> pathTiles, Vector2Int target, int range)
		=> StepsToFirstTileWithinRange(pathTiles, target, Vector2Int.one, range);

	// 목표가 여러 타일을 차지하면(보스 3×3 등) 앵커 한 점이 아니라 점유 영역까지의 거리로 잰다(MovementMath.DistanceToFootprint).
	public static int StepsToFirstTileWithinRange(IList<Vector2Int> pathTiles, Vector2Int targetAnchor, Vector2Int targetSize, int range)
	{
		for (int i = 0; i < pathTiles.Count; i++)
			if (MovementMath.DistanceToFootprint(pathTiles[i], targetAnchor, targetSize) <= range) return i + 1;
		return pathTiles.Count;
	}
}
