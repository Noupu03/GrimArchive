using UnityEngine;
using UnityEngine.InputSystem;

// 마우스 스크린 좌표(Y=0이 아래)를 OnGUI 좌표(Y=0이 위)로 뒤집어 Rect.Contains로 확인하는 공용
// 헬퍼(2026-08-20) — BuildingControlPanel과 MonsterPlacementController가 각자 동일한 로직을
// 중복 구현하고 있었다.
public static class GUIMouseUtil
{
    public static bool IsMouseOverRect(Rect rect)
    {
        if (Mouse.current == null) return false;
        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
        return rect.Contains(guiPos);
    }
}
