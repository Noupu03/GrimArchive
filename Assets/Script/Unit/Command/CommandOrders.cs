using UnityEngine;

// 리더 명령을 "받은" 쪽의 개인 상태(LeaderComponent가 들고 있다) — 파티원 개인이 받은 집중 공격 지정과 행동 우선 명령.
// 같은 명령의 재전달은 CommandId로 가려 적용시간·추적 시작 시각을 새로 정하지 않는다(4-1·9-표2·L10).

// 개인이 받은 집중 공격(또는 통로 차단물 공동 파괴) 지정. 고정된 총 지속시간이 없고 개인별 추적 기한과 종료 조건으로 끝난다(11-1·12장).
public sealed class FocusOrder
{
	public string CommandId;
	public TacticalCommand Command;       // FocusAttack / SuperiorFocusAttack / ObjectFocus
	public CommandAuthority Authority;    // 일반 리더 명령인가 상위 명령인가
	public string IssuerName;             // 명령한 지휘자 이름(지휘 상실 시 이 이름으로 종료한다)
	public float ReceivedAt;

	public Unit Target;                   // 적 대상(ObjectFocus가 아니면)
	public Vector3Int? ObjectTarget;      // 차단물 대상(ObjectFocus)
	public Vector2Int LastKnownPosition;  // 마지막으로 직접 확인한(또는 명령 시점에 알려 준) 대상 위치
	public bool EverSeen;                 // 명령을 받은 뒤 직접 본 적이 있는가
	public float LostAt = -1f;            // 위치를 놓친 시각(보는 중이면 −1). 한 번도 못 봤으면 ReceivedAt이 추적 시작
	public float ArrivedAt = -1f;         // 마지막 위치에 도착한 시각(확인 대기 시작)
	public bool Suspended;                // 상위 명령과 충돌해 잠시 중단(종료 조건·기한은 계속 계산된다, 16-1장)
	public float PathBlockedSince = -1f;  // 허용된 경로를 못 찾기 시작한 시각 — 모든 허용 방법이 불가능하다고 확인되면 이 파티원의 지정을 끝낸다(11-1장)
	public int PursuitStuckTurns;         // 마지막 위치로 가는 이동이 진전 없이 끝난 연속 횟수

	public bool IsObject => ObjectTarget.HasValue;

	// 추적 시계의 시작 시각 — 놓친 때부터, 한 번도 못 봤으면 명령을 받은 때부터(12-2-표1).
	public float PursuitStart => LostAt >= 0f ? LostAt : ReceivedAt;
}

// 개인이 받은 행동 우선 명령(강공격·제압·보호) — 받은 시점부터 5초 동안 기술 선택 순서를 바꾼다. 리더 본인은 발령 시점부터 5초.
public sealed class ActionOrder
{
	public string CommandId;
	public TacticalCommand Command;
	public CommandAuthority Authority;
	public string IssuerName;
	public float ReceivedAt;
	public float ExpiresAt;
	public Unit SuppressTarget;           // 제압 대상 적 한 명(강공격·보호는 null — 보호는 대상을 지정하지 않는다)

	public bool IsActive(float now) => now < ExpiresAt;
}
