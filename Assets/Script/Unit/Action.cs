using System.Collections.Generic;
using UnityEngine;

// ==========================================
// GOAP Architecture & Behaviors
// ==========================================
public class GoapState : Dictionary<string, bool> { }

public abstract class GoapGoal
{
    public string Name;
    public GoapState DesiredState = new GoapState();
    public abstract float GetPriority(Unit unit);
}

public abstract class GoapAction
{
    public string ActionName;
    public float Cost = 1f;
    public GoapState Preconditions = new GoapState();
    public GoapState Effects = new GoapState();

    public void AddPrecondition(string key, bool value) => Preconditions[key] = value;
    public void AddEffect(string key, bool value) => Effects[key] = value;

    public abstract bool IsValid(Unit unit);
    public abstract void Execute(Unit unit);

    protected Unit GetClosestEnemy(Unit unit, out float minDist)
    {
        IEnumerable<Unit> enemies = unit is Human ? Unit.humanFactionData.spottedEnemyUnits : unit.personalSpottedEnemies;
        Unit target = null;
        minDist = float.MaxValue;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.hp <= 0 || enemy.currentFloor != unit.currentFloor) continue;
            float d = Vector2Int.Distance(unit.position, enemy.position);
            if (d < minDist)
            {
                minDist = d;
                target = enemy;
            }
        }
        return target;
    }

    protected class AStarNode
    {
        public Vector2Int Pos;
        public AStarNode Parent;
        public int GCost;
        public int HCost;
        public int FCost => GCost + HCost;
    }

    protected void MoveTowardsPos(Unit unit, Vector2Int targetPos)
    {
        if (unit.position == targetPos) return;

        FactionData myData = unit is Human ? Unit.humanFactionData : Unit.monsterFactionData;
        int floorIdx = unit.currentFloor;

        if (myData.discoveredMap == null || floorIdx >= myData.discoveredMap.Length || myData.discoveredMap[floorIdx] == null)
        {
            FallbackMove(unit, targetPos);
            return;
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
                if (openList[i].FCost < current.FCost || (openList[i].FCost == current.FCost && openList[i].HCost < current.HCost))
                {
                    current = openList[i];
                    currentIndex = i;
                }
            }

            openList.RemoveAt(currentIndex);
            closedSet.Add(current.Pos);

            if (current.Pos == targetPos)
            {
                closestNode = current;
                break;
            }

            if (current.HCost < closestNode.HCost)
                closestNode = current;

            foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
            {
                Vector2Int dirVec = unit.GetDirVector(d);
                if (dirVec == Vector2Int.zero) continue;
                Vector2Int neighborPos = current.Pos + dirVec;

                if (closedSet.Contains(neighborPos)) continue;

                bool isWall = false;
                bool isOccupied = false;
                int w = (int)unit.unitType.footprint.x;
                int h = (int)unit.unitType.footprint.y;

                for (int dx = 0; dx < w; dx++)
                {
                    for (int dy = 0; dy < h; dy++)
                    {
                        int nx = neighborPos.x + dx;
                        int ny = neighborPos.y + dy;
                        if (nx < 0 || nx >= mapW || ny < 0 || ny >= mapH) { isWall = true; break; }
                        if (myData.discoveredMap[floorIdx][nx, ny] == 2) { isWall = true; break; }

                        if (neighborPos != targetPos)
                        {
                            if (GameSession.Instance != null && GameSession.Instance.unitGrid.TryGetValue(new Vector3Int(nx, ny, floorIdx), out Unit u))
                            {
                                if (u != null && u != unit && u.hp > 0)
                                    isOccupied = true;
                            }
                        }
                    }
                    if (isWall) break;
                }

                if (isWall) continue;

                if (dirVec.x != 0 && dirVec.y != 0)
                {
                    bool cornerWall1 = false;
                    bool cornerWall2 = false;

                    for (int dx = 0; dx < w; dx++)
                    {
                        for (int dy = 0; dy < h; dy++)
                        {
                            int cx1 = current.Pos.x + dx + dirVec.x;
                            int cy1 = current.Pos.y + dy;
                            int cx2 = current.Pos.x + dx;
                            int cy2 = current.Pos.y + dy + dirVec.y;

                            if (cx1 >= 0 && cx1 < mapW && cy1 >= 0 && cy1 < mapH && myData.discoveredMap[floorIdx][cx1, cy1] == 2) cornerWall1 = true;
                            if (cx2 >= 0 && cx2 < mapW && cy2 >= 0 && cy2 < mapH && myData.discoveredMap[floorIdx][cx2, cy2] == 2) cornerWall2 = true;
                        }
                    }
                    if (cornerWall1 && cornerWall2) continue;
                }

                int moveCost = (dirVec.x != 0 && dirVec.y != 0) ? 14 : 10;
                if (isOccupied) moveCost += 30;

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

        if (closestNode == startNode)
        {
            return;
        }

        AStarNode step = closestNode;
        while (step.Parent != null && step.Parent != startNode)
            step = step.Parent;

        Vector2Int diff = step.Pos - startPos;
        foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
        {
            if (unit.GetDirVector(d) == diff)
            {
                unit.Move(d);
                return;
            }
        }
    }

    private int GetHeuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return 10 * (dx + dy) - 6 * Mathf.Min(dx, dy);
    }

    protected void FallbackMove(Unit unit, Vector2Int targetPos)
    {
        Vector2Int diff = targetPos - unit.position;
        int dx = diff.x == 0 ? 0 : (diff.x > 0 ? 1 : -1);
        int dy = diff.y == 0 ? 0 : (diff.y > 0 ? 1 : -1);

        foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
        {
            if (unit.GetDirVector(d) == new Vector2Int(dx, dy))
            {
                unit.Move(d);
                break;
            }
        }
    }

    protected void MoveTowardsTarget(Unit unit, Unit target)
    {
        MoveTowardsPos(unit, target.position);
    }
}

