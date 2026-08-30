using UnityEngine;
using System.Collections.Generic;
using Haare.Client.Routine;
using Cysharp.Threading.Tasks;
using System.Linq;
using VContainer;
using Haare.Util.Logger;

namespace GrimArchive.Wave
{
    public enum WaveState
    {
        Idle,
        Running,
        Ended
    }

    /// <summary>
    /// 인류 웨이브의 라이프사이클(발생, 목표 추적, 이탈, 종료)을 제어하는 시스템입니다.
    /// 기획 원칙에 따라 발생조건, 목적, 파티 구성, 종료 조건을 모듈화하여 관리합니다.
    /// </summary>
    public class HumanWaveManager : NativeRoutine
    {
        public static HumanWaveManager Instance { get; private set; }

        // 첫 웨이브를 포함해 항상 waveData.waveCooldown 하나로만 동작한다.
        public float waveCooldown => targetSpawner?.waveData?.waveCooldown ?? 10f;

        [Inject]
        public WaveSpawner targetSpawner; // VContainer를 통해 자동 주입

        public WaveState currentState = WaveState.Idle;
        public float cooldownTimer = 0f;

        // 시체는 시간이 아닌 웨이브 수 단위로 소멸한다 — 스폰 시점 WaveNumber를 스냅샷하고
        // (InteractableObject.SpawnWaveNumber), "스폰 웨이브+2 <= 지금 웨이브"면 정리한다(GameSession.DespawnCorpsesForNewWave).
        public int WaveNumber { get; private set; } = 0;

        // 추적 중인 웨이브 데이터
        public Party activeParty;

        // 웨이브 승리 조건: 목표 방(waveData.targetFloor의 보스방) 코어를 인류 소유로 전환. _retreating이
        // true가 되면(코어 파괴 성공) UpdatePartyDestination이 목적지를 exitAreaPos로 바꿔 퇴각 로직을 재사용한다.
        private Room _targetRoom;
        private bool _retreating;

        // 탈출 지점 영역 (임시: 던전 입구/StartRoom 기준 위치)
        public Vector2Int exitAreaPos;

        // ── 0층 사전 스폰(웨이브 시작 전 대기 연출) ── 웨이브 타이머가 돌면 0층에 미리 스폰해 대기시키고
        // 계단으로 이동해 목표 층으로 넘어간다(목표 없는 사전 스폰 유닛은 Goal_Explore로 배회하다
        // pendingStairTargetFloor 세팅 시 Goal_UseStairs가 가로챔). 이 상수는 DungeonEntranceSystem.
        // PrepareNoticeLeadSeconds와 같은 값(6초)이어야 하므로 두 클래스를 바꿀 땐 반드시 같이 바꿔야 한다.
        private const float PreSpawnLeadSeconds = 6f;
        private bool preSpawnTriggered = false;
        // "사전 스폰을 시도한 시점"과 실제 몬스터 소집 시점은 다를 수 있어(계단 미확보 시 사전 스폰이
        // 스킵되고 소집은 StartWave() 폴백에서 일어남), 소집이 실제로 걸린 순간에만 켜지는 전용 플래그를
        // 둔다(WaveGaugePanel 게이지 점멸이 구독).
        private bool monstersSummonedThisCycle = false;
        public bool IsMonstersSummonedThisCycle => monstersSummonedThisCycle;
        private Party preSpawnedParty;

        // 소집 시점은 "진입 준비" 문구가 뜨는 시점(PreSpawnLeadSeconds, 위 주석 참고)과 일치시킨다.
        private bool monsterMusterTriggered = false;
        // 던전 입구 시퀀스가 아주 빨리 끝나면 preSpawnedParty가 monsterMusterTriggered 체크보다 먼저
        // null로 비워질 수 있어, "이번 사이클에 사전 스폰이 성공했다"는 사실을 별도 플래그로 고정한다.
        private bool preSpawnSucceeded = false;

        // waveCooldown을 진행 바 시간으로 쓰는 단순 비율. ComputePreSpawnTriggerSeconds()가 스폰
        // 시점을 역산해두므로, 바가 100%에 도달하는 시점과 실제 1층 진입 시작 시점이 일치한다.
        public float WaveProgress01
        {
            get
            {
                if (currentState != WaveState.Idle) return 1f;
                return Mathf.Clamp01(1f - cooldownTimer / waveCooldown);
            }
        }

