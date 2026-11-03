using System.Collections.Generic;

// 강공격을 검토할 이유(계기)의 추적(리더 명령 5-1-표2) — 순수 상태기계라 게임 객체 없이 시나리오를 테스트할 수 있다.
// 계기 4종: 새 교전 시작(1회) / 집중 공격 신규 지정·교체(실제 지정당 1회) / 공격 대상의 행동 불능 발생 / 확인한 적 HP 30% 이하(같은 적·같은 교전 1회).
// "계기는 명령을 검토할 이유"이고 쿨타임 종료 자체는 새 계기가 아니다. 발령 직전에도 계기가 유효해야 하며(근거 소멸 시 취소), 발령하면 그때 유효한 계기를 전부 소비해 여러 계기를 명령 횟수로 누적하지 않는다.
public sealed class StrikeTriggerTracker
{
	public struct EnemyView
	{
		public int Id;                  // 적 식별(유닛 인스턴스 id)
		public bool IsEngagementTarget; // 지금 교전 중인 적인가
		public bool IsAttackTarget;     // 리더 또는 파티의 현재 공격 대상(집중 공격 대상 포함)인가
		public bool HpUnlocked;         // 도감 이해도로 HP 정보가 해금됐는가(6항)
		public float HpRatio;           // 현재 HP ÷ 최대 HP — 현재 직접 확인한 값만 넣는다(과거 HP는 쓰지 않는다)
		public bool Incapacitated;      // 행동 불능(기절 등)
	}

	private bool _inEngagement;
	private bool _newEngagementPending;
	private string _seenFocusId;
	private bool _focusPending;
	private readonly HashSet<int> _incapacitatedUsed = new HashSet<int>();
	private readonly HashSet<int> _lowHpUsed = new HashSet<int>();
	private readonly HashSet<int> _stillIncapacitated = new HashSet<int>(); // Observe가 매번 채우는 임시 집합 — 리더 0.25초 틱마다 새로 만들지 않도록 재사용

	// 현재 유효한(소비되지 않은) 계기들 — Observe가 매번 다시 계산한다.
	private bool _hasIncapacitated;
	private readonly List<int> _incapacitatedNow = new List<int>();
	private readonly List<int> _lowHpNow = new List<int>();

	public bool HasValidTrigger => (_newEngagementPending && _inEngagement) || _focusPending || _hasIncapacitated || _lowHpNow.Count > 0;

	// 5-1장: 강공격은 "리더가 현재 교전 중인 적을 직접 확인하고" 계기가 성립할 때 판단한다 — 파티원만 적을 보고 리더는 교전 중인 적을 못 보면 계기가 있어도 판단하지 않는다(계기는 소비되지 않고 리더가 보면 그때 판단).
	private bool _leaderSeesEngagedEnemy;
	public bool LeaderSeesEngagedEnemy => _leaderSeesEngagedEnemy;
	public bool CanJudge => _leaderSeesEngagedEnemy && HasValidTrigger;
	public bool NewEngagementValid => _newEngagementPending && _inEngagement;
	public bool FocusValid => _focusPending;
	public bool IncapacitationValid => _hasIncapacitated;
	public bool LowHpValid => _lowHpNow.Count > 0;

	// 매 검토 시점마다 현재 상황을 넣는다. focusId는 지금 유효한 집중 공격 지정의 명령 식별(없으면 null).
	public void Observe(bool inEngagement, string focusId, IReadOnlyList<EnemyView> enemies)
	{
		if (inEngagement && !_inEngagement) _newEngagementPending = true;          // 전투 중이 아니던 파티가 전투를 시작하면 1회 판단
		if (!inEngagement && _inEngagement) EndEngagement();                       // 교전 종료 → 같은 교전 단위 이력을 모두 비운다
		_inEngagement = inEngagement;

		if (string.IsNullOrEmpty(focusId)) { _focusPending = false; _seenFocusId = null; }
		else if (focusId != _seenFocusId) { _focusPending = true; _seenFocusId = focusId; } // 같은 지정의 재전달·점수 변화는 새 계기가 아니다

		_incapacitatedNow.Clear();
		_lowHpNow.Clear();
		_hasIncapacitated = false;
		_leaderSeesEngagedEnemy = false;

		// 이번 검토에 보이지 않는 적의 행동 불능 이력은 풀어 둔다(회복 뒤 다시 걸리면 새 계기).
		var stillIncapacitated = _stillIncapacitated;
		stillIncapacitated.Clear();
		if (enemies != null)
		{
			foreach (var e in enemies)
			{
				if (e.IsEngagementTarget) _leaderSeesEngagedEnemy = true;
				if (e.Incapacitated) stillIncapacitated.Add(e.Id);
				if (e.IsAttackTarget && e.Incapacitated && !_incapacitatedUsed.Contains(e.Id)) _incapacitatedNow.Add(e.Id);
				if (e.IsEngagementTarget && e.HpUnlocked && LeaderMath.IsLowHp(e.HpRatio) && !_lowHpUsed.Contains(e.Id)) _lowHpNow.Add(e.Id);
			}
		}
		_incapacitatedUsed.RemoveWhere(id => !stillIncapacitated.Contains(id));
		_hasIncapacitated = _incapacitatedNow.Count > 0;
	}

	// 강공격을 실제로 발령했다 — 지금 유효한 계기를 전부 소비한다. 저HP 이력은 교전이 끝날 때까지 남아 같은 적으로 다시 발령하지 않는다(적용시간·쿨타임이 지나도 초기화되지 않음, L15).
	public void ConsumeAll()
	{
		_newEngagementPending = false;
		_focusPending = false;
		foreach (int id in _incapacitatedNow) _incapacitatedUsed.Add(id);
		foreach (int id in _lowHpNow) _lowHpUsed.Add(id);
		_incapacitatedNow.Clear();
		_lowHpNow.Clear();
		_hasIncapacitated = false;
	}

	// 지휘 상실·후퇴처럼 발령 자체가 취소되는 경우 — 계기를 소비하지 않고 모두 지운다(종료된 상황의 명령을 나중에 내리지 않는다, L16).
	public void CancelPending()
	{
		_newEngagementPending = false;
		_focusPending = false;
		_incapacitatedNow.Clear();
		_lowHpNow.Clear();
		_hasIncapacitated = false;
	}

	private void EndEngagement()
	{
		_newEngagementPending = false;
		_lowHpUsed.Clear();
		_incapacitatedUsed.Clear();
	}
}
