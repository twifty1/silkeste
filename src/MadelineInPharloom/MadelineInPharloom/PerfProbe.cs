using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// Dev only (silkeste_dev.txt): times every Update / LateUpdate / FixedUpdate / OnGUI of Silkeste's own scripts and logs,
/// every 2 seconds, how many milliseconds per frame they take and which ones cost the most (user 2026-10-19: laggy
/// cutscenes and menus).
/// </summary>
internal static class PerfProbe
{
	private static readonly Dictionary<string, long> ticks = new Dictionary<string, long>();

	private static readonly Dictionary<MethodBase, string> names = new Dictionary<MethodBase, string>();

	private static bool installed;

	public static void Install()
	{
		if (installed)
		{
			return;
		}
		installed = true;
		Harmony harmony = new Harmony("melty.madelineinpharloom.perf");
		HarmonyMethod pre = new HarmonyMethod(typeof(PerfProbe), "Pre");
		HarmonyMethod post = new HarmonyMethod(typeof(PerfProbe), "Post");
		int n = 0;
		foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
		{
			string an = asm.GetName().Name;
			if (an != "MadelineInPharloom" && an != "SilkesteExtras" && an != "SilkesteRemix")
			{
				continue;
			}
			Type[] types;
			try
			{
				types = asm.GetTypes();
			}
			catch (ReflectionTypeLoadException e)
			{
				types = e.Types.Where((Type t) => t != null).ToArray();
			}
			foreach (Type t in types)
			{
				if (!typeof(MonoBehaviour).IsAssignableFrom(t) || t.IsAbstract || t.ContainsGenericParameters)
				{
					continue;
				}
				foreach (string m in new string[4] { "Update", "LateUpdate", "FixedUpdate", "OnGUI" })
				{
					MethodInfo mi = t.GetMethod(m, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
					if (mi == null || mi.GetMethodBody() == null)
					{
						continue;
					}
					try
					{
						names[mi] = t.Name + "." + m;
						harmony.Patch(mi, pre, post);
						n++;
					}
					catch (Exception e)
					{
						Plugin.Log.LogWarning("Perf probe: " + t.Name + "." + m + ": " + e.Message);
					}
				}
			}
		}
		Plugin.Log.LogInfo("Perf probe: timing " + n + " Silkeste methods");
	}

	private static void Pre(out long __state)
	{
		__state = Stopwatch.GetTimestamp();
	}

	private static void Post(long __state, MethodBase __originalMethod)
	{
		long d = Stopwatch.GetTimestamp() - __state;
		if (__originalMethod != null && names.TryGetValue(__originalMethod, out string key))
		{
			ticks.TryGetValue(key, out long v);
			ticks[key] = v + d;
		}
	}

	/// <summary>The line for the log: Silkeste's ms per frame and the top five, then the counters start over.</summary>
	public static string Report(int frames)
	{
		if (!installed || frames <= 0)
		{
			return "";
		}
		double perMs = 1000.0 / Stopwatch.Frequency / frames;
		long total = 0;
		foreach (long v in ticks.Values)
		{
			total += v;
		}
		string top = string.Join(", ", ticks.OrderByDescending((KeyValuePair<string, long> kv) => kv.Value).Take(5).Select((KeyValuePair<string, long> kv) => kv.Key + " " + (kv.Value * perMs).ToString("0.0")).ToArray());
		ticks.Clear();
		return "; Silkeste " + (total * perMs).ToString("0.0") + " ms/frame (" + top + ")";
	}
}
