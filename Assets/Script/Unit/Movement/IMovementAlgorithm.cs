using UnityEngine;

public interface IMovementAlgorithm
{
    bool TryGetNextStep(Unit unit, Vector2Int targetPos, out Dir nextDir);
}
