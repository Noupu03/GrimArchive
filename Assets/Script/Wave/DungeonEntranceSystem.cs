using System;
using System.Collections.Generic;
using UnityEngine;

namespace GrimArchive.Wave
{
    // 던전 입구 구조 — 인간 파티가 0층 숨은 스폰 청크에서 등장해 로비로 걸어 나와, 집결처럼 유닛마다 받은 진형 자리(1선 근접 → 2선 리더 → 3선 원거리)에 선 뒤 고정 시간 대기한다. 대기가 끝나면 전원에게 계단 접근 이동 명령(우선순위 200이라 Tactical/Combat이 못 가로챔)을 내리고 손을 뗀다 — 계단 통과는 개인 FSM+BT(MoveToStairs/CrossStairs)가 전담하고 isInDungeonEntranceSequence도 CrossStairs가 개인별로 푼다.
    // 2026-10-21: 예전엔 진형 전체를 슬롯 좌표로 한 칸씩 순간이동시켜(벽·바닥 확인 없음) 인원이 많으면 한 줄이 로비 폭을 넘어 벽 속에 놓이고 연출처럼 보였다. 지금은 ① 자리 배정이 벽·로비 밖을 피해 가까운 빈 바닥 칸으로 밀어 주고(PartyFormationMath.AssignEntranceSlots, 집결 자리와 같은 점진 확장) ② 유닛이 이동 명령으로 직접 걸어가며 ③ 전원이 자리에 서면(또는 시간 상한이 지나면) 대기를 시작한다.
    public class DungeonEntranceSystem
    {
        private enum Phase { Idle, Gathering, Waiting, Done }

        // "텍스트 연출" 대기 시간(자리표시자) — HumanWaveManager.PreSpawnLeadSeconds(6초)를 포함하며,
        // HumanWaveManager가 스폰 트리거 시점 계산에도 참조하므로 public.
        public const float WaitSeconds = 10f;

        // "진입 준비" 문구가 뜨는 기준(웨이브 시작까지 남은 시간) — HumanWaveManager.PreSpawnLeadSeconds와
        // 같은 값(6초)이지만, 이 클래스는 HumanWaveManager를 참조하지 않으므로 별도 상수로 둔다.
        private const float PrepareNoticeLeadSeconds = 6f;
        private const string EntranceNoticeKey = "DungeonEntranceSequence";

        // 집결 시간 상한 — 가장 먼 유닛이 걸어갈 시간의 2배 + 여유. 막혀서 못 서는 유닛이 있어도 파티 전체가 영원히 기다리지 않는다(집결의 미집결자 상한과 같은 발상).
        private const float MinGatherTimeoutSeconds = 8f;
        private const float GatherTimeoutMargin = 5f;

        // 진형 한 명의 자리 — Rank는 이동 방향 기준 앞(0)부터 뒤로 갈수록 커지는 선(1선/2선/3선), Lane은 그 선 안에서 rowY 기준 좌우(Y축) 오프셋(이상 자리). Slot은 벽·바닥을 반영해 실제로 배정된 칸(null이면 제자리).
        private sealed class Member
        {
            public Human Unit;
            public int Rank;
            public int Lane;
            public Vector2Int? Slot;
            public bool Settled;
        }

        private Phase _phase = Phase.Idle;
        private readonly List<Member> _members = new List<Member>();
        private float _waitTimer;
        private float _gatherTimer;
        private float _gatherTimeout;
        private int _targetX;
        private int _rowY;
        private int _pendingStairApproachX;
        private bool _prepareNoticeShown;

        public bool IsActive => _phase != Phase.Idle && _phase != Phase.Done;

        // PreSpawnWaveUnits가 숨은 스폰 청크에 인간들을 스폰한 직후 호출한다. roomEntryX는 진형 앞줄(0랭크)이 서는 x, stairApproachX는 대기 후 내릴 계단 접근 명령의 목적지(같은 행 rowY 기준)다. 스폰 직후 '무더기' 상태의 유닛들은 각자 받은 자리로 걸어간다.
        public void Begin(GameSession session, Party party, int rowY, int roomEntryX, int stairApproachX)
        {
            _members.Clear();
            BuildFormation(session, party, _members);
            if (_members.Count == 0) { _phase = Phase.Idle; return; }

            _rowY = rowY;
            _targetX = roomEntryX;
            _phase = Phase.Gathering;
            _gatherTimer = 0f;
            _prepareNoticeShown = false;

            AssignSlots(session);
            _gatherTimeout = ComputeGatherTimeout();
            foreach (var m in _members)
            {
                m.Unit.isInDungeonEntranceSequence = true;
                if (!m.Slot.HasValue || m.Unit.position == m.Slot.Value) { m.Settled = true; continue; }
                m.Unit.SetMoveCommand(m.Slot.Value, false, false);
            }

            NoticeCenter.Instance?.PushFixed(EntranceNoticeKey, "인간 파티가 던전 입구로 이동 중입니다...", NoticeCenter.InfoColor);

            // Waiting이 끝나는 순간 계단 접근 이동 명령에 쓸 목적지를 미리 저장해둔다.
            _pendingStairApproachX = stairApproachX;
        }