// Goals
public class Goal_Panic : GoapGoal
{
    public Goal_Panic() { Name = "Panic"; DesiredState["panicResolved"] = true; }
    public override float GetPriority(Unit unit)
    {
        if (unit is Human && unit.mental < unit.maxMental * 0.3f)
        {
            return 150f;
        }
        return 0f;
    }
}


public class Goal_PlayerCommand : GoapGoal
{
    public Goal_PlayerCommand() { Name = "PlayerCommand"; DesiredState["playerCommandExecuted"] = true; }
    public override float GetPriority(Unit unit) => (unit.playerMoveTarget.HasValue || unit.playerAttackTarget != null) ? 100f : 0f;
}

public class Goal_DefeatEnemy : GoapGoal
{
    public Goal_DefeatEnemy() { Name = "DefeatEnemy"; DesiredState["enemyAlive"] = false; }
    public override float GetPriority(Unit unit)
    {
        IEnumerable<Unit> enemies = unit is Human ? Unit.humanFactionData.spottedEnemyUnits : unit.personalSpottedEnemies;
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.hp > 0 && enemy.currentFloor == unit.currentFloor)
            {
                return 80f;
            }
        }
        return 0f;
    }
}

public class Goal_Explore : GoapGoal
{
    public Goal_Explore() { Name = "Explore"; DesiredState["explored"] = true; }
    public override float GetPriority(Unit unit)
    {
        return 10f;
    }
}

// Actions
public class Action_Panic : GoapAction
{
    public Action_Panic()
    {
        ActionName = "Panic";
        AddEffect("panicResolved", true);
    }

    public override bool IsValid(Unit unit) => unit is Human && unit.mental < unit.maxMental * 0.3f;

    public override void Execute(Unit unit)
    {
        if (Random.value > 0.5f)
        {
            Dir randomDir = (Dir)Random.Range(0, 8);
            unit.Move(randomDir);
            Debug.Log($"{unit.unitType.typeName}가 공황에 빠져 무작위로 이동합니다.");
        }
        else
        {
            Debug.Log($"{unit.unitType.typeName}가 공황에 빠져 멈춰있습니다.");
        }
    }
}




public class Action_PlayerCommandExecute : GoapAction
{
    public Action_PlayerCommandExecute()
    {
        ActionName = "PlayerCommandExecute";
        AddEffect("playerCommandExecuted", true);
    }

    public override bool IsValid(Unit unit) => unit.playerMoveTarget.HasValue;

    public override void Execute(Unit unit)
    {
        if (unit.playerMoveTarget.HasValue)
        {
            Vector2Int target = unit.playerMoveTarget.Value;

            if (unit.position == target)
            {
                unit.playerMoveTarget = null;
            }
            else
            {
                MoveTowardsPos(unit, target);
            }
        }
    }
}

public class Action_RandomExplore : GoapAction
{
    public Action_RandomExplore()
    {
        ActionName = "RandomExplore";
        AddEffect("explored", true);
    }
    public override bool IsValid(Unit unit) => true;
    public override void Execute(Unit unit)
    {
        Dir randomDir = (Dir)Random.Range(0, 8);
        unit.Move(randomDir);
    }
}

public class Action_EngageEnemy : GoapAction
{
    private readonly List<SkillAction> knightSkills = new List<SkillAction>
    {
        new SkillAction_KnightFocusedStab(),
        new SkillAction_KnightShieldBash(),
        new SkillAction_KnightFrontSlash()
    };
    private readonly List<SkillAction> meleeTankSkills = new List<SkillAction>
    {
        new SkillAction_MeleeTankHeavySmash(),
        new SkillAction_MeleeTankAmbushClaw(),
        new SkillAction_MeleeTankClawSwipe()
    };
    public Action_EngageEnemy()
    {
        ActionName = "EngageEnemy";
        AddPrecondition("enemyVisible", true);
        AddEffect("enemyAlive", false);
    }
    public override bool IsValid(Unit unit) => true;
    public override void Execute(Unit unit)
    {
        ExecuteSkillActionBased(unit);
    }

    private IEnumerable<SkillAction> GetSkillActions(Unit unit)
    {
        if (unit.unitType is Knight)
            return knightSkills;
        if (unit.unitType is MeleeTank)
            return meleeTankSkills;
        return new List<SkillAction>();
    }

