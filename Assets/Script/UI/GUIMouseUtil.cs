using UnityEngine;

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
}
