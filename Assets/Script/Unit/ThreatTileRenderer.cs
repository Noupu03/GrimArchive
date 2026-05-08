using UnityEngine;
using System.Collections.Generic;

public class ThreatTileRenderer : MonoBehaviour
{
	public static ThreatTileRenderer Instance;

	private List<SpriteRenderer> tilePool =
		new List<SpriteRenderer>();

	private Sprite squareSprite;

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

		squareSprite =
			Sprite.Create(
				tex,
				new Rect(0, 0, 1, 1),
				new Vector2(0.5f, 0.5f),
				1f
			);
	}

	SpriteRenderer GetTileRenderer(int index)
	{
		if (index < tilePool.Count)
		{
			return tilePool[index];
		}

		GameObject go =
			new GameObject(
				"ThreatTile_" + index
			);

		go.transform.SetParent(transform);

		SpriteRenderer sr =
			go.AddComponent<SpriteRenderer>();

		sr.sprite = squareSprite;

		sr.sortingOrder = 5;

		go.transform.localScale =
			Vector3.one;

		tilePool.Add(sr);

		return sr;
	}

	public void Render(List<Unit> units)
	{
		for (int i = 0; i < tilePool.Count; i++)
		{
			if (tilePool[i] != null)
			{
				tilePool[i].gameObject.SetActive(false);
			}
		}

		int index = 0;

		foreach (Unit u in units)
		{
			if (u == null)
				continue;

			if (u.threatTiles == null)
				continue;

			Vector3 floorOffset = Vector3.zero;

			if (UnitGenerate.Instance != null)
			{
				floorOffset =
					UnitGenerate.Instance
					.GetFloorOffset_Public(
						u.currentFloor
					);
			}

			foreach (Vector2Int pos in u.threatTiles)
			{
				SpriteRenderer sr =
					GetTileRenderer(index);

				sr.gameObject.SetActive(true);

				sr.transform.position =
					new Vector3(
						pos.x + 0.5f,
						pos.y + 0.5f,
						0f
					)
					+ floorOffset;

				sr.transform.localScale =
					Vector3.one;

				if (u is Human)
				{
					sr.color =
						new Color(
							0f,
							1f,
							1f,
							0.65f // 기존 0.25f → 더 진하게
						);
				}
				else
				{
					sr.color =
						new Color(
							1f,
							0f,
							0f,
							0.65f // 기존 0.25f → 더 진하게
						);
				}

				index++;
			}
		}
	}
}