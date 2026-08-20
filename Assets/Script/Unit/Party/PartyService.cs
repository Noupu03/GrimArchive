using System.Collections.Generic;

// 파티 목록 저장소. CreateParty/CheckPartyWaveState는 한때 이 클래스가 갖고 있었지만(구버전
// Unit.UnitParty.party API 기준), 실제로는 GameSession이 자기 안에 별도로 같은 역할의 메서드를
// 직접 구현해 써왔다(GameSession.CreateParty/CheckPartyWaveState, HumanWaveManager/WaveSpawner가
// GameSession.Instance.CreateParty를 호출) — 이 클래스의 두 메서드는 어디서도 호출되지 않는 죽은
// 코드였다(2026-08-20 grep으로 호출부 없음 확인 후 제거). parties 리스트만 GameSession.parties
// 프로퍼티로 계속 노출된다.
public class PartyService
{
    public List<Party> parties { get; private set; } = new List<Party>();
}
