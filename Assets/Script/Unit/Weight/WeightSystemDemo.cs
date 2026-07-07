using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;

// 대표 가중치 3종(이해도/위험도/흥미도) 시스템을 실제로 돌려보는 수동 검증용 데모.
// 프로덕션 코드 경로(VContainer 주입)와 무관하게 HumanKnowledgeBase를 직접 new해서 쓰므로
// 아무 빈 GameObject에 붙이고 Play만 누르면 동작한다.
//
// 사용법:
//   1. 씬에 빈 GameObject를 만들고 이 컴포넌트를 붙인다.
//   2. Play 모드 진입 시 자동 실행되며, Console 창에서 [DEMO] 태그로 필터링해서 보면 된다.
//   3. Play 모드 중에도 인스펙터에서 컴포넌트 우클릭 → "가중치 시스템 검증 샘플 실행"으로 재실행 가능.
//
// 각 시나리오는 GrimArchive_대표가중치_연산공식_v0.7 문서의 장/예시 번호를 그대로 따라가며,
// 계산 결과와 "문서 기대값"을 나란히 로그로 찍어서 직접 대조해볼 수 있게 했다.
public class WeightSystemDemo : MonoBehaviour
{
	private readonly List<ScriptableObject> _spawned = new();

	private void Start() => RunAllScenarios();

	[ContextMenu("가중치 시스템 검증 샘플 실행")]
	private void RunAllScenarios()
	{
		CleanupSpawned();
		Section("가중치 시스템 검증 샘플 시작 (연산공식 문서 v0.7 기준)");

		Scenario1_PersonalWeightChange();
		Scenario2_GlobalReflection_MultipleSurvivors();
		Scenario3_PanicNoiseAndBlend();
		Scenario4_SpecialUnitUnderstanding_And_PoolDecrease();
		Scenario5_InfoNoiseRandomSamples();
		Scenario6_DangerDecreaseAndFinalDanger();
		Scenario7_WipeoutDanger();
		Scenario8_TileObjectUnitCorpseInterest();
		Scenario9_PersonalMap_ObserveMonster();

		Section("전체 시나리오 종료");
		CleanupSpawned();
	}

	// ─────────────────────────── 유틸 ───────────────────────────
	private static void Section(string title) => LogHelper.Log(LogHelper.DEMO, $"\n========== {title} ==========");
	private static void Log(string msg) => LogHelper.Log(LogHelper.DEMO, msg);
	// 부동소수점 비교는 오차 허용(기본 0.001) — 문자열 비교는 계산 순서에 따른 미세한 float
	// 오차로 실제로는 맞는데 "MISMATCH"로 잘못 뜰 수 있어서 피한다.
	private static void Expect(string label, float actual, float expected, float tolerance = 0.001f)
	{
		bool ok = Mathf.Abs(actual - expected) <= tolerance;
		PrintExpect(label, actual, expected, ok);
	}

	private static void Expect(string label, object actual, object expected)
	{
		bool ok = Equals(actual, expected);
		PrintExpect(label, actual, expected, ok);
	}

	private static void PrintExpect(string label, object actual, object expected, bool ok)
	{
		string mark = ok ? "<color=lime>OK</color>" : "<color=red>MISMATCH</color>";
		LogHelper.Log(LogHelper.DEMO, $"  [{mark}] {label} = {actual}  (문서 기대값: {expected})");
	}

	private Human NewHuman(string name, float mental = 100f, float maxMental = 100f)
	{
		var u = ScriptableObject.CreateInstance<Human>();
		u.name = name;
		u.unitType = new Knight();
		u.mental = mental;
		u.maxMental = maxMental;
		_spawned.Add(u);
		return u;
	}

	private Monster NewMonster(string name)
	{
		var u = ScriptableObject.CreateInstance<Monster>();
		u.name = name;
		u.unitType = new MeleeTank();
		_spawned.Add(u);
		return u;
	}

	private void CleanupSpawned()
	{
		foreach (var u in _spawned) if (u != null) Destroy(u);
		_spawned.Clear();
	}