        // 아직 0층에서 계단으로 걸어가는 중(=목표 층에 아직 도착 못한) 파티원 집합 — MonitorWave/
        // UpdatePartyDestination의 목표물 추적 로직이 이 유닛들을 건드리지 않도록 걸러내는 데도 쓴다.
        private readonly HashSet<Human> stagingUnits = new HashSet<Human>();
        private Vector2Int floor0StairPos;
        private Vector2Int floor1StairPos;
        private bool stairPosResolved = false;

        // 0층 숨은 스폰 청크~계단까지 파티 진형 이동 시퀀스 전담(자체 클래스로 분리해 비대화 방지).
        // PreSpawnWaveUnits가 시작시키고, 매 프레임 Update, 계단 도달 시 OnDungeonEntranceArrivedAtStairs
        // 콜백으로 stagingUnits/pendingStairTargetFloor를 넘겨받는다.
        private readonly DungeonEntranceSystem _dungeonEntrance = new DungeonEntranceSystem();

        // 이전 웨이브에서 살아남아 0층으로 퇴각한 파티원 — 새로 스폰하지 않고 이미 0층에 있는 유닛을
        // 그대로 다음 파티에 편입한다.
        private readonly List<Human> retreatedSurvivors = new List<Human>();

        // 웨이브 시작 후 이 시간이 지나도 0층에 남아있는 파티원은 강제로 목표 층으로 건너뛰게 한다
        // (A* 교착 등으로 계단 근처에서 영구히 못 넘어오는 경우의 안전장치).
        private const float StairForceCrossTimeoutSeconds = 15f;
        private float runningStateTimer = 0f;

        public override async UniTask Initialize(System.Threading.CancellationToken cts)
        {
            await base.Initialize(cts);
            Instance = this;
            // WaveSpawner.Initialize()와의 NativeRoutine 실행 순서 경합 방지 — WaveSpawner.cs의
            // EnsureWaveDataLoaded() 주석 참고.
            targetSpawner?.EnsureWaveDataLoaded();
            cooldownTimer = waveCooldown;
            currentState = WaveState.Idle;

            // Haare Framework 기준: UniTask 기반 Native Routine 루프 실행
            WaveLoop(cts).Forget();
        }

        private async UniTaskVoid WaveLoop(System.Threading.CancellationToken cts)
        {
            while (!cts.IsCancellationRequested)
            {
                if (currentState == WaveState.Idle)
                {
                    cooldownTimer -= Time.deltaTime;
                    if (!preSpawnTriggered && cooldownTimer <= ComputePreSpawnTriggerSeconds())
                    {
                        preSpawnTriggered = true;
                        PreSpawnWaveUnits();
                    }
                    // 몬스터 소집 배치(R키 배치모드)는 폐기됐지만, 이 타이밍 플래그는 WaveGaugePanel
                    // (게이지 점멸)/"진입 준비" 문구가 여전히 참조하므로 유지한다.
                    if (!monsterMusterTriggered && preSpawnSucceeded && cooldownTimer <= PreSpawnLeadSeconds)
                    {
                        monsterMusterTriggered = true;
                        monstersSummonedThisCycle = true;
                    }
                    if (cooldownTimer <= 0f)
                    {
                        StartWave();
                    }
                }
                else if (currentState == WaveState.Running)
                {
                    UpdateStagingStairWalk();
                    MonitorWave();
                }

                // 던전 입구 구조는 cooldownTimer/currentState와 무관하게 독립 진행된다(웨이브가 Running으로
                // 넘어간 뒤에도 인간 파티는 입구를 걸어들어올 수 있음). Time.deltaTime을 그대로 써서 게임이
                // 멈추면 파티도 같이 멈춘다(notice 자체는 NoticeCenter가 unscaled 처리).
                _dungeonEntrance.Update(GameSession.Instance, Time.deltaTime, cooldownTimer, OnDungeonEntranceArrivedAtStairs);

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: cts);
            }
        }

