using UnityEngine;

// AnimationEventVfxSpawner.OnVfxEvent(string eventKey)가 참조하는 이펙트 스폰 정의 1개.
[System.Serializable]
public class VfxEventDefinition
{
    public string eventKey;              // AnimationEvent의 string 파라미터와 매칭
    public GameObject effectPrefab;      // 스폰할 파티클 프리팹

    [Tooltip("비워두면 이 컴포넌트가 붙은 오브젝트의 transform을 기준으로 사용")]
    public Transform referenceTransform; // 위치/회전 기준이 되는 오브젝트

    public Vector3 positionOffset;       // 레퍼런스 로컬 기준 위치 오프셋
    public Vector3 rotationOffset;       // 레퍼런스 로컬 기준 회전 오프셋 (Euler) — 스케일은 다루지 않음
}
