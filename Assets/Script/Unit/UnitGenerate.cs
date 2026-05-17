using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using System.Collections;
#if UNITY_2022_2_OR_NEWER
using UnityEngine.U2D.Animation;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

public class UnitGenerate : MonoBehaviour
{
	public static UnitGenerate Instance { get; private set; }

	// View mapping to separate SO data logic from Visual gameobjects
	private Dictionary<Unit, GameObject> visualMap = new Dictionary<Unit, GameObject>();
	private Dictionary<Unit, Coroutine> moveCoroutines = new Dictionary<Unit, Coroutine>();
	private Dictionary<Unit, Vector3> targetPosMap = new Dictionary<Unit, Vector3>();
	private Sprite humanSprite;
	private Sprite monsterSprite;
	private Dictionary<Unit, Coroutine> blinkCoroutines = new Dictionary<Unit, Coroutine>();

	void Awake()
	{
		Instance = this;
		humanSprite = CreateCircleSprite(Color.white);
		monsterSprite = CreateTriangleSprite(Color.white);
	}

	public T GenerateUnitAtRandomFloor<T>(UnitType unitType, int floorIdx = 1) where T : Unit
	{
		Vector2Int pos = GetRandomFloorPos(unitType.footprint, floorIdx);

		T unit = ScriptableObject.CreateInstance<T>();
		unit.name = $"{unitType.typeName}_{pos.x}_{pos.y}_{floorIdx}";
		unit.unitType = unitType;
		unit.position = pos;
		unit.currentFloor = floorIdx;
		unit.SetupStats();

		SetupUnitVisual(unit, 1.0f);
		return unit;
	}

	// ─────────────────────────────────────────────────────────────────
	//  유닛 비주얼 생성
	//
	//  구조:
	//    go (루트) ← SmoothMove가 이동시킴, UnitVisual(FOV) 보유
	//    └─ Visual (자식) ← Animator/SpriteRenderer 배치
	//                       애니메이션 클립의 Position/Rotation 커브가
	//                       이 자식의 localTransform을 조작하므로
	//                       루트 position과 충돌하지 않음
	//         └─ Outline
	// ─────────────────────────────────────────────────────────────────
	private void SetupUnitVisual(Unit unit, float visualScale)
	{
		// ── 루트 오브젝트
		GameObject go = new GameObject(unit.name);
		Transform tilemapTransform = GetFloorTilemapTransform(unit.currentFloor);
		if (tilemapTransform != null) go.transform.SetParent(tilemapTransform);
		go.transform.localScale = new Vector3(unit.unitType.footprint.x * visualScale,
		                                      unit.unitType.footprint.y * visualScale, 1f);

		UnitVisual uv = go.AddComponent<UnitVisual>();
		uv.Setup();

		// ── Visual 자식 (애니메이션 대상)
		GameObject visual = new GameObject("Visual");
		visual.transform.SetParent(go.transform);
		visual.transform.localPosition = Vector3.zero;

		SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
		sr.sortingOrder = 10;

#if UNITY_2022_2_OR_NEWER
		if (UnitSpriteManager.Instance != null)
		{
			var spriteLibrary = UnitSpriteManager.Instance.GetSpriteLibrary(unit.unitType);
			if (spriteLibrary != null)
			{
				SpriteLibrary spriteLibComp = visual.AddComponent<SpriteLibrary>();
				spriteLibComp.spriteLibraryAsset = spriteLibrary;

				SpriteResolver spriteResolver = visual.AddComponent<SpriteResolver>();
				UpdateSpriteResolver(spriteResolver, unit.currentDir);

				GameObject outlineGo = new GameObject("Outline");
				outlineGo.transform.SetParent(visual.transform);
				outlineGo.transform.localPosition = Vector3.zero;
				SpriteRenderer outlineSr = outlineGo.AddComponent<SpriteRenderer>();
				outlineSr.sprite = sr.sprite;
				outlineSr.color = Color.black;
				outlineSr.sortingOrder = 9;
				outlineGo.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
				outlineGo.SetActive(false);

				go.transform.position = new Vector3(unit.position.x + unit.unitType.footprint.x / 2f,
				                                    unit.position.y + unit.unitType.footprint.y / 2f, 0)
				                      + GetFloorOffset(unit.currentFloor);
				visualMap[unit] = go;
				AttachAnimationController(visual, unit.unitType.typeName);
				return;
			}
		}
#endif

		// ── 폴백
		if (unit is Human)        sr.sprite = humanSprite;
		else if (unit is Monster) sr.sprite = monsterSprite;

		GameObject outlineGo2 = new GameObject("Outline");
		outlineGo2.transform.SetParent(visual.transform);
		outlineGo2.transform.localPosition = Vector3.zero;
		SpriteRenderer outlineSr2 = outlineGo2.AddComponent<SpriteRenderer>();
		outlineSr2.sprite = sr.sprite;
		outlineSr2.color = Color.black;
		outlineSr2.sortingOrder = 9;
		outlineGo2.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
		outlineGo2.SetActive(false);

		go.transform.position = new Vector3(unit.position.x + unit.unitType.footprint.x / 2f,
		                                    unit.position.y + unit.unitType.footprint.y / 2f, 0)
		                      + GetFloorOffset(unit.currentFloor);
		visualMap[unit] = go;
		AttachAnimationController(visual, unit.unitType.typeName);
	}