        // 매 프레임 호출. 대기가 끝나 전원에게 계단 접근 명령을 내린 순간 onArrivedAtStairs를 1회 호출한다(HumanWaveManager가 activeParty/exitAreaPos/stagingUnits/pendingStairTargetFloor를 세팅 — 실제 계단 통과는 이후 개인 FSM+BT가 이어받아 미리 세팅해도 안전). cooldownTimerRemaining은 '진입 준비' 문구 표시 기준이다.
        public void Update(GameSession session, float deltaTime, float cooldownTimerRemaining, Action<List<Human>> onArrivedAtStairs)
        {
            if (!IsActive || session == null) return;

            if (!_prepareNoticeShown && cooldownTimerRemaining <= PrepareNoticeLeadSeconds)
            {
                _prepareNoticeShown = true;
                NoticeCenter.Instance?.PushFixed(EntranceNoticeKey, "인간 파티가 진입을 준비하고 있습니다...", NoticeCenter.WarningColor);
            }

            PruneDead();
            if (_members.Count == 0) { Finish(); return; }

            switch (_phase)
            {
                case Phase.Gathering:
                    _gatherTimer += deltaTime;
                    RefreshSettled();
                    if (AllSettled() || _gatherTimer >= _gatherTimeout)
                    {
                        _phase = Phase.Waiting;
                        _waitTimer = 0f;
                    }
                    break;

                case Phase.Waiting:
                    FaceForward(session);
                    _waitTimer += deltaTime;
                    if (_waitTimer >= WaitSeconds)
                    {
                        NoticeCenter.Instance?.PushFixed(EntranceNoticeKey, "인간 파티가 던전으로 진입합니다!", NoticeCenter.WarningColor);

                        // 여기서부터는 대형을 붙들지 않는다 — 전원에게 같은 계단 접근 좌표로 이동 명령을 내리고 즉시 시퀀스를 끝낸다(isInDungeonEntranceSequence는 CrossStairs가 각자 끈다). 아직 자리로 걷던 유닛의 명령도 이 명령으로 대체된다.
                        var arrivedMembers = new List<Human>(_members.Count);
                        foreach (var m in _members)
                        {
                            Human h = m.Unit;
                            if (h == null || h.hp <= 0) continue;
                            h.SetMoveCommand(new Vector2Int(_pendingStairApproachX, _rowY), false, false);
                            arrivedMembers.Add(h);
                        }
                        onArrivedAtStairs?.Invoke(arrivedMembers);
                        Finish();
                    }
                    break;
            }
        }

        // isInDungeonEntranceSequence는 여기서 끄지 않는다 — NavigationFSMState.CrossStairs가 각 유닛이 층을 건너는 순간 개별로 끈다. 이 메서드는 이 클래스 자신의 진행 상태만 정리한다.
        private void Finish()
        {
            NoticeCenter.Instance?.ClearFixed(EntranceNoticeKey);
            _members.Clear();
            _phase = Phase.Done;
        }

        private void PruneDead()
        {
            _members.RemoveAll(m => m.Unit == null || m.Unit.hp <= 0);
        }

        // ── 자리 배정·집결 ──

        // 이상 자리(랭크·레인)를 벽·로비 밖을 피해 가까운 빈 바닥 칸으로 맞춘다 — 인원이 많아 한 줄이 로비 폭을 넘어도 넘친 인원이 옆·뒤 칸에 서서 벽에 닿지 않는다.
        private void AssignSlots(GameSession session)
        {
            var entries = new List<(int rank, int lane)>(_members.Count);
            foreach (var m in _members) entries.Add((m.Rank, m.Lane));

            var cmap = session.cmap;
            Vector2Int?[] slots = PartyFormationMath.AssignEntranceSlots(
                entries, new Vector2Int(_targetX, _rowY), t => cmap == null || cmap.IsStaticTileWalkable(0, t), _targetX);
            for (int i = 0; i < _members.Count; i++) _members[i].Slot = slots[i];
        }

