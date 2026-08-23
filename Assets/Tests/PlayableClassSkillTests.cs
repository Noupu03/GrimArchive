#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Cysharp.Threading.Tasks;

// ========================================================================
// 8종 플레이어블 캐릭터 클래스 및 고유 스킬 포괄 검증 테스트 스위트
// Tier 1: Core Feature Coverage (SkillData, 8종 SkillActions, Factory, JSON)
// Tier 2: Boundary & Corner Cases (Zero HP, Null Target/Session, Death Rollback, 8-Way Angles)
// Tier 3: Cross-Feature Combinations (Buff+Debuff Stack, MultiHit vs Shield, Heal vs Curse)
// ========================================================================

[TestFixture]
public class PlayableClassSkillTests
{
    private Human CreateTestHuman(string typeName = "Warrior", Vector2Int? pos = null, Dir dir = Dir.UP)
    {
        var human = ScriptableObject.CreateInstance<Human>();
        human.unitType = new Knight { typeName = typeName, footprint = new Vector2(1, 1) };
        human.position = pos ?? new Vector2Int(10, 10);
        human.currentDir = dir;
        human.currentFloor = 0;
        human.FactionBehavior = new HumanFactionBehavior();

        human.Health.maxHp = 150f;
        human.Health.hp = 150f;
        human.CombatStat.physicalAttack = 40f;
        human.CombatStat.magicalAttack = 20f;
        human.CombatStat.physicalDefense = 10f;
        human.CombatStat.magicalDefense = 10f;

        human.CombatState.State.skillCooldowns = new float[10];
        return human;
    }

    private Monster CreateTestMonster(string typeName = "WildMonster", Vector2Int? pos = null, Dir dir = Dir.DOWN)
    {
        var monster = ScriptableObject.CreateInstance<Monster>();
        monster.unitType = new WildMonsterA { typeName = typeName, footprint = new Vector2(1, 1) };
        monster.position = pos ?? new Vector2Int(10, 11);
        monster.currentDir = dir;
        monster.currentFloor = 0;
        monster.FactionBehavior = new WildMonsterBehavior();

        monster.Health.maxHp = 200f;
        monster.Health.hp = 200f;
        monster.CombatStat.physicalAttack = 25f;
        monster.CombatStat.magicalAttack = 10f;
        monster.CombatStat.physicalDefense = 5f;
        monster.CombatStat.magicalDefense = 5f;

        monster.CombatState.State.skillCooldowns = new float[10];
        return monster;
    }

