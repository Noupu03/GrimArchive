#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 03_탐색반응·경계·조사·함정대응_시스템_v0.6 재검증(시야인지반응_03_구현현황_2026-07-24.txt)에서
// 코드 리딩만으로 발견한 것들을, 실제 FSM 클래스(UnitFSM/TacticalFSMState/PlayerCommandFSMState)를
// 진짜로 생성하고 틱을 돌려서 재현되는지 검증한다 — ExplorationSystemTests.cs는 ExplorationMath
// 순수 함수만 고정하고 실제 Human/UnitFSM 인스턴스는 만들지 않으므로, 이 파일이 그 공백을 메운다.
// ScriptableObject.CreateInstance<Human>()이 Unit.OnEnable()을 실제로 호출해 Components를 채우므로
// (실제 스폰 경로 UnitGenerate.GenerateUnitAtPos와 동일한 초기화), Session 없이도 대부분의 FSM 로직을
// 그대로 실행할 수 있다(Session이 필요한 코드는 null 체크로 안전하게 우회됨 — FindDirectlyVisibleInteractingAlly 등).
// ========================================================================

public class FSMBehaviorTests
{
	// ── 6-4/6-5장. 보호 포메이션 배치 위치 (2026-07-24 버그 수정 검증) ──────────────

	[Test]
	public void GetEscortSlotPosition_MeleeSlot_IsInFrontOfInteractingUnit()
	{
		var target = ScriptableObject.CreateInstance<Human>();
		target.position = new Vector2Int(10, 10);
		target.currentDir = Dir.UP; // UP = (0,1)

		var escort = ScriptableObject.CreateInstance<Human>();

		Vector2Int slot = escort.GetEscortSlotPosition(target, backDistance: 1f);

		// 6-4장: 근접 유닛은 상호작용 유닛 "전방"(바라보는 방향 쪽)에 선다.
		Assert.AreEqual(new Vector2Int(10, 11), slot);
	}

	[Test]
	public void GetEscortSlotPosition_RangedSlot_IsBehindInteractingUnit()
	{
		var target = ScriptableObject.CreateInstance<Human>();
		target.position = new Vector2Int(10, 10);
		target.currentDir = Dir.UP;

		var escort = ScriptableObject.CreateInstance<Human>();

		Vector2Int slot = escort.GetEscortSlotPosition(target, backDistance: 2f);

		// 6-5장: 원거리 유닛은 후방 2칸 이상.
		Assert.AreEqual(new Vector2Int(10, 8), slot);
	}

	// ── 5-6/9-7장. 조사/함정 해제 중단 시 진행도 50% 손실 ───────────────────────────
	// 실제 UnitFSM(PlayerCommandFSMState→CombatFSMState→TacticalFSMState→NavigationFSMState)을 그대로
	// 돌려서, "같은 Tactical 상태 안에서 브랜치만 바뀌는 중단"과 "다른 최상위 상태로 완전히 전환되는
	// 중단"이 실제로 다르게 동작하는지 확인한다.

	[Test]
	public void Investigate_InterruptedByHitWithinTactical_ProgressIsNotHalved_KnownGap()
	{
		var human = ScriptableObject.CreateInstance<Human>();
		human.position = new Vector2Int(0, 0);
		human.currentInvestigation = new InvestigationState
		{
			TargetObjectId = "dummy",
			TargetPosition = new Vector3Int(0, 0, 0),
			Progress01 = 0.6f,
			PenaltyActive = true,
		};

		var fsm = human.fsm;

		// 1틱째: 아직 안 맞은 상태로 우선 Tactical(조사)에 진입시킨다.
		human.JudgeState();
		human.ExecuteAction();
		Assert.AreEqual("TacticalFSMState", fsm.CurrentState.GetType().Name);

		// 2틱째: 문서 5-6장 중단 조건("상호작용 유닛이 공격받음")을 재현 — 피격당함.
		human.isHitThisTurn = true;
		human.JudgeState();
		human.ExecuteAction();

		// 문서대로라면 진행도가 50% 손실(0.3)돼야 하지만, 실제로는 CanInvestigate가 이 틱만 실패를
		// 반환해 같은 Tactical 상태 안에서 다른 브랜치(패닉/함정/경계/대기/포메이션 전부 조건 불충족)로
		// 넘어갈 뿐 상태 자체는 안 바뀌므로 OnExit(50% 손실 로직이 있는 곳)가 호출되지 않는다 — 진행도
		// 보존됨을 실제로 확인한다(시야인지반응_03_구현현황_2026-07-24.txt 5-6장 참고, 알려진 gap).
		Assert.AreEqual("TacticalFSMState", fsm.CurrentState.GetType().Name);
		Assert.AreEqual(0.6f, human.currentInvestigation.Progress01, 0.001f);
	}

