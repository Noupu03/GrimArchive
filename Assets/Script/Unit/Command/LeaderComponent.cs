using System.Collections.Generic;
using UnityEngine;

// 리더 명령 시스템의 유닛별 상태(리더 명령 설계명세서 v0.15) — PropagationComponent/RetreatComponent와 같은 패턴(Unit.Components에 등록, Unit이 LeaderState 프로퍼티로 노출).
// 파티 단위 상태(승계 시계·쿨타임·집중 공격 지정 등)는 Party.Command.cs에 있다. 명령을 '받은' 쪽의 개인 상태가 여기 있다.
public class LeaderComponent : IUnitComponent
{
	private readonly Unit _owner;

	// 통솔력 일시 보정 — 출처 키 → 변화량. 현재 통솔력 = 기본 통솔력 + 합계(0~100 클램프). 버프·디버프 시스템이 생기면 SetLeadershipModifier로 넣는다(13-1장: 전파 시점의 버프·디버프가 반영된 값).
	private readonly Dictionary<string, float> _leadershipModifiers = new Dictionary<string, float>();

	// 기본 버프·사망 충격이 쓰는 세부 스테이터스 가산 장부.
	public readonly StatModifierLedger Stats;

	// 사망 충격(18-1장)이 끝나는 시각(Time.time 기준, 0 = 충격 없음). 중첩하지 않고 새 사망을 알 때마다 남은 시간을 새 10초로 갱신한다.
	public float DeathShockUntil;

	// 상위 지휘관(원정 편성 단계에서 지정, 원정당 최대 한 명, 승계 없음 — 16장). 지금은 코드 API·debug 메뉴로만 지정한다(파티 구성 문서 대기).
	public bool IsSuperiorCommander;

	// 상위 지휘관의 상위 공동 공격 상태(ExpeditionCommandSystem) — 쿨타임은 지휘자별이라 상위 지휘관 개인에게 둔다(상위 15초, 일반 집중 공격 등 10초와 함께 기다린다).
	public float SuperiorCooldownUntil;
	public float NextSuperiorTick;
	public string SuperiorFocusCommandId;
	public Unit SuperiorFocusTarget;

	// ── 받은 명령(개인 상태) ──
	// 일반 리더의 집중 공격 지정과 상위 지휘관의 상위 공동 공격 지정은 따로 들고, 상위가 있으면 일반을 잠시 중단한다(16-1장). 상위가 끝나면 아직 끝나지 않은 일반만 재개한다.
	public FocusOrder GeneralFocus;
	public FocusOrder SuperiorFocus;
	public ActionOrder Action;      // 행동 우선 명령(강공격·제압·보호) — 새 명령은 이전 우선순위를 대체한다
	public float NextFocusTick;     // FocusAttackSystem.Tick 스로틀

	// 지금 적용 중인 기본 버프(LeaderBuffSystem) — 같은 강도·종류면 다시 적용하지 않기 위한 기록.
	public float BuffRate;
	public PartyConcept BuffConcept;
	public string BuffSource;
	public float NextBuffTick;

	// 지금 따르는 집중 공격 — 상위 명령이 있으면 그것, 없으면 중단되지 않은 일반 명령.
	public FocusOrder EffectiveFocus
	{
		get
		{
			if (SuperiorFocus != null) return SuperiorFocus;
			return GeneralFocus != null && !GeneralFocus.Suspended ? GeneralFocus : null;
		}
	}

	public LeaderComponent(Unit owner)
	{
		_owner = owner;
		Stats = new StatModifierLedger(owner);
	}

	public void ClearAllOrders()
	{
		GeneralFocus = null;
		SuperiorFocus = null;
		Action = null;
	}

	// 일시 보정 전 값 — 리더 선정과 기본 버프 자격에 쓴다.
	public float BaseLeadership => _owner.BaseStat != null ? _owner.BaseStat.leadership : 0f;

	// 보정을 반영한 값 — 새 명령 자격·전파 반경·효과 강도·정신력 감소 완화에 쓴다.
	public float CurrentLeadership
	{
		get
		{
			float sum = 0f;
			foreach (var v in _leadershipModifiers.Values) sum += v;
			return LeaderMath.ClampLeadership(BaseLeadership + sum);
		}
	}

	public void SetLeadershipModifier(string source, float delta)
	{
		if (delta == 0f) _leadershipModifiers.Remove(source);
		else _leadershipModifiers[source] = delta;
	}

	public void RemoveLeadershipModifier(string source) => _leadershipModifiers.Remove(source);

	public float GetLeadershipModifier(string source) => _leadershipModifiers.TryGetValue(source, out float v) ? v : 0f;

	public void OnUpdate(float deltaTime) { }
	public void OnDespawn() { }
}
