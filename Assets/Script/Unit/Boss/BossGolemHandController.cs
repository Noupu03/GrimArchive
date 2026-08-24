using UnityEngine;
using DG.Tweening;

// 보스 골렘 전용 비주얼 컴포넌트(2026-08-24, 기본 구조). 왼손/오른손은 Unit이 아니라 순수 Transform —
// 실제 피해 판정은 SkillAction_Golem* 3종이 기존 Hitbox 시스템으로 처리하고, 여기서는 그 판정과
// "대략 맞춰" 손을 움직이는 연출만 담당한다(사용자 확정: "재활용성 없는 보스 전용 기능이라 기존
// 스크립트 구조에서 벗어나도 됨").
//
// 손 애니메이션과 피해 판정 타이밍은 지금은 독립적으로 굴러간다 — SkillAction_Golem*이 시전(castMs)
// 시작 시점에 이 컨트롤러를 곧바로 호출해 손을 움직이기 시작하고, 실제 피해는 기존 BeginAttackCast의
// castMs 경과 시점에 별도로 계산된다. slamTravelDuration 등을 baseDelayMs와 맞춰두면 대략 일치하지만
// 프레임 단위로 보장되진 않는다 — 실제 연출 검증 후 더 타이트하게 동기화할지 결정할 것(다음 우선순위).
//
// 보스 프리팹의 Visual 계층 아래에 이 컴포넌트를 붙이고 leftHand/rightHand를 인스펙터에서 연결한다.
//
// 크기 기준(2026-08-24 사용자 확정): 본체(풋프린트)는 기본 몬스터의 3배 — units.json의 "보스 골렘"
// 항목이 footprint [3,3]으로 등록돼 있다. [2026-08-24 후속 수정] 원래는 UnitGenerate.SetupUnitVisual이
// 루트 오브젝트의 localScale을 footprint 배율만큼 그대로 키우는 방식이었는데, 본체 스프라이트 원본
// 아트 자체가 이미 3배 크기로 그려져 있어서 (footprint 3배) × (이미 3배인 이미지) = 9배로 렌더링되는
// 버그가 있었다(사용자 신고 "이미지 자체가 3배 스케일링, 3배해서 9배가 되어버림") — 이제
// UnitVisualDefinition.visualScaleIgnoresFootprint를 이 프리팹에 켜서, footprint는 인구수/충돌 등
// 게임플레이 판정에만 쓰이고 루트 localScale은 1배 그대로 유지된다(BossGolemVisualScaleFix.cs 참고).
// 손 일러스트는 일반 유닛 스프라이트와 같은 크기인데, 이 변경 이후로는 부모(본체) 자체가 이미 1배라
// 아래 ApplyNormalSpriteScale의 역수 계산이 사실상 항상 1을 곱하는 것과 같아진다 — 그래도 "부모
// lossyScale의 역수"라는 계산 자체는 그대로 두는 게 안전하다(나중에 본체 스케일 기준이 다시 바뀌어도
// 손은 자동으로 정상 크기를 유지).
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

    // 부모(3배로 커진 본체) 스케일의 역수를 곱해 손을 월드 기준 1배(일반 유닛 스프라이트 크기)로
    // 되돌린다. 손이 계층 더 깊이 중첩되더라도 "바로 위 부모"의 lossyScale만 상쇄하면 되므로 이 정도로
    // 충분하다 — 부모 자체가 회전/추가 스케일까지 걸려있는 특수 케이스는 지금 대상이 아니다.
    //
    // [2026-08-24 수정] 프리팹에 저장된 localScale의 "부호"는 절대 건드리지 않는다 — GolemHand.spriteLib에는
    // 손 스프라이트가 방향별 한 벌뿐이라 반대쪽 손은 좌우 반전으로 만드는데(프리팹에서 RightHand의
    // localScale.x = -1), 예전처럼 크기 보정 결과를 양수로 덮어쓰면 에디터에서는 뒤집혀 보이던 오른손이
    // 플레이 시작(Awake) 순간 원래 방향으로 되돌아가 양손이 같은 방향을 봤다(사용자 신고 "골렘의
    // RightHand가 게임 작동할 때 flip이 안됨"). 크기 보정은 절댓값으로만 하고 부호는 프리팹 값을 그대로
    // 따른다 — 손의 좌우 반전은 SpriteRenderer.flipX가 아니라 이 localScale 부호가 담당한다(flipX는
    // 나중에 본체처럼 방향별 스프라이트를 손에도 적용하게 될 때를 위해 비워둔다).
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
