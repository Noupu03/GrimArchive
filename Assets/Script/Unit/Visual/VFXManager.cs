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

    // 캐릭터 스프라이트 피벗(바텀 센터) 기준 로컬 오프셋 — 부모 지정 스폰 시 이펙트를 몸통 높이로
    // 띄운다(2026-08-24 사용자 요청, 아래 Spawn 참고).
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
        
        // 이펙트 크기는 항상 "프리팹에 저장된 스케일" 그대로다(2026-08-24 사용자 요청 "파티클 생성시
        // 게임 프리팹의 Scale이 아닌 임의로 (1,1,1) 데이터를 사용하는 듯 함 — 불러오려는 VFX 파티클의
        // Scale을 그대로 적용되게"). 예전엔 부모 지정 스폰이 부모 lossyScale의 역수를, 무부모 스폰이
        // Vector3.one을 통째로 덮어써서 프리팹 루트에서 조정한 크기(Assets/VFX/Prefab/*.prefab의 루트
        // 스케일 0.01 등)가 전부 무시되고 항상 1배로 나왔다.
        Vector3 authoredScale = prefab.transform.localScale;

        if (parent != null)
        {
            go.transform.SetParent(parent);
            // 캐릭터 스프라이트는 피벗이 바텀 센터(발밑)라 로컬 원점(0,0)에 그대로 붙이면 이펙트가
            // 발밑에서 나오는 것처럼 보인다(사용자 신고, 2026-08-24 "파티클 피벗 0.5 위로 올려서
            // 나오게 해주세요 — 스프라이트는 피벗이 바텀 센터 중심이라 기준점 이상해짐"). 몸통
            // 높이에 가깝도록 0.5만큼 위로 띄운다 — 히트 스파크/가드/패리/사망 VFX 등 부모 지정
            // 스폰 전부가 이 한 지점을 거치므로 한 번에 고쳐진다.
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

        // 프리팹에 ParticleLifetimeController가 붙어있으면 정지 타이밍을 그 컴포넌트에 맡긴다(2026-08-24
        // 사용자 요청 "파티클 프리팹 비활성화로 처리하면 중간에 짤려버림... 생성 중단 → 남은 파티클이
        // 없으면 프리팹 비활성화") — 아래 "재생 길이만큼 지난 뒤 통째로 반납"하던 기존 방식은 진행 중인
        // 파티클까지 그 순간 화면에서 뚝 끊겼다. 컴포넌트가 없는 기존 프리팹은 기존 자동 추정 방식으로
        // 그대로 폴백한다(하위 호환).
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

    // "이펙트의 최종 월드 스케일 == 프리팹에 저장된 스케일"이 되도록 부모의 누적 스케일(lossyScale)만
    // 상쇄한 로컬 스케일을 계산한다(2026-08-24). 이 프로젝트의 VFX 프리팹은 전부 ParticleSystem의
    // Scaling Mode가 Hierarchy(scalingMode: 0)라 파티클 크기가 계층 전체 스케일을 따라간다 — 프리팹
    // 루트 스케일 하나로 자식 파티클까지 통째로 조절할 수 있는 대신, 그대로 두면 유닛 비주얼 루트의
    // footprint 배율(2x2 유닛이면 (2,2,1) — UnitGenerate.SetupUnitVisual)까지 곱해져 대형 유닛의
    // 이펙트만 커진다. 이펙트 크기는 유닛 크기와 무관하게 프리팹이 정한 값으로 통일한다.
    // 부호까지 그대로 나눠 상쇄하므로 부모가 좌우 반전(스케일 x 음수)돼 있어도 이펙트는 반전되지 않는다.
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

    // 코어/문 파괴 채널링 전용 VFX(2026-08-24 사용자 요청, VFX_BlockBreaking.prefab) — "재생 시간이
    // 끝나도 파괴 행동이 끝날 때까지 계속, 파괴가 끝나면 즉시 종료"라는 요구라 위 Spawn()의 "재생
    // 시간만큼 지난 뒤 자동으로 풀에 반납" 방식과는 맞지 않는다(프리팹 자체도 looping=1이라 자동으로
    // 안 끝남 — 호출부가 명시적으로 멈춰야 한다). 채널링 시작/종료가 잦은 이벤트가 아니라 풀링 이득이
    // 적어 순수 Instantiate/Destroy로 관리한다. Unit.SetAttackObjectTarget/ClearAttackObjectTarget이
    // 각각 시작/종료를 담당한다.
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

        // 정렬 순서(2026-08-24 사용자 신고 "이펙트 여전히 안 나옴" 원인) — 프리팹의 ParticleSystemRenderer가
        // 기본값(Sorting Layer "Default"/Order 0)이라, 같은 자리의 문/코어 스프라이트(GameSession.
        // SpawnObject가 sortingOrder=5로 그림)에 완전히 가려져 재생은 되지만 안 보였다. 대상 스프라이트와
        // 같은 정렬 레이어에, 그보다 확실히 위인 순서로 맞춘다.
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

    // 방 점령(코어 파괴로 소유권 전환) 축하 폭발 VFX(2026-08-24 사용자 요청 "인간 점령시 VFX_CoreBoomHuman,
    // 플레이어 몬스터 점령시 VFX_CoreBoomMonster") — 단발성이라 위 SpawnBlockBreakingVfx와 달리 기존
    // 풀링 Spawn()을 그대로 쓴다(재생 시간 지나면 자동으로 풀에 반납). OffenseProcessor.OnCoreDestroyed가
    // 소유권이 실제로 바뀐 순간에만 호출한다.
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
