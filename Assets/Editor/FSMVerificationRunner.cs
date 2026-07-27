using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using GrimArchive.Wave;

// 2026-07-25: 03문서 재검증 우선순위 항목(6-4장 근접 배치, 5-6장 조사 중단 손실) 코드 리딩 결과를
// 실제 Human/UnitFSM을 생성해 돌려서 검증하기 위한 배치 모드 전용 러너.
// Assets/Tests의 FSMBehaviorTests.cs(NUnit)와 같은 내용을 검증하지만, Tests 폴더가 별도 어셈블리
// 정의 없이 Assembly-CSharp에 #if UNITY_INCLUDE_TESTS로 묶여 있어(그 심볼이 정의 안 됨) Test Runner가
// 아예 그 코드를 컴파일하지 못하는 상태였다 — asmdef를 추가해봤지만 "Assembly-CSharp"를 이름으로
// 참조하는 게 이 프로젝트/버전에서 실제로는 컴파일러 참조로 이어지지 않아(CreateMap/MapRandering/Floor
// 등 타입을 못 찾음, 기존 CreateMapPlayTests.cs까지 컴파일 에러) 되돌렸다. 이 파일은 Assets/Editor
// 아래(Assembly-CSharp-Editor, Assembly-CSharp를 자동으로 참조함)에 둬서 그 문제를 완전히 우회한다.
// `Unity.exe -batchmode -nographics -projectPath <proj> -executeMethod FSMVerificationRunner.Run -logFile <log>`
// 로 실행하면 콘솔에 PASS/FAIL을 출력하고 종료 코드로도 결과를 알린다.
public static class FSMVerificationRunner
{
	private static int _failCount;

	public static void Run()
	{
		_failCount = 0;

		GetEscortSlotPosition_MeleeSlot_IsInFrontOfInteractingUnit();
		GetEscortSlotPosition_RangedSlot_IsBehindInteractingUnit();
		GetEscortSlotPosition_TargetStillMoving_IsBehindNotFront();
		GetEscortSlotPosition_OverlapWithInteractionObject_StepsFartherOrSideways();
		MoveToCore_DifferentFloor_NeverFalsePositiveArrives();
		Investigate_InterruptedByHit_ProgressIsHalvedOnce();
		Investigate_InterruptedByPlayerCommand_ProgressIsHalved();
		TrapDisarm_InterruptedByHit_ProgressIsHalvedOnce();
		Investigate_EscortHit_NonPartyObjective_Interrupts();
		Investigate_EscortHit_PartyObjective_Continues();

		if (_failCount == 0)
		{
			Debug.Log("[FSMVerification] ALL PASSED");
			EditorApplication.Exit(0);
		}
		else
		{
			Debug.LogError($"[FSMVerification] {_failCount} FAILED");
			EditorApplication.Exit(1);
		}
	}

	// Session은 [Inject] 전용 private 필드라 정식 DI(VContainer) 없이는 못 채운다 — 검증 목적으로만
	// 리플렉션으로 최소 GameSession+ObjectSpawner를 만들어 꽂아준다. MoveToInvestigateTarget/
	// InvestigatePerform이 human.Session.objectGrid를 그대로 참조하므로, 대상 오브젝트가 그 안에
	// 있어야 실제 BT 분기(조사 진행)까지 도달한다.
	private static void AttachMinimalSession(Human human, InteractableObject investigateTarget)
	{
		var objectSpawner = new ObjectSpawner();
		objectSpawner.objectGrid.Add(investigateTarget.Position, investigateTarget);

		var session = new GameSession();
		typeof(GameSession).GetField("_objectSpawner", BindingFlags.NonPublic | BindingFlags.Instance)
			.SetValue(session, objectSpawner);

		typeof(Unit).GetField("_gameSession", BindingFlags.NonPublic | BindingFlags.Instance)
			.SetValue(human, session);
	}

	private static void Check(string name, bool condition, string detail)
	{
		if (condition)
		{
			Debug.Log($"[FSMVerification] PASS: {name}");
		}
		else
		{
			_failCount++;
			Debug.LogError($"[FSMVerification] FAIL: {name} — {detail}");
		}
	}

	// 2026-07-27 수정(IsEscortTargetActivelyInteracting) 이후로 6-4/6-5 문서 배치는 target이 "실제로
	// 상호작용 중"(PenaltyActive/Active)일 때만 적용된다 — 그냥 Human을 만들기만 하면 세 상호작용
	// 필드가 전부 null이라 "이동 중" 취급돼 후방으로 계산된다. 이 테스트들은 그 활성 상태를 명시적으로
	// 만들어야 원래 의도(전방/후방 배치)를 검증한다.
	private static Human MakeActivelyInteractingTarget(Vector2Int pos, Dir dir)
	{
		var target = ScriptableObject.CreateInstance<Human>();
		target.position = pos;
		target.currentDir = dir;
		target.currentInvestigation = new InvestigationState
		{
			TargetObjectId = "dummy",
			TargetPosition = new Vector3Int(pos.x, pos.y, 0),
			Progress01 = 0.1f,
			PenaltyActive = true,
		};
		return target;
	}

