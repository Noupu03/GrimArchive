using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Haare.Util.Logger;

public abstract class UnitFunction : Unit, IVisionContext
{
	public new GameSession Session => base.Session;
	public override void TakeDamage(float damage)
	{
		float prevHp = Health.hp;
		Health.hp -= damage;
		CombatState.State.isHitThisTurn = true;
		if (this.Generate != null) this.Generate.TriggerHitEffect(this);
	}

	public override void TakePhysicalDamage(float rawDamage, Unit attacker)
	{
		if (attacker != null)
		{
			lastDamageDealer = attacker;
			rawDamage = DefenseSystem.EvaluateImpactDefense(this, attacker, rawDamage);
		}

		if (rawDamage > 0f)
		{
			float damage = Mathf.Max(1f, rawDamage - CombatStat.physicalDefense);
			TakeDamage(damage);
			RecordHitWeightEvent(damage, attacker, rawDamage);
		}
	}

	public override void TakeMagicalDamage(float rawDamage, Unit attacker)
	{
		if (attacker != null)
		{
			lastDamageDealer = attacker;
			rawDamage = DefenseSystem.EvaluateImpactDefense(this, attacker, rawDamage);
		}

		if (rawDamage > 0f)
		{
			float damage = Mathf.Max(1f, rawDamage - CombatStat.magicalDefense);
			TakeDamage(damage);
			RecordHitWeightEvent(damage, attacker, rawDamage);
		}
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

		string incidentId = (++_incidentIdCounter).ToString();
		lastAttacker = attacker;
		lastTrapAttacker = null; // 4-14장: 몬스터 피격이 더 최근이면 함정 원인 기록을 덮어써 무효화한다.

		if (defenderIsHuman)
		{
			// 02문서 17장: "피격 사실/피해량은 확정되지만 공격자 정체는 별도 인지 판정이 필요하다."
			// Health.hp 차감(TakeDamage)은 이미 위에서 확정됐고, 여기서는 공격자를 "누구"로 특정해 기록할
			// 이벤트만 인지 판정으로 게이팅한다 — IsAttackerIdentified가 4장 조건5(피격 시 공격 후
			// 가시성 증가를 반영한 즉시 재판정)를 강제로 수행한다.
			bool attackerIdentified = IsAttackerIdentified(attacker);
			if (attackerIdentified)
			{
				// "일정 피해량 이상"만 위험도/이해도 증가 (3장 공통 규칙)
				if (appliedDamage >= BaseStat.heavyHitThreshold)
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
				// 03문서 4-1/4-10/4-11/4-12장: 공격자 정체를 인지하지 못한 피격 — 경계 상태로 전환해
				// 수색을 시작한다. 공격 방향까지 인지했는지는 별도 판정 공식이 없어(02문서는 "정체"만
				// 게이팅하고 "방향"은 규정하지 않음) 인지 범위 안이면 방향도 안다고 근사한다(4-10장
				// 방향 수색으로 이어짐, 범위 밖이면 4-11장 주변 수색). 추가 공격이 오면 수색시간을
				// 15초로 재설정한다(4-12장) — 매번 새 레코드로 덮어써 자동으로 재설정된다.
				bool directionKnown = Vector2.Distance(position, attacker.position) <= VisionMath.AwarenessDistance(spotting);
				currentAlertSearch = new AlertSearchState
				{
					TargetPosition = directionKnown ? new Vector2Int(attacker.position.x, attacker.position.y) : (Vector2Int?)null,
				};
			}
		}
		else
		{
			// 인류(attacker)가 몬스터(this)를 때림 — 이해도만 오르고 위험도 변화는 없음(표 값 자체가 danger=0)
			// 02문서 4장 조건5/26장("인지 판정: 인류/몬스터 공통 사용"): 이 RecordEvent 자체는 게이팅
			// 대상이 아니다(attacker=인류가 이미 자기 공격 대상을 스스로 알고 있음) — 다만 몬스터(this)
			// 쪽 인지 판정도 인류와 동일하게 "피격 시 재판정" 트리거를 받아야 하므로, 게이팅 없이
			// 재판정만 수행해 둔다(이후 CastRay/GOAP의 Perception.State.personalSpottedEnemies 등에 반영될 수 있게).
			ForceReidentifyAttacker(attacker);
			this.Knowledge.RecordEvent(EventId.E_MONSTER_HIT_SELF, attacker, this, InfoType.DirectExperience, incidentId);
			BroadcastWitnessEvent(EventId.E_MONSTER_HIT_SEEN, attacker, this, incidentId);
		}
	}

	// 02문서 4장 조건5: "인지 범위 안에 있으나 미인식/수상한 타일인 대상이 공격하면, 공격 후 가시성
	// 증가를 반영해 즉시 재판정한다." 이 규칙 자체는 26장 표(인지 판정: 인류/몬스터 공통 사용)에 따라
	// 관찰자가 인류든 몬스터든 동일하게 적용된다 — attacker에 대한 perceptionRecords가 여기서 갱신된다.
	// 벽/차단 오브젝트에 의한 시야 차단(LOS)까지는 재확인하지 않는다 — 근접 공격자는 인접 타일에서만
	// 발생해 사실상 항상 차단이 없고, 원거리/마법 공격자를 위해 CastRay와 동일한 레이마칭을 매 피격마다
	// 다시 도는 것은 이번 범위에 비해 과한 비용이라 거리+인지각만으로 판정한다(판단 근거: 구현현황 문서).
	private void ForceReidentifyAttacker(Unit attacker)
	{
		if (attacker == null) return;

		float dist = Vector2.Distance(position, attacker.position);
		float effectiveSpotting = VisionStat.spotting + (Perception.IsAlert ? PerceptionMath.AlertDetectionBonus : 0f); // 02문서 10장: 경계 중 감지 보정
		float perceptionDistance = VisionMath.AwarenessDistance(effectiveSpotting);
		float perceptionAngle = VisionMath.AwarenessAngle(effectiveSpotting);

		Vector2 forward = GetDirVector(currentDir);
		if (forward == Vector2.zero) forward = Vector2.down;
		float centerAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
		float angleToAttackerDeg = Mathf.Atan2(attacker.position.y - position.y, attacker.position.x - position.x) * Mathf.Rad2Deg;
		bool inAngle = Mathf.Abs(Mathf.DeltaAngle(centerAngle, angleToAttackerDeg)) <= perceptionAngle / 2f;

		if (dist > perceptionDistance || !inAngle) return; // 5장 조건1: 인지 범위 밖 — 판정 자체 불가.
		if (IsFullyBlockedTowards(angleToAttackerDeg * Mathf.Deg2Rad, dist)) return; // 01-A 8장 조건2: 완전 차단 구조 뒤.

		Vector3Int tile = new Vector3Int(attacker.position.x, attacker.position.y, attacker.currentFloor);
		ForceRollPerception(attacker, attacker.GetFinalVisibility(), tile, PerceptionTargetKind.EnemyUnit);
	}

	// 01-A 8장 조건2 전용 최소 LOS 체크 — CastRay의 DDA 레이마칭과 동일한 차단 기준(벽 타일/
	// InteractableObject.IsFullyBlocking)만 재현한다. 지형 밝히기/오브젝트 발견 등 CastRay의 부수효과는
	// 전혀 일으키지 않는 순수 판정 함수.
	private bool IsFullyBlockedTowards(float angleRad, float maxDistance)
	{
		CreateMap cmap = (Session != null && Session.cmap != null) ? Session.cmap : null;
		if (cmap == null || cmap.map.floors == null) return false;
		if (currentFloor < 0 || currentFloor >= cmap.map.floors.Length) return false;
		Floor floor = cmap.map.floors[currentFloor];
		if (floor.chunks == null) return false;

		Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
		float rayPosX = position.x + 0.5f, rayPosY = position.y + 0.5f;
		int x = position.x, y = position.y;
		int stepX = dir.x > 0 ? 1 : (dir.x < 0 ? -1 : 0);
		int stepY = dir.y > 0 ? 1 : (dir.y < 0 ? -1 : 0);
		float tMaxX = dir.x != 0 ? Mathf.Abs(((dir.x > 0 ? x + 1 : x) - rayPosX) / dir.x) : float.PositiveInfinity;
		float tMaxY = dir.y != 0 ? Mathf.Abs(((dir.y > 0 ? y + 1 : y) - rayPosY) / dir.y) : float.PositiveInfinity;
		float tDeltaX = dir.x != 0 ? Mathf.Abs(1f / dir.x) : float.PositiveInfinity;
		float tDeltaY = dir.y != 0 ? Mathf.Abs(1f / dir.y) : float.PositiveInfinity;

		int mapWidth = floor.config.width * 8;
		int mapHeight = floor.config.height * 8;
		float dist = 0f;

		while (dist < maxDistance)
		{
			if (tMaxX < tMaxY) { dist = tMaxX; tMaxX += tDeltaX; x += stepX; }
			else { dist = tMaxY; tMaxY += tDeltaY; y += stepY; }
			if (dist >= maxDistance) break; // 공격자 자신이 서 있는 타일은 차단 판정에서 제외.

			if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return true;
			int cx = x / 8, tx = x % 8, cy = y / 8, ty = y % 8;
			if (cx < 0 || cx >= floor.config.width || cy < 0 || cy >= floor.config.height) return true;
			Chunks c = floor.chunks[cx, cy];
			if (c.roomId == -1 || c.chunk == null) return true;

			Tile tile = c.chunk[tx, ty];
			Vector3Int tilePos = new Vector3Int(x, y, currentFloor);
			// 문 닫힘 시스템(2026-07-28 재정정, 사용자 요청 "문이 닫혀버리면, 벽과 같은 가시성을 가지게
			// 하고, 벽처럼 아예 이동 불가하게 해줘") — 인류가 문(isStructureExist)에 안 막히던 예외를
			// 없앤다. 닫힌 문은 이제 어느 진영이든 예외 없이 벽과 동일하게 취급한다.
			if (tile.name == "Wall" || tile.isStructureExist) return true;

			if (Session != null && Session.objectGrid.TryGetValue(tilePos, out InteractableObject blocker) &&
				!blocker.IsCollected && blocker.IsFullyBlocking)
			{
				return true;
			}
		}
		return false;
	}

