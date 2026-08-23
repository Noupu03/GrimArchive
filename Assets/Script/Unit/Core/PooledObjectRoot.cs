using UnityEngine;

// Projectile/VFXManager가 각자 동일하게 구현하고 있던 오브젝트 풀링 루트 트랜스폼 조회를 통합한 것.
// 씬에 "Object Pooling" GameObject가 없으면 하나 만들고(DontDestroyOnLoad), 있으면 그대로 재사용한다.
public static class PooledObjectRoot
{
    private static Transform _root;

    public static Transform Get()
    {
        if (_root != null) return _root;
        var go = GameObject.Find("Object Pooling");
        if (go == null)
        {
            go = new GameObject("Object Pooling");
            Object.DontDestroyOnLoad(go);
        }
        _root = go.transform;
        return _root;
    }
}
