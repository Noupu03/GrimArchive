using UnityEngine;

// Resources.Load<Sprite>를 필드에 지연 캐시하는 패턴(2026-08-25 리팩토링 — DoorSystem/
// ObjectPlacementController/FogOfWarSystem/MapRandering이 각자 `if (x == null) x = Resources.
// Load<Sprite>(path);`(또는 `x ??= ...`)를 반복 구현하고 있었다)을 한 곳으로 모은다. 캐시 필드 자체는
// (생명주기/보유 시점이 클래스마다 다를 수 있어) 각 클래스가 그대로 소유하고, 이 헬퍼는 "없으면
// 로드해서 채운다"는 로딩 로직 한 줄만 공유한다.
public static class SpriteCache
{
    public static Sprite GetOrLoad(ref Sprite cache, string resourcePath)
    {
        if (cache == null) cache = Resources.Load<Sprite>(resourcePath);
        return cache;
    }
}
