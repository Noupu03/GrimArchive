#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

// ========================================================================
// CreateMap + MapRandering 자동화 테스트
// ========================================================================
// 실행 방법:
//   1. Unity 에디터 → Window → General → Test Runner
//   2. EditMode 탭 선택
//   3. Run All 또는 개별 테스트 우클릭 → Run
//
// 주의사항:
//   - asmdef 없이 Assembly-CSharp에 포함됨
//   - #if UNITY_INCLUDE_TESTS 로 보호되어 빌드 시 제외됨
//   - NUnit [Test] 사용 (동기 테스트 — 코루틴 불필요)
// ========================================================================

public class CreateMapPlayTests
{
    // ── 헬퍼: 테스트용 CreateMap GameObject 생성 ──
    private CreateMap SetupCreateMap(int seed = 12345)
    {
        var go = new GameObject("TestCreateMap");
        var cm = go.AddComponent<CreateMap>();
        cm.useFixedSeed = true;
        cm.seed = seed;
        return cm;
    }

    // ── 헬퍼: 테스트용 MapRandering + Tilemap 씬 구성 ──
    private (MapRandering renderer, Tilemap tilemap) SetupRenderer(CreateMap cm)
    {
        var gridGo = new GameObject("TestGrid");
        gridGo.AddComponent<Grid>();

        var tilemapGo = new GameObject("TestTilemap");
        tilemapGo.transform.SetParent(gridGo.transform);
        var tilemap = tilemapGo.AddComponent<Tilemap>();
        tilemapGo.AddComponent<TilemapRenderer>();

        var rendererGo = new GameObject("TestMapRandering");
        var mr = rendererGo.AddComponent<MapRandering>();
        mr.createMap = cm;
        mr.tilemap = tilemap;

        return (mr, tilemap);
    }

