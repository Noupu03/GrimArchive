using UnityEngine;

public class WildMonsterBehavior : IFactionBehavior
{
	public bool IsEnemy(IFactionBehavior other) { return other is HumanFactionBehavior || other is PlayerMonsterBehavior; }

	public void OnUpdate(Unit unit)
	{
		// 야생 몬스터는 자신이 속한 방 내부에서만 행동 (방 밖 추격 안함)
	}

	public void OnDeath(Unit unit, Unit killer)
	{
		// 명세서 v0.2 1·8·25-1항: 처치 즉시 자원은 지급하지 않고 시체 정제로만 획득하며,
		// 플레이어 몬스터가 처치한 경우에만 해당 야생 몬스터 종류의 생산 해금을 기록한다.
		if (killer != null && killer.FactionBehavior is PlayerMonsterBehavior && unit?.unitType != null)
		{
			ResourceManager.Instance?.UnlockMonsterSpecies(unit.unitType.GetType().Name);
		}
	}
}
