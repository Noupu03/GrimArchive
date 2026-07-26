using UnityEngine;

[System.Serializable]
public class VisionStatComponent : IUnitComponent
{
    private Unit _owner;
    
    public float stealth = 0f;
    public float spotting = 0f;
    public float baseVisibility = 100f;
    public float attackVisibilityBoostTimer = 0f;

    // 03문서 4-6장: 수상한 타일 대상이 이동할 때마다 push되는 +20 증가분의 개별 잔여시간(각 5초).
    // UnitFunction.Move가 push, OnUpdate가 개별 감쇠/제거한다.
    public readonly System.Collections.Generic.List<float> suspiciousMoveBoostTimers = new System.Collections.Generic.List<float>();

    
    public VisionStatComponent() { }

    public VisionStatComponent(Unit owner)
    {
        _owner = owner;
    }

    public void OnUpdate(float deltaTime) {}
    public void OnDespawn() {}
}