    private void ExecuteSkillActionBased(Unit unit)
    {
        Unit target = GetClosestEnemy(unit, out float minDist);
        if (target == null) return;

        float engageDist = unit.unitType is MeleeTank ? 2.5f : 1.5f;
        Vector2Int diff = target.position - unit.position;
        unit.currentDir = SkillAction.GetDirection8(diff);

        // 공격 각도를 자동으로 계산하여 설정 (대상을 향한 정확한 각도)
        float attackRange = unit.unitType is MeleeTank ? 3 : 2;
        unit.currentAttackAngle = ((UnitFunction)unit).CalculateAttackAngleToEnemy(target, (int)attackRange);

        // 히트박스 공격 범위 검사 (타일 기반이 아님)
        int range = unit.unitType is MeleeTank ? 3 : 2;
        Hitbox attackCheckBox = SkillAction.BuildLineHitbox(unit, range);
        bool canHit = SkillAction.GetEnemiesInHitbox(unit, attackCheckBox).Contains(target);

        if (minDist <= engageDist && canHit)
        {
            SkillAction bestSkill = null;
            float bestPriority = float.MinValue;

            foreach (SkillAction skill in GetSkillActions(unit))
            {
                if (skill == null) continue;
                if (!skill.IsAvailable(unit)) continue;

                float priority = skill.GetPriority(unit, target, minDist);
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    bestSkill = skill;
                }
            }

            if (bestSkill != null)
            {
                bestSkill.Execute(unit, target, minDist);
                return;
            }
        }

        if (unit.evadeCooldown > 0f)
        {
            return;
        }

        MoveTowardsTarget(unit, target);

        Vector2Int diff2 = target.position - unit.position;
        if (Mathf.Abs(diff2.x) > Mathf.Abs(diff2.y))
        {
            unit.currentDir = diff2.x > 0 ? Dir.RIGHT : Dir.LEFT;
        }
        else
        {
            unit.currentDir = diff2.y > 0 ? Dir.UP : Dir.DOWN;
        }
    }
}

// ==========================================
// GOAP Brain (Agent)
// ==========================================
public class GoapBrain
{
    protected List<GoapGoal> availableGoals;
    protected List<GoapAction> availableActions;
    protected GoapAction currentPlannedAction;

    public void JudgeState(Unit unit)
    {
        if (availableGoals == null)
            availableGoals = new List<GoapGoal> { new Goal_Panic(), new Goal_PlayerCommand(), new Goal_DefeatEnemy(), new Goal_Explore() };
        if (availableActions == null)
            availableActions = new List<GoapAction> { new Action_Panic(), new Action_PlayerCommandExecute(), new Action_RandomExplore(), new Action_EngageEnemy() };

        GoapGoal bestGoal = null;
        float highestPriority = -1f;

        foreach (var goal in availableGoals)
        {
            float priority = goal.GetPriority(unit);
            if (priority > highestPriority)
            {
                highestPriority = priority;
                bestGoal = goal;
            }
        }

        GoapState worldState = new GoapState();
        IEnumerable<Unit> enemies = unit is Human ? Unit.humanFactionData.spottedEnemyUnits : unit.personalSpottedEnemies;
        bool enemyVisible = false;
        foreach (var e in enemies)
        {
            if (e != null && e.hp > 0 && e.currentFloor == unit.currentFloor) { enemyVisible = true; break; }
        }
        worldState["enemyVisible"] = enemyVisible;
        worldState["isHit"] = unit.isHitThisTurn;

        currentPlannedAction = null;
        float lowestCost = float.MaxValue;

        if (bestGoal != null)
        {
            foreach (var action in availableActions)
            {
                if (!action.IsValid(unit)) continue;

                bool fulfillsGoal = false;
                foreach (var eff in action.Effects)
                {
                    if (bestGoal.DesiredState.ContainsKey(eff.Key) && bestGoal.DesiredState[eff.Key] == eff.Value)
                    {
                        fulfillsGoal = true;
                        break;
                    }
                }

                if (fulfillsGoal && action.Cost < lowestCost)
                {
                    bool meetsPreconditions = true;
                    foreach (var pre in action.Preconditions)
                    {
                        if (!worldState.ContainsKey(pre.Key) || worldState[pre.Key] != pre.Value)
                        {
                            meetsPreconditions = false;
                            break;
                        }
                    }

                    if (meetsPreconditions)
                    {
                        currentPlannedAction = action;
                        lowestCost = action.Cost;
                    }
                }
            }
        }

        if (!enemyVisible) unit.oneTimeReactUsed = false;
    }

    public void ExecuteAction(Unit unit)
    {
        if (unit.isCastingAttack)
        {
            unit.castTimer -= Time.deltaTime;

            if (unit.castTimer <= 0f)
            {
                unit.pendingAttack?.Invoke();
            }

            return;
        }

        if (currentPlannedAction != null)
        {
            currentPlannedAction.Execute(unit);
        }
        else
        {
            Dir randomDir = (Dir)Random.Range(0, 8);
            unit.Move(randomDir);
        }

        unit.isHitThisTurn = false;
    }
}
