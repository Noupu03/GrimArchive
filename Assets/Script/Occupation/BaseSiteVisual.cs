using System.Collections.Generic;
using UnityEngine;

// 거점 공사 현장의 월드 연출 한 벌 — 바닥 청사진(점선 테두리 + 격자, 진행도만큼 아래에서 위로 채워짐), 방 안 건설물(진행도 25%마다 한 단씩 올라감),
// 일꾼의 먼지·망치 불꽃, 완공 연출을 맡는다. 오브젝트 로직(InteractableObject)이 없는 순수 비주얼이라 이동·조사·인지에 영향을 주지 않는다.
// 스프라이트는 전용 아트가 생기기 전까지 쓰는 자리표시자(코드로 그린 픽셀)이고 종류별로 한 번만 만들어 모든 현장이 공유한다(텍스처 누수 방지).
public sealed class BaseSiteVisual : MonoBehaviour
{
	private const int PixelsPerTile = 16;
	private const int BlueprintOrder = 3;   // 바닥 장식 — 방 윤곽선(1) 위, 오브젝트(5) 아래
	private const int FillOrder = 4;
	private const int StructureOrder = 5;   // 유닛(10)보다 아래라 일꾼이 건설물 앞을 지나도 가리지 않는다
	private const int DustOrder = 9;        // 일꾼 발치 먼지는 유닛 스프라이트 바로 아래
	private const int SparkOrder = 12;      // 망치 불꽃은 항상 보이게
	private const int MaxParticles = 48;
	private const float StagePopSeconds = 0.28f;
	private const float CompletionSeconds = 0.7f;
	private const float SettleSeconds = 0.6f;
	private const float GroundInset = 0.06f; // 건설물 바닥이 타일 아래 끝에서 살짝 떠 있는 정도

	private sealed class Particle
	{
		public SpriteRenderer Renderer;
		public Vector3 Velocity;
		public Color Color;
		public float Age;
		public float Life;
		public float StartScale;
		public float EndScale;
		public bool Active;
	}

	private static readonly Color BasicTint = new Color(0.45f, 0.75f, 1f, 1f);
	private static readonly Color DefenseTint = new Color(1f, 0.65f, 0.25f, 1f);
	private static readonly Color DustColor = new Color(0.78f, 0.7f, 0.56f, 0.75f);

	private static Sprite _fillSprite;
	private static Sprite _dotSprite;
	private static readonly Dictionary<int, Sprite> BlueprintSprites = new Dictionary<int, Sprite>();
	private static readonly Sprite[,] StructureSprites = new Sprite[2, BaseSiteMath.StructureStages];

	private SiteLayout _layout;
	private bool _defense;
	private Color _accent;
	private SpriteRenderer _blueprint;
	private SpriteRenderer _fill;
	private SpriteRenderer _structure;
	private readonly List<Particle> _particles = new List<Particle>();
	private float _progress;
	private int _stage = -1;
	private float _popT = 1f;
	private Phase _phase = Phase.Building;
	private float _phaseT;

	// Building = 공사 중, Completing = 번쩍이며 사라지는 중(끝나면 스스로 파괴), Settling = 청사진만 걷히며 건설물이 상징물로 자리 잡는 중, Emblem = 완공된 상징물(건설물 한 채만 남음).
	private enum Phase { Building, Completing, Settling, Emblem }

	public SiteLayout Layout => _layout;

	// tileCenterWorld = 현장 중심 타일의 월드 중심(층 오프셋 포함). 영역은 이 점을 기준으로 펼쳐진다.
	public static BaseSiteVisual Create(Transform parent, Vector3 tileCenterWorld, SiteLayout layout, bool defense)
	{
		var go = new GameObject($"BaseSite_{layout.Center.x}_{layout.Center.y}{(defense ? "_defense" : string.Empty)}");
		if (parent != null) go.transform.SetParent(parent);
		go.transform.position = tileCenterWorld;
		var visual = go.AddComponent<BaseSiteVisual>();
		visual.Init(layout, defense);
		return visual;
	}

