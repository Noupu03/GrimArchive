using System.Collections.Generic;
using UnityEngine;

// 생산시설의 정면 출현 구역 규칙·표시(건설 명세 2-10·2-13).
public partial class BuildingManager
{
	// ── 정면 출현 구역(2-10) ──
	// 구역은 가장 큰 생산 가능 유닛에 맞추고 회전 방향을 따른다. 유닛별 크기 데이터가 없어 깊이는 임시 2칸이다(오브젝트 종류·생산시설 종류 문서 소관).
	public const int FrontZoneDepth = 2;

	public static RectInt GetFrontZone(BuildingData b)
		=> ConstructionRules.FrontZoneRect(new Vector2Int(b.Position.x, b.Position.y), b.Footprint, b.Rotation, FrontZoneDepth);

	private static bool HasFrontZone(BuildingData b)
		=> b != null && b.Kind == FacilityKind.UnitProduction;

	// 구역 전체가 서 있을 수 있는 바닥이어야 출현 규칙을 쓴다 - 아니면(옛 시설·시작방 배치) 예전 시설 둘레 출현으로 폴백한다.
	private bool IsFrontZoneUsable(BuildingData b)
	{
		RectInt z = GetFrontZone(b);
		for (int x = z.xMin; x < z.xMax; x++)
			for (int y = z.yMin; y < z.yMax; y++)
				if (createMap != null && !createMap.IsStaticTileWalkable(b.Position.z, new Vector2Int(x, y))) return false;
		return true;
	}

	// 그 타일이 어느 생산시설(건설 계획 포함)의 출현 구역인가 - 구역에는 건설·재설치·방어 위치 저장을 할 수 없다(2-10). 배치모드가 저장 검사에 쓴다.
	public bool IsFrontZoneReserved(Vector3Int pos) => IsInsideOtherFrontZone(pos, null);

	// ignore를 뺀 다른 생산시설의 출현 구역 안인가 - 건설·재설치는 거기에 할 수 없다.
	private bool IsInsideOtherFrontZone(Vector3Int pos, BuildingData ignore)
	{
		for (int i = 0; i < _allBuildings.Count; i++)
		{
			BuildingData p = _allBuildings[i];
			if (p == null || p == ignore || !HasFrontZone(p) || p.Position.z != pos.z) continue;
			RectInt z = GetFrontZone(p);
			if (pos.x >= z.xMin && pos.x < z.xMax && pos.y >= z.yMin && pos.y < z.yMax) return true;
		}
		return false;
	}

	// 출현 직후 같은 방의 가까운 빈 공간(구역·건물 격자 밖)으로 걸어서 구역을 벗어난다 - 이동 중 전투 인지는 일반 이동 기준이고 플레이어 명령이 우선한다(2-10-표1).
	private void LeaveFrontZoneAfterSpawn(Unit m, RectInt zone)
	{
		GameSession session = GameSession.Instance;
		if (m == null || session == null) return;
		bool found = false;
		Vector2Int best = m.position;
		float bestDist = float.MaxValue;
		for (int x = zone.xMin - 8; x < zone.xMax + 8; x++)
		{
			for (int y = zone.yMin - 8; y < zone.yMax + 8; y++)
			{
				if (x >= zone.xMin && x < zone.xMax && y >= zone.yMin && y < zone.yMax) continue;
				Vector3Int t = new Vector3Int(x, y, m.currentFloor);
				if (buildingGrid.ContainsKey(t) || session.unitGrid.ContainsKey(t) || session.objectGrid.ContainsKey(t)) continue;
				if (IsInsideOtherFrontZone(t, null)) continue;
				if (createMap != null && !createMap.IsStaticTileWalkable(t.z, new Vector2Int(x, y))) continue;
				if (!session.roomGrid.TryGetValue(t, out Room dest) || dest != m.currentRoom) continue;
				float d = Vector2Int.Distance(m.position, new Vector2Int(x, y));
				if (d >= bestDist) continue;
				bestDist = d;
				best = new Vector2Int(x, y);
				found = true;
			}
		}
		if (found) IssueInternalEconomyMove(m, best); // 이탈할 공간이 없으면 현재 위치에서 대기한다
	}

	// ── 정면 구역 표시(2-13) ──
	private readonly PerBuildingViews<FrontZoneRenderer> _zoneViews = new PerBuildingViews<FrontZoneRenderer>(
		b => new FrontZoneRenderer($"FrontZone_{b.FacilityId}"), view => view.Destroy());
	private float _nextZoneViewAt;

	private void UpdateFrontZoneViews()
	{
		if (Time.unscaledTime < _nextZoneViewAt) return;
		_nextZoneViewAt = Time.unscaledTime + 0.3f;

		_zoneViews.DestroyStale(_allBuildings);
		for (int i = 0; i < _allBuildings.Count; i++)
		{
			BuildingData b = _allBuildings[i];
			if (b == null || b.Kind != FacilityKind.UnitProduction) continue;
			FrontZoneRenderer view = _zoneViews.GetOrCreate(b);
			bool show = b.VisualObject != null && b.VisualObject.activeSelf;
			if (!show) { view.Hide(); continue; }
			Vector3 off = unitGenerate != null ? unitGenerate.GetFloorOffset(b.Position.z) : Vector3.zero;
			// 정식 설치된 시설은 옅게, 건설 예정·재설치 중에는 건설이 필요한 전체 영역을 알리도록 조금 진하게 보여 준다.
			Color c = b.IsFormallyInstalled ? new Color(0.35f, 0.85f, 0.55f, 0.16f) : new Color(0.95f, 0.8f, 0.3f, 0.28f);
			view.Show(GetFrontZone(b), b.Position.z, off, c);
		}
	}
}