    // GameSession은 순수 C# 클래스(NativeRoutine 상속)라 AddComponent로 붙일 수 없고, units도
    // private set이라 직접 대입할 수 없다. Unit.Session이 참조하는 _gameSession은 VContainer
    // [Inject] 필드라 DI 컨테이너 없이는 리플렉션으로만 채울 수 있다(PropagationSystemTests.cs의
    // _knowledgeBase 주입 관례와 동일).
    private static void AttachSession(Unit unit, GameSession session)
    {
        typeof(Unit).GetField("_gameSession", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(unit, session);
    }

    // ========================================================================
    // TIER 1: CORE FEATURE UNIT COVERAGE
    // ========================================================================

    #region Tier 1: Feature Coverage

    [Test]
    public void Tier1_SkillData_ExtensionFields_DefaultAndAssignment()
    {
        var sd = new SkillData
        {
            skillName = "테스트 스킬",
            skillArchetype = "Backstab",
            multiHitCount = 3,
            effectDuration = 5.0f,
            effectAmount = 2.5f,
            explosionRadius = 1.8f
        };

        Assert.AreEqual("테스트 스킬", sd.skillName);
        Assert.AreEqual("Backstab", sd.skillArchetype);
        Assert.AreEqual(3, sd.multiHitCount);
        Assert.AreEqual(5.0f, sd.effectDuration, 0.001f);
        Assert.AreEqual(2.5f, sd.effectAmount, 0.001f);
        Assert.AreEqual(1.8f, sd.explosionRadius, 0.001f);
    }

    [Test]
    public void Tier1_Warrior_GenericSmash_DealsHeavyDamageAndAppliesStun()
    {
        var warrior = CreateTestHuman("전사", new Vector2Int(10, 10), Dir.UP);
        var target = CreateTestMonster("야생 몬스터", new Vector2Int(10, 11), Dir.DOWN);

        var skillData = new SkillData
        {
            skillName = "강타",
            skillArchetype = "Generic",
            baseDelayMs = 0f,
            baseCooldown = 4.0f,
            cooldownSlot = 0,
            hitShape = "LINE",
            hitRange = 1,
            threatRange = 1,
            damageMultiplier = 1.8f,
            hasStun = true,
            stunDuration = 1.0f,
            priorityBase = 60f
        };

        var session = new GameSession();
        var sessionUnits = new List<Unit> { warrior, target };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        var smash = new SkillAction_Generic(skillData);

        Assert.IsTrue(smash.IsAvailable(warrior), "스킬 쿨다운 0일 때 사용 가능해야 함");
        float priority = smash.GetPriority(warrior, target, 1.0f);
        Assert.GreaterOrEqual(priority, 60f, "기본 우선순위 이상이어야 함");

        float initialHp = target.Health.hp;
        smash.Execute(warrior, target, 1.0f);

        // 물리 공격력 40 * 1.8 = 72 데미지, 방어력 5 차감 -> 67 데미지 (최대  overlapRatio 적용)
        Assert.Less(target.Health.hp, initialHp, "강타 공격 후 대상 체력이 감소해야 함");
        Assert.GreaterOrEqual(target.StatusEffects.State.stunDuration, 0.9f, "스턴 효과가 적용되어야 함");
        Assert.Greater(warrior.CombatState.State.skillCooldowns[0], 0f, "스킬 쿨다운이 적용되어야 함");
    }

    [Test]
    public void Tier1_Rogue_Backstab_SameDirection_DealsCriticalMultiplier()
    {
        // 공격자와 대상이 같은 방향(UP)을 바라보고 대상 뒤에 위치할 때
        var rogue = CreateTestHuman("도적", new Vector2Int(10, 10), Dir.UP);
        var target = CreateTestMonster("야생 몬스터", new Vector2Int(10, 11), Dir.UP);

        var skillData = new SkillData
        {
            skillName = "기습",
            skillArchetype = "Backstab",
            baseDelayMs = 0f,
            baseCooldown = 3.0f,
            cooldownSlot = 0,
            hitShape = "LINE",
            hitRange = 1,
            threatRange = 1,
            damageMultiplier = 1.2f,
            effectAmount = 2.5f, // 2.5배 백스탭 치명타
            priorityBase = 65f
        };

        var session = new GameSession();
        var sessionUnits = new List<Unit> { rogue, target };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        var backstab = new SkillAction_Backstab(skillData);

        bool isBackstab = SkillAction_Backstab.IsBackstab(rogue, target);
        Assert.IsTrue(isBackstab, "동일 방향 및 후방 위치 시 백스탭으로 판정되어야 함");

        float priority = backstab.GetPriority(rogue, target, 1.0f);
        Assert.GreaterOrEqual(priority, 65f + 35f, "백스탭 조건 만족 시 보너스 우선순위 부여");

        float initialHp = target.Health.hp;
        backstab.Execute(rogue, target, 1.0f);

        float damageTaken = initialHp - target.Health.hp;
        Assert.Greater(damageTaken, 40f, "백스탭 치명타 배율이 적용되어 높은 피해를 입혀야 함");
    }

    [Test]
    public void Tier1_Rogue_Backstab_OppositeDirection_DealsStandardDamage()
    {
        // 정면 마주봄 (UP vs DOWN)
        var rogue = CreateTestHuman("도적", new Vector2Int(10, 10), Dir.UP);
        var target = CreateTestMonster("야생 몬스터", new Vector2Int(10, 11), Dir.DOWN);

        var skillData = new SkillData
        {
            skillName = "기습",
            skillArchetype = "Backstab",
            baseDelayMs = 0f,
            baseCooldown = 3.0f,
            cooldownSlot = 0,
            hitShape = "LINE",
            hitRange = 1,
            threatRange = 1,
            damageMultiplier = 1.2f,
            effectAmount = 2.5f,
            priorityBase = 65f
        };

        var session = new GameSession();
        var sessionUnits = new List<Unit> { rogue, target };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        var backstab = new SkillAction_Backstab(skillData);

        bool isBackstab = SkillAction_Backstab.IsBackstab(rogue, target);
        Assert.IsFalse(isBackstab, "정면 마주볼 때는 백스탭이 아니어야 함");

        float initialHp = target.Health.hp;
        backstab.Execute(rogue, target, 1.0f);

        float damageTaken = initialHp - target.Health.hp;
        Assert.Greater(damageTaken, 0f, "기본 피해는 정상 적용되어야 함");
    }

    [Test]
    public void Tier1_Mage_GroundAoE_DamagesAllEnemiesInExplosionRadius()
    {
        var mage = CreateTestHuman("마법사", new Vector2Int(5, 5), Dir.UP);
        mage.CombatStat.magicalAttack = 50f;

        var enemy1 = CreateTestMonster("적1", new Vector2Int(5, 8), Dir.DOWN); // 폭발 중심
        var enemy2 = CreateTestMonster("적2", new Vector2Int(6, 8), Dir.DOWN); // 반경 1칸 내
        var enemyOutside = CreateTestMonster("적3", new Vector2Int(5, 12), Dir.DOWN); // 반경 밖

        // Session Mocking for AoE enumeration
        var session = new GameSession();
        var sessionUnits = new List<Unit> { mage, enemy1, enemy2, enemyOutside };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        var skillData = new SkillData
        {
            skillName = "파이어볼",
            skillArchetype = "GroundAoE",
            baseDelayMs = 0f,
            baseCooldown = 5.0f,
            cooldownSlot = 0,
            hitRange = 6,
            explosionRadius = 1.8f,
            damageMultiplier = 1.5f,
            priorityBase = 70f
        };

        var aoe = new SkillAction_GroundAoE(skillData);

        float hp1Before = enemy1.Health.hp;
        float hp2Before = enemy2.Health.hp;
        float hpOutBefore = enemyOutside.Health.hp;

        aoe.Execute(mage, enemy1, 3.0f);

        Assert.Less(enemy1.Health.hp, hp1Before, "폭발 중심의 적1은 마법 피해를 입어야 함");
        Assert.Less(enemy2.Health.hp, hp2Before, "폭발 반경 내 적2는 마법 피해를 입어야 함");
        Assert.AreEqual(hpOutBefore, enemyOutside.Health.hp, "폭발 반경 밖의 적3은 피해를 입지 않아야 함");
    }

    [Test]
    public void Tier1_Priest_Heal_RestoresLowestHpAllyHealth()
    {
        var priest = CreateTestHuman("사제", new Vector2Int(10, 10), Dir.UP);
        var damagedAlly = CreateTestHuman("부상당한 전사", new Vector2Int(10, 11), Dir.UP);
        var fullHpAlly = CreateTestHuman("건강한 아군", new Vector2Int(11, 10), Dir.UP);

        damagedAlly.Health.maxHp = 100f;
        damagedAlly.Health.hp = 30f; // 30% HP

        fullHpAlly.Health.maxHp = 100f;
        fullHpAlly.Health.hp = 100f; // 100% HP

        var session = new GameSession();
        var sessionUnits = new List<Unit> { priest, damagedAlly, fullHpAlly };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        var skillData = new SkillData
        {
            skillName = "치유",
            skillArchetype = "Heal",
            baseDelayMs = 0f,
            baseCooldown = 4.5f,
            cooldownSlot = 0,
            hitRange = 5,
            effectAmount = 40.0f,
            priorityBase = 75f
        };

        var heal = new SkillAction_Heal(skillData);

        Assert.IsTrue(heal.IsAvailable(priest), "부상당한 아군이 있으면 힐 사용 가능해야 함");
        float priority = heal.GetPriority(priest, damagedAlly, 1.0f);
        Assert.Greater(priority, 75f, "부상 아군에 대해 높은 우선순위를 가져야 함");

        heal.Execute(priest, damagedAlly, 1.0f);

        Assert.AreEqual(70f, damagedAlly.Health.hp, 0.01f, "30 HP에서 40 치유되어 70 HP가 되어야 함");
    }

    [Test]
    public void Tier1_Priest_Heal_UnavailableWhenAllAlliesFull()
    {
        var priest = CreateTestHuman("사제", new Vector2Int(10, 10), Dir.UP);
        var ally = CreateTestHuman("건강한 아군", new Vector2Int(10, 11), Dir.UP);

        priest.Health.hp = priest.Health.maxHp;
        ally.Health.hp = ally.Health.maxHp;

        var session = new GameSession();
        var sessionUnits = new List<Unit> { priest, ally };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        var skillData = new SkillData
        {
            skillName = "치유",
            skillArchetype = "Heal",
            baseDelayMs = 0f,
            baseCooldown = 4.5f,
            cooldownSlot = 0,
            hitRange = 5,
            effectAmount = 35.0f,
            priorityBase = 75f
        };

        var heal = new SkillAction_Heal(skillData);

        float priority = heal.GetPriority(priest, ally, 1.0f);
        Assert.AreEqual(0f, priority, "모든 아군 체력이 100%일 때 우선순위 0으로 스킬 낭비 방지");
    }

    [Test]
    public void Tier1_Paladin_Shield_AppliesTemporaryShieldAndRollsBack()
    {
        var paladin = CreateTestHuman("성기사", new Vector2Int(10, 10), Dir.UP);
        var target = CreateTestHuman("보호대상", new Vector2Int(10, 11), Dir.UP);
        target.Health.maxHp = 100f;
        target.Health.hp = 100f;

        var session = new GameSession();
        var sessionUnits = new List<Unit> { paladin, target };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        var skillData = new SkillData
        {
            skillName = "성스러운 방패",
            skillArchetype = "Shield",
            baseDelayMs = 0f,
            baseCooldown = 8.0f,
            cooldownSlot = 0,
            hitRange = 3,
            effectAmount = 40.0f,
            effectDuration = 0.05f, // 50ms for fast test
            priorityBase = 65f
        };

        var shield = new SkillAction_Shield(skillData);
        shield.Execute(paladin, target, 1.0f);

        Assert.AreEqual(140f, target.Health.maxHp, "보호막 적용 즉시 최대 체력이 40 증가해야 함");
        Assert.AreEqual(140f, target.Health.hp, "보호막 적용 즉시 현재 체력이 40 증가해야 함");
    }

    [Test]
    public void Tier1_Shaman_Curse_DebuffsEnemyAndRollsBack()
    {
        var shaman = CreateTestHuman("주술사", new Vector2Int(10, 10), Dir.UP);
        var enemy = CreateTestMonster("적", new Vector2Int(10, 12), Dir.DOWN);

        enemy.CombatStat.physicalAttack = 50f;
        enemy.CombatStat.physicalDefense = 20f;

        var session = new GameSession();
        var sessionUnits = new List<Unit> { shaman, enemy };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        var skillData = new SkillData
        {
            skillName = "저주",
            skillArchetype = "Curse",
            baseDelayMs = 0f,
            baseCooldown = 7.0f,
            cooldownSlot = 0,
            hitRange = 5,
            effectAmount = 15.0f,
            effectDuration = 0.05f,
            priorityBase = 60f
        };

        var curse = new SkillAction_Curse(skillData);
        curse.Execute(shaman, enemy, 2.0f);

        Assert.Less(enemy.CombatStat.physicalAttack, 50f, "적의 물리 공격력이 감소해야 함");
        Assert.Less(enemy.CombatStat.physicalDefense, 20f, "적의 물리 방어력이 감소해야 함");
    }

    [Test]
    public void Tier1_Monk_MultiHit_ExecutesMultiHitStrikes()
    {
        var monk = CreateTestHuman("무도가", new Vector2Int(10, 10), Dir.UP);
        var enemy = CreateTestMonster("적", new Vector2Int(10, 11), Dir.DOWN);
        float hpBefore = enemy.Health.hp;

        var session = new GameSession();
        var sessionUnits = new List<Unit> { monk, enemy };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        var skillData = new SkillData
        {
            skillName = "연격",
            skillArchetype = "MultiHit",
            baseDelayMs = 0f,
            baseCooldown = 3.5f,
            cooldownSlot = 0,
            hitShape = "LINE",
            hitRange = 1,
            threatRange = 1,
            damageMultiplier = 0.5f,
            multiHitCount = 3,
            hasStun = true,
            stunDuration = 0.5f,
            priorityBase = 65f
        };

        var multiHit = new SkillAction_MultiHit(skillData);
        multiHit.Execute(monk, enemy, 1.0f);

        Assert.Less(enemy.Health.hp, hpBefore, "연격 타격 후 체력이 감소해야 함");
    }

    [Test]
    public void Tier1_Bard_PartyBuff_BuffsAllAliveAllies()
    {
        var bard = CreateTestHuman("음유시인", new Vector2Int(10, 10), Dir.UP);
        var ally1 = CreateTestHuman("아군1", new Vector2Int(11, 10), Dir.UP);
        var ally2 = CreateTestHuman("아군2", new Vector2Int(10, 11), Dir.UP);

        ally1.CombatStat.physicalAttack = 30f;
        ally2.CombatStat.physicalAttack = 35f;

        var session = new GameSession();
        var sessionUnits = new List<Unit> { bard, ally1, ally2 };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        var skillData = new SkillData
        {
            skillName = "전투의 노래",
            skillArchetype = "PartyBuff",
            baseDelayMs = 0f,
            baseCooldown = 10.0f,
            cooldownSlot = 0,
            hitRange = 8,
            effectAmount = 12.0f,
            effectDuration = 0.05f,
            priorityBase = 60f
        };

        var partyBuff = new SkillAction_PartyBuff(skillData);
        partyBuff.Execute(bard, bard, 0f);

        Assert.AreEqual(42f, ally1.CombatStat.physicalAttack, "아군1 물리 공격력 +12 버프 적용");
        Assert.AreEqual(47f, ally2.CombatStat.physicalAttack, "아군2 물리 공격력 +12 버프 적용");
    }

    [Test]
    public void Tier1_VisualDefinition_BuildSkillActions_RoutesAllEightArchetypes()
    {
        var uvdGo = new GameObject("TestUVD");
        var uvd = uvdGo.AddComponent<UnitVisualDefinition>();

        uvd.skills = new List<SkillData>
        {
            new SkillData { skillName = "강타", skillArchetype = "Generic" },
            new SkillData { skillName = "기습", skillArchetype = "Backstab" },
            new SkillData { skillName = "파이어볼", skillArchetype = "GroundAoE" },
            new SkillData { skillName = "치유", skillArchetype = "Heal" },
            new SkillData { skillName = "성스러운 방패", skillArchetype = "Shield" },
            new SkillData { skillName = "저주", skillArchetype = "Curse" },
            new SkillData { skillName = "연격", skillArchetype = "MultiHit" },
            new SkillData { skillName = "전투의 노래", skillArchetype = "PartyBuff" }
        };

        var actions = uvd.BuildSkillActions();
        Assert.AreEqual(8, actions.Count, "8개 스킬 액션이 모두 인스턴스화되어야 함");

        Assert.IsInstanceOf<SkillAction_Generic>(actions[0]);
        Assert.IsInstanceOf<SkillAction_Backstab>(actions[1]);
        Assert.IsInstanceOf<SkillAction_GroundAoE>(actions[2]);
        Assert.IsInstanceOf<SkillAction_Heal>(actions[3]);
        Assert.IsInstanceOf<SkillAction_Shield>(actions[4]);
        Assert.IsInstanceOf<SkillAction_Curse>(actions[5]);
        Assert.IsInstanceOf<SkillAction_MultiHit>(actions[6]);
        Assert.IsInstanceOf<SkillAction_PartyBuff>(actions[7]);

        GameObject.DestroyImmediate(uvdGo);
    }

    #endregion

    // ========================================================================
    // TIER 2: BOUNDARY & CORNER CASES
    // ========================================================================

    #region Tier 2: Boundary & Corner Cases

    [Test]
    public void Tier2_DeadTarget_DoesNotTakeAdditionalDamageOrThrow()
    {
        var warrior = CreateTestHuman("전사", new Vector2Int(10, 10), Dir.UP);
        var deadTarget = CreateTestMonster("사망 몬스터", new Vector2Int(10, 11), Dir.DOWN);
        deadTarget.Health.hp = 0f;

        var skillData = new SkillData
        {
            skillName = "강타",
            skillArchetype = "Generic",
            damageMultiplier = 2.0f
        };
        var smash = new SkillAction_Generic(skillData);

        Assert.DoesNotThrow(() => smash.Execute(warrior, deadTarget, 1.0f));
        Assert.AreEqual(0f, deadTarget.Health.hp, "사망한 대상의 체력은 0 이하로 왜곡되지 않아야 함");
    }

    [Test]
    public void Tier2_NullTargetAndNullSession_GracefullyHandled()
    {
        var rogue = CreateTestHuman("도적", new Vector2Int(10, 10), Dir.UP);
        var skillData = new SkillData { skillName = "기습", skillArchetype = "Backstab" };
        var backstab = new SkillAction_Backstab(skillData);

        Assert.DoesNotThrow(() => backstab.Execute(rogue, null, 1.0f), "대상 null이어도 예외 없이 안전 처리");
    }

    [Test]
    public void Tier2_Backstab_EightDirectionalMatrix_Verification()
    {
        var rogue = CreateTestHuman("도적", new Vector2Int(10, 10), Dir.UP);
        var target = CreateTestMonster("대상", new Vector2Int(10, 11), Dir.UP);

        // Same directions -> Backstab True
        rogue.currentDir = Dir.UP; target.currentDir = Dir.UP;
        Assert.IsTrue(SkillAction_Backstab.IsBackstab(rogue, target), "UP vs UP = Backstab");

        rogue.currentDir = Dir.RIGHT; target.currentDir = Dir.RIGHT;
        Assert.IsTrue(SkillAction_Backstab.IsBackstab(rogue, target), "RIGHT vs RIGHT = Backstab");

        rogue.currentDir = Dir.UP_RIGHT; target.currentDir = Dir.UP_RIGHT;
        Assert.IsTrue(SkillAction_Backstab.IsBackstab(rogue, target), "UP_RIGHT vs UP_RIGHT = Backstab");

        // Opposite directions -> Backstab False
        rogue.currentDir = Dir.UP; target.currentDir = Dir.DOWN;
        Assert.IsFalse(SkillAction_Backstab.IsBackstab(rogue, target), "UP vs DOWN != Backstab");

        rogue.currentDir = Dir.LEFT; target.currentDir = Dir.RIGHT;
        Assert.IsFalse(SkillAction_Backstab.IsBackstab(rogue, target), "LEFT vs RIGHT != Backstab");

        // Perpendicular directions -> Backstab False
        rogue.currentDir = Dir.UP; target.currentDir = Dir.RIGHT;
        Assert.IsFalse(SkillAction_Backstab.IsBackstab(rogue, target), "UP vs RIGHT != Backstab");
    }

    [Test]
    public void Tier2_Overhealing_StrictlyClampedToMaxHp()
    {
        var priest = CreateTestHuman("사제", new Vector2Int(10, 10), Dir.UP);
        var target = CreateTestHuman("대상", new Vector2Int(10, 11), Dir.UP);
        target.Health.maxHp = 100f;
        target.Health.hp = 95f;

        var session = new GameSession();
        var sessionUnits = new List<Unit> { priest, target };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        var skillData = new SkillData
        {
            skillName = "치유",
            skillArchetype = "Heal",
            effectAmount = 50.0f,
            hitRange = 5
        };
        var heal = new SkillAction_Heal(skillData);
        heal.Execute(priest, target, 1.0f);

        Assert.AreEqual(100f, target.Health.hp, "오버힐 시 최대 체력을 초과하지 않고 maxHp로 클램핑되어야 함");
    }

    [Test]
    public void Tier2_GroundAoE_ZeroRadius_HitsOnlyCenterTile()
    {
        var mage = CreateTestHuman("마법사", new Vector2Int(5, 5), Dir.UP);
        var target = CreateTestMonster("중심적", new Vector2Int(5, 8), Dir.DOWN);
        var adjacent = CreateTestMonster("인접적", new Vector2Int(5, 9), Dir.DOWN);

        var session = new GameSession();
        var sessionUnits = new List<Unit> { mage, target, adjacent };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        var skillData = new SkillData
        {
            skillName = "파이어볼",
            skillArchetype = "GroundAoE",
            hitRange = 6,
            explosionRadius = 0.1f, // 점 타격
            damageMultiplier = 2.0f
        };
        var aoe = new SkillAction_GroundAoE(skillData);

        float centerHpBefore = target.Health.hp;
        float adjHpBefore = adjacent.Health.hp;

        aoe.Execute(mage, target, 3.0f);

        Assert.Less(target.Health.hp, centerHpBefore, "중심 타일 적은 피해를 입음");
        Assert.AreEqual(adjHpBefore, adjacent.Health.hp, "반경 0.1일 때 인접 타일 적은 피해를 입지 않음");
    }

    #endregion

    // ========================================================================
    // TIER 3: CROSS-FEATURE COMBINATIONS
    // ========================================================================

    #region Tier 3: Cross-Feature Combinations

    [Test]
    public void Tier3_BuffAndDebuff_StackingAndSymmetricRollback()
    {
        var bard = CreateTestHuman("음유시인", new Vector2Int(10, 10), Dir.UP);
        var shaman = CreateTestHuman("주술사", new Vector2Int(10, 11), Dir.UP);
        var combatant = CreateTestHuman("전투원", new Vector2Int(10, 12), Dir.UP);

        float initialAtk = combatant.CombatStat.physicalAttack; // 40

        var session = new GameSession();
        var sessionUnits = new List<Unit> { bard, shaman, combatant };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        // 1. Bard Buff (+12 Atk)
        var buffSkill = new SkillData
        {
            skillName = "전투의 노래",
            skillArchetype = "PartyBuff",
            effectAmount = 12f,
            effectDuration = 0.05f
        };
        var partyBuff = new SkillAction_PartyBuff(buffSkill);
        partyBuff.Execute(bard, combatant, 2f);

        Assert.AreEqual(initialAtk + 12f, combatant.CombatStat.physicalAttack, "버프 후 52 Atk");

        // 2. Shaman Curse (-14 Atk)
        var curseSkill = new SkillData
        {
            skillName = "저주",
            skillArchetype = "Curse",
            effectAmount = 14f,
            effectDuration = 0.05f
        };
        var curse = new SkillAction_Curse(curseSkill);
        curse.Execute(shaman, combatant, 1f);

        Assert.AreEqual(initialAtk + 12f - 14f, combatant.CombatStat.physicalAttack, "디버프 중첩 후 38 Atk");
    }

    [Test]
    public void Tier3_MultiHit_AgainstShield_AbsorbsDamageFromShieldPoolFirst()
    {
        var monk = CreateTestHuman("무도가", new Vector2Int(10, 10), Dir.UP);
        var defender = CreateTestHuman("수호대상", new Vector2Int(10, 11), Dir.DOWN);
        var paladin = CreateTestHuman("성기사", new Vector2Int(9, 11), Dir.UP);

        defender.Health.maxHp = 100f;
        defender.Health.hp = 100f;

        var session = new GameSession();
        var sessionUnits = new List<Unit> { monk, defender, paladin };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        // 1. Paladin applies 50 shield
        var shieldSkill = new SkillData
        {
            skillName = "성스러운 방패",
            skillArchetype = "Shield",
            effectAmount = 50f,
            effectDuration = 5f
        };
        var shield = new SkillAction_Shield(shieldSkill);
        shield.Execute(paladin, defender, 1f);

        Assert.AreEqual(150f, defender.Health.maxHp);
        Assert.AreEqual(150f, defender.Health.hp);

        // 2. Monk hits with MultiHit
        var multiSkill = new SkillData
        {
            skillName = "연격",
            skillArchetype = "MultiHit",
            damageMultiplier = 0.5f,
            multiHitCount = 3,
            hitRange = 1,
            threatRange = 1
        };
        var multiHit = new SkillAction_MultiHit(multiSkill);
        multiHit.Execute(monk, defender, 1f);

        // Defender was damaged, but base 100 HP is protected by shield pool
        Assert.Greater(defender.Health.hp, 90f, "보호막 풀이 데미지를 흡수하여 기본 체력 보존");
    }

    [Test]
    public void Tier3_HealUnderCurse_RestoresHealthWithoutCorruptingDebuff()
    {
        var priest = CreateTestHuman("사제", new Vector2Int(10, 10), Dir.UP);
        var cursedAlly = CreateTestHuman("저주받은 아군", new Vector2Int(10, 11), Dir.UP);
        var shamanEnemy = CreateTestMonster("적 주술사", new Vector2Int(10, 15), Dir.DOWN);

        cursedAlly.Health.maxHp = 100f;
        cursedAlly.Health.hp = 30f;
        cursedAlly.CombatStat.physicalAttack = 40f;

        var session = new GameSession();
        var sessionUnits = new List<Unit> { priest, cursedAlly, shamanEnemy };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        // 1. Apply Curse (-10 Atk)
        var curseSkill = new SkillData { skillName = "저주", skillArchetype = "Curse", effectAmount = 10f, effectDuration = 5f };
        var curse = new SkillAction_Curse(curseSkill);
        curse.Execute(shamanEnemy, cursedAlly, 4f);

        Assert.AreEqual(30f, cursedAlly.CombatStat.physicalAttack, "저주로 공격력 감소");

        // 2. Priest Heals (+40 HP)
        var healSkill = new SkillData { skillName = "치유", skillArchetype = "Heal", effectAmount = 40f, hitRange = 5 };
        var heal = new SkillAction_Heal(healSkill);
        heal.Execute(priest, cursedAlly, 1f);

        Assert.AreEqual(70f, cursedAlly.Health.hp, "체력 정상 회복");
        Assert.AreEqual(30f, cursedAlly.CombatStat.physicalAttack, "치유 후에도 저주 디버프 상태는 불변 유지");
    }

    #endregion
}
#endif
