using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Haare.Util.Logger;

// 유닛 프리팹에 붙는 애니메이션 슬롯 구동기. 프리팹 인스펙터의 슬롯에 클립을 꽂으면 그 상황에서 재생된다.
//  - 슬롯 목록은 Tools(new)/유닛/애니메이션 슬롯 동기화가 units.json 기준으로 만들어 둔다(꽂은 클립은 보존).
//  - 클립이 비어 있는 슬롯은 트리거를 무시한다 → 비어 있는 상황은 동작이 바뀌지 않는다.
//  - 클립 경로는 이 프리팹 안의 첫 Animator가 달린 GameObject 기준이다. 없으면 이 GameObject에 컨트롤러 없는
//    Animator를 만든다(PlayableGraph 출력 대상으로만 쓴다).
public class UnitAnimationDriver : MonoBehaviour
{
	[Serializable]
	public class SlotEntry
	{
		public AnimSlot      slot;
		public string        skillName;   // slot == Skill일 때만 의미
		public AnimationClip clip;
	}

	private const float MoveThreshold       = 0.001f;
	private const int   StopDebounceFrames  = 4;
	private const float FinishEpsilon       = 1e-3f;

	[SerializeField] private List<SlotEntry> slots = new List<SlotEntry>();
	[Tooltip("슬롯 전환 크로스페이드 시간(초). 0이면 즉시 컷.")]
	[SerializeField] private float fadeSeconds = 0.1f;
	[Tooltip("리깅 캐릭터의 좌우 반전 대상(부위 스프라이트들을 감싼 부모). 비워 두면 단일 스프라이트의 flipX를 쓴다.")]
	[SerializeField] private Transform mirrorRoot;

	public IList<SlotEntry> Slots      => slots;
	public bool UsesMirrorRoot         => mirrorRoot != null;

	private Animator                 _animator;
	private PlayableGraph            _graph;
	private AnimationMixerPlayable   _mixer;
	private AnimationClipPlayable[]  _playables;
	private AnimSlot[]               _inputSlot;
	private string[]                 _inputSkill;
	private float[]                  _clipLength;
	private float[]                  _weights;
	// 프레임마다 도는 경로에서 문자열 키를 만들지 않도록 슬롯 → 입력 번호를 배열로 들고 있다(AnimSlot 값은 0부터 연속).
	private readonly int[] _slotInput = CreateEmptySlotTable();
	private readonly Dictionary<string, int> _skillInput = new Dictionary<string, int>();
	private Func<AnimSlot, bool> _hasClip;

	private static int[] CreateEmptySlotTable()
	{
		var table = new int[Enum.GetValues(typeof(AnimSlot)).Length];
		for (int i = 0; i < table.Length; i++) table[i] = -1;
		return table;
	}

	private Unit  _unit;
	private int   _current = -1;
	private int   _oneShotInput = -1;
	private bool  _dead;
	private int   _deathInput = -1;

	private readonly AnimMoveTracker _move = new AnimMoveTracker(StopDebounceFrames);
	private Vector3 _lastPos;

	// 디버그 라벨 — 지금 재생 중인 슬롯 이름. 재생 중인 클립이 없으면 빈 문자열.
	public string CurrentLabel
	{
		get
		{
			if (_current < 0) return "";
			AnimSlot s = _inputSlot[_current];
			return s == AnimSlot.Skill ? "스킬:" + _inputSkill[_current] : AnimSlotCatalog.Get(s).Label;
		}
	}

	public void Bind(Unit unit) { _unit = unit; }

	// 좌우 반전 — mirrorRoot가 있을 때만 스케일 x 부호를 바꾼다(스킨드 스프라이트는 SpriteRenderer.flipX가 메쉬에 안 먹는다).
	public void ApplyMirror(bool flip)
	{
		if (mirrorRoot == null) return;
		Vector3 s = mirrorRoot.localScale;
		float want = flip ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
		if (Mathf.Approximately(s.x, want)) return;
		s.x = want;
		mirrorRoot.localScale = s;
	}