	private void Init(SiteLayout layout, bool defense)
	{
		_layout = layout;
		_defense = defense;
		_accent = defense ? DefenseTint : BasicTint;

		float size = layout.Size;
		_blueprint = CreateChild("Blueprint", BlueprintOrder, GetBlueprintSprite(layout.Radius), Vector3.zero);
		_blueprint.color = _accent;

		_fill = CreateChild("BlueprintFill", FillOrder, GetFillSprite(), new Vector3(0f, -size / 2f, 0f));
		_fill.color = new Color(_accent.r, _accent.g, _accent.b, 0.28f);
		_fill.transform.localScale = new Vector3(size, 0f, 1f);

		_structure = CreateChild("Structure", StructureOrder, null, new Vector3(0f, -0.5f + GroundInset, 0f));
		SetStage(0, pop: false);
	}

	private SpriteRenderer CreateChild(string childName, int sortingOrder, Sprite sprite, Vector3 localPosition)
	{
		var go = new GameObject(childName);
		go.transform.SetParent(transform, false);
		go.transform.localPosition = localPosition;
		var sr = go.AddComponent<SpriteRenderer>();
		sr.sprite = sprite;
		sr.sortingOrder = sortingOrder;
		return sr;
	}

	public void SetVisible(bool visible)
	{
		if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
	}

	// 진행도 0~1 — 청사진이 아래에서 위로 채워지고, 건설물은 단이 바뀔 때 한 단 더 올라간다.
	public void SetProgress(float progress01)
	{
		if (_phase != Phase.Building) return;
		_progress = Mathf.Clamp01(progress01);
		_fill.transform.localScale = new Vector3(_layout.Size, _layout.Size * _progress, 1f);
		int stage = BaseSiteMath.StructureStage(_progress);
		if (stage != _stage) SetStage(stage, pop: _stage >= 0);
	}

	private void SetStage(int stage, bool pop)
	{
		_stage = stage;
		_structure.sprite = GetStructureSprite(_defense, stage);
		if (!pop) return;
		_popT = 0f;
		EmitSparks(5, 0.9f, 0.3f);
	}

