using UnityEngine;
using System.Collections.Generic;

namespace GrimArchive.Wave
{
    [System.Serializable]
    public class WaveUnitGroup
    {
        [Tooltip("소환할 UnitType 이름 (예: MeleeTank, Archer, 기사형)")]
        public string unitTypeName;
        [Tooltip("해당 타입의 소환 마리 수")]
        public int count;
    }

    public enum PartyFaction
    {
        Human,
        Monster
    }

    // 파티(그룹) 하나의 구성 — 이름 + 소속 진영 + 소환할 UnitType/수량.
    // faction=Human이면 실제 Party로 등록되어 연산공식 문서 6장(웨이브 클리어 생존자 반영)/
    // 13장(파티 전멸)의 판정 대상이 된다. faction=Monster는 그냥 이 웨이브의 몬스터 스쿼드로
    // 스폰만 되고(별도 Party 객체 없음), 같은 웨이브의 모든 인류 파티가 상대할 WaveMonsters로 묶인다.
    [System.Serializable]
    public class WavePartyConfig
    {
        [Tooltip("파티/그룹 이름 (표시/로그용 — 몬스터 그룹이면 로그에만 쓰인다)")]
        public string partyName = "Party";
        [Tooltip("이 그룹의 진영. Human=실제 파티(전멸/생존자 반영 대상) / Monster=이 웨이브의 몬스터 스쿼드")]
        public PartyFaction faction = PartyFaction.Human;
        [Tooltip("이 그룹으로 소환할 UnitType 이름과 수량")]
        public List<WaveUnitGroup> units = new List<WaveUnitGroup>();
    }

    /// <summary>
    /// 웨이브에 등장할 유닛들의 구성을 정의하는 데이터.
    /// 인류/몬스터 모두 "파티(그룹)" 단위로만 구성한다 — 진영은 WavePartyConfig.faction으로 결정.
    /// UnitTypes.cs 에 정의된 클래스명을 문자열로 입력하여 VContainer DI 시스템과 연동되도록 합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "New Wave Data", menuName = "Wave System/Wave Data")]
    public class WaveData : ScriptableObject
    {
        [Header("이 웨이브를 구성하는 파티(그룹) 목록")]
        [Tooltip("Human 그룹은 실제 파티로 등록되어 전멸/웨이브클리어 생존자 반영 판정을 받고, " +
                 "Monster 그룹은 이 웨이브의 몬스터 스쿼드로 스폰만 된다. 같은 웨이브 안의 모든 " +
                 "Monster 그룹 합계가, 이 웨이브의 모든 Human 파티가 공통으로 상대하는 " +
                 "WaveMonsters가 된다.")]
        public List<WavePartyConfig> parties = new List<WavePartyConfig>();
    }
}
