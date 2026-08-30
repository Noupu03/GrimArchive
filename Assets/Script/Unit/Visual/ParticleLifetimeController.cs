using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

// 파티클 프리팹에 직접 붙이는 독립 컴포넌트 — 그래픽 작업자가 프리팹 단위로 "유지 시간"을 직접
// 조정할 수 있게 한다. Unit/UnitGenerate/VFXManager 등 유닛 파이프라인을 참조하지 않는 순수 컴포넌트라
// 그쪽이 나중에 새로 설계돼도 그대로 재사용 가능(같은 원칙을 따르는 AnimationEventVfxSpawner 참고).
//
// 흐름: 재생 시작 → emissionDuration 경과 → 새 파티클 생성만 중단(Stop+StopEmitting, 이미 나온
// 파티클은 수명대로 자연 소멸) → 전부 사라지면(IsAlive==false) 비활성화. 재생 시간을 추정해 통째로
// SetActive(false)하는 방식은 진행 중이던 파티클이 화면에서 뚝 끊겨 보이는 문제가 있어 피한다.
[RequireComponent(typeof(ParticleSystem))]
public class ParticleLifetimeController : MonoBehaviour
{
    [Tooltip("새 파티클 생성을 멈추기까지의 시간(초) — 그래픽 작업자가 프리팹마다 직접 설정한다. " +
             "0 이하면 이 파티클 시스템의 재생 길이(Main.Duration)를 그대로 쓴다. 루핑 파티클이면 " +
             "반드시 0보다 큰 값을 지정해야 한다 — 안 그러면 스스로 멈추지 않는다.")]
    [SerializeField] private float emissionDuration = -1f;

    private ParticleSystem _ps;
    private CancellationTokenSource _cts;

    private void Awake()
    {
        _ps = GetComponent<ParticleSystem>();
    }

    private void OnDisable()
    {
        _cts?.Cancel();
        _cts = null;
    }

    // 파티클 재생 직후(Play() 호출 다음) 스폰한 쪽이 호출한다. onFinished를 주지 않으면 파티클이
    // 완전히 사라진 뒤 스스로 SetActive(false)한다(독립 실행 시 기본 동작). 준다면 그 대신 호출자가
    // 뒷정리(예: 오브젝트 풀 반납)를 책임진다 — VFXManager가 풀링된 인스턴스에 이 방식으로 연결한다.
    public void BeginLifecycle(System.Action onFinished = null)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        RunLifecycle(onFinished, _cts.Token).Forget();
    }

    private async UniTaskVoid RunLifecycle(System.Action onFinished, CancellationToken token)
    {
        float wait = emissionDuration > 0f ? emissionDuration : _ps.main.duration;
        bool canceled = await UniTask.Delay(System.TimeSpan.FromSeconds(wait), cancelImmediately: true, cancellationToken: token)
            .SuppressCancellationThrow();
        if (canceled || _ps == null) return;

        // 생성 중단 — 이미 나온 파티클은 각자 남은 수명만큼 계속 존재하다 자연 소멸한다.
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        while (_ps != null && _ps.IsAlive(true))
        {
            canceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            if (canceled) return;
        }

        if (onFinished != null) onFinished.Invoke();
        else if (this != null) gameObject.SetActive(false);
    }
}
