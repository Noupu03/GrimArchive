#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

// ========================================================================
// CreateMap + MapRandering 자동화 테스트 (Floor 기반)
// ========================================================================

public class CreateMapPlayTests
{
    // ── 헬퍼: 테스트용 CreateMap GameObject 생성 ──
    private CreateMap SetupCreateMap(int seed = 14056)
    {
        var cm = new CreateMap();
        cm.useFixedSeed = true;
        cm.seed = seed;
        return cm;
    }

    // ── 헬퍼: 테스트용 MapRandering 씬 구성 ──
    // 새 시스템에서는 DoRandering()이 자동으로 자식 Tilemap을 생성하므로 Grid만 준비
    private MapRandering SetupRenderer(CreateMap cm)
    {
        var mr = new MapRandering();
        mr.Construct(cm);
        return mr;
    }

    // ── 헬퍼: 테스트 후 씬 정리 ──
    private void Cleanup()
    {
        var mapRoot = GameObject.Find("MapRoot_Grid");
        if (mapRoot != null) Object.DestroyImmediate(mapRoot);
    }

    // ====================================================================
    // ① Floor 배열 생성 검증: 4개 Floor (F0~F3)
    // ====================================================================
    [Test]
    public void InitMap_CreatesFloorArrays()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        Assert.IsNotNull(cm.map.floors, "map.floors가 null입니다.");
        Assert.AreEqual(4, cm.map.floors.Length, "Floor 수가 4가 아닙니다.");

        // F1: 7×7
        Assert.AreEqual(7, cm.map.floors[1].chunks.GetLength(0), "F1 X축 청크 수가 7이 아닙니다.");
        Assert.AreEqual(7, cm.map.floors[1].chunks.GetLength(1), "F1 Y축 청크 수가 7이 아닙니다.");

        // F2: 8×8
        Assert.AreEqual(8, cm.map.floors[2].chunks.GetLength(0), "F2 X축 청크 수가 8이 아닙니다.");
        Assert.AreEqual(8, cm.map.floors[2].chunks.GetLength(1), "F2 Y축 청크 수가 8이 아닙니다.");

        // F3: 9×9
        Assert.AreEqual(9, cm.map.floors[3].chunks.GetLength(0), "F3 X축 청크 수가 9이 아닙니다.");
        Assert.AreEqual(9, cm.map.floors[3].chunks.GetLength(1), "F3 Y축 청크 수가 9이 아닙니다.");

        Cleanup();
    }

    // ====================================================================
    // ② 모든 청크에 8x8 타일 배열 할당 검증 (Floor 1 기준)
    // ====================================================================
    [Test]
    public void AllChunks_Have8x8TileArray()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    Assert.IsNotNull(c.chunk, $"F{f} chunk[{x},{y}].chunk이 null입니다.");
                    Assert.AreEqual(8, c.chunk.GetLength(0), $"F{f} chunk[{x},{y}] X축 타일 수가 8이 아닙니다.");
                    Assert.AreEqual(8, c.chunk.GetLength(1), $"F{f} chunk[{x},{y}] Y축 타일 수가 8이 아닙니다.");
                }
            }
        }

        Cleanup();
    }

    // ====================================================================
    // ③ 시작방이 존재하는지 검증 (각 Floor)
    // ====================================================================
    [Test]
    public void StartRoom_ExistsOnEachFloor()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            bool hasStartRoom = false;
            for (int x = 0; x < w && !hasStartRoom; x++)
                for (int y = 0; y < h && !hasStartRoom; y++)
                    if (floor.chunks[x, y].roomRole == RoomRole.StartRoom)
                        hasStartRoom = true;

            Assert.IsTrue(hasStartRoom, $"Floor {f}에 StartRoom이 없습니다.");
        }

        Cleanup();
    }

    // ====================================================================
    // ④ 타일 이름 검증: 가장자리=Wall, 내부=Floor (Floor 1 기준)
    // ====================================================================
    [Test]
    public void TileNames_EdgeIsWall_InteriorIsFloor()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // Floor 1의 시작방 청크를 찾아서 검증
        Floor floor = cm.map.floors[1];
        int w = floor.config.width;
        int h = floor.config.height;

        // 시작방 청크: roomRole == StartRoom
        int sx = -1, sy = -1;
        for (int x = 0; x < w && sx < 0; x++)
            for (int y = 0; y < h && sx < 0; y++)
                if (floor.chunks[x, y].roomRole == RoomRole.StartRoom)
                { sx = x; sy = y; }

        Assert.IsTrue(sx >= 0 && sy >= 0, "시작방을 찾을 수 없습니다.");

        Chunks c = floor.chunks[sx, sy];

        Assert.AreEqual("Wall", c.chunk[0, 0].name, "좌하단 꼭짓점이 Wall이 아닙니다.");
        Assert.AreEqual("Wall", c.chunk[7, 7].name, "우상단 꼭짓점이 Wall이 아닙니다.");
        Assert.AreEqual("Floor", c.chunk[2, 2].name, "내부 타일(2,2)이 Floor가 아닙니다.");
        Assert.AreEqual("Floor", c.chunk[5, 5].name, "내부 타일(5,5)이 Floor가 아닙니다.");

        Cleanup();
    }

    // ====================================================================
    // ⑤ 같은 방 내부 청크 간 벽 허물기 검증 (Floor 1)
    // ====================================================================
    [Test]
    public void InternalWalls_OpenedBetweenSameRoomChunks()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        Floor floor = cm.map.floors[1];
        int w = floor.config.width;
        int h = floor.config.height;
        int openedCount = 0;

        // 수평 내부 벽 검증
        for (int x = 0; x < w - 1; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int idA = floor.chunks[x, y].roomId;
                int idB = floor.chunks[x + 1, y].roomId;

                if (idA >= 0 && idA == idB)
                {
                    Chunks cA = floor.chunks[x, y];
                    Chunks cB = floor.chunks[x + 1, y];

                    for (int ty = 0; ty < 8; ty++)
                    {
                        Assert.AreEqual("Floor", cA.chunk[7, ty].name,
                            $"내부벽 미허물: F1 chunk[{x},{y}] tx=7, ty={ty}");
                        Assert.AreEqual("Floor", cB.chunk[0, ty].name,
                            $"내부벽 미허물: F1 chunk[{x + 1},{y}] tx=0, ty={ty}");
                    }
                    openedCount++;
                }
            }
        }

        // 수직 내부 벽 검증
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h - 1; y++)
            {
                int idA = floor.chunks[x, y].roomId;
                int idB = floor.chunks[x, y + 1].roomId;

                if (idA >= 0 && idA == idB)
                {
                    Chunks cA = floor.chunks[x, y];
                    Chunks cB = floor.chunks[x, y + 1];

                    for (int tx = 0; tx < 8; tx++)
                    {
                        Assert.AreEqual("Floor", cA.chunk[tx, 7].name,
                            $"내부벽 미허물: F1 chunk[{x},{y}] tx={tx}, ty=7");
                        Assert.AreEqual("Floor", cB.chunk[tx, 0].name,
                            $"내부벽 미허물: F1 chunk[{x},{y + 1}] tx={tx}, ty=0");
                    }
                    openedCount++;
                }
            }
        }

        Assert.Greater(openedCount, 0, "내부 벽이 허물어진 멀티청크 방이 하나도 없습니다.");

        Cleanup();
    }

    // ====================================================================
    // ⑥ 시드 재현성 검증: 같은 시드 → 같은 맵
    // ====================================================================
    [Test]
    public void SameSeed_ProducesSameMap()
    {
        var cm = SetupCreateMap(99999);
        cm.GenerateMap();

        Floor floor = cm.map.floors[1];
        int w = floor.config.width;
        int h = floor.config.height;

        int[,] snapshot = new int[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                snapshot[x, y] = floor.chunks[x, y].roomId;

        cm.seed = 99999;
        cm.GenerateMap();

        Floor floor2 = cm.map.floors[1];
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Assert.AreEqual(snapshot[x, y], floor2.chunks[x, y].roomId,
                    $"시드 재현 실패: F1 chunk[{x},{y}] roomId가 다릅니다.");
            }
        }

        Cleanup();
    }

    // ====================================================================
    // ⑦ 다른 시드 → 다른 맵 (최소 1개 청크 차이)
    // ====================================================================
    [Test]
    public void DifferentSeed_ProducesDifferentMap()
    {
        var cm = SetupCreateMap(11111);
        cm.GenerateMap();

        Floor floor = cm.map.floors[1];
        int w = floor.config.width;
        int h = floor.config.height;

        int[,] snapshot = new int[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                snapshot[x, y] = floor.chunks[x, y].roomId;

        cm.seed = 22222;
        cm.GenerateMap();

        Floor floor2 = cm.map.floors[1];
        bool hasDifference = false;
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (snapshot[x, y] != floor2.chunks[x, y].roomId)
                {
                    hasDifference = true;
                    break;
                }
            }
            if (hasDifference) break;
        }

        Assert.IsTrue(hasDifference, "다른 시드인데 맵이 완전히 동일합니다.");

        Cleanup();
    }

    // ====================================================================
    // ⑧ 방 간 통로 존재 검증 (Floor 1)
    // ====================================================================
    [Test]
    public void ConnectedRooms_HaveOpenPassage()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        Floor floor = cm.map.floors[1];
        int w = floor.config.width;
        int h = floor.config.height;
        int passageCount = 0;

        // 수평 경계
        for (int x = 0; x < w - 1; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int idA = floor.chunks[x, y].roomId;
                int idB = floor.chunks[x + 1, y].roomId;
                if (idA < 0 || idB < 0 || idA == idB) continue;

                Chunks cA = floor.chunks[x, y];
                Chunks cB = floor.chunks[x + 1, y];

                for (int ty = 3; ty <= 4; ty++)
                {
                    if (cA.chunk[7, ty].name == "Floor" && cB.chunk[0, ty].name == "Floor")
                    {
                        passageCount++;
                        break;
                    }
                }
            }
        }

        // 수직 경계
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h - 1; y++)
            {
                int idA = floor.chunks[x, y].roomId;
                int idB = floor.chunks[x, y + 1].roomId;
                if (idA < 0 || idB < 0 || idA == idB) continue;

                Chunks cA = floor.chunks[x, y];
                Chunks cB = floor.chunks[x, y + 1];

                for (int tx = 3; tx <= 4; tx++)
                {
                    if (cA.chunk[tx, 7].name == "Floor" && cB.chunk[tx, 0].name == "Floor")
                    {
                        passageCount++;
                        break;
                    }
                }
            }
        }

        Assert.Greater(passageCount, 0, "방 사이 통로가 하나도 없습니다.");

        Cleanup();
    }

    // ====================================================================
    // ⑨ MapRandering: RenderAllFloors 후 각 Floor Tilemap에 타일 배치 검증
    // ====================================================================
    [Test]
    public void MapRandering_PlacesTilesOnTilemap()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        var mr = SetupRenderer(cm);
        mr.DoRandering();

        // 4개 Floor Tilemap이 생성되었는지 확인
        Assert.IsNotNull(mr.floorTilemaps, "floorTilemaps가 null입니다.");
        Assert.AreEqual(cm.map.floors.Length, mr.floorTilemaps.Length,
            "Floor 수와 Tilemap 수가 일치하지 않습니다.");

        // 각 Floor Tilemap에 타일이 배치되었는지 확인
        for (int f = 0; f < mr.floorTilemaps.Length; f++)
        {
            Tilemap tilemap = mr.floorTilemaps[f];
            Assert.IsNotNull(tilemap, $"F{f} Tilemap이 null입니다.");

            int tileCount = 0;
            BoundsInt bounds = tilemap.cellBounds;
            foreach (var pos in bounds.allPositionsWithin)
            {
                if (tilemap.HasTile(pos))
                    tileCount++;
            }

            Assert.Greater(tileCount, 0, $"F{f} Tilemap에 타일이 하나도 배치되지 않았습니다.");
        }

        Cleanup();
    }

    // ====================================================================
    // ⑩ MapRandering: 참조 누락 시 에러 없이 안전하게 반환되는지 검증
    // ====================================================================
    [Test]
    public void MapRandering_ReturnsGracefully_WhenReferenceMissing()
    {
        var mr = new MapRandering();
        // createMap 의도적으로 미연결

        Assert.DoesNotThrow(() => mr.RenderAllFloors(),
            "참조 미연결 시 RenderAllFloors에서 예외가 발생했습니다.");

        Cleanup();
    }

    // ====================================================================
    // ⑪ Floor별 독립 크기 검증
    // ====================================================================
    [Test]
    public void Floors_HaveCorrectSizes()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        Assert.AreEqual(3, cm.map.floors[0].config.width, "F0 width");
        Assert.AreEqual(3, cm.map.floors[0].config.height, "F0 height");
        Assert.AreEqual(7, cm.map.floors[1].config.width, "F1 width");
        Assert.AreEqual(7, cm.map.floors[1].config.height, "F1 height");
        Assert.AreEqual(8, cm.map.floors[2].config.width, "F2 width");
        Assert.AreEqual(8, cm.map.floors[2].config.height, "F2 height");
        Assert.AreEqual(9, cm.map.floors[3].config.width, "F3 width");
        Assert.AreEqual(9, cm.map.floors[3].config.height, "F3 height");

        Cleanup();
    }

    // ====================================================================
    // ⑫ FloorId가 청크에 올바르게 할당되었는지 검증
    // ====================================================================
    [Test]
    public void Chunks_HaveCorrectFloorId()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 0; f < cm.map.floors.Length; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Assert.AreEqual((int)floor.config.floorId, floor.chunks[x, y].floorId,
                        $"F{f} chunk[{x},{y}]의 floorId가 올바르지 않습니다.");
                }
            }
        }

        Cleanup();
    }

    // ====================================================================
    // ⑬ RoomRole 할당 검증: 각 Floor에 Boss/SubPurpose/Normal/Start 존재
    // ====================================================================
    [Test]
    public void RoomRoles_AssignedOnEachFloor()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            bool hasStart = false, hasBoss = false, hasNormal = false, hasSub = false;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    switch (floor.chunks[x, y].roomRole)
                    {
                        case RoomRole.StartRoom:      hasStart = true; break;
                        case RoomRole.BossRoom:       hasBoss = true; break;
                        case RoomRole.NormalRoom:      hasNormal = true; break;
                        case RoomRole.SubPurposeRoom: hasSub = true; break;
                    }
                }
            }

            Assert.IsTrue(hasStart, $"Floor {f}에 StartRoom이 없습니다.");
            Assert.IsTrue(hasBoss, $"Floor {f}에 BossRoom이 없습니다.");
            Assert.IsTrue(hasNormal, $"Floor {f}에 NormalRoom이 없습니다.");
            // SubPurpose는 F1에서 1개이므로 존재해야 함
            Assert.IsTrue(hasSub, $"Floor {f}에 SubPurposeRoom이 없습니다.");
        }

        Cleanup();
    }

    // ====================================================================
    // ⑭ BossRoom이 StartRoom에서 가장 먼 방인지 검증 (Floor 1)
    // ====================================================================
    [Test]
    public void BossRoom_IsFarthestFromStart()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        Floor floor = cm.map.floors[1];
        int w = floor.config.width;
        int h = floor.config.height;

        // StartRoom과 BossRoom의 청크 위치를 찾음
        Vector2Int startPos = new Vector2Int(-1, -1);
        Vector2Int bossPos = new Vector2Int(-1, -1);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (floor.chunks[x, y].roomRole == RoomRole.StartRoom && startPos.x < 0)
                    startPos = new Vector2Int(x, y);
                if (floor.chunks[x, y].roomRole == RoomRole.BossRoom && bossPos.x < 0)
                    bossPos = new Vector2Int(x, y);
            }
        }

        Assert.IsTrue(startPos.x >= 0, "StartRoom을 찾을 수 없습니다.");
        Assert.IsTrue(bossPos.x >= 0, "BossRoom을 찾을 수 없습니다.");

        // BossRoom은 StartRoom과 다른 위치에 있어야 함
        Assert.AreNotEqual(startPos, bossPos, "BossRoom과 StartRoom이 같은 위치입니다.");

        Cleanup();
    }

    // ====================================================================
    // ⑮ 외곽 벽 두께 검증: 외곽 청크에 wallThickness 적용 확인
    // ====================================================================
    [Test]
    public void OuterWallThickness_Applied()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        Floor floor = cm.map.floors[1];
        int w = floor.config.width;
        int h = floor.config.height;
        int thkMin = floor.config.wallThicknessMin;

        bool foundThickWall = false;

        // 하단 경계(cy=0)의 방 있는 청크 검사
        for (int cx = 0; cx < w; cx++)
        {
            Chunks c = floor.chunks[cx, 0];
            if (c.roomId == -1) continue;

            // ty=0 ~ thkMin-1 라인은 반드시 Wall
            for (int tx = 0; tx < 8; tx++)
            {
                for (int ty = 0; ty < thkMin; ty++)
                {
                    Assert.AreEqual("Wall", c.chunk[tx, ty].name,
                        $"외곽 벽 두께 미적용: F1 chunk[{cx},0] tile[{tx},{ty}]");
                }
            }
            foundThickWall = true;
        }

        Assert.IsTrue(foundThickWall, "외곽 경계에 방이 있는 청크가 없습니다.");

        Cleanup();
    }

    // ====================================================================
    // ⑯ 보스방 형태(bossRoomFormat) 배치 검증: 청크 수 일치
    // ====================================================================
    [Test]
    public void BossRoom_HasCorrectChunkCount()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // Floor 1: "2x2" → 4청크
        AssertBossChunkCount(cm.map.floors[1], 4, "F1 (2x2)");

        // Floor 2: "ㄷ7" → 7청크
        AssertBossChunkCount(cm.map.floors[2], 7, "F2 (ㄷ7)");

        // Floor 3: "3x3" → 9청크
        AssertBossChunkCount(cm.map.floors[3], 9, "F3 (3x3)");

        Cleanup();
    }

    private void AssertBossChunkCount(Floor floor, int expectedCount, string label)
    {
        int w = floor.config.width;
        int h = floor.config.height;

        int bossChunkCount = 0;
        int bossRoomId = -1;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (floor.chunks[x, y].roomRole == RoomRole.BossRoom)
                {
                    bossChunkCount++;
                    if (bossRoomId == -1)
                        bossRoomId = floor.chunks[x, y].roomId;
                    else
                        Assert.AreEqual(bossRoomId, floor.chunks[x, y].roomId,
                            $"{label}: 보스방 청크의 roomId가 일치하지 않습니다.");
                }
            }
        }

        Assert.AreEqual(expectedCount, bossChunkCount,
            $"{label}: 보스방 청크 수가 {expectedCount}이 아닙니다 (실제: {bossChunkCount}).");
    }

    // ====================================================================
    // ⑰ 보스방이 시작방과 겹치지 않는지 검증
    // ====================================================================
    [Test]
    public void BossRoom_DoesNotOverlapStartRoom()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    // 같은 청크가 StartRoom이면서 BossRoom일 수 없음
                    if (c.roomRole == RoomRole.BossRoom)
                    {
                        Assert.AreNotEqual(RoomRole.StartRoom, c.roomRole,
                            $"Floor {f} chunk[{x},{y}]이 StartRoom이면서 BossRoom입니다.");
                    }
                }
            }
        }

        Cleanup();
    }

    // ====================================================================
    // ⑱ Floor 0 로비 생성 검증: 3×3 전체가 하나의 방
    // ====================================================================
    [Test]
    public void Floor0_IsFullLobby()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        Floor floor = cm.map.floors[0];
        int w = floor.config.width;
        int h = floor.config.height;

        Assert.AreEqual(3, w, "F0 width가 3이 아닙니다.");
        Assert.AreEqual(3, h, "F0 height가 3이 아닙니다.");

        // 전체 청크가 같은 roomId를 가져야 함
        int lobbyId = floor.chunks[0, 0].roomId;
        Assert.IsTrue(lobbyId >= 0, "F0 로비 roomId가 유효하지 않습니다.");

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Assert.AreEqual(lobbyId, floor.chunks[x, y].roomId,
                    $"F0 chunk[{x},{y}]의 roomId가 로비({lobbyId})와 다릅니다.");
                Assert.AreEqual(RoomRole.StartRoom, floor.chunks[x, y].roomRole,
                    $"F0 chunk[{x},{y}]의 roomRole이 StartRoom이 아닙니다.");
            }
        }

        Cleanup();
    }

    // ====================================================================
    // ⑲ F0에 F1로의 계단만 존재하는지 검증 (E: 순차 계단 구조)
    // ====================================================================
    [Test]
    public void Floor0_HasStairToFloor1Only()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        Floor floor = cm.map.floors[0];
        int w = floor.config.width;
        int h = floor.config.height;

        var foundTargets = new HashSet<int>();

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int target = floor.chunks[x, y].stairTargetFloor;
                if (target >= 0)
                    foundTargets.Add(target);
            }
        }

        Assert.IsTrue(foundTargets.Contains(1), "F0에 F1로의 계단이 없습니다.");
        Assert.IsFalse(foundTargets.Contains(2), "F0에 F2로의 직접 계단이 있습니다. (순차 구조 위반)");
        Assert.IsFalse(foundTargets.Contains(3), "F0에 F3로의 직접 계단이 있습니다. (순차 구조 위반)");

        Cleanup();
    }

    // ====================================================================
    // ⑳ 각 층(1~3) 시작방에 F0 귀환 계단이 존재하는지 검증
    // ====================================================================
    [Test]
    public void EachFloor_HasReturnStairToFloor0()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            bool hasReturnStair = false;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    if (c.stairTargetFloor == 0)
                    {
                        hasReturnStair = true;
                        // 시작방에 있어야 함
                        Assert.AreEqual(RoomRole.StartRoom, c.roomRole,
                            $"Floor {f}: F0 귀환 계단이 시작방이 아닌 chunk[{x},{y}]에 있습니다.");
                    }
                }
            }

            Assert.IsTrue(hasReturnStair, $"Floor {f}에 F0 귀환 계단이 없습니다.");
        }

        Cleanup();
    }

    // ====================================================================
    // ㉑ 계단 타일이 실제로 "Stair" 이름을 가지는지 검증
    // ====================================================================
    [Test]
    public void StairTiles_HaveStairName()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // F0의 계단 청크에서 Stair 타일 존재 확인
        Floor floor = cm.map.floors[0];
        int w = floor.config.width;
        int h = floor.config.height;

        bool foundStairTile = false;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (floor.chunks[x, y].stairTargetFloor >= 0)
                {
                    Chunks c = floor.chunks[x, y];
                    // 중앙 2×2 (3~4, 3~4)가 Stair여야 함
                    for (int tx = 3; tx <= 4; tx++)
                    {
                        for (int ty = 3; ty <= 4; ty++)
                        {
                            Assert.AreEqual("Stair", c.chunk[tx, ty].name,
                                $"F0 chunk[{x},{y}] tile[{tx},{ty}]이 Stair가 아닙니다.");
                            foundStairTile = true;
                        }
                    }
                }
            }
        }

        Assert.IsTrue(foundStairTile, "F0에서 Stair 타일을 찾을 수 없습니다.");

        Cleanup();
    }

    // ====================================================================
    // ㉒ OccupationState 초기화 검증: StartRoom만 PlayerControlled, 나머지 Neutral
    // ====================================================================
    [Test]
    public void OccupationState_MatchesRoomRole()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    switch (c.roomRole)
                    {
                        case RoomRole.StartRoom:
                            Assert.AreEqual(OccupationState.PlayerControlled, c.occupationState,
                                $"F{f} chunk[{x},{y}] StartRoom의 OccupationState가 PlayerControlled가 아닙니다.");
                            break;
                        case RoomRole.BossRoom:
                            Assert.AreEqual(OccupationState.Neutral, c.occupationState,
                                $"F{f} chunk[{x},{y}] BossRoom의 OccupationState가 Neutral이 아닙니다.");
                            break;
                        case RoomRole.SubPurposeRoom:
                            Assert.AreEqual(OccupationState.Neutral, c.occupationState,
                                $"F{f} chunk[{x},{y}] SubPurposeRoom의 OccupationState가 Neutral이 아닙니다.");
                            break;
                        case RoomRole.NormalRoom:
                            Assert.AreEqual(OccupationState.Neutral, c.occupationState,
                                $"F{f} chunk[{x},{y}] NormalRoom의 OccupationState가 Neutral이 아닙니다.");
                            break;
                    }
                }
            }
        }

        Cleanup();
    }

    // ====================================================================
    // ㉓ 위험도(dangerous) 검증: 시작방=0, 나머지>0
    // ====================================================================
    [Test]
    public void Dangerous_ZeroAtStartRoom_PositiveElsewhere()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            bool hasPositiveDanger = false;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    if (c.chunk == null || c.roomId < 0) continue;

                    // 내부 타일(2,2) 기준 검사
                    int danger = c.chunk[2, 2].dangerous;

                    if (c.roomRole == RoomRole.StartRoom)
                    {
                        Assert.AreEqual(0, danger,
                            $"F{f} StartRoom chunk[{x},{y}]의 위험도가 0이 아닙니다. ({danger})");
                    }
                    else if (c.roomRole != RoomRole.None)
                    {
                        Assert.GreaterOrEqual(danger, 0,
                            $"F{f} chunk[{x},{y}]의 위험도가 음수입니다.");
                        if (danger > 0) hasPositiveDanger = true;
                    }
                }
            }

            Assert.IsTrue(hasPositiveDanger,
                $"Floor {f}에 위험도가 0보다 큰 방이 하나도 없습니다.");
        }

        Cleanup();
    }

    // ====================================================================
    // ㉔ 이해도(understand) 검증: 시작방=100, 나머지=0
    // ====================================================================
    [Test]
    public void Understand_FullAtStartRoom_ZeroElsewhere()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    if (c.chunk == null || c.roomId < 0) continue;

                    int understand = c.chunk[2, 2].understand;

                    if (c.roomRole == RoomRole.StartRoom)
                    {
                        Assert.AreEqual(100, understand,
                            $"F{f} StartRoom chunk[{x},{y}]의 이해도가 100이 아닙니다. ({understand})");
                    }
                    else if (c.roomRole != RoomRole.None)
                    {
                        Assert.AreEqual(0, understand,
                            $"F{f} chunk[{x},{y}] (role={c.roomRole})의 이해도가 0이 아닙니다. ({understand})");
                    }
                }
            }
        }

        Cleanup();
    }

    // ====================================================================
    // ㉕ 위험도 거리 단조 증가 검증: BossRoom이 가장 높은 위험도 (Floor 1)
    // ====================================================================
    [Test]
    public void Dangerous_BossRoom_HasHighestDanger()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        Floor floor = cm.map.floors[1];
        int w = floor.config.width;
        int h = floor.config.height;

        int bossDanger = -1;
        int maxNonBossDanger = -1;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.chunk == null || c.roomId < 0) continue;

                int danger = c.chunk[2, 2].dangerous;

                if (c.roomRole == RoomRole.BossRoom)
                {
                    if (danger > bossDanger) bossDanger = danger;
                }
                else if (c.roomRole != RoomRole.None)
                {
                    if (danger > maxNonBossDanger) maxNonBossDanger = danger;
                }
            }
        }

        Assert.IsTrue(bossDanger >= 0, "BossRoom을 찾을 수 없습니다.");
        Assert.GreaterOrEqual(bossDanger, maxNonBossDanger,
            $"BossRoom 위험도({bossDanger})가 다른 방의 최대 위험도({maxNonBossDanger})보다 낮습니다.");

        Cleanup();
    }

    // ====================================================================
    // ㉖ Wall weight=-1, Floor weight>=1 검증
    // ====================================================================
    [Test]
    public void Weight_WallNegative_FloorPositive()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    if (c.chunk == null) continue;

                    for (int tx = 0; tx < 8; tx++)
                    {
                        for (int ty = 0; ty < 8; ty++)
                        {
                            Tile t = c.chunk[tx, ty];
                            if (t.name == "Wall")
                            {
                                Assert.AreEqual(-1, t.weight,
                                    $"F{f} chunk[{x},{y}] tile[{tx},{ty}] Wall의 weight가 -1이 아닙니다. ({t.weight})");
                            }
                            else if (t.name == "Floor" || t.name == "Stair")
                            {
                                Assert.GreaterOrEqual(t.weight, 1,
                                    $"F{f} chunk[{x},{y}] tile[{tx},{ty}] {t.name}의 weight가 1 미만입니다. ({t.weight})");
                            }
                        }
                    }
                }
            }
        }

        Cleanup();
    }

    // ====================================================================
    // ㉗ BossRoom 내부 Floor 타일: weight=2 (높은 이동 비용)
    // ====================================================================
    [Test]
    public void Weight_BossRoomFloor_IsTwo()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            bool foundBossFloor = false;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    if (c.roomRole != RoomRole.BossRoom || c.chunk == null) continue;

                    for (int tx = 0; tx < 8; tx++)
                    {
                        for (int ty = 0; ty < 8; ty++)
                        {
                            Tile t = c.chunk[tx, ty];
                            if (t.name == "Floor")
                            {
                                Assert.AreEqual(2, t.weight,
                                    $"F{f} BossRoom chunk[{x},{y}] tile[{tx},{ty}] Floor의 weight가 2가 아닙니다. ({t.weight})");
                                foundBossFloor = true;
                            }
                        }
                    }
                }
            }

            Assert.IsTrue(foundBossFloor, $"Floor {f}에 BossRoom Floor 타일이 없습니다.");
        }

        Cleanup();
    }

    // ====================================================================
    // ㉘ BossRoom 가시성 보정: 비Wall 타일 visibility=50
    // ====================================================================
    [Test]
    public void Visibility_BossRoom_IsDimmed()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        Floor floor = cm.map.floors[1];
        int w = floor.config.width;
        int h = floor.config.height;

        bool foundDimmed = false;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.roomRole != RoomRole.BossRoom || c.chunk == null) continue;

                for (int tx = 0; tx < 8; tx++)
                {
                    for (int ty = 0; ty < 8; ty++)
                    {
                        Tile t = c.chunk[tx, ty];
                        if (t.name != "Wall")
                        {
                            Assert.AreEqual(50, t.visibility,
                                $"BossRoom chunk[{x},{y}] tile[{tx},{ty}] ({t.name}) visibility가 50이 아닙니다. ({t.visibility})");
                            foundDimmed = true;
                        }
                    }
                }
            }
        }

        Assert.IsTrue(foundDimmed, "BossRoom에 비Wall 타일이 없습니다.");

        Cleanup();
    }

    // ====================================================================
    // ㉙ landform 할당 검증: RoomRole별 올바른 값
    // ====================================================================
    [Test]
    public void Landform_MatchesRoomRole()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    switch (c.roomRole)
                    {
                        case RoomRole.StartRoom:
                            Assert.AreEqual(0, c.landform,
                                $"F{f} chunk[{x},{y}] StartRoom의 landform이 0이 아닙니다. ({c.landform})");
                            break;
                        case RoomRole.NormalRoom:
                            Assert.IsTrue(c.landform >= 0 && c.landform <= 2,
                                $"F{f} chunk[{x},{y}] NormalRoom의 landform이 0~2 범위 밖입니다. ({c.landform})");
                            break;
                        case RoomRole.SubPurposeRoom:
                            Assert.AreEqual(3, c.landform,
                                $"F{f} chunk[{x},{y}] SubPurposeRoom의 landform이 3이 아닙니다. ({c.landform})");
                            break;
                        case RoomRole.BossRoom:
                            Assert.AreEqual(4, c.landform,
                                $"F{f} chunk[{x},{y}] BossRoom의 landform이 4이 아닙니다. ({c.landform})");
                            break;
                    }
                }
            }
        }

        Cleanup();
    }

    // ====================================================================
    // ㉚ NormalRoom 개수 제한 검증: FloorConfig.normalRoomCount 이하
    // ====================================================================
    [Test]
    public void NormalRoomCount_DoesNotExceedConfig()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;
            int expectedMin = floor.config.normalRoomCount;

            // 고유 NormalRoom roomId 수집
            var normalRoomIds = new HashSet<int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (floor.chunks[x, y].roomRole == RoomRole.NormalRoom)
                        normalRoomIds.Add(floor.chunks[x, y].roomId);

            Assert.GreaterOrEqual(normalRoomIds.Count, expectedMin,
                $"Floor {f}: NormalRoom 수({normalRoomIds.Count})가 설정값({expectedMin})보다 부족합니다.");
            Assert.Greater(normalRoomIds.Count, 0,
                $"Floor {f}: NormalRoom이 하나도 없습니다.");
        }

        Cleanup();
    }

    // ====================================================================
    // ㉛ 초과 방 제거 검증: RoomRole.None이면서 roomId>=0인 청크 없음
    // ====================================================================
    [Test]
    public void PrunedRooms_HaveNoOrphanChunks()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    if (c.roomRole == RoomRole.None)
                    {
                        Assert.AreEqual(-1, c.roomId,
                            $"Floor {f} chunk[{x},{y}]: RoomRole.None인데 roomId({c.roomId})가 -1이 아닙니다.");
                    }
                }
            }
        }

        Cleanup();
    }

    // ====================================================================
    // ㉜ 제거된 방 청크는 전체 Wall 타일인지 검증
    // ====================================================================
    [Test]
    public void PrunedChunks_AreAllWall()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    if (c.roomId == -1 && c.chunk != null)
                    {
                        for (int tx = 0; tx < 8; tx++)
                        {
                            for (int ty = 0; ty < 8; ty++)
                            {
                                Assert.AreEqual("Wall", c.chunk[tx, ty].name,
                                    $"Floor {f} 빈 chunk[{x},{y}] tile[{tx},{ty}]이 Wall이 아닙니다.");
                            }
                        }
                    }
                }
            }
        }

        Cleanup();
    }

    // ====================================================================
    // ㉝ 제거 후에도 모든 유효한 방이 연결되어 있는지 검증
    // ====================================================================
    [Test]
    public void AfterPrune_AllRoomsStillConnected()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            // 유효한 roomId 수집
            var validRoomIds = new HashSet<int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (floor.chunks[x, y].roomId >= 0)
                        validRoomIds.Add(floor.chunks[x, y].roomId);

            Assert.Greater(validRoomIds.Count, 1,
                $"Floor {f}: 유효한 방이 2개 미만입니다.");

            // BFS: 임의의 시작 roomId에서 통로(Floor 타일)를 통해 도달 가능 여부 확인
            // 타일 기반 flood fill
            bool[,] visited = new bool[w * 8, h * 8];
            int startWx = -1, startWy = -1;

            // 시작방의 Floor 타일 찾기
            for (int x = 0; x < w && startWx < 0; x++)
                for (int y = 0; y < h && startWx < 0; y++)
                    if (floor.chunks[x, y].roomRole == RoomRole.StartRoom)
                    {
                        startWx = x * 8 + 2;
                        startWy = y * 8 + 2;
                    }

            Assert.IsTrue(startWx >= 0, $"Floor {f}: 시작방을 찾을 수 없습니다.");

            // BFS flood fill
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue((startWx, startWy));
            visited[startWx, startWy] = true;

            int[] ddx = { 1, -1, 0, 0 };
            int[] ddy = { 0, 0, 1, -1 };

            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + ddx[d];
                    int ny = cy + ddy[d];
                    if (nx < 0 || nx >= w * 8 || ny < 0 || ny >= h * 8) continue;
                    if (visited[nx, ny]) continue;

                    int chunkX = nx / 8, chunkY = ny / 8;
                    int tileX = nx % 8, tileY = ny % 8;
                    Tile t = floor.chunks[chunkX, chunkY].chunk[tileX, tileY];
                    if (t.name != "Wall")
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }

            // 모든 유효한 방의 내부 타일(2,2)에 도달 가능한지 확인
            var reachedRoomIds = new HashSet<int>();
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (floor.chunks[x, y].roomId >= 0)
                    {
                        int wx = x * 8 + 2;
                        int wy = y * 8 + 2;
                        if (visited[wx, wy])
                            reachedRoomIds.Add(floor.chunks[x, y].roomId);
                    }
                }
            }

            foreach (int id in validRoomIds)
            {
                Assert.IsTrue(reachedRoomIds.Contains(id),
                    $"Floor {f}: roomId {id}에 시작방에서 도달할 수 없습니다.");
            }
        }

        Cleanup();
    }

    // ====================================================================
    // ㉞ 모든 청크가 방에 소속되어 있거나 빈칸(pruned)인지 검증
    // (roomId >= 0 && RoomRole != None) 또는 (roomId == -1 && RoomRole == None)
    // ====================================================================
    [Test]
    public void AllChunks_HaveConsistentRoomState()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    bool hasRoom = (c.roomId >= 0);
                    bool hasRole = (c.roomRole != RoomRole.None);

                    // 방이 있으면 역할도 있어야 하고, 없으면 둘 다 없어야 함
                    Assert.AreEqual(hasRoom, hasRole,
                        $"Floor {f} chunk[{x},{y}]: roomId={c.roomId}, roomRole={c.roomRole} 불일치");
                }
            }
        }

        Cleanup();
    }

    // ====================================================================
    // ㉟ 멀티청크 방이 존재하는지 검증 (PlaceRoomsOfSize 다중 배치 확인)
    // ====================================================================
    [Test]
    public void MultiChunkRooms_ExistOnEachFloor()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            // roomId별 청크 수 계산
            var roomChunkCount = new Dictionary<int, int>();
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    int id = floor.chunks[x, y].roomId;
                    if (id < 0) continue;
                    if (!roomChunkCount.ContainsKey(id))
                        roomChunkCount[id] = 0;
                    roomChunkCount[id]++;
                }
            }

            // 2청크 이상인 방이 최소 1개 존재 (보스방 제외)
            int multiChunkCount = 0;
            foreach (var kvp in roomChunkCount)
            {
                if (kvp.Value >= 2)
                    multiChunkCount++;
            }

            // 보스방은 항상 멀티청크이므로 최소 1개는 있어야 함
            Assert.Greater(multiChunkCount, 0,
                $"Floor {f}: 2청크 이상인 방이 하나도 없습니다.");
        }

        Cleanup();
    }

    // ====================================================================
    // ㊱ 방 배치 커버리지 검증: 유효한 방의 총 청크 수가 충분한지
    // ====================================================================
    [Test]
    public void RoomCoverage_SufficientOnEachFloor()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;
            int totalChunks = w * h;

            int roomChunks = 0;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (floor.chunks[x, y].roomId >= 0)
                        roomChunks++;

            // 방이 차지하는 비율이 최소 30% 이상이어야 함
            float ratio = (float)roomChunks / totalChunks;
            Assert.GreaterOrEqual(ratio, 0.3f,
                $"Floor {f}: 방 커버리지({ratio:P0})가 30% 미만입니다. ({roomChunks}/{totalChunks})");
        }

        Cleanup();
    }

    // ====================================================================
    // ㊲ 인접 1×1 병합 검증: 병합 후 2~3청크 방이 존재하는지
    // ====================================================================
    [Test]
    public void MergedSingleRooms_CreateSmallMultiChunkRooms()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // Floor 1 기준 검증
        Floor floor = cm.map.floors[1];
        int w = floor.config.width;
        int h = floor.config.height;

        // roomId별 청크 수 계산 (보스방/시작방 제외)
        var roomChunkCount = new Dictionary<int, int>();
        var roomRoles = new Dictionary<int, RoomRole>();
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Chunks c = floor.chunks[x, y];
                if (c.roomId < 0) continue;
                if (!roomChunkCount.ContainsKey(c.roomId))
                {
                    roomChunkCount[c.roomId] = 0;
                    roomRoles[c.roomId] = c.roomRole;
                }
                roomChunkCount[c.roomId]++;
            }
        }

        // 보스방/시작방 제외, 2~3청크 크기의 방이 있는지 확인
        int smallMultiCount = 0;
        foreach (var kvp in roomChunkCount)
        {
            RoomRole role = roomRoles[kvp.Key];
            if (role == RoomRole.BossRoom || role == RoomRole.StartRoom) continue;
            if (kvp.Value >= 2 && kvp.Value <= 3)
                smallMultiCount++;
        }

        // 병합이 작동했다면 2~3청크 방이 최소 1개 이상
        Assert.Greater(smallMultiCount, 0,
            "Floor 1: 병합된 2~3청크 크기의 방이 하나도 없습니다.");

        Cleanup();
    }

    // ====================================================================
    // ㊳ ValidateMap: 기본 시드에서 검증 통과 확인
    // ====================================================================
    [Test]
    public void ValidateMap_PassesWithDefaultSeed()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        Assert.IsTrue(cm.lastValidationPassed, "기본 시드에서 검증이 통과하지 않았습니다.");
        Assert.AreEqual(0, cm.lastValidationErrors.Count,
            $"검증 오류가 있습니다: {string.Join(", ", cm.lastValidationErrors)}");

        Cleanup();
    }

    // ====================================================================
    // ㊴ ValidateMap: 다양한 시드에서 검증 통과 (재시도 포함)
    // ====================================================================
    [Test]
    public void ValidateMap_PassesWithVariousSeeds()
    {
        int[] testSeeds = { 1, 42, 999, 7777, 14686, 54321 };

        foreach (int s in testSeeds)
        {
            var cm = SetupCreateMap(s);
            cm.maxRetryCount = 5;
            cm.GenerateMap();

            Assert.IsTrue(cm.lastValidationPassed,
                $"시드 {s}: 검증 실패. 오류: {string.Join("; ", cm.lastValidationErrors)}");

            Cleanup();
        }
    }

    // ====================================================================
    // ㊵ ValidateMap: 각 Floor에 시작방이 정확히 1개 존재
    // ====================================================================
    [Test]
    public void ValidateMap_EachFloorHasExactlyOneStartRoom()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            var startIds = new HashSet<int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (floor.chunks[x, y].roomRole == RoomRole.StartRoom)
                        startIds.Add(floor.chunks[x, y].roomId);

            Assert.AreEqual(1, startIds.Count,
                $"Floor {f}: 시작방 수가 1이 아닙니다. ({startIds.Count}개)");
        }

        Cleanup();
    }

    // ====================================================================
    // ㊶ ValidateMap: 각 Floor에 보스방이 정확히 1개 존재
    // ====================================================================
    [Test]
    public void ValidateMap_EachFloorHasExactlyOneBossRoom()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            var bossIds = new HashSet<int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (floor.chunks[x, y].roomRole == RoomRole.BossRoom)
                        bossIds.Add(floor.chunks[x, y].roomId);

            Assert.AreEqual(1, bossIds.Count,
                $"Floor {f}: 보스방 수가 1이 아닙니다. ({bossIds.Count}개)");
        }

        Cleanup();
    }

    // ====================================================================
    // ㊷ ValidateMap: roomId/roomRole 일관성 (고아 청크 없음)
    // ====================================================================
    [Test]
    public void ValidateMap_NoOrphanOrInconsistentChunks()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    // roomRole=None이면 roomId=-1이어야 함
                    if (c.roomRole == RoomRole.None)
                        Assert.AreEqual(-1, c.roomId,
                            $"F{f} chunk[{x},{y}]: 고아 청크 (role=None, roomId={c.roomId})");
                    // roomId>=0이면 roomRole!=None이어야 함
                    if (c.roomId >= 0)
                        Assert.AreNotEqual(RoomRole.None, c.roomRole,
                            $"F{f} chunk[{x},{y}]: 역할 없는 방 (roomId={c.roomId}, role=None)");
                }
            }
        }

        Cleanup();
    }

    // ====================================================================
    // ㊸ ValidateMap: lastRetryCount가 maxRetryCount 이하
    // ====================================================================
    [Test]
    public void ValidateMap_RetryCountWithinLimit()
    {
        var cm = SetupCreateMap();
        cm.maxRetryCount = 5;
        cm.GenerateMap();

        Assert.LessOrEqual(cm.lastRetryCount, cm.maxRetryCount,
            $"재시도 횟수({cm.lastRetryCount})가 최대({cm.maxRetryCount})를 초과했습니다.");
        Assert.IsTrue(cm.lastValidationPassed,
            "기본 시드에서 검증이 통과하지 않았습니다.");

        Cleanup();
    }

    // ====================================================================
    // ㊹ Gizmo: 기본 토글 필드 존재 및 기본값 확인
    // ====================================================================
    [Test]
    public void Gizmo_DefaultToggleFieldsAreTrue()
    {
        var cm = SetupCreateMap();

        Assert.IsTrue(cm.showGizmos, "showGizmos 기본값이 true가 아닙니다.");
        Assert.IsTrue(cm.gizmoShowRoomBounds, "gizmoShowRoomBounds 기본값이 true가 아닙니다.");
        Assert.IsTrue(cm.gizmoShowPassages, "gizmoShowPassages 기본값이 true가 아닙니다.");
        Assert.IsTrue(cm.gizmoShowStairs, "gizmoShowStairs 기본값이 true가 아닙니다.");
        Assert.IsTrue(cm.gizmoShowRoomLabels, "gizmoShowRoomLabels 기본값이 true가 아닙니다.");

        Cleanup();
    }

    // ====================================================================
    // ㊺ Gizmo: 토글 끄면 필드 반영 확인
    // ====================================================================
    [Test]
    public void Gizmo_TogglesCanBeDisabled()
    {
        var cm = SetupCreateMap();

        cm.showGizmos = false;
        cm.gizmoShowRoomBounds = false;
        cm.gizmoShowPassages = false;
        cm.gizmoShowStairs = false;
        cm.gizmoShowRoomLabels = false;

        Assert.IsFalse(cm.showGizmos);
        Assert.IsFalse(cm.gizmoShowRoomBounds);
        Assert.IsFalse(cm.gizmoShowPassages);
        Assert.IsFalse(cm.gizmoShowStairs);
        Assert.IsFalse(cm.gizmoShowRoomLabels);

        Cleanup();
    }

    // ====================================================================
    // ㊻ Gizmo: 맵 생성 후에도 Gizmo 필드 값 유지
    // ====================================================================
    [Test]
    public void Gizmo_FieldsSurviveMapGeneration()
    {
        var cm = SetupCreateMap();

        cm.showGizmos = true;
        cm.gizmoShowRoomBounds = false;
        cm.gizmoShowPassages = true;
        cm.gizmoShowStairs = false;
        cm.gizmoShowRoomLabels = true;

        cm.GenerateMap();

        Assert.IsTrue(cm.showGizmos, "GenerateMap 이후 showGizmos 값이 변경되었습니다.");
        Assert.IsFalse(cm.gizmoShowRoomBounds, "GenerateMap 이후 gizmoShowRoomBounds 값이 변경되었습니다.");
        Assert.IsTrue(cm.gizmoShowPassages, "GenerateMap 이후 gizmoShowPassages 값이 변경되었습니다.");
        Assert.IsFalse(cm.gizmoShowStairs, "GenerateMap 이후 gizmoShowStairs 값이 변경되었습니다.");
        Assert.IsTrue(cm.gizmoShowRoomLabels, "GenerateMap 이후 gizmoShowRoomLabels 값이 변경되었습니다.");

        Cleanup();
    }

    // ====================================================================
    // ㊼ Gizmo: GetCurrentFloor가 currentFloorIndex에 따라 올바른 Floor 반환
    // ====================================================================
    [Test]
    public void Gizmo_GetCurrentFloorMatchesIndex()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 0; f < cm.map.floors.Length; f++)
        {
            cm.currentFloorIndex = f;
            Floor floor = cm.GetCurrentFloor();

            Assert.AreEqual((int)cm.map.floors[f].config.floorId, (int)floor.config.floorId,
                $"currentFloorIndex={f}일 때 GetCurrentFloor가 올바른 Floor를 반환하지 않습니다.");
        }

        Cleanup();
    }

    // ====================================================================
    // ㊽ Serialize: SerializeMap이 비어있지 않은 JSON 문자열 반환
    // ====================================================================
    [Test]
    public void Serialize_ReturnsNonEmptyJson()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        string json = cm.SerializeMap();

        Assert.IsNotNull(json);
        Assert.IsTrue(json.Length > 100, $"직렬화된 JSON이 너무 짧습니다: {json.Length} bytes");
        Assert.IsTrue(json.Contains("floors"), "JSON에 'floors' 키가 없습니다.");

        Cleanup();
    }

    // ====================================================================
    // ㊾ Serialize: JSON 왕복(Roundtrip) — Floor 수 보존
    // ====================================================================
    [Test]
    public void Serialize_RoundtripPreservesFloorCount()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        int originalFloorCount = cm.map.floors.Length;
        string json = cm.SerializeMap();

        // 새 CreateMap에 역직렬화
        var cm2 = SetupCreateMap();
        cm2.DeserializeMap(json);

        Assert.AreEqual(originalFloorCount, cm2.map.floors.Length,
            "왕복 직렬화 후 Floor 수가 다릅니다.");

        Cleanup();
    }

    // ====================================================================
    // ㊿ Serialize: 왕복 후 FloorConfig 보존
    // ====================================================================
    [Test]
    public void Serialize_RoundtripPreservesFloorConfig()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        string json = cm.SerializeMap();
        var cm2 = SetupCreateMap();
        cm2.DeserializeMap(json);

        for (int f = 0; f < cm.map.floors.Length; f++)
        {
            FloorConfig orig = cm.map.floors[f].config;
            FloorConfig rest = cm2.map.floors[f].config;

            Assert.AreEqual((int)orig.floorId, (int)rest.floorId,
                $"F{f}: floorId 불일치");
            Assert.AreEqual(orig.width, rest.width,
                $"F{f}: width 불일치");
            Assert.AreEqual(orig.height, rest.height,
                $"F{f}: height 불일치");
            Assert.AreEqual(orig.normalRoomCount, rest.normalRoomCount,
                $"F{f}: normalRoomCount 불일치");
            Assert.AreEqual(orig.subPurposeRoomCount, rest.subPurposeRoomCount,
                $"F{f}: subPurposeRoomCount 불일치");
            Assert.AreEqual(orig.bossRoomFormat, rest.bossRoomFormat,
                $"F{f}: bossRoomFormat 불일치");
        }

        Cleanup();
    }

    // ====================================================================
    // 51 Serialize: 왕복 후 청크 roomId/roomRole/roomName 보존
    // ====================================================================
    [Test]
    public void Serialize_RoundtripPreservesChunkData()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        string json = cm.SerializeMap();
        var cm2 = SetupCreateMap();
        cm2.DeserializeMap(json);

        for (int f = 0; f < cm.map.floors.Length; f++)
        {
            Floor origFloor = cm.map.floors[f];
            Floor restFloor = cm2.map.floors[f];
            int w = origFloor.config.width;
            int h = origFloor.config.height;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Chunks origC = origFloor.chunks[x, y];
                    Chunks restC = restFloor.chunks[x, y];

                    Assert.AreEqual(origC.roomId, restC.roomId,
                        $"F{f} chunk[{x},{y}]: roomId 불일치");
                    Assert.AreEqual(origC.roomRole, restC.roomRole,
                        $"F{f} chunk[{x},{y}]: roomRole 불일치");
                    Assert.AreEqual(origC.roomName, restC.roomName,
                        $"F{f} chunk[{x},{y}]: roomName 불일치");
                    Assert.AreEqual(origC.occupationState, restC.occupationState,
                        $"F{f} chunk[{x},{y}]: occupationState 불일치");
                    Assert.AreEqual(origC.stairTargetFloor, restC.stairTargetFloor,
                        $"F{f} chunk[{x},{y}]: stairTargetFloor 불일치");
                    Assert.AreEqual(origC.landform, restC.landform,
                        $"F{f} chunk[{x},{y}]: landform 불일치");
                }
            }
        }

        Cleanup();
    }

    // ====================================================================
    // 52 Serialize: 왕복 후 타일 데이터 보존 (모든 타일 속성)
    // ====================================================================
    [Test]
    public void Serialize_RoundtripPreservesTileData()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        string json = cm.SerializeMap();
        var cm2 = SetupCreateMap();
        cm2.DeserializeMap(json);

        // F1의 모든 타일 검증 (대표 Floor)
        Floor origFloor = cm.map.floors[1];
        Floor restFloor = cm2.map.floors[1];
        int w = origFloor.config.width;
        int h = origFloor.config.height;

        for (int cx = 0; cx < w; cx++)
        {
            for (int cy = 0; cy < h; cy++)
            {
                Chunks origC = origFloor.chunks[cx, cy];
                Chunks restC = restFloor.chunks[cx, cy];

                if (origC.chunk == null)
                {
                    Assert.IsNull(restC.chunk,
                        $"F1 chunk[{cx},{cy}]: 원본은 null이지만 복원본은 null이 아닙니다.");
                    continue;
                }

                Assert.IsNotNull(restC.chunk,
                    $"F1 chunk[{cx},{cy}]: 원본은 유효하지만 복원본이 null입니다.");

                for (int tx = 0; tx < 8; tx++)
                {
                    for (int ty = 0; ty < 8; ty++)
                    {
                        Tile origT = origC.chunk[tx, ty];
                        Tile restT = restC.chunk[tx, ty];

                        Assert.AreEqual(origT.name, restT.name,
                            $"F1 [{cx},{cy}][{tx},{ty}]: name 불일치");
                        Assert.AreEqual(origT.weight, restT.weight,
                            $"F1 [{cx},{cy}][{tx},{ty}]: weight 불일치");
                        Assert.AreEqual(origT.visibility, restT.visibility,
                            $"F1 [{cx},{cy}][{tx},{ty}]: visibility 불일치");
                        Assert.AreEqual(origT.dangerous, restT.dangerous,
                            $"F1 [{cx},{cy}][{tx},{ty}]: dangerous 불일치");
                        Assert.AreEqual(origT.understand, restT.understand,
                            $"F1 [{cx},{cy}][{tx},{ty}]: understand 불일치");
                        Assert.AreEqual(origT.isObjectExist, restT.isObjectExist,
                            $"F1 [{cx},{cy}][{tx},{ty}]: isObjectExist 불일치");
                        Assert.AreEqual(origT.isStructureExist, restT.isStructureExist,
                            $"F1 [{cx},{cy}][{tx},{ty}]: isStructureExist 불일치");
                    }
                }
            }
        }

        Cleanup();
    }

    // ====================================================================
    // 53 Serialize: 파일 저장/로드 왕복 검증
    // ====================================================================
    [Test]
    public void Serialize_SaveAndLoadFileRoundtrip()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, "test_map_roundtrip.json");

        try
        {
            cm.SaveMapToFile(tempPath);

            Assert.IsTrue(System.IO.File.Exists(tempPath), "저장된 파일이 존재하지 않습니다.");

            string fileContent = System.IO.File.ReadAllText(tempPath);
            Assert.IsTrue(fileContent.Length > 100, "저장된 파일이 너무 짧습니다.");

            var cm2 = SetupCreateMap();
            cm2.LoadMapFromFile(tempPath);

            Assert.IsNotNull(cm2.map.floors, "로드된 맵의 floors가 null입니다.");
            Assert.AreEqual(cm.map.floors.Length, cm2.map.floors.Length,
                "파일 왕복 후 Floor 수가 다릅니다.");

            // 대표 데이터 검증: F2 시작방 roomRole
            int startCount = 0;
            Floor f2 = cm2.map.floors[2];
            for (int x = 0; x < f2.config.width; x++)
                for (int y = 0; y < f2.config.height; y++)
                    if (f2.chunks[x, y].roomRole == RoomRole.StartRoom)
                        startCount++;

            Assert.GreaterOrEqual(startCount, 1,
                "파일 로드 후 F2에 시작방이 없습니다.");

            Cleanup();
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }

    // ====================================================================
    // 54 Serialize: DeserializeMap이 floorConfigs도 동기화
    // ====================================================================
    [Test]
    public void Serialize_DeserializeSyncsFloorConfigs()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        string json = cm.SerializeMap();

        var cm2 = SetupCreateMap();
        cm2.DeserializeMap(json);

        Assert.IsNotNull(cm2.floorConfigs, "역직렬화 후 floorConfigs가 null입니다.");
        Assert.AreEqual(cm.map.floors.Length, cm2.floorConfigs.Length,
            "역직렬화 후 floorConfigs 길이가 floors와 다릅니다.");

        for (int f = 0; f < cm2.floorConfigs.Length; f++)
        {
            Assert.AreEqual((int)cm2.map.floors[f].config.floorId,
                (int)cm2.floorConfigs[f].floorId,
                $"F{f}: floorConfigs[{f}].floorId와 floors[{f}].config.floorId가 다릅니다.");
        }

        Cleanup();
    }

    // ====================================================================
    // 55 Serialize: 복원된 맵이 검증 통과
    // ====================================================================
    [Test]
    public void Serialize_RestoredMapPassesValidation()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();
        Assert.IsTrue(cm.lastValidationPassed, "원본 맵 검증 실패");

        string json = cm.SerializeMap();

        var cm2 = SetupCreateMap();
        cm2.DeserializeMap(json);

        var errors = cm2.ValidateMap();
        Assert.AreEqual(0, errors.Count,
            $"복원된 맵 검증 실패: {string.Join("; ", errors)}");

        Cleanup();
    }

    // ====================================================================
    // Gate 정합성: 모든 Gate의 roomA/B가 실제 존재하고 width > 0
    // ====================================================================
    [Test]
    public void Gates_AreValid()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            Assert.IsNotNull(floor.gates, $"F{f} gates가 null입니다.");
            Assert.IsTrue(floor.gates.Count > 0, $"F{f} Gate가 0개입니다.");

            var validRoomIds = new HashSet<int>();
            int w = floor.config.width;
            int h = floor.config.height;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (floor.chunks[x, y].roomId >= 0)
                        validRoomIds.Add(floor.chunks[x, y].roomId);

            foreach (Gate g in floor.gates)
            {
                Assert.IsTrue(validRoomIds.Contains(g.roomA), $"F{f} Gate roomA={g.roomA} 없음");
                Assert.IsTrue(validRoomIds.Contains(g.roomB), $"F{f} Gate roomB={g.roomB} 없음");
                Assert.IsTrue(g.width > 0, $"F{f} Gate width={g.width} 유효하지 않음");
            }
        }

        Cleanup();
    }

    // ====================================================================
    // allowMaxFootprint 범위 검증: 유효한 방의 값이 1~5 이내
    // ====================================================================
    [Test]
    public void AllowMaxFootprint_InRange()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    if (c.roomId >= 0)
                    {
                        Assert.IsTrue(c.allowMaxFootprint >= 1 && c.allowMaxFootprint <= 5,
                            $"F{f} chunk[{x},{y}] allowMaxFootprint={c.allowMaxFootprint} 범위 벗어남");
                    }
                }
        }

        Cleanup();
    }

    // ====================================================================
    // SubPurposeRoom 단일 입구: 각 서브목적방의 Gate ≤ 1개
    // ====================================================================
    [Test]
    public void SubPurposeRooms_HaveSingleEntry()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            var subIds = new HashSet<int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (floor.chunks[x, y].roomRole == RoomRole.SubPurposeRoom)
                        subIds.Add(floor.chunks[x, y].roomId);

            if (subIds.Count == 0 || floor.gates == null) continue;

            var subGateCount = new Dictionary<int, int>();
            foreach (int sid in subIds)
                subGateCount[sid] = 0;

            foreach (Gate g in floor.gates)
            {
                if (subGateCount.ContainsKey(g.roomA)) subGateCount[g.roomA]++;
                if (subGateCount.ContainsKey(g.roomB)) subGateCount[g.roomB]++;
            }

            foreach (var kvp in subGateCount)
                Assert.IsTrue(kvp.Value <= 1,
                    $"F{f} 서브목적방 roomId={kvp.Key} Gate 수={kvp.Value} (1 초과)");
        }

        Cleanup();
    }

    // ====================================================================
    // stairIsOpen: F0→F1/F1→F0만 개방, 나머지 잠금
    // ====================================================================
    [Test]
    public void StairIsOpen_CorrectDefaults()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // F0 계단 확인
        Floor f0 = cm.map.floors[0];
        int w0 = f0.config.width;
        int h0 = f0.config.height;

        for (int x = 0; x < w0; x++)
            for (int y = 0; y < h0; y++)
            {
                Chunks c = f0.chunks[x, y];
                if (c.stairTargetFloor >= 0)
                {
                    bool expected = (c.stairTargetFloor == 1);
                    Assert.AreEqual(expected, c.stairIsOpen,
                        $"F0 chunk[{x},{y}] → F{c.stairTargetFloor}: stairIsOpen={c.stairIsOpen} (기대:{expected})");
                }
            }

        // 각 층 → F0 귀환 계단 확인
        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    if (c.stairTargetFloor == 0)
                    {
                        bool expected = (f == 1);
                        Assert.AreEqual(expected, c.stairIsOpen,
                            $"F{f} chunk[{x},{y}] → F0: stairIsOpen={c.stairIsOpen} (기대:{expected})");
                    }
                }
        }

        Cleanup();
    }

    // ====================================================================
    // maxNormalRoomChunks: 일반방 청크 수가 상한 이하
    // ====================================================================
    [Test]
    public void NormalRooms_ChunkCountWithinLimit()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int maxChunks = floor.config.maxNormalRoomChunks;
            int w = floor.config.width;
            int h = floor.config.height;

            var roomChunkCount = new Dictionary<int, int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    if (c.roomId >= 0 && c.roomRole == RoomRole.NormalRoom)
                    {
                        if (!roomChunkCount.ContainsKey(c.roomId))
                            roomChunkCount[c.roomId] = 0;
                        roomChunkCount[c.roomId]++;
                    }
                }

            foreach (var kvp in roomChunkCount)
                Assert.IsTrue(kvp.Value <= maxChunks,
                    $"F{f} roomId={kvp.Key} 청크 수={kvp.Value} > 상한 {maxChunks}");
        }

        Cleanup();
    }

    // ====================================================================
    // 점령 API: ConquerRoom 후 OccupationState 변경 확인
    // ====================================================================
    [Test]
    public void ConquerRoom_ChangesOccupation()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // F1에서 NormalRoom 하나 찾기
        Floor f1 = cm.map.floors[1];
        int targetId = -1;
        for (int x = 0; x < f1.config.width; x++)
            for (int y = 0; y < f1.config.height; y++)
                if (f1.chunks[x, y].roomRole == RoomRole.NormalRoom && targetId < 0)
                    targetId = f1.chunks[x, y].roomId;

        Assert.IsTrue(targetId >= 0, "F1에 NormalRoom이 없습니다.");

        cm.ConquerRoom(1, targetId);

        // 점령 후 확인
        f1 = cm.map.floors[1];
        for (int x = 0; x < f1.config.width; x++)
            for (int y = 0; y < f1.config.height; y++)
                if (f1.chunks[x, y].roomId == targetId)
                {
                    Assert.AreEqual(OccupationState.PlayerControlled, f1.chunks[x, y].occupationState,
                        $"F1 chunk[{x},{y}] 점령 후 상태가 PlayerControlled이 아닙니다.");

                    // 이해도 100 확인
                    if (f1.chunks[x, y].chunk != null)
                        Assert.AreEqual(100, f1.chunks[x, y].chunk[2, 2].understand,
                            $"F1 chunk[{x},{y}] 점령 후 이해도가 100이 아닙니다.");
                }

        Cleanup();
    }

    // ====================================================================
    // 후퇴 API: RetreatFromRoom 후 OccupationState → Neutral 복원
    // ====================================================================
    [Test]
    public void RetreatFromRoom_RestoresNeutral()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // F1에서 NormalRoom 하나 찾아 점령 후 후퇴
        Floor f1 = cm.map.floors[1];
        int targetId = -1;
        for (int x = 0; x < f1.config.width; x++)
            for (int y = 0; y < f1.config.height; y++)
                if (f1.chunks[x, y].roomRole == RoomRole.NormalRoom && targetId < 0)
                    targetId = f1.chunks[x, y].roomId;

        Assert.IsTrue(targetId >= 0, "F1에 NormalRoom이 없습니다.");

        cm.ConquerRoom(1, targetId);
        cm.RetreatFromRoom(1, targetId);

        f1 = cm.map.floors[1];
        for (int x = 0; x < f1.config.width; x++)
            for (int y = 0; y < f1.config.height; y++)
                if (f1.chunks[x, y].roomId == targetId)
                    Assert.AreEqual(OccupationState.Neutral, f1.chunks[x, y].occupationState,
                        $"F1 chunk[{x},{y}] 후퇴 후 상태가 Neutral이 아닙니다.");

        Cleanup();
    }

    // ====================================================================
    // 계단 개방 API: OpenStair 후 stairIsOpen 변경 확인 (E: 보스방 계단)
    // ====================================================================
    [Test]
    public void OpenStair_ChangesStairIsOpen()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // E: F1 보스방→F2 계단은 기본 잠금
        Floor f1 = cm.map.floors[1];
        bool foundClosed = false;
        for (int x = 0; x < f1.config.width; x++)
            for (int y = 0; y < f1.config.height; y++)
                if (f1.chunks[x, y].stairTargetFloor == 2)
                {
                    Assert.IsFalse(f1.chunks[x, y].stairIsOpen, "F1→F2 보스방 계단이 이미 개방됨");
                    foundClosed = true;
                }
        Assert.IsTrue(foundClosed, "F1→F2 보스방 계단을 찾을 수 없음");

        cm.OpenStair(1, 2);

        f1 = cm.map.floors[1];
        for (int x = 0; x < f1.config.width; x++)
            for (int y = 0; y < f1.config.height; y++)
                if (f1.chunks[x, y].stairTargetFloor == 2)
                    Assert.IsTrue(f1.chunks[x, y].stairIsOpen, "OpenStair 후 F1→F2 보스방 계단이 여전히 잠김");

        Cleanup();
    }

    // ====================================================================
    // 직렬화: 새 필드(gates, allowMaxFootprint, stairIsOpen) 왕복 검증
    // ====================================================================
    [Test]
    public void Serialization_PreservesNewFields()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        string json = cm.SerializeMap();
        var cm2 = SetupCreateMap();
        cm2.DeserializeMap(json);

        for (int f = 1; f <= 3; f++)
        {
            Floor orig = cm.map.floors[f];
            Floor rest = cm2.map.floors[f];

            // gates 수 일치
            Assert.AreEqual(orig.gates.Count, rest.gates.Count,
                $"F{f} gates 수 불일치: {orig.gates.Count} vs {rest.gates.Count}");

            int w = orig.config.width;
            int h = orig.config.height;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    Assert.AreEqual(orig.chunks[x, y].allowMaxFootprint, rest.chunks[x, y].allowMaxFootprint,
                        $"F{f} chunk[{x},{y}] allowMaxFootprint 불일치");
                    Assert.AreEqual(orig.chunks[x, y].stairIsOpen, rest.chunks[x, y].stairIsOpen,
                        $"F{f} chunk[{x},{y}] stairIsOpen 불일치");
                }
        }

        Cleanup();
    }

    // ====================================================================
    // Footprint: Gate 폭이 인접 방 allowMaxFootprint 이상인지 검증
    // ====================================================================
    [Test]
    public void GateWidth_MatchesAllowMaxFootprint()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            foreach (Gate g in floor.gates)
            {
                int maxFpA = 1, maxFpB = 1;
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        int id = floor.chunks[x, y].roomId;
                        if (id == g.roomA) maxFpA = Mathf.Max(maxFpA, floor.chunks[x, y].allowMaxFootprint);
                        if (id == g.roomB) maxFpB = Mathf.Max(maxFpB, floor.chunks[x, y].allowMaxFootprint);
                    }

                int expected = Mathf.Max(maxFpA, maxFpB);
                Assert.GreaterOrEqual(g.width, expected,
                    $"F{f} Gate(room{g.roomA}↔room{g.roomB}): width={g.width} < allowMaxFootprint 기대값 {expected}");
            }
        }

        Cleanup();
    }

    // ====================================================================
    // Footprint: CanEntityPassGate 런타임 API 검증
    // ====================================================================
    [Test]
    public void CanEntityPassGate_ReturnsCorrectResult()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // 모든 Gate에 대해 width 이하는 통과, width 초과는 불가
        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            foreach (Gate g in floor.gates)
            {
                Assert.IsTrue(cm.CanEntityPassGate(1, g),
                    $"F{f} Gate(room{g.roomA}↔room{g.roomB}): Footprint 1이 통과 불가");
                Assert.IsTrue(cm.CanEntityPassGate(g.width, g),
                    $"F{f} Gate width={g.width}과 동일한 크기가 통과 불가");
                if (g.width < 5)
                    Assert.IsFalse(cm.CanEntityPassGate(g.width + 1, g),
                        $"F{f} Gate width={g.width}보다 큰 크기가 통과 가능");
            }
        }

        Cleanup();
    }

    // ====================================================================
    // Footprint: CanEntityEnterRoom 런타임 API 검증
    // ====================================================================
    [Test]
    public void CanEntityEnterRoom_ReturnsCorrectResult()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            // 모든 방은 Footprint 1로 진입 가능
            var roomIds = new HashSet<int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (floor.chunks[x, y].roomId >= 0)
                        roomIds.Add(floor.chunks[x, y].roomId);

            foreach (int rid in roomIds)
            {
                // Gate가 있는 방은 Footprint 1로 진입 가능해야 함
                bool hasGate = false;
                foreach (Gate g in floor.gates)
                    if (g.roomA == rid || g.roomB == rid) { hasGate = true; break; }

                if (hasGate)
                    Assert.IsTrue(cm.CanEntityEnterRoom(1, f, rid),
                        $"F{f} roomId={rid}: Footprint 1이 진입 불가");
            }
        }

        Cleanup();
    }

    // ====================================================================
    // Footprint: IsValidFootprint 헬퍼 검증
    // ====================================================================
    [Test]
    public void IsValidFootprint_CorrectRange()
    {
        Assert.IsFalse(CreateMap.IsValidFootprint(0), "0은 유효하지 않아야 함");
        Assert.IsTrue(CreateMap.IsValidFootprint(1), "1은 유효해야 함");
        Assert.IsTrue(CreateMap.IsValidFootprint(5), "5는 유효해야 함");
        Assert.IsFalse(CreateMap.IsValidFootprint(6), "6은 유효하지 않아야 함");
    }

    // ====================================================================
    // Footprint: 계단 청크의 AllowMaxFootprint 검증
    // F0 계단 + 귀환 계단 = 5, 보스방 내 순차 계단은 보스방 footprint 유지
    // ====================================================================
    [Test]
    public void StairChunks_HaveAllowMaxFootprint5()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // F0의 계단 청크: allowMaxFootprint = 5
        Floor f0 = cm.map.floors[0];
        for (int x = 0; x < f0.config.width; x++)
            for (int y = 0; y < f0.config.height; y++)
                if (f0.chunks[x, y].stairTargetFloor >= 0)
                    Assert.AreEqual(5, f0.chunks[x, y].allowMaxFootprint,
                        $"F0 stair chunk[{x},{y}] allowMaxFootprint가 5가 아닙니다.");

        // F1~F3의 귀환 계단(시작방, stairTargetFloor==0): allowMaxFootprint = 5
        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            for (int x = 0; x < floor.config.width; x++)
                for (int y = 0; y < floor.config.height; y++)
                    if (floor.chunks[x, y].stairTargetFloor == 0)
                        Assert.AreEqual(5, floor.chunks[x, y].allowMaxFootprint,
                            $"F{f} return stair chunk[{x},{y}] allowMaxFootprint가 5가 아닙니다.");
        }

        // E: 보스방 순차 계단 (stairTargetFloor > 0, BossRoom): 보스방 footprint 값 유지 (1~5 범위)
        for (int f = 1; f <= 2; f++)
        {
            Floor floor = cm.map.floors[f];
            int targetFloor = f + 1;
            for (int x = 0; x < floor.config.width; x++)
                for (int y = 0; y < floor.config.height; y++)
                    if (floor.chunks[x, y].stairTargetFloor == targetFloor)
                    {
                        Assert.AreEqual(RoomRole.BossRoom, floor.chunks[x, y].roomRole,
                            $"F{f} → F{targetFloor} 순차 계단이 보스방이 아닙니다.");
                        Assert.IsTrue(floor.chunks[x, y].allowMaxFootprint >= 1 && floor.chunks[x, y].allowMaxFootprint <= 5,
                            $"F{f} boss stair chunk[{x},{y}] allowMaxFootprint={floor.chunks[x, y].allowMaxFootprint} 범위 벗어남");
                    }
        }

        Cleanup();
    }

    // ====================================================================
    // Footprint: Gate 폭이 가변적으로 생성되는지 검증 (최소 2 이상)
    // ====================================================================
    [Test]
    public void GateWidth_IsAtLeast2()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            foreach (Gate g in floor.gates)
            {
                Assert.GreaterOrEqual(g.width, 2,
                    $"F{f} Gate(room{g.roomA}↔room{g.roomB}): width={g.width}가 최소 2 미만");
            }
        }

        Cleanup();
    }
    // ====================================================================
    // E: 보스방 순차 계단 존재 검증 (F1 보스방→F2, F2 보스방→F3)
    // ====================================================================
    [Test]
    public void BossRoom_HasSequentialStairs()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 2; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;
            int targetFloor = f + 1;
            bool foundBossStair = false;

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (floor.chunks[x, y].roomRole == RoomRole.BossRoom &&
                        floor.chunks[x, y].stairTargetFloor == targetFloor)
                        foundBossStair = true;

            Assert.IsTrue(foundBossStair,
                $"Floor {f}: 보스방에 F{targetFloor}로의 순차 계단이 없습니다.");
        }

        Cleanup();
    }

    // ====================================================================
    // E: 보스방 순차 계단 초기 잠금 상태 검증
    // ====================================================================
    [Test]
    public void BossRoomStairs_InitiallyLocked()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 2; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;
            int targetFloor = f + 1;

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (floor.chunks[x, y].roomRole == RoomRole.BossRoom &&
                        floor.chunks[x, y].stairTargetFloor == targetFloor)
                        Assert.IsFalse(floor.chunks[x, y].stairIsOpen,
                            $"F{f} boss stair chunk[{x},{y}] → F{targetFloor}: 초기 상태가 잠금이 아닙니다.");
        }

        Cleanup();
    }

    // ====================================================================
    // C: 서브목적방 1×1(1청크) 강제 검증
    // ====================================================================
    [Test]
    public void SubPurposeRooms_AreAlways1Chunk()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            var subChunkCount = new Dictionary<int, int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    if (c.roomRole == RoomRole.SubPurposeRoom && c.roomId >= 0)
                    {
                        if (!subChunkCount.ContainsKey(c.roomId))
                            subChunkCount[c.roomId] = 0;
                        subChunkCount[c.roomId]++;
                    }
                }

            foreach (var kvp in subChunkCount)
                Assert.AreEqual(1, kvp.Value,
                    $"F{f} SubPurposeRoom roomId={kvp.Key}: 청크 수={kvp.Value} (1이어야 함)");
        }

        Cleanup();
    }

    // ====================================================================
    // D: 시작방의 직접 연결이 NormalRoom에만 되어 있는지 검증
    // ====================================================================
    [Test]
    public void StartRoom_OnlyDirectlyConnectedToNormalRooms()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;

            // 시작방 roomId 찾기
            int startId = -1;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (floor.chunks[x, y].roomRole == RoomRole.StartRoom)
                    { startId = floor.chunks[x, y].roomId; break; }

            Assert.IsTrue(startId >= 0, $"F{f}: 시작방을 찾을 수 없습니다.");

            // roomId→RoomRole 맵
            var roleMap = new Dictionary<int, RoomRole>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    int id = floor.chunks[x, y].roomId;
                    if (id >= 0 && !roleMap.ContainsKey(id))
                        roleMap[id] = floor.chunks[x, y].roomRole;
                }

            // 시작방과 직접 연결된 Gate의 상대방 역할 확인
            if (floor.gates == null) continue;
            foreach (Gate g in floor.gates)
            {
                int otherId = -1;
                if (g.roomA == startId) otherId = g.roomB;
                else if (g.roomB == startId) otherId = g.roomA;
                else continue;

                if (roleMap.ContainsKey(otherId))
                {
                    Assert.AreEqual(RoomRole.NormalRoom, roleMap[otherId],
                        $"F{f}: 시작방이 NormalRoom이 아닌 {roleMap[otherId]}(roomId={otherId})과 직접 연결됨");
                }
            }
        }

        Cleanup();
    }

    // ====================================================================
    // H: F0→F1 계단이 인류 전용(stairHumanOnly=true)인지 검증
    // ====================================================================
    [Test]
    public void Floor0ToFloor1Stair_IsHumanOnly()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        Floor f0 = cm.map.floors[0];
        int w = f0.config.width;
        int h = f0.config.height;

        bool foundHumanOnly = false;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (f0.chunks[x, y].stairTargetFloor == 1)
                {
                    Assert.IsTrue(f0.chunks[x, y].stairHumanOnly,
                        $"F0 chunk[{x},{y}] → F1: stairHumanOnly가 true가 아닙니다.");
                    foundHumanOnly = true;
                }

        Assert.IsTrue(foundHumanOnly, "F0→F1 계단을 찾을 수 없습니다.");

        Cleanup();
    }

    // ====================================================================
    // H: stairHumanOnly 직렬화 왕복 검증
    // ====================================================================
    [Test]
    public void Serialization_PreservesStairHumanOnly()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        string json = cm.SerializeMap();
        var cm2 = SetupCreateMap();
        cm2.DeserializeMap(json);

        Floor origF0 = cm.map.floors[0];
        Floor restF0 = cm2.map.floors[0];
        int w = origF0.config.width;
        int h = origF0.config.height;

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                Assert.AreEqual(origF0.chunks[x, y].stairHumanOnly, restF0.chunks[x, y].stairHumanOnly,
                    $"F0 chunk[{x},{y}] stairHumanOnly 왕복 불일치");

        Cleanup();
    }

    // ====================================================================
    // G: FindRetreatTarget 검증 — 시작방에서 자신 이외의 안전 방 반환
    // ====================================================================
    [Test]
    public void FindRetreatTarget_ReturnsPlayerControlledOrStart()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // F1에서 NormalRoom 하나 찾기
        Floor f1 = cm.map.floors[1];
        int normalId = -1;
        for (int x = 0; x < f1.config.width; x++)
            for (int y = 0; y < f1.config.height; y++)
                if (f1.chunks[x, y].roomRole == RoomRole.NormalRoom && normalId < 0)
                    normalId = f1.chunks[x, y].roomId;

        Assert.IsTrue(normalId >= 0, "F1에 NormalRoom이 없습니다.");

        // 점령 후 후퇴 대상 찾기
        cm.ConquerRoom(1, normalId);

        // 시작방 roomId 찾기
        int startId = -1;
        f1 = cm.map.floors[1];
        for (int x = 0; x < f1.config.width; x++)
            for (int y = 0; y < f1.config.height; y++)
                if (f1.chunks[x, y].roomRole == RoomRole.StartRoom)
                { startId = f1.chunks[x, y].roomId; break; }

        // 다른 NormalRoom에서 후퇴 대상 찾기
        int otherNormal = -1;
        for (int x = 0; x < f1.config.width; x++)
            for (int y = 0; y < f1.config.height; y++)
                if (f1.chunks[x, y].roomRole == RoomRole.NormalRoom &&
                    f1.chunks[x, y].roomId != normalId && otherNormal < 0)
                    otherNormal = f1.chunks[x, y].roomId;

        if (otherNormal >= 0)
        {
            int retreatTarget = cm.FindRetreatTarget(1, otherNormal);
            Assert.IsTrue(retreatTarget >= 0,
                $"F1에서 roomId={otherNormal}의 후퇴 대상을 찾을 수 없습니다.");
        }

        Cleanup();
    }

    // ====================================================================
    // G: IsFloorOccupied 검증 — 보스방 점령 전/후
    // ====================================================================
    [Test]
    public void IsFloorOccupied_FalseByDefault_TrueAfterConquer()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // 기본: 보스방 미점령
        Assert.IsFalse(cm.IsFloorOccupied(1), "F1 보스방이 초기에 점령되어 있습니다.");

        // 보스방 roomId 찾기
        Floor f1 = cm.map.floors[1];
        int bossId = -1;
        for (int x = 0; x < f1.config.width; x++)
            for (int y = 0; y < f1.config.height; y++)
                if (f1.chunks[x, y].roomRole == RoomRole.BossRoom)
                { bossId = f1.chunks[x, y].roomId; break; }

        Assert.IsTrue(bossId >= 0, "F1 보스방을 찾을 수 없습니다.");

        cm.ConquerRoom(1, bossId);

        Assert.IsTrue(cm.IsFloorOccupied(1), "보스방 점령 후 IsFloorOccupied가 true가 아닙니다.");

        Cleanup();
    }

    // ====================================================================
    // G: CanReachFloor 검증 — F0→F1 가능, F0→F2 불가 (잠김)
    // ====================================================================
    [Test]
    public void CanReachFloor_RespectsStairOpenState()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // F0→F1: 개방 계단 존재
        Assert.IsTrue(cm.CanReachFloor(0, 1), "F0→F1 도달 불가 (개방 계단 존재해야 함)");

        // F0→F2: F1 보스방 계단 잠김 → 도달 불가
        Assert.IsFalse(cm.CanReachFloor(0, 2), "F0→F2 도달 가능 (보스방 계단 잠금 상태여야 함)");

        // F1 보스방 계단 개방 후 F0→F2 도달 가능
        cm.OpenStair(1, 2);
        Assert.IsTrue(cm.CanReachFloor(0, 2), "F1→F2 계단 개방 후 F0→F2 도달 불가");

        Cleanup();
    }

    // ====================================================================
    // G: CanCommandEnemyRoom 검증 — 인접 방만 명령 가능
    // ====================================================================
    [Test]
    public void CanCommandEnemyRoom_RequiresAdjacentPlayerRoom()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // 잘못된 입력: 존재하지 않는 방
        Assert.IsFalse(cm.CanCommandEnemyRoom(1, -1, 0));

        // 시작방 roomId 찾기
        Floor f1 = cm.map.floors[1];
        int startId = -1;
        for (int x = 0; x < f1.config.width; x++)
            for (int y = 0; y < f1.config.height; y++)
                if (f1.chunks[x, y].roomRole == RoomRole.StartRoom)
                { startId = f1.chunks[x, y].roomId; break; }

        Assert.IsTrue(startId >= 0, "F1 시작방을 찾을 수 없습니다.");

        // 시작방에서 인접한 방 중 하나 찾기
        int adjacentId = -1;
        if (f1.gates != null)
        {
            foreach (Gate g in f1.gates)
            {
                if (g.roomA == startId) { adjacentId = g.roomB; break; }
                if (g.roomB == startId) { adjacentId = g.roomA; break; }
            }
        }

        if (adjacentId >= 0)
        {
            Assert.IsTrue(cm.CanCommandEnemyRoom(1, startId, adjacentId),
                $"시작방(roomId={startId})에서 인접 방(roomId={adjacentId})에 명령 불가");
        }

        Cleanup();
    }

    // ====================================================================
    // B: 시작방/보스방 벽 두께 1 검증
    // ====================================================================
    [Test]
    public void WallThickness_StartAndBossRoomsHaveThickness1()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // F1 기준: 시작방의 외곽 경계 벽 두께 = 1
        Floor floor = cm.map.floors[1];
        int w = floor.config.width;
        int h = floor.config.height;

        for (int cx = 0; cx < w; cx++)
        {
            for (int cy = 0; cy < h; cy++)
            {
                Chunks c = floor.chunks[cx, cy];
                if (c.roomId < 0 || c.chunk == null) continue;
                if (c.roomRole != RoomRole.StartRoom && c.roomRole != RoomRole.BossRoom) continue;

                // 하단 외곽 경계(cy==0)인 경우: ty=0은 Wall, ty=1은 Floor
                bool isBottom = (cy == 0) || (cy > 0 && floor.chunks[cx, cy - 1].roomId == -1);
                if (isBottom)
                {
                    // 내부 tx(1~6)에서 확인
                    for (int tx = 1; tx <= 6; tx++)
                    {
                        Assert.AreEqual("Wall", c.chunk[tx, 0].name,
                            $"F1 {c.roomRole} chunk[{cx},{cy}] tile[{tx},0]이 Wall이 아닙니다.");
                        Assert.AreEqual("Floor", c.chunk[tx, 1].name,
                            $"F1 {c.roomRole} chunk[{cx},{cy}] tile[{tx},1]이 Floor가 아닙니다. (두께 1 초과)");
                    }
                    break; // 하나만 확인
                }
            }
        }

        Cleanup();
    }

    // ====================================================================
    // A: 축별 벽 두께 독립 적용 검증 — 좌우(X)와 상하(Y) 두께가 독립적
    // ====================================================================
    [Test]
    public void WallThicknessXY_AxisIndependent()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // F2(wallThicknessMax=3)에서 NormalRoom 외곽 청크의 좌우/상하 두께가 각각 적용되는지 확인
        Floor floor = cm.map.floors[2];
        int w = floor.config.width;
        int h = floor.config.height;

        bool foundValidChunk = false;
        for (int cx = 0; cx < w && !foundValidChunk; cx++)
        {
            for (int cy = 0; cy < h && !foundValidChunk; cy++)
            {
                Chunks c = floor.chunks[cx, cy];
                if (c.roomId < 0 || c.chunk == null) continue;
                if (c.roomRole != RoomRole.NormalRoom) continue;

                // 외곽 경계 여부 확인
                bool isLeft = (cx == 0) || (cx > 0 && floor.chunks[cx - 1, cy].roomId == -1);
                bool isBottom = (cy == 0) || (cy > 0 && floor.chunks[cx, cy - 1].roomId == -1);

                if (!isLeft && !isBottom) continue;

                // 벽 두께가 1 이상인지 확인 (최소 1줄은 Wall)
                if (isLeft)
                {
                    Assert.AreEqual("Wall", c.chunk[0, 4].name,
                        $"F2 NormalRoom chunk[{cx},{cy}] 좌측 외곽 tile[0,4]이 Wall이 아닙니다.");
                }
                if (isBottom)
                {
                    Assert.AreEqual("Wall", c.chunk[4, 0].name,
                        $"F2 NormalRoom chunk[{cx},{cy}] 하단 외곽 tile[4,0]이 Wall이 아닙니다.");
                }

                foundValidChunk = true;
            }
        }

        Assert.IsTrue(foundValidChunk, "F2에서 외곽 경계가 있는 NormalRoom 청크를 찾을 수 없습니다.");
        Cleanup();
    }

    // ====================================================================
    // B: F0 visibility/understand = 100 (안개 없음) 검증
    // ====================================================================
    [Test]
    public void Floor0_AllTilesHaveFullVisibilityAndUnderstand()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        Floor f0 = cm.map.floors[0];
        int w = f0.config.width;
        int h = f0.config.height;

        for (int cx = 0; cx < w; cx++)
        {
            for (int cy = 0; cy < h; cy++)
            {
                Chunks c = f0.chunks[cx, cy];
                if (c.chunk == null) continue;

                for (int tx = 0; tx < 8; tx++)
                {
                    for (int ty = 0; ty < 8; ty++)
                    {
                        Tile t = c.chunk[tx, ty];
                        Assert.AreEqual(100, t.visibility,
                            $"F0 chunk[{cx},{cy}] tile[{tx},{ty}] visibility={t.visibility} (100이어야 함)");
                        Assert.AreEqual(100, t.understand,
                            $"F0 chunk[{cx},{cy}] tile[{tx},{ty}] understand={t.understand} (100이어야 함)");
                    }
                }
            }
        }

        Cleanup();
    }

    // ====================================================================
    // C: 보스방 계단이 입구 반대편에 배치되는지 검증
    // ====================================================================
    [Test]
    public void BossRoomStair_PlacedOppositeEntrance()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 2; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;
            int targetFloor = f + 1;

            // 보스방 청크 및 계단 위치 찾기
            var bossChunks = new List<(int x, int y)>();
            int stairX = -1, stairY = -1;
            int bossRoomId = -1;

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (floor.chunks[x, y].roomRole == RoomRole.BossRoom)
                    {
                        bossChunks.Add((x, y));
                        bossRoomId = floor.chunks[x, y].roomId;
                        if (floor.chunks[x, y].stairTargetFloor == targetFloor)
                        {
                            stairX = x;
                            stairY = y;
                        }
                    }
                }

            Assert.IsTrue(stairX >= 0 && stairY >= 0,
                $"F{f}: 보스방 순차 계단을 찾을 수 없습니다.");

            // 보스방에 연결된 Gate 찾기
            if (floor.gates == null || bossRoomId < 0) continue;

            Gate? entranceGate = null;
            foreach (Gate g in floor.gates)
            {
                if (g.roomA == bossRoomId || g.roomB == bossRoomId)
                {
                    entranceGate = g;
                    break;
                }
            }

            if (entranceGate == null) continue;

            // Gate 방향과 계단 방향이 반대인지 확인
            Gate eg = entranceGate.Value;
            int gateChunkX = (eg.roomA == bossRoomId) ? eg.chunkAX : eg.chunkBX;
            int gateChunkY = (eg.roomA == bossRoomId) ? eg.chunkAY : eg.chunkBY;

            // 바운딩 박스 중앙
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var (bx, by) in bossChunks)
            {
                if (bx < minX) minX = bx;
                if (by < minY) minY = by;
                if (bx > maxX) maxX = bx;
                if (by > maxY) maxY = by;
            }
            float centerX = (minX + maxX) / 2f;
            float centerY = (minY + maxY) / 2f;

            // 입구 방향 벡터
            float entranceDirX = gateChunkX - centerX;
            float entranceDirY = gateChunkY - centerY;

            // 계단 방향 벡터
            float stairDirX = stairX - centerX;
            float stairDirY = stairY - centerY;

            // 내적이 음수 또는 0이면 반대편 (또는 중앙)
            float dot = entranceDirX * stairDirX + entranceDirY * stairDirY;
            Assert.IsTrue(dot <= 0.01f,
                $"F{f}: 보스방 계단({stairX},{stairY})이 입구({gateChunkX},{gateChunkY}) 반대편이 아닙니다. (내적={dot})");
        }

        Cleanup();
    }

    // ====================================================================
    // D(v2): 총 방 수 합산 검증 — 시작+일반+보스 = totalRoomCount (서브 목적방은 별도 카운트)
    // ====================================================================
    [Test]
    public void TotalRoomCount_MatchesConfig()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int f = 1; f <= 3; f++)
        {
            Floor floor = cm.map.floors[f];
            int w = floor.config.width;
            int h = floor.config.height;
            int expectedTotal = floor.config.totalRoomCount;
            int expectedSub = floor.config.subPurposeRoomCount;

            var startIds = new HashSet<int>();
            var normalIds = new HashSet<int>();
            var bossIds = new HashSet<int>();
            var subIds = new HashSet<int>();

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    Chunks c = floor.chunks[x, y];
                    if (c.roomId < 0) continue;
                    switch (c.roomRole)
                    {
                        case RoomRole.StartRoom: startIds.Add(c.roomId); break;
                        case RoomRole.NormalRoom: normalIds.Add(c.roomId); break;
                        case RoomRole.BossRoom: bossIds.Add(c.roomId); break;
                        case RoomRole.SubPurposeRoom: subIds.Add(c.roomId); break;
                    }
                }

            int actualTotal = startIds.Count + normalIds.Count + bossIds.Count;
            Assert.GreaterOrEqual(actualTotal, expectedTotal,
                $"F{f}: 총 방 수 부족 — 최소 기대 {expectedTotal}, 실제 {actualTotal} " +
                $"(시작={startIds.Count}, 일반={normalIds.Count}, 보스={bossIds.Count}, 서브(별도)={subIds.Count})");

            Assert.AreEqual(expectedSub, subIds.Count,
                $"F{f}: 서브 목적방 수 불일치 — 기대 {expectedSub}, 실제 {subIds.Count}");
        }

        Cleanup();
    }

    // ====================================================================
    // F: CanReenterFloor — 시작방 점령 여부에 따른 재진입 가능 판정
    // ====================================================================
    [Test]
    public void CanReenterFloor_RespectsStartRoomControl()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // F0: 항상 재진입 가능
        Assert.IsTrue(cm.CanReenterFloor(0), "F0은 항상 재진입 가능해야 합니다.");

        // F1: 시작방이 PlayerControlled이므로 재진입 가능
        Assert.IsTrue(cm.CanReenterFloor(1), "F1 시작방이 PlayerControlled인데 재진입 불가입니다.");

        // F1 시작방 후퇴 → 재진입 불가 확인 (시작방은 후퇴 불가이므로 직접 상태 변경)
        // 시작방의 occupationState를 Neutral로 변경하여 테스트
        Floor f1 = cm.map.floors[1];
        int startId = -1;
        for (int x = 0; x < f1.config.width; x++)
            for (int y = 0; y < f1.config.height; y++)
                if (f1.chunks[x, y].roomRole == RoomRole.StartRoom)
                {
                    startId = f1.chunks[x, y].roomId;
                    Chunks c = f1.chunks[x, y];
                    c.occupationState = OccupationState.Neutral;
                    cm.map.floors[1].chunks[x, y] = c;
                }

        Assert.IsFalse(cm.CanReenterFloor(1), "F1 시작방이 Neutral인데 재진입 가능합니다.");

        // 범위 초과
        Assert.IsFalse(cm.CanReenterFloor(-1), "잘못된 인덱스에 대해 true 반환");
        Assert.IsFalse(cm.CanReenterFloor(10), "잘못된 인덱스에 대해 true 반환");

        Cleanup();
    }

    // ====================================================================
    // E: RecalculateGateWidth — 런타임 Gate 폭 재계산 검증
    // ====================================================================
    [Test]
    public void RecalculateGateWidth_IncreasesWidth()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // F1에서 Gate 하나 찾기
        Floor f1 = cm.map.floors[1];
        if (f1.gates == null || f1.gates.Count == 0)
        {
            Assert.Inconclusive("F1에 Gate가 없습니다.");
            return;
        }

        Gate firstGate = f1.gates[0];
        int origWidth = firstGate.width;

        // 기존보다 큰 Footprint로 재계산
        int newFootprint = Mathf.Min(origWidth + 2, 6);
        int result = cm.RecalculateGateWidth(1, firstGate.roomA, firstGate.roomB, newFootprint);

        Assert.IsTrue(result >= origWidth,
            $"재계산된 Gate 폭({result})이 원래 폭({origWidth})보다 작습니다.");

        // 존재하지 않는 Gate: -1 반환
        Assert.AreEqual(-1, cm.RecalculateGateWidth(1, -999, -998, 3));

        Cleanup();
    }

    // ====================================================================
    // H: FindPathAcrossFloors — 층간 경로 시퀀스 반환 검증
    // ====================================================================
    [Test]
    public void FindPathAcrossFloors_ReturnsCorrectSequence()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // F0→F1: 직접 경로 [0, 1]
        var path01 = cm.FindPathAcrossFloors(0, 1);
        Assert.AreEqual(2, path01.Count, "F0→F1 경로가 2단계가 아닙니다.");
        Assert.AreEqual(0, path01[0]);
        Assert.AreEqual(1, path01[1]);

        // 같은 층: [f]
        var pathSame = cm.FindPathAcrossFloors(2, 2);
        Assert.AreEqual(1, pathSame.Count);
        Assert.AreEqual(2, pathSame[0]);

        // F0→F2: 보스방 계단 잠금 → 도달 불가 → 빈 리스트
        var path02 = cm.FindPathAcrossFloors(0, 2);
        Assert.AreEqual(0, path02.Count, "F0→F2: 보스방 계단 잠금 상태에서 경로가 존재합니다.");

        // 보스방 계단 개방 후 경로 존재
        cm.OpenStair(1, 2);
        var path02Open = cm.FindPathAcrossFloors(0, 2);
        Assert.IsTrue(path02Open.Count >= 2, "F1→F2 계단 개방 후 F0→F2 경로가 없습니다.");
        Assert.AreEqual(0, path02Open[0]);
        Assert.AreEqual(2, path02Open[path02Open.Count - 1]);

        // 잘못된 입력
        var pathInvalid = cm.FindPathAcrossFloors(-1, 5);
        Assert.AreEqual(0, pathInvalid.Count);

        Cleanup();
    }
}
#endif