        // ComputePreSpawnTriggerSeconds()가 계산한 시점에 이번 웨이브 인류 파티를 0층에 미리 스폰한다 —
        // 목표를 안 심어 GOAP이 Goal_Explore로 배회하게 두고, 숨은 스폰 청크에 등장시킨 뒤 나머지 진입
        // 시퀀스(입구 이동→대기→계단 이동)는 DungeonEntranceSystem에 넘긴다.
        private void PreSpawnWaveUnits()
        {
            if (targetSpawner == null || targetSpawner.waveData == null || targetSpawner.waveData.parties == null) return;
            if (!ResolveStairPositions())
            {
                LogHelper.Warning(LogHelper.GAME, "[HumanWaveManager] 0층 계단 위치를 찾지 못해 사전 스폰을 건너뜁니다(즉시 스폰으로 대체됩니다).");
                return;
            }

            int rowY = floor0StairPos.y;

            var members = new List<Human>();
            foreach (var config in targetSpawner.waveData.parties)
            {
                if (config == null || config.faction != PartyFaction.Human || config.units == null) continue;
                foreach (var group in config.units)
                {
                    if (string.IsNullOrEmpty(group.unitTypeName)) continue;
                    for (int i = 0; i < group.count; i++)
                    {
                        Vector2Int spawnPos = FindSpawnPosInHiddenChunk(rowY);
                        Human human = targetSpawner.InstantiatePreSpawnHumanAt(group.unitTypeName, spawnPos, 0);
                        if (human == null) continue;

                        members.Add(human);
                    }
                }
            }

            // 이전 웨이브 생존자는 새로 스폰하지 않고 그대로 이번 파티에 합류시킨다(대형 시작 위치
            // 통일을 위해 숨은 스폰 청크로 다시 위치시킴 — 어차피 플레이어 시야 밖).
            int survivorCount = 0;
            foreach (var survivor in retreatedSurvivors)
            {
                if (survivor == null || survivor.hp <= 0) continue;

                Vector2Int pos = FindSpawnPosInHiddenChunk(rowY);
                Vector2Int survivorOldPos = survivor.position;
                GameSession.Instance.UnregisterUnitPos(survivor, survivorOldPos);
                survivor.currentFloor = 0;
                survivor.position = pos;
                // 유닛끼리는 겹치면 안 되므로, 이미 다른 유닛이 그 칸에 있으면(극히 드문 스폰 혼잡)
                // 등록을 취소하고 원래 자리로 되돌린다.
                if (!GameSession.Instance.RegisterUnitPos(survivor, survivor.position))
                {
                    survivor.position = survivorOldPos;
                    GameSession.Instance.RegisterUnitPos(survivor, survivorOldPos);
                }

                members.Add(survivor);
                survivorCount++;
            }
            retreatedSurvivors.Clear();

            if (members.Count == 0) return;

            preSpawnedParty = GameSession.Instance.CreateParty("PreSpawnParty", members);
            preSpawnSucceeded = true;
            LogHelper.Log(LogHelper.GAME, $"[HumanWaveManager] 0층에 웨이브 파티 {members.Count}명 사전 스폰(배회 대기) — 신규 {members.Count - survivorCount}명, 이전 웨이브 생존자 {survivorCount}명 합류.");

            // 몬스터 소집 호출은 WaveLoop()의 cooldownTimer <= PreSpawnLeadSeconds 체크로 옮겨져 있다 —
            // 여기서는 그 체크가 참조할 preSpawnedParty만 준비해둔다.

            // 스폰 직후 곧바로 입구 진입 시퀀스(숨은 청크→입구 이동→대기→계단 이동)를 시작한다
            // (floor0StairPos는 ResolveStairPositions가 계단 옆 실제로 밟을 수 있는 타일로 구해둔 값).
            _dungeonEntrance.Begin(GameSession.Instance, preSpawnedParty, rowY, DungeonEntranceRoomEntryX, floor0StairPos.x);
        }

        // "웨이브 진행 바 100% = 1층 진입 시작"이 되려면 입구까지 이동(WalkingIn)이 Waiting 시간 전에 끝나야
        // 한다 — 실제 이동속도는 스폰 전엔 알 수 없어 기준 속도로 거리를 추정해 시간을 구하고 Waiting 시간을
        // 더한다(계단 위치를 못 구했으면 PreSpawnLeadSeconds 폴백).
        private const float ReferenceWalkSpeedForSpawnEstimate = 3f; // BaseStatComponent.walkSpeed 기본값과 동일.

