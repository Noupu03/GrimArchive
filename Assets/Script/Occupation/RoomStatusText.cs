// 방 라벨(인구수 N/M)에 덧붙이는 상태 줄 — 인류 거점 단계·공사 진행(OccupationSystem)과 클리어 진행의 남은 적 수(RoomClearSystem)를 보여 준다(코어 점령 v0.2 19장).
// GameSession의 라벨 갱신이 매 프레임 키만 비교하고, 키가 바뀔 때만 문자열을 만든다.
public static class RoomStatusText
{
	public static int Key(GameSession session, Room room)
	{
		int key = session.Occupation != null ? session.Occupation.RoomStatusKey(room) : 0;
		if (session.RoomClear != null && session.RoomClear.TryGetRemainingEnemies(room, out int hostile)) key |= (hostile + 1) << 12;
		return key;
	}

	public static string Compose(GameSession session, Room room)
	{
		string text = $"{room.CurrentPopulation}/{room.MaxPopulation}";
		string baseLine = session.Occupation != null ? session.Occupation.DescribeRoom(room) : string.Empty;
		if (baseLine.Length > 0) text += "\n" + baseLine;
		if (session.RoomClear != null && session.RoomClear.TryGetRemainingEnemies(room, out int hostile)) text += $"\n남은 적 {hostile}";
		return text;
	}
}
