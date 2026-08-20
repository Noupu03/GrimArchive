using UnityEngine;
using System.Collections.Generic;
using VContainer;
using DG.Tweening;
using GrimArchive.Wave;

// 부팅 시 DebugInfoPanel/도감 패널을 띄우던 역할은 GameUIPresenter(Haare UIPresenter/RegisterEntryPoint
// 경로)로 옮겨졌다 — 이 클래스는 이제 OnGUI() 오버레이(게임 속도/일시정지 표시)만 담당한다.
public class UIManager : MonoBehaviour
{
    private GameSession _gameSession;

    [Inject]
    public void Construct(GameSession gameSession)
    {
        _gameSession = gameSession;
    }

    // 웨이브 시작 몇 초 전부터 알림을 띄울지(고정값, 2026-07-23 사용자 요청 "잠시 후, 웨이브가
    // 시작됩니다"). HumanWaveManager.cooldownTimer가 이 값 이하로 떨어지는 순간(엣지 트리거) 딱 한
    // 번 NoticeCenter에 Push한다(2026-08-19 수정, 사용자 요청 "이 문구들을 시스템화" — 매 프레임
    // 조건부로 직접 그리던 방식에서, 알림 시스템(NoticeCenter)에 한 번 알리는 방식으로 교체).
    private const float WaveStartWarningSeconds = 3f;
    private bool _waveStartNoticeShown;

    void OnGUI()
    {
        if (_gameSession == null) return;

        DrawTopLeftUI();
        CheckWaveStartNotice();
	}

    private void CheckWaveStartNotice()
    {
        HumanWaveManager wm = HumanWaveManager.Instance;
        if (wm == null) return;

        bool inWarningWindow = wm.currentState == WaveState.Idle
            && wm.cooldownTimer <= WaveStartWarningSeconds
            && wm.cooldownTimer > 0f;

        if (inWarningWindow && !_waveStartNoticeShown)
        {
            _waveStartNoticeShown = true;
            NoticeCenter.Instance?.Push("잠시 후, 웨이브가 시작됩니다.", NoticeCenter.WarningColor, WaveStartWarningSeconds);
        }
        else if (!inWarningWindow && wm.currentState != WaveState.Idle)
        {
            // 다음 웨이브 대기 사이클로 넘어가면 다시 엣지 트리거될 수 있도록 리셋.
            _waveStartNoticeShown = false;
        }
    }

	private void DrawTopLeftUI()
    {
        int y = 50; // 좌상단 FPS 카운터(CoreCanvas의 FPSText, y 10~40)와 안 겹치게 그 아래부터 시작.
		GUI.Label(new Rect(10, y, 300, 40), $"게임 속도: {_gameSession.currentGameSpeed}x {(_gameSession.isPaused ? "<color=red>[일시정지]</color>" : "")}\nSpace: 일시정지 | 0/1/2/3: 배속(0.5x/1x/2x/3x)");
        y += 50;
    }

	// 2026-08-20 — ShowFloatingText(Unit)는 위치/기본색/기본시간만 유닛 기준으로 채워 ShowFloatingTextAt로
	// 위임한다(둘이 거의 동일한 바디를 중복 구현하고 있었음).
	public void ShowFloatingText(Unit unit, string message)
	{
		if (unit == null) return;

		Vector3 pos = GetWorldTextPosition(unit);
		pos.y -= 16.7f;
		Color color = (unit is Human) ? Color.green : Color.red;

		ShowFloatingTextAt(pos, message, color);
	}

	// 03문서 9-8장(2026-07-27 추가) — 함정 해제 성공/실패 결과 문구용. ShowFloatingText(Unit)와 달리
	// 유닛이 아니라 임의의 월드 좌표(함정 위치 등)에 띄워야 해서 별도 오버로드로 뒀다.
	public void ShowFloatingTextAt(Vector3 worldPos, string message, Color color, float duration = 0.5f)
	{
		GameObject go = new GameObject("FloatingText");
		go.transform.position = worldPos;

		TextMesh text = go.AddComponent<TextMesh>();
		text.text = message;

		text.fontSize = 90;
		text.characterSize = 0.05f;

		text.anchor = TextAnchor.MiddleCenter;
		text.alignment = TextAlignment.Center;
		text.color = color;

		MeshRenderer mr = go.GetComponent<MeshRenderer>();
		mr.sortingOrder = 9999; // ★ 핵심 (맵 위로 올림)

		DOVirtual.DelayedCall(duration, () => { if (go != null) Destroy(go); });
	}

	Vector3 GetWorldTextPosition(Unit unit)
	{
		return new Vector3(
			unit.position.x + unit.unitType.footprint.x * 0.5f,
			unit.position.y + unit.unitType.footprint.y + 1f,
			0
		);
	}
}
