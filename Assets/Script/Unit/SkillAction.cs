using System.Collections.Generic;
using UnityEngine;

public abstract class SkillAction
{
    public string SkillName { get; protected set; }

    public abstract bool IsAvailable(Unit unit);
    public abstract float GetPriority(Unit unit, Unit target, float minDist);
    public abstract void Execute(Unit unit, Unit target, float minDist);

    public static List<Vector2Int> GetLineTiles(
        Unit unit,
        int range
    )
    {
        List<Vector2Int> tiles = new List<Vector2Int>();

        Vector2Int dir = unit.GetDirVector(unit.currentDir);

        Vector2Int current = unit.position;

        for (int i = 1; i <= range; i++)
        {
            current += dir;

            tiles.Add(current);
        }

        return tiles;
    }

    public static List<Vector2Int> GetFrontAreaTiles(Unit unit, int width, int depth)
    {
        List<Vector2Int> result = new List<Vector2Int>();

        Vector2Int forward = unit.GetDirVector(unit.currentDir);

        Vector2Int right = GetRightVector(forward);

        for (int d = 1; d <= depth; d++)
        {
            Vector2Int center = unit.position + forward * d;

            for (int w = -width / 2; w <= width / 2; w++)
            {
                result.Add(center + right * w);
            }
        }

        return result;
    }

    public static void BeginAttackCast(
        Unit unit,
        float castMs,
        ThreatTileData threat,
        System.Action attackAction,
        System.Action cooldownAction = null,
        System.Action effectAction = null
    )
    {
        unit.isCastingAttack = true;

        unit.castTimer = castMs / 1000f;

        threat.tiles =
            BuildThreatTiles(unit, threat);

        threat.visualTiles =
            BuildVisualThreatTiles(unit, threat);

        unit.threatTiles.Clear();

        unit.threatTiles.Add(threat);

        unit.pendingAttack = () =>
        {
            try
            {
                effectAction?.Invoke();

                attackAction?.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogError(
                    $"[AttackCast Error] {unit.unitType.typeName} : {e}"
                );
            }
            finally
            {
                // =========================
                // 무조건 실행
                // =========================

                // 쿨타임 적용
                cooldownAction?.Invoke();

                // 위협 타일 제거
                unit.threatTiles.Clear();

                // 캐스팅 종료
                unit.isCastingAttack = false;

                // 예약 제거
                unit.pendingAttack = null;

                // 타이머 초기화
                unit.castTimer = 0f;
            }
        };
    }

    public static List<Unit> GetEnemiesInTiles(Unit attacker, List<Vector2Int> tiles)
    {
        List<Unit> result = new List<Unit>();

        foreach (Unit u in GameSession.Instance.units)
        {
            if (u == null) continue;
            if (u == attacker) continue;
            if (u.hp <= 0) continue;

            if (u.currentFloor != attacker.currentFloor)
                continue;

            bool isEnemy =
                (attacker is Human && u is Monster) ||
                (attacker is Monster && u is Human);

            if (!isEnemy)
                continue;

            int w = (int)u.unitType.footprint.x;
            int h = (int)u.unitType.footprint.y;

            for (int dx = 0; dx < w; dx++)
            {
                for (int dy = 0; dy < h; dy++)
                {
                    Vector2Int p = new Vector2Int(u.position.x + dx, u.position.y + dy);

                    if (tiles.Contains(p))
                    {
                        result.Add(u);

                        dx = w;
                        break;
                    }
                }
            }
        }

        return result;
    }

    public static void DamageEnemiesInTiles(Unit attacker, List<Vector2Int> tiles, float multiplier, bool stun = false, float stunDuration = 0f)
    {
        List<Unit> targets =
            GetEnemiesInTiles(attacker, tiles);

        foreach (Unit hit in targets)
        {
            hit.TakePhysicalDamage(attacker.physicalAttack * multiplier, attacker
            );

            if (stun)
            {
                hit.ApplyStun(stunDuration);
            }
        }
    }

