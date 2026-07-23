using System.Collections.Generic;
using UnityEngine;

// ==========================================
// GOAP Architecture
// ==========================================

public class GoapState : Dictionary<string, bool> { }

public abstract class GoapGoal
{
	public string    Name;
	public GoapState DesiredState = new GoapState();
	public abstract float GetPriority(Unit unit);

	// 03문서 2-1장 "이미 수행 중인 조사나 함정 해제는 더 높은 우선순위 후보가 새로 발생했다는 이유
	// 만으로 자동 중단하지 않는다"/10장 "대기 목적이 해결되지 않았다면 대기를 유지"를 구현하기 위한
	// 고착 목표 플래그. 기본값 false = 기존 4개 목표(Panic/PlayerCommand/DefeatEnemy/Explore)는
	// 지금처럼 매 틱 최고점을 다시 고른다(동작 변경 없음). true를 반환하는 목표만 아래 ShouldInterrupt
	// 조건이 충족되기 전까지 다른 목표의 점수와 무관하게 유지된다(시야인지반응_03_GOAP목표우선순위표
	// _2026-07-22.txt 0-1절 참고).
	public virtual bool IsSticky(Unit unit) => false;

	// IsSticky가 true인 동안 이 목표를 실제로 내려놓아야 하는 조건. 기본 구현은 스스로 조건이
	// 소멸했는지(GetPriority<=0)만 본다 — 고착 목표(TrapResponse/Investigate/Wait)는 이걸 오버라이드해
	// 문서가 명시하는 개별 중단 조건(피격/적 정확 인지/위협 콜라이더 인지 등)을 추가로 검사한다.
	public virtual bool ShouldInterrupt(Unit unit) => GetPriority(unit) <= 0f;
}

public abstract class GoapAction
{
	public string    ActionName;
	public float     Cost = 1f;
	public GoapState Preconditions = new GoapState();
	public GoapState Effects       = new GoapState();

	// 머리 위 상태 라벨(GoapBrain.PlanText)에 표시할 숫자 — Actions.cs 순서 그대로 1~19를 매긴다.
	// 0은 "계획 없음" 전용 값이라 실제 Action은 절대 0을 쓰지 않는다(사용자 요청, 2026-07-22: 라벨은
	// 문자열이 아니라 숫자로만 표시).
	public abstract int ActionCode { get; }

	public void AddPrecondition(string key, bool value) => Preconditions[key] = value;
	public void AddEffect(string key, bool value)       => Effects[key]       = value;

	public abstract bool IsValid(Unit unit);
	public abstract void Execute(Unit unit);

	protected Unit GetClosestEnemy(Unit unit, out float minDist)
	{
		// 인간 진영도 몬스터와 동일하게 개인 시야(PerceptionState.personalSpottedEnemies)만 사용 — 진영 공유 시야 제거.
		IEnumerable<Unit> enemies = unit.GetComponent<PerceptionComponent>().State.personalSpottedEnemies;

		Unit  target  = null;
		minDist = float.MaxValue;

		foreach (var enemy in enemies)
		{
			if (enemy == null || enemy.GetComponent<HealthComponent>().hp <= 0 || enemy.currentFloor != unit.currentFloor) continue;
			float d = Vector2Int.Distance(unit.position, enemy.position);
			if (d < minDist) { minDist = d; target = enemy; }
		}
		return target;
	}

	// AStarNode has been moved to AStarMovement.cs

	protected void MoveTowardsPos(Unit unit, Vector2Int targetPos)
	{
		if (unit.MovementAlgorithm != null)
		{
			if (unit.MovementAlgorithm.TryGetNextStep(unit, targetPos, out Dir nextDir))
			{
				unit.Move(nextDir);
			}
		}
	}

	// FallbackMove and GetHeuristic are now encapsulated in MovementAlgorithms

	protected void MoveTowardsTarget(Unit unit, Unit target) => MoveTowardsPos(unit, target.position);

