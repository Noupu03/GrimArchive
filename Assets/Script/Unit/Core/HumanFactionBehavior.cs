using UnityEngine;

public class HumanFactionBehavior : IFactionBehavior
{
	public bool IsEnemy(IFactionBehavior other)
	{
		return other is PlayerMonsterBehavior || other is WildMonsterBehavior;
	}

	public void OnUpdate(Unit unit) {}

	public void OnDeath(Unit unit, Unit killer)
	{
		// 명세서 v0.2 1·2·25-1항: 처치 즉시 자원을 지급하지 않으며, 생성된 시체를 통합 정제시설에 입고·정제 완료해야 보상을 획득한다.
	}
}
