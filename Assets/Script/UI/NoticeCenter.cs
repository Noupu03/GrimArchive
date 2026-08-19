using System.Collections.Generic;
using UnityEngine;

// Notice 시스템(2026-08-19 신규, 사용자 요청 "배치모드시 뜨는 방 선택 문구와, 웨이브 시작을 알리는
// 문구들을 시스템화") — 화면 어디서든 한 줄로 띄울 수 있는 스택형 알림. 다른 rythoom 프로젝트
// (C:\Users\songs\Documents\GitHub\rythoom\adapter\Autoload\NoticeCenter.cs, Godot Autoload)의
// 동일한 개념/API를 이 프로젝트 관례로 그대로 옮겼다 — 프리팹/Addressables 없이 GameCompositionRoot가
// InputManager/UIManager와 같은 방식(RegisterComponentOnNewGameObject)으로 항상 하나 띄워두고,
// 실제 그리기는 OnGUI로 한다("프리팹 껍데기 + OnGUI" 대신 아예 프리팹도 없는, UIManager와 동일한
// 상시 오버레이 패턴).
//
// 쓰는 법 — 어디서든:
//   NoticeCenter.Instance?.Push("문구");
// ResourceManager.Instance/HumanWaveManager.Instance 등과 완전히 같은 접근 방식이라 새로 익힐 게
// 없다. 강조색을 주려면 두 번째 인자로 QuestCompleteColor/WarningColor 중 하나(또는 아무 Color)를
// 넘기면 되고, 생략하면 무난한 InfoColor. 지속시간도 세 번째 인자로 초 단위 override 가능(생략하면
// DefaultDurationSeconds).
//
// 상태에 따라 문구가 바뀌고 특정 시점에만 사라져야 하는 경우(예: 배치 모드 진행 안내)는 대신
// PushPersistent(key, 문구)/Remove(key)를 쓴다 — 같은 key로 다시 부르면 기존 걸 지우고 새로
// 넣어서 문구를 교체하고, 시간이 지나도 자동으로 사라지지 않다가 Remove(key)를 부를 때만 사라진다.
//
// "스택형" — 알림마다 독립적인 남은 시간을 가지고 있어서 여러 개가 동시에 화면에 쌓여 보일 수
// 있다. 화면 상단 중앙에 먼저 뜬 게 위, 나중에 뜬 게 아래로 쌓이고, 각자 지속시간이 끝나면
// 사라지며 아래 알림들이 자동으로 한 칸씩 올라온다(고정 슬롯이 아니라 살아있는 알림 목록을 매
// 프레임 다시 배치).
//
// 일시정지 무시(2026-08-19 수정, 사용자 요청 "시간 멈춤에 영향받지 않게 해줘") — 이 프로젝트의
// 일시정지/배치 모드는 Time.timeScale을 0에 가깝게(0.0001f) 낮추는 방식이다. 처음엔 Time.deltaTime
// (스케일 적용)으로 남은 시간을 깎아 일시정지 중 알림도 함께 멈추게 했었는데, 몬스터 배치 모드
// 진입 알림처럼 "모드 진입 자체가 시간을 멈추는" 경우 알림이 사실상 안 사라지는 문제가 있었다.
// Time.unscaledDeltaTime을 써서 게임이 멈춰 있어도 알림은 항상 실시간으로 뜨고 사라진다.
public class NoticeCenter : MonoBehaviour
{
    public static NoticeCenter Instance { get; private set; }

    private readonly struct Notice
    {
        // 지속형 알림(PushPersistent)을 찾아 교체/제거하는 데 쓰는 키 — 일반 Push로 쌓는 알림은
        // null로 둔다(스택에 계속 쌓이기만 함, 기존 동작).
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
    // 지속형 알림(PushPersistent)의 "지속시간" — 자동 만료가 아니라 Remove(key)로만 사라지므로 그냥
    // 충분히 긴 값(하루). RemainingSeconds가 이 값에서 시작해 실시간으로 계속 줄어들긴 하지만
    // 세션 중 0 밑으로 내려갈 일이 없다.
    private const float PersistentDurationSeconds = 86400f;
    private const float FadeSeconds = 0.4f;
    // 사용자 요청(2026-08-19 "notice ui 조금 아래로 내려줘. 웨이브 진행바랑 겹친다") — WaveGaugePanel이
    // 화면 상단 중앙 y10~약42(BarTopMargin 10 + barHeight ≈32)를 차지하므로 그 아래로 여유를 두고 배치.
    private const float TopMargin = 60f;
    private const float BoxWidth = 480f;
    private const float BoxHeight = 52f;
    private const float BoxGap = 10f;
    private const float AccentBarWidth = 6f;
    private const float BorderWidth = 2f;
    private const int FontSize = 19;
    private static readonly Color BoxBackgroundColor = new Color(0.04f, 0.04f, 0.06f, 0.92f);
    private static readonly Color TextColor = Color.white;

    private readonly List<Notice> _notices = new List<Notice>();

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 알림을 스택에 새로 추가한다. accentColor를 생략하면 InfoColor, durationSeconds를 생략하면
    // DefaultDurationSeconds. 시간이 지나면 자동으로 사라진다(일회성 이벤트 알림용).
    public void Push(string text, Color? accentColor = null, float? durationSeconds = null)
    {
        float duration = durationSeconds ?? DefaultDurationSeconds;
        _notices.Add(new Notice(null, text, accentColor ?? InfoColor, duration, duration));
    }

    // 지속형 알림(2026-08-19 신규, 사용자 요청 "이 세 알림은 현재 무슨 배치를 사용하고 있는가에
    // 따라 지워졌다가 새로 생기고, 배치모드 완전히 종료 시에만 사라지게") — 시간이 지나도 자동으로
    // 사라지지 않고 Remove(key)를 직접 호출해야만 사라진다. 같은 key로 다시 부르면 기존 것을
    // 지우고 새로 넣는다(페이드 인이 다시 시작돼 "새로 생긴" 느낌을 준다) — 상태가 바뀔 때마다
    // 같은 key로 다시 호출하면 되는 구조.
    public void PushPersistent(string key, string text, Color? accentColor = null)
    {
        _notices.RemoveAll(n => n.Key == key);
        _notices.Add(new Notice(key, text, accentColor ?? InfoColor, PersistentDurationSeconds, PersistentDurationSeconds));
    }

    // key로 지정한 지속형 알림을 제거한다(예: 배치모드 완전 종료 시).
    public void Remove(string key)
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
            richText = true
        };

        foreach (var notice in _notices)
        {
            float elapsed = notice.DurationSeconds - notice.RemainingSeconds;
            float alpha = ComputeAlpha(elapsed, notice.RemainingSeconds);

            Rect rect = new Rect(x, y, BoxWidth, BoxHeight);

            GUI.color = BoxBackgroundColor * new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = notice.AccentColor * new Color(1f, 1f, 1f, alpha);
            DrawRectBorder(rect, BorderWidth);
            GUI.DrawTexture(new Rect(rect.x, rect.y, AccentBarWidth, rect.height), Texture2D.whiteTexture);

            style.normal.textColor = TextColor * new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(rect.x + AccentBarWidth + 12f, rect.y, rect.width - AccentBarWidth - 20f, rect.height), notice.Text, style);

            y += BoxHeight + BoxGap;
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

    private static void DrawRectBorder(Rect r, float thickness)
    {
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMin, r.yMax - thickness, r.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, thickness, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - thickness, r.yMin, thickness, r.height), Texture2D.whiteTexture);
    }
}