	// 02문서 17장: 피격 대상(this)이 attacker의 정체를 "가중치 이벤트에 쓸 만큼" 확인했는지. 가중치
	// 시스템의 정체 기반 이벤트 자체가 인류 전용(26장)이라 이 게이팅 판정은 인류 관찰자 전용이다 —
	// 재판정 자체(ForceReidentifyAttacker)는 인류/몬스터 공통으로 이미 수행됐다.
	private bool IsAttackerIdentified(Unit attacker)
	{
		ForceReidentifyAttacker(attacker);
		if (!(this is Human)) return true; // 게이팅은 인류 관찰자 전용
		return IsCurrentlyIdentified(attacker);
	}

	// 방금 IsAttackerIdentified/ResolveReachedTarget으로 판정된 기록을 다시 굴리지 않고 그대로 조회만
	// 한다 — RecordStatusWeightEvent는 RecordHitWeightEvent와 같은 피격 시퀀스 안에서 호출되므로(4장:
	// 같은 트리거를 또 재판정하지 않는다) 새 판정이 아니라 조회여야 한다.
	private bool IsCurrentlyIdentified(Unit target)
		=> target != null && Perception.State.perceptionRecords.TryGetValue(target, out var record) && record.Outcome == PerceptionOutcome.AccuratePerception;

	// 3장 "직접 목격(SEEN)" 계층 근사 구현 — 실제 FOV 기반 목격 판정(그 순간 그 자리를 보고
	// 있었는지)은 아직 없어서, GameSession.RecordKillWeightEvent와 동일하게 "그 순간 생존해 있는
	// 다른 인류 전원이 목격한 것"으로 근사한다. participant는 이미 SELF 이벤트로 기록된 당사자라
	// 중복 집계를 막기 위해 목격자 명단에서 제외한다.
	private void BroadcastWitnessEvent(EventId id, Unit participant, Unit target, string incidentId)
	{
		if (Session == null || this.Knowledge == null) return;
		foreach (var witness in Session.units)
		{
			if (witness == null || witness == participant || !(witness is Human) || witness.Health.hp <= 0) continue;
			this.Knowledge.RecordEvent(id, witness, target, InfoType.DirectWitness, incidentId);
		}
	}

	public override void TakeMentalDamage(float rawDamage, Unit attacker)
	{
		if (this is Human)
		{
			int prevStage = Mathf.FloorToInt(BaseStat.mental / (BaseStat.maxMental * 0.25f));
			BaseStat.mental -= rawDamage; // 정신력만 감소
			int currentStage = Mathf.FloorToInt(BaseStat.mental / (BaseStat.maxMental * 0.25f));
			CombatState.State.isHitThisTurn = true;
		}
	}

	public override void ApplyStun(float duration)   { StatusEffects.State.stunDuration   = Mathf.Max(StatusEffects.State.stunDuration,   duration); RecordStatusWeightEvent(); }
	public override void ApplySlow(float duration)   { StatusEffects.State.slowDuration   = Mathf.Max(StatusEffects.State.slowDuration,   duration); RecordStatusWeightEvent(); }
	public override void ApplyPoison(float duration) { StatusEffects.State.poisonDuration = Mathf.Max(StatusEffects.State.poisonDuration, duration); RecordStatusWeightEvent(); }
	public override void ApplyBurn(float duration)   { StatusEffects.State.burnDuration   = Mathf.Max(StatusEffects.State.burnDuration,   duration); RecordStatusWeightEvent(); }

