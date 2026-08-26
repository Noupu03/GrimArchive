using UnityEngine;
using UnityEngine.EventSystems;

// 마우스 스크린 좌표(Y=0이 아래)를 OnGUI 좌표(Y=0이 위)로 뒤집어 Rect.Contains로 확인하는 공용
// 헬퍼(2026-08-20) — BuildingControlPanel과 MonsterPlacementController가 각자 동일한 로직을
// 중복 구현하고 있었다. 포인터 좌표는 GameInputScheme(2026-08-21, 입력 스킴 중앙화)을 통해 읽는다.
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
    // 선택이 각자 동일한 3항 판정을 반복하던 것을 통합(2026-08-25 리팩토링). InputManager는
    // BuildingControlPanel 확인이 하나 더 필요해서(OnGUI라 IsPointerOverGameObject로 안 잡힘) 이
    // 공통부에 자기 조건을 추가로 ||한다 — 여기서는 항상 확인해야 하는 3개만 다룬다.
    public static bool IsPointerOverAnyPanel()
    {
        return (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            || (BottomMenuBar.Instance != null && BottomMenuBar.Instance.IsMouseOverUI())
            || (DebugInfoPanel.Instance != null && DebugInfoPanel.Instance.IsMouseOverUI());
    }
}
