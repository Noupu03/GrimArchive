using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace Verification
{
    class Program
    {
        static int totalTests = 0;
        static int passedTests = 0;
        static int failedTests = 0;
        static List<string> failureDetails = new();

        static void Assert(bool condition, string testName, string failMessage = "")
        {
            totalTests++;
            if (condition)
            {
                passedTests++;
                Console.WriteLine($"[PASS] {testName}");
            }
            else
            {
                failedTests++;
                string err = $"[FAIL] {testName}: {failMessage}";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(err);
                Console.ResetColor();
                failureDetails.Add(err);
            }
        }

        static void SetupAssemblyResolver(string rootDir)
        {
            var searchPaths = new List<string>
            {
                Path.Combine(rootDir, "Temp/bin/Debug"),
                Path.Combine(rootDir, "Library/Bee/PlayerScriptAssemblies"),
                Path.Combine(rootDir, "Library/ScriptAssemblies"),
                Path.Combine(rootDir, "Assets/Plugins/Demigiant/DOTween"),
                @"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Data\Managed",
                @"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Data\Managed\UnityEngine"
            };

            string pkgDir = Path.Combine(rootDir, "Assets/Packages");
            if (Directory.Exists(pkgDir))
            {
                foreach (var dll in Directory.GetFiles(pkgDir, "*.dll", SearchOption.AllDirectories))
                {
                    string dir = Path.GetDirectoryName(dll);
                    if (!searchPaths.Contains(dir)) searchPaths.Add(dir);
                }
            }

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string asmName = new AssemblyName(args.Name).Name;
                foreach (var dir in searchPaths)
                {
                    if (Directory.Exists(dir))
                    {
                        string candidate = Path.Combine(dir, asmName + ".dll");
                        if (File.Exists(candidate))
                        {
                            try
                            {
                                return Assembly.LoadFrom(candidate);
                            }
                            catch { }
                        }
                    }
                }
                return null;
            };
        }

        static void Main(string[] args)
        {
            string rootDir = Directory.GetCurrentDirectory();
            Console.WriteLine("================================================================================");
            Console.WriteLine("CHALLENGER 2: EMPIRICAL STRESS TEST & REFLECTION / RESOURCE HARNESS");
            Console.WriteLine($"Root: {rootDir}");
            Console.WriteLine("================================================================================\n");

            SetupAssemblyResolver(rootDir);

            // -------------------------------------------------------------------------
            // PART 1: Assembly Reflection & WaveSpawner.ResolveUnitType Consistency
            // -------------------------------------------------------------------------
            Console.WriteLine(">>> TEST SUITE 1: WaveSpawner Reflection & UnitTypes Dynamic Resolution");
            string asmPath = Path.Combine(rootDir, "Temp/bin/Debug/Assembly-CSharp.dll");
            Assert(File.Exists(asmPath), "Assembly-CSharp.dll existence", $"File not found at {asmPath}");

            if (File.Exists(asmPath))
            {
                var asm = Assembly.LoadFrom(asmPath);
                var unitTypeBase = asm.GetType("UnitType");
                Assert(unitTypeBase != null, "UnitType base class exists in Assembly-CSharp");

                Type[] allTypes;
                try
                {
                    allTypes = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    allTypes = ex.Types.Where(t => t != null).ToArray();
                }

                var nonAbstractSubclasses = allTypes
                    .Where(t => t.BaseType != null && (t.BaseType == unitTypeBase || t.IsSubclassOf(unitTypeBase)) && !t.IsAbstract)
                    .ToList();

                Assert(nonAbstractSubclasses.Count >= 15, $"Subclasses of UnitType discovered (found {nonAbstractSubclasses.Count})");

                // Simulate WaveSpawner.ResolveUnitType and CreateUnitTypeInstance exactly
                Type ResolveUnitType(string typeName)
                {
                    if (string.IsNullOrEmpty(typeName)) return null;

                    Type t = asm.GetType(typeName);
                    if (t == null)
                    {
                        foreach (Type type in nonAbstractSubclasses)
                        {
                            try
                            {
                                var tempInstance = Activator.CreateInstance(type);
                                var field = type.GetField("typeName");
                                if (field != null && (string)field.GetValue(tempInstance) == typeName)
                                {
                                    t = type;
                                    break;
                                }
                            }
                            catch { }
                        }
                    }
                    return t;
                }

                object CreateUnitTypeInstance(string typeName)
                {
                    Type t = ResolveUnitType(typeName);
                    if (t == null) return null;
                    try
                    {
                        var inst = Activator.CreateInstance(t);
                        if (!unitTypeBase.IsAssignableFrom(t)) return null;
                        return inst;
                    }
                    catch
                    {
                        return null;
                    }
                }

                // 1.1 Test Target Monsters
                var targetMonsters = new Dictionary<string, string>
                {
                    { "인형 기사", "DollKnight" },
                    { "놀", "GnoleA" },
                    { "고블린 후드", "GoblinHoodA" }
                };

                foreach (var kvp in targetMonsters)
                {
                    string targetName = kvp.Key;
                    string expectedClassName = kvp.Value;

                    Type resolved = ResolveUnitType(targetName);
                    Assert(resolved != null, $"ResolveUnitType('{targetName}') resolves non-null", "Returned null");
                    if (resolved != null)
                    {
                        Assert(resolved.Name == expectedClassName, $"ResolveUnitType('{targetName}') maps to {expectedClassName}", $"Resolved to {resolved.Name}");

                        var inst = Activator.CreateInstance(resolved);
                        var nameField = resolved.GetField("typeName");
                        var footField = resolved.GetField("footprint");

                        string actualName = (string)nameField.GetValue(inst);
                        object footprintVal = footField.GetValue(inst);

                        Assert(actualName == targetName, $"{expectedClassName}.typeName field matches '{targetName}'", $"Actual: {actualName}");
                        Assert(footprintVal != null, $"{expectedClassName}.footprint is non-null");

                        var created = CreateUnitTypeInstance(targetName);
                        Assert(created != null, $"CreateUnitTypeInstance('{targetName}') returns valid UnitType instance");
                    }
                }

                // 1.2 Test Legacy & Human Unit Types
                var otherTypes = new Dictionary<string, string>
                {
                    { "근접 탱커", "MeleeTank" },
                    { "야생 몬스터 A", "WildMonsterA" },
                    { "보스 골렘", "BossGolem" },
                    { "야생 거점", "WildBaseType" },
                    { "전사", "Warrior" },
                    { "도적", "Rogue" },
                    { "마법사", "Mage" },
                    { "사제", "Priest" },
                    { "성기사", "Paladin" },
                    { "주술사", "Shaman" },
                    { "무도가", "Monk" },
                    { "음유시인", "Bard" },
                    { "기사형", "Knight" },
                    { "인간", "HumanBaseType" },
                    { "아처형", "Archer" }
                };

                foreach (var kvp in otherTypes)
                {
                    Type resolved = ResolveUnitType(kvp.Key);
                    Assert(resolved != null && resolved.Name == kvp.Value, $"ResolveUnitType('{kvp.Key}') -> {kvp.Value}");
                    var created = CreateUnitTypeInstance(kvp.Key);
                    Assert(created != null, $"CreateUnitTypeInstance('{kvp.Key}') returns instance of {kvp.Value}");
                }

                // 1.3 Adversarial Reflection Inputs
                string[] adversarialInputs = new[]
                {
                    "",
                    "   ",
                    "인형기사", // missing space
                    "놀 ",     // trailing space
                    " 고블린 후드", // leading space
                    "NonExistentMonster_12345",
                    "UnitType", // abstract base class
                    "Monster",  // entity class, not UnitType
                    "Human",    // entity class
                    null
                };

                foreach (var adv in adversarialInputs)
                {
                    var created = CreateUnitTypeInstance(adv);
                    Assert(created == null, $"CreateUnitTypeInstance('{adv ?? "<null>"}') safely evaluates to null without uncaught exception");
                }

                // 1.4 Parameterless Constructor & Exception Safety on all UnitType subclasses
                foreach (var t in nonAbstractSubclasses)
                {
                    bool canInstantiate = false;
                    try
                    {
                        var inst = Activator.CreateInstance(t);
                        canInstantiate = (inst != null);
                    }
                    catch { }
                    Assert(canInstantiate, $"Public parameterless constructor works for {t.Name}");
                }
            }

            Console.WriteLine();

            // -------------------------------------------------------------------------
            // PART 2: Resource Path Loading & Prefab Integrity (UnitSpriteManager)
            // -------------------------------------------------------------------------
            Console.WriteLine(">>> TEST SUITE 2: UnitSpriteManager Resource Loading & Prefab Architecture");

            string[] testPrefabs = new[] { "인형 기사", "놀", "고블린 후드" };
            var expectedSpriteLibMetas = new Dictionary<string, string>
            {
                { "인형 기사", "Assets/Sprite/Mon/Mon_DollKnight/Mon_DollKnight.spriteLib.meta" },
                { "놀", "Assets/Sprite/Mon/Mon_Gnole/Mon_GnoleA.spriteLib.meta" },
                { "고블린 후드", "Assets/Sprite/Mon/Mon_GoblinHood/Mon_GoblinHoodA.spriteLib.meta" }
            };

            foreach (var pName in testPrefabs)
            {
                string relPrefabPath = $"Assets/Resources/Units/{pName}.prefab";
                string fullPrefabPath = Path.Combine(rootDir, relPrefabPath);
                string metaPath = fullPrefabPath + ".meta";

                Assert(File.Exists(fullPrefabPath), $"Prefab exists: {relPrefabPath}");
                Assert(File.Exists(metaPath), $"Prefab .meta exists: {relPrefabPath}.meta");

                if (File.Exists(fullPrefabPath))
                {
                    string rawContent = File.ReadAllText(fullPrefabPath);
                    // Unescape unicode strings in YAML (e.g. \uC778\uD615 -> 인형)
                    string content = Regex.Unescape(rawContent);

                    // 2.1 Check Root Components
                    Assert(content.Contains("m_Name: " + pName) || content.Contains("m_Name: \"" + pName + "\""), $"Prefab '{pName}' root name matches");
                    Assert(content.Contains("UnitVisualDefinition"), $"Prefab '{pName}' has UnitVisualDefinition");
                    Assert(content.Contains("unitTypeName: " + pName) || content.Contains("unitTypeName: \"" + pName + "\""), $"Prefab '{pName}' UnitVisualDefinition.unitTypeName matches");

                    // 2.2 Check Child Visual & 2D Animation Components
                    Assert(content.Contains("SpriteRenderer"), $"Prefab '{pName}' has SpriteRenderer");
                    Assert(content.Contains("SpriteLibrary"), $"Prefab '{pName}' has SpriteLibrary");
                    Assert(content.Contains("SpriteResolver"), $"Prefab '{pName}' has SpriteResolver");
                    Assert(content.Contains("ShadowCaster2D"), $"Prefab '{pName}' has ShadowCaster2D");

                    // 2.3 Check SpriteLibrary GUID Linkage
                    if (expectedSpriteLibMetas.TryGetValue(pName, out string sMetaPath))
                    {
                        string fullSMetaPath = Path.Combine(rootDir, sMetaPath);
                        Assert(File.Exists(fullSMetaPath), $"SpriteLibrary .meta exists: {sMetaPath}");
                        if (File.Exists(fullSMetaPath))
                        {
                            string sMetaText = File.ReadAllText(fullSMetaPath);
                            var match = Regex.Match(sMetaText, @"guid:\s*([0-9a-fA-F]{32})");
                            Assert(match.Success, $"SpriteLib meta GUID parsed: {sMetaPath}");
                            if (match.Success)
                            {
                                string guid = match.Groups[1].Value;
                                Assert(content.Contains(guid), $"Prefab '{pName}' contains valid SpriteLibrary GUID ({guid})");
                            }
                        }
                    }
                }
            }

            Console.WriteLine();

            // -------------------------------------------------------------------------
            // PART 3: JSON Parsing Robustness & Cross-Referencing
            // -------------------------------------------------------------------------
            Console.WriteLine(">>> TEST SUITE 3: JSON Parsing Robustness & Data Integrity");

            // 3.1 units.json
            string unitsJsonPath = Path.Combine(rootDir, "Assets/Data/units.json");
            Assert(File.Exists(unitsJsonPath), "Assets/Data/units.json existence");

            // 3.2 skills.json
            string skillsJsonPath = Path.Combine(rootDir, "Assets/Data/skills.json");
            Assert(File.Exists(skillsJsonPath), "Assets/Data/skills.json existence");

            HashSet<string> validSkillNames = new();
            if (File.Exists(skillsJsonPath))
            {
                using var skillDoc = JsonDocument.Parse(File.ReadAllText(skillsJsonPath));
                var skillsArray = skillDoc.RootElement.GetProperty("skills");
                foreach (var s in skillsArray.EnumerateArray())
                {
                    validSkillNames.Add(s.GetProperty("skillName").GetString());
                }
                Assert(validSkillNames.Count >= 15, $"Loaded {validSkillNames.Count} valid skills from skills.json");
            }

            if (File.Exists(unitsJsonPath))
            {
                string unitsJsonText = File.ReadAllText(unitsJsonPath);
                using var doc = JsonDocument.Parse(unitsJsonText);
                var root = doc.RootElement;
                Assert(root.TryGetProperty("units", out var unitsArr), "units.json has root 'units' array");

                Dictionary<string, JsonElement> unitsMap = new();
                bool duplicateFound = false;

                foreach (var u in unitsArr.EnumerateArray())
                {
                    string tName = u.GetProperty("typeName").GetString();
                    if (unitsMap.ContainsKey(tName))
                    {
                        duplicateFound = true;
                    }
                    unitsMap[tName] = u;
                }

                Assert(!duplicateFound, "No duplicate typeNames in units.json");
                Assert(unitsMap.ContainsKey("인형 기사"), "'인형 기사' present in units.json");
                Assert(unitsMap.ContainsKey("놀"), "'놀' present in units.json");
                Assert(unitsMap.ContainsKey("고블린 후드"), "'고블린 후드' present in units.json");
                Assert(unitsMap.ContainsKey("근접 탱커"), "'근접 탱커' preserved in units.json");

                // Validate Monster fields & cross-reference skills
                string[] targetMonKeys = new[] { "인형 기사", "놀", "고블린 후드" };
                foreach (var monKey in targetMonKeys)
                {
                    if (unitsMap.TryGetValue(monKey, out var mon))
                    {
                        string uClass = mon.GetProperty("unitClass").GetString();
                        Assert(uClass == "Monster", $"Unit '{monKey}' unitClass is 'Monster'");

                        // Footprint
                        var fp = mon.GetProperty("footprint");
                        Assert(fp.GetArrayLength() == 2 && fp[0].GetInt32() == 1 && fp[1].GetInt32() == 1, $"Unit '{monKey}' footprint is [1, 1]");

                        // Skills validation against skills.json
                        var skills = mon.GetProperty("skills");
                        Assert(skills.GetArrayLength() == 3, $"Unit '{monKey}' has exactly 3 skills");
                        foreach (var sk in skills.EnumerateArray())
                        {
                            string skName = sk.GetString();
                            Assert(validSkillNames.Contains(skName), $"Skill '{skName}' referenced by '{monKey}' exists in skills.json");
                        }

                        // Stats complete check
                        var stats = mon.GetProperty("stats");
                        string[] requiredStats = new[] {
                            "maxHp", "maxMp", "physicalAttack", "magicalAttack", "physicalDefense",
                            "magicalDefense", "HPRegen", "attackspeed", "walkSpeed", "reaction",
                            "criticalChance", "cooltimeReduction", "statusResistance", "maxMental",
                            "mental", "spotting", "leadershipRange", "charisma", "physicalAttackSpeed",
                            "magicalCastSpeed"
                        };
                        bool allStatsPresent = true;
                        foreach (var sName in requiredStats)
                        {
                            if (!stats.TryGetProperty(sName, out _))
                            {
                                allStatsPresent = false;
                                break;
                            }
                        }
                        Assert(allStatsPresent, $"Unit '{monKey}' contains all 20 required stat properties");

                        // Weight complete check
                        var weight = mon.GetProperty("weight");
                        string[] requiredWeights = new[] {
                            "isSpecialUnit", "isInterestTarget", "baseInterest", "baseDanger",
                            "heavyHitThreshold", "stealth", "baseVisibility"
                        };
                        bool allWeightsPresent = true;
                        foreach (var wName in requiredWeights)
                        {
                            if (!weight.TryGetProperty(wName, out _))
                            {
                                allWeightsPresent = false;
                                break;
                            }
                        }
                        Assert(allWeightsPresent, $"Unit '{monKey}' contains all 7 required weight properties");

                        // Visual effects check
                        var effects = mon.GetProperty("visual").GetProperty("effects");
                        Assert(effects.TryGetProperty("hitSpark", out _) &&
                               effects.TryGetProperty("bloodDrip", out _) &&
                               effects.TryGetProperty("guard", out _) &&
                               effects.TryGetProperty("parry", out _),
                               $"Unit '{monKey}' contains hitSpark, bloodDrip, guard, parry VFX keys");
                    }
                }

                // 3.3 Adversarial JSON parser test (malformed JSON resilience)
                bool caughtMalformed = false;
                try
                {
                    JsonDocument.Parse("{ units: [ { typeName: missing_quotes } ] }");
                }
                catch (JsonException)
                {
                    caughtMalformed = true;
                }
                Assert(caughtMalformed, "JsonException correctly triggers on invalid JSON syntax");
            }

            Console.WriteLine();
            Console.WriteLine("================================================================================");
            Console.WriteLine($"RESULTS SUMMARY: Total: {totalTests}, Passed: {passedTests}, Failed: {failedTests}");
            Console.WriteLine("================================================================================");

            if (failedTests > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nFailures:");
                foreach (var f in failureDetails)
                {
                    Console.WriteLine(" - " + f);
                }
                Console.ResetColor();
                Environment.Exit(1);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n>>> VERDICT: ALL TESTS PASSED EMPIRICALLY! NO DEFECTS FOUND <<<");
                Console.ResetColor();
                Environment.Exit(0);
            }
        }
    }
}
