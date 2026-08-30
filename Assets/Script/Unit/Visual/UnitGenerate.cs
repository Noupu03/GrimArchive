using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
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

	// 우측 상단 UI(DebugInfoPanel)의 "시야 표시" 토글이 켜고 끄는 전역 스위치 — 켜져 있으면 선택
	// 여부와 무관하게 모든 유닛의 시야/인지 범위를 SyncVisuals가 표시한다(SetVisionRangesVisible 참고).
	public bool ShowAllVisionRanges = false;

	// debug 메뉴 "유닛 상태 표시" 토글 — 꺼지면 유닛 머리 위 FSM 상태 라벨(SyncVisual의
	// uv.UpdateStatusLabel 호출부 참고)을 전부 숨긴다. 기본은 꺼짐 — 필요할 때 debug 메뉴에서 켠다.
	public bool ShowUnitStatusLabels = false;

	// 명령 경로 시각화(2026-08-24) — BuildCachedPathPreview가 따라갈 최대 waypoint 수. 정상적인 경로는
	// 이보다 훨씬 짧다 — 캐시가 어긋난 극단적 상황에서 무한 루프/과도한 LineRenderer 포인트를 막는 안전판.
	private const int CommandPathMaxSteps = 200;

	// 선택 표시용 발밑 링(SelectionMarker) 관련 상수. 캐릭터 스프라이트/애니메이션과 완전히
	// 무관하게(풋프린트 크기만으로 계산) 발밑에 깔리는 납작한 타원 링을 스타크래프트식으로 그린다.
	private const float SelectionRingDiameterRatio = 1.15f; // 풋프린트 대비 링 지름 배율
	private const float SelectionRingFlatten       = 0.5f;  // 세로로 납작하게 누르는 비율(원→타원)
	private const float SelectionRingFootOffset    = 0.06f; // 발밑에서 살짝 띄우는 정도(월드 유닛)
	private const int   SelectionMarkerSortingOrder = 9;    // 캐릭터(보통 10)보다 한 칸 아래(발밑에 깔림)
	private const int   SelectionRingTextureSize   = 64;
	private const float SelectionRingInnerRatio    = 0.62f; // 안쪽 반지름 비율(0~1) — 클수록 얇은 링
	private const float SelectionRingOuterRatio    = 0.95f; // 바깥쪽 반지름 비율(0~1)
	// 진영별 링 색상 — 인류는 파란색, 몬스터는 빨간색.
	private static readonly Color SelectionRingColorHuman   = new Color(0.2f, 0.45f, 1f, 1f);
	private static readonly Color SelectionRingColorMonster = new Color(1f, 0.2f, 0.2f, 1f);

	private static Sprite _selectionRingSprite;
	private static Sprite SelectionRingSprite
	{
		get
		{
			if (_selectionRingSprite == null)
				_selectionRingSprite = CreateRingSprite(
					SelectionRingTextureSize, SelectionRingInnerRatio, SelectionRingOuterRatio);
			return _selectionRingSprite;
		}
	}

	private static Sprite CreateRingSprite(int size, float innerRatio, float outerRatio)
	{
		Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
		Color[] pixels = new Color[size * size];
		Vector2 center = new Vector2(size / 2f, size / 2f);
		float outerR = size / 2f * outerRatio;
		float innerR = size / 2f * innerRatio;
		const float aa = 1.25f; // 가장자리 부드럽게(안티에일리어싱) 처리할 픽셀 폭

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
				float alphaOuter = Mathf.Clamp01((outerR - d) / aa + 0.5f);
				float alphaInner = Mathf.Clamp01((d - innerR) / aa + 0.5f);
				pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Min(alphaOuter, alphaInner));
			}
		}

		tex.SetPixels(pixels);
		tex.Apply();
		return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
	}

	private class VisualCache
	{
		public UnitVisual UnitVisual;
		public UnitVisualDefinition UnitVisualDefinition;
		public WeaponAttachment WeaponAttachment;
		public SpriteRenderer SelectionMarker;
		// 횃불 위 예외 처리(아래 SyncVisual 참고)용 캐시.
		public ShadowCaster2D ShadowCaster;
#if UNITY_2022_2_OR_NEWER
		public SpriteResolver SpriteResolver;
#endif
		// 시야/인지 범위 콘 다시 그리기 여부 판단용 — DrawVisionAndPerceptionRange가 매번 삼각함수
		// 계산+LineRenderer 갱신을 도는 비용이 커서, "보이는 상태 + 방향이 바뀌었을 때"에만 다시 그린다.
		public bool VisionRangeShown;
		public Vector2 LastVisionForward;
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
			cache.ShadowCaster = go.GetComponentInChildren<ShadowCaster2D>();
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
			_cachedMapRandering = Session != null ? Session.mapRandering : null;
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
		// 스프라이트 아트가 이미 footprint 배율로 그려진 유닛(보스 골렘 등)은 footprint를 시각적
		// 확대에 다시 곱하지 않는다 — footprint 값 자체는 다른 곳(스탯/인구수/충돌 판정)에는 그대로 쓰인다.
		float scaleX = visualDef != null && visualDef.visualScaleIgnoresFootprint ? visualScale : unit.unitType.footprint.x * visualScale;
		float scaleY = visualDef != null && visualDef.visualScaleIgnoresFootprint ? visualScale : unit.unitType.footprint.y * visualScale;
		go.transform.localScale = new Vector3(scaleX, scaleY, 1f);

		UnitVisual uv = go.GetComponent<UnitVisual>();
		if (uv == null) uv = go.AddComponent<UnitVisual>();
		uv.Setup(unit is Human); // 진영별 시야/인지 범위 색 팔레트 선택(UnitVisual.Setup 참고)
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

		// 발밑 선택 링을 스폰 시점에 미리 만들어둔다 — 웨이브 스폰 직후 여러 유닛이 같은 프레임에
		// EnsureSelectionMarker 생성 비용을 몰아서 쓰면 프레임 드랍이 생기므로, 한 유닛씩 처리되는
		// 스폰 시점에 만들어 비용을 분산시킨다.
		EnsureSelectionMarker(cache, go, unit);

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

	// 외부(SkillAction 등)에서 유닛 타입별 이펙트/시체 스프라이트 등을 조회할 수 있게 하는 공개 창구
	// (2026-08-24 신규) — GetVisualDef 자체는 private으로 유지하고, 꼭 필요한 만큼만 공개한다.
	public UnitVisualDefinition GetVisualDefinition(Unit u) => GetVisualDef(u);

	public void SpawnGuardVFX(Unit defender, Unit attacker = null)
	{
		if (defender == null) return;
		GameObject prefab = GetVisualDef(defender)?.guardPrefab;
		if (prefab == null) return;

		Transform parent = GetVisualTransform(defender);
		Vector3 defPos = parent != null ? parent.position : VFXManager.GetWorldPos(defender);
		Quaternion rotation = Quaternion.identity;

		if (attacker != null)
		{
			Transform atkTrans = GetVisualTransform(attacker);
			Vector3 atkPos = atkTrans != null ? atkTrans.position : VFXManager.GetWorldPos(attacker);
			Vector3 dir = atkPos - defPos;
			if (dir.sqrMagnitude > 0.0001f)
			{
				float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
				rotation = Quaternion.Euler(0f, 0f, angle);
			}
		}
		else
		{
			Vector2 dir = defender.GetDirVector(defender.currentDir);
			if (dir.sqrMagnitude > 0.0001f)
			{
				float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
				rotation = Quaternion.Euler(0f, 0f, angle);
			}
		}

		VFXManager.Spawn(prefab, defPos, rotation, parent);
	}

	public void SpawnParryVFX(Unit defender, Unit attacker = null)
	{
		if (defender == null) return;
		GameObject prefab = GetVisualDef(defender)?.parryPrefab;
		if (prefab == null) return;

		Transform parent = GetVisualTransform(defender);
		Vector3 defPos = parent != null ? parent.position : VFXManager.GetWorldPos(defender);
		Quaternion rotation = Quaternion.identity;

		if (attacker != null)
		{
			Transform atkTrans = GetVisualTransform(attacker);
			Vector3 atkPos = atkTrans != null ? atkTrans.position : VFXManager.GetWorldPos(attacker);
			Vector3 dir = atkPos - defPos;
			if (dir.sqrMagnitude > 0.0001f)
			{
				float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
				rotation = Quaternion.Euler(0f, 0f, angle);
			}
		}
		else
		{
			Vector2 dir = defender.GetDirVector(defender.currentDir);
			if (dir.sqrMagnitude > 0.0001f)
			{
				float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
				rotation = Quaternion.Euler(0f, 0f, angle);
			}
		}

		VFXManager.Spawn(prefab, defPos, rotation, parent);
	}

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
		UnitSpriteManager.ApplySpriteResolverLabel(spriteResolver, variation, label, flipX);
#else
		LogHelper.Error(LogHelper.GAME, "SpriteResolver는 Unity 2022.2 이상에서 지원됩니다.");
#endif
	}

	#region 유닛 생성 보조 기능성

	// 계층 정리 — 유닛은 해당 층의 "Units" 하위 그룹(GameSession.GetFloorCategoryGroup)에 묶는다.
	// 실패(GameSession 아직 준비 안 됨 등) 시 타일맵 자체로 폴백.
	private Transform GetFloorTilemapTransform(int floorIdx)
	{
		Transform unitsGroup = Session?.GetFloorCategoryGroup(floorIdx, "Units");
		if (unitsGroup != null) return unitsGroup;

		var mr = GetMapRandering();
		if (mr != null && mr.floorTilemaps != null && floorIdx >= 0 && floorIdx < mr.floorTilemaps.Length)
		{
			var childTilemap = mr.floorTilemaps[floorIdx];
			if (childTilemap != null) return childTilemap.transform;
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
			if (mr.floorTilemaps != null && floorIdx >= 0 && floorIdx < mr.floorTilemaps.Length)
			{
				var childTilemap = mr.floorTilemaps[floorIdx];
				if (childTilemap != null) return childTilemap.transform.position;
			}
			if (mr.floorOffsets != null && floorIdx >= 0 && floorIdx < mr.floorOffsets.Length)
			{
				Vector3Int offset = mr.floorOffsets[floorIdx];
				return new Vector3(offset.x, offset.y, 0f);
			}
		}
		return Vector3.zero;
	}

	// GameSession.RemoveDeadUnit이 units 리스트에서 뺀 그 자리에서 곧장 이 메서드를 호출해 visualMap
	// 에서도 함께 제거한다 — 모든 사망이 명시적으로 호출하므로 별도 안전망 스윕은 필요 없다.
	public void RemoveVisual(Unit u)
	{
		if (u == null || !visualMap.TryGetValue(u, out GameObject go)) return;
		if (go != null) { KillVisualTweens(go); Object.Destroy(go); _cacheMap.Remove(go); }
		visualMap.Remove(u);
		targetPosMap.Remove(u);
	}

	// 사망 VFX — GameSession.RemoveDeadUnit이 사망 판정 직후, 시체 오브젝트를 스폰하고 이 유닛의
	// 비주얼을 파괴하기 직전에 호출한다. 사망 판정 즉시 시체 오브젝트로 전환되므로 별도 Death
	// 스프라이트 단계 없이 VFX만 1회 재생하고 끝난다.
	public void PlayDeathVisual(Unit u)
	{
		if (u == null || !visualMap.TryGetValue(u, out GameObject go) || go == null) return;

		var cache = GetCache(go);
		var def = cache.UnitVisualDefinition;
		if (def == null || def.deathVfxPrefab == null) return;

		// 유닛에 부모로 붙는 2-인자 Spawn 오버로드를 쓰면 안 된다 — RemoveDeadUnit이 이 메서드 호출
		// 직후 RemoveVisual(u)로 비주얼 GameObject를 Destroy해 자식 VFX도 함께 파괴된다. 고정 월드
		// 좌표에 부모 없이 스폰해 유닛 비주얼 생명주기와 분리한다.
		VFXManager.Spawn(def.deathVfxPrefab, VFXManager.GetWorldPos(u), Quaternion.identity);
	}

	// go.transform.DOKill()만으로는 TriggerHitEffect()가 sr을 타겟으로 만든 DOTween 시퀀스가 안
	// 죽는다(타겟이 transform이 아니라 sr이라 별개 트윈) — 킬샷 시 파괴된 SpriteRenderer를 계속
	// 건드리려다 DOTween "missing target" 에러가 나는 원인이었다.
	private void KillVisualTweens(GameObject go)
	{
		go.transform.DOKill();
		Transform vt = go.transform.Find("Visual");
		SpriteRenderer sr = vt != null ? vt.GetComponent<SpriteRenderer>() : go.GetComponentInChildren<SpriteRenderer>();
		if (sr != null) sr.DOKill();
	}

	public void SyncVisual(Unit u)
	{
		if (u == null || !visualMap.TryGetValue(u, out GameObject go)) return;

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
				float duration  = u.BaseStat.walkSpeed > 0f ? (1f / u.BaseStat.walkSpeed) : 0.1f;
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

			// 횃불 위 유닛 빛 투과 예외 — 유닛이 서 있는 타일 중 하나라도 횃불과 겹치면 ShadowCaster2D를
			// 꺼서 빛이 통과하게 한다(안 그러면 유닛이 광원 위치를 덮어 빛이 가려진다).
			if (cache.ShadowCaster != null && Session != null)
			{
				bool onTorch = false;
				int fw = Mathf.Max(1, Mathf.RoundToInt(u.unitType.footprint.x));
				int fh = Mathf.Max(1, Mathf.RoundToInt(u.unitType.footprint.y));
				for (int dx = 0; dx < fw && !onTorch; dx++)
					for (int dy = 0; dy < fh && !onTorch; dy++)
						if (Session.IsTorchAt(new Vector3Int(u.position.x + dx, u.position.y + dy, u.currentFloor)))
							onTorch = true;

				cache.ShadowCaster.enabled = !onTorch;
			}

			// 시야/인지 범위 표시 — 우측 상단 "시야 표시" 토글(ShowAllVisionRanges)이 켜져 있을 때만
			// 그린다. 다중 선택 시엔 "이 유닛의" 범위를 특정할 수 없어 표시하지 않는다.
			UnitVisual uv = cache.UnitVisual;
			if (uv != null)
			{
				// FSM 상태 라벨은 선택 여부와 무관하게 항상 갱신한다. 안개 가려진 방은 숨긴다(라벨
				// 오프셋 때문에 안개 sortingOrder만으로 항상 덮인다고 보장할 수 없음). u.currentRoom은
				// 야생 몬스터가 제외돼 항상 null이라, 실시간 위치 기준 roomGrid 조회로 통일한다.
				bool hiddenByFog = false;
				if (Session != null && Session.roomGrid != null &&
					Session.roomGrid.TryGetValue(new Vector3Int(u.position.x, u.position.y, u.currentFloor), out Room liveRoom))
				{
					hiddenByFog = !liveRoom.FogRevealed;
				}
				uv.UpdateStatusLabel(ShowUnitStatusLabels && !hiddenByFog ? u.fsm.GetLabel(u) : null, u is Human);

				// 함정 해제 시도 중임을 유닛 하단에 표시(사용자 요청, 2026-07-23) — 머리 위 상태
				// 라벨과 같은 world-space TextMesh 방식, 위치만 하단으로 뒤집는다.
				bool isDisarmingTrap = u is Human hDisarm
					&& hDisarm.currentTrapInteraction != null
					&& hDisarm.currentTrapInteraction.Phase == TrapPhase.Disarming;
				uv.UpdateBelowLabel(isDisarmingTrap ? "" : null);//오류 때문에 잠시 비워둠
			}

			UpdateUnitSpriteForDirection(u);
			RefreshSelectionVisual(u, cache, go);
	}

	// 선택 표시(발밑 링)/시야 범위 시각화를 SyncVisual 안에서만 갱신하면 SyncVisual이 stateChanged
	// 게이트 뒤에 있어 가만히 서 있는 유닛은 선택 상태가 바뀌어도 반영되지 않는다(잔류 버그) —
	// 별도 공개 메서드로 빼서 GameSession.UpdateProcess가 상태 변화와 무관하게 매 프레임 호출한다.
	public void RefreshSelectionVisual(Unit u)
	{
		if (u == null || !visualMap.TryGetValue(u, out GameObject go)) return;
		RefreshSelectionVisual(u, GetCache(go), go);
	}

	private void RefreshSelectionVisual(Unit u, VisualCache cache, GameObject go)
	{
		UnitVisual uv = cache.UnitVisual;
		if (uv != null)
		{
			// 체력바는 상태 변화 여부와 무관하게 매 프레임 갱신되는 여기에 둔다 — stateChanged
			// 게이팅에 묶이면 원거리/함정 피해만 입는 경우 갱신이 누락된다.
			bool hpBarHiddenByFog = Session != null && Session.roomGrid != null &&
				Session.roomGrid.TryGetValue(new Vector3Int(u.position.x, u.position.y, u.currentFloor), out Room hpBarRoom) &&
				!hpBarRoom.FogRevealed;
			uv.UpdateHealthBar(!hpBarHiddenByFog, u.hp, u.maxHp);

			// 단일 선택으로는 자동 표시하지 않는다 — 우측 하단 "시야 표시" 전역 토글
			// (ShowAllVisionRanges)로만 켜고 끈다. 선택 여부와는 완전히 무관.
			bool showRanges = ShowAllVisionRanges;
			uv.SetVisionRangesVisible(showRanges); // 켜고 끄는 것 자체는 저렴 — 매 프레임 갱신해도 무관.

			if (showRanges)
			{
				Vector2 forward = u.GetDirVector(u.currentDir);
				if (forward == Vector2.zero) forward = Vector2.down;

				// DrawVisionAndPerceptionRange는 무거운 작업이라 "막 보이게 된 순간" 또는 "방향이
				// 바뀐 순간"에만 다시 그린다.
				if (!cache.VisionRangeShown || cache.LastVisionForward != forward)
				{
					uv.DrawVisionAndPerceptionRange(
						VisionMath.ViewDistance(u.VisionStat.spotting), VisionMath.BaseViewAngleDeg,
						VisionMath.AwarenessDistance(u.VisionStat.spotting), VisionMath.AwarenessAngle(u.VisionStat.spotting),
						u.isSpecialUnit, VisionMath.CircularPerceptionRadius(u.VisionStat.spotting),
						forward);
					cache.LastVisionForward = forward;
				}
				cache.VisionRangeShown = true;
			}
			else
			{
				cache.VisionRangeShown = false;
			}

			// 명령 경로 시각화는 다중 선택된 유닛 각각에 독립적으로 보여준다. MovementAlgorithm의
			// 기존 경로 캐시를 그대로 읽어 새로 계산하지 않는다.
			bool isSelected = u.InputMgr != null && u.InputMgr.IsUnitSelected(u);
			List<Vector2Int> commandPath = null;
			Vector2Int? hoverTile = null;
			Vector2Int? explicitDest = null;

			if (isSelected)
			{
				bool overUI = (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
					|| (BuildingControlPanel.Instance != null && BuildingControlPanel.Instance.IsMouseOverPanel())
					|| (BottomMenuBar.Instance != null && BottomMenuBar.Instance.IsMouseOverUI())
					|| (DebugInfoPanel.Instance != null && DebugInfoPanel.Instance.IsMouseOverUI());

				if (!overUI)
				{
					Vector2 mousePos = GameInputScheme.PointerScreenPos;
					Vector3Int gridPos = ScreenGridUtil.ScreenToGridPos(mousePos, GetFloorOffset(u.currentFloor), u.currentFloor);
					hoverTile = new Vector2Int(gridPos.x, gridPos.y);
				}

				if (u.HasActivePlayerCommand())
				{
					explicitDest = u.playerMoveTarget;
					if (!explicitDest.HasValue && u.playerAttackObjectTarget.HasValue)
						explicitDest = new Vector2Int(u.playerAttackObjectTarget.Value.x, u.playerAttackObjectTarget.Value.y);
					if (!explicitDest.HasValue && u.playerAttackTarget != null)
						explicitDest = u.playerAttackTarget.position;
					if (!explicitDest.HasValue && u.playerInteractTarget.HasValue)
						explicitDest = new Vector2Int(u.playerInteractTarget.Value.x, u.playerInteractTarget.Value.y);

					if (u.MovementAlgorithm != null)
					{
						// 정지 상태(Time.timeScale < 0.01f)에서는 FSM이 멈춰있어 경로 캐시가 갱신되지 않으므로,
						// 시각화를 위해 목적지가 다르면 1회 강제 계산하여 _pathMap을 채워준다.
						if (explicitDest.HasValue && Time.timeScale < 0.01f)
						{
							u.MovementAlgorithm.TryGetCachedDestination(out Vector2Int cacheDestForCheck);
							if (cacheDestForCheck != explicitDest.Value)
							{
								u.MovementAlgorithm.TryGetNextStep(u, explicitDest.Value, out _);
							}
						}

						if (u.MovementAlgorithm.TryGetCachedDestination(out Vector2Int currentCache))
						{
							commandPath = u.MovementAlgorithm.BuildCachedPathPreview(u, CommandPathMaxSteps);
							if (!explicitDest.HasValue) explicitDest = currentCache;
						}
					}
				}
			}
			uv.UpdateCommandPathVisual(isSelected, commandPath, GetFloorOffset(u.currentFloor), hoverTile, explicitDest);
		}

		EnsureSelectionMarker(cache, go, u);
		if (cache.SelectionMarker != null)
		{
			bool isSelected = (u.InputMgr != null && u.InputMgr.IsUnitSelected(u));
			if (cache.SelectionMarker.gameObject.activeSelf != isSelected)
				cache.SelectionMarker.gameObject.SetActive(isSelected);
		}
	}

	// 캐릭터 스프라이트/애니메이션과 완전히 무관한, 풋프린트 기반 발밑 링을 go의 자식으로 한 번만
	// 만들어둔다. go.transform.localScale이 이미 풋프린트 크기로 맞춰져 있어서(SetupUnitVisual 참고)
	// 부모 스케일을 역산해 로컬 스케일을 계산해야 월드 기준으로 일정한 링 크기가 나온다.
	private void EnsureSelectionMarker(VisualCache cache, GameObject go, Unit unit)
	{
		if (cache.SelectionMarker != null) return;

		Vector2 footprint = unit.unitType.footprint;
		if (footprint.x <= 0f || footprint.y <= 0f) return;

		GameObject markerGo = new GameObject("SelectionMarker");
		markerGo.transform.SetParent(go.transform, false);
		markerGo.SetActive(false);

		var sr = markerGo.AddComponent<SpriteRenderer>();
		sr.sprite = SelectionRingSprite;
		sr.sortingOrder = SelectionMarkerSortingOrder;
		// 진영 구분: 인류는 파란색, 몬스터는 빨간색. 유닛이 인류/몬스터 사이를 오갈 일은 없어서
		// 생성 시점에 한 번만 정해도 된다.
		sr.color = unit is Human ? SelectionRingColorHuman : SelectionRingColorMonster;

		// go의 로컬 X=0은 풋프린트 가로 중앙, Y=0은 발밑에 해당한다(SyncVisuals의 newPos 계산 참고).
		// go의 실제 localScale을 역산한다 — footprint를 그대로 나누면 visualScaleIgnoresFootprint가
		// 켜진 유닛(루트 localScale=1 고정)에서 링이 부모 스케일과 어긋나 잘못된 크기로 그려진다.
		Vector3 parentScale = go.transform.localScale;
		float invX = parentScale.x != 0f ? 1f / parentScale.x : 1f;
		float invY = parentScale.y != 0f ? 1f / parentScale.y : 1f;

		float diameter = Mathf.Max(footprint.x, footprint.y) * SelectionRingDiameterRatio;
		float scaleX = diameter * invX;
		float scaleY = diameter * SelectionRingFlatten * invY;

		markerGo.transform.localPosition = new Vector3(0f, SelectionRingFootOffset * invY, 0f);
		markerGo.transform.localScale    = new Vector3(scaleX, scaleY, 1f);

		cache.SelectionMarker = sr;
	}

	public void TriggerHitEffect(Unit u)
	{
		if (u == null) return;

		UnitVisualDefinition visualDef = GetVisualDef(u);

		if (u.CombatState.State.suppressHitVFX)
		{
			if (visualDef != null) u.VFX?.Spawn(visualDef.attackFailPrefab, u);
			u.CombatState.State.suppressHitVFX = false;
		}
		else
		{
			if (visualDef != null)
			{
				u.VFX?.Spawn(visualDef.hitSparkPrefab, u);
				// hitSparkPrefab을 대체하지 않고 같은 피격에 함께 스폰 — 두 프리팹이 서로 다른
				// VFXManager 풀 슬롯을 쓰므로(Dictionary 키가 프리팹 자체) 동시 재생이 자연스럽게 처리된다.
				u.VFX?.Spawn(visualDef.bloodEffectPrefab, u);
			}
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

	// InputManager의 몬스터(M) 배치 고스트에서도 재사용하려고 public으로 뒀다 — 인류 폴백
	// 스프라이트(흰색)와 같은 원 모양을 색만 바꿔 그린다.
	public Sprite CreateCircleSprite(Color color)
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

	// GameSession.SpawnObject(함정 미리보기/실제 스폰)와 InputManager의 배치 고스트에서도 재사용하려고
	// public으로 뒀다 — 몬스터 폴백 스프라이트(흰색)와 같은 삼각형 모양을 색만 바꿔 그린다.
	public Sprite CreateTriangleSprite(Color color)
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
			return u != null && u.Health.hp > 0;
		return false;
	}

	public bool IsAreaClear(Vector2Int pos, Vector2 footprint, int floorIdx)
	{
		CreateMap cmap = (Session != null && Session.cmap != null) ? Session.cmap : null;
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

				// name=="Wall"만 확인하면 이름은 "Wall"이 아니지만 isStructureExist=true인 타일(계단
				// 2x2 블록 등)이 걸러지지 않고 스폰 가능한 자리로 취급된다 — CreateMap.
				// IsStaticTileWalkable(UnitFunction.CanMove와 동일 기준)로 통일한다.
				if (x < 0 || y < 0) return false;
				if (!cmap.IsStaticTileWalkable(floorIdx, new Vector2Int(x, y))) return false;
				if (IsOccupied(new Vector2Int(x, y), floorIdx)) return false;
				// 문은 통행은 가능해야 하므로(DoorSystem.DoorTag 참고) 스폰 위치 판정만 막고,
				// AStarMovement 등 이동 판정 쪽은 건드리지 않는다.
				if (Session != null && Session.IsDoorTile(new Vector3Int(x, y, floorIdx))) return false;
			}
		}
		return true;
	}

	public Vector2Int GetRandomFloorPos(Vector2 footprint, int floorIdx = 1)
	{
		CreateMap cmap = (Session != null && Session.cmap != null) ? Session.cmap : null;
		if (cmap == null || cmap.map.floors == null || cmap.map.floors.Length == 0) return Vector2Int.zero;
		if (floorIdx < 0 || floorIdx >= cmap.map.floors.Length) return Vector2Int.zero;

		Floor floor = cmap.map.floors[floorIdx];
		if (floor.chunks == null) return Vector2Int.zero;

		int csRandom = floor.config.chunkSize;
		for (int i = 0; i < 2000; i++)
		{
			int cx = Random.Range(0, floor.config.width);
			int cy = Random.Range(0, floor.config.height);
			Chunks c = floor.chunks[cx, cy];
			if (c.roomId != -1 && c.chunk != null)
			{
				int tx = Random.Range(0, csRandom);
				int ty = Random.Range(0, csRandom);
				Vector2Int cand = new Vector2Int(cx * csRandom + tx, cy * csRandom + ty);
				if (IsAreaClear(cand, footprint, floorIdx)) return cand;
			}
		}
		return Vector2Int.zero;
	}

	public Vector2Int GetStartRoomPos(Vector2 footprint, int floorIdx = 1)
	{
		CreateMap cmap = (Session != null && Session.cmap != null) ? Session.cmap : null;
		if (cmap == null || cmap.map.floors == null || floorIdx < 0 || floorIdx >= cmap.map.floors.Length) return Vector2Int.zero;

		Floor floor = cmap.map.floors[floorIdx];
		if (floor.chunks == null) return Vector2Int.zero;

		// 청크 바깥쪽 벽을 피해 안쪽 절반만 후보로 삼는다 — chunkSize=8이면 margin=2로 원래 값과 동일.
		int csStart = floor.config.chunkSize;
		int marginStart = csStart / 4;
		for (int cx = 0; cx < floor.config.width; cx++)
			for (int cy = 0; cy < floor.config.height; cy++)
			{
				Chunks c = floor.chunks[cx, cy];
				if (c.roomRole == RoomRole.StartRoom && c.chunk != null)
					for (int tx = marginStart; tx < csStart - marginStart; tx++)
						for (int ty = marginStart; ty < csStart - marginStart; ty++)
						{
							Vector2Int cand = new Vector2Int(cx * csStart + tx, cy * csStart + ty);
							if (IsAreaClear(cand, footprint, floorIdx)) return cand;
						}
			}
		return GetRandomFloorPos(footprint, floorIdx);
	}

	public Vector2Int GetBossRoomPos(Vector2 footprint, int floorIdx = 1)
	{
		CreateMap cmap = (Session != null && Session.cmap != null) ? Session.cmap : null;
		if (cmap == null || cmap.map.floors == null || floorIdx < 0 || floorIdx >= cmap.map.floors.Length) return Vector2Int.zero;

		Floor floor = cmap.map.floors[floorIdx];
		if (floor.chunks == null) return Vector2Int.zero;

		int csBoss = floor.config.chunkSize;
		int marginBoss = csBoss / 4; // GetStartRoomPos와 동일한 비율 일반화.
		for (int cx = 0; cx < floor.config.width; cx++)
			for (int cy = 0; cy < floor.config.height; cy++)
			{
				Chunks c = floor.chunks[cx, cy];
				if (c.roomRole == RoomRole.BossRoom && c.chunk != null)
					for (int tx = marginBoss; tx < csBoss - marginBoss; tx++)
						for (int ty = marginBoss; ty < csBoss - marginBoss; ty++)
						{
							Vector2Int cand = new Vector2Int(cx * csBoss + tx, cy * csBoss + ty);
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
