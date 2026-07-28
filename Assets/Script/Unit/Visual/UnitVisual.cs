using UnityEngine;

public class UnitVisual : MonoBehaviour
{
	// 이 GameObject가 표현하는 논리 유닛(ScriptableObject) 참조 — UnitGenerate.SetupUnitVisual에서
	// 설정한다. 인스펙터에서 유닛을 선택했을 때 개인 지도(Human.Memory.personalMap)를 볼 수 있게 하려고 둔다
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

	// ─────────────────────────── GOAP 상태 라벨 (머리 위, 월드 고정) ───────────────────────────
	// GameSession.ProcessUnitAction → Unit.JudgeState → GoapBrain이 지금 세워둔 "앞으로 실행할 계획"을
	// 보여준다. 카메라 위치/배율과 무관하게 항상 유닛 위에 붙어 있어야 하므로 Screen Space Canvas가
	// 아니라 이 유닛 트랜스폼의 자식인 world-space TextMesh로 만든다(UIManager.ShowFloatingText와
	// 같은 컴포넌트, 저 쪽은 0.5초짜리 팝업이고 이건 계속 갱신되는 상시 라벨이라는 차이만 있음).
	private TextMesh _statusLabel;
	private const int StatusLabelSortingOrder = 20; // 시야/인지선(8~10)보다 위, 선택 마커보다도 위
	private const float StatusLabelWorldOffsetAboveTop = 0.35f; // 유닛 스프라이트 상단에서 얼마나 띄울지(월드 단위)

	// GoapBrain.PlanText(예: "6-7" = MoveToTrap→TrapDisarmPerform, Actions.cs의 ActionCode 1~19 참고)를
	// 그대로 받아 표시한다 — 과거에 실행한 목표를 누적해서 보여주던 이전 방식(GoalTrailText) 대신,
	// 지금 이 유닛이 "앞으로 실행할" 계획만 숫자로 순서대로 보여준다(사용자 요청, 2026-07-22 — 문자열이
	// 아니라 숫자로만). 이미 실행이 끝난 스텝은 GoapBrain.currentPlan에서 곧바로 빠지므로 여기서 따로
	// 지우는 처리가 필요 없다.
	public void UpdateStatusLabel(string planText, bool isHuman)
	{
		EnsureStatusLabel();
		if (_statusLabel == null) return;
		_statusLabel.text = planText;
		_statusLabel.color = isHuman ? Color.cyan : Color.yellow;
	}

	private void EnsureStatusLabel()
	{
		if (_statusLabel != null || boundUnit == null) return;

		Vector2 footprint = boundUnit.unitType.footprint;
		if (footprint.x <= 0f || footprint.y <= 0f) footprint = Vector2.one;

		GameObject go = new GameObject("StatusLabel");
		go.transform.SetParent(transform, false);

		_statusLabel = go.AddComponent<TextMesh>();
		_statusLabel.fontSize = 48;
		_statusLabel.characterSize = 0.08f;
		_statusLabel.anchor = TextAnchor.MiddleCenter;
		_statusLabel.alignment = TextAlignment.Center;

		MeshRenderer mr = go.GetComponent<MeshRenderer>();
		mr.sortingOrder = StatusLabelSortingOrder;

		// go(=이 UnitVisual의 트랜스폼)의 localScale이 이미 풋프린트 크기로 맞춰져 있어서
		// (UnitGenerate.SetupUnitVisual 참고, EnsureSelectionMarker와 동일한 이유) 라벨이 유닛
		// 크기에 따라 늘어나 보이지 않게 부모 스케일을 역산한다. 위치도 같은 이유로 "풋프린트 상단
		// + 월드 여백"을 로컬 좌표로 환산한다.
		go.transform.localScale = new Vector3(1f / footprint.x, 1f / footprint.y, 1f);
		go.transform.localPosition = new Vector3(0f, (footprint.y + StatusLabelWorldOffsetAboveTop) / footprint.y, 0f);
	}

	// ─────────────────────────── 하단 상태 라벨 (함정 해제 시도중 등, 월드 고정) ───────────────────────────
	// 머리 위 상태 라벨(StatusLabel)과 완전히 같은 world-space TextMesh 방식이고, 유닛 풋프린트
	// "하단 - 여백" 쪽에 붙는다는 것만 다르다(사용자 요청, 2026-07-23 — "함정 해제 시도중... 머리
	// 위에 뜨는 계획과 같은 방식으로").
	private TextMesh _belowLabel;
	private const int BelowLabelSortingOrder = 20;
	private const float BelowLabelWorldOffsetBelowBottom = 0.35f;

	public void UpdateBelowLabel(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			if (_belowLabel != null) _belowLabel.gameObject.SetActive(false);
			return;
		}

		EnsureBelowLabel();
		if (_belowLabel == null) return;
		_belowLabel.gameObject.SetActive(true);
		_belowLabel.text = text;
	}

	private void EnsureBelowLabel()
	{
		if (_belowLabel != null || boundUnit == null) return;

		Vector2 footprint = boundUnit.unitType.footprint;
		if (footprint.x <= 0f || footprint.y <= 0f) footprint = Vector2.one;

		GameObject go = new GameObject("BelowLabel");
		go.transform.SetParent(transform, false);

		_belowLabel = go.AddComponent<TextMesh>();
		_belowLabel.fontSize = 40;
		_belowLabel.characterSize = 0.07f;
		_belowLabel.anchor = TextAnchor.MiddleCenter;
		_belowLabel.alignment = TextAlignment.Center;
		_belowLabel.color = Color.red;

		MeshRenderer mr = go.GetComponent<MeshRenderer>();
		mr.sortingOrder = BelowLabelSortingOrder;

		// StatusLabel과 같은 이유(부모 스케일이 풋프린트에 맞춰져 있음)로 스케일을 역산하고, 위치는
		// "풋프린트 하단 - 월드 여백"으로 뒤집는다.
		go.transform.localScale = new Vector3(1f / footprint.x, 1f / footprint.y, 1f);
		go.transform.localPosition = new Vector3(0f, -BelowLabelWorldOffsetBelowBottom / footprint.y, 0f);
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
