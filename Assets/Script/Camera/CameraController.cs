using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public float panSpeed = 35f;
    public float zoomSpeed = 0.05f; // Input system의 스크롤 값은 크므로 조절
    public float minZoom = 5f;
    public float maxZoom = 50f;

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
