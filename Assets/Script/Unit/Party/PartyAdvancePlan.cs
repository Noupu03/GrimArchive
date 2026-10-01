using System.Collections.Generic;
using UnityEngine;

public enum AdvancePhase { FormingUp, Breaching, Entering }

// 집결을 마친 파티가 다음 방으로 가는 한 번의 진행 — 문 앞 진형(FormingUp) → 문 파괴(Breaching, 막힌 문이 있을 때만) → 랭크 순 입장(Entering).
// Party.AdvancePlan이 들고 있고 끝나면(입장 완료·중단) null이 된다. 데이터만 담고, 전이는 PartyAdvanceSystem·파괴 지시는 PartyBreachCommand·유닛 한 틱은 PartyAdvanceSteps가 맡는다.
public class PartyAdvancePlan
{
	public AdvancePhase Phase;
	public int Floor;
	public Vector2Int DoorPos;
	// 리더 방에서 다음 방으로 향하는 축(near 줄 → far 줄 방향, 단위 벡터).
	public Vector2Int Forward;
	// 게이트의 문 타일 두 줄 — near가 리더 방 쪽, far가 다음 방 쪽. 문은 줄마다 오브젝트 하나(1×2)다.
	public Vector2Int[] NearTiles;
	public Vector2Int[] FarTiles;
	public Room FromRoom;
	public Room ToRoom;
	// 진형 대기를 건너뛰고 곧바로 돌파로 시작한 시도(재시도) — 진형 정체와 무관하게 전원이 공격 자리로 간다.
	public bool Direct;

	// ── 진형·입장 ──
	public readonly Dictionary<Human, int> Ranks = new Dictionary<Human, int>();
	// 진형 자리(입장 전·돌파 중 자리가 없는 유닛의 대기 위치) — 입장 단계에서 아직 출발 허가가 없는 랭크도 여기서 기다린다.
	public readonly Dictionary<Human, Vector2Int> FormSlots = new Dictionary<Human, Vector2Int>();
	// 진형/입장 구역 — 자리 배정에 실제로 쓴 범위(중심 + 가장 먼 자리까지의 반경). 좁아서 자기 자리에 못 들어간 유닛이 구역 안이면 그 자리에서 준비 완료로 인정하는 기준이다.
	public Vector2Int FormCenter;
	public int FormRadius = 1;
	public Vector2Int EntryCenter;
	public int EntryRadius = 1;
	// 입장 단계에서 문을 지나 개인 행동으로 풀린 유닛 수(로그용).
	public int EnteredCount;

	// ── 돌파(리더의 문 파괴 지시) ──
	// 지금 부수는 행(0 near, 1 far, -1 아직 정해지지 않음)과 그 행에 달라붙도록 지시받은 파티원의 공격 자리.
	public int BreachRow = -1;
	public readonly Dictionary<Human, Vector2Int> AttackSlots = new Dictionary<Human, Vector2Int>();
	// 지시를 다시 낼 때가 됐다 — 목표 행 변경·멤버 변경·자리 포기가 켠다. 주기적 재지시는 NextAssignTime.
	public bool AssignDirty;
	public float NextAssignTime;
	// 진행 감시 — 목표 행 문 체력이 줄어든(또는 행이 바뀐) 마지막 시각과 그때의 체력.
	public float LastProgressTime;
	public float LastDoorHp = float.MaxValue;
	// 로그 중복 방지 — 마지막으로 남긴 지시의 (행, 투입 인원).
	public int LastLoggedRow = -1;
	public int LastLoggedCount = -1;

	// ── 시계 ──
	public float PhaseStartTime;
	public float NextTickTime;
	public float NextDiagTime;

	public Vector2Int[] RowTiles(int row) => row == 0 ? NearTiles : FarTiles;

	// 지금 부수는 문의 대표 타일(없으면 null) — 문 오브젝트는 줄당 하나라 어느 칸이든 같은 오브젝트다.
	public Vector3Int? BreachTile
	{
		get
		{
			if (BreachRow < 0) return null;
			var t = RowTiles(BreachRow)[0];
			return new Vector3Int(t.x, t.y, Floor);
		}
	}
}
