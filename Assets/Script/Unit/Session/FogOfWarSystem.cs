using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using VContainer;
using Cysharp.Threading.Tasks;
using Haare.Util.Logger;

// 안개 시스템(2026-07-28, 사용자 요청 "0층, 시작방과 양옆 방을 제외하고는 안개가 생겨. 이 안개는,
// 인접 방으로 플레이어 진영 몬스터가 진입한 경험이 있어야지만 사라져.") + 횃불 배치(같은 날 신규,
// "spot light2d 이용해서 토치 프리팹 생성") — 둘을 한 클래스로 묶은 이유는 횃불이 안개 해제 타이밍에
// 강하게 결합돼 있기 때문이다: 아직 안개가 안 걷힌 방의 횃불은 즉시 스폰하지 않고 대기시켰다가
// RevealRoomFog가 그 방을 걷는 순간 SpawnPendingTorchesForRoom으로 실제 스폰한다(사용자 요청, "안개가
// 있는 방에 토치 미리 생성하지 말고, 안개 걷히고 나서 토치 생성하게 해줘"). GameSession이 지나치게
// 커지는 것을 막기 위해 분리했다(2026-08-20, DoorSystem과 동일한 이유·같은 방식 — UnitRegistry와
// 동일한 지연 GameSession 조회 패턴).
//
// 순수 시각 오버레이라 이동/시야/AI 판정 등 어떤 게임플레이 로직도 건드리지 않는다(Room.FogRevealed는
// UI/시각 목적 전용 플래그).
//
// 외부 호출부(GameSession이 그대로 얇게 위임): RevealRoomFog(UnitFunction.SyncRoomAffiliation),
// RevealFogAroundCapturedRoom(OffenseProcessor 3곳). Initialize()/SpawnTorches()는 GameSession.
// Initialize() 안에서만 쓰여 외부 위임이 필요 없다.
public class FogOfWarSystem
{
    private IObjectResolver _resolver;
    private GameSession Session => _cachedSession ??= _resolver.Resolve<GameSession>();
    private GameSession _cachedSession;

    [Inject]
    public void Construct(IObjectResolver resolver)
    {
        _resolver = resolver;
    }

    // ── 안개 ──────────────────────────────────────────────────────────
    // Tilemap 일괄 렌더링으로 전환(2026-08-24 사용자 요청 "안개가 프레임 드랍을 많이 유발함 —
    // 방 전체를 하나의 안개로 bake") — 이전엔 방 하나(청크 여러 개, 타일 수백 개)당 타일마다
    // GameObject 1개 + 자식 SpriteRenderer 2개(배경/무늬)를 만들어서, 큰 방은 수천 개의
    // GameObject/Transform/SpriteRenderer가 쌓였다. 이제 방/게이트 하나당 Tilemap 2장(배경/무늬)
    // 뿐이라(TilemapRenderer는 칠해진 셀 수와 무관하게 청크 단위로 몇 번의 드로우콜만 낸다) 값은
    // GameObject 개수가 100~1000배 가까이 줄어든다. 그래서 Dictionary 값 타입이 List<GameObject>
    // (타일별 오브젝트 목록)에서 GameObject(배경+무늬 Tilemap 2개를 자식으로 둔 루트 오브젝트
    // 하나) 하나로 바뀌었다 — 아래 SpawnFogBlock/FadeOutAndDestroyFogAsync 참고.
    private readonly Dictionary<Room, GameObject> _roomFogVisuals = new Dictionary<Room, GameObject>();
    // 문/통로 안개(2026-07-28, 사용자 요청 "문이 있는 공간(복도)도 옆방중에 하나라도 안개가 있다면 다
    // 안개로 가리고") — 게이트 하나는 두 방을 잇는 통로라 어느 한 쪽 Room에도 배타적으로 속하지 않는다.
    // (floorIndex, min(roomA,roomB), max(roomA,roomB))로 게이트를 식별해 별도로 추적한다.
    private readonly Dictionary<(int floor, int roomA, int roomB), GameObject> _gateFogVisuals = new Dictionary<(int, int, int), GameObject>();
    private Sprite _fogSprite;
    private Sprite _fogBackingSprite;
    // Tilemap이 공유하는 타일 애셋 2장(배경/무늬) — 방/게이트/빈 청크 등 모든 안개 스폰이 이 둘만
    // 재사용한다(예전처럼 스폰마다 스프라이트를 새로 만들지 않음). TryPrepareFogTiles가 지연 생성.
    private UnityEngine.Tilemaps.Tile _fogBackingTile;
    private UnityEngine.Tilemaps.Tile _fogPatternTile;
    // "스윽 사라지게"(사용자 요청, 구체적 초 수 지정 없음) — 자리표시자, 나중에 조정 요청 오면 이
    // 상수만 바꾸면 됨.
    private const float FogFadeOutSeconds = 0.6f;
    // 후속 신고(2026-07-28, "안개를 통해 몬스터의 상태 보인다") — 위협타일(ThreatTileRenderer,
    // 999)이 안개(기존 900)보다 위라 공격 예고 셀이 뚫고 보였다. 이 값보다 확실히 위여야 유닛/
    // 오브젝트/라벨/위협타일 등 안개 아래 모든 것이 실제로 안 보인다. 안개 자체가 배경(Backing)+
    // 무늬(Pattern) 2겹이라 배경=이 값, 무늬=+1을 쓴다.
    private const int FogSortingOrder = 1000;
    // 후속 신고(2026-07-28, "안개 좀더 선명하게. 약간 투명해서 안에 다 비쳐보여") — obj/fog.png
    // 단독으로는 줄무늬 사이 틈으로 안이 비쳐서, 그 뒤에 불투명에 가까운 단색 배경 한 겹을 깔고
    // 그 위에 무늬를 얹는 2겹 구조로 바꿨다. 배경색 자체(RGB)는 임의 선택 — 조정 요청 오면 이
    // 상수만 바꾸면 됨.
    private static readonly Color FogBackingColor = new Color(0.05f, 0.05f, 0.08f, 0.95f);

    // ── 벽+안개 통합 셰도우 ─────────────────────────────────────────────
    private readonly Dictionary<int, List<GameObject>> _floorFogShadowCasters = new Dictionary<int, List<GameObject>>();

