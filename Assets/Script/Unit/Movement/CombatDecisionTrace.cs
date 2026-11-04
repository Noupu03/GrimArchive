using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

// 임시 진단(2026-11-04) — "무엇이 인류 유닛을 움직였는지"를 유닛당 최근 12개 판단으로 남긴다. OscillationDiagnostics의 [진동진단] 로그는 왕복 순간의 스냅샷(상태 라벨·목표·거리)만 알려 줘서, 전투 중 원거리·지원 유닛이 공격 없이 왔다갔다하는 원인(후퇴↔추격 맞물림 / 지원 접근 / 회피 / 상태·대상 교체)을 코드만으로 확정하지 못했다.
// 동작은 바꾸지 않는다: 기록은 읽기만 하고, AIBehaviorConfig.oscillationDiagnosticsEnabled가 꺼지면 즉시 반환한다. 원인이 확정돼 고쳐지면 지워도 되는 임시 코드다(OscillationDiagnostics·PropagationDebugVisualizer와 같은 관례).
public static class CombatDecisionTrace
{
	public const int Capacity = 12;      // 유닛당 보관하는 판단 수
	public const int DescribeCount = 10; // 로그 한 줄에 싣는 최근 판단 수

	public static bool? EnabledOverride; // 테스트용
	public static Func<float> Clock = () => Time.time;

	public static bool Enabled => EnabledOverride ?? (AIConfigLoader.Behavior?.oscillationDiagnosticsEnabled ?? true);

	// 연속으로 같은 (태그, 상세)이면 새 항목을 만들지 않고 횟수만 올린다 — 제자리 대기처럼 매 틱 반복되는 판단이 링을 밀어내지 않게. 거리는 가장 최근 값으로 갱신한다.
	internal sealed class Ring
	{
		public struct Entry
		{
			public float Time;   // 이 항목이 처음 생긴 시각
			public string Tag;
			public int Dist;     // 적과의 체비셰프 거리, 모르면 -1
			public string Detail;
			public int Count;
		}

		private readonly List<Entry> _entries = new List<Entry>(Capacity + 1);

		public int Count => _entries.Count;
		public Entry this[int index] => _entries[index];

		public void Add(float time, string tag, int dist, string detail)
		{
			int last = _entries.Count - 1;
			if (last >= 0)
			{
				Entry e = _entries[last];
				if (e.Tag == tag && e.Detail == detail)
				{
					e.Count++;
					e.Dist = dist;
					_entries[last] = e;
					return;
				}
			}
			_entries.Add(new Entry { Time = time, Tag = tag, Dist = dist, Detail = detail, Count = 1 });
			if (_entries.Count > Capacity) _entries.RemoveAt(0);
		}

		// "-2.4s 후퇴(d3)[정밀 사격] | -2.0s 추격×3(d4)[정밀 사격]" — 오래된 것부터, 시각은 now 기준 경과(항목이 처음 생긴 시각).
		public string Format(float now, int maxCount)
		{
			if (_entries.Count == 0) return "기록 없음";
			var sb = new StringBuilder(160);
			int start = Math.Max(0, _entries.Count - maxCount);
			for (int i = start; i < _entries.Count; i++)
			{
				Entry e = _entries[i];
				if (i > start) sb.Append(" | ");
				sb.Append('-').Append((now - e.Time).ToString("F1")).Append("s ").Append(e.Tag);
				if (e.Count > 1) sb.Append('×').Append(e.Count);
				if (e.Dist >= 0) sb.Append("(d").Append(e.Dist).Append(')');
				if (!string.IsNullOrEmpty(e.Detail)) sb.Append('[').Append(e.Detail).Append(']');
			}
			return sb.ToString();
		}
	}

	// 유닛이 사라지면 기록도 같이 수거된다.
	private static readonly ConditionalWeakTable<Unit, Ring> _rings = new ConditionalWeakTable<Unit, Ring>();

	// tag·detail은 상수 문자열이나 이미 있는 문자열을 넘길 것 — 매 틱 불리는 곳에서 문자열을 새로 조립하면 진단이 GC 부담이 된다.
	public static void Record(Unit unit, string tag, int dist = -1, string detail = null)
	{
		if (!(unit is Human) || !Enabled) return;
		_rings.GetOrCreateValue(unit).Add(Clock(), tag, dist, detail);
	}

	public static string Describe(Unit unit, float now)
		=> unit != null && _rings.TryGetValue(unit, out Ring ring) ? ring.Format(now, DescribeCount) : "기록 없음";
}
