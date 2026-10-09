#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

// ========================================================================
// 유닛 애니메이션 슬롯(Script/Unit/Visual/Animation) — 순수 판정 고정.
// 슬롯 목록(AnimSlotCatalog), 루프 슬롯 선택·트리거 우선순위·속도 맞춤·크로스페이드 가중치·이동 디바운스.
// 실제 재생(PlayableGraph)은 Unity 플레이로만 확인된다 — 여기선 "어떤 슬롯이 어떤 조건에서 선택되는가"만 고정한다.
// ========================================================================

public class UnitAnimMathTests
{
	private const float Eps = 1e-4f;

	private static System.Func<AnimSlot, bool> Has(params AnimSlot[] slots)
	{
		var set = new HashSet<AnimSlot>(slots);
		return s => set.Contains(s);
	}

	// ── 슬롯 목록 ───────────────────────────────────────────────

	[Test]
	public void SlotsFor_Human_IncludesHumanOnlySlots_AndSkillsInOrder()
	{
		var list = AnimSlotCatalog.SlotsFor(true, new[] { "집중 찌르기", "방패 타격" });
		var slots = list.Select(x => x.slot).ToList();

		CollectionAssert.Contains(slots, AnimSlot.Investigate);
		CollectionAssert.Contains(slots, AnimSlot.DisarmTrap);
		CollectionAssert.Contains(slots, AnimSlot.PickUp);
		CollectionAssert.Contains(slots, AnimSlot.UseStairs);

		var skills = list.Where(x => x.slot == AnimSlot.Skill).Select(x => x.skillName).ToList();
		CollectionAssert.AreEqual(new[] { "집중 찌르기", "방패 타격" }, skills);
	}

	[Test]
	public void SlotsFor_Monster_ExcludesHumanOnlySlots()
	{
		var slots = AnimSlotCatalog.SlotsFor(false, new[] { "육중한 내리찍기" }).Select(x => x.slot).ToList();

		CollectionAssert.DoesNotContain(slots, AnimSlot.Investigate);
		CollectionAssert.DoesNotContain(slots, AnimSlot.DisarmTrap);
		CollectionAssert.DoesNotContain(slots, AnimSlot.PickUp);
		CollectionAssert.DoesNotContain(slots, AnimSlot.UseStairs);
		CollectionAssert.Contains(slots, AnimSlot.Channel);   // 플레이어 명령으로 코어·문을 부수는 몬스터도 있다
		CollectionAssert.Contains(slots, AnimSlot.Death);
	}

	[Test]
	public void SlotsFor_DuplicateAndEmptySkillNames_AreDropped()
	{
		var skills = AnimSlotCatalog.SlotsFor(true, new[] { "강타", "강타", "", null })
			.Where(x => x.slot == AnimSlot.Skill).ToList();
		Assert.AreEqual(1, skills.Count);
	}

	[Test]
	public void KeyOf_OnlySkillSlotsIncludeTheName()
	{
		Assert.AreNotEqual(AnimSlotCatalog.KeyOf(AnimSlot.Skill, "A"), AnimSlotCatalog.KeyOf(AnimSlot.Skill, "B"));
		Assert.AreEqual(AnimSlotCatalog.KeyOf(AnimSlot.Hit, "A"), AnimSlotCatalog.KeyOf(AnimSlot.Hit, "B"));
	}

	[Test]
	public void EverySlot_HasCatalogInfo()
	{
		foreach (AnimSlot s in System.Enum.GetValues(typeof(AnimSlot)))
			Assert.DoesNotThrow(() => AnimSlotCatalog.Get(s), s.ToString());
	}

	// ── 루프 슬롯 선택 ──────────────────────────────────────────

	[Test]
	public void ResolveLoopSlot_NothingTrue_IsIdle()
	{
		Assert.AreEqual(AnimSlot.Idle, UnitAnimMath.ResolveLoopSlot(new AnimPollInputs(), Has(AnimSlot.Idle)));
	}

	[Test]
	public void ResolveLoopSlot_Moving_IsWalk_OnlyWhenClipExists()
	{
		var i = new AnimPollInputs { Moving = true };
		Assert.AreEqual(AnimSlot.Walk, UnitAnimMath.ResolveLoopSlot(i, Has(AnimSlot.Idle, AnimSlot.Walk)));
		Assert.AreEqual(AnimSlot.Idle, UnitAnimMath.ResolveLoopSlot(i, Has(AnimSlot.Idle)));
	}

