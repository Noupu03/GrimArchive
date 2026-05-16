using UnityEngine;

public struct Hitbox
{
	public Vector2 center;
	public Vector2 size; // width, height
	public float rotation; // 필요 시 (현재는 0 추천)

	public Rect ToRect()
	{
		return new Rect(
			center.x - size.x * 0.5f,
			center.y - size.y * 0.5f,
			size.x,
			size.y
		);
	}

	public bool Overlaps(Hitbox other)
	{
		return ToRect().Overlaps(other.ToRect());
	}

	/// <summary>
	/// 두 히트박스의 교차 영역의 면적을 계산합니다.
	/// (rotation = 0인 AABB 기반 계산)
	/// </summary>
	public float CalculateOverlapArea(Hitbox other)
	{
		Rect thisRect = this.ToRect();
		Rect otherRect = other.ToRect();

		// 교차 영역의 x 범위 계산
		float overlapLeft = Mathf.Max(thisRect.xMin, otherRect.xMin);
		float overlapRight = Mathf.Min(thisRect.xMax, otherRect.xMax);

		// 교차 영역의 y 범위 계산
		float overlapBottom = Mathf.Max(thisRect.yMin, otherRect.yMin);
		float overlapTop = Mathf.Min(thisRect.yMax, otherRect.yMax);

		// 교차 폭과 높이
		float overlapWidth = overlapRight - overlapLeft;
		float overlapHeight = overlapTop - overlapBottom;

		// 교차 영역이 존재하지 않으면 0 반환
		if (overlapWidth <= 0 || overlapHeight <= 0)
			return 0f;

		return overlapWidth * overlapHeight;
	}

	/// <summary>
	/// 두 히트박스의 교차 비율을 계산합니다 (0~1).
	/// 비율 = 교차 면적 / 이 히트박스의 면적
	/// </summary>
	public float CalculateOverlapRatio(Hitbox other)
	{
		float thisArea = this.size.x * this.size.y;
		if (thisArea <= 0) return 0f;

		float overlapArea = this.CalculateOverlapArea(other);
		return Mathf.Clamp01(overlapArea / thisArea);
	}
}
