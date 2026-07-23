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
		if (attacker != null)
		{
			rawDamage = DefenseSystem.EvaluateImpactDefense(this, attacker, rawDamage);
		}

		if (rawDamage > 0f)
		{
			float damage = Mathf.Max(1f, rawDamage - physicalDefense);
			TakeDamage(damage);
			RecordHitWeightEvent(damage, attacker, rawDamage);
		}
	}

	public override void TakeMagicalDamage(float rawDamage, Unit attacker)
	{
		if (attacker != null)
		{
			rawDamage = DefenseSystem.EvaluateImpactDefense(this, attacker, rawDamage);
		}

		if (rawDamage > 0f)
		{
			float damage = Mathf.Max(1f, rawDamage - magicalDefense);
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

		string incidentId = System.Guid.NewGuid().ToString();
		lastAttacker = attacker;

		if (defenderIsHuman)
		{
			// 02문서 17장: "피격 사실/피해량은 확정되지만 공격자 정체는 별도 인지 판정이 필요하다."
			// hp 차감(TakeDamage)은 이미 위에서 확정됐고, 여기서는 공격자를 "누구"로 특정해 기록할
			// 이벤트만 인지 판정으로 게이팅한다 — IsAttackerIdentified가 4장 조건5(피격 시 공격 후
			// 가시성 증가를 반영한 즉시 재판정)를 강제로 수행한다.
			bool attackerIdentified = IsAttackerIdentified(attacker);
			if (attackerIdentified)
			{
				// "일정 피해량 이상"만 위험도/이해도 증가 (3장 공통 규칙)
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
			// 재판정만 수행해 둔다(이후 CastRay/GOAP의 personalSpottedEnemies 등에 반영될 수 있게).
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
		float effectiveSpotting = spotting + (IsAlert ? PerceptionMath.AlertDetectionBonus : 0f); // 02문서 10장: 경계 중 감지 보정
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
			if (tile.name == "Wall" || tile.isStructureExist) return true;

			Vector3Int tilePos = new Vector3Int(x, y, currentFloor);
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
		=> target != null && perceptionRecords.TryGetValue(target, out var record) && record.Outcome == PerceptionOutcome.AccuratePerception;

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
		// 02문서 17장: 상태이상을 건 공격자의 정체도 동일하게 게이팅한다 — RecordHitWeightEvent가 같은
		// 피격 시퀀스에서 이미 판정을 굴려놨으므로 여기서는 그 결과만 조회한다(재판정 아님).
		if (!IsCurrentlyIdentified(lastAttacker)) return;

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
				if (c.chunk[tx, cyVal].name == "Wall" || c.chunk[tx, cyVal].isStructureExist) return false;

				if (!ignoreUnits && Session != null &&
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
		}

		if (this.Generate != null)
			this.Generate.UpdateUnitSpriteForDirection(this);
	}

	// ─────────────────────── 02문서 4장: 트리거 기반 지속 인지 상태 ───────────────────────
	// 이번 UpdateFOV 패스에서 인지 범위 안으로 실제 도달한 대상(적 유닛=Unit 참조, 오브젝트=Id)의
	// 집합 — UpdateFOV 시작 시 비우고, CastRay가 레이를 쏘며 채운다. 이 패스가 끝난 뒤(모든 레이 +
	// 특수 원형 스윕 완료 후) 이 집합에 없는 기존 perceptionRecords는 "이번엔 못 봤다"로 판정해
	// PerceptionRecord.WasInRange를 false로 내린다 — 그래야 나중에 다시 보였을 때(재진입/완전 차단
	// 후 재등장 모두 포함) 새 트리거로 인식돼 재판정이 걸린다(4장 조건1~3이 전부 "지금 안 보이다가
	// 다시 보임"이라는 동일 신호라 이 하나의 메커니즘으로 셋 다 커버된다).
	private readonly Dictionary<object, (float dist, Vector3Int tile)> _reachedPerceptionThisPass = new Dictionary<object, (float, Vector3Int)>();

	// 이번 패스에 처음 도달한 대상이면 트리거 조건(최초 진입/재진입/수상한 타일 2칸 재접근)을 검사해
	// 필요하면 재판정하고, 이미 이번 패스에 다른 레이로 처리된 대상이면 그 결과를 그대로 반환한다
	// (여러 레이가 같은 타일에 도달해도 판정은 패스당 한 번만 — firstTouchThisPass로 호출부가 후속
	// 처리(등록 등)를 중복 실행하지 않도록 알려준다).
	private PerceptionOutcome ResolveReachedTarget(object key, float targetVisibility, Vector3Int tile, float dist, PerceptionTargetKind kind, out bool firstTouchThisPass)
	{
		firstTouchThisPass = !_reachedPerceptionThisPass.ContainsKey(key);
		_reachedPerceptionThisPass[key] = (dist, tile);
		if (!firstTouchThisPass)
			return perceptionRecords.TryGetValue(key, out var already) ? already.Outcome : PerceptionOutcome.Unrecognized;

		perceptionRecords.TryGetValue(key, out var existing);

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
			if (!perceptionRecords.TryGetValue(key, out var frozen))
			{
				frozen = new PerceptionRecord();
				perceptionRecords[key] = frozen;
			}
			frozen.WasInRange = true;
			frozen.LastKnownTile = tile;
			frozen.TargetKind = kind;
			return frozen.Outcome;
		}

		float detectionCorrection = PerceptionMath.DetectionCorrection(spotting, IsAlert);
		float mentalCorrection = GetMentalVisibilityCorrection();
		float total = PerceptionMath.TotalPerceptionVisibility(targetVisibility, detectionCorrection, mentalCorrection);
		PerceptionOutcome outcome = PerceptionMath.RollOutcome(total, Random.value);

		if (!perceptionRecords.TryGetValue(key, out var record))
		{
			record = new PerceptionRecord();
			perceptionRecords[key] = record;
		}
		bool nowSuspicious = outcome == PerceptionOutcome.SuspiciousTile;
		NotifyPerceptionSuspiciousChanged(record.PendingSuspiciousInvestigation, nowSuspicious); // IsAlert 카운터 O(1) 유지
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
			bool tileIsWall = tile.name == "Wall" || tile.isStructureExist;
			myData.discoveredMap[currentFloor][x, y] = tileIsWall ? 2 : 1;

			// 01-A 3장/4장: 이 타일이 인지 거리 + 인지각(또는 특수 원형 인지 범위) 안에 실제로 들어오는지.
			Vector3Int revealedTile = new Vector3Int(x, y, currentFloor);
			bool inPerceptionRange = rayInPerceptionAngle && dist <= perceptionDistance;

			// 지형 밝히기 — FactionData.discoveredMap과 같은 정보(벽/바닥)를 인류 개인 지도에도
			// 기록한다. 몬스터 발견 여부와 무관하게 시야가 지나가는 모든 타일마다 갱신된다(01장 2절
			// "시야 범위 처리: 1.타일 위치 확인"은 인지 범위 여부와 무관하게 항상 가능하다).
			if (this is Human terrainObserver)
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
							PerceptionTargetKind objKind = obj.Tags.Any(t => t.Contains("WipeoutTrace")) ? PerceptionTargetKind.WipeoutTrace
								: obj.Tags.Any(t => t.Contains("Corpse")) ? PerceptionTargetKind.Corpse
								: obj.Tags.Any(t => t.Contains("Trap")) ? PerceptionTargetKind.Trap
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

								// 03문서 9장: 함정을 처음 정확 인지하면 함정 대응 상태를 만든다. 이미 다른 함정을
								// 처리 중이면(currentTrapInteraction != null) 새 함정은 무시한다 — 9-7장 목록에
								// "새 함정 발견"은 중단 조건이 아니고, 2-1장이 이미 수행 중인 반응은 자동 중단
								// 하지 않는다고 명시하므로 한 번에 하나만 처리한다.
								if (objKind == PerceptionTargetKind.Trap && currentTrapInteraction == null)
								{
									currentTrapInteraction = new TrapInteractionState
									{
										TrapObjectId = obj.Id,
										TrapPosition = obj.Position,
									};
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

			if (Session != null &&
				Session.unitGrid.TryGetValue(revealedTile, out Unit unit))
			{
				if (unit != null && unit != this && unit.hp > 0)
				{
					bool isEnemy = this.IsEnemy(unit);
					if (isEnemy)
					{
						if (inPerceptionRange)
						{
							// 02문서 4장/8장/12장: 트리거 시점에만 재판정(확률표)하고, 그 사이엔 이전 결과를
							// 유지한다. 정확 인지된 대상만 personalSpottedEnemies·개인 지도에 반영한다.
							PerceptionOutcome outcome = ResolveReachedTarget(unit, unit.GetFinalVisibility(), revealedTile, dist, PerceptionTargetKind.EnemyUnit, out bool firstTouch);

							if (outcome == PerceptionOutcome.AccuratePerception)
							{
								if (!personalSpottedEnemies.Contains(unit)) personalSpottedEnemies.Add(unit);

								// 지도는 인류만 들고 있다 — 인류가 몬스터를 발견한 시점에만 개인 지도에 기록.
								// GetFinalDanger/GetUnitInterest(전역 종/개체 누적)가 아니라 GetPersonalDanger/
								// GetPersonalInterest(이 관찰자의 personalWeights)를 쓴다 — 전역 값은 OnWaveEnd가
								// 있어야만 갱신되는데(6장) 웨이브 루프가 아직 없어 영원히 그대로다. personalWeights는
								// RecordEvent()가 호출되는 즉시(4장) 갱신되므로, 이걸 써야 실제 전투 이벤트에 맞춰
								// 개인 지도가 바로바로 반영된다.
								if (this is Human human && Knowledge != null)
								{
									float danger = Knowledge.GetPersonalDanger(human, unit);
									float interest = Knowledge.GetPersonalInterest(human, unit);
									human.personalMap.ObserveMonster(unit.name, revealedTile, danger, interest);

									// 20장/21장: 이 몬스터가 서 있는 방의 "확인된 유닛" 목록에도 반영 — 방
									// 위험도/흥미도의 Exploring/Complete 단계 계산에 쓰인다.
									bool isBossRoom = c.roomRole == RoomRole.BossRoom;
									human.personalMap.ObserveUnitInRoom(c.roomId, isBossRoom, unit.name, danger, interest);
								}
							}
							// else: 수상한 타일/미인식 — personalSpottedEnemies는 매 UpdateFOV마다 Clear() 후
							// 다시 채우는 스냅샷이라(01-A 8장 관련 기존 구조), 여기서 추가하지 않는 것만으로
							// 자연히 제외된다(정체 미확정 대상을 타겟/전투 후보로 넘기지 않음).
						}
						else if (!_reachedPerceptionThisPass.ContainsKey(unit) && !visionOnlyNonEmptyTiles.Contains(revealedTile))
						{
							// 01장 7절: 인지 범위 밖 — 정체는 모르지만 "비어있지 않은 타일"로만 인지.
							visionOnlyNonEmptyTiles.Add(revealedTile);
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
		personalSpottedEnemies.Clear();
		visionOnlyNonEmptyTiles.Clear();
		_reachedPerceptionThisPass.Clear();

		FactionData myData = this is Human ? humanFactionData : monsterFactionData;
		Vector2 forward = GetDirVector(currentDir);
		if (forward == Vector2.zero) forward = Vector2.down;

		CreateMap cmap = (Session != null && Session.cmap != null) ? Session.cmap : null;
		if (cmap == null || cmap.map.floors == null) return;

		// 01-A 1~4장: 시야각은 고정(120도), 시야/인지 거리와 인지각은 감지 스탯(spotting)에 따라 결정된다.
		// 02문서 10장: 경계 중에는 감지 보정(+20)이 인지 판정뿐 아니라 시야 거리/인지 거리/인지각에도 반영된다.
		float viewAngle          = VisionMath.BaseViewAngleDeg;
		float effectiveSpotting  = spotting + (IsAlert ? PerceptionMath.AlertDetectionBonus : 0f);
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
			int circularRadius = VisionMath.CircularPerceptionRadius(spotting);
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
		foreach (var kv in perceptionRecords)
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
		var candidates = new List<VisionMath.VisionDirectionCandidate>();

		// 1순위: 스킬 사용 중(캐스팅 중) — 공격에 사용한 자유 각도(currentAttackAngle) 기준.
		if (isCastingAttack)
		{
			Vector2 aimDir = new Vector2(Mathf.Cos(currentAttackAngle), Mathf.Sin(currentAttackAngle));
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
		if (playerAttackTarget != null && playerAttackTarget.hp > 0)
			candidates.Add(new VisionMath.VisionDirectionCandidate(VisionDirectionReason.PlayerCommand, DirectionToward(playerAttackTarget.position)));

		// 8순위: 확인이 필요한 비어있지 않은 타일 — 01-A 7장(시야 범위 안 + 인지 범위 밖 + 비어있지
		// 않은 타일 → 임시 위험도/흥미도 +5, "처리: 경로와 탐색 방향 판단에만 사용") + 10장 8순위 표.
		// 2026-07-20까지는 VisionMath.TempWeightForVisionOnlyTile()가 값만 계산하고 아무도 안 읽는
		// 죽은 값이었다(소비자 부재) — "경로 판단" 절반은 여전히 10_목표설정·이동경로·재설정 문서
		// (부재)의 몫이지만, "탐색 방향 판단" 절반은 이 시야 방향 전환 후보로 지금 바로 충족 가능해
		// 연결한다. visionOnlyNonEmptyTiles는 CastRay가 매 UpdateFOV마다 채우는, 아직 인지 범위엔
		// 안 들어온 "비어있지 않은 타일" 목록 그대로다.
		var nearestUnconfirmedTile = NearestTile(visionOnlyNonEmptyTiles);
		if (nearestUnconfirmedTile.HasValue)
		{
			var t = nearestUnconfirmedTile.Value;
			candidates.Add(new VisionMath.VisionDirectionCandidate(VisionDirectionReason.UnconfirmedTile, DirectionToward(new Vector2Int(t.x, t.y))));
		}

		// 9순위: 경계 상태 — 02문서 20장 "시야 방향 전환 후보 O". 수상한 타일 확인 대기 중인 기록이
		// 있으면 그중 가장 가까운 타일 방향으로 전환한다. 04_탐색반응·경계 문서가 없어 실제로 그
		// 타일까지 "이동해서 접근"하는 행동은 만들지 않는다(사용자 확인: 판정 로직만 구현) — 방향
		// 전환만 이 판정 결과에서 직접 나온다.
		var nearestSuspiciousTile = NearestTile(perceptionRecords.Values.Where(r => r.PendingSuspiciousInvestigation).Select(r => r.LastKnownTile));
		if (nearestSuspiciousTile.HasValue)
		{
			var t = nearestSuspiciousTile.Value;
			candidates.Add(new VisionMath.VisionDirectionCandidate(VisionDirectionReason.Alert, DirectionToward(new Vector2Int(t.x, t.y))));
		}

		// 10순위(최하위, 항상 후보로 존재): 이동 중이면 Move()가 이미 반영한 이동 방향, 아니면 기존 시야
		// 방향을 그대로 유지 — 다른 후보가 전혀 없을 때의 기본값 역할을 한다.
		candidates.Add(new VisionMath.VisionDirectionCandidate(VisionDirectionReason.Moving, currentDir));

		currentDir = VisionMath.ResolveVisionDirection(candidates, currentDir);
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
			if (u == null || u == this || u.hp <= 0) continue;
			bool isEnemy = this.IsEnemy(u);
			if (!isEnemy) continue;
			if (Vector2Int.Distance(u.position, position) <= 1.5f) return u; // 1칸 이내(대각 포함)
		}
		return null;
	}

	#endregion

	public override void OnUpdate(float deltaTime)
	{
		// 기본 스탯(physicalAttack/spotting 등)이 버프/장비 등으로 실시간으로 바뀔 수 있으므로,
		// 그로부터 파생되는 스탯(sterngth/agility/sense 등, CalculateDerivedStats 참고)도 매 프레임
		// 다시 계산해서 항상 최신 기본 스탯을 반영하게 한다. 순수 사칙연산이라 유닛 수가 많아도
		// 부담이 거의 없다(할당 없음, Mathf.Clamp 수십 번 수준) — 별도의 "변경 감지"용 캐시/이벤트
		// 없이 매번 새로 계산하는 쪽이 오히려 더 단순하고 저렴하다.
		CalculateDerivedStats();

		if (stunDuration   > 0f) stunDuration   -= deltaTime;
		if (slowDuration   > 0f) slowDuration   -= deltaTime;
		if (poisonDuration > 0f) { poisonDuration -= deltaTime; hp -= 1f * deltaTime; }
		if (burnDuration   > 0f) { burnDuration   -= deltaTime; hp -= 1f * deltaTime; }

		// 01-A 9장: 공격 후 가시성 상승 지속시간 감소 (SkillAction.BeginAttackCast가 공격 실행 시 세팅)
		if (attackVisibilityBoostTimer > 0f) attackVisibilityBoostTimer = Mathf.Max(0f, attackVisibilityBoostTimer - deltaTime);

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
		if (this is Human human && Session != null && CanPerceive)
		{
			foreach (var tile in human.personalMap.KnownDangerTiles.ToList())
			{
				bool threatPresent = Session.unitGrid.TryGetValue(tile, out Unit occupant) && occupant is Monster
					&& occupant.hp > 0f && human.personalSpottedEnemies.Contains(occupant);
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

		// 03문서 4/9장: 경계·함정 대응 타이머 — actionCooldown 주기(GOAP 판단)와 무관하게 실제 경과
		// 시간으로 흘러야 해서 매 프레임 OnUpdate에서 진행시킨다. GOAP Action은 이 값을 읽고 완료
		// 시점에 결과를 확정할 뿐, 시간 자체는 여기서만 흐른다.
		if (currentAlertSearch != null)
		{
			currentAlertSearch.ElapsedSeconds += deltaTime;
			float limit = currentAlertSearch.IsPostCombatSweep ? ExplorationMath.PostCombatAlertSeconds : ExplorationMath.UnidentifiedAttackSearchSeconds;
			if (currentAlertSearch.ElapsedSeconds >= limit) currentAlertSearch = null; // 4-12장/4-8장: 시간 종료 → 경계 해제
		}

		if (currentTrapInteraction != null)
		{
			var trap = currentTrapInteraction;
			if (!trap.JoinWaitElapsed)
			{
				bool recorded = (this is Human trapHuman) && trapHuman.personalMap.IsTrapRecorded(trap.TrapObjectId);
				if (recorded) trap.JoinWaitElapsed = true; // 9-4장: 기록 함정은 대기 단계 자체가 없음
				else
				{
					trap.JoinWaitTimer += deltaTime;
					if (trap.JoinWaitTimer >= ExplorationMath.TrapJoinWaitSeconds) trap.JoinWaitElapsed = true;
				}
			}
			else if (trap.Phase == TrapPhase.Disarming)
			{
				trap.DisarmProgress01 = Mathf.Min(1f, trap.DisarmProgress01 + deltaTime / ExplorationMath.TrapDisarmDurationSeconds);
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
		float raw    = attacker.physicalAttack * multiplier;
		float damage = Mathf.Max(1f, raw - physicalDefense);
		hp -= damage;
		isHitThisTurn = true;
		if (this.Generate != null)
			this.Generate.TriggerHitEffect(this);

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
