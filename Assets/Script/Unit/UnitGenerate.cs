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
    private GameObject visualContainer;
    private Sprite humanSprite;
    private Sprite monsterSprite;

    void Awake()
    {
        Instance = this;
        visualContainer = new GameObject("UnitVisuals");
        humanSprite = CreateCircleSprite(Color.green);
        monsterSprite = CreateTriangleSprite(Color.red); 
    }

    public T GenerateUnitAtRandomFloor<T>(UnitType unitType) where T : Unit//유닛 생성 로직
	{
        Vector2Int pos = GetRandomFloorPos();

        T unit = ScriptableObject.CreateInstance<T>();
        unit.name = $"{unitType.typeName}_{pos.x}_{pos.y}";
        unit.unitType = unitType;
        unit.currentState = UnitState.TEST_RANDOM_MOVE_6;
        unit.position = pos;

		// Visual 생성 (Update 로직이 없는 깡통 오브젝트)
		GameObject go = new GameObject(unit.name);
		go.transform.SetParent(visualContainer.transform);
		SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
		sr.sortingOrder = 10;

		UnitVisual uv = go.AddComponent<UnitVisual>();
		uv.Setup();

		if (typeof(T) == typeof(Human)) sr.sprite = humanSprite;
		else if (typeof(T) == typeof(Monster)) sr.sprite = monsterSprite;

		go.transform.position = new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0);
		visualMap[unit] = go;

		return unit;
	}
	#region 생성 보조 기능성
	public void SyncVisuals(List<Unit> units)//비주얼화 코루틴 시작
	{
		foreach (var u in units)
		{
			if (u != null && visualMap.TryGetValue(u, out GameObject go))
			{
				Vector3 newPos = new Vector3(u.position.x + 0.5f, u.position.y + 0.5f, 0);
				StartCoroutine(SmoothMove(go.transform, newPos, 1f));

				// 시각화 업데이트 (FOV)
				UnitVisual uv = go.GetComponent<UnitVisual>();
				if(uv != null)
				{
					Vector2 forward = u.GetDirVector(u.currentDir);
					if (forward == Vector2.zero) forward = Vector2.down;
					uv.DrawFOV(128f, 160f, forward);
				}
			}
		}
	}

	private System.Collections.IEnumerator SmoothMove(Transform visualTransform, Vector3 targetPos, float duration)
	{
		if (visualTransform == null) yield break;

		Vector3 startPos = visualTransform.position;
		float elapsed = 0f;

		while (elapsed < duration)
		{
			if (visualTransform == null) yield break; // 중간에 파괴된 경우 방어
			visualTransform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
			elapsed += Time.deltaTime;
			yield return null;
		}

		if (visualTransform != null)
			visualTransform.position = targetPos;
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

    public Vector2Int GetRandomFloorPos()//랜덤한 바닥 위치 반환
	{
        CreateMap cmap = FindObjectOfType<CreateMap>();
        if (cmap == null || cmap.map.session == null) return Vector2Int.zero;

        // Try getting a random floor position
        for (int i = 0; i < 1000; i++)
        {
            int cx = Random.Range(0, 16);
            int cy = Random.Range(0, 16);
            Chunks c = cmap.map.session[cx, cy];
            if (c.roomId != -1 && c.chunk != null)
            {
                int tx = Random.Range(0, 8);
                int ty = Random.Range(0, 8);
                if (c.chunk[tx, ty].name != "Wall")
                {
                    return new Vector2Int(cx * 8 + tx, cy * 8 + ty);
                }
            }
        }
        return Vector2Int.zero; // default fallback
	}
    #endregion
}

public class GameSession : MonoBehaviour//게임 세션 관리 및 턴 처리(대부분 임시적인 테스트용 요소임
{
	public List<Unit> units = new List<Unit>();
	private float updateTimer = 0f;

	[Header("진영별 맵 시각화 텍스처 (인스펙터에서 클릭하여 확인)")]
	public Texture2D humanMapTexture;
	public Texture2D monsterMapTexture;

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

