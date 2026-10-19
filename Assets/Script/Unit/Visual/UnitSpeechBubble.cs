using UnityEngine;

// 유닛 머리 위 말풍선 — UnitVisual이 처음 필요할 때 만들어 들고 있는 월드 공간 TextMesh + 배경 스프라이트. 상태 라벨·체력바와 같은 방식으로 부모 localScale을 역산해 유닛을 따라다닌다.
// 표시·순위 덮어쓰기·페이드·안개 숨김만 맡고, 무엇을 언제 말할지는 SpeechSystem이 정한다.
public class UnitSpeechBubble : MonoBehaviour
{
	const int BgSortingOrder = 121;    // 체력바(101)보다 위, 위협타일(999)·안개(1000~)보다 아래
	const int TextSortingOrder = 122;
	const float OffsetAboveTop = 1.0f; // 유닛 스프라이트 상단 기준 — 체력바(0.2)·상태 라벨(0.55) 위
	const float FadeSeconds = 0.4f;
	const float PadX = 0.28f, PadY = 0.12f;
	const float BgAlpha = 0.72f;
	const int LayoutFrames = 3;        // TextMesh 경계가 갱신되길 기다리며 배경 크기를 맞추는 프레임 수

	TextMesh _text;
	MeshRenderer _textRenderer;
	SpriteRenderer _bg;
	Color _baseColor = Color.white;
	float _elapsed, _duration;
	int _priority, _layoutFrames;
	bool _showing, _hidden, _bgReady;

	static Sprite _sharedBgSprite;

	public static UnitSpeechBubble Create(Transform parent, Vector2 footprint)
	{
		if (footprint.x <= 0f || footprint.y <= 0f) footprint = Vector2.one;

		var root = new GameObject("SpeechBubble");
		root.transform.SetParent(parent, false);
		Vector3 ps = parent.localScale;
		float invX = ps.x != 0f ? 1f / ps.x : 1f;
		float invY = ps.y != 0f ? 1f / ps.y : 1f;
		root.transform.localScale = new Vector3(invX, invY, 1f);
		root.transform.localPosition = new Vector3(0f, (footprint.y + OffsetAboveTop) * invY, 0f);

		var bubble = root.AddComponent<UnitSpeechBubble>();
		bubble.Build();
		return bubble;
	}

	void Build()
	{
		var bgGo = new GameObject("Bg");
		bgGo.transform.SetParent(transform, false);
		_bg = bgGo.AddComponent<SpriteRenderer>();
		_bg.sprite = _sharedBgSprite ??= CreateWhiteSprite();
		_bg.sortingOrder = BgSortingOrder;

		var textGo = new GameObject("Text");
		textGo.transform.SetParent(transform, false);
		_text = textGo.AddComponent<TextMesh>();
		_text.fontSize = 48;
		_text.anchor = TextAnchor.MiddleCenter;
		_text.alignment = TextAlignment.Center;
		_textRenderer = textGo.GetComponent<MeshRenderer>();
		_textRenderer.sortingOrder = TextSortingOrder;

		ApplyVisibility();
	}

	// 새 대사를 띄운다. 이미 떠 있는 더 높은 순위의 말풍선이 있으면 덮어쓰지 않고 false.
	public bool Show(string text, SpeechTone tone, float seconds, int priority)
	{
		if (!SpeechMath.CanReplace(_showing, _priority, priority)) return false;

		_showing = true;
		_priority = priority;
		_elapsed = 0f;
		_duration = Mathf.Max(0.5f, seconds);
		_layoutFrames = LayoutFrames;
		_bgReady = false;

		_baseColor = ColorOf(tone);
		_text.text = SpeechMath.Wrap(text);
		_text.characterSize = SizeOf(tone);
		_text.color = _baseColor;
		ApplyVisibility();
		return true;
	}

	public void Clear()
	{
		_showing = false;
		_priority = 0;
		ApplyVisibility();
	}

	// 안개에 가려진 방의 유닛이면 숨긴다(체력바와 같은 기준) — 시간은 계속 흐른다.
	public void SetHidden(bool hidden)
	{
		if (_hidden == hidden) return;
		_hidden = hidden;
		ApplyVisibility();
	}

	void LateUpdate()
	{
		if (!_showing) return;

		_elapsed += Time.deltaTime;
		if (_elapsed >= _duration) { Clear(); return; }

		if (_layoutFrames > 0)
		{
			LayoutBackground();
			if (--_layoutFrames == 0) { _bgReady = true; ApplyVisibility(); }
		}

		float a = SpeechMath.FadeAlpha(_elapsed, _duration, FadeSeconds);
		_text.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, a);
		_bg.color = new Color(0f, 0f, 0f, BgAlpha * a);
	}

	void LayoutBackground()
	{
		Bounds b = _textRenderer.bounds;
		if (b.size.x <= 0.001f) return;
		Vector3 s = transform.lossyScale;
		if (s.x == 0f || s.y == 0f) return;

		Vector3 center = transform.InverseTransformPoint(b.center);
		_bg.transform.localPosition = new Vector3(center.x, center.y, 0.01f);
		_bg.transform.localScale = new Vector3(b.size.x / s.x + PadX, b.size.y / s.y + PadY, 1f);
	}

	void ApplyVisibility()
	{
		bool visible = _showing && !_hidden;
		if (_textRenderer != null) _textRenderer.enabled = visible;
		if (_bg != null) _bg.enabled = visible && _bgReady;
	}

	static Color ColorOf(SpeechTone tone)
	{
		switch (tone)
		{
			case SpeechTone.Alert: return new Color(1f, 0.92f, 0.45f);
			case SpeechTone.Fear: return new Color(1f, 0.65f, 0.4f);
			case SpeechTone.Panic: return new Color(1f, 0.4f, 0.4f);
			default: return new Color(0.92f, 0.96f, 1f);
		}
	}

	static float SizeOf(SpeechTone tone) => tone == SpeechTone.Panic ? 0.075f : 0.06f;

	static Sprite CreateWhiteSprite()
	{
		var tex = new Texture2D(4, 4);
		var pixels = new Color[16];
		for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
		tex.SetPixels(pixels);
		tex.Apply();
		return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
	}
}
