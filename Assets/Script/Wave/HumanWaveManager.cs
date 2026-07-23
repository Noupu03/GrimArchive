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
        WaitingForTarget, // ? ì?ê°€ ?¤ë¸Œ?íŠ¸ë¥??ì„±???Œê¹Œì§€ ?€ê¸°í•˜???íƒœ ì¶”ê?
        Secured // ëª©í‘œë¬¼ì´ ?ˆì „?˜ê²Œ ë°˜ì¶œ ?„ë£Œ?˜ì—ˆ?¼ë‚˜, ?˜ë¨¸ì§€ ?Œí‹°?ë“¤???ˆì¶œ ì¤‘ì¸ ?íƒœ
    }

    /// <summary>
    /// ?¸ë¥˜ ?¨ì´ë¸Œì˜ ?¼ì´?„ì‚¬?´í´(ë°œìƒ, ëª©í‘œ ì¶”ì , ?´íƒˆ, ì¢…ë£Œ)???µì œ?˜ëŠ” ?œìŠ¤?œì…?ˆë‹¤.
    /// ê¸°íš???ì¹™???°ë¼ ë°œìƒì¡°ê±´, ëª©ì , ?Œë? êµ¬ì„±, ì¢…ë£Œ ì¡°ê±´??ëª¨ë“ˆ?”í•˜??ê´€ë¦¬í•©?ˆë‹¤.
    /// </summary>
    public class HumanWaveManager : NativeRoutine
    {
        public static HumanWaveManager Instance { get; private set; }

        public float waveCooldown => targetSpawner?.waveData?.waveCooldown ?? 10f;
        
        [Inject]
        public WaveSpawner targetSpawner; // VContainerë¥??µí•´ ?ë™ ì£¼ì…
        
        public WaveState currentState = WaveState.Idle;
        public float cooldownTimer = 0f;

        // ì¶”ì  ì¤‘ì¸ ?¨ì´ë¸??°ì´??
        public Party activeParty;
        public InteractableObject dummyTarget;
        public DummyTargetState targetState = DummyTargetState.WaitingForTarget;
        public Human targetCarrier = null;

        // ?´íƒˆ ì§€???ì—­ (?„ì‹œ: ?˜ì „ ?…êµ¬??StartRoom ê¸°ì? ?„ì¹˜)
        public Vector2Int exitAreaPos;

        public override async UniTask Initialize(System.Threading.CancellationToken cts)
        {
            await base.Initialize(cts);
            Instance = this;
            cooldownTimer = waveCooldown;
            currentState = WaveState.Idle;

            // Haare Framework ê¸°ì?: UniTask ê¸°ë°˜ Native Routine ë£¨í”„ ?¤í–‰
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
                Debug.LogError("[HumanWaveManager] Target Spawnerê°€ ?¤ì •?˜ì? ?Šì•˜?µë‹ˆ??");
                return;
            }

            Debug.Log("[HumanWaveManager] ?¸ë¥˜ ?¨ì´ë¸?ë°œìƒ! (ëª©í‘œë¬¼ì´ ?ì„±???Œê¹Œì§€ ?€ê¸°í•©?ˆë‹¤)");
            currentState = WaveState.Running;

            // 1. ?Œë? êµ¬ì„± ë°??¤í° (ê¸°ì¡´ WaveSpawner ?¬ì‚¬??
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
                Debug.LogWarning("[HumanWaveManager] ?¨ì´ë¸??Œí™˜ ?œë„?ˆìœ¼???Œí‹°ê°€ ?ì„±?˜ì? ?Šì•˜?µë‹ˆ??");
                EndWave(false);
                return;
            }

            // 2. ? ì?ê°€ O?¤ë¡œ ëª©í‘œë¬¼ì„ ?¤í°???Œê¹Œì§€ ?€ê¸??íƒœ ì§„ì…
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
                    Debug.Log("[HumanWaveManager] ?¨ì? ?Œí‹°?ì´ ?„ë©¸?ˆì?ë§?ëª©í‘œ???´ë? ë°˜ì¶œ?˜ì—ˆ?µë‹ˆ?? ?¨ì´ë¸??±ê³µ.");
                    EndWave(true);
                }
                else
                {
                    Debug.Log("[HumanWaveManager] ?Œí‹°ê°€ ?„ë©¸?ˆìŠµ?ˆë‹¤. ?¨ì´ë¸??¤íŒ¨.");
                    EndWave(false);
                }
                return;
            }

            // ? ì?ê°€ ëª©í‘œ ?¤ë¸Œ?íŠ¸(O??ë¥??ì„±???Œê¹Œì§€ ?€ê¸?
            if (targetState == DummyTargetState.WaitingForTarget)
            {
                foreach (var obj in GameSession.Instance.objectGrid.Values)
                {
                    // O?¤ë¡œ ?ì„±???¤ë¸Œ?íŠ¸ê°€ "Loot" ?œê·¸ë¥?ê°€ì§?(?˜ìœ„ ê³„ì¸µ ?¬í•¨ ì§€??
                    if (obj != null && obj.Tags != null && obj.Tags.Any(tag => tag.Contains("Loot")))
                    {
                        dummyTarget = obj;
                        targetState = DummyTargetState.OnGround;
                        UpdatePartyDestination();
                        Debug.Log($"[HumanWaveManager] ? ì?ê°€ ?ì„±??ëª©í‘œë¬?{obj.Id})??ë°œê²¬?ˆìŠµ?ˆë‹¤! ì¶”ì ???œì‘?©ë‹ˆ??");
                        break;
                    }
                }

                // ?¬ì „??ëª©í‘œê°€ ?†ë‹¤ë©?? ë‹›?¤ì? ê¸°ë³¸ AI???°ë¼ ?ìœ ë¡?²Œ ?Œì•„?¤ë‹ˆ?„ë¡ ?¡ë‹ˆ??
                if (targetState == DummyTargetState.WaitingForTarget)
                {
                    foreach (var member in activeParty.Members)
                    {
                        if (member != null && member.GetComponent<HealthComponent>().hp > 0)
                        {
                            member.playerMoveTarget = null; // ?´ë™ ëª©í‘œ ?´ì œ -> ?ìœ  ë°°íšŒ
                            member.isManualMoveCommand = false;
                        }
                    }
                    return; // ëª©í‘œê°€ ?†ìœ¼ë¯€ë¡?ë¡œì§ ì¢…ë£Œ
                }
            }

            // ?„ì¬ ?€ê²Ÿì„ ?¤ê³  ?ˆëŠ” ê²½ìš°, ?€ê²Ÿì˜ ?¼ë¦¬???„ì¹˜ë¥??´ë°˜?ì˜ ?„ì¹˜ë¡?ë§??„ë ˆ??ê°±ì‹ 
            if (targetState == DummyTargetState.Carried && targetCarrier != null)
            {
                dummyTarget.Position = new Vector3Int(targetCarrier.position.x, targetCarrier.position.y, dummyTarget.Position.z);
            }

            // Carrier ?¬ë§ ì²´í¬ (?œë ë¡œì§)
            if (targetState == DummyTargetState.Carried && (targetCarrier == null || targetCarrier.GetComponent<HealthComponent>().hp <= 0))
            {
                DropDummyTarget();
            }

            // ëª©í‘œ ?ë“ ì²´í¬ (OnGround ?¼ë•Œ)
            if (targetState == DummyTargetState.OnGround)
            {
                foreach (var member in activeParty.Members)
                {
                    if (member == null || member.GetComponent<HealthComponent>().hp <= 0) continue;
                    
                    if (member.position.x == dummyTarget.Position.x && member.position.y == dummyTarget.Position.y)
                    {
                        PickupDummyTarget(member);
                        break;
                    }
                }
            }
            // ?´íƒˆ ì§€??ì²´í¬ (Carried ?ëŠ” Secured ?¼ë•Œ)
            else if (targetState == DummyTargetState.Carried || targetState == DummyTargetState.Secured)
            {
                // [TODO: ?¥í›„?ëŠ” ì£¼ë? ? ë‹›???¤ë¸Œ?íŠ¸ë¥???? ë‹›???¸ìœ„?˜ëŠ” ?¸ë? AI ?œìŠ¤?œì„ ì¶”ê??´ì•¼ ??
                // ?„ì¬???¨ìˆœ??ëª©í‘œë¥??ë“?´ì„œ ?´ê°???Œë???ê°ì ?ˆì¶œ ì§€?ìœ¼ë¡??´ë™?˜ë©°, ?ˆì¶œ ì§€?ì— ?„ì°©?˜ëŠ” ê°œë³„ ? ë‹›ë¶€???? œ(?ˆì¶œ) ì²˜ë¦¬?©ë‹ˆ??
                
                for (int i = activeParty.Members.Count - 1; i >= 0; i--)
                {
                    var member = activeParty.Members[i];
                    if (member == null || member.GetComponent<HealthComponent>().hp <= 0) continue;
                    
                    if (member.position == exitAreaPos)
                    {
                        if (targetState == DummyTargetState.Carried && member == targetCarrier)
                        {
                            Debug.Log("[HumanWaveManager] ëª©í‘œ ë°˜ì¶œ ?±ê³µ! ?¨ì? ?Œí‹°?ë“¤ ?ˆì¶œ ?€ê¸?ì¤?..");
                            targetState = DummyTargetState.Secured;
                            targetCarrier = null;
                        }
                        else
                        {
                            Debug.Log($"[HumanWaveManager] {member.name} ? ë‹› ê°œë³„ ?ˆì¶œ ?±ê³µ.");
                        }
                        
                        GameSession.Instance.DespawnUnit(member);
                        activeParty.Members.RemoveAt(i);
                    }
                }

                // ëª¨ë“  ?Œí‹°?ì´ ?ˆì¶œ?ˆê±°???¬ë§?ˆë‹¤ë©??¨ì´ë¸?ì¢…ë£Œ
                if (activeParty.GetSurvivors().Count == 0)
                {
                    bool isSuccess = (targetState == DummyTargetState.Secured);
                    if (isSuccess)
                    {
                        Debug.Log("[HumanWaveManager] ëª©í‘œ ?•ë³´ ??ëª¨ë“  ?Œí‹°?ì´ ?´íƒˆ(?ëŠ” ?¬ë§)?˜ì—¬ ?¨ì´ë¸Œë? ?±ê³µ?ìœ¼ë¡?ì¢…ë£Œ?©ë‹ˆ??");
                    }
                    else
                    {
                        Debug.Log("[HumanWaveManager] ?´ê° ì¤?ëª¨ë“  ?Œí‹°?ì´ ?¬ë§?˜ì—¬ ?¨ì´ë¸Œì— ?¤íŒ¨?ˆìŠµ?ˆë‹¤.");
                    }
                    
                    EndWave(isSuccess);
                    return;
                }
            }
        }

        private void PickupDummyTarget(Human unit)
        {
            Debug.Log($"[HumanWaveManager] {unit.name} ? ë‹›???”ë? ëª©í‘œë¥??ë“?ˆìŠµ?ˆë‹¤.");
            
            GameSession.Instance.CollectObject(dummyTarget.Position);

            targetState = DummyTargetState.Carried;
            targetCarrier = unit;

            UpdatePartyDestination();
        }

        private void DropDummyTarget()
        {
            Debug.Log("[HumanWaveManager] ëª©í‘œ ë³´ìœ ?ê? ?¬ë§?˜ì—¬ ëª©í‘œë¥??œë?©ë‹ˆ??");
            
            // dummyTarget.Position?€ ë§??„ë ˆ??Carrier???„ì¹˜ë¡??™ê¸°?”ë˜ë¯€ë¡?ìµœì‹  ?¬ë§ ?„ì¹˜ ? ì?
            dummyTarget.IsCollected = false;

            // SpawnObject ?´ë??ì„œ objectGrid ?±ë¡ê³??œê° ?¨ê³¼(Visual) ?ì„±???™ì‹œ??ì²˜ë¦¬??
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
                if (member == null || member.GetComponent<HealthComponent>().hp <= 0) continue;
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

            Debug.Log($"[HumanWaveManager] ?¨ì´ë¸??•ë¦¬ ?„ë£Œ. ?¤ìŒ ?¨ì´ë¸Œê¹Œì§€ {waveCooldown}ì´??€ê¸?");
            
            activeParty = null;
            dummyTarget = null;
            targetCarrier = null;
            cooldownTimer = waveCooldown;
            currentState = WaveState.Idle;
        }
    }
}


