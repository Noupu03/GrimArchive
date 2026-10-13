using System.Collections.Generic;
using UnityEngine;

// 후퇴·도주 이동 한 유닛의 진행 상태(RetreatMovement가 읽고 쓴다) — 목적지, 같은 차단 구간의 우회 누적, 차단 대응 대상, 정체 카운터.
public sealed class RetreatMoveState
{
	public RetreatDestination DestinationKind;
	public Vector2Int Destination;
	public int DestinationFloor = -1;

	// 우회 구간(명세서 5-2장): 길이 막히기 시작할 때 잰 기준 경로 길이와 그 뒤 걸은 칸. 구간이 끝날 때까지 기준을 유지해 재탐색으로 한도를 초기화하지 않는다.
	public int SegmentBaseline = -1;
	public int SegmentWalked;

	public float NextPathCheckTime;
	public float NextDestinationRefreshTime;
	public int StuckTurns;
	public bool ForcePassTrap; // 우회·파괴가 안 돼 피해를 감수하고 함정을 지나가는 중(예상 피해를 알고 통과 후 HP > 0일 때만)

	// 차단 대응: 부술 문/함정 위치, 뚫어야 하는 적. Kind가 None이면 대응 중이 아니다.
	public RetreatBlockKind Block;
	public Vector3Int BlockObjectPos;
	public Unit BlockEnemy;
	public float BlockSince;

	public void ResetSegment() { SegmentBaseline = -1; SegmentWalked = 0; }

	public void ClearBlock()
	{
		Block = RetreatBlockKind.None;
		BlockEnemy = null;
		BlockSince = 0f;
		ForcePassTrap = false;
	}

	public void Reset()
	{
		DestinationKind = RetreatDestination.None;
		DestinationFloor = -1;
		StuckTurns = 0;
		NextPathCheckTime = 0f;
		NextDestinationRefreshTime = 0f;
		ResetSegment();
		ClearBlock();
	}
}

// 후퇴·도주 시스템의 유닛별 상태(후퇴도주 문서 2026-10-13) — PropagationComponent와 같은 패턴(Unit.Components에 등록, Unit이 캐시 프로퍼티로 노출). 침공(웨이브) 단위 이력이라 실제 던전 밖 이탈 시 ResetForNewInvasion이 비운다.
public class RetreatComponent : IUnitComponent
{
	private Unit _owner;

	// 이 유닛 자신의 후퇴·도주 상태 — PersonalRetreat/PersonalFlee만 쓴다(파티 후퇴는 Party.Retreat).
	public RetreatMode Mode;
	public float ModeSince;

	// 개인 도주 판정 상태(확률 차수·미사용 공황 감소량·확정 여부).
	public readonly FleeRollState FleeRoll = new FleeRollState();

	// 몬스터 직접 조우 충격 이력.
	public readonly EncounterShockTracker Encounters = new EncounterShockTracker();

	// 파티원 도주 확인·보고로 정신력이 줄어든 사건(도주자 이름) — 같은 인류·같은 도주 사건에는 1회만 적용한다(명세서 7-1장, R62~R67). 정신력 회복·방 이동으로 다시 만들지 않는다.
	public readonly HashSet<string> WitnessedFlees = new HashSet<string>();

	// 파티 후퇴: 리더의 후퇴 명령을 받았는가(집결 명령과 같은 전달 범위 규칙, Party.IsReachedByLeaderCommand) / 후퇴 목적 거점에 도착해 휴식 대상인가 / 이 유닛의 휴식 자리.
	public bool Ordered;
	public bool AtBase;
	public Vector2Int? RestSlot;
	public float AtBaseSince;

	// 이동 진행 상태(차단 대응·우회 누적).
	public readonly RetreatMoveState Move = new RetreatMoveState();

	// 관찰 틱 스로틀(RetreatSystem.Tick).
	public float NextObserveTime;

	public bool IsFleeing => Mode == RetreatMode.PersonalFlee;
	public bool IsPersonallyRetreating => Mode == RetreatMode.PersonalRetreat;

	public RetreatComponent() { }

	public RetreatComponent(Unit owner)
	{
		_owner = owner;
	}

	// 파티 후퇴 명령·거점 도착·이동 진행 상태만 비운다(개인 도주 확률·조우 이력은 침공 단위라 유지).
	public void ClearPartyRetreatState()
	{
		Ordered = false;
		AtBase = false;
		RestSlot = null;
		AtBaseSince = 0f;
		Move.Reset();
	}

	public void ResetForNewInvasion()
	{
		Mode = RetreatMode.None;
		ModeSince = 0f;
		FleeRoll.ResetForNewInvasion();
		Encounters.ResetForNewInvasion();
		WitnessedFlees.Clear();
		NextObserveTime = 0f;
		ClearPartyRetreatState();
	}

	public void OnUpdate(float deltaTime) { }
	public void OnDespawn() { }
}
