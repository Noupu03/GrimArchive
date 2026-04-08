using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class DebugSeed14686
{
    [MenuItem("Debug/Seed 14686 Detailed Analysis")]
    static void RunAnalysis()
    {
        var go = new GameObject("TempCreateMap");
        var cm = go.AddComponent<CreateMap>();
        cm.useFixedSeed = true;
        cm.seed = 14686;
        cm.maxRetryCount = 0; // 재시도 없이 1회만

        cm.GenerateMap();

        // F2 분석
        if (cm.map.floors == null || cm.map.floors.Length < 3)
        {
            Debug.LogError("DebugSeed14686: F2가 존재하지 않습니다.");
            Object.DestroyImmediate(go);
            return;
        }

        ref Floor f2 = ref cm.map.floors[2];
        int w = f2.config.width;
        int h = f2.config.height;

        Debug.Log($"=== F2 Analysis: {w}x{h}, thkMin={f2.config.wallThicknessMin}, thkMax={f2.config.wallThicknessMax} ===");

        // 방 목록
        var roomChunks = new Dictionary<int, List<(int x, int y)>>();
        var roomRoles = new Dictionary<int, RoomRole>();
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Chunks c = f2.chunks[x, y];
                if (c.roomId < 0) continue;
                if (!roomChunks.ContainsKey(c.roomId))
                    roomChunks[c.roomId] = new List<(int, int)>();
                roomChunks[c.roomId].Add((x, y));
                roomRoles[c.roomId] = c.roomRole;
            }
        }

        Debug.Log($"F2 rooms: {roomChunks.Count}");
        foreach (var kvp in roomChunks)
        {
            var role = roomRoles[kvp.Key];
            Debug.Log($"  roomId={kvp.Key}, role={role}, chunks={kvp.Value.Count}: {string.Join(", ", kvp.Value)}");
        }

        // Gate 목록
        if (f2.gates != null)
        {
            Debug.Log($"F2 gates: {f2.gates.Count}");
            foreach (var g in f2.gates)
            {
                Debug.Log($"  Gate: room{g.roomA} <-> room{g.roomB}, chunk[{g.chunkAX},{g.chunkAY}]-[{g.chunkBX},{g.chunkBY}], width={g.width}, horiz={g.isHorizontal}");
            }
        }
        else
        {
            Debug.Log("F2 gates: null");
        }

        // 각 Gate 주변 타일 상태 확인
        if (f2.gates != null)
        {
            foreach (var g in f2.gates)
            {
                AnalyzeGateTiles(ref f2, g);
            }
        }

        // FloodFill 시작점 확인
        int startWx = -1, startWy = -1;
        for (int x = 0; x < w && startWx < 0; x++)
            for (int y = 0; y < h && startWx < 0; y++)
                if (f2.chunks[x, y].roomRole == RoomRole.StartRoom)
                {
                    for (int tx = 0; tx < 8; tx++)
                        for (int ty = 0; ty < 8; ty++)
                            if (f2.chunks[x, y].chunk != null && f2.chunks[x, y].chunk[tx, ty].name != "Wall" && startWx < 0)
                            {
                                startWx = x * 8 + tx;
                                startWy = y * 8 + ty;
                            }
                }

        Debug.Log($"F2 FloodFill start: ({startWx}, {startWy})");

        // 시작방 청크의 타일 맵 출력
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (f2.chunks[x, y].roomRole == RoomRole.StartRoom)
                    PrintChunkTiles(ref f2, x, y, "StartRoom");

        // 시작방과 연결된 Gate의 양쪽 청크 타일 맵 출력
        if (f2.gates != null)
        {
            int startRoomId = -1;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (f2.chunks[x, y].roomRole == RoomRole.StartRoom)
                        startRoomId = f2.chunks[x, y].roomId;

            foreach (var g in f2.gates)
            {
                if (g.roomA == startRoomId || g.roomB == startRoomId)
                {
                    PrintChunkTiles(ref f2, g.chunkAX, g.chunkAY, $"Gate-A room{g.roomA}");
                    PrintChunkTiles(ref f2, g.chunkBX, g.chunkBY, $"Gate-B room{g.roomB}");
                }
            }
        }

        // 도달 가능 방 확인
        if (startWx >= 0)
        {
            int totalW = w * 8;
            int totalH = h * 8;
            bool[,] visited = new bool[totalW, totalH];
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue((startWx, startWy));
            visited[startWx, startWy] = true;
            int[] ddx = { 1, -1, 0, 0 };
            int[] ddy = { 0, 0, 1, -1 };
            int floorTilesVisited = 0;

            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                floorTilesVisited++;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + ddx[d];
                    int ny = cy + ddy[d];
                    if (nx < 0 || nx >= totalW || ny < 0 || ny >= totalH) continue;
                    if (visited[nx, ny]) continue;
                    int chunkX = nx / 8, chunkY = ny / 8;
                    int tileX = nx % 8, tileY = ny % 8;
                    if (f2.chunks[chunkX, chunkY].chunk != null && f2.chunks[chunkX, chunkY].chunk[tileX, tileY].name != "Wall")
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }

            Debug.Log($"F2 FloodFill: visited {floorTilesVisited} floor tiles");

            var reachable = new HashSet<int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (f2.chunks[x, y].roomId < 0) continue;
                    bool reached = false;
                    for (int tx = 0; tx < 8 && !reached; tx++)
                        for (int ty = 0; ty < 8 && !reached; ty++)
                            if (f2.chunks[x, y].chunk != null && f2.chunks[x, y].chunk[tx, ty].name != "Wall" && visited[x * 8 + tx, y * 8 + ty])
                                reached = true;
                    if (reached) reachable.Add(f2.chunks[x, y].roomId);
                }

            Debug.Log($"F2 reachable rooms: {reachable.Count} — {string.Join(", ", reachable)}");

            foreach (var kvp in roomChunks)
            {
                if (!reachable.Contains(kvp.Key))
                {
                    Debug.LogWarning($"F2 UNREACHABLE: roomId={kvp.Key}, role={roomRoles[kvp.Key]}");
                    // 이 방의 첫 번째 청크 타일 출력
                    var (fx, fy) = kvp.Value[0];
                    PrintChunkTiles(ref f2, fx, fy, $"Unreachable room{kvp.Key}");

                    // 이 방에 연결된 Gate 확인
                    if (f2.gates != null)
                    {
                        foreach (var g in f2.gates)
                        {
                            if (g.roomA == kvp.Key || g.roomB == kvp.Key)
                            {
                                Debug.LogWarning($"  Has Gate: room{g.roomA}<->room{g.roomB} at [{g.chunkAX},{g.chunkAY}]-[{g.chunkBX},{g.chunkBY}], w={g.width}, h={g.isHorizontal}");
                                AnalyzeGateTiles(ref f2, g);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"  NO gates for this room!");
                    }
                }
            }
        }

        // Validation 결과
        Debug.Log($"Validation passed: {cm.lastValidationPassed}");
        foreach (string err in cm.lastValidationErrors)
            Debug.LogWarning($"Validation: {err}");

        Object.DestroyImmediate(go);
    }

    static void PrintChunkTiles(ref Floor floor, int cx, int cy, string label)
    {
        Chunks c = floor.chunks[cx, cy];
        if (c.chunk == null)
        {
            Debug.Log($"  [{cx},{cy}] {label}: chunk is null");
            return;
        }

        string header = $"  [{cx},{cy}] {label} (roomId={c.roomId}, role={c.roomRole}):";
        // Print top to bottom (ty=7 at top)
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(header);
        for (int ty = 7; ty >= 0; ty--)
        {
            sb.Append($"    ty{ty}: ");
            for (int tx = 0; tx < 8; tx++)
            {
                string n = c.chunk[tx, ty].name;
                if (n == "Wall") sb.Append("W");
                else if (n == "Floor") sb.Append(".");
                else if (n == "Stair") sb.Append("S");
                else sb.Append("?");
            }
            sb.AppendLine();
        }
        Debug.Log(sb.ToString());
    }

    static void AnalyzeGateTiles(ref Floor floor, Gate g)
    {
        int w = g.width;
        if (g.isHorizontal)
        {
            int leftX = Mathf.Min(g.chunkAX, g.chunkBX);
            int rightX = Mathf.Max(g.chunkAX, g.chunkBX);
            int y = g.chunkAY;

            Chunks cL = floor.chunks[leftX, y];
            Chunks cR = floor.chunks[rightX, y];

            int startTy = (8 - Mathf.Clamp(w, 2, 6)) / 2;
            int endTy = startTy + Mathf.Clamp(w, 2, 6) - 1;

            bool leftOpen = false, rightOpen = false;
            for (int ty = startTy; ty <= endTy; ty++)
            {
                if (cL.chunk != null && cL.chunk[7, ty].name != "Wall") leftOpen = true;
                if (cR.chunk != null && cR.chunk[0, ty].name != "Wall") rightOpen = true;
            }

            if (!leftOpen || !rightOpen)
            {
                Debug.LogWarning($"  Gate room{g.roomA}<->room{g.roomB} BLOCKED: leftEdgeOpen={leftOpen}, rightEdgeOpen={rightOpen}");
                if (cL.chunk != null)
                {
                    var sb = new System.Text.StringBuilder($"  Left chunk[{leftX},{y}] tx=6,7: ");
                    for (int ty = 0; ty < 8; ty++)
                        sb.Append($"({cL.chunk[6, ty].name[0]},{cL.chunk[7, ty].name[0]}) ");
                    Debug.Log(sb.ToString());
                }
                if (cR.chunk != null)
                {
                    var sb = new System.Text.StringBuilder($"  Right chunk[{rightX},{y}] tx=0,1: ");
                    for (int ty = 0; ty < 8; ty++)
                        sb.Append($"({cR.chunk[0, ty].name[0]},{cR.chunk[1, ty].name[0]}) ");
                    Debug.Log(sb.ToString());
                }
            }
        }
        else
        {
            int x = g.chunkAX;
            int bottomY = Mathf.Min(g.chunkAY, g.chunkBY);
            int topY = Mathf.Max(g.chunkAY, g.chunkBY);

            Chunks cB = floor.chunks[x, bottomY];
            Chunks cT = floor.chunks[x, topY];

            int startTx = (8 - Mathf.Clamp(w, 2, 6)) / 2;
            int endTx = startTx + Mathf.Clamp(w, 2, 6) - 1;

            bool bottomOpen = false, topOpen = false;
            for (int tx = startTx; tx <= endTx; tx++)
            {
                if (cB.chunk != null && cB.chunk[tx, 7].name != "Wall") bottomOpen = true;
                if (cT.chunk != null && cT.chunk[tx, 0].name != "Wall") topOpen = true;
            }

            if (!bottomOpen || !topOpen)
            {
                Debug.LogWarning($"  Gate room{g.roomA}<->room{g.roomB} BLOCKED: bottomEdgeOpen={bottomOpen}, topEdgeOpen={topOpen}");
                if (cB.chunk != null)
                {
                    var sb = new System.Text.StringBuilder($"  Bottom chunk[{x},{bottomY}] ty=6,7: ");
                    for (int tx = 0; tx < 8; tx++)
                        sb.Append($"({cB.chunk[tx, 6].name[0]},{cB.chunk[tx, 7].name[0]}) ");
                    Debug.Log(sb.ToString());
                }
                if (cT.chunk != null)
                {
                    var sb = new System.Text.StringBuilder($"  Top chunk[{x},{topY}] ty=0,1: ");
                    for (int tx = 0; tx < 8; tx++)
                        sb.Append($"({cT.chunk[tx, 0].name[0]},{cT.chunk[tx, 1].name[0]}) ");
                    Debug.Log(sb.ToString());
                }
            }
        }
    }
}