    public static void DamageEnemiesInTilesCompressed(Unit attacker, List<Vector2Int> tiles, float multiplier, float compression, bool stun = false, float stunDuration = 0f)
    {
        foreach (Unit u in GameSession.Instance.units)
        {
            if (u == null || u.hp <= 0) continue;
            if (u == attacker) continue;
            if (u.currentFloor != attacker.currentFloor) continue;

            bool isEnemy = (attacker is Human && u is Monster) || (attacker is Monster && u is Human);

            if (!isEnemy) continue;

            int w = (int)u.unitType.footprint.x;
            int h = (int)u.unitType.footprint.y;

            for (int dx = 0; dx < w; dx++)
            {
                for (int dy = 0; dy < h; dy++)
                {
                    Vector2 targetPos =
                        new Vector2(u.position.x + dx, u.position.y + dy);

                    foreach (var t in tiles)
                    {
                        if (Vector2.Distance(t, targetPos) < 0.1f)
                        {
                            float finalMultiplier = multiplier;

                            // =====================================
                            // 대각선 보정 타일이면 50% 감소
                            // =====================================

                            foreach (ThreatTileData tt in attacker.threatTiles)
                            {
                                if (tt.partialTiles.Contains(t))
                                {
                                    finalMultiplier *= 0.5f;
                                    break;
                                }
                            }

                            u.TakePhysicalDamage(
                                attacker.physicalAttack * finalMultiplier,
                                attacker
                            );

                            if (stun)
                                u.ApplyStun(stunDuration);

                            goto NEXT_UNIT;
                        }
                    }
                }
            }

        NEXT_UNIT:;
        }
    }

    public static List<Vector2Int> BuildVisualThreatTiles(
        Unit unit,
        ThreatTileData data
    )
    {
        List<Vector2Int> result =
            new List<Vector2Int>();

        Vector2Int forward =
            unit.GetDirVector(unit.currentDir);

        bool diagonal =
            forward.x != 0 &&
            forward.y != 0;

        // =========================
        // LINE
        // =========================

        if (data.shape == ThreatShape.LINE)
        {
            Vector2Int current =
                unit.position;

            int realRange = data.range;

            if (diagonal)
            {
                realRange =
                    Mathf.RoundToInt(
                        data.range / Mathf.Sqrt(2f)
                    );

                realRange =
                    Mathf.Max(1, realRange);
            }

            for (int i = 1; i <= realRange; i++)
            {
                current += forward;

                result.Add(current);
            }
        }

        // =========================
        // RECT
        // =========================

        else if (data.shape == ThreatShape.RECT)
        {
            Vector2Int right =
                GetRightVector(forward);

            int realDepth = data.depth;
            int realWidth = data.width;

            if (diagonal)
            {
                realDepth =
                    Mathf.RoundToInt(
                        data.depth / Mathf.Sqrt(2f)
                    );

                realDepth =
                    Mathf.Max(1, realDepth);

                realWidth =
                    Mathf.RoundToInt(
                        data.width / Mathf.Sqrt(2f)
                    );

                realWidth =
                    Mathf.Max(1, realWidth);
            }

            for (int d = 1; d <= realDepth; d++)
            {
                Vector2Int center =
                    unit.position + forward * d;

                for (
                    int w = -realWidth / 2;
                    w <= realWidth / 2;
                    w++
                )
                {
                    result.Add(center + right * w);
                }
            }
        }

        return result;
    }

