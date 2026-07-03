using UnityEngine;
using System.Collections.Generic;
using Haare.Util.Logger;

/// <summary>
/// 공격 시 자유로운 각도 회전 로직을 검증하는 스크립트
/// 이 스크립트는 게임 실행 중에 공격 인식 및 방향 설정을 로깅합니다.
/// </summary>
public class AttackAngleTestValidator : MonoBehaviour
{
    private float testInterval = 1.0f;
    private float testTimer = 0f;

    private void Update()
    {
        testTimer += Time.deltaTime;
        if (testTimer < testInterval) return;
        testTimer = 0f;

        ValidateAttackAngleMechanics();
    }

    private void ValidateAttackAngleMechanics()
    {
        if (GameSession.Instance == null) return;

        foreach (var unit in GameSession.Instance.units)
        {
            if (unit == null) continue;
            if (!(unit is UnitFunction uf)) continue;

            // 1. 현재 공격 상태 및 각도 로깅
            if (unit.isCastingAttack)
            {
                LogHelper.Log(LogHelper.GAME, $"[Attack Test] {unit.unitType.typeName}: 공격 중, 각도 = {unit.currentAttackAngle * Mathf.Rad2Deg}°, 현재 시야 방향 = {unit.currentDir}");

                // 2. currentThreat가 정상적으로 설정되었는지 확인
                if (unit.currentThreat != null)
                {
                    LogHelper.Log(LogHelper.GAME, $"[Attack Test] {unit.unitType.typeName}: 위협 타일 생성됨, 히트박스 크기 = {unit.currentThreat.hitbox.size}");
                }
            }

            // 3. 가장 가까운 적 찾기 및 공격 각도 검증
            IEnumerable<Unit> enemies = unit is Human
                ? Unit.humanFactionData.spottedEnemyUnits
                : unit.personalSpottedEnemies;

            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.hp <= 0) continue;
                if (enemy.currentFloor != unit.currentFloor) continue;

                // 목표까지의 거리와 각도 계산
                Vector2 dirToTarget = ((Vector2)enemy.position - (Vector2)unit.position).normalized;
                float expectedAngle = Mathf.Atan2(dirToTarget.y, dirToTarget.x);
                float distance = Vector2Int.Distance(unit.position, enemy.position);

                // 공격 중이고 사거리 내라면
                float attackRange = (unit.Generate != null ? unit.Generate.GetEngageDistance(unit.unitType.typeName, 2) : 2);
                if (unit.isCastingAttack && distance <= attackRange + 1)
                {
                    float angleDiff = Mathf.Abs(unit.currentAttackAngle - expectedAngle);
                    if (angleDiff > Mathf.PI) angleDiff = 2 * Mathf.PI - angleDiff;

                    LogHelper.Log(LogHelper.GAME, $"[Attack Test] {unit.unitType.typeName} → {enemy.unitType.typeName}: " +
                        $"거리={distance:F2}, 공격각도={unit.currentAttackAngle * Mathf.Rad2Deg:F1}°, " +
                        $"예상각도={expectedAngle * Mathf.Rad2Deg:F1}°, 각도차이={angleDiff * Mathf.Rad2Deg:F1}°");
                }
            }

            // 4. 이동이 8방향으로만 제한되는지 검증
            ValidateMovementDirections();
        }
    }

    private void ValidateMovementDirections()
    {
        // 이동이 8방향만 지원되는지 확인하기 위해 GetDirVector 테스트
        if (GameSession.Instance == null || GameSession.Instance.units.Count == 0) return;

        Unit testUnit = GameSession.Instance.units[0];
        if (!(testUnit is UnitFunction)) return;

        int validDirectionCount = 0;
        foreach (Dir dir in System.Enum.GetValues(typeof(Dir)))
        {
            Vector2Int dirVec = testUnit.GetDirVector(dir);
            if (dirVec != Vector2Int.zero)
                validDirectionCount++;
        }

        // 정상적으로 8방향 지원
        if (validDirectionCount == 8)
        {
            // LogHelper.Log(LogHelper.GAME, "[Movement Test] 정상: 이동은 8방향만 지원됩니다.");
        }
        else
        {
            LogHelper.Warning(LogHelper.GAME, $"[Movement Test] 경고: {validDirectionCount}개 방향만 지원됩니다.");
        }
    }

    /// <summary>
    /// 공격 인식 로직 검증: 적이 사거리 범위 내에 있을 때 공격 가능한지 확인
    /// </summary>
    public static void ValidateAttackRecognition()
    {
        if (GameSession.Instance == null) return;

        foreach (var attacker in GameSession.Instance.units)
        {
            if (attacker == null || !(attacker is UnitFunction)) continue;

            List<ThreatTileData> threats = attacker.DetectThreats();

            if (threats.Count > 0)
            {
                LogHelper.Log(LogHelper.GAME, $"[Recognition Test] {attacker.unitType.typeName}: {threats.Count}개의 위협 감지됨");

                // 실제 히트박스 충돌 확인
                foreach (var enemy in GameSession.Instance.units)
                {
                    if (enemy == null || enemy == attacker || enemy.hp <= 0) continue;
                    if (enemy.currentFloor != attacker.currentFloor) continue;

                    Hitbox attackerBox = SkillAction.GetUnitHitbox(attacker);
                    Hitbox enemyBox = SkillAction.GetUnitHitbox(enemy);

                    // 사거리 내에 있는지 확인
                    float distance = Vector2Int.Distance(attacker.position, enemy.position);
                    float attackRange = (attacker.Generate != null ? attacker.Generate.GetEngageDistance(attacker.unitType.typeName, 2) : 2);

                    if (distance <= attackRange)
                    {
                        LogHelper.Log(LogHelper.GAME, $"[Recognition Test] {attacker.unitType.typeName} → {enemy.unitType.typeName}: " +
                            $"사거리({attackRange}) 내에 위치, 거리={distance:F2}");
                    }
                }
            }
        }
    }
}
