#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

// ========================================================================
// 07_전파·소리·간접입력 문서(개념 v0.2 / 연산공식 v0.2)의 숫자 예시를 그대로 고정하는 테스트.
// VisionSystemTests.cs와 동일한 컨벤션(NUnit, #if UNITY_INCLUDE_TESTS). PropagationMath는 순수
// 함수라 Unity 오브젝트 없이 직접 검증 가능하다.
// ========================================================================

public class PropagationSystemTests
{
	// ── 1장. 공간 판정 ──
	[Test]
	public void SameSpace_RoomIdEquality()
	{
		Assert.IsTrue(PropagationMath.SameSpace(3, 3));
		Assert.IsFalse(PropagationMath.SameSpace(3, 4));
		Assert.IsFalse(PropagationMath.SameSpace(-1, -1)); // 방 밖(-1)은 공간 자체가 아님
	}

	// ── 1-2장. 거리 계산: 장애물 없음 → 직선 경로 그대로 성공 ──
	[Test]
	public void SpaceDistance_NoObstacle_FindsDirectPath()
	{
		bool ok = PropagationMath.TryGetSpaceDistance(Vector2Int.zero, new Vector2Int(2, 0), 3, _ => true, out int dist);
		Assert.IsTrue(ok);
		Assert.AreEqual(2, dist);
	}

	// ── 1-2장 예시: 직선거리 2칸이지만 벽을 돌아 실제 최단 경로가 4칸이면 범위3은 실패, 범위4는 성공 ──
	[Test]
	public void SpaceDistance_WallDetour_MatchesDocExample()
	{
		// (1,0)과 (1,1)을 막아 (0,0)→(2,0) 직선을 봉쇄, 위로 돌아가는 경로만 허용.
		bool IsWalkable(Vector2Int p) => !(p == new Vector2Int(1, 0) || p == new Vector2Int(1, 1));

		bool okRange3 = PropagationMath.TryGetSpaceDistance(Vector2Int.zero, new Vector2Int(2, 0), 3, IsWalkable, out _);
		Assert.IsFalse(okRange3);

		bool okRange4 = PropagationMath.TryGetSpaceDistance(Vector2Int.zero, new Vector2Int(2, 0), 4, IsWalkable, out int dist4);
		Assert.IsTrue(okRange4);
		Assert.AreEqual(4, dist4);
	}

	[Test]
	public void SpaceDistance_Unreachable_Fails()
	{
		bool ok = PropagationMath.TryGetSpaceDistance(Vector2Int.zero, new Vector2Int(5, 5), 3, _ => true, out _);
		Assert.IsFalse(ok);
	}

	// ── 2-1장. 인류 전파 범위 = 기본 3칸 + 카리스마 보정(25당 +1, 100에서 +4) ──
	[Test]
	public void PropagationRange_CharismaTable()
	{
		Assert.AreEqual(3, PropagationMath.PropagationRange(0));
		Assert.AreEqual(3, PropagationMath.PropagationRange(24));
		Assert.AreEqual(4, PropagationMath.PropagationRange(25));
		Assert.AreEqual(4, PropagationMath.PropagationRange(49));
		Assert.AreEqual(5, PropagationMath.PropagationRange(50));
		Assert.AreEqual(6, PropagationMath.PropagationRange(75));
		Assert.AreEqual(7, PropagationMath.PropagationRange(100));
	}

	// ── 4장. 소리 기본 범위 6종 ──
	[Test]
	public void SoundBaseRange_Table()
	{
		Assert.AreEqual(2, PropagationMath.SoundBaseRange(SoundType.Movement));
		Assert.AreEqual(3, PropagationMath.SoundBaseRange(SoundType.AttackExecution));
		Assert.AreEqual(5, PropagationMath.SoundBaseRange(SoundType.HitImpact));
		Assert.AreEqual(6, PropagationMath.SoundBaseRange(SoundType.HitScream));
		Assert.AreEqual(7, PropagationMath.SoundBaseRange(SoundType.Death));
		Assert.AreEqual(5, PropagationMath.SoundBaseRange(SoundType.TrapActivation));
	}

