#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Cysharp.Threading.Tasks;

// ========================================================================
// 8종 플레이어블 캐릭터 클래스 전투 시뮬레이션 테스트 스위트
// Tier 4: Real-World Combat Scenarios (Full Party Simulation, Dynamic Priest Triage,
// Bard Party DPS Amplification, Paladin Frontline Protection, Burst Combo Chains)
// ========================================================================

[TestFixture]
public class CombatSimulationTests
{
    private Human CreateClassUnit(string className, Vector2Int pos, Dir dir = Dir.UP)
    {
        var unit = ScriptableObject.CreateInstance<Human>();
        unit.unitType = new Knight { typeName = className, footprint = new Vector2(1, 1) };
        unit.position = pos;
        unit.currentDir = dir;
        unit.currentFloor = 0;
        unit.FactionBehavior = new HumanFactionBehavior();
        unit.CombatState.State.skillCooldowns = new float[10];

        switch (className)
        {
            case "전사":
                unit.Health.maxHp = 160f; unit.Health.hp = 160f;
                unit.CombatStat.physicalAttack = 40f; unit.CombatStat.magicalAttack = 10f;
                unit.CombatStat.physicalDefense = 15f; unit.CombatStat.magicalDefense = 10f;
                break;
            case "도적":
                unit.Health.maxHp = 120f; unit.Health.hp = 120f;
                unit.CombatStat.physicalAttack = 45f; unit.CombatStat.magicalAttack = 8f;
                unit.CombatStat.physicalDefense = 8f; unit.CombatStat.magicalDefense = 8f;
                break;
            case "마법사":
                unit.Health.maxHp = 100f; unit.Health.hp = 100f;
                unit.CombatStat.physicalAttack = 12f; unit.CombatStat.magicalAttack = 52f;
                unit.CombatStat.physicalDefense = 6f; unit.CombatStat.magicalDefense = 16f;
                break;
            case "사제":
                unit.Health.maxHp = 120f; unit.Health.hp = 120f;
                unit.CombatStat.physicalAttack = 15f; unit.CombatStat.magicalAttack = 35f;
                unit.CombatStat.physicalDefense = 10f; unit.CombatStat.magicalDefense = 16f;
                break;
            case "성기사":
                unit.Health.maxHp = 170f; unit.Health.hp = 170f;
                unit.CombatStat.physicalAttack = 34f; unit.CombatStat.magicalAttack = 20f;
                unit.CombatStat.physicalDefense = 16f; unit.CombatStat.magicalDefense = 16f;
                break;
            case "주술사":
                unit.Health.maxHp = 110f; unit.Health.hp = 110f;
                unit.CombatStat.physicalAttack = 18f; unit.CombatStat.magicalAttack = 42f;
                unit.CombatStat.physicalDefense = 8f; unit.CombatStat.magicalDefense = 14f;
                break;
            case "무도가":
                unit.Health.maxHp = 140f; unit.Health.hp = 140f;
                unit.CombatStat.physicalAttack = 36f; unit.CombatStat.magicalAttack = 10f;
                unit.CombatStat.physicalDefense = 12f; unit.CombatStat.magicalDefense = 10f;
                break;
            case "음유시인":
                unit.Health.maxHp = 125f; unit.Health.hp = 125f;
                unit.CombatStat.physicalAttack = 25f; unit.CombatStat.magicalAttack = 28f;
                unit.CombatStat.physicalDefense = 10f; unit.CombatStat.magicalDefense = 12f;
                break;
        }

        return unit;
    }