    // ── 횃불 ──────────────────────────────────────────────────────────
    private GameObject _torchPrefab;
    public readonly HashSet<Vector3Int> ActiveTorchPositions = new HashSet<Vector3Int>();
    // 횃불 지연 스폰(2026-07-28, 사용자 요청 "안개가 있는 방에 토치 미리 생성하지 말고, 안개 걷히고
    // 나서 토치 생성하게 해줘") — 아직 안개가 안 걷힌 방의 횃불 배치 좌표는 바로 스폰하지 않고 방
    // 단위로 모아뒀다가, RevealRoomFog가 그 방을 걷는 순간 SpawnPendingTorchesForRoom이 실제로 꺼내
    // 스폰한다.
    private readonly Dictionary<Room, List<(Vector2Int pos, TorchWallSide side)>> _pendingTorchTiles = new Dictionary<Room, List<(Vector2Int, TorchWallSide)>>();

    // GameSession.Initialize()가 맵 역직렬화/방 그리드 구성 직후 한 번 호출한다(옛 이름:
    // InitializeFogOfWar). 0층은 안개 개념 자체가 없고(인류 로비), 1층 이상은 그 층의 시작방
    // (BuildRoomGrid 직후 시점 RoomFaction==Player로 식별 — 아직 전투/점령 변화가 전혀 없는 순수
    // 생성값)과 그 방과 Gate로 직접 연결된 인접 방만 처음부터 안개 없이 시작한다. 문 타일은
    // ApplyOccupationTint/ChangeRoomColor와 동일한 선례(문이 있는 바닥은 점령색 칠도 안 함)를 따라
    // 안개도 씌우지 않는다(문은 이미 별도 스프라이트로 열림/닫힘을 표현).
    public void Initialize()
    {
        CreateMap cmap = Session.cmap;
        List<Room> allRooms = Session.allRooms;
        if (allRooms == null || cmap == null || cmap.map.floors == null) return;

        foreach (var room in allRooms)
        {
            if (room != null) room.FogRevealed = room.Floor == 0;
        }

        // 0층 숨은 스폰 청크 상시 안개(2026-08-23 사용자 요청, "던전 입구 구조 프로그래머 지시서" —
        // "1x3 청크 왼쪽에 플레이어에게 보이지 않는 1x1 청크를 붙여") — 0층은 위에서 보듯 안개 개념
        // 자체가 없어(FogRevealed 항상 true) 이 클래스의 나머지 로직을 전혀 안 타는데, 그동안
        // "안 보임"은 CameraController.Floor0HiddenChunksX(카메라가 그 칸까지 못 가게 관찰 범위 자체를
        // 제한)로만 구현돼 있었다. 카메라 클램프는 안전장치이지 렌더링 차단이 아니므로, 이 청크만
        // 예외적으로 하드코딩 안개를 씌운다 — 방 기반 Reveal 시스템 어디에도 등록하지 않아 어떤
        // 트리거로도 영원히 안 걷힌다.
        SpawnPermanentFogForFloor0HiddenChunk();

        for (int floorIndex = 1; floorIndex < cmap.map.floors.Length; floorIndex++)
        {
            Room startRoom = null;
            foreach (var room in allRooms)
            {
                if (room != null && room.Floor == floorIndex && room.RoomFaction == FactionType.Player)
                {
                    startRoom = room;
                    break;
                }
            }
            if (startRoom == null) continue;
            startRoom.FogRevealed = true;

            Floor floor = cmap.map.floors[floorIndex];
            if (floor.gates == null) continue;
            foreach (Gate g in floor.gates)
            {
                int neighborId = -1;
                if (g.roomA == startRoom.RoomId) neighborId = g.roomB;
                else if (g.roomB == startRoom.RoomId) neighborId = g.roomA;
                if (neighborId < 0) continue;

                Room neighbor = FindRoomByFloorAndId(floorIndex, neighborId);
                if (neighbor != null) neighbor.FogRevealed = true;
            }
        }

        foreach (var room in allRooms)
        {
            if (room != null && !room.FogRevealed) SpawnFogForRoom(room);
        }

        // 문/통로 안개 + 방이 생성되지 않은 청크(2026-07-28, 사용자 요청 "문이 있는 공간(복도)도
        // 옆방중에 하나라도 안개가 있다면 다 안개로 가리고, 벽만 있는, 방이 생성되지 않은 청크도
        // 안개 씌워줘") — 룸 기준 루프가 끝나 모든 Room.FogRevealed가 최종 확정된 뒤에 실행해야
        // "양옆 다 걷혔는지" 판정이 정확하다.
        for (int floorIndex = 1; floorIndex < cmap.map.floors.Length; floorIndex++)
        {
            SpawnFogForEmptyChunks(floorIndex);

            Floor floor = cmap.map.floors[floorIndex];
            if (floor.gates == null) continue;
            foreach (Gate g in floor.gates)
            {
                Room roomA = FindRoomByFloorAndId(floorIndex, g.roomA);
                Room roomB = FindRoomByFloorAndId(floorIndex, g.roomB);
                bool aRevealed = roomA == null || roomA.FogRevealed;
                bool bRevealed = roomB == null || roomB.FogRevealed;
                if (!aRevealed || !bRevealed) SpawnFogForGate(floorIndex, g);
            }

            RebuildFloorFogShadowCasters(floorIndex);
        }
    }

    private Room FindRoomByFloorAndId(int floorIndex, int roomId)
    {
        List<Room> allRooms = Session.allRooms;
        if (allRooms == null || roomId < 0) return null;
        foreach (var r in allRooms)
            if (r != null && r.Floor == floorIndex && r.RoomId == roomId) return r;
        return null;
    }

    private static (int floor, int roomA, int roomB) GateKey(int floorIndex, Gate g)
    {
        int a = Mathf.Min(g.roomA, g.roomB);
        int b = Mathf.Max(g.roomA, g.roomB);
        return (floorIndex, a, b);
    }