	// ── 4-1장. 피격 비명 발생 기준: 최종 HP 감소량 ≥ 최대 HP × 10% ──
	[Test]
	public void HitScream_ThresholdExample()
	{
		Assert.IsTrue(PropagationMath.IsHitScreamTriggered(10f, 100f));
		Assert.IsFalse(PropagationMath.IsHitScreamTriggered(9.99f, 100f));
	}

	// ── 5장. 소리 감지 범위 = 소리 기본 범위 + 감지 보정(25당 +1) ──
	[Test]
	public void SoundDetectionRange_HumanNoAlert()
	{
		// 이동음(기본2) + 감지50(+2) = 4
		Assert.AreEqual(4, PropagationMath.SoundDetectionRange(SoundType.Movement, 50f, isAlert: false, isMonster: false));
	}

	// ── 5-1장. 경계 상태: 감지 = min(기본+20, 100)을 먼저 적용한 뒤 범위 보정 ──
	[Test]
	public void SoundDetectionRange_AlertAppliesBonusBeforeBreakpoint()
	{
		// 기본감지 10 + 20 = 30 → 25~49 구간(+1). 경계 아니면 10은 0~24 구간(+0)이라 값이 달라야 한다.
		int alert = PropagationMath.SoundDetectionRange(SoundType.Movement, 10f, isAlert: true, isMonster: false);
		int normal = PropagationMath.SoundDetectionRange(SoundType.Movement, 10f, isAlert: false, isMonster: false);
		Assert.AreEqual(3, alert);  // 2(기본) + 1(30→+1구간)
		Assert.AreEqual(2, normal); // 2(기본) + 0
	}

	[Test]
	public void SoundDetectionRange_AlertCapsAt100()
	{
		Assert.AreEqual(100f, PropagationMath.AlertFinalDetection(90f), 0.001f);
	}

	// ── 5-2장. 몬스터 청각 보정 +2칸 ──
	[Test]
	public void SoundDetectionRange_MonsterHearingBonus()
	{
		int monster = PropagationMath.SoundDetectionRange(SoundType.Movement, 0f, isAlert: false, isMonster: true);
		int human = PropagationMath.SoundDetectionRange(SoundType.Movement, 0f, isAlert: false, isMonster: false);
		Assert.AreEqual(human + 2, monster);
	}

	// ── 6장. 추정 지역 반경(전투 관련 소리 2칸, 함정 작동음 1칸, 이동음 없음) ──
	[Test]
	public void EstimatedAreaRadius_Table()
	{
		Assert.AreEqual(2, PropagationMath.EstimatedAreaRadius(SoundType.AttackExecution));
		Assert.AreEqual(2, PropagationMath.EstimatedAreaRadius(SoundType.HitImpact));
		Assert.AreEqual(2, PropagationMath.EstimatedAreaRadius(SoundType.HitScream));
		Assert.AreEqual(2, PropagationMath.EstimatedAreaRadius(SoundType.Death));
		Assert.AreEqual(1, PropagationMath.EstimatedAreaRadius(SoundType.TrapActivation));
		Assert.AreEqual(0, PropagationMath.EstimatedAreaRadius(SoundType.Movement));
		Assert.IsFalse(PropagationMath.HasEstimatedArea(SoundType.Movement));
		Assert.IsTrue(PropagationMath.HasEstimatedArea(SoundType.Death));
	}

	// ── 7-1장. 소리 내부 우선순위(사망/비명=1 최우선 ... 이동음=5 최하) ──
	[Test]
	public void SoundPriorityRank_Order()
	{
		Assert.AreEqual(1, PropagationMath.SoundPriorityRank(SoundType.Death));
		Assert.AreEqual(1, PropagationMath.SoundPriorityRank(SoundType.HitScream));
		Assert.AreEqual(2, PropagationMath.SoundPriorityRank(SoundType.HitImpact));
		Assert.AreEqual(3, PropagationMath.SoundPriorityRank(SoundType.TrapActivation));
		Assert.AreEqual(4, PropagationMath.SoundPriorityRank(SoundType.AttackExecution));
		Assert.AreEqual(5, PropagationMath.SoundPriorityRank(SoundType.Movement));
	}

