using UnityEngine;
using UnityEngine.InputSystem;

// 입력 스킴 중앙화 — WASD/휠/스페이스/1~4/좌클릭/우클릭/Ctrl/ESC 등 여러 컨트롤러가 각자 device를
// 폴링하던 걸 이 클래스로 모아 이 파일만 보면 전체 입력 표면을 알 수 있게 한다. InputAction/
// InputActionAsset 대신 매 프레임 device를 직접 읽는 정적 프로퍼티로 구성해 MonoBehaviour 생명주기 의존을 없앴다.
public static class GameInputScheme
{
    private static Keyboard Kb => Keyboard.current;
    private static Mouse    Ms => Mouse.current;

    public static bool IsReady => Kb != null && Ms != null;
    // GUIMouseUtil처럼 키보드 유무와 무관하게 "포인터 좌표를 읽을 수 있는가"만 필요한 순수 UI
    // 히트테스트 호출부용 — IsReady(둘 다 필요)와 분리해 마우스만 있어도 정상 동작한다.
    public static bool PointerAvailable => Ms != null;

    // ── 카메라 이동(WASD) ──
    public static bool MoveUp    => Kb != null && Kb.wKey.isPressed;
    public static bool MoveDown  => Kb != null && Kb.sKey.isPressed;
    public static bool MoveLeft  => Kb != null && Kb.aKey.isPressed;
    public static bool MoveRight => Kb != null && Kb.dKey.isPressed;

    // ── 줌(마우스 휠) — CameraController만 실제로 카메라에 적용한다(두 곳에서 동시에 소비하면
    // DebugInfoPanel의 중복 줌 적용 버그가 재현된다).
    public static float ZoomDelta => Ms != null ? Ms.scroll.ReadValue().y : 0f;

    // ── 일시정지(스페이스바) ──
    public static bool PausePressedThisFrame => Kb != null && Kb.spaceKey.wasPressedThisFrame;

    // ── 설정 패널(ESC) ──
    public static bool EscapePressedThisFrame => Kb != null && Kb.escapeKey.wasPressedThisFrame;

    // ── 게임 속도(1~4 → 0.5x/1.0x/1.5x/2.0x) ──
    public static bool Speed05PressedThisFrame => Kb != null && Kb.digit1Key.wasPressedThisFrame;
    public static bool Speed10PressedThisFrame => Kb != null && Kb.digit2Key.wasPressedThisFrame;
    public static bool Speed15PressedThisFrame => Kb != null && Kb.digit3Key.wasPressedThisFrame;
    public static bool Speed20PressedThisFrame => Kb != null && Kb.digit4Key.wasPressedThisFrame;

    // ── 선택 추가 모디파이어(Ctrl, 좌/우 구분 없음) ──
    public static bool SelectAddHeld => Kb != null && (Kb.leftCtrlKey.isPressed || Kb.rightCtrlKey.isPressed);

    // ── 좌클릭 ──
    public static bool PrimaryDown => Ms != null && Ms.leftButton.wasPressedThisFrame;
    public static bool PrimaryHeld => Ms != null && Ms.leftButton.isPressed;
    public static bool PrimaryUp   => Ms != null && Ms.leftButton.wasReleasedThisFrame;

    // ── 우클릭 ──
    public static bool SecondaryDown => Ms != null && Ms.rightButton.wasPressedThisFrame;

    public static Vector2 PointerScreenPos => Ms != null ? Ms.position.ReadValue() : Vector2.zero;
}
