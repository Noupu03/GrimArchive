using UnityEngine;

public enum ThreatShape
{
	LINE,
	CONE,
	RECT,
	CIRCLE
}

public class ThreatTileData
{
	public ThreatShape shape;

	public int range = 1;
	public int width = 1;
	public int depth = 1;

	public Color color = new Color(1f, 0f, 0f, 0.5f);

	public Hitbox hitbox;

	// =========================================
	// SAFE FACTORY METHOD
	// =========================================
	public static ThreatTileData Create()
	{
		return new ThreatTileData
		{
			shape   = ThreatShape.LINE,
			range   = 1,
			width   = 1,
			depth   = 1,
			color   = new Color(1f, 0f, 0f, 0.5f),
			hitbox  = default
		};
	}
}
