// ============================================================================
// CreateMap.TileWall.cs — 타일 이름 · 벽 두께 처리
// ----------------------------------------------------------------------------
// 역할: 청크 내 타일 이름 지정(AssignTileNames),
//       외곽 벽 두께 적용(ApplyOuterWallThickness),
//       같은 방 내부 청크 간 벽 허물기(OpenInternalWalls),
//       벽 두께 계산 유틸리티.
// 단계: GenerateMap Phase 2 — 연결 구축 직전, 타일 레벨 처리
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

public partial class CreateMap
{
    // ③.9 가장자리=Wall, 내부=Floor / roomId==-1이면 전체 Wall
    void AssignTileNames(ref Floor floor)
    {
        int w = floor.config.width;
        int h = floor.config.height;
        int cs = floor.config.chunkSize;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];

                if (c.chunk == null)
                    c.chunk = new Tile[cs, cs];

                if (c.roomId == -1)
                {
                    for (int tx = 0; tx < cs; tx++)
                        for (int ty = 0; ty < cs; ty++)
                            c.chunk[tx, ty] = TileFactory.Wall();
                    floor.chunks[x, y] = c;
                    continue;
                }

                for (int tx = 0; tx < cs; tx++)
                {
                    for (int ty = 0; ty < cs; ty++)
                    {
                        c.chunk[tx, ty] = (tx == 0 || tx == cs - 1 || ty == 0 || ty == cs - 1)
                            ? TileFactory.Wall()
                            : TileFactory.Floor();
                    }
                }

