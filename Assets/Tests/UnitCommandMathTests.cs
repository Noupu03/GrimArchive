#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 자동 방어 복귀가 전투·경계에 밀리는지(배치모드 1-6 표1)를 정하는 플래그 판정 — 순수 함수(UnitCommandMath)와 그것을 읽는 Unit 프로퍼티·FSM 상태의 우선순위.
// 세션·맵 없이 EditMode에서 돈다.
// ========================================================================

public class UnitCommandMathTests
{
	private static Monster NewPlayerMonster()
	{
		var m = ScriptableObject.CreateInstance<Monster>();
		m.FactionBehavior = new PlayerMonsterBehavior();
		return m;
	}

	// ── 순수 판정 ───────────────────────────────────────────────

	[Test]
	public void HasForcedMove_IsTrueOnlyForManualMovesThatAreNotAutoReturns()
	{
		foreach (bool target in new[] { false, true })
			foreach (bool manual in new[] { false, true })
				foreach (bool auto in new[] { false, true })
					Assert.AreEqual(target && manual && !auto, UnitCommandMath.HasForcedMove(target, manual, auto), $"target={target} manual={manual} auto={auto}");
	}

	[Test]
	public void MayLeaveRoom_AutoReturnOnlyWhileTheReturnStateWalks()
	{
		Assert.IsFalse(UnitCommandMath.MayLeaveRoom(false, false, false), "명령 없는 자율 이동은 방을 나가지 못한다");
		Assert.IsTrue(UnitCommandMath.MayLeaveRoom(true, false, false), "직접 이동 명령은 늘 방 이탈 제한을 우회한다");
		Assert.IsTrue(UnitCommandMath.MayLeaveRoom(true, true, true), "자동 복귀는 복귀 상태가 걷는 동안 우회한다");
		Assert.IsFalse(UnitCommandMath.MayLeaveRoom(true, true, false), "자동 복귀 중이라도 전투·경계 상태에서는 방 제한이 그대로다(수동 플래그가 남아 있어도)");
	}

	// ── Unit 프로퍼티 ────────────────────────────────────────────

	[Test]
	public void Unit_DirectMove_IsForced_AndMayLeaveRoom()
	{
		var m = NewPlayerMonster();
		m.playerMoveTarget = new Vector2Int(5, 5);
		m.isManualMoveCommand = true;
		Assert.IsTrue(m.HasForcedPlayerMove);
		Assert.IsTrue(m.MayLeaveRoomNow);
	}

	[Test]
	public void Unit_AutoReturn_IsNotForced_AndKeepsRoomConfinementUntilTheReturnStateWalks()
	{
		var m = NewPlayerMonster();
		m.playerMoveTarget = new Vector2Int(5, 5);
		m.isManualMoveCommand = true;
		m.isDefenseAutoReturn = true;
		Assert.IsFalse(m.HasForcedPlayerMove, "전투·경계가 끼어들 수 있어야 한다");
		Assert.IsFalse(m.MayLeaveRoomNow, "FSM이 아직 복귀 상태가 아니면 방 이탈 제한을 우회하지 않는다");
		Assert.IsTrue(m.HasActivePlayerCommand(), "그래도 이동 명령은 걸려 있다(유입 인구 예약·재발행 방지가 이 값에 기대고 있다)");
	}

	// ── FSM 상태 우선순위 ────────────────────────────────────────

	[Test]
	public void DefenseReturnState_ClaimsOnlyFlaggedPlayerMonsters()
	{
		var state = new DefenseReturnFSMState();
		var auto = NewPlayerMonster();
		auto.playerMoveTarget = new Vector2Int(3, 3);
		auto.isManualMoveCommand = true;
		auto.isDefenseAutoReturn = true;
		Assert.Greater(state.GetPriority(auto), 0f);

		var direct = NewPlayerMonster();
		direct.playerMoveTarget = new Vector2Int(3, 3);
		direct.isManualMoveCommand = true;
		Assert.AreEqual(0f, state.GetPriority(direct), "직접 이동 명령은 PlayerCommand가 맡는다");

		var idle = NewPlayerMonster();
		Assert.AreEqual(0f, state.GetPriority(idle));

		var wild = ScriptableObject.CreateInstance<Monster>();
		wild.FactionBehavior = new WildMonsterBehavior();
		wild.playerMoveTarget = new Vector2Int(3, 3);
		wild.isManualMoveCommand = true;
		wild.isDefenseAutoReturn = true;
		Assert.AreEqual(0f, state.GetPriority(wild), "플레이어 몬스터 전용이다");
	}

	[Test]
	public void PlayerCommandState_LetsAutoReturnsThrough_ButStillClaimsDirectMoves()
	{
		var state = new PlayerCommandFSMState();
		var auto = NewPlayerMonster();
		auto.playerMoveTarget = new Vector2Int(3, 3);
		auto.isManualMoveCommand = true;
		auto.isDefenseAutoReturn = true;
		Assert.AreEqual(0f, state.GetPriority(auto), "자동 복귀는 강제 고착이 아니라 전투·경계에 밀린다");

		var direct = NewPlayerMonster();
		direct.playerMoveTarget = new Vector2Int(3, 3);
		direct.isManualMoveCommand = true;
		Assert.Greater(state.GetPriority(direct), 0f, "직접 이동 적용은 지정 위치 이동 우선(명세 1-6 표1)");
	}
}
#endif
