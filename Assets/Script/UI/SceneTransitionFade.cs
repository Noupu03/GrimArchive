using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title -> ssh 전환. Additive 로드만으로는 그 뒤 ssh 씬 자체의 초기화(맵 생성 등)가 진행되는 동안
// 화면이 잠깐 끊겨 보이는 문제가 있어, 그 구간 전체를 의도적인 검은 오버레이로 덮는 방식으로 처리한다.
// 로딩 시작과 동시에 애니메이션 없이 즉시 화면을 덮어서 끊기는 순간 자체가 안 보이게 하고, 전환이
// 완전히 끝난 뒤에만 부드럽게 페이드인해서 자연스럽게 드러낸다. 씬에 미리 배치할 필요 없이 코드에서
// 자기 자신을 만들고 DontDestroyOnLoad로 전환 내내 살아남는다.
public class SceneTransitionFade : MonoBehaviour
{
    private static SceneTransitionFade _instance;

    private CanvasGroup _canvasGroup;

    private const float RevealSeconds = 0.4f;
    // ssh 씬의 Awake/Start/Initialize(맵 생성 등)가 충분히 끝날 때까지 여유를 준 뒤에 페이드인을
    // 시작한다 — 씬 로드가 isDone이 된 시점과 화면에 실제로 그릴 게 준비된 시점 사이에 간극이 있어서,
    // 곧바로 페이드인하면 여전히 빈 화면이 잠깐 보일 수 있다. 몇 프레임으로는 부족해 시간 기반으로 뒀다.
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
        // 애니메이션 없이 즉시 덮는다 — 이 시점부터는 화면이 이미 오버레이 뒤라, 이후 씬 스왑이나
        // 초기화 중 어떤 끊김이 나도 사용자 눈에는 보이지 않는다.
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
