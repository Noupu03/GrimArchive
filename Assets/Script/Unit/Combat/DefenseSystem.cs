using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum DefenseType { None, Block, Dodge, Blink, Parry }

public class DefenseCandidate
{
	public DefenseType type;
	public float score;
	public float weight;

	public DefenseCandidate(DefenseType type, float score, float weight)
	{
		this.type   = type;
		this.score  = score;
		this.weight = weight;
	}
}

public static class DefenseSystem
{
	// =========================================================
	// 메인
	// =========================================================

	public static void EvaluateEarlyReaction(Unit defender, Unit attacker, ThreatTileData threat)
	{
		if (defender == null || attacker == null) return;

		List<DefenseCandidate> candidates = BuildDefenseCandidates(defender, attacker, threat, true); // true = early reaction only

		if (candidates.Count == 0) return;

		DefenseCandidate selected = SelectDefense(candidates);
		if (selected == null) return;

		ExecuteEarlyReaction(defender, attacker, selected);
	}

	public static float EvaluateImpactDefense(Unit defender, Unit attacker, float rawDamage)
	{
		if (defender == null || attacker == null) return rawDamage;

		// Parry or Block only
		List<DefenseCandidate> candidates = BuildDefenseCandidates(defender, attacker, null, false); // false = impact defense only

		if (candidates.Count == 0) return rawDamage;

		DefenseCandidate selected = SelectDefense(candidates);
		if (selected == null) return rawDamage;

		return ExecuteImpactDefense(defender, attacker, selected, rawDamage);
	}

	// =========================================================
	// 후보 생성
	// =========================================================

	static List<DefenseCandidate> BuildDefenseCandidates(Unit defender, Unit attacker, ThreatTileData threat, bool isEarlyReaction)
	{
		List<DefenseCandidate> result = new List<DefenseCandidate>();

		float durability = defender.Durability;
		float resistance = defender.resistance;
		float agility    = defender.agility;
		float sense      = defender.sense;
		float focus      = defender.concentration;
		float magic      = defender.MagicPower;

		if (isEarlyReaction)
		{
			// Dodge
			float dodgeScore = agility * 0.75f + sense * 0.25f;
			result.Add(new DefenseCandidate(DefenseType.Dodge, dodgeScore, dodgeScore));

			// Blink
			if (magic >= 80f)
			{
				float blinkScore = magic * 0.80f + agility * 0.20f;
				result.Add(new DefenseCandidate(DefenseType.Blink, blinkScore, blinkScore));
			}
		}
		else
		{
			// Block
			float blockScore = durability * 0.65f + resistance * 0.35f;
			result.Add(new DefenseCandidate(DefenseType.Block, blockScore, blockScore));

			// Parry
			float parryScore = focus * 0.60f + sense * 0.30f + agility * 0.10f;
			result.Add(new DefenseCandidate(DefenseType.Parry, parryScore, parryScore));
		}

		// 최고 점수 가중치 x2
		if (result.Count > 0)
		{
			float maxScore = 0f;
			for (int i = 0; i < result.Count; i++)
				maxScore = Mathf.Max(maxScore, result[i].score);
			for (int i = 0; i < result.Count; i++)
				if (Mathf.Approximately(result[i].score, maxScore))
					result[i].weight *= 2f;
		}

		return result;
	}

	// =========================================================
	// 가중치 선택
	// =========================================================

	static DefenseCandidate SelectDefense(List<DefenseCandidate> list)
	{
		if (list == null || list.Count == 0) return null;

		float total = 0f;
		for (int i = 0; i < list.Count; i++) total += list[i].weight;

		float roll  = Random.Range(0f, total);
		float accum = 0f;

		for (int i = 0; i < list.Count; i++)
		{
			accum += list[i].weight;
			if (roll <= accum) return list[i];
		}

		return list[list.Count - 1];
	}

	// =========================================================
	// 실행
	// =========================================================

