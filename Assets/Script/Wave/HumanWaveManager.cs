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
        WaitingForTarget, // 유저가 오브젝트를 생성할 때까지 대기하는 상태 추가
        Secured // 목표물이 안전하게 반출 완료되었으나, 나머지 파티원들이 탈출 중인 상태
    }

    /// <summary>
    /// 인류 웨이브의 라이프사이클(발생, 목표 추적, 이탈, 종료)을 통제하는 시스템입니다.
    /// 기획의 원칙에 따라 발생조건, 목적, 소대 구성, 종료 조건을 모듈화하여 관리합니다.
    /// </summary>
    public class HumanWaveManager : NativeRoutine
    {
        public static HumanWaveManager Instance { get; private set; }

        public float waveCooldown => targetSpawner?.waveData?.waveCooldown ?? 10f;
        
        [Inject]
        public WaveSpawner targetSpawner; // VContainer를 통해 자동 주입
        
        public WaveState currentState = WaveState.Idle;
        public float cooldownTimer = 0f;

        // 추적 중인 웨이브 데이터
        public Party activeParty;
        public InteractableObject dummyTarget;
        public DummyTargetState targetState = DummyTargetState.WaitingForTarget;
        public Human targetCarrier = null;

        // 이탈 지점 영역 (임시: 던전 입구인 StartRoom 기준 위치)
        public Vector2Int exitAreaPos;

        public override async UniTask Initialize(System.Threading.CancellationToken cts)
        {
            await base.Initialize(cts);
            Instance = this;
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
                    if (cooldownTimer <= 0f)
                    {
                        StartWave();
                    }
                }
                else if (currentState == WaveState.Running)
                {
                    MonitorWave();
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: cts);
            }
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

            // 1. 소대 구성 및 스폰 (기존 WaveSpawner 재사용)
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

            // 2. 유저가 O키로 목표물을 스폰할 때까지 대기 상태 진입
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
                    Debug.Log("[HumanWaveManager] 남은 파티원이 전멸했지만 목표는 이미 반출되었습니다. 웨이브 성공.");
                    EndWave(true);
                }
                else
                {
                    Debug.Log("[HumanWaveManager] 파티가 전멸했습니다. 웨이브 실패.");
                    EndWave(false);
                }
                return;
            }

            // 유저가 목표 오브젝트(O키)를 생성할 때까지 대기
            if (targetState == DummyTargetState.WaitingForTarget)
            {
                foreach (var obj in GameSession.Instance.objectGrid.Values)
                {
                    // O키로 생성된 오브젝트가 "Loot" 태그를 가짐 (하위 계층 포함 지원)
                    if (obj != null && obj.Tags != null && obj.Tags.Any(tag => tag.Contains("Loot")))
                    {
                        dummyTarget = obj;
                        targetState = DummyTargetState.OnGround;
                        UpdatePartyDestination();
                        Debug.Log($"[HumanWaveManager] 유저가 생성한 목표물({obj.Id})을 발견했습니다! 추적을 시작합니다.");
                        break;
                    }
                }

                // 여전히 목표가 없다면 유닛들은 기본 AI에 따라 자유롭게 돌아다니도록 둡니다.
                if (targetState == DummyTargetState.WaitingForTarget)
                {
                    foreach (var member in activeParty.Members)
                    {
                        if (member != null && member.hp > 0)
                        {
                            member.playerMoveTarget = null; // 이동 목표 해제 -> 자유 배회
                            member.isManualMoveCommand = false;
                        }
                    }
                    return; // 목표가 없으므로 로직 종료
                }
            }

            // 현재 타겟을 들고 있는 경우, 타겟의 논리적 위치를 운반자의 위치로 매 프레임 갱신
            if (targetState == DummyTargetState.Carried && targetCarrier != null)
            {
                dummyTarget.Position = new Vector3Int(targetCarrier.position.x, targetCarrier.position.y, dummyTarget.Position.z);
            }

            // Carrier 사망 체크 (드랍 로직)
            if (targetState == DummyTargetState.Carried && (targetCarrier == null || targetCarrier.hp <= 0))
            {
                DropDummyTarget();
            }

            // 목표 획득 체크 (OnGround 일때)
            if (targetState == DummyTargetState.OnGround)
            {
                foreach (var member in activeParty.Members)
                {
                    if (member == null || member.hp <= 0) continue;
                    
                    if (member.position.x == dummyTarget.Position.x && member.position.y == dummyTarget.Position.y)
                    {
                        PickupDummyTarget(member);
                        break;
                    }
                }
            }
            // 이탈 지점 체크 (Carried 또는 Secured 일때)
            else if (targetState == DummyTargetState.Carried || targetState == DummyTargetState.Secured)
            {
                // [TODO: 향후에는 주변 유닛이 오브젝트를 든 유닛을 호위하는 편대 AI 시스템을 추가해야 함]
                // 현재는 단순히 목표를 획득해서 퇴각할 때부터 각자 탈출 지점으로 이동하며, 탈출 지점에 도착하는 개별 유닛부터 삭제(탈출) 처리합니다.
                
                for (int i = activeParty.Members.Count - 1; i >= 0; i--)
                {
                    var member = activeParty.Members[i];
                    if (member == null || member.hp <= 0) continue;
                    
                    if (member.position == exitAreaPos)
                    {
                        if (targetState == DummyTargetState.Carried && member == targetCarrier)
                        {
                            Debug.Log("[HumanWaveManager] 목표 반출 성공! 남은 파티원들 탈출 대기 중...");
                            targetState = DummyTargetState.Secured;
                            targetCarrier = null;
                        }
                        else
                        {
                            Debug.Log($"[HumanWaveManager] {member.name} 유닛 개별 탈출 성공.");
                        }
                        
                        GameSession.Instance.DespawnUnit(member);
                        activeParty.Members.RemoveAt(i);
                    }
                }

                // 모든 파티원이 탈출했거나 사망했다면 웨이브 종료
                if (activeParty.GetSurvivors().Count == 0)
                {
                    bool isSuccess = (targetState == DummyTargetState.Secured);
                    if (isSuccess)
                    {
                        Debug.Log("[HumanWaveManager] 목표 확보 후 모든 파티원이 이탈(또는 사망)하여 웨이브를 성공적으로 종료합니다.");
                    }
                    else
                    {
                        Debug.Log("[HumanWaveManager] 퇴각 중 모든 파티원이 사망하여 웨이브에 실패했습니다.");
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

            // SpawnObject 내부에서 objectGrid 등록과 시각 효과(Visual) 생성을 동시에 처리함
            GameSession.Instance.SpawnObject(dummyTarget, Color.magenta);

            targetState = DummyTargetState.OnGround;
            targetCarrier = null;

            UpdatePartyDestination();
        }

        private void UpdatePartyDestination()
        {
            Vector2Int dest = targetState == DummyTargetState.OnGround ? 
                              new Vector2Int(dummyTarget.Position.x, dummyTarget.Position.y) : 
                              exitAreaPos;

            foreach (var member in activeParty.Members)
            {
                if (member == null || member.hp <= 0) continue;
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
                foreach (var survivor in survivors)
                {
                    if (survivor != null)
                    {
                        GameSession.Instance.DespawnUnit(survivor);
                    }
                }
            }

            Debug.Log($"[HumanWaveManager] 웨이브 정리 완료. 다음 웨이브까지 {waveCooldown}초 대기.");
            
            activeParty = null;
            dummyTarget = null;
            targetCarrier = null;
            cooldownTimer = waveCooldown;
            currentState = WaveState.Idle;
        }
    }
}
