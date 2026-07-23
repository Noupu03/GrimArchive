using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title.unity -> ssh.unity 전환(TitlePresenter.StartGame, 기존엔 SceneManager.LoadScene("ssh") 동기
// 호출 한 줄)이 씬이 바뀌는 한 프레임 동안 카메라가 끊겨 화면이 검게 번쩍이는 현상을 감춘다(사용자
// 신고, 2026-07-23 "ssh 씬 로드될때 한번 검은색으로 깜빡거려서 부자연스러운데"). 씬에 미리 배치해둘
// 필요 없이 코드에서 자기 자신의 풀스크린 검은 오버레이를 만들고 DontDestroyOnLoad로 전환 내내
// 살아남는다 — 로드 전 완전히 불투명하게 페이드아웃한 뒤에만 실제 씬 전환을 실행해서, 전환 중 발생하는
// 프레임은 전부 그 검은 오버레이 뒤에 가려진다.
public class SceneTransitionFade : MonoBehaviour
{
    private static SceneTransitionFade _instance;

    private CanvasGroup _canvasGroup;

    private const float FadeOutSeconds = 0.15f;
    private const float FadeInSeconds = 0.25f;

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

    public void LoadSceneWithFade(string sceneName)
    {
        StartCoroutine(FadeAndLoadRoutine(sceneName));
    }

    private IEnumerator FadeAndLoadRoutine(string sceneName)
    {
        _canvasGroup.blocksRaycasts = true;
        yield return FadeRoutine(0f, 1f, FadeOutSeconds);

        var op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f) yield return null;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        yield return FadeRoutine(1f, 0f, FadeInSeconds);
        _canvasGroup.blocksRaycasts = false;
    }

    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        _canvasGroup.alpha = to;
    }
}
