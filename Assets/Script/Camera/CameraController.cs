using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public float panSpeed = 35f;
    public float zoomSpeed = 0.05f; // Input system의 스크롤 값은 크므로 조절
    public float minZoom = 5f;
    public float maxZoom = 50f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        // 1. 카메라 조작 자동 부착
        if (Camera.main != null && Camera.main.gameObject.GetComponent<CameraController>() == null)
        {
            Camera.main.gameObject.AddComponent<CameraController>();
            Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, "CameraController 자동 부착 완료.");
        }

        // 2. EventSystem InputModule 크래시 방지 (새로운 Input System 적용)
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem != null)
        {
            var standalone = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (standalone != null)
            {
                Destroy(standalone);
                eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, "EventSystem을 InputSystemUIInputModule로 자동 교체 완료.");
            }
        }
    }

    void Update()
    {
        Vector3 pos = transform.position;

        if (Keyboard.current != null)
        {
            float move = panSpeed * Time.unscaledDeltaTime;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) pos.y += move;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) pos.y -= move;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) pos.x += move;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) pos.x -= move;
        }

        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (scroll != 0.0f)
            {
                Camera.main.orthographicSize -= scroll * zoomSpeed;
                Camera.main.orthographicSize = Mathf.Clamp(Camera.main.orthographicSize, minZoom, maxZoom);
            }
        }

        transform.position = pos;
    }
}
