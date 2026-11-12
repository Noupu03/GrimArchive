using System;
using System.Collections.Generic;
using UnityEngine;

// 시설마다 하나씩 두는 월드 표시(진행 바·출현 구역)를 지연 생성하고, 격자에서 사라진 시설의 표시를 정리한다.
public sealed class PerBuildingViews<TView> where TView : class
{
	private readonly Dictionary<BuildingData, TView> _views = new Dictionary<BuildingData, TView>();
	private readonly List<BuildingData> _stale = new List<BuildingData>();
	private readonly Func<BuildingData, TView> _create;
	private readonly Action<TView> _destroy;

	public PerBuildingViews(Func<BuildingData, TView> create, Action<TView> destroy)
	{
		_create = create;
		_destroy = destroy;
	}

	public bool TryGet(BuildingData b, out TView view) => _views.TryGetValue(b, out view);

	public TView GetOrCreate(BuildingData b)
	{
		if (!_views.TryGetValue(b, out TView view))
		{
			view = _create(b);
			_views[b] = view;
		}
		return view;
	}

	// alive에 없는 시설의 표시를 파괴한다.
	public void DestroyStale(List<BuildingData> alive)
	{
		_stale.Clear();
		foreach (var kv in _views)
			if (!alive.Contains(kv.Key)) _stale.Add(kv.Key);
		foreach (BuildingData b in _stale)
		{
			_destroy(_views[b]);
			_views.Remove(b);
		}
	}
}

// 월드 막대·사각형 표시가 공유하는 1×1 흰 스프라이트 - 인스턴스마다 텍스처를 새로 만들지 않는다.
public static class WorldWhiteSprite
{
	private static Sprite _sprite;

	public static Sprite Get()
	{
		if (_sprite != null) return _sprite;
		Texture2D tex = Texture2D.whiteTexture;
		_sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
		return _sprite;
	}
}
