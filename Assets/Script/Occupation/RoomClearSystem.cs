using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Haare.Util.Logger;

// 방 클리어 조회·확보 판정(세션 연결부) — 순수 판정은 RoomClearMath. GameSession과 같은 DI 싱글턴이 지연 조회한다(DoorSystem과 같은 패턴).
// 플레이어 방 확보(코어 점령 설계명세서 v0.2 1·11장) = 방 클리어(적대 생존 0, 은신 포함) + 담당 범위의 코어 부산물 장치 없음(장치 결합 경로는 아직 없다). 별도 점령 상호작용은 없고, 이 시스템이 주기적으로 판정해 확보한다.
public class RoomClearSystem
{
	private const float TickIntervalSeconds = 0.5f;

	private IObjectResolver _resolver;
	private GameSession Session => _cachedSession ??= _resolver.Resolve<GameSession>();
	private GameSession _cachedSession;
	private float _timer;
	private readonly Dictionary<Room, (int hostile, int player)> _counts = new Dictionary<Room, (int, int)>();

	[Inject]
	public void Construct(IObjectResolver resolver) => _resolver = resolver;

	// 방 안의 적대 생존 유닛 수와 플레이어 유닛 수를 센다. 방은 직사각형이 아닐 수 있어 Bounds가 아니라 roomGrid(층 포함 키)로 "이 방의 칸"인지 확인한다.
	public void Count(Room room, out int hostileAlive, out int playerUnits)
	{
		hostileAlive = 0;
		playerUnits = 0;
		var session = Session;
		if (room == null || session == null) return;

		foreach (var u in session.units)
		{
			if (!TryGetRoomAndFaction(session, u, out Room here, out FactionType? faction) || here != room) continue;
			if (faction == FactionType.Player) playerUnits++;
			else if (RoomClearMath.IsHostileToPlayer(faction)) hostileAlive++;
		}
	}

	public bool IsCleared(Room room)
	{
		Count(room, out int hostile, out _);
		return RoomClearMath.IsCleared(hostile);
	}

	// GameSession.UpdateProcess가 매 프레임 부른다. 0.5초마다 유닛을 한 번만 훑어 방별로 집계하고, 플레이어 유닛이 있고 적대 생존이 없는 방을 확보한다.
	public void Tick(float deltaTime)
	{
		_timer += deltaTime;
		if (_timer < TickIntervalSeconds) return;
		_timer = 0f;

		var session = Session;
		if (session?.units == null || session.OffenseProcessor == null) return;

		_counts.Clear();
		foreach (var u in session.units)
		{
			if (!TryGetRoomAndFaction(session, u, out Room room, out FactionType? faction)) continue;
			_counts.TryGetValue(room, out var c);
			if (faction == FactionType.Player) c.player++;
			else if (RoomClearMath.IsHostileToPlayer(faction)) c.hostile++;
			_counts[room] = c;
		}

		foreach (var kv in _counts)
		{
			Room room = kv.Key;
			if (room.RoomFaction == FactionType.Player) continue;
			if (!RoomClearMath.ShouldEvaluate(kv.Value.player) || !RoomClearMath.IsCleared(kv.Value.hostile)) continue;

			FactionType previous = room.RoomFaction;
			if (!session.OffenseProcessor.ApplyRoomOwnership(room, FactionType.Player)) continue;
			LogHelper.Log(LogHelper.GAME, $"[방 확보] F{room.Floor} {room.RoomName}: 적대 생존 0 → 플레이어 확보(이전 {previous})");
			NoticeCenter.Instance?.PushMomentary($"{room.Floor}층 {room.RoomName}을(를) 확보했습니다.", NoticeCenter.QuestCompleteColor);
		}
	}

	private static bool TryGetRoomAndFaction(GameSession session, Unit u, out Room room, out FactionType? faction)
	{
		room = null;
		faction = null;
		if (u == null || u.hp <= 0f || u.currentFloor < 1) return false;
		if (!session.roomGrid.TryGetValue(new Vector3Int(u.position.x, u.position.y, u.currentFloor), out room) || room == null) return false;
		faction = OffenseProcessor.MapToRoomFaction(u.FactionBehavior);
		return true;
	}
}