	[Test]
	public void ResolveLoopSlot_Priority_StunBeatsChannelBeatsInteractionBeatsWalk()
	{
		var all = Has(AnimSlot.Stunned, AnimSlot.Channel, AnimSlot.DisarmTrap, AnimSlot.Investigate, AnimSlot.Walk, AnimSlot.Idle);
		var i = new AnimPollInputs { Stunned = true, Channeling = true, Disarming = true, Investigating = true, Moving = true };

		Assert.AreEqual(AnimSlot.Stunned, UnitAnimMath.ResolveLoopSlot(i, all));
		i.Stunned = false;
		Assert.AreEqual(AnimSlot.Channel, UnitAnimMath.ResolveLoopSlot(i, all));
		i.Channeling = false;
		Assert.AreEqual(AnimSlot.DisarmTrap, UnitAnimMath.ResolveLoopSlot(i, all));
		i.Disarming = false;
		Assert.AreEqual(AnimSlot.Investigate, UnitAnimMath.ResolveLoopSlot(i, all));
		i.Investigating = false;
		Assert.AreEqual(AnimSlot.Walk, UnitAnimMath.ResolveLoopSlot(i, all));
	}

	[Test]
	public void ResolveLoopSlot_EmptyChannelSlot_FallsThroughToNextCondition()
	{
		// Channel 클립이 없으면 부수는 중에도 이동·대기 연출로 떨어진다(미구현 슬롯이 동작을 바꾸지 않음).
		var i = new AnimPollInputs { Channeling = true, Moving = true };
		Assert.AreEqual(AnimSlot.Walk, UnitAnimMath.ResolveLoopSlot(i, Has(AnimSlot.Walk, AnimSlot.Idle)));
		i.Moving = false;
		Assert.AreEqual(AnimSlot.Idle, UnitAnimMath.ResolveLoopSlot(i, Has(AnimSlot.Idle)));
	}

	// ── 트리거 우선순위 ─────────────────────────────────────────

	[Test]
	public void AcceptsTrigger_NoActiveOneShot_AcceptsAnything()
	{
		Assert.IsTrue(UnitAnimMath.AcceptsTrigger(AnimSlot.Hit, false, AnimSlot.Idle, false, false));
		Assert.IsTrue(UnitAnimMath.AcceptsTrigger(AnimSlot.Spawn, false, AnimSlot.Idle, false, false));
	}

	[Test]
	public void AcceptsTrigger_HitDoesNotInterruptSkillDodgeOrBlink()
	{
		Assert.IsFalse(UnitAnimMath.AcceptsTrigger(AnimSlot.Hit, true, AnimSlot.Skill, false, false));
		Assert.IsFalse(UnitAnimMath.AcceptsTrigger(AnimSlot.Hit, true, AnimSlot.Dodge, false, false));
		Assert.IsFalse(UnitAnimMath.AcceptsTrigger(AnimSlot.Hit, true, AnimSlot.Blink, false, false));
	}

	[Test]
	public void AcceptsTrigger_SkillInterruptsHit_AndSamePriorityRestarts()
	{
		Assert.IsTrue(UnitAnimMath.AcceptsTrigger(AnimSlot.Skill, true, AnimSlot.Hit, false, false));
		Assert.IsTrue(UnitAnimMath.AcceptsTrigger(AnimSlot.Skill, true, AnimSlot.Skill, false, false));
		Assert.IsTrue(UnitAnimMath.AcceptsTrigger(AnimSlot.Dodge, true, AnimSlot.Skill, false, false));
	}

	[Test]
	public void AcceptsTrigger_StunnedVisible_RejectsEverythingExceptDeath()
	{
		Assert.IsFalse(UnitAnimMath.AcceptsTrigger(AnimSlot.Skill, false, AnimSlot.Idle, true, false));
		Assert.IsFalse(UnitAnimMath.AcceptsTrigger(AnimSlot.Dodge, false, AnimSlot.Idle, true, false));
		Assert.IsTrue(UnitAnimMath.AcceptsTrigger(AnimSlot.Death, false, AnimSlot.Idle, true, false));
	}

