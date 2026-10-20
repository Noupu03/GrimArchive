using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Haare.Util.Logger;

// 방 클리어 조회·플레이어 방 확보(세션 연결부) — 순수 판정은 RoomClearMath. 확보 = 방 클리어(적대 생존 0, 은신 포함) + 담당 범위의 부산물 장치 없음(장치 결합 경로는 아직 없다). 별도 점령 상호작용은 없고 이 시스템이 0.5초마다 판정한다(코어 점령 v0.2 1·11장).
public class RoomClearSystem
{
	private const float TickIntervalSeconds = 0.5f;

	private IObjectResolver _resolver;
	private GameSession Session => _cachedSession ??= _resolver.Resolve<GameSession>();
	private GameSession _cachedSession;
	private float _timer;
	private bool _defeatAnnounced;
	private readonly Dictionary<Room, (int hostile, int player)> _counts = new Dictionary<Room, (int, int)>();

	[Inject]
	public void Construct(IObjectResolver resolver) => _resolver = resolver;

	// 지금 이 방에 적대 생존 유닛이 없는가(은신 포함) — 코어 활성화·수복 조건이 묻는다.
	public bool IsCleared(Room room)
	{
		if (room == null || Session == null) return false;
		foreach (var u in Session.units)
			if (TryClassify(u, out Room here, out FactionType? faction) && here == room && RoomClearMath.IsHostileToPlayer(faction)) return false;
		return true;
	}

	// 플레이어가 클리어를 진행 중인 방(아직 확보 못 했고 플레이어 유닛이 서 있음)의 남은 적 수 — 마지막 틱 기준이다(19장 "클리어 진행은 남은 적 기준으로 표시").
	public bool TryGetRemainingEnemies(Room room, out int hostile)
	{
		hostile = 0;
		if (room.RoomFaction == FactionType.Player || !_counts.TryGetValue(room, out var c) || !RoomClearMath.ShouldEvaluate(c.player)) return false;
		hostile = c.hostile;
		return true;
	}

	public void Tick(float deltaTime)
	{
		_timer += deltaTime;
		if (_timer < TickIntervalSeconds) return;
		_timer = 0f;
		if (Session?.units == null || Session.OffenseProcessor == null) return;

		CountAllRooms();
		foreach (var kv in _counts)
		{
			Room room = kv.Key;
			if (room.RoomFaction == FactionType.Player || !RoomClearMath.ShouldEvaluate(kv.Value.player) || !RoomClearMath.IsCleared(kv.Value.hostile)) continue;

			FactionType previous = room.RoomFaction;
			if (!Session.OffenseProcessor.ApplyRoomOwnership(room, FactionType.Player)) continue;
			LogHelper.Log(LogHelper.GAME, $"[방 확보] F{room.Floor} {room.RoomName}: 적대 생존 0 → 플레이어 확보(이전 {previous})");
			NoticeCenter.Instance?.PushMomentary($"{room.Floor}층 {room.RoomName}을(를) 확보했습니다.", NoticeCenter.QuestCompleteColor);
		}
		CheckDefeat();
	}

	private void CountAllRooms()
	{
		_counts.Clear();
		foreach (var u in Session.units)
		{
			if (!TryClassify(u, out Room room, out FactionType? faction)) continue;
			_counts.TryGetValue(room, out var c);
			if (faction == FactionType.Player) c.player++;
			else if (RoomClearMath.IsHostileToPlayer(faction)) c.hostile++;
			_counts[room] = c;
		}
	}

	// 플레이어가 확보한 방이 0개면 패배한다(1장). 문서가 패배 시 처리(종료 화면·재시작)를 정하지 않아 알림과 로그만 남기고 게임은 멈추지 않는다 — 확보를 잃는 경로(부산물 장치)가 아직 없어 지금은 도달하지 않는다.
	private void CheckDefeat()
	{
		if (_defeatAnnounced) return;
		foreach (var room in Session.allRooms)
			if (room != null && room.Floor >= 1 && room.RoomFaction == FactionType.Player) return;

		_defeatAnnounced = true;
		LogHelper.Warning(LogHelper.GAME, "[패배 조건] 플레이어가 확보한 방이 0개입니다.");
		NoticeCenter.Instance?.PushMomentary("확보한 방이 없습니다 — 패배 조건에 도달했습니다.", NoticeCenter.WarningColor);
	}

	private bool TryClassify(Unit u, out Room room, out FactionType? faction)
	{
		room = null;
		faction = null;
		if (u == null || u.hp <= 0f || u.currentFloor < 1) return false;
		if (!Session.roomGrid.TryGetValue(new Vector3Int(u.position.x, u.position.y, u.currentFloor), out room) || room == null) return false;
		faction = OffenseProcessor.MapToRoomFaction(u.FactionBehavior);
		return true;
	}
}
