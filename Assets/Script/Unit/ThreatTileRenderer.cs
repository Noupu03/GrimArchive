using UnityEngine;
using System.Collections.Generic;

public class ThreatTileRenderer : MonoBehaviour
{
	public static ThreatTileRenderer Instance;

	private Sprite squareSprite;

	private class ThreatVisual
	{
		public SpriteRenderer renderer;
	}

	private Dictionary<Unit, List<ThreatVisual>> activeSprites
		= new Dictionary<Unit, List<ThreatVisual>>();

	void Awake()
	{
		Instance = this;
		CreateSquareSprite();
	}

	void CreateSquareSprite()
	{
		Texture2D tex = new Texture2D(1, 1);
		tex.SetPixel(0, 0, Color.white);
		tex.Apply();

		squareSprite = Sprite.Create(
			tex,
			new Rect(0, 0, 1, 1),
			new Vector2(0.5f, 0.5f),
			1f
		);
	}

	SpriteRenderer CreateRenderer(string name)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(transform);

		SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
		sr.sprite = squareSprite;
		sr.sortingOrder = 999;

		return sr;
	}

	public void Render(List<Unit> units)
	{
		HashSet<Unit> aliveUnits = new HashSet<Unit>(units);

		// =====================================
		// REMOVE PHASE
		// =====================================
		List<Unit> removeList = new();

		foreach (var pair in activeSprites)
		{
			Unit u = pair.Key;

			bool shouldRemove =
				u == null ||
				!aliveUnits.Contains(u) ||
				u.threatTiles == null ||
				u.threatTiles.Count == 0;

			if (shouldRemove)
			{
				foreach (var tv in pair.Value)
					if (tv.renderer != null)
						Destroy(tv.renderer.gameObject);

				removeList.Add(u);
			}
		}

		foreach (var u in removeList)
			activeSprites.Remove(u);

		// =====================================
		// RENDER PHASE
		// =====================================
		foreach (Unit u in units)
		{
			if (u == null || u.threatTiles == null)
				continue;

			if (!activeSprites.ContainsKey(u))
				activeSprites[u] = new List<ThreatVisual>();

			var visuals = activeSprites[u];
			int index = 0;

			Vector3 offset =
				UnitGenerate.Instance != null
				? UnitGenerate.Instance.GetFloorOffset(u.currentFloor)
				: Vector3.zero;

			foreach (ThreatTileData t in u.threatTiles)
			{
				if (t == null || t.hitbox.size == Vector2.zero)
					continue;

				Hitbox box = t.hitbox;

				Color color = u is Human ? Color.green : Color.red;
				color.a = t.color.a;

				ThreatVisual tv;

				if (index >= visuals.Count)
				{
					tv = new ThreatVisual
					{
						renderer = CreateRenderer("ThreatBox")
					};
					visuals.Add(tv);
				}
				else
				{
					tv = visuals[index];
				}

				var sr = tv.renderer;

				// =====================================
				// HITBOX → WORLD POSITION
				// =====================================
				Vector2 center = box.center;
				Vector2 size = box.size;

				Vector3 pos = new Vector3(
					center.x + 0.5f,
					center.y + 0.5f,
					-5f
				) + offset;

				sr.transform.position = pos;

				// =====================================
				// SIZE = HITBOX SIZE
				// =====================================
				sr.transform.localScale = new Vector3(
					size.x,
					size.y,
					1f
				);

				sr.transform.rotation = Quaternion.identity;
				sr.color = color;
				sr.gameObject.SetActive(true);

				index++;
			}

			// =====================================
			// disable extra sprites
			// =====================================
			for (int i = index; i < visuals.Count; i++)
				visuals[i].renderer.gameObject.SetActive(false);
		}
	}
}