	private void Awake()
	{
		_lastPos = transform.position;
		BuildGraph();
	}

	private void OnDestroy()
	{
		if (_graph.IsValid()) _graph.Destroy();
	}

	private void BuildGraph()
	{
		_hasClip = s => _slotInput[(int)s] >= 0;

		var entries = new List<SlotEntry>();
		foreach (SlotEntry e in slots)
		{
			if (e == null || e.clip == null) continue;
			if (InputFor(e.slot, e.skillName) >= 0)
			{
				LogHelper.Warning(LogHelper.GAME, $"[UnitAnimationDriver] '{name}'에 슬롯 '{AnimSlotCatalog.KeyOf(e.slot, e.skillName)}'이 중복돼 뒤쪽 항목을 무시합니다.");
				continue;
			}
			if (e.slot == AnimSlot.Skill) _skillInput[e.skillName ?? ""] = entries.Count;
			else _slotInput[(int)e.slot] = entries.Count;
			entries.Add(e);
		}
		if (entries.Count == 0) return;

		_animator = GetComponentInChildren<Animator>(true);
		if (_animator == null) _animator = gameObject.AddComponent<Animator>();
		_animator.runtimeAnimatorController = null; // 컨트롤러가 있으면 그래프 출력과 충돌한다 — 이 드라이버가 전담
		_animator.applyRootMotion = false;

		int n = entries.Count;
		_playables  = new AnimationClipPlayable[n];
		_inputSlot  = new AnimSlot[n];
		_inputSkill = new string[n];
		_clipLength = new float[n];
		_weights    = new float[n];

		_graph = PlayableGraph.Create("UnitAnim_" + name);
		_graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
		AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Anim", _animator);
		_mixer = AnimationMixerPlayable.Create(_graph, n);
		output.SetSourcePlayable(_mixer);

		for (int i = 0; i < n; i++)
		{
			_playables[i]  = AnimationClipPlayable.Create(_graph, entries[i].clip);
			_inputSlot[i]  = entries[i].slot;
			_inputSkill[i] = entries[i].skillName;
			_clipLength[i] = entries[i].clip.length;
			_graph.Connect(_playables[i], 0, _mixer, i);
			_mixer.SetInputWeight(i, 0f);
		}

		int idle = InputFor(AnimSlot.Idle);
		if (idle >= 0) { _weights[idle] = 1f; _mixer.SetInputWeight(idle, 1f); _current = idle; }
		_graph.Play();
	}

	public bool HasClip(AnimSlot slot, string skillName = null) => InputFor(slot, skillName) >= 0;

	private int InputFor(AnimSlot slot, string skillName = null)
	{
		if (slot != AnimSlot.Skill) return _slotInput[(int)slot];
		return _skillInput.TryGetValue(skillName ?? "", out int idx) ? idx : -1;
	}

	// 이벤트로 시작하는 원샷 슬롯. 클립이 없거나 우선순위에서 밀리면 false(아무 일도 안 일어난다).
	public bool Trigger(AnimSlot slot, string skillName = null, float castSeconds = 0f)
	{
		if (!_graph.IsValid()) return false;
		int idx = InputFor(slot, skillName);
		if (idx < 0 || AnimSlotCatalog.Get(slot).Kind == AnimSlotKind.Loop) return false;

		bool stunnedVisible = ReadInputs().Stunned && HasClip(AnimSlot.Stunned);
		bool hasActive = _oneShotInput >= 0;
		AnimSlot active = hasActive ? _inputSlot[_oneShotInput] : AnimSlot.Idle;
		if (!UnitAnimMath.AcceptsTrigger(slot, hasActive, active, stunnedVisible, _dead)) return false;

		float speed = slot == AnimSlot.Skill ? UnitAnimMath.FitSpeed(_clipLength[idx], castSeconds) : 1f;
		_oneShotInput = idx;
		Enter(idx, speed);
		return true;
	}

