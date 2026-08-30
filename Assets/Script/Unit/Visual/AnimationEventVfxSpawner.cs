using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// AnimationClip의 AnimationEvent에서 호출돼 파티클 이펙트 프리팹을 스폰하는 독립 컴포넌트 — 기존
// 유닛 파이프라인(Unit/UnitGenerate/VFXManager)을 참조하지 않아 그쪽이 새로 설계돼도 재사용 가능하다.
// 사용법: 클립에 이벤트 추가(Function: OnVfxEvent, String: eventKey) 후 이 컴포넌트에 eventKey별 설정을 지정.
public class AnimationEventVfxSpawner : MonoBehaviour
{
    [SerializeField] private List<VfxEventDefinition> vfxEvents = new List<VfxEventDefinition>();

    // AnimationEvent의 Function으로 이 메서드를 지정하고, String 파라미터에 eventKey를 넣는다.
    public void OnVfxEvent(string eventKey)
    {
        VfxEventDefinition def = vfxEvents.Find(e => e.eventKey == eventKey);
        if (def == null || def.effectPrefab == null)
        {
            LogHelper.Warning(LogHelper.GAME, $"[AnimationEventVfxSpawner] eventKey '{eventKey}'에 대한 정의를 찾을 수 없습니다.");
            return;
        }

        Transform reference = def.referenceTransform != null ? def.referenceTransform : transform;
        Vector3 pos = reference.TransformPoint(def.positionOffset);
        Quaternion rot = reference.rotation * Quaternion.Euler(def.rotationOffset);

        // 스케일은 프리팹 원본 그대로 유지된다 — VFXManager.Spawn 자신이 프리팹 스케일을 적용한다.
        VFXManager.Spawn(def.effectPrefab, pos, rot);
    }
}