	private static void GetEscortSlotPosition_MeleeSlot_IsInFrontOfInteractingUnit()
	{
		var target = MakeActivelyInteractingTarget(new Vector2Int(10, 10), Dir.UP); // UP = (0,1)

		var escort = ScriptableObject.CreateInstance<Human>();
		Vector2Int slot = escort.GetEscortSlotPosition(target, backDistance: 1f);
		Vector2Int expected = new Vector2Int(10, 11);

		Check("6-4 근접 배치는 전방(실제 상호작용 중)", slot == expected, $"expected {expected}, got {slot}");
	}

	private static void GetEscortSlotPosition_RangedSlot_IsBehindInteractingUnit()
	{
		var target = MakeActivelyInteractingTarget(new Vector2Int(10, 10), Dir.UP);

		var escort = ScriptableObject.CreateInstance<Human>();
		Vector2Int slot = escort.GetEscortSlotPosition(target, backDistance: 2f);
		Vector2Int expected = new Vector2Int(10, 8);

		Check("6-5 원거리 배치는 후방 2칸(실제 상호작용 중)", slot == expected, $"expected {expected}, got {slot}");
	}

	// 2026-07-27 신규 — 상호작용 유닛이 아직 목적지로 "이동 중"(활성 상호작용 없음)이면 근접이든
	// 원거리든 전방이 아니라 후방(뒤따름)으로 배치해야 한다(코어 진행 경로를 막던 버그의 근본 수정).
	private static void GetEscortSlotPosition_TargetStillMoving_IsBehindNotFront()
	{
		var target = ScriptableObject.CreateInstance<Human>();
		target.position = new Vector2Int(10, 10);
		target.currentDir = Dir.UP; // 활성 상호작용 없음 = "이동 중"

		var escort = ScriptableObject.CreateInstance<Human>();
		Vector2Int slot = escort.GetEscortSlotPosition(target, backDistance: 1f);
		Vector2Int expected = new Vector2Int(10, 9); // 후방 1칸(뒤따름)

		Check(
			"6-4 이동 중인 상호작용 유닛은 전방이 아니라 후방으로 배치(2026-07-27 교착 수정)",
			slot == expected, $"expected {expected}, got {slot}");
	}

	// 2026-07-27 신규 — 전방 슬롯이 상호작용 오브젝트 자신의 위치와 겹치면(오브젝트가 Passable이라
	// target이 그 위에 서 있을 때) 한 칸 더 물러나거나 옆으로 피해야 한다.
	private static void GetEscortSlotPosition_OverlapWithInteractionObject_StepsFartherOrSideways()
	{
		var target = MakeActivelyInteractingTarget(new Vector2Int(10, 10), Dir.UP);
		// target이 정확히 오브젝트 위(전방 슬롯과 같은 자리)에 서 있는 상황을 재현.
		target.currentInvestigation.TargetPosition = new Vector3Int(10, 11, 0);

		var escort = ScriptableObject.CreateInstance<Human>();
		Vector2Int slot = escort.GetEscortSlotPosition(target, backDistance: 1f);
		Vector2Int naiveFrontSlot = new Vector2Int(10, 11);

		Check(
			"6-4 전방 슬롯이 상호작용 오브젝트와 겹치면 다른 자리로 회피",
			slot != naiveFrontSlot, $"slot이 오브젝트 위치({naiveFrontSlot})와 그대로 겹침, got {slot}");
	}

	// 2026-07-27 신규 — 리더가 코어와 다른 층에 있으면 MoveToCore의 Chebyshev 도착 판정이 층을 무시하고
	// X/Y만 봐서 "도착"으로 오판하던 버그의 방어 코드(둘째 안전망)를 검증한다. CanContinueCore가 이미
	// 층이 다르면 코어 상호작용 자체를 안 만드므로, 여기서는 MoveToCore 그 자체에 대한 방어만 별도로
	// 리플렉션 호출로 확인한다(CanContinueCore를 우회해 currentCoreInteraction을 직접 주입).
	private static void MoveToCore_DifferentFloor_NeverFalsePositiveArrives()
	{
		var human = ScriptableObject.CreateInstance<Human>();
		human.position = new Vector2Int(5, 5); // 코어와 X/Y가 우연히 같은 위치
		human.currentFloor = 0;
		human.currentCoreInteraction = new CoreInteractionState
		{
			CoreObjectId = "core",
			CorePosition = new Vector3Int(5, 5, 1), // 층(Z)이 다름
		};
		human.party = new Party("p1", "TestParty");
		human.party.PendingCoreObjectId = "core";
		human.party.PendingCorePosition = new Vector3Int(5, 5, 1);
		AttachMinimalSession(human, new InteractableObject("core", new Vector3Int(5, 5, 1), 10f));

		var method = typeof(TacticalFSMState).GetMethod("MoveToCore", BindingFlags.NonPublic | BindingFlags.Static);
		object status = method.Invoke(null, new object[] { human });
		bool stillHasInteraction = human.currentCoreInteraction != null;

		Check(
			"7-3 MoveToCore는 층이 다르면 도착 오판 없이 Running만 반환",
			status.ToString() == "Running" && stillHasInteraction,
			$"status={status} (expected Running), stillHasInteraction={stillHasInteraction} (expected true)");
	}

