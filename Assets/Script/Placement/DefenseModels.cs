using UnityEngine;

public enum DefenseMoveSource
{
	Direct,     // [이동 적용]으로 직접 발행 — 미배치로 돌려도 유지
	AutoReturn, // 단일 작업 종료 뒤 자동 복귀 — 미배치로 돌리면 해제
}

// 유닛당 전역 하나. 저장만으로는 현재 작업·전투·이동을 바꾸지 않는다.
public sealed class DefenseRecord
{
	public Unit Unit;
	public int Floor;
	public int RoomId;
	public Vector2Int Tile; // 몸 좌하단 기준
	public Dir Facing;
	public bool NeedsReassign; // 건설 예약과 겹침 — 위치는 유지하고 이동 적용만 건너뛴다
}

// 발행 순간의 목적지·방향. 이후 저장 위치를 바꿔도 이 값은 그대로다.
// 유입 인구는 이동 명령에서 파생하므로 따로 예약하지 않는다.
public sealed class IssuedMove
{
	public Unit Unit;
	public DefenseMoveSource Source;
	public int Floor;
	public int RoomId;
	public Vector2Int Tile;
	public Dir Facing;
}

public enum PlacementBlock
{
	None,
	NotPlayerUnit,
	Dead,
	Evolving,
	WrongFloor,
	OutOfRoom,
	NotWalkable,
	DoorOrStair,
	Facility,
	FrontZone, // 생산시설의 정면 출현 구역(건설 명세 2-10) - 방어 위치를 저장할 수 없다
	OverlapsOtherDefense,
	RoomNotSecured,
	RoomFull,
	CoreDestroyed,
	DoorTooNarrow,
	Unreachable,
	DestinationReserved,
	NotPlaced,
	NeedsReassign,
}

public static class PlacementBlockText
{
	public static string ToKorean(this PlacementBlock b) => b switch
	{
		PlacementBlock.None                 => "가능",
		PlacementBlock.NotPlayerUnit        => "조작할 수 있는 아군 몬스터가 아닙니다",
		PlacementBlock.Dead                 => "사망한 유닛입니다",
		PlacementBlock.Evolving             => "진화 연출 중이라 새 명령을 받을 수 없습니다",
		PlacementBlock.WrongFloor           => "다른 층입니다",
		PlacementBlock.OutOfRoom            => "방 밖입니다",
		PlacementBlock.NotWalkable          => "벽·구조물이라 설 수 없습니다",
		PlacementBlock.DoorOrStair          => "문·계단 위에는 설 수 없습니다",
		PlacementBlock.Facility             => "시설·건설 계획과 겹칩니다",
		PlacementBlock.FrontZone            => "생산시설의 정면 출현 구역에는 저장할 수 없습니다",
		PlacementBlock.OverlapsOtherDefense => "다른 유닛의 방어 위치와 겹칩니다",
		PlacementBlock.RoomNotSecured       => "확보하지 않은 방입니다",
		PlacementBlock.RoomFull             => "방 정원이 부족합니다",
		PlacementBlock.CoreDestroyed        => "코어 수복 후 배치 가능",
		PlacementBlock.DoorTooNarrow        => "몸이 커서 문을 지날 수 없습니다",
		PlacementBlock.Unreachable          => "그 방까지 갈 수 있는 길이 없습니다",
		PlacementBlock.DestinationReserved  => "최종 방어 타일이 다른 계획과 겹칩니다",
		PlacementBlock.NotPlaced            => "방어 위치가 저장되지 않았습니다",
		PlacementBlock.NeedsReassign        => "방어 위치 재지정 필요",
		_                                   => b.ToString(),
	};
}