        updateTimer += Time.deltaTime;
        if (updateTimer >= 2f)
        {
            updateTimer -= 2f;
            ProcessTurn();
        }
    }

    public void OnKeyDown_H()//H 키를 눌렀을 때 인간 유닛 생성
	{
        if (UnitGenerate.Instance == null) return;

        UnitType[] types = { new Warrior(), new Scout(), new Archer() };
        UnitType selection = types[Random.Range(0, types.Length)];

        Human human = UnitGenerate.Instance.GenerateUnitAtRandomFloor<Human>(selection);
        units.Add(human);
        Debug.Log($"Generated Human: {selection.typeName} at {human.position}");
    }

    public void OnKeyDown_M()//M 키를 눌렀을 때 몬스터 유닛 생성
	{
        if (UnitGenerate.Instance == null) return;

        UnitType[] types = { new Goblin(), new Orc() };
        UnitType selection = types[Random.Range(0, types.Length)];

        Monster monster = UnitGenerate.Instance.GenerateUnitAtRandomFloor<Monster>(selection);
        units.Add(monster);
        Debug.Log($"Generated Monster: {selection.typeName} at {monster.position}");
    }

	public void ProcessTurn()//턴 처리 로직 (유닛 상태 판단 및 액션 실행)//핵심로직!!!
	{
		// 턴마다 발견한 적 목록 초기화 (새로운 시야 기준으로 갱신)
		Unit.humanFactionData.ClearSpottedUnits();
		Unit.monsterFactionData.ClearSpottedUnits();

		foreach (var u in units)
		{
			if (u != null)
			{
				u.JudgeState(); // 상태 판단 로직 실행
				u.ExecuteAction();
				u.UpdateFOV(units); // 행동 후 자신의 시야를 공용 데이터에 갱신
			}
		}

		// 턴 액션 처리 후, 씬 상주 시각적 요소들 위치 일괄 동기화
		if (UnitGenerate.Instance != null)
		{
			UnitGenerate.Instance.SyncVisuals(units);
		}

		UpdateFactionTextures();
	}

	private void UpdateFactionTextures()//인스펙터 꾸미기 관련
	{
		if (humanMapTexture == null) { humanMapTexture = new Texture2D(128, 128); humanMapTexture.filterMode = FilterMode.Point; }
		if (monsterMapTexture == null) { monsterMapTexture = new Texture2D(128, 128); monsterMapTexture.filterMode = FilterMode.Point; }

		Color[] hPixels = new Color[128 * 128];
		Color[] mPixels = new Color[128 * 128];

		for (int y = 0; y < 128; y++)
		{
			for (int x = 0; x < 128; x++)
			{
				// 0: 미탐색(검은색), 1: 바닥(흰색), 2: 벽(회색)
				int hVal = Unit.humanFactionData.discoveredMap[x, y];
				hPixels[y * 128 + x] = hVal == 1 ? Color.white : (hVal == 2 ? Color.gray : Color.black);

				int mVal = Unit.monsterFactionData.discoveredMap[x, y];
				mPixels[y * 128 + x] = mVal == 1 ? Color.white : (mVal == 2 ? Color.gray : Color.black);
			}
		}

		// 자기 진영 유닛 현재 위치 정보 초록색으로 덧씌우기
		foreach(var u in units)
		{
			if (u == null) continue;
			int idx = u.position.y * 128 + u.position.x;
			if (idx >= 0 && idx < hPixels.Length)
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
			if (enemy == null) continue;
			int idx = enemy.position.y * 128 + enemy.position.x;
			if (idx >= 0 && idx < hPixels.Length) hPixels[idx] = Color.red;
		}

		foreach (var enemy in Unit.monsterFactionData.spottedEnemyUnits)
		{
			if (enemy == null) continue;
			int idx = enemy.position.y * 128 + enemy.position.x;
			if (idx >= 0 && idx < mPixels.Length) mPixels[idx] = Color.blue;
		}

		humanMapTexture.SetPixels(hPixels);
		humanMapTexture.Apply();

		monsterMapTexture.SetPixels(mPixels);
		monsterMapTexture.Apply();
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

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("인류 진영 맵", EditorStyles.boldLabel);
		if (gs.humanMapTexture != null)
		{
			Rect rect = GUILayoutUtility.GetRect(256, 256);
			GUI.DrawTexture(rect, gs.humanMapTexture, ScaleMode.ScaleToFit);
		}

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("몬스터 진영 맵", EditorStyles.boldLabel);
		if (gs.monsterMapTexture != null)
		{
			Rect rect = GUILayoutUtility.GetRect(256, 256);
			GUI.DrawTexture(rect, gs.monsterMapTexture, ScaleMode.ScaleToFit);
		}
	}
}
#endif

public class UnitVisual : MonoBehaviour
{
	public LineRenderer fovLine;

	public void Setup()
	{
		fovLine = gameObject.AddComponent<LineRenderer>();
		fovLine.startWidth = 0.05f;
		fovLine.endWidth = 0.05f;
		fovLine.material = new Material(Shader.Find("Sprites/Default")); // 기본 2D 쉐이더로 단색 표시
		fovLine.startColor = new Color(0f, 1f, 1f, 0.5f); // 하늘색 반투명
		fovLine.endColor = new Color(0f, 1f, 1f, 0.5f);
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
