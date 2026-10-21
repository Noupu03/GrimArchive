using UnityEngine;

// 코어 완전 활성화 연출 — 코어 위치에서 링 여러 개가 시차를 두고 방 끝까지 퍼진다(소리 전파 디버그 링과 같은 "코드로 만든 링 스프라이트를 키우며 페이드"하는 방식).
// 링은 코어 방 사각형으로 잘려(SpriteMask) 벽 너머 이웃 방·안개 방으로 번지지 않는다. 오브젝트 로직이 없는 순수 비주얼이고, 링 스프라이트는 전용 아트가 생기기 전까지 쓰는 자리표시자다.
public sealed class CoreActivationVfx : MonoBehaviour
{
	private const int RingCount = 3;
	private const float RingStaggerSeconds = 0.22f;
	private const float SpreadTilesPerSecond = 9f;  // 방이 커도 퍼지는 속도가 비슷해 보이도록 지속 시간을 반경에 비례시킨다
	private const float MinRingSeconds = 0.9f;
	private const float MaxRingSeconds = 2.2f;
	private const float StartRadius = 0.5f;         // 링이 코어 칸에서 시작하는 반경(타일)
	private const float PeakAlpha = 0.9f;
	private const float RingDimPerIndex = 0.25f;    // 뒤따르는 링일수록 흐려져 선두 링이 도드라진다
	private const float FadeInRatio = 0.1f;         // 링 수명 앞쪽 일부는 서서히 나타나 갑자기 튀어나오지 않게

	private const int TextureSize = 256;
	private const float RingPeak = 0.9f;            // 텍스처 반경 대비 링 중심 위치
	private const float RingHalfWidth = 0.07f;
	private const float TrailAlpha = 0.14f;         // 링 안쪽으로 끌리는 옅은 잔광(파동이 지나간 자리)
	private const float TrailStart = 0.45f;

	private static Sprite _ringSprite;
	private static Sprite _maskSprite;

	private SpriteRenderer[] _rings;
	private Color _tint;
	private float _maxRadius;
	private float _ringSeconds;
	private float _totalSeconds;
	private float _elapsed;

	// centerWorld = 링이 퍼지기 시작하는 월드 좌표(층 오프셋 포함), roomWorldRect = 코어 방의 월드 사각형(링을 자르는 범위).
	public static void Play(Transform parent, Vector3 centerWorld, Rect roomWorldRect, Color tint, int sortingLayerId, int sortingOrder)
	{
		var go = new GameObject("CoreActivationVfx");
		if (parent != null) go.transform.SetParent(parent);
		go.transform.position = centerWorld;
		go.AddComponent<CoreActivationVfx>().Init(roomWorldRect, tint, sortingLayerId, sortingOrder);
	}

	private void Init(Rect roomWorldRect, Color tint, int sortingLayerId, int sortingOrder)
	{
		_tint = tint;

		// 링이 방 안 어디든 닿으려면 코어에서 가장 먼 모서리까지 퍼져야 한다.
		Vector2 center = transform.position;
		float farthest = 0f;
		for (int i = 0; i < 4; i++)
		{
			var corner = new Vector2((i & 1) == 0 ? roomWorldRect.xMin : roomWorldRect.xMax, (i & 2) == 0 ? roomWorldRect.yMin : roomWorldRect.yMax);
			farthest = Mathf.Max(farthest, Vector2.Distance(center, corner));
		}
		_maxRadius = Mathf.Max(farthest, StartRadius + 1f);
		_ringSeconds = Mathf.Clamp(_maxRadius / SpreadTilesPerSecond, MinRingSeconds, MaxRingSeconds);
		_totalSeconds = _ringSeconds + RingStaggerSeconds * (RingCount - 1);

		var maskGo = new GameObject("RoomMask");
		maskGo.transform.SetParent(transform, false);
		maskGo.transform.position = new Vector3(roomWorldRect.center.x, roomWorldRect.center.y, transform.position.z);
		maskGo.transform.localScale = new Vector3(roomWorldRect.width, roomWorldRect.height, 1f);
		maskGo.AddComponent<SpriteMask>().sprite = GetMaskSprite();

		_rings = new SpriteRenderer[RingCount];
		for (int i = 0; i < RingCount; i++)
		{
			var ringGo = new GameObject("Ring" + i);
			ringGo.transform.SetParent(transform, false);
			var sr = ringGo.AddComponent<SpriteRenderer>();
			sr.sprite = GetRingSprite();
			sr.sortingLayerID = sortingLayerId;
			sr.sortingOrder = sortingOrder;
			sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
			sr.enabled = false;
			_rings[i] = sr;
		}
	}

	private void Update()
	{
		_elapsed += Time.deltaTime;
		if (_elapsed >= _totalSeconds)
		{
			Destroy(gameObject);
			return;
		}

		for (int i = 0; i < RingCount; i++)
		{
			float local = _elapsed - RingStaggerSeconds * i;
			SpriteRenderer ring = _rings[i];
			if (local <= 0f || local >= _ringSeconds)
			{
				ring.enabled = false;
				continue;
			}

			float t = local / _ringSeconds;
			float eased = 1f - (1f - t) * (1f - t) * (1f - t); // 처음엔 빠르게 퍼지고 끝으로 갈수록 느려진다
			float radius = Mathf.Lerp(StartRadius, _maxRadius, eased);
			ring.transform.localScale = new Vector3(radius, radius, 1f); // 링 스프라이트의 반지름이 월드 1단위라 스케일 값이 곧 반경(타일)

			float alpha = PeakAlpha * Mathf.Pow(1f - t, 1.5f) * Mathf.Clamp01(t / FadeInRatio) * (1f - RingDimPerIndex * i);
			Color c = Color.Lerp(Color.white, _tint, eased); // 퍼지기 시작할 땐 흰빛, 퍼질수록 코어 색으로
			c.a = alpha;
			ring.color = c;
			ring.enabled = true;
		}
	}

	// ── 자리표시자 스프라이트 ──

	// 반지름이 월드 1단위인 부드러운 링 + 안쪽 잔광 — 스케일 값이 곧 반경이 된다. 두께는 반경에 비례해 커진다.
	private static Sprite GetRingSprite()
	{
		if (_ringSprite != null) return _ringSprite;

		var pixels = new Color32[TextureSize * TextureSize];
		float half = TextureSize / 2f;
		for (int y = 0; y < TextureSize; y++)
		{
			for (int x = 0; x < TextureSize; x++)
			{
				float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
				float line = r <= 1f ? Smooth(1f - Mathf.Abs(r - RingPeak) / RingHalfWidth) : 0f;
				float trail = r < RingPeak ? TrailAlpha * Smooth((r - TrailStart) / (RingPeak - TrailStart)) : 0f;
				pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Max(line, trail) * 255f));
			}
		}

		var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
		texture.SetPixels32(pixels);
		texture.Apply();
		return _ringSprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize / 2f);
	}

	// 가로 1·세로 1(월드 단위)인 흰 사각형 — 스케일로 방 크기에 맞추는 마스크용.
	private static Sprite GetMaskSprite()
	{
		if (_maskSprite != null) return _maskSprite;

		var pixels = new Color32[16];
		for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
		var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
		texture.SetPixels32(pixels);
		texture.Apply();
		return _maskSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
	}

	private static float Smooth(float x)
	{
		x = Mathf.Clamp01(x);
		return x * x * (3f - 2f * x);
	}
}
