using System;

[Serializable]
public enum TileEffect
{
    None
    // 효과 종류는 필요에 따라 확장하세요.
}

[Serializable]
public struct Tile
{
    // 타일 이름
    public string name;
    // 효과
    public TileEffect effect;
    // 점유 여부
    public bool isObjectExist;
    // 건물 존재 여부
    public bool isStructureExist;
    // 위험도
    public int dangerous;
    // 이해도
    public int understand;
    // 가중치
    public int weight;
    // 가시성
    public int visibility;
}
[Serializable]
public struct Chunks
{
    // 8x8 크기 tile 데이터
    public Tile[,] chunk;

    // 지형 프리팹 가져오기용
    public int landform;

    // 방 식별자: 문자열 기반 roomCode 대신 정수 id를 사용하여 비교/검색 비용을 줄이고
    // 일관된 그룹핑이 가능하도록 개선했습니다. 사람 가독성을 위해 roomName도 함께 저장합니다.
    // - roomId: 내부 비교/인덱스 용 (최소 비용)
    // - roomName: 디버깅/에디터 표시용 (예: "room0")
    public int roomId;
	public string roomName;
	public enum WhoOccupation
	{
		None,
		Monster,
		Human
	};
	public WhoOccupation whoOccupation;
}
[Serializable]
public struct Map
{
    // session 범위 크기 chunks 데이터 (16x16)
    public Chunks[,] session;
}
[Serializable]
public struct Session
{
    public int[,,] session;
}
public struct MapData
{
    // Map 및 Session 데이터
    public Map map;
    public Session session;
}

// 타일 팩토리: 타일 종류별 기본값을 한 곳에서 관리합니다.
// 새로운 타일 종류(예: Water, Lava 등)를 추가하려면 여기에 메서드를 추가하세요.
public static class TileFactory
{
    public static Tile Wall()
    {
        return new Tile
        {
            name = "Wall",
            effect = TileEffect.None,
            isObjectExist = false,
            isStructureExist = false,
            dangerous = 0,
            understand = 0,
            weight = 0,
            visibility = 0
        };
    }

    public static Tile Floor()
    {
        return new Tile
        {
            name = "Floor",
            effect = TileEffect.None,
            isObjectExist = false,
            isStructureExist = false,
            dangerous = 0,
            understand = 0,
            weight = 0,
            visibility = 100
        };
    }

    // 확장 예시: 새 타일 종류를 추가할 때 아래처럼 메서드를 추가하세요.
    // public static Tile Water()
    // {
    //     return new Tile
    //     {
    //         name = "Water",
    //         effect = TileEffect.None,
    //         isObjectExist = false,
    //         isStructureExist = false,
    //         dangerous = 1,
    //         understand = 0,
    //         weight = 2,
    //         visibility = 0
    //     };
    // }
}

// Room ID generator: 방을 생성할 때 전역 고유 ID를 발급합니다.
// 사용법: var id = RoomIdGenerator.GetNextId();
//        var name = RoomIdGenerator.FormatName(id);

