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
			// 히트박스 경계선 렌더링
			// =====================================
			Hitbox box = threat.hitbox;
			Vector2 center = box.center;
			Vector2 size = box.size;

			// 히트박스의 4개 꼭짓점
			Vector2 halfSize = size * 0.5f;
			Vector3 p1 = new Vector3(center.x - halfSize.x, center.y - halfSize.y, 0f) + floorOffset;
			Vector3 p2 = new Vector3(center.x + halfSize.x, center.y - halfSize.y, 0f) + floorOffset;
			Vector3 p3 = new Vector3(center.x + halfSize.x, center.y + halfSize.y, 0f) + floorOffset;
			Vector3 p4 = new Vector3(center.x - halfSize.x, center.y + halfSize.y, 0f) + floorOffset;

			// 닫힌 사각형 그리기 (4개 점 + 첫번째 점 반복)
			lr.positionCount = 5;
			lr.SetPosition(0, p1);
			lr.SetPosition(1, p2);
			lr.SetPosition(2, p3);
			lr.SetPosition(3, p4);
			lr.SetPosition(4, p1);

			// 색상 설정
			Color color = u is Human ? Color.green : Color.red;
			color.a = threat.color.a;

			lr.startColor = color;
			lr.endColor = color;
			lr.enabled = true;
		}
	}
}