	protected void MoveAwayFromTarget(Unit unit, Unit target, float desiredDist)
	{
		Vector2 away = (Vector2)(unit.position - target.position);
		if (away == Vector2.zero) away = Vector2.right;

		// 체비쇼프 스케일: max(|x|,|y|)=1로 정규화 → 대각선 방향도 정확한 타일 거리로 이동
		float maxComp = Mathf.Max(Mathf.Abs(away.x), Mathf.Abs(away.y));
		Vector2 scaled = away / maxComp;

		int targetDist = Mathf.RoundToInt(desiredDist);
		Vector2Int retreatPos = target.position + new Vector2Int(
			Mathf.RoundToInt(scaled.x * targetDist),
			Mathf.RoundToInt(scaled.y * targetDist)
		);
		MoveTowardsPos(unit, retreatPos);
	}

	// 03문서 6장 보호 포메이션 — Action_MoveToEscortSlotMelee/Ranged가 공유하는 이동 로직(6-4/6-5장
	// 배치 3단계 폴백까지는 아직 구현하지 않고, "상호작용 유닛 기준 지정 거리·방향" 1단계만 구현한
	// 간이 버전 — 시야인지반응_03_GOAP목표우선순위표_2026-07-22.txt 3-5-1절이 예고한 FormationMath
	// 모듈로 나중에 대체할 자리). backDistance<=1이면 근접(6-4장 인접 배치 간이화), 그보다 크면
	// 원거리(6-5장 후방 배치)로 취급한다. 실제 슬롯 좌표 공식은 Human.GetEscortSlotPosition(Unit.cs)와
	// GoapWorldState.Build의 atEscortSlot 판정이 같은 걸 쓰도록 공유한다.
	protected void MoveToEscortSlot(Human human, float backDistance)
	{
		if (human.currentFormation == null || human.currentFormation.EscortTarget == null || !human.currentFormation.EscortTarget.IsInteracting)
		{
			Human target = human.FindDirectlyVisibleInteractingAlly();
			if (target == null) { human.currentFormation = null; return; }
			human.currentFormation = new FormationState { EscortTarget = target };
		}

		Human escortTarget = human.currentFormation.EscortTarget;
		Vector2Int slot = human.GetEscortSlotPosition(escortTarget, backDistance);
		MoveTowardsPos(human, slot);
	}
}

// ==========================================
// GOAP Brain (Agent)
// ==========================================
public class GoapBrain
{
	protected List<GoapGoal>   availableGoals;
	protected List<GoapAction> availableActions;
	protected GoapGoal         currentGoal;

	// 예전엔 "목표에 맞는 액션 1개"만 즉시 매칭했지만, 이제 GoapPlanner가 실제로 액션을 체이닝한
	// 시퀀스(계획)를 만들어 여기 담아두고 한 틱에 한 스텝씩 소비한다. 계획이 깨지는 특정 트리거(아래
	// JudgeState의 재계획 조건 참고)가 발생할 때만 다시 계획을 세운다 — 매 틱 통째로 다시 고르지 않는다.
	protected readonly Queue<GoapAction> currentPlan = new Queue<GoapAction>();

	// UnitVisual 상태 라벨 등 외부에서 "지금 이 유닛이 뭘 하고 있는지"를 읽어야 할 때 쓴다.
	public GoapGoal CurrentGoal => currentGoal;

