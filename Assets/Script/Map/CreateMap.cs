using System.Collections.Generic;
using UnityEngine;

public class CreateMap : MonoBehaviour
{
    public Map map;
    public Session session;

    private int nextRoomId = 0;

    // true면 고정 시드, false면 매번 랜덤
    public bool useFixedSeed = true;
    public int seed = 12345;

    private int spawnRoomId = -1;
    private Dictionary<int, HashSet<int>> roomAdjacency = new Dictionary<int, HashSet<int>>();
    private HashSet<(int, int)> connectedPairs = new HashSet<(int, int)>();

    void Awake() => GenerateMap();

    public void GenerateMap()
    {
        if (useFixedSeed) UnityEngine.Random.InitState(seed);
        else UnityEngine.Random.InitState(System.Environment.TickCount);

        nextRoomId = 0;
        spawnRoomId = -1;
        roomAdjacency.Clear();
        connectedPairs.Clear();

        //①16×16 chunks 배열 초기화
        InitMap();
        //②세션 좌표 범위 설정
        DefineSession();
        //③세션 1~4에 방 배치 (0번 스폰은 제외)
        PlaceRooms();
        //③ 부가기능: 스폰 영역(세션 0) 청크에 roomId 할당
        AssignSpawn();
        //③.5 임시: 프리팹 미확정 → 청크 내 가장자리 타일은 Wall, 나머지는 Floor
        AssignTileNames();
        //③.6 같은 방 내부 청크 간 벽 개방
        OpenInternalWalls();
        // ④ 방 인접 관계 그래프 생성
        BuildGraph();
        // ⑤ 방 연결 (스폰: 모든 인접 방 무조건 연결 / 나머지: MST)
        ConnectRooms();
        // ⑥ 추가 루프: 막힌 벽 중 ~15%를 랜덤 개방하여 갈림길/우회로 생성
        AddLoops();
    }

