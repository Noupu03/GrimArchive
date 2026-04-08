// ============================================================================
// CreateMap.RoomPlacement.cs — 방 배치 · 형태 결정
// ----------------------------------------------------------------------------
// 역할: 보스방 배치(PlaceBossRoom), 일반/서브 방 배치(PlaceRooms),
//       시작방 지정(AssignStartRoom), 방 형태 템플릿(Shape Templates),
//       인접 단일 청크 병합(Merge) 로직.
// 단계: GenerateMap Phase 1 — 그리드 위에 방들을 물리적으로 배치
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public partial class CreateMap
{
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

    // ── 보스방 형태 템플릿 (bossRoomFormat 파싱 결과) ──
    // ㄷ자 7청크: 3×3에서 한 변 중앙 열 2개 제거 (4회전)
    static readonly List<(int dx, int dy)[]> bossShapeDigeut7 = new List<(int dx, int dy)[]>
    {
        // ㄷ 열림→우: X X X / X · · / X X X
        new (int,int)[] { (0,0),(1,0),(2,0),(0,1),(0,2),(1,2),(2,2) },
        // ㄷ 열림→상: X · X / X · X / X X X
        new (int,int)[] { (0,0),(2,0),(0,1),(2,1),(0,2),(1,2),(2,2) },
        // ㄷ 열림→좌: X X X / · · X / X X X
        new (int,int)[] { (0,0),(1,0),(2,0),(2,1),(0,2),(1,2),(2,2) },
        // ㄷ 열림→하: X X X / X · X / X · X
        new (int,int)[] { (0,0),(1,0),(2,0),(0,1),(2,1),(0,2),(2,2) },
    };

    // ③.0 보스방 전용 배치 (PlaceRooms 이전에 호출)
    private bool[,] bossOccupied; // PlaceRooms에서 공유할 점유 맵

    void PlaceBossRoom(ref Floor floor)
    {
        int w = floor.config.width;
        int h = floor.config.height;
        string fmt = floor.config.bossRoomFormat;

        bossOccupied = new bool[w, h];

        if (string.IsNullOrEmpty(fmt)) return;

        // bossRoomFormat 파싱 → 청크 오프셋 목록 생성
        var shapes = ParseBossFormat(fmt);
        if (shapes == null || shapes.Count == 0)
        {
            Debug.LogWarning($"CreateMap: 알 수 없는 bossRoomFormat '{fmt}'");
            return;
        }

        // 시작방(0, h/2)에서 가장 먼 코너에 배치 시도
        var corners = new List<(int x, int y)>
        {
            (w - 1, h - 1), // 우상
            (w - 1, 0),     // 우하
            (0, h - 1),     // 좌상
            (0, 0),         // 좌하
        };

        // 시작방에서 먼 순서로 정렬
        int sx = 0, sy = h / 2;
        corners.Sort((a, b) =>
        {
            float da = Mathf.Abs(a.x - sx) + Mathf.Abs(a.y - sy);
            float db = Mathf.Abs(b.x - sx) + Mathf.Abs(b.y - sy);
            return db.CompareTo(da);
        });

        // 각 코너 주변에서 모든 형태 시도
        bool placed = false;
        foreach (var (cornerX, cornerY) in corners)
        {
            if (placed) break;

            // F: 시작방 방향에 따라 입구 방향 우선 정렬 (셔플 전에 입구 방향 형태 우선)
            var prioritized = PrioritizeBossShapesByEntrance(shapes, sx, sy, cornerX, cornerY);

            foreach (var shape in prioritized)
            {
                // 모양의 바운딩 박스 계산
                int maxDx = 0, maxDy = 0;
                foreach (var (dx, dy) in shape)
                {
                    if (dx > maxDx) maxDx = dx;
                    if (dy > maxDy) maxDy = dy;
                }
                int shapeW = maxDx + 1;
                int shapeH = maxDy + 1;

                // 코너 기준 배치 오프셋: 모양이 코너 쪽으로 붙도록
                int baseX = cornerX >= w / 2 ? cornerX - shapeW + 1 : cornerX;
                int baseY = cornerY >= h / 2 ? cornerY - shapeH + 1 : cornerY;

                // 범위 클램프
                if (baseX < 0) baseX = 0;
                if (baseY < 0) baseY = 0;
                if (baseX + shapeW > w) baseX = w - shapeW;
                if (baseY + shapeH > h) baseY = h - shapeH;

                if (baseX < 0 || baseY < 0) continue; // 플로어보다 큰 모양

                // 배치 가능 여부 확인 (시작방 위치와 겹치지 않는지)
                bool canPlace = true;
                foreach (var (dx, dy) in shape)
                {
                    int px = baseX + dx;
                    int py = baseY + dy;
                    if (px == sx && py == sy) { canPlace = false; break; }
                }
                if (!canPlace) continue;

                // 배치 실행
                int id = RoomIdGenerator.GetNextId();
                string name = RoomIdGenerator.FormatName(id);

                foreach (var (dx, dy) in shape)
                {
                    int px = baseX + dx;
                    int py = baseY + dy;

                    bossOccupied[px, py] = true;

                    Chunks c = floor.chunks[px, py];
                    c.roomId = id;
                    c.roomName = name;
                    c.roomRole = RoomRole.BossRoom;
                    floor.chunks[px, py] = c;
                }

                placed = true;
                Debug.Log($"CreateMap: Floor {(int)floor.config.floorId} BossRoom '{fmt}' placed as {name} at base[{baseX},{baseY}] ({shape.Length} chunks)");
                break;
            }
        }

        if (!placed)
            Debug.LogWarning($"CreateMap: Floor {(int)floor.config.floorId} BossRoom '{fmt}' 배치 실패");
    }

    // bossRoomFormat 문자열 → 청크 오프셋 목록
    List<(int dx, int dy)[]> ParseBossFormat(string fmt)
    {
        // "NxM" 형식: N×M 사각형
        if (fmt.Contains("x"))
        {
            var parts = fmt.Split('x');
            if (parts.Length == 2 && int.TryParse(parts[0], out int bw) && int.TryParse(parts[1], out int bh))
            {
                var shape = new (int, int)[bw * bh];
                int idx = 0;
                for (int dx = 0; dx < bw; dx++)
                    for (int dy = 0; dy < bh; dy++)
                        shape[idx++] = (dx, dy);
                return new List<(int dx, int dy)[]> { shape };
            }
        }

        // "ㄷN" 형식: ㄷ자 N청크
        if (fmt.StartsWith("ㄷ") && int.TryParse(fmt.Substring(1), out int chunkCount))
        {
            if (chunkCount == 7)
                return bossShapeDigeut7;
        }

        return null;
    }

    // F: 보스방 형태를 시작방 방향 기준 입구 우선으로 정렬
    List<(int dx, int dy)[]> PrioritizeBossShapesByEntrance(List<(int dx, int dy)[]> shapes, int startX, int startY, int cornerX, int cornerY)
    {
        // 시작방→코너 방향 벡터
        int dirX = startX - cornerX; // 코너에서 시작방 방향
        int dirY = startY - cornerY;

        // 각 형태에 대해 "입구 방향 점수" 계산: 시작방 방향에 열린 면이 있는 형태가 우선
        var scored = new List<(int score, int originalIdx, (int dx, int dy)[] shape)>();
        for (int i = 0; i < shapes.Count; i++)
        {
            var shape = shapes[i];
            int score = ScoreBossEntrance(shape, dirX, dirY);
            scored.Add((score, i, shape));
        }

        // 같은 점수 내에서는 랜덤 순서 (다양성 확보)
        ShuffleList(scored);
        scored.Sort((a, b) => b.score.CompareTo(a.score)); // 높은 점수 우선

        var result = new List<(int dx, int dy)[]>();
        foreach (var entry in scored)
            result.Add(entry.shape);
        return result;
    }

    // 보스방 형태의 입구 방향 점수
    int ScoreBossEntrance((int dx, int dy)[] shape, int dirX, int dirY)
    {
        var cells = new HashSet<(int, int)>();
        int maxDx = 0, maxDy = 0;
        foreach (var (dx, dy) in shape)
        {
            cells.Add((dx, dy));
            if (dx > maxDx) maxDx = dx;
            if (dy > maxDy) maxDy = dy;
        }

        int score = 0;

        if (dirX > 0)
        {
            for (int dy = 0; dy <= maxDy; dy++)
                if (cells.Contains((0, dy)))
                    score++;
        }
        if (dirX < 0)
        {
            for (int dy = 0; dy <= maxDy; dy++)
                if (cells.Contains((maxDx, dy)))
                    score++;
        }
        if (dirY > 0)
        {
            for (int dx = 0; dx <= maxDx; dx++)
                if (cells.Contains((dx, 0)))
                    score++;
        }
        if (dirY < 0)
        {
            for (int dx = 0; dx <= maxDx; dx++)
                if (cells.Contains((dx, maxDy)))
                    score++;
        }

        return score;
    }

    // ③ 방 배치: 큰 사각형 → 비정형 → 1×1 채우기 (보스방 영역 제외)
    void PlaceRooms(ref Floor floor)
    {
        int width = floor.config.width;
        int height = floor.config.height;
        int maxChunks = floor.config.maxNormalRoomChunks;

        // 보스방 점유 영역을 초기 occupied로 복사
        bool[,] occupied = new bool[width, height];
        if (bossOccupied != null)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    occupied[x, y] = bossOccupied[x, y];
        }

        // 1단계: 큰 사각형 우선 (청크 수가 maxNormalRoomChunks 이하인 것만)
        if (maxChunks >= 9)
            PlaceRoomsOfSize(ref floor, 0, 0, width, height, 3, 3, occupied);
        if (maxChunks >= 6)
        {
            PlaceRoomsOfSize(ref floor, 0, 0, width, height, 3, 2, occupied);
            PlaceRoomsOfSize(ref floor, 0, 0, width, height, 2, 3, occupied);
        }
        if (maxChunks >= 4)
            PlaceRoomsOfSize(ref floor, 0, 0, width, height, 2, 2, occupied);

        // 2단계: 비정형 모양 (청크 수가 maxNormalRoomChunks 이하인 것만)
        PlaceRoomsWithShapes(ref floor, 0, 0, width, height, occupied);

        // 3단계: 남은 빈 칸 1×1
        FillRemainingWithSingle(ref floor, 0, 0, width, height, occupied);

        Debug.Log($"CreateMap: Floor {(int)floor.config.floorId} rooms placed (maxChunks={maxChunks}).");
    }

    // ③.2 rw×rh 사각형 방을 랜덤 위치에 겹치지 않게 여러 개 배치
    void PlaceRoomsOfSize(ref Floor floor, int startX, int startY, int width, int height, int rw, int rh, bool[,] occupied)
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
                int id = RoomIdGenerator.GetNextId();
                string name = RoomIdGenerator.FormatName(id);

                for (int dx = 0; dx < rw; dx++)
                {
                    for (int dy = 0; dy < rh; dy++)
                    {
                        occupied[lx + dx, ly + dy] = true;
                        int mx = startX + lx + dx;
                        int my = startY + ly + dy;

                        Chunks c = floor.chunks[mx, my];
                        c.roomId = id;
                        c.roomName = name;
                        floor.chunks[mx, my] = c;
                    }
                }
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

    // ③.4 남은 공간에 비정형 템플릿 배치 (maxNormalRoomChunks 제한 적용)
    void PlaceRoomsWithShapes(ref Floor floor, int startX, int startY, int width, int height, bool[,] occupied)
    {
        int maxChunks = floor.config.maxNormalRoomChunks;

        var templateIndices = new List<int>();
        for (int i = 0; i < shapeTemplates.Count; i++)
            templateIndices.Add(i);

        ShuffleList(templateIndices);

        foreach (int ti in templateIndices)
        {
            var shape = shapeTemplates[ti];

            // 청크 수가 maxNormalRoomChunks를 초과하면 스킵
            if (shape.Length > maxChunks) continue;

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
                    int id = RoomIdGenerator.GetNextId();
                    string name = RoomIdGenerator.FormatName(id);

                    foreach (var (dx, dy) in shape)
                    {
                        occupied[lx + dx, ly + dy] = true;
                        int mx = startX + lx + dx;
                        int my = startY + ly + dy;

                        Chunks c = floor.chunks[mx, my];
                        c.roomId = id;
                        c.roomName = name;
                        floor.chunks[mx, my] = c;
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

    // ③.7 남은 빈 청크 → 1×1 방 배치 (초과분은 PruneExcessRooms에서 제거)
    void FillRemainingWithSingle(ref Floor floor, int startX, int startY, int width, int height, bool[,] occupied)
    {
        for (int lx = 0; lx < width; lx++)
        {
            for (int ly = 0; ly < height; ly++)
            {
                if (!occupied[lx, ly])
                {
                    occupied[lx, ly] = true;

                    int id = RoomIdGenerator.GetNextId();
                    string name = RoomIdGenerator.FormatName(id);
                    int mx = startX + lx;
                    int my = startY + ly;

                    Chunks c = floor.chunks[mx, my];
                    c.roomId = id;
                    c.roomName = name;
                    floor.chunks[mx, my] = c;
                }
            }
        }

        // 인접 1×1 방 병합: 같은 크기(1청크)인 인접 방을 하나로 합침
        MergeAdjacentSingleRooms(ref floor, startX, startY, width, height);
    }

    // ③.8a 인접 1×1 방 병합
    void MergeAdjacentSingleRooms(ref Floor floor, int startX, int startY, int width, int height)
    {
        int maxChunks = floor.config.maxNormalRoomChunks;

        // roomId별 청크 수 계산
        var roomChunkCount = new Dictionary<int, int>();
        for (int lx = 0; lx < width; lx++)
        {
            for (int ly = 0; ly < height; ly++)
            {
                int id = floor.chunks[startX + lx, startY + ly].roomId;
                if (id < 0) continue;
                if (!roomChunkCount.ContainsKey(id))
                    roomChunkCount[id] = 0;
                roomChunkCount[id]++;
            }
        }

        // 1청크짜리 방 위치 수집 (셔플하여 다양한 병합 패턴)
        var singles = new List<(int x, int y)>();
        for (int lx = 0; lx < width; lx++)
        {
            for (int ly = 0; ly < height; ly++)
            {
                int mx = startX + lx;
                int my = startY + ly;
                int id = floor.chunks[mx, my].roomId;
                if (id >= 0 && roomChunkCount.ContainsKey(id) && roomChunkCount[id] == 1)
                    singles.Add((mx, my));
            }
        }

        ShuffleList(singles);
        var merged = new HashSet<int>(); // 이미 병합된 roomId

        int[] ddx = { 1, 0, -1, 0 };
        int[] ddy = { 0, 1, 0, -1 };

        foreach (var (sx, sy) in singles)
        {
            int baseId = floor.chunks[sx, sy].roomId;
            if (baseId < 0 || merged.Contains(baseId)) continue;

            string baseName = floor.chunks[sx, sy].roomName;
            int currentSize = 1;
            int maxMerge = Mathf.Clamp(UnityEngine.Random.Range(2, maxChunks + 1), 2, maxChunks);

            var queue = new Queue<(int x, int y)>();
            queue.Enqueue((sx, sy));
            merged.Add(baseId);

            while (queue.Count > 0 && currentSize < maxMerge)
            {
                var (cx, cy) = queue.Dequeue();

                for (int d = 0; d < 4; d++)
                {
                    if (currentSize >= maxMerge) break;
                    int nx = cx + ddx[d];
                    int ny = cy + ddy[d];
                    if (nx < startX || nx >= startX + width || ny < startY || ny >= startY + height) continue;

                    int nId = floor.chunks[nx, ny].roomId;
                    if (nId < 0 || merged.Contains(nId)) continue;
                    if (!roomChunkCount.ContainsKey(nId) || roomChunkCount[nId] != 1) continue;

                    Chunks nc = floor.chunks[nx, ny];
                    nc.roomId = baseId;
                    nc.roomName = baseName;
                    floor.chunks[nx, ny] = nc;

                    merged.Add(nId);
                    roomChunkCount[baseId]++;
                    currentSize++;
                    queue.Enqueue((nx, ny));
                }
            }
        }
    }

    // ③.8 시작방 할당: 왼쪽 중앙 (디자인 문서: x=0, y=height/2)
    void AssignStartRoom(ref Floor floor)
    {
        int w = floor.config.width;
        int h = floor.config.height;

        spawnRoomId = RoomIdGenerator.GetNextId();
        string name = RoomIdGenerator.FormatName(spawnRoomId);

        int sx = 0;
        int sy = h / 2;

        int oldRoomId = floor.chunks[sx, sy].roomId;
        if (oldRoomId >= 0)
        {
            int orphanId = RoomIdGenerator.GetNextId();
            string orphanName = RoomIdGenerator.FormatName(orphanId);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (x == sx && y == sy) continue;
                    if (floor.chunks[x, y].roomId == oldRoomId)
                    {
                        Chunks oc = floor.chunks[x, y];
                        oc.roomId = orphanId;
                        oc.roomName = orphanName;
                        floor.chunks[x, y] = oc;
                    }
                }
        }

        Chunks c = floor.chunks[sx, sy];
        c.roomId = spawnRoomId;
        c.roomName = name;
        c.roomRole = RoomRole.StartRoom;
        floor.chunks[sx, sy] = c;

        Debug.Log($"CreateMap: Floor {(int)floor.config.floorId} start room assigned as {name} at [{sx},{sy}].");
    }
}
