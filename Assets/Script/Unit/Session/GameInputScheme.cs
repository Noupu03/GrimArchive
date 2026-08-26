using UnityEngine;
using UnityEngine.InputSystem;

// 입력 스킴 중앙화(2026-08-21, 사용자 요청 "wasd, 마우스 휠, 스페이스바, 마우스 좌클릭 우클릭, 0123
// 속도조절만 남기고 전부 없애줘" 정리 이후 "구조적 제안도 너가 개선해봐") — 이 게임이 실제로 쓰는
// 입력은 정확히 이것뿐이다: WASD(카메라 이동) / 마우스 휠(줌) / 스페이스바(일시정지) / 1~4(게임 속도
// 0.5x/1.0x/1.5x/2.0x, 2026-08-23 사용자 요청으로 0~3에서 재배정) / 좌클릭 / 우클릭 / Ctrl(선택
// 추가 모디파이어) / ESC(2026-08-26 추가, 설정 패널 토글). 예전엔 이 입력들을 InputManager/CameraController/
// BuildPlacementController/ObjectPlacementController/MonsterPlacementController 5곳이 각자
// Keyboard.current.xKey / Mouse.current.yButton을 직접 폴링해서 흩어져 있었다 — "지금 이 게임이 정확히
// 어떤 입력을 쓰는지" 감사하려면 5개 파일을 다 grep해야 했다(이번 정리 작업 자체가 그 비용을 보여줌).
// 이 클래스 하나로 모아서 이 파일만 보면 전체 입력 표면을 알 수 있게 한다.
//
// Input System의 InputAction/InputActionAsset(콜백 기반, Enable/Disable 생명주기 필요) 대신 매 프레임
// 직접 device를 읽는 정적 프로퍼티로 구성했다 — 동작 방식은 기존과 완전히 동일(그냥 한 곳으로 모음)
// 하면서도, 여러 MonoBehaviour의 초기화/파괴 순서에 의존하는 Enable/Disable/Dispose 관리가 필요 없어
// 에디터에서 직접 검증할 수 없는 이 세션에서는 이 쪽이 회귀 위험이 훨씬 낮다. 나중에 실제 키 리바인딩
// (사용자 커스터마이징) 기능이 필요해지면, 이 클래스 내부 구현만 InputAction 기반으로 바꾸면 되고
// 호출부(아래 각 프로퍼티를 참조하는 코드)는 전혀 손댈 필요가 없다.
public static class GameInputScheme
{
    private static Keyboard Kb => Keyboard.current;
    private static Mouse    Ms => Mouse.current;

    public static bool IsReady => Kb != null && Ms != null;
    // GUIMouseUtil처럼 키보드 유무와 무관하게 "포인터 좌표를 읽을 수 있는가"만 필요한 순수 UI
    // 히트테스트 호출부용 — IsReady(둘 다 필요)와 분리해 마우스만 있어도 정상 동작하게 한다.
    public static bool PointerAvailable => Ms != null;

    // ── 카메라 이동(WASD) ──
    public static bool MoveUp    => Kb != null && Kb.wKey.isPressed;
    public static bool MoveDown  => Kb != null && Kb.sKey.isPressed;
    public static bool MoveLeft  => Kb != null && Kb.aKey.isPressed;
    public static bool MoveRight => Kb != null && Kb.dKey.isPressed;

    // ── 줌(마우스 휠) — CameraController만 실제로 카메라에 적용한다(2026-08-21, DebugInfoPanel의
    // 중복 줌 적용 버그 제거 참고: 이 값을 두 곳에서 동시에 소비하면 다시 같은 버그가 재현된다).
    public static float ZoomDelta => Ms != null ? Ms.scroll.ReadValue().y : 0f;

    // ── 일시정지(스페이스바) ──
    public static bool PausePressedThisFrame => Kb != null && Kb.spaceKey.wasPressedThisFrame;

    // ── 설정 패널(ESC, 2026-08-26 사용자 요청 "esc를 누르면... 옵션 panel이 뜨게 해줘" → 이후
    // "옵션이라는 말, 설정으로 통일해") ──
    public static bool EscapePressedThisFrame => Kb != null && Kb.escapeKey.wasPressedThisFrame;

    // ── 게임 속도(1~4 → 0.5x/1.0x/1.5x/2.0x, 2026-08-23 사용자 요청으로 0~3에서 재배정) ──
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
