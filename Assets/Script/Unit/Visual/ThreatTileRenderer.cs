using UnityEngine;
using System.Collections.Generic;
using VContainer;
using Haare.Util.Logger;
#if UNITY_2022_2_OR_NEWER
using UnityEngine.U2D.Animation;
#endif

// Update/OnGUI/인스펙터 데이터가 전혀 없는 순수 스폰/렌더 메커니즘이라 씬 GameObject일 필요가 없는
// 순수 C# 클래스. 생성한 위협 타일 시각화들을 묶어둘 부모 Transform만 자체적으로 하나 만들어 든다.
public class ThreatTileRenderer
{
	// Assets/Resources/attackZone.spriteLib — 라벨 "0"~"5".
	// 씬 배치 없이 Resources.Load로 가져오므로 인스펙터 할당이 필요 없다.
	private const string AttackZoneLibraryResourcePath = "attackZone";

	// 야생 몬스터 위협타일 색(핑크와 보라 사이) — 지난 논의에서 회색 대신 이 색으로 정하기로 했었음.
	private static readonly Color WildThreatColor = new Color(0.85f, 0.35f, 0.95f, 1f);

	// 슬롯 순서: [forward(공격 방향, 스프라이트 "아래"), right(오른쪽), backward(유닛 쪽, "위"), left(왼쪽)]
	// 라벨 원본 패턴 (회전 0 기준)
	private static readonly bool[][] LabelPatterns =
	{
		new[] { false, false, false, false }, // 0: 빈 모서리
		new[] { true,  false, false, false }, // 1: 아래만
		new[] { true,  true,  false, false }, // 2: 아래+오른쪽
		new[] { true,  true,  true,  false }, // 3: 아래+오른쪽+위
		new[] { true,  true,  true,  true  }, // 4: 전부
		new[] { true,  false, true,  false }, // 5: 아래+위
	};

#if UNITY_2022_2_OR_NEWER
	private SpriteLibraryAsset _attackZoneLibrary;
	private string _attackZoneCategory;
	private readonly Dictionary<int, Sprite> _labelSpriteCache = new();
#endif

	private class ThreatVisual
	{
		public Transform root;
		public List<SpriteRenderer> cellSprites = new List<SpriteRenderer>();
		public float durationTimer;
		public float maxDuration;
		public Color baseColor;
		// 시전(예고) 중인 공격의 범위인지. true면 시간에 따라 옅어지지 않고 원래 색을 그대로 유지하다가,
		// 시전이 끝나는 순간(= 피해가 들어가는 순간) 사라진다. 파이어볼처럼 시전이 긴 스킬은 예전
		// 페이드 방식으로는 정작 피해가 들어갈 때 거의 투명해져 범위가 보이지 않았다(2026-08-23).
		public bool holdUntilImpact;
	}

	// Unit당 1개만 관리
	private Dictionary<Unit, ThreatVisual> activeVisuals
		= new Dictionary<Unit, ThreatVisual>();

	// 생성한 ThreatZone 시각화들을 담아둘 부모 컨테이너. 예전에는 이 컴포넌트 자신의 transform이었다.
	private readonly Transform _root = new GameObject("ThreatTileRenderer").transform;

	private UnitGenerate _unitGenerate;

	[Inject]
	public void Construct(UnitGenerate unitGenerate)
	{
		_unitGenerate = unitGenerate;
	}

