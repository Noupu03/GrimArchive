using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Haare.Util.Logger;

public abstract class UnitFunction : Unit
{
	public override void TakeDamage(float damage)
	{
		float prevHp = hp;
		hp -= damage;
		isHitThisTurn = true;
		if (this.Generate != null) this.Generate.TriggerHitEffect(this);
	}

	public override void TakePhysicalDamage(float rawDamage, Unit attacker)
	{
		float damage = Mathf.Max(1f, rawDamage - physicalDefense);
		TakeDamage(damage);
		RecordHitWeightEvent(damage, attacker, rawDamage);
	}

	public override void TakeMagicalDamage(float rawDamage, Unit attacker)
	{
		float damage = Mathf.Max(1f, rawDamage - magicalDefense);
		TakeDamage(damage);
		RecordHitWeightEvent(damage, attacker, rawDamage);
	}

	// 대표 가중치 3종 연산공식 문서 3장/10장: 피격 이벤트를 이해도/위험도에 즉시 반영한다.
	// "직접 경험(SELF)"은 여기서 바로 연결하고, "직접 목격(SEEN)"은 GameSession.RecordKillWeightEvent의
	// 처치 목격과 동일한 근사(그 순간 생존한 다른 인류 전원이 목격한 것으로 처리)로 함께 연결한다.
	// 간접 파악(INDIRECT, 소리/전파)은 그 시스템 자체가 없어 여전히 미연결(구현현황 문서에 사유 기재).
	// public: DefenseSystem(정적 클래스, UnitFunction 밖)의 Block 판정도 hp를 직접 깎는 별도
	// 데미지 경로라 이 메서드를 그대로 재사용해서 연결한다.
	// rawDamage: 방어/저항 적용 전 원래 피해량(11-2장 "타격 1회별 기준 피해량" 판정에 필요 — 공격자가
	// 인류일 때는 안 쓰이므로 그 경우 호출부에서 아무 값이나 넘겨도 무방하다, 실제로는 appliedDamage와
	// 동일값을 넘긴다).
	public void RecordHitWeightEvent(float appliedDamage, Unit attacker, float rawDamage)
	{
		if (attacker == null || this.Knowledge == null) return;

		bool defenderIsHuman = this is Human;
		bool attackerIsHuman = attacker is Human;
		if (defenderIsHuman == attackerIsHuman) return; // 같은 진영끼리는 이 시스템의 대상이 아님

		string incidentId = System.Guid.NewGuid().ToString();
		lastAttacker = attacker;

		if (defenderIsHuman)
		{
			// 인류(this)가 몬스터(attacker)에게 맞음 — "일정 피해량 이상"만 위험도/이해도 증가 (3장 공통 규칙)
			if (appliedDamage >= heavyHitThreshold)
			{
				this.Knowledge.RecordEvent(EventId.E_HIT_HEAVY_SELF, this, attacker, InfoType.DirectExperience, incidentId);
				BroadcastWitnessEvent(EventId.E_HIT_HEAVY_SEEN, this, attacker, incidentId);
			}

			// 11-2장: 실제 적용 피해량이 공격자의 기준(방어 적용 전) 피해량보다 1 이상 낮으면
			// "인류의 방어력/저항 때문에 예상보다 약하게 들어간 타격"으로 보고 몬스터 종 위험도를
			// 소폭(-0.01, 전투당 최대 -1) 감소시킨다.
			this.Knowledge.ApplyPerHitDangerDecreaseCheck(attacker, rawDamage, appliedDamage);
		}
		else
		{
			// 인류(attacker)가 몬스터(this)를 때림 — 이해도만 오르고 위험도 변화는 없음(표 값 자체가 danger=0)
			this.Knowledge.RecordEvent(EventId.E_MONSTER_HIT_SELF, attacker, this, InfoType.DirectExperience, incidentId);
			BroadcastWitnessEvent(EventId.E_MONSTER_HIT_SEEN, attacker, this, incidentId);
		}
	}

