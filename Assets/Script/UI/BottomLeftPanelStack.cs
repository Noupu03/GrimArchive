using System.Collections.Generic;
using UnityEngine;

// 좌하단 패널 스택 — 패널 정체성이 아니라 "실제로 보이기 시작한 순서"로 왼쪽부터 자리를 배정한다.
// 이미 떠 있는 패널은 움직이지 않고 새 패널은 항상 스택 끝(맨 오른쪽)에 추가된다. 일부 소비자
// (BuildingControlPanel 등)는 "안 보이게 된" 프레임에 Report(visible:false)를 못 부를 수 있어,
// 소비자 보고에만 의존하지 않고 "이번 프레임에 아무도 갱신 안 한 항목은 사라진 것"으로 간주하는
// 스윕을 안전망으로 둔다(최대 1프레임 지연 자동 정리).
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

    // 매 프레임 한 번만 실행. `frame - 1` 기준으로 판단하는 이유: 정확히 이번 프레임 값으로 판단하면
    // 같은 프레임의 다른 패널이 아직 Report를 부르기 전에 스윕이 먼저 실행돼 오판할 수 있다.
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
