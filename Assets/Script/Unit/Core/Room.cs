using System.Collections.Generic;
using UnityEngine;

public class Room
{
    public string RoomName { get; set; } = "Room";
    private List<Unit> _containedUnits = new List<Unit>();
    public IReadOnlyList<Unit> ContainedUnits => _containedUnits;

    public void AddUnit(Unit unit)
    {
        if (!_containedUnits.Contains(unit)) _containedUnits.Add(unit);
    }

    public void RemoveUnit(Unit unit)
    {
        if (_containedUnits.Contains(unit)) _containedUnits.Remove(unit);
    }
}
