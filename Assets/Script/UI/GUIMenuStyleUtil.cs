using System.Collections.Generic;
using UnityEngine;

// 하단 메뉴 바(BottomMenuBar)와 정보 UI(DebugInfoPanel) 버튼들이 공유하는 OnGUI 스타일(2026-08-20,
// 사용자 요청 "정보 UI도 메뉴와 동일한 스타일로") — 큰 굵은 글씨 + 흰 테두리. 두 클래스가 각자
// 똑같은 스타일 상수/테두리 그리기 코드를 따로 들고 있던 걸 여기 한 곳으로 모았다(GUIMouseUtil/
// GUISpriteUtil과 동일한 이 프로젝트의 OnGUI 공용 유틸 관례).
public static class GUIMenuStyleUtil
{
    public const float ButtonBorderThickness = 2f;
    public static readonly Color ButtonBorderColor = Color.white;
    public const int ButtonFontSize = 18;

    // 선택/활성 상태 배경색. 사용자가 두 차례 "여전히 잘 안 보인다"고 신고한 원인은 숫자 자체가 아니라
    // 그리는 방식이었다 — 예전엔 GUI.backgroundColor로 Unity 기본 버튼 스킨 텍스처(입체감을 위해 이미
    // 회색조 음영이 들어간 그라데이션)를 곱색(tint)했는데, 곱색은 밑바탕이 순백이 아닌 이상 아무리 값을
    // 올려도 탁하게 보인다. DrawFlatButton은 스킨 텍스처 대신 Texture2D.whiteTexture 위에 이 색을 그대로
    // 칠해서(곱색 아님, 채우기) 값 그대로의 순색이 나오게 한다.
    public static readonly Color ActiveColor = new Color(0.05f, 0.45f, 1f, 1f);
    public static readonly Color InactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
    public static readonly Color DisabledColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
    // 활성 버튼은 테두리도 흰색 대신 밝은 하늘색으로 그려서 배경색만으로는 부족할 때도 한눈에 띄게 한다.
    public static readonly Color ActiveBorderColor = new Color(0.65f, 0.9f, 1f, 1f);

    // 2026-08-20, 사용자 요청("디버그 메뉴 버튼들 화면 넘침... 상세정보의 기본정보/세부스탯/장비 등의
    // 글씨도 너무 커서 넘치니 줄여줘. 혹은 모든 버튼에 텍스트 넣을때 글자 크기 스케일링을 적용하던가")
    // — 버튼마다 고정 폰트 크기 대신, 그 버튼의 실제 폭에 라벨이 들어가는지 CalcSize로 재보고 넘치면
    // 최소 크기까지 한 단계씩 줄인 스타일을 쓴다. 배경이 없는(투명) 스타일인 이유는 DrawFlatButton이
    // 배경/테두리를 직접 그리고 GUI.Button은 클릭 판정 + 텍스트 렌더링에만 쓰기 때문 — 그러지 않으면
    // Unity 기본 버튼 스킨이 우리가 그린 순색 배경 위에 다시 겹쳐 그려져 탁해진다. 같은 (라벨, 폭)
    // 조합은 캐시해서 매 프레임 GUIStyle을 새로 만들지 않는다.
    // 2026-08-25 프레임 드랍 대응 — 스타일 자체는 캐시돼 있었지만, 캐시 키를 문자열 보간으로 매 버튼
    // 그리기·매 OnGUI 패스마다(캐시 히트 여부와 무관하게) 새로 만들고 있었다. 값 타입 튜플 키로
    // 바꿔 캐싱 동작(같은 조합 → 같은 스타일)은 그대로 두고 문자열 할당만 없앤다.
    private static readonly Dictionary<(string label, int width, int fontSize), GUIStyle> _fittedTransparentStyleCache = new();
    private const float FittedButtonHorizontalPadding = 16f;

