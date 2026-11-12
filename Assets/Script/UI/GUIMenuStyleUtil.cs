using System.Collections.Generic;
using UnityEngine;

// 하단 메뉴 바(BottomMenuBar)와 정보 UI(DebugInfoPanel) 버튼들이 공유하는 OnGUI 스타일 — 큰 굵은 글씨 +
// 흰 테두리. 두 클래스가 각자 들고 있던 중복 스타일 상수/테두리 코드를 한 곳으로 모았다(GUIMouseUtil/
// GUISpriteUtil과 동일한 이 프로젝트의 OnGUI 공용 유틸 관례).
public static class GUIMenuStyleUtil
{
    public const float ButtonBorderThickness = 2f;
    public static readonly Color ButtonBorderColor = Color.white;
    public const int ButtonFontSize = 18;

    // 선택/활성 상태 배경색. Unity 기본 버튼 스킨 텍스처는 이미 회색조 음영이 들어가 있어 GUI.backgroundColor로
    // 곱색(tint)하면 아무리 올려도 탁하게 보이므로, DrawFlatButton은 스킨 텍스처 대신 Texture2D.whiteTexture
    // 위에 이 색을 그대로 채워 순색이 나오게 한다.
    public static readonly Color ActiveColor = new Color(0.05f, 0.45f, 1f, 1f);
    public static readonly Color InactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
    public static readonly Color DisabledColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
    // 활성 버튼은 테두리도 흰색 대신 밝은 하늘색으로 그려서 배경색만으로는 부족할 때도 한눈에 띄게 한다.
    public static readonly Color ActiveBorderColor = new Color(0.65f, 0.9f, 1f, 1f);
    public static readonly Color CaptionColor = new Color(0.78f, 0.78f, 0.78f, 1f);
    public static readonly Color WarningColor = new Color(1f, 0.72f, 0.35f, 1f);

    private const string Ellipsis = "…";
    private static readonly GUIContent _measure = new GUIContent();

    // 버튼 글자는 폭에 들어가도록 최소 크기까지 줄이고, 그래도 넘치면 …로 자른다(여러 줄은 높이도 맞춘다). 스타일은 Clip이라 버튼 밖으로 나가지 않는다.
    // 배경 없는(투명) 스타일인 이유는 DrawFlatButton이 배경/테두리를 직접 그리고 GUI.Button은 클릭 판정+텍스트 렌더링에만 쓰기 때문
    // (안 그러면 Unity 기본 스킨이 순색 배경 위에 겹쳐져 탁해짐). 캐시 키는 값 타입 튜플로 문자열 할당을 피하고,
    // 진행률처럼 계속 바뀌는 라벨이 무한히 쌓이지 않게 상한을 넘으면 비운다.
    private readonly struct FittedButton
    {
        public readonly GUIStyle Style;
        public readonly string Text;

        public FittedButton(GUIStyle style, string text)
        {
            Style = style;
            Text = text;
        }
    }

    private static readonly Dictionary<(string label, int width, int height, int maxFontSize, int minFontSize, int padding), FittedButton> _fittedButtonCache = new();
    private const int FittedButtonCacheLimit = 512;
    private const float FittedButtonHorizontalPadding = 16f;

    private static FittedButton GetFittedButton(string label, Rect rect, int maxFontSize, int minFontSize, float horizontalPadding)
    {
        label ??= string.Empty;
        bool multiLine = label.IndexOf('\n') >= 0;
        var key = (label, Mathf.RoundToInt(rect.width), multiLine ? Mathf.RoundToInt(rect.height) : 0, maxFontSize, minFontSize, Mathf.RoundToInt(horizontalPadding));
        if (_fittedButtonCache.TryGetValue(key, out var cached)) return cached;
        if (_fittedButtonCache.Count >= FittedButtonCacheLimit) _fittedButtonCache.Clear();

        var style = new GUIStyle(GUIStyle.none)
        {
            fontSize = maxFontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            richText = true,
            clipping = TextClipping.Clip,
        };
        style.normal.textColor = Color.white;
        style.hover.textColor = Color.white;
        style.active.textColor = Color.white;
        style.focused.textColor = Color.white;

        float maxWidth = rect.width - horizontalPadding;
        float maxHeight = rect.height - 4f;
        _measure.text = label;
        int fontSize = maxFontSize;
        while (fontSize > minFontSize
            && (style.CalcSize(_measure).x > maxWidth || (multiLine && style.CalcHeight(_measure, rect.width) > maxHeight)))
        {
            fontSize--;
            style.fontSize = fontSize;
        }

        // 서식 태그가 든 라벨은 중간에서 자르면 태그가 깨지므로 Clip에 맡긴다.
        string shown = label;
        if (label.IndexOf('<') < 0 && style.CalcSize(_measure).x > maxWidth)
            shown = EllipsizeLines(label, style, maxWidth);

        var fitted = new FittedButton(style, shown);
        _fittedButtonCache[key] = fitted;
        return fitted;
    }

