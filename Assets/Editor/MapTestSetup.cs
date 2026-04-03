using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// ========================================================================
// 맵 테스트 씬 자동 세팅
// ========================================================================
// 사용법: Unity 에디터 메뉴 → Tools → Map Test Setup
//
// 자동으로 수행하는 작업:
//   1. 현재 씬에 Grid + Tilemap 생성
//   2. CreateMap 오브젝트 생성 (시드 제어 포함)
//   3. MapRandering 오브젝트 생성 + 참조 자동 연결
//   4. Atras.png 슬라이스 스프라이트(Atras_0, Atras_1) 자동 로드
//   5. 카메라 위치를 맵 중앙으로 이동
//   6. 맵 생성 + 렌더링 즉시 실행
// ========================================================================

public class MapTestSetup : EditorWindow
{
    private int seed = 12345;
    private bool useFixedSeed = true;

    [MenuItem("Tools/Map Test Setup")]
    public static void ShowWindow()
    {
        GetWindow<MapTestSetup>("Map Test Setup");
    }

    void OnGUI()
    {
        GUILayout.Label("맵 테스트 씬 자동 세팅", EditorStyles.boldLabel);
        GUILayout.Space(10);

        useFixedSeed = EditorGUILayout.Toggle("고정 시드 사용", useFixedSeed);
        seed = EditorGUILayout.IntField("시드 값", seed);

        GUILayout.Space(10);

        if (GUILayout.Button("씬 세팅 + 맵 생성", GUILayout.Height(40)))
        {
            SetupScene();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("기존 오브젝트 정리", GUILayout.Height(30)))
        {
            CleanupScene();
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "1. '씬 세팅 + 맵 생성' 클릭\n" +
            "2. Grid, CreateMap, MapRandering이 자동 생성됨\n" +
            "3. Atras_0(Wall), Atras_1(Floor) 스프라이트 자동 연결\n" +
            "4. 카메라가 맵 중앙으로 이동\n" +
            "5. Play 모드 없이 즉시 확인 가능",
            MessageType.Info);
    }

    void SetupScene()
    {
        // 기존 테스트 오브젝트 정리
        CleanupScene();

        // ── 1. Grid + Tilemap ──
        var gridGo = new GameObject("MapGrid");
        gridGo.AddComponent<Grid>();

        var tilemapGo = new GameObject("MapTilemap");
        tilemapGo.transform.SetParent(gridGo.transform);
        var tilemap = tilemapGo.AddComponent<Tilemap>();
        var tilemapRenderer = tilemapGo.AddComponent<TilemapRenderer>();
        tilemapRenderer.sortingOrder = 0;

        Undo.RegisterCreatedObjectUndo(gridGo, "Create Map Grid");

        // ── 2. CreateMap ──
        var createMapGo = new GameObject("CreateMap");
        var createMap = createMapGo.AddComponent<CreateMap>();
        createMap.useFixedSeed = useFixedSeed;
        createMap.seed = seed;

        Undo.RegisterCreatedObjectUndo(createMapGo, "Create CreateMap");

        // ── 3. MapRandering ──
        var renderingGo = new GameObject("MapRandering");
        var mapRandering = renderingGo.AddComponent<MapRandering>();
        mapRandering.createMap = createMap;
        mapRandering.tilemap = tilemap;

        Undo.RegisterCreatedObjectUndo(renderingGo, "Create MapRandering");

        // ── 4. 스프라이트 자동 로드 (Assets/Asset/Atras.png 슬라이스) ──
        Sprite wallSprite = null;
        Sprite floorSprite = null;

        // 슬라이스된 스프라이트를 모두 로드
        Object[] sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Asset/Atras.png");
        if (sprites != null)
        {
            foreach (Object obj in sprites)
            {
                if (obj is Sprite sprite)
                {
                    if (sprite.name == "Atras_0")
                        wallSprite = sprite;
                    else if (sprite.name == "Atras_1")
                        floorSprite = sprite;
                }
            }
        }

        if (wallSprite != null && floorSprite != null)
        {
            mapRandering.wallSprite = wallSprite;
            mapRandering.floorSprite = floorSprite;
            Debug.Log("MapTestSetup: 스프라이트 자동 연결 완료 (Atras_0 → Wall, Atras_1 → Floor).");
        }
        else
        {
            Debug.LogWarning(
                "MapTestSetup: Assets/Asset/Atras.png에서 Atras_0 또는 Atras_1을 찾을 수 없습니다.\n" +
                "Atras.png의 Sprite Mode가 'Multiple'이고 슬라이스가 완료되었는지 확인하세요.");
        }

        // ── 5. 맵 생성 + 렌더링 ──
        createMap.GenerateMap();
        mapRandering.DoRandering();

        // ── 6. 카메라를 맵 중앙으로 이동 ──
        CenterCamera();

        // 씬 변경 표시
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"MapTestSetup: 세팅 완료! 시드={seed}, 고정시드={useFixedSeed}");
    }

    void CleanupScene()
    {
        DestroyIfExists("MapGrid");
        DestroyIfExists("CreateMap");
        DestroyIfExists("MapRandering");
    }

    void DestroyIfExists(string name)
    {
        var go = GameObject.Find(name);
        if (go != null)
        {
            Undo.DestroyObjectImmediate(go);
            Debug.Log($"MapTestSetup: '{name}' 제거됨.");
        }
    }

    void CenterCamera()
    {
        // 맵 중앙: 16청크 × 8타일 = 128타일, 중앙 = 64
        float centerX = 64f;
        float centerY = 64f;

        // Scene View 카메라 이동
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            sceneView.pivot = new Vector3(centerX, centerY, 0);
            sceneView.size = 70f;
            sceneView.Repaint();
        }

        // Main Camera가 있으면 위치 조정
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Undo.RecordObject(mainCam.transform, "Move Camera");
            mainCam.transform.position = new Vector3(centerX, centerY, -10f);
            mainCam.orthographic = true;
            mainCam.orthographicSize = 70f;
        }
    }
}
