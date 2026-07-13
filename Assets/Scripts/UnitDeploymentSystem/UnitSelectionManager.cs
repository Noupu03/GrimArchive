using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace UnitDeploymentSystem
{
    public class UnitSelectionManager : MonoBehaviour
    {
        private List<Unit> selectedUnits = new List<Unit>();
        public IReadOnlyList<Unit> SelectedUnits => selectedUnits;

        public void SelectUnit(Unit unit)
        {
            if (!selectedUnits.Contains(unit))
            {
                selectedUnits.Add(unit);
            }
        }

        public void DeselectUnit(Unit unit)
        {
            if (selectedUnits.Contains(unit))
            {
                selectedUnits.Remove(unit);
            }
        }

        public void ClearSelection()
        {
            selectedUnits.Clear();
        }

        // HAARE Framework: 다중 명령 하달 처리
        public async UniTaskVoid CommandMoveToRoomAsync(Room targetRoom)
        {
            if (targetRoom.IsCombatActive)
            {
                Debug.LogWarning("해당 방은 전투 중이므로 이동할 수 없습니다.");
                return;
            }

            // 1. 인구수 검사 (이미 해당 방에 소속된 유닛은 제외)
            int totalCostToMove = 0;
            foreach (var unit in selectedUnits)
            {
                if (unit.CurrentRoom != targetRoom)
                {
                    totalCostToMove += unit.PopulationCost;
                }
            }

            if (!targetRoom.CanAcceptPopulation(totalCostToMove))
            {
                Debug.LogWarning("대상 방의 수용 가능 인구수를 초과하여 이동 명령이 취소되었습니다.");
                return;
            }

            // 2. 이동 가능이 확정되었으므로 각 유닛에게 명령 하달 (즉시 소속 변경은 Unit 내부에서 처리됨)
            foreach (var unit in selectedUnits)
            {
                if (unit.CurrentRoom != targetRoom)
                {
                    // Unit 내부의 IssueMoveCommand를 통해 기존 취소 및 새 Native Routine 시작
                    unit.IssueMoveCommand(targetRoom);
                }
            }

            await UniTask.CompletedTask;
        }

        public void CommandSetBehavior(IBehavior behavior)
        {
            foreach (var unit in selectedUnits)
            {
                unit.SetBehavior(behavior);
            }
        }
    }
}
