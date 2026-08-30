// 이벤트 발생 1건의 기록("영수증"). 유닛이 사건을 겪을 때마다 쌓이고, 웨이브 종료 시 생존자들의
// IncidentEntry를 모아 6장 생존자 전역 반영 계산의 입력으로 쓴다. IncidentId는 같은 사건을 여러
// 유닛이 동시에 겪었을 때 묶는 고유 키(10장/6-2장, 호출부가 발급)이고, EventId는 3장 이벤트 종류 ID다.
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

	// 기록 당시 대상이 특수 유닛(보스/네메시스, 개별 누적)이었는지 — 일반 유닛(종별 누적)인지.
	// OnWaveEnd가 종/개별 저장소 중 어디에 반영할지 판단하는 데 쓴다.
	public bool IsIndividualTarget;

	public IncidentEntry(string incidentId, EventId eventId, string targetId, WeightType weightType,
		InfoType infoType, float changeValue, MentalErrorState mentalState, string observerUnitName, bool isIndividualTarget = false)
	{
		IncidentId = incidentId;
		EventId = eventId;
		TargetId = targetId;
		WeightType = weightType;
		InfoType = infoType;
		ChangeValue = changeValue;
		MentalStateAtRecord = mentalState;
		ObserverUnitName = observerUnitName;
		IsIndividualTarget = isIndividualTarget;
	}

	// 6-2장 동일 수치 정보 중복 판정 키 (EventID/IncidentID/TargetID/WeightType/InfoType/ChangeValue 전부 동일하면 중복)
	public string DedupKey => $"{IncidentId}|{EventId}|{TargetId}|{WeightType}|{InfoType}|{ChangeValue}";
}
