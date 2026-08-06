using UnityEngine;

// 07문서 3장: "정보는 파티 전체가 하나로 공유하지 않고 유닛별로 개별 보유한다." 이 파일은 그중
// "전파 정보"(다른 유닛에게 전달받은 확인 정보)와 "일시 간접입력"(소리·공격 방향으로 얻은 임시 사건
// 정보) 두 계층을 담는다 — "직접 정보"는 기존 PerceptionRecord(적 유닛)/PersonalMapKnowledge(지도)가
// 이미 담당하므로 건드리지 않는다. PerceptionRecord.cs와 동일 패턴(대상 키 기반, 지속 상태를 트리거
// 시점에만 갱신)을 따른다.

// 전파 정보 — 다른 유닛에게 전달받은 대상(적 유닛 Unit 참조 또는 InteractableObject.Id)의 마지막
// 확인 위치·시각. 13장: 전파 시점이 새로운 확인 시점으로 취급되지 않으며, 전달된 위치를 대상의 현재
// 위치로 확정하지 않는다 — 그래서 "직접 인지"(PerceptionRecord)와 분리된 별도 기록이다.
//
// 저장소 분리 vs 판단 시점 병합(사용자 결정, 2026-08-06): 저장소 자체(이 클래스 vs PersonalMapKnowledge)는
// 계속 분리하되, "이 대상을 지금 어디로 알고 있나" 판단은 두 저장소 중 타임스탬프가 더 최신인 쪽만
// 쓴다 — 24장 PriorityRank(직접이 오래됐어도 간접보다 우선)가 아니라 13장 자체의 순수 최신성 규칙.
// PropagationSystem.GetLatestKnownPosition이 이 비교를 담당한다.
public class PropagatedInfoRecord
{
	public Vector3Int LastKnownTile;
	public float LastKnownTimestamp;

	// 24장 우선순위 판정(WeightMath.PriorityRank/ShouldReplace)에 그대로 넘길 수 있도록 이 정보가
	// 어떤 InfoType으로 들어왔는지 남겨둔다(전파 정보는 항상 Indirect).
	public InfoType SourceInfoType = InfoType.Indirect;
}

// 일시 간접입력 — 소리 또는 공격 방향으로 얻은, 확인 행동이 끝나면 사라지는 임시 사건 정보.
// 유닛당 최대 1건(07-A 7-3장)만 보류되므로 컬렉션이 아니라 단일 필드로 둔다.
public class PendingSoundReaction
{
	public SoundType Type;
	public Vector2Int SourcePosition;      // 방향 계산·접근 이동에 쓰는 실제 사건 위치
	public bool HasEstimatedArea;
	public Vector3Int EstimatedAreaCenter; // 6장: 원형 추정 지역의 중심(정확 위치로 확정하지 않음)
	public int EstimatedAreaRadius;
	public float DetectedAtTime;
	public float ValidUntilTime;           // 7-3장: 발생 후 5초 — 이 시각을 넘기면 확인 행동을 새로 시작 못함
	public bool ResponseStarted;           // AlertSearchState로 승격돼 확인 행동을 이미 시작했는지

	// 가중치 시스템 E_HIT_HEAVY_INDIRECT 연결용(2026-08-05, 구현현황 문서 "다음 우선순위 1번") — 소리가
	// 피격 발생 공격음/피격 비명이고 실제로 몬스터가 인류에게 heavyHitThreshold 이상 피해를 준 경우에만
	// 채워진다. 확인 행동이 인지 판정에 성공한 순간(SoundAreaApproach) 이 정보로 간접 이벤트를 기록한다.
	public Unit Victim;
	public Unit Attacker;
	public bool IsHeavyHit;
	public string IncidentId; // 원본 RecordHitWeightEvent가 발급한 값 그대로(6장 전역 반영 그룹화 공유)
}