	// 일꾼 발치에서 먼지가 피어오른다 — 현장 쪽으로 살짝 쏠린다.
	public void EmitWorkerDust(Vector3 workerWorld)
	{
		Vector3 toward = transform.position - workerWorld;
		toward.z = 0f;
		toward = toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector3.zero;
		for (int i = 0; i < 3; i++)
		{
			var velocity = toward * Random.Range(0.1f, 0.35f) + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.25f, 0.55f), 0f);
			Emit(workerWorld + new Vector3(Random.Range(-0.15f, 0.15f), -0.25f, 0f), velocity, DustColor, Random.Range(0.5f, 0.85f), 0.14f, 0.34f, DustOrder);
		}
	}

	// 건설물 몸체 위에서 망치질 불꽃이 튄다.
	public void EmitHammerSpark() => EmitSparks(Random.Range(3, 5), 1.6f, 0.2f);

	private void EmitSparks(int count, float speed, float spread)
	{
		Vector3 origin = transform.position + new Vector3(Random.Range(-0.3f, 0.3f), -0.1f + Random.Range(0f, 0.5f), 0f);
		Color spark = Color.Lerp(_accent, new Color(1f, 0.95f, 0.6f, 1f), 0.65f);
		for (int i = 0; i < count; i++)
		{
			float angle = Random.Range(0f, Mathf.PI * 2f);
			var velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) + 0.4f, 0f) * Random.Range(speed * 0.5f, speed);
			Emit(origin + new Vector3(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f), velocity, spark, Random.Range(0.25f, 0.45f), 0.16f, 0.03f, SparkOrder);
		}
	}

	private void Emit(Vector3 position, Vector3 velocity, Color color, float life, float startScale, float endScale, int sortingOrder)
	{
		Particle p = null;
		foreach (var candidate in _particles)
			if (!candidate.Active) { p = candidate; break; }
		if (p == null)
		{
			if (_particles.Count >= MaxParticles) return; // 일꾼이 많아도 화면이 터지지 않게 상한
			var go = new GameObject("Particle");
			go.transform.SetParent(transform, false);
			var sr = go.AddComponent<SpriteRenderer>();
			sr.sprite = GetDotSprite();
			p = new Particle { Renderer = sr };
			_particles.Add(p);
		}
		p.Active = true;
		p.Age = 0f;
		p.Life = life;
		p.Velocity = velocity;
		p.Color = color;
		p.StartScale = startScale;
		p.EndScale = endScale;
		p.Renderer.sortingOrder = sortingOrder;
		p.Renderer.transform.position = position;
		p.Renderer.transform.localScale = Vector3.one * startScale;
		p.Renderer.color = color;
		p.Renderer.enabled = true;
	}

	// 현장을 걷는다. celebrate면 완공 연출(건설물이 번쩍이며 사라지고 불꽃이 터진다)을 보여 주고 스스로 사라진다.
	public void Dismiss(bool celebrate)
	{
		if (!celebrate) { Destroy(gameObject); return; }
		if (_phase == Phase.Completing) return;
		_phase = Phase.Completing;
		_phaseT = 0f;
		_fill.transform.localScale = new Vector3(_layout.Size, _layout.Size, 1f);
		EmitSparks(14, 2.4f, 0.35f);
	}

	// 완공한 현장을 상징물로 남긴다 — 청사진과 채움은 걷히고 건설물(마지막 단계)만 그 자리에 계속 서 있다. animate면 튀어 오르며 자리 잡고 불꽃이 터진다.
	// 안개 속처럼 보이지 않는 곳(비활성)에서는 연출 없이 곧바로 상징물 상태가 된다 — Update가 돌지 않아 연출이 끝나지 않기 때문이다.
	public void Settle(bool animate)
	{
		if (_phase == Phase.Settling || _phase == Phase.Emblem) return;
		_phase = Phase.Settling;
		_phaseT = 0f;
		_progress = 1f;
		_fill.transform.localScale = new Vector3(_layout.Size, _layout.Size, 1f);
		if (_stage != BaseSiteMath.StructureStages - 1) SetStage(BaseSiteMath.StructureStages - 1, pop: false);
		if (animate && gameObject.activeInHierarchy) EmitSparks(14, 2.4f, 0.35f);
		else FinishSettle();
	}

	private void FinishSettle()
	{
		_phase = Phase.Emblem;
		_blueprint.gameObject.SetActive(false);
		_fill.gameObject.SetActive(false);
		_structure.transform.localScale = Vector3.one;
		_structure.color = Color.white;
	}

	// 상징물을 다른 칸으로 옮긴다(원래 칸이 건물·오브젝트에 막혔을 때). tileCenterWorld = 새 칸의 월드 중심.
	public void MoveTo(Vector3 tileCenterWorld) => transform.position = tileCenterWorld;

	// 방어 시설 오브젝트(GameSession.SpawnObject가 전리품 스프라이트를 주황으로 칠해 만든 자리표시자)의 비주얼을 망루로 바꾼다 — 기초 상징물이 천막이듯 방어 거점의 상징물은 오브젝트 자신이다.
	// 건설물 스프라이트는 밑변 중앙이 기준점이라 오브젝트 루트(타일 중심)를 타일 아래쪽으로 내려 현장 건설물과 같은 자리에 앉힌다.
	public static void ApplyDefenseEmblem(GameObject objectVisual)
	{
		if (objectVisual == null) return;
		var sr = objectVisual.GetComponent<SpriteRenderer>();
		if (sr == null) return;
		sr.sprite = GetStructureSprite(true, BaseSiteMath.StructureStages - 1);
		sr.color = Color.white;
		objectVisual.transform.position += new Vector3(0f, -0.5f + GroundInset, 0f);
	}

	private void Update()
	{
		float dt = Time.deltaTime;
		switch (_phase)
		{
			case Phase.Building: UpdateIdle(dt); break;
			case Phase.Completing: UpdateCompletion(dt); break;
			case Phase.Settling: UpdateSettling(dt); break;
		}
		UpdateParticles(dt);
	}

	private void UpdateSettling(float dt)
	{
		_phaseT += dt;
		float t = Mathf.Clamp01(_phaseT / SettleSeconds);
		float eased = 1f - (1f - t) * (1f - t);
		_structure.transform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, eased);
		_structure.color = Color.white;
		_blueprint.color = new Color(_accent.r, _accent.g, _accent.b, (1f - t) * 0.9f);
		_fill.color = new Color(_accent.r, _accent.g, _accent.b, (1f - t) * 0.28f);
		if (t >= 1f) FinishSettle();
	}

	private void UpdateIdle(float dt)
	{
		if (_popT < 1f) _popT = Mathf.Min(1f, _popT + dt / StagePopSeconds);
		_structure.transform.localScale = Vector3.one * BaseSiteMath.PopScale(_popT);
		var c = Color.white;
		c.a = BaseSiteMath.StructureAlpha(_progress);
		_structure.color = c;

		var b = _accent;
		b.a = 0.75f + 0.2f * Mathf.Sin(Time.time * 2.2f);
		_blueprint.color = b;
	}

	private void UpdateCompletion(float dt)
	{
		_phaseT += dt;
		float t = Mathf.Clamp01(_phaseT / CompletionSeconds);
		_structure.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.3f, t);
		_structure.color = new Color(1f, 1f, 1f, 1f - t);
		_blueprint.color = new Color(_accent.r, _accent.g, _accent.b, (1f - t) * 0.9f);
		_fill.color = new Color(_accent.r, _accent.g, _accent.b, (1f - t) * 0.28f);
		if (t >= 1f) Destroy(gameObject);
	}

	private void UpdateParticles(float dt)
	{
		foreach (var p in _particles)
		{
			if (!p.Active) continue;
			p.Age += dt;
			if (p.Age >= p.Life)
			{
				p.Active = false;
				p.Renderer.enabled = false;
				continue;
			}
			float t = p.Age / p.Life;
			p.Renderer.transform.position += p.Velocity * dt;
			p.Renderer.transform.localScale = Vector3.one * Mathf.Lerp(p.StartScale, p.EndScale, t);
			var c = p.Color;
			c.a *= 1f - t;
			p.Renderer.color = c;
		}
	}

	// ── 자리표시자 스프라이트 ──

	private static Sprite MakeSprite(int width, int height, Color32[] pixels, Vector2 pivot, float pixelsPerUnit)
	{
		var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
		texture.SetPixels32(pixels);
		texture.Apply();
		return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, pixelsPerUnit);
	}

	// 밑변 중앙이 기준점이고 가로 1·세로 1(월드 단위)인 흰 사각형 — 스케일로 크기를 조절하는 채움용.
	private static Sprite GetFillSprite()
	{
		if (_fillSprite != null) return _fillSprite;
		var pixels = new Color32[16];
		for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
		return _fillSprite = MakeSprite(4, 4, pixels, new Vector2(0.5f, 0f), 4f);
	}

	// 작은 원 — 먼지·불꽃 입자. 지름이 월드 단위 1이라 스케일 값이 곧 입자의 지름(타일 한 칸 = 1)이다.
	private static Sprite GetDotSprite()
	{
		if (_dotSprite != null) return _dotSprite;
		const int size = 8;
		var pixels = new Color32[size * size];
		for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
			{
				float dx = x - 3.5f, dy = y - 3.5f;
				pixels[y * size + x] = dx * dx + dy * dy <= 12.25f ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
			}
		return _dotSprite = MakeSprite(size, size, pixels, new Vector2(0.5f, 0.5f), size);
	}

	// 청사진 — 가장자리 점선(모서리는 실선) + 타일 경계 격자 + 옅은 바탕. 색은 SpriteRenderer.color로 입힌다.
	private static Sprite GetBlueprintSprite(int radius)
	{
		if (BlueprintSprites.TryGetValue(radius, out var cached) && cached != null) return cached;

		int size = (radius * 2 + 1) * PixelsPerTile;
		var pixels = new Color32[size * size];
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				bool horizontalEdge = y == 0 || y == size - 1;
				bool verticalEdge = x == 0 || x == size - 1;
				byte alpha = 22; // 바탕
				if (horizontalEdge || verticalEdge)
				{
					bool corner = (x < 4 || x >= size - 4) && (y < 4 || y >= size - 4);
					bool dash = horizontalEdge ? (x / 3) % 2 == 0 : (y / 3) % 2 == 0;
					alpha = (byte)(corner || dash ? 255 : 0);
				}
				else if (x % PixelsPerTile == 0 || y % PixelsPerTile == 0)
				{
					alpha = 90; // 타일 경계 격자
				}
				pixels[y * size + x] = new Color32(255, 255, 255, alpha);
			}
		}
		return BlueprintSprites[radius] = MakeSprite(size, size, pixels, new Vector2(0.5f, 0.5f), PixelsPerTile);
	}

	// 건설물 — 단계가 오를수록 아래에서부터 더 높이 그려진다(발판 기둥이 그 위로 삐져나와 짓는 중임을 보여 준다). 16×16, 밑변 중앙이 기준점.
	private static Sprite GetStructureSprite(bool defense, int stage)
	{
		int kind = defense ? 1 : 0;
		stage = Mathf.Clamp(stage, 0, BaseSiteMath.StructureStages - 1);
		if (StructureSprites[kind, stage] != null) return StructureSprites[kind, stage];

		const int size = 16;
		int revealed = (stage + 1) * (size / BaseSiteMath.StructureStages);
		var scaffold = new Color32(120, 84, 48, 255);
		var pixels = new Color32[size * size];
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				Color32? px = y < revealed ? (defense ? DefensePixel(x, y) : TentPixel(x, y)) : (Color32?)null;
				if (px == null && stage < BaseSiteMath.StructureStages - 1 && (x == 0 || x == size - 1) && y < revealed + 3) px = scaffold;
				pixels[y * size + x] = px ?? new Color32(0, 0, 0, 0);
			}
		}
		return StructureSprites[kind, stage] = MakeSprite(size, size, pixels, new Vector2(0.5f, 0f), PixelsPerTile);
	}

	// 기초 시설 — 깃발을 단 천막.
	private static Color32? TentPixel(int x, int y)
	{
		var cloth = new Color32(222, 210, 178, 255);
		var trim = new Color32(110, 88, 60, 255);
		var dark = new Color32(52, 40, 30, 255);
		var accent = new Color32(64, 140, 255, 255);

		if (y >= 1 && y <= 10)
		{
			float half = (10.5f - y) * 0.62f + 0.8f;
			float dx = Mathf.Abs(x - 7.5f);
			if (dx <= half)
			{
				if (y <= 5 && dx <= 1.6f) return dark;
				return dx > half - 1f ? trim : cloth;
			}
		}
		if (x == 8 && y >= 11 && y <= 15) return trim;
		if (y >= 12 && y <= 14 && x >= 9 && x <= 12 - (y == 13 ? 0 : 1)) return accent;
		return null;
	}

	// 방어 시설 — 깃발을 단 석조 망루.
	private static Color32? DefensePixel(int x, int y)
	{
		var stone = new Color32(158, 158, 166, 255);
		var mortar = new Color32(118, 118, 128, 255);
		var trim = new Color32(78, 78, 90, 255);
		var dark = new Color32(40, 36, 36, 255);
		var accent = new Color32(255, 160, 56, 255);

		if (y >= 1 && y <= 9 && x >= 3 && x <= 12)
		{
			if (y <= 5 && x >= 6 && x <= 9) return dark;
			if (x == 3 || x == 12 || y == 1) return trim;
			bool brick = y % 3 == 0 || (x + (y / 3) % 2 * 2) % 4 == 0;
			return brick ? mortar : stone;
		}
		if (y >= 10 && y <= 11 && x >= 3 && x <= 12 && (x - 3) % 4 <= 1) return trim;
		if (x == 8 && y >= 12 && y <= 15) return trim;
		if (y >= 13 && y <= 15 && x >= 9 && x <= 12 - (y == 14 ? 0 : 1)) return accent;
		return null;
	}
}