	// ─────────────────────────── 시나리오 1. 4장: 개인 가중치 계산 ───────────────────────────
	private void Scenario1_PersonalWeightChange()
	{
		Section("시나리오 1 (4장) - 개인 가중치 즉시 반영: 인류가 몬스터에게 강타를 직접 맞음");

		var kb = new HumanKnowledgeBase();
		var ranger = NewHuman("인류_철수");
		var goblinKing = NewMonster("고블린킹");
		goblinKing.isSpecialUnit = true; // 개별 누적을 쓰는 보스 취급 → 식별 키가 종 타입명이 아니라 개체명(name)이 됨

		string key = PersonalWeightRecord.MakeKey(goblinKing.name, WeightType.Danger);
		ranger.personalWeights[key] = new PersonalWeightRecord(goblinKing.name, WeightType.Danger) { StoredValue = 2.0f };
		Log($"이벤트 전: 철수가 기억하는 고블린킹 개인 위험도 저장값 = {ranger.personalWeights[key].StoredValue}");

		string incidentId = System.Guid.NewGuid().ToString();
		kb.RecordEvent(EventId.E_HUMAN_KILL_SEEN, ranger, goblinKing, InfoType.DirectWitness, incidentId);

		float stored = ranger.personalWeights[key].StoredValue;
		int applied = ranger.personalWeights[key].AppliedValue;
		Log($"이벤트(E_HUMAN_KILL_SEEN, 직접목격) 발생 후: 저장값 = {stored}, 적용값 = {applied}");
		Expect("개인 위험도 저장값", stored, 5.0f);
		Expect("개인 위험도 적용값", applied, 5);
	}

	// ─────────────────────────── 시나리오 2. 6장: 다수 생존자 전역 반영 (6-4장 예시 2 재현) ───────────────────────────
	private void Scenario2_GlobalReflection_MultipleSurvivors()
	{
		Section("시나리오 2 (6장/6-4장 예시2) - 오크가 인류 1명을 처치, 생존자 B/C/D가 각자 다르게 정보를 가져옴");

		var kb = new HumanKnowledgeBase();
		var orc = NewMonster("오크");
		var witnessB = NewHuman("생존자_B");
		var witnessC = NewHuman("생존자_C");
		var witnessD = NewHuman("생존자_D");

		string incidentId = "INCIDENT_011_DEMO";
		Log("같은 사건(IncidentID 공유)을 B,C는 '직접 목격'으로, D는 '간접 파악'으로 인지함");
		kb.RecordEvent(EventId.E_HUMAN_KILL_SEEN, witnessB, orc, InfoType.DirectWitness, incidentId);
		kb.RecordEvent(EventId.E_HUMAN_KILL_SEEN, witnessC, orc, InfoType.DirectWitness, incidentId);
		kb.RecordEvent(EventId.E_HUMAN_KILL_INDIRECT, witnessD, orc, InfoType.Indirect, incidentId);

		Log("→ B/C는 동일 수치(+3, 직접목격)라 6-2장 기준 1회만 인정되어야 하고,");
		Log("  대표 변화량은 목격+3, 간접+1.5 → 전역 반영량 = (3*3 + 1.5*2)/(3+2) = 2.4 여야 한다.");

		kb.OnWaveEnd(new List<Unit> { witnessB, witnessC, witnessD }); // 생존자 전원 생환 처리

		float dangerApplied = kb.GetFinalDanger(orc.unitType.typeName, null, orc.baseDanger);
		Log($"웨이브 종료 후 '{orc.unitType.typeName}' 종 최종 위험도(저장값 반영 후 조회) = {dangerApplied}");
		Expect("오크 종 최종 위험도(적용값)", Mathf.FloorToInt(dangerApplied), 2);
	}

