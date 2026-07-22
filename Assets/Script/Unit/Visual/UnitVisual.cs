using UnityEngine;

public class UnitVisual : MonoBehaviour
{
	// 이 GameObject가 표현하는 논리 유닛(ScriptableObject) 참조 — UnitGenerate.SetupUnitVisual에서
	// 설정한다. 인스펙터에서 유닛을 선택했을 때 개인 지도(Human.personalMap)를 볼 수 있게 하려고 둔다
	// (Assets/Editor/UnitVisualEditor.cs 참고).
	public Unit boundUnit;

	// 시야 범위/인지 범위 표시선 — 이전에는 모든 유닛에 대해 매 프레임 계산했지만(알파 0으로 항상
	// 투명해 실제로는 아무것도 안 보이면서 LineRenderer만 매 프레임 갱신하던 낭비), 이제는
	// UnitGenerate.SyncVisuals가 "단일 선택된 유닛" 또는 "UnitGenerate.ShowAllVisionRanges 전역
	// 토글이 켜진 모든 유닛"일 때만 갱신·표시한다. 인류/몬스터 진영별로 색이 다르다(Setup 참고).
	private LineRenderer _visionRangeLine;
	private LineRenderer _perceptionRangeLine;
	// 엘리트/네메시스/보스의 원형 인지 범위(01-A 12장) — 위와 동일 표시 조건일 때만 함께 표시.
	private LineRenderer _circularPerceptionLine;

	// 시야 범위와 인지 범위는 같은 계열의 명도/채도 차이만으로는 구분이 잘 안 돼서(사용자 피드백,
	// 2026-07-13), 아예 색상(Hue) 자체를 다르게 쓴다 — 진영은 "차가운 계열(인류)/따뜻한 계열(몬스터)"
	// 구도로만 구분하고, 그 안에서 시야=넓고 옅은 색, 인지=전혀 다른 톤의 진하고 굵은 색으로 나눈다.
	// (선 굵기도 함께 달리해서 색만으로 구분하기 어려운 경우에도 구분 가능하게 함.)
	private static readonly Color HumanVisionColor      = new Color(0.15f, 0.85f, 0.90f, 0.55f); // 청록(cyan)
	private static readonly Color HumanPerceptionColor  = new Color(0.55f, 0.20f, 1.00f, 0.95f);  // 남보라(violet)
	private static readonly Color HumanCircularColor    = new Color(0.95f, 0.95f, 1.00f, 0.85f);  // 흰빛 하이라이트
	private static readonly Color MonsterVisionColor     = new Color(1.00f, 0.80f, 0.10f, 0.55f); // 호박색(amber)
	private static readonly Color MonsterPerceptionColor = new Color(0.95f, 0.10f, 0.15f, 0.95f);  // 선명한 빨강
	private static readonly Color MonsterCircularColor   = new Color(1.00f, 0.55f, 0.85f, 0.85f);  // 진한 핑크 하이라이트

	private const float VisionLineWidth = 0.045f;      // 시야 범위 — 얇게
	private const float PerceptionLineWidth = 0.09f;    // 인지 범위 — 굵게(색뿐 아니라 굵기로도 구분)
	private const float CircularLineWidth = 0.07f;

	public void Setup(bool isHuman)
	{
		Color visionColor = isHuman ? HumanVisionColor : MonsterVisionColor;
		Color perceptionColor = isHuman ? HumanPerceptionColor : MonsterPerceptionColor;
		Color circularColor = isHuman ? HumanCircularColor : MonsterCircularColor;

		_visionRangeLine = CreateRangeLine("VisionRangeLine", visionColor, VisionLineWidth, 8);
		_perceptionRangeLine = CreateRangeLine("PerceptionRangeLine", perceptionColor, PerceptionLineWidth, 10);
		_circularPerceptionLine = CreateRangeLine("CircularPerceptionLine", circularColor, CircularLineWidth, 9);
	}

	private LineRenderer CreateRangeLine(string name, Color color, float width, int sortingOrder)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(transform, false);

		LineRenderer line = go.AddComponent<LineRenderer>();
		line.startWidth = width;
		line.endWidth = width;
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
