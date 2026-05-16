using UnityEngine;
using System.Collections.Generic;

public abstract class UnitFunction : Unit
{
	public override void TakeDamage(float damage)
	{
		float prevHp = hp;
		hp -= damage;
		isHitThisTurn = true;
		if (UnitGenerate.Instance != null) UnitGenerate.Instance.TriggerHitEffect(this);
	}

	public override void TakePhysicalDamage(float rawDamage, Unit attacker)
	{
		float damage = Mathf.Max(1f, rawDamage - physicalDefense);
		TakeDamage(damage);
	}

	public override void TakeMagicalDamage(float rawDamage, Unit attacker)
	{
		float damage = Mathf.Max(1f, rawDamage - magicalDefense);
		TakeDamage(damage);
	}

	public override void TakeMentalDamage(float rawDamage, Unit attacker)
	{
		if (this is Human)
		{
			int prevStage = Mathf.FloorToInt(mental / (maxMental * 0.25f));
			mental -= rawDamage; // 정신력만 감소
			int currentStage = Mathf.FloorToInt(mental / (maxMental * 0.25f));
			isHitThisTurn = true;
		}
	}


	public override void ApplyStun(float duration) { stunDuration = Mathf.Max(stunDuration, duration); }
	public override void ApplySlow(float duration) { slowDuration = Mathf.Max(slowDuration, duration); }
	public override void ApplyPoison(float duration) { poisonDuration = Mathf.Max(poisonDuration, duration); }
	public override void ApplyBurn(float duration) { burnDuration = Mathf.Max(burnDuration, duration); }

	#region 기능함수들
	public override Vector2Int GetDirVector(Dir dir)
	{
		switch (dir)
		{
			case Dir.UP: return new Vector2Int(0, 1);
			case Dir.UP_RIGHT: return new Vector2Int(1, 1);
			case Dir.RIGHT: return new Vector2Int(1, 0);
			case Dir.DOWN_RIGHT: return new Vector2Int(1, -1);
			case Dir.DOWN: return new Vector2Int(0, -1);
			case Dir.DOWN_LEFT: return new Vector2Int(-1, -1);
			case Dir.LEFT: return new Vector2Int(-1, 0);
			case Dir.UP_LEFT: return new Vector2Int(-1, 1);
			default: return Vector2Int.zero;
		}
	}


	public override bool CanMove(Vector2Int pos)//움직일 수 있는지 판단하는 함수
	{
		CreateMap cmap = (GameSession.Instance != null && GameSession.Instance.cmap != null) ? GameSession.Instance.cmap : FindObjectOfType<CreateMap>();
		if (cmap == null || cmap.map.floors == null) return false;
		if (currentFloor < 0 || currentFloor >= cmap.map.floors.Length) return false;

		Floor floor = cmap.map.floors[currentFloor];
		if (floor.chunks == null) return false;

		int w = (int)unitType.footprint.x;
		int h = (int)unitType.footprint.y;

		for (int dx = 0; dx < w; dx++)
		{
			for (int dy = 0; dy < h; dy++)
			{
				int targetX = pos.x + dx;
				int targetY = pos.y + dy;

				int cx = targetX / 8;
				int tx = targetX % 8;
				int cy = targetY / 8;
				int cyVal = targetY % 8;

				if (cx < 0 || cx >= floor.config.width || cy < 0 || cy >= floor.config.height) return false;

				Chunks c = floor.chunks[cx, cy];
				if (c.roomId == -1 || c.chunk == null) return false;

				if (c.chunk[tx, cyVal].name == "Wall") return false;

				// 다른 유닛 점유 여부 확인 (최적화: O(1) 캐싱 배열)
				if (GameSession.Instance != null && GameSession.Instance.unitGrid.TryGetValue(new Vector3Int(targetX, targetY, currentFloor), out Unit u))
				{
					if (u != null && u != this && u.hp > 0)
					{
						return false;
					}
				}
			}
		}

		return true;
	}