	// ─────────────────────────── 시나리오 3. 6-3장/25장: 정신력 오차 평균 + 공황 블렌드 ───────────────────────────
	private void Scenario3_PanicNoiseAndBlend()
	{
		Section("시나리오 3 (6-3장/25장) - 정신력 상태로 인한 수치 오차 평균 + 공황 단계 블렌드");

		var group = new List<IncidentEntry>
		{
			new IncidentEntry("INCIDENT_020_DEMO", EventId.E_HUMAN_KILL_SEEN, "Monster_A", WeightType.Danger, InfoType.DirectWitness, 3f, MentalErrorState.Normal, "B"),
			new IncidentEntry("INCIDENT_020_DEMO", EventId.E_HUMAN_KILL_SEEN, "Monster_A", WeightType.Danger, InfoType.DirectWitness, 3f, MentalErrorState.Normal, "C"),
			new IncidentEntry("INCIDENT_020_DEMO", EventId.E_HUMAN_KILL_SEEN, "Monster_A", WeightType.Danger, InfoType.DirectWitness, 3.2f, MentalErrorState.Fear, "D"),
		};
		Log("정상 B/C는 +3, 공포 상태 D는 +3.2로 같은 사건을 다르게 기록 (6-3장 예시)");
		float representative = WeightMath.ComputeGlobalReflectionAmount(group);
		Log($"직접 목격 대표 변화량 = {representative}");
		Expect("6-3장 대표 변화량", representative, 3.1f);

		Log("25장 예시: 공황 단계 유닛 기록값 140, 다음 우선순위 기록값 100");
		float blended = WeightMath.BlendPanicValue(140f, 100f);
		Log($"최종 기록값 = 140*0.3 + 100*0.7 = {blended}");
		Expect("25장 공황 블렌드 결과", blended, 112f);
	}

	// ─────────────────────────── 시나리오 4. 7-1장/8장: 특수유닛 이해도 + 총량 한도 감소 ───────────────────────────
	private void Scenario4_SpecialUnitUnderstanding_And_PoolDecrease()
	{
		Section("시나리오 4 (7-1장/8장) - 특수 유닛 이해도 합성 + 이해도 총량 한도 초과 시 감소");

		Log("보스급 특수 유닛: 종별 이해도 적용값 70, 개별 이해도 적용값 60 (각각 50 상한 캡)");
		float composed = WeightMath.ComposeUnderstanding(70f, 60f, isSpecialUnit: true);
		Log($"특수 유닛 최종 이해도 = min(70,50) + min(60,50) = {composed}");
		Expect("7-1장 특수유닛 이해도", composed, 100f);

		Log("8장 예시: 인류 전체 이해도 총합 199, 새로 반영될 증가량 3, 검증용 한도 200, 감소대상 현재값 20");
		var result = WeightMath.ComputePoolDecrease(currentTotal: 199f, incomingIncrease: 3f, poolCap: 200f, staleTargetCurrentValue: 20f);
		Log($"초과량={result.OverflowAmount}, 감소하한={result.DecreaseFloor}, 감소가능량={result.DecreaseCapacity}, 실제감소량={result.ActualDecrease}");
		Expect("초과량", result.OverflowAmount, 2f);
		Expect("감소 하한(20*0.3)", result.DecreaseFloor, 6f);
		Expect("감소 가능량(20-6)", result.DecreaseCapacity, 14f);
		Expect("실제 감소량(min(2,14))", result.ActualDecrease, 2f);
	}

