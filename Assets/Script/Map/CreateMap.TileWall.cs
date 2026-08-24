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

        // 같은 방이 여러 청크에 걸쳐 이어지는 변(가장자리)에서 각 청크가 매번 기준 두께로
        // 리셋되면 청크 경계마다 눈에 띄는 턱("±2로 채워지는 부분", 2026-08-24 사용자 신고)이
        // 생긴다. 직전 청크의 마지막 두께 값을 이어받아 다음 청크가 그 값에서부터 랜덤워크를
        // 계속하게 해서 청크 경계를 가로질러도 두께가 매끄럽게 이어지게 한다. cx가 outer
        // 루프라 bottom/top(가로 변, cy 고정)은 (roomId, cy) 키로 lastCx 인접 여부를 확인하고,
        // left/right(세로 변, cx 고정)는 cy가 inner 루프에서 항상 오름차순이라 (roomId, cx)
        // 키로 lastCy 인접 여부를 확인한다. 같은 roomId라도 청크가 실제로 붙어있지 않으면(다른
        // 덩어리 사이 빈틈) 이어붙이지 않도록 인접성을 검사한 뒤에만 이어받는다.
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

                // 양쪽이 모두 외곽인 축(방이 딱 청크 하나 폭인 좁은 통로)은 벽 두께 합이 청크
                // 크기 이상이면 내부 Floor가 완전히 사라지므로 최소 2칸 확보하도록 제한
                // (SampleThicknessForSpan과 동일한 비율 상한).
                int bothSidesCap = cs * 3 / 8;
                bool bothX = isLeft && isRight;
                bool bothY = isBottom && isTop;
                if (bothX) thicknessX = Mathf.Min(thicknessX, bothSidesCap);
                if (bothY) thicknessY = Mathf.Min(thicknessY, bothSidesCap);

                // ── 가장자리를 따라 두께가 굵게 출렁이는 랜덤워크 프로파일 ──────────────────
                // 사용자 피드백(2026-08-24, "전혀 개선 안되었어. 랜덤성을 짙게, 최소 채움은
                // 보장, 더 과감하게 채워도 된다 — 어차피 맵 크기는 커졌다") — 고정 두께 직선
                // 밴드 하나로는 코너/가장자리가 늘 반듯한 인공적인 선으로 보인다. 방 하나에
                // 값 하나가 아니라, 가장자리 칸(ty 또는 tx)마다 독립적으로 값이 출렁이는
                // 랜덤워크를 써서 두툼하고 들쭉날쭉한 자연 지형처럼 만든다. 이 값은 순수 시각적
                // 외곽 채움(같은 방의 내부 청크 간 통로를 여는 OpenInternalWalls, 방-방 게이트를
                // 뚫는 ConnectRooms와는 완전히 분리된 야생/맵경계 접촉면에만 적용)이라 아무리
                // 두껍게 먹어도 통행 경로에는 영향이 없다.
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

    // 길이 length의 가장자리를 따라 두께가 startValue에서 시작해, 칸마다 30% 확률로만 ±1씩
    // 랜덤워크로 흔들리는(나머지 70%는 직전 값 유지) 프로파일을 만든다. 이웃 청크에서 이어받은
    // startValue가 이 청크의 [minThickness, maxThickness] 범위를 벗어나 있어도(청크마다 통로
    // 폭 제한 등으로 상한이 달라질 수 있음) 그 자리에서 즉시 스냅하지 않고, 아래 루프에서 한
    // 칸에 1칸씩만 계단식으로 범위 안으로 되돌아온다 — 청크 경계에서 두께가 뚝 끊기지 않고
    // 항상 계단형으로 자연스럽게 이어지도록 보장하는 핵심 불변식(2026-08-24 사용자 피드백:
    // "여전히 이어붙는 지점 끊김. 자연스럽게 계단형으로 스텝 이어지도록").
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
                // 매 칸마다 흔들리면 전체가 구불구불해 보여서(2026-08-24 피드백: "너무 굴곡이고
                // 어느정도 평평하게") 낮은 확률로만 ±1 스텝을 밟고 나머지 칸은 직전 값을 그대로
                // 유지한다 — 평평한 구간 사이사이에 완만한 턱이 생기는 정도로 절제.
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
        // 양쪽이 동시에 외곽일 때 내부 Floor가 사라지지 않도록 한쪽 최대 두께를 제한한다. 원래
        // 청크 크기 8 기준 "3으로 제한(3+3=6 < 8 → 최소 2칸 Floor 확보)"이었던 걸 청크 크기가
        // 층별로 달라질 수 있게 되면서(2026-08-23, 맵 1.5배 확장) 같은 비율로 일반화했다 —
        // chunkSize=8이면 upperLimit이 정확히 원래 값(2/3)과 같아 회귀 걱정 없다.
        int upperLimit = (span <= 1) ? chunkSize / 4 : chunkSize * 3 / 8;

        // 최소 채움 보장(2026-08-24 사용자 피드백) — 기본 두께 시드 자체도 1칸까지 내려가지
        // 않게 해서, 랜덤워크 프로파일(BuildEdgeThicknessProfile)이 항상 든든한 값에서
        // 출발하게 한다.
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

    // ── 횃불 벽걸이 배치(2026-08-21) 전용 청크 경계 질의 ──────────────────────────────
    // FogOfWarSystem(순수 시각 오버레이)이 직접 타일을 스캔하던 걸 여기로 옮겼다 — "청크 경계 한
    // 면이 벽인지/게이트로 뚫렸는지" 판정은 이 파일이 이미 담당하는 청크 벽 도메인 지식이라, 그
    // 판정 로직 자체는 지도 생성 계층에 속해야 시각 오버레이 계층이 타일 이름 문자열까지 직접
    // 알 필요가 없다(레이어 경계 유지).
    //
    // side 하나당 "어느 축을 따라 훑는지(isYAxis) + 그 축에서 벽 쪽 끝 좌표(edgeValue) + 안쪽으로
    // 전진하는 방향(inward)"만 정의하면 IsSolidWallEdge(경계 8칸 전부 스캔)와
    // TryFindFloorTileInFrontOfWall(중앙 기준선을 따라 안쪽으로 전진) 둘 다 이 하나의 축 정의에서
    // 파생된다 — 예전엔 두 메서드가 Top/Right/Bottom/Left 4갈래 switch를 각자 따로 들고 있어 벽면
    // 정의가 바뀌면 손으로 둘 다 맞춰야 했다.
    // chunkSize 자체 층별 설정화(2026-08-23, 맵 1.5배 확장)로 edgeValue(Top/Right 쪽 끝 좌표)도
    // "그 청크의 실제 크기 - 1"을 받아야 정확하다 — Chunks 하나만으로는 크기를 모르므로 호출부가
    // c.chunk.GetLength(0)으로 구해서 넘긴다.
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

    // 청크 로컬 경계 한 줄이 전부 Wall이면 "복도 없이 완전히 막힌 벽"(횃불 후보), 일부만
    // Wall이면 게이트(복도)가 뚫려 있다는 뜻(제외), 전부 Wall이 아니면 애초에 벽이 아니다(같은 방
    // 인접 청크와 통짜로 붙어있음, 제외) — OpenInternalWalls/OpenHorizontalPassage·
    // OpenVerticalPassage(게이트 폭 2~6칸 부분 개방)가 만드는 세 경우를 타일 값만으로 구분한다.
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

    // 벽 중앙 기준선(가로 벽은 로컬 x=중앙 열, 세로 벽은 로컬 y=중앙 행)을 따라 벽 안쪽으로 걸어
    // 들어가 처음 만나는 Floor 타일을 반환한다 — 벽 두께가 얼마든 항상 "벽에 맞닿은 바닥 칸"을
    // 정확히 찾아 벽과 스프라이트가 겹치지 않게 한다. 도중에 Floor가 아닌 타일(Stair 등)을 만나면
    // 실패 처리한다.
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
