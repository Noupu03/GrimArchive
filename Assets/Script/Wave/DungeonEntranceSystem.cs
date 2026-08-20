using System;
using System.Collections.Generic;
using UnityEngine;

namespace GrimArchive.Wave
{
    // 던전 입구 구조(2026-08-20, "던전 입구 구조 프로그래머 지시서") — 인간 파티가 0층 숨은 스폰
    // 청크에서 등장해 1x3 던전 입구로 걸어들어오고, 고정 시간(약 10초) 대기한 뒤, 파티 진형(1선
    // 근접 → 2선 리더 → 3선 원거리, 각 선마다 여러 유닛이 옆으로 늘어선 대형)을 유지한 채 계단까지
    // 걸어가는 시퀀스를 전담한다. HumanWaveManager가 소유하고 매 프레임 Update를 호출한다 — 문/몬스터
    // 소집 등 기존 웨이브 타이밍 체인(HumanWaveManager.StartWave, cooldownTimer<=0 시점)은 전혀
    // 건드리지 않고 그대로 두며, 이 시퀀스가 끝나 계단에 도달해야만(그 뒤에 실제 GOAP 계단 통과)
    // 인간 파티가 진짜로 1층에 들어간다 — 사용자 확인: "웨이브 시작"(문 닫힘/몬스터 소집)과 "인간
    // 파티의 실제 1층 진입"이 이 구조 이후로는 서로 다른 시점이 된다(파티가 그만큼 늦게 넘어옴).
    //
    // 이동은 정상 GOAP/A*를 타지 않고 이 클래스가 직접 정수 격자 위치를 조작한다 — 0층 던전 입구는
    // 전투/오브젝트/장애물이 전혀 없는 직선 복도(문서 명시 "던전 입구 기능 범위")라 경로탐색이
    // 필요 없고, 그래야 "파티 전원 동일 속도 + 고정 간격 대형 유지"를 정확히 보장할 수 있다.
    public class DungeonEntranceSystem
    {
        private enum Phase { Idle, WalkingIn, Waiting, WalkingToStairs, Done }

        // "약 10초 텍스트 연출"(문서 명시, "추후 플레이 테스트 또는 연출 추가에 따라 조정 가능") — 대기 시간.
        // 2026-08-20 사용자 확인: 이 10초는 HumanWaveManager.PreSpawnLeadSeconds(6초)를 포함하는 10초다
        // — 대기 시작~막판 6초 전까지는 조용히 대기, 남은 cooldownTimer가 6초 이하가 되는 순간부터
        // "진입 준비" 문구가 뜬다. HumanWaveManager가 스폰 트리거 시점을 계산할 때도 이 상수를
        // 참조하므로 public.
        public const float WaitSeconds = 10f;

        // "진입 준비" 문구가 뜨는 기준(웨이브 시작까지 남은 시간). HumanWaveManager.PreSpawnLeadSeconds
        // 와 같은 값(6초)이지만, 이 클래스는 HumanWaveManager를 참조하지 않으므로 별도 상수로 둔다.
        private const float PrepareNoticeLeadSeconds = 6f;
        private const string EntranceNoticeKey = "DungeonEntranceSequence";

        // 파티 진형 슬롯 — 대형 내 유닛 한 명의 상대 위치. Rank는 이동 방향 기준 앞(0)부터 뒤로 갈수록
        // 커지는 선(1선/2선/3선), Lane은 그 선 안에서 rowY 기준 좌우(여기서는 상하, Y축) 오프셋이다.
        private struct FormationSlot
        {
            public Human Unit;
            public int Rank;
            public int Lane;
        }

        private Phase _phase = Phase.Idle;
        private readonly List<FormationSlot> _formation = new List<FormationSlot>();
        private float _waitTimer;
        private float _stepTimer;
        private float _stepIntervalSeconds;
        private int _frontX;
        private int _targetX;
        private int _rowY;
        private int _floor;
        private int _pendingStairApproachX;
        private bool _prepareNoticeShown;

        // 선(랭크) 간 간격(이동 방향 축, 타일) — 이 값만큼씩 뒤로 갈수록 밀린다.
        private const int RankSpacingX = 2;

        public bool IsActive => _phase != Phase.Idle && _phase != Phase.Done;

