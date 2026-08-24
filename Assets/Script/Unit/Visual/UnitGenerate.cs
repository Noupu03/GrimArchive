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

	// debug 메뉴 "유닛 상태 표시" 토글(2026-08-24 신규, 2026-08-24 후속 사용자 요청으로 기본값 꺼짐으로
	// 변경) — 꺼지면 유닛 머리 위 현재 FSM 상태 라벨(SyncVisual의 uv.UpdateStatusLabel 호출부 참고)을
	// 전부 숨긴다. "시야 표시"(ShowAllVisionRanges)와 마찬가지로 기본은 꺼짐 — 필요할 때 debug
	// 메뉴에서 켠다.
	public bool ShowUnitStatusLabels = false;

	// 명령 경로 시각화(2026-08-24) — BuildCachedPathPreview가 따라갈 최대 waypoint 수. 정상적인 경로는
	// 이보다 훨씬 짧다 — 캐시가 어긋난 극단적 상황에서 무한 루프/과도한 LineRenderer 포인트를 막는 안전판.
	private const int CommandPathMaxSteps = 200;

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
		// 횃불 위 예외 처리(2026-08-24 사용자 요청, 아래 SyncVisual 참고)용 캐시.
		public ShadowCaster2D ShadowCaster;
#if UNITY_2022_2_OR_NEWER
		public SpriteResolver SpriteResolver;
