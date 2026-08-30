using UnityEngine;
using UnityEngine.EventSystems;

// 마우스 스크린 좌표(Y=0이 아래)를 OnGUI 좌표(Y=0이 위)로 뒤집어 Rect.Contains로 확인하는 공용
// 헬퍼(여러 곳의 중복 구현 통합) — 포인터 좌표는 GameInputScheme(입력 스킴 중앙화)을 통해 읽는다.
public static class GUIMouseUtil
{
    public static bool IsMouseOverRect(Rect rect)
    {
        if (!GameInputScheme.PointerAvailable) return false;
        Vector2 screenPos = GameInputScheme.PointerScreenPos;
        Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
        return rect.Contains(guiPos);
    }

    // 배치 모드 컨트롤러(BuildPlacementController/ObjectPlacementController)와 InputManager의 드래그
    // 선택이 반복하던 3항 판정을 통합했다. InputManager는 BuildingControlPanel 확인이 하나 더 필요해서
    // (OnGUI라 IsPointerOverGameObject로 안 잡힘) 이 공통부에 자기 조건을 추가로 ||한다.
    public static bool IsPointerOverAnyPanel()
    {
        return (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            || (BottomMenuBar.Instance != null && BottomMenuBar.Instance.IsMouseOverUI())
            || (DebugInfoPanel.Instance != null && DebugInfoPanel.Instance.IsMouseOverUI());
    }
}
