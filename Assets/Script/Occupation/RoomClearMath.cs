// 방 클리어 판정(순수) — 코어 점령 설계명세서 v0.2 11장: 방 안의 적대 생존 유닛을 모두 처치하면 클리어한다(은신 유닛 포함). 함정·시체·시설 잔존은 클리어를 막지 않는다.
public static class RoomClearMath
{
	// 플레이어에게 적대적인 진영 — 야생·인류. 플레이어 몬스터와 방 소유 개념이 없는 진영(null)은 아니다.
	public static bool IsHostileToPlayer(FactionType? faction) => faction == FactionType.Wild || faction == FactionType.Human;

	public static bool IsCleared(int hostileAlive) => hostileAlive <= 0;

	// 플레이어 유닛이 한 명이라도 서 있는 방만 확보 판정 대상이다 — 아무도 가지 않은 빈 방이 저절로 확보되면 안 된다(11-2장 "플레이어가 목표 방 선택 이동·공격 명령" 순서도).
	public static bool ShouldEvaluate(int playerUnitsInRoom) => playerUnitsInRoom > 0;
}
