using System;
using UnityEditor;
using UnityEngine;

// UnitAnimationDriver 인스펙터 — 작업자가 프리팹만 열어도 "어느 상황에 어떤 클립이 재생되는지"를 알 수 있게
// 슬롯마다 한글 이름·종류·발동 조건을 보여 주고, 비어 있는 슬롯과 채워진 슬롯을 구분한다.
// 슬롯 목록 자체는 Tools(new)/유닛/애니메이션 슬롯 동기화가 만든다(여기서는 클립만 꽂는다).
[CustomEditor(typeof(UnitAnimationDriver))]
public class UnitAnimationDriverEditor : Editor
{
	private static GUIStyle _descStyle;

	private static GUIStyle DescStyle => _descStyle ??= new GUIStyle(EditorStyles.miniLabel)
	{
		wordWrap = true,
		padding  = new RectOffset(18, 4, 0, 4),
	};

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		EditorGUILayout.HelpBox(
			"아래 슬롯에 애니메이션 클립을 꽂으면 해당 상황에서 재생됩니다. 비워 둔 슬롯은 아무 동작도 하지 않습니다.\n" +
			"· 클립 경로는 이 프리팹 안 첫 Animator의 GameObject 기준입니다(리그 루트에 Animator를 두세요).\n" +
			"· 슬롯 목록이 units.json과 다르면 Tools(new)/유닛/애니메이션 슬롯 동기화를 실행하세요(꽂은 클립은 보존됩니다).",
			MessageType.Info);

		DrawPropertiesExcluding(serializedObject, "m_Script", "slots");

		SerializedProperty slots = serializedObject.FindProperty("slots");
		Array slotValues = Enum.GetValues(typeof(AnimSlot));

		int filled = 0;
		for (int i = 0; i < slots.arraySize; i++)
			if (slots.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue != null) filled++;

		EditorGUILayout.Space();
		EditorGUILayout.LabelField($"슬롯 {filled} / {slots.arraySize} 채워짐", EditorStyles.boldLabel);

		for (int i = 0; i < slots.arraySize; i++)
		{
			SerializedProperty e = slots.GetArrayElementAtIndex(i);
			AnimSlot slot   = (AnimSlot)slotValues.GetValue(e.FindPropertyRelative("slot").enumValueIndex);
			string skill    = e.FindPropertyRelative("skillName").stringValue;
			SerializedProperty clip = e.FindPropertyRelative("clip");

			AnimSlotInfo info = AnimSlotCatalog.Get(slot);
			string title = slot == AnimSlot.Skill ? $"스킬 · {skill}" : info.Label;
			string kind  = info.Kind == AnimSlotKind.Loop ? "반복" : info.Kind == AnimSlotKind.Hold ? "1회·유지" : "1회";

			Color prev = GUI.backgroundColor;
			if (clip.objectReferenceValue == null) GUI.backgroundColor = new Color(1f, 1f, 1f, 0.55f);
			EditorGUILayout.PropertyField(clip, new GUIContent($"{title}  [{kind}]", info.Description));
			GUI.backgroundColor = prev;

			EditorGUILayout.LabelField(info.Description, DescStyle);
		}

		serializedObject.ApplyModifiedProperties();
	}
}
