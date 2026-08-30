using System.Collections.Generic;
using UnityEngine;

// 파티 — 인류 유닛들이 함께 웨이브(던전)에 입장하는 단위. 생존자 전역 반영/파티 전멸/정보 오차
// 공유 등 "파티"를 전제로 하는 가중치 로직의 실체. GameSession.CreateParty()로 생성·등록된다.
public class Party
{
	public string Id;
	public string Name;
	public readonly List<Human> Members = new List<Human>();

	// 리더·명령 체계 문서가 아직 없어 임시로 추가한 최소 리더 개념 — "전투 종료 후 경계 10초 → 리더가
	// 집결 명령 생성 → 파티원이 집결"이 요구하는 최소 기능만 채운다. 생존 파티원 중 leadership 최댓값을
	// 리더로 삼고, 리더가 죽으면 GameSession이 재선정한다.
	public Human Leader;

	// 집결의 최소 버전 — 전투 종료 후 경계 10초가 끝나면 리더가 자기 위치를 집결지로 지정한다(리더
	// 명령 전파 체계가 없어 "즉시 전 파티원이 아는" 것으로 근사). Goal_Wait이 이 값을 읽는다.
	public Vector2Int? RallyPoint;
	public bool IsRallyActive;

	// 생존 파티원 중 leadership 최댓값을 리더로 재선정한다 — 리더가 없거나 죽었을 때 GameSession이 호출한다.
	public void AssignLeaderIfNeeded()
	{
		if (Leader != null && Leader.hp > 0) return;

		Human best = null;
		float bestLeadership = float.MinValue;
		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0) continue;
			if (m.leadership > bestLeadership) { bestLeadership = m.leadership; best = m; }
		}
		Leader = best;
	}

	// 이번 웨이브에 이 파티와 함께 소환된 몬스터 목록 — 전부 죽으면 "웨이브 클리어"로 보고 생존자
	// 전역 반영(HumanKnowledgeBase.OnWaveEnd)을 트리거한다. 여러 Party가 같은 리스트 참조를 공유하는
	// 설계라 통짜 재대입은 스폰 시점 한 곳에서만 허용한다.
	public List<Monster> WaveMonsters { get; private set; } = new List<Monster>();
	public void SetWaveMonsters(List<Monster> monsters) => WaveMonsters = monsters;

	// 이 파티의 이번 웨이브가 이미 종료 처리(전멸/클리어)됐는지 — 유닛 사망마다 재확인하므로 중복 트리거를 막아야 한다.
	public bool WaveEnded;

	// 파티원 사망 발견: 시체 오브젝트 Id → 그 사망 사건의 진행 상태. PartyDeathSystem이 읽고 쓴다.
	// WaveSpawner가 웨이브마다 새 Party를 만들므로 별도 정리 없이 웨이브 경계에서 자연히 초기화된다.
	public readonly Dictionary<string, PartyDeathRecord> DeathRecords = new Dictionary<string, PartyDeathRecord>();

	// 함정 오브젝트 Id → 그 함정의 발견자/선정 해제 유닛 조율 상태. TrapPartySystem이 읽고 쓴다.
	public readonly Dictionary<string, TrapPartyCoordination> TrapCoordinations = new Dictionary<string, TrapPartyCoordination>();

	public Party(string id, string name)
	{
		Id = id;
		Name = name;
	}

	// 13-1장: 파티 전체가 사망하면 파티 전멸.
	public bool IsWiped
	{
		get
		{
			if (Members.Count == 0) return false;
			foreach (var m in Members)
				if (m != null && m.Health.hp > 0) return false;
			return true;
		}
	}

	// 6장: 이 파티와 함께 들어온 몬스터가 전부 죽으면 웨이브 클리어로 본다.
	public bool IsWaveCleared
	{
		get
		{
			if (WaveMonsters.Count == 0) return false;
			foreach (var m in WaveMonsters)
				if (m != null && m.Health.hp > 0) return false;
			return true;
		}
	}

	// 생존자 전역 반영(OnWaveEnd)에 넘길 생존자 목록. "생존/후퇴/도주 성공 유닛"을 이 샘플 구현에서는
	// "웨이브 종료 시점에 살아있는 파티원"으로 근사한다 — 후퇴/도주 판정 시스템이 아직 없다.
	public List<Unit> GetSurvivors()
	{
		var survivors = new List<Unit>();
		foreach (var m in Members)
			if (m != null && m.Health.hp > 0) survivors.Add(m);
		return survivors;
	}

	// 전투 종료 후 10초 경계 스윕이 끝난 유닛이(UnitFunction.OnUpdate) 호출한다. 파티 전체가 전투/
	// 전투직후 스윕에서 완전히 벗어났을 때만 실제로 집결을 시작하고, 아직 남은 파티원이 있으면 그
	// 유닛이 끝날 때 재시도된다. 리더 명령 전파 체계가 없어 "즉시 전 파티원이 리더 위치를 안다"로 근사한다.
	public void TryStartRally()
	{
		if (IsRallyActive) return;

		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0) continue;
			if (m.personalSpottedEnemies.Count > 0) return; // 아직 전투 중인 파티원 있음(명시적 전투 플래그 부재로 근사)
			if (m.currentAlertSearch != null && m.currentAlertSearch.IsPostCombatSweep) return; // 아직 스윕 중
		}

		AssignLeaderIfNeeded();
		if (Leader == null) return;

		RallyPoint = Leader.position;
		IsRallyActive = true;

		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0 || m.currentWait != null) continue;
			m.currentWait = new WaitState { Reason = WaitReason.AwaitingPartyAtRallyPoint, WaitPosition = RallyPoint };
		}
	}

	// TacticalFSMState.ExecuteWait이 유닛 하나가 집결지에 도착해 currentWait을 비울 때마다 호출한다.
	// 아직 집결 대기 중인 파티원이 남아있으면 유지, 전원 도착했으면 집결을 종료한다(별도 마칭
	// 포메이션 시스템이 없어 "다음 목표 수행"으로 자연히 넘어가는 것을 대체로 본다).
	public void CheckRallyComplete()
	{
		if (!IsRallyActive) return;
		foreach (var m in Members)
		{
			if (m == null || m.hp <= 0) continue;
			if (m.currentWait != null && m.currentWait.Reason == WaitReason.AwaitingPartyAtRallyPoint) return;
		}
		IsRallyActive = false;
		RallyPoint = null;
	}
}