	// 3장 "직접 목격(SEEN)" 계층 근사 구현 — 실제 FOV 기반 목격 판정(그 순간 그 자리를 보고
	// 있었는지)은 아직 없어서, GameSession.RecordKillWeightEvent와 동일하게 "그 순간 생존해 있는
	// 다른 인류 전원이 목격한 것"으로 근사한다. participant는 이미 SELF 이벤트로 기록된 당사자라
	// 중복 집계를 막기 위해 목격자 명단에서 제외한다.
	private void BroadcastWitnessEvent(EventId id, Unit participant, Unit target, string incidentId)
	{
		if (Session == null || this.Knowledge == null) return;
		foreach (var witness in Session.units)
		{
			if (witness == null || witness == participant || !(witness is Human) || witness.hp <= 0) continue;
			this.Knowledge.RecordEvent(id, witness, target, InfoType.DirectWitness, incidentId);
		}
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

	public override void ApplyStun(float duration)   { stunDuration   = Mathf.Max(stunDuration,   duration); RecordStatusWeightEvent(); }
	public override void ApplySlow(float duration)   { slowDuration   = Mathf.Max(slowDuration,   duration); RecordStatusWeightEvent(); }
	public override void ApplyPoison(float duration) { poisonDuration = Mathf.Max(poisonDuration, duration); RecordStatusWeightEvent(); }
	public override void ApplyBurn(float duration)   { burnDuration   = Mathf.Max(burnDuration,   duration); RecordStatusWeightEvent(); }

	// 3장 E_STATUS_SELF/SEEN: 상태이상 직접 경험/목격. 실제 게임에서 걸리는 상태이상은 현재 스턴뿐이라
	// (SkillAction/Projectile이 ApplyStun만 호출) 사실상 스턴 적용 시점에서만 발동하지만, 나중에
	// 슬로우/독/화상도 실제로 걸리기 시작하면 이 헬퍼를 그대로 타므로 별도 연결이 필요 없다.
	// lastAttacker는 SkillAction/Projectile이 항상 TakeDamage 계열을 먼저 호출한 뒤 Apply*를
	// 호출하는 순서 덕분에(RecordHitWeightEvent에서 세팅됨) 이 시점에 이미 정확한 가해자를 가리킨다.
	private void RecordStatusWeightEvent()
	{
		if (!(this is Human) || !(lastAttacker is Monster) || this.Knowledge == null) return;

		string incidentId = System.Guid.NewGuid().ToString();
		this.Knowledge.RecordEvent(EventId.E_STATUS_SELF, this, lastAttacker, InfoType.DirectExperience, incidentId);
		BroadcastWitnessEvent(EventId.E_STATUS_SEEN, this, lastAttacker, incidentId);
	}

	/// <summary>
	/// 공격 시 적을 향한 최적의 각도를 계산합니다 (자유 각도)
	/// </summary>
	public float CalculateAttackAngleToEnemy(Unit targetEnemy, int attackRange)
	{
		if (targetEnemy == null)
			return Mathf.Atan2(GetDirVector(currentDir).y, GetDirVector(currentDir).x);

		Vector2 dirToTarget = ((Vector2)targetEnemy.position - (Vector2)position).normalized;
		return Mathf.Atan2(dirToTarget.y, dirToTarget.x);
	}

	#region 기능함수들

	public override Vector2Int GetDirVector(Dir dir)
	{
		return dir switch
		{
			Dir.UP         => new Vector2Int(0,  1),
			Dir.UP_RIGHT   => new Vector2Int(1,  1),
			Dir.RIGHT      => new Vector2Int(1,  0),
			Dir.DOWN_RIGHT => new Vector2Int(1, -1),
			Dir.DOWN       => new Vector2Int(0, -1),
			Dir.DOWN_LEFT  => new Vector2Int(-1,-1),
			Dir.LEFT       => new Vector2Int(-1, 0),
			Dir.UP_LEFT    => new Vector2Int(-1, 1),
			_              => Vector2Int.zero
		};
	}

	public override bool CanMove(Vector2Int pos)
	{
		CreateMap cmap = (Session != null && Session.cmap != null)
			? Session.cmap
			: UnityEngine.Object.FindObjectOfType<CreateMap>();

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

				// C#의 정수 나눗셈/나머지는 0쪽으로 버림하므로(예: -1/8=0, -1%8=-1), 이 사전 체크
				// 없이 바로 나누면 음수 좌표가 cx/cy=0으로 잘못 계산되고 tx/cyVal이 음수가 되어
				// 아래 c.chunk[tx, cyVal] 인덱싱에서 IndexOutOfRangeException이 난다(맵 서쪽/북쪽
				// 경계에서 유닛이 바깥으로 이동을 시도할 때 실제로 재현된 크래시).
				if (targetX < 0 || targetY < 0) return false;

				int cx    = targetX / 8;
				int tx    = targetX % 8;
				int cy    = targetY / 8;
				int cyVal = targetY % 8;

				if (cx >= floor.config.width || cy >= floor.config.height) return false;

				Chunks c = floor.chunks[cx, cy];
				if (c.roomId == -1 || c.chunk == null) return false;
				if (c.chunk[tx, cyVal].name == "Wall") return false;

				if (Session != null &&
					Session.unitGrid.TryGetValue(new Vector3Int(targetX, targetY, currentFloor), out Unit u))
				{
					if (u != null && u != this && u.hp > 0) return false;
				}
			}
		}
		return true;
	}

	public override void Move(Dir dir)
	{
		currentDir = dir; // 이동 방향으로 시야 방향 갱신
		Vector2Int nextPos = position + GetDirVector(dir);

		if (CanMove(nextPos))
			position = nextPos;

		if (this.Generate != null)
			this.Generate.UpdateUnitSpriteForDirection(this);
	}

	protected void CastRay(FactionData myData, CreateMap cmap, Vector2Int startPos, float angleRad, float maxRadius, List<Unit> allUnits)
	{
		Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

		float rayPosX = startPos.x + 0.5f;
		float rayPosY = startPos.y + 0.5f;

		int x = startPos.x;
		int y = startPos.y;

		int stepX = dir.x > 0 ? 1 : (dir.x < 0 ? -1 : 0);
		int stepY = dir.y > 0 ? 1 : (dir.y < 0 ? -1 : 0);

		float tMaxX   = dir.x != 0 ? Mathf.Abs(((dir.x > 0 ? x + 1 : x) - rayPosX) / dir.x) : float.PositiveInfinity;
		float tMaxY   = dir.y != 0 ? Mathf.Abs(((dir.y > 0 ? y + 1 : y) - rayPosY) / dir.y) : float.PositiveInfinity;
		float tDeltaX = dir.x != 0 ? Mathf.Abs(1f / dir.x) : float.PositiveInfinity;
		float tDeltaY = dir.y != 0 ? Mathf.Abs(1f / dir.y) : float.PositiveInfinity;

		float dist = 0f;

		Floor floor = cmap.map.floors[currentFloor];
		if (floor.chunks == null) return;
		int mapWidth  = floor.config.width  * 8;
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
			bool tileIsWall = tile.name == "Wall";
			myData.discoveredMap[currentFloor][x, y] = tileIsWall ? 2 : 1;

			// 지형 밝히기 — FactionData.discoveredMap과 같은 정보(벽/바닥)를 인류 개인 지도에도
			// 기록한다. 몬스터 발견 여부와 무관하게 시야가 지나가는 모든 타일마다 갱신된다.
			if (this is Human terrainObserver)
			{
				var revealedTile = new Vector3Int(x, y, currentFloor);
				bool isFirstReveal = terrainObserver.personalMap.RevealTile(revealedTile, tileIsWall);
				bool isBossRoom = c.roomRole == RoomRole.BossRoom;
				// 3-2장 E_EXPLORED_SAFE_TILE: "탐사완료+안전확인 타일 → 흥미도 0"이 실제로 발생하는
				// 순간은 정확히 이 타일이 "처음" 밝혀지는 시점이다(미탐사 기본 흥미도 5 → 탐사완료
				// 기본 흥미도 0으로 전환). 매 프레임 다시 찍히면 안 되니 처음 밝힐 때만 로그.
				if (isFirstReveal)
					LogHelper.Log($"<b><color=blue>[EventId:E_EXPLORED_SAFE_TILE]</color></b>",
						$"관찰자={terrainObserver.name} 타일={revealedTile} 흥미도 미탐사({WeightMath.UnexploredTileBaseInterest})→탐사완료({WeightMath.ExploredTileBaseInterest})");

				// 20장/21장: 방 탐사 상태(Unexplored→Exploring→Complete). 바닥 타일을 "처음" 밝힐
				// 때만 카운트한다 — 매 프레임 다시 세면 총 타일 수(cmap.GetRoomFloorTileCount)를
				// 순식간에 넘겨버린다. 벽 타일은 셀 대상이 아니다(총 타일 수도 바닥만 셈).
				if (isFirstReveal && !tileIsWall)
				{
					int totalFloorTiles = cmap.GetRoomFloorTileCount(currentFloor, c.roomId);
					terrainObserver.personalMap.ObserveRoomTileRevealed(c.roomId, isBossRoom, totalFloorTiles);
				}

				if (Session != null && Session.objectGrid.TryGetValue(revealedTile, out InteractableObject obj))
				{
					// "처음 발견"인지는 수치(흥미도 등)로 추측하지 않고 IsObjectKnown으로 직접 확인한다
					// — 예전엔 "현재 흥미도<=5"로 추측했는데, base흥미도가 낮은 오브젝트나 조사로
					// 감쇠된 오브젝트를 다시 "새로 발견"으로 오판해 RegisterObject가 재호출되면서
					// 감쇠된 값이 기본값으로 되돌아가는 버그가 있었다(2026-07-08 수정).
					if (!obj.IsCollected && !terrainObserver.personalMap.IsObjectKnown(obj.Id))
					{
						// 15장(오브젝트 위험도 합성)/16장(오브젝트 흥미도 합성) 동시 등록.
						terrainObserver.personalMap.RegisterObject(obj.Id, obj.Position, obj.BaseDanger, obj.BaseInterest, obj.Tags, obj.CauserStage);
						LogHelper.Log($"<b><color=green>[EventId:E_INTEREST_OBJECT_FOUND]</color></b>",
							$"관찰자={terrainObserver.name} 타일={revealedTile} 오브젝트={obj.Id} 위험도={obj.BaseDanger} 흥미도={obj.BaseInterest}");

						// 20장/21장: 이 오브젝트가 있는 방의 "확인된 오브젝트" 목록에도 반영.
						terrainObserver.personalMap.ObserveObjectInRoom(c.roomId, isBossRoom, obj.Id, obj.BaseDanger, obj.BaseInterest);

						// 13-2장: 생환 파티가 전멸 흔적을 발견하면 동일 traceId당 1회만 던전 위험도에 반영.
						if (obj.Tags.Contains("WipeoutTrace") && !string.IsNullOrEmpty(obj.TraceId))
						{
							terrainObserver.Knowledge?.OnWipeoutTraceReflected(obj.TraceId);
							LogHelper.Log($"<b><color=red>[EventId:E_WIPEOUT_TRACE]</color></b>",
								$"관찰자={terrainObserver.name} 전멸흔적={obj.TraceId} 던전 위험도 전역 반영");
						}
					}
				}
			}

			if (Session != null &&
				Session.unitGrid.TryGetValue(new Vector3Int(x, y, currentFloor), out Unit unit))
			{
				if (unit != null && unit != this && unit.hp > 0)
				{
					bool isEnemy = (this is Human && unit is Monster) || (this is Monster && unit is Human);
					if (isEnemy)
					{
						// 인간 진영도 몬스터와 동일하게 개인 시야만 기록 — 진영 공유 시야(myData.spottedEnemyUnits) 제거.
						if (!personalSpottedEnemies.Contains(unit))
						{
							personalSpottedEnemies.Add(unit);

							// 지도는 인류만 들고 있다 — 인류가 몬스터를 발견한 시점에만 개인 지도에 기록.
							// GetFinalDanger/GetUnitInterest(전역 종/개체 누적)가 아니라 GetPersonalDanger/
							// GetPersonalInterest(이 관찰자의 personalWeights)를 쓴다 — 전역 값은 OnWaveEnd가
							// 있어야만 갱신되는데(6장) 웨이브 루프가 아직 없어 영원히 그대로다. personalWeights는
							// RecordEvent()가 호출되는 즉시(4장) 갱신되므로, 이걸 써야 실제 전투 이벤트에 맞춰
							// 개인 지도가 바로바로 반영된다.
							if (this is Human human && Knowledge != null)
							{
								var sightingTile = new Vector3Int(x, y, currentFloor);
								float danger = Knowledge.GetPersonalDanger(human, unit);
								float interest = Knowledge.GetPersonalInterest(human, unit);
								human.personalMap.ObserveMonster(unit.name, sightingTile, danger, interest);

								// 20장/21장: 이 몬스터가 서 있는 방의 "확인된 유닛" 목록에도 반영 — 방
								// 위험도/흥미도의 Exploring/Complete 단계 계산에 쓰인다.
								bool isBossRoom = c.roomRole == RoomRole.BossRoom;
								human.personalMap.ObserveUnitInRoom(c.roomId, isBossRoom, unit.name, danger, interest);
							}
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

			if (x != startPos.x || y != startPos.y)
			{
				int vis = tile.visibility;

				// visibility 데이터가 설정되지 않은 맵을 위한 예외처리
				if (vis == 0 && tile.name != "Wall") vis = 100;
				if (tile.name == "Wall") vis = 0;

				if (vis <= 0) break; // 시야 즉시 차단
				if (vis < 100 && Random.Range(0, 100) >= vis) break; // 시야 차단 막힘
			}

			if (tMaxX < tMaxY)
			{
				dist = tMaxX; tMaxX += tDeltaX; x += stepX;
			}
			else
			{
				dist = tMaxY; tMaxY += tDeltaY; y += stepY;
			}
		}
	}

	public override void UpdateFOV(List<Unit> allUnits)
	{
		personalSpottedEnemies.Clear();
		FactionData myData = this is Human ? humanFactionData : monsterFactionData;
		Vector2 forward = GetDirVector(currentDir);
		if (forward == Vector2.zero) forward = Vector2.down;

		CreateMap cmap = (Session != null && Session.cmap != null)
			? Session.cmap
			: UnityEngine.Object.FindObjectOfType<CreateMap>();
		if (cmap == null || cmap.map.floors == null) return;

		float fovAngle   = 160f;
		float centerAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;

		// 방사형 레이캐스트 최적화 적용 (800 -> 72)
		int numRays = 72; // 최적화: 시야각 누락되지 않는 선에서 최대한 감소

		for (int i = 0; i <= numRays; i++)
		{
			float angle = centerAngle - (fovAngle / 2f) + (fovAngle * i / numRays);
			float rad   = angle * Mathf.Deg2Rad;
			CastRay(myData, cmap, position, rad, ViewRadius, allUnits);
		}
	}

	#endregion

	public override void OnUpdate(float deltaTime)
	{
		if (stunDuration   > 0f) stunDuration   -= deltaTime;
		if (slowDuration   > 0f) slowDuration   -= deltaTime;
		if (poisonDuration > 0f) { poisonDuration -= deltaTime; hp -= 1f * deltaTime; }
		if (burnDuration   > 0f) { burnDuration   -= deltaTime; hp -= 1f * deltaTime; }

		// 15장: 안전 확인 시간 진행 — 이 유닛(개인 지도 소유자)이 위험도를 기록해 둔 타일마다,
		// 지금 그 타일에 몬스터가 실제로 있는지 확인해서 있으면 타이머를 리셋하고 없으면 흘려보낸다.
		// 매 프레임 도는 OnUpdate에 걸어서 real deltaTime을 쓴다(ProcessUnitAction의 actionCooldown
		// 주기와 달리 여긴 걸음 속도와 무관하게 매 프레임 호출됨).
		if (this is Human human && Session != null)
		{
			foreach (var tile in human.personalMap.KnownDangerTiles.ToList())
			{
				bool threatPresent = Session.unitGrid.TryGetValue(tile, out Unit occupant) && occupant is Monster && occupant.hp > 0f;
				human.personalMap.TickTileSafety(tile, threatPresent, deltaTime);
			}

			// 16장(v0.7 (1) 개정판): 흥미도 확인 시간 진행 — "타일에 흥미도 있는 오브젝트가
			// 있는가"는 PersonalMapKnowledge 안에서 자기완결적으로 판단 가능해 GameSession 조회가
			// 필요 없다(TickTileSafety의 threatPresent와 달리 인자로 안 넘김).
			foreach (var tile in human.personalMap.KnownInterestTiles.ToList())
			{
				human.personalMap.TickTileInterestConfirm(tile, deltaTime);
			}
		}

		if (hp > 0f)
			hp = Mathf.Min(maxHp, hp + HPRegen * deltaTime);

		if (evadeCooldown > 0f)
			evadeCooldown -= Time.deltaTime;

		if (isCastingAttack)
		{
			pendingCastUpdate?.Invoke();
			
			castTimer -= deltaTime;
			if (castTimer <= 0f)
			{
				// 공격 타이밍: 반응한 유닛의 VFX(가드·패링) 실행
				if (Session != null)
				{
					foreach (Unit u in Session.units)
					{
						if (u == null || u.reactingAttacker != this) continue;
						u.pendingVFX?.Invoke();
						u.pendingVFX = null;
					}
				}

				isCastingAttack = false;
				currentThreat   = null;
				pendingAttack?.Invoke();
				pendingAttack   = null;
				pendingCastUpdate = null;

				if (Session != null)
				{
					foreach (Unit u in Session.units)
					{
						if (u == null) continue;
						u.reactedAttackers.Remove(this);
						if (u.reactingAttacker == this)
						{
							u.reactingAttacker = null;
							u.reactingThreat   = null;
						}
					}
				}
			}
		}

		for (int i = 0; i < skillCooldowns.Length; i++)
		{
			if (skillCooldowns[i] > 0f) skillCooldowns[i] -= deltaTime;
		}

		List<ThreatTileData> detectedThreats = DetectThreats();
		if (detectedThreats.Count > 0) OnThreatDetected(detectedThreats);
	}

	public override void OnThreatDetected(List<ThreatTileData> threats)
	{
		if (stunDuration > 0f) return;

		foreach (var threat in threats)
		{
			Unit attacker = FindAttackerFromThreat(threat);
			if (attacker == null) continue;
			if (reactedAttackers.Contains(attacker)) continue;

			float reactionTimeMs  = 30000f / Mathf.Max(1f, reaction);
			float reactionTimeSec = reactionTimeMs / 1000f;

			if (attacker.isCastingAttack)
			{
				if (attacker.castTimer >= reactionTimeSec)
				{
					reactedAttackers.Add(attacker);
					currentReactionWindow = reactionTimeSec;
					reactingThreat        = threat;
					reactingAttacker      = attacker;
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
		DefenseSystem.EvaluateDefense(this, attacker, threat);
	}

	public override void OnDirectHit(Unit attacker, ThreatTileData threat)
	{
		ApplyDirectDamage(attacker);
	}

	public override void ApplyDirectDamage(Unit attacker, float multiplier = 1f)
	{
		float raw    = attacker.physicalAttack * multiplier;
		float damage = Mathf.Max(1f, raw - physicalDefense);
		hp -= damage;
		isHitThisTurn = true;
		if (this.Generate != null)
			this.Generate.TriggerHitEffect(this);

		// 실제 근접 위협/반응 시스템(OnDirectHit, DefenseSystem의 방어 실패/닷지 실패/블링크 실패/
		// 패링 반격)이 데미지를 주는 진짜 경로인데, TakePhysicalDamage를 거치지 않고 hp를 직접 깎고
		// 있어서 RecordHitWeightEvent가 한 번도 호출되지 않고 있었다 — 여기서도 동일하게 연결한다.
		RecordHitWeightEvent(damage, attacker, raw);
	}

	private Unit FindAttackerFromThreat(ThreatTileData threat)
	{
		foreach (Unit u in Session.units)
		{
			if (u == null) continue;
			if (!u.isCastingAttack) continue;
			if (u.currentThreat == threat) return u;
		}
		return null;
	}

	public override bool IsInThreat(Vector2Int pos, ThreatTileData threat)
	{
		if (threat.hitbox.size == Vector2.zero) return false;

		Hitbox posHitbox = new Hitbox
		{
			center = (Vector2)pos + Vector2.one * 0.5f,
			size   = Vector2.one
		};
		return threat.hitbox.Overlaps(posHitbox);
	}

	public override List<ThreatTileData> DetectThreats()
	{
		List<ThreatTileData> result = new List<ThreatTileData>();

		foreach (Unit u in Session.units)
		{
			if (u == null || u == this) continue;
			if (u.currentFloor != currentFloor) continue;

			ThreatTileData threat = u.currentThreat;
			if (threat == null || !u.isCastingAttack) continue;

			if (threat.hitbox.Overlaps(Unit.GetUnitHitbox(this)))
				result.Add(threat);
		}
		return result;
	}

	public override bool RollCritical(bool canCritical)
	{
		if (!canCritical) return false;
		return Random.Range(0f, 100f) < criticalChance;
	}

	public override float ApplyCriticalDamage(float rawDamage) => Mathf.Floor(rawDamage * 1.5f);

	public virtual void DrawThreatTiles()
	{
		if (!isCastingAttack || currentThreat == null) return;

		Color color = this is Human ? Color.cyan : Color.red;
		color.a = 0.8f;

		Vector3 floorOffset = this.Generate != null
			? this.Generate.GetFloorOffset(currentFloor)
			: Vector3.zero;

		ThreatTileData threat = currentThreat;
		if (threat == null || threat.hitbox.size == Vector2.zero) return;

		Hitbox  box      = threat.hitbox;
		Vector2 center   = box.center;
		Vector2 halfSize = box.size * 0.5f;

		Vector3 p1 = new Vector3(center.x - halfSize.x, center.y - halfSize.y, 0f) + floorOffset;
		Vector3 p2 = new Vector3(center.x + halfSize.x, center.y - halfSize.y, 0f) + floorOffset;
		Vector3 p3 = new Vector3(center.x + halfSize.x, center.y + halfSize.y, 0f) + floorOffset;
		Vector3 p4 = new Vector3(center.x - halfSize.x, center.y + halfSize.y, 0f) + floorOffset;

		Debug.DrawLine(p1, p2, color);
		Debug.DrawLine(p2, p3, color);
		Debug.DrawLine(p3, p4, color);
		Debug.DrawLine(p4, p1, color);
	}
}
