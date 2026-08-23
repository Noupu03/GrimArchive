using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

// 이펙트 프리팹은 이제 전역 공유가 아니라 유닛 타입별 프리팹(UnitVisualDefinition)에서 온다.
// 이 클래스는 순수 스폰 메커니즘(위치 계산/생명주기 관리)만 담당한다. Update/OnGUI/인스펙터 데이터가
// 전혀 없어서 씬 GameObject일 필요가 없는 순수 C# 클래스.
public class VFXManager
{
    private static Dictionary<GameObject, ObjectPool<GameObject>> _pools = new Dictionary<GameObject, ObjectPool<GameObject>>();

    public void Spawn(GameObject prefab, Unit unit)
    {
        if (prefab == null || unit == null) return;

        Transform parent  = unit.Generate?.GetVisualTransform(unit);
        Vector3   worldPos = GetWorldPos(unit);

        Spawn(prefab, worldPos, Quaternion.identity, parent);
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null) return null;

        if (!_pools.TryGetValue(prefab, out var pool))
        {
            pool = new ObjectPool<GameObject>(
                createFunc: () => {
                    var go = Object.Instantiate(prefab);
                    go.transform.SetParent(PooledObjectRoot.Get());
                    return go;
                },
                actionOnGet: (obj) => { if (obj != null) obj.SetActive(true); },
                actionOnRelease: (obj) => { 
                    if (obj != null) {
                        obj.SetActive(false);
                        obj.transform.SetParent(PooledObjectRoot.Get());
                    }
                },
                actionOnDestroy: (obj) => { if (obj != null) Object.Destroy(obj); },
                collectionCheck: false,
                defaultCapacity: 10,
                maxSize: 100
            );
            _pools[prefab] = pool;
        }

        GameObject go = pool.Get();
        if (go == null) 
        {
            // If the pooled object was destroyed (e.g. scene change), recreate it
            pool.Clear();
            go = pool.Get();
        }
        
        if (parent != null)
        {
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            Vector3 pScale = parent.lossyScale;
            go.transform.localScale = new Vector3(
                Mathf.Abs(pScale.x) > 0.0001f ? (1f / pScale.x) : 1f,
                Mathf.Abs(pScale.y) > 0.0001f ? (1f / pScale.y) : 1f,
                1f
            );
            go.transform.rotation = rotation;
        }
        else
        {
            go.transform.SetParent(PooledObjectRoot.Get());
            go.transform.position = position;
            go.transform.localScale = Vector3.one;
            go.transform.rotation = rotation;
        }

        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play(); // 재사용 시 파티클 재생
        }

        float lifetime = ps != null
            ? ps.main.duration + ps.main.startLifetime.constantMax
            : 2f;
        
        ReleaseToPoolAfterTime(pool, go, lifetime).Forget();
        return go;
    }

    private static async UniTaskVoid ReleaseToPoolAfterTime(ObjectPool<GameObject> pool, GameObject go, float delay)
    {
        // Cancel if the gameobject is destroyed unexpectedly (e.g. scene change)
        bool canceled = await UniTask.Delay(System.TimeSpan.FromSeconds(delay), ignoreTimeScale: false, cancelImmediately: true).SuppressCancellationThrow();
        if (!canceled && go != null && go.activeInHierarchy)
        {
            pool.Release(go);
        }
    }

    public static Vector3 GetWorldPos(Unit unit)
    {
        if (unit == null) return Vector3.zero;
        Vector3 offset = unit.Generate != null
            ? unit.Generate.GetFloorOffset(unit.currentFloor)
            : Vector3.zero;

        Vector2 footprint = unit.unitType != null ? unit.unitType.footprint : Vector2.one;

        return new Vector3(
            unit.position.x + footprint.x * 0.5f,
            unit.position.y + footprint.y * 0.5f,
            0f
        ) + offset;
    }
}
