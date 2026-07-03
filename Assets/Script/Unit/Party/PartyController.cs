
using UnityEngine;
using System.Collections.Generic;
using Haare.Util.Logger;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PartyController : MonoBehaviour
{
	/*
    public static PartyController Instance;

    public List<Party> activeParties = new List<Party>();

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (GameSession.Instance == null) return;

        foreach (var p in activeParties)
        {
            p.UpdateLeader();
        }
    }

    public Party GetPartyOf(Unit u)
    {
        foreach (var p in activeParties)
        {
            if (p.members.Contains(u)) return p;
        }
        return null;
    }

    public void HandleSpottingBroadcast(Unit spotter, Unit target)
    {
        Party p = GetPartyOf(spotter);
        if (p == null) return;

        if (p.leaderlessShockTimer > 0f) return; // 쇼크 상태면 Broadcast 무효 (효율 상실)

        // 같은 파티의 아군에게 전달하여 Wait 상태 진입
        foreach (var u in p.members)
        {
            if (u.hp > 0 && u.currentFloor == spotter.currentFloor && u != spotter)
            {
                u.isWaitState = true;
                LogHelper.Log(LogHelper.GAME, $"[{p.partyName}] {u.unitType.typeName}가 {spotter.unitType.typeName}의 Broadcast를 수신하고 대기 상태 진입.");
            }
        }
    }*/
}

#if UNITY_EDITOR // PartyController 인스펙터 꾸미기
[CustomEditor(typeof(PartyController))]
public class PartyControllerEditor : Editor
{
    /*
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        PartyController pc = (PartyController)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("파티 현황", EditorStyles.boldLabel);

        if (pc.activeParties == null || pc.activeParties.Count == 0)
        {
            EditorGUILayout.HelpBox("현재 파티가 없습니다.", MessageType.Info);
        }
        else
        {
            foreach (var party in pc.activeParties)
            {
                EditorGUILayout.BeginVertical("box");

                string goalStr = party.partyGoal switch {
                    PartyGoal.Sweep => "소탕 파티",
                    PartyGoal.Exploration => "탐사 파티",
                    PartyGoal.Recovery => "회수 파티",
                    _ => "알 수 없음"
                };

                EditorGUILayout.LabelField($"■ {party.partyName} ({goalStr})", EditorStyles.boldLabel);

                if (party.leaderlessShockTimer > 0)
                {
                    GUIStyle redStyle = new GUIStyle(EditorStyles.label);
                    redStyle.normal.textColor = Color.red;
                    EditorGUILayout.LabelField($"리더리스 쇼크: {party.leaderlessShockTimer:F1}s", redStyle);
                }

                if (party.members.Count == 0)
                {
                    EditorGUILayout.LabelField("파티원 전멸/없음");
                }
                else
                {
                    foreach (var u in party.members)
                    {
                        if (u == null) continue;
                        string leaderMark = (party.leader == u) ? "★(리더)" : "";
                        EditorGUILayout.LabelField($"  - {u.unitType.typeName} {leaderMark}");
                        EditorGUILayout.LabelField($"    HP: {u.hp:F1} / MP: {u.mp:F1} / 정신력: {u.currentMental:F1}");
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
        }

        Repaint();
    }*/
}
#endif