	static bool HasPathWithoutWalls(Unit defender, Vector2Int start, Vector2Int target, int maxRange)
	{
		Queue<Vector2Int> queue = new Queue<Vector2Int>();
		Dictionary<Vector2Int, int> dists = new Dictionary<Vector2Int, int>();

		queue.Enqueue(start);
		dists[start] = 0;

		while (queue.Count > 0)
		{
			Vector2Int current = queue.Dequeue();
			int d = dists[current];

			if (current == target) return true;
			if (d >= maxRange) continue;

			for (int x = -1; x <= 1; x++)
			{
				for (int y = -1; y <= 1; y++)
				{
					if (x == 0 && y == 0) continue;
					Vector2Int nextPos = current + new Vector2Int(x, y);

					if (dists.ContainsKey(nextPos)) continue;

					if (!defender.CanMove(nextPos, true)) continue;

					// 대각선 이동 시 코너 커팅(벽 뚫기) 방지
					if (Mathf.Abs(x) == 1 && Mathf.Abs(y) == 1)
					{
						if (!defender.CanMove(current + new Vector2Int(x, 0), true) ||
							!defender.CanMove(current + new Vector2Int(0, y), true))
						{
							continue;
						}
					}

					dists[nextPos] = d + 1;
					queue.Enqueue(nextPos);
				}
			}
		}

		return false;
	}

	static void ExecuteEarlyReaction(Unit defender, Unit attacker, DefenseCandidate selected)
	{
		switch (selected.type)
		{
			case DefenseType.Dodge:
			{
				float success = Mathf.Clamp(
					defender.agility * 0.007f + defender.sense * 0.003f,
					CombatConstants.MIN_DEFENSE_SUCCESS_RATE,
					CombatConstants.MAX_DEFENSE_SUCCESS_RATE
				);

				if (Random.value <= success)
				{
					bool moved = TryDodgeMove(defender, defender.reactingThreat);
					if (moved)
					{
						defender.evadeCooldown = 1.2f;
					}
				}
				break;
			}
			case DefenseType.Blink:
			{
				float cost = defender.maxMp * CombatConstants.BLINK_MP_COST_RATIO;
				if (defender.mp < cost) return;

				List<Vector2Int> safeTiles = FindSafeTiles(defender, defender.reactingThreat, 4, true);
				
				// 벽 관통 방지 필터링: 출발지부터 목적지까지 벽을 뚫지 않는 경로가 존재하는 타일만 선별
				List<Vector2Int> validTiles = new List<Vector2Int>();
				foreach (var tile in safeTiles)
				{
					if (HasPathWithoutWalls(defender, defender.position, tile, 4))
					{
						validTiles.Add(tile);
					}
				}

				if (validTiles.Count == 0) return;

				defender.mp -= cost;
				defender.ForceMove(validTiles[Random.Range(0, validTiles.Count)]);
				break;
			}
		}
	}

	static float ExecuteImpactDefense(Unit defender, Unit attacker, DefenseCandidate selected, float rawDamage)
	{
		switch (selected.type)
		{
			case DefenseType.Block:
			{
				float reduction = Mathf.Clamp(
					defender.Durability * 0.0075f + defender.resistance * 0.0025f,
					CombatConstants.MIN_BLOCK_DAMAGE_REDUCTION,
					CombatConstants.MAX_BLOCK_DAMAGE_REDUCTION
				);

				var guardDef = defender;
				guardDef.suppressHitVFX = true;
				guardDef.pendingVFX = () => defender.Generate?.SpawnGuardVFX(guardDef);
				
				return rawDamage * (1f - reduction);
			}

			case DefenseType.Parry:
			{
				float success = Mathf.Clamp(
					defender.concentration * 0.006f + defender.sense * 0.002f + defender.agility * 0.002f,
					0.05f,
					0.85f
				);

				if (Random.value <= success)
				{
					// 반격 데미지
					attacker.TakePhysicalDamage(defender.physicalAttack * 0.5f, defender);

					var parryDef = defender;
					parryDef.suppressHitVFX = true;
					parryDef.pendingVFX = () => defender.Generate?.SpawnParryVFX(parryDef);
					
					return 0f;
				}
				break;
			}
		}
		
		return rawDamage;
	}

