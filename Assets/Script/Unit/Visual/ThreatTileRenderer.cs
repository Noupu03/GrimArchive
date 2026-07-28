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

	private HashSet<Unit> _cachedAliveUnits = new HashSet<Unit>();
	private List<Unit> _cachedRemoveList = new List<Unit>();

	public void Render(List<Unit> units)
	{
		_cachedAliveUnits.Clear();
		foreach(var u in units) _cachedAliveUnits.Add(u);

		// =====================================
		// REMOVE PHASE
		// =====================================
		_cachedRemoveList.Clear();

		foreach (var pair in activeVisuals)
		{
			Unit u = pair.Key;

			bool shouldRemove =
				u == null ||
				!_cachedAliveUnits.Contains(u) ||
				u.AIState.currentThreat == null;

			if (shouldRemove)
			{
				if (pair.Value != null && pair.Value.root != null)
					Object.Destroy(pair.Value.root.gameObject);

				_cachedRemoveList.Add(u);
			}
		}

		foreach (var u in _cachedRemoveList)
			activeVisuals.Remove(u);

		// =====================================
		// RENDER PHASE
		// =====================================
		foreach (Unit u in units)
		{
			if (u == null || u.AIState.currentThreat == null)
				continue;

			ThreatTileData threat = u.AIState.currentThreat;

			if (threat.hitbox.size == Vector2.zero)
				continue;

			Vector3 floorOffset =
				_unitGenerate != null
				? _unitGenerate.GetFloorOffset(u.currentFloor)
				: Vector3.zero;

			if (!activeVisuals.TryGetValue(u, out ThreatVisual tv))
			{
				GameObject rootGo = new GameObject("ThreatZone");
				rootGo.transform.SetParent(_root);
				tv = new ThreatVisual { root = rootGo.transform };
				activeVisuals[u] = tv;
			}

			Hitbox box = threat.hitbox;

			// 히트박스 로컬 그리드: depth(공격 방향으로 뻗는 칸 수) x width(좌우 폭)
			int depth = Mathf.Max(1, Mathf.RoundToInt(box.size.x));
			int width = Mathf.Max(1, Mathf.RoundToInt(box.size.y));

			float rad = box.rotation * Mathf.Deg2Rad;
			float cos = Mathf.Cos(rad);
			float sin = Mathf.Sin(rad);

			// 야생 몬스터(WildMonsterBehavior)의 공격 위협타일은 그 외 몬스터(플레이어 소속, 빨강)와
			// 구분되는 색을 쓴다 — 원래 회색이었으나(2026-07-27) 채도가 낮아 던전 벽/바닥의 회색 톤과
			// 비슷해 안 보인다는 신고를 거쳐(2026-07-28), 지난 논의에서 핑크와 보라 사이 색으로
			// 정하기로 했었다 — WildThreatColor로 교체.
			bool isWild = !(u is Human) && u.FactionBehavior is WildMonsterBehavior;
			Color color = u is Human ? Color.green
				: isWild ? WildThreatColor
				: Color.red;
			color.a = isWild ? Mathf.Max(threat.color.a, 0.85f) : threat.color.a;

			int index = 0;
			for (int dx = 0; dx < depth; dx++)
			{
				for (int dy = 0; dy < width; dy++)
				{
					bool forward  = dx == depth - 1; // 공격 방향 끝 (더 뻗을 칸이 없음)
					bool backward = dx == 0;         // 유닛과 맞닿은 칸 (더 가까운 칸이 없음)
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
					// 스프라이트가 "아래(-Y)" 방향 기준으로 그려져 있어 게임 각도 관례와 맞추려면 +90도,
					// 여기에 칸별 패턴 정렬을 위한 rotSteps*90도를 추가로 더한다.
					sr.transform.rotation = Quaternion.Euler(0f, 0f, box.rotation + 90f + rotSteps * 90f);

					sr.color = color;
					sr.enabled = true;
				}
			}

			// 이번 프레임에 안 쓰인 여분 칸 스프라이트는 꺼둔다 (재사용 대비 유지)
			for (int i = index; i < tv.cellSprites.Count; i++)
				tv.cellSprites[i].enabled = false;
		}
	}
}
