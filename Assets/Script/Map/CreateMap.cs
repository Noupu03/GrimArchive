using System.Collections.Generic;
using UnityEngine;

public class CreateMap : MonoBehaviour
{
    public Map map;
    public Session session;

    // 방 ID 글로벌 카운터 (0부터 시작, 생성할 때마다 +1)
    private int nextRoomId = 0;

    // 랜덤 시드 제어
    // - useFixedSeed true면 inspector에서 지정한 seed로 Random을 초기화하여 재현 가능하게 만듭니다.
    // - false면 현재 시간 기반 시드로 초기화하여 매번 다른 맵을 생성합니다.
    public bool useFixedSeed = true;
    public int seed = 12345;

    // 스폰 영역의 roomId (AssignSpawn에서 할당)
    private int spawnRoomId = -1;

    // ④ 방 인접 관계 그래프: roomId → 인접한 roomId 집합
    private Dictionary<int, HashSet<int>> roomAdjacency = new Dictionary<int, HashSet<int>>();

    // ⑤⑥ 연결된 방 쌍 기록 (통로 개방 여부 판정용)
    private HashSet<(int, int)> connectedPairs = new HashSet<(int, int)>();

    void Awake()
    {
        // map generation entrypoint
        GenerateMap();
    }

    // 전체 맵 생성 시퀀스 (Awake에서 호출)
    public void GenerateMap()
    {
        // 랜덤 시드 초기화
        if (useFixedSeed)
            UnityEngine.Random.InitState(seed);
        else
            UnityEngine.Random.InitState(System.Environment.TickCount);

        // 상태 초기화: 여러 번 GenerateMap을 호출해도 재현 가능하도록 내부 상태 초기화
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

    // 맵 초기화: map.session 16x16 및 각 Chunks.chunk 8x8 할당
    void InitMap()
    {
        map.session = new Chunks[16, 16];

        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                // structs are value types; prepare a local instance and assign back to the array
                Chunks c = new Chunks();
                c.chunk = new Tile[8, 8];
                c.landform = 0;
                c.roomId = -1;
                c.roomName = string.Empty;

                map.session[x, y] = c;
            }
        }

        // 선택적으로 session 배열도 기본 할당
        session.session = new int[5, 2, 2];

