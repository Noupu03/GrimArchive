using UnityEngine;

public class UnitVisual : MonoBehaviour
{
	public LineRenderer fovLine;

	public void Setup()
	{
		fovLine = gameObject.AddComponent<LineRenderer>();
		fovLine.startWidth  = 0.05f;
		fovLine.endWidth    = 0.05f;
		fovLine.material    = new Material(Shader.Find("Sprites/Default"));
		fovLine.startColor  = new Color(0f, 1f, 1f, 0f);
		fovLine.endColor    = new Color(0f, 1f, 1f, 0f);
		fovLine.useWorldSpace = false;
		fovLine.sortingOrder  = 9;
	}

	public void DrawFOV(float radius, float fovAngle, Vector2 forward)
	{
		int segments = 20;
		fovLine.positionCount = segments + 2;
		fovLine.SetPosition(0, Vector3.zero);

		float startAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - (fovAngle / 2f);
		for (int i = 0; i <= segments; i++)
		{
			float currentAngle = startAngle + (fovAngle * i / segments);
			float rad          = currentAngle * Mathf.Deg2Rad;
			fovLine.SetPosition(i + 1, new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius);
		}
	}
}