        // PreSpawnWaveUnits가 숨은 스폰 청크에 인간들을 스폰한 직후 호출한다. roomEntryX/stairApproachX는
        // 같은 행(rowY) 위의 목표 x좌표 — 문서 흐름("좌측 1x1 청크 등장 → 1x3으로 이동 → 대기 → 우측
        // 계단으로 이동")의 두 목적지. 2026-08-20 사용자 요청("생성 시점에는 무더기로 생성되었다가,
        // 포메이션을 맞춰서 대기하고, 그 포메이션대로 입장") — 스폰 직후엔 HumanWaveManager가 좁은
        // 반경 안에 흩어 놓은 "무더기" 상태 그대로 두고, 이후 매 스텝 각 유닛이 자기 대형 슬롯(랭크/
        // 레인)으로 이동하게 하면 WalkingIn이 진행되는 동안 자연스럽게 무더기→대형으로 정렬된다.
        public void Begin(GameSession session, Party party, int floor, int rowY, int roomEntryX, int stairApproachX)
        {
            _formation.Clear();
            BuildFormation(session, party, _formation);
            if (_formation.Count == 0) { _phase = Phase.Idle; return; }

            _floor = floor;
            _rowY = rowY;
            _frontX = FrontmostSpawnX(_formation);
            _targetX = roomEntryX;
            _stepIntervalSeconds = 1f / Mathf.Max(0.01f, MinWalkSpeed(_formation));
            _stepTimer = 0f;
            _phase = Phase.WalkingIn;
            _prepareNoticeShown = false;

            foreach (var slot in _formation) slot.Unit.isInDungeonEntranceSequence = true;

            NoticeCenter.Instance?.PushPersistent(EntranceNoticeKey, "인간 파티가 던전 입구로 이동 중입니다...", NoticeCenter.InfoColor);

            // 이어지는 WalkingToStairs 단계에서 쓸 목적지를 미리 저장해둔다.
            _pendingStairApproachX = stairApproachX;
        }

        // 매 프레임 호출. 계단에 도달해 실제로 GOAP 계단 통과를 넘겨줄 준비가 되면 onArrivedAtStairs를
        // 1회 호출한다(호출부인 HumanWaveManager가 stagingUnits/pendingStairTargetFloor를 세팅).
        // cooldownTimerRemaining은 HumanWaveManager.cooldownTimer를 그대로 받는다 — "진입 준비" 문구가
        // 내부 단계(WalkingIn/Waiting) 전환 시점이 아니라 실제 웨이브 시작까지 남은 시간 기준으로
        // 뜨도록 하기 위함(2026-08-20 사용자 확인: "저 6초 포함해서 10초").
        public void Update(GameSession session, float deltaTime, float cooldownTimerRemaining, Action<List<Human>> onArrivedAtStairs)
        {
            if (!IsActive || session == null) return;

            if (!_prepareNoticeShown && cooldownTimerRemaining <= PrepareNoticeLeadSeconds)
            {
                _prepareNoticeShown = true;
                NoticeCenter.Instance?.PushPersistent(EntranceNoticeKey, "인간 파티가 진입을 준비하고 있습니다...", NoticeCenter.WarningColor);
            }

            PruneDead();
            if (_formation.Count == 0) { Finish(); return; }

            switch (_phase)
            {
                case Phase.WalkingIn:
                    if (StepFormation(session, deltaTime))
                    {
                        _phase = Phase.Waiting;
                        _waitTimer = 0f;
                    }
                    break;

                case Phase.Waiting:
                    _waitTimer += deltaTime;
                    if (_waitTimer >= WaitSeconds)
                    {
                        _targetX = _pendingStairApproachX;
                        _phase = Phase.WalkingToStairs;
                        NoticeCenter.Instance?.PushPersistent(EntranceNoticeKey, "인간 파티가 던전으로 진입합니다!", NoticeCenter.WarningColor);
                    }
                    break;

                case Phase.WalkingToStairs:
                    if (StepFormation(session, deltaTime))
                    {
                        var arrivedMembers = new List<Human>(_formation.Count);
                        foreach (var slot in _formation) arrivedMembers.Add(slot.Unit);
                        onArrivedAtStairs?.Invoke(arrivedMembers);
                        Finish();
                    }
                    break;
            }
        }

        private void Finish()
        {
            foreach (var slot in _formation)
            {
                if (slot.Unit == null) continue;
                slot.Unit.isInDungeonEntranceSequence = false;
            }
            NoticeCenter.Instance?.Remove(EntranceNoticeKey);
            _formation.Clear();
            _phase = Phase.Done;
        }

        private void PruneDead()
        {
            _formation.RemoveAll(s => s.Unit == null || s.Unit.hp <= 0);
        }

        // 목표 x에 도달했으면 true. 아직이면 "가장 느린 파티원 속도" 간격마다 대형 전체(모든 랭크/
        // 레인)를 1칸씩 같은 방향으로 통째로 전진시킨다 — 대형의 상대 형태를 유지한 채 블록으로
        // 이동한다(예전의 1열 종대와 달리 랭크별로 옆으로 늘어선 형태를 그대로 유지).
        private bool StepFormation(GameSession session, float deltaTime)
        {
            if (_frontX == _targetX) return true;

            _stepTimer += deltaTime;
            int dir = _targetX > _frontX ? 1 : -1;

            while (_stepTimer >= _stepIntervalSeconds && _frontX != _targetX)
            {
                _stepTimer -= _stepIntervalSeconds;
                _frontX += dir;
                ApplyFormationPositions(session, dir);
            }

            return _frontX == _targetX;
        }

