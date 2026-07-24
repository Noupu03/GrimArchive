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

    // 웨이브 시작 몇 초 전부터 화면 중앙 경고 문구를 띄울지(고정값, 2026-07-23 사용자 요청 "잠시 후,
    // 웨이브가 시작됩니다"). HumanWaveManager.cooldownTimer가 이 값 이하로 떨어지면 표시되다가
    // 웨이브가 실제로 시작되는 순간(currentState != Idle) 사라진다.
    private const float WaveStartWarningSeconds = 3f;

    void OnGUI()
    {
        if (_gameSession == null) return;

        DrawTopLeftUI();
        DrawUnitLabels();
        DrawWaveStartBanner();
		//DrawPartyStatus();=======파티 관련 참조 주석처리========
	}

    private void DrawWaveStartBanner()
    {
        HumanWaveManager wm = HumanWaveManager.Instance;
        if (wm == null) return;
        if (wm.currentState != WaveState.Idle) return;
        if (wm.cooldownTimer > WaveStartWarningSeconds || wm.cooldownTimer <= 0f) return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            richText = true
        };
        style.normal.textColor = Color.yellow;

        float w = 700f, h = 60f;
        Rect rect = new Rect(Screen.width / 2f - w / 2f, Screen.height * 0.2f, w, h);

        // 검은 배경을 살짝 깔아 어떤 배경 위에서도 글자가 잘 보이게 한다.
        Color prevColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prevColor;

        GUI.Label(rect, "잠시 후, 웨이브가 시작됩니다.", style);
    }

	private void DrawTopLeftUI()
    {
        int y = 50; // 좌상단 FPS 카운터(CoreCanvas의 FPSText, y 10~40)와 안 겹치게 그 아래부터 시작.
		/*=======아티팩트 관련 참조 주석처리========
        // 1. 유물 생성 모드 토글
        if (ArtifactManager.Instance != null)
        {
            if (GUI.Button(new Rect(10, y, 150, 30), $"유물 생성 모드: {(ArtifactManager.Instance.artifactPlacementMode ? "ON" : "OFF")}"))
            {
                ArtifactManager.Instance.artifactPlacementMode = !ArtifactManager.Instance.artifactPlacementMode;
            }
            y += 40;
        }*/

		// 2. 게임 속도 및 일시정지 상태
		GUI.Label(new Rect(10, y, 300, 40), $"게임 속도: {_gameSession.currentGameSpeed}x {(_gameSession.isPaused ? "<color=red>[일시정지]</color>" : "")}\nSpace: 일시정지 | 0/1/2/3: 배속(0.5x/1x/2x/3x)");
        y += 50;
    }

    private void DrawUnitLabels()
    {
        if (Camera.main == null) return;

		//======= 파티 관련 참조 주석처리 ========
        /*// 4. 유닛 머리 위 디버그 텍스트 (카메라 프로젝션 적용, 카메라를 따라 같이 이동함)
        foreach (var u in _gameSession.units)
        {
            if (u == null || u.Health.hp <= 0) continue;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(new Vector3(u.position.x + u.unitType.footprint.x / 2f, u.position.y + u.unitType.footprint.y + 0.5f, 0));
            if (screenPos.z > 0)
            {
                string actionName = u.fsm != null ? u.fsm.GetLabel(u) : "Idle";

                string debugText = $"{u.unitType.typeName}\n{actionName}";
                if (InputManager.Instance != null && InputManager.Instance.selectedUnit == u)
                    debugText = "<color=yellow>[선택됨]</color>\n" + debugText;

                GUI.contentColor = Color.white;
                GUIStyle centeredStyle = GUI.skin.GetStyle("Label");
                centeredStyle.alignment = TextAnchor.UpperCenter;
                centeredStyle.fontSize = 12;
                centeredStyle.richText = true;

                // Unity의 화면 좌표계는 좌하단이 (0,0)이므로 GUI 좌상단 (0,0)에 맞게 보정
                GUI.Label(new Rect(screenPos.x - 50, Screen.height - screenPos.y +200, 100, 40), debugText, centeredStyle);
            }
        }*/
	}

	/*=======파티 관련 참조 주석처리========
    private void DrawPartyStatus()
    {
        int y = 150; // 시작 y 위치를 더 내림 (위쪽 UI와 겹치지 않게)

        if (PartyController.Instance == null || PartyController.Instance.activeParties.Count == 0) return;

        foreach (var party in PartyController.Instance.activeParties)
        {
            if (party.members.Count == 0) continue;

            int panicCount = 0;
            float totalMental = 0f;
            int humanCount = 0;
            string panicNames = "";

            foreach (var u in party.members)
            {
                if (u == null || u.Health.hp <= 0) continue;
                humanCount++;
                totalMental += u.currentMental;
                if (u.currentMental < u.baseMental * 0.3f)
                {
                    panicCount++;
                    if (panicNames != "") panicNames += ", ";
                    panicNames += u.unitType.typeName;
                }
            }

            if (humanCount > 0)
            {
                string goalStr = party.partyGoal switch
                {
                    PartyGoal.Sweep => "소탕",
                    PartyGoal.Exploration => "탐사",
                    PartyGoal.Recovery => "회수",
                    _ => "오류"
                };

                GUI.Label(new Rect(10, y, 300, 20), $"[{party.partyName} - {goalStr}] 평균 정신력: {totalMental / humanCount:F1}");
                y += 20;

                string panicInfo = $"공황: {panicCount}명";
                if (panicCount > 0)
                {
                    panicInfo += $" ({panicNames})";
                    GUI.Label(new Rect(10, y, 400, 20), $"<color=red>{panicInfo}</color>");
                }
                else
                {
                    GUI.Label(new Rect(10, y, 400, 20), panicInfo);
                }
                y += 20;
            }

            if (party.leaderlessShockTimer > 0)
            {
                GUI.Label(new Rect(10, y, 300, 20), $"<color=red>리더리스 쇼크: {party.leaderlessShockTimer:F1}s</color>");
                y += 20;
            }
            y += 10;
        }
    }
*/
	public void ShowFloatingText(Unit unit, string message)
	{
		if (unit == null) return;

		GameObject go = new GameObject("FloatingText");

		Vector3 pos = GetWorldTextPosition(unit);
		pos.y -= 16.7f;
		go.transform.position = pos;

		TextMesh text = go.AddComponent<TextMesh>();
		text.text = message;

		text.fontSize = 90;
		text.characterSize = 0.05f;

		text.anchor = TextAnchor.MiddleCenter;
		text.alignment = TextAlignment.Center;
		text.color = (unit is Human) ? Color.green : Color.red;

		MeshRenderer mr = go.GetComponent<MeshRenderer>();
		mr.sortingOrder = 9999; // ★ 핵심 (맵 위로 올림)

		DOVirtual.DelayedCall(0.5f, () => { if (go != null) Destroy(go); });
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
