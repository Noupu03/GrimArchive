using UnityEngine;

public interface IPerceptible
{
    Vector2Int PerceptiblePosition { get; }
    int PerceptibleFloor { get; }
    float PerceptibleStealth { get; }
    float PerceptibleSpotting { get; }
    FactionData PerceptibleFactionData { get; }
    IFactionBehavior PerceptibleFactionBehavior { get; }
    bool IsPerceptibleSpecialUnit { get; }
}