    private HashSet<Vector2Int> CollectDoorTilesForFloor(int floorIndex)
    {
        var doorTiles = new HashSet<Vector2Int>();
        CreateMap cmap = Session.cmap;
        if (cmap == null || cmap.map.floors == null || floorIndex < 0 || floorIndex >= cmap.map.floors.Length) return doorTiles;

        Floor floor = cmap.map.floors[floorIndex];
        if (floor.gates == null) return doorTiles;
        foreach (var gate in floor.gates)
            foreach (var row in DoorSystem.GetGateDoorTiles(gate, floor.config.chunkSize))
                foreach (var tile in row)
                    doorTiles.Add(tile);
        return doorTiles;
    }

    // SpawnObject의 단색 폴백 텍스처 생성과 동일한 방식(사용자 요청 "안개 좀더 선명하게, 안 비쳐
    // 보이게") — 불투명에 가까운 배경 한 장을 미리 만들어두고 모든 안개 타일이 공유해서 쓴다.
    private Sprite EnsureFogBackingSprite()
    {
        if (_fogBackingSprite != null) return _fogBackingSprite;

        Texture2D tex = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        _fogBackingSprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        return _fogBackingSprite;
    }

    // 안개 타일 애셋(배경/무늬) 지연 생성 — 최초 1회만 만들고 이후 모든 안개 스폰이 이 둘을
    // 공유한다(2026-08-24, Tilemap 전환). 스프라이트 원본 크기가 1 월드유닛과 정확히 안 맞을 수
    // 있어(예: obj/fog.png의 PPU 설정에 따라) Tile.transform으로 셀 크기(1x1)에 정확히 맞춘다 —
    // 예전 per-GameObject 방식의 patScaleX/Y·backScaleX/Y 계산과 동일한 목적.
    private bool TryPrepareFogTiles()
    {
        if (_fogBackingTile != null && _fogPatternTile != null) return true;

        if (_fogSprite == null) _fogSprite = Resources.Load<Sprite>("obj/fog");
        if (_fogSprite == null)
        {
            LogHelper.Warning(LogHelper.GAME, "TryPrepareFogTiles: Resources.Load<Sprite>(\"obj/fog\")가 null입니다 — Import 설정(Sprite Mode) 확인 필요.");
            return false;
        }
        Sprite backingSprite = EnsureFogBackingSprite();

        Vector2 patternWorldSize = _fogSprite.bounds.size;
        float patScaleX = patternWorldSize.x > 0f ? 1f / patternWorldSize.x : 1f;
        float patScaleY = patternWorldSize.y > 0f ? 1f / patternWorldSize.y : 1f;
        Vector2 backingWorldSize = backingSprite.bounds.size;
        float backScaleX = backingWorldSize.x > 0f ? 1f / backingWorldSize.x : 1f;
        float backScaleY = backingWorldSize.y > 0f ? 1f / backingWorldSize.y : 1f;

        // 배경(불투명에 가까움) — 무늬 텍스처의 줄무늬 틈으로 안이 비쳐 보이지 않도록 항상 먼저
        // 완전히 가린다. 무늬(위) — obj/fog.png 그대로.
        _fogBackingTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        _fogBackingTile.sprite = backingSprite;
        _fogBackingTile.color = FogBackingColor;
        _fogBackingTile.transform = Matrix4x4.Scale(new Vector3(backScaleX, backScaleY, 1f));

        _fogPatternTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        _fogPatternTile.sprite = _fogSprite;
        _fogPatternTile.color = Color.white;
        _fogPatternTile.transform = Matrix4x4.Scale(new Vector3(patScaleX, patScaleY, 1f));

        return true;
    }

    // 안개 "덩어리" 하나(배경+무늬 Tilemap 2장) 생성 — SpawnFogForRoom/SpawnFogForGate/
    // SpawnFogForEmptyChunks/SpawnPermanentFogForFloor0HiddenChunk 공용(2026-08-24, 사용자 요청
    // "안개가 프레임 드랍을 많이 유발함 — 방 전체를 하나의 안개로 bake"로 per-tile GameObject
    // 방식에서 전환). parentGroup(Session.GetFloorCategoryGroup의 "Fog" 그룹)이 이미 그 층의
    // floorOffset만큼 계층 구조로 옮겨져 있으므로(MapRandering.floorTilemaps 자식), 예전처럼
    // 좌표마다 offset을 더할 필요 없이 원본 월드 타일 좌표를 그대로 셀 좌표로 쓴다 — SetParent(...,
    // false)로 로컬 좌표 0을 유지하면 부모 계층의 오프셋이 자동으로 적용된다. 셰도우 캐스트는
    // 여기서 셀 단위로 안 붙인다(사용자 요청, 2026-07-28) — 문 안개는 SpawnFogForGate가 타일들을
    // 다 모은 뒤 윤곽선 하나짜리 캐스터를 별도로 만든다(RebuildFloorFogShadowCasters 참고).
    private GameObject SpawnFogBlock(List<Vector3Int> cells, Transform parentGroup, string namePrefix)
    {
        if (cells == null || cells.Count == 0) return null;
        if (!TryPrepareFogTiles()) return null;

        GameObject root = new GameObject($"Fog_{namePrefix}");
        if (parentGroup != null) root.transform.SetParent(parentGroup, false);

        var cellArray = cells.ToArray();
        var backingTiles = new UnityEngine.Tilemaps.TileBase[cellArray.Length];
        var patternTiles = new UnityEngine.Tilemaps.TileBase[cellArray.Length];
        for (int i = 0; i < cellArray.Length; i++)
        {
            backingTiles[i] = _fogBackingTile;
            patternTiles[i] = _fogPatternTile;
        }

        GameObject backingGo = new GameObject("Backing");
        backingGo.transform.SetParent(root.transform, false);
        var backingTilemap = backingGo.AddComponent<UnityEngine.Tilemaps.Tilemap>();
        var backingRenderer = backingGo.AddComponent<UnityEngine.Tilemaps.TilemapRenderer>();
        backingRenderer.sortingOrder = FogSortingOrder;
        backingTilemap.SetTiles(cellArray, backingTiles);

        GameObject patternGo = new GameObject("Pattern");
        patternGo.transform.SetParent(root.transform, false);
        var patternTilemap = patternGo.AddComponent<UnityEngine.Tilemaps.Tilemap>();
        var patternRenderer = patternGo.AddComponent<UnityEngine.Tilemaps.TilemapRenderer>();
        patternRenderer.sortingOrder = FogSortingOrder + 1;
        patternTilemap.SetTiles(cellArray, patternTiles);

        return root;
    }

