using UnityEngine;

// 02문서 4장: "인지 판정은 매 프레임 반복 처리가 아니다" — 관찰자가 대상별(적 유닛=Unit 참조,
// 오브젝트=InteractableObject.Id)로 들고 있는 지속 인지 상태. 명확한 트리거 시점(최초 진입/재진입/
// 완전 차단 후 재등장/수상한 타일 2칸 접근/피격 후 가시성 증가)에만 다시 판정하고, 그 사이엔 이
// 레코드의 Outcome을 그대로 유지한다. Unit.perceptionRecords(관찰자별 Dictionary)에 저장된다.
public class PerceptionRecord
{
	public PerceptionOutcome Outcome = PerceptionOutcome.Unrecognized;

	// 직전 판정 패스에서 이 대상이 "인지 범위 안 + 완전 차단 아님" 상태로 실제 도달됐는지. 이 값이
	// false→true로 바뀌는 순간이 4장의 "최초 진입/재진입/완전 차단 후 재등장" 3가지 트리거를 전부
	// 커버한다(셋 다 "지금 안 보이다가 다시 보임"이라는 동일한 신호이기 때문 — 판단 근거는
	// 구현현황 문서 기재).
	public bool WasInRange;

	// 20~22장: 이 대상이 지금 "수상한 타일" 확인 대기 중인지. true인 동안 관찰자는 경계 상태(코드상
	// Unit.IsAlert가 이 플래그를 참조)이고, 2칸 이내로 접근하면 재판정 트리거가 걸린다.
	public bool PendingSuspiciousInvestigation;

	public Vector3Int LastKnownTile;

	public bool IsSuspicious => Outcome == PerceptionOutcome.SuspiciousTile;

	// 21장: 저장값이 아니라 판단 입력값이라는 문서 표현 그대로 — PersonalMapKnowledge의 영구 타일
	// 위험도/흥미도에 기록하지 않고, 필요할 때(04 문서의 소비자)마다 이 레코드에서 즉시 계산해 쓴다.
	public float TempDanger => IsSuspicious ? PerceptionMath.SuspiciousTileTempDanger : 0f;
	public float TempInterest => IsSuspicious ? PerceptionMath.SuspiciousTileTempInterest : 0f;
}
