using System.Collections.Generic;
using UnityEngine;

// 파티 — 인류 유닛들이 함께 웨이브(던전)에 입장하는 단위. 연산공식 문서 6장(생존자 전역 반영)/
// 13장(파티 전멸/전멸 흔적)/23장(파티 입장 시 정보 오차 공유)이 "파티"를 전제로 하는 로직의 실체.
// GameSession.CreateParty()로 생성되고, GameSession.parties에 등록된다.
public class Party
{
	public string Id;
	public string Name;
	public readonly List<Human> Members = new List<Human>();

	// 08_리더·명령·파티통제 문서가 아직 없어(시야인지반응_GOAP전체목표_마스터우선순위표_2026-07-22.txt
	// 3절 C-11 참고) 임시로 추가한 최소 리더 개념 — "리더 명령" 전체 체계는 없고, 03문서 4-8장/11장
	// (전투 종료 후 경계 10초 → 리더가 집결 명령 생성 → 파티원이 집결)이 요구하는 최소 기능만
	// 채운다. 생존 파티원 중 leadership(Unit.cs 파생 스탯) 최댓값을 리더로 삼고, 리더가 죽으면
	// GameSession이 재선정한다(AssignLeaderIfNeeded 참고).
	public Human Leader;

	// 03문서 4-8장/11장 집결의 최소 버전 — 전투 종료 후 경계 10초가 끝나면 리더가 자기 위치를
	// 집결지로 지정한다(리더 명령 발행/전파 체계가 없어 "즉시 전 파티원이 아는" 것으로 근사).
	// Goal_Wait의 AwaitingPartyAtRallyPoint 케이스가 이 값을 읽는다.
	public Vector2Int? RallyPoint;
	public bool IsRallyActive;

	// 생존 파티원 중 leadership 최댓값을 리더로 재선정한다 — 리더가 없거나(최초 생성) 죽었을 때
	// GameSession이 호출한다. 동률이면 Members 순서상 먼저 오는 쪽(안정적 우선순위, 별도 규칙 없음).
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

	// 이번 웨이브(던전 진입)에 이 파티와 함께 소환된 몬스터 목록 — 전부 죽으면 "웨이브 클리어"로
	// 보고 6장 생존자 전역 반영(HumanKnowledgeBase.OnWaveEnd)을 트리거한다.
	// WaveSpawner.SpawnWave()가 파티와 함께 스폰된 몬스터를 채워준다.
	public List<Monster> WaveMonsters = new List<Monster>();

	// 이 파티의 이번 웨이브가 이미 종료 처리(전멸 또는 클리어)됐는지 — GameSession이 유닛 사망마다
	// 판정을 재확인하므로, 한 번 처리한 뒤에는 중복 트리거를 막아야 한다.
	public bool WaveEnded;

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
				if (m != null && m.hp > 0) return false;
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
				if (m != null && m.hp > 0) return false;
			return true;
		}
	}

	// 6장 생존자 전역 반영(OnWaveEnd)에 넘길 생존자 목록. 문서의 "생존, 후퇴, 도주에 성공한 유닛"을
	// 이 샘플 구현에서는 "웨이브 종료(클리어) 시점에 살아있는 파티원"으로 근사한다 — 후퇴/도주를
	// 판정할 별도 시스템이 아직 없다.
	public List<Unit> GetSurvivors()
	{
		var survivors = new List<Unit>();
		foreach (var m in Members)
			if (m != null && m.hp > 0) survivors.Add(m);
		return survivors;
	}
}