                floor.chunks[x, y] = c;
            }
        }

        // 외곽 벽 두께 적용
        ApplyOuterWallThickness(ref floor);

        LogHelper.Log(LogHelper.GAME, $"CreateMap: Floor {(int)floor.config.floorId} tile names assigned.");
    }

    // ③.9b 외곽 경계 + 빈 청크 인접 경계에 벽 두께 적용
    void ApplyOuterWallThickness(ref Floor floor)
    {
        int w = floor.config.width;
        int h = floor.config.height;
        int cs = floor.config.chunkSize;
        int thkMin = floor.config.wallThicknessMin;
        int thkMax = floor.config.wallThicknessMax;

        var roomBounds = ComputeRoomBounds(ref floor);

        // 같은 방이 여러 청크에 걸쳐 이어지는 변에서 매번 기준 두께로 리셋되면 청크 경계마다 눈에
        // 띄는 턱이 생긴다. 직전 청크의 마지막 두께 값을 이어받아 다음 청크가 그 값부터 랜덤워크를
        // 이어가게 한다(같은 roomId라도 실제로 붙어있지 않으면 이어붙이지 않음).
        var bottomContinuity = new Dictionary<(int roomId, int cy), (int lastCx, int value)>();
        var topContinuity = new Dictionary<(int roomId, int cy), (int lastCx, int value)>();
        var leftContinuity = new Dictionary<(int roomId, int cx), (int lastCy, int value)>();
        var rightContinuity = new Dictionary<(int roomId, int cx), (int lastCy, int value)>();

        for (int cx = 0; cx < w; cx++)
        {
            for (int cy = 0; cy < h; cy++)
            {
                Chunks c = floor.chunks[cx, cy];
                if (c.roomId == -1 || c.chunk == null) continue;

                bool isLeft   = GetRoomId(ref floor, cx - 1, cy) == -1;
                bool isRight  = GetRoomId(ref floor, cx + 1, cy) == -1;
                bool isBottom = GetRoomId(ref floor, cx, cy - 1) == -1;
                bool isTop    = GetRoomId(ref floor, cx, cy + 1) == -1;

                if (!isLeft && !isRight && !isBottom && !isTop) continue;

                bool isFixedThicknessRoom = c.roomRole == RoomRole.StartRoom || c.roomRole == RoomRole.BossRoom;

                // StartRoom/BossRoom: 정확히 1칸 고정 두께 직선 벽(WallThickness_StartAndBossRoomsHaveThickness1
                // 테스트가 이를 보장하는 고정 규격 방이라 아래 랜덤 프로파일 대상에서 제외).
                if (isFixedThicknessRoom)
                {
                    int fixedThickness = 1;
                    for (int tx = 0; tx < cs; tx++)
                    {
                        for (int ty = 0; ty < cs; ty++)
                        {
                            bool shouldWall = false;
                            if (isLeft   && tx < fixedThickness) shouldWall = true;
                            if (isRight  && tx >= cs - fixedThickness) shouldWall = true;
                            if (isBottom && ty < fixedThickness) shouldWall = true;
                            if (isTop    && ty >= cs - fixedThickness) shouldWall = true;
                            if (shouldWall)
                                c.chunk[tx, ty] = TileFactory.Wall();
                        }
                    }
                    floor.chunks[cx, cy] = c;
                    continue;
                }

                if (!wallThicknessCache.TryGetValue(c.roomId, out var cached))
                {
                    cached = ComputeVariableThicknessXY(c.roomId, roomBounds, thkMin, thkMax, cs);
                    wallThicknessCache[c.roomId] = cached;
                }
                int thicknessX = cached.thicknessX;
                int thicknessY = cached.thicknessY;

                // 양쪽이 모두 외곽인 축(좁은 통로)은 벽 두께 합이 청크 크기 이상이면 내부 Floor가
                // 완전히 사라지므로 최소 2칸 확보하도록 제한(SampleThicknessForSpan과 동일한 비율 상한).
                int bothSidesCap = cs * 3 / 8;
                bool bothX = isLeft && isRight;
                bool bothY = isBottom && isTop;
                if (bothX) thicknessX = Mathf.Min(thicknessX, bothSidesCap);
                if (bothY) thicknessY = Mathf.Min(thicknessY, bothSidesCap);

                // ── 가장자리를 따라 두께가 굵게 출렁이는 랜덤워크 프로파일 ──────────────────
                // 방 하나에 값 하나가 아니라 가장자리 칸마다 독립적으로 값이 출렁이게 해 자연 지형처럼
                // 만든다. 순수 시각적 외곽 채움(통로·게이트와는 분리된 야생/맵경계 접촉면 전용)이라
                // 통행 경로에는 영향이 없다.
                int minGuaranteed = Mathf.Max(thkMin, 2); // 최소 채움 보장 — 1칸짜리 약한 벽 방지
                // 한쪽만 외곽인 경우(통로 폭 제한 없음): 청크 절반까지 과감하게 허용.
                int singleSideMax = Mathf.Max(minGuaranteed, cs / 2);
                int boldMaxX = bothX ? Mathf.Max(minGuaranteed, thicknessX) : singleSideMax;
                int boldMaxY = bothY ? Mathf.Max(minGuaranteed, thicknessY) : singleSideMax;

                int leftStart = thicknessX;
                if (isLeft && leftContinuity.TryGetValue((c.roomId, cx), out var lc) && lc.lastCy == cy - 1)
                    leftStart = lc.value;
                int rightStart = thicknessX;
                if (isRight && rightContinuity.TryGetValue((c.roomId, cx), out var rc) && rc.lastCy == cy - 1)
                    rightStart = rc.value;
                int bottomStart = thicknessY;
                if (isBottom && bottomContinuity.TryGetValue((c.roomId, cy), out var botc) && botc.lastCx == cx - 1)
                    bottomStart = botc.value;
                int topStart = thicknessY;
                if (isTop && topContinuity.TryGetValue((c.roomId, cy), out var topc) && topc.lastCx == cx - 1)
                    topStart = topc.value;

                int[] leftProfile   = isLeft   ? BuildEdgeThicknessProfile(cs, leftStart, minGuaranteed, boldMaxX) : null;
                int[] rightProfile  = isRight  ? BuildEdgeThicknessProfile(cs, rightStart, minGuaranteed, boldMaxX) : null;
                int[] bottomProfile = isBottom ? BuildEdgeThicknessProfile(cs, bottomStart, minGuaranteed, boldMaxY) : null;
                int[] topProfile    = isTop    ? BuildEdgeThicknessProfile(cs, topStart, minGuaranteed, boldMaxY) : null;

                if (isLeft)   leftContinuity[(c.roomId, cx)] = (cy, leftProfile[cs - 1]);
                if (isRight)  rightContinuity[(c.roomId, cx)] = (cy, rightProfile[cs - 1]);
                if (isBottom) bottomContinuity[(c.roomId, cy)] = (cx, bottomProfile[cs - 1]);
                if (isTop)    topContinuity[(c.roomId, cy)] = (cx, topProfile[cs - 1]);

                for (int tx = 0; tx < cs; tx++)
                {
                    for (int ty = 0; ty < cs; ty++)
                    {
                        bool shouldWall = false;

                        if (isLeft   && tx < leftProfile[ty])        shouldWall = true;
                        if (isRight  && tx >= cs - rightProfile[ty]) shouldWall = true;
                        if (isBottom && ty < bottomProfile[tx])      shouldWall = true;
                        if (isTop    && ty >= cs - topProfile[tx])   shouldWall = true;

                        if (shouldWall)
                            c.chunk[tx, ty] = TileFactory.Wall();
                    }
                }

                floor.chunks[cx, cy] = c;
            }
        }
    }

    // 길이 length의 가장자리를 따라 두께가 startValue에서 시작해, 칸마다 30% 확률로만 ±1씩 흔들리는
    // 프로파일을 만든다. startValue가 범위를 벗어나 있어도 즉시 스냅하지 않고 한 칸에 1칸씩만
    // 계단식으로 복귀시켜, 청크 경계에서 두께가 뚝 끊기지 않게 한다.
    int[] BuildEdgeThicknessProfile(int length, int startValue, int minThickness, int maxThickness)
    {
        int lo = Mathf.Min(minThickness, maxThickness);
        int hi = Mathf.Max(minThickness, maxThickness);
        var profile = new int[length];
        int current = startValue;

        for (int i = 0; i < length; i++)
        {
            if (current < lo)
                current++;                         // 최소 채움 쪽으로 한 칸씩 계단 복귀
            else if (current > hi)
                current--;                          // 과감한 최대치 쪽으로 한 칸씩 계단 복귀
            else if (UnityEngine.Random.value < 0.3f)
            {
                // 매 칸마다 흔들리면 전체가 구불구불해 보이므로 낮은 확률로만 ±1 스텝을 밟고
                // 나머지 칸은 직전 값을 유지한다 — 평평한 구간 사이사이에 완만한 턱만 생기게 절제.
                int step = UnityEngine.Random.Range(-1, 2);
                current = Mathf.Clamp(current + step, lo, hi);
            }
            profile[i] = current;
        }

        return profile;
    }

    Dictionary<int, (int minX, int minY, int maxX, int maxY)> ComputeRoomBounds(ref Floor floor)
    {
        int w = floor.config.width;
        int h = floor.config.height;
        var bounds = new Dictionary<int, (int minX, int minY, int maxX, int maxY)>();

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int id = floor.chunks[x, y].roomId;
                if (id < 0) continue;

                if (bounds.TryGetValue(id, out var b))
                {
                    bounds[id] = (
                        Mathf.Min(b.minX, x), Mathf.Min(b.minY, y),
                        Mathf.Max(b.maxX, x), Mathf.Max(b.maxY, y)
                    );
                }
                else
                {
                    bounds[id] = (x, y, x, y);
                }
            }
        }

        return bounds;
    }

    (int thicknessX, int thicknessY) ComputeVariableThicknessXY(int roomId, Dictionary<int, (int minX, int minY, int maxX, int maxY)> roomBounds, int thkMin, int thkMax, int chunkSize)
    {
        if (!roomBounds.TryGetValue(roomId, out var b))
        {
            int fallback = UnityEngine.Random.Range(thkMin, thkMax + 1);
            return (fallback, fallback);
        }

        int spanX = b.maxX - b.minX + 1;
        int spanY = b.maxY - b.minY + 1;

        int thicknessX = SampleThicknessForSpan(spanX, thkMin, thkMax, chunkSize);
        int thicknessY = SampleThicknessForSpan(spanY, thkMin, thkMax, chunkSize);

        return (thicknessX, thicknessY);
    }

    int SampleThicknessForSpan(int span, int thkMin, int thkMax, int chunkSize)
    {
        // 양쪽이 동시에 외곽일 때 내부 Floor가 사라지지 않도록 한쪽 최대 두께를 제한한다. 청크
        // 크기가 층별로 달라질 수 있어 비율로 일반화했다.
        int upperLimit = (span <= 1) ? chunkSize / 4 : chunkSize * 3 / 8;

        // 기본 두께 시드 자체도 1칸까지 내려가지 않게 해서 랜덤워크가 항상 든든한 값에서 출발하게 한다.
        int effectiveMin = Mathf.Max(thkMin, 2);
        int effectiveMax = Mathf.Min(thkMax, upperLimit);
        if (effectiveMax < effectiveMin) effectiveMax = effectiveMin;

        return UnityEngine.Random.Range(effectiveMin, effectiveMax + 1);
    }

    // ③.10 같은 roomId 청크 간 내부 벽 허물기
    void OpenInternalWalls(ref Floor floor)
    {
        int w = floor.config.width;
        int h = floor.config.height;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int id = floor.chunks[x, y].roomId;
                if (id == -1) continue;

                if (x + 1 < w && floor.chunks[x + 1, y].roomId == id)
                    RemoveHorizontalWall(ref floor, x, y, id);

                if (y + 1 < h && floor.chunks[x, y + 1].roomId == id)
                    RemoveVerticalWall(ref floor, x, y, id);
            }
        }

        LogHelper.Log(LogHelper.GAME, $"CreateMap: Floor {(int)floor.config.floorId} internal walls removed.");
    }

    int GetRoomId(ref Floor floor, int cx, int cy)
    {
        int w = floor.config.width;
        int h = floor.config.height;
        if (cx < 0 || cx >= w || cy < 0 || cy >= h) return -1;
        return floor.chunks[cx, cy].roomId;
    }

    bool IsWorldBorder(ref Floor floor, int worldX, int worldY)
    {
        int cs = floor.config.chunkSize;
        int worldMaxX = floor.config.width * cs - 1;
        int worldMaxY = floor.config.height * cs - 1;
        return worldX <= 0 || worldX >= worldMaxX || worldY <= 0 || worldY >= worldMaxY;
    }

    int GetWallThickness(int roomId, bool isXAxis)
    {
        if (roomId < 0) return 1;
        if (wallThicknessCache.TryGetValue(roomId, out var cached))
            return isXAxis ? cached.thicknessX : cached.thicknessY;
        return 1;
    }

    void RemoveHorizontalWall(ref Floor floor, int x, int y, int roomId)
    {
        Chunks cA = floor.chunks[x, y];
        Chunks cB = floor.chunks[x + 1, y];
        int cs = floor.config.chunkSize;

        int thkA = GetWallThickness(cA.roomId, true);
        int thkB = GetWallThickness(cB.roomId, true);

        for (int ty = 0; ty <= cs - 1; ty++)
        {
            int worldAX = x * cs + cs - 1;
            int worldAY = y * cs + ty;
            int worldBX = (x + 1) * cs;
            int worldBY = worldAY;

            if (IsWorldBorder(ref floor, worldAX, worldAY) || IsWorldBorder(ref floor, worldBX, worldBY)) continue;

            if (ty == 0)
            {
                if (GetRoomId(ref floor, x, y - 1) != roomId || GetRoomId(ref floor, x + 1, y - 1) != roomId)
                    continue;
            }
            else if (ty == cs - 1)
            {
                if (GetRoomId(ref floor, x, y + 1) != roomId || GetRoomId(ref floor, x + 1, y + 1) != roomId)
                    continue;
            }

            for (int tx = cs - thkA; tx < cs; tx++)
                cA.chunk[tx, ty] = TileFactory.Floor();
            for (int tx = 0; tx < thkB; tx++)
                cB.chunk[tx, ty] = TileFactory.Floor();
        }

        floor.chunks[x, y] = cA;
        floor.chunks[x + 1, y] = cB;
    }

    void RemoveVerticalWall(ref Floor floor, int x, int y, int roomId)
    {
        Chunks cA = floor.chunks[x, y];
        Chunks cB = floor.chunks[x, y + 1];
        int cs = floor.config.chunkSize;

        int thkA = GetWallThickness(cA.roomId, false);
        int thkB = GetWallThickness(cB.roomId, false);

        for (int tx = 0; tx <= cs - 1; tx++)
        {
            int worldAX = x * cs + tx;
            int worldAY = y * cs + cs - 1;
            int worldBX = worldAX;
            int worldBY = (y + 1) * cs;

            if (IsWorldBorder(ref floor, worldAX, worldAY) || IsWorldBorder(ref floor, worldBX, worldBY)) continue;

            if (tx == 0)
            {
                if (GetRoomId(ref floor, x - 1, y) != roomId || GetRoomId(ref floor, x - 1, y + 1) != roomId)
                    continue;
            }
            else if (tx == cs - 1)
            {
                if (GetRoomId(ref floor, x + 1, y) != roomId || GetRoomId(ref floor, x + 1, y + 1) != roomId)
                    continue;
            }

            for (int ty = cs - thkA; ty < cs; ty++)
                cA.chunk[tx, ty] = TileFactory.Floor();
            for (int ty = 0; ty < thkB; ty++)
                cB.chunk[tx, ty] = TileFactory.Floor();
        }

        floor.chunks[x, y] = cA;
        floor.chunks[x, y + 1] = cB;
    }

    // ── 횃불 벽걸이 배치 전용 청크 경계 질의 ──────────────────────────────
    // "청크 경계 한 면이 벽인지/게이트로 뚫렸는지" 판정은 이 파일이 담당해 시각 오버레이 계층이
    // 타일 이름 문자열을 직접 알 필요가 없게 한다. side 하나당 축 정의(isYAxis/edgeValue/inward)만
    // 있으면 IsSolidWallEdge와 TryFindFloorTileInFrontOfWall 둘 다 여기서 파생된다.
    private static void GetWallAxis(TorchWallSide side, int chunkSize, out bool isYAxis, out int edgeValue, out int inward)
    {
        switch (side)
        {
            case TorchWallSide.Top:    isYAxis = true;  edgeValue = chunkSize - 1; inward = -1; break;
            case TorchWallSide.Bottom: isYAxis = true;  edgeValue = 0; inward = 1;  break;
            case TorchWallSide.Right:  isYAxis = false; edgeValue = chunkSize - 1; inward = -1; break;
            default:                   isYAxis = false; edgeValue = 0; inward = 1;  break; // Left
        }
    }

    // 청크 로컬 경계 한 줄이 전부 Wall이면 "완전히 막힌 벽"(횃불 후보), 일부만 Wall이면 게이트(복도)가
    // 뚫려 있다는 뜻(제외), 전부 Wall이 아니면 애초에 벽이 아니다(제외).
    public static bool IsSolidWallEdge(Chunks c, TorchWallSide side)
    {
        int cs = c.chunk.GetLength(0);
        GetWallAxis(side, cs, out bool isYAxis, out int edgeValue, out _);
        for (int i = 0; i < cs; i++)
        {
            int tx = isYAxis ? i : edgeValue;
            int ty = isYAxis ? edgeValue : i;
            if (c.chunk[tx, ty].name != "Wall") return false;
        }
        return true;
    }

    // 벽 중앙 기준선을 따라 안쪽으로 걸어 들어가 처음 만나는 Floor 타일을 반환한다 — 벽 두께가
    // 얼마든 "벽에 맞닿은 바닥 칸"을 정확히 찾는다. 도중 Floor가 아닌 타일(Stair 등)을 만나면 실패 처리.
    public static bool TryFindFloorTileInFrontOfWall(Chunks c, TorchWallSide side, out Vector2Int local)
    {
        int cs = c.chunk.GetLength(0);
        int center = cs / 2;
        GetWallAxis(side, cs, out bool isYAxis, out int edgeValue, out int inward);
        for (int step = 0; step < cs; step++)
        {
            int coord = edgeValue + inward * step;
            int tx = isYAxis ? center : coord;
            int ty = isYAxis ? coord : center;

            string name = c.chunk[tx, ty].name;
            if (name == "Wall") continue;

            local = new Vector2Int(tx, ty);
            return name == "Floor";
        }

        local = default;
        return false;
    }
}
