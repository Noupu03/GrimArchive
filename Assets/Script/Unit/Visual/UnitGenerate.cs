using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using VContainer;
using DG.Tweening;
using Haare.Util.Logger;
#if UNITY_2022_2_OR_NEWER
using UnityEngine.U2D.Animation;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

public class UnitGenerate
{
	// View mapping to separate SO data logic from Visual gameobjects
	private Dictionary<Unit, GameObject> visualMap        = new Dictionary<Unit, GameObject>();
	private Dictionary<Unit, Vector3>    targetPosMap     = new Dictionary<Unit, Vector3>();

	// 선택 표시용 사각 테두리(SelectionMarker) 관련 상수. (패닉 빨간 테두리는 안 씀 — 제거됨)
	// 예전엔 캐릭터 스프라이트를 검게 복제해서 뒤에 깔아두는 방식(OutlineSpriteSync)을 썼는데,
	// 실제 유닛 프리팹(JsonToUnitPrefabConverter가 굽는 것들)에는 애초에 그 동기화 컴포넌트가
	// 안 붙어 있어서 스프라이트가 한 번도 세팅되지 않는 죽은 오브젝트였다(폴백 도형에만 붙어있었음).
	// 그래서 캐릭터 스프라이트/애니메이션 시스템과 아예 무관하게, 풋프린트 크기만으로 계산되는
	// 4개짜리 얇은 사각 바(위/아래/좌/우)로 다시 만들었다 — 스프라이트가 뭘로 바뀌든 안 깨진다.
	private const float SelectionMarkerThickness = 0.12f; // 월드 유닛 기준 두께(풋프린트 크기와 무관하게 일정)
	private const float SelectionMarkerPadding   = 0.08f; // 풋프린트보다 살짝 크게
	private const int   SelectionMarkerSortingOrder = 9;  // 캐릭터(보통 10)보다 한 칸 아래
	private static readonly string[] SelectionMarkerBarNames = { "Top", "Bottom", "Left", "Right" };

