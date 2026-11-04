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
	const int CrownSortingOrder = 123; // 배경(121)·글자(122)보다 위
	const float CrownScale = 0.42f;    // 왕관 너비(16px 스프라이트 = 1단위이므로 곧 월드 너비) — 리더 명령 배율이 곱해진다
	const float CrownOverlap = 0.04f;  // 테두리 윗변에 살짝 걸쳐 "붙어 있게" 한다

	// 리더 명령 말풍선 전용 꾸밈: 노란 테두리 + 왕관 + 한 단계 더 큰 크기(글자·여백·테두리·왕관이 함께 커진다).
	const float LeaderScale = 1.3f;
	const float BorderThickness = 0.035f;
	static readonly Color BorderColor = new Color(1f, 0.82f, 0.16f);

	TextMesh _text;
	MeshRenderer _textRenderer;
	SpriteRenderer _bg;
	SpriteRenderer _crown;             // 리더 명령 말풍선에만 켜진다
	SpriteRenderer[] _border;          // 위·아래·왼쪽·오른쪽 띠 4장 — 배경이 반투명이라 통짜 판 대신 띠로 둘러 안쪽이 노랗게 비치지 않게 한다
	Color _baseColor = Color.white;
	float _elapsed, _duration, _styleScale = 1f;
	int _priority, _layoutFrames;
	bool _showing, _hidden, _bgReady, _leaderOrder;

	static Sprite _sharedBgSprite;
	static Sprite _sharedCrownSprite;

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

		_border = new SpriteRenderer[4];
		for (int i = 0; i < _border.Length; i++)
		{
			var barGo = new GameObject("Border" + i);
			barGo.transform.SetParent(transform, false);
			_border[i] = barGo.AddComponent<SpriteRenderer>();
			_border[i].sprite = _sharedBgSprite;
			_border[i].sortingOrder = BgSortingOrder;
		}

		var crownGo = new GameObject("Crown");
		crownGo.transform.SetParent(transform, false);
		_crown = crownGo.AddComponent<SpriteRenderer>();
		_crown.sprite = _sharedCrownSprite ??= CreateCrownSprite();
		_crown.sortingOrder = CrownSortingOrder;

		ApplyVisibility();
	}

	// 새 대사를 띄운다. 이미 떠 있는 더 높은 순위의 말풍선이 있으면 덮어쓰지 않고 false. leaderOrder면(리더의 명령) 노란 테두리·왕관을 붙이고 한 단계 크게 띄운다.
	public bool Show(string text, SpeechTone tone, float seconds, int priority, bool leaderOrder = false)
	{
		if (!SpeechMath.CanReplace(_showing, _priority, priority)) return false;

		_showing = true;
		_priority = priority;
		_elapsed = 0f;
		_duration = Mathf.Max(0.5f, seconds);
		_layoutFrames = LayoutFrames;
		_bgReady = false;
		_leaderOrder = leaderOrder;
		_styleScale = leaderOrder ? LeaderScale : 1f;
		_crown.transform.localScale = new Vector3(CrownScale * _styleScale, CrownScale * _styleScale, 1f);

		_baseColor = ColorOf(tone);
		_text.text = SpeechMath.Wrap(text);
		_text.characterSize = SizeOf(tone) * _styleScale;
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
		_crown.color = new Color(1f, 1f, 1f, a);
		var border = new Color(BorderColor.r, BorderColor.g, BorderColor.b, a);
		foreach (var bar in _border) bar.color = border;
	}

	void LayoutBackground()
	{
		Bounds b = _textRenderer.bounds;
		if (b.size.x <= 0.001f) return;
		Vector3 lossy = transform.lossyScale;
		if (lossy.x == 0f || lossy.y == 0f) return;

		Vector3 center = transform.InverseTransformPoint(b.center);
		float w = b.size.x / lossy.x + PadX * _styleScale;
		float h = b.size.y / lossy.y + PadY * _styleScale;
		_bg.transform.localPosition = new Vector3(center.x, center.y, 0.01f);
		_bg.transform.localScale = new Vector3(w, h, 1f);

		// 노란 테두리 — 배경 바깥으로 두께 t만큼 네 변에 띠를 두른다(왼쪽·오른쪽 띠는 배경 높이만큼, 위·아래 띠는 모서리까지 덮도록 양쪽으로 t씩 더 길다).
		float t = BorderThickness * _styleScale;
		SetBar(_border[0], center.x, center.y + h * 0.5f + t * 0.5f, w + 2f * t, t); // 위
		SetBar(_border[1], center.x, center.y - h * 0.5f - t * 0.5f, w + 2f * t, t); // 아래
		SetBar(_border[2], center.x - w * 0.5f - t * 0.5f, center.y, t, h);          // 왼쪽
		SetBar(_border[3], center.x + w * 0.5f + t * 0.5f, center.y, t, h);          // 오른쪽

		// 왕관은 테두리 윗변 한가운데에 밑면을 맞춘다(스프라이트 피벗이 밑 중앙).
		float top = center.y + h * 0.5f + t;
		_crown.transform.localPosition = new Vector3(center.x, top - CrownOverlap, 0.02f);
	}

	static void SetBar(SpriteRenderer bar, float x, float y, float width, float height)
	{
		bar.transform.localPosition = new Vector3(x, y, 0.01f);
		bar.transform.localScale = new Vector3(width, height, 1f);
	}

	void ApplyVisibility()
	{
		bool visible = _showing && !_hidden;
		if (_textRenderer != null) _textRenderer.enabled = visible;
		if (_bg != null) _bg.enabled = visible && _bgReady;
		bool decorated = visible && _bgReady && _leaderOrder; // 테두리·왕관은 위치가 잡힌(배경 레이아웃이 끝난) 뒤에만, 리더 명령일 때만 보인다
		if (_crown != null) _crown.enabled = decorated;
		if (_border != null) foreach (var bar in _border) if (bar != null) bar.enabled = decorated;
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

	// 코드로 그린 16×12 픽셀 왕관(전용 아트가 생기면 이 함수만 교체) — 금색 면 + 어두운 외곽선 + 중앙 붉은 보석. 봉우리 3개(가운데가 한 칸 높음)는 좌우 대칭이다.
	static Sprite CreateCrownSprite()
	{
		const int W = 16, H = 12;
		var fill = new bool[W, H];
		for (int y = 1; y <= 4; y++)                      // 밑단 띠
			for (int x = 1; x <= 14; x++) fill[x, y] = true;
		for (int y = 5; y <= 10; y++)                     // 봉우리: 끝에서 아래로 갈수록 0.6칸씩 넓어지는 삼각형
			for (int x = 1; x <= 14; x++)
			{
				float cx = x + 0.5f;
				if (InSpike(cx, y, 2.5f, 9, 0.5f) || InSpike(cx, y, 8f, 10, 1f) || InSpike(cx, y, 13.5f, 9, 0.5f)) fill[x, y] = true;
			}

		var gold = new Color(1f, 0.80f, 0.18f);
		var goldLight = new Color(1f, 0.93f, 0.52f);
		var goldDark = new Color(0.82f, 0.58f, 0.08f);
		var gem = new Color(0.86f, 0.16f, 0.20f);
		var outline = new Color(0.26f, 0.15f, 0.03f);

		var pixels = new Color[W * H];                    // 기본값 (0,0,0,0) = 투명
		for (int y = 0; y < H; y++)
			for (int x = 0; x < W; x++)
			{
				Color c;
				if (fill[x, y])
				{
					c = gold;
					if (y == 4) c = goldLight;             // 띠 윗줄은 밝게
					else if (y == 1) c = goldDark;         // 띠 아랫줄은 어둡게
					if ((x == 7 || x == 8) && (y == 2 || y == 3)) c = gem;
				}
				else if (HasFilledNeighbor(fill, x, y, W, H)) c = outline;
				else continue;
				pixels[y * W + x] = c;
			}

		var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
		tex.SetPixels(pixels);
		tex.Apply();
		return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), W); // 피벗 밑 중앙, 16px = 1단위
	}

	static bool InSpike(float cx, int y, float centerX, int tipY, float tipHalfWidth)
		=> y <= tipY && Mathf.Abs(cx - centerX) <= tipHalfWidth + (tipY - y) * 0.6f;

	static bool HasFilledNeighbor(bool[,] fill, int x, int y, int w, int h)
		=> (x > 0 && fill[x - 1, y]) || (x < w - 1 && fill[x + 1, y]) || (y > 0 && fill[x, y - 1]) || (y < h - 1 && fill[x, y + 1]);

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
