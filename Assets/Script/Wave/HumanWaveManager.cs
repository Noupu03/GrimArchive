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

    public enum DummyTargetState
    {
        OnGround,
        Carried,
        WaitingForTarget, // ?��?가 ?�브?�트�??�성???�까지 ?�기하???�태 추�?
        Secured // 목표물이 ?�전?�게 반출 ?�료?�었?�나, ?�머지 ?�티?�들???�출 중인 ?�태
    }

    /// <summary>
    /// ?�류 ?�이브의 ?�이?�사?�클(발생, 목표 추적, ?�탈, 종료)???�제?�는 ?�스?�입?�다.
    /// 기획???�칙???�라 발생조건, 목적, ?��? 구성, 종료 조건??모듈?�하??관리합?�다.
    /// </summary>
    public class HumanWaveManager : NativeRoutine
    {
        public static HumanWaveManager Instance { get; private set; }

        public float waveCooldown => targetSpawner?.waveData?.waveCooldown ?? 10f;

        [Inject]
        public WaveSpawner targetSpawner; // VContainer를 통해 자동 주입

        public WaveState currentState = WaveState.Idle;
        public float cooldownTimer = 0f;

        // 추적 중인 ?�이�??�이??
        public Party activeParty;
        public InteractableObject dummyTarget;
        public DummyTargetState targetState = DummyTargetState.WaitingForTarget;
        public Human targetCarrier = null;

        // ?�탈 지???�역 (?�시: ?�전 ?�구??StartRoom 기�? ?�치)
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
        private const float PreSpawnLeadSeconds = 6f;
        private bool preSpawnTriggered = false;
        private Party preSpawnedParty;
        // 아직 0층에서 계단으로 걸어가는 중(=목표 층에 아직 도착 못한) 파티원 집합 — MonitorWave/
        // UpdatePartyDestination의 목표물 추적 로직이 이 유닛들을 건드리지 않도록 걸러내는 데도 쓴다.
        private readonly HashSet<Human> stagingUnits = new HashSet<Human>();
        private Vector2Int floor0StairPos;
        private Vector2Int floor1StairPos;
        private bool stairPosResolved = false;

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

        // O 오브젝트(목표) 수집 소요시간 — 이전엔 도착 즉시 획득이었는데 "너무 바로 가져가 버린다"는
        // 사용자 피드백(2026-07-23)으로 10초 수집 시간을 부여한다. 같은 유닛이 목표 타일에 계속
        // 머무는 동안만 진행도가 쌓이고, 자리를 벗어나거나 다른 유닛으로 바뀌면 초기화된다.
        private const float ObjectPickupDurationSeconds = 10f;
        private float pickupProgressSeconds = 0f;
        private Human pickupCandidateUnit = null;

        // 03문서 7-2장(2026-07-27 개정): "웨이브 진입 전 설정된 파티 목표 오브젝트에 도달한 유닛은
        // 1초 동안 합류 정보를 전파한 뒤 상호작용(=여기서는 10초 수집 타이머)을 시작한다." dummyTarget이
        // 이 문서가 말하는 "파티 목표 오브젝트"의 실체라 도착~수집 사이에 1초 지연을 끼워 넣는다.
        // 다른 파티원의 "1초 재전파 후 합류" 절반은 UpdatePartyDestination이 이미 매 틱 전원에게
        // playerMoveTarget을 부여하는(즉시·상시 공유) 더 단순한 모델이라 별도 재전파 지연을 얹을
        // 실익이 없어 생략했다(구현현황에 근사 사유 기재).
        private const float PartyGoalJoinPropagationSeconds = 1f;
        private float joinPropagationTimer = 0f;

        public override async UniTask Initialize(System.Threading.CancellationToken cts)
        {
            await base.Initialize(cts);
            Instance = this;
            // WaveSpawner.Initialize()와의 NativeRoutine 실행 순서 경합 방지(2026-07-27, 사용자 신고
            // "게임 시작 시 웨이브가 10초인 것 같다") — WaveSpawner.cs의 EnsureWaveDataLoaded() 주석 참고.
            targetSpawner?.EnsureWaveDataLoaded();
            cooldownTimer = waveCooldown;
            currentState = WaveState.Idle;

            // Haare Framework 기�?: UniTask 기반 Native Routine 루프 ?�행
            WaveLoop(cts).Forget();
        }

        private async UniTaskVoid WaveLoop(System.Threading.CancellationToken cts)
        {
            while (!cts.IsCancellationRequested)
            {
                if (currentState == WaveState.Idle)
                {
                    cooldownTimer -= Time.deltaTime;
                    if (!preSpawnTriggered && cooldownTimer <= PreSpawnLeadSeconds)
                    {
                        preSpawnTriggered = true;
                        PreSpawnWaveUnits();
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

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: cts);
            }
        }

        // 웨이브 시작 PreSpawnLeadSeconds초 전에 이번 웨이브의 인류 파티를 0층에 미리 스폰한다 —
        // 아무 목표도 안 심어주므로 GOAP이 알아서 Goal_Explore(19번)로 배회한다.
        private void PreSpawnWaveUnits()
        {
            if (targetSpawner == null || targetSpawner.waveData == null || targetSpawner.waveData.parties == null) return;
            if (!ResolveStairPositions())
            {
                Debug.LogWarning("[HumanWaveManager] 0층 계단 위치를 찾지 못해 사전 스폰을 건너뜁니다(즉시 스폰으로 대체됩니다).");
                return;
            }

            var members = new List<Human>();
            foreach (var config in targetSpawner.waveData.parties)
            {
                if (config == null || config.faction != PartyFaction.Human || config.units == null) continue;
                foreach (var group in config.units)
                {
                    if (string.IsNullOrEmpty(group.unitTypeName)) continue;
                    for (int i = 0; i < group.count; i++)
                    {
                        Vector2Int spawnPos = FindSpawnPosNearFloor0Entrance();
                        Human human = targetSpawner.InstantiatePreSpawnHumanAt(group.unitTypeName, spawnPos, 0);
                        if (human == null) continue;

                        members.Add(human);
                    }
                }
            }

            // 이전 웨이브에서 살아남아 0층으로 퇴각해 있던 파티원들도 그대로 이번 파티에 합류시킨다
            // (사용자 요청, 2026-07-23) — 이미 0층에 존재하는 유닛이라 새로 스폰하지 않는다.
            int survivorCount = 0;
            foreach (var survivor in retreatedSurvivors)
            {
                if (survivor == null || survivor.hp <= 0) continue;
                members.Add(survivor);
                survivorCount++;
            }
            retreatedSurvivors.Clear();

            if (members.Count == 0) return;

            preSpawnedParty = GameSession.Instance.CreateParty("PreSpawnParty", members);
            Debug.Log($"[HumanWaveManager] 0층에 웨이브 파티 {members.Count}명 사전 스폰(배회 대기) — 신규 {members.Count - survivorCount}명, 이전 웨이브 생존자 {survivorCount}명 합류.");
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
            if (!GameSession.Instance.cmap.TryGetStairApproachPosition(0, targetFloor, out floor0StairPos)) return false;
            if (!GameSession.Instance.cmap.TryGetStairApproachPosition(targetFloor, 0, out floor1StairPos)) return false;

            stairPosResolved = true;
            return true;
        }

        private Vector2Int FindSpawnPosNearFloor0Entrance()
        {
            var generator = GameSession.Instance.unitGenerate;
            for (int i = 0; i < 20; i++)
            {
                int ox = UnityEngine.Random.Range(-4, 5);
                int oy = UnityEngine.Random.Range(-4, 5);
                Vector2Int cand = floor0StairPos + new Vector2Int(ox, oy);
                if (generator != null && generator.IsAreaClear(cand, Vector2.one, 0)) return cand;
            }
            return floor0StairPos;
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
                    ForceCrossToTargetFloor(member, targetFloor);
                    arrived.Add(member);
                }
            }

            foreach (var member in arrived) stagingUnits.Remove(member);
        }

        // 정상 GOAP 경로(Action_CrossStairs)가 시간 안에 처리하지 못한 파티원을 강제로 목표 층
        // 계단 지점으로 옮긴다 — 위치/그리드만 직접 갱신하고 나머지(목표 배정 등)는 다음 틱
        // UpdatePartyDestination이 이어받는다.
        private void ForceCrossToTargetFloor(Human member, int targetFloor)
        {
            GameSession.Instance.UnregisterUnitPos(member, member.position);
            member.currentFloor = targetFloor;
            member.position = floor1StairPos;
            GameSession.Instance.RegisterUnitPos(member, member.position);

            member.pendingStairTargetFloor = null;
            member.playerMoveTarget = null;
            member.isManualMoveCommand = false;

            Debug.LogWarning($"[HumanWaveManager] {member.unitType.typeName}가 {StairForceCrossTimeoutSeconds}초 동안 계단을 못 넘어와 강제로 F{targetFloor}로 이동시켰습니다.");
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

            GameSession.Instance.UnregisterUnitPos(member, member.position);
            member.currentFloor = 0;
            member.position = floor0StairPos;
            GameSession.Instance.RegisterUnitPos(member, member.position);

            member.pendingStairTargetFloor = null;
            member.playerMoveTarget = null;
            member.isManualMoveCommand = false;

            retreatedSurvivors.Add(member);

            Debug.Log($"[HumanWaveManager] {member.unitType.typeName}가 퇴각하여 0층으로 돌아갔습니다.");
        }

        private void StartWave()
        {
            if (targetSpawner == null)
            {
                Debug.LogError("[HumanWaveManager] Target Spawner가 ?�정?��? ?�았?�니??");
                return;
            }

            Debug.Log("[HumanWaveManager] ?�류 ?�이�?발생! (목표물이 ?�성???�까지 ?�기합?�다)");
            currentState = WaveState.Running;
            runningStateTimer = 0f;

            if (preSpawnedParty != null && preSpawnedParty.Members.Count > 0)
            {
                // 0층에 미리 대기시켜둔 파티를 그대로 이번 웨이브에 쓴다 — 새로 스폰하는 대신
                // 계단으로 걸어가게 하고, UpdateStagingStairWalk가 도착을 감시해서 목표 층으로
                // 넘겨준다.
                activeParty = preSpawnedParty;
                preSpawnedParty = null;
                exitAreaPos = floor1StairPos; // 목표 층 진입 지점을 그대로 탈출 지점으로도 사용

                // pendingStairTargetFloor를 세팅하는 순간 Goal_UseStairs(140, 최우선순위)가
                // Goal_Explore(10, 대기 중 배회하던 것)를 밀어내고 계단 이동/통과를 맡는다(사용자 요청,
                // 2026-07-23). 층별 인지 필터(UpdatePartyDestination/MonitorWave)가 있어서 계단을
                // 넘기 전까지는 다른 층의 웨이브 목표를 잘못 쫓아가지 않는다.
                stagingUnits.Clear();
                foreach (var member in activeParty.Members)
                {
                    if (member == null) continue;
                    member.pendingStairTargetFloor = targetSpawner.waveData.targetFloor;
                    stagingUnits.Add(member);
                }
            }
            else
            {
                // 사전 스폰이 안 됐으면(대기시간이 PreSpawnLeadSeconds보다 짧았던 경우 등) 예전처럼
                // 즉시 스폰한다 — 기존 WaveSpawner 재사용.
                int beforePartyCount = GameSession.Instance.parties.Count;
                targetSpawner.SpawnWave();

                if (GameSession.Instance.parties.Count > beforePartyCount)
                {
                    activeParty = GameSession.Instance.parties.Last();
                    if (activeParty.Members.Count > 0)
                    {
                        exitAreaPos = activeParty.Members[0].position;
                    }
                }
                else
                {
                    Debug.LogWarning("[HumanWaveManager] 웨이브 소환 시도했으나 파티가 생성되지 않았습니다.");
                    EndWave(false);
                    return;
                }
            }

            // 2. ?��?가 O?�로 목표물을 ?�폰???�까지 ?��??�태 진입
            targetState = DummyTargetState.WaitingForTarget;
            dummyTarget = null;
            targetCarrier = null;
        }

        private void MonitorWave()
        {
            if (activeParty == null || activeParty.IsWiped)
            {
                if (targetState == DummyTargetState.Secured)
                {
                    Debug.Log("[HumanWaveManager] ?��? ?�티?�이 ?�멸?��?�?목표???��? 반출?�었?�니?? ?�이�??�공.");
                    EndWave(true);
                }
                else
                {
                    Debug.Log("[HumanWaveManager] ?�티가 ?�멸?�습?�다. ?�이�??�패.");
                    EndWave(false);
                }
                return;
            }

            int targetFloor = targetSpawner.waveData.targetFloor;

            // 유저가 목표 오브젝트(O키)를 생성할 때까지 대기 — 인지 범위를 웨이브가 실제로 진행되는
            // 층(targetFloor)으로 한정한다(사용자 요청, 2026-07-23 "유닛들의 인지 범위를 해당 층
            // 내로") — 다른 층(예: 아직 계단을 안 넘은 0층)에 있는 오브젝트는 이 웨이브의 목표로
            // 인식하지 않는다.
            if (targetState == DummyTargetState.WaitingForTarget)
            {
                foreach (var obj in GameSession.Instance.objectGrid.Values)
                {
                    // O키로 생성된 오브젝트가 "Loot" 태그를 가짐 (하위 계층 포함 지원)
                    if (obj != null && obj.Position.z == targetFloor && obj.Tags != null && obj.Tags.Any(tag => tag.Contains("Loot")))
                    {
                        dummyTarget = obj;
                        targetState = DummyTargetState.OnGround;
                        UpdatePartyDestination();
                        Debug.Log($"[HumanWaveManager] ?��?가 ?�성??목표�?{obj.Id})??발견?�습?�다! 추적???�작?�니??");
                        break;
                    }
                }

                // 여전히 목표가 없다면(또는 그 층에 없다면) 유닛들은 기본 AI에 따라 자유롭게
                // 돌아다니도록 둡니다. (0층에서 계단으로 이동 중인 사전 스폰 파티원은 건드리지 않는다
                // — UpdateStagingStairWalk가 그쪽 이동을 따로 관리한다.)
                if (targetState == DummyTargetState.WaitingForTarget)
                {
                    foreach (var member in activeParty.Members)
                    {
                        if (member == null || member.hp <= 0 || stagingUnits.Contains(member)) continue;
                        // 플레이어가 방금 수동으로 이동/공격을 지시했다면 건드리지 않는다(사용자 신고,
                        // 2026-07-24 "플레이어 지정 명령 잘 안 작동해") — 이 메서드가 매 프레임(웨이브
                        // 진행 중 내내) 돌면서 무조건 playerMoveTarget/isManualMoveCommand를 초기화해
                        // 버려서, 우클릭 명령이 사실상 같은 프레임 안에 지워지던 게 원인이었다. 명령이
                        // 끝나면(PlayerCommandFSMState가 직접 플래그를 정리) 다음 프레임부터 자동으로
                        // 이 메서드가 다시 챙긴다.
                        if (member.isManualMoveCommand && member.playerMoveTarget.HasValue) continue;
                        if (member.playerAttackTarget != null) continue;
                        member.playerMoveTarget = null; // 목표 없음 -> 자유 배회
                        member.isManualMoveCommand = false;
                    }
                    return; // 목표가 없으므로 로직 종료
                }
            }

            // ?�재 ?�겟을 ?�고 ?�는 경우, ?�겟의 ?�리???�치�??�반?�의 ?�치�?�??�레??갱신
            if (targetState == DummyTargetState.Carried && targetCarrier != null)
            {
                dummyTarget.Position = new Vector3Int(targetCarrier.position.x, targetCarrier.position.y, dummyTarget.Position.z);
            }

            // Carrier ?�망 체크 (?�랍 로직)
            if (targetState == DummyTargetState.Carried && (targetCarrier == null || targetCarrier.Health.hp <= 0))
            {
                DropDummyTarget();
            }

            // 목표가 이미 정해진 뒤에도 매 틱 다시 적용한다(사용자 요청, 2026-07-23) — 계단을 막
            // 넘어와 stagingUnits에서 빠진 파티원처럼, 목표가 "처음 발견된 순간"엔 아직 다른 층에
            // 있어서 건너뛰어졌던 유닛도 이걸로 자동으로 따라잡는다(같은 값 재적용이라 매 틱 불러도
            // 무해 — GOAP은 목표/계획이 실제로 바뀔 때만 재계획한다).
            UpdatePartyDestination();

            // 목표 획득 체크 (OnGround 일때) — 같은 층에 있고, 아직 0층에서 계단으로 걸어가는 중인
            // 사전 스폰 파티원은 제외한다. 도착 즉시 획득이 아니라 ObjectPickupDurationSeconds(10초)
            // 동안 같은 유닛이 목표 타일에 머물러야 획득된다(사용자 요청, 2026-07-23).
            if (targetState == DummyTargetState.OnGround)
            {
                Human unitOnTarget = null;
                foreach (var member in activeParty.Members)
                {
                    if (member == null || member.hp <= 0 || stagingUnits.Contains(member)) continue;
                    if (member.currentFloor != dummyTarget.Position.z) continue;

                    if (member.position.x == dummyTarget.Position.x && member.position.y == dummyTarget.Position.y)
                    {
                        unitOnTarget = member;
                        break;
                    }
                }

                if (unitOnTarget != null)
                {
                    if (pickupCandidateUnit != unitOnTarget)
                    {
                        pickupCandidateUnit = unitOnTarget;
                        pickupProgressSeconds = 0f;
                        joinPropagationTimer = 0f; // 7-2장: 새로 도착한 유닛부터 1초 합류 전파 재시작
                    }

                    // 7-2장: 1초 합류 정보 전파가 끝나야 실제 수집(상호작용) 타이머가 흐르기 시작한다.
                    if (joinPropagationTimer < PartyGoalJoinPropagationSeconds)
                    {
                        joinPropagationTimer += Time.deltaTime;
                    }
                    else
                    {
                        pickupProgressSeconds += Time.deltaTime;
                        if (pickupProgressSeconds >= ObjectPickupDurationSeconds)
                        {
                            PickupDummyTarget(unitOnTarget);
                            pickupCandidateUnit = null;
                            pickupProgressSeconds = 0f;
                        }
                    }
                }
                else
                {
                    pickupCandidateUnit = null;
                    pickupProgressSeconds = 0f;
                    joinPropagationTimer = 0f;
                }
            }
            // ?�탈 지??체크 (Carried ?�는 Secured ?�때)
            else if (targetState == DummyTargetState.Carried || targetState == DummyTargetState.Secured)
            {
                // [TODO: 향후에는 주변 유닛이 오브젝트를 든 유닛을 호위하는 편대 AI 시스템을 추가해야 함]
                // 현재는 단순히 목표를 획득해서 퇴각할 때부터 각자 탈출 지점으로 이동하며, 탈출 지점에 도착하는 개별 유닛부터 삭제(탈출) 처리합니다.

                for (int i = activeParty.Members.Count - 1; i >= 0; i--)
                {
                    var member = activeParty.Members[i];
                    if (member == null || member.hp <= 0 || stagingUnits.Contains(member)) continue;
                    if (member.currentFloor != targetFloor) continue;

                    if (member.position == exitAreaPos)
                    {
                        if (targetState == DummyTargetState.Carried && member == targetCarrier)
                        {
                            Debug.Log("[HumanWaveManager] 목표 반출 ?�공! ?��? ?�티?�들 ?�출 ?��?�?..");
                            targetState = DummyTargetState.Secured;
                            targetCarrier = null;
                        }
                        else
                        {
                            Debug.Log($"[HumanWaveManager] {member.name} ?�닛 개별 ?�출 ?�공.");
                        }

                        // 탈출 지점에 도착한 파티원은 사라지는(Despawn) 대신 0층으로 돌려보낸다
                        // (사용자 요청, 2026-07-23 "퇴각 로직 후에 0층으로 이동시키자").
                        RetreatMemberToFloor0(member);
                        activeParty.Members.RemoveAt(i);
                    }
                }

                // 모든 ?�티?�이 ?�출?�거???�망?�다�??�이�?종료
                if (activeParty.GetSurvivors().Count == 0)
                {
                    bool isSuccess = (targetState == DummyTargetState.Secured);
                    if (isSuccess)
                    {
                        Debug.Log("[HumanWaveManager] 목표 ?�보 ??모든 ?�티?�이 ?�탈(?�는 ?�망)?�여 ?�이브�? ?�공?�으�?종료?�니??");
                    }
                    else
                    {
                        Debug.Log("[HumanWaveManager] ?�각 �?모든 ?�티?�이 ?�망?�여 ?�이브에 ?�패?�습?�다.");
                    }

                    EndWave(isSuccess);
                    return;
                }
            }
        }

        private void PickupDummyTarget(Human unit)
        {
            Debug.Log($"[HumanWaveManager] {unit.name} 유닛이 더미 목표를 획득했습니다.");

            GameSession.Instance.CollectObject(dummyTarget.Position);

            targetState = DummyTargetState.Carried;
            targetCarrier = unit;

            UpdatePartyDestination();
        }

        private void DropDummyTarget()
        {
            Debug.Log("[HumanWaveManager] 목표 보유자가 사망하여 목표를 드랍합니다.");

            // dummyTarget.Position은 매 프레임 Carrier의 위치로 동기화되므로 최신 사망 위치 유지
            dummyTarget.IsCollected = false;

            // SpawnObject ?��??�서 objectGrid ?�록�??�각 ?�과(Visual) ?�성???�시??처리??
            GameSession.Instance.SpawnObject(dummyTarget, Color.magenta);

            targetState = DummyTargetState.OnGround;
            targetCarrier = null;
            pickupCandidateUnit = null;
            pickupProgressSeconds = 0f;
            joinPropagationTimer = 0f;

            UpdatePartyDestination();
        }

        private void UpdatePartyDestination()
        {
            Vector2Int dest = targetState == DummyTargetState.OnGround ?
                              new Vector2Int(dummyTarget.Position.x, dummyTarget.Position.y) :
                              exitAreaPos;
            int destFloor = targetState == DummyTargetState.OnGround ? dummyTarget.Position.z : targetSpawner.waveData.targetFloor;

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

            if (targetState == DummyTargetState.OnGround && dummyTarget != null)
            {
                GameSession.Instance.CollectObject(dummyTarget.Position);
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
            dummyTarget = null;
            targetCarrier = null;
            cooldownTimer = waveCooldown;
            currentState = WaveState.Idle;

            // 다음 웨이브 사이클을 위해 사전 스폰 관련 상태 초기화.
            preSpawnTriggered = false;
            preSpawnedParty = null;
            stagingUnits.Clear();
            pickupCandidateUnit = null;
            pickupProgressSeconds = 0f;
            joinPropagationTimer = 0f;
        }
    }
}