	// Animator/UnitAnimationController를 Visual 자식에 부착
	private void AttachAnimationController(GameObject visual, string typeName)
	{
		var ctrl = visual.AddComponent<UnitAnimationController>();
		ctrl.Init(typeName);
	}

	public void UpdateUnitSpriteForDirection(Unit unit)
	{
		if (!visualMap.TryGetValue(unit, out GameObject go))
			return;

#if UNITY_2022_2_OR_NEWER
		// Visual 자식에 있는 SpriteResolver를 검색
		SpriteResolver spriteResolver = go.GetComponentInChildren<SpriteResolver>();
		if (spriteResolver != null)
		{
			UpdateSpriteResolver(spriteResolver, unit.currentDir);

			SpriteRenderer sr = spriteResolver.GetComponent<SpriteRenderer>();

			// Outline은 Visual 자식의 하위에 위치
			Transform outlineTransform = go.transform.Find("Visual/Outline");
			if (outlineTransform != null)
			{
				SpriteRenderer outlineSr = outlineTransform.GetComponent<SpriteRenderer>();
				if (outlineSr != null && sr != null)
				{
					outlineSr.sprite = sr.sprite;
					outlineSr.flipX = sr.flipX;
				}
			}
			return;
		}
#endif
		if (UnitSpriteManager.Instance == null)
			return;
	}