	// ─────────────────────────── 시나리오 5. 9장/23장: 정보 오차 랜덤 샘플 ───────────────────────────
	private void Scenario5_InfoNoiseRandomSamples()
	{
		Section("시나리오 5 (9장/23장) - 오차 범위 안 랜덤 샘플 (여러 번 돌려서 범위를 벗어나지 않는지 직접 확인)");

		var kb = new HumanKnowledgeBase();

		Log("9장 예시: 미표기 내부 실제수치 100, 이해도 적용값 35 → ±30%, 인식범위 70~130 이어야 함");
		float ratio = WeightMath.HiddenInfoNoiseRatio(35);
		Expect("이해도 35의 오차율", ratio, 0.3f);
		var samples9 = new List<int>();
		for (int i = 0; i < 10; i++) samples9.Add(kb.ApplyHiddenInfoNoise(100, 35));
		Log($"샘플 10개: [{string.Join(", ", samples9)}] (전부 70~130 사이여야 함)");

		Log("23장 예시: 간접 파악, 실제 위험도 100 → ±10%, 인식범위 90~110 이어야 함");
		var samples23 = new List<int>();
		for (int i = 0; i < 10; i++) samples23.Add(kb.ApplyInfoValueNoise(100, InfoType.Indirect, MentalErrorState.Normal));
		Log($"샘플 10개: [{string.Join(", ", samples23)}] (전부 90~110 사이여야 함)");

		Log("같은 조건인데 관찰자가 '공황' 상태면 정신력 오차(±25%)가 정보유형 오차(±10%)보다 커서 그쪽을 채택해야 함");
		var (tileErr, valueErr) = WeightMath.InfoError(InfoType.Indirect, MentalErrorState.Panic);
		Expect("공황 상태일 때 채택되는 수치오차율", valueErr, 0.25f);
		Expect("공황 상태일 때 채택되는 위치오차(타일)", tileErr, 2);
	}

	// ─────────────────────────── 시나리오 6. 11장/12장: 위험도 감소 판정 + 최종위험도/단계 ───────────────────────────
	private void Scenario6_DangerDecreaseAndFinalDanger()
	{
		Section("시나리오 6 (11장/12장) - 위험도 감소 판정 + 최종 위험도 계산/단계");

		Log("11-1장 예시: 처치 참여 인류가 받은 총 피해량 2, 대상 기준 피해량 12");
		bool qualifies = WeightMath.TotalDamageDecreaseQualifies(2f, 12f, out float judgement);
		Log($"판정값 = 2-12 = {judgement} → 감소 조건 충족 여부 = {qualifies}");
		Expect("11-1장 판정값", judgement, -10f);
		Expect("11-1장 감소 조건 충족", qualifies, true);

		Log("11-2장 예시: 타격 1회 기준피해량 12, 실제 적용 피해량 10");
		bool perHit = WeightMath.PerHitDecreaseQualifies(12f, 10f, out float diff);
		Log($"차이 = {diff} → 조건 충족 여부 = {perHit}");
		Expect("11-2장 피해량 차이", diff, 2f);

		Log("12-1장 예시: 기본위험도 10 + 종별누적 640 + 개별누적 480 = 1130 (999로 클램프되어야 함)");
		float finalDanger = WeightMath.ComposeFinalDanger(baseDanger: 10f, speciesAccumulated: 640f, individualAccumulated: 480f);
		var stage = WeightMath.GetDangerStage(Mathf.FloorToInt(finalDanger));
		Log($"최종 위험도 = {finalDanger}, 위험도 단계 = {stage}");
		Expect("12-1장 최종 위험도(클램프)", finalDanger, 999f);
		Expect("위험도 단계", stage, DangerStage.StageMax);
	}

	// ─────────────────────────── 시나리오 7. 13장: 전멸 위험도 ───────────────────────────
	private void Scenario7_WipeoutDanger()
	{
		Section("시나리오 7 (13장) - 파티 전멸 던전 위험도 + 전멸 흔적 발견 (중복 반영 방지)");

		var kb = new HumanKnowledgeBase();
		Log("파티 전멸 발생 (13-1장: 던전 위험도 +30)");
		kb.OnPartyWipeout();
		Log($"던전 전체 위험도 = {kb.GetDungeonDanger()}");
		Expect("파티 전멸 직후 던전 위험도", kb.GetDungeonDanger(), 30f);

		Log("전멸 원인 대상 위험도 단계 = Stage2(+10 보정) 인 전멸 흔적을 등록 후, 생환 파티가 발견함");
		string traceId = kb.RegisterWipeoutTrace(DangerStage.Stage2);
		kb.OnWipeoutTraceReflected(traceId);
		Log($"1차 반영 후 던전 위험도 = {kb.GetDungeonDanger()} (기대값: 30 + 25 + 10 = 65)");
		Expect("전멸흔적 1차 반영 후 던전 위험도", kb.GetDungeonDanger(), 65f);

		Log("같은 흔적을 다른 파티가 또 발견해서 재반영을 시도함 (13-2장: 동일 흔적ID 1회만 반영되어야 함)");
		kb.OnWipeoutTraceReflected(traceId);
		Log($"2차 반영 시도 후 던전 위험도 = {kb.GetDungeonDanger()} (변화 없어야 함)");
		Expect("중복 반영 방지 확인", kb.GetDungeonDanger(), 65f);
	}

