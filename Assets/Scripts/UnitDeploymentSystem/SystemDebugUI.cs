using UnityEngine;
using System.Linq;

namespace UnitDeploymentSystem
{
    public class SystemDebugUI : MonoBehaviour
    {
        private UnitSelectionManager selectionManager;
        private Room[] allRooms;

        private void Start()
        {
            selectionManager = FindObjectOfType<UnitSelectionManager>();
            allRooms = FindObjectsOfType<Room>();
        }

        private void Update()
        {
            // 주기적으로 방 목록을 갱신 (런타임에 방이 추가/삭제될 경우 대비, 최적화를 위해 60프레임마다)
            if (Time.frameCount % 60 == 0)
            {
                allRooms = FindObjectsOfType<Room>();
            }
        }

        private void OnGUI()
        {
            if (selectionManager == null) return;

            // 좌측 상단에 반투명한 박스 배경을 그려 글자가 잘 보이게 함
            GUI.Box(new Rect(10, 10, 350, 400), "시스템 검증 UI");
            
            GUILayout.BeginArea(new Rect(20, 40, 330, 380));
            
            // 1. 방 정보 (핵심 검증 요소)
            GUILayout.Label("<b>--- 방(Room) 인구수 상태 ---</b>");
            if (allRooms != null && allRooms.Length > 0)
            {
                foreach (var room in allRooms)
                {
                    // 수용력 초과 시 빨간색 표시 (정상적이라면 초과하지 않아야 함)
                    GUI.contentColor = room.CurrentPopulation > room.MaxPopulation ? Color.red : Color.green;
                    GUILayout.Label($"[{room.name}] 인구수: {room.CurrentPopulation} / {room.MaxPopulation}");
                    
                    GUI.contentColor = Color.white;
                    GUILayout.Label($"  -> 소속 유닛 개체 수: {room.ContainedUnits.Count}");
                }
            }
            else
            {
                GUILayout.Label("배치된 방이 없습니다.");
            }

            GUILayout.Space(20);

            // 2. 다중 선택 정보
            GUILayout.Label("<b>--- 선택 및 유닛 상태 ---</b>");
            GUILayout.Label($"현재 선택된 유닛 수: {selectionManager.SelectedUnits.Count}");
            
            foreach (var unit in selectionManager.SelectedUnits)
            {
                string roomName = unit.CurrentRoom != null ? unit.CurrentRoom.name : "None";
                string movingState = unit.IsMoving ? "<color=yellow>[이동 중]</color>" : "[대기 중]";
                GUILayout.Label($"- {unit.name} | 방: {roomName} | {movingState}");
            }

            GUILayout.EndArea();
        }
    }
}
