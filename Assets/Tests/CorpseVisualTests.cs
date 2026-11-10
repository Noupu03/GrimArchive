#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 사망 클립 마지막 프레임을 시체로 남길 때의 순수 계산(Script/Unit/Visual/CorpseVisualMath.cs) — 회의록 2026-11-10.
// 실제 비주얼 이어받기(UnitGenerate.RemoveVisualKeepAsCorpse)는 Unity 플레이로만 확인된다 —
// 여기선 "부위 정렬 순서가 어떻게 옮겨지는가", "옆 타일에 놓인 시체가 얼마나 옮겨지는가"만 고정한다.
// ========================================================================

public class CorpseVisualTests
{
	[Test]
	public void LowestVisiblePart_BecomesCorpseBase()
	{
		Assert.AreEqual(CorpseVisualMath.CorpseSortingBase, CorpseVisualMath.RemapSortingOrder(10, 10));
	}

	[Test]
	public void PartOrderGaps_ArePreserved()
	{
		// 몸 10, 무기 앞면 11 → 몸 5, 그 위 6 — 부위끼리의 앞뒤 관계는 그대로.
		int body = CorpseVisualMath.RemapSortingOrder(10, 10);
		int front = CorpseVisualMath.RemapSortingOrder(11, 10);
		int back = CorpseVisualMath.RemapSortingOrder(8, 8);
		Assert.AreEqual(body + 1, front);
		Assert.AreEqual(CorpseVisualMath.CorpseSortingBase, back);
	}

	[Test]
	public void Corpse_IsBelowLivingUnits()
	{
		// 산 유닛 몸 정렬값은 100(실측) — 시체가 살아 있는 유닛을 가리면 안 된다.
		Assert.Less(CorpseVisualMath.RemapSortingOrder(100, 100), 100);
	}

	[Test]
	public void SameTile_NoShift()
	{
		Assert.AreEqual(Vector3.zero, CorpseVisualMath.TileShift(new Vector2Int(7, 4), new Vector3Int(7, 4, 1)));
	}

	[Test]
	public void DisplacedCorpseTile_ShiftsVisualBySameAmount()
	{
		// 함정 칸에서 죽어 시체가 (8,4)로 밀렸으면 비주얼도 오른쪽으로 한 칸. 층(z)은 위치 이동에 쓰지 않는다.
		Vector3 shift = CorpseVisualMath.TileShift(new Vector2Int(7, 4), new Vector3Int(8, 3, 2));
		Assert.AreEqual(new Vector3(1f, -1f, 0f), shift);
	}
}
#endif
