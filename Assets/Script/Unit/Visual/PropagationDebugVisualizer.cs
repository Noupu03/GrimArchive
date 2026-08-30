using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer;

// 임시 디버그 시각화 — 07_전파·소리·간접입력 시스템 동작을 눈으로 확인하기 위한 용도. 검증이 끝나면
// 이 파일 + GameCompositionRoot 등록 + GameSession 호출부 세 곳만 지우면 제거된다.
// ThreatTileRenderer와 동일한 컨벤션(Update/인스펙터 없는 순수 C# 클래스, GameSession이 매 프레임
// Render(units) 호출, LineRenderer 대신 SpriteRenderer+기본 머티리얼 — 즉석 머티리얼은 이 URP
// 프로젝트에서 안 보임)을 따른다. 그려주는 것 2가지: (1) 살아있는 인류마다 현재 전파 범위를 옅은
// 하늘색 원 테두리로. (2) OnSoundEmitted(감지 성공 여부와 무관한 발생 사건 자체)를 구독해 소리 발생
// 위치에 종류별 색 원으로 도달 범위를 표시했다가 지운다 — 아무도 못 들었어도 원은 뜬다.
public class PropagationDebugVisualizer
{
	// DebugInfoPanel의 "시야 표시" 토글(UnitGenerate.ShowAllVisionRanges)과 동일 컨벤션 — 기본은 꺼짐,
	// 우측 상단 패널(DrawPropagationToggle)에서 항목별(전파 범위 + 소리 6종)로 켠다.
	public bool ShowPropagationRange = false;
	public bool ShowMovement = false;
	public bool ShowAttackExecution = false;
	public bool ShowHitImpact = false;
	public bool ShowHitScream = false;
	public bool ShowDeath = false;
	public bool ShowTrapActivation = false;

	private bool AnySoundEnabled => ShowMovement || ShowAttackExecution || ShowHitImpact || ShowHitScream || ShowDeath || ShowTrapActivation;
	private bool AnyEnabled => ShowPropagationRange || AnySoundEnabled;

	private bool IsSoundTypeEnabled(SoundType type) => type switch
	{
		SoundType.Movement => ShowMovement,
		SoundType.AttackExecution => ShowAttackExecution,
		SoundType.HitImpact => ShowHitImpact,
		SoundType.HitScream => ShowHitScream,
		SoundType.Death => ShowDeath,
		SoundType.TrapActivation => ShowTrapActivation,
		_ => false,
	};

	private const float DebugFlashSeconds = 1.2f;
	// 링 텍스처 자체의 반지름(스프라이트 로컬 단위) — RingSprite가 이 반지름의 원을 그리도록 만들고,
	// 실제 표시 반지름(타일 수)은 transform.localScale로 맞춘다(scale 1 = 반지름 1칸).
	private const float RingLocalRadius = 1f;

	private static readonly Dictionary<SoundType, Color> SoundColors = new Dictionary<SoundType, Color>
	{
		{ SoundType.Movement, new Color(0.6f, 0.6f, 0.6f) },
		{ SoundType.AttackExecution, new Color(1f, 0.6f, 0f) },
		{ SoundType.HitImpact, new Color(1f, 0.2f, 0.2f) },
		{ SoundType.HitScream, new Color(1f, 0f, 0.6f) },
		{ SoundType.Death, new Color(1f, 1f, 1f) }, // 검정은 어두운 바닥 위에서 안 보일 수 있어 흰색으로
		{ SoundType.TrapActivation, new Color(1f, 1f, 0f) },
	};
	private static readonly Color PropagationRangeColor = new Color(0.2f, 0.8f, 1f, 0.6f);

	private class CircleVisual { public SpriteRenderer Renderer; }

	// 리스트 인덱스로 풀에서 원을 배정받으면, 프레임마다 _recentSounds 구성이 바뀌면서 같은
	// GameObject가 다른 플래시로 순간 재배정되는 버그가 생긴다 — 각 플래시가 발생 즉시 자기 전용
	// GameObject를 만들어 목숨이 다할 때까지 그대로 들고 있는다.
	private class SoundFlash
	{
		public SoundType Type;
		public float StartTime;
		public SpriteRenderer Renderer;
	}

	private static Sprite _ringSprite;

	private readonly Transform _root = new GameObject("PropagationDebugVisualizer(Temp)").transform;
	private readonly Dictionary<Unit, CircleVisual> _rangeVisuals = new Dictionary<Unit, CircleVisual>();
	private readonly HashSet<Unit> _cachedAliveHumans = new HashSet<Unit>();
	private readonly List<Unit> _cachedRemoveList = new List<Unit>();
	private readonly List<SoundFlash> _recentSounds = new List<SoundFlash>();

	private UnitGenerate _unitGenerate;

