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

    // 웨이브 시작 알림을 언제 띄울지(2026-07-23 사용자 요청 "잠시 후, 웨이브가 시작됩니다").
    // 2026-08-20 수정, 사용자 요청 "문구 및 관련 표시들 등장하는 시점을 0층 몬스터 소환 시점으로
    // 바꿔줘" — 예전엔 남은 시간이 고정값(3초) 이하로 떨어지는 순간이었는데, 이제 실제로 플레이어
    // 몬스터들이 배치 위치로 소집되는 순간(HumanWaveManager.IsMonstersSummonedThisCycle이 켜지는
    // 순간)에 맞춘다 — 몬스터 소집이 화면에 보이기 시작하는 시점과 알림이 항상 같이 뜬다. 지속시간도
    // 고정값 대신 그 시점에 남은 실제 웨이브 시작까지의 시간(cooldownTimer)만큼 표시해 웨이브 시작과
    // 거의 동시에 자연스럽게 사라지게 한다.
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

        if (wm.currentState == WaveState.Idle && wm.IsMonstersSummonedThisCycle && !_waveStartNoticeShown)
        {
            _waveStartNoticeShown = true;
            float duration = Mathf.Max(0.1f, wm.cooldownTimer); // 소집 시점부터 실제 웨이브 시작까지 남은 시간만큼 표시
            NoticeCenter.Instance?.Push("잠시 후, 웨이브가 시작됩니다.", NoticeCenter.WarningColor, duration);
        }
        else if (wm.currentState != WaveState.Idle)
        {
            // 웨이브가 실제로 시작되면(다음 대기 사이클에서 다시 엣지 트리거될 수 있도록) 리셋.
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
