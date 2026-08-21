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

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];

                if (c.chunk == null)
                    c.chunk = new Tile[8, 8];

                if (c.roomId == -1)
                {
                    for (int tx = 0; tx < 8; tx++)
                        for (int ty = 0; ty < 8; ty++)
                            c.chunk[tx, ty] = TileFactory.Wall();
                    floor.chunks[x, y] = c;
                    continue;
                }

                for (int tx = 0; tx < 8; tx++)
                {
                    for (int ty = 0; ty < 8; ty++)
                    {
                        c.chunk[tx, ty] = (tx == 0 || tx == 7 || ty == 0 || ty == 7)
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
        int thkMin = floor.config.wallThicknessMin;
        int thkMax = floor.config.wallThicknessMax;

        var roomBounds = ComputeRoomBounds(ref floor);

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

                int thicknessX, thicknessY;
                if (c.roomRole == RoomRole.StartRoom || c.roomRole == RoomRole.BossRoom)
                {
                    thicknessX = 1;
                    thicknessY = 1;
                }
                else
                {
                    if (!wallThicknessCache.TryGetValue(c.roomId, out var cached))
                    {
                        cached = ComputeVariableThicknessXY(c.roomId, roomBounds, thkMin, thkMax);
                        wallThicknessCache[c.roomId] = cached;
                    }
                    thicknessX = cached.thicknessX;
                    thicknessY = cached.thicknessY;
                }

                // 양쪽이 모두 외곽인 축은 벽 두께 합이 8 이상이면
                // 내부 Floor가 완전히 사라지므로 최소 2칸 확보하도록 제한
                if (isLeft && isRight)
                    thicknessX = Mathf.Min(thicknessX, 3);
                if (isBottom && isTop)
                    thicknessY = Mathf.Min(thicknessY, 3);

                for (int tx = 0; tx < 8; tx++)
                {
                    for (int ty = 0; ty < 8; ty++)
                    {
                        bool shouldWall = false;

                        if (isLeft   && tx < thicknessX) shouldWall = true;
                        if (isRight  && tx >= 8 - thicknessX) shouldWall = true;
                        if (isBottom && ty < thicknessY) shouldWall = true;
                        if (isTop    && ty >= 8 - thicknessY) shouldWall = true;

                        if (shouldWall)
                            c.chunk[tx, ty] = TileFactory.Wall();
                    }
                }

                floor.chunks[cx, cy] = c;
            }
        }
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

    (int thicknessX, int thicknessY) ComputeVariableThicknessXY(int roomId, Dictionary<int, (int minX, int minY, int maxX, int maxY)> roomBounds, int thkMin, int thkMax)
    {
        if (!roomBounds.TryGetValue(roomId, out var b))
        {
            int fallback = UnityEngine.Random.Range(thkMin, thkMax + 1);
            return (fallback, fallback);
        }

        int spanX = b.maxX - b.minX + 1;
        int spanY = b.maxY - b.minY + 1;

        int thicknessX = SampleThicknessForSpan(spanX, thkMin, thkMax);
        int thicknessY = SampleThicknessForSpan(spanY, thkMin, thkMax);

        return (thicknessX, thicknessY);
    }

    int SampleThicknessForSpan(int span, int thkMin, int thkMax)
    {
        // 양쪽이 동시에 외곽일 때 내부 Floor가 사라지지 않도록
        // 한쪽 최대 두께를 3으로 제한 (3+3=6 < 8 → 최소 2칸 Floor 확보)
        int upperLimit = (span <= 1) ? 2 : 3;

        int effectiveMin = Mathf.Max(thkMin, 1);
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
        int worldMaxX = floor.config.width * 8 - 1;
        int worldMaxY = floor.config.height * 8 - 1;
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

        int thkA = GetWallThickness(cA.roomId, true);
        int thkB = GetWallThickness(cB.roomId, true);

        for (int ty = 0; ty <= 7; ty++)
        {
            int worldAX = x * 8 + 7;
            int worldAY = y * 8 + ty;
            int worldBX = (x + 1) * 8;
            int worldBY = worldAY;

            if (IsWorldBorder(ref floor, worldAX, worldAY) || IsWorldBorder(ref floor, worldBX, worldBY)) continue;

            if (ty == 0)
            {
                if (GetRoomId(ref floor, x, y - 1) != roomId || GetRoomId(ref floor, x + 1, y - 1) != roomId)
                    continue;
            }
            else if (ty == 7)
            {
                if (GetRoomId(ref floor, x, y + 1) != roomId || GetRoomId(ref floor, x + 1, y + 1) != roomId)
                    continue;
            }

            for (int tx = 8 - thkA; tx < 8; tx++)
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

        int thkA = GetWallThickness(cA.roomId, false);
        int thkB = GetWallThickness(cB.roomId, false);

        for (int tx = 0; tx <= 7; tx++)
        {
            int worldAX = x * 8 + tx;
            int worldAY = y * 8 + 7;
            int worldBX = worldAX;
            int worldBY = (y + 1) * 8;

            if (IsWorldBorder(ref floor, worldAX, worldAY) || IsWorldBorder(ref floor, worldBX, worldBY)) continue;

            if (tx == 0)
            {
                if (GetRoomId(ref floor, x - 1, y) != roomId || GetRoomId(ref floor, x - 1, y + 1) != roomId)
                    continue;
            }
            else if (tx == 7)
            {
                if (GetRoomId(ref floor, x + 1, y) != roomId || GetRoomId(ref floor, x + 1, y + 1) != roomId)
                    continue;
            }

            for (int ty = 8 - thkA; ty < 8; ty++)
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
    private static void GetWallAxis(TorchWallSide side, out bool isYAxis, out int edgeValue, out int inward)
    {
        switch (side)
        {
            case TorchWallSide.Top:    isYAxis = true;  edgeValue = 7; inward = -1; break;
            case TorchWallSide.Bottom: isYAxis = true;  edgeValue = 0; inward = 1;  break;
            case TorchWallSide.Right:  isYAxis = false; edgeValue = 7; inward = -1; break;
            default:                   isYAxis = false; edgeValue = 0; inward = 1;  break; // Left
        }
    }

    // 청크 로컬 8칸짜리 경계 한 줄이 전부 Wall이면 "복도 없이 완전히 막힌 벽"(횃불 후보), 일부만
    // Wall이면 게이트(복도)가 뚫려 있다는 뜻(제외), 전부 Wall이 아니면 애초에 벽이 아니다(같은 방
    // 인접 청크와 통짜로 붙어있음, 제외) — OpenInternalWalls/OpenHorizontalPassage·
    // OpenVerticalPassage(게이트 폭 2~6칸 부분 개방)가 만드는 세 경우를 타일 값만으로 구분한다.
    public static bool IsSolidWallEdge(Chunks c, TorchWallSide side)
    {
        GetWallAxis(side, out bool isYAxis, out int edgeValue, out _);
        for (int i = 0; i < 8; i++)
        {
            int tx = isYAxis ? i : edgeValue;
            int ty = isYAxis ? edgeValue : i;
            if (c.chunk[tx, ty].name != "Wall") return false;
        }
        return true;
    }

    // 벽 중앙 기준선(가로 벽은 로컬 x=4 열, 세로 벽은 로컬 y=4 행)을 따라 벽 안쪽으로 걸어 들어가
    // 처음 만나는 Floor 타일을 반환한다 — 벽 두께(1~3칸, ApplyOuterWallThickness)가 얼마든 항상
    // "벽에 맞닿은 바닥 칸"을 정확히 찾아 벽과 스프라이트가 겹치지 않게 한다. 도중에 Floor가 아닌
    // 타일(Stair 등)을 만나면 실패 처리한다.
    public static bool TryFindFloorTileInFrontOfWall(Chunks c, TorchWallSide side, out Vector2Int local)
    {
        const int center = 4;
        GetWallAxis(side, out bool isYAxis, out int edgeValue, out int inward);
        for (int step = 0; step < 8; step++)
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
