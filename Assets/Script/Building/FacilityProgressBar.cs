using UnityEngine;

// 외부 작업(건설·수리·강화·철거·이동) 진행도를 시설 하단에 막대로 보여 준다(건설 명세 2-13). 새 프리팹 없이 코드로 만든다.
// 미완성 건물을 옮기는 중에는 현재 분해·재설치 단계 막대(위)와 원래 건설 진행률 막대(아래, 가는 막대)를 구분한다.
public sealed class FacilityProgressBar
{
	private static Sprite _sprite;

	private readonly string _name;
	private GameObject _root;
	private SpriteRenderer _bg, _fill, _bg2, _fill2;

	public FacilityProgressBar(string name) { _name = name; }

	private static Sprite WhiteSprite()
	{
		if (_sprite != null) return _sprite;
		Texture2D tex = Texture2D.whiteTexture;
		_sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
		return _sprite;
	}

	private SpriteRenderer MakeRenderer(string childName, int order)
	{
		var go = new GameObject(childName);
		go.transform.SetParent(_root.transform, false);
		var sr = go.AddComponent<SpriteRenderer>();
		sr.sprite = WhiteSprite();
		sr.sortingOrder = order;
		return sr;
	}

	// 막대 왼쪽 정렬: 칸 단위 폭 width 안에서 progress만큼 채운다. 중심 좌표는 막대 전체의 가운데다.
	private static void Place(SpriteRenderer bg, SpriteRenderer fill, Vector3 center, float width, float height, float progress, Color fillColor)
	{
		bg.gameObject.SetActive(true);
		fill.gameObject.SetActive(true);
		bg.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
		bg.transform.position = center;
		bg.transform.localScale = new Vector3(width, height, 1f);

		float p = Mathf.Clamp01(progress);
		float fw = Mathf.Max(0.0001f, (width - 0.06f) * p);
		fill.color = fillColor;
		fill.transform.position = new Vector3(center.x - (width - 0.06f) * 0.5f + fw * 0.5f, center.y, center.z);
		fill.transform.localScale = new Vector3(fw, Mathf.Max(0.02f, height - 0.05f), 1f);
	}

	public void Show(Vector3 center, float width, float progress, Color fillColor, float originalConstruction = -1f)
	{
		if (_root == null)
		{
			_root = new GameObject(_name);
			_bg = MakeRenderer("Bg", 60);
			_fill = MakeRenderer("Fill", 61);
			_bg2 = MakeRenderer("Bg2", 60);
			_fill2 = MakeRenderer("Fill2", 61);
		}
		if (!_root.activeSelf) _root.SetActive(true);

		Place(_bg, _fill, center, width, 0.17f, progress, fillColor);
		if (originalConstruction >= 0f)
			Place(_bg2, _fill2, new Vector3(center.x, center.y - 0.16f, center.z), width, 0.1f, originalConstruction, new Color(0.95f, 0.8f, 0.3f, 1f));
		else
		{
			_bg2.gameObject.SetActive(false);
			_fill2.gameObject.SetActive(false);
		}
	}

	public void Hide()
	{
		if (_root != null && _root.activeSelf) _root.SetActive(false);
	}

	public void Destroy()
	{
		if (_root != null) Object.Destroy(_root);
		_root = null;
	}
}
