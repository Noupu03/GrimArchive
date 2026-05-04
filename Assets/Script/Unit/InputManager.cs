using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;
    public Unit selectedUnit;

    void Awake()
    {
        Instance = this;
    }

    private bool IsPointInFootprint(Vector3Int pos, Unit u)
    {
        int w = (int)u.unitType.footprint.x;
        int h = (int)u.unitType.footprint.y;
        return (pos.x >= u.position.x && pos.x < u.position.x + w &&
                pos.y >= u.position.y && pos.y < u.position.y + h);
    }

    private bool IsValidTile(Vector2Int pos, int floorIdx)
    {
        if (GameSession.Instance == null || GameSession.Instance.cmap == null) return false;
        var cmap = GameSession.Instance.cmap;
        if (cmap.map.floors == null || floorIdx < 0 || floorIdx >= cmap.map.floors.Length) return false;
        
        var floor = cmap.map.floors[floorIdx];
        if (floor.chunks == null) return false;

        if (pos.x < 0 || pos.y < 0) return false;

        int cx = pos.x / 8;
        int cy = pos.y / 8;
        int tx = pos.x % 8;
        int ty = pos.y % 8;

        if (cx >= floor.config.width || cy >= floor.config.height) return false;
        
        var c = floor.chunks[cx, cy];
        if (c.roomId == -1 || c.chunk == null) return false; // 방이 없으면 유효하지 않음
        if (c.chunk[tx, ty].name == "Wall") return false; // 벽이면 불가

        return true;
    }

    void Update()
    {
        if (GameSession.Instance == null) return;
        if (Keyboard.current == null || Mouse.current == null) return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            GameSession.Instance.isPaused = !GameSession.Instance.isPaused;
            Time.timeScale = GameSession.Instance.isPaused ? 0.0001f : GameSession.Instance.currentGameSpeed; // TimeScale=0 일 때 InputSystem Update가 멈추는 현상 우회
        }
        if (Keyboard.current.digit1Key.wasPressedThisFrame) { GameSession.Instance.currentGameSpeed = 1f; if (!GameSession.Instance.isPaused) Time.timeScale = 1f; }
        if (Keyboard.current.digit2Key.wasPressedThisFrame) { GameSession.Instance.currentGameSpeed = 2f; if (!GameSession.Instance.isPaused) Time.timeScale = 2f; }
        if (Keyboard.current.digit3Key.wasPressedThisFrame) { GameSession.Instance.currentGameSpeed = 3f; if (!GameSession.Instance.isPaused) Time.timeScale = 3f; }

        int currentFloor = 1; // 1층 고정 임시
        Vector3 floorOffset = UnitGenerate.Instance != null ? UnitGenerate.Instance.GetFloorOffset_Public(currentFloor) : Vector3.zero;

        if (Mouse.current.leftButton.wasPressedThisFrame) // 좌클릭 (선택 또는 공격 명령)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector3 worldPoint = Camera.main.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, Mathf.Abs(Camera.main.transform.position.z)));
            Vector3 localPoint = worldPoint - floorOffset;
            Vector3Int gridPos = new Vector3Int(Mathf.FloorToInt(localPoint.x), Mathf.FloorToInt(localPoint.y), currentFloor);

			/*=======아티팩트 관련 참조 주석처리========
            if (ArtifactManager.Instance != null && ArtifactManager.Instance.artifactPlacementMode)
            {
                if (IsValidTile(new Vector2Int(gridPos.x, gridPos.y), currentFloor))
                {
                    ArtifactManager.Instance.SpawnArtifact(new Vector2Int(gridPos.x, gridPos.y), currentFloor);
                }
                else
                {
                    Debug.LogWarning("이곳에는 유물을 생성할 수 없습니다 (타일맵 범위 밖이거나 벽).");
                }
                return;
            }*/

			bool clickedEnemy = false;

            if (selectedUnit != null && selectedUnit.hp > 0)
            {
                foreach (var u in GameSession.Instance.units)
                {
                    if (u == selectedUnit || u.hp <= 0) continue;
                    if (u.currentFloor != currentFloor) continue;
                    if (IsPointInFootprint(gridPos, u))
                    {
                        if ((selectedUnit is Monster && u is Human) || (selectedUnit is Human && u is Monster))
                        {
                            selectedUnit.playerAttackTarget = u;
                            selectedUnit.playerMoveTarget = null;
                            Debug.Log($"InputManager: {selectedUnit.unitType.typeName}에게 {u.unitType.typeName} 공격 명령 (좌클릭)");
                            clickedEnemy = true;
                            break;
                        }
                    }
                }
            }

            if (!clickedEnemy)
            {
                selectedUnit = null; // 허공 클릭 시 해제
                foreach (var u in GameSession.Instance.units)
                {
                    if (u == null || u.hp <= 0) continue;
                    if (u.currentFloor != currentFloor) continue;
                    if (IsPointInFootprint(gridPos, u))
                    {
                        selectedUnit = u;
                        Debug.Log($"InputManager: {u.unitType.typeName}을(를) 선택했습니다.");
                        break;
                    }
                }
            }
        }

        if (Mouse.current.rightButton.wasPressedThisFrame && selectedUnit != null && selectedUnit.hp > 0) // 우클릭 (새 이동)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector3 worldPoint = Camera.main.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, Mathf.Abs(Camera.main.transform.position.z)));
            Vector3 localPoint = worldPoint - floorOffset;
            Vector3Int gridPos = new Vector3Int(Mathf.FloorToInt(localPoint.x), Mathf.FloorToInt(localPoint.y), currentFloor);

            selectedUnit.playerMoveTarget = new Vector2Int(gridPos.x, gridPos.y);
            selectedUnit.playerAttackTarget = null; // 이동 명령 시 기존 공격 타겟 취소
            Debug.Log($"InputManager: {selectedUnit.unitType.typeName}에게 ({gridPos.x}, {gridPos.y}) 이동 명령");
        }
    }
}