        private float ComputePreSpawnTriggerSeconds()
        {
            if (!ResolveStairPositions()) return PreSpawnLeadSeconds;

            int walkInDistanceEstimate = Mathf.Abs(DungeonEntranceRoomEntryX - DungeonEntranceHiddenChunkCenterX);
            float stepIntervalEstimate = 1f / ReferenceWalkSpeedForSpawnEstimate;
            float estimate = walkInDistanceEstimate * stepIntervalEstimate + DungeonEntranceSystem.WaitSeconds;
            return Mathf.Max(estimate, PreSpawnLeadSeconds);
        }

        private bool ResolveStairPositions()
        {
            if (stairPosResolved) return true;
            if (GameSession.Instance == null || GameSession.Instance.cmap == null || targetSpawner.waveData == null) return false;

            // 계단 블록 자체가 아니라 그 옆 실제로 밟을 수 있는 타일(TryGetStairApproachPosition)을 쓴다 —
            // 계단 타일은 isStructureExist라 서 있을 수 없고, 퇴각 목표로 쓰면 A*가 안개 속에서 영원히
            // 도착 못 하는 버그가 있었다.
            int targetFloor = targetSpawner.waveData.targetFloor;

            // 힌트 없이 부르면 계단 블록 왼쪽 아래 칸을 반환해 대형이 청크 중앙보다 한 칸 처진다 —
            // 계단 블록 좌상단과 같은 행을 힌트로 줘서 그 행 위 칸을 고르게 한다.
            if (!GameSession.Instance.cmap.TryGetStairPosition(0, targetFloor, out Vector2Int floor0StairBlockPos)) return false;
            Vector2Int floor0RowHint = new Vector2Int(floor0StairBlockPos.x - 1, floor0StairBlockPos.y);
            if (!GameSession.Instance.cmap.TryGetStairApproachPosition(0, targetFloor, floor0RowHint, out floor0StairPos)) return false;

            if (!GameSession.Instance.cmap.TryGetStairApproachPosition(targetFloor, 0, out floor1StairPos)) return false;

            stairPosResolved = true;
            return true;
        }

        // 0층 최좌측 숨은 스폰 청크(카메라 관찰 범위 밖, CameraController.Floor0HiddenChunksX 참고) 안에서
        // 스폰 위치를 고른다(rowY는 floor0StairPos.y와 통일). 이 두 상수는 CreateMap.FloorConfigFactory의
        // Floor_0.chunkSize와 반드시 같이 맞춰야 한다.
        private const int DungeonEntranceHiddenChunkCenterX = 6; // 청크0(숨김) 로컬 중앙(12/2).
        // 청크1(가시 영역 최좌측) 중앙(18)에서 DungeonEntranceSystem 대형 최후미가 뒤로 밀리는 최대
        // 깊이(RankSpacingX(2)*랭크수(최대 2)=4)만큼 더 민 값 — 대기 포메이션이 안개에 가리지 않게.
        private const int DungeonEntranceRoomEntryX = 24;        // 청크1/2 경계 — 대형 전체가 안개에서 떨어짐.

        private Vector2Int FindSpawnPosInHiddenChunk(int rowY)
        {
            var generator = GameSession.Instance.unitGenerate;
            Vector2Int center = new Vector2Int(DungeonEntranceHiddenChunkCenterX, rowY);
            for (int i = 0; i < 10; i++)
            {
                int ox = UnityEngine.Random.Range(-2, 3);
                Vector2Int cand = center + new Vector2Int(ox, 0);
                if (generator != null && generator.IsAreaClear(cand, Vector2.one, 0)) return cand;
            }
            return center;
        }

