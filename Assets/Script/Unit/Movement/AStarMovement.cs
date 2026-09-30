using System.Collections.Generic;
using UnityEngine;

public class AStarMovement : IMovementAlgorithm
{
    protected class AStarNode
    {
        public Vector2Int Pos;
        public AStarNode Parent;
        public int GCost;
        public int HCost;
        public int FCost => GCost + HCost;
        public int HeapIndex = -1;
    }

    protected class MinHeap
    {
        private List<AStarNode> items = new List<AStarNode>();
        public int Count => items.Count;

        public void Push(AStarNode item)
        {
            item.HeapIndex = items.Count;
            items.Add(item);
            HeapifyUp(item.HeapIndex);
        }

        public AStarNode Pop()
        {
            if (items.Count == 0) return null;
            AStarNode root = items[0];
            int lastIndex = items.Count - 1;
            AStarNode lastItem = items[lastIndex];
            items[0] = lastItem;
            lastItem.HeapIndex = 0;
            items.RemoveAt(lastIndex);
            if (items.Count > 0)
                HeapifyDown(0);
            return root;
        }

        public void UpdateItem(AStarNode item)
        {
            HeapifyUp(item.HeapIndex);
        }

        public bool Contains(AStarNode item)
        {
            return item.HeapIndex >= 0 && item.HeapIndex < items.Count && items[item.HeapIndex] == item;
        }

