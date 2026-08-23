using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 설계 문서 '3.2 투사체'와 '2. 공격 공통 흐름'에 맞춰 
/// 투사체를 발사하는 스킬 액션입니다.
/// </summary>
public class SkillAction_Projectile : SkillAction
{
    readonly SkillData _d;
    private GameObject _projectilePrefab;
    private GameObject _fallbackPrefab;

    public SkillAction_Projectile(SkillData data, GameObject projectilePrefab = null)
    {
        _d = data;
        SkillName = data.skillName;
        _projectilePrefab = projectilePrefab;
    }

    public override float DefaultBaseDelayMs  => _d.baseDelayMs;
    public override float DefaultBaseCooldown => _d.baseCooldown;

    // 문서 내용: 투사체의 위험 범위는 이동 경로 전체로 정의한다 (너비: 투사체 Collider, 길이: 이동 거리)
    public override ThreatShape HitShape => ThreatShape.RECT;
    public override int HitRange => _d.threatRange;
    public override int HitWidth => _d.threatWidth;
    public override int HitDepth => _d.threatDepth;

    public override bool IsAvailable(Unit unit)
    {
        if (unit == null || unit.CombatState.State.skillCooldowns == null) return false;
        if (_d.cooldownSlot < 0 || _d.cooldownSlot >= unit.CombatState.State.skillCooldowns.Length) return false;
        return unit.CombatState.State.skillCooldowns[_d.cooldownSlot] <= 0f;
    }

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        float p = _d.priorityBase;
        if (target != null && target.Health != null && unit.CombatStat.physicalAttack * _d.priorityKillMultiplier >= target.Health.hp) p += _d.priorityKillBonus;
        if (minDist <= _d.priorityRangeThreshold)                         p += _d.priorityRangeBonus;
        return p;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        var threat = ThreatTileData.Create();
        threat.shape = ThreatShape.RECT;
        
        float finalWidth = _d.threatWidth > 0 ? _d.threatWidth : 1f;
        if (_projectilePrefab != null)
        {
            var sr = _projectilePrefab.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                finalWidth = Mathf.Max(finalWidth, sr.bounds.size.y);
            }
        }
        
        threat.width = Mathf.CeilToInt(finalWidth);
        int maxRange = _d.threatRange > 0 ? _d.threatRange : 15;
        threat.depth = maxRange;

        if (!_d.isPiercing)
        {
            Hitbox maxHitbox = BuildRectHitboxWithAngle(unit, threat.width, maxRange, unit.CombatState.State.currentAttackAngle);
            List<Unit> enemies = GetEnemiesInHitbox(unit, maxHitbox);
            float minHitDist = maxRange;
            
            Vector2 unitCenter = (Vector2)unit.position + new Vector2(unit.unitType.footprint.x, unit.unitType.footprint.y) * 0.5f;
            Vector2 forward = new Vector2(Mathf.Cos(unit.CombatState.State.currentAttackAngle), Mathf.Sin(unit.CombatState.State.currentAttackAngle));

            foreach(var enemy in enemies)
            {
                Hitbox enemyBox = GetUnitHitbox(enemy);
                Vector2 toEnemy = enemyBox.center - unitCenter;
                float dist = Vector2.Dot(toEnemy, forward);
                
                if (dist > 0 && dist < minHitDist)
                {
                    minHitDist = dist;
                }
            }
            threat.depth = Mathf.Max(1, Mathf.CeilToInt(minHitDist));
        }
        else
        {
            threat.depth = maxRange;
        }
        
        threat.hitbox = BuildRectHitboxWithAngle(unit, threat.width, threat.depth, unit.CombatState.State.currentAttackAngle);

        BeginAttackCast(unit, 0f, threat,
            () =>
            {
                FireProjectile(unit, threat.depth);
            },
            () => unit.CombatState.State.skillCooldowns[_d.cooldownSlot] = ApplyCooldown(unit, _d.baseCooldown),
            null,
            null,
            shape: AttackShape.Projectile
        );
    }

    // 프리팹이 지정되지 않은 경우 1회만 생성해 두고 이후엔 풀링 키로 재사용한다.
    private GameObject GetOrCreateFallbackPrefab()
    {
        if (_fallbackPrefab != null) return _fallbackPrefab;
        _fallbackPrefab = new GameObject($"Projectile_{SkillName}_Template");
        var sr = _fallbackPrefab.AddComponent<SpriteRenderer>();
        sr.sprite = CreateFallbackSprite();
        sr.color = Color.yellow;
        sr.sortingOrder = 15;
        _fallbackPrefab.AddComponent<Projectile>();
        UnityEngine.Object.DontDestroyOnLoad(_fallbackPrefab);
        _fallbackPrefab.SetActive(false);
        return _fallbackPrefab;
    }

    private void FireProjectile(Unit attacker, int maxDistance)
    {
        GameObject prefabKey = _projectilePrefab != null ? _projectilePrefab : GetOrCreateFallbackPrefab();
        Projectile proj = Projectile.Spawn(prefabKey);

        // 투사체 자체의 실제 충돌(Hitbox) 크기는 위협 타일 전체가 아닌 투사체 머리 부분의 사이즈입니다.
        Hitbox projectileHitbox = new Hitbox
        {
            center = (Vector2)attacker.position + new Vector2(attacker.unitType.footprint.x, attacker.unitType.footprint.y) * 0.5f,
            size = new Vector2(1f, _d.threatWidth > 0 ? _d.threatWidth : 1f),
            rotation = attacker.CombatState.State.currentAttackAngle * Mathf.Rad2Deg
        };

        Vector2 moveDir = new Vector2(Mathf.Cos(attacker.CombatState.State.currentAttackAngle), Mathf.Sin(attacker.CombatState.State.currentAttackAngle));

        // 투사체 로직 초기화
        proj.Init(attacker, _d, projectileHitbox, moveDir, maxDistance);
    }

    private Sprite CreateFallbackSprite()
    {
        Texture2D texture = new Texture2D(8, 8);
        Color[] pixels = new Color[64];
        for (int i = 0; i < 64; i++) pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 32f);
    }
}
