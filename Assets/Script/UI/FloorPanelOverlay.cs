using UnityEngine;

// 층 변경 상시 패널을 다른 UI(유닛 상세정보/메뉴 정보 등)보다 뒤로 깔기 위한 전용 컴포넌트.
// OnGUI는 페인터 알고리즘이라 겹치는 자리에서는 "나중에 그려지는 쪽"이 이긴다. Unity는 OnGUI를
// 스크립트 실행 순서(낮을수록 먼저)대로 호출하므로, 이 컴포넌트를 충분히 낮은 실행 순서로 고정해두면
// 항상 다른 UI(대부분 기본 실행 순서 0)보다 먼저 그려져 — 즉 겹치는 자리에서는 뒤로 깔린다.
//
// BottomMenuBar 자신(하단바+서브메뉴)은 이 순서 영향을 받으면 안 되므로, 층 패널 그리기
// (DrawFloorPanel)만 이 별도 컴포넌트로 분리했다 — 새 프리팹/GUID 발급 없이 BottomMenuBar.Constructor()가
// 런타임에 같은 GameObject에 붙인다.
[DefaultExecutionOrder(-1000)]
public class FloorPanelOverlay : MonoBehaviour
{
    private void OnGUI()
    {
        BottomMenuBar.Instance?.DrawFloorPanel();
    }
}
