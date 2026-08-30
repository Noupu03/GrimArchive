using System.Collections.Generic;
using UnityEngine;

public class UnitVisual : MonoBehaviour
{
	// 이 GameObject가 표현하는 논리 유닛(ScriptableObject) 참조 — UnitGenerate.SetupUnitVisual에서
	// 설정한다. 인스펙터에서 개인 지도(Human.Memory.personalMap)를 볼 수 있게 한다(UnitVisualEditor.cs 참고).
	public Unit boundUnit;

	// 시야 범위/인지 범위 표시선 — UnitGenerate.SyncVisuals가 "단일 선택된 유닛" 또는
	// "ShowAllVisionRanges 전역 토글이 켜진 유닛"일 때만 갱신·표시한다. 진영별로 색이 다르다(Setup 참고).
	private LineRenderer _visionRangeLine;
	private LineRenderer _perceptionRangeLine;
	// 엘리트/네메시스/보스의 원형 인지 범위(01-A 12장) — 위와 동일 표시 조건일 때만 함께 표시.
	private LineRenderer _circularPerceptionLine;

	// 시야/인지 범위는 명도·채도 차이만으로는 구분이 잘 안 돼 색상(Hue) 자체를 다르게 쓴다 — 진영은
	// "차가운 계열(인류)/따뜻한 계열(몬스터)"로, 그 안에서 시야=넓고 옅은 색, 인지=전혀 다른 톤의
	// 진하고 굵은 색으로 나눈다(선 굵기도 함께 달리해 이중으로 구분).
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
	// GameSession.ProcessUnitAction → Unit.JudgeState → GoapBrain이 세워둔 "앞으로 실행할 계획"을
	// 보여준다. 카메라와 무관하게 유닛 위에 붙어야 하므로 Screen Space Canvas 대신 world-space
	// TextMesh로 만든다(UIManager.ShowFloatingText는 0.5초짜리 팝업이라 다름).
	private TextMesh _statusLabel;
	private const int StatusLabelSortingOrder = 20; // 시야/인지선(8~10)보다 위, 선택 마커보다도 위
	// 체력바(아래 EnsureHealthBar) 바로 위로 올라오게, 기존 0.35에서 상향(2026-08-24).
	private const float StatusLabelWorldOffsetAboveTop = 0.55f; // 유닛 스프라이트 상단에서 얼마나 띄울지(월드 단위)

	// GoapBrain.PlanText(예: "6-7" = MoveToTrap→TrapDisarmPerform, ActionCode 1~19 참고)를 그대로
	// 받아 표시한다 — 실행이 끝난 스텝은 GoapBrain.currentPlan에서 곧바로 빠져 따로 지울 필요 없다.
	public void UpdateStatusLabel(string planText, bool isHuman)
	{
		// 안개에 가려진 유닛은 UnitGenerate.SyncVisuals가 planText로 null/빈 문자열을 넘긴다 —
		// UpdateBelowLabel과 동일한 "null이면 숨김" 관례.
		if (string.IsNullOrEmpty(planText))
		{
			if (_statusLabel != null) _statusLabel.gameObject.SetActive(false);
			return;
		}

		EnsureStatusLabel();
		if (_statusLabel == null) return;
		_statusLabel.gameObject.SetActive(true);
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

		// 실제 부모 localScale을 역산한다 — footprint를 그대로 쓰면 visualScaleIgnoresFootprint가
		// 켜진 유닛(루트 localScale=1 고정)에서 라벨이 잘못된 배율/위치로 렌더링된다.
		Vector3 parentScale = transform.localScale;
		float invX = parentScale.x != 0f ? 1f / parentScale.x : 1f;
		float invY = parentScale.y != 0f ? 1f / parentScale.y : 1f;
		go.transform.localScale = new Vector3(invX, invY, 1f);
		go.transform.localPosition = new Vector3(0f, (footprint.y + StatusLabelWorldOffsetAboveTop) * invY, 0f);
	}

	// ─────────────────────────── 체력바 (머리 위, 월드 고정, 항상 표시) ───────────────────────────
	// ObjectProgressBarVisual과 동일한 배경+채움 SpriteRenderer 2장 구성을 재사용하되, 체력바는
	// StatusLabel과 같은 "머리 위" 계열이라 별도로 둔다. 위치 계산도 동일하게 부모 localScale을 역산한다.
	private SpriteRenderer _healthBarBg;
	private SpriteRenderer _healthBarFill;
	private float _healthBarFillBaseScaleX;
	// VFXManager가 이펙트 sortingOrder를 "대상 스프라이트+10"으로 매겨 기존 19로는 쉽게 역전당했다 —
	// 웬만한 이펙트보다 위, 위협타일(999)/안개(1000~)보다는 아래인 값.
	private const int HealthBarSortingOrder = 101;
	private const float HealthBarWidth = 0.8f;
	private const float HealthBarHeight = 0.12f;
	private const float HealthBarWorldOffsetAboveTop = 0.2f; // 유닛 스프라이트 상단에서 띄우는 높이(월드 단위)

