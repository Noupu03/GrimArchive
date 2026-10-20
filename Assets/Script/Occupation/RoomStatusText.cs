// 방 상태 문구 — 방 라벨에는 플레이어 인원 제한(N/M)만 두고, 소유 진영·클리어 진행(남은 적)·인류 거점 단계와 공사 진행·코어 상태는 방 라벨을 클릭하면 뜨는 방 정보 패널(BuildingControlPanel)에서 조회한다(코어 점령 v0.2 19장).
public static class RoomStatusText
{
	public static string Population(Room room) => $"{room.CurrentPopulation}/{room.MaxPopulation}";

	public static string FactionName(FactionType faction) => faction switch
	{
		FactionType.Player => "플레이어",
		FactionType.Wild => "야생",
		FactionType.Human => "인류",
		_ => faction.ToString(),
	};

	// 플레이어의 방 확보 진행 — 확보했으면 완료, 플레이어 유닛이 서 있는 방이면 남은 적 수(은신 포함), 아니면 아직 시작 전.
	public static string ClearText(GameSession session, Room room)
	{
		if (room.RoomFaction == FactionType.Player) return "확보 완료";
		if (session.RoomClear != null && session.RoomClear.TryGetRemainingEnemies(room, out int hostile)) return $"진행 중 — 남은 적 {hostile}";
		return "미확보";
	}

	// 인류 거점 단계(미완공 공사 진행·기초 거점·방어 시설 공사·방어 거점) — 거점 공사가 없으면 "없음".
	public static string BaseText(GameSession session, Room room)
	{
		string text = session.Occupation != null ? session.Occupation.DescribeRoom(room) : string.Empty;
		return text.Length > 0 ? text : "없음";
	}

	// 코어가 있는 방만 상태를 돌려주고, 없으면 빈 문자열.
	public static string CoreText(GameSession session, Room room)
	{
		if (room.CoreObjectId == null || !session.objectGrid.TryGetValue(room.CorePosition, out InteractableObject core)) return string.Empty;
		return CoreMath.Describe(CoreMath.StatusOf(core.CoreEverActivated, core.CoreDestroyed));
	}
}
