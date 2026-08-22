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
        if (Vector2.Distance(_cacheTarget, targetPos) <= 3f && Time.time - _cacheTime < 5f)
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
        // -------------------------------------------------------------

        Vector2Int startPos = unit.position;

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

        while (openList.Count > 0 && iter < maxIter)
        {
            iter++;
            AStarNode current = openList.Pop();
            closedSet.Add(current.Pos);

            if (current.Pos == targetPos) { closestNode = current; break; }
            if (current.HCost < closestNode.HCost) closestNode = current;

            foreach (Dir d in _allDirs)
            {
                Vector2Int dirVec = unit.GetDirVector(d);
                if (dirVec == Vector2Int.zero) continue;
                Vector2Int neighborPos = current.Pos + dirVec;

                if (closedSet.Contains(neighborPos)) continue;

                if (!IsTileWalkable(unit, current.Pos, neighborPos, dirVec, myData, mapW, mapH, floorIdx, targetPos, out bool isOccupied)) continue;

                int moveCost = (dirVec.x != 0 && dirVec.y != 0) ? 14 : 10;
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

        if (closestNode == startNode) return false;

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

    // 유닛 점유 타일을 "비용만 추가되는 통행 가능 칸"으로 취급했었는데, 실제 이동을 실행하는
    // UnitFunction.CanMove/Move()는 점유된 칸을 예외 없이 완전히 막는다(2026-07-22 발견) — A*가
    // "이 길로 가면 조금 더 걸리지만 갈 수는 있다"고 추천한 칸이 실제로는 Move() 단계에서 조용히
    // 실패해서, GOAP은 "이동했다"고 착각한 채 다음 계획으로 넘어가지만 유닛은 제자리에 멈춰버리는
    // 버그였다(파티가 밀집한 웨이브 대형에서 서로 자리를 막아 자주 재현 — 사용자 신고 스크린샷 참고).
    // 이제 CanMove와 똑같이 점유된 칸은 완전히 막아서(원래 예외였던 targetPos 자체도 포함) 이 둘이
    // 항상 같은 판단을 하도록 맞춘다.
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

                // 문 진영 통행 판정(기초문서.md 피드백, 2026-08-22) — UnitFunction.CanMove와 반드시
                // 같은 결론을 내야 한다(위 "코너 커팅 방지" 주석과 동일한 이유 — 둘이 어긋나면 A*가
                // 실제로는 막힌 경로를 갈 수 있다고 오판한다).
                if (unit.Session != null && unit.Session.IsBlockedByClosedDoor(new Vector3Int(nx, ny, floorIdx), unit))
                {
                    isWall = true;
                    break;
                }

                if (unit.Session != null &&
                    unit.Session.unitGrid.TryGetValue(new Vector3Int(nx, ny, floorIdx), out Unit u))
                {
                    if (u != null && u != unit && u.hp > 0) { isOccupied = true; isWall = true; break; }
                }
            }
        }

        // 코너 커팅 방지 — Move()의 실제 판정(CanMove, 벽+유닛 점유 둘 다 봄)과 반드시 일치해야
        // 한다. 여기서 벽만 보고 점유는 빼먹으면, A*는 이 대각선이 통과 가능하다고 판단하는데 실제
        // Move()는 대각선 양옆 한 칸을 다른 유닛이 차지하고 있어서 거부하는 불일치가 생긴다 — 좁은
        // 곳에 유닛이 몰렸을 때 서로 대각선으로 길을 막아서 몇몇이 영영 못 움직이는 원인이었다(사용자
        // 제보 콘솔 로그, 2026-07-23 — "이동 시도했지만 실제로는 못 움직임. 점유=False"가 목표 칸이
        // 아니라 대각선 코너 쪽 점유 때문이었다).
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
    // 단일 타일 판정과 같은 기준(벽+점유)을 discoveredMap 기반으로 재현한다. 코너 커팅 방지 체크가
    // Move()의 실제 판정과 어긋나지 않도록 이 헬퍼 하나로 통일해서 쓴다.
    private bool IsCoordBlocked(Unit unit, FactionData myData, int mapW, int mapH, int floorIdx, int x, int y)
    {
        if (x < 0 || x >= mapW || y < 0 || y >= mapH) return true;
        if (myData.discoveredMap[floorIdx][x, y] == 2) return true;

        // 닫힌 문 판정(2026-08-22, 사용자 신고 "2*2문에서 1개 문만 남겨두고 이동할때 중간에 멈춤") —
        // Move()의 코너 커팅 검사는 CanMove를 쓰므로 문(다른 진영 소유)까지 막힌 것으로 보는데, 여기가
        // 벽+점유만 확인하면 A*는 "남은 적 문 옆 뚫린 칸으로의 대각선"을 통과 가능이라 판단하고 Move()는
        // 거부하는 불일치가 생긴다 — 유닛이 아무 피드백 없이 문턱 앞에서 영영 멈추는 원인.
        if (unit.Session != null && unit.Session.IsBlockedByClosedDoor(new Vector3Int(x, y, floorIdx), unit)) return true;

        if (unit.Session != null &&
            unit.Session.unitGrid.TryGetValue(new Vector3Int(x, y, floorIdx), out Unit u))
        {
            if (u != null && u != unit && u.hp > 0) return true;
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
