using System.Collections.Generic;
using UnityEngine;

// AnimatorController로 재생되는 AnimationClip에 찍은 AnimationEvent에서 호출되어
// 파티클 이펙트 프리팹을 스폰하는 독립 컴포넌트. Unit/UnitGenerate/VFXManager 등
// 기존 유닛 파이프라인을 전혀 참조하지 않는다 — 그쪽이 나중에 새로 설계되어도 그대로 재사용 가능.
//
// 사용법:
//   1. Animation 창에서 클립에 이벤트 추가 → Function: OnVfxEvent, String: 임의의 eventKey
//   2. Animator가 붙은 오브젝트(또는 그 자식)에 이 컴포넌트를 붙이고 vfxEvents에 eventKey별로
//      effectPrefab / referenceTransform / positionOffset / rotationOffset을 지정
public class AnimationEventVfxSpawner : MonoBehaviour
{
    [SerializeField] private List<VfxEventDefinition> vfxEvents = new List<VfxEventDefinition>();

    // AnimationEvent의 Function으로 이 메서드를 지정하고, String 파라미터에 eventKey를 넣는다.
    public void OnVfxEvent(string eventKey)
    {
        VfxEventDefinition def = vfxEvents.Find(e => e.eventKey == eventKey);
        if (def == null || def.effectPrefab == null)
        {
            Debug.LogWarning($"[AnimationEventVfxSpawner] eventKey '{eventKey}'에 대한 정의를 찾을 수 없습니다.");
            return;
        }

        Transform reference = def.referenceTransform != null ? def.referenceTransform : transform;
        Vector3 pos = reference.TransformPoint(def.positionOffset);
        Quaternion rot = reference.rotation * Quaternion.Euler(def.rotationOffset);

        GameObject instance = VFXManager.Spawn(def.effectPrefab, pos, rot);
        if (instance != null)
        {
            instance.transform.localScale = def.effectPrefab.transform.localScale; // 스케일은 프리팹 원본 그대로 유지
        }
    }
}
