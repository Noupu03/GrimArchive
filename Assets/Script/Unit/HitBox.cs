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

	public static Hitbox BuildLineHitbox(Unit unit, int range)
	{
		Vector2 dir = unit.GetDirVector(unit.currentDir);

		Vector2 size = new Vector2(
			Mathf.Abs(dir.x) > 0 ? range : 1,
			Mathf.Abs(dir.y) > 0 ? range : 1
		);

		Vector2 center =
			(Vector2)unit.position +
			dir * (range * 0.5f + 0.5f);

		return new Hitbox
		{
			center = center,
			size = size
		};
	}
	public static Hitbox BuildRectHitbox(Unit unit, int width, int depth)
	{
		Vector2 forward = unit.GetDirVector(unit.currentDir);
		Vector2 right = new Vector2(forward.y, -forward.x);

		Vector2 center =
			(Vector2)unit.position +
			forward * (depth * 0.5f + 0.5f);

		Vector2 size = new Vector2(width, depth);

		return new Hitbox
		{
			center = center,
			size = size
		};
	}
}