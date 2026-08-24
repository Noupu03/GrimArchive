using System.Collections.Generic;
using UnityEngine;
using Haare.Client.Routine;
using Haare.Client.UI;

// Notice 시스템(2026-08-19 신규, 사용자 요청 "배치모드시 뜨는 방 선택 문구와, 웨이브 시작을 알리는
// 문구들을 시스템화") — 화면 어디서든 한 줄로 띄울 수 있는 스택형 알림. 다른 rythoom 프로젝트
// (C:\Users\songs\Documents\GitHub\rythoom\adapter\Autoload\NoticeCenter.cs, Godot Autoload)의
// 동일한 개념/API를 이 프로젝트 관례로 그대로 옮겼다. Haare ICustomPanel로 편입된 뒤(아래 2026-08-20
// 단락 참고)로는 다른 UI 패널들과 동일하게 빈 프리팹 + GameUIPresenter.BootSequence 부팅 로드를
// 쓴다 — 실제 그리기는 여전히 OnGUI.
//
// 두 종류로 명확히 구분해서 캡슐화한다(2026-08-20, 사용자 요청 "notice를 두개로 구분하자... 이 두개
// 구분해서 캡슐화 해두자" — 이름만 봐도 어느 쪽을 써야 할지 알 수 있게 하는 게 목적, 예전엔 Push/
// PushPersistent라는 이름이라 "지금 상황에 어느 걸 써야 하나"가 호출부 주석에 의존했었다):
//
//   1. 고정형(PushFixed/ClearFixed) — 특정 상황이 끝날 때까지 화면에 계속 떠 있는다. 자동 만료 없음,
//      ClearFixed(key)를 직접 불러야만 사라진다. 같은 key로 다시 PushFixed하면 문구만 교체된다(상태
//      전이마다 같은 key로 다시 부르는 구조). 사용처: 소집 배치 모드 진행 안내
//      (MonsterPlacementController.PlacementNoticeKey), 인류 웨이브 던전 입구 안내
//      (DungeonEntranceSystem.EntranceNoticeKey) — 둘 다 "그 상황이 끝날 때까지 계속 알아야 하는" 정보.
//   2. 순간형(PushMomentary) — 몇 초 뒤 자동으로 사라지는 일회성 알림. 그 외 모든 notice(자원 부족,
//      명령 취소, 배치 모드 시작/취소, 층 이동 등)가 여기 해당.
//
// 쓰는 법 — 어디서든:
//   NoticeCenter.Instance?.PushMomentary("문구");
// ResourceManager.Instance/HumanWaveManager.Instance 등과 완전히 같은 접근 방식이라 새로 익힐 게
// 없다. 강조색을 주려면 두 번째 인자로 QuestCompleteColor/WarningColor 중 하나(또는 아무 Color)를
// 넘기면 되고, 생략하면 무난한 InfoColor. 지속시간도 세 번째 인자로 초 단위 override 가능(생략하면
// DefaultDurationSeconds).
//
// "스택형" — 알림마다 독립적인 남은 시간을 가지고 있어서 여러 개가 동시에 화면에 쌓여 보일 수
// 있다. 화면 상단 중앙에 먼저 뜬 게 위, 나중에 뜬 게 아래로 쌓이고, 각자 지속시간이 끝나면
// 사라지며 아래 알림들이 자동으로 한 칸씩 올라온다(고정 슬롯이 아니라 살아있는 알림 목록을 매
// 프레임 다시 배치). 고정형/순간형 둘 다 같은 스택에 같이 쌓인다 — 화면상으로는 종류가 안 갈리고,
// "언제 사라지는가"만 다르다.
//
// 일시정지 무시(2026-08-19 수정, 사용자 요청 "시간 멈춤에 영향받지 않게 해줘") — 이 프로젝트의
// 일시정지/배치 모드는 Time.timeScale을 0에 가깝게(0.0001f) 낮추는 방식이다. 처음엔 Time.deltaTime
// (스케일 적용)으로 남은 시간을 깎아 일시정지 중 알림도 함께 멈추게 했었는데, 몬스터 배치 모드
// 진입 알림처럼 "모드 진입 자체가 시간을 멈추는" 경우 알림이 사실상 안 사라지는 문제가 있었다.
// Time.unscaledDeltaTime을 써서 게임이 멈춰 있어도 알림은 항상 실시간으로 뜨고 사라진다 — 고정형/
// 순간형 둘 다 이 규칙을 따른다(2026-08-20 사용자 확인: "notice가 시간에 영향받지 않게 하라는거지,
// 웨이브 로직이 시간에 영향 받지 않게 하란 소리가 아니야" — 정지 중 멈춰야 하는 건 게임 로직 쪽이지
// notice의 표시/소멸 타이밍이 아니다).
// UI 리팩토링(2026-08-20, 사용자 요청 "모든 UI Haare 프레임워크에 편입") — 예전엔 GameCompositionRoot가
// RegisterComponentOnNewGameObject로 직접 배선하는 "느슨한" MonoBehaviour였는데(UIManager와 동일
// 사유), 다른 UI 패널들(BuildingControlPanel/BottomMenuBar 등)과 동일하게 [PanelAttribute] Haare
// ICustomPanel로 편입했다. 실제 그리기는 여전히 OnGUI(이 프로젝트의 확립된 관례, 빈 프리팹 +
// OnGUI) — 접근 방식(Instance 정적 접근)은 전혀 안 바뀌어서 호출부 변경은 필요 없었다.
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
    // 고정형 알림(PushFixed)의 "지속시간" — 자동 만료가 아니라 ClearFixed(key)로만 사라지므로 그냥
    // 충분히 긴 값(하루). RemainingSeconds가 이 값에서 시작해 실시간으로 계속 줄어들긴 하지만
    // 세션 중 0 밑으로 내려갈 일이 없다.
    private const float PersistentDurationSeconds = 86400f;
    private const float FadeSeconds = 0.4f;
    // 사용자 요청(2026-08-19 "notice ui 조금 아래로 내려줘. 웨이브 진행바랑 겹친다") — WaveGaugePanel
    // 바로 아래에 여유를 두고 배치한다. 예전엔 이 여백을 상수(60f)로 하드코딩해서, 나중에 게이지
    // 크기가 바뀌면(2026-08-21, 사용자 요청 "웨이브 시각화 UI 크기를 50% 늘려줘") 손으로 다시 맞춰야
    // 했다(정확히 방 점령색 alpha 드리프트와 같은 종류의 실수 위험) — 이제 WaveGaugePanel.
    // GetReservedTopHeight()를 그대로 물어봐서 항상 실제 크기에 맞게 계산한다(BottomMenuBar.
    // GetReservedBottomLeftHeight와 동일 관례). 인스턴스가 아직 없으면(패널 부팅 순서 등) 예전 상수로
    // 폴백.
    private const float TopMarginGap = 18f;
    private const float TopMarginFallback = 60f;

    private static float TopMargin
        => WaveGaugePanel.Instance != null
            ? WaveGaugePanel.Instance.GetReservedTopHeight() + TopMarginGap
            : TopMarginFallback;
    private const float BoxWidth = 480f;
    private const float BoxHeight = 52f;
    private const float BoxGap = 10f;
    // 사용자 요청(2026-08-20 "notice에 텍스트가 초과되지 않도록 해줘") — 문구가 한 줄(BoxHeight 기준)에
    // 안 들어갈 만큼 길면 줄바꿈해서 박스 높이 자체를 늘린다. 이 값은 늘어난 박스 안에서 텍스트 위/
    // 아래 여백으로 쓴다.
    private const float BoxVerticalPadding = 14f;
    private const float AccentBarWidth = 6f;
    private const float BorderWidth = 2f;
    private const int FontSize = 19;
    private static readonly Color BoxBackgroundColor = new Color(0.04f, 0.04f, 0.06f, 0.92f);
    private static readonly Color TextColor = Color.white;

    private readonly List<Notice> _notices = new List<Notice>();

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
    // 순간형(Momentary) — 몇 초 뒤 자동으로 사라지는 일회성 알림. accentColor를 생략하면 InfoColor,
    // durationSeconds를 생략하면 DefaultDurationSeconds. 그 외 모든 notice(자원 부족, 명령 취소,
    // 배치 모드 시작/취소 등)는 전부 이걸 쓴다.
    // =====================================================
    public void PushMomentary(string text, Color? accentColor = null, float? durationSeconds = null)
    {
        float duration = durationSeconds ?? DefaultDurationSeconds;
        _notices.Add(new Notice(null, text, accentColor ?? InfoColor, duration, duration));
    }

    // =====================================================
    // 고정형(Fixed) — 특정 상황이 끝날 때까지 계속 떠 있는 알림(2026-08-19 신규, 사용자 요청 "이 세
    // 알림은 현재 무슨 배치를 사용하고 있는가에 따라 지워졌다가 새로 생기고, 배치모드 완전히 종료
    // 시에만 사라지게"). 시간이 지나도 자동으로 사라지지 않고 ClearFixed(key)를 직접 호출해야만
    // 사라진다. 같은 key로 다시 부르면 기존 것을 지우고 새로 넣는다(페이드 인이 다시 시작돼 "새로
    // 생긴" 느낌을 준다) — 상태가 바뀔 때마다 같은 key로 다시 호출하면 되는 구조. 사용처: 소집 배치
    // 모드 진행 안내, 인류 웨이브 던전 입구 안내 — 2026-08-20 클래스 상단 doc 참고.
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

        GUIStyle style = new GUIStyle(GUI.skin.label)
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
            float textHeight = style.CalcHeight(new GUIContent(notice.Text), textAreaWidth);
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

    // 등장/퇴장 둘 다 FadeSeconds에 걸쳐 부드럽게 — elapsed/remaining 중 더 작은 쪽(둘 다
    // FadeSeconds로 정규화)이 기준이라, 지속시간이 FadeSeconds*2보다 짧아도 이상하게 겹쳐 깨지지
    // 않는다.
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
