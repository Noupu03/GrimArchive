public enum FactionType
{
    Player,
    Wild,
    RegularMonster,

    // 방 소유로는 0층 로비만 쓴다(OccupationState.HumanControlled). 1층 이상의 인류 거점은 소유가 아니라 Room.BaseStage로 표현한다(코어 점령 v0.2).
    // OffenseProcessor.MapToRoomFaction 참고. Room은 직렬화되지 않고 매 세션 BuildRoomGrid로 다시 만들어지므로 enum 순서를 바꿔도 세이브 호환성 문제 없음.
    Human
}
