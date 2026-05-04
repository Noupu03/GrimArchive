using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class UnitGenerate : MonoBehaviour
{
	public static UnitGenerate Instance;

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

		// Visual 생성 (Update 로직이 없는 깡통 오브젝트)
		GameObject go = new GameObject(unit.name);
		Transform tilemapTransform = GetFloorTilemapTransform(floorIdx);
		if (tilemapTransform != null)
		{
			go.transform.SetParent(tilemapTransform);
		}

		SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
		sr.sortingOrder = 10;

		UnitVisual uv = go.AddComponent<UnitVisual>();
		uv.Setup();

		if (typeof(T) == typeof(Human)) { sr.sprite = humanSprite; sr.color = Color.green; }
		else if (typeof(T) == typeof(Monster)) { sr.sprite = monsterSprite; sr.color = Color.red; }

		GameObject outlineGo = new GameObject("Outline");
		outlineGo.transform.SetParent(go.transform);
		outlineGo.transform.localPosition = Vector3.zero;
		SpriteRenderer outlineSr = outlineGo.AddComponent<SpriteRenderer>();
		outlineSr.sprite = sr.sprite;
		outlineSr.color = Color.black;
		outlineSr.sortingOrder = 9;
		outlineGo.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
		outlineGo.SetActive(false);

		go.transform.position = new Vector3(pos.x + unit.unitType.footprint.x / 2f, pos.y + unit.unitType.footprint.y / 2f, 0) + GetFloorOffset(floorIdx);
		visualMap[unit] = go;

		return unit;
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

	private Vector3 GetFloorOffset(int floorIdx)
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
					if (u.hasArtifact) speed *= 0.5f;
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
					bool isPanicking = u is Human && u.currentMental < u.baseMental * 0.3f;

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

	private System.Collections.IEnumerator SmoothMove(Transform visualTransform, Vector3 targetPos, float duration)//부드럽게 이동하기(그래픽상)
	{
		if (visualTransform == null) yield break;

		Vector3 startPos = visualTransform.position;
		float elapsed = 0f;

		while (elapsed < duration)
		{
			if (visualTransform == null) yield break; // 중간에 파괴된 경우 방어
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

		sr.color = Color.white; // 깜빡임 색상
		yield return new WaitForSeconds(0.025f); // 깜빡이는 간격을 더 짧게 수정
		if (sr != null) 
		{
			sr.color = (u is Human) ? Color.green : Color.red;
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

	public bool IsOccupied_Public(Vector2Int pos, int floorIdx)//해당 위치에 유닛이 존재하는지 여부 반환
	{
		return IsOccupied(pos, floorIdx);
	}

	public Vector3 GetFloorOffset_Public(int floorIdx) => GetFloorOffset(floorIdx);

	private bool IsOccupied(Vector2Int pos, int floorIdx)//해당 위치에 유닛이 존재하는지 여부 반환
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

		// Visual 생성 (Update 로직이 없는 깡통 오브젝트)
		GameObject go = new GameObject(unit.name);
		Transform tilemapTransform = GetFloorTilemapTransform(floorIdx);
		if (tilemapTransform != null)
		{
			go.transform.SetParent(tilemapTransform);
		}

		SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
		sr.sortingOrder = 10;

		UnitVisual uv = go.AddComponent<UnitVisual>();
		uv.Setup();

		if (typeof(T) == typeof(Human)) { sr.sprite = humanSprite; sr.color = Color.green; }
		else if (typeof(T) == typeof(Monster)) { sr.sprite = monsterSprite; sr.color = Color.red; }

		GameObject outlineGo = new GameObject("Outline");
		outlineGo.transform.SetParent(go.transform);
		outlineGo.transform.localPosition = Vector3.zero;
		SpriteRenderer outlineSr = outlineGo.AddComponent<SpriteRenderer>();
		outlineSr.sprite = sr.sprite;
		outlineSr.color = Color.black;
		outlineSr.sortingOrder = 9;
		outlineGo.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
		outlineGo.SetActive(false);

		go.transform.position = new Vector3(pos.x + unit.unitType.footprint.x / 2f, pos.y + unit.unitType.footprint.y / 2f, 0) + GetFloorOffset(floorIdx);
		visualMap[unit] = go;

		return unit;
	}
	#endregion
}

public class GameSession : MonoBehaviour//게임 세션 관리 및 턴 처리(대부분 임시적인 테스트용 요소임
{
	public static GameSession Instance;
	public CreateMap cmap;
	public Dictionary<Vector3Int, Unit> unitGrid = new Dictionary<Vector3Int, Unit>();

	public List<Unit> units = new List<Unit>();
	private float updateTimer = 0f;
	private float textureUpdateTimer = 0f;
	private bool needTextureUpdate = false;

	public float currentGameSpeed = 1f;
	public bool isPaused = false;

	[Header("진영별 맵 시각화 텍셀 (인스펙터에서 클릭하여 확인)")]
	public Texture2D[] humanMapTextures = new Texture2D[4];
	public Texture2D[] monsterMapTextures = new Texture2D[4];

	public void RegisterUnitPos(Unit u, Vector2Int pos)
	{
		if (u == null) return;
		int w = (int)u.unitType.footprint.x;
		int h = (int)u.unitType.footprint.y;
		for (int dx = 0; dx < w; dx++)
		{
			for (int dy = 0; dy < h; dy++)
			{
				unitGrid[new Vector3Int(pos.x + dx, pos.y + dy, u.currentFloor)] = u;
			}
		}
	}

	public void UnregisterUnitPos(Unit u, Vector2Int pos)
	{
		if (u == null) return;
		int w = (int)u.unitType.footprint.x;
		int h = (int)u.unitType.footprint.y;
		for (int dx = 0; dx < w; dx++)
		{
			for (int dy = 0; dy < h; dy++)
			{
				unitGrid.Remove(new Vector3Int(pos.x + dx, pos.y + dy, u.currentFloor));
			}
		}
	}

	void Awake()
	{
		Instance = this;

		if (GetComponent<InputManager>() == null)
		{
			gameObject.AddComponent<InputManager>();
		}
		/*=======파티 관련 참조 주석처리========
		if (GetComponent<PartyController>() == null)
		{
			gameObject.AddComponent<PartyController>();
		}
		*/
		if (GetComponent<ArtifactManager>() == null)
		{
			gameObject.AddComponent<ArtifactManager>();
		}
		if (FindObjectOfType<UIManager>() == null)
		{
			GameObject uiObj = new GameObject("UIManager");
			uiObj.AddComponent<UIManager>();
		}
		if (Camera.main != null && Camera.main.gameObject.GetComponent<CameraController>() == null)
		{
			Camera.main.gameObject.AddComponent<CameraController>();
		}
	}

	void Start()
	{
		cmap = FindObjectOfType<CreateMap>();
		if (cmap != null)
		{
			Unit.humanFactionData.InitMap(cmap);
			Unit.monsterFactionData.InitMap(cmap);
		}
	}

	void Update()
	{
		if (Keyboard.current != null)
		{
			if (Keyboard.current.hKey.wasPressedThisFrame)
			{
				OnKeyDown_H();
			}

			if (Keyboard.current.mKey.wasPressedThisFrame)
			{
				OnKeyDown_M();
			}
		}

		bool visualNeedsSync = false;

		// 턴 액션 처리 후, 씬 상주 시각적 요소들 위치 일괄 동기화
		for (int i = units.Count - 1; i >= 0; i--)
		{
			var u = units[i];
			if (u == null || u.hp <= 0)
			{
				if (u != null && u.hasArtifact) u.DropArtifact();

				if (UnitGenerate.Instance != null && u != null)
				{
					UnitGenerate.Instance.RemoveVisual(u);
				}
				if (u != null) UnregisterUnitPos(u, u.position);
				units.RemoveAt(i);
				if (u != null) Destroy(u);
				visualNeedsSync = true;
				needTextureUpdate = true;
				continue; // 사망/파괴 시 시각적 요소 제거 완료
			}

			u.OnUpdate(Time.deltaTime);

			u.actionCooldown -= Time.deltaTime;
			if (u.actionCooldown <= 0f)
			{
				// 반응도에 의한 지연 대신 속도 비례 쿨다운 (타일당 이동/행동 시간)
				float speed = u.walkSpeed;
				if (u.hasArtifact) speed *= 0.7f; // 유물 운반 시 이동속도 0.7배

				u.actionCooldown = speed > 0f ? (1f / speed) : 1f;

				u.JudgeState(); // 상태 판단 로직 실행
				Vector2Int oldPos = u.position;
				u.ExecuteAction();

				if (oldPos != u.position)
				{
					UnregisterUnitPos(u, oldPos);
					RegisterUnitPos(u, u.position);
				}

				u.UpdateFOV(units); // 행동 후 자신의 시야를 공용 데이터에 갱신

				visualNeedsSync = true;
				needTextureUpdate = true;
			}
		}

		if (visualNeedsSync || Time.timeScale < 0.01f) // 일시정지 상태여도 외부 조작(InputManager 등)에 의한 선택 렌더링 피드백이 즉시 반영되도록 매 프레임 Sync
		{
			if (UnitGenerate.Instance != null)
			{
				UnitGenerate.Instance.SyncVisuals(units);
			}
		}

		// 최적화 3: 텍스처 갱신 쓰로틀링
		textureUpdateTimer += Time.deltaTime;
		if (needTextureUpdate && textureUpdateTimer >= 0.2f)
		{
			UpdateFactionTextures();
			needTextureUpdate = false;
			textureUpdateTimer = 0f;
		}
	}

	public void OnKeyDown_H()//H 키를 눌렀을 때 인간 유닛 생성
	{
		if (UnitGenerate.Instance == null) return;

		UnitType[] types = { new Knight() };
		Vector2Int[] offsets = { new Vector2Int(0,0) };

		// 1층(Floor_1, 인덱스 1)의 StartRoom을 찾아 해당 위치에 고정 파티(기사) 생성
		Vector2Int startPos = UnitGenerate.Instance.GetStartRoomPos(types[0].footprint, 1);

		/*=======파티 관련 참조 주석처리========
		Party newParty = new Party();
		int partyId = PartyController.Instance != null ? PartyController.Instance.activeParties.Count + 1 : 1;
		newParty.partyName = $"Party {partyId}";
		newParty.partyGoal = (PartyGoal)Random.Range(0, 2); // 무작위 목표 지정(테스트용->탐사파티 안나오게 설정해둠.)
		*/

		for (int i = 0; i < types.Length; i++)
		{
			Vector2Int spawnPos = startPos + offsets[i];
			if (!UnitGenerate.Instance.IsAreaClear(spawnPos, types[i].footprint, 1)) spawnPos = UnitGenerate.Instance.GetRandomFloorPos(types[i].footprint, 1); // 혹시 막혀있다면 fallback

			Human human = UnitGenerate.Instance.GenerateUnitAtPos<Human>(types[i], spawnPos, 1);
			units.Add(human);
			//newParty.members.Add(human);=======파티 관련 참조 주석처리========
			if (GameSession.Instance != null) GameSession.Instance.RegisterUnitPos(human, human.position);
			//Debug.Log($"Generated Human: {types[i].typeName} at Floor 1, {human.position} ({newParty.partyName})");=======파티 관련 참조 주석처리========
		}

		//if (PartyController.Instance != null) PartyController.Instance.activeParties.Add(newParty);=======파티 관련 참조 주석처리========
	}

	public void OnKeyDown_M()//M 키를 눌렀을 때 몬스터 유닛 생성
	{
		if (UnitGenerate.Instance == null) return;

		bool hasBoss = false;

		UnitType[] types;
		types = new UnitType[] { new MeleeTank() };

		UnitType selection = types[Random.Range(0, types.Length)];

		// 보스방이 위치한 층을 찾아 해당 층에 스폰 (없으면 1층 기본값)
		int targetFloor = 1;

		Monster monster;
		// 일반 몬스터는 랜덤 층, 랜덤 방 (기존 방식 유지 시 1층 고정이나, 다른 층 확장을 염두에 둘 수도 있음, 일단 1층)
		monster = UnitGenerate.Instance.GenerateUnitAtRandomFloor<Monster>(selection, 1);

		units.Add(monster);
		if (GameSession.Instance != null) GameSession.Instance.RegisterUnitPos(monster, monster.position);
		Debug.Log($"Generated Monster: {selection.typeName} at Floor {monster.currentFloor}, {monster.position}");
	}

	private void UpdateFactionTextures()//인스펙터 꾸미기 관련
	{
		if (cmap == null || cmap.map.floors == null) return;
		int floorCount = cmap.map.floors.Length;
		if (humanMapTextures.Length != floorCount) humanMapTextures = new Texture2D[floorCount];
		if (monsterMapTextures.Length != floorCount) monsterMapTextures = new Texture2D[floorCount];

		for (int f = 0; f < floorCount; f++)
		{
			int mapW = Unit.humanFactionData.discoveredMap[f].GetLength(0);
			int mapH = Unit.humanFactionData.discoveredMap[f].GetLength(1);

			if (humanMapTextures[f] == null || humanMapTextures[f].width != mapW || humanMapTextures[f].height != mapH) { humanMapTextures[f] = new Texture2D(mapW, mapH); humanMapTextures[f].filterMode = FilterMode.Point; }
			if (monsterMapTextures[f] == null || monsterMapTextures[f].width != mapW || monsterMapTextures[f].height != mapH) { monsterMapTextures[f] = new Texture2D(mapW, mapH); monsterMapTextures[f].filterMode = FilterMode.Point; }

			Color[] hPixels = new Color[mapW * mapH];
			Color[] mPixels = new Color[mapW * mapH];

			for (int y = 0; y < mapH; y++)
			{
				for (int x = 0; x < mapW; x++)
				{
					// 0: 미탐색(검은색), 1: 바닥(흰색), 2: 벽(회색)
					int hVal = Unit.humanFactionData.discoveredMap[f][x, y];
					hPixels[y * mapW + x] = hVal == 1 ? Color.white : (hVal == 2 ? Color.gray : Color.black);

					int mVal = Unit.monsterFactionData.discoveredMap[f][x, y];
					mPixels[y * mapW + x] = mVal == 1 ? Color.white : (mVal == 2 ? Color.gray : Color.black);
				}
			}

			// 자기 진영 유닛 현재 위치 정보 초록색으로 덧씌우기
			foreach (var u in units)
			{
				if (u == null || u.currentFloor != f) continue;
				int idx = u.position.y * mapW + u.position.x;
				if (u.position.x >= 0 && u.position.x < mapW && u.position.y >= 0 && u.position.y < mapH)
				{
					if (u is Human)
					{
						hPixels[idx] = Color.green;
						// 인간에게 발견된 적 오크는 빨간색으로 표시
						if (Unit.humanFactionData.spottedEnemyUnits.Contains(u)) hPixels[idx] = Color.red;
					}
					else if (u is Monster)
					{
						mPixels[idx] = Color.yellow;
						// 몬스터에게 발견된 적 인간은 파란색으로 표시
						if (Unit.monsterFactionData.spottedEnemyUnits.Contains(u)) mPixels[idx] = Color.blue;
					}
				}
			}

			// 상대 진영 발견 유닛 정보 덧씌우기
			foreach (var enemy in Unit.humanFactionData.spottedEnemyUnits)
			{
				if (enemy == null || enemy.currentFloor != f) continue;
				int idx = enemy.position.y * mapW + enemy.position.x;
				if (enemy.position.x >= 0 && enemy.position.x < mapW && enemy.position.y >= 0 && enemy.position.y < mapH) hPixels[idx] = Color.red;
			}

			foreach (var enemy in Unit.monsterFactionData.spottedEnemyUnits)
			{
				if (enemy == null || enemy.currentFloor != f) continue;
				int idx = enemy.position.y * mapW + enemy.position.x;
				if (enemy.position.x >= 0 && enemy.position.x < mapW && enemy.position.y >= 0 && enemy.position.y < mapH) mPixels[idx] = Color.blue;
			}

			humanMapTextures[f].SetPixels(hPixels);
			humanMapTextures[f].Apply();

			monsterMapTextures[f].SetPixels(mPixels);
			monsterMapTextures[f].Apply();
		}
	}
}

#if UNITY_EDITOR//인스펙터 꾸미기
[CustomEditor(typeof(GameSession))]
public class GameSessionEditor : Editor
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

		GameSession gs = (GameSession)target;

		int maxFloor = gs.humanMapTextures != null ? gs.humanMapTextures.Length : 0;
		for (int f = 0; f < maxFloor; f++)
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField($"[{f}층] 인류 / 몬스터 맵", EditorStyles.boldLabel);

			EditorGUILayout.BeginHorizontal();
			if (gs.humanMapTextures != null && gs.humanMapTextures.Length > f && gs.humanMapTextures[f] != null)
			{
				Rect rect1 = GUILayoutUtility.GetRect(128, 128);
				GUI.DrawTexture(rect1, gs.humanMapTextures[f], ScaleMode.ScaleToFit);
			}
			if (gs.monsterMapTextures != null && gs.monsterMapTextures.Length > f && gs.monsterMapTextures[f] != null)
			{
				Rect rect2 = GUILayoutUtility.GetRect(128, 128);
				GUI.DrawTexture(rect2, gs.monsterMapTextures[f], ScaleMode.ScaleToFit);
			}
			EditorGUILayout.EndHorizontal();
		}
	}
}
#endif

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

