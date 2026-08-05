using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer;

// 임시 디버그 시각화 — 07_전파·소리·간접입력 시스템이 실제로 어떻게 동작하는지(구현현황 문서 "검증
// 상태"의 실제 플레이 검증 항목들) 눈으로 확인하기 위한 용도. ThreatTileRenderer와 동일한 컨벤션
// (Update/인스펙터가 없는 순수 C# 클래스, GameSession이 매 프레임 Render(units) 호출)을 따른다.
// 검증이 끝나면 이 파일 + GameCompositionRoot 등록 + GameSession 호출부 세 곳만 지우면 깔끔히
// 제거된다 — 다른 로직은 이 클래스를 참조하지 않는다.
//
// 2026-08-05: 처음엔 LineRenderer + Shader.Find("Sprites/Default")로 원을 그렸는데, 실제로 켜봐도
// 아무것도 안 보인다는 신고를 받았다 — 이 프로젝트가 URP를 쓰고 있어 그렇게 즉석으로 만든 머티리얼이
// 제대로 렌더링된다는 보장이 없었다(검증된 적 없는 경로). 대신 ThreatTileRenderer가 이미 증명된
// 방식(SpriteRenderer + 스프라이트, 머티리얼을 직접 손대지 않고 SpriteRenderer 기본 머티리얼에
// 맡김)을 그대로 따라, 원형 링 텍스처를 코드로 한 번 생성해 SpriteRenderer로 그리는 방식으로 교체했다.
//
// 그려주는 것 2가지:
//   1. 살아있는 인류마다 현재 전파 범위(카리스마 기반, 6장)를 옅은 하늘색 원 테두리로.
//   2. PropagationSystem.OnSoundEmitted를 구독해, 소리가 발생할 때마다 그 위치에 소리 종류별 색
//      원으로 "그 소리가 도달하는 기본 범위"를 표시했다가 지운다(아래 DebugFlashSeconds — 07-A
//      7-3장의 "확인 행동 5초 유예"와는 무관한, 순수 시각화용 임의의 짧은 표시 시간). OnSoundPerceived
//      (누군가 실제로 감지 성공했을 때만 발동)가 아니라 OnSoundEmitted(감지 성공 여부와 무관하게
//      소리가 난 사건 자체)를 구독한다 — "몬스터가 소리 내는 범위 자체를 보고 싶다"는 사용자 요청
//      (2026-08-05)에 맞춘 선택. 즉 아무도 못 들었어도(범위 밖/이미 다른 걸 보고 있어서 등) 원은 뜬다
//      — 감지 성공 여부(누가 실제로 반응했는지)는 이 시각화로는 알 수 없다는 한계가 있다.
public class PropagationDebugVisualizer
{
	// DebugInfoPanel의 "시야 표시" 토글(UnitGenerate.ShowAllVisionRanges)과 동일 컨벤션 — 기본은 꺼짐,
	// 우측 상단 버튼(DrawPropagationToggle)으로 켠다.
	public bool Enabled = false;

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

	// 2026-08-05 수정: 예전엔 "리스트 인덱스 위치"로 풀에서 원을 배정받았는데, 프레임마다 _recentSounds의
	// 구성(어떤 플래시가 만료됐는지/새로 추가됐는지)이 바뀌면서 같은 GameObject가 순간적으로 다른
	// 플래시로 재배정되는 버그가 있었다(사용자가 "한 프레임만에 사라지는 거 아니냐"고 지적) — 이제
	// 각 플래시가 발생 즉시 자기 전용 GameObject를 만들어 그 목숨이 다할 때까지 그대로 들고 있는다.
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
		// OnSoundPerceived(누군가 감지 성공)가 아니라 OnSoundEmitted(소리가 발생한 사건 자체)를
		// 구독한다 — "몬스터가 소리를 내는 범위 자체를 보고 싶다"는 사용자 요청(2026-08-05)에 맞춰,
		// 실제로 아무도 감지 못 했어도(범위 밖/이미 다른 걸 보고 있어서 등) 소리가 날 때마다 무조건
		// 원이 뜬다 — 감지 성공 여부와 무관하게 "이 소리가 어디까지 들리는 범위였는지"를 보여준다.
		// Enabled가 꺼져 있을 땐 만들어봐야 그릴 일이 없으니 구독 콜백에서 바로 걸러낸다(이동음처럼
		// 잦은 소리까지 꺼진 상태에서 GameObject가 무한정 생기는 걸 방지).
		PropagationSystem.OnSoundEmitted.Subscribe(e =>
		{
			if (!Enabled) return;
			var cv = CreateCircle("Sound_" + e.Type);
			PlaceCircle(cv.Renderer, TileCenter(e.Position, e.FloorIndex), e.RangeTiles);
			_recentSounds.Add(new SoundFlash { Type = e.Type, StartTime = Time.time, Renderer = cv.Renderer });
		});
	}

	public void Render(List<Unit> units)
	{
		if (_root == null || _root.gameObject == null) return;
		_root.gameObject.SetActive(Enabled);
		if (!Enabled) return;

		RenderPropagationRanges(units);
		RenderActiveSounds();
	}

	private void RenderPropagationRanges(List<Unit> units)
	{
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
		// 반경은 실제로 줄어드는 게 아니라(14장 소리별 기본 범위는 고정값) 표시 시간이 다 되면 그냥
		// 사라지는 것뿐이므로, 위치·반경은 발생 시점(구독 콜백)에 이미 고정해뒀고 여기서는 매 프레임
		// 남은 시간에 따라 투명도만 갱신한다 — 만료되면 그 플래시 전용 GameObject를 destroy한다.
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

	// ThreatTileRenderer는 프로젝트에 이미 있는 스프라이트 라이브러리를 쓰지만, 이 임시 시각화는 별도
	// 에셋을 만들기 싫어서 코드로 원형 링 텍스처를 한 번만 절차적으로 생성해 재사용한다. 머티리얼은
	// SpriteRenderer가 스프라이트를 배정받으면 자동으로 붙는 기본 스프라이트 머티리얼을 그대로 쓴다
	// (LineRenderer용으로 즉석에서 Shader.Find("Sprites/Default")를 만들어 붙였던 예전 방식은 이
	// URP 프로젝트에서 실제로 안 보이는 문제가 있어 폐기 — SpriteRenderer 기본 경로는 ThreatTileRenderer가
	// 이미 검증해 준 방식이라 안전하다).
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
