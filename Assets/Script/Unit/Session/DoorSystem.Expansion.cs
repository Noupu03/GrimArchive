using System.Collections.Generic;
using Haare.Util.Logger;
using UnityEngine;

// 건설 설계명세서 v0.2 2-9 문 확장의 문·맵 쪽 처리 - 확장 계획(양 끝 벽)과 완료 시 개구부 변경·문 재구성.
// 폭 단계·최대 폭·HP 배율은 문 종류 문서가 정한다 - 아래 상수는 문서 예시(1×2 → 1×4, x1.5)를 따른 자리표시자다.
public partial class DoorSystem
{
	public const int GateExpansionStep = 2;         // 양 끝 벽 1칸씩 -> 폭 +2(문서 예: 1×2 -> 1×4)
	public const int GateExpansionMaxWidth = 4;     // 최대 폭 자리표시자

	// 확장 계획 - 게이트 위치와 양 끝에서 열릴 벽 타일. 작업 현황·작업 자리 계산이 쓴다.
	public sealed class GateExpansionPlan
	{
		public int Floor;
		public int GateIndex;
		public int NewWidth;
		public readonly List<Vector2Int> End0 = new List<Vector2Int>();
		public readonly List<Vector2Int> End1 = new List<Vector2Int>();
		public List<Vector2Int> EndWalls(int end) => end == 0 ? End0 : End1;
	}

	// 문(방 쪽 줄 묶음)이 속한 게이트의 확장 계획을 만든다. 확장은 기존 문 양 끝 벽에만 가능하다(2-9).
	public bool TryBuildExpansionPlan(InteractableObject door, out GateExpansionPlan plan, out string fail)
	{
		plan = null;
		fail = null;
		if (door == null || !IsBarrier(door) || IsFence(door)) { fail = "확장할 수 있는 문이 아닙니다."; return false; }
		if (Session.cmap == null || !TryGetGateKeyAt(door.Position, out int key)) { fail = "이 문이 놓인 통로 정보를 찾을 수 없습니다."; return false; }

		var p = new GateExpansionPlan { Floor = key / 100000, GateIndex = key % 100000 };
		if (!Session.cmap.TryGetGateExpansionWalls(p.Floor, p.GateIndex, GateExpansionStep, GateExpansionMaxWidth, p.End0, p.End1, out p.NewWidth, out fail)) return false;
		plan = p;
		return true;
	}

	// 실제 작업이 시작되면 문이 열리고, 중단·완료 뒤에도 열린 채 남는다. 접근만으로는 열리지 않는다(건설 명세 1-7·2-12).
	public void OpenForWork(InteractableObject door)
	{
		if (door == null || door.DoorManualOpen) return;
		door.DoorManualOpen = true;
		StateVersion++;
	}

	public void ClearWallKnowledge(Vector3Int tile) => ClearStaleWallCache(tile);

	private struct DoorRecord
	{
		public Vector3Int Pos;
		public FactionType Owner;
		public float MaxHp;
		public float Hp;
		public bool ManualOpen;
		public bool Target;
	}

	// 확장 완료: 양 끝 벽을 바닥으로 바꾸고 문을 새 폭으로 다시 세운다. 확장한 문은 최대 HP 배율을 곱한 새 최대 HP에서 남은 HP 비율을 유지하고, 반대편 문은 최대 HP를 그대로 두고 폭만 넓어진다.
	// 문 앞뒤 소유 진영·열림 상태는 그대로 잇는다. 반대편 문이 이미 파괴돼 있었으면 다시 만들지 않는다(개구부만 넓어진다). 새 문 오브젝트(확장한 쪽)를 돌려준다.
	public InteractableObject ApplyGateExpansion(InteractableObject door, GateExpansionPlan plan, float hpMultiplier)
	{
		if (door == null || plan == null || Session.cmap == null) return null;
		Floor floor = Session.cmap.map.floors[plan.Floor];
		Gate gate = floor.gates[plan.GateIndex];

		// 1) 지금 서 있는 문 묶음을 기록하고 제거한다.
		var records = new Dictionary<int, DoorRecord>();
		foreach (var group in GetGateDoorGroups(gate, plan.Floor))
		{
			var pos = new Vector3Int(group.Tiles[0].x, group.Tiles[0].y, plan.Floor);
			if (!Session.objectGrid.TryGetValue(pos, out InteractableObject d) || d == null || !IsBarrier(d) || IsFence(d)) continue;
			records[group.RoomId] = new DoorRecord { Pos = pos, Owner = d.DoorOwnerFaction, MaxHp = d.DoorMaxHp, Hp = d.DoorHp, ManualOpen = d.DoorManualOpen, Target = d == door };
		}
		foreach (var rec in records.Values) RemoveDoor(rec.Pos);

		// 2) 맵 데이터를 바꾸고 렌더링·그림자·안개·지식 캐시를 갱신한다.
		var opened = new List<Vector2Int>(plan.End0.Count + plan.End1.Count);
		opened.AddRange(plan.End0);
		opened.AddRange(plan.End1);
		Session.cmap.ApplyGateExpansionTiles(plan.Floor, plan.GateIndex, plan.NewWidth, opened);
		Session.OnGateTilesOpened(plan.Floor, opened);
		_gateTileIndex = null;
		_roomAnchorCache.Clear();
		_clearanceCache.Clear();
		StateVersion++;

		// 3) 새 폭의 문을 다시 세운다.
		Gate newGate = Session.cmap.map.floors[plan.Floor].gates[plan.GateIndex];
		float rotation = newGate.isHorizontal ? 90f : 0f;
		InteractableObject expanded = null;
		foreach (var group in GetGateDoorGroups(newGate, plan.Floor))
		{
			if (!records.TryGetValue(group.RoomId, out DoorRecord rec)) continue;
			var tiles = ToGridTiles(group, plan.Floor);
			if (AnyTileOccupied(tiles)) continue;
			SpawnDoorGroup(tiles, rotation, rec.Owner);
			if (!Session.objectGrid.TryGetValue(tiles[0], out InteractableObject nd) || nd == null) continue;

			float newMax = rec.Target ? rec.MaxHp * Mathf.Max(1f, hpMultiplier) : rec.MaxHp;
			nd.DoorMaxHp = newMax;
			nd.DoorHp = ConstructionRules.HpAfterMaxChange(rec.Hp, rec.MaxHp, newMax);
			nd.DoorManualOpen = rec.ManualOpen;
			if (rec.Target) expanded = nd;
		}
		LogHelper.Log(LogHelper.GAME, $"[문 확장] F{plan.Floor} 통로 {plan.GateIndex} 폭 -> {plan.NewWidth} (문 {records.Count}개 재구성)");
		return expanded;
	}
}
