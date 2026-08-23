using UnityEngine;
using VContainer;
using DG.Tweening;
using Haare.Client.Routine;
using Haare.Client.UI;

// 부팅 시 DebugInfoPanel/도감 패널을 띄우던 역할은 GameUIPresenter(Haare UIPresenter/RegisterEntryPoint
// 경로)로 옮겨졌다 — 이 클래스는 이제 OnGUI() 오버레이(게임 속도/일시정지 표시)와 플로팅 텍스트만 담당한다.
//
// UI 리팩토링(2026-08-20, 사용자 요청 "모든 UI Haare 프레임워크에 편입") — 예전엔 GameCompositionRoot가
// RegisterComponentOnNewGameObject로 직접 배선하는 "느슨한" MonoBehaviour였는데(NoticeCenter와 동일
// 사유), 다른 UI 패널들(BuildingControlPanel/BottomMenuBar 등)과 동일하게 [PanelAttribute] Haare
// ICustomPanel로 편입했다. 그 대신 Unit.cs의 모든 유닛이 쓰던 [Inject] private UIManager _uiManager
// 생성자 주입은(Haare 패널은 VContainer 컨테이너에 등록되지 않아 더는 주입받을 수 없다) BuildingControlPanel.
// Instance와 동일한 정적 Instance 접근으로 교체했다(사용자 확인) — GameSession.Initialize의
// _resolver.Resolve<UIManager>() 강제 인스턴스화 호출도 더는 필요 없어 제거됐다(GameUIPresenter.
// BootSequence가 그 역할을 대신함).
[PanelAttribute("Prefabs/UIManager")]
public class UIManager : MonoRoutine, ICustomPanel
{
    public SceneUIManager uiManager { get; set; }
    public GameObject panel { get; set; }

    // Unit.UI 프로퍼티(Unit.cs) 등 정적 접근이 필요한 소비처가 쓴다.
    public static UIManager Instance { get; private set; }

    private GameSession _gameSession;

    [Inject]
    public void Construct(GameSession gameSession)
    {
        _gameSession = gameSession;
    }

    protected override void Constructor()
    {
        base.Constructor();
        Instance = this;
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        panel = gameObject;
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    public void BindEvent() { }

    void OnGUI()
    {
        if (_gameSession == null) return;

        DrawTopLeftUI();
	}

	// 사용자 요청(2026-08-20 "좌상단의 배속... 글자 크기 좀 키워주고, 메뉴 스타일로 바꿔줘" → "배속표시는
	// 크기 좀 줄이자" → "배속 표시 다시 큰 상태로 만들어주고, 텍스트 잘리지 않게만 해줘. 가로 길이 너무
	// 길어서 빈 공간이 많이 보임") — 주 문구(게임 속도)는 메뉴 스타일 큰 굵은 글씨로 키우고, 보조 안내문
	// (조작키 힌트)은 작은 글씨로 남겨 두 줄의 실제 렌더 크기를 각각 측정한 뒤 박스를 그 최대 폭에 맞춰
	// 동적으로 그린다 — 고정폭(300)을 쓰면 큰 글씨일 때 잘리거나 작은 글씨일 때 빈 공간이 남는 문제를
	// 동시에 해결.
	private const int SpeedIndicatorFontSize = GUIMenuStyleUtil.LabelFontSize;
	private const int SpeedIndicatorHintFontSize = 13;
	private static GUIStyle _speedIndicatorStyle;
	private static GUIStyle _speedIndicatorHintStyle;

	private GUIStyle GetSpeedIndicatorStyle()
	{
		if (_speedIndicatorStyle == null)
		{
			_speedIndicatorStyle = new GUIStyle(GUI.skin.label) { fontSize = SpeedIndicatorFontSize, fontStyle = FontStyle.Bold, richText = true };
			_speedIndicatorStyle.normal.textColor = Color.white;
		}
		return _speedIndicatorStyle;
	}

	private GUIStyle GetSpeedIndicatorHintStyle()
	{
		if (_speedIndicatorHintStyle == null)
		{
			_speedIndicatorHintStyle = new GUIStyle(GUI.skin.label) { fontSize = SpeedIndicatorHintFontSize, richText = true };
			_speedIndicatorHintStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f, 1f);
		}
		return _speedIndicatorHintStyle;
	}

	private void DrawTopLeftUI()
    {
        int y = 50; // 좌상단 FPS 카운터(CoreCanvas의 FPSText, y 10~40)와 안 겹치게 그 아래부터 시작.

        string line1 = $"게임 속도: {_gameSession.currentGameSpeed}x {(_gameSession.isPaused ? "<color=red>[일시정지]</color>" : "")}";
        const string line2 = "Space: 일시정지 | 1/2/3/4: 배속(0.5x/1.0x/1.5x/2.0x)";

        GUIStyle line1Style = GetSpeedIndicatorStyle();
        GUIStyle line2Style = GetSpeedIndicatorHintStyle();
        Vector2 size1 = line1Style.CalcSize(new GUIContent(line1));
        Vector2 size2 = line2Style.CalcSize(new GUIContent(line2));

        const int paddingX = 10;
        const int paddingY = 6;
        const int lineGap = 2;
        float width = Mathf.Max(size1.x, size2.x) + paddingX * 2;
        float height = size1.y + size2.y + lineGap + paddingY * 2;

        Rect rect = new Rect(10, y, width, height);
        GUIMenuStyleUtil.DrawPanelBox(rect);
        GUI.Label(new Rect(rect.x + paddingX, rect.y + paddingY, size1.x, size1.y), line1, line1Style);
        GUI.Label(new Rect(rect.x + paddingX, rect.y + paddingY + size1.y + lineGap, size2.x, size2.y), line2, line2Style);
    }

	// 2026-08-20 — ShowFloatingText(Unit)는 위치/기본색/기본시간만 유닛 기준으로 채워 ShowFloatingTextAt로
	// 위임한다(둘이 거의 동일한 바디를 중복 구현하고 있었음).
	public void ShowFloatingText(Unit unit, string message)
	{
		if (unit == null) return;

		Vector3 pos = GetWorldTextPosition(unit);
		pos.y -= 16.7f;
		Color color = (unit is Human) ? Color.green : Color.red;

		ShowFloatingTextAt(pos, message, color);
	}

	// 03문서 9-8장(2026-07-27 추가) — 함정 해제 성공/실패 결과 문구용. ShowFloatingText(Unit)와 달리
	// 유닛이 아니라 임의의 월드 좌표(함정 위치 등)에 띄워야 해서 별도 오버로드로 뒀다.
	public void ShowFloatingTextAt(Vector3 worldPos, string message, Color color, float duration = 0.5f)
	{
		GameObject go = new GameObject("FloatingText");
		go.transform.position = worldPos;

		TextMesh text = go.AddComponent<TextMesh>();
		text.text = message;

		text.fontSize = 90;
		text.characterSize = 0.05f;

		text.anchor = TextAnchor.MiddleCenter;
		text.alignment = TextAlignment.Center;
		text.color = color;

		MeshRenderer mr = go.GetComponent<MeshRenderer>();
		mr.sortingOrder = 9999; // ★ 핵심 (맵 위로 올림)

		DOVirtual.DelayedCall(duration, () => { if (go != null) Destroy(go); });
	}

	Vector3 GetWorldTextPosition(Unit unit)
	{
		return new Vector3(
			unit.position.x + unit.unitType.footprint.x * 0.5f,
			unit.position.y + unit.unitType.footprint.y + 1f,
			0
		);
	}
}