	[Test]
	public void ShouldReplaceSound_HigherPriorityWins_ThenCloserDistance()
	{
		Assert.IsTrue(PropagationMath.ShouldReplaceSound(candidateRank: 1, candidateDist: 10f, currentRank: 5, currentDist: 1f));
		Assert.IsFalse(PropagationMath.ShouldReplaceSound(candidateRank: 5, candidateDist: 1f, currentRank: 1, currentDist: 10f));
		Assert.IsTrue(PropagationMath.ShouldReplaceSound(candidateRank: 2, candidateDist: 3f, currentRank: 2, currentDist: 5f));
		Assert.IsFalse(PropagationMath.ShouldReplaceSound(candidateRank: 2, candidateDist: 5f, currentRank: 2, currentDist: 3f));
	}

	// 7-2장 마지막 타이브레이크: 순위·거리가 모두 같으면 현재 시야 방향에 더 가까운 소리를 우선한다.
	[Test]
	public void ShouldReplaceSound_SameRankAndDistance_PrefersCloserToCurrentViewDirection()
	{
		Assert.IsTrue(PropagationMath.ShouldReplaceSound(
			candidateRank: 2, candidateDist: 5f, currentRank: 2, currentDist: 5f,
			candidateDirStepDist: 1, currentDirStepDist: 3));
		Assert.IsFalse(PropagationMath.ShouldReplaceSound(
			candidateRank: 2, candidateDist: 5f, currentRank: 2, currentDist: 5f,
			candidateDirStepDist: 3, currentDirStepDist: 1));
		// 방향까지 동률이면 기존(현재 확인 중) 소리를 유지한다.
		Assert.IsFalse(PropagationMath.ShouldReplaceSound(
			candidateRank: 2, candidateDist: 5f, currentRank: 2, currentDist: 5f,
			candidateDirStepDist: 2, currentDirStepDist: 2));
	}

	// ── 9-1/9-2장. 근거리 즉시 전투(2칸), 위험도 단계별 합류 대기 ──
	[Test]
	public void RequiresJoinWait_Table()
	{
		Assert.IsFalse(PropagationMath.RequiresJoinWait(DangerStage.StageMax, distanceTiles: 2)); // 근거리
		Assert.IsFalse(PropagationMath.RequiresJoinWait(null, distanceTiles: 5));                 // 미확인
		Assert.IsFalse(PropagationMath.RequiresJoinWait(DangerStage.Stage0, distanceTiles: 5));
		Assert.IsFalse(PropagationMath.RequiresJoinWait(DangerStage.Stage1, distanceTiles: 5));
		Assert.IsTrue(PropagationMath.RequiresJoinWait(DangerStage.Stage2, distanceTiles: 5));
		Assert.IsTrue(PropagationMath.RequiresJoinWait(DangerStage.Stage3, distanceTiles: 5));   // "2단계 규칙 임시 적용"
		Assert.IsTrue(PropagationMath.RequiresJoinWait(DangerStage.StageMax, distanceTiles: 5));
	}

	// ── 17장. 공격 방향 간접입력: 광역/지면 영역만 방향 정보 없음 ──
	[Test]
	public void AttackShapeProvidesDirection_Table()
	{
		Assert.IsTrue(PropagationMath.AttackShapeProvidesDirection(AttackShape.Melee));
		Assert.IsTrue(PropagationMath.AttackShapeProvidesDirection(AttackShape.Projectile));
		Assert.IsFalse(PropagationMath.AttackShapeProvidesDirection(AttackShape.AreaGround));
	}

	// ========================================================================
	// 아래부터는 PropagationSystem(부수효과 있는 호출부) 테스트 — FSMBehaviorTests.cs와 동일 컨벤션으로
	// ScriptableObject.CreateInstance로 실제 Human/Monster를 만들어 검증한다. Knowledge는 [Inject] private
	// 필드라 DI 컨테이너 없이는 채워지지 않으므로, 테스트에서만 리플렉션으로 직접 주입한다.
	// ========================================================================

