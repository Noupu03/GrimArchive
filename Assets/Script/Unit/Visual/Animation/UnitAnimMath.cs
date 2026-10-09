using System;
using UnityEngine;

// 폴링으로 정하는 루프 슬롯의 입력 — 유닛 상태를 읽어 드라이버가 채운다.
public struct AnimPollInputs
{
	public bool Stunned;
	public bool Channeling;     // 코어/문 공격 채널링 또는 함정 파괴
	public bool Disarming;
	public bool Investigating;
	public bool Moving;
}

// 애니메이션 슬롯 판정의 순수 계산 모음 (부수효과 없음, 테스트 용이).
public static class UnitAnimMath
{
	public const float MinSkillSpeed = 0.5f;
	public const float MaxSkillSpeed = 3f;

	// 루프 슬롯 선택 — 조건이 맞아도 클립이 비어 있으면 건너뛰어 다음 후보로 떨어진다. Idle은 항상 마지막 폴백.
	public static AnimSlot ResolveLoopSlot(AnimPollInputs i, Func<AnimSlot, bool> hasClip)
	{
		if (i.Stunned       && hasClip(AnimSlot.Stunned))     return AnimSlot.Stunned;
		if (i.Channeling    && hasClip(AnimSlot.Channel))     return AnimSlot.Channel;
		if (i.Disarming     && hasClip(AnimSlot.DisarmTrap))  return AnimSlot.DisarmTrap;
		if (i.Investigating && hasClip(AnimSlot.Investigate)) return AnimSlot.Investigate;
		if (i.Moving        && hasClip(AnimSlot.Walk))        return AnimSlot.Walk;
		return AnimSlot.Idle;
	}

	// 원샷 트리거를 받아들일지. 사망은 항상, 기절이 보이는 중이거나 사망 후에는 그 외 전부 거부,
	// 진행 중인 원샷이 있으면 우선순위가 같거나 높을 때만(같으면 재시작).
	public static bool AcceptsTrigger(AnimSlot incoming, bool hasActiveOneShot, AnimSlot activeOneShot, bool stunnedVisible, bool dead)
	{
		if (incoming == AnimSlot.Death) return !dead;
		if (dead || stunnedVisible) return false;
		if (!hasActiveOneShot) return true;
		return AnimSlotCatalog.Get(incoming).Priority >= AnimSlotCatalog.Get(activeOneShot).Priority;
	}

	// 시전 시간에 클립 길이를 맞추는 재생 속도. 시전 시간이 없으면(즉발) 1배속.
	public static float FitSpeed(float clipLength, float castSeconds)
	{
		if (clipLength <= 0f || castSeconds <= 0f) return 1f;
		return Mathf.Clamp(clipLength / castSeconds, MinSkillSpeed, MaxSkillSpeed);
	}

	public static float WrapTime(float time, float length)
		=> length <= 0f ? 0f : time % length;

	// 목표 입력 쪽으로 가중치를 dt/fade 만큼 옮기고 합이 1이 되게 정규화한다. target<0이면 전부 0으로 줄어든다.
	public static void StepWeights(float[] weights, int target, float dt, float fadeSeconds)
	{
		if (weights == null || weights.Length == 0) return;
		float step = fadeSeconds <= 0f ? 1f : Mathf.Clamp01(dt / fadeSeconds);
		float sum = 0f;
		for (int i = 0; i < weights.Length; i++)
		{
			weights[i] = Mathf.MoveTowards(weights[i], i == target ? 1f : 0f, step);
			sum += weights[i];
		}
		if (sum > 1e-5f)
			for (int i = 0; i < weights.Length; i++) weights[i] /= sum;
	}
}

// 이동 여부 디바운스 — 움직인 프레임이 있으면 즉시 이동 중, 연속으로 안 움직인 프레임이 기준을 넘어야 정지로 본다.
public sealed class AnimMoveTracker
{
	private readonly int _debounceFrames;
	private int _stillFrames;
	public bool Moving { get; private set; }

	public AnimMoveTracker(int debounceFrames = 4) { _debounceFrames = Mathf.Max(1, debounceFrames); }

	public bool Update(bool movedThisFrame)
	{
		if (movedThisFrame) { _stillFrames = 0; Moving = true; }
		else if (++_stillFrames >= _debounceFrames) Moving = false;
		return Moving;
	}
}
