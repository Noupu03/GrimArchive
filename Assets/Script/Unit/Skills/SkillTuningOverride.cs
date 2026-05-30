using System.Collections.Generic;

/// <summary>
/// 런타임 스킬 튜닝값 레지스트리. SkillTuner MonoBehaviour가 씬 시작 시 채워준다.
/// </summary>
public static class SkillTuningOverride
{
    struct TuningValues
    {
        public float baseDelayMs;
        public float baseCooldown;
    }

    static readonly Dictionary<string, TuningValues> _overrides = new();

    public static void Register(string skillName, float delayMs, float cooldown)
        => _overrides[skillName] = new TuningValues { baseDelayMs = delayMs, baseCooldown = cooldown };

    public static void Clear() => _overrides.Clear();

    public static float GetDelayMs(string skillName, float defaultMs)
        => _overrides.TryGetValue(skillName, out var v) ? v.baseDelayMs : defaultMs;

    public static float GetCooldown(string skillName, float defaultCd)
        => _overrides.TryGetValue(skillName, out var v) ? v.baseCooldown : defaultCd;
}
