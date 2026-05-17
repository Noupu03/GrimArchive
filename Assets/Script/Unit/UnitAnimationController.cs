using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Visual 자식 오브젝트에 부착됨. UnitGenerate.AttachAnimationController()가 Init()을 호출.
///
/// 이동 감지: 자신의 transform이 아닌 부모(루트)의 world position 델타를 감시.
/// → 애니메이션 클립이 자식(Visual)의 localPosition을 변경해도 이동 감지에 영향 없음.
/// </summary>
[RequireComponent(typeof(Animator))]
public class UnitAnimationController : MonoBehaviour
{
    private const float MOVE_THRESHOLD = 0.001f;
    private const int   IDLE_DEBOUNCE  = 4;        // 정지 판정 전 대기 프레임

    private enum AnimState { Uninitialized, Idle, Walk }

    private Animator               animator;
    private SpriteRenderer         sr;
    private PlayableGraph          graph;
    private AnimationMixerPlayable mixer;
    private Transform              movementRoot;   // 루트 오브젝트 (SmoothMove 대상)

    private AnimState state          = AnimState.Uninitialized;
    private Vector3   lastRootPos;
    private int       stationaryFrames;

    // ─────────────────────────────────────────
    //  진입점
    // ─────────────────────────────────────────
    public void Init(string unitTypeName)
    {
        animator = GetComponent<Animator>();
        sr       = GetComponent<SpriteRenderer>();

        // 루트(부모) position으로 이동 감지 — 애니메이션이 자식 localPos를 건드려도 무관
        movementRoot = transform.parent != null ? transform.parent : transform;
        lastRootPos  = movementRoot.position;

        animator.applyRootMotion = false;

        if (UnitSpriteManager.Instance == null ||
            !UnitSpriteManager.Instance.TryGetAnimationClips(unitTypeName, out var idle, out var walk))
        {
            Debug.LogWarning($"[UnitAnimationController] '{unitTypeName}' 클립을 UnitSpriteManager에서 찾을 수 없습니다.");
            return;
        }

        BuildGraph(idle, walk);
    }

    // ─────────────────────────────────────────
    //  Playable 그래프 구성
    // ─────────────────────────────────────────
    private void BuildGraph(AnimationClip idle, AnimationClip walk)
    {
        if (graph.IsValid()) graph.Destroy();

        graph = PlayableGraph.Create($"UnitAnim_{gameObject.name}");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        var output = AnimationPlayableOutput.Create(graph, "Anim", animator);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        output.SetSourcePlayable(mixer);

        graph.Connect(AnimationClipPlayable.Create(graph, idle), 0, mixer, 0);
        graph.Connect(AnimationClipPlayable.Create(graph, walk), 0, mixer, 1);

        SetWeights(AnimState.Idle);
        graph.Play();
        state = AnimState.Idle;
    }

    // ─────────────────────────────────────────
    //  매 프레임 상태 전환
    // ─────────────────────────────────────────
    private void Update()
    {
        if (state == AnimState.Uninitialized) return;

        // 루트 위치 델타로 이동 판정
        Vector3 delta  = movementRoot.position - lastRootPos;
        bool    moving = delta.sqrMagnitude > MOVE_THRESHOLD * MOVE_THRESHOLD;

        if (moving)
        {
            stationaryFrames = 0;
            if (state != AnimState.Walk)
                TransitionTo(AnimState.Walk);

            // 수평 이동 방향에 따라 스프라이트 반전
            if (sr != null && Mathf.Abs(delta.x) > 0.0001f)
                sr.flipX = delta.x < 0f;
        }
        else
        {
            stationaryFrames++;
            if (stationaryFrames >= IDLE_DEBOUNCE && state != AnimState.Idle)
                TransitionTo(AnimState.Idle);
        }

        lastRootPos = movementRoot.position;
    }

    private void TransitionTo(AnimState next)
    {
        state = next;
        SetWeights(next);
    }

    private void SetWeights(AnimState s)
    {
        mixer.SetInputWeight(0, s == AnimState.Idle ? 1f : 0f);
        mixer.SetInputWeight(1, s == AnimState.Walk ? 1f : 0f);
    }

    private void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
    }
}