    // 폭에 들어가지 않으면 뒤를 잘라 …를 붙인다. 글자 폭 측정이 들어 있어 Repaint에서만 부른다.
    public static string Ellipsize(string text, GUIStyle style, float maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (maxWidth <= 0f) return string.Empty;

        _measure.text = text;
        if (style.CalcSize(_measure).x <= maxWidth) return text;

        int lo = 0;
        int hi = text.Length - 1; // 앞에서 lo글자를 남기고 …를 붙였을 때 들어가는 가장 큰 lo
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            _measure.text = text.Substring(0, mid) + Ellipsis;
            if (style.CalcSize(_measure).x <= maxWidth) lo = mid;
            else hi = mid - 1;
        }
        return lo <= 0 ? Ellipsis : text.Substring(0, lo) + Ellipsis;
    }

    private static string EllipsizeLines(string text, GUIStyle style, float maxWidth)
    {
        if (text.IndexOf('\n') < 0) return Ellipsize(text, style, maxWidth);
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++) lines[i] = Ellipsize(lines[i], style, maxWidth);
        return string.Join("\n", lines);
    }

    // 하단 메뉴 바/서브메뉴/정보 탭이 공유하는 버튼 하나(배경 채우기 + 테두리 + 글자 자동 축소 텍스트)를
    // 그리고 클릭 여부를 돌려준다(위 ActiveColor 주석 참고 — 순색 채우기로 선택 상태를 또렷하게 함).
    // maxFontSize/minFontSize/horizontalPadding는 월드 배율로 크기가 바뀌는 버튼(배치모드 방 패널)용 — 기본값이면 기존 버튼과 같다.
    public static bool DrawFlatButton(Rect rect, string label, bool active, bool interactable = true,
        int maxFontSize = ButtonFontSize, int minFontSize = 11, float horizontalPadding = FittedButtonHorizontalPadding)
    {
        Color fill = !interactable ? DisabledColor : (active ? ActiveColor : InactiveColor);
        Color prevColor = GUI.color;
        GUI.color = fill;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prevColor;

        DrawButtonBorder(rect, active ? ActiveBorderColor : ButtonBorderColor);

        FittedButton fitted = GetFittedButton(label, rect, maxFontSize, minFontSize, horizontalPadding);
        bool wasEnabled = GUI.enabled;
        GUI.enabled = interactable;
        bool clicked = GUI.Button(rect, fitted.Text, fitted.Style);
        GUI.enabled = wasEnabled;
        return clicked;
    }

    private static readonly GUILayoutOption[] DefaultLayoutButtonOptions = { GUILayout.Height(24f) };

    // GUILayout 흐름 안에서 DrawFlatButton을 쓰기 위한 래퍼 — GUILayoutUtility.GetRect로 흐름상의 Rect만 받아와
    // 그 위에 기존 DrawFlatButton을 그대로 그린다. 글자 길이로 최소 폭을 요청하면 긴 라벨이 스크롤 영역 자체를
    // 가로로 넓혀 글자가 화면 밖으로 밀리므로, 폭은 배치가 정하고 글자가 그 안에 맞춘다.
    public static bool DrawFlatButtonLayout(string label, bool active = false, bool interactable = true, params GUILayoutOption[] options)
    {
        if (options == null || options.Length == 0) options = DefaultLayoutButtonOptions;
        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, options);
        return DrawFlatButton(rect, label, active, interactable);
    }

    // 버튼이 아닌 상시 표시 텍스트(배속 안내, 자원 보유량 등)도 같은 스타일 언어(굵은 큰 글씨 + 어두운
    // 배경 박스 + 흰 테두리)를 쓰도록 확장.
    public const int LabelFontSize = 20;
    public static readonly Color PanelBackgroundColor = new Color(0.04f, 0.04f, 0.06f, 0.85f);

    private static GUIStyle _labelStyle;

    public static GUIStyle LabelStyle
    {
        get
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = LabelFontSize, fontStyle = FontStyle.Bold, richText = true };
                _labelStyle.normal.textColor = Color.white;
            }
            return _labelStyle;
        }
    }

    // 정보량이 많은 패널은 LabelStyle(20px)이 너무 커서 줄바꿈/잘림이 심해지므로, 같은 색·굵기는
    // 유지하고 크기만 줄이고 wordWrap을 켠 보조 스타일.
    public const int BodyLabelFontSize = 14;
    private static GUIStyle _bodyLabelStyle;

    public static GUIStyle BodyLabelStyle
    {
        get
        {
            if (_bodyLabelStyle == null)
            {
                _bodyLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = BodyLabelFontSize, fontStyle = FontStyle.Bold, richText = true, wordWrap = true };
                _bodyLabelStyle.normal.textColor = Color.white;
            }
            return _bodyLabelStyle;
        }
    }

    private static GUIStyle MakeCompactLabel(int fontSize, FontStyle fontStyle, Color color, bool wordWrap, TextAnchor anchor, bool richText = true)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = fontStyle,
            richText = richText,
            wordWrap = wordWrap,
            alignment = anchor,
            margin = new RectOffset(0, 0, 0, 1),
            padding = new RectOffset(2, 2, 0, 1),
        };
        style.normal.textColor = color;
        return style;
    }

    // 여백을 줄인 패널용 스타일 — 본문(줄바꿈)·보조 설명·경고, 그리고 한 줄 고정 칸용(넘치면 DrawClippedLabel이 …로 줄인다).
    private static GUIStyle _compactBodyLabelStyle, _captionLabelStyle, _warningLabelStyle, _lineLabelStyle, _headerLabelStyle;
    public static GUIStyle CompactBodyLabelStyle => _compactBodyLabelStyle ??= MakeCompactLabel(BodyLabelFontSize, FontStyle.Bold, Color.white, true, TextAnchor.UpperLeft);
    public static GUIStyle CaptionLabelStyle => _captionLabelStyle ??= MakeCompactLabel(12, FontStyle.Bold, CaptionColor, true, TextAnchor.UpperLeft);
    public static GUIStyle WarningLabelStyle => _warningLabelStyle ??= MakeCompactLabel(13, FontStyle.Bold, WarningColor, true, TextAnchor.UpperLeft);
    public static GUIStyle LineLabelStyle => _lineLabelStyle ??= MakeCompactLabel(BodyLabelFontSize, FontStyle.Bold, Color.white, false, TextAnchor.MiddleLeft, richText: false);
    public static GUIStyle HeaderLabelStyle => _headerLabelStyle ??= MakeCompactLabel(16, FontStyle.Bold, Color.white, false, TextAnchor.MiddleLeft, richText: false);

    // 고정 칸 안의 한 줄 글자 — 넘치면 …로 줄인다. 글자 폭 측정은 그릴 때(Repaint)만 하고, 호출 순서는 이벤트와 무관하게 같다.
    // richColor(예 "#c8c8c8")를 주면 줄인 뒤에 색 태그로 감싼다 - 태그를 먼저 붙이면 자르다가 깨진다(richText 스타일에서만 쓴다).
    public static void DrawClippedLabel(Rect rect, string text, GUIStyle style, string richColor = null)
    {
        bool repaint = Event.current.type == EventType.Repaint;
        string shown = repaint ? Ellipsize(text, style, rect.width - style.padding.horizontal) : text;
        if (richColor != null) shown = string.Concat("<color=", richColor, ">", shown, "</color>");
        GUI.Label(rect, shown, style);
    }

    // ── 목록 한 줄(두 줄 높이) — 왼쪽 제목 + 오른쪽 보조 글자 + 아래 설명 한 줄. 모든 글자는 칸 안에서 …로 줄어든다 ──
    public const float ListRowHeight = 36f;
    private const float ListRowPadX = 8f;
    private static GUIStyle _rowTitleStyle, _rowRightStyle, _rowDetailStyle, _rowWarnStyle;
    private static GUIStyle RowTitleStyle => _rowTitleStyle ??= MakeCompactLabel(13, FontStyle.Bold, Color.white, false, TextAnchor.UpperLeft, richText: false);
    private static GUIStyle RowRightStyle => _rowRightStyle ??= MakeCompactLabel(12, FontStyle.Bold, CaptionColor, false, TextAnchor.UpperRight, richText: false);
    private static GUIStyle RowDetailStyle => _rowDetailStyle ??= MakeCompactLabel(12, FontStyle.Bold, CaptionColor, false, TextAnchor.UpperLeft, richText: false);
    private static GUIStyle RowWarnStyle => _rowWarnStyle ??= MakeCompactLabel(12, FontStyle.Bold, WarningColor, false, TextAnchor.UpperLeft, richText: false);

    public static bool DrawListRow(Rect rect, string left, string right, string detail, bool active, bool interactable = true, bool warnDetail = false)
    {
        bool clicked = DrawFlatButton(rect, string.Empty, active, interactable);

        bool repaint = Event.current.type == EventType.Repaint; // 글자 폭 측정은 그릴 때만 한다(라벨 호출 순서는 이벤트와 무관하게 같다)
        float innerWidth = rect.width - ListRowPadX * 2f;
        GUIStyle titleStyle = RowTitleStyle;
        GUIStyle rightStyle = RowRightStyle;
        GUIStyle detailStyle = warnDetail ? RowWarnStyle : RowDetailStyle;

        string leftShown = left;
        float leftWidth = 0f;
        if (repaint)
        {
            leftShown = Ellipsize(left, titleStyle, innerWidth - titleStyle.padding.horizontal);
            if (!string.IsNullOrEmpty(leftShown))
            {
                _measure.text = leftShown;
                leftWidth = titleStyle.CalcSize(_measure).x;
            }
        }

        var titleRect = new Rect(rect.x + ListRowPadX, rect.y + 2f, innerWidth, 17f);
        GUI.Label(titleRect, leftShown, titleStyle);

        float rightMax = innerWidth - leftWidth - 10f;
        bool showRight = !repaint || (!string.IsNullOrEmpty(right) && rightMax > 24f);
        GUI.Label(titleRect, showRight ? (repaint ? Ellipsize(right, rightStyle, rightMax - rightStyle.padding.horizontal) : right) : string.Empty, rightStyle);

        var detailRect = new Rect(rect.x + ListRowPadX, rect.y + 19f, innerWidth, 15f);
        GUI.Label(detailRect, repaint ? Ellipsize(detail, detailStyle, innerWidth - detailStyle.padding.horizontal) : detail, detailStyle);
        return clicked;
    }

    // 패널 높이를 내용에 맞출 때 쓴다 — 내용 높이를 Repaint에서 재 두고 다음 프레임부터 패널 높이로 쓴다(한 프레임 안에서 Layout과 Repaint의 Rect가 달라지지 않게).
    // GUILayout.EndVertical() 바로 뒤에서 부른다.
    public static void MeasureContent(ref float contentHeight)
    {
        Rect last = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.Repaint) contentHeight = last.height;
    }

    public static void DrawButtonBorder(Rect r) => DrawButtonBorder(r, ButtonBorderColor);

    public static void DrawButtonBorder(Rect r, Color color)
    {
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, ButtonBorderThickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - ButtonBorderThickness, r.width, ButtonBorderThickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, ButtonBorderThickness, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - ButtonBorderThickness, r.y, ButtonBorderThickness, r.height), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    // 어두운 배경 채우기 + 흰 테두리 — 배속/자원 표시처럼 버튼이 아닌 상시 정보 패널의 바탕으로 쓴다.
    public static void DrawPanelBox(Rect r)
    {
        Color prevColor = GUI.color;
        GUI.color = PanelBackgroundColor;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = prevColor;
        DrawButtonBorder(r);
    }
}
