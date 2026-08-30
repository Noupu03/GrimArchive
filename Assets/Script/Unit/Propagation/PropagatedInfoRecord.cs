using UnityEngine;

// 07문서 3장 원칙에 따라 "전파 정보"(다른 유닛에게 전달받은 확인 정보)와 "일시 간접입력"(소리·공격
// 방향 임시 사건 정보) 두 계층을 담는다 — "직접 정보"는 PerceptionRecord/PersonalMapKnowledge가 같은
// 패턴(대상 키 기반·트리거 시점 갱신)으로 담당한다.

// 전파 정보 — 다른 유닛에게 전달받은 대상의 마지막 확인 위치·시각(13장: 전파 시점·위치를 확정하지
// 않아 직접 인지 PerceptionRecord와 분리 보관). "지금 어디로 알고 있나"는 이 클래스와
// PersonalMapKnowledge 중 타임스탬프가 최신인 쪽만 쓴다(13장 고유 규칙 — GetLatestKnownPosition 담당).
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

	// 가중치 시스템 E_HIT_HEAVY_INDIRECT 연결용 — 소리가 피격발생공격음/피격비명이고 실제
	// heavyHitThreshold 이상 피해일 때만 채워지며, 인지 판정 성공 시점(SoundAreaApproach)에 기록한다.
	public Unit Victim;
	public Unit Attacker;
	public bool IsHeavyHit;
	public string IncidentId; // 원본 RecordHitWeightEvent가 발급한 값 그대로(6장 전역 반영 그룹화 공유)
}
