using UnityEngine;
using System.Collections.Generic;
using VContainer;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using Haare.Client.UI;

public class UIManager : MonoBehaviour
{
    private CoreUIManager _coreUIManager;
    private IObjectResolver _resolver;

    [Inject]
    public void Construct(CoreUIManager coreUIManager, IObjectResolver resolver)
    {
        _coreUIManager = coreUIManager;
        _resolver = resolver;
    }

    void Start()
    {
        // DebugInfoPanel(CustomText/CustomButton/CustomSlider)을 CoreCanvas 위에 띄운다.
        // 줌 버튼/선택 유닛 정보는 이 패널로 이전되어 아래 OnGUI()에선 더 이상 그리지 않는다.
        LoadDebugPanelAsync().Forget();
    }

    private async UniTaskVoid LoadDebugPanelAsync()
    {
        await _coreUIManager.LoadPanel<DebugInfoPanel>(_resolver);
    }

    void OnGUI()
    {
        if (GameSession.Instance == null) return;

        DrawTopLeftUI();
        DrawUnitLabels();
		//DrawPartyStatus();=======파티 관련 참조 주석처리========
	}

	private void DrawTopLeftUI()
    {
        int y = 10;
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
		GUI.Label(new Rect(10, y, 250, 40), $"게임 속도: {GameSession.Instance.currentGameSpeed}x {(GameSession.Instance.isPaused ? "<color=red>[일시정지]</color>" : "")}\n단축키: 0, 1, 2, 3 / Space");
        y += 50;
    }

    private void DrawUnitLabels()
    {
        if (Camera.main == null) return;

		//======= 파티 관련 참조 주석처리 ========
        /*// 4. 유닛 머리 위 디버그 텍스트 (카메라 프로젝션 적용, 카메라를 따라 같이 이동함)
        foreach (var u in GameSession.Instance.units)
        {
            if (u == null || u.hp <= 0) continue;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(new Vector3(u.position.x + u.unitType.footprint.x / 2f, u.position.y + u.unitType.footprint.y + 0.5f, 0));
            if (screenPos.z > 0)
            {
                string actionName = "Idle";
                if (u.brain != null)
                {
                    var field = typeof(GoapBrain).GetField("currentPlannedAction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        var action = field.GetValue(u.brain) as GoapAction;
                        if (action != null) actionName = action.ActionName;
                    }
                }

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
                if (u == null || u.hp <= 0) continue;
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
