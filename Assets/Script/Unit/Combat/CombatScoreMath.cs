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
	// 원거리 지원은 공격 점수표 적용 대상이 아니라 행 자체가 없다(호출 안 함).
	private static readonly float[,] RoleMultiplierTable =
	{
		//                    적MeleeTank  적MeleeDps  적RangedDps  적MeleeSupport  적RangedSupport
		/* MeleeTank    */  { 1.10f,      1.20f,      1.00f,       1.00f,          1.00f },
		/* MeleeDps     */  { 0.90f,      1.10f,      1.15f,       1.10f,          1.20f },
		/* RangedDps    */  { 0.90f,      1.00f,      1.20f,       1.15f,          1.30f },
		/* MeleeSupport */  { 1.00f,      1.15f,      1.00f,       1.05f,          1.10f },
	};

	public static float RoleMultiplier(CombatRole attackerRole, CombatRole defenderRole)
	{
		int row = (int)attackerRole;
		if (row < 0 || row >= RoleMultiplierTable.GetLength(0)) row = (int)CombatRole.MeleeTank; // RangedSupport 폴백
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
	// 원거리 지원(공격 기능 없음)은 이 한도 자체가 적용 대상이 아니다 — 호출부가 애초에 공격 후보로
	// 삼지 않으므로 여기서는 그 경우를 별도로 처리하지 않는다.
	public static int AttackRetargetMoveLimit(CombatRole role) => role switch
	{
		CombatRole.MeleeTank    => 4,
		CombatRole.MeleeDps     => 6,
		CombatRole.RangedDps    => 4,
		CombatRole.MeleeSupport => 4, // "근접 지원이 공격 중인 경우" — 치료 이동에는 적용하지 않음(호출부 책임)
		_                       => 4,
	};
}