	[Test]
	public void AcceptsTrigger_AfterDeath_RejectsEverything()
	{
		Assert.IsFalse(UnitAnimMath.AcceptsTrigger(AnimSlot.Hit, true, AnimSlot.Death, false, true));
		Assert.IsFalse(UnitAnimMath.AcceptsTrigger(AnimSlot.Death, true, AnimSlot.Death, false, true));
	}

	// ── 속도 맞춤 · 시간 감기 ───────────────────────────────────

	[Test]
	public void FitSpeed_StretchesClipToCastWindow()
	{
		Assert.AreEqual(1f,   UnitAnimMath.FitSpeed(1f, 1f),   Eps);
		Assert.AreEqual(2f,   UnitAnimMath.FitSpeed(1f, 0.5f), Eps);  // 1초 클립을 0.5초 시전에 맞춤 → 2배속
		Assert.AreEqual(0.5f, UnitAnimMath.FitSpeed(1f, 2f),   Eps);  // 2초 시전이면 0.5배속
	}

	[Test]
	public void FitSpeed_InstantCast_IsNormalSpeed()
	{
		Assert.AreEqual(1f, UnitAnimMath.FitSpeed(0.8f, 0f), Eps);
		Assert.AreEqual(1f, UnitAnimMath.FitSpeed(0f, 1f),   Eps);
	}

	[Test]
	public void FitSpeed_IsClamped()
	{
		Assert.AreEqual(UnitAnimMath.MaxSkillSpeed, UnitAnimMath.FitSpeed(10f, 0.1f), Eps);
		Assert.AreEqual(UnitAnimMath.MinSkillSpeed, UnitAnimMath.FitSpeed(0.1f, 10f), Eps);
	}

	[Test]
	public void WrapTime_LoopsAndGuardsZeroLength()
	{
		Assert.AreEqual(0.25f, UnitAnimMath.WrapTime(1.25f, 1f), Eps);
		Assert.AreEqual(0f,    UnitAnimMath.WrapTime(3f, 0f),    Eps);
	}

	// ── 크로스페이드 가중치 ─────────────────────────────────────

	[Test]
	public void StepWeights_MovesTowardTarget_AndStaysNormalized()
	{
		var w = new[] { 1f, 0f, 0f };
		UnitAnimMath.StepWeights(w, 1, 0.05f, 0.1f);   // 절반 진행
		Assert.AreEqual(1f, w.Sum(), Eps);
		Assert.Greater(w[1], 0f);
		Assert.Less(w[0], 1f);

		UnitAnimMath.StepWeights(w, 1, 1f, 0.1f);       // 충분히 지나면 완전히 넘어감
		Assert.AreEqual(1f, w[1], Eps);
		Assert.AreEqual(0f, w[0], Eps);
	}

	[Test]
	public void StepWeights_ZeroFade_CutsImmediately()
	{
		var w = new[] { 1f, 0f };
		UnitAnimMath.StepWeights(w, 1, 0.016f, 0f);
		Assert.AreEqual(1f, w[1], Eps);
		Assert.AreEqual(0f, w[0], Eps);
	}

	[Test]
	public void StepWeights_NoTarget_DecaysToZeroWithoutNaN()
	{
		var w = new[] { 1f, 0f };
		UnitAnimMath.StepWeights(w, -1, 1f, 0.1f);
		Assert.AreEqual(0f, w[0], Eps);
		Assert.AreEqual(0f, w[1], Eps);
	}

	// ── 이동 디바운스 ───────────────────────────────────────────

	[Test]
	public void MoveTracker_StartsMovingImmediately_StopsAfterDebounce()
	{
		var t = new AnimMoveTracker(4);
		Assert.IsTrue(t.Update(true));
		Assert.IsTrue(t.Update(false));
		Assert.IsTrue(t.Update(false));
		Assert.IsTrue(t.Update(false));
		Assert.IsFalse(t.Update(false));   // 4번째 정지 프레임
	}

	[Test]
	public void MoveTracker_MovementResetsTheStillCounter()
	{
		var t = new AnimMoveTracker(3);
		t.Update(true);
		t.Update(false);
		t.Update(false);
		t.Update(true);                    // 다시 움직임 → 카운터 리셋
		Assert.IsTrue(t.Update(false));
		Assert.IsTrue(t.Update(false));
		Assert.IsFalse(t.Update(false));
	}
}
#endif
