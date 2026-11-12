using System;
using UnityEngine;

// 건설 설계명세서 v0.2(2026-11-12)의 순수 판정·수식 모음 — 부수효과 없음(테스트 용이). 수치 중 "자리표시자"는 문서가 콘텐츠 문서로 미룬 값이다.
public static class ConstructionRules
{
	// ── 2-4. 손상에 따른 효율 ──
	// e = 0.5 + 0.5 × 남은 HP 비율. 최대 HP의 2%를 잃을 때마다 적용 성능이 1%씩 줄어든다.
	public static double DamageCoefficient(float hp, float maxHp)
	{
		if (maxHp <= 0f) return 1.0;
		double ratio = Math.Min(1.0, Math.Max(0.0, hp / (double)maxHp));
		return 0.5 + 0.5 * ratio;
	}

	// 전투 시설의 공격 피해에 곱하는 손상 계수 — 방어력 계산 뒤, 방어 행동의 피해 감소 전에 적용한다(최종 내림·유효 적중 최소 1은 전투 공식 몫).
	public static double ApplyToCombatDamage(double damageAfterDefense, float hp, float maxHp)
		=> damageAfterDefense * DamageCoefficient(hp, maxHp);

	// 내부 HP는 소수까지 보관하고 화면은 반올림한다. 생존 HP가 1 미만이면 1을 표시한다(2-2).
	public static int DisplayHp(float hp)
	{
		if (hp <= 0f) return 0;
		return Math.Max(1, (int)Math.Round(hp, MidpointRounding.AwayFromZero));
	}

	// ── 2-2. 건설 중 HP / 2-7. 재설치 HP ──
	// 건설 단계 최대 HP는 완성 시설 최대 HP의 일부다(문서 예: 완성 1,000 → 건설 100). 종류별 수치는 오브젝트 종류 문서 소관이라 임시 비율을 쓴다.
	public const float ConstructionStageHpRatio = 0.1f; // 자리표시자

	public static float ConstructionStageMaxHp(float completedMaxHp)
		=> Math.Max(1f, completedMaxHp * ConstructionStageHpRatio);

	// 재설치 시작: 이동 전 HP 비율 × 재설치 단계 최대 HP (600/1,000 × 100 = 60).
	public static float ReinstallStartHp(float hpBeforeMove, float maxHpBeforeMove, float reinstallStageMaxHp)
		=> RatioOf(hpBeforeMove, maxHpBeforeMove) * reinstallStageMaxHp;

	// 재설치 완료: 재설치 남은 HP 비율 × 완성 시설 최대 HP (40/100 × 1,000 = 400).
	public static float ReinstallCompleteHp(float reinstallHp, float reinstallStageMaxHp, float completedMaxHp)
		=> RatioOf(reinstallHp, reinstallStageMaxHp) * completedMaxHp;

	// 최대 HP가 바뀌는 완료(강화·문 확장)는 남은 HP 비율을 새 최대 HP에 적용한다(120/200 → 1.5배 300 → 180).
	public static float HpAfterMaxChange(float hp, float oldMaxHp, float newMaxHp)
		=> RatioOf(hp, oldMaxHp) * newMaxHp;

	// 신규 건설 완료 HP — 완성 시설의 최대 HP로 전부 회복한다(2-2-표1). 강화·재설치와 달리 비율을 승계하지 않는다.
	public static float NewConstructionCompleteHp(float completedMaxHp) => completedMaxHp;

	private static float RatioOf(float hp, float maxHp)
		=> maxHp > 0f ? Math.Min(1f, Math.Max(0f, hp / maxHp)) : 1f;

	// ── 2-6. 시설 회전 / 2-10. 정면 ──
	public static int NormalizeRotation(int quarterTurns) => ((quarterTurns % 4) + 4) % 4;

	// 시계 방향 90도 회전 수만큼 점유 크기를 돌린다(홀수 회전이면 가로·세로가 바뀐다).
	public static Vector2Int RotatedFootprint(Vector2Int footprint, int quarterTurns)
		=> (NormalizeRotation(quarterTurns) % 2 == 0) ? footprint : new Vector2Int(footprint.y, footprint.x);

