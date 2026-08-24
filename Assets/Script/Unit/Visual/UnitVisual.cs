using System.Collections.Generic;
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
	// 체력바(아래 EnsureHealthBar) 바로 위로 올라오게, 기존 0.35에서 상향(2026-08-24).
	private const float StatusLabelWorldOffsetAboveTop = 0.55f; // 유닛 스프라이트 상단에서 얼마나 띄울지(월드 단위)

	// GoapBrain.PlanText(예: "6-7" = MoveToTrap→TrapDisarmPerform, Actions.cs의 ActionCode 1~19 참고)를
	// 그대로 받아 표시한다 — 과거에 실행한 목표를 누적해서 보여주던 이전 방식(GoalTrailText) 대신,
	// 지금 이 유닛이 "앞으로 실행할" 계획만 숫자로 순서대로 보여준다(사용자 요청, 2026-07-22 — 문자열이
	// 아니라 숫자로만). 이미 실행이 끝난 스텝은 GoapBrain.currentPlan에서 곧바로 빠지므로 여기서 따로
	// 지우는 처리가 필요 없다.
	public void UpdateStatusLabel(string planText, bool isHuman)
	{
		// 안개 시스템(2026-07-28, 사용자 요청 "안개 속의 유닛은 머리 위의 상태도 보이지 않게") —
		// UnitGenerate.SyncVisuals가 안개에 가려진 유닛이면 planText로 null/빈 문자열을 넘긴다.
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

		// go(=이 UnitVisual의 트랜스폼)의 실제 localScale을 역산한다(2026-08-24 수정) — 예전엔 항상
		// footprint를 그대로 썼는데, UnitVisualDefinition.visualScaleIgnoresFootprint가 켜진 유닛
		// (보스 골렘 등 — 스프라이트 아트 자체가 이미 footprint 배율로 그려져 있어 루트 localScale이
		// footprint가 아니라 1로 고정됨, UnitGenerate.SetupUnitVisual 참고)에서는 실제 부모 스케일이
		// footprint와 달라져 라벨이 잘못된 배율/위치로 렌더링됐다. "풋프린트 상단 + 월드 여백"이라는
		// 월드 공간 목표 자체는 그대로다 — 로컬 좌표로 환산할 때 나누는 값만 실제 부모 스케일로 바꾼다.
		Vector3 parentScale = transform.localScale;
		float invX = parentScale.x != 0f ? 1f / parentScale.x : 1f;
		float invY = parentScale.y != 0f ? 1f / parentScale.y : 1f;
		go.transform.localScale = new Vector3(invX, invY, 1f);
		go.transform.localPosition = new Vector3(0f, (footprint.y + StatusLabelWorldOffsetAboveTop) * invY, 0f);
	}

	// ─────────────────────────── 체력바 (머리 위, 월드 고정, 항상 표시) ───────────────────────────
	// 2026-08-24 사용자 요청 "유닛의 머리 위에 체력바 항상 뜨도록 표기해줘". ObjectProgressBarVisual
	// (함정 해제/코어 조사 진행률)과 동일한 배경+채움 SpriteRenderer 2장 구성을 재사용하되, 그쪽은
	// 오브젝트 "아래"에 붙는 반면 이 체력바는 StatusLabel과 같은 "머리 위" 계열이라 별도로 둔다 — 위치
	// 계산도 StatusLabel/EnsureBelowLabel과 동일하게 실제 부모 localScale을 역산한다(같은 이유,
	// visualScaleIgnoresFootprint 유닛 대응).
	private SpriteRenderer _healthBarBg;
	private SpriteRenderer _healthBarFill;
	private float _healthBarFillBaseScaleX;
	private const int HealthBarSortingOrder = 19; // 상태 라벨(20)보다 한 단계 아래, 시야/인지선보다는 위
	private const float HealthBarWidth = 0.8f;
	private const float HealthBarHeight = 0.12f;
	private const float HealthBarWorldOffsetAboveTop = 0.2f; // 유닛 스프라이트 상단에서 띄우는 높이(월드 단위)

	private static Sprite _sharedHealthBarCenterSprite;
	private static Sprite _sharedHealthBarLeftSprite;

	// 단일 색상(2026-08-24 사용자 요청 "체력바 단일 색상으로 처리해주고, 초록색 말고 다른색으로 해줘.
	// 바닥 색이랑 겹쳐서 안봄") — 원래 비율별 초록/노랑/빨강 3색이었는데, 초록이 바닥 타일 색과 거의
	// 구분이 안 돼 요청으로 고정 단색으로 바꿨다. 바닥/시체·오브젝트 색과 잘 겹치지 않는 선명한
	// 마젠타 계열로 선택.
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

		// StatusLabel과 동일한 이유/방식(2026-08-24 수정 — 실제 부모 localScale을 역산, footprint를
		// 그대로 쓰지 않음)으로 스케일을 역산하고, 위치는 "풋프린트 하단 - 월드 여백"으로 뒤집는다.
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
	// 2026-08-24 사용자 요청 "유닛에게 명령 실행시, 유닛이 명령받은 지점과, 명령 경로가 뜨도록 시각화를
	// 하게 해줘. 길찾기 알고리즘에 따른 변경도 같이 실시간 반영. 이 시각화는 유닛 선택 중일때만 보임.
	// (다중 선택했을때도.)" — 위 시야/인지 콘과 달리 유닛 트랜스폼을 따라다니면 안 되므로(경로는 절대
	// 월드 좌표) useWorldSpace=true로 별도 LineRenderer를 쓴다. 새로 경로를 계산하지 않고
	// Unit.MovementAlgorithm(AStarMovement)이 실제 이동 판단에 쓰던 캐시(_cacheTarget/_pathMap)를 그대로
	// 읽기만 하므로, 길찾기가 다시 도는 순간(장애물 변화 등) 자동으로 갱신된 경로가 반영된다.
	private LineRenderer _commandPathLine;
	private LineRenderer _commandDestMarker;
	private static readonly Color CommandPathColor = new Color(1f, 0.95f, 0.2f, 0.95f); // 선명한 노랑
	private const float CommandPathLineWidth = 0.06f;
	private const float CommandDestMarkerRadius = 0.28f;
	private const int CommandPathSortingOrder = 12;
	private const int CommandDestMarkerSortingOrder = 13;

	// floorOffset(2026-08-24 버그 수정, 사용자 신고 "안보이는데?") — unit.position은 층별 로컬 그리드
	// 좌표라 실제 월드 좌표가 되려면 그 층의 타일맵 오프셋(UnitGenerate.GetFloorOffset)을 더해야 한다
	// (SyncVisual의 newPos 계산과 동일한 이유). 이 오프셋 없이 그리면 여러 층이 월드 공간에 나란히
	// 떨어져 배치돼 있는 경우 엉뚱한(대개 화면 밖) 위치에 그려져 아예 안 보였다.
	public void UpdateCommandPathVisual(bool visible, List<Vector2Int> pathTiles, Vector3 floorOffset)
	{
		if (!visible || pathTiles == null || pathTiles.Count < 2)
		{
			if (_commandPathLine != null) _commandPathLine.enabled = false;
			if (_commandDestMarker != null) _commandDestMarker.enabled = false;
			return;
		}

		EnsureCommandPathVisuals();
		_commandPathLine.enabled = true;
		_commandDestMarker.enabled = true;

		_commandPathLine.positionCount = pathTiles.Count;
		for (int i = 0; i < pathTiles.Count; i++)
			_commandPathLine.SetPosition(i, TileCenterWorld(pathTiles[i], floorOffset));

		DrawWorldCircle(_commandDestMarker, TileCenterWorld(pathTiles[pathTiles.Count - 1], floorOffset), CommandDestMarkerRadius);
	}

	private static Vector3 TileCenterWorld(Vector2Int tile, Vector3 floorOffset) => new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f) + floorOffset;

	private void EnsureCommandPathVisuals()
	{
		if (_commandPathLine != null) return;

		_commandPathLine = CreateWorldLine("CommandPathLine", CommandPathColor, CommandPathLineWidth, CommandPathSortingOrder);
		_commandDestMarker = CreateWorldLine("CommandDestMarker", CommandPathColor, CommandPathLineWidth, CommandDestMarkerSortingOrder);
		_commandDestMarker.loop = true;
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

	private static void DrawWorldCircle(LineRenderer line, Vector3 worldCenter, float radius)
	{
		if (line == null) return;

		int segments = 16;
		line.loop = true;
		line.positionCount = segments;
		for (int i = 0; i < segments; i++)
		{
			float rad = (360f * i / segments) * Mathf.Deg2Rad;
			line.SetPosition(i, worldCenter + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius);
		}
	}
}