    private Monster CreateBossMonster(Vector2Int pos, float hp = 1000f)
    {
        var boss = ScriptableObject.CreateInstance<Monster>();
        boss.unitType = new WildMonsterA { typeName = "보스 몬스터", footprint = new Vector2(2, 2) };
        boss.position = pos;
        boss.currentDir = Dir.DOWN;
        boss.currentFloor = 0;
        boss.FactionBehavior = new WildMonsterBehavior();
        boss.CombatState.State.skillCooldowns = new float[10];

        boss.Health.maxHp = hp;
        boss.Health.hp = hp;
        boss.CombatStat.physicalAttack = 35f;
        boss.CombatStat.magicalAttack = 20f;
        boss.CombatStat.physicalDefense = 12f;
        boss.CombatStat.magicalDefense = 12f;

        return boss;
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
    // TIER 4: REAL-WORLD COMBAT SCENARIOS
    // ========================================================================

    [Test]
    public void Tier4_FullParty_Vs_WildBoss_MultiRoundCombatSimulation()
    {
        // 8-Man party setup
        var warrior = CreateClassUnit("전사", new Vector2Int(10, 10), Dir.UP);
        var paladin = CreateClassUnit("성기사", new Vector2Int(11, 10), Dir.UP);
        var monk    = CreateClassUnit("무도가", new Vector2Int(12, 10), Dir.UP);
        var rogue   = CreateClassUnit("도적", new Vector2Int(10, 13), Dir.UP); // Behind boss
        var mage    = CreateClassUnit("마법사", new Vector2Int(10, 7), Dir.UP);
        var priest  = CreateClassUnit("사제", new Vector2Int(11, 7), Dir.UP);
        var shaman  = CreateClassUnit("주술사", new Vector2Int(12, 7), Dir.UP);
        var bard    = CreateClassUnit("음유시인", new Vector2Int(9, 7), Dir.UP);

        var boss = CreateBossMonster(new Vector2Int(10, 11), hp: 500f);

        var session = new GameSession();
        var sessionUnits = new List<Unit> { warrior, paladin, monk, rogue, mage, priest, shaman, bard, boss };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        // Skills definition
        var warriorSmash = new SkillAction_Generic(new SkillData { skillName = "강타", damageMultiplier = 1.8f, hasStun = true, stunDuration = 1f });
        var rogueBackstab = new SkillAction_Backstab(new SkillData { skillName = "기습", damageMultiplier = 1.2f, effectAmount = 2.5f });
        var mageAoE = new SkillAction_GroundAoE(new SkillData { skillName = "파이어볼", damageMultiplier = 1.5f, explosionRadius = 2f });
        var priestHeal = new SkillAction_Heal(new SkillData { skillName = "치유", effectAmount = 35f, hitRange = 6 });
        var paladinShield = new SkillAction_Shield(new SkillData { skillName = "성스러운 방패", effectAmount = 40f, hitRange = 4 });
        var shamanCurse = new SkillAction_Curse(new SkillData { skillName = "저주", effectAmount = 15f, hitRange = 6 });
        var monkMulti = new SkillAction_MultiHit(new SkillData { skillName = "연격", damageMultiplier = 0.5f, multiHitCount = 3 });
        var bardBuff = new SkillAction_PartyBuff(new SkillData { skillName = "전투의 노래", effectAmount = 12f, hitRange = 8 });

        float bossHpStart = boss.Health.hp;

        // Round 1: Support Buffs & Debuffs
        bardBuff.Execute(bard, warrior, 0f);
        shamanCurse.Execute(shaman, boss, 4f);
        paladinShield.Execute(paladin, warrior, 1f);

        Assert.Greater(warrior.CombatStat.physicalAttack, 40f, "바드 버프로 전사 공격력 상승");
        Assert.Less(boss.CombatStat.physicalDefense, 12f, "주술사 저주로 보스 방어력 감소");
        Assert.AreEqual(200f, warrior.Health.hp, "성기사 쉴드로 전사 체력 160->200");

        // Round 2: Offensive Burst Execution
        warriorSmash.Execute(warrior, boss, 1f);
        rogueBackstab.Execute(rogue, boss, 1f);
        mageAoE.Execute(mage, boss, 4f);
        monkMulti.Execute(monk, boss, 1f);

        Assert.Less(boss.Health.hp, bossHpStart, "파티 총공격으로 보스 체력 대폭 감소");
        float bossDamageTaken = bossHpStart - boss.Health.hp;
        Assert.Greater(bossDamageTaken, 150f, "버프/디버프 연계 공격으로 150 이상의 총 피해 달성");

        // Round 3: Boss Counter-Attack & Priest Triage
        warrior.TakePhysicalDamage(80f, boss);
        Assert.Less(warrior.Health.hp, 150f, "보스 반격으로 전사 피격");

        float warriorHpBeforeHeal = warrior.Health.hp;
        priestHeal.Execute(priest, warrior, 3f);
        Assert.Greater(warrior.Health.hp, warriorHpBeforeHeal, "사제 힐로 전사 부상 회복");
    }

    [Test]
    public void Tier4_Priest_DynamicTriage_UnderFocusFire_AlwaysHealsLowestAlly()
    {
        var priest = CreateClassUnit("사제", new Vector2Int(10, 5), Dir.UP);
        var ally1 = CreateClassUnit("전사", new Vector2Int(9, 8), Dir.UP); // 70% HP
        var ally2 = CreateClassUnit("도적", new Vector2Int(11, 8), Dir.UP); // 20% HP (Critical)
        var ally3 = CreateClassUnit("무도가", new Vector2Int(10, 8), Dir.UP); // 90% HP

        ally1.Health.maxHp = 100f; ally1.Health.hp = 70f;
        ally2.Health.maxHp = 100f; ally2.Health.hp = 20f;
        ally3.Health.maxHp = 100f; ally3.Health.hp = 90f;

        var session = new GameSession();
        var sessionUnits = new List<Unit> { priest, ally1, ally2, ally3 };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        var healSkill = new SkillData
        {
            skillName = "치유",
            skillArchetype = "Heal",
            effectAmount = 40f,
            hitRange = 6,
            priorityBase = 75f
        };
        var heal = new SkillAction_Heal(healSkill);

        // Priest evaluates lowest HP ally
        Unit lowestAlly = heal.FindLowestHpAlly(priest, 6);
        Assert.AreSame(ally2, lowestAlly, "가장 체력 비율이 낮은 도적(20%)이 타겟팅되어야 함");

        heal.Execute(priest, lowestAlly, 3f);
        Assert.AreEqual(60f, ally2.Health.hp, "도적이 20에서 40 치유되어 60 HP가 됨");

        // Subsequent Triage evaluation: Ally1 is now lowest (70% vs Ally2's 60%) -> Ally2 is still lowest
        Unit nextTarget = heal.FindLowestHpAlly(priest, 6);
        Assert.AreSame(ally2, nextTarget, "아직 도적(60%)이 전사(70%)보다 낮으므로 우선 선택");
    }

    [Test]
    public void Tier4_Bard_PartyDpsAmplification_Benchmark()
    {
        var warriorA = CreateClassUnit("전사", new Vector2Int(10, 10), Dir.UP);
        var targetA = CreateBossMonster(new Vector2Int(10, 11), hp: 1000f);

        var sessionA = new GameSession();
        var sessionAUnits = new List<Unit> { warriorA, targetA };
        sessionA.units.AddRange(sessionAUnits);
        foreach (var u in sessionAUnits) AttachSession(u, sessionA);

        var warriorB = CreateClassUnit("전사", new Vector2Int(10, 10), Dir.UP);
        var bard = CreateClassUnit("음유시인", new Vector2Int(9, 10), Dir.UP);
        var targetB = CreateBossMonster(new Vector2Int(10, 11), hp: 1000f);

        var sessionB = new GameSession();
        var sessionBUnits = new List<Unit> { warriorB, bard, targetB };
        sessionB.units.AddRange(sessionBUnits);
        foreach (var u in sessionBUnits) AttachSession(u, sessionB);

        var smashA = new SkillAction_Generic(new SkillData { skillName = "강타", damageMultiplier = 1.8f });
        var smashB = new SkillAction_Generic(new SkillData { skillName = "강타", damageMultiplier = 1.8f });
        var bardBuff = new SkillAction_PartyBuff(new SkillData { skillName = "전투의 노래", effectAmount = 15f, hitRange = 6 });

        // Unbuffed attack
        smashA.Execute(warriorA, targetA, 1f);
        float unbuffedDamage = 1000f - targetA.Health.hp;

        // Buffed attack with Bard
        bardBuff.Execute(bard, warriorB, 1f);
        smashB.Execute(warriorB, targetB, 1f);
        float buffedDamage = 1000f - targetB.Health.hp;

        Assert.Greater(buffedDamage, unbuffedDamage, "바드 버프 파티가 더 높은 화력을 발휘해야 함");
        float dpsBoostRatio = buffedDamage / unbuffedDamage;
        Assert.GreaterOrEqual(dpsBoostRatio, 1.25f, "공격력 증폭으로 인한 유의미한 DPS 증가 검증");
    }

    [Test]
    public void Tier4_Paladin_ClutchShield_PreventsLethalDamage()
    {
        var paladin = CreateClassUnit("성기사", new Vector2Int(10, 9), Dir.UP);
        var warrior = CreateClassUnit("전사", new Vector2Int(10, 10), Dir.UP);
        var boss = CreateBossMonster(new Vector2Int(10, 11), hp: 500f);

        warrior.Health.maxHp = 100f;
        warrior.Health.hp = 25f; // 25 HP remaining

        var session = new GameSession();
        var sessionUnits = new List<Unit> { paladin, warrior, boss };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        // Boss incoming lethal hit of 50 damage
        float bossHit = 50f;

        // Paladin casts Shield before boss hit
        var shield = new SkillAction_Shield(new SkillData { skillName = "성스러운 방패", effectAmount = 40f, hitRange = 3 });
        shield.Execute(paladin, warrior, 1f);

        // Warrior now has 65 effective HP (25 + 40 shield)
        warrior.TakePhysicalDamage(bossHit, boss);

        Assert.Greater(warrior.Health.hp, 0f, "성기사 쉴드로 인해 치명적 타격(50)을 받고도 전사가 생존해야 함");
    }

    [Test]
    public void Tier4_CoordinatedBurstCombo_Curse_Stun_Backstab_MultiHit()
    {
        var shaman = CreateClassUnit("주술사", new Vector2Int(10, 7), Dir.UP);
        var warrior = CreateClassUnit("전사", new Vector2Int(10, 10), Dir.UP);
        var rogue = CreateClassUnit("도적", new Vector2Int(10, 13), Dir.UP); // Behind target
        var monk = CreateClassUnit("무도가", new Vector2Int(11, 11), Dir.LEFT);
        var boss = CreateBossMonster(new Vector2Int(10, 11), hp: 600f);

        var session = new GameSession();
        var sessionUnits = new List<Unit> { shaman, warrior, rogue, monk, boss };
        session.units.AddRange(sessionUnits);
        foreach (var u in sessionUnits) AttachSession(u, session);

        var curse = new SkillAction_Curse(new SkillData { skillName = "저주", effectAmount = 15f, hitRange = 6 });
        var smash = new SkillAction_Generic(new SkillData { skillName = "강타", damageMultiplier = 1.8f, hasStun = true, stunDuration = 2f });
        var backstab = new SkillAction_Backstab(new SkillData { skillName = "기습", damageMultiplier = 1.2f, effectAmount = 2.5f });
        var multiHit = new SkillAction_MultiHit(new SkillData { skillName = "연격", damageMultiplier = 0.5f, multiHitCount = 3 });

        float startHp = boss.Health.hp;

        // Step 1: Shaman shreds boss defense
        curse.Execute(shaman, boss, 4f);
        // Step 2: Warrior stuns boss
        smash.Execute(warrior, boss, 1f);
        Assert.Greater(boss.StatusEffects.State.stunDuration, 0f, "보스 스턴 상태 진입");

        // Step 3: Rogue lands critical backstab
        backstab.Execute(rogue, boss, 1f);

        // Step 4: Monk finishes combo
        multiHit.Execute(monk, boss, 1f);

        float totalDamage = startHp - boss.Health.hp;
        Assert.Greater(totalDamage, 200f, "연계 콤보로 200 이상의 폭발적 피해를 입혀야 함");
    }
}
#endif