	// 머리 위 상태 라벨용 "앞으로 실행할 계획" — 과거에 실행한 목표/액션을 누적해서 보여주던 이전
	// 방식(GoalTrailText) 대신, currentPlan에 지금 남아있는 액션들을 ActionCode 숫자로 순서대로
	// 나열한다(예: "6-7" = MoveToTrap→TrapDisarmPerform, Actions.cs 등록 순서 1~19 참고). 이미
	// 실행이 끝난 스텝은 ExecuteAction이 Dequeue하면서 currentPlan에서 곧바로 빠지므로 자연히
	// 화면에서도 사라진다 — 별도로 "지운다"는 로직이 필요 없다(사용자 요청, 2026-07-22 — 문자열이
	// 아니라 숫자로만 표시).
	//
	// MoveToPlayerTarget(2)/CompletePlayerCommand(3)은 플레이어가 직접 내린 이동 명령과
	// HumanWaveManager가 파티 전체에 자동으로 내리는 웨이브 회수/퇴각 이동이 같은 액션 클래스를
	// 공유한다(둘 다 unit.playerMoveTarget 기반) — 라벨만으로는 구분이 안 된다는 사용자 지적에 따라,
	// unit.isManualMoveCommand(둘의 유일한 차이 — InputManager는 true, HumanWaveManager는 false로
	// 세팅)를 여기서 확인해 자동이동인 경우 20/21로 바꿔 표시한다. Preconditions/Effects/Cost는 완전히
	// 동일한 같은 액션이라 GoapAction 자체를 분리할 이유는 없어(§8-0 "공유해도 안전" 원칙과 동일한
	// 이유), 표시용 코드만 여기서 갈라준다.
	// UnitGenerate.SyncVisuals가 (거의) 매 프레임, 씬의 모든 유닛에 대해 이 메서드를 호출한다 — 계획이
	// 그대로인 유닛까지 매번 List<int>+string.Join을 새로 할당하면 유닛 수만큼 GC 압박이 쌓여 프레임
	// 드랍의 큰 원인이 된다(2026-07-22, 사용자 신고). currentPlan이 실제로 바뀔 때만(_planVersion 증가
	// — JudgeState/ExecuteAction에서 Enqueue/Dequeue/Clear할 때마다 올림) 문자열을 다시 만들고, 그
	// 외엔 캐시된 문자열을 그대로 반환한다. isManualMoveCommand는 계획 변경 없이도 바뀔 수 있어(2/3↔
	// 20/21 라벨에 영향) 캐시 키에 같이 넣는다.
	private int    _planVersion;
	private int    _cachedPlanTextVersion = -1;
	private bool   _cachedPlanTextManual;
	private string _cachedPlanText = "0";

	public string PlanText(Unit unit)
	{
		bool manual = unit.isManualMoveCommand;
		if (_cachedPlanTextVersion == _planVersion && _cachedPlanTextManual == manual) return _cachedPlanText;

		_cachedPlanTextVersion = _planVersion;
		_cachedPlanTextManual  = manual;

		if (currentPlan.Count == 0) { _cachedPlanText = "0"; return _cachedPlanText; }

		var steps = new List<int>(currentPlan.Count);
		foreach (var action in currentPlan)
		{
			int code = action.ActionCode;
			if (!manual)
			{
				if (action is Action_MoveToPlayerTarget) code = 20;
				else if (action is Action_CompletePlayerCommand) code = 21;
			}
			steps.Add(code);
		}
		_cachedPlanText = string.Join("-", steps);
		return _cachedPlanText;
	}

