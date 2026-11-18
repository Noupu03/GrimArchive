using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 일회용: 생산 가능한 몬스터 2종(고블린 후드·놀)을 역할별로 차별화한다(체험판 난이도 곡선 D안, 2026-11-18).
// 런타임 스탯·스킬의 진실의 원천은 프리팹이고, units.json/skills.json은 프리팹과 이미 어긋나 있다(예: 근접 탱커 프리팹 HP 300 vs json 180,
// 아처 프리팹 '정밀 사격' 피해 배율 1 vs json 10). 그래서 json을 통째로 덮는 LoadDataFromJson 대신
// ① 지정한 스탯 필드만 바꾸고 ② 스킬은 이미 튜닝된 다른 프리팹의 SkillData를 그대로 복사한다. 비주얼·무기·애니메이션 슬롯은 건드리지 않는다.
// 수치는 전부 자리표시자다(근접 탱커 HP300·공격60을 기준으로 잡음).
// 배치 실행: Unity -batchmode -projectPath <경로> -executeMethod ApplyMonsterJsonToPrefabs.Run -quit
public static class ApplyMonsterJsonToPrefabs
{
	private const string Dir = "Assets/Prefabs/Units/";

	[MenuItem("Disposable/유닛/고블린 후드·놀 역할 차별화(체험판 D안)")]
	public static void Run()
	{
		var precise = CopySkill("아처형", "정밀 사격");
		var multi = CopySkill("무도가", "연격");
		var claw = CopySkill("고블린 후드", "급습 할퀴기");
		var slam = CopySkill("근접 탱커", "육중한 내리찍기");
		if (precise == null || multi == null || claw == null || slam == null)
		{
			Debug.LogError("[ApplyMonsterJsonToPrefabs] 원본 스킬을 찾지 못해 중단합니다.");
			return;
		}

		// 고블린 후드 = 원거리: 약하지만 뒤에서 쏜다
		Edit("고블린 후드", def =>
		{
			def.stats.maxHp = 140; def.stats.physicalAttack = 50; def.stats.physicalDefense = 6; def.stats.magicalDefense = 6;
			def.engageDistance = 6;
			def.skills = new List<SkillData> { precise, Clone(claw) };
		});

		// 놀 = 돌격: 빠르고 세게 치지만 탱커보다 무르다
		Edit("놀", def =>
		{
			def.stats.maxHp = 200; def.stats.physicalAttack = 65; def.stats.physicalDefense = 8; def.stats.magicalDefense = 6;
			def.stats.walkSpeed = 3.6f; def.stats.attackspeed = 120; def.stats.criticalChance = 18;
			def.skills = new List<SkillData> { multi, Clone(claw), slam };
		});

		AssetDatabase.SaveAssets();
		Debug.Log("[ApplyMonsterJsonToPrefabs] 완료");
	}

	private static void Edit(string unitName, System.Action<UnitVisualDefinition> apply)
	{
		string path = Dir + unitName + ".prefab";
		GameObject root = PrefabUtility.LoadPrefabContents(path);
		try
		{
			var def = root != null ? root.GetComponent<UnitVisualDefinition>() : null;
			if (def == null) { Debug.LogError($"[ApplyMonsterJsonToPrefabs] UnitVisualDefinition 없음: {path}"); return; }
			apply(def);
			PrefabUtility.SaveAsPrefabAsset(root, path);
			Debug.Log($"[ApplyMonsterJsonToPrefabs] 반영: {path} (HP {def.stats.maxHp}, 공격 {def.stats.physicalAttack}, 스킬 {def.skills.Count}개)");
		}
		finally
		{
			if (root != null) PrefabUtility.UnloadPrefabContents(root);
		}
	}

	private static SkillData CopySkill(string fromUnit, string skillName)
	{
		var go = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + fromUnit + ".prefab");
		var def = go != null ? go.GetComponent<UnitVisualDefinition>() : null;
		if (def == null) return null;
		foreach (var s in def.skills)
			if (s != null && s.skillName == skillName) return Clone(s);
		Debug.LogError($"[ApplyMonsterJsonToPrefabs] {fromUnit}에 '{skillName}' 없음");
		return null;
	}

	// 오브젝트 참조(투사체·이펙트 프리팹)까지 그대로 복사한다.
	private static SkillData Clone(SkillData src)
	{
		var copy = new SkillData();
		EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(src), copy);
		return copy;
	}
}