        Debug.Log("CreateMap: Map initialized with 16x16 chunks, each chunk has 8x8 tiles.");
    }

    //②세션 좌표 범위 설정
    // session.session[s][0][0] = startX, [0][1] = endX
    // session.session[s][1][0] = startY, [1][1] = endY
    void DefineSession()
    {
        if (session.session == null || session.session.GetLength(0) < 5)
        {
            session.session = new int[5, 2, 2];
        }

        // 0번째: 스폰 중앙 영역 (x=7~8, y=7~8)
        session.session[0, 0, 0] = 7; // startX
        session.session[0, 0, 1] = 8; // endX
        session.session[0, 1, 0] = 7; // startY
        session.session[0, 1, 1] = 8; // endY

        // 1번째: (x=0~8), (y=9~15)
        session.session[1, 0, 0] = 0;
        session.session[1, 0, 1] = 8;
        session.session[1, 1, 0] = 9;
        session.session[1, 1, 1] = 15;

        // 2번째: (x=9~15), (y=7~15)
        session.session[2, 0, 0] = 9;
        session.session[2, 0, 1] = 15;
        session.session[2, 1, 0] = 7;
        session.session[2, 1, 1] = 15;

        // 3번째: (x=7~15), (y=0~7)
        session.session[3, 0, 0] = 7;
        session.session[3, 0, 1] = 15;
        session.session[3, 1, 0] = 0;
        session.session[3, 1, 1] = 6;

        // 4번째: (x=0~7), (y=0~9)
        session.session[4, 0, 0] = 0;
        session.session[4, 0, 1] = 6;
        session.session[4, 1, 0] = 0;
        session.session[4, 1, 1] = 8;

        Debug.Log("CreateMap: Sessions defined.");
    }

    // ③ 세션 1~4에 방 배치 (0번 스폰은 제외)
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

            // 세션 내 점유 여부 추적 (로컬 좌표 기준)
            bool[,] occupied = new bool[width, height];

            // 큰 방부터 배치: 2x2 → 2x1, 1x2 → 1x1
            PlaceRoomsOfSize(startX, startY, width, height, 2, 2, occupied);
            PlaceRoomsOfSize(startX, startY, width, height, 2, 1, occupied);
            PlaceRoomsOfSize(startX, startY, width, height, 1, 2, occupied);

            // 남은 빈 공간을 1x1 방으로 채우기
            FillRemainingWithSingle(startX, startY, width, height, occupied);
        }

        Debug.Log($"CreateMap: Rooms placed. Total rooms: {nextRoomId}");
    }

    // ③ 부가기능 지정 크기(rw x rh)의 방을 랜덤 위치에 겹치지 않게 배치
    void PlaceRoomsOfSize(int startX, int startY, int width, int height, int rw, int rh, bool[,] occupied)
    {
        // 배치 가능한 모든 로컬 좌표 수집
        var positions = new List<(int lx, int ly)>();
        for (int lx = 0; lx <= width - rw; lx++)
        {
            for (int ly = 0; ly <= height - rh; ly++)
            {
                positions.Add((lx, ly));
            }
        }

        // Fisher-Yates 셔플로 랜덤 순서 배치
        for (int i = positions.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var temp = positions[i];
            positions[i] = positions[j];
            positions[j] = temp;
        }

        foreach (var (lx, ly) in positions)
        {
            if (CanPlace(lx, ly, rw, rh, occupied))
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
            }
        }
    }

    // ③ 부가기능 해당 위치에 rw x rh 크기의 방을 배치할 수 있는지 확인
    bool CanPlace(int lx, int ly, int rw, int rh, bool[,] occupied)
    {
        for (int dx = 0; dx < rw; dx++)
        {
            for (int dy = 0; dy < rh; dy++)
            {
                if (occupied[lx + dx, ly + dy]) return false;
            }
        }
        return true;
    }

    // ③ 부가기능 남은 빈 청크를 각각 1x1 방으로 할당
    void FillRemainingWithSingle(int startX, int startY, int width, int height, bool[,] occupied)
    {
        for (int lx = 0; lx < width; lx++)
        {
            for (int ly = 0; ly < height; ly++)
            {
                if (!occupied[lx, ly])
                {
                    occupied[lx, ly] = true;

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

    // ③.5 임시: 프리팹 미확정 → 청크 내 가장자리 타일은 Wall, 나머지는 Floor
    void AssignTileNames()
    {
        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                Chunks c = map.session[x, y];

                if (c.roomId == -1 || c.chunk == null) continue;

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

    // ③.6 같은 방(같은 roomId) 내부 인접 청크 간 벽 개방
    void OpenInternalWalls()
    {
        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                int id = map.session[x, y].roomId;
                if (id == -1) continue;

                // 오른쪽 청크가 같은 방이면 수평 내부 벽 개방
                if (x + 1 < 16 && map.session[x + 1, y].roomId == id)
                    OpenHorizontalPassage(x, y);

                // 위쪽 청크가 같은 방이면 수직 내부 벽 개방
                if (y + 1 < 16 && map.session[x, y + 1].roomId == id)
                    OpenVerticalPassage(x, y);
            }
        }

        Debug.Log("CreateMap: Internal walls opened for multi-chunk rooms.");
    }

    // ③ 부가기능: 스폰 영역(세션 0) 청크에 roomId 할당
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
    // ④ 방 인접 관계 그래프 생성
    void BuildGraph()
    {
        roomAdjacency.Clear();

        // 4방향 오프셋 (우, 상, 좌, 하)
        int[] dx = { 1, 0, -1, 0 };
        int[] dy = { 0, 1, 0, -1 };

        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                int idA = map.session[x, y].roomId;
                if (idA == -1) continue;

                // 노드 등록 (인접이 없더라도 그래프에 포함)
                if (!roomAdjacency.ContainsKey(idA))
                    roomAdjacency[idA] = new HashSet<int>();

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + dx[d];
                    int ny = y + dy[d];

                    if (nx < 0 || nx >= 16 || ny < 0 || ny >= 16) continue;

                    int idB = map.session[nx, ny].roomId;
                    if (idB == -1 || idB == idA) continue;

                    // 양방향 엣지 추가
                    AddEdge(idA, idB);
                }
            }
        }

        Debug.Log($"CreateMap: Graph built. Nodes: {roomAdjacency.Count}");
    }


    // ④ 부가기능: 양방향 엣지 등록
    void AddEdge(int a, int b)
    {
        if (!roomAdjacency.ContainsKey(a))
            roomAdjacency[a] = new HashSet<int>();
        if (!roomAdjacency.ContainsKey(b))
            roomAdjacency[b] = new HashSet<int>();

        roomAdjacency[a].Add(b);
        roomAdjacency[b].Add(a);
    }

    // ⑤ 방 연결 (스폰: 모든 인접 방 무조건 연결 / 나머지: MST)
    void ConnectRooms()
    {
        connectedPairs.Clear();

        // 1. 스폰은 모든 인접 방과 무조건 연결 (인라인 처리)
        if (roomAdjacency.ContainsKey(spawnRoomId))
        {
            foreach (int neighbor in roomAdjacency[spawnRoomId])
            {
                var pair = MakePair(spawnRoomId, neighbor);
                if (connectedPairs.Add(pair))
                    OpenPassage(spawnRoomId, neighbor);
            }
        }

        // 2. 나머지 방들은 랜덤 Prim MST로 최소 연결 보장
        // 스폰 + 스폰과 연결된 방들을 초기 visited로 설정
        var visited = new HashSet<int> { spawnRoomId };
        if (roomAdjacency.ContainsKey(spawnRoomId))
        {
            foreach (int neighbor in roomAdjacency[spawnRoomId])
                visited.Add(neighbor);
        }

        var allNodes = new HashSet<int>(roomAdjacency.Keys);

        while (visited.Count < allNodes.Count)
        {
            // visited에서 unvisited로 가는 모든 엣지 수집
            var frontier = new List<(int from, int to)>();

            foreach (int v in visited)
            {
                if (!roomAdjacency.ContainsKey(v)) continue;
                foreach (int adj in roomAdjacency[v])
                {
                    if (!visited.Contains(adj))
                        frontier.Add((v, adj));
                }
            }

            if (frontier.Count == 0) break;

            // 랜덤 선택으로 다양한 경로 생성
            int idx = UnityEngine.Random.Range(0, frontier.Count);
            var (from, to) = frontier[idx];

            visited.Add(to);
            var pair = MakePair(from, to);
            if (connectedPairs.Add(pair))
            {
                OpenPassage(from, to);
            }
        }

        Debug.Log($"CreateMap: Rooms connected. Passages: {connectedPairs.Count}");
    }



    // (인라인됨) ConnectByMST logic was merged into ConnectRooms

    // ⑥ 추가 루프: 막힌 벽 중 ~15%를 랜덤 개방하여 갈림길/우회로 생성
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

        int loopCount = Mathf.Max(1, Mathf.RoundToInt(unopened.Count * 0.15f));

        // Fisher-Yates 셔플
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

    // ⑥ 부가 함수 페어 정렬 헬퍼 (a < b 순서로 통일)
    (int, int) MakePair(int a, int b) => a < b ? (a, b) : (b, a);

    // ⑥ 부가 함수 두 방 사이 청크 경계를 찾아 벽 타일을 Floor로 변경 (후보 중 랜덤 선택)
    void OpenPassage(int roomA, int roomB)
    {
        // 후보 경계를 모두 수집
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

        // 후보 중 랜덤 선택
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

    // ⑥ 부가 함수 수평 통로: chunk(x,y)의 오른쪽 벽(tx=7) ↔ chunk(x+1,y)의 왼쪽 벽(tx=0) 개방
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

    // ⑥ 부가 함수 수직 통로: chunk(x,y)의 윗벽(ty=7) ↔ chunk(x,y+1)의 아랫벽(ty=0) 개방
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


}
//맵 초기화시 차라리 NULL을 벽 처리로 하는 것이 상당히 괜찮을 것 같다.