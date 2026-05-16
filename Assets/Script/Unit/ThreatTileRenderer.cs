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

	// Unit당 1개만 관리 (List 제거)
	private Dictionary<Unit, ThreatVisual> activeSprites
		= new Dictionary<Unit, ThreatVisual>();

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
				u.currentThreat == null;

			if (shouldRemove)
			{
				if (pair.Value != null && pair.Value.renderer != null)
					Destroy(pair.Value.renderer.gameObject);

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
			if (u == null || u.currentThreat == null)
				continue;

			ThreatTileData t = u.currentThreat;

			if (t.hitbox.size == Vector2.zero)
				continue;

			Vector3 offset =
				UnitGenerate.Instance != null
				? UnitGenerate.Instance.GetFloorOffset(u.currentFloor)
				: Vector3.zero;

			ThreatVisual tv;

			if (!activeSprites.ContainsKey(u))
			{
				tv = new ThreatVisual
				{
					renderer = CreateRenderer("ThreatBox")
				};
				activeSprites[u] = tv;
			}
			else
			{
				tv = activeSprites[u];
			}

			SpriteRenderer sr = tv.renderer;

			// =====================================
			// HITBOX → WORLD POSITION
			// =====================================
			Hitbox box = t.hitbox;

			Vector2 center = box.center;
			Vector2 size = box.size.normalized;

			Vector3 pos = new Vector3(
				center.x + 0.5f,
				center.y + 0.5f,
				-5f
			)+offset;

			sr.transform.position = pos;
			sr.transform.rotation = u.GetDirRotation(u.currentDir);

			// =====================================
			// SIZE = HITBOX SIZE
			// =====================================
			sr.transform.localScale = new Vector3(
				size.x,
				size.y,
				1f
			);

			Color color = u is Human ? Color.green : Color.red;
			color.a = t.color.a;

			sr.color = color;
			sr.gameObject.SetActive(true);
		}
	}
}