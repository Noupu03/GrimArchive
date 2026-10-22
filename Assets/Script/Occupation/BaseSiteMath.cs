using System;
using System.Collections.Generic;
using UnityEngine;

// 거점 공사 현장(방 안 건설물 + 바닥 청사진)의 자리와 연출 수치(순수). 코어 점령 v0.2 12장은 공사 연출을 정하지 않아 전부 연출 판단이다.
// 현장 = 중심 타일 + 반경(0이면 1×1, 1이면 3×3 청사진). 영역 안 모든 칸이 비어 있어야 하고, 가능하면 둘레 한 칸도 계단·건물·코어·오브젝트에 닿지 않는 곳을 고른다.
public struct SiteLayout : IEquatable<SiteLayout>
{
	public Vector2Int Center;
	public int Radius;

	public int Size => Radius * 2 + 1;

	public bool Equals(SiteLayout other) => Center == other.Center && Radius == other.Radius;
	public override bool Equals(object obj) => obj is SiteLayout other && Equals(other);
	public override int GetHashCode() => Center.GetHashCode() * 31 + Radius;
}

public static class BaseSiteMath
{
	// 청사진 한 변(타일) = 2 × 반경 + 1. 건설물은 이 영역 한가운데 한 칸에 선다.
	public const int BlueprintRadius = 1;

	// 건설물이 진행도에 따라 올라가는 단계 수(25%마다 한 단).
	public const int StructureStages = 4;

	// 건설물·완공 상징물(천막·망루)의 크기 배율(2026-10-22 사용자 요청 "천막 크기 키우자, 방어 거점도 동일"). 청사진(3×3)이 선 자리는 영역(±1.5칸) 안에 들어가는 한도까지 키우고,
	// 1×1 자리는 이웃 칸의 계단·건물을 덮지 않게 한 칸 크기를 지킨다(겹침 방지가 사용자 요구).
	public const float BlueprintStructureScale = 2.4f;
	public static float StructureScale(int radius) => radius >= BlueprintRadius ? BlueprintStructureScale : 1f;

	// 방 인원 라벨("N/M" TextMesh, GameSession.CreateRoomPopulationLabel)이 방 중심(Room.Bounds.center)에 놓이고 글자가 오브젝트 위(정렬 50)에 그려진다 — 그 아래에 건설물을 세우면 글자가 건설물을 덮는다.
	// 라벨이 차지하는 대략의 반폭·반높이(월드 단위, 글자 크기 48 × 0.11 기준으로 넉넉하게)이고, 이 직사각형에 닿는 칸은 현장 영역에서 뺀다.
	public const float RoomLabelHalfWidth = 1f;
	public const float RoomLabelHalfHeight = 0.4f;

	// tile(한 칸 = [x, x+1] × [y, y+1])이 라벨 직사각형과 겹치는가.
	public static bool IsUnderRoomLabel(Vector2 labelCenter, Vector2Int tile)
		=> tile.x < labelCenter.x + RoomLabelHalfWidth && tile.x + 1 > labelCenter.x - RoomLabelHalfWidth
			&& tile.y < labelCenter.y + RoomLabelHalfHeight && tile.y + 1 > labelCenter.y - RoomLabelHalfHeight;

	// 영역(반경 radius)의 모든 칸이 비어 있는가.
	public static bool IsFootprintClear(SiteLayout layout, Func<Vector2Int, bool> isClear)
	{
		for (int dx = -layout.Radius; dx <= layout.Radius; dx++)
			for (int dy = -layout.Radius; dy <= layout.Radius; dy++)
				if (!isClear(layout.Center + new Vector2Int(dx, dy))) return false;
		return true;
	}

	// 방 경계 안에서 현장 자리를 고른다. 큰 영역(3×3)이 작은 영역(1×1)보다, 둘레 여유(margin)가 있는 자리가 없는 자리보다 먼저다:
	// (3×3, 여유 1) → (3×3, 여유 0) → (1×1, 여유 1) → (1×1, 여유 0). 같은 단계 안에서는 preferred(방 중앙)에 가까운 자리, 동점이면 y가 작은 쪽 → x가 작은 쪽이다.
	// isClear = 영역에 들어가도 되는 칸(방 안, 바닥, 오브젝트·구조물·문 없음), isCrowded = 둘레에 닿으면 곤란한 칸(계단·건물·코어·오브젝트). 둘 다 못 찾으면 false.
	// maxRadius를 0으로 주면 1×1 자리만 고른다. 완공 상징물을 옮길 때는 상징물이 3×3에서 커졌으므로 기본값(3×3 먼저, 없으면 1×1로 줄임)을 쓴다.
	public static bool TryPickSite(RectInt bounds, Vector2 preferred, Func<Vector2Int, bool> isClear, Func<Vector2Int, bool> isCrowded, out SiteLayout layout, int maxRadius = BlueprintRadius)
	{
		for (int tier = 0; tier < 4; tier++)
		{
			int radius = tier < 2 ? BlueprintRadius : 0;
			if (radius > maxRadius) continue;
			int margin = tier % 2 == 0 ? 1 : 0;
			if (TryPickInTier(bounds, preferred, radius, margin, isClear, isCrowded, out layout)) return true;
		}
		layout = default;
		return false;
	}

