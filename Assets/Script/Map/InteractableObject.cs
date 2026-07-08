using UnityEngine;
using System;

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
    public bool IsCollected;

    public InteractableObject(string id, Vector3Int position, float baseInterest, float baseDanger = 0f)
    {
        Id = id;
        Position = position;
        BaseInterest = baseInterest;
        BaseDanger = baseDanger;
        IsCollected = false;
    }
}
