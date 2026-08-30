using UnityEngine;
using DG.Tweening;

// 보스 골렘 전용 비주얼 컴포넌트. 왼손/오른손은 Unit이 아니라 순수 Transform — 실제 피해 판정은
// SkillAction_Golem* 3종이 기존 Hitbox 시스템으로 처리하고, 여기서는 그 판정에 "대략 맞춰" 손을
// 움직이는 연출만 담당한다(재활용성 없는 보스 전용 기능이라 기존 스크립트 구조에서 벗어나도 됨).
// 손 애니메이션과 피해 판정 타이밍은 독립적으로 굴러간다 — slamTravelDuration 등을 baseDelayMs와
// 맞춰두면 대략 일치하지만 프레임 단위로 보장되진 않는다.
//
// 보스 프리팹의 Visual 계층 아래에 이 컴포넌트를 붙이고 leftHand/rightHand를 인스펙터에서 연결한다.
//
// 본체(풋프린트)는 기본 몬스터의 3배지만 원본 아트 자체가 이미 3배로 그려져 있어, footprint 배율을
// 그대로 localScale에 곱하면 9배가 되는 버그가 있었다 — UnitVisualDefinition.visualScaleIgnoresFootprint를
// 켜서 footprint는 게임플레이 판정에만 쓰고 루트 localScale은 1배로 유지한다(BossGolemVisualScaleFix.cs).
// 손 일러스트는 일반 유닛 스프라이트 크기라, 아래 ApplyNormalSpriteScale이 부모 lossyScale의 역수를
// 곱해 항상 정상 크기를 유지한다.
public class BossGolemHandController : MonoBehaviour
{
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;

    // 자리표시자 수치 — 실제 애니메이션/스킬 baseDelayMs 검증 후 튜닝
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

    // 부모(3배로 커진 본체) 스케일의 역수를 곱해 손을 월드 기준 1배로 되돌린다. 바로 위 부모의
    // lossyScale만 상쇄하면 충분하다(부모에 회전/추가 스케일까지 걸린 특수 케이스는 대상 아님).
    //
    // ⚠️ 프리팹에 저장된 localScale의 부호는 절대 덮어쓰지 말 것 — 손 스프라이트가 방향별 한 벌뿐이라
    // 반대쪽 손은 좌우 반전(localScale.x = -1)으로 만든다. 크기 보정을 양수로 덮어쓰면 그 반전이 풀려
    // 양손이 같은 방향을 본다 — 보정은 절댓값으로만 하고 부호는 프리팹 값을 그대로 따른다. 좌우 반전은
    // SpriteRenderer.flipX가 아니라 이 localScale 부호가 담당한다.
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

    // 공격1: 손 하나가 대상 좌표 위로 들렸다가 내려찍는다. 좌우 손을 번갈아 사용 — 연속 시전 시 같은
    // 손이 복귀하기 전에 다시 뽑혀 안 보이는 문제 방지.
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