	public bool PlaySkill(string skillName, float castMs) => Trigger(AnimSlot.Skill, skillName, castMs / 1000f);

	// 사망 시작 — Death 클립이 있으면 클립 길이(초)를 돌려주고, 없으면 0(호출한 쪽이 즉시 정리).
	public float BeginDeath()
	{
		_unit = null;
		int idx = InputFor(AnimSlot.Death);
		if (!_graph.IsValid() || idx < 0 || _dead) return 0f;
		_dead = true;
		_deathInput = idx;
		_oneShotInput = -1;
		Enter(idx, 1f);
		return _clipLength[idx];
	}

	private void Enter(int idx, float speed)
	{
		_playables[idx].SetTime(0d);
		_playables[idx].SetSpeed(speed);
		_current = idx;
	}

	private void Update()
	{
		Vector3 pos = transform.position;
		_move.Update((pos - _lastPos).sqrMagnitude > MoveThreshold * MoveThreshold);
		_lastPos = pos;

		if (!_graph.IsValid()) return;

		AnimPollInputs inputs = ReadInputs();
		int target;
		if (_dead)
		{
			target = _deathInput;
		}
		else if (inputs.Stunned && HasClip(AnimSlot.Stunned))
		{
			_oneShotInput = -1;   // 기절이 진행 중인 원샷을 끊는다
			target = InputFor(AnimSlot.Stunned);
		}
		else
		{
			if (_oneShotInput >= 0 && _playables[_oneShotInput].GetTime() >= _clipLength[_oneShotInput] - FinishEpsilon)
				_oneShotInput = -1;
			target = _oneShotInput >= 0
				? _oneShotInput
				: InputFor(UnitAnimMath.ResolveLoopSlot(inputs, _hasClip));
		}

		if (target != _current && target >= 0) Enter(target, 1f);
		else if (target < 0) _current = -1;

		KeepCurrentInRange();
		UnitAnimMath.StepWeights(_weights, _current, Time.deltaTime, fadeSeconds);
		for (int i = 0; i < _weights.Length; i++) _mixer.SetInputWeight(i, _weights[i]);
	}

	// 루프는 시간을 수동으로 감고(클립의 Loop Time 플래그에 의존하지 않는다), 사망은 마지막 프레임에서 멈춘다.
	private void KeepCurrentInRange()
	{
		if (_current < 0) return;
		double t = _playables[_current].GetTime();
		float len = _clipLength[_current];
		if (len <= 0f || t < len) return;

		AnimSlotKind kind = AnimSlotCatalog.Get(_inputSlot[_current]).Kind;
		if (kind == AnimSlotKind.Loop)
			_playables[_current].SetTime(UnitAnimMath.WrapTime((float)t, len));
		else if (kind == AnimSlotKind.Hold)
		{
			_playables[_current].SetTime(len);
			_playables[_current].SetSpeed(0f);
		}
	}

	private AnimPollInputs ReadInputs()
	{
		var i = new AnimPollInputs { Moving = _move.Moving };
		Unit u = _unit;
		if (u == null) return i;   // 파괴된 유닛(ScriptableObject)이면 Unity null

		i.Stunned = u.StatusEffects.State.stunDuration > 0f;

		TrapInteractionState trap = u.currentTrapInteraction;
		if (trap != null)
		{
			if (trap.Phase == TrapPhase.Disarming  && trap.PenaltyActive) i.Disarming = true;
			if (trap.Phase == TrapPhase.Destroying && trap.DestroyActive) i.Channeling = true;
		}
		if (u.currentAttackObjectTarget.HasValue) i.Channeling = true;
		if (u is Human h && h.currentInvestigation != null && h.currentInvestigation.PenaltyActive) i.Investigating = true;
		return i;
	}
}
