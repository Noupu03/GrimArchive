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
    // Queue로 관리: 만료시간(Time.time + 5초)을 enqueue, OnUpdate에서 Peek/Dequeue로 O(1) 제거.
    public readonly System.Collections.Generic.Queue<float> suspiciousMoveBoostTimers = new System.Collections.Generic.Queue<float>();

    
    public VisionStatComponent() { }

    public VisionStatComponent(Unit owner)
    {
        _owner = owner;
    }

    public void OnUpdate(float deltaTime) {}
    public void OnDespawn() {}
}


