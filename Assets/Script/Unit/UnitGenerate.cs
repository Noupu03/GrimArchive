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

	public T GenerateUnitAtRandomFloor<T>(UnitType unitType, int floorIdx = 1) where T : Unit//유닛 생성 로직
	{
		Vector2Int pos;
		pos = GetRandomFloorPos(unitType.footprint, floorIdx);

		T unit = ScriptableObject.CreateInstance<T>();
		unit.name = $"{unitType.typeName}_{pos.x}_{pos.y}_{floorIdx}";
		unit.unitType = unitType;
		unit.position = pos;
		unit.currentFloor = floorIdx;
		unit.SetupStats();

		SetupUnitVisual(unit, 2.7f);  // 0.9f * 3 = 2.7f

		return unit;
	}

	private void SetupUnitVisual(Unit unit, float visualScale)
	{
		GameObject go = new GameObject(unit.name);
		Transform tilemapTransform = GetFloorTilemapTransform(unit.currentFloor);
		if (tilemapTransform != null) go.transform.SetParent(tilemapTransform);

		SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
		sr.sortingOrder = 10;

		UnitVisual uv = go.AddComponent<UnitVisual>();
		uv.Setup();

		go.transform.localScale = new Vector3(unit.unitType.footprint.x * visualScale, unit.unitType.footprint.y * visualScale, 1f);

#if UNITY_2022_2_OR_NEWER
		// Sprite Library 기반 설정
		if (UnitSpriteManager.Instance != null)
		{
			var spriteLibrary = UnitSpriteManager.Instance.GetSpriteLibrary(unit.unitType);
			if (spriteLibrary != null)
			{
				SpriteLibrary spriteLibComp = go.AddComponent<SpriteLibrary>();
				spriteLibComp.spriteLibraryAsset = spriteLibrary;

				SpriteResolver spriteResolver = go.AddComponent<SpriteResolver>();
				UpdateSpriteResolver(spriteResolver, unit.currentDir);

				// SpriteResolver가 설정되면 SpriteRenderer의 sprite가 자동으로 업데이트됨
				// 약간의 지연이 필요할 수 있으므로 다음 프레임에 업데이트를 확인합니다.

				// 아웃라인도 함께 설정
				GameObject outlineGo = new GameObject("Outline");
				outlineGo.transform.SetParent(go.transform);
				outlineGo.transform.localPosition = Vector3.zero;
				SpriteRenderer outlineSr = outlineGo.AddComponent<SpriteRenderer>();
				outlineSr.sprite = sr.sprite;
				outlineSr.color = Color.black;
				outlineSr.sortingOrder = 9;
				outlineGo.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
				outlineGo.SetActive(false);

				go.transform.position = new Vector3(unit.position.x + unit.unitType.footprint.x / 2f, unit.position.y + unit.unitType.footprint.y / 2f, 0) + GetFloorOffset(unit.currentFloor);
				visualMap[unit] = go;
				return;
			}
		}
#endif

		// 폴백: Sprite Library를 사용하지 않는 경우 또는 찾을 수 없는 경우
		Sprite sprite = null;

		// 폴백: SpriteManager가 없거나 스프라이트를 못 찾은 경우
		if (sprite == null)
		{
			if (unit is Human) { sprite = humanSprite; }
			else if (unit is Monster) { sprite = monsterSprite; }
		}

		sr.sprite = sprite;

		GameObject outlineGo2 = new GameObject("Outline");
		outlineGo2.transform.SetParent(go.transform);
		outlineGo2.transform.localPosition = Vector3.zero;
		SpriteRenderer outlineSr2 = outlineGo2.AddComponent<SpriteRenderer>();
		outlineSr2.sprite = sprite;
		outlineSr2.color = Color.black;
		outlineSr2.sortingOrder = 9;
		outlineGo2.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
		outlineGo2.SetActive(false);

		go.transform.position = new Vector3(unit.position.x + unit.unitType.footprint.x / 2f, unit.position.y + unit.unitType.footprint.y / 2f, 0) + GetFloorOffset(unit.currentFloor);
		visualMap[unit] = go;
	}

	public void UpdateUnitSpriteForDirection(Unit unit)
	{
		if (!visualMap.TryGetValue(unit, out GameObject go))
			return;

#if UNITY_2022_2_OR_NEWER
		SpriteResolver spriteResolver = go.GetComponent<SpriteResolver>();
		if (spriteResolver != null)
		{
			UpdateSpriteResolver(spriteResolver, unit.currentDir);

			SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
			if (sr != null)
			{
				// SpriteResolver가 업데이트되면 SpriteRenderer의 sprite가 자동으로 갱신됨
			}

			// 아웃라인도 함께 업데이트
			Transform outlineTransform = go.transform.Find("Outline");
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

		// 폴백: SpriteResolver가 없는 경우 (레거시 방식)
		if (UnitSpriteManager.Instance == null)
			return;

		// 여기에 레거시 스프라이트 업데이트 로직을 추가할 수 있습니다
	}

	/// <summary>
	/// SpriteResolver의 Category와 Label을 방향에 따라 업데이트합니다.
	/// </summary>
	private void UpdateSpriteResolver(SpriteResolver spriteResolver, Dir direction)
	{
#if UNITY_2022_2_OR_NEWER
		UnitSpriteManager.GetSpriteLabelForDirection(direction, out string category, out string label, out bool flipX);

		spriteResolver.SetCategoryAndLabel(category, label);

		SpriteRenderer sr = spriteResolver.GetComponent<SpriteRenderer>();
		if (sr != null)
		{
			sr.flipX = flipX;
		}
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
			if (childTilemap != null)
			{
				return childTilemap;
			}
		}
		return null;
	}

	public Vector3 GetFloorOffset(int floorIdx)
	{
		var mr = FindObjectOfType<MapRandering>();
		if (mr != null)
		{
			Transform childTilemap = mr.transform.Find($"F{floorIdx}_Tilemap");
			if (childTilemap != null)
			{
				return childTilemap.position;
			}
			else if (mr.floorOffsets != null && floorIdx < mr.floorOffsets.Length)
			{
				Vector3Int offset = mr.floorOffsets[floorIdx];
				return mr.transform.position + new Vector3(offset.x, offset.y, 0f);
			}
		}
		return Vector3.zero;
	}

	public void RemoveVisual(Unit u)//스프라이트 지우기
	{
		if (u != null && visualMap.TryGetValue(u, out GameObject go))
		{
			if (go != null) Destroy(go);
			visualMap.Remove(u);
			if (moveCoroutines.ContainsKey(u)) moveCoroutines.Remove(u);
			if (targetPosMap.ContainsKey(u)) targetPosMap.Remove(u);
			if (blinkCoroutines.ContainsKey(u)) blinkCoroutines.Remove(u);
		}

		// 안전장치: 이미 ScriptableObject가 파괴되어 Unity Null 처리가 된 키값들을 딕셔너리에서 일괄 제거
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

	public void SyncVisuals(List<Unit> units)//비주얼화 코루틴 시작
	{
		foreach (var u in units)
		{
			if (u != null && visualMap.TryGetValue(u, out GameObject go))
			{
				Vector3 newPos = new Vector3(u.position.x + u.unitType.footprint.x / 2f, u.position.y + u.unitType.footprint.y / 2f, 0) + GetFloorOffset(u.currentFloor);

				Transform targetParent = GetFloorTilemapTransform(u.currentFloor);
				if (targetParent != null && go.transform.parent != targetParent)
				{
					go.transform.SetParent(targetParent);
				}

				// 매 프레임 위치를 확인하고 변경 시 이동 코루틴 갱신 유지
				if (!targetPosMap.TryGetValue(u, out Vector3 currentTarget) || currentTarget != newPos)
				{
					if (moveCoroutines.TryGetValue(u, out Coroutine existingCoroutine) && existingCoroutine != null)
					{
						StopCoroutine(existingCoroutine);
					}

					float speed = u.walkSpeed;
					float duration = speed > 0f ? (1f / speed) : 0.1f;

					targetPosMap[u] = newPos;
					moveCoroutines[u] = StartCoroutine(SmoothMove(go.transform, newPos, duration)); // 논리 갱신 속도에 맞춤
				}
				else if (Time.timeScale == 0f || Time.timeScale < 0.01f)
				{
				    // 일시정지 상태 등 업데이트가 멈춘 상태에서도 아웃라인/상태 반영이 필요할 수 있으므로, 보간 중이 아니라면 바로 위치 맞춤
				    if (!moveCoroutines.ContainsKey(u) || moveCoroutines[u] == null)
				        go.transform.position = newPos;
				}

				// 시각화 업데이트 (FOV)
				UnitVisual uv = go.GetComponent<UnitVisual>();
				if (uv != null)
				{
					Vector2 forward = u.GetDirVector(u.currentDir);
					if (forward == Vector2.zero) forward = Vector2.down;
					uv.DrawFOV(Unit.ViewRadius, 160f, forward);
				}

				Transform outlineTransform = go.transform.Find("Outline");
				if (outlineTransform != null)
				{
					bool isSelected = (InputManager.Instance != null && InputManager.Instance.selectedUnit == u);
					bool isPanicking = u is Human && u.mental < u.maxMental * 0.3f;

					outlineTransform.gameObject.SetActive(isSelected || isPanicking);

					if (isSelected || isPanicking)
					{
						SpriteRenderer outlineSr = outlineTransform.GetComponent<SpriteRenderer>();
						if (outlineSr != null)
						{
							outlineSr.color = isSelected ? Color.black : Color.red;
						}
					}
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

			visualTransform.position =
				Vector3.Lerp(startPos, targetPos, elapsed / duration);

			elapsed += Time.unscaledDeltaTime;
			yield return null;
		}

		// 핵심: 반드시 최종 좌표 강제 보정
		if (visualTransform != null)
		{
			visualTransform.position = targetPos;
		}
	}

	public void TriggerHitEffect(Unit u)
	{
		if (u != null && visualMap.TryGetValue(u, out GameObject go))
		{
			if (blinkCoroutines.TryGetValue(u, out Coroutine existingCoroutine) && existingCoroutine != null)
			{
				StopCoroutine(existingCoroutine);
			}
			blinkCoroutines[u] = StartCoroutine(HitBlink(u, go));
		}
	}

	private System.Collections.IEnumerator HitBlink(Unit u, GameObject go)
	{
		if (go == null) yield break;
		SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
		if (sr == null) yield break;

		Color originalColor = sr.color;
		sr.color = Color.white; // 하얀색 깜빡임
		yield return new WaitForSeconds(0.1f); // 하얀색 유지 시간
		if (sr != null)
		{
			sr.color = originalColor; // 원래 색으로 복구
		}

		if (blinkCoroutines.ContainsKey(u))
		{
			blinkCoroutines.Remove(u);
		}
	}

	private Sprite CreateCircleSprite(Color color)//원형 스프라이트 생성(임시) - 실제 프로젝트에서는 에셋으로 대체하는 것을 권장
	{
		Texture2D texture = new Texture2D(32, 32);
		Color[] pixels = new Color[32 * 32];
		float radius = 15f;
		Vector2 center = new Vector2(16f, 16f);
		for (int y = 0; y < 32; y++)
		{
			for (int x = 0; x < 32; x++)
			{
				if (Vector2.Distance(center, new Vector2(x, y)) <= radius)
					pixels[y * 32 + x] = color;
				else
					pixels[y * 32 + x] = Color.clear;
			}
		}
		texture.SetPixels(pixels);
		texture.Apply();
		return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
	}

	private Sprite CreateTriangleSprite(Color color)//삼각형 스프라이트 생성(임시) - 실제 프로젝트에서는 에셋으로 대체하는 것을 권장
	{
		Texture2D texture = new Texture2D(32, 32);
		Color[] pixels = new Color[32 * 32];
		for (int y = 0; y < 32; y++)
		{
			for (int x = 0; x < 32; x++)
			{
				float normalizedY = y / 31f;
				float halfWidth = (1f - normalizedY) * 16f;
				if (x >= 16f - halfWidth && x <= 16f + halfWidth)
					pixels[y * 32 + x] = color;
				else
					pixels[y * 32 + x] = Color.clear;
			}
		}
		texture.SetPixels(pixels);
		texture.Apply();
		return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
	}

	public bool IsOccupied(Vector2Int pos, int floorIdx)//해당 위치에 유닛이 존재하는지 여부 반환
	{
		if (GameSession.Instance != null && GameSession.Instance.unitGrid.TryGetValue(new Vector3Int(pos.x, pos.y, floorIdx), out Unit u))
		{
			return u != null && u.hp > 0;
		}
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

				int cx = x / 8;
				int cy = y / 8;
				int tx = x % 8;
				int ty = y % 8;

				if (cx < 0 || cx >= chunkW || cy < 0 || cy >= chunkH) return false;
				Chunks c = floor.chunks[cx, cy];
				if (c.roomId == -1 || c.chunk == null) return false;
				if (c.chunk[tx, ty].name == "Wall") return false;

				if (IsOccupied(new Vector2Int(x, y), floorIdx)) return false;
			}
		}
		return true;
	}

	public Vector2Int GetRandomFloorPos(Vector2 footprint, int floorIdx = 1)//랜덤한 바닥 위치 반환(임시)
	{
		CreateMap cmap = (GameSession.Instance != null && GameSession.Instance.cmap != null) ? GameSession.Instance.cmap : FindObjectOfType<CreateMap>();
		if (cmap == null || cmap.map.floors == null || cmap.map.floors.Length == 0) return Vector2Int.zero;

		if (floorIdx < 0 || floorIdx >= cmap.map.floors.Length) return Vector2Int.zero;

		Floor floor = cmap.map.floors[floorIdx];
		if (floor.chunks == null) return Vector2Int.zero;

		int chunkW = floor.config.width;
		int chunkH = floor.config.height;

		// 아무 방(roomId != -1)에나 랜덤 생성
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
		return Vector2Int.zero; // default fallback
	}

	public Vector2Int GetStartRoomPos(Vector2 footprint, int floorIdx = 1)
	{
		CreateMap cmap = (GameSession.Instance != null && GameSession.Instance.cmap != null) ? GameSession.Instance.cmap : FindObjectOfType<CreateMap>();
		if (cmap == null || cmap.map.floors == null || floorIdx < 0 || floorIdx >= cmap.map.floors.Length) return Vector2Int.zero;

		Floor floor = cmap.map.floors[floorIdx];
		if (floor.chunks == null) return Vector2Int.zero;

		int chunkW = floor.config.width;
		int chunkH = floor.config.height;

		for (int cx = 0; cx < chunkW; cx++)
		{
			for (int cy = 0; cy < chunkH; cy++)
			{
				Chunks c = floor.chunks[cx, cy];
				if (c.roomRole == RoomRole.StartRoom && c.chunk != null)
				{
					// 시작방을 찾았으면 무작위가 아닌 중앙 근처 빈 공간 반환
					for (int tx = 2; tx < 6; tx++)
					{
						for (int ty = 2; ty < 6; ty++)
						{
							Vector2Int cand = new Vector2Int(cx * 8 + tx, cy * 8 + ty);
							if (IsAreaClear(cand, footprint, floorIdx)) return cand;
						}
					}
				}
			}
		}
		return GetRandomFloorPos(footprint, floorIdx); // 못 찾으면 일반 랜덤 방 반환
	}

	public Vector2Int GetBossRoomPos(Vector2 footprint, int floorIdx = 1)
	{
		CreateMap cmap = (GameSession.Instance != null && GameSession.Instance.cmap != null) ? GameSession.Instance.cmap : FindObjectOfType<CreateMap>();
		if (cmap == null || cmap.map.floors == null || floorIdx < 0 || floorIdx >= cmap.map.floors.Length) return Vector2Int.zero;

		Floor floor = cmap.map.floors[floorIdx];
		if (floor.chunks == null) return Vector2Int.zero;

		int chunkW = floor.config.width;
		int chunkH = floor.config.height;

		for (int cx = 0; cx < chunkW; cx++)
		{
			for (int cy = 0; cy < chunkH; cy++)
			{
				Chunks c = floor.chunks[cx, cy];
				if (c.roomRole == RoomRole.BossRoom && c.chunk != null)
				{
					for (int tx = 2; tx < 6; tx++)
					{
						for (int ty = 2; ty < 6; ty++)
						{
							Vector2Int cand = new Vector2Int(cx * 8 + tx, cy * 8 + ty);
							if (IsAreaClear(cand, footprint, floorIdx)) return cand;
						}
					}
				}
			}
		}
		return GetRandomFloorPos(footprint, floorIdx); // 못 찾으면 일반 랜덤 방 반환
	}


	public T GenerateUnitAtPos<T>(UnitType unitType, Vector2Int pos, int floorIdx = 1) where T : Unit
	{
		T unit = ScriptableObject.CreateInstance<T>();
		unit.name = $"{unitType.typeName}_{pos.x}_{pos.y}_{floorIdx}";
		unit.unitType = unitType;
		unit.position = pos;
		unit.currentFloor = floorIdx;
		unit.SetupStats();

		SetupUnitVisual(unit, 3.0f);  // 1.0f * 3 = 3.0f

		return unit;
	}
	#endregion
}

