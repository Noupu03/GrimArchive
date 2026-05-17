using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// UnitGenerate가 유닛 생성 시 자동으로 부착. Init(typeName) 호출로 초기화.
/// Playables API를 사용해 Animator Controller 없이 클립을 직접 재생.
/// transform.position 델타로 이동을 감지해 Idle/Walk 상태를 전환.
/// </summary>
[RequireComponent(typeof(Animator))]
public class UnitAnimationController : MonoBehaviour
{
    // 이동으로 판정하는 최소 프레임당 이동량 (월드 단위)
    private const float MOVE_THRESHOLD  = 0.001f;
    // Walk → Idle 전환 전 정지 상태를 유지해야 하는 프레임 수 (flickering 방지)
    private const int   IDLE_DEBOUNCE   = 4;

    private enum AnimState { Uninitialized, Idle, Walk }

    private Animator          animator;
    private SpriteRenderer    sr;
    private PlayableGraph     graph;
    private AnimationMixerPlayable mixer;

    private AnimState state          = AnimState.Uninitialized;
    private Vector3   lastPos;
    private int       stationaryFrames;

    // ─────────────────────────────────────────
    //  진입점 — UnitGenerate에서 호출
    // ─────────────────────────────────────────
    public void Init(string unitTypeName)
    {
        animator = GetComponent<Animator>();
        sr       = GetComponent<SpriteRenderer>();
        lastPos  = transform.position;

        // Root motion이 transform을 건드리지 않도록
        animator.applyRootMotion = false;

        if (!UnitSpriteManager.Instance.TryGetAnimationClips(unitTypeName, out var idle, out var walk))
        {
            Debug.LogWarning($"[UnitAnimationController] '{unitTypeName}' 에 대한 클립이 UnitSpriteManager에 없습니다.");
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

        var idlePlayable = AnimationClipPlayable.Create(graph, idle);
        var walkPlayable = AnimationClipPlayable.Create(graph, walk);

        graph.Connect(idlePlayable, 0, mixer, 0);
        graph.Connect(walkPlayable, 0, mixer, 1);

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

        Vector3 delta   = transform.position - lastPos;
        bool    moving  = delta.sqrMagnitude > MOVE_THRESHOLD * MOVE_THRESHOLD;

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

        lastPos = transform.position;
    }

    private void TransitionTo(AnimState next)
    {
        state = next;
        SetWeights(next);
    }

    private void SetWeights(AnimState s)
    {
        mixer.SetInputWeight(0, s == AnimState.Idle ? 1f : 0f);  // 0번 = Idle
        mixer.SetInputWeight(1, s == AnimState.Walk ? 1f : 0f);  // 1번 = Walk
    }

    // ─────────────────────────────────────────
    //  정리
    // ─────────────────────────────────────────
    private void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
    }
}
