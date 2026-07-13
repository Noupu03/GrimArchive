using System.Collections.Generic;
using UnityEngine;

public class Room
{
    public string RoomName { get; set; } = "Room";
    public int MaxPopulation { get; set; } = 10;
    public bool IsCombatActive
    {
        get
        {
            bool hasHuman = false;
            bool hasMonster = false;
            foreach (var unit in _containedUnits)
            {
                if (unit == null || unit.hp <= 0) continue;
                if (unit is Human) hasHuman = true;
                if (unit is Monster) hasMonster = true;
                
                if (hasHuman && hasMonster) return true;
            }
            return false;
        }
    }
    public Vector3 TopLeftWorldPos { get; set; } = Vector3.zero;

    private List<Unit> _containedUnits = new List<Unit>();
    public IReadOnlyList<Unit> ContainedUnits => _containedUnits;

    public int CurrentPopulation
    {
        get
        {
            int total = 0;
            foreach (var unit in _containedUnits)
            {
                if (unit is Human) continue; // 인간은 배치 인구수(인원) 산정에서 제외
                total += unit.populationCost;
            }
            return total;
        }
    }

    public bool CanAcceptPopulation(int additionalPopulation)
    {
        return CurrentPopulation + additionalPopulation <= MaxPopulation;
    }

    public void AddUnit(Unit unit)
    {
        if (!_containedUnits.Contains(unit)) _containedUnits.Add(unit);
    }

    public void RemoveUnit(Unit unit)
    {
        if (_containedUnits.Contains(unit)) _containedUnits.Remove(unit);
    }
}
