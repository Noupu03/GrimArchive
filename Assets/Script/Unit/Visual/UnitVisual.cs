using UnityEngine;

public class UnitVisual : MonoBehaviour
{
	// 이 GameObject가 표현하는 논리 유닛(ScriptableObject) 참조 — UnitGenerate.SetupUnitVisual에서
	// 설정한다. 인스펙터에서 유닛을 선택했을 때 개인 지도(Human.personalMap)를 볼 수 있게 하려고 둔다
	// (Assets/Editor/UnitVisualEditor.cs 참고).
	public Unit boundUnit;

	// 시야 범위(청록)/인지 범위(주황) 표시선 — 이전에는 모든 유닛에 대해 매 프레임 계산했지만
	// (알파 0으로 항상 투명해 실제로는 아무것도 안 보이면서 LineRenderer만 매 프레임 갱신하던 낭비),
	// 이제는 UnitGenerate.SyncVisuals가 "단일 선택된 유닛"일 때만 갱신·표시한다.
	private LineRenderer _visionRangeLine;
	private LineRenderer _perceptionRangeLine;
	// 엘리트/네메시스/보스의 원형 인지 범위(01-A 13장) — 해당 유닛이 단일 선택됐을 때만 함께 표시.
	private LineRenderer _circularPerceptionLine;

	public void Setup()
	{
		_visionRangeLine = CreateRangeLine("VisionRangeLine", new Color(0.25f, 0.85f, 1f, 0.85f), 8);
		_perceptionRangeLine = CreateRangeLine("PerceptionRangeLine", new Color(1f, 0.6f, 0.15f, 0.9f), 9);
		_circularPerceptionLine = CreateRangeLine("CircularPerceptionLine", new Color(1f, 0.25f, 0.85f, 0.85f), 9);
	}

	private LineRenderer CreateRangeLine(string name, Color color, int sortingOrder)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(transform, false);

		LineRenderer line = go.AddComponent<LineRenderer>();
		line.startWidth = 0.05f;
		line.endWidth = 0.05f;
		line.material = new Material(Shader.Find("Sprites/Default"));
		line.startColor = color;
		line.endColor = color;
		line.useWorldSpace = false;
		line.sortingOrder = sortingOrder;
		line.enabled = false;
		return line;
	}

	public void SetVisionRangesVisible(bool visible)
	{
		if (_visionRangeLine != null) _visionRangeLine.enabled = visible;
		if (_perceptionRangeLine != null) _perceptionRangeLine.enabled = visible;
		if (!visible && _circularPerceptionLine != null) _circularPerceptionLine.enabled = false;
	}

	// viewRadius/viewAngle: 시야 범위(01-A 2장, 120도 고정). perceptionRadius/perceptionAngle: 인지
	// 범위(01-A 3~4장). showCircular/circularRadius: 엘리트/네메시스/보스 전용 원형 인지 범위(13장).
	public void DrawVisionAndPerceptionRange(float viewRadius, float viewAngle, float perceptionRadius, float perceptionAngle, bool showCircular, float circularRadius, Vector2 forward)
	{
		DrawCone(_visionRangeLine, viewRadius, viewAngle, forward);
		DrawCone(_perceptionRangeLine, perceptionRadius, perceptionAngle, forward);

		if (_circularPerceptionLine != null)
		{
			_circularPerceptionLine.enabled = showCircular;
			if (showCircular) DrawCircle(_circularPerceptionLine, circularRadius);
		}
	}

	private static void DrawCone(LineRenderer line, float radius, float angle, Vector2 forward)
	{
		if (line == null) return;

		int segments = 20;
		// loop=true로 마지막 호 끝점 -> 중심점을 잇는 변까지 그려야 완전한 부채꼴(원뿔) 윤곽이 된다.
		// loop=false였을 때는 이 닫는 변이 그려지지 않아 한쪽 변이 뚫려 보였다.
		line.loop = true;
		line.positionCount = segments + 2;
		line.SetPosition(0, Vector3.zero);

		float startAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - (angle / 2f);
		for (int i = 0; i <= segments; i++)
		{
			float currentAngle = startAngle + (angle * i / segments);
			float rad = currentAngle * Mathf.Deg2Rad;
			line.SetPosition(i + 1, new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius);
		}
	}

	private static void DrawCircle(LineRenderer line, float radius)
	{
		if (line == null) return;

		int segments = 24;
		line.loop = true;
		line.positionCount = segments;
		for (int i = 0; i < segments; i++)
		{
			float rad = (360f * i / segments) * Mathf.Deg2Rad;
			line.SetPosition(i, new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius);
		}
	}
}
