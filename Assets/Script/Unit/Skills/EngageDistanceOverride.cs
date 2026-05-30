using System.Collections.Generic;

/// <summary>
/// 런타임 대치 거리 레지스트리. EngageDistanceTuner MonoBehaviour가 씬 시작 시 채워준다.
/// </summary>
public static class EngageDistanceOverride
{
    static readonly Dictionary<string, int> _overrides = new();

    public static void Register(string unitTypeName, int engageDist)
        => _overrides[unitTypeName] = engageDist;

    public static void Clear() => _overrides.Clear();

    public static int Get(string unitTypeName, int defaultDist)
        => _overrides.TryGetValue(unitTypeName, out var v) ? v : defaultDist;
}
