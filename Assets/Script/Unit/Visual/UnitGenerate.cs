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

	// 우측 상단 UI(DebugInfoPanel)의 "시야 표시" 토글이 켜고 끄는 전역 스위치 — 켜져 있으면 선택
	// 여부와 무관하게 모든 유닛의 시야/인지 범위를 SyncVisuals가 표시한다(SetVisionRangesVisible 참고).
	public bool ShowAllVisionRanges = false;

	// 선택 표시용 발밑 링(SelectionMarker) 관련 상수. 캐릭터 스프라이트/애니메이션과 완전히
	// 무관하게(풋프린트 크기만으로 계산) 발밑에 깔리는 납작한 타원 링을 스타크래프트식으로 그린다.
	// (예전엔 사각 4바 프레임이었는데 캐릭터를 어색하게 감싸서 보기 안 좋다는 피드백으로 교체함.
	//  그 이전엔 OutlineSpriteSync로 스프라이트를 검게 복제하는 방식이었는데, 실제 유닛 프리팹엔
	//  그 동기화 컴포넌트가 애초에 안 붙어있어서 완전히 죽은 기능이었다 — 그래서 스프라이트 자체에
	//  안 엮이는 이 방식으로 넘어옴.)
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
		go.transform.localScale = new Vector3(
			unit.unitType.footprint.x * visualScale,
			unit.unitType.footprint.y * visualScale,
			1f
		);

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

	// 계층 정리(2026-07-28, 사용자 요청 "각 층에 자식으로 할당된 오브젝트들을... 유닛이면 유닛...
	// 묶어서 나타나게 해줘") — 예전엔 F{n}_Tilemap 바로 아래에 유닛을 매달았는데, 이제 그 층의
	// "Units" 하위 그룹(GameSession.GetFloorCategoryGroup)으로 통일한다. 실패(GameSession 아직 준비
	// 안 됨 등) 시 예전처럼 타일맵 자체로 폴백.
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
			if (kvp.Key == null || kvp.Key.Health.hp <= 0)
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

			// 시야/인지 범위 표시 — 유닛을 단일 선택했을 때, 또는 우측 상단 "시야 표시" 토글
			// (ShowAllVisionRanges)이 켜져 있을 때 그린다. 여러 유닛을 동시에 선택했을 때는 "이
			// 유닛의" 범위라고 특정할 수 없으므로(전역 토글이 꺼져 있는 한) 표시하지 않는다.
			UnitVisual uv = cache.UnitVisual;
			if (uv != null)
			{
				// 선택 여부와 무관하게 항상 표시 — GOAP이 앞으로 실행할 계획을 카메라 위치/배율과
				// 무관하게 유닛 머리 위에 계속 보여준다(UnitVisual.UpdateStatusLabel). 다만 안개에
				// 가려진 방 안에 있으면 숨긴다(사용자 요청, 2026-07-28 "안개 속의 유닛은 머리 위의
				// 상태도 보이지 않게 해줘") — 라벨은 유닛 머리 위로 오프셋(0.35 유닛)이 붙어 안개
				// 스프라이트 sortingOrder만으로는 항상 완전히 덮인다고 보장할 수 없어 명시적으로 끈다.
				// 후속 신고(2026-07-28, "안개가 덮여있는 방 유닛 머리 위의 상태 표시가 미약하게 보여")
				// — u.currentRoom(SyncRoomAffiliation 기반)은 야생 몬스터 A(WildMonsterBehavior)가
				// 동기화 대상에서 제외돼 있어 항상 null로 남는다 — 그래서 야생 몬스터는 이 조건이 절대
				// true가 안 돼 라벨이 안개 sortingOrder에만 기대는 채로 살짝 비쳐 보였다. currentRoom
				// 대신 실시간 위치 기준 roomGrid 조회로 바꿔 진영/동기화 여부와 무관하게 모든 유닛에
				// 똑같이 적용한다.
				bool hiddenByFog = false;
				if (Session != null && Session.roomGrid != null &&
					Session.roomGrid.TryGetValue(new Vector3Int(u.position.x, u.position.y, u.currentFloor), out Room liveRoom))
				{
					hiddenByFog = !liveRoom.FogRevealed;
				}
				uv.UpdateStatusLabel(hiddenByFog ? null : u.fsm.GetLabel(u), u is Human);

				// 함정 해제 시도 중임을 유닛 하단에 표시(사용자 요청, 2026-07-23) — 머리 위 상태
				// 라벨과 같은 world-space TextMesh 방식, 위치만 하단으로 뒤집는다.
				bool isDisarmingTrap = u is Human hDisarm
					&& hDisarm.currentTrapInteraction != null
					&& hDisarm.currentTrapInteraction.Phase == TrapPhase.Disarming;
				uv.UpdateBelowLabel(isDisarmingTrap ? "" : null);//오류 때문에 잠시 비워둠

				bool isSoleSelected = u.InputMgr != null && u.InputMgr.selectedUnits.Count == 1 && u.InputMgr.selectedUnits[0] == u;
				bool showRanges = ShowAllVisionRanges || isSoleSelected;
				uv.SetVisionRangesVisible(showRanges);

				if (showRanges)
				{
					Vector2 forward = u.GetDirVector(u.currentDir);
					if (forward == Vector2.zero) forward = Vector2.down;

					uv.DrawVisionAndPerceptionRange(
						VisionMath.ViewDistance(u.VisionStat.spotting), VisionMath.BaseViewAngleDeg,
						VisionMath.AwarenessDistance(u.VisionStat.spotting), VisionMath.AwarenessAngle(u.VisionStat.spotting),
						u.isSpecialUnit, VisionMath.CircularPerceptionRadius(u.VisionStat.spotting),
						forward);
				}
			}

			UpdateUnitSpriteForDirection(u);

			EnsureSelectionMarker(cache, go, u);
			if (cache.SelectionMarker != null)
			{
				bool isSelected = (u.InputMgr != null && u.InputMgr.selectedUnits.Contains(u));
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

		// go의 로컬 X=0은 이미 풋프린트 가로 중앙, 로컬 Y=0은 풋프린트 바닥(발밑)에 해당한다
		// (SyncVisuals의 newPos = position + footprint.x/2, position.y + 0.05f 참고).
		float diameter = Mathf.Max(footprint.x, footprint.y) * SelectionRingDiameterRatio;
		float scaleX = diameter / footprint.x;
		float scaleY = diameter * SelectionRingFlatten / footprint.y;

		markerGo.transform.localPosition = new Vector3(0f, SelectionRingFootOffset / footprint.y, 0f);
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

				// 버그 수정(2026-08-20, 사용자 신고 "계단 인접 유닛생산 건물에서 유닛 생산시, 계단
				// 위로 스폰되는 경우가 있어") — 예전엔 여기서 c.chunk[tx,ty].name=="Wall"만 확인해서,
				// 이름은 "Wall"이 아니지만 isStructureExist=true인 타일(계단 2x2 블록, TileFactory.
				// Stair() 참고 — name="Stair", isStructureExist=true)은 걸러지지 않고 스폰 가능한
				// 자리로 취급됐다. CreateMap.IsStaticTileWalkable(UnitFunction.CanMove와 동일 기준,
				// name!="Wall" && !isStructureExist)로 교체해 청크 인덱싱 중복도 함께 없앤다 — 문
				// 타일(아래 IsDoorTile 검사)은 열린 상태일 때 isStructureExist가 꺼져 있어도 여전히
				// 별도로 막아야 하므로 그 검사는 그대로 둔다.
				if (x < 0 || y < 0) return false;
				if (!cmap.IsStaticTileWalkable(floorIdx, new Vector2Int(x, y))) return false;
				if (IsOccupied(new Vector2Int(x, y), floorIdx)) return false;
				// 사용자 정정(2026-07-28, "문이 있는 자리에는... 몬스터 배치도 불가능(이동만 가능)")
				// — 문은 통행은 가능해야 하므로(DoorSystem.DoorTag 주석 참고) 여기서는 스폰 위치
				// 판정만 막고, AStarMovement 등 이동 판정 쪽은 건드리지 않는다.
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
		CreateMap cmap = (Session != null && Session.cmap != null) ? Session.cmap : null;
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
		CreateMap cmap = (Session != null && Session.cmap != null) ? Session.cmap : null;
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
