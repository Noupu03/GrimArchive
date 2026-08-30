using UnityEngine;
using DG.Tweening;

// 보스 골렘 전용 비주얼 컴포넌트 — 왼손/오른손은 순수 Transform이고 실제 피해는 SkillAction_Golem*
// 3종이 Hitbox로 독립 판정하며 여기서는 그에 맞춘 손 이동 연출만 담당한다(타이밍은 프레임 단위로
// 보장되지 않음). 본체 localScale은 footprint 배율과 무관하게 1배로 고정돼(9배 방지) 손 스프라이트는
// ApplyNormalSpriteScale이 부모 lossyScale의 역수로 보정한다.
public class BossGolemHandController : MonoBehaviour
{
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;

    [SerializeField] private float slamTravelDuration = 0.35f;
    [SerializeField] private float sweepTravelDuration = 0.6f;
    [SerializeField] private float clapTravelDuration = 0.3f;
    [SerializeField] private float slamLiftHeight = 1.5f;
    [SerializeField] private float clapApproachDistance = 1.5f;

    private Vector3 _leftRestLocalPos;
    private Vector3 _rightRestLocalPos;
    private bool _nextSlamUsesLeft;

    private void Awake()
    {
        if (leftHand != null)
        {
            ApplyNormalSpriteScale(leftHand);
            _leftRestLocalPos = leftHand.localPosition;
        }
        if (rightHand != null)
        {
            ApplyNormalSpriteScale(rightHand);
            _rightRestLocalPos = rightHand.localPosition;
        }
    }

    // 부모 lossyScale의 역수로 손을 월드 기준 1배로 되돌린다(부모 회전/추가 스케일 특수 케이스는 미대상).
    // ⚠️ localScale 부호는 절대 덮어쓰지 말 것 — 손 스프라이트가 방향별 한 벌뿐이라 반대쪽 손은
    // localScale.x=-1 반전으로 만들어지고, 좌우 반전은 SpriteRenderer.flipX가 아닌 이 부호가 담당한다.
    private static void ApplyNormalSpriteScale(Transform hand)
    {
        Vector3 parentScale = hand.parent != null ? hand.parent.lossyScale : Vector3.one;
        Vector3 authored = hand.localScale;
        hand.localScale = new Vector3(
            Mathf.Sign(authored.x) * (parentScale.x != 0f ? 1f / Mathf.Abs(parentScale.x) : 1f),
            Mathf.Sign(authored.y) * (parentScale.y != 0f ? 1f / Mathf.Abs(parentScale.y) : 1f),
            1f);
    }

    public static BossGolemHandController Get(Unit unit)
    {
        Transform root = unit?.Generate?.GetVisualTransform(unit);
        return root != null ? root.GetComponentInChildren<BossGolemHandController>() : null;
    }

    // 공격1: 손 하나가 대상 좌표 위로 들렸다가 내려찍는다 — 좌우 손을 번갈아 써서 연속 시전 시 손이
    // 복귀 전에 다시 뽑히는 문제를 막는다.
    public void PlaySlam(Vector3 worldTargetPos)
    {
        Transform hand = _nextSlamUsesLeft ? leftHand : rightHand;
        _nextSlamUsesLeft = !_nextSlamUsesLeft;
        if (hand == null) return;

        Vector3 restLocal = hand == leftHand ? _leftRestLocalPos : _rightRestLocalPos;
        Vector3 raised = worldTargetPos + Vector3.up * slamLiftHeight;

        hand.DOKill();
        DOTween.Sequence()
            .Append(hand.DOMove(raised, slamTravelDuration * 0.6f).SetEase(Ease.OutQuad))
            .Append(hand.DOMove(worldTargetPos, slamTravelDuration * 0.4f).SetEase(Ease.InQuad))
            .Append(hand.DOLocalMove(restLocal, slamTravelDuration * 0.5f).SetEase(Ease.OutQuad))
            .SetTarget(hand);
    }

    // 공격2: 손 하나가 A 좌표에서 B 좌표까지 쭉 훑고 지나간다. 실제 피해는 SkillAction_GolemSweep이
    // A→B 라인 히트박스로 독립적으로 판정한다.
    public void PlaySweep(Vector3 worldFromPos, Vector3 worldToPos)
    {
        Transform hand = rightHand != null ? rightHand : leftHand;
        if (hand == null) return;

        Vector3 restLocal = hand == leftHand ? _leftRestLocalPos : _rightRestLocalPos;

        hand.DOKill();
        hand.position = worldFromPos;
        DOTween.Sequence()
            .Append(hand.DOMove(worldToPos, sweepTravelDuration).SetEase(Ease.InOutSine))
            .Append(hand.DOLocalMove(restLocal, sweepTravelDuration * 0.5f).SetEase(Ease.OutQuad))
            .SetTarget(hand);
    }

    // 공격3: 양손이 대상 좌표를 좌우에서 동시에 협공(박수)한다.
    public void PlayClap(Vector3 worldTargetPos)
    {
        if (leftHand == null || rightHand == null) return;

        leftHand.DOKill();
        rightHand.DOKill();

        Vector3 leftApproach  = worldTargetPos + Vector3.left  * clapApproachDistance;
        Vector3 rightApproach = worldTargetPos + Vector3.right * clapApproachDistance;

        DOTween.Sequence()
            .Append(leftHand.DOMove(leftApproach, clapTravelDuration).SetEase(Ease.OutQuad))
            .Append(leftHand.DOMove(worldTargetPos, clapTravelDuration * 0.3f).SetEase(Ease.InQuad))
            .Append(leftHand.DOLocalMove(_leftRestLocalPos, clapTravelDuration * 0.5f).SetEase(Ease.OutQuad))
            .SetTarget(leftHand);

        DOTween.Sequence()
            .Append(rightHand.DOMove(rightApproach, clapTravelDuration).SetEase(Ease.OutQuad))
            .Append(rightHand.DOMove(worldTargetPos, clapTravelDuration * 0.3f).SetEase(Ease.InQuad))
            .Append(rightHand.DOLocalMove(_rightRestLocalPos, clapTravelDuration * 0.5f).SetEase(Ease.OutQuad))
            .SetTarget(rightHand);
    }
}
