using UnityEngine;

// 마우스를 따라다니며 배치 가능/불가능 여부를 색으로 보여주는 "고스트" 스프라이트(2026-08-20 통합) —
// 빌드 모드(B/V, BuildPlacementController)와 오브젝트/함정/코어 배치 모드(O/P/C,
// ObjectPlacementController)가 각자 거의 동일한 GameObject 생성/위치 갱신/색상 표시 로직을 중복
// 구현하고 있던 것을 하나로 뺐다. 몬스터 배치 모드(R)는 고스트 하나가 아니라 여러 개(대기열의 각
// 유닛 실루엣)를 동시에 그려야 해서 이 클래스를 쓰지 않고 별도 OnGUI 그리기를 유지한다
// (MonsterPlacementController.DrawPlacementSilhouettes).
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
}
