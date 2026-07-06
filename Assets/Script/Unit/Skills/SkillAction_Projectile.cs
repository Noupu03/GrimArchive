using UnityEngine;
using Haare.Util.Logger;
using System.Collections.Generic;

/// <summary>
/// 설계 문서 '3.2 투사체'와 '2. 공격 공통 흐름'에 맞춰 
/// 투사체를 발사하는 스킬 액션입니다.
/// </summary>
public class SkillAction_Projectile : SkillAction
{
    readonly SkillData _d;
    private GameObject _projectilePrefab;

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

    public override bool IsAvailable(Unit unit) => unit.skillCooldowns[_d.cooldownSlot] <= 0f;

    public override float GetPriority(Unit unit, Unit target, float minDist)
    {
        float p = _d.priorityBase;
        if (unit.physicalAttack * _d.priorityKillMultiplier >= target.hp) p += _d.priorityKillBonus;
        if (minDist <= _d.priorityRangeThreshold)                         p += _d.priorityRangeBonus;
        return p;
    }

    public override void Execute(Unit unit, Unit target, float minDist)
    {
        float finalDelayMs = Mathf.Max(200f, _d.baseDelayMs * (100f / Mathf.Max(1f, unit.attackspeed)));

        // 1. 공격 범위(위험 범위) 생성
        // 문서: 투사체의 위험 범위는 투사체의 너비와 날아갈 최대 사거리(이동 경로 전체)를 합친 형태가 되어야 함.
        var threat = ThreatTileData.Create();
        threat.shape = ThreatShape.RECT;
        
        // 투사체가 날아갈 경로 전체를 위협 타일로 지정합니다. 
        // 폭(Width)은 투사체의 두께, 깊이(Depth)는 투사체의 최대 사거리를 의미합니다.
        threat.width = _d.threatWidth > 0 ? _d.threatWidth : 1; 
        threat.depth = _d.threatRange > 0 ? _d.threatRange : 15; // 사거리

        // 2. 선딜 (BeginAttackCast) 시작
        BeginAttackCast(unit, finalDelayMs, threat,
            () =>
            {
                // 3. 공격 실행 (선딜 종료 후 투사체 실제 발사)
                FireProjectile(unit, threat.depth);
                LogHelper.Log(LogHelper.GAME, $"{unit.unitType.typeName} 발사: {SkillName}");
            },
            () => unit.skillCooldowns[_d.cooldownSlot] = ApplyCooldown(unit, _d.baseCooldown)
        );
    }

    private void FireProjectile(Unit attacker, int maxDistance)
    {
        // 투사체 게임 오브젝트 생성
        GameObject projObj;
        if (_projectilePrefab != null)
        {
            projObj = Object.Instantiate(_projectilePrefab);
        }
        else
        {
            projObj = new GameObject($"Projectile_{SkillName}");
            // 폴백 비주얼 (임시)
            var sr = projObj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateFallbackSprite();
            sr.color = Color.yellow;
            sr.sortingOrder = 15;
        }

        Projectile proj = projObj.GetComponent<Projectile>();
        if (proj == null) proj = projObj.AddComponent<Projectile>();

        // 투사체 자체의 실제 충돌(Hitbox) 크기는 위협 타일 전체가 아닌 투사체 머리 부분의 사이즈입니다.
        Hitbox projectileHitbox = new Hitbox
        {
            center = (Vector2)attacker.position + new Vector2(attacker.unitType.footprint.x, attacker.unitType.footprint.y) * 0.5f,
            size = new Vector2(_d.threatWidth > 0 ? _d.threatWidth : 1, 1),
            rotation = attacker.currentAttackAngle * Mathf.Rad2Deg
        };

        Vector2 moveDir = new Vector2(Mathf.Cos(attacker.currentAttackAngle), Mathf.Sin(attacker.currentAttackAngle));

        // 속도는 임의로 10f로 설정. (추후 SkillData 등에 projectileSpeed를 확장 가능)
        float projectileSpeed = 10f; 
        
        // 투사체 로직 초기화
        proj.Init(attacker, projectileHitbox, moveDir, projectileSpeed, _d.damageMultiplier);
        proj.maxDistance = maxDistance;
        proj.hasStun = _d.hasStun;
        proj.stunDuration = _d.stunDuration;
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