	// 3장 E_STATUS_SELF/SEEN: 상태이상 직접 경험/목격. 실제 게임에서 걸리는 상태이상은 현재 스턴뿐이라
	// (SkillAction/Projectile이 ApplyStun만 호출) 사실상 스턴 적용 시점에서만 발동하지만, 나중에
	// 슬로우/독/화상도 실제로 걸리기 시작하면 이 헬퍼를 그대로 타므로 별도 연결이 필요 없다.
	// lastAttacker는 SkillAction/Projectile이 항상 TakeDamage 계열을 먼저 호출한 뒤 Apply*를
	// 호출하는 순서 덕분에(RecordHitWeightEvent에서 세팅됨) 이 시점에 이미 정확한 가해자를 가리킨다.
	private void RecordStatusWeightEvent()
	{
		if (!(this is Human) || !(lastAttacker is Monster) || this.Knowledge == null) return;
		// 02문서 17장: 상태이상을 건 공격자의 정체도 동일하게 게이팅한다 — RecordHitWeightEvent가 같은
		// 피격 시퀀스에서 이미 판정을 굴려놨으므로 여기서는 그 결과만 조회한다(재판정 아님).
		if (!IsCurrentlyIdentified(lastAttacker)) return;

		string incidentId = (++_incidentIdCounter).ToString();
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

	public override bool CanMove(Vector2Int pos, bool ignoreUnits = false)
	{
		CreateMap cmap = (Session != null && Session.cmap != null) ? Session.cmap : null;

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

				if (targetX < 0 || targetY < 0) return false;

				int cx    = targetX / 8;
				int tx    = targetX % 8;
				int cy    = targetY / 8;
				int cyVal = targetY % 8;

				if (cx >= floor.config.width || cy >= floor.config.height) return false;

				Chunks c = floor.chunks[cx, cy];
				if (c.roomId == -1 || c.chunk == null) return false;
				Tile moveTile = c.chunk[tx, cyVal];
				// 문 닫힘 시스템(2026-07-28 재정정, 사용자 요청 "문이 닫혀버리면... 벽처럼 아예 이동
				// 불가하게 해줘. 지금 플레이어 지정 명령으로 이동이 되어버려") — 인류가 문(isStructureExist)
				// 에 안 막히던 예외(2026-07-28 앞선 요청 "인류는 문에 안 막혀야")를 없앤다. 닫힌 문은
				// 이제 진영·명령 종류(AI 자율 이동/플레이어 지정 명령) 구분 없이 예외 없이 벽과 동일하게
				// 막는다 — 인류 자율 탐색이 닫힌 문 앞에서 다시 멈추는 건 의도된 트레이드오프(방을
				// 정리해야 문이 열리는 규칙을 인류에게도 예외 없이 적용).
				if (moveTile.name == "Wall" || moveTile.isStructureExist) return false;

				if (!ignoreUnits && Session != null &&
					Session.unitGrid.TryGetValue(new Vector3Int(targetX, targetY, currentFloor), out Unit u))
				{
					if (u != null && u != this && u.Health.hp > 0) return false;
				}


			}
		}
		return true;
	}

	public override void Move(Dir dir)
	{
		Vector2Int dirVec = GetDirVector(dir);
		Vector2Int nextPos = position + dirVec;

		bool canMove = CanMove(nextPos);

		// 코너 커팅(벽 뚫기) 방지: 대각선 이동 시 양옆 직교 타일 중 하나라도 이동 불가면 블록
		if (canMove && Mathf.Abs(dirVec.x) == 1 && Mathf.Abs(dirVec.y) == 1)
		{
			if (!CanMove(position + new Vector2Int(dirVec.x, 0)) ||
				!CanMove(position + new Vector2Int(0, dirVec.y)))
			{
				canMove = false;
			}
		}

		// 실제로 이동에 성공했을 때만 시야 방향을 갱신한다 — 다른 유닛이 길을 막아 매 틱 CanMove가
		// 실패하는 동안에도 예전엔 시도한 방향으로 currentDir이 계속 바뀌어서(웨이브 파티가 몰리는
		// 구간에서 서로 자리를 다투며 시도 방향이 틱마다 흔들림), 제자리에서 캐릭터만 계속 홱홱 도는
		// 것처럼 보였다(사용자 신고, 2026-07-23 "얘내 자꾸 움찔움찔 거리는데, 유닛으로 인한 길 막힘
		// 때문인듯"). 막혀서 못 움직인 틱엔 기존 방향을 그대로 유지해 제자리에 가만히 서 있게 한다.
		if (canMove)
		{
			currentDir = dir;
			position = nextPos;

			// 4-6장: 이 유닛을 "수상한 타일" 대상으로 추적 중인 적(관찰자)이 하나라도 있으면, 이동
			// 1회마다 이 유닛 자신의 가시성을 임시로 +20 늘리는 타이머를 하나 push한다(각 타이머는
			// 5초 뒤 개별 소멸 — OnUpdate에서 감쇠).
			if (IsTrackedAsSuspiciousByAnyEnemy())
				VisionStat.suspiciousMoveBoostTimers.Enqueue(UnityEngine.Time.time + VisionMath.SuspiciousMoveBoostDurationSeconds);
		}

		if (this.Generate != null)
			this.Generate.UpdateUnitSpriteForDirection(this);
	}

	// 4-6장 트리거 판정 — Session 전체를 순회해 "나를 적으로 보는 유닛 중 지금 나를 수상한 타일로
	// 추적 중인 관찰자가 있는가"를 확인한다. 관찰자별로 다른 값을 주는 대신(07 전파 문서 부재로 이번
	// 구현에서도 단순화) 이 유닛 자신의 가시성 하나에 반영해 모든 관찰자에게 동일하게 적용한다.
	// J: ForceRollPerception이 PendingSuspiciousInvestigation 변경 시마다 _suspiciousObserverCount를
	// 증감하므로 O(N) 순회 없이 O(1)로 판정한다.
	private bool IsTrackedAsSuspiciousByAnyEnemy() => _suspiciousObserverCount > 0;

	// ─────────────────────── 02문서 4장: 트리거 기반 지속 인지 상태 ───────────────────────
	// 이번 UpdateFOV 패스에서 인지 범위 안으로 실제 도달한 대상(적 유닛=Unit 참조, 오브젝트=Id)의
	// 집합 — UpdateFOV 시작 시 비우고, CastRay가 레이를 쏘며 채운다. 이 패스가 끝난 뒤(모든 레이 +
	// 특수 원형 스윕 완료 후) 이 집합에 없는 기존 perceptionRecords는 "이번엔 못 봤다"로 판정해
	// PerceptionRecord.WasInRange를 false로 내린다 — 그래야 나중에 다시 보였을 때(재진입/완전 차단
	// 후 재등장 모두 포함) 새 트리거로 인식돼 재판정이 걸린다(4장 조건1~3이 전부 "지금 안 보이다가
	// 다시 보임"이라는 동일 신호라 이 하나의 메커니즘으로 셋 다 커버된다).
	private readonly Dictionary<object, (float dist, Vector3Int tile)> _reachedPerceptionThisPass = new Dictionary<object, (float, Vector3Int)>();

	// I: ResolveVisionDirection이 매 틱 new List<>()를 할당하던 것을 제거 — 재사용 필드로 교체한다.
	private readonly List<VisionMath.VisionDirectionCandidate> _visionDirectionCandidates = new List<VisionMath.VisionDirectionCandidate>();
	// ⑩: Guid.NewGuid().ToString() 대신 단조 증가 카운터로 incidentId 생성 — 문자열 1회 할당으로 감소.
	private static int _incidentIdCounter;
	// ⑨: KnownDangerTiles/KnownInterestTiles.ToList()가 0.1초마다 List를 새로 할당하던 것을 제거.
	private readonly List<Vector3Int> _dangerTilesCopy = new List<Vector3Int>();
	private readonly List<Vector3Int> _interestTilesCopy = new List<Vector3Int>();

	// 이번 패스에 처음 도달한 대상이면 트리거 조건(최초 진입/재진입/수상한 타일 2칸 재접근)을 검사해
	// 필요하면 재판정하고, 이미 이번 패스에 다른 레이로 처리된 대상이면 그 결과를 그대로 반환한다
	// (여러 레이가 같은 타일에 도달해도 판정은 패스당 한 번만 — firstTouchThisPass로 호출부가 후속
	// 처리(등록 등)를 중복 실행하지 않도록 알려준다).
	private PerceptionOutcome ResolveReachedTarget(object key, float targetVisibility, Vector3Int tile, float dist, PerceptionTargetKind kind, out bool firstTouchThisPass)
	{
		firstTouchThisPass = !_reachedPerceptionThisPass.ContainsKey(key);
		_reachedPerceptionThisPass[key] = (dist, tile);
		if (!firstTouchThisPass)
			return Perception.State.perceptionRecords.TryGetValue(key, out var already) ? already.Outcome : PerceptionOutcome.Unrecognized;

		Perception.State.perceptionRecords.TryGetValue(key, out var existing);

		// 4장 조건1/2/3: 기록이 없거나(최초 진입) 직전 패스엔 도달하지 못했던(재진입/차단 후 재등장) 대상.
		bool isNewOrReentering = existing == null || !existing.WasInRange;
		// 4장 조건4/22장: 수상한 타일 확인 대기 중 + 2칸 이내로 접근.
		bool suspiciousReapproach = existing != null && existing.PendingSuspiciousInvestigation
			&& dist <= PerceptionMath.SuspiciousTileReapproachDistanceTiles;

		// 5장(인지 판정 불가 상태)은 ForceRollPerception 내부에서 일괄 가드한다 — 트리거가 걸려도
		// 실제로 굴리지 않고 기존 기록을 동결한 채 반환한다(existing==null이어도 새 레코드만 만들고
		// 굴리지 않음).
		if (isNewOrReentering || suspiciousReapproach)
			return ForceRollPerception(key, targetVisibility, tile, kind);

		existing.WasInRange = true;
		existing.LastKnownTile = tile;
		return existing.Outcome;
	}

	// 8장/12장: 감지 보정(경계 반영) + 정신력 보정을 더한 최종 계산 가시성으로 확률표를 굴려 즉시
	// 판정을 갱신한다. 트리거 조건과 무관하게 "지금 당장 다시 판정"이 필요한 지점(4장 조건5: 피격
	// 후 가시성 증가 반영 재판정, ResolveReachedTarget의 트리거 발생 시)에서 공통으로 쓴다.
	private PerceptionOutcome ForceRollPerception(object key, float targetVisibility, Vector3Int tile, PerceptionTargetKind kind)
	{
		// 5장: 인지 판정 자체가 불가한 상태(기절 등)면 트리거가 걸려도 굴리지 않고 기존 기록을 그대로
		// 동결한다 — ForceReidentifyAttacker(피격 시 강제 재판정, 4장 조건5)도 이 지점을 거치므로 여기
		// 한 곳에서 가드하면 모든 호출 경로에 일괄 적용된다.
		if (!CanPerceive)
		{
			if (!Perception.State.perceptionRecords.TryGetValue(key, out var frozen))
			{
				frozen = new PerceptionRecord();
				Perception.State.perceptionRecords[key] = frozen;
			}
			frozen.WasInRange = true;
			frozen.LastKnownTile = tile;
			frozen.TargetKind = kind;
			return frozen.Outcome;
		}

		float detectionCorrection = PerceptionMath.DetectionCorrection(VisionStat.spotting, Perception.IsAlert);
		float mentalCorrection = GetMentalVisibilityCorrection();
		float total = PerceptionMath.TotalPerceptionVisibility(targetVisibility, detectionCorrection, mentalCorrection);
		PerceptionOutcome outcome = PerceptionMath.RollOutcome(total, Random.value);

		if (!Perception.State.perceptionRecords.TryGetValue(key, out var record))
		{
			record = new PerceptionRecord();
			Perception.State.perceptionRecords[key] = record;
		}
		bool nowSuspicious = outcome == PerceptionOutcome.SuspiciousTile;
		bool wasSuspicious = record.PendingSuspiciousInvestigation;
		Perception.NotifyPerceptionSuspiciousChanged(wasSuspicious, nowSuspicious); // Perception.IsAlert 카운터 O(1) 유지
		// J: key가 Unit이면 그 유닛의 _suspiciousObserverCount를 증감해 IsTrackedAsSuspiciousByAnyEnemy를 O(1)로 만든다.
		if (key is Unit suspTrackedUnit)
		{
			if (!wasSuspicious && nowSuspicious) suspTrackedUnit._suspiciousObserverCount++;
			else if (wasSuspicious && !nowSuspicious) suspTrackedUnit._suspiciousObserverCount--;
		}
		record.Outcome = outcome;
		record.WasInRange = true;
		record.LastKnownTile = tile;
		record.TargetKind = kind;
		record.PendingSuspiciousInvestigation = nowSuspicious;

		// 03문서 4-1/4-4장: 수상한 타일이 새로 발생하면 경계 상태를 만든다(이미 경계 중이면 다른
		// 수상한 타일로 갈아타지 않는다 — Goal_Alert는 고착이 아니라 매 틱 재평가되지만, 정확히 어느
		// 타일을 보고 있었는지는 유지해야 4-5장 "2칸 이내 재인지"가 의미를 가진다).
		if (nowSuspicious && currentAlertSearch == null)
		{
			currentAlertSearch = new AlertSearchState { TargetPosition = new Vector2Int(tile.x, tile.y) };
		}
		return outcome;
	}

	// rayInPerceptionAngle: 이 레이가 인지각(01-A 4장) 범위 안인지 여부(레이별로 UpdateFOV가 미리 계산해 전달).
	// perceptionDistance: 인지 거리(01-A 3장) — 이 거리 이내 + 인지각 안일 때만 "인지 범위 진입"으로 취급한다.
	// 특수 원형 인지 범위(01-A 12장) 스윕 시에는 항상 true + circularRadius를 그대로 넘긴다(각도 무관 판정).
	// IVisionContext methods
	public PerceptionOutcome ResolveReachedTarget(object key, float targetVisibility, Vector3Int tile, float currentDist, out bool firstTouch)
	{
		return this.ResolveReachedTarget(key, targetVisibility, tile, currentDist, PerceptionTargetKind.None, out firstTouch);
	}
	
	public bool HasReachedPerceptionThisPass(object key) => _reachedPerceptionThisPass.ContainsKey(key);
	public bool HasVisionOnlyNonEmptyTile(Vector3Int tile) => Perception.State.visionOnlyNonEmptyTiles.Contains(tile);
	public void AddVisionOnlyNonEmptyTile(Vector3Int tile) => Perception.State.visionOnlyNonEmptyTiles.Add(tile);
	public void AddPersonalSpottedEnemy(Unit unit)
	{
		Perception.State.personalSpottedEnemies.Add(unit); // HashSet이므로 Contains 검사 불필요
	}

	protected void CastRay(FactionData myData, CreateMap cmap, Vector2Int startPos, float angleRad, float maxRadius, List<Unit> allUnits, bool rayInPerceptionAngle, float perceptionDistance)
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

		// 플레이어 진영 몬스터 방 제한 MVP(2026-07-27, 사용자 요청 "몬스터는 방과 방 사이 못봄", 사용자
		// 신고 "시야각 차단이 잘 안되는거 같아"/"자꾸 전투 상태가 됨") — ClampDirectionToOwnRoom(시야
		// "방향"만 방 안쪽으로 트는 근사)만으로는 넓은 시야각(120도) 콘이 인접 방까지 걸치는 경우를
		// 못 막는다. 방 제한 유닛(RoomConfinedMovement)이면 레이 자체가 자기 방 밖 타일에 닿는 순간
		// 벽을 만난 것처럼 끊는다 — 지형/오브젝트/유닛 인지가 전부 이 지점 이후로는 발생하지 않는다.
		bool roomRestrictedObserver = MovementAlgorithm is RoomConfinedMovement;
		int myRoomId = roomRestrictedObserver ? cmap.GetRoomIdAt(currentFloor, startPos) : -1;

		while (dist <= maxRadius)
		{
			if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) break;

			int cx = x / 8;
			int tx = x % 8;
			int cy = y / 8;
			int ty = y % 8;

			if (cx < 0 || cx >= floor.config.width || cy < 0 || cy >= floor.config.height)
			{
				if (x >= 0 && x < mapWidth && y >= 0 && y < mapHeight)
					myData.discoveredMap[currentFloor][x, y] = 2;
				break;
			}

			Chunks c = floor.chunks[cx, cy];
			if (c.roomId == -1 || c.chunk == null)
			{
				myData.discoveredMap[currentFloor][x, y] = 2;
				break;
			}

			if (roomRestrictedObserver && myRoomId >= 0 && c.roomId != myRoomId)
			{
				break; // 자기 방을 벗어난 타일 — 벽과 동일하게 레이 차단(지형도 더 안 밝힘)
			}

			Tile tile = c.chunk[tx, ty];
			// 문 닫힘 시스템(2026-07-28 재정정, 사용자 요청 "문이 닫혀버리면, 벽과 같은 가시성을 가지게
			// 하고, 벽처럼 아예 이동 불가하게 해줘") — 인류가 문(isStructureExist)을 벽으로 기억하지
			// 않던 예외를 없앤다. 닫힌 문은 이제 예외 없이 벽과 동일하게 기억/차단된다(열린 문은 원래도
			// isStructureExist=false라 전부 자연히 통과 가능).
			bool tileIsWall = tile.name == "Wall" || tile.isStructureExist;
			// H: 이미 탐색된 타일(비-0)은 이웃 검사를 건너뛴다 — 같은 타일에 여러 레이가 도달하면
			// Wall Dilation 3×3 루프가 중복 실행되므로, 처음 발견할 때(0→1/2)만 실행한다.
			bool wasUndiscovered = myData.discoveredMap[currentFloor][x, y] == 0;
			myData.discoveredMap[currentFloor][x, y] = tileIsWall ? 2 : 1;

			// 시야 사각지대(DDA 틈새) 근본적 해결: 바닥을 보았다면, 그 바닥과 맞닿은 8방향의 숨은 벽을 즉시 시야에 추가합니다. (Wall Dilation)
			if (!tileIsWall && wasUndiscovered)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dy = -1; dy <= 1; dy++)
					{
						int nx = x + dx, ny = y + dy;
						if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight && myData.discoveredMap[currentFloor][nx, ny] == 0)
						{
							int ncx = nx / 8, ntx = nx % 8, ncy = ny / 8, nty = ny % 8;
							if (ncx < 0 || ncx >= floor.config.width || ncy < 0 || ncy >= floor.config.height)
							{
								myData.discoveredMap[currentFloor][nx, ny] = 2; // 맵 밖은 벽
							}
							else
							{
								Chunks nc = floor.chunks[ncx, ncy];
								bool nIsWall = nc.roomId == -1 || nc.chunk == null || nc.chunk[ntx, nty].name == "Wall"
									|| nc.chunk[ntx, nty].isStructureExist;
								if (nIsWall)
								{
									myData.discoveredMap[currentFloor][nx, ny] = 2; // 숨은 벽 및 청크 빈 공간(허공) 즉시 확정
								}
							}
						}
					}
				}
			}

			Vector3Int revealedTile = new Vector3Int(x, y, currentFloor);
			bool inPerceptionRange = rayInPerceptionAngle && dist <= perceptionDistance;
			Human terrainObserver = this as Human;

			// ③: _visionHandlers 3개짜리 loop이었지만 handler 변수를 전혀 사용하지 않아 동일 블록이
			// 3× 실행되던 버그. if로 교체해 1×만 실행한다.
			if (terrainObserver != null)
			{
				bool isFirstReveal = terrainObserver.personalMap.RevealTile(revealedTile, tileIsWall);
				bool isBossRoom = c.roomRole == RoomRole.BossRoom;

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
						if (inPerceptionRange)
						{
							// 02문서 6장: 오브젝트 유형별 가시성(시체/전멸흔적=100 고정, 그 외=BaseVisibility).
							float objVisibility = VisionMath.ResolveObjectVisibility(obj.BaseVisibility, obj.Tags);
							// Core는 정확히 일치하는 태그("Object/Passable/Core")로만 판정한다(substring이
							// 아님 — "DungeonCore" 같은 다른 태그와 우연히 겹치지 않도록). 2026-07-27부터
							// 보스방 던전 코어(GameSession.DungeonCoreTag)도 이 태그를 함께 갖도록 통합돼
							// 같은 경로를 탄다(사용자 요청 "코어에 대해서, 통합하자") — 웨이브 목표 추적·
							// 운반 기능(HumanWaveManager)은 여전히 Loot 하위 태그로 별도 동작하고, 이
							// 발견 훅은 그 위에 리더 전용 발견~조사 절차만 추가로 얹는다.
							PerceptionTargetKind objKind = obj.Tags.Any(t => t.Contains("WipeoutTrace")) ? PerceptionTargetKind.WipeoutTrace
								: obj.Tags.Any(t => t.Contains("Corpse")) ? PerceptionTargetKind.Corpse
								: obj.Tags.Any(t => t.Contains("Trap")) ? PerceptionTargetKind.Trap
								: obj.Tags.Contains("Object/Passable/Core") ? PerceptionTargetKind.Core
								: PerceptionTargetKind.None;
							PerceptionOutcome outcome = ResolveReachedTarget(obj.Id, objVisibility, revealedTile, dist, objKind, out bool firstTouch);

							// 02문서 12장/14장: 정확 인지된 오브젝트만 실제로 등록한다. 수상한 타일/미인식은
							// 정체를 등록하지 않는다 — 미인식은 01장 5절 마지막 규칙("실제로 위험 요소가
							// 있어도 안전하다고 오판할 수 있다")과 동일하게 안전타일 취급으로 이어진다.
							if (firstTouch && outcome == PerceptionOutcome.AccuratePerception)
							{
								// 15장(오브젝트 위험도 합성)/16장(오브젝트 흥미도 합성) 동시 등록.
								terrainObserver.personalMap.RegisterObject(obj.Id, obj.Position, obj.BaseDanger, obj.BaseInterest, obj.Tags, obj.CauserStage);

								// 20장/21장: 이 오브젝트가 있는 방의 "확인된 오브젝트" 목록에도 반영.
								terrainObserver.personalMap.ObserveObjectInRoom(c.roomId, isBossRoom, obj.Id, obj.BaseDanger, obj.BaseInterest);

								// 13-2장: 생환 파티가 전멸 흔적을 발견하면 동일 traceId당 1회만 던전 위험도에 반영.
								if (obj.Tags.Any(t => t.Contains("WipeoutTrace")) && !string.IsNullOrEmpty(obj.TraceId))
								{
									terrainObserver.Knowledge?.OnWipeoutTraceReflected(obj.TraceId);
								}

								// 03문서 9장(2026-07-27 개편): 함정을 처음 정확 인지하면 발견/선정 조율은
								// TrapPartySystem이 전담한다(발견자 단독 처리가 아니라 파티 전체 성공률 비교 +
								// ETA 동률 우선 선정 — 9-2/9-3장).
								if (objKind == PerceptionTargetKind.Trap)
								{
									TrapPartySystem.OnTrapDiscovered(terrainObserver, obj);
								}

								// 03문서 4-12~4-15장(2026-07-27 신규): 파티원 시체를 나중에(사망 순간 목격이
								// 아니라) 처음 정확 인지하는 경우의 "발견" 트리거.
								if (objKind == PerceptionTargetKind.Corpse && obj.Tags.Contains("Human"))
								{
									PartyDeathSystem.OnCorpseDiscovered(terrainObserver, obj);
								}

								// 03문서 7-3장(2026-07-27 신규): 코어를 처음 정확 인지하면 리더에게 전파한다.
								if (objKind == PerceptionTargetKind.Core)
								{
									CorePartySystem.OnCoreDiscovered(terrainObserver, obj);
								}
							}
							// else: 수상한 타일/미인식 — 다음 트리거(재진입/2칸 재접근)까지 이 판정을 유지한다.
						}
						else if (!_reachedPerceptionThisPass.ContainsKey(obj.Id) && !visionOnlyNonEmptyTiles.Contains(revealedTile))
						{
							// 01장 7절/01-A 7장: 인지 범위 밖 — 아직 정체를 모르는 "비어있지 않은 타일".
							// 정식 등록(RegisterObject 등)은 인지 범위에 들어와야만 가능하다.
							visionOnlyNonEmptyTiles.Add(revealedTile);
						}
					}
				}
			}

			// 유닛 인지 — _visionHandlers의 foreach가 handler.Handle()을 호출하지 않아
			// UnitPerceptionHandler가 데드 코드 상태이므로 직접 처리한다.
			// 인류·몬스터 모두 동작해야 전투가 성립된다.
			if (Session != null && Session.unitGrid.TryGetValue(revealedTile, out Unit unitAtTile)
				&& unitAtTile != null && unitAtTile != this && unitAtTile.Health.hp > 0
				&& this.IsEnemy(unitAtTile))
			{
				if (inPerceptionRange)
				{
					PerceptionOutcome unitOutcome = ResolveReachedTarget(unitAtTile, unitAtTile.GetFinalVisibility(), revealedTile, dist, out bool _);
					if (unitOutcome == PerceptionOutcome.AccuratePerception)
					{
						AddPersonalSpottedEnemy(unitAtTile);
						// 4-15장: 원인미상 파티원 사망 수색 중 몬스터를 정확 인지하면 사망 원인 확인 시도.
						if (this is Human deathSearchObserver)
							PartyDeathSystem.OnDeathSearchSpotted(deathSearchObserver, unitAtTile);
					}
				}
				else if (!_reachedPerceptionThisPass.ContainsKey(unitAtTile) && !visionOnlyNonEmptyTiles.Contains(revealedTile))
				{
					visionOnlyNonEmptyTiles.Add(revealedTile);
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

				// 01장 11절(2026-07-13 개정): "완전 차단 오브젝트"는 벽과 동일하게 레이를 막는다.
				// 이 판정은 InteractableObject.IsFullyBlocking(구조물 성격 여부)만 본다 — 그 오브젝트
				// 자신의 BaseVisibility(=미인식 판정, 위 인지 판정 블록에서 이미 처리됨)와는 완전히
				// 별개다. "시야 판정 불가 오브젝트"(=미인식 대상, 구조물이 아닌 일반 오브젝트/유닛)는
				// 이 플래그가 꺼져 있어 레이를 막지 않는다.
				if (Session != null &&
					Session.objectGrid.TryGetValue(revealedTile, out InteractableObject blocker) &&
					!blocker.IsCollected && blocker.IsFullyBlocking)
				{
					break;
				}
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
		Perception.State.personalSpottedEnemies.Clear();
		Perception.State.visionOnlyNonEmptyTiles.Clear();
		_reachedPerceptionThisPass.Clear();

		FactionData myData = this is Human ? humanFactionData : monsterFactionData;
		Vector2 forward = GetDirVector(currentDir);
		if (forward == Vector2.zero) forward = Vector2.down;

		CreateMap cmap = (Session != null && Session.cmap != null) ? Session.cmap : null;
		if (cmap == null || cmap.map.floors == null) return;

		// 01-A 1~4장: 시야각은 고정(120도), 시야/인지 거리와 인지각은 감지 스탯(spotting)에 따라 결정된다.
		// 02문서 10장: 경계 중에는 감지 보정(+20)이 인지 판정뿐 아니라 시야 거리/인지 거리/인지각에도 반영된다.
		float viewAngle          = VisionMath.BaseViewAngleDeg;
		float effectiveSpotting  = VisionStat.spotting + (Perception.IsAlert ? PerceptionMath.AlertDetectionBonus : 0f);
		float viewDistance       = VisionMath.ViewDistance(effectiveSpotting);
		float perceptionAngle    = VisionMath.AwarenessAngle(effectiveSpotting);
		float perceptionDistance = VisionMath.AwarenessDistance(effectiveSpotting);

		// 03문서 5-4장(조사)/9-6장(함정 해제): 진행 중에는 시야 범위/인지 범위 전부 기본값의 50%.
		// ExplorationPenaltyActive는 Unit.cs에 정의(Investigate/TrapInteraction 둘 중 하나라도 페널티
		// 활성 상태면 true) — 두 문서가 같은 50% 비율이라 별도 분기 없이 하나의 배수로 처리한다.
		if (ExplorationPenaltyActive)
		{
			viewDistance       *= ExplorationMath.InvestigatePenaltyRatio;
			perceptionAngle    *= ExplorationMath.InvestigatePenaltyRatio;
			perceptionDistance *= ExplorationMath.InvestigatePenaltyRatio;
		}

		float centerAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;

		// 방사형 레이캐스트 최적화 적용 (800 -> 72)
		int numRays = 72; // 최적화: 시야각 누락되지 않는 선에서 최대한 감소

		for (int i = 0; i <= numRays; i++)
		{
			float angle = centerAngle - (viewAngle / 2f) + (viewAngle * i / numRays);
			float rad   = angle * Mathf.Deg2Rad;

			// 인지각은 시야각과 같은 중심(centerAngle)을 공유하는 좁은 안쪽 부채꼴이라, 이 레이가 그
			// 부채꼴 안인지는 중심 각도와의 차이만 비교하면 된다 — 인지각이 120도(시야각과 동일)에
			// 도달하면 이 조건이 항상 참이 되어 "시야 범위 전체가 인지 범위화"(01장 4절)가 자연히 성립한다.
			bool rayInPerceptionAngle = Mathf.Abs(Mathf.DeltaAngle(centerAngle, angle)) <= perceptionAngle / 2f;

			CastRay(myData, cmap, position, rad, viewDistance, allUnits, rayInPerceptionAngle, perceptionDistance);
		}

		// 01-A 12장: 엘리트/네메시스/보스 보조 원형 인지 범위 — 정면 각도와 무관하게 주변 위협을
		// 감지한다(전용 유닛 타입이 없어 isSpecialUnit 플래그로 대상을 판정, VisionMath 주석 참고).
		// 벽에는 여전히 막힌다(원형 인지 범위는 "각도 무관"일 뿐 "완전 차단 무시"는 아니다 — 01장 15절).
		if (isSpecialUnit)
		{
			int circularRadius = VisionMath.CircularPerceptionRadius(VisionStat.spotting);
			int circularRays = 32;
			for (int i = 0; i < circularRays; i++)
			{
				float rad = (360f * i / circularRays) * Mathf.Deg2Rad;
				CastRay(myData, cmap, position, rad, circularRadius, allUnits, true, circularRadius);
			}
		}

		// 02문서 4장: 이번 패스(레이 전체 + 특수 원형 스윕)에서 한 번도 도달하지 못한 기존 기록은
		// "지금 안 보인다"로 내려둔다 — Outcome(정확 인지/수상한 타일/미인식) 자체는 건드리지 않고
		// WasInRange만 false로 바꿔서, 다음에 다시 도달할 때 ResolveReachedTarget이 재진입/완전 차단
		// 후 재등장 트리거로 인식하게 한다.
		foreach (var kv in Perception.State.perceptionRecords)
		{
			if (!_reachedPerceptionThisPass.ContainsKey(kv.Key))
				kv.Value.WasInRange = false;
		}
	}

	// 01-A 11장: 이번 턴 활성화된 시야 방향 전환 후보를 모아 우선순위가 가장 높은 방향으로 currentDir를
	// 갱신한다. 리더 명령(5)/기습(2,6)/소리(7) 후보는 대응 게임 시스템(09_명령·리더, 06_전투반응·기습,
	// 08_전파·소리 문서)이 이 폴더에 아직 없어 후보 자체를 만들 수 없다 — 11장의 "적용할 수 없는
	// 후보는 비교에서 제외한다"는 규칙과 동일하게 취급(자연히 제외됨). 미확인 타일(8, 01-A 7장 값
	// 소비 겸용)과 경계(9, 02문서 20장) 후보는 실제로 연결됐다(아래 참고) — "경로 재설정"까지는 여전히
	// 10_목표설정 문서(부재) 몫이라 방향 전환만 담당한다. 실제로 활성화 가능한 후보만 아래에서 구성한다.
	public override void ResolveVisionDirection()
	{
		_visionDirectionCandidates.Clear();
		var candidates = _visionDirectionCandidates;

		// 1순위: 스킬 사용 중(캐스팅 중) — 공격에 사용한 자유 각도(CombatState.State.currentAttackAngle) 기준.
		if (CombatState.State.isCastingAttack)
		{
			Vector2 aimDir = new Vector2(Mathf.Cos(CombatState.State.currentAttackAngle), Mathf.Sin(CombatState.State.currentAttackAngle));
			Dir skillDir = SkillAction.GetDirection8(new Vector2Int(Mathf.RoundToInt(aimDir.x), Mathf.RoundToInt(aimDir.y)));
			candidates.Add(new VisionMath.VisionDirectionCandidate(VisionDirectionReason.SkillUse, skillDir));
		}

		// 3순위: 1칸 이내 근접 전투 대상 존재.
		Unit adjacentEnemy = FindAdjacentEnemy();
		if (adjacentEnemy != null)
			candidates.Add(new VisionMath.VisionDirectionCandidate(VisionDirectionReason.AdjacentMeleeTarget, DirectionToward(adjacentEnemy.position)));

		// 0순위(표 밖 특례): playerAttackTarget은 플레이어가 UI에서 직접 지정한 수동 공격 명령
		// (InputManager.cs 세터, Goals.cs 주석 "수동 공격 명령은 최우선" 참고)이라 10장 "플레이어의
		// 명령에 대한 시야 전환은 항상 최우선 순위가 된다"는 특례를 그대로 적용한다(사용자 확인,
		// 2026-07-22 — 이전엔 4순위 CurrentAttackTarget으로 분류돼 있었음).
		if (playerAttackTarget != null && playerAttackTarget.Health.hp > 0)
			candidates.Add(new VisionMath.VisionDirectionCandidate(VisionDirectionReason.PlayerCommand, DirectionToward(playerAttackTarget.position)));

		// 8순위: 확인이 필요한 비어있지 않은 타일 — 01-A 7장(시야 범위 안 + 인지 범위 밖 + 비어있지
		// 않은 타일 → 임시 위험도/흥미도 +5, "처리: 경로와 탐색 방향 판단에만 사용") + 10장 8순위 표.
		// 2026-07-20까지는 VisionMath.TempWeightForVisionOnlyTile()가 값만 계산하고 아무도 안 읽는
		// 죽은 값이었다(소비자 부재) — "경로 판단" 절반은 여전히 10_목표설정·이동경로·재설정 문서
		// (부재)의 몫이지만, "탐색 방향 판단" 절반은 이 시야 방향 전환 후보로 지금 바로 충족 가능해
		// 연결한다. Perception.State.visionOnlyNonEmptyTiles는 CastRay가 매 UpdateFOV마다 채우는, 아직 인지 범위엔
		// 안 들어온 "비어있지 않은 타일" 목록 그대로다.
		var nearestUnconfirmedTile = NearestTile(Perception.State.visionOnlyNonEmptyTiles);
		if (nearestUnconfirmedTile.HasValue)
		{
			var t = nearestUnconfirmedTile.Value;
			candidates.Add(new VisionMath.VisionDirectionCandidate(VisionDirectionReason.UnconfirmedTile, DirectionToward(new Vector2Int(t.x, t.y))));
		}

		// 9순위: 경계 상태 — 02문서 20장 "시야 방향 전환 후보 O". 수상한 타일 확인 대기 중인 기록이
		// 있으면 그중 가장 가까운 타일 방향으로 전환한다. 04_탐색반응·경계 문서가 없어 실제로 그
		// 타일까지 "이동해서 접근"하는 행동은 만들지 않는다(사용자 확인: 판정 로직만 구현) — 방향
		// 전환만 이 판정 결과에서 직접 나온다.
		// I: .Where().Select() LINQ 체인이 IEnumerable 할당 2개를 만들던 것을 수동 루프로 교체한다.
		{
			Vector3Int? nearestSuspiciousTile = null;
			float nearestSuspDistSq = float.MaxValue;
			foreach (var kv in Perception.State.perceptionRecords)
			{
				if (!kv.Value.PendingSuspiciousInvestigation) continue;
				var t = kv.Value.LastKnownTile;
				float dx = t.x - position.x, dy = t.y - position.y;
				float dSq = dx * dx + dy * dy;
				if (dSq < nearestSuspDistSq) { nearestSuspDistSq = dSq; nearestSuspiciousTile = t; }
			}
			if (nearestSuspiciousTile.HasValue)
			{
				var t = nearestSuspiciousTile.Value;
				candidates.Add(new VisionMath.VisionDirectionCandidate(VisionDirectionReason.Alert, DirectionToward(new Vector2Int(t.x, t.y))));
			}
		}

		// 10순위(최하위, 항상 후보로 존재): 이동 중이면 Move()가 이미 반영한 이동 방향, 아니면 기존 시야
		// 방향을 그대로 유지 — 다른 후보가 전혀 없을 때의 기본값 역할을 한다.
		candidates.Add(new VisionMath.VisionDirectionCandidate(VisionDirectionReason.Moving, currentDir));

		currentDir = VisionMath.ResolveVisionDirection(candidates, currentDir);

		// 2026-07-27 신규 — 방 제한 유닛(RoomConfinedMovement, 야생 몬스터 A/플레이어 몬스터)은 자기
		// 방 밖을 바라볼 수 없다(사용자 요청: "해당 방 바깥쪽을 쳐다볼 수 없어" / "몬스터는 방과 방
		// 사이 못봄"). 위 우선순위로 고른 방향이 방 경계 밖 타일을 향하면, 방 안쪽을 보는 방향 중 원래
		// 의도(가장 가까운 각도)에 제일 가까운 방향으로 대체한다. 다만 이것만으로는 넓은 시야각(120도)
		// 콘이 인접 방까지 걸치는 경우를 못 막아서(사용자 신고 "시야각 차단이 잘 안되는거 같아") 실제
		// 차단은 CastRay에서 레이 자체를 방 경계로 끊는 걸로 보강했다 — 여기 방향 클램프는 그 위에 얹는
		// 보조 근사(자연스러운 실루엣)로 남겨둔다.
		if (MovementAlgorithm is RoomConfinedMovement)
			currentDir = ClampDirectionToOwnRoom(currentDir);
	}

	private Dir ClampDirectionToOwnRoom(Dir dir)
	{
		if (Session?.cmap == null) return dir;
		int myRoomId = Session.cmap.GetRoomIdAt(currentFloor, position);
		if (myRoomId < 0) return dir;

		if (IsDirectionInsideRoom(dir, myRoomId)) return dir;

		for (int offset = 1; offset <= 4; offset++)
		{
			Dir cw = (Dir)(((int)dir + offset) % 8);
			if (IsDirectionInsideRoom(cw, myRoomId)) return cw;
			Dir ccw = (Dir)(((int)dir - offset + 8) % 8);
			if (IsDirectionInsideRoom(ccw, myRoomId)) return ccw;
		}
		return dir; // 안전망 — 자기 위치가 속한 방 안쪽으로 최소 한 방향은 항상 있어야 정상.
	}

	private bool IsDirectionInsideRoom(Dir dir, int myRoomId)
	{
		Vector2Int lookPos = position + GetDirVector(dir);
		return Session.cmap.GetRoomIdAt(currentFloor, lookPos) == myRoomId;
	}

	// 유닛 배치 시스템(2026-07-27 신규) — 실제 위치가 속한 Room(GameSession.roomGrid, 타일 단위 정확
	// 조회)과 currentRoom이 다르면 옛 방에서 빼고 새 방에 등록한다. 매 프레임 호출되지만 비교 자체는
	// 가벼워서(Dictionary 조회 1회) 부담이 적다.
	private Vector2Int _lastRoomSyncPos = new Vector2Int(int.MinValue, int.MinValue);
	private int _lastRoomSyncFloor = int.MinValue;

	// 최적화(2026-07-27, 프레임드랍 점검 요청) — 위치/층이 지난 틱과 같으면 Dictionary 조회 자체를
	// 건너뛴다. 대기 중인 유닛(수색/보호 포메이션/조사 등 제자리 상태)이 많을 때 매 프레임 불필요한
	// roomGrid 조회를 없애준다.
	private void SyncRoomAffiliation()
	{
		if (position == _lastRoomSyncPos && currentFloor == _lastRoomSyncFloor) return;
		_lastRoomSyncPos = position;
		_lastRoomSyncFloor = currentFloor;

		if (Session?.roomGrid == null) return;
		Session.roomGrid.TryGetValue(new Vector3Int(position.x, position.y, currentFloor), out Room actualRoom);
		if (actualRoom == currentRoom) return;

		// 점령 시스템(2026-07-28, 사용자 요청 "빈 방에 그냥 입성시, 그 방은 입성한 진영이 점령하게
		// 해줘") — "비어있었다"는 이 유닛이 실제로 등록되기 전(AddUnit 호출 전) 기준이어야 하므로 여기서
		// 먼저 스냅샷을 뜬다.
		bool enteredRoomWasEmpty = actualRoom != null && IsRoomEffectivelyEmpty(actualRoom);

		Room previousRoom = currentRoom;
		currentRoom?.RemoveUnit(this);
		actualRoom?.AddUnit(this);
		currentRoom = actualRoom;

		// 문 닫힘 시스템(2026-07-28, 사용자 요청) — 유닛이 방을 떠나면서 그 방이 "정리된 상태"가 될 수
		// 있다(예: 마지막 몬스터가 방을 벗어남). 들어간 방(actualRoom)은 인원이 늘어날 뿐이라 새로
		// 열릴 조건을 만들 수 없고(문은 한번 열리면 다시 잠그지 않음) 떠난 방만 확인하면 된다.
		if (previousRoom != null)
		{
			Session.RefreshRoomGateStates(previousRoom);
			// 점령 재계산(2026-07-28) — 죽지 않고 그냥 방을 나가서(예: 인류가 퇴각) 단일 진영이 되는
			// 경우도 GameSession.RemoveDeadUnit과 대칭으로 처리한다. TryFlipRoomOwnershipOnDeath 위
			// 주석 참고.
			Session.OffenseProcessor?.TryResolveRoomOwnership(previousRoom, $"{unitType?.typeName ?? "유닛"} 방 이탈");
		}

		// 점령 시스템(2026-07-28) — 전투 없이도 빈 방에 그냥 들어오기만 하면 입성한 유닛의 진영이 그
		// 방을 점령한다. OffenseProcessor.TryClaimEmptyRoomOnEntry가 기존 점령 전환 경로(전투 사망/
		// 야생 전멸 시)와 동일하게 Room.RoomFaction + CreateMap.occupationState + 방 색칠을 함께 갱신.
		if (enteredRoomWasEmpty) Session.OffenseProcessor?.TryClaimEmptyRoomOnEntry(actualRoom, this);

		// 안개 시스템(2026-07-28, 사용자 요청 "인접 방으로 플레이어 진영 몬스터가 진입한 경험이
		// 있어야지만 사라져") — 플레이어 진영 몬스터가 아직 안개가 걷히지 않은 방에 처음 들어오는
		// 순간을 감지해 GameSession.RevealRoomFog로 넘긴다(영구 해제 + 페이드아웃).
		if (actualRoom != null && !actualRoom.FogRevealed && IsPlayerMonsterFaction)
			Session.RevealRoomFog(actualRoom);
	}

	// SyncRoomAffiliation 전용 — 살아있는 점유 유닛이 하나도 없으면 "빈 방"으로 본다(GameSession.
	// RefreshRoomGateStates의 "완전히 비어있음" 판정과 동일 기준).
	private static bool IsRoomEffectivelyEmpty(Room room)
	{
		foreach (var u in room.ContainedUnits)
			if (u != null && u.hp > 0) return false;
		return true;
	}

	private Dir DirectionToward(Vector2Int targetPos)
	{
		Vector2Int diff = targetPos - position;
		return SkillAction.GetDirection8(diff);
	}

	// 관찰자(this) 기준으로 가장 가까운 타일을 고른다 — 8순위(비어있지 않은 타일)/9순위(경계) 시야
	// 방향 전환 후보가 여러 대상 중 하나를 골라야 할 때 공통으로 쓴다.
	private Vector3Int? NearestTile(IEnumerable<Vector3Int> tiles)
	{
		Vector3Int? nearest = null;
		float nearestDistSq = float.MaxValue;
		foreach (var tile in tiles)
		{
			float dx = tile.x - position.x;
			float dy = tile.y - position.y;
			float distSq = dx * dx + dy * dy;
			if (distSq < nearestDistSq) { nearestDistSq = distSq; nearest = tile; }
		}
		return nearest;
	}

	private Unit FindAdjacentEnemy()
	{
		if (Session == null) return null;
		foreach (Unit u in Session.units)
		{
			if (u == null || u == this || u.Health.hp <= 0) continue;
			bool isEnemy = this.IsEnemy(u);
			if (!isEnemy) continue;
			if (Vector2Int.Distance(u.position, position) <= 1.5f) return u; // 1칸 이내(대각 포함)
		}
		return null;
	}

	#endregion

	public override void OnUpdate(float deltaTime)
	{
		// 기본 스탯(CombatStat.physicalAttack/VisionStat.spotting 등)이 버프/장비 등으로 실시간으로 바뀔 수 있으므로,
		// 그로부터 파생되는 스탯(BaseStat.sterngth/BaseStat.agility/BaseStat.sense 등, CalculateDerivedStats 참고)도 매 프레임
		// 다시 계산해서 항상 최신 기본 스탯을 반영하게 한다. 순수 사칙연산이라 유닛 수가 많아도
		// 부담이 거의 없다(할당 없음, Mathf.Clamp 수십 번 수준) — 별도의 "변경 감지"용 캐시/이벤트
		// 없이 매번 새로 계산하는 쪽이 오히려 더 단순하고 저렴하다.
		CalculateDerivedStats();

		// 유닛 배치 시스템(2026-07-27 신규) 3장/4.3장 — 실제 위치 기준으로 소속 방을 매 프레임
		// 동기화한다("방에 도착하면 소속 방으로 변경"을 명령 완료 이벤트 대신 위치 기반으로 구현 —
		// 스폰 직후 배치처럼 이 시스템이 다루지 않는 경로에도 자연히 적용됨). 배회 몬스터
		// (WildMonsterBehavior)는 9장 보류 항목이라 대상에서 제외한다.
		if (!(FactionBehavior is WildMonsterBehavior))
			SyncRoomAffiliation();

		if (StatusEffects.State.stunDuration   > 0f) StatusEffects.State.stunDuration   -= deltaTime;
		if (StatusEffects.State.slowDuration   > 0f) StatusEffects.State.slowDuration   -= deltaTime;
		if (StatusEffects.State.poisonDuration > 0f) { StatusEffects.State.poisonDuration -= deltaTime; Health.hp -= 1f * deltaTime; }
		if (StatusEffects.State.burnDuration   > 0f) { StatusEffects.State.burnDuration   -= deltaTime; Health.hp -= 1f * deltaTime; }

		// 01-A 9장: 공격 후 가시성 상승 지속시간 감소 (SkillAction.BeginAttackCast가 공격 실행 시 세팅)
		if (VisionStat.attackVisibilityBoostTimer > 0f) VisionStat.attackVisibilityBoostTimer = Mathf.Max(0f, VisionStat.attackVisibilityBoostTimer - deltaTime);

		// 4-6장: 이동당 +20 증가분을 개별적으로 5초 뒤 제거한다(먼저 생긴 증가분부터 먼저 사라짐).
		// ⑪: 만료시간(절대값)을 Queue에 저장 → Peek/Dequeue로 O(1), O(N) RemoveAt/shift 제거.
		while (VisionStat.suspiciousMoveBoostTimers.Count > 0 && VisionStat.suspiciousMoveBoostTimers.Peek() <= UnityEngine.Time.time)
			VisionStat.suspiciousMoveBoostTimers.Dequeue();

		// 15장: 안전 확인 시간 진행 — 이 유닛(개인 지도 소유자)이 위험도를 기록해 둔 타일마다,
		// 지금 그 타일에 몬스터가 "정확 인지된 상태로" 있는지 확인해서 있으면 타이머를 리셋하고
		// 없으면 흘려보낸다. 매 프레임 도는 OnUpdate에 걸어서 real deltaTime을 쓴다(ProcessUnitAction의
		// actionCooldown 주기와 달리 여긴 걸음 속도와 무관하게 매 프레임 호출됨).
		// 2026-07-20(02문서 23장 반영): 예전엔 raw 점유(Session.unitGrid)만 봤는데, 그러면 실제로는
		// 미인식/수상한 타일로 판정된(=인지 실패) 몬스터도 "물리적으로 있으니 위험"으로 취급돼 23장
		// "인지 판정 결과가 미인식이면 실제로 유닛이 존재해도 안전타일로 인지된다"는 규칙과 어긋났다.
		// personalSpottedEnemies는 이제 정확 인지(AccuratePerception)된 대상만 담으므로(4장 구현),
		// "물리적으로 있다" + "정확 인지 중이다" 둘 다 확인해야 진짜 위협으로 친다.
		// 02문서 5장 조건3: 인지 판정을 수행할 수 있는 상태(기절 등 아님)여야 안전/흥미 확인 타이머도
		// 진행한다 — 2장이 "미확인 일반 타일"도 인지 판정 대상에 포함시키므로 이 게이팅도 동일하게 적용.
		_safetyTickTimer += deltaTime;
		if (_safetyTickTimer >= 0.1f)
		{
			if (this is Human human && Session != null && CanPerceive)
			{
				// ⑨: ToList()가 매 0.1초마다 새 List를 할당하던 것을 캐시 필드 재사용으로 교체.
				// TickTileSafety/TickTileInterestConfirm이 컬렉션을 수정(Remove)할 수 있어 직접 순회 불가.
				_dangerTilesCopy.Clear();
				_dangerTilesCopy.AddRange(human.Memory.personalMap.KnownDangerTiles);
				foreach (var tile in _dangerTilesCopy)
				{
					bool threatPresent = Session.unitGrid.TryGetValue(tile, out Unit occupant) && occupant is Monster
						&& occupant.Health.hp > 0f && human.Perception.State.personalSpottedEnemies.Contains(occupant);
					human.Memory.personalMap.TickTileSafety(tile, threatPresent, _safetyTickTimer);
				}

				_interestTilesCopy.Clear();
				_interestTilesCopy.AddRange(human.Memory.personalMap.KnownInterestTiles);
				foreach (var tile in _interestTilesCopy)
				{
					human.Memory.personalMap.TickTileInterestConfirm(tile, _safetyTickTimer);
				}
			}
			_safetyTickTimer = 0f;
		}

		// 03문서 4/9장: 경계·함정 대응 타이머 — actionCooldown 주기(GOAP 판단)와 무관하게 실제 경과
		// 시간으로 흘러야 해서 매 프레임 OnUpdate에서 진행시킨다. GOAP Action은 이 값을 읽고 완료
		// 시점에 결과를 확정할 뿐, 시간 자체는 여기서만 흐른다.
		if (currentAlertSearch != null)
		{
			currentAlertSearch.ElapsedSeconds += deltaTime;
			float limit = currentAlertSearch.IsPostCombatSweep ? ExplorationMath.PostCombatAlertSeconds
				: currentAlertSearch.IsDeathSearch ? ExplorationMath.DeathSearchSeconds
				: ExplorationMath.UnidentifiedAttackSearchSeconds;
			if (currentAlertSearch.ElapsedSeconds >= limit)
			{
				bool wasPostCombatSweep = currentAlertSearch.IsPostCombatSweep;
				currentAlertSearch = null; // 4-8/4-12/4-15장: 시간 종료 → 경계 해제
				// 11장: 전투 종료 후 스윕이 끝난 시점 — 파티 전체가 끝났으면 집결 시작(Party.TryStartRally
				// 자체가 다른 파티원이 아직 스윕/전투 중이면 조용히 아무 것도 안 하고 반환한다).
				if (wasPostCombatSweep && this is Human human && human.party != null)
					human.party.TryStartRally();
			}
		}

		if (currentTrapInteraction != null)
		{
			var trap = currentTrapInteraction;
			if (!trap.JoinWaitElapsed)
			{
				bool recorded = (this is Human trapHuman) && trapHuman.personalMap.IsTrapRecorded(trap.TrapObjectId);
				bool becameElapsed = false;
				if (recorded) { trap.JoinWaitElapsed = true; becameElapsed = true; } // 9-4장: 기록 함정은 대기 단계 자체가 없음
				else
				{
					trap.JoinWaitTimer += deltaTime;
					if (trap.JoinWaitTimer >= ExplorationMath.TrapJoinWaitSeconds) { trap.JoinWaitElapsed = true; becameElapsed = true; }
				}
				// 9-2~9-5장(2026-07-27 개편): 대기가 막 끝난 시점에 파티 전체 성공률 비교로 실제 해제
				// 담당을 선정한다. AutoConfirmed(웨이브 진입 전 최고 성공률 유닛)는 발견 즉시 이미
				// 확정돼 있어 다시 선정할 필요가 없다.
				if (becameElapsed && !trap.AutoConfirmed && this is Human discovererHuman)
					TrapPartySystem.ResolveSelection(discovererHuman, trap);
			}
			else if (trap.SearchingForSelectedUnit)
			{
				// TacticalFSMState.TrapSearchForMissingUnit(BT)가 실제 이동을 담당 — 여기서는 시간만 안 건드림.
			}
			else if (trap.SelectedUnitName != null && !trap.IsSelectedDisarmer && this is Human waitingHuman)
			{
				// 9-7장: 선정되지 않은 발견 유닛 — ETA+3초 미도착이면 SearchingForSelectedUnit으로 전환.
				TrapPartySystem.TickWaitingForSelectedUnit(waitingHuman, trap, deltaTime);
			}
			else if (trap.Phase == TrapPhase.Disarming)
			{
				trap.DisarmProgress01 = Mathf.Min(1f, trap.DisarmProgress01 + deltaTime / ExplorationMath.TrapDisarmDurationSeconds);
				// 9-7/9-8장(2026-07-27 추가): 함정 바로 아래 진행 막대 갱신 — 실제 해제 중일 때만.
				// ⑫: GetComponent를 첫 틱에만 캐시하고 이후엔 재사용.
				if (trap.CachedProgressBar == null && Session != null)
					trap.CachedProgressBar = Session.GetObjectVisual(trap.TrapPosition)?.GetComponent<ObjectProgressBarVisual>();
				if (trap.CachedProgressBar != null) trap.CachedProgressBar.SetProgress(trap.DisarmProgress01, true);
			}
			else if (trap.Phase == TrapPhase.Destroying && Session != null && Session.objectGrid.TryGetValue(trap.TrapPosition, out var trapObj))
			{
				trapObj.TrapHp = Mathf.Max(0f, trapObj.TrapHp - physicalAttack * ExplorationMath.TrapDestroyDamagePerSecondPerAttack * deltaTime);
			}
		}

		if (this is Human investigatorHuman && investigatorHuman.currentInvestigation != null && investigatorHuman.currentInvestigation.PenaltyActive)
		{
			var inv = investigatorHuman.currentInvestigation;
			inv.Progress01 = Mathf.Min(1f, inv.Progress01 + deltaTime / ExplorationMath.InvestigateDurationSeconds);
		}

		// 7-3장(2026-07-27 신규): 리더의 코어 조사 — 도착 후 1초 전파, 이후 조사 진행도 증가.
		if (this is Human coreHuman && coreHuman.currentCoreInteraction != null && coreHuman.currentCoreInteraction.Active)
		{
			var core = coreHuman.currentCoreInteraction;
			if (!core.PropagationDone)
			{
				core.PropagationTimer += deltaTime;
				if (core.PropagationTimer >= ExplorationMath.PartyGoalInitialPropagationSeconds) core.PropagationDone = true;
			}
			else
			{
				core.Progress01 = Mathf.Min(1f, core.Progress01 + deltaTime / ExplorationMath.CoreInvestigateDurationSeconds);
				// 2026-07-27 추가: 코어 바로 아래 진행 막대 갱신(함정 해제 막대와 같은 컴포넌트 재사용).
				// ⑫: GetComponent를 첫 틱에만 캐시하고 이후엔 재사용.
				if (core.CachedProgressBar == null && Session != null)
					core.CachedProgressBar = Session.GetObjectVisual(core.CorePosition)?.GetComponent<ObjectProgressBarVisual>();
				if (core.CachedProgressBar != null) core.CachedProgressBar.SetProgress(core.Progress01, true);
			}
		}

		if (hp > 0f)
			hp = Mathf.Min(maxHp, hp + HPRegen * deltaTime);

		if (CombatState.State.evadeCooldown > 0f)
			CombatState.State.evadeCooldown -= Time.deltaTime;

		if (CombatState.State.isCastingAttack)
		{
			AIState.pendingCastUpdate?.Invoke();
			
			CombatState.State.castTimer -= deltaTime;
			if (CombatState.State.castTimer <= 0f)
			{
				// 공격 타이밍: 반응한 유닛의 VFX(가드·패링) 실행
				if (Session != null)
				{
					foreach (Unit u in Session.units)
					{
						if (u == null || u.AIState.reactingAttacker != this) continue;
						u.AIState.pendingVFX?.Invoke();
						u.AIState.pendingVFX = null;
					}
				}

				CombatState.State.isCastingAttack = false;
				Session?.castingUnits.Remove(this);
				AIState.currentThreat   = null;
				AIState.pendingAttack?.Invoke();
				AIState.pendingAttack   = null;
				AIState.pendingCastUpdate = null;

				if (Session != null)
				{
					foreach (Unit u in Session.units)
					{
						if (u == null) continue;
						u.AIState.reactedAttackers.Remove(this);
						if (u.AIState.reactingAttacker == this)
						{
							u.AIState.reactingAttacker = null;
							u.AIState.reactingThreat   = null;
						}
					}
				}
			}
		}

		for (int i = 0; i < CombatState.State.skillCooldowns.Length; i++)
		{
			if (CombatState.State.skillCooldowns[i] > 0f) CombatState.State.skillCooldowns[i] -= deltaTime;
		}

		List<ThreatTileData> detectedThreats = DetectThreats();
		if (detectedThreats.Count > 0) OnThreatDetected(detectedThreats);
	}

	public override void OnThreatDetected(List<ThreatTileData> threats)
	{
		if (StatusEffects.State.stunDuration > 0f) return;

		foreach (var threat in threats)
		{
			Unit attacker = FindAttackerFromThreat(threat);
			if (attacker == null) continue;
			if (AIState.reactedAttackers.Contains(attacker)) continue;

			// 4-2장: 경계 상태에서 기습/신규 공격에 대한 최초 반응은 반응속도가 1.2배 빨라진다.
			// reactedAttackers가 공격자별로 한 번만 이 계산을 타게 게이팅해주므로 별도 처리 없이
			// "최초 반응"에만 적용된다. 5-4/9-8장: 조사·함정 해제 중에는 반대로 반응속도가 50%로
			// 느려진다(ExplorationPenaltyActive — CastRay의 시야/인지 페널티와 같은 플래그 재사용).
			float alertReaction = BaseStat.reaction * (currentAlertSearch != null ? ExplorationMath.AlertReactionSpeedRatio : 1f);
			if (ExplorationPenaltyActive) alertReaction *= ExplorationMath.InvestigatePenaltyRatio;
			float reactionTimeMs  = 30000f / Mathf.Max(1f, alertReaction);
			float reactionTimeSec = reactionTimeMs / 1000f;

			if (attacker.CombatState.State.isCastingAttack)
			{
				if (attacker.CombatState.State.castTimer >= reactionTimeSec)
				{
					AIState.reactedAttackers.Add(attacker);
					AIState.currentReactionWindow = reactionTimeSec;
					AIState.reactingThreat        = threat;
					AIState.reactingAttacker      = attacker;
					OnReactToThreat(attacker, threat);
				}
				else
				{
					AIState.reactedAttackers.Add(attacker);
					// Failed to react in time - brace for impact (no early damage applied)
				}
			}
		}
	}

	public override void OnReactToThreat(Unit attacker, ThreatTileData threat)
	{
		DefenseSystem.EvaluateEarlyReaction(this, attacker, threat);
	}

	public override void OnDirectHit(Unit attacker, ThreatTileData threat)
	{
		// Obsolete: Impact defense and damage is now handled in TakePhysicalDamage
	}

	public override void ApplyDirectDamage(Unit attacker, float multiplier = 1f)
	{
		if (attacker != null) lastDamageDealer = attacker;

		float raw    = attacker.CombatStat.physicalAttack * multiplier;
		float damage = Mathf.Max(1f, raw - CombatStat.physicalDefense);
		Health.hp -= damage;
		CombatState.State.isHitThisTurn = true;
		if (this.Generate != null)
			this.Generate.TriggerHitEffect(this);

		RecordHitWeightEvent(damage, attacker, raw);
	}

	private Unit FindAttackerFromThreat(ThreatTileData threat)
	{
		foreach (Unit u in Session.units)
		{
			if (u == null) continue;
			if (!u.CombatState.State.isCastingAttack) continue;
			if (u.AIState.currentThreat == threat) return u;
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

	private List<ThreatTileData> _cachedThreats = new List<ThreatTileData>();
	private float _safetyTickTimer = 0f;

	public override List<ThreatTileData> DetectThreats()
	{
		_cachedThreats.Clear();
		if (Session == null) return _cachedThreats;

		// ①: Session.units(전체) 대신 castingUnits(공격 모션 중인 유닛만)를 순회.
		// 실제 공격 중인 유닛은 보통 0~3개 → O(N²) → O(N × 0~3)
		foreach (Unit u in Session.castingUnits)
		{
			if (u == null || u == this) continue;
			if (u.currentFloor != currentFloor) continue;

			ThreatTileData threat = u.AIState.currentThreat;
			if (threat == null) continue;

			if (threat.hitbox.Overlaps(Unit.GetUnitHitbox(this)))
				_cachedThreats.Add(threat);
		}
		return _cachedThreats;
	}

	public override bool RollCritical(bool canCritical)
	{
		if (!canCritical) return false;
		return Random.Range(0f, 100f) < CombatStat.criticalChance;
	}

	public override float ApplyCriticalDamage(float rawDamage) => Mathf.Floor(rawDamage * 1.5f);

	public virtual void DrawThreatTiles()
	{
		if (!CombatState.State.isCastingAttack || AIState.currentThreat == null) return;

		Color color = this is Human ? Color.cyan : Color.red;
		color.a = 0.8f;

		Vector3 floorOffset = this.Generate != null
			? this.Generate.GetFloorOffset(currentFloor)
			: Vector3.zero;

		ThreatTileData threat = AIState.currentThreat;
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


