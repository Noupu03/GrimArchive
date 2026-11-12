#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// 건설 설계명세서 v0.2 후속 구현 테스트 - 겹친 시체 선택 순서(2-8-표2), 내부 작업 면적·비켜주기 분류(1-6·2-8), 문 확장 맵 변경(2-9).
[TestFixture]
public class FacilityRulesTests2
{
	private static CorpseRecord Corpse(double life, double soul, CorpseOriginFaction origin = CorpseOriginFaction.HumanEnemy)
		=> new CorpseRecord
		{
			CorpseId = "c",
			OriginFaction = origin,
			IsProducibleSpecies = false,
			ConfiguredRefineLifeReward = life,
			ConfiguredRefineSoulReward = soul,
		};

	// ── 겹친 시체 선택 순서 ──

	[Test]
	public void FloorCorpseOrder_SoulShardTotalFirst_ThenLifeEnergy_ThenPlacementOrder()
	{
		var a = Corpse(life: 50, soul: 0);
		var b = Corpse(life: 1, soul: 5);
		Assert.AreEqual(1, FloorCorpseOrder.PickBestIndex(new List<CorpseRecord> { a, b }), "영혼 파편이 많은 쪽이 먼저");

		var c = Corpse(life: 10, soul: 5);
		Assert.AreEqual(1, FloorCorpseOrder.PickBestIndex(new List<CorpseRecord> { a, c, b }), "동률 영혼 파편은 생명 에너지가 큰 쪽");
		Assert.AreEqual(1, FloorCorpseOrder.PickBestIndex(new List<CorpseRecord> { b, c }), "영혼 파편 동률이면 생명 에너지가 큰 c(인덱스 1)");

		var d = Corpse(life: 10, soul: 5);
		Assert.AreEqual(0, FloorCorpseOrder.PickBestIndex(new List<CorpseRecord> { c, d }), "전부 같으면 먼저 놓인 순");
		Assert.AreEqual(-1, FloorCorpseOrder.PickBestIndex(new List<CorpseRecord>()));
	}

	[Test]
	public void FloorCorpseOrder_EvolutionBonusCountsAsSoulShard()
	{
		var plain = Corpse(life: 10, soul: 5);
		var evolved = Corpse(life: 10, soul: 5);
		evolved.CumulativeBaseEvolutionSoulCost = 100; // 진화 추가분 20%
		Assert.AreEqual(1, FloorCorpseOrder.PickBestIndex(new List<CorpseRecord> { plain, evolved }));
	}

	// ── 내부 작업 ──

	[Test]
	public void InternalWork_EntryAlsoNeedsAreaForEveryoneToExitWithoutOverlap()
	{
		var body = new Vector2Int(1, 1);
		// 2x2 시설(면적 4)에 1x1 넷은 들어가지만 다섯째는 면적이 부족하다.
		Assert.IsNull(InternalWorkRules.GetEnterBlockReason(FacilityKind.LifeEnergy, 3, body, true, false, insideBodyArea: 3, facilityArea: 4));
		Assert.IsNotNull(InternalWorkRules.GetEnterBlockReason(FacilityKind.LifeEnergy, 3, new Vector2Int(2, 1), true, false, insideBodyArea: 3, facilityArea: 4), "남은 면적보다 큰 몸체");
		Assert.IsNotNull(InternalWorkRules.GetEnterBlockReason(FacilityKind.Research, 2, body, true, false, 2, 4), "정원 가득");
	}

	[Test]
	public void InternalWork_BlockerClassification_FollowsYieldTable()
	{
		Assert.AreEqual(InternalWorkRules.BlockerState.BusyOrFighting, InternalWorkRules.ClassifyBlocker(true, true, true), "작업·전투 중이면 강제하지 않는다");
		Assert.AreEqual(InternalWorkRules.BlockerState.WorkerOnlyEscape, InternalWorkRules.ClassifyBlocker(false, true, true));
		Assert.AreEqual(InternalWorkRules.BlockerState.ExitWaitingAlly, InternalWorkRules.ClassifyBlocker(false, false, true));
		Assert.AreEqual(InternalWorkRules.BlockerState.WaitingAlly, InternalWorkRules.ClassifyBlocker(false, false, false));
	}

