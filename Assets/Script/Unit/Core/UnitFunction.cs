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

	// 피격 이벤트를 이해도/위험도에 즉시 반영한다. DefenseSystem(정적 클래스, UnitFunction 밖)의 Block
	// 판정도 hp를 직접 깎는 별도 데미지 경로라 이 메서드를 그대로 재사용해서 연결한다.
	// rawDamage: 방어/저항 적용 전 원래 피해량("타격 1회별 기준 피해량" 판정용) — 공격자가 인류일 땐 안 쓰인다.
	public void RecordHitWeightEvent(float appliedDamage, Unit attacker, float rawDamage)
	{
		if (attacker == null || this.Knowledge == null) return;

		bool defenderIsHuman = this is Human;
		bool attackerIsHuman = attacker is Human;
		if (defenderIsHuman == attackerIsHuman) return; // 같은 진영끼리는 이 시스템의 대상이 아님

		string incidentId = (++_incidentIdCounter).ToString();
		lastAttacker = attacker;
		lastTrapAttacker = null; // 4-14장: 몬스터 피격이 더 최근이면 함정 원인 기록을 덮어써 무효화한다.

		// E_HIT_HEAVY_INDIRECT 연결용 — 공격자 정체 인지 게이팅은 피격 당사자 기준이라 여기엔 적용하지
		// 않는다(간접 확인은 별도 관찰자가 나중에 스스로 인지, TryConfirmIndirectHit).
		bool isHeavyHitOnHuman = defenderIsHuman && !attackerIsHuman && appliedDamage >= BaseStat.heavyHitThreshold;

		// 피격 발생 공격음은 항상 발생하고, 최종 HP 감소량이 최대 HP의 10% 이상이면 피격 비명도 함께 발생한다.
		PropagationSystem.EmitSound(Session, SoundType.HitImpact, position, currentFloor, this, attacker, isHeavyHitOnHuman, incidentId);
		if (PropagationMath.IsHitScreamTriggered(appliedDamage, Health.maxHp))
			PropagationSystem.EmitSound(Session, SoundType.HitScream, position, currentFloor, this, attacker, isHeavyHitOnHuman, incidentId);

		if (defenderIsHuman)
		{
			// 피격 사실/피해량은 이미 확정됐고, 공격자를 "누구"로 특정해 기록할 이벤트만 인지 판정으로
			// 게이팅한다 — IsAttackerIdentified가 피격 후 가시성 증가를 반영한 즉시 재판정을 강제 수행한다.
			bool attackerIdentified = IsAttackerIdentified(attacker);
			if (attackerIdentified)
			{
				if (appliedDamage >= BaseStat.heavyHitThreshold)
				{
					this.Knowledge.RecordEvent(EventId.E_HIT_HEAVY_SELF, this, attacker, InfoType.DirectExperience, incidentId);
					BroadcastWitnessEvent(EventId.E_HIT_HEAVY_SEEN, this, attacker, incidentId);
				}

				// 실제 적용 피해량이 방어 적용 전 피해량보다 1 이상 낮으면 "예상보다 약하게 들어간 타격"으로
				// 보고 몬스터 종 위험도를 소폭 감소시킨다.
				this.Knowledge.ApplyPerHitDangerDecreaseCheck(attacker, rawDamage, appliedDamage);
			}
			else
			{
				// 공격자 정체를 인지하지 못한 피격 — 경계 상태로 전환해 수색을 시작한다. 방향 정보는
				// 인지 범위 안 + 공격 형태가 방향을 특정 가능(근접/투사체 O, 광역·지면 영역 공격 X)할 때만 "안다"로 취급한다.
				bool inAwarenessRange = Vector2.Distance(position, attacker.position) <= VisionMath.AwarenessDistance(spotting);
				bool shapeProvidesDirection = PropagationMath.AttackShapeProvidesDirection(attacker.CombatState.State.lastAttackShape);
				bool directionKnown = inAwarenessRange && shapeProvidesDirection;
				Vector2Int? attackerPos = directionKnown ? new Vector2Int(attacker.position.x, attacker.position.y) : (Vector2Int?)null;
				currentAlertSearch = new AlertSearchState { TargetPosition = attackerPos };

				// "적을 정확 인지하기 전에 공격받은 경우" 이 사실(+ 방향)을 파티원에게 1회 전파한다.
				if (this is Human victimHuman)
					PropagationSystem.PropagateAttackedFact(victimHuman, attackerPos);
			}
		}
		else
		{
			// 인류(attacker)가 몬스터(this)를 때림 — 이해도만 오르고 위험도 변화는 없음(표 값 자체가 danger=0).
			// 이 RecordEvent 자체는 게이팅 대상이 아니지만(attacker=인류가 이미 자기 대상을 앎), 몬스터(this)
			// 쪽 인지 판정도 "피격 시 재판정" 트리거를 받아야 하므로 게이팅 없이 재판정만 수행한다.
			ForceReidentifyAttacker(attacker);
			this.Knowledge.RecordEvent(EventId.E_MONSTER_HIT_SELF, attacker, this, InfoType.DirectExperience, incidentId);
			BroadcastWitnessEvent(EventId.E_MONSTER_HIT_SEEN, attacker, this, incidentId);
		}
	}

	// 인지 범위 안에 있으나 미인식/수상한 타일인 대상이 공격하면, 공격 후 가시성 증가를 반영해 즉시
	// 재판정한다(관찰자가 인류든 몬스터든 동일 적용). LOS 재확인은 하지 않는다 — 근접 공격자는 인접
	// 타일이라 사실상 항상 차단이 없고, 원거리 공격마다 전체 쉐도우 캐스팅을 다시 도는 비용을 피한다.
	private void ForceReidentifyAttacker(Unit attacker)
	{
		if (attacker == null) return;

		float dist = Vector2.Distance(position, attacker.position);
		float effectiveSpotting = VisionStat.spotting + (Perception.IsAlert ? PerceptionMath.AlertDetectionBonus : 0f); // 02문서 10장: 경계 중 감지 보정
		float perceptionDistance = VisionMath.AwarenessDistance(effectiveSpotting);
		// 전방위 시야(2026-08-24, 보스 골렘) — UpdateFOV와 동일하게 각도 제한만 없앤다. 거리와
		// 완전 차단(IsFullyBlockedTowards) 판정은 아래에서 그대로 적용된다.
		float perceptionAngle = hasOmnidirectionalVision ? 360f : VisionMath.AwarenessAngle(effectiveSpotting);

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

	// 01-A 8장 조건2 전용 최소 LOS 체크 — DDA 레이마칭으로 벽 타일/InteractableObject.IsFullyBlocking만
	// 확인한다. 지형 밝히기/오브젝트 발견 등 UpdateFOV의 ProcessTile 부수효과는 일으키지 않는 순수 판정 함수.
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

		int mapWidth = floor.config.width * floor.config.chunkSize;
		int mapHeight = floor.config.height * floor.config.chunkSize;
		float dist = 0f;

		while (dist < maxDistance)
		{
			if (tMaxX < tMaxY) { dist = tMaxX; tMaxX += tDeltaX; x += stepX; }
			else { dist = tMaxY; tMaxY += tDeltaY; y += stepY; }
			if (dist >= maxDistance) break; // 공격자 자신이 서 있는 타일은 차단 판정에서 제외.

			if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return true;
			Vector3Int tilePos = new Vector3Int(x, y, currentFloor);
			// 닫힌 문은 벽과 동일하게 가시성 차단(예외 없이 모든 진영). 청크 인덱싱+벽 판정은
			// CreateMap.IsStaticTileWalkable로 통합.
			if (!cmap.IsStaticTileWalkable(currentFloor, new Vector2Int(x, y))) return true;

			if (Session != null && Session.objectGrid.TryGetValue(tilePos, out InteractableObject blocker) &&
				!blocker.IsCollected && blocker.IsFullyBlocking)
			{
				return true;
			}
		}
		return false;
	}

	// 피격 대상(this)이 attacker의 정체를 "가중치 이벤트에 쓸 만큼" 확인했는지 — 가중치 시스템의 정체
	// 기반 이벤트가 인류 전용이라 이 게이팅 판정도 인류 관찰자 전용이다.
	private bool IsAttackerIdentified(Unit attacker)
	{
		ForceReidentifyAttacker(attacker);
		if (!(this is Human)) return true; // 게이팅은 인류 관찰자 전용
		return IsCurrentlyIdentified(attacker);
	}

	// 방금 판정된 기록을 다시 굴리지 않고 조회만 한다 — 같은 피격 시퀀스 안에서 같은 트리거를 또 재판정하지 않기 위함.
	private bool IsCurrentlyIdentified(Unit target)
		=> target != null && Perception.State.perceptionRecords.TryGetValue(target, out var record) && record.Outcome == PerceptionOutcome.AccuratePerception;

	// "직접 목격(SEEN)" 계층 근사 구현 — 실제 FOV 기반 목격 판정이 없어 "그 순간 생존해 있는 다른 인류
	// 전원이 목격한 것"으로 근사한다. participant는 이미 SELF 이벤트로 기록된 당사자라 목격자 명단에서 제외.
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

	// 상태이상 직접 경험/목격. 현재 실제로 걸리는 상태이상은 스턴뿐이지만 슬로우/독/화상도 걸리기
	// 시작하면 이 헬퍼를 그대로 타므로 별도 연결이 필요 없다. lastAttacker는 SkillAction/Projectile이
	// 항상 TakeDamage 계열을 먼저 호출한 뒤 Apply*를 호출하므로 이 시점에 이미 정확한 가해자를 가리킨다.
	private void RecordStatusWeightEvent()
	{
		if (!(this is Human) || !(lastAttacker is Monster) || this.Knowledge == null) return;
		// 상태이상을 건 공격자의 정체도 동일하게 게이팅한다 — RecordHitWeightEvent가 이미 판정을 굴려놨으므로 조회만.
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
		{
			// J: GetDirVector를 같은 인수로 두 번 호출하던 것을 캐시로 교체
			Vector2Int dv = GetDirVector(currentDir);
			return Mathf.Atan2(dv.y, dv.x);
		}

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

				// 2026-08-20 — 청크 인덱싱+벽 판정 중복을 CreateMap.IsStaticTileWalkable(동일 기준)
				// 호출로 통합(이 유닛 자신의 currentFloor 기준이라 결과는 기존과 완전히 동일하다).
				if (!cmap.IsStaticTileWalkable(currentFloor, new Vector2Int(targetX, targetY))) return false;

				// 문 진영 통행 판정 — 문 타일은 Tile.isStructureExist를 건드리지 않는다(DoorSystem
				// 참고) — 대신 여기서 보유 진영 일치 여부를 직접 확인한다(보유 진영만 통과 예외).
				if (Session != null && Session.IsBlockedByClosedDoor(new Vector3Int(targetX, targetY, currentFloor), this)) return false;

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
		// 고정 유닛 최종 안전망 — FSM/명령/배회 등 어느 경로로 이동 요청이 들어와도 여기서 전부
		// 무시한다. 이동 호출부가 FSM 바깥에도 여럿 있어 단일 관문에서 한 번 더 막는 편이 확실하다.
		if (isImmobile) return;

		Vector2Int dirVec = GetDirVector(dir);
		Vector2Int nextPos = position + dirVec;

		// 문 개폐 시각 트리거 — 실제 이동 성공 여부와 무관하게, 인접 칸에서 이 칸으로 넘어가려는 시도
		// 자체가 열림 신호다(통행 가능 여부 판정과는 완전히 별개, 순수 시각 연출).
		Session?.NotifyDoorApproachAttempt(new Vector3Int(nextPos.x, nextPos.y, currentFloor), this);

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

		// 실제로 이동에 성공했을 때만 시야 방향을 갱신한다 — 막혀서 못 움직인 틱까지 방향을 바꾸면
		// 유닛이 몰리는 구간에서 제자리에서 계속 홱홱 도는 것처럼 보인다.
		if (canMove)
		{
			currentDir = dir;
			position = nextPos;

			// 이 유닛을 "수상한 타일" 대상으로 추적 중인 적이 하나라도 있으면, 이동 1회마다 가시성을
			// 임시로 +20 늘리는 타이머를 하나 push한다(각 타이머는 5초 뒤 개별 소멸 — OnUpdate에서 감쇠).
			if (IsTrackedAsSuspiciousByAnyEnemy())
				VisionStat.suspiciousMoveBoostTimers.Enqueue(UnityEngine.Time.time + VisionMath.SuspiciousMoveBoostDurationSeconds);

			PropagationSystem.EmitSound(Session, SoundType.Movement, position, currentFloor, this);
		}

		if (this.Generate != null)
			this.Generate.UpdateUnitSpriteForDirection(this);
	}

	// "나를 적으로 보는 유닛 중 지금 나를 수상한 타일로 추적 중인 관찰자가 있는가" — 관찰자별로 다른
	// 값을 주는 대신 이 유닛 자신의 가시성 하나에 반영해 모든 관찰자에게 동일하게 적용한다.
	// ForceRollPerception이 PendingSuspiciousInvestigation 변경 시마다 _suspiciousObserverCount를 증감하므로 O(1) 판정.
	private bool IsTrackedAsSuspiciousByAnyEnemy() => _suspiciousObserverCount > 0;

	// 트리거 기반 지속 인지 상태 — 이번 UpdateFOV 패스에서 인지 범위 안으로 실제 도달한 대상(적
	// 유닛=Unit 참조, 오브젝트=Id)의 집합. 패스가 끝난 뒤 이 집합에 없는 기존 perceptionRecords는
	// WasInRange를 false로 내려, 나중에 다시 보였을 때(재진입/완전 차단 후 재등장) 새 트리거로 인식돼 재판정이 걸리게 한다.
	private readonly Dictionary<object, (float dist, Vector3Int tile)> _reachedPerceptionThisPass = new Dictionary<object, (float, Vector3Int)>();
	// 쉐도우 캐스팅 결과 버퍼 — UpdateFOV가 패스마다 Clear() 후 재사용한다.
	private readonly HashSet<Vector2Int> _shadowCastResult = new HashSet<Vector2Int>();

	// I: ResolveVisionDirection이 매 틱 new List<>()를 할당하던 것을 제거 — 재사용 필드로 교체한다.
	private readonly List<VisionMath.VisionDirectionCandidate> _visionDirectionCandidates = new List<VisionMath.VisionDirectionCandidate>();
	// ⑩: Guid.NewGuid().ToString() 대신 단조 증가 카운터로 incidentId 생성 — 문자열 1회 할당으로 감소.
	private static int _incidentIdCounter;
	// ⑨: KnownDangerTiles/KnownInterestTiles.ToList()가 0.1초마다 List를 새로 할당하던 것을 제거.
	private readonly List<Vector3Int> _dangerTilesCopy = new List<Vector3Int>();
	private readonly List<Vector3Int> _interestTilesCopy = new List<Vector3Int>();

	// ⑪ 프레임 드랍 대응: UpdateFOV의 isOpaque 판정용 캡처값을 람다 대신 인스턴스 필드+캐시된 델리게이트로
	// 옮겨, ScanOctant와 동일 빈도로 반복 호출돼도 유닛 생애 첫 호출에만 할당되게 한다.
	private int _fovMapWidth, _fovMapHeight, _fovChunkSize;
	private Floor _fovFloorData;
	private bool _fovRoomRestrictedObserver;
	private int _fovMyRoomId;
	private System.Func<Vector2Int, bool> _isOpaqueDelegate;

	private bool IsOpaqueAt(Vector2Int pos)
	{
		int px = pos.x, py = pos.y;
		if (px < 0 || px >= _fovMapWidth || py < 0 || py >= _fovMapHeight) return true;
		int ocx = px / _fovChunkSize, otx = px % _fovChunkSize, ocy = py / _fovChunkSize, oty = py % _fovChunkSize;
		if (ocx >= _fovFloorData.config.width || ocy >= _fovFloorData.config.height) return true;
		Chunks oc = _fovFloorData.chunks[ocx, ocy];
		if (oc.roomId == -1 || oc.chunk == null) return true;
		if (_fovRoomRestrictedObserver && _fovMyRoomId >= 0 && oc.roomId != _fovMyRoomId) return true;
		Tile ot = oc.chunk[otx, oty];
		if (ot.name == "Wall" || ot.isStructureExist) return true;
		if (Session != null && Session.objectGrid.TryGetValue(new Vector3Int(px, py, currentFloor), out InteractableObject bl)
			&& !bl.IsCollected && bl.IsFullyBlocking) return true;
		return false;
	}

	// 이번 패스에 처음 도달한 대상이면 트리거 조건(최초 진입/재진입/수상한 타일 2칸 재접근)을 검사해
	// 필요하면 재판정하고, 이미 처리된 대상이면 그 결과를 그대로 반환한다(여러 레이가 같은 타일에
	// 도달해도 판정은 패스당 한 번만 — firstTouchThisPass로 호출부의 중복 후속 처리를 막는다).
	private PerceptionOutcome ResolveReachedTarget(object key, float targetVisibility, Vector3Int tile, float dist, PerceptionTargetKind kind, out bool firstTouchThisPass)
	{
		firstTouchThisPass = !_reachedPerceptionThisPass.ContainsKey(key);
		_reachedPerceptionThisPass[key] = (dist, tile);
		if (!firstTouchThisPass)
			return Perception.State.perceptionRecords.TryGetValue(key, out var already) ? already.Outcome : PerceptionOutcome.Unrecognized;

		Perception.State.perceptionRecords.TryGetValue(key, out var existing);

		// 기록이 없거나(최초 진입) 직전 패스엔 도달하지 못했던(재진입/차단 후 재등장) 대상.
		bool isNewOrReentering = existing == null || !existing.WasInRange;
		// 수상한 타일 확인 대기 중 + 2칸 이내로 접근.
		bool suspiciousReapproach = existing != null && existing.PendingSuspiciousInvestigation
			&& dist <= PerceptionMath.SuspiciousTileReapproachDistanceTiles;

		// 인지 판정 불가 상태는 ForceRollPerception 내부에서 일괄 가드한다 — 트리거가 걸려도 실제로
		// 굴리지 않고 기존 기록을 동결한 채 반환한다.
		if (isNewOrReentering || suspiciousReapproach)
			return ForceRollPerception(key, targetVisibility, tile, kind);

		existing.WasInRange = true;
		existing.LastKnownTile = tile;
		return existing.Outcome;
	}

	// 감지 보정(경계 반영) + 정신력 보정을 더한 최종 계산 가시성으로 확률표를 굴려 즉시 판정을 갱신한다.
	// 트리거 조건과 무관하게 "지금 당장 다시 판정"이 필요한 지점(피격 후 가시성 증가 반영 재판정,
	// ResolveReachedTarget의 트리거 발생 시)에서 공통으로 쓴다.
	private PerceptionOutcome ForceRollPerception(object key, float targetVisibility, Vector3Int tile, PerceptionTargetKind kind)
	{
		// 인지 판정 자체가 불가한 상태(기절 등)면 트리거가 걸려도 굴리지 않고 기존 기록을 그대로
		// 동결한다 — ForceReidentifyAttacker도 이 지점을 거치므로 한 곳에서 가드하면 모든 경로에 일괄 적용된다.
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
			if (!wasSuspicious && nowSuspicious) suspTrackedUnit.IncrementSuspiciousObserverCount();
			else if (wasSuspicious && !nowSuspicious) suspTrackedUnit.DecrementSuspiciousObserverCount();
		}
		record.Outcome = outcome;
		record.WasInRange = true;
		record.LastKnownTile = tile;
		record.TargetKind = kind;
		record.PendingSuspiciousInvestigation = nowSuspicious;

		// 수상한 타일이 새로 발생하면 경계 상태를 만든다(이미 경계 중이면 다른 타일로 갈아타지 않는다 —
		// 어느 타일을 보고 있었는지 유지해야 "2칸 이내 재인지"가 의미를 가진다).
		if (nowSuspicious && currentAlertSearch == null)
		{
			currentAlertSearch = new AlertSearchState { TargetPosition = new Vector2Int(tile.x, tile.y) };
		}
		return outcome;
	}

	// perceptionDistance 이내 + 인지각 안일 때만 "인지 범위 진입"으로 취급한다. 특수 원형 인지 범위
	// 스윕 시에는 항상 true + circularRadius를 그대로 넘긴다(각도 무관 판정).
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

		// 조사/함정 해제 진행 중에는 시야 범위/인지 범위 전부 기본값의 50%(ExplorationPenaltyActive, Unit.cs).
		if (ExplorationPenaltyActive)
		{
			viewDistance       *= ExplorationMath.InvestigatePenaltyRatio;
			perceptionAngle    *= ExplorationMath.InvestigatePenaltyRatio;
			perceptionDistance *= ExplorationMath.InvestigatePenaltyRatio;
		}

		// 전방위 시야 — 각도 제한만 없앤다. 거리/차폐는 그대로 적용되므로 벽을 뚫어 보진 않는다.
		// 위 조사 페널티보다 뒤에 둬서 페널티가 각도를 다시 깎지 않게 한다.
		if (hasOmnidirectionalVision)
		{
			viewAngle       = 360f;
			perceptionAngle = 360f;
		}

		float centerAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;

		// 대칭 쉐도우 캐스팅 — 72레이 DDA + Wall Dilation 대체.
		// 방 제한 유닛(RoomConfinedMovement)은 isOpaque에서 방 밖 타일을 불투명 처리해 시야를 차단한다.
		bool roomRestrictedObserver = MovementAlgorithm is RoomConfinedMovement;
		int myRoomId = roomRestrictedObserver ? cmap.GetRoomIdAt(currentFloor, position) : -1;

		Floor floorData = cmap.map.floors[currentFloor];
		if (floorData.chunks == null) return;
		int csVision = floorData.config.chunkSize;
		int mapWidth  = floorData.config.width  * csVision;
		int mapHeight = floorData.config.height * csVision;

		Human terrainObserver = this as Human;
		// 몬스터는 인류와 동일한 시야 파이프라인에 편승하되, ProcessTile에서 지형/함정만 기록한다.
		Monster terrainObserverMonster = this as Monster;
		var visionNonEmpty = Perception.State.visionOnlyNonEmptyTiles;

		// 불투명 판정(벽/구조물/방 밖/완전차단오브젝트 → true) — 매 호출 람다 할당 대신 캐시된 델리게이트를 쓴다(⑪ 참고).
		_fovMapWidth = mapWidth;
		_fovMapHeight = mapHeight;
		_fovChunkSize = csVision;
		_fovFloorData = floorData;
		_fovRoomRestrictedObserver = roomRestrictedObserver;
		_fovMyRoomId = myRoomId;
		System.Func<Vector2Int, bool> isOpaque = _isOpaqueDelegate ??= IsOpaqueAt;

		// 가시 타일 처리 로컬 함수 — discoveredMap 갱신, 지형 발견, 오브젝트/유닛 인지
		void ProcessTile(int x, int y, bool inPerceptionRange)
		{
			if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return;
			int pcx = x / csVision, ptx = x % csVision, pcy = y / csVision, pty = y % csVision;
			if (pcx >= floorData.config.width || pcy >= floorData.config.height) return;
			Chunks chunk = floorData.chunks[pcx, pcy];
			if (chunk.roomId == -1 || chunk.chunk == null) return;
			// 방 제한 유닛: 방 밖 타일은 discoveredMap에도 반영하지 않는다
			if (roomRestrictedObserver && myRoomId >= 0 && chunk.roomId != myRoomId) return;

			Tile tile = chunk.chunk[ptx, pty];
			bool tileIsWall = tile.name == "Wall" || tile.isStructureExist;
			myData.discoveredMap[currentFloor][x, y] = tileIsWall ? 2 : 1;

			Vector3Int revealedTile = new Vector3Int(x, y, currentFloor);
			float dist = (x == position.x && y == position.y) ? 0f
				: Mathf.Sqrt((x - position.x) * (x - position.x) + (y - position.y) * (y - position.y));

			if (terrainObserver != null)
			{
				bool isFirstReveal = terrainObserver.personalMap.RevealTile(revealedTile, tileIsWall);
				bool isBossRoom = chunk.roomRole == RoomRole.BossRoom;
				if (isFirstReveal && !tileIsWall)
				{
					int totalFloorTiles = cmap.GetRoomFloorTileCount(currentFloor, chunk.roomId);
					terrainObserver.personalMap.ObserveRoomTileRevealed(chunk.roomId, isBossRoom, totalFloorTiles);
				}
				if (Session != null && Session.objectGrid.TryGetValue(revealedTile, out InteractableObject obj))
				{
					if (!obj.IsCollected && !terrainObserver.personalMap.IsObjectKnown(obj.Id))
					{
						if (inPerceptionRange)
						{
							float objVis = VisionMath.ResolveObjectVisibility(obj.BaseVisibility, obj.Tags);
							PerceptionTargetKind objKind = TagsContain(obj.Tags, "WipeoutTrace") ? PerceptionTargetKind.WipeoutTrace
								: TagsContain(obj.Tags, "Corpse") ? PerceptionTargetKind.Corpse
								: TagsContain(obj.Tags, "Trap") ? PerceptionTargetKind.Trap
								: obj.Tags.Contains("Object/Passable/Core") ? PerceptionTargetKind.Core
								: PerceptionTargetKind.None;
							PerceptionOutcome outcome = ResolveReachedTarget(obj.Id, objVis, revealedTile, dist, objKind, out bool firstTouch);
							if (firstTouch && outcome == PerceptionOutcome.AccuratePerception)
							{
								terrainObserver.personalMap.RegisterObject(obj.Id, obj.Position, obj.BaseDanger, obj.BaseInterest, obj.Tags, obj.CauserStage);
								terrainObserver.personalMap.ObserveObjectInRoom(chunk.roomId, isBossRoom, obj.Id, obj.BaseDanger, obj.BaseInterest);
								if (objKind == PerceptionTargetKind.WipeoutTrace && !string.IsNullOrEmpty(obj.TraceId))
									terrainObserver.Knowledge?.OnWipeoutTraceReflected(obj.TraceId);
								if (objKind == PerceptionTargetKind.Trap)
									TrapPartySystem.OnTrapDiscovered(terrainObserver, obj);
								if (objKind == PerceptionTargetKind.Corpse && obj.Tags.Contains("Human"))
									PartyDeathSystem.OnCorpseDiscovered(terrainObserver, obj);
								if (objKind == PerceptionTargetKind.Corpse && obj.Tags.Contains("Monster"))
									PropagationSystem.OnMonsterCorpseDiscovered(terrainObserver, obj);
								// 코어는 TacticalFSMState.HasCoreAttackTarget이 방 단위로 직접 확인하므로 발견 이벤트 전파가 따로 필요 없다.
							}
						}
						else if (!_reachedPerceptionThisPass.ContainsKey(obj.Id) && !visionNonEmpty.Contains(revealedTile))
							visionNonEmpty.Add(revealedTile);
					}
				}
			}
			else if (terrainObserverMonster != null)
			{
				// 인류(PersonalMapKnowledge)와 달리 위험도·흥미도·방·오브젝트 등록은 하지 않는다 —
				// 지형 기록 + 함정 위치만 남긴다.
				terrainObserverMonster.monsterMap.RevealTile(revealedTile, tileIsWall);
				if (!tileIsWall && Session != null && Session.objectGrid.TryGetValue(revealedTile, out InteractableObject monsterSeenObj)
					&& !monsterSeenObj.IsCollected && TagsContain(monsterSeenObj.Tags, "Trap"))
				{
					terrainObserverMonster.monsterMap.RecordTrap(monsterSeenObj.Id, monsterSeenObj.Position);
				}
			}

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
						if (this is Human deathSearchObserver)
							PartyDeathSystem.OnDeathSearchSpotted(deathSearchObserver, unitAtTile);
					}
				}
				else if (!_reachedPerceptionThisPass.ContainsKey(unitAtTile) && !visionNonEmpty.Contains(revealedTile))
					visionNonEmpty.Add(revealedTile);
			}
		}

		// 메인 패스: 시야 반경 내 쉐도우 캐스팅 → FOV 120° 콘 필터 후 처리
		_shadowCastResult.Clear();
		VisionMath.SymmetricShadowCast(position, (int)viewDistance, isOpaque, _shadowCastResult);
		float halfViewAngle = viewAngle * 0.5f;
		float halfPercAngle = perceptionAngle * 0.5f;
		foreach (Vector2Int tp in _shadowCastResult)
		{
			int x = tp.x, y = tp.y;
			bool isOrigin = (x == position.x && y == position.y);
			bool inPerc;
			if (isOrigin)
			{
				inPerc = true;
			}
			else
			{
				float angleToTile = Mathf.Atan2(y - position.y, x - position.x) * Mathf.Rad2Deg;
				float delta = Mathf.Abs(Mathf.DeltaAngle(centerAngle, angleToTile));
				if (delta > halfViewAngle) continue;
				float d = Mathf.Sqrt((x - position.x) * (x - position.x) + (y - position.y) * (y - position.y));
				inPerc = delta <= halfPercAngle && d <= perceptionDistance;
			}
			ProcessTile(x, y, inPerc);
		}

		// 특수 유닛 원형 인지 패스: 360° 전방향, 원형 반경 내 전부 인지 범위로 처리
		if (isSpecialUnit)
		{
			int circularRadius = VisionMath.CircularPerceptionRadius(VisionStat.spotting);
			_shadowCastResult.Clear();
			VisionMath.SymmetricShadowCast(position, circularRadius, isOpaque, _shadowCastResult);
			foreach (Vector2Int tp in _shadowCastResult)
				ProcessTile(tp.x, tp.y, true);
		}

		// 이번 패스에서 한 번도 도달하지 못한 기존 기록은 Outcome은 건드리지 않고 WasInRange만 false로
		// 내려서, 다음에 다시 도달할 때 ResolveReachedTarget이 재진입 트리거로 인식하게 한다.
		foreach (var kv in Perception.State.perceptionRecords)
		{
			if (!_reachedPerceptionThisPass.ContainsKey(kv.Key))
				kv.Value.WasInRange = false;
		}
	}

	// 이번 턴 활성화된 시야 방향 전환 후보를 모아 우선순위가 가장 높은 방향으로 currentDir를 갱신한다.
	// 리더 명령/기습 후보는 대응 시스템이 없어 후보 자체를 만들 수 없다("적용할 수 없는 후보는 제외"
	// 규칙으로 자연히 제외). "경로 재설정"은 별도 목표설정 시스템 몫이라 여기선 방향 전환만 담당한다.
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

		// 0순위(표 밖 특례): playerAttackTarget은 플레이어가 직접 지정한 수동 공격 명령(InputManager.cs
		// 세터)이라 10장 "플레이어 명령에 대한 시야 전환은 항상 최우선"이라는 특례를 적용한다.
		if (playerAttackTarget != null && playerAttackTarget.Health.hp > 0)
			candidates.Add(new VisionMath.VisionDirectionCandidate(VisionDirectionReason.PlayerCommand, DirectionToward(playerAttackTarget.position)));

		// 7순위: 소리 감지 — 아직 확인 행동을 시작하지 않은(또는 이미 시작된) 유효한 소리 반응 대상이 있으면 그 방향으로.
		if (this is Human soundHuman)
		{
			var pendingSound = soundHuman.Propagation.PendingSound;
			var alert = currentAlertSearch;
			Vector2Int? soundDir = (alert != null && alert.IsSoundResponse && alert.TargetPosition.HasValue) ? alert.TargetPosition
				: (pendingSound != null && UnityEngine.Time.time <= pendingSound.ValidUntilTime) ? pendingSound.SourcePosition
				: (Vector2Int?)null;
			if (soundDir.HasValue)
				candidates.Add(new VisionMath.VisionDirectionCandidate(VisionDirectionReason.SoundDetected, DirectionToward(soundDir.Value)));
		}

		// 8순위: 확인이 필요한 비어있지 않은 타일(시야 범위 안 + 인지 범위 밖). "경로 판단" 절반은
		// 별도 이동경로 시스템 몫이라 여기선 "탐색 방향 판단" 절반만 연결한다. visionOnlyNonEmptyTiles는
		// UpdateFOV의 ProcessTile이 매 패스 채우는, 아직 인지 범위엔 안 들어온 타일 목록이다.
		var nearestUnconfirmedTile = NearestTile(Perception.State.visionOnlyNonEmptyTiles);
		if (nearestUnconfirmedTile.HasValue)
		{
			var t = nearestUnconfirmedTile.Value;
			candidates.Add(new VisionMath.VisionDirectionCandidate(VisionDirectionReason.UnconfirmedTile, DirectionToward(new Vector2Int(t.x, t.y))));
		}

		// 9순위: 경계 상태 — 수상한 타일 확인 대기 중인 기록이 있으면 가장 가까운 타일 방향으로 전환한다.
		// 실제 이동 접근 행동은 만들지 않고 방향 전환만 담당한다.
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

		// 방 제한 유닛은 자기 방 밖을 바라볼 수 없다 — 고른 방향이 방 경계 밖이면 방 안쪽 방향 중
		// 가장 가까운 방향으로 대체한다. 실제 시야 차단은 isOpaque가 쉐도우 캐스팅 단계에서 이미
		// 막으므로, 여기 방향 클램프는 보조적인 실루엣 보정일 뿐이다.
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

	// 실제 위치가 속한 Room(타일 단위 정확 조회)과 currentRoom이 다르면 옛 방에서 빼고 새 방에 등록한다.
	private Vector2Int _lastRoomSyncPos = new Vector2Int(int.MinValue, int.MinValue);
	private int _lastRoomSyncFloor = int.MinValue;

	// 위치/층이 지난 틱과 같으면 Dictionary 조회 자체를 건너뛴다 — 제자리 상태 유닛이 많을 때 불필요한 roomGrid 조회를 없앤다.
	private void SyncRoomAffiliation()
	{
		if (position == _lastRoomSyncPos && currentFloor == _lastRoomSyncFloor) return;
		_lastRoomSyncPos = position;
		_lastRoomSyncFloor = currentFloor;

		if (Session?.roomGrid == null) return;
		Session.roomGrid.TryGetValue(new Vector3Int(position.x, position.y, currentFloor), out Room actualRoom);
		if (actualRoom == currentRoom) return;

		// 방 소유권 전환은 코어 체력제(OffenseProcessor.OnCoreDestroyed)로만 일어난다 — 방을 떠나는
		// 것만으로는 점령이 바뀌지 않는다.
		currentRoom?.RemoveUnit(this);
		actualRoom?.AddUnit(this);
		currentRoom = actualRoom;

		// 플레이어 진영 몬스터가 아직 안개가 걷히지 않은 방에 처음 들어오는 순간을 감지해 영구 해제한다.
		if (actualRoom != null && !actualRoom.FogRevealed && IsPlayerMonsterFaction)
			Session.RevealRoomFog(actualRoom);
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
		// A: Session.units 전체 O(N) 순회 → personalSpottedEnemies(이미 IsEnemy 보장)만 순회
		foreach (Unit u in Perception.State.personalSpottedEnemies)
		{
			if (u == null || u.Health.hp <= 0 || u.currentFloor != currentFloor) continue;
			if (Vector2Int.Distance(u.position, position) <= 1.5f) return u;
		}
		return null;
	}

	// C: LINQ Any(lambda) → IEnumerator 박싱 없이 직접 for 루프로 순회
	private static bool TagsContain(System.Collections.Generic.List<string> tags, string sub)
	{
		for (int i = 0; i < tags.Count; i++)
			if (tags[i].Contains(sub)) return true;
		return false;
	}

	#endregion

	public override void OnUpdate(float deltaTime)
	{
		// 기본 스탯이 버프/장비로 실시간 변할 수 있으므로 파생 스탯도 매 프레임 다시 계산한다 — 순수
		// 사칙연산이라 부담이 적어 별도 변경 감지 캐시 없이 매번 재계산하는 쪽이 더 단순하다.
		CalculateDerivedStats();

		// 실제 위치 기준으로 소속 방을 매 프레임 동기화한다("방에 도착하면 소속 방으로 변경"을 명령
		// 완료 이벤트 대신 위치 기반으로 구현). 배회 몬스터는 제외한다.
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

		// 안전 확인 시간 진행 — 위험도를 기록해 둔 타일마다 몬스터가 "정확 인지된 상태로" 있는지 확인해
		// 타이머를 리셋/진행한다. "물리적으로 점유" + "정확 인지 중" 둘 다 확인해야 위협으로 친다
		// (미인식/수상한 타일 판정이면 실제 존재해도 안전타일로 취급). 인지 판정 불가 상태면 타이머도 진행하지 않는다.
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

				// 소리 감지는 발생 즉시(EmitSound) 범위 스캔+통지가 끝나므로 여기서 재평가할 대상이 없다.
				// 사망 정보는 스냅샷이 아니라 매 틱 재확인해 지속 재전파한다.
				PartyDeathSystem.TickOngoingPropagation(human);
				// 함정 정보도 사망/코어와 동일하게 미보유 파티원에게 지속 재전파한다.
				TrapPartySystem.TickOngoingPropagation(human);
			}
			_safetyTickTimer = 0f;
		}

		// 경계·함정 대응 타이머 — actionCooldown 주기(GOAP 판단)와 무관하게 실제 경과 시간으로 흘러야
		// 해서 매 프레임 OnUpdate에서 진행시킨다.
		if (currentAlertSearch != null)
		{
			currentAlertSearch.ElapsedSeconds += deltaTime;
			// 소리 반응 접근 자체엔 시간 제한이 없다 — 아직 인지 판정을 안 굴린 소리 반응 접근 중엔
			// 아래 "미식별 공격 수색" 워치독을 적용하지 않는다.
			bool isSoundResponseStillApproaching = currentAlertSearch.IsSoundResponse && !currentAlertSearch.SoundPerceptionRolled;
			if (!isSoundResponseStillApproaching)
			{
				float limit = currentAlertSearch.IsPostCombatSweep ? ExplorationMath.PostCombatAlertSeconds
					: currentAlertSearch.IsDeathSearch ? ExplorationMath.DeathSearchSeconds
					: ExplorationMath.UnidentifiedAttackSearchSeconds;
				if (currentAlertSearch.ElapsedSeconds >= limit)
				{
					bool wasPostCombatSweep = currentAlertSearch.IsPostCombatSweep;
					currentAlertSearch = null; // 시간 종료 → 경계 해제
					// 전투 종료 후 스윕이 끝난 시점 — 파티 전체가 끝났으면 집결 시작.
					if (wasPostCombatSweep && this is Human human && human.party != null)
						human.party.TryStartRally();
				}
			}
		}

		// 07-A 9장(2026-07-31 신규): 전투 진입 합류 대기 타이머 — currentAlertSearch와 동일하게 실제
		// 경과 시간으로 매 프레임 흐른다.
		if (this is Human joinWaitHuman && joinWaitHuman.currentJoinCombatWait != null)
			PropagationSystem.TickJoinCombatWait(joinWaitHuman, deltaTime);

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
				// 대기가 막 끝난 시점에 파티 전체 성공률 비교로 실제 해제 담당을 선정한다. AutoConfirmed
				// (웨이브 진입 전 최고 성공률 유닛)는 발견 즉시 이미 확정돼 있어 다시 선정할 필요가 없다.
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

		// 오브젝트(코어/문) 공격 채널링 — TrapPhase.Destroying과 동일한 패턴. 자동 AI와 플레이어 명령
		// 양쪽이 인접 도착 시 이 필드를 채우고, 대상이 파괴/전환/소멸될 때까지 매 프레임 데미지를 적용한다.
		if (currentAttackObjectTarget.HasValue && Session != null)
		{
			Vector3Int targetPos = currentAttackObjectTarget.Value;
			if (Session.objectGrid.TryGetValue(targetPos, out var targetObj))
			{
				bool isCore = targetObj.Tags != null && targetObj.Tags.Contains(GameSession.CoreTag);
				bool isDoor = targetObj.Tags != null && targetObj.Tags.Contains(DoorSystem.DoorTag);
				// 함정 공격 — 코어/문과 동일한 채널링 필드를 공유하지만, 자동 AI가 스스로 트리거하는
				// 경로는 없다(플레이어 우클릭 명령만 채운다).
				bool isTrap = targetObj.Tags != null && targetObj.Tags.Exists(t => t.Contains("Trap"));
				Vector2Int targetPos2D = new Vector2Int(targetPos.x, targetPos.y);

				// 채널링 중에는 항상 공격 대상을 바라본다 — currentDir만 세팅하고 UpdateUnitSpriteForDirection을
				// 빠뜨리면 실제 스프라이트는 갱신되지 않으므로 반드시 짝지어 호출한다.
				if ((isCore || isDoor || isTrap) && targetPos2D != position)
				{
					currentDir = SkillAction.GetDirection8(targetPos2D - position);
					Generate?.UpdateUnitSpriteForDirection(this);
				}

				// 코어/문 파괴는 반드시 인접 1칸에서만 이뤄져야 한다 — 채널링 시작 시점에만 확인하면
				// 도중 회피/점멸로 밀려나도 데미지가 계속 적용돼 원거리 파괴가 되므로 매 프레임 재확인한다.
				bool isAdjacent = AIMovementHelper.IsAdjacent(position, targetPos2D);

				// 같은 프레임에 다른 유닛이 먼저 코어를 파괴시켜 소유권이 전환된 경우(즉시 반피 회복)
				// CoreHp>0f만으로는 못 막으므로 IsRoomCoreStillHostile로 다시 확인한다. 종족 무관 헬퍼를
				// 써야 플레이어 몬스터의 수동 공격이 매 프레임 스스로 취소되지 않는다.
				bool stillHostile = isCore && TacticalFSMState.IsRoomCoreStillHostile(this, targetPos);
				if (isCore && (!isAdjacent || !stillHostile))
				{
					ClearAttackObjectTarget();
				}
				else if (isDoor && !isAdjacent)
				{
					ClearAttackObjectTarget();
				}
				else if (isTrap && !isAdjacent)
				{
					ClearAttackObjectTarget();
				}
				else if (isCore && targetObj.CoreHp > 0f)
				{
					// 고정 초당 데미지 — physicalAttack 스탯과 무관하게 채널링 유닛 1명당 이 값만큼만
					// 깎는다. 여러 명이 동시 공격하면 각자 블록을 독립 실행하므로 인원수만큼 자연히 합산된다.
					targetObj.CoreHp = Mathf.Max(0f, targetObj.CoreHp - GameSession.CoreAttackDamagePerSecond * deltaTime);
					targetObj.TimeSinceLastDamaged = 0f; // 자동 회복 지연 타이머 리셋(InteractableObject.TimeSinceLastDamaged 참고)

					// 체력 표시 — 함정 해제(0→1 완료도)와 달리 코어/문은 체력이 깎이는 대상이라
					// 남은 체력 비율을 그대로 채움비로 쓴다(가득 찬 채로 시작해 맞을수록 줄어듦).
					float coreProgress = targetObj.CoreMaxHp > 0f ? targetObj.CoreHp / targetObj.CoreMaxHp : 0f;
					Session.GetObjectVisual(targetPos)?.GetComponent<ObjectProgressBarVisual>()?.SetProgress(coreProgress, true);

					if (targetObj.CoreHp <= 0f)
					{
						// 코어 파괴 순간 OffenseProcessor.OnCoreDestroyed로 방 소유권을 즉시 전환한다
						// (코어 자체는 사라지지 않고 반피로 회복 — 소유권 전환이 곧 "파괴 완료").
						if (Session.roomGrid.TryGetValue(targetPos, out Room coreRoom))
							Session.OffenseProcessor?.OnCoreDestroyed(coreRoom, targetObj, this);
						ClearAttackObjectTarget();
					}
				}
				else if (isDoor && targetObj.DoorHp > 0f)
				{
					// 고정 초당 데미지(코어와 동일한 설계) — DoorSystem.DoorAttackDamagePerSecond 참고.
					targetObj.DoorHp = Mathf.Max(0f, targetObj.DoorHp - DoorSystem.DoorAttackDamagePerSecond * deltaTime);
					targetObj.TimeSinceLastDamaged = 0f; // 자동 회복 지연 타이머 리셋(InteractableObject.TimeSinceLastDamaged 참고)

					// 체력 표시(코어와 동일, 위 주석 참고) — 남은 체력 비율을 그대로 채움비로 쓴다.
					float doorProgress = targetObj.DoorMaxHp > 0f ? targetObj.DoorHp / targetObj.DoorMaxHp : 0f;
					Session.GetObjectVisual(targetPos)?.GetComponent<ObjectProgressBarVisual>()?.SetProgress(doorProgress, true);

					if (targetObj.DoorHp <= 0f)
					{
						Session.RemoveDoor(targetPos); // 문 오브젝트 자체를 제거 — 재설치 전까지 통로가 뚫린다.
						ClearAttackObjectTarget();
					}
				}
				else if (isTrap && targetObj.TrapHp > 0f)
				{
					// 함정 파괴 데미지 — 코어/문의 고정 초당 비율과 달리 인류 GOAP TrapDestroy가 쓰던
					// physicalAttack 비례 배율을 그대로 재사용한다(누가 부수든 난이도는 동일해야 함).
					targetObj.TrapHp = Mathf.Max(0f, targetObj.TrapHp - physicalAttack * ExplorationMath.TrapDestroyDamagePerSecondPerAttack * deltaTime);

					float trapProgress = targetObj.TrapMaxHp > 0f ? targetObj.TrapHp / targetObj.TrapMaxHp : 0f;
					Session.GetObjectVisual(targetPos)?.GetComponent<ObjectProgressBarVisual>()?.SetProgress(trapProgress, true);

					if (targetObj.TrapHp <= 0f)
					{
						Session.CollectObject(targetPos); // 함정 오브젝트 자체를 제거.
						ClearAttackObjectTarget();
					}
				}
				else
				{
					ClearAttackObjectTarget(); // 이미 파괴됐거나 해당 없는 오브젝트
				}
			}
			else
			{
				ClearAttackObjectTarget();
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
					foreach (Unit u in AIState.unitsReactingToMe)
					{
						if (u == null) continue;
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
					foreach (Unit u in AIState.unitsReactingToMe)
					{
						if (u == null) continue;
						u.AIState.reactedAttackers.Remove(this);
						if (u.AIState.reactingAttacker == this)
						{
							u.AIState.reactingAttacker = null;
							u.AIState.reactingThreat   = null;
						}
					}
					AIState.unitsReactingToMe.Clear();
				}
			}
		}

		for (int i = 0; i < CombatState.State.skillCooldowns.Length; i++)
		{
			if (CombatState.State.skillCooldowns[i] > 0f) CombatState.State.skillCooldowns[i] -= deltaTime;
		}
	}

	public override void OnThreatDetected(List<ThreatTileData> threats)
	{
		if (StatusEffects.State.stunDuration > 0f) return;

		foreach (var threat in threats)
		{
			Unit attacker = FindAttackerFromThreat(threat);
			if (attacker == null) continue;
			if (AIState.reactedAttackers.Contains(attacker)) continue;

			AIState.reactedAttackers.Add(attacker);
			AIState.reactingThreat        = threat;
			AIState.reactingAttacker      = attacker;
			attacker.AIState.unitsReactingToMe.Add(this);
			OnReactToThreat(attacker, threat);
		}
	}

	public override void OnReactToThreat(Unit attacker, ThreatTileData threat)
	{
		// "정지"/"제자리 공격"/고정 유닛은 완전 무반응이라 회피/블링크도 발동하지 않는다 — 이 경로는
		// UnitFSM 바깥(GameSession 위협 감지 루프)에서 직접 호출되므로 여기서 별도로 막아야 한다.
		if (isHalted || isStandGroundAttack || isImmobile) return;

		if (attacker != null)
		{
			AIState.reactedAttackers.Add(attacker);
			attacker.AIState.unitsReactingToMe.Add(this);
		}
		DefenseSystem.EvaluateEarlyReaction(this, attacker, threat);
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
		// B: castingUnits는 isCastingAttack=true 유닛만 포함 → Session.units 전체 순회 불필요
		foreach (Unit u in Session.castingUnits)
		{
			if (u == null) continue;
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


