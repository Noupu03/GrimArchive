using UnityEngine;

// 03문서 7-3장(2026-07-27 신규): 리더가 코어를 조사하는 동안의 하위 상태. 리더 전용(Human.
// currentCoreInteraction) — 일반 유닛은 코어를 직접 조사하지 않고 전파만 한다(CorePartySystem 참고).
public class CoreInteractionState
{
	public string CoreObjectId;
	public Vector3Int CorePosition;

	// 코어 위치 도착 후 1초 동안 파티 목표 합류 정보를 전파한 뒤 조사를 시작한다(7-2/7-3장 공통 패턴).
	public bool PropagationDone;
	public float PropagationTimer;

	// 조사 진행도(0~1) — 문서가 코어 조사 자체의 소요시간을 명시하지 않아 일반 조사(5-4장)와 같은
	// 내부 판단값(ExplorationMath.CoreInvestigateDurationSeconds)을 재사용한다.
	public float Progress01;

	// TacticalFSMState.CoreInvestigatePerform이 매 틱 true로 세팅 — UnitFunction.OnUpdate는 이 값이
	// true일 때만(=코어 위치에 실제로 도달해 수행 중일 때만) 전파/진행도 타이머를 흘려보낸다
	// (InvestigationState.PenaltyActive와 동일한 패턴).
	public bool Active;
}
