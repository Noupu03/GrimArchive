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

        // 01번 문서 2장: 몬스터 처치 임무의 공통 집계 — 여러 인류 파티가 있어도 시스템이 하나로
        // 합산한다(웨이브마다 StartWave에서 리셋). "막 달성됨" 신호를 공유 bool로 두면 먼저 본
        // 파티가 소비해버려 나머지가 못 보므로, 파티별 소비 여부는 Party._monsterKillQuotaConsumed가
        // 각자 따로 든다.
        public float CommonMonsterKillCount { get; private set; }
        private readonly HashSet<string> _countedKillIds = new HashSet<string>();

        // GameSession.RemoveDeadUnit이 인류에게 죽은 몬스터의 사망 시점(Destroy 전)에 호출한다.
        // deathEventId는 그 순간 생성되는 시체 오브젝트 Id를 그대로 재사용 — 사망마다 고유해 "사망
        // 사건 식별자로 중복 집계를 막는다"는 요구를 그대로 만족한다.
        public void OnMonsterKilled(string deathEventId, string speciesTypeName)
        {
            if (string.IsNullOrEmpty(deathEventId) || !_countedKillIds.Add(deathEventId)) return;
            CommonMonsterKillCount += PartyGoalMath.MonsterKillWeight(speciesTypeName);
        }

        // 추적 중인 웨이브 데이터
        public Party activeParty;

        // 웨이브 승리 조건: 목표 방(waveData.targetFloor의 보스방) 코어를 인류 소유로 전환. _retreating이
        // true가 되면(코어 파괴 성공) UpdatePartyDestination이 목적지를 exitAreaPos로 바꿔 퇴각 로직을 재사용한다.
        private Room _targetRoom;
        private bool _retreating;
        // 방 이동 결정 로그 폭주 방지 — "다음 문을 모름"은 상태가 바뀔 때 한 번만, 코어 경로의 반복 발행은 일정 간격으로만 남긴다.
        private bool _doorSearchLogged;
        private float _nextCoreAdvanceLogTime;
        private float _nextDoorApproachCheckTime;

        // 탈출 지점 영역 (임시: 던전 입구/StartRoom 기준 위치)
        public Vector2Int exitAreaPos;

        // ── 0층 사전 스폰(웨이브 시작 전 대기 연출) ── 웨이브 타이머가 돌면 0층에 미리 스폰해 대기시키고
        // 계단으로 이동해 목표 층으로 넘어간다(목표 없는 사전 스폰 유닛은 NavigationFSMState로 배회하다
        // pendingStairTargetFloor 세팅 시 계단이동으로 전환됨). 이 상수는 DungeonEntranceSystem.
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
        // 목표를 안 심어 NavigationFSMState가 배회하게 두고, 숨은 스폰 청크에 등장시킨 뒤 나머지 진입
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

        // DungeonEntranceSystem이 전원에게 계단 접근 이동 명령을 내린 그 순간 호출하는 콜백 — 여기서
        // pendingStairTargetFloor를 세팅해 개인 FSM+BT 계단이동(NavigationFSMState.MoveToStairs/
        // CrossStairs)이 이어받는다.
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

            // 입구 시퀀스가 cooldownTimer보다 먼저 끝나면 preSpawnedParty가 StartWave() 실행 전에
            // null로 비워져 activeParty가 세팅되지 않고 MonitorWave()가 "전멸"로 오판할 수 있다 —
            // StartWave()와 동일하게 여기서도 확정적으로 세팅한다(이미 세팅돼 있으면 무해한 재대입).
            activeParty = preSpawnedParty;
            exitAreaPos = floor1StairPos;

            preSpawnedParty = null;
        }

        // WaveState.Running 동안 매 프레임 호출 — 계단 이동/통과 자체는 개인 FSM+BT(NavigationFSMState)가
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

        // 정상 이동 경로가 시간 안에 처리하지 못한 파티원을 강제로 목표 층 계단 지점으로 옮긴다 — 위치/
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

        // 탈출 지점에 도착한 파티원을 게임에서 지우는 대신 0층으로 돌려보내 자유탐색으로 배회하게
        // 하고 retreatedSurvivors에 등록해 다음 웨이브에 합류시킨다. 계단 위치를 못 구하면 제거한다.
        private void RetreatMemberToFloor0(Human member)
        {
            if (member == null) return;
            member.ClearInteractionProgress(); // v0.6 9-9장: 웨이브를 떠나는 시점에 조사·해제 진행도 제거(생존자는 재사용된다)
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

            // 01번 문서 2장: 몬스터 처치 임무 수량은 웨이브 발급 시 고정·초기화한다.
            CommonMonsterKillCount = 0f;
            _countedKillIds.Clear();

            // 문은 항상 기본적으로 닫혀있고 진영·근접 여부로 매 프레임 스스로 개폐하므로(DoorSystem.
            // UpdateProcess) 웨이브 시작 시점에 별도로 잠글 필요가 없다.
            runningStateTimer = 0f;

            if (preSpawnedParty != null && preSpawnedParty.Members.Count > 0)
            {
                // 0층에 미리 대기시켜둔 파티를 그대로 쓴다. pendingStairTargetFloor는
                // OnDungeonEntranceArrivedAtStairs가 세팅해야 개인 FSM+BT 계단이동이 그 전에
                // 끼어들지 않는다.
                activeParty = preSpawnedParty;
                exitAreaPos = floor1StairPos; // 목표 층 진입 지점을 그대로 탈출 지점으로도 사용
            }
            else if (monstersSummonedThisCycle)
            {
                // 던전 입구 시퀀스가 cooldownTimer보다 먼저 끝나 이미 OnDungeonEntranceArrivedAtStairs
                // 에서 activeParty/exitAreaPos를 세팅해둔 경우 — 새 파티를 중복 생성하지 않는다.
                // (2026-09-27 버그 수정: 예전엔 그 콜백이 activeParty를 세팅한 적이 없어 이 분기에서
                // activeParty가 null로 남아 웨이브가 즉시 "전멸" 오판으로 끝나는 버그가 있었다.)
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
            // SpawnBossCores가 게임 시작 시 채워둔 room.CorePosition을 그대로 사용).
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

        // 코어 파괴 전에는 매 틱 전원을 코어 좌표로 강제 이동시키지 않는다 — 개인 탐색/조사(01번
        // 문서)와 리더의 집결 판단(05번 문서, Party.TickRoomActivityCheck)에 맡기고, 여기서는 집결이
        // 막 끝나 다음 문으로 이동해야 하는 순간(Party.ReadyToAdvance)이나 리더가 미처리 코어를 확인한
        // 순간(Party.LeaderKnownCorePosition, 검증문서 03-03)에만 목적지를 지정한다. 코어 파괴 후
        // 퇴각(_retreating)은 기존대로 매 틱 강제 이동을 유지한다(05번 10항 범위 밖).
        private void UpdatePartyDestination()
        {
            if (_retreating)
            {
                PartyAdvanceSystem.Abort(activeParty, "퇴각 전환", retry: false);
                // WaitState 기반으로 이동한다(WaitReason.Retreating 참고 — playerMoveTarget 경로는
                // 실제로 이동을 발생시키지 않는 죽은 경로였음).
                Vector2Int exitDest = exitAreaPos;
                int exitFloor = targetSpawner.waveData.targetFloor;
                foreach (var member in activeParty.Members)
                {
                    if (member == null || member.hp <= 0 || stagingUnits.Contains(member)) continue;
                    if (member.currentFloor != exitFloor) continue;
                    if (member.isManualMoveCommand && member.playerMoveTarget.HasValue) continue;
                    if (member.playerAttackTarget != null) continue;
                    if (member.currentWait != null) continue; // 이미 다른 대기 사유 진행 중이면 덮어쓰지 않음.
                    member.currentWait = new WaitState { Reason = WaitReason.Retreating, WaitPosition = exitDest };
                }
                return;
            }

            // 검증문서 03-03: "리더가 코어 정보를 확보하면 일반 임무·집결·다음 방 이동보다 코어
            // 처리를 우선한다"가 원래 의미하는 건 "가만히 있는다"가 아니라 "코어(=이미 _targetRoom과
            // 동일한 웨이브 목표 방)를 향해 실제로 이동한다"다. 그런데 Party.OnLeaderLearnsCore는
            // 집결을 해제하며 ReadyToAdvance까지 false로 되돌리므로, 원래 가드(ReadyToAdvance만 확인)
            // 그대로면 코어를 알게 된 순간부터 처리될 때까지 이 함수가 아무 이동도 발행하지 않는
            // 공백이 생겼다(발견 당시 TacticalBehaviorType.CoreAttack은 "이미 그 방에 서 있을 때만"
            // 발동하는 제자리 판정이라 이동을 대신해주지 못함). LeaderKnownCorePosition이 있을 때도
            // 같은 "다음 문 찾기" 경로를 타게 해 해결 — _targetRoom이 항상 코어가 있는 방과 동일하므로
            // 별도 목적지 계산이 필요 없다. 원문은 "리더와 발견자"로 좁게 표현했지만, 실제로는 이미
            // 있던 파티 공동 이동(AdvancingToNextRoom, 아래 foreach) 경로를 그대로 재사용해 파티
            // 전체가 함께 향하게 했다 — 집결·공동 이동 자체가 원래 파티 단위 개념이라 자연스러운 확장.
            // 집결을 마친 방을 리더가 떠났다면 그 방에서 시작한 이동 의도(ReadyToAdvance)는 소멸시킨다 — 안 그러면 새 방의
            // 문이 알려지는 즉시 그 방의 활동을 건너뛰고 이동 명령이 발행된다(검증 갭 정리, Party.TickAdvanceState).
            activeParty.TickAdvanceState();
            PartyAdvanceSystem.Tick(activeParty); // 문 앞 진형 → 문 파괴 → 입장 단계 전이(계획이 없으면 즉시 반환)
            TryAssignLeaderDoorApproach();

            bool hasPendingCore = activeParty.LeaderKnownCorePosition.HasValue;
            if (_targetRoom == null || !(activeParty.ReadyToAdvance || hasPendingCore))
            {
                _doorSearchLogged = false;
                return;
            }

            // 방 이동을 결정한 사유 — 집결 완료(ReadyToAdvance)가 실제 "결정"이고, 코어 경로는 코어 처리가 끝날 때까지 반복 발행된다.
            bool decidedByRally = activeParty.ReadyToAdvance;
            string partyTag = $"[파티] {activeParty.Name}({activeParty.Type.ToKorean()})";
            string reason = decidedByRally ? "집결 완료" : "코어 처리";

            if (!TryFindNextDoorTowardTargetRoom(out Vector2Int doorPos, out int doorFloor))
            {
                if (!_doorSearchLogged)
                {
                    _doorSearchLogged = true;
                    LogHelper.Log(LogHelper.GAME, $"{partyTag} 방 이동 결정({reason})했으나 다음 문을 아직 모름 — 리더 {activeParty.Leader?.name}은(는) 탐색, 파티원은 리더 주변 유지");
                }
                // 05번 1장 73줄: 다음 문을 아직 모르면 진형을 유지하며 문을 찾는다 — 리더는 대기 없이 기존 자유 탐색이 문을
                // 찾고, 나머지 파티원은 아는 리더 위치 주변을 따라다닌다(PartyDoorSearchSystem). ReadyToAdvance는 그대로
                // 두어 문이 알려지는 순간 아래 공동 이동 명령이 바로 발행되게 한다.
                AssignDoorSearchFollowers();
                return;
            }
            _doorSearchLogged = false;

            // 집결 완료로 정한 방 이동은 문 앞 진형 → 리더 지시 문 파괴 → 랭크 순 입장으로 진행한다(PartyAdvanceSystem). 코어 처리 경로는 예전 공동 이동을 그대로 쓰고,
            // 진형을 만들 수 없을 때(게이트·파티원 없음)나 partyAdvanceFormationEnabled를 끄면 아래 예전 방식으로 폴백한다.
            // 진형 계획이 중단돼 시도 한도(PartyAdvanceSystem.MaxAttempts)에 닿았으면 더 시도하지 않고 예전 방식으로 폴백한다 — 중단 뒤 파티가 영구히 멈추는 것을 막는다.
            bool attemptsExhausted = activeParty.AdvanceAttempts >= PartyAdvanceSystem.MaxAttempts;
            if (decidedByRally && (AIConfigLoader.Behavior?.partyAdvanceFormationEnabled ?? true) && !attemptsExhausted
                && PartyAdvanceSystem.Begin(activeParty, doorPos, doorFloor, stagingUnits))
            {
                activeParty.ReadyToAdvance = false; // 명령은 1회만 발행 — 이후 진행은 AdvancePlan이 맡는다.
                return;
            }
            if (decidedByRally && attemptsExhausted)
                LogHelper.Log(LogHelper.GAME, $"{partyTag} 진형 방 이동이 {activeParty.AdvanceAttempts}회 중단돼 예전 방식(문 앞 자리로 이동한 뒤 개인 행동)으로 이동합니다");

            // playerMoveTarget 대신 currentWait(AdvancingToNextRoom)을 쓴다 — playerMoveTarget은
            // PlayerCommandFSMState 전용(우선순위 200)이라 전투 중에도 무시하고 걸어가지만, currentWait
            // 기반 ExecuteWait은 Tactical(50)이라 Combat(100)에 자연히 밀린다(00번 4장 우선순위와 일치).
            // 05번 문서 3장: 전원이 문 타일 자체로 몰리면 통과 구간을 막으므로, 파티원마다 문 주변의
            // 서로 다른 대기 자리(체비셰프 거리 2 이상)를 배정한다(AIMovementHelper.FindDoorWaitSlot).
            var claimedDoorSlots = new HashSet<Vector2Int>();
            int assigned = 0;
            foreach (var member in activeParty.Members)
            {
                if (member == null || member.hp <= 0 || stagingUnits.Contains(member)) continue;
                if (member.currentFloor != doorFloor) continue;
                if (member.isManualMoveCommand && member.playerMoveTarget.HasValue) continue;
                if (member.playerAttackTarget != null) continue;
                // 이미 다른 대기 사유(집결·코어 보고 등) 진행 중이면 덮어쓰지 않는다. 다음 문을 찾으며 리더를 따라다니던
                // 공동 탐색 추종(SearchingNextDoor)만 문이 알려진 지금 공동 이동으로 바꿔 덮어쓴다.
                if (member.currentWait != null && member.currentWait.Reason != WaitReason.SearchingNextDoor) continue;
                // 전파받은 적 위치로 접근하던 경계는 공동 이동이 시작되면 접는다(03번 1장 50줄).
                if (member.currentAlertSearch != null && member.currentAlertSearch.IsIndirectEnemyApproach) member.currentAlertSearch = null;
                Vector2Int slot = AIMovementHelper.FindDoorWaitSlot(member, doorPos, claimedDoorSlots);
                member.currentWait = new WaitState { Reason = WaitReason.AdvancingToNextRoom, WaitPosition = slot, DoorPosition = doorPos };
                assigned++;
            }

            // 방 이동 명령 발행 로그 — 집결 완료로 결정된 경우는 매번, 코어 경로의 반복 발행은 2초 간격으로만 남긴다.
            if (decidedByRally || (assigned > 0 && Time.time >= _nextCoreAdvanceLogTime))
            {
                if (!decidedByRally) _nextCoreAdvanceLogTime = Time.time + 2f;
                LogHelper.Log(LogHelper.GAME, $"{partyTag} 방 이동 명령 발행 — 사유: {reason}, 목표 문 ({doorPos.x},{doorPos.y}) [{DescribeDoor(doorPos, doorFloor)}], 이동 대상 {assigned}명"
                    + (assigned == 0 ? " ⚠ 명령을 받을 파티원이 없음(이미 다른 대기 중)" : ""));
            }
            activeParty.ReadyToAdvance = false; // 명령은 1회만 발행 — 도착(또는 통행 불가) 후 개인 행동이 넘겨받는다.
            // hasPendingCore 경로는 ReadyToAdvance가 이미 false였으므로 위 대입은 무해(false→false) —
            // LeaderKnownCorePosition 자체는 Party.TryStartRally가 코어 처리 완료를 감지해 스스로 지운다.
        }

        // 01번 7-1장·04번 소탕 파티: 집결 전에 리더가 현재 방의 다음 이동 문 앞까지 이동하며 확인한다 — 도착하면 Party.MarkLeaderReachedNextDoor가 집결 판단 자격을
        // 만든다(Party.IsRoomActivityComplete MopUp 분기). 문을 아직 모르면 아무것도 하지 않는다 — 리더의 기존 자유 탐색이 시야를 넓혀 문을 찾는다.
        // 리더 외 파티원은 이동시키지 않는다(집결 때 리더 위치로 모임, 진형 합류는 05번 8장 미작성). objectGrid 순회가 있어 0.5초 간격으로만 확인한다.
        private void TryAssignLeaderDoorApproach()
        {
            var party = activeParty;
            if (party.Type != PartyType.MopUp || _targetRoom == null) return;
            if (party.ReadyToAdvance || party.IsRallyActive || party.AdvanceFromRoom != null || party.LeaderKnownCorePosition.HasValue) return;
            var leader = party.Leader;
            if (leader == null || leader.hp <= 0 || leader.currentRoom == null) return;
            if (party.DoorApproachRoom == leader.currentRoom) return;
            if (leader.currentWait != null || stagingUnits.Contains(leader)) return;
            if (leader.isInDungeonEntranceSequence || leader.pendingStairTargetFloor.HasValue) return;
            if (leader.isManualMoveCommand && leader.playerMoveTarget.HasValue) return;
            if (leader.playerAttackTarget != null) return;
            if (Time.time < _nextDoorApproachCheckTime) return;
            _nextDoorApproachCheckTime = Time.time + 0.5f;

            if (!TryFindNextDoorTowardTargetRoom(out Vector2Int doorPos, out int doorFloor) || doorFloor != leader.currentFloor) return;
            Vector2Int slot = AIMovementHelper.FindDoorWaitSlot(leader, doorPos, new HashSet<Vector2Int>());
            leader.currentWait = new WaitState { Reason = WaitReason.ApproachingNextDoor, WaitPosition = slot };
            LogHelper.Log(LogHelper.GAME, $"[파티] {party.Name}({party.Type.ToKorean()}) 리더 {leader.name}: 다음 이동 문 ({doorPos.x},{doorPos.y}) 앞으로 이동하며 확인 시작");
        }

        // 방 이동 명령 로그용 — 목표 문의 소유 진영과 인류 통행 가능 여부(닫힌 적 진영 문은 파괴해야 통과, 04번 10장).
        private static string DescribeDoor(Vector2Int doorPos, int doorFloor)
        {
            var session = GameSession.Instance;
            if (session == null || !session.objectGrid.TryGetValue(new Vector3Int(doorPos.x, doorPos.y, doorFloor), out InteractableObject door))
                return "문 정보 없음";
            bool humanCanPass = door.DoorHp <= 0f || door.DoorOwnerFaction == FactionType.Human;
            return $"문 소유 {door.DoorOwnerFaction}, 인류 통행 {(humanCanPass ? "가능" : "불가 — 파괴 필요")}";
        }

        // 리더 외 적격 파티원에게 공동 탐색 추종을 배정한다(이동 명령과 같은 자격 조건 + 이미 다른 대기가 없고 쿨다운이 지남).
        private void AssignDoorSearchFollowers()
        {
            var leader = activeParty.Leader;
            if (leader == null) return;
            float now = Time.time;
            foreach (var member in activeParty.Members)
            {
                if (member == null || member == leader || member.hp <= 0 || stagingUnits.Contains(member)) continue;
                if (member.currentFloor != leader.currentFloor) continue;
                if (member.isManualMoveCommand && member.playerMoveTarget.HasValue) continue;
                if (member.playerAttackTarget != null) continue;
                if (!PartyDoorSearchSystem.CanAssign(member, now)) continue;
                PartyDoorSearchSystem.Assign(member);
            }
        }

        // 05번 1장: "알려진 문"으로만 진행한다. 리더가 아는(개인 지도에 반영된) 현재 방의 문 중 목표
        // 방에 가장 가까운 것을 고른다 — 진짜 방 그래프 최단경로 대신 좌표 거리로 근사한다(리더·명령
        // 문서가 생기면 교체 대상, 행동경로목표결정 구현현황 문서 "큰 줄기 FSM 재설계" 참고).
        private bool TryFindNextDoorTowardTargetRoom(out Vector2Int doorPos, out int doorFloor)
            => AIMovementHelper.TryFindKnownDoorInCurrentRoom(activeParty.Leader, _targetRoom.Bounds.center, out doorPos, out doorFloor);

        // 웨이브 목표 방(보스방) 중심 — 보고 이동이 "알려진 다음 이동 문"을 고를 때 쓴다(검증문서 03-15). 웨이브가 없으면 null.
        public Vector2? TargetRoomCenter => _targetRoom != null ? _targetRoom.Bounds.center : (Vector2?)null;

        private void EndWave(bool isSuccess)
        {
            currentState = WaveState.Ended;

            var survivors = activeParty != null ? activeParty.GetSurvivors() : new List<Unit>();
            // v0.6 5-6·9-9장: 웨이브 종료 시 유지 중이던 조사·해제 진행도 제거 — 성공/실패 모두 해당한다.
            foreach (var survivor in survivors)
            {
                if (survivor is Human survivorHuman) survivorHuman.ClearInteractionProgress();
            }
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
            _doorSearchLogged = false;
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


