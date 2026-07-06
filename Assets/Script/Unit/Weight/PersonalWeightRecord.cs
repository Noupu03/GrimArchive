using UnityEngine;

// 유닛 개인이 특정 대상(TargetId)에 대해 들고 있는 가중치 하나. (4장 개인 가중치 계산의 저장소)
// Unit.personalWeights에 (TargetId, WeightType) 복합 키로 보관된다.
public class PersonalWeightRecord
{
	public string TargetId;
	public WeightType Type;

	// 1장: 내부적으로 소수점까지 저장, 조회/판단에는 정수 적용값 사용.
	public float StoredValue;
	public int AppliedValue => Mathf.FloorToInt(StoredValue);

	// 이 기록이 마지막으로 갱신된 정보 유형 (24장 우선순위 판단에 사용)
	public InfoType LastInfoType;

	// 오차 재현을 위해, 기록 당시(혹은 최신 갱신 시점) 유닛의 정신 상태를 같이 들고 있는다 (9-2장/23장).
	public MentalErrorState MentalStateAtRecord;

	public PersonalWeightRecord(string targetId, WeightType type)
	{
		TargetId = targetId;
		Type = type;
	}

	public static string MakeKey(string targetId, WeightType type) => targetId + "#" + type;
}
