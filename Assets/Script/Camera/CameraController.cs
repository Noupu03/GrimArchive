using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public float panSpeed = 35f;
    public float zoomSpeed = 0.05f; // Input system의 스크롤 값은 크므로 조절
    public float minZoom = 5f;
    public float maxZoom = 50f;

    // 층별 카메라 전환(2026-08-20, "층별 카메라 전환 프로그래머 지시서") — 층 전환은 유닛 이동이
    // 아니라 카메라 관찰 위치 변경으로만 처리한다(문서 명시). 별도의 Fog of War/렌더링 분리 시스템은
    // 쓰지 않고(MapRandering.ComputeSpacedOffsets가 이미 층마다 가로로 충분히 떨어뜨려 배치해둠),
    // 카메라가 "지금 보고 있는 층" 범위 밖으로 못 나가게 막는 것만으로 "다른 층이 동시에 노출되지
    // 않음"을 만족시킨다. 층의 원시 월드 범위 자체는 MapRandering.TryGetFloorWorldBounds가 계산한다
    // (ChunkSize를 이미 그쪽이 들고 있어 여기서 매직넘버로 다시 정의하지 않는다) — 이 클래스는 그
    // 범위에 "0층 숨은 스폰 청크 제외" 같은 카메라 고유의 조정만 얹는다.
    // 던전 입구 구조(2026-08-19, "던전 입구 구조 프로그래머 지시서")의 0층 최좌측 1x1 청크는 인간
    // 파티가 등장하는, 플레이어에게 보이지 않아야 하는 칸이다 — 별도 렌더링 은폐 없이 카메라가 그
    // 칸까지 가지 못하게 관찰 가능 범위 자체에서 제외해 "안 보임"을 구현한다.
    private const int Floor0HiddenChunksX = 1;
    private const int ChunkSizeTiles = 8; // Floor0HiddenChunksX(청크 단위)를 타일 단위로 환산할 때만 씀.

    private int _currentFloor = -1; // -1 = 아직 초기화 전(맵 로드 대기 중).
    private bool _floorViewInitialized = false;

    // UI 리뉴얼(2026-08-20, "맵" 하단 메뉴 서브메뉴가 층 버튼 4개로 직접 층을 지정) — BottomMenuBar가
    // VContainer 주입 대상이 아니라 정적 접근이 필요해 다른 매니저들(NoticeCenter.Instance 등)과 동일한
    // 관례를 따른다.
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

    // 특정 층으로 즉시 이동(맵 메뉴 버튼용) — SwitchFloor(방향)와 달리 절대 인덱스를 받는다.
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
        // AutoAttach가 씬 로드 직후(맵/GameSession 생성보다 먼저) 실행될 수 있어, 층 범위 정보가
        // 준비될 때까지는 관찰 층 개념 없이 대기한다 — 이 지연 초기화가 끝나기 전엔 패닝만 자유롭게
        // 허용(기존 동작 그대로)하고 층 클램프는 걸지 않는다.
        EnsureFloorViewInitialized();

        Vector3 pos = transform.position;

        if (Keyboard.current != null)
        {
            float move = panSpeed * Time.unscaledDeltaTime;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) pos.y += move;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) pos.y -= move;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) pos.x += move;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) pos.x -= move;

            if (Keyboard.current.leftBracketKey.wasPressedThisFrame) SwitchFloor(-1);
            if (Keyboard.current.rightBracketKey.wasPressedThisFrame) SwitchFloor(1);
        }

        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (scroll != 0.0f)
            {
                Camera.main.orthographicSize -= scroll * zoomSpeed;
                Camera.main.orthographicSize = Mathf.Clamp(Camera.main.orthographicSize, minZoom, maxZoom);
            }
        }

        if (_floorViewInitialized) pos = ClampToCurrentFloorBounds(pos);

        transform.position = pos;
    }

    // 맵이 준비되는 순간(GameSession/CreateMap이 아직 없을 수 있는 씬 로드 직후 몇 프레임을 버틴 뒤)
    // 딱 한 번 초기 관찰 층을 정한다 — 플레이어가 실제로 몬스터를 배치·조작하는 1층을 기본값으로
    // 삼는다(0층은 인간 파티 대기 공간일 뿐 플레이어가 조작할 대상이 없음).
    private void EnsureFloorViewInitialized()
    {
        if (_floorViewInitialized) return;
        if (!TryGetFloorCount(out int floorCount) || floorCount <= 0) return;

        _currentFloor = Mathf.Clamp(1, 0, floorCount - 1);
        _floorViewInitialized = true;
        SnapToFloorCenter(_currentFloor);
    }

    private void SwitchFloor(int direction)
    {
        if (!_floorViewInitialized || !TryGetFloorCount(out int floorCount) || floorCount <= 0) return;

        int next = Mathf.Clamp(_currentFloor + direction, 0, floorCount - 1);
        if (next == _currentFloor) return;

        _currentFloor = next;
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
        float xMin = raw.xMin + hiddenChunksX * ChunkSizeTiles;
        float xMax = Mathf.Max(xMin, raw.xMax); // 방어적 처리(설정 오류로 숨김 청크가 층 폭 이상일 경우)

        bounds = new Rect(xMin, raw.yMin, xMax - xMin, raw.height);
        return true;
    }

}
