using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using VContainer;
using Cysharp.Threading.Tasks;
using Haare.Util.Logger;

// 안개 시스템(0층·시작방·인접방 제외, 플레이어 진영 진입 경험 필요) + 횃불 배치를 한 클래스로 묶었다
// — 안개 안 걷힌 방의 횃불 스폰은 RevealRoomFog가 안개를 걷는 순간까지 대기한다. 순수 시각
// 오버레이라 이동/시야/AI 등 게임플레이 로직은 건드리지 않는다(Room.FogRevealed는 시각 전용 플래그).
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
    // 방/게이트 하나당 Tilemap 2장(배경/무늬)으로 일괄 렌더링한다 — 타일별 GameObject 방식은 큰
    // 방에서 수천 개가 쌓여 프레임 드랍을 유발하므로 청크 단위 드로우콜로 대체했다.
    private readonly Dictionary<Room, GameObject> _roomFogVisuals = new Dictionary<Room, GameObject>();
    // 문/통로 안개 — 게이트 하나는 두 방을 잇는 통로라 어느 한 쪽 Room에도 배타적으로 속하지 않는다.
    // (floorIndex, min(roomA,roomB), max(roomA,roomB))로 게이트를 식별해 별도로 추적한다.
    private readonly Dictionary<(int floor, int roomA, int roomB), GameObject> _gateFogVisuals = new Dictionary<(int, int, int), GameObject>();
    private Sprite _fogSprite;
    private Sprite _fogBackingSprite;
    // Tilemap이 공유하는 타일 애셋 2장(배경/무늬) — 방/게이트/빈 청크 등 모든 안개 스폰이 이 둘만
    // 재사용한다(스폰마다 스프라이트를 새로 만들지 않음). TryPrepareFogTiles가 지연 생성.
    private UnityEngine.Tilemaps.Tile _fogBackingTile;
    private UnityEngine.Tilemaps.Tile _fogPatternTile;
	// 런타임 생성 스프라이트가 이 빌드에서 렌더링되지 않는 문제 우회: 안개를 Lit 셰이더로 바꾸고
	// 벽+안개 통합 셰도우 캐스터로 그 자리 빛을 차단해 항상 검정이 되게 한다.
	private Material _fogMaterial;
    private const float FogFadeOutSeconds = 0.6f;
    // 위협타일(ThreatTileRenderer, 999)보다 확실히 위여야 유닛/오브젝트/라벨 등이 안개 아래로 실제로
    // 안 보인다. 안개는 배경(Backing)+무늬(Pattern) 2겹이라 배경=이 값, 무늬=+1을 쓴다.
    private const int FogSortingOrder = 1000;
    // obj/fog.png 단독으로는 줄무늬 사이 틈으로 안이 비쳐서, 그 뒤에 불투명에 가까운 단색 배경 한
    // 겹을 깔고 그 위에 무늬를 얹는 2겹 구조로 바꿨다. 배경색 자체(RGB)는 임의 선택.
    private static readonly Color FogBackingColor = new Color(0.05f, 0.05f, 0.08f, 0.95f);

    // ── 벽+안개 통합 셰도우 ─────────────────────────────────────────────
    private readonly Dictionary<int, List<GameObject>> _floorFogShadowCasters = new Dictionary<int, List<GameObject>>();

    // ── 횃불 ──────────────────────────────────────────────────────────
    private GameObject _torchPrefab;
    public readonly HashSet<Vector3Int> ActiveTorchPositions = new HashSet<Vector3Int>();
    // 횃불 지연 스폰 — 아직 안개가 안 걷힌 방의 횃불 배치 좌표는 바로 스폰하지 않고 방 단위로 모아
    // 뒀다가, RevealRoomFog가 그 방을 걷는 순간 SpawnPendingTorchesForRoom이 꺼내 스폰한다.
    private readonly Dictionary<Room, List<(Vector2Int pos, TorchWallSide side)>> _pendingTorchTiles = new Dictionary<Room, List<(Vector2Int, TorchWallSide)>>();

    // GameSession.Initialize()가 맵 구성 직후 한 번 호출한다. 0층은 안개 개념이 없고, 1층 이상은
    // 시작방(RoomFaction==Player, 생성 시점 값)과 그 인접 방만 처음부터 안개 없이 시작한다. 문
    // 타일은 별도 스프라이트로 개폐를 표현하므로 안개를 씌우지 않는다.
    public void Initialize()
    {
        CreateMap cmap = Session.cmap;
        List<Room> allRooms = Session.allRooms;
        if (allRooms == null || cmap == null || cmap.map.floors == null) return;

        foreach (var room in allRooms)
        {
            if (room != null) room.FogRevealed = room.Floor == 0;
        }

        // 0층 최좌측 숨은 스폰 청크(던전 입구 옆 1x1, 카메라 범위 제한만으로 가려져 있던 곳)에
        // 예외적으로 하드코딩 안개를 씌운다 — 어떤 Reveal 트리거에도 등록되지 않아 영원히 안 걷힌다.
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

        // 문/통로 안개 + 빈 청크 안개 처리 — 모든 Room.FogRevealed가 확정된 뒤 실행해야 양옆 판정이
        // 정확하다.
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

    // SpawnObject의 단색 폴백 텍스처 생성과 동일한 방식 — 불투명에 가까운 배경 한 장을 미리 만들어
    // 모든 안개 타일이 공유한다.
    private Sprite EnsureFogBackingSprite()
    {
        if (_fogBackingSprite != null) return _fogBackingSprite;

		// 런타임 생성 Texture2D 스프라이트가 이 빌드에서 렌더링 안 되는 문제를 피하려고 엔진 내장
		// whiteTexture를 재사용한다.
		_fogBackingSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f), Texture2D.whiteTexture.width, 0, SpriteMeshType.FullRect);
		return _fogBackingSprite;
    }

    // 안개 타일 애셋(배경/무늬) 지연 생성 — 최초 1회만 만들고 이후 모든 안개 스폰이 공유한다. 스프라이트
    // 원본 크기가 1 월드유닛과 정확히 안 맞을 수 있어 Tile.transform으로 셀 크기(1x1)에 맞춘다.
    private bool TryPrepareFogTiles()
    {
        if (_fogBackingTile != null && _fogPatternTile != null) return true;

        SpriteCache.GetOrLoad(ref _fogSprite, "obj/fog");
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

    // 안개 덩어리(배경+무늬 Tilemap 2장) 생성 — 여러 Spawn* 메서드 공용. parentGroup이 이미
    // floorOffset만큼 옮겨져 있어 원본 월드 좌표를 그대로 셀 좌표로 쓸 수 있다. 셰도우 캐스터는
    // 여기서 안 붙이고 별도로 만든다.
    private GameObject SpawnFogBlock(List<Vector3Int> cells, Transform parentGroup, string namePrefix)
    {
        if (cells == null || cells.Count == 0) return null;
        if (!TryPrepareFogTiles()) return null;

        if (_fogMaterial == null) _fogMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));

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
        backingRenderer.material = _fogMaterial;
        backingTilemap.SetTiles(cellArray, backingTiles);

        GameObject patternGo = new GameObject("Pattern");
        patternGo.transform.SetParent(root.transform, false);
        var patternTilemap = patternGo.AddComponent<UnityEngine.Tilemaps.Tilemap>();
        var patternRenderer = patternGo.AddComponent<UnityEngine.Tilemaps.TilemapRenderer>();
        patternRenderer.material = _fogMaterial;
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
                // 청크 전체를 가린다(바닥만이 아니라 벽 타일도 포함). 문/통로 타일만 예외 —
                // SpawnFogForGate가 두 방의 안개 상태를 함께 보고 따로 처리한다.
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

    // 게이트가 잇는 두 방 중 하나라도 FogRevealed==false면 그 게이트의 문 타일 전체를 안개로
    // 덮는다. 방이 없는 쪽은 이미 걷힌 것으로 간주한다.
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

    // roomId < 0인 빈 청크는 어떤 Room에도 속하지 않아 SpawnFogForRoom에 걸리지 않고, 유닛이 들어갈
    // 수 없어 해제 트리거도 없으므로 영구 안개로 둔다.
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

    // 0층 최좌측 숨은 스폰 청크(좌표 (0,0)) 전용 상시 안개 — 방 기반 Reveal 트리거 대상이 아니라
    // 스폰만 하고 등록하지 않는다(빈 청크 영구 안개와 동일 패턴). 상/하/좌 1칸 여유는 청크 경계에
    // 정확히 맞추면 카메라 클램프/벽 렌더링 오차로 틈이 보일 수 있어서다(우측은 확장 안 함).
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

    // 벽과 "아직 안 걷힌 안개" 마스크를 OR로 합쳐 한 번에 윤곽선을 뽑는다 — 따로 캐스팅하면 문처럼
    // 벽 없는 구간에서 경계가 끊어진다. 안개는 방이 걷힐 때마다 바뀌므로 벽과 달리 매번 층 전체를
    // 재계산한다(방 하나 걷힐 때만 발생해 성능 문제 없음).
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

        // 0층은 Room.FogRevealed가 항상 true라 방 기반 판정만으로는 숨은 청크가 안 걷힌 것으로 안
        // 잡힌다 — 여기서 강제로 덮어쓴다.
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

    // 새 ShadowCaster2D는 Update()가 한 번 돌아야 셰도우 그룹에 등록되므로(Awake 시점엔 미등록), 기존
    // 캐스터를 먼저 지우면 빛이 새는 프레임이 생긴다 — 새 캐스터를 먼저 만들고 2프레임 뒤에 기존 걸
    // 지운다. debug 전용(GameSession.DebugConvertFloorTileToWall)도 이 경로로 벽 타일 추가 후 다시 굽는다.
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

    // 안개 해제 — UnitFunction.SyncRoomAffiliation이 플레이어 진영 몬스터의 방 최초 입장을 감지하면
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

        // 횃불 지연 스폰 — 이 방을 위해 대기 중이던 횃불이 있으면 지금 배치한다.
        SpawnPendingTorchesForRoom(room);

        TryRevealAdjacentGates(room);

        // 이 방(과 인접 게이트)의 FogRevealed가 방금 바뀌었으니 벽+안개 통합 셰도우도 다시 굽는다.
        RebuildFloorFogShadowCasters(room.Floor);
    }

    // '방 최초 진입' 트리거(RevealRoomFog)는 이동 제약 때문에 드물게만 발생해 안전장치로만 남겨두고,
    // 더 확실한 '점령'(OffenseProcessor가 RoomFaction을 Player로 확정하는 순간)을 주 트리거로 쓴다.
    // 점령된 방과 Gate로 직접 연결된 인접 방까지만 안개를 걷는다.
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

    // room이 걷혔을 때 맞닿은 게이트 중 반대쪽 방도 이미 걷혀 있으면(양쪽 다 FogRevealed) 그
    // 게이트 안개도 같이 걷는다.
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

    // SceneTransitionFade.FadeAsync와 동일한 UniTask 알파 페이드 관례. Tilemap.color(전체 셀
    // 곱연산 틴트)만 1→0으로 보간하면 baked 알파에 자동으로 곱해져 시작 알파를 따로 기억할 필요가
    // 없다.
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
    // Assets/Resources/Prefabs/VFX/Torch.prefab을 Instantiate — 0층도 다른 층과 동일한 청크 단위 배치를
    // 받는다. 청크당 "게이트로 뚫리지 않은 완전히 막힌 벽 1면"을 랜덤으로 골라 그 앞 바닥 칸에 놓으며
    // (TryFindTorchTilePos, 벽/게이트 판정은 CreateMap에 위임), 그런 벽이 없는 청크는 배치하지 않는다.
    // SpriteRenderer.sortingOrder=51 — 일반 오브젝트(≤50)보다 위, 위협 타일/안개 등 특수 오버레이
    // (998+)보다는 아래.
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
                    // 0층 숨은 스폰 청크 — 상시 안개로 덮여 안 보이는 데다, 횃불 빛이 안개 경계
                    // 너머로 새는 것도 막는다.
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

    // 청크 4면 중 게이트로 뚫리지 않은 완전히 막힌 벽만 후보로 모아 랜덤 선택 후 그 앞 바닥 칸을
    // 반환한다. 벽/게이트 판정은 CreateMap에 위임한다.
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

        // 방향별 스프라이트/위치 보정은 TorchVisual.cs가 담당 — 보정은 루트가 아닌 자식 "Visual"의
        // 로컬 좌표에만 적용된다(루트를 옮기면 Light2D도 같이 밀려 엉뚱한 곳을 비춘다).
        TorchVisual.ApplyTorchVisual(go, side);
        return go;
    }
}
