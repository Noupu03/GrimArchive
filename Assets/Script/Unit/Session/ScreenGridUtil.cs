using UnityEngine;

// 화면 좌표 → 월드/그리드 좌표 변환 공용 유틸(2026-08-20) — InputManager와 배치 모드 컨트롤러들
// (BuildPlacementController/ObjectPlacementController/MonsterPlacementController)이 각자 동일한
// 변환 로직(Camera.main 기준 스크린→월드 투영, 층 오프셋 보정 후 그리드 좌표 스냅)을 중복
// 구현하던 것을 통합했다.
public static class ScreenGridUtil
{
    public static Vector3 ScreenToWorldPoint(Vector2 screenPos)
    {
        // 2026-08-25 프레임 드랍 대응 — 이 유틸은 InputManager/배치 컨트롤러들/UnitGenerate.
        // RefreshSelectionVisual(선택된 유닛마다 매 프레임)에서 호출된다. Camera.main을 한 호출 안에서
        // 두 번 읽던 것을 한 번으로 줄인다(두 접근 사이 카메라가 바뀔 이론적 가능성까지 없애 더 안전).
        Camera cam = Camera.main;
        return cam.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, Mathf.Abs(cam.transform.position.z))
        );
    }

    public static Vector3Int ScreenToGridPos(Vector2 screenPos, Vector3 floorOffset, int currentFloor)
    {
        Vector3 localPoint = ScreenToWorldPoint(screenPos) - floorOffset;

        return new Vector3Int(
            Mathf.FloorToInt(localPoint.x),
            Mathf.FloorToInt(localPoint.y),
            currentFloor
        );
    }
}
