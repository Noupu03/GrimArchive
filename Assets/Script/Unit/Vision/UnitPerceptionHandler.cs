using UnityEngine;

public class UnitPerceptionHandler : IVisionTileHandler
{
    public void Handle(Unit observer, IVisionContext context, Vector3Int tile, bool inPerceptionRange, float dist, Chunks chunk, Tile tileData)
    {
        if (context.Session != null && context.Session.unitGrid.TryGetValue(tile, out Unit unit))
        {
            if (unit != null && unit != observer && unit.Health.hp > 0)
            {
                bool isEnemy = observer.IsEnemy(unit);
                if (isEnemy)
                {
                    if (inPerceptionRange)
                    {
                        PerceptionOutcome outcome = context.ResolveReachedTarget(unit, unit.GetFinalVisibility(), tile, dist, out bool firstTouch);

                        if (outcome == PerceptionOutcome.AccuratePerception)
                        {
                            context.AddPersonalSpottedEnemy(unit);

                            if (observer is Human human && observer.Knowledge != null)
                            {
                                float danger = observer.Knowledge.GetPersonalDanger(human, unit);
                                float interest = observer.Knowledge.GetPersonalInterest(human, unit);
                                human.Memory.personalMap.ObserveMonster(unit.name, tile, danger, interest);

                                human.Memory.personalMap.ObserveUnitInRoom(chunk.roomId, false, unit.name, danger, interest);
                                if (unit.unitType != null && unit.unitType.isBoss)
                                    human.Memory.personalMap.ConfirmBossRoom(chunk.roomId);

                                // 04번 문서 4장: 이 종을 정확 인지한 시점에 공격범위(보유 스킬 중 최대
                                // HitRange)를 확인한다 — 회피 이동이 참조할 "아는 범위" 게이트.
                                var enemySkills = unit.Generate?.GetSkills(unit.unitType.typeName);
                                if (enemySkills != null)
                                {
                                    int maxRange = 0;
                                    foreach (var s in enemySkills)
                                        if (s != null && s.HitRange > maxRange) maxRange = s.HitRange;
                                    if (maxRange > 0)
                                        human.Memory.personalMap.ConfirmAttackRange(unit.unitType.typeName, maxRange);
                                }
                            }
                        }
                    }
                    else if (!context.HasReachedPerceptionThisPass(unit) && !context.HasVisionOnlyNonEmptyTile(tile))
                    {
                        context.AddVisionOnlyNonEmptyTile(tile);
                    }
                }
            }
        }
    }
}