	private static void Investigate_InterruptedByHit_ProgressIsHalvedOnce()
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
		AttachMinimalSession(human, new InteractableObject("dummy", new Vector3Int(0, 0, 0), 10f));

		var fsm = human.fsm;

		human.JudgeState();
		human.ExecuteAction();
		bool enteredTactical = fsm.CurrentState.GetType().Name == "TacticalFSMState";

		human.isHitThisTurn = true;
		human.JudgeState();
		human.ExecuteAction();
		bool stillTacticalAfterFirstHit = fsm.CurrentState.GetType().Name == "TacticalFSMState";
		float progressAfterFirstHit = human.currentInvestigation != null ? human.currentInvestigation.Progress01 : -1f;

		// 위협이 지속돼 다음 틱도 계속 맞는 중이면(위협 콜라이더 지속 등) 다시 절반이 깎이면 안 된다
		// — 5-6장은 "중단되면 50%"이지 매 틱 반복 손실이 아니다(1회성 확인).
		human.isHitThisTurn = true;
		human.JudgeState();
		human.ExecuteAction();
		float progressAfterSecondHit = human.currentInvestigation != null ? human.currentInvestigation.Progress01 : -1f;

		Check(
			"5-6 조사 중 피격(같은 상태 내부 전환)은 진행도 50% 손실 후 유지(수정 완료, 1회성)",
			enteredTactical && stillTacticalAfterFirstHit
				&& Mathf.Abs(progressAfterFirstHit - 0.3f) < 0.001f
				&& Mathf.Abs(progressAfterSecondHit - 0.3f) < 0.001f,
			$"enteredTactical={enteredTactical}, stillTacticalAfterFirstHit={stillTacticalAfterFirstHit}, " +
			$"afterFirstHit={progressAfterFirstHit} (expected 0.3), afterSecondHit={progressAfterSecondHit} (expected 0.3, no double loss)");
	}

	private static void TrapDisarm_InterruptedByHit_ProgressIsHalvedOnce()
	{
		var human = ScriptableObject.CreateInstance<Human>();
		human.position = new Vector2Int(0, 0);
		human.currentTrapInteraction = new TrapInteractionState
		{
			TrapObjectId = "trap",
			TrapPosition = new Vector3Int(0, 0, 0),
			Phase = TrapPhase.Disarming,
			JoinWaitElapsed = true,
			DisarmProgress01 = 0.6f,
			PenaltyActive = true,
		};
		var trapObj = new InteractableObject(
			"trap", new Vector3Int(0, 0, 0), baseInterest: 0f, baseDanger: 30f,
			tags: new List<string> { "Object/Building/Passable/Trap" });
		AttachMinimalSession(human, trapObj);

		human.isHitThisTurn = true;
		bool canDisarm = CallCanDisarm(human);
		float progressAfterHit = human.currentTrapInteraction != null ? human.currentTrapInteraction.DisarmProgress01 : -1f;

		human.isHitThisTurn = true;
		bool canDisarmSecond = CallCanDisarm(human);
		float progressAfterSecondHit = human.currentTrapInteraction != null ? human.currentTrapInteraction.DisarmProgress01 : -1f;

		Check(
			"9-7 함정 해제 중 피격은 진행도 50% 손실 후 유지(수정 완료, 1회성)",
			!canDisarm && !canDisarmSecond
				&& Mathf.Abs(progressAfterHit - 0.3f) < 0.001f
				&& Mathf.Abs(progressAfterSecondHit - 0.3f) < 0.001f,
			$"canDisarm={canDisarm}, canDisarmSecond={canDisarmSecond}, afterHit={progressAfterHit} (expected 0.3), afterSecondHit={progressAfterSecondHit} (expected 0.3, no double loss)");
	}

	// CanDisarm은 TacticalFSMState의 private static — 리플렉션으로 직접 호출해 BT 전체를 안 돌려도
	// 되게 한다(트랩 통과 판정 등 부수 로직까지 갖출 필요 없이 이 조건 함수 하나만 검증).
	private static bool CallCanDisarm(Unit unit)
	{
		var method = typeof(TacticalFSMState).GetMethod("CanDisarm", BindingFlags.NonPublic | BindingFlags.Static);
		return (bool)method.Invoke(null, new object[] { unit });
	}

	private static bool CallCanInvestigate(Unit unit)
	{
		var method = typeof(TacticalFSMState).GetMethod("CanInvestigate", BindingFlags.NonPublic | BindingFlags.Static);
		return (bool)method.Invoke(null, new object[] { unit });
	}

	// 8-2장 검증 공통 셋업 — 파티 안에 조사 중인 interactor와 그를 호위(EscortTarget=interactor) 중인
	// escort를 만들고, escort가 이번 틱에 피격당한 상황을 구성한다.
	private static Human BuildEscortedInvestigator(out Human escort, string investigateTargetId)
	{
		var party = new Party("p1", "TestParty");

		var interactor = ScriptableObject.CreateInstance<Human>();
		interactor.position = new Vector2Int(0, 0);
		interactor.currentInvestigation = new InvestigationState
		{
			TargetObjectId = investigateTargetId,
			TargetPosition = new Vector3Int(0, 0, 0),
			Progress01 = 0.6f,
			PenaltyActive = true,
		};
		AttachMinimalSession(interactor, new InteractableObject(investigateTargetId, new Vector3Int(0, 0, 0), 10f));
		interactor.party = party;

		escort = ScriptableObject.CreateInstance<Human>();
		escort.position = new Vector2Int(1, 0);
		escort.party = party;
		escort.currentFormation = new FormationState { EscortTarget = interactor };
		escort.isHitThisTurn = true;

		party.Members.Add(interactor);
		party.Members.Add(escort);

		return interactor;
	}

	private static void SetWaveDummyTarget(InteractableObject obj)
	{
		var waveManager = new HumanWaveManager();
		waveManager.dummyTarget = obj;
		typeof(HumanWaveManager)
			.GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)
			.SetValue(null, waveManager);
	}

	private static void Investigate_EscortHit_NonPartyObjective_Interrupts()
	{
		var interactor = BuildEscortedInvestigator(out _, "regular_loot");
		SetWaveDummyTarget(null); // 이번 웨이브의 파티 목표 없음(또는 다른 오브젝트) — 지금 조사 중인 건 그냥 일반 조사

		bool canInvestigate = CallCanInvestigate(interactor);
		float progress = interactor.currentInvestigation != null ? interactor.currentInvestigation.Progress01 : -1f;

		Check(
			"8-2 보호 유닛 피격 시 일반 조사(비-파티목표)는 중단",
			!canInvestigate && Mathf.Abs(progress - 0.3f) < 0.001f,
			$"canInvestigate={canInvestigate} (expected false), progress={progress} (expected 0.3)");
	}

	private static void Investigate_EscortHit_PartyObjective_Continues()
	{
		var targetObj = new InteractableObject("wave_goal", new Vector3Int(0, 0, 0), 10f);
		var interactor = BuildEscortedInvestigator(out _, "wave_goal");
		SetWaveDummyTarget(targetObj); // 지금 조사 중인 대상이 이번 웨이브의 파티 목표 오브젝트

		bool canInvestigate = CallCanInvestigate(interactor);
		float progress = interactor.currentInvestigation != null ? interactor.currentInvestigation.Progress01 : -1f;

		Check(
			"8-2 보호 유닛 피격 시 파티 목표 조사는 유지",
			canInvestigate && Mathf.Abs(progress - 0.6f) < 0.001f,
			$"canInvestigate={canInvestigate} (expected true), progress={progress} (expected 0.6, unhalved)");
	}

	private static void Investigate_InterruptedByPlayerCommand_ProgressIsHalved()
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
		AttachMinimalSession(human, new InteractableObject("dummy", new Vector3Int(0, 0, 0), 10f));

		var fsm = human.fsm;

		human.JudgeState();
		human.ExecuteAction();
		bool enteredTactical = fsm.CurrentState.GetType().Name == "TacticalFSMState";

		human.playerMoveTarget = new Vector2Int(5, 5);
		human.isManualMoveCommand = true;
		human.JudgeState();

		bool switchedToPlayerCommand = fsm.CurrentState.GetType().Name == "PlayerCommandFSMState";
		float progress = human.currentInvestigation != null ? human.currentInvestigation.Progress01 : -1f;

		Check(
			"5-6 조사 중 플레이어 명령(완전 전환)은 진행도 50% 손실",
			enteredTactical && switchedToPlayerCommand && Mathf.Abs(progress - 0.3f) < 0.001f,
			$"enteredTactical={enteredTactical}, switchedToPlayerCommand={switchedToPlayerCommand}, progress={progress} (expected 0.3)");
	}
}