	// ─────────────────────────── 시나리오 8. 15~19장: 개인 지도(타일/오브젝트/유닛/시체) 흥미도·위험도 ───────────────────────────
	// 15~17장/20~21장은 2026-07-07부로 개인유닛화되어 HumanKnowledgeBase가 아니라
	// PersonalMapKnowledge(원래는 Human.personalMap으로 각 유닛이 개별로 들고 있음, 여기선 검증용으로
	// 독립 인스턴스 사용)로 옮겨갔다. 18장/19장(유닛/시체 흥미도)은 그대로 HumanKnowledgeBase/WeightMath에 있다.
	private void Scenario8_TileObjectUnitCorpseInterest()
	{
		Section("시나리오 8 (15~17장/18장/19장) - 개인 지도(타일/오브젝트) + 유닛/시체 흥미도");

		var kb = new HumanKnowledgeBase();
		var map = new PersonalMapKnowledge();
		var tile = new Vector3Int(3, 4, 0);

		Log("16장: 미탐사 타일(기본흥미도5) 위에 흥미도120짜리 오브젝트가 있으면 타일 흥미도 = 5+120 = 125 여야 함");
		map.RegisterObjectInterest("obj_보물상자", tile, 120f);
		float tileInterest = map.GetTileInterest(tile, explored: false, objectIdAtTile: "obj_보물상자");
		Expect("타일 최종 흥미도", tileInterest, 125f);

		Log("17장: 조사 완료 시 오브젝트 흥미도 50% 감소 (120 → 60)");
		map.OnObjectInvestigated("obj_보물상자");
		Expect("조사 완료 후 오브젝트 흥미도", map.GetTileInterest(tile, false, "obj_보물상자") - WeightMath.UnexploredTileBaseInterest, 60f);

		Log("회수 중 유닛 사망으로 드랍 시 기본값×50% 재적용 (120 × 0.5 = 60)");
		map.OnObjectDroppedByCarrierDeath("obj_보물상자");
		Expect("드랍 후 오브젝트 흥미도", map.GetTileInterest(tile, false, "obj_보물상자") - WeightMath.UnexploredTileBaseInterest, 60f);

		Log("18장: 일반 유닛 기본흥미도 100, 이해도 적용값 35 → 흥미도 = 100*65% = 65 여야 함");
		// kb.GetUnitInterest(Unit)은 종별 이해도를 이벤트 파이프라인을 통해서만 갱신할 수 있어
		// 특정 이해도값(35)을 재현하기 번거로우므로, 같은 공식을 직접 호출해 검증한다.
		float unitInterest = WeightMath.UnitInterestFromUnderstanding(baseInterest: 100f, understandingApplied: 35);
		Log($"UnitInterestFromUnderstanding(100, 35) = {unitInterest}");
		Expect("18장 유닛 흥미도", unitInterest, 65f);

		Log("19장: 시체/흔적 기본흥미도 10, 원인 대상 위험도 단계 Stage2(+10) → 20 이어야 함");
		float corpseInterest = WeightMath.CorpseTraceInterest(DangerStage.Stage2);
		Expect("19장 시체/흔적 흥미도", corpseInterest, 20f);

		Log("15장: 미탐사 타일 기본 위험도 = 2 여야 함");
		var dangerTile = new Vector3Int(9, 9, 0);
		Expect("미탐사 타일 기본 위험도", map.GetTileDanger(dangerTile, explored: false), 2f);

		Log("위험도 250(Stage1, 안전확인 2초)짜리 유닛이 있었던 타일 → 위협 사라진 뒤 2.5초 경과하면 위험도 0");
		map.SetTileDangerFromUnit(dangerTile, 250f);
		map.TickTileSafety(dangerTile, threatPresent: false, deltaTime: 2.5f);
		Expect("안전확인시간 경과 후 타일 위험도", map.GetTileDanger(dangerTile, explored: true), 0f);
	}