        // DungeonEntranceSystem이 파티 진형을 계단까지 이끌고 도착했을 때 호출하는 콜백 — 여기서
        // pendingStairTargetFloor를 세팅해 Goal_UseStairs(140)가 정상 GOAP 계단 통과를 맡는다.
        private void OnDungeonEntranceArrivedAtStairs(List<Human> members)
        {
            if (targetSpawner?.waveData == null) return;

            int targetFloor = targetSpawner.waveData.targetFloor;
            stagingUnits.Clear();
            foreach (var member in members)
            {
                if (member == null) continue;
                member.pendingStairTargetFloor = targetFloor;
                stagingUnits.Add(member);
            }

            preSpawnedParty = null;
        }

        // WaveState.Running 동안 매 프레임 호출 — 계단 이동/통과 자체는 정상 GOAP(Goal_UseStairs 계열)이
        // 전담하고, 여기서는 목표 층 도착 파티원을 stagingUnits에서 빼는 것과 시간 초과 강제 이동만 담당한다.
        private void UpdateStagingStairWalk()
        {
            if (stagingUnits.Count == 0) return;

            runningStateTimer += Time.deltaTime;
            bool forceCross = runningStateTimer >= StairForceCrossTimeoutSeconds;
            int targetFloor = targetSpawner.waveData.targetFloor;

            var arrived = new List<Human>();
            foreach (var member in stagingUnits)
            {
                if (member == null || member.hp <= 0 || member.currentFloor == targetFloor)
                {
                    arrived.Add(member);
                    continue;
                }

                if (forceCross)
                {
                    // 도착 지점이 전부 점유돼 있으면 이번 틱은 실패로 보고 staging에 남겨 재시도한다.
                    if (ForceCrossToTargetFloor(member, targetFloor))
                        arrived.Add(member);
                }
            }

            foreach (var member in arrived) stagingUnits.Remove(member);
        }

        // 정상 GOAP 경로가 시간 안에 처리하지 못한 파티원을 강제로 목표 층 계단 지점으로 옮긴다 — 위치/
        // 그리드만 갱신하고 나머지는 다음 틱 UpdatePartyDestination이 이어받는다. 점유 안 된 후보 칸을
        // 찾아 보내며, 전부 점유면 false를 반환해 호출부가 재시도하게 한다.
        private bool ForceCrossToTargetFloor(Human member, int targetFloor)
        {
            if (!AIMovementHelper.TryResolveUnoccupiedStairArrival(GameSession.Instance, targetFloor, 0, out Vector2Int arrivePos))
                return false;

            Vector2Int memberOldPos = member.position;
            int memberOldFloor = member.currentFloor;
            GameSession.Instance.UnregisterUnitPos(member, memberOldPos);
            member.currentFloor = targetFloor;
            member.position = arrivePos;
            // 확인 시점과 등록 시점 사이에 다른 경로가 같은 칸을 먼저 차지했을 수 있는 최종 안전망.
            if (!GameSession.Instance.RegisterUnitPos(member, member.position))
            {
                member.currentFloor = memberOldFloor;
                member.position = memberOldPos;
                GameSession.Instance.RegisterUnitPos(member, memberOldPos);
                return false;
            }

            member.pendingStairTargetFloor = null;
            member.playerMoveTarget = null;
            member.isManualMoveCommand = false;

            LogHelper.Warning(LogHelper.GAME, $"[HumanWaveManager] {member.unitType.typeName}가 {StairForceCrossTimeoutSeconds}초 동안 계단을 못 넘어와 강제로 F{targetFloor}로 이동시켰습니다.");
            return true;
        }

        // 탈출 지점에 도착한 파티원을 게임에서 지우는 대신 0층으로 돌려보내 Goal_Explore로 배회하게
        // 하고 retreatedSurvivors에 등록해 다음 웨이브에 합류시킨다. 계단 위치를 못 구하면 제거한다.
        private void RetreatMemberToFloor0(Human member)
        {
            if (member == null) return;
            if (!ResolveStairPositions())
            {
                GameSession.Instance.DespawnUnit(member);
                return;
            }

            // 파티 전원이 한꺼번에 퇴각할 때 같은 칸으로 텔레포트하면 겹친다 — 점유 안 된 후보 칸을
            // 찾아 보내고, 전부 점유된 극단적 혼잡에서만 대표 좌표로 보내고 경고를 남긴다.
            int targetFloor = targetSpawner.waveData.targetFloor;
            if (!AIMovementHelper.TryResolveUnoccupiedStairArrival(GameSession.Instance, 0, targetFloor, out Vector2Int arrivePos))
            {
                LogHelper.Warning(LogHelper.GAME, $"[HumanWaveManager] {member.unitType.typeName} 퇴각 도착 지점이 전부 점유돼 대표 좌표로 보냅니다(드물게 겹칠 수 있음).");
                arrivePos = floor0StairPos;
            }

            GameSession.Instance.UnregisterUnitPos(member, member.position);
            member.currentFloor = 0;
            member.position = arrivePos;
            GameSession.Instance.RegisterUnitPos(member, member.position);

            member.pendingStairTargetFloor = null;
            member.playerMoveTarget = null;
            member.isManualMoveCommand = false;

            retreatedSurvivors.Add(member);

            LogHelper.Log(LogHelper.GAME, $"[HumanWaveManager] {member.unitType.typeName}가 퇴각하여 0층으로 돌아갔습니다.");
        }

        // 게이지 위에 표시할 "인간 파티" 아이콘 후보 유닛 타입 이름을 최대 max개 반환한다(사전 스폰된
        // 파티가 있으면 그 구성을, 없으면 waveData의 다음 웨이브 구성을 사용). 앞 max명을 그대로 뽑으면
        // 같은 유형이 몰려 중복만 보일 수 있어 유형별로 먼저 하나씩 채운다.
        public List<string> GetApproachingPartyTypeNames(int max)
        {
            var allNames = new List<string>();

            if (preSpawnedParty != null && preSpawnedParty.Members.Count > 0)
            {
                foreach (var member in preSpawnedParty.Members)
                {
                    if (member == null || member.unitType == null) continue;
                    allNames.Add(member.unitType.typeName);
                }
            }
            else if (targetSpawner != null && targetSpawner.waveData != null && targetSpawner.waveData.parties != null)
            {
                foreach (var config in targetSpawner.waveData.parties)
                {
                    if (config == null || config.faction != PartyFaction.Human || config.units == null) continue;
                    foreach (var group in config.units)
                    {
                        if (string.IsNullOrEmpty(group.unitTypeName)) continue;
                        for (int i = 0; i < group.count; i++)
                            allNames.Add(group.unitTypeName);
                    }
                }
            }

            return BuildDistinctFirstTypeNames(allNames, max);
        }

        // 서로 다른 유형을 우선 하나씩 채우고, 유형 종류가 max보다 적을 때만 다시 채워 중복으로
        // 메운다. 실제 인원수보다 많은 아이콘은 만들지 않는다.
        private static List<string> BuildDistinctFirstTypeNames(List<string> allNames, int max)
        {
            int cap = Mathf.Min(max, allNames.Count);
            var result = new List<string>();

            foreach (var name in allNames)
            {
                if (result.Count >= cap) break;
                if (!result.Contains(name)) result.Add(name);
            }
            if (result.Count < cap)
            {
                foreach (var name in allNames)
                {
                    if (result.Count >= cap) break;
                    result.Add(name);
                }
            }

            return result;
        }

        private void StartWave()
        {
            if (targetSpawner == null)
            {
                LogHelper.Error(LogHelper.GAME, "[HumanWaveManager] Target Spawner가 설정되지 않았습니다.");
                return;
            }

            LogHelper.Log(LogHelper.GAME, "[HumanWaveManager] 인류 웨이브 발생! (목표물이 생성될 때까지 대기합니다)");
            currentState = WaveState.Running;

            WaveNumber++;
            GameSession.Instance?.DespawnCorpsesForNewWave(WaveNumber);

            // 문은 항상 기본적으로 닫혀있고 진영·근접 여부로 매 프레임 스스로 개폐하므로(DoorSystem.
            // UpdateProcess) 웨이브 시작 시점에 별도로 잠글 필요가 없다.
            runningStateTimer = 0f;

            if (preSpawnedParty != null && preSpawnedParty.Members.Count > 0)
            {
                // 0층에 미리 대기시켜둔 파티를 그대로 쓴다. activeParty/exitAreaPos만 세팅하고 stagingUnits는
                // 비워둔다 — pendingStairTargetFloor는 OnDungeonEntranceArrivedAtStairs가 세팅해야
                // Goal_UseStairs가 그 전에 끼어들지 않는다.
                activeParty = preSpawnedParty;
                exitAreaPos = floor1StairPos; // 목표 층 진입 지점을 그대로 탈출 지점으로도 사용
            }
            else if (monstersSummonedThisCycle)
            {
                // 던전 입구 시퀀스가 빨리 끝나 이미 OnDungeonEntranceArrivedAtStairs로 완료 처리된
                // 경우 — activeParty는 이미 세팅돼 있으므로 새 파티를 중복 생성하지 않는다.
            }
            else
            {
                // 사전 스폰 자체가 안 됐으면(0층 계단 위치를 못 찾는 등) 즉시 스폰하는 폴백 —
                // 던전 입구 연출 없이 계단 바로 옆에 즉시 등장한다.
                int beforePartyCount = GameSession.Instance.parties.Count;
                targetSpawner.SpawnWave();

                if (GameSession.Instance.parties.Count > beforePartyCount)
                {
                    activeParty = GameSession.Instance.parties.Last();
                    if (activeParty.Members.Count > 0)
                    {
                        exitAreaPos = activeParty.Members[0].position;
                    }

                    // WaveGaugePanel이 이 플래그를 참조하므로 유지한다.
                    monstersSummonedThisCycle = true;
                }
                else
                {
                    LogHelper.Warning(LogHelper.GAME, "[HumanWaveManager] 웨이브 소환 시도했으나 파티가 생성되지 않았습니다.");
                    EndWave(false);
                    return;
                }
            }

            // 목표 방(targetFloor의 보스방)의 Room을 미리 찾아둔다(실제 코어 위치는 GameSession.
            // SpawnAllRoomCores가 게임 시작 시 채워둔 room.CorePosition을 그대로 사용).
            _retreating = false;
            _targetRoom = null;
            int targetFloor = targetSpawner.waveData.targetFloor;
            if (GameSession.Instance?.unitGenerate != null)
            {
                Vector2Int bossPos = GameSession.Instance.unitGenerate.GetBossRoomPos(Vector2.one, targetFloor);
                GameSession.Instance.roomGrid.TryGetValue(new Vector3Int(bossPos.x, bossPos.y, targetFloor), out _targetRoom);
            }
            if (_targetRoom == null || _targetRoom.CoreObjectId == null)
                LogHelper.Warning(LogHelper.GAME, "[HumanWaveManager] 목표 방의 코어를 찾지 못했습니다 — 이번 웨이브는 목표 없이 진행됩니다.");
        }

        private void MonitorWave()
        {
            if (activeParty == null || activeParty.IsWiped)
            {
                if (_retreating)
                {
                    LogHelper.Log(LogHelper.GAME, "[HumanWaveManager] 파티가 전멸했지만 코어는 이미 파괴되어 웨이브 성공.");
                    EndWave(true);
                }
                else
                {
                    LogHelper.Log(LogHelper.GAME, "[HumanWaveManager] 파티가 전멸했습니다. 웨이브 실패.");
                    EndWave(false);
                }
                return;
            }

            int targetFloor = targetSpawner.waveData.targetFloor;

            // 목표 방의 코어를 파괴(=인류 소유로 전환, OffenseProcessor.OnCoreDestroyed)하면 그 순간 퇴각으로
            // 전환한다(실제 코어 공격은 TacticalFSMState/UnitFunction.OnUpdate가 담당, 여기선 목적지 지정과
            // 성공 판정만 한다).
            if (!_retreating && _targetRoom != null && _targetRoom.RoomFaction == FactionType.Human)
            {
                LogHelper.Log(LogHelper.GAME, "[HumanWaveManager] 목표 방 코어 파괴 성공! 생존 파티원 퇴각 시작.");
                _retreating = true;
            }

            // 목표가 이미 정해진 뒤에도 매 틱 다시 적용한다 — 계단을 막 넘어온 유닛처럼 처음엔
            // 건너뛰어졌던 유닛도 자동으로 따라잡는다(같은 값 재적용이라 매 틱 불러도 무해).
            UpdatePartyDestination();

            if (!_retreating) return; // 아직 코어를 파괴하지 못함 — 유닛은 자유롭게(GOAP) 방으로 접근·공격

            // 탈출 지점 체크 (퇴각 중)
            // [TODO: 향후에는 주변 유닛이 부상병을 호위하는 편대 AI 시스템을 추가해야 함]
            // 현재는 단순히 코어 파괴 후 각자 탈출 지점으로 이동하며, 탈출 지점에 도착하는 개별 유닛부터 삭제(탈출) 처리합니다.
            for (int i = activeParty.Members.Count - 1; i >= 0; i--)
            {
                var member = activeParty.Members[i];
                if (member == null || member.hp <= 0 || stagingUnits.Contains(member)) continue;
                if (member.currentFloor != targetFloor) continue;

                if (member.position == exitAreaPos)
                {
                    LogHelper.Log(LogHelper.GAME, $"[HumanWaveManager] {member.name} 유닛 개별 탈출 성공.");

                    RetreatMemberToFloor0(member);
                    activeParty.Members.RemoveAt(i);
                }
            }

            // 모든 파티원이 탈출했거나 사망했다면 웨이브 종료
            if (activeParty.GetSurvivors().Count == 0)
            {
                LogHelper.Log(LogHelper.GAME, "[HumanWaveManager] 코어 파괴 후 모든 파티원이 탈출(또는 사망)하여 웨이브가 성공적으로 종료됩니다.");
                EndWave(true);
            }
        }

        private void UpdatePartyDestination()
        {
            if (!_retreating && _targetRoom == null) return; // 목표 없음 — 자유 배회에 맡김(에러 상황)

            Vector2Int dest = _retreating ? exitAreaPos : new Vector2Int(_targetRoom.CorePosition.x, _targetRoom.CorePosition.y);
            int destFloor = _retreating ? targetSpawner.waveData.targetFloor : _targetRoom.CorePosition.z;

            // 목표와 다른 층에 있는 파티원(계단 이동 중인 사전 스폰 파티원 등)은 건드리지 않는다
            // (그쪽은 pendingStairTargetFloor 기반으로 별도 관리된다).
            foreach (var member in activeParty.Members)
            {
                if (member == null || member.hp <= 0 || stagingUnits.Contains(member)) continue;
                if (member.currentFloor != destFloor) continue;
                // 플레이어 수동 명령 진행 중이면 자동 목표 재할당이 덮어쓰지 않는다 — 명령이 끝나면
                // (PlayerCommandFSMState가 플래그 정리) 다음 틱부터 다시 챙긴다.
                if (member.isManualMoveCommand && member.playerMoveTarget.HasValue) continue;
                if (member.playerAttackTarget != null) continue;
                member.playerMoveTarget = dest;
                member.isManualMoveCommand = false;
            }
        }

        private void EndWave(bool isSuccess)
        {
            currentState = WaveState.Ended;

            var survivors = activeParty != null ? activeParty.GetSurvivors() : new List<Unit>();
            if (activeParty != null && !activeParty.WaveEnded)
            {
                activeParty.WaveEnded = true;
                if (survivors.Count > 0)
                {
                    survivors[0].Knowledge.OnWaveEnd(survivors);
                }
            }

            if (isSuccess && activeParty != null)
            {
                // 개별 탈출 루프를 거치지 않고 곧장 성공 처리된 경우, 남은 생존자도 0층으로 돌려보낸다.
                foreach (var survivor in survivors)
                {
                    if (survivor is Human survivorHuman)
                    {
                        RetreatMemberToFloor0(survivorHuman);
                    }
                }
            }

            LogHelper.Log(LogHelper.GAME, $"[HumanWaveManager] 웨이브 정리 완료. 다음 웨이브까지 {waveCooldown}초 대기.");

            activeParty = null;
            _targetRoom = null;
            _retreating = false;
            cooldownTimer = waveCooldown;
            currentState = WaveState.Idle;

            // 다음 웨이브 사이클을 위해 사전 스폰 관련 상태 초기화.
            preSpawnTriggered = false;
            monsterMusterTriggered = false;
            preSpawnSucceeded = false;
            monstersSummonedThisCycle = false;
            preSpawnedParty = null;
            stagingUnits.Clear();
        }
    }
}


