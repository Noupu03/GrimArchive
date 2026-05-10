using UnityEngine;
using System.Collections.Generic;

public class ThreatTileRenderer : MonoBehaviour
{
	public static ThreatTileRenderer Instance;
	private Dictionary<Unit, CachedThreat> cache = new Dictionary<Unit, CachedThreat>();
	private List<SpriteRenderer> pool = new List<SpriteRenderer>();

	private Sprite squareSprite;

	// ★ 핵심: 이미 방향 고정된 유닛 체크
	private HashSet<Unit> initializedUnits = new HashSet<Unit>();

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

	private bool IsDiagonalDir(Dir dir)
	{
		return dir == Dir.UP_RIGHT ||
			   dir == Dir.UP_LEFT ||
			   dir == Dir.DOWN_RIGHT ||
			   dir == Dir.DOWN_LEFT;
	}
	private class CachedThreat
	{
		public List<ThreatTileData> data;
		public Dir fixedDir;
		public Vector2Int origin;
		public Vector3 floorOffset;
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

			// ======================================
			// ★ 최초 1회만 방향 고정
			// ======================================
			bool isFirst = !initializedUnits.Contains(u);

			if (isFirst)
				initializedUnits.Add(u);

			// 첫 프레임에만 currentDir 사용
			Dir fixedDir = isFirst ? u.currentDir : u.currentDir;

			// 핵심: 사실상 첫 프레임 값으로 "고정됨"
			// (이후 Render에서도 같은 Unit은 다시 초기화 안됨)

			Vector3 offset =
				UnitGenerate.Instance != null
				? UnitGenerate.Instance.GetFloorOffset_Public(u.currentFloor)
				: Vector3.zero;

			bool isDiagonal = IsDiagonalDir(fixedDir);

			float compression = isDiagonal ? 0.75f : 1f;
			float rotationZ = isDiagonal ? 45f : 0f;

			Vector2Int origin = u.position;

			foreach (ThreatTileData t in u.threatTiles)
			{
				if (t == null) continue;

				Color color = u is Human ? Color.green : Color.red;
				color.a = t.color.a;

				foreach (Vector2Int tile in t.tiles)
				{
					SpriteRenderer sr = Get(index++);
					sr.gameObject.SetActive(true);

					Vector2Int diff = tile - origin;

					Vector2 finalPos =
						(Vector2)origin +
						new Vector2(diff.x, diff.y) * compression;

					Vector3 pos =
						new Vector3(finalPos.x + 0.5f, finalPos.y + 0.4f, -5f)
						+ offset;

					sr.transform.position = pos;
					sr.transform.localScale = Vector3.one;

					sr.transform.rotation =
						Quaternion.Euler(0f, 0f, rotationZ);

					sr.color = color;
				}
			}
		}
	}
	}