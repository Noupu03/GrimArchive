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

// Room ID generator: 방을 생성할 때 전역 고유 ID를 발급합니다.
// 사용법: var id = RoomIdGenerator.GetNextId();
//        var name = RoomIdGenerator.FormatName(id);

