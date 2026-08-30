using System.Collections.Generic;
using UnityEngine;

// 오브젝트 위치 그리드 저장소. 실제 스폰(아트 스프라이트 선택/문·함정·시체 구분/ShadowCaster2D 등)은
// GameSession.SpawnObject가 담당한다 — objectGrid는 이 클래스가 소유한 딕셔너리를 GameSession.
// objectGrid 프로퍼티로 노출해 공유한다.
public class ObjectSpawner
{
    public Dictionary<Vector3Int, InteractableObject> objectGrid { get; private set; } = new Dictionary<Vector3Int, InteractableObject>();

    // GameSession.CollectObject가 실제 비주얼 파괴까지 담당하므로(GameSession.objectVisuals 참고),
    // 여기서는 그리드 상태(수집됨 표시 + 제거)만 갱신한다.
    public void CollectObject(Vector3Int pos)
    {
        if (objectGrid.TryGetValue(pos, out var obj))
        {
            obj.IsCollected = true;
            objectGrid.Remove(pos);
        }
    }
}
