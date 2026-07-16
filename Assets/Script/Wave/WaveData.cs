using UnityEngine;
using System.Collections.Generic;
using System;

namespace GrimArchive.Wave
{
    [System.Serializable]
    public class WaveUnitGroup
    {
        [Tooltip("소환할 UnitType 이름 (예: MeleeTank, Archer, 기사)")]
        public string unitTypeName;
        [Tooltip("해당 유닛의 소환 마리 수")]
        public int count;
    }

    public enum PartyFaction
    {
        Human,
        Monster
    }

    [System.Serializable]
    public class WavePartyConfig
    {
        [Tooltip("파티/그룹 이름")]
        public string partyName = "Party";
        [Tooltip("진영 (Human / Monster)")]
        public PartyFaction faction = PartyFaction.Human;
        [Tooltip("이 그룹으로 소환할 UnitType 이름과 수량")]
        public List<WaveUnitGroup> units = new List<WaveUnitGroup>();
    }

    [CreateAssetMenu(fileName = "New Wave Data", menuName = "Wave System/Wave Data")]
    public class WaveData : ScriptableObject
    {
        [Header("웨이브 소환 설정")]
        [Tooltip("다음 웨이브 발생까지의 대기 시간 (초)")]
        public float waveCooldown = 10f;

        public SpawnMode spawnMode = SpawnMode.ByRoomRole;

        [Tooltip("몇 층(Floor)에 소환할 것인가?")]
        public int targetFloor = 1;

        [Tooltip("소환 중심점 (예: F1 입구 계단).")]
        public Vector3 spawnCenter = Vector3.zero;

        [Tooltip("소환 중심점으로부터 그리드 단위 범위 최대 반경")]
        public int spawnTileRadius = 3;

        [Tooltip("ByRoomRole 선택 시 지정할 방 역할")]
        public RoomRole targetRoomRole = RoomRole.NormalRoom;

        [Tooltip("ByRoomId 선택 시 지정할 방 번호")]
        public int targetRoomId = 0;

        [Header("웨이브 구성 파티(그룹) 목록")]
        public List<WavePartyConfig> parties = new List<WavePartyConfig>();
    }
}