	private void UpdateSpriteResolver(SpriteResolver spriteResolver, Dir direction)
	{
#if UNITY_2022_2_OR_NEWER
		UnitSpriteManager.GetSpriteLabelForDirection(direction, out string category, out string label, out bool flipX);
		spriteResolver.SetCategoryAndLabel(category, label);
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

	public Vector3 GetFloorOffset(int floorIdx)
	{
		var mr = FindObjectOfType<MapRandering>();
		if (mr != null)
		{
			Transform childTilemap = mr.transform.Find($"F{floorIdx}_Tilemap");
			if (childTilemap != null) return childTilemap.position;
			else if (mr.floorOffsets != null && floorIdx < mr.floorOffsets.Length)
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
			if (go != null) Destroy(go);
			visualMap.Remove(u);
			if (moveCoroutines.ContainsKey(u)) moveCoroutines.Remove(u);
			if (targetPosMap.ContainsKey(u)) targetPosMap.Remove(u);
			if (blinkCoroutines.ContainsKey(u)) blinkCoroutines.Remove(u);
		}

		List<Unit> deadKeys = new List<Unit>();
		foreach (var kvp in visualMap)
		{
			if (kvp.Key == null || kvp.Key.hp <= 0)
			{
				if (kvp.Value != null) Destroy(kvp.Value);
				deadKeys.Add(kvp.Key);
			}
		}
		foreach (var deadKey in deadKeys)
		{
			visualMap.Remove(deadKey);
			if (moveCoroutines.ContainsKey(deadKey)) moveCoroutines.Remove(deadKey);
			if (targetPosMap.ContainsKey(deadKey)) targetPosMap.Remove(deadKey);
			if (blinkCoroutines.ContainsKey(deadKey)) blinkCoroutines.Remove(deadKey);
		}
	}

	public void SyncVisuals(List<Unit> units)
	{
		foreach (var u in units)
		{
			if (u == null || !visualMap.TryGetValue(u, out GameObject go)) continue;

			Vector3 newPos = new Vector3(u.position.x + u.unitType.footprint.x / 2f,
			                             u.position.y + u.unitType.footprint.y / 2f, 0)
			               + GetFloorOffset(u.currentFloor);

			Transform targetParent = GetFloorTilemapTransform(u.currentFloor);
			if (targetParent != null && go.transform.parent != targetParent)
				go.transform.SetParent(targetParent);

			if (!targetPosMap.TryGetValue(u, out Vector3 currentTarget) || currentTarget != newPos)
			{
				if (moveCoroutines.TryGetValue(u, out Coroutine existingCoroutine) && existingCoroutine != null)
					StopCoroutine(existingCoroutine);

				float duration = u.walkSpeed > 0f ? (1f / u.walkSpeed) : 0.1f;
				targetPosMap[u] = newPos;
				moveCoroutines[u] = StartCoroutine(SmoothMove(go.transform, newPos, duration));
			}
			else if (Time.timeScale < 0.01f)
			{
				if (!moveCoroutines.ContainsKey(u) || moveCoroutines[u] == null)
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
				bool isSelected = (InputManager.Instance != null && InputManager.Instance.selectedUnit == u);
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

	private IEnumerator SmoothMove(Transform visualTransform, Vector3 targetPos, float duration)
	{
		if (visualTransform == null) yield break;

		Vector3 startPos = visualTransform.position;
		float elapsed = 0f;

		while (elapsed < duration)
		{
			if (visualTransform == null) yield break;
			visualTransform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
			elapsed += Time.unscaledDeltaTime;
			yield return null;
		}

		if (visualTransform != null)
			visualTransform.position = targetPos;
	}

	public void TriggerHitEffect(Unit u)
	{
		if (u != null && visualMap.TryGetValue(u, out GameObject go))
		{
			if (blinkCoroutines.TryGetValue(u, out Coroutine existingCoroutine) && existingCoroutine != null)
				StopCoroutine(existingCoroutine);
			blinkCoroutines[u] = StartCoroutine(HitBlink(u, go));
		}
	}

	private IEnumerator HitBlink(Unit u, GameObject go)
	{
		if (go == null) yield break;
		Transform visualTransform = go.transform.Find("Visual");
		SpriteRenderer sr = visualTransform != null
			? visualTransform.GetComponent<SpriteRenderer>()
			: go.GetComponentInChildren<SpriteRenderer>();
		if (sr == null) yield break;

		Color originalColor = sr.color;

		// 피격 화이트 플래시
		sr.color = Color.white;
		yield return new WaitForSeconds(0.05f);
		if (sr == null) yield break;

		// 3회 깜빡임 (투명 ↔ 원래 색)
		for (int i = 0; i < 3; i++)
		{
			sr.color = Color.clear;
			yield return new WaitForSeconds(0.05f);
			if (sr == null) yield break;
			sr.color = originalColor;
			yield return new WaitForSeconds(0.05f);
			if (sr == null) yield break;
		}

		if (sr != null) sr.color = originalColor;

		if (blinkCoroutines.ContainsKey(u))
			blinkCoroutines.Remove(u);
	}

	private Sprite CreateCircleSprite(Color color)
	{
		Texture2D texture = new Texture2D(32, 32);
		Color[] pixels = new Color[32 * 32];
		float radius = 15f;
		Vector2 center = new Vector2(16f, 16f);
		for (int y = 0; y < 32; y++)
			for (int x = 0; x < 32; x++)
				pixels[y * 32 + x] = Vector2.Distance(center, new Vector2(x, y)) <= radius ? color : Color.clear;
		texture.SetPixels(pixels);
		texture.Apply();
		return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
	}

	private Sprite CreateTriangleSprite(Color color)
	{
		Texture2D texture = new Texture2D(32, 32);
		Color[] pixels = new Color[32 * 32];
		for (int y = 0; y < 32; y++)
		{
			for (int x = 0; x < 32; x++)
			{
				float halfWidth = (1f - y / 31f) * 16f;
				pixels[y * 32 + x] = (x >= 16f - halfWidth && x <= 16f + halfWidth) ? color : Color.clear;
			}
		}
		texture.SetPixels(pixels);
		texture.Apply();
		return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
	}

	public bool IsOccupied(Vector2Int pos, int floorIdx)
	{
		if (GameSession.Instance != null && GameSession.Instance.unitGrid.TryGetValue(new Vector3Int(pos.x, pos.y, floorIdx), out Unit u))
			return u != null && u.hp > 0;
		return false;
	}

	public bool IsAreaClear(Vector2Int pos, Vector2 footprint, int floorIdx)
	{
		CreateMap cmap = (GameSession.Instance != null && GameSession.Instance.cmap != null) ? GameSession.Instance.cmap : FindObjectOfType<CreateMap>();
		if (cmap == null || cmap.map.floors == null || floorIdx < 0 || floorIdx >= cmap.map.floors.Length) return false;

		Floor floor = cmap.map.floors[floorIdx];
		if (floor.chunks == null) return false;

		int chunkW = floor.config.width;
		int chunkH = floor.config.height;
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

				if (cx < 0 || cx >= chunkW || cy < 0 || cy >= chunkH) return false;
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
		CreateMap cmap = (GameSession.Instance != null && GameSession.Instance.cmap != null) ? GameSession.Instance.cmap : FindObjectOfType<CreateMap>();
		if (cmap == null || cmap.map.floors == null || cmap.map.floors.Length == 0) return Vector2Int.zero;
		if (floorIdx < 0 || floorIdx >= cmap.map.floors.Length) return Vector2Int.zero;

		Floor floor = cmap.map.floors[floorIdx];
		if (floor.chunks == null) return Vector2Int.zero;

		int chunkW = floor.config.width;
		int chunkH = floor.config.height;

		for (int i = 0; i < 2000; i++)
		{
			int cx = Random.Range(0, chunkW);
			int cy = Random.Range(0, chunkH);
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
		CreateMap cmap = (GameSession.Instance != null && GameSession.Instance.cmap != null) ? GameSession.Instance.cmap : FindObjectOfType<CreateMap>();
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
		CreateMap cmap = (GameSession.Instance != null && GameSession.Instance.cmap != null) ? GameSession.Instance.cmap : FindObjectOfType<CreateMap>();
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
		unit.name = $"{unitType.typeName}_{pos.x}_{pos.y}_{floorIdx}";
		unit.unitType = unitType;
		unit.position = pos;
		unit.currentFloor = floorIdx;
		unit.SetupStats();

		SetupUnitVisual(unit, 1.0f);
		return unit;
	}
	#endregion
}

public class UnitVisual : MonoBehaviour
{
	public LineRenderer fovLine;

	public void Setup()
	{
		fovLine = gameObject.AddComponent<LineRenderer>();
		fovLine.startWidth = 0.05f;
		fovLine.endWidth = 0.05f;
		fovLine.material = new Material(Shader.Find("Sprites/Default"));
		fovLine.startColor = new Color(0f, 1f, 1f, 0f);
		fovLine.endColor = new Color(0f, 1f, 1f, 0f);
		fovLine.useWorldSpace = false;
		fovLine.sortingOrder = 9;
	}

	public void DrawFOV(float radius, float fovAngle, Vector2 forward)
	{
		int segments = 20;
		fovLine.positionCount = segments + 2;
		fovLine.SetPosition(0, Vector3.zero);

		float startAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - (fovAngle / 2f);
		for (int i = 0; i <= segments; i++)
		{
			float currentAngle = startAngle + (fovAngle * i / segments);
			float rad = currentAngle * Mathf.Deg2Rad;
			fovLine.SetPosition(i + 1, new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius);
		}
	}
}
