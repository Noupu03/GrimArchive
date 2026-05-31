using UnityEngine;

public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [SerializeField] GameObject hitSparkPrefab;
    [SerializeField] GameObject guardPrefab;
    [SerializeField] GameObject parryPrefab;
    [SerializeField] GameObject attackFailPrefab;  // 공격 실패 (미설정 시 재생 생략)

    void Awake()
    {
        Instance = this;
    }

    public void SpawnHitSpark(Unit unit)    => Spawn(hitSparkPrefab, unit);
    public void SpawnGuard(Unit unit)       => Spawn(guardPrefab, unit);
    public void SpawnParry(Unit unit)       => Spawn(parryPrefab, unit);
    public void SpawnAttackFail(Unit unit)  => Spawn(attackFailPrefab, unit);

    void Spawn(GameObject prefab, Unit unit)
    {
        if (prefab == null || unit == null) return;

        Transform parent  = UnitGenerate.Instance?.GetVisualTransform(unit);
        Vector3   worldPos = GetWorldPos(unit);

        var go = parent != null
            ? Instantiate(prefab, worldPos, Quaternion.identity, parent)
            : Instantiate(prefab, worldPos, Quaternion.identity);

        var ps = go.GetComponent<ParticleSystem>();
        float lifetime = ps != null
            ? ps.main.duration + ps.main.startLifetime.constantMax
            : 2f;
        Destroy(go, lifetime);
    }

    static Vector3 GetWorldPos(Unit unit)
    {
        Vector3 offset = UnitGenerate.Instance != null
            ? UnitGenerate.Instance.GetFloorOffset(unit.currentFloor)
            : Vector3.zero;

        return new Vector3(
            unit.position.x + unit.unitType.footprint.x * 0.5f,
            unit.position.y + unit.unitType.footprint.y * 0.5f,
            0f
        ) + offset;
    }
}
