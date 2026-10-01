#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

// ========================================================================
// 2026-10-01 플레이 로그(101~107줄): 진형 계획이 중단되면 ReadyToAdvance가 이미 소비돼 있고 AdvanceFromRoom은 리더가 방을 떠나야 풀려 파티가 영구히 멈췄다.
// 중단 정책 — 시도 한도(PartyAdvanceSystem.MaxAttempts) 안에서는 ReadyToAdvance를 되살려 재시도하고, 한도에 닿으면 같은 신호로 HumanWaveManager가 예전 방식으로 폴백한다.
// 세션·유닛 없이 Party와 빈 계획 객체만으로 확인한다(Ranks가 비어 있어 Finish가 유닛을 건드리지 않는다).
// ========================================================================

public class PartyAdvanceFlowTests
{
	private static Party NewPartyWithPlan(int attempts)
	{
		var party = new Party("p", "테스트 파티") { AdvanceAttempts = attempts };
		party.AdvancePlan = new PartyAdvancePlan();
		return party;
	}

	[Test]
	public void Abort_WhileAttemptsRemain_RestoresReadyToAdvance_ForRetry()
	{
		var party = NewPartyWithPlan(attempts: 1);
		Assert.Less(party.AdvanceAttempts, PartyAdvanceSystem.MaxAttempts);

		PartyAdvanceSystem.Abort(party, "테스트");

		Assert.IsNull(party.AdvancePlan);
		Assert.IsTrue(party.ReadyToAdvance, "재시도를 위해 이동 지시가 되살아나야 한다");
	}

	[Test]
	public void Abort_AtAttemptLimit_StillRestoresReadyToAdvance_SoTheLegacyFallbackRuns()
	{
		var party = NewPartyWithPlan(attempts: PartyAdvanceSystem.MaxAttempts);

		PartyAdvanceSystem.Abort(party, "테스트");

		Assert.IsNull(party.AdvancePlan);
		Assert.IsTrue(party.ReadyToAdvance, "한도에 닿아도 지시는 살아 있어야 HumanWaveManager가 예전 방식으로 이동시킨다");
		Assert.GreaterOrEqual(party.AdvanceAttempts, PartyAdvanceSystem.MaxAttempts);
	}

	[Test]
	public void Abort_WithoutRetry_LeavesReadyToAdvanceOff()
	{
		var party = NewPartyWithPlan(attempts: 1);

		PartyAdvanceSystem.Abort(party, "퇴각 전환", retry: false);

		Assert.IsNull(party.AdvancePlan);
		Assert.IsFalse(party.ReadyToAdvance, "퇴각·리더 없음처럼 이동을 이어 갈 수 없는 중단은 되살리지 않는다");
	}

	[Test]
	public void Abort_WithoutPlan_DoesNothing()
	{
		var party = new Party("p", "테스트 파티") { AdvanceAttempts = 1 };

		PartyAdvanceSystem.Abort(party, "테스트");

		Assert.IsFalse(party.ReadyToAdvance);
		Assert.AreEqual(1, party.AdvanceAttempts);
	}
}
#endif
