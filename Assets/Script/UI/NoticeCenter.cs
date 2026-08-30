using System.Collections.Generic;
using UnityEngine;
using Haare.Client.Routine;
using Haare.Client.UI;

// Notice 시스템 — 화면 어디서든 한 줄로 띄울 수 있는 스택형 알림(Haare ICustomPanel, 그리기는
// OnGUI). 고정형(PushFixed/ClearFixed, key로 교체·제거)과 순간형(PushMomentary, 몇 초 뒤 자동
// 소멸) 두 종류가 같은 스택에 쌓이며, 알림마다 독립적인 남은 시간을 가져 사라지면 아래 알림이
// 한 칸씩 올라온다. Time.unscaledDeltaTime을 써서 timeScale≈0(일시정지/배치 모드)에서도 항상
// 실시간으로 뜨고 사라진다.
[PanelAttribute("Prefabs/NoticeCenter")]
public class NoticeCenter : MonoRoutine, ICustomPanel
{
    public SceneUIManager uiManager { get; set; }
    public GameObject panel { get; set; }

    public static NoticeCenter Instance { get; private set; }

    private readonly struct Notice
    {
        // 고정형(PushFixed) 알림을 찾아 교체/제거하는 데 쓰는 키 — 순간형(PushMomentary)으로 쌓는
        // 알림은 null로 둔다(스택에 계속 쌓이기만 함, 기존 동작).
        public readonly string Key;
        public readonly string Text;
        public readonly Color AccentColor;
        public readonly float DurationSeconds;
        public readonly float RemainingSeconds;

        public Notice(string key, string text, Color accentColor, float durationSeconds, float remainingSeconds)
        {
            Key = key;
            Text = text;
            AccentColor = accentColor;
            DurationSeconds = durationSeconds;
            RemainingSeconds = remainingSeconds;
        }

        public Notice WithRemaining(float remaining) => new Notice(Key, Text, AccentColor, DurationSeconds, remaining);
    }

    // 카테고리 구분 없이 쓰는 기본색 — 일반 정보성 알림.
    public static readonly Color InfoColor = new Color(0.85f, 0.85f, 0.9f, 1f);
    // 목표/진행 알림류 — 노란 강조색.
    public static readonly Color QuestCompleteColor = new Color(1f, 0.85f, 0.25f, 1f);
    // 위험/경고성(웨이브 임박 등) — 붉은 계열.
    public static readonly Color WarningColor = new Color(0.95f, 0.35f, 0.3f, 1f);

    private const float DefaultDurationSeconds = 4f;
    // 고정형 알림(PushFixed)의 "지속시간" — ClearFixed(key)로만 사라지므로 충분히 긴 값(하루)을 둔다.
    private const float PersistentDurationSeconds = 86400f;
    private const float FadeSeconds = 0.4f;
    // WaveGaugePanel 바로 아래에 배치 — 여백 하드코딩 대신 GetReservedTopHeight()로 실제 크기에
    // 맞게 계산한다. 인스턴스가 아직 없으면 예전 상수로 폴백.
    private const float TopMarginGap = 18f;
    private const float TopMarginFallback = 60f;

    private static float TopMargin
        => WaveGaugePanel.Instance != null
            ? WaveGaugePanel.Instance.GetReservedTopHeight() + TopMarginGap
            : TopMarginFallback;
    private const float BoxWidth = 480f;
    private const float BoxHeight = 52f;
    private const float BoxGap = 10f;
    // 문구가 한 줄(BoxHeight 기준)에 안 들어갈 만큼 길면 줄바꿈해서 박스 높이 자체를 늘린다. 이 값은
    // 늘어난 박스 안에서 텍스트 위/아래 여백으로 쓴다.
    private const float BoxVerticalPadding = 14f;
    private const float AccentBarWidth = 6f;
    private const float BorderWidth = 2f;
    private const int FontSize = 19;
    private static readonly Color BoxBackgroundColor = new Color(0.04f, 0.04f, 0.06f, 0.92f);
    private static readonly Color TextColor = Color.white;

    private readonly List<Notice> _notices = new List<Notice>();

    // 프레임 드랍 대응 — OnGUI는 Layout+Repaint로 프레임당 최대 2회 호출되므로, GUIStyle은 한 번만
    // 만들어 캐시하고 GUIContent는 재사용 가능한 스크래치 인스턴스 하나의 .text만 매번 바꿔쓴다.
    private GUIStyle _noticeStyle;
    private readonly GUIContent _scratchContent = new GUIContent();

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

