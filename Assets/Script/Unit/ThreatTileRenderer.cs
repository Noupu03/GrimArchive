using UnityEngine;
using System.Collections.Generic;

public class ThreatTileRenderer : MonoBehaviour
{
	public static ThreatTileRenderer Instance;

	private class ThreatVisual
	{
		public LineRenderer lineRenderer;
	}

	// Unit당 1개만 관리
	private Dictionary<Unit, ThreatVisual> activeVisuals
		= new Dictionary<Unit, ThreatVisual>();

	void Awake()
	{
		Instance = this;
	}

	LineRenderer CreateLineRenderer(string name)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(transform);

		LineRenderer lr = go.AddComponent<LineRenderer>();
		lr.material = new Material(Shader.Find("Sprites/Default"));
		lr.sortingOrder = 999;
		lr.widthMultiplier = 0.1f;
		lr.useWorldSpace = true;

		return lr;
	}

	public void Render(List<Unit> units)
	{
		HashSet<Unit> aliveUnits = new HashSet<Unit>(units);

		// =====================================
		// REMOVE PHASE
		// =====================================
		List<Unit> removeList = new();

		foreach (var pair in activeVisuals)
		{
			Unit u = pair.Key;

			bool shouldRemove =
				u == null ||
				!aliveUnits.Contains(u) ||
				u.currentThreat == null;

			if (shouldRemove)
			{
				if (pair.Value != null && pair.Value.lineRenderer != null)
					Destroy(pair.Value.lineRenderer.gameObject);

				removeList.Add(u);
			}
		}

		foreach (var u in removeList)
			activeVisuals.Remove(u);

		// =====================================
		// RENDER PHASE
		// =====================================
		foreach (Unit u in units)
		{
			if (u == null || u.currentThreat == null)
				continue;

			ThreatTileData threat = u.currentThreat;

			if (threat.hitbox.size == Vector2.zero)
				continue;

			Vector3 floorOffset =
				UnitGenerate.Instance != null
				? UnitGenerate.Instance.GetFloorOffset(u.currentFloor)
				: Vector3.zero;

			ThreatVisual tv;

			if (!activeVisuals.ContainsKey(u))
			{
				tv = new ThreatVisual
				{
					lineRenderer = CreateLineRenderer("ThreatBox")
				};
				activeVisuals[u] = tv;
			}
			else
			{
				tv = activeVisuals[u];
			}

			LineRenderer lr = tv.lineRenderer;

			// =====================================
			// 히트박스 경계선 렌더링 (회전 적용)
			// =====================================
			Hitbox box = threat.hitbox;
			Vector2 center = box.center;
			Vector2 size = box.size;
			float rotation = box.rotation;

			// 히트박스의 4개 꼭짓점 (회전 전 로컬 좌표)
			Vector2 halfSize = size * 0.5f;
			Vector2[] localPoints = new Vector2[]
			{
				new Vector2(-halfSize.x, -halfSize.y),  // 좌하
				new Vector2(halfSize.x, -halfSize.y),   // 우하
				new Vector2(halfSize.x, halfSize.y),    // 우상
				new Vector2(-halfSize.x, halfSize.y)    // 좌상
			};

			// 회전 적용
			float radians = rotation * Mathf.Deg2Rad;
			float cos = Mathf.Cos(radians);
			float sin = Mathf.Sin(radians);

			Vector3[] worldPoints = new Vector3[5];
			for (int i = 0; i < 4; i++)
			{
				Vector2 rotated = new Vector2(
					localPoints[i].x * cos - localPoints[i].y * sin,
					localPoints[i].x * sin + localPoints[i].y * cos
				);
				worldPoints[i] = new Vector3(center.x + rotated.x, center.y + rotated.y, 0f) + floorOffset;
			}
			worldPoints[4] = worldPoints[0];  // 닫힌 루프

			// LineRenderer에 점 설정
			lr.positionCount = 5;
			for (int i = 0; i < 5; i++)
			{
				lr.SetPosition(i, worldPoints[i]);
			}

			// 색상 설정
			Color color = u is Human ? Color.green : Color.red;
			color.a = threat.color.a;

			lr.startColor = color;
			lr.endColor = color;
			lr.enabled = true;
		}
	}
}