	private static Sprite _sharedHealthBarCenterSprite;
	private static Sprite _sharedHealthBarLeftSprite;

	// 단일 색상 — 비율별 초록/노랑/빨강 3색은 초록이 바닥 타일 색과 구분이 안 돼, 잘 겹치지 않는
	// 선명한 마젠타 계열 고정 단색으로 대체.
	private static readonly Color HealthBarFillColor = new Color(1f, 0.15f, 0.6f, 1f);

	public void UpdateHealthBar(bool visible, float hp, float maxHp)
	{
		EnsureHealthBar();
		if (_healthBarBg == null || _healthBarFill == null) return;

		_healthBarBg.enabled = visible;
		_healthBarFill.enabled = visible;
		if (!visible) return;

		float ratio = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
		_healthBarFill.transform.localScale = new Vector3(_healthBarFillBaseScaleX * ratio, _healthBarFill.transform.localScale.y, 1f);
	}

	private void EnsureHealthBar()
	{
		if (_healthBarBg != null || boundUnit == null) return;

		Vector2 footprint = boundUnit.unitType.footprint;
		if (footprint.x <= 0f || footprint.y <= 0f) footprint = Vector2.one;

		Vector3 parentScale = transform.localScale;
		float invX = parentScale.x != 0f ? 1f / parentScale.x : 1f;
		float invY = parentScale.y != 0f ? 1f / parentScale.y : 1f;

		Vector3 anchor = new Vector3(0f, (footprint.y + HealthBarWorldOffsetAboveTop) * invY, 0f);

		_healthBarBg = CreateHealthBarSprite("HealthBarBg", new Color(0f, 0f, 0f, 0.6f), centerPivot: true);
		_healthBarBg.transform.localPosition = anchor;
		_healthBarBg.transform.localScale = new Vector3(HealthBarWidth * invX, HealthBarHeight * invY, 1f);

		_healthBarFill = CreateHealthBarSprite("HealthBarFill", HealthBarFillColor, centerPivot: false);
		_healthBarFillBaseScaleX = HealthBarWidth * invX;
		_healthBarFill.transform.localPosition = anchor + new Vector3(-HealthBarWidth * invX / 2f, 0f, 0f);
		_healthBarFill.transform.localScale = new Vector3(_healthBarFillBaseScaleX, HealthBarHeight * invY, 1f);

		_healthBarBg.enabled = false;
		_healthBarFill.enabled = false;
	}