	// 정면 방향: 0=아래(-y), 1=왼쪽(-x), 2=위(+y), 3=오른쪽(+x) - 시계 방향 회전 순서.
	public static Vector2Int FrontDirection(int rotation)
	{
		switch (NormalizeRotation(rotation))
		{
			case 1: return new Vector2Int(-1, 0);
			case 2: return new Vector2Int(0, 1);
			case 3: return new Vector2Int(1, 0);
			default: return new Vector2Int(0, -1);
		}
	}

	// 정면 출현 구역: 시설 변 하나를 따라 깊이만큼 이어지는 직사각형(회전 방향을 따른다).
	public static RectInt FrontZoneRect(Vector2Int origin, Vector2Int footprint, int rotation, int depth)
	{
		Vector2Int d = FrontDirection(rotation);
		if (d.y < 0) return new RectInt(origin.x, origin.y - depth, footprint.x, depth);
		if (d.y > 0) return new RectInt(origin.x, origin.y + footprint.y, footprint.x, depth);
		if (d.x < 0) return new RectInt(origin.x - depth, origin.y, depth, footprint.y);
		return new RectInt(origin.x + footprint.x, origin.y, depth, footprint.y);
	}

	// 구역 안에서 유닛 몸 전체가 들어가는 빈 출현 위치를 구역 중심에 가까운 순으로 찾는다. 구역보다 큰 유닛은 나오지 못한다(소형 시설은 강화해도 대형 유닛 생산 불가).
	public static bool TryFindFrontSpawnOrigin(RectInt zone, Vector2Int unitFootprint, Func<Vector2Int, Vector2Int, bool> isClear, out Vector2Int origin)
	{
		origin = default;
		if (unitFootprint.x > zone.width || unitFootprint.y > zone.height) return false;
		float cx = zone.x + zone.width / 2f, cy = zone.y + zone.height / 2f;
		bool found = false;
		float best = float.MaxValue;
		for (int x = zone.xMin; x + unitFootprint.x <= zone.xMax; x++)
		{
			for (int y = zone.yMin; y + unitFootprint.y <= zone.yMax; y++)
			{
				var cand = new Vector2Int(x, y);
				float d = Mathf.Abs(x + unitFootprint.x / 2f - cx) + Mathf.Abs(y + unitFootprint.y / 2f - cy);
				if (d >= best || !isClear(cand, unitFootprint)) continue;
				best = d;
				origin = cand;
				found = true;
			}
		}
		return found;
	}

	// ── 2-3. 실제 수리와 시설 가동 ──
	// 실제 수리·강화·분해·철거 작업이 진행되는 동안에만 시설 가동을 멈춘다. 등록만 됐거나 작업자가 접근 중이거나, 차액 승인 대기 등으로 실제 작업이 정지한 동안엔 다른 조건을 충족하면 가동한다.
	public static bool IsFieldWorkStoppingFacility(bool hasStartedWork, int actualWorkerCount, bool awaitingApproval)
		=> hasStartedWork && actualWorkerCount > 0 && !awaitingApproval;

	// ── 1-5. 전투 후 자동 재개 ──
	// 전투를 끝내고 전투 후 경계(10초)까지 마쳐야 작업 복귀 조건을 검사한다. 현재 상태가 전투·경계가 아니고 강제 이동이 없을 때만 복귀한다.
	public static bool ShouldResumeAssignedWork(bool hasAssignment, bool atWorkSpot, bool inCombatOrAlert, bool hasPendingMove)
		=> hasAssignment && !atWorkSpot && !inCombatOrAlert && !hasPendingMove;

	// ── 1-3. 비용 처리 ── (80을 선점한 예약이 70에 시작하면 70을 차감하고 선점 80을 해제해 차액 10을 다시 쓴다)
	public static int ReleasedAfterStart(int reserved, int actualPaid) => Math.Max(0, reserved - actualPaid);
}
