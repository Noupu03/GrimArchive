using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    void Awake()
    {
        Instance = this;
    }

    void OnGUI()
    {
        if (GameSession.Instance == null) return;

        DrawTopLeftUI();
        DrawTopRightUI();
        DrawSelectedUnitInfo();
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

    private void DrawTopRightUI()
    {
        // 3. 카메라 이동 및 줌 컨트롤 (우상단)
        GUILayout.BeginArea(new Rect(Screen.width - 200, 10, 190, 80));
        GUILayout.Label("카메라 이동 (W/A/S/D)\n카메라 확대 (마우스 휠)");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Zoom In", GUILayout.Height(30)))
        {
            if (Camera.main != null)
                Camera.main.orthographicSize = Mathf.Clamp(Camera.main.orthographicSize - 2f, 5f, 50f);
        }
        if (GUILayout.Button("Zoom Out", GUILayout.Height(30)))
        {
            if (Camera.main != null)
                Camera.main.orthographicSize = Mathf.Clamp(Camera.main.orthographicSize + 2f, 5f, 50f);
        }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
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

	private void DrawSelectedUnitInfo()
    {
        if (InputManager.Instance == null || InputManager.Instance.selectedUnit == null) return;

        Unit u = InputManager.Instance.selectedUnit;

        int boxW = 220;
        int boxH = 520;
        GUI.Box(new Rect(10, Screen.height - boxH - 10, boxW, boxH), "선택 유닛 정보");

        int y = Screen.height - boxH + 20;
        int x = 15;
        int lineH = 20;

        GUI.Label(new Rect(x, y, boxW - 10, lineH), $"<b>이름:</b> {u.unitType.typeName}"); y += lineH;
        GUI.Label(new Rect(x, y, boxW - 10, lineH), $"<b>진영:</b> {(u is Human ? "인류" : "몬스터")}"); y += lineH;

        /*=======파티 관련 참조 주석처리========
        string partyInfo = "없음";
        if (PartyController.Instance != null)
        {
            var p = PartyController.Instance.GetPartyOf(u);
            if (p != null) partyInfo = p.partyName;
        }
        if (u is Human)
        {
            GUI.Label(new Rect(x, y, boxW - 10, lineH), $"<b>파티:</b> {partyInfo}"); y += lineH;
        }
        */

        y += 5; // spacing
        GUI.Label(new Rect(x, y, boxW - 10, lineH), $"<b>HP:</b> {u.hp:F1}"); y += lineH;
        if (u is Human) { GUI.Label(new Rect(x, y, boxW - 10, lineH), $"<b>MP:</b> {u.mp:F1}"); y += lineH; }
        if (u is Human)
        {
            string panicStr = (u.mental < u.maxMental * 0.3f) ? " <color=red>공황</color>" : "";
            GUI.Label(new Rect(x, y, boxW - 10, lineH), $"<b>정신력:</b> {u.mental:F1} / {u.maxMental:F1}{panicStr}");
            y += lineH;
        }
        y += 5; // spacing
        GUI.Label(new Rect(x, y, boxW - 10, lineH), $"<b>물리공격력:</b> {u.physicalAttack:F1}"); y += lineH;
        GUI.Label(new Rect(x, y, boxW - 10, lineH), $"<b>물리방어력:</b> {u.physicalDefense:F1}"); y += lineH;
        GUI.Label(new Rect(x, y, boxW - 10, lineH), $"<b>마법공격력:</b> {u.magicalAttack:F1}"); y += lineH;
        GUI.Label(new Rect(x, y, boxW - 10, lineH), $"<b>마법방어력:</b> {u.magicalDefense:F1}"); y += lineH;
        y += 5; // spacing
        GUI.Label(new Rect(x, y, boxW - 10, lineH), $"<b>이동속도:</b> {u.walkSpeed:F1}"); y += lineH;
		y += 5; // spacing
		GUI.Label(new Rect(x, y, boxW - 10, lineH), "<b>기본 능력치</b>"); y += lineH;

		GUI.Label(new Rect(x, y, boxW - 10, lineH), $"근력: {u.sterngth:F1}"); y += lineH;
		GUI.Label(new Rect(x, y, boxW - 10, lineH), $"내구: {u.Durability:F1}"); y += lineH;
		GUI.Label(new Rect(x, y, boxW - 10, lineH), $"민첩: {u.agility:F1}"); y += lineH;
		GUI.Label(new Rect(x, y, boxW - 10, lineH), $"집중: {u.concentration:F1}"); y += lineH;
		GUI.Label(new Rect(x, y, boxW - 10, lineH), $"마력: {u.MagicPower:F1}"); y += lineH;
		GUI.Label(new Rect(x, y, boxW - 10, lineH), $"저항: {u.resistance:F1}"); y += lineH;
		GUI.Label(new Rect(x, y, boxW - 10, lineH), $"감각: {u.sense:F1}"); y += lineH;
		GUI.Label(new Rect(x, y, boxW - 10, lineH), $"통솔: {u.leadership:F1}"); y += lineH;
		GUI.Label(new Rect(x, y, boxW - 10, lineH), $"<b>위치:</b> ({u.position.x}, {u.position.y}) F{u.currentFloor}"); y += lineH;

        string statusStr = "";
        if (u.stunDuration > 0) statusStr += $"기절({u.stunDuration:F1}s) ";
        if (u.slowDuration > 0) statusStr += $"둔화({u.slowDuration:F1}s) ";
        if (u.poisonDuration > 0) statusStr += $"중독({u.poisonDuration:F1}s) ";
        if (u.burnDuration > 0) statusStr += $"화상({u.burnDuration:F1}s) ";
        if (statusStr != "")
        {
            GUI.Label(new Rect(x, y, boxW - 10, lineH), $"<color=red>상태이상: {statusStr}</color>");
        }
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

		StartCoroutine(DestroyFloatingText(go, 0.5f));
	}

	Vector3 GetWorldTextPosition(Unit unit)
	{
		return new Vector3(
			unit.position.x + unit.unitType.footprint.x * 0.5f,
			unit.position.y + unit.unitType.footprint.y + 1f,
			0
		);
	}

	IEnumerator DestroyFloatingText(GameObject go, float t)
	{
		yield return new WaitForSeconds(t);
		Destroy(go);
	}
}
