using System.Collections.Generic;

namespace GrimArchive.Wave
{
	public readonly struct UnitCount
	{
		public readonly string UnitType;
		public readonly int Count;
		public UnitCount(string unitType, int count) { UnitType = unitType; Count = count; }
	}

	// 파티 한 개의 편성 계획 — 이전 웨이브 생존자가 합류하기 전의 전체 구성이다.
	public sealed class PartyPlan
	{
		public string Name;
		public PartyConcept Concept;
		public PartyType Mission;
		public int Stage;
		public bool IsIndependent; // 리더 없는 초기 독립 유닛
		public int TargetSize;
		public readonly List<UnitCount> Members = new List<UnitCount>();
		public QuotaTargets Quota;
		public int ReinforcementCount; // 점령 거점 충원 부족분 — 표시만 한다(편입은 미구현)

		public int MemberTotal
		{
			get { int n = 0; foreach (var m in Members) n += m.Count; return n; }
		}

		public override string ToString()
		{
			var sb = new System.Text.StringBuilder();
			sb.Append(Name).Append(" — 단계 ").Append(Stage).Append(", 규모 ").Append(TargetSize).Append("명: ");
			for (int i = 0; i < Members.Count; i++)
			{
				if (i > 0) sb.Append(", ");
				sb.Append(Members[i].UnitType).Append(" x").Append(Members[i].Count);
			}
			return sb.ToString();
		}
	}

	// 지금은 원정 파티 1개만 스폰·추적하지만 다중 파티로 넓힐 수 있게 목록으로 둔다.
	public sealed class WavePlan
	{
		public int WaveNumber;
		public int Stage;
		public readonly List<PartyPlan> Parties = new List<PartyPlan>();

		public PartyPlan Primary => Parties.Count > 0 ? Parties[0] : null;
	}
}