	public void JudgeState(Unit unit)
	{
		if (availableGoals == null)
			availableGoals = new List<GoapGoal>
			{
				new Goal_Panic(), new Goal_UseStairs(), new Goal_TrapResponse(), new Goal_PlayerCommand(), new Goal_DefeatEnemy(),
				new Goal_Alert(), new Goal_Investigate(), new Goal_Wait(), new Goal_ProtectiveFormation(),
				new Goal_Explore()
			};

		if (availableActions == null)
			availableActions = new List<GoapAction>
			{
				new Action_Panic(),
				new Action_MoveToStairs(), new Action_CrossStairs(),
				new Action_MoveToPlayerTarget(), new Action_CompletePlayerCommand(),
				new Action_EngageEnemy(),
				new Action_TrapJoinWait(), new Action_MoveToTrap(), new Action_TrapDisarmPerform(),
				new Action_TrapBypass(), new Action_TrapPass(), new Action_TrapDestroy(),
				new Action_MoveToInvestigateTarget(), new Action_InvestigatePerform(),
				new Action_AlertApproach(), new Action_AlertPerimeterSearch(),
				new Action_Wait(),
				new Action_MoveToEscortSlotMelee(), new Action_MoveToEscortSlotRanged(), new Action_HoldFormation(),
				new Action_RandomExplore()
			};

		// 최우선 목표 선택 — 03문서 2-1장/10장의 "고착 목표"(IsSticky) 규칙: 현재 활성 목표가 고착
		// 상태이고 아직 그 목표의 ShouldInterrupt 조건이 충족되지 않았다면, 다른 목표의 점수가 더
		// 높아도 무시하고 그대로 유지한다(시야인지반응_03_GOAP목표우선순위표_2026-07-22.txt 0-1절).
		// 이 "목표 선택" 레이어는 실제 GOAP 플래너 위에 놓이는 표준적인 목표 관리자 레이어라 그대로
		// 유지한다 — 바뀌는 건 목표가 정해진 다음 "어떻게 도달하는가"(아래 계획 수립) 뿐이다.
		GoapGoal previousGoal = currentGoal;
		GoapGoal bestGoal;
		// "다음 우선순위 목표"(사용자 요청, 2026-07-22) — bestGoal의 계획이 끝나자마자(예: 전투가
		// 끝나자마자) 곧바로 이어서 할 일을 지금 미리 계획해두기 위해, 이번 스캔에서 두 번째로 높은
		// 우선순위였던 목표도 함께 기억해둔다. 고착 유지 중(위 IsSticky 분기)에는 이번 틱에 전체 스캔을
		// 안 도므로 null — 그래도 무방하다(고착 중엔 대개 replanNeeded 자체가 안 걸림, 아래 참고).
		GoapGoal secondGoal = null;

		if (currentGoal != null && currentGoal.IsSticky(unit) && !currentGoal.ShouldInterrupt(unit))
		{
			bestGoal = currentGoal;
		}
		else
		{
			bestGoal = null;
			float bestPriority = -1f, secondPriority = -1f;

			foreach (var goal in availableGoals)
			{
				float priority = goal.GetPriority(unit);
				if (priority > bestPriority)
				{
					secondGoal = bestGoal; secondPriority = bestPriority;
					bestGoal = goal; bestPriority = priority;
				}
				else if (priority > secondPriority)
				{
					secondGoal = goal; secondPriority = priority;
				}
			}
		}

		// 03문서 4-8장: "전투 종료 → 10초 동안 경계 상태로 주변 이동·확인" — DefeatEnemy가 활성이다가
		// 이번 틱에 더 이상 아니게 된 순간(=전투 종료 순간)이 정확히 그 트리거다. 다른 고착 목표
		// 재판단과 독립적으로, "직전 목표가 DefeatEnemy였는가"만 보면 되므로 currentGoal을 덮어쓰기
		// 직전 값(previousGoal)으로 확인한다.
		if (previousGoal is Goal_DefeatEnemy && !(bestGoal is Goal_DefeatEnemy) && unit.currentAlertSearch == null)
		{
			unit.currentAlertSearch = new AlertSearchState { IsPostCombatSweep = true };
		}

		currentGoal = bestGoal;

		GoapState worldState = GoapWorldState.Build(unit);

		// 재계획 트리거 — "변수가 생기면 그때그때 계획을 변경"의 실제 구현. 이 넷 중 하나라도 참이
		// 아니면 지금 계획을 그대로 유지하고 다음 스텝을 이어서 실행한다(매 틱 다시 세우지 않음).
		bool replanNeeded = currentPlan.Count == 0 || previousGoal != bestGoal;
		if (!replanNeeded)
		{
			GoapAction nextAction = currentPlan.Peek();
			if (!nextAction.IsValid(unit) || !GoapPlanner.PreconditionsMet(worldState, nextAction))
				replanNeeded = true;
		}

		if (replanNeeded)
		{
			currentPlan.Clear();
			_planVersion++;
			if (bestGoal != null)
			{
				List<GoapAction> plan = GoapPlanner.Plan(unit, worldState, bestGoal.DesiredState, availableActions);
				if (plan == null && bestGoal is Goal_UseStairs)
				{
					// 사용자 신고(2026-07-23) 진단용 — Goal_UseStairs가 최우선으로 뽑혔는데도 계획을
					// 못 세우면(예: MaxDepth 초과, Precondition 불일치 등) 여기서 바로 드러난다.
					Haare.Util.Logger.LogHelper.Warning(Haare.Util.Logger.LogHelper.GAME,
						$"[GoapBrain] {unit.name}: Goal_UseStairs가 선택됐지만 계획을 못 세움(GoapPlanner.Plan==null).\");
				}
				if (plan != null)
				{
					foreach (var action in plan) currentPlan.Enqueue(action);

					// 다음 우선순위 목표까지 미리 이어붙이기(사용자 요청, 2026-07-22) — bestGoal의 계획이
					// 전부 끝난 뒤의 세계 상태(ApplyAll)를 기준으로 secondGoal의 계획도 이어서 세워 같은
					// 큐 뒤에 붙인다. 예: 전투(DefeatEnemy)가 끼어들어도 그 직전까지 하던 목표(퇴각/조사
					// 등)가 secondGoal로 잡혀 전투 계획 바로 뒤에 이어지므로, 전투가 실제로 끝나는 순간
					// (EngageEnemy의 Effects가 검증되는 시점) 재계획 없이 곧바로 다음 행동으로 넘어간다.
					//
					// Goal_Explore는 예외 — 다른 목표가 전혀 안 걸리면 사실상 항상 2등으로 잡히는
					// 최하위 폴백이라(완료 조건 자체가 없음), 이어붙이면 웨이브 이동(퇴각/루팅) 도착
					// 직후 바로 "무작위 배회"가 실행돼 도착 판정 전에 그 자리를 벗어나 복귀가 안 되는
					// 버그가 있었다(2026-07-22, 사용자 신고 "19번으로 고정된 인간 유닛들이 복귀를 안함").
					// Explore는 애초에 "체이닝해서 예약해둘 다음 행동"이 아니라 매 틱 새로 판단해야 할
					// 순수 폴백이라 아예 이어붙이기 대상에서 제외한다.
					if (secondGoal != null && !(secondGoal is Goal_Explore))
					{
						GoapState afterBestGoal = GoapPlanner.ApplyAll(worldState, plan);
						List<GoapAction> followUp = GoapPlanner.Plan(unit, afterBestGoal, secondGoal.DesiredState, availableActions);
						if (followUp != null) foreach (var action in followUp) currentPlan.Enqueue(action);
					}
				}
			}
		}

		if (!worldState["enemyVisible"]) unit.oneTimeReactUsed = false;
	}

	public void ExecuteAction(Unit unit)
	{
		// 캐스팅 중일 때는 UnitFunction.OnUpdate가 타이머/펜딩공격을 처리하므로 여기서는 그냥 대기
		if (unit.GetComponent<CombatStateComponent>().State.isCastingAttack) return;

		if (currentPlan.Count > 0)
		{
			GoapAction action = currentPlan.Peek();
			action.Execute(unit);

			// 여기가 예전엔 빠져있던 "Effects를 실제로 검증" 단계 — 선언만 믿지 않고 방금 실행한
			// 액션의 Effects가 실제 게임 상태에도 반영됐는지 다시 확인한 뒤에만 다음 스텝으로 넘어간다.
			// 아직이면(여러 틱짜리 진행도 상태머신 등) 같은 액션을 다음 틱에 그대로 재실행한다.
			GoapState freshState = GoapWorldState.Build(unit);
			if (GoapPlanner.EffectsSatisfied(freshState, action)) currentPlan.Dequeue();
		}
		else
		{
			Dir randomDir = (Dir)Random.Range(0, 8);
			unit.Move(randomDir);
		}

		unit.GetComponent<CombatStateComponent>().State.isHitThisTurn = false;
	}
}
