// 이벤트 발생 1건의 기록 ("영수증"). 유닛이 사건을 겪을 때마다 하나씩 쌓이고,
// 웨이브 종료 시 생존자들의 IncidentEntry를 모아 6장 생존자 전역 반영 계산의 입력으로 쓴다.
//
// EventID: 연산공식 문서 3장의 이벤트 종류 ID (WeightEnums.EventId)
// IncidentID: 실제로 발생한 "사건" 하나를 가리키는 고유 ID (10장/6-2장) — 같은 사건을 여러 유닛이
//             동시에 겪었을 때(직접 목격 여러 명 등) 이 값으로 묶는다. 호출부에서 발급/전달한다.
public class IncidentEntry
{
	public string IncidentId;
	public EventId EventId;
	public string TargetId;
	public WeightType WeightType;
	public InfoType InfoType;
	public float ChangeValue;
	public MentalErrorState MentalStateAtRecord;
	public string ObserverUnitName; // 디버그 추적용 (어느 유닛이 겪었는지)

	public IncidentEntry(string incidentId, EventId eventId, string targetId, WeightType weightType,
		InfoType infoType, float changeValue, MentalErrorState mentalState, string observerUnitName)
	{
		IncidentId = incidentId;
		EventId = eventId;
		TargetId = targetId;
		WeightType = weightType;
		InfoType = infoType;
		ChangeValue = changeValue;
		MentalStateAtRecord = mentalState;
		ObserverUnitName = observerUnitName;
	}

	// 6-2장 동일 수치 정보 중복 판정 키 (EventID/IncidentID/TargetID/WeightType/InfoType/ChangeValue 전부 동일하면 중복)
	public string DedupKey => $"{IncidentId}|{EventId}|{TargetId}|{WeightType}|{InfoType}|{ChangeValue}";
}
