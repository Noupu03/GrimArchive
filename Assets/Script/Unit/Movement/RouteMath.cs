using System.Collections.Generic;
using UnityEngine;

// 경로 평가 순수 계산(검증 04-08, 04번 문서 9장 "전체 길 미확인과 통행 불가의 구분 / 미확인 경로의 추정 이동거리"). 부수효과 없음 — RouteAssessment가 유닛·탐색 결과를 읽어 넘긴다.

// 목적지까지의 길에 대한 현재 정보 기준 판정.
public enum RouteStatus
{
	FullyKnown,    // 개인 지도로 확인한 길만으로 도달한다 — 실제 경로 칸수.
	PartlyUnknown, // 도달은 하지만 길 일부가 아직 미확인이다(또는 점유 때문에 일시적으로만 막힘) — 추정 이동거리.
	TrapBlocked,   // 알려진 함정 구역 때문에만 못 간다 — 후보로 남긴다(접근 시도에서 03-13 막힘 신호가 나와 함정 대응이 재개된다).
	Unreachable,   // 점유를 무시한 구조 경로로도, 함정을 꺼도 길이 없다 — 현재 정보로 통행 불가가 확인됐다.
}

public readonly struct RouteEstimate
{
	public readonly RouteStatus Status;
	public readonly int Tiles;

	public RouteEstimate(RouteStatus status, int tiles)
	{
		Status = status;
		Tiles = tiles;
	}
}

// 통행 불가 판정 당시 "경로 판정에 영향을 주는 아는 정보"의 서명 — 이 값이 그대로면 같은 대상을 다시 탐색해도 같은 결론이다.
// 층 / 개인이 아는 지형 개정(IKnownTerrain.TerrainRevision) / 아는 함정 수 / 문 상태 개정(DoorSystem.StateVersion). 점유는 구조 경로가 무시하므로 포함하지 않는다.
public readonly struct RouteSignature : System.IEquatable<RouteSignature>
{
	public readonly int Floor;
	public readonly int Terrain;
	public readonly int Traps;
	public readonly int Doors;

	public RouteSignature(int floor, int terrain, int traps, int doors)
	{
		Floor = floor;
		Terrain = terrain;
		Traps = traps;
		Doors = doors;
	}

	public bool Equals(RouteSignature other) => Floor == other.Floor && Terrain == other.Terrain && Traps == other.Traps && Doors == other.Doors;
	public override bool Equals(object obj) => obj is RouteSignature other && Equals(other);
	public override int GetHashCode() => (((Floor * 31 + Terrain) * 31) + Traps) * 31 + Doors;
}

// 통행 불가로 판정한 대상의 메모 — 판정 시 서명과 시각.
public struct RouteMemoEntry
{
	public RouteSignature Signature;
	public float Time;
}

public static class RouteMath
{
	// 통행 불가로 확인된 대상의 거리 — 점수(흥미도 ÷ max(1, 거리))와 이동 예상시간에서 사실상 선택되지 않을 만큼 크게 둔다. int 합산·초 환산에서 넘치지 않는 값.
	public const int UnreachableDistanceTiles = 9999;

	public static int ChebyshevDistance(Vector2Int a, Vector2Int b)
		=> Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

	// 04번 9장: 추정 이동거리 = 시야를 넓힐 위치(프론티어)까지의 실제 경로 칸수 + 그 위치에서 목적지까지 장애물이 없을 때의 최소 칸수(8방향 기준 체비셰프).
	// 이동할 수 있는 방향이 여럿이면(프론티어가 여럿) 추정값이 가장 짧은 쪽을 쓴다. 도달 가능한 프론티어가 없으면 null.
	// 예: 알려진 구간 4칸 + 남은 구간 최소 6칸 = 10칸.
	public static int? EstimateViaFrontier(IReadOnlyDictionary<Vector2Int, int> frontierSteps, Vector2Int target)
	{
		int? best = null;
		foreach (var kv in frontierSteps)
		{
			int total = kv.Value + ChebyshevDistance(kv.Key, target);
			if (best == null || total < best.Value) best = total;
		}
		return best;
	}

	// 04번 9장 "조건이 그대로인 대상을 즉시 다시 선택해 같은 실패를 반복하지 않는다": 통행 불가로 확인된 대상만 후보에서 뺀다.
	// 일시적 점유 막힘은 04-05 대기·우회가, 함정 구역 막힘은 03-13 함정 대응이 처리하므로 후보에 남긴다.
	public static bool ShouldExcludeFromCandidates(RouteStatus status) => status == RouteStatus.Unreachable;

	// 통행 불가 판정 메모의 유효성 — 판정 때 서명과 같으면 유효하다. 서명이 바뀌었어도 판정한 지 minRecheckSeconds가 안 지났으면 유효로 본다(탐험 중 아는 지형이
	// 계속 바뀌어 매 틱 다시 탐색하는 비용 폭주를 막는 성능 장치 — 정보가 바뀌었다는 사실이 이 시간만큼 늦게 반영될 뿐 판정 기준은 그대로다).
	public static bool IsUnreachableMemoValid(RouteMemoEntry memo, RouteSignature current, float now, float minRecheckSeconds)
	{
		if (memo.Signature.Equals(current)) return true;
		return now - memo.Time < minRecheckSeconds;
	}
}