	// =========================================================
	// 안전 타일 탐색 (HITBOX 기반)
	// =========================================================

	static List<Vector2Int> FindSafeTiles(Unit defender, ThreatTileData threat, int radius, bool ignoreUnits)
	{
		List<Vector2Int> result = new List<Vector2Int>();

		for (int x = -radius; x <= radius; x++)
		{
			for (int y = -radius; y <= radius; y++)
			{
				Vector2Int pos = defender.position + new Vector2Int(x, y);
				if (pos == defender.position) continue;
				if (Vector2Int.Distance(defender.position, pos) > radius) continue;

				// =================================================
				// HITBOX 판정
				// =================================================
				if (threat != null && Hitbox.IsInside(pos, threat.hitbox)) continue;

				int w = (int)defender.unitType.footprint.x;
				int h = (int)defender.unitType.footprint.y;
				bool unsafeTile = false;

				for (int dx = 0; dx < w && !unsafeTile; dx++)
				{
					for (int dy = 0; dy < h && !unsafeTile; dy++)
					{
						Vector2Int p = new Vector2Int(pos.x + dx, pos.y + dy);
						if (threat != null && Hitbox.IsInside(p, threat.hitbox))
							unsafeTile = true;
					}
				}

				if (unsafeTile) continue;
				if (!defender.CanMove(pos, ignoreUnits)) continue;

				result.Add(pos);
			}
		}

		return result;
	}

	// =========================================================
	// Dodge 이동 — 히트박스와 겹침 면적이 가장 적은 방향으로 회피
	// =========================================================

	static bool TryDodgeMove(Unit defender, ThreatTileData threat)
	{
		float currentOverlap = threat != null
			? GetOverlapArea(defender, defender.position, threat.hitbox)
			: 0f;

		List<Vector2Int> best    = new List<Vector2Int>();
		float            minOverlap = currentOverlap;

		for (int x = -1; x <= 1; x++)
		{
			for (int y = -1; y <= 1; y++)
			{
				if (x == 0 && y == 0) continue;
				Vector2Int candidate = defender.position + new Vector2Int(x, y);

				// 벽/ 유닛 대상 제외
				if (!defender.CanMove(candidate)) continue;

				// 코너 커팅(벽 뚫기) 방지: 대각선 회피 시 양옆 직교 타일 중 하나라도 이동 불가면 회피 불가
				if (Mathf.Abs(x) == 1 && Mathf.Abs(y) == 1)
				{
					if (!defender.CanMove(defender.position + new Vector2Int(x, 0)) ||
						!defender.CanMove(defender.position + new Vector2Int(0, y)))
					{
						continue;
					}
				}

				float overlap = threat != null
					? GetOverlapArea(defender, candidate, threat.hitbox)
					: 0f;

				if (overlap < minOverlap - 0.001f)
				{
					// 현재 최선보다 더 나은 위치
					minOverlap = overlap;
					best.Clear();
					best.Add(candidate);
				}
				else if (Mathf.Abs(overlap - minOverlap) <= 0.001f)
				{
					// 동률 — 랜덤 선택 풀에 추가
					best.Add(candidate);
				}
			}
		}

		if (best.Count == 0) return false;

		defender.ForceMove(best[Random.Range(0, best.Count)]);
		return true;
	}

	// 주어진 위치에서 유닛 히트박스와 위협 히트박스의 겹침 면적
	static float GetOverlapArea(Unit unit, Vector2Int pos, Hitbox threatHitbox)
	{
		Hitbox unitBox = new Hitbox
		{
			center = (Vector2)pos + new Vector2(unit.unitType.footprint.x, unit.unitType.footprint.y) * 0.5f,
			size   = new Vector2(unit.unitType.footprint.x, unit.unitType.footprint.y)
		};
		return threatHitbox.CalculateOverlapArea(unitBox);
	}
}