	private static Sprite _selectionMarkerSprite;
	private static Sprite SelectionMarkerSprite
	{
		get
		{
			if (_selectionMarkerSprite == null)
			{
				var tex = Texture2D.whiteTexture;
				_selectionMarkerSprite = Sprite.Create(
					tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
			}
			return _selectionMarkerSprite;
		}
	}

	private class VisualCache
	{
		public UnitVisual UnitVisual;
		public UnitVisualDefinition UnitVisualDefinition;
		public WeaponAttachment WeaponAttachment;
		public Transform SelectionMarkerRoot;
		public SpriteRenderer[] SelectionMarkerBars;
#if UNITY_2022_2_OR_NEWER
		public SpriteResolver SpriteResolver;
#endif
	}

	private Dictionary<GameObject, VisualCache> _cacheMap = new Dictionary<GameObject, VisualCache>();
	private MapRandering _cachedMapRandering;

	private VisualCache GetCache(GameObject go)
	{
		if (!_cacheMap.TryGetValue(go, out VisualCache cache))
		{
			cache = new VisualCache();
			cache.UnitVisual = go.GetComponent<UnitVisual>();
			cache.UnitVisualDefinition = go.GetComponent<UnitVisualDefinition>();
			cache.WeaponAttachment = go.GetComponentInChildren<WeaponAttachment>();
#if UNITY_2022_2_OR_NEWER
			cache.SpriteResolver = go.GetComponentInChildren<SpriteResolver>();
#endif
			_cacheMap[go] = cache;
		}
		return cache;
	}

	private MapRandering GetMapRandering()
	{
		if (_cachedMapRandering == null)
			_cachedMapRandering = Object.FindObjectOfType<MapRandering>();
		return _cachedMapRandering;
	}

	private Sprite humanSprite;
	private Sprite monsterSprite;

	private UnitSpriteManager _unitSpriteManager;
	private IObjectResolver _resolver;

	// GameSession은 생성자에서 UnitGenerate를 역주입받는 관계라 직접 주입받으면 순환 의존이 생긴다.
	// 대신 이미 갖고 있는 IObjectResolver로 실제 호출 시점(런타임)에 지연 resolve해서 순환을 피한다.
	private GameSession _gameSession;
	private GameSession Session => _gameSession ??= _resolver.Resolve<GameSession>();

	[Inject]
	public void Construct(UnitSpriteManager unitSpriteManager, IObjectResolver resolver)
	{
		_unitSpriteManager = unitSpriteManager;
		_resolver = resolver;

		humanSprite   = CreateCircleSprite(Color.white);
		monsterSprite = CreateTriangleSprite(Color.white);
	}

	public T GenerateUnitAtRandomFloor<T>(UnitType unitType, int floorIdx = 1) where T : Unit
	{
		Vector2Int pos = GetRandomFloorPos(unitType.footprint, floorIdx);

		T unit = ScriptableObject.CreateInstance<T>();
		_resolver.Inject(unit);
		unit.name        = $"{unitType.typeName}_{pos.x}_{pos.y}_{floorIdx}";
		unit.unitType    = unitType;
		unit.position    = pos;
		unit.currentFloor = floorIdx;

		SetupUnitVisual(unit, 1.0f);
		return unit;
	}

	// ─────────────────────────────────────────────────────────────────
	//  유닛 비주얼 생성
	//
	//  유닛 타입별 프리팹(UnitSpriteManager에 등록됨)이 있으면 그걸 Instantiate하고,
	//  없으면 원/삼각형 폴백 도형을 코드로 생성한다. 프리팹의 UnitVisualDefinition이
	//  스탯/스킬/이펙트를, 프리팹 자식 계층(Visual)의 SpriteLibrary/SpriteResolver/
	//  UnitAnimationController가 스프라이트/애니메이션을 담당한다.
	// ─────────────────────────────────────────────────────────────────
	private void SetupUnitVisual(Unit unit, float visualScale)
	{
		GameObject prefab = _unitSpriteManager != null ? _unitSpriteManager.GetPrefab(unit.unitType.typeName) : null;
		UnitVisualDefinition visualDef = prefab != null ? prefab.GetComponent<UnitVisualDefinition>() : null;

		GameObject go = prefab != null ? Object.Instantiate(prefab) : BuildFallbackVisual(unit);
		go.name = unit.name;

		Transform tilemapTransform = GetFloorTilemapTransform(unit.currentFloor);
		if (tilemapTransform != null) go.transform.SetParent(tilemapTransform);
		go.transform.localScale = new Vector3(
			unit.unitType.footprint.x * visualScale,
			unit.unitType.footprint.y * visualScale,
			1f
		);

		UnitVisual uv = go.GetComponent<UnitVisual>();
		if (uv == null) uv = go.AddComponent<UnitVisual>();
		uv.Setup();
		uv.boundUnit = unit;

		var cache = GetCache(go);

#if UNITY_2022_2_OR_NEWER
		SpriteLibrary spriteLib = go.GetComponentInChildren<SpriteLibrary>();
		if (spriteLib != null && spriteLib.spriteLibraryAsset != null && string.IsNullOrEmpty(unit.spriteVariation))
			unit.spriteVariation = _unitSpriteManager.PickRandomVariation(spriteLib.spriteLibraryAsset);

		if (cache.SpriteResolver != null)
			UpdateSpriteResolver(cache.SpriteResolver, unit.currentDir, unit.spriteVariation);
#endif

		cache.WeaponAttachment?.UpdatePose(unit.currentDir);

		go.transform.position = new Vector3(
			unit.position.x + unit.unitType.footprint.x / 2f,
			unit.position.y + 0.05f,
			0
		) + GetFloorOffset(unit.currentFloor);

		visualMap[unit] = go;

		if (visualDef != null) visualDef.ApplyStatsTo(unit);
		unit.SetupStats();
	}

	// 프리팹이 등록 안 된 유닛 타입을 위한 폴백 (원/삼각형 도형, 애니메이션 없음)
	private GameObject BuildFallbackVisual(Unit unit)
	{
		GameObject go = new GameObject();

		GameObject visual = new GameObject("Visual");
		visual.transform.SetParent(go.transform);
		visual.transform.localPosition = Vector3.zero;

		SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
		sr.sortingOrder = 10;
		if (unit is Human)        sr.sprite = humanSprite;
		else if (unit is Monster) sr.sprite = monsterSprite;

		return go;
	}

	private UnitVisualDefinition GetVisualDef(Unit u) =>
		visualMap.TryGetValue(u, out GameObject go) && go != null ? GetCache(go).UnitVisualDefinition : null;

	public void SpawnGuardVFX(Unit u) => u?.VFX?.Spawn(GetVisualDef(u)?.guardPrefab, u);
	public void SpawnParryVFX(Unit u) => u?.VFX?.Spawn(GetVisualDef(u)?.parryPrefab, u);

	public List<SkillAction> GetSkills(string unitTypeName) =>
		_unitSpriteManager != null ? _unitSpriteManager.GetSkills(unitTypeName) : new List<SkillAction>();

	public int GetEngageDistance(string unitTypeName, int defaultDist) =>
		_unitSpriteManager != null ? _unitSpriteManager.GetEngageDistance(unitTypeName, defaultDist) : defaultDist;

	public void UpdateUnitSpriteForDirection(Unit unit)
	{
		if (!visualMap.TryGetValue(unit, out GameObject go)) return;

		var cache = GetCache(go);
		cache.WeaponAttachment?.UpdatePose(unit.currentDir);

#if UNITY_2022_2_OR_NEWER
		if (cache.SpriteResolver != null)
		{
			UpdateSpriteResolver(cache.SpriteResolver, unit.currentDir, unit.spriteVariation);
			return;
		}
#endif
		if (_unitSpriteManager == null) return;
	}

	private void UpdateSpriteResolver(SpriteResolver spriteResolver, Dir direction, string variation)
	{
#if UNITY_2022_2_OR_NEWER
		UnitSpriteManager.GetSpriteLabelForDirection(direction, out string label, out bool flipX);
		spriteResolver.SetCategoryAndLabel(variation, label);
		SpriteRenderer sr = spriteResolver.GetComponent<SpriteRenderer>();
		if (sr != null) sr.flipX = flipX;
#else
		LogHelper.Error(LogHelper.GAME, "SpriteResolver는 Unity 2022.2 이상에서 지원됩니다.");
#endif
	}

	#region 유닛 생성 보조 기능성

	private Transform GetFloorTilemapTransform(int floorIdx)
	{
		var mr = GetMapRandering();
		if (mr != null)
		{
			Transform childTilemap = mr.transform.Find($"F{floorIdx}_Tilemap");
			if (childTilemap != null) return childTilemap;
		}
		return null;
	}

	public Transform GetVisualTransform(Unit unit)
		=> visualMap.TryGetValue(unit, out var go) && go != null ? go.transform : null;

	public Vector3 GetFloorOffset(int floorIdx)
	{
		var mr = GetMapRandering();
		if (mr != null)
		{
			Transform childTilemap = mr.transform.Find($"F{floorIdx}_Tilemap");
			if (childTilemap != null) return childTilemap.position;
			if (mr.floorOffsets != null && floorIdx < mr.floorOffsets.Length)
			{
				Vector3Int offset = mr.floorOffsets[floorIdx];
				return mr.transform.position + new Vector3(offset.x, offset.y, 0f);
			}
		}
		return Vector3.zero;
	}

	public void RemoveVisual(Unit u)
	{
		if (u != null && visualMap.TryGetValue(u, out GameObject go))
		{
			if (go != null) { KillVisualTweens(go); Object.Destroy(go); _cacheMap.Remove(go); }
			visualMap.Remove(u);
			targetPosMap.Remove(u);
		}

		List<Unit> deadKeys = new List<Unit>();
		foreach (var kvp in visualMap)
		{
			if (kvp.Key == null || kvp.Key.hp <= 0)
			{
				if (kvp.Value != null) { KillVisualTweens(kvp.Value); Object.Destroy(kvp.Value); _cacheMap.Remove(kvp.Value); }
				deadKeys.Add(kvp.Key);
			}
		}
		foreach (var deadKey in deadKeys)
		{
			visualMap.Remove(deadKey);
			targetPosMap.Remove(deadKey);
		}
	}

	// go.transform.DOKill()만으로는 TriggerHitEffect()가 Visual 자식의 SpriteRenderer를 타겟으로
	// 만든 DOTween 시퀀스(sr.DOColor(...))가 안 죽는다 — 타겟이 transform이 아니라 sr이라서 별개
	// 트윈으로 취급됨. 피격 직후 곧바로 죽는 경우(킬샷) 그 시퀀스가 파괴된 SpriteRenderer를 계속
	// 건드리려다 DOTween Safe Mode의 "missing target" 에러로 잡히는 원인이었다.
	private void KillVisualTweens(GameObject go)
	{
		go.transform.DOKill();
		Transform vt = go.transform.Find("Visual");
		SpriteRenderer sr = vt != null ? vt.GetComponent<SpriteRenderer>() : go.GetComponentInChildren<SpriteRenderer>();
		if (sr != null) sr.DOKill();
	}

	public void SyncVisuals(List<Unit> units)
	{
		foreach (var u in units)
		{
			if (u == null || !visualMap.TryGetValue(u, out GameObject go)) continue;

			Vector3 newPos = new Vector3(
				u.position.x + u.unitType.footprint.x / 2f,
				u.position.y + 0.05f,
				0
			) + GetFloorOffset(u.currentFloor);

			Transform targetParent = GetFloorTilemapTransform(u.currentFloor);
			if (targetParent != null && go.transform.parent != targetParent)
				go.transform.SetParent(targetParent);

			if (!targetPosMap.TryGetValue(u, out Vector3 currentTarget) || currentTarget != newPos)
			{
				float duration  = u.walkSpeed > 0f ? (1f / u.walkSpeed) : 0.1f;
				targetPosMap[u] = newPos;

				go.transform.DOKill();
				// 일시정지(Time.timeScale=0에 가까운 값) 중에도 이동 애니메이션이 계속 보이도록 unscaled time 사용
				go.transform.DOMove(newPos, duration).SetEase(Ease.Linear).SetUpdate(true);
			}
			else if (Time.timeScale < 0.01f)
			{
				go.transform.position = newPos;
			}

			var cache = GetCache(go);

			// FOV
			UnitVisual uv = cache.UnitVisual;
			if (uv != null)
			{
				Vector2 forward = u.GetDirVector(u.currentDir);
				if (forward == Vector2.zero) forward = Vector2.down;
				uv.DrawFOV(Unit.ViewRadius, 160f, forward);
			}

			UpdateUnitSpriteForDirection(u);

			EnsureSelectionMarker(cache, go, u.unitType.footprint);
			if (cache.SelectionMarkerRoot != null)
			{
				bool isSelected = (u.InputMgr != null && u.InputMgr.selectedUnits.Contains(u));

				cache.SelectionMarkerRoot.gameObject.SetActive(isSelected);
				if (isSelected)
				{
					foreach (var bar in cache.SelectionMarkerBars)
						if (bar != null) bar.color = Color.black;
				}
			}
		}
	}

	// 캐릭터 스프라이트/애니메이션과 완전히 무관한, 풋프린트 기반 사각 테두리 4개(위/아래/좌/우)를
	// go의 자식으로 한 번만 만들어둔다. go.transform.localScale이 이미 풋프린트 크기로 맞춰져
	// 있어서(SetupUnitVisual 참고) 부모 스케일을 역산해 로컬 좌표/스케일을 계산해야 월드 기준으로
	// 일정한 두께/여백이 나온다.
	private void EnsureSelectionMarker(VisualCache cache, GameObject go, Vector2 footprint)
	{
		if (cache.SelectionMarkerBars != null) return;
		if (footprint.x <= 0f || footprint.y <= 0f) return;

		GameObject markerGo = new GameObject("SelectionMarker");
		markerGo.transform.SetParent(go.transform, false);
		markerGo.SetActive(false);

		var bars = new SpriteRenderer[4];
		for (int i = 0; i < bars.Length; i++)
		{
			GameObject bar = new GameObject(SelectionMarkerBarNames[i]);
			bar.transform.SetParent(markerGo.transform, false);
			var sr = bar.AddComponent<SpriteRenderer>();
			sr.sprite = SelectionMarkerSprite;
			sr.sortingOrder = SelectionMarkerSortingOrder;
			bars[i] = sr;
		}

		cache.SelectionMarkerRoot = markerGo.transform;
		cache.SelectionMarkerBars = bars;

		// go의 로컬 X=0은 이미 풋프린트 가로 중앙(SyncVisuals의 newPos.x = position.x + footprint.x/2f),
		// 로컬 Y=0은 풋프린트 바닥(newPos.y = position.y + 0.05f)에 해당한다 — 그래서 이 좌표계 기준으로
		// 위/아래/좌/우 바를 배치한다.
		float thickX = SelectionMarkerThickness / footprint.x;
		float thickY = SelectionMarkerThickness / footprint.y;
		float topEdge    = 1f + SelectionMarkerPadding / footprint.y;
		float bottomEdge =    - SelectionMarkerPadding / footprint.y;
		float leftEdge   = -0.5f - SelectionMarkerPadding / footprint.x;
		float rightEdge  =  0.5f + SelectionMarkerPadding / footprint.x;
		float centerY    = (topEdge + bottomEdge) / 2f;
		float fullW      = rightEdge - leftEdge;
		float fullH      = topEdge - bottomEdge;

		bars[0].transform.localPosition = new Vector3(0f, topEdge - thickY / 2f, 0f);      // Top
		bars[0].transform.localScale    = new Vector3(fullW, thickY, 1f);

		bars[1].transform.localPosition = new Vector3(0f, bottomEdge + thickY / 2f, 0f);   // Bottom
		bars[1].transform.localScale    = new Vector3(fullW, thickY, 1f);

		bars[2].transform.localPosition = new Vector3(leftEdge + thickX / 2f, centerY, 0f); // Left
		bars[2].transform.localScale    = new Vector3(thickX, fullH, 1f);

		bars[3].transform.localPosition = new Vector3(rightEdge - thickX / 2f, centerY, 0f); // Right
		bars[3].transform.localScale    = new Vector3(thickX, fullH, 1f);
	}

	public void TriggerHitEffect(Unit u)
	{
		if (u == null) return;

		UnitVisualDefinition visualDef = GetVisualDef(u);

		if (u.suppressHitVFX)
		{
			if (visualDef != null) u.VFX?.Spawn(visualDef.attackFailPrefab, u);
			u.suppressHitVFX = false;
		}
		else
		{
			if (visualDef != null) u.VFX?.Spawn(visualDef.hitSparkPrefab, u);
		}

		if (visualMap.TryGetValue(u, out GameObject go))
		{
			Transform      vt = go.transform.Find("Visual");
			SpriteRenderer sr = vt != null ? vt.GetComponent<SpriteRenderer>() : go.GetComponentInChildren<SpriteRenderer>();
			if (sr == null) return;

			// 재피격으로 중단되면 sr.color가 페이드 중간값(반투명 등)일 수 있어, 그 값을 "원래 색"으로
			// 잘못 캡처하지 않도록 매번 흰색에서 새로 시작한다 (이 스프라이트는 항상 흰색이 기본값).
			sr.DOKill();
			sr.color = Color.white;
			DOTween.Sequence()
				.Append(sr.DOColor(Color.clear, 0.05f))
				.Append(sr.DOColor(Color.white, 0.05f))
				.SetTarget(sr);
		}
	}

	#endregion

	#region 폴백 스프라이트 생성

	private Sprite CreateCircleSprite(Color color)
	{
		Texture2D texture = new Texture2D(32, 32);
		Color[]   pixels  = new Color[32 * 32];
		Vector2   center  = new Vector2(16f, 16f);
		for (int y = 0; y < 32; y++)
			for (int x = 0; x < 32; x++)
				pixels[y * 32 + x] = Vector2.Distance(center, new Vector2(x, y)) <= 15f ? color : Color.clear;
		texture.SetPixels(pixels);
		texture.Apply();
		return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
	}

	private Sprite CreateTriangleSprite(Color color)
	{
		Texture2D texture = new Texture2D(32, 32);
		Color[]   pixels  = new Color[32 * 32];
		for (int y = 0; y < 32; y++)
		{
			float halfWidth = (1f - y / 31f) * 16f;
			for (int x = 0; x < 32; x++)
				pixels[y * 32 + x] = (x >= 16f - halfWidth && x <= 16f + halfWidth) ? color : Color.clear;
		}
		texture.SetPixels(pixels);
		texture.Apply();
		return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
	}

	#endregion

	#region 위치 유틸리티

	public bool IsOccupied(Vector2Int pos, int floorIdx)
	{
		if (Session != null &&
			Session.unitGrid.TryGetValue(new Vector3Int(pos.x, pos.y, floorIdx), out Unit u))
			return u != null && u.hp > 0;
		return false;
	}

	public bool IsAreaClear(Vector2Int pos, Vector2 footprint, int floorIdx)
	{
		CreateMap cmap = (Session != null && Session.cmap != null)
			? Session.cmap
			: Object.FindObjectOfType<CreateMap>();
		if (cmap == null || cmap.map.floors == null || floorIdx < 0 || floorIdx >= cmap.map.floors.Length) return false;

		Floor floor = cmap.map.floors[floorIdx];
		if (floor.chunks == null) return false;

		int fw = (int)footprint.x;
		int fh = (int)footprint.y;

		for (int dx = 0; dx < fw; dx++)
		{
			for (int dy = 0; dy < fh; dy++)
			{
				int x = pos.x + dx;
				int y = pos.y + dy;

				// C#의 정수 나눗셈/나머지는 0쪽으로 버림하므로(예: -1/8=0, -1%8=-1), 이 사전 체크
				// 없이 바로 나누면 음수 좌표가 cx/cy=0으로 잘못 계산되고 tx/ty가 음수가 되어 아래
				// c.chunk[tx, ty] 인덱싱에서 IndexOutOfRangeException이 난다(UnitFunction.CanMove와
				// 동일한 버그 패턴).
				if (x < 0 || y < 0) return false;

				int cx = x / 8; int cy = y / 8;
				int tx = x % 8; int ty = y % 8;

				if (cx >= floor.config.width || cy >= floor.config.height) return false;
				Chunks c = floor.chunks[cx, cy];
				if (c.roomId == -1 || c.chunk == null) return false;
				if (c.chunk[tx, ty].name == "Wall") return false;
				if (IsOccupied(new Vector2Int(x, y), floorIdx)) return false;
			}
		}
		return true;
	}

	public Vector2Int GetRandomFloorPos(Vector2 footprint, int floorIdx = 1)
	{
		CreateMap cmap = (Session != null && Session.cmap != null)
			? Session.cmap
			: Object.FindObjectOfType<CreateMap>();
		if (cmap == null || cmap.map.floors == null || cmap.map.floors.Length == 0) return Vector2Int.zero;
		if (floorIdx < 0 || floorIdx >= cmap.map.floors.Length) return Vector2Int.zero;

		Floor floor = cmap.map.floors[floorIdx];
		if (floor.chunks == null) return Vector2Int.zero;

		for (int i = 0; i < 2000; i++)
		{
			int cx = Random.Range(0, floor.config.width);
			int cy = Random.Range(0, floor.config.height);
			Chunks c = floor.chunks[cx, cy];
			if (c.roomId != -1 && c.chunk != null)
			{
				int tx = Random.Range(0, 8);
				int ty = Random.Range(0, 8);
				Vector2Int cand = new Vector2Int(cx * 8 + tx, cy * 8 + ty);
				if (IsAreaClear(cand, footprint, floorIdx)) return cand;
			}
		}
		return Vector2Int.zero;
	}

	public Vector2Int GetStartRoomPos(Vector2 footprint, int floorIdx = 1)
	{
		CreateMap cmap = (Session != null && Session.cmap != null)
			? Session.cmap
			: Object.FindObjectOfType<CreateMap>();
		if (cmap == null || cmap.map.floors == null || floorIdx < 0 || floorIdx >= cmap.map.floors.Length) return Vector2Int.zero;

		Floor floor = cmap.map.floors[floorIdx];
		if (floor.chunks == null) return Vector2Int.zero;

		for (int cx = 0; cx < floor.config.width; cx++)
			for (int cy = 0; cy < floor.config.height; cy++)
			{
				Chunks c = floor.chunks[cx, cy];
				if (c.roomRole == RoomRole.StartRoom && c.chunk != null)
					for (int tx = 2; tx < 6; tx++)
						for (int ty = 2; ty < 6; ty++)
						{
							Vector2Int cand = new Vector2Int(cx * 8 + tx, cy * 8 + ty);
							if (IsAreaClear(cand, footprint, floorIdx)) return cand;
						}
			}
		return GetRandomFloorPos(footprint, floorIdx);
	}

	public Vector2Int GetBossRoomPos(Vector2 footprint, int floorIdx = 1)
	{
		CreateMap cmap = (Session != null && Session.cmap != null)
			? Session.cmap
			: Object.FindObjectOfType<CreateMap>();
		if (cmap == null || cmap.map.floors == null || floorIdx < 0 || floorIdx >= cmap.map.floors.Length) return Vector2Int.zero;

		Floor floor = cmap.map.floors[floorIdx];
		if (floor.chunks == null) return Vector2Int.zero;

		for (int cx = 0; cx < floor.config.width; cx++)
			for (int cy = 0; cy < floor.config.height; cy++)
			{
				Chunks c = floor.chunks[cx, cy];
				if (c.roomRole == RoomRole.BossRoom && c.chunk != null)
					for (int tx = 2; tx < 6; tx++)
						for (int ty = 2; ty < 6; ty++)
						{
							Vector2Int cand = new Vector2Int(cx * 8 + tx, cy * 8 + ty);
							if (IsAreaClear(cand, footprint, floorIdx)) return cand;
						}
			}
		return GetRandomFloorPos(footprint, floorIdx);
	}

	public T GenerateUnitAtPos<T>(UnitType unitType, Vector2Int pos, int floorIdx = 1) where T : Unit
	{
		T unit = ScriptableObject.CreateInstance<T>();
		_resolver.Inject(unit);
		unit.name        = $"{unitType.typeName}_{pos.x}_{pos.y}_{floorIdx}";
		unit.unitType    = unitType;
		unit.position    = pos;
		unit.currentFloor = floorIdx;

		SetupUnitVisual(unit, 1.0f);
		return unit;
	}

	#endregion
}
