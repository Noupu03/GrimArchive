using UnityEngine;

public struct Hitbox
{
	public Vector2 center;
	public Vector2 size;     // width, height
	public float   rotation; // 필요 시 사용

	public Rect ToRect()
	{
		return new Rect(
			center.x - size.x * 0.5f,
			center.y - size.y * 0.5f,
			size.x,
			size.y
		);
	}

	public bool Overlaps(Hitbox other) => ToRect().Overlaps(other.ToRect());

	/// <summary>
	/// 두 히트박스의 교차 영역 면적을 계산합니다.
	/// (rotation = 0인 AABB 기반 계산)
	/// </summary>
	public float CalculateOverlapArea(Hitbox other)
	{
		Rect thisRect  = ToRect();
		Rect otherRect = other.ToRect();

		float overlapWidth  = Mathf.Min(thisRect.xMax, otherRect.xMax) - Mathf.Max(thisRect.xMin, otherRect.xMin);
		float overlapHeight = Mathf.Min(thisRect.yMax, otherRect.yMax) - Mathf.Max(thisRect.yMin, otherRect.yMin);

		if (overlapWidth <= 0 || overlapHeight <= 0) return 0f;
		return overlapWidth * overlapHeight;
	}

	/// <summary>
	/// 두 히트박스의 교차 비율을 계산합니다 (0~1).
	/// 비율 = 교차 면적 / 이 히트박스의 면적
	/// </summary>
	public float CalculateOverlapRatio(Hitbox other)
	{
		float thisArea = size.x * size.y;
		if (thisArea <= 0) return 0f;
		return Mathf.Clamp01(CalculateOverlapArea(other) / thisArea);
	}

	public static bool IsInside(Vector2Int pos, Hitbox box)
	{
		return pos.x >= box.center.x - box.size.x * 0.5f &&
		       pos.x <= box.center.x + box.size.x * 0.5f &&
		       pos.y >= box.center.y - box.size.y * 0.5f &&
		       pos.y <= box.center.y + box.size.y * 0.5f;
	}
}
