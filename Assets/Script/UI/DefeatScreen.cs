using UnityEngine;

// 패배 화면에 보여 줄 한 판의 기록.
public readonly struct DefeatSummary
{
    public readonly DefeatReason Reason;
    public readonly float ElapsedSeconds;
    public readonly int KillCount;
    public readonly int WaveCount;

    public DefeatSummary(DefeatReason reason, float elapsedSeconds, int killCount, int waveCount)
    {
        Reason = reason;
        ElapsedSeconds = elapsedSeconds;
        KillCount = killCount;
        WaveCount = waveCount;
    }
}

// 패배 화면 — rythoom의 사망 화면(DeathScreen) 연출을 따른다: 게임을 멈추고 붉은 어두운 배경이 짧게 페이드인한 뒤 제목과 한 판의 기록이 보이고,
// 버튼은 배경 페이드가 끝나고도 잠깐 숨겨 뒀다가 나타난다(패배 직전에 누르던 클릭으로 실수로 눌리지 않게). 되돌릴 수 없는 종료 상태라 ESC로 닫히지 않고
// "타이틀로 이동"만 유일한 탈출구다. 그리기는 NoticeCenter와 같은 OnGUI + unscaledTime이라 timeScale≈0에서도 동작한다. 타이밍은 DefeatMath.
public class DefeatScreen : MonoBehaviour
{
    // InputManager가 패배 중에는 월드 입력·일시정지 토글·ESC를 모두 막는다.
    public static bool IsShown { get; private set; }

    private static readonly Color DimColor = new Color(0.15f, 0.02f, 0.02f, 0.75f);
    private static readonly Color TitleColor = new Color(0.95f, 0.25f, 0.25f, 1f);
    private static readonly Color BodyColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    private static readonly Color ButtonColor = GUIMenuStyleUtil.InactiveColor;
    private static readonly Color ButtonHoverColor = GUIMenuStyleUtil.ActiveColor;

    private DefeatSummary _summary;
    private GameSession _session;
    private float _shownAt;
    private bool _leaving;
    private GUIStyle _titleStyle;
    private GUIStyle _bodyStyle;
    private GUIStyle _buttonStyle;
    private int _styleHeight;

    // Enter Play Mode 설정에서 도메인 리로드를 끄면 정적 값이 판 사이에 남는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsShown = false;

    public static void Show(GameSession session, DefeatSummary summary)
    {
        if (IsShown) return;
        IsShown = true;

        // GameSettingsPanel.Open과 같은 일시정지 규칙 — timeScale을 0이 아닌 0.0001로 둔다.
        if (session != null) session.isPaused = true;
        Time.timeScale = 0.0001f;
        // ESC 설정 패널이 열린 채 패배하면 두 오버레이가 겹치고 재개 버튼이 게임을 다시 돌릴 수 있다.
        GameSettingsPanel.Instance?.ClosePanel();

        var go = new GameObject("DefeatScreen");
        var screen = go.AddComponent<DefeatScreen>();
        screen._summary = summary;
        screen._session = session;
        screen._shownAt = Time.unscaledTime;
    }

    private void OnDestroy()
    {
        IsShown = false;
    }

    private void OnGUI()
    {
        GUI.depth = -10000; // 가장 앞 — 다른 OnGUI 패널보다 먼저 마우스 이벤트를 받는다.

        float t = Time.unscaledTime - _shownAt;
        float alpha = DefeatMath.FadeAlpha(t);
        float scale = Mathf.Max(0.75f, Screen.height / 1080f);
        EnsureStyles(scale);

        Color prevColor = GUI.color;

        GUI.color = new Color(DimColor.r, DimColor.g, DimColor.b, DimColor.a * alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        // rythoom은 Dim과 패널을 한 루트의 알파로 같이 페이드한다.
        GUI.color = new Color(1f, 1f, 1f, alpha);

        float panelWidth = 640f * scale;
        float x = (Screen.width - panelWidth) * 0.5f;
        float y = Screen.height * 0.5f - 210f * scale;

        _titleStyle.normal.textColor = TitleColor;
        GUI.Label(new Rect(x, y, panelWidth, 80f * scale), "패배", _titleStyle);
        y += 92f * scale;

        _bodyStyle.normal.textColor = BodyColor;
        GUI.Label(new Rect(x, y, panelWidth, 40f * scale), DefeatMath.Describe(_summary.Reason), _bodyStyle);
        y += 64f * scale;

        GUI.Label(new Rect(x, y, panelWidth, 36f * scale), $"버틴 시간: {DefeatMath.FormatElapsed(_summary.ElapsedSeconds)}", _bodyStyle);
        y += 38f * scale;
        GUI.Label(new Rect(x, y, panelWidth, 36f * scale), $"처치한 유닛: {_summary.KillCount}", _bodyStyle);
        y += 38f * scale;
        GUI.Label(new Rect(x, y, panelWidth, 36f * scale), $"맞이한 웨이브: {_summary.WaveCount}", _bodyStyle);
        y += 70f * scale;

        DrawReturnButton(new Rect(x + (panelWidth - 320f * scale) * 0.5f, y, 320f * scale, 60f * scale), t);

        GUI.color = prevColor;

        // 뒤에 깔린 다른 OnGUI 패널이 클릭을 받지 않게 남은 마우스 이벤트를 소비한다(버튼이 이미 처리한 이벤트는 Used라 영향 없음).
        Event e = Event.current;
        if (e != null && (e.isMouse || e.type == EventType.ScrollWheel)) e.Use();
    }

    private void DrawReturnButton(Rect rect, float secondsSinceShown)
    {
        float buttonAlpha = DefeatMath.ButtonAlpha(secondsSinceShown);
        if (buttonAlpha <= 0f) return; // 숨겨진 동안은 클릭 자체가 안 먹힌다.

        Event e = Event.current;
        bool hover = e != null && rect.Contains(e.mousePosition);
        Color fill = hover ? ButtonHoverColor : ButtonColor;

        GUI.color = new Color(fill.r, fill.g, fill.b, fill.a * buttonAlpha);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, buttonAlpha);
        GUIMenuStyleUtil.DrawButtonBorder(rect);

        if (GUI.Button(rect, "타이틀로 이동", _buttonStyle) && !_leaving)
        {
            _leaving = true;
            GameExitFlow.ReturnToTitle(_session);
        }
    }

    // 화면 높이가 바뀌면 글자 크기를 다시 맞춘다.
    private void EnsureStyles(float scale)
    {
        if (_titleStyle != null && _styleHeight == Screen.height) return;
        _styleHeight = Screen.height;

        _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(64f * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26f * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        _buttonStyle = new GUIStyle(GUIStyle.none) { fontSize = Mathf.RoundToInt(24f * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _buttonStyle.normal.textColor = Color.white;
        _buttonStyle.hover.textColor = Color.white;
        _buttonStyle.active.textColor = Color.white;
        _buttonStyle.focused.textColor = Color.white;
    }
}
