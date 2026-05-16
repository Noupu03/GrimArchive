using UnityEngine;

public static class HitboxUtil
{
	public static bool IsInside(Vector2Int pos, Hitbox box)
	{
		return pos.x >= box.center.x - box.size.x * 0.5f &&
			   pos.x <= box.center.x + box.size.x * 0.5f &&
			   pos.y >= box.center.y - box.size.y * 0.5f &&
			   pos.y <= box.center.y + box.size.y * 0.5f;
	}
}