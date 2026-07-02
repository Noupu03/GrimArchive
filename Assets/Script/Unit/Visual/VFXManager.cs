using UnityEngine;

// 이펙트 프리팹은 이제 전역 공유가 아니라 유닛 타입별 프리팹(UnitVisualDefinition)에서 온다.
// 이 클래스는 순수 스폰 메커니즘(위치 계산/생명주기 관리)만 담당한다.
public class VFXManager : MonoBehaviour
{
    public void Spawn(GameObject prefab, Unit unit)
    {
        if (prefab == null || unit == null) return;

        Transform parent  = unit.Generate?.GetVisualTransform(unit);
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
        Vector3 offset = unit.Generate != null
            ? unit.Generate.GetFloorOffset(unit.currentFloor)
            : Vector3.zero;

        return new Vector3(
            unit.position.x + unit.unitType.footprint.x * 0.5f,
            unit.position.y + unit.unitType.footprint.y * 0.5f,
            0f
        ) + offset;
    }
}
