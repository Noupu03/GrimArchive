using System;
using UnityEngine;

// 판단에 쓸 수 있는 HP 정보의 범위(리더 명령 6항·7-표1, L14).
//  · 적의 HP 비율: 도감 이해도로 HP 정보가 해금되고 "현재 직접 확인"한 경우에만 쓴다. 과거 HP를 현재값으로 삼지 않는다.
//  · 인류 아군의 HP 비율: 모든 인류 유닛이 직접 확인한 아군의 HP는 도감 해금 없이 안다. 시야 밖 HP는 자동 갱신하지 않는다.
public static class EnemyInfoAccess
{
	// 도감 HP 정보 해금 판정 — 도감 설계(해금 단계·재잠금 시 사용 조건)가 없어 기본 '해금됨'이다. 도감이 생기면 이 한 곳만 바꾼다(사용자 확정 2026-11-01). 테스트가 교체한다.
	public static Func<Unit, Unit, bool> HpInfoUnlockedProvider = (observer, enemy) => true;

	public static bool IsEnemyHpInfoUnlocked(Unit observer, Unit enemy) => HpInfoUnlockedProvider(observer, enemy);

	// 적의 현재 HP 비율 — 관찰자가 지금 직접 보고 있고(personalSpottedEnemies는 UpdateFOV가 매 패스 새로 채운다) HP 정보가 해금된 경우에만.
	public static bool TryGetEnemyHpRatio(Human observer, Unit enemy, out float ratio)
	{
		ratio = 0f;
		if (observer == null || enemy == null || enemy.hp <= 0f || enemy.maxHp <= 0f) return false;
		if (!observer.personalSpottedEnemies.Contains(enemy)) return false;
		if (!IsEnemyHpInfoUnlocked(observer, enemy)) return false;
		ratio = enemy.hp / enemy.maxHp;
		return true;
	}

	// 같은 파티 인류 아군의 현재 HP 비율 — 자기 자신이거나 지금 직접 보고 있을 때만 안다.
	public static bool TryGetAllyHpRatio(Human observer, Human ally, out float ratio)
	{
		ratio = 0f;
		if (observer == null || ally == null || ally.hp <= 0f || ally.maxHp <= 0f) return false;
		if (observer != ally && !observer.visiblePartyMembers.Contains(ally)) return false;
		ratio = ally.hp / ally.maxHp;
		return true;
	}
}