    // =====================================================
    // 순간형(Momentary) — 몇 초 뒤 자동으로 사라지는 일회성 알림. 생략 시 InfoColor/DefaultDurationSeconds.
    // =====================================================
    public void PushMomentary(string text, Color? accentColor = null, float? durationSeconds = null)
    {
        float duration = durationSeconds ?? DefaultDurationSeconds;
        _notices.Add(new Notice(null, text, accentColor ?? InfoColor, duration, duration));
    }

    // =====================================================
    // 고정형(Fixed) — ClearFixed(key)를 직접 호출해야만 사라지는 알림. 같은 key로 다시 부르면 기존
    // 것을 지우고 새로 넣는다(페이드 인 재시작) — 상태가 바뀔 때마다 같은 key로 다시 호출하면 된다.
    // =====================================================
    public void PushFixed(string key, string text, Color? accentColor = null)
    {
        _notices.RemoveAll(n => n.Key == key);
        _notices.Add(new Notice(key, text, accentColor ?? InfoColor, PersistentDurationSeconds, PersistentDurationSeconds));
    }

    // key로 지정한 고정형 알림을 제거한다(예: 배치모드 완전 종료 시).
    public void ClearFixed(string key)
    {
        _notices.RemoveAll(n => n.Key == key);
    }

    // 지금 떠 있는 알림을 전부 즉시 지운다(씬 전환 등에서 필요해지면 호출).
    public void ClearAll()
    {
        _notices.Clear();
    }

    private void Update()
    {
        if (_notices.Count == 0) return;

        for (int i = 0; i < _notices.Count; i++)
            _notices[i] = _notices[i].WithRemaining(_notices[i].RemainingSeconds - Time.unscaledDeltaTime);

        _notices.RemoveAll(n => n.RemainingSeconds <= 0f);
    }

    private void OnGUI()
    {
        if (_notices.Count == 0) return;

        float x = (Screen.width - BoxWidth) / 2f;
        float y = TopMargin;
        Color prevColor = GUI.color;

        GUIStyle style = _noticeStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = FontSize,
            fontStyle = FontStyle.Bold,
            richText = true,
            wordWrap = true,
        };

        float textAreaWidth = BoxWidth - AccentBarWidth - 20f;

        foreach (var notice in _notices)
        {
            float elapsed = notice.DurationSeconds - notice.RemainingSeconds;
            float alpha = ComputeAlpha(elapsed, notice.RemainingSeconds);

            // 문구가 길어 한 줄에 안 들어가면 박스 높이를 실제 필요한 줄 수만큼 늘린다(BoxHeight는
            // 최소값으로만 쓴다) — 그래야 텍스트가 박스 밖으로 넘치지 않는다.
            _scratchContent.text = notice.Text;
            float textHeight = style.CalcHeight(_scratchContent, textAreaWidth);
            float boxHeight = Mathf.Max(BoxHeight, textHeight + BoxVerticalPadding);

            Rect rect = new Rect(x, y, BoxWidth, boxHeight);

            GUI.color = BoxBackgroundColor * new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = notice.AccentColor * new Color(1f, 1f, 1f, alpha);
            DrawRectBorder(rect, BorderWidth);
            GUI.DrawTexture(new Rect(rect.x, rect.y, AccentBarWidth, rect.height), Texture2D.whiteTexture);

            style.normal.textColor = TextColor * new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(rect.x + AccentBarWidth + 12f, rect.y, textAreaWidth, rect.height), notice.Text, style);

            y += boxHeight + BoxGap;
        }

        GUI.color = prevColor;
    }

    // 등장/퇴장 둘 다 FadeSeconds에 걸쳐 부드럽게 — elapsed/remaining 중 더 작은 쪽이 기준이라
    // 지속시간이 FadeSeconds*2보다 짧아도 깨지지 않는다.
    private static float ComputeAlpha(float elapsed, float remaining)
    {
        float fadeIn = Mathf.Clamp01(elapsed / FadeSeconds);
        float fadeOut = Mathf.Clamp01(remaining / FadeSeconds);
        return Mathf.Min(fadeIn, fadeOut);
    }

    // InputManager의 드래그 박스 선택 테두리도 같은 구현을 그대로 재사용한다(2026-08-25 리팩토링 —
    // 두 곳이 동일한 4줄을 각자 갖고 있었음).
    internal static void DrawRectBorder(Rect r, float thickness)
    {
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMin, r.yMax - thickness, r.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, thickness, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - thickness, r.yMin, thickness, r.height), Texture2D.whiteTexture);
    }
}