	public override void Move(Dir dir)//움직이는 함수
	{
		currentDir = dir; // 이동 방향으로 시야 방향 갱신
		Vector2Int v = GetDirVector(dir);
		Vector2Int nextPos = position + v;

		if (CanMove(nextPos))
		{
			position = nextPos;
		}
	}


	protected void CastRay(FactionData myData, CreateMap cmap, Vector2Int startPos, float angleRad, float maxRadius, List<Unit> allUnits)//시야 레이캐스트
	{
		Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

		float rayPosX = startPos.x + 0.5f;
		float rayPosY = startPos.y + 0.5f;

		int x = startPos.x;
		int y = startPos.y;

		int stepX = dir.x > 0 ? 1 : (dir.x < 0 ? -1 : 0);
		int stepY = dir.y > 0 ? 1 : (dir.y < 0 ? -1 : 0);

		float tMaxX = dir.x != 0 ? Mathf.Abs(((dir.x > 0 ? x + 1 : x) - rayPosX) / dir.x) : float.PositiveInfinity;
		float tMaxY = dir.y != 0 ? Mathf.Abs(((dir.y > 0 ? y + 1 : y) - rayPosY) / dir.y) : float.PositiveInfinity;

		float tDeltaX = dir.x != 0 ? Mathf.Abs(1f / dir.x) : float.PositiveInfinity;
		float tDeltaY = dir.y != 0 ? Mathf.Abs(1f / dir.y) : float.PositiveInfinity;

		float dist = 0f;

		Floor floor = cmap.map.floors[currentFloor];
		if (floor.chunks == null) return;
		int mapWidth = floor.config.width * 8;
		int mapHeight = floor.config.height * 8;

		while (dist <= maxRadius)
		{
			if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) break;

			int cx = x / 8;
			int tx = x % 8;
			int cy = y / 8;
			int ty = y % 8;

			if (cx < 0 || cx >= floor.config.width || cy < 0 || cy >= floor.config.height) break;

			Chunks c = floor.chunks[cx, cy];
			if (c.roomId == -1 || c.chunk == null) break;

			Tile tile = c.chunk[tx, ty];
			myData.discoveredMap[currentFloor][x, y] = tile.name == "Wall" ? 2 : 1;

			// 유닛 발견 (O(1) 캐싱 검색 적용)
			if (GameSession.Instance != null && GameSession.Instance.unitGrid.TryGetValue(new Vector3Int(x, y, currentFloor), out Unit unit))
			{
				if (unit != null && unit != this && unit.hp > 0)
				{
					bool isEnemy = (this is Human && unit is Monster) || (this is Monster && unit is Human);
					if (isEnemy)
					{
						if (!personalSpottedEnemies.Contains(unit)) personalSpottedEnemies.Add(unit);
						if (this is Human && !myData.spottedEnemyUnits.Contains(unit))
						{
							myData.spottedEnemyUnits.Add(unit);
						}
					}
				}
			}
			/*=======아티팩트 관련 참조 주석처리========
			// 유물 발견
			if (ArtifactManager.Instance != null)
			{
				foreach(var art in ArtifactManager.Instance.artifacts)
				{
					if (!art.isPickedUp && art.floor == currentFloor && art.position.x == x && art.position.y == y)
					{
						if (!myData.spottedArtifacts.Contains(art)) myData.spottedArtifacts.Add(art);
					}
				}
			}*/

			// 가시성 체크 (본인 위치 제외)
			if (x != startPos.x || y != startPos.y)
			{
				int vis = tile.visibility;

				// visibility 데이터가 설정되지 않은 맵을 위한 예외처리
				if (vis == 0 && tile.name != "Wall") vis = 100;
				if (tile.name == "Wall") vis = 0;

				if (vis <= 0) break; // 시야 즉시 차단
				if (vis < 100)
				{
					// visibility 확률에 따른 시야 통과 여부 검사
					if (Random.Range(0, 100) >= vis)
					{
						break; // 시야 차단 막힘
					}
				}
			}

			// 다음 타일 이동
			if (tMaxX < tMaxY)
			{
				dist = tMaxX;
				tMaxX += tDeltaX;
				x += stepX;
			}
			else
			{
				dist = tMaxY;
				tMaxY += tDeltaY;
				y += stepY;
			}
		}
	}

	public override void UpdateFOV(List<Unit> allUnits)//시야 업데이트 함수
	{
		personalSpottedEnemies.Clear();
		FactionData myData = this is Human ? humanFactionData : monsterFactionData;
		Vector2 forward = GetDirVector(currentDir);
		if (forward == Vector2.zero) forward = Vector2.down;

		CreateMap cmap = (GameSession.Instance != null && GameSession.Instance.cmap != null) ? GameSession.Instance.cmap : FindObjectOfType<CreateMap>();
		if (cmap == null || cmap.map.floors == null) return;

		float fovAngle = 160f;

		float centerAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;

		// 방사형 레이캐스트 최적화 적용 (800 -> 72)
		int numRays = 72; // 최적화: 시야각 누락되지 않는 선에서 최대한 감소

		for (int i = 0; i <= numRays; i++)
		{
			float angle = centerAngle - (fovAngle / 2f) + (fovAngle * i / numRays);
			float rad = angle * Mathf.Deg2Rad;
			CastRay(myData, cmap, position, rad, ViewRadius, allUnits);
		}
	}
	#endregion

	public override void OnUpdate(float deltaTime)
	{
		if (stunDuration > 0f) stunDuration -= deltaTime;
		if (slowDuration > 0f) slowDuration -= deltaTime;

		if (poisonDuration > 0f)
		{
			poisonDuration -= deltaTime;
			hp -= 1f * deltaTime;
		}

		if (burnDuration > 0f)
		{
			burnDuration -= deltaTime;
			hp -= 1f * deltaTime;
		}

		if (hp > 0f)
		{
			float healAmount = HPRegen * deltaTime;
			hp = Mathf.Min(maxHp, hp + healAmount);
		}

		if (evadeCooldown > 0f)
		{
			evadeCooldown -= Time.deltaTime;
		}

		if (isCastingAttack)
		{
			castTimer -= deltaTime;
			if (castTimer <= 0f)
			{
				isCastingAttack = false;
				currentThreat = null;
				pendingAttack?.Invoke();
				pendingAttack = null;

				if (GameSession.Instance != null)
				{
					foreach (Unit u in GameSession.Instance.units)
					{
						if (u == null) continue;
						u.reactedAttackers.Remove(this);
					}
				}
			}
		}

		for (int i = 0; i < skillCooldowns.Length; i++)
		{
			if (skillCooldowns[i] > 0f)
			{
				skillCooldowns[i] -= deltaTime;
			}
		}

		List<ThreatTileData> detectedThreats = DetectThreats();
		if (detectedThreats.Count > 0)
		{
			OnThreatDetected(detectedThreats);
		}
	}

	public override void OnThreatDetected(List<ThreatTileData> threats)
	{
		if (stunDuration > 0f) return;

		foreach (var threat in threats)
		{
			Unit attacker = FindAttackerFromThreat(threat);
			if (attacker == null) continue;

			if (reactedAttackers.Contains(attacker)) continue;

			float reactionTimeMs = 30000f / Mathf.Max(1f, reaction);
			float reactionTimeSec = reactionTimeMs / 1000f;

			if (attacker.isCastingAttack)
			{
				if(attacker.castTimer >= reactionTimeSec)
				{
					reactedAttackers.Add(attacker);
					currentReactionWindow = reactionTimeSec;
					reactingThreat = threat;
					reactingAttacker = attacker;
					OnReactToThreat(attacker, threat);
				}
				else
				{
					reactedAttackers.Add(attacker);
					OnDirectHit(attacker, threat);
				}
			}
		}
	}

	public override void OnReactToThreat(Unit attacker, ThreatTileData threat)
	{
		Debug.Log($"{unitType.typeName} 반응 성공!");
		DefenseSystem.EvaluateDefense(this, attacker, threat);
	}

	public override void OnDirectHit(Unit attacker, ThreatTileData threat)
	{
		Debug.Log($"{unitType.typeName} 반응 실패 → 직격!");
		ApplyDirectDamage(attacker);
	}

	public override void ApplyDirectDamage(Unit attacker, float multiplier = 1f)
	{
		float raw = attacker.physicalAttack * multiplier;
		float damage = Mathf.Max(1f, raw - physicalDefense);
		hp -= damage;
		isHitThisTurn = true;
		if (UnitGenerate.Instance != null)
			UnitGenerate.Instance.TriggerHitEffect(this);
	}

	private Unit FindAttackerFromThreat(ThreatTileData threat)
	{
		foreach (Unit u in GameSession.Instance.units)
		{
			if (u == null) continue;
			if (!u.isCastingAttack) continue;
			if (u.currentThreat == threat)
				return u;
		}
		return null;
	}

	public override bool IsInThreat(Vector2Int pos, ThreatTileData threat)
	{
		if (threat.tiles == null) return false;
		return threat.tiles.Contains(pos);
	}

	public override List<ThreatTileData> DetectThreats()
	{
		List<ThreatTileData> result = new();

		foreach (Unit u in GameSession.Instance.units)
		{
			if (u == null || u == this) continue;
			if (u.currentFloor != currentFloor) continue;

			ThreatTileData threat = u.currentThreat;
			if (threat == null) continue;

			if (!u.isCastingAttack) continue;

			int w = (int)unitType.footprint.x;
			int h = (int)unitType.footprint.y;

			for (int dx = 0; dx < w; dx++)
				for (int dy = 0; dy < h; dy++)
				{
					Vector2Int p = new Vector2Int(position.x + dx, position.y + dy);

					if (threat.hitbox.Overlaps(Unit.GetUnitHitbox(this)))
					{
						result.Add(threat);
						goto NEXT;
					}
				}

		NEXT:;
		}

		return result;
	}

	public override bool RollCritical(bool canCritical)
	{
		if (!canCritical) return false;
		float roll = Random.Range(0f, 100f);
		return roll < criticalChance;
	}

	public override float ApplyCriticalDamage(float rawDamage)
	{
		return Mathf.Floor(rawDamage * 1.5f);
	}

	public virtual void DrawThreatTiles()
	{
		if (!isCastingAttack || currentThreat == null)
			return;

		Color color =
			this is Human
			? Color.cyan
			: Color.red;

		color.a = 0.8f;

		Vector3 floorOffset = Vector3.zero;

		if (UnitGenerate.Instance != null)
		{
			floorOffset =
				UnitGenerate.Instance.GetFloorOffset(currentFloor);
		}

		ThreatTileData threat = currentThreat;
		if (threat == null || threat.tiles == null) return;
		{ 
			foreach (Vector2Int tile in threat.tiles)
			{
				Vector3 p1 =
					new Vector3(tile.x, tile.y, 0f) + floorOffset;

				Vector3 p2 =
					new Vector3(tile.x + 1, tile.y, 0f) + floorOffset;

				Vector3 p3 =
					new Vector3(tile.x + 1, tile.y + 1, 0f) + floorOffset;

				Vector3 p4 =
					new Vector3(tile.x, tile.y + 1, 0f) + floorOffset;

				Debug.DrawLine(p1, p2, color);
				Debug.DrawLine(p2, p3, color);
				Debug.DrawLine(p3, p4, color);
				Debug.DrawLine(p4, p1, color);
			}
		}
	}
}
