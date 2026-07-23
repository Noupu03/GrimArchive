using System.Collections.Generic;
using System.Text;

// goap.cpp류 A*(균일비용 탐색 — 액션 종류가 적어 휴리스틱 없이도 충분히 빠르다) 플래너. GoapCore.cs가
// 예전에 하던 "목표에 맞는 액션 1개만 즉시 매칭"을 대신해 Preconditions/Effects를 실제로 체이닝해서
// 시작 상태(GoapWorldState.Build)에서 목표의 DesiredState까지 도달하는 액션 시퀀스를 찾는다. 지금까지
// 빠져있던 "Effects를 다음 상태에 적용"하는 단계(Apply)가 바로 이 파일에 있다.
public static class GoapPlanner
{
	private const int MaxDepth = 6; // 액션 종류가 ~18개뿐이라 이 정도 깊이면 충분(GoapCore.cs 등록 목록 참고)

	private class Node
	{
		public GoapState State;
		public List<GoapAction> Path;
		public float Cost;
	}

	// 실패하면 null(지금 유효한 액션들로는 목표에 도달할 경로가 없음 — GoapBrain은 계획 없이 랜덤 이동으로 폴백).
	public static List<GoapAction> Plan(Unit unit, GoapState start, GoapState desired, List<GoapAction> availableActions)
	{
		if (desired == null || IsSatisfied(start, desired)) return new List<GoapAction>();

		var validActions = new List<GoapAction>();
		foreach (var a in availableActions) if (a.IsValid(unit)) validActions.Add(a);

		var frontier = new List<Node> { new Node { State = start, Path = new List<GoapAction>(), Cost = 0f } };
		// 상태별로 지금까지 찾은 "가장 싼 도달 비용"을 기억한다(HashSet이 아니라 Dictionary인 이유:
		// 같은 상태에 먼저 도달한 액션이 있어도, 나중에 더 싼 경로로 같은 상태에 다시 도달하면 그쪽을
		// 채택해야 한다 — 등록 순서가 우연히 비용 순서와 같을 거라고 가정하지 않기 위함).
		var bestCost = new Dictionary<string, float> { [Serialize(start)] = 0f };

		while (frontier.Count > 0)
		{
			int bestIdx = 0;
			for (int i = 1; i < frontier.Count; i++) if (frontier[i].Cost < frontier[bestIdx].Cost) bestIdx = i;
			Node node = frontier[bestIdx];
			frontier.RemoveAt(bestIdx);

			// 이 노드가 이미 더 싼 경로로 대체된 상태라면(재-relax) 건너뛴다.
			string nodeKey = Serialize(node.State);
			if (bestCost.TryGetValue(nodeKey, out float known) && node.Cost > known) continue;

			// 균일비용 탐색에서는 "꺼낼 때"가 그 상태에 도달하는 최저 비용이 확정되는 시점이다 —
			// 만들어지는(generate) 시점에 바로 반환하면 같은 효과를 공유하는 더 싼 액션(예: Bypass/
			// Pass/Destroy가 전부 trapHandled=true를 선언)을 무시하고 먼저 발견된 걸 골라버릴 수 있다.
			if (IsSatisfied(node.State, desired)) return node.Path;

			if (node.Path.Count >= MaxDepth) continue;

			foreach (var action in validActions)
			{
				if (!Satisfies(node.State, action.Preconditions)) continue;

				GoapState nextState = Apply(node.State, action.Effects);
				float nextCost = node.Cost + action.Cost;
				string key = Serialize(nextState);
				if (bestCost.TryGetValue(key, out float existing) && existing <= nextCost) continue;

				bestCost[key] = nextCost;
				var nextPath = new List<GoapAction>(node.Path) { action };
				frontier.Add(new Node { State = nextState, Path = nextPath, Cost = nextCost });
			}
		}

		return null;
	}

	// GoapBrain이 매 틱 "다음 액션을 아직 실행해도 되는가"/"방금 실행한 액션이 실제로 끝났는가"를
	// 검증할 때 쓰는 공개 래퍼 — 탐색 내부의 Satisfies/IsSatisfied와 같은 판정을 재사용한다.
	public static bool PreconditionsMet(GoapState state, GoapAction action) => Satisfies(state, action.Preconditions);
	public static bool EffectsSatisfied(GoapState state, GoapAction action) => IsSatisfied(state, action.Effects);

	// GoapBrain이 "다음 우선순위 목표까지 미리 이어붙이기"(2026-07-22, 사용자 요청)를 구현할 때 쓴다 —
	// 방금 세운 계획(plan)의 Effects를 시작 상태에 순서대로 접어 넣어, "이 계획이 전부 끝난 뒤의 세계
	// 상태"를 만든 뒤 그 상태를 기준으로 다음 목표의 계획을 이어서 세운다.
	public static GoapState ApplyAll(GoapState start, List<GoapAction> plan)
	{
		GoapState state = start;
		foreach (var action in plan) state = Apply(state, action.Effects);
		return state;
	}

	private static bool IsSatisfied(GoapState state, GoapState desired)
	{
		foreach (var kv in desired)
			if (!state.TryGetValue(kv.Key, out bool v) || v != kv.Value) return false;
		return true;
	}

	private static bool Satisfies(GoapState state, GoapState preconditions)
	{
		foreach (var kv in preconditions)
			if (!state.TryGetValue(kv.Key, out bool v) || v != kv.Value) return false;
		return true;
	}

	private static GoapState Apply(GoapState state, GoapState effects)
	{
		var next = new GoapState();
		foreach (var kv in state) next[kv.Key] = kv.Value;
		foreach (var kv in effects) next[kv.Key] = kv.Value;
		return next;
	}

	private static string Serialize(GoapState state)
	{
		var keys = new List<string>(state.Keys);
		keys.Sort(System.StringComparer.Ordinal);
		var sb = new StringBuilder();
		foreach (var k in keys) { sb.Append(k); sb.Append(state[k] ? '1' : '0'); sb.Append('|'); }
		return sb.ToString();
	}
}
