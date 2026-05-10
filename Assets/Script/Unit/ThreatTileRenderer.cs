using UnityEngine;
using System.Collections.Generic;

public class ThreatTileRenderer : MonoBehaviour
{
	public static ThreatTileRenderer Instance;

	private List<SpriteRenderer> pool = new List<SpriteRenderer>();

	private Sprite squareSprite;

	void Awake()
	{
		Instance = this;

		CreateSquareSprite();
	}
	SpriteRenderer Get(int index)
	{
		if (index < pool.Count)
			return pool[index];

		GameObject go = new GameObject("ThreatShape_" + index);
		go.transform.SetParent(transform);

		SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
		sr.sprite = squareSprite;
		sr.sortingOrder = 999;

		pool.Add(sr);

		return sr;
	}
	void CreateSquareSprite()
	{
		Texture2D tex =
			new Texture2D(1, 1);

		tex.SetPixel(0, 0, Color.white);

		tex.Apply();

		squareSprite =
			Sprite.Create(
				tex,
				new Rect(0, 0, 1, 1),
				new Vector2(0.5f, 0.5f),
				1f
			);
	}

	public void Render(List<Unit> units)
	{
		for (int i = 0; i < pool.Count; i++)
			pool[i].gameObject.SetActive(false);

		int index = 0;

		foreach (Unit u in units)
		{
			if (u == null || u.threatTiles == null)
				continue;

			Vector3 offset =
				UnitGenerate.Instance != null
				? UnitGenerate.Instance.GetFloorOffset_Public(u.currentFloor)
				: Vector3.zero;

			foreach (ThreatTileData t in u.threatTiles)
			{
				if (t == null) continue;

				Color color = u is Human ? Color.green : Color.red;
				color.a = t.color.a;

				Vector2Int origin = u.position;

				foreach (Vector2Int tile in t.tiles)
				{
					SpriteRenderer sr = Get(index++);
					sr.gameObject.SetActive(true);

					Vector2Int diff = tile - origin;

					// =========================
					// 핵심: 대각선 압축
					// =========================
					Vector2 renderPos = origin;

					int dx = diff.x;
					int dy = diff.y;

					int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));

					Vector2 dir = new Vector2(
						dx == 0 ? 0 : dx / Mathf.Abs(dx),
						dy == 0 ? 0 : dy / Mathf.Abs(dy)
					);

					// 대각선이면 더 짧게 압축
					float compression = (dx != 0 && dy != 0) ? 0.75f : 1f;

					Vector2 finalPos =
						(Vector2)origin +
						new Vector2(dx, dy) * compression;

					Vector3 pos =
						new Vector3(finalPos.x + 0.5f, finalPos.y + 0.5f, -5f)
						+ offset;

					sr.transform.position = pos;
					sr.transform.rotation = Quaternion.identity;
					sr.transform.localScale = Vector3.one;
					sr.color = color;
				}
			}
		}
	}
}