        private void HeapifyUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                if (Compare(items[index], items[parentIndex]) < 0)
                {
                    Swap(index, parentIndex);
                    index = parentIndex;
                }
                else break;
            }
        }

        private void HeapifyDown(int index)
        {
            int lastIndex = items.Count - 1;
            while (true)
            {
                int leftChild = index * 2 + 1;
                int rightChild = index * 2 + 2;
                int smallest = index;

                if (leftChild <= lastIndex && Compare(items[leftChild], items[smallest]) < 0)
                    smallest = leftChild;
                if (rightChild <= lastIndex && Compare(items[rightChild], items[smallest]) < 0)
                    smallest = rightChild;

                if (smallest != index)
                {
                    Swap(index, smallest);
                    index = smallest;
                }
                else break;
            }
        }

        private int Compare(AStarNode a, AStarNode b)
        {
            int cmp = a.FCost.CompareTo(b.FCost);
            if (cmp == 0) cmp = a.HCost.CompareTo(b.HCost);
            return cmp;
        }

        private void Swap(int a, int b)
        {
            var temp = items[a];
            items[a] = items[b];
            items[b] = temp;
            items[a].HeapIndex = a;
            items[b].HeapIndex = b;
        }

        public void Clear() { items.Clear(); }
    }

    // D: Enum.GetValues는 호출마다 새 배열을 힙에 할당한다 — A* 핫패스(최대 5만 회 반복)에서 매번
    // 호출되면 GC 압력이 폭발하므로, 한 번만 평가해 정적 배열로 고정한다.
    private static readonly Dir[] _allDirs = (Dir[])System.Enum.GetValues(typeof(Dir));

    private Vector2Int _cacheTarget = new Vector2Int(-9999, -9999);
    private Dictionary<Vector2Int, Dir> _pathMap = new Dictionary<Vector2Int, Dir>();
    private float _cacheTime = 0f;

    // 검증문서 03-13: 알려진 활성 함정 회피 컨텍스트 — 탐색 시작마다 Refresh한다(TrapAvoidance.cs). TrapModeOverride가 있으면 유닛 상태와 무관하게 그 모드로
    // 고정한다(진단·긴급 보호용 스크래치 인스턴스). 이동 캐시는 컨텍스트 시그니처가 바뀌면(모드 전환·새 함정 인지·구역 출입) 무효화한다.
    private readonly TrapMoveContext _trapCtx = new TrapMoveContext();
    private int _cacheTrapSignature;
    public TrapMoveMode? TrapModeOverride;

    // "구역 때문에 못 간다"를 진단하는 데 쓰는 함정 무시 스크래치 — 유닛 자신의 탐색 결과(노드 풀)를 건드리지 않게 별도 인스턴스다.
    private static readonly AStarMovement _trapDiagScratch = new AStarMovement { TrapModeOverride = TrapMoveMode.Off };

    // F: A* 실행마다 새로 생성하던 컨테이너를 인스턴스 필드로 올려 재사용한다.
    private readonly MinHeap _openList = new MinHeap();
    private readonly HashSet<Vector2Int> _closedSet = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, AStarNode> _allNodes = new Dictionary<Vector2Int, AStarNode>();

    // 1번: AStarNode 객체 풀 — 매 A* 실행마다 수천~수만 개를 new로 생성하던 것을 없앤다.
    // 실행 종료 시 _allNodes에 남은 노드를 전부 반납하고, 다음 실행 시 꺼내 재사용한다.
    private readonly Stack<AStarNode> _nodePool = new Stack<AStarNode>();

    private AStarNode RentNode()
    {
        if (_nodePool.Count > 0)
        {
            var n = _nodePool.Pop();
            n.Parent = null; n.GCost = 0; n.HCost = 0; n.HeapIndex = -1;
            return n;
        }
        return new AStarNode();
    }

    private void ReturnAllNodes()
    {
        foreach (var n in _allNodes.Values) _nodePool.Push(n);
    }

    public bool TryGetNextStep(Unit unit, Vector2Int targetPos, out Dir nextDir)
    {
        nextDir = Dir.UP; // Default assignment

        if (unit.position == targetPos) return false;

        _trapCtx.Refresh(unit, TrapModeOverride, null);

        FactionData myData = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
        int floorIdx = unit.currentFloor;

        if (myData.discoveredMap == null || floorIdx >= myData.discoveredMap.Length || myData.discoveredMap[floorIdx] == null)
        {
            return TryFallbackMove(unit, targetPos, out nextDir);
        }

        int mapW = myData.discoveredMap[floorIdx].GetLength(0);
        int mapH = myData.discoveredMap[floorIdx].GetLength(1);

        // --- 캐싱 로직: A* 연산 폭주를 막아 프레임 드랍(지랄나는 연산량) 방지 ---
        // 타겟이 약간(3칸 이내) 움직였더라도 기존 목적지 방향 캐시를 유지한다 (근시안적 길찾기 유지).
        if (Vector2.Distance(_cacheTarget, targetPos) <= 3f && Time.time - _cacheTime < 5f && _cacheTrapSignature == _trapCtx.Signature)
        {
            if (_pathMap.TryGetValue(unit.position, out Dir cachedDir))
            {
                Vector2Int cNextPos = unit.position + unit.GetDirVector(cachedDir);
                if (IsTileWalkable(unit, unit.position, cNextPos, unit.GetDirVector(cachedDir), myData, mapW, mapH, floorIdx, targetPos, out bool _))
                {
                    nextDir = cachedDir;
                    return true;
                }
            }
        }
        
        _cacheTarget = targetPos;
        _pathMap.Clear();
        _cacheTime = Time.time;
        _cacheTrapSignature = _trapCtx.Signature;
        // -------------------------------------------------------------

        Vector2Int startPos = unit.position;
        AStarNode closestNode = RunSearch(unit, startPos, targetPos, myData, mapW, mapH, floorIdx, out bool _);

        if (closestNode.Pos == startPos)
        {
            DiagnoseTrapBlock(unit, targetPos, closestNode.HCost);
            return false;
        }

        AStarNode stepNode = closestNode;
        while (stepNode.Parent != null)
        {
            Vector2Int diff = stepNode.Pos - stepNode.Parent.Pos;
            foreach (Dir d in _allDirs)
            {
                if (unit.GetDirVector(d) == diff)
                {
                    _pathMap[stepNode.Parent.Pos] = d;
                    break;
                }
            }
            stepNode = stepNode.Parent;
        }

        if (_pathMap.TryGetValue(startPos, out Dir startDir))
        {
            nextDir = startDir;
            return true;
        }

        return false;
    }

    // TryGetNextStep의 A* 탐색 루프를 그대로 추출한 순수 검색 — 프레임 간 이동 캐시(_cacheTarget/
    // _pathMap/_cacheTime)는 건드리지 않는다(TryGetPathLength도 재사용해 거리 조회로 캐시가 오염되지
    // 않게 한다). reachedTarget=true면 closestNode가 targetPos에 정확히 도달, false면 도달 실패 시
    // 발견한 가장 가까운 노드(휴리스틱 기준) — 시작 위치 그대로면 한 걸음도 못 나간 완전 실패다.
    private AStarNode RunSearch(Unit unit, Vector2Int startPos, Vector2Int targetPos, FactionData myData, int mapW, int mapH, int floorIdx, out bool reachedTarget)
    {
        _openList.Clear();
        _closedSet.Clear();
        ReturnAllNodes(); // 이전 실행 노드를 풀에 반납한 뒤 컨테이너를 비운다.
        _allNodes.Clear();
        MinHeap openList = _openList;
        HashSet<Vector2Int> closedSet = _closedSet;
        Dictionary<Vector2Int, AStarNode> allNodes = _allNodes;

        AStarNode startNode = RentNode();
        startNode.Pos = startPos; startNode.GCost = 0; startNode.HCost = GetHeuristic(startPos, targetPos);
        openList.Push(startNode);
        allNodes[startPos] = startNode;

        int maxIter = 50000; // 맵 횡단을 위해 A* 길찾기 최대 연산량도 대폭 상향 (MinHeap 덕분에 5만 번도 순식간에 처리됨)
        int iter = 0;
        AStarNode closestNode = startNode;
        bool acceptedNearTarget = false;

        while (openList.Count > 0 && iter < maxIter)
        {
            iter++;
            AStarNode current = openList.Pop();
            closedSet.Add(current.Pos);

            if (current.Pos == targetPos) { closestNode = current; break; }
            // 조회 전용: 목표(점유 영역)의 공격 거리 안 어느 타일에든 닿으면 도달로 본다(CanReachWithinRange).
            if (_queryAcceptRange >= 0 && MovementMath.DistanceToFootprint(current.Pos, targetPos, _queryAcceptSize) <= _queryAcceptRange)
            {
                closestNode = current;
                acceptedNearTarget = true;
                break;
            }
            if (current.HCost < closestNode.HCost) closestNode = current;

            foreach (Dir d in _allDirs)
            {
                Vector2Int dirVec = unit.GetDirVector(d);
                if (dirVec == Vector2Int.zero) continue;
                Vector2Int neighborPos = current.Pos + dirVec;

                if (closedSet.Contains(neighborPos)) continue;

                if (!IsTileWalkable(unit, current.Pos, neighborPos, dirVec, myData, mapW, mapH, floorIdx, targetPos, out bool isOccupied)) continue;

                int moveCost = (dirVec.x != 0 && dirVec.y != 0) ? 14 : 10;
                moveCost += GetExtraTileCost(unit, neighborPos);
                int newGCost = current.GCost + moveCost;

                if (!allNodes.TryGetValue(neighborPos, out AStarNode neighborNode))
                {
                    neighborNode = RentNode();
                    neighborNode.Pos = neighborPos; neighborNode.HCost = GetHeuristic(neighborPos, targetPos);
                    allNodes[neighborPos] = neighborNode;
                }

                bool inOpen = openList.Contains(neighborNode);
                if (!inOpen || newGCost < neighborNode.GCost)
                {
                    neighborNode.GCost = newGCost;
                    neighborNode.Parent = current;

                    if (!inOpen) openList.Push(neighborNode);
                    else openList.UpdateItem(neighborNode);
                }
            }
        }

        reachedTarget = acceptedNearTarget || closestNode.Pos == targetPos;
        return closestNode;
    }

    // 거리·경로 "조회" 전용 탐색 — 목표 타일을 다른 유닛이 점유하고 있어도(유닛 위치를 목표로 준 조회: 추격 대상·보호 대상까지의 경로) 그 점유는 막지 않는다.
    // IsTileWalkable은 실제 이동(Move)과 어긋나지 않게 점유 타일을 목표여도 막지만, 그 규칙을 조회에도 적용하면 유닛 위치를 향한 TryGetPathLength/TryGetPathTiles가 도달 판정에
    // 항상 실패한다(검증 03-13 부수 발견 — 02-05 이동 한도·02-10 노출 경로 비교가 사실상 동작하지 않던 원인). 다른 칸의 점유는 그대로 장애물로 본다. 이동(TryGetNextStep)은
    // 이 예외를 쓰지 않는다 — 목표 유닛의 타일로 실제로 들어갈 수는 없기 때문이다.
    private bool _queryExemptTargetOccupancy;
    // 조회 전용 추가 옵션(RunQuerySearch가 세팅·해제): 목표 점유 영역의 공격 거리 안 아무 타일이나 도달로 인정(-1 = 끔) / 적 진영 유닛의 점유를 장애물로 보지 않는 구조 경로
    // (02-04 4번 "보스로 가는 길을 막는 적" 탐지 — 적이 서 있는 자리를 지나는 경로를 잰다. 아군·자기 진영 유닛은 여전히 장애물이다).
    private int _queryAcceptRange = -1;
    private Vector2Int _queryAcceptSize = Vector2Int.one;
    private bool _queryIgnoreEnemyUnits;

    private AStarNode RunQuerySearch(Unit unit, Vector2Int targetPos, FactionData myData, int mapW, int mapH, int floorIdx, out bool reachedTarget,
        int acceptRange = -1, Vector2Int? acceptSize = null, bool ignoreEnemyUnits = false)
    {
        _queryExemptTargetOccupancy = true;
        _queryAcceptRange = acceptRange;
        _queryAcceptSize = acceptSize ?? Vector2Int.one;
        _queryIgnoreEnemyUnits = ignoreEnemyUnits;
        try { return RunSearch(unit, unit.position, targetPos, myData, mapW, mapH, floorIdx, out reachedTarget); }
        finally
        {
            _queryExemptTargetOccupancy = false;
            _queryAcceptRange = -1;
            _queryIgnoreEnemyUnits = false;
        }
    }

    // 04번 문서 9번 항목: 후보 스코어링용 실제 경로 길이 조회 — TryGetNextStep과 달리 이동 캐시는
    // 건드리지 않는다(일회성 순위 매기기용 조회라서). fullyRevealed는 경로의 모든 칸이 이 유닛의
    // 개인 지도에 이미 드러나 있는지를 뜻한다(인류가 아니면 항상 false). 목표 타일 점유는 막지 않는다(RunQuerySearch).
    // exemptTrapTile: 그 함정 자체가 목표인 조회(담당 후보의 도착시간 등, 03번 v0.12 8장)라 그 함정의 회피 구역을 면제하고 함정 타일 도달을 허용한다.
    public bool TryGetPathLength(Unit unit, Vector2Int targetPos, out int pathLength, out bool fullyRevealed, Vector2Int? exemptTrapTile = null)
    {
        pathLength = 0;
        fullyRevealed = false;

        if (unit.position == targetPos)
        {
            fullyRevealed = unit is Human;
            return true;
        }

        FactionData myData = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
        int floorIdx = unit.currentFloor;

        if (myData.discoveredMap == null || floorIdx >= myData.discoveredMap.Length || myData.discoveredMap[floorIdx] == null)
            return false;

        int mapW = myData.discoveredMap[floorIdx].GetLength(0);
        int mapH = myData.discoveredMap[floorIdx].GetLength(1);

        _trapCtx.Refresh(unit, TrapModeOverride, exemptTrapTile);
        AStarNode closestNode = RunQuerySearch(unit, targetPos, myData, mapW, mapH, floorIdx, out bool reachedTarget);
        if (!reachedTarget) return false;

        Human human = unit as Human;
        fullyRevealed = human != null;
        int length = 0;
        AStarNode stepNode = closestNode;
        while (stepNode.Parent != null)
        {
            length++;
            if (fullyRevealed && !human.personalMap.IsTileRevealed(new Vector3Int(stepNode.Pos.x, stepNode.Pos.y, floorIdx)))
                fullyRevealed = false;
            stepNode = stepNode.Parent;
        }

        pathLength = length;
        return true;
    }

    // 명령 경로 시각화용 — TryGetNextStep이 채워둔 _cacheTarget/_pathMap을 그대로 읽기만 한다.
    // 아직 한 번도 경로를 계산한 적 없으면 _pathMap.Count == 0이라 false를 반환한다.
    public bool TryGetCachedDestination(out Vector2Int destination)
    {
        destination = _cacheTarget;
        return _pathMap.Count > 0;
    }

    // unit.position에서 시작해 _pathMap을 따라 _cacheTarget까지 좌표를 나열한다. 유닛이 마지막 계산
    // 이후 이동했으면 앞쪽 일부 좌표가 없을 수 있다(다음 TryGetNextStep 호출에서 다시 채워진다).
    public List<Vector2Int> BuildCachedPathPreview(Unit unit, int maxSteps)
    {
        var result = new List<Vector2Int> { unit.position };
        Vector2Int cur = unit.position;
        int steps = 0;
        while (cur != _cacheTarget && steps < maxSteps)
        {
            if (!_pathMap.TryGetValue(cur, out Dir dir)) break;
            cur += unit.GetDirVector(dir);
            result.Add(cur);
            steps++;
        }
        return result;
    }

    public void ClearCache()
    {
        _cacheTarget = new Vector2Int(-9999, -9999);
        _pathMap.Clear();
        _cacheTime = 0f;
    }

    // 04번 문서 4장: 타일별 추가 이동비용 훅. 기본은 함정 회피 비용뿐(알려진 함정이 없으면 0 — 기존 동작 그대로) — 항상 0 이상만 반환해야 한다,
    // 음수면 GetHeuristic의 admissibility가 깨져 A*가 최적해를 못 찾을 수 있다.
    protected virtual int GetExtraTileCost(Unit unit, Vector2Int tilePos) => _trapCtx.Active ? _trapCtx.ExtraCost(tilePos) : 0;

    // TryGetPathLength(칸 수만)와 달리 실제 경로 타일 좌표가 필요한 호출부(예: 노출 경로가 어느 위험
    // 지역과 겹치는지 판정)용 — 같은 RunSearch를 재사용하고 이동 캐시는 건드리지 않는다(일회성 조회).
    // 목표 타일 점유는 막지 않는다(RunQuerySearch) — 경로 마지막 타일이 목표 타일이다.
    // ignoreEnemyUnits: 적 진영 유닛의 점유도 장애물로 보지 않는 "구조 경로"(02-04 4번 — 적을 치우면 열리는 길이 어디인지 잰다). 아군·자기 진영 유닛은 여전히 장애물이다.
    public bool TryGetPathTiles(Unit unit, Vector2Int targetPos, out List<Vector2Int> tiles, Vector2Int? exemptTrapTile = null, bool ignoreEnemyUnits = false)
    {
        tiles = new List<Vector2Int>();
        if (unit.position == targetPos) return true;

        FactionData myData = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
        int floorIdx = unit.currentFloor;
        if (myData.discoveredMap == null || floorIdx >= myData.discoveredMap.Length || myData.discoveredMap[floorIdx] == null)
            return false;

        int mapW = myData.discoveredMap[floorIdx].GetLength(0);
        int mapH = myData.discoveredMap[floorIdx].GetLength(1);

        _trapCtx.Refresh(unit, TrapModeOverride, exemptTrapTile);
        AStarNode closestNode = RunQuerySearch(unit, targetPos, myData, mapW, mapH, floorIdx, out bool reachedTarget, ignoreEnemyUnits: ignoreEnemyUnits);
        if (!reachedTarget) return false;

        AStarNode stepNode = closestNode;
        while (stepNode.Parent != null)
        {
            tiles.Add(stepNode.Pos);
            stepNode = stepNode.Parent;
        }
        tiles.Reverse();
        return true;
    }

    // 조회: 다른 유닛의 점유를 피해서 목표(점유 영역 = 좌하단 targetPos + 크기 targetSize)의 공격 거리(range, 체비셰프) 안 어느 타일에든 닿을 수 있는가 — 목표 자신의 점유는 무시하고,
    // 이미 거리 안이면 true. "보스로 가는 길이 막혔는가"(02-04 4번)의 우회 가능 여부 판정에 쓴다. 이동 캐시는 안 건드린다.
    public bool CanReachWithinRange(Unit unit, Vector2Int targetPos, Vector2Int targetSize, int range)
    {
        if (MovementMath.DistanceToFootprint(unit.position, targetPos, targetSize) <= range) return true;

        FactionData myData = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
        int floorIdx = unit.currentFloor;
        if (myData.discoveredMap == null || floorIdx >= myData.discoveredMap.Length || myData.discoveredMap[floorIdx] == null)
            return false;

        int mapW = myData.discoveredMap[floorIdx].GetLength(0);
        int mapH = myData.discoveredMap[floorIdx].GetLength(1);

        _trapCtx.Refresh(unit, TrapModeOverride, null);
        RunQuerySearch(unit, targetPos, myData, mapW, mapH, floorIdx, out bool reached, acceptRange: range, acceptSize: targetSize);
        return reached;
    }

    // 도달 여부와 무관하게 "가장 가까이 갈 수 있는 곳"까지의 경로 타일과 그 지점의 휴리스틱 비용 — 함정 때문에 막혔는지 진단할 때 쓴다(이동 캐시는 안 건드림).
    public bool TraceTowards(Unit unit, Vector2Int targetPos, out List<Vector2Int> tiles, out int closestHCost)
    {
        tiles = new List<Vector2Int>();
        closestHCost = int.MaxValue;
        if (unit.position == targetPos) return false;

        FactionData myData = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
        int floorIdx = unit.currentFloor;
        if (myData.discoveredMap == null || floorIdx >= myData.discoveredMap.Length || myData.discoveredMap[floorIdx] == null)
            return false;

        int mapW = myData.discoveredMap[floorIdx].GetLength(0);
        int mapH = myData.discoveredMap[floorIdx].GetLength(1);

        _trapCtx.Refresh(unit, TrapModeOverride, null);
        AStarNode closestNode = RunSearch(unit, unit.position, targetPos, myData, mapW, mapH, floorIdx, out bool _);
        closestHCost = closestNode.HCost;

        for (AStarNode stepNode = closestNode; stepNode.Parent != null; stepNode = stepNode.Parent)
            tiles.Add(stepNode.Pos);
        tiles.Reverse();
        return true;
    }

    // 한 걸음도 못 나가는 순간(closest == start), 함정을 무시하면 더 가까이 갈 수 있고 그 경로가 이 유닛의 회피 구역을 지난다면 그 함정을 "막힘 신호"로 남긴다
    // (unit.trapBlock*). 소비자: TrapPartySystem.TickBlockedPathResponse(비전투 — 대응 재개), CombatFSMState.ChaseTarget(전투 — 파괴 판단). 유닛당 0.5초 스로틀.
    // 이동 탐색과 이 진단은 점유된 목표 타일(추격 대상)에 도달로 안 잡히므로(조회 전용 예외는 RunQuerySearch만) "도달 여부"가 아니라 "가장 가까이 간 정도"(HCost)를 비교한다.
    private void DiagnoseTrapBlock(Unit unit, Vector2Int targetPos, int blockedHCost)
    {
        if (TrapModeOverride.HasValue || !_trapCtx.HasHardBlocks) return;
        if (Time.time < unit.nextTrapBlockDiagTime) return;
        unit.nextTrapBlockDiagTime = Time.time + 0.5f;

        if (!_trapDiagScratch.TraceTowards(unit, targetPos, out List<Vector2Int> offPath, out int offHCost) || offHCost >= blockedHCost) return;
        if (_trapCtx.TryFindBlockingTrap(offPath, out TrapAvoidance.KnownTrap blocking))
        {
            unit.trapBlockTrapId = blocking.Id;
            unit.trapBlockSignalTime = Time.time;
        }
    }

    // 점유된 칸은 CanMove와 동일하게 예외 없이 완전히 막는다(원래 예외였던 targetPos 자체도 포함) —
    // 그렇지 않으면 A*가 "갈 수 있다"고 추천한 칸에서 실제 Move()가 조용히 실패해, GOAP은 "이동했다"고
    // 착각한 채 다음 계획으로 넘어가지만 유닛은 제자리에 멈추는 불일치가 생긴다. 예외는 "조회"뿐이다 —
    // 거리·경로 길이 조회(RunQuerySearch)는 목표 타일 점유만 무시해 유닛 위치까지의 경로를 잴 수 있게 한다.
    protected virtual bool IsTileWalkable(Unit unit, Vector2Int currentPos, Vector2Int neighborPos, Vector2Int dirVec, FactionData myData, int mapW, int mapH, int floorIdx, Vector2Int targetPos, out bool isOccupied)
    {
        isOccupied = false;
        bool isWall = false;
        int fw = (int)unit.unitType.footprint.x;
        int fh = (int)unit.unitType.footprint.y;

        for (int dx = 0; dx < fw && !isWall; dx++)
        {
            for (int dy = 0; dy < fh && !isWall; dy++)
            {
                int nx = neighborPos.x + dx;
                int ny = neighborPos.y + dy;

                if (nx < 0 || nx >= mapW || ny < 0 || ny >= mapH) { isWall = true; break; }
                if (myData.discoveredMap[floorIdx][nx, ny] == 2) { isWall = true; break; }

                // UnitFunction.CanMove와 반드시 같은 결론을 내야 한다 — 어긋나면 A*가 막힌 경로를 갈 수 있다고 오판한다.
                if (unit.Session != null && unit.Session.IsBlockedByClosedDoor(new Vector3Int(nx, ny, floorIdx), unit))
                {
                    isWall = true;
                    break;
                }

                if (unit.Session != null &&
                    unit.Session.unitGrid.TryGetValue(new Vector3Int(nx, ny, floorIdx), out Unit u))
                {
                    if (u != null && u != unit && u.hp > 0
                        && !(_queryExemptTargetOccupancy && nx == targetPos.x && ny == targetPos.y)
                        && !(_queryIgnoreEnemyUnits && unit.IsEnemy(u)))
                    { isOccupied = true; isWall = true; break; }
                }

                // 검증문서 03-13: 알려진 활성 함정 — 일반 모드는 인접 1칸 구역, 전투 모드는 통과 조건을 못 채운 함정 타일을 막는다.
                // A*가 CanMove보다 엄격한 쪽이라(막힌 걸음을 안 고를 뿐) 두 판정이 어긋나도 유닛이 제자리에 얼어붙지 않는다.
                if (_trapCtx.Active && _trapCtx.BlocksTile(new Vector2Int(nx, ny), targetPos)) { isWall = true; break; }
            }
        }

        // 코너 커팅 방지 — Move()의 실제 판정(CanMove, 벽+유닛 점유 둘 다 봄)과 반드시 일치해야 한다.
        // 벽만 보고 점유를 빼먹으면 A*는 대각선이 통과 가능하다고 판단하는데 실제 Move()는 대각선
        // 양옆 한 칸을 다른 유닛이 차지해 거부하는 불일치가 생긴다.
        if (!isWall && Mathf.Abs(dirVec.x) == 1 && Mathf.Abs(dirVec.y) == 1)
        {
            int ortho1X = currentPos.x + dirVec.x, ortho1Y = currentPos.y;
            int ortho2X = currentPos.x, ortho2Y = currentPos.y + dirVec.y;

            if (IsCoordBlocked(unit, myData, mapW, mapH, floorIdx, ortho1X, ortho1Y) ||
                IsCoordBlocked(unit, myData, mapW, mapH, floorIdx, ortho2X, ortho2Y))
            {
                isWall = true;
            }
        }

        if (isWall) return false;

        // 대각선 코너 차단
        if (dirVec.x != 0 && dirVec.y != 0)
        {
            bool cornerWall1 = false, cornerWall2 = false;
            for (int dx = 0; dx < fw && !(cornerWall1 && cornerWall2); dx++)
            {
                for (int dy = 0; dy < fh && !(cornerWall1 && cornerWall2); dy++)
                {
                    int cx1 = currentPos.x + dx + dirVec.x;
                    int cy1 = currentPos.y + dy;
                    int cx2 = currentPos.x + dx;
                    int cy2 = currentPos.y + dy + dirVec.y;

                    if (cx1 >= 0 && cx1 < mapW && cy1 >= 0 && cy1 < mapH && myData.discoveredMap[floorIdx][cx1, cy1] == 2) cornerWall1 = true;
                    if (cx2 >= 0 && cx2 < mapW && cy2 >= 0 && cy2 < mapH && myData.discoveredMap[floorIdx][cx2, cy2] == 2) cornerWall2 = true;
                }
            }
            if (cornerWall1 && cornerWall2) return false;
        }

        return true;
    }

    // 좌표 하나가 벽이거나(범위 밖 포함) 다른 살아있는 유닛이 점유 중이면 true — UnitFunction.CanMove의
    // 단일 타일 판정과 같은 기준을 discoveredMap 기반으로 재현해 코너 커팅 체크와 Move()가 어긋나지 않게 한다.
    private bool IsCoordBlocked(Unit unit, FactionData myData, int mapW, int mapH, int floorIdx, int x, int y)
    {
        if (x < 0 || x >= mapW || y < 0 || y >= mapH) return true;
        if (myData.discoveredMap[floorIdx][x, y] == 2) return true;

        // Move()의 코너 커팅 검사는 CanMove를 쓰므로 다른 진영 소유 문도 막힌 것으로 본다 — 여기서
        // 빠뜨리면 A*가 "닫힌 문 옆 대각선"을 통과 가능이라 오판해 문턱 앞에서 영영 멈추게 된다.
        if (unit.Session != null && unit.Session.IsBlockedByClosedDoor(new Vector3Int(x, y, floorIdx), unit)) return true;

        if (unit.Session != null &&
            unit.Session.unitGrid.TryGetValue(new Vector3Int(x, y, floorIdx), out Unit u))
        {
            if (u != null && u != unit && u.hp > 0 && !(_queryIgnoreEnemyUnits && unit.IsEnemy(u))) return true;
        }

        return false;
    }

    private int GetHeuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return 10 * (dx + dy) - 6 * Mathf.Min(dx, dy);
    }

    protected bool TryFallbackMove(Unit unit, Vector2Int targetPos, out Dir nextDir)
    {
        nextDir = Dir.UP;
        Vector2Int diff = targetPos - unit.position;
        int dx = diff.x == 0 ? 0 : (diff.x > 0 ? 1 : -1);
        int dy = diff.y == 0 ? 0 : (diff.y > 0 ? 1 : -1);

        FactionData myData = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
        int floorIdx = unit.currentFloor;
        int mapW = 0, mapH = 0;
        if (myData.discoveredMap != null && floorIdx < myData.discoveredMap.Length && myData.discoveredMap[floorIdx] != null)
        {
            mapW = myData.discoveredMap[floorIdx].GetLength(0);
            mapH = myData.discoveredMap[floorIdx].GetLength(1);
        }

        foreach (Dir d in _allDirs)
        {
            Vector2Int dirVec = unit.GetDirVector(d);
            if (dirVec == new Vector2Int(dx, dy))
            {
                if (mapW > 0 && mapH > 0)
                {
                    if (!IsTileWalkable(unit, unit.position, unit.position + dirVec, dirVec, myData, mapW, mapH, floorIdx, targetPos, out bool isOcc))
                        continue;
                }
                nextDir = d;
                return true;
            }
        }
        return false;
    }
}
