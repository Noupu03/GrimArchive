using System.Collections.Generic;
using UnityEngine;

namespace UnitDeploymentSystem
{
    public class Room : MonoBehaviour
    {
        [SerializeField] private int maxPopulation = 10;
        public int MaxPopulation => maxPopulation;

        private List<Unit> containedUnits = new List<Unit>();
        public IReadOnlyList<Unit> ContainedUnits => containedUnits;

        public int CurrentPopulation 
        {
            get
            {
                int total = 0;
                foreach (var unit in containedUnits)
                {
                    total += unit.PopulationCost;
                }
                return total;
            }
        }

        public bool IsCombatActive { get; set; } = false;

        public bool CanAcceptPopulation(int additionalPopulation)
        {
            return CurrentPopulation + additionalPopulation <= MaxPopulation;
        }

        public void AddUnit(Unit unit)
        {
            if (!containedUnits.Contains(unit))
            {
                containedUnits.Add(unit);
            }
        }

        public void RemoveUnit(Unit unit)
        {
            if (containedUnits.Contains(unit))
            {
                containedUnits.Remove(unit);
            }
        }
    }
}