        private float ComputeGatherTimeout()
        {
            float longest = 0f;
            foreach (var m in _members)
            {
                if (!m.Slot.HasValue) continue;
                float tiles = Mathf.Max(Mathf.Abs(m.Unit.position.x - m.Slot.Value.x), Mathf.Abs(m.Unit.position.y - m.Slot.Value.y));
                longest = Mathf.Max(longest, tiles / Mathf.Max(0.1f, m.Unit.BaseStat.walkSpeed));
            }
            return Mathf.Max(MinGatherTimeoutSeconds, longest * 2f + GatherTimeoutMargin);
        }

        // 자리에 섰는가 — 자리 곁(집결과 같은 널널한 도착 반경)이거나, 이동 명령이 끝났으면(자리에 닿았거나 막혀 명령이 포기된 경우) 선 자리에서 인정한다.
        private void RefreshSettled()
        {
            int radius = ArrivalRadius();
            foreach (var m in _members)
            {
                if (m.Settled) continue;
                bool atSlot = m.Slot.HasValue && PartyFormationMath.IsAtSlot(m.Unit.position, m.Slot.Value, radius);
                m.Settled = atSlot || !m.Unit.playerMoveTarget.HasValue;
            }
        }

        private bool AllSettled()
        {
            foreach (var m in _members)
                if (!m.Settled) return false;
            return true;
        }

        private static int ArrivalRadius()
            => Party.RallyLenient
                ? Mathf.Max(PartyFormationMath.ArrivalRadius, AIConfigLoader.Behavior?.rallyArrivalRadius ?? 2)
                : PartyFormationMath.ArrivalRadius;

        // 대기 중에는 자리에 선 유닛이 진행 방향(오른쪽)을 보게 한다 — 걸어오던 방향 그대로 서 있지 않게. 자리로 걷는 중인 유닛은 건드리지 않는다.
        private void FaceForward(GameSession session)
        {
            foreach (var m in _members)
            {
                Human h = m.Unit;
                if (h == null || h.hp <= 0 || h.playerMoveTarget.HasValue || h.currentDir == Dir.RIGHT) continue;
                h.currentDir = Dir.RIGHT;
                session.unitGenerate?.SyncVisual(h);
            }
        }

        // 파티 진형(문서 명시): 탱커/근접딜러를 근접 랭크(0)로 합쳐 맨 앞에, 리더는 항상 다음 랭크(1)에
        // 단독으로, 원거리는 마지막 랭크(2)에 둔다(근접/원거리 판정은 UnitGenerate.GetEngageDistance 재사용,
        // 새 분류 데이터 없음). 같은 랭크에 유닛이 여럿이면 AddRank/LaneOffset이 rowY 기준 좌우로 늘어서게 한다.
        private static void BuildFormation(GameSession session, Party party, List<Member> outFormation)
        {
            outFormation.Clear();
            if (party == null) return;

            party.AssignLeaderIfNeeded();
            Human leader = party.Leader;

            var melee = new List<Human>();
            var ranged = new List<Human>();
            foreach (var m in party.Members)
            {
                if (m == null || m.hp <= 0 || m == leader) continue;
                if (IsMelee(session, m)) melee.Add(m);
                else ranged.Add(m);
            }

            AddRank(outFormation, melee, 0);
            if (leader != null && leader.hp > 0) AddRank(outFormation, new List<Human> { leader }, 1);
            AddRank(outFormation, ranged, 2);
        }

        // rank(선) 하나를 채운다 — 단순 i - n/2 공식은 짝수 인원에서 대형 중심이 rowY보다 처지므로,
        // 홀수는 중앙 포함 대칭(-1,0,1), 짝수는 중앙을 비우고 좌우 동수 대칭(-2,-1,+1,+2)으로 계산한다.
        private static void AddRank(List<Member> outFormation, List<Human> members, int rank)
        {
            int n = members.Count;
            for (int i = 0; i < n; i++)
            {
                int lane = PartyFormationMath.LaneOffset(i, n);
                outFormation.Add(new Member { Unit = members[i], Rank = rank, Lane = lane });
            }
        }

        // 근접/원거리 판정 — 방 문 앞 진형(PartyAdvanceSystem)도 이 메서드를 쓴다.
        public static bool IsMelee(GameSession session, Human h)
        {
            int engageDistance = session?.unitGenerate != null && h.unitType != null
                ? session.unitGenerate.GetEngageDistance(h.unitType.typeName, 2)
                : 2;
            return PartyFormationMath.IsMeleeEngage(engageDistance);
        }
    }
}