	[Test]
	public void Investigate_InterruptedByPlayerCommand_ProgressIsHalved_OnExitPathWorks()
	{
		var human = ScriptableObject.CreateInstance<Human>();
		human.position = new Vector2Int(0, 0);
		human.currentInvestigation = new InvestigationState
		{
			TargetObjectId = "dummy",
			TargetPosition = new Vector3Int(0, 0, 0),
			Progress01 = 0.6f,
			PenaltyActive = true,
		};

		var fsm = human.fsm;

		human.JudgeState();
		human.ExecuteAction();
		Assert.AreEqual("TacticalFSMState", fsm.CurrentState.GetType().Name);

		// 플레이어가 다른 곳으로 이동 명령을 내려 완전히 다른 최상위 상태(PlayerCommandFSMState)로
		// 전환시킨다 — 이 경로는 실제로 UnitFSM.SelectState가 _current를 바꾸므로 TacticalFSMState.
		// OnExit가 호출된다.
		human.playerMoveTarget = new Vector2Int(5, 5);
		human.isManualMoveCommand = true;
		human.JudgeState();

		Assert.AreEqual("PlayerCommandFSMState", fsm.CurrentState.GetType().Name);
		// OnExit에서 조사 중단 처리 — 0.6 * 0.5 = 0.3으로 손실.
		Assert.AreEqual(0.3f, human.currentInvestigation.Progress01, 0.001f);
	}

	// ── 07-A 7-3장 재확인(2026-08-05): "확인 행동 시작 기한은 5초지만, 시작 후엔 시간 제한 없이 계속
	// 수행" — 소리 반응 접근 중(아직 인지 판정 안 굴림)엔 03문서 4-11장 "미식별 공격 수색 15초"
	// 워치독이 적용되면 안 된다. 사용자가 "소리 발생 지점이 멀면 도착 전에 취소된다"고 신고해서 발견한
	// 버그의 수정 검증 — UnitFunction.OnUpdate의 AlertSearchState 정리 분기.
	[Test]
	public void SoundResponseApproach_NotCutOffByUnidentifiedAttackWatchdog()
	{
		var human = ScriptableObject.CreateInstance<Human>();
		human.currentAlertSearch = new AlertSearchState { IsSoundResponse = true, SoundPerceptionRolled = false };

		// 4-11장 워치독(15초)을 넘는 16초를 한 번에 흘려보낸다 — 아직 인지 판정 전이므로 취소되면 안 됨.
		human.OnUpdate(16f);

		Assert.IsNotNull(human.currentAlertSearch, "소리 반응 접근 중엔 시간 제한 없이 계속 수행돼야 한다(07-A 7-3장).");
	}

	// 회귀 방지 — 위 수정이 원래 4-11장 "미식별 공격 수색"(IsSoundResponse=false) 자체의 15초 워치독까지
	// 없애버린 건 아닌지 확인.
	[Test]
	public void UnidentifiedAttackSearch_StillCutOffAfter15Seconds()
	{
		var human = ScriptableObject.CreateInstance<Human>();
		human.currentAlertSearch = new AlertSearchState(); // IsSoundResponse=false — 미식별 공격 수색

		human.OnUpdate(16f);

		Assert.IsNull(human.currentAlertSearch, "미식별 공격 수색은 4-11장 그대로 15초 후 종료돼야 한다.");
	}
}
#endif