	private static bool TryPickInTier(RectInt bounds, Vector2 preferred, int radius, int margin, Func<Vector2Int, bool> isClear, Func<Vector2Int, bool> isCrowded, out SiteLayout layout)
	{
		layout = default;
		bool found = false;
		float bestDistance = float.MaxValue;

		for (int y = bounds.yMin + radius; y <= bounds.yMax - 1 - radius; y++)
		{
			for (int x = bounds.xMin + radius; x <= bounds.xMax - 1 - radius; x++)
			{
				var candidate = new SiteLayout { Center = new Vector2Int(x, y), Radius = radius };
				float distance = (new Vector2(x + 0.5f, y + 0.5f) - preferred).sqrMagnitude;
				if (found && distance >= bestDistance) continue; // 아직 더 가까운 후보가 없으면 비싼 칸 검사를 건너뛴다

				if (!IsFootprintClear(candidate, isClear)) continue;
				if (margin > 0 && HasCrowdedMargin(candidate, margin, isCrowded)) continue;

				found = true;
				bestDistance = distance;
				layout = candidate;
			}
		}
		return found;
	}

	// 영역 바로 바깥 margin칸 두께의 테두리에 붙어 있으면 안 되는 칸이 하나라도 있는가.
	private static bool HasCrowdedMargin(SiteLayout layout, int margin, Func<Vector2Int, bool> isCrowded)
	{
		int reach = layout.Radius + margin;
		for (int dx = -reach; dx <= reach; dx++)
		{
			for (int dy = -reach; dy <= reach; dy++)
			{
				if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) <= layout.Radius) continue; // 영역 자신은 isClear가 이미 봤다
				if (isCrowded(layout.Center + new Vector2Int(dx, dy))) return true;
			}
		}
		return false;
	}

	// ── 일꾼 자리 배정 (2026-10-22 사용자 요청) ──
	// 거점을 지을 때 근접 역할(전방 근접·근접 지원 포함)은 문·목책 둘레 1칸에, 그 밖(원거리 공격·원거리 지원)은 건설물(천막) 둘레 1칸에 서서 가만히 일한다.

	public static bool IsGateGuardRole(CombatRole role)
		=> role == CombatRole.MeleeTank || role == CombatRole.MeleeDps || role == CombatRole.MeleeSupport;

	public static bool IsInFootprint(SiteLayout layout, Vector2Int tile)
		=> Mathf.Max(Mathf.Abs(tile.x - layout.Center.x), Mathf.Abs(tile.y - layout.Center.y)) <= layout.Radius;

	// 게이트 하나(문 칸 + 목책 칸)에서 정확히 체비셰프 1칸 떨어진 자리 중 isFree인 곳 가운데 from에 가장 가까운 곳. 동점이면 y가 작은 쪽 → x가 작은 쪽(결과가 매번 같도록).
	// 게이트 칸 자신은 후보가 아니다. 방 밖·벽·점유 같은 칸 판정은 isFree가 맡는다.
	public static bool TryPickGuardSlot(IReadOnlyList<Vector2Int> gateTiles, Vector2Int from, Func<Vector2Int, bool> isFree, out Vector2Int slot)
	{
		slot = default;
		if (gateTiles == null || gateTiles.Count == 0) return false;

		var gateSet = new HashSet<Vector2Int>(gateTiles);
		var seen = new HashSet<Vector2Int>();
		bool found = false;
		int bestDistance = int.MaxValue;

		foreach (var gateTile in gateTiles)
		{
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					var candidate = gateTile + new Vector2Int(dx, dy);
					if (gateSet.Contains(candidate) || !seen.Add(candidate)) continue;

					int distance = Mathf.Max(Mathf.Abs(candidate.x - from.x), Mathf.Abs(candidate.y - from.y));
					if (found && (distance > bestDistance || (distance == bestDistance && !IsBefore(candidate, slot)))) continue; // 더 나을 수 없는 후보는 칸 검사를 건너뛴다
					if (!isFree(candidate)) continue;

					found = true;
					bestDistance = distance;
					slot = candidate;
				}
			}
		}
		return found;
	}

	private static bool IsBefore(Vector2Int a, Vector2Int b) => a.y != b.y ? a.y < b.y : a.x < b.x;

	// 근접 일꾼을 게이트들에 고르게 나누기 위한 시도 순서 — 이미 서 있는 인원이 적은 게이트가 먼저, 같으면 유닛에게 가까운 게이트, 그래도 같으면 목록 순서.
	public static List<int> OrderGatesByLoad(IReadOnlyList<int> loads, IReadOnlyList<int> distances)
	{
		var order = new List<int>(loads.Count);
		for (int i = 0; i < loads.Count; i++) order.Add(i);
		order.Sort((a, b) =>
		{
			if (loads[a] != loads[b]) return loads[a].CompareTo(loads[b]);
			if (distances[a] != distances[b]) return distances[a].CompareTo(distances[b]);
			return a.CompareTo(b);
		});
		return order;
	}

	// ── 연출 수치 ──

	// 건설물이 보여 주는 단계(0~StructureStages-1) — 진행도 0에서도 첫 단은 보인다.
	public static int StructureStage(float progress01)
		=> Mathf.Clamp((int)(Mathf.Clamp01(progress01) * StructureStages), 0, StructureStages - 1);

	// 건설물 불투명도 — 막 시작한 현장은 반투명, 완공이 가까울수록 또렷하다.
	public static float StructureAlpha(float progress01) => Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(progress01));

	// 일꾼 작업 효과(먼지·불꽃) 간격 — 일꾼이 많을수록 잦지만 너무 정신없지 않게 0.2~0.8초로 묶는다. 일꾼이 없으면 효과도 없다.
	public static float WorkFxInterval(int workers)
		=> workers <= 0 ? float.PositiveInfinity : Mathf.Clamp(0.8f / Mathf.Sqrt(workers), 0.2f, 0.8f);

	// 단이 바뀔 때 건설물이 톡 튀는 크기 배율(t 0→1: 1 → 1.18 → 1).
	public static float PopScale(float t01) => 1f + 0.18f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t01));
}