    public static List<Vector2Int> BuildThreatTiles(Unit unit, ThreatTileData data)
    {
        List<Vector2Int> result = new List<Vector2Int>();

        Vector2Int forward = unit.GetDirVector(unit.currentDir);

        Vector2Int right = new Vector2Int(forward.y, -forward.x);

        bool diagonal =
            forward.x != 0 &&
            forward.y != 0;

        // =========================
        // LINE
        // =========================
        if (data.shape == ThreatShape.LINE)
        {
            Vector2Int current = unit.position;

            int realRange = data.range;

            // =====================================
            // 대각선 거리 보정
            // =====================================

            if (diagonal)
            {
                realRange =
                    Mathf.RoundToInt(
                        data.range / Mathf.Sqrt(2f)
                    );

                realRange =
                    Mathf.Max(1, realRange);
            }

            for (int i = 1; i <= realRange; i++)
            {
                current += forward;

                result.Add(current);

                // =====================================
                // 대각선 끊김 보정
                // =====================================

                if (diagonal)
                {
                    Vector2Int partial1 =
                        new Vector2Int(
                            current.x - forward.x,
                            current.y
                        );

                    Vector2Int partial2 =
                        new Vector2Int(
                            current.x,
                            current.y - forward.y
                        );

                    result.Add(partial1);
                    result.Add(partial2);

                    data.partialTiles.Add(partial1);
                    data.partialTiles.Add(partial2);
                }
            }
        }

        // =========================
        // RECT
        // =========================
        else if (data.shape == ThreatShape.RECT)
        {
            int realDepth = data.depth;
            int realWidth = data.width;

            // =====================================
            // 대각선 거리 보정
            // =====================================

            if (diagonal)
            {
                realDepth =
                    Mathf.RoundToInt(
                        data.depth / Mathf.Sqrt(2f)
                    );

                realDepth =
                    Mathf.Max(1, realDepth);

                realWidth =
                    Mathf.RoundToInt(
                        data.width / Mathf.Sqrt(2f)
                    );

                realWidth =
                    Mathf.Max(1, realWidth);
            }

            for (int d = 1; d <= realDepth; d++)
            {
                Vector2Int center =
                    unit.position + forward * d;

                for (
                    int w = -realWidth / 2;
                    w <= realWidth / 2;
                    w++
                )
                {
                    Vector2Int tile =
                        center + right * w;

                    result.Add(tile);

                    // =====================================
                    // 대각선 끊김 보정
                    // =====================================

                    if (diagonal)
                    {
                        Vector2Int partial1 =
                            new Vector2Int(
                                tile.x - forward.x,
                                tile.y
                            );

                        Vector2Int partial2 =
                            new Vector2Int(
                                tile.x,
                                tile.y - forward.y
                            );

                        result.Add(partial1);
                        result.Add(partial2);

                        data.partialTiles.Add(partial1);
                        data.partialTiles.Add(partial2);
                    }
                }
            }
        }

        // =========================
        // CONE
        // =========================
        else if (data.shape == ThreatShape.CONE)
        {
            int realDepth = data.depth;

            if (diagonal)
            {
                realDepth =
                    Mathf.RoundToInt(
                        data.depth / Mathf.Sqrt(2f)
                    );

                realDepth =
                    Mathf.Max(1, realDepth);
            }

            for (int d = 1; d <= realDepth; d++)
            {
                int spread = d;

                Vector2Int center =
                    unit.position + forward * d;

                for (int w = -spread; w <= spread; w++)
                {
                    Vector2Int tile =
                        center + right * w;

                    result.Add(tile);

                    if (diagonal)
                    {
                        result.Add(
                            new Vector2Int(
                                tile.x - forward.x,
                                tile.y
                            )
                        );

                        result.Add(
                            new Vector2Int(
                                tile.x,
                                tile.y - forward.y
                            )
                        );
                    }
                }
            }
        }

        // =========================
        // CIRCLE
        // =========================
        else if (data.shape == ThreatShape.CIRCLE)
        {
            for (int x = -data.range; x <= data.range; x++)
            {
                for (int y = -data.range; y <= data.range; y++)
                {
                    Vector2Int p =
                        unit.position +
                        new Vector2Int(x, y);

                    if (
                        Vector2Int.Distance(
                            unit.position,
                            p
                        ) <= data.range
                    )
                    {
                        result.Add(p);
                    }
                }
            }
        }

        // 중복 제거
        result =
            new List<Vector2Int>(
                new HashSet<Vector2Int>(result)
            );

        return result;
    }

    public static Dir GetDirection8(Vector2Int diff)
    {
        if (diff == Vector2Int.zero)
            return Dir.DOWN;

        int x = diff.x;
        int y = diff.y;

        // 대각선
        if (x > 0 && y > 0)
            return Dir.UP_RIGHT;

        if (x > 0 && y < 0)
            return Dir.DOWN_RIGHT;

        if (x < 0 && y > 0)
            return Dir.UP_LEFT;

        if (x < 0 && y < 0)
            return Dir.DOWN_LEFT;

        // 직선
        if (x > 0)
            return Dir.RIGHT;

        if (x < 0)
            return Dir.LEFT;

        if (y > 0)
            return Dir.UP;

        return Dir.DOWN;
    }

    public static Vector2Int GetRightVector(Vector2Int forward)
    {
        // 직선 방향
        if (forward == Vector2Int.up)
            return Vector2Int.right;

        if (forward == Vector2Int.down)
            return Vector2Int.left;

        if (forward == Vector2Int.right)
            return Vector2Int.down;

        if (forward == Vector2Int.left)
            return Vector2Int.up;

        // 대각 방향
        if (forward == new Vector2Int(1, 1))
            return new Vector2Int(1, -1);

        if (forward == new Vector2Int(1, -1))
            return new Vector2Int(-1, -1);

        if (forward == new Vector2Int(-1, -1))
            return new Vector2Int(-1, 1);

        if (forward == new Vector2Int(-1, 1))
            return new Vector2Int(1, 1);

        return Vector2Int.right;
    }

