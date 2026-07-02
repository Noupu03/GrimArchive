using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using VContainer;
using DG.Tweening;
#if UNITY_2022_2_OR_NEWER
using UnityEngine.U2D.Animation;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

public class UnitGenerate : MonoBehaviour
{
	// View mapping to separate SO data logic from Visual gameobjects
	private Dictionary<Unit, GameObject> visualMap        = new Dictionary<Unit, GameObject>();
	private Dictionary<Unit, Vector3>    targetPosMap     = new Dictionary<Unit, Vector3>();

	private Sprite humanSprite;
	private Sprite monsterSprite;

	private UnitSpriteManager _unitSpriteManager;
	private IObjectResolver _resolver;

	[Inject]
	public void Construct(UnitSpriteManager unitSpriteManager, IObjectResolver resolver)
	{
		_unitSpriteManager = unitSpriteManager;
		_resolver = resolver;
	}

	void Awake()
	{
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

		GameObject go = prefab != null ? Instantiate(prefab) : BuildFallbackVisual(unit);
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

#if UNITY_2022_2_OR_NEWER
		SpriteLibrary spriteLib = go.GetComponentInChildren<SpriteLibrary>();
		if (spriteLib != null && spriteLib.spriteLibraryAsset != null && string.IsNullOrEmpty(unit.spriteVariation))
			unit.spriteVariation = _unitSpriteManager.PickRandomVariation(spriteLib.spriteLibraryAsset);

		SpriteResolver spriteResolver = go.GetComponentInChildren<SpriteResolver>();
		if (spriteResolver != null)
			UpdateSpriteResolver(spriteResolver, unit.currentDir, unit.spriteVariation);
#endif

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

		GameObject outlineGo = new GameObject("Outline");
		outlineGo.transform.SetParent(visual.transform);
		outlineGo.transform.localPosition = Vector3.zero;
		SpriteRenderer outlineSr = outlineGo.AddComponent<SpriteRenderer>();
		outlineSr.color        = Color.black;
		outlineSr.sortingOrder = 9;
		outlineGo.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
		outlineGo.AddComponent<OutlineSpriteSync>().Init(sr);
		outlineGo.SetActive(false);

		return go;
	}

	private UnitVisualDefinition GetVisualDef(Unit u) =>
		visualMap.TryGetValue(u, out GameObject go) && go != null ? go.GetComponent<UnitVisualDefinition>() : null;

	public void SpawnGuardVFX(Unit u) => u?.VFX?.Spawn(GetVisualDef(u)?.guardPrefab, u);
	public void SpawnParryVFX(Unit u) => u?.VFX?.Spawn(GetVisualDef(u)?.parryPrefab, u);

	public List<SkillAction> GetSkills(string unitTypeName) =>
		_unitSpriteManager != null ? _unitSpriteManager.GetSkills(unitTypeName) : new List<SkillAction>();

	public int GetEngageDistance(string unitTypeName, int defaultDist) =>
		_unitSpriteManager != null ? _unitSpriteManager.GetEngageDistance(unitTypeName, defaultDist) : defaultDist;

	public void UpdateUnitSpriteForDirection(Unit unit)
	{
		if (!visualMap.TryGetValue(unit, out GameObject go)) return;

#if UNITY_2022_2_OR_NEWER
		SpriteResolver spriteResolver = go.GetComponentInChildren<SpriteResolver>();
		if (spriteResolver != null)
		{
			UpdateSpriteResolver(spriteResolver, unit.currentDir, unit.spriteVariation);
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
		Debug.LogError("SpriteResolver는 Unity 2022.2 이상에서 지원됩니다.");
#endif
	}

	#region 유닛 생성 보조 기능성

	private Transform GetFloorTilemapTransform(int floorIdx)
	{
		var mr = FindObjectOfType<MapRandering>();
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
		var mr = FindObjectOfType<MapRandering>();
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
			if (go != null) { go.transform.DOKill(); Destroy(go); }
			visualMap.Remove(u);
			targetPosMap.Remove(u);
		}

		List<Unit> deadKeys = new List<Unit>();
		foreach (var kvp in visualMap)
		{
			if (kvp.Key == null || kvp.Key.hp <= 0)
			{
				if (kvp.Value != null) { kvp.Value.transform.DOKill(); Destroy(kvp.Value); }
				deadKeys.Add(kvp.Key);
			}
		}
		foreach (var deadKey in deadKeys)
		{
			visualMap.Remove(deadKey);
			targetPosMap.Remove(deadKey);
		}
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

			// FOV
			UnitVisual uv = go.GetComponent<UnitVisual>();
			if (uv != null)
			{
				Vector2 forward = u.GetDirVector(u.currentDir);
				if (forward == Vector2.zero) forward = Vector2.down;
				uv.DrawFOV(Unit.ViewRadius, 160f, forward);
			}

			UpdateUnitSpriteForDirection(u);

			// 아웃라인은 Visual 자식의 하위에 위치
			Transform outlineTransform = go.transform.Find("Visual/Outline");
			if (outlineTransform != null)
			{
				bool isSelected  = (u.InputMgr != null && u.InputMgr.selectedUnit == u);
				bool isPanicking = u is Human && u.mental < u.maxMental * 0.3f;

				outlineTransform.gameObject.SetActive(isSelected || isPanicking);
				if (isSelected || isPanicking)
				{
					SpriteRenderer outlineSr = outlineTransform.GetComponent<SpriteRenderer>();
					if (outlineSr != null)
						outlineSr.color = isSelected ? Color.black : Color.red;
				}
			}
		}
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
		if (GameSession.Instance != null &&
			GameSession.Instance.unitGrid.TryGetValue(new Vector3Int(pos.x, pos.y, floorIdx), out Unit u))
			return u != null && u.hp > 0;
		return false;
	}

	public bool IsAreaClear(Vector2Int pos, Vector2 footprint, int floorIdx)
	{
		CreateMap cmap = (GameSession.Instance != null && GameSession.Instance.cmap != null)
			? GameSession.Instance.cmap
			: FindObjectOfType<CreateMap>();
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
				int cx = x / 8; int cy = y / 8;
				int tx = x % 8; int ty = y % 8;

				if (cx < 0 || cx >= floor.config.width || cy < 0 || cy >= floor.config.height) return false;
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
		CreateMap cmap = (GameSession.Instance != null && GameSession.Instance.cmap != null)
			? GameSession.Instance.cmap
			: FindObjectOfType<CreateMap>();
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
		CreateMap cmap = (GameSession.Instance != null && GameSession.Instance.cmap != null)
			? GameSession.Instance.cmap
			: FindObjectOfType<CreateMap>();
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
		CreateMap cmap = (GameSession.Instance != null && GameSession.Instance.cmap != null)
			? GameSession.Instance.cmap
			: FindObjectOfType<CreateMap>();
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