#endif
		// 시야/인지 범위 콘(LineRenderer) 다시 그리기 여부 판단용(2026-08-22, 프레임 드랍 대응) —
		// DrawVisionAndPerceptionRange는 삼각함수 20+세그먼트 계산 + LineRenderer.SetPosition을
		// 두 콘(시야/인지)에 매번 새로 돌리는 비용이 있어, "보이는 상태 + 방향이 실제로 바뀌었을 때"
		// 에만 다시 그린다 — RefreshSelectionVisual 자체는 매 프레임 호출되지만 이 필드로 걸러낸다.
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
		// 스프라이트 아트가 이미 footprint 배율로 그려진 유닛(2026-08-24, 보스 골렘 대응 — 사용자 신고
		// "이미지 자체가 3배 스케일링, 3배해서 9배가 되어버림")은 footprint를 시각적 확대에 다시 곱하지
		// 않는다 — footprint 값 자체는 UnitStatsData/인구수/충돌 판정 등 다른 곳에서 여전히 그대로 쓰인다.
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

		// 발밑 선택 링을 스폰 시점에 미리 만들어둔다(2026-08-22 사용자 신고 "프레임 드랍이 심해짐" —
		// RefreshSelectionVisual을 모든 유닛에 매 프레임 무조건 호출하게 되면서, 마커가 아직 없는
		// 유닛들(특히 웨이브 스폰으로 한 번에 여러 명이 등장한 직후)이 전부 같은 프레임에 몰려
		// EnsureSelectionMarker의 GameObject/SpriteRenderer 생성 비용이 한꺼번에 터졌다 — "괜찮다가
		// 순간적으로 프레임이 9까지 떨어졌다가 다시 올라감" 증상과 일치. 스폰은 원래 한 유닛씩
		// 처리되므로 여기서 만들면 그 비용이 자연히 분산된다.
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

	// 사망 연출 도입(2026-08-24)으로 "hp<=0인 다른 유닛도 한꺼번에 정리"하던 예전 안전망 스윕을
	// 제거했다 — 사망한 유닛은 GameSession.RemoveDeadUnit이 units 리스트에서 뺀 그 자리에서 곧장 이
	// 메서드까지 호출해 visualMap에서도 함께 제거된다(2026-08-24 후속: 사망 스프라이트를 잠깐 붙들고
	// 있던 지연 단계 자체를 없애면서, "일정 시간 동안 hp<=0 상태로 남아있는" 중간 상태가 사라졌다 —
	// 아래 PlayDeathVisual 참고). 모든 사망은 RemoveDeadUnit이 명시적으로 RemoveVisual(u)를 호출하므로
	// 별도 안전망 스윕이 필요 없다는 결론 자체는 그대로 유지된다.
	public void RemoveVisual(Unit u)
	{
		if (u == null || !visualMap.TryGetValue(u, out GameObject go)) return;
		if (go != null) { KillVisualTweens(go); Object.Destroy(go); _cacheMap.Remove(go); }
		visualMap.Remove(u);
		targetPosMap.Remove(u);
	}

	// 사망 VFX(2026-08-24 신규, 2026-08-24 후속 수정 — 사용자 요청 "Death 스프라이트 단계 자체를
	// 삭제하고 싶어... 사망 판정 즉시 Corpse 스프라이트로 전환 및 Death VFX Prefab이 발동되게") —
	// GameSession.RemoveDeadUnit이 사망 판정 직후, 시체 오브젝트를 스폰하고 이 유닛의 비주얼을 파괴하기
	// 직전에 호출한다. VFX만 1회 재생하고 끝 — 예전엔 스프라이트를 UnitVisualDefinition.deathSprite로
	// 고정한 채 DeathVisualDurationSeconds(0.8초)만큼 붙들고 있다가 시체로 교체했는데, 이제 그 중간
	// 단계 없이 시체 오브젝트가 즉시 나타나므로 스프라이트를 잠깐이라도 바꿔둘 이유가 없다(교체 자체가
	// 그 즉시 일어난다). deathSprite 필드는 그래서 함께 제거했다.
	public void PlayDeathVisual(Unit u)
	{
		if (u == null || !visualMap.TryGetValue(u, out GameObject go) || go == null) return;

		var cache = GetCache(go);
		var def = cache.UnitVisualDefinition;
		if (def == null || def.deathVfxPrefab == null) return;

		// u.VFX?.Spawn(prefab, u)(2-인자, 유닛에 부모로 붙는 오버로드)를 쓰면 안 된다 — GameSession.
		// RemoveDeadUnit이 이 메서드를 호출한 직후 같은 프레임에 RemoveVisual(u)로 이 유닛의 비주얼
		// GameObject(go)를 Destroy하는데, 그 자식으로 붙은 VFX 인스턴스도 함께 파괴되어 렌더링될
		// 기회조차 없이 사라진다(2026-08-24 사용자 신고 "Death VFX가 발생 안 함(적어도 시각적으로는)"
		// — 지연 단계를 없애면서 생긴 회귀). 고정 월드 좌표에 부모 없이 스폰해서 유닛 비주얼의
		// 생명주기와 완전히 분리한다 — 죽는 자리에서 한 번 재생되면 그만이라 유닛을 따라다닐 필요도
		// 없다.
		VFXManager.Spawn(def.deathVfxPrefab, VFXManager.GetWorldPos(u), Quaternion.identity);
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

			// 횃불 위 유닛 빛 투과 예외(2026-08-24 사용자 요청 "모든 유닛에 [ShadowCaster2D] 넣고,
			// 횃불 바로 위에 있으면 빛 그냥 투과로 예외처리") — 유닛 스프라이트의 ShadowCaster2D가
			// 평소엔 빛을 정상적으로 가리지만(그림자), 유닛이 서 있는 타일(발자국 전체) 중 하나라도
			// 횃불 타일과 겹치면 그 순간만 컴포넌트를 꺼서 빛이 그대로 통과하게 한다 — 안 그러면
			// 유닛이 광원 위치를 그대로 덮어 횃불 빛 전체가 가려져 보였다.
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

	// 선택 표시(발밑 링)/단일 선택 시 시야 범위 시각화(2026-08-22 사용자 신고 "선택 담당 시각화들이
	// 꼬임 — 유닛 발밑의 동그라미가 몇개는 생기고 몇개는 안생김. 단일 선택시 나타나는 시야 표현
	// 시각화가 계속 남아있음. 유닛 발밑의 동그라미가 다른 유닛 선택해도 사라지지 않음") — 원인은 이
	// 두 표시를 SyncVisual 안에서만 갱신했는데, SyncVisual 자체가 GameSession.ProcessUnitAction의
	// stateChanged 게이트(그 유닛의 position/label/currentDir이 "이번 틱에 실제로 바뀐 경우"에만
	// 호출됨) 뒤에 있었다는 것 — 가만히 서 있는 유닛은 선택 상태가 바뀌어도 그 사실이 전혀 반영되지
	// 않았다(발밑 링이 안 생기거나/안 사라짐, 시야 범위가 계속 남음). SyncVisual에서 분리해 별도
	// 공개 메서드로 빼고, GameSession.UpdateProcess의 매 프레임 무조건 도는 유닛 순회 루프에서
	// 상태 변화 여부와 무관하게 매 프레임 호출한다(가벼운 SetActive/불리언 비교뿐이라 비용 낮음).
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
			// 체력바(2026-08-24 사용자 요청 "유닛의 머리 위에 체력바 항상 뜨도록") — SyncVisual의
			// UpdateStatusLabel과 달리 여기(상태 변화 여부와 무관하게 모든 살아있는 유닛에 대해 매
			// 프레임 갱신되는 지점, 위 RefreshSelectionVisual(Unit) 호출부 주석 참고)에 둔다 — 가만히
			// 서서 원거리/함정 피해만 입는 경우처럼 위치·라벨·방향이 전혀 안 바뀌어도 체력은 계속
			// 바뀌므로 stateChanged 게이팅에 묶이면 갱신이 누락된다. 안개에 가려진 유닛은 상태 라벨과
			// 동일하게 숨긴다.
			bool hpBarHiddenByFog = Session != null && Session.roomGrid != null &&
				Session.roomGrid.TryGetValue(new Vector3Int(u.position.x, u.position.y, u.currentFloor), out Room hpBarRoom) &&
				!hpBarRoom.FogRevealed;
			uv.UpdateHealthBar(!hpBarHiddenByFog, u.hp, u.maxHp);

			// 단일 선택 시 자동 표시 제거(2026-08-24 사용자 요청 "유닛을 단일 선택했을때, 시야
			// 시각화가 보이는데, 안보이게 해줘") — 이제 우측 하단 "시야 표시" 전역 토글
			// (ShowAllVisionRanges)로만 켜고 끈다. 선택 여부와는 완전히 무관.
			bool showRanges = ShowAllVisionRanges;
			uv.SetVisionRangesVisible(showRanges); // 켜고 끄는 것 자체는 저렴 — 매 프레임 갱신해도 무관.

			if (showRanges)
			{
				Vector2 forward = u.GetDirVector(u.currentDir);
				if (forward == Vector2.zero) forward = Vector2.down;

				// DrawVisionAndPerceptionRange는 콘 2개(시야/인지)마다 20+ 세그먼트 삼각함수 계산 +
				// LineRenderer.SetPosition을 돌리는 무거운 작업이다 — 매 프레임 무조건 다시 그리면
				// (특히 "시야 표시" 전역 토글로 여러 유닛이 동시에 켜져 있을 때) 프레임 드랍이
				// 심해진다(사용자 신고, 2026-08-22). 실제로 "막 보이게 된 순간" 또는 "바라보는 방향이
				// 바뀐 순간"에만 다시 그리고, 그 외엔 이전에 그려둔 모양을 그대로 둔다.
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

			// 명령 경로 시각화(2026-08-24 사용자 요청 "유닛에게 명령 실행시, 유닛이 명령받은 지점과,
			// 명령 경로가 뜨도록... 이 시각화는 유닛 선택 중일때만 보임(다중 선택했을때도)") — 시야
			// 표시와 달리 다중 선택된 유닛 각각에 대해 독립적으로 보여준다(단일 선택 제한 없음).
			// MovementAlgorithm이 실제 이동 판단에 쓰던 경로 캐시를 그대로 읽어 새로 계산하지 않는다 —
			// 길찾기가 다시 도는 순간 자동으로 최신 경로가 반영된다.
			bool isSelected = u.InputMgr != null && u.InputMgr.IsUnitSelected(u);
			List<Vector2Int> commandPath = null;
			if (isSelected && u.HasActivePlayerCommand() && u.MovementAlgorithm != null
				&& u.MovementAlgorithm.TryGetCachedDestination(out Vector2Int _))
			{
				commandPath = u.MovementAlgorithm.BuildCachedPathPreview(u, CommandPathMaxSteps);
			}
			uv.UpdateCommandPathVisual(isSelected, commandPath, GetFloorOffset(u.currentFloor));
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

		// go의 로컬 X=0은 이미 풋프린트 가로 중앙, 로컬 Y=0은 풋프린트 바닥(발밑)에 해당한다
		// (SyncVisuals의 newPos = position + footprint.x/2, position.y + 0.05f 참고).
		// go의 실제 localScale을 역산한다(2026-08-24 수정) — footprint를 그대로 나누면
		// UnitVisualDefinition.visualScaleIgnoresFootprint가 켜진 유닛(보스 골렘 등, 루트 localScale이
		// footprint가 아니라 1로 고정됨)에서 발밑 링이 실제 부모 스케일과 어긋나 잘못된 크기로 그려진다
		// — UnitVisual.EnsureStatusLabel과 동일한 이유/패턴.
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
				// 피격 추가 이펙트(2026-08-24 사용자 요청 "hitspark는 유지하고 추가로 BloodDrip이
				// 출력되게") — hitSparkPrefab을 대체하지 않고 같은 피격에 함께 스폰한다. 두 프리팹이
				// 서로 다른 VFXManager 풀 슬롯을 쓰므로(Dictionary 키가 프리팹 자체) 동시 재생이
				// 자연스럽게 처리된다.
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

		// 청크 바깥쪽 벽을 피해 안쪽 절반만 후보로 삼는다 — 청크 크기 8 기준 [2,6)이었던 걸 비율
		// 그대로 일반화했다(2026-08-23, 맵 1.5배 확장). chunkSize=8이면 margin=2로 원래 값과 동일.
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