    private void SpawnFogForRoom(Room room)
    {
        if (room == null || room.Floor < 0) return;

        HashSet<Vector2Int> doorTiles = CollectDoorTilesForFloor(room.Floor);
        Transform fogGroup = Session.GetFloorCategoryGroup(room.Floor, "Fog");

        var cells = new List<Vector3Int>();
        for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
        {
            for (int y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
            {
                // 청크 전체를 가린다(사용자 요청 "바닥만 가리는게 아니라, 청크 전체 가려야됨") — 벽
                // 타일도 더 이상 건너뛰지 않는다. 문/통로 타일만 예외 — SpawnFogForGate가 두 방의
                // 안개 상태를 함께 보고 따로 처리한다(아래 참고).
                var p2 = new Vector2Int(x, y);
                if (doorTiles.Contains(p2)) continue;
                // Bounds는 사각 경계값이라 방이 L자 등 비직사각형이면 다른 방 타일까지 포함할 수 있다
                // — roomGrid로 실제 소유 방을 재확인해 그 방 타일만 안개로 덮는다.
                if (!Session.roomGrid.TryGetValue(new Vector3Int(x, y, room.Floor), out Room owner) || owner != room) continue;

                cells.Add(new Vector3Int(x, y, 0));
            }
        }

        GameObject block = SpawnFogBlock(cells, fogGroup, room.RoomName);
        if (block != null) _roomFogVisuals[room] = block;
    }

    // 문/통로 안개(2026-07-28, 사용자 요청 "문이 있는 공간(복도)도 옆방중에 하나라도 안개가 있다면 다
    // 안개로 가리고") — 게이트가 잇는 두 방 중 하나라도 아직 FogRevealed==false면 그 게이트의 문
    // 타일들(GetGateDoorTiles) 전체를 안개로 덮는다. 방이 없는 쪽(neighborId가 실제 Room을 못 찾는
    // 경우, 이론상 발생 안 함)은 "이미 걷힌 것"으로 간주해 반대쪽 방 상태만으로 판단한다.
    private void SpawnFogForGate(int floorIndex, Gate g)
    {
        Transform fogGroup = Session.GetFloorCategoryGroup(floorIndex, "Fog");
        int chunkSize = Session.cmap.map.floors[floorIndex].config.chunkSize;

        var cells = new List<Vector3Int>();
        foreach (var row in DoorSystem.GetGateDoorTiles(g, chunkSize))
            foreach (var pos in row)
                cells.Add(new Vector3Int(pos.x, pos.y, 0));

        GameObject block = SpawnFogBlock(cells, fogGroup, "Gate");
        if (block != null) _gateFogVisuals[GateKey(floorIndex, g)] = block;
    }

    // 방이 생성되지 않은 청크(2026-07-28, 사용자 요청 "벽만 있는, 방이 생성되지 않은 청크도 안개
    // 씌워줘") — CreateMap.Chunks.roomId < 0인 청크는 BuildRoomGrid가 아예 roomGrid에 등록하지
    // 않아(어떤 Room에도 안 속함) SpawnFogForRoom 루프에 걸리지 않는다. 이런 청크는 유닛이 물리적으로
    // 들어갈 방법이 없어(벽뿐이거나 방 자체가 없음) 해제 트리거가 있을 수 없으므로 영구 안개로 둔다
    // (별도 딕셔너리 추적 없이 스폰만 하고 끝 — 다른 타일 오브젝트들처럼 씬 파괴 시 자연 정리됨).
    private void SpawnFogForEmptyChunks(int floorIndex)
    {
        CreateMap cmap = Session.cmap;
        if (cmap == null || cmap.map.floors == null || floorIndex <= 0 || floorIndex >= cmap.map.floors.Length) return;
        Floor floor = cmap.map.floors[floorIndex];
        if (floor.chunks == null) return;

        Transform fogGroup = Session.GetFloorCategoryGroup(floorIndex, "Fog");

        int w = floor.config.width, h = floor.config.height, cs = floor.config.chunkSize;
        var cells = new List<Vector3Int>();
        for (int cx = 0; cx < w; cx++)
        {
            for (int cy = 0; cy < h; cy++)
            {
                if (floor.chunks[cx, cy].roomId >= 0) continue;
                for (int tx = 0; tx < cs; tx++)
                    for (int ty = 0; ty < cs; ty++)
                        cells.Add(new Vector3Int(cx * cs + tx, cy * cs + ty, 0));
            }
        }

        SpawnFogBlock(cells, fogGroup, "Void");
    }

    // 0층 최좌측 숨은 스폰 청크(청크 좌표 (0,0) — HumanWaveManager.DungeonEntranceHiddenChunkCenterX가
    // 이 청크의 로컬 중앙을 가리키는 것과 동일한 청크) 전용 상시 안개(2026-08-23). Initialize()가
    // 한 번만 호출한다 — 방 기반 Reveal 트리거(RevealRoomFog 등) 대상이 아니라서 _roomFogVisuals에
    // 등록하지 않고 스폰만 하고 끝(SpawnFogForEmptyChunks의 "빈 청크" 영구 안개와 동일한 패턴). 마지막에
    // 이 청크가 반영된 0층 전용 벽+안개 통합 셰도우도 함께 굽는다(RebuildFloorFogShadowCasters는 원래
    // 1층 이상만 돌았다 — 0층은 안개가 전혀 없었으므로). 청크 타일 크기는 별도 상수로 들고 있지 않고
    // cmap.map.floors[0].config.chunkSize를 그대로 읽는다(맵 1.5배 확장, 2026-08-23 "0층은 청크 크기만
    // 늘려" — CreateMap의 실제 생성 설정과 항상 일치시키기 위함, 손으로 맞춰야 하는 별도 상수가 아님).
    // 상/하/좌 여유 안개(2026-08-23 사용자 요청 "0층 상, 하, 좌 부분 안개를 1칸씩 늘려줘") — 청크
    // 경계에 정확히 맞춰 깔면 카메라 클램프/벽 렌더링과의 미세한 오차로 가장자리에 틈이 보일 위험이
    // 있어 안전 여유분을 둔다. 우측(=보이는 1x3 던전 입구와 맞닿는 면)만 그대로 둔다 — 그쪽까지
    // 늘리면 실제로 보여야 할 구역을 침범한다. 시각적 스프라이트 오버레이라 실제 맵 타일 범위를
    // 벗어난 좌표에 놓여도 그냥 빈 배경 위에 그려질 뿐 문제없다. 청크 크기가 커져도 이 여유분 자체는
    // "렌더링 오차 흡수용 1타일"이라는 목적이 그대로라 스케일하지 않는다.
    private const int Floor0HiddenFogPadding = 1;

    private void SpawnPermanentFogForFloor0HiddenChunk()
    {
        CreateMap cmap = Session.cmap;
        if (cmap == null || cmap.map.floors == null || cmap.map.floors.Length == 0) return;

        Transform fogGroup = Session.GetFloorCategoryGroup(0, "Fog");
        int chunkTiles = cmap.map.floors[0].config.chunkSize;

        int xStart = -Floor0HiddenFogPadding;
        int xEnd = chunkTiles; // 우측 경계는 확장하지 않음.
        int yStart = -Floor0HiddenFogPadding;
        int yEnd = chunkTiles + Floor0HiddenFogPadding;

        var cells = new List<Vector3Int>();
        for (int tx = xStart; tx < xEnd; tx++)
            for (int ty = yStart; ty < yEnd; ty++)
                cells.Add(new Vector3Int(tx, ty, 0));

        SpawnFogBlock(cells, fogGroup, "Floor0Hidden");

        RebuildFloorFogShadowCasters(0);
    }

    // 벽 + "아직 안 걷힌 안개" 전체를 하나의 격자로 합쳐서 한 번에 윤곽선을 뽑는다(사용자 요청,
    // 2026-07-28 "지금 문에잇는 안개만 섀도캐스팅 박혀있어. 문+ 벽 섀도우캐스팅과 겹치는 안개 모두
    // 한번에 해서 구워줘"). 안개(방/문/빈 청크)를 벽과 따로따로 셰도우 캐스팅하면 방 경계에서 벽의
    // 자체 셰도우와 거의 겹치는 별개의 선이 하나 더 생길 뿐이라 눈에 띄는 차이가 없었다 — 벽이 이미
    // 막고 있는 경계를 안개가 다시 막아봤자 티가 안 남. 대신 벽 마스크와 "안 걷힌 안개" 마스크를
    // OR로 합친 뒤 그 결과를 통째로 외곽선 추적하면, 문(벽이 없는 구간)처럼 벽만으로는 안 막히는
    // 지점까지 안개가 자연스럽게 이어 붙어 하나의 연속된 경계가 된다(이음새 없음).
    //
    // 안개는 방이 걷힐 때마다 바뀌는 동적 상태라, 벽처럼 한 번만 굽고 끝낼 수 없다 — 그 방이 있는
    // 층 전체를 RevealRoomFog/Initialize에서 다시 구워(재계산) 갈아 끼운다. 잦은 일이 아니라(웨이브
    // 진행에 따라 방 하나 걷힐 때만) 층 전체를 매번 다시 훑어도 성능 문제는 없다.
    private bool[,] BuildStillFoggedMask(int floorIndex, int worldW, int worldH)
    {
        var mask = new bool[worldW, worldH];
        Floor floor = Session.cmap.map.floors[floorIndex];

        for (int x = 0; x < worldW; x++)
        {
            for (int y = 0; y < worldH; y++)
            {
                if (Session.roomGrid.TryGetValue(new Vector3Int(x, y, floorIndex), out Room owner) && owner != null)
                    mask[x, y] = !owner.FogRevealed;
                else
                    mask[x, y] = true; // 방이 없는 칸(빈 청크) — 영구 안개
            }
        }

        // 0층 숨은 스폰 청크(SpawnPermanentFogForFloor0HiddenChunk) — 0층 Room.FogRevealed는 항상
        // true라 위 방 기반 판정만으로는 이 칸이 "안 걷힌 것"으로 안 잡힌다. 여기서 강제로 덮어써야
        // 벽+안개 통합 셰도우 캐스터가 이 칸도 실제로 빛을 막아준다.
        if (floorIndex == 0)
        {
            int hiddenX = Mathf.Min(floor.config.chunkSize, worldW);
            for (int x = 0; x < hiddenX; x++)
                for (int y = 0; y < worldH; y++)
                    mask[x, y] = true;
        }

        // 게이트(문) 타일은 두 방 중 하나라도 안 걷혔으면 안개 — 각 타일이 속한 청크의 개별 방
        // 판정과 별개로 덮어쓴다(SpawnFogForGate와 동일한 규칙).
        if (floor.gates != null)
        {
            foreach (var g in floor.gates)
            {
                Room roomA = FindRoomByFloorAndId(floorIndex, g.roomA);
                Room roomB = FindRoomByFloorAndId(floorIndex, g.roomB);
                bool aRevealed = roomA == null || roomA.FogRevealed;
                bool bRevealed = roomB == null || roomB.FogRevealed;
                bool stillFogged = !aRevealed || !bRevealed;

                foreach (var row in DoorSystem.GetGateDoorTiles(g, floor.config.chunkSize))
                    foreach (var pos in row)
                        if (pos.x >= 0 && pos.x < worldW && pos.y >= 0 && pos.y < worldH)
                            mask[pos.x, pos.y] = stillFogged;
            }
        }

        return mask;
    }

    // 재구성 시 빛이 한 프레임 새는 문제 수정(사용자 확인, 2026-07-28 "새로 구울때 한번 번쩍거리면서
    // 빛이 새는데") — 원인은 순서: 기존 캐스터를 먼저 Destroy()하면 실제 파괴/등록 해제는 그 프레임
    // 렌더링 전에 일어나는데, 새로 만든 ShadowCaster2D는 자기 Update()가 최소 한 번 돌아야 셰도우
    // 그룹에 실제로 등록된다(ShadowCaster2D.Update() 내부에서 등록 — Awake 시점엔 아직 미등록). 즉
    // "새 걸 등록하기 전에 기존 걸 지우는" 순간 사이에 이 층 전체가 무방비 상태인 프레임이 한 번
    // 생겨서 그 프레임에 빛이 새어 보였다. 그래서 새 캐스터를 먼저 만들어 등록될 시간을 확실히 준
    // 뒤에(2프레임 대기) 기존 걸 지우는 순서로 바꿨다 — 겹치는 몇 프레임 동안 신/구 캐스터가 같이
    // 있어도 중복으로 막아줄 뿐 문제 없다.
    // 2026-08-24 debug 전용(GameSession.DebugConvertFloorTileToWall) — 맵 데이터에 벽 타일을 즉석에서
    // 추가한 뒤, 빛(Light2D)이 그 벽도 막게 벽+안개 통합 셰도우 캐스터를 다시 굽는다. RevealRoomFog 등
    // 기존 호출부와 동일한 private 메서드를 그대로 재사용 — 이 메서드는 floorIndex==0을 포함해 항상
    // MapRandering.BuildWallMask를 그 시점 데이터로 새로 읽으므로 방금 바뀐 벽도 곧바로 반영된다.
    public void NotifyFloorGeometryChanged(int floorIndex) => RebuildFloorFogShadowCasters(floorIndex);

    private void RebuildFloorFogShadowCasters(int floorIndex)
    {
        CreateMap cmap = Session.cmap;
        if (cmap == null || cmap.map.floors == null || floorIndex < 0 || floorIndex >= cmap.map.floors.Length) return;
        Floor floor = cmap.map.floors[floorIndex];
        if (floor.chunks == null) return;

        bool[,] isWall = MapRandering.BuildWallMask(ref floor, out int worldW, out int worldH);
        bool[,] stillFogged = BuildStillFoggedMask(floorIndex, worldW, worldH);

        bool[,] combined = new bool[worldW, worldH];
        for (int x = 0; x < worldW; x++)
            for (int y = 0; y < worldH; y++)
                combined[x, y] = isWall[x, y] || stillFogged[x, y];

        Transform fogGroup = Session.GetFloorCategoryGroup(floorIndex, "Fog");
        var loops = MapRandering.TraceContours(combined, worldW, worldH);
        var created = new List<GameObject>();
        MapRandering.CreateEdgeShadowCasters(fogGroup, loops, "FloorShadowCaster", created);

        List<GameObject> oldCasters = _floorFogShadowCasters.TryGetValue(floorIndex, out var prev) ? prev : null;
        _floorFogShadowCasters[floorIndex] = created;

        if (oldCasters != null && oldCasters.Count > 0)
            DestroyAfterFramesAsync(oldCasters).Forget();
    }

    private async UniTaskVoid DestroyAfterFramesAsync(List<GameObject> toDestroy)
    {
        await UniTask.DelayFrame(2);
        foreach (var go in toDestroy)
            if (go != null) UnityEngine.Object.Destroy(go);
    }

    // 안개 해제(2026-07-28, 사용자 요청 "인접 방으로 플레이어 진영 몬스터가 진입한 경험이 있어야지만
    // 사라져") — UnitFunction.SyncRoomAffiliation이 플레이어 진영 몬스터의 방 최초 입장을 감지하면
    // 호출한다. Room.FogRevealed는 한번 true가 되면 다시 false로 안 돌아간다(영구 해제).
    public void RevealRoomFog(Room room)
    {
        if (room == null || room.FogRevealed) return;
        room.FogRevealed = true;

        if (_roomFogVisuals.TryGetValue(room, out GameObject block) && block != null)
        {
            _roomFogVisuals.Remove(room);
            FadeOutAndDestroyFogAsync(block).Forget();
        }

        // 횃불 지연 스폰(2026-07-28, 사용자 요청 "안개가 있는 방에 토치 미리 생성하지 말고, 안개
        // 걷히고 나서 토치 생성하게 해줘") — 이 방을 위해 대기 중이던 횃불이 있으면 지금 배치한다.
        SpawnPendingTorchesForRoom(room);

        TryRevealAdjacentGates(room);

        // 이 방(과 인접 게이트)의 FogRevealed가 방금 바뀌었으니 벽+안개 통합 셰도우도 다시 굽는다
        // (RebuildFloorFogShadowCasters 주석 참고).
        RebuildFloorFogShadowCasters(room.Floor);
    }

    // 안개 해금 규칙 변경(2026-07-28, 사용자 요청 "안개 해금이 잘 안돼. 규칙을 바꾸자. 플레이어
    // 유닛이 해당 방을 점령한 적이 있으면 인접 방의 안개가 사라지도록 처리하자") — 기존 "유닛이 방에
    // 물리적으로 들어오면 그 방 자체의 안개가 사라짐"(RevealRoomFog, UnitFunction.SyncRoomAffiliation
    // 트리거)은 신뢰도가 낮았다고 판단해(RoomConfinedMovement로 방 밖 자율 이동이 막혀 있고, 플레이어
    // 명령도 이미 소유/인접 방으로만 제한돼 있어 "새 방에 처음 들어가는" 순간 자체가 드물게만 발생)
    // 그대로 안전장치로 남겨두고, 훨씬 확실한 이벤트인 "점령"(OffenseProcessor의
    // TryResolveRoomOwnership/TryClaimEmptyRoomOnEntry/OnOffenseSuccess 세 경로가 Room.RoomFaction을
    // Player로 확정하는 순간)을 새 트리거로 추가한다. 점령된 방 자기 자신과, 그 방과 Gate로 직접
    // 연결된 인접 방들의 안개를 함께 걷는다(2칸 이상 떨어진 방까지 한꺼번에 열리진 않음 — 정확히
    // "인접 방"까지만).
    public void RevealFogAroundCapturedRoom(Room room)
    {
        if (room == null) return;
        RevealRoomFog(room);

        CreateMap cmap = Session.cmap;
        if (cmap == null || cmap.map.floors == null) return;
        if (room.Floor < 0 || room.Floor >= cmap.map.floors.Length) return;
        Floor floor = cmap.map.floors[room.Floor];
        if (floor.gates == null) return;

        foreach (Gate g in floor.gates)
        {
            if (g.roomA != room.RoomId && g.roomB != room.RoomId) continue;
            int neighborId = g.roomA == room.RoomId ? g.roomB : g.roomA;
            Room neighbor = FindRoomByFloorAndId(room.Floor, neighborId);
            if (neighbor != null) RevealRoomFog(neighbor);
        }
    }

    // 문/통로 안개 해제 — room이 막 걷혔을 때, 그 room과 맞닿은 게이트들 중 반대쪽 방도 이미 걷혀
    // 있으면(즉 양쪽 다 FogRevealed) 그 게이트의 안개도 같이 걷는다. 아직 반대쪽이 안 걷혔으면
    // 그대로 둔다("옆방중에 하나라도 안개가 있다면 다 안개로 가리고").
    private void TryRevealAdjacentGates(Room room)
    {
        CreateMap cmap = Session.cmap;
        if (room == null || cmap == null || cmap.map.floors == null) return;
        if (room.Floor < 0 || room.Floor >= cmap.map.floors.Length) return;
        Floor floor = cmap.map.floors[room.Floor];
        if (floor.gates == null) return;

        foreach (Gate g in floor.gates)
        {
            if (g.roomA != room.RoomId && g.roomB != room.RoomId) continue;
            int neighborId = g.roomA == room.RoomId ? g.roomB : g.roomA;
            Room neighbor = FindRoomByFloorAndId(room.Floor, neighborId);
            bool neighborRevealed = neighbor == null || neighbor.FogRevealed;
            if (!neighborRevealed) continue;

            var key = GateKey(room.Floor, g);
            if (_gateFogVisuals.TryGetValue(key, out GameObject block) && block != null)
            {
                _gateFogVisuals.Remove(key);
                FadeOutAndDestroyFogAsync(block).Forget();
            }
        }
    }

    // "스윽 사라지게"(사용자 요청) — SceneTransitionFade.FadeAsync와 동일한 UniTask 알파 페이드 관례.
    // Tilemap 전환(2026-08-24) 이후로는 배경/무늬 각각 Tilemap.color(전체 셀에 곱해지는 틴트) 하나만
    // 0으로 보간하면 된다 — Tile 자산 자체의 baked 알파(배경 0.95/무늬 1.0)에 곱해지므로, 예전처럼
    // 렌더러마다 "시작 알파"를 따로 기억해둘 필요 없이 둘 다 1→0으로 페이드해도 배경이 잠깐 더
    // 진해지는 튐 없이 동일한 결과가 나온다.
    private async UniTaskVoid FadeOutAndDestroyFogAsync(GameObject block)
    {
        if (block == null) return;
        var tilemaps = block.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>();

        float t = 0f;
        while (t < FogFadeOutSeconds)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t / FogFadeOutSeconds));
            foreach (var tm in tilemaps)
            {
                if (tm == null) continue;
                Color c = tm.color;
                c.a = alpha;
                tm.color = c;
            }
            await UniTask.Yield();
        }

        if (block != null) UnityEngine.Object.Destroy(block);
    }

    // ── 횃불 ──────────────────────────────────────────────────────────
    // 횃불 배치(2026-07-28, 사용자 요청 "spot light2d 이용해서 토치 프리팹 생성하도록 해봐. 생성
    // 로직은 동일함. 프리팹은 너가 직접 생성해서 실제 파일로 존재해야 해") — Assets/Resources/
    // Prefabs/VFX/Torch.prefab(SpriteRenderer + Light2D(Point/원형, 따뜻한 색, 반경 4~6))을 Resources.Load로
    // 불러와 Instantiate한다. 0층 전용 "층 전체를 덮는 대형 횃불 하나" 예외는 폐지됐다(사용자 요청
    // "0층 예외 지우고, 0층 청크도 기존 규칙에 따라 토치 깔아줘") — 0층도 다른 층과 완전히 동일한
    // 청크 단위 배치를 받는다.
    //
    // 배치 로직 재설계(2026-08-21, 사용자 요청) — 예전엔 청크 정중앙 바닥 타일에 놓았지만, 이제
    // "복도(게이트)가 뚫리지 않은 완전히 막힌 벽 1면"을 청크당 최대 1개 랜덤으로 골라 그 벽의 중앙
    // 바로 앞(벽에 맞닿은 바닥 칸)에 놓는다 — TryFindTorchTilePos 및 그 안에서 쓰는
    // CreateMap.IsSolidWallEdge/TryFindFloorTileInFrontOfWall 참고(청크 경계가 벽인지/게이트인지는
    // 지도 생성 계층이 이미 담당하는 지식이라 그쪽으로 옮겼다 — 이 클래스는 여전히 "언제·어디에 뭘
    // 놓을지"만 결정하고 타일 이름 자체는 들여다보지 않는다). 그런 벽이 하나도 없는 청크(예: 사방이
    // 게이트로 뚫렸거나 다른 방 청크와 완전히 붙어있는 내부 청크)는 횃불을 놓지 않는다. 계단 전용
    // 후보 로직은 이제 필요 없다 — 계단은 항상 청크 내부(로컬 (3,3)~(4,4))에 있어 벽에 붙는 바닥
    // 후보와 겹치지 않고, 혹시 겹치더라도 TryFindFloorTileInFrontOfWall이 "Floor"가 아닌 타일(Stair
    // 포함)에서 멈추면 실패 처리하므로 안전하다.
    //
    // 렌더 순서(2026-08-21, 사용자 요청 "다른 오브젝트들이랑 겹쳤을때 최상단에 위치하게") —
    // Torch.prefab의 SpriteRenderer.sortingOrder를 5(바닥 오브젝트/건물/계단 아이콘 공통값)에서
    // 51로 올렸다. 유닛(8~11)·선택 마커(9)·상태 라벨(20)·방 인구수 라벨(50)까지 전부 위지만, 위협
    // 타일 셀(999)·소리전파 디버그(998)·안개(1000~)처럼 항상 최상단이어야 하는 특수 오버레이보다는
    // 아래다(GameSession.CreateRoomPopulationLabel의 50 선택과 동일한 관례 — "일반 오브젝트보다 위,
    // 특수 오버레이보다는 아래").
    public void SpawnTorches()
    {
        CreateMap cmap = Session.cmap;
        if (cmap == null || cmap.map.floors == null) return;
        if (_torchPrefab == null) _torchPrefab = Resources.Load<GameObject>("Prefabs/VFX/Torch");
        if (_torchPrefab == null)
        {
            LogHelper.Warning(LogHelper.GAME, "SpawnTorches: Resources.Load<GameObject>(\"Prefabs/VFX/Torch\")가 null입니다.");
            return;
        }

        for (int floorIdx = 0; floorIdx < cmap.map.floors.Length; floorIdx++)
        {
            Floor floor = cmap.map.floors[floorIdx];
            if (floor.chunks == null) continue;

            int w = floor.config.width, h = floor.config.height;
            for (int cx = 0; cx < w; cx++)
            {
                for (int cy = 0; cy < h; cy++)
                {
                    // 0층 최좌측 숨은 스폰 청크(SpawnPermanentFogForFloor0HiddenChunk와 동일한 청크,
                    // 2026-08-23 사용자 요청 "그 청크에는 횃불 생성 안되어야 해") — 상시 안개로 덮여
                    // 있어 어차피 안 보이는 데다, 횃불 Light2D가 안개 경계 너머로 새어 보일 여지 자체를
                    // 없앤다.
                    if (floorIdx == 0 && cx == 0) continue;

                    Chunks c = floor.chunks[cx, cy];
                    if (c.roomId < 0 || c.chunk == null) continue;

                    if (!TryFindTorchTilePos(floorIdx, cx, cy, c, out Vector2Int tilePos, out TorchWallSide side)) continue;

                    Room room = FindRoomByFloorAndId(floorIdx, c.roomId);
                    if (room != null && !room.FogRevealed)
                    {
                        if (!_pendingTorchTiles.TryGetValue(room, out var pending))
                        {
                            pending = new List<(Vector2Int, TorchWallSide)>();
                            _pendingTorchTiles[room] = pending;
                        }
                        pending.Add((tilePos, side));
                        continue;
                    }

                    SpawnTorchAt(floorIdx, tilePos, side);
                }
            }
        }

        LogHelper.Log(LogHelper.GAME, "SpawnTorches: 횃불 배치 완료(안개가 안 걷힌 방은 대기열로 보류).");
    }

    // 횃불 지연 스폰 전용 — RevealRoomFog가 room을 막 걷었을 때 호출된다. 그 방을 위해 쌓여있던
    // 대기 좌표가 있으면 지금 실제로 Instantiate한다.
    private void SpawnPendingTorchesForRoom(Room room)
    {
        if (room == null) return;
        if (!_pendingTorchTiles.TryGetValue(room, out var pending)) return;
        _pendingTorchTiles.Remove(room);

        if (_torchPrefab == null) _torchPrefab = Resources.Load<GameObject>("Prefabs/VFX/Torch");
        if (_torchPrefab == null) return;

        foreach (var entry in pending)
            SpawnTorchAt(room.Floor, entry.pos, entry.side);
    }

    // 청크의 4면(위/오른쪽/아래/왼쪽) 중 "복도(게이트)로 뚫리지 않은 완전히 막힌 벽"만 후보로 모아
    // 그중 하나를 랜덤으로 고르고(사용자 요청 "랜덤 벽 1개만"), 그 벽 중앙 바로 앞의 바닥 칸을 반환한다.
    // 후보가 하나도 없으면(벽이 없는 청크) false — 그 청크는 횃불을 놓지 않는다. "이 청크 경계가
    // 벽인지/게이트인지"는 CreateMap.IsSolidWallEdge/TryFindFloorTileInFrontOfWall(지도 생성 계층
    // 소유 지식, 2026-08-21 이관)에 위임하고, 여기서는 여러 방 중 "언제·어디에" 횃불을 놓을지만 결정한다.
    private bool TryFindTorchTilePos(int floorIdx, int cx, int cy, Chunks c, out Vector2Int tilePos, out TorchWallSide side)
    {
        tilePos = default;
        side = default;

        var candidates = new List<TorchWallSide>(4);
        foreach (TorchWallSide s in _allWallSides)
            if (CreateMap.IsSolidWallEdge(c, s)) candidates.Add(s);

        if (candidates.Count == 0) return false;

        side = candidates[UnityEngine.Random.Range(0, candidates.Count)];

        if (!CreateMap.TryFindFloorTileInFrontOfWall(c, side, out Vector2Int local)) return false;

        int cs = c.chunk.GetLength(0);
        Vector2Int cand = new Vector2Int(cx * cs + local.x, cy * cs + local.y);
        if (Session.objectGrid.ContainsKey(new Vector3Int(cand.x, cand.y, floorIdx))) return false;

        tilePos = cand;
        return true;
    }

    private static readonly TorchWallSide[] _allWallSides =
        { TorchWallSide.Top, TorchWallSide.Right, TorchWallSide.Bottom, TorchWallSide.Left };

    private GameObject SpawnTorchAt(int floorIdx, Vector2Int tilePos, TorchWallSide side)
    {
        MapRandering mapRandering = Session.mapRandering;
        Vector3 offset = (mapRandering != null && mapRandering.floorOffsets != null && floorIdx < mapRandering.floorOffsets.Length)
            ? mapRandering.floorOffsets[floorIdx] : Vector3.zero;
        Transform torchGroup = Session.GetFloorCategoryGroup(floorIdx, "Torches");
        Vector3 worldPos = new Vector3(tilePos.x + 0.5f, tilePos.y + 0.5f, 0f) + offset;

        GameObject go = UnityEngine.Object.Instantiate(_torchPrefab, worldPos, Quaternion.identity);
        go.name = $"Torch_{tilePos.x}_{tilePos.y}";
        if (torchGroup != null) go.transform.SetParent(torchGroup, true);
        ActiveTorchPositions.Add(new Vector3Int(tilePos.x, tilePos.y, floorIdx));

        // 방향별 스프라이트 적용 + 위치 보정은 Visual 계층(TorchVisual.cs)이 전담한다(2026-08-21
        // 정리 — 이 클래스는 SpriteResolver를 직접 건드리지 않는다). 위치 보정은 루트(=Light2D가
        // 달린 실제 광원 위치)가 아니라 스프라이트 전용 자식 "Visual"의 로컬 좌표에만 적용된다
        // (사용자 요청 "스프라이트 오프셋만 조절되고, 생성 위치 자체는 그대로인거로... 빛 때문에
        // 그럼") — 루트를 옮기면 빛도 같이 밀려서 실제 타일 앞이 아닌 곳을 비추게 되기 때문이다.
        TorchVisual.ApplyTorchVisual(go, side);
        return go;
    }
}
