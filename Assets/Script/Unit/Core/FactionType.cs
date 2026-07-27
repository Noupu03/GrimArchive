public enum FactionType
{
    Player,
    Wild,
    RegularMonster,

    // 점령 전환 MVP(2026-07-27, 사용자 요청 "인류도 소유권 있어") — 인류 웨이브 유닛의 막타로도 방
    // 점령이 넘어갈 수 있도록 추가. OffenseProcessor.MapToRoomFaction 참고. Room은 직렬화되지 않고
    // 매 세션 BuildRoomGrid로 다시 만들어지므로 enum 순서를 바꿔도(끝에 추가) 세이브 호환성 문제 없음.
    Human
}
