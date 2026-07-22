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

        Vector2Int startPos = unit.position;

        List<AStarNode> openList = new List<AStarNode>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, AStarNode> allNodes = new Dictionary<Vector2Int, AStarNode>();

        AStarNode startNode = new AStarNode { Pos = startPos, GCost = 0, HCost = GetHeuristic(startPos, targetPos) };
        openList.Add(startNode);
        allNodes[startPos] = startNode;

        int maxIter = 5000;
        int iter = 0;
        AStarNode closestNode = startNode;

        while (openList.Count > 0 && iter < maxIter)
        {
            iter++;

            AStarNode current = openList[0];
            int currentIndex = 0;
            for (int i = 1; i < openList.Count; i++)
            {
                if (openList[i].FCost < current.FCost ||
                    (openList[i].FCost == current.FCost && openList[i].HCost < current.HCost))
                {
                    current = openList[i];
                    currentIndex = i;
                }
            }

            openList.RemoveAt(currentIndex);
            closedSet.Add(current.Pos);

            if (current.Pos == targetPos) { closestNode = current; break; }
            if (current.HCost < closestNode.HCost) closestNode = current;

            foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
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
                    neighborNode = new AStarNode { Pos = neighborPos, HCost = GetHeuristic(neighborPos, targetPos) };
                    allNodes[neighborPos] = neighborNode;
                }

                bool inOpen = openList.Contains(neighborNode);
                if (!inOpen || newGCost < neighborNode.GCost)
                {
                    neighborNode.GCost = newGCost;
                    neighborNode.Parent = current;
                    if (!inOpen) openList.Add(neighborNode);
                }
            }
        }

        if (closestNode == startNode) return false;

        AStarNode step = closestNode;
        while (step.Parent != null && step.Parent != startNode)
            step = step.Parent;

        Vector2Int diff = step.Pos - startPos;
        foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
        {
            if (unit.GetDirVector(d) == diff)
            {
                nextDir = d;
                return true;
            }
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

                if (unit.Session != null &&
                    unit.Session.unitGrid.TryGetValue(new Vector3Int(nx, ny, floorIdx), out Unit u))
                {
                    if (u != null && u != unit && u.hp > 0) { isOccupied = true; isWall = true; break; }
                }
            }
        }

        // 코너 커팅 방지
        if (!isWall && Mathf.Abs(dirVec.x) == 1 && Mathf.Abs(dirVec.y) == 1)
        {
            int ortho1X = currentPos.x + dirVec.x, ortho1Y = currentPos.y;
            int ortho2X = currentPos.x, ortho2Y = currentPos.y + dirVec.y;

            bool ortho1Wall = (ortho1X < 0 || ortho1X >= mapW || ortho1Y < 0 || ortho1Y >= mapH || myData.discoveredMap[floorIdx][ortho1X, ortho1Y] == 2);
            bool ortho2Wall = (ortho2X < 0 || ortho2X >= mapW || ortho2Y < 0 || ortho2Y >= mapH || myData.discoveredMap[floorIdx][ortho2X, ortho2Y] == 2);

            if (ortho1Wall || ortho2Wall) isWall = true;
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

        foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
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
