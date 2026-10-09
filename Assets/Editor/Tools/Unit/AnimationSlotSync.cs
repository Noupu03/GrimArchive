using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 유닛 프리팹에 "애니메이션 클립을 꽂는 자리"(UnitAnimationDriver 슬롯 목록)를 만들고 units.json과 맞춰 두는 도구.
//  - 새 프리팹을 만들지 않고 기존 Assets/Prefabs/Units/*.prefab을 열어 컴포넌트만 추가/갱신하므로 다른 내용
//    (보스 골렘 손 오브젝트 등)은 건드리지 않는다.
//  - 이미 꽂아 둔 클립은 보존한다. 새로 생긴 슬롯만 추가하고, units.json에서 사라진 스킬 슬롯은 클립이 비어 있으면
//    지우고 클립이 있으면 남긴 채 경고한다(작업자의 작업물을 지우지 않는다).
//  - 새 슬롯은 항상 빈 채로 만든다(클립은 작업자가 꽂는다).
public static class AnimationSlotSync
{
	private const string UnitsJsonPath   = "Assets/Data/units.json";
	private const string PrefabFolder    = "Assets/Prefabs/Units";
	private const string OldControllerPath = "Assets/Art/Animations/Char.controller";

#pragma warning disable 0649
	[Serializable] private class JsonUnit     { public string typeName; public string unitClass; public string[] skills; }
	[Serializable] private class JsonDatabase { public JsonUnit[] units; }
#pragma warning restore 0649

	[MenuItem("Tools(new)/유닛/애니메이션 슬롯 동기화")]
	public static void SyncAll()
	{
		if (!File.Exists(UnitsJsonPath))
		{
			Debug.LogError($"[AnimationSlotSync] {UnitsJsonPath} 파일을 찾을 수 없습니다.");
			return;
		}

		var db = JsonUtility.FromJson<JsonDatabase>(File.ReadAllText(UnitsJsonPath));
		int changed = 0, unchanged = 0, missing = 0;

		foreach (JsonUnit u in db.units)
		{
			string path = $"{PrefabFolder}/{SanitizeFileName(u.typeName)}.prefab";
			if (!File.Exists(path))
			{
				Debug.LogWarning($"[AnimationSlotSync] '{u.typeName}' 프리팹이 없습니다({path}) — 'JSON -> 유닛 프리팹 생성'을 먼저 실행하세요.");
				missing++;
				continue;
			}

			GameObject root = PrefabUtility.LoadPrefabContents(path);
			try
			{
				if (ApplyTo(root, u.unitClass == "Human", u.skills, u.typeName))
				{
					PrefabUtility.SaveAsPrefabAsset(root, path);
					changed++;
				}
				else unchanged++;
			}
			finally { PrefabUtility.UnloadPrefabContents(root); }
		}

		AssetDatabase.SaveAssets();
		Debug.Log($"[AnimationSlotSync] 완료 — 갱신 {changed}, 변경 없음 {unchanged}, 프리팹 없음 {missing}");
	}

	// 프리팹 루트에 UnitAnimationDriver를 보장하고 슬롯 목록을 맞춘다. 바뀐 게 있으면 true.
	// 컨버터가 새로 만든 루트에도 같은 함수를 써서, 프리팹을 다시 뽑아도 슬롯 구성이 같게 나온다.
	public static bool ApplyTo(GameObject root, bool isHuman, IEnumerable<string> skills, string debugName = null)
	{
		bool changed = false;

		var driver = root.GetComponent<UnitAnimationDriver>();
		if (driver == null) { driver = root.AddComponent<UnitAnimationDriver>(); changed = true; }

		// 옛 Char 컨트롤러는 한 번도 Walk를 재생한 적이 없다 — 드라이버가 Animator를 전담하므로 참조를 끊는다.
		foreach (Animator a in root.GetComponentsInChildren<Animator>(true))
		{
			if (a.runtimeAnimatorController != null && AssetDatabase.GetAssetPath(a.runtimeAnimatorController) == OldControllerPath)
			{
				a.runtimeAnimatorController = null;
				changed = true;
			}
		}

		var so  = new SerializedObject(driver);
		var arr = so.FindProperty("slots");
		Array slotValues = Enum.GetValues(typeof(AnimSlot));

		// 현재 프리팹의 항목: 키 → 클립 (순서 유지)
		var existing = new List<(string key, AnimSlot slot, string skill, AnimationClip clip)>();
		for (int i = 0; i < arr.arraySize; i++)
		{
			SerializedProperty e = arr.GetArrayElementAtIndex(i);
			AnimSlot slot = (AnimSlot)slotValues.GetValue(e.FindPropertyRelative("slot").enumValueIndex);
			string skill  = e.FindPropertyRelative("skillName").stringValue;
			var clip      = e.FindPropertyRelative("clip").objectReferenceValue as AnimationClip;
			existing.Add((AnimSlotCatalog.KeyOf(slot, skill), slot, skill, clip));
		}
		var existingByKey = new Dictionary<string, AnimationClip>();
		foreach (var e in existing) existingByKey[e.key] = e.clip;

		// 원하는 목록: 카탈로그 순서. 이미 있는 항목은 클립을 그대로 두고, 새 항목은 빈 채로 만든다.
		var desired = AnimSlotCatalog.SlotsFor(isHuman, skills);
		var next = new List<(AnimSlot slot, string skill, AnimationClip clip)>();
		var desiredKeys = new HashSet<string>();
		foreach (var d in desired)
		{
			string key = AnimSlotCatalog.KeyOf(d.slot, d.skillName);
			desiredKeys.Add(key);
			existingByKey.TryGetValue(key, out AnimationClip clip);
			next.Add((d.slot, d.skillName, clip));
		}

		// 목록에서 빠진 항목: 클립이 있으면 작업물을 지우지 않고 뒤에 남기며 경고, 비어 있으면 정리.
		foreach (var e in existing)
		{
			if (desiredKeys.Contains(e.key)) continue;
			if (e.clip != null)
			{
				Debug.LogWarning($"[AnimationSlotSync] '{debugName ?? root.name}'의 슬롯 '{e.key}'은 units.json에 없지만 클립이 꽂혀 있어 남겨 둡니다.");
				next.Add((e.slot, e.skill, e.clip));
			}
		}

		bool sameAsCurrent = next.Count == existing.Count;
		if (sameAsCurrent)
			for (int i = 0; i < next.Count && sameAsCurrent; i++)
				sameAsCurrent = AnimSlotCatalog.KeyOf(next[i].slot, next[i].skill) == existing[i].key && next[i].clip == existing[i].clip;

		if (!sameAsCurrent)
		{
			arr.arraySize = next.Count;
			for (int i = 0; i < next.Count; i++)
			{
				SerializedProperty e = arr.GetArrayElementAtIndex(i);
				e.FindPropertyRelative("slot").enumValueIndex = Array.IndexOf(slotValues, next[i].slot);
				e.FindPropertyRelative("skillName").stringValue = next[i].slot == AnimSlot.Skill ? next[i].skill : "";
				e.FindPropertyRelative("clip").objectReferenceValue = next[i].clip;
			}
			so.ApplyModifiedPropertiesWithoutUndo();
			changed = true;
		}
		return changed;
	}

	private static string SanitizeFileName(string name)
	{
		foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
		return name;
	}
}
