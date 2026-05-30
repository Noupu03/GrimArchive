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

	public static void EvaluateDefense(Unit defender, Unit attacker, ThreatTileData threat)
	{
		if (defender == null || attacker == null) return;

		List<DefenseCandidate> candidates = BuildDefenseCandidates(defender, attacker, threat);

		if (candidates.Count == 0)
		{
			Debug.Log("방어 실패 -> 직격");
			defender.ApplyDirectDamage(attacker);
			return;
		}

		DefenseCandidate selected = SelectDefense(candidates);

		if (selected == null)
		{
			defender.ApplyDirectDamage(attacker);
			return;
		}

		Debug.Log($"{defender.unitType.typeName} 선택 방어 : {selected.type}");
		ExecuteDefense(defender, attacker, selected);
	}

	// =========================================================
	// 후보 생성
	// =========================================================

	static List<DefenseCandidate> BuildDefenseCandidates(Unit defender, Unit attacker, ThreatTileData threat)
	{
		List<DefenseCandidate> result = new List<DefenseCandidate>();

		// 실제 유닛 스탯 사용
		float durability = defender.Durability;
		float resistance = defender.resistance;
		float agility    = defender.agility;
		float sense      = defender.sense;
		float focus      = defender.concentration;
		float magic      = defender.MagicPower;

		// -------------------------------------------------
		// Block
		// -------------------------------------------------
		float blockScore = durability * 0.65f + resistance * 0.35f;
		result.Add(new DefenseCandidate(DefenseType.Block, blockScore, blockScore));

		// -------------------------------------------------
		// Dodge
		// -------------------------------------------------
		float dodgeScore = agility * 0.75f + sense * 0.25f;
		result.Add(new DefenseCandidate(DefenseType.Dodge, dodgeScore, dodgeScore));

		// -------------------------------------------------
		// Parry
		// -------------------------------------------------
		float parryScore = focus * 0.60f + sense * 0.30f + agility * 0.10f;
		result.Add(new DefenseCandidate(DefenseType.Parry, parryScore, parryScore));

		// -------------------------------------------------
		// Blink (마력 80 이상일 때만)
		// -------------------------------------------------
		if (magic >= 80f)
		{
			float blinkScore = magic * 0.80f + agility * 0.20f;
			result.Add(new DefenseCandidate(DefenseType.Blink, blinkScore, blinkScore));
		}

		// 최고 점수 가중치 x2
		float maxScore = 0f;
		for (int i = 0; i < result.Count; i++)
			maxScore = Mathf.Max(maxScore, result[i].score);
		for (int i = 0; i < result.Count; i++)
			if (Mathf.Approximately(result[i].score, maxScore))
				result[i].weight *= 2f;

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

	static void ExecuteDefense(Unit defender, Unit attacker, DefenseCandidate selected)
	{
		switch (selected.type)
		{
			// =================================================
			// Block
			// =================================================
			case DefenseType.Block:
			{
				float reduction = Mathf.Clamp(
					defender.Durability * 0.0075f + defender.resistance * 0.0025f,
					CombatConstants.MIN_BLOCK_DAMAGE_REDUCTION,
					CombatConstants.MAX_BLOCK_DAMAGE_REDUCTION
				);

				float raw      = attacker.physicalAttack;
				float base_dmg = Mathf.Max(1f, raw - defender.physicalDefense);
				float damage   = Mathf.Max(1f, base_dmg * (1f - reduction));

				defender.hp -= damage;
				Debug.Log($"{defender.unitType.typeName} Block! damage:{damage:F0}");
				UIManager.Instance?.ShowFloatingText(defender, $"Block! {damage:F0}");
				break;
			}

			// =================================================
			// Dodge (Hitbox 기반 판정 유지)
			// =================================================
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
						UIManager.Instance?.ShowFloatingText(defender, $"Dodge Success {success:P0}");
					}
					else
					{
						defender.ApplyDirectDamage(attacker);
					}
				}
				else
				{
					defender.ApplyDirectDamage(attacker);
				}
				break;
			}

			// =================================================
			// Parry
			// =================================================
			case DefenseType.Parry:
			{
				float success = Mathf.Clamp(
					defender.concentration * 0.006f + defender.sense * 0.002f + defender.agility * 0.002f,
					0.05f,
					0.85f
				);

				if (Random.value <= success)
				{
					attacker.ApplyDirectDamage(defender, 0.5f);
					UIManager.Instance?.ShowFloatingText(defender, $"Parry {success:P0}");
				}
				else
				{
					defender.ApplyDirectDamage(attacker);
				}
				break;
			}

			// =================================================
			// Blink
			// =================================================
			case DefenseType.Blink:
			{
				float cost = defender.maxMp * CombatConstants.BLINK_MP_COST_RATIO;

				if (defender.mp < cost)
				{
					defender.ApplyDirectDamage(attacker);
					return;
				}

				defender.mp -= cost;

				List<Vector2Int> safeTiles = FindSafeTiles(defender, defender.reactingThreat, 4, true);

				if (safeTiles.Count == 0)
				{
					defender.ApplyDirectDamage(attacker);
					return;
				}

				defender.ForceMove(safeTiles[Random.Range(0, safeTiles.Count)]);
				UIManager.Instance?.ShowFloatingText(defender, "Blink!");
				break;
			}
		}
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
				if (!ignoreUnits && !defender.CanMove(pos)) continue;

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

				// 벽 / 점령 타일 제외
				if (!defender.CanMove(candidate)) continue;

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
