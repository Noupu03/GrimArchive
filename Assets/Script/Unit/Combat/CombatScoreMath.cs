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

	// 3장 4x5 표(초기 조정값). 근접 지원 행은 "공격 행동을 선택했을 때만" 쓴다(문서 표 아래 설명).
	// 원거리 지원 행은 검증문서 02-03에서 추가(2026-09-28) — 원래 "공격 점수표 적용 대상 아님(공격
	// 기능 없음)"으로 행 자체가 없었으나, 같은 RangedSupport로 매핑된 주술사가 실제로는 적 대상
	// 스킬(Curse, Affinity=Enemy)을 갖고 있어 MeleeTank 행으로 조용히 폴백되는 문제가 있었다. 튜닝된
	// 배율값이 없어 다른 역할 행을 빌려 쓰는 대신 전부 중립(1.00)으로 채웠다 — 사제·음유시인처럼
	// 실제로 공격을 안 하는 RangedSupport 유닛에는 이 행 자체가 호출되지 않으므로 영향이 없다.
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
	public static bool ShouldSwitchAttackTarget(bool isHuman, float currentScore, float newScore)
		=> isHuman ? newScore > currentScore * 1.2f : newScore > currentScore;

	// ── 8장: 긴급 아군 보호 후보 조건 ───────────────────────────────────
	// 치명적 공격 예상 조건(2번째 확인 항목)은 신뢰할 만한 피해 추정치 연결이 아직 없어(9장 피해
	// 추정 한계 참고) 이번 구현에서는 사용하지 않는다 — 저체력+위협, 행동불능+피격 두 조건만 확인한다.
	public const float EmergencyProtectHpRatio = 0.30f;

	// 9장: 노출 경로로 접근할 때 "첫 보호효과 시점 이동자 자기 HP" 잔여율 허용 하한. 위
	// EmergencyProtectHpRatio(누구를 보호할지 시작 조건)와 우연히 같은 수치지만 의미가 다른 별개
	// 값이다 — 원문이 "조정 가능한 별도 값"이라고 명시.
	public const float ProtectApproachDamageRiskHpFloor = 0.30f;

	// 9장·순서도 02-07: 피해를 감수하는 노출 경로는 "확보한 피해 정보로 남을 HP를 추정할 수 있고" 그 예상 HP가 하한 이상일 때만 후보에 든다. 추정할 수 없으면
	// 0 피해로 계산하지도, 계산에서 빼고 허용하지도 않고 해당 노출 경로를 제외한다(검증 02-10 재판정, 2026-09-30).
	public static bool IsExposureRouteAllowed(bool damageEstimable, float projectedHpRatio)
		=> damageEstimable && projectedHpRatio >= ProtectApproachDamageRiskHpFloor;

	public static bool IsEmergencyProtectCandidate(float hpRatio, bool underAttackThreat, bool isIncapacitated, bool isHitThisTurn)
	{
		if (hpRatio <= EmergencyProtectHpRatio && underAttackThreat) return true;
		if (isIncapacitated && isHitThisTurn) return true;
		return false;
	}

	// ── 9장: 여러 긴급 후보의 비교 (치명타 예상 조건 생략, 위 참고) ─────────
	// true면 candidate가 incumbent보다 우선한다. 3번(HP비율 낮음) → 4번(행동불능) → 5번(자신의 보호
	// 효과 도달시간, 거리로 근사) 순. 1~2번(치명타 예상)은 생략.
	public static bool IsBetterProtectCandidate(
		float candidateHpRatio, bool candidateIncapacitated, float candidateDist,
		float incumbentHpRatio, bool incumbentIncapacitated, float incumbentDist)
	{
		if (candidateHpRatio != incumbentHpRatio) return candidateHpRatio < incumbentHpRatio;
		if (candidateIncapacitated != incumbentIncapacitated) return candidateIncapacitated;
		return candidateDist < incumbentDist;
	}

	// ── 7장: 일반 치료 시작 기준 (HP 비율 미만) ─────────────────────────
	// 문서는 몽크(근접지원)·사제(원거리지원) 두 예시만 준다 — 나머지 원거리지원(주술사/음유시인)도
	// 같은 80%를 쓰지만, 실제로 치유 스킬을 보유한 건 지금 데이터상 사제뿐이다.
	public static float GeneralHealThresholdRatio(CombatRole healerRole)
		=> healerRole == CombatRole.MeleeSupport ? 0.60f : 0.80f;

	// ── 5장: 공격 대상 변경 시 이동 한도(칸) ────────────────────────────
	// [정정, 2026-09-28] 원거리 지원도 다른 역할과 동일하게 SelectAttackTarget/GetPriority를 그대로
	// 거친다 — "공격 후보로 안 삼는다"는 예전 서술은 검증문서 02-03에서 확인 결과 부정확했다(RangedSupport
	// 클래스 대부분이 실제로 공격을 안 해서 관측되지 않았을 뿐). 그래서 default 케이스로 나머지 역할과
	// 동일한 4를 받는다 — 별도 분기가 필요 없어 명시 케이스를 추가하지 않았다.
	public static int AttackRetargetMoveLimit(CombatRole role) => role switch
	{
		CombatRole.MeleeTank    => 4,
		CombatRole.MeleeDps     => 6,
		CombatRole.RangedDps    => 4,
		CombatRole.MeleeSupport => 4, // "근접 지원이 공격 중인 경우" — 치료 이동에는 적용하지 않음(호출부 책임)
		_                       => 4,
	};

	// 5장 "새 대상을 공격할 수 있는 위치까지의 경로 길이": 경로 타일(시작 제외, 마지막이 대상 타일)을 따라 처음으로 공격 거리(체비셰프) 안에 들어오는 지점까지의 걸음 수.
	// 이미 공격 거리 안이면 0칸이라 호출부가 경로를 재기 전에 먼저 처리한다. 거리 안에 드는 타일이 없으면(정상 경로는 마지막 타일 = 대상 타일이라 항상 해당) 전체 길이.
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
