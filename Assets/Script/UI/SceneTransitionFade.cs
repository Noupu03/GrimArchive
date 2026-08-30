using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title -> ssh 전환용 — Additive 로드만으로는 ssh 씬 초기화(맵 생성 등) 중 화면이 잠깐 끊겨 보이므로,
// 로딩 시작과 동시에 애니메이션 없이 검은 오버레이로 즉시 덮고 전환이 완전히 끝난 뒤에만 페이드인한다.
// 씬에 미리 배치할 필요 없이 코드에서 자기 자신을 만들고 DontDestroyOnLoad로 전환 내내 살아남는다.
public class SceneTransitionFade : MonoBehaviour
{
    private static SceneTransitionFade _instance;

    private CanvasGroup _canvasGroup;

    private const float RevealSeconds = 0.4f;
    // 씬 로드가 isDone된 시점과 실제로 그릴 게 준비된 시점 사이에 간극이 있어, 곧바로 페이드인하면
    // 빈 화면이 잠깐 보일 수 있다(몇 프레임으론 부족해 시간 기반으로 여유를 줌).
    private const float PostLoadSettleSeconds = 2f;

    public static SceneTransitionFade EnsureInstance()
    {
        if (_instance != null) return _instance;

        var go = new GameObject("SceneTransitionFade");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<SceneTransitionFade>();
        _instance.BuildOverlay();
        return _instance;
    }

    private void BuildOverlay()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000; // 전환 중엔 항상 최상단에 그려져야 한다

        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        var imageGo = new GameObject("FadeImage");
        imageGo.transform.SetParent(transform, false);
        var image = imageGo.AddComponent<Image>();
        image.color = Color.black;

        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
    }

    public async UniTask LoadSceneWithCoverAsync(string sceneToLoad, string sceneToUnload)
    {
        // 애니메이션 없이 즉시 덮는다 — 이 시점부터는 오버레이 뒤라 이후 씬 스왑/초기화 중 끊김이 나도 안 보인다.
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;

        var loadOp = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Additive);
        await loadOp;

        SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneToLoad));

        await SceneManager.UnloadSceneAsync(sceneToUnload);

        await UniTask.Delay(System.TimeSpan.FromSeconds(PostLoadSettleSeconds), ignoreTimeScale: true);

        await FadeAsync(1f, 0f, RevealSeconds);
        _canvasGroup.blocksRaycasts = false;
    }

    private async UniTask FadeAsync(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            await UniTask.Yield();
        }
        _canvasGroup.alpha = to;
    }
}
