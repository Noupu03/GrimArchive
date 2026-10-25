using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Haare.Util.Logger;
using GrimArchive.Wave;

// 방 클리어 조회·플레이어 방 확보(세션 연결부) — 순수 판정은 RoomClearMath. 확보 = 방 클리어(적대 생존 0, 은신 포함) + 담당 범위의 부산물 장치 없음(장치 결합 경로는 아직 없다). 별도 점령 상호작용은 없고 이 시스템이 0.5초마다 판정한다(코어 점령 v0.2 1·11장).
public class RoomClearSystem
{
	private const float TickIntervalSeconds = 0.5f;

	private IObjectResolver _resolver;
	private GameSession Session => _cachedSession ??= _resolver.Resolve<GameSession>();
	private GameSession _cachedSession;
	private BuildingManager Buildings => _cachedBuildings ??= _resolver.Resolve<BuildingManager>();
	private BuildingManager _cachedBuildings;
	private float _timer;
	private bool _defeatAnnounced;
	private bool _hadSecuredRoom;
	private bool _hadAssets;
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

	// 패배(1장): 플레이어가 확보한 방이 0개면 인류 거점에 은신 유닛이 남아 있어도 게임 종료, 방이 있어도 유닛·건물이 모두 사라졌으면 진행할 수 없어 마찬가지다(판정은 DefeatMath).
	// 종료 화면은 DefeatScreen(rythoom 사망 화면 연출) — 확보를 잃는 경로(부산물 장치)가 아직 없어 지금 실제로 닿는 건 유닛·건물 전멸뿐이다.
	private void CheckDefeat()
	{
		if (_defeatAnnounced) return;

		int securedRooms = 0;
		foreach (var room in Session.allRooms)
			if (room != null && room.Floor >= 1 && room.RoomFaction == FactionType.Player) securedRooms++;

		int playerUnits = 0;
		foreach (var u in Session.units)
			if (u != null && u.hp > 0f && u.IsPlayerMonsterFaction) playerUnits++;
		int buildings = Buildings != null ? Buildings.BuildingCount : 0;

		// 잃을 것을 가졌던 적이 있어야 잃은 것으로 본다 — 시작 직후 동기화 전이나 초기 건물 자리를 못 찾은 경우에 곧바로 패배하지 않게.
		if (securedRooms > 0) _hadSecuredRoom = true;
		if (playerUnits > 0 || buildings > 0) _hadAssets = true;

		DefeatReason reason = DefeatMath.Evaluate(_hadSecuredRoom, securedRooms, _hadAssets, playerUnits, buildings);
		if (reason == DefeatReason.None) return;

		_defeatAnnounced = true;
		LogHelper.Warning(LogHelper.GAME, $"[패배] {reason} — 확보한 방 {securedRooms}, 플레이어 유닛 {playerUnits}, 건물 {buildings}");
		int waves = HumanWaveManager.Instance != null ? HumanWaveManager.Instance.WaveNumber : 0;
		DefeatScreen.Show(Session, new DefeatSummary(reason, Session.ElapsedGameSeconds, Session.PlayerKillCount, waves));
	}

	private bool TryClassify(Unit u, out Room room, out FactionType? faction)
	{
		room = null;
		faction = null;
		if (u == null || u.hp <= 0f || u.currentFloor < 1) return false;
		if (u.unitType is WildBaseType) return false; // 야생 생성 거점은 방 클리어 조건에서 제외한다(코어 점령 v0.2 11장) — 클리어 뒤에도 남아 있을 수 있고 자동 생성만 멈춘다
		if (!Session.roomGrid.TryGetValue(new Vector3Int(u.position.x, u.position.y, u.currentFloor), out room) || room == null) return false;
		faction = OffenseProcessor.MapToRoomFaction(u.FactionBehavior);
		return true;
	}
}