	private static void InjectKnowledge(Unit u, HumanKnowledgeBase kb)
	{
		typeof(Unit).GetField("_knowledgeBase", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
			.SetValue(u, kb);
	}

	// ── 2026-08-05 재설계: 소리는 발생 즉시(EmitSound가 범위 스캔까지 끝내고) OnSoundPerceived로
	// 통지되고, 이 이벤트가 곧바로 PendingSound를 세팅한다 — 예전처럼 활성 소리 목록을 매 틱 다시
	// 훑지 않는다는 걸 이벤트를 직접 발행해서 검증한다(EmitSound 자체는 GameSession/CreateMap 전체
	// 셋업이 필요해 이 테스트 범위 밖 — 실제 플레이 검증 항목으로 남겨둠). 7-2장 우선순위 유지/교체
	// 규칙도 함께 고정한다.
	[Test]
	public void OnSoundPerceived_SetsPendingSound_AndRespectsPriority()
	{
		var human = ScriptableObject.CreateInstance<Human>();
		human.position = Vector2Int.zero;

		// 낮은 우선순위(이동음, rank 5) 먼저 도착.
		PropagationSystem.OnSoundPerceived.OnNext(new PropagationSystem.SoundPerceivedEvent(
			human, SoundType.Movement, new Vector2Int(1, 0), 0, null, null, false, "INC_A"));
		Assert.AreEqual(SoundType.Movement, human.Propagation.PendingSound.Type);

		// 더 급한 소리(피격 비명, rank 1) 도착 — 교체돼야 한다.
		PropagationSystem.OnSoundPerceived.OnNext(new PropagationSystem.SoundPerceivedEvent(
			human, SoundType.HitScream, new Vector2Int(2, 0), 0, null, null, false, "INC_B"));
		Assert.AreEqual(SoundType.HitScream, human.Propagation.PendingSound.Type);

		// 다시 이동음이 와도 이미 더 급한 소리가 대기 중이므로 무시돼야 한다(7-2장 "기존 유지").
		PropagationSystem.OnSoundPerceived.OnNext(new PropagationSystem.SoundPerceivedEvent(
			human, SoundType.Movement, new Vector2Int(3, 0), 0, null, null, false, "INC_C"));
		Assert.AreEqual(SoundType.HitScream, human.Propagation.PendingSound.Type);
	}

	// ── 07문서 1장 "소리 감지: 인류/몬스터 모두 적용" 검증 중 발견한 갭 수정(2026-08-06) — 몬스터도
	// SoundPerceivedEvent의 Observer가 될 수 있고, PendingSound는 Propagation(base Unit 소유)이라
	// 인류와 동일하게 채워져야 한다.
	[Test]
	public void OnSoundPerceived_MonsterListener_SetsPendingSound()
	{
		var monster = ScriptableObject.CreateInstance<Monster>();
		monster.unitType = new MeleeTank();
		monster.position = Vector2Int.zero;

		PropagationSystem.OnSoundPerceived.OnNext(new PropagationSystem.SoundPerceivedEvent(
			monster, SoundType.Movement, new Vector2Int(1, 0), 0, null, null, false, "INC_MON_1"));

		Assert.AreEqual(SoundType.Movement, monster.Propagation.PendingSound.Type);
	}

	// TryPromotePendingSoundToAlert가 Human 전용이었을 때는 몬스터의 PendingSound가 채워져도
	// currentAlertSearch로 절대 승격되지 않아 TacticalFSMState.HasAlert가 몬스터에 대해 항상 실패했다
	// (07문서 1장 검증 중 발견). Unit으로 일반화한 뒤에는 인류와 동일하게 승격되어야 한다.
	[Test]
	public void TryPromotePendingSoundToAlert_PromotesForMonsterToo()
	{
		var monster = ScriptableObject.CreateInstance<Monster>();
		monster.unitType = new MeleeTank();
		monster.position = Vector2Int.zero;
		monster.Propagation.PendingSound = new PendingSoundReaction
		{
			Type = SoundType.Movement,
			SourcePosition = new Vector2Int(2, 0),
			HasEstimatedArea = false,
			ValidUntilTime = 9999f,
		};

		bool promoted = PropagationSystem.TryPromotePendingSoundToAlert(monster);

		Assert.IsTrue(promoted);
		Assert.IsNotNull(monster.currentAlertSearch);
		Assert.IsTrue(monster.currentAlertSearch.IsSoundResponse);
		Assert.AreEqual(SoundType.Movement, monster.currentAlertSearch.SoundKind);
	}

	// ── E_HIT_HEAVY_INDIRECT 연결(2026-08-05): 인지 판정 성공 시점(TryConfirmIndirectHit)에서만 기록되고,
	// 공격자를 아직 정확 인지하지 못한 상태에서는 기록되지 않는다.
	[Test]
	public void TryConfirmIndirectHit_RecordsOnlyWhenAttackerAccuratelyPerceived()
	{
		var kb = new HumanKnowledgeBase();
		var observer = ScriptableObject.CreateInstance<Human>();
		InjectKnowledge(observer, kb);

		var attacker = ScriptableObject.CreateInstance<Monster>();
		attacker.unitType = new MeleeTank();
		attacker.hp = 10f;

		var victim = ScriptableObject.CreateInstance<Human>();
		victim.hp = 10f;

		var alert = new AlertSearchState { IsSoundResponse = true, SoundKind = SoundType.HitImpact };
		observer.Propagation.PendingSound = new PendingSoundReaction
		{
			IsHeavyHit = true,
			Victim = victim,
			Attacker = attacker,
			IncidentId = "INC_HIT_1",
		};

		string dangerKey = PersonalWeightRecord.MakeKey("근접 탱커", WeightType.Danger);

		// 아직 공격자를 정확 인지하지 못한 상태 — 기록되면 안 됨.
		PropagationSystem.TryConfirmIndirectHit(observer, alert);
		Assert.IsFalse(observer.personalWeights.ContainsKey(dangerKey));

		// 정확 인지로 전환된 뒤에는 기록된다(weight_events.json E_HIT_HEAVY_INDIRECT danger +0.075).
		observer.Perception.State.perceptionRecords[attacker] = new PerceptionRecord { Outcome = PerceptionOutcome.AccuratePerception };
		PropagationSystem.TryConfirmIndirectHit(observer, alert);
		Assert.AreEqual(0.075f, observer.personalWeights[dangerKey].StoredValue, 0.0001f);
	}

	// ── E_MONSTER_KILL_INDIRECT 연결(2026-08-05): 인류에게 죽은 몬스터 시체만 확인 대상이고, 그 외
	// (야생끼리 등)는 발견해도 기록되지 않는다.
	[Test]
	public void OnMonsterCorpseDiscovered_OnlyRecordsWhenKilledByHuman()
	{
		var kb = new HumanKnowledgeBase();
		var discoverer = ScriptableObject.CreateInstance<Human>();
		InjectKnowledge(discoverer, kb);

		// danger(-0.125)는 개인 즉시 반영이 [0,999]로 클램프돼(WeightMath.DangerMin=0) 기록 여부를 못
		// 구분하므로, 클램프 영향이 없는 understanding(+0.25)으로 기록 여부를 확인한다.
		string understandingKey = PersonalWeightRecord.MakeKey("근접 탱커", WeightType.Understanding);

		var wildCorpse = new InteractableObject("corpse_wild", Vector3Int.zero, 0f, tags: new System.Collections.Generic.List<string> { "Object/Passable/Corpse", "Monster" });
		wildCorpse.MonsterKilledByHuman = false;
		wildCorpse.MonsterSpeciesKey = "근접 탱커";
		PropagationSystem.OnMonsterCorpseDiscovered(discoverer, wildCorpse);
		Assert.IsFalse(discoverer.personalWeights.ContainsKey(understandingKey));

		var humanKilledCorpse = new InteractableObject("corpse_hk", Vector3Int.zero, 0f, tags: new System.Collections.Generic.List<string> { "Object/Passable/Corpse", "Monster" });
		humanKilledCorpse.MonsterKilledByHuman = true;
		humanKilledCorpse.MonsterSpeciesKey = "근접 탱커";
		PropagationSystem.OnMonsterCorpseDiscovered(discoverer, humanKilledCorpse);
		Assert.AreEqual(0.25f, discoverer.personalWeights[understandingKey].StoredValue, 0.0001f);
	}

	// ── 07문서 13장(저장소 분리 + 최신성만으로 판단, 사용자 결정 2026-08-06) ──
	[Test]
	public void GetLatestKnownPosition_ReturnsFalse_WhenNeitherRecorded()
	{
		var observer = ScriptableObject.CreateInstance<Human>();
		var target = ScriptableObject.CreateInstance<Monster>();
		target.unitType = new MeleeTank();
		target.name = "target_none";

		bool found = PropagationSystem.GetLatestKnownPosition(observer, target, out _, out _);
		Assert.IsFalse(found);
	}

	[Test]
	public void GetLatestKnownPosition_UsesDirect_WhenOnlyDirectRecorded()
	{
		var observer = ScriptableObject.CreateInstance<Human>();
		var target = ScriptableObject.CreateInstance<Monster>();
		target.unitType = new MeleeTank();
		target.name = "target_direct";
		var directTile = new Vector3Int(3, 3, 0);

		observer.personalMap.ObserveMonster(target.name, directTile, 1f, 1f);

		bool found = PropagationSystem.GetLatestKnownPosition(observer, target, out var tile, out _);
		Assert.IsTrue(found);
		Assert.AreEqual(directTile, tile);
	}

	// 직접 정보가 더 오래됐으면 24장 PriorityRank(직접>간접 항상 우선)가 아니라 전파 정보가 이긴다 —
	// 13장이 24장과 다른 규칙(순수 최신성)임을 못박는 테스트.
	[Test]
	public void GetLatestKnownPosition_PrefersNewerPropagated_OverOlderDirect()
	{
		var observer = ScriptableObject.CreateInstance<Human>();
		var target = ScriptableObject.CreateInstance<Monster>();
		target.unitType = new MeleeTank();
		target.name = "target_newer_propagated";
		var directTile = new Vector3Int(1, 1, 0);
		var propagatedTile = new Vector3Int(5, 5, 0);

		observer.personalMap.ObserveMonster(target.name, directTile, 1f, 1f); // Timestamp = Time.time
		observer.Propagation.PropagatedInfo[target] = new PropagatedInfoRecord
		{
			LastKnownTile = propagatedTile,
			LastKnownTimestamp = 999f, // 직접 정보보다 확실히 최신
		};

		bool found = PropagationSystem.GetLatestKnownPosition(observer, target, out var tile, out var timestamp);
		Assert.IsTrue(found);
		Assert.AreEqual(propagatedTile, tile);
		Assert.AreEqual(999f, timestamp);
	}

	[Test]
	public void GetLatestKnownPosition_KeepsDirect_WhenNewerThanPropagated()
	{
		var observer = ScriptableObject.CreateInstance<Human>();
		var target = ScriptableObject.CreateInstance<Monster>();
		target.unitType = new MeleeTank();
		target.name = "target_direct_newer";
		var directTile = new Vector3Int(2, 2, 0);
		var propagatedTile = new Vector3Int(9, 9, 0);

		observer.Propagation.PropagatedInfo[target] = new PropagatedInfoRecord
		{
			LastKnownTile = propagatedTile,
			LastKnownTimestamp = -100f, // Time.time(항상 >=0)보다 확실히 오래됨
		};
		observer.personalMap.ObserveMonster(target.name, directTile, 1f, 1f);

		bool found = PropagationSystem.GetLatestKnownPosition(observer, target, out var tile, out _);
		Assert.IsTrue(found);
		Assert.AreEqual(directTile, tile);
	}
}
#endif
