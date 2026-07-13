using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class InteractableObject
{
    public string Id;
    public Vector3Int Position;
    public float BaseInterest;
    // 15장(v0.7 (1) 개정판): 타일 최종 위험도 = 기본 탐사 위험도 + 오브젝트 위험도. 지금 스폰되는
    // 오브젝트는 전부 순수 루팅 대상(위협 없음)이라 기본값 0 — 나중에 함정류 오브젝트가 생기면
    // 이 값을 0이 아닌 값으로 생성하기만 하면 된다(파이프는 이미 연결돼 있음).
    public float BaseDanger;
    // 01장 9절(시야-인지-반응): 대상 기본 가시성 — 오브젝트가 타일의 가시성(일반 100/벽 0)을
    // 무효화하고 대신 갖는 자기 값. 기본값 0(사용자 요청 — 오브젝트는 일단 전부 "안 보이는" 상태로
    // 두고 시야-인지 시스템이 실제로 어떻게 반응하는지 테스트한다). 눈에 잘 띄어야 하는 오브젝트가
    // 생기면 스폰 시점에 값을 올려서 지정하면 된다(VisionMath.ResolveBaseVisibility).
    public float BaseVisibility = 0f;
    public bool IsCollected;
    // 17장: 조사(investigate) 완료 여부 — Loot 오브젝트에만 의미가 있다(조사 50%감소 → 회수 0%감소
    // 2단계). Corpse/WipeoutTrace는 확인(check) 즉시 흥미도 0으로 가는 단일 단계라 이 필드를 안 쓴다.
    public bool IsInvestigated;

    // 시체/전리품 등을 구분하기 위한 태그
    public List<string> Tags = new List<string>();
    // 시체(흔적)인 경우, 원인 제공자의 위험도 단계
    public DangerStage CauserStage;
    // WipeoutTrace 태그일 때만 사용 — HumanKnowledgeBase.RegisterWipeoutTrace()가 발급한 흔적 ID.
    // 13-2장: 생환 파티가 이 오브젝트를 발견하면 OnWipeoutTraceReflected(TraceId)로 동일 ID당
    // 1회만 던전 위험도에 반영한다.
    public string TraceId;

    public InteractableObject(string id, Vector3Int position, float baseInterest, float baseDanger = 0f, List<string> tags = null, DangerStage causerStage = DangerStage.Stage0, string traceId = null, float baseVisibility = 0f)
    {
        Id = id;
        Position = position;
        BaseInterest = baseInterest;
        BaseDanger = baseDanger;
        BaseVisibility = baseVisibility;
        IsCollected = false;

        if (tags != null)
        {
            Tags = new List<string>(tags);
        }
        else
        {
            // 기본값은 전리품("Loot")으로 처리
            Tags = new List<string> { "Loot" };
        }

        CauserStage = causerStage;
        TraceId = traceId;
    }
}