    public static float GetCompression(Unit unit)
    {
        bool isDiagonal =
            unit.currentDir == Dir.UP_RIGHT ||
            unit.currentDir == Dir.UP_LEFT ||
            unit.currentDir == Dir.DOWN_RIGHT ||
            unit.currentDir == Dir.DOWN_LEFT;

        return isDiagonal ? 0.75f : 1f;
    }

    public static Vector2 ApplyCompression(Vector2Int origin, Vector2Int pos, float compression)
    {
        Vector2 diff = pos - origin;
        return (Vector2)origin + diff * compression;
    }

    public static float ApplyCooldown(Unit unit, float baseCd)
    {
        float reduction = Mathf.Min(50f, unit.cooltimeReduction);
        return baseCd * (1f - reduction / 100f);
    }
}

public class SkillAction_KnightFocusedStab : SkillAction
{
    public SkillAction_KnightFocusedStab()
    {
        SkillName = "집중 찌르기";
    }

    public override bool IsAvailable(Unit unit) => unit.skillCooldowns[3] <= 0f;

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        float expectedDamage = unit.physicalAttack * 1.25f;
        float priority = 70f;

        if (expectedDamage >= target.hp) priority += 20f;
        if (minDist <= 2f) priority += 10f;

        return priority;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        float finalDelayMs =
            Mathf.Max(200f,
            800f * (100f / Mathf.Max(1f, unit.attackspeed)));

        List<Vector2Int> tiles = GetLineTiles(unit, 2);
        float compression = GetCompression(unit);

        BeginAttackCast(
            unit,
            finalDelayMs,
            new ThreatTileData
            {
                shape = ThreatShape.LINE,
                range = 2
            },

            // attack
            () =>
            {
                DamageEnemiesInTilesCompressed(
                    unit,
                    tiles,
                    1.25f,
                    compression
                );

                Debug.Log($"{unit.unitType.typeName} 집중 찌르기");
            },

            // cooldown
            () =>
            {
                unit.skillCooldowns[3] =
                    ApplyCooldown(unit, 8f);
            }
        );
    }
}

public class SkillAction_KnightShieldBash : SkillAction
{
    public SkillAction_KnightShieldBash()
    {
        SkillName = "방패 타격";
    }

    public override bool IsAvailable(Unit unit) => unit.skillCooldowns[2] <= 0f;

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        float expectedDamage = unit.physicalAttack * 0.9f;
        float priority = 55f;

        if (expectedDamage >= target.hp) priority += 20f;
        if (minDist <= 1.5f) priority += 10f;

        return priority;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        float finalDelayMs =
            Mathf.Max(200f,
            650f * (100f / Mathf.Max(1f, unit.attackspeed)));

        List<Vector2Int> tiles = GetLineTiles(unit, 1);
        float compression = GetCompression(unit);

        BeginAttackCast(
            unit,
            finalDelayMs,
            new ThreatTileData
            {
                shape = ThreatShape.LINE,
                range = 1
            },

            // attack
            () =>
            {
                DamageEnemiesInTilesCompressed(
                    unit,
                    tiles,
                    0.9f,
                    compression,
                    true,
                    1f
                );

                Debug.Log($"{unit.unitType.typeName} 방패 타격");
            },

            // cooldown
            () =>
            {
                unit.skillCooldowns[2] =
                    ApplyCooldown(unit, 6f);
            }
        );
    }
}

public class SkillAction_KnightFrontSlash : SkillAction
{
    public SkillAction_KnightFrontSlash()
    {
        SkillName = "전방 베기";
    }

    public override bool IsAvailable(Unit unit) => unit.skillCooldowns[0] <= 0f;

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        float expectedDamage = unit.physicalAttack;
        float priority = 40f;

        if (expectedDamage >= target.hp) priority += 20f;
        if (minDist <= 1.5f) priority += 10f;

        return priority;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        float finalDelayMs =
            Mathf.Max(200f,
            450f * (100f / Mathf.Max(1f, unit.attackspeed)));

        List<Vector2Int> tiles = GetLineTiles(unit, 1);
        float compression = GetCompression(unit);

