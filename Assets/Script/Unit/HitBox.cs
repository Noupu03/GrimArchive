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
}