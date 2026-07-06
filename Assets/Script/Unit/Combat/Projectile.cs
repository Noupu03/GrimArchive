using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 설계 문서의 '3.2 투사체(Projectile)' 명세에 맞춰 구현된 논리/시각 투사체 클래스입니다.
/// 기존 Hitbox와 SkillAction의 판정 로직을 재활용하여 물리(Collider2D) 없이도
/// 그리드 및 Hitbox 시스템 상에서 일관된 충돌 판정을 수행합니다.
/// </summary>
public class Projectile : MonoBehaviour
{
    [Header("투사체 정보")]
    public float speed = 5f;
    public float maxDistance = 15f;
    public float damageMultiplier = 1f;

    [Header("상태 이상 옵션 (Attack Modifier 확장용)")]
    public bool hasStun = false;
    public float stunDuration = 0f;

    [Header("공격 옵션 (Attack Modifier)")]
    public bool isPiercing = false; // 관통 여부
    
    // 공격자 정보 및 자체 논리적 Collider(Hitbox)
    private Unit _attacker;
    private Hitbox _logicalCollider;
    private Vector2 _moveDir;
    private Vector2 _startPos;
    private bool _isInitialized = false;
    
    // 이미 타격한 대상 기록 (관통 시 중복 타격 방지)
    private HashSet<Unit> _hitTargets = new HashSet<Unit>();

    /// <summary>
    /// 투사체 초기화
    /// </summary>
    /// <param name="attacker">공격자(Unit)</param>
    /// <param name="initialHitbox">시작 위치와 크기를 가진 Hitbox</param>
    /// <param name="moveDir">날아갈 방향 (정규화된 벡터 권장)</param>
    public void Init(Unit attacker, Hitbox initialHitbox, Vector2 moveDir, float speed, float damageMultiplier)
    {
        this._attacker = attacker;
        this._logicalCollider = initialHitbox;
        this._moveDir = moveDir.normalized;
        this.speed = speed;
        this.damageMultiplier = damageMultiplier;
        
        this._startPos = initialHitbox.center;
        this._hitTargets.Clear();
        
        // 투사체의 시각적 위치 동기화
        transform.position = GetVisualPosition(_logicalCollider.center);
        
        _isInitialized = true;
    }

    void Update()
    {
        if (!_isInitialized || _attacker == null) return;

        // 1. 투사체 이동 (논리 Hitbox 위치 갱신)
        _logicalCollider.center += _moveDir * speed * Time.deltaTime;

        // 2. 시각적 위치 동기화
        transform.position = GetVisualPosition(_logicalCollider.center);

        // 3. 충돌 판정 (기존 기능 재활용: SkillAction.GetEnemiesInHitbox)
        // 매 프레임 해당 Hitbox에 적이 닿았는지 검사합니다. (Collider 역활)
        List<Unit> hitEnemies = SkillAction.GetEnemiesInHitbox(_attacker, _logicalCollider);
        
        bool hasHitNewEnemy = false;

        foreach(var enemy in hitEnemies)
        {
            if (_hitTargets.Contains(enemy)) continue; // 이미 타격한 적은 무시

            // 충돌 시 적에게 데미지 입히기
            // 현재 Hitbox 시스템상, Projectile의 크기가 매우 작다면 AreaRatio가 낮게 나올 수 있으므로 
            // 100% 데미지를 주고싶다면 multiplier 조정이나 개별 Damage 메서드를 호출할 수도 있음
            float finalDamage = Mathf.Max(1f, _attacker.physicalAttack * damageMultiplier);
            enemy.TakePhysicalDamage(finalDamage, _attacker);
            if (hasStun) enemy.ApplyStun(stunDuration);

            _hitTargets.Add(enemy);
            hasHitNewEnemy = true;
        }

        if (hasHitNewEnemy)
        {
            // TODO: 폭발(Explosion) 등의 옵션이 확장될 경우 이 부분에서 분기 처리
            
            // 관통(Pierce) 옵션이 꺼져있다면 즉시 투사체 소멸
            if (!isPiercing)
            {
                DestroyProjectile();
                return;
            }
        }

        // 4. 최대 사거리 도달 시 소멸
        if (Vector2.Distance(_startPos, _logicalCollider.center) > maxDistance)
        {
            DestroyProjectile();
        }
    }

    private void DestroyProjectile()
    {
        // TODO: 파괴 시 터지는 VFX 처리 추가 가능
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