        BeginAttackCast(
            unit,
            finalDelayMs,
            new ThreatTileData
            {
                shape = ThreatShape.LINE,
                range = 1
            },

            // attack
            () =>
            {
                DamageEnemiesInTilesCompressed(
                    unit,
                    tiles,
                    1f,
                    compression
                );

                Debug.Log($"{unit.unitType.typeName} 전방 베기");
            },

            // cooldown
            () =>
            {
                unit.skillCooldowns[0] =
                    ApplyCooldown(unit, 1.2f);
            }
        );
    }
}

public class SkillAction_MeleeTankHeavySmash : SkillAction
{
    public SkillAction_MeleeTankHeavySmash()
    {
        SkillName = "육중한 내리찍기";
    }

    public override bool IsAvailable(Unit unit) => unit.skillCooldowns[3] <= 0f;

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        float expectedDamage = unit.physicalAttack * 1.45f;
        float priority = 75f;

        if (expectedDamage >= target.hp) priority += 20f;
        if (minDist <= 2.5f) priority += 10f;

        return priority;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        float finalDelayMs =
            Mathf.Max(200f,
            1000f * (100f / Mathf.Max(1f, unit.attackspeed)));

        List<Vector2Int> tiles =
            GetFrontAreaTiles(unit, 2, 2);

        float compression =
            GetCompression(unit);

        BeginAttackCast(
            unit,
            finalDelayMs,
            new ThreatTileData
            {
                shape = ThreatShape.RECT,
                width = 2,
                depth = 2
            },

            // attack
            () =>
            {
                DamageEnemiesInTilesCompressed(
                    unit,
                    tiles,
                    1.45f,
                    compression
                );

                Debug.Log($"{unit.unitType.typeName} 육중한 내리찍기");
            },

            // cooldown
            () =>
            {
                unit.skillCooldowns[3] =
                    ApplyCooldown(unit, 7f);
            }
        );
    }
}

public class SkillAction_MeleeTankAmbushClaw : SkillAction
{
    public SkillAction_MeleeTankAmbushClaw()
    {
        SkillName = "급습 할퀴기";
    }

    public override bool IsAvailable(Unit unit) => unit.skillCooldowns[2] <= 0f;

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        float expectedDamage = unit.physicalAttack * 0.65f;
        float priority = 60f;

        if (expectedDamage >= target.hp) priority += 20f;
        if (minDist <= 1.5f) priority += 10f;

        return priority;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        float finalDelayMs =
            Mathf.Max(200f,
            240f * (100f / Mathf.Max(1f, unit.attackspeed)));

        List<Vector2Int> tiles =
            GetLineTiles(unit, 1);

        float compression =
            GetCompression(unit);

        BeginAttackCast(
            unit,
            finalDelayMs,
            new ThreatTileData
            {
                shape = ThreatShape.LINE,
                range = 1
            },

            // attack
            () =>
            {
                DamageEnemiesInTilesCompressed(
                    unit,
                    tiles,
                    0.65f,
                    compression
                );

                Debug.Log($"{unit.unitType.typeName} 급습 할퀴기");
            },

            // cooldown
            () =>
            {
                unit.skillCooldowns[2] =
                    ApplyCooldown(unit, 4f);
            }
        );
    }
}

public class SkillAction_MeleeTankClawSwipe : SkillAction
{
    public SkillAction_MeleeTankClawSwipe()
    {
        SkillName = "발톱 후려치기";
    }

    public override bool IsAvailable(Unit unit) => unit.skillCooldowns[1] <= 0f;

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        float expectedDamage = unit.physicalAttack;
        float priority = 50f;

        if (expectedDamage >= target.hp) priority += 20f;
        if (minDist <= 3f) priority += 10f;

        return priority;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        float finalDelayMs =
            Mathf.Max(200f,
            650f * (100f / Mathf.Max(1f, unit.attackspeed)));

        List<Vector2Int> tiles =
            GetLineTiles(unit, 3);

        float compression =
            GetCompression(unit);

        BeginAttackCast(
            unit,
            finalDelayMs,
            new ThreatTileData
            {
                shape = ThreatShape.LINE,
                range = 3
            },

            // attack
            () =>
            {
                DamageEnemiesInTilesCompressed(
                    unit,
                    tiles,
                    1f,
                    compression
                );

                Debug.Log($"{unit.unitType.typeName} 발톱 후려치기");
            },

            // cooldown
            () =>
            {
                unit.skillCooldowns[1] =
                    ApplyCooldown(unit, 1.4f);
            }
        );
    }
}
