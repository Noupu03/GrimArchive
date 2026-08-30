using System.Collections.Generic;

// 파티 목록 저장소. CreateParty/CheckPartyWaveState는 실제로는 GameSession이 직접 구현해 쓰고 있어
// 죽은 코드였기에 제거했다 — parties 리스트만 GameSession.parties 프로퍼티로 계속 노출된다.
public class PartyService
{
    public List<Party> parties { get; private set; } = new List<Party>();
}