	[Inject]
	public void Construct(UnitGenerate unitGenerate)
	{
		_unitGenerate = unitGenerate;
		// OnSoundPerceived(감지 성공)가 아니라 OnSoundEmitted(발생 사건 자체)를 구독한다. 소리 종류
		// 토글이 꺼져 있으면 콜백에서 바로 걸러내 GameObject가 무한정 생기는 것을 방지한다.
		PropagationSystem.OnSoundEmitted.Subscribe(e =>
		{
			if (!IsSoundTypeEnabled(e.Type)) return;
			var cv = CreateCircle("Sound_" + e.Type);
			PlaceCircle(cv.Renderer, TileCenter(e.Position, e.FloorIndex), e.RangeTiles);
			_recentSounds.Add(new SoundFlash { Type = e.Type, StartTime = Time.time, Renderer = cv.Renderer });
		});
	}

	public void Render(List<Unit> units)
	{
		if (_root == null || _root.gameObject == null) return;
		_root.gameObject.SetActive(AnyEnabled);
		if (!AnyEnabled) return;

		RenderPropagationRanges(units);
		RenderActiveSounds();
	}

	private void RenderPropagationRanges(List<Unit> units)
	{
		if (!ShowPropagationRange)
		{
			foreach (var kv in _rangeVisuals)
				if (kv.Value.Renderer != null) Object.Destroy(kv.Value.Renderer.gameObject);
			_rangeVisuals.Clear();
			return;
		}

		_cachedAliveHumans.Clear();
		foreach (var u in units)
			if (u is Human human && human.hp > 0) _cachedAliveHumans.Add(u);

		_cachedRemoveList.Clear();
		foreach (var kv in _rangeVisuals)
			if (!_cachedAliveHumans.Contains(kv.Key)) _cachedRemoveList.Add(kv.Key);
		foreach (var u in _cachedRemoveList)
		{
			if (_rangeVisuals[u].Renderer != null) Object.Destroy(_rangeVisuals[u].Renderer.gameObject);
			_rangeVisuals.Remove(u);
		}

		foreach (var u in _cachedAliveHumans)
		{
			Human human = (Human)u;
			if (!_rangeVisuals.TryGetValue(u, out var cv))
			{
				cv = CreateCircle("PropRange_" + human.name);
				_rangeVisuals[u] = cv;
			}
			float radius = PropagationSystem.GetPropagationRange(human);
			PlaceCircle(cv.Renderer, TileCenter(human.position, human.currentFloor), radius);
			cv.Renderer.color = PropagationRangeColor;
		}
	}

	private void RenderActiveSounds()
	{
		// 반경은 고정값이라 표시 시간이 다 되면 그냥 사라진다 — 위치·반경은 발생 시점에 이미 고정해뒀고
		// 여기서는 매 프레임 남은 시간에 따라 투명도만 갱신한다.
		for (int i = _recentSounds.Count - 1; i >= 0; i--)
		{
			var s = _recentSounds[i];
			float remainingRatio = 1f - (Time.time - s.StartTime) / DebugFlashSeconds;
			if (remainingRatio <= 0f)
			{
				if (s.Renderer != null) Object.Destroy(s.Renderer.gameObject);
				_recentSounds.RemoveAt(i);
				continue;
			}

			Color c = SoundColors.TryGetValue(s.Type, out var col) ? col : Color.white;
			c.a = remainingRatio;
			s.Renderer.color = c;
		}
	}

	private CircleVisual CreateCircle(string name)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(_root);
		SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
		sr.sprite = GetOrCreateRingSprite();
		sr.sortingOrder = 998;
		return new CircleVisual { Renderer = sr };
	}

	private static void PlaceCircle(SpriteRenderer sr, Vector3 center, float radius)
	{
		sr.transform.position = center;
		sr.transform.localScale = new Vector3(radius, radius, 1f);
	}

	// 별도 에셋 없이 코드로 원형 링 텍스처를 한 번만 생성해 재사용한다. 머티리얼은 SpriteRenderer의
	// 기본 머티리얼을 그대로 쓴다 — 즉석 Shader.Find 머티리얼은 이 URP 프로젝트에서 안 보인다.
	private static Sprite GetOrCreateRingSprite()
	{
		if (_ringSprite != null) return _ringSprite;

		const int size = 128;
		const float outerR = 0.95f; // 텍스처 반경 기준 바깥 경계
		const float innerR = 0.75f; // 링 두께 = outerR - innerR(꽤 두껍게 — 작은 화면에서도 보이도록)
		var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
		Color[] pixels = new Color[size * size];
		Vector2 center = new Vector2(size / 2f, size / 2f);
		float maxDist = size / 2f;

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / maxDist;
				float alpha = (dist >= innerR && dist <= outerR) ? 1f : 0f;
				pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
			}
		}
		tex.SetPixels(pixels);
		tex.Apply();

		// pixelsPerUnit = size/2 → 스프라이트 전체 폭이 로컬 2단위(반지름 1) = RingLocalRadius와 일치.
		_ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / (RingLocalRadius * 2f));
		return _ringSprite;
	}

	private Vector3 TileCenter(Vector2Int pos, int floor)
	{
		Vector3 floorOffset = _unitGenerate != null ? _unitGenerate.GetFloorOffset(floor) : Vector3.zero;
		return new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0f) + floorOffset;
	}
}
