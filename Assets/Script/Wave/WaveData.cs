using UnityEngine;
using System.Collections.Generic;

namespace GrimArchive.Wave
{
    [System.Serializable]
    public class WaveUnitGroup
    {
        [Tooltip("소환할 몬스터의 UnitType 이름 (예: MeleeTank, Archer)")]
        public string unitTypeName;
        [Tooltip("해당 타입의 소환 마리 수")]
        public int count;
    }

    /// <summary>
    /// 웨이브에 등장할 유닛들의 구성을 정의하는 데이터
    /// UnitTypes.cs 에 정의된 클래스명을 문자열로 입력하여 VContainer DI 시스템과 연동되도록 합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "New Wave Data", menuName = "Wave System/Wave Data")]
    public class WaveData : ScriptableObject
    {
        [Header("미리 설정된 유닛 그룹")]
        [Tooltip("정해진 유닛 클래스 이름과 수량을 설정합니다.")]
        public List<WaveUnitGroup> configuredUnits = new List<WaveUnitGroup>();

        [Header("미리 설정되지 않은 (랜덤) 유닛")]
        [Tooltip("랜덤 소환 기능을 사용할지 여부")]
        public bool useRandomUnits = false;
        [Tooltip("랜덤으로 뽑을 유닛 클래스명 풀 (예: MeleeTank, Archer)")]
        public List<string> randomUnitPool = new List<string>();
        [Tooltip("랜덤으로 소환할 총 마리 수")]
        public int randomUnitTotalCount = 0;
    }
}
