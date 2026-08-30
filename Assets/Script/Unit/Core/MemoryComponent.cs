using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class MemoryComponent : IUnitComponent
{
    private Unit _owner;
    
    public PersonalMapKnowledge personalMap = new PersonalMapKnowledge();
    public List<string> collectedObjects = new List<string>();
    // 몬스터용 개인 지도 — personalMap과 달리 지형 밝히기 + 함정 위치만 담는 축소판(가중치 없음).
    // 모든 Unit이 MemoryComponent를 갖고 있어(Unit.cs OnEnable) Human/Monster 둘 다 자동으로 이
    // 필드도 갖게 되지만, 실제로 채우고 노출하는 건 Monster.monsterMap 프로퍼티(Unit.cs)뿐이다.
    public MonsterMapKnowledge monsterMap = new MonsterMapKnowledge();

    
    public MemoryComponent() { }

    public MemoryComponent(Unit owner)
    {
        _owner = owner;
    }

    public void OnUpdate(float deltaTime) {}
    public void OnDespawn() {}
}


