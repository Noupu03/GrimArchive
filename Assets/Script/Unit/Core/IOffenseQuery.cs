using System.Collections.Generic;
using UnityEngine;

public interface IOffenseQuery
{
    IReadOnlyList<Unit> GetUnitsInRoom(RectInt bounds);
}