	private SpriteRenderer CreateHealthBarSprite(string name, Color color, bool centerPivot)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(transform, false);
		var sr = go.AddComponent<SpriteRenderer>();
		sr.sprite = centerPivot
			? (_sharedHealthBarCenterSprite ??= CreateWhiteSprite(new Vector2(0.5f, 0.5f)))
			: (_sharedHealthBarLeftSprite ??= CreateWhiteSprite(new Vector2(0f, 0.5f)));
		sr.color = color;
		sr.sortingOrder = HealthBarSortingOrder;
		return sr;
	}

	private static Sprite CreateWhiteSprite(Vector2 pivot)
	{
		Texture2D tex = new Texture2D(4, 4);
		Color[] pixels = new Color[16];
		for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
		tex.SetPixels(pixels);
		tex.Apply();
		return Sprite.Create(tex, new Rect(0, 0, 4, 4), pivot, 4f);
	}

	// ─────────────────────────── 하단 상태 라벨 (함정 해제 시도중 등, 월드 고정) ───────────────────────────
	// 머리 위 상태 라벨(StatusLabel)과 완전히 같은 world-space TextMesh 방식이고, 유닛 풋프린트
	// "하단 - 여백" 쪽에 붙는다는 것만 다르다.
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

		// StatusLabel과 동일한 이유/방식(실제 부모 localScale을 역산)으로 스케일을 역산하고, 위치는
		// "풋프린트 하단 - 월드 여백"으로 뒤집는다.
		Vector3 parentScale = transform.localScale;
		float invX = parentScale.x != 0f ? 1f / parentScale.x : 1f;
		float invY = parentScale.y != 0f ? 1f / parentScale.y : 1f;
		go.transform.localScale = new Vector3(invX, invY, 1f);
		go.transform.localPosition = new Vector3(0f, -BelowLabelWorldOffsetBelowBottom * invY, 0f);
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

	// ─────────────────────────── 명령 경로 시각화 (선택 중일 때만, 월드 좌표) ───────────────────────────
	// 위 시야/인지 콘과 달리 유닛 트랜스폼을 따라다니면 안 되므로(경로는 절대 월드 좌표)
	// useWorldSpace=true로 별도 LineRenderer를 쓴다. Unit.MovementAlgorithm의 기존 경로 캐시를
	// 그대로 읽기만 해서, 길찾기가 다시 도는 순간 자동으로 갱신된다.
	private LineRenderer _commandPathLine;
	private LineRenderer _commandDestMarker;
	private LineRenderer _hoverMarker;
	private static readonly Color CommandPathColor = new Color(1f, 0.95f, 0.2f, 0.8f); // 경로 선: 노란색
	private static readonly Color CommandDestColor = new Color(0.2f, 0.9f, 0.2f, 0.95f); // 확정된 목적지: 녹색
	private static readonly Color HoverMarkerColor = new Color(0.3f, 0.8f, 1f, 0.8f); // 포인터 호버 타일: 하늘색
	private const float CommandPathLineWidth = 0.06f;
	private const float CommandDestMarkerRadius = 0.28f;
	private const int CommandPathSortingOrder = 12;
	private const int CommandDestMarkerSortingOrder = 13;
	private const int HoverMarkerSortingOrder = 14;

	// floorOffset — unit.position은 층별 로컬 그리드 좌표라 그 층의 타일맵 오프셋(GetFloorOffset)을
	// 더해야 실제 월드 좌표가 된다. 없으면 여러 층이 나란히 배치된 경우 화면 밖에 그려진다.
	public void UpdateCommandPathVisual(bool visible, List<Vector2Int> pathTiles, Vector3 floorOffset, Vector2Int? hoverTile = null, Vector2Int? explicitDestTile = null)
	{
		if (!visible)
		{
			if (_commandPathLine != null) _commandPathLine.enabled = false;
			if (_commandDestMarker != null) _commandDestMarker.enabled = false;
			if (_hoverMarker != null) _hoverMarker.enabled = false;
			return;
		}

		EnsureCommandPathVisuals();

		if (hoverTile.HasValue)
		{
			_hoverMarker.enabled = true;
			DrawWorldSquare(_hoverMarker, TileCenterWorld(hoverTile.Value, floorOffset), 1.0f);
		}
		else
		{
			_hoverMarker.enabled = false;
		}

		bool hasPath = pathTiles != null && pathTiles.Count >= 2;
		Vector2Int? dest = explicitDestTile ?? (hasPath ? pathTiles[pathTiles.Count - 1] : (Vector2Int?)null);

		if (hasPath)
		{
			_commandPathLine.enabled = true;
			_commandPathLine.positionCount = pathTiles.Count;
			for (int i = 0; i < pathTiles.Count; i++)
				_commandPathLine.SetPosition(i, TileCenterWorld(pathTiles[i], floorOffset));
		}
		else
		{
			_commandPathLine.enabled = false;
		}

		if (dest.HasValue)
		{
			_commandDestMarker.enabled = true;
			DrawWorldSquare(_commandDestMarker, TileCenterWorld(dest.Value, floorOffset), 0.9f);
		}
		else
		{
			_commandDestMarker.enabled = false;
		}
	}

	private static Vector3 TileCenterWorld(Vector2Int tile, Vector3 floorOffset) => new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f) + floorOffset;

	private void EnsureCommandPathVisuals()
	{
		if (_commandPathLine != null) return;

		_commandPathLine = CreateWorldLine("CommandPathLine", CommandPathColor, CommandPathLineWidth, CommandPathSortingOrder);
		_commandDestMarker = CreateWorldLine("CommandDestMarker", CommandDestColor, CommandPathLineWidth, CommandDestMarkerSortingOrder);
		_commandDestMarker.loop = true;
		_hoverMarker = CreateWorldLine("HoverMarker", HoverMarkerColor, CommandPathLineWidth, HoverMarkerSortingOrder);
		_hoverMarker.loop = true;
	}

	private LineRenderer CreateWorldLine(string name, Color color, float width, int sortingOrder)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(transform, false);

		LineRenderer line = go.AddComponent<LineRenderer>();
		line.startWidth = width;
		line.endWidth = width;
		line.material = new Material(Shader.Find("Sprites/Default"));
		line.startColor = color;
		line.endColor = color;
		line.useWorldSpace = true; // 경로 좌표는 유닛 트랜스폼과 무관한 절대 월드 좌표.
		line.sortingOrder = sortingOrder;
		line.enabled = false;
		return line;
	}

	private static void DrawWorldSquare(LineRenderer line, Vector3 worldCenter, float size)
{
    if (line == null) return;

    float half = size * 0.5f;

    Vector3[] corners =
    {
        worldCenter + new Vector3(-half, -half, 0f),
        worldCenter + new Vector3( half, -half, 0f),
        worldCenter + new Vector3( half,  half, 0f),
        worldCenter + new Vector3(-half,  half, 0f)
    };

    line.loop = true;
    line.positionCount = 4;

    for (int i = 0; i < 4; i++)
        line.SetPosition(i, corners[i]);
}

}
