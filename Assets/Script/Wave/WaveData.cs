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
        [Tooltip("다음 웨이브 발생까지의 대기 시간 (초) — 첫 웨이브를 포함해 항상 이 값 하나로만 동작한다 (2026-08-24 사용자 요청: 첫 웨이브 전용 시간 제거)")]
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

        [Header("편성 계획(파티 구성)")]
        [Tooltip("true면 인류 파티를 party_composition.json 기반 편성 계획(PartyPlanner)으로 만든다 — 컨셉·임무·규모·임무 수량이 웨이브마다 정해진다. false면 아래 parties의 인류 그룹을 그대로 쓴다(수동 편성)")]
        public bool useCompositionPlanner = true;
        [Tooltip("체크하면 forcedMission을 이번 웨이브 임무로 고정한다(특정 사건이 임무를 지정하는 경우, 파티 구성 3-1장)")]
        public bool useForcedMission = false;
        public PartyType forcedMission = PartyType.Explore;

        [Header("웨이브 구성 파티(그룹) 목록 — 몬스터 그룹은 항상 여기서 읽고, 인류 그룹은 편성 계획을 끈 경우에만 쓴다")]
        public List<WavePartyConfig> parties = new List<WavePartyConfig>();
    }
}
