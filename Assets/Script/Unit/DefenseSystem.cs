using UnityEngine;

public static class DefenseSystem
{
	public static void EvaluateDefense(Unit defender, Unit attacker, ThreatTileData threat)
	{
		if (defender == null || attacker == null) return;

		// 1. 방어 후보 생성
		bool canBlock = true;
		bool canDodge = true;
		bool canBlink = false;
		bool canParry = true;

		// 2. 지금은 단순 우선순위 테스트용
		if (canDodge)
		{
			Debug.Log($"{defender.unitType.typeName} 회피 선택");
			return;
		}

		if (canBlock)
		{
			Debug.Log($"{defender.unitType.typeName} 막기 선택");
			return;
		}

		if (canParry)
		{
			Debug.Log($"{defender.unitType.typeName} 패링 선택");
			return;
		}

		// 3. 아무것도 못하면 직격
		defender.ApplyDirectDamage(attacker);
	}
}