using UnityEngine;

// 후퇴·도주 이동 판정(순수) — 후퇴도주 명세서 5장. 우회 한도·함정 강행 조건·도주 속도·통과 순서. 실제 이동·경로 탐색은 RetreatMovement가 이 값들을 읽어 쓴다.
public static class RetreatPathMath
{
	public const int DefaultDetourExtraTiles = 6; // 조정 가능한 설정값
	public const float DefaultSprintMultiplier = 1.5f; // 자리표시자 — SprintSpeed 전용 스탯이 없어 걷기 속도에 곱한다(유닛 능력치 문서가 확정하면 교체)

	// 우회 허용: 우회 경로 길이 − 기준 경로 길이 ≤ 한도(경로 전체가 아니라 추가 이동 거리). 기준 4칸이면 10칸 허용, 11칸 불허. totalDetourLength는 같은 차단 구간에서 이미 우회해 이동한 거리를 합산한 값이어야 한다(재탐색으로 한도를 초기화하지 않음).
	public static bool IsDetourAllowed(int baselineLength, int totalDetourLength, int extraTileLimit = DefaultDetourExtraTiles)
		=> totalDetourLength - baselineLength <= extraTileLimit;

	// 길을 막은 상대 진영 문 후보 중 먼저 부술 문의 번호 — 유닛(기준 경로를 따라 갈 수 있는 마지막 칸 end)에 가장 가까운 문, 동률이면 목적지에 가까운 문. 게이트는 두 줄(방 쪽·바깥 쪽)이라 목적지에 가까운 먼 줄 문을 고르면 바로 앞 줄 문에 막혀 닿지 못하고 막힘 판정이 뒤집혔다(검증 D2). 후보가 없으면 -1.
	public static int ChooseBlockingDoor(Vector2Int end, Vector2Int target, System.Collections.Generic.IList<Vector2Int> doorTiles)
	{
		int best = -1, bestFromEnd = int.MaxValue, bestToTarget = int.MaxValue;
		for (int i = 0; i < doorTiles.Count; i++)
		{
			int fromEnd = Mathf.Max(Mathf.Abs(doorTiles[i].x - end.x), Mathf.Abs(doorTiles[i].y - end.y));
			int toTarget = Mathf.Max(Mathf.Abs(doorTiles[i].x - target.x), Mathf.Abs(doorTiles[i].y - target.y));
			if (fromEnd < bestFromEnd || (fromEnd == bestFromEnd && toTarget < bestToTarget))
			{
				best = i; bestFromEnd = fromEnd; bestToTarget = toTarget;
			}
		}
		return best;
	}

	// 함정 강행 통과: 알고 있는 예상 피해를 반영한 HP가 0보다 클 때만 허용. 피해량을 모르거나(null) 예상 HP가 0 이하면 강행하지 않는다. 별도 수치 랜덤 오차·최댓값 추정은 쓰지 않는다.
	public static bool CanForcePassTrap(float currentHp, float? expectedDamage)
		=> expectedDamage.HasValue && currentHp - expectedDamage.Value > 0f;

	// 개인 도주는 달리기(SprintSpeed), 파티 후퇴·리더 없는 개인 후퇴는 걷기(WalkSpeed).
	public static float SprintSpeed(float walkSpeed, float multiplier = DefaultSprintMultiplier) => walkSpeed * multiplier;
	public static float RetreatSpeed(RetreatMode mode, float walkSpeed, float sprintMultiplier = DefaultSprintMultiplier)
		=> mode == RetreatMode.PersonalFlee ? SprintSpeed(walkSpeed, sprintMultiplier) : walkSpeed;

	// 같은 차단 구간의 우회 누적 거리 = 이미 우회해 걸은 칸 + 남은 우회 경로. 구간이 시작될 때 잰 기준 경로 길이를 구간이 끝날 때까지 유지해 재탐색으로 한도를 초기화하지 않는다(R13).
	public static int TotalDetourLength(int walkedInSegment, int remainingDetourLength) => walkedInSegment + remainingDetourLength;

	// 후퇴 중 좁은 통로 통과 순서(명세서 5-1장). 파티 후퇴: 사제형 → 궁수형 → 지휘관형 → 기사형 = 평소 입장 순서(OccupancyMath.EntryRank)의 정반대(원거리 지원 → 원거리 공격 → 근접 지원 → 전방 근접). 4개 클래스 밖의 직업·정예는 후속 설계라 같은 역할 규칙으로 일반화한다.
	public static int PartyRetreatRank(int entryRank) => 3 - entryRank;

	// 개인 도주 통과 순서: 현재 이동속도 기준 예상 탈출시간이 짧은 유닛이 먼저이고 동률이면 궁수형 → 사제형 → 지휘관형 → 기사형(= 원거리 공격 → 원거리 지원 → 근접 지원 → 전방 근접). PassKey.Rank(정수) 하나로 비교되도록 탈출 시간을 0.1초 단위로 올림 정수화해 역할 동률 순위를 아래 자리에 붙인다.
	public const int FleeRankOffset = -1000000; // 도주자는 같은 통로의 일반 순위(0~3)보다 항상 먼저 — 도주는 모든 행동보다 출구 이동이 우선이다

	public static int FleeRank(float escapeSeconds, int entryRank)
	{
		int tieOrder;
		switch (entryRank)
		{
			case 2: tieOrder = 0; break; // 원거리 공격(궁수형)
			case 3: tieOrder = 1; break; // 원거리 지원(사제형)
			case 1: tieOrder = 2; break; // 근접 지원(지휘관형)
			default: tieOrder = 3; break; // 전방 근접(기사형)
		}
		int tenths = escapeSeconds <= 0f ? 0 : Mathf.Min(100000, Mathf.CeilToInt(escapeSeconds * 10f));
		return FleeRankOffset + tenths * 4 + tieOrder;
	}

	// 남은 이동 칸수 ÷ 속도(칸/초) = 예상 탈출 시간(초). 속도가 0 이하면 아주 느린 값으로 둔다.
	public static float EscapeSeconds(int remainingSteps, float speed) => Mathf.Max(0, remainingSteps) / Mathf.Max(0.1f, speed);
}

// 집결 대기·미집결자 수색 기한(순수) — 후퇴도주 명세서 6장. 집결 명령은 같은 방 전원에게 전달되고 실제 합류까지 시간이 필요하다.
public static class RallySearchMath
{
	public const float DefaultWaitSeconds = 8f;        // 조정 가능한 설정값
	public const float ArrivalEstimateGraceSeconds = 3f;
	public const float DispersedSearchSeconds = 5f;    // 미집결자 마지막 위치 분산 수색
	public const float DeathCauseSearchSeconds = 5f;   // 찾던 인원의 시체 발견 후 원인 수색

	// 유효한 도착 예정 시각이 있으면 예정+3초, 없으면 명령 발령+8초. 더 늦은 쪽을 고르는 방식은 쓰지 않는다. 같은 정보를 다시 받아도 기한을 미루지 않는다(호출부가 최초 수신만 반영).
	public static float Deadline(float commandTime, float? arrivalEstimate, float waitSeconds = DefaultWaitSeconds)
		=> arrivalEstimate.HasValue ? arrivalEstimate.Value + ArrivalEstimateGraceSeconds : commandTime + waitSeconds;
}
