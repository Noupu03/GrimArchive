#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 배치 테스트 러너 — Assets/Tests가 asmdef 없이 Assembly-CSharp에 컴파일돼 Unity Test Runner가 인식하지 못하므로("No tests were executed"), 에디터 런타임 안에서 리플렉션으로 NUnit 속성(Test/TestCase/SetUp/TearDown)을 직접 실행한다. 메뉴는 없고 명령줄 전용이며 에디터가 이 프로젝트를 열고 있으면 못 돌린다(단일 인스턴스).
// 사용: Unity -batchmode -nographics -projectPath <경로> -executeMethod BatchTestRunner.Run -testOut <결과파일> [-fixtures A;B]
public static class BatchTestRunner
{
	static string Arg(string name)
	{
		var a = Environment.GetCommandLineArgs();
		for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
		return null;
	}

	static bool HasAttr(MemberInfo m, string fullName) => m.GetCustomAttributes(true).Any(x => x.GetType().FullName == fullName);

	static object Coerce(object v, Type t)
	{
		if (v == null) return null;
		if (t.IsInstanceOfType(v)) return v;
		if (t.IsEnum) return Enum.ToObject(t, v);
		return Convert.ChangeType(v, t);
	}

	public static void Run()
	{
		string outPath = Arg("-testOut") ?? "temp_test_out.txt";
		string filter = Arg("-fixtures");
		var wanted = filter == null ? null : new HashSet<string>(filter.Split(';'));
		var sb = new StringBuilder();
		int pass = 0, fail = 0, errLogs = 0;
		var failures = new List<string>();

		var asm = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "Assembly-CSharp");
		var fixtures = asm.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.GetMethods().Any(m => HasAttr(m, "NUnit.Framework.TestAttribute") || HasAttr(m, "NUnit.Framework.TestCaseAttribute")))
			.Where(t => wanted == null || wanted.Contains(t.Name)).OrderBy(t => t.FullName).ToList();

		int logErrors = 0;
		Application.LogCallback cb = (cond, st, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) logErrors++; };
		Application.logMessageReceived += cb;

		sb.AppendLine($"fixtures: {fixtures.Count}");
		foreach (var fx in fixtures)
		{
			object inst;
			try { inst = Activator.CreateInstance(fx); }
			catch (Exception e) { sb.AppendLine($"[FIXTURE-FAIL] {fx.Name}: {e.GetBaseException().Message}"); fail++; continue; }

			var setUps = fx.GetMethods().Where(m => HasAttr(m, "NUnit.Framework.SetUpAttribute")).ToList();
			var tearDowns = fx.GetMethods().Where(m => HasAttr(m, "NUnit.Framework.TearDownAttribute")).ToList();
			int fxPass = 0, fxFail = 0;

			foreach (var m in fx.GetMethods().Where(x => HasAttr(x, "NUnit.Framework.TestAttribute") || HasAttr(x, "NUnit.Framework.TestCaseAttribute")).OrderBy(x => x.MetadataToken))
			{
				var cases = new List<object[]>();
				foreach (var attr in m.GetCustomAttributes(true))
				{
					if (attr.GetType().FullName != "NUnit.Framework.TestCaseAttribute") continue;
					var args = (object[])attr.GetType().GetProperty("Arguments").GetValue(attr);
					cases.Add(args);
				}
				if (cases.Count == 0) cases.Add(new object[0]);

				foreach (var args in cases)
				{
					string name = $"{fx.Name}.{m.Name}" + (args.Length > 0 ? "(" + string.Join(",", args.Select(a => a?.ToString() ?? "null")) + ")" : "");
					logErrors = 0;
					var sw = Stopwatch.StartNew();
					string err = null;
					try
					{
						foreach (var s in setUps) s.Invoke(inst, null);
						var ps = m.GetParameters();
						var callArgs = new object[ps.Length];
						for (int i = 0; i < ps.Length; i++) callArgs[i] = i < args.Length ? Coerce(args[i], ps[i].ParameterType) : null;
						m.Invoke(inst, callArgs);
					}
					catch (TargetInvocationException tie)
					{
						var inner = tie.InnerException;
						var frames = (inner?.StackTrace ?? "").Split('\n').Where(l => l.Contains("Assets")).Take(4).Select(l => l.Trim());
						err = inner?.GetType().Name + ": " + inner?.Message + " @@ " + string.Join(" | ", frames);
					}
					catch (Exception e) { err = e.GetType().Name + ": " + e.Message; }
					finally
					{
						foreach (var td in tearDowns) { try { td.Invoke(inst, null); } catch (Exception e) { err = err ?? ("TearDown " + e.GetBaseException().Message); } }
					}
					sw.Stop();
					errLogs += logErrors;
					if (err == null) { pass++; fxPass++; if (logErrors > 0) sb.AppendLine($"  [PASS+logErr{logErrors}] {name}"); }
					else { fail++; fxFail++; var line = $"[FAIL] {name} :: {err.Replace('\n', ' ')}"; failures.Add(line); sb.AppendLine(line); }
				}
			}
			sb.AppendLine($"== {fx.Name}: pass {fxPass} / fail {fxFail}");
			File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
		}

		Application.logMessageReceived -= cb;
		sb.AppendLine($"TOTAL pass {pass} fail {fail} (Debug.LogError/Exception during passing+failing tests: {errLogs})");
		if (failures.Count > 0) { sb.AppendLine("--- FAILURES ---"); foreach (var f in failures) sb.AppendLine(f); }
		File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
		EditorApplication.Exit(fail > 0 ? 1 : 0);
	}
}
#endif
