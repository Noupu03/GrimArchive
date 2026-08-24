#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class MonsterTypeIntegrationTests
{
    [Test]
    public void UnitTypes_MonsterSubclasses_InstantiateCorrectly()
    {
        var dollKnight = new DollKnight();
        Assert.AreEqual("인형 기사", dollKnight.typeName);
        Assert.AreEqual(new Vector2(1, 1), dollKnight.footprint);

        var gnoleA = new GnoleA();
        Assert.AreEqual("놀", gnoleA.typeName);
        Assert.AreEqual(new Vector2(1, 1), gnoleA.footprint);

        var goblinHoodA = new GoblinHoodA();
        Assert.AreEqual("고블린 후드", goblinHoodA.typeName);
        Assert.AreEqual(new Vector2(1, 1), goblinHoodA.footprint);
    }

    [Test]
    public void Reflection_ResolvesMonsterSubclassesByTypeName()
    {
        var allTypes = typeof(UnitType).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(UnitType)) && !t.IsAbstract)
            .ToList();

        Type ResolveByTypeName(string name)
        {
            foreach (var type in allTypes)
            {
                try
                {
                    var instance = (UnitType)Activator.CreateInstance(type);
                    if (instance.typeName == name) return type;
                }
                catch { }
            }
            return null;
        }

        Assert.AreEqual(typeof(DollKnight), ResolveByTypeName("인형 기사"));
        Assert.AreEqual(typeof(GnoleA), ResolveByTypeName("놀"));
        Assert.AreEqual(typeof(GoblinHoodA), ResolveByTypeName("고블린 후드"));
        Assert.AreEqual(typeof(MeleeTank), ResolveByTypeName("근접 탱커"));
    }

    [System.Serializable]
    private class TestUnitData
    {
        public string typeName;
        public string unitClass;
        public string[] skills;
        public TestVisual visual;
    }

    [System.Serializable]
    private class TestVisual
    {
        public string spriteLibrary;
    }

    [System.Serializable]
    private class TestUnitDb
    {
        public TestUnitData[] units;
    }

    [Test]
    public void UnitsJson_ContainsTargetMonsterDefinitions()
    {
        string jsonPath = Path.Combine(Application.dataPath, "Data/units.json");
        Assert.IsTrue(File.Exists(jsonPath), "units.json must exist");

        string jsonText = File.ReadAllText(jsonPath);
        var db = JsonUtility.FromJson<TestUnitDb>(jsonText);
        Assert.IsNotNull(db);
        Assert.IsNotNull(db.units);

        var unitMap = db.units.ToDictionary(u => u.typeName);

        Assert.IsTrue(unitMap.ContainsKey("근접 탱커"), "근접 탱커 must be preserved for backward compatibility");
        Assert.IsTrue(unitMap.ContainsKey("인형 기사"), "인형 기사 must be in units.json");
        Assert.IsTrue(unitMap.ContainsKey("놀"), "놀 must be in units.json");
        Assert.IsTrue(unitMap.ContainsKey("고블린 후드"), "고블린 후드 must be in units.json");

        Assert.AreEqual("Monster", unitMap["인형 기사"].unitClass);
        Assert.AreEqual("Mon_DollKnight", unitMap["인형 기사"].visual.spriteLibrary);
        Assert.AreEqual(3, unitMap["인형 기사"].skills.Length);

        Assert.AreEqual("Monster", unitMap["놀"].unitClass);
        Assert.AreEqual("Mon_GnoleA", unitMap["놀"].visual.spriteLibrary);
        Assert.AreEqual(3, unitMap["놀"].skills.Length);

        Assert.AreEqual("Monster", unitMap["고블린 후드"].unitClass);
        Assert.AreEqual("Mon_GoblinHoodA", unitMap["고블린 후드"].visual.spriteLibrary);
        Assert.AreEqual(3, unitMap["고블린 후드"].skills.Length);
    }
}
#endif
