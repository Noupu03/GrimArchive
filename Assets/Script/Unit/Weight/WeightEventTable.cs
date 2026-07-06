using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Haare.Util.Logger;

// 연산공식 문서 3장 이벤트 변화량 표를 담는 정적 테이블.
// Assets/Data/weight_events.json에서 로드한다 (JsonToUnitPrefabConverter.cs와 동일하게
// "Assets/..." 상대경로 + File.ReadAllText를 쓰므로 에디터/Play 모드 전용 — 실제 빌드에서
// 쓰려면 Resources 또는 Addressables로 옮겨야 한다. 구현현황 문서에 기재).
public static class WeightEventTable
{
	private const string JsonPath = "Assets/Data/weight_events.json";

	public readonly struct Delta
	{
		public readonly float Understanding;
		public readonly float Danger;
		public Delta(float understanding, float danger) { Understanding = understanding; Danger = danger; }
	}

	[Serializable]
	private class JsonEvent
	{
		public string id;
		public float understanding;
		public float danger;
	}

	[Serializable]
	private class JsonEventDatabase { public JsonEvent[] events; }

	private static Dictionary<EventId, Delta> _table;

	private static Dictionary<EventId, Delta> Table
	{
		get
		{
			if (_table == null) Load();
			return _table;
		}
	}

	private static void Load()
	{
		_table = new Dictionary<EventId, Delta>();

		if (!File.Exists(JsonPath))
		{
			LogHelper.Error(LogHelper.GAME, $"[WeightEventTable] {JsonPath} 파일을 찾을 수 없습니다.");
			return;
		}

		var db = JsonUtility.FromJson<JsonEventDatabase>(File.ReadAllText(JsonPath));
		if (db?.events == null) return;

		foreach (var e in db.events)
		{
			if (!Enum.TryParse(e.id, out EventId parsed))
			{
				LogHelper.Warning(LogHelper.GAME, $"[WeightEventTable] 알 수 없는 EventId '{e.id}' — WeightEnums.EventId에 없음.");
				continue;
			}
			_table[parsed] = new Delta(e.understanding, e.danger);
		}
	}

	// E_PARTY_WIPEOUT / E_WIPEOUT_TRACE는 테이블에 없다(동적 계산 필요, HumanKnowledgeBase 전용 메서드 사용).
	public static bool TryGet(EventId id, out Delta delta) => Table.TryGetValue(id, out delta);

	public static Delta Get(EventId id)
	{
		if (Table.TryGetValue(id, out var d)) return d;
		LogHelper.Warning(LogHelper.GAME, $"[WeightEventTable] '{id}'는 테이블에 없음 (동적 이벤트이거나 3-2 흥미도 이벤트). 0/0 반환.");
		return default;
	}
}
