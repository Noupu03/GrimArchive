#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

// ========================================================================
// 스프라이트 방향 환산(Script/Unit/Visual/SpriteFacingMath.cs) — 회의록 2026-11-09 고정.
// Dir 8방향 → 반측면 정면(DownLeft)/후면(UpLeft) × 좌우 반전 4방향.
// 순수 가로(LEFT/RIGHT)는 정면, 순수 세로(UP/DOWN)는 직전 좌우 반전을 유지한다(사용자 확정).
// 실제 SpriteResolver/무기 포즈 적용은 Unity 플레이로만 확인된다 — 여기선 "어떤 방향이 어떤 그림이 되는가"만 고정한다.
// ========================================================================

public class SpriteFacingTests
{
	// ── 대각선: 정면/후면 + 좌우가 모두 정해진다 ─────────────────────

	[TestCase(Dir.DOWN_LEFT,  false, false)]
	[TestCase(Dir.DOWN_RIGHT, false, true)]
	[TestCase(Dir.UP_LEFT,    true,  false)]
	[TestCase(Dir.UP_RIGHT,   true,  true)]
	public void Diagonals_MapToBackAndFlip(Dir dir, bool expectBack, bool expectFlip)
	{
		// 직전 상태와 무관해야 한다 — 두 값 모두로 확인.
		foreach (bool prev in new[] { false, true })
		{
			SpriteFacing f = SpriteFacingMath.Resolve(dir, prev);
			Assert.AreEqual(expectBack, f.Back, $"{dir} prev={prev} Back");
			Assert.AreEqual(expectFlip, f.FlipX, $"{dir} prev={prev} FlipX");
		}
	}

	// ── 순수 가로: 정면, 좌우는 방향대로 ─────────────────────────────

	[Test]
	public void PureLeft_IsFrontNoFlip()
	{
		SpriteFacing f = SpriteFacingMath.Resolve(Dir.LEFT, true);
		Assert.IsFalse(f.Back);
		Assert.IsFalse(f.FlipX);
	}

	[Test]
	public void PureRight_IsFrontFlipped()
	{
		SpriteFacing f = SpriteFacingMath.Resolve(Dir.RIGHT, false);
		Assert.IsFalse(f.Back);
		Assert.IsTrue(f.FlipX);
	}

	// ── 순수 세로: 정면/후면은 정해지고 좌우는 직전 상태 유지 ─────────

	[Test]
	public void PureUp_IsBack_KeepsPreviousFlip()
	{
		Assert.IsTrue(SpriteFacingMath.Resolve(Dir.UP, false).Back);
		Assert.IsFalse(SpriteFacingMath.Resolve(Dir.UP, false).FlipX);
		Assert.IsTrue(SpriteFacingMath.Resolve(Dir.UP, true).FlipX);
	}

	[Test]
	public void PureDown_IsFront_KeepsPreviousFlip()
	{
		Assert.IsFalse(SpriteFacingMath.Resolve(Dir.DOWN, false).Back);
		Assert.IsFalse(SpriteFacingMath.Resolve(Dir.DOWN, false).FlipX);
		Assert.IsTrue(SpriteFacingMath.Resolve(Dir.DOWN, true).FlipX);
	}

	[Test]
	public void WalkingUpAfterRight_StaysFlipped_ThenLeftClearsIt()
	{
		// 오른쪽으로 걷다가 위로 꺾어도 좌우가 갑자기 바뀌지 않고, 왼쪽을 보는 순간 풀린다.
		bool flip = false;
		flip = SpriteFacingMath.Resolve(Dir.RIGHT, flip).FlipX;
		Assert.IsTrue(flip);
		flip = SpriteFacingMath.Resolve(Dir.UP, flip).FlipX;
		Assert.IsTrue(flip);
		flip = SpriteFacingMath.Resolve(Dir.UP_RIGHT, flip).FlipX;
		Assert.IsTrue(flip);
		flip = SpriteFacingMath.Resolve(Dir.DOWN, flip).FlipX;
		Assert.IsTrue(flip);
		flip = SpriteFacingMath.Resolve(Dir.DOWN_LEFT, flip).FlipX;
		Assert.IsFalse(flip);
		flip = SpriteFacingMath.Resolve(Dir.UP, flip).FlipX;
		Assert.IsFalse(flip);
	}

	// ── 8방향 전부 두 라벨 중 하나로만 접힌다 ────────────────────────

	[Test]
	public void AllEightDirections_FoldIntoTwoLabelsAndTwoPoseKeys()
	{
		foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
		{
			SpriteFacing f = SpriteFacingMath.Resolve(d, false);
			Assert.That(f.Label, Is.EqualTo("DownLeft").Or.EqualTo("UpLeft"), d.ToString());
			Assert.That(f.PoseDir, Is.EqualTo(Dir.DOWN_LEFT).Or.EqualTo(Dir.UP_LEFT), d.ToString());
		}
	}

	[Test]
	public void LabelAndPoseKey_FollowBack()
	{
		SpriteFacing front = new SpriteFacing(false, false);
		SpriteFacing back  = new SpriteFacing(true, false);
		Assert.AreEqual("DownLeft", front.Label);
		Assert.AreEqual(Dir.DOWN_LEFT, front.PoseDir);
		Assert.AreEqual("UpLeft", back.Label);
		Assert.AreEqual(Dir.UP_LEFT, back.PoseDir);
	}

	[Test]
	public void Equality_ComparesBackAndFlip()
	{
		Assert.IsTrue(new SpriteFacing(true, false).Equals(new SpriteFacing(true, false)));
		Assert.IsFalse(new SpriteFacing(true, false).Equals(new SpriteFacing(false, false)));
		Assert.IsFalse(new SpriteFacing(true, false).Equals(new SpriteFacing(true, true)));
	}

	// 범위 밖 값(예: 아직 정해지지 않은 센티넬)이 들어와도 예외 없이 정면으로 처리한다.
	[Test]
	public void OutOfRangeDir_FallsBackToFront_KeepsFlip()
	{
		SpriteFacing f = SpriteFacingMath.Resolve((Dir)(-1), true);
		Assert.IsFalse(f.Back);
		Assert.IsTrue(f.FlipX);
	}
}
#endif
