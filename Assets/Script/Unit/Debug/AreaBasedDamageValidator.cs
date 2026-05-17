using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 피격 면적 기반 데미지 시스템 검증 스크립트
/// 공격 히트박스와 피격 대상의 교차 면적 비율과 데미지 적용을 검증합니다.
/// </summary>
public class AreaBasedDamageValidator : MonoBehaviour
{
    private float testInterval = 2.0f;
    private float testTimer = 0f;
    private bool showLogging = true; // 로깅 활성화/비활성화

    private void Update()
    {
        testTimer += Time.deltaTime;
        if (testTimer < testInterval) return;
        testTimer = 0f;

        if (showLogging)
            ValidateAreaBasedDamage();
    }

    private void ValidateAreaBasedDamage()
    {
        if (GameSession.Instance == null) return;

        foreach (var attacker in GameSession.Instance.units)
        {
            if (attacker == null || !(attacker is UnitFunction)) continue;
            if (!attacker.isCastingAttack || attacker.currentThreat == null) continue;

            // 현재 공격의 히트박스 정보
            Hitbox attackHitbox = attacker.currentThreat.hitbox;
            float attackArea = attackHitbox.size.x * attackHitbox.size.y;

            Debug.Log($"\n[Area Damage Test] {attacker.unitType.typeName} 공격 중");
            Debug.Log($"  공격 히트박스 크기: {attackHitbox.size.x} x {attackHitbox.size.y} = {attackArea:F2} 면적");
            Debug.Log($"  공격 위치: ({attackHitbox.center.x:F1}, {attackHitbox.center.y:F1})");

            // 각 적에 대해 교차 면적과 데미지 비율 계산
            foreach (var enemy in GameSession.Instance.units)
            {
                if (enemy == null || enemy == attacker || enemy.hp <= 0) continue;
                if (enemy.currentFloor != attacker.currentFloor) continue;

                Hitbox enemyHitbox = SkillAction.GetUnitHitbox(enemy);
                float enemyArea = enemyHitbox.size.x * enemyHitbox.size.y;
                float overlapArea = attackHitbox.CalculateOverlapArea(enemyHitbox);
                float overlapRatio = attackHitbox.CalculateOverlapRatio(enemyHitbox);

                if (overlapRatio > 0)
                {
                    Debug.Log($"  → {enemy.unitType.typeName}:");
                    Debug.Log($"     적 히트박스: {enemyHitbox.size.x} x {enemyHitbox.size.y} = {enemyArea:F2} 면적");
                    Debug.Log($"     교차 면적: {overlapArea:F2}");
                    Debug.Log($"     교차 비율: {overlapRatio:P0}");

                    // 예상 데미지 계산
                    float baseDamage = attacker.physicalAttack;
                    float expectedDamage = Mathf.Max(1f, baseDamage * Mathf.Max(0.1f, overlapRatio));
                    Debug.Log($"     예상 데미지: {expectedDamage:F1} (기본 데미지: {baseDamage:F1})");
                }
            }
        }
    }

    /// <summary>
    /// 두 히트박스의 교차 면적을 시각적으로 테스트합니다.
    /// </summary>
    public static void TestOverlapCalculation()
    {
        // 테스트 히트박스 1: (3, 3) 크기, 중심 (0, 0)
        Hitbox box1 = new Hitbox
        {
            center = new Vector2(0, 0),
            size = new Vector2(3, 3),
            rotation = 0f
        };

        // 테스트 히트박스 2: (2, 2) 크기, 중심 (1.5f, 1.5f)
        Hitbox box2 = new Hitbox
        {
            center = new Vector2(1.5f, 1.5f),
            size = new Vector2(2, 2),
            rotation = 0f
        };

        float overlapArea = box1.CalculateOverlapArea(box2);
        float overlapRatio = box1.CalculateOverlapRatio(box2);

        Debug.Log($"[Overlap Test] 교차 면적: {overlapArea:F2}, 비율: {overlapRatio:P0}");
        // 예상: 교차 면적 = 1.5 x 1.5 = 2.25, 비율 = 2.25 / 9 = 25%
    }

    /// <summary>
    /// 부분 피격과 완전 피격의 데미지 차이를 검증합니다.
    /// </summary>
    public static void TestPartialVsFullHit()
    {
        // 공격 히트박스 (4 x 4 = 16 면적)
        Hitbox attackBox = new Hitbox
        {
            center = new Vector2(0, 0),
            size = new Vector2(4, 4),
            rotation = 0f
        };

        // 적 1: 완전 피격 (4 x 4)
        Hitbox fullHitEnemy = new Hitbox
        {
            center = new Vector2(0, 0),
            size = new Vector2(4, 4),
            rotation = 0f
        };

        // 적 2: 부분 피격 (2 x 2)
        Hitbox partialHitEnemy = new Hitbox
        {
            center = new Vector2(3, 3),
            size = new Vector2(2, 2),
            rotation = 0f
        };

        float fullRatio = attackBox.CalculateOverlapRatio(fullHitEnemy);
        float partialRatio = attackBox.CalculateOverlapRatio(partialHitEnemy);

        Debug.Log($"[Partial vs Full Hit Test]");
        Debug.Log($"  완전 피격 비율: {fullRatio:P0}");
        Debug.Log($"  부분 피격 비율: {partialRatio:P0}");
        Debug.Log($"  배수 차이: {(fullRatio / partialRatio):F2}배");
    }
}
