using System.Collections.Generic;
using UnityEngine;

public enum AdvancePhase { FormingUp, Breaching, Entering }

// 집결을 마친 파티가 다음 방으로 가는 한 번의 진행 — 문 앞 진형(FormingUp) → 문 파괴(Breaching, 막힌 문이 있을 때만) → 랭크 순 입장(Entering). Party.AdvancePlan이 들고 있고 끝나면 null이며, 데이터만 담는다(전이는 PartyAdvanceSystem, 파괴 지시는 PartyBreachCommand, 유닛 한 틱은 PartyAdvanceSteps).
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
	// 각 줄의 가운데 타일 — 진형·입장 자리와 통과 판정의 기준점.
	public Vector2Int NearAnchor => NearTiles[(NearTiles.Length - 1) / 2];
	public Vector2Int FarAnchor => FarTiles[(FarTiles.Length - 1) / 2];
	public Room FromRoom;
	public Room ToRoom;

	// ── 진형·입장 ──
	public readonly Dictionary<Human, int> Ranks = new Dictionary<Human, int>();
	// 입장 해제 순서(04번 8장 좁은 통로: 전방 근접 0 → 근접 지원 1 → 원거리 공격 2 → 원거리 지원 3, 리더도 자기 역할) — 자리 기하용 Ranks(3랭크)와 따로 둔다.
	public readonly Dictionary<Human, int> EntryTiers = new Dictionary<Human, int>();
	// 진형 자리(입장 전·돌파 중 자리가 없는 유닛의 대기 위치) — 입장 단계에서 아직 출발 허가가 없는 랭크도 여기서 기다린다.
	public readonly Dictionary<Human, Vector2Int> FormSlots = new Dictionary<Human, Vector2Int>();
	// 진형/입장 구역 — 자리 배정에 실제로 쓴 범위(중심 + 가장 먼 자리까지의 반경). 좁아서 자기 자리에 못 들어간 유닛이 구역 안이면 그 자리에서 준비 완료로 인정하는 기준이다.
	public Vector2Int FormCenter;
	public int FormRadius = 1;
	public Vector2Int EntryCenter;
	public int EntryRadius = 1;
	// 입장 단계에서 문을 지나 개인 행동으로 풀린 유닛 수(로그용).
	public int EnteredCount;
	// 입장 단계에서 다음 유닛을 출발시킬 수 있는 시각(entryReleaseIntervalSeconds 간격) — BeginEntering이 단계 시작 시각으로 초기화한다.
	public float NextEntryReleaseTime;
	// 입장 지시를 낸 시각(교전·경계 정지로 밀리지 않는다) — 이후에 얻은 리더 정보와 그 전의 낡은 정보를 가르는 기준(PartyAdvanceSystem.InformStragglersOfGate).
	public float EntryCommandTime;

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

	// 돌파 실패(30초 무진행) 뒤 진형으로 물러났다 다시 시도한 횟수, 그리고 다음 돌파를 시작할 수 있는 시각(진형 복귀 후 쿨다운).
	public int BreachCycles;
	public float RetryNotBefore;

	// ── 시계 ──
	public float PhaseStartTime;
	public float NextTickTime;
	public float NextDiagTime;
	// 교전·경계 중 시간 제한 정지(PartyEngagement) — 마지막 시계 갱신 시각, 이번 단계에서 멈춰 준 누적 시간, 정지 시작 로그를 이미 남겼는지.
	public float LastClockTime;
	public float PausedSeconds;
	public bool PauseLogged;

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
