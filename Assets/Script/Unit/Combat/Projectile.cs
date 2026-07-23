using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 설계 문서의 '3.2 투사체(Projectile)' 명세에 맞춰 구현된 논리/시각 투사체 클래스입니다.
/// 기존 Hitbox와 SkillAction의 판정 로직을 재활용하여 물리(Collider2D) 없이도
/// 그리드 및 Hitbox 시스템 상에서 일관된 충돌 판정을 수행합니다.
/// </summary>
public class Projectile : MonoBehaviour
{
    // 공격자 정보 및 자체 논리적 Collider(Hitbox)
    private Unit _attacker;
    private SkillData _skillData;
    private Hitbox _logicalCollider;
    private Vector2 _moveDir;
    private Vector2 _startPos;
    private float _maxDistance;
    private bool _isInitialized = false;
    
    // 이미 타격한 대상 기록 (관통 시 중복 타격 방지)
    private HashSet<Unit> _hitTargets = new HashSet<Unit>();

    /// <summary>
    /// 투사체 초기화
    /// </summary>
    public void Init(Unit attacker, SkillData skillData, Hitbox initialHitbox, Vector2 moveDir, float maxDistance)
    {
        this._attacker = attacker;
        this._skillData = skillData;
        this._logicalCollider = initialHitbox;
        this._moveDir = moveDir.normalized;
        this._maxDistance = maxDistance;
        
        this._startPos = initialHitbox.center;
        this._hitTargets.Clear();
        
        // 투사체의 시각적 위치 및 회전(방향) 동기화
        transform.position = GetVisualPosition(_logicalCollider.center);
        
        float angle = Mathf.Atan2(this._moveDir.y, this._moveDir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        
        _isInitialized = true;
    }

    void Update()
    {
        if (!_isInitialized || _skillData == null) return;

        // 투사체를 쏜 공격자가 비행 중에 죽으면(UnityEngine.Object.Destroy) _attacker는 "파괴된 참조"가
        // 되어 Unity의 == null 오버라이드로 이 시점부터 항상 true를 반환한다. 예전에는 이 조건에서 그냥
        // return만 하고 끝내서 투사체가 이동/충돌/소멸 어느 것도 하지 못한 채 허공에 영원히 멈춰
        // 있었다(재발한 버그) — 공격자가 사라지면 추가 피해 계산 자체가 불가능하므로 투사체도 즉시
        // 소멸시킨다.
        if (_attacker == null)
        {
            DestroyProjectile();
            return;
        }

        float currentSpeed = _skillData.projectileSpeed > 0f ? _skillData.projectileSpeed : 10f; // 기본값 방어

        // 1. 투사체 이동 (논리 Hitbox 위치 갱신)
        _logicalCollider.center += _moveDir * currentSpeed * Time.deltaTime;

        // 2. 시각적 위치 동기화
        transform.position = GetVisualPosition(_logicalCollider.center);

        // 3. 충돌 판정 (기존 기능 재활용: SkillAction.GetEnemiesInHitbox)
        List<Unit> hitEnemies = SkillAction.GetEnemiesInHitbox(_attacker, _logicalCollider);
        
        bool hasHitNewEnemy = false;

        foreach(var enemy in hitEnemies)
        {
            if (_hitTargets.Contains(enemy)) continue; // 이미 타격한 적은 무시

            Hitbox enemyBox = SkillAction.GetUnitHitbox(enemy);
            float overlapRatio = _logicalCollider.CalculateOverlapRatio(enemyBox);
            float finalRatio = Mathf.Max(0.2f, overlapRatio);

            ApplyHitEffect(enemy, finalRatio);

            _hitTargets.Add(enemy);
            hasHitNewEnemy = true;
        }

        if (hasHitNewEnemy)
        {
            // 관통(Pierce) 옵션이 꺼져있다면 즉시 투사체 소멸
            if (!_skillData.isPiercing)
            {
                DestroyProjectile();
                return;
            }
        }

        // 4. 최대 사거리 도달 시 소멸
        if (Vector2.Distance(_startPos, _logicalCollider.center) > _maxDistance)
        {
            DestroyProjectile();
        }
    }

    private void ApplyHitEffect(Unit enemy, float overlapRatio)
    {
        // 1. 데미지 계산 및 적용 (점유율 보정)
        float baseDamage = Mathf.Max(1f, _attacker.GetComponent<CombatStatComponent>().physicalAttack * _skillData.damageMultiplier);
        float finalDamage = Mathf.Max(1f, baseDamage * overlapRatio);
        enemy.TakePhysicalDamage(finalDamage, _attacker);
        
        // 투사체가 실제로 데미지를 입혔음을 확인하기 위한 로그 추가
        Haare.Util.Logger.LogHelper.Log(Haare.Util.Logger.LogHelper.GAME, 
            $"[Projectile Hit] {_skillData.skillName} 투사체가 {enemy.unitType.typeName}에게 적중! 데미지: {finalDamage:F1}");

        // 2. 상태 이상 적용
        if (_skillData.hasStun) 
        {
            enemy.ApplyStun(_skillData.stunDuration);
        }

        // 3. 피격 이펙트(VFX) 스폰
        if (_skillData.hitEffectPrefab != null && _attacker.VFX != null)
        {
            // Hit 위치를 투사체 현재 위치와 적 위치의 중간쯤으로 잡거나 적 위치로 잡음
            Vector3 hitPos = GetVisualPosition(_logicalCollider.center);
            
            // 임시로 프리팹 인스턴스화 후 파괴 로직 추가 (VFX 매니저가 안해준다면)
            GameObject fx = Instantiate(_skillData.hitEffectPrefab, hitPos, Quaternion.identity);
            Destroy(fx, 1.5f); // 1.5초 후 자동 삭제 (이펙트 시스템에 맞춰 조정 필요)
        }
    }

    private void DestroyProjectile()
    {
        // TODO: 파괴 시 터지는 전역 VFX 처리 추가 가능
        Destroy(gameObject);
    }

    /// <summary>
    /// 논리 좌표를 실제 월드(비주얼) 좌표로 변환합니다. 
    /// (층(Floor)에 따른 Offset 적용)
    /// </summary>
    private Vector3 GetVisualPosition(Vector2 logicalPos)
    {
        Vector3 pos = new Vector3(logicalPos.x, logicalPos.y, 0f);
        
        // 공격자의 현재 층(Floor) 오프셋을 더해 시각적으로 맞춤
        if (_attacker != null && _attacker.Generate != null)
        {
            pos += _attacker.Generate.GetFloorOffset(_attacker.currentFloor);
        }
        
        return pos;
    }

#if UNITY_EDITOR
    // 에디터에서 투사체의 논리적 Collider(Hitbox) 형태를 디버그선으로 그려줍니다.
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !_isInitialized) return;

        Gizmos.color = Color.magenta;
        Vector2 center = _logicalCollider.center;
        Vector2 halfSize = _logicalCollider.size * 0.5f;

        Vector3 floorOffset = _attacker != null && _attacker.Generate != null ? 
                              _attacker.Generate.GetFloorOffset(_attacker.currentFloor) : Vector3.zero;

        Vector3 p1 = new Vector3(center.x - halfSize.x, center.y - halfSize.y, 0f) + floorOffset;
        Vector3 p2 = new Vector3(center.x + halfSize.x, center.y - halfSize.y, 0f) + floorOffset;
        Vector3 p3 = new Vector3(center.x + halfSize.x, center.y + halfSize.y, 0f) + floorOffset;
        Vector3 p4 = new Vector3(center.x - halfSize.x, center.y + halfSize.y, 0f) + floorOffset;

        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p4);
        Gizmos.DrawLine(p4, p1);
    }
#endif
}
