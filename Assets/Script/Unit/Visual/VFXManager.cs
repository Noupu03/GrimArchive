using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

// 이펙트 프리팹은 전역 공유가 아니라 유닛 타입별 프리팹(UnitVisualDefinition)에서 온다 — 이
// 클래스는 순수 스폰 메커니즘(위치 계산/생명주기 관리)만 담당하는 씬 불필요 순수 C# 클래스.
public class VFXManager
{
    private static Dictionary<GameObject, ObjectPool<GameObject>> _pools = new Dictionary<GameObject, ObjectPool<GameObject>>();

    // 캐릭터 스프라이트 피벗(바텀 센터) 기준 로컬 오프셋 — 부모 지정 스폰 시 이펙트를 몸통 높이로
    // 띄운다(아래 Spawn 참고).
    private static readonly Vector3 ParentedSpawnPivotOffset = new Vector3(0f, 0.5f, 0f);

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
        
        // 이펙트 크기는 항상 "프리팹에 저장된 스케일" 그대로다 — 부모/무부모 스폰 둘 다 이 값을
        // 기준으로 계산해야 프리팹 루트에서 조정한 크기가 무시되지 않는다.
        Vector3 authoredScale = prefab.transform.localScale;

        if (parent != null)
        {
            go.transform.SetParent(parent);
            // 캐릭터 스프라이트는 피벗이 바텀 센터(발밑)라 로컬 원점에 그대로 붙이면 발밑에서 나오는 것처럼
            // 보여 몸통 높이만큼(0.5) 띄운다 — 부모 지정 스폰 전부가 이 지점을 거치므로 한 번에 고쳐진다.
            go.transform.localPosition = ParentedSpawnPivotOffset;
            go.transform.localScale = ToLocalScale(authoredScale, parent);
            go.transform.rotation = rotation;
        }
        else
        {
            Transform poolRoot = PooledObjectRoot.Get();
            go.transform.SetParent(poolRoot);
            go.transform.position = position;
            go.transform.localScale = ToLocalScale(authoredScale, poolRoot);
            go.transform.rotation = rotation;
        }

        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play(); // 재사용 시 파티클 재생
        }

        // ParticleLifetimeController가 붙어있으면 정지 타이밍을 그 컴포넌트에 맡긴다("재생 길이만큼
        // 지난 뒤 통째로 반납"하면 진행 중인 파티클이 뚝 끊기므로) — 없으면 자동 추정 방식으로 폴백한다.
        var lifetimeController = go.GetComponent<ParticleLifetimeController>();
        if (lifetimeController != null)
        {
            lifetimeController.BeginLifecycle(() =>
            {
                // 씬 전환 등으로 이미 파괴된 경우 방어(ReleaseToPoolAfterTime과 동일한 안전장치).
                if (go != null) pool.Release(go);
            });
        }
        else
        {
            float lifetime = ps != null
                ? ps.main.duration + ps.main.startLifetime.constantMax
                : 2f;

            ReleaseToPoolAfterTime(pool, go, lifetime).Forget();
        }
        return go;
    }

    // 이펙트 최종 월드 스케일이 프리팹 저장 스케일과 같도록 부모 누적 스케일(lossyScale)만
    // 상쇄한다 — VFX는 Scaling Mode가 Hierarchy라 안 하면 유닛 footprint 배율까지 곱해져 대형
    // 유닛만 커진다(부호도 상쇄해 좌우 반전된 부모도 안전).
    private static Vector3 ToLocalScale(Vector3 authoredScale, Transform parent)
    {
        if (parent == null) return authoredScale;

        Vector3 pScale = parent.lossyScale;
        return new Vector3(
            Mathf.Abs(pScale.x) > 0.0001f ? authoredScale.x / pScale.x : authoredScale.x,
            Mathf.Abs(pScale.y) > 0.0001f ? authoredScale.y / pScale.y : authoredScale.y,
            Mathf.Abs(pScale.z) > 0.0001f ? authoredScale.z / pScale.z : authoredScale.z
        );
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

    // 코어/문 파괴 채널링 전용 VFX — 파괴 행동이 끝날 때까지 계속 재생돼야 해서 위 Spawn()의 자동
    // 반납 방식과 안 맞는다(looping=1이라 명시적으로 멈춰야 함). 채널링이 잦지 않아 순수
    // Instantiate/Destroy로 관리하며 Unit.SetAttackObjectTarget/ClearAttackObjectTarget이 시작/종료를 담당한다.
    private static GameObject _blockBreakingVfxPrefab;
    private static GameObject BlockBreakingVfxPrefab =>
        _blockBreakingVfxPrefab ??= Resources.Load<GameObject>("Prefabs/VFX/VFX_BlockBreaking");

    public static GameObject SpawnBlockBreakingVfx(Unit unit, Vector3Int targetPos)
    {
        if (BlockBreakingVfxPrefab == null || unit == null || unit.Session == null) return null;

        // 대상(문/코어) 오브젝트의 실제 비주얼에 자식으로 붙인다 — 위치가 자동으로 맞고, 그 오브젝트의
        // 비주얼이 파괴되면(예: 문 파괴 시 Session.RemoveDoor) 이 이펙트도 함께 파괴되는 안전망이 된다.
        GameObject targetVisual = unit.Session.GetObjectVisual(targetPos);
        Transform parent = targetVisual != null ? targetVisual.transform : null;
        Vector3 worldPos = parent != null ? parent.position : Vector3.zero;

        GameObject instance = Object.Instantiate(BlockBreakingVfxPrefab, worldPos, Quaternion.identity, parent);
        if (parent != null)
        {
            instance.transform.localPosition = Vector3.zero;
            // 위 Spawn()과 동일한 규칙 — 대상(문/코어) 비주얼의 스케일이 어떻든 이펙트는 프리팹이 정한
            // 크기 그대로 보이게 한다(Scaling Mode가 Hierarchy라 상쇄하지 않으면 부모 스케일이 곱해진다).
            instance.transform.localScale = ToLocalScale(BlockBreakingVfxPrefab.transform.localScale, parent);
        }

        // 정렬 순서 — 프리팹 기본값(Sorting Layer "Default"/Order 0)이면 같은 자리의 문/코어
        // 스프라이트(sortingOrder=5)에 가려 안 보이므로, 대상과 같은 정렬 레이어에 그보다 위로 맞춘다.
        var psr = instance.GetComponent<ParticleSystemRenderer>();
        if (psr != null)
        {
            SpriteRenderer targetSr = targetVisual != null ? targetVisual.GetComponent<SpriteRenderer>() : null;
            if (targetSr != null) psr.sortingLayerID = targetSr.sortingLayerID;
            psr.sortingOrder = (targetSr != null ? targetSr.sortingOrder : 5) + 10;
        }

        var ps = instance.GetComponent<ParticleSystem>();
        if (ps != null) ps.Play();

        return instance;
    }

    // SpawnBlockBreakingVfx로 만든 인스턴스를 멈출 때 사용 — looping VFX라 자연 종료가 없으므로
    // 항상 명시적으로 파괴해야 한다.
    public static void StopBlockBreakingVfx(GameObject instance)
    {
        if (instance != null) Object.Destroy(instance);
    }

    // 방 점령(코어 파괴로 소유권 전환) 축하 폭발 VFX — 단발성이라 SpawnBlockBreakingVfx와 달리 기존
    // 풀링 Spawn()을 그대로 쓴다. OffenseProcessor.OnCoreDestroyed가 소유권 전환 순간에만 호출한다.
    private static GameObject _coreBoomHumanVfxPrefab;
    private static GameObject _coreBoomMonsterVfxPrefab;
    private static GameObject CoreBoomHumanVfxPrefab =>
        _coreBoomHumanVfxPrefab ??= Resources.Load<GameObject>("Prefabs/VFX/VFX_CoreBoomHuman Variant");
    private static GameObject CoreBoomMonsterVfxPrefab =>
        _coreBoomMonsterVfxPrefab ??= Resources.Load<GameObject>("Prefabs/VFX/VFX_CoreBoomMonster Variant");

    public static void SpawnCoreCaptureVfx(FactionType claimant, Vector3 worldPos)
    {
        GameObject prefab = claimant switch
        {
            FactionType.Human => CoreBoomHumanVfxPrefab,
            FactionType.Player => CoreBoomMonsterVfxPrefab,
            _ => null,
        };
        if (prefab == null) return;

        Spawn(prefab, worldPos, Quaternion.identity);
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
