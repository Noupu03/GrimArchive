using System.Collections.Generic;
using UnityEngine;
using Haare.Util.Logger;
using GrimArchive.Wave;

// 코어 잔해 — 코어가 파괴되면 고정 코어와 별개로 회수·운반하는 오브젝트가 생긴다. 인류가 주워 던전 밖으로 반출하면 반출한 웨이브 번호를 남긴다(부산물 장치 연구·반입·결합은 아직 없다, 코어 점령 v0.2 7·8장).
public sealed class CoreSalvage
{
	public const string Tag = "Object/Passable/CoreSalvage";

	private readonly GameSession _session;
	private readonly List<InteractableObject> _pieces = new List<InteractableObject>();
	private readonly List<int> _exportWaves = new List<int>();

	public CoreSalvage(GameSession session) => _session = session;

	public IReadOnlyList<int> ExportWaves => _exportWaves;
	public bool HasAny => _pieces.Count > 0;

	public static bool PartyHasCarrier(Party party)
	{
		if (party == null) return false;
		foreach (var m in party.Members)
			if (m != null && m.hp > 0f && m.carriesCoreSalvage) return true;
		return false;
	}

	public void Spawn(Vector3Int near)
	{
		Vector3Int pos = _session.objectGrid.ContainsKey(near) ? _session.FindNearbyFreeObjectTile(near) : near;
		var piece = new InteractableObject("CoreSalvage_" + System.Guid.NewGuid().ToString().Substring(0, 4), pos, 120f, 0f, new List<string> { Tag });
		_session.SpawnObject(piece, Color.cyan);
		if (_session.objectGrid.TryGetValue(pos, out InteractableObject placed) && placed == piece) _pieces.Add(piece);
	}

	// room 안에서 아직 아무도 줍지 않은 잔해 — 줍힌 것은 objectGrid에서 사라지므로 목록에서도 정리한다.
	public InteractableObject FindInRoom(Room room)
	{
		if (room == null) return null;
		for (int i = _pieces.Count - 1; i >= 0; i--)
		{
			InteractableObject piece = _pieces[i];
			if (piece == null || piece.IsCollected || !_session.objectGrid.TryGetValue(piece.Position, out InteractableObject live) || live != piece)
			{
				_pieces.RemoveAt(i);
				continue;
			}
			if (_session.roomGrid.TryGetValue(piece.Position, out Room r) && r == room) return piece;
		}
		return null;
	}

	public void PickUp(Human carrier, InteractableObject piece)
	{
		if (carrier == null || piece == null || piece.IsCollected) return;
		_session.CollectObject(piece.Position);
		carrier.carriesCoreSalvage = true;
		SpeechSystem.Say(carrier, SpeechId.SalvagePicked);
		LogHelper.Log(LogHelper.GAME, $"[코어] {carrier.name}이(가) 코어 잔해를 획득 — 파티가 던전 밖으로 반출한다");
	}

	// 운반자가 죽으면 사망 위치 근처에 다시 놓인다(운반 중 공격받으면 떨어질 수 있다는 8장의 가장 단순한 형태 — 확률 낙하는 문서에 수치가 없다).
	public void OnCarrierDied(Human carrier) => Drop(carrier, "사망");

	// 개인 도주를 확정한 운반자는 현재 타일에 잔해를 내려놓고 도주한다(후퇴도주 5-3·R41). 잔해가 그 문서의 "운반 유물"인지는 문서가 말하지 않아 이 한 규칙만 해석해 적용했고, 이동속도·공격·방어 페널티는 적용하지 않는다.
	public void OnCarrierFled(Human carrier) => Drop(carrier, "개인 도주 확정");

	private void Drop(Human carrier, string reason)
	{
		if (carrier == null || !carrier.carriesCoreSalvage) return;
		carrier.carriesCoreSalvage = false;
		Spawn(new Vector3Int(carrier.position.x, carrier.position.y, carrier.currentFloor));
		LogHelper.Log(LogHelper.GAME, $"[코어] 운반자 {carrier.name} {reason} — 잔해가 그 자리에 다시 놓임");
	}

	// 잔해를 든 인류가 던전 밖으로 나갔다(RetreatExit.Complete) — 후퇴·도주로 나간 경우도 반출 성공이다.
	public void OnExported(Human carrier)
	{
		if (carrier == null || !carrier.carriesCoreSalvage) return;
		carrier.carriesCoreSalvage = false;
		int wave = HumanWaveManager.Instance != null ? HumanWaveManager.Instance.WaveNumber : 0;
		_exportWaves.Add(wave);
		LogHelper.Log(LogHelper.GAME, $"[코어] 코어 잔해 반출 성공(웨이브 {wave}) — 부산물 장치 연구는 반출 뒤 정규 2웨이브, 반입은 3번째 웨이브부터(미구현)");
		NoticeCenter.Instance?.PushMomentary("인류가 코어 잔해를 던전 밖으로 반출했습니다.", NoticeCenter.WarningColor);
	}
}
