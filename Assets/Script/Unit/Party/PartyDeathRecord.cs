using System.Collections.Generic;
using UnityEngine;

// 03문서 4-12~4-15장(2026-07-27 신규): 파티원 시체 발견 → 정신력 감소 → 사망 원인 확인 → 위험도 반영
// → 원인미상 수색까지 이어지는 한 건의 "사망 사건" 진행 상태. 시체 오브젝트 Id를 키로 Party.DeathRecords에
// 저장된다(Party가 "같은 웨이브에 진입한 인류는 하나의 파티로 취급"이라는 4-12장 전제를 그대로 구현).
public class PartyDeathRecord
{
	public string DeadUnitName;
	public Vector3Int DeathPosition;
	// 사망 순간 바라보던 방향 — 4-15장 원인미상 수색의 전방/후방/좌/우 분산 기준으로 재사용한다
	// (죽은 유닛은 Destroy되므로 나중에 다시 조회할 수 없어 사망 시점에 스냅샷으로 저장해둔다).
	public Dir DeadFacing;

	// 엔진상 실제 가해자(사용자에게는 "미확인"이어도 내부적으로는 이미 알고 있음 — 이 값 자체를
	// 플레이어/AI 판단에 그대로 노출하지 않고, 아래 CauseConfirmed가 true가 된 시점부터만 가중치
	// 이벤트에 실제로 사용한다. 4-14장 "정확히 확인된 경우로 한정" 요구사항을 지키기 위한 장치).
	public Unit CauseMonster;
	public bool CauseConfirmed;
	public bool CauseConfirmedDirectly; // true=목격(SEEN,+3) / false=간접 확인(INDIRECT,+1.5)
	public bool DangerApplied; // 이 사망 사건으로 위험도 이벤트를 이미 적용했는지(중복 방지)
	public string IncidentId;

	// 이 사망 정보를 이미 알고 있는(정신력 감소를 이미 적용받은) 파티원 이름 집합 — 재수신 시
	// 추가 감소 없음(4-13장)을 이 집합으로 보장한다.
	public readonly HashSet<string> InfoKnownUnits = new HashSet<string>();

	public bool UnknownCauseSearchTriggered;
}
