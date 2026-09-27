using UnityEngine;

// 유닛 개인이 특정 종의 특정 스킬에 대해 들고 있는 "예상 피해량" 하나. (02번 문서 9번 항목/07번
// 검증항목 02-01 — 입장 기록의 예상 공격 피해량 + 직접 경험 확정)
// PersonalWeightRecord와 필드 구성·갱신 규칙은 동일하되, WeightType(이해도/위험도/흥미도) 파이프라인
// (WeightMath.Clamp/WeightEventTable/웨이브종료 풀캡)과는 무관한 별도 값이라 분리된 클래스를 쓴다 —
// 이쪽은 "공식값 시딩 → 1회 관측치로 통째 교체" 구조라 이벤트 델타 누적 파이프라인에 맞지 않는다.
// Unit.personalSkillDamage에 (SpeciesKey, SkillName) 복합 키로 보관된다.
public class PersonalSkillDamageRecord
{
	public string SpeciesKey;
	public string SkillName;

	public float StoredValue;
	public int AppliedValue => Mathf.FloorToInt(StoredValue);

	public InfoType LastInfoType;
	public MentalErrorState MentalStateAtRecord;

	// 23장: 오차 적용된 "관찰자가 실제로 믿는" 값 — StoredValue/LastInfoType/MentalStateAtRecord가
	// 갱신될 때만 재계산해 그 사이엔 고정한다. 조회는 전부 이 값을 쓴다.
	public int PerceivedValue;

	public PersonalSkillDamageRecord(string speciesKey, string skillName)
	{
		SpeciesKey = speciesKey;
		SkillName = skillName;
	}

	public static string MakeKey(string speciesKey, string skillName) => speciesKey + "#" + skillName;
}
