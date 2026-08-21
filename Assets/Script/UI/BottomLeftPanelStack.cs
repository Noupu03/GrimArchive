using System.Collections.Generic;
using UnityEngine;

// 좌하단 패널 스택(2026-08-21, 사용자 요청 "유닛 정보 UI와 같은 방식으로 위치하게 해주고, 스택형태로
// 좌우로 쌓이게 해줘" → "새 창이 왼쪽으로 오는 형태가 아니라 오른쪽으로 늘어나는 형태로" → "패널이
// 사라지면 등록에서 빠지고... 이 부분이 잘 안됐어. 안 당겨져서 계속 빈틈이 생겨") — 패널 정체성이
// 아니라 "실제로 보이기 시작한 순서"로 왼쪽부터 자리를 배정한다. 이미 떠 있는 패널은 다른 패널이
// 새로 나타나도 절대 움직이지 않고, 새로 보이기 시작한 패널은 항상 지금 떠 있는 것들의 맨 오른쪽
// (스택의 끝)에 추가된다.
//
// 빈틈 버그의 원인 — BuildingControlPanel.OnGUI()/IsMouseOverPanel()은 `if (_current == null) return;`
// 로 먼저 걸러진 뒤에야 GetPanelRect()(=Report 호출부)를 부른다. 즉 패널이 "안 보이게 된" 바로 그
// 프레임부터 Report(visible:false, ...) 호출 자체가 아예 실행되지 않아서(가드가 그 전에 먼저 끝내
// 버림), 등록이 영원히 안 지워졌다. MonsterPlacementController도 같은 구조(placement 모드를 완전히
// 나가면 DrawGUI 자체가 안 불림). 매 소비자가 "나 이제 안 보여"를 스스로 보고할 거라고 믿는 대신,
// 프레임 단위로 "이번 프레임에 아무도 갱신 안 한 항목은 사라진 것으로 간주"하는 스윕을 안전망으로
// 둔다 — 소비자가 보고를 놓치는 경로가 있어도 최대 1프레임 지연으로 자동 정리된다.
public static class BottomLeftPanelStack
{
    public const float Margin = 10f;
    public const float Gap = 10f;

    // 등록 순서 = 화면상 왼쪽→오른쪽 순서. 폭/마지막 보고 프레임은 매 Report 호출마다 갱신.
    private static readonly List<string> _order = new List<string>();
    private static readonly Dictionary<string, float> _widths = new Dictionary<string, float>();
    private static readonly Dictionary<string, int> _lastSeenFrame = new Dictionary<string, int>();
    private static int _sweptFrame = -1;

    // 패널 하나가 매 프레임 자기 상태를 보고하고 자기 시작 X를 돌려받는 단일 진입점.
    public static float Report(string panelId, bool visible, float width)
    {
        SweepStaleEntriesOncePerFrame();

        if (!visible)
        {
            // 스스로 "안 보인다"를 보고할 수 있는 소비자는 즉시 반영(스윕의 1프레임 지연보다 빠르게).
            RemoveEntry(panelId);
            return Margin;
        }

        if (!_order.Contains(panelId)) _order.Add(panelId);
        _widths[panelId] = width;
        _lastSeenFrame[panelId] = Time.frameCount;

        float x = Margin;
        foreach (var id in _order)
        {
            if (id == panelId) break;
            if (_widths.TryGetValue(id, out float w)) x += w + Gap;
        }
        return x;
    }

    private static void RemoveEntry(string panelId)
    {
        _order.Remove(panelId);
        _widths.Remove(panelId);
        _lastSeenFrame.Remove(panelId);
    }

    // 매 프레임 한 번만 실행 — "지난 프레임까지는 갱신됐는데 이번 프레임 시점까지 아직 아무도 다시
    // Report(visible:true)로 갱신하지 않은" 항목만 지운다(정확히 이번 프레임 값 하나로 판단하면, 이
    // 스윕이 그 프레임의 다른 패널이 아직 Report를 부르기도 전에 먼저 실행될 수 있어 아직 살아있는
    // 패널을 오판해 지울 수 있다 — 그래서 `frame - 1` 기준으로 한 프레임 여유를 둔다).
    private static void SweepStaleEntriesOncePerFrame()
    {
        int frame = Time.frameCount;
        if (_sweptFrame == frame) return;
        _sweptFrame = frame;

        for (int i = _order.Count - 1; i >= 0; i--)
        {
            string id = _order[i];
            if (!_lastSeenFrame.TryGetValue(id, out int lastFrame) || lastFrame < frame - 1)
                RemoveEntry(id);
        }
    }

    // Y는 스택의 모든 패널이 공유 — 하단 메뉴 바(+열려 있으면 서브메뉴)가 차지한 높이 위에 밀착한다.
    public static float GetReservedBottomHeight()
        => BottomMenuBar.Instance != null ? BottomMenuBar.Instance.GetReservedBottomLeftHeight() : 0f;
}
