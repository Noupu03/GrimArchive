using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float panSpeed = 35f;
    public float zoomSpeed = 0.05f; // Input system의 스크롤 값은 크므로 조절
    public float minZoom = 5f;
    public float maxZoom = 50f;

    // 층 전환은 유닛 이동이 아니라 카메라 관찰 위치 변경으로만 처리한다. 별도 Fog of War 분리 시스템
    // 없이(MapRandering이 층마다 가로로 떨어뜨려 배치), 카메라가 "지금 보고 있는 층" 밖으로 못 나가게
    // 막는 것만으로 다른 층 노출을 막는다. 0층 최좌측 1x1 청크는 인간 파티 대기 공간이라 카메라
    // 관찰 가능 범위 자체에서 제외한다.
    private const int Floor0HiddenChunksX = 1;
    // Floor0HiddenChunksX(청크 단위)를 타일 단위로 환산할 때 쓰는 0층 청크 크기 — 청크 크기가 층별
    // 설정값(FloorConfig.chunkSize)이라 별도 상수로 손으로 맞추지 않고 실제 맵 데이터에서 읽는다.
    private static int Floor0ChunkSizeTiles
    {
        get
        {
            var cmap = GameSession.Instance?.cmap;
            if (cmap == null || cmap.map.floors == null || cmap.map.floors.Length == 0) return 8;
            return cmap.map.floors[0].config.chunkSize;
        }
    }

    private int _currentFloor = -1; // -1 = 아직 초기화 전(맵 로드 대기 중).
    private bool _floorViewInitialized = false;

    // BottomMenuBar("맵" 서브메뉴 층 버튼)가 VContainer 주입 대상이 아니라 정적 접근이 필요해
    // 다른 매니저들(NoticeCenter.Instance 등)과 동일한 관례를 따른다.
    public static CameraController Instance { get; private set; }
    public int CurrentFloor => _currentFloor;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // BottomMenuBar가 층 버튼 개수(현재는 문서 명시대로 4개 고정)를 실제 층 수와 대조해 범위 밖 버튼을
    // 비활성화하는 데 쓴다.
    public bool TryGetFloorCount(out int floorCount) => TryGetFloorCountStatic(out floorCount);

    // 층 전환 메뉴에서 "아직 밝혀지지 않은 층" 버튼을 숨기는 데 쓴다. Room.FogRevealed(영구 해제
    // 플래그)를 재사용해 그 층에 안개 걷힌 방이 하나라도 있으면 밝혀진 층으로 본다.
    public bool IsFloorRevealed(int floorIndex)
    {
        var allRooms = GameSession.Instance?.allRooms;
        if (allRooms == null) return false;
        foreach (var room in allRooms)
            if (room != null && room.Floor == floorIndex && room.FogRevealed) return true;
        return false;
    }

    // 특정 층으로 즉시 이동(맵 메뉴 버튼용) — 절대 인덱스를 받는다. 층 전환은 이 메서드뿐이다.
    public void GoToFloor(int floorIndex)
    {
        if (!_floorViewInitialized || !TryGetFloorCountStatic(out int floorCount) || floorCount <= 0) return;

        int clamped = Mathf.Clamp(floorIndex, 0, floorCount - 1);
        if (clamped == _currentFloor) return;

        _currentFloor = clamped;
        SnapToFloorCenter(_currentFloor);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        // 1. 카메라 조작 자동 부착
        if (Camera.main != null && Camera.main.gameObject.GetComponent<CameraController>() == null)
        {
            Camera.main.gameObject.AddComponent<CameraController>();
            Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, "CameraController 자동 부착 완료.");
        }

        // 2. EventSystem InputModule 크래시 방지 (새로운 Input System 적용)
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem != null)
        {
            var standalone = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (standalone != null)
            {
                Destroy(standalone);
                eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, "EventSystem을 InputSystemUIInputModule로 자동 교체 완료.");
            }
        }
    }

    void Update()
    {
        // AutoAttach가 씬 로드 직후 실행될 수 있어, 층 범위 정보가 준비될 때까지는 패닝만 자유롭게
        // 허용하고 층 클램프는 걸지 않는다.
        EnsureFloorViewInitialized();

        Vector3 pos = transform.position;

        // 입력은 GameInputScheme이 중앙에서 읽는다 — 카메라 줌은 이 클래스만 적용한다(DebugInfoPanel이
        // 같은 휠 값을 별도로 다시 적용해 줌 속도가 겹치던 버그를 없앤 지점, GameInputScheme 참고).
        float move = panSpeed * Time.unscaledDeltaTime;
        if (GameInputScheme.MoveUp)    pos.y += move;
        if (GameInputScheme.MoveDown)  pos.y -= move;
        if (GameInputScheme.MoveRight) pos.x += move;
        if (GameInputScheme.MoveLeft)  pos.x -= move;

        float scroll = GameInputScheme.ZoomDelta;
        if (scroll != 0.0f)
        {
            Camera.main.orthographicSize -= scroll * zoomSpeed;
            Camera.main.orthographicSize = Mathf.Clamp(Camera.main.orthographicSize, minZoom, maxZoom);
        }

        if (_floorViewInitialized) pos = ClampToCurrentFloorBounds(pos);

        transform.position = pos;
    }

    // 맵이 준비되는 순간 딱 한 번 초기 관찰 층을 정한다 — 플레이어가 실제로 조작하는 1층을 기본값으로
    // 삼는다(0층은 인간 파티 대기 공간일 뿐).
    private void EnsureFloorViewInitialized()
    {
        if (_floorViewInitialized) return;
        if (!TryGetFloorCount(out int floorCount) || floorCount <= 0) return;

        _currentFloor = Mathf.Clamp(1, 0, floorCount - 1);
        _floorViewInitialized = true;
        SnapToFloorCenter(_currentFloor);
    }

    private void SnapToFloorCenter(int floorIndex)
    {
        if (!TryGetFloorViewBounds(floorIndex, out Rect bounds)) return;

        Vector3 pos = transform.position;
        pos.x = bounds.center.x;
        pos.y = bounds.center.y;
        transform.position = pos;
    }

    private Vector3 ClampToCurrentFloorBounds(Vector3 pos)
    {
        if (!TryGetFloorViewBounds(_currentFloor, out Rect bounds)) return pos;

        pos.x = Mathf.Clamp(pos.x, bounds.xMin, bounds.xMax);
        pos.y = Mathf.Clamp(pos.y, bounds.yMin, bounds.yMax);
        return pos;
    }

    private static bool TryGetFloorCountStatic(out int floorCount)
    {
        floorCount = 0;
        var cmap = GameSession.Instance?.cmap;
        if (cmap == null || cmap.map.floors == null) return false;
        floorCount = cmap.map.floors.Length;
        return true;
    }

    // floorIndex 층에서 카메라가 실제로 관찰 가능한 월드 범위(중심 클램프 대상) — 원시 범위는
    // MapRandering.TryGetFloorWorldBounds에서 받아오고, 0층만 최좌측 숨김 스폰 청크(Floor0HiddenChunksX)
    // 만큼 왼쪽 경계를 안으로 당겨서 그 칸이 화면에 안 잡히게 한다.
    private static bool TryGetFloorViewBounds(int floorIndex, out Rect bounds)
    {
        bounds = default;
        var mapRandering = GameSession.Instance?.mapRandering;
        if (mapRandering == null || !mapRandering.TryGetFloorWorldBounds(floorIndex, out Rect raw)) return false;

        int hiddenChunksX = floorIndex == 0 ? Floor0HiddenChunksX : 0;
        float xMin = raw.xMin + hiddenChunksX * Floor0ChunkSizeTiles;
        float xMax = Mathf.Max(xMin, raw.xMax); // 방어적 처리(설정 오류로 숨김 청크가 층 폭 이상일 경우)

        bounds = new Rect(xMin, raw.yMin, xMax - xMin, raw.height);
        return true;
    }

}
