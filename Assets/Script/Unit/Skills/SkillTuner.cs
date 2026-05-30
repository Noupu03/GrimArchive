using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 인스펙터에서 모든 스킬의 선딜레이·쿨타임을 조정하는 컴포넌트.
/// 빈 GameObject에 붙이고 에디터의 "스킬 자동 감지" 버튼으로 목록을 채운다.
/// </summary>
public class SkillTuner : MonoBehaviour
{
    [System.Serializable]
    public class SkillTuningEntry
    {
        public string skillName;
        [Tooltip("공격속도 보정 전 기본 선딜레이 (ms)")]
        [Min(50f)]  public float baseDelayMs  = 500f;
        [Tooltip("쿨타임 감소 적용 전 기본 쿨타임 (초)")]
        [Min(0.1f)] public float baseCooldown = 3f;
    }

    public List<SkillTuningEntry> entries = new();

    void Awake() => Apply();

    public void Apply()
    {
        SkillTuningOverride.Clear();
        foreach (var e in entries)
            SkillTuningOverride.Register(e.skillName, e.baseDelayMs, e.baseCooldown);
    }
}
