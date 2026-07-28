using UnityEngine;
using UnityEditor;

// UnitVisual의 커스텀 인스펙터 — Play 모드에서 이 GameObject가 표현하는 유닛이 Human이면
// personalMap(개인 지도: 타일 위험도, 오브젝트, 몬스터 목격, 방 위험도/흥미도) 요약을 그대로 보여준다.
// "각 유닛 오브젝트에서 지도를 볼 수 있게" 하기 위한 디버그 뷰 — 씬에서 인류 유닛을 선택하면 바로 보인다.
[CustomEditor(typeof(UnitVisual))]
public class UnitVisualEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var visual = (UnitVisual)target;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("개인 지도는 Play 모드에서만 표시됩니다.", MessageType.Info);
            return;
        }

        if (visual.boundUnit is not Human human)
        {
            EditorGUILayout.HelpBox("이 유닛은 인류(Human)가 아니라 개인 지도를 갖지 않습니다.", MessageType.None);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("개인 지도 (PersonalMapKnowledge)", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(human.GetComponent<MemoryComponent>().personalMap.BuildDebugSummary(), GUILayout.MinHeight(120));

        // 지형 밝히기는 GameSession의 인류/몬스터 맵 텍스처(GameSessionEditor)와 동일한 방식으로
        // 그림으로 보여준다 — 칸이 금방 수백 단위로 늘어나 텍스트 나열은 못 봐줄 정도가 되기 때문.
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("지형 밝히기 (흰색=바닥, 회색=벽, 검정=미탐색)", EditorStyles.boldLabel);
        foreach (int floor in human.GetComponent<MemoryComponent>().personalMap.KnownTerrainFloors)
        {
            var tex = human.GetComponent<MemoryComponent>().personalMap.GetTerrainTexture(floor);
            if (tex == null) continue;

            EditorGUILayout.LabelField($"{floor}층");
            Rect rect = GUILayoutUtility.GetRect(128, 128);
            GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
        }

        // Play 모드 중 값이 계속 바뀌므로 매 프레임 다시 그려서 라이브로 갱신되게 한다.
        Repaint();
    }
}