    private static GUIStyle GetFittedTransparentButtonStyle(string label, float maxWidth, int maxFontSize = ButtonFontSize, int minFontSize = 11)
    {
        var key = (label, Mathf.RoundToInt(maxWidth), maxFontSize);
        if (_fittedTransparentStyleCache.TryGetValue(key, out var cached)) return cached;

        var style = new GUIStyle(GUIStyle.none)
        {
            fontSize = maxFontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            richText = true,
        };
        style.normal.textColor = Color.white;
        style.hover.textColor = Color.white;
        style.active.textColor = Color.white;
        style.focused.textColor = Color.white;

        int fontSize = maxFontSize;
        while (fontSize > minFontSize && style.CalcSize(new GUIContent(label)).x > maxWidth - FittedButtonHorizontalPadding)
        {
            fontSize--;
            style.fontSize = fontSize;
        }

        _fittedTransparentStyleCache[key] = style;
        return style;
    }

    // 하단 메뉴 바/서브메뉴/정보 탭이 공유하는 버튼 하나(배경 채우기 + 테두리 + 글자 자동 축소 텍스트)를
    // 그리고 클릭 여부를 돌려준다 — 위 ActiveColor 주석 참고, 배경을 스킨 텍스처 곱색이 아니라 순색
    // 채우기로 그려서 선택 상태가 또렷하게 보이게 하는 게 핵심.
    public static bool DrawFlatButton(Rect rect, string label, bool active, bool interactable = true)
    {
        Color fill = !interactable ? DisabledColor : (active ? ActiveColor : InactiveColor);
        Color prevColor = GUI.color;
        GUI.color = fill;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prevColor;

        DrawButtonBorder(rect, active ? ActiveBorderColor : ButtonBorderColor);

        bool wasEnabled = GUI.enabled;
        GUI.enabled = interactable;
        bool clicked = GUI.Button(rect, label, GetFittedTransparentButtonStyle(label, rect.width));
        GUI.enabled = wasEnabled;
        return clicked;
    }

    // GUILayout 흐름 안에서 DrawFlatButton을 쓰기 위한 래퍼(2026-08-21, 사용자 요청 "건물 선택 정보
    // UI/소집 배치 모드 UI도 다른 메뉴들과 동일한 스타일로") — BuildingControlPanel(생산 대기열)/
    // MonsterPlacementController(배치 대기열)처럼 항목 개수가 가변적인 목록은 BottomMenuBar처럼
    // 매번 손으로 Rect를 계산하기보다 GUILayout 자동 배치가 훨씬 간단해서, GUILayoutUtility.GetRect로
    // 흐름상의 Rect만 받아와 그 위에 기존 DrawFlatButton을 그대로 그린다 — 스타일은 완전히 동일하다.
    public static bool DrawFlatButtonLayout(string label, bool active = false, bool interactable = true, params GUILayoutOption[] options)
    {
        Rect rect = GUILayoutUtility.GetRect(new GUIContent(label), GUIStyle.none, options);
        return DrawFlatButton(rect, label, active, interactable);
    }

    // 2026-08-20, 사용자 요청 "좌상단의 배속, 우상단의 자원 보유량도 글자 크기 좀 키워주고, 메뉴
    // 스타일로 바꿔줘" — 버튼이 아닌 상시 표시 텍스트(UIManager 배속 안내, StatusInfoPanel 자원
    // 보유량)도 같은 스타일 언어(굵은 큰 글씨 + 어두운 배경 박스 + 흰 테두리)를 쓰도록 확장.
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

    // 정보량이 많은 패널(생산 대기열/배치 대기열 등, 2026-08-21 신규)은 LabelStyle(20px)이 너무 커서
    // 좁은 패널 안에서 줄바꿈/잘림이 심해진다 — 같은 색·굵기 언어는 유지하면서 크기만 줄인 보조
    // 스타일. wordWrap을 켜서 긴 문장이 패널 폭에 맞게 자연스럽게 줄바꿈되게 한다.
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
