using UnityEngine;

// 화면 좌표 → 월드/그리드 좌표 변환 공용 유틸(2026-08-20) — InputManager와 배치 모드 컨트롤러들
// (BuildPlacementController/ObjectPlacementController/MonsterPlacementController)이 각자 동일한
// 변환 로직(Camera.main 기준 스크린→월드 투영, 층 오프셋 보정 후 그리드 좌표 스냅)을 중복
// 구현하던 것을 통합했다.
public static class ScreenGridUtil
{
    public static Vector3 ScreenToWorldPoint(Vector2 screenPos)
    {
        return Camera.main.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, Mathf.Abs(Camera.main.transform.position.z))
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
