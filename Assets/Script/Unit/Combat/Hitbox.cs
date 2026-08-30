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

	public bool Overlaps(Hitbox other)
	{
		// 회전이 둘 다 없으면 빠른 AABB 검사
		if (Mathf.Abs(this.rotation) < 0.01f && Mathf.Abs(other.rotation) < 0.01f)
		{
			return ToRect().Overlaps(other.ToRect());
		}

		// OBB vs OBB (Separating Axis Theorem)
		Vector2[] axes = new Vector2[4];
		float angle1 = this.rotation * Mathf.Deg2Rad;
		float angle2 = other.rotation * Mathf.Deg2Rad;
		
		axes[0] = new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1));
		axes[1] = new Vector2(-Mathf.Sin(angle1), Mathf.Cos(angle1));
		axes[2] = new Vector2(Mathf.Cos(angle2), Mathf.Sin(angle2));
		axes[3] = new Vector2(-Mathf.Sin(angle2), Mathf.Cos(angle2));

		Vector2[] corners1 = GetCorners(this, angle1);
		Vector2[] corners2 = GetCorners(other, angle2);

		foreach (Vector2 axis in axes)
		{
			if (!OverlapOnAxis(corners1, corners2, axis))
				return false; // 하나의 축이라도 안 겹치면 충돌 안함
		}
		return true;
	}

	private Vector2[] GetCorners(Hitbox box, float angleRad)
	{
		Vector2 right = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * (box.size.x * 0.5f);
		Vector2 up = new Vector2(-Mathf.Sin(angleRad), Mathf.Cos(angleRad)) * (box.size.y * 0.5f);
		
		return new Vector2[]
		{
			box.center + right + up,
			box.center + right - up,
			box.center - right - up,
			box.center - right + up
		};
	}

	private bool OverlapOnAxis(Vector2[] corners1, Vector2[] corners2, Vector2 axis)
	{
		float min1 = float.MaxValue, max1 = float.MinValue;
		foreach (Vector2 c in corners1)
		{
			float proj = Vector2.Dot(c, axis);
			min1 = Mathf.Min(min1, proj);
			max1 = Mathf.Max(max1, proj);
		}

		float min2 = float.MaxValue, max2 = float.MinValue;
		foreach (Vector2 c in corners2)
		{
			float proj = Vector2.Dot(c, axis);
			min2 = Mathf.Min(min2, proj);
			max2 = Mathf.Max(max2, proj);
		}

		return max1 >= min2 && max2 >= min1;
	}

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
	/// 비율 = 교차 면적 / 두 히트박스 중 "작은 쪽"의 면적 — 공격 범위 면적으로만 나누면 광역기가
	/// 넓을수록 데미지가 깎이고, 대상 면적으로만 나누면 대형 유닛이 작은 공격에 덜 맞는 문제가
	/// 생겨, 작은 쪽 기준으로 "완전히 겹치면 1.0, 스치면 그만큼 감소"를 두 경우 모두 성립시킨다.
	/// </summary>
	public float CalculateOverlapRatio(Hitbox other)
	{
		float thisArea  = size.x * size.y;
		float otherArea = other.size.x * other.size.y;
		float baseArea  = Mathf.Min(thisArea, otherArea);
		if (baseArea <= 0) return 0f;
		return Mathf.Clamp01(CalculateOverlapArea(other) / baseArea);
	}

	public static bool IsInside(Vector2Int pos, Hitbox box)
	{
		return pos.x >= box.center.x - box.size.x * 0.5f &&
		       pos.x <= box.center.x + box.size.x * 0.5f &&
		       pos.y >= box.center.y - box.size.y * 0.5f &&
		       pos.y <= box.center.y + box.size.y * 0.5f;
	}
}
