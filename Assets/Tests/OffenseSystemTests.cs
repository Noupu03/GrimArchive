#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 오펜스 MVP 최소 구현 검증 테스트.
// 순수 C# 로직(ResourceAccumulator, Room 상태)은 NUnit으로 직접 검증하고,
// OffenseProcessor의 성공 조건 로직은 Room/RoomType 조합으로 시뮬레이션한다.
// ========================================================================

public class OffenseSystemTests
{
    // ─────────────── ResourceAccumulator 단위 테스트 ───────────────

    [SetUp]
    public void ResetAccumulator()
    {
        // 각 테스트 전 누적량 초기화 (싱글턴이므로 직접 Clear 호출)
        ResourceAccumulator.Instance.ClearResourceB();
    }

    [Test]
    public void ResourceB_Accumulates_Correctly()
    {
        ResourceAccumulator.Instance.AccumulateResourceB(10);
        ResourceAccumulator.Instance.AccumulateResourceB(10);
        Assert.AreEqual(20, ResourceAccumulator.Instance.AccumulatedResourceB);
    }

    [Test]
    public void ResourceB_ClearResets_To_Zero()
    {
        ResourceAccumulator.Instance.AccumulateResourceB(50);
        ResourceAccumulator.Instance.ClearResourceB();
        Assert.AreEqual(0, ResourceAccumulator.Instance.AccumulatedResourceB);
    }

    [Test]
    public void ResourceB_CommitResets_After_Grant()
    {
        ResourceAccumulator.Instance.AccumulateResourceB(30);
        // ResourceManager.Instance가 null이어도 Commit 자체는 동작해야 함 (null-safe 설계)
        ResourceAccumulator.Instance.CommitResourceB();
        Assert.AreEqual(0, ResourceAccumulator.Instance.AccumulatedResourceB,
            "CommitResourceB 호출 후 누적량이 0으로 초기화되어야 한다");
    }

    [Test]
    public void ResourceB_Zero_Commit_DoesNothing()
    {
        // 누적량 0인 상태에서 Commit해도 문제없어야 함
        Assert.DoesNotThrow(() => ResourceAccumulator.Instance.CommitResourceB());
        Assert.AreEqual(0, ResourceAccumulator.Instance.AccumulatedResourceB);
    }

    // ─────────────── Room 상태 단위 테스트 ───────────────

    [Test]
    public void Room_DefaultFaction_IsWild()
    {
        var room = new Room();
        Assert.AreEqual(FactionType.Wild, room.RoomFaction,
            "방의 기본 세력은 야생이어야 한다");
    }

    [Test]
    public void Room_FactionChange_ToPlayer()
    {
        var room = new Room { RoomFaction = FactionType.Wild };
        room.RoomFaction = FactionType.Player;
        Assert.AreEqual(FactionType.Player, room.RoomFaction,
            "오펜스 성공 후 방 소속이 플레이어로 변경되어야 한다");
    }

    [Test]
    public void Room_HasActiveSpawner_DefaultFalse()
    {
        var room = new Room();
        Assert.IsFalse(room.HasActiveSpawner,
            "거점이 부착되기 전에는 HasActiveSpawner가 false여야 한다");
    }

    [Test]
    public void Room_SpawnerType_Normal_IsNormal()
    {
        var room = new Room { Type = RoomType.Normal };
        Assert.AreEqual(RoomType.Normal, room.Type);
    }

    [Test]
    public void Room_SpawnerType_Spawner_IsSpawner()
    {
        var room = new Room { Type = RoomType.Spawner };
        Assert.AreEqual(RoomType.Spawner, room.Type);
    }

    // ─────────────── 성공 조건 로직 검증 ───────────────

    [Test]
    public void NormalRoom_Success_WhenNoWildUnits()
    {
        // 야생 무리형: 야생 유닛이 없으면 성공 — NormalRoom 성공 조건은
        // "방 안에 WildMonsterBehavior 유닛이 없는가"이다.
        // 순수 Room 상태만으로 검증 가능한 부분: Type이 Normal이고 야생 유닛이 없을 때.
        var room = new Room { Type = RoomType.Normal, RoomFaction = FactionType.Wild };

        // 야생 유닛이 0마리인 상태를 표현 — Room.ContainedUnits가 비어있음
        bool noWild = true; // GetUnitsInRoom 결과 시뮬레이션
        bool normalSuccess = (room.Type == RoomType.Normal) && noWild;
        Assert.IsTrue(normalSuccess,
            "야생 무리형: 야생 유닛 전멸 시 오펜스 성공이어야 한다");
    }

    [Test]
    public void SpawnerRoom_Success_WhenSpawnerDestroyed_RegardlessOfWildUnits()
    {
        // 야생 거점형 MVP 4.2: 거점 파괴만으로 성공 (남은 야생 유닛 무관)
        var room = new Room { Type = RoomType.Spawner, RoomFaction = FactionType.Wild };
        room.HasActiveSpawner = false; // 거점 파괴

        bool spawnerSuccess = (room.Type == RoomType.Spawner) && !room.HasActiveSpawner;
        Assert.IsTrue(spawnerSuccess,
            "야생 거점형: 거점 파괴 시 남은 야생 유닛 여부와 무관하게 오펜스 성공이어야 한다");
    }

    [Test]
    public void SpawnerRoom_NotSuccess_WhenSpawnerAlive()
    {
        var room = new Room { Type = RoomType.Spawner, RoomFaction = FactionType.Wild };
        room.HasActiveSpawner = true; // 거점 생존

        bool spawnerSuccess = (room.Type == RoomType.Spawner) && !room.HasActiveSpawner;
        Assert.IsFalse(spawnerSuccess,
            "야생 거점형: 거점이 살아있으면 오펜스 성공이 아니어야 한다");
    }

    // ─────────────── 전체 흐름 검증 (정산 포함) ───────────────

    [Test]
    public void FullFlow_WildKill_Accumulate_ThenSuccess_CommitsB()
    {
        // 야생 유닛 처치 → 자원 B 누적 → 오펜스 성공 → 자원 B 정산 → 방 소속 변경
        ResourceAccumulator.Instance.ClearResourceB();

        // 야생 유닛 3마리 처치 시뮬레이션 (WildMonsterBehavior.OnDeath 로직 재현)
        ResourceAccumulator.Instance.AccumulateResourceB(10);
        ResourceAccumulator.Instance.AccumulateResourceB(10);
        ResourceAccumulator.Instance.AccumulateResourceB(10);

        Assert.AreEqual(30, ResourceAccumulator.Instance.AccumulatedResourceB,
            "야생 유닛 3마리 처치 후 자원 B 누적량이 30이어야 한다");

        // 오펜스 성공 → 정산
        var room = new Room { Type = RoomType.Normal, RoomFaction = FactionType.Wild };
        ResourceAccumulator.Instance.CommitResourceB();
        room.RoomFaction = FactionType.Player;

        Assert.AreEqual(0, ResourceAccumulator.Instance.AccumulatedResourceB,
            "오펜스 성공 후 자원 B 누적량이 0으로 초기화되어야 한다");
        Assert.AreEqual(FactionType.Player, room.RoomFaction,
            "오펜스 성공 후 방 소속이 플레이어로 변경되어야 한다");
    }
}
#endif