    // ── 헬퍼: 테스트 후 씬 정리 ──
    private void Cleanup(params GameObject[] objects)
    {
        foreach (var go in objects)
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }
    }

    // ====================================================================
    // ① 맵 초기화 검증: 16x16 청크 배열 생성
    // ====================================================================
    [Test]
    public void InitMap_Creates16x16Chunks()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        Assert.IsNotNull(cm.map.session, "map.session이 null입니다.");
        Assert.AreEqual(16, cm.map.session.GetLength(0), "X축 청크 수가 16이 아닙니다.");
        Assert.AreEqual(16, cm.map.session.GetLength(1), "Y축 청크 수가 16이 아닙니다.");

        Cleanup(cm.gameObject);
    }

    // ====================================================================
    // ② 모든 청크에 8x8 타일 배열 할당 검증
    // ====================================================================
    [Test]
    public void AllChunks_Have8x8TileArray()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                Chunks c = cm.map.session[x, y];
                Assert.IsNotNull(c.chunk, $"chunk[{x},{y}].chunk이 null입니다.");
                Assert.AreEqual(8, c.chunk.GetLength(0), $"chunk[{x},{y}] X축 타일 수가 8이 아닙니다.");
                Assert.AreEqual(8, c.chunk.GetLength(1), $"chunk[{x},{y}] Y축 타일 수가 8이 아닙니다.");
            }
        }

        Cleanup(cm.gameObject);
    }

    // ====================================================================
    // ③ 모든 청크에 방(roomId)이 할당되었는지 검증
    // ====================================================================
    [Test]
    public void AllChunks_HaveRoomAssigned()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                int roomId = cm.map.session[x, y].roomId;
                Assert.GreaterOrEqual(roomId, 0, $"chunk[{x},{y}]에 방이 할당되지 않았습니다 (roomId={roomId}).");
            }
        }

        Cleanup(cm.gameObject);
    }

    // ====================================================================
    // ④ 스폰 영역(7~8, 7~8) 검증: 모두 같은 roomId
    // ====================================================================
    [Test]
    public void SpawnArea_HasUniformRoomId()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        int spawnId = cm.map.session[7, 7].roomId;
        Assert.GreaterOrEqual(spawnId, 0, "스폰 영역에 roomId가 할당되지 않았습니다.");

        for (int x = 7; x <= 8; x++)
        {
            for (int y = 7; y <= 8; y++)
            {
                Assert.AreEqual(spawnId, cm.map.session[x, y].roomId,
                    $"스폰 청크[{x},{y}]의 roomId({cm.map.session[x, y].roomId})가 스폰 기준({spawnId})과 다릅니다.");
            }
        }

        Cleanup(cm.gameObject);
    }

    // ====================================================================
    // ⑤ 타일 이름 검증: 가장자리=Wall, 내부=Floor
    // ====================================================================
    [Test]
    public void TileNames_EdgeIsWall_InteriorIsFloor()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        // 스폰 내부 청크(7,7) — 꼭짓점은 통로와 무관하므로 항상 Wall
        Chunks c = cm.map.session[7, 7];

        Assert.AreEqual("Wall", c.chunk[0, 0].name, "좌하단 꼭짓점이 Wall이 아닙니다.");
        Assert.AreEqual("Wall", c.chunk[7, 7].name, "우상단 꼭짓점이 Wall이 아닙니다.");
        Assert.AreEqual("Wall", c.chunk[0, 7].name, "좌상단 꼭짓점이 Wall이 아닙니다.");
        Assert.AreEqual("Wall", c.chunk[7, 0].name, "우하단 꼭짓점이 Wall이 아닙니다.");

        Assert.AreEqual("Floor", c.chunk[2, 2].name, "내부 타일(2,2)이 Floor가 아닙니다.");
        Assert.AreEqual("Floor", c.chunk[5, 5].name, "내부 타일(5,5)이 Floor가 아닙니다.");

        Cleanup(cm.gameObject);
    }

    // ====================================================================
    // ⑥ 같은 방 내부 청크 간 벽 개방 검증
    // ====================================================================
    [Test]
    public void InternalWalls_OpenedBetweenSameRoomChunks()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        int openedCount = 0;

        // 수평 내부 벽 검증
        for (int x = 0; x < 15; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                int idA = cm.map.session[x, y].roomId;
                int idB = cm.map.session[x + 1, y].roomId;

                if (idA >= 0 && idA == idB)
                {
                    Chunks cA = cm.map.session[x, y];
                    Chunks cB = cm.map.session[x + 1, y];

                    for (int ty = 3; ty <= 4; ty++)
                    {
                        Assert.AreEqual("Floor", cA.chunk[7, ty].name,
                            $"내부벽 미개방: chunk[{x},{y}] tx=7, ty={ty}");
                        Assert.AreEqual("Floor", cB.chunk[0, ty].name,
                            $"내부벽 미개방: chunk[{x + 1},{y}] tx=0, ty={ty}");
                    }
                    openedCount++;
                }
            }
        }

        // 수직 내부 벽 검증
        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 15; y++)
            {
                int idA = cm.map.session[x, y].roomId;
                int idB = cm.map.session[x, y + 1].roomId;

                if (idA >= 0 && idA == idB)
                {
                    Chunks cA = cm.map.session[x, y];
                    Chunks cB = cm.map.session[x, y + 1];

                    for (int tx = 3; tx <= 4; tx++)
                    {
                        Assert.AreEqual("Floor", cA.chunk[tx, 7].name,
                            $"내부벽 미개방: chunk[{x},{y}] tx={tx}, ty=7");
                        Assert.AreEqual("Floor", cB.chunk[tx, 0].name,
                            $"내부벽 미개방: chunk[{x},{y + 1}] tx={tx}, ty=0");
                    }
                    openedCount++;
                }
            }
        }

        Assert.Greater(openedCount, 0, "내부 벽이 개방된 멀티청크 방이 하나도 없습니다.");

        Cleanup(cm.gameObject);
    }

    // ====================================================================
    // ⑦ 시드 재현성 검증: 같은 시드 → 같은 맵
    // ====================================================================
    [Test]
    public void SameSeed_ProducesSameMap()
    {
        var cm = SetupCreateMap(99999);
        cm.GenerateMap();

        int[,] snapshot = new int[16, 16];
        for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++)
                snapshot[x, y] = cm.map.session[x, y].roomId;

        cm.seed = 99999;
        cm.GenerateMap();

        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                Assert.AreEqual(snapshot[x, y], cm.map.session[x, y].roomId,
                    $"시드 재현 실패: chunk[{x},{y}] roomId가 다릅니다.");
            }
        }

        Cleanup(cm.gameObject);
    }

    // ====================================================================
    // ⑧ 다른 시드 → 다른 맵 (최소 1개 청크 차이)
    // ====================================================================
    [Test]
    public void DifferentSeed_ProducesDifferentMap()
    {
        var cm = SetupCreateMap(11111);
        cm.GenerateMap();

        int[,] snapshot = new int[16, 16];
        for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++)
                snapshot[x, y] = cm.map.session[x, y].roomId;

        cm.seed = 22222;
        cm.GenerateMap();

        bool hasDifference = false;
        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                if (snapshot[x, y] != cm.map.session[x, y].roomId)
                {
                    hasDifference = true;
                    break;
                }
            }
            if (hasDifference) break;
        }

        Assert.IsTrue(hasDifference, "다른 시드인데 맵이 완전히 동일합니다.");

        Cleanup(cm.gameObject);
    }

    // ====================================================================
    // ⑨ 방 간 통로 존재 검증
    // ====================================================================
    [Test]
    public void ConnectedRooms_HaveOpenPassage()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        int passageCount = 0;

        // 수평 경계
        for (int x = 0; x < 15; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                int idA = cm.map.session[x, y].roomId;
                int idB = cm.map.session[x + 1, y].roomId;
                if (idA < 0 || idB < 0 || idA == idB) continue;

                Chunks cA = cm.map.session[x, y];
                Chunks cB = cm.map.session[x + 1, y];

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
        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 15; y++)
            {
                int idA = cm.map.session[x, y].roomId;
                int idB = cm.map.session[x, y + 1].roomId;
                if (idA < 0 || idB < 0 || idA == idB) continue;

                Chunks cA = cm.map.session[x, y];
                Chunks cB = cm.map.session[x, y + 1];

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
        Debug.Log($"테스트 통과: 총 {passageCount}개 통로 발견.");

        Cleanup(cm.gameObject);
    }

    // ====================================================================
    // ⑩ MapRandering: RenderMap 후 Tilemap에 타일 배치 검증
    // ====================================================================
    [Test]
    public void MapRandering_PlacesTilesOnTilemap()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        var (mr, tilemap) = SetupRenderer(cm);
        mr.DoRandering();

        int tileCount = 0;
        BoundsInt bounds = tilemap.cellBounds;
        foreach (var pos in bounds.allPositionsWithin)
        {
            if (tilemap.HasTile(pos))
                tileCount++;
        }

        Assert.Greater(tileCount, 0, "Tilemap에 타일이 하나도 배치되지 않았습니다.");
        Debug.Log($"테스트 통과: Tilemap에 {tileCount}개 타일 배치됨.");

        Cleanup(cm.gameObject, mr.gameObject,
            tilemap.transform.parent != null ? tilemap.transform.parent.gameObject : tilemap.gameObject);
    }

    // ====================================================================
    // ⑪ MapRandering: 참조 누락 시 에러 없이 안전하게 반환되는지 검증
    // ====================================================================
    [Test]
    public void MapRandering_ReturnsGracefully_WhenReferenceMissing()
    {
        var rendererGo = new GameObject("TestMapRandering");
        var mr = rendererGo.AddComponent<MapRandering>();
        // createMap, tilemap 의도적으로 미연결

        // RenderMap이 NullReferenceException 없이 안전하게 반환되는지 확인
        Assert.DoesNotThrow(() => mr.RenderMap(),
            "참조 미연결 시 RenderMap에서 예외가 발생했습니다.");

        Cleanup(rendererGo);
    }

    // ====================================================================
    // ⑫ 세션 범위 검증: 5개 세션이 16x16 전체를 커버하는지
    // ====================================================================
    [Test]
    public void Sessions_CoverEntireMap()
    {
        var cm = SetupCreateMap();
        cm.GenerateMap();

        bool[,] covered = new bool[16, 16];

        for (int s = 0; s <= 4; s++)
        {
            int sx = cm.session.session[s, 0, 0];
            int ex = cm.session.session[s, 0, 1];
            int sy = cm.session.session[s, 1, 0];
            int ey = cm.session.session[s, 1, 1];

            for (int x = sx; x <= ex; x++)
                for (int y = sy; y <= ey; y++)
                    covered[x, y] = true;
        }

        for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++)
                Assert.IsTrue(covered[x, y], $"좌표[{x},{y}]가 어떤 세션에도 포함되지 않습니다.");

        Cleanup(cm.gameObject);
    }
}
#endif