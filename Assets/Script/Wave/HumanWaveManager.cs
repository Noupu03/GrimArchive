using UnityEngine;
using System.Collections.Generic;
using Haare.Client.Routine;
using Cysharp.Threading.Tasks;
using System.Linq;
using VContainer;

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

        public float waveCooldown => targetSpawner?.waveData?.waveCooldown ?? 10f;
        // 첫 웨이브만 넉넉한 시간을 주기 위한 별도 값(기초문서.md 피드백, 2026-08-22 — 사용자 확인
        // "첫 웨이브만 별도로 늘림", 60초). waveData에 값이 없으면(구 버전 에셋) 기존 waveCooldown으로 폴백.
        public float firstWaveCooldown => targetSpawner?.waveData?.firstWaveCooldown ?? waveCooldown;

        [Inject]
        public WaveSpawner targetSpawner; // VContainer를 통해 자동 주입

        public WaveState currentState = WaveState.Idle;
        public float cooldownTimer = 0f;

        // 추적 중인 웨이브 데이터
        public Party activeParty;

        // 코어 전면 개편(기초문서.md 피드백, 2026-08-22) — 웨이브 승리 조건이 "루팅 오브젝트 운반-탈출"
        // 에서 "목표 방의 코어를 인류 소유로 전환"으로 바뀌었다. 목표 방은 예전 던전 코어와 동일하게
        // waveData.targetFloor의 보스방으로 고정한다(_unitGenerate.GetBossRoomPos, 옛
        // SpawnInitialDungeonCore와 동일 좌표 기준). _retreating이 true가 되면(=코어 파괴 성공)
        // UpdatePartyDestination이 목적지를 exitAreaPos로 바꿔 기존 퇴각 로직을 그대로 재사용한다.
        private Room _targetRoom;
        private bool _retreating;

        // 탈출 지점 영역 (임시: 던전 입구/StartRoom 기준 위치)
        public Vector2Int exitAreaPos;

        // ── 0층 사전 스폰 (웨이브 시작 전 대기 연출, 2026-07-23 사용자 요청) ──
        // "웨이브 시작 타이머가 걸리면 0층에서 웨이브 스폰되고, 웨이브가 시작되면 계단으로 이동해서
        // 1층으로 넘어간다"는 흐름을 구현한다. 예외 상태 없이 완전히 정상 GOAP만 쓴다(사용자 요청,
        // 2026-07-23 "시작시 배회인 19번 목표가 되어야 하는데 유닛들 목표가 0번이야 — 19번으로
        // 있다가 웨이브 시작시 목표가 바뀌어야지"). 사전 스폰된 유닛은 아무 목표도 없으니 GOAP이
        // 알아서 최하위 폴백인 Goal_Explore(우선순위 10, Action_RandomExplore=19번)로 배회하고,
        // 웨이브가 시작돼 pendingStairTargetFloor가 세팅되면 Goal_UseStairs(140)가 그보다 훨씬
        // 높은 우선순위로 자연스럽게 목표를 가로챈다 — 별도의 "배회 고정" 상태나 HumanWaveManager의
        // 수동 배회 코드가 전혀 필요 없다.
        // 2026-08-20 이후 이 상수의 역할이 둘로 늘었다: (1) 계단 위치를 못 구해 거리 추정이 불가능할
        // 때 ComputePreSpawnTriggerSeconds()의 폴백 값, (2) DungeonEntranceSystem.PrepareNoticeLeadSeconds
        // 와 같은 값(6초)으로, "인간 파티가 진입을 준비하고 있습니다" 문구가 웨이브 시작(=1층 진입
        // 시작) 몇 초 전에 뜨는지를 나타낸다(사용자 확인: "저 6초 포함해서 10초"). 두 클래스가 서로를
        // 참조하지 않으므로 값 자체는 각자 상수로 따로 들고 있다 — 바꿀 때 둘 다 같이 바꿀 것.
        private const float PreSpawnLeadSeconds = 6f;
        private bool preSpawnTriggered = false;
        // 2026-08-20, 사용자 요청 "잠시후 웨이브가 시작됩니다 문구 및 관련 표시들 등장하는 시점을,
        // 0층 몬스터 소환 시점으로 바꿔줘" — preSpawnTriggered는 "사전 스폰을 시도한 시점"일 뿐이고,
        // 0층 계단 위치를 못 찾아 사전 스폰이 통째로 스킵되는 예외 경로(ResolveStairPositions 실패)
        // 에서는 실제 몬스터 소집(MonsterDefensePlacementSystem.ApplyDefenseStartPositions)이 그보다
        // 훨씬 나중인 StartWave()의 즉시 스폰 폴백 시점에야 일어난다 — 그래서 이 시도 시점 플래그
        // 대신, 실제로 ApplyDefenseStartPositions가 호출된 순간에만 켜지는 전용 플래그를 따로 둔다.
        // WaveGaugePanel(게이지 점멸)이 이 플래그를 직접 구독해서 "정확히 몬스터 소집이 실제로
        // 일어나는 순간"과 항상 일치하게 한다.
        private bool monstersSummonedThisCycle = false;
        public bool IsMonstersSummonedThisCycle => monstersSummonedThisCycle;
        private Party preSpawnedParty;

        // 소집 시점(2026-08-20, 사용자 요청 "소집 시점 바꿔줘. '인간 파티가 진입을 준비하고 있습니다'
        // 시점에서 소집하게") — 예전엔 ApplyDefenseStartPositions가 PreSpawnWaveUnits(0층 스폰 시점)에서
        // 곧바로 호출됐는데, 그건 "진입 준비" 문구(DungeonEntranceSystem.PrepareNoticeLeadSeconds, 웨이브
        // 시작 6초 전)보다 훨씬 이른 시점이었다(ComputePreSpawnTriggerSeconds가 Waiting 10초까지 감안해
        // 역산하므로). 이제는 그 6초 기준을 여기서도 그대로 써서(PreSpawnLeadSeconds, 두 클래스가 반드시
        // 같은 값을 들고 있어야 함 — 위 주석 참고) "진입 준비" 문구와 같은 순간에 소집이 걸리게 한다.
        private bool monsterMusterTriggered = false;
        // preSpawnedParty만으로 "사전 스폰이 성공했는지"를 판단하면 안 되는 이유 — 던전 입구 시퀀스가
        // 아주 빨리 끝나면(대기시간이 극단적으로 짧은 등) OnDungeonEntranceArrivedAtStairs가 위
        // monsterMusterTriggered 체크(cooldownTimer<=PreSpawnLeadSeconds)보다 먼저 preSpawnedParty를
        // null로 비워버릴 수 있다 — StartWave()의 "던전 입구 시퀀스가 빨리 끝난 극단적 경우" 주석과
        // 같은 시나리오. 그 경우에도 소집은 이미 한 번 걸렸어야 하므로, "이번 사이클에 사전 스폰이
        // 성공했다"는 사실 자체는 별도 플래그로 고정해서 preSpawnedParty가 나중에 비워져도 유지한다.
        private bool preSpawnSucceeded = false;

        // 웨이브 게이지 진행도(2026-08-20, 사용자 요청) — waveData에 설정된 시간(waveCooldown) 그대로가
        // 실제 진행 바 시간이 되도록 단순 비율을 쓴다. "1층 진입 시작"(=Waiting 10초가 끝나고
        // WalkingToStairs로 넘어가는 순간, 사용자 확인)이 cooldownTimer==0과 같은 순간이 되도록
        // ComputePreSpawnTriggerSeconds()가 스폰 시점을 미리 계산해두므로, 바가 100%에 도달하는
        // 시점과 실제 1층 진입 시작 시점은 이 스케줄링으로 인해 일치한다(아래 ComputePreSpawnTriggerSeconds
        // 참고).
        public float WaveProgress01
        {
            get
            {
                if (currentState != WaveState.Idle) return 1f;
                float budget = _isFirstWaveCycle ? firstWaveCooldown : waveCooldown;
                return Mathf.Clamp01(1f - cooldownTimer / budget);
            }
        }

        // 아직 0층에서 계단으로 걸어가는 중(=목표 층에 아직 도착 못한) 파티원 집합 — MonitorWave/
        // UpdatePartyDestination의 목표물 추적 로직이 이 유닛들을 건드리지 않도록 걸러내는 데도 쓴다.
        private readonly HashSet<Human> stagingUnits = new HashSet<Human>();
        private Vector2Int floor0StairPos;
        private Vector2Int floor1StairPos;
        private bool stairPosResolved = false;

        // 던전 입구 구조(2026-08-20) — 0층 숨은 스폰 청크~계단까지 파티 진형 이동 시퀀스 전담(자체
        // 클래스로 분리, HumanWaveManager 비대화 방지 — MonsterDefensePlacementSystem/DoorSystem과
        // 동일한 이유). PreSpawnWaveUnits가 시작시키고, 매 프레임 Update, 계단 도달 시
        // OnDungeonEntranceArrivedAtStairs 콜백으로 stagingUnits/pendingStairTargetFloor를 넘겨받는다.
        private readonly DungeonEntranceSystem _dungeonEntrance = new DungeonEntranceSystem();

        // 이전 웨이브에서 살아남아 0층으로 퇴각한 파티원 — 다음 웨이브에 그대로 합류시킨다(사용자
        // 요청, 2026-07-23 "살아남은 유닛들도 다음 웨이브에 포함해서 같이 이동하도록 해줘"). 새로
        // 스폰하지 않고 이미 0층에 있는 유닛을 그대로 다음 파티에 편입한다.
        private readonly List<Human> retreatedSurvivors = new List<Human>();

        // 웨이브 시작 후 이 시간이 지나도 0층에 남아있는 파티원은 강제로 목표 층으로 건너뛰게 한다
        // (사용자 신고 "몇명이 고장나서 0층에 남아있어", 2026-07-23) — 좁은 통로에서 여러 유닛이
        // 서로의 길을 막아 A* 경로가 매 틱 똑같이 실패하는 교착(정적 인원 배치라 자연 해소가 안 됨)
        // 등, 몇 명만 계단 근처에서 영구히 못 넘어오는 경우에 대한 안전장치. 정상적으로는 발동 전에
        // Goal_UseStairs/Action_CrossStairs가 이미 처리한다.
        private const float StairForceCrossTimeoutSeconds = 15f;
        private float runningStateTimer = 0f;

        // 첫 웨이브 쿨다운(firstWaveCooldown)은 딱 한 번만 적용된다 — 두 번째 웨이브부터는 항상
        // waveCooldown 기준. WaveProgress01이 진행 바 분모를 고를 때 참조한다.
        private bool _isFirstWaveCycle = true;

        public override async UniTask Initialize(System.Threading.CancellationToken cts)
        {
            await base.Initialize(cts);
            Instance = this;
            // WaveSpawner.Initialize()와의 NativeRoutine 실행 순서 경합 방지(2026-07-27, 사용자 신고
            // "게임 시작 시 웨이브가 10초인 것 같다") — WaveSpawner.cs의 EnsureWaveDataLoaded() 주석 참고.
            targetSpawner?.EnsureWaveDataLoaded();
            cooldownTimer = firstWaveCooldown;
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
                    // 몬스터 소집 배치(R키 배치모드)는 기초문서.md 피드백(2026-08-22)으로 폐기됐지만,
                    // 이 타이밍 플래그들은 WaveGaugePanel(게이지 점멸)/"진입 준비" 문구가 여전히
                    // 참조하므로 그대로 유지한다(위 monsterMusterTriggered/preSpawnSucceeded 주석 참고).
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

                // 던전 입구 구조(2026-08-20) — cooldownTimer/currentState와 무관하게 독립적으로
                // 진행된다(웨이브가 이미 Running으로 넘어가 문이 닫히고 몬스터가 소집된 뒤에도, 인간
                // 파티는 여전히 입구에서 걸어들어오거나 대기 중일 수 있다 — 사용자 확인 사항).
                // cooldownTimer를 그대로 넘겨 "진입 준비" 문구를 실제 웨이브 시작까지 남은 시간
                // 기준으로 띄우게 한다(DungeonEntranceSystem.Update 참고).
                //
                // 2026-08-20: 한때 일시정지 중엔 Time.unscaledDeltaTime을 써서 이 시퀀스가 정지와
                // 무관하게 진행되게 한 적이 있었는데, 사용자가 정정했다 — "notice가 시간에 영향받지
                // 않게 하라는거지, 웨이브 로직이 시간에 영향 받지 않게 하란 소리가 아니야. 정지되면
                // 웨이브 진행도 멈춰야지." notice 자체는 NoticeCenter가 이미 Time.unscaledDeltaTime로
                // 페이드/지속시간을 계산해서 정지 영향을 안 받으므로, 여기서 별도 처리할 필요가 없었다.
                // 게임 전체와 동일하게 Time.deltaTime을 그대로 쓴다(파티도 게임이 멈추면 같이 멈춘다).
                _dungeonEntrance.Update(GameSession.Instance, Time.deltaTime, cooldownTimer, OnDungeonEntranceArrivedAtStairs);

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: cts);
            }
        }

        // ComputePreSpawnTriggerSeconds()가 계산한 시점(웨이브 시작=1층 진입 시작에 맞춰 역산한 시간)에
        // 이번 웨이브의 인류 파티를 0층에 미리 스폰한다 — 아무 목표도 안 심어주므로 GOAP이 알아서
        // Goal_Explore(19번)로 배회한다. 던전 입구 구조(2026-08-20) 이후로는 이 "사전 스폰 시점"이
        // 문서가 말하는 "웨이브가 던전에 도착한 시점"의 실체다(사용자 확인) — 숨은 스폰 청크에
        // 등장시킨 뒤 DungeonEntranceSystem에게 나머지 진입 시퀀스(입구 이동→대기→계단 이동)를 넘긴다.
        private void PreSpawnWaveUnits()
        {
            if (targetSpawner == null || targetSpawner.waveData == null || targetSpawner.waveData.parties == null) return;
            if (!ResolveStairPositions())
            {
                Debug.LogWarning("[HumanWaveManager] 0층 계단 위치를 찾지 못해 사전 스폰을 건너뜁니다(즉시 스폰으로 대체됩니다).");
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

            // 이전 웨이브에서 살아남아 0층으로 퇴각해 있던 파티원들도 그대로 이번 파티에 합류시킨다
            // (사용자 요청, 2026-07-23) — 이미 0층에 존재하는 유닛이라 새로 스폰하지 않는다. 던전 입구
            // 구조(2026-08-20)부터는 대형 시작 위치를 통일해야 하므로, 이들도 숨은 스폰 청크로
            // 다시 위치시킨다(어차피 플레이어 시야 밖).
            int survivorCount = 0;
            foreach (var survivor in retreatedSurvivors)
            {
                if (survivor == null || survivor.hp <= 0) continue;

                Vector2Int pos = FindSpawnPosInHiddenChunk(rowY);
                Vector2Int survivorOldPos = survivor.position;
                GameSession.Instance.UnregisterUnitPos(survivor, survivorOldPos);
                survivor.currentFloor = 0;
                survivor.position = pos;
                // 2026-08-22 사용자 신고 "어떤 상황에서도 유닛끼리는 겹쳐지면 안돼" — 이미 다른
                // 유닛이 그 칸에 있으면(극히 드문 스폰 혼잡) 등록을 취소하고 원래 자리로 되돌린다.
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
            Debug.Log($"[HumanWaveManager] 0층에 웨이브 파티 {members.Count}명 사전 스폰(배회 대기) — 신규 {members.Count - survivorCount}명, 이전 웨이브 생존자 {survivorCount}명 합류.");

            // 몬스터 배치 프리셋(2026-08-19 재구현, 사용자 요청 "0층에 인류가 소환된 시점부터, 몬스터들은
            // 배치모드에서 배치했던 지점으로 이동하고 소집 대기를 해") — 처음엔 바로 이 지점("0층에
            // 인류가 소환된 시점")에서 곧바로 소집했는데, 2026-08-20 사용자 요청("소집 시점 바꿔줘.
            // '인간 파티가 진입을 준비하고 있습니다' 시점에서 소집하게")으로 실제 소집 호출은
            // WaveLoop()의 cooldownTimer <= PreSpawnLeadSeconds 체크(위 monsterMusterTriggered)로
            // 옮겨졌다 — 여기서는 그 체크가 참조할 preSpawnedParty만 준비해둔다.

            // 던전 입구 구조(2026-08-20) — 스폰 직후 곧바로 입구 진입 시퀀스(숨은 청크→1x3 입구
            // 이동→대기→계단 이동)를 시작한다. floor0StairPos는 ResolveStairPositions가 이미
            // 계단 바로 옆 실제로 밟을 수 있는 타일로 구해뒀다(위 rowY와 같은 행).
            _dungeonEntrance.Begin(GameSession.Instance, preSpawnedParty, rowY, DungeonEntranceRoomEntryX, floor0StairPos.x);
        }

        // 사전 스폰(및 던전 입구 시퀀스 시작) 트리거 시점을 계산한다(2026-08-20, 사용자 확인) —
        // 예전엔 PreSpawnLeadSeconds(6초) 고정이었지만, "웨이브 진행 바 100% = 1층 진입 시작(=Waiting
        // 10초가 끝나 WalkingToStairs로 넘어가는 순간)"이 되려면 WalkingIn(입구까지 이동)이 그
        // Waiting 10초가 시작되기 전에 끝나 있어야 한다. 실제 파티 구성(이동속도)은 스폰 전엔 알 수
        // 없으므로, BaseStatComponent 기본 이동속도(3f)를 기준으로 입구까지 거리를 추정해 필요한
        // 시간을 구하고, 그 뒤에 Waiting 10초(DungeonEntranceSystem.WaitSeconds)를 더한다 — 이 총
        // 시간만큼 cooldownTimer가 남았을 때 스폰한다. 계단 위치를 못 구했으면(맵에 계단 데이터가
        // 없는 등) 추정할 거리가 없으므로 예전처럼 PreSpawnLeadSeconds를 그대로 쓴다(뒤이어
        // ResolveStairPositions가 다시 실패하면 PreSpawnWaveUnits가 즉시 스폰 폴백으로 넘긴다).
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

            // 계단 블록 자체(TryGetStairPosition)가 아니라 그 바로 옆 실제로 밟을 수 있는 타일
            // (TryGetStairApproachPosition)을 쓴다 — 계단 타일은 isStructureExist라 유닛이 서 있을 수
            // 없고, 이걸 퇴각 목표(exitAreaPos)로 쓰면 A*가 안개 속에서 그 타일을 목적지로 잡아
            // 영원히 도착 못 하는 버그가 있었다(사용자 제보 콘솔 로그, 2026-07-23).
            int targetFloor = targetSpawner.waveData.targetFloor;

            // 0층 쪽은 힌트 없이 부르면(2026-08-20 사용자 신고 "대기할 때 아래로 쳐져있어") 항상
            // dx=-1,dy=-1(계단 블록 바로 왼쪽 위) 칸을 먼저 찾아 반환해서, 이걸 그대로 rowY로 쓰던
            // 던전 입구 대형 전체가 청크 세로 중앙(계단 블록이 차지하는 두 중앙 행)보다 한 칸 아래로
            // 처져 있었다. 계단 블록 좌상단(TryGetStairPosition, 두 중앙 행 중 위쪽)과 같은 행을
            // 힌트로 줘서 그 행 위의 칸(블록 바로 왼쪽)을 고르게 한다.
            if (!GameSession.Instance.cmap.TryGetStairPosition(0, targetFloor, out Vector2Int floor0StairBlockPos)) return false;
            Vector2Int floor0RowHint = new Vector2Int(floor0StairBlockPos.x - 1, floor0StairBlockPos.y);
            if (!GameSession.Instance.cmap.TryGetStairApproachPosition(0, targetFloor, floor0RowHint, out floor0StairPos)) return false;

            if (!GameSession.Instance.cmap.TryGetStairApproachPosition(targetFloor, 0, out floor1StairPos)) return false;

            stairPosResolved = true;
            return true;
        }

        // 던전 입구 구조(2026-08-20) — 0층 최좌측 숨은 1x1 스폰 청크(카메라 관찰 범위 밖,
        // CameraController.Floor0HiddenChunksX 참고) 안에서 스폰 위치를 고른다. rowY는
        // DungeonEntranceSystem이 그대로 이어받아 쓸 행이라 ResolveStairPositions가 구한
        // floor0StairPos.y와 통일한다(입구~계단까지 한 행에서 직선으로만 움직이면 되게).
        private const int DungeonEntranceHiddenChunkCenterX = 4; // 청크0(숨김) 로컬 중앙.
        private const int DungeonEntranceRoomEntryX = 12;        // 청크1(가시 영역 최좌측) 중앙.

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

        // 던전 입구 구조(2026-08-20) — DungeonEntranceSystem이 파티 진형을 계단까지 이끌고 도착했을
        // 때(0층 던전 계단 도달) 호출하는 콜백. 여기서 비로소 pendingStairTargetFloor를 세팅해
        // Goal_UseStairs(140, 최우선순위)가 Goal_Explore/던전 입구 시퀀스 홀드를 밀어내고 정상
        // GOAP 계단 통과를 맡게 한다(StartWave에서 하던 일을 그대로 여기로 옮긴 것 — "웨이브 시작"과
        // "인간 파티의 실제 1층 진입"이 이제 서로 다른 시점이기 때문).
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

        // WaveState.Running 동안(웨이브 시작 후) 매 프레임 호출 — 계단 이동/통과 자체는 정상
        // GOAP(Goal_UseStairs → Action_MoveToStairs → Action_CrossStairs, Unit.pendingStairTargetFloor
        // 기반)이 전담한다(사용자 요청, 2026-07-23 "예외처리 없이 goap로직에 넣어도 되겠군"). 여기서는
        // 목표 층에 도착한 파티원을 stagingUnits에서 빼는 것과, 시간이 너무 지나도 못 넘어온 파티원을
        // 강제로 건너뛰는 안전장치만 담당한다. 목표(루팅 오브젝트) 재할당은 MonitorWave가 매 틱
        // UpdatePartyDestination을 돌려서 자동으로 따라잡는다.
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
                    // 도착 지점이 전부 점유돼 있으면(극단적 혼잡, 2026-08-05 겹침 수정) 이번 틱은
                    // 실패로 보고 staging에 그대로 남겨 다음 틱에 재시도한다.
                    if (ForceCrossToTargetFloor(member, targetFloor))
                        arrived.Add(member);
                }
            }

            foreach (var member in arrived) stagingUnits.Remove(member);
        }

        // 정상 GOAP 경로(Action_CrossStairs)가 시간 안에 처리하지 못한 파티원을 강제로 목표 층
        // 계단 지점으로 옮긴다 — 위치/그리드만 직접 갱신하고 나머지(목표 배정 등)는 다음 틱
        // UpdatePartyDestination이 이어받는다.
        // 2026-08-05 사용자 신고 "유닛끼리 겹친다" 수정 — 예전엔 캐시해둔 floor1StairPos(항상 같은
        // 대표 좌표 1칸)로 점유 확인 없이 텔레포트해서, 같은 틱에 여러 파티원이 강제 이동되면 전부
        // 같은 칸에 겹쳤다. 점유 안 된 후보 칸을 찾아서 그쪽으로 보내고, 전부 점유면(극단적 혼잡)
        // false를 반환해 호출부가 다음 틱에 재시도하게 한다. 반환값: 실제로 이동했는지.
        private bool ForceCrossToTargetFloor(Human member, int targetFloor)
        {
            if (!AIMovementHelper.TryResolveUnoccupiedStairArrival(GameSession.Instance, targetFloor, 0, out Vector2Int arrivePos))
                return false;

            Vector2Int memberOldPos = member.position;
            int memberOldFloor = member.currentFloor;
            GameSession.Instance.UnregisterUnitPos(member, memberOldPos);
            member.currentFloor = targetFloor;
            member.position = arrivePos;
            // 2026-08-22 사용자 신고 "어떤 상황에서도 유닛끼리는 겹쳐지면 안돼" — TryResolveUnoccupiedStairArrival
            // 이 확인한 시점과 이 등록 시점 사이에 다른 경로가 같은 칸을 먼저 차지했을 수 있는 최종 안전망.
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

            Debug.LogWarning($"[HumanWaveManager] {member.unitType.typeName}가 {StairForceCrossTimeoutSeconds}초 동안 계단을 못 넘어와 강제로 F{targetFloor}로 이동시켰습니다.");
            return true;
        }

        // 목표를 확보하고 탈출 지점(계단)에 도착한 파티원을 게임에서 지우는 대신 0층으로 돌려보낸다
        // (사용자 요청, 2026-07-23 "퇴각 로직 후에 0층으로 이동시키자") — 0층 계단 지점에 다시 등장해
        // 아무 목표도 없는 정상 GOAP 상태로 남는다(자동으로 Goal_Explore 배회). retreatedSurvivors에
        // 등록해서 다음 웨이브 사전 스폰 때 그대로 합류시킨다(사용자 요청, 2026-07-23 "살아남은
        // 유닛들도 다음 웨이브에 포함해서 같이 이동하도록 해줘"). 계단 위치를 못 구하면(맵에 계단
        // 데이터가 없는 등) 안전하게 예전처럼 게임에서 제거한다(이 경우 합류시킬 수 없음).
        private void RetreatMemberToFloor0(Human member)
        {
            if (member == null) return;
            if (!ResolveStairPositions())
            {
                GameSession.Instance.DespawnUnit(member);
                return;
            }

            // 2026-08-05 사용자 신고 "유닛끼리 겹친다" 수정 — 파티 전원이 한꺼번에 퇴각할 때(같은
            // foreach 루프 안에서 연달아 호출됨) 전부 같은 floor0StairPos 한 칸으로 텔레포트해서
            // 겹쳤다. 점유 안 된 후보 칸을 찾아 보낸다 — 이 호출부는(퇴각 루프) "다음 틱 재시도"가
            // 자연스럽지 않은 일회성 호출이라, 후보가 전부 점유된 극단적 혼잡(거의 안 생김)에서만
            // 예전처럼 대표 좌표로 보내고 경고를 남긴다(완전히 막아 그 파티원이 영영 못 돌아오게
            // 하는 것보다 낫다는 판단).
            int targetFloor = targetSpawner.waveData.targetFloor;
            if (!AIMovementHelper.TryResolveUnoccupiedStairArrival(GameSession.Instance, 0, targetFloor, out Vector2Int arrivePos))
            {
                Debug.LogWarning($"[HumanWaveManager] {member.unitType.typeName} 퇴각 도착 지점이 전부 점유돼 대표 좌표로 보냅니다(드물게 겹칠 수 있음).");
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

            Debug.Log($"[HumanWaveManager] {member.unitType.typeName}가 퇴각하여 0층으로 돌아갔습니다.");
        }

        // 웨이브 시각화(2026-08-19 신규) — 게이지 위에 표시할 "인간 파티" 아이콘 후보 유닛 타입
        // 이름을 최대 max개 반환한다. 이미 사전 스폰된 파티가 있으면(마지막 PreSpawnLeadSeconds
        // 구간) 그 실제 구성을, 아직 스폰 전이면 waveData에 정의된 다음 웨이브의 인류 파티 구성을
        // 그대로 사용한다(실제 스폰과 동일한 순서 — 위 PreSpawnWaveUnits 참고).
        //
        // 2026-08-20 사용자 신고("기사형 3명이 떠버려서 어색하다") — 예전엔 파티 순서대로 앞 max명을
        // 그대로 뽑아서, 같은 유형이 앞쪽에 몰려 있으면 그 유형만 중복으로 보였다. 유형별로 먼저
        // 하나씩 채우고(BuildDistinctFirstTypeNames), 실제 유형 종류가 max보다 적을 때만 중복을
        // 허용해 남은 자리를 채운다(사용자 요청 "유형별로 1개씩... 유형이 3개 미만이라면 중복 허용").
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

        // 서로 다른 유형을 우선 하나씩 채우고, 유형 종류가 max보다 적을 때만 원래 순서대로 다시 채워
        // 남는 자리를 중복으로 메운다. 실제 인원수(allNames.Count)보다 많은 아이콘은 만들지 않는다
        // (인원이 2명뿐인데 3개를 채우겠다고 없는 3번째를 지어내지 않음).
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
                Debug.LogError("[HumanWaveManager] Target Spawner가 설정되지 않았습니다.");
                return;
            }

            Debug.Log("[HumanWaveManager] 인류 웨이브 발생! (목표물이 생성될 때까지 대기합니다)");
            currentState = WaveState.Running;

            // 문은 이제 항상 기본적으로 닫혀있고 진영·근접 여부로 매 프레임 스스로 개폐한다
            // (기초문서.md 피드백, 2026-08-22 — DoorSystem.UpdateProcess) — 웨이브 시작 시점에 별도로
            // 잠글 필요가 없어졌다.
            runningStateTimer = 0f;

            if (preSpawnedParty != null && preSpawnedParty.Members.Count > 0)
            {
                // 0층에 미리 대기시켜둔 파티를 그대로 이번 웨이브에 쓴다. 던전 입구 구조(2026-08-20,
                // 사용자 확인) 이후로는 이 시점에도 파티가 아직 입구를 걸어들어오거나 대기 중일 수
                // 있다 — "웨이브 시작"(문 닫힘/몬스터 소집, 지금 이 메서드)과 "인간 파티의 실제 1층
                // 진입"은 이제 서로 다른 시점이다. 여기서는 activeParty/exitAreaPos만 미리 세팅해
                // 다른 시스템(웨이브 게이지 등)이 참조할 수 있게 하고, stagingUnits는 비워둔 채로
                // 둔다 — pendingStairTargetFloor는 DungeonEntranceSystem이 계단에 실제로 도달했을
                // 때(OnDungeonEntranceArrivedAtStairs) 세팅해야 Goal_UseStairs가 그 전에 끼어들지
                // 않는다.
                activeParty = preSpawnedParty;
                exitAreaPos = floor1StairPos; // 목표 층 진입 지점을 그대로 탈출 지점으로도 사용
            }
            else if (monstersSummonedThisCycle)
            {
                // 던전 입구 시퀀스가 PreSpawnLeadSeconds보다 빨리 끝나(대기시간이 극단적으로
                // 짧아지는 등) 이 시점 이전에 이미 OnDungeonEntranceArrivedAtStairs로 완료 처리된
                // 극단적 경우 — preSpawnedParty가 이미 비워져 있다. 이번 사이클엔 이미 정상 진행됐다는
                // 뜻이라 아래 즉시 스폰 폴백으로 새 파티를 중복 생성하지 않는다(activeParty는 이미
                // 세팅돼 있음).
            }
            else
            {
                // 사전 스폰 자체가 안 됐으면(0층 계단 위치를 못 찾는 등 PreSpawnWaveUnits 실패) 예전처럼
                // 즉시 스폰한다 — 기존 WaveSpawner 재사용. 이 폴백 경로는 던전 입구 시퀀스를 거치지
                // 않고 계단 바로 옆에 즉시 등장한다(입구 연출은 스킵되지만 예외적 안전장치이므로 허용).
                int beforePartyCount = GameSession.Instance.parties.Count;
                targetSpawner.SpawnWave();

                if (GameSession.Instance.parties.Count > beforePartyCount)
                {
                    activeParty = GameSession.Instance.parties.Last();
                    if (activeParty.Members.Count > 0)
                    {
                        exitAreaPos = activeParty.Members[0].position;
                    }

                    // 몬스터 소집 배치(R키 배치모드)는 기초문서.md 피드백(2026-08-22)으로 폐기됐지만,
                    // 이 플래그는 WaveGaugePanel이 여전히 참조하므로 그대로 유지한다.
                    monstersSummonedThisCycle = true;
                }
                else
                {
                    Debug.LogWarning("[HumanWaveManager] 웨이브 소환 시도했으나 파티가 생성되지 않았습니다.");
                    EndWave(false);
                    return;
                }
            }

            // 코어 전면 개편(기초문서.md 피드백, 2026-08-22) — 목표 방(옛 던전 코어와 동일하게
            // targetFloor의 보스방)의 Room을 미리 찾아둔다. 실제 코어 위치는 GameSession.
            // SpawnAllRoomCores가 게임 시작 시 채워둔 room.CorePosition을 그대로 쓴다.
            _retreating = false;
            _targetRoom = null;
            int targetFloor = targetSpawner.waveData.targetFloor;
            if (GameSession.Instance?.unitGenerate != null)
            {
                Vector2Int bossPos = GameSession.Instance.unitGenerate.GetBossRoomPos(Vector2.one, targetFloor);
                GameSession.Instance.roomGrid.TryGetValue(new Vector3Int(bossPos.x, bossPos.y, targetFloor), out _targetRoom);
            }
            if (_targetRoom == null || _targetRoom.CoreObjectId == null)
                Debug.LogWarning("[HumanWaveManager] 목표 방의 코어를 찾지 못했습니다 — 이번 웨이브는 목표 없이 진행됩니다.");
        }

        private void MonitorWave()
        {
            if (activeParty == null || activeParty.IsWiped)
            {
                if (_retreating)
                {
                    Debug.Log("[HumanWaveManager] 파티가 전멸했지만 코어는 이미 파괴되어 웨이브 성공.");
                    EndWave(true);
                }
                else
                {
                    Debug.Log("[HumanWaveManager] 파티가 전멸했습니다. 웨이브 실패.");
                    EndWave(false);
                }
                return;
            }

            int targetFloor = targetSpawner.waveData.targetFloor;

            // 코어 전면 개편(기초문서.md 피드백, 2026-08-22) — 목표 방의 코어를 파괴(=인류 소유로
            // 전환, OffenseProcessor.OnCoreDestroyed)하면 그 순간 퇴각으로 전환한다. 실제 코어 공격은
            // TacticalFSMState.CoreAttackPerform/UnitFunction.OnUpdate가 담당 — 여기서는 목적지
            // 지정과 성공 판정만 한다.
            if (!_retreating && _targetRoom != null && _targetRoom.RoomFaction == FactionType.Human)
            {
                Debug.Log("[HumanWaveManager] 목표 방 코어 파괴 성공! 생존 파티원 퇴각 시작.");
                _retreating = true;
            }

            // 목표가 이미 정해진 뒤에도 매 틱 다시 적용한다(사용자 요청, 2026-07-23) — 계단을 막
            // 넘어와 stagingUnits에서 빠진 파티원처럼, 목표가 "처음 발견된 순간"엔 아직 다른 층에
            // 있어서 건너뛰어졌던 유닛도 이걸로 자동으로 따라잡는다(같은 값 재적용이라 매 틱 불러도 무해).
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
                    Debug.Log($"[HumanWaveManager] {member.name} 유닛 개별 탈출 성공.");

                    // 탈출 지점에 도착한 파티원은 사라지는(Despawn) 대신 0층으로 돌려보낸다
                    // (사용자 요청, 2026-07-23 "퇴각 로직 후에 0층으로 이동시키자").
                    RetreatMemberToFloor0(member);
                    activeParty.Members.RemoveAt(i);
                }
            }

            // 모든 파티원이 탈출했거나 사망했다면 웨이브 종료
            if (activeParty.GetSurvivors().Count == 0)
            {
                Debug.Log("[HumanWaveManager] 코어 파괴 후 모든 파티원이 탈출(또는 사망)하여 웨이브가 성공적으로 종료됩니다.");
                EndWave(true);
            }
        }

        private void UpdatePartyDestination()
        {
            if (!_retreating && _targetRoom == null) return; // 목표 없음 — 자유 배회에 맡김(에러 상황)

            Vector2Int dest = _retreating ? exitAreaPos : new Vector2Int(_targetRoom.CorePosition.x, _targetRoom.CorePosition.y);
            int destFloor = _retreating ? targetSpawner.waveData.targetFloor : _targetRoom.CorePosition.z;

            // 인지 범위를 해당 층 내로 한정한다(사용자 요청, 2026-07-23) — 목표와 다른 층에 있는
            // 파티원(주로 아직 0층에서 계단으로 이동 중인 사전 스폰 파티원)은 건드리지 않는다. 그쪽은
            // Goal_UseStairs/Action_CrossStairs가 도착할 때까지 별도로(pendingStairTargetFloor 기반)
            // 관리한다.
            foreach (var member in activeParty.Members)
            {
                if (member == null || member.hp <= 0 || stagingUnits.Contains(member)) continue;
                if (member.currentFloor != destFloor) continue;
                // 플레이어 수동 명령 진행 중이면 웨이브의 자동 목표 재할당이 덮어쓰지 않는다(사용자
                // 신고, 2026-07-24 "플레이어 지정 명령 잘 안 작동해") — 이 메서드가 매 틱 호출돼
                // isManualMoveCommand를 계속 false로 되돌리는 바람에 우클릭 명령이 사실상 무시됐다.
                // 명령이 끝나면(PlayerCommandFSMState가 플래그 정리) 다음 틱부터 자동으로 다시 챙긴다.
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
                // 파티가 전멸 없이 목표를 확보한 채 웨이브가 끝난 경우(예: 개별 탈출 루프를 거치지
                // 않고 곧장 성공 처리된 경로) 남은 생존자도 사라지는 대신 0층으로 돌려보낸다(사용자
                // 요청, 2026-07-23 "퇴각 로직 후에 0층으로 이동시키자").
                foreach (var survivor in survivors)
                {
                    if (survivor is Human survivorHuman)
                    {
                        RetreatMemberToFloor0(survivorHuman);
                    }
                }
            }

            Debug.Log($"[HumanWaveManager] 웨이브 정리 완료. 다음 웨이브까지 {waveCooldown}초 대기.");

            activeParty = null;
            _targetRoom = null;
            _retreating = false;
            cooldownTimer = waveCooldown;
            _isFirstWaveCycle = false;
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


