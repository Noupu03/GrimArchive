using UnityEngine;

// 마우스를 따라다니며 배치 가능/불가능 여부를 색으로 보여주는 "고스트" 스프라이트 — 빌드 모드와
// 오브젝트/함정/코어 배치 모드가 각자 거의 동일한 로직을 중복 구현하던 것을 하나로 뺐다.
public class PlacementGhost
{
    private static readonly Color CanPlaceColor = new Color(0f, 1f, 0f, 0.5f);
    private static readonly Color CannotPlaceColor = new Color(1f, 0f, 0f, 0.5f);

    private readonly string _name;
    private readonly int _sortingOrder;
    private GameObject _go;
    private SpriteRenderer _renderer;

    public PlacementGhost(string name, int sortingOrder = 10)
    {
        _name = name;
        _sortingOrder = sortingOrder;
    }

    public void Show(Sprite sprite)
    {
        if (_go == null)
        {
            _go = new GameObject(_name);
            _renderer = _go.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = _sortingOrder;
        }
        _renderer.sprite = sprite;
        _go.SetActive(true);
    }

    public void Hide()
    {
        if (_go != null) _go.SetActive(false);
    }

    public void UpdatePosition(Vector3Int gridPos, Vector3 floorOffset, bool canPlace)
    {
        if (_go == null) return;
        _go.transform.position = new Vector3(gridPos.x + 0.5f, gridPos.y + 0.5f, 0f) + floorOffset;
        _renderer.color = canPlace ? CanPlaceColor : CannotPlaceColor;
    }

    // footprint 지원(다중 타일 건물 전용) — gridPos는 footprint의 최소 좌표(좌하단) 앵커다. 이
    // 오버로드만 스프라이트를 footprint 칸 수에 맞춰 스케일한다(안 그러면 고스트가 1칸 크기로만 보임).
    public void UpdatePosition(Vector3Int gridPos, Vector3 floorOffset, bool canPlace, Vector2Int footprint)
    {
        if (_go == null) return;
        _go.transform.position = new Vector3(gridPos.x + footprint.x / 2f, gridPos.y + footprint.y / 2f, 0f) + floorOffset;
        _renderer.color = canPlace ? CanPlaceColor : CannotPlaceColor;

        Vector2 spriteWorldSize = _renderer.sprite != null ? (Vector2)_renderer.sprite.bounds.size : Vector2.one;
        float scaleX = spriteWorldSize.x > 0f ? footprint.x / spriteWorldSize.x : footprint.x;
        float scaleY = spriteWorldSize.y > 0f ? footprint.y / spriteWorldSize.y : footprint.y;
        _go.transform.localScale = new Vector3(scaleX, scaleY, 1f);
    }
}