    // ① 16×16 청크 배열 초기화 (각 청크 8×8 타일)
    void InitMap()
    {
        map.session = new Chunks[16, 16];

        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                Chunks c = new Chunks();
                c.chunk = new Tile[8, 8];
                c.landform = 0;
                c.roomId = -1;
                c.roomName = string.Empty;
                map.session[x, y] = c;
            }
        }

        session.session = new int[5, 2, 2];
        Debug.Log("CreateMap: Map initialized with 16x16 chunks, each chunk has 8x8 tiles.");
    }

    // ② 세션 좌표 범위 설정 — [s,0,0]=startX, [s,0,1]=endX, [s,1,0]=startY, [s,1,1]=endY
    void DefineSession()
    {
        if (session.session == null || session.session.GetLength(0) < 5)
            session.session = new int[5, 2, 2];

        // S0: 스폰 중앙 (7~8, 7~8)
        session.session[0, 0, 0] = 7;  session.session[0, 0, 1] = 8;
        session.session[0, 1, 0] = 7;  session.session[0, 1, 1] = 8;

        // S1: (0~8, 9~15)
        session.session[1, 0, 0] = 0;  session.session[1, 0, 1] = 8;
        session.session[1, 1, 0] = 9;  session.session[1, 1, 1] = 15;

        // S2: (9~15, 7~15)
        session.session[2, 0, 0] = 9;  session.session[2, 0, 1] = 15;
        session.session[2, 1, 0] = 7;  session.session[2, 1, 1] = 15;

        // S3: (7~15, 0~6)
        session.session[3, 0, 0] = 7;  session.session[3, 0, 1] = 15;
        session.session[3, 1, 0] = 0;  session.session[3, 1, 1] = 6;

        // S4: (0~6, 0~8)
        session.session[4, 0, 0] = 0;  session.session[4, 0, 1] = 6;
        session.session[4, 1, 0] = 0;  session.session[4, 1, 1] = 8;

        Debug.Log("CreateMap: Sessions defined.");
    }

    // ③.1 비정형 방 모양 템플릿 (L/T/ㄷ/S·Z/직선, 각 회전 포함)
    static readonly List<(int dx, int dy)[]> shapeTemplates = new List<(int dx, int dy)[]>
    {
        // L자 (4회전)
        new (int,int)[] { (0,0),(0,1),(0,2),(1,2) },
        new (int,int)[] { (0,0),(1,0),(2,0),(0,1) },
        new (int,int)[] { (0,0),(1,0),(1,1),(1,2) },
        new (int,int)[] { (2,0),(0,1),(1,1),(2,1) },

        // T자 (4회전)
        new (int,int)[] { (0,0),(1,0),(2,0),(1,1) },
        new (int,int)[] { (0,0),(0,1),(1,1),(0,2) },
        new (int,int)[] { (1,0),(0,1),(1,1),(2,1) },
        new (int,int)[] { (1,0),(0,1),(1,1),(1,2) },

        // ㄷ자 (4회전, 5청크)
        new (int,int)[] { (0,0),(1,0),(2,0),(0,1),(2,1) },
        new (int,int)[] { (0,0),(1,0),(1,1),(0,2),(1,2) },
        new (int,int)[] { (0,0),(2,0),(0,1),(1,1),(2,1) },
        new (int,int)[] { (0,0),(1,0),(0,1),(0,2),(1,2) },

        // S/Z자 (각 2회전)
        new (int,int)[] { (1,0),(0,1),(1,1),(0,2) },
        new (int,int)[] { (0,0),(1,0),(1,1),(2,1) },
        new (int,int)[] { (0,0),(0,1),(1,1),(1,2) },
        new (int,int)[] { (1,0),(2,0),(0,1),(1,1) },

        // 직선 1×3
        new (int,int)[] { (0,0),(1,0),(2,0) },
        new (int,int)[] { (0,0),(0,1),(0,2) },

        // 직선 2×1
        new (int,int)[] { (0,0),(1,0) },
        new (int,int)[] { (0,0),(0,1) },
    };

    // ③ 세션 1~4 방 배치: 큰 사각형 → 비정형 → 1×1 채우기
    void PlaceRooms()
    {
        for (int s = 1; s <= 4; s++)
        {
            int startX = session.session[s, 0, 0];
            int endX   = session.session[s, 0, 1];
            int startY = session.session[s, 1, 0];
            int endY   = session.session[s, 1, 1];

            int width  = endX - startX + 1;
            int height = endY - startY + 1;

            bool[,] occupied = new bool[width, height];

            // 1단계: 큰 사각형 우선
            PlaceRoomsOfSize(startX, startY, width, height, 3, 3, occupied);
            PlaceRoomsOfSize(startX, startY, width, height, 3, 2, occupied);
            PlaceRoomsOfSize(startX, startY, width, height, 2, 3, occupied);
            PlaceRoomsOfSize(startX, startY, width, height, 2, 2, occupied);

            // 2단계: 비정형 모양
            PlaceRoomsWithShapes(startX, startY, width, height, occupied);

            // 3단계: 남은 빈 칸 1×1
            FillRemainingWithSingle(startX, startY, width, height, occupied);
        }

        Debug.Log($"CreateMap: Rooms placed. Total rooms: {nextRoomId}");
    }

    // ③.2 rw×rh 사각형 방을 랜덤 위치에 겹치지 않게 배치
    void PlaceRoomsOfSize(int startX, int startY, int width, int height, int rw, int rh, bool[,] occupied)
    {
        var positions = new List<(int lx, int ly)>();
        for (int lx = 0; lx <= width - rw; lx++)
            for (int ly = 0; ly <= height - rh; ly++)
                positions.Add((lx, ly));

        ShuffleList(positions);

        foreach (var (lx, ly) in positions)
        {
            if (CanPlaceRect(lx, ly, rw, rh, width, height, occupied))
            {
                int id = nextRoomId++;
                string name = $"room{id}";

                for (int dx = 0; dx < rw; dx++)
                {
                    for (int dy = 0; dy < rh; dy++)
                    {
                        occupied[lx + dx, ly + dy] = true;
                        int mx = startX + lx + dx;
                        int my = startY + ly + dy;

                        Chunks c = map.session[mx, my];
                        c.roomId = id;
                        c.roomName = name;
                        map.session[mx, my] = c;
                    }
                }
                break;
            }
        }
    }

    // ③.3 사각형 배치 가능 여부 확인
    bool CanPlaceRect(int lx, int ly, int rw, int rh, int width, int height, bool[,] occupied)
    {
        for (int dx = 0; dx < rw; dx++)
            for (int dy = 0; dy < rh; dy++)
            {
                int cx = lx + dx, cy = ly + dy;
                if (cx >= width || cy >= height) return false;
                if (occupied[cx, cy]) return false;
            }
        return true;
    }
     
    // ③.4 남은 공간에 비정형 템플릿 배치
    void PlaceRoomsWithShapes(int startX, int startY, int width, int height, bool[,] occupied)
    {
        var templateIndices = new List<int>();
        for (int i = 0; i < shapeTemplates.Count; i++)
            templateIndices.Add(i);

        ShuffleList(templateIndices);

        foreach (int ti in templateIndices)
        {
            var shape = shapeTemplates[ti];

            int maxDx = 0, maxDy = 0;
            foreach (var (dx, dy) in shape)
            {
                if (dx > maxDx) maxDx = dx;
                if (dy > maxDy) maxDy = dy;
            }
            int shapeW = maxDx + 1;
            int shapeH = maxDy + 1;

            var positions = new List<(int lx, int ly)>();
            for (int lx = 0; lx <= width - shapeW; lx++)
                for (int ly = 0; ly <= height - shapeH; ly++)
                    positions.Add((lx, ly));

            ShuffleList(positions);

            foreach (var (lx, ly) in positions)
            {
                if (CanPlaceShape(lx, ly, shape, width, height, occupied))
                {
                    int id = nextRoomId++;
                    string name = $"room{id}";

                    foreach (var (dx, dy) in shape)
                    {
                        occupied[lx + dx, ly + dy] = true;
                        int mx = startX + lx + dx;
                        int my = startY + ly + dy;

                        Chunks c = map.session[mx, my];
                        c.roomId = id;
                        c.roomName = name;
                        map.session[mx, my] = c;
                    }
                    break;
                }
            }
        }
    }

    // ③.5 모양 배치 가능 여부 확인
    bool CanPlaceShape(int lx, int ly, (int dx, int dy)[] shape, int width, int height, bool[,] occupied)
    {
        foreach (var (dx, dy) in shape)
        {
            int cx = lx + dx, cy = ly + dy;
            if (cx < 0 || cx >= width || cy < 0 || cy >= height) return false;
            if (occupied[cx, cy]) return false;
        }
        return true;
    }

    // ③.6 Fisher-Yates 셔플
    void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    // ③.7 남은 빈 청크 → 1×1 방 (10% 확률로 벽 전용)
    void FillRemainingWithSingle(int startX, int startY, int width, int height, bool[,] occupied)
    {
        for (int lx = 0; lx < width; lx++)
        {
            for (int ly = 0; ly < height; ly++)
            {
                if (!occupied[lx, ly])
                {
                    occupied[lx, ly] = true;

                    if (UnityEngine.Random.value < 0.9f)
                        continue;

                    int id = nextRoomId++;
                    string name = $"room{id}";
                    int mx = startX + lx;
                    int my = startY + ly;

                    Chunks c = map.session[mx, my];
                    c.roomId = id;
                    c.roomName = name;
                    map.session[mx, my] = c;
                }
            }
        }
    }

    // ③.8 가장자리=Wall, 내부=Floor / roomId==-1이면 전체 Wall
    void AssignTileNames()
    {
        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                Chunks c = map.session[x, y];

                if (c.chunk == null)
                    c.chunk = new Tile[8, 8];

                if (c.roomId == -1)
                {
                    for (int tx = 0; tx < 8; tx++)
                        for (int ty = 0; ty < 8; ty++)
                        {
                            Tile t = c.chunk[tx, ty];
                            t.name = "Wall";
                            c.chunk[tx, ty] = t;
                        }
                    map.session[x, y] = c;
                    continue;
                }

                for (int tx = 0; tx < 8; tx++)
                {
                    for (int ty = 0; ty < 8; ty++)
                    {
                        Tile t = c.chunk[tx, ty];
                        t.name = (tx == 0 || tx == 7 || ty == 0 || ty == 7)
                            ? "Wall"
                            : "Floor";
                        c.chunk[tx, ty] = t;
                    }
                }

                map.session[x, y] = c;
            }
        }

        Debug.Log("CreateMap: Tile names assigned (Wall/Floor).");
    }

    // ③.9 같은 roomId 청크 간 내부 벽 허물기 (코너는 대각선 확인)
    void OpenInternalWalls()
    {
        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                int id = map.session[x, y].roomId;
                if (id == -1) continue;

                if (x + 1 < 16 && map.session[x + 1, y].roomId == id)
                    RemoveHorizontalWall(x, y, id);

                if (y + 1 < 16 && map.session[x, y + 1].roomId == id)
                    RemoveVerticalWall(x, y, id);
            }
        }

        Debug.Log("CreateMap: Internal walls removed for multi-chunk rooms.");
    }

    // ③.10 청크 좌표의 roomId 반환 (범위 밖이면 -1)
    int GetRoomId(int cx, int cy)
    {
        if (cx < 0 || cx >= 16 || cy < 0 || cy >= 16) return -1;
        return map.session[cx, cy].roomId;
    }

    // ③.11 월드 경계(0 또는 127) 여부 확인
    bool IsWorldBorder(int worldX, int worldY)
    {
        const int worldMax = 16 * 8 - 1;
        return worldX == 0 || worldX == worldMax || worldY == 0 || worldY == worldMax;
    }

    // ③.12 수평 경계 벽 허물기: chunk(x,y) tx=7 ↔ chunk(x+1,y) tx=0
    void RemoveHorizontalWall(int x, int y, int roomId)
    {
        Chunks cA = map.session[x, y];
        Chunks cB = map.session[x + 1, y];

        for (int ty = 0; ty <= 7; ty++)
        {
            int worldAX = x * 8 + 7;
            int worldAY = y * 8 + ty;
            int worldBX = (x + 1) * 8;
            int worldBY = worldAY;

            if (IsWorldBorder(worldAX, worldAY) || IsWorldBorder(worldBX, worldBY)) continue;

            if (ty == 0)
            {
                if (GetRoomId(x, y - 1) != roomId || GetRoomId(x + 1, y - 1) != roomId)
                    continue;
            }
            else if (ty == 7)
            {
                if (GetRoomId(x, y + 1) != roomId || GetRoomId(x + 1, y + 1) != roomId)
                    continue;
            }

            Tile tA = cA.chunk[7, ty];
            tA.name = "Floor";
            cA.chunk[7, ty] = tA;

            Tile tB = cB.chunk[0, ty];
            tB.name = "Floor";
            cB.chunk[0, ty] = tB;
        }

        map.session[x, y] = cA;
        map.session[x + 1, y] = cB;
    }

    // ③.13 수직 경계 벽 허물기: chunk(x,y) ty=7 ↔ chunk(x,y+1) ty=0
    void RemoveVerticalWall(int x, int y, int roomId)
    {
        Chunks cA = map.session[x, y];
        Chunks cB = map.session[x, y + 1];

        for (int tx = 0; tx <= 7; tx++)
        {
            int worldAX = x * 8 + tx;
            int worldAY = y * 8 + 7;
            int worldBX = worldAX;
            int worldBY = (y + 1) * 8;

            if (IsWorldBorder(worldAX, worldAY) || IsWorldBorder(worldBX, worldBY)) continue;

            if (tx == 0)
            {
                if (GetRoomId(x - 1, y) != roomId || GetRoomId(x - 1, y + 1) != roomId)
                    continue;
            }
            else if (tx == 7)
            {
                if (GetRoomId(x + 1, y) != roomId || GetRoomId(x + 1, y + 1) != roomId)
                    continue;
            }

            Tile tA = cA.chunk[tx, 7];
            tA.name = "Floor";
            cA.chunk[tx, 7] = tA;

            Tile tB = cB.chunk[tx, 0];
            tB.name = "Floor";
            cB.chunk[tx, 0] = tB;
        }

        map.session[x, y] = cA;
        map.session[x, y + 1] = cB;
    }

    // ③.14 스폰 영역(세션 0) roomId 할당
    void AssignSpawn()
    {
        spawnRoomId = nextRoomId++;
        string name = $"room{spawnRoomId}";

        int startX = session.session[0, 0, 0];
        int endX   = session.session[0, 0, 1];
        int startY = session.session[0, 1, 0];
        int endY   = session.session[0, 1, 1];

        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                Chunks c = map.session[x, y];
                c.roomId = spawnRoomId;
                c.roomName = name;
                map.session[x, y] = c;
            }
        }

        Debug.Log($"CreateMap: Spawn assigned as {name}.");
    }

    // ④ 방 인접 그래프 생성
    void BuildGraph()
    {
        roomAdjacency.Clear();

        int[] dx = { 1, 0, -1, 0 };
        int[] dy = { 0, 1, 0, -1 };

        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                int idA = map.session[x, y].roomId;
                if (idA == -1) continue;

                if (!roomAdjacency.ContainsKey(idA))
                    roomAdjacency[idA] = new HashSet<int>();

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + dx[d];
                    int ny = y + dy[d];
                    if (nx < 0 || nx >= 16 || ny < 0 || ny >= 16) continue;

                    int idB = map.session[nx, ny].roomId;
                    if (idB == -1 || idB == idA) continue;

                    AddEdge(idA, idB);
                }
            }
        }

        Debug.Log($"CreateMap: Graph built. Nodes: {roomAdjacency.Count}");
    }

    // ④.1 양방향 엣지 등록
    void AddEdge(int a, int b)
    {
        if (!roomAdjacency.ContainsKey(a))
            roomAdjacency[a] = new HashSet<int>();
        if (!roomAdjacency.ContainsKey(b))
            roomAdjacency[b] = new HashSet<int>();

        roomAdjacency[a].Add(b);
        roomAdjacency[b].Add(a);
    }

    // ⑤ 스폰은 인접 방 전부 연결 / 나머지는 랜덤 Prim MST
    void ConnectRooms()
    {
        connectedPairs.Clear();

        // 스폰 → 모든 인접 방 무조건 연결
        if (roomAdjacency.ContainsKey(spawnRoomId))
        {
            foreach (int neighbor in roomAdjacency[spawnRoomId])
            {
                var pair = MakePair(spawnRoomId, neighbor);
                if (connectedPairs.Add(pair))
                    OpenPassage(spawnRoomId, neighbor);
            }
        }

        // 나머지: 랜덤 Prim MST
        var visited = new HashSet<int> { spawnRoomId };
        if (roomAdjacency.ContainsKey(spawnRoomId))
            foreach (int neighbor in roomAdjacency[spawnRoomId])
                visited.Add(neighbor);

        var allNodes = new HashSet<int>(roomAdjacency.Keys);

        while (visited.Count < allNodes.Count)
        {
            var frontier = new List<(int from, int to)>();

            foreach (int v in visited)
            {
                if (!roomAdjacency.ContainsKey(v)) continue;
                foreach (int adj in roomAdjacency[v])
                    if (!visited.Contains(adj))
                        frontier.Add((v, adj));
            }

            if (frontier.Count == 0) break;

            int idx = UnityEngine.Random.Range(0, frontier.Count);
            var (from, to) = frontier[idx];

            visited.Add(to);
            var pair = MakePair(from, to);
            if (connectedPairs.Add(pair))
                OpenPassage(from, to);
        }

        Debug.Log($"CreateMap: Rooms connected. Passages: {connectedPairs.Count}");
    }
    // ⑤.1 페어 정렬 헬퍼 (a < b 순서로 통일)
    (int, int) MakePair(int a, int b) => a < b ? (a, b) : (b, a);

    // ⑤.2 두 방 사이 청크 경계 후보 중 랜덤으로 하나 개방
    void OpenPassage(int roomA, int roomB)
    {
        var horizontal = new List<(int x, int y)>();
        var vertical   = new List<(int x, int y)>();

        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                int id = map.session[x, y].roomId;
                if (id != roomA && id != roomB) continue;

                if (x + 1 < 16)
                {
                    int nId = map.session[x + 1, y].roomId;
                    if ((id == roomA && nId == roomB) || (id == roomB && nId == roomA))
                        horizontal.Add((x, y));
                }

                if (y + 1 < 16)
                {
                    int nId = map.session[x, y + 1].roomId;
                    if ((id == roomA && nId == roomB) || (id == roomB && nId == roomA))
                        vertical.Add((x, y));
                }
            }
        }

        int total = horizontal.Count + vertical.Count;
        if (total == 0) return;

        int pick = UnityEngine.Random.Range(0, total);
        if (pick < horizontal.Count)
        {
            var (hx, hy) = horizontal[pick];
            OpenHorizontalPassage(hx, hy);
        }
        else
        {
            var (vx, vy) = vertical[pick - horizontal.Count];
            OpenVerticalPassage(vx, vy);
        }
    }

    // ⑤.3 수평 통로: tx=7 ↔ tx=0 (ty=3~4)
    void OpenHorizontalPassage(int x, int y)
    {
        Chunks cA = map.session[x, y];
        Chunks cB = map.session[x + 1, y];

        for (int ty = 3; ty <= 4; ty++)
        {
            Tile tA = cA.chunk[7, ty];
            tA.name = "Floor";
            cA.chunk[7, ty] = tA;

            Tile tB = cB.chunk[0, ty];
            tB.name = "Floor";
            cB.chunk[0, ty] = tB;
        }

        map.session[x, y] = cA;
        map.session[x + 1, y] = cB;
    }

    // ⑤.4 수직 통로: ty=7 ↔ ty=0 (tx=3~4)
    void OpenVerticalPassage(int x, int y)
    {
        Chunks cA = map.session[x, y];
        Chunks cB = map.session[x, y + 1];

        for (int tx = 3; tx <= 4; tx++)
        {
            Tile tA = cA.chunk[tx, 7];
            tA.name = "Floor";
            cA.chunk[tx, 7] = tA;

            Tile tB = cB.chunk[tx, 0];
            tB.name = "Floor";
            cB.chunk[tx, 0] = tB;
        }

        map.session[x, y] = cA;
        map.session[x, y + 1] = cB;
    }

    // ⑥ 미연결 벽 중 ~15% 랜덤 개방 (루프/우회로)
    void AddLoops()
    {
        var unopened = new List<(int, int)>();

        foreach (var kvp in roomAdjacency)
        {
            int a = kvp.Key;
            foreach (int b in kvp.Value)
            {
                if (a >= b) continue;
                var pair = MakePair(a, b);
                if (!connectedPairs.Contains(pair))
                    unopened.Add(pair);
            }
        }

        int loopCount = Mathf.Max(1, Mathf.RoundToInt(unopened.Count * 0.05f));

        for (int i = unopened.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var temp = unopened[i];
            unopened[i] = unopened[j];
            unopened[j] = temp;
        }

        for (int i = 0; i < loopCount && i < unopened.Count; i++)
        {
            connectedPairs.Add(unopened[i]);
            OpenPassage(unopened[i].Item1, unopened[i].Item2);
        }

        Debug.Log($"CreateMap: Loops added. Extra passages: {loopCount}");
    }

}