        private void ApplyFormationPositions(GameSession session, int dir)
        {
            Dir facing = dir >= 0 ? Dir.RIGHT : Dir.LEFT;

            foreach (var slot in _formation)
            {
                Human h = slot.Unit;
                if (h == null || h.hp <= 0) continue;

                Vector2Int newPos = new Vector2Int(_frontX - slot.Rank * RankSpacingX * dir, _rowY + slot.Lane);
                if (newPos == h.position)
                {
                    h.currentDir = facing;
                    continue;
                }

                session.UnregisterUnitPos(h, h.position);
                h.position = newPos;
                h.currentDir = facing;
                session.RegisterUnitPos(h, h.position);

                // 이 이동은 GameSession.ProcessUnitAction의 통상 턴 처리 밖(HumanWaveManager가 별도
                // 프레임 루프에서 직접 호출)에서 일어나므로, 그 함수의 oldPos/최종 위치 diff 기반
                // SyncVisual 트리거가 이 변화를 못 잡을 수 있다 — MonsterDefensePlacementSystem이
                // 겪었던 것과 동일한 갭이라 여기서도 직접 동기화한다.
                session.unitGenerate?.SyncVisual(h);
            }
        }

        // 파티 진형(문서 명시): 탱커 1선/근접딜러 2선/지휘관·리더 3선/원거리딜러 4선. 인류 유닛 타입이
        // 현재 2종(근접형/원거리형)뿐이라 "탱커"와 "근접딜러"를 데이터로 구분할 수 없어 하나의 근접
        // 랭크(0)로 합쳐 맨 앞에 두고, 리더는 항상 다음 랭크(1)에 단독으로, 원거리는 마지막 랭크(2)에
        // 둔다(문서 요구대로 리더는 유형과 무관하게 항상 근접-원거리 그룹 사이). 근접/원거리 판정은
        // UnitGenerate.GetEngageDistance(이미 전투 사거리 판정에 쓰이는 값)를 그대로 재사용한다 — 새
        // 분류 데이터를 만들지 않는다. 2026-08-20 사용자 요청("1열로 하지 말고 1선/2선/3선 포메이션
        // 느낌으로") — 같은 랭크에 유닛이 여럿이면 AssignLane으로 rowY를 기준으로 좌우(Y축)에 나란히
        // 늘어서게 한다.
        private static void BuildFormation(GameSession session, Party party, List<FormationSlot> outFormation)
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

        // rank(선) 하나를 채운다 — 유닛이 여럿이면 rowY를 중심으로 좌우 대칭으로 늘어서게 레인을
        // 배정한다. 2026-08-20 사용자 신고("대기할 때 아래쪽으로 쏠려있어, 중간지점에 모이도록") —
        // 예전엔 i - n/2 공식을 썼는데 이게 짝수 인원에서 항상 음수 쪽(-Y, 아래)에 한 명 더 치우쳐
        // 대형 전체의 중심이 rowY보다 아래로 처졌었다. 홀수는 중앙(0)을 포함해 대칭(-1,0,1 등), 짝수는
        // 중앙을 비우고 좌우 동수로 대칭(-2,-1,+1,+2 등)이 되도록 LaneOffset으로 계산한다.
        private static void AddRank(List<FormationSlot> outFormation, List<Human> members, int rank)
        {
            int n = members.Count;
            for (int i = 0; i < n; i++)
            {
                int lane = LaneOffset(i, n);
                outFormation.Add(new FormationSlot { Unit = members[i], Rank = rank, Lane = lane });
            }
        }

        private static int LaneOffset(int i, int n)
        {
            if (n % 2 == 1) return i - n / 2; // 홀수: 중앙 포함 대칭.

            int half = n / 2;
            return i < half ? i - half : i - half + 1; // 짝수: 중앙 비우고 좌우 대칭.
        }

        private const int MeleeEngageDistanceThreshold = 3; // 기사형(2)=근접, 아처형(7)=원거리 기준 중간값.

        private static bool IsMelee(GameSession session, Human h)
        {
            int engageDistance = session?.unitGenerate != null && h.unitType != null
                ? session.unitGenerate.GetEngageDistance(h.unitType.typeName, 2)
                : 2;
            return engageDistance <= MeleeEngageDistanceThreshold;
        }

        // 스폰 시점의 "무더기" 중 가장 앞선(가는 방향 기준 가장 오른쪽) 유닛의 x좌표를 대형 전체의
        // 시작 기준점으로 쓴다 — HumanWaveManager.FindSpawnPosInHiddenChunk가 좁은 반경 안에 흩어
        // 놓으므로 위치 차가 작아 어떤 유닛을 기준으로 삼아도 크게 다르지 않다.
        private static int FrontmostSpawnX(List<FormationSlot> formation)
        {
            int max = int.MinValue;
            foreach (var slot in formation)
            {
                if (slot.Unit == null) continue;
                if (slot.Unit.position.x > max) max = slot.Unit.position.x;
            }
            return max == int.MinValue ? 0 : max;
        }

        private static float MinWalkSpeed(List<FormationSlot> formation)
        {
            float min = float.MaxValue;
            foreach (var slot in formation)
            {
                if (slot.Unit == null) continue;
                if (slot.Unit.BaseStat.walkSpeed < min) min = slot.Unit.BaseStat.walkSpeed;
            }
            return min == float.MaxValue ? 1f : Mathf.Max(0.1f, min);
        }
    }
}
