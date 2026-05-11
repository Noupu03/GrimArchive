using UnityEngine;
using System.Collections.Generic;

public class ThreatTileRenderer : MonoBehaviour
{
	public static ThreatTileRenderer Instance;

	private Sprite squareSprite;

	// 유닛별 생성된 위협 스프라이트 캐시
	private class ThreatVisual
	{
		public SpriteRenderer renderer;
		public bool initialized;
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

	private bool IsDiagonalDir(Dir dir)
	{
		return dir == Dir.UP_RIGHT ||
			   dir == Dir.UP_LEFT ||
			   dir == Dir.DOWN_RIGHT ||
			   dir == Dir.DOWN_LEFT;
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

		// =========================================
		// 제거 처리
		// =========================================
		List<Unit> removeList = new List<Unit>();

		foreach (var pair in activeSprites)
		{
			Unit u = pair.Key;

			bool shouldRemove =
				u == null ||
				!aliveUnits.Contains(u) ||
				u.threatTiles == null ||
				u.threatTiles.Count == 0;

			if (!shouldRemove)
				continue;

			foreach (ThreatVisual tv in pair.Value)
			{
				if (tv.renderer != null)
					Destroy(tv.renderer.gameObject);
			}

			removeList.Add(u);
		}

		foreach (Unit u in removeList)
			activeSprites.Remove(u);

		// =========================================
		// 렌더
		// =========================================
		foreach (Unit u in units)
		{
			if (u == null || u.threatTiles == null)
				continue;

			if (!activeSprites.ContainsKey(u))
				activeSprites[u] = new List<ThreatVisual>();

			List<ThreatVisual> visuals = activeSprites[u];

			int visualIndex = 0;

			Dir fixedDir = u.currentDir;

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
				if (t == null)
					continue;

				Color color =
					u is Human ? Color.green : Color.red;

				color.a = t.color.a;

				foreach (Vector2Int tile in t.tiles)
				{
					ThreatVisual tv;

					// =====================================
					// 부족하면 새로 생성
					// =====================================
					if (visualIndex >= visuals.Count)
					{
						SpriteRenderer sr =
							CreateRenderer("ThreatTile");

						tv = new ThreatVisual();

						tv.renderer = sr;
						tv.initialized = false;

						visuals.Add(tv);
					}
					else
					{
						tv = visuals[visualIndex];
					}

					SpriteRenderer srRenderer = tv.renderer;

					// =====================================
					// 최초 1회만 위치/회전 초기화
					// =====================================
					if (!tv.initialized)
					{
						Vector2Int diff = tile - origin;

						Vector2 finalPos =
							(Vector2)origin +
							new Vector2(diff.x, diff.y) * compression;

						Vector3 pos =
							new Vector3(
								finalPos.x + 0.5f,
								finalPos.y + 0.4f,
								-5f
							) + offset;

						srRenderer.transform.position = pos;

						srRenderer.transform.localScale = Vector3.one;

						srRenderer.transform.rotation =
							Quaternion.Euler(0f, 0f, rotationZ);

						tv.initialized = true;
					}

					// 색상만 갱신 가능
					srRenderer.color = color;

					srRenderer.gameObject.SetActive(true);

					visualIndex++;
				}
			}

			// 남는 스프라이트 숨김
			for (int i = visualIndex; i < visuals.Count; i++)
			{
				visuals[i].renderer.gameObject.SetActive(false);
			}
		}
	}
}