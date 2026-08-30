using UnityEngine;

// 층 변경 상시 패널을 다른 UI보다 뒤로 깔기 위한 전용 컴포넌트 — OnGUI는 페인터 알고리즘(나중에
// 그려지는 쪽이 위)이라, 이 컴포넌트만 낮은 실행 순서로 고정해 다른 UI(대부분 기본값 0)보다 먼저
// 그려서 뒤로 깔리게 한다. BottomMenuBar 본체가 이 순서 영향을 받지 않도록 층 패널 그리기
// (DrawFloorPanel)만 분리해 BottomMenuBar.Constructor()가 런타임에 같은 GameObject에 붙인다.
[DefaultExecutionOrder(-1000)]
public class FloorPanelOverlay : MonoBehaviour
{
    private void OnGUI()
    {
        BottomMenuBar.Instance?.DrawFloorPanel();
    }
}