	public ThreatTileRenderer()
	{
#if UNITY_2022_2_OR_NEWER
		_attackZoneLibrary = Resources.Load<SpriteLibraryAsset>(AttackZoneLibraryResourcePath);
		if (_attackZoneLibrary != null)
		{
			foreach (var cat in _attackZoneLibrary.GetCategoryNames())
			{
				_attackZoneCategory = cat;
				break;
			}
		}
		else
		{
			LogHelper.Warning(LogHelper.GAME, $"[ThreatTileRenderer] Resources/{AttackZoneLibraryResourcePath}.spriteLib를 찾을 수 없습니다.");
		}
#endif
	}

#if UNITY_2022_2_OR_NEWER
	private Sprite GetLabelSprite(int label)
	{
		if (_attackZoneLibrary == null) return null;
		if (_labelSpriteCache.TryGetValue(label, out var cached)) return cached;

		Sprite s = _attackZoneLibrary.GetSprite(_attackZoneCategory, label.ToString());
		_labelSpriteCache[label] = s;
		return s;
	}
#endif

	// 4방향 경계 패턴에 맞는 라벨과 추가 회전(0~3, *90도)을 찾는다.
	private static (int label, int rotSteps) MatchPattern(bool forward, bool right, bool backward, bool left)
	{
		for (int label = 0; label < LabelPatterns.Length; label++)
		{
			bool[] p = LabelPatterns[label];
			for (int k = 0; k < 4; k++)
			{
				bool r0 = p[((0 - k) % 4 + 4) % 4];
				bool r1 = p[((1 - k) % 4 + 4) % 4];
				bool r2 = p[((2 - k) % 4 + 4) % 4];
				bool r3 = p[((3 - k) % 4 + 4) % 4];

				if (r0 == forward && r1 == right &&
					r2 == backward && r3 == left)
					return (label, k);
			}
		}
		return (0, 0);
	}

	SpriteRenderer GetOrCreateCellSprite(ThreatVisual tv, int index)
	{
		if (index < tv.cellSprites.Count) return tv.cellSprites[index];

		GameObject go = new GameObject($"Cell_{index}");
		go.transform.SetParent(tv.root);

		SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
		sr.sortingOrder = 999;

		tv.cellSprites.Add(sr);
		return sr;
	}

	/// <param name="holdUntilImpact">
	/// 시전 중인 공격의 예고 범위인지. true면 시전이 끝나 실제 피해가 들어갈 때까지 색이 옅어지지 않고
	/// 그대로 남아있다가 그 순간 사라진다. false(기본)면 종전대로 duration에 걸쳐 서서히 사라지는
	/// 잔상으로 그린다 — 즉발 공격은 표시 시점에 이미 피해가 끝나 있으므로 그쪽이 맞다.
	/// </param>
	public void ShowThreatZone(Unit u, ThreatTileData threat, float duration = 0.5f, bool holdUntilImpact = false)
	{
		if (u == null || threat == null || threat.hitbox.size == Vector2.zero) return;

		Vector3 floorOffset = _unitGenerate != null ? _unitGenerate.GetFloorOffset(u.currentFloor) : Vector3.zero;

		if (!activeVisuals.TryGetValue(u, out ThreatVisual tv) || tv.root == null)
		{
			GameObject rootGo = new GameObject("ThreatZone");
			rootGo.transform.SetParent(_root);
			tv = new ThreatVisual { root = rootGo.transform };
			activeVisuals[u] = tv;
		}

		Hitbox box = threat.hitbox;
		int depth = Mathf.Max(1, Mathf.RoundToInt(box.size.x));
		int width = Mathf.Max(1, Mathf.RoundToInt(box.size.y));

		float rad = box.rotation * Mathf.Deg2Rad;
		float cos = Mathf.Cos(rad);
		float sin = Mathf.Sin(rad);

		bool isWild = !(u is Human) && u.FactionBehavior is WildMonsterBehavior;
		Color color = u is Human ? Color.green
			: isWild ? WildThreatColor
			: Color.red;
		color.a = isWild ? Mathf.Max(threat.color.a, 0.85f) : (threat.color.a > 0f ? threat.color.a : 0.85f);

		tv.durationTimer    = duration;
		tv.maxDuration      = duration;
		tv.baseColor        = color;
		tv.holdUntilImpact  = holdUntilImpact;

		int index = 0;
		for (int dx = 0; dx < depth; dx++)
		{
			for (int dy = 0; dy < width; dy++)
			{
				bool forward  = dx == depth - 1;
				bool backward = dx == 0;
				bool right    = dy == width - 1;
				bool left     = dy == 0;

				var (label, rotSteps) = MatchPattern(forward, right, backward, left);

				SpriteRenderer sr = GetOrCreateCellSprite(tv, index);
				index++;

#if UNITY_2022_2_OR_NEWER
				sr.sprite = GetLabelSprite(label);
#endif

				Vector2 localOffset = new Vector2(
					-depth * 0.5f + 0.5f + dx,
					-width * 0.5f + 0.5f + dy
				);
				Vector2 rotatedOffset = new Vector2(
					localOffset.x * cos - localOffset.y * sin,
					localOffset.x * sin + localOffset.y * cos
				);

				sr.transform.position = new Vector3(box.center.x + rotatedOffset.x, box.center.y + rotatedOffset.y, 0f) + floorOffset;
				sr.transform.rotation = Quaternion.Euler(0f, 0f, box.rotation + 90f + rotSteps * 90f);
				sr.color = color;
				sr.enabled = true;
			}
		}

		for (int i = index; i < tv.cellSprites.Count; i++)
			tv.cellSprites[i].enabled = false;
	}

