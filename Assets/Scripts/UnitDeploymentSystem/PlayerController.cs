using UnityEngine;
using System.Linq;
namespace UnitDeploymentSystem
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private UnitSelectionManager selectionManager;
        [SerializeField] private Camera mainCamera;

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (selectionManager == null) selectionManager = FindObjectOfType<UnitSelectionManager>();
        }

        private void Update()
        {
            // 좌클릭: 유닛 선택 (Shift 클릭 등 복수 선택은 단순화를 위해 토글 방식으로 구현)
            if (Input.GetMouseButtonDown(0))
            {
                HandleSelection();
            }

            // 우클릭: 선택한 유닛들을 대상 방으로 이동 명령
            if (Input.GetMouseButtonDown(1))
            {
                HandleMovementCommand();
            }
        }

        private void HandleSelection()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // 클릭한 대상이 유닛인지 확인
                Unit clickedUnit = hit.collider.GetComponent<Unit>();
                if (clickedUnit != null)
                {
                    // 단순화를 위해 선택 추가 (만약 이미 있다면 해제, 실무에서는 Shift 키 조합 등을 사용)
                    if (selectionManager.SelectedUnits.Contains(clickedUnit))
                    {
                        selectionManager.DeselectUnit(clickedUnit);
                        Debug.Log($"{clickedUnit.name} 선택 해제됨");
                    }
                    else
                    {
                        selectionManager.SelectUnit(clickedUnit);
                        Debug.Log($"{clickedUnit.name} 선택됨");
                    }
                }
                else
                {
                    // 빈 공간 클릭 시 선택 초기화
                    selectionManager.ClearSelection();
                    Debug.Log("모든 선택 초기화됨");
                }
            }
        }

        private void HandleMovementCommand()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // 클릭한 대상이 방(Room)인지 확인
                Room targetRoom = hit.collider.GetComponent<Room>();
                if (targetRoom != null)
                {
                    Debug.Log($"{targetRoom.name}으로 이동 명령 하달!");
                    // 관리자에게 다중 이동 명령을 전달 (Native Routine 비동기 실행)
                    selectionManager.CommandMoveToRoomAsync(targetRoom).Forget();
                }
            }
        }
    }
}