	// ── 문 확장: 맵 변경 ──

	[Test]
	public void GateExpansion_OpensBothEndsAndWidensGate_DoorCenterStaysFixed()
	{
		var cm = new CreateMap { useFixedSeed = true, seed = 14056 };
		cm.GenerateMap();

		int cs = 0;
		Floor? found = null;
		int floorIdx = -1, gateIdx = -1;
		var end0 = new List<Vector2Int>();
		var end1 = new List<Vector2Int>();
		int newWidth = 0;
		for (int f = 0; f < cm.map.floors.Length && found == null; f++)
		{
			Floor floor = cm.map.floors[f];
			if (floor.gates == null) continue;
			for (int g = 0; g < floor.gates.Count; g++)
			{
				if (!cm.TryGetGateExpansionWalls(f, g, 2, 4, end0, end1, out newWidth, out _)) continue;
				found = floor; floorIdx = f; gateIdx = g; cs = floor.config.chunkSize;
				break;
			}
		}
		if (found == null) { Assert.Inconclusive("확장할 수 있는 통로가 없습니다."); return; }

		Gate before = cm.map.floors[floorIdx].gates[gateIdx];
		var rowsBefore = DoorSystem.GetGateDoorTiles(before, cs);
		Vector2 centerBefore = DoorGeometry.CenterOf(rowsBefore[0]);

		var all = new List<Vector2Int>(end0);
		all.AddRange(end1);
		foreach (Vector2Int t in all) Assert.AreEqual("Wall", GetTile(cm, floorIdx, t).name, "확장 전 양 끝은 벽이다");

		Assert.IsTrue(cm.ApplyGateExpansionTiles(floorIdx, gateIdx, newWidth, all));
		foreach (Vector2Int t in all) Assert.AreNotEqual("Wall", GetTile(cm, floorIdx, t).name, "확장 뒤 양 끝은 바닥이다");

		Gate after = cm.map.floors[floorIdx].gates[gateIdx];
		Assert.AreEqual(before.width + 2, after.width);
		var rowsAfter = DoorSystem.GetGateDoorTiles(after, cs);
		Assert.AreEqual(before.width + 2, rowsAfter[0].Count);
		Assert.AreEqual(centerBefore.x, DoorGeometry.CenterOf(rowsAfter[0]).x, 1e-4f, "문 중심은 고정");
		Assert.AreEqual(centerBefore.y, DoorGeometry.CenterOf(rowsAfter[0]).y, 1e-4f, "문 중심은 고정");
		foreach (Vector2Int t in all) Assert.IsTrue(IsInRows(rowsAfter, t) || IsBehindRows(rowsAfter, t, after.isHorizontal), "새로 열린 칸은 넓어진 통로 안이다");

		// 폭 한계에 닿으면 더 넓힐 수 없다
		Assert.IsFalse(cm.TryGetGateExpansionWalls(floorIdx, gateIdx, 2, after.width, new List<Vector2Int>(), new List<Vector2Int>(), out _, out string fail));
		Assert.IsNotNull(fail);
	}

	private static Tile GetTile(CreateMap cm, int floorIdx, Vector2Int t)
	{
		Floor floor = cm.map.floors[floorIdx];
		int cs = floor.config.chunkSize;
		return floor.chunks[t.x / cs, t.y / cs].chunk[t.x % cs, t.y % cs];
	}

	private static bool IsInRows(List<Vector2Int>[] rows, Vector2Int t)
	{
		foreach (var row in rows) if (row.Contains(t)) return true;
		return false;
	}

	// 벽 두께가 2 이상이면 문 줄(청크 경계 칸) 뒤쪽 칸도 열린다 - 같은 줄 방향 좌표가 문 줄 범위 안이면 통로 안이다.
	private static bool IsBehindRows(List<Vector2Int>[] rows, Vector2Int t, bool horizontal)
	{
		foreach (var row in rows)
			foreach (Vector2Int r in row)
				if (horizontal ? r.y == t.y : r.x == t.x) return true;
		return false;
	}
}
#endif