public class UnitVisual : MonoBehaviour//유닛 시야 시각화
{
	public LineRenderer fovLine;

	public void Setup()
	{
		fovLine = gameObject.AddComponent<LineRenderer>();
		fovLine.startWidth = 0.05f;
		fovLine.endWidth = 0.05f;
		fovLine.material = new Material(Shader.Find("Sprites/Default")); // 기본 2D 쉐이더로 단색 표시
		fovLine.startColor = new Color(0f, 1f, 1f, 0f); // 하늘색 반투명
		fovLine.endColor = new Color(0f, 1f, 1f, 0f);
		fovLine.useWorldSpace = false; // 부모(유닛) 기준 좌표
		fovLine.sortingOrder = 9;
	}

	public void DrawFOV(float radius, float fovAngle, Vector2 forward)
	{
		int segments = 20;
		fovLine.positionCount = segments + 2;

		fovLine.SetPosition(0, Vector3.zero); // 본인 위치 중심

		float startAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - (fovAngle / 2f);

		for (int i = 0; i <= segments; i++)
		{
			float currentAngle = startAngle + (fovAngle * i / segments);
			float rad = currentAngle * Mathf.Deg2Rad;
			Vector3 point = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius;
			fovLine.SetPosition(i + 1, point);
		}
	}
}

