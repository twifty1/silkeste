using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace MadelineInPharloom;

/// <summary>
/// Melty started putting its own "Melty Net for Hollow Knight Silksong" plugin into every Play of Silkeste (a Melty Net
/// room, voice, hooks on damage, spawns, saves and TextMeshPro, saves moved to Melty's own folder). Silkeste is
/// single-player, and players got lag and dialogue that never moved on. The creator wants it gone (user 2026-11-09):
/// the plugin is stopped before its Awake runs, and saves it kept in Melty's folder come back to the game's own.
/// </summary>
public static class MeltyNetGuard
{
	private static readonly string[] Callbacks = new string[10] { "Awake", "Start", "OnEnable", "Update", "FixedUpdate", "LateUpdate", "OnGUI", "OnDisable", "OnDestroy", "OnApplicationQuit" };

	private static Harmony harmony;

	private static string pluginsDir;

	public static void Install()
	{
		try
		{
			pluginsDir = Path.GetFullPath(Paths.PluginPath);
			harmony = new Harmony("melty.madelineinpharloom.meltynetguard");
			AppDomain.CurrentDomain.AssemblyLoad += (object sender, AssemblyLoadEventArgs e) => Check(e.LoadedAssembly);
			foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
			{
				Check(asm);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Melty Net guard: " + ex.Message);
		}
		BringSavesBack();
	}

	private static bool IsMeltyNet(Type t)
	{
		if (t == null || !typeof(BaseUnityPlugin).IsAssignableFrom(t))
		{
			return false;
		}
		BepInPlugin info = t.GetCustomAttributes(typeof(BepInPlugin), inherit: false).FirstOrDefault() as BepInPlugin;
		return info != null && info.Name != null && info.Name.StartsWith("Melty Net", StringComparison.OrdinalIgnoreCase);
	}

	private static void Check(Assembly asm)
	{
		string location;
		try
		{
			if (asm.IsDynamic || string.IsNullOrEmpty(asm.Location))
			{
				return;
			}
			location = Path.GetFullPath(asm.Location);
		}
		catch (Exception)
		{
			return;
		}
		// only plugins: looking through the game's own assemblies would cost a lot at start
		if (!location.StartsWith(pluginsDir, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		Type[] types;
		try
		{
			types = asm.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			types = ex.Types;
		}
		catch (Exception)
		{
			return;
		}
		foreach (Type t in types)
		{
			if (IsMeltyNet(t))
			{
				Block(t, asm);
			}
		}
	}

	private static readonly System.Collections.Generic.HashSet<Type> blocked = new System.Collections.Generic.HashSet<Type>();

	private static void Block(Type t, Assembly asm)
	{
		if (!blocked.Add(t))
		{
			return;
		}
		int patched = 0;
		foreach (string name in Callbacks)
		{
			MethodInfo m = AccessTools.DeclaredMethod(t, name, Type.EmptyTypes);
			if (m == null || m.IsAbstract)
			{
				continue;
			}
			try
			{
				harmony.Patch(m, new HarmonyMethod(typeof(MeltyNetGuard), name == "Awake" ? "AwakePrefix" : "SkipPrefix"));
				patched++;
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Melty Net guard: couldn't stop " + t.Name + "." + name + ": " + ex.Message);
			}
		}
		// if it started before Silkeste (another load order), take its hooks back off and stop it
		int unhooked = 0;
		foreach (MethodBase original in Harmony.GetAllPatchedMethods().ToList())
		{
			Patches info = Harmony.GetPatchInfo(original);
			if (info == null)
			{
				continue;
			}
			foreach (Patch p in info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers).ToList())
			{
				if (p.PatchMethod != null && p.PatchMethod.DeclaringType != null && p.PatchMethod.DeclaringType.Assembly == asm)
				{
					try
					{
						harmony.Unpatch(original, p.PatchMethod);
						unhooked++;
					}
					catch (Exception)
					{
					}
				}
			}
		}
		foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Include, FindObjectsSortMode.None))
		{
			UnityEngine.Object.Destroy(o);
		}
		Plugin.Log.LogInfo("Melty Net guard: Melty Net kept out of Silkeste, which is single-player (" + t.FullName + ", " + patched + " callbacks stopped" + ((unhooked > 0) ? (", " + unhooked + " of its hooks taken off") : "") + ")");
	}

	private static bool AwakePrefix(MonoBehaviour __instance)
	{
		UnityEngine.Object.Destroy(__instance);
		return false;
	}

	private static bool SkipPrefix()
	{
		return false;
	}

	/// <summary>
	/// While Melty Net ran, the game saved into Melty's folder instead of its own. A save there that is newer than the
	/// game's own copy is put back (the game's old file kept beside it), so nobody loses what they played through Melty.
	/// </summary>
	private static void BringSavesBack()
	{
		try
		{
			string melty = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeltyNetLayer-hollow-knight-silksong", "saves", "silkeste");
			// once Silkeste has its own save folder (SaveSplit), the game's folder is vanilla's: nothing goes back there
			if (!Directory.Exists(melty) || SaveSplit.AnyMigrated())
			{
				return;
			}
			// Melty copied the player's own save folder there (times kept): the folder most of its files still match is it
			string root = Application.persistentDataPath;
			string[] meltyFiles = Directory.GetFiles(melty);
			var scored = (Directory.Exists(root) ? Directory.GetDirectories(root) : new string[0]).Select((string d) => new
			{
				Dir = d,
				Same = meltyFiles.Count(delegate(string f)
				{
					string mine = Path.Combine(d, Path.GetFileName(f));
					return File.Exists(mine) && Math.Abs((File.GetLastWriteTimeUtc(mine) - File.GetLastWriteTimeUtc(f)).TotalSeconds) < 2.0;
				})
			}).Where(s => s.Same > 0).OrderByDescending(s => s.Same).ToList();
			if (scored.Count == 0 || (scored.Count > 1 && scored[0].Same == scored[1].Same))
			{
				Plugin.Log.LogInfo("Melty Net guard: saves in Melty's folder left there (which of the game's save folders they came from isn't clear)");
				return;
			}
			string own = scored[0].Dir;
			for (int i = 1; i <= 4; i++)
			{
				string src = Path.Combine(melty, "user" + i + ".dat");
				string dst = Path.Combine(own, "user" + i + ".dat");
				if (!File.Exists(src) || (File.Exists(dst) && File.GetLastWriteTimeUtc(src) <= File.GetLastWriteTimeUtc(dst).AddSeconds(2.0)))
				{
					continue;
				}
				if (File.Exists(dst))
				{
					string keep = dst + ".before-melty-net";
					if (File.Exists(keep))
					{
						keep = dst + ".before-melty-net-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
					}
					File.Copy(dst, keep);
				}
				File.Copy(src, dst, overwrite: true);
				Plugin.Log.LogInfo("Melty Net guard: save " + i + " brought back from Melty's folder (it was newer)");
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Melty Net guard: saves: " + ex.Message);
		}
	}
}
