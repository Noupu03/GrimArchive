using UnityEngine;
using System.Collections.Generic;
using VContainer;
using Haare.Util.Logger;
#if UNITY_2022_2_OR_NEWER
using UnityEngine.U2D.Animation;
#endif

public class ThreatTileRenderer : MonoBehaviour
{
	// Assets/Resources/attackZone.spriteLib — 라벨 "0"~"5".
	// 씬 배치 없이 Resources.Load로 가져오므로 인스펙터 할당이 필요 없다.
	private const string AttackZoneLibraryResourcePath = "attackZone";

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

	private UnitGenerate _unitGenerate;

	[Inject]
	public void Construct(UnitGenerate unitGenerate)
	{
		_unitGenerate = unitGenerate;
	}

	void Awake()
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

	// pattern을 시계 방향으로 k*90도 회전시킨 결과 (슬롯 순서: forward, right, backward, left)
	private static bool[] RotateSlots(bool[] pattern, int k)
	{
		bool[] result = new bool[4];
		for (int i = 0; i < 4; i++)
			result[i] = pattern[((i - k) % 4 + 4) % 4];
		return result;
	}

	// 4방향 경계 패턴에 맞는 라벨과 추가 회전(0~3, *90도)을 찾는다.
	private static (int label, int rotSteps) MatchPattern(bool forward, bool right, bool backward, bool left)
	{
		bool[] required = { forward, right, backward, left };
		for (int label = 0; label < LabelPatterns.Length; label++)
		{
			for (int k = 0; k < 4; k++)
			{
				bool[] rotated = RotateSlots(LabelPatterns[label], k);
				if (rotated[0] == required[0] && rotated[1] == required[1] &&
					rotated[2] == required[2] && rotated[3] == required[3])
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

	public void Render(List<Unit> units)
	{
		HashSet<Unit> aliveUnits = new HashSet<Unit>(units);

		// =====================================
		// REMOVE PHASE
		// =====================================
		List<Unit> removeList = new();

		foreach (var pair in activeVisuals)
		{
			Unit u = pair.Key;

			bool shouldRemove =
				u == null ||
				!aliveUnits.Contains(u) ||
				u.currentThreat == null;

			if (shouldRemove)
			{
				if (pair.Value != null && pair.Value.root != null)
					Destroy(pair.Value.root.gameObject);

				removeList.Add(u);
			}
		}

		foreach (var u in removeList)
			activeVisuals.Remove(u);

		// =====================================
		// RENDER PHASE
		// =====================================
		foreach (Unit u in units)
		{
			if (u == null || u.currentThreat == null)
				continue;

			ThreatTileData threat = u.currentThreat;

			if (threat.hitbox.size == Vector2.zero)
				continue;

			Vector3 floorOffset =
				_unitGenerate != null
				? _unitGenerate.GetFloorOffset(u.currentFloor)
				: Vector3.zero;

			if (!activeVisuals.TryGetValue(u, out ThreatVisual tv))
			{
				GameObject rootGo = new GameObject("ThreatZone");
				rootGo.transform.SetParent(transform);
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

			Color color = u is Human ? Color.green : Color.red;
			color.a = threat.color.a;

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