	// ─────────────────────────── 시나리오 9 (신규). 개인 지도 — 몬스터 목격 기록 ───────────────────────────
	// UnitFunction.CastRay가 인류 유닛의 시야에 몬스터가 처음 들어오는 순간 자동으로 호출하는 것과
	// 동일한 동작을 재현한다(personalMap.ObserveMonster). 실제 게임에서는 CastRay가 이 호출을 대신 한다.
	private void Scenario9_PersonalMap_ObserveMonster()
	{
		Section("시나리오 9 (신규) - 인류가 몬스터를 목격하면 개인 지도에 위치+위험도+흥미도가 기록됨");

		var kb = new HumanKnowledgeBase();
		var ranger = NewHuman("정찰병");
		var goblin = NewMonster("정찰용_고블린");
		goblin.baseDanger = 50f;

		var sightingTile = new Vector3Int(7, 2, 0);
		Log($"정찰병이 {sightingTile} 타일에서 '{goblin.unitType.typeName}'을 처음 목격함");

		// CastRay 내부와 동일한 계산 — 목격 시점의 위험도/흥미도를 스냅샷으로 지도에 기록.
		float danger = kb.GetFinalDanger(goblin.unitType.typeName, null, goblin.baseDanger);
		float interest = kb.GetUnitInterest(goblin);
		ranger.personalMap.ObserveMonster(goblin.name, sightingTile, danger, interest);

		Log($"기록된 위험도={danger}, 흥미도={interest}");
		Expect("목격 시점 위험도 = baseDanger(종별 누적 없음)", danger, 50f);

		bool found = ranger.personalMap.TryGetMonsterSighting(goblin.name, out var tile, out var recordedDanger, out var recordedInterest);
		Expect("몬스터 목격 기록 존재 여부", found, true);
		Expect("기록된 목격 위치", tile, sightingTile);
		Expect("기록된 목격 위험도", recordedDanger, danger);

		Log("15장: 몬스터가 있던 타일은 그 몬스터의 기록 위험도를 그대로 타일 위험도로 가져야 함");
		Expect("목격 타일의 위험도", ranger.personalMap.GetTileDanger(sightingTile, explored: true), danger);

		Log("지형 밝히기: CastRay가 시야가 지나가는 모든 타일마다 벽/바닥 여부를 개인 지도에 기록함");
		var wallTile = new Vector3Int(8, 2, 0);
		var floorTile = new Vector3Int(6, 2, 0);
		var unrevealedTile = new Vector3Int(0, 0, 0);
		ranger.personalMap.RevealTile(wallTile, isWall: true);
		ranger.personalMap.RevealTile(floorTile, isWall: false);
		Expect("벽으로 밝힌 타일", ranger.personalMap.GetTileTerrain(wallTile), 2);
		Expect("바닥으로 밝힌 타일", ranger.personalMap.GetTileTerrain(floorTile), 1);
		Expect("아직 안 밝힌 타일 = 미탐색(0)", ranger.personalMap.GetTileTerrain(unrevealedTile), 0);
		Expect("지형 밝힘 여부로 자동 판단하는 GetTileDanger 오버로드", ranger.personalMap.GetTileDanger(floorTile), ranger.personalMap.GetTileDanger(floorTile, explored: true));
		Expect("아직 안 밝힌 타일은 미탐사 취급", ranger.personalMap.GetTileDanger(unrevealedTile), ranger.personalMap.GetTileDanger(unrevealedTile, explored: false));

		Log($"정찰병 개인 지도 요약:\n{ranger.personalMap.BuildDebugSummary()}");
	}
}
