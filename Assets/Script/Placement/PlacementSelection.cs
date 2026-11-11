using System;
using System.Collections.Generic;
using UnityEngine;

// 층별 임시 선택·남은 순서. 저장한 방어 계획(DefensePlacementSystem)과 분리해야 모드를 다시 열 때 이전 유닛이 선택돼 있지 않다.
public sealed class PlacementSelection
{
	// 공통 입력(회전·미배치·이동 적용)의 대상. 새 타일을 지정한 인원도 남는다.
	public readonly List<Unit> Selected = new List<Unit>();
	// 아직 새 타일을 지정하지 않은 인원 — Order[0]이 다음 배치 인원이다.
	public readonly List<Unit> Order = new List<Unit>();

	public Unit Next => Order.Count > 0 ? Order[0] : null;
	public bool HasSelection => Selected.Count > 0;

	// 처음 선택한 여럿은 드래그 시작점에서 가까운 순서로 줄 세운다. additive면 기존 선택 뒤에 붙인다.
	public void SelectGroup(IEnumerable<Unit> units, Vector2 anchor, Func<Unit, Vector2> centerOf, bool additive)
	{
		var incoming = new List<Unit>();
		foreach (var u in units)
			if (u != null && !incoming.Contains(u)) incoming.Add(u);
		DefensePlacementMath.SortForSequentialPlacement(incoming, centerOf, anchor);

		if (!additive) Clear();
		foreach (var u in incoming)
		{
			if (Selected.Contains(u)) continue;
			Selected.Add(u);
			Order.Add(u);
		}
	}

	public void Toggle(Unit unit)
	{
		if (unit == null) return;
		if (Selected.Remove(unit)) { Order.Remove(unit); return; }
		Selected.Add(unit);
		Order.Add(unit);
	}

	public void SelectOnly(Unit unit)
	{
		Clear();
		if (unit == null) return;
		Selected.Add(unit);
		Order.Add(unit);
	}

	// 배치 성공 — 순서 칸에서만 뺀다. 전체 선택에는 남는다.
	public bool OnPlaced(Unit unit) => Order.Remove(unit);

	public bool Swap(int a, int b) => DefensePlacementMath.SwapOrder(Order, a, b);

	public void Clear()
	{
		Selected.Clear();
		Order.Clear();
	}

	public void Remove(Unit unit)
	{
		Selected.Remove(unit);
		Order.Remove(unit);
	}

	public void Prune()
	{
		Selected.RemoveAll(u => u == null || u.hp <= 0f);
		Order.RemoveAll(u => u == null || u.hp <= 0f);
	}
}

// 모드 안에서만 기억하는 층별 선택 — 완전히 나가면 모든 층을 지운다.
public sealed class PlacementSelectionStore
{
	readonly Dictionary<int, PlacementSelection> _byFloor = new Dictionary<int, PlacementSelection>();

	public PlacementSelection For(int floor)
	{
		if (!_byFloor.TryGetValue(floor, out var s)) _byFloor[floor] = s = new PlacementSelection();
		return s;
	}

	public void ClearAll() => _byFloor.Clear();
}