	public void RemoveThreatZone(Unit u)
	{
		if (u == null) return;
		if (activeVisuals.TryGetValue(u, out ThreatVisual tv))
		{
			if (tv != null && tv.root != null)
			{
				Object.Destroy(tv.root.gameObject);
			}
			activeVisuals.Remove(u);
		}
	}

	private List<Unit> _cachedRemoveList = new List<Unit>();

	public void Render(List<Unit> units)
	{
		_cachedRemoveList.Clear();

		foreach (var pair in activeVisuals)
		{
			Unit u = pair.Key;
			ThreatVisual tv = pair.Value;

			// 유닛이 파괴되었거나, 체력이 0 이하(사망)이거나, 시각화 루트가 없으면 즉시 정리
			if (u == null || u.Health.hp <= 0 || tv == null || tv.root == null)
			{
				if (tv != null && tv.root != null)
				{
					Object.Destroy(tv.root.gameObject);
				}
				_cachedRemoveList.Add(u);
				continue;
			}

			// 예고 범위(시전 중인 공격)는 시간에 따라 옅어지지 않는다 — 시전이 살아있는 동안 원래 색을
			// 그대로 유지하다가, 시전이 끝나는 순간(피해 실행 또는 취소) 사라진다. 그래야 "범위가 뜬
			// 시점부터 피해가 들어가는 순간까지" 계속 보인다(2026-08-23 사용자 요청).
			if (tv.holdUntilImpact)
			{
				bool stillCasting = u.CombatState.State.isCastingAttack && u.AIState.currentThreat != null;
				if (stillCasting)
				{
					foreach (var sr in tv.cellSprites)
					{
						if (sr != null && sr.enabled) sr.color = tv.baseColor;
					}
				}
				else
				{
					if (tv.root != null) Object.Destroy(tv.root.gameObject);
					_cachedRemoveList.Add(u);
				}
				continue;
			}

			tv.durationTimer -= Time.deltaTime;
			if (tv.durationTimer <= 0f)
			{
				if (tv.root != null) Object.Destroy(tv.root.gameObject);
				_cachedRemoveList.Add(u);
			}
			else
			{
				float ratio = Mathf.Clamp01(tv.durationTimer / Mathf.Max(0.001f, tv.maxDuration));
				Color fadedColor = tv.baseColor;
				fadedColor.a = tv.baseColor.a * ratio;
				foreach (var sr in tv.cellSprites)
				{
					if (sr != null && sr.enabled) sr.color = fadedColor;
				}
			}
		}

		foreach (var u in _cachedRemoveList)
			activeVisuals.Remove(u);
	}
}
