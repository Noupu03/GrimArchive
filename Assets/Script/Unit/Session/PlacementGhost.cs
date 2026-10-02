using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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

    // 멀티 타일 오브젝트(문 1×2 묶음) 고스트의 두 번째 칸부터 쓰는 추가 스프라이트 — 필요할 때 만들고 안 쓰면 숨긴다.
    private readonly List<GameObject> _extraGhosts = new List<GameObject>();

    public void Show(Sprite sprite)
    {
        if (_go == null)
        {
            _go = new GameObject(_name);
            _renderer = _go.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = _sortingOrder;
        }
        _renderer.sprite = sprite;
        // 다른 모드(footprint 스케일·문 고스트의 회전)가 남긴 변형이 새 모드로 새지 않게 초기화한다.
        _go.transform.localScale = Vector3.one;
        _go.transform.rotation = Quaternion.identity;
        HideExtraGhosts();
        _go.SetActive(true);
    }

    // 스프라이트를 Addressables로 불러온 뒤 보여 준다 — 로드가 끝났을 때 그 모드가 이미 끝났으면
    // (stillWanted=false) 보여 주지 않고 null을 돌려준다. 이미 불러 둔 스프라이트면 같은 프레임에 끝난다.
    public async UniTask<Sprite> ShowAsync(string spriteKey, Func<bool> stillWanted)
    {
        Sprite sprite = await GameAssets.LoadSpriteAsync(spriteKey);
        if (!stillWanted()) return null;
        Show(sprite);
        return sprite;
    }

    // 멀티 타일 오브젝트(문 1×2 묶음) 전용 — 칸마다 스프라이트 한 장씩 같은 회전으로 보여 준다(실제 문과 같은 모양). 첫 칸은 기본 고스트, 나머지는 추가 고스트.
    public void UpdateTiles(IReadOnlyList<Vector3> worldCenters, float rotationZDegrees, bool canPlace)
    {
        if (_go == null || worldCenters.Count == 0) return;
        Quaternion rotation = Quaternion.Euler(0f, 0f, rotationZDegrees);
        Color color = canPlace ? CanPlaceColor : CannotPlaceColor;

        _go.transform.position = worldCenters[0];
        _go.transform.rotation = rotation;
        _go.transform.localScale = new Vector3(DoorGeometry.IsMirroredLeaf(0, worldCenters.Count) ? -1f : 1f, 1f, 1f);
        _renderer.color = color;

        for (int i = 1; i < worldCenters.Count; i++)
        {
            var extra = EnsureExtraGhost(i - 1);
            extra.SetActive(true);
            extra.transform.position = worldCenters[i];
            extra.transform.rotation = rotation;
            // 실제 문과 같은 반전 규칙(DoorGeometry.IsMirroredLeaf) — 고스트도 양쪽으로 갈라진 모양으로 보인다.
            extra.transform.localScale = new Vector3(DoorGeometry.IsMirroredLeaf(i, worldCenters.Count) ? -1f : 1f, 1f, 1f);
            extra.GetComponent<SpriteRenderer>().color = color;
        }
        for (int j = worldCenters.Count - 1; j < _extraGhosts.Count; j++) _extraGhosts[j].SetActive(false);
    }

    private GameObject EnsureExtraGhost(int index)
    {
        while (_extraGhosts.Count <= index)
        {
            var go = new GameObject(_name + "_Extra" + _extraGhosts.Count);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = _sortingOrder;
            _extraGhosts.Add(go);
        }
        var extra = _extraGhosts[index];
        extra.GetComponent<SpriteRenderer>().sprite = _renderer.sprite; // 모드가 바뀌어 스프라이트가 달라졌을 수 있다
        return extra;
    }

    private void HideExtraGhosts()
    {
        foreach (var g in _extraGhosts) if (g != null) g.SetActive(false);
    }

    public void Hide()
    {
        if (_go != null) _go.SetActive(false);
        HideExtraGhosts();
    }

    public void UpdatePosition(Vector3Int gridPos, Vector3 floorOffset, bool canPlace)
    {
        if (_go == null) return;
        HideExtraGhosts(); // 문 고스트에서 일반 위치로 돌아왔을 때 두 번째 칸이 남지 않게
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
