using UnityEngine;

// 몬스터 "종"(UnitType.typeName 기준) 하나가 들고 있는 전역 누적 가중치. (4-3장/7장/10장/11장/12-1장/14장)
public class SpeciesWeightState
{
	public string SpeciesKey; // UnitType.typeName (예: "근접 탱커")

	public float UnderstandingStored;  // 종별 이해도 (0~100)
	public int LastUnderstandingUpdateWave; // 8장 감소 대상 선정을 위한 최종 갱신 웨이브

	public float DangerAccumulatedStored; // 종별 위험도 누적값 (12-1장 최종위험도 구성요소, 0~999 클램프는 조회 시점에)

	// 14장. 특수 행동 누적 상한 (소환/버프/디버프 각각 종별로 관리, 상한 10)
	public const float SpecialActionCap = 10f;
	public float SummonAccum;
	public float BuffAccum;
	public float DebuffAccum;

	public SpeciesWeightState(string speciesKey)
	{
		SpeciesKey = speciesKey;
	}

	public void AccumulateSummon(float delta) => SummonAccum = Mathf.Clamp(SummonAccum + delta, 0f, SpecialActionCap);
	public void AccumulateBuff(float delta) => BuffAccum = Mathf.Clamp(BuffAccum + delta, 0f, SpecialActionCap);
	public void AccumulateDebuff(float delta) => DebuffAccum = Mathf.Clamp(DebuffAccum + delta, 0f, SpecialActionCap);
}

// 보스/네메시스 등 특수 유닛 개체 하나가 들고 있는 개별 누적 가중치. (7-1장/12-1장)
public class IndividualWeightState
{
	public string UnitId; // 개체 고유 식별자 (예: Unit.name — 유닛 인스턴스별로 고유하게 붙는 이름)

	public float UnderstandingStored; // 개별 이해도 (0~100, 7-1장에서 종별과 각각 50 상한으로 합산)
	public int LastUnderstandingUpdateWave;

	public float DangerAccumulatedStored; // 개별 위험도 누적값

	public IndividualWeightState(string unitId)
	{
		UnitId = unitId;
